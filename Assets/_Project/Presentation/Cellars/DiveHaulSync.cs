// Authority: host. The host owns the one DiveSession (Core/Cellars/DiveSession: floor, phase, stir, momentum, retry; it holds the director's PartyBags, DiveCheckpoint and DivePieces, which outlive a
// dive) and writes a compact summary to ONE host-written NetworkVariable, so every peer (and a late joiner, with the spawn) can display it without an RPC replay. Clients never write:
// ServerAddPiece / ServerAddItem / ServerAddGold / ServerPick / ServerUnpick / ServerBank / ServerFailDive / ServerStart / ServerEnterFloor / ServerMarkCleared / ServerGoHome / ServerFinish /
// ServerBegin / ServerAdopt all refuse (return false) off the host. The dive director builds a fresh session per dive and adopts it here (ServerAdopt); a haul sync spawned alone (tests) makes its own.
// Pieces and people (DivePieces): a find is safe at once (never banked, never lost on failure); ServerAddPiece marks it found and PieceAdded (host) lets the director move it into the town stock.
// Per-player loot (plan 2, spec decision 39): the host owns one PlayerBag per recorded player inside the session's PartyBags, keyed like CombatHost growth:
// the roster's PlayerKey string, or the client id when there is no roster, so a rejoin gets the same bag back. NO bag data rides on Net (everyone can read Net); each owner's
// own view of their bag is the owner-only PlayerBagSync on the player prefab (plan 2 task 5). Keys, pieces and people stay one shared copy. A client asks to pick items into bank slots by RPC
// (PlayerBagSync); ServerPick / ServerUnpick re-check everything (the asker is up, the session is in the bank room, the asker stands in range of it) and the bag re-checks capacity and item kinds.
// Bank (stand-in until plan 3 task 8): on the bank floor (5, once its guardian is cleared) the first bank press enters the bank room (DiveSession.EnterBankRoom: every connected client's bag gets a
// visit sized by BankSlots.SlotsFor, the 3-player extra slot goes to a seeded pick that skips the previous owner, and everyone is healed), the second pushes on (DiveSession.PushOn).
// A client the roster has no key for (an RPC in flight from one that just disconnected) has no bag: ServerBagOf, ServerAddItem, ServerAddGold, ServerPick and ServerUnpick refuse it.
// A client banks by asking: RequestBankRpc (SendTo.Server) -> ServerTryBankFor(sender), which the host validates before banking:
//   1. the dive is on the bank floor; 2. when the sender has a player object and the built floor's bank cell is known, the player is within BankRadius metres (XZ) of the bank cell centre
//      (cell origin math from CellarFloorBuilder.CellCenter, via CellarFloorSync). Known gap: with no player object or no built floor on the host the
//      position check is skipped (floor-has-bank only); no anti-cheat by design, so a modded client standing elsewhere is not a concern.
// Stir (Core/Cellars/DiveStir, owned by the session): the host feeds it the connected player count every frame and ticks a running collapse with Time.deltaTime; value, extra-enemy, hunter and
// collapse state ride in the same Summary. Valuables and the collapse start are wired in plan 3 task 10. Clients never write. No auto-bank on walk-in.
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
            // Stir (host-computed): value rounded to a whole point 0-100, extra enemies per room (latched), hunter latch, collapse state and whole seconds left (rounded up, 0 = not running).
            public int StirValue, ExtraEnemiesPerRoom, DeepSecondsLeft;
            public bool HunterLatched, DeepClosesLatched;
            public DivePhase DivePhase => (DivePhase)Phase;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref FoundPieces); s.SerializeValue(ref PiecesLeft); s.SerializeValue(ref PeopleLeft);
                s.SerializeValue(ref ClearedMask); s.SerializeValue(ref CurrentFloor); s.SerializeValue(ref Phase);
                s.SerializeValue(ref CheckpointHeld); s.SerializeValue(ref BankChosen);
                s.SerializeValue(ref StirValue); s.SerializeValue(ref ExtraEnemiesPerRoom); s.SerializeValue(ref DeepSecondsLeft);
                s.SerializeValue(ref HunterLatched); s.SerializeValue(ref DeepClosesLatched);
            }
            public bool Equals(Summary o) => FoundPieces == o.FoundPieces && PiecesLeft == o.PiecesLeft && PeopleLeft == o.PeopleLeft && ClearedMask == o.ClearedMask
                && CurrentFloor == o.CurrentFloor && Phase == o.Phase && CheckpointHeld == o.CheckpointHeld && BankChosen == o.BankChosen
                && StirValue == o.StirValue && ExtraEnemiesPerRoom == o.ExtraEnemiesPerRoom && DeepSecondsLeft == o.DeepSecondsLeft
                && HunterLatched == o.HunterLatched && DeepClosesLatched == o.DeepClosesLatched;
            public override bool Equals(object obj) => obj is Summary o && Equals(o);
            public override int GetHashCode() => unchecked((FoundPieces * 31 + PiecesLeft) * 31 + ClearedMask * 7 + CurrentFloor);
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
            if (IsServer) { _session = new DiveSession(new PartyBags(), new DiveCheckpoint(), DivePieces.ForWorld(DefaultWorldSeed)); Publish(); }
        }

        public override void OnNetworkDespawn() { _session = null; if (EncounterPlan != null) { EncounterPlan = null; EncounterChanged?.Invoke(); } }

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
            _session.Stir.PlayersPresent = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
            bool failed = _session.Phase == DivePhase.Floor && _session.Stir.Tick(Mathf.Min(Time.deltaTime, MaxTickSeconds));   // a collapse that ran out (plan 3 task 10 starts the collapse)
            if (failed) _session.Fail();
            Publish();
            RefreshEncounterPlan();
            if (failed) DiveFailed?.Invoke();
        }

        private void Publish()
        {
            DiveSession s = _session;
            DiveStir stir = s.Stir;
            int floor = DiveFloor;
            int found = 0;
            foreach (int id in s.Pieces.FoundIds) if (!PieceCatalog.IsPerson(id)) found++;
            var next = new Summary
            {
                StirValue = Mathf.RoundToInt(stir.Value), ExtraEnemiesPerRoom = stir.ExtraEnemyLatched ? ExtraEnemiesPerRoomOnLatch : 0, DeepSecondsLeft = Mathf.CeilToInt(stir.CollapseSecondsLeft),
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
            _session = session; _heal = null; _visitExtraOwner = -1; if (EncounterPlan != null) { EncounterPlan = null; EncounterChanged?.Invoke(); } Publish(); return true;
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
            _visitExtraOwner = -1;
            Publish();
            return true;
        }

        // Host only: the stair to the next floor (DiveSession.EnterFloor: only the next floor, and floor 5's only after the bank choice).
        public bool ServerEnterFloor(int floor)
        {
            if (!Host || !_session.EnterFloor(floor)) return false;
            Publish();
            return true;
        }

        // Host only: the party goes home from the bank room (the checkpoint is held afterwards) / finishes the Phase 1 stand-in ending after floor 7.
        public bool ServerGoHome()
        {
            if (!Host || !_session.GoHome()) return false;
            _visitExtraOwner = -1; Publish(); return true;
        }

        public bool ServerFinish()
        {
            if (!Host || !_session.Finish()) return false;
            Publish(); return true;
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
            return bag != null && bag.Add(itemId);
        }

        public bool ServerAddGold(ulong clientId, int amount)
        {
            PlayerBag bag = ServerBagOf(clientId);
            return bag != null && bag.AddGold(amount);
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

        private int _previousExtraOwner = -1;   // in memory for this host session only (saving it is plan 3)
        private int _visitExtraOwner = -1;      // the 3-player extra slot's owner picked when this floor's visits opened (-1 = none); a late joiner's visit reuses it

        // Entering the bank room (DiveSession.EnterBankRoom): every connected, keyed client's bag gets a bank visit with BankSlots.SlotsFor capacity; the 3-player extra slot goes to a seeded pick that
        // skips the previous owner. False when the session refuses (not on the cleared bank floor, or already chosen).
        private bool OpenBankVisits()
        {
            var ids = new System.Collections.Generic.List<ulong>();
            foreach (ulong cid in NetworkManager.ConnectedClientsIds) if (PlayerIdOf(cid) != null) ids.Add(cid);   // an unkeyed client (not in the roster yet) takes no slot and does not count
            if (ids.Count == 0) return false;
            if (ids.Count > 4) ids.RemoveRange(4, ids.Count - 4);
            var slots = new System.Collections.Generic.List<int>();
            var bagIds = new System.Collections.Generic.List<string>();
            foreach (ulong id in ids) { slots.Add(PlayerSlotOf(id)); ServerBagOf(id); bagIds.Add(PlayerIdOf(id)); }   // ServerBagOf makes the bag a visit opens on
            if (!_session.EnterBankRoom(slots, DiveSeed, _previousExtraOwner, bagIds)) return false;
            _visitExtraOwner = _session.ExtraSlotOwner;
            if (_visitExtraOwner >= 0) _previousExtraOwner = _visitExtraOwner;
            return true;
        }

        // The party is in the bank room and this client's bag has no visit (it joined after the room opened): open one now, sized for the party as it stands.
        // Decision (plan 2 task 6): a late joiner can lift the party's total slots above BankSlots.TotalSlots (the spec is silent); the others' visits keep their size.
        private void EnsureVisit(ulong clientId)
        {
            if (_session.Phase != DivePhase.BankRoom) return;
            PlayerBag bag = ServerBagOf(clientId);
            if (bag == null || bag.BankOpen) return;
            int keyed = 0;
            foreach (ulong cid in NetworkManager.ConnectedClientsIds) if (PlayerIdOf(cid) != null) keyed++;   // unkeyed clients do not count toward the party
            int n = Mathf.Clamp(keyed, 1, 4);
            bag.OpenBank(BankSlots.SlotsFor(n, PlayerSlotOf(clientId), _visitExtraOwner));
        }

        private void EnsureVisitsForEveryone()
        {
            foreach (ulong id in NetworkManager.ConnectedClientsIds) EnsureVisit(id);
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
        public bool ServerFailDive() { if (!Host) return false; _session.Fail(); Publish(); return true; }
        // The bank press, a stand-in until plan 3 task 8 gives the room its picks and the confirm window: on the cleared bank floor the first press enters the bank room (visits open, everyone is
        // healed), the second pushes on (picks leave the bag, the bank locks, the stair opens). No position check (host logic, e.g. a scripted bank).
        public bool ServerBank()
        {
            if (!Host) return false;
            if (_session.Phase == DivePhase.BankRoom)
            {
                EnsureVisitsForEveryone();   // a mid-room joiner who never picked still gets the push-on with the party
                if (!_session.PushOn()) return false;
                _visitExtraOwner = -1;
                Publish();
                Banked?.Invoke();
                return true;
            }
            if (!OpenBankVisits()) return false;
            Publish();
            HealEveryone();
            Banked?.Invoke();
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

        public bool ServerTryBankFor(ulong clientId)
        {
            return BankAskerOk(clientId, false) && ServerBank();
        }

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
