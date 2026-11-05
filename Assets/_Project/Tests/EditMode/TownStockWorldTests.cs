// Plan 3 task 7: TownStock as the new dive's world source. A found piece or person goes straight into stock (ReceiveFound); CaptureWorld writes the stock, town, dive and bags sections from a live
// DiveSession; RestoreWorld rebuilds the bags, the checkpoint and the found pieces from them. Pure Core + NUnit.
// Catches: a found piece lost across a save, a checkpoint that rolls back (MergeLive keeping the saved one over the live one), the floor of a piece changing, an old save's banked haul not coming home.
// Does not catch: the host's file I/O (WorldSaveStore tests) or the director's wiring (PlayMode).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownStockWorldTests
    {
        static readonly IReadOnlyDictionary<int, int> Depths = PieceDepths.Assign(12345UL, PieceCatalog.Specs);

        static DiveSession NewSession(out TownStock stock, out DivePieces pieces)
        {
            stock = new TownStock(HollowmereLayout.Create());
            pieces = new DivePieces(Depths);
            return new DiveSession(new PartyBags(), new DiveCheckpoint(), pieces);
        }

        // The way the host reloads: capture (cleaned and conserved), a cleaned copy of the loaded sections, RestoreWorld into a fresh stock.
        static (TownStock.WorldRestore world, TownStock stock) Reload(TownStock stock, DiveSession session, string extraOwner = null, bool boss = false)
        {
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(stock.CaptureWorld(session, boss, extraOwner, Depths, null), null, null, false);
            var fresh = new TownStock(HollowmereLayout.Create());
            return (fresh.RestoreWorld(loaded, Depths), fresh);
        }

        [Test]
        public void ReceiveFound_PieceToStock_PersonAsResident_NeverTwice()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            Assert.That(stock.ReceiveFound(100), Is.True);
            Assert.That(stock.ReceiveFound(100), Is.False, "a piece exists once");
            Assert.That(stock.State.InStock(100), Is.True);
            Assert.That(stock.ReceiveFound(PieceCatalog.BakerId), Is.True);
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.True);
            Assert.That(stock.Holds(100) && stock.Holds(PieceCatalog.BakerId) && !stock.Holds(200), Is.True);
        }

        [Test]
        public void RoundTrip_KeepsFoundPiecesAndPeople_ThePiecesNotFoundStayOnTheirFloors()
        {
            DiveSession session = NewSession(out TownStock stock, out DivePieces pieces);
            Assert.That(pieces.Pick(100) && pieces.Pick(PieceCatalog.BakerId), Is.True);
            stock.ReceiveFound(100); stock.ReceiveFound(PieceCatalog.BakerId);
            (TownStock.WorldRestore w, TownStock fresh) = Reload(stock, session);
            Assert.That(w.Pieces.IsFound(100), Is.True);
            Assert.That(w.Pieces.IsFound(PieceCatalog.BakerId), Is.True, "the baker comes home with the party");
            Assert.That(w.Pieces.IsFound(101), Is.False);
            Assert.That(w.Pieces.IsFound(300), Is.False);
            Assert.That(fresh.State.InStock(100), Is.True);
            Assert.That(w.Pieces.DepthOf(200), Is.EqualTo(Depths[200]), "a piece keeps its floor");
        }

        [Test]
        public void RoundTrip_KeepsTheCheckpointBossShortcutAndExtraSlotOwner()
        {
            DiveSession session = NewSession(out TownStock stock, out _);
            session.Checkpoint.Restore(true);
            string owner = new PlayerKey(IdentityKind.Local, "abc").ToString();
            (TownStock.WorldRestore w, _) = Reload(stock, session, owner, boss: true);
            Assert.That(w.Checkpoint.Held, Is.True);
            Assert.That(w.BossBeaten, Is.True);
            Assert.That(w.ExtraSlotOwner, Is.EqualTo(owner));
        }

        [Test]
        public void SpentCheckpoint_LiveValueWinsOverTheSavedOne_WhenMerged()
        {
            DiveSession session = NewSession(out TownStock stock, out _);
            var saved = new TownSnapshot { Dive = new DiveSave { CheckpointHeld = true, ExtraSlotOwner = new PlayerKey(IdentityKind.Local, "old").ToString() } };
            // The party spent the checkpoint on a dive: the live capture says not held, and that must not roll back to the file's value.
            TownSnapshot merged = TownSaveRules.MergeLive(stock.CaptureWorld(session, false, null, Depths, null), saved);
            Assert.That(merged.Dive.CheckpointHeld, Is.False);
            Assert.That(merged.Dive.ExtraSlotOwner, Is.Null);
        }

        [Test]
        public void Capture_InDive_OnlyWhileTheSessionIsBelow_AndMasksClearedFloors()
        {
            DiveSession session = NewSession(out TownStock stock, out _);
            Assert.That(stock.CaptureWorld(session, false, null, Depths, null).Dive.InDive, Is.False);
            Assert.That(session.TryStart(1, false), Is.True);
            session.ClearFloor(1);
            TownSnapshot below = stock.CaptureWorld(session, false, null, Depths, null);
            Assert.That(below.Dive.InDive, Is.True);
            Assert.That(below.Dive.ClearedMask, Is.EqualTo(1));
            session.Fail();
            Assert.That(stock.CaptureWorld(session, false, null, Depths, null).Dive.InDive, Is.False, "a failed dive is over: nothing to count again on load");
        }

        [Test]
        public void Capture_WritesThePieceDepths()
        {
            DiveSession session = NewSession(out TownStock stock, out _);
            TownSnapshot snap = stock.CaptureWorld(session, false, null, Depths, null);
            Assert.That(snap.Dive.PieceDepths.Count, Is.EqualTo(Depths.Count));
            foreach (PieceDepthSave d in snap.Dive.PieceDepths) Assert.That(Depths[d.Id], Is.EqualTo(d.Floor));
        }

        [Test]
        public void Restore_AnOldSavesBankedHaul_ComesHomeAndCountsAsFound()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            var snap = new TownSnapshot { Dive = new DiveSave { BankedPieces = new List<int> { 100 }, BankedResidents = new List<string> { TownBuildings.BakerResident } } };
            TownStock.WorldRestore w = stock.RestoreWorld(snap, Depths);
            Assert.That(stock.State.InStock(100), Is.True);
            Assert.That(w.Pieces.IsFound(100), Is.True);
            Assert.That(w.Pieces.IsFound(PieceCatalog.BakerId), Is.True);
        }

        [Test]
        public void BagsOutliveTheSession_HomeGoldAndWaitingSatchelRoundTrip()
        {
            DiveSession session = NewSession(out TownStock stock, out _);
            string id = new PlayerKey(IdentityKind.Local, "abc").ToString();
            PlayerBag bag = session.Bags.For(id);
            bag.AddGold(7);
            Assert.That(session.TryStart(1, false), Is.True);
            Assert.That(bag.Add(CellarLoot.Items[0].Id), Is.True);
            session.ClearFloor(1); session.EnterFloor(2);
            session.Fail();   // the carried item waits in a satchel at depth 2
            (TownStock.WorldRestore w, _) = Reload(stock, session);
            Assert.That(w.Bags.TryGet(id, out PlayerBag back), Is.True);
            Assert.That(back.Satchel.Waiting, Is.Not.Null);
            Assert.That(back.Satchel.Waiting.Floor, Is.EqualTo(2));
            Assert.That(back.Gold, Is.GreaterThan(0), "the gold the dive kept on failing is home gold");
        }
    }
}
