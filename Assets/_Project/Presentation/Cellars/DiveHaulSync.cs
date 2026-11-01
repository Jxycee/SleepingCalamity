// Authority: host. The host owns the one DiveState (shared party haul, checkpoint flag, cleared floors, current floor; Core/Cellars/DiveState) and writes a
// compact summary to ONE host-written NetworkVariable, so every peer (and a late joiner, with the spawn) can display it without an RPC replay. Clients never
// write: ServerAddPiece / ServerAddLoot / ServerBank / ServerFailDive / ServerSetFloor / ServerMarkCleared / ServerBegin all refuse (return false) off the host.
// A client banks by asking: RequestBankRpc (SendTo.Server) -> ServerTryBankFor(sender), which the host validates before banking:
//   1. the dive's current floor has a bank point (floor 2 checkpoint or floor 4 heart; DiveState.FloorHasBank);
//   2. when the sender has a player object and the built floor's bank cell is known, the player is within BankRadius metres (XZ) of the bank cell centre
//      (cell origin math from CellarFloorBuilder.CellCenter, via CellarFloorSync). Known gap: with no player object or no built floor on the host the
//      position check is skipped (floor-has-bank only); no anti-cheat by design, so a modded client standing elsewhere is not a concern.
// Stir meter (Core/Cellars/StirMeter, owned by the DiveState): host-only. The host's Update feeds it the connected player count, ticks the deep-closes countdown with
// Time.deltaTime and fails the dive at 0; value, extra-enemy count, hunter/deep-closes latches and whole seconds left ride in the same Summary. Clients never write.
// No auto-bank on walk-in.
// Healing (P47, host-authoritative): a successful bank (ServerBank, so the client RPC path and scripted banks alike) fully heals every living connected hero
// (HeroVitals.ServerHeal + HeroCombat.ServerBroadcastHeal, like HealingDrop) and raises Banked. The floor's healing point (Core/Cellars/CellarHealing, cell from the
// built floor's CellarFloorBuild.HealingCell) is walked onto: the host's Update heals every hurt living hero within HealTuning.RadiusMeters of its cell centre in
// one tick, then the point recharges HealTuning.RechargeSeconds on the host clock. Peers see only NetHealSecondsLeft (0 = ready, else whole seconds left, one
// host-written NetworkVariable so late joiners get it with the spawn) and show/hide the orb on their own built floor. Downed or out heroes are not healed
// (PlayerVitals.Heal refuses them; the revive rule stands). A point recharges per floor visit: a new (seed, floor) starts ready.
// Host disconnect: NGO shuts the session down and this object despawns (no host migration in Phase 1).
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;

namespace SleepingCalamity.Presentation
{
    public sealed class DiveHaulSync : NetworkBehaviour
    {
        public const float BankRadius = 5f;
        public static readonly int[] DefaultPiecesPerFloor = { 2, 2, 2 };

