using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.Runtime
{
    // Authority: host. Implemented by HostRoster. Read by ConnectionApprover on the host only.
    public interface IJoinContextProvider
    {
        bool JoinsOpen { get; }
        void GetRoster(List<RosterEntry> into);   // clears `into`, then adds every slot (connected or not)
        bool TryGetKey(ulong clientId, out PlayerKey key);
        bool TryGetSlot(ulong clientId, out byte slot);   // the roster slot of a connected client
    }
}
