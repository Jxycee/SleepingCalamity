// Catches: the dodge i-frames (frames 5-18 of 30) and the 100 ms owner leniency wrong, the guard arc/parry window/damage factor drifting from P46, a
// guard-breaker passing or not, hitbox geometry (cone angle, fat targets, height band), and the hitstop cap (12 frames per 0.5 s).
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class GuardDodgeTests
    {
        // ---- dodge ----

        [Test]
        public void IFrames_AreFramesFiveToEighteen_PlusLeniency()
        {
            Assert.That(DodgeTiming.IsInIFrames(true, 4f / 60f), Is.False);
            Assert.That(DodgeTiming.IsInIFrames(true, 5f / 60f), Is.True);
            Assert.That(DodgeTiming.IsInIFrames(true, 18f / 60f), Is.True);
            Assert.That(DodgeTiming.IsInIFrames(true, 19f / 60f), Is.False);

            // 100 ms of leniency either side: safe from the first moment, and until 0.4 s.
            Assert.That(DodgeTiming.IsInvulnerable(true, 0.0f), Is.True, "5 frames (83 ms) of start-up is inside the 100 ms leniency");
            Assert.That(DodgeTiming.IsInvulnerable(true, 0.39f), Is.True);
            Assert.That(DodgeTiming.IsInvulnerable(true, 0.41f), Is.False, "the end of the window plus 100 ms");
            Assert.That(DodgeTiming.IsInvulnerable(false, 0.2f), Is.False, "no roll, no invulnerability");
        }

        [Test]
        public void DodgeState_ReportsInvulnerabilityAcrossTheRoll()
        {
            var d = new DodgeState();
            Assert.That(d.Invulnerable, Is.False);
            Assert.That(d.TryStart(1f, 0f), Is.True);
            Assert.That(d.Invulnerable, Is.True, "leniency covers the first instant");
            for (int i = 0; i < 25; i++) d.Tick(1f / 60f);   // 0.417 s
            Assert.That(d.Invulnerable, Is.False, "the window closed");
            Assert.That(d.Active, Is.True, "but the roll goes on to half a second");
            for (int i = 0; i < 10; i++) d.Tick(1f / 60f);
            Assert.That(d.Active, Is.False);
            Assert.That(d.Invulnerable, Is.False);
            Assert.That(MovementMath.DodgeDuration, Is.EqualTo(DodgeTiming.TotalFrames / 60f).Within(1e-6f), "30 frames at 60 Hz = the 0.5 s roll");
        }

        // ---- guard ----

        [Test]
        public void Guard_FrontalArc_IsOneHundredTwentyDegrees()
        {
            Assert.That(GuardMath.Resolve(true, 0.5f, 59f, false, true), Is.EqualTo(GuardOutcome.Guarded));
            Assert.That(GuardMath.Resolve(true, 0.5f, -59f, false, true), Is.EqualTo(GuardOutcome.Guarded));
            Assert.That(GuardMath.Resolve(true, 0.5f, 61f, false, true), Is.EqualTo(GuardOutcome.None));
            Assert.That(GuardMath.Resolve(true, 0.5f, 180f, false, true), Is.EqualTo(GuardOutcome.None));
            Assert.That(GuardMath.Resolve(false, 0.5f, 0f, false, true), Is.EqualTo(GuardOutcome.None));
        }

        [Test]
        public void Guard_FirstSixFramesParry()
        {
            Assert.That(GuardMath.Resolve(true, 0f, 0f, false, true), Is.EqualTo(GuardOutcome.Parried));
            Assert.That(GuardMath.Resolve(true, 6f / 60f, 0f, false, true), Is.EqualTo(GuardOutcome.Parried));
            Assert.That(GuardMath.Resolve(true, 7f / 60f, 0f, false, true), Is.EqualTo(GuardOutcome.Guarded));
        }

        [Test]
        public void Guard_BreakersAndUnguardables_PassThrough()
        {
            Assert.That(GuardMath.Resolve(true, 0.5f, 0f, true, true), Is.EqualTo(GuardOutcome.None), "guard-break");
            Assert.That(GuardMath.Resolve(true, 0.5f, 0f, false, false), Is.EqualTo(GuardOutcome.None), "unguardable");
            Assert.That(AttackCatalog.WardenSlam.GuardBreak, Is.True, "the Warden's slam breaks guards");
            Assert.That(AttackCatalog.Axe.Attacks[AttackSet.Finisher].GuardBreak, Is.True, "so does the axe finisher (vs the broom)");
            Assert.That(GuardMath.GuardedDamage, Is.EqualTo(0.2f));
        }

        [Test]
        public void GuardedHit_StillChipsForAtLeastOne()
        {
            var i = DamageInput.Basic(12f, 1f, 10f);
            i.Other = GuardMath.GuardedDamage;
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(3));
            i.Atk = 1f;
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(1));
        }

        // ---- hitbox geometry ----

        [Test]
        public void Cone_ReachAndAngle()
        {
            // The axe's first swing: 2.3 m, half angle 55. Facing +Z.
            Assert.That(HitGeometry.InCone(0, 0, 0, 0, 2f, 0.45f, 2.3f, 55f), Is.True);
            Assert.That(HitGeometry.InCone(0, 0, 0, 0, 2.7f, 0.45f, 2.3f, 55f), Is.True, "a fat target's near edge is inside the reach");
            Assert.That(HitGeometry.InCone(0, 0, 0, 0, 3.0f, 0.45f, 2.3f, 55f), Is.False);
            Assert.That(HitGeometry.InCone(0, 0, 0, 2f, 0.2f, 0.45f, 2.3f, 55f), Is.False, "beside it, 84 degrees off");
            Assert.That(HitGeometry.InCone(0, 0, 0, 0, -1f, 0.45f, 2.3f, 55f), Is.False, "behind");
            Assert.That(HitGeometry.InCone(0, 0, 90f, 2f, 0f, 0.45f, 2.3f, 55f), Is.True, "yaw 90 faces +X");
        }

        [Test]
        public void Circle_And_HeightBand()
        {
            var staffSpin = AttackCatalog.Staff.Attacks[AttackSet.Finisher];
            Assert.That(HitGeometry.Hits(staffSpin, 0, 0, 0, 0, 0, 0, -3f, 0.5f), Is.True, "a spin reaches behind");
            Assert.That(HitGeometry.Hits(staffSpin, 0, 0, 0, 0, 0, 0, -4.2f, 0.5f), Is.False);
            Assert.That(HitGeometry.Hits(staffSpin, 0, 0, 0, 0, 0, 3.5f, -1f, 0.5f), Is.False, "3.5 m above: outside the +-2 m band");
            Assert.That(HitGeometry.Hits(AttackCatalog.Bow.Attacks[AttackSet.Special], 0, 0, 0, 0, 0, 0, 5f, 0.5f), Is.False, "rays are the caller's raycast");
        }

        [Test]
        public void AngleBetween_WrapsAround()
        {
            Assert.That(HitGeometry.AngleBetweenDeg(350f, 10f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(HitGeometry.AngleBetweenDeg(-170f, 170f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(HitGeometry.YawDegOf(1f, 0f), Is.EqualTo(90f).Within(1e-4f));
        }

        // ---- hitstop ----

        [Test]
        public void Hitstop_IsCappedAtTwelveFramesPerHalfSecond()
        {
            var b = new HitstopBudget();
            Assert.That(b.Request(10, 0f), Is.EqualTo(10), "a finisher gets its 10");
            Assert.That(b.Request(4, 0.1f), Is.EqualTo(2), "only 2 left in the window");
            Assert.That(b.Request(4, 0.2f), Is.EqualTo(0));
            Assert.That(b.Request(4, 0.55f), Is.EqualTo(4), "the first grants aged out of the 0.5 s window");
            Assert.That(b.Request(0, 1f), Is.EqualTo(0));
            Assert.That(b.Request(-3, 1f), Is.EqualTo(0));
        }

        [Test]
        public void Hitstop_FourFrames_OnNormalHits_TenOnFinishers()
        {
            Assert.That(AttackCatalog.Axe.Attacks[AttackSet.Hit1].HitstopFrames, Is.EqualTo(4));
            Assert.That(AttackCatalog.Axe.Attacks[AttackSet.Finisher].HitstopFrames, Is.EqualTo(10));
        }
    }
}
