// Assets/_Project/Tests/EditMode/BagViewTests.cs: BagView, the owner-only snapshot of a PlayerBag (PlayerBagSync, plan 2 task 5): the copy from the Core bag, its caps, equality and the NGO wire round trip.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using Unity.Collections;
using Unity.Netcode;

namespace SleepingCalamity.Tests.EditMode
{
    public class BagViewTests
    {
        private static BagView RoundTrip(BagView v)
        {
            using (var writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(in v);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    var back = new BagView();
                    back.Carried.Add(999);   // a reused struct must not keep stale items
                    back.Picks.Add(7);
                    reader.ReadNetworkSerializableInPlace(ref back);
                    return back;
                }
            }
        }

        private static PlayerBag RichBag()
        {
            var b = new PlayerBag();
            b.Add(101); b.Add(112); b.Add(104);
            b.AddGold(40);
            b.OpenBank(2);
            b.Pick(2); b.Pick(0);
            b.MarkChestOpened(77u, 3, 5);
            return b;
        }

        [Test]
        public void From_AFreshBag_EqualsDefault()
        {
            Assert.That(BagView.From(new PlayerBag()).Equals(default(BagView)), Is.True);
            Assert.That(BagView.From(null).Equals(default(BagView)), Is.True, "no bag, no view");
        }

        [Test]
        public void From_CopiesTheBagFields()
        {
            BagView v = BagView.From(RichBag());
            Assert.That(v.Carried.Length, Is.EqualTo(3));
            Assert.That(new[] { (int)v.Carried[0], v.Carried[1], v.Carried[2] }, Is.EqualTo(new[] { 101, 112, 104 }));
            Assert.That(new[] { (int)v.Picks[0], v.Picks[1] }, Is.EqualTo(new[] { 2, 0 }), "picks keep their order");
            Assert.That(v.DiveGold, Is.EqualTo(40));
            Assert.That(v.BankOpen, Is.True);
            Assert.That(v.SlotCapacity, Is.EqualTo(2));
            Assert.That(v.ChestSeed, Is.EqualTo(77u));
            Assert.That(v.ChestFloor, Is.EqualTo(3));
            Assert.That(v.OpenedChests, Is.EqualTo(1UL << 5));
            Assert.That(v.SatchelWaiting, Is.False);
            Assert.That(v.SatchelCarried, Is.False);
        }

        [Test]
        public void From_AfterPushOn_CountsBankedItemsAndClosesTheVisit()
        {
            PlayerBag b = RichBag();
            b.PushOn();
            BagView v = BagView.From(b);
            Assert.That(v.BankedThisDive, Is.EqualTo(2));
            Assert.That(v.BankOpen, Is.False);
            Assert.That(v.Picks.Length, Is.EqualTo(0));
            Assert.That(v.Carried.Length, Is.EqualTo(1));
        }

        [Test]
        public void From_TheSatchelAndTheHomeTotals()
        {
            PlayerBag b = RichBag();
            b.PushOn();
            b.Fail(3);   // the banked items go to the stash, the carried one waits in a satchel at floor 3
            BagView waiting = BagView.From(b);
            Assert.That(waiting.SatchelWaiting, Is.True);
            Assert.That(waiting.SatchelCarried, Is.False);
            Assert.That(waiting.SatchelFloor, Is.EqualTo(3));
            Assert.That(waiting.StashTotal, Is.EqualTo(2));
            Assert.That(waiting.Gold, Is.EqualTo(20), "half the dive gold is kept");
            Assert.That(waiting.DiveGold, Is.EqualTo(0));

            Assert.That(b.Satchel.PickUp(), Is.True);
            BagView carried = BagView.From(b);
            Assert.That(carried.SatchelWaiting, Is.False);
            Assert.That(carried.SatchelCarried, Is.True);
            Assert.That(carried.SatchelFloor, Is.EqualTo(3));
        }

        [Test]
        public void From_ShowsTheSatchelPickAs255()
        {
            var b = PlayerBag.Restore(0, new System.Collections.Generic.Dictionary<int, int>(), new LostSatchel(2, new[] { 105 }));
            b.Satchel.PickUp();
            b.OpenBank(1);
            Assert.That(b.Pick(PlayerBag.SatchelPick), Is.True);
            BagView v = BagView.From(b);
            Assert.That(v.Picks.Length, Is.EqualTo(1));
            Assert.That(v.Picks[0], Is.EqualTo(BagView.SatchelPickByte));
            Assert.That(BagView.SatchelPickByte, Is.EqualTo((byte)255));
        }