        // Everything a peer needs to draw the haul and the pieces-left counters, in one value so a client never sees a half-updated haul.
        public struct Summary : INetworkSerializable, System.IEquatable<Summary>
        {
            public int UnbankedPieces, BankedPieces, UnbankedLoot, LeftFloor1, LeftFloor2, LeftFloor3, ClearedMask, CurrentFloor;
            public bool CheckpointReached;
            // Stir meter (host-computed): value rounded to a whole point 0-100, extra enemies per room (latched), hunter and deep-closes latches, whole seconds left (rounded up, 0 = not running).
            public int StirValue, ExtraEnemiesPerRoom, DeepSecondsLeft;
            public bool HunterLatched, DeepClosesLatched;
            public int Left(int floor) => floor == 1 ? LeftFloor1 : floor == 2 ? LeftFloor2 : floor == 3 ? LeftFloor3 : 0;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref UnbankedPieces); s.SerializeValue(ref BankedPieces); s.SerializeValue(ref UnbankedLoot);
                s.SerializeValue(ref LeftFloor1); s.SerializeValue(ref LeftFloor2); s.SerializeValue(ref LeftFloor3);
                s.SerializeValue(ref ClearedMask); s.SerializeValue(ref CurrentFloor); s.SerializeValue(ref CheckpointReached);
                s.SerializeValue(ref StirValue); s.SerializeValue(ref ExtraEnemiesPerRoom); s.SerializeValue(ref DeepSecondsLeft);
                s.SerializeValue(ref HunterLatched); s.SerializeValue(ref DeepClosesLatched);
            }
            public bool Equals(Summary o) => UnbankedPieces == o.UnbankedPieces && BankedPieces == o.BankedPieces && UnbankedLoot == o.UnbankedLoot
                && LeftFloor1 == o.LeftFloor1 && LeftFloor2 == o.LeftFloor2 && LeftFloor3 == o.LeftFloor3 && ClearedMask == o.ClearedMask
                && CurrentFloor == o.CurrentFloor && CheckpointReached == o.CheckpointReached
                && StirValue == o.StirValue && ExtraEnemiesPerRoom == o.ExtraEnemiesPerRoom && DeepSecondsLeft == o.DeepSecondsLeft
                && HunterLatched == o.HunterLatched && DeepClosesLatched == o.DeepClosesLatched;
            public override bool Equals(object obj) => obj is Summary o && Equals(o);
            public override int GetHashCode() => unchecked(((UnbankedPieces * 31 + BankedPieces) * 31 + UnbankedLoot) * 31 + ClearedMask * 7 + CurrentFloor);
        }

        public readonly NetworkVariable<Summary> Net = new NetworkVariable<Summary>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Whole seconds until the floor's healing point can be used again: 0 = ready (or the floor has none). Host-written.
        public readonly NetworkVariable<int> NetHealSecondsLeft = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private DiveState _state;   // host only
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
            StirMeter stir = _state.Stir;
            if (fs == null || fs.Layout == null || fs.Built == null || fs.BuiltFloor != _state.CurrentFloor)
            {
                if (EncounterPlan != null) { EncounterPlan = null; EncounterChanged?.Invoke(); }
                return;
            }
            int extra = stir.ExtraEnemiesPerRoom; bool hunter = stir.HunterLatched;
            if (EncounterPlan != null && _planSeed == fs.BuiltSeed && _planFloor == fs.BuiltFloor && _planExtra == extra && _planHunter == hunter) return;
            EncounterPlan = CellarEncounterPlan.Build(fs.Layout, fs.Built.Dressing, extra, hunter);
            _planSeed = fs.BuiltSeed; _planFloor = fs.BuiltFloor; _planExtra = extra; _planHunter = hunter;
            EncounterChanged?.Invoke();
        }

        public Summary Current => Net.Value;
        // Host only; null on clients.
        public DiveState HostState => _state;

        public override void OnNetworkSpawn()
        {
            if (IsServer) { _state = new DiveState(new DiveHaul(DefaultPiecesPerFloor)); Publish(); }
        }

        public override void OnNetworkDespawn() { _state = null; EncounterPlan = null; }

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
            _state.Stir.PlayersPresent = Mathf.Max(1, NetworkManager.ConnectedClientsIds.Count);
            bool failed = _state.Tick(Mathf.Min(Time.deltaTime, MaxTickSeconds));
            Publish();
            RefreshEncounterPlan();
            if (failed) DiveFailed?.Invoke();
        }

        private void Publish()
        {
            DiveHaul h = _state.Haul;
            StirMeter stir = _state.Stir;
            stir.Refresh();
            var next = new Summary
            {
                StirValue = Mathf.RoundToInt(stir.Value), ExtraEnemiesPerRoom = stir.ExtraEnemiesPerRoom, DeepSecondsLeft = Mathf.CeilToInt(stir.DeepClosesSecondsLeft),
                HunterLatched = stir.HunterLatched, DeepClosesLatched = stir.DeepClosesLatched,
                UnbankedPieces = h.UnbankedPieceCount, BankedPieces = h.BankedPieceCount, UnbankedLoot = h.UnbankedLootCount,
                LeftFloor1 = h.PiecesLeft(1), LeftFloor2 = h.PiecesLeft(2), LeftFloor3 = h.PiecesLeft(3),
                ClearedMask = _state.ClearedMask, CurrentFloor = _state.CurrentFloor, CheckpointReached = _state.CheckpointReached,
            };
            if (!next.Equals(Net.Value)) Net.Value = next;   // the countdown ticks every frame; only send when something a peer can see changed
        }

        private bool Host => IsServer && IsSpawned && _state != null;

        // ---- host-only mutations (all return false on a client or on bad input) ----
        // New dive state with the given pieces per floor 1-3 (clears checkpoint, cleared floors and the haul).
        public bool ServerBegin(int[] piecesPerFloor)
        {
            return ServerBegin(piecesPerFloor, StirTuning.Default);
        }
        // Same, with explicit stir tuning (tests use a short countdown).
        public bool ServerBegin(int[] piecesPerFloor, StirTuning tuning)
        {
            if (!IsServer || !IsSpawned || piecesPerFloor == null) return false;
            _state = new DiveState(new DiveHaul(piecesPerFloor), tuning); _heal = null; EncounterPlan = null; Publish(); return true;
        }
        public bool ServerAddPiece(int pieceId) { if (!Host || !_state.Haul.AddPiece(pieceId)) return false; Publish(); return true; }
        public bool ServerAddLoot(int itemId, int count) { if (!Host || !_state.Haul.AddLoot(itemId, count)) return false; Publish(); return true; }
        public bool ServerSetFloor(int floor) { if (!Host || !_state.SetFloor(floor)) return false; Publish(); return true; }
        public bool ServerMarkCleared(int floor) { if (!Host || !_state.MarkCleared(floor)) return false; Publish(); return true; }
        public bool ServerFailDive() { if (!Host) return false; _state.FailDive(); Publish(); return true; }
        // Banks at the current floor's bank point with no position check (host logic, e.g. a scripted bank).
        public bool ServerBank()
        {
            if (!Host || !_state.BankAtPoint()) return false;
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

        private bool TryHealWorldPosition(out Vector3 pos)
        {
            pos = default;
            ResolveFloorSync();
            CellarFloorSync fs = FloorSync;
            if (fs == null || fs.Built == null || fs.Built.Root == null || !fs.Built.HealingCell.HasValue || fs.BuiltFloor != _state.CurrentFloor) return false;
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
        // Town banking for the M6 flow and the stir reset.
        public bool ServerBankInTown() { if (!Host || !_state.BankInTown()) return false; Publish(); return true; }

        // ---- client request ----
        // Any player may interact with a bank point; the host validates (see the class comment) and banks the whole shared haul.
        public void RequestBank() { RequestBankRpc(); }

        // Host only: bank requests received over the RPC, accepted or refused (lets a test wait for a refusal to land).
        public int BankRequestsHandled { get; private set; }

        [Rpc(SendTo.Server)]
        private void RequestBankRpc(RpcParams rpcParams = default) { BankRequestsHandled++; ServerTryBankFor(rpcParams.Receive.SenderClientId); }

        public bool ServerTryBankFor(ulong clientId)
        {
            if (!Host || !DiveState.FloorHasBank(_state.CurrentFloor)) return false;
            if (TryBankWorldPosition(out Vector3 bank) && NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient c)
                && c.PlayerObject != null && !InRange(c.PlayerObject.transform.position, bank)) return false;
            return ServerBank();
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
            if (fs.BuiltFloor != _state.CurrentFloor) return false;   // the built floor lags or leads the dive floor: no cell to trust, skip the range check
            CellPos cell = fs.Layout.BankCell.Value;
            pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y));
            return true;
        }
    }
}
