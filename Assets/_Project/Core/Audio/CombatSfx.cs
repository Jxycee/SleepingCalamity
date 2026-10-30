// Core/Audio/CombatSfx.cs: the M3 combat cues, generated like the rest of the procedural set (deterministic for a given seed, peak-normalised by
// SfxRecipes). These recipes are the fallback and the mixing source: SfxGenerator.ApplyFileClips swaps in the recorded Kenney CC0 clips by cue id
// (docs/audio/SOURCES.md has the file mapping); a cue without a file plays its generated sound.
// Pure C#, no engine. Authority: none.
using System;

namespace SleepingCalamity.Core
{
    public static class CombatSfx
    {
        public const string Hit = "Combat_Hit", HitHeavy = "Combat_HitHeavy", HitCrit = "Combat_HitCrit", Whoosh = "Combat_Whoosh", Block = "Combat_Block",
            Parry = "Combat_Parry", Slam = "Combat_Slam", Hurt = "Combat_Hurt", Pop = "Combat_Pop", Heal = "Combat_Heal", Spell = "Combat_Spell",
            Shot = "Combat_Shot", Dodge = "Combat_Dodge", Telegraph = "Combat_Telegraph", Die = "Combat_Die";

        private const float TwoPi = 2f * MathF.PI;

        public static readonly SfxRecipe[] Recipes =
        {
            R(Hit, 0.16f, 0.8f, 0.04f), R(HitHeavy, 0.30f, 0.95f, 0.06f), R(HitCrit, 0.30f, 0.9f, 0.08f), R(Whoosh, 0.20f, 0.5f, 0.05f), R(Block, 0.22f, 0.7f, 0.05f),
            R(Parry, 0.35f, 0.85f, 0.08f), R(Slam, 0.50f, 1.0f, 0.15f), R(Hurt, 0.22f, 0.9f, 0.10f), R(Pop, 0.22f, 0.7f, 0.10f), R(Heal, 0.45f, 0.6f, 0.15f),
            R(Spell, 0.45f, 0.8f, 0.15f), R(Shot, 0.20f, 0.7f, 0.05f), R(Dodge, 0.18f, 0.45f, 0.08f), R(Telegraph, 0.25f, 0.6f, 0.15f), R(Die, 0.45f, 0.9f, 0.10f),
        };

        private static SfxRecipe R(string id, float seconds, float volume, float minInterval) =>
            new SfxRecipe(id, seconds, false, AudioCategory.Sfx, volume, true, 40f, minInterval);

        public static bool TryGenerate(string id, int n, int sr, ref Pcg32 rng, out float[] o)
        {
            o = new float[n];
            switch (id)
            {
                case Hit: Thump(o, sr, 190f, 70f, 28f, 0.9f); Noise(o, ref rng, 55f, 0.55f, 0.65f); break;
                case HitHeavy: Thump(o, sr, 120f, 42f, 14f, 1.0f); Noise(o, ref rng, 30f, 0.45f, 0.8f); Tone(o, sr, 90f, 90f, 11f, 0.4f); break;
                case HitCrit: Thump(o, sr, 200f, 70f, 22f, 0.8f); Noise(o, ref rng, 40f, 0.5f, 0.6f); Tone(o, sr, 1760f, 1760f, 9f, 0.5f); Tone(o, sr, 2637f, 2637f, 12f, 0.3f); break;
                case Whoosh: Swell(o, ref rng, 0.3f, 0.55f); break;
                case Block: Tone(o, sr, 960f, 960f, 22f, 0.6f); Tone(o, sr, 1530f, 1530f, 28f, 0.4f); Tone(o, sr, 2410f, 2410f, 40f, 0.25f); Noise(o, ref rng, 90f, 0.7f, 0.4f); break;
                case Parry: Tone(o, sr, 1568f, 1568f, 10f, 0.7f); Tone(o, sr, 2349f, 2349f, 14f, 0.5f); Tone(o, sr, 3136f, 3136f, 18f, 0.3f); Noise(o, ref rng, 120f, 0.8f, 0.5f); break;
                case Slam: Thump(o, sr, 90f, 32f, 7f, 1.0f); Noise(o, ref rng, 9f, 0.15f, 0.7f); break;
                case Hurt: Thump(o, sr, 330f, 120f, 12f, 0.8f); Noise(o, ref rng, 40f, 0.4f, 0.5f); break;
                case Pop: Tone(o, sr, 660f, 660f, 22f, 0.7f, 0f, 0.05f); Tone(o, sr, 990f, 990f, 22f, 0.7f, 0.05f, 0.1f); Tone(o, sr, 1320f, 1320f, 14f, 0.8f, 0.1f, 0.22f); break;
                case Heal: Tone(o, sr, 523f, 523f, 6f, 0.6f, 0f, 0.4f); Tone(o, sr, 784f, 784f, 6f, 0.5f, 0.08f, 0.4f); Tone(o, sr, 1046f, 1046f, 6f, 0.4f, 0.16f, 0.45f); break;
                case Spell: Swell(o, ref rng, 0.5f, 0.6f); Tone(o, sr, 280f, 880f, 3f, 0.5f); break;
                case Shot: Tone(o, sr, 240f, 160f, 24f, 0.7f); Noise(o, ref rng, 70f, 0.8f, 0.5f); break;
                case Dodge: Swell(o, ref rng, 0.15f, 0.9f); break;
                case Telegraph: Tone(o, sr, 440f, 330f, 6f, 0.5f); Tone(o, sr, 466f, 349f, 6f, 0.5f); break;
                case Die: Thump(o, sr, 260f, 55f, 7f, 0.8f); Noise(o, ref rng, 12f, 0.3f, 0.6f); break;
                default: return false;
            }
            return true;
        }

