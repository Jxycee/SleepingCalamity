using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Feedback;

namespace SleepingCalamity.Tests.EditMode
{
    public class HitFeedbackCoreTests
    {
        [Test]
        public void Trauma_AddClampsAtOne_DecaysLinearly_ShakeIsSquared()
        {
            var t = new TraumaShake { DecayPerSecond = 1f };
            t.Add(0.7f); t.Add(0.7f);
            Assert.AreEqual(1f, t.Trauma, 1e-6f);
            t.Tick(0.25f);
            Assert.AreEqual(0.75f, t.Trauma, 1e-6f);
            Assert.AreEqual(0.5625f, t.Shake, 1e-6f);
            t.Tick(5f);
            Assert.AreEqual(0f, t.Trauma, 1e-6f);
        }

        [Test]
        public void Trauma_ZeroTraumaGivesExactlyZeroOutput_AndSampleIsDeterministic()
        {
            var t = new TraumaShake();
            t.Sample(1.3f, 7, out float y, out float p, out float r);
            Assert.AreEqual(0f, y); Assert.AreEqual(0f, p); Assert.AreEqual(0f, r);
            t.Add(0.8f);
            t.Sample(1.3f, 7, out float y1, out float p1, out float r1);
            t.Sample(1.3f, 7, out float y2, out float p2, out float r2);
            Assert.AreEqual(y1, y2); Assert.AreEqual(p1, p2); Assert.AreEqual(r1, r2);
            Assert.LessOrEqual(System.Math.Abs(y1), TraumaShake.MaxYawDeg);
        }

        [Test]
        public void Trauma_NoiseIsSmooth()
        {
            float prev = TraumaShake.Noise(0f, 3);
            for (float x = 0.01f; x < 8f; x += 0.01f)
            {
                float v = TraumaShake.Noise(x, 3);
                Assert.Less(System.Math.Abs(v - prev), 0.1f);
                Assert.LessOrEqual(System.Math.Abs(v), 1.0001f);
                prev = v;
            }
        }

        [Test]
        public void Trauma_ShakeSetting_ZeroGivesNothing_HalfHalvesTheAdd()
        {
            var a = new TraumaShake(); a.Add(0.4f, 0f);
            Assert.AreEqual(0f, a.Trauma);
            var b = new TraumaShake(); b.Add(0.4f, 0.5f);
            Assert.AreEqual(0.2f, b.Trauma, 1e-6f);
        }

        [Test]
        public void Hitstop_OffIsZero_ReducedHalves_CapApplies_FalloffScales()
        {
            Assert.AreEqual(0, HitstopPolicy.Apply(14, HitstopMode.Off));
            Assert.AreEqual(7, HitstopPolicy.Apply(14, HitstopMode.Reduced));
            Assert.AreEqual(14, HitstopPolicy.Apply(14, HitstopMode.Full));
            Assert.AreEqual(HitstopPolicy.GlobalCapFrames, HitstopPolicy.Apply(40, HitstopMode.Full));
            Assert.AreEqual(8, HitstopPolicy.Falloff(8, 0, 0.5f));
            Assert.AreEqual(4, HitstopPolicy.Falloff(8, 1, 0.5f));
            Assert.AreEqual(2, HitstopPolicy.Falloff(8, 2, 0.5f));
        }

        [Test]
        public void Weights_Classify()
        {
            Assert.AreEqual(HitWeight.Finisher, HitWeights.Classify(true, false, 4));
            Assert.AreEqual(HitWeight.Heavy, HitWeights.Classify(false, false, 14));
            Assert.AreEqual(HitWeight.Light, HitWeights.Classify(false, true, 4));
            Assert.AreEqual(HitWeight.Light, HitWeights.Classify(false, false, 2));
            Assert.AreEqual(HitWeight.Normal, HitWeights.Classify(false, false, 4));
        }

