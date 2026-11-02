// M6 S4: the pure pieces of town replication and placement mode. TownWire (the town as flat arrays) must round-trip a TownState exactly, through the real NGO serializer (TownNet) too, so a late
// joiner rebuilds the same town from one value; PlacementSteps picks what the next press does; PlacementText words the wishes and refusals.
// Catches: a stock piece, a part slot, a turn or a resident lost on the wire (late joiner sees a different town), a signature that ignores a change (the host would not republish), a bad
// entry on the wire throwing instead of being dropped, the next-step order (frame, parts, baker) or pick-up order (baker, last part, building) wrong.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using Unity.Collections;
using Unity.Netcode;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownWireTests
    {
        const int Frame = 100, Oven = 101, Door = 200, Sign = 201, SpareFrame = 300;
        static readonly TownLayout Hollow = HollowmereLayout.Create();

        static TownState Town(params int[] pieces)
        {
            var s = new TownState(Hollow);
            foreach (int p in pieces) Assert.That(s.AddPiece(p), Is.EqualTo(PlaceResult.Ok));
            return s;
        }

        static T RoundTrip<T>(T value) where T : INetworkSerializable, new()
        {
            using (var w = new FastBufferWriter(2048, Allocator.Temp))
            {
                w.WriteNetworkSerializable(value);
                using (var r = new FastBufferReader(w, Allocator.Temp))
                {
                    r.ReadNetworkSerializable(out T back);
                    return back;
                }
            }
        }

        static TownNet Wire(TownNet net) => RoundTrip(net);

        [Test]
        public void Wire_RoundTripsAHalfBuiltTown_ThroughTheNetworkSerializer()
        {
            TownState s = Town(Frame, Oven, Door, Sign, SpareFrame);
            Assert.That(s.AddResident(TownBuildings.BakerResident), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlacePart("bakery", "oven"), Is.EqualTo(PlaceResult.Ok));
            TownWire wire = TownWire.From(s);

            TownWire back = Wire(new TownNet { Wire = wire }).Wire;
            Assert.That(back.Signature(), Is.EqualTo(wire.Signature()));
            TownState rebuilt = back.ToState(Hollow);
            Assert.That(rebuilt.Conservation(), Is.EqualTo(s.Conservation()));
            PlacedBuilding b = rebuilt.PlacedBuildingOf("bakery");
            Assert.That(b.FoundationId, Is.EqualTo("F02"));
            Assert.That(b.PiecesByPart, Is.EqualTo(s.PlacedBuildingOf("bakery").PiecesByPart));
            Assert.That(rebuilt.InStock(Door) && rebuilt.InStock(Sign) && rebuilt.InStock(SpareFrame), Is.True);
            Assert.That(rebuilt.ResidentInStock("baker"), Is.True);
        }

        [Test]
        public void Wire_RoundTripsACompleteBuildingWithItsResident_AndTheTurn()
        {
            TownState s = Town(Frame, Oven, Door, Sign);
            s.AddResident(TownBuildings.BakerResident);
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.SetTurn("bakery", 1), Is.EqualTo(PlaceResult.Ok).Or.EqualTo(PlaceResult.PathBlocked));
            foreach (string p in new[] { "oven", "door", "sign" }) Assert.That(s.PlacePart("bakery", p), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.Ok));

            TownState rebuilt = Wire(new TownNet { Wire = TownWire.From(s) }).Wire.ToState(Hollow);
            Assert.That(rebuilt.IsComplete("bakery"), Is.True);
            Assert.That(rebuilt.PlacedBuildingOf("bakery").Resident, Is.EqualTo("baker"));
            Assert.That(rebuilt.PlacedBuildingOf("bakery").Turn, Is.EqualTo(s.PlacedBuildingOf("bakery").Turn));
            Assert.That(TownWire.From(rebuilt).Signature(), Is.EqualTo(TownWire.From(s).Signature()));
        }

        [Test]
        public void Wire_EmptyAndNullTowns_RoundTrip_AndEqualityTracksChanges()
        {
            TownNet empty = Wire(default(TownNet));
            Assert.That(empty.Wire.Buildings, Is.Empty);
            Assert.That(empty.Wire.StockPieces, Is.Empty);
            TownState s = Town(Frame);
            TownNet a = new TownNet { Wire = TownWire.From(s) }, same = new TownNet { Wire = TownWire.From(s) };
            Assert.That(a.Equals(same), Is.True);
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(a.Equals(new TownNet { Wire = TownWire.From(s) }), Is.False, "the host must see a placed frame as a change");
        }

        [Test]
        public void Wire_ABadEntryIsDroppedNotThrown()
        {
            var wire = new TownWire
            {
                StockPieces = new[] { Frame, 9999, Frame },
                StockResidents = new[] { "baker", "ghost", null },
                Buildings = new[]
                {
                    new TownWire.Building { BuildingId = "castle", FoundationId = "F02", Parts = new[] { 1 } },
                    new TownWire.Building { BuildingId = "bakery", FoundationId = "nope", Parts = new[] { Frame, -1, -1, -1 } },
                },
            };
            TownState s = null;
            Assert.DoesNotThrow(() => s = wire.ToState(Hollow));
            Assert.That(s.Placed, Is.Empty);
            Assert.That(s.InStock(Frame), Is.True);
            Assert.That(s.ResidentInStock("baker"), Is.True);
        }

        [Test]
        public void Ghost_RoundTrips()
        {
            var g = new GhostNet { Active = true, Building = "bakery", Foundation = "R2_05", Turn = 3 };
            GhostNet back = RoundTrip(g);
            Assert.That(back.Equals(g), Is.True);
            Assert.That(back.Foundation.ToString(), Is.EqualTo("R2_05"));
        }

        // ------------------------------------------------------------------ the next press
        [Test]
        public void Steps_PlaceOrder_FrameThenPartsThenTheBaker_AndPickUpOrderIsTheReverse()
        {
            TownState s = Town(Frame, Oven, Door, Sign);
            s.AddResident(TownBuildings.BakerResident);
            Assert.That(PlacementSteps.Next(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.Frame));
            Assert.That(PlacementSteps.NextPickUp(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.None), "nothing stands yet");
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(PlacementSteps.Next(s, "bakery"), Is.EqualTo(new PlaceStep(PlaceStepKind.Part, "oven")));
            Assert.That(PlacementSteps.NextPickUp(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.Frame));
            s.PlacePart("bakery", "oven"); s.PlacePart("bakery", "door");
            Assert.That(PlacementSteps.Next(s, "bakery"), Is.EqualTo(new PlaceStep(PlaceStepKind.Part, "sign")));
            Assert.That(PlacementSteps.NextPickUp(s, "bakery"), Is.EqualTo(new PlaceStep(PlaceStepKind.Part, "door")));
            s.PlacePart("bakery", "sign");
            Assert.That(PlacementSteps.Next(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.Resident));
            s.PlaceResident("bakery");
            Assert.That(PlacementSteps.Next(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.None));
            Assert.That(PlacementSteps.NextPickUp(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.Resident));
        }

        [Test]
        public void Steps_NoFrameInStock_IsNone_AndFreeLotsSkipTheOccupiedOne()
        {
            TownState s = Town(Oven);
            Assert.That(PlacementSteps.Next(s, "bakery").Kind, Is.EqualTo(PlaceStepKind.None));
            TownState t = Town(Frame);
            BuildingDef def = TownBuildings.Get("bakery");
            t.PlaceFrame("bakery", "F02", 0);
            Foundation f02 = Hollow.Foundation("F02");
            Assert.That(PlacementSteps.NearestFree(t, def, f02.Centre.X, f02.Centre.Z), Is.Not.EqualTo("F02"));
            Assert.That(PlacementSteps.CandidateFoundations(Hollow, def), Does.Contain("F02"));
        }

        [Test]
        public void Placement_PathBlockedFrame_IsRefusedByTheRules_TheHostRunsOnItsOwnState()
        {
            TownState s = Town(Frame);
            Assert.That(s.CanPlaceFrame("bakery", "F02", 2), Is.EqualTo(PlaceResult.PathBlocked));
            Assert.That(s.CanPlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
        }

        // ------------------------------------------------------------------ words
        [Test]
        public void Text_UnmetWishesNameTheNumberAndANudge_MetOnesSayGood()
        {
            BuildingDef def = TownBuildings.Get("bakery");
            WishReport rep = WishEvaluator.Evaluate(Hollow, def, "F02", 0);
            foreach (WishResult r in rep.Results)
            {
                string line = PlacementText.WishLine(r);
                Assert.That(line, Does.StartWith(r.Wish.Hint));
                if (!r.Met) Assert.That(line, Does.Contain(" m").Or.Contain("deg"));
            }
            var near = new WishResult(PlacementWish.NearPoint("DryFountain", 12f, "Near the fountain"), false, 13f, 12f, "m");
            Assert.That(PlacementText.WishLine(near), Is.EqualTo("Near the fountain: 13 m of 12 m, a little closer"));
            var far = new WishResult(PlacementWish.FarFrom("F09", 8f, "Not by the inn"), false, 5f, 8f, "m");
            Assert.That(PlacementText.WishLine(far), Does.Contain("a little further"));
            Assert.That(PlacementText.Refusal((byte)PlaceResult.PathBlocked), Does.Contain("block"));
            Assert.That(PlacementText.Refusal(TownSync.NotBuilder), Does.Contain("building"));
            Assert.That(PlacementText.StockLine(Town(Frame, Oven, SpareFrame)), Is.EqualTo("frame x2, oven"));
        }
    }
}
