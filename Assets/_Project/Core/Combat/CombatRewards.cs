// Core/Combat/CombatRewards.cs: what a confirmed hit or kill pays, using the M4 rules (WeaponGrowth, WeaponStyle, TechBonus). No growth math lives here:
// this only decides WHICH of those rules a combat event triggers and hands the ABS to the right WeaponState. Pure C#.
// Authority: host. Only host-confirmed hits and kills reach it. Each player has one PlayerGrowth (their weapons, their style ledger, their pips).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Core.Combat
{
    // One player's growth state on the host for this arena session (M6 will back it with the save; M3 starts every session with fresh heirlooms).
    public sealed class PlayerGrowth
    {
        public readonly string PlayerId;
        public readonly Dictionary<string, WeaponState> Weapons = new Dictionary<string, WeaponState>();
        public readonly StyleAbsLedger Ledger = new StyleAbsLedger();
        public readonly SpellPips Pips = new SpellPips();
        public readonly HeroAbilityBook Abilities = new HeroAbilityBook();

        public PlayerGrowth(string playerId)
        {
            PlayerId = playerId;
            foreach (string id in AttackCatalog.Heirlooms) Weapons[id] = WeaponState.NewHeirloom(id);
        }

        readonly Dictionary<string, float> _techPaid = new Dictionary<string, float>();
        public const float TechBaseValue = 1f, TechBasePerEnemyCap = 3f;   // Placeholder: a tech with no style ABS is worth 1 base point, at most 3 per enemy

        // The base value a tech event is worth when the hit paid no style ABS: 1, until the per-enemy cap is spent, then 0 (a farmed weak point pays nothing).
        public float TechBase(string enemyId)
        {
            if (enemyId == null) return 0f;
            _techPaid.TryGetValue(enemyId, out float used);
            float give = Math.Max(0f, Math.Min(TechBaseValue, TechBasePerEnemyCap - used));
            _techPaid[enemyId] = used + give;
            return give;
        }

        public void ForgetEnemy(string enemyId) { Ledger.Forget(enemyId); if (enemyId != null) _techPaid.Remove(enemyId); }

        public WeaponState Weapon(string weaponId)
        {
            if (!Weapons.TryGetValue(weaponId, out WeaponState w))
            {
                w = WeaponState.NewHeirloom(weaponId);
                Weapons[weaponId] = w;
            }
            return w;
        }
    }

    public struct AbsGain
    {
        public string PlayerId, WeaponKey;
        public float Abs;
        public bool TechPop;
        public int LevelsGained;
    }

    public static class CombatRewards
    {
        // After a confirmed hit on a living enemy: style ABS (one StyleEvent per rule that the hit satisfies), tech bonuses, spell-pip refill.
        // `hitsInSwing` counts this swing's hits so far including this one (the staff pays from the second). Returns the ABS granted (0 when none).
        public static AbsGain OnHit(PlayerGrowth p, string weaponKey, string enemyId, AttackData attack, in EnemyHitResult result, int hitsInSwing, bool shotHitWeakPart)
        {
            if (p == null || attack == null) return default;
            var gain = new AbsGain { PlayerId = p.PlayerId, WeaponKey = weaponKey };
            WeaponState weapon = p.Weapon(weaponKey);
            float abs = 0f;
            bool tech = false;

            switch (WeaponStyle.FamilyOf(weaponKey))
            {
                case WeaponFamily.Axe:
                    if (attack.IsFinisher) abs += p.Ledger.Award(enemyId, weaponKey, StyleEvent.FinisherHit);
                    if (result.GuardBreakHit) { abs += p.Ledger.Award(enemyId, weaponKey, StyleEvent.GuardBreak); tech = true; }
                    break;
                case WeaponFamily.Dagger:
                    abs += p.Ledger.Award(enemyId, weaponKey, StyleEvent.Hit);
                    break;
                case WeaponFamily.Staff:
                    if (hitsInSwing >= 2) abs += p.Ledger.Award(enemyId, weaponKey, StyleEvent.ExtraSwingHit);
                    break;
                case WeaponFamily.Bow:
                    if (attack.Kind == AttackKind.Shot && (shotHitWeakPart || result.WeakPart)) { abs += p.Ledger.Award(enemyId, weaponKey, StyleEvent.WeakPointShot); }
                    break;
            }
            if (result.WeakPart) tech = true;
            if (result.ElementMatch) tech = true;

            // A tech event pays its value x1.5 and pops; with no style ABS in the event the tech itself is worth a base point (capped per enemy).
            if (tech)
            {
                TechResult t = TechBonus.Apply(abs > 0f ? abs : p.TechBase(enemyId), TechKind.WeakPart);
                abs = t.Abs;
                gain.TechPop = t.Pop && t.Abs > 0f;
            }

            if (attack.Kind != AttackKind.Spell) p.Pips.OnHit();   // landing hits refills the staff's pips (the spell itself does not)
            if (abs > 0f) { gain.LevelsGained = weapon.AddAbs(abs); gain.Abs = abs; }
            return gain;
        }

        // A parry on a telegraphed attack: the dagger's parry ABS and a tech pop (any weapon can parry only with the dagger, P46, so no family check).
        public static AbsGain OnParry(PlayerGrowth p, string weaponKey, string enemyId)
        {
            var gain = new AbsGain { PlayerId = p.PlayerId, WeaponKey = weaponKey };
            float abs = p.Ledger.Award(enemyId, weaponKey, StyleEvent.Parry);
            TechResult t = TechBonus.Apply(abs > 0f ? abs : 1f, TechKind.Parry);
            gain.Abs = t.Abs;
            gain.TechPop = t.Pop;
            gain.LevelsGained = p.Weapon(weaponKey).AddAbs(t.Abs);
            return gain;
        }

        // The killing blow: every player who hit in the last 10 s gets the species value on the weapon of their latest hit (P12). A finisher kill pays
        // x1.25, and for the player who landed it the tech bonus on top, with a pop.
        public static List<AbsGain> OnKill(IReadOnlyDictionary<string, PlayerGrowth> players, IReadOnlyList<HitRecord> hits, double killTime, Species species,
            string killerId, bool finisherKill)
        {
            var result = new List<AbsGain>();
            float value = WeaponGrowth.KillValue(species, finisherKill);
            foreach (AbsCredit c in WeaponGrowth.Credit(hits, killTime, value))
            {
                if (!players.TryGetValue(c.PlayerId, out PlayerGrowth p)) continue;
                float abs = c.Abs;
                bool pop = false;
                if (finisherKill && c.PlayerId == killerId)
                {
                    TechResult t = TechBonus.Apply(abs, TechKind.FinisherKill);
                    abs = t.Abs;
                    pop = t.Pop;
                }
                int levels = p.Weapon(c.WeaponKey).AddAbs(abs);
                result.Add(new AbsGain { PlayerId = c.PlayerId, WeaponKey = c.WeaponKey, Abs = abs, TechPop = pop, LevelsGained = levels });
            }
            return result;
        }
    }
}
