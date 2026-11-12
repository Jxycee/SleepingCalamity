// Core/Town/Placement/TownSaves.cs: the town's world-save sections (M6 S2) and their rules. Pure C#, no Unity types. Four optional sections of WorldSave, each cleaned on its own:
//   stock     what the town holds unplaced: dive pieces, rescued residents, loot counts
//   town      the placed buildings: foundation, turn, the piece each part consumed, the resident
//   residents per-resident state that never rolls back (the baker's first-meet beat, Q4 C)
//   dive      the cleared-floor mask, the plan 3 fields (held checkpoint, piece depths, extra-slot owner), the banked pieces and residents an OLD save may still hold, and the pieces still left on their floors
// Clean costs only the bad entry (an unknown piece, a building that does not fit its parts, a duplicate), never the section, and a section that does not fit its type is
// dropped alone by WorldSaveStore. Then the CONSERVATION pass (Reconcile) makes every known piece and resident (PieceCatalog, TownBuildings) sit in exactly ONE place:
// placed, else stock, else banked in the haul, else left on its floor. A duplicate keeps its highest place; a thing in no place goes to stock (only when the dive section is
// present: it is the authority on what is still on the floors, so a save without one never hands out free pieces). Quitting mid-dive is a failed dive (DiveSession.Fail):
// a piece is safe the moment it is picked up (it goes straight into the town stock), so a piece found mid-dive is saved in stock and never goes back to its floor; InDive is cleared on load.
// Player loot is not here: the optional "bags" section holds it (BagsSave), a mid-dive bag written as the failed copy. The old dive's town loot (stock.loot) and banked loot (dive.bankedLoot) are
// still READ so an old save loads: MigrateLegacyLoot moves them into the first recorded player's stash. A capture never writes them (nor dive.bankedPieces); the old 4-floor checkpoint
// (dive.checkpointReached) reads as not held.
// Plan 3 task 6 adds: the dive section's v2 fields (CheckpointHeld, BossBeaten, PieceDepths saved and never recomputed, ExtraSlotOwner, a ten-floor cleared mask) and the optional
// "suspend" section (SuspendSave.cs). A snapshot with a valid suspend section is a bank-room quit, never a failed dive.
// Authority: host only (the host's world save). The live town and haul reach it through HostWorld.ITownSource.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SleepingCalamity.Core
{
    [Serializable]
    public sealed class LootCountSave
    {
        public int ItemId = -1;
        public int Count;
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class StockSave
    {
        public List<int> Pieces = new List<int>();
        public List<string> Residents = new List<string>();
        public List<LootCountSave> Loot = new List<LootCountSave>();   // legacy (the 4-floor dive's town loot): read only, MigrateLegacyLoot moves it into a player's stash; a capture writes none
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class PlacedBuildingSave
    {
        public string BuildingId = "";
        public string FoundationId = "";
        public int Turn;                                    // quarter turns, 0-3
        public List<int> Parts = new List<int>();           // per BuildingDef.Parts index: the piece id consumed, -1 for an empty slot (index 0 = the frame)
        public string Resident;                             // null: nobody moved in
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class TownSave
    {
        public List<PlacedBuildingSave> Buildings = new List<PlacedBuildingSave>();
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class ResidentSave
    {
        public string Id = "";
        public bool FirstMeetBeat;                          // latched for good: the baker's first-meet line, gift and Wren's list tick (Q4 C)
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class ResidentsSave
    {
        public List<ResidentSave> Residents = new List<ResidentSave>();
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class DiveSave
    {
        public bool InDive;                                 // the party was below when this was written: load counts it as a failed dive
        public int ClearedMask;                             // floors cleared this dive (bit floor-1, ten bits; an old save's bits 1-4 are masked, never rejected)
        public bool CheckpointReached;                      // legacy (the old 4-floor checkpoint): read, then cleaned to false (it is not a plan 3 checkpoint); a capture writes false
        public List<int> BankedPieces = new List<int>();    // legacy: banked in the old haul, not yet home; RestoreWorld brings them home, a capture writes none
        public List<string> BankedResidents = new List<string>();   // legacy: the baker, banked at the old floor-4 heart, not yet home
        public List<LootCountSave> BankedLoot = new List<LootCountSave>();   // legacy: banked chest loot not yet moved; MigrateLegacyLoot moves it into a player's stash, a capture writes none
        public List<int> OnFloorPieces = new List<int>();   // known pieces not yet found (or lost to a failed dive): back on their floors
        public List<string> OnFloorResidents = new List<string>();   // the baker before the rescue
        public bool CheckpointHeld;                         // plan 3: DiveCheckpoint.Held (an old save has no field: not held)
        public bool BossBeaten;                             // plan 3: the boss shortcut is open
        public List<PieceDepthSave> PieceDepths;            // plan 3 decision 10: the floor of each piece and person, assigned once from the world seed and kept; null: an old save (assigned at first load)
        public string ExtraSlotOwner;                       // plan 3: the previous owner of the 3-player extra bank slot (a bag id "Kind:Id"), or null
        [JsonIgnore] public bool V2Known;                   // capture only: the source knows CheckpointHeld, BossBeaten and ExtraSlotOwner, so MergeLive does not carry the saved ones over
        public List<int> KnownPieces;                       // every piece id the writing build knew; null: a save from before this list (every then-current id counts as known)
        public List<string> KnownResidents;                 // same for resident ids
        [JsonExtensionData] public Dictionary<string, object> Extra;

        internal DiveSave ShallowCopy() => (DiveSave)MemberwiseClone();
    }

    [Serializable]
    public sealed class PieceDepthSave
    {
        public int Id;
        public int Floor;
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    // The four sections as one value (any may be null: an older save, or a section that was reset), plus the per-player bags section (Core/Save/BagsSave.cs; null: no bags saved).
    // Bags is not part of the piece/resident conservation pass (Reconcile): bag items are not pieces.
    public sealed class TownSnapshot
    {
        public SuspendSave Suspend;                         // the bank-room suspend (SuspendSave.cs); null: none, or the source did not say (see SuspendKnown)
        public bool SuspendKnown;                           // capture only: the source knows whether a suspend exists, so a null Suspend means "none" and clears the saved one (a used suspend)
        public StockSave Stock;
        public TownSave Town;
        public ResidentsSave Residents;
        public DiveSave Dive;
        public BagsSave Bags;
    }

    public static class TownSaveRules
    {
        public const string SectionStock = "stock", SectionTown = "town", SectionResidents = "residents", SectionDive = "dive", SectionSuspend = "suspend";
        public const int MaxLootCount = 999999;

        static bool KnownPiece(int id) => PieceCatalog.TryGet(id, out _);
        static bool KnownResident(string id) => id != null && TownBuildings.OfResident(id) != null;
        static Dictionary<string, object> CopyExtra(Dictionary<string, object> e) => e != null ? new Dictionary<string, object>(e) : null;

        public static IEnumerable<int> KnownPieceIds() { foreach (PieceInfo p in PieceCatalog.All) yield return p.Id; }
        public static IEnumerable<string> KnownResidentIds() { foreach (BuildingDef b in TownBuildings.All) yield return b.ResidentId; }

        // ---------------------------------------------------------------- per-section clean (each returns a fresh object; dropped gets one label per dropped entry)
        static List<int> CleanPieces(List<int> src, List<string> dropped, string what)
        {
            var set = new SortedSet<int>();
            if (src != null)
                foreach (int id in src)
                    if (!KnownPiece(id) || !set.Add(id)) dropped.Add(what + ":" + id);
            return new List<int>(set);
        }

        static List<string> CleanResidentIds(List<string> src, List<string> dropped, string what)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (src != null)
                foreach (string id in src)
                    if (!KnownResident(id) || !set.Add(id)) dropped.Add(what + ":" + id);
            return new List<string>(set);
        }

        static List<LootCountSave> CleanLoot(List<LootCountSave> src, List<string> dropped, string what)
        {
            var byItem = new SortedDictionary<int, long>();
            Dictionary<int, Dictionary<string, object>> extra = null;
            if (src != null)
                foreach (LootCountSave l in src)
                {
                    if (l == null || l.ItemId < 0 || l.Count <= 0) { dropped.Add(what); continue; }
                    byItem.TryGetValue(l.ItemId, out long have);
                    byItem[l.ItemId] = Math.Min(MaxLootCount, have + l.Count);
                    if (l.Extra != null) { if (extra == null) extra = new Dictionary<int, Dictionary<string, object>>(); extra[l.ItemId] = CopyExtra(l.Extra); }
                }
            var list = new List<LootCountSave>();
            foreach (KeyValuePair<int, long> kv in byItem)
                list.Add(new LootCountSave { ItemId = kv.Key, Count = (int)kv.Value, Extra = extra != null && extra.TryGetValue(kv.Key, out var e) ? e : null });
            return list;
        }

        public static StockSave CleanStock(StockSave s, List<string> dropped)
        {
            var clean = new StockSave();
            if (s == null) return clean;
            clean.Pieces = CleanPieces(s.Pieces, dropped, "piece");
            clean.Residents = CleanResidentIds(s.Residents, dropped, "resident");
            clean.Loot = CleanLoot(s.Loot, dropped, "loot");
            clean.Extra = CopyExtra(s.Extra);
            return clean;
        }

        // A building that does not fit is dropped whole (its pieces are found again by Reconcile and go to stock); a part slot or a resident that does not fit is
        // emptied alone.
        public static TownSave CleanTown(TownSave s, List<string> dropped)
        {
            var clean = new TownSave();
            if (s == null) return clean;
            var usedPieces = new HashSet<int>();
            var usedResidents = new HashSet<string>();
            var buildings = new SortedDictionary<string, PlacedBuildingSave>(StringComparer.Ordinal);
            if (s.Buildings != null)
                foreach (PlacedBuildingSave b in s.Buildings)
                {
                    BuildingDef def = b == null ? null : TownBuildings.Get(b.BuildingId);
                    if (def == null || buildings.ContainsKey(def.Id) || string.IsNullOrEmpty(b.FoundationId) || b.Parts == null || b.Parts.Count != def.Parts.Length
                        || !FitsPart(b.Parts[0], def, 0) || usedPieces.Contains(b.Parts[0]))
                    {
                        dropped.Add("building:" + (b?.BuildingId ?? "null"));
                        continue;
                    }
                    var c = new PlacedBuildingSave { BuildingId = def.Id, FoundationId = b.FoundationId, Turn = WishEvaluator.NormalTurn(b.Turn), Extra = CopyExtra(b.Extra) };
                    bool complete = true;
                    for (int i = 0; i < def.Parts.Length; i++)
                    {
                        int id = b.Parts[i];
                        if (id >= 0 && FitsPart(id, def, i) && usedPieces.Add(id)) c.Parts.Add(id);
                        else
                        {
                            if (id >= 0) dropped.Add("part:" + def.Id + "/" + def.Parts[i]);
                            c.Parts.Add(-1);
                            complete = false;
                        }
                    }
                    if (b.Resident != null)
                    {
                        if (b.Resident == def.ResidentId && complete && usedResidents.Add(b.Resident)) c.Resident = b.Resident;
                        else dropped.Add("resident:" + b.Resident);
                    }
                    buildings[def.Id] = c;
                }
            clean.Buildings.AddRange(buildings.Values);
            clean.Extra = CopyExtra(s.Extra);
            return clean;
        }

        static bool FitsPart(int pieceId, BuildingDef def, int partIndex) =>
            PieceCatalog.TryGet(pieceId, out PieceInfo info) && info.Building == def.Id && info.Part == def.Parts[partIndex];

        public static ResidentsSave CleanResidents(ResidentsSave s, List<string> dropped)
        {
            var clean = new ResidentsSave();
            if (s == null) return clean;
            var byId = new SortedDictionary<string, ResidentSave>(StringComparer.Ordinal);
            if (s.Residents != null)
                foreach (ResidentSave r in s.Residents)
                {
                    if (r == null || !KnownResident(r.Id)) { dropped.Add("resident:" + (r?.Id ?? "null")); continue; }
                    if (byId.TryGetValue(r.Id, out ResidentSave have)) { have.FirstMeetBeat |= r.FirstMeetBeat; continue; }   // a repeated record merges (a latch never rolls back)
                    byId[r.Id] = new ResidentSave { Id = r.Id, FirstMeetBeat = r.FirstMeetBeat, Extra = CopyExtra(r.Extra) };
                }
            clean.Residents.AddRange(byId.Values);
            clean.Extra = CopyExtra(s.Extra);
            return clean;
        }

        // A saved depth is valid when the id is in PieceCatalog.Specs (the baker is there, though it is not in PieceCatalog.All) and the floor is its fixed floor, or one of the generated
        // floors of its half (so a piece can never be saved onto the authored floor 1, a set piece or the bank, where nothing spawns it).
        static bool ValidDepth(int id, int floor)
        {
            foreach (PieceSpec spec in PieceCatalog.Specs)
            {
                if (spec.Id != id) continue;
                if (spec.FixedFloor != 0 && id != PieceCatalog.FrameId) return floor == spec.FixedFloor;   // the frame is fixed on floor 2 for new worlds (D1) but a saved one on floor 4 stays valid: saved worlds keep their assignment
                foreach (int f in PieceDepths.CandidateFloors(spec.SecondHalf)) if (f == floor) return true;
                return false;
            }
            return false;
        }

        // The floor of every piece and person: the saved depth where the (cleaned) dive section has a valid one, else the world seed's assignment. So a world's depths are decided once: the save
        // keeps them, and a seed or table that would assign differently later changes nothing for an entry already saved. dive may be null (an old save, or no dive section yet).
        public static IReadOnlyDictionary<int, int> ResolvePieceDepths(DiveSave dive, ulong worldSeed)
        {
            var result = new SortedDictionary<int, int>();
            foreach (KeyValuePair<int, int> kv in PieceDepths.Assign(worldSeed, PieceCatalog.Specs)) result[kv.Key] = kv.Value;
            if (dive?.PieceDepths != null)
                foreach (PieceDepthSave p in dive.PieceDepths)
                    if (p != null && ValidDepth(p.Id, p.Floor)) result[p.Id] = p.Floor;
            return result;
        }

        public static DiveSave CleanDive(DiveSave s, List<string> dropped)
        {
            var clean = new DiveSave();
            if (s == null) return clean;
            clean.InDive = s.InDive;
            clean.ClearedMask = s.ClearedMask & ((1 << UndercroftPlan.BossFloor) - 1);   // mask to the ten Undercroft floors rather than reject
            clean.CheckpointReached = false;   // the old 4-floor checkpoint (floor 2) maps to "not held": the plan 3 checkpoint is earned by going home from the bank room
            clean.CheckpointHeld = s.CheckpointHeld;
            clean.BossBeaten = s.BossBeaten;
            clean.V2Known = s.V2Known;
            if (s.ExtraSlotOwner != null)
            {
                if (BagsSaveRules.TryParseBagId(s.ExtraSlotOwner, out _)) clean.ExtraSlotOwner = s.ExtraSlotOwner;
                else dropped.Add("extra slot owner");
            }
            if (s.PieceDepths != null)
            {
                var depths = new SortedDictionary<int, PieceDepthSave>();
                foreach (PieceDepthSave p in s.PieceDepths)
                {
                    if (p == null || !ValidDepth(p.Id, p.Floor) || depths.ContainsKey(p.Id)) { dropped.Add("piece depth:" + (p == null ? "null" : p.Id.ToString())); continue; }
                    depths[p.Id] = new PieceDepthSave { Id = p.Id, Floor = p.Floor, Extra = CopyExtra(p.Extra) };
                }
                clean.PieceDepths = new List<PieceDepthSave>(depths.Values);
            }
            clean.BankedPieces = CleanPieces(s.BankedPieces, dropped, "banked piece");
            clean.BankedResidents = CleanResidentIds(s.BankedResidents, dropped, "banked resident");
            clean.BankedLoot = CleanLoot(s.BankedLoot, dropped, "banked loot");
            clean.OnFloorPieces = CleanPieces(s.OnFloorPieces, dropped, "floor piece");
            clean.OnFloorResidents = CleanResidentIds(s.OnFloorResidents, dropped, "floor resident");
            // the known-id lists are kept as written (null stays null: an older save); ids this build does not know are dropped quietly (a save from a newer build)
            if (s.KnownPieces != null) { var kp = new SortedSet<int>(); foreach (int id in s.KnownPieces) if (KnownPiece(id)) kp.Add(id); clean.KnownPieces = new List<int>(kp); }
            if (s.KnownResidents != null) { var kr = new SortedSet<string>(StringComparer.Ordinal); foreach (string id in s.KnownResidents) if (KnownResident(id)) kr.Add(id); clean.KnownResidents = new List<string>(kr); }
            clean.Extra = CopyExtra(s.Extra);
            return clean;
        }

        // Cleans every present section into a fresh snapshot (null stays null), names the sections that lost an entry in rejected, then runs the conservation pass.
        public static TownSnapshot CleanSnapshot(TownSnapshot s, List<string> rejected, List<string> repaired = null, bool failMidDive = true)
        {
            var clean = new TownSnapshot();
            if (s == null) return clean;
            var d = new List<string>();
            if (s.Stock != null) { d.Clear(); clean.Stock = CleanStock(s.Stock, d); if (d.Count > 0) rejected?.Add(SectionStock); }
            if (s.Town != null) { d.Clear(); clean.Town = CleanTown(s.Town, d); if (d.Count > 0) rejected?.Add(SectionTown); }
            if (s.Residents != null) { d.Clear(); clean.Residents = CleanResidents(s.Residents, d); if (d.Count > 0) rejected?.Add(SectionResidents); }
            if (s.Dive != null) { d.Clear(); clean.Dive = CleanDive(s.Dive, d); if (d.Count > 0) rejected?.Add(SectionDive); }
            if (s.Bags != null) { d.Clear(); clean.Bags = BagsSaveRules.Clean(s.Bags, d); if (d.Count > 0) rejected?.Add(WorldSaveRules.SectionBags); }
            if (s.Suspend != null) { d.Clear(); clean.Suspend = SuspendSaveRules.Clean(s.Suspend, d); if (d.Count > 0) rejected?.Add(SectionSuspend); }
            clean.SuspendKnown = s.SuspendKnown;
            // A suspend stands for a dive that is still in progress (the party is below, or waiting at the trapdoor to resume it: Dive.InDive). Without one it is stale (a source that never says
            // "known, none" would otherwise keep a used suspend forever, and a later failed-dive quit would resume from it): dropped here, whoever wrote it.
            // A suspend dropped here (bad floor or phase, or stale) keeps its dive records: they are settled as a failed dive into the bags (SuspendSaveRules.SettleAsFailed), never lost.
            if (clean.Suspend != null && (clean.Dive == null || !clean.Dive.InDive))
            {
                clean.Bags = SuspendSaveRules.SettleAsFailed(clean.Bags, s.Suspend);
                clean.Suspend = null;
                repaired?.Add("suspend dropped: no dive is in progress");
            }
            else if (s.Suspend != null && clean.Suspend == null)
            {
                clean.Bags = SuspendSaveRules.SettleAsFailed(clean.Bags, s.Suspend);
                repaired?.Add("suspend dropped: bad floor or phase; its dive records were settled as a failed dive");
            }
            Reconcile(clean, repaired, failMidDive);
            return clean;
        }

        // A latch never rolls back: every first-meet beat true in `previous` is true in the result. `live` (an object the caller owns) is updated in place and returned;
        // null live with a previous gives a copy of previous; both null gives null.
        public static ResidentsSave MergeLatches(ResidentsSave live, ResidentsSave previous)
        {
            if (previous == null) return live;
            if (live == null) live = new ResidentsSave { Extra = CopyExtra(previous.Extra) };
            if (live.Extra == null) live.Extra = CopyExtra(previous.Extra);   // unknown section-level fields from a newer build survive
            foreach (ResidentSave p in previous.Residents)
            {
                if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                ResidentSave have = live.Residents.Find(x => x != null && x.Id == p.Id);
                if (have == null) live.Residents.Add(new ResidentSave { Id = p.Id, FirstMeetBeat = p.FirstMeetBeat, Extra = CopyExtra(p.Extra) });
                else { have.FirstMeetBeat |= p.FirstMeetBeat; if (have.Extra == null) have.Extra = CopyExtra(p.Extra); }
            }
            return live;
        }

        // What a source captured, filled in from the host's previous sections where the source said nothing: a null section or null residents keep the previous copy, and a
        // first-meet beat never goes back to false. Returns a new snapshot (clean it before use); inputs are not changed.
        public static TownSnapshot MergeLive(TownSnapshot live, TownSnapshot previous)
        {
            if (live == null) return previous;
            previous = previous ?? new TownSnapshot();
            var d = new List<string>();
            StockSave stock = live.Stock;
            ResidentsSave res = live.Residents == null ? null : CleanResidents(live.Residents, d);
            return new TownSnapshot
            {
                Stock = stock ?? previous.Stock, Town = live.Town ?? previous.Town, Dive = MergeDive(live.Dive, previous.Dive), Residents = MergeLatches(res, previous.Residents),
                Bags = live.Bags ?? previous.Bags,
                Suspend = live.SuspendKnown ? live.Suspend : previous.Suspend, SuspendKnown = live.SuspendKnown,   // null from a source that did not say keeps the saved one; known-null clears it
            };
        }

        // The live dive section over the saved one. A live capture knows the piece depths only through the host (it stamps them), so the saved piece depths always carry over, and the saved
        // CheckpointHeld, BossBeaten and ExtraSlotOwner carry over unless the source marked them known (V2Known). The live section is copied, never changed.
        static DiveSave MergeDive(DiveSave live, DiveSave previous)
        {
            if (live == null) return previous;
            if (previous == null) return live;
            DiveSave merged = live.ShallowCopy();
            if (!live.V2Known) { merged.CheckpointHeld = previous.CheckpointHeld; merged.BossBeaten = previous.BossBeaten; merged.ExtraSlotOwner = previous.ExtraSlotOwner; }
            if (live.PieceDepths == null) merged.PieceDepths = previous.PieceDepths;
            return merged;
        }

        // ---------------------------------------------------------------- the conservation pass (operates on cleaned sections, in place)
        // Priority: placed > stock > banked in the haul > left on its floor. repaired (may be null) gets one line per fix.
        public static void Reconcile(TownSnapshot s, List<string> repaired, bool failMidDive = true)
        {
            if (s == null || (s.Stock == null && s.Town == null && s.Dive == null)) return;   // an older save, or only residents: nothing to conserve
            if (s.Stock == null) s.Stock = new StockSave();
            if (s.Town == null) s.Town = new TownSave();

            if (failMidDive && s.Dive != null && s.Dive.InDive && s.Suspend == null)   // a bank-room suspend is a save, not a failed dive
            {
                s.Dive.InDive = false;   // FailDive: nothing unbanked was saved (it is on its floor); banked, cleared floors and the checkpoint stay
                repaired?.Add("quit mid-dive: counted as a failed dive");
            }

            var pieces = new HashSet<int>();
            var residents = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlacedBuildingSave b in s.Town.Buildings)
            {
                foreach (int p in b.Parts) if (p >= 0) pieces.Add(p);
                if (b.Resident != null) residents.Add(b.Resident);
            }
            RemoveSeen(s.Stock.Pieces, pieces, repaired, "stock");
            RemoveSeen(s.Stock.Residents, residents, repaired, "stock");
            if (s.Dive != null)
            {
                RemoveSeen(s.Dive.BankedPieces, pieces, repaired, "banked haul");
                RemoveSeen(s.Dive.BankedResidents, residents, repaired, "banked haul");
                RemoveSeen(s.Dive.OnFloorPieces, pieces, repaired, "floor");
                RemoveSeen(s.Dive.OnFloorResidents, residents, repaired, "floor");
                // An id the save knew but that is nowhere was lost: back in stock. An id the save never knew (content a later build added) is not owed: it goes to its floor,
                // unrescued. A dive section without the known lists (written before them) treats every then-current id as known, so older saves behave as before.
                HashSet<int> knownP = s.Dive.KnownPieces == null ? null : new HashSet<int>(s.Dive.KnownPieces);
                HashSet<string> knownR = s.Dive.KnownResidents == null ? null : new HashSet<string>(s.Dive.KnownResidents, StringComparer.Ordinal);
                foreach (int id in KnownPieceIds())
                {
                    if (pieces.Contains(id)) continue;
                    pieces.Add(id);
                    if (knownP == null || knownP.Contains(id)) { s.Stock.Pieces.Add(id); repaired?.Add("piece " + id + " was nowhere: back in stock"); }
                    else { s.Dive.OnFloorPieces.Add(id); repaired?.Add("piece " + id + " is new content: on its floor"); }
                }
                foreach (string id in KnownResidentIds())
                {
                    if (residents.Contains(id)) continue;
                    residents.Add(id);
                    if (knownR == null || knownR.Contains(id)) { s.Stock.Residents.Add(id); repaired?.Add("resident " + id + " was nowhere: back in stock"); }
                    else { s.Dive.OnFloorResidents.Add(id); repaired?.Add("resident " + id + " is new content: on its floor"); }
                }
                s.Stock.Pieces.Sort();
                s.Stock.Residents.Sort(StringComparer.Ordinal);
                s.Dive.OnFloorPieces.Sort();
                s.Dive.OnFloorResidents.Sort(StringComparer.Ordinal);
                // every current id is accounted for now: the next save knows them all
                s.Dive.KnownPieces = new List<int>(KnownPieceIds());
                s.Dive.KnownResidents = new List<string>(KnownResidentIds());
                s.Dive.KnownPieces.Sort();
                s.Dive.KnownResidents.Sort(StringComparer.Ordinal);
            }
        }

        static void RemoveSeen<T>(List<T> list, HashSet<T> seen, List<string> repaired, string where)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (seen.Add(list[i])) continue;
                repaired?.Add(list[i] + " listed twice: removed from " + where);
                list.RemoveAt(i--);
            }
        }

        // ---------------------------------------------------------------- pristine (a fresh world's value: not worth writing into a save that has no such section)
        public static bool IsPristine(StockSave s) => s == null || (s.Pieces.Count == 0 && s.Residents.Count == 0 && s.Loot.Count == 0 && s.Extra == null);
        public static bool IsPristine(TownSave s) => s == null || (s.Buildings.Count == 0 && s.Extra == null);
        public static bool IsPristine(ResidentsSave s)
        {
            if (s == null) return true;
            foreach (ResidentSave r in s.Residents) if (r.FirstMeetBeat || r.Extra != null) return false;
            return s.Extra == null;
        }
        public static bool IsPristine(DiveSave s)
        {
            if (s == null) return true;
            int known = 0;
            foreach (int _ in KnownPieceIds()) known++;
            foreach (string _ in KnownResidentIds()) known++;
            // PieceDepths is deliberately not part of this: an untouched, never-stamped world's depths are re-derived from its seed at the next load (and D1 reshuffled those worlds' non-frame pieces), so they alone are not worth a dive section in the file.
            return !s.InDive && s.ClearedMask == 0 && !s.CheckpointReached && !s.CheckpointHeld && !s.BossBeaten && s.ExtraSlotOwner == null && s.BankedPieces.Count == 0 && s.BankedResidents.Count == 0 && s.BankedLoot.Count == 0
                && s.OnFloorPieces.Count + s.OnFloorResidents.Count == known && s.Extra == null;
        }
    }

    // Live state <-> save sections.
    public static class TownSaves
    {
        // The sections for the host's live town. bankedResidents is any resident already found but not in stock or placed (null: none; the new dive stocks a person the moment they are found);
        // residents is the host's resident state (null: "the source does not know", HostWorld keeps its saved copy). Everything not in stock, placed or found is written as left on its floor. The
        // dive section carries no loot, no banked pieces and a false legacy checkpoint: a capture clears what an old save held. The bags section is not captured here (TownStock.CaptureWorld adds
        // it: it needs the bag ids). The result is cleaned and conserved.
        // suspend / suspendKnown (plan 3): the bank-room suspend section to carry, and whether the caller knows it (known + null clears the saved one). The existing callers say nothing.
        public static TownSnapshot Capture(TownState town, IEnumerable<string> bankedResidents, ResidentsSave residents, SuspendSave suspend = null, bool suspendKnown = false)
        {
            var raw = new TownSnapshot { Stock = new StockSave(), Town = new TownSave(), Residents = residents, Dive = new DiveSave(), Suspend = suspend, SuspendKnown = suspendKnown };
            raw.Dive.KnownPieces = new List<int>(TownSaveRules.KnownPieceIds());
            raw.Dive.KnownResidents = new List<string>(TownSaveRules.KnownResidentIds());
            if (suspend != null) raw.Dive.InDive = true;   // a suspend is a dive still in progress (CleanSnapshot drops one without)
            var pieces = new HashSet<int>();
            var people = new HashSet<string>(StringComparer.Ordinal);
            if (town != null)
            {
                foreach (int p in town.StockPieces) { raw.Stock.Pieces.Add(p); pieces.Add(p); }
                foreach (string r in town.StockResidents) { raw.Stock.Residents.Add(r); people.Add(r); }
                foreach (PlacedBuilding b in town.Placed)
                {
                    raw.Town.Buildings.Add(new PlacedBuildingSave { BuildingId = b.BuildingId, FoundationId = b.FoundationId, Turn = b.Turn, Parts = new List<int>(b.PiecesByPart), Resident = b.Resident });
                    foreach (int p in b.PiecesByPart) if (p >= 0) pieces.Add(p);
                    if (b.Resident != null) people.Add(b.Resident);
                }
            }
            if (bankedResidents != null)
                foreach (string r in bankedResidents) { raw.Dive.BankedResidents.Add(r); people.Add(r); }
            foreach (int id in TownSaveRules.KnownPieceIds()) if (!pieces.Contains(id)) raw.Dive.OnFloorPieces.Add(id);
            foreach (string id in TownSaveRules.KnownResidentIds()) if (!people.Contains(id)) raw.Dive.OnFloorResidents.Add(id);
            return TownSaveRules.CleanSnapshot(raw, null, null, false);   // a live capture keeps InDive: the failed-dive rule applies when the file is loaded
        }

        // A TownState from the (cleaned) sections. A building the layout refuses (unknown or occupied foundation, wrong size, would cut the path graph) is not placed:
        // its pieces and resident go back to stock, so nothing is lost. repaired (may be null) gets a line per such building.
        public static TownState ToState(TownSnapshot snap, TownLayout layout, List<string> repaired = null)
        {
            var state = new TownState(layout);
            if (snap == null) return state;
            var back = new List<int>();
            var backPeople = new List<string>();
            if (snap.Town != null)
                foreach (PlacedBuildingSave b in snap.Town.Buildings)
                {
                    PlaceResult r = state.RestoreBuilding(b.BuildingId, b.FoundationId, b.Turn, b.Parts, b.Resident != null);
                    bool residentLeft = false;
                    if (r == PlaceResult.NotComplete || r == PlaceResult.ResidentAlreadyPlaced)
                    {
                        r = state.RestoreBuilding(b.BuildingId, b.FoundationId, b.Turn, b.Parts, false);
                        residentLeft = b.Resident != null;
                    }
                    if (r == PlaceResult.Ok)
                    {
                        if (residentLeft) { backPeople.Add(b.Resident); repaired?.Add("resident " + b.Resident + " could not move into " + b.BuildingId + ": back in stock"); }
                        continue;
                    }
                    repaired?.Add("building " + b.BuildingId + " on " + b.FoundationId + " refused (" + r + "): back in stock");
                    foreach (int p in b.Parts) if (p >= 0) back.Add(p);
                    if (b.Resident != null) backPeople.Add(b.Resident);
                }
            if (snap.Stock != null)
            {
                foreach (int p in snap.Stock.Pieces) state.AddPiece(p);
                foreach (string r in snap.Stock.Residents) state.AddResident(r);
            }
            foreach (int p in back) state.AddPiece(p);
            foreach (string r in backPeople) state.AddResident(r);
            return state;
        }

        // The legacy loot of an old save (stock.loot and dive.bankedLoot, the 4-floor dive's town and banked chest loot) moved into the first recorded player's home stash (plan 3 task 12). Both
        // lists are emptied, so a save written after the load no longer carries them; the stash entry is created when the player has no bag record yet. Idempotent. `first` is the world's first
        // recorded player; with none (default PlayerKey) nothing is moved and the lists stay. Returns the item count moved.
        public static int MigrateLegacyLoot(TownSnapshot snap, IReadOnlyList<WorldPlayerSave> recorded)
        {
            if (recorded != null)
                foreach (WorldPlayerSave r in recorded)
                {
                    if (r == null || !BagsSaveRules.TryKind(r.Identity, out IdentityKind kind)) continue;
                    var key = new PlayerKey(kind, r.Id);
                    if (key.IsValid) return MigrateLegacyLoot(snap, key);
                }
            return 0;
        }

        public static int MigrateLegacyLoot(TownSnapshot snap, PlayerKey first)
        {
            if (snap == null || !first.IsValid) return 0;
            bool stockHas = snap.Stock != null && snap.Stock.Loot.Count > 0, bankedHas = snap.Dive != null && snap.Dive.BankedLoot.Count > 0;
            if (!stockHas && !bankedHas) return 0;
            if (snap.Bags == null) snap.Bags = new BagsSave();
            PlayerBagSave entry = null;
            foreach (PlayerBagSave p in snap.Bags.Players)
                if (p != null && p.Identity == (int)first.Kind && p.Id == first.Id) { entry = p; break; }
            if (entry == null) { entry = new PlayerBagSave { Identity = (int)first.Kind, Id = first.Id }; snap.Bags.Players.Add(entry); }
            int moved = 0;
            var add = new List<LootCountSave>();
            if (stockHas) add.AddRange(snap.Stock.Loot);
            if (bankedHas) add.AddRange(snap.Dive.BankedLoot);
            foreach (LootCountSave l in add)
            {
                if (l == null || l.ItemId <= 0 || l.Count <= 0) continue;   // a bag stash holds real items only (ids above 0)
                LootCountSave have = entry.Stash.Find(x => x != null && x.ItemId == l.ItemId);
                if (have == null) entry.Stash.Add(new LootCountSave { ItemId = l.ItemId, Count = Math.Min(TownSaveRules.MaxLootCount, l.Count) });
                else have.Count = Math.Min(TownSaveRules.MaxLootCount, have.Count + l.Count);
                moved += l.Count;
            }
            if (snap.Stock != null) snap.Stock.Loot.Clear();
            if (snap.Dive != null) snap.Dive.BankedLoot.Clear();
            return moved;
        }

        public static bool FirstMeetBeat(ResidentsSave r, string residentId)
        {
            if (r != null) foreach (ResidentSave x in r.Residents) if (x.Id == residentId) return x.FirstMeetBeat;
            return false;
        }
    }
}
