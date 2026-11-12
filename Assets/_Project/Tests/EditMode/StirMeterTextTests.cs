// Authority: none (pure EditMode). PL-2 task 2.4 (QUEUE :51): the HUD's "N home" piece count after a reload.
// Catches: a count that misses pieces the town holds after RestoreWorld (stocked, placed, or an old save's banked ones) or counts the baker as a piece.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.UI;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class StirMeterTextTests
    {
        static readonly IReadOnlyDictionary<int, int> Depths = PieceDepths.Assign(12345UL, PieceCatalog.Specs);

        [Test]
        public void HomeCount_AfterReload_CountsBankedPieces()
        {
            // A town with a placed frame and a stocked oven, saved; the file also carries an old save's banked door and baker. Reloaded: three pieces home, the baker is a person.
            var stock = new TownStock(HollowmereLayout.Create());
            stock.State.AddPiece(PieceCatalog.FrameId); stock.State.AddPiece(PieceCatalog.OvenId);
            Assert.That(stock.State.PlaceFrame(TownBuildings.Bakery, "F02", 0), Is.EqualTo(PlaceResult.Ok));
            var session = new DiveSession(new PartyBags(), new DiveCheckpoint(), new DivePieces(Depths));
            TownSnapshot saved = stock.CaptureWorld(session, false, null, Depths, null);
            saved.Dive.BankedPieces.Add(PieceCatalog.DoorId);
            saved.Dive.BankedResidents.Add(TownBuildings.BakerResident);
            var fresh = new TownStock(HollowmereLayout.Create());
            TownStock.WorldRestore w = fresh.RestoreWorld(TownSaveRules.CleanSnapshot(saved, null, null, false), Depths);
            Assert.That(StirMeterText.HomeCount(w.Pieces), Is.EqualTo(3), "placed frame + stocked oven + the banked door; the baker is a person");
            Assert.That(StirMeterText.HomeCount(null), Is.EqualTo(0));
        }
    }
}
