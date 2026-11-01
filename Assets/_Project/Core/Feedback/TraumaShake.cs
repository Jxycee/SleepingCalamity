// Authority: none (pure logic, local and cosmetic). The "trauma" model of camera shake (hit-feedback.md section 3 and 13): events ADD trauma (0..1), it decays
// linearly, and the visible shake is trauma squared, so small hits are subtle and big ones stack hard. The angles come from smooth value noise (no per-frame
// random jitter), deterministic for a seed so EditMode can test it. No UnityEngine types.
using System;

namespace SleepingCalamity.Core.Feedback
{
    public sealed class TraumaShake
    {
        public const float MaxYawDeg = 2.5f, MaxPitchDeg = 2.0f, MaxRollDeg = 1.5f, Frequency = 18f, DefaultDecayPerSecond = 1.4f;

        public float Trauma { get; private set; }
        public float DecayPerSecond = DefaultDecayPerSecond;

        // Adds `amount` scaled by the player's shake setting (0..1). Clamped at 1. A scale of 0 (or a non-positive amount) changes nothing.
        public void Add(float amount, float scale = 1f)
        {
            if (!(amount > 0f) || !(scale > 0f)) return;
            Trauma = Math.Min(1f, Trauma + amount * Math.Min(1f, scale));
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || Trauma <= 0f) return;
            Trauma = Math.Max(0f, Trauma - DecayPerSecond * dt);
        }

        public void Clear() { Trauma = 0f; }

        public float Shake => Trauma * Trauma;

        // The camera rotation offset in degrees at `time` seconds. Exactly zero when there is no trauma.
        public void Sample(float time, int seed, out float yaw, out float pitch, out float roll)
        {
            float s = Shake;
            if (s <= 0f) { yaw = pitch = roll = 0f; return; }
            float t = time * Frequency;
            yaw = MaxYawDeg * s * Noise(t, seed);
            pitch = MaxPitchDeg * s * Noise(t, seed + 101);
            roll = MaxRollDeg * s * Noise(t, seed + 202);
        }

        // Smooth value noise in -1..1: random lattice values blended with a smoothstep.
        public static float Noise(float x, int seed)
        {
            float f = (float)Math.Floor(x);
            int i = (int)f;
            float u = x - f;
            u = u * u * (3f - 2f * u);
            return Lattice(i, seed) * (1f - u) + Lattice(i + 1, seed) * u;
        }

        private static float Lattice(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFu) / 32767.5f - 1f;
            }
        }
    }
}
