// Core/Cellars/DiveFloorTable.cs: per-floor facts the new dive needs (plan 3 task 2; spec section 5). Pure C#, static data over UndercroftPlan.
// Authority: none (static table). Phase 1 builds floors 1-7; floor 7 has no stair, the dive ends there through DiveSession.Finish.
namespace SleepingCalamity.Core
{
    public static class DiveFloorTable
    {
        public const int LastBuiltFloor = UndercroftPlan.Phase1LastFloor;

        public static UndercroftFloorKind KindOf(int floor) => UndercroftPlan.KindOf(floor);

        // Calm floors (floor-card note): the authored start, the set pieces, the bank, and the first rough floor 6 is calm too.
        public static bool IsCalmFloor(int floor) => floor == 1 || floor == 3 || floor == 5 || floor == 6 || (floor >= 8 && floor <= UndercroftPlan.BossFloor);

        public static bool HasBank(int floor) => floor == UndercroftPlan.BankFloor;

        // Floor 5's stair opens only once the party has chosen to push on; floor 7 ends the built dive and has none.
        public static bool HasStair(int floor, bool bankChosenThisDive = false)
        {
            if (UndercroftPlan.NextFloor(floor, LastBuiltFloor) == 0) return false;
            return floor != UndercroftPlan.BankFloor || bankChosenThisDive;
        }

        public const float CollapseWalkSpeed = 4.8f;   // placeholder: the jog (MovementMath.JogSpeed) a party walks the way to the target at

        // The walk from the party's start to this floor's collapse target, in seconds (DiveStir.BeginCollapse takes it): the fewest 4-neighbour steps over the floor's open room cells and
        // corridor cells (rooms are walkable inside, a room and a corridor join through an open door, the locked gate counts as a door), times CellMeters, over CollapseWalkSpeed. A layout
        // with no start or no target gives 0 (the collapse then runs its minimum); a target the cell graph cannot reach falls back to the straight Manhattan distance.
        public static float PathSecondsToTarget(CellarFloorLayout layout)
        {
            CellPos? target = CollapseTargetCell(layout);
            if (layout == null || !target.HasValue || layout.EntryStarts.Count == 0) return 0f;
            CellPos start = layout.EntryStarts[0], goal = target.Value;
            int steps = CellSteps(layout, start, goal);
            if (steps < 0) steps = System.Math.Abs(start.X - goal.X) + System.Math.Abs(start.Y - goal.Y);
            return steps * CellarFloorLayout.CellMeters / CollapseWalkSpeed;
        }

        static int CellSteps(CellarFloorLayout l, CellPos from, CellPos to)
        {
            const int G = CellarFloorLayout.GridSize;
            bool In(int x, int y) => x >= 0 && y >= 0 && x < G && y < G;
            if (!In(from.X, from.Y) || !In(to.X, to.Y)) return -1;
            var room = new int[G, G]; var corridor = new bool[G, G];
            for (int x = 0; x < G; x++) for (int y = 0; y < G; y++) room[x, y] = -1;
            for (int r = 0; r < l.Rooms.Count; r++)
            {
                PlacedRoom p = l.Rooms[r];
                for (int lx = 0; lx < p.Width; lx++)
                    for (int ly = 0; ly < p.Height; ly++)
                        if (In(p.X + lx, p.Y + ly) && p.Room.CellAt(lx, ly) == CellKind.Floor) room[p.X + lx, p.Y + ly] = r;
            }
            foreach (PlacedCorridor c in l.Corridors) if (In(c.X, c.Y)) corridor[c.X, c.Y] = true;
            var dist = new int[G, G];
            for (int x = 0; x < G; x++) for (int y = 0; y < G; y++) dist[x, y] = -1;
            var queue = new System.Collections.Generic.Queue<(int, int)>();
            dist[from.X, from.Y] = 0; queue.Enqueue((from.X, from.Y));
            var sides = new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West };
            while (queue.Count > 0)
            {
                (int cx, int cy) = queue.Dequeue();
                if (cx == to.X && cy == to.Y) return dist[cx, cy];
                foreach (CellSide s in sides)
                {
                    CellPos n = new CellPos(cx, cy).Step(s);
                    if (!In(n.X, n.Y) || dist[n.X, n.Y] >= 0) continue;
                    bool ok;
                    if (room[cx, cy] >= 0 && room[n.X, n.Y] == room[cx, cy]) ok = true;                                   // inside one room
                    else if (corridor[cx, cy] && corridor[n.X, n.Y]) ok = (l.CorridorAt(cx, cy).Mask & CellarRoom.MaskBit(s)) != 0;   // along a corridor
                    else ok = DoorBetween(l, room, cx, cy, n.X, n.Y, s);                                                  // through a door
                    if (!ok) continue;
                    dist[n.X, n.Y] = dist[cx, cy] + 1; queue.Enqueue((n.X, n.Y));
                }
            }
            return -1;
        }

        // A room cell and the cell outside it join when the room has a door on that cell and side.
        static bool DoorBetween(CellarFloorLayout l, int[,] room, int ax, int ay, int bx, int by, CellSide aToB)
        {
            CellSide bToA = (CellSide)(((int)aToB + 2) % 4);
            return HasDoor(l, room[ax, ay], ax, ay, aToB) || HasDoor(l, room[bx, by], bx, by, bToA);
        }

        static bool HasDoor(CellarFloorLayout l, int roomIndex, int x, int y, CellSide side)
        {
            if (roomIndex < 0) return false;
            PlacedRoom p = l.Rooms[roomIndex];
            foreach (CellDoor d in p.Room.Doors) if (p.X + d.X == x && p.Y + d.Y == y && d.Side == side) return true;
            return false;
        }

        // Where a collapse timer measures its path to: the stair, the bank room on floor 5, the end cell on floor 7.
        public static CellPos? CollapseTargetCell(CellarFloorLayout layout)
        {
            if (layout == null) return null;
            if (layout.Floor == UndercroftPlan.BankFloor) return layout.BankRoomCell ?? layout.StairExit;
            if (layout.Floor == LastBuiltFloor) return layout.EndCell ?? layout.StairExit;
            return layout.StairExit;
        }
    }
}
