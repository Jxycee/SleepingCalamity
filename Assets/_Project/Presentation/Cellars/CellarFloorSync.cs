// Authority: host. The host picks the dive seed (32-bit) and the current floor id and writes them to one host-written NetworkVariable (seed and floor together, so they land atomically), so a late
// joiner receives them with the spawn, no RPC to replay. Nobody sends geometry: every peer, the host included, builds the floor LOCALLY from
// (seed, floor) with CellarFloorGenerator + CellarFloorBuilder whenever the pair differs from what it has built, so the floors are identical by
// construction (CellarFloorLayout.LayoutHash() is the proof a test compares). Clients never write; ServerSetFloor is a host-only call (it refuses
// on a client). Solo runs the same code as a local host. Floor 0 = no floor built.
// Host disconnect: when the host leaves, NGO shuts the session down and the sync object despawns, destroying the floor built under it (no host migration in Phase 1).
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;

namespace SleepingCalamity.Presentation
{
    public sealed class CellarFloorSync : NetworkBehaviour
    {
        public const int NoFloor = 0, MaxFloor = 4;

        // One variable holding both values, so a client never sees a new seed with the old floor (NGO applies variables one at a time).
        public struct DiveFloor : INetworkSerializable, System.IEquatable<DiveFloor>
        {
            public uint Seed; public int Floor;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { serializer.SerializeValue(ref Seed); serializer.SerializeValue(ref Floor); }
            public bool Equals(DiveFloor o) => Seed == o.Seed && Floor == o.Floor;
            public override bool Equals(object obj) => obj is DiveFloor o && Equals(o);
            public override int GetHashCode() => unchecked((int)Seed * 31 + Floor);
        }

        public readonly NetworkVariable<DiveFloor> Net = new NetworkVariable<DiveFloor>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private CellarFloorBuild _built;
        private CellarFloorLayout _layout;
        private uint _builtSeed;
        private int _builtFloor = NoFloor;

        public CellarFloorKit Kit;   // optional; null builds the flat-colour greybox

        public int BuildCount { get; private set; }
        public CellarFloorLayout Layout => _layout;
        public CellarFloorBuild Built => _built;
        public uint BuiltSeed => _builtSeed;
        public int BuiltFloor => _builtFloor;
        // 0 while no floor is built.
        public ulong BuiltLayoutHash => _layout != null ? _layout.LayoutHash() : 0UL;

        // Host only: set the dive seed and the floor together (one variable carries both, so they ride the same tick). Returns false off the host or for a bad floor.
        public bool ServerSetFloor(uint seed, int floor)
        {
            if (!IsServer || floor < NoFloor || floor > MaxFloor) return false;
            Net.Value = new DiveFloor { Seed = seed, Floor = floor };
            Refresh();   // the host builds at once; clients build when the values land
            return true;
        }

        public override void OnNetworkSpawn()
        {
            Net.OnValueChanged += OnValueChanged;
            Refresh();   // a late joiner already holds the values
        }

        public override void OnNetworkDespawn()
        {
            Net.OnValueChanged -= OnValueChanged;
            Clear();
        }

        private void OnValueChanged(DiveFloor _, DiveFloor __) { Refresh(); }

        // Builds only when the (seed, floor) pair differs from the built one, so setting seed and floor in one tick builds once.
        private void Refresh()
        {
            if (!IsSpawned) return;
            uint seed = Net.Value.Seed; int floor = Net.Value.Floor;
            if (floor == _builtFloor && (floor == NoFloor || seed == _builtSeed)) return;
            Clear();
            if (floor < 1 || floor > MaxFloor) return;
            _layout = CellarFloorGenerator.Generate(seed, floor);
            _built = CellarFloorBuilder.Build(_layout, transform, Kit);
            _builtSeed = seed; _builtFloor = floor;
            BuildCount++;
        }

        private void Clear()
        {
            if (_built != null && _built.Root != null) Destroy(_built.Root.gameObject);
            _built = null; _layout = null; _builtFloor = NoFloor; _builtSeed = 0u;
        }
    }
}
