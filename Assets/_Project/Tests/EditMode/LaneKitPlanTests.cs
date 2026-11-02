// Authority: none (pure EditMode tests). Core/Town/LaneKitPlan: the placement rows of Hollowmere's ring 2 lane kit (Q11, Reference/hollowmere/lanes/CONTRACT.md).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class LaneKitPlanTests
    {
        static readonly TownLayout T = HollowmereLayout.Create();
        static readonly List<LaneKitRow> Rows = LaneKitPlan.Plan(T);

        static TownPoint P(LaneKitRow r) => new TownPoint(r.X, r.Z);
        static float Ground(LaneKitRow r) => HollowmereLayout.Ground(T, r.X, r.Z);
        static TownPoint Along(LaneKitRow r) { double a = r.Yaw * Math.PI / 180.0; return new TownPoint((float)Math.Cos(a), (float)-Math.Sin(a)); }
        // Height of the lane line (the relaxed profile) under the row: the lane-aligned pieces follow it, while Ground bends toward lot pads at the lane edge.
        static float LaneLine(LaneKitRow r)
        {
            float best = float.MaxValue, h = 0f; var p = P(r);
            foreach (Lane l in T.Lanes)
                for (int i = 1; i < l.Points.Length; i++)
                {
                    TownPoint q = HollowmereLayout.ClosestOnSegment(p, l.Points[i - 1], l.Points[i], out float t);
                    float d = TownPoint.Distance(p, q);
                    if (d < best) { best = d; h = l.Heights[i - 1] + (l.Heights[i] - l.Heights[i - 1]) * t; }
                }
            return h;
        }
        static bool IsWallLike(LaneKitRow r) => r.IsWall || r.Piece == LanePiece.GatePier || r.Piece == LanePiece.GardenWallEnd || r.Piece == LanePiece.WallLantern || r.Piece == LanePiece.WallLanternBroken;

        [Test]
        public void Plan_IsDeterministic_AndMatchesTheGolden()
        {
            Assert.AreEqual(LaneKitPlan.Signature(Rows), LaneKitPlan.Signature(LaneKitPlan.Plan(HollowmereLayout.Create())));
            string sig = LaneKitPlan.Signature(Rows);
            Assert.AreEqual(GoldenCount, Rows.Count, "row count (hash " + LaneKitPlan.Hash(sig) + ")");
            Assert.AreEqual(GoldenHash, LaneKitPlan.Hash(sig), "signature hash");
        }
        const int GoldenCount = 1098;   // A10: no gutter bars on the segments beside a stair flight (was 1201)
        const uint GoldenHash = 1145032234u;

        [Test]
        public void BothPresetsArePopulated_WithEveryFamily()
        {
            foreach (bool rebuilt in new[] { false, true })
            {
                var rows = Rows.Where(r => r.In(rebuilt)).ToList();
                foreach (LanePiece p in new[] { LanePiece.LaneStep, LanePiece.LaneKerb, LanePiece.GutterGrate, LanePiece.GatePier, LanePiece.GardenWallEnd, LanePiece.RetainButtress })
                    Assert.That(rows.Count(r => r.Piece == p), Is.GreaterThan(0), p + (rebuilt ? " rebuilt" : " ruined"));
                Assert.That(rows.Count(r => r.Piece == (rebuilt ? LanePiece.Gutter : LanePiece.GutterChoked)), Is.GreaterThan(0), "gutters");
                Assert.That(rows.Count(r => r.Piece == (rebuilt ? LanePiece.WallLantern : LanePiece.WallLanternBroken)), Is.GreaterThan(0), "lanterns");
                Assert.That(rows.Count(r => r.Piece == LanePiece.GardenWall || r.Piece == LanePiece.GardenWallBroken), Is.GreaterThan(0), "garden walls");
                Assert.That(rows.Count(r => r.IsRetaining), Is.GreaterThan(0), "retaining walls");
            }
            Assert.That(Rows.Count(r => r.Preset == LanePreset.Ruined && r.Piece == LanePiece.GardenWallBroken), Is.GreaterThan(0));
            Assert.That(Rows.Count(r => r.Piece == LanePiece.RetainWallBreach), Is.GreaterThan(0));
            Assert.AreEqual(0, Rows.Count(r => r.Preset == LanePreset.Rebuilt && (r.Piece == LanePiece.GardenWallBroken || r.Piece == LanePiece.RetainWallBreach || r.Piece == LanePiece.GutterChoked)));
        }

        [Test]
        public void Rows_StandOnTheGround()
        {
            foreach (LaneKitRow r in Rows)
            {
                float g = Ground(r);
                if (r.IsRetaining)
                {
                    var stack = Rows.Where(o => o.IsRetaining && Math.Abs(o.X - r.X) < 0.01f && Math.Abs(o.Z - r.Z) < 0.01f).ToList();   // tall steps are stacked modules
                    float lo = stack.Min(o => o.Y) - 1.3f, hi = stack.Max(o => o.Y + LaneKitPlan.Top(o.Piece == LanePiece.RetainWallBreach ? LanePiece.RetainWall : o.Piece)) + 0.3f;
                    Assert.That(g, Is.InRange(lo, hi), r.Id + " ground " + g + " vs y " + r.Y);
                }
                else if (r.Piece == LanePiece.RetainButtress) Assert.That(g, Is.InRange(r.Y - 1.3f, r.Y + 3.3f), r.Id);
                else if (r.Piece == LanePiece.WallLantern || r.Piece == LanePiece.WallLanternBroken) Assert.That(r.Y - g, Is.InRange(0.2f, 3.2f), r.Id + " mount height");
                else if (r.Piece == LanePiece.LaneStep) Assert.That(Math.Abs(r.Y - LaneLine(r)), Is.LessThanOrEqualTo(LaneKitPlan.StepVisible + 0.04f), r.Id + " steps are placed against the lane line, within a riser");
                else if (r.Piece == LanePiece.LaneKerb || r.Piece == LanePiece.Gutter || r.Piece == LanePiece.GutterChoked || r.Piece == LanePiece.GutterGrate)
                    Assert.That(Math.Abs(r.Y - LaneLine(r)), Is.LessThanOrEqualTo(0.25f), r.Id);
                else Assert.That(Math.Abs(r.Y - g), Is.LessThanOrEqualTo(0.25f), r.Id + " y " + r.Y + " ground " + g);
            }
        }

        [Test]
        public void Steps_RisersAreWalkable()
        {
            var steps = Rows.Where(r => r.Piece == LanePiece.LaneStep).ToList();
            Assert.That(steps.Count, Is.GreaterThan(20));
            foreach (LaneKitRow r in steps)
            {
                Assert.That(r.Rise, Is.LessThanOrEqualTo(0.2f), r.Id);
                Assert.That(r.Rise, Is.LessThan(MovementMath.StepOffset), r.Id);
                Assert.That(r.Rise, Is.GreaterThan(0.1f), r.Id);
            }
        }

        [Test]
        public void WallsGatesAndLanterns_StayClearOfLotsLandmarksSquareKeepAndTownWall()
        {
            foreach (LaneKitRow r in Rows.Where(IsWallLike))
            {
                TownPoint p = P(r);
                bool gardenLike = r.Piece != LanePiece.RetainWall && r.Piece != LanePiece.RetainWallLow && r.Piece != LanePiece.RetainWallBreach && r.Piece != LanePiece.RetainButtress && !r.Lane.StartsWith("Terrace") && !r.Lane.StartsWith("Lot:");
                foreach (Foundation f in T.Foundations) Assert.IsFalse(f.Footprint.Contains(p, gardenLike ? 1.0f : 0f), r.Id + " in lot " + f.Id);
                foreach (Landmark l in T.Landmarks) Assert.IsFalse(l.Footprint.Contains(p), r.Id + " in landmark " + l.Id);
                Assert.IsFalse(LaneKitPlan.InSquare(p), r.Id + " in the square");
                Assert.IsFalse(LaneKitPlan.InKeep(p), r.Id + " on the keep terrace");
            }
            foreach (LaneKitRow r in Rows)
                Assert.That(Math.Abs(HollowmereLayout.PlateauDistance(r.X, r.Z) - HollowmereLayout.TownWallOffset), Is.GreaterThanOrEqualTo(2f), r.Id + " near the town wall line");
        }

        [Test]
        public void NoWallRowCrossesAPathEdge()
        {
            var edges = LaneKitPlan.EdgeSegments(T);
            foreach (LaneKitRow r in Rows.Where(x => x.IsWall))
            {
                TownPoint c = P(r), d = Along(r) * r.ScaleX;
                Assert.IsFalse(LaneKitPlan.CrossesEdge(edges, c - d, c + d, 0f), r.Id + " crosses a path edge");
            }
        }

        [Test]
        public void OnlyInScopeLanes_AndNoMillOrSaltwake()
        {
            foreach (LaneKitRow r in Rows)
            {
                if (r.Lane.StartsWith("Terrace") || r.Lane.StartsWith("Lot:")) continue;
                Assert.Contains(r.Lane, LaneKitPlan.InScope, r.Id);
            }
        }
    }
}
