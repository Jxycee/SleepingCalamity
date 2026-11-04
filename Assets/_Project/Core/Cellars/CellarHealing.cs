// Core/Cellars/CellarHealing.cs: the healing point of one Cellars floor (PHASE1 P47: about half of floors 1-3 hold one; floor 4 and the floor-2 checkpoint heal at the bank).
// A third pure function of the same (seed, floor): variety layer 6. It draws from its OWN stream Rng.Derive(seed, "cellars.healing", floor), so the layout and the
// dressing (LayoutHash, DressingHash goldens) never move; it only READS the layout and the resolved dressing to keep clear of what they placed.
//   - floor 4 (the bakery set-piece) never has one; floors 1-3 roll ChancePercent (HealTuning, 51), floor 2 included (its checkpoint room is a separate, always-full heal);
//   - the cell is one draw over the legal candidates in a fixed order (unlocked combat rooms row by row, then corridor cells row by row): a floor cell of an
//     unlocked combat room, or a straight corridor cell, that the party can reach from the entry (WalkDistances; the lock is ignored there, so the locked room and
//     the corridors of the locked link are excluded by hand), and that is not a door, a cell beside a door, a spawn, a chest, a feature, a socket cell, a vignette cell
//     or a dead end.
// No float, no hashing of strings, no set iteration for a decision. Authority: none (every peer derives the same point; the host alone runs the use and the recharge).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarHealing
    {
        public const int FirstFloor = 1, LastFloor = UndercroftPlan.Phase1LastFloor;   // plan 3 task 3: the built dive floors; the set pieces 3 and 5 are stubs and never roll one

        public static CellPos? PointFor(CellarFloorLayout layout, CellarFloorDressing dressing, HealTuning tuning)
        {
            if (layout == null) throw new System.ArgumentNullException(nameof(layout));
            if (layout.IsStub || layout.Floor < FirstFloor || layout.Floor > LastFloor) return null;
            var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.healing", (ulong)layout.Floor));
            bool has = rng.Range(0, 100) < tuning.ChancePercent;
            int pick = rng.Range(0, 1 << 20);   // always drawn, so the chance never shifts the cell
            if (!has) return null;
            List<CellPos> cands = Candidates(layout, dressing);
            if (cands.Count == 0) return null;
            return cands[pick % cands.Count];
        }

        public static CellPos? PointFor(CellarFloorLayout layout, CellarFloorDressing dressing) => PointFor(layout, dressing, HealTuning.Default);

        // Every legal cell, in the fixed order described above (public so a test can check the pick is one of them).
        public static List<CellPos> Candidates(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            int n = CellarFloorLayout.GridSize;
            var bad = new bool[n, n];
            void Ban(int x, int y) { if (x >= 0 && y >= 0 && x < n && y < n) bad[x, y] = true; }
            foreach (FloorChest c in layout.Chests) Ban(c.X, c.Y);
            if (layout.Carrier != null) Ban(layout.Carrier.X, layout.Carrier.Y);
            if (dressing != null)
                foreach (PlacedVignette v in dressing.Vignettes) Ban(v.Cell.X, v.Cell.Y);
            foreach (CellPos d in layout.DeadEnds) Ban(d.X, d.Y);
            if (layout.LockedLinkIndex >= 0)
                foreach (CellPos c in layout.Links[layout.LockedLinkIndex].Path) Ban(c.X, c.Y);
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                for (int d = 0; d < p.Room.Doors.Count; d++) { CellPos o = p.DoorOutside(d); Ban(o.X, o.Y); }
            }

            int[,] reach = layout.WalkDistances();
            var cands = new List<CellPos>();
            if (reach == null) return cands;
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                if (p.Room.Kind != RoomKind.Combat || r == layout.LockedRoomIndex) continue;
                for (int y = 0; y < p.Height; y++)
                    for (int x = 0; x < p.Width; x++)
                    {
                        if (p.Room.CellAt(x, y) != CellKind.Floor || NearDoorOrObject(p.Room, x, y)) continue;
                        CellPos w = p.Cell(x, y);
                        if (bad[w.X, w.Y] || reach[w.X, w.Y] < 0) continue;
                        cands.Add(w);
                    }
            }
            foreach (PlacedCorridor c in layout.Corridors)
            {
                if (c.Piece.Shape != CorridorShape.Straight || bad[c.X, c.Y] || reach[c.X, c.Y] < 0) continue;
                cands.Add(new CellPos(c.X, c.Y));
            }
            return cands;
        }

        static bool NearDoorOrObject(CellarRoom room, int x, int y)
        {
            foreach (CellDoor d in room.Doors) if (System.Math.Abs(d.X - x) + System.Math.Abs(d.Y - y) <= 1) return true;
            foreach (CellarSpawn s in room.Spawns) if (s.X == x && s.Y == y) return true;
            foreach (ChestSlot c in room.Chests) if (c.X == x && c.Y == y) return true;
            foreach (RoomFeature f in room.Features) if (f.X == x && f.Y == y) return true;
            foreach (DressingSocket k in room.Sockets)
                for (int o = 0; o < k.CellOptions; o++) { CellPos c = k.OptionCell(o); if (c.X == x && c.Y == y) return true; }
            return false;
        }
    }
}
