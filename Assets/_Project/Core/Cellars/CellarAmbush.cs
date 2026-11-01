// Core/Cellars/CellarAmbush.cs: where the enemies of the Ambush floor's ambush room stand when they spring (procgen 3b). Pure C#, no engine references.
// A pure function of (layout, dressing, plan, room): for each non-hunter plan spawn of the room, in plan order, the cell it springs at. Candidates are the room's free floor cells
// (CellarEncounterPlan.FreeCellsOf: never a door, chest, vignette, heal point, authored object) plus the plan's own movable spawn cells, minus resolved chunk solid cells (clutter is not excluded: it keeps clear of spawn cells and never sits on chunk solids).
// The ring is the farther half (at least as many cells as spawns) of the candidates from the room's centre, i.e. along the walls; each spawn picks a distinct ring cell on its own
// stream Rng.Derive(seed, "cellars.ambush", floor << 32 | room). The gate-key carrier and the warden keep their authored cell (their logic is keyed to it); a spawn with no cell left
// keeps its plan cell (or a plan cell another spawn vacated, non-solid first), so the count always matches the plan. Authority: none (static data); the host consumes it.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarAmbush
    {
        static bool TakenByMoved(List<EncounterSpawn> mine, bool[] moved, CellPos[] result, int i)
        {
            for (int j = 0; j < mine.Count; j++) if (j != i && moved[j] && result[j].X == mine[i].X && result[j].Y == mine[i].Y) return true;
            return false;
        }

        public static CellPos[] Cells(CellarFloorLayout layout, CellarFloorDressing dressing, CellarEncounterPlan plan, int room) => Cells(layout, dressing, plan, room, out _);

        // `fallbacks`: movable spawns that found no candidate cell (cand.Count < movable) and kept or swapped plan cells instead. Zero means every movable spawn got a ring/candidate pick.
        public static CellPos[] Cells(CellarFloorLayout layout, CellarFloorDressing dressing, CellarEncounterPlan plan, int room, out int fallbacks)
        {
            fallbacks = 0;
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
            // No cell left for a spawn (cand ran dry, so this is the true fallback): it keeps its plan cell when that is not chunk solid and nobody took it; otherwise it takes a plan cell another
            // spawn vacated (non-solid first, then any), so cells stay distinct and the count matches the plan.
            for (int i = 0; i < mine.Count; i++) if (!fixedAt[i] && !moved[i]) fallbacks++;
            for (int i = 0; i < mine.Count; i++) if (!fixedAt[i] && !moved[i] && !bad[mine[i].X, mine[i].Y]) used[mine[i].X, mine[i].Y] = true;
            for (int i = 0; i < mine.Count; i++)
            {
                if (fixedAt[i] || moved[i]) continue;
                if (!bad[mine[i].X, mine[i].Y] && result[i].X == mine[i].X && result[i].Y == mine[i].Y && !TakenByMoved(mine, moved, result, i)) continue;
                int pick = -1;
                for (int pass = 0; pass < 2 && pick < 0; pass++)
                    for (int j = 0; j < mine.Count && pick < 0; j++)
                        if (!used[mine[j].X, mine[j].Y] && (pass == 1 || !bad[mine[j].X, mine[j].Y])) pick = j;
                if (pick >= 0) { result[i] = mine[pick].Cell; used[mine[pick].X, mine[pick].Y] = true; }
            }
            return result;
        }
    }
}
