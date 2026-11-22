// Authority: none (pure UI/Core logic). PL-2 task 2.3 fix round 1: the rule that keeps the result card's bag snapshot safe from the ordering of two replicated values. The end's bag (emptied, satchel set) can reach
// a peer before NetActive=false does; the model must keep the last real snapshot instead. Also the BagView field the card reads for a recovered satchel.
// Catches: a post-end view replacing the snapshot (the card would show nothing lost and no gold), a real change (an item banked, gold earned) being ignored, a satchel count that does not survive the wire.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.UI;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveResultModelTests
    {
        static DiveResultInput Held() => new DiveResultInput { Carried = 2, Banked = 1, DiveGold = 10 };

        [Test]
        public void EmptyView_AfterSomethingWasHeld_IsTheEndsBag()
        {
            Assert.That(DiveResultModel.IsPostEnd(Held(), BagView.From(new PlayerBag())), Is.True);
        }

        [Test]
        public void RealChanges_AreNotPostEnd()
        {
            var bag = new PlayerBag();
            bag.Add(104); bag.AddGold(3);
            Assert.That(DiveResultModel.IsPostEnd(Held(), BagView.From(bag)), Is.False, "still carrying and earning");
            var banked = new PlayerBag();
            banked.Add(104); banked.OpenBank(3); banked.Pick(0); banked.PushOn();
            BagView v = BagView.From(banked);
            Assert.That(v.Carried.Length, Is.EqualTo(0));
            Assert.That(v.BankedThisDive, Is.EqualTo(1));
            Assert.That(DiveResultModel.IsPostEnd(new DiveResultInput { Carried = 1 }, v), Is.False, "banking the last carried item empties the hands but fills the bank");
        }

        [Test]
        public void NothingHeldBefore_NothingToProtect()
        {
            Assert.That(DiveResultModel.IsPostEnd(default, BagView.From(new PlayerBag())), Is.False);
        }

        [Test]
        public void CarriedSatchel_CountsItsItems()
        {
            var bag = PlayerBag.Restore(0, new System.Collections.Generic.Dictionary<int, int>(), new LostSatchel(2, new[] { 104, 105, 110 }));
            Assert.That(bag.PickUpSatchel(), Is.True);
            BagView v = BagView.From(bag);
            Assert.That(v.SatchelCarried, Is.True);
            Assert.That(v.SatchelItems, Is.EqualTo(3));
            Assert.That(v.Equals(BagView.From(bag)), Is.True);
            Assert.That(BagView.From(PlayerBag.Restore(0, new System.Collections.Generic.Dictionary<int, int>(), new LostSatchel(2, new[] { 104 }))).SatchelItems, Is.EqualTo(0), "a waiting satchel is not carried");
        }
    }
}
