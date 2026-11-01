// Core/Town/TownStock.cs: the town's host-held stock for M6 S3 (memory only; the save sections are S2). Wraps the TownState (pieces and residents in stock, buildings placed)
// and adds the loot the dive banked. Receive() moves what the party banked into the town: banked pieces into TownState stock, the baker when the heart was banked, and the banked
// loot as a delta against what was already moved (DiveHaul keeps running banked totals for the whole session). Idempotent: running it twice moves nothing twice, so the haul
// (which keeps banked pieces "collected" so the floors do not respawn them) and the town never double count.
// Authority: host only (the instance lives in the host's DiveDirector). Pure C#, no engine references.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class TownStock
    {
        private readonly SortedDictionary<int, int> _loot = new SortedDictionary<int, int>();
        private readonly Dictionary<int, int> _lootMoved = new Dictionary<int, int>();

        public TownStock(TownLayout layout) { State = new TownState(layout); }

        public TownState State { get; }
        public int LootCount(int itemId) => _loot.TryGetValue(itemId, out int c) ? c : 0;
        public IEnumerable<KeyValuePair<int, int>> Loot => _loot;
        public int TotalLoot { get { int t = 0; foreach (int c in _loot.Values) t += c; return t; } }

        // Moves the banked haul (and the rescued baker) into the town. Returns the number of NEW pieces stocked.
        public int Receive(DiveHaul haul, bool bakerRescued)
        {
            int added = 0;
            if (haul != null)
            {
                foreach (int piece in haul.BankedPieces)
                    if (State.AddPiece(piece) == PlaceResult.Ok) added++;
                foreach (KeyValuePair<int, int> item in haul.BankedLootItems)
                {
                    _lootMoved.TryGetValue(item.Key, out int moved);
                    int delta = item.Value - moved;
                    if (delta <= 0) continue;
                    _loot.TryGetValue(item.Key, out int have);
                    _loot[item.Key] = have + delta;
                    _lootMoved[item.Key] = item.Value;
                }
            }
            if (bakerRescued) State.AddResident(TownBuildings.BakerResident);
            return added;
        }
    }
}
