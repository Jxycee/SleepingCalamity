// Core/Cellars/CellarPieceSpots.cs: where the bakery pieces lie on the generated Undercroft floors 2, 4, 6 and 7, plus the baker's spot and the lost-satchel spot (plan 3 task 5, decision 10). Pure C#, no engine references.
// Floors 1 (authored), 3 (the baker's bakery) and 5 (guardian + bank) hold no piece spots: random pieces never land there (PieceDepths). One floor can hold up to four pieces, so a floor must offer four cells.
// The i-th returned cell holds the i-th piece of that floor that is not found yet (DivePieces.OnFloor). Cells come from the free cells of combat rooms that are neither the locked room nor
// an entry room (CellarEncounterPlan.FreeCellsOf already excludes doors, authored spawns, chests, features, barrels and crates, vignettes, the healing point, the carrier,
// dead ends, entry/stair/bank/baker cells), spread over the eligible rooms round-robin from a room order shuffled on the stream Rng.Derive(seed, "cellars.pieces", floor).
// A floor outside the generated set, and a floor with no eligible cell, return none. Fewer cells than asked returns fewer (never an illegal cell). Own stream, no golden moves.
// Authority: none (static data; every peer derives the same spots, the host decides who collected one).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarPieceSpots
    {
        public static IReadOnlyList<CellPos> Choose(CellarFloorLayout layout, CellarFloorDressing dressing, int count)
        {
            var result = new List<CellPos>();
            if (layout == null || !HoldsPieces(layout.Floor) || layout.IsStub || count <= 0) return result;
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

        // Generated floors only (2, 4, 6, 7 in Phase 1).
        public static bool HoldsPieces(int floor) => CellarFloorGenerator.IsGeneratedFloor(floor);

        // The baker sleeps on the bakery floor (floor 3) at the hearth: the layout's own BakerCell. Null on any other floor.
        public static CellPos? BakerSpot(CellarFloorLayout layout)
            => layout != null && layout.Floor == PieceCatalog.BakerFloor ? layout.BakerCell : null;

        // Where a failed party's lost satchel lies on its floor (plan 3 task 9 places it): a free floor cell of a room other than the party start's, never the stair, the end door, a chest or a piece
        // spot, picked on the stream "cellars.satchel" and as far from the party start as the choice allows (the farther half of the candidates, so the recovery walk is real).
        // Phase 1 has no water, so every floor cell is dry. Deterministic per (layout, seed).
        public static CellPos SatchelSpot(CellarFloorLayout layout, uint seed)
        {
            CellarFloorDressing dressing = CellarFloorDressing.Resolve(layout);
            var taken = new HashSet<(int, int)>();
            foreach (CellPos p in Choose(layout, dressing, MaxPiecesOnAFloor)) taken.Add((p.X, p.Y));
            foreach (var c in layout.Chests) taken.Add((c.X, c.Y));
            foreach (CellPos p in layout.EntryStarts) taken.Add((p.X, p.Y));
            if (layout.StairExit.HasValue) taken.Add((layout.StairExit.Value.X, layout.StairExit.Value.Y));
            if (layout.EndCell.HasValue) taken.Add((layout.EndCell.Value.X, layout.EndCell.Value.Y));
            if (layout.BankCell.HasValue) taken.Add((layout.BankCell.Value.X, layout.BankCell.Value.Y));
            if (layout.BakerCell.HasValue) taken.Add((layout.BakerCell.Value.X, layout.BakerCell.Value.Y));
            if (layout.WardenCell.HasValue) taken.Add((layout.WardenCell.Value.X, layout.WardenCell.Value.Y));
            int startRoom = layout.EntryStarts.Count > 0 ? layout.RoomAt(layout.EntryStarts[0].X, layout.EntryStarts[0].Y) : -1;
            var cands = new List<CellPos>();
            for (int tier = 0; tier < 2 && cands.Count == 0; tier++)   // tier 0: free cells of combat rooms; tier 1 (fallback): any floor cell outside the start room
                for (int r = 0; r < layout.Rooms.Count; r++)
                {
                    if (r == startRoom) continue;
                    PlacedRoom pr = layout.Rooms[r];
                    if (tier == 0 && (pr.Room.Kind != RoomKind.Combat || r == layout.LockedRoomIndex)) continue;
                    IEnumerable<CellPos> cells = tier == 0 ? CellarEncounterPlan.FreeCellsOf(layout, dressing, r) : AllFloorCells(pr);
                    foreach (CellPos c in cells) if (!taken.Contains((c.X, c.Y))) cands.Add(c);
                }
            if (cands.Count == 0) throw new System.InvalidOperationException("floor " + layout.Floor + ": no cell for the satchel");
            CellPos from = layout.EntryStarts.Count > 0 ? layout.EntryStarts[0] : cands[0];
            cands.Sort((a, b) => { int d = Dist(b, from).CompareTo(Dist(a, from)); return d != 0 ? d : (a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X)); });   // farthest first, ties row-major
            int far = System.Math.Max(1, (cands.Count + 1) / 2);
            var rng = new Pcg32(Rng.Derive(seed, "cellars.satchel", (ulong)layout.Floor));
            return cands[rng.Range(0, far)];
        }

        public const int MaxPiecesOnAFloor = 4;   // decision 10: the four first-half pieces can all land on one floor

        static IEnumerable<CellPos> AllFloorCells(PlacedRoom pr)
        {
            for (int y = 0; y < pr.Height; y++)
                for (int x = 0; x < pr.Width; x++)
                    if (pr.Room.CellAt(x, y) == CellKind.Floor) yield return pr.Cell(x, y);
        }

        static int Dist(CellPos a, CellPos b) => System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);

        static bool HoldsEntry(CellarFloorLayout layout, int room)
        {
            foreach (CellPos s in layout.EntryStarts) if (layout.RoomAt(s.X, s.Y) == room) return true;
            return false;
        }
    }
}
