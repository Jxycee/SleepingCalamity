// Authority: host. M5 contents of the built Cellars floor: enemies, chests, keys, the locked door, bakery pieces. The host alone decides everything and writes ONE
// host-written NetworkVariable (State: opened-chest mask, collected-piece mask, cleared-room mask, key/door/exit flags, the dropped gate key) tagged with the (seed, floor) it
// belongs to, so a late joiner receives it with the spawn and a peer ignores a state from another floor. Every peer builds the floor LOCALLY (CellarFloorSync) and this
// object only shows state on it: chests lowered when opened, the gate hidden when the door is open, piece spheres hidden when collected, a gold cube where the gate key lies.
// Clients never write. They ask: RequestOpenChestRpc(chestIndex) / RequestOpenDoorRpc (SendTo.Server); the host re-checks index, not-already-open, key held and the
// requesting player's XZ range (ChestRange / GateRange) from its own copy of the player object, so a client gate is only a prompt. The owner-side Interact prompt (rising
// edge of the local hero's InteractPressed/Held) picks the nearest chest or the gate in range and sends the RPC.
// Host logic (Update, host only): (1) floor change or DiveFailed resets per-floor state and despawns the floor's enemies; (2) a player stepping into a Combat room for the
// first time spawns that room's CellarEncounterPlan spawns (DiveHaulSync.EncounterPlan, extras at that moment included) through CombatHost.SpawnEnemy: Tier1 = floor-banded
// Slime/Broom/RatKing (CellarEnemyMix), Tier2 = RatKing tier 2, the bakery's WardenCell = DoughWarden; (2b) the Ambush floor's ambush room (host-only twist) springs its enemies after FloorTwistTuning.AmbushDelaySeconds at CellarAmbush ring cells, cancelled (room re-armed) if no living player is left when the delay ends; (3) when the plan's Hunter appears one tier-2 RatKing spawns at its cell;
// (4) the enemy spawned for layout.Carrier drops the GATE key where it falls (only on death: a wipe/clear despawn drops nothing, resets the room and it respawns on re-entry): a player within GateKeyRange picks it up and ExitUnlocked is raised; keys live only in this state (GateKey, RoomKeyHeld), never in the haul's loot;
// (5) pieces: a player within PieceRange of an uncollected piece spot adds it to the haul (ServerAddPiece), the collected mask follows the haul every frame so a failed
// dive shows the pieces again; (6) the room key (key chest) lets the host open the locked gate, by Interact or by walking up to it.
// Spine hooks: subscribe to the host event ExitUnlocked(int floor) (fired once per floor when the gate key is picked up; State.ExitUnlocked stays true on every peer until the
// floor changes) to open the stair. Room clearing is kept here (ClearedRooms mask): DiveHaulSync.ServerMarkCleared marks a whole FLOOR cleared and is not called from here.
// Players are ignored for room entry and piece pickup until they are inside the armed floor's bounds and have moved over StaleMoveDistance from where they stood at arm time
// (the owner teleport after a floor change may not have landed yet).
// Known limits: players come from NetworkManager.ConnectedClients player objects (PlayersOverride replaces that in tests); enemies of a room whose stir extras latch after
// entry are not topped up; the greybox CombatHost this creates (when the scene has none) runs with waves off.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;

namespace SleepingCalamity.Presentation
{
    public sealed class CellarContentsSync : NetworkBehaviour
    {
        public const float StaleMoveDistance = 3f;
        public const float ChestRange = 2.5f, GateRange = 3f, GateKeyRange = 1.5f, PieceRange = 1.2f;

        public struct PlayerPos { public ulong ClientId; public Vector3 Position; }

