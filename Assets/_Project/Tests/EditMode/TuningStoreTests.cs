// Tests for Core/Tuning: defaults with no files, an override, bad JSON, unknown keys logged once, folder order.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

// The namespace sits under SleepingCalamity.Core so the file needs only System and NUnit usings, which lets Tools/cloud/sc_core_tests.py run it without Unity.
namespace SleepingCalamity.Core.Tuning.Tests
{
    public class TuningStoreTests
    {
        List<string> _log;
        string _dir;

        [SetUp]
        public void SetUp()
        {
            TuningStore.Reset();
            _log = new List<string>();
            TuningStore.Log = _log.Add;
            _dir = Path.Combine(Path.GetTempPath(), "sc_tuning_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            TuningStore.Reset();
            TuningStore.Log = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void NoFiles_DefaultsAreUnchanged()
        {
            Assert.AreEqual(0, TuningStore.LoadFolders(Path.Combine(_dir, "missing"), null, ""));
            Assert.AreEqual(1.0f, Combat.PoiseMeter.ResetSeconds);
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
            Assert.AreEqual(0.5f, Combat.PoiseMeter.PerExtraPlayer);
            Assert.AreEqual(1.25f, Weapons.WeaponGrowth.FinisherMultiplier);
            Assert.AreEqual(10.0, Weapons.WeaponGrowth.CreditWindowSeconds);
            Assert.AreEqual(5, Weapons.WeaponGrowth.FlatAtkFromLevel);
            Assert.AreEqual(0.9f, Weapons.WeaponGrowth.BuildUpThreshold);
            Assert.AreEqual(0.1f, Weapons.WeaponGrowth.BuildUpBonus);
            Assert.AreEqual(60f, TargetingConfig.SoftConeHalfAngleDeg);
            Assert.AreEqual(6f, TargetingConfig.SoftRange);
            Assert.AreEqual(10f, TargetingConfig.AcquireRange);
            Assert.AreEqual(13f, TargetingConfig.ReleaseRange);
            Assert.AreEqual(0.6f, TargetingConfig.FacingWeight);
            Assert.AreEqual(1f, HeroCombatTuning.KnockbackScale);
            Assert.AreEqual(0.25f, HeroCombatTuning.HurtStagger);
            Assert.AreEqual(1.3f, HeroCombatTuning.ShotHeight);
            Assert.AreEqual(10f, HeroCombatTuning.ShotAssistDeg);
            Assert.AreEqual(0.25f, HeroCombatTuning.ShotRadius);
            Combat.AttackData axe1 = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Assert.AreEqual(34, axe1.TotalFrames);
            Assert.AreEqual(2.3f, axe1.Range);
            Assert.AreEqual(11, axe1.HitOn.Start);
            Assert.AreEqual(0, _log.Count);
        }

        [Test]
        public void Override_AppliesToEachSection()
        {
            Assert.IsTrue(TuningStore.ApplyJson(@"{
                ""poise"": { ""steadySeconds"": 3.5 },
                ""weaponGrowth"": { ""flatAtkFromLevel"": 7, ""creditWindowSeconds"": 12 },
                ""targeting"": { ""softRange"": 8 },
                ""heroCombat"": { ""shotRadius"": 0.4, ""hurtStagger"": 0.1 },
                ""attacks"": { ""axe_1"": { ""range"": 3.1, ""hitOn"": [12, 18], ""guardBreak"": true } }
            }", "t.json"));
            Assert.AreEqual(3.5f, Combat.PoiseMeter.SteadySeconds);
            Assert.AreEqual(1.0f, Combat.PoiseMeter.ResetSeconds);   // untouched keys keep their default
            Assert.AreEqual(7, Weapons.WeaponGrowth.FlatAtkFromLevel);
            Assert.AreEqual(12.0, Weapons.WeaponGrowth.CreditWindowSeconds);
            Assert.AreEqual(8f, TargetingConfig.SoftRange);
            Assert.AreEqual(0.4f, HeroCombatTuning.ShotRadius);
            Assert.AreEqual(0.1f, HeroCombatTuning.HurtStagger);
            Combat.AttackData a = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Assert.AreEqual(3.1f, a.Range);
            Assert.AreEqual(12, a.HitOn.Start);
            Assert.AreEqual(18, a.HitOn.End);
            Assert.IsTrue(a.GuardBreak);
            Assert.AreEqual(34, a.TotalFrames);   // untouched
            Assert.AreEqual(3.1f, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.AirHit1].Range);   // the air version follows the ground one
            Assert.AreEqual(0, _log.Count);

            TuningStore.Reset();
            Assert.AreEqual(2.3f, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1].Range);
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
        }

        [Test]
        public void BadJson_IsIgnoredAndLoggedOnce()
        {
            Assert.IsFalse(TuningStore.ApplyJson("{ \"poise\": { \"steadySeconds\": 9 ", "bad.json"));
            Assert.IsFalse(TuningStore.ApplyJson("{ \"poise\": { \"steadySeconds\": 9 ", "bad.json"));
            Assert.IsFalse(TuningStore.ApplyJson("[1,2]", "list.json"));
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
            Assert.AreEqual(2, _log.Count);
        }

        [Test]
        public void InvalidValues_KeepTheDefault()
        {
            TuningStore.ApplyJson(@"{ ""poise"": { ""steadySeconds"": ""soon"", ""resetSeconds"": -4 },
                ""weaponGrowth"": { ""flatAtkFromLevel"": 2.5 },
                ""attacks"": { ""axe_1"": { ""hitOn"": [1], ""range"": null, ""guardBreak"": 1 } } }", "v.json");
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
            Assert.AreEqual(1.0f, Combat.PoiseMeter.ResetSeconds);
            Assert.AreEqual(5, Weapons.WeaponGrowth.FlatAtkFromLevel);
            Assert.AreEqual(2.3f, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1].Range);
            Assert.AreEqual(6, _log.Count);
        }

