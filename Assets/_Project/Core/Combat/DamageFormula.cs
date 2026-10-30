// Core/Combat/DamageFormula.cs: the one damage function (docs/PHASE1.md 6.2, P8). Pure C#, no engine.
//   dmg = max(1, ceil(ATK x move x 100/(100+DEF) x (1 + element) x tag x part x crit x other))
// The ratio keeps tier gaps readable and every hit counts. The ceil is the plan's formula (a hit never rounds down to nothing). Every multiplier is a
// named field. Authority: none (a pure function; the host calls it for hits on enemies, the victim's host-side record calls it for hits on players).
using System;

namespace SleepingCalamity.Core.Combat
{
    public struct DamageInput
    {
        public float Atk;           // the attacker's attack stat
        public float Move;          // the move multiplier (finishers already include the combo bonus, see DamageFormula.MoveMultiplier)
        public float Def;           // the target's defence
        public float ElementBonus;  // added to 1: 0.2 means x1.2 (DamageFormula.ElementBonus)
        public float Tag;           // species tag multiplier ("anti-pest"), 1 when none
        public float Part;          // weak-part multiplier, 1 when the body was hit
        public bool Crit;
        public float Other;         // everything else as one named factor: rage, guard (x0.2), a broom's bristles (x0.2)

        public static DamageInput Basic(float atk, float move, float def) =>
            new DamageInput { Atk = atk, Move = move, Def = def, Tag = 1f, Part = 1f, Other = 1f };
    }

    public static class DamageFormula
    {
        public const float CritChance = 0.05f, CritMultiplier = 1.5f;
        public const float ElementPerPoint = 0.02f;   // Placeholder: a weapon's Fire value x 2% against a fire-weak enemy (Fire 10 = +20%)
        public const float TagPerPoint = 0.03f;       // Placeholder: a weapon's Pest value x 3% against a pest enemy (Pest 10 = x1.3)
        public const float ComboBonusPerHit = 0.10f;  // the finisher gains +10% per prior hit in the combo (6.1)
        public const int MinimumDamage = 1;

        // Whole damage for one hit; never below 1, whatever the multipliers (NaN and negatives count as no damage -> the minimum).
        public static int Compute(in DamageInput i)
        {
            float def = i.Def < 0f ? 0f : i.Def;
            float crit = i.Crit ? CritMultiplier : 1f;
            float raw = i.Atk * i.Move * (100f / (100f + def)) * (1f + i.ElementBonus) * i.Tag * i.Part * crit * i.Other;
            if (float.IsNaN(raw) || raw <= 0f) return MinimumDamage;
            if (float.IsInfinity(raw) || raw > 1e9f) return 1_000_000_000;
            int dmg = (int)MathF.Ceiling(raw - 1e-4f);   // the epsilon keeps 12.0000001 (float noise) from becoming 13
            return dmg < MinimumDamage ? MinimumDamage : dmg;
        }

        // A finisher's move multiplier with the combo bonus: base x (1 + 0.10 x prior hits). Other attacks are unchanged. "Prior hits" counts the hits
        // of this chain that connected, so a finisher after two connected hits is x1.2 (1.6 x 1.2 = 1.92) and one that follows two whiffs is plain.
        public static float MoveMultiplier(float baseMove, bool finisher, int priorHits)
        {
            if (!finisher || priorHits <= 0) return baseMove;
            return baseMove * (1f + ComboBonusPerHit * priorHits);
        }

        // The roll is a number in [0,1) from the caller's RNG, so the formula stays deterministic and testable.
        public static bool RollCrit(float roll01, float chance = CritChance) => roll01 >= 0f && roll01 < chance;

        // Fire value of the weapon against an enemy weak to fire (0 against everything else).
        public static float ElementBonus(float weaponFire, bool enemyWeakToFire) => enemyWeakToFire && weaponFire > 0f ? weaponFire * ElementPerPoint : 0f;

        // Pest value of the weapon against a pest-tagged enemy (1 against everything else).
        public static float TagMultiplier(float weaponPest, bool enemyIsPest) => enemyIsPest && weaponPest > 0f ? 1f + weaponPest * TagPerPoint : 1f;
    }
}
