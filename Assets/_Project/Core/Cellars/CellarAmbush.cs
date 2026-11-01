// Core/Cellars/CellarAmbush.cs: where the enemies of the Ambush floor's ambush room stand when they spring (procgen 3b). Pure C#, no engine references.
// A pure function of (layout, dressing, plan, room): for each non-hunter plan spawn of the room, in plan order, the cell it springs at. Candidates are the room's free floor cells
// (CellarEncounterPlan.FreeCellsOf: never a door, chest, vignette, heal point, authored object) plus the plan's own movable spawn cells, minus resolved chunk solid cells and clutter cells.
// The ring is the farther half (at least as many cells as spawns) of the candidates from the room's centre, i.e. along the walls; each spawn picks a distinct ring cell on its own
// stream Rng.Derive(seed, "cellars.ambush", floor << 32 | room). The gate-key carrier and the warden keep their authored cell (their logic is keyed to it); a spawn with no cell left
// keeps its plan cell, so the count always matches the plan. Authority: none (static data); the host consumes it.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarAmbush
    {
        public static CellPos[] Cells(CellarFloorLayout layout, CellarFloorDressing dressing, CellarEncounterPlan plan, int room)
        {
            var mine = new List<EncounterSpawn>();
            foreach (EncounterSpawn s in plan.Spawns) if (s.RoomIndex == room && s.Kind != EncounterKind.Hunter) mine.Add(s);
            var result = new CellPos[mine.Count];
            var fixedAt = new bool[mine.Count]; int movable = 0;
            for (int i = 0; i < mine.Count; i++)
            {
                EncounterSpawn s = mine[i]; result[i] = s.Cell;
                bool carrier = s.Kind == EncounterKind.Authored && layout.Carrier != null && layout.Carrier.X == s.X && layout.Carrier.Y == s.Y;
                bool warden = layout.WardenCell.HasValue && layout.WardenCell.Value.X == s.X && layout.WardenCell.Value.Y == s.Y;
                fixedAt[i] = carrier || warden; if (!fixedAt[i]) movable++;
            }
            if (movable == 0 || room < 0 || room >= layout.Rooms.Count) return result;

            int n = CellarFloorLayout.GridSize;
            var bad = new bool[n, n];
            foreach (ResolvedChunk c in CellarRoomChunks.Resolve(layout, dressing).ChunksOf(room))
                foreach (var sc in CellarRoomChunks.SolidCells(layout, c)) bad[sc.cell.X, sc.cell.Y] = true;
            foreach (CellarClutterRow row in CellarFloorClutter.Resolve(layout, dressing).Rows) if (row.RoomIndex == room) bad[row.CellX, row.CellY] = true;
            for (int i = 0; i < mine.Count; i++) if (fixedAt[i]) bad[mine[i].X, mine[i].Y] = true;

            var cand = new List<CellPos>(); var seen = new bool[n, n];
            foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(layout, dressing, room)) if (!bad[c.X, c.Y] && !seen[c.X, c.Y]) { seen[c.X, c.Y] = true; cand.Add(c); }
            for (int i = 0; i < mine.Count; i++) if (!fixedAt[i] && !bad[mine[i].X, mine[i].Y] && !seen[mine[i].X, mine[i].Y]) { seen[mine[i].X, mine[i].Y] = true; cand.Add(mine[i].Cell); }

            PlacedRoom p = layout.Rooms[room];
            int cx2 = 2 * p.X + p.Width - 1, cy2 = 2 * p.Y + p.Height - 1;   // twice the centre, so integer maths
            long Dist(CellPos c) { long dx = 2L * c.X - cx2, dy = 2L * c.Y - cy2; return dx * dx + dy * dy; }
            cand.Sort((a, b) => { int d = Dist(b).CompareTo(Dist(a)); if (d != 0) return d; d = a.Y.CompareTo(b.Y); return d != 0 ? d : a.X.CompareTo(b.X); });
            int ring = System.Math.Min(cand.Count, System.Math.Max(movable, cand.Count / 2));
            var pool = cand.GetRange(0, ring);
            var rest = cand.GetRange(ring, cand.Count - ring);   // farthest-first, used only when the ring runs out

            var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.ambush", ((ulong)layout.Floor << 32) | (uint)room));
            var used = new bool[n, n];
            for (int i = 0; i < mine.Count; i++) if (fixedAt[i]) used[mine[i].X, mine[i].Y] = true;
            var moved = new bool[mine.Count];
            for (int i = 0; i < mine.Count; i++)
            {
                if (fixedAt[i]) continue;
                if (pool.Count > 0) { int k = rng.Range(0, pool.Count); result[i] = pool[k]; pool.RemoveAt(k); used[result[i].X, result[i].Y] = true; moved[i] = true; }
                else if (rest.Count > 0) { result[i] = rest[0]; rest.RemoveAt(0); used[result[i].X, result[i].Y] = true; moved[i] = true; }
            }
            // No cell left for a spawn: it keeps its plan cell, or, when another spawn took it, a plan cell another spawn vacated (always one: ring picks outside the plan cells use none), so cells stay distinct.
            for (int i = 0; i < mine.Count; i++)
            {
                if (fixedAt[i] || moved[i] || !used[mine[i].X, mine[i].Y]) { if (!fixedAt[i] && !moved[i]) used[mine[i].X, mine[i].Y] = true; continue; }
                bool placed = false;
                for (int j = 0; j < mine.Count && !placed; j++)   // a cell another spawn vacated
                    if (!used[mine[j].X, mine[j].Y]) { result[i] = mine[j].Cell; used[mine[j].X, mine[j].Y] = true; placed = true; }
            }
            return result;
        }
    }
}
