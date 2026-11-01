// Authority: none (pure EditMode tests). Core/Town/SaltwakeRoadPlan and the western fall (HollowmereLayout.OuterFall): the Saltwake road outside the
// Lower Gate (Q15).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class SaltwakeRoadPlanTests
    {
        static TownLayout layout;
        static List<RoadItem> plan;
        static TownLayout T => layout ?? (layout = HollowmereLayout.Create());
        static List<RoadItem> Plan => plan ?? (plan = SaltwakeRoadPlan.Create(T));
        static Lane Road => HollowmereLayout.FindLane(T, SaltwakeRoadPlan.RoadLane);

        static string Sig(IEnumerable<RoadItem> items) => string.Join(";", items.Select(i => i.Kind + i.Piece + i.X.ToString("0.000") + "," + i.Z.ToString("0.000") + "," + i.Yaw.ToString("0.0") + i.Presets));

        [Test]
        public void Plan_IsDeterministic()
        {
            Assert.AreEqual(Sig(Plan), Sig(SaltwakeRoadPlan.Create(HollowmereLayout.Create())));
        }

        [Test]
        public void Fall_LeavesTheTownAndTheLakeAlone()
        {
            Assert.AreEqual(0f, HollowmereLayout.OuterFall(SaltwakeRoadPlan.TerrainMinX + 1f + 200f, -80f));
            Assert.AreEqual(0f, HollowmereLayout.OuterFall(HollowmereLayout.FallStartX + 0.1f, -100f));
            Assert.Less(HollowmereLayout.OuterFall(-148f, -86f), -6f, "the west edge sits well below the meadow");
            foreach (Foundation f in T.Foundations) Assert.AreEqual(0f, HollowmereLayout.OuterFall(f.Centre.X, f.Centre.Z), f.Id);
        }

        [Test]
        public void Road_WindsDownAndLeavesTheTerrain()
        {
            Lane road = Road;
            float s = 0f, drop = road.Heights[0] - road.Heights[road.Heights.Length - 1];
            Assert.Greater(drop, 6f, "the road falls into the valley");
            for (int i = 1; i < road.Points.Length; i++)
            {
                float run = TownPoint.Distance(road.Points[i - 1], road.Points[i]);
                s += run;
                Assert.LessOrEqual(road.Heights[i] - road.Heights[i - 1], 0.05f, "never climbs on the way out, point " + i);
                Assert.LessOrEqual(System.Math.Abs(road.Heights[i] - road.Heights[i - 1]), HollowmereLayout.MaxLaneGrade * run + 0.05f, "grade, point " + i);
            }
            Assert.Less(road.Points[road.Points.Length - 1].X, SaltwakeRoadPlan.TerrainMinX, "the lane runs off the terrain's west edge");
            Assert.Greater(SaltwakeRoadPlan.ShrineS(road.Points), SaltwakeRoadPlan.RoadsideStart, "the shrine stands past the R3 lots");
        }

        [Test]
        public void Roadside_HasOneShrineOneCartAndMilestonesPerPreset()
        {
            foreach (RoadPresets p in new[] { RoadPresets.Ruined, RoadPresets.Rebuilt })
            {
                var mine = Plan.Where(i => (i.Presets & p) != 0).ToList();
                Assert.AreEqual(1, mine.Count(i => i.Kind == RoadItemKind.Shrine), p + " shrine");
                Assert.AreEqual(1, mine.Count(i => i.Kind == RoadItemKind.Cart), p + " cart");
                Assert.GreaterOrEqual(mine.Count(i => i.Kind == RoadItemKind.Milestone), 3, p + " milestones");
                Assert.GreaterOrEqual(mine.Count(i => i.Kind == RoadItemKind.Hedge), 10, p + " hedges");
                Assert.GreaterOrEqual(mine.Count(i => i.Kind == RoadItemKind.Fence), 10, p + " fences");
            }
            Assert.IsTrue(Plan.Any(i => i.Piece == "Cart_Broken" && i.Presets == RoadPresets.Ruined));
            Assert.IsTrue(Plan.Any(i => i.Piece == "Shrine_Ruined" && i.Presets == RoadPresets.Ruined));
        }

        [Test]
        public void Roadside_StaysOffTheRoadLotsAndLandmarks()
        {
            Lane road = Road;
            foreach (RoadItem i in Plan.Where(i => !i.OnRing))
            {
                var p = new TownPoint(i.X, i.Z);
                Assert.Greater(HollowmereLayout.DistanceToPolyline(p, road.Points), road.Width * 0.5f + 0.4f, "on the road: " + i);
                foreach (Foundation f in T.Foundations) Assert.IsFalse(f.Footprint.Contains(p, 1f), i + " in lot " + f.Id);
                foreach (Landmark l in T.Landmarks) Assert.IsFalse(l.Footprint.Contains(p, 1f), i + " in " + l.Id);
                foreach (Lane lane in T.Lanes)
                    if (lane.Id != road.Id) Assert.Greater(HollowmereLayout.DistanceToPolyline(p, lane.Points), lane.Width * 0.5f, i + " on " + lane.Id);
            }
        }

        [Test]
        public void Cards_StayOnTheirSideOfTheTerrainEdgeAndAwayFromTheRoad()
        {
            Lane road = Road;
            foreach (RoadItem i in Plan.Where(i => i.Kind == RoadItemKind.Tree || i.Kind == RoadItemKind.FieldHedge))
            {
                Assert.AreEqual(!i.OnRing, SaltwakeRoadPlan.OnTerrain(i.X, i.Z), "ring flag: " + i);
                if (!i.OnRing && i.Kind == RoadItemKind.Tree)
                    Assert.GreaterOrEqual(HollowmereLayout.DistanceToPolyline(new TownPoint(i.X, i.Z), road.Points), SaltwakeRoadPlan.RoadsideTreeDistance, "a card tree too close to the road: " + i);
                if (!i.OnRing && i.Kind == RoadItemKind.FieldHedge) Assert.Greater(SaltwakeRoadPlan.FieldWeight(T, i.X, i.Z), 0.5f, "outside the fields: " + i);
                float lx = (i.X - HollowmereLayout.LakeCentre.X) / HollowmereLayout.LakeRadiusX, lz = (i.Z - HollowmereLayout.LakeCentre.Z) / HollowmereLayout.LakeRadiusZ;
                if (!i.OnRing) Assert.Greater(lx * lx + lz * lz, 1.1f * 1.1f, "in the lake: " + i);   // (the western valley lies below the water line, away from the lake)
            }
            Assert.Greater(Plan.Count(i => i.Kind == RoadItemKind.Tree && !i.OnRing), 60, "trees on the terrain frame the low cameras");
            Assert.Greater(Plan.Count(i => i.Kind == RoadItemKind.Tree && i.OnRing), 500, "trees on the backdrop ring");
        }

        [Test]
        public void HorizonBelt_IsADenseWoodedBandWithAGapOverTheLake()
        {
            List<RoadItem> belt = Plan.Where(i => i.Belt).ToList();
            Assert.Greater(belt.Count, 700, "dense enough to read as a wooded skyline");
            Assert.Less(belt.Count, 3000);
            TownPoint[] ringRoad = SaltwakeRoadPlan.RingRoad(T);
            foreach (RoadItem i in belt)
            {
                Assert.AreEqual(RoadItemKind.Tree, i.Kind); Assert.IsTrue(i.OnRing);
                float r = TownPoint.Distance(new TownPoint(i.X, i.Z), SaltwakeRoadPlan.RingCentre);
                Assert.That(r, Is.InRange(SaltwakeRoadPlan.BeltMinRadius, SaltwakeRoadPlan.BeltMaxRadius), i.ToString());
                Assert.That(i.Scale, Is.InRange(SaltwakeRoadPlan.BeltMinScale, SaltwakeRoadPlan.BeltMaxScale), i.ToString());
                Assert.IsFalse(SaltwakeRoadPlan.InBeltGap(i.X, i.Z), "in the lake gap: " + i);
                Assert.IsFalse(SaltwakeRoadPlan.OnTerrain(i.X, i.Z, -10f), "on the terrain: " + i);
                var p = new TownPoint(i.X, i.Z);
                Assert.GreaterOrEqual(HollowmereLayout.DistanceToPolyline(p, Road.Points), SaltwakeRoadPlan.BeltRoadClear, "on the road: " + i);
                Assert.GreaterOrEqual(HollowmereLayout.DistanceToPolyline(p, ringRoad), SaltwakeRoadPlan.BeltRoadClear, "on the ring road: " + i);
            }
            Assert.Greater(belt.Max(i => i.Scale) - belt.Min(i => i.Scale), 0.7f, "scale variation");
            // all the way round: every 30-degree sector outside the gap holds trees
            for (int sector = 0; sector < 12; sector++)
            {
                float mid = -180f + sector * 30f + 15f, cx = SaltwakeRoadPlan.RingCentre.X + 220f * (float)System.Math.Cos(mid * System.Math.PI / 180), cz = SaltwakeRoadPlan.RingCentre.Z + 220f * (float)System.Math.Sin(mid * System.Math.PI / 180);
                Func<float, bool> gapAt = d => SaltwakeRoadPlan.InBeltGap(SaltwakeRoadPlan.RingCentre.X + 220f * (float)Math.Cos(d * Math.PI / 180), SaltwakeRoadPlan.RingCentre.Z + 220f * (float)Math.Sin(d * Math.PI / 180));
                bool wholeGap = gapAt(mid - 14f) && gapAt(mid + 14f), noGap = !gapAt(mid - 15f) && !gapAt(mid) && !gapAt(mid + 15f);
                int inSector = belt.Count(i => { float a = (float)(Math.Atan2(i.Z - SaltwakeRoadPlan.RingCentre.Z, i.X - SaltwakeRoadPlan.RingCentre.X) * 180 / Math.PI); return a >= mid - 15f && a < mid + 15f; });
                if (wholeGap) Assert.AreEqual(0, inSector, "gap sector " + mid); else if (noGap) Assert.Greater(inSector, 10, "sector " + mid);
            }
        }

        [Test]
        public void Fields_AreZeroInsideTheTownAndOnTheLake()
        {
            Assert.AreEqual(0f, SaltwakeRoadPlan.FieldWeight(T, 0f, 0f));
            Assert.AreEqual(0f, SaltwakeRoadPlan.FieldWeight(T, -21f, -60f), "the Lower Gate");
            Assert.AreEqual(0f, SaltwakeRoadPlan.FieldWeight(T, HollowmereLayout.LakeCentre.X, HollowmereLayout.LakeCentre.Z));
            Assert.Greater(SaltwakeRoadPlan.FieldWeight(T, -120f, -40f), 0.9f, "open ground west of the town");
        }
    }
}
