// Tests/EditMode/PlayerBagTests.cs: tests for SleepingCalamity.Core.PlayerBag and CellarLoot.KindOf (per-player loot plan task 1; spec 4.3, decisions 33-35, 39).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class PlayerBagTests
    {
        static PlayerBag BagWith(params int[] items)
        {
            var b = new PlayerBag();
            foreach (int i in items) Assert.That(b.Add(i), Is.True);
            return b;
        }

        [Test]
        public void Add_RefusesKeysProgressAndAFullBag()
        {
            var b = new PlayerBag();
            Assert.That(b.Add(101), Is.True);
            Assert.That(b.Add(CellarLoot.RoomKeyId), Is.False);
            Assert.That(b.Add(CellarLoot.GateKeyId), Is.False);
            for (int i = 1; i < PlayerBag.MaxCarried; i++) Assert.That(b.Add(101), Is.True);
            Assert.That(b.Carried.Count, Is.EqualTo(PlayerBag.MaxCarried));
            Assert.That(b.Add(101), Is.False);
            Assert.That(new PlayerBag().Add(0), Is.False);
            Assert.That(new PlayerBag().Add(-3), Is.False);
        }

        [Test]
        public void Pick_OnlyWhileTheBankIsOpen_UpToCapacity_OnceEach()
        {
            var b = BagWith(101, 102, 103);
            Assert.That(b.Pick(0), Is.False);
            b.OpenBank(2);
            Assert.That(b.Pick(0), Is.True);
            Assert.That(b.Pick(0), Is.False);
            Assert.That(b.Pick(1), Is.True);
            Assert.That(b.Pick(2), Is.False);   // full
            Assert.That(b.Pick(9), Is.False);
            Assert.That(b.Pick(PlayerBag.SatchelPick), Is.False);   // no satchel carried
        }

        [Test]
        public void PushOn_BanksPicksAndAutoFillsByRank()
        {
            var b = BagWith(101, 112, 110);
            b.OpenBank(2);
            Assert.That(b.Pick(0), Is.True);
            b.PushOn();
            Assert.That(b.BankedThisDive.Count, Is.EqualTo(2));
            Assert.That(b.BankedThisDive[101], Is.EqualTo(1));
            Assert.That(b.BankedThisDive[112], Is.EqualTo(1));
            Assert.That(b.Carried, Is.EqualTo(new[] { 110 }));
            Assert.That(b.BankOpen, Is.False);
            Assert.That(b.Picks.Count, Is.EqualTo(0));
        }

        [Test]
        public void Fail_HalvesDiveGold_PacksCarriedIntoSatchel_KeepsBanked()
        {
            var b = BagWith(101, 104);
            b.OpenBank(1);
            Assert.That(b.Pick(0), Is.True);
            b.PushOn();   // banks 101; the single slot is used, so 104 stays carried
            b.AddGold(7);
            b.Fail(3);
            Assert.That(b.Gold, Is.EqualTo(3));
            Assert.That(b.DiveGold, Is.EqualTo(0));
            Assert.That(b.Stash[101], Is.EqualTo(1));
            Assert.That(b.Satchel.Waiting.Floor, Is.EqualTo(3));
            Assert.That(b.Satchel.Waiting.Items, Is.EqualTo(new[] { 104 }));
            Assert.That(b.Carried, Is.Empty);
            Assert.That(b.BankedThisDive, Is.Empty);
        }

        [Test]
        public void Fail_WithNothingCarried_KeepsTheOldSatchel()
        {
            var b = PlayerBag.Restore(0, new Dictionary<int, int>(), new LostSatchel(2, new[] { 105 }));
            b.Fail(4);
            Assert.That(b.Satchel.Waiting, Is.Not.Null);
            Assert.That(b.Satchel.Waiting.Floor, Is.EqualTo(2));
            Assert.That(b.Satchel.Waiting.Items, Is.EqualTo(new[] { 105 }));
        }

        [Test]
        public void Fail_WhileCarryingTheSatchel_LosesIt_NewOneReplaces()
        {
            var b = PlayerBag.Restore(0, new Dictionary<int, int>(), new LostSatchel(2, new[] { 105 }));
            Assert.That(b.Satchel.PickUp(), Is.True);
            b.Add(106);
            b.Fail(4);
            Assert.That(b.Satchel.Waiting.Items, Is.EqualTo(new[] { 106 }));
            Assert.That(b.Satchel.Waiting.Floor, Is.EqualTo(4));
            Assert.That(b.Satchel.Carried, Is.Null);
            Assert.That(b.Stash.ContainsKey(105), Is.False);
            Assert.That(b.BankedThisDive.ContainsKey(105), Is.False);
        }

        [Test]
        public void GoHome_BringsEverythingHome()
        {
            var b = PlayerBag.Restore(0, new Dictionary<int, int>(), new LostSatchel(2, new[] { 105 }));
            Assert.That(b.Satchel.PickUp(), Is.True);
            b.Add(104);
            b.OpenBank(1);
            b.Pick(0);
            b.PushOn();   // 104 banked
            b.Add(101);
            b.AddGold(9);
            b.GoHome();
            Assert.That(b.Stash[101], Is.EqualTo(1));
            Assert.That(b.Stash[104], Is.EqualTo(1));
            Assert.That(b.Stash[105], Is.EqualTo(1));
            Assert.That(b.Gold, Is.EqualTo(9));
            Assert.That(b.DiveGold, Is.EqualTo(0));
            Assert.That(b.Carried, Is.Empty);
            Assert.That(b.BankedThisDive, Is.Empty);
            Assert.That(b.Satchel.Carried, Is.Null);
            Assert.That(b.BankOpen, Is.False);
        }

        [Test]
        public void SatchelCanBeBanked_AsOneSlot()
        {
            var b = PlayerBag.Restore(0, new Dictionary<int, int>(), new LostSatchel(2, new[] { 105, 106 }));
            Assert.That(b.Satchel.PickUp(), Is.True);
            b.OpenBank(1);
            Assert.That(b.Pick(PlayerBag.SatchelPick), Is.True);
            b.PushOn();
            Assert.That(b.BankedThisDive[105], Is.EqualTo(1));
            Assert.That(b.BankedThisDive[106], Is.EqualTo(1));
            Assert.That(b.Satchel.Carried, Is.Null);
        }

        [Test]
        public void CopyAsFailed_LeavesTheOriginalUntouched()
        {
            var b = BagWith(104);
            b.AddGold(7);
            PlayerBag c = b.CopyAsFailed(2);
            Assert.That(c.Gold, Is.EqualTo(3));
            Assert.That(c.DiveGold, Is.EqualTo(0));
            Assert.That(c.Satchel.Waiting.Floor, Is.EqualTo(2));
            Assert.That(c.Satchel.Waiting.Items, Is.EqualTo(new[] { 104 }));
            Assert.That(c.Carried, Is.Empty);
            Assert.That(b.Gold, Is.EqualTo(0));
            Assert.That(b.DiveGold, Is.EqualTo(7));
            Assert.That(b.Carried, Is.EqualTo(new[] { 104 }));
            Assert.That(b.Satchel.Waiting, Is.Null);
        }

        [Test]
        public void ChestMask_ClearsWhenTheDiveEnds_FailOrGoHome()
        {
            foreach (bool fail in new[] { true, false })
            {
                var b = new PlayerBag();
                b.MarkChestOpened(5, 1, 3);
                if (fail) b.Fail(1); else b.GoHome();
                Assert.That(b.ChestOpened(5, 1, 3), Is.False, fail ? "Fail" : "GoHome");
                Assert.That(b.MarkChestOpened(5, 1, 3), Is.True);
            }
        }

        [Test]
        public void ChestMask_ResetsOnANewFloor_RefusesTwice()
        {
            var b = new PlayerBag();
            Assert.That(b.MarkChestOpened(5, 1, 3), Is.True);
            Assert.That(b.MarkChestOpened(5, 1, 3), Is.False);
            Assert.That(b.ChestOpened(5, 1, 3), Is.True);
            Assert.That(b.MarkChestOpened(5, 2, 3), Is.True);
            Assert.That(b.ChestOpened(5, 1, 3), Is.False);
            Assert.That(b.MarkChestOpened(5, 2, 64), Is.False);
            Assert.That(b.MarkChestOpened(5, 2, -1), Is.False);
        }

        // Version is what lets PlayerBagSync skip rebuilding its snapshot every frame: it must move on every mutation and stay put on a refusal.
        [Test]
        public void Version_MovesOnEveryMutation_NotOnRefusals()
        {
            var b = new PlayerBag();
            int v = b.Version;
            void Moved(string what) { Assert.That(b.Version, Is.GreaterThan(v), what); v = b.Version; }
            void Same(string what) { Assert.That(b.Version, Is.EqualTo(v), what); }

            Assert.That(b.Add(101), Is.True); Moved("Add");
            Assert.That(b.Add(CellarLoot.RoomKeyId), Is.False); Same("a refused key");
            Assert.That(b.Add(0), Is.False); Same("a refused id");
            Assert.That(b.AddGold(5), Is.True); Moved("AddGold");
            Assert.That(b.AddGold(0), Is.False); Same("zero gold");
            Assert.That(b.Pick(0), Is.False); Same("a pick with no visit");
            b.OpenBank(2); Moved("OpenBank");
            Assert.That(b.Pick(0), Is.True); Moved("Pick");
            Assert.That(b.Pick(0), Is.False); Same("the same pick twice");
            Assert.That(b.Pick(7), Is.False); Same("a bad index");
            Assert.That(b.Unpick(0), Is.True); Moved("Unpick");
            Assert.That(b.Unpick(0), Is.False); Same("an unpick of nothing");
            b.CancelBank(); Moved("CancelBank");
            b.CancelBank(); Same("CancelBank with no visit");
            b.PushOn(); Same("PushOn with no visit");
            Assert.That(b.MarkChestOpened(5, 1, 3), Is.True); Moved("MarkChestOpened");
            Assert.That(b.MarkChestOpened(5, 1, 3), Is.False); Same("an open chest again");
            Assert.That(b.MarkChestOpened(5, 1, 64), Is.False); Same("a chest index out of range");
            b.OpenBank(1); v = b.Version;
            b.PushOn(); Moved("PushOn");
            b.Fail(2); Moved("Fail");
            b.GoHome(); Moved("GoHome");
        }

        [Test]
        public void CancelBank_ClosesTheVisitAndKeepsEverythingCarried()
        {
            var b = BagWith(101, 102);
            b.OpenBank(2);
            Assert.That(b.Pick(1), Is.True);
            b.CancelBank();
            Assert.That(b.BankOpen, Is.False);
            Assert.That(b.SlotCapacity, Is.EqualTo(0));
            Assert.That(b.Picks, Is.Empty);
            Assert.That(b.Carried, Is.EqualTo(new[] { 101, 102 }));
            Assert.That(b.BankedThisDive, Is.Empty);
            Assert.That(b.Pick(0), Is.False, "no visit, no pick");
        }

        [Test]
        public void PartyBags_CancelBankVisits_ClosesEveryOpenVisit()
        {
            var p = new PartyBags();
            p.For("a").Add(101); p.For("b").Add(102);
            p.For("a").OpenBank(2); p.For("b").OpenBank(1);
            Assert.That(p.AnyBankOpen, Is.True);
            p.CancelBankVisits();
            Assert.That(p.AnyBankOpen, Is.False);
            Assert.That(p.For("a").Carried, Is.EqualTo(new[] { 101 }));
        }

        [Test]
        public void StashTotal_AndBankedTotal_CountItemsNotKinds()
        {
            var b = BagWith(101, 101, 102);
            b.OpenBank(3);
            Assert.That(b.Pick(0), Is.True); Assert.That(b.Pick(1), Is.True); Assert.That(b.Pick(2), Is.True);
            b.PushOn();
            Assert.That(b.BankedTotal, Is.EqualTo(3));
            Assert.That(b.StashTotal, Is.EqualTo(0));
            b.GoHome();
            Assert.That(b.BankedTotal, Is.EqualTo(0));
            Assert.That(b.StashTotal, Is.EqualTo(3));
        }

        [Test]
        public void KindOf_TableItemsAreLarge_KeysAreKeys()
        {
            foreach (LootItem i in CellarLoot.Items) Assert.That(CellarLoot.KindOf(i.Id), Is.EqualTo(DiveItemKind.Large), i.Name);
            Assert.That(CellarLoot.KindOf(CellarLoot.RoomKeyId), Is.EqualTo(DiveItemKind.Key));
            Assert.That(CellarLoot.KindOf(CellarLoot.GateKeyId), Is.EqualTo(DiveItemKind.Key));
            Assert.That(CellarLoot.KindOf(424242), Is.EqualTo(DiveItemKind.Usable));
            Assert.That(CellarLoot.RankOf(CellarLoot.RoomKeyId), Is.EqualTo(0));
            Assert.That(CellarLoot.RankOf(424242), Is.EqualTo(0));
        }
    }
}
