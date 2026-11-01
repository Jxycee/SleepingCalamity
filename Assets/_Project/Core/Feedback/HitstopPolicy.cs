// Authority: none (pure logic, cosmetic). Hitstop frame arithmetic for the feedback layer (hit-feedback.md section 2 and 13.2). The attacker's own freeze
// still comes from AttackData.HitstopFrames through HitstopBudget (the tuned Core values stay the source of truth, decision A33); this policy covers the
// victim-side hold and the player's hitstop setting. Frames are at 60 Hz.
using System;

namespace SleepingCalamity.Core.Feedback
{
    public enum HitstopMode : byte { Full = 0, Reduced = 1, Off = 2 }

    public static class HitstopPolicy
    {
        public const int GlobalCapFrames = 16;

        // Mode applied to a frame count: Off 0, Reduced half (rounded down), Full unchanged. Never negative, never above the global cap.
        public static int Apply(int frames, HitstopMode mode)
        {
            if (frames <= 0 || mode == HitstopMode.Off) return 0;
            if (mode == HitstopMode.Reduced) frames /= 2;
            return Math.Min(frames, GlobalCapFrames);
        }

        // The hit number `hitIndex` (0 = first) of one swing keeps falloff^hitIndex of the frames (0.5: later hits at 50%), rounded to nearest.
        public static int Falloff(int frames, int hitIndex, float falloff)
        {
            if (frames <= 0 || hitIndex < 0) return 0;
            if (falloff >= 1f) return frames;
            if (falloff <= 0f) return hitIndex == 0 ? frames : 0;
            return (int)Math.Round(frames * Math.Pow(falloff, hitIndex));
        }
    }
}
