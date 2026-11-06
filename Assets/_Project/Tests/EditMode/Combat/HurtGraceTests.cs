// Catches: the post-hit grace window drifting from combat-timings the recommended M3 values (0.8 / 1.2 / 0.15 s: combat-timings "Recommended M3 values" 1-3), an off-by-one at the window edges, the
// hurt pulse not being a 0..1 half sine over the window, and the knock-down threshold (knockback >= 3, never on a kill).
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class HurtGraceTests
    {
        [Test]
        public void LightWindow_EdgesAreExact()
        {
            Assert.That(HurtGrace.Seconds(HurtKind.Light), Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, 0f), Is.True);
            float s = HurtGrace.Seconds(HurtKind.Light);
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, s - 1e-4f), Is.True, "just inside");
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, s), Is.False, "exactly at Seconds the window is over");
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, -0.01f), Is.False, "negative time");
            Assert.That(HurtGrace.IsActive(false, HurtKind.Light, 0.5f), Is.False, "no hit, no grace");
        }

        [Test]
        public void HeavyOutlastsLight_AndMultiHitIsShort()
        {
            Assert.That(HurtGrace.Seconds(HurtKind.Heavy), Is.EqualTo(1.2f).Within(1e-6f));
            Assert.That(HurtGrace.Seconds(HurtKind.Heavy), Is.EqualTo(HurtGrace.KnockdownSeconds + 0.3f).Within(1e-6f));
            Assert.That(HurtGrace.IsActive(true, HurtKind.Heavy, 1.0f), Is.True);
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, 1.0f), Is.False);
            Assert.That(HurtGrace.IsActive(true, HurtKind.Heavy, 1.21f), Is.False);
            Assert.That(HurtGrace.Seconds(HurtKind.MultiHit), Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(HurtGrace.IsActive(true, HurtKind.MultiHit, 0.16f), Is.False);
        }

        [Test]
        public void Classify_UsesTheKnockbackThreshold_AndAGuardBreakIsHeavy()
        {
            Assert.That(HurtGrace.Classify(2.99f), Is.EqualTo(HurtKind.Light));
            Assert.That(HurtGrace.Classify(3f), Is.EqualTo(HurtKind.Heavy));
            Assert.That(HurtGrace.Classify(0f, multiHit: true), Is.EqualTo(HurtKind.MultiHit));
            Assert.That(HurtGrace.Classify(5f, multiHit: true), Is.EqualTo(HurtKind.Heavy));
            Assert.That(HurtGrace.Classify(0f, guardBreak: true), Is.EqualTo(HurtKind.Heavy), "a guard break is heavy at any knockback");
            Assert.That(HurtGrace.Classify(0f, multiHit: true, guardBreak: true), Is.EqualTo(HurtKind.Heavy));
            Assert.That(HurtGrace.IsHeavyHit(0f, true), Is.True);
            Assert.That(HurtGrace.IsHeavyHit(3f, false), Is.True);
            Assert.That(HurtGrace.IsHeavyHit(2.9f, false), Is.False);
        }

        [Test]
        public void GraceTimer_48TicksOfASixtiethExpireExactlyOnTick48()
        {
            var t = new GraceTimer();
            Assert.That(t.Start(HurtKind.Light), Is.True);
            for (int i = 1; i <= 47; i++) { t.Tick(1f / 60f); Assert.That(t.Active, Is.True, "still active after tick " + i); }
            t.Tick(1f / 60f);
            Assert.That(t.Active, Is.False, "expired on tick 48");
            Assert.That(t.Remaining, Is.EqualTo(0f));
            var h = new GraceTimer();
            h.Start(HurtKind.Heavy);
            for (int i = 1; i <= 71; i++) h.Tick(1f / 60f);
            Assert.That(h.Active, Is.True);
            h.Tick(1f / 60f);
            Assert.That(h.Active, Is.False, "heavy expires on tick 72");
            var m = new GraceTimer();
            m.Start(HurtKind.MultiHit);
            for (int i = 1; i <= 8; i++) m.Tick(1f / 60f);
            Assert.That(m.Active, Is.True);
            m.Tick(1f / 60f);
            Assert.That(m.Active, Is.False, "multi-hit expires on tick 9");
        }

        [Test]
        public void GraceTimer_AHitDuringGraceDoesNotRestartIt_AndAHeavyOneIsIgnored()
        {
            var t = new GraceTimer();
            t.Start(HurtKind.Light);
            for (int i = 0; i < 30; i++) t.Tick(1f / 60f);
            float before = t.Remaining;
            Assert.That(t.Start(HurtKind.Light), Is.False, "a second light hit does not restart");
            Assert.That(t.Remaining, Is.EqualTo(before));
            Assert.That(t.Start(HurtKind.Heavy), Is.False, "a heavy hit in light grace is ignored");
            Assert.That(t.Kind, Is.EqualTo(HurtKind.Light));
            Assert.That(t.Remaining, Is.EqualTo(before));
            for (int i = 0; i < 18; i++) t.Tick(1f / 60f);
            Assert.That(t.Active, Is.False, "the original window ended on its own schedule (48 ticks in all)");
            Assert.That(t.Start(HurtKind.Heavy), Is.True, "after it ends a new hit starts a new window");
        }

        [Test]
        public void GraceTimer_PulseAndFlicker()
        {
            var t = new GraceTimer();
            Assert.That(t.Pulse, Is.EqualTo(0f));
            Assert.That(t.FlickerOn, Is.False);
            t.Start(HurtKind.Light);
            for (int i = 0; i < 24; i++) t.Tick(1f / 60f);   // the midpoint
            Assert.That(t.Pulse, Is.EqualTo(1f).Within(1e-3f));
            for (int i = 0; i < 6; i++) t.Tick(1f / 60f);
            Assert.That(t.Flickering, Is.False, "the flicker is the first 0.4 s only");
            var u = new GraceTimer();
            u.Start(HurtKind.Light);
            Assert.That(u.Flickering, Is.True);
        }

        [Test]
        public void Pulse_IsAHalfSine_ZeroAtEndsOneAtMiddle()
        {
            foreach (HurtKind k in new[] { HurtKind.Light, HurtKind.Heavy, HurtKind.MultiHit })
            {
                float d = HurtGrace.Seconds(k);
                Assert.That(HurtGrace.Pulse(true, k, 0f), Is.EqualTo(0f).Within(1e-6f));
                Assert.That(HurtGrace.Pulse(true, k, d * 0.5f), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(HurtGrace.Pulse(true, k, d * 0.25f), Is.EqualTo(0.7071f).Within(1e-3f));
                Assert.That(HurtGrace.Pulse(true, k, d), Is.EqualTo(0f), "outside the window");
                Assert.That(HurtGrace.Pulse(false, k, d * 0.5f), Is.EqualTo(0f), "no hit");
            }
        }
    }
}
