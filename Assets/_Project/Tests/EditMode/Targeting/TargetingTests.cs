// Authority: none (pure EditMode tests). SleepingCalamity.Core targeting: soft target scoring, hard-lock hysteresis, line-of-sight grace,
// flick switching and the elite rule (docs/PHASE1.md 6.1 and 6.5). Targets sit relative to a seeker at the origin facing +Z.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class TargetingTests
    {
        private static readonly TargetSeeker AtOrigin = new TargetSeeker(0f, 0f, 0f, 0f, 1f);

        // A target `distance` metres away, `angleDeg` to the right of straight ahead (+Z).
        private static TargetCandidate At(int id, float distance, float angleDeg = 0f, float dy = 0f, bool elite = false, bool sight = true, bool alive = true)
        {
            float a = angleDeg * (MathF.PI / 180f);
            return new TargetCandidate(id, distance * MathF.Sin(a), dy, distance * MathF.Cos(a), alive, elite, sight);
        }

        private static List<TargetCandidate> List(params TargetCandidate[] c) => new List<TargetCandidate>(c);

        // ---- soft target ----

        // Catches: a cone that is wider or narrower than 60 degrees half-angle, or one-sided.
        [Test]
        public void Soft_ConeEdges_SixtyDegreesHalfAngleBothSides()
        {
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 4f, 59f)), Is.True, "59 right");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 4f, 61f)), Is.False, "61 right");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 4f, -59f)), Is.True, "59 left");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 4f, -61f)), Is.False, "61 left");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 4f, 180f)), Is.False, "directly behind");
        }

        [Test]
        public void Soft_RangeEdge_SixMetres()
        {
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 5.99f)), Is.True);
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 6.0f)), Is.True, "exactly 6 m is inside");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 6.01f)), Is.False);
        }

        [Test]
        public void Soft_HeightBand_PlusMinusTwoMetres()
        {
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 3f, dy: 1.99f)), Is.True, "just under +2 m");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 3f, dy: 2.01f)), Is.False, "just over +2 m");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 3f, dy: -1.99f)), Is.True, "just over -2 m");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 3f, dy: -2.01f)), Is.False, "just under -2 m");
        }

        [Test]
        public void Soft_DeadOrBlockedTargetsAreNeverPicked()
        {
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 3f, alive: false)), Is.False, "dead");
            Assert.That(SoftTargeting.InSoftZone(AtOrigin, At(1, 3f, sight: false)), Is.False, "behind a wall");
            Assert.That(SoftTargeting.PickSoft(AtOrigin, List(At(1, 3f, alive: false), At(2, 3f, sight: false))), Is.EqualTo(SoftTargeting.NoTarget));
        }

        // Catches: facing carrying no weight (the nearest target winning even when you are looking at another), or distance carrying none.
        [Test]
        public void Soft_Pick_FacingOutweighsNearness_ButNearnessBreaksTies()
        {
            // Straight ahead at 5 m beats a closer target 50 degrees off to the side.
            var list = List(At(10, 2f, 50f), At(20, 5f, 0f));
            Assert.That(list[SoftTargeting.PickSoft(AtOrigin, list)].Id, Is.EqualTo(20));

            // Equal angle: the closer one wins.
            list = List(At(30, 5f, 20f), At(40, 2.5f, -20f));
            Assert.That(list[SoftTargeting.PickSoft(AtOrigin, list)].Id, Is.EqualTo(40));
        }

        [Test]
        public void Soft_NoFacing_StillPicksByDistance()
        {
            var noFacing = new TargetSeeker(0f, 0f, 0f, 0f, 0f);
            var list = List(At(1, 5f, 90f), At(2, 2f, 170f));
            Assert.That(list[SoftTargeting.PickSoft(noFacing, list)].Id, Is.EqualTo(2));
        }

        // ---- hard lock: acquire and hysteresis ----

        [Test]
        public void Lock_Acquire_TenMetres()
        {
            var near = new TargetLock();
            Assert.That(near.TryAcquire(AtOrigin, List(At(1, 9.9f))), Is.True);
            Assert.That(near.TargetId, Is.EqualTo(1));

            var far = new TargetLock();
            Assert.That(far.TryAcquire(AtOrigin, List(At(1, 10.1f))), Is.False);
            Assert.That(far.IsLocked, Is.False);

            var between = new TargetLock();
            Assert.That(between.TryAcquire(AtOrigin, List(At(1, 11.5f))), Is.False, "11.5 m is inside the release range but outside the acquire range");
        }

        [Test]
        public void Lock_Acquire_PrefersWhatYouFace_AndTakesOneBehindWhenNothingElse()
        {
            var lockOn = new TargetLock();
            Assert.That(lockOn.TryAcquire(AtOrigin, List(At(1, 3f, 140f), At(2, 8f, 10f))), Is.True);
            Assert.That(lockOn.TargetId, Is.EqualTo(2), "facing wins over nearness");

            var behind = new TargetLock();
            Assert.That(behind.TryAcquire(AtOrigin, List(At(1, 4f, 180f))), Is.True, "a lone target behind you can still be locked");
        }

        [Test]
        public void Lock_Acquire_RejectsDeadBlockedAndOtherFloors()
        {
            var lockOn = new TargetLock();
            Assert.That(lockOn.TryAcquire(AtOrigin, List(At(1, 4f, alive: false), At(2, 4f, sight: false), At(3, 4f, dy: 4.5f))), Is.False);
        }

        // Catches: acquire and release sharing one distance (the lock flickering at the edge). Acquire 10 m, hold to 13 m.
        [Test]
        public void Lock_Hysteresis_AcquireAtTen_HoldsToThirteen()
        {
            var lockOn = new TargetLock();
            Assert.That(lockOn.TryAcquire(AtOrigin, List(At(1, 9.9f))), Is.True);

            Assert.That(lockOn.Tick(0.016f, AtOrigin, List(At(1, 11.5f))), Is.EqualTo(LockDrop.None), "11.5 m: kept");
            Assert.That(lockOn.Tick(0.016f, AtOrigin, List(At(1, 12.99f))), Is.EqualTo(LockDrop.None), "12.99 m: kept");
            Assert.That(lockOn.IsLocked, Is.True);

            Assert.That(lockOn.Tick(0.016f, AtOrigin, List(At(1, 13.01f))), Is.EqualTo(LockDrop.OutOfRange), "13.01 m: dropped");
            Assert.That(lockOn.IsLocked, Is.False);
            Assert.That(lockOn.LastDrop, Is.EqualTo(LockDrop.OutOfRange));
        }

        // ---- line-of-sight grace ----

        [Test]
        public void Lock_LosGrace_DropsAfterOneSecondWithoutSight()
        {
            var lockOn = new TargetLock();
            lockOn.TryAcquire(AtOrigin, List(At(1, 5f)));
            List<TargetCandidate> blocked = List(At(1, 5f, sight: false));

            Assert.That(lockOn.Tick(0.25f, AtOrigin, blocked), Is.EqualTo(LockDrop.None));
            Assert.That(lockOn.Tick(0.25f, AtOrigin, blocked), Is.EqualTo(LockDrop.None));
            Assert.That(lockOn.Tick(0.25f, AtOrigin, blocked), Is.EqualTo(LockDrop.None), "0.75 s: still held");
            Assert.That(lockOn.Tick(0.25f, AtOrigin, blocked), Is.EqualTo(LockDrop.LostSight), "1.0 s: gone");
            Assert.That(lockOn.IsLocked, Is.False);
        }

        // Catches: a grace timer that never resets (three short blocked moments adding up to a drop).
        [Test]
        public void Lock_LosGrace_RegainingSightResetsTheTimer()
        {
            var lockOn = new TargetLock();
            lockOn.TryAcquire(AtOrigin, List(At(1, 5f)));
            List<TargetCandidate> blocked = List(At(1, 5f, sight: false));
            List<TargetCandidate> clear = List(At(1, 5f));

            for (int round = 0; round < 3; round++)
            {
                Assert.That(lockOn.Tick(0.75f, AtOrigin, blocked), Is.EqualTo(LockDrop.None), "round " + round);
                Assert.That(lockOn.Tick(0.016f, AtOrigin, clear), Is.EqualTo(LockDrop.None));
                Assert.That(lockOn.SecondsWithoutSight, Is.EqualTo(0f));
            }
            Assert.That(lockOn.IsLocked, Is.True);
        }

        // ---- elite / boss ----

        // Catches: an elite lock that never drops (alpha playtest item 8). It holds past the normal release range and sight grace, but ends at the elite ones.
        [Test]
        public void Lock_Elite_HoldsLongerButDrops()
        {
            var lockOn = new TargetLock();
            Assert.That(lockOn.TryAcquire(AtOrigin, List(At(1, 9f, elite: true))), Is.True);

            Assert.That(lockOn.Tick(0.016f, AtOrigin, List(At(1, 20f, elite: true))), Is.EqualTo(LockDrop.None), "20 m: past the normal 13 m, inside the elite range");
            Assert.That(lockOn.Tick(2f, AtOrigin, List(At(1, 20f, elite: true, sight: false))), Is.EqualTo(LockDrop.None), "2 s behind a wall: past the normal 1 s grace");
            Assert.That(lockOn.Tick(0.016f, AtOrigin, List(At(1, 20f, elite: true))), Is.EqualTo(LockDrop.None));
            Assert.That(lockOn.Tick(0.016f, AtOrigin, List(At(1, 40f, elite: true))), Is.EqualTo(LockDrop.OutOfRange), "40 m away");
            Assert.That(lockOn.IsLocked, Is.False);

            var walled = new TargetLock();
            walled.TryAcquire(AtOrigin, List(At(1, 9f, elite: true)));
            LockDrop drop = LockDrop.None;
            for (int i = 0; i < 20 && drop == LockDrop.None; i++) drop = walled.Tick(0.5f, AtOrigin, List(At(1, 9f, elite: true, sight: false)));
            Assert.That(drop, Is.EqualTo(LockDrop.LostSight), "10 s behind a wall no longer holds");

            lockOn = new TargetLock();
            lockOn.TryAcquire(AtOrigin, List(At(1, 9f, elite: true)));
            lockOn.Release();
            Assert.That(lockOn.IsLocked, Is.False, "the player's toggle still releases an elite");
            Assert.That(lockOn.LastDrop, Is.EqualTo(LockDrop.Released));
        }

        [Test]
        public void Lock_Elite_StillDropsWhenDeadOrGone()
        {
            var dead = new TargetLock();
            dead.TryAcquire(AtOrigin, List(At(1, 5f, elite: true)));
            Assert.That(dead.Tick(0.016f, AtOrigin, List(At(1, 5f, elite: true, alive: false))), Is.EqualTo(LockDrop.Dead));

            var gone = new TargetLock();
            gone.TryAcquire(AtOrigin, List(At(1, 5f, elite: true)));
            Assert.That(gone.Tick(0.016f, AtOrigin, List(At(2, 5f))), Is.EqualTo(LockDrop.Missing));
        }

        [Test]
        public void Lock_Tick_WhenNotLocked_DoesNothing()
        {
            var lockOn = new TargetLock();
            Assert.That(lockOn.Tick(1f, AtOrigin, List(At(1, 5f))), Is.EqualTo(LockDrop.None));
            Assert.That(lockOn.IsLocked, Is.False);
        }

        // ---- flick switch ----

        // Catches: a flick that switches the wrong way round, or ignores which side of the current target the candidate is on.
        [Test]
        public void Flick_PicksTheTargetInTheFlickDirection()
        {
            // Locked on the middle one (id 2, straight ahead at 6 m); id 1 is to its left, id 3 to its right.
            List<TargetCandidate> all = List(new TargetCandidate(1, -4f, 0f, 6f), new TargetCandidate(2, 0f, 0f, 6f), new TargetCandidate(3, 4f, 0f, 6f));
            var lockOn = new TargetLock();
            lockOn.TryAcquire(AtOrigin, List(all[1]));
            Assert.That(lockOn.TargetId, Is.EqualTo(2));

            Assert.That(lockOn.TrySwitch(AtOrigin, 1f, 0f, all), Is.True, "flick right (+X)");
            Assert.That(lockOn.TargetId, Is.EqualTo(3));
            Assert.That(lockOn.TrySwitch(AtOrigin, -1f, 0f, all), Is.True, "flick left (-X): back through the middle? no, the nearest to the left of 3 is 2");
            Assert.That(lockOn.TargetId, Is.EqualTo(2));
            Assert.That(lockOn.TrySwitch(AtOrigin, -1f, 0f, all), Is.True);
            Assert.That(lockOn.TargetId, Is.EqualTo(1));
        }

        [Test]
        public void Flick_TakesTheNearestInThatDirection_AndKeepsTheLockWhenNothingIsThere()
        {
            List<TargetCandidate> all = List(new TargetCandidate(1, 0f, 0f, 6f), new TargetCandidate(2, 7f, 0f, 6f), new TargetCandidate(3, 3f, 0f, 6f));
            var lockOn = new TargetLock();
            lockOn.TryAcquire(AtOrigin, List(all[0]));

            Assert.That(lockOn.TrySwitch(AtOrigin, 1f, 0f, all), Is.True);
            Assert.That(lockOn.TargetId, Is.EqualTo(3), "3 m to the right beats 7 m to the right");

            Assert.That(lockOn.TrySwitch(AtOrigin, 0f, 1f, all), Is.False, "nothing further ahead");
            Assert.That(lockOn.TargetId, Is.EqualTo(3), "the lock is kept");
        }

        [Test]
        public void Flick_SkipsDeadBlockedAndOutOfReachTargets_AndNeedsALock()
        {
            List<TargetCandidate> all = List(
                new TargetCandidate(1, 0f, 0f, 6f),
                new TargetCandidate(2, 3f, 0f, 6f, alive: false),
                new TargetCandidate(3, 4f, 0f, 6f, hasLineOfSight: false),
                new TargetCandidate(4, 6f, 0f, 20f),
                new TargetCandidate(5, 7f, 0f, 6f));
            var lockOn = new TargetLock();
            Assert.That(lockOn.TrySwitch(AtOrigin, 1f, 0f, all), Is.False, "not locked: a flick does nothing");

            lockOn.TryAcquire(AtOrigin, List(all[0]));
            Assert.That(lockOn.TrySwitch(AtOrigin, 1f, 0f, all), Is.True);
            Assert.That(lockOn.TargetId, Is.EqualTo(5), "dead, blocked and 20 m away are skipped");
        }

        [Test]
        public void FlickDetector_FiresOncePerPush_AndRearmsAtRest()
        {
            var flick = new FlickDetector();
            float x, y;
            Assert.That(flick.Update(0.9f, 0f, out x, out y), Is.False, "a stick already pushed at the start does not fire");
            Assert.That(flick.Update(0f, 0f, out x, out y), Is.False);

            Assert.That(flick.Update(0.5f, 0f, out x, out y), Is.False, "below the on threshold");
            Assert.That(flick.Update(0.9f, 0.1f, out x, out y), Is.True, "slow push fires when it passes 0.75");
            Assert.That(x, Is.GreaterThan(0.9f));
            Assert.That(flick.Update(1f, 0f, out x, out y), Is.False, "held: no repeat");
            Assert.That(flick.Update(0.5f, 0f, out x, out y), Is.False, "still above the off threshold: not re-armed");
            Assert.That(flick.Update(1f, 0f, out x, out y), Is.False);

            Assert.That(flick.Update(0.1f, 0f, out x, out y), Is.False, "back to rest re-arms");
            Assert.That(flick.Update(0f, -1f, out x, out y), Is.True, "a down flick");
            Assert.That(y, Is.LessThan(-0.99f));
        }

        [Test]
        public void FlickInput_ToWorld_FollowsTheCameraYaw()
        {
            float x, z;
            FlickInput.ToWorld(0f, 1f, 0f, out x, out z);
            Assert.That(x, Is.EqualTo(1f).Within(1e-5f), "camera looks +Z: stick right = +X");
            Assert.That(z, Is.EqualTo(0f).Within(1e-5f));

            FlickInput.ToWorld(90f, 1f, 0f, out x, out z);
            Assert.That(x, Is.EqualTo(0f).Within(1e-5f), "camera looks +X: stick right = -Z");
            Assert.That(z, Is.EqualTo(-1f).Within(1e-5f));

            FlickInput.ToWorld(90f, 0f, 1f, out x, out z);
            Assert.That(x, Is.EqualTo(1f).Within(1e-5f), "camera looks +X: stick up = +X");
            Assert.That(z, Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
