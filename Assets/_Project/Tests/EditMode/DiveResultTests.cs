// Authority: none (pure Core). PL-2 task 2.3: DiveResult, what one player's dive brought home, built from that player's own numbers plus the ending.
// Rules checked against Core, not invented here: a dive that goes home or finishes brings carried, banked and satchel items home and keeps all dive gold (PlayerBag.GoHome); a failed or abandoned
// dive keeps banked items, leaves the carried ones in one lost satchel, loses a carried satchel and keeps half the dive gold (PlayerBag.Fail, DiveGold.KeptOnFail); pieces and people found are safe
// the moment they are picked up (DivePieces), so a fall never takes them back.
// Catches: a result that loses banked items on a fall, counts lost items on a happy ending, keeps the wrong share of gold, drops the people or the floors cleared.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveResultTests
    {
        [Test]
        public void WentHome_BankedItemsCount_NothingLost()
        {
            DiveResult r = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.WentHome, Banked = 3, Carried = 0, DiveGold = 40, ClearedMask = 0b1111, LastFloor = 5 });
            Assert.That(r.ItemsBanked, Is.EqualTo(3));
            Assert.That(r.ItemsHome, Is.EqualTo(3));
            Assert.That(r.ItemsLost, Is.EqualTo(0));
            Assert.That(r.SatchelLost, Is.False);
            Assert.That(r.GoldKept, Is.EqualTo(40));
            Assert.That(r.GoldLost, Is.EqualTo(0));
            Assert.That(r.FloorsCleared, Is.EqualTo(4));
            Assert.That(r.Fell, Is.False);
        }

        [Test]
        public void Fell_UnbankedLost_GoldHalved_PiecesStayFound()
        {
            foreach (DiveEnding ending in new[] { DiveEnding.Failed, DiveEnding.Abandoned })
            {
                DiveResult r = DiveResult.Build(new DiveResultInput { Ending = ending, Banked = 2, Carried = 5, SatchelCarried = true, DiveGold = 41, PiecesFound = 2 });
                Assert.That(r.Fell, Is.True, ending.ToString());
                Assert.That(r.ItemsHome, Is.EqualTo(2), "only the banked items are safe");
                Assert.That(r.ItemsBanked, Is.EqualTo(2));
                Assert.That(r.ItemsLost, Is.EqualTo(5), "the carried items wait in the lost satchel");
                Assert.That(r.SatchelLost, Is.True, "a satchel carried when the dive failed is lost");
                Assert.That(r.GoldKept, Is.EqualTo(DiveGold.KeptOnFail(41)));
                Assert.That(r.GoldKept, Is.EqualTo(20));
                Assert.That(r.GoldLost, Is.EqualTo(21));
                Assert.That(r.PiecesFound, Is.EqualTo(2), "pieces are safe on pickup");
            }
        }

        [Test]
        public void Finished_EverythingCarriedComesHome()
        {
            DiveResult r = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.Finished, Carried = 4, Banked = 1, DiveGold = 9, LastFloor = 7 });
            Assert.That(r.ItemsHome, Is.EqualTo(5));
            Assert.That(r.ItemsBanked, Is.EqualTo(1));
            Assert.That(r.ItemsLost, Is.EqualTo(0));
            Assert.That(r.GoldKept, Is.EqualTo(9));
            Assert.That(r.Fell, Is.False);
        }

        [Test]
        public void PeopleFound_AreListed()
        {
            var people = new List<string> { TownBuildings.BakerResident };
            foreach (DiveEnding ending in new[] { DiveEnding.Failed, DiveEnding.WentHome })
            {
                DiveResult r = DiveResult.Build(new DiveResultInput { Ending = ending, PeopleFound = people });
                Assert.That(r.PeopleFound, Is.EqualTo(new[] { TownBuildings.BakerResident }), ending + ": a person is safe on pickup");
            }
            Assert.That(DiveResult.Build(new DiveResultInput { Ending = DiveEnding.WentHome }).PeopleFound, Is.Empty, "nobody found, an empty list rather than null");
        }

        [Test]
        public void WentHome_RecoveredSatchelItemsComeHome()
        {
            DiveResult r = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.WentHome, Carried = 1, Banked = 1, SatchelCarried = true, SatchelItems = 3 });
            Assert.That(r.ItemsHome, Is.EqualTo(5), "carried, banked and the recovered satchel's three");
            Assert.That(r.SatchelLost, Is.False);
            Assert.That(r.SatchelItems, Is.EqualTo(3));
            string text = string.Join("\n", GuideText.ResultLines(r));
            StringAssert.Contains("satchel", text, "the card says the recovered satchel came home");
            StringAssert.Contains("3", text);
        }

        [Test]
        public void Fell_CarriedSatchelItems_AreGone_NotHome()
        {
            DiveResult r = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.Failed, Banked = 1, Carried = 2, SatchelCarried = true, SatchelItems = 4 });
            Assert.That(r.ItemsHome, Is.EqualTo(1), "only the banked item is safe");
            Assert.That(r.SatchelLost, Is.True);
            string text = string.Join("\n", GuideText.ResultLines(r));
            StringAssert.Contains("gone", text);
            StringAssert.Contains("4", text);
        }

        [Test]
        public void BadNumbers_AreClamped()
        {
            DiveResult r = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.Failed, Carried = -3, Banked = -1, DiveGold = -50, PiecesFound = -2 });
            Assert.That(r.ItemsLost, Is.EqualTo(0));
            Assert.That(r.ItemsHome, Is.EqualTo(0));
            Assert.That(r.GoldKept, Is.EqualTo(0));
            Assert.That(r.GoldLost, Is.EqualTo(0));
            Assert.That(r.PiecesFound, Is.EqualTo(0));
        }

        [Test]
        public void Lines_NameTheEnding_TheLostSatchel_AndThePeople()
        {
            var fell = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.Failed, Carried = 3, Banked = 1, DiveGold = 10, PeopleFound = new List<string> { TownBuildings.BakerResident } });
            string text = string.Join("\n", GuideText.ResultLines(fell));
            Assert.That(text, Does.Contain("satchel"), "a fall says what waits in the satchel");
            Assert.That(text, Does.Contain("3"));
            StringAssert.Contains("baker", text.ToLowerInvariant(), "the person found is named");

            var home = DiveResult.Build(new DiveResultInput { Ending = DiveEnding.WentHome, Carried = 2, DiveGold = 7 });
            Assert.That(string.Join("\n", GuideText.ResultLines(home)), Does.Not.Contain("satchel"), "a happy ending loses nothing");
            foreach (DiveEnding e in new[] { DiveEnding.Failed, DiveEnding.WentHome, DiveEnding.Abandoned, DiveEnding.Finished })
                Assert.That(GuideText.ResultLines(DiveResult.Build(new DiveResultInput { Ending = e })), Is.Not.Empty, e + " has words");
        }
    }
}
