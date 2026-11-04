// Tests/EditMode/CellarLootPerPlayerTests.cs: tests for CellarLoot.RollFor, the per-player chest roll (per-player loot plan task 2; spec decision 39).
using System;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarLootPerPlayerTests
    {
        [Test]
        public void RollFor_IsDeterministic()
        {
            for (int slot = 0; slot < 4; slot++)
                Assert.That(CellarLoot.RollFor(12345u, 2, 3, slot), Is.EqualTo(CellarLoot.RollFor(12345u, 2, 3, slot)));
        }

        [Test]
        public void RollFor_StaysInTheChestsCategoryBand()
        {
            for (int n = 0; n < 200; n++)
            {
                uint seed = (uint)(1000 + n * 7919);
                int floor = 1 + n % 4, chest = n % 8;
                LootCategory cat = CellarLootMix.CategoryFor(seed, floor, chest, CellarLootMix.KeyFlagsOfDive(seed, floor));
                for (int slot = 0; slot < 4; slot++)
                {
                    int rank = CellarLoot.RankOf(CellarLoot.RollFor(seed, floor, chest, slot));
                    Assert.That(rank, Is.InRange(CellarLootMixTuning.BandLo[(int)cat], CellarLootMixTuning.BandHi[(int)cat]), $"seed {seed} floor {floor} chest {chest} slot {slot}");
                }
            }
        }

        [Test]
        public void RollFor_PlayersDoNotAllGetTheSameItems()
        {
            int differ = 0;
            for (int chest = 0; chest < 64; chest++)
                if (CellarLoot.RollFor(98765u, 3, chest, 0) != CellarLoot.RollFor(98765u, 3, chest, 1)) differ++;
            Assert.That(differ, Is.GreaterThanOrEqualTo(16));
        }

        [Test]
        public void RollFor_DoesNotMoveTheSharedRoll()
        {
            // Expected values were captured from the unchanged Roll before RollFor existed.
            Assert.That(CellarLoot.Roll(12345u, 1, 0, false), Is.EqualTo(102));
            Assert.That(CellarLoot.Roll(777u, 2, 5, false), Is.EqualTo(107));
            Assert.That(CellarLoot.Roll(4242424u, 4, 9, false), Is.EqualTo(104));
        }

        // The pre-task-5 RollFor, reproduced from the public pieces (stream, band, triangular draw), as the "today" reference.
        static int OldRollFor(uint seed, int floor, int chestIndex, int playerSlot)
        {
            var rng = new Pcg32(Rng.Derive(seed, "cellars.loot.player", ((ulong)floor << 32) | ((ulong)(uint)chestIndex << 2) | (uint)playerSlot));
            LootCategory cat = CellarLootMix.CategoryFor(seed, floor, chestIndex, CellarLootMix.KeyFlagsOfDive(seed, floor));
            int lo = CellarLootMixTuning.BandLo[(int)cat], hi = CellarLootMixTuning.BandHi[(int)cat];
            float tri = rng.NextFloat() + rng.NextFloat() - 1f;
            int drawn = (int)Math.Round(CellarLoot.PeakRank(floor) + tri * 1.5f, MidpointRounding.AwayFromZero);
            drawn = drawn < 1 ? 1 : drawn > 5 ? 5 : drawn;
            int rank = drawn < lo ? lo : drawn > hi ? hi : drawn;
            int n = 0; foreach (LootItem i in CellarLoot.Items) if (i.Rank == rank) n++;
            int pick = rng.Range(0, n);
            foreach (LootItem i in CellarLoot.Items) if (i.Rank == rank && pick-- == 0) return i.Id;
            return CellarLoot.Items[0].Id;
        }

        [Test]
        public void RollFor_RankBonus0_EqualsTodayForEverySeed()
        {
            for (uint seed = 1; seed <= 500; seed++)
                for (int floor = 1; floor <= 7; floor += 2)
                {
                    int chest = (int)(seed % 8), slot = (int)(seed % 4);
                    int want = OldRollFor(seed, floor, chest, slot);
                    Assert.That(CellarLoot.RollFor(seed, floor, chest, slot, 0), Is.EqualTo(want), $"seed {seed} floor {floor}");
                    Assert.That(CellarLoot.RollFor(seed, floor, chest, slot), Is.EqualTo(want), $"4-arg seed {seed} floor {floor}");
                }
        }

        [Test]
        public void RollFor_RankBonus1_NeverLowersTheRank()
        {
            int raised = 0;
            for (uint seed = 1; seed <= 500; seed++)
            {
                int floor = 1 + (int)(seed % 7), chest = (int)(seed % 8), slot = (int)(seed % 4);
                int r0 = CellarLoot.RankOf(CellarLoot.RollFor(seed, floor, chest, slot, 0)), r1 = CellarLoot.RankOf(CellarLoot.RollFor(seed, floor, chest, slot, 1));
                Assert.That(r1, Is.GreaterThanOrEqualTo(r0), $"seed {seed}");
                Assert.That(r1, Is.LessThanOrEqualTo(CellarLoot.MaxRank));
                if (r1 > r0) raised++;
            }
            Assert.That(raised, Is.GreaterThan(100));
            Assert.That(CellarLoot.RollFor(5u, 2, 1, 0, -3), Is.EqualTo(CellarLoot.RollFor(5u, 2, 1, 0, 0)), "a negative bonus is no bonus");
        }

        [Test]
        public void Boots_AreBankable_KeysAreNot()
        {
            Assert.That(CellarLoot.KindOf(CellarLoot.BootsId), Is.EqualTo(DiveItemKind.Gear));
            Assert.That(BankSlots.IsBankable(DiveItemKind.Gear), Is.True);
            Assert.That(BankSlots.IsBankable(CellarLoot.KindOf(CellarLoot.RoomKeyId)), Is.False);
            Assert.That(BankSlots.IsBankable(CellarLoot.KindOf(CellarLoot.GateKeyId)), Is.False);
            Assert.That(CellarLoot.Items.Any(i => i.Id == CellarLoot.BootsId), Is.False, "boots are not a rank-table item");
            Assert.That(CellarLoot.NameOf(CellarLoot.BootsId), Is.Not.StartWith("Item"));
        }

        [Test]
        public void Boots_OnlyOnePairPerPlayer()
        {
            var bag = new PlayerBag();
            Assert.That(bag.Add(CellarLoot.BootsId), Is.True);
            Assert.That(bag.Add(CellarLoot.BootsId), Is.False, "second pair refused while carried");
            Assert.That(bag.HasBoots, Is.True);
            bag.OpenBank(3);
            Assert.That(bag.Pick(0), Is.True);
            bag.PushOn();
            Assert.That(bag.Carried, Is.Empty);
            Assert.That(bag.HasBoots, Is.True, "banked pair counts");
            Assert.That(bag.Add(CellarLoot.BootsId), Is.False, "second pair refused while banked");
            bag.GoHome();
            Assert.That(bag.HasBoots, Is.True, "stash pair counts");
            Assert.That(bag.Add(CellarLoot.BootsId), Is.False, "second pair refused with one in the stash");
            Assert.That(new PlayerBag().HasBoots, Is.False);
        }

        [Test]
        public void GoldPurse_IsNeverAddedToABag()
        {
            Assert.That(CellarLoot.GoldId, Is.LessThan(0));
            Assert.That(new PlayerBag().Add(CellarLoot.GoldId), Is.False);
            Assert.That(CellarLoot.KindOf(CellarLoot.GoldId), Is.Not.EqualTo(DiveItemKind.Large));
        }

        [Test]
        public void RollChest_GoldPurse_AboutOneInFour_PerPlayerDiffers()
        {
            int gold = 0, total = 0, differ = 0;
            for (int chest = 0; chest < 400; chest++)
            {
                int a = CellarLoot.RollChest(98765u, 2, chest, 0), b = CellarLoot.RollChest(98765u, 2, chest, 1);
                total++; if (a == CellarLoot.GoldId) gold++;
                if ((a == CellarLoot.GoldId) != (b == CellarLoot.GoldId)) differ++;
                Assert.That(CellarLoot.RollChest(98765u, 2, chest, 0), Is.EqualTo(a), "deterministic");
            }
            Assert.That(gold, Is.InRange(total * 15 / 100, total * 35 / 100));
            Assert.That(differ, Is.GreaterThan(40), "players do not all get a purse on the same chests");
        }

        [Test]
        public void RollChest_NonGoldNonBoots_IsTheRollForPick()
        {
            for (uint seed = 1; seed <= 300; seed++)
            {
                int floor = 1 + (int)(seed % 7), chest = (int)(seed % 8), slot = (int)(seed % 4);
                int got = CellarLoot.RollChest(seed, floor, chest, slot);
                if (got == CellarLoot.GoldId || got == CellarLoot.BootsId) continue;
                Assert.That(got, Is.EqualTo(CellarLoot.RollFor(seed, floor, chest, slot, 0)), $"seed {seed}");
            }
        }

        [Test]
        public void RollChest_Boots_RareEnough_NeverTwice_AndMoreLikelyDeep()
        {
            int shallow = 0, deep = 0;
            for (int n = 0; n < 6000; n++)
            {
                uint seed = (uint)(1 + n * 31);
                int chest = n % 8, slot = n % 4;
                if (CellarLoot.RollChest(seed, 2, chest, slot) == CellarLoot.BootsId) shallow++;
                if (CellarLoot.RollChest(seed, 6, chest, slot) == CellarLoot.BootsId) deep++;
                Assert.That(CellarLoot.RollChest(seed, 6, chest, slot, 0, true), Is.Not.EqualTo(CellarLoot.BootsId), "a player with boots never rolls them");
            }
            Assert.That(shallow, Is.GreaterThan(0));
            Assert.That(deep, Is.GreaterThan(shallow));
            Assert.That(shallow, Is.LessThan(600), "rare");
        }

        [Test]
        public void RollFor_RejectsSlotsOutside0To3()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarLoot.RollFor(1u, 1, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarLoot.RollFor(1u, 1, 0, 4));
        }
    }
}
