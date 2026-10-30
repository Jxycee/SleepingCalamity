// Core/Weapons/WeaponStyle.cs: growth beyond kill credit (user-approved 2026-10-30): style-event ABS per heirloom, the tech bonus and its pop
// flag, hero abilities unlocked by weapon milestones, and spell pips. Pure C#, no engine. Every number is a P15 Placeholder.
// Authority: host (it judges the events and applies the ABS); the owner reads pips and abilities.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Weapons
{
    public enum WeaponFamily { None = 0, Axe, Staff, Dagger, Bow }

    // What a weapon does to earn ABS apart from kills.
    public enum StyleEvent { FinisherHit, GuardBreak, Hit, Parry, ExtraSwingHit, WeakPointShot }

    public static class WeaponStyle
    {
        // Placeholder: the most one player can earn from style events on a single enemy (kills are separate, in WeaponGrowth.Credit).
        public const float PerEnemyCap = 6f;

        // The family of a weapon id (an heirloom or its build-up target).
        public static WeaponFamily FamilyOf(string id)
        {
            switch (id)
            {
                case WeaponCatalog.Axe: case WeaponCatalog.CinderAxe: return WeaponFamily.Axe;
                case WeaponCatalog.Staff: case WeaponCatalog.WardenStaff: return WeaponFamily.Staff;
                case WeaponCatalog.DaggerShield: case WeaponCatalog.BaneDagger: return WeaponFamily.Dagger;
                case WeaponCatalog.BowSword: case WeaponCatalog.EmberBow: return WeaponFamily.Bow;
                default: return WeaponFamily.None;
            }
        }

        // ABS one style event pays on a weapon: axe from finisher hits (2) and guard breaks (3); dagger from hits (1) and parries (3); staff from
        // each hit past the first in one swing (1); bow from weak-point shots (2). Any other pairing pays nothing.
        public static float StyleAbs(string weaponId, StyleEvent e)
        {
            switch (FamilyOf(weaponId))
            {
                case WeaponFamily.Axe: return e == StyleEvent.FinisherHit ? 2f : e == StyleEvent.GuardBreak ? 3f : 0f;
                case WeaponFamily.Dagger: return e == StyleEvent.Hit ? 1f : e == StyleEvent.Parry ? 3f : 0f;
                case WeaponFamily.Staff: return e == StyleEvent.ExtraSwingHit ? 1f : 0f;
                case WeaponFamily.Bow: return e == StyleEvent.WeakPointShot ? 2f : 0f;
                default: return 0f;
            }
        }
    }

    // One player's style ABS by enemy, so one target cannot be farmed: at most PerEnemyCap per enemy. Keep one per player (the host holds it
    // while the enemy lives; Forget(enemy) when it dies or despawns).
    public sealed class StyleAbsLedger
    {
        readonly Dictionary<string, float> _paid = new Dictionary<string, float>();

        // The ABS actually granted (0 when the cap is spent).
        public float Award(string enemyId, string weaponId, StyleEvent e)
        {
            float want = WeaponStyle.StyleAbs(weaponId, e);
            if (want <= 0f || enemyId == null) return 0f;
            _paid.TryGetValue(enemyId, out float used);
            float grant = Math.Max(0f, Math.Min(want, WeaponStyle.PerEnemyCap - used));
            _paid[enemyId] = used + grant;
            return grant;
        }

        public void Forget(string enemyId) { if (enemyId != null) _paid.Remove(enemyId); }
    }

    public enum TechKind { Parry, WeakPart, ElementMatch, FinisherKill }

    public readonly struct TechResult
    {
        public readonly float Abs;     // value plus the bonus
        public readonly bool Pop;      // presentation plays the tech "pop"
        public TechResult(float abs, bool pop) { Abs = abs; Pop = pop; }
    }

    public static class TechBonus
    {
        public const float Bonus = 0.5f;   // Placeholder: +50% of the event or kill value

        // A tech event pays value x 1.5 and flags a pop. Several techs on one event do not stack (one bonus). Applies to the value handed in, so
        // a finisher kill's x1.25 (WeaponGrowth.KillValue) is already inside it. No techs: the value unchanged, no pop.
        public static TechResult Apply(float value, params TechKind[] techs)
        {
            if (techs == null || techs.Length == 0) return new TechResult(value, false);
            return new TechResult(value * (1f + Bonus), true);
        }
    }

    // ---- Hero abilities --------------------------------------------------------------------------------------------------------------

    // Data: an ability unlocked by a milestone on a weapon family. MaxTier is its strength with the weapon that unlocked it; with any other
    // weapon the hero keeps it one tier weaker.
    public sealed class AbilityDefinition
    {
        public readonly string Id;
        public readonly WeaponFamily Family;
        public readonly int UnlockLevel;       // the heirloom reached this level (>= 1); 0 when only a build-up unlocks it
        public readonly bool NeedsBuildUp;     // the heirloom was built up
        public readonly int MaxTier;
        public AbilityDefinition(string id, WeaponFamily family, int unlockLevel, bool needsBuildUp, int maxTier)
        { Id = id; Family = family; UnlockLevel = unlockLevel; NeedsBuildUp = needsBuildUp; MaxTier = maxTier; }

        public bool IsMetBy(WeaponState w) =>
            NeedsBuildUp ? w.History.Count > 0 : w.Level >= UnlockLevel || w.History.Count > 0;
    }

    public static class AbilityCatalog
    {
        // Placeholder ids: one at heirloom level 3, one on build-up, per family.
        public static readonly AbilityDefinition[] Default =
        {
            new AbilityDefinition("axe_quake", WeaponFamily.Axe, 3, false, 2),
            new AbilityDefinition("axe_cinder_sweep", WeaponFamily.Axe, 0, true, 2),
            new AbilityDefinition("staff_spark_ward", WeaponFamily.Staff, 3, false, 2),
            new AbilityDefinition("staff_warden_pulse", WeaponFamily.Staff, 0, true, 2),
            new AbilityDefinition("dagger_riposte", WeaponFamily.Dagger, 3, false, 2),
            new AbilityDefinition("dagger_bane_flurry", WeaponFamily.Dagger, 0, true, 2),
            new AbilityDefinition("bow_eagle_eye", WeaponFamily.Bow, 3, false, 2),
            new AbilityDefinition("bow_ember_volley", WeaponFamily.Bow, 0, true, 2),
        };
    }

    // What the hero has unlocked. Unlocks only ever grow: a re-route or weapon swap never takes one away.
    public sealed class HeroAbilityBook
    {
        readonly HashSet<string> _unlocked = new HashSet<string>();
        readonly IReadOnlyList<AbilityDefinition> _defs;

        public HeroAbilityBook(IReadOnlyList<AbilityDefinition> defs = null) { _defs = defs ?? AbilityCatalog.Default; }

        public IReadOnlyCollection<string> Unlocked => _unlocked;
        public bool Has(string id) => id != null && _unlocked.Contains(id);

        // Checks a weapon against the milestones of its family; returns the ids newly unlocked.
        public List<string> Update(WeaponState w)
        {
            var fresh = new List<string>();
            var fam = WeaponStyle.FamilyOf(w.DefinitionId);
            foreach (var d in _defs)
                if (d.Family == fam && !_unlocked.Contains(d.Id) && d.IsMetBy(w)) { _unlocked.Add(d.Id); fresh.Add(d.Id); }
            return fresh;
        }

        public void Restore(IEnumerable<string> ids)
        {
            _unlocked.Clear();
            if (ids == null) return;
            foreach (var id in ids) foreach (var d in _defs) if (d.Id == id) _unlocked.Add(id);
        }

        // The tier usable with the equipped weapon: 0 locked; MaxTier with its own family; MaxTier - 1 (at least 1) with another.
        public int TierWith(string abilityId, WeaponFamily equipped)
        {
            if (!Has(abilityId)) return 0;
            foreach (var d in _defs)
                if (d.Id == abilityId) return d.Family == equipped ? d.MaxTier : Math.Max(1, d.MaxTier - 1);
            return 0;
        }
    }

    // ---- Spell pips ------------------------------------------------------------------------------------------------------------------

    // The staff's (later any active skill's) charges. Landing hits refills them: one pip per HitsPerPip hits. Weapon growth raises the cap.
    public sealed class SpellPips
    {
        public const int StartCap = 3;      // Placeholder
        public const int HitsPerPip = 4;    // Placeholder

        public int Cap { get; private set; }
        public int Current { get; private set; }
        public int HitProgress { get; private set; }   // hits toward the next pip (0..HitsPerPip-1)

        public SpellPips(int cap = StartCap) { Cap = Math.Max(0, cap); Current = Cap; }

        // Placeholder growth: +1 cap per 3 weapon levels and +1 per build-up.
        public static int CapFor(int level, int buildUps) => StartCap + Math.Max(0, level) / 3 + Math.Max(0, buildUps);

        // Raising the cap adds the new room as empty pips; lowering clamps.
        public void SetCap(int cap)
        {
            cap = Math.Max(0, cap);
            Cap = cap;
            if (Current > Cap) Current = Cap;
        }

        // A landed hit. Returns the pips gained (0 or 1). At a full cap hits bank nothing.
        public int OnHit()
        {
            if (Current >= Cap) { HitProgress = 0; return 0; }
            if (++HitProgress < HitsPerPip) return 0;
            HitProgress = 0;
            Current++;
            return 1;
        }

        public bool TrySpend(int n = 1)
        {
            if (n < 0 || Current < n) return false;
            Current -= n;
            return true;
        }
    }
}
