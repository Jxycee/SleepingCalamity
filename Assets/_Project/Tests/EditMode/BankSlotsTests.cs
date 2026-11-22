// Tests/EditMode/BankSlotsTests.cs: tests for SleepingCalamity.Core.BankSlots (spec decisions 4, 5, 34).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class BankSlotsTests
    {
        [Test]
        public void Bankable_WeaponsUsablesGearLargeAndTheSatchel_NeverKeysOrProgress()
        {
            foreach (var k in new[] { DiveItemKind.Weapon, DiveItemKind.Usable, DiveItemKind.Gear, DiveItemKind.Large, DiveItemKind.Satchel })
                Assert.That(BankSlots.IsBankable(k), Is.True, k.ToString());
            Assert.That(BankSlots.IsBankable(DiveItemKind.Key), Is.False);
            Assert.That(BankSlots.IsBankable(DiveItemKind.Progress), Is.False);
        }

        [Test]
        public void Totals_ByPartySize()
        {
            Assert.That(BankSlots.TotalSlots(1), Is.EqualTo(3));
            Assert.That(BankSlots.TotalSlots(2), Is.EqualTo(4));
            Assert.That(BankSlots.TotalSlots(3), Is.EqualTo(4));
            Assert.That(BankSlots.TotalSlots(4), Is.EqualTo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => BankSlots.TotalSlots(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BankSlots.TotalSlots(5));
        }

        [Test]
        public void PerPlayer_ThreePlayersGetOneEach_PlusTheExtraForItsOwner()
        {
            Assert.That(BankSlots.SlotsFor(1, 0, -1), Is.EqualTo(3));
            Assert.That(BankSlots.SlotsFor(2, 1, -1), Is.EqualTo(2));
            Assert.That(BankSlots.SlotsFor(3, 2, 2), Is.EqualTo(2));
            Assert.That(BankSlots.SlotsFor(3, 0, 2), Is.EqualTo(1));
            Assert.That(BankSlots.SlotsFor(4, 3, -1), Is.EqualTo(1));
        }

        [Test]
        public void ExtraSlot_IsDeterministic_AndNeverTheSameOwnerTwiceInARow()
        {
            var present = new List<int> { 0, 2, 3 };
            for (ulong seed = 1; seed <= 500; seed++)
            {
                int a = BankSlots.PickExtraSlotOwner(seed, present, 2);
                Assert.That(a, Is.Not.EqualTo(2), $"seed {seed}");
                Assert.That(present, Does.Contain(a));
                Assert.That(BankSlots.PickExtraSlotOwner(seed, present, 2), Is.EqualTo(a), "same seed, same pick");
            }
        }

        [Test]
        public void ExtraSlot_WithNoPreviousOwner_CanPickAnyone()
        {
            var present = new List<int> { 0, 1, 2 };
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 200; seed++) seen.Add(BankSlots.PickExtraSlotOwner(seed, present, -1));
            Assert.That(seen, Is.EquivalentTo(present));
        }

        [Test]
        public void PickExtraSlotOwner_EmptyList_ReturnsMinusOne()
        {
            Assert.That(BankSlots.PickExtraSlotOwner(7UL, new List<int>(), -1), Is.EqualTo(-1));
            Assert.That(BankSlots.PickExtraSlotOwner(7UL, new List<int>(), 2), Is.EqualTo(-1));
        }

        static int Sum(int[] a) { int t = 0; foreach (int x in a) t += x; return t; }

        [Test]
        public void Allocate_NoPicks_IsSlotsFor()
        {
            Assert.That(BankSlots.Allocate(2, new[] { 0, 1 }, -1, new[] { 0, 0 }), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(BankSlots.Allocate(3, new[] { 0, 1, 2 }, 1, new[] { 0, 0, 0 }), Is.EqualTo(new[] { 1, 2, 1 }));
            Assert.That(BankSlots.Allocate(1, new[] { 3 }, -1, null), Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void Allocate_SoloWithThreePicksAndAJoiner_KeepsThePicks_JoinerGetsTheRest()
        {
            int[] cap = BankSlots.Allocate(2, new[] { 0, 1 }, -1, new[] { 3, 0 });
            Assert.That(cap, Is.EqualTo(new[] { 3, 1 }), "a keeps all three picks; the joiner gets what remains of four");
            Assert.That(Sum(cap), Is.EqualTo(BankSlots.TotalSlots(2)));
        }

        [Test]
        public void Allocate_SurplusComesOutOfUnusedSlotsOnly()
        {
            // three players, extra owner slot 2 (unused); slot 0 has two picks, slot 1 none: the 5 wanted slots lose the owner's unused extra, not a pick
            int[] cap = BankSlots.Allocate(3, new[] { 0, 1, 2 }, 2, new[] { 2, 0, 0 });
            Assert.That(Sum(cap), Is.EqualTo(BankSlots.TotalSlots(3)));
            Assert.That(cap[0], Is.EqualTo(2));
            for (int i = 0; i < 3; i++) Assert.That(cap[i], Is.GreaterThanOrEqualTo(i == 0 ? 2 : 0));
        }

        [Test]
        public void Allocate_NeverBelowPicks_EvenWhenPicksExceedTheTotal()
        {
            int[] cap = BankSlots.Allocate(2, new[] { 0, 1 }, -1, new[] { 3, 3 });
            Assert.That(cap, Is.EqualTo(new[] { 3, 3 }), "a hand-edited save: nothing is taken from a filled slot");
        }

        [Test]
        public void Allocate_ShrinkingParty_ShareTheFullTotal()
        {
            Assert.That(Sum(BankSlots.Allocate(2, new[] { 0, 1 }, -1, new[] { 1, 0 })), Is.EqualTo(BankSlots.TotalSlots(2)));
            Assert.That(Sum(BankSlots.Allocate(1, new[] { 0 }, -1, new[] { 2 })), Is.EqualTo(BankSlots.TotalSlots(1)));
        }

        [Test]
        public void IsStirValuable_OnlyWeaponAndLarge()
        {
            foreach (DiveItemKind k in Enum.GetValues(typeof(DiveItemKind)))
                Assert.That(BankSlots.IsStirValuable(k), Is.EqualTo(k == DiveItemKind.Weapon || k == DiveItemKind.Large), k.ToString());
        }
    }
}
