// Assets/_Project/Tests/EditMode/DiveMomentumTests.cs: tests for DiveMomentum and BossRetry (spec decisions 25, 32, 36, 37).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveMomentumTests
    {
        [Test]
        public void PushingOnAfterWalkingToTheBank_RaisesSecondHalfChests()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(1);
            Assert.That(m.ChestRankBonus(6), Is.EqualTo(0), "not before pushing on");
            m.OnPushOn();
            Assert.That(m.ChestRankBonus(4), Is.EqualTo(0), "first-half floors never get it");
            Assert.That(m.ChestRankBonus(6), Is.EqualTo(1));
            Assert.That(m.ChestRankBonus(10), Is.EqualTo(1));
        }

        [Test]
        public void StartingAtTheBank_StartsCold()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(UndercroftPlan.BankFloor);
            m.OnPushOn();
            Assert.That(m.PushedOn, Is.False);
            Assert.That(m.ChestRankBonus(7), Is.EqualTo(0));
        }

        [Test]
        public void TheEndChestRoom_NeedsAnUnbrokenDiveFromFloorOne()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(3);
            m.OnPushOn();
            Assert.That(m.ChestRankBonus(6), Is.EqualTo(1), "a floor-pick start still earns momentum");
            Assert.That(m.EndChestRoomOpens(true), Is.False, "but not the unbroken-dive room");

            m.OnDiveStart(1);
            m.OnPushOn();
            Assert.That(m.EndChestRoomOpens(false), Is.False);
            Assert.That(m.EndChestRoomOpens(true), Is.True);
        }

        [Test]
        public void BossRetry_OnlyAtTheBoss_OncePerDive()
        {
            var r = new BossRetry();
            r.OnDiveStart();
            int gold = 1000;
            Assert.That(r.PriceFor(UndercroftPlan.BankFloor, gold), Is.EqualTo(RetryPrice.None), "no retry for the floor-5 guardian");
            Assert.That(r.TryRetry(UndercroftPlan.BossFloor, ref gold), Is.True);
            Assert.That(gold, Is.EqualTo(1000 - BossRetry.GoldPrice));
            Assert.That(r.TryRetry(UndercroftPlan.BossFloor, ref gold), Is.False, "once per dive");
            r.OnDiveStart();
            Assert.That(r.PriceFor(UndercroftPlan.BossFloor, gold), Is.EqualTo(RetryPrice.Gold), "a new dive gets a new retry");
        }

        [Test]
        public void BossRetry_TheIdolPaysFirst_GoldIsTheFallback()
        {
            var r = new BossRetry();
            r.OnDiveStart();
            r.ActivateIdol();
            int gold = 0;
            Assert.That(r.PriceFor(UndercroftPlan.BossFloor, gold), Is.EqualTo(RetryPrice.Idol));
            Assert.That(r.TryRetry(UndercroftPlan.BossFloor, ref gold), Is.True);
            Assert.That(gold, Is.EqualTo(0), "the idol costs no gold");

            r.OnDiveStart();
            Assert.That(r.IdolActivated, Is.False, "the idol is found again each dive");
            Assert.That(r.PriceFor(UndercroftPlan.BossFloor, BossRetry.GoldPrice - 1), Is.EqualTo(RetryPrice.None), "too little gold and no idol");
        }
    }
}
