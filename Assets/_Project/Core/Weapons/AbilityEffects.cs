// Core/Weapons/AbilityEffects.cs: what the unlocked hero abilities DO in combat (M4, PHASE1 decision notes 2026-10-30). Pure C#, no engine.
// Every effect and number here is a Placeholder, and every number lives in AbilityTuning. The tier rule is HeroAbilityBook.TierWith: MaxTier with
// the ability's own weapon family in hand, one tier weaker on any other weapon (0 = off).
// Authority: host. CombatHost reads these when it applies HP (player hits on enemies, enemy hits on a hero) and nothing is decided on a client.
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core.Weapons
{
    // Placeholder: all ability numbers, per tier, in one spot. Retune here, nowhere else.
    public static class AbilityTuning
    {
        public const float QuakeFinisherPerTier = 0.15f;      // axe_quake: finisher damage x(1 + 0.15 x tier)
        public const float CinderSweepFirePerTier = 5f;       // axe_cinder_sweep: finishers add fire 5 x tier
        public const float SparkWardIncomingPerTier = 0.10f;  // staff_spark_ward: damage taken x(1 - 0.10 x tier)
        public const float WardenPulseSpellPerTier = 0.20f;   // staff_warden_pulse: spell damage x(1 + 0.20 x tier)
        public const float RiposteDamagePerTier = 0.25f;      // dagger_riposte: damage x(1 + 0.25 x tier) ...
        public const float RiposteWindowSeconds = 2f;         // ... for this long after a successful parry
        public const float BaneFlurryPestPerTier = 0.10f;     // dagger_bane_flurry: damage vs pest-tagged enemies x(1 + 0.10 x tier)
        public const float EagleEyeCritPerTier = 0.05f;       // bow_eagle_eye: crit chance + 0.05 x tier
        public const float EmberVolleyFirePerTier = 5f;       // bow_ember_volley: charged attacks add fire 5 x tier
    }

    // What the abilities add to one player hit on an enemy. DamageMult multiplies the whole hit (1 = none).
    public struct AbilityHitMods
    {
        public float DamageMult;
        public float Fire;          // added to the weapon's Fire value (it bites only a fire-weak enemy, like any Fire)
        public float CritChance;    // added to the crit chance
        public static AbilityHitMods None => new AbilityHitMods { DamageMult = 1f };
    }

    public static class AbilityEffects
    {
        public const string Quake = "axe_quake", CinderSweep = "axe_cinder_sweep", SparkWard = "staff_spark_ward", WardenPulse = "staff_warden_pulse",
            Riposte = "dagger_riposte", BaneFlurry = "dagger_bane_flurry", EagleEye = "bow_eagle_eye", EmberVolley = "bow_ember_volley";

        // secondsSinceParry: time since this hero's last successful parry (negative or infinite = none). book may be null (no abilities).
        public static AbilityHitMods OutgoingHit(HeroAbilityBook book, WeaponFamily held, AttackKind kind, bool enemyIsPest, float secondsSinceParry)
        {
            AbilityHitMods m = AbilityHitMods.None;
            if (book == null) return m;
            bool finisher = kind == AttackKind.Finisher;
            int t;
            if (finisher && (t = book.TierWith(Quake, held)) > 0) m.DamageMult *= 1f + AbilityTuning.QuakeFinisherPerTier * t;
            if (finisher && (t = book.TierWith(CinderSweep, held)) > 0) m.Fire += AbilityTuning.CinderSweepFirePerTier * t;
            if (kind == AttackKind.Spell && (t = book.TierWith(WardenPulse, held)) > 0) m.DamageMult *= 1f + AbilityTuning.WardenPulseSpellPerTier * t;
            if (secondsSinceParry >= 0f && secondsSinceParry <= AbilityTuning.RiposteWindowSeconds && (t = book.TierWith(Riposte, held)) > 0)
                m.DamageMult *= 1f + AbilityTuning.RiposteDamagePerTier * t;
            if (enemyIsPest && (t = book.TierWith(BaneFlurry, held)) > 0) m.DamageMult *= 1f + AbilityTuning.BaneFlurryPestPerTier * t;
            if ((t = book.TierWith(EagleEye, held)) > 0) m.CritChance += AbilityTuning.EagleEyeCritPerTier * t;
            if (kind == AttackKind.Charge && (t = book.TierWith(EmberVolley, held)) > 0) m.Fire += AbilityTuning.EmberVolleyFirePerTier * t;
            return m;
        }

        // The factor on damage the hero takes (1 = none).
        public static float IncomingMult(HeroAbilityBook book, WeaponFamily held)
        {
            if (book == null) return 1f;
            int t = book.TierWith(SparkWard, held);
            return t > 0 ? 1f - AbilityTuning.SparkWardIncomingPerTier * t : 1f;
        }
    }
}
