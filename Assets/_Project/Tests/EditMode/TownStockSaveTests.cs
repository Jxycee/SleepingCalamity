// M6 step 0: TownStock as the host's town source: capture/restore round trip through TownSaves, banked loot not double counted on reload, the banked baker survives,
// pieces already home are marked home (the floors never respawn them). Pure Core + NUnit.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownStockSaveTests
    {
        static readonly int[] PerFloor = { 2, 2, 2 };
        const string Baker = "baker";

        static DiveState NewDive() => new DiveState(new DiveHaul(PerFloor));

        // Reloads the way the host does: capture (cleaned and conserved), a cleaned copy of the loaded sections, Restore into a fresh stock.
        static (TownStock stock, DiveState dive) Reload(TownStock stock, DiveState dive, IEnumerable<string> banked = null)
        {
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(stock.Capture(dive, banked), null, null, false);
            var fresh = new TownStock(HollowmereLayout.Create());
            DiveState d = fresh.Restore(loaded, PerFloor, StirTuning.Default);
            return (fresh, d);
        }

        // Catches: stock, loot, cleared floors or the checkpoint lost across a save, or the live objects handed out.
        [Test]
        public void RoundTrip_KeepsStockLootAndProgress()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddPiece(100); dive.Haul.AddPiece(101); dive.Haul.AddLoot(7, 3); dive.Haul.Bank();
            stock.Receive(dive.Haul, false);
            dive.RestoreProgress(3, true);
            (TownStock r, DiveState rd) = Reload(stock, dive);
            Assert.That(r.State.StockPieces.ToList(), Is.EqualTo(new[] { 100, 101 }));
            Assert.That(r.LootCount(7), Is.EqualTo(3));
            Assert.That(rd.ClearedMask, Is.EqualTo(3));
            Assert.That(rd.CheckpointReached, Is.True);
            Assert.That(rd.Haul.IsCollected(100), Is.True, "home pieces count as collected");
        }

        // Catches: the haul's running banked total added to the restored stock again (3 became 6), then again on every reload.
        [Test]
        public void Reload_DoesNotDoubleCountBankedLoot_AcrossReloadsAndNewBanks()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddLoot(7, 3); dive.Haul.Bank();
            stock.Receive(dive.Haul, false);
            (TownStock r, DiveState rd) = Reload(stock, dive);
            Assert.That(r.LootCount(7), Is.EqualTo(3));
            r.Receive(rd.Haul, false);
            Assert.That(r.LootCount(7), Is.EqualTo(3), "receiving the restored haul moves nothing again");
            (TownStock r2, DiveState rd2) = Reload(r, rd);
            Assert.That(r2.LootCount(7), Is.EqualTo(3));
            rd2.Haul.AddLoot(7, 2); rd2.Haul.Bank();
            r2.Receive(rd2.Haul, false);
            Assert.That(r2.LootCount(7), Is.EqualTo(5), "only the new 2 arrive");
        }

        // Catches: spent loot coming back after a reload (the saved stock count was topped up from the haul's running banked total).
        [Test]
        public void Reload_SpentLoot_StaysSpent_AndOnlyUnmovedLootIsSavedAsBanked()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddLoot(7, 3); dive.Haul.Bank();
            stock.Receive(dive.Haul, false);
            TownSnapshot snap = stock.Capture(dive, null);
            Assert.That(snap.Dive.BankedLoot, Is.Empty, "loot already in the town is not saved as banked again");
            Assert.That(snap.Stock.Loot.Single().Count, Is.EqualTo(3));
            snap.Stock.Loot.Single().Count = 1;   // two spent
            var fresh = new TownStock(HollowmereLayout.Create());
            DiveState d = fresh.Restore(TownSaveRules.CleanSnapshot(snap, null, null, false), PerFloor, StirTuning.Default);
            Assert.That(fresh.LootCount(7), Is.EqualTo(1));
            d.Haul.AddLoot(7, 2); d.Haul.Bank();
            fresh.Receive(d.Haul, false);
            Assert.That(fresh.LootCount(7), Is.EqualTo(3), "only the new 2 arrive");
        }

        // Catches: partly moved loot saving its whole running total (the moved part would arrive twice).
        [Test]
        public void Capture_PartlyMovedLoot_SavesOnlyTheUnmovedPart()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddLoot(7, 3); dive.Haul.Bank();
            stock.Receive(dive.Haul, false);
            dive.Haul.AddLoot(7, 2); dive.Haul.Bank();   // 2 more banked, not yet received
            TownSnapshot snap = stock.Capture(dive, null);
            Assert.That(snap.Dive.BankedLoot.Single().Count, Is.EqualTo(2));
            (TownStock r, _) = Reload(stock, dive);
            Assert.That(r.LootCount(7), Is.EqualTo(5));
        }

        // Catches: the dive state built at the first dive start ignoring the current piece counts, or losing progress/checkpoint (the hub prompt reads the checkpoint).
        [Test]
        public void BuildDive_UsesGivenFloorCounts_KeepsProgress_AndLeavesTheStockAlone()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddPiece(100); dive.Haul.Bank();
            stock.Receive(dive.Haul, false);
            dive.RestoreProgress(5, true);
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(stock.Capture(dive, null), null, null, false);
            var fresh = new TownStock(HollowmereLayout.Create());
            fresh.Restore(loaded, PerFloor, StirTuning.Default);
            int before = fresh.State.StockPieces.Count();
            DiveState d = fresh.BuildDive(loaded, PerFloor, StirTuning.Default);
            Assert.That(d.CheckpointReached, Is.True);
            Assert.That(d.ClearedMask, Is.EqualTo(5));
            Assert.That(d.Haul.IsCollected(100), Is.True);
            Assert.That(fresh.State.StockPieces.Count(), Is.EqualTo(before));
        }

        // Catches: loot banked mid-dive and not yet home being lost when the quit turned the dive into a failed one.
        [Test]
        public void Reload_BankedButUnmovedLoot_ComesHomeOnce()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddLoot(7, 3); dive.Haul.Bank();   // banked at the checkpoint, the dive not over: never Received
            (TownStock r, _) = Reload(stock, dive);
            Assert.That(r.LootCount(7), Is.EqualTo(3));
        }

        // Catches: the baker banked at the heart (_heartBanked) vanishing when the game was saved before the dive ended.
        [Test]
        public void Reload_BakerBanked_SurvivesAsAStockResident()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            TownSnapshot snap = stock.Capture(dive, new[] { Baker });
            Assert.That(snap.Dive.BankedResidents, Is.EqualTo(new[] { Baker }));
            (TownStock r, _) = Reload(stock, dive, new[] { Baker });
            Assert.That(r.State.ResidentInStock(Baker), Is.True);
            (TownStock none, _) = Reload(new TownStock(HollowmereLayout.Create()), NewDive());
            Assert.That(none.State.ResidentInStock(Baker), Is.False, "the baker was not rescued");
        }

        // Catches: a piece the town already holds (stock or placed) spawning on its floor again after a reload.
        [Test]
        public void Reload_PiecesAlreadyHome_AreMarkedHome_NotRespawned()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Haul.AddPiece(100); dive.Haul.Bank();
            dive.Haul.AddPiece(201);   // picked up mid-dive: safe at once, so a quit keeps it
            stock.Receive(dive.Haul, false);
            (TownStock r, DiveState rd) = Reload(stock, dive);
            Assert.That(rd.Haul.IsCollected(100), Is.True);
            Assert.That(rd.Haul.PiecesLeft(1), Is.EqualTo(1), "floor 1 holds one piece fewer");
            Assert.That(rd.Haul.IsCollected(201), Is.True, "a piece picked up mid-dive never goes back to its floor");
            Assert.That(r.State.InStock(201), Is.True);
            Assert.That(rd.Haul.AddPiece(100), Is.False, "never picked up twice");
            Assert.That(r.State.InStock(100), Is.True);
        }

        static readonly PlayerKey KeyA = new PlayerKey(IdentityKind.Ugs, "aaa");

        // Catches: bags dropped on the way through TownStock.Capture/Restore, a client-id fallback bag written, a mid-dive bag written as anything but the failed copy (items in the
        // stash and in the satchel at once, or lost), the live bag changed by the capture.
        [Test]
        public void Reload_BagsComeBack_AMidDiveBagAsTheFailedCopy()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            PlayerBag a = dive.Bags.For(KeyA.ToString());
            a.Add(101); a.OpenBank(1); a.Pick(0); a.PushOn();   // banked this dive
            a.Add(104); a.AddGold(7);                            // carried
            dive.Bags.For("3").AddGold(5);                       // a client-id fallback bag
            dive.SetFloor(2);
            (TownStock r, DiveState rd) = Reload(stock, dive);
            PlayerBag back = rd.Bags.For(KeyA.ToString());
            Assert.That(back.Gold, Is.EqualTo(3));
            Assert.That(back.Stash, Is.EqualTo(new Dictionary<int, int> { { 101, 1 } }));
            Assert.That(back.Satchel.Waiting.Floor, Is.EqualTo(2)); Assert.That(back.Satchel.Waiting.Items, Is.EqualTo(new[] { 104 }));
            Assert.That(back.Carried, Is.Empty);
            Assert.That(rd.Bags.All.Select(kv => kv.Key), Is.EqualTo(new[] { KeyA.ToString() }), "the fallback bag was not saved");
            Assert.That(a.Carried, Is.EqualTo(new[] { 104 }), "capturing leaves the live bag alone"); Assert.That(a.DiveGold, Is.EqualTo(7));
        }

        // Catches: bags written into a world that has none, or a snapshot with no dive state inventing a bags section.
        [Test]
        public void Capture_WithNoBagsWithLoot_WritesAPristineSection()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            DiveState dive = NewDive();
            dive.Bags.For(KeyA.ToString());
            TownSnapshot snap = stock.Capture(dive, null);
            Assert.That(BagsSaveRules.IsPristine(snap.Bags), Is.True);
            Assert.That(stock.Capture(null, null).Bags, Is.Null, "no dive state: the source says nothing");
        }

        // Catches: BuildDive (called again at the first dive start) losing the saved bags.
        [Test]
        public void BuildDive_RestoresTheSavedBags()
        {
            var stock = new TownStock(HollowmereLayout.Create());
            var snap = new TownSnapshot { Bags = new BagsSave() };
            snap.Bags.Players.Add(new PlayerBagSave { Identity = 1, Id = "aaa", Gold = 8 });
            DiveState d = stock.BuildDive(snap, PerFloor, StirTuning.Default);
            Assert.That(d.Bags.For(KeyA.ToString()).Gold, Is.EqualTo(8));
            Assert.That(stock.BuildDive(null, PerFloor, StirTuning.Default).Bags.All, Is.Empty);
        }
    }
}
