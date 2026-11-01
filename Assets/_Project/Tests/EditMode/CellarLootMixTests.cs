using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    // Procgen item 1d: the loot-mix steering (CellarLootMix). Placeholder targets, so the tests prove the mechanism (determinism, convergence, no starvation), not feel.
    public sealed class CellarLootMixTests
    {
        static bool[][] Dive(uint seed, int floors = 4) => CellarLootMix.KeyFlagsOfDive(seed, floors);

        [Test]
        public void Replay_IsDeterministic_AndFloorNDoesNotDependOnLaterFloors()
        {
            foreach (uint seed in new uint[] { 0, 1, 12345, 99999 })
            {
                bool[][] d = Dive(seed);
                LootCategory[][] a = CellarLootMix.Replay(seed, d), b = CellarLootMix.Replay(seed, d);
                for (int f = 1; f <= 4; f++)
                {
                    CollectionAssert.AreEqual(a[f - 1], b[f - 1]);
                    for (int i = 0; i < d[f - 1].Length; i++)
                        Assert.AreEqual(a[f - 1][i], CellarLootMix.CategoryFor(seed, f, i, Dive(seed, f)), "seed " + seed + " floor " + f + " chest " + i);
                }
            }
        }

        [Test]
        public void Shares_ConvergeToTheTargets_OverManySeeds_AndNoCategoryStarves()
        {
            var got = new double[3]; var want = new double[3]; double total = 0;
            for (uint seed = 0; seed < 1500; seed++)
            {
                bool[][] d = Dive(seed); LootCategory[][] cats = CellarLootMix.Replay(seed, d);
                for (int f = 1; f <= 4; f++)
                    for (int i = 0; i < d[f - 1].Length; i++)
                    {
                        if (d[f - 1][i]) continue;
                        got[(int)cats[f - 1][i]]++; total++;
                        int[] row = CellarLootMix.TargetRow(f); for (int c = 0; c < 3; c++) want[c] += (double)row[c] / CellarLootMixTuning.Scale;
                    }
            }
            string msg = "";
            for (int c = 0; c < 3; c++) { got[c] /= total; want[c] /= total; msg += (LootCategory)c + " got " + got[c].ToString("F3") + " want " + want[c].ToString("F3") + "; "; }
            UnityEngine.Debug.Log("LootMix shares over 1500 dives, " + total + " chests: " + msg);
            for (int c = 0; c < 3; c++)
            {
                Assert.That(Math.Abs(got[c] - want[c]), Is.LessThan(0.04), msg);
                Assert.That(got[c], Is.GreaterThan(0.05), "no category starves: " + msg);
            }
        }

        [Test]
        public void Feedback_PullsEachDiveCloserToItsTargets_ThanNoFeedback()
        {
            double with = 0, without = 0;
            for (uint seed = 0; seed < 600; seed++)
            {
                bool[][] d = Dive(seed);
                with += Deviation(CellarLootMix.Replay(seed, d), d); without += Deviation(CellarLootMix.Replay(seed, d, 0), d);
            }
            Assert.That(with, Is.LessThan(without * 0.9), "with feedback " + with + " vs none " + without);
        }

        static double Deviation(LootCategory[][] cats, bool[][] keys)
        {
            var count = new double[3]; var exp = new double[3];
            for (int f = 1; f <= cats.Length; f++)
                for (int i = 0; i < cats[f - 1].Length; i++)
                {
                    if (keys[f - 1][i]) continue;
                    count[(int)cats[f - 1][i]]++; int[] row = CellarLootMix.TargetRow(f); for (int c = 0; c < 3; c++) exp[c] += (double)row[c] / CellarLootMixTuning.Scale;
                }
            double dev = 0; for (int c = 0; c < 3; c++) dev += Math.Abs(count[c] - exp[c]); return dev;
        }

        [Test]
        public void Roll_StaysInsideTheSteeredBand_AndTargetsAreSane()
        {
            foreach (int[] row in CellarLootMixTuning.Targets) Assert.AreEqual(CellarLootMixTuning.Scale, row[0] + row[1] + row[2]);
            for (uint seed = 0; seed < 80; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    bool[] keys = Dive(seed, floor)[floor - 1];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i]) continue;
                        LootCategory cat = CellarLootMix.CategoryFor(seed, floor, i, Dive(seed, floor));
                        int rank = CellarLoot.RankOf(CellarLoot.Roll(seed, floor, i, false));
                        Assert.That(rank, Is.InRange(CellarLootMixTuning.BandLo[(int)cat], CellarLootMixTuning.BandHi[(int)cat]));
                    }
                }
        }

        [Test]
        public void LootMixHash_Golden_Seed12345()
        {
            var sb = new System.Text.StringBuilder();
            var got = new ulong[5];
            for (int f = 1; f <= 4; f++) { got[f] = CellarLootMix.LootMixHash(12345, Dive(12345, f)); sb.Append(got[f]).Append("UL, "); }
            string all = sb.ToString();   // pinned 2026-11-01 (procgen 1d loot mix, integer form, deliberate; was the float form 1851538259201835552 ...)
            Assert.AreEqual(5686594895284728819UL, got[1], "floor 1; actual " + all); Assert.AreEqual(9935741248512517506UL, got[2], "floor 2; actual " + all);
            Assert.AreEqual(2709122302315985379UL, got[3], "floor 3; actual " + all); Assert.AreEqual(12947464770208806370UL, got[4], "floor 4; actual " + all);
        }
    }
}
