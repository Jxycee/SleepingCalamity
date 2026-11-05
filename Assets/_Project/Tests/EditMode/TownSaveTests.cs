// M6 S2 / plan 3 task 12: the town's world-save sections (stock, town, residents, dive; Core/Town/Placement/TownSaves.cs): per-entry clean, the conservation pass (missing, duplicate,
// mid-dive quit), the live-state capture and restore, the store's round trip / bad section / bad entry for each section, an S0-era save, the HostWorld provider, and an OLD four-floor
// save (its loot, banked pieces and checkpoint) loading into the new dive.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownSaveTests
    {
        const int Frame = 100, Oven = 101, Door = 200, Sign = 201, SpareFrame = 300, SpareOven = 301;
        const string Baker = "baker";
        static readonly TownLayout Hollow = HollowmereLayout.Create();
        static readonly IReadOnlyDictionary<int, int> Depths = PieceDepths.Assign(12345UL, PieceCatalog.Specs);

        static StockSave Stock(params int[] pieces) { var s = new StockSave(); s.Pieces.AddRange(pieces); return s; }

        // Everything the bakery needs placed and the baker with it; the spare frame and the spare oven were found in the dive and sit in stock (a found piece is safe at once).
        static TownState Live()
        {
            var town = new TownState(Hollow);
            foreach (int p in new[] { Frame, Oven, Door, Sign }) town.AddPiece(p);
            town.AddResident(Baker);
            int turn = Enumerable.Range(0, 4).First(t => town.CanPlaceFrame("bakery", "F02", t) == PlaceResult.Ok);
            Assert.That(town.PlaceFrame("bakery", "F02", turn), Is.EqualTo(PlaceResult.Ok));
            foreach (string part in new[] { "oven", "door", "sign" }) Assert.That(town.PlacePart("bakery", part), Is.EqualTo(PlaceResult.Ok));
            Assert.That(town.PlaceResident("bakery"), Is.EqualTo(PlaceResult.Ok));
            town.AddResident(Baker);   // refused: already placed (a resident exists once)
            town.AddPiece(SpareFrame); town.AddPiece(SpareOven);
            return town;
        }

        static List<string> Places(TownSnapshot s)
        {
            var l = new List<string>();
            foreach (PlacedBuildingSave b in s.Town.Buildings) { foreach (int p in b.Parts) if (p >= 0) l.Add("piece:" + p); if (b.Resident != null) l.Add("resident:" + b.Resident); }
            foreach (int p in s.Stock.Pieces) l.Add("piece:" + p);
            foreach (string r in s.Stock.Residents) l.Add("resident:" + r);
            foreach (int p in s.Dive.BankedPieces) l.Add("piece:" + p);
            foreach (string r in s.Dive.BankedResidents) l.Add("resident:" + r);
            foreach (int p in s.Dive.OnFloorPieces) l.Add("piece:" + p);
            foreach (string r in s.Dive.OnFloorResidents) l.Add("resident:" + r);
            return l;
        }

        static void AssertExactlyOnce(TownSnapshot s)
        {
            List<string> all = Places(s);
            foreach (int id in TownSaveRules.KnownPieceIds()) Assert.That(all.Count(x => x == "piece:" + id), Is.EqualTo(1), "piece " + id);
            foreach (string id in TownSaveRules.KnownResidentIds()) Assert.That(all.Count(x => x == "resident:" + id), Is.EqualTo(1), "resident " + id);
            Assert.That(all.Count, Is.EqualTo(TownSaveRules.KnownPieceIds().Count() + TownSaveRules.KnownResidentIds().Count()));
        }

        // ---------------------------------------------------------------- capture and restore
        // Catches: the live town or haul losing something on the way through the sections (a piece, the turn, which piece a part consumed, the resident, loot, the cleared floors).
        [Test]
        public void CaptureAndRestore_RoundTrip_ThroughTheJsonSections()
        {
            TownState town = Live();
            TownSnapshot snap = TownSaves.Capture(town, null, new ResidentsSave { Residents = { new ResidentSave { Id = Baker, FirstMeetBeat = true } } });
            AssertExactlyOnce(snap);
            Assert.That(snap.Dive.OnFloorPieces, Is.Empty, "a piece picked up mid-dive is safe: it is not written back on its floor");
            Assert.That(snap.Dive.BankedPieces, Is.Empty, "nothing waits banked any more: a found piece is in stock");
            Assert.That(snap.Stock.Pieces, Is.EqualTo(new[] { SpareFrame, SpareOven }));
            Assert.That(snap.Stock.Loot, Is.Empty); Assert.That(snap.Dive.BankedLoot, Is.Empty);

            var save = new WorldSave { Stock = snap.Stock, Town = snap.Town, Residents = snap.Residents, Dive = snap.Dive };
            var back = JsonConvert.DeserializeObject<WorldSave>(JsonConvert.SerializeObject(save));
            WorldSave clean = WorldSaveRules.Clean(back, 1UL, null);

            TownState town2 = TownSaves.ToState(new TownSnapshot { Stock = clean.Stock, Town = clean.Town }, Hollow);
            Assert.That(town2.Conservation(), Is.EqualTo(town.Conservation()));
            PlacedBuilding b = town2.PlacedBuildingOf("bakery");
            Assert.That(b.FoundationId, Is.EqualTo("F02")); Assert.That(b.Turn, Is.EqualTo(town.PlacedBuildingOf("bakery").Turn)); Assert.That(b.Resident, Is.EqualTo(Baker));
            Assert.That(b.PiecesByPart, Is.EqualTo(town.PlacedBuildingOf("bakery").PiecesByPart));
            TownStock.WorldRestore w = new TownStock(Hollow).RestoreWorld(new TownSnapshot { Stock = clean.Stock, Town = clean.Town, Dive = clean.Dive }, Depths);
            foreach (int id in new[] { Frame, Oven, Door, Sign, SpareFrame, SpareOven }) Assert.That(w.Pieces.IsFound(id), Is.True, "piece " + id + " stays found");
            Assert.That(w.Pieces.IsFound(PieceCatalog.BakerId), Is.True, "the baker (placed in the bakery) stays found");
            Assert.That(TownSaves.FirstMeetBeat(clean.Residents, Baker), Is.True);
        }

        // Catches: a restore that re-picks the lowest piece of a part instead of the saved one (the spare frame swapping places with the first).
        [Test]
        public void Restore_KeepsTheVeryPiecesPlaced()
        {
            var snap = new TownSnapshot { Stock = Stock(Frame), Town = new TownSave(), Dive = new DiveSave() };
            snap.Town.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { SpareFrame, -1, -1, -1 } });
            TownState s = TownSaves.ToState(TownSaveRules.CleanSnapshot(snap, null), Hollow);
            Assert.That(s.PlacedBuildingOf("bakery").PiecesByPart[0], Is.EqualTo(SpareFrame));
            Assert.That(s.InStock(Frame), Is.True);
        }

        // Catches: a building the layout refuses (unknown lot, wrong size) taking its pieces and resident with it.
        [Test]
        public void ToState_RefusedBuilding_GoesBackToStock()
        {
            foreach (string lot in new[] { "F99", "F01" })
            {
                var snap = new TownSnapshot { Stock = new StockSave(), Town = new TownSave() };
                snap.Town.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = lot, Parts = new List<int> { Frame, Oven, Door, Sign }, Resident = Baker });
                var repaired = new List<string>();
                TownState s = TownSaves.ToState(snap, Hollow, repaired);
                Assert.That(s.PlacedBuildingOf("bakery"), Is.Null, lot);
                Assert.That(s.StockPieces, Is.EquivalentTo(new[] { Frame, Oven, Door, Sign }), lot);
                Assert.That(s.StockResidents, Is.EquivalentTo(new[] { Baker }), lot);
                Assert.That(repaired.Count, Is.EqualTo(1));
            }
        }

        // ---------------------------------------------------------------- per-entry clean
        // Catches: one bad entry costing the section: unknown or repeated ids, a bad loot row.
        [Test]
        public void CleanStock_DropsOnlyTheBadEntries()
        {
            var s = Stock(Oven, Frame, 999, Frame, -1);
            s.Residents.AddRange(new[] { Baker, "ghost", null, Baker });
            s.Loot.AddRange(new[] { new LootCountSave { ItemId = 7, Count = 3 }, new LootCountSave { ItemId = 7, Count = 2 }, new LootCountSave { ItemId = -1, Count = 1 }, new LootCountSave { ItemId = 8, Count = 0 }, null });
            var dropped = new List<string>();
            StockSave c = TownSaveRules.CleanStock(s, dropped);
            Assert.That(c.Pieces, Is.EqualTo(new[] { Frame, Oven }));
            Assert.That(c.Residents, Is.EqualTo(new[] { Baker }));
            Assert.That(c.Loot.Count, Is.EqualTo(1)); Assert.That(c.Loot[0].Count, Is.EqualTo(5));
            Assert.That(dropped.Count, Is.GreaterThan(0));
            Assert.That(c, Is.Not.SameAs(s));
        }

        // Catches: a bad building entry costing the others, a bad slot costing the building, a resident in an unfinished building.
        [Test]
        public void CleanTown_DropsOnlyTheBadEntries()
        {
            var t = new TownSave();
            t.Buildings.Add(new PlacedBuildingSave { BuildingId = "castle", FoundationId = "F02", Parts = new List<int> { Frame, Oven, Door, Sign } });
            t.Buildings.Add(null);
            t.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Turn = 6, Parts = new List<int> { Frame, 999, Door, Door }, Resident = Baker });
            t.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F03", Parts = new List<int> { SpareFrame, -1, -1, -1 } });   // a second bakery
            var dropped = new List<string>();
            TownSave c = TownSaveRules.CleanTown(t, dropped);
            Assert.That(c.Buildings.Count, Is.EqualTo(1));
            PlacedBuildingSave b = c.Buildings[0];
            Assert.That(b.Turn, Is.EqualTo(2), "turn wraps");
            Assert.That(b.Parts, Is.EqualTo(new[] { Frame, -1, Door, -1 }), "an unknown piece and a piece in the wrong part leave their slots empty");
            Assert.That(b.Resident, Is.Null, "nobody moves into an unfinished building");
            Assert.That(dropped.Count, Is.GreaterThanOrEqualTo(5));
        }

        // Catches: a frame slot holding another building's piece, or the same piece placed twice, passing as a building.
        [Test]
        public void CleanTown_AFrameThatIsNotAFrame_DropsTheBuilding()
        {
            var t = new TownSave();
            t.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { Oven, -1, -1, -1 } });
            Assert.That(TownSaveRules.CleanTown(t, new List<string>()).Buildings, Is.Empty);
            t.Buildings[0] = new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { Frame, -1, -1 } };
            Assert.That(TownSaveRules.CleanTown(t, new List<string>()).Buildings, Is.Empty, "the wrong number of slots");
        }

        // Catches: a repeated resident record, or an unknown one, costing the baker's latched beat.
        [Test]
        public void CleanResidents_KeepsTheLatch_DropsTheStrangers()
        {
            var r = new ResidentsSave { Residents = { new ResidentSave { Id = Baker }, new ResidentSave { Id = "ghost", FirstMeetBeat = true }, new ResidentSave { Id = Baker, FirstMeetBeat = true }, null } };
            var dropped = new List<string>();
            ResidentsSave c = TownSaveRules.CleanResidents(r, dropped);
            Assert.That(c.Residents.Count, Is.EqualTo(1)); Assert.That(c.Residents[0].FirstMeetBeat, Is.True);
            Assert.That(dropped.Count, Is.EqualTo(2));
        }

        // Catches: a cleared mask with bits past floor 10 reaching DiveSession (the mask holds the ten Undercroft floors since plan 3 task 6), or unknown banked ids surviving.
        [Test]
        public void CleanDive_MasksTheFloors_AndDropsUnknownIds()
        {
            var d = new DiveSave { ClearedMask = 0xFFF, BankedPieces = { Frame, 5, Frame }, BankedResidents = { "ghost" }, OnFloorPieces = { 12345 } };
            DiveSave c = TownSaveRules.CleanDive(d, new List<string>());
            Assert.That(c.ClearedMask, Is.EqualTo(0x3FF));
            Assert.That(c.BankedPieces, Is.EqualTo(new[] { Frame })); Assert.That(c.BankedResidents, Is.Empty); Assert.That(c.OnFloorPieces, Is.Empty);
        }

        // ---------------------------------------------------------------- conservation
        // Catches: a piece or the baker that a bad entry cost never coming back (a nothing-is-missable break).
        [Test]
        public void Conservation_Missing_GoesToStock()
        {
            var snap = new TownSnapshot { Stock = Stock(), Town = new TownSave(), Dive = new DiveSave { OnFloorPieces = { Frame } } };
            var repaired = new List<string>();
            TownSnapshot c = TownSaveRules.CleanSnapshot(snap, null, repaired);
            Assert.That(c.Stock.Pieces, Is.EqualTo(new[] { Oven, Door, Sign, SpareFrame, SpareOven }));
            Assert.That(c.Stock.Residents, Is.EqualTo(new[] { Baker }));
            Assert.That(repaired.Count, Is.EqualTo(6));
            AssertExactlyOnce(c);
        }

        // Catches: a thing kept in two places (placed and in stock, banked and on its floor), which would duplicate it once one copy is used.
        [Test]
        public void Conservation_Duplicates_KeepPlacedOverStockOverHaulOverFloor()
        {
            var snap = new TownSnapshot
            {
                Stock = new StockSave { Pieces = { Frame, Oven }, Residents = { Baker } },
                Town = new TownSave { Buildings = { new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { Frame, -1, -1, -1 } } } },
                Dive = new DiveSave { BankedPieces = { Frame, Oven, Door }, OnFloorPieces = { Frame, Oven, Door, Sign, SpareFrame, SpareOven }, OnFloorResidents = { Baker } },
            };
            TownSnapshot c = TownSaveRules.CleanSnapshot(snap, null);
            Assert.That(c.Town.Buildings[0].Parts[0], Is.EqualTo(Frame));
            Assert.That(c.Stock.Pieces, Is.EqualTo(new[] { Oven }), "the placed frame leaves stock; the oven stays in stock, not the haul");
            Assert.That(c.Dive.BankedPieces, Is.EqualTo(new[] { Door }));
            Assert.That(c.Dive.OnFloorPieces, Is.EqualTo(new[] { Sign, SpareFrame, SpareOven }));
            Assert.That(c.Stock.Residents, Is.EqualTo(new[] { Baker })); Assert.That(c.Dive.OnFloorResidents, Is.Empty);
            AssertExactlyOnce(c);
        }

        // Catches: the placed resident also staying in stock (two bakers).
        [Test]
        public void Conservation_APlacedResident_LeavesStockAndHaul()
        {
            var snap = new TownSnapshot
            {
                Stock = new StockSave { Residents = { Baker } },
                Town = new TownSave { Buildings = { new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { Frame, Oven, Door, Sign }, Resident = Baker } } },
                Dive = new DiveSave { BankedResidents = { Baker } },
            };
            TownSnapshot c = TownSaveRules.CleanSnapshot(snap, null);
            Assert.That(c.Stock.Residents, Is.Empty); Assert.That(c.Dive.BankedResidents, Is.Empty);
            AssertExactlyOnce(c);
        }

        // Catches: a save with no dive section (nothing says what is still on the floors) handing every piece out as free stock.
        [Test]
        public void Conservation_WithoutADiveSection_AddsNothing_ButStillRemovesDuplicates()
        {
            var snap = new TownSnapshot
            {
                Stock = Stock(Frame, Frame),
                Town = new TownSave { Buildings = { new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { Frame, -1, -1, -1 } } } },
            };
            TownSnapshot c = TownSaveRules.CleanSnapshot(snap, null);
            Assert.That(c.Stock.Pieces, Is.Empty);
            Assert.That(c.Dive, Is.Null);
        }

        // Catches: an older save (no town sections) gaining sections, or a residents-only save starting conservation.
        [Test]
        public void Conservation_OldSave_StaysUntouched()
        {
            TownSnapshot c = TownSaveRules.CleanSnapshot(new TownSnapshot(), null);
            Assert.That(c.Stock, Is.Null); Assert.That(c.Town, Is.Null); Assert.That(c.Dive, Is.Null);
            c = TownSaveRules.CleanSnapshot(new TownSnapshot { Residents = new ResidentsSave() }, null);
            Assert.That(c.Stock, Is.Null);
        }

        // Catches: a quit mid-dive sending a picked-up piece back to its floor (pieces are safe on pickup), or forgetting the cleared floors (DiveSession.Fail's rule: the dive is over, what was
        // found stays found).
        [Test]
        public void MidDiveQuit_LoadsAsAFailedDive()
        {
            var stock = new TownStock(Hollow);
            var session = new DiveSession(new PartyBags(), new DiveCheckpoint(), new DivePieces(Depths));
            Assert.That(session.TryStart(1, false), Is.True);
            session.ClearFloor(1);
            stock.ReceiveFound(SpareOven);   // picked up mid-dive: safe at once
            TownSnapshot snap = stock.CaptureWorld(session, false, null, Depths, null);
            Assert.That(snap.Dive.InDive, Is.True, "the file says the party was below");
            Assert.That(snap.Dive.BankedPieces, Is.Empty);
            Assert.That(snap.Dive.OnFloorPieces, Has.No.Member(SpareOven));

            TownSnapshot loaded = TownSaveRules.CleanSnapshot(snap, null);
            Assert.That(loaded.Dive.InDive, Is.False);
            Assert.That(loaded.Stock.Pieces, Is.EqualTo(new[] { SpareOven }), "the piece found mid-dive stays found");
            Assert.That(loaded.Dive.ClearedMask, Is.EqualTo(1));
            AssertExactlyOnce(loaded);
        }

        // Catches: any seeded mess of ids ending with a thing in zero or two places, or a duplicate surviving.
        [Test]
        public void Conservation_Fuzz_EveryKnownThingEndsInExactlyOnePlace()
        {
            int[] ids = { 100, 101, 200, 201, 300, 301, 400, 999, -1 };
            string[] people = { Baker, "ghost", null };
            for (int seed = 0; seed < 300; seed++)
            {
                var rng = new System.Random(seed);
                List<int> Pick() { var l = new List<int>(); for (int i = rng.Next(0, 6); i > 0; i--) l.Add(ids[rng.Next(ids.Length)]); return l; }
                List<string> PickR() { var l = new List<string>(); for (int i = rng.Next(0, 3); i > 0; i--) l.Add(people[rng.Next(people.Length)]); return l; }
                var snap = new TownSnapshot
                {
                    Stock = new StockSave { Pieces = Pick(), Residents = PickR() },
                    Town = new TownSave(),
                    Dive = new DiveSave { BankedPieces = Pick(), BankedResidents = PickR(), OnFloorPieces = Pick(), OnFloorResidents = PickR(), InDive = rng.Next(2) == 0 },
                };
                if (rng.Next(2) == 0) snap.Town.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { ids[rng.Next(ids.Length)], ids[rng.Next(ids.Length)], ids[rng.Next(ids.Length)], ids[rng.Next(ids.Length)] }, Resident = people[rng.Next(people.Length)] });
                TownSnapshot c = TownSaveRules.CleanSnapshot(snap, null);
                AssertExactlyOnce(c);
                Assert.That(c.Dive.InDive, Is.False);
                TownState st = TownSaves.ToState(c, Hollow);   // and the live model accepts it without losing anything
                var all = st.Conservation();
                foreach (int p in c.Stock.Pieces) Assert.That(all, Does.Contain("piece:" + p));
            }
        }

        // Catches: the S2 additions to the pure Core pieces: every piece the dive can assign is a known piece (the other half is TownPieceCatalogSpawnTests).
        [Test]
        public void EveryAssignablePiece_IsKnown()
        {
            foreach (PieceSpec spec in PieceCatalog.Specs) if (!PieceCatalog.IsPerson(spec.Id)) Assert.That(TownSaveRules.KnownPieceIds(), Does.Contain(spec.Id));
            Assert.That(PieceCatalog.Specs.Count - 1, Is.EqualTo(TownSaveRules.KnownPieceIds().Count()));
            Assert.That(TownSaveRules.KnownResidentIds(), Is.EqualTo(new[] { Baker }));
        }

        // ---------------------------------------------------------------- the world file
        string _dir, _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sc-town-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, WorldSaveStore.WorldBaseName + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        WorldSaveStore Load()
        {
            var store = new WorldSaveStore(_path, 0xF15F15F1UL);
            store.Load();
            return store;
        }

        static string WithSections(string sections) => WorldSaveStoreTests.GoldenV1.Replace("\"players\"", sections + ", \"players\"");

        // A realistic stock + dive pair for the JSON cases below (nothing placed: the whole bakery is in stock, the baker is on floor 4).
        const string GoodStock = "\"stock\": { \"pieces\": [100, 101], \"residents\": [], \"loot\": [ { \"itemId\": 7, \"count\": 3 } ] }";
        const string GoodDive = "\"dive\": { \"inDive\": false, \"clearedMask\": 3, \"checkpointReached\": true, \"bankedPieces\": [200], \"bankedResidents\": [], \"bankedLoot\": [], \"onFloorPieces\": [201, 300, 301], \"onFloorResidents\": [\"baker\"] }";

        // Catches: any of the four sections not surviving a save and load, or its unknown members (a newer build's) being dropped.
        [Test]
        public void Sections_RoundTrip()
        {
            TownState town = Live();
            TownSnapshot snap = TownSaves.Capture(town, null, new ResidentsSave { Residents = { new ResidentSave { Id = Baker, FirstMeetBeat = true } } });
            snap.Stock.Extra = new Dictionary<string, object> { { "futureField", 3L } };
            snap.Stock.Loot.Add(new LootCountSave { ItemId = 9, Count = 4 });   // a legacy member: a file written by the old build still reads
            snap.Dive.ClearedMask = 3;
            WorldSaveStore store = Load();
            WorldSave w = WorldSaveRules.New(7UL);
            w.Stock = snap.Stock; w.Town = snap.Town; w.Residents = snap.Residents; w.Dive = snap.Dive;
            store.Replace(w);
            Assert.That(store.FlushIfDirty(), Is.True);
            string text = File.ReadAllText(_path);
            foreach (string key in new[] { "\"stock\"", "\"town\"", "\"residents\"", "\"dive\"" }) Assert.That(text, Does.Contain(key));

            WorldSaveStore again = Load();
            Assert.That(again.Rejected, Is.Empty);
            Assert.That(again.Current.Stock.Pieces, Is.EqualTo(snap.Stock.Pieces));
            Assert.That(again.Current.Stock.Loot[0].ItemId, Is.EqualTo(9));
            Assert.That(again.Current.Stock.Extra, Does.ContainKey("futureField"));
            Assert.That(again.Current.Town.Buildings[0].Parts, Is.EqualTo(snap.Town.Buildings[0].Parts));
            Assert.That(again.Current.Town.Buildings[0].Resident, Is.EqualTo(Baker));
            Assert.That(again.Current.Residents.Residents[0].FirstMeetBeat, Is.True);
            Assert.That(again.Current.Dive.BankedPieces, Is.Empty);
            Assert.That(again.Current.Dive.ClearedMask, Is.EqualTo(3));
        }

        // Catches: a damaged section of any of the four costing the players, the seed or the other town sections (a bad section resets only itself).
        [TestCase("stock", "5")]
        [TestCase("town", "\"x\"")]
        [TestCase("residents", "[1]")]
        [TestCase("dive", "7")]
        public void BadSection_KeepsTheRest(string name, string bad)
        {
            // The others are present and good, so the bad one is the only reset. (A bad town leaves the dive's floor list short of nothing: no building was saved.)
            var parts = new Dictionary<string, string>
            {
                { "stock", GoodStock }, { "town", "\"town\": { \"buildings\": [] }" }, { "residents", "\"residents\": { \"residents\": [ { \"id\": \"baker\", \"firstMeetBeat\": true } ] }" }, { "dive", GoodDive },
            };
            parts[name] = "\"" + name + "\": " + bad;
            File.WriteAllText(_path, WithSections(string.Join(", ", parts.Values)));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { name }));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(store.Current.WorldSeed, Is.EqualTo("00000000F15F15F1"));
            Assert.That(store.Current.Stock.Pieces, Does.Contain(Frame), "a reset stock section gets its pieces back from the conservation pass");
            if (name != "dive") Assert.That(store.Current.Dive.ClearedMask, Is.EqualTo(3));
            if (name != "residents") Assert.That(TownSaves.FirstMeetBeat(store.Current.Residents, Baker), Is.True);
            if (name == "dive") Assert.That(store.Current.Dive, Is.Null);
        }

        // Catches: one mistyped or unknown entry in a list costing the section or its good entries.
        [Test]
        public void OneBadEntry_IsDroppedAlone()
        {
            string stock = "\"stock\": { \"pieces\": [100, \"x\", 999, 101], \"residents\": [\"baker\", 5], \"loot\": [ { \"itemId\": 7, \"count\": 3 }, { \"itemId\": \"q\" } ] }";
            string town = "\"town\": { \"buildings\": [ { \"buildingId\": 5 }, { \"buildingId\": \"bakery\", \"foundationId\": \"F02\", \"turn\": 1, \"parts\": [200, -1, -1, -1] }, \"nope\" ] }";
            string dive = "\"dive\": { \"clearedMask\": \"x\", \"checkpointReached\": true, \"bankedPieces\": [201, 1.5], \"onFloorPieces\": [] }";
            File.WriteAllText(_path, WithSections(stock + ", " + town + ", " + dive));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EquivalentTo(new[] { "stock", "town", "dive" }), "named so the original is kept");
            Assert.That(store.Current.Stock.Pieces, Has.Member(Frame).And.Member(Oven).And.Not.Member(999));
            Assert.That(store.Current.Stock.Loot.Single().Count, Is.EqualTo(3));
            Assert.That(store.Current.Town.Buildings, Is.Empty, "the one well-formed building has a door where its frame belongs: dropped by Clean");
            Assert.That(store.Current.Dive.CheckpointReached, Is.False, "the old 4-floor checkpoint never reads as a plan 3 checkpoint");
            Assert.That(store.Current.Dive.ClearedMask, Is.EqualTo(0));
            Assert.That(store.Current.Dive.BankedPieces, Is.EqualTo(new[] { 201 }));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
            AssertExactlyOnce(new TownSnapshot { Stock = store.Current.Stock, Town = store.Current.Town, Dive = store.Current.Dive });
        }

        // Catches: a well-formed building entry in a file being dropped, or the pieces it holds being duplicated into stock.
        [Test]
        public void AGoodBuilding_LoadsAsSaved()
        {
            string town = "\"town\": { \"buildings\": [ { \"buildingId\": \"bakery\", \"foundationId\": \"F02\", \"turn\": 1, \"parts\": [100, 101, -1, -1] } ] }";
            File.WriteAllText(_path, WithSections(town + ", " + GoodDive));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.Current.Town.Buildings[0].Parts, Is.EqualTo(new[] { 100, 101, -1, -1 }));
            Assert.That(store.Current.Stock.Pieces, Is.Empty, "every other piece is already on a floor or in the haul");
            AssertExactlyOnce(new TownSnapshot { Stock = store.Current.Stock, Town = store.Current.Town, Dive = store.Current.Dive });
        }

        // Catches: a mid-dive quit in the FILE not loading as a failed dive.
        [Test]
        public void MidDiveFile_LoadsAsAFailedDive()
        {
            string dive = "\"dive\": { \"inDive\": true, \"clearedMask\": 1, \"checkpointReached\": false, \"bankedPieces\": [100], \"onFloorPieces\": [101, 200, 201, 300, 301], \"onFloorResidents\": [\"baker\"] }";
            File.WriteAllText(_path, WithSections("\"stock\": { }, \"town\": { }, " + dive));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.Empty, "a mid-dive quit is not damage");
            Assert.That(store.Current.Dive.InDive, Is.False);
            Assert.That(store.Current.Dive.BankedPieces, Is.EqualTo(new[] { 100 }));
            Assert.That(store.Current.Dive.ClearedMask, Is.EqualTo(1));
        }

        // Catches: the v1 golden file or a save from before the town sections (S0 added weapons) failing to load, or gaining town sections it never had.
        [Test]
        public void GoldenV1_AndAnS0EraSave_StillLoad_WithNoTownSections()
        {
            File.WriteAllText(_path, WorldSaveStoreTests.GoldenV1);
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.Current.Stock, Is.Null); Assert.That(store.Current.Town, Is.Null); Assert.That(store.Current.Residents, Is.Null); Assert.That(store.Current.Dive, Is.Null);

            File.WriteAllText(_path, WithSections("\"weapons\": { \"weapons\": [], \"heroes\": [ { \"ownerId\": \"aaa\" } ] }"));
            store = Load();
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.Current.Weapons.Heroes.Count, Is.EqualTo(1));
            Assert.That(store.Current.Stock, Is.Null); Assert.That(store.Current.Dive, Is.Null);
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // ---------------------------------------------------------------- HostWorld's provider
        sealed class FakeTown : HostWorld.ITownSource
        {
            public TownSnapshot Next; public int Calls; public TownSnapshot Restored;
            public TownSnapshot CaptureTown() { Calls++; return Next; }
            public bool Adopt;   // true: like a real hub, it rebuilds its state from the loaded sections and captures that afterwards
            public void RestoreTown(TownSnapshot loaded) { Restored = loaded; if (Adopt) Next = loaded; }
        }

        static TownSnapshot Fresh() => TownSaves.Capture(new TownState(Hollow), null, null);

        // Catches: a fresh world writing four sections nobody asked for (old saves must stay as they were), or a change not marking the save.
        [Test]
        public void Host_CaptureWritesOnlyOnChange_AndNeverAliases()
        {
            var world = new HostWorld();
            var save = WorldSaveRules.New(1UL);
            var src = new FakeTown { Next = Fresh() };
            world.RegisterTownSource(src);
            Assert.That(world.CaptureTownInto(save), Is.False, "a pristine town is not written");
            Assert.That(save.Stock, Is.Null); Assert.That(save.Dive, Is.Null);

            TownState town = Live();
            src.Next = TownSaves.Capture(town, null, null);
            Assert.That(world.CaptureTownInto(save), Is.True);
            Assert.That(save.Town.Buildings.Count, Is.EqualTo(1));
            Assert.That(world.CaptureTownInto(save), Is.False, "nothing changed");
            Assert.That(save.Dive, Is.Not.SameAs(world.Town.Dive));
            save.Dive.ClearedMask = 15;   // the file's value is its own: the host's copy is untouched
            Assert.That(world.Town.Dive.ClearedMask, Is.EqualTo(0));

            src.Next = TownSaves.Capture(town, null, new ResidentsSave { Residents = { new ResidentSave { Id = Baker, FirstMeetBeat = true } } });
            Assert.That(world.CaptureTownInto(save), Is.True);
            Assert.That(TownSaves.FirstMeetBeat(save.Residents, Baker), Is.True);
        }

        // The DiveDirector's path: RestoreTown copies the loaded residents (MergeLatches(null, loaded)), the baker latches, CaptureTown hands the section back, the host merges and writes it.
        sealed class DirectorLike : HostWorld.ITownSource
        {
            public ResidentsSave Residents;
            public TownSnapshot CaptureTown() { TownSnapshot f = Fresh(); return new TownSnapshot { Stock = f.Stock, Town = f.Town, Dive = f.Dive, Residents = Residents }; }
            public void RestoreTown(TownSnapshot loaded) { Residents = loaded?.Residents != null ? TownSaveRules.MergeLatches(null, loaded.Residents) : null; }
        }

        // Catches: the director's residents copy dropping the section-level (or a resident's) unknown fields from a newer build, or the first-meet latch not reaching the file.
        [Test]
        public void Residents_SectionAndEntryExtra_SurviveTheDirectorsPath_WithTheFirstMeetLatch()
        {
            var loaded = new ResidentsSave { Extra = new Dictionary<string, object> { { "sectionFuture", 7L } } };
            loaded.Residents.Add(new ResidentSave { Id = TownBuildings.BakerResident, FirstMeetBeat = false, Extra = new Dictionary<string, object> { { "entryFuture", "x" } } });
            var saved = new WorldSave { Residents = loaded };
            var world = new HostWorld();
            world.LoadTown(saved);
            var dir = new DirectorLike();
            world.RegisterTownSource(dir);
            Assert.That(dir.Residents.Extra, Does.ContainKey("sectionFuture"), "RestoreTown keeps the section-level Extra");

            dir.Residents.Residents.Find(r => r.Id == TownBuildings.BakerResident).FirstMeetBeat = true;   // BakerTick's latch
            Assert.That(world.CaptureTownInto(saved), Is.True);
            Assert.That(saved.Residents.Extra, Does.ContainKey("sectionFuture"), "the file keeps the section-level Extra");
            ResidentSave baker = saved.Residents.Residents.Find(r => r.Id == TownBuildings.BakerResident);
            Assert.That(baker.FirstMeetBeat, Is.True, "the latch reached the file");
            Assert.That(baker.Extra, Does.ContainKey("entryFuture"), "the resident's Extra survives");
        }

        // Catches: MergeLatches (live given, previous has Extra) overwriting nothing / dropping previous' unknown fields.
        [Test]
        public void MergeLatches_FillsMissingExtraFromPrevious()
        {
            var prev = new ResidentsSave { Extra = new Dictionary<string, object> { { "a", 1L } } };
            prev.Residents.Add(new ResidentSave { Id = TownBuildings.BakerResident, FirstMeetBeat = true, Extra = new Dictionary<string, object> { { "b", 2L } } });
            var live = new ResidentsSave();
            live.Residents.Add(new ResidentSave { Id = TownBuildings.BakerResident });
            ResidentsSave r = TownSaveRules.MergeLatches(live, prev);
            Assert.That(r.Extra, Does.ContainKey("a"));
            Assert.That(r.Residents[0].FirstMeetBeat, Is.True);
            Assert.That(r.Residents[0].Extra, Does.ContainKey("b"));
        }

        // Catches: a hub that goes away (a scene change) taking the town with it before the next save, or a stale source writing into a new world.
        [Test]
        public void Host_UnregisterKeepsTheTown_LoadTownDropsTheSource()
        {
            var world = new HostWorld();
            TownState town = Live();
            var src = new FakeTown { Next = TownSaves.Capture(town, null, null) };
            world.RegisterTownSource(src);
            world.UnregisterTownSource(src);
            Assert.That(world.Town.Town.Buildings.Count, Is.EqualTo(1));
            var save = WorldSaveRules.New(1UL);
            Assert.That(world.CaptureTownInto(save), Is.True, "the kept town reaches the file");
            int calls = src.Calls;
            world.CaptureTownInto(save);
            Assert.That(src.Calls, Is.EqualTo(calls), "no source any more");

            world.RegisterTownSource(src);
            world.LoadTown(null);
            Assert.That(world.Town.Town, Is.Null);
            Assert.That(world.CaptureTownInto(WorldSaveRules.New(2UL)), Is.False);
            Assert.That(src.Calls, Is.EqualTo(calls), "the dropped source is not asked again");
        }

        // Catches: a hub that registers (still empty) and is captured by a save flush before it has rebuilt its state wiping the loaded town out of the file.
        [Test]
        public void Host_RegisteringASource_RestoresItBeforeItCanBeCaptured()
        {
            TownState town = Live();
            TownSnapshot snap = TownSaves.Capture(town, null, null);
            snap.Dive.ClearedMask = 3;
            var saved = new WorldSave { Stock = snap.Stock, Town = snap.Town, Residents = snap.Residents, Dive = snap.Dive };
            var world = new HostWorld();
            world.LoadTown(saved);
            var hub = new FakeTown { Next = Fresh(), Adopt = true };   // empty until RestoreTown is called
            world.RegisterTownSource(hub);
            Assert.That(hub.Restored.Town.Buildings.Count, Is.EqualTo(1));
            Assert.That(hub.Restored.Dive.ClearedMask, Is.EqualTo(3));
            Assert.That(hub.Restored, Is.Not.SameAs(world.Town), "the provider gets its own copy");
            Assert.That(world.CaptureTownInto(saved), Is.False, "the loaded town is still the town");
            Assert.That(saved.Town.Buildings.Count, Is.EqualTo(1));
            Assert.That(saved.Stock.Pieces, Is.EqualTo(new[] { SpareFrame, SpareOven }));
        }

        // Catches: a loaded mid-dive file reaching the provider still in the dive, or the loaded sections aliasing the file's value.
        [Test]
        public void Host_LoadTown_GivesTheProviderCleanedCopies()
        {
            var saved = new WorldSave
            {
                Stock = Stock(Frame),
                Dive = new DiveSave { InDive = true, BankedPieces = { Oven }, OnFloorPieces = { Door, Sign, SpareFrame, SpareOven }, OnFloorResidents = { Baker } },
            };
            var world = new HostWorld();
            world.LoadTown(saved);
            Assert.That(world.Town.Dive.InDive, Is.False);
            Assert.That(world.Town.Stock, Is.Not.SameAs(saved.Stock));
            Assert.That(world.Town.Stock.Pieces, Is.EqualTo(new[] { Frame }));
            saved.Stock.Pieces.Add(101);
            Assert.That(world.Town.Stock.Pieces, Is.EqualTo(new[] { Frame }));
        }

        // Catches: the first-meet latch rolling back when the source's resident state says false, says nothing (null), or the host never loaded the file's value.
        [Test]
        public void Host_FirstMeetBeat_NeverRollsBack_FalseOrNullFromTheSource()
        {
            TownState town = Live();
            TownSnapshot snap = TownSaves.Capture(town, null, new ResidentsSave { Residents = { new ResidentSave { Id = Baker, FirstMeetBeat = true } } });
            var saved = new WorldSave { Stock = snap.Stock, Town = snap.Town, Residents = snap.Residents, Dive = snap.Dive };

            var world = new HostWorld();
            world.LoadTown(saved);
            var src = new FakeTown { Next = TownSaves.Capture(town, null, new ResidentsSave { Residents = { new ResidentSave { Id = Baker, FirstMeetBeat = false } } }) };
            world.RegisterTownSource(src);
            world.CaptureTownInto(saved);
            Assert.That(TownSaves.FirstMeetBeat(saved.Residents, Baker), Is.True, "source says false");

            src.Next = TownSaves.Capture(town, null, null);
            world.CaptureTownInto(saved);
            Assert.That(TownSaves.FirstMeetBeat(saved.Residents, Baker), Is.True, "source says nothing");
            Assert.That(TownSaves.FirstMeetBeat(world.Town.Residents, Baker), Is.True, "the host's copy keeps it too");

            var fresh = new HostWorld();   // never loaded the file: the file's own latch still wins
            fresh.RegisterTownSource(new FakeTown { Next = TownSaves.Capture(town, null, new ResidentsSave { Residents = { new ResidentSave { Id = Baker } } }) });
            fresh.CaptureTownInto(saved);
            Assert.That(TownSaves.FirstMeetBeat(saved.Residents, Baker), Is.True, "file's latch");
        }

        static readonly PlayerKey First = new PlayerKey(IdentityKind.Ugs, "first"), Second = new PlayerKey(IdentityKind.Ugs, "second");

        static WorldSave OldFourFloorWorld()
        {
            // The shape the 4-floor dive wrote: the bakery placed (frame, oven, door, sign) with the baker, the spare frame banked in the old haul, 3 of loot 7 in the town and 2 more banked, the
            // floor-2 checkpoint reached, all four floors cleared, no piece depths, one stash entry (the second player's) already in the bags section.
            TownState town = new TownState(Hollow);
            foreach (int p in new[] { Frame, Oven, Door, Sign }) town.AddPiece(p);
            town.AddResident(Baker);
            int turn = Enumerable.Range(0, 4).First(t => town.CanPlaceFrame("bakery", "F02", t) == PlaceResult.Ok);
            Assert.That(town.PlaceFrame("bakery", "F02", turn), Is.EqualTo(PlaceResult.Ok));
            foreach (string part in new[] { "oven", "door", "sign" }) Assert.That(town.PlacePart("bakery", part), Is.EqualTo(PlaceResult.Ok));
            Assert.That(town.PlaceResident("bakery"), Is.EqualTo(PlaceResult.Ok));
            TownSnapshot snap = TownSaves.Capture(town, null, null);
            snap.Dive.BankedPieces.Add(SpareFrame);
            snap.Dive.OnFloorPieces.Remove(SpareFrame);
            snap.Stock.Loot.Add(new LootCountSave { ItemId = 7, Count = 3 });
            snap.Dive.BankedLoot.Add(new LootCountSave { ItemId = 7, Count = 2 });
            snap.Dive.BankedLoot.Add(new LootCountSave { ItemId = 9, Count = 1 });
            snap.Dive.ClearedMask = 0b1111; snap.Dive.CheckpointReached = true; snap.Dive.PieceDepths = null; snap.Dive.CheckpointHeld = false;
            var w = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(777UL), Stock = snap.Stock, Town = snap.Town, Residents = snap.Residents, Dive = snap.Dive, Players = new WorldPlayersSave { Kind = 1, Identity = (int)IdentityKind.Ugs } };
            w.Players.Recorded.Add(new WorldPlayerSave { Identity = (int)First.Kind, Id = First.Id, Name = "First" });
            w.Players.Recorded.Add(new WorldPlayerSave { Identity = (int)Second.Kind, Id = Second.Id, Name = "Second" });
            w.Bags = new BagsSave();
            var second = new PlayerBagSave { Identity = (int)Second.Kind, Id = Second.Id, Gold = 40 };
            second.Stash.Add(new LootCountSave { ItemId = 104, Count = 2 });
            w.Bags.Players.Add(second);
            return w;
        }

        // Catches: an old four-floor save losing a piece, the baker, a stash or the town's loot on the way into the new dive, a checkpoint that carries over as held, depths recomputed on every load,
        // or loot that is moved twice (the next capture must no longer carry it).
        [Test]
        public void OldFourFloorSave_LoadsIntoNewDive()
        {
            WorldSave old = OldFourFloorWorld();
            var world = new HostWorld();
            world.LoadTown(old);

            Assert.That(world.Town.Stock.Loot, Is.Empty, "the town's old loot moved");
            Assert.That(world.Town.Dive.BankedLoot, Is.Empty, "the banked old loot moved");
            Assert.That(world.PieceFloors.Count, Is.EqualTo(PieceCatalog.Specs.Count), "depths assigned from the world seed");
            Assert.That(world.PieceFloors, Is.EqualTo(PieceDepths.Assign(777UL, PieceCatalog.Specs)));
            Assert.That(world.Town.Dive.PieceDepths.Count, Is.EqualTo(PieceCatalog.Specs.Count), "and saved, so a later load never recomputes them");
            Assert.That(world.Town.Dive.CheckpointReached, Is.False);
            Assert.That(world.Town.Dive.CheckpointHeld, Is.False);

            var stock = new TownStock(Hollow);
            TownStock.WorldRestore w = stock.RestoreWorld(world.Town, world.PieceFloors);
            foreach (int id in new[] { Frame, Oven, Door, Sign, SpareFrame }) Assert.That(w.Pieces.IsFound(id), Is.True, "piece " + id + " stays found");
            Assert.That(w.Pieces.IsFound(PieceCatalog.BakerId), Is.True, "the baker stays found");
            Assert.That(w.Pieces.IsFound(SpareOven), Is.False, "a piece still on its floor stays there");
            Assert.That(stock.State.InStock(SpareFrame), Is.True, "the old haul's banked piece came home");
            Assert.That(stock.State.PlacedBuildingOf("bakery").Resident, Is.EqualTo(Baker));

            PlayerBag firstBag = w.Bags.For(First.ToString());
            Assert.That(firstBag.Stash, Is.EqualTo(new Dictionary<int, int> { { 7, 5 }, { 9, 1 } }), "old loot is the first recorded player's stash");
            PlayerBag secondBag = w.Bags.For(Second.ToString());
            Assert.That(secondBag.Stash, Is.EqualTo(new Dictionary<int, int> { { 104, 2 } }), "an existing stash is untouched");
            Assert.That(secondBag.Gold, Is.EqualTo(40));
            Assert.That(w.Checkpoint.Held, Is.False, "the old floor-2 checkpoint is not a plan 3 checkpoint");

            var session = new DiveSession(w.Bags, w.Checkpoint, w.Pieces);
            Assert.That(session.TryStart(3, false), Is.False, "without a held checkpoint only floor 1 starts");
            Assert.That(session.TryStart(1, false), Is.True, "the next dive starts on floor 1");
            Assert.That(session.IsCleared(1), Is.False, "the old cleared floors do not carry: fresh floors every dive");

            // the next capture writes none of the legacy members, and a reload of it finds everything where it is
            TownSnapshot next = stock.CaptureWorld(new DiveSession(w.Bags, w.Checkpoint, w.Pieces), false, null, world.PieceFloors, null);
            Assert.That(next.Stock.Loot, Is.Empty); Assert.That(next.Dive.BankedLoot, Is.Empty); Assert.That(next.Dive.BankedPieces, Is.Empty);
            Assert.That(next.Dive.ClearedMask, Is.EqualTo(0)); Assert.That(next.Dive.CheckpointReached, Is.False);
            Assert.That(next.Bags.Players.Single(p => p.Id == First.Id).Stash.Select(l => l.ItemId + ":" + l.Count), Is.EqualTo(new[] { "7:5", "9:1" }));
        }

        // Catches: the same old loot arriving twice when the world is loaded again after a save (idempotent), and loot with no recorded player being thrown away silently.
        [Test]
        public void MigrateLegacyLoot_IsIdempotent_AndKeepsTheLootWhenNobodyIsRecorded()
        {
            WorldSave old = OldFourFloorWorld();
            var world = new HostWorld();
            world.LoadTown(old);
            world.CaptureTownInto(old);
            var again = new HostWorld();
            again.LoadTown(old);
            Assert.That(TownSnapshotStash(again.Town, First), Is.EqualTo(new[] { "7:5", "9:1" }), "loading the saved result moves nothing again");

            WorldSave nobody = OldFourFloorWorld();
            nobody.Players.Recorded.Clear();
            var w2 = new HostWorld();
            w2.LoadTown(nobody);
            Assert.That(w2.Town.Stock.Loot.Single().Count, Is.EqualTo(3), "no recorded player: the loot stays in the file for a later load");
        }

        static string[] TownSnapshotStash(TownSnapshot snap, PlayerKey key) =>
            snap.Bags.Players.Single(p => p.Id == key.Id).Stash.Select(l => l.ItemId + ":" + l.Count).ToArray();

        // Catches: a legacy item id the bag stash cannot hold (0 or negative) or a damaged count poisoning the stash, and a stash entry that already exists being replaced instead of added to.
        [Test]
        public void MigrateLegacyLoot_AddsToAnExistingStash_AndSkipsBadEntries()
        {
            var snap = new TownSnapshot { Stock = new StockSave(), Dive = new DiveSave(), Bags = new BagsSave() };
            var entry = new PlayerBagSave { Identity = (int)First.Kind, Id = First.Id };
            entry.Stash.Add(new LootCountSave { ItemId = 7, Count = 4 });
            snap.Bags.Players.Add(entry);
            snap.Stock.Loot.Add(new LootCountSave { ItemId = 7, Count = 3 });
            snap.Stock.Loot.Add(new LootCountSave { ItemId = 0, Count = 3 });
            snap.Dive.BankedLoot.Add(new LootCountSave { ItemId = 7, Count = 2 });
            Assert.That(TownSaves.MigrateLegacyLoot(snap, First), Is.EqualTo(5));
            Assert.That(entry.Stash.Single().Count, Is.EqualTo(9));
            Assert.That(snap.Stock.Loot, Is.Empty); Assert.That(snap.Dive.BankedLoot, Is.Empty);
            Assert.That(TownSaves.MigrateLegacyLoot(snap, First), Is.EqualTo(0));
        }

        // Catches: a restored world that treats stock and placed pieces as uncollected, so the cellar spawns them again after a load.
        [Test]
        public void RestoreWorld_EveryKnownPieceNotOnItsFloor_IsFound()
        {
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(TownSaves.Capture(Live(), null, null), null);
            TownStock.WorldRestore w = new TownStock(Hollow).RestoreWorld(loaded, Depths);
            foreach (int id in TownSaveRules.KnownPieceIds())
                Assert.That(w.Pieces.IsFound(id), Is.EqualTo(!loaded.Dive.OnFloorPieces.Contains(id)), "piece " + id);
            Assert.That(w.Pieces.IsFound(SpareOven), Is.True, "the piece picked up mid-dive is safe: it never goes back to its floor");
        }

        // Catches: a part-built building with a resident losing the resident when the retry without them succeeds.
        [Test]
        public void ToState_RetryWithoutResident_PutsTheResidentBackInStock()
        {
            var snap = new TownSnapshot { Stock = new StockSave(), Town = new TownSave() };
            snap.Town.Buildings.Add(new PlacedBuildingSave { BuildingId = "bakery", FoundationId = "F02", Parts = new List<int> { Frame, -1, -1, -1 }, Resident = Baker });
            TownState s = TownSaves.ToState(snap, Hollow);
            Assert.That(s.PlacedBuildingOf("bakery"), Is.Not.Null);
            Assert.That(s.PlacedBuildingOf("bakery").Resident, Is.Null);
            Assert.That(s.StockResidents, Is.EquivalentTo(new[] { Baker }));
        }

        // Catches: pieces or residents added in a later build handed out free from stock because an older dive section never listed them.
        [Test]
        public void Reconcile_IdsTheSaveNeverKnew_GoToTheirFloors_NotStock()
        {
            var dive = new DiveSave { KnownPieces = new List<int>(), KnownResidents = new List<string>() };   // knew nothing: everything is "new content"
            TownSnapshot c = TownSaveRules.CleanSnapshot(new TownSnapshot { Stock = new StockSave(), Town = new TownSave(), Dive = dive }, null);
            Assert.That(c.Stock.Pieces, Is.Empty);
            Assert.That(c.Stock.Residents, Is.Empty);
            AssertExactlyOnce(c);
            Assert.That(c.Dive.OnFloorPieces.Count, Is.EqualTo(TownSaveRules.KnownPieceIds().Count()));
            Assert.That(c.Dive.OnFloorResidents, Is.EquivalentTo(TownSaveRules.KnownResidentIds()));

            // only one id unknown to the save: that one goes to its floor, the rest of the missing ones still go to stock
            var some = new DiveSave { KnownPieces = TownSaveRules.KnownPieceIds().Where(i => i != SpareOven).ToList(), KnownResidents = TownSaveRules.KnownResidentIds().ToList() };
            c = TownSaveRules.CleanSnapshot(new TownSnapshot { Stock = new StockSave(), Town = new TownSave(), Dive = some }, null);
            Assert.That(c.Dive.OnFloorPieces, Is.EqualTo(new[] { SpareOven }));
            Assert.That(c.Stock.Pieces.Contains(SpareOven), Is.False);
            Assert.That(c.Stock.Pieces.Count, Is.EqualTo(TownSaveRules.KnownPieceIds().Count() - 1));
            AssertExactlyOnce(c);

            // an older save (no known lists) treats every current id as known: unchanged behaviour, missing goes to stock
            c = TownSaveRules.CleanSnapshot(new TownSnapshot { Stock = new StockSave(), Town = new TownSave(), Dive = new DiveSave() }, null);
            Assert.That(c.Stock.Pieces.Count, Is.EqualTo(TownSaveRules.KnownPieceIds().Count()));
            Assert.That(c.Dive.OnFloorPieces, Is.Empty);

            // a capture records what it knew
            TownSnapshot cap = TownSaves.Capture(Live(), null, null);
            Assert.That(cap.Dive.KnownPieces, Is.EquivalentTo(TownSaveRules.KnownPieceIds()));
            Assert.That(cap.Dive.KnownResidents, Is.EquivalentTo(TownSaveRules.KnownResidentIds()));
        }
    }
}
