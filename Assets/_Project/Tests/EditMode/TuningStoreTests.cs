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
    }
}
