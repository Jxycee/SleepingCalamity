// Assets/_Project/Tests/EditMode/LostSatchelTests.cs: tests for DiveGold and LostSatchel/SatchelSlot (spec decisions 7, 33-35).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class LostSatchelTests
    {
        [Test]
        public void DiveGold_HalvesWhatTheDiveEarned_RoundingDown()
        {
            Assert.That(DiveGold.KeptOnFail(101), Is.EqualTo(50));
            Assert.That(DiveGold.KeptOnFail(0), Is.EqualTo(0));
            Assert.That(DiveGold.KeptOnFail(-5), Is.EqualTo(0));
        }

        [Test]
        public void Failing_LeavesTheUnbankedItemsAsOneSatchelAtThatDepth()
        {
            var slot = new SatchelSlot();
            slot.OnFail(6, new List<int> { 11, 12, 12 });
            Assert.That(slot.Waiting, Is.Not.Null);
            Assert.That(slot.Waiting.Floor, Is.EqualTo(6));
            Assert.That(slot.Waiting.Items, Is.EqualTo(new[] { 11, 12, 12 }));
            Assert.That(slot.ShouldPlaceOn(6), Is.True);
            Assert.That(slot.ShouldPlaceOn(4), Is.False);
        }

        [Test]
        public void PickedUp_ItTurnsBackIntoItemsOnlyOutsideTheDungeon()
        {
            var slot = new SatchelSlot();
            slot.OnFail(4, new List<int> { 7 });
            Assert.That(slot.PickUp(), Is.True);
            Assert.That(slot.Waiting, Is.Null);
            Assert.That(slot.Carried, Is.Not.Null);
            Assert.That(slot.ShouldPlaceOn(4), Is.False, "it isn't placed again once carried");
            Assert.That(slot.EmptyOutsideTheDungeon(), Is.EqualTo(new[] { 7 }));
            Assert.That(slot.Carried, Is.Null);
            Assert.That(slot.EmptyOutsideTheDungeon(), Is.Empty);
        }

        [Test]
        public void FailingWhileCarryingIt_LosesItForGood_AndLeavesTheNewHaul()
        {
            var slot = new SatchelSlot();
            slot.OnFail(4, new List<int> { 7 });
            slot.PickUp();
            slot.OnFail(6, new List<int> { 9 });
            Assert.That(slot.Carried, Is.Null);
            Assert.That(slot.Waiting.Items, Is.EqualTo(new[] { 9 }), "the old satchel's items are gone");
        }

        [Test]
        public void ANewFailure_ReplacesAWaitingSatchel_UnlessItDroppedNothing()
        {
            var slot = new SatchelSlot();
            slot.OnFail(2, new List<int> { 1 });
            slot.OnFail(7, new List<int> { 2 });
            Assert.That(slot.Waiting.Floor, Is.EqualTo(7));
            slot.OnFail(3, new List<int>());
            Assert.That(slot.Waiting.Floor, Is.EqualTo(7), "an empty failure leaves the waiting satchel where it is");
        }

        [Test]
        public void PickUp_FailsWhenNothingWaits()
        {
            Assert.That(new SatchelSlot().PickUp(), Is.False);
        }
    }
}