        public struct ContentsState : INetworkSerializable, System.IEquatable<ContentsState>
        {
            public uint Seed; public int Floor;
            public ulong Chests, Pieces, Cleared;
            public int PieceCount;      // pieces assigned to this floor by the haul (host-set at arm time; peers place that many spots)
            public bool RoomKeyHeld, DoorOpen, ExitUnlocked;
            public byte GateKey;        // 0 none yet, 1 lying at GateKeyPos, 2 taken
            public Vector3 GateKeyPos;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref Seed); s.SerializeValue(ref Floor); s.SerializeValue(ref Chests); s.SerializeValue(ref Pieces); s.SerializeValue(ref Cleared); s.SerializeValue(ref PieceCount);
                s.SerializeValue(ref RoomKeyHeld); s.SerializeValue(ref DoorOpen); s.SerializeValue(ref ExitUnlocked); s.SerializeValue(ref GateKey); s.SerializeValue(ref GateKeyPos);
            }
            public bool Equals(ContentsState o) => Seed == o.Seed && Floor == o.Floor && Chests == o.Chests && Pieces == o.Pieces && Cleared == o.Cleared && PieceCount == o.PieceCount
                && RoomKeyHeld == o.RoomKeyHeld && DoorOpen == o.DoorOpen && ExitUnlocked == o.ExitUnlocked && GateKey == o.GateKey && GateKeyPos == o.GateKeyPos;
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
        // Host only: a chest was opened (chest index, item id; RoomKeyId for the key chest).
        public event System.Action<int, int> ChestOpened;
        // Host only: a room was entered / cleared (room index).
        public event System.Action<int> RoomEntered, RoomCleared;

        public ContentsState State => Net.Value;
        public int ChestRequestsHandled { get; private set; }   // host only: RPCs received, accepted or refused
        public int LastLootItem { get; private set; }            // host only
        public Enemy GateKeyCarrierEnemy => _carrierEnemy;       // host only (tests)

        // ---- per-peer built-floor handles ----
        private int _handledBuild = -1;
        private Transform[] _chests = new Transform[0];
        private Transform _gate;
        private CellPos[] _pieceSpots = new CellPos[0];
        private GameObject[] _pieces = new GameObject[0];
        private GameObject _gateKeyMarker;
        private bool _prevInteract;
        private float _nextLookup;

