// M6 S5: the baker's service rule (Core/Town/Placement/BakerService) and the first-meet latch. Pure Core + NUnit.
// Catches: loaves granted with an unfinished bakery, no baker or broken wishes; a missing reason; a latch that un-latches when the baker is picked up or a wish breaks;
// the latch lost in the residents save section (round-trip, merge with the saved copy).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class BakerServiceTests
    {
        static readonly TownLayout Hollow = HollowmereLayout.Create();

        // A town with all four bakery pieces and the baker home; `parts` parts placed after the frame (0-3), baker moved in when `resident`.
        static TownState Town(string lot, int turn, int parts, bool resident)
        {
            var s = new TownState(Hollow);
            foreach (int p in new[] { 100, 101, 200, 201 }) s.AddPiece(p);
            s.AddResident(TownBuildings.BakerResident);
            Assert.That(s.PlaceFrame(TownBuildings.Bakery, lot, turn), Is.EqualTo(PlaceResult.Ok));
            string[] order = { "oven", "door", "sign" };
            for (int i = 0; i < parts; i++) Assert.That(s.PlacePart(TownBuildings.Bakery, order[i]), Is.EqualTo(PlaceResult.Ok));
            if (resident) Assert.That(s.PlaceResident(TownBuildings.Bakery), Is.EqualTo(PlaceResult.Ok));
            return s;
        }

        // Catches: a mid-dive spawn (late joiner, reconnect) with no loaves, or a stale decision carried into town / the next dive.
        [Test]
        public void LoafLedger_ReplaysTheDiveDecisionToMidDiveSpawns_ClearedWhenTheDiveEnds()
        {
            var l = new DiveLoafLedger();
            Assert.That(l.ForSpawn(), Is.Null, "no dive: leave the count alone");
            l.Begin(3);
            Assert.That(l.ForSpawn(), Is.EqualTo(3));
            l.End();
            Assert.That(l.ForSpawn(), Is.Null);
            l.Begin(0);
            Assert.That(l.ForSpawn(), Is.EqualTo(0), "a dive where the baker granted none");
        }

        [Test]
        public void AllMet_GrantsThreePerPlayer()
        {
            BakerLoaves r = BakerService.Evaluate(Town("F02", 0, 3, true));
            Assert.That(r.PerPlayer, Is.EqualTo(3));
            Assert.That(r.Granted, Is.True);
            Assert.That(r.Reason, Is.Null);
            Assert.That(r.Notify, Is.False);
        }

        [Test]
        public void NoBakery_NoLoaves_WithReason()
        {
            BakerLoaves r = BakerService.Evaluate(new TownState(Hollow));
            Assert.That(r.PerPlayer, Is.EqualTo(0)); Assert.That(r.Reason, Is.Not.Empty); Assert.That(r.Notify, Is.False);
            Assert.That(BakerService.Evaluate(null).PerPlayer, Is.EqualTo(0));
        }

        [Test]
        public void IncompleteBakery_NoLoaves_WithReason()
        {
            BakerLoaves r = BakerService.Evaluate(Town("F02", 0, 2, false));
            Assert.That(r.PerPlayer, Is.EqualTo(0)); Assert.That(r.Reason, Is.Not.Empty); Assert.That(r.Notify, Is.False);
        }

        [Test]
        public void NoBaker_NoLoaves_WithReason()
        {
            BakerLoaves r = BakerService.Evaluate(Town("F02", 0, 3, false));
            Assert.That(r.PerPlayer, Is.EqualTo(0)); Assert.That(r.Reason, Does.Contain("moved in")); Assert.That(r.Notify, Is.False);
        }

        [Test]
        public void WishUnmet_NoLoaves_NotifiesWithThePromptText()
        {
            BakerLoaves far = BakerService.Evaluate(Town("F03", 0, 3, true));         // not near the fountain
            Assert.That(far.PerPlayer, Is.EqualTo(0)); Assert.That(far.Notify, Is.True); Assert.That(far.Reason, Is.EqualTo(BakerService.WishesUnmetNotice));
            BakerLoaves turned = BakerService.Evaluate(Town("F02", 1, 3, true));      // door turned away from the square
            Assert.That(turned.PerPlayer, Is.EqualTo(0)); Assert.That(turned.Notify, Is.True);
        }

        [Test]
        public void WishRepairedLater_GrantsAgain()
        {
            TownState s = Town("F02", 1, 3, true);
            Assert.That(BakerService.Evaluate(s).PerPlayer, Is.EqualTo(0));
            Assert.That(s.SetTurn(TownBuildings.Bakery, 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(BakerService.Evaluate(s).PerPlayer, Is.EqualTo(3));
        }

        [Test]
        public void FirstMeet_LatchesWhenBakerIsHoused_AndNeverUnlatches()
        {
            Assert.That(BakerService.NextFirstMeet(false, Town("F02", 0, 3, false)), Is.False, "no baker yet");
            Assert.That(BakerService.NextFirstMeet(false, Town("F02", 0, 2, false)), Is.False);
            TownState s = Town("F02", 0, 3, true);
            bool latched = BakerService.NextFirstMeet(false, s);
            Assert.That(latched, Is.True);
            Assert.That(s.PickUpResident(TownBuildings.Bakery), Is.True);
            latched = BakerService.NextFirstMeet(latched, s);
            Assert.That(latched, Is.True, "picking the baker up does not undo the beat");
            Assert.That(s.PickUpBuilding(TownBuildings.Bakery), Is.True);
            Assert.That(BakerService.NextFirstMeet(latched, s), Is.True, "nor does tearing the bakery down");
            Assert.That(BakerService.NextFirstMeet(false, Town("F03", 0, 3, true)), Is.True, "broken wishes still count as housed");
        }

        [Test]
        public void FirstMeetLatch_RoundTripsThroughTheResidentsSection()
        {
            var residents = new ResidentsSave();
            residents.Residents.Add(new ResidentSave { Id = TownBuildings.BakerResident, FirstMeetBeat = true });
            var stock = new TownStock(Hollow);
            TownSnapshot snap = stock.Capture(null, null, residents);
            Assert.That(TownSaves.FirstMeetBeat(snap.Residents, TownBuildings.BakerResident), Is.True);
            Assert.That(TownSaves.FirstMeetBeat(stock.Capture(null, null).Residents, TownBuildings.BakerResident), Is.False, "null residents: the host keeps its saved beats");
            // a later capture that has not latched yet cannot roll the saved latch back
            ResidentsSave merged = TownSaveRules.MergeLatches(new ResidentsSave(), snap.Residents);
            Assert.That(TownSaves.FirstMeetBeat(merged, TownBuildings.BakerResident), Is.True);
        }
    }
}