        [Test]
        public void Rumble_MaxOfOverlappingPulses_Decays_NeverAboveOne_StopZeroes_SliderScales()
        {
            var r = new PulseRumble();
            r.Add(0.2f, 0.9f, 1f);
            r.Add(0.6f, 0.1f, 1f);
            Assert.AreEqual(0.6f, r.Low, 1e-6f);
            Assert.AreEqual(0.9f, r.High, 1e-6f);
            r.Tick(0.5f);
            Assert.AreEqual(0.3f, r.Low, 1e-6f);
            Assert.AreEqual(0.45f, r.High, 1e-6f);
            r.Tick(0.6f);
            Assert.AreEqual(0f, r.Low); Assert.AreEqual(0f, r.High); Assert.AreEqual(0, r.Active);

            r.Add(5f, 5f, 1f);
            Assert.AreEqual(1f, r.Low); Assert.AreEqual(1f, r.High);
            r.Scale = 0.5f; r.Tick(0f);
            Assert.AreEqual(0.5f, r.Low, 1e-6f);
            r.Scale = 0f; r.Tick(0f);
            Assert.AreEqual(0f, r.Low); Assert.AreEqual(0f, r.High);
            r.Scale = 1f; r.Tick(0f);
            Assert.Greater(r.Low, 0f);
            r.Stop();
            Assert.AreEqual(0f, r.Low); Assert.AreEqual(0f, r.High); Assert.AreEqual(0, r.Active);
        }

        [Test]
        public void Rumble_ManyPulses_DoNotOverflow()
        {
            var r = new PulseRumble();
            for (int i = 0; i < 50; i++) r.Add(0.1f, 0.1f, 0.5f + i * 0.01f);
            Assert.LessOrEqual(r.Active, PulseRumble.MaxPulses);
        }

        [Test]
        public void Flash_ReduceCapsBrightness_AndStrobeGuardAllowsThreePerSecond()
        {
            var f = new FlashLimiter();
            Assert.AreEqual(1f, f.Brightness(1f));
            for (int i = 0; i < 20; i++) Assert.IsTrue(f.Allow(i * 0.01f));   // reduce off: never limited
            f.Reduce = true;
            Assert.AreEqual(FlashLimiter.ReducedMaxBrightness, f.Brightness(1f));
            Assert.AreEqual(0.2f, f.Brightness(0.2f), 1e-6f);

            // any three consecutive allowed starts span at least one second, so a window holds at most 3
            var h = new FlashLimiter { Reduce = true };
            var starts = new List<float>();
            for (int i = 0; i < 400; i++) { float now = i * 0.013f; if (h.Allow(now)) starts.Add(now); }
            Assert.Greater(starts.Count, 6);
            for (int i = 3; i < starts.Count; i++) Assert.GreaterOrEqual(starts[i] - starts[i - 3], FlashLimiter.WindowSeconds - 1e-4f);
        }

        [Test]
        public void Profiles_EveryWeightMapsToAProfile_IdsUnique_ValuesInRange()
        {
            foreach (HitWeight w in System.Enum.GetValues(typeof(HitWeight)))
                Assert.IsNotNull(FeedbackProfiles.Get(FeedbackProfiles.IdFor(w)), w.ToString());
            var ids = new HashSet<string>();
            foreach (FeedbackProfileData p in FeedbackProfiles.Defaults)
            {
                Assert.IsTrue(ids.Add(p.Id), "duplicate " + p.Id);
                Assert.That(p.Trauma, Is.InRange(0f, 1f));
                Assert.That(p.RumbleLow, Is.InRange(0f, 1f));
                Assert.That(p.RumbleHigh, Is.InRange(0f, 1f));
                Assert.GreaterOrEqual(p.RumbleMs, 0);
            }
            Assert.Greater(FeedbackProfiles.Get(FeedbackProfiles.HitFinisher).Trauma, FeedbackProfiles.Get(FeedbackProfiles.HitLight).Trauma);
        }
    }
}
