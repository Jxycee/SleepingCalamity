// Core/Weapons/WeaponGrowth.cs: the weapon growth formulas (PHASE1 6.3): ABS per kill, level cost, ATK per level, co-op credit. Pure functions.
// Numbers are P15 Placeholders. Authority: host (the host decides kills and applies the credit).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Weapons
{
    public enum Species { Slime, RatKing, Broom, Elite }

    // One player's hit on an enemy: who, with which weapon (any stable key), and when (seconds on one clock shared with the kill time).
    public readonly struct HitRecord
    {
        public readonly string PlayerId, WeaponKey;
        public readonly double Time;
        public HitRecord(string playerId, string weaponKey, double time) { PlayerId = playerId; WeaponKey = weaponKey; Time = time; }
    }

    public readonly struct AbsCredit
    {
        public readonly string PlayerId, WeaponKey;
        public readonly float Abs;
        public AbsCredit(string playerId, string weaponKey, float abs) { PlayerId = playerId; WeaponKey = weaponKey; Abs = abs; }
    }

    public static class WeaponGrowth
    {
        public const float FinisherMultiplier = 1.25f;
        public const double CreditWindowSeconds = 10.0;   // inclusive: a hit exactly 10.0 s before the kill still counts
        public const int FlatAtkFromLevel = 5;            // +2 ATK per level up to and including level 5, then +1
        public const float BuildUpThreshold = 0.9f;       // build-up opens at 90% of the target's base
        public const float BuildUpBonus = 0.1f;           // +10% of the target's base stats

        public static float SpeciesValue(Species s)
        {
            switch (s)
            {
                case Species.Slime: return 4f;
                case Species.RatKing: return 8f;
                case Species.Broom: return 6f;
                case Species.Elite: return 40f;
                default: return 0f;
            }
        }

        // ABS a kill pays (before it is shared out): the species value, x1.25 for a finisher kill.
        public static float KillValue(Species s, bool finisher) => KillValue(SpeciesValue(s), finisher);
        public static float KillValue(float baseValue, bool finisher) => finisher ? baseValue * FinisherMultiplier : baseValue;

        // ABS needed to go from `level` to level + 1: 40 x (1 + 0.5 x L), as a float.
        public static float LevelCost(int level) => 40f * (1f + 0.5f * level);

        // ATK gained on reaching `newLevel` (1-based): +2 up to level 5, +1 after.
        public static float AtkGain(int newLevel) => newLevel <= FlatAtkFromLevel ? 2f : 1f;

        // Co-op credit: every player with a hit in [killTime - 10 s, killTime] gets the full value on the weapon of their latest such hit.
        // One credit per player, in order of first qualifying hit. Hits after the kill time are ignored.
        public static List<AbsCredit> Credit(IReadOnlyList<HitRecord> hits, double killTime, float killValue)
        {
            var result = new List<AbsCredit>();
            if (hits == null || double.IsNaN(killTime) || double.IsInfinity(killTime)) return result;
            if (float.IsNaN(killValue) || float.IsInfinity(killValue) || killValue <= 0f) return result;
            var latest = new Dictionary<string, HitRecord>();
            var order = new List<string>();
            foreach (var h in hits)
            {
                if (h.PlayerId == null || double.IsNaN(h.Time) || double.IsInfinity(h.Time)) continue;
                if (h.Time > killTime || killTime - h.Time > CreditWindowSeconds) continue;
                if (!latest.TryGetValue(h.PlayerId, out var prev)) order.Add(h.PlayerId);
                if (prev.PlayerId == null || h.Time >= prev.Time) latest[h.PlayerId] = h;
            }
            foreach (var p in order) result.Add(new AbsCredit(p, latest[p].WeaponKey, killValue));
            return result;
        }
    }
}
