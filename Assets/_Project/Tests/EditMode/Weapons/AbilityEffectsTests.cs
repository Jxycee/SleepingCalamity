// Tests/EditMode/Weapons/AbilityEffectsTests.cs: the placeholder ability effects (Core/Weapons/AbilityEffects) and the tier rule, plus the enemy model
// honouring the modifiers. Pure Core. Catches: an effect with the wrong number, an effect that works without its unlock, a tier that ignores the held
// weapon, a modifier the enemy model drops.
using NUnit.Framework;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode.Weapons
{
    public class AbilityEffectsTests
    {
        const float Far = -1f;   // no parry

        static HeroAbilityBook Book(params string[] ids) { var b = new HeroAbilityBook(); b.Restore(ids); return b; }

        [Test]
        public void TierRule_FullWithOwnFamily_OneWeakerWithAnother_ZeroWhenLocked()
        {
            var b = Book(AbilityEffects.Quake);
            Assert.That(b.TierWith(AbilityEffects.Quake, WeaponFamily.Axe), Is.EqualTo(2));
            Assert.That(b.TierWith(AbilityEffects.Quake, WeaponFamily.Bow), Is.EqualTo(1));
            Assert.That(b.TierWith(AbilityEffects.SparkWard, WeaponFamily.Staff), Is.EqualTo(0), "locked");
        }

        [TestCase(WeaponFamily.Axe, 1.30f)]
        [TestCase(WeaponFamily.Staff, 1.15f)]
        public void Quake_FinisherOnly(WeaponFamily held, float mult)
        {
            var b = Book(AbilityEffects.Quake);
            Assert.That(AbilityEffects.OutgoingHit(b, held, AttackKind.Finisher, false, Far).DamageMult, Is.EqualTo(mult).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, held, AttackKind.Normal, false, Far).DamageMult, Is.EqualTo(1f));
        }

        [Test]
        public void CinderSweep_FinishersAddFire_PerTier()
        {
            var b = Book(AbilityEffects.CinderSweep);
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Axe, AttackKind.Finisher, false, Far).Fire, Is.EqualTo(10f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Finisher, false, Far).Fire, Is.EqualTo(5f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Axe, AttackKind.Normal, false, Far).Fire, Is.EqualTo(0f));
        }

        [Test]
        public void SparkWard_ReducesIncomingDamage()
        {
            var b = Book(AbilityEffects.SparkWard);
            Assert.That(AbilityEffects.IncomingMult(b, WeaponFamily.Staff), Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(AbilityEffects.IncomingMult(b, WeaponFamily.Axe), Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(AbilityEffects.IncomingMult(Book(), WeaponFamily.Staff), Is.EqualTo(1f));
            Assert.That(AbilityEffects.IncomingMult(null, WeaponFamily.Staff), Is.EqualTo(1f));
        }

        [Test]
        public void WardenPulse_BoostsSpellsOnly()
        {
            var b = Book(AbilityEffects.WardenPulse);
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Staff, AttackKind.Spell, false, Far).DamageMult, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Bow, AttackKind.Spell, false, Far).DamageMult, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Staff, AttackKind.Normal, false, Far).DamageMult, Is.EqualTo(1f));
        }

        [Test]
        public void Riposte_OnlyInsideTheWindowAfterAParry()
        {
            var b = Book(AbilityEffects.Riposte);
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Normal, false, 0.5f).DamageMult, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Normal, false, AbilityTuning.RiposteWindowSeconds).DamageMult, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Normal, false, 2.01f).DamageMult, Is.EqualTo(1f), "window over");
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Normal, false, Far).DamageMult, Is.EqualTo(1f), "never parried");
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Axe, AttackKind.Normal, false, 0.5f).DamageMult, Is.EqualTo(1.25f).Within(1e-4f));
        }

        [Test]
        public void BaneFlurry_OnlyAgainstPests()
        {
            var b = Book(AbilityEffects.BaneFlurry);
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Normal, true, Far).DamageMult, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Dagger, AttackKind.Normal, false, Far).DamageMult, Is.EqualTo(1f));
        }

        [Test]
        public void EagleEye_AddsCritChance()
        {
            var b = Book(AbilityEffects.EagleEye);
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Bow, AttackKind.Shot, false, Far).CritChance, Is.EqualTo(0.10f).Within(1e-4f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Axe, AttackKind.Normal, false, Far).CritChance, Is.EqualTo(0.05f).Within(1e-4f));
        }

        [Test]
        public void EmberVolley_ChargedAttacksAddFire()
        {
            var b = Book(AbilityEffects.EmberVolley);
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Bow, AttackKind.Charge, false, Far).Fire, Is.EqualTo(10f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Axe, AttackKind.Charge, false, Far).Fire, Is.EqualTo(5f));
            Assert.That(AbilityEffects.OutgoingHit(b, WeaponFamily.Bow, AttackKind.Shot, false, Far).Fire, Is.EqualTo(0f));
        }

        [Test]
        public void NoBook_NoEffect()
        {
            AbilityHitMods m = AbilityEffects.OutgoingHit(null, WeaponFamily.Axe, AttackKind.Finisher, true, 0.1f);
            Assert.That(m.DamageMult, Is.EqualTo(1f));
            Assert.That(m.Fire, Is.EqualTo(0f));
            Assert.That(m.CritChance, Is.EqualTo(0f));
        }

        [Test]
        public void EnemyModel_AppliesTheDamageBonus()
        {
            AttackData atk = AttackCatalog.Axe.Attacks[AttackSet.Hit1];
            int Hit(float bonus)
            {
                var e = new EnemyModel(EnemyCatalog.Slime, 1, 1);
                return e.ApplyHit(new EnemyHitInput { Attack = atk, Atk = 40f, PartIndex = -1, CritRoll = 0.9f, CritChance = 0f, AbilityDamageBonus = bonus }).Damage;
            }
            Assert.That(Hit(0.5f), Is.GreaterThan(Hit(0f)));
        }
    }
}
