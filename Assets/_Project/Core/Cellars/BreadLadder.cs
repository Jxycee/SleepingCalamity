// Core/Cellars/BreadLadder.cs: bread as the free, upgradable heal (spec decisions 20, 27; section 4.8). Pure C#.
// Survivors' bread before the baker, his bread after, +HealPerUpgrade per baker upgrade; free loaves plus baked extras, capped at StackCap.
// Replaces HeroVitals.LoafHeal and BakerService's loaf count in plan 3.
using System;

namespace SleepingCalamity.Core
{
    public static class BreadLadder
    {
        public const int SurvivorsHeal = 15, BakerHeal = 30, HealPerUpgrade = 10;   // placeholders
        public const int StackCap = 10, FreeLoavesPerDive = 3;                      // free loaves: 3-5 to tune

        public static int HealPerLoaf(bool bakerHome, int bakerUpgrades) =>
            bakerHome ? BakerHeal + HealPerUpgrade * Math.Max(0, bakerUpgrades) : SurvivorsHeal;

        public static int LoavesAtDiveStart(int extraBaked) => Math.Min(StackCap, FreeLoavesPerDive + Math.Max(0, extraBaked));
    }
}
