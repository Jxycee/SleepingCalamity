// Assets/_Project/Tests/EditMode/DiveFloorTableTests.cs: tests for SleepingCalamity.Core.DiveFloorTable (plan 3 task 2).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveFloorTableTests
    {
        [Test]
        public void KindOf_MatchesTheUndercroftPlan()
        {
            for (int f = 1; f <= UndercroftPlan.BossFloor; f++) Assert.That(DiveFloorTable.KindOf(f), Is.EqualTo(UndercroftPlan.KindOf(f)), "floor " + f);
            Assert.That(DiveFloorTable.LastBuiltFloor, Is.EqualTo(UndercroftPlan.Phase1LastFloor));
        }

        [Test]
        public void CalmFloors_AreOneThreeFiveSixAndEightToTen()
        {
            var calm = new[] { 1, 3, 5, 6, 8, 9, 10 };
            for (int f = 1; f <= 10; f++) Assert.That(DiveFloorTable.IsCalmFloor(f), Is.EqualTo(System.Array.IndexOf(calm, f) >= 0), "floor " + f);
        }

        [Test]
        public void Bank_IsFloor5Only()
        {
            for (int f = 1; f <= 10; f++) Assert.That(DiveFloorTable.HasBank(f), Is.EqualTo(f == 5), "floor " + f);
        }

        [Test]
        public void Stairs_OnOneToFourAndSix_FloorFiveOnlyAfterTheBankChoice_NeverSeven()
        {
            foreach (int f in new[] { 1, 2, 3, 4, 6 }) Assert.That(DiveFloorTable.HasStair(f), Is.True, "floor " + f);
            Assert.That(DiveFloorTable.HasStair(5), Is.False, "before the bank choice");
            Assert.That(DiveFloorTable.HasStair(5, true), Is.True);
            Assert.That(DiveFloorTable.HasStair(7), Is.False);
            Assert.That(DiveFloorTable.HasStair(7, true), Is.False);
        }

        [Test]
        public void CollapseTargetCell_NullLayout_IsNull()
        {
            Assert.That(DiveFloorTable.CollapseTargetCell(null), Is.Null);
        }
    }
}
