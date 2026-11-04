// Tests/EditMode/FightSpaceTests.cs: FightSpace room-capacity rule against the Cellars pool (metrics gym plan, Task 2). Core-only.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class FightSpaceTests
    {
        [Test] public void FullParty_NeedsThirteenCells() => Assert.That(FightSpace.RequiredFloorCells(4, 8), Is.EqualTo(13));

        [Test] public void FullParty_SmallestRoomIs3x5() => Assert.That(FightSpace.SmallestRectangle(4, 8), Is.EqualTo((3, 5)));

        [Test] public void Solo_SmallestRoomIs2x3() => Assert.That(FightSpace.SmallestRectangle(1, 3), Is.EqualTo((2, 3)));

        [Test] public void PoolRoomsThatFitAFullParty()
        {
            var fit = CellarRoomPool.All.Where(r => r.Kind == RoomKind.Combat && FightSpace.Fits(r, 4, 8)).Select(r => r.Id).OrderBy(s => s);
            // eleven after rooms-v2 (pool resize); the stoke chicane is the small-party room
            Assert.That(fit, Is.EqualTo(new[] { "coal_boiler_hall", "coal_bunker", "coal_furnace_ring", "larder_cold_store", "larder_hanging_hall", "larder_pantry", "larder_smoke_gallery", "wine_aisles", "wine_cask_row", "wine_press_hall", "wine_vault_l" }));
        }

        [Test] public void EveryPoolCombatRoom_FitsSolo()
        {
            foreach (var r in CellarRoomPool.All.Where(r => r.Kind == RoomKind.Combat))
                Assert.That(FightSpace.Fits(r, 1, 3), Is.True, r.Id);
        }
    }
}
