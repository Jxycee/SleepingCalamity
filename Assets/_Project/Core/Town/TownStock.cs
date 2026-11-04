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
        public TownSnapshot Capture(DiveState dive, IEnumerable<string> bankedResidents) => Capture(dive, bankedResidents, null);

        // M6 S5: residents = the host's latched first-meet beats (null: not known, the saved copy is kept).
        public TownSnapshot Capture(DiveState dive, IEnumerable<string> bankedResidents, ResidentsSave residents)
        {
            TownSnapshot snap = TownSaves.Capture(State, dive, _loot, bankedResidents, residents);
            if (snap.Dive != null && dive != null)
            {
                snap.Dive.BankedLoot.Clear();
                foreach (KeyValuePair<int, int> kv in dive.Haul.BankedLootItems)
                {
                    _lootMoved.TryGetValue(kv.Key, out int moved);
                    if (kv.Value - moved > 0) snap.Dive.BankedLoot.Add(new LootCountSave { ItemId = kv.Key, Count = kv.Value - moved });
                }
            }
            // The players' bags (the optional "bags" section): a mid-dive bag is written as the failed copy, the live bags are not changed. The host (HostWorld) keeps only the
            // recorded players' entries; here every bag whose id is a PlayerKey string is captured (a client-id fallback bag is not).
            if (dive != null) snap.Bags = BagsSaveRules.Capture(dive.Bags, BagKeys(dive.Bags), dive.CurrentFloor != DiveState.Town, dive.CurrentFloor);
            return snap;
        }

        // ---- plan 3 task 7: the new dive's world state (DiveSession + the director's persistent bags, checkpoint and pieces) ----
        // A piece or person found in the dive is safe at once (spec 4.4): it goes straight into stock, so "found" is exactly "in stock or placed". False for an id already home.
        public bool ReceiveFound(int id)
        {
            if (PieceCatalog.IsPerson(id)) { string r = PieceCatalog.ResidentOf(id); return r != null && State.AddResident(r) == PlaceResult.Ok; }
            return State.AddPiece(id) == PlaceResult.Ok;
        }

        // True when the town holds the piece or person (stock or placed).
        public bool Holds(int id)
        {
            if (PieceCatalog.IsPerson(id))
            {
                string r = PieceCatalog.ResidentOf(id);
                if (r == null) return false;
                if (State.ResidentInStock(r)) return true;
                foreach (PlacedBuilding b in State.Placed) if (b.Resident == r) return true;
                return false;
            }
            if (State.InStock(id)) return true;
            foreach (PlacedBuilding b in State.Placed) foreach (int p in b.PiecesByPart) if (p == id) return true;
            return false;
        }

        // The sections for the world save from the live session. The session's bags, checkpoint and pieces are the director's persistent ones. Found pieces are already in stock (ReceiveFound), so the
        // dive section's on-floor lists come out right without the old haul. depths: the world's piece depths (written into the dive section so a later save keeps them); extraSlotOwner: the saved
        // previous owner of the 3-player extra slot (a bag id or null). The dive section is marked V2Known so the saved CheckpointHeld, BossBeaten and ExtraSlotOwner never carry over a live value.
        public TownSnapshot CaptureWorld(DiveSession session, bool bossBeaten, string extraSlotOwner, IReadOnlyDictionary<int, int> depths, ResidentsSave residents)
        {
            TownSnapshot snap = TownSaves.Capture(State, null, _loot, null, residents);
            bool inDive = session != null && (session.Phase == DivePhase.Floor || session.Phase == DivePhase.BankRoom);
            if (snap.Dive != null)
            {
                snap.Dive.InDive = inDive;
                snap.Dive.ClearedMask = inDive ? session.Snapshot().ClearedMask : 0;   // fresh floors every dive: a mask only matters while the party is below
                snap.Dive.CheckpointHeld = session != null && session.Checkpoint.Held;
                snap.Dive.BossBeaten = bossBeaten;
                snap.Dive.ExtraSlotOwner = extraSlotOwner;
                snap.Dive.V2Known = true;
                if (depths != null && depths.Count > 0)
                {
                    snap.Dive.PieceDepths = new List<PieceDepthSave>();
                    foreach (KeyValuePair<int, int> kv in depths) snap.Dive.PieceDepths.Add(new PieceDepthSave { Id = kv.Key, Floor = kv.Value });
                }
            }
            if (session != null) snap.Bags = BagsSaveRules.Capture(session.Bags, BagKeys(session.Bags), inDive, session.Floor);
            return snap;
        }

        public sealed class WorldRestore
        {
            public PartyBags Bags = new PartyBags();
            public DiveCheckpoint Checkpoint = new DiveCheckpoint();
            public DivePieces Pieces;
            public bool BossBeaten;
            public string ExtraSlotOwner;
        }

        // Rebuilds the stock and the town from loaded (cleaned, conserved) sections and returns what the director runs with: each recorded player's gold, stash and waiting satchel, the checkpoint,
        // and the pieces with every one the town holds marked found. An old save's banked haul comes home here (Receive), exactly as Restore did. depths: the world's piece depths.
        public WorldRestore RestoreWorld(TownSnapshot snap, IReadOnlyDictionary<int, int> depths)
        {
            State = TownSaves.ToState(snap, _layout);
            _loot.Clear(); _lootMoved.Clear();
            foreach (KeyValuePair<int, int> kv in TownSaves.StockLoot(snap)) _loot[kv.Key] = kv.Value;
            DiveHaul haul = TownSaves.RestoreHaul(snap?.Dive, new[] { 2, 2, 2 });
            bool baker = snap?.Dive != null && snap.Dive.BankedResidents.Contains(TownBuildings.BakerResident);
            Receive(haul, baker);
            var w = new WorldRestore { Pieces = new DivePieces(depths) };
            BagsSaveRules.Restore(snap?.Bags, w.Bags);
            if (snap?.Dive != null) { w.Checkpoint.Restore(snap.Dive.CheckpointHeld); w.BossBeaten = snap.Dive.BossBeaten; w.ExtraSlotOwner = snap.Dive.ExtraSlotOwner; }
            var found = new List<int>();
            if (depths != null) foreach (int id in depths.Keys) if (Holds(id)) found.Add(id);
            w.Pieces.Restore(found);
            return w;
        }

        static Dictionary<string, PlayerKey> BagKeys(PartyBags bags)
        {
            var keys = new Dictionary<string, PlayerKey>();
            foreach (KeyValuePair<string, PlayerBag> kv in bags.All)
                if (BagsSaveRules.TryParseBagId(kv.Key, out PlayerKey key)) keys[kv.Key] = key;
            return keys;
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

        // The dive state for a loaded snapshot: haul with its banked pieces and loot, every piece the town holds marked home (RestoreHaul), cleared floors and checkpoint, and the saved bags.
        // Pure: leaves the stock, the town and the loot counts alone, so the host can call it again at the first dive start with the piece counts and stir tuning as they
        // are then (they can change after the director spawns).
        public DiveState BuildDive(TownSnapshot snap, IReadOnlyList<int> piecesPerFloor, StirTuning tuning)
        {
            DiveHaul haul = TownSaves.RestoreHaul(snap?.Dive, piecesPerFloor);
            var dive = new DiveState(haul, tuning);
            if (snap?.Dive != null) dive.RestoreProgress(snap.Dive.ClearedMask, snap.Dive.CheckpointReached);
            BagsSaveRules.Restore(snap?.Bags, dive.Bags);   // each recorded player's gold, home stash and waiting satchel (no dive in progress)
            return dive;
        }
    }
}
