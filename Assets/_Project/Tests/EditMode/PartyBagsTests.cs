// Tests/EditMode/PartyBagsTests.cs: tests for SleepingCalamity.Core.PartyBags (per-player loot plan task 1; one bag per recorded player).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class PartyBagsTests
    {
        [Test]
        public void For_CreatesOncePerId_AllIsOrdinalById()
        {
            var p = new PartyBags();
            PlayerBag b = p.For("b");
            PlayerBag a = p.For("a");
            Assert.That(p.For("b"), Is.SameAs(b));
            Assert.That(p.TryGet("a", out PlayerBag got), Is.True);
            Assert.That(got, Is.SameAs(a));
            Assert.That(p.TryGet("zz", out _), Is.False);
            Assert.That(p.All.Select(kv => kv.Key).ToArray(), Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void FailAll_AndGoHomeAll_ApplyToEveryBag()
        {
            var p = new PartyBags();
            p.For("a").Add(104); p.For("b").Add(110);
            p.For("a").AddGold(8);
            p.FailAll(2);
            Assert.That(p.For("a").Satchel.Waiting.Floor, Is.EqualTo(2));
            Assert.That(p.For("b").Satchel.Waiting.Items, Is.EqualTo(new[] { 110 }));
            Assert.That(p.For("a").Gold, Is.EqualTo(4));
            p.For("a").Add(101); p.For("b").Add(102);
            p.GoHomeAll();
            Assert.That(p.For("a").Stash[101], Is.EqualTo(1));
            Assert.That(p.For("b").Stash[102], Is.EqualTo(1));
            Assert.That(p.AnyCarried, Is.False);
        }

        [Test]
        public void AnyBankOpen_TracksOpenVisits()
        {
            var p = new PartyBags();
            Assert.That(p.AnyBankOpen, Is.False);
            p.For("a").Add(101); p.For("b");
            Assert.That(p.AnyBankOpen, Is.False, "carrying alone is not an open visit");
            p.For("b").OpenBank(2);
            Assert.That(p.AnyBankOpen, Is.True);
            p.PushOnAll();
            Assert.That(p.AnyBankOpen, Is.False);
        }

        [Test]
        public void PushOnAll_OnlyTouchesBagsWithAnOpenVisit()
        {
            var p = new PartyBags();
            p.For("a").Add(101); p.For("b").Add(102);
            p.For("a").OpenBank(1);
            Assert.That(p.For("a").Pick(0), Is.True);   // plan 3 task 8: the push-on banks the picks only
            p.PushOnAll();
            Assert.That(p.For("a").BankedThisDive[101], Is.EqualTo(1));
            Assert.That(p.For("a").BankOpen, Is.False);
            Assert.That(p.For("b").BankedThisDive, Is.Empty);
            Assert.That(p.For("b").Carried, Is.EqualTo(new[] { 102 }));
        }

        [Test]
        public void AnyCarried_TracksCarriedItems()
        {
            var p = new PartyBags();
            Assert.That(p.AnyCarried, Is.False);
            p.For("a").Add(101);
            Assert.That(p.AnyCarried, Is.True);
            p.For("a").GoHome();
            Assert.That(p.AnyCarried, Is.False);
        }

        [Test]
        public void Adopt_ReplacesTheBag()
        {
            var p = new PartyBags();
            var fresh = new PlayerBag(); fresh.Add(103);
            p.Adopt("a", fresh);
            Assert.That(p.For("a"), Is.SameAs(fresh));
        }
    }
}
