// Authority: none (pure EditMode test). Exports the REAL tuning numbers (Core catalogs, growth, pacing, co-op, healing, stir, floor mix) to
// Tools/balance_sim/data/game_numbers.json so the Python balance simulator reads the same values as the game.
//   - SC_BALANCE_EXPORT=1: writes the file (every section, floorStats included).
//   - otherwise: the committed file must match the live numbers in every section except "floorStats" (drift guard).
// The JSON is hand-written (stable key order, invariant culture) so the output is byte-for-byte deterministic.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;

namespace SleepingCalamity.Tests.EditMode
{
    public class BalanceExportTests
    {
        const string ExportEnv = "SC_BALANCE_EXPORT";
        const int StatSeeds = 200, FloorCount = 7;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- a tiny ordered JSON tree ----
        sealed class Obj
        {
            public readonly List<KeyValuePair<string, object>> Items = new List<KeyValuePair<string, object>>();
            public Obj Add(string key, object value) { Items.Add(new KeyValuePair<string, object>(key, value)); return this; }
        }

        static string Num(object v)
        {
            switch (v)
            {
                case int i: return i.ToString(Inv);
                case float f: return f.ToString("R", Inv);
                case double d: return d.ToString("R", Inv);
                default: throw new ArgumentException("not a number: " + v);
            }
        }

