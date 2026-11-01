// Core/Cellars/CellarLayoutPreset.cs: the per-floor layout preset of the Undercroft Cellars generator (procgen item 2; research procedural-dungeons 3 item 7).
// Pure C#, no engine references. Authority: none (a pure function of (seed, floor); every peer derives the same preset).
// A preset steers the existing generator steps (which earlier room a new room links to, the placement order, the loop rules); it is not a second generator.
//   Spine  a long chain: rooms mostly link to the newest room; 0-1 loops.
//   Ring   a guaranteed entry-to-join loop with two arcs (rooms split across two routes that meet before the exit, which hangs off the join room as a leaf); at most 1 extra loop.
//   Hub    a big combat room placed right after the entry that most rooms link to; 1-2 loops.
//   Wing   two branches off the entry; the exit ends the larger one; one loop that stays inside the other (dead) branch, so it never joins the two halves of the path.
// Floors 1-3 pick one on its own stream Rng.Derive(seed, "cellars.preset", floor), never the same as the floor before (the previous pick is replayed from the seed).
// Floor 4 (the fixed bakery) has no preset.
using System;

namespace SleepingCalamity.Core
{
    public enum LayoutPreset : byte { Spine = 0, Ring = 1, Hub = 2, Wing = 3 }

    public static class CellarLayoutPresets
    {
        // PLACEHOLDER (tuning, not tested for feel): relative weights in enum order (Spine, Ring, Hub, Wing). The previous floor's preset is dropped before drawing.
        public static readonly int[] Weights = { 30, 30, 20, 20 };

        // Pure in (seed, floor); floors 1-3.
        public static LayoutPreset PresetFor(uint seed, int floor)
        {
            if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor), "presets are for floors 1-3");
            int prev = floor > 1 ? (int)PresetFor(seed, floor - 1) : -1;
            var rng = new Pcg32(Rng.Derive(seed, "cellars.preset", (ulong)floor));
            int total = 0;
            for (int i = 0; i < Weights.Length; i++) if (i != prev) total += Weights[i];
            int roll = rng.Range(0, total);
            for (int i = 0; i < Weights.Length; i++)
            {
                if (i == prev) continue;
                if (roll < Weights[i]) return (LayoutPreset)i;
                roll -= Weights[i];
            }
            throw new InvalidOperationException("unreachable");
        }
    }
}
