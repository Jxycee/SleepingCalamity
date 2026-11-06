// Core/Cellars/CellarFloorRoster.cs: per-floor enemy roster subset and one floor twist of the Undercroft Cellars (procgen item 3; research procedural-dungeons takeaways).
// Pure function of (seed, floor) on its own streams "cellars.roster" / "cellars.modifier" (via FloorModifierSet) / "cellars.twist.rooms": no layout, dressing, mix, atmosphere or encounter stream moves.
//   - Roster: the Tier1 kinds a floor may use are those CellarEnemyMix weights above zero (floors past 4 use the floor-4 band). The generated floors (2, 4, 6, 7) draw a subset (all eligible, or all but one);
//     the authored and set-piece floors (1, 3, 5) keep the full set. Never empty. Spawners remap a rolled kind that is not on the roster to one that is.
//   - Twist: derived from the floor's modifiers (plan 5): Crowded / Ambush when slot A is that hazard, Thin when slot B is Sparse, else None. The rates, the repeat rule and the printed
//     card live in FloorModifiers.cs; there is no second hidden roll.
// Authority: none (static data derived from the dive seed; every peer computes the same answer).
using System;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public enum FloorTwist : byte { None = 0, Ambush = 1, Thin = 2, Crowded = 3 }

    // PLACEHOLDER tuning in ONE spot. The twist rates moved into FloorModifierTuning (plan 5 decision 3).
    public static class FloorTwistTuning
    {
        public const float AmbushDelaySeconds = 0.75f;   // placeholder: how long after a player enters the ambush room its enemies spring at the ring cells
        public const int FirstTwistFloor = 2;   // twists run on the generated floors (2, 4, 6, 7), from here on
    }

    public static class CellarFloorRoster
    {
        public const int SlimeBit = 1, RatKingBit = 2, BroomBit = 4;
        public static int BitOf(EnemyKind k) => k == EnemyKind.Slime ? SlimeBit : k == EnemyKind.RatKing ? RatKingBit : k == EnemyKind.Broom ? BroomBit : 0;

        // Tier1 kinds the floor-banded mix can produce on this floor (bit mask over Slime / RatKing / Broom).
        public static int EligibleMask(int floor)
        {
            int[] w = CellarEnemyMix.WeightsFor(floor);   // Slime, Broom, RatKing
            return (w[0] > 0 ? SlimeBit : 0) | (w[1] > 0 ? BroomBit : 0) | (w[2] > 0 ? RatKingBit : 0);
        }

        // The roster of one floor: a non-empty subset of EligibleMask(floor).
        public static int RosterMask(uint seed, int floor)
        {
            int eligible = EligibleMask(floor);
            int count = 0; for (int b = 1; b <= 4; b <<= 1) if ((eligible & b) != 0) count++;
            if (!CellarFloorGenerator.IsGeneratedFloor(floor) || count < 2) return eligible;
            var rng = new Pcg32(Rng.Derive(seed, "cellars.roster", (ulong)floor));
            if (rng.Range(0, 2) == 0) return eligible;          // the full set
            int drop = rng.Range(0, count), seen = 0;            // all but one: which one goes
            for (int b = 1; b <= 4; b <<= 1)
                if ((eligible & b) != 0) { if (seen++ == drop) return eligible & ~b; }
            return eligible;
        }

        // The twist of a floor: a pure function of (seed, floor), derived from its modifiers (Crowded / Ambush hazard, Sparse boon).
        public static FloorTwist TwistOf(uint seed, int floor)
        {
            FloorModifierSet m = FloorModifierSet.For(seed, floor);
            return m.A == FloorModifier.Crowded ? FloorTwist.Crowded : m.A == FloorModifier.Ambush ? FloorTwist.Ambush : m.B == FloorModifier.Sparse ? FloorTwist.Thin : FloorTwist.None;
        }

        // Hash of the layer for (seed, floor): roster mask and twist kind (the room picks are part of the plan, not this layer).
        public static ulong LayerHash(uint seed, int floor)
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(RosterMask(seed, floor)); h.AppendInt32LE((int)TwistOf(seed, floor));
            return h.Value;
        }
    }
}
