// M6 S3: the host-held town stock (Core/Town/TownStock): the banked haul moves into TownState stock, the baker joins when the heart was banked, loot is counted once,
// and the move is idempotent so the haul and the town never double count. Pure Core + NUnit.
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownStockTests
    {
        static DiveHaul Haul() => new DiveHaul(new[] { 2, 2, 2 });

        [Test]
        public void Receive_MovesEveryPickedUpPieceToStock_AtOnce()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveHaul haul = Haul();
            haul.AddPiece(DiveHaul.PieceId(1, 0)); haul.AddPiece(DiveHaul.PieceId(1, 1));
            Assert.That(stock.Receive(haul, bakerRescued: false), Is.EqualTo(2), "no bank press needed: a piece is safe on pickup");
            haul.AddPiece(DiveHaul.PieceId(2, 0));
            Assert.That(stock.Receive(haul, bakerRescued: false), Is.EqualTo(1), "only the new one arrives");
            haul.FailDive();
            Assert.That(stock.Receive(haul, bakerRescued: false), Is.EqualTo(0), "a failed dive keeps the pieces and moves nothing twice");
            Assert.That(stock.State.InStock(100), Is.True);
            Assert.That(stock.State.InStock(101), Is.True);
            Assert.That(stock.State.InStock(200), Is.True);
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.False);
        }

        [Test]
        public void Receive_IsIdempotent_AndPicksUpLaterBanks()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveHaul haul = Haul();
            haul.AddPiece(100); haul.Bank();
            Assert.That(stock.Receive(haul, false), Is.EqualTo(1));
            Assert.That(stock.Receive(haul, false), Is.EqualTo(0), "the same banked piece is not stocked twice");
            haul.AddPiece(201); haul.Bank();
            Assert.That(stock.Receive(haul, false), Is.EqualTo(1));
            Assert.That(new System.Collections.Generic.List<int>(stock.State.StockPieces), Is.EqualTo(new[] { 100, 201 }));
        }

        [Test]
        public void Receive_AddsTheBakerOnce_WhenTheHeartWasBanked()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveHaul haul = Haul();
            stock.Receive(haul, bakerRescued: true);
            stock.Receive(haul, bakerRescued: true);
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.True);
            Assert.That(new System.Collections.Generic.List<string>(stock.State.StockResidents), Has.Count.EqualTo(1));
        }

        [Test]
        public void Receive_CountsBankedLootOnceAcrossDives()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveHaul haul = Haul();
            haul.AddLoot(7, 3); haul.AddLoot(9, 1); haul.Bank();
            haul.AddLoot(7, 5);   // unbanked
            stock.Receive(haul, false);
            Assert.That(stock.LootCount(7), Is.EqualTo(3));
            Assert.That(stock.LootCount(9), Is.EqualTo(1));
            stock.Receive(haul, false);
            Assert.That(stock.LootCount(7), Is.EqualTo(3), "a second move of the same bank adds nothing");
            haul.Bank();   // the 5 are banked now (the haul keeps its running totals: 8)
            stock.Receive(haul, false);
            Assert.That(stock.LootCount(7), Is.EqualTo(8));
            Assert.That(stock.TotalLoot, Is.EqualTo(9));
        }

        [Test]
        public void Receive_KeepsTheTownConservationSet_StableAcrossRepeats()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveHaul haul = Haul();
            haul.AddPiece(100); haul.AddPiece(300); haul.Bank();
            stock.Receive(haul, true);
            var before = stock.State.Conservation();
            stock.Receive(haul, true);
            Assert.That(stock.State.Conservation(), Is.EqualTo(before));
        }
    }
}
