// Authority: none (pure rule). The owner-side gravity-hold decision of a dive floor hand-over (DiveDirector.UpdateHold).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class HandoverHoldTests
    {
        private const float T = 10f;

        [Test] public void Key_Table()
        {
            Assert.That(HandoverHold.Key(false, 3, 3, 9u, 3, 9u), Is.EqualTo(0), "no dive");
            Assert.That(HandoverHold.Key(true, 0, 0, 0u, 0, 0u), Is.EqualTo(-1), "dive on, nothing resolved");
            Assert.That(HandoverHold.Key(true, 2, 1, 7u, 1, 7u), Is.EqualTo(2), "haul changed first");
            Assert.That(HandoverHold.Key(true, 1, 2, 7u, 1, 7u), Is.EqualTo(2), "FloorSync changed first (haul still reads the applied floor)");
            Assert.That(HandoverHold.Key(true, 1, 1, 8u, 1, 7u), Is.EqualTo(1 + HandoverHold.SeedOnlyOffset), "same floor number, new seed");
            Assert.That(HandoverHold.Key(true, 2, 2, 7u, 2, 7u), Is.EqualTo(2), "all match: the key equals applied (released)");
            Assert.That(HandoverHold.Key(true, 0, 3, 7u, 0, 0u), Is.EqualTo(3), "only FloorSync resolved");
        }

        [Test] public void FloorSyncFirst_HoldsThenReleasesWhenTeleportApplies()
        {
            var s = new HandoverHoldState();
            int k = HandoverHold.Key(true, 1, 2, 7u, 1, 7u);
            Assert.That(HandoverHold.Decide(ref s, k, 1, 0f, T), Is.True, "held from the frame FloorSync lands");
            int k2 = HandoverHold.Key(true, 2, 2, 7u, 1, 7u);   // the haul catches up: same key, same timer
            Assert.That(HandoverHold.Decide(ref s, k2, 1, 9f, T), Is.True);
            Assert.That(HandoverHold.Decide(ref s, k2, 1, 10f, T), Is.False, "10 s cap from the first change");
            Assert.That(HandoverHold.Decide(ref s, HandoverHold.Key(true, 2, 2, 7u, 2, 7u), 2, 11f, T), Is.False);
        }

        [Test] public void KeyEqualsApplied_Released()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, 2, 2, 0f, T), Is.False);
            Assert.That(s.Active, Is.False);
        }

        [Test] public void HomeAgainstAppliedHome_Released()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, 0, 0, 5f, T), Is.False);
        }

        [Test] public void UnresolvedFloor_MinusOne_Holds()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, -1, 0, 1f, T), Is.True, "the dive is on but the floor is unresolved: hold");
            Assert.That(HandoverHold.Decide(ref s, -1, 0, 5f, T), Is.True);
        }

        [Test] public void KeyChanged_RestartsTheTimer()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, 2, 1, 0f, T), Is.True);
            Assert.That(HandoverHold.Decide(ref s, 2, 1, 9.9f, T), Is.True, "same key: the original timer runs");
            Assert.That(HandoverHold.Decide(ref s, 3, 1, 9.9f, T), Is.True, "a new key restarts it");
            Assert.That(HandoverHold.Decide(ref s, 3, 1, 19.8f, T), Is.True, "9.9 s into the restarted window");
            Assert.That(HandoverHold.Decide(ref s, 3, 1, 19.9f, T), Is.False, "10 s after the restart: released");
        }

        [Test] public void Timeout_Releases_AndStaysReleasedForTheSameKey()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, 2, 1, 0f, T), Is.True);
            Assert.That(HandoverHold.Decide(ref s, 2, 1, 10f, T), Is.False, "timed out");
            Assert.That(HandoverHold.Decide(ref s, 2, 1, 50f, T), Is.False, "a lost teleport never freezes a hero again for this key");
        }

        [Test] public void TeleportApplied_ReleasesAndReArms()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, 2, 1, 0f, T), Is.True);
            Assert.That(HandoverHold.Decide(ref s, 2, 2, 1f, T), Is.False, "applied == key");
            Assert.That(HandoverHold.Decide(ref s, 3, 2, 2f, T), Is.True, "the next stair holds again, timer from 2 s");
        }

        [Test] public void DiveOffWithAppliedFloor_HoldsUntilTheHomeTeleportApplies()
        {
            var s = new HandoverHoldState();
            Assert.That(HandoverHold.Decide(ref s, 0, 3, 0f, T), Is.True, "NetActive false (key 0), applied floor 3: the hub teleport is pending");
            Assert.That(HandoverHold.Decide(ref s, 0, 3, 4f, T), Is.True);
            Assert.That(HandoverHold.Decide(ref s, 0, 0, 5f, T), Is.False, "the home teleport applied (applied 0)");
        }
    }
}
