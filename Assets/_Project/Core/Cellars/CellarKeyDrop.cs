// Core/Cellars/CellarKeyDrop.cs: where the gate key may lie after its carrier dies (playtest 18). Pure C#, no engine references.
// A walkable cell is a Floor cell of a room that no resolved chunk marks solid. Settle returns the cell itself when it is walkable (and the caller did not report the exact
// spot as inside a wall); otherwise (or when forceMove is set, then the cell's own centre) the nearest walkable cell by centre distance (ties: smaller y, then smaller x), so the key can always be picked up. Authority: none (static).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarKeyDrop
    {
        public static bool[,] Walkable(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            int n = CellarFloorLayout.GridSize;
            var ok = new bool[n, n];
            var resolved = CellarRoomChunks.Resolve(layout, dressing);
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++)
                    if (p.Room.CellAt(x, y) == CellKind.Floor) ok[p.X + x, p.Y + y] = true;
                foreach (ResolvedChunk c in resolved.ChunksOf(r))
                    foreach (var sc in CellarRoomChunks.SolidCells(layout, c)) ok[sc.cell.X, sc.cell.Y] = false;
            }
            return ok;
        }

        // `cell`: the cell the key would lie in. `forceMove`: the host found the exact spot inside a wall though the cell is walkable. `moved` is true when the key must leave its exact spot (new cell, or snap to this cell's centre).
        public static CellPos Settle(CellarFloorLayout layout, CellarFloorDressing dressing, CellPos cell, bool forceMove, out bool moved)
        {
            moved = false;
            int n = CellarFloorLayout.GridSize;
            bool[,] ok = Walkable(layout, dressing);
            bool inside = cell.X >= 0 && cell.Y >= 0 && cell.X < n && cell.Y < n;
            if (inside && ok[cell.X, cell.Y]) { moved = forceMove; return cell; }   // forceMove: keep the cell, the caller snaps the key to its centre
            long best = long.MaxValue; CellPos pick = cell; bool found = false;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                if (!ok[x, y]) continue;
                long dx = x - cell.X, dy = y - cell.Y, d = dx * dx + dy * dy;
                if (d < best) { best = d; pick = new CellPos(x, y); found = true; }
            }
            if (!found) return cell;
            moved = pick.X != cell.X || pick.Y != cell.Y;
            return pick;
        }
    }
}
