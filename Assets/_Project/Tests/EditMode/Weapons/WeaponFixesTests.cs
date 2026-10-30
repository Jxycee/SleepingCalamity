// Tests/EditMode/Weapons/WeaponFixesTests.cs: review fixes for Core/Weapons (re-route and attachments, monotonic pip cap, save repair,
// input guards, ability and pip saves, closed-form AddAbs). Pure Core.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode.Weapons
{
    public class WeaponFixesTests
    {
        static void LevelTo(WeaponState w, int level)
        {
            while (w.Level < level) w.AddAbs(w.LevelCostNow - w.Abs);
        }

        // An axe at level 3 with 10 fire, ready to build up.
        static WeaponState ReadyAxe()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.Axe);
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Ember);
            LevelTo(w, 3);
            return w;
        }

        // A dagger (2 slots) ready to build up into the 3-slot Bane Dagger.
        static WeaponState ReadyDagger()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.DaggerShield);
            w.Fit(AttachmentKind.PestBane); w.Fit(AttachmentKind.PestBane);
            LevelTo(w, 2);
            Assert.That(w.CanBuildUp, Is.True);
            return w;
        }

        // ---- 1. Re-route and fitted attachments ---------------------------------------------------------------------------------------

        [Test]
        public void Reroute_AfterRemovingAnAttachment_DoesNotBringItBack()
        {
            var w = ReadyAxe();
            w.Fit(AttachmentKind.Heartwood);
            w.TryBuildUp();
            Assert.That(w.Remove(AttachmentKind.Heartwood), Is.True);   // the player bags it
            Assert.That(w.TryReroute(), Is.True);
            Assert.That(w.Fitted, Is.Empty, "no duplicate of the bagged attachment");
        }

        [Test]
        public void Reroute_AfterFittingAnAttachment_KeepsIt()
        {
            var w = ReadyAxe();
            w.TryBuildUp();
            w.Fit(AttachmentKind.PestBane);
            Assert.That(w.TryReroute(out var dropped), Is.True);
            Assert.That(dropped, Is.Empty);
            Assert.That(w.Fitted, Is.EqualTo(new[] { AttachmentKind.PestBane }));
        }

        [Test]
        public void Reroute_TrimsToTheRestoredSlots_AndReturnsTheDropped()
        {
            var w = ReadyDagger();                              // 2 slots
            Assert.That(w.TryBuildUp(out var d0), Is.True);
            Assert.That(d0, Is.Empty);
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Heartwood); w.Fit(AttachmentKind.PestBane);   // 3 slots on the target
            Assert.That(w.TryReroute(out var dropped), Is.True);
            Assert.That(w.Fitted, Is.EqualTo(new[] { AttachmentKind.Ember, AttachmentKind.Heartwood }));
            Assert.That(dropped, Is.EqualTo(new[] { AttachmentKind.PestBane }));
        }

        [Test]
        public void BuildUp_ReportsAttachmentsThatNoLongerFit()
        {
            var catalog = new WeaponCatalog(new[]
            {
                new WeaponDefinition("a", "A", new WeaponStats(10f), 3, "b"),
                new WeaponDefinition("b", "B", new WeaponStats(10f), 1, null),
            });
            var w = WeaponState.NewHeirloom("a", catalog);
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Heartwood); w.Fit(AttachmentKind.PestBane);
            Assert.That(w.TryBuildUp(out var dropped), Is.True);
            Assert.That(w.Fitted, Is.EqualTo(new[] { AttachmentKind.Ember }));
            Assert.That(dropped, Is.EqualTo(new[] { AttachmentKind.Heartwood, AttachmentKind.PestBane }));
        }

        [Test]
        public void ReroutePreview_ShowsTheAttachmentStatsThatWillBeLost()
        {
            var w = ReadyAxe();
            Assert.That(w.GetReroutePreview().Possible, Is.False);
            w.TryBuildUp();
            var restored = w.GetReroutePreview().RestoredStats;
            Assert.That(w.GetReroutePreview().StatsLost, Is.EqualTo(new WeaponStats(3f, 1f)), "only the build-up bonus so far");
            w.Fit(AttachmentKind.Heartwood);
            w.AddAbs(w.LevelCostNow);                           // absorbs Heartwood, +2 level ATK
            var p = w.GetReroutePreview();
            Assert.That(p.Possible, Is.True);
            Assert.That(p.RestoredDefinitionId, Is.EqualTo(WeaponCatalog.Axe));
            Assert.That(p.RestoredStats, Is.EqualTo(restored));
            Assert.That(p.AbsorbedSinceBuildUp, Is.EqualTo(new WeaponStats(2f)));
            Assert.That(p.StatsLost, Is.EqualTo(new WeaponStats(w.Stats.Atk - restored.Atk, 1f)));
        }

        [Test]
        public void ReroutePreview_AbsorbedTotalsSurviveSave()
        {
            var w = ReadyAxe();
            w.TryBuildUp();
            w.Fit(AttachmentKind.Heartwood);
            w.AddAbs(w.LevelCostNow);
            WeaponState.TryFromSave(WeaponsSaveRules.Clean(new WeaponsSave { Weapons = { w.ToSave("k") } }, null, null).Weapons[0], null, out var back);
            Assert.That(back.GetReroutePreview().AbsorbedSinceBuildUp, Is.EqualTo(new WeaponStats(2f)));
        }

        // ---- 2. Spell pip cap is monotonic --------------------------------------------------------------------------------------------

        [Test]
        public void PipCap_NeverDropsOnBuildUpOrReroute_LevelSixCase()
        {
            var w = ReadyAxe();                                 // level 3
            LevelTo(w, 6);
            int before = w.SpellPipCap;
            Assert.That(before, Is.EqualTo(SpellPips.CapFor(6, 0)));
            w.TryBuildUp();                                     // level -> 0
            Assert.That(w.Level, Is.EqualTo(0));
            Assert.That(w.SpellPipCap, Is.GreaterThanOrEqualTo(before));
            int built = w.SpellPipCap;
            w.TryReroute();
            Assert.That(w.SpellPipCap, Is.EqualTo(built), "re-route does not lower it");
            Assert.That(w.LifetimeLevels, Is.EqualTo(6));
            Assert.That(w.BuildUpsEver, Is.EqualTo(1));
        }

        [Test]
        public void PipCap_SurvivesSaveAndOnlyGrows()
        {
            var w = ReadyAxe();
            LevelTo(w, 6);
            w.TryBuildUp();
            WeaponState.TryFromSave(w.ToSave("k"), null, out var back);
            Assert.That(back.SpellPipCap, Is.EqualTo(w.SpellPipCap));
            back.AddAbs(500f);
            Assert.That(back.SpellPipCap, Is.GreaterThanOrEqualTo(w.SpellPipCap));
        }

        // ---- 3. History clean ----------------------------------------------------------------------------------------------------------

        static WeaponSave WithHistory(params string[] defs)
        {
            var s = WeaponState.NewHeirloom(WeaponCatalog.Axe).ToSave("k");
            foreach (var d in defs) s.History.Add(new BuildUpSnapshotSave { DefinitionId = d, Atk = s.History.Count + 1 });
            return s;
        }

        [Test]
        public void Clean_BadHistoryEntry_DropsItAndOlderOnly_AndReportsRepairedNotRejected()
        {
            var save = new WeaponsSave { Weapons = { WithHistory("axe", "nope", "axe") } };   // newest last
            var rejected = new List<string>(); var repaired = new List<string>();
            var clean = WeaponsSaveRules.Clean(save, null, rejected, repaired);
            Assert.That(rejected, Is.Empty);
            Assert.That(repaired, Is.EqualTo(new[] { "k" }));
            Assert.That(clean.Weapons.Count, Is.EqualTo(1));
            Assert.That(clean.Weapons[0].History.Count, Is.EqualTo(1));
            Assert.That(clean.Weapons[0].History[0].Atk, Is.EqualTo(3f), "the newer entry survives");
        }

        [Test]
        public void Clean_HistoryAbsAtOrPastCost_IsReset()
        {
            var s = WithHistory("axe");
            s.History[0].Level = 0; s.History[0].Abs = 40f;     // cost of level 0 is 40
            var repaired = new List<string>();
            var clean = WeaponsSaveRules.Clean(new WeaponsSave { Weapons = { s } }, null, null, repaired);
            Assert.That(clean.Weapons[0].History[0].Abs, Is.EqualTo(0f));
            Assert.That(repaired, Is.EqualTo(new[] { "k" }));
        }

        [Test]
        public void Clean_TrimmedAttachments_AreReportedAsRepaired()
        {
            var s = WeaponState.NewHeirloom(WeaponCatalog.Axe).ToSave("k");
            s.Fitted = new List<int> { 0, 1, 2, 0 };
            var repaired = new List<string>();
            WeaponsSaveRules.Clean(new WeaponsSave { Weapons = { s } }, null, null, repaired);
            Assert.That(repaired, Is.EqualTo(new[] { "k" }));
        }

        [Test]
        public void Clean_RejectsEmptyKeys_AndDedupesOnlyValidEntries()
        {
            var empty = WeaponState.NewHeirloom(WeaponCatalog.Axe).ToSave("");
            var badFirst = WeaponState.NewHeirloom(WeaponCatalog.Axe).ToSave("a"); badFirst.DefinitionId = "nope";
            var goodSecond = WeaponState.NewHeirloom(WeaponCatalog.Staff).ToSave("a");
            var rejected = new List<string>();
            var clean = WeaponsSaveRules.Clean(new WeaponsSave { Weapons = { empty, badFirst, goodSecond } }, null, rejected);
            Assert.That(clean.Weapons.Count, Is.EqualTo(1));
            Assert.That(clean.Weapons[0].DefinitionId, Is.EqualTo(WeaponCatalog.Staff), "the invalid 'a' did not shadow the valid one");
            Assert.That(rejected, Is.EqualTo(new[] { "", "a" }));
        }

        // ---- 4. Guards and shapes --------------------------------------------------------------------------------------------------------

        [Test]
        public void Credit_IgnoresNaNTimesAndNaNValues()
        {
            var hits = new[] { new HitRecord("a", "w", double.NaN), new HitRecord("b", "w", 5.0) };
            var c = WeaponGrowth.Credit(hits, 6.0, 4f);
            Assert.That(c.Count, Is.EqualTo(1));
            Assert.That(c[0].PlayerId, Is.EqualTo("b"));
            Assert.That(WeaponGrowth.Credit(hits, double.NaN, 4f), Is.Empty);
            Assert.That(WeaponGrowth.Credit(hits, 6.0, float.NaN), Is.Empty);
            Assert.That(WeaponGrowth.Credit(hits, 6.0, -1f), Is.Empty);
        }

        [Test]
        public void TechBonus_RejectsNaNInfinityAndNegative()
        {
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -3f })
            {
                var r = TechBonus.Apply(bad, TechKind.Parry);
                Assert.That(r.Abs, Is.EqualTo(0f));
                Assert.That(r.Pop, Is.False);
            }
        }

        [Test]
        public void AddAbs_HugeAmounts_FinishAtOnceAndStopAtMaxLevel()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.Axe);
            Assert.That(w.AddAbs(float.MaxValue), Is.EqualTo(WeaponState.MaxLevel));
            Assert.That(w.Level, Is.EqualTo(WeaponState.MaxLevel));
            Assert.That(w.Abs, Is.EqualTo(0f));
            Assert.That(w.Stats.Atk, Is.EqualTo(22f + 10f + (WeaponState.MaxLevel - 5)));
            Assert.That(w.AddAbs(1000f), Is.EqualTo(0));
        }

        [Test]
        public void AddAbs_ClosedForm_MatchesLevellingOneAtATime()
        {
            foreach (float amount in new[] { 39.9f, 40f, 100f, 700f, 5000f, 123456f })
            {
                var a = WeaponState.NewHeirloom(WeaponCatalog.Axe);
                a.Fit(AttachmentKind.Heartwood);
                int ga = a.AddAbs(amount);

                var b = WeaponState.NewHeirloom(WeaponCatalog.Axe);
                b.Fit(AttachmentKind.Heartwood);
                float left = amount; int gb = 0;
                while (left >= b.LevelCostNow - b.Abs) { left -= b.LevelCostNow - b.Abs; b.AddAbs(b.LevelCostNow - b.Abs); gb++; }
                b.AddAbs(left);
                Assert.That(ga, Is.EqualTo(gb), "levels for " + amount);
                Assert.That(a.Stats, Is.EqualTo(b.Stats), "stats for " + amount);
                Assert.That(a.Abs, Is.EqualTo(b.Abs).Within(0.5f), "bar for " + amount);
            }
        }

        [Test]
        public void HeroAbilities_UnknownIdsAreKeptAndWrittenBack_AndSaveRoundTrips()
        {
            var book = new HeroAbilityBook();
            book.Restore(new HeroAbilitiesSave { Unlocked = { "axe_quake", "from_the_future", "", "axe_quake" } });
            Assert.That(book.Has("axe_quake"), Is.True);
            Assert.That(book.Has("from_the_future"), Is.False, "unknown ids are not usable");
            string json = JsonConvert.SerializeObject(book.ToSave());
            var back = new HeroAbilityBook();
            back.Restore(JsonConvert.DeserializeObject<HeroAbilitiesSave>(json));
            Assert.That(back.ToSave().Unlocked, Is.EqualTo(new[] { "axe_quake", "from_the_future" }));
        }

        [Test]
        public void HeroAbilitiesSave_KeepsUnknownMembers_AndCleanDropsBlanks()
        {
            var loaded = JsonConvert.DeserializeObject<HeroAbilitiesSave>("{\"Unlocked\":[\"a\",null,\"a\",\"b\"],\"newField\":7}");
            var clean = WeaponsSaveRules.CleanAbilities(loaded);
            Assert.That(clean.Unlocked, Is.EqualTo(new[] { "a", "b" }));
            StringAssert.Contains("newField", JsonConvert.SerializeObject(clean));
            Assert.That(WeaponsSaveRules.CleanAbilities(null).Unlocked, Is.Empty);
        }

        [Test]
        public void TierWith_MaxTierZero_IsZero()
        {
            var defs = new[] { new AbilityDefinition("dud", WeaponFamily.Axe, 0, false, 0) };
            var book = new HeroAbilityBook(defs);
            book.Update(WeaponState.NewHeirloom(WeaponCatalog.Axe));
            Assert.That(book.Has("dud"), Is.True);
            Assert.That(book.TierWith("dud", WeaponFamily.Axe), Is.EqualTo(0));
            Assert.That(book.TierWith("dud", WeaponFamily.Bow), Is.EqualTo(0));
        }

        [Test]
        public void SpellPips_SaveRoundTripsThroughJson_AndClampsDamage()
        {
            var p = new SpellPips();
            p.SetCap(5);
            p.TrySpend(1);
            p.OnHit(); p.OnHit();
            var back = SpellPips.FromSave(JsonConvert.DeserializeObject<SpellPipsSave>(JsonConvert.SerializeObject(p.ToSave())));
            Assert.That((back.Cap, back.Current, back.HitProgress), Is.EqualTo((p.Cap, p.Current, p.HitProgress)));

            var bad = SpellPips.FromSave(new SpellPipsSave { Cap = 2, Current = 99, HitProgress = 50 });
            Assert.That((bad.Cap, bad.Current, bad.HitProgress), Is.EqualTo((2, 2, SpellPips.HitsPerPip - 1)));
            Assert.That(SpellPips.FromSave(null).Cap, Is.EqualTo(SpellPips.StartCap));
        }
    }
}