        [Test]
        public void UnknownKey_IsLoggedOnce()
        {
            string json = @"{ ""poise"": { ""nonsense"": 1 }, ""mystery"": {}, ""attacks"": { ""no_such_attack"": {} } }";
            TuningStore.ApplyJson(json, "u.json");
            TuningStore.ApplyJson(json, "u.json");
            Assert.AreEqual(3, _log.Count);
            StringAssert.Contains("poise.nonsense", _log[0]);
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
        }

        [Test]
        public void TargetingPairs_ReleaseBelowAcquire_RejectsTheWholeSection()
        {
            TuningStore.ApplyJson(@"{ ""targeting"": { ""softRange"": 9, ""acquireRange"": 12, ""releaseRange"": 11 } }", "p.json");
            Assert.AreEqual(10f, TargetingConfig.AcquireRange);
            Assert.AreEqual(13f, TargetingConfig.ReleaseRange);
            Assert.AreEqual(6f, TargetingConfig.SoftRange);   // the valid key in the same section is rolled back too
            Assert.AreEqual(1, _log.Count);
            StringAssert.Contains("releaseRange", _log[0]);
            // Lowering only acquireRange, or raising only releaseRange, is consistent with the defaults and applies.
            TuningStore.ApplyJson(@"{ ""targeting"": { ""acquireRange"": 8 } }", "q.json");
            Assert.AreEqual(8f, TargetingConfig.AcquireRange);
            // Raising acquireRange past the (default) release range alone is rejected too.
            TuningStore.ApplyJson(@"{ ""targeting"": { ""acquireRange"": 20 } }", "r.json");
            Assert.AreEqual(8f, TargetingConfig.AcquireRange);
        }

        [Test]
        public void TargetingPairs_FlickOffAboveOn_Rejected()
        {
            TuningStore.ApplyJson(@"{ ""targeting"": { ""flickOn"": 0.5, ""flickOff"": 0.6 } }", "f.json");
            Assert.AreEqual(0.75f, TargetingConfig.FlickOn);
            Assert.AreEqual(0.35f, TargetingConfig.FlickOff);
            Assert.AreEqual(1, _log.Count);
            StringAssert.Contains("flickOff", _log[0]);
        }

        [Test]
        public void AttackPairs_WindowStartAfterEnd_RejectsTheAttack()
        {
            TuningStore.ApplyJson(@"{ ""attacks"": { ""axe_1"": { ""range"": 3.1, ""hitOn"": [18, 12] } } }", "w.json");
            Combat.AttackData a = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Assert.AreEqual(2.3f, a.Range, "the valid range in the same attack is not applied either");
            Assert.AreEqual(11, a.HitOn.Start);
            Assert.AreEqual(1, _log.Count);
            StringAssert.Contains("hitOn", _log[0]);
        }

