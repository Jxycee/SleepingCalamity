// Tests for SleepingCalamity.Core.DiveRoute (the dive's start floor, stair target and go-home rule).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveRouteTests
    {
        [Test]
        public void StartFloor_IsOneUntilTheCheckpointWasReached_ThenThree()
        {
            Assert.That(DiveRoute.StartFloor(false), Is.EqualTo(1));
            Assert.That(DiveRoute.StartFloor(true), Is.EqualTo(3));
        }

        [Test]
        public void NextFloor_StairsRunOneToFour_NoneBelowTheHeartOrOutsideTheDive()
        {
            Assert.That(DiveRoute.NextFloor(1), Is.EqualTo(2));
            Assert.That(DiveRoute.NextFloor(2), Is.EqualTo(3));
            Assert.That(DiveRoute.NextFloor(3), Is.EqualTo(4));
            Assert.That(DiveRoute.NextFloor(4), Is.EqualTo(0));
            Assert.That(DiveRoute.NextFloor(0), Is.EqualTo(0));
            Assert.That(DiveRoute.NextFloor(-1), Is.EqualTo(0));
            Assert.That(DiveRoute.NextFloor(5), Is.EqualTo(0));
            Assert.That(DiveRoute.HasStair(3), Is.True);
            Assert.That(DiveRoute.HasStair(4), Is.False);
        }

        [Test]
        public void CanGoHome_OnlyFromTheHeartAfterBanking()
        {
            Assert.That(DiveRoute.CanGoHome(4, true), Is.True);
            Assert.That(DiveRoute.CanGoHome(4, false), Is.False);
            Assert.That(DiveRoute.CanGoHome(2, true), Is.False);
            Assert.That(DiveRoute.CanGoHome(0, true), Is.False);
        }
    }
}