        static void Write(StringBuilder sb, object v, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); break;
                case Obj o:
                    sb.Append("{\n");
                    for (int i = 0; i < o.Items.Count; i++)
                    {
                        sb.Append(' ', (depth + 1) * 2).Append('"').Append(o.Items[i].Key).Append("\": ");
                        Write(sb, o.Items[i].Value, depth + 1);
                        sb.Append(i + 1 < o.Items.Count ? ",\n" : "\n");
                    }
                    sb.Append(' ', depth * 2).Append('}');
                    break;
                case System.Collections.IEnumerable list when !(v is string):
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list) { if (!first) sb.Append(", "); first = false; Write(sb, item, depth + 1); }
                    sb.Append(']');
                    break;
                default: sb.Append(Num(v)); break;
            }
        }

        // ---- the export ----
        static readonly (EnemyDefinition def, string key)[] EnemyKeys =
        {
            (EnemyCatalog.Slime, "Slime"), (EnemyCatalog.RatKing, "RatKing"), (EnemyCatalog.Broom, "Broom"), (EnemyCatalog.DoughWarden, "Warden"),
        };

        static Obj Damage() => new Obj()
            .Add("critChance", DamageFormula.CritChance).Add("critMultiplier", DamageFormula.CritMultiplier)
            .Add("elementPerPoint", DamageFormula.ElementPerPoint).Add("tagPerPoint", DamageFormula.TagPerPoint)
            .Add("comboBonusPerHit", DamageFormula.ComboBonusPerHit).Add("minDamage", DamageFormula.MinimumDamage)
            .Add("guardMult", GuardMath.GuardedDamage).Add("playerDef", PlayerVitals.BaseDef).Add("playerMaxHp", PlayerVitals.BaseMaxHp)
            .Add("rageMult", 1f + RageTracker.AttackBonus).Add("rageHits", RageTracker.HitsToRage).Add("rageSeconds", RageTracker.RageSeconds)
            .Add("rageTelegraphSeconds", RageTracker.TelegraphSeconds)
            .Add("openingDamage", EnemyWindows.OpeningDamage)
            .Add("frontBlockPassedMult", FrontalBlock.PassedDamage).Add("frontBlockHalfAngleDeg", FrontalBlock.HalfAngleDeg)
            .Add("guardHalfAngleDeg", GuardMath.FrontHalfAngleDeg).Add("parryFrames", GuardMath.ParryFrames)
            .Add("dodgeTotalFrames", DodgeTiming.TotalFrames).Add("dodgeIFrameStart", DodgeTiming.IFrameStart).Add("dodgeIFrameEnd", DodgeTiming.IFrameEnd)
            .Add("spellFireValue", EnemyModel.SpellFireValue);

        static Obj Enemies()
        {
            var o = new Obj();
            foreach (var (def, key) in EnemyKeys)
            {
                var ids = new List<string>();
                foreach (AttackData a in def.Attacks) ids.Add(a.Id);
                o.Add(key, new Obj().Add("hp", def.Hp).Add("atk", def.Atk).Add("def", def.Def).Add("speed", def.MoveSpeed)
                    .Add("weakFire", def.WeakToFire).Add("isPest", def.IsPest).Add("frontBlock", def.FrontalBlock)
                    .Add("poiseThreshold", def.PoiseThreshold).Add("staggerSeconds", def.StaggerSeconds).Add("isElite", def.IsElite)
                    .Add("canRage", def.CanRage).Add("ripostes", def.Ripostes).Add("attacks", ids));
            }
            return o;
        }

        static Obj TierScale() => new Obj()
            .Add("tier1Atk", EnemyCatalog.Tier1Atk).Add("tier2Atk", EnemyCatalog.Tier2Atk).Add("tier2Life", EnemyCatalog.Tier2Life).Add("tier2Def", EnemyCatalog.Tier2Def)
            .Add("cellarsMaxTier", EnemyCatalog.CellarsMaxTier).Add("wardenTier", EnemyCatalog.AuthoredTier(false, true, 5))
            .Add("tier2MinFloor", EnemyCatalog.Tier2MinFloor).Add("soloAtk", EnemyCatalog.SoloAtk).Add("soloLife", EnemyCatalog.SoloLife);

        static Obj EnemyAttacks()
        {
            var o = new Obj();
            foreach (AttackData a in AttackCatalog.AllEnemyAttacks)
            {
                if (a.Id == AttackCatalog.TestNova.Id) continue;
                o.Add(a.Id, new Obj().Add("totalFrames", a.TotalFrames).Add("hitOnFrame", a.HitOn.Start).Add("hitOffFrame", a.HitOn.End)
                    .Add("mult", a.Multiplier).Add("range", a.Range).Add("guardBreak", a.GuardBreak).Add("guardable", a.Guardable));
            }
            return o;
        }

        static Obj EnemyPacing()
        {
            float t1c = AttackPacing.Tier1.CooldownScale, t1r = AttackPacing.Tier1.RecoverScale;
            return new Obj()
                .Add("cooldown", new Obj().Add("default", AttackPacing.CooldownSeconds(EnemyKind.Slime, 1) / t1c)
                    .Add("RatKing", AttackPacing.CooldownSeconds(EnemyKind.RatKing, 1) / t1c).Add("Warden", AttackPacing.CooldownSeconds(EnemyKind.DoughWarden, 1) / t1c))
                .Add("recover", new Obj().Add("default", AttackPacing.RecoverSeconds(EnemyKind.Slime, 1) / t1r).Add("RatKing", AttackPacing.RecoverSeconds(EnemyKind.RatKing, 1) / t1r))
                .Add("tier1Scale", t1c).Add("tier1RecoverScale", t1r)
                .Add("tier2CooldownScale", AttackPacing.Tier2.CooldownScale).Add("tier2RecoverScale", AttackPacing.Tier2.RecoverScale)
                .Add("tier1MaxAttackersPerHero", AttackPacing.Tier1.MaxAttackersPerHero).Add("tier2MaxAttackersPerHero", AttackPacing.Tier2.MaxAttackersPerHero)
                .Add("tier1MinStrikeGapSeconds", AttackPacing.Tier1.MinStrikeGapSeconds).Add("tier2MinStrikeGapSeconds", AttackPacing.Tier2.MinStrikeGapSeconds)
                .Add("signatureEvery", AttackPacing.SignatureEvery).Add("signatureExtraRecoverSeconds", AttackPacing.SignatureExtraRecoverSeconds);
        }

        static Obj HeroAttacks()
        {
            var o = new Obj();
            var sets = new (string weapon, AttackSet set)[]
            {
                ("axe", AttackCatalog.Axe), ("staff", AttackCatalog.Staff), ("dagger", AttackCatalog.Dagger), ("bow", AttackCatalog.Bow),
            };
            foreach (var (weapon, set) in sets)
            {
                for (int slot = AttackSet.Hit1; slot <= AttackSet.Special; slot++)
                {
                    AttackData a = set.Attacks[slot];
                    if (a == null) continue;
                    string kind = slot == AttackSet.Hit1 ? "normal1" : slot == AttackSet.Hit2 ? "normal2" : slot == AttackSet.Finisher ? "finisher"
                        : slot == AttackSet.Charge ? "charge" : a.Kind == AttackKind.Shot ? "shot" : "spell";
                    o.Add(a.Id, new Obj().Add("weapon", weapon).Add("kind", kind).Add("totalFrames", a.TotalFrames).Add("hitOnFrame", a.HitOn.Start)
                        .Add("hitOffFrame", a.HitOn.End).Add("mult", a.Multiplier).Add("range", a.Range).Add("guardBreak", a.GuardBreak).Add("fire", a.Fire));
                }
            }
            return o;
        }

        static Obj Weapons()
        {
            var o = new Obj();
            string[] ids =
            {
                WeaponCatalog.Axe, WeaponCatalog.Staff, WeaponCatalog.DaggerShield, WeaponCatalog.BowSword,
                WeaponCatalog.CinderAxe, WeaponCatalog.WardenStaff, WeaponCatalog.BaneDagger, WeaponCatalog.EmberBow,
            };
            foreach (string id in ids)
            {
                Assert.IsTrue(WeaponCatalog.Default.TryGet(id, out WeaponDefinition d), id);
                o.Add(id, new Obj().Add("atk", d.Base.Atk).Add("fire", d.Base.Fire).Add("pest", d.Base.Pest).Add("slots", d.SlotCount)
                    .Add("buildUpTarget", d.BuildUpTargetId).Add("family", WeaponStyle.FamilyOf(id).ToString()));
            }
            // the three attachment drops (ember +5 fire, pest-bane +5 pest, heartwood +2 ATK)
            o.Add("_attachments", new Obj()
                .Add("ember", new Obj().Add("atk", Attachments.Bonus(AttachmentKind.Ember).Atk).Add("fire", Attachments.Bonus(AttachmentKind.Ember).Fire).Add("pest", Attachments.Bonus(AttachmentKind.Ember).Pest))
                .Add("pestBane", new Obj().Add("atk", Attachments.Bonus(AttachmentKind.PestBane).Atk).Add("fire", Attachments.Bonus(AttachmentKind.PestBane).Fire).Add("pest", Attachments.Bonus(AttachmentKind.PestBane).Pest))
                .Add("heartwood", new Obj().Add("atk", Attachments.Bonus(AttachmentKind.Heartwood).Atk).Add("fire", Attachments.Bonus(AttachmentKind.Heartwood).Fire).Add("pest", Attachments.Bonus(AttachmentKind.Heartwood).Pest)));
            return o;
        }

        static Obj Growth()
        {
            // level cost is 40 x (1 + 0.5 L): base = LevelCost(0), per level = LevelCost(1) - LevelCost(0)
            float costBase = WeaponGrowth.LevelCost(0), costPer = WeaponGrowth.LevelCost(1) - costBase;
            return new Obj()
                .Add("absPerKill", new Obj().Add("Slime", WeaponGrowth.SpeciesValue(Species.Slime)).Add("RatKing", WeaponGrowth.SpeciesValue(Species.RatKing))
                    .Add("Broom", WeaponGrowth.SpeciesValue(Species.Broom)).Add("Elite", WeaponGrowth.SpeciesValue(Species.Elite)))
                .Add("finisherKillMult", WeaponGrowth.FinisherMultiplier).Add("creditWindowSeconds", WeaponGrowth.CreditWindowSeconds)
                .Add("levelCostBase", costBase).Add("levelCostPerLevel", costPer)
                .Add("atkPerLevelEarly", WeaponGrowth.AtkGain(1)).Add("earlyLevels", WeaponGrowth.FlatAtkFromLevel)
                .Add("atkPerLevelLate", WeaponGrowth.AtkGain(WeaponGrowth.FlatAtkFromLevel + 1))
                .Add("buildUpThreshold", WeaponGrowth.BuildUpThreshold).Add("buildUpBonus", WeaponGrowth.BuildUpBonus)
                .Add("styleAbs", new Obj()
                    .Add("axeFinisherHit", WeaponStyle.StyleAbs(WeaponCatalog.Axe, StyleEvent.FinisherHit))
                    .Add("axeGuardBreak", WeaponStyle.StyleAbs(WeaponCatalog.Axe, StyleEvent.GuardBreak))
                    .Add("daggerHit", WeaponStyle.StyleAbs(WeaponCatalog.DaggerShield, StyleEvent.Hit))
                    .Add("daggerParry", WeaponStyle.StyleAbs(WeaponCatalog.DaggerShield, StyleEvent.Parry))
                    .Add("staffExtraSwing", WeaponStyle.StyleAbs(WeaponCatalog.Staff, StyleEvent.ExtraSwingHit))
                    .Add("bowWeakShot", WeaponStyle.StyleAbs(WeaponCatalog.BowSword, StyleEvent.WeakPointShot)))
                .Add("styleAbsCapPerEnemy", WeaponStyle.PerEnemyCap);
        }

        static Obj Coop()
        {
            var life = new List<float>(); var cap = new List<int>();
            for (int p = 1; p <= 4; p++)
            {
                life.Add(CoopScaling.LifeMultiplier(p));
                int c = CoopScaling.GlobalStrikeCap(p);
                cap.Add(c == int.MaxValue ? 0 : c);   // 0 = no cap (solo)
            }
            return new Obj().Add("lifeMult", life).Add("extraEnemiesPerExtraPlayer", CoopScaling.ExtraEnemiesPerRoom(2)).Add("strikeCap", cap)
                .Add("deathDownedSeconds", PlayerVitals.DownedSeconds).Add("reviveHoldSeconds", PlayerVitals.ReviveHoldSeconds)
                .Add("reviveHpFraction", PlayerVitals.ReviveHpFraction)
                .Add("poisePerExtraPlayer", Core.Tuning.PoiseTuning.PerExtraPlayer).Add("revengeCapPerExtraPlayer", RevengeGauge.PerExtraPlayer);
        }

        static List<int> HealPointFloors()
        {
            var floors = new List<int>();
            for (int f = CellarHealing.FirstFloor; f <= CellarHealing.LastFloor; f++)
                if (!CellarFloorGenerator.Layout(1u, f).IsStub) floors.Add(f);   // a stub floor (3 bakery, 5 guardian arena) never rolls a point
            return floors;
        }

        static Obj Healing()
        {
            HealTuning h = HealTuning.Default;
            return new Obj().Add("healPointChancePercent", h.ChancePercent).Add("healPointFloors", HealPointFloors())
                .Add("healPointRechargeSeconds", h.RechargeSeconds).Add("loafHeal", HeroVitals.LoafHeal)
                .Add("loavesPerDive", BakerService.LoavesPerPlayer).Add("loavesNeedBakery", true).Add("bankFullHeal", true)
                .Add("breadLadderFreeLoavesUnused", BreadLadder.FreeLoavesPerDive);
        }

        static Obj Stir() => new Obj()
            .Add("perFloorCleared", DiveStir.PerFloorCleared).Add("perUnbankedValuable", DiveStir.PerUnbankedValuable).Add("max", DiveStir.Max)
            .Add("extraEnemyThreshold", DiveStir.ExtraEnemyThreshold).Add("hunterThreshold", DiveStir.HunterThreshold).Add("deepClosesThreshold", DiveStir.DeepClosesThreshold)
            .Add("collapseMultiplier", DiveStir.CollapseMultiplier).Add("collapseMinSeconds", DiveStir.CollapseMinSeconds).Add("rewardRankStep", DiveStir.RewardRankStep);

        static Obj FloorMix()
        {
            var o = new Obj();
            for (int f = 1; f <= FloorCount; f++)
            {
                int[] w = CellarEnemyMix.WeightsFor(f);   // Slime, Broom, RatKing; floors past 4 clamp to the floor-4 band
                o.Add(f.ToString(Inv), new Obj().Add("Slime", w[0]).Add("Broom", w[1]).Add("RatKing", w[2]));
            }
            return o;
        }

        static Obj Floors()
        {
            var o = new Obj();
            var heal = HealPointFloors();
            for (int f = 1; f <= FloorCount; f++)
                o.Add(f.ToString(Inv), new Obj().Add("kind", DiveFloorTable.KindOf(f).ToString()).Add("calm", DiveFloorTable.IsCalmFloor(f))
                    .Add("guardian", f == UndercroftPlan.BankFloor).Add("hasHealPoint", heal.Contains(f)));
            return o;
        }

        static string KindKey(EnemyKind k) => k == EnemyKind.DoughWarden ? "Warden" : k.ToString();

        // Runs the real generator + encounter plan + template resolver (solo, no stir extras, no hunter) over seeds 1..StatSeeds.
        static Obj FloorStats()
        {
            var o = new Obj();
            for (int f = 1; f <= FloorCount; f++)
            {
                long combatRooms = 0, enemies = 0, t1 = 0, t2 = 0, heal = 0; int seeds = 0;
                var kinds = new Dictionary<EnemyKind, long>();
                for (uint seed = 1; seed <= StatSeeds; seed++)
                {
                    CellarFloorLayout layout = CellarFloorGenerator.Layout(seed, f);
                    CellarFloorDressing dressing = CellarFloorDressing.Resolve(layout);
                    CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(layout, dressing, 0, false);
                    seeds++;
                    if (CellarHealing.PointFor(layout, dressing).HasValue) heal++;
                    for (int r = 0; r < layout.Rooms.Count; r++)
                    {
                        if (layout.Rooms[r].Room.Kind != RoomKind.Combat || plan.CountIn(r) == 0) continue;
                        int inRoom = 0;
                        CellarTemplateRoom tr = CellarTemplateSpawns.Resolve(layout, dressing, plan, r, 1, null);
                        if (tr != null)
                        {
                            foreach (TemplateSpawn ts in tr.Spawns)
                            {
                                inRoom++; Count(kinds, ts.Kind);
                                if (EnemyCatalog.AuthoredTier(false, false, f) == 2) t2++; else t1++;
                            }
                        }
                        else
                        {
                            int index = 0;
                            foreach (EncounterSpawn sp in plan.Spawns)
                            {
                                if (sp.RoomIndex != r || sp.Kind == EncounterKind.Hunter) continue;
                                bool warden = layout.WardenCell.HasValue && layout.WardenCell.Value.X == sp.X && layout.WardenCell.Value.Y == sp.Y;
                                int tier = EnemyCatalog.AuthoredTier(sp.Tier == SpawnTier.Tier2, warden, f);
                                EnemyKind kind = warden ? EnemyKind.DoughWarden : sp.Tier == SpawnTier.Tier2 ? EnemyKind.RatKing : CellarEnemyMix.Tier1Kind(layout.Seed, f, r, index, plan.RosterMask);
                                index++; inRoom++; Count(kinds, kind);
                                if (tier == 2) t2++; else t1++;
                            }
                        }
                        if (inRoom > 0) { combatRooms++; enemies += inRoom; }
                    }
                }
                var share = new Obj();
                foreach (EnemyKind k in new[] { EnemyKind.Slime, EnemyKind.Broom, EnemyKind.RatKing, EnemyKind.DoughWarden })
                    share.Add(KindKey(k), enemies == 0 ? 0.0 : (kinds.TryGetValue(k, out long n) ? n : 0L) / (double)enemies);
                o.Add(f.ToString(Inv), new Obj().Add("seeds", seeds).Add("meanCombatRooms", combatRooms / (double)seeds)
                    .Add("meanEnemiesPerRoom", combatRooms == 0 ? 0.0 : enemies / (double)combatRooms)
                    .Add("meanEnemiesTotal", enemies / (double)seeds).Add("meanTier1Enemies", t1 / (double)seeds).Add("meanTier2Enemies", t2 / (double)seeds)
                    .Add("healPointShare", heal / (double)seeds).Add("kindShare", share));
            }
            return o;
        }

        static void Count(Dictionary<EnemyKind, long> d, EnemyKind k) { d.TryGetValue(k, out long n); d[k] = n + 1; }

        static Obj Build(bool withStats)
        {
            var root = new Obj().Add("version", 1)
                .Add("damage", Damage()).Add("enemies", Enemies()).Add("tierScale", TierScale()).Add("enemyAttacks", EnemyAttacks()).Add("enemyPacing", EnemyPacing())
                .Add("heroAttacks", HeroAttacks()).Add("weapons", Weapons()).Add("growth", Growth()).Add("coop", Coop()).Add("healing", Healing()).Add("stir", Stir())
                .Add("floorMix", FloorMix()).Add("floors", Floors());
            if (withStats) root.Add("floorStats", FloorStats());
            return root;
        }

        static string ToJson(Obj root)
        {
            var sb = new StringBuilder();
            Write(sb, root, 0);
            return sb.Append('\n').ToString();
        }

        static string JsonPath()
        {
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;   // the checkout root (Assets' parent)
            return Path.Combine(root, "Tools", "balance_sim", "data", "game_numbers.json");
        }

        [Test]
        public void BalanceNumbers_ExportOrMatchCommittedFile()
        {
            string path = JsonPath();
            if (Environment.GetEnvironmentVariable(ExportEnv) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, ToJson(Build(true)), new UTF8Encoding(false));
                UnityEngine.Debug.Log("SC BALANCE EXPORT wrote " + path);
                Assert.IsTrue(File.Exists(path));
                return;
            }

            Assert.IsTrue(File.Exists(path), "missing " + path + ": run with SC_BALANCE_EXPORT=1 to regenerate");
            JObject committed = JObject.Parse(File.ReadAllText(path));
            JObject live = JObject.Parse(ToJson(Build(false)));
            committed.Remove("floorStats");   // measured, not guarded
            live.Remove("floorStats");
            if (JToken.DeepEquals(committed, live)) return;

            var diffs = new List<string>();
            foreach (var kv in live)
            {
                if (!committed.TryGetValue(kv.Key, out JToken old)) diffs.Add(kv.Key + " (missing in file)");
                else if (!JToken.DeepEquals(old, kv.Value)) diffs.Add(kv.Key + ": " + FirstDiff(old, kv.Value, kv.Key));
            }
            foreach (var kv in committed) if (!live.ContainsKey(kv.Key)) diffs.Add(kv.Key + " (only in file)");
            Assert.Fail("Tools/balance_sim/data/game_numbers.json has drifted from the game numbers: run with SC_BALANCE_EXPORT=1 to regenerate it. " + string.Join("; ", diffs));
        }

        // The simulator writes override files under the exporter's own key names; the tuning sections must accept every one of them, as written, with the
        // exported value (so applying the exported numbers changes nothing and warns about nothing).
        [Test]
        public void ExportedEnemyAttackAndPacingKeys_ApplyVerbatimThroughTheTuningSections()
        {
            var log = new List<string>();
            Core.Tuning.TuningStore.Reset();
            Core.Tuning.TuningStore.Log = log.Add;
            try
            {
                var before = new List<string>();
                foreach (AttackData a in AttackCatalog.AllEnemyAttacks) before.Add(a.TotalFrames + "|" + a.HitOn + "|" + a.Multiplier + "|" + a.Range + "|" + a.GuardBreak + "|" + a.Guardable);
                float cd = AttackPacing.CooldownSeconds(EnemyKind.RatKing, 2), rc = AttackPacing.RecoverSeconds(EnemyKind.RatKing, 1);
                string json = ToJson(new Obj().Add("enemyAttacks", EnemyAttacks()).Add("enemyPacing", EnemyPacing()));
                Assert.IsTrue(Core.Tuning.TuningStore.ApplyJson(json, "export.json"));
                Assert.IsEmpty(log, string.Join("; ", log));
                for (int i = 0; i < before.Count; i++)
                {
                    AttackData a = AttackCatalog.AllEnemyAttacks[i];
                    Assert.AreEqual(before[i], a.TotalFrames + "|" + a.HitOn + "|" + a.Multiplier + "|" + a.Range + "|" + a.GuardBreak + "|" + a.Guardable, a.Id);
                }
                Assert.AreEqual(cd, AttackPacing.CooldownSeconds(EnemyKind.RatKing, 2));
                Assert.AreEqual(rc, AttackPacing.RecoverSeconds(EnemyKind.RatKing, 1));

                // and the values really land: every exported attack key set to a changed value moves the C# field it maps to
                Assert.IsTrue(Core.Tuning.TuningStore.ApplyJson(@"{ ""enemyAttacks"": { ""rat_spin"": { ""mult"": 1.5, ""hitOnFrame"": 31, ""hitOffFrame"": 41, ""range"": 1.4, ""guardable"": false } } }", "alias.json"));
                Assert.IsEmpty(log, string.Join("; ", log));
                Assert.AreEqual(1.5f, AttackCatalog.RatSpin.Multiplier);
                Assert.AreEqual(31, AttackCatalog.RatSpin.HitOn.Start);
                Assert.AreEqual(41, AttackCatalog.RatSpin.HitOn.End);
                Assert.AreEqual(1.4f, AttackCatalog.RatSpin.Range);
                Assert.IsFalse(AttackCatalog.RatSpin.Guardable);
            }
            finally { Core.Tuning.TuningStore.Reset(); Core.Tuning.TuningStore.Log = null; }
        }

        static string FirstDiff(JToken a, JToken b, string path)
        {
            if (a is JObject ao && b is JObject bo)
            {
                foreach (var kv in bo)
                {
                    if (!ao.TryGetValue(kv.Key, out JToken x)) return path + "." + kv.Key + " new";
                    if (!JToken.DeepEquals(x, kv.Value)) return FirstDiff(x, kv.Value, path + "." + kv.Key);
                }
                foreach (var kv in ao) if (!bo.ContainsKey(kv.Key)) return path + "." + kv.Key + " removed";
            }
            return path + " file " + a + " vs live " + b;
        }
    }
}