        // ---- host state ----
        private ContentsState _st;
        private readonly CellarRoomClear _clear = new CellarRoomClear();
        private readonly HashSet<int> _entered = new HashSet<int>();
        private sealed class Tracked { public int Room; public bool Carrier; }
        private readonly Dictionary<Enemy, Tracked> _tracked = new Dictionary<Enemy, Tracked>();
        private Enemy _carrierEnemy, _hunter; private bool _hunterSpawned;
        private int _carrierLostRoom = -1;   // the carrier despawned without dying while room-mates lived: its room resets once no tracked enemy is left, so the key stays obtainable
        private CombatHost _combat; private bool _ownsCombat;
        private int _hostBuild = -1; private bool _resetRequested;
        private DiveHaulSync _subscribedHaul;
        private readonly List<PlayerPos> _players = new List<PlayerPos>();
        private readonly Dictionary<ulong, Vector3> _armPos = new Dictionary<ulong, Vector3>();   // host: where each player stood when the floor was armed
        private readonly HashSet<ulong> _live = new HashSet<ulong>();                              // host: players seen inside the floor after moving away from _armPos
        private Bounds _armBounds; private bool _hasBounds;
        private int _handledPieces = -2;

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
            _gateKeyMarker = null; _chests = new Transform[0]; _gate = null; _pieces = new GameObject[0]; _pieceSpots = new CellPos[0]; _handledBuild = -1; _handledPieces = -2;
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
                DestroyPieceMarkers();
                _handledBuild = key; _handledPieces = -2;
                if (fs.Built == null || fs.Layout == null) { _chests = new Transform[0]; _gate = null; return; }
                CellarFloorLayout l = fs.Layout;
                _chests = new Transform[l.Chests.Count];
                for (int i = 0; i < l.Chests.Count; i++) _chests[i] = CellarFloorBuilder.FindChest(fs.Built.Root, l.Chests[i]);
                _gate = CellarFloorBuilder.FindGate(fs.Built.Root);
            }
            if (fs.Built == null || fs.Layout == null) return;
            // the piece count comes from the haul via the host state; wait until the state of THIS floor has arrived
            ContentsState s = IsServer ? _st : Net.Value;
            int want = StateMatches(fs, s) ? s.PieceCount : -1;
            if (want == _handledPieces || want < 0) return;
            DestroyPieceMarkers();
            _handledPieces = want;
            var spots = CellarPieceSpots.Choose(fs.Layout, fs.Built.Dressing, want);
            if (spots.Count < want) Debug.LogWarning("CellarContentsSync: floor " + fs.Layout.Floor + " has room for " + spots.Count + " of " + want + " pieces; the rest cannot be collected.");
            _pieceSpots = new CellPos[spots.Count]; for (int i = 0; i < spots.Count; i++) _pieceSpots[i] = spots[i];
            _pieces = CellarFloorBuilder.BuildPieces(fs.Built.Root, spots);
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
            for (int i = 0; i < _chests.Length; i++)
                if (_chests[i] != null) CellarFloorBuilder.SetChestOpened(_chests[i], ok && (s.Chests & (1UL << i)) != 0);
            if (_gate != null) { bool open = ok && s.DoorOpen; if (_gate.gameObject.activeSelf == open) _gate.gameObject.SetActive(!open); }
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i] != null) { bool show = !(ok && (s.Pieces & (1UL << i)) != 0); if (_pieces[i].activeSelf != show) _pieces[i].SetActive(show); }
            bool keyLies = ok && s.GateKey == 1;
            if (keyLies && _gateKeyMarker == null) _gateKeyMarker = CellarFloorBuilder.BuildGateKey(transform, s.GateKeyPos);
            else if (!keyLies && _gateKeyMarker != null) { Destroy(_gateKeyMarker); _gateKeyMarker = null; }
        }

        // =================================================================================================================================
        // owner side: the Interact prompt
        // =================================================================================================================================

        private void LocalInteract(CellarFloorSync fs)
        {
            NetworkClient me = NetworkManager.LocalClient;
            NetworkObject po = me?.PlayerObject;
            if (po == null) return;
            HeroCombat hc = po.GetComponent<HeroCombat>();
            if (hc == null || hc.Input == null) return;
            var intent = hc.Input.Intent;
            bool now = intent.InteractPressed || intent.InteractHeld;
            bool edge = now && !_prevInteract; _prevInteract = now;
            if (!edge || fs.Built == null) return;
            int best = FindLocalTarget(fs, po.transform.position, out bool gate);
            if (best >= 0) RequestOpenChest(best);
            else if (gate) RequestOpenDoor();
        }

        // The chest (index) or the gate that an Interact press at `p` would use; -1 and gate=false when none is in range.
        private int FindLocalTarget(CellarFloorSync fs, Vector3 p, out bool gate)
        {
            gate = false;
            if (fs == null || fs.Built == null) return -1;
            ContentsState s = Net.Value; bool ok = StateMatches(fs, s);
            int best = -1; float bestD = ChestRange * ChestRange;
            for (int i = 0; i < _chests.Length; i++)
            {
                if (_chests[i] == null || (ok && (s.Chests & (1UL << i)) != 0)) continue;
                float d = FlatSqr(p, _chests[i].position);
                if (d <= bestD) { bestD = d; best = i; }
            }
            if (best >= 0) return best;
            gate = _gate != null && !(ok && s.DoorOpen) && FlatSqr(p, _gate.position) <= GateRange * GateRange;
            return -1;
        }

        /// <summary>Owner side: true when an Interact press at `p` would open a chest or the gate. The dive director then leaves that press to the contents (one press, one action).</summary>
        public bool WouldUseInteract(Vector3 p) { int c = FindLocalTarget(FloorSync, p, out bool gate); return c >= 0 || (gate && Net.Value.RoomKeyHeld); }   // the locked gate takes no press without the key

        public void RequestOpenChest(int chestIndex) { RequestOpenChestRpc(chestIndex); }
        public void RequestOpenDoor() { RequestOpenDoorRpc(); }

        [Rpc(SendTo.Server)]
        private void RequestOpenChestRpc(int chestIndex, RpcParams rpcParams = default) { ChestRequestsHandled++; ServerTryOpenChest(rpcParams.Receive.SenderClientId, chestIndex); }

        [Rpc(SendTo.Server)]
        private void RequestOpenDoorRpc(RpcParams rpcParams = default) { ServerTryOpenDoor(rpcParams.Receive.SenderClientId); }

        // =================================================================================================================================
        // host
        // =================================================================================================================================

        private bool HostReady(CellarFloorSync fs) => IsServer && IsSpawned && _layout != null && _root != null && _st.Floor == _layout.Floor && _st.Seed == _layout.Seed;

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

        private void HostTick(CellarFloorSync fs)
        {
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
            SpawnHunter(fs);
            TickGateKey();
            TickPieces(fs);
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
            _st = new ContentsState { Seed = layout.Seed, Floor = layout.Floor, PieceCount = PiecesForFloor(layout.Floor) };
            _armPos.Clear(); _live.Clear(); _hasBounds = false;
            CellarFloorSync afs = FloorSync;
            if (afs != null && afs.Built != null && afs.Built.Root == floorRoot) { _armBounds = afs.Built.Bounds; _armBounds.Expand(new Vector3(2f, 0f, 2f)); _hasBounds = true; }
            _players.Clear();
            if (PlayersOverride != null) PlayersOverride(_players); else CollectFromNetwork();
            foreach (PlayerPos p in _players) _armPos[p.ClientId] = p.Position;
            if (layout.Floor >= 1 && layout.Floor <= DiveHaul.PieceFloors && !CarrierIsValid(layout, _dressing)) _st.ExitUnlocked = true;
            Publish();
            if (_st.ExitUnlocked) ExitUnlocked?.Invoke(layout.Floor);
            return true;
        }

        // Host entry point 2: forget the floor (dive ended or failed): despawns its enemies and publishes an empty state. Safe to call when nothing is armed.
        public void ServerClearFloor()
        {
            if (!IsServer) return;
            UntrackAll();
            if (_combat != null) _combat.ServerClearEnemies();
            _clear.Reset(); _entered.Clear(); _ambushRoom = -1; _carrierEnemy = null; _carrierLostRoom = -1; _hunter = null; _hunterSpawned = false;
            _layout = null; _root = null; _dressing = null; _armPos.Clear(); _live.Clear(); _hasBounds = false;
            _st = new ContentsState();
            if (IsSpawned) Publish();
        }

        // Pieces the haul assigned to `floor`: still to find plus already collected (banked or not). Falls back to the default counts without a running haul.
        private int PiecesForFloor(int floor)
        {
            if (floor < 1 || floor > DiveHaul.PieceFloors) return 0;
            DiveHaul h = HaulSync != null && HaulSync.HostState != null ? HaulSync.HostState.Haul : null;
            if (h == null) return DiveHaulSync.DefaultPiecesPerFloor[floor - 1];
            int n = h.PiecesLeft(floor);
            foreach (int id in h.UnbankedPieces) if (h.OriginFloor(id) == floor) n++;
            foreach (int id in h.BankedPieces) if (h.OriginFloor(id) == floor) n++;
            return n;
        }

        // The carrier is usable when its room exists, it stands in that room and an authored spawn of a Combat room sits on its cell.
        private static bool CarrierIsValid(CellarFloorLayout l, CellarFloorDressing dressing)
        {
            GateKeyCarrier c = l.Carrier;
            if (c == null || c.RoomIndex < 0 || c.RoomIndex >= l.Rooms.Count || l.RoomAt(c.X, c.Y) != c.RoomIndex) return false;
            foreach (EncounterSpawn s in CellarEncounterPlan.Build(l, dressing, 0, false).Spawns)
                if (s.RoomIndex == c.RoomIndex && s.Kind == EncounterKind.Authored && s.X == c.X && s.Y == c.Y) return true;
            return false;
        }

        // Host: may the stair of `floor` be used? True on floor 4 and beyond (no stair gate), on any floor 1-3 whose carrier is missing/invalid, and once the gate key was picked.
        // False for a floor 1-3 that is not the armed one. The spine's wiring: ExitUnlocked += f => DiveDirector.Instance.ServerOpenStair(f).
        public bool IsExitUnlocked(int floor)
        {
            if (floor < 1 || floor > DiveHaul.PieceFloors) return true;
            return _layout != null && _layout.Floor == floor && _st.ExitUnlocked;
        }

        public bool AutoArm = true;
        private CellarFloorLayout _layout; private Transform _root; private CellarFloorDressing _dressing;

        private void Publish() { if (!_st.Equals(Net.Value)) Net.Value = _st; }

        private void UntrackAll()
        {
            foreach (Enemy e in new List<Enemy>(_tracked.Keys)) Unhook(e);
            _tracked.Clear();
            if (_hunter != null) Unhook(_hunter);
        }

        private void Unhook(Enemy e) { if (e == null) return; e.ServerDied -= OnEnemyDied; e.ServerDespawned -= OnEnemyDespawned; e.ServerDied -= OnHunterDied; e.ServerDespawned -= OnHunterDespawned; }

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
                ambush = CellarAmbush.Cells(l, _dressing, plan, room);
            }
            foreach (EncounterSpawn sp in plan.Spawns)
            {
                if (sp.RoomIndex != room || sp.Kind == EncounterKind.Hunter) continue;
                slot++;
                bool carrierSpawn = l.Carrier != null && l.Carrier.X == sp.X && l.Carrier.Y == sp.Y && sp.Kind == EncounterKind.Authored;
                if (carrierSpawn && _st.GateKey != 0) { index++; continue; }   // its key already dropped (or was taken): a re-entered room does not bring the carrier back
                bool warden = l.WardenCell.HasValue && l.WardenCell.Value.X == sp.X && l.WardenCell.Value.Y == sp.Y;
                EnemyKind kind = warden ? EnemyKind.DoughWarden : sp.Tier == SpawnTier.Tier2 ? EnemyKind.RatKing : CellarEnemyMix.Tier1Kind(l.Seed, l.Floor, room, index, plan.RosterMask);
                int tier = sp.Tier == SpawnTier.Tier2 ? 2 : 1;
                index++;
                Enemy e = combat.SpawnEnemy(kind, tier, World(fs, ambush != null ? ambush[slot] : sp.Cell), room: room);
                bool carrier = l.Carrier != null && l.Carrier.X == sp.X && l.Carrier.Y == sp.Y && sp.Kind == EncounterKind.Authored;
                if (e == null)
                {
                    if (carrier && !_st.ExitUnlocked) { _st.ExitUnlocked = true; Publish(); ExitUnlocked?.Invoke(_st.Floor); }   // the carrier could not spawn: never soft-lock the stair
                    continue;
                }
                _tracked[e] = new Tracked { Room = room, Carrier = carrier };
                if (carrier) _carrierEnemy = e;
                _clear.Spawn(room);
                e.ServerDied += OnEnemyDied; e.ServerDespawned += OnEnemyDespawned;
            }
            _st.Cleared = _clear.ClearedMask;
        }

        private void SpawnHunter(CellarFloorSync fs)
        {
            if (_hunterSpawned) return;
            CellarEncounterPlan plan = HaulSync != null ? HaulSync.EncounterPlan : null;
            if (plan == null || plan.Hunter == null || plan.Floor != _layout.Floor || plan.Seed != _layout.Seed) return;
            _hunterSpawned = true;
            _hunter = EnsureCombat().SpawnEnemy(EnemyKind.RatKing, 2, World(fs, plan.Hunter.Cell));
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
                else { _clear.ResetRoom(t.Room); _entered.Remove(t.Room); if (_carrierLostRoom == t.Room) _carrierLostRoom = -1; }
                _st.Cleared = _clear.ClearedMask; Publish();
                return;
            }
            if (t.Carrier && _st.GateKey == 0) { _st.GateKey = 1; _st.GateKeyPos = e.transform.position; }
            if (_clear.Died(t.Room)) { _st.Cleared = _clear.ClearedMask; RoomCleared?.Invoke(t.Room); }
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
                if (FlatSqr(p.Position, _st.GateKeyPos) > GateKeyRange * GateKeyRange) continue;
                _st.GateKey = 2; _st.ExitUnlocked = true;
                Publish();
                ExitUnlocked?.Invoke(_st.Floor);
                return;
            }
        }

        private void TickPieces(CellarFloorSync fs)
        {
            DiveHaul haul = HaulSync != null && HaulSync.HostState != null ? HaulSync.HostState.Haul : null;
            int floor = _layout.Floor;
            for (int i = 0; i < _pieceSpots.Length; i++)
            {
                int id = DiveHaul.PieceId(floor, i);
                if (haul == null || haul.IsCollected(id)) continue;
                Vector3 at = World(fs, _pieceSpots[i]);
                foreach (PlayerPos p in _players)
                    if (FlatSqr(p.Position, at) <= PieceRange * PieceRange) { HaulSync.ServerAddPiece(id); break; }
            }
            ulong mask = 0;
            for (int i = 0; i < _pieceSpots.Length && haul != null; i++) if (haul.IsCollected(DiveHaul.PieceId(floor, i))) mask |= 1UL << i;
            _st.Pieces = mask;
        }

        private void TickAutoDoor(CellarFloorSync fs)
        {
            if (_gate == null || !_st.RoomKeyHeld || _st.DoorOpen) return;
            foreach (PlayerPos p in _players)
                if (FlatSqr(p.Position, _gate.position) <= GateRange * GateRange) { _st.DoorOpen = true; return; }
        }

        // Host: a client (or a test) asks to open chest `chestIndex`. Refused off the host, for a bad or already open chest, or out of range.
        public bool ServerTryOpenChest(ulong clientId, int chestIndex)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || chestIndex < 0 || chestIndex >= _layout.Chests.Count || chestIndex >= 64) return false;
            if ((_st.Chests & (1UL << chestIndex)) != 0) return false;
            if (!TryPlayerPos(clientId, out Vector3 pos)) return false;
            Transform chest = chestIndex < _chests.Length ? _chests[chestIndex] : null;
            Vector3 at = chest != null ? chest.position : World(fs, new CellPos(_layout.Chests[chestIndex].X, _layout.Chests[chestIndex].Y));
            if (FlatSqr(pos, at) > ChestRange * ChestRange) return false;
            FloorChest fc = _layout.Chests[chestIndex];
            int item = CellarLoot.Roll(_layout.Seed, _layout.Floor, chestIndex, fc.HoldsKey);
            _st.Chests |= 1UL << chestIndex;
            if (fc.HoldsKey) _st.RoomKeyHeld = true;
            LastLootItem = item;
            if (!fc.HoldsKey && HaulSync != null) HaulSync.ServerAddLoot(item, 1);   // the room key lives in State.RoomKeyHeld, not in the haul
            Publish();
            ChestOpened?.Invoke(chestIndex, item);
            return true;
        }

        public bool ServerTryOpenDoor(ulong clientId)
        {
            CellarFloorSync fs = FloorSync;
            if (!HostReady(fs) || _gate == null || !_st.RoomKeyHeld || _st.DoorOpen) return false;
            if (!TryPlayerPos(clientId, out Vector3 pos) || FlatSqr(pos, _gate.position) > GateRange * GateRange) return false;
            _st.DoorOpen = true; Publish(); return true;
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
        public int EnterEnemyCount(int room) => _clear.Spawned(room);   // host only
        public int AliveInRoom(int room) => _clear.Alive(room);         // host only
    }
}
