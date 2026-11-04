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

        // Where a collapse timer measures its path to: the stair, the bank room on floor 5, the end cell on floor 7 (Task 3 adds EndCell; until then the exit feature).
        public static CellPos? CollapseTargetCell(CellarFloorLayout layout)
        {
            if (layout == null) return null;
            if (layout.Floor == UndercroftPlan.BankFloor) return layout.BankCell ?? layout.StairExit;
            return layout.StairExit;
        }
    }
}
