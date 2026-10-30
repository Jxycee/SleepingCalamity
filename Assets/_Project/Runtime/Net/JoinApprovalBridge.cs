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

        // Raised when a join is approved, before its client connects (main thread). The online session caches the player's service id here, so
        // a client that drops during scene sync can still be removed from the Lobby.
        public event System.Action<ApprovedJoin> Approved;

        // Replaces any pending entry for the same ClientId.
        public void AddApproved(in ApprovedJoin join)
        {
            _pending[join.ClientId] = join;
            EventIsolation.Invoke(Approved, join);
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

        // Only ConnectionApprover removes entries (on the client's disconnect event); a session that removed one first would hide an
        // approved-but-never-connected client from the approver, which then could not free its place.
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
