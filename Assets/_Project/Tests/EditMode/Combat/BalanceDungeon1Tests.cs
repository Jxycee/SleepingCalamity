// Authority: none (pure EditMode test). Pins the Run 5 balance values for the first dungeon (the Cellars) so a later edit cannot move them silently.
// The values, the targets and the reasons are in docs/design/balance-dungeon1.md (decided 2026-11-15, with leeway: changing one on purpose means changing
// this file, the balance note and Tools/balance_sim/data/game_numbers.json together, then re-running Tools\balance_sim\run.cmd).
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Tuning;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Tests.EditMode
{
    public class BalanceDungeon1Tests
    {
        const string Note = " (docs/design/balance-dungeon1.md)";

        [SetUp]
        public void SetUp() => TuningStore.Reset();

        [TearDown]
        public void TearDown() => TuningStore.Reset();

        [Test]
        public void EnemyScales_Tier1Attack_AndTheSoloAssist()
        {
            Assert.AreEqual(0.5f, EnemyCatalog.Tier1Atk, 1e-6f, "tier-1 attack scale" + Note);
            Assert.AreEqual(0.6f, EnemyCatalog.SoloAtk, 1e-6f, "solo attack assist" + Note);
            Assert.AreEqual(0.75f, EnemyCatalog.SoloLife, 1e-6f, "solo life assist" + Note);
            Assert.AreEqual(1, EnemyCatalog.CellarsMaxTier, "the Cellars stay tier 1" + Note);
        }

        [Test]
        public void EnemyLife_BroomRatKingAndWarden()
        {
            Assert.AreEqual(80, EnemyCatalog.Slime.Hp, "slime life is unchanged" + Note);
            Assert.AreEqual(130, EnemyCatalog.Broom.Hp, "broom life" + Note);
            Assert.AreEqual(180, EnemyCatalog.RatKing.Hp, "rat-king life" + Note);
            Assert.AreEqual(2000, EnemyCatalog.DoughWarden.Hp, "Warden life" + Note);
        }

        [Test]
        public void OnePlayerModels_GetTheSoloAssistOnLifeAndAttack()
        {
            Assert.AreEqual(60, new EnemyModel(EnemyCatalog.Slime, 1, 1).MaxHp, "slime 80 x 0.75" + Note);
            Assert.AreEqual(98, new EnemyModel(EnemyCatalog.Broom, 1, 1).MaxHp, "broom 130 x 0.75, rounded up" + Note);
            Assert.AreEqual(135, new EnemyModel(EnemyCatalog.RatKing, 1, 1).MaxHp, "rat-king 180 x 0.75" + Note);
            Assert.AreEqual(1500, new EnemyModel(EnemyCatalog.DoughWarden, EnemyCatalog.AuthoredTier(false, true, 5), 1).MaxHp, "Warden 2000 x 0.75" + Note);
            Assert.AreEqual(12f * 0.5f * 0.6f, new EnemyModel(EnemyCatalog.Slime, 1, 1).Atk, 1e-4f, "slime attack 12 x 0.5 x 0.6" + Note);
            Assert.AreEqual(30f * 0.5f * 0.6f, new EnemyModel(EnemyCatalog.DoughWarden, 1, 1).Atk, 1e-4f, "Warden attack 30 x 0.5 x 0.6 at tier 1" + Note);
        }

        [Test]
        public void TheWarden_IsTier1ByDefault_AndTier2WithTheKnob()
        {
            Assert.AreEqual(1, EnemyCatalog.WardenTier, "Warden tier" + Note);
            Assert.AreEqual(1, EnemyCatalog.AuthoredTier(false, true, 5), "the floor-5 Warden is tier 1" + Note);
            Assert.AreEqual(1, EnemyCatalog.AuthoredTier(true, true, 4), "the Warden tier ignores the tier-2 marker" + Note);
            Assert.AreEqual(1, new EnemyModel(EnemyCatalog.DoughWarden, EnemyCatalog.AuthoredTier(false, true, 5), 1).Tier, "model tier" + Note);
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""enemies"": { ""wardenTier"": 2 } }", "w.json"));
            Assert.AreEqual(2, EnemyCatalog.AuthoredTier(false, true, 5), "wardenTier 2 restores the old tier-2 fight" + Note);
            Assert.AreEqual(1, EnemyCatalog.AuthoredTier(false, false, 5), "regular enemies are not touched by wardenTier" + Note);
            TuningStore.Reset();
            Assert.AreEqual(1, EnemyCatalog.AuthoredTier(false, true, 5), "Reset puts the Warden back to tier 1" + Note);
            Assert.IsTrue(TuningStore.ApplyJson(@"{ ""enemies"": { ""wardenTier"": 3 } }", "w3.json"));
            Assert.AreEqual(1, EnemyCatalog.AuthoredTier(false, true, 5), "wardenTier is limited to 1-2" + Note);
        }

        [Test]
        public void CoopLife_TwoThreeFourPlayers()
        {
            Assert.AreEqual(1.0f, CoopScaling.LifeMultiplier(1), 1e-6f, "solo" + Note);
            Assert.AreEqual(1.5f, CoopScaling.LifeMultiplier(2), 1e-6f, "2 players" + Note);
            Assert.AreEqual(2.3f, CoopScaling.LifeMultiplier(3), 1e-6f, "3 players" + Note);
            Assert.AreEqual(3.0f, CoopScaling.LifeMultiplier(4), 1e-6f, "4 players" + Note);
        }

        [Test]
        public void WeaponGrowth_LevelCostIs40PlusFortyPerLevel()
        {
            Assert.AreEqual(40f, WeaponGrowth.LevelCost(0), 1e-4f, "base stays 40, so the first level-up still lands on floor 1" + Note);
            Assert.AreEqual(80f, WeaponGrowth.LevelCost(1), 1e-4f, "per level 40" + Note);
            Assert.AreEqual(40f + 40f * 12, WeaponGrowth.LevelCost(12), 1e-3f, "per level 40" + Note);
            Assert.AreEqual(40f, WeaponGrowth.LevelCost(1) - WeaponGrowth.LevelCost(0), 1e-4f, "exported as growth.levelCostPerLevel" + Note);
        }

        [Test]
        public void WardenAttacks_HitHarderAndLessOften()
        {
            Assert.AreEqual(2.1f, AttackCatalog.WardenSlam.Multiplier, 1e-6f, "warden_slam" + Note);
            Assert.AreEqual(2.5f, AttackCatalog.WardenPound.Multiplier, 1e-6f, "warden_pound" + Note);
            Assert.AreEqual(2.8f, AttackCatalog.WardenQuake.Multiplier, 1e-6f, "warden_quake" + Note);
            Assert.AreEqual(1.5f, AttackCatalog.WardenSweep.Multiplier, 1e-6f, "warden_sweep" + Note);
            Assert.AreEqual(2.4f, AttackPacing.CooldownWarden, 1e-6f, "Warden cooldown seconds" + Note);
        }

        [Test]
        public void TheShippedBalanceFile_CarriesOnlyValuesEqualToTheCSharpDefaults()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Tuning", "10-balance.json");
            Assert.IsTrue(File.Exists(path), path);
            JObject root = JObject.Parse(File.ReadAllText(path));
            JObject enemies = (JObject)root["enemies"];
            Assert.AreEqual(EnemyCatalog.SoloAtk, (float)enemies["soloAtk"], 1e-6f, "10-balance.json soloAtk must equal the C# default" + Note);
            Assert.AreEqual(EnemyCatalog.SoloLife, (float)enemies["soloLife"], 1e-6f, "10-balance.json soloLife must equal the C# default" + Note);
            Assert.AreEqual(EnemyCatalog.CellarsMaxTier, (int)enemies["cellarsMaxTier"], "10-balance.json cellarsMaxTier must equal the C# default" + Note);
            Assert.IsTrue(TuningStore.ApplyJson(File.ReadAllText(path), "10-balance.json"));
            Assert.AreEqual(0.6f, EnemyCatalog.SoloAtk, 1e-6f, "loading the shipped file keeps the chosen solo attack" + Note);
            Assert.AreEqual(0.75f, EnemyCatalog.SoloLife, 1e-6f, "loading the shipped file keeps the chosen solo life" + Note);
        }
    }
}
