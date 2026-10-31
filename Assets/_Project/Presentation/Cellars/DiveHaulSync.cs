// Authority: host. The host owns the one DiveState (shared party haul, checkpoint flag, cleared floors, current floor; Core/Cellars/DiveState) and writes a
// compact summary to ONE host-written NetworkVariable, so every peer (and a late joiner, with the spawn) can display it without an RPC replay. Clients never
// write: ServerAddPiece / ServerAddLoot / ServerBank / ServerFailDive / ServerSetFloor / ServerMarkCleared / ServerBegin all refuse (return false) off the host.
// A client banks by asking: RequestBankRpc (SendTo.Server) -> ServerTryBankFor(sender), which the host validates before banking:
//   1. the dive's current floor has a bank point (floor 2 checkpoint or floor 4 heart; DiveState.FloorHasBank);
//   2. when the sender has a player object and the built floor's bank cell is known, the player is within BankRadius metres (XZ) of the bank cell centre
//      (cell origin math from CellarFloorBuilder.CellCenter, via CellarFloorSync). Known gap: with no player object or no built floor on the host the
//      position check is skipped (floor-has-bank only); no anti-cheat by design, so a modded client standing elsewhere is not a concern.
// No auto-bank on walk-in. Healing at bank points (P47) is not here: no host HP API is wired to it yet.
// Host disconnect: NGO shuts the session down and this object despawns (no host migration in Phase 1).
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;

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
            public int Left(int floor) => floor == 1 ? LeftFloor1 : floor == 2 ? LeftFloor2 : floor == 3 ? LeftFloor3 : 0;
            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref UnbankedPieces); s.SerializeValue(ref BankedPieces); s.SerializeValue(ref UnbankedLoot);
                s.SerializeValue(ref LeftFloor1); s.SerializeValue(ref LeftFloor2); s.SerializeValue(ref LeftFloor3);
                s.SerializeValue(ref ClearedMask); s.SerializeValue(ref CurrentFloor); s.SerializeValue(ref CheckpointReached);
            }
            public bool Equals(Summary o) => UnbankedPieces == o.UnbankedPieces && BankedPieces == o.BankedPieces && UnbankedLoot == o.UnbankedLoot
                && LeftFloor1 == o.LeftFloor1 && LeftFloor2 == o.LeftFloor2 && LeftFloor3 == o.LeftFloor3 && ClearedMask == o.ClearedMask
                && CurrentFloor == o.CurrentFloor && CheckpointReached == o.CheckpointReached;
            public override bool Equals(object obj) => obj is Summary o && Equals(o);
            public override int GetHashCode() => unchecked(((UnbankedPieces * 31 + BankedPieces) * 31 + UnbankedLoot) * 31 + ClearedMask * 7 + CurrentFloor);
        }

        public readonly NetworkVariable<Summary> Net = new NetworkVariable<Summary>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private DiveState _state;   // host only

        public CellarFloorSync FloorSync;   // optional; found in the scene when null. Used only for the bank position check.

        public Summary Current => Net.Value;
        // Host only; null on clients.
        public DiveState HostState => _state;

        public override void OnNetworkSpawn()
        {
            if (IsServer) { _state = new DiveState(new DiveHaul(DefaultPiecesPerFloor)); Publish(); }
        }

        public override void OnNetworkDespawn() { _state = null; }

        private void Publish()
        {
            DiveHaul h = _state.Haul;
            Net.Value = new Summary
            {
                UnbankedPieces = h.UnbankedPieceCount, BankedPieces = h.BankedPieceCount, UnbankedLoot = h.UnbankedLootCount,
                LeftFloor1 = h.PiecesLeft(1), LeftFloor2 = h.PiecesLeft(2), LeftFloor3 = h.PiecesLeft(3),
                ClearedMask = _state.ClearedMask, CurrentFloor = _state.CurrentFloor, CheckpointReached = _state.CheckpointReached,
            };
        }

        private bool Host => IsServer && IsSpawned && _state != null;

        // ---- host-only mutations (all return false on a client or on bad input) ----
        // New dive state with the given pieces per floor 1-3 (clears checkpoint, cleared floors and the haul).
        public bool ServerBegin(int[] piecesPerFloor)
        {
            if (!IsServer || !IsSpawned || piecesPerFloor == null) return false;
            _state = new DiveState(new DiveHaul(piecesPerFloor)); Publish(); return true;
        }
        public bool ServerAddPiece(int pieceId) { if (!Host || !_state.Haul.AddPiece(pieceId)) return false; Publish(); return true; }
        public bool ServerAddLoot(int itemId, int count) { if (!Host || !_state.Haul.AddLoot(itemId, count)) return false; Publish(); return true; }
        public bool ServerSetFloor(int floor) { if (!Host || !_state.SetFloor(floor)) return false; Publish(); return true; }
        public bool ServerMarkCleared(int floor) { if (!Host || !_state.MarkCleared(floor)) return false; Publish(); return true; }
        public bool ServerFailDive() { if (!Host) return false; _state.FailDive(); Publish(); return true; }
        // Banks at the current floor's bank point with no position check (host logic, e.g. a scripted bank).
        public bool ServerBank() { if (!Host || !_state.BankAtPoint()) return false; Publish(); return true; }
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
            if (FloorSync == null) FloorSync = FindFirstObjectByType<CellarFloorSync>();
            CellarFloorSync fs = FloorSync;
            if (fs == null || fs.Layout == null || fs.Built == null || fs.Built.Root == null || !fs.Layout.BankCell.HasValue) return false;
            if (fs.BuiltFloor != _state.CurrentFloor) return false;   // the built floor lags or leads the dive floor: no cell to trust, skip the range check
            CellPos cell = fs.Layout.BankCell.Value;
            pos = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y));
            return true;
        }
    }
}
