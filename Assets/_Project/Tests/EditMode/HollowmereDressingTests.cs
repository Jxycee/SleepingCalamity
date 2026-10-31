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
    }
}
