// Tests for the "enemyAttacks" and "enemyPacing" tuning sections (Core/Tuning/TuningSections.cs): enemy attacks are changed IN PLACE, so the
// definitions, the id registry and the network attack index keep pointing at the same objects; the enemy rules still hold after an override; and
// a Reset gives back the exact current numbers (the pin table below fails on any accidental drift).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Tuning;

namespace SleepingCalamity.Tests.EditMode
{
    public class EnemyAttackTuningTests
    {
        List<string> _log;

        [SetUp]
        public void SetUp()
        {
            TuningStore.Reset();
            _log = new List<string>();
            TuningStore.Log = _log.Add;
        }

        [TearDown]
        public void TearDown()
        {
            TuningStore.Reset();
            TuningStore.Log = null;
        }

        // id, totalFrames, hitOn, multiplier, range, halfAngle, knockback, guardBreak, lunge ("a-b@meters"), superArmor, turn ("a-b@deg/s")
        struct Row
        {
            public string Id, HitOn, Lunge, Armor, Turn;
            public int Total;
            public float Mult, Range, Half, Knock;
            public bool GuardBreak;
        }

        static Row R(string id, int total, string hitOn, float mult, float range, float half, float knock, bool gb, string lunge, string armor, string turn) =>
            new Row { Id = id, Total = total, HitOn = hitOn, Mult = mult, Range = range, Half = half, Knock = knock, GuardBreak = gb, Lunge = lunge, Armor = armor, Turn = turn };

        // These are today's numbers (P9 placeholders). Changing one on purpose means changing this table in the same commit.
        static readonly Row[] Pinned =
        {
            R("slime_hop", 60, "34-40", 1.0f, 1.1f, 180f, 1f, false, "16-34@2.2", "none", "0-14@360"),
            R("slime_glob", 66, "46-52", 0.8f, 1.3f, 180f, 1f, false, "none@0", "none", "none@0"),
            R("rat_lunge", 62, "28-36", 1.1f, 1.45f, 42f, 1f, false, "16-28@3.4", "none", "0-14@720"),
            R("rat_spin", 62, "30-40", 1.3f, 1.45f, 180f, 2f, false, "none@0", "none", "none@0"),
            R("broom_sweep", 70, "36-44", 1.2f, 2.4f, 80f, 3f, false, "none@0", "none", "0-20@300"),
            R("broom_poke", 52, "28-34", 1.0f, 1.9f, 30f, 1f, false, "16-28@1.2", "none", "0-14@420"),
            R("warden_slam", 100, "56-64", 1.5f, 3.0f, 55f, 4f, true, "none@0", "0-100", "0-40@200"),
            R("warden_pound", 116, "72-80", 1.8f, 3.0f, 180f, 5f, true, "none@0", "0-116", "none@0"),
            R("warden_sweep", 98, "54-64", 1.1f, 3.0f, 100f, 3f, false, "none@0", "0-98", "0-36@240"),
            R("warden_breakfree", 78, "36-44", 1.0f, 3.0f, 180f, 6f, true, "none@0", "0-78", "none@0"),
            R("test_nova", 60, "12-40", 1.0f, 30f, 180f, 0f, false, "none@0", "none", "none@0"),
            R("warden_quake", 160, "90-100", 2.0f, 3.0f, 180f, 6f, true, "none@0", "0-160", "0-50@160"),
            R("broom_riposte", 58, "27-33", 1.0f, 2.0f, 50f, 2f, false, "none@0", "none", "0-12@420"),
        };

        static string Lunge(AttackData a) => a.Lunge + "@" + a.LungeMeters.ToString(System.Globalization.CultureInfo.InvariantCulture);
        static string Turn(AttackData a) => a.Turn + "@" + a.TurnDegPerSec.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Every tunable value of an attack on one line, to compare before and after.
        static string Snap(AttackData a) => string.Join("|", a.TotalFrames, a.HitOn, a.ComboOpen, a.FinisherAllowed, a.CancelFrom, a.IFrames, a.SuperArmor, Turn(a), Lunge(a),
            a.Multiplier, a.Range, a.HalfAngleDeg, a.HeightBand, a.Knockback, a.Revenge, a.HitstopFrames, a.CameraKick, a.GuardBreak, a.Guardable, a.Fire, a.Poise);

