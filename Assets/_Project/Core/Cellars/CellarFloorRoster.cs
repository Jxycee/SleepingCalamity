// Core/Cellars/CellarFloorRoster.cs: per-floor enemy roster subset and one floor twist of the Undercroft Cellars (procgen item 3; research procedural-dungeons takeaways).
// Pure function of (seed, floor) on its own streams "cellars.roster" / "cellars.twist" / "cellars.twist.rooms": no layout, dressing, mix, atmosphere or encounter stream moves.
//   - Roster: the Tier1 kinds a floor may use are those CellarEnemyMix weights above zero. Floors 2-3 draw a subset (all eligible, or all but one); floor 1 (one kind) and floor 4
//     (bakery set piece: boss/hunter/elite vignette untouched) keep the full set. Never empty. Spawners remap a rolled kind that is not on the roster to one that is.
//   - Twist: at most one of None / Ambush / Thin / Crowded per floor, floors 2-3 only (never floor 1 or 4), and one twist kind repeats at most once per dive
//     (earlier floors are replayed from the seed, as CellarAtmosphere.IsDarkFloor does).
// Authority: none (static data derived from the dive seed; every peer computes the same answer).
using System;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public enum FloorTwist : byte { None = 0, Ambush = 1, Thin = 2, Crowded = 3 }

    // PLACEHOLDER tuning in ONE spot (percent of floors 2-3; the rest is None). The user's three-run check decides the real numbers.
    public static class FloorTwistTuning
    {
        public const int AmbushPercent = 12;   // procgen 3b ambush, deliberate: placeholder, the user's three-run check decides the real number
        public const float AmbushDelaySeconds = 0.75f;   // placeholder: how long after a player enters the ambush room its enemies spring at the ring cells
        public const int ThinPercent = 12;
        public const int CrowdedPercent = 12;
        public const int FirstTwistFloor = 2, LastTwistFloor = 3;
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
            if (floor < 2 || floor > 3 || count < 2) return eligible;
            var rng = new Pcg32(Rng.Derive(seed, "cellars.roster", (ulong)floor));
            if (rng.Range(0, 2) == 0) return eligible;          // the full set
            int drop = rng.Range(0, count), seen = 0;            // all but one: which one goes
            for (int b = 1; b <= 4; b <<= 1)
                if ((eligible & b) != 0) { if (seen++ == drop) return eligible & ~b; }
            return eligible;
        }

        static FloorTwist RollTwist(uint seed, int floor)
        {
            int r = new Pcg32(Rng.Derive(seed, "cellars.twist", (ulong)floor)).Range(0, 100);
            if (r < FloorTwistTuning.AmbushPercent) return FloorTwist.Ambush;
            r -= FloorTwistTuning.AmbushPercent;
            if (r < FloorTwistTuning.ThinPercent) return FloorTwist.Thin;
            r -= FloorTwistTuning.ThinPercent;
            if (r < FloorTwistTuning.CrowdedPercent) return FloorTwist.Crowded;
            return FloorTwist.None;
        }

        // The twist of a floor: a pure function of (seed, floor); a kind already used by an earlier floor of the dive becomes None.
        public static FloorTwist TwistOf(uint seed, int floor)
        {
            if (floor < FloorTwistTuning.FirstTwistFloor || floor > FloorTwistTuning.LastTwistFloor) return FloorTwist.None;
            int used = 0;
            for (int f = FloorTwistTuning.FirstTwistFloor; f <= floor; f++)
            {
                FloorTwist t = RollTwist(seed, f);
                if (t != FloorTwist.None && (used & (1 << (int)t)) != 0) t = FloorTwist.None;
                if (f == floor) return t;
                used |= 1 << (int)t;
            }
            return FloorTwist.None;
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