        // A sine that glides f0 -> f1 across the buffer (or a part of it), with an exponential decay, added to o.
        private static void Tone(float[] o, int sr, float f0, float f1, float decay, float amp, float startSec = 0f, float endSec = -1f)
        {
            int n = o.Length;
            int a = Math.Max(0, (int)(startSec * sr)), b = endSec < 0f ? n : Math.Min(n, (int)(endSec * sr));
            double phase = 0.0;
            for (int i = a; i < b; i++)
            {
                float t = (i - a) / (float)sr;
                float k = (i - a) / (float)Math.Max(1, b - a);
                float f = f0 + (f1 - f0) * k;
                phase += TwoPi * f / sr;
                float env = MathF.Min(1f, t / 0.003f) * MathF.Min(1f, (b - 1 - i) / (0.008f * sr)) * MathF.Exp(-decay * t);
                o[i] += (float)Math.Sin(phase) * env * amp;
            }
        }

        // A thump: a sine gliding down fast (a drum-like body).
        private static void Thump(float[] o, int sr, float f0, float f1, float decay, float amp)
        {
            int n = o.Length;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float f = f1 + (f0 - f1) * MathF.Exp(-t * 40f);
                phase += TwoPi * f / sr;
                float env = MathF.Min(1f, t / 0.002f) * MathF.Min(1f, (n - 1 - i) / (0.01f * sr)) * MathF.Exp(-decay * t);
                o[i] += (float)Math.Sin(phase) * env * amp;
            }
        }

        // A noise burst with an exponential decay; bright (high pass-ish) at brightness 1, dull at 0.
        private static void Noise(float[] o, ref Pcg32 rng, float decay, float brightness, float amp)
        {
            int n = o.Length;
            float lp = 0f;
            float sr = 44100f;
            for (int i = 0; i < n; i++)
            {
                float white = rng.NextFloat() * 2f - 1f;
                lp += (0.1f + 0.9f * brightness) * (white - lp);
                float t = i / sr;
                float env = MathF.Min(1f, t / 0.001f) * MathF.Min(1f, (n - 1 - i) / (0.008f * sr)) * MathF.Exp(-decay * t);
                o[i] += lp * env * amp;
            }
        }

        // A swell of filtered noise: rises, peaks at `peakAt` (0..1 of the buffer), falls to zero.
        private static void Swell(float[] o, ref Pcg32 rng, float peakAt, float amp)
        {
            int n = o.Length;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                float env = k < peakAt ? k / peakAt : (1f - k) / (1f - peakAt);
                env = env * env;
                float cutoff = 0.05f + 0.45f * env;
                float white = rng.NextFloat() * 2f - 1f;
                lp += cutoff * (white - lp);
                o[i] += lp * env * amp;
            }
        }
    }
}
