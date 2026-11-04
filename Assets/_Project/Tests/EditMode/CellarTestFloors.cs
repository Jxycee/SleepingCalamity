// Authority: none (pure EditMode test helper). Test tiering (2026-11-01): the Cellar seed sweeps ran ~16 minutes because every test regenerated the same
// (seed, floor) layouts. A sweep is now ONE test body with two [TestCase]s: FastSeeds fixed seeds in the normal tier, and the full count tagged
// [Category("Soak")] (run with `unity.ps1 soak`). Layouts, dressings and chunk plans are generated once per (seed, floor) and shared by every test in the run.
// The cached objects are shared: never mutate them (tests that cut or edit a layout build a new CellarFloorLayout from its parts, or call Generate directly).
using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public static class CellarTestFloors
    {
        /// <summary>Seed count of the fast twin of every Soak sweep (the Soak case keeps the original count).</summary>
        public const int FastSeeds = 60;

        /// <summary>The built dive floors that are not stub set pieces: the ones the layered systems (dressing, vignettes, clutter, trim, healing) run on. Floors 3 and 5 are stubs.</summary>
        public static readonly int[] DiveFloors = { 1, 2, 4, 6, 7 };

        static readonly Dictionary<(uint, int), CellarFloorLayout> Layouts = new Dictionary<(uint, int), CellarFloorLayout>();
        static readonly Dictionary<(uint, int), CellarFloorDressing> Dressings = new Dictionary<(uint, int), CellarFloorDressing>();
        static readonly Dictionary<(uint, int), CellarFloorClutter> Clutters = new Dictionary<(uint, int), CellarFloorClutter>();
        static readonly Dictionary<(uint, int), CellarFloorTrim> Trims = new Dictionary<(uint, int), CellarFloorTrim>();
        static readonly Dictionary<(uint, int), CellarFloorWarmLights> Lights = new Dictionary<(uint, int), CellarFloorWarmLights>();
        static readonly Dictionary<(uint, int), CellarRoomChunks> Chunks = new Dictionary<(uint, int), CellarRoomChunks>();

        public static CellarFloorLayout Layout(uint seed, int floor)
        {
            if (!Layouts.TryGetValue((seed, floor), out CellarFloorLayout l)) Layouts[(seed, floor)] = l = CellarFloorGenerator.Layout(seed, floor);
            return l;
        }

        public static CellarFloorDressing Dressing(uint seed, int floor)
        {
            if (!Dressings.TryGetValue((seed, floor), out CellarFloorDressing d)) Dressings[(seed, floor)] = d = CellarFloorDressing.Resolve(Layout(seed, floor));
            return d;
        }

        public static CellarFloorClutter Clutter(uint seed, int floor)
        {
            if (!Clutters.TryGetValue((seed, floor), out CellarFloorClutter c)) Clutters[(seed, floor)] = c = CellarFloorClutter.Resolve(Layout(seed, floor), Dressing(seed, floor), ChunkPlan(seed, floor));
            return c;
        }

        public static CellarFloorTrim Trim(uint seed, int floor)
        {
            if (!Trims.TryGetValue((seed, floor), out CellarFloorTrim t)) Trims[(seed, floor)] = t = CellarFloorTrim.Resolve(Layout(seed, floor), Dressing(seed, floor), ChunkPlan(seed, floor));
            return t;
        }

        public static CellarFloorWarmLights WarmLights(uint seed, int floor)
        {
            if (!Lights.TryGetValue((seed, floor), out CellarFloorWarmLights w)) Lights[(seed, floor)] = w = CellarFloorWarmLights.Resolve(Layout(seed, floor), Dressing(seed, floor), ChunkPlan(seed, floor), Clutter(seed, floor), Trim(seed, floor));
            return w;
        }

        public static CellarRoomChunks ChunkPlan(uint seed, int floor)
        {
            if (!Chunks.TryGetValue((seed, floor), out CellarRoomChunks c)) Chunks[(seed, floor)] = c = CellarRoomChunks.Resolve(Layout(seed, floor), Dressing(seed, floor));
            return c;
        }
    }
}
