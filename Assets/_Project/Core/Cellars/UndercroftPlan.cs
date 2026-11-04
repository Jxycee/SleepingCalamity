// Core/Cellars/UndercroftPlan.cs: the Undercroft's floor table (spec 2026-11-03 section 5; decisions 2, 26, 28). Pure C#.
// Floor 1 authored, 2/4/6/7 generated, 3/5/8/9 set pieces, 10 the boss. The bank closes the first half on floor 5. Phase 1 builds to floor 7.
using System;

namespace SleepingCalamity.Core
{
    public enum UndercroftFloorKind { Authored, Generated, SetPiece, Boss }

    public static class UndercroftPlan
    {
        public const int FirstFloor = 1, BankFloor = 5, BossFloor = 10, Phase1LastFloor = 7;

        public static bool IsFloor(int floor) => floor >= FirstFloor && floor <= BossFloor;

        public static UndercroftFloorKind KindOf(int floor) => floor switch
        {
            1 => UndercroftFloorKind.Authored,
            2 or 4 or 6 or 7 => UndercroftFloorKind.Generated,
            3 or 5 or 8 or 9 => UndercroftFloorKind.SetPiece,
            10 => UndercroftFloorKind.Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(floor), floor, "the Undercroft has floors 1-10"),
        };

        public static bool IsFirstHalf(int floor) => IsFloor(floor) && floor <= BankFloor;

        // The floor the stair leads to, or 0 at the end of the built dive (the boss floor, or the Phase 1 stand-in ending).
        public static int NextFloor(int floor, int lastBuiltFloor) => IsFloor(floor) && floor < lastBuiltFloor ? floor + 1 : 0;
    }
}
