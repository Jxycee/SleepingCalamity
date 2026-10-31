// Authority: none (pure EditMode tests). The Hollowmere town plan (Core/Town/HollowmereLayout) and the rules TownLayout.Validate() enforces:
// foundations never overlap each other, a landmark or a path node; path edges never cross a footprint; every side of every lot has a reachable doorstep.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class HollowmereLayoutTests
    {
        [Test]
        public void Hollowmere_IsValid()
        {
            List<string> errors = HollowmereLayout.Create().Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Ring1_HasTheDesignMix()
        {
            TownLayout t = HollowmereLayout.Create();
            Assert.AreEqual(3, t.Count(1, FoundationSize.L), "ring 1 L");
            Assert.AreEqual(4, t.Count(1, FoundationSize.M), "ring 1 M");
            Assert.AreEqual(3, t.Count(1, FoundationSize.S), "ring 1 S");
            int ring2 = t.Foundations.Count(f => f.Ring == 2);
            Assert.That(ring2, Is.InRange(20, 25), "ring 2 has about 20-25 foundations along the lanes");
            Assert.That(t.Foundations.Count(f => f.Ring == 3), Is.GreaterThan(0), "ring 3 is roughed in");
        }

        [Test]
        public void ClassSizes_MatchTheContract()
        {
            Assert.AreEqual(6f, Foundation.SideLength(FoundationSize.S));
            Assert.AreEqual(8f, Foundation.SideLength(FoundationSize.M));
            Assert.AreEqual(11f, Foundation.SideLength(FoundationSize.L));
            Assert.AreEqual(5f, Foundation.MaxBuilding(FoundationSize.S));
            Assert.AreEqual(7.5f, Foundation.MaxBuilding(FoundationSize.M));
            Assert.AreEqual(10.5f, Foundation.MaxBuilding(FoundationSize.L));
            foreach (Foundation f in HollowmereLayout.Create().Foundations)
            {
                Footprint fp = f.Footprint;
                Assert.AreEqual(f.Side, fp.HalfX * 2f, 1e-4f, f.Id + " is square at its class size");
                Assert.AreEqual(f.Side, fp.HalfZ * 2f, 1e-4f, f.Id + " is square at its class size");
            }
        }

        [Test]
        public void EveryFoundation_HasAReachableDoorstepOnEverySide()
        {
            TownLayout t = HollowmereLayout.Create();
            HashSet<string> reach = t.ReachableFrom(t.RootNode);
            foreach (Foundation f in t.Foundations)
                foreach (LotSide s in TownLayout.AllSides)
                {
                    PathNode n = t.Node(f.DoorstepId(s));
                    Assert.IsNotNull(n, f.Id + " " + s);
                    Assert.IsTrue(reach.Contains(n.Id), f.Id + " " + s + " doorstep unreachable");
                    Assert.IsFalse(f.Footprint.Contains(n.Pos), f.Id + " " + s + " doorstep inside the lot");
                }
        }

        // ---- Q3: the seeded ring 2 fill ----
        // "Neighbouring" = two assigned lots whose centres are closer than HollowmereLayout.NeighbourDistance (22 m: the next lot or two along the lane and the
        // lots across it). Turn is 0 everywhere (fronts to the lane), so the same variant twice in a row along a lane is also a variant clash.
        static List<(Foundation F, HollowmereLayout.HomeAssignment A)> KitLots(TownLayout t) =>
            t.Foundations.Where(f => f.Ring <= 2).Select(f => (f, HollowmereLayout.RebuiltHome(f.Id)))
                .Where(x => x.Item2.Kind == HollowmereLayout.BuildingKind.Townhouse || x.Item2.Kind == HollowmereLayout.BuildingKind.StoneCottage || x.Item2.Kind == HollowmereLayout.BuildingKind.Workshop).ToList();

        [Test]
        public void Ring2Fill_IsDeterministic()
        {
            TownLayout t = HollowmereLayout.Create();
            var a = HollowmereLayout.AssignRing2(t, HollowmereLayout.Ring2Seed);
            var b = HollowmereLayout.AssignRing2(HollowmereLayout.Create(), HollowmereLayout.Ring2Seed);
            CollectionAssert.AreEqual(a.Keys.OrderBy(k => k).ToList(), b.Keys.OrderBy(k => k).ToList());
            foreach (var kv in a) { Assert.AreEqual(kv.Value.Kind, b[kv.Key].Kind, kv.Key); Assert.AreEqual(kv.Value.Variant, b[kv.Key].Variant, kv.Key); Assert.AreEqual(kv.Value.Turn, b[kv.Key].Turn, kv.Key); }
            foreach (var kv in a) Assert.AreEqual(kv.Value.Kind, HollowmereLayout.RebuiltHome(kv.Key).Kind, kv.Key + " via RebuiltHome");
        }

        [Test]
        public void Ring2Fill_FamiliesFitTheLotClass()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (var (f, a) in KitLots(t))
            {
                Assert.That(a.Turn, Is.InRange(0, 3), f.Id + " turn");
                switch (a.Kind)
                {
                    case HollowmereLayout.BuildingKind.StoneCottage: Assert.AreEqual(FoundationSize.S, f.Size, f.Id + " cottage is S class"); break;
                    case HollowmereLayout.BuildingKind.Townhouse: Assert.AreEqual(FoundationSize.M, f.Size, f.Id + " townhouse is M class"); break;
                    case HollowmereLayout.BuildingKind.Workshop:
                        if (a.Variant == 'A') Assert.AreEqual(FoundationSize.S, f.Size, f.Id + " Workshop_A is S class");
                        else Assert.AreEqual(FoundationSize.M, f.Size, f.Id + " Workshop_" + a.Variant + " is M class");
                        break;
                }
            }
        }

        [Test]
        public void Ring2Fill_LeavesReservedAndOtherLotsAlone()
        {
            TownLayout t = HollowmereLayout.Create();
            var fill = HollowmereLayout.AssignRing2(t, HollowmereLayout.Ring2Seed);
            foreach (string id in new[] { "R2_07", "R2_18", "R2_22", "R2_08", "R2_09", "R2_01", "R2_03" }.Concat(HollowmereLayout.Ring2Reserved))
                CollectionAssert.DoesNotContain(fill.Keys, id, id + " is reserved or already used");
            foreach (Foundation f in t.Foundations.Where(f => f.Size == FoundationSize.L)) CollectionAssert.DoesNotContain(fill.Keys, f.Id, f.Id + " is L");
            foreach (string id in fill.Keys) Assert.AreEqual(2, t.Foundation(id).Ring, id);
            Assert.AreEqual(HollowmereLayout.BuildingKind.Garden, HollowmereLayout.RebuiltBuilding("R2_07"));
            Assert.AreEqual(HollowmereLayout.BuildingKind.Smithy, HollowmereLayout.RebuiltBuilding("R2_01"), "Q2's smithy stays fixed");
            Assert.AreEqual(HollowmereLayout.BuildingKind.Schoolhouse, HollowmereLayout.RebuiltBuilding("R2_03"), "Q2's schoolhouse stays fixed");
        }

        [Test]
        public void Ring2Fill_EmptyShareIsAboutAThird()
        {
            var fill = HollowmereLayout.AssignRing2(HollowmereLayout.Create(), HollowmereLayout.Ring2Seed);
            int empty = fill.Values.Count(a => a.Kind == HollowmereLayout.BuildingKind.Empty);
            double share = empty / (double)fill.Count;
            Assert.That(fill.Count, Is.GreaterThanOrEqualTo(10), "a real spread");
            Assert.That(share, Is.InRange(0.25, 0.35), empty + " of " + fill.Count + " empty");
        }

        [Test]
        public void Ring2Fill_NoNeighbourRepeatsAVariant()
        {
            TownLayout t = HollowmereLayout.Create();
            var lots = KitLots(t);
            for (int i = 0; i < lots.Count; i++)
                for (int j = i + 1; j < lots.Count; j++)
                {
                    var (fa, a) = lots[i]; var (fb, b) = lots[j];
                    if (a.Kind != b.Kind || a.Variant != b.Variant) continue;
                    Assert.GreaterOrEqual(TownPoint.Distance(fa.Centre, fb.Centre), HollowmereLayout.NeighbourDistance, fa.Id + " and " + fb.Id + " both " + a.VariantName);
                }
        }

        [Test]
        public void Ring2Fill_EmptiesAreSpreadNotClumped()
        {
            TownLayout t = HollowmereLayout.Create();
            var empties = HollowmereLayout.AssignRing2(t, HollowmereLayout.Ring2Seed).Where(kv => kv.Value.Kind == HollowmereLayout.BuildingKind.Empty).Select(kv => t.Foundation(kv.Key)).ToList();
            for (int i = 0; i < empties.Count; i++)
                for (int j = i + 1; j < empties.Count; j++)
                    Assert.GreaterOrEqual(TownPoint.Distance(empties[i].Centre, empties[j].Centre), 14f, empties[i].Id + " and " + empties[j].Id + " are next to each other");
        }

        [Test]
        public void Ring2Fill_GoldenForTheFixedSeed()
        {
            TownLayout t = HollowmereLayout.Create();
            var fill = HollowmereLayout.AssignRing2(t, HollowmereLayout.Ring2Seed);
            CollectionAssert.AreEquivalent(new[] { "R2_10", "R2_15", "R2_17", "R2_21" }, fill.Where(kv => kv.Value.Kind == HollowmereLayout.BuildingKind.Empty).Select(kv => kv.Key).ToList(), "empty lots for the shipped seed");
            Assert.AreEqual("Townhouse_B", fill["R2_02"].VariantName);
            Assert.AreEqual("Workshop_C", fill["R2_13"].VariantName);
            Assert.AreEqual("Workshop_C", fill["R2_23"].VariantName);
            var other = HollowmereLayout.AssignRing2(t, HollowmereLayout.Ring2Seed + 1);
            Assert.IsTrue(fill.Any(kv => other[kv.Key].Kind != kv.Value.Kind || other[kv.Key].Variant != kv.Value.Variant), "a different seed gives a different town");
        }

        [Test]
        public void EarlierPlacements_AreUnchanged()
        {
            Assert.AreEqual("Townhouse_A", HollowmereLayout.RebuiltHome("F06").VariantName); Assert.AreEqual(0, HollowmereLayout.RebuiltHome("F06").Turn);
            Assert.AreEqual("Townhouse_B", HollowmereLayout.RebuiltHome("F07").VariantName); Assert.AreEqual(0, HollowmereLayout.RebuiltHome("F07").Turn);
            Assert.AreEqual("StoneCottage_B", HollowmereLayout.RebuiltHome("R2_09").VariantName); Assert.AreEqual(0, HollowmereLayout.RebuiltHome("R2_09").Turn);
            Assert.AreEqual("Workshop_B", HollowmereLayout.RebuiltHome("R2_08").VariantName); Assert.AreEqual(0, HollowmereLayout.RebuiltHome("R2_08").Turn);
            TownLayout t = HollowmereLayout.Create();
            Assert.AreEqual(FoundationSize.S, t.Foundation("R2_09").Size); Assert.AreEqual(FoundationSize.M, t.Foundation("R2_08").Size);
            Assert.AreNotEqual(0f, t.Foundation("R2_09").Y, "pad height kept"); Assert.AreNotEqual(0f, t.Foundation("R2_08").Y, "pad height kept");
        }

        [Test]
        public void ApprovedCorner_KeepsItsPlace()
        {
            TownLayout t = HollowmereLayout.Create();
            Landmark fountain = t.Landmark("DryFountain"), stall = t.Landmark("Stall_1");
            Assert.AreEqual(0f, fountain.Footprint.Centre.X); Assert.AreEqual(0f, fountain.Footprint.Centre.Z);
            Assert.AreEqual(4.5f, stall.Footprint.Centre.X); Assert.AreEqual(4.5f, stall.Footprint.Centre.Z); Assert.AreEqual(200f, stall.Footprint.Yaw);
            // The facade (front 7.5 m wide on z 7.5, x -5..2.5, about 6.5 m deep) fits on F01 inside the M body limit.
            Foundation f01 = t.Foundation("F01");
            Assert.AreEqual(FoundationSize.M, f01.Size);
            var facade = new Footprint(new TownPoint(-1.25f, 7.5f + 3.25f), 7.5f, 6.5f, 0f);
            foreach (TownPoint c in facade.Corners()) Assert.IsTrue(f01.Footprint.Contains(c, 1e-2f), "facade corner " + c + " off F01");
        }

        [Test]
        public void FixedLandmarks_ArePresent()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (LandmarkKind k in new[] { LandmarkKind.Fountain, LandmarkKind.BellTower, LandmarkKind.TitheGate, LandmarkKind.HeroesHome, LandmarkKind.Mill })
                Assert.IsTrue(t.Landmarks.Any(l => l.Kind == k), "missing " + k);
            Assert.AreEqual(3, t.Landmarks.Count(l => l.Kind == LandmarkKind.Stall), "three fixed market stalls");
            Assert.IsTrue(t.Landmark("TitheGate").Footprint.Centre.Z > HollowmereLayout.SquareMaxZ, "the tithe gate is on the north side");
            Assert.IsTrue(t.Landmark("BellTower").Footprint.Centre.Z > HollowmereLayout.SquareMaxZ, "the bell tower is on the north side");
        }

        [Test]
        public void KeepHall_StandsOnTheTerrace_AndItsDoorIsReachableFromTheSquare()
        {
            TownLayout t = HollowmereLayout.Create();
            Landmark hall = t.Landmark("KeepHall");
            Assert.IsNotNull(hall, "KeepHall landmark");
            Assert.AreEqual(LandmarkKind.KeepHall, hall.Kind);
            Assert.AreEqual(HollowmereLayout.KeepHeight, hall.Y, 1e-4f, "on the terrace");
            Assert.AreEqual("KeepHall_Door", hall.DoorNode);
            PathNode door = t.Node(hall.DoorNode);
            Assert.IsNotNull(door);
            Assert.AreEqual(HollowmereLayout.KeepHeight, door.Y, 1e-4f);
            Assert.IsFalse(hall.Footprint.Contains(door.Pos), "the door node is outside the footprint");
            Assert.IsTrue(t.ReachableFrom(t.RootNode).Contains(door.Id), "door reachable from the square");
        }

        // ------------------------------------------------------------------------------------------------ the validator on broken layouts
        static TownLayout Tiny()
        {
            // One M lot at the origin with its doorstep ring, joined to a root node south of it.
            var t = new TownLayout { RootNode = "root" };
            var f = new Foundation { Id = "A", Ring = 1, Size = FoundationSize.M, Centre = new TownPoint(0f, 0f), Facing = LotSide.South };
            t.Foundations.Add(f);
            AddLotNodes(t, f);
            t.Nodes.Add(new PathNode { Id = "root", Pos = new TownPoint(0f, -8f) });
            t.Edges.Add(new PathEdge("root", f.DoorstepId(LotSide.South)));
            return t;
        }

        static void AddLotNodes(TownLayout t, Foundation f)
        {
            for (int c = 0; c < 4; c++) t.Nodes.Add(new PathNode { Id = f.CornerId(c), Pos = f.CornerPoint(c), Kind = PathNodeKind.Corner });
            foreach (LotSide s in TownLayout.AllSides) t.Nodes.Add(new PathNode { Id = f.DoorstepId(s), Pos = f.DoorstepPoint(s), Kind = PathNodeKind.Doorstep });
            string[] ring = { f.CornerId(0), f.DoorstepId(LotSide.South), f.CornerId(1), f.DoorstepId(LotSide.East), f.CornerId(2), f.DoorstepId(LotSide.North), f.CornerId(3), f.DoorstepId(LotSide.West) };
            for (int i = 0; i < ring.Length; i++) t.Edges.Add(new PathEdge(ring[i], ring[(i + 1) % ring.Length]));
        }

        static void AssertError(TownLayout t, string fragment)
        {
            List<string> errors = t.Validate();
            Assert.IsTrue(errors.Any(e => e.Contains(fragment)), "expected an error containing '" + fragment + "', got:\n" + string.Join("\n", errors));
        }

        [Test]
        public void Validator_AcceptsTheTinyLayout()
        {
            List<string> errors = Tiny().Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Validator_RejectsOverlappingFoundations()
        {
            TownLayout t = Tiny();
            var b = new Foundation { Id = "B", Ring = 1, Size = FoundationSize.S, Centre = new TownPoint(6.5f, 0f), Yaw = 30f };
            t.Foundations.Add(b);
            AssertError(t, "A overlaps foundation B");
        }

        [Test]
        public void Validator_AllowsTouchingFoundations()
        {
            var a = new Footprint(new TownPoint(0f, 0f), 8f, 8f, 0f);
            Assert.IsFalse(a.Overlaps(new Footprint(new TownPoint(8f, 0f), 8f, 8f, 0f)), "edge to edge is not an overlap");
            Assert.IsTrue(a.Overlaps(new Footprint(new TownPoint(7.9f, 0f), 8f, 8f, 0f)));
            Assert.IsTrue(a.Overlaps(new Footprint(new TownPoint(5.2f, 5.2f), 4f, 4f, 45f)), "a turned lot over a corner");
            Assert.IsFalse(a.Overlaps(new Footprint(new TownPoint(6.9f, 6.9f), 4f, 4f, 45f)), "a turned lot clear of the corner");
        }

        [Test]
        public void Validator_RejectsAFoundationOnALandmark()
        {
            TownLayout t = Tiny();
            t.Landmarks.Add(new Landmark { Id = "Well", Footprint = new Footprint(new TownPoint(4f, 4f), 2f, 2f, 0f) });
            AssertError(t, "A overlaps landmark Well");
        }

        [Test]
        public void Validator_RejectsAPathNodeInsideAFoundation()
        {
            TownLayout t = Tiny();
            t.Nodes.Add(new PathNode { Id = "stray", Pos = new TownPoint(1f, 1f) });
            AssertError(t, "path node stray is inside foundation A");
        }

        [Test]
        public void Validator_RejectsAnEdgeAcrossAFoundation()
        {
            TownLayout t = Tiny();
            t.Edges.Add(new PathEdge(Tiny().Foundations[0].DoorstepId(LotSide.West), Tiny().Foundations[0].DoorstepId(LotSide.East)));
            AssertError(t, "crosses foundation A");
        }

        [Test]
        public void Validator_RejectsAnEdgeAcrossALandmark()
        {
            TownLayout t = Tiny();
            t.Nodes.Add(new PathNode { Id = "far", Pos = new TownPoint(12f, -8f) });
            t.Edges.Add(new PathEdge("root", "far"));
            t.Landmarks.Add(new Landmark { Id = "Stall", Footprint = new Footprint(new TownPoint(6f, -8f), 2.8f, 1.8f, 20f) });
            AssertError(t, "path edge root-far crosses landmark Stall");
        }

        [Test]
        public void Validator_RejectsAnUnreachableSide()
        {
            TownLayout t = Tiny();
            // Cut the lot's perimeter on both sides of the north doorstep: it stays on the graph but nothing reaches it.
            t.Edges.RemoveAll(e => e.A == "A_North" || e.B == "A_North");
            t.Nodes.Add(new PathNode { Id = "orphan", Pos = new TownPoint(0f, 9f) });
            t.Edges.Add(new PathEdge("A_North", "orphan"));
            AssertError(t, "A: the North doorstep is not reachable from root");
        }

        [Test]
        public void Validator_RejectsAMissingDoorstep()
        {
            TownLayout t = Tiny();
            t.Nodes.RemoveAll(n => n.Id == "A_East");
            AssertError(t, "A: no doorstep node on its East side");
        }

        [Test]
        public void Validator_RejectsDuplicateIdsAndDanglingEdges()
        {
            TownLayout t = Tiny();
            t.Foundations.Add(new Foundation { Id = "A", Ring = 2, Size = FoundationSize.S, Centre = new TownPoint(40f, 0f) });
            t.Edges.Add(new PathEdge("root", "nowhere"));
            AssertError(t, "duplicate id A");
            AssertError(t, "names a missing node");
        }

        // ------------------------------------------------------------------------------------------------ terrain (terrain pass, 2026-10-31)
        [Test]
        public void Terrain_TerracesStepDownTheHill()
        {
            TownLayout t = HollowmereLayout.Create();
            Assert.AreEqual(0f, HollowmereLayout.Ground(t, 0f, -10f), 0.05f, "the square is the datum");
            Assert.AreEqual(HollowmereLayout.KeepHeight, HollowmereLayout.Ground(t, 0f, 30f), 0.3f, "the keep terrace stands 4 m above the square");
            float flank = HollowmereLayout.Ground(t, -46f, -10f), south = HollowmereLayout.Ground(t, 0f, -49f);
            Assert.That(flank, Is.InRange(-4.5f, -2.5f), "the Ring Lane on the west flank is one terrace (about 3 m) down");
            Assert.That(south, Is.InRange(-7.5f, -5.5f), "the Ring Lane on the south is two terraces down: it spirals");
            float water = HollowmereLayout.WaterY;
            Assert.That(-water, Is.InRange(10f, 14f), "the lake sits 10-14 m below the square");
            Assert.Less(HollowmereLayout.Ground(t, HollowmereLayout.LakeCentre.X, HollowmereLayout.LakeCentre.Z), water - 2f, "the lake fills a basin");
            Assert.Less(HollowmereLayout.NaturalGround(0f, -67f), HollowmereLayout.NaturalGround(0f, -64f) - 2f, "a dry ditch lies outside the wall's bank");
        }

        [Test]
        public void Terrain_EveryLotIsAFlatPadAtItsOwnHeight()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (Foundation f in t.Foundations)
                foreach (TownPoint c in f.Footprint.Corners())
                    Assert.AreEqual(f.Y, HollowmereLayout.Ground(t, c.X, c.Z), 0.05f, f.Id + " pad is not flat at its corner " + c);
            foreach (Foundation f in t.Foundations.Where(f => f.Ring == 1)) Assert.AreEqual(0f, f.Y, 1e-3f, f.Id + " stands on the square");
        }

        [Test]
        public void Terrain_NoPathIsSteeperThan20Degrees()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (PathEdge e in t.Edges)
            {
                PathNode a = t.Node(e.A), b = t.Node(e.B);
                if (a.Kind == PathNodeKind.Stair && b.Kind == PathNodeKind.Stair) continue;
                float run = TownPoint.Distance(a.Pos, b.Pos);
                Assert.LessOrEqual(System.Math.Abs(a.Y - b.Y), TownLayout.MaxGrade * run + 0.05f, e.A + "-" + e.B);
            }
        }

        [Test]
        public void Validator_RejectsASteepEdgeButNotAStair()
        {
            TownLayout t = Tiny();
            t.Nodes.Add(new PathNode { Id = "up", Pos = new TownPoint(0f, -12f), Y = 3f, Kind = PathNodeKind.Lane });
            t.Edges.Add(new PathEdge("root", "up"));
            AssertError(t, "path edge root-up is too steep");

            TownLayout s = Tiny();
            s.Node("root").Kind = PathNodeKind.Stair;
            s.Nodes.Add(new PathNode { Id = "up", Pos = new TownPoint(0f, -12f), Y = 3f, Kind = PathNodeKind.Stair });
            s.Edges.Add(new PathEdge("root", "up"));
            List<string> errors = s.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Validator_RejectsADoorstepOffTheLotHeight()
        {
            TownLayout t = Tiny();
            t.Node("A_West").Y = 1.2f;
            AssertError(t, "A: the West doorstep is not at the lot's height");
        }

        [Test]
        public void Ruins_MatchTheKitAndTurn()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (Foundation f in t.Foundations)
            {
                StringAssert.IsMatch("^Ruin_[SML]_[ABC]$", f.RuinVariant, f.Id);
                Assert.AreEqual("Ruin_" + f.Size + "_", f.RuinVariant.Substring(0, 7), f.Id + " ruin of its own size class");
                Assert.That(f.RuinTurn, Is.InRange(0, 3), f.Id);
            }
            Assert.Greater(t.Foundations.Select(f => f.RuinTurn).Distinct().Count(), 2, "the turns vary from lot to lot");
            Assert.AreEqual(HollowmereLayout.Create().Foundations.Select(f => f.RuinTurn), t.Foundations.Select(f => f.RuinTurn), "turns are deterministic");
        }

        [Test]
        public void Rebuilt_KitReadyShops_StandOnRing2MediumLots()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (var kind in new[] { HollowmereLayout.BuildingKind.Smithy, HollowmereLayout.BuildingKind.Schoolhouse })
            {
                var lots = t.Foundations.Where(f => HollowmereLayout.RebuiltBuilding(f.Id) == kind).ToList();
                Assert.AreEqual(1, lots.Count, kind + " stands on exactly one lot");
                Assert.AreEqual(2, lots[0].Ring, kind + " is a ring-2 shop");
                Assert.AreEqual(FoundationSize.M, lots[0].Size, kind + " needs an M lot");
            }
        }

        [Test]
        public void Footprint_SegmentTest()
        {
            var fp = new Footprint(new TownPoint(0f, 0f), 4f, 2f, 30f);
            Assert.IsTrue(fp.SegmentCrosses(new TownPoint(-5f, 0f), new TownPoint(5f, 0f)));
            Assert.IsFalse(fp.SegmentCrosses(new TownPoint(-5f, 4f), new TownPoint(5f, 4f)));
            Assert.IsFalse(fp.SegmentCrosses(new TownPoint(3f, 3f), new TownPoint(4f, 4f)));
            Assert.IsTrue(fp.SegmentCrosses(new TownPoint(0f, 0f), new TownPoint(9f, 9f)), "starting inside counts");
        }
    }
}
