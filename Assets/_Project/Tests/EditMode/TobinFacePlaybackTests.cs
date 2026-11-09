// Pure-logic tests for TobinFacePlayback: the jawOpen cap, the blink curve and schedule, the eye-look clamp.
using NUnit.Framework;
using SleepingCalamity.Presentation.Characters;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TobinFacePlaybackTests
    {
        [Test]
        public void JawOpen_IsCappedAtHalf()
        {
            Assert.AreEqual(0.5f, TobinFaceLogic.JawOpenCap);
            Assert.AreEqual(0.5f, TobinFaceLogic.ClampJaw(1f), 1e-6f);
            Assert.AreEqual(0.5f, TobinFaceLogic.ClampJaw(0.5f), 1e-6f);
            Assert.AreEqual(0.3f, TobinFaceLogic.ClampJaw(0.3f), 1e-6f);
            Assert.AreEqual(0f, TobinFaceLogic.ClampJaw(-1f), 1e-6f);
        }

        [Test]
        public void BlinkCurve_ClosesFullyThenOpens()
        {
            Assert.AreEqual(0f, TobinFaceLogic.BlinkWeight(0f, 0.07f, 0.12f));
            Assert.AreEqual(1f, TobinFaceLogic.BlinkWeight(0.07f, 0.07f, 0.12f), 1e-5f);
            Assert.Greater(TobinFaceLogic.BlinkWeight(0.03f, 0.07f, 0.12f), 0f);
            Assert.Less(TobinFaceLogic.BlinkWeight(0.03f, 0.07f, 0.12f), 1f);
            Assert.AreEqual(0f, TobinFaceLogic.BlinkWeight(0.19f, 0.07f, 0.12f), 1e-5f);
            Assert.AreEqual(0f, TobinFaceLogic.BlinkWeight(5f, 0.07f, 0.12f));
        }

        [Test]
        public void BlinkController_BlinksWithinIntervalAndReachesClosed()
        {
            var rng = new System.Random(7);
            var b = new BlinkController(() => (float)rng.NextDouble(), 2.5f, 6f, 0.07f, 0.12f);
            const float dt = 1f / 60f;
            float t = 0f, lastBlinkEnd = 0f, peak = 0f; int blinks = 0; bool was = false;
            for (int i = 0; i < 60 * 120; i++)
            {
                t += dt;
                float w = b.Advance(dt);
                peak = Mathf.Max(peak, w);
                Assert.That(w, Is.InRange(0f, 1f));
                bool now = w > 0f;
                if (now && !was)
                {
                    float gap = t - lastBlinkEnd;
                    Assert.GreaterOrEqual(gap, 2.5f - 2 * dt, "blink came sooner than minInterval");
                    Assert.LessOrEqual(gap, 6f + 2 * dt, "blink came later than maxInterval");
                    blinks++;
                }
                if (!now && was) lastBlinkEnd = t;
                was = now;
            }
            Assert.GreaterOrEqual(blinks, 120 / 6 - 1, "expected roughly one blink per few seconds");
            Assert.GreaterOrEqual(peak, 0.99f, "a blink must fully close the lids");
        }

        [Test]
        public void ForceBlink_StartsOnNextStep()
        {
            var b = new BlinkController(() => 0.5f);
            b.ForceBlinkNow();
            Assert.Greater(b.Advance(1f / 60f), 0f);
        }

        [Test]
        public void LookClamp_LimitsYawAndPitch()
        {
            TobinFaceLogic.ClampLook(new Vector3(10f, 0f, 1f), 35f, 25f, out float yaw, out float pitch);
            Assert.AreEqual(35f, yaw, 1e-4f); Assert.AreEqual(0f, pitch, 1e-4f);
            TobinFaceLogic.ClampLook(new Vector3(0f, 10f, 1f), 35f, 25f, out yaw, out pitch);
            Assert.AreEqual(0f, yaw, 1e-4f); Assert.AreEqual(-25f, pitch, 1e-4f);   // looking up = negative pitch about +X
            TobinFaceLogic.ClampLook(new Vector3(-1f, 0f, 1f), 35f, 25f, out yaw, out pitch);
            Assert.AreEqual(-35f, yaw, 1e-4f);
            TobinFaceLogic.ClampLook(new Vector3(0.1f, 0f, 1f), 35f, 25f, out yaw, out pitch);
            Assert.AreEqual(Mathf.Atan(0.1f) * Mathf.Rad2Deg, yaw, 1e-3f);
        }
    }
}
