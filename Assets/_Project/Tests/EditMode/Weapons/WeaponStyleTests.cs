// Tests/EditMode/Weapons/WeaponStyleTests.cs: style-event ABS, tech bonus, hero abilities and spell pips (Core/Weapons/WeaponStyle). Pure Core.
using NUnit.Framework;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode.Weapons
{
    public class WeaponStyleTests
    {
        [Test]
        public void StyleAbs_EachHeirloomOnlyEarnsFromItsOwnVerbs()
        {
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.Axe, StyleEvent.FinisherHit), Is.EqualTo(2f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.Axe, StyleEvent.GuardBreak), Is.EqualTo(3f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.DaggerShield, StyleEvent.Hit), Is.EqualTo(1f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.DaggerShield, StyleEvent.Parry), Is.EqualTo(3f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.Staff, StyleEvent.ExtraSwingHit), Is.EqualTo(1f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.BowSword, StyleEvent.WeakPointShot), Is.EqualTo(2f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.Axe, StyleEvent.Parry), Is.EqualTo(0f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.Staff, StyleEvent.Hit), Is.EqualTo(0f), "a plain hit is not a staff verb");
            Assert.That(WeaponStyle.StyleAbs("nope", StyleEvent.Hit), Is.EqualTo(0f));
            Assert.That(WeaponStyle.StyleAbs(WeaponCatalog.CinderAxe, StyleEvent.GuardBreak), Is.EqualTo(3f), "build-up targets keep the family verbs");
        }

        [Test]
        public void Ledger_CapsPerEnemy_ButNotAcrossEnemies_AndForgetResets()
        {
            var l = new StyleAbsLedger();
            float total = 0f;
            for (int i = 0; i < 10; i++) total += l.Award("e1", WeaponCatalog.DaggerShield, StyleEvent.Parry);   // 3 each
            Assert.That(total, Is.EqualTo(WeaponStyle.PerEnemyCap));
            Assert.That(l.Award("e1", WeaponCatalog.DaggerShield, StyleEvent.Hit), Is.EqualTo(0f));
            Assert.That(l.Award("e2", WeaponCatalog.DaggerShield, StyleEvent.Hit), Is.EqualTo(1f));
            l.Forget("e1");
            Assert.That(l.Award("e1", WeaponCatalog.DaggerShield, StyleEvent.Hit), Is.EqualTo(1f));
        }

        [Test]
        public void Ledger_PartialGrantAtTheCap()
        {
            var l = new StyleAbsLedger();
            l.Award("e", WeaponCatalog.Axe, StyleEvent.GuardBreak);   // 3
            l.Award("e", WeaponCatalog.Axe, StyleEvent.FinisherHit);  // 5
            Assert.That(l.Award("e", WeaponCatalog.Axe, StyleEvent.GuardBreak), Is.EqualTo(1f), "only 1 of the 6 left");
        }

        [Test]
        public void StyleAbs_IsSeparateFromKillCredit()
        {
            var hits = new[] { new HitRecord("a", "w", 1.0) };
            var credit = WeaponGrowth.Credit(hits, 2.0, WeaponGrowth.KillValue(Species.Slime, false));
            Assert.That(credit[0].Abs, Is.EqualTo(4f), "kill credit ignores style events");
        }

        [Test]
        public void TechBonus_AddsHalf_FlagsPop_AndDoesNotStack()
        {
            var r = TechBonus.Apply(4f, TechKind.Parry);
            Assert.That(r.Abs, Is.EqualTo(6f));
            Assert.That(r.Pop, Is.True);
            Assert.That(TechBonus.Apply(4f, TechKind.Parry, TechKind.WeakPart, TechKind.ElementMatch).Abs, Is.EqualTo(6f));
            var none = TechBonus.Apply(4f);
            Assert.That(none.Abs, Is.EqualTo(4f));
            Assert.That(none.Pop, Is.False);
            Assert.That(TechBonus.Apply(4f, null).Pop, Is.False);
        }

        [Test]
        public void TechBonus_OnAFinisherKill_StacksOnTheFinisherMultiplier()
        {
            var r = TechBonus.Apply(WeaponGrowth.KillValue(Species.Elite, true), TechKind.FinisherKill);
            Assert.That(r.Abs, Is.EqualTo(75f));   // 40 x 1.25 x 1.5
        }

        [Test]
        public void Abilities_UnlockAtLevel3_AndOnBuildUp_AndNeverLeave()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.Axe);
            var book = new HeroAbilityBook();
            Assert.That(book.Update(w), Is.Empty);
            w.AddAbs(40f + 60f);                                  // level 2
            Assert.That(book.Update(w), Is.Empty);
            w.AddAbs(80f);                                        // level 3
            Assert.That(book.Update(w), Is.EqualTo(new[] { "axe_quake" }));
            Assert.That(book.Update(w), Is.Empty, "reported once");

            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Ember);
            w.AddAbs(100f);                                       // level 4, 10 fire
            Assert.That(w.TryBuildUp(), Is.True);
            Assert.That(book.Update(w), Is.EqualTo(new[] { "axe_cinder_sweep" }));

            w.TryReroute();
            Assert.That(book.Has("axe_cinder_sweep"), Is.True, "the hero keeps it after a re-route");
            Assert.That(book.Unlocked.Count, Is.EqualTo(2));
        }

        [Test]
        public void Abilities_OnlyTheWeaponsOwnFamilyUnlocksThem()
        {
            var staff = WeaponState.NewHeirloom(WeaponCatalog.Staff);
            var book = new HeroAbilityBook();
            staff.AddAbs(180f);
            Assert.That(book.Update(staff), Is.EqualTo(new[] { "staff_spark_ward" }));
            Assert.That(book.Has("axe_quake"), Is.False);
        }

        [Test]
        public void Abilities_OneTierWeakerWithAnotherWeapon_AndLockedIsZero()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.Axe);
            var book = new HeroAbilityBook();
            Assert.That(book.TierWith("axe_quake", WeaponFamily.Axe), Is.EqualTo(0));
            w.AddAbs(180f);
            book.Update(w);
            Assert.That(book.TierWith("axe_quake", WeaponFamily.Axe), Is.EqualTo(2));
            Assert.That(book.TierWith("axe_quake", WeaponFamily.Bow), Is.EqualTo(1));
            Assert.That(book.TierWith("nope", WeaponFamily.Axe), Is.EqualTo(0));
        }

        [Test]
        public void Abilities_RestoreKeepsOnlyKnownIds()
        {
            var book = new HeroAbilityBook();
            book.Restore(new[] { "bow_eagle_eye", "junk" });
            Assert.That(book.Unlocked, Is.EquivalentTo(new[] { "bow_eagle_eye" }));
        }

        [Test]
        public void Pips_StartAtThree_RefillOnePerFourHits_StopAtCap()
        {
            var p = new SpellPips();
            Assert.That((p.Cap, p.Current), Is.EqualTo((3, 3)));
            Assert.That(p.OnHit(), Is.EqualTo(0), "full: nothing banks");
            Assert.That(p.TrySpend(2), Is.True);
            Assert.That(p.TrySpend(2), Is.False, "only 1 left");
            int gained = 0;
            for (int i = 0; i < 3; i++) gained += p.OnHit();
            Assert.That(gained, Is.EqualTo(0));
            Assert.That(p.OnHit(), Is.EqualTo(1));
            Assert.That(p.Current, Is.EqualTo(2));
            for (int i = 0; i < 40; i++) p.OnHit();
            Assert.That(p.Current, Is.EqualTo(3));
            Assert.That(p.HitProgress, Is.EqualTo(0));
        }

        [Test]
        public void Pips_CapGrowsWithTheWeapon_NewRoomStartsEmpty_AndLoweringClamps()
        {
            Assert.That(SpellPips.CapFor(0, 0), Is.EqualTo(3));
            Assert.That(SpellPips.CapFor(6, 0), Is.EqualTo(5));
            Assert.That(SpellPips.CapFor(0, 1), Is.EqualTo(4));
            var p = new SpellPips();
            p.SetCap(5);
            Assert.That((p.Cap, p.Current), Is.EqualTo((5, 3)));
            p.SetCap(2);
            Assert.That((p.Cap, p.Current), Is.EqualTo((2, 2)));
            Assert.That(p.TrySpend(-1), Is.False);
        }
    }
}
