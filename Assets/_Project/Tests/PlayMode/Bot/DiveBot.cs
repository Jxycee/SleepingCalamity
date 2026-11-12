// Authority: test-only (local owner, solo host). A scripted alpha tester for the Cellars dive: it plays a whole solo dive through the same FakeInputService a
// pad would feed (camera-relative stick, the interact edge, attack/dodge through ArenaBot when an enemy is in sight), so it exercises the real input, prompts,
// locomotion, CharacterController, contents, haul and director paths that DiveWalkQaTests skips by teleporting and calling RPCs.
// Route per floor (the 7-floor dive): objective first (fight only what blocks or threatens: within 3.5 m, chasing/attacking within 5 m, the gate-key carrier, a guardian), take the gate key when it drops, open every chest it can reach (key chest first, then the locked door, then the room
// behind it), walk over every piece and the baker, take the stair once the exit unlocks. Floor 5: beat the guardian, rest in the bank room, pick the bankable items it carries (the
// bank screen's pick requests) and push on (the bank screen's Y key through the dev input). Floor 6 like the others; floor 7: walk to the EndCell and finish the dive.
// It walks with its own runtime NavMesh (hero-sized agent) and flags what a human tester would write up: stuck spots (with what blocked the capsule), a press that
// did nothing, a pickup that didn't happen while standing on it, a fall out of the floor, the hero embedded in geometry, a downed soft-lock, errors in the log.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class DiveBot
    {
        public sealed class Finding { public string Kind, Where, Text; public int Floor; public Vector3 Pos; public override string ToString() => $"**{Kind}** F{Floor} at ({Pos.x:0.0}, {Pos.y:0.0}, {Pos.z:0.0}){(string.IsNullOrEmpty(Where) ? "" : " " + Where)}: {Text}"; }

        enum Goal { None, Heal, Kill, Guardian, GateKey, Valve, Pump, PumpHold, Chest, Door, Piece, Bank, Stair, Finish, HubStair }
        sealed class Target { public Goal Goal; public int Index; public Vector3 Pos; public float Range; public string Name; public int Attempts; public float GaveUpAt = -1f, Started, ArrivedAt = -1f; }

        readonly FakeInputService _input;
        readonly Transform _hero;
        readonly HeroLocomotion _loco;
        readonly HeroCombat _combat;
        readonly HeroVitals _vitals;
        readonly CharacterController _cc;
        readonly ArenaBot _fighter;
        public readonly List<Finding> Findings = new List<Finding>();
        public readonly List<string> Log = new List<string>();
        public readonly List<string> HitLog = new List<string>();   // every hit the hero took: floor, damage, HP left, nearest enemy
        readonly HashSet<string> _seen = new HashSet<string>();

        public int Floor { get; private set; }            // the floor the bot is playing (0 = hub)
        public int FloorsCleared, ChestsOpened, PiecesTaken, Presses, StuckEvents, EnemiesKilled;
        public bool PushedOn, Finished, Downed;           // PushedOn: the bank room's push-on went through; Finished: the dive ended at floor 7's EndCell
        public int BankPicks, BankedItems;                 // pick requests sent in the bank room; items the host's bag holds as banked when the push-on went through
        public DiveEndReason? LastEnd;                     // the reason of the last DiveEnded the bot saw
        bool _endHooked;
        public readonly HashSet<int> ClearedFloors = new HashSet<int>();   // floors the bot left by their stair (1-4, 6) and the guardian floor once its guardian fell (5)
        public readonly HashSet<int> EnteredFloors = new HashSet<int>();
        float _bankRoomClock, _pushKeyAt = -1f;
        public bool GoHomeAtBank;                          // PL-1: in the bank room press the go-home button (Secondary) instead of pushing on
        public bool TownExternal;                          // PL-1: a LoopBot owns the town (it presses the trapdoor itself): no hub bake, no hub stair goal
        public bool Tough;                                 // test-only: the host tops the hero up below 50% HP, so the run explores floors 3-4 instead of measuring difficulty
        public int DiveFails, Deaths;
        float _teleportCheckAt = -1f; Bounds _teleportBounds;
        float _bakeAt = -1f; Bounds _bakeArea; bool _fellThisFloor;
        readonly Queue<string> _trail = new Queue<string>(); float _trailClock;
        public float Seconds;

        NavMeshDataInstance _nav;
        bool _hasNav;
        readonly NavMeshPath _path = new NavMeshPath();
        readonly List<Vector3> _corners = new List<Vector3>();
        int _corner;
        Target _target;
        readonly List<Target> _given = new List<Target>();
        float _replanAt, _progressClock, _floorClock, _pressCheckAt = -1f, _downedAt = -1f, _embeddedClock, _fightClock;
        Vector3 _progressPos;
        int _stuckOnTarget;
        bool _pressNext, _releaseNext;
        System.Action _pressCheck;
        int _builtFloorSeen = -1;
                bool _navDoorOpen;
        float _doorSeenAt = -1f;
        int _lastHp = -1;
        float _healSince, _healGiveUpAt = -999f, _lastFaultAt = -999f, _loafAt = -999f;
        public int MaxChestsPerFloor = 2;   // optional (non-key) chests opened per floor
        public int HealTrips, LoavesEaten, FaultDeaths, HealsReached;
        public readonly Dictionary<int, int> HitsByFloor = new Dictionary<int, int>(), VisitsByFloor = new Dictionary<int, int>();

        public DiveBot(FakeInputService input, Transform hero)
        {
            _input = input; _hero = hero;
            _loco = hero.GetComponent<HeroLocomotion>(); _combat = hero.GetComponent<HeroCombat>(); _vitals = hero.GetComponent<HeroVitals>();
            _cc = hero.GetComponent<CharacterController>();
            _fighter = new ArenaBot(input, hero) { Dodges = true, FlankFrontalBlockers = true, DodgeMargin = 0.3f };   // Broom (FrontalBlock): flank to its side, close to strike range, repeat the combo; roll only when the swing really reaches
            _progressPos = hero.position;
        }

        public void Note(string kind, string text, string where = "")
        {
            if (kind == "Fall" || kind == "Entry" || kind == "Nav" || kind == "Embedded") _lastFaultAt = Seconds;   // a floor/traversal fault, not a balance problem
            string key = kind + "|" + Floor + "|" + text;
            if (!_seen.Add(key)) return;
            Findings.Add(new Finding { Kind = kind, Floor = Floor, Pos = _hero.position, Where = where, Text = text });
            Debug.Log("[DIVEBOT] FINDING " + Findings[Findings.Count - 1]);
        }

        void Say(string s) { Log.Add($"{Seconds,7:0.0}s F{Floor}: {s}"); Debug.Log("[DIVEBOT] " + s); }

        static DiveDirector Dir => DiveDirector.Instance;

        // ------------------------------------------------------------------------------------------------ the tick
        public void Tick(float dt)
        {
            Seconds += dt; _floorClock += dt;
            var f = new PlayerInputFrame();
            DiveDirector d = Dir;
            if (d == null) { _input.Frame = f; return; }

            Vitals(dt);
            if (_pressCheck != null && Seconds >= _pressCheckAt) { System.Action c = _pressCheck; _pressCheck = null; c(); }
            if (_releaseNext) { _releaseNext = false; _input.Frame = f; return; }   // one empty frame after a press so the next press is an edge
            if (_pressNext) { _pressNext = false; f.InteractPressed = true; _releaseNext = true; Presses++; _input.Frame = f; return; }
            if (!_vitals.IsUp || d.HasPendingTeleport) { _input.Frame = f; return; }

            SyncFloor(d);
            if (BankRoom(d, dt)) { if (_pushFrame) { _pushFrame = false; if (GoHomeAtBank) f.SecondaryPressed = true; else f.PrimaryPressed = true; } _input.Frame = f; return; }
            if (_bakeAt > 0f && Seconds >= _bakeAt) { _bakeAt = -1f; Bake(_bakeArea); _corners.Clear(); EntryProbe(); }
            if (_bakeAt > 0f) { _input.Frame = f; return; }
            if (_teleportCheckAt > 0f && Seconds >= _teleportCheckAt)
            {
                _teleportCheckAt = -1f;
                if (Floor > 0 && Flat(_hero.position - _teleportBounds.center).magnitude > _teleportBounds.extents.magnitude + 2f)
                    Note("Flow", $"3 s after floor {Floor} built, the hero is still {Flat(_hero.position - _teleportBounds.center).magnitude:0} m from it (not teleported)");
            }

            // a loaf (H) when hurt, at most one per second; the host re-checks everything
            if (_vitals.Loaves > 0 && _vitals.Hp < _vitals.MaxHp * 0.5f && Seconds - _loafAt > 1f) { _loafAt = Seconds; _vitals.RequestEatLoaf(); LoavesEaten++; }
            // hurt: walk to this floor's healing point (a player does) and stand in it; it heals to full for free
            if (_target != null && _target.Goal == Goal.Heal && Done(_target)) { HealsReached++; Say($"healed to {_vitals.Hp}/{_vitals.MaxHp}"); }
            if (_target != null && _target.Goal == Goal.Heal && (Done(_target) || Seconds - _healSince > 25f)) { if (!Done(_target)) _healGiveUpAt = Seconds; _target = null; _corners.Clear(); }
            if ((_target == null || _target.Goal != Goal.Heal) && Floor >= 1 && Seconds - _healGiveUpAt > 60f && _vitals.Hp < _vitals.MaxHp * 0.45f
                && d.HaulSync != null && d.HaulSync.TryHealingPoint(out Vector3 healPos, out bool healReady) && healReady)
            {
                _target = new Target { Goal = Goal.Heal, Pos = healPos, Range = 1.2f, Name = "healing point" }; _corners.Clear(); _healSince = Seconds; HealTrips++;
                Say($"hurt ({_vitals.Hp}/{_vitals.MaxHp}): heading for the healing point at {V(healPos)}");
            }
            bool healing = _target != null && _target.Goal == Goal.Heal;

            // objective first: fight only what blocks or threatens (close, chasing/attacking within 5 m, the gate-key carrier, a guardian); otherwise keep walking (ArenaBot writes the frame itself)
            TrackKills();
            Enemy engaged = healing ? null : PickEngaged(d);
            if (engaged != null)
            {
                _fighter.Preferred = engaged;
                if (!_engagedAt.ContainsKey(engaged.NetworkObjectId)) _engagedAt[engaged.NetworkObjectId] = (engaged, engaged.Def.Id, Seconds);
                _fightClock += dt;
                _fighter.Tick(dt, Seconds);
                if (_fightClock > 60f) { Note("Combat", "fought for 60 s without the room going quiet (an enemy that can't be killed or reached?)"); _fightClock = 0f; }
                _progressPos = _hero.position; _progressClock = 0f;
                return;
            }
            _fightClock = 0f;

            if (_target != null && Done(_target) && _target.Goal == Goal.Piece) PiecesTaken++;
            if (_target != null && _target.Goal == Goal.Kill && Seconds - _target.Started > 90f) { Note("Flow", "stood at the gate-key carrier's cell for 90 s and the key did not drop" + CarrierDiag(d)); GiveUp(_target); }
            if (_target == null || Done(_target)) { _target = NextTarget(d); _corners.Clear(); _stuckOnTarget = 0; if (_target != null) _target.Started = Seconds; if (_target != null) Say($"goal: {_target.Name} at {V(_target.Pos)}"); }
            if (_target == null) { _input.Frame = f; return; }

            float flat = Flat(_target.Pos - _hero.position).magnitude;
            if (flat <= _target.Range * 0.8f)
            {
                Arrive(d, _target, ref f);
                _input.Frame = f;
                return;
            }
            Walk(ref f, dt);
            _input.Frame = f;
        }

        // ------------------------------------------------------------------------------------------------ the bank room
        // While the party is in the bank room the bot stands still: it picks every bankable item it carries into its slots (the bank screen's pick request, through its own bag view), then
        // pushes on with the bank screen's Y key (the dev input). True while the room is open.
        bool BankRoom(DiveDirector d, float dt)
        {
            if (_pushKeyAt >= 0f && Seconds >= _pushKeyAt) _pushKeyAt = -1f;   // push-on retry timer (real Primary edge, one frame)
            DiveHaulSync h = d.HaulSync;
            if (h == null || !d.NetActive.Value || h.Current.DivePhase != DivePhase.BankRoom) { _bankRoomClock = 0f; return false; }
            _bankRoomClock += dt;
            if (_bankRoomClock < 1.5f || !d.BankScreenOpen) return true;
            PlayerBagSync bag = PlayerBagSync.ForLocalPlayer(AppBootstrapManager());
            if (bag == null) return true;
            BagView v = bag.View;
            if (_bankRoomClock < 3.0f)
            {
                if (!_pickedThisVisit)
                {
                    _pickedThisVisit = true;
                    int free = v.SlotCapacity - v.Picks.Length;
                    for (int i = 0; i < v.Carried.Length && free > 0; i++)
                    {
                        if (!BankSlots.IsBankable(CellarLoot.KindOf(v.Carried[i]))) continue;
                        bag.RequestPick(i); free--; BankPicks++;
                    }
                    Say($"bank room: picked {BankPicks} of {v.Carried.Length} carried items into {v.SlotCapacity} slots");
                }
                return true;
            }
            if (_bankRoomClock >= 3.0f && _pushKeyAt < 0f)
            {
                _pushFrame = true; _pushKeyAt = Seconds + 1.0f;   // the real Primary button, one-frame edge (the screen is held only in bank range, so retry each second)
                if (_bankRoomClock > 12f) Note("Interact", "pressed the push-on key in the bank room for 12 s and the room did not close");
            }
            return true;
        }
        bool _pickedThisVisit, _pushFrame;
        static Unity.Netcode.NetworkManager AppBootstrapManager() => SleepingCalamity.App.AppBootstrap.Current != null ? SleepingCalamity.App.AppBootstrap.Current.NetworkManager : null;

        // ------------------------------------------------------------------------------------------------ floors
        void SyncFloor(DiveDirector d)
        {
            bool active = d.NetActive.Value;
            CellarFloorSync fs = d.FloorSync != null ? d.FloorSync : Object.FindAnyObjectByType<CellarFloorSync>();
            int built = active && fs != null && fs.Built != null ? fs.BuiltFloor : 0;
            if (built == _builtFloorSeen) { RebakeIfDoorOpened(d, fs); return; }
            if (built > 0 && Floor > 0 && built != Floor + 1 && built != Floor)
                Note("Flow", $"floor jumped from {Floor} to {built}");
            if (Floor > 0 && built != Floor) { FloorsCleared++; if (built == Floor + 1) ClearedFloors.Add(Floor); Say($"left floor {Floor} after {_floorClock:0} s"); }
            if (built > 0) EnteredFloors.Add(built);
            if (built > 0) VisitsByFloor[built] = (VisitsByFloor.TryGetValue(built, out int vis) ? vis : 0) + 1;
            _builtFloorSeen = built; Floor = built; _floorClock = 0f; _given.Clear(); _target = null; _corners.Clear();
            if (built > 0)
            {
                Bounds b = fs.Built.Bounds;
                _teleportCheckAt = Seconds + 3f; _teleportBounds = b;
                _bakeAt = Seconds + 0.5f; _bakeArea = new Bounds(new Vector3(b.center.x, b.min.y + 2f, b.center.z), new Vector3(b.size.x + 4f, 12f, b.size.z + 4f));   // after the old floor's objects are gone
                _fellThisFloor = false;
                _navDoorOpen = Contents(d) != null && Contents(d).State.DoorOpen;
                Say($"floor {built} seed {fs.Layout.Seed} preset {fs.Layout.Preset} rooms {fs.Layout.Rooms.Count} chests {fs.Layout.Chests.Count} locked {fs.Layout.HasLockedRoom}");
            }
            else
            {
                Vector3 hub = (d.NetHome.Value + d.NetHubStair.Value) * 0.5f;
                if (!TownExternal) Bake(new Bounds(hub, new Vector3(70f, 20f, 70f)));
                if (Finished) Say("back in the hub");
            }
        }

        void RebakeIfDoorOpened(DiveDirector d, CellarFloorSync fs)
        {
            CellarContentsSync c = Contents(d);
            if (Floor <= 0 || c == null || fs == null || fs.Built == null) return;
            if (c.State.DoorOpen && !_navDoorOpen)
            {
                // CellarContentsSync hides the gate in its own Update, a frame after DoorOpen flips: wait (up to 2 s) until the gate stops blocking, then bake once.
                if (_doorSeenAt < 0f) _doorSeenAt = Seconds;
                bool timedOut = Seconds - _doorSeenAt > 2f;
                if (c.GateBlocking && !timedOut) return;
                _navDoorOpen = true; _doorSeenAt = -1f;
                Bounds b = fs.Built.Bounds;
                Bake(new Bounds(new Vector3(b.center.x, b.min.y + 2f, b.center.z), new Vector3(b.size.x + 4f, 12f, b.size.z + 4f)));
                Say("locked door opened: NavMesh re-baked");
                if (c.GateBlocking) Note("Door", "the door reads open (DoorOpen) but its gate object still blocks after 2 s");
            }
        }

        static CellarContentsSync Contents(DiveDirector d) => d.ContentsSync != null ? d.ContentsSync : Object.FindAnyObjectByType<CellarContentsSync>();

        void Bake(Bounds area)
        {
            if (_hasNav) { _nav.Remove(); _hasNav = false; }
            var sources = new List<NavMeshBuildSource>();
            int mask = ~((1 << 6) | (1 << 7) | (1 << 11) | (1 << 2));   // not players, remote players, enemies or Ignore Raycast
            NavMeshBuilder.CollectSources(area, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || c.GetComponentInParent<Enemy>() != null || c.transform.root == _hero.root));
            NavMeshBuildSettings st = NavMesh.GetSettingsByID(0);
            st.agentRadius = _cc.radius; st.agentHeight = _cc.height; st.agentClimb = _cc.stepOffset; st.agentSlope = _cc.slopeLimit;
            st.overrideVoxelSize = true; st.voxelSize = 0.1f; st.minRegionArea = 0.25f;
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(st, sources, area, Vector3.zero, Quaternion.identity);
            if (data == null) { Note("Bot", "runtime NavMesh bake failed"); return; }
            _nav = NavMesh.AddNavMeshData(data); _hasNav = true;
        }

        // Right after the floor's NavMesh: is the hero (at its entry start) on walkable ground, and what is around it?
        void EntryProbe()
        {
            if (Floor <= 0) return;
            bool onNav = NavMesh.SamplePosition(_hero.position, out NavMeshHit h, 2f, NavMesh.AllAreas);
            if (onNav) return;
            float r = _cc.radius;
            var around = Physics.OverlapSphere(_hero.position + Vector3.up * 1f, 1.5f, ~((1 << 6) | (1 << 7) | (1 << 11)), QueryTriggerInteraction.Ignore).Where(c => c.transform.root != _hero.root).Select(c => Path(c.transform) + " " + c.GetType().Name).Take(8);
            bool ground = Physics.Raycast(_hero.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit gh, 3f, ~((1 << 6) | (1 << 7) | (1 << 11)), QueryTriggerInteraction.Ignore);
            Note("Entry", $"0.5 s after arriving on floor {Floor}, no walkable ground within 2 m of the hero at {V(_hero.position)}; ground below: {(ground ? Path(gh.collider.transform) + " at y " + gh.point.y.ToString("0.00") : "none")}; colliders within 1.5 m: {string.Join(", ", around)}", PlanWhere());
        }

        public void Dispose() { if (_hasNav) { _nav.Remove(); _hasNav = false; } }

        // ------------------------------------------------------------------------------------------------ goals
        Target NextTarget(DiveDirector d)
        {
            var list = new List<Target>();
            if (Floor == 0)
            {
                if (Finished || TownExternal) return null;
                list.Add(new Target { Goal = Goal.HubStair, Pos = d.NetHubStair.Value, Range = DiveDirector.PromptRadius - 0.5f, Name = "hub stair" });
                return list[0];
            }
            CellarFloorSync fs = d.FloorSync;
            CellarContentsSync c = Contents(d);
            if (fs == null || fs.Layout == null || c == null) return null;
            CellarFloorLayout l = fs.Layout;
            var s = c.State;
            bool stateOk = s.Seed == l.Seed && s.Floor == l.Floor;
            if (stateOk && s.GateKey == 1) list.Add(new Target { Goal = Goal.GateKey, Pos = s.GateKeyPos, Range = CellarContentsSync.GateKeyRange, Name = "gate key" });
            int opened = 0;
            if (stateOk) for (int i = 0; i < l.Chests.Count; i++) if (c.ChestOpenForLocal(i)) opened++;   // per player: a loot chest is open once THIS player (the bot) opened it (plan 2 task 6)
            for (int i = 0; i < l.Chests.Count; i++)
            {
                bool open = stateOk && c.ChestOpenForLocal(i);
                if (open) continue;
                bool locked = l.HasLockedRoom && l.Chests[i].RoomIndex == l.LockedRoomIndex;
                if (locked && !(stateOk && s.DoorOpen)) continue;
                if (!l.Chests[i].HoldsKey && opened >= MaxChestsPerFloor) continue;   // a player heading for the goal opens the chests on the way, not all of them
                if (c.TryChestPosition(i, out Vector3 p)) list.Add(new Target { Goal = Goal.Chest, Index = i, Pos = p, Range = CellarContentsSync.ChestRange, Name = (l.Chests[i].HoldsKey ? "key chest " : "chest ") + i + (locked ? " (locked room)" : "") });
            }
            // the exit stays locked until the gate-key carrier dies: a player heads for its room (the room spawns when you walk in)
            // plan 5: TwoValves / HoldPump floors. Objective first: walk to the nearest unturned valve and hold interact until its bit sets; walk to the pump, press once, then stay in its ring (the fight logic
            // handles the waves) until the exit unlocks. The carrier opens nothing on these floors, so the bot does not hunt it.
            bool valveFloor = stateOk && s.Objective == (byte)FloorObjective.TwoValves, pumpFloor = stateOk && s.Objective == (byte)FloorObjective.HoldPump;
            if (valveFloor && !s.ExitUnlocked)
                for (int i = 0; i < ValveHold.Count; i++)
                    if ((s.ValveMask & (1 << i)) == 0 && c.TryValvePosition(i, out Vector3 vp)) list.Add(new Target { Goal = Goal.Valve, Index = i, Pos = vp, Range = FloorObjectiveTuning.ValveReachMeters, Name = "valve " + i });
            if (pumpFloor && !s.ExitUnlocked && c.TryPumpPosition(out Vector3 pumpPos))
                list.Add(s.PumpStarted ? new Target { Goal = Goal.PumpHold, Pos = pumpPos, Range = 2f, Name = "the pump (holding)" } : new Target { Goal = Goal.Pump, Pos = pumpPos, Range = FloorObjectiveTuning.PumpStartReach, Name = "the pump" });
            if (stateOk && !valveFloor && !pumpFloor && s.GateKey == 0 && !s.ExitUnlocked && l.Carrier != null && fs.Built != null && fs.Built.Root != null
                && !(l.HasLockedRoom && l.Carrier.RoomIndex == l.LockedRoomIndex && !s.DoorOpen))
                list.Add(new Target { Goal = Goal.Kill, Pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(l.Carrier.X, l.Carrier.Y)), Range = 1.5f, Name = "gate-key carrier" });
            if (stateOk && s.RoomKeyHeld && !s.DoorOpen && c.TryGatePosition(out Vector3 gp)) list.Add(new Target { Goal = Goal.Door, Pos = gp, Range = CellarContentsSync.GateRange, Name = "locked door" });
            for (int i = 0; i < c.PieceCount; i++)
                if (c.PieceVisible(i) && c.TryPiecePosition(i, out Vector3 pp)) list.Add(new Target { Goal = Goal.Piece, Index = i, Pos = pp, Range = CellarContentsSync.PieceRange, Name = "piece " + i });
            GuardianAndBankTargets(d, fs, list);
            if (list.Count(t => t.Goal != Goal.Stair && t.Goal != Goal.Finish) == 0 || _floorClock > 240f)
            {
                if (DiveFloorTable.HasStair(Floor, PushedOn) && CellarFloorBuilder.TryDescentWorld(l, fs.Built, out Vector3 st))
                {
                    // the bank floor's stair is not a gate-key stair: it opens with the push-on (NetStairOpen), the contents' ExitUnlocked stays false
                    bool bankStairOpen = Floor == UndercroftPlan.BankFloor && PushedOn && d.NetStairOpen.Value;
                    if ((stateOk && s.ExitUnlocked) || bankStairOpen) list.Add(new Target { Goal = Goal.Stair, Pos = st, Range = DiveDirector.PromptRadius - 0.5f, Name = "stair" });
                    else if (_floorClock > 150f) Note("Flow", $"floor {Floor}: everything taken or tried but the exit is still locked after {_floorClock:0} s (gate key {(stateOk ? s.GateKey.ToString() : "?")})");
                }
                if (Floor == DiveFloorTable.LastBuiltFloor && CellarFloorBuilder.TryEndWorld(l, fs.Built, out Vector3 end))
                    list.Add(new Target { Goal = Goal.Finish, Pos = end, Range = DiveDirector.PromptRadius - 0.5f, Name = "the ending door" });
            }
            // skip targets given up on in the last 30 s
            list.RemoveAll(t => _given.Any(g => g.Goal == t.Goal && g.Index == t.Index && Seconds - g.GaveUpAt < 30f));
            if (list.Count == 0) return null;
            return list.OrderBy(t => Order(t.Goal)).ThenBy(t => Flat(t.Pos - _hero.position).magnitude).First();
        }

        // Floor 5: the guardian first (its room spawns it when the bot walks in), then the bank room once the floor is cleared and the choice is still open.
        void GuardianAndBankTargets(DiveDirector d, CellarFloorSync fs, List<Target> list)
        {
            if (Floor != UndercroftPlan.BankFloor || d.HaulSync == null) return;
            DiveHaulSync.Summary sum = d.HaulSync.Current;
            bool cleared = (sum.ClearedMask & (1 << (UndercroftPlan.BankFloor - 1))) != 0;
            if (!cleared && fs.Layout.WardenCell.HasValue && fs.Built != null && fs.Built.Root != null)
            {
                list.Add(new Target { Goal = Goal.Guardian, Pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(fs.Layout.WardenCell.Value.X, fs.Layout.WardenCell.Value.Y)), Range = 1.5f, Name = "the guardian" });
                return;
            }
            if (cleared) ClearedFloors.Add(Floor);
            if (!cleared || sum.BankChosen || PushedOn || sum.DivePhase == DivePhase.BankRoom || !CellarFloorBuilder.TryBankWorld(fs.Layout, fs.Built, out Vector3 bank)) return;
            // stand west of the lamp: the green go-home column is east of it, and a press near the column would take the party home
            list.Add(new Target { Goal = Goal.Bank, Pos = bank + new Vector3(-1.2f, 0f, 0f), Range = 1.2f, Name = "bank room" });
        }

        static int Order(Goal g) => g switch { Goal.GateKey => 0, Goal.Valve => 0, Goal.Pump => 0, Goal.PumpHold => 0, Goal.Chest => 1, Goal.Kill => 2, Goal.Door => 2, Goal.Guardian => 2, Goal.Piece => 3, Goal.Bank => 4, Goal.Stair => 5, Goal.Finish => 6, _ => 9 };

        bool Done(Target t)
        {
            DiveDirector d = Dir; CellarContentsSync c = Contents(d);
            var s = c != null ? c.State : default;
            switch (t.Goal)
            {
                case Goal.Kill: return s.GateKey != 0 || s.ExitUnlocked;
                case Goal.Heal: return _vitals.Hp >= _vitals.MaxHp * 0.9f;
                case Goal.Chest: return c != null && c.ChestOpenForLocal(t.Index);
                case Goal.GateKey: return s.GateKey != 1;
                case Goal.Door: return s.DoorOpen;
                case Goal.Valve: return (s.ValveMask & (1 << t.Index)) != 0 || s.ExitUnlocked;
                case Goal.Pump: return s.PumpStarted || s.ExitUnlocked;
                case Goal.PumpHold: return s.ExitUnlocked;
                case Goal.Piece: return c == null || !c.PieceVisible(t.Index);
                case Goal.Guardian: return d != null && d.HaulSync != null && (d.HaulSync.Current.ClearedMask & (1 << (UndercroftPlan.BankFloor - 1))) != 0;
                case Goal.Bank: return d != null && d.HaulSync != null && (d.HaulSync.Current.DivePhase == DivePhase.BankRoom || d.HaulSync.Current.BankChosen);
                case Goal.Finish: return Finished;
                case Goal.HubStair: return d != null && d.NetActive.Value;
                default: return false;
            }
        }

        // In range: press for things that take a press, check pickups that should just happen.
        void Arrive(DiveDirector d, Target t, ref PlayerInputFrame f)
        {
            CellarContentsSync c = Contents(d);
            switch (t.Goal)
            {
                case Goal.Piece:
                case Goal.GateKey:
                    // walk right onto it, then give the host a moment
                    if (Flat(t.Pos - _hero.position).magnitude > 0.35f) { Steer(ref f, Flat(t.Pos - _hero.position).normalized * 0.5f); return; }
                    if (_pressCheck == null)
                    {
                        Target tt = t; int fl = Floor;
                        Check(2.0f, () => { if (!Done(tt) && Floor == fl) Note("Pickup", $"{tt.Name} at {V(tt.Pos)} not picked up while standing on it ({Flat(tt.Pos - _hero.position).magnitude:0.00} m away, range {tt.Range:0.0} m)"); GiveUp(tt); });
                    }
                    return;
                case Goal.Chest:
                    Press(t, () =>
                    {
                        if (!Done(t)) { Note("Interact", $"pressed interact at {t.Name} ({Flat(t.Pos - _hero.position).magnitude:0.0} m, range {t.Range:0.0}) and it didn't open; prompt was '{d.PromptText}'"); GiveUp(t); }
                        else { ChestsOpened++; if (c != null && !c.ChestShownOpen(t.Index)) Note("Visual", $"{t.Name} opened in the state but its model doesn't show open"); }
                    });
                    return;
                case Goal.Valve:
                    // hold interact (the real held input; the owner reports every 0.2 s, the host turns it after 2 s); the fight logic interrupts it when an enemy engages, the hold then restarts
                    f.InteractHeld = true;
                    if (t.ArrivedAt < 0f) t.ArrivedAt = Seconds;
                    if (Seconds - t.ArrivedAt > 40f) { Note("Interact", $"held interact at {t.Name} ({Flat(t.Pos - _hero.position).magnitude:0.0} m, reach {FloorObjectiveTuning.ValveReachMeters:0.0}) for 40 s and it did not turn; prompt was '{d.PromptText}'"); GiveUp(t); }
                    return;
                case Goal.Pump:
                    Press(t, () => { if (!Done(t)) { Note("Interact", $"pressed interact at the pump ({Flat(t.Pos - _hero.position).magnitude:0.0} m, reach {FloorObjectiveTuning.PumpStartReach:0.0}) and it did not start"); GiveUp(t); } });
                    return;
                case Goal.PumpHold:
                    // stand in the ring; the fight logic takes over whenever a wave enemy engages. The pump needs about 75 s (more when enemies stand at it).
                    if (t.ArrivedAt < 0f) t.ArrivedAt = Seconds;
                    if (Seconds - t.ArrivedAt > 240f) { Note("Flow", $"stood at the pump for 240 s and the exit never unlocked (progress {(c != null ? c.PumpProgress : 0f):0.00})"); GiveUp(t); }
                    return;
                case Goal.Door:
                    Press(t, () => { if (!Done(t)) { Note("Interact", $"held the room key, pressed interact at the locked door ({Flat(t.Pos - _hero.position).magnitude:0.0} m) and it stayed shut"); GiveUp(t); } });
                    return;
                case Goal.Guardian:
                    // walking into the guardian's room spawns it and the fight logic takes over; once in range with nothing in sight, wait for it
                    if (_target != null && _target.Started > 0f && Seconds - _target.Started > 120f) { Note("Flow", "stood at the guardian's cell for 120 s and the floor was not cleared"); GiveUp(t); }
                    return;
                case Goal.Bank:
                    if (d.Prompt != DiveDirector.PromptKind.Bank) { Note("Prompt", $"standing {Flat(t.Pos - _hero.position).magnitude:0.0} m from the {t.Name} but the prompt is '{d.Prompt}' ('{d.PromptText}')"); }
                    Press(t, () =>
                    {
                        if (!Done(t)) { Note("Interact", "pressed interact at the bank room's lamp on the cleared bank floor and the room did not open"); GiveUp(t); }
                    }, 3f);
                    return;
                case Goal.Stair:
                    int from = Floor;
                    if (d.Prompt != DiveDirector.PromptKind.Stair) Note("Prompt", $"at the stair ({Flat(t.Pos - _hero.position).magnitude:0.0} m) with the exit unlocked but the prompt is '{d.Prompt}' ('{d.PromptText}')");
                    Press(t, () => { if (Floor == from && Dir.FloorSync.BuiltFloor == from) { Note("Interact", "pressed interact at the stair with the exit unlocked and stayed on the floor (6 s)"); GiveUp(t); } }, 6f);
                    return;
                case Goal.Finish:
                    if (d.Prompt != DiveDirector.PromptKind.Finish) Note("Prompt", $"at the ending door ({Flat(t.Pos - _hero.position).magnitude:0.0} m) but the prompt is '{d.Prompt}' ('{d.PromptText}')");
                    Press(t, () =>
                    {
                        Finished = !Dir.NetActive.Value;
                        if (!Finished) { Note("Interact", "pressed interact at the ending door on floor 7 and the dive is still active"); GiveUp(t); }
                        else Say("finished the dive");
                    }, 6f);
                    return;
                case Goal.HubStair:
                    if (d.Prompt != DiveDirector.PromptKind.Descend) Note("Prompt", $"at the hub stair ({Flat(t.Pos - _hero.position).magnitude:0.0} m) but the prompt is '{d.Prompt}'", "hub");
                    Press(t, () => { if (!Dir.NetActive.Value) { Note("Interact", "pressed interact at the hub stair and no dive started (6 s)", "hub"); GiveUp(t); } }, 6f);
                    return;
            }
        }

        void Press(Target t, System.Action check, float wait = 1.5f)
        {
            if (_pressCheck != null) return;
            t.Attempts++;
            if (t.Attempts > 3) { GiveUp(t); return; }
            _pressNext = true;
            Check(wait, check);
        }

        void Check(float after, System.Action c) { _pressCheck = c; _pressCheckAt = Seconds + after; }

        void GiveUp(Target t)
        {
            t.GaveUpAt = Seconds; _given.Add(t);
            if (_target == t) { _target = null; _corners.Clear(); }
        }

        // ------------------------------------------------------------------------------------------------ walking
        void Walk(ref PlayerInputFrame f, float dt)
        {
            if (!_hasNav) { Steer(ref f, Flat(_target.Pos - _hero.position).normalized); return; }
            if (_corners.Count == 0 || Seconds >= _replanAt) Plan();
            if (_corners.Count == 0) return;
            while (_corner < _corners.Count - 1 && Flat(_corners[_corner] - _hero.position).magnitude < 0.45f) _corner++;
            Vector3 to = Flat(_corners[_corner] - _hero.position);
            Steer(ref f, to.normalized);

            _progressClock += dt;
            if (_progressClock >= 2.5f)
            {
                float moved = Flat(_hero.position - _progressPos).magnitude;
                if (moved < 0.4f && _combat.MoveScale > 0.5f)
                {
                    StuckEvents++; _stuckOnTarget++;
                    string why = Blocker(to.normalized);
                    if (_stuckOnTarget == 2) Note("Stuck", $"walking to {_target.Name} at {V(_target.Pos)}: moved {moved:0.00} m in 2.5 s; capsule blocked by {why}", PlanWhere());
                    f.JumpPressed = true;   // try to get unstuck
                    _replanAt = 0f;
                    if (_stuckOnTarget >= 4) { Note("Unreachable", $"gave up on {_target.Name} at {V(_target.Pos)} after {_stuckOnTarget} stuck spells (last blocker {why})", PlanWhere()); GiveUp(_target); }
                }
                _progressClock = 0f; _progressPos = _hero.position;
            }
        }

        void Plan()
        {
            _replanAt = Seconds + 1.5f; _corners.Clear(); _corner = 0;
            if (!NavMesh.SamplePosition(_hero.position, out NavMeshHit from, 2f, NavMesh.AllAreas)) { _corners.Add(_target.Pos); if (_floorClock > 2f) Note("Nav", "the hero is off the walkable area (no NavMesh within 2 m)", PlanWhere()); return; }
            if (!NavMesh.SamplePosition(_target.Pos, out NavMeshHit to, _target.Range + 0.5f, NavMesh.AllAreas)) { Note("Nav", $"no walkable ground within {_target.Range + 0.5f:0.0} m of {_target.Name} at {V(_target.Pos)}"); GiveUp(_target); return; }
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                _target.Attempts++;
                if (_target.Attempts >= 3) { Note("Nav", $"no walkable path from the hero to {_target.Name} at {V(_target.Pos)} ({_path.status})", PlanWhere()); GiveUp(_target); return; }
            }
            _corners.AddRange(_path.corners);
            if (_corners.Count == 0) _corners.Add(_target.Pos);
        }

        string PlanWhere()
        {
            DiveDirector d = Dir;
            if (Floor <= 0 || d == null || d.FloorSync == null || d.FloorSync.Layout == null || d.FloorSync.Built == null) return "";
            Vector3 local = d.FloorSync.Built.Root.InverseTransformPoint(_hero.position);
            int gx = Mathf.FloorToInt(local.x / CellarFloorBuilder.CellSize), gy = Mathf.FloorToInt(-local.z / CellarFloorBuilder.CellSize);
            CellarFloorLayout l = d.FloorSync.Layout;
            for (int i = 0; i < l.Rooms.Count; i++) { PlacedRoom r = l.Rooms[i]; if (gx >= r.X && gy >= r.Y && gx < r.X + r.Width && gy < r.Y + r.Height) return $"(seed {l.Seed}, room {r.RoomId} cell ({gx - r.X},{gy - r.Y}))"; }
            return $"(seed {l.Seed}, cell ({gx},{gy}))";
        }

        void Steer(ref PlayerInputFrame f, Vector3 worldDir)
        {
            float yaw = _loco.ViewYawDeg * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            f.MoveX = Mathf.Clamp(Vector3.Dot(worldDir, right), -1f, 1f);
            f.MoveY = Mathf.Clamp(Vector3.Dot(worldDir, forward), -1f, 1f);
        }

        string Blocker(Vector3 dir)
        {
            float r = _cc.radius;
            Vector3 a = _hero.position + Vector3.up * (r + _cc.stepOffset), b = _hero.position + Vector3.up * (_cc.height - r);
            foreach (RaycastHit h in Physics.CapsuleCastAll(a, b, r * 0.95f, dir, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.transform.root != _hero.root) return $"{Path(h.collider.transform)} ({h.collider.GetType().Name}, {h.distance:0.00} m)";
            if (!Physics.Raycast(_hero.position + Vector3.up * 0.3f, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore)) return "nothing (no ground under the hero)";
            return "nothing at body height (a ledge or step under the step offset? CharacterController snag)";
        }

        static string Path(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        bool InSight(Enemy e, float range)
        {
            Vector3 eye = _hero.position + Vector3.up * 1.2f;
            Vector3 to = e.transform.position + Vector3.up * 0.8f - eye;
            if (Flat(to).magnitude > range || Mathf.Abs(to.y) > 3f) return false;
            foreach (RaycastHit h in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.transform.root != _hero.root && h.collider.GetComponentInParent<Enemy>() == null) return false;
            return true;
        }

        const float SightRange = 9f, CloseRange = 3.5f, ChaseRange = 5f;
        Enemy _engagedEnemy;
        readonly Dictionary<ulong, (Enemy E, string Id, float Start)> _engagedAt = new Dictionary<ulong, (Enemy, string, float)>();
        readonly Dictionary<string, List<float>> _killTimes = new Dictionary<string, List<float>>();

        // The enemy worth stopping for, or null. Sticky: once engaged, the bot fights it until it dies or leaves sight (no flapping at the range edges).
        Enemy PickEngaged(DiveDirector d)
        {
            if (_engagedEnemy != null && _engagedEnemy.IsSpawned && _engagedEnemy.Alive && InSight(_engagedEnemy, SightRange)) return _engagedEnemy;
            _engagedEnemy = null;
            CellarContentsSync c = Contents(d);
            Enemy carrier = c != null ? c.GateKeyCarrierEnemy : null;
            Enemy best = null; float bestScore = float.MaxValue;
            foreach (Enemy e in Enemy.All)
            {
                if (e == null || !e.IsSpawned || !e.Alive) continue;
                float dist = Flat(e.transform.position - _hero.position).magnitude;
                if (dist > SightRange) continue;
                bool key = e == carrier || e.Def.Kind == EnemyKind.DoughWarden || e.Def.IsElite;   // the gate-key carrier and guardians stand between the bot and the exit
                bool threat = dist <= CloseRange || (dist <= ChaseRange && (e.Phase == EnemyPhase.Chase || e.Attacking));
                if (!key && !threat) continue;
                if (!InSight(e, SightRange)) continue;
                float score = (key ? 0f : 100f) + dist;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return _engagedEnemy = best;
        }

        // Kill time per enemy type: seconds from the first engage to death (removed once it is dead or gone).
        void TrackKills()
        {
            if (_engagedAt.Count == 0) return;
            List<ulong> done = null;
            foreach (var kv in _engagedAt)
                if (kv.Value.E == null || !kv.Value.E.IsSpawned || !kv.Value.E.Alive)
                {
                    if (!_killTimes.TryGetValue(kv.Value.Id, out List<float> l)) _killTimes[kv.Value.Id] = l = new List<float>();
                    l.Add(Seconds - kv.Value.Start); EnemiesKilled++;
                    (done ??= new List<ulong>()).Add(kv.Key);
                }
            if (done != null) foreach (ulong k in done) _engagedAt.Remove(k);
        }

        public string KillTimes() => _killTimes.Count == 0 ? "none" : string.Join(", ", _killTimes.OrderBy(k => k.Key).Select(k => $"{k.Key} n={k.Value.Count} avg {k.Value.Average():0.0} s max {k.Value.Max():0.0} s"));

        // ------------------------------------------------------------------------------------------------ watchers
        bool _wasActive;
        void Vitals(float dt)
        {
            DiveDirector dd = Dir;
            bool active = dd != null && dd.NetActive.Value;
            if (dd != null && !_endHooked) { _endHooked = true; dd.DiveEnded += r => LastEnd = r; }
            if (!PushedOn && active && dd.HaulSync != null && dd.HaulSync.Current.BankChosen)
            {
                PushedOn = true;
                PlayerBag mine = dd.HaulSync.ServerBagOf(dd.NetworkManager.LocalClientId);   // the host's own bag (a solo run): what the host accepted
                if (mine != null) { BankedItems = 0; foreach (var kv in mine.BankedThisDive) BankedItems += kv.Value; }
                Say($"pushed on ({BankPicks} picks asked, {BankedItems} items safe in the bag)");
            }
            if (_wasActive && !active && LastEnd == DiveEndReason.Finished && !Finished) { Finished = true; Say("finished the dive (the host ended it Finished)"); }
            if (_wasActive && !active && !Finished) { DiveFails++; if (Seconds - _lastFaultAt < 20f) FaultDeaths++; Say($"the dive ended without going home (fail #{DiveFails}{(Seconds - _lastFaultAt < 20f ? ", floor/traversal fault in the last 20 s: not a balance death" : "")}); HP {_vitals.Hp}, last HP {_lastHp}"); if (_lastHp >= 0 && _lastHp < _vitals.MaxHp * 0.35f) Deaths++; }
            _wasActive = active;
            if (Tough && _vitals.IsUp && _vitals.Hp < _vitals.MaxHp / 2 && _vitals.IsServer) _vitals.ServerHeal(_vitals.MaxHp);
            if (_lastHp >= 0 && _vitals.Hp < _lastHp && !EnemyNear(4f)) Note("Combat", $"took {_lastHp - _vitals.Hp} damage with no enemy within 4 m");
            if (_lastHp >= 0 && _vitals.Hp < _lastHp)
            {
                Enemy near = null; float best = 8f;
                foreach (Enemy e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                { float d = Flat(e.transform.position - _hero.position).magnitude; if (d < best) { best = d; near = e; } }
                HitsByFloor[Floor] = (HitsByFloor.TryGetValue(Floor, out int hf) ? hf : 0) + 1;
                HitLog.Add($"F{Floor} -{_lastHp - _vitals.Hp} ({_vitals.Hp}/{_vitals.MaxHp}) by {(near != null ? near.Def.Id + " tier " + near.Model?.Tier + " at " + best.ToString("0.0") + " m" : "nothing within 8 m")} [src {(_vitals.LastHitSource.Length > 0 ? _vitals.LastHitSource : "?")}]");
            }
            _lastHp = _vitals.Hp;
            if (!_vitals.IsUp)
            {
                if (_downedAt < 0f) { _downedAt = Seconds; Downed = true; Say($"downed (HP {_vitals.Hp})"); }
                else if (Seconds - _downedAt > 30f) { Note("SoftLock", "solo hero downed for 30 s and nothing happened (no revive, no dive fail, no return to the hub)"); _downedAt = Seconds; }
            }
            else if (_downedAt >= 0f) { Say($"back up after {Seconds - _downedAt:0} s, floor {Floor}, dive active {Dir?.NetActive.Value}"); _downedAt = -1f; }

            CellarFloorSync fs = Dir != null ? Dir.FloorSync : null;
            _trailClock += dt;
            if (_trailClock >= 0.25f)
            {
                _trailClock = 0f;
                _trail.Enqueue($"{Seconds:0.00}s {V(_hero.position)} cc {(_cc.enabled ? "on" : "OFF")} grounded {_cc.isGrounded} up {_vitals.IsUp} tp {Dir?.HasPendingTeleport} stick {_input.Frame.MoveX:0.0},{_input.Frame.MoveY:0.0} goal {_target?.Name}");
                while (_trail.Count > 40) _trail.Dequeue();
            }
            if (Floor > 0 && fs != null && fs.Built != null && _hero.position.y < fs.Built.Root.position.y - 1.5f && !_fellThisFloor)
            {
                _fellThisFloor = true;
                bool floorBelowStart = false;
                Note("Fall", $"the hero dropped below the floor (y {_hero.position.y:0.0}); last 10 s: " + string.Join(" | ", _trail), PlanWhere());
                _ = floorBelowStart;
            }

            // embedded: the capsule overlaps solid geometry (not itself, not enemies) for over a second
            float r = _cc.radius - 0.08f;
            Vector3 a = _hero.position + Vector3.up * (r + _cc.stepOffset), b = _hero.position + Vector3.up * (_cc.height - r - 0.05f);
            bool inside = Physics.OverlapCapsule(a, b, r, ~((1 << 6) | (1 << 7) | (1 << 11)), QueryTriggerInteraction.Ignore).Any(col => col.transform.root != _hero.root && col.GetComponentInParent<Enemy>() == null);
            _embeddedClock = inside ? _embeddedClock + dt : 0f;
            if (_embeddedClock > 1f)
            {
                Collider col = Physics.OverlapCapsule(a, b, r, ~((1 << 6) | (1 << 7) | (1 << 11)), QueryTriggerInteraction.Ignore).First(x => x.transform.root != _hero.root && x.GetComponentInParent<Enemy>() == null);
                Note("Embedded", $"the hero's capsule sits inside {Path(col.transform)} for over a second", PlanWhere());
                _embeddedClock = -5f;
            }
        }

        // What the host knows about the gate-key carrier when the bot has waited for it in vain: the carrier enemy (null = never spawned or lost), its room's entered/cleared flags, and every live enemy's position.
        string CarrierDiag(DiveDirector d)
        {
            CellarContentsSync c = Contents(d); if (c == null) return "";
            Enemy ce = c.GateKeyCarrierEnemy;
            string all = string.Join(" ", Enemy.All.Where(e => e != null).Select(e => $"{e.NetKind.Value}@{V(e.transform.position)}{(e.IsSpawned ? "" : "!unspawned")}{(e.Alive ? "" : "!dead")}"));
            return $" [carrier enemy {(ce == null ? "none" : V(ce.transform.position) + (ce.Alive ? " alive" : " dead"))}; state {c.State}; enemies {all}]";
        }

        bool EnemyNear(float range)
        {
            foreach (Enemy e in Enemy.All) if (e != null && e.IsSpawned && Flat(e.transform.position - _hero.position).magnitude < range + 1.5f) return true;
            return false;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static string V(Vector3 p) => $"({p.x:0.0}, {p.y:0.0}, {p.z:0.0})";

        public string Summary() =>
            $"{(Tough ? "TOUGH (host top-ups) " : "")}{Seconds:0} s, dive fails {DiveFails} (after low HP {Deaths}), floors cleared {FloorsCleared} ({string.Join(",", ClearedFloors.OrderBy(x => x))}), chests {ChestsOpened}, bank picks {BankPicks} ({BankedItems} accepted), pushed on {PushedOn}, pieces {PiecesTaken}, presses {Presses}, heal trips {HealTrips} (reached {HealsReached}), loaves eaten {LoavesEaten}, fails near a floor fault {FaultDeaths}, hits per floor {string.Join(", ", HitsByFloor.OrderBy(k => k.Key).Select(k => "F" + k.Key + " " + k.Value + " in " + (VisitsByFloor.TryGetValue(k.Key, out int vv) ? vv : 0) + " visits"))}, stuck spells {StuckEvents}, downed {Downed}, finished {Finished}, findings {Findings.Count}, kill times (first engage to death): {KillTimes()}";

        public BotReport Fight => _fighter.Report;
    }
}
