using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.Runtime
{
    // Authority: host only (a plain C# object, never replicated). The host's list of player slots for the current session: who is connected
    // now and which slots belong to players who left (kept so a rejoin reclaims its slot, JoinRules.ChooseSlot). ConnectionApprover reads it
    // through IJoinContextProvider; the approver's NGO connection events keep it current (Connect on ClientConnected, Disconnect on
    // ClientDisconnected). Main thread only. Cleared when the approver is uninstalled (the session ended).
    public sealed class HostRoster : IJoinContextProvider
    {
        public const int MaxSlots = NetworkConstants.MaxPlayers;

        private struct Entry
        {
            public PlayerKey Key;
            public byte Slot;
            public ulong ClientId;
            public string Name;
            public bool Connected;
        }

        private readonly List<Entry> _entries = new List<Entry>(MaxSlots);

        public bool JoinsOpen { get; set; } = true;

        public int ConnectedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _entries.Count; i++) if (_entries[i].Connected) n++;
                return n;
            }
        }

        public void GetRoster(List<RosterEntry> into)
        {
            into.Clear();
            for (int i = 0; i < _entries.Count; i++)
                into.Add(new RosterEntry(_entries[i].Key, _entries[i].Slot, _entries[i].Connected));
        }

        public bool TryGetKey(ulong clientId, out PlayerKey key)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Connected && _entries[i].ClientId == clientId)
                {
                    key = _entries[i].Key;
                    return true;
                }
            }
            key = default;
            return false;
        }

        // The slot a connected client holds (spawn positions follow it).
        public bool TryGetSlot(ulong clientId, out byte slot)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Connected && _entries[i].ClientId == clientId)
                {
                    slot = _entries[i].Slot;
                    return true;
                }
            }
            slot = 0;
            return false;
        }

        // The client a key is connected as now (a reconnecting key takes over its own stale connection).
        public bool TryFindConnected(PlayerKey key, out ulong clientId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Connected && _entries[i].Key == key)
                {
                    clientId = _entries[i].ClientId;
                    return true;
                }
            }
            clientId = 0;
            return false;
        }

        // The keys of the players connected now, in slot order.
        public void GetConnectedKeys(List<PlayerKey> into)
        {
            into.Clear();
            for (int slot = 0; slot < MaxSlots; slot++)
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Slot == slot && _entries[i].Connected) into.Add(_entries[i].Key);
        }

        // The display names of the players connected now, in slot order (the same order as GetConnectedKeys). A blank name reads as "Player N".
        public void GetConnectedNames(List<string> into)
        {
            into.Clear();
            for (int slot = 0; slot < MaxSlots; slot++)
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Slot == slot && _entries[i].Connected)
                        into.Add(string.IsNullOrWhiteSpace(_entries[i].Name) ? "Player " + (slot + 1) : _entries[i].Name);
        }

        // An approved join whose client has connected. The slot is the verdict's: a reclaimed or taken-over slot replaces its old entry.
        public void Connect(in ApprovedJoin join)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Slot == join.Slot || _entries[i].ClientId == join.ClientId) _entries.RemoveAt(i);
            _entries.Add(new Entry { Key = join.Key, Slot = join.Slot, ClientId = join.ClientId, Name = join.Name, Connected = true });
        }

        // The slot stays, marked disconnected, so the same key can reclaim it.
        public void Disconnect(ulong clientId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].ClientId != clientId || !_entries[i].Connected) continue;
                Entry e = _entries[i];
                e.Connected = false;
                _entries[i] = e;
            }
        }

        public void Clear()
        {
            _entries.Clear();
            JoinsOpen = true;
        }
    }
}
