using System.Collections.Generic;

namespace SleepingCalamity.Runtime
{
    // Authority: host. App-scope handoff from the approver to the HostRoster, once the approved client connects (both in Networking and Runtime). Main thread only.
    // Pending entries count as connected for the approver's capacity and duplicate checks (D4).
    public sealed class JoinApprovalBridge
    {
        private readonly Dictionary<ulong, ApprovedJoin> _pending = new Dictionary<ulong, ApprovedJoin>();

        // ConnectionApprover sets this to the HostRoster on Install and clears it on Uninstall.
        public IJoinContextProvider Provider { get; set; }

        // Replaces any pending entry for the same ClientId.
        public void AddApproved(in ApprovedJoin join)
        {
            _pending[join.ClientId] = join;
        }

        // Removes the entry when found.
        public bool TryTakeApproved(ulong clientId, out ApprovedJoin join)
        {
            if (_pending.TryGetValue(clientId, out join))
            {
                _pending.Remove(clientId);
                return true;
            }
            join = default;
            return false;
        }

        // Clears `into` first.
        public void GetPending(List<ApprovedJoin> into)
        {
            into.Clear();
            foreach (KeyValuePair<ulong, ApprovedJoin> entry in _pending)
            {
                into.Add(entry.Value);
            }
        }

        public void RemovePending(ulong clientId)
        {
            _pending.Remove(clientId);
        }

        // Pending only. Provider is untouched.
        public void Clear()
        {
            _pending.Clear();
        }
    }
}
