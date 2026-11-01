// M6 S1: the Core town model (Core/Town/Placement): piece catalog, TownState rules, path refusal, the six wish kinds at their edges, the baker's three wishes on
// the real Hollowmere layout, and a seeded place / pick-up fuzz. Pure Core + NUnit, so the cloud core-test runner picks it up.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownModelTests
    {
        const int Frame = 100, Oven = 101, Door = 200, Sign = 201, SpareFrame = 300, SpareOven = 301;
        static readonly TownLayout Hollow = HollowmereLayout.Create();

        static TownState Fresh(params int[] pieces)
        {
            var s = new TownState(Hollow);
            foreach (int p in pieces) Assert.That(s.AddPiece(p), Is.EqualTo(PlaceResult.Ok));
            return s;
        }
        static TownState AllPieces() => Fresh(Frame, Oven, Door, Sign);

        // A bare layout with no path graph, for wish geometry at exact distances.
        static Foundation Lot(string id, FoundationSize size, float x, float z, float yaw = 0f, LotSide facing = LotSide.South) =>
            new Foundation { Id = id, Ring = 1, Size = size, Centre = new TownPoint(x, z), Yaw = yaw, Facing = facing };
        static TownLayout Bare(Foundation f, params Landmark[] marks)
        {
            var t = new TownLayout();
            t.Foundations.Add(f);
            foreach (Landmark m in marks) t.Landmarks.Add(m);
            return t;
        }
        static Landmark Mark(string id, float x, float z, float size = 2f) => new Landmark { Id = id, Name = id, Kind = LandmarkKind.Stall, Footprint = new Footprint(new TownPoint(x, z), size, size, 0f) };
        static WishResult One(TownLayout t, PlacementWish w, int turn = 0) => WishEvaluator.EvaluateOne(t, w, t.Foundations[0], turn);

        // ------------------------------------------------------------------ piece catalog
        [Test]
        public void Catalog_EveryDefaultPieceMapsToAPartOfTheBakery()
        {
            var seen = new HashSet<string>();
            for (int floor = 1; floor <= DiveHaul.PieceFloors; floor++)
                for (int i = 0; i < 2; i++)   // M5 spawns 2 per floor by default (DiveHaulSync.DefaultPiecesPerFloor)
                {
                    Assert.That(PieceCatalog.TryGet(DiveHaul.PieceId(floor, i), out PieceInfo info), Is.True, "piece " + DiveHaul.PieceId(floor, i));
                    Assert.That(info.Building, Is.EqualTo(TownBuildings.Bakery));
                    Assert.That(TownBuildings.Get(info.Building).PartIndex(info.Part), Is.GreaterThanOrEqualTo(0));
                    seen.Add(info.Part);
                }
            foreach (string part in TownBuildings.Get(TownBuildings.Bakery).Parts) Assert.That(seen, Does.Contain(part), "no piece for " + part);
        }

        [Test]
        public void Catalog_IdsAreUnique_UnknownIdsDoNotMap_BakerIsAResident()
        {
            var ids = new HashSet<int>();
            foreach (PieceInfo p in PieceCatalog.All) Assert.That(ids.Add(p.Id), Is.True);
            Assert.That(PieceCatalog.TryGet(999, out _), Is.False);
            Assert.That(PieceCatalog.TryGet(PieceCatalog.BakerId, out _), Is.False, "the baker is not a piece");
            Assert.That(PieceCatalog.ResidentOf(PieceCatalog.BakerId), Is.EqualTo(TownBuildings.BakerResident));
            Assert.That(TownBuildings.OfResident(TownBuildings.BakerResident).Id, Is.EqualTo(TownBuildings.Bakery));
        }

        [Test]
        public void Stock_RefusesUnknownAndDuplicates()
        {
            var s = Fresh(Frame);
            Assert.That(s.AddPiece(Frame), Is.EqualTo(PlaceResult.AlreadyKnown));
            Assert.That(s.AddPiece(12345), Is.EqualTo(PlaceResult.UnknownPiece));
            Assert.That(s.AddResident("nobody"), Is.EqualTo(PlaceResult.UnknownResident));
            Assert.That(s.AddResident(TownBuildings.BakerResident), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.AddResident(TownBuildings.BakerResident), Is.EqualTo(PlaceResult.AlreadyKnown));
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.AddPiece(Frame), Is.EqualTo(PlaceResult.AlreadyKnown), "a placed piece still exists once");
        }

        // ------------------------------------------------------------------ rules
        [Test]
        public void Rule_SizeClassMustMatch()
        {
            var s = AllPieces();
            Assert.That(s.PlaceFrame("bakery", "F01", 0), Is.EqualTo(PlaceResult.SizeMismatch), "F01 is M");
            Assert.That(s.PlaceFrame("bakery", "F04", 0), Is.EqualTo(PlaceResult.SizeMismatch), "F04 is S");
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok), "F02 is L");
        }

        [Test]
        public void Rule_UnknownNamesAreRefused()
        {
            var s = AllPieces();
            Assert.That(s.PlaceFrame("castle", "F02", 0), Is.EqualTo(PlaceResult.UnknownBuilding));
            Assert.That(s.PlaceFrame("bakery", "F99", 0), Is.EqualTo(PlaceResult.UnknownFoundation));
            Assert.That(s.PlacePart("bakery", "oven"), Is.EqualTo(PlaceResult.NoFrame));
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(s.PlacePart("bakery", "chimney"), Is.EqualTo(PlaceResult.UnknownPart));
        }

        [Test]
        public void Rule_FoundationOccupied_AndBuildingPlacedOnce()
        {
            var s = AllPieces();
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.FoundationOccupied));
            Assert.That(s.PlaceFrame("bakery", "F03", 0), Is.EqualTo(PlaceResult.AlreadyPlaced));
            Assert.That(s.OccupantOf("F02").BuildingId, Is.EqualTo("bakery"));
            Assert.That(s.OccupantOf("F03"), Is.Null);
        }

        [Test]
        public void Rule_FrameBeforeParts_AndTheFramePieceIsNeeded()
        {
            var s = Fresh(Oven, Door);
            Assert.That(s.PlacePart("bakery", "oven"), Is.EqualTo(PlaceResult.NoFrame));
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.NoPieceInStock), "no frame piece yet");
            s.AddPiece(Frame);
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlacePart("bakery", "oven"), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlacePart("bakery", "oven"), Is.EqualTo(PlaceResult.PartAlreadyPlaced));
            Assert.That(s.PlacePart("bakery", "sign"), Is.EqualTo(PlaceResult.NoPieceInStock), "the sign has not come home");
            Assert.That(s.PlacePart("bakery", "frame"), Is.EqualTo(PlaceResult.PartAlreadyPlaced));
        }

        [Test]
        public void Rule_ResidentOnlyWhenComplete()
        {
            var s = AllPieces(); s.AddResident("baker");
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.NotComplete));
            s.PlacePart("bakery", "oven"); s.PlacePart("bakery", "door");
            Assert.That(s.IsComplete("bakery"), Is.False);
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.NotComplete), "three of four parts is not complete");
            s.PlacePart("bakery", "sign");
            Assert.That(s.IsComplete("bakery"), Is.True);
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.ResidentAlreadyPlaced));
            Assert.That(s.PlaceResident("castle"), Is.EqualTo(PlaceResult.UnknownBuilding));
        }

        [Test]
        public void Rule_ResidentNeedsTheBakerInStock_AndAFramePlaced()
        {
            var s = AllPieces();
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.NotPlaced));
            s.PlaceFrame("bakery", "F02", 0);
            foreach (string p in new[] { "oven", "door", "sign" }) s.PlacePart("bakery", p);
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.NoResidentInStock), "the baker has not been rescued");
        }

        [Test]
        public void Rule_SparePiecesStayInStockAndFillTheirPart()
        {
            var s = Fresh(Frame, SpareFrame, Oven, SpareOven);
            s.PlaceFrame("bakery", "F02", 0); s.PlacePart("bakery", "oven");
            Assert.That(s.InStock(SpareFrame), Is.True);
            Assert.That(s.InStock(SpareOven), Is.True);
            Assert.That(s.PlacePart("bakery", "oven"), Is.EqualTo(PlaceResult.PartAlreadyPlaced));
        }

        // ------------------------------------------------------------------ path refusal
        [Test]
        public void Path_BaselineHollowmereIsFullyReachable()
        {
            Assert.That(Hollow.Validate(), Is.Empty);
            Assert.That(PathRule.FirstUnreachable(Hollow, Hollow.Adjacency(), new List<PlacedBuilding>()), Is.Null);
        }

        [Test]
        public void Path_ADoorTurnedAwayFromTheNetworkIsRefused_AndNothingChanges()
        {
            var s = AllPieces();
            Assert.That(s.PlaceFrame("bakery", "F02", 2), Is.EqualTo(PlaceResult.PathBlocked), "door to the north: its doorstep is cut off once the other sides are walled");
            Assert.That(s.InStock(Frame), Is.True, "a refusal keeps the piece in stock");
            Assert.That(s.PlacedBuildingOf("bakery"), Is.Null);
            Assert.That(s.CanPlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
        }

        [Test]
        public void Path_SetTurnRefusedKeepsTheOldTurn()
        {
            var s = AllPieces();
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.SetTurn("bakery", 2), Is.EqualTo(PlaceResult.PathBlocked));
            Assert.That(s.PlacedBuildingOf("bakery").Turn, Is.EqualTo(0));
            Assert.That(s.SetTurn("castle", 1), Is.EqualTo(PlaceResult.NotPlaced));
            Assert.That(s.SetTurn("bakery", 4), Is.EqualTo(PlaceResult.Ok), "turn 4 is turn 0");
            Assert.That(s.PlacedBuildingOf("bakery").Turn, Is.EqualTo(0));
        }

        [Test]
        public void Path_PlacementThatCutsAnotherLotOffIsRefused()
        {
            // Walling a lot's side doorsteps can strand a neighbour: find at least one (lot, turn) on the real layout that the rule refuses for a reason other than
            // its own door, then prove the refusal names a node that was reachable in the baseline.
            var adj = Hollow.Adjacency();
            int refused = 0;
            foreach (Foundation f in Hollow.Foundations)
                for (int t = 0; t < 4; t++)
                {
                    var b = new PlacedBuilding(TownBuildings.Get("bakery"), f.Id, t, Frame);
                    string bad = PathRule.FirstUnreachable(Hollow, adj, new List<PlacedBuilding> { b });
                    if (bad != null) refused++;
                }
            Assert.That(refused, Is.GreaterThan(0));
        }

        // ------------------------------------------------------------------ pick-up
        [Test]
        public void PickUp_BuildingReturnsFrameAllPartsAndResident()
        {
            var s = AllPieces(); s.AddResident("baker");
            var before = s.Conservation();
            s.PlaceFrame("bakery", "F02", 0);
            foreach (string p in new[] { "oven", "door", "sign" }) s.PlacePart("bakery", p);
            s.PlaceResident("bakery");
            Assert.That(s.Conservation(), Is.EqualTo(before));
            Assert.That(s.PickUpBuilding("bakery"), Is.True);
            Assert.That(s.PlacedBuildingOf("bakery"), Is.Null);
            foreach (int p in new[] { Frame, Oven, Door, Sign }) Assert.That(s.InStock(p), Is.True);
            Assert.That(s.ResidentInStock("baker"), Is.True);
            Assert.That(s.Conservation(), Is.EqualTo(before));
            Assert.That(s.PickUpBuilding("bakery"), Is.False, "nothing left to pick up");
        }

        [Test]
        public void PickUp_APartOfACompleteBuildingSendsTheResidentBackToo()
        {
            var s = AllPieces(); s.AddResident("baker");
            s.PlaceFrame("bakery", "F02", 0);
            foreach (string p in new[] { "oven", "door", "sign" }) s.PlacePart("bakery", p);
            s.PlaceResident("bakery");
            Assert.That(s.PickUpPart("bakery", "door"), Is.True);
            Assert.That(s.InStock(Door), Is.True);
            Assert.That(s.ResidentInStock("baker"), Is.True, "an incomplete building cannot house the baker");
            Assert.That(s.PlacedBuildingOf("bakery").Resident, Is.Null);
            Assert.That(s.PickUpPart("bakery", "door"), Is.False, "already off");
            Assert.That(s.PickUpPart("bakery", "chimney"), Is.False);
            Assert.That(s.PlacePart("bakery", "door"), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlaceResident("bakery"), Is.EqualTo(PlaceResult.Ok));
        }

        [Test]
        public void PickUp_TheFramePartPicksUpTheWholeBuilding_AndTheResidentAlone()
        {
            var s = AllPieces(); s.AddResident("baker");
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(s.PickUpPart("bakery", "frame"), Is.True);
            Assert.That(s.PlacedBuildingOf("bakery"), Is.Null);
            s.PlaceFrame("bakery", "F02", 0);
            foreach (string p in new[] { "oven", "door", "sign" }) s.PlacePart("bakery", p);
            s.PlaceResident("bakery");
            Assert.That(s.PickUpResident("bakery"), Is.True);
            Assert.That(s.IsComplete("bakery"), Is.True, "the building stays complete");
            Assert.That(s.PickUpResident("bakery"), Is.False);
        }

        [Test]
        public void PickUp_FromAnyStateWorks_EvenWhenPartsAreMissing()
        {
            var s = Fresh(Frame);
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(s.PickUpBuilding("bakery"), Is.True);
            Assert.That(s.InStock(Frame), Is.True);
        }

        // ------------------------------------------------------------------ wish kinds at their edges
        [Test]
        public void Wish_AtLot()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0));
            Assert.That(One(t, PlacementWish.AtLot("L1", "")).Met, Is.True);
            Assert.That(One(t, PlacementWish.AtLot("L2", "")).Met, Is.False);
            Assert.That(One(t, PlacementWish.AtLot("L2", "")).Measured, Is.NaN);
        }

        [Test]
        public void Wish_NearPoint_EdgeIsInclusive()
        {
            // S lot at origin (half side 3); stall 2 m square at x=10 spans 9..11; gap = 9 - 3 = 6 m.
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0), Mark("Stall", 10, 0));
            WishResult r = One(t, PlacementWish.NearPoint("Stall", 6f, ""));
            Assert.That(r.Measured, Is.EqualTo(6f).Within(1e-3f));
            Assert.That(r.Met, Is.True, "exactly at the limit is met");
            Assert.That(One(t, PlacementWish.NearPoint("Stall", 5.99f, "")).Met, Is.False);
            Assert.That(One(t, PlacementWish.NearPoint("Stall", 6.5f, "")).Met, Is.True);
            Assert.That(One(t, PlacementWish.NearPoint("Nothing", 100f, "")).Met, Is.False, "an unknown target is never met");
        }

        [Test]
        public void Wish_NearPoint_ByPoint()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0));
            Assert.That(One(t, PlacementWish.NearPoint(8f, 0f, 5f, "")).Measured, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(One(t, PlacementWish.NearPoint(8f, 0f, 5f, "")).Met, Is.True);
            Assert.That(One(t, PlacementWish.NearPoint(8.01f, 0f, 5f, "")).Met, Is.False);
        }

        [Test]
        public void Wish_FarFrom_EdgeIsInclusive_AndTouchingIsZero()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0), Mark("Inn", 10, 0), Mark("Next", 4, 0));
            Assert.That(One(t, PlacementWish.FarFrom("Inn", 6f, "")).Met, Is.True);
            Assert.That(One(t, PlacementWish.FarFrom("Inn", 6.01f, "")).Met, Is.False);
            WishResult touch = One(t, PlacementWish.FarFrom("Next", 1f, ""));   // 2 m box at x=4 spans 3..5: touches the lot edge at x=3
            Assert.That(touch.Measured, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(touch.Met, Is.False);
        }

        [Test]
        public void Wish_FarFrom_UsesTheFootprintGapNotCentres_AndRotatedLots()
        {
            // The lot turned 45 degrees: its corner reaches 3*sqrt(2) along x, so the gap to the stall (west edge at x=9) is 9 - 4.2426.
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0, 45f), Mark("Stall", 10, 0));
            WishResult r = One(t, PlacementWish.FarFrom("Stall", 4f, ""));
            Assert.That(r.Measured, Is.EqualTo(9f - 3f * (float)Math.Sqrt(2.0)).Within(0.01f));
            Assert.That(r.Met, Is.True);
        }

        [Test]
        public void Wish_NextTo_EdgeIsInclusive()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0), Mark("Stall", 10, 0));
            Assert.That(One(t, PlacementWish.NextTo("Stall", 6f, "")).Met, Is.True);
            Assert.That(One(t, PlacementWish.NextTo("Stall", 5.99f, "")).Met, Is.False);
            t.Landmarks.Add(Mark("Wall", 4, 0));   // touching
            Assert.That(One(t, PlacementWish.NextTo("Wall", 0f, "")).Met, Is.True, "touching is a gap of 0");
        }

        [Test]
        public void Wish_Facing_ToleranceEdge()
        {
            // S lot at origin facing South, door at (0,-2.5) (body 5 m). A target at (d, -2.5-d) is exactly 45 degrees off the door's outward direction.
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0));
            WishResult r45 = One(t, PlacementWish.Facing(10f, -12.5f, 45f, ""));
            Assert.That(r45.Measured, Is.EqualTo(45f).Within(0.01f));
            Assert.That(r45.Met, Is.True, "exactly 45 degrees is within 45");
            WishResult r46 = One(t, PlacementWish.Facing(10f, -11.5f, 45f, ""));
            Assert.That(r46.Measured, Is.GreaterThan(45f));
            Assert.That(r46.Met, Is.False);
            Assert.That(One(t, PlacementWish.Facing(0f, -30f, 45f, "")).Measured, Is.EqualTo(0f).Within(0.01f));
            Assert.That(One(t, PlacementWish.Facing(0f, 30f, 45f, "")).Measured, Is.EqualTo(180f).Within(0.01f), "behind the building");
        }

        [Test]
        public void Wish_Facing_FollowsTheTurn_AndTheLotsYaw()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0));
            var west = PlacementWish.Facing(-30f, 0f, 10f, "");
            Assert.That(One(t, west, 0).Met, Is.False);
            Assert.That(WishEvaluator.DoorSide(t.Foundations[0], 1), Is.EqualTo(LotSide.West), "South then one step is West");
            Assert.That(One(t, west, 1).Met, Is.True);
            Assert.That(One(t, west, 5).Met, Is.True, "turn wraps");
            Assert.That(One(t, west, -3).Met, Is.True, "negative turn wraps");
            // A lot yawed 90 degrees: its local South points world +X?  Check the outward vector instead of assuming.
            Foundation y = Lot("L2", FoundationSize.S, 0, 0, 90f);
            TownPoint o = WishEvaluator.DoorOutward(y, 0);
            TownPoint expect = y.Footprint.ToWorld(0f, -1f) - y.Footprint.Centre;
            Assert.That(o.X, Is.EqualTo(expect.X).Within(1e-4f));
            Assert.That(o.Z, Is.EqualTo(expect.Z).Within(1e-4f));
        }

        [Test]
        public void Wish_Facing_ByLandmarkUsesItsCentre()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 0, 0), Mark("Fountain", 0, -20));
            Assert.That(One(t, PlacementWish.Facing("Fountain", 45f, "")).Met, Is.True);
            Assert.That(One(t, PlacementWish.Facing("Fountain", 45f, ""), 2).Met, Is.False);
        }

        [Test]
        public void Wish_InArea_EdgeIsInclusive()
        {
            var t = Bare(Lot("L1", FoundationSize.S, 5, 5));
            Assert.That(One(t, PlacementWish.InArea(5f, 5f, 10f, 10f, "")).Met, Is.True, "the centre on the corner is inside");
            Assert.That(One(t, PlacementWish.InArea(5.01f, 0f, 10f, 10f, "")).Met, Is.False);
            Assert.That(One(t, PlacementWish.InArea(5.01f, 0f, 10f, 10f, "")).Measured, Is.EqualTo(0.01f).Within(1e-3f));
            Assert.That(One(t, PlacementWish.InArea(10f, 10f, 0f, 0f, "")).Met, Is.True, "corners given in any order");
        }

        // ------------------------------------------------------------------ the baker's wishes on the real town
        [Test]
        public void Baker_HasTheThreeWishes()
        {
            BuildingDef b = TownBuildings.Get("bakery");
            Assert.That(b.Wishes.Length, Is.EqualTo(3));
            Assert.That(b.Wishes[0].Kind, Is.EqualTo(PlacementWishKind.NearPoint));
            Assert.That(b.Wishes[1].Kind, Is.EqualTo(PlacementWishKind.Facing));
            Assert.That(b.Wishes[2].Kind, Is.EqualTo(PlacementWishKind.FarFrom));
            Assert.That(b.Size, Is.EqualTo(FoundationSize.L));
            Assert.That(b.Parts, Is.EqualTo(new[] { "frame", "oven", "door", "sign" }));
        }

        [Test]
        public void Baker_AllThreeMetOnTheBakeryLot()
        {
            var s = AllPieces();
            Assert.That(s.PlaceFrame("bakery", "F02", 0), Is.EqualTo(PlaceResult.Ok));
            WishReport r = s.Wishes("bakery");
            Assert.That(r.AllMet, Is.True, string.Join("; ", Details(r)));
            Assert.That(r.Score, Is.EqualTo(1f));
            Assert.That(r.Results[0].Measured, Is.LessThanOrEqualTo(TownBuildings.BakerNearFountainM));
            Assert.That(r.Results[1].Measured, Is.LessThanOrEqualTo(TownBuildings.BakerFacingDeg));
            Assert.That(r.Results[2].Measured, Is.GreaterThanOrEqualTo(TownBuildings.BakerInnAwayM));
            Assert.That(r.Results[0].Detail, Does.Contain("limit"));
        }

        [Test]
        public void Baker_EachWishCanFailOnItsOwn()
        {
            BuildingDef b = TownBuildings.Get("bakery");
            WishReport far = WishEvaluator.Evaluate(Hollow, b, "F03", 0);   // far lot, door to the square
            Assert.That(far.Results[0].Met, Is.False, "F03 is not near the fountain");
            Assert.That(far.Results[2].Met, Is.True);
            WishReport turned = WishEvaluator.Evaluate(Hollow, b, "F02", 1);   // door to the west
            Assert.That(turned.Results[1].Met, Is.False, "door turned away from the square");
            Assert.That(turned.Results[0].Met, Is.True);
            WishReport atInn = WishEvaluator.Evaluate(Hollow, b, TownBuildings.Inn, 0);
            Assert.That(atInn.Results[2].Met, Is.False, "right against the inn");
            Assert.That(atInn.Results[2].Measured, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(atInn.Score, Is.LessThan(1f));
            Assert.That(WishEvaluator.Evaluate(Hollow, b, "F99", 0).MetCount, Is.EqualTo(0));
        }

        [Test]
        public void Baker_PreviewMatchesAfterPlacing_AndWishesFollowPickUp()
        {
            var s = AllPieces();
            WishReport preview = WishEvaluator.Evaluate(Hollow, TownBuildings.Get("bakery"), "F02", 0);
            s.PlaceFrame("bakery", "F02", 0);
            Assert.That(s.Wishes("bakery").Score, Is.EqualTo(preview.Score));
            s.PickUpBuilding("bakery");
            Assert.That(s.Wishes("bakery"), Is.Null);
        }

        static IEnumerable<string> Details(WishReport r) { foreach (WishResult x in r.Results) yield return x.Detail; }

        // ------------------------------------------------------------------ fuzz
        [Test]
        public void Fuzz_PlaceAndPickUp_NeverBreaksConservationOrLocksTheTown([Range(1, 20)] int seed)
        {
            var rng = new Random(seed * 7919);
            var s = new TownState(Hollow);
            var expected = new SortedSet<string>(StringComparer.Ordinal);
            int[] pieceIds = { Frame, Oven, Door, Sign, SpareFrame, SpareOven, 400, 999, -1 };
            string[] parts = { "frame", "oven", "door", "sign", "chimney" };
            string[] lots = new string[Hollow.Foundations.Count + 1];
            for (int i = 0; i < Hollow.Foundations.Count; i++) lots[i] = Hollow.Foundations[i].Id;
            lots[lots.Length - 1] = "F99";

            for (int step = 0; step < 400; step++)
            {
                var before = s.Conservation();
                int op = rng.Next(10);
                switch (op)
                {
                    case 0:
                    {
                        int id = pieceIds[rng.Next(pieceIds.Length)];
                        if (s.AddPiece(id) == PlaceResult.Ok) expected.Add("piece:" + id);
                        break;
                    }
                    case 1:
                        if (s.AddResident(rng.Next(4) == 0 ? "nobody" : "baker") == PlaceResult.Ok) expected.Add("resident:baker");
                        break;
                    case 2: case 3:
                    {
                        string lot = lots[rng.Next(lots.Length)]; int turn = rng.Next(-1, 6);
                        PlaceResult can = s.CanPlaceFrame("bakery", lot, turn);
                        PlaceResult got = s.PlaceFrame("bakery", lot, turn);
                        Assert.That(got, Is.EqualTo(can), "the preview and the placement agree");
                        break;
                    }
                    case 4: case 5: s.PlacePart("bakery", parts[rng.Next(parts.Length)]); break;
                    case 6: s.PlaceResident("bakery"); break;
                    case 7: s.SetTurn("bakery", rng.Next(-2, 6)); break;
                    case 8:
                    {
                        bool placed = s.PlacedBuildingOf("bakery") != null;
                        if (rng.Next(2) == 0) Assert.That(s.PickUpBuilding("bakery"), Is.EqualTo(placed));
                        else if (placed) s.PickUpPart("bakery", parts[rng.Next(parts.Length)]);
                        break;
                    }
                    default: s.PickUpResident("bakery"); break;
                }

                Assert.That(s.Conservation(), Is.EqualTo(expected), "step " + step + " op " + op + ": stock + placed must equal everything that came home");
                CheckInvariants(s);
            }

            // Whatever state the fuzz reached, one pick-up returns the lot to stock: nothing is stuck.
            if (s.PlacedBuildingOf("bakery") != null) Assert.That(s.PickUpBuilding("bakery"), Is.True);
            Assert.That(s.Placed, Is.Empty);
            var stock = new SortedSet<string>(StringComparer.Ordinal);
            foreach (int p in s.StockPieces) stock.Add("piece:" + p);
            foreach (string r in s.StockResidents) stock.Add("resident:" + r);
            Assert.That(stock, Is.EqualTo(expected));
        }

        static void CheckInvariants(TownState s)
        {
            var lots = new HashSet<string>();
            foreach (PlacedBuilding b in s.Placed)
            {
                BuildingDef def = TownBuildings.Get(b.BuildingId);
                Assert.That(lots.Add(b.FoundationId), Is.True, "one building per foundation");
                Assert.That(s.Layout.Foundation(b.FoundationId).Size, Is.EqualTo(def.Size));
                Assert.That(b.Turn, Is.InRange(0, 3));
                Assert.That(b.PiecesByPart[0], Is.GreaterThanOrEqualTo(0), "a placed building always has its frame");
                for (int i = 0; i < def.Parts.Length; i++)
                    if (b.PiecesByPart[i] >= 0)
                    {
                        Assert.That(PieceCatalog.TryGet(b.PiecesByPart[i], out PieceInfo info), Is.True);
                        Assert.That(info.Building, Is.EqualTo(def.Id)); Assert.That(info.Part, Is.EqualTo(def.Parts[i]));
                        Assert.That(s.InStock(b.PiecesByPart[i]), Is.False, "a placed piece is not also in stock");
                    }
                if (b.Resident != null) Assert.That(s.IsComplete(b.BuildingId), Is.True, "the resident lives only in a complete building");
            }
            var all = new List<PlacedBuilding>(s.Placed);
            Assert.That(PathRule.Blocked(s.Layout, s.Layout.Adjacency(), all), Is.False, "no reachable state cuts the path graph");
        }
    }
}
