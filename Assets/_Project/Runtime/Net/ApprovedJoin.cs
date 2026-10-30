using SleepingCalamity.Core;

namespace SleepingCalamity.Runtime
{
    // Authority: host. One approved join waiting for its client to connect (then HostRoster takes it).
    public readonly struct ApprovedJoin
    {
        public readonly ulong ClientId;
        public readonly PlayerKey Key;
        public readonly string Name;
        public readonly byte Slot;
        public readonly bool Reclaimed;

        public ApprovedJoin(ulong clientId, PlayerKey key, string name, byte slot, bool reclaimed)
        {
            ClientId = clientId;
            Key = key;
            Name = name;
            Slot = slot;
            Reclaimed = reclaimed;
        }
    }
}
