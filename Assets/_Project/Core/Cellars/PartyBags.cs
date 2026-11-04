// Core/Cellars/PartyBags.cs: the host's PlayerBag per recorded player, keyed by player id (spec decision 39; per-player loot plan task 1). Pure C#.
// Authority: host only (owned by DiveState). The id is the PlayerKey string, or the client id when there is no roster, so a rejoin gets the same bag back.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class PartyBags
    {
        readonly SortedDictionary<string, PlayerBag> _bags = new SortedDictionary<string, PlayerBag>(StringComparer.Ordinal);

        public PlayerBag For(string playerId)
        {
            if (!_bags.TryGetValue(playerId, out PlayerBag bag)) _bags[playerId] = bag = new PlayerBag();
            return bag;
        }

        public bool TryGet(string playerId, out PlayerBag bag) => _bags.TryGetValue(playerId, out bag);

        public IEnumerable<KeyValuePair<string, PlayerBag>> All => _bags;   // ordinal order by id

        public bool AnyCarried
        {
            get { foreach (var kv in _bags) if (kv.Value.Carried.Count > 0) return true; return false; }
        }

        public bool AnyBankOpen
        {
            get { foreach (var kv in _bags) if (kv.Value.BankOpen) return true; return false; }
        }

        public void CancelBankVisits() { foreach (var kv in _bags) kv.Value.CancelBank(); }

        public void PushOnAll() { foreach (var kv in _bags) if (kv.Value.BankOpen) kv.Value.PushOn(); }

        public void GoHomeAll() { foreach (var kv in _bags) kv.Value.GoHome(); }

        public void FailAll(int floor) { foreach (var kv in _bags) kv.Value.Fail(floor); }

        public void Adopt(string playerId, PlayerBag bag) { _bags[playerId] = bag; }
    }
}
