// Tests/EditMode/CellarLootPerPlayerTests.cs: tests for CellarLoot.RollFor, the per-player chest roll (per-player loot plan task 2; spec decision 39).
using System;
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

        [Test]
        public void RollFor_RejectsSlotsOutside0To3()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarLoot.RollFor(1u, 1, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarLoot.RollFor(1u, 1, 0, 4));
        }
    }
}