        [Test]
        public void Reset_GivesEveryEnemyAttackItsCurrentNumbers()
        {
            Assert.That(Pinned.Length, Is.EqualTo(AttackCatalog.AllEnemyAttacks.Length), "a new enemy attack needs a row in the pin table");
            foreach (Row r in Pinned)
            {
                AttackData a = AttackCatalog.EnemyAttackById[r.Id];
                Assert.That(a.TotalFrames, Is.EqualTo(r.Total), r.Id + " totalFrames");
                Assert.That(a.HitOn.ToString(), Is.EqualTo(r.HitOn), r.Id + " hitOn");
                Assert.That(a.Multiplier, Is.EqualTo(r.Mult), r.Id + " multiplier");
                Assert.That(a.Range, Is.EqualTo(r.Range), r.Id + " range");
                Assert.That(a.HalfAngleDeg, Is.EqualTo(r.Half), r.Id + " halfAngle");
                Assert.That(a.Knockback, Is.EqualTo(r.Knock), r.Id + " knockback");
                Assert.That(a.GuardBreak, Is.EqualTo(r.GuardBreak), r.Id + " guardBreak");
                Assert.That(Lunge(a), Is.EqualTo(r.Lunge), r.Id + " lunge");
                Assert.That(a.SuperArmor.ToString(), Is.EqualTo(r.Armor), r.Id + " superArmor");
                Assert.That(Turn(a), Is.EqualTo(r.Turn), r.Id + " turn");
                Assert.That(a.CancelFrom, Is.EqualTo(a.TotalFrames), r.Id + " has no early cancel");
                Assert.That(EnemyAttackRules.Problem(a), Is.Null, r.Id + " breaks an enemy rule as shipped");
            }
        }

        [Test]
        public void AnOverride_ChangesTheSameInstanceEverywhere_AndKeepsTheNetworkIndex()
        {
            AttackData byDef = EnemyCatalog.RatKing.Attacks[0];
            int index = Array.IndexOf(AttackCatalog.AllEnemyAttacks, AttackCatalog.RatLunge);
            Assert.That(byDef, Is.SameAs(AttackCatalog.RatLunge));

            TuningStore.ApplyJson(@"{ ""enemyAttacks"": { ""rat_lunge"": { ""multiplier"": 1.2, ""range"": 1.4, ""hitOn"": [30, 38], ""totalFrames"": 64, ""guardBreak"": true } } }", "o.json");

            Assert.That(_log, Is.Empty);
            AttackData a = AttackCatalog.RatLunge;
            Assert.That(a.Multiplier, Is.EqualTo(1.2f));
            Assert.That(a.Range, Is.EqualTo(1.4f));
            Assert.That(a.HitOn.ToString(), Is.EqualTo("30-38"));
            Assert.That(a.TotalFrames, Is.EqualTo(64));
            Assert.That(a.CancelFrom, Is.EqualTo(64), "no early cancel follows the new length");
            Assert.That(a.GuardBreak, Is.True);
            Assert.That(EnemyCatalog.RatKing.Attacks[0], Is.SameAs(a));
            Assert.That(AttackCatalog.AllEnemyAttacks[index], Is.SameAs(a));
            Assert.That(AttackCatalog.EnemyAttackById["rat_lunge"], Is.SameAs(a));
            Assert.That(Array.IndexOf(AttackCatalog.AllEnemyAttacks, a), Is.EqualTo(index));
            Assert.That(a.Tell, Is.Not.Null, "the tell stays");
            Assert.That(AttackCatalog.RatSpin.Multiplier, Is.EqualTo(1.3f), "other attacks are untouched");
        }

        [Test]
        public void TheSimulatorsExportNames_ApplyAsAliases()
        {
            TuningStore.ApplyJson(@"{ ""enemyAttacks"": { ""rat_lunge"": { ""mult"": 1.25, ""hitOnFrame"": 30, ""hitOffFrame"": 40 } } }", "alias.json");
            Assert.That(_log, Is.Empty);
            Assert.That(AttackCatalog.RatLunge.Multiplier, Is.EqualTo(1.25f));
            Assert.That(AttackCatalog.RatLunge.HitOn.ToString(), Is.EqualTo("30-40"));
        }

