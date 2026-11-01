// Catches: the post-hit grace window drifting from combat-timings rows 1-4 and 8 (80 / 160 / 8 ticks at 60 Hz), an off-by-one at the window edges, the
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
            Assert.That(HurtGrace.Seconds(HurtKind.Light), Is.EqualTo(80f / 60f).Within(1e-6f));
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, 0f), Is.True);
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, 1.32f), Is.True, "just inside");
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, 1.34f), Is.False, "just outside");
            Assert.That(HurtGrace.IsActive(false, HurtKind.Light, 0.5f), Is.False, "no hit, no grace");
        }

        [Test]
        public void HeavyOutlastsLight_AndMultiHitIsShort()
        {
            Assert.That(HurtGrace.Seconds(HurtKind.Heavy), Is.EqualTo(160f / 60f).Within(1e-6f));
            Assert.That(HurtGrace.IsActive(true, HurtKind.Heavy, 2.0f), Is.True);
            Assert.That(HurtGrace.IsActive(true, HurtKind.Light, 2.0f), Is.False);
            Assert.That(HurtGrace.IsActive(true, HurtKind.Heavy, 2.68f), Is.False);
            Assert.That(HurtGrace.Seconds(HurtKind.MultiHit), Is.EqualTo(8f / 60f).Within(1e-6f));
            Assert.That(HurtGrace.IsActive(true, HurtKind.MultiHit, 0.14f), Is.False);
        }

        [Test]
        public void Classify_UsesTheKnockbackThreshold()
        {
            Assert.That(HurtGrace.Classify(2.99f), Is.EqualTo(HurtKind.Light));
            Assert.That(HurtGrace.Classify(3f), Is.EqualTo(HurtKind.Heavy));
            Assert.That(HurtGrace.Classify(0f, multiHit: true), Is.EqualTo(HurtKind.MultiHit));
            Assert.That(HurtGrace.Classify(5f, multiHit: true), Is.EqualTo(HurtKind.Heavy));
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

        [Test]
        public void Knockdown_NeedsHeavyKnockback_AndASurvivor()
        {
            Assert.That(HurtGrace.Knockdown(3f, false), Is.True);
            Assert.That(HurtGrace.Knockdown(2.9f, false), Is.False);
            Assert.That(HurtGrace.Knockdown(4f, true), Is.False, "a killing blow is a death, not a knock-down");
        }
    }
}
