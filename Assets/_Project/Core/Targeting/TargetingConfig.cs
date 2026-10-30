// Core, no engine references. The starting numbers for the two targeting layers (docs/PHASE1.md 6.1 and 6.5). Tune in M3 and M7.
// Soft target: always on, a cone + range + height band weighted by facing (swings home toward it). Hard lock: a toggle with hysteresis.
namespace SleepingCalamity.Core
{
    public static class TargetingConfig
    {
        // Soft target (6.5): 60 degrees half-angle, 6 m, +-2 m of height.
        public const float SoftConeHalfAngleDeg = 60f;
        public const float SoftRange = 6f;
        public const float HeightBand = 2f;

        // Hard lock (6.5): acquire inside 10 m, keep until 13 m, 1.0 s of lost line of sight. The 3 m gap is the hysteresis.
        public const float AcquireRange = 10f;
        public const float ReleaseRange = 13f;
        public const float LosGraceSeconds = 1.0f;

        // Not in 6.5 (added in M2): a locked target may sit on a ledge, so the lock's height band is wider than the soft target's.
        public const float HardHeightBand = 4f;

        // Score = FacingWeight * facing + (1 - FacingWeight) * proximity, both 0..1 (higher is better).
        public const float FacingWeight = 0.6f;

        // Right-stick flick (6.1): fires when the stick passes FlickOn from rest, re-arms only after it is back under FlickOff.
        // The new target must lie within FlickConeHalfAngleDeg of the flick direction, measured from the current target.
        public const float FlickOn = 0.75f;
        public const float FlickOff = 0.35f;
        public const float FlickConeHalfAngleDeg = 60f;
    }
}
