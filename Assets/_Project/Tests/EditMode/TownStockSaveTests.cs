// M6 step 0 / plan 3 task 12: TownStock as the host's town source across a save. The capture comes from a live DiveSession (TownStock.CaptureWorld), the restore is RestoreWorld; what an
// OLD save held (banked pieces, a banked baker, the 4-floor dive's loot) is read and comes home exactly once; the new capture writes none of it. Pure Core + NUnit.
// Catches: a bag lost or written wrong on the way through the sections (a mid-dive bag must be the failed copy), a client-id fallback bag written, the live bag changed by a capture, an old
// save's banked piece or baker lost or stocked twice, a capture that still writes the retired loot or banked-piece fields. Does not catch: the host's file I/O (WorldSaveStoreTests).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownStockSaveTests
    {
        const string Baker = "baker";
        static readonly IReadOnlyDictionary<int, int> Depths = PieceDepths.Assign(12345UL, PieceCatalog.Specs);
        static readonly PlayerKey KeyA = new PlayerKey(IdentityKind.Ugs, "aaa");

        static DiveSession NewSession() => new DiveSession(new PartyBags(), new DiveCheckpoint(), new DivePieces(Depths));

        // Reloads the way the host does: capture (cleaned and conserved), a cleaned copy of the loaded sections, RestoreWorld into a fresh stock.
        static (TownStock.WorldRestore world, TownStock stock) Reload(TownStock stock, DiveSession session)
        {
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(stock.CaptureWorld(session, false, null, Depths, null), null, null, false);
            var fresh = new TownStock(HollowmereLayout.Create());
            return (fresh.RestoreWorld(loaded, Depths), fresh);
        }

        [Test]
        public void Receive_MovesTheFoundPiecesToStock_Once_AndTheBakerWhenRescued()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            var pieces = new DivePieces(Depths);
            pieces.Pick(PieceCatalog.FrameId); pieces.Pick(PieceCatalog.OvenId);
            Assert.That(stock.Receive(pieces, bakerRescued: false), Is.EqualTo(2), "no bank press needed: a piece is safe on pickup");
            pieces.Pick(PieceCatalog.DoorId);
            Assert.That(stock.Receive(pieces, false), Is.EqualTo(1), "only the new one arrives");
            Assert.That(stock.Receive(pieces, false), Is.EqualTo(0), "nothing moves twice");
            Assert.That(stock.State.ResidentInStock(Baker), Is.False);
            stock.Receive(pieces, bakerRescued: true); stock.Receive(pieces, bakerRescued: true);
            Assert.That(stock.State.ResidentInStock(Baker), Is.True);
            Assert.That(stock.State.StockResidents.Count(), Is.EqualTo(1), "a resident exists once");
            Assert.That(stock.State.StockPieces.ToList(), Is.EqualTo(new[] { 100, 101, 200 }));
        }

        [Test]
        public void Receive_APickedUpPerson_BecomesAResident()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            var pieces = new DivePieces(Depths);
            Assert.That(pieces.Pick(PieceCatalog.BakerId), Is.True);
            Assert.That(stock.Receive(pieces, false), Is.EqualTo(0), "a person is not a piece");
            Assert.That(stock.State.ResidentInStock(Baker), Is.True);
        }

        [Test]
        public void Reload_AnOldSavesBankedPiecesAndBanker_ComeHomeOnce_AndCountAsFound()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            var snap = new TownSnapshot { Stock = new StockSave(), Town = new TownSave(), Dive = new DiveSave { BankedPieces = new List<int> { 100, 201 }, BankedResidents = new List<string> { Baker } } };
            snap.Dive.OnFloorPieces.AddRange(new[] { 101, 200, 300, 301 });
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(snap, null, null, false);
            TownStock.WorldRestore w = stock.RestoreWorld(loaded, Depths);
            Assert.That(stock.State.StockPieces.ToList(), Is.EqualTo(new[] { 100, 201 }));
            Assert.That(stock.State.ResidentInStock(Baker), Is.True);
            Assert.That(w.Pieces.IsFound(100) && w.Pieces.IsFound(201) && w.Pieces.IsFound(PieceCatalog.BakerId), Is.True);
            Assert.That(w.Pieces.IsFound(101), Is.False, "a piece still on its floor stays there");
            // the next capture holds them as stock, not as banked, and a reload of that finds them where they are
            TownSnapshot next = TownSaveRules.CleanSnapshot(stock.CaptureWorld(NewSession(), false, null, Depths, null), null, null, false);
            Assert.That(next.Dive.BankedPieces, Is.Empty);
            Assert.That(next.Dive.BankedResidents, Is.Empty);
            Assert.That(next.Stock.Pieces, Is.EqualTo(new[] { 100, 201 }));
            Assert.That(next.Stock.Residents, Is.EqualTo(new[] { Baker }));
        }

        [Test]
        public void Capture_WritesNoLoot_NoBankedPieces_AndALegacyCheckpointThatIsNotHeld()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            stock.ReceiveFound(100);
            TownSnapshot snap = stock.CaptureWorld(NewSession(), false, null, Depths, null);
            Assert.That(snap.Stock.Loot, Is.Empty);
            Assert.That(snap.Dive.BankedLoot, Is.Empty);
            Assert.That(snap.Dive.BankedPieces, Is.Empty);
            Assert.That(snap.Dive.CheckpointReached, Is.False);
        }

        // Catches: bags dropped on the way through CaptureWorld/RestoreWorld, a client-id fallback bag written, a mid-dive bag written as anything but the failed copy (items in the stash and in
        // the satchel at once, or lost), the live bag changed by the capture.
        [Test]
        public void Reload_BagsComeBack_AMidDiveBagAsTheFailedCopy()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveSession session = NewSession();
            PlayerBag a = session.Bags.For(KeyA.ToString());
            Assert.That(session.TryStart(1, false), Is.True);
            a.Add(101); a.OpenBank(1); a.Pick(0); a.PushOn();   // banked this dive
            a.Add(104); a.AddGold(7);                            // carried
            session.Bags.For("3").AddGold(5);                    // a client-id fallback bag
            session.ClearFloor(1); Assert.That(session.EnterFloor(2), Is.True);
            (TownStock.WorldRestore w, _) = Reload(stock, session);
            PlayerBag back = w.Bags.For(KeyA.ToString());
            Assert.That(back.Gold, Is.EqualTo(3));
            Assert.That(back.Stash, Is.EqualTo(new Dictionary<int, int> { { 101, 1 } }));
            Assert.That(back.Satchel.Waiting.Floor, Is.EqualTo(2)); Assert.That(back.Satchel.Waiting.Items, Is.EqualTo(new[] { 104 }));
            Assert.That(back.Carried, Is.Empty);
            Assert.That(w.Bags.All.Select(kv => kv.Key), Is.EqualTo(new[] { KeyA.ToString() }), "the fallback bag was not saved");
            Assert.That(a.Carried, Is.EqualTo(new[] { 104 }), "capturing leaves the live bag alone"); Assert.That(a.DiveGold, Is.EqualTo(7));
        }

        // Catches: bags written into a world that has none (an empty bag is not worth a section).
        [Test]
        public void Capture_WithAnEmptyBag_WritesAPristineSection()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveSession session = NewSession();
            session.Bags.For(KeyA.ToString());
            Assert.That(BagsSaveRules.IsPristine(stock.CaptureWorld(session, false, null, Depths, null).Bags), Is.True);
        }

        [Test]
        public void RestoreWorld_RestoresTheSavedBags()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            var snap = new TownSnapshot { Bags = new BagsSave() };
            snap.Bags.Players.Add(new PlayerBagSave { Identity = 1, Id = "aaa", Gold = 8 });
            Assert.That(stock.RestoreWorld(snap, Depths).Bags.For(KeyA.ToString()).Gold, Is.EqualTo(8));
            Assert.That(new TownStock(HollowmereLayout.Create()).RestoreWorld(null, Depths).Bags.All, Is.Empty);
        }
    }
}
