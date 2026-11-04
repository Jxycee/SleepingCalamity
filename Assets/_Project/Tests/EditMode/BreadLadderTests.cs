// Tests/EditMode/BreadLadderTests.cs: tests for SleepingCalamity.Core.BreadLadder (spec decisions 20, 27).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class BreadLadderTests
    {
        [Test]
        public void SurvivorsBread_IsWeakerThanTheBakers()
        {
            Assert.That(BreadLadder.HealPerLoaf(false, 0), Is.EqualTo(15));
            Assert.That(BreadLadder.HealPerLoaf(false, 3), Is.EqualTo(15), "upgrades need the baker");
            Assert.That(BreadLadder.HealPerLoaf(true, 0), Is.EqualTo(30));
        }

        [Test]
        public void BakerUpgrades_RaiseTheHeal()
        {
            Assert.That(BreadLadder.HealPerLoaf(true, 2), Is.EqualTo(50));
            Assert.That(BreadLadder.HealPerLoaf(true, -1), Is.EqualTo(30));
        }

        [Test]
        public void Carried_IsFreePlusExtra_CappedAtTen()
        {
            Assert.That(BreadLadder.LoavesAtDiveStart(0), Is.EqualTo(3));
            Assert.That(BreadLadder.LoavesAtDiveStart(4), Is.EqualTo(7));
            Assert.That(BreadLadder.LoavesAtDiveStart(20), Is.EqualTo(BreadLadder.StackCap));
            Assert.That(BreadLadder.LoavesAtDiveStart(-2), Is.EqualTo(3));
        }
    }
}
