// Tests/EditMode/Weapons/WeaponTreeViewTests.cs: the weapon-tree view model (Core/Weapons/WeaponTreeView, M4 UI). Pure Core.
using NUnit.Framework;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode.Weapons
{
    public class WeaponTreeViewTests
    {
        static WeaponState Axe() => WeaponState.NewHeirloom(WeaponCatalog.Axe);
        static WeaponTreeView View(WeaponState w, bool bench = true) => WeaponTreeView.Build(WeaponTreeSnapshot.From(w, bench));
        static void LevelTo(WeaponState w, int level) { while (w.Level < level) w.AddAbs(w.LevelCostNow - w.Abs); }

        // Catches: the host pushing the tree on every hit. SyncKey must ignore a small ABS gain and change on level, fitting and weapon.
        [Test]
        public void SyncKey_IgnoresAbsTicks_ButTracksLevelFittedAndWeapon()
        {
            var w = Axe();
            string k0 = WeaponTreeSnapshot.From(w, true).SyncKey("axe");
            w.AddAbs(1f);
            Assert.That(WeaponTreeSnapshot.From(w, true).SyncKey("axe"), Is.EqualTo(k0), "ABS alone is carried by the growth mirror");
            Assert.That(WeaponTreeSnapshot.From(w, true).SyncKey("sword"), Is.Not.EqualTo(k0), "a weapon swap");
            Assert.That(WeaponTreeSnapshot.From(w, false).SyncKey("axe"), Is.Not.EqualTo(k0), "the workbench flag");
            w.Fit(AttachmentKind.Ember);
            string kFit = WeaponTreeSnapshot.From(w, true).SyncKey("axe");
            Assert.That(kFit, Is.Not.EqualTo(k0), "a fitted attachment");
            LevelTo(w, 1);
            Assert.That(WeaponTreeSnapshot.From(w, true).SyncKey("axe"), Is.Not.EqualTo(kFit), "a level");
        }

        [Test]
        public void FreshAxe_ShowsLevelAbsStatsAndEmptySlots_NothingEnabled()
        {
            var v = View(Axe());
            Assert.That(v.Title, Is.EqualTo("Heirloom Axe"));
            Assert.That(v.LevelLine, Is.EqualTo("Level 0   ABS 0 / 40"));
            Assert.That(v.StatsLine, Is.EqualTo("ATK 22  Fire 0  Pest 0"));
            Assert.That(v.Slots.Count, Is.EqualTo(3));
            Assert.That(v.Slots.TrueForAll(s => !s.Filled), Is.True);
            Assert.That(v.CanFit, Is.True);
            Assert.That(v.CanBuildUp, Is.False);
            Assert.That(v.CanReroute, Is.False);
            Assert.That(v.RerouteLine, Does.Contain("nothing to undo"));
        }

        [Test]
        public void AbsFraction_FollowsTheFillRatio()
        {
            var w = Axe();
            w.AddAbs(10f);
            Assert.That(View(w).AbsFraction, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void Thresholds_OnlyForStatsTheTargetAsks_WithTheNinetyPercentMarker()
        {
            var v = View(Axe());   // Cinder Axe asks ATK 30 and Fire 10, not Pest
            Assert.That(v.Thresholds.Count, Is.EqualTo(2));
            Assert.That(v.Thresholds[0].Label, Does.StartWith("ATK 22 / 30"));
            Assert.That(v.Thresholds[0].Label, Does.Contain("need 27"));
            Assert.That(v.Thresholds[0].Fraction, Is.EqualTo(22f / 30f).Within(1e-5f));
            Assert.That(v.Thresholds[0].Met, Is.False);
            Assert.That(v.Thresholds[1].Fraction, Is.EqualTo(0f));
            Assert.That(v.Thresholds[0].Marker, Is.EqualTo(0.9f));
        }

        [Test]
        public void BuildUpButton_EnablesAtNinetyPercentOfBoth_AndMatchesCore()
        {
            var w = Axe();
            LevelTo(w, 3);                       // ATK 28 of 27 needed, fire still 0
            Assert.That(View(w).CanBuildUp, Is.False, "attack alone is not enough");
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Ember);
            LevelTo(w, 4);                       // absorbs both embers: fire 10 of 9
            var v = View(w);
            Assert.That(w.CanBuildUp, Is.True);
            Assert.That(v.CanBuildUp, Is.EqualTo(w.CanBuildUp));
            Assert.That(v.Thresholds.TrueForAll(t => t.Met), Is.True);
        }

        [Test]
        public void AbsorbPreview_ListsTheFittedBonusesAndTheAtkGain()
        {
            var w = Axe();
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Heartwood);
            var v = View(w);
            var p = w.PreviewLevelUp();
            Assert.That(v.AbsorbLine, Does.Contain("absorbs 2"));
            Assert.That(v.AbsorbLine, Does.Contain("ATK " + p.WithAbsorb.Atk + "  Fire " + p.WithAbsorb.Fire));   // ATK 26, Fire 5
            Assert.That(v.AbsorbLine, Does.Contain("without: ATK " + p.WithoutAbsorb.Atk));
            Assert.That(v.Slots[0].Filled && v.Slots[1].Filled && !v.Slots[2].Filled, Is.True);
            Assert.That(v.Slots[0].Kind, Is.EqualTo(AttachmentKind.Ember));
        }

        [Test]
        public void FullSlots_DisableFit()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.DaggerShield);   // 2 slots
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Ember);
            Assert.That(View(w).CanFit, Is.False);
        }

        [Test]
        public void Reroute_NeedsHistoryAndTheWorkbench()
        {
            var w = Axe();
            LevelTo(w, 3); w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Ember); LevelTo(w, 4);
            Assert.That(w.TryBuildUp(), Is.True);
            var atBench = View(w, true);
            var away = View(w, false);
            Assert.That(atBench.CanReroute, Is.True);
            Assert.That(away.CanReroute, Is.False);
            Assert.That(away.RerouteLine, Does.Contain("home workbench"));
            Assert.That(atBench.Title, Is.EqualTo("Cinder Axe"));
            Assert.That(atBench.RerouteLine, Does.Contain("Heirloom Axe"));
        }

        [Test]
        public void AWeaponWithNoTarget_HasNoBuildUp()
        {
            var v = View(WeaponState.NewHeirloom(WeaponCatalog.CinderAxe));
            Assert.That(v.CanBuildUp, Is.False);
            Assert.That(v.Thresholds, Is.Empty);
            Assert.That(v.BuildUpLine, Does.Contain("No build-up"));
        }
    }
}
