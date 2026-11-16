// Core/Cellars/CollapseStartPlan.cs: what the host does on a tick while the collapse is pending (DiveHaulSync.TickCollapseStart). Pure C#. Authority: none (the host feeds it, only the host acts).
// The collapse timer needs the walk time to this floor's target. A slow floor build must never start it with path 0 (that gives the 60 s minimum on any floor size), so:
//   - no floor sync in the scene (haul-only tests): begin at once with no path;
//   - the floor is built: begin with the built layout's path;
//   - not built yet: wait, up to WaitCapSeconds; past the cap, begin with the path of the layout regenerated from (dive seed, floor) (a pure function, so it is the same floor).
using System;

namespace SleepingCalamity.Core
{
    public enum CollapseStartAction { Wait, BeginBuilt, BeginFromSeed, BeginNoPath }

    public static class CollapseStartPlan
    {
        public const float WaitCapSeconds = 30f;   // placeholder: how long a lagging floor build is waited for

        public static CollapseStartAction Decide(bool hasFloorSync, bool built, float waitedSeconds)
        {
            if (!hasFloorSync) return CollapseStartAction.BeginNoPath;
            if (built) return CollapseStartAction.BeginBuilt;
            return waitedSeconds < WaitCapSeconds ? CollapseStartAction.Wait : CollapseStartAction.BeginFromSeed;
        }

        // The fallback walk time: the same PathSecondsToTarget over the layout the generator makes for (seed, floor). Never 0 on a built floor (a floor with a start and a target).
        public static float FallbackPathSeconds(uint seed, int floor)
        {
            if (floor < 1) return 0f;
            return DiveFloorTable.PathSecondsToTarget(CellarFloorGenerator.Layout(seed, floor));
        }
    }
}
