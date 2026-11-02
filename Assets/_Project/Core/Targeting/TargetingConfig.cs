// Core, no engine references. The starting numbers for the two targeting layers (docs/PHASE1.md 6.1 and 6.5). Tune in M3 and M7.
// Soft target: always on, a cone + range + height band weighted by facing (swings home toward it). Hard lock: a toggle with hysteresis.
// Tunable (Core/Tuning, "targeting"): the defaults are in TargetingTuning and noted beside each value here.
namespace SleepingCalamity.Core
{
    public static class TargetingConfig
    {
        // Soft target (6.5): 60 degrees half-angle, 6 m, +-2 m of height.
        public static float SoftConeHalfAngleDeg => Tuning.TargetingTuning.SoftConeHalfAngleDeg;
        public static float SoftRange => Tuning.TargetingTuning.SoftRange;
        public static float HeightBand => Tuning.TargetingTuning.HeightBand;

        // Hard lock (6.5): acquire inside 10 m, keep until 13 m, 1.0 s of lost line of sight. The 3 m gap is the hysteresis.
        public static float AcquireRange => Tuning.TargetingTuning.AcquireRange;
        public static float ReleaseRange => Tuning.TargetingTuning.ReleaseRange;
        public static float LosGraceSeconds => Tuning.TargetingTuning.LosGraceSeconds;

        // Elites and bosses: a wider release range (26 m) and a longer sight grace (4 s) instead of never dropping.
        public static float EliteReleaseRange => Tuning.TargetingTuning.EliteReleaseRange;
        public static float EliteLosGraceSeconds => Tuning.TargetingTuning.EliteLosGraceSeconds;

        // Not in 6.5 (added in M2): a locked target may sit on a ledge, so the lock's height band is wider than the soft target's (4 m).
        public static float HardHeightBand => Tuning.TargetingTuning.HardHeightBand;

        // Score = FacingWeight * facing + (1 - FacingWeight) * proximity, both 0..1 (higher is better). Default 0.6.
        public static float FacingWeight => Tuning.TargetingTuning.FacingWeight;

        // Right-stick flick (6.1): fires when the stick passes FlickOn (0.75) from rest, re-arms only after it is back under FlickOff (0.35).
        // The new target must lie within FlickConeHalfAngleDeg (60) of the flick direction, measured from the current target.
        public static float FlickOn => Tuning.TargetingTuning.FlickOn;
        public static float FlickOff => Tuning.TargetingTuning.FlickOff;
        public static float FlickConeHalfAngleDeg => Tuning.TargetingTuning.FlickConeHalfAngleDeg;
    }
}
