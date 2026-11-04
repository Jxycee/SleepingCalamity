// Tests for SleepingCalamity.Core.UndercroftPlan (spec 2026-11-03 section 5: the 10-floor Undercroft).
using System;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class UndercroftPlanTests
    {
        [Test]
        public void Kinds_MatchTheSignedOffFloorTable()
        {
            Assert.That(UndercroftPlan.KindOf(1), Is.EqualTo(UndercroftFloorKind.Authored));
            foreach (int f in new[] { 2, 4, 6, 7 }) Assert.That(UndercroftPlan.KindOf(f), Is.EqualTo(UndercroftFloorKind.Generated), $"floor {f}");
            foreach (int f in new[] { 3, 5, 8, 9 }) Assert.That(UndercroftPlan.KindOf(f), Is.EqualTo(UndercroftFloorKind.SetPiece), $"floor {f}");
            Assert.That(UndercroftPlan.KindOf(10), Is.EqualTo(UndercroftFloorKind.Boss));
        }

        [Test]
        public void KindOf_ThrowsOutsideTheDungeon()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UndercroftPlan.KindOf(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => UndercroftPlan.KindOf(11));
            Assert.That(UndercroftPlan.IsFloor(0), Is.False);
            Assert.That(UndercroftPlan.IsFloor(10), Is.True);
        }

        [Test]
        public void Halves_SplitAtTheBankFloor()
        {
            Assert.That(UndercroftPlan.IsFirstHalf(1), Is.True);
            Assert.That(UndercroftPlan.IsFirstHalf(5), Is.True, "the bank floor closes the first half");
            Assert.That(UndercroftPlan.IsFirstHalf(6), Is.False);
            Assert.That(UndercroftPlan.IsFirstHalf(10), Is.False);
        }

        [Test]
        public void NextFloor_StopsAtTheLastBuiltFloor()
        {
            Assert.That(UndercroftPlan.NextFloor(1, UndercroftPlan.Phase1LastFloor), Is.EqualTo(2));
            Assert.That(UndercroftPlan.NextFloor(6, UndercroftPlan.Phase1LastFloor), Is.EqualTo(7));
            Assert.That(UndercroftPlan.NextFloor(7, UndercroftPlan.Phase1LastFloor), Is.EqualTo(0), "Phase 1 ends after floor 7 (stand-in ending)");
            Assert.That(UndercroftPlan.NextFloor(9, UndercroftPlan.BossFloor), Is.EqualTo(10));
            Assert.That(UndercroftPlan.NextFloor(10, UndercroftPlan.BossFloor), Is.EqualTo(0));
            Assert.That(UndercroftPlan.NextFloor(0, UndercroftPlan.BossFloor), Is.EqualTo(0));
        }
    }
}
