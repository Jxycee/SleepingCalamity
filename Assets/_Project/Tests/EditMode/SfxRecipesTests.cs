using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using NUnit.Framework;

namespace SleepingCalamity.Tests.EditMode
{
    // Ported from FishGame's SfxRecipesTests (the M1 set; its M2 fishing and M3 rain/derby cues did not come across).
    public class SfxRecipesTests
    {
        private const ulong Seed = 1234UL;

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Math.Abs(samples[i]);
                if (a > peak) peak = a;
            }
            return peak;
        }

        [Test]
        public void SfxRecipes_Table_MatchesContract()
        {
            string[] ids =
            {
                SfxIds.UiClick, SfxIds.UiConfirm, SfxIds.UiError,
                SfxIds.WindAmbient, SfxIds.WaterLap, SfxIds.BellRing,
            };
            Assert.AreEqual(ids.Length, SfxRecipes.All.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(ids[i], SfxRecipes.All[i].Id, "order at index " + i);
            }

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.UiClick, out SfxRecipe click));
            Assert.AreEqual(0.08f, click.LengthSeconds, 1e-6f);
            Assert.IsFalse(click.Loop);
            Assert.AreEqual(AudioCategory.Ui, click.Category);
            Assert.AreEqual(0.6f, click.BaseVolume, 1e-6f);
            Assert.IsFalse(click.Spatial);
            Assert.AreEqual(0.03f, click.MinInterval, 1e-6f);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.WindAmbient, out SfxRecipe wind));
            Assert.AreEqual(6.0f, wind.LengthSeconds, 1e-6f);
            Assert.IsTrue(wind.Loop);
            Assert.AreEqual(AudioCategory.Ambience, wind.Category);
            Assert.AreEqual(0.5f, wind.BaseVolume, 1e-6f);
            Assert.IsFalse(wind.Spatial);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.WaterLap, out SfxRecipe lap));
            Assert.AreEqual(0.4f, lap.LengthSeconds, 1e-6f);
            Assert.IsFalse(lap.Loop);
            Assert.AreEqual(AudioCategory.Ambience, lap.Category);
            Assert.AreEqual(0.3f, lap.MinInterval, 1e-6f);

            Assert.IsTrue(SfxRecipes.TryGet(SfxIds.BellRing, out SfxRecipe bell));
            Assert.AreEqual(1.0f, bell.LengthSeconds, 1e-6f);
            Assert.AreEqual(AudioCategory.Sfx, bell.Category);
            Assert.AreEqual(0.9f, bell.BaseVolume, 1e-6f);
            Assert.IsTrue(bell.Spatial);
            Assert.AreEqual(30f, bell.MaxDistance, 1e-6f);
            Assert.AreEqual(0.25f, bell.MinInterval, 1e-6f);

            Assert.IsFalse(SfxRecipes.TryGet("Not_A_Cue", out _));
        }

        [Test]
        public void SfxRecipes_EveryRecipe_HasCorrectSampleCount()
        {
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                int expected = (int)Math.Round(recipe.LengthSeconds * SfxRecipes.SampleRate);
                Assert.AreEqual(expected, SfxRecipes.SampleCount(recipe, SfxRecipes.SampleRate), recipe.Id);
                Assert.AreEqual(expected, samples.Length, recipe.Id);
            }
        }

        [Test]
        public void SfxRecipes_EveryRecipe_PeakBelowOne()
        {
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                float peak = Peak(SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed));
                Assert.Less(peak, 1.0f, recipe.Id);
                Assert.Greater(peak, 0.5f, recipe.Id + " is almost silent");
            }
        }

        [Test]
        public void SfxRecipes_EveryRecipe_HasNoNaNOrInf()
        {
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                int bad = -1;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    {
                        bad = i;
                        break;
                    }
                }
                Assert.AreEqual(-1, bad, recipe.Id + " has NaN or Inf at sample " + bad);
            }
        }

        [Test]
        public void SfxRecipes_Loops_HaveSmoothSeam()
        {
            int loops = 0;
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                if (!recipe.Loop) continue;
                loops++;
                float[] samples = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                float seam = Math.Abs(samples[samples.Length - 1] - samples[0]);
                Assert.Less(seam, 0.05f, recipe.Id);
            }
            Assert.Greater(loops, 0, "at least one recipe must be a loop");
        }

        [Test]
        public void SfxRecipes_SameSeed_IsDeterministic()
        {
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                float[] a = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                float[] b = SfxRecipes.Generate(recipe.Id, SfxRecipes.SampleRate, Seed);
                Assert.AreEqual(a.Length, b.Length, recipe.Id);
                int firstDiff = -1;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i])
                    {
                        firstDiff = i;
                        break;
                    }
                }
                Assert.AreEqual(-1, firstDiff, recipe.Id + " differs at sample " + firstDiff);
            }
        }

        [Test]
        public void SfxRecipes_UnknownId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SfxRecipes.Generate("Not_A_Cue", SfxRecipes.SampleRate, Seed));
        }

        [Test]
        public void SfxRecipes_AllIds_Unique()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SfxRecipe recipe in SfxRecipes.All)
            {
                Assert.IsTrue(seen.Add(recipe.Id), "duplicate id " + recipe.Id);
                Assert.IsTrue(SfxRecipes.TryGet(recipe.Id, out SfxRecipe found), "TryGet misses " + recipe.Id);
                Assert.AreEqual(recipe.Id, found.Id);
            }
            Assert.AreSame(SfxRecipes.All, SfxRecipes.All, "All is one cached list");
        }

        [Test]
        public void SfxRecipes_NineArgConstructor_MinDistanceIsLast()
        {
            foreach (SfxRecipe r in SfxRecipes.All) Assert.AreEqual(1f, r.MinDistance, 1e-6f, r.Id + " keeps the 8-arg default MinDistance");

            var nine = new SfxRecipe("X", 1f, true, AudioCategory.Music, 0.3f, true, 12f, 0.4f, 7f);
            Assert.AreEqual(12f, nine.MaxDistance, 1e-6f, "ctor order: maxDistance is the 7th");
            Assert.AreEqual(0.4f, nine.MinInterval, 1e-6f, "ctor order: minInterval is the 8th");
            Assert.AreEqual(7f, nine.MinDistance, 1e-6f, "ctor order: minDistance is LAST");
        }
    }
}
