// Authority: none (pure EditMode tests). The Hollowmere square dressing data (Core/Town/HollowmereDressing): no decoration blocks the path graph, stands in a lot,
// a landmark or the approved corner, both presets are dressed, and the bunting spans are hung high and short enough.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class HollowmereDressingTests
    {
        static IEnumerable<DecoRow> In(DecoPreset p) => HollowmereDressing.Rows.Where(r => HollowmereDressing.InPreset(r.Preset, p));

        [Test]
        public void Dressing_IsValidOnTheTownLayout()
        {
            List<string> errors = HollowmereDressing.Validate(HollowmereLayout.Create());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Validate_ReportsADecorationOnAPathNode()
        {
            TownLayout t = HollowmereLayout.Create();
            PathNode n = t.Node("SQ_0");
            var bad = new DecoRow { Id = "X", Asset = "Bench_A", Batch = "Square", Preset = DecoPreset.Both, X = n.Pos.X, Z = n.Pos.Z, SizeX = 1f, SizeZ = 1f };
            List<string> errors = HollowmereDressing.Validate(t, new[] { bad }, new BuntingSpan[0]);
            Assert.IsTrue(errors.Any(e => e.Contains("path node")), string.Join("\n", errors));
        }

        [Test]
        public void EveryDecoration_HasAPositiveAreaFootprintAndUniqueId()
        {
            var ids = new HashSet<string>();
            foreach (DecoRow r in HollowmereDressing.Rows)
            {
                Footprint fp = HollowmereDressing.FootprintOf(r);
                Assert.Greater(fp.HalfX * fp.HalfZ, 0f, r.Id + " footprint area");
                Assert.IsTrue(ids.Add(r.Id), "duplicate id " + r.Id);
                Assert.IsFalse(string.IsNullOrEmpty(r.Asset), r.Id + " asset");
            }
            foreach (BuntingSpan b in HollowmereDressing.Bunting) Assert.IsTrue(ids.Add(b.Id), "duplicate id " + b.Id);
        }

        [Test]
        public void Bunting_EndsAreHighAndSpansShort()
        {
            foreach (BuntingSpan b in HollowmereDressing.Bunting)
            {
                Assert.GreaterOrEqual(b.AH, HollowmereDressing.MinBuntingHeight, b.Id + " end A");
                Assert.GreaterOrEqual(b.BH, HollowmereDressing.MinBuntingHeight, b.Id + " end B");
                Assert.LessOrEqual(b.Length, HollowmereDressing.MaxBuntingSpan, b.Id + " span");
                Assert.Greater(b.Sag, 0f, b.Id + " sag");
            }
        }

        [Test]
        public void Rebuilt_IsLivedIn()
        {
            Assert.IsTrue(In(DecoPreset.Rebuilt).Any(r => r.Kind == DecoKind.Bench && !r.Broken), "bench");
            Assert.IsTrue(In(DecoPreset.Rebuilt).Any(r => r.Kind == DecoKind.Planter && !r.Broken), "planter");
            Assert.IsTrue(In(DecoPreset.Rebuilt).Any(r => r.Kind == DecoKind.LanternPost && !r.Broken), "lantern post");
            Assert.IsTrue(In(DecoPreset.Rebuilt).Any(r => r.Kind == DecoKind.BannerPole), "banner pole");
            Assert.IsTrue(HollowmereDressing.Bunting.Any(b => HollowmereDressing.InPreset(b.Preset, DecoPreset.Rebuilt) && !b.Torn), "bunting span");
            Assert.IsFalse(In(DecoPreset.Rebuilt).Any(r => r.Broken), "nothing broken when rebuilt");
        }

        [Test]
        public void Ruined_IsSparseAndBroken()
        {
            Assert.IsTrue(In(DecoPreset.Ruined).Any(r => r.Broken), "at least one broken item");
            Assert.Less(In(DecoPreset.Ruined).Count(), In(DecoPreset.Rebuilt).Count(), "sparser than rebuilt");
            Assert.IsTrue(HollowmereDressing.Bunting.Any(b => HollowmereDressing.InPreset(b.Preset, DecoPreset.Ruined) && b.Torn), "torn bunting");
        }

        // ---- B4 spots (home door S1, bakery lot S3, market row S4)
        [Test]
        public void Spots_AreValidOnTheTownLayout()
        {
            List<string> errors = HollowmereDressing.ValidateSpots(HollowmereLayout.Create());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Spots_ValidateReportsASolidRowOnAPathNode()
        {
            TownLayout t = HollowmereLayout.Create();
            PathNode n = t.Node("SQ_0");
            var bad = new SpotRow { Id = "X", Piece = "Crate_A", Stage = HollowmereDressing.Always, X = n.Pos.X, Z = n.Pos.Z, SizeX = 1f, SizeZ = 1f };
            Assert.IsTrue(HollowmereDressing.ValidateSpots(t, new[] { bad }).Any(e => e.Contains("path node")));
        }

        [Test]
        public void Spots_CoverEveryStageOfTheBakeryAndTheMarketRow()
        {
            var s = HollowmereDressing.Spots;
            Assert.IsTrue(s.Any(r => r.Spot == "S3" && r.Stage == HollowmereDressing.StageRuined && r.Piece == "T05a"), "stage 0 roof tiles");
            Assert.AreEqual(12, s.Count(r => r.Piece == "T05a"), "twelve roof tiles");
            Assert.AreEqual(2, s.Count(r => r.Piece == "T05b"), "two rubble");
            Assert.AreEqual(4, s.Count(r => r.Piece == "T05d" && r.Anchor == "F02"), "four planks in the footing");
            Assert.AreEqual(2, s.Count(r => r.Piece == "Scaffold_Bay" && r.Stage == HollowmereDressing.StageFrame), "two scaffold bays (west and south)");
            Assert.IsTrue(s.Any(r => r.Piece == "Sack_A" && r.Stage == HollowmereDressing.StageFrame) && s.Any(r => r.Piece == "Sack_A" && r.Stage == HollowmereDressing.StageRebuilt), "flour sacks from stage 1 on");
            Assert.AreEqual(6, s.Count(r => r.Spot == "S3" && (r.Piece == "T08e" || r.Piece == "T08f")), "six loaves in stage 2");
            Assert.IsFalse(s.Any(r => r.Spot == "S3" && r.Stage == HollowmereDressing.StageRuined && r.Piece.StartsWith("Basket")), "no bread while ruined");
            for (int c = 0; c < 3; c++) { }
            Assert.AreEqual(3, new[] { "Back_Tools", "Inn_Wall", "Back_Cloth" }.Count(id => s.Count(r => r.Id.StartsWith(id + "_")) == 5), "three clusters of five");
        }

        [Test]
        public void Spots_HomeDoorSetIsThere()
        {
            var s = HollowmereDressing.Spots.Where(r => r.Spot == "S1").ToList();
            foreach (string piece in new[] { "Woodshed", "T06a", "T06b", "T16g", "Crate_A", "T16e", "FlowerBox", "T12c" }) Assert.IsTrue(s.Any(r => r.Piece == piece), "home door has " + piece);
            Assert.AreEqual(2, s.Count(r => r.Piece == "T06a"), "two log piles");
            Assert.AreEqual(6, s.Count(r => r.Piece == "T06b"), "six loose logs");
        }

        [Test]
        public void SpawnYaw_FacesTheFountainSideOfTheSquare()
        {
            TownLayout t = HollowmereLayout.Create();
            float yaw = HollowmereDressing.SpawnYaw(t);
            PathNode door = t.Nodes.First(n => n.Id == t.Landmarks.First(l => l.Id == "HeroesHome_Cistern").DoorNode);
            double toFountain = System.Math.Atan2(-door.Pos.X, -(door.Pos.Z - 3f)) * 180.0 / System.Math.PI;
            double diff = System.Math.Abs(((yaw - toFountain + 540.0) % 360.0) - 180.0);
            Assert.Less(diff, 25.0, "within 25 degrees of straight at the fountain (yaw " + yaw + ")");
            Assert.Less(yaw, -90f); Assert.Greater(yaw, -180f);   // west-south-west, never at the house
        }
    }
}