        [Test]
        public void AttackPairs_CancelFromAfterTotalFrames_RejectsTheAttack()
        {
            TuningStore.ApplyJson(@"{ ""attacks"": { ""axe_1"": { ""totalFrames"": 20, ""cancelFrom"": 25 }, ""axe_2"": { ""range"": 2.9 } } }", "c.json");
            Combat.AttackData a = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Assert.AreEqual(34, a.TotalFrames);
            Assert.AreEqual(1, _log.Count);
            StringAssert.Contains("cancelFrom", _log[0]);
            Assert.AreEqual(2.9f, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit2].Range, "another attack in the file still applies");
            // Shortening totalFrames below the existing cancelFrom is rejected as well.
            TuningStore.ApplyJson(@"{ ""attacks"": { ""axe_1"": { ""totalFrames"": 1 } } }", "d.json");
            Assert.AreEqual(34, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1].TotalFrames);
            Assert.AreEqual(2, _log.Count);
        }

        [Test]
        public void AttackOverride_KeepsAirAttacksConsistent_ResetRestoresBoth()
        {
            Combat.AttackData groundBefore = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Combat.AttackData airBefore = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.AirHit1];
            TuningStore.ApplyJson(@"{ ""attacks"": { ""axe_1"": { ""totalFrames"": 40, ""multiplier"": 2 } } }", "a.json");
            Combat.AttackData ground = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Combat.AttackData air = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.AirHit1];
            Assert.AreEqual(40, ground.TotalFrames);
            Assert.AreEqual(ground.AsAir().TotalFrames, air.TotalFrames, "the air version is derived from the tuned ground attack");
            Assert.AreEqual(34, air.TotalFrames);   // 40 scaled by the air factor 0.85
            Assert.AreEqual(2f, ground.Multiplier);
            Assert.AreEqual(1.8f, air.Multiplier, 1e-4f);   // air hits deal 0.9x
            Assert.AreEqual(ground.Range, air.Range);

            TuningStore.Reset();
            Assert.AreEqual(groundBefore.TotalFrames, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1].TotalFrames);
            Assert.AreEqual(airBefore.TotalFrames, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.AirHit1].TotalFrames);
            Assert.AreEqual(groundBefore.Multiplier, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1].Multiplier);
            Assert.AreEqual(airBefore.Multiplier, Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.AirHit1].Multiplier);
        }

        [Test]
        public void IntegerValuedNumbers_Apply()
        {
            TuningStore.ApplyJson(@"{ ""poise"": { ""steadySeconds"": 3 }, ""weaponGrowth"": { ""flatAtkFromLevel"": 7 },
                ""targeting"": { ""softRange"": 8 },
                ""attacks"": { ""axe_1"": { ""range"": 3, ""hitOn"": [12, 18], ""totalFrames"": 36 } } }", "i.json");
            Assert.AreEqual(3f, Combat.PoiseMeter.SteadySeconds);
            Assert.AreEqual(7, Weapons.WeaponGrowth.FlatAtkFromLevel);
            Assert.AreEqual(8f, TargetingConfig.SoftRange);
            Combat.AttackData a = Combat.AttackCatalog.Axe.Attacks[Combat.AttackSet.Hit1];
            Assert.AreEqual(3f, a.Range);
            Assert.AreEqual(12, a.HitOn.Start);
            Assert.AreEqual(36, a.TotalFrames);
            Assert.AreEqual(0, _log.Count);
        }

        [Test]
        public void EmptyFile_IsIgnoredWithoutThrowing()
        {
            Assert.DoesNotThrow(() => Assert.IsFalse(TuningStore.ApplyJson("", "empty.json")));
            Assert.DoesNotThrow(() => Assert.IsFalse(TuningStore.ApplyJson("   \n", "blank.json")));
            Assert.DoesNotThrow(() => Assert.IsFalse(TuningStore.ApplyJson(null, "null.json")));
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
        }

        [Test]
        public void Utf8Bom_IsToleratedWithoutThrowing()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "bom.json"), @"{ ""poise"": { ""steadySeconds"": 4 } }", new System.Text.UTF8Encoding(true));
            Assert.DoesNotThrow(() => Assert.AreEqual(1, TuningStore.LoadFolders(_dir)));
            Assert.AreEqual(4f, Combat.PoiseMeter.SteadySeconds);
            // A BOM left in the string by a caller that decoded the bytes itself.
            Assert.DoesNotThrow(() => TuningStore.ApplyJson("﻿" + @"{ ""poise"": { ""resetSeconds"": 2 } }", "bom2.json"));
            Assert.AreEqual(2f, Combat.PoiseMeter.ResetSeconds);
        }

        [Test]
        public void DuplicateKeys_IgnoreTheWholeFileWithoutThrowing()
        {
            Assert.DoesNotThrow(() => Assert.IsFalse(TuningStore.ApplyJson(@"{ ""poise"": { ""steadySeconds"": 3, ""steadySeconds"": 5 } }", "dup.json")));
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
            Assert.AreEqual(1, _log.Count);
        }

        [Test]
        public void MalformedJson_NeverThrows()
        {
            foreach (string bad in new[] { "{", "}", "{\"poise\":}", "{\"poise\":{\"steadySeconds\":1,}}x", "nul", "{'a':", "\0\0\0", "{} {}" })
                Assert.DoesNotThrow(() => TuningStore.ApplyJson(bad, "m.json"), bad);
            Assert.AreEqual(2.0f, Combat.PoiseMeter.SteadySeconds);
        }

        [Test]
        public void Folders_LaterFolderAndLaterFileWin_BadFileDoesNotBlockOthers()
        {
            string baseDir = Path.Combine(_dir, "base"), modDir = Path.Combine(_dir, "mod");
            Directory.CreateDirectory(baseDir);
            Directory.CreateDirectory(modDir);
            File.WriteAllText(Path.Combine(baseDir, "a.json"), @"{ ""targeting"": { ""softRange"": 7, ""acquireRange"": 11 } }");
            File.WriteAllText(Path.Combine(baseDir, "b.json"), @"{ ""targeting"": { ""softRange"": 9 } }");
            File.WriteAllText(Path.Combine(baseDir, "c.json"), "not json");
            File.WriteAllText(Path.Combine(modDir, "mod.json"), @"{ ""targeting"": { ""softRange"": 5 } }");
            Assert.AreEqual(4, TuningStore.LoadFolders(baseDir, modDir));
            Assert.AreEqual(5f, TargetingConfig.SoftRange);   // mod folder beats base, which had b.json beat a.json
            Assert.AreEqual(11f, TargetingConfig.AcquireRange);
            Assert.AreEqual(1, _log.Count);
        }

        [Test]
        public void Enemies_TierMultipliersOverride_BadValuesKeepDefault_ResetRestores()
        {
            Assert.AreEqual(0.65f, Combat.EnemyCatalog.Tier1Atk);
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""enemies"": { ""tier1Atk"": 0.4, ""tier2Life"": ""x"", ""bogus"": 1 } }", "e.json"));
            Assert.AreEqual(0.4f, Combat.EnemyCatalog.Tier1Atk);
            Assert.AreEqual(3f, Combat.EnemyCatalog.Tier2Life);
            Assert.AreEqual(2, _log.Count);
            Assert.AreEqual(Combat.EnemyCatalog.Slime.Atk * 0.4f, new Combat.EnemyModel(Combat.EnemyCatalog.Slime, 1, 1).Atk, 1e-4f);
            TuningStore.Reset();
            Assert.AreEqual(0.65f, Combat.EnemyCatalog.Tier1Atk);
        }

        [Test]
        public void SoloMultipliers_ApplyToOneHeroOnly()
        {
            var slime = Combat.EnemyCatalog.Slime;
            int solo = new Combat.EnemyModel(slime, 1, 1).MaxHp, duo = new Combat.EnemyModel(slime, 1, 2).MaxHp;
            float soloAtk = new Combat.EnemyModel(slime, 1, 1).Atk, duoAtk = new Combat.EnemyModel(slime, 1, 2).Atk;
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""enemies"": { ""soloAtk"": 0.5, ""soloLife"": 0.5 } }", "s.json"));
            Assert.AreEqual(solo / 2, new Combat.EnemyModel(slime, 1, 1).MaxHp);
            Assert.AreEqual(soloAtk * 0.5f, new Combat.EnemyModel(slime, 1, 1).Atk, 1e-4f);
            Assert.AreEqual(duo, new Combat.EnemyModel(slime, 1, 2).MaxHp, "2 players: unchanged");
            Assert.AreEqual(duoAtk, new Combat.EnemyModel(slime, 1, 2).Atk, 1e-4f);
        }

        [Test]
        public void AuthoredTier_CellarsAreTier1ByDefault_WardenAlwaysTier2_MaxTierRestoresTier2()
        {
            Assert.AreEqual(1, Combat.EnemyCatalog.CellarsMaxTier);
            Assert.AreEqual(1, Combat.EnemyCatalog.AuthoredTier(true, false, 1));
            Assert.AreEqual(1, Combat.EnemyCatalog.AuthoredTier(true, false, 3), "cellars: every regular enemy is tier 1");
            Assert.AreEqual(2, Combat.EnemyCatalog.AuthoredTier(true, true, 4), "the Warden keeps tier 2");
            Assert.AreEqual(1, Combat.EnemyCatalog.CappedTier(2));
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""enemies"": { ""cellarsMaxTier"": 2 } }", "t.json"));
            Assert.AreEqual(2, Combat.EnemyCatalog.AuthoredTier(true, false, 3));
            Assert.AreEqual(1, Combat.EnemyCatalog.AuthoredTier(true, false, 2), "tier2MinFloor still applies");
            Assert.AreEqual(2, Combat.EnemyCatalog.CappedTier(2));
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""enemies"": { ""tier2MinFloor"": 1 } }", "t2.json"));
            Assert.AreEqual(2, Combat.EnemyCatalog.AuthoredTier(true, false, 1));
        }

        [Test]
        public void HurtGrace_DefaultsMatchTheResearchValues_OverrideMovesTheWindow_ResetRestores()
        {
            Assert.AreEqual(0.8f, Combat.HurtGrace.LightSeconds);
            Assert.AreEqual(1.2f, Combat.HurtGrace.HeavySeconds);
            Assert.AreEqual(0.15f, Combat.HurtGrace.MultiHitSeconds);
            Assert.AreEqual(0.9f, Combat.HurtGrace.KnockdownSeconds);
            Assert.AreEqual(0.4f, Combat.HurtGrace.FlickerSeconds);
            Assert.AreEqual(3f, Combat.HurtGrace.HeavyKnockback);
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""hurtGrace"": { ""lightSeconds"": 0.5, ""heavyKnockback"": 4, ""flickerSeconds"": ""x"", ""bogus"": 1 } }", "g.json"));
            Assert.AreEqual(0.5f, Combat.HurtGrace.Seconds(Combat.HurtKind.Light));
            Assert.AreEqual(Combat.HurtKind.Light, Combat.HurtGrace.Classify(3.5f), "the heavy threshold moved to 4");
            Assert.AreEqual(Combat.HurtKind.Heavy, Combat.HurtGrace.Classify(4f));
            Assert.IsFalse(Combat.HurtGrace.IsActive(true, Combat.HurtKind.Light, 0.6f));
            Assert.AreEqual(0.4f, Combat.HurtGrace.FlickerSeconds, "a bad value keeps the default");
            Assert.AreEqual(2, _log.Count);
            TuningStore.Reset();
            Assert.AreEqual(0.8f, Combat.HurtGrace.LightSeconds);
            Assert.AreEqual(3f, Combat.HurtGrace.HeavyKnockback);
        }

        [Test]
        public void HurtGrace_OutOfRangeValuesKeepTheDefault_HeavyShorterThanKnockdownRejectsTheSection()
        {
            TuningStore.ApplyJson(@"{ ""hurtGrace"": { ""lightSeconds"": 0, ""multiHitSeconds"": 99 } }", "r.json");
            Assert.AreEqual(0.8f, Combat.HurtGrace.LightSeconds);
            Assert.AreEqual(0.15f, Combat.HurtGrace.MultiHitSeconds);
            Assert.AreEqual(2, _log.Count);
            TuningStore.ApplyJson(@"{ ""hurtGrace"": { ""lightSeconds"": 0.6, ""heavySeconds"": 0.5 } }", "h.json");
            Assert.AreEqual(1.2f, Combat.HurtGrace.HeavySeconds);
            Assert.AreEqual(0.8f, Combat.HurtGrace.LightSeconds, "the valid key in the rejected section is rolled back too");
            StringAssert.Contains("heavySeconds", _log[2]);
        }
    }
}
