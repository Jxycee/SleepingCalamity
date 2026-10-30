// Tests/EditMode/Weapons/WeaponGrowthTests.cs: weapon growth rules (Core/Weapons, PHASE1 6.3). Pure Core.
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode.Weapons
{
    public class WeaponGrowthTests
    {
        static WeaponState Axe() => WeaponState.NewHeirloom(WeaponCatalog.Axe);

        // Levels the axe up n times exactly, with nothing fitted.
        static void LevelTo(WeaponState w, int level)
        {
            while (w.Level < level) w.AddAbs(w.LevelCostNow - w.Abs);
        }

        [Test]
        public void Heirlooms_StartAtThe65AtkNumbers_AndHaveTwoToThreeSlotsAndATarget()
        {
            var expect = new Dictionary<string, float>
            {
                { WeaponCatalog.Axe, 22f }, { WeaponCatalog.Staff, 14f }, { WeaponCatalog.DaggerShield, 10f }, { WeaponCatalog.BowSword, 12f },
            };
            foreach (var kv in expect)
            {
                Assert.That(WeaponCatalog.Default.TryGet(kv.Key, out var d), Is.True);
                Assert.That(d.Base.Atk, Is.EqualTo(kv.Value), kv.Key);
                Assert.That(d.SlotCount, Is.InRange(2, 3));
                Assert.That(WeaponCatalog.Default.TryGet(d.BuildUpTargetId, out var t), Is.True, "target exists");
                Assert.That(t.SlotCount, Is.GreaterThanOrEqualTo(d.SlotCount), "build-up never loses slots");
            }
        }

        [Test]
        public void KillValue_SpeciesTable_AndFinisherIsTimes1Point25()
        {
            Assert.That(WeaponGrowth.KillValue(Species.Slime, false), Is.EqualTo(4f));
            Assert.That(WeaponGrowth.KillValue(Species.RatKing, false), Is.EqualTo(8f));
            Assert.That(WeaponGrowth.KillValue(Species.Broom, false), Is.EqualTo(6f));
            Assert.That(WeaponGrowth.KillValue(Species.Elite, false), Is.EqualTo(40f));
            Assert.That(WeaponGrowth.KillValue(Species.Slime, true), Is.EqualTo(5f));
            Assert.That(WeaponGrowth.KillValue(Species.Elite, true), Is.EqualTo(50f));
        }

        [Test]
        public void LevelCost_Is40Times1PlusHalfL_AsFloat()
        {
            Assert.That(WeaponGrowth.LevelCost(0), Is.EqualTo(40f));
            Assert.That(WeaponGrowth.LevelCost(1), Is.EqualTo(60f));
            Assert.That(WeaponGrowth.LevelCost(2), Is.EqualTo(80f));
            Assert.That(WeaponGrowth.LevelCost(5), Is.EqualTo(140f));
            Assert.That(WeaponGrowth.LevelCost(7), Is.EqualTo(180f));
        }

        [Test]
        public void AddAbs_BelowCost_FillsTheBar_NoLevel()
        {
            var w = Axe();
            Assert.That(w.AddAbs(10f), Is.EqualTo(0));
            Assert.That(w.Level, Is.EqualTo(0));
            Assert.That(w.AbsFill, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(w.Stats.Atk, Is.EqualTo(22f));
        }

        [Test]
        public void AddAbs_ExactCost_LevelsUpAndEmptiesTheBar()
        {
            var w = Axe();
            Assert.That(w.AddAbs(40f), Is.EqualTo(1));
            Assert.That(w.Level, Is.EqualTo(1));
            Assert.That(w.Abs, Is.EqualTo(0f));
            Assert.That(w.Stats.Atk, Is.EqualTo(24f));
        }

        [Test]
        public void AddAbs_OneBigGain_LevelsSeveralTimes_AndKeepsTheRemainder()
        {
            var w = Axe();
            // 40 + 60 + 80 = 180 pays levels 1-3; 25 is left toward level 4 (cost 100).
            Assert.That(w.AddAbs(205f), Is.EqualTo(3));
            Assert.That(w.Level, Is.EqualTo(3));
            Assert.That(w.Abs, Is.EqualTo(25f).Within(1e-4f));
            Assert.That(w.Stats.Atk, Is.EqualTo(22f + 6f));
        }

        [Test]
        public void AddAbs_IgnoresZeroNegativeAndNonFinite()
        {
            var w = Axe();
            Assert.That(w.AddAbs(0f), Is.EqualTo(0));
            Assert.That(w.AddAbs(-50f), Is.EqualTo(0));
            Assert.That(w.AddAbs(float.NaN), Is.EqualTo(0));
            Assert.That(w.AddAbs(float.PositiveInfinity), Is.EqualTo(0));
            Assert.That(w.Level, Is.EqualTo(0));
            Assert.That(w.Abs, Is.EqualTo(0f));
        }

        [Test]
        public void AtkGain_IsTwoUpToLevel5_ThenOne()
        {
            var w = Axe();
            var atk = new List<float>();
            for (int i = 0; i < 7; i++) { LevelTo(w, w.Level + 1); atk.Add(w.Stats.Atk); }
            Assert.That(atk, Is.EqualTo(new[] { 24f, 26f, 28f, 30f, 32f, 33f, 34f }));
        }

        [Test]
        public void Attachments_FitUpToSlots_RemoveFreely_AndDoNothingUntilLevelUp()
        {
            var w = WeaponState.NewHeirloom(WeaponCatalog.DaggerShield);   // 2 slots
            Assert.That(w.Fit(AttachmentKind.Ember), Is.True);
            Assert.That(w.Fit(AttachmentKind.Heartwood), Is.True);
            Assert.That(w.Fit(AttachmentKind.PestBane), Is.False, "slots full");
            Assert.That(w.Stats, Is.EqualTo(new WeaponStats(10f)), "fitted but not absorbed");
            Assert.That(w.Remove(AttachmentKind.Ember), Is.True);
            Assert.That(w.Remove(AttachmentKind.Ember), Is.False);
            Assert.That(w.Fit(AttachmentKind.PestBane), Is.True);
            Assert.That(w.Stats, Is.EqualTo(new WeaponStats(10f)));
        }

        [Test]
        public void Attachments_AreAbsorbedPermanentlyAtLevelUp_AndSlotsFreeUp()
        {
            var w = Axe();
            w.Fit(AttachmentKind.Ember);
            w.Fit(AttachmentKind.Heartwood);
            w.AddAbs(39f);
            Assert.That(w.Fitted.Count, Is.EqualTo(2), "not absorbed before the level-up");
            w.AddAbs(1f);
            Assert.That(w.Stats, Is.EqualTo(new WeaponStats(22f + 2f + 2f, fire: 5f)));
            Assert.That(w.Fitted.Count, Is.EqualTo(0));
            Assert.That(w.Remove(AttachmentKind.Ember), Is.False, "gone for good");
        }

        [Test]
        public void MultiLevelGain_AbsorbsAttachmentsOnlyOnce()
        {
            var w = Axe();
            w.Fit(AttachmentKind.Heartwood);
            w.AddAbs(100f);   // levels 1 and 2
            Assert.That(w.Level, Is.EqualTo(2));
            Assert.That(w.Stats.Atk, Is.EqualTo(22f + 2f + 4f));
        }

        [Test]
        public void Preview_MatchesTheRealLevelUp_WithAndWithoutAbsorbing_AndChangesNothing()
        {
            var w = Axe();
            w.Fit(AttachmentKind.Ember);
            w.Fit(AttachmentKind.PestBane);
            w.Fit(AttachmentKind.Heartwood);
            var before = w.Stats;
            var p = w.PreviewLevelUp();
            Assert.That(w.Stats, Is.EqualTo(before), "preview is pure");
            Assert.That(w.Fitted.Count, Is.EqualTo(3));
            Assert.That(p.NextLevel, Is.EqualTo(1));
            Assert.That(p.WithoutAbsorb, Is.EqualTo(new WeaponStats(24f)));
            Assert.That(p.WithAbsorb, Is.EqualTo(new WeaponStats(26f, fire: 5f, pest: 5f)));
            Assert.That(p.AbsToNext, Is.EqualTo(40f));

            w.AddAbs(40f);
            Assert.That(w.Stats, Is.EqualTo(p.WithAbsorb));

            // Without absorbing: remove first, then the real result equals the "without" preview.
            var w2 = Axe();
            w2.Fit(AttachmentKind.Ember);
            var p2 = w2.PreviewLevelUp();
            w2.Remove(AttachmentKind.Ember);
            w2.AddAbs(40f);
            Assert.That(w2.Stats, Is.EqualTo(p2.WithoutAbsorb));
        }

        [Test]
        public void Preview_AtLevel5_UsesTheOnePointGain()
        {
            var w = Axe();
            LevelTo(w, 5);
            var p = w.PreviewLevelUp();
            Assert.That(p.NextLevel, Is.EqualTo(6));
            Assert.That(p.WithoutAbsorb.Atk, Is.EqualTo(w.Stats.Atk + 1f));
        }

        [Test]
        public void BuildUp_ThresholdIsExactly90Percent()
        {
            // Staff 14 -> Warden Staff base ATK 20, pest 10: needs ATK 18 and pest 9.
            var w = WeaponState.NewHeirloom(WeaponCatalog.Staff);
            LevelTo(w, 1);                                  // ATK 16
            w.Fit(AttachmentKind.PestBane);
            w.Fit(AttachmentKind.PestBane);
            w.AddAbs(w.LevelCostNow - w.Abs);               // level 2: ATK 18, pest 10
            Assert.That(w.Stats.Atk, Is.EqualTo(18f));
            Assert.That(w.GetBuildUpProgress().Available, Is.True, "18 of 20 is exactly 90%");

            var below = WeaponState.NewHeirloom(WeaponCatalog.Staff);
            LevelTo(below, 1);                              // ATK 16: below
            below.Fit(AttachmentKind.PestBane);
            below.Fit(AttachmentKind.PestBane);
            LevelTo(below, 2);
            // Same weapon, but one ATK short: rebuild by state from save with ATK 17.99.
            var s = below.ToSave();
            s.Atk = 17.99f;
            WeaponState.TryFromSave(s, null, out var shy);
            Assert.That(shy.GetBuildUpProgress().Available, Is.False);
        }

        [Test]
        public void BuildUp_NeedsBothAtkAndTheElement()
        {
            var w = Axe();
            LevelTo(w, 3);                                   // ATK 28 >= 27 but no fire
            var p = w.GetBuildUpProgress();
            Assert.That(p.HasTarget, Is.True);
            Assert.That(p.Available, Is.False);
            Assert.That(p.Atk, Is.EqualTo(28f / 30f).Within(1e-6f));
            Assert.That(p.Fire, Is.EqualTo(0f));
            Assert.That(p.Pest, Is.EqualTo(1f), "not asked for reads full");
            Assert.That(w.TryBuildUp(), Is.False);

            var q = Axe();
            q.Fit(AttachmentKind.Ember); q.Fit(AttachmentKind.Ember);
            q.AddAbs(40f);                                   // fire 10 but ATK 24 (of 30)
            var pq = q.GetBuildUpProgress();
            Assert.That(pq.Fire, Is.EqualTo(1f));
            Assert.That(pq.Atk, Is.EqualTo(24f / 30f).Within(1e-6f));
            Assert.That(pq.Available, Is.False);
        }

        [Test]
        public void BuildUp_ResetsLevel_KeepsFillRatio_AndAdds10PercentOfTargetBase()
        {
            var w = BuiltUpReady();
            w.AddAbs(w.LevelCostNow * 0.5f);                 // half way up the level-3 bar
            float fill = w.AbsFill;
            var before = w.Stats;
            Assert.That(w.TryBuildUp(), Is.True);
            Assert.That(w.DefinitionId, Is.EqualTo(WeaponCatalog.CinderAxe));
            Assert.That(w.Level, Is.EqualTo(0));
            Assert.That(w.AbsFill, Is.EqualTo(fill).Within(1e-5f));
            Assert.That(w.Stats, Is.EqualTo(before + new WeaponStats(3f, fire: 1f)), "10% of 30 ATK and 10 fire");
            Assert.That(w.TryBuildUp(), Is.False, "the target has no further build-up");
        }

        // An axe at level 3 (ATK 28) with 10 fire (two Embers absorbed at level 1).
        static WeaponState BuiltUpReady()
        {
            var w = Axe();
            w.Fit(AttachmentKind.Ember); w.Fit(AttachmentKind.Ember);
            LevelTo(w, 3);
            Assert.That(w.CanBuildUp, Is.True);
            return w;
        }

        [Test]
        public void BuildUp_ThenReroute_ReturnsIdenticalStats_NoIrreversibleChoice()
        {
            var w = BuiltUpReady();
            w.Fit(AttachmentKind.Heartwood);
            w.AddAbs(33f);
            var snapshot = (w.DefinitionId, w.Level, w.Abs, w.Stats);

            Assert.That(w.CanReroute, Is.False);
            Assert.That(w.TryBuildUp(), Is.True);
            Assert.That(w.CanReroute, Is.True);
            w.AddAbs(500f);                                  // growth after the build-up is lost by the re-route
            Assert.That(w.TryReroute(), Is.True);

            Assert.That((w.DefinitionId, w.Level, w.Abs, w.Stats), Is.EqualTo(snapshot));
            Assert.That(w.Fitted, Is.Empty, "the Heartwood was absorbed after the build-up; re-route restores stats, not attachments");
            Assert.That(w.CanReroute, Is.False);
            Assert.That(w.TryReroute(), Is.False);
            Assert.That(w.TryBuildUp(), Is.True, "and it can be built up again");
        }

        [Test]
        public void Credit_EveryPlayerInTheLast10s_GetsTheFullValue_OnTheirOwnWeapon()
        {
            var hits = new[]
            {
                new HitRecord("a", "axeA", 95.0), new HitRecord("b", "staffB", 99.0), new HitRecord("a", "axeA", 100.0),
                new HitRecord("c", "bowC", 80.0),   // too old
            };
            var c = WeaponGrowth.Credit(hits, 100.0, 8f);
            Assert.That(c.Count, Is.EqualTo(2));
            Assert.That(c[0].PlayerId, Is.EqualTo("a"));
            Assert.That(c[0].WeaponKey, Is.EqualTo("axeA"));
            Assert.That(c[0].Abs, Is.EqualTo(8f));
            Assert.That(c[1].PlayerId, Is.EqualTo("b"));
            Assert.That(c[1].Abs, Is.EqualTo(8f), "no last-hit competition: b is not halved");
        }

        [Test]
        public void Credit_WindowEdge_Exactly10sCounts_JustOverDoesNot()
        {
            var hits = new[] { new HitRecord("in", "w1", 90.0), new HitRecord("out", "w2", 89.999) };
            var c = WeaponGrowth.Credit(hits, 100.0, 4f);
            Assert.That(c.Count, Is.EqualTo(1));
            Assert.That(c[0].PlayerId, Is.EqualTo("in"));
        }

        [Test]
        public void Credit_UsesTheLatestWeaponPerPlayer_IgnoresHitsAfterTheKill_AndNullInput()
        {
            var hits = new[] { new HitRecord("a", "axe", 92.0), new HitRecord("a", "bow", 97.0), new HitRecord("a", "late", 101.0) };
            var c = WeaponGrowth.Credit(hits, 100.0, 6f);
            Assert.That(c.Count, Is.EqualTo(1));
            Assert.That(c[0].WeaponKey, Is.EqualTo("bow"));
            Assert.That(WeaponGrowth.Credit(null, 1.0, 4f), Is.Empty);
        }

        [Test]
        public void FinisherKill_PaysTimes1Point25_ToEveryCreditedPlayer()
        {
            var hits = new[] { new HitRecord("a", "w", 5.0), new HitRecord("b", "x", 6.0) };
            var c = WeaponGrowth.Credit(hits, 7.0, WeaponGrowth.KillValue(Species.RatKing, true));
            Assert.That(c.Count, Is.EqualTo(2));
            Assert.That(c[0].Abs, Is.EqualTo(10f));
            Assert.That(c[1].Abs, Is.EqualTo(10f));
        }

        // ---- Save ---------------------------------------------------------------------------------------------------------------------

        [Test]
        public void Save_RoundTripsThroughJson_IncludingHistory_AndStillReroutes()
        {
            var w = BuiltUpReady();
            w.Fit(AttachmentKind.PestBane);
            w.AddAbs(20f);
            w.TryBuildUp();
            w.AddAbs(15f);
            var section = new WeaponsSave { Weapons = { w.ToSave("k1", "p1") } };

            string json = JsonConvert.SerializeObject(section);
            var loaded = JsonConvert.DeserializeObject<WeaponsSave>(json);
            var rejected = new List<string>();
            var clean = WeaponsSaveRules.Clean(loaded, null, rejected);
            Assert.That(rejected, Is.Empty);
            Assert.That(clean.Weapons[0].Key, Is.EqualTo("k1"));
            Assert.That(clean.Weapons[0].OwnerId, Is.EqualTo("p1"));

            Assert.That(WeaponState.TryFromSave(clean.Weapons[0], null, out var back), Is.True);
            Assert.That(back.DefinitionId, Is.EqualTo(w.DefinitionId));
            Assert.That(back.Level, Is.EqualTo(w.Level));
            Assert.That(back.Abs, Is.EqualTo(w.Abs));
            Assert.That(back.Stats, Is.EqualTo(w.Stats));
            CollectionAssert.AreEqual(w.Fitted, back.Fitted);
            Assert.That(back.History.Count, Is.EqualTo(1));

            var original = w.History[0];
            Assert.That(back.TryReroute(), Is.True);
            Assert.That(back.Stats, Is.EqualTo(original.Stats));
            Assert.That(back.DefinitionId, Is.EqualTo(original.DefinitionId));
            Assert.That(back.Level, Is.EqualTo(original.Level));
        }

        [Test]
        public void Save_KeepsUnknownMembers_ThroughACleanAndASecondSave()
        {
            string json = "{\"weapons\":[{\"key\":\"k\",\"definitionId\":\"axe\",\"future\":5}],\"newSection\":{\"x\":1}}";
            var loaded = JsonConvert.DeserializeObject<WeaponsSave>(json);
            var clean = WeaponsSaveRules.Clean(loaded, null, null);
            string again = JsonConvert.SerializeObject(clean);
            StringAssert.Contains("future", again);
            StringAssert.Contains("newSection", again);
        }

        [Test]
        public void Clean_DropsOnlyTheBadWeapon_AndTrimsBadAttachments()
        {
            var good = Axe().ToSave("good");
            var unknown = Axe().ToSave("unknown"); unknown.DefinitionId = "nope";
            var dup = Axe().ToSave("good");
            var neg = Axe().ToSave("neg"); neg.Atk = -1f;
            var nan = Axe().ToSave("nan"); nan.Abs = float.NaN;
            var over = Axe().ToSave("over");
            over.Fitted = new List<int> { 0, 99, 1, 2, 0 };    // 99 invalid, 3 slots
            var save = new WeaponsSave { Weapons = { good, unknown, dup, neg, nan, over, null } };
            var rejected = new List<string>();
            var clean = WeaponsSaveRules.Clean(save, null, rejected);

            Assert.That(clean.Weapons.ConvertAll(x => x.Key), Is.EqualTo(new[] { "good", "over" }));
            Assert.That(clean.Weapons[1].Fitted, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(rejected.Count, Is.EqualTo(5));
            Assert.That(WeaponsSaveRules.Clean(null, null, null).Weapons, Is.Empty);
        }
    }
}
