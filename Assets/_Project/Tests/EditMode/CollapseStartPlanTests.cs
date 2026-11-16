// Assets/_Project/Tests/EditMode/CollapseStartPlanTests.cs: tests for SleepingCalamity.Core.CollapseStartPlan (dive step 6 job 1: a slow floor build never starts the collapse with path 0).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CollapseStartPlanTests
    {
        // Catches: the old 3 s give-up (a lagging build began with path 0 = the 60 s minimum on any floor size).
        [Test]
        public void ALaggingBuild_WaitsPastTheOldThreeSeconds_UpToTheCap()
        {
            Assert.That(CollapseStartPlan.Decide(true, false, 0f), Is.EqualTo(CollapseStartAction.Wait));
            Assert.That(CollapseStartPlan.Decide(true, false, 3.5f), Is.EqualTo(CollapseStartAction.Wait));
            Assert.That(CollapseStartPlan.Decide(true, false, CollapseStartPlan.WaitCapSeconds - 0.1f), Is.EqualTo(CollapseStartAction.Wait));
            Assert.That(CollapseStartPlan.WaitCapSeconds, Is.GreaterThan(3f));
        }

        [Test]
        public void PastTheCap_ItBeginsFromTheSeedPath_NeverNoPath()
        {
            Assert.That(CollapseStartPlan.Decide(true, false, CollapseStartPlan.WaitCapSeconds), Is.EqualTo(CollapseStartAction.BeginFromSeed));
            Assert.That(CollapseStartPlan.Decide(true, false, 999f), Is.EqualTo(CollapseStartAction.BeginFromSeed));
        }

        [Test]
        public void ABuiltFloor_BeginsAtOnce_AndNoFloorSyncBeginsWithNoPath()
        {
            Assert.That(CollapseStartPlan.Decide(true, true, 0f), Is.EqualTo(CollapseStartAction.BeginBuilt));
            Assert.That(CollapseStartPlan.Decide(false, false, 0f), Is.EqualTo(CollapseStartAction.BeginNoPath));
        }

        [Test]
        public void FallbackPath_IsTheBuiltLayoutsPath_AndRealOnEveryFloor()
        {
            foreach (int floor in new[] { 1, 2, 3, 4, 5, 6, 7 })
                for (uint seed = 1; seed <= 5; seed++)
                {
                    float fallback = CollapseStartPlan.FallbackPathSeconds(seed, floor);
                    Assert.That(fallback, Is.GreaterThan(0f), "floor " + floor + " seed " + seed);
                    Assert.That(fallback, Is.EqualTo(DiveFloorTable.PathSecondsToTarget(CellarFloorGenerator.Layout(seed, floor))), "same as the built layout's path");
                }
            Assert.That(CollapseStartPlan.FallbackPathSeconds(1, 0), Is.EqualTo(0f));
        }
    }
}
