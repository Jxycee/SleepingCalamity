// Authority: host. M5 contents of the built Cellars floor: enemies, chests, keys, the locked door, bakery pieces. The host alone decides everything and writes ONE
// host-written NetworkVariable (State: opened-KEY-chest mask, collected-piece mask, cleared-room mask, key/door/exit flags, the dropped gate key) tagged with the (seed, floor) it
// belongs to, so a late joiner receives it with the spawn and a peer ignores a state from another floor. Every peer builds the floor LOCALLY (CellarFloorSync) and this
// object only shows state on it: chests lowered when opened, the gate hidden when the door is open, piece spheres hidden when collected, a gold cube where the gate key lies.
// Chests are PER PLAYER (plan 2, spec decision 39): a loot chest opens once for each player, who rolls their own item (CellarLoot.RollChest with the roster slot, the dive's momentum rank bonus and
// whether they already own boots) into their own bag (DiveHaulSync.ServerAddItem). A gold purse roll goes to DiveHaulSync.ServerAddGold and never to PlayerBag.Add. A chest opened with a FULL bag
// (PlayerBag.IsFull, the carried satchel counts as a slot) is refused: it stays closed for that player and only that player is told, by an owner-only RPC (plan 3 decision 6). Which loot chests a player opened lives in that player's PlayerBag (host-owned) and rides on that
// player's owner-only PlayerBagSync, so a peer draws a loot chest lowered only for its own local player; the shared State.Chests bit is never set for a loot chest. The KEY chest and the
// keys stay in the shared State (one room key for the party: State.Chests bit + RoomKeyHeld).
// The LOST SATCHEL (plan 3 task 9, decisions 33-35) is per player the same way: whether one waits on this floor rides on that player's owner-only PlayerBagSync view (BagView.SatchelWaiting + SatchelFloor, so a
// late joiner or a restore gets it with the spawn and no toast ever replays), and each peer builds the prop locally, only when ITS OWN view says a satchel waits here, at CellarPieceSpots.SatchelSpot(layout, dive
// seed). Another client's view has no such satchel, so it never draws one; the host's pick-up (a player within SatchelRange of the spot, DiveHaulSync.ServerPickUpSatchel) only ever finds the asker's own.
// Clients never write. They ask: RequestOpenChestRpc(chestIndex) / RequestOpenDoorRpc (SendTo.Server); the host re-checks index, not-already-open, key held and the
// requesting player's XZ range (ChestRange / GateRange) from its own copy of the player object, so a client gate is only a prompt. The owner-side Interact prompt (rising
// edge of the local hero's InteractPressed/Held) picks the nearest chest or the gate in range and sends the RPC.
// Host logic (Update, host only): (1) floor change or DiveFailed resets per-floor state and despawns the floor's enemies; (2) a player stepping into a Combat room for the
// first time spawns that room's CellarEncounterPlan spawns (DiveHaulSync.EncounterPlan, extras at that moment included) through CombatHost.SpawnEnemy: Tier1 = floor-banded
// Slime/Broom/RatKing (CellarEnemyMix), Tier2 = RatKing tier 2, the bakery's WardenCell = DoughWarden; (2b) the Ambush floor's ambush room (host-only twist) springs its enemies after FloorTwistTuning.AmbushDelaySeconds at CellarAmbush ring cells, cancelled (room re-armed) if no living player is left when the delay ends; (3) when the plan's Hunter appears one tier-2 RatKing spawns at its cell;
// (2c) ENCOUNTER TEMPLATES (spawner wiring, host only; clients never spawn): for the 12 pool combat rooms CellarTemplateSpawns.Resolve picks the kinds, cells and waves (EncounterBuilder, party size = connected players, isAmbushRoom = plan.AmbushRoom == room)
// and holds the count to the plan's live count; wave 1 spawns on the host tick after the last wave-0 enemy dies (the room is not cleared in between). Rooms it returns null for (bakery set pieces, Tier 2 / warden rooms) keep the plan-driven spawns above;
// (4) the enemy spawned for layout.Carrier drops the GATE key where it falls (only on death: a wipe/clear despawn drops nothing, resets the room and it respawns on re-entry): a player within GateKeyRange picks it up and ExitUnlocked is raised; keys live only in this state (GateKey, RoomKeyHeld), never in the haul's loot;
// (4b) ENEMY DROPS (host only): a real death of a tracked enemy (not a despawn, not the hunter or a pump-wave enemy) gives every still-connected player in the enemy's HitLog their OWN roll
// (CellarLoot.RollEnemyDrop, per roster slot) into their own bag via DiveHaulSync.ServerAddItem, paid on the next host tick (the killing blow joins HitLog after ServerDied fires). A full bag drops nothing
// for that player and only that player is told (NoticeOwner). The carrier's gate key is unchanged and never loot. Bags already ride the world save, so a drop needs no save field;
// (5) pieces: a player within PieceRange of an uncollected piece spot adds it to the haul (ServerAddPiece), the collected mask follows the haul every frame so a failed
// dive shows the pieces again; (6) the room key (key chest) lets the host open the locked gate, by Interact or by walking up to it.
// Spine hooks: subscribe to the host event ExitUnlocked(int floor) (fired once per floor when the gate key is picked up; State.ExitUnlocked stays true on every peer until the
// floor changes) to open the stair. Room clearing is kept here (ClearedRooms mask): DiveHaulSync.ServerMarkCleared marks a whole FLOOR cleared and is not called from here.
// Players are ignored for room entry and piece pickup until they are inside the armed floor's bounds and have moved over StaleMoveDistance from where they stood at arm time
// (the owner teleport after a floor change may not have landed yet).
// Range checks also need line of sight (WallBetween: a horizontal ray at eye height against the floor's Walls/Pillars/Doors colliders; the floor's colliders are on the default layer, not Structure). Ambush springs keep 4 m from every player and wait for someone in the room; the hunter (re)spawns only with no player within 8 m; the dropped gate key settles on a walkable cell.
// FLOOR OBJECTIVES (plan 5 task 4; authority: host writes, owners ask). The floor's objective (FloorObjectivePlan.For, a pure function of the layout, so every peer computes the same objective and cells) is written to
// State.Objective at arm time. TwoValves: an owner holding Interact within ValveReachMeters of an unturned valve sends ValveHoldRpc(valve) every ReportInterval; the host re-checks the sender (up, live, on this floor, within reach + 0.5 m),
// feeds Core ValveHold, and sets State.ValveMask bits (a turned valve stays turned, any order); both turned sets ExitUnlocked. HoldPump: one press within PumpStartReach sends StartPumpRpc; the host re-checks and starts Core PumpHold,
// ticks it with the living heroes inside PumpRingMeters and the pump-wave enemies at the pump, spawns the waves (Tier-1 roster enemies at CellarAmbush ring cells of the pump room, one at a time, never above GameMetrics.MaxLiveEnemies on the floor),
// writes State.PumpStarted / PumpPercent (whole percents only), and at 100% sets ExitUnlocked. A valve or pump floor keeps the gate-key carrier as an ordinary enemy (it never drops a key; GateKey stays 0). Peers build the valve and pump
// markers locally (CellarObjectiveMarkers, no NetworkObject) and restyle them from the replicated state, so a late joiner sees valves turned and the pump's progress with the spawn.
// Known limits: players come from NetworkManager.ConnectedClients player objects (PlayersOverride replaces that in tests); enemies of a room whose stir extras latch after
// entry are not topped up; the greybox CombatHost this creates (when the scene has none) runs with waves off.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Presentation
{
    public sealed class CellarContentsSync : NetworkBehaviour
    {
        public const float StaleMoveDistance = 3f;
        public const float ChestRange = 2.5f, GateRange = 3f, GateKeyRange = 1.5f, PieceRange = 1.2f, SatchelRange = 1.5f;
        public const string BagFullText = "Your bag is full.";

        public struct PlayerPos { public ulong ClientId; public Vector3 Position; }

        public struct ContentsState : INetworkSerializable, System.IEquatable<ContentsState>
        {
            public uint Seed; public int Floor;
            public ulong Chests, Pieces, Cleared;
            public int PieceCount;      // this floor's pieces not already home (host-set at arm time; peers place that many spots; spot i = the i-th such piece id, not PieceId(floor, i))
            public bool RoomKeyHeld, DoorOpen, ExitUnlocked;
            public byte GateKey;        // 0 none yet, 1 lying at GateKeyPos, 2 taken
            public Vector3 GateKeyPos;
            public byte Objective;      // FloorObjective of this floor (host-set at arm time)
            public byte ValveMask;      // bit i = valve i turned (TwoValves)
            public bool PumpStarted;    // HoldPump: the pump was started
            public byte PumpPercent;    // HoldPump: whole percent 0-100
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref Seed); s.SerializeValue(ref Floor); s.SerializeValue(ref Chests); s.SerializeValue(ref Pieces); s.SerializeValue(ref Cleared); s.SerializeValue(ref PieceCount);
                s.SerializeValue(ref RoomKeyHeld); s.SerializeValue(ref DoorOpen); s.SerializeValue(ref ExitUnlocked); s.SerializeValue(ref GateKey); s.SerializeValue(ref GateKeyPos);
                s.SerializeValue(ref Objective); s.SerializeValue(ref ValveMask); s.SerializeValue(ref PumpStarted); s.SerializeValue(ref PumpPercent);
            }
            public bool Equals(ContentsState o) => Seed == o.Seed && Floor == o.Floor && Chests == o.Chests && Pieces == o.Pieces && Cleared == o.Cleared && PieceCount == o.PieceCount
                && RoomKeyHeld == o.RoomKeyHeld && DoorOpen == o.DoorOpen && ExitUnlocked == o.ExitUnlocked && GateKey == o.GateKey && GateKeyPos == o.GateKeyPos
                && Objective == o.Objective && ValveMask == o.ValveMask && PumpStarted == o.PumpStarted && PumpPercent == o.PumpPercent;
            public override bool Equals(object obj) => obj is ContentsState o && Equals(o);
            public override int GetHashCode() => unchecked((int)Chests * 31 + (int)Pieces * 7 + Floor);
        }

        public readonly NetworkVariable<ContentsState> Net = new NetworkVariable<ContentsState>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [SerializeField] private NetworkObject slimePrefab, ratPrefab, broomPrefab, wardenPrefab, healPrefab;
        public void Configure(NetworkObject slime, NetworkObject rat, NetworkObject broom, NetworkObject warden, NetworkObject heal)
        { slimePrefab = slime; ratPrefab = rat; broomPrefab = broom; wardenPrefab = warden; healPrefab = heal; }

        public CellarFloorSync FloorSync;   // optional; found in the scene when null
        public DiveHaulSync HaulSync;       // optional; found in the scene when null (host needs it for loot, pieces and the plan)
        public System.Action<List<PlayerPos>> PlayersOverride;   // test hook: replaces the player-object scan on the host

        // Host only: raised once per floor when the gate key is picked up (the spine opens the stair).
        public event System.Action<int> ExitUnlocked;
        // Host only: the floor's guardian (the bank floor's Dough Warden) died: the director clears the floor so the bank room opens.
        public event System.Action<int> GuardianDefeated;
        // Host only: a chest was opened (chest index, item id; RoomKeyId for the key chest; CellarLoot.GoldId for a purse).
        public event System.Action<int, int> ChestOpened;
        // Host only: a loot chest stayed closed because the opener's bag was full (client id, chest index).
        public event System.Action<ulong, int> ChestRefusedFull;
        // Host only: a lost satchel was picked up (client id).
        public event System.Action<ulong> SatchelPickedUp;
        // Host only: a room was entered / cleared (room index).
        public event System.Action<int> RoomEntered, RoomCleared;

        public ContentsState State => Net.Value;
        public int ChestRequestsHandled { get; private set; }   // host only: RPCs received, accepted or refused
        public int ValveHoldRequests { get; private set; }       // host only: valve hold reports received, accepted or refused
        public int LastLootItem { get; private set; }            // host only
        public int NoticeCount { get; private set; }             // any peer: owner-only notices this peer received (a joiner or a restore never gets one)
        public string LastNotice { get; private set; }           // any peer: the text of the last one
        public Enemy GateKeyCarrierEnemy => _carrierEnemy;       // host only (tests)
        public Enemy HunterEnemy => _hunter;                     // host only (tests)

        // ---- floor objective read API (any peer; from the replicated state, so a late joiner agrees) ----
        public const float ValveReportReachSlack = 0.5f;          // the host accepts a valve report from this much beyond ValveReachMeters (movement between the owner's check and the RPC)
        public static float PumpSecondsOverrideForTests;          // test hook: > 0 replaces FloorObjectiveTuning.PumpSeconds (progress runs faster; thresholds and waves are fractions of it)
        private ContentsState Cur => IsServer ? _st : Net.Value;
        public FloorObjective CurrentObjective => (FloorObjective)Cur.Objective;
        public int ValvesTurned { get { int m = Cur.ValveMask, n = 0; for (int i = 0; i < ValveHold.Count; i++) if ((m & (1 << i)) != 0) n++; return n; } }
        public float PumpProgress => Cur.PumpPercent / 100f;
        public bool PumpStarted => Cur.PumpStarted;
        public FloorObjectivePlan Plan => _plan;                 // computed locally per built floor (host and clients equal); null before a floor is built
        public FloorModifierSet Modifiers => _mods;              // the floor's modifiers, computed once per built floor (never per frame)

        // ---- per-peer built-floor handles ----
        private int _handledBuild = -1;
        private Transform[] _chests = new Transform[0];
        private bool[] _chestHoldsKey = new bool[0];   // per chest: the key chest (shared bit) rather than a per-player loot chest
        private PlayerBagSync _localBag;               // this peer's own player's bag view (per NetworkManager, see PlayerBagSync.ForLocalPlayer)
        private Transform _gate;
        private CellPos[] _pieceSpots = new CellPos[0];
        private GameObject[] _pieces = new GameObject[0];
        private GameObject _gateKeyMarker;
        private GameObject _satchel;                   // this peer's own player's lost-satchel prop (null when no satchel of theirs waits on this floor)
        private int _satchelBuild = -1;
        private bool _prevInteract;
        private float _nextLookup;

        // ---- host state ----
        private ContentsState _st;
        private readonly CellarRoomClear _clear = new CellarRoomClear();
        private readonly HashSet<int> _entered = new HashSet<int>();
        private sealed class Tracked { public int Room; public bool Carrier, Guardian; public int DropKey; }

        // ---- per-player enemy drops (host) ----
        // A real death (not a despawn) of a TRACKED enemy gives every player who hit it and is still connected their own roll (CellarLoot.RollEnemyDrop, keyed by the enemy's per-floor DropKey and the
        // player's roster slot) into their own bag. The kill is seen inside Enemy.ServerApplyHit, before CombatHost appends the killing blow to HitLog, so the drop is queued here and paid on the
        // next host tick. DropKey = room * 64 + the room's spawn count this floor (never reset within a floor, so a respawn after a wipe rolls fresh). The hunter and the pump-wave enemies drop nothing.
        public const string DropFullText = "Your bag is full: an enemy drop was lost.";
        public System.Func<int, int, int> EnemyDropRollOverride;   // test seam (host): (player slot, drop key) -> item id, 0 = no drop; null = CellarLoot.RollEnemyDrop
        public int EnemyDropsGiven { get; private set; }            // host only (tests)
        public event System.Action<ulong, int> EnemyDropGiven;      // host: (clientId, item id)
        private struct PendingDrop { public List<HitRecord> Hits; public uint Seed; public int Floor, Key, Tier; }
        private readonly List<PendingDrop> _drops = new List<PendingDrop>();
        private readonly Dictionary<int, int> _roomSpawns = new Dictionary<int, int>();
        /// <summary>Host test entry: queues the drop of an enemy that "died" with these hitters (bag ids, DiveHaulSync.PlayerIdOf), exactly as a tracked death would, so the real roll and payout run without
        /// spawning a room. False off the host or with no floor armed.</summary>
        public bool TestQueueEnemyDrop(IEnumerable<string> hitters, int dropKey, int tier = 1)
        {
            if (!IsServer || !IsSpawned || _layout == null) return false;
            var hits = new List<HitRecord>();
            foreach (string id in hitters) hits.Add(new HitRecord(id, "test", 0.0));
            _drops.Add(new PendingDrop { Hits = hits, Seed = _layout.Seed, Floor = _layout.Floor, Key = dropKey, Tier = tier });
            return true;
        }

        private int NextDropKey(int room) { _roomSpawns.TryGetValue(room, out int n); _roomSpawns[room] = n + 1; return room * 64 + (n & 63); }
        private readonly Dictionary<Enemy, Tracked> _tracked = new Dictionary<Enemy, Tracked>();
        private Enemy _carrierEnemy, _hunter; private bool _hunterSpawned;
        private int _carrierLostRoom = -1;   // the carrier despawned without dying while room-mates lived: its room resets once no tracked enemy is left, so the key stays obtainable
        private CombatHost _combat; private bool _ownsCombat;
        private int _hostBuild = -1; private bool _resetRequested;
        private DiveHaulSync _subscribedHaul;
        private readonly List<PlayerPos> _players = new List<PlayerPos>();
        private readonly List<PlayerPos> _rawPlayers = new List<PlayerPos>();   // unfiltered player positions for the spawn-safety guards
        private readonly List<PlanePoint> _planePoints = new List<PlanePoint>();   // PlanePoints() cache (no per-frame allocation while the hunter waits to respawn)
        private readonly List<int> _pieceIds = new List<int>();                  // host: piece id of each spot (the floor's non-home pieces, ascending)
        private readonly Dictionary<ulong, Vector3> _armPos = new Dictionary<ulong, Vector3>();   // host: where each player stood when the floor was armed
        private readonly HashSet<ulong> _live = new HashSet<ulong>();                              // host: players seen inside the floor after moving away from _armPos
        private Bounds _armBounds; private bool _hasBounds;
        private int _handledPieces = -2;

        // objective: per built floor, computed once on the build (never per frame) on every peer
        private CellarFloorLayout _infoLayout; private FloorObjectivePlan _plan; private FloorModifierSet _mods;
        private ulong _roomPaid;   // host: rooms already paid their Crowded/Ambush gold this floor visit (a room that resets and re-clears pays once); reset in ServerClearFloor
        private GameObject[] _valveMarkers = new GameObject[ValveHold.Count]; private GameObject _pumpMarker;
        private int _shownValveMask = -1, _shownPump = -1;
        private string _objectivePrompt = "";          // owner side: the hold/press prompt of a valve or the pump in reach ("" = none)
        private float _nextValveReport;
        // host
        private readonly ValveHold _valves = new ValveHold();
        private PumpHold _pump = new PumpHold();
        private readonly List<Enemy>[] _waveEnemies = { new List<Enemy>(), new List<Enemy>(), new List<Enemy>() };   // living members per pump wave
        private readonly int[] _waveToSpawn = new int[3]; private readonly float[] _waveNextAt = new float[3];      // members of a started wave still to appear, and when the next one does
        private int _pumpSpawnIndex;                                                                                   // running index for the roster remap and the ring cell pick
        private bool _unlockRaised;                                                                                    // ExitUnlocked was raised for this floor

        public override void OnNetworkSpawn() { _handledBuild = -1; _hostBuild = -1; }

        public override void OnNetworkDespawn()
        {
            if (_subscribedHaul != null) { _subscribedHaul.DiveFailed -= OnDiveFailed; _subscribedHaul = null; }
            UntrackAll();   // unhooked first, so clearing the enemies below is not read as a wipe
            if (_ownsCombat && _combat != null)
            {
                if (NetworkManager != null && NetworkManager.IsServer) _combat.ServerClearEnemies();
                Destroy(_combat.gameObject);
            }
            _combat = null; _ownsCombat = false;
            DestroyHandles();
        }

        private void DestroyHandles()
        {
            if (_gateKeyMarker != null) Destroy(_gateKeyMarker);
            if (_satchel != null) Destroy(_satchel);
            DestroyObjectiveMarkers();
            _gateKeyMarker = null; _satchel = null; _satchelBuild = -1; _chests = new Transform[0]; _gate = null; _pieces = new GameObject[0]; _pieceSpots = new CellPos[0]; _handledBuild = -1; _handledPieces = -2;
        }

        private void OnDiveFailed() { _resetRequested = true; }

        private void Resolve()
        {
            if ((FloorSync != null && HaulSync != null) || Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + 1f;
            if (FloorSync == null) FloorSync = FindFirstObjectByType<CellarFloorSync>();
            if (HaulSync == null) HaulSync = FindFirstObjectByType<DiveHaulSync>();
        }

        private void Update()
        {
            if (!IsSpawned) return;
            Resolve();
            CellarFloorSync fs = FloorSync;
            if (fs == null) return;
            RefreshHandles(fs);
            if (IsServer) HostTick(fs);
            ShowState(fs);
            if (IsClient) LocalInteract(fs);
        }

        // =================================================================================================================================
        // every peer: handles + visuals
        // =================================================================================================================================

        private void RefreshHandles(CellarFloorSync fs)
        {
            int key = fs.Built == null ? 0 : fs.BuildCount;
            if (key != _handledBuild)
            {
                DestroyPieceMarkers(); DestroyObjectiveMarkers();
                _handledBuild = key; _handledPieces = -2;
                if (fs.Built == null || fs.Layout == null) { _chests = new Transform[0]; _chestHoldsKey = new bool[0]; _gate = null; return; }
                CellarFloorLayout l = fs.Layout;
                EnsureFloorInfo(l, fs.Built.Dressing);
                _chests = new Transform[l.Chests.Count]; _chestHoldsKey = new bool[l.Chests.Count];
                for (int i = 0; i < l.Chests.Count; i++) { _chests[i] = CellarFloorBuilder.FindChest(fs.Built.Root, l.Chests[i]); _chestHoldsKey[i] = l.Chests[i].HoldsKey; }
                _gate = CellarFloorBuilder.FindGate(fs.Built.Root);
                BuildObjectiveMarkers(fs);
            }
            if (fs.Built == null || fs.Layout == null) return;
            // the piece count comes from the haul via the host state; wait until the state of THIS floor has arrived
            ContentsState s = IsServer ? _st : Net.Value;
            int want = StateMatches(fs, s) ? s.PieceCount : -1;
            if (want == _handledPieces || want < 0) return;
            DestroyPieceMarkers();
            _handledPieces = want;
            var spots = PieceSpotsFor(fs.Layout, fs.Built.Dressing, want);
            if (spots.Count < want) Debug.LogWarning("CellarContentsSync: floor " + fs.Layout.Floor + " has room for " + spots.Count + " of " + want + " pieces; the rest cannot be collected.");
            _pieceSpots = new CellPos[spots.Count]; for (int i = 0; i < spots.Count; i++) _pieceSpots[i] = spots[i];
            _pieces = CellarFloorBuilder.BuildPieces(fs.Built.Root, spots);
        }

        // Where the floor's pieces lie: the generated floors' free combat-room cells (CellarPieceSpots.Choose), or the baker's own cell on the bakery floor (plan 3 decision 10).
        private static IReadOnlyList<CellPos> PieceSpotsFor(CellarFloorLayout layout, CellarFloorDressing dressing, int count)
        {
            CellPos? baker = CellarPieceSpots.BakerSpot(layout);
            if (baker.HasValue) return count > 0 ? new[] { baker.Value } : new CellPos[0];
            return CellarPieceSpots.Choose(layout, dressing, count);
        }

        // The floor's objective plan and modifiers: pure functions of the layout (seed, floor), computed once per built layout on every peer (the host's arm reuses them).
        private void EnsureFloorInfo(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            if (layout == null || ReferenceEquals(_infoLayout, layout)) return;
            _infoLayout = layout;
            _plan = FloorObjectivePlan.For(layout, dressing);
            _mods = FloorModifierSet.For(layout.Seed, layout.Floor);
        }

        private void BuildObjectiveMarkers(CellarFloorSync fs)
        {
            if (_plan == null || fs.Built == null || fs.Built.Root == null) return;
            Transform root = fs.Built.Root;
            Vector3 At(CellPos c) => root.TransformPoint(CellarFloorBuilder.CellCenter(c.X, c.Y));
            if (_plan.Objective == FloorObjective.TwoValves)
            {
                _valveMarkers[0] = CellarObjectiveMarkers.BuildValve(root, At(_plan.ValveCellA));
                _valveMarkers[1] = CellarObjectiveMarkers.BuildValve(root, At(_plan.ValveCellB));
            }
            else if (_plan.Objective == FloorObjective.HoldPump) _pumpMarker = CellarObjectiveMarkers.BuildPump(root, At(_plan.PumpCell), FloorObjectiveTuning.PumpRingMeters);
            _shownValveMask = -1; _shownPump = -1;
        }

        private void DestroyObjectiveMarkers()
        {
            for (int i = 0; i < _valveMarkers.Length; i++) { if (_valveMarkers[i] != null) Destroy(_valveMarkers[i]); _valveMarkers[i] = null; }
            if (_pumpMarker != null) Destroy(_pumpMarker);
            _pumpMarker = null; _shownValveMask = -1; _shownPump = -1; _objectivePrompt = "";
        }

        private void DestroyPieceMarkers()
        {
            foreach (GameObject g in _pieces) if (g != null) Destroy(g);
            _pieces = new GameObject[0]; _pieceSpots = new CellPos[0];
        }

        private bool StateMatches(CellarFloorSync fs, in ContentsState s) => fs.Built != null && s.Floor == fs.BuiltFloor && s.Seed == fs.BuiltSeed;

        private void ShowState(CellarFloorSync fs)
        {
            ContentsState s = IsServer ? _st : Net.Value;
            bool ok = StateMatches(fs, s);
            ulong mine = ok ? LocalOpenedMask(s) : 0UL;
            for (int i = 0; i < _chests.Length; i++)
                if (_chests[i] != null) CellarFloorBuilder.SetChestOpened(_chests[i], ok && ChestIsOpen(i, s, mine));
            if (_gate != null) { bool open = ok && s.DoorOpen; if (_gate.gameObject.activeSelf == open) _gate.gameObject.SetActive(!open); }
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i] != null) { bool show = !(ok && (s.Pieces & (1UL << i)) != 0); if (_pieces[i].activeSelf != show) _pieces[i].SetActive(show); }
            bool keyLies = ok && s.GateKey == 1;
            if (keyLies && _gateKeyMarker == null) _gateKeyMarker = CellarFloorBuilder.BuildGateKey(transform, s.GateKeyPos);
            else if (!keyLies && _gateKeyMarker != null) { Destroy(_gateKeyMarker); _gateKeyMarker = null; }
            ShowSatchel(fs, s, ok);
            ShowObjective(s, ok);
        }

        // Valves turn and the pump changes colour with the replicated state (restyled only when it changes).
        private void ShowObjective(in ContentsState s, bool ok)
        {
            int mask = ok ? s.ValveMask : 0;
            if (mask != _shownValveMask)
            {
                _shownValveMask = mask;
                for (int i = 0; i < _valveMarkers.Length; i++) if (_valveMarkers[i] != null) CellarObjectiveMarkers.SetValve(_valveMarkers[i], (mask & (1 << i)) != 0);
            }
            int pump = !ok ? 0 : (s.PumpPercent >= 100 || (s.ExitUnlocked && s.Objective == (byte)FloorObjective.HoldPump)) ? 2 : s.PumpStarted ? 1 : 0;
            if (pump != _shownPump)
            {
                _shownPump = pump;
                if (_pumpMarker != null) CellarObjectiveMarkers.SetPump(_pumpMarker, pump >= 1, pump == 2);
            }
        }

        // The local player's own bag view (see LocalOpenedMask for the per-NetworkManager lookup); false before the hero exists.
        private bool TryLocalView(out BagView v)
        {
            v = default;
            if (_localBag == null || !_localBag.IsSpawned || !_localBag.IsOwner) _localBag = PlayerBagSync.ForLocalPlayer(NetworkManager);
            if (_localBag == null) return false;
            v = _localBag.View;
            return true;
        }

        // The lost-satchel prop: drawn only while THIS peer's own player has one waiting on the state's floor; rebuilt when the floor is rebuilt.
        private void ShowSatchel(CellarFloorSync fs, in ContentsState s, bool ok)
        {
            bool want = false;
            if (ok && fs.Layout != null && TryLocalView(out BagView v)) want = v.SatchelWaiting && v.SatchelFloor == s.Floor;
            if (_satchel != null && (!want || _satchelBuild != fs.BuildCount)) { Destroy(_satchel); _satchel = null; }
            if (!want || _satchel != null) return;
            if (!TrySatchelCell(fs.Layout, s.Seed, out CellPos cell)) return;
            _satchel = CellarFloorBuilder.BuildSatchel(transform, fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y)));
            _satchelBuild = fs.BuildCount;
        }

        // SatchelSpot resolves the floor's dressing and sorts its free cells: far too much for every frame, so the last answer is kept for its (layout, seed).
        private CellarFloorLayout _spotLayout; private uint _spotSeed; private CellPos _spotCell; private bool _spotOk;

        private bool TrySatchelCell(CellarFloorLayout layout, uint seed, out CellPos cell)
        {
            if (layout == null) { cell = default; return false; }
            if (!ReferenceEquals(_spotLayout, layout) || _spotSeed != seed)
            {
                _spotLayout = layout; _spotSeed = seed;
                try { _spotCell = CellarPieceSpots.SatchelSpot(layout, seed); _spotOk = true; }
                catch (System.InvalidOperationException) { _spotCell = default; _spotOk = false; }   // a floor with no free cell: no prop (and no pick-up)
            }
            cell = _spotCell;
            return _spotOk;
        }

        // The loot chests the local player opened on the state's (seed, floor), from the owner-only PlayerBagSync view of THIS peer's local player object (0 before it exists, or when the view
        // is of another floor). Resolved per NetworkManager, not through PlayerBagSync.Local: several peers in one process (tests) would all see the last spawned one.
        private ulong LocalOpenedMask(in ContentsState s)
        {
            if (_localBag == null || !_localBag.IsSpawned || !_localBag.IsOwner) _localBag = PlayerBagSync.ForLocalPlayer(NetworkManager);
            if (_localBag == null) return 0UL;
            BagView v = _localBag.View;
            return v.ChestSeed == s.Seed && v.ChestFloor == s.Floor ? v.OpenedChests : 0UL;
        }

        // The key chest is shared (State.Chests); a loot chest is open only for the player who opened it (`mine` from LocalOpenedMask).
        private bool ChestIsOpen(int i, in ContentsState s, ulong mine)
        {
            ulong bit = 1UL << i;
            return i < _chestHoldsKey.Length && _chestHoldsKey[i] ? (s.Chests & bit) != 0 : (mine & bit) != 0;
        }

        /// <summary>This peer's view: is chest `i` open for the local player (a loot chest after they opened it, the key chest once anyone opened it)? False until a floor of this state is built.</summary>
        public bool ChestOpenForLocal(int i)
        {
            CellarFloorSync fs = FloorSync;
            if (fs == null || i < 0 || i >= _chests.Length || i >= 64) return false;
            ContentsState s = IsServer ? _st : Net.Value;
            return StateMatches(fs, s) && ChestIsOpen(i, s, LocalOpenedMask(s));
        }

        // =================================================================================================================================
        // owner side: the Interact prompt
        // =================================================================================================================================

        // Restructured for the valves (plan 5 task 4): the held-valve report runs on EVERY frame the button is down, so it comes before the rising-edge return; a press that starts a valve hold or the pump is
        // not also a chest, gate or key press.
        private void LocalInteract(CellarFloorSync fs)
        {
            _objectivePrompt = "";
            NetworkClient me = NetworkManager.LocalClient;
            NetworkObject po = me?.PlayerObject;
            if (po == null) return;
            HeroCombat hc = po.GetComponent<HeroCombat>();
            if (hc == null || hc.Input == null) return;
            var intent = hc.Input.Intent;
            bool now = intent.InteractPressed || intent.InteractHeld;
            bool edge = now && !_prevInteract; _prevInteract = now;
            if (fs.Built == null) return;
            HeroVitals vitals = po.GetComponent<HeroVitals>();
            bool up = vitals == null || vitals.IsUp;
            Vector3 at = po.transform.position;
            int valve = up ? NearestUnturnedValve(fs, at) : -1;
            if (valve >= 0)
            {
                _objectivePrompt = ValvePromptText;
                if (now && Time.time >= _nextValveReport) { _nextValveReport = Time.time + FloorObjectiveTuning.ReportInterval; RequestValveHold(valve); }   // game time, like the host's report clock (a fixed-step bot run is faster than real time)
                return;   // the whole press belongs to the valve
            }
            bool pump = up && PumpStartableAt(fs, at);
            if (pump) _objectivePrompt = PumpPromptText;
            if (!edge) return;
            if (pump) { RequestStartPump(); return; }
            int best = FindLocalTarget(fs, at, out bool gate);
            if (best >= 0) RequestOpenChest(best);
            else if (gate) RequestOpenDoor();
        }

        public const string ValvePromptText = "Hold [{0}] to open the valve", PumpPromptText = "[{0}] start the pump when you're ready";

        // Owner side: the unturned valve within ValveReachMeters of `p` (flat), -1 when none. Reads the replicated state of the built floor.
        private int NearestUnturnedValve(CellarFloorSync fs, Vector3 p)
        {
            ContentsState s = Net.Value;
            if (_plan == null || _plan.Objective != FloorObjective.TwoValves || !StateMatches(fs, s) || s.Objective != (byte)FloorObjective.TwoValves) return -1;
            int best = -1; float bestD = FloorObjectiveTuning.ValveReachMeters * FloorObjectiveTuning.ValveReachMeters;
            for (int i = 0; i < ValveHold.Count; i++)
            {
                if ((s.ValveMask & (1 << i)) != 0 || _valveMarkers[i] == null) continue;
                float d = FlatSqr(p, _valveMarkers[i].transform.position);
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // Owner side: the pump is not started yet and `p` is within PumpStartReach of it.
        private bool PumpStartableAt(CellarFloorSync fs, Vector3 p)
        {
            ContentsState s = Net.Value;
            if (_plan == null || _plan.Objective != FloorObjective.HoldPump || _pumpMarker == null || !StateMatches(fs, s) || s.Objective != (byte)FloorObjective.HoldPump || s.PumpStarted) return false;
            return FlatSqr(p, _pumpMarker.transform.position) <= FloorObjectiveTuning.PumpStartReach * FloorObjectiveTuning.PumpStartReach;
        }

        private void OnGUI()
        {
            if (!IsSpawned || _objectivePrompt.Length == 0) return;
            IInputService input = HeroInput.ResolveDefault();
            string key = input != null ? input.GetBindingDisplayString(InputActionNames.Interact) : "";
            GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 150f, 380f, 30f), string.Format(_objectivePrompt, string.IsNullOrEmpty(key) ? "E" : key));
        }

        // The chest (index) or the gate that an Interact press at `p` would use; -1 and gate=false when none is in range.
        private int FindLocalTarget(CellarFloorSync fs, Vector3 p, out bool gate)
        {
            gate = false;
            if (fs == null || fs.Built == null) return -1;
            ContentsState s = Net.Value; bool ok = StateMatches(fs, s);
            ulong mine = ok ? LocalOpenedMask(s) : 0UL;
            int best = -1; float bestD = ChestRange * ChestRange;
            for (int i = 0; i < _chests.Length; i++)
            {
                if (_chests[i] == null || (ok && ChestIsOpen(i, s, mine))) continue;   // a chest already open for the local player takes no press
                float d = FlatSqr(p, _chests[i].position);
                if (d <= bestD && !WallBetween(fs.Built.Root, p, _chests[i].position)) { bestD = d; best = i; }
            }
            if (best >= 0) return best;
            gate = _gate != null && !(ok && s.DoorOpen) && FlatSqr(p, _gate.position) <= GateRange * GateRange && !WallBetween(fs.Built.Root, p, _gate.position);
            return -1;
        }

        /// <summary>This peer's view: is the local player's lost satchel drawn on the floor right now? (Tests; a real renderer is judged on the walk.)</summary>
        public bool SatchelShownForLocal => _satchel != null && _satchel.activeInHierarchy;
        public bool TrySatchelPosition(out Vector3 pos) { pos = default; if (_satchel == null) return false; pos = _satchel.transform.position; return true; }

        /// <summary>Owner side: true when an Interact press at `p` would open a chest or the gate. The dive director then leaves that press to the contents (one press, one action).</summary>
        public bool WouldUseInteract(Vector3 p)
        {
            int c = FindLocalTarget(FloorSync, p, out bool gate);   // the locked gate takes no press without the key
            return c >= 0 || (gate && Net.Value.RoomKeyHeld) || (FloorSync != null && (NearestUnturnedValve(FloorSync, p) >= 0 || PumpStartableAt(FloorSync, p)));
        }

        public void RequestOpenChest(int chestIndex) { RequestOpenChestRpc(chestIndex); }
        public void RequestOpenDoor() { RequestOpenDoorRpc(); }
        public void RequestValveHold(int valve) { ValveHoldRpc((byte)valve); }
        public void RequestStartPump() { StartPumpRpc(); }

        [Rpc(SendTo.Server)]
        private void RequestOpenChestRpc(int chestIndex, RpcParams rpcParams = default) { ChestRequestsHandled++; ServerTryOpenChest(rpcParams.Receive.SenderClientId, chestIndex); }

        [Rpc(SendTo.Server)]
        private void RequestOpenDoorRpc(RpcParams rpcParams = default) { ServerTryOpenDoor(rpcParams.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        private void ValveHoldRpc(byte valve, RpcParams rpcParams = default) { ValveHoldRequests++; ServerReportValve(rpcParams.Receive.SenderClientId, valve); }

        [Rpc(SendTo.Server)]
        private void StartPumpRpc(RpcParams rpcParams = default) { ServerTryStartPump(rpcParams.Receive.SenderClientId); }

        // Host to ONE client: a short refusal (the bag is full). A one-shot: it is never resent to a late joiner and a save restore sends none.
        private void NoticeOwner(ulong clientId, string text) { NoticeOwnerRpc(text, RpcTarget.Single(clientId, RpcTargetUse.Temp)); }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NoticeOwnerRpc(string text, RpcParams rpcParams = default)
        {
            NoticeCount++; LastNotice = text;
            DiveDirector.ShowNoticeOnLocal(NetworkManager, text);
        }

        // =================================================================================================================================
        // host
        // =================================================================================================================================

        private bool HostReady(CellarFloorSync fs) => IsServer && IsSpawned && _layout != null && _root != null && _st.Floor == _layout.Floor && _st.Seed == _layout.Seed;

        // Playtest 13: range alone ignored walls. A wall (Walls/Pillars/Doors group collider of the built floor; the locked gate itself does not count: it sits in the opening) between
        // `a` and `b` on a horizontal ray at eye height blocks the interaction. The floor's colliders are on the default layer, not Structure, so the filter is by group, which also keeps
        // props (crates, barrels) from blocking. `root` is the built floor's root.
        private static bool IsWallCollider(Transform t, Transform root)
        {
            if (t.name == "LockedGate") return false;
            bool group = false;
            for (Transform c = t; c != null; c = c.parent)
            {
                if (c == root) return group;   // only colliders under the built floor's root count
                if (c.name == CellarFloorBuilder.WallGroup || c.name == CellarFloorBuilder.PillarGroup || c.name == CellarFloorBuilder.DoorGroup) group = true;
            }
            return false;
        }

        private static bool WallBetween(Transform root, Vector3 a, Vector3 b)
        {
            if (root == null) return false;
            float y = root.position.y + 1f;
            a.y = y; b.y = y;
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 0.01f) return false;
            RaycastHit[] hits = Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits) if (IsWallCollider(h.collider.transform, root)) return true;
            return false;
        }

        private static bool InsideWall(Transform root, Vector3 p)
        {
            if (root == null) return false;
            foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * 0.5f, 0.15f, ~0, QueryTriggerInteraction.Ignore)) if (IsWallCollider(c.transform, root)) return true;
            return false;
        }

        // Spawn-safety points: EVERY connected player's position, ignoring the downed (IsUp) and not-yet-live (FilterStale) filters of _players. A hunter that despawns during a wipe,
        // or an ambush that springs while a hero is down or still loading in, must not appear on top of one. (The carrier/warden spawns and the plan-cell fallback ignore players.)
        private List<PlanePoint> PlanePoints()   // returns the shared cache: use it before the next call, never keep it
        {
            _rawPlayers.Clear(); _planePoints.Clear();
            if (PlayersOverride != null) PlayersOverride(_rawPlayers);
            else
                foreach (KeyValuePair<ulong, NetworkClient> kv in NetworkManager.ConnectedClients)
                {
                    NetworkObject po = kv.Value.PlayerObject;
                    if (po != null) _rawPlayers.Add(new PlayerPos { ClientId = kv.Key, Position = po.transform.position });
                }
            foreach (PlayerPos p in _rawPlayers) { Vector3 l = _root.InverseTransformPoint(p.Position); _planePoints.Add(new PlanePoint(l.x, -l.z)); }
            return _planePoints;
        }

        // Where the gate key lies when its carrier died at `at`: the same spot unless that is inside a wall or off the walkable cells, then the nearest walkable cell's centre.
        private Vector3 SettledKeyPos(Vector3 at)
        {
            if (_layout == null || _root == null || _dressing == null) return at;
            Vector3 l = _root.InverseTransformPoint(at);
            var cell = new CellPos(Mathf.FloorToInt(l.x / CellarFloorBuilder.CellSize), Mathf.FloorToInt(-l.z / CellarFloorBuilder.CellSize));
            CellPos pick = CellarKeyDrop.Settle(_layout, _dressing, cell, InsideWall(_root, at), out bool moved);
            return moved ? World(null, pick) : at;
        }

        private static float FlatSqr(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz; }

        private Vector3 World(CellarFloorSync fs, CellPos c) => _root.TransformPoint(CellarFloorBuilder.CellCenter(c.X, c.Y));

        private void CollectPlayers()
        {
            _players.Clear();
            if (PlayersOverride != null) PlayersOverride(_players);
            else CollectFromNetwork();
            FilterStale();
        }

        // Drops players the host cannot trust yet: still at their pre-floor-change position, or outside the armed floor.
        private void FilterStale()
        {
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                PlayerPos p = _players[i];
                if (_live.Contains(p.ClientId)) continue;
                bool inside = !_hasBounds || (p.Position.x >= _armBounds.min.x && p.Position.x <= _armBounds.max.x && p.Position.z >= _armBounds.min.z && p.Position.z <= _armBounds.max.z);
                bool moved = !_armPos.TryGetValue(p.ClientId, out Vector3 was) || FlatSqr(p.Position, was) > StaleMoveDistance * StaleMoveDistance;
                if (inside && moved) _live.Add(p.ClientId); else _players.RemoveAt(i);
            }
        }

        private void CollectFromNetwork()
        {
            foreach (KeyValuePair<ulong, NetworkClient> kv in NetworkManager.ConnectedClients)
            {
                NetworkObject po = kv.Value.PlayerObject;
                if (po == null) continue;
                HeroVitals v = po.GetComponent<HeroVitals>();
                if (v != null && !v.IsUp) continue;
                _players.Add(new PlayerPos { ClientId = kv.Key, Position = po.transform.position });
            }
        }

        private bool TryPlayerPos(ulong clientId, out Vector3 pos)
        {
            CollectPlayers();
            foreach (PlayerPos p in _players) if (p.ClientId == clientId) { pos = p.Position; return true; }
            pos = default; return false;
        }

        // Host: pays the queued enemy deaths (see the per-player enemy drops block). Runs first in HostTick, before a new floor's arm clears the pending list.
        private void TickDrops()
        {
            if (_drops.Count == 0) return;
            PendingDrop[] due = _drops.ToArray(); _drops.Clear();
            foreach (PendingDrop d in due)
            {
                var seen = new HashSet<string>();
                foreach (HitRecord h in d.Hits)
                {
                    if (!seen.Add(h.PlayerId) || HaulSync == null || !HaulSync.TryClientOfPlayerId(h.PlayerId, out ulong cid)) continue;
                    int slot = HaulSync.PlayerSlotOf(cid);
                    int item = EnemyDropRollOverride != null ? EnemyDropRollOverride(slot, d.Key) : CellarLoot.RollEnemyDrop(d.Seed, d.Floor, d.Key, slot, d.Tier);
                    if (item <= 0) continue;
                    PlayerBag bag = HaulSync.ServerBagOf(cid);
                    if (bag == null) continue;
                    if (bag.IsFull) { NoticeOwner(cid, DropFullText); continue; }   // only that player is told; nobody else's drop moves
                    if (!HaulSync.ServerAddItem(cid, item)) continue;
                    EnemyDropsGiven++; EnemyDropGiven?.Invoke(cid, item);
                }
            }
        }

        private void HostTick(CellarFloorSync fs)
        {
            TickDrops();
            if (HaulSync != null && _subscribedHaul != HaulSync)
            {
                if (_subscribedHaul != null) _subscribedHaul.DiveFailed -= OnDiveFailed;
                _subscribedHaul = HaulSync; _subscribedHaul.DiveFailed += OnDiveFailed;
            }
            int build = fs.Built == null ? 0 : fs.BuildCount;
            if (AutoArm && build != _hostBuild)
            {
                _hostBuild = build; _resetRequested = false;
                if (fs.Built == null || fs.Layout == null) ServerClearFloor(); else ServerArmFloor(fs.Layout, fs.Built.Root);
            }
            else if (_resetRequested) { _resetRequested = false; if (_layout != null) ServerArmFloor(_layout, _root); }
            if (!HostReady(fs)) return;

            CollectPlayers();
            EnterRooms(fs);
            TickAmbush(fs);
            TickWaveTwo(fs);
            SpawnHunter(fs);
            TickGateKey();
            TickObjective(fs);
            TickPieces(fs);
            TickSatchels(fs);
            TickAutoDoor(fs);
            Publish();
        }

        // Host entry point 1: arm the contents for a built floor (resets all per-floor state, despawns the old floor's enemies). `floorRoot` is the built floor's root: every
        // world position (spawns, pieces, room entry) goes through its transform, so a floor built anywhere (the spine builds 200 m east of the hub) works. A floor 1-3 with no
        // usable gate-key carrier raises ExitUnlocked(floor) at once, so the stair can never soft-lock. The auto path (AutoArm, default on) calls this whenever the
        // CellarFloorSync builds a new floor and when the dive fails; the spine may set AutoArm = false and call it on FloorChanged instead.
        public bool ServerArmFloor(CellarFloorLayout layout, Transform floorRoot)
        {
            if (!IsServer || !IsSpawned || layout == null || floorRoot == null) return false;
            ServerClearFloor();
            _layout = layout; _root = floorRoot; _dressing = CellarFloorDressing.Resolve(layout);
            EnsureFloorInfo(layout, _dressing);
            _st = new ContentsState { Seed = layout.Seed, Floor = layout.Floor, PieceCount = ArmPieces(layout.Floor), Objective = (byte)_plan.Objective };
            _armPos.Clear(); _live.Clear(); _hasBounds = false;
            CellarFloorSync afs = FloorSync;
            if (afs != null && afs.Built != null && afs.Built.Root == floorRoot) { _armBounds = afs.Built.Bounds; _armBounds.Expand(new Vector3(2f, 0f, 2f)); _hasBounds = true; }
            _players.Clear();
            if (PlayersOverride != null) PlayersOverride(_players); else CollectFromNetwork();
            foreach (PlayerPos p in _players) _armPos[p.ClientId] = p.Position;
            if (HasGateKeyStair(layout.Floor) && CarrierGatesExit && !CarrierIsValid(layout, _dressing)) _st.ExitUnlocked = true;   // a valve or pump floor waits for its objective instead
            Publish();
            if (_st.ExitUnlocked) { _unlockRaised = true; ExitUnlocked?.Invoke(layout.Floor); }
            return true;
        }

        // Host entry point 2: forget the floor (dive ended or failed): despawns its enemies and publishes an empty state. Safe to call when nothing is armed.
        public void ServerClearFloor()
        {
            if (!IsServer) return;
            UntrackAll(); _drops.Clear(); _roomSpawns.Clear();
            if (_combat != null) _combat.ServerClearEnemies();
            _clear.Reset(); _entered.Clear(); _ambushRoom = -1; _carrierEnemy = null; _carrierLostRoom = -1; _hunter = null; _hunterSpawned = false;
            _valves.Reset(); _unlockRaised = false; _roomPaid = 0UL;
            _layout = null; _root = null; _dressing = null; _armPos.Clear(); _live.Clear(); _hasBounds = false;
            _st = new ContentsState();
            if (IsSpawned) Publish();
        }

        // The pieces and people not yet found on `floor` (DivePieces.OnFloor), frozen now: spot i belongs to the i-th such id (ascending) for the whole visit, so finding spot 0 never shifts the
        // next piece into it. A person (the baker) counts as a spot too. Without a running session there are none.
        private int ArmPieces(int floor)
        {
            _pieceIds.Clear();
            DivePieces p = HaulSync != null && HaulSync.Session != null ? HaulSync.Session.Pieces : null;
            if (p != null) foreach (int id in p.OnFloor(floor)) _pieceIds.Add(id);
            return _pieceIds.Count;
        }

        // Only the floors whose way down needs the gate key (1-4 and 6: a stair that exists, and not the bank floor, whose stair opens on the push-on choice instead) have a carrier; the bank
        // floor and the ending floor have none.
        public static bool HasGateKeyStair(int floor) => UndercroftPlan.IsFloor(floor) && !DiveFloorTable.HasBank(floor) && DiveFloorTable.HasStair(floor, true);

        // The carrier is usable when its room exists, it stands in that room and an authored spawn of a Combat room sits on its cell.
        private static bool CarrierIsValid(CellarFloorLayout l, CellarFloorDressing dressing)
        {
            GateKeyCarrier c = l.Carrier;
            if (c == null || c.RoomIndex < 0 || c.RoomIndex >= l.Rooms.Count || l.RoomAt(c.X, c.Y) != c.RoomIndex) return false;
            foreach (EncounterSpawn s in CellarEncounterPlan.Build(l, dressing, 0, false).Spawns)
                if (s.RoomIndex == c.RoomIndex && s.Kind == EncounterKind.Authored && s.X == c.X && s.Y == c.Y) return true;
            return false;
        }

        // Host: may the stair of `floor` be used? True on a floor with no gate-key stair (the bank floor, the ending floor), on any other floor whose carrier is missing/invalid, and once the gate key was picked.
        // False for a gate-key floor that is not the armed one. The spine's wiring: ExitUnlocked += f => DiveDirector.Instance.ServerOpenStair(f).
        public bool IsExitUnlocked(int floor)
        {
            if (_layout != null && _layout.Floor == floor && !CarrierGatesExit && floor == DiveFloorTable.LastBuiltFloor) return _st.ExitUnlocked;   // floor 7's valve or pump gates the stand-in ending door
            if (!HasGateKeyStair(floor)) return true;
            return _layout != null && _layout.Floor == floor && _st.ExitUnlocked;
        }

        // False on a valve or pump floor: its gate-key carrier is an ordinary enemy (no key, no early unlock); Rescue, Guardian, the ending and the authored floors keep the carrier rules.
        private bool CarrierGatesExit => _plan == null || (_plan.Objective != FloorObjective.TwoValves && _plan.Objective != FloorObjective.HoldPump);

        /// <summary>Any peer: floor `floor`'s way out is gated by a valve or pump objective that is not finished (floor 7's ending door; the stair floors are gated by the stair itself). Before the floor's state arrives it answers from the locally computed plan (closed for a valve or pump floor).</summary>
        public bool IsObjectiveGated(int floor)
        {
            ContentsState s = Cur;
            if (s.Floor == floor)
            {
                if (s.ExitUnlocked) return false;
                return s.Objective == (byte)FloorObjective.TwoValves || s.Objective == (byte)FloorObjective.HoldPump;
            }
            // The state is not (yet) on this floor (just built, between floors, a late joiner): fail closed from the plan every peer computes for the built floor, never open.
            CellarFloorSync fs = FloorSync;
            if (fs != null && fs.Layout != null && fs.Built != null && fs.BuiltFloor == floor) EnsureFloorInfo(fs.Layout, fs.Built.Dressing);
            if (_plan == null || _infoLayout == null || _infoLayout.Floor != floor) return false;
            return _plan.Objective == FloorObjective.TwoValves || _plan.Objective == FloorObjective.HoldPump;
        }

        /// <summary>The objective line of the current floor for a prompt or the card: "Open both valves (1/2)", "Start the pump and hold it (40%)", "Find the gate key".</summary>
        public string ObjectiveLine => FloorCardText.ObjectiveLine(CurrentObjective, ValvesTurned, PumpProgress, PumpStarted, Cur.GateKey, Cur.ExitUnlocked);

        public bool AutoArm = true;
        private CellarFloorLayout _layout; private Transform _root; private CellarFloorDressing _dressing;

        private void Publish() { if (!_st.Equals(Net.Value)) Net.Value = _st; }

        private void UntrackAll()
        {
            foreach (Enemy e in new List<Enemy>(_tracked.Keys)) Unhook(e);
            _tracked.Clear(); _waveTwo.Clear(); _waveTwoDue.Clear();
            if (_hunter != null) Unhook(_hunter);
            ResetPump();
        }

        private void Unhook(Enemy e) { if (e == null) return; e.ServerDied -= OnEnemyDied; e.ServerDespawned -= OnEnemyDespawned; e.ServerDied -= OnHunterDied; e.ServerDespawned -= OnHunterDespawned; e.ServerDied -= OnWaveEnemyGone; e.ServerDespawned -= OnWaveEnemyGone; }

        private CombatHost EnsureCombat()
        {
            if (_combat != null) return _combat;
            CombatHost c = CombatHost.Current;
            if (c == null)
            {
                var go = new GameObject("CellarCombat"); go.transform.SetParent(transform, false);
                c = go.AddComponent<CombatHost>(); c.WavesEnabled = false; c.WipeResetsParty = false;
                c.Configure(slimePrefab, ratPrefab, broomPrefab, wardenPrefab, healPrefab);
                _ownsCombat = true;
            }
            c.ServerBegin(NetworkManager);
            c.RoomOf = RoomOfWorld;   // room-scoped aggro: an enemy notices any hero inside its own room (the same lookup EnterRooms uses)
            _combat = c; return c;
        }

        private CellarEncounterPlan CurrentPlan(CellarFloorSync fs)
        {
            CellarEncounterPlan p = HaulSync != null ? HaulSync.EncounterPlan : null;
            if (p != null && p.Floor == _layout.Floor && p.Seed == _layout.Seed) return p;
            DiveHaulSync.Summary s = HaulSync != null ? HaulSync.Current : default;
            return CellarEncounterPlan.BuildForDive(_layout, _dressing, s.ExtraEnemiesPerRoom, s.HunterLatched);
        }

        // Room index of a world position, -1 outside every room (or with no layout armed).
        private int RoomOfWorld(Vector3 world)
        {
            if (_layout == null || _root == null) return -1;
            Vector3 local = _root.InverseTransformPoint(world);
            return _layout.RoomAt(Mathf.FloorToInt(local.x / CellarFloorBuilder.CellSize), Mathf.FloorToInt(-local.z / CellarFloorBuilder.CellSize));
        }

        private void EnterRooms(CellarFloorSync fs)
        {
            foreach (PlayerPos p in _players)
            {
                Vector3 local = _root.InverseTransformPoint(p.Position);
                int room = _layout.RoomAt(Mathf.FloorToInt(local.x / CellarFloorBuilder.CellSize), Mathf.FloorToInt(-local.z / CellarFloorBuilder.CellSize));
                if (room < 0 || !_entered.Add(room)) continue;
                SpawnRoom(fs, room);
                RoomEntered?.Invoke(room);
            }
        }

        private int _ambushRoom = -1; private float _ambushDue;   // host: the ambush room waiting out AmbushDelaySeconds

        private void TickAmbush(CellarFloorSync fs)
        {
            if (_ambushRoom < 0 || Time.time < _ambushDue) return;
            int room = _ambushRoom; _ambushRoom = -1;
            if (_players.Count == 0) { _entered.Remove(room); return; }   // party wipe during the delay: no spring now, re-entering the room triggers it again
            bool inRoom = false;
            foreach (PlayerPos p in _players) if (RoomOfWorld(p.Position) == room) { inRoom = true; break; }
            if (!inRoom) { _ambushRoom = room; _ambushDue = Time.time + FloorTwistTuning.AmbushDelaySeconds; return; }   // everyone left during the delay: re-arm, nothing springs on an empty room
            SpawnRoom(fs, room, true);
        }

        private void SpawnRoom(CellarFloorSync fs, int room, bool ambushNow = false)
        {
            CellarEncounterPlan plan = CurrentPlan(fs);
            if (plan.CountIn(room) == 0) return;
            CombatHost combat = EnsureCombat();
            CellarFloorLayout l = _layout; int index = 0, slot = -1;
            CellPos[] ambush = null;
            if (plan.Twist == FloorTwist.Ambush && room == plan.AmbushRoom)   // host-only twist: the room's enemies spring from the ring cells after a short delay
            {
                if (!ambushNow) { _ambushRoom = room; _ambushDue = Time.time + FloorTwistTuning.AmbushDelaySeconds; return; }
                ambush = CellarAmbush.Cells(l, _dressing, plan, room, PlanePoints());
            }
            CellarTemplateRoom tr = CellarTemplateSpawns.Resolve(l, _dressing, plan, room, PresentPlayers, ambush);   // host: encounter template for the 12 pool combat rooms; null keeps the plan-driven spawns below (bakery set pieces, Tier 2 / warden rooms)
            if (tr != null)
            {
                foreach (TemplateSpawn ts in tr.InWave(0)) SpawnTemplateEnemy(fs, room, ts);
                List<TemplateSpawn> second = new List<TemplateSpawn>(tr.InWave(1));
                if (second.Count > 0 && RoomHasTracked(room)) _waveTwo[room] = second;   // wave 1 appears when wave 0 is cleared (TickWaveTwo)
                _st.Cleared = _clear.ClearedMask;
                return;
            }
            foreach (EncounterSpawn sp in plan.Spawns)
            {
                if (sp.RoomIndex != room || sp.Kind == EncounterKind.Hunter) continue;
                slot++;
                bool carrierSpawn = CarrierGatesExit && l.Carrier != null && l.Carrier.X == sp.X && l.Carrier.Y == sp.Y && sp.Kind == EncounterKind.Authored;
                if (carrierSpawn && _st.GateKey != 0) { index++; continue; }   // its key already dropped (or was taken): a re-entered room does not bring the carrier back
                bool warden = l.WardenCell.HasValue && l.WardenCell.Value.X == sp.X && l.WardenCell.Value.Y == sp.Y;
                if (warden && HaulSync != null && HaulSync.Session != null && (HaulSync.Session.StartedAtBank || HaulSync.Session.IsCleared(l.Floor))) { index++; continue; }   // a party that started at the bank (decision 2), or returns to a cleared bank floor (a resumed suspend), never meets the guardian
                int tier = EnemyCatalog.AuthoredTier(sp.Tier == SpawnTier.Tier2, warden, l.Floor);
                EnemyKind kind = warden ? EnemyKind.DoughWarden : sp.Tier == SpawnTier.Tier2 ? EnemyKind.RatKing : CellarEnemyMix.Tier1Kind(l.Seed, l.Floor, room, index, plan.RosterMask);
                index++;
                Enemy e = combat.SpawnEnemy(kind, tier, World(fs, ambush != null ? ambush[slot] : sp.Cell), room: room);
                bool carrier = CarrierGatesExit && l.Carrier != null && l.Carrier.X == sp.X && l.Carrier.Y == sp.Y && sp.Kind == EncounterKind.Authored;   // false on a valve or pump floor: an ordinary enemy
                if (e == null)
                {
                    if (carrier && !_st.ExitUnlocked) { _st.ExitUnlocked = true; Publish(); ExitUnlocked?.Invoke(_st.Floor); }   // the carrier could not spawn: never soft-lock the stair
                    continue;
                }
                _tracked[e] = new Tracked { Room = room, Carrier = carrier, Guardian = warden, DropKey = NextDropKey(room) };
                if (carrier) _carrierEnemy = e;
                _clear.Spawn(room);
                e.ServerDied += OnEnemyDied; e.ServerDespawned += OnEnemyDespawned;
            }
            _st.Cleared = _clear.ClearedMask;
        }

        // Host: one template enemy (Tier1, floor-banded tier). The carrier cell keeps the gate-key carrier; a re-entered room whose key already dropped does not bring it back.
        private void SpawnTemplateEnemy(CellarFloorSync fs, int room, TemplateSpawn ts)
        {
            bool carrier = CarrierGatesExit && ts.Carrier;
            if (carrier && _st.GateKey != 0) return;
            Enemy e = EnsureCombat().SpawnEnemy(ts.Kind, EnemyCatalog.AuthoredTier(false, false, _layout.Floor), World(fs, ts.Cell), room: room);
            if (e == null)
            {
                if (carrier && !_st.ExitUnlocked) { _st.ExitUnlocked = true; Publish(); ExitUnlocked?.Invoke(_st.Floor); }   // the carrier could not spawn: never soft-lock the stair
                return;
            }
            _tracked[e] = new Tracked { Room = room, Carrier = carrier, DropKey = NextDropKey(room) };
            if (carrier) _carrierEnemy = e;
            _clear.Spawn(room);
            e.ServerDied += OnEnemyDied; e.ServerDespawned += OnEnemyDespawned;
        }

        // Host: wave 1 of a template room. Its last wave-0 death leaves the room's alive count at one (Gone skips CellarRoomClear.Died), so the room cannot clear in between; this spawns
        // wave 1 on the next host tick and then settles that one count, which clears the room only if nothing could be spawned.
        private readonly Dictionary<int, List<TemplateSpawn>> _waveTwo = new Dictionary<int, List<TemplateSpawn>>();
        private readonly List<int> _waveTwoDue = new List<int>();

        private void TickWaveTwo(CellarFloorSync fs)
        {
            if (_waveTwoDue.Count == 0) return;
            foreach (int room in _waveTwoDue.ToArray())
            {
                _waveTwoDue.Remove(room);
                if (!_waveTwo.TryGetValue(room, out List<TemplateSpawn> second)) continue;
                _waveTwo.Remove(room);
                foreach (TemplateSpawn ts in second) SpawnTemplateEnemy(fs, room, ts);
                if (_clear.Died(room)) { _st.Cleared = _clear.ClearedMask; PayRoomClear(room); RoomCleared?.Invoke(room); }
                else _st.Cleared = _clear.ClearedMask;
                Publish();
            }
        }

        private void SpawnHunter(CellarFloorSync fs)
        {
            if (_hunterSpawned || _mods.Has(FloorModifier.Hushed)) return;   // Hushed: no hunter on this floor even when it is latched (the latch stays for the next floor)
            CellarEncounterPlan plan = HaulSync != null ? HaulSync.EncounterPlan : null;
            if (plan == null || plan.Hunter == null || plan.Floor != _layout.Floor || plan.Seed != _layout.Seed) return;
            if (!CellarSpawnSafety.FarFromAll(plan.Hunter.Cell, PlanePoints(), CellarSpawnSafety.HunterMinPlayerMeters)) return;   // never (re)appear on top of a player: wait until the cell is clear
            _hunterSpawned = true;
            _hunter = EnsureCombat().SpawnEnemy(EnemyKind.RatKing, EnemyCatalog.CappedTier(2), World(fs, plan.Hunter.Cell));
            if (_hunter != null) { _hunter.ServerDied += OnHunterDied; _hunter.ServerDespawned += OnHunterDespawned; }
        }

        private void OnHunterDied(Enemy e) { Unhook(e); }   // dead stays dead: no respawn
        private void OnHunterDespawned(Enemy e) { Unhook(e); if (e == _hunter) { _hunter = null; _hunterSpawned = false; } }   // wiped/cleared while alive: it comes back

        private void OnEnemyDied(Enemy e) { Gone(e, true); }
        private void OnEnemyDespawned(Enemy e) { Gone(e, false); }

        // An enemy left the world. A DEATH counts toward the room and the carrier's death drops the gate key. Any other despawn (party wipe, floor clear) is not a kill: no key, no
        // clear; once the room has no tracked enemy left it is forgotten (entered flag and counts), so it spawns afresh when a player walks in again.
        private void Gone(Enemy e, bool died)
        {
            if (e == null || !_tracked.TryGetValue(e, out Tracked t)) return;
            _tracked.Remove(e); Unhook(e);
            if (!died)
            {
                if (e == _carrierEnemy) _carrierEnemy = null;
                bool roomLeft = RoomHasTracked(t.Room);
                if (roomLeft) { _clear.Despawned(t.Room); if (t.Carrier && _st.GateKey == 0) _carrierLostRoom = t.Room; }
                else { _clear.ResetRoom(t.Room); _entered.Remove(t.Room); _waveTwo.Remove(t.Room); _waveTwoDue.Remove(t.Room); if (_carrierLostRoom == t.Room) _carrierLostRoom = -1; }
                _st.Cleared = _clear.ClearedMask; Publish();
                return;
            }
            if (_layout != null) _drops.Add(new PendingDrop { Hits = e.HitLog, Seed = _layout.Seed, Floor = _layout.Floor, Key = t.DropKey, Tier = e.Tier });
            if (t.Carrier && _st.GateKey == 0) { _st.GateKey = 1; _st.GateKeyPos = SettledKeyPos(e.transform.position); }
            if (t.Guardian) GuardianDefeated?.Invoke(_st.Floor);
            if (_waveTwo.ContainsKey(t.Room) && !RoomHasTracked(t.Room)) { if (!_waveTwoDue.Contains(t.Room)) _waveTwoDue.Add(t.Room); Publish(); return; }   // wave 0 is down: wave 1 follows (TickWaveTwo) before the room can clear
            if (_clear.Died(t.Room)) { _st.Cleared = _clear.ClearedMask; PayRoomClear(t.Room); RoomCleared?.Invoke(t.Room); }
            if (_carrierLostRoom == t.Room && _st.GateKey == 0 && !RoomHasTracked(t.Room))   // its carrier was lost without a drop: reset the room so the carrier comes back on entry
            { _clear.ResetRoom(t.Room); _entered.Remove(t.Room); _carrierLostRoom = -1; _st.Cleared = _clear.ClearedMask; }
            Publish();
        }

        private bool RoomHasTracked(int room) { foreach (Tracked o in _tracked.Values) if (o.Room == room) return true; return false; }

        private void TickGateKey()
        {
            if (_st.GateKey != 1) return;
            foreach (PlayerPos p in _players)
            {
                if (FlatSqr(p.Position, _st.GateKeyPos) > GateKeyRange * GateKeyRange || WallBetween(_root, p.Position, _st.GateKeyPos)) continue;
                _st.GateKey = 2; _st.ExitUnlocked = true;
                Publish();
                ExitUnlocked?.Invoke(_st.Floor);
                return;
            }
        }

        private void TickPieces(CellarFloorSync fs)
        {
            DivePieces pieces = HaulSync != null && HaulSync.Session != null ? HaulSync.Session.Pieces : null;
            ulong mask = 0;
            for (int i = 0; i < _pieceSpots.Length && i < _pieceIds.Count && pieces != null; i++)
            {
                int id = _pieceIds[i];
                if (!pieces.IsFound(id))
                {
                    Vector3 at = World(fs, _pieceSpots[i]);
                    foreach (PlayerPos p in _players)
                        if (FlatSqr(p.Position, at) <= PieceRange * PieceRange) { HaulSync.ServerAddPiece(id); break; }
                }
                if (pieces.IsFound(id)) mask |= 1UL << i;
            }
            _st.Pieces = mask;
        }

        // Each player walking onto their own waiting satchel carries it (a full bag leaves it lying: PlayerBag.PickUpSatchel). Only the walker's own bag is looked at, so no one else's satchel can be taken.
        private void TickSatchels(CellarFloorSync fs)
        {
            if (HaulSync == null || _layout == null || !TrySatchelCell(_layout, _layout.Seed, out CellPos cell)) return;
            Vector3 at = World(fs, cell);
            for (int i = 0; i < _players.Count; i++)
                if (FlatSqr(_players[i].Position, at) <= SatchelRange * SatchelRange) PickUpSatchelOf(_players[i].ClientId);
        }

        private bool PickUpSatchelOf(ulong clientId)
        {
            PlayerBag bag = HaulSync.ServerBagOf(clientId);
            if (bag == null || !bag.Satchel.ShouldPlaceOn(_layout.Floor) || !HaulSync.ServerPickUpSatchel(clientId)) return false;
            SatchelPickedUp?.Invoke(clientId);
            return true;
        }

        /// <summary>Host: `clientId` picks up its own lost satchel if one waits on this floor and they stand within SatchelRange of its spot (no wall check: the spot is a free room cell). False for a stranger
        /// (no satchel of theirs here), out of range, a full bag, or off the host.</summary>
        public bool ServerTryPickUpSatchel(ulong clientId)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || HaulSync == null || !TrySatchelCell(_layout, _layout.Seed, out CellPos cell)) return false;
            if (!TryPlayerPos(clientId, out Vector3 pos) || FlatSqr(pos, World(fs, cell)) > SatchelRange * SatchelRange) return false;
            return PickUpSatchelOf(clientId);
        }

        private void TickAutoDoor(CellarFloorSync fs)
        {
            if (_gate == null || !_st.RoomKeyHeld || _st.DoorOpen) return;
            foreach (PlayerPos p in _players)
                if (FlatSqr(p.Position, _gate.position) <= GateRange * GateRange && !WallBetween(_root, p.Position, _gate.position)) { _st.DoorOpen = true; return; }
        }

        // Host: a client (or a test) asks to open chest `chestIndex`. Refused off the host, for a bad or already open chest, or out of range.
        public bool ServerTryOpenChest(ulong clientId, int chestIndex)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || chestIndex < 0 || chestIndex >= _layout.Chests.Count || chestIndex >= 64) return false;
            FloorChest fc = _layout.Chests[chestIndex];
            PlayerBag bag = null;
            if (fc.HoldsKey)
            {
                if ((_st.Chests & (1UL << chestIndex)) != 0) return false;   // the key chest is one for the whole party
            }
            else
            {
                // A loot chest is the opener's own: refused with no haul sync (no bag to fill), for a client without a bag, and when this player's bag already opened it.
                // HaulSync is the host's DiveHaulSync; a null one refuses every loot chest (production wires it in DiveDirector).
                bag = HaulSync != null ? HaulSync.ServerBagOf(clientId) : null;
                if (bag == null || bag.ChestOpened(_layout.Seed, _layout.Floor, chestIndex)) return false;
            }
            if (!TryPlayerPos(clientId, out Vector3 pos)) return false;
            Transform chest = chestIndex < _chests.Length ? _chests[chestIndex] : null;
            Vector3 at = chest != null ? chest.position : World(fs, new CellPos(_layout.Chests[chestIndex].X, _layout.Chests[chestIndex].Y));
            if (FlatSqr(pos, at) > ChestRange * ChestRange || WallBetween(_root, pos, at)) return false;
            int item;
            if (fc.HoldsKey)
            {
                item = CellarLoot.Roll(_layout.Seed, _layout.Floor, chestIndex, true);   // CellarLoot.RoomKeyId
                _st.Chests |= 1UL << chestIndex;
                _st.RoomKeyHeld = true;
            }
            else
            {
                int bonus = HaulSync.Session != null ? HaulSync.Session.Momentum.ChestRankBonus(_layout.Floor) : 0;   // momentum: a second-half chest rolls one rank higher
                bonus = _mods.ChestRankBonus(bonus);   // Dim: one more rank on every chest roll of the floor
                item = CellarLoot.RollChest(_layout.Seed, _layout.Floor, chestIndex, HaulSync.PlayerSlotOf(clientId), bonus, bag.OwnsBoots);   // may be GoldId
                if (item != CellarLoot.GoldId && bag.IsFull)   // a full bag refuses the chest (decision 6): it stays closed for this player, who alone is told
                {
                    NoticeOwner(clientId, BagFullText);
                    ChestRefusedFull?.Invoke(clientId, chestIndex);
                    return false;
                }
                bag.MarkChestOpened(_layout.Seed, _layout.Floor, chestIndex);
                if (item == CellarLoot.GoldId) HaulSync.ServerAddGold(clientId, CellarLoot.GoldPurse);   // a purse never reaches PlayerBag.Add (it rejects ids <= 0)
                else HaulSync.ServerAddItem(clientId, item);
                for (int k = 1; k <= _mods.ExtraChestRolls; k++) GiveExtraRoll(clientId, bag, chestIndex, k, bonus);   // Stirred: the printed reward
            }
            LastLootItem = item;
            Publish();
            ChestOpened?.Invoke(chestIndex, item);
            return true;
        }

        // Stirred's extra roll (plan 5 task 5): an extra CellarLoot.RollChest on a derived chest index, added under the normal bag-full rule. A refused roll is simply lost (the opener is told).
        public int LastExtraLootItem { get; private set; }   // host only (tests)
        private void GiveExtraRoll(ulong clientId, PlayerBag bag, int chestIndex, int k, int rankBonus)
        {
            int extra = CellarLoot.RollChest(_layout.Seed, _layout.Floor, chestIndex + 1000 * k, HaulSync.PlayerSlotOf(clientId), rankBonus, bag.OwnsBoots);
            LastExtraLootItem = extra;
            if (extra == CellarLoot.GoldId) HaulSync.ServerAddGold(clientId, CellarLoot.GoldPurse);
            else if (bag.IsFull) NoticeOwner(clientId, BagFullText);
            else HaulSync.ServerAddItem(clientId, extra);
        }

        // Crowded: gold for every present player each time a room becomes cleared; Ambush: more when the ambush room does. Per player through the ordinary ServerAddGold path.
        public int GoldPaidForClears { get; private set; }   // host only (tests): the total gold handed out by the Crowded / Ambush rewards
        private void PayRoomClear(int room)
        {
            if (room < 0 || room >= 64) return;
            ulong bit = 1UL << room;
            if ((_roomPaid & bit) != 0) return;   // paid already: a room that reset and cleared again does not pay twice
            _roomPaid |= bit;
            int each = _mods.GoldPerRoomClear;
            if (_mods.AmbushRoomGold > 0)
            {
                CellarEncounterPlan plan = CurrentPlan(FloorSync);
                if (plan.Twist == FloorTwist.Ambush && room == plan.AmbushRoom) each += _mods.AmbushRoomGold;
            }
            PayEveryone(each);
        }

        // "Present" = every connected client with a hero in the dive, downed ones included (they are in the party); HaulSync.ServerAddGold skips anyone without a dive hero.
        private void PayEveryone(int each)
        {
            if (each <= 0 || HaulSync == null) return;
            foreach (ulong id in NetworkManager.ConnectedClientsIds) if (HaulSync.ServerAddGold(id, each)) GoldPaidForClears += each;
        }

        public bool ServerTryOpenDoor(ulong clientId)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || _gate == null || !_st.RoomKeyHeld || _st.DoorOpen) return false;
            if (!TryPlayerPos(clientId, out Vector3 pos) || FlatSqr(pos, _gate.position) > GateRange * GateRange || WallBetween(_root, pos, _gate.position)) return false;
            _st.DoorOpen = true; Publish(); return true;
        }

        // ---- floor objectives (host) ----
        private void RaiseExitUnlocked()
        {
            _st.ExitUnlocked = true;
            Publish();
            if (_unlockRaised) return;
            _unlockRaised = true;
            ExitUnlocked?.Invoke(_st.Floor);
        }

        private bool ValveAt(int valve, out Vector3 at)
        {
            at = default;
            if (_plan == null || _plan.Objective != FloorObjective.TwoValves || valve < 0 || valve >= ValveHold.Count) return false;
            at = World(null, valve == 0 ? _plan.ValveCellA : _plan.ValveCellB);
            return true;
        }

        // Host: the owner of a hero reports "holding at valve `valve`" (every ReportInterval while the button is held). Refused off the host, on a floor that is not a valve floor, for a turned or bad valve, for a
        // hero that is down, not yet on the floor or farther than ValveReachMeters + ValveReportReachSlack. A fresh report keeps the valve's hold running (ValveHold ends it when reports stop).
        public bool ServerReportValve(ulong clientId, int valve)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || _st.Objective != (byte)FloorObjective.TwoValves || (_st.ValveMask & (1 << valve)) != 0 || !ValveAt(valve, out Vector3 at)) return false;
            if (!TryPlayerPos(clientId, out Vector3 pos)) return false;
            float reach = FloorObjectiveTuning.ValveReachMeters + ValveReportReachSlack;
            if (FlatSqr(pos, at) > reach * reach) return false;
            _valves.Report(valve, Time.time);
            return true;
        }

        // Host: one press within PumpStartReach of the pump starts it (a hero that is up; the pump then runs by itself while someone stands in the ring). False if already started.
        public bool ServerTryStartPump(ulong clientId)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || _st.Objective != (byte)FloorObjective.HoldPump || _plan == null || _pump.Started) return false;
            if (!TryPlayerPos(clientId, out Vector3 pos)) return false;
            if (FlatSqr(pos, World(fs, _plan.PumpCell)) > FloorObjectiveTuning.PumpStartReach * FloorObjectiveTuning.PumpStartReach) return false;
            _pump.Start();
            _st.PumpStarted = true; Publish();
            return true;
        }

        private void TickObjective(CellarFloorSync fs)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.25f);
            if (_st.Objective == (byte)FloorObjective.TwoValves && !_st.ExitUnlocked)
            {
                if (_valves.Tick(Time.time, dt)) { _st.ValveMask = (byte)_valves.TurnedMask; if (_valves.AllTurned) RaiseExitUnlocked(); else Publish(); }
            }
            else if (_st.Objective == (byte)FloorObjective.HoldPump && _pump.Started) TickPump(dt);
        }

        private int PresentPlayers => Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
        private int WaveLiving(int w) => _waveEnemies[w].Count + _waveToSpawn[w];

        private void TickPump(float dt)
        {
            if (_pump.Done) return;
            Vector3 pumpAt = World(null, _plan.PumpCell);
            bool hero = false;
            float ring2 = FloorObjectiveTuning.PumpRingMeters * FloorObjectiveTuning.PumpRingMeters;
            foreach (PlayerPos p in _players) if (FlatSqr(p.Position, pumpAt) <= ring2) { hero = true; break; }
            bool enemyAtPump = false;
            float slow2 = FloorObjectiveTuning.PumpSlowRadius * FloorObjectiveTuning.PumpSlowRadius;
            for (int w = 0; w < _waveEnemies.Length && !enemyAtPump; w++)
                foreach (Enemy e in _waveEnemies[w]) if (e != null && e.Alive && FlatSqr(e.transform.position, pumpAt) <= slow2) { enemyAtPump = true; break; }
            float scaled = PumpSecondsOverrideForTests > 0f ? dt * (FloorObjectiveTuning.PumpSeconds / PumpSecondsOverrideForTests) : dt;
            PumpTick t = _pump.Tick(scaled, hero, enemyAtPump, WaveLiving(0), WaveLiving(1), WaveLiving(2));
            if (t.StartWave >= 0) BeginWave(t.StartWave);
            if (t.JustDone) { for (int w = 0; w < _waveToSpawn.Length; w++) _waveToSpawn[w] = 0; }   // the pump is done: no more enemies appear (the living ones stay)
            else SpawnWaveMembers();
            byte pct = t.JustDone || _pump.Done ? (byte)100 : (byte)Mathf.Min(99, Mathf.FloorToInt(_pump.Progress * 100f + 0.0001f));
            if (pct != _st.PumpPercent) { _st.PumpPercent = pct; Publish(); }
            if (t.JustDone) RaiseExitUnlocked();
        }

        // A wave starts: its size is trimmed so the floor stays within GameMetrics.MaxLiveEnemies (counting what other waves still have to spawn); members then appear one at a time.
        private void BeginWave(int w)
        {
            int live = _combat != null ? _combat.AliveEnemies.Count : 0;
            for (int o = 0; o < _waveToSpawn.Length; o++) if (o != w) live += _waveToSpawn[o];
            _waveToSpawn[w] = PumpHold.WaveSize(PresentPlayers, live);
            _waveNextAt[w] = Time.time;
            WavesStarted++;
        }

        public CombatHost CombatForTests => IsServer ? EnsureCombat() : null;   // host only (tests): the floor's combat host, created on demand (a test spawns inert fodder with it to fill the live cap)
        public int WavesStarted { get; private set; }      // host only (tests): pump waves begun on this floor
        public int PumpEnemiesSpawned { get; private set; } // host only (tests)
        public int PumpWaveLiving(int wave) => wave >= 0 && wave < _waveEnemies.Length ? _waveEnemies[wave].Count : 0;   // host only (tests)

        private void SpawnWaveMembers()
        {
            for (int w = 0; w < _waveToSpawn.Length; w++)
            {
                if (_waveToSpawn[w] <= 0 || Time.time < _waveNextAt[w]) continue;
                CombatHost combat = EnsureCombat();
                if (combat.AliveEnemies.Count >= GameMetrics.MaxLiveEnemies) continue;   // the cap holds the spawn back: it comes when the floor has room again
                Enemy e = SpawnPumpEnemy(combat);
                _waveToSpawn[w]--;
                _waveNextAt[w] = Time.time + PumpHold.Stagger(PresentPlayers);
                if (e == null) continue;
                _waveEnemies[w].Add(e); PumpEnemiesSpawned++;
                e.ServerDied += OnWaveEnemyGone; e.ServerDespawned += OnWaveEnemyGone;
            }
        }

        // One Tier-1 roster enemy at a CellarAmbush ring cell of the pump room (a room with no plan spawns uses its free cells), skipping the gate-key carrier's cell.
        private Enemy SpawnPumpEnemy(CombatHost combat)
        {
            CellarFloorSync fs = FloorSync;
            CellarEncounterPlan plan = CurrentPlan(fs);
            int room = _plan.PumpRoom;
            IReadOnlyList<CellPos> cells = CellarAmbush.Cells(_layout, _dressing, plan, room, PlanePoints());
            if (cells.Count == 0) cells = CellarEncounterPlan.FreeCellsOf(_layout, _dressing, room);
            if (cells.Count == 0) return null;
            CellPos cell = cells[_pumpSpawnIndex % cells.Count];
            for (int tries = 0; tries < cells.Count && _layout.Carrier != null && cell.X == _layout.Carrier.X && cell.Y == _layout.Carrier.Y; tries++) cell = cells[(_pumpSpawnIndex + tries + 1) % cells.Count];
            EnemyKind kind = CellarEnemyMix.Tier1Kind(_layout.Seed, _layout.Floor, room, 100 + _pumpSpawnIndex, plan.RosterMask);
            _pumpSpawnIndex++;
            return combat.SpawnEnemy(kind, EnemyCatalog.AuthoredTier(false, false, _layout.Floor), World(fs, cell), room: room);
        }

        private void OnWaveEnemyGone(Enemy e)
        {
            Unhook(e);
            foreach (List<Enemy> l in _waveEnemies) l.Remove(e);
        }

        private void ResetPump()
        {
            foreach (List<Enemy> l in _waveEnemies) { foreach (Enemy e in l) Unhook(e); l.Clear(); }
            for (int w = 0; w < _waveToSpawn.Length; w++) { _waveToSpawn[w] = 0; _waveNextAt[w] = 0f; }
            _pump = new PumpHold(); _pumpSpawnIndex = 0; WavesStarted = 0; PumpEnemiesSpawned = 0;
        }

        // Test/world hooks: world position of a chest / the gate / a piece / the gate key on this peer's built floor.
        public bool TryChestPosition(int i, out Vector3 pos) { pos = default; if (i < 0 || i >= _chests.Length || _chests[i] == null) return false; pos = _chests[i].position; return true; }
        public bool TryGatePosition(out Vector3 pos) { pos = default; if (_gate == null) return false; pos = _gate.position; return true; }
        public bool GateBlocking => _gate != null && _gate.gameObject.activeSelf;
        public bool ChestShownOpen(int i) => i >= 0 && i < _chests.Length && _chests[i] != null && _chests[i].localScale.y < 0.9f;
        public int PieceCount => _pieceSpots.Length;
        public bool PieceVisible(int i) => i >= 0 && i < _pieces.Length && _pieces[i] != null && _pieces[i].activeSelf;
        public bool TryPiecePosition(int i, out Vector3 pos)
        {
            pos = default;
            if (i < 0 || i >= _pieces.Length || _pieces[i] == null) return false;
            pos = _pieces[i].transform.position; return true;
        }
        public bool GateKeyMarkerShown => _gateKeyMarker != null;
        public bool TryValvePosition(int i, out Vector3 pos) { pos = default; if (i < 0 || i >= _valveMarkers.Length || _valveMarkers[i] == null) return false; pos = _valveMarkers[i].transform.position; return true; }
        public bool TryPumpPosition(out Vector3 pos) { pos = default; if (_pumpMarker == null) return false; pos = _pumpMarker.transform.position; return true; }
        public bool ValveShownTurned(int i) => i >= 0 && i < _valveMarkers.Length && CellarObjectiveMarkers.ValveShownTurned(_valveMarkers[i]);
        public Color PumpMarkerColor => CellarObjectiveMarkers.PumpColorOf(_pumpMarker);
        public bool PumpMarkerShown => _pumpMarker != null;
        public int EnterEnemyCount(int room) => _clear.Spawned(room);   // host only
        public int AliveInRoom(int room) => _clear.Alive(room);         // host only
    }
}
