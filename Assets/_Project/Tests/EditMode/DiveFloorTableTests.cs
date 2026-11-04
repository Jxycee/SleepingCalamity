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

        // Catches: a collapse that never learns the walk (0 s on every floor), or a path shorter than the straight line / a target the walk cannot reach.
        [Test]
        public void PathSecondsToTarget_IsTheCellWalkOverJogSpeed_OnEveryBuiltFloor()
        {
            foreach (int floor in new[] { 1, 2, 3, 4, 5, 6, 7 })
                for (uint seed = 1; seed <= 5; seed++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                    CellPos start = l.EntryStarts[0], target = DiveFloorTable.CollapseTargetCell(l).Value;
                    float seconds = DiveFloorTable.PathSecondsToTarget(l);
                    int manhattan = System.Math.Abs(start.X - target.X) + System.Math.Abs(start.Y - target.Y);
                    string tag = " floor " + floor + " seed " + seed;
                    Assert.That(seconds, Is.GreaterThan(0f), "a real walk" + tag);
                    Assert.That(seconds, Is.GreaterThanOrEqualTo(manhattan * CellarFloorLayout.CellMeters / DiveFloorTable.CollapseWalkSpeed - 0.001f), "never shorter than the straight line" + tag);
                    Assert.That(seconds, Is.LessThan(CellarFloorLayout.GridSize * CellarFloorLayout.GridSize * CellarFloorLayout.CellMeters / DiveFloorTable.CollapseWalkSpeed), "and reachable" + tag);
                    Assert.That(DiveFloorTable.PathSecondsToTarget(l), Is.EqualTo(seconds), "deterministic" + tag);
                }
            Assert.That(DiveFloorTable.PathSecondsToTarget(null), Is.EqualTo(0f));
        }

        [Test]
        public void CollapseTimer_IsTheMinimumForAShortWalk_AndOneAndAHalfTimesALongOne()
        {
            Assert.That(DiveStir.CollapseSeconds(0f), Is.EqualTo(DiveStir.CollapseMinSeconds));
            Assert.That(DiveStir.CollapseSeconds(100f), Is.EqualTo(150f), "1.5 x a long walk");
            var stir = new DiveStir { UnbankedValuables = 30 };
            Assert.That(stir.CollapsePending, Is.True);
            stir.BeginCollapse(DiveFloorTable.PathSecondsToTarget(CellarFloorGenerator.Layout(7, 2)));
            Assert.That(stir.CollapseSecondsLeft, Is.GreaterThanOrEqualTo(DiveStir.CollapseMinSeconds));
        }
    }
}
