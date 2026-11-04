// Authority: host. The host owns the one DiveSession (Core/Cellars/DiveSession: floor, phase, stir, momentum, retry; it holds the director's PartyBags, DiveCheckpoint and DivePieces, which outlive a
// dive) and writes a compact summary to ONE host-written NetworkVariable, so every peer (and a late joiner, with the spawn) can display it without an RPC replay. Clients never write:
// ServerAddPiece / ServerAddItem / ServerAddGold / ServerPick / ServerUnpick / ServerEnterBankRoom / ServerChoosePushOn / ServerChooseGoHome / ServerResizeVisits / ServerFailDive / ServerStart / ServerEnterFloor / ServerMarkCleared / ServerGoHome / ServerFinish /
// ServerBegin / ServerAdopt all refuse (return false) off the host. The dive director builds a fresh session per dive and adopts it here (ServerAdopt); a haul sync spawned alone (tests) makes its own.
// Pieces and people (DivePieces): a find is safe at once (never banked, never lost on failure); ServerAddPiece marks it found and PieceAdded (host) lets the director move it into the town stock.
// Per-player loot (plan 2, spec decision 39): the host owns one PlayerBag per recorded player inside the session's PartyBags, keyed like CombatHost growth:
// the roster's PlayerKey string, or the client id when there is no roster, so a rejoin gets the same bag back. NO bag data rides on Net (everyone can read Net); each owner's
// own view of their bag is the owner-only PlayerBagSync on the player prefab (plan 2 task 5). Keys, pieces and people stay one shared copy. A client asks to pick items into bank slots by RPC
// (PlayerBagSync); ServerPick / ServerUnpick re-check everything (the asker is up, the session is in the bank room, the asker stands in range of it) and the bag re-checks capacity and item kinds.
// Bank room (plan 3 task 8): on the bank floor (5, once its guardian is cleared) a bank press enters the bank room (ServerEnterBankRoom: every present, keyed client's bag gets a visit sized by
// BankSlots.SlotsFor, the 3-player extra slot goes to a seeded pick that never repeats the previous owner (PreviousExtraOwner, saved by the director), and everyone is healed). Inside the room the
// party picks (ServerPick / ServerUnpick) and chooses once: push on or go home (ServerChoosePushOn / ServerChooseGoHome). The choice is shared (decision 11, a proposal needing the user's
// sign-off): a living present player's press opens a BankChoice window (10 s placeholder); a second player's press for the same choice, or the timer, confirms it; solo is instant; the other
// choice swaps the window; downed or absent players are not counted. The window (choice, who pressed as a slot mask, whole seconds left) and the phase ride in Summary, so a late joiner sees them.
// Capacity follows presence (ServerResizeVisits, re-run when a client connects, leaves or its roster key arrives): the leaver's visit is cancelled, a rejoiner gets one, excess picks drop from the end.
// The empty slots stay empty at the push-on (no auto-fill). A confirmed push-on raises PushedOn and a confirmed go-home WentHome (host events; the director ends the dive and saves).
// A client the roster has no key for (an RPC in flight from one that just disconnected) has no bag: ServerBagOf, ServerAddItem, ServerAddGold, ServerPick and ServerUnpick refuse it.
// A client banks by asking: RequestBankRpc (SendTo.Server) -> ServerTryBankFor(sender), which the host validates before banking:
//   1. the dive is on the bank floor; 2. when the sender has a player object and the built floor's bank cell is known, the player is within BankRadius metres (XZ) of the bank cell centre
//      (cell origin math from CellarFloorBuilder.CellCenter, via CellarFloorSync). Known gap: with no player object or no built floor on the host the
//      position check is skipped (floor-has-bank only); no anti-cheat by design, so a modded client standing elsewhere is not a concern.
// Stir (Core/Cellars/DiveStir, owned by the session; plan 3 task 10): every host frame it is fed the connected player count and the valuables still carried across all bags (PartyBags.CarriedValuables:
// weapons and large items, never usables, gear, keys or the satchel). A collapse that is pending (value 100) begins with the walk time from the party start to this floor's collapse target
// (DiveFloorTable.PathSecondsToTarget; target = the stair, the bank cell on floor 5, the end cell on floor 7) and is ticked with Time.deltaTime; it closes early when every living hero stands at that
// target (CollapseTargetRadius), and running out fails the dive (DiveFailed). Value, reward rank bonus, latches and collapse seconds ride in the same Summary. Clients never write. No auto-bank on walk-in.
// The world bank press only ENTERS the bank room (ServerTryBankFor); inside the room it does nothing, so a stray Interact press can never push a solo party on: only RequestPushOn / RequestGoHome decide.
// Healing (P47, host-authoritative): entering the bank room fully heals every living connected hero (HeroVitals.ServerHeal + HeroCombat.ServerBroadcastHeal, like HealingDrop) and raises Banked.
// The floor's healing point (Core/Cellars/CellarHealing, cell from the built floor's CellarFloorBuild.HealingCell) is walked onto: the host's Update heals every hurt living hero within
// HealTuning.RadiusMeters of its cell centre in one tick, then the point recharges HealTuning.RechargeSeconds on the host clock. Peers see only NetHealSecondsLeft (0 = ready, else whole
// seconds left, one host-written NetworkVariable so late joiners get it with the spawn) and show/hide the orb on their own built floor. Downed or out heroes are not healed
// (PlayerVitals.Heal refuses them; the revive rule stands). A point recharges per floor visit: a new (seed, floor) starts ready.
// Host disconnect: NGO shuts the session down and this object despawns (no host migration in Phase 1).
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Presentation
{
    public sealed class DiveHaulSync : NetworkBehaviour
    {
        public const float BankRadius = 5f;
        public static readonly int[] DefaultPiecesPerFloor = { 2, 2, 2 };   // legacy (the old 3-floor haul): read only by EditMode tests that plan 3 task 12 ports

        // Everything a peer needs to draw the haul and the pieces-left counters, in one value so a client never sees a half-updated haul.
        public struct Summary : INetworkSerializable, System.IEquatable<Summary>
        {
            // FoundPieces: pieces found in every dive so far (all safe at once; people are not counted). PiecesLeft / PeopleLeft: not yet found on the current floor (0 at home).
            // ClearedMask: floors cleared this dive (bit floor-1, ten bits). CurrentFloor: the dive's floor, 0 at home. Phase: (int)DivePhase.
            public int FoundPieces, PiecesLeft, PeopleLeft, ClearedMask, CurrentFloor, Phase;
            public bool CheckpointHeld, BankChosen;   // CheckpointHeld: the next dive may pick a floor; BankChosen: this dive pushed on at the bank
            // The bank choice's confirm window (BankChoice): Choice = (int)BankChoiceKind (0 = none open), ChoiceMask = the roster slots that pressed (bit per slot), ChoiceSecondsLeft = whole seconds (rounded up).
            public int Choice, ChoiceMask, ChoiceSecondsLeft;
            // Stir (host-computed): value rounded to a whole point 0-100, extra enemies per room (latched), hunter latch, collapse state and whole seconds left (rounded up, 0 = not running).
            public int StirValue, ExtraEnemiesPerRoom, DeepSecondsLeft, RewardRankBonus;   // RewardRankBonus: DiveStir.RewardRankBonus (the reward side of the stir, carried across the push-on)
            public bool HunterLatched, DeepClosesLatched;
            public DivePhase DivePhase => (DivePhase)Phase;
            public BankChoiceKind ChoiceKind => (BankChoiceKind)Choice;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref FoundPieces); s.SerializeValue(ref PiecesLeft); s.SerializeValue(ref PeopleLeft);
                s.SerializeValue(ref ClearedMask); s.SerializeValue(ref CurrentFloor); s.SerializeValue(ref Phase);
                s.SerializeValue(ref CheckpointHeld); s.SerializeValue(ref BankChosen);
                s.SerializeValue(ref Choice); s.SerializeValue(ref ChoiceMask); s.SerializeValue(ref ChoiceSecondsLeft);
                s.SerializeValue(ref StirValue); s.SerializeValue(ref ExtraEnemiesPerRoom); s.SerializeValue(ref DeepSecondsLeft); s.SerializeValue(ref RewardRankBonus);
                s.SerializeValue(ref HunterLatched); s.SerializeValue(ref DeepClosesLatched);
            }
            public bool Equals(Summary o) => FoundPieces == o.FoundPieces && PiecesLeft == o.PiecesLeft && PeopleLeft == o.PeopleLeft && ClearedMask == o.ClearedMask
                && CurrentFloor == o.CurrentFloor && Phase == o.Phase && CheckpointHeld == o.CheckpointHeld && BankChosen == o.BankChosen
                && StirValue == o.StirValue && ExtraEnemiesPerRoom == o.ExtraEnemiesPerRoom && DeepSecondsLeft == o.DeepSecondsLeft && RewardRankBonus == o.RewardRankBonus
                && HunterLatched == o.HunterLatched && DeepClosesLatched == o.DeepClosesLatched
                && Choice == o.Choice && ChoiceMask == o.ChoiceMask && ChoiceSecondsLeft == o.ChoiceSecondsLeft;
            public override bool Equals(object obj) => obj is Summary o && Equals(o);
            public override int GetHashCode() => unchecked(((FoundPieces * 31 + PiecesLeft) * 31 + ClearedMask * 7 + CurrentFloor) * 31 + Choice * 5 + ChoiceMask);
        }

        public readonly NetworkVariable<Summary> Net = new NetworkVariable<Summary>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Whole seconds until the floor's healing point can be used again: 0 = ready (or the floor has none). Host-written.
        public readonly NetworkVariable<int> NetHealSecondsLeft = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public const ulong DefaultWorldSeed = 1UL;   // a haul sync spawned alone (no director) deals its pieces from this seed
        public const int ExtraEnemiesPerRoomOnLatch = 1;   // what Summary.ExtraEnemiesPerRoom reports once DiveStir's extra-enemy latch is on (plan 3 task 10 refines)

        private DiveSession _session;   // host only
        private HealTuning _healTuning = HealTuning.Default;   // host only
        private HealingPointState _heal;                        // host only; the current floor's point
        private uint _healSeed; private int _healFloor;        // host only: which built floor _heal belongs to

        public CellarFloorSync FloorSync;   // optional; found in the scene when null. Used for the bank position check, the healing point and its orb.
        private float _nextFloorLookup;     // the scene search runs at most once a second while no floor sync exists (town, haul-only tests)

        private void ResolveFloorSync()
        {
            if (FloorSync != null || Time.unscaledTime < _nextFloorLookup) return;
            _nextFloorLookup = Time.unscaledTime + 1f;
            FloorSync = FindFirstObjectByType<CellarFloorSync>();
        }

        // ---- stir encounter plan (host only; P49) ----
        // Where the extra enemies and the rat-king placeholder stand on the BUILT floor, recomputed whenever the built floor, ExtraEnemiesPerRoom or the hunter latch changes.
        // A future spawner reads EncounterPlan (and listens to EncounterChanged); nothing spawns from it yet. Null off the host or while no floor is built.
        // Peers derive the same plan from the synced Summary flags plus their own built layout (CellarEncounterPlan.Build is a pure function), so no RPC is needed.
        public CellarEncounterPlan EncounterPlan { get; private set; }
        public event System.Action EncounterChanged;
        private uint _planSeed; private int _planFloor, _planExtra; private bool _planHunter;

        private void RefreshEncounterPlan()
        {
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            DiveStir stir = _session.Stir;
            if (fs == null || fs.Layout == null || fs.Built == null || fs.BuiltFloor != DiveFloor)
            {
                if (EncounterPlan != null) { EncounterPlan = null; EncounterChanged?.Invoke(); }
                return;
            }
            int extra = stir.ExtraEnemyLatched ? ExtraEnemiesPerRoomOnLatch : 0; bool hunter = stir.HunterLatched;
            if (EncounterPlan != null && _planSeed == fs.BuiltSeed && _planFloor == fs.BuiltFloor && _planExtra == extra && _planHunter == hunter) return;
            EncounterPlan = CellarEncounterPlan.BuildForDive(fs.Layout, fs.Built.Dressing, extra, hunter);
            _planSeed = fs.BuiltSeed; _planFloor = fs.BuiltFloor; _planExtra = extra; _planHunter = hunter;
            EncounterChanged?.Invoke();
        }

        public Summary Current => Net.Value;
        // Host only; null on clients.
        public DiveSession Session => _session;
        // The party's bags (they outlive a dive: the session holds the director's PartyBags). Host only; null on clients.
        public PartyBags Bags => _session?.Bags;

        // The floor the party is on (0 at home or once the dive ended).
        private bool OnBankFloor => DiveFloorTable.HasBank(DiveFloor);

        private int DiveFloor => _session != null && (_session.Phase == DivePhase.Floor || _session.Phase == DivePhase.BankRoom) ? _session.Floor : 0;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            _session = new DiveSession(new PartyBags(), new DiveCheckpoint(), DivePieces.ForWorld(DefaultWorldSeed));
            _presenceManager = NetworkManager;
            _presenceManager.OnClientConnectedCallback += OnPresenceChanged;
            _presenceManager.OnClientDisconnectCallback += OnPresenceChanged;
            Publish();
        }

        public override void OnNetworkDespawn()
        {
            if (_presenceManager != null) { _presenceManager.OnClientConnectedCallback -= OnPresenceChanged; _presenceManager.OnClientDisconnectCallback -= OnPresenceChanged; _presenceManager = null; }
            _session = null; _choice.Cancel();
            if (EncounterPlan != null) { EncounterPlan = null; EncounterChanged?.Invoke(); }
        }

        private NetworkManager _presenceManager;   // host: the manager whose connect / disconnect callbacks mark the bank room's visits stale
        private bool _presenceDirty;
        private void OnPresenceChanged(ulong clientId) { _presenceDirty = true; }

        // Host only: raised when the deep-closes countdown ran out and the dive was failed (the dive flow sends the party home).
        public event System.Action DiveFailed;

        // Longest host frame the countdown counts; a hitch (a floor build) must not eat seconds of it.
        private const float MaxTickSeconds = 0.25f;

        // Host only: raised after a successful bank, once every living hero has been healed.
        public event System.Action Banked;

        // Host only: the stir countdown runs on the host clock, and the player factor follows who is connected right now.
        private void Update()
        {
            if (Host) TickHealingPoint(Mathf.Min(Time.deltaTime, MaxTickSeconds));
            if (IsSpawned) ShowHealingOrb();
            if (!Host) return;
            DiveStir stir = _session.Stir;
            FeedStir();
            TickCollapseStart();
            bool failed = _session.Phase == DivePhase.Floor && stir.Tick(Mathf.Min(Time.deltaTime, MaxTickSeconds));   // a collapse that ran out fails the dive
            if (!failed) TickCollapseClose();
            if (failed) _session.Fail();
            TickBankRoom(Mathf.Min(Time.deltaTime, MaxTickSeconds));
            Publish();
            RefreshEncounterPlan();
            if (failed) DiveFailed?.Invoke();
        }

        // The stir's two inputs, set every host frame and again the moment an item is added: who is connected, and the valuables still carried in every bag (the push-on takes the count after the picks as
        // its baseline, so the same feed after it measures only what was found since).
        private void FeedStir()
        {
            DiveStir stir = _session.Stir;
            stir.PlayersPresent = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
            if (_session.Phase == DivePhase.Floor || _session.Phase == DivePhase.BankRoom) stir.UnbankedValuables = _session.Bags.CarriedValuables();
        }

        // ---- the collapse (plan 3 task 10; host) ----
        public const float CollapseTargetRadius = 4f;     // placeholder: every living hero within this many metres (flat) of the target cell closes the timer early
        public const float CollapseFloorWaitSeconds = 3f; // a built floor that lags the dive floor is waited for this long before the collapse begins with no path
        private float _collapseWaitSince = -1f;

        // The collapse is pending (stir 100, once per half) on a floor of the dive: it begins with the walk time to this floor's target. With a floor sync in the scene it waits for the layout of
        // this floor (a few seconds at most); with none (haul-only tests) it begins at once with no path, so the minimum applies.
        private void TickCollapseStart()
        {
            DiveStir stir = _session.Stir;
            if (_session.Phase != DivePhase.Floor || !stir.CollapsePending) { _collapseWaitSince = -1f; return; }
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            bool built = fs != null && fs.Layout != null && fs.Built != null && fs.BuiltFloor == DiveFloor;
            if (fs != null && !built)
            {
                if (_collapseWaitSince < 0f) _collapseWaitSince = Time.unscaledTime;
                if (Time.unscaledTime - _collapseWaitSince < CollapseFloorWaitSeconds) return;
            }
            _collapseWaitSince = -1f;
            stir.BeginCollapse(built ? DiveFloorTable.PathSecondsToTarget(fs.Layout) : 0f);
        }

        // A running collapse closes when every living hero (at least one) is at the target: the party is leaving, so the host treats it like the stair (DiveStir.OnStairTaken).
        private void TickCollapseClose()
        {
            DiveStir stir = _session.Stir;
            if (!stir.CollapseRunning || _session.Phase != DivePhase.Floor || !TryCollapseTargetWorld(out Vector3 target)) return;
            int living = 0;
            var all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals v = all[i];
                if (!IsMine(v)) continue;
                living++;
                float dx = v.transform.position.x - target.x, dz = v.transform.position.z - target.z;
                if (dx * dx + dz * dz > CollapseTargetRadius * CollapseTargetRadius) return;
            }
            if (living > 0) stir.OnStairTaken();
        }

        // World position of this floor's collapse target cell (the stair, the bank cell, the end cell), when this peer has built the floor.
        public bool TryCollapseTargetWorld(out Vector3 pos)
        {
            pos = default;
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            if (fs == null || fs.Layout == null || fs.Built == null || fs.Built.Root == null || fs.BuiltFloor != DiveFloor) return false;
            CellPos? cell = DiveFloorTable.CollapseTargetCell(fs.Layout);
            if (!cell.HasValue) return false;
            pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.Value.X, cell.Value.Y));
            return true;
        }

        private void Publish()
        {
            DiveSession s = _session;
            DiveStir stir = s.Stir;
            int floor = DiveFloor;
            int found = 0;
            foreach (int id in s.Pieces.FoundIds) if (!PieceCatalog.IsPerson(id)) found++;
            int choice = 0, choiceMask = 0, choiceSeconds = 0;
            if (_choice.IsOpen)
            {
                choice = (int)_choice.Kind; choiceSeconds = Mathf.CeilToInt(_choice.SecondsLeft);
                BuildPresence();
                foreach (string presser in _choice.Pressers) { int at = _presentIds.IndexOf(presser); if (at >= 0) choiceMask |= 1 << _presentSlots[at]; }
            }
            var next = new Summary
            {
                Choice = choice, ChoiceMask = choiceMask, ChoiceSecondsLeft = choiceSeconds,
                StirValue = Mathf.RoundToInt(stir.Value), ExtraEnemiesPerRoom = stir.ExtraEnemyLatched ? ExtraEnemiesPerRoomOnLatch : 0, DeepSecondsLeft = Mathf.CeilToInt(stir.CollapseSecondsLeft), RewardRankBonus = stir.RewardRankBonus,
                HunterLatched = stir.HunterLatched, DeepClosesLatched = stir.CollapseRunning,
                FoundPieces = found, PiecesLeft = floor > 0 ? s.Pieces.PiecesLeft(floor) : 0, PeopleLeft = floor > 0 ? s.Pieces.PeopleLeft(floor) : 0,
                ClearedMask = floor > 0 ? s.Snapshot().ClearedMask : 0, CurrentFloor = floor, Phase = (int)s.Phase, CheckpointHeld = s.Checkpoint.Held, BankChosen = s.BankChosenThisDive,
            };
            if (!next.Equals(Net.Value)) Net.Value = next;   // the countdown ticks every frame; only send when something a peer can see changed
        }

        private bool Host => IsServer && IsSpawned && _session != null;

        // ---- host-only mutations (all return false on a client or on bad input) ----
        // A fresh session over the same bags, checkpoint and pieces (the party's state outlives a dive).
        public bool ServerBegin()
        {
            if (!Host) return false;
            return ServerAdopt(new DiveSession(_session.Bags, _session.Checkpoint, _session.Pieces));
        }
        // Host only: runs with the session the dive director built over its persistent bags, checkpoint and pieces (a fresh one per dive, or one rebuilt from the world save).
        public bool ServerAdopt(DiveSession session)
        {
            if (!IsServer || !IsSpawned || session == null) return false;
            _session = session; _heal = null; _visitExtraOwner = -1; _choice.Cancel(); if (EncounterPlan != null) { EncounterPlan = null; EncounterChanged?.Invoke(); } Publish(); return true;
        }
        // Host only: adopts the session a resumed suspend rebuilt (DiveDirector.ServerResumeSuspended): the saved dive seed, and the bank room's extra-slot owner kept as it was (it was set
        // before the quit, so PreviousExtraOwner is not written again this visit).
        public bool ServerAdoptResumed(DiveSession session, uint diveSeed)
        {
            if (!ServerAdopt(session)) return false;
            DiveSeed = diveSeed;
            _visitPrior = PreviousExtraOwner; _previousSet = true;
            _visitExtraOwner = session.ExtraSlotOwner;
            _lastPresentIds.Clear();
            Publish();
            return true;
        }

        // Host only: the present players' roster slots and bag ids (the connected, keyed clients, at most four) into the two lists.
        public bool GetPresence(System.Collections.Generic.List<int> slots, System.Collections.Generic.List<string> ids)
        {
            slots.Clear(); ids.Clear();
            if (!Host || !BuildPresence()) return false;
            slots.AddRange(_presentSlots); ids.AddRange(_presentIds);
            return true;
        }

        // A piece or person found is safe the moment it is picked up: PieceAdded (host) lets the dive director move it into the town stock at once. False for an unknown id or one already found.
        public bool ServerAddPiece(int pieceId)
        {
            if (!Host || !_session.Pieces.Pick(pieceId)) return false;
            Publish();
            PieceAdded?.Invoke(pieceId);
            return true;
        }
        // Host only: raised after a piece was added (its id).
        public event System.Action<int> PieceAdded;

        // Host only: starts the dive on `floor` (DiveSession.TryStart: the checkpoint rule decides which floors may start). Cancels every bank visit.
        public bool ServerStart(int floor, bool bossBeaten)
        {
            if (!Host || !_session.TryStart(floor, bossBeaten)) return false;
            _visitExtraOwner = -1; _choice.Cancel();
            Publish();
            return true;
        }

        // Host only: the stair to the next floor (DiveSession.EnterFloor: only the next floor, and floor 5's only after the bank choice).
        public bool ServerEnterFloor(int floor)
        {
            if (!Host || !_session.EnterFloor(floor)) return false;
            _visitExtraOwner = -1; _choice.Cancel();
            Publish();
            return true;
        }

        // Host only: the party goes home from the bank (the checkpoint is held afterwards); raises WentHome, which the director answers by ending the dive. The shared choice (ServerChooseGoHome)
        // ends here once it is confirmed. Tests and host logic may call it directly (no confirm window).
        public bool ServerGoHome()
        {
            if (!Host || !_session.GoHome()) return false;
            _visitExtraOwner = -1; _choice.Cancel(); Publish();
            WentHome?.Invoke();
            return true;
        }

        // Host only: finishes the Phase 1 stand-in ending after floor 7.
        public bool ServerFinish()
        {
            if (!Host || !_session.Finish()) return false;
            _choice.Cancel(); Publish(); return true;
        }

        // ---- per-player bags (host only) ----
        // The identity source for bag keys and slots (the session's HostRoster); resolved from ServiceRoots.App when null, like CombatHost.ServerBegin.
        public IJoinContextProvider Identity { get; set; }

        // The dive seed the 3-player extra bank slot owner is picked from; the dive director sets it when a dive starts.
        public uint DiveSeed { get; set; }

        private IJoinContextProvider ResolveIdentity()
        {
            if (Identity == null && ServiceRoots.App != null && ServiceRoots.App.TryGet(out HostRoster roster)) Identity = roster;
            return Identity;
        }

        // The bag key of a client, so a rejoin gets the same bag back: the roster's PlayerKey string (CombatHost.PlayerIdOf's rule). With NO roster (tests) the client id string.
        // With a roster that has no key for the client, null: an RPC in flight from a client that just disconnected must not make a throwaway bag. The host's own client is the one
        // exception (it never drops mid-session, and a solo dive must always have its bag): it falls back to the client id like the no-roster case.
        public string PlayerIdOf(ulong clientId)
        {
            IJoinContextProvider id = ResolveIdentity();
            if (id == null) return clientId.ToString();
            if (id.TryGetKey(clientId, out PlayerKey key)) return key.ToString();
            return NetworkManager != null && clientId == NetworkManager.LocalClientId ? clientId.ToString() : null;
        }

        private readonly System.Collections.Generic.Dictionary<ulong, int> _seenOrder = new System.Collections.Generic.Dictionary<ulong, int>();   // no roster: client id -> order first seen

        // 0-3, used for the chest roll stream and the 3-player extra slot. The roster slot (stable across a rejoin); with no roster, the order the host first saw each client id
        // (the host first, so 0), clamped to 3 because RollFor throws above it (tests with more than four ids share slot 3).
        public int PlayerSlotOf(ulong clientId)
        {
            IJoinContextProvider id = ResolveIdentity();
            if (id != null && id.TryGetSlot(clientId, out byte rosterSlot)) return Mathf.Clamp(rosterSlot, 0, 3);
            if (!_seenOrder.TryGetValue(clientId, out int order))
            {
                if (NetworkManager != null && clientId == NetworkManager.LocalClientId) order = 0;
                else { order = 1; foreach (int o in _seenOrder.Values) if (o >= order) order = o + 1; }
                _seenOrder[clientId] = order;
            }
            return Mathf.Clamp(order, 0, 3);
        }

        // Host only: the roster's key of a connected client, false when it has none (or there is no roster). A watcher compares it to notice the key changing.
        public bool TryRosterKey(ulong clientId, out PlayerKey key)
        {
            key = default;
            IJoinContextProvider id = ResolveIdentity();
            return id != null && id.TryGetKey(clientId, out key);
        }

        // The client's bag (created on first use); null off the host, and null for a client the roster has no key for (PlayerIdOf).
        public PlayerBag ServerBagOf(ulong clientId)
        {
            if (!Host) return null;
            string id = PlayerIdOf(clientId);
            return id == null ? null : _session.Bags.For(id);
        }

        public bool ServerAddItem(ulong clientId, int itemId)
        {
            PlayerBag bag = ServerBagOf(clientId);
            if (bag == null || !bag.Add(itemId)) return false;
            FeedStir();   // a valuable found is in the stir at once, not a frame later
            return true;
        }

        public bool ServerAddGold(ulong clientId, int amount)
        {
            PlayerBag bag = ServerBagOf(clientId);
            return bag != null && bag.AddGold(amount);
        }

        // Host only: the player picks up the lost satchel waiting on this floor (PlayerBag.PickUpSatchel: it takes one carry slot, so a full bag leaves it where it lies). The caller checks the
        // range; this checks the dive is on that floor and the satchel waits there. The satchel is the owner's own: nobody else's bag ever has it.
        public bool ServerPickUpSatchel(ulong clientId)
        {
            if (!Host || _session.Phase != DivePhase.Floor) return false;
            PlayerBag bag = ServerBagOf(clientId);
            return bag != null && bag.Satchel.ShouldPlaceOn(DiveFloor) && bag.PickUpSatchel();
        }

        // The asker may touch the bank: the party is in the bank room, the hero is up, and (when the built floor's bank cell is known) the hero stands within BankRadius of it.
        private bool BankAskerOk(ulong clientId, bool needRoomOpen = true)
        {
            if (!Host || (needRoomOpen ? _session.Phase != DivePhase.BankRoom : !OnBankFloor)) return false;
            if (!AskerIsUp(NetworkManager, clientId)) return false;   // a downed hero cannot bank
            if (TryBankWorldPosition(out Vector3 bank) && NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient c)
                && c.PlayerObject != null && !InRange(c.PlayerObject.transform.position, bank)) return false;
            return true;
        }

        // Puts carried item `carriedIndexOrSatchel` (PlayerBag.SatchelPick = the carried satchel) into one of the asker's bank slots. The bag refuses with no open visit, a full
        // slot set, a bad or repeated index and a non-bankable kind.
        public bool ServerPick(ulong clientId, int carriedIndexOrSatchel)
        {
            if (!BankAskerOk(clientId)) return false;
            EnsureVisit(clientId);
            PlayerBag bag = ServerBagOf(clientId);
            return bag != null && bag.Pick(carriedIndexOrSatchel);
        }

        public bool ServerUnpick(ulong clientId, int carriedIndexOrSatchel)
        {
            if (!BankAskerOk(clientId)) return false;
            EnsureVisit(clientId);
            PlayerBag bag = ServerBagOf(clientId);
            return bag != null && bag.Unpick(carriedIndexOrSatchel);
        }

        // ---- the bank room (plan 3 task 8) ----
        // The roster slot of the previous visit's 3-player extra-slot owner (-1 = none known). The director loads it from the saved bag id (DiveSave.ExtraSlotOwner) and saves it back from here.
        // Set ONCE per visit, when the extra owner is first picked; a resize that picks a new owner (the owner left) does not overwrite it again, and the re-pick still skips the owner the visit
        // started from (_visitPrior). It outlives every dive session (ServerAdopt leaves it alone).
        public int PreviousExtraOwner { get; set; } = -1;
        private int _visitPrior = -1;           // PreviousExtraOwner as this visit opened: what a re-pick must skip
        private bool _previousSet;              // PreviousExtraOwner was written for this visit already
        private int _visitExtraOwner = -1;      // the 3-player extra slot's owner of this visit (-1 = none)

        private readonly BankChoice _choice = new BankChoice();
        // Host only: the seconds a push-on / go-home window waits for a second player (BankChoice.WindowSeconds, a placeholder). Tests set it.
        public float ChoiceWindowSeconds { get => _choice.Window; set => _choice.Window = value; }
        private const float PresencePollSeconds = 0.25f;
        private float _nextPresencePoll;
        private readonly System.Collections.Generic.List<int> _presentSlots = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<string> _presentIds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _livingIds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _lastPresentIds = new System.Collections.Generic.List<string>();

        // The connected, keyed clients (at most four): their roster slots and bag ids, in client order. An unkeyed client (not in the roster yet) takes no slot and does not count.
        private bool BuildPresence()
        {
            _presentSlots.Clear(); _presentIds.Clear();
            if (NetworkManager == null) return false;
            foreach (ulong cid in NetworkManager.ConnectedClientsIds)
            {
                string id = PlayerIdOf(cid);
                if (id == null || _presentIds.Count >= 4) continue;
                _presentSlots.Add(PlayerSlotOf(cid)); _presentIds.Add(id);
            }
            return _presentIds.Count > 0;
        }

        // The bag ids of the present players whose hero is up (the confirm window's quorum). A rig with no hero objects counts everyone as living (AskerIsUp).
        private void BuildLiving()
        {
            _livingIds.Clear();
            foreach (ulong cid in NetworkManager.ConnectedClientsIds)
            {
                string id = PlayerIdOf(cid);
                if (id != null && _livingIds.Count < 4 && AskerIsUp(NetworkManager, cid)) _livingIds.Add(id);
            }
        }

        // The bag id of each roster slot (index = slot, null for an empty one): the roster's keys, overlaid with the connected clients. The director names the extra-slot owner with it.
        public string[] IdsBySlot()
        {
            var ids = new string[4];
            IJoinContextProvider provider = ResolveIdentity();
            if (provider != null)
            {
                var roster = new System.Collections.Generic.List<RosterEntry>();
                provider.GetRoster(roster);
                foreach (RosterEntry e in roster) if (e.Slot >= 0 && e.Slot < 4) ids[e.Slot] = e.Key.ToString();
            }
            if (NetworkManager != null)
                foreach (ulong cid in NetworkManager.ConnectedClientsIds)
                {
                    string id = PlayerIdOf(cid);
                    if (id != null) ids[PlayerSlotOf(cid)] = id;
                }
            return ids;
        }

        // The roster slot of a bag id (-1 when it has none).
        public int SlotOfBagId(string id)
        {
            string[] ids = IdsBySlot();
            for (int i = 0; i < ids.Length; i++) if (ids[i] != null && ids[i] == id) return i;
            return -1;
        }

        private void LockPreviousOwner()
        {
            if (_previousSet || _session.ExtraSlotOwner < 0) return;
            PreviousExtraOwner = _session.ExtraSlotOwner; _previousSet = true;
        }

        // Entering the bank room (DiveSession.EnterBankRoom): every connected, keyed client's bag gets a bank visit with BankSlots.SlotsFor capacity; the 3-player extra slot goes to a seeded pick that
        // skips the previous owner. False when the session refuses (not on the cleared bank floor, or already chosen).
        private bool OpenBankVisits()
        {
            if (!BuildPresence()) return false;
            foreach (string id in _presentIds) _session.Bags.For(id);   // a bag exists for every visit it opens on
            int prior = PreviousExtraOwner;
            if (!_session.EnterBankRoom(_presentSlots, DiveSeed, prior, _presentIds)) return false;
            _visitPrior = prior; _previousSet = false;
            _visitExtraOwner = _session.ExtraSlotOwner;
            LockPreviousOwner();
            _lastPresentIds.Clear(); _lastPresentIds.AddRange(_presentIds);
            return true;
        }

        // Host only: the bank room's visits follow who is present right now (decision 7): the leaver's visit is cancelled, a rejoiner or late joiner gets one, capacity and the 3-player extra slot are
        // recomputed, excess picks drop from the end. Runs by itself when the party changes; a pick from a client with no visit also runs it. False outside the bank room.
        public bool ServerResizeVisits()
        {
            if (!Host || _session.Phase != DivePhase.BankRoom || !BuildPresence()) return false;
            if (!_session.ResizeBankRoom(_presentSlots, _presentIds, DiveSeed, _visitPrior)) return false;
            _visitExtraOwner = _session.ExtraSlotOwner;
            LockPreviousOwner();
            _lastPresentIds.Clear(); _lastPresentIds.AddRange(_presentIds);
            Publish();
            return true;
        }

        // The party is in the bank room and this client's bag has no visit (it joined after the room opened): resize now.
        private void EnsureVisit(ulong clientId)
        {
            if (_session.Phase != DivePhase.BankRoom) return;
            PlayerBag bag = ServerBagOf(clientId);
            if (bag != null && !bag.BankOpen) ServerResizeVisits();
        }

        // Update (host): while the room is open, resize when the party changed (a connect or disconnect marked it, or the keyed players differ from the last resize), and run the confirm window's timer.
        private void TickBankRoom(float dt)
        {
            bool inRoom = _session.Phase == DivePhase.BankRoom;
            bool atBank = inRoom || (_session.Phase == DivePhase.Floor && OnBankFloor && _session.IsCleared(DiveFloor) && !_session.BankChosenThisDive);
            if (!atBank) { if (_choice.IsOpen) _choice.Cancel(); return; }
            if (inRoom && (_presenceDirty || Time.unscaledTime >= _nextPresencePoll))
            {
                _presenceDirty = false; _nextPresencePoll = Time.unscaledTime + PresencePollSeconds;
                BuildPresence();
                bool same = _presentIds.Count == _lastPresentIds.Count;
                for (int i = 0; same && i < _presentIds.Count; i++) same = _presentIds[i] == _lastPresentIds[i];
                if (!same) ServerResizeVisits();
            }
            if (!_choice.IsOpen) return;
            BuildLiving();
            BankChoiceKind confirmed = _choice.Tick(dt, _livingIds);
            if (confirmed != BankChoiceKind.None) ApplyChoice(confirmed);
        }

        // Host only: marks `floor` cleared (DiveSession.ClearFloor: the party's current floor only; feeds the stir). False when nothing changed.
        public bool ServerMarkCleared(int floor)
        {
            if (!Host) return false;
            int before = _session.Snapshot().ClearedMask;
            _session.ClearFloor(floor);
            if (_session.Snapshot().ClearedMask == before) return false;
            Publish(); return true;
        }
        public bool ServerFailDive() { if (!Host) return false; _session.Fail(); _choice.Cancel(); Publish(); return true; }

        // Host only: enters the bank room from the cleared bank floor (visits open, everyone is healed). No position check (host logic, e.g. a scripted bank); the RPC path is ServerTryBankFor.
        public bool ServerEnterBankRoom()
        {
            if (!Host || !OpenBankVisits()) return false;
            _choice.Cancel();
            Publish();
            HealEveryone();
            Banked?.Invoke();
            return true;
        }

        // Host only: the shared choice was confirmed. PushedOn: the bank is locked and the picks are safe (the director saves the suspend); WentHome: see ServerGoHome.
        public event System.Action PushedOn;
        public event System.Action WentHome;

        // A player's press for "push on" / "go home" (decision 11). The host re-checks everything: the asker is up and keyed, the party is at the bank (push on: in the bank room; go home: in the room or
        // on the cleared bank floor, the choice not made yet), and the asker is in range of the bank cell. The press opens, joins or swaps the confirm window; a confirm applies at once. False for a
        // refused press and for a repeated press by the same player.
        public bool ServerChoosePushOn(ulong clientId) => ServerPressChoice(clientId, BankChoiceKind.PushOn);
        public bool ServerChooseGoHome(ulong clientId) => ServerPressChoice(clientId, BankChoiceKind.GoHome);

        private bool ServerPressChoice(ulong clientId, BankChoiceKind kind)
        {
            if (!Host || _session.BankChosenThisDive || kind == BankChoiceKind.None) return false;
            bool inRoom = _session.Phase == DivePhase.BankRoom;
            bool atBank = inRoom || (_session.Phase == DivePhase.Floor && OnBankFloor && _session.IsCleared(DiveFloor));
            if (kind == BankChoiceKind.PushOn ? !inRoom : !atBank) return false;
            if (!BankAskerOk(clientId, false)) return false;
            string presser = PlayerIdOf(clientId);
            if (presser == null) return false;
            BuildLiving();
            BankChoicePress result = _choice.Press(presser, kind, Mathf.Max(1, _livingIds.Count));
            if (result == BankChoicePress.Ignored) return false;
            if (result == BankChoicePress.Confirmed) return ApplyChoice(_choice.ConfirmedKind);
            Publish();
            return true;
        }

        // The confirmed choice takes effect (host).
        private bool ApplyChoice(BankChoiceKind kind)
        {
            if (kind == BankChoiceKind.GoHome) return ServerGoHome();
            if (kind != BankChoiceKind.PushOn || _session.Phase != DivePhase.BankRoom) return false;
            ServerResizeVisits();   // a player who joined since the last resize is in the push-on too
            if (!_session.PushOn()) return false;
            _visitExtraOwner = -1; _choice.Cancel();
            Publish();
            PushedOn?.Invoke();
            return true;
        }

        // Host only: test hook, the healing tuning (short recharge). Restarts the current point ready.
        public bool ServerSetHealTuning(HealTuning tuning)
        {
            if (!IsServer || !IsSpawned) return false;
            _healTuning = tuning; _heal = null; return true;
        }

        // ---- healing (host) ----
        private static void HealOne(HeroVitals v)
        {
            int healed = v.ServerHeal(v.MaxHp);
            if (healed <= 0) return;
            HeroCombat c = v.GetComponent<HeroCombat>();
            if (c != null) c.ServerBroadcastHeal(healed);
        }

        private bool IsMine(HeroVitals v) => v != null && v.IsSpawned && v.NetworkManager == NetworkManager && v.IsUp;

        private void HealEveryone()
        {
            var all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++) if (IsMine(all[i])) HealOne(all[i]);
        }

        // Authority: read-only, no state change, any peer (the cell comes from the replicated floor, the recharge from NetHealSecondsLeft, which only the host writes).
        // For tests/bots: where this floor's healing point is and whether it is ready (no recharge left).
        public bool TryHealingPoint(out Vector3 pos, out bool ready) { ready = NetHealSecondsLeft.Value <= 0; return TryHealWorldPosition(out pos); }

        private bool TryHealWorldPosition(out Vector3 pos)
        {
            pos = default;
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            if (fs == null || fs.Built == null || fs.Built.Root == null || !fs.Built.HealingCell.HasValue || fs.BuiltFloor != DiveFloor) return false;
            CellPos cell = fs.Built.HealingCell.Value;
            pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y));
            return true;
        }

        private void TickHealingPoint(float dt)
        {
            int left = 0;
            if (TryHealWorldPosition(out Vector3 pos))
            {
                if (_heal == null || _healSeed != FloorSync.BuiltSeed || _healFloor != FloorSync.BuiltFloor)
                { _heal = new HealingPointState(_healTuning.RechargeSeconds); _healSeed = FloorSync.BuiltSeed; _healFloor = FloorSync.BuiltFloor; }
                _heal.Tick(dt);
                if (_heal.Ready)
                {
                    bool any = false; float r2 = _healTuning.RadiusMeters * _healTuning.RadiusMeters;
                    var all = HeroVitals.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        HeroVitals v = all[i];
                        if (!IsMine(v) || v.Hp >= v.MaxHp) continue;
                        float dx = v.transform.position.x - pos.x, dz = v.transform.position.z - pos.z;
                        if (dx * dx + dz * dz > r2) continue;
                        HealOne(v); any = true;
                    }
                    if (any) _heal.TryUse();
                }
                left = Mathf.CeilToInt(_heal.SecondsLeft);
            }
            else _heal = null;
            if (NetHealSecondsLeft.Value != left) NetHealSecondsLeft.Value = left;
        }

        // Every peer: the orb over the healing point is lit while the point is ready.
        private void ShowHealingOrb()
        {
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            if (fs == null || fs.Built == null || fs.Built.HealingOrb == null) return;
            bool ready = NetHealSecondsLeft.Value <= 0;
            if (fs.Built.HealingOrb.activeSelf != ready) fs.Built.HealingOrb.SetActive(ready);
        }
        // ---- client request ----
        // Any player may interact with the bank point; the host validates (see the class comment) and runs the bank press for the whole party.
        public void RequestBank() { RequestBankRpc(); }

        // Host only: bank requests received over the RPC, accepted or refused (lets a test wait for a refusal to land).
        public int BankRequestsHandled { get; private set; }

        [Rpc(SendTo.Server)]
        private void RequestBankRpc(RpcParams rpcParams = default) { BankRequestsHandled++; ServerTryBankFor(rpcParams.Receive.SenderClientId); }

        // The world's bank press (the director's prompt): outside the room it enters it; inside the room it does nothing (the picks and the choice are made on the bank screen: RequestPushOn / RequestGoHome).
        public bool ServerTryBankFor(ulong clientId)
        {
            if (!BankAskerOk(clientId, false)) return false;
            return _session.Phase != DivePhase.BankRoom && ServerEnterBankRoom();   // inside the room a world press does nothing (no stray push-on); the bank screen keys decide
        }

        // Any player may press "push on" (the bank screen's key); the host validates (ServerChoosePushOn).
        public void RequestPushOn() { RequestPushOnRpc(); }

        // Host only: push-on requests received over the RPC, accepted or refused.
        public int ChoiceRequestsHandled { get; private set; }

        [Rpc(SendTo.Server)]
        private void RequestPushOnRpc(RpcParams rpcParams = default) { ChoiceRequestsHandled++; ServerChoosePushOn(rpcParams.Receive.SenderClientId); }

        // Host: false when the asker's hero is downed or out (no player object or no vitals passes, like the range checks).
        public static bool AskerIsUp(NetworkManager nm, ulong clientId)
        {
            if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out NetworkClient c) || c.PlayerObject == null) return true;
            HeroVitals v = c.PlayerObject.GetComponent<HeroVitals>();
            return v == null || v.IsUp;
        }

        public static bool InRange(Vector3 player, Vector3 bank)
        {
            float dx = player.x - bank.x, dz = player.z - bank.z;
            return dx * dx + dz * dz <= BankRadius * BankRadius;
        }

        // World position of the built floor's bank cell centre, if this peer has built a floor that has one.
        private bool TryBankWorldPosition(out Vector3 pos)
        {
            pos = default;
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            if (fs == null || fs.Layout == null || fs.Built == null || fs.Built.Root == null || !fs.Layout.BankCell.HasValue) return false;
            if (fs.BuiltFloor != DiveFloor) return false;   // the built floor lags or leads the dive floor: no cell to trust, skip the range check
            CellPos cell = fs.Layout.BankCell.Value;
            pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y));
            return true;
        }
    }
}
