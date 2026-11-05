// M6 S3 / plan 3 task 12: the host-held town stock (Core/Town/TownStock): found pieces move into TownState stock, the baker joins when rescued, and the move is idempotent so the
// found set and the town never double count. Pure Core + NUnit.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownStockTests
    {
        static DivePieces Pieces() => new DivePieces(PieceDepths.Assign(12345UL, PieceCatalog.Specs));

        [Test]
        public void Receive_MovesEveryPickedUpPieceToStock_AtOnce()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DivePieces pieces = Pieces();
            pieces.Pick(PieceCatalog.FrameId); pieces.Pick(PieceCatalog.OvenId);
            Assert.That(stock.Receive(pieces, bakerRescued: false), Is.EqualTo(2), "no bank press needed: a piece is safe on pickup");
            pieces.Pick(PieceCatalog.DoorId);
            Assert.That(stock.Receive(pieces, bakerRescued: false), Is.EqualTo(1), "only the new one arrives");
            Assert.That(stock.Receive(pieces, bakerRescued: false), Is.EqualTo(0), "moving again moves nothing twice");
            Assert.That(stock.State.InStock(100), Is.True);
            Assert.That(stock.State.InStock(101), Is.True);
            Assert.That(stock.State.InStock(200), Is.True);
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.False);
        }

        [Test]
        public void Receive_IsIdempotent_AndPicksUpLaterFinds()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DivePieces pieces = Pieces();
            pieces.Pick(100);
            Assert.That(stock.Receive(pieces, false), Is.EqualTo(1));
            Assert.That(stock.Receive(pieces, false), Is.EqualTo(0), "the same piece is not stocked twice");
            pieces.Pick(201);
            Assert.That(stock.Receive(pieces, false), Is.EqualTo(1));
            Assert.That(new List<int>(stock.State.StockPieces), Is.EqualTo(new[] { 100, 201 }));
        }

        [Test]
        public void Receive_AddsTheBakerOnce_WhenRescued()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DivePieces pieces = Pieces();
            stock.Receive(pieces, bakerRescued: true);
            stock.Receive(pieces, bakerRescued: true);
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.True);
            Assert.That(new List<string>(stock.State.StockResidents), Has.Count.EqualTo(1));
        }

        [Test]
        public void Receive_KeepsTheTownConservationSet_StableAcrossRepeats()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DivePieces pieces = Pieces();
            pieces.Pick(100); pieces.Pick(300);
            stock.Receive(pieces, true);
            var before = stock.State.Conservation();
            stock.Receive(pieces, true);
            Assert.That(stock.State.Conservation(), Is.EqualTo(before));
        }
    }
}
