// Tests/EditMode/RoomDataV2Tests.cs: room data v2 contract, derived affordances and enemy habitat (metrics gym plan, Task 3). Core-only.
using System;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class RoomDataV2Tests
    {
        static CellarRoom Grid3() => CellarRoom.Parse("hand_3x3", "Hand", RoomKind.Combat, SubTheme.Larder, "hall", new[] { "...", "...", "..." });

        static GimmickRegion Water(params int[] values) => new GimmickRegion("w", 'w', GimmickKind.Water, values);

        [Test] public void FromV1_KeepsBaseAndMarksTheLockedDoor()
        {
            CellarRoom cold = CellarRoomPool.All.First(r => r.Doors.Any(d => d.Locked));
            RoomDataV2 v2 = RoomDataV2.FromV1(cold);
            Assert.That(v2.Version, Is.EqualTo(2));
            Assert.That(v2.Base, Is.SameAs(cold));
            Assert.That(v2.Doors.Count, Is.EqualTo(cold.Doors.Count));
            for (int i = 0; i < cold.Doors.Count; i++)
            {
                Assert.That(v2.Doors[i].DoorIndex, Is.EqualTo(i));
                Assert.That(v2.Doors[i].Type, Is.EqualTo(cold.Doors[i].Locked ? DoorType.Locked : DoorType.Plain));
                Assert.That(v2.Doors[i].Level, Is.EqualTo(0));
            }
            for (int x = 0; x < cold.Width; x++) for (int y = 0; y < cold.Height; y++) Assert.That(v2.LevelAt(x, y), Is.EqualTo(0));
            Assert.That(v2.Regions, Is.Empty); Assert.That(v2.Stairs, Is.Empty); Assert.That(v2.Sockets, Is.Empty);
        }

        [Test] public void Author_ParsesLevelsAndRegions()
        {
            RoomDataV2 r = RoomDataV2.Author(Grid3(), new[] { "001", "000", "000" }, new[] { ".ww", "...", "..." }, new[] { Water(0, 80) }, null, null, null, RoomAffordance.None);
            Assert.That(r.LevelAt(2, 0), Is.EqualTo(1)); Assert.That(r.LevelAt(0, 0), Is.EqualTo(0));
            Assert.That(r.RegionAt(1, 0).Id, Is.EqualTo("w")); Assert.That(r.RegionAt(0, 0), Is.Null);
            Assert.That(r.Regions[0].Cells.Select(c => (c.X, c.Y)), Is.EqualTo(new[] { (1, 0), (2, 0) }));
            Assert.That(r.DepthCm(1, 0, new[] { 1 }), Is.EqualTo(80));
            Assert.That(r.DepthCm(2, 0, new[] { 1 }), Is.LessThanOrEqualTo(0));   // 80 - 200 on the level-1 cell
            Assert.That(r.DepthCm(1, 0, new[] { 0 }), Is.EqualTo(0));
            Assert.That(r.DepthCm(0, 0, new[] { 1 }), Is.EqualTo(0));            // outside the region
        }

        [Test] public void Standable_WadeVsDeep()
        {
            RoomDataV2 r = RoomDataV2.Author(Grid3(), null, new[] { "w..", "...", "..." }, new[] { Water(50, 51) }, null, null, null, RoomAffordance.None);
            Assert.That(r.Standable(0, 0, new[] { 0 }), Is.True);    // 50 cm: wade
            Assert.That(r.Standable(0, 0, new[] { 1 }), Is.False);   // 51 cm: deep
            Assert.That(r.Standable(1, 1, new[] { 1 }), Is.True);
        }

        [Test] public void Standable_IsFalseOnPillarsAndSolids()
        {
            CellarRoom b = CellarRoom.Parse("p", "P", RoomKind.Combat, SubTheme.Larder, "hall", new[] { "P#.", "...", "..." });
            RoomDataV2 r = RoomDataV2.FromV1(b);
            Assert.That(r.Standable(0, 0, new int[0]), Is.False); Assert.That(r.Standable(1, 0, new int[0]), Is.False); Assert.That(r.Standable(2, 0, new int[0]), Is.True);
        }

        [Test] public void Heat_BurningBlocks()
        {
            var heat = new GimmickRegion("h", 'h', GimmickKind.Heat, 0, 1);
            RoomDataV2 r = RoomDataV2.Author(Grid3(), null, new[] { "h..", "...", "..." }, new[] { heat }, null, null, null, RoomAffordance.None);
            Assert.That(r.Burning(0, 0, new[] { 1 }), Is.True); Assert.That(r.Standable(0, 0, new[] { 1 }), Is.False);
            Assert.That(r.Burning(0, 0, new[] { 0 }), Is.False); Assert.That(r.Standable(0, 0, new[] { 0 }), Is.True);
            Assert.That(r.DepthCm(0, 0, new[] { 1 }), Is.EqualTo(0));
        }

        [Test] public void Author_ThrowsOnBadGrid()
        {
            var w = new[] { Water(0, 80) };
            Assert.Throws<ArgumentException>(() => RoomDataV2.Author(Grid3(), new[] { "00", "000", "000" }, null, w, null, null, null, RoomAffordance.None));   // short row
            Assert.Throws<ArgumentException>(() => RoomDataV2.Author(Grid3(), new[] { "000", "000" }, null, w, null, null, null, RoomAffordance.None));         // wrong row count
            Assert.Throws<ArgumentException>(() => RoomDataV2.Author(Grid3(), null, new[] { "q..", "...", "..." }, w, null, null, null, RoomAffordance.None));   // unknown letter
            Assert.Throws<ArgumentException>(() => RoomDataV2.Author(Grid3(), new[] { "0x0", "000", "000" }, null, w, null, null, null, RoomAffordance.None));   // unknown character
        }

        [Test] public void Affordances_AreDerived()
        {
            RoomDataV2 hand = RoomDataV2.Author(Grid3(), new[] { "001", "000", "000" }, new[] { ".ww", "...", "..." }, new[] { Water(0, 80) }, null, null, null, RoomAffordance.HighCeiling);
            Assert.That(hand.Affordances & RoomAffordance.Water, Is.EqualTo(RoomAffordance.Water));
            Assert.That(hand.Affordances & RoomAffordance.Ledge, Is.EqualTo(RoomAffordance.Ledge));
            Assert.That(hand.Affordances & RoomAffordance.HighCeiling, Is.EqualTo(RoomAffordance.HighCeiling));
            Assert.That(hand.Affordances & RoomAffordance.OpenFloor, Is.EqualTo(RoomAffordance.None), "the only 3x3 window holds a ledge and water");
            Assert.That(RoomDataV2.FromV1(Grid3()).Affordances & RoomAffordance.OpenFloor, Is.EqualTo(RoomAffordance.OpenFloor));
            Assert.That(RoomDataV2.FromV1(CellarRoomPool.Find("wine_press_hall")).Affordances & RoomAffordance.FullParty, Is.EqualTo(RoomAffordance.FullParty));
            Assert.That(RoomDataV2.FromV1(CellarRoomPool.Find("larder_pantry")).Affordances & RoomAffordance.FullParty, Is.EqualTo(RoomAffordance.None));
        }

        [Test] public void ToolVerbs_AreFrozen() =>
            Assert.That(Enum.GetNames(typeof(ToolVerb)), Is.EqualTo(new[] { "None", "Sluice", "Bellows", "Blast", "Smoke", "Star", "Tide" }));

        [Test] public void EnemyHabitat_WardenNeedsOpenFloor()
        {
            Assert.That(EnemyHabitat.Needs(SleepingCalamity.Core.Combat.EnemyKind.DoughWarden), Is.EqualTo(RoomAffordance.OpenFloor));
            foreach (SleepingCalamity.Core.Combat.EnemyKind k in new[] { SleepingCalamity.Core.Combat.EnemyKind.Slime, SleepingCalamity.Core.Combat.EnemyKind.RatKing, SleepingCalamity.Core.Combat.EnemyKind.Broom }) Assert.That(EnemyHabitat.Needs(k), Is.EqualTo(RoomAffordance.None));
            RoomDataV2 open = RoomDataV2.FromV1(Grid3());
            RoomDataV2 ledge = RoomDataV2.Author(Grid3(), new[] { "001", "000", "000" }, null, null, null, null, null, RoomAffordance.None);
            Assert.That(EnemyHabitat.Fits(SleepingCalamity.Core.Combat.EnemyKind.DoughWarden, open), Is.True);
            Assert.That(EnemyHabitat.Fits(SleepingCalamity.Core.Combat.EnemyKind.DoughWarden, ledge), Is.False);
            Assert.That(EnemyHabitat.Fits(SleepingCalamity.Core.Combat.EnemyKind.Slime, ledge), Is.True);
        }
    }
}
