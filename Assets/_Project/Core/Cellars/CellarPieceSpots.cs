// Core/Cellars/CellarPieceSpots.cs: where the bakery pieces lie on floors 1-3 (PHASE1 7.2). Pure C#, no engine references.
// The i-th returned cell holds the i-th piece of floor f that is not already home (DiveHaul.PieceId(f, i) when none is home). Cells come from the free cells of combat rooms that are neither the locked room nor
// an entry room (CellarEncounterPlan.FreeCellsOf already excludes doors, authored spawns, chests, features, barrels and crates, vignettes, the healing point, the carrier,
// dead ends, entry/stair/bank/baker cells), spread over the eligible rooms round-robin from a room order shuffled on the stream Rng.Derive(seed, "cellars.pieces", floor).
// Floor 4 (the bakery) and a floor with no eligible cell return none. Fewer cells than asked returns fewer (never an illegal cell). Own stream, no golden moves.
// Authority: none (static data; every peer derives the same spots, the host decides who collected one).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarPieceSpots
    {
        public static IReadOnlyList<CellPos> Choose(CellarFloorLayout layout, CellarFloorDressing dressing, int count)
        {
            var result = new List<CellPos>();
            if (layout == null || layout.Floor < 1 || layout.Floor > DiveHaul.PieceFloors || layout.IsStub || count <= 0) return result;
            var rooms = new List<int>(); var cells = new List<List<CellPos>>();
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                if (r == layout.LockedRoomIndex || layout.Rooms[r].Room.Kind != RoomKind.Combat || HoldsEntry(layout, r)) continue;
                var free = new List<CellPos>(CellarEncounterPlan.FreeCellsOf(layout, dressing, r));
                if (free.Count == 0) continue;
                rooms.Add(r); cells.Add(free);
            }
            var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.pieces", (ulong)layout.Floor));
            for (int i = rooms.Count - 1; i > 0; i--)   // Fisher-Yates over the eligible rooms
            {
                int j = rng.Range(0, i + 1);
                (rooms[i], rooms[j]) = (rooms[j], rooms[i]); (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            bool any = true;
            while (result.Count < count && any)
            {
                any = false;
                for (int k = 0; k < rooms.Count && result.Count < count; k++)
                {
                    List<CellPos> free = cells[k];
                    if (free.Count == 0) continue;
                    int pick = rng.Range(0, free.Count);
                    result.Add(free[pick]); free.RemoveAt(pick); any = true;
                }
            }
            return result;
        }

        static bool HoldsEntry(CellarFloorLayout layout, int room)
        {
            foreach (CellPos s in layout.EntryStarts) if (layout.RoomAt(s.X, s.Y) == room) return true;
            return false;
        }
    }
}
