// Core/Cellars/CellarVignetteTuning.cs: the ONE place for the state-conditioned vignette odds (procgen item 5). Every number here is a PLACEHOLDER for the user's
// three-run check; none is tested for feel. VignetteDive.Select multiplies a vignette's draw probability by Percent/100, where Percent is a pure function of
// (floor depth, the floor's special-room kind, what earlier floors of the same dive showed). No runtime player state (stir, hp) ever enters: host and peers agree.
// Authority: none (static data).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarVignetteTuning
    {
        // Depth: percent by category (index VignetteCategory: Fluff, Reward, RiskReward, Danger) for floors 1, 2 and 3+ (PLACEHOLDER).
        static readonly int[][] DepthPercent =
        {
            new[] { 130, 100,  80 },   // Fluff: more on the quiet first floor
            new[] {  80, 100, 120 },   // Reward
            new[] {  80, 100, 120 },   // RiskReward
            new[] {  70, 100, 130 },   // Danger: more as the dive goes deeper
        };

        // Special-room kind of the floor: percent by category (Fluff, Reward, RiskReward, Danger) (PLACEHOLDER). A Treasury floor is rich enough already; a Quiet floor
        // reads as calm; an elite den wants fewer extra threats and a little relief.
        static int[] KindPercent(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.EliteDen: return new[] { 100, 140, 100,  60 };
                case SpecialKind.Treasury: return new[] { 120,  50,  60, 100 };
                case SpecialKind.Shrine:   return new[] { 150, 100, 100,  80 };
                case SpecialKind.Quiet:    return new[] { 150, 100,  90,  70 };
                default:                   return new[] { 100, 100, 100, 100 };
            }
        }

        public const int FamilyRepeatPercent = 40;      // a vignette whose family showed on the previous floor (PLACEHOLDER)
        public const int CategoryFatigueAfter = 2;      // a category already shown this many times earlier in the dive ...
        public const int CategoryFatiguePercent = 70;   // ... draws at this percent (PLACEHOLDER)
        public const int MinPercent = 10, MaxPercent = 400;

        static readonly Dictionary<string, string> Families = new Dictionary<string, string>
        {
            { "abandoned_camp", "camp" }, { "broken_handcart", "camp" }, { "menders_bench", "camp" },
            { "toppled_wine_rack", "stores" }, { "collapsed_shelf", "stores" }, { "lantern_cache", "stores" }, { "hanging_larder", "stores" }, { "crate_stack_nest", "stores" },
            { "childs_drawing", "keepers" }, { "keepers_lectern", "keepers" }, { "cracked_idol", "keepers" },
            { "flooded_alcove", "damp" }, { "pale_cap_ring", "damp" },
            { "sleeping_elite", "lair" }, { "bone_nest", "lair" }, { "tripwire_bell", "lair" },
        };

        // The look-alike group of a vignette (two of one family on back-to-back floors read as a repeat). Unlisted ids are their own family.
        public static string FamilyOf(string id) => Families.TryGetValue(id, out string f) ? f : id;

        // percent (100 = unchanged) for vignette v on floor `floor`; catSeen = vignettes shown per category on earlier floors; prevFamilies = families shown on the previous floor.
        public static int Percent(CellarVignette v, int floor, SpecialKind special, int[] catSeen, ISet<string> prevFamilies)
        {
            int c = (int)v.Category;
            long p = 100;
            p = p * DepthPercent[c][Math.Min(floor, 3) - 1] / 100;
            p = p * KindPercent(special)[c] / 100;
            if (prevFamilies.Contains(FamilyOf(v.Id))) p = p * FamilyRepeatPercent / 100;
            if (catSeen[c] >= CategoryFatigueAfter) p = p * CategoryFatiguePercent / 100;
            return (int)Math.Max(MinPercent, Math.Min(MaxPercent, p));
        }
    }
}
