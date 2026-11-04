// Core/Rooms/PilotRoomsV2.cs: the one room converted to data v2, the hanging hall (larder_hanging_hall) with a balcony, a stair and a flooding sump (metrics gym plan, Task 5; spec 3.2).
// Same id, name, kind, theme, silhouette, size, door cells and spawn count as the pool's copy, which stays as it is for the generator. Migration cost shown here: one pillar and one chest
// moved to make room for the stair and the water, and the chunk region was dropped. Grid used as planned; the validator did not force a change. Authority: none (static data).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class PilotRoomsV2
    {
        const CellSide N = CellSide.North, E = CellSide.East, S = CellSide.South, W = CellSide.West;

        // Same variant names and fill chances as CellarRoomPool's private V() and Fill() (Shelf 85, Roots 60, Lantern 100).
        static readonly string[] ShelfVariants = { "Jars", "Hams", "Sacks" }, RootsVariants = { "Thin", "Thick" }, LanternVariants = { "Lit", "Dim", "Out" };

        static RoomDataV2 build;
        static IReadOnlyList<RoomDataV2> all;

        public static RoomDataV2 HangingHall => build ?? (build = BuildHangingHall());
        public static IReadOnlyList<RoomDataV2> All => all ?? (all = new[] { HangingHall });

        static RoomDataV2 BuildHangingHall()
        {
            var room = CellarRoom.Parse("larder_hanging_hall", "Hanging hall", RoomKind.Combat, SubTheme.Larder, "pillared-hall", new[] {
                "..DC",
                ".P.s",
                "Ds..",
                ".CPS" },
                new SocketSpec(SocketKind.Shelf, 0, 0, S, ShelfVariants).Chance(85),
                new SocketSpec(SocketKind.Shelf, 1, 0, S, ShelfVariants).Chance(85),
                new SocketSpec(SocketKind.Roots, 0, 1, E, RootsVariants).Chance(60),
                new SocketSpec(SocketKind.Lantern, 3, 2, W, LanternVariants).Chance(100));
            return RoomDataV2.Author(room,
                new[] { "0001", "0001", "0001", "0001" },
                new[] { "....", "..w.", "..w.", "...." },
                new[] { new GimmickRegion("sump", 'w', GimmickKind.Water, 0, 80) },
                new[] { new StairCell(2, 2, E) },
                new[] { new ToolSocket("pilot_hall.valve", 0, 3, E, SocketPurpose.Control, ToolVerb.None, "sump") },
                null, RoomAffordance.None);
        }
    }
}