        [Test]
        public void Reset_RestoresAnOverride()
        {
            string before = Snap(AttackCatalog.WardenQuake);
            TuningStore.ApplyJson(@"{ ""enemyAttacks"": { ""warden_quake"": { ""multiplier"": 3.5, ""knockback"": 9 } } }", "o.json");
            Assert.That(AttackCatalog.WardenQuake.Multiplier, Is.EqualTo(3.5f));
            TuningStore.Reset();
            Assert.That(Snap(AttackCatalog.WardenQuake), Is.EqualTo(before));
            Assert.That(AttackCatalog.WardenQuake.Multiplier, Is.EqualTo(2.0f));
        }

        [Test]
        public void ARejectedOverride_WarnsAndKeepsEveryDefault()
        {
            string[] bad =
            {
                @"{ ""warden_sweep"": { ""hitOn"": [52, 62] } }",                       // under the 54-frame elite wind-up floor
                @"{ ""rat_lunge"": { ""hitOn"": [40, 48] } }",                          // glint leads the hitbox by 26 frames (12-24)
                @"{ ""rat_lunge"": { ""hitOn"": [20, 28] } }",                          // glint only 6 frames ahead
                @"{ ""rat_lunge"": { ""range"": 2.0 } }",                               // reach beyond what the rat-king visibly extends
                @"{ ""rat_lunge"": { ""lunge"": [16, 32] } }",                          // the lunge ends after the hitbox opens
                @"{ ""rat_lunge"": { ""cancelFrom"": 80 } }",                           // cancel after the end of the attack
                @"{ ""rat_lunge"": { ""hitOn"": [36, 28] } }",                          // window runs backwards
                @"{ ""slime_hop"": { ""totalFrames"": 30 } }",                          // ends before its own hit window closes
                @"{ ""slime_hop"": { ""totalFrames"": 45 } }",                          // under the punish window
                @"{ ""no_such_attack"": { ""range"": 1 } }",                            // unknown id
                @"{ ""test_nova"": { ""range"": 1 } }",                                 // the test attack is not tunable
                @"{ ""axe_1"": { ""range"": 1 } }",                                     // a hero attack is the "attacks" section
                @"{ ""rat_lunge"": 5 }",                                                // wrong type: not an object
                @"{ ""rat_lunge"": { ""range"": ""far"" } }",                           // wrong type: not a number
                @"{ ""rat_lunge"": { ""multiplier"": null } }",
                @"{ ""rat_lunge"": { ""guardBreak"": 1 } }",                            // wrong type: not a bool
                @"{ ""rat_lunge"": { ""hitOn"": [28] } }",                              // wrong shape
                @"{ ""rat_lunge"": { ""multiplier"": -1 } }",                           // out of range
                @"{ ""rat_lunge"": { ""totalFrames"": 1000000000 } }",                  // out of range
                @"{ ""rat_lunge"": { ""totalFrames"": 62.5 } }",                        // not a whole number
                @"{ ""rat_lunge"": { ""bogus"": 1 } }",                                 // unknown key
            };
            foreach (string body in bad)
            {
                TuningStore.Reset();
                _log.Clear();
                var before = new List<string>();
                foreach (AttackData a in AttackCatalog.AllEnemyAttacks) before.Add(Snap(a));
                TuningStore.ApplyJson(@"{ ""enemyAttacks"": " + body + " }", "bad.json");
                Assert.That(_log, Is.Not.Empty, "no warning for " + body);
                for (int i = 0; i < before.Count; i++)
                    Assert.That(Snap(AttackCatalog.AllEnemyAttacks[i]), Is.EqualTo(before[i]), AttackCatalog.AllEnemyAttacks[i].Id + " changed by " + body);
            }
        }

        [Test]
        public void AnAttackThatBreaksARule_DoesNotKeepItsOtherValidKeys()
        {
            TuningStore.ApplyJson(@"{ ""enemyAttacks"": { ""rat_lunge"": { ""multiplier"": 5, ""range"": 2.0 } } }", "mixed.json");
            Assert.That(_log, Is.Not.Empty);
            Assert.That(AttackCatalog.RatLunge.Multiplier, Is.EqualTo(1.1f), "the whole attack is rolled back, not half of it");
        }

