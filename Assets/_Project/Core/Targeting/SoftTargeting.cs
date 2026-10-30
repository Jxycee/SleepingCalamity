// Core, no engine references, unit-tested. The always-on soft target (docs/PHASE1.md 6.1/6.5) and the scoring both layers share.
// Range and angle are measured on the flat (XZ) plane; the height band compares feet heights.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class SoftTargeting
    {
        public const int NoTarget = -1;

        public static float FlatDistance(float dx, float dz) => MathF.Sqrt(dx * dx + dz * dz);

        // Angle in degrees (0..180) between two flat vectors; 0 when either has no length (standing on it, or no facing).
        public static float AngleDeg(float ax, float az, float bx, float bz)
        {
            float la = MathF.Sqrt(ax * ax + az * az);
            float lb = MathF.Sqrt(bx * bx + bz * bz);
            if (la <= 1e-6f || lb <= 1e-6f) return 0f;
            float cos = (ax * bx + az * bz) / (la * lb);
            if (cos > 1f) cos = 1f;
            else if (cos < -1f) cos = -1f;
            return MathF.Acos(cos) * (180f / MathF.PI);
        }

        // 0..1, higher is better: FacingWeight on how centred the target is in the cone, the rest on how close it is.
        public static float Score(float distance, float angleDeg, float range, float coneHalfAngleDeg)
        {
            float facing = 1f - Clamp01(angleDeg / coneHalfAngleDeg);
            float proximity = 1f - Clamp01(distance / range);
            return TargetingConfig.FacingWeight * facing + (1f - TargetingConfig.FacingWeight) * proximity;
        }

        // Inside the soft zone: alive, in sight, within 6 m, within +-2 m of height, within the 60 degree half-angle (edges included).
        public static bool InSoftZone(in TargetSeeker seeker, in TargetCandidate c)
        {
            return InZone(in seeker, in c, TargetingConfig.SoftRange, TargetingConfig.HeightBand, TargetingConfig.SoftConeHalfAngleDeg, out _, out _);
        }

        // The best soft target's candidate index, or NoTarget. Swings home toward it; it is picked fresh every call (no stickiness).
        public static int PickSoft(in TargetSeeker seeker, IReadOnlyList<TargetCandidate> candidates)
        {
            return PickBest(in seeker, candidates, TargetingConfig.SoftRange, TargetingConfig.HeightBand, TargetingConfig.SoftConeHalfAngleDeg);
        }

        // The best target to lock onto: within 10 m (any direction, but facing still counts in the score), +-4 m of height, in sight.
        public static int PickAcquire(in TargetSeeker seeker, IReadOnlyList<TargetCandidate> candidates)
        {
            return PickBest(in seeker, candidates, TargetingConfig.AcquireRange, TargetingConfig.HardHeightBand, 180f);
        }

        private static int PickBest(in TargetSeeker seeker, IReadOnlyList<TargetCandidate> candidates, float range, float band, float cone)
        {
            int best = NoTarget;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                TargetCandidate c = candidates[i];
                if (!InZone(in seeker, in c, range, band, cone, out float distance, out float angle)) continue;
                float score = Score(distance, angle, range, cone);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private static bool InZone(in TargetSeeker s, in TargetCandidate c, float range, float band, float cone, out float distance, out float angle)
        {
            distance = 0f;
            angle = 0f;
            if (!c.Alive || !c.HasLineOfSight) return false;
            float dx = c.X - s.X;
            float dz = c.Z - s.Z;
            distance = FlatDistance(dx, dz);
            if (distance > range) return false;
            if (MathF.Abs(c.Y - s.Y) > band) return false;
            angle = AngleDeg(s.FacingX, s.FacingZ, dx, dz);
            return angle <= cone;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