        [Test]
        public void From_AFullBag_FitsTheFixedLists_AndAnIdTooBigForUshortBecomesZero()
        {
            var b = new PlayerBag();
            for (int i = 0; i < PlayerBag.MaxCarried - 1; i++) Assert.That(b.Add(101), Is.True);
            Assert.That(b.Add(70000), Is.True, "an unknown id is a usable item");
            Assert.That(b.Carried.Count, Is.EqualTo(PlayerBag.MaxCarried));
            BagView v = BagView.From(b);
            Assert.That(v.Carried.Length, Is.EqualTo(PlayerBag.MaxCarried), "all 60 fit, indexes stay aligned with the bag");
            Assert.That(v.Carried[0], Is.EqualTo((ushort)101));
            Assert.That(v.Carried[PlayerBag.MaxCarried - 1], Is.EqualTo((ushort)0), "an id above 65535 shows as 0 (unknown), not a wrapped id");
            Assert.That(new FixedList128Bytes<ushort>().Capacity, Is.GreaterThanOrEqualTo(PlayerBag.MaxCarried), "the view's list must hold a full bag");
        }

        [Test]
        public void From_HugeTotalsSaturate()
        {
            var b = new PlayerBag();
            b.AddGold(int.MaxValue);
            b.AddGold(5);
            Assert.That(BagView.From(b).DiveGold, Is.EqualTo(int.MaxValue));
            var stash = new System.Collections.Generic.Dictionary<int, int> { { 101, int.MaxValue }, { 102, 9 } };
            Assert.That(BagView.From(PlayerBag.Restore(0, stash, null)).StashTotal, Is.EqualTo(int.MaxValue), "the stash total saturates instead of overflowing");
        }

        [Test]
        public void Equality_SameBagEqual_AnyFieldDifferenceNotEqual()
        {
            BagView a = BagView.From(RichBag()), b = BagView.From(RichBag());
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.Equals((object)b), Is.True);

            BagView c = a; c.Carried.Add(1); Assert.That(a.Equals(c), Is.False, "carried");
            c = a; c.Picks.RemoveAt(0); Assert.That(a.Equals(c), Is.False, "picks");
            c = a; c.Gold++; Assert.That(a.Equals(c), Is.False, "gold");
            c = a; c.DiveGold++; Assert.That(a.Equals(c), Is.False, "dive gold");
            c = a; c.StashTotal++; Assert.That(a.Equals(c), Is.False, "stash total");
            c = a; c.BankedThisDive++; Assert.That(a.Equals(c), Is.False, "banked");
            c = a; c.SlotCapacity++; Assert.That(a.Equals(c), Is.False, "capacity");
            c = a; c.BankOpen = !c.BankOpen; Assert.That(a.Equals(c), Is.False, "bank open");
            c = a; c.SatchelWaiting = true; Assert.That(a.Equals(c), Is.False, "satchel waiting");
            c = a; c.SatchelCarried = true; Assert.That(a.Equals(c), Is.False, "satchel carried");
            c = a; c.SatchelFloor++; Assert.That(a.Equals(c), Is.False, "satchel floor");
            c = a; c.ChestSeed++; Assert.That(a.Equals(c), Is.False, "chest seed");
            c = a; c.ChestFloor++; Assert.That(a.Equals(c), Is.False, "chest floor");
            c = a; c.OpenedChests ^= 1UL; Assert.That(a.Equals(c), Is.False, "opened chests");
        }

        [Test]
        public void Serialization_RoundTrips_AndClearsAReusedStruct()
        {
            BagView v = BagView.From(RichBag());
            v.StashTotal = 12; v.Gold = 345; v.SatchelWaiting = true; v.SatchelFloor = 4;
            Assert.That(RoundTrip(v).Equals(v), Is.True);
            Assert.That(RoundTrip(default(BagView)).Equals(default(BagView)), Is.True, "an empty view clears the stale items of a reused struct");

            var full = new PlayerBag();
            for (int i = 0; i < PlayerBag.MaxCarried; i++) full.Add(101 + i % 12);
            full.OpenBank(3);
            full.Pick(59); full.Pick(0); full.Pick(30);
            BagView big = BagView.From(full);
            Assert.That(RoundTrip(big).Equals(big), Is.True, "a full bag round trips");
        }
    }
}
