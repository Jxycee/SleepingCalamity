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
