// Core/Town/TownStock.cs: the town's host-held stock for M6 S3 (memory only; the save sections are S2). Wraps the TownState (pieces and residents in stock, buildings placed)
// and adds the loot the dive banked. Receive() moves what the party banked into the town: banked pieces into TownState stock, the baker when the heart was banked, and the banked
// loot as a delta against what was already moved (DiveHaul keeps running banked totals for the whole session). Idempotent: running it twice moves nothing twice, so the haul
// (which keeps banked pieces "collected" so the floors do not respawn them) and the town never double count.
// M6 step 0: it is the host's town source for the world save (Capture / Restore round-trip the stock, town, residents and dive sections through TownSaves). Restore rebuilds the
// TownState, the haul (banked pieces and loot, the pieces already home marked home so the floors never respawn them) and the dive progress from a loaded snapshot; the
// loot already in the town counts as moved, so the haul's running banked total is never added a second time.
// Authority: host only (the instance lives in the host's DiveDirector). Pure C#, no engine references.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class TownStock
    {
        private readonly SortedDictionary<int, int> _loot = new SortedDictionary<int, int>();
        private readonly Dictionary<int, int> _lootMoved = new Dictionary<int, int>();

        private readonly TownLayout _layout;

        public TownStock(TownLayout layout) { _layout = layout; State = new TownState(layout); }

        public TownState State { get; private set; }
        public IReadOnlyDictionary<int, int> LootCounts => _loot;
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

        // The town's sections for the world save, from this stock and the live dive state. bankedResidents: the baker banked at the heart and not yet home. Residents are
        // passed as null ("not known": the host keeps its saved first-meet beats).
        // The saved dive section holds only the banked loot the town has not received yet (banked minus moved): the stock's own counts already include the rest, and the
        // stock can shrink (loot spent), so the haul's running banked total must never be saved or the spent loot would come back.
        public TownSnapshot Capture(DiveState dive, IEnumerable<string> bankedResidents)
        {
            TownSnapshot snap = TownSaves.Capture(State, dive, _loot, bankedResidents, null);
            if (snap.Dive != null && dive != null)
            {
                snap.Dive.BankedLoot.Clear();
                foreach (KeyValuePair<int, int> kv in dive.Haul.BankedLootItems)
                {
                    _lootMoved.TryGetValue(kv.Key, out int moved);
                    if (kv.Value - moved > 0) snap.Dive.BankedLoot.Add(new LootCountSave { ItemId = kv.Key, Count = kv.Value - moved });
                }
            }
            return snap;
        }

        // Rebuilds the stock from loaded (cleaned, conserved) sections and returns the dive state to run with (see BuildDive). A mid-dive quit has already become a failed dive,
        // so what the party had banked comes home now (Receive). Loot: the stock's counts are restored as saved and nothing counts as moved yet, so the saved banked loot
        // (only the part not yet moved) arrives on this Receive, exactly once.
        public DiveState Restore(TownSnapshot snap, IReadOnlyList<int> piecesPerFloor, StirTuning tuning)
        {
            State = TownSaves.ToState(snap, _layout);
            _loot.Clear(); _lootMoved.Clear();
            foreach (KeyValuePair<int, int> kv in TownSaves.StockLoot(snap)) _loot[kv.Key] = kv.Value;
            DiveState dive = BuildDive(snap, piecesPerFloor, tuning);
            bool baker = snap?.Dive != null && snap.Dive.BankedResidents.Contains(TownBuildings.BakerResident);
            Receive(dive.Haul, baker);
            return dive;
        }

        // The dive state for a loaded snapshot: haul with its banked pieces and loot, every piece the town holds marked home (RestoreHaul), cleared floors and checkpoint.
        // Pure: leaves the stock, the town and the loot counts alone, so the host can call it again at the first dive start with the piece counts and stir tuning as they
        // are then (they can change after the director spawns).
        public DiveState BuildDive(TownSnapshot snap, IReadOnlyList<int> piecesPerFloor, StirTuning tuning)
        {
            DiveHaul haul = TownSaves.RestoreHaul(snap?.Dive, piecesPerFloor);
            var dive = new DiveState(haul, tuning);
            if (snap?.Dive != null) dive.RestoreProgress(snap.Dive.ClearedMask, snap.Dive.CheckpointReached);
            return dive;
        }
    }
}
