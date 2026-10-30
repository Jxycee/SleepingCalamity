// Catches: the host's hit resolution (EnemyModel.ApplyHit) and the reward routing (CombatRewards) drifting from 6.3 / 6.6: tier stats, the broom's 80%
// block and the guard-breaking finisher, weak-part multipliers and parts that drop healing once, the elite never staggering, kills crediting every
// recent attacker once, the tech bonus pop, and the per-enemy style/tech cap.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode
{
    public class EnemyModelTests
    {
        static EnemyHitInput Hit(AttackData a, float atk = 22f, float bearing = 0f, int part = -1, int prior = 0, float fire = 0f, float pest = 0f) => new EnemyHitInput
        {
            Attack = a, Atk = atk, Fire = fire, Pest = pest, PriorChainHits = prior, BearingToAttackerDeg = bearing, PartIndex = part, CritRoll = 0.9f, CritChance = 0.05f,
        };

        static AttackData Axe(int i) => AttackCatalog.Axe.Attacks[i];

        [Test]
        public void Tier2_UsesTheDc2FamilyRatios()
        {
            var t1 = new EnemyModel(EnemyCatalog.Broom, 1, 1);
            var t2 = new EnemyModel(EnemyCatalog.Broom, 2, 1);
            Assert.That(t1.MaxHp, Is.EqualTo(60));
            Assert.That(t2.MaxHp, Is.EqualTo(180), "life x3");
            Assert.That(t2.Atk, Is.EqualTo(15f * 2.3f).Within(1e-4f), "attack x2.3");
            Assert.That(t2.DefStat, Is.EqualTo(15f * 3.3f).Within(1e-4f), "defence x3.3");
        }

        [Test]
        public void StatsTable_MatchesSection66()
        {
            Assert.That(new[] { EnemyCatalog.Slime.Hp, EnemyCatalog.RatKing.Hp, EnemyCatalog.Broom.Hp, EnemyCatalog.DoughWarden.Hp }, Is.EqualTo(new[] { 40, 90, 60, 600 }));
            Assert.That(new[] { EnemyCatalog.Slime.Atk, EnemyCatalog.RatKing.Atk, EnemyCatalog.Broom.Atk, EnemyCatalog.DoughWarden.Atk }, Is.EqualTo(new[] { 12f, 18f, 15f, 30f }));
            Assert.That(new[] { EnemyCatalog.Slime.Def, EnemyCatalog.RatKing.Def, EnemyCatalog.Broom.Def, EnemyCatalog.DoughWarden.Def }, Is.EqualTo(new[] { 5f, 10f, 15f, 20f }));
        }

        [Test]
        public void AxeHit_OnSlime_DealsRatioDamage_AndStaggers()
        {
            var e = new EnemyModel(EnemyCatalog.Slime);
            EnemyHitResult r = e.ApplyHit(Hit(Axe(AttackSet.Hit1)));
            Assert.That(r.Damage, Is.EqualTo(21), "22 x 100/105 = 20.95");
            Assert.That(e.Hp, Is.EqualTo(19));
            Assert.That(r.Staggered, Is.True);
            Assert.That(r.Knockback, Is.GreaterThan(0f));
            Assert.That(r.Killed, Is.False);
        }

        [Test]
        public void Overkill_ClampsDamageToTheLifeLeft_AndKills()
        {
            var e = new EnemyModel(EnemyCatalog.Slime);
            e.ApplyHit(Hit(Axe(AttackSet.Hit1)));
            EnemyHitResult r = e.ApplyHit(Hit(Axe(AttackSet.Hit2)));
            Assert.That(r.Killed, Is.True);
            Assert.That(r.Damage, Is.EqualTo(19));
            Assert.That(e.Alive, Is.False);
            Assert.That(e.ApplyHit(Hit(Axe(AttackSet.Hit1))).Damage, Is.EqualTo(0), "a dead enemy takes nothing");
        }

        [Test]
        public void Broom_BlocksEightyPercentOfFrontalHits_NotFlankOrBack()
        {
            var front = new EnemyModel(EnemyCatalog.Broom);
            var back = new EnemyModel(EnemyCatalog.Broom);
            EnemyHitResult f = front.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 10f));
            EnemyHitResult b = back.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 150f));
            Assert.That(f.Blocked, Is.True);
            Assert.That(b.Blocked, Is.False);
            Assert.That(b.Damage, Is.EqualTo(20), "22 x 100/115 = 19.1");
            Assert.That(f.Damage, Is.EqualTo(4), "19.13 x 0.2 = 3.8");
            Assert.That(b.Damage, Is.GreaterThan(f.Damage * 4));
        }

        [Test]
        public void AxeFinisher_BreaksThroughTheBristles()
        {
            var e = new EnemyModel(EnemyCatalog.Broom);
            EnemyHitResult r = e.ApplyHit(Hit(Axe(AttackSet.Finisher), bearing: 0f, prior: 2));
            Assert.That(r.Blocked, Is.False, "the finisher is a guard-breaker");
            Assert.That(r.GuardBreakHit, Is.True);
            Assert.That(r.Damage, Is.EqualTo(37), "22 x 1.92 x 100/115 = 36.7");
        }

        [Test]
        public void FireAxe_AgainstTheFireWeakBroom_GetsTheElementBonus_AndTheTagOnlyOnPests()
        {
            var broom = new EnemyModel(EnemyCatalog.Broom);
            EnemyHitResult r = broom.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 30f, bearing: 150f, fire: 10f, pest: 10f));
            Assert.That(r.ElementMatch, Is.True);
            Assert.That(r.Damage, Is.EqualTo(32), "30 x 100/115 x 1.2 = 31.3; the broom is no pest, so Pest 10 does nothing");
            var rat = new EnemyModel(EnemyCatalog.RatKing);
            EnemyHitResult p = rat.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 15f, pest: 10f));
            Assert.That(p.ElementMatch, Is.False);
            Assert.That(p.Damage, Is.EqualTo(18), "15 x 100/110 x 1.3 = 17.7");
        }

        [Test]
        public void Elite_NeverStaggers_AndGainsRevenge_PartyScaled()
        {
            var solo = new EnemyModel(EnemyCatalog.DoughWarden, 1, 1);
            var duo = new EnemyModel(EnemyCatalog.DoughWarden, 1, 2);
            Assert.That(solo.MaxHp, Is.EqualTo(600));
            Assert.That(duo.MaxHp, Is.EqualTo(1020), "600 x 1.7");
            EnemyHitResult r = solo.ApplyHit(Hit(Axe(AttackSet.Finisher), prior: 2));
            Assert.That(r.Staggered, Is.False, "super armour");
            Assert.That(r.Knockback, Is.EqualTo(0f));
            Assert.That(solo.Revenge.Value, Is.EqualTo(3f));

            int hits = 0;
            bool breaks = false;
            while (!breaks && hits < 50) { hits++; breaks = solo.ApplyHit(Hit(Axe(AttackSet.Charge))).RevengeTriggered; }
            Assert.That(breaks, Is.True);
            Assert.That(hits, Is.EqualTo(3), "3 + 4 + 4 + 4 crosses 12 on the third charge");

            int hitsDuo = 0;
            bool breaksDuo = false;
            while (!breaksDuo && hitsDuo < 50) { hitsDuo++; breaksDuo = duo.ApplyHit(Hit(Axe(AttackSet.Charge))).RevengeTriggered; }
            Assert.That(hitsDuo, Is.EqualTo(5), "cap 18 = five charges of 4");
        }

        [Test]
        public void WardenPart_WeakPointMultiplies_BreaksOnce_AndFlagsTheDrop()
        {
            var e = new EnemyModel(EnemyCatalog.DoughWarden);
            EnemyHitResult body = e.ApplyHit(Hit(Axe(AttackSet.Hit1)));
            var e2 = new EnemyModel(EnemyCatalog.DoughWarden);
            EnemyHitResult part = e2.ApplyHit(Hit(Axe(AttackSet.Hit1), part: 0));
            Assert.That(part.WeakPart, Is.True);
            Assert.That(part.Damage, Is.GreaterThan(body.Damage), "x1.5 on the weak point");

            int broken = -1, brokenTimes = 0;
            for (int i = 0; i < 40; i++)
            {
                EnemyHitResult r = e2.ApplyHit(Hit(Axe(AttackSet.Hit1), part: 0));
                if (r.PartBroken >= 0) { broken = r.PartBroken; brokenTimes++; }
            }
            Assert.That(broken, Is.EqualTo(0));
            Assert.That(brokenTimes, Is.EqualTo(1), "one healing drop per part");
            Assert.That(e2.Parts[0].Broken, Is.True);
            Assert.That(e2.Parts[1].Broken, Is.False, "the other part is untouched");
        }

        [Test]
        public void SlimeGetsAngry_AfterSevenHits_ButTheEliteDoesNot()
        {
            var rat = new EnemyModel(EnemyCatalog.RatKing, 2, 1);   // lots of life, so seven hits do not kill it
            bool started = false;
            for (int i = 0; i < 7; i++) started |= rat.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 10f)).RageStarted;
            Assert.That(started, Is.True);
            var warden = new EnemyModel(EnemyCatalog.DoughWarden);
            bool wardenRage = false;
            for (int i = 0; i < 10; i++) wardenRage |= warden.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 5f)).RageStarted;
            Assert.That(wardenRage, Is.False);
        }

        [Test]
        public void Rage_RaisesWhatTheEnemyDealsToPlayers()
        {
            var e = new EnemyModel(EnemyCatalog.RatKing);
            int calm = e.AttackDamage(AttackCatalog.RatLunge, PlayerVitals.BaseDef);
            for (int i = 0; i < 7; i++) e.Rage.OnHit();
            e.Rage.Tick(1f);
            int raging = e.AttackDamage(AttackCatalog.RatLunge, PlayerVitals.BaseDef);
            Assert.That(calm, Is.EqualTo(18), "18 x 1.1 x 100/110 = 18.0");
            Assert.That(raging, Is.EqualTo(24), "x1.3");
        }

        // ---- rewards ----

        [Test]
        public void Kill_CreditsEveryRecentAttackerOnce_FinisherPaysMore()
        {
            var players = new Dictionary<string, PlayerGrowth> { ["a"] = new PlayerGrowth("a"), ["b"] = new PlayerGrowth("b") };
            var hits = new List<HitRecord>
            {
                new HitRecord("a", WeaponCatalog.Axe, 100.0), new HitRecord("a", WeaponCatalog.Axe, 102.0),
                new HitRecord("b", WeaponCatalog.DaggerShield, 101.0),
            };
            var gains = CombatRewards.OnKill(players, hits, 103.0, Species.RatKing, "a", false);
            Assert.That(gains.Count, Is.EqualTo(2));
            Assert.That(gains[0].Abs, Is.EqualTo(8f), "rat-king 8, full value to each");
            Assert.That(gains[1].Abs, Is.EqualTo(8f));
            Assert.That(players["a"].Weapon(WeaponCatalog.Axe).Abs, Is.EqualTo(8f));
            Assert.That(players["b"].Weapon(WeaponCatalog.DaggerShield).Abs, Is.EqualTo(8f));

            var fin = CombatRewards.OnKill(players, hits, 103.0, Species.RatKing, "a", true);
            Assert.That(fin[0].Abs, Is.EqualTo(8f * 1.25f * 1.5f).Within(1e-4f), "finisher x1.25 then the tech bonus x1.5 for the killer");
            Assert.That(fin[0].TechPop, Is.True);
            Assert.That(fin[1].Abs, Is.EqualTo(8f * 1.25f).Within(1e-4f), "the other attacker gets the finisher value without the tech");
            Assert.That(fin[1].TechPop, Is.False);
        }

        [Test]
        public void Kill_IgnoresHitsOlderThanTenSeconds_AndUnknownPlayers()
        {
            var players = new Dictionary<string, PlayerGrowth> { ["a"] = new PlayerGrowth("a") };
            var hits = new List<HitRecord> { new HitRecord("a", WeaponCatalog.Axe, 50.0), new HitRecord("ghost", WeaponCatalog.Axe, 99.0) };
            Assert.That(CombatRewards.OnKill(players, hits, 100.0, Species.Slime, "a", false), Is.Empty);
        }

        [Test]
        public void StyleAbs_AxeFinisherPays_AndTheCapStopsFarming()
        {
            var p = new PlayerGrowth("a");
            var none = new EnemyHitResult { PartBroken = -1 };
            AbsGain g = CombatRewards.OnHit(p, WeaponCatalog.Axe, "e1", Axe(AttackSet.Finisher), none, 1, false);
            Assert.That(g.Abs, Is.EqualTo(2f));
            Assert.That(CombatRewards.OnHit(p, WeaponCatalog.Axe, "e1", Axe(AttackSet.Hit1), none, 1, false).Abs, Is.EqualTo(0f), "a plain axe hit earns nothing");
            for (int i = 0; i < 10; i++) CombatRewards.OnHit(p, WeaponCatalog.Axe, "e1", Axe(AttackSet.Finisher), none, 1, false);
            Assert.That(CombatRewards.OnHit(p, WeaponCatalog.Axe, "e1", Axe(AttackSet.Finisher), none, 1, false).Abs, Is.EqualTo(0f), "6 ABS per enemy at most");
            Assert.That(p.Weapon(WeaponCatalog.Axe).Abs, Is.EqualTo(6f));
        }

        [Test]
        public void TechBonus_WeakPart_AddsFiftyPercent_WithAPop_AndTheTechBaseIsCapped()
        {
            var p = new PlayerGrowth("a");
            var weak = new EnemyHitResult { WeakPart = true, PartBroken = -1 };
            // The dagger pays 1 per hit; a weak-part hit makes it a tech: 1 x 1.5 with a pop.
            AbsGain g = CombatRewards.OnHit(p, WeaponCatalog.DaggerShield, "w", AttackCatalog.Dagger.Attacks[AttackSet.Hit1], weak, 1, false);
            Assert.That(g.Abs, Is.EqualTo(1.5f));
            Assert.That(g.TechPop, Is.True);
            // The axe pays nothing for a plain hit, so a weak-part hit is worth the capped tech base (1 x 1.5, three times per enemy).
            var axeP = new PlayerGrowth("b");
            float total = 0f;
            int pops = 0;
            for (int i = 0; i < 10; i++)
            {
                AbsGain t = CombatRewards.OnHit(axeP, WeaponCatalog.Axe, "w", Axe(AttackSet.Hit1), weak, 1, false);
                total += t.Abs;
                if (t.TechPop) pops++;
            }
            Assert.That(total, Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(pops, Is.EqualTo(3));
        }

        [Test]
        public void SpellPips_RefillByHits_AndTheSpellSpendsThem()
        {
            var p = new PlayerGrowth("a");
            var none = new EnemyHitResult { PartBroken = -1 };
            Assert.That(p.Pips.TrySpend(), Is.True);
            Assert.That(p.Pips.TrySpend(), Is.True);
            Assert.That(p.Pips.Current, Is.EqualTo(1));
            for (int i = 0; i < SpellPips.HitsPerPip; i++) CombatRewards.OnHit(p, WeaponCatalog.Staff, "e", AttackCatalog.Staff.Attacks[AttackSet.Hit1], none, 1, false);
            Assert.That(p.Pips.Current, Is.EqualTo(2), "four hits refilled a pip");
            CombatRewards.OnHit(p, WeaponCatalog.Staff, "e", AttackCatalog.Staff.Attacks[AttackSet.Special], none, 1, false);
            Assert.That(p.Pips.HitProgress, Is.EqualTo(0), "the spell's own hit does not refill");
        }

        [Test]
        public void Parry_PaysTheDaggerParryAbs_WithAPop()
        {
            var p = new PlayerGrowth("a");
            AbsGain g = CombatRewards.OnParry(p, WeaponCatalog.DaggerShield, "rat1");
            Assert.That(g.Abs, Is.EqualTo(3f * 1.5f));
            Assert.That(g.TechPop, Is.True);
            Assert.That(p.Weapon(WeaponCatalog.DaggerShield).Abs, Is.EqualTo(4.5f));
        }
    }
}
