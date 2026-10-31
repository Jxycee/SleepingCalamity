// Authority: none (pure EditMode tests). The Hollowmere market and lane clutter data (Core/Town/HollowmereClutter, Q14): the plan is valid on the town layout, the path
// graph rule is real (negative tests), Rebuilt has every ground kind and Ruined its broken counterpart, the one Well keeps its spot, signs hang on the door-side face of
// the right building (never one whose art has its own Part_Sign), and washing lines run wall to wall with the lowest cloth clear of the ground.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class HollowmereClutterTests
    {
        static IEnumerable<ClutterRow> In(DecoPreset p) => HollowmereClutter.Rows.Where(r => r.Preset == p);

        [Test]
        public void Clutter_IsValidOnTheTownLayout()
        {
            List<string> errors = HollowmereClutter.Validate(HollowmereLayout.Create());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Rebuilt_HasEveryGroundKind_AndTheRuinedHasItsBrokenCounterpart()
        {
            var rebuilt = In(DecoPreset.Rebuilt).ToList();
            var ruined = In(DecoPreset.Ruined).ToList();
            foreach (string asset in HollowmereClutter.RebuiltAssets())
            {
                Assert.IsTrue(rebuilt.Any(r => r.Asset == asset), "Rebuilt has no " + asset);
                string broken = HollowmereClutter.RuinedAsset(asset);
                Assert.IsNotNull(broken, asset + " has no ruined counterpart");
                Assert.IsTrue(ruined.Any(r => r.Asset == broken), "Ruined has no " + broken);
            }
            Assert.IsTrue(ruined.All(r => r.Broken), "every Ruined row is a broken variant");
            Assert.IsTrue(rebuilt.All(r => !r.Broken), "no Rebuilt row is a broken variant");
        }

        [Test]
        public void Rebuilt_IsAtLeastThreeTimesAsDenseAsRuined()
        {
            int rb = In(DecoPreset.Rebuilt).Count(), ru = In(DecoPreset.Ruined).Count();
            Assert.GreaterOrEqual(rb, 3 * ru, "Rebuilt " + rb + " rows vs Ruined " + ru);
        }

        [Test]
        public void MarketKinds_HaveTheirWantedCounts()
        {
            var rebuilt = In(DecoPreset.Rebuilt).ToList();
            Assert.AreEqual(3, rebuilt.Count(r => r.Kind == ClutterKind.Handcart), "handcarts");
            int sacks = rebuilt.Count(r => r.Kind == ClutterKind.SackStack);
            Assert.That(sacks, Is.InRange(5, 6), "sack stacks");
        }

        [Test]
        public void OneWellPerPreset_AtTheSameSpot()
        {
            var well = In(DecoPreset.Rebuilt).Where(r => r.Kind == ClutterKind.Well).ToList();
            var ruin = In(DecoPreset.Ruined).Where(r => r.Kind == ClutterKind.Well).ToList();
            Assert.AreEqual(1, well.Count, "Rebuilt wells");
            Assert.AreEqual(1, ruin.Count, "Ruined wells");
            Assert.AreEqual(well[0].X, ruin[0].X, 1e-4f);
            Assert.AreEqual(well[0].Z, ruin[0].Z, 1e-4f);
            Assert.AreEqual("Well", well[0].Asset);
            Assert.AreEqual("Well_Ruined", ruin[0].Asset);
        }

        [Test]
        public void EveryRow_HasAnIdAnAreaAndAPlaceOnRing1Or2()
        {
            var ids = new HashSet<string>();
            foreach (ClutterRow r in HollowmereClutter.Rows)
            {
                Assert.IsTrue(ids.Add(r.Id), "duplicate id " + r.Id);
                Assert.IsFalse(string.IsNullOrEmpty(r.Asset), r.Id + " asset");
                Assert.Greater(r.SizeX * r.SizeZ, 0f, r.Id + " footprint area");
                Assert.IsTrue(r.Ring == 1 || r.Ring == 2, r.Id + " ring");
            }
        }

        [Test]
        public void Signs_HangOnTheDoorSideFaceOfTheRightBuilding()
        {
            TownLayout t = HollowmereLayout.Create();
            Assert.IsNotEmpty(HollowmereClutter.Signs);
            foreach (ClutterSign s in HollowmereClutter.Signs)
            {
                Foundation f = t.Foundation(s.Lot);
                Assert.IsNotNull(f, s.Id + " lot");
                HollowmereLayout.BuildingKind kind = HollowmereLayout.RebuiltBuilding(s.Lot);
                Assert.IsTrue(HollowmereClutter.TradeFits(s.Trade, kind, f.Ring), s.Id + ": " + s.Trade + " does not fit a " + kind);
                Assert.IsFalse(HollowmereClutter.HasOwnSignArt(kind), s.Id + ": " + kind + " art has its own Part_Sign");
                Assert.IsTrue(HollowmereClutter.BodySize(f, HollowmereLayout.RebuiltHome(s.Lot), out float w, out float d), s.Id + " has no body");
                TownPoint dir = HollowmereClutter.DoorDir(f);
                float along = (s.X - f.Centre.X) * dir.X + (s.Z - f.Centre.Z) * dir.Z;
                float across = (s.X - f.Centre.X) * dir.Z - (s.Z - f.Centre.Z) * dir.X;
                Assert.AreEqual(d * 0.5f, along, 0.05f, s.Id + " is not on the door-side face");
                Assert.Less(Math.Abs(across), w * 0.5f, s.Id + " is off the face");
                float yawErr = ((s.Yaw - HollowmereClutter.YawOf(dir)) % 360f + 540f) % 360f - 180f;
                Assert.AreEqual(0f, yawErr, 1f, s.Id + " arm does not point out from the wall");
                Assert.GreaterOrEqual(s.PivotHeight - HollowmereClutter.SignDrop, HollowmereClutter.MinSignBoard, s.Id + " board too low");
            }
        }

        [Test]
        public void Signs_CoverTheTradesWithoutTheirOwnSignArt()
        {
            var trades = HollowmereClutter.Signs.Select(s => s.Trade).ToList();
            foreach (string trade in HollowmereClutter.SignedTrades) CollectionAssert.Contains(trades, trade);
            CollectionAssert.DoesNotContain(trades, "Bakery");
            CollectionAssert.DoesNotContain(trades, "Inn");
            CollectionAssert.DoesNotContain(trades, "Smithy");
        }

        [Test]
        public void Validate_ReportsARowOnAPathNode()
        {
            TownLayout t = HollowmereLayout.Create();
            PathNode n = t.Node("SQ_0");
            var bad = new ClutterRow { Id = "X_node", Asset = "CrateStack", Kind = ClutterKind.CrateStack, Preset = DecoPreset.Rebuilt, Ring = 1, X = n.Pos.X, Z = n.Pos.Z, SizeX = 1.3f, SizeZ = 0.9f, Height = 1.4f };
            List<string> errors = HollowmereClutter.Validate(t, new[] { bad }, new ClutterSign[0], new WashingSpan[0]);
            Assert.IsTrue(errors.Any(e => e.Contains("path node")), string.Join("\n", errors));
        }

        [Test]
        public void Validate_ReportsARowAcrossAPathEdge()
        {
            TownLayout t = HollowmereLayout.Create();
            // the longest edge, at its midpoint, so no node is under the row: only the edge rule can fire
            PathEdge best = default; float len = 0f;
            foreach (PathEdge e in t.Edges)
            {
                PathNode a = t.Node(e.A), b = t.Node(e.B);
                float l = TownPoint.Distance(a.Pos, b.Pos);
                if (l > len) { len = l; best = e; }
            }
            Assert.IsNotNull(best.A);
            Assert.Greater(len, 6f, "the test needs an edge longer than the row");
            TownPoint mid = (t.Node(best.A).Pos + t.Node(best.B).Pos) * 0.5f;
            var bad = new ClutterRow { Id = "X_edge", Asset = "CrateStack", Kind = ClutterKind.CrateStack, Preset = DecoPreset.Rebuilt, Ring = 1, X = mid.X, Z = mid.Z, SizeX = 1.3f, SizeZ = 0.9f, Height = 1.4f };
            List<string> errors = HollowmereClutter.Validate(t, new[] { bad }, new ClutterSign[0], new WashingSpan[0]);
            Assert.IsTrue(errors.Any(e => e.Contains("blocks path edge")), string.Join("\n", errors));
        }

        [Test]
        public void EveryPlannedRow_KeepsClearOfTheWholePathGraph()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (ClutterRow r in HollowmereClutter.Rows)
            {
                var pad = new Footprint(new TownPoint(r.X, r.Z), r.SizeX + 2f * HollowmereClutter.Clearance, r.SizeZ + 2f * HollowmereClutter.Clearance, r.Yaw);
                foreach (PathNode n in t.Nodes) Assert.IsFalse(pad.Contains(new TownPoint(n.Pos.X, n.Pos.Z)), r.Id + " covers node " + n.Id);
                foreach (PathEdge e in t.Edges) Assert.IsFalse(pad.SegmentCrosses(t.Node(e.A).Pos, t.Node(e.B).Pos), r.Id + " blocks edge " + e.A + "-" + e.B);
            }
        }

        [Test]
        public void WashingLines_HaveBetweenThreeAndFiveSpansOfThreeToTenMetres()
        {
            Assert.That(HollowmereClutter.Spans.Count, Is.InRange(3, 5));
            foreach (WashingSpan w in HollowmereClutter.Spans) Assert.That(w.Length, Is.InRange(3f, 10f), w.Id + " length");
        }

        [Test]
        public void WashingLines_LowestClothIsAtLeastTwoMetresAboveTheGround()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (WashingSpan w in HollowmereClutter.Spans)
            {
                float ya = HollowmereLayout.Ground(t, w.AX, w.AZ) + w.AH, yb = HollowmereLayout.Ground(t, w.BX, w.BZ) + w.BH;
                for (int i = 0; i <= 40; i++)
                {
                    float s = i / 40f, x = w.AX + (w.BX - w.AX) * s, z = w.AZ + (w.BZ - w.AZ) * s;
                    float line = ya + (yb - ya) * s - 4f * w.Sag * s * (1f - s);
                    float lowestCloth = line - HollowmereClutter.ClothHang;
                    Assert.GreaterOrEqual(lowestCloth - HollowmereLayout.Ground(t, x, z), 2.0f, w.Id + " at " + s.ToString("0.00"));
                }
            }
        }
    }
}