        [Test]
        public void Copy_IsIndependent_AndTuneEnemyAttackRejectsUnknownInput()
        {
            AttackData a = AttackCatalog.SlimeHop;
            AttackData copy = a.Copy();
            Assert.That(copy, Is.Not.SameAs(a));
            Assert.That(Snap(copy), Is.EqualTo(Snap(a)));
            Assert.That(copy.Tell, Is.SameAs(a.Tell));
            Assert.That(AttackCatalog.TuneEnemyAttack("nope", copy), Is.False);
            Assert.That(AttackCatalog.TuneEnemyAttack("slime_hop", null), Is.False);
        }

        // ---- enemyPacing ----

        [Test]
        public void Pacing_AppliesAndResets()
        {
            float cdSlime = AttackPacing.CooldownSeconds(EnemyKind.Slime, 1), cdWarden = AttackPacing.CooldownSeconds(EnemyKind.DoughWarden, 2);
            TuningStore.ApplyJson(@"{ ""enemyPacing"": { ""cooldown"": { ""default"": 2.0, ""Warden"": 1.0 }, ""recover"": { ""RatKing"": 0.5 },
                ""tier1Scale"": 1.1, ""tier2CooldownScale"": 0.6, ""signatureEvery"": 4 } }", "p.json");
            Assert.That(_log, Is.Empty);
            Assert.That(AttackPacing.CooldownSeconds(EnemyKind.Slime, 1), Is.EqualTo(2.0f * 1.1f).Within(1e-5f));
            Assert.That(AttackPacing.CooldownSeconds(EnemyKind.DoughWarden, 2), Is.EqualTo(1.0f * 0.6f).Within(1e-5f));
            Assert.That(AttackPacing.RecoverSeconds(EnemyKind.RatKing, 1), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AttackPacing.Tier2.CooldownScale, Is.EqualTo(0.6f));
            Assert.That(AttackPacing.Tier2.MaxAttackersPerHero, Is.EqualTo(2), "the other profile fields stay");
            Assert.That(AttackPacing.SignatureEvery, Is.EqualTo(4));

            TuningStore.Reset();
            Assert.That(AttackPacing.CooldownSeconds(EnemyKind.Slime, 1), Is.EqualTo(cdSlime));
            Assert.That(AttackPacing.CooldownSeconds(EnemyKind.DoughWarden, 2), Is.EqualTo(cdWarden));
            Assert.That(AttackPacing.CooldownSeconds(EnemyKind.Slime, 1), Is.EqualTo(1.2f));
            Assert.That(AttackPacing.RecoverSeconds(EnemyKind.RatKing, 1), Is.EqualTo(0.9f));
            Assert.That(AttackPacing.Tier2.CooldownScale, Is.EqualTo(0.7f));
            Assert.That(AttackPacing.SignatureEvery, Is.EqualTo(3));
        }

        [Test]
        public void Pacing_RejectsBadValues_AndAWindowThatLeavesNoPunish()
        {
            string[] bad =
            {
                @"{ ""cooldown"": { ""default"": 0 } }",         // out of range
                @"{ ""cooldown"": { ""default"": ""fast"" } }",   // wrong type
                @"{ ""cooldown"": { ""nope"": 1 } }",             // unknown key
                @"{ ""tier1MaxAttackersPerHero"": 1.5 }",         // not whole
                @"{ ""recover"": { ""default"": 0 } }",           // slime hop would leave 20 punish frames (under 30)
            };
            foreach (string body in bad)
            {
                TuningStore.Reset();
                _log.Clear();
                TuningStore.ApplyJson(@"{ ""enemyPacing"": " + body + " }", "badp.json");
                Assert.That(_log, Is.Not.Empty, "no warning for " + body);
                Assert.That(AttackPacing.CooldownDefault, Is.EqualTo(1.2f), body);
                Assert.That(AttackPacing.RecoverDefault, Is.EqualTo(0.35f), body);
                Assert.That(AttackPacing.Tier1.MaxAttackersPerHero, Is.EqualTo(1), body);
            }
        }
    }
}
