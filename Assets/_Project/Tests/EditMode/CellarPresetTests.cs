// Authority: none (pure EditMode tests). Procgen item 2: layout presets (Spine / Ring / Hub / Wing) and the far dead-end stair exit (Core/Cellars: CellarLayoutPreset, CellarFloorGenerator, CellarFloorLayout.ExitIsLeaf).
// Catches: a preset that differs between peers or repeats on consecutive floors, a preset share that drifts from its weights, a stair exit that is not a leaf, sits next to the entry or is nearer than
// MinExitDistance, a Ring floor with no arc, Spine floors that are not longer than Hub floors, loop counts outside a preset's rule, a fallback rate that climbs, and a floor-4 hash that moved.
// Spread numbers are written to procgen_item2_stats.txt in the temp folder.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarPresetTests
    {
        const int Seeds = 2000;

        sealed class Sample { public uint Seed; public int Floor, Fallback; public CellarFloorLayout Layout; }

        static List<Sample> all;
        static List<Sample> All()
        {
            if (all != null) return all;
            var list = new List<Sample>();
            for (uint seed = 0; seed < Seeds; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor, out int fb);
                    list.Add(new Sample { Seed = seed, Floor = floor, Fallback = fb, Layout = l });
                }
            return all = list;
        }

        static void Stat(string text)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "procgen_item2_stats.txt"), text + "\n"); } catch (IOException) { }
            TestContext.Out.WriteLine(text);
        }

        static int Loops(CellarFloorLayout l) => l.Links.Count - (l.Rooms.Count - 1);

        [Test]
        public void Preset_IsPure_MatchesTheLayout_AndFloor4HasNone()
        {
            foreach (uint seed in new uint[] { 0, 1, 7, 12345, 99999, 0xC0FFEE12 })
            {
                for (int f = 3; f >= 1; f--) Assert.AreEqual(CellarLayoutPresets.PresetFor(seed, f), CellarLayoutPresets.PresetFor(seed, f), "seed " + seed);
                for (int f = 1; f <= 3; f++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, f, out int fb);
                    if (fb == 0) Assert.AreEqual(CellarLayoutPresets.PresetFor(seed, f), l.Preset, "seed " + seed + " floor " + f);
                    Assert.IsTrue(l.Preset.HasValue);
                }
                Assert.IsFalse(CellarFloorGenerator.Generate(seed, 4).Preset.HasValue);
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CellarLayoutPresets.PresetFor(1, 4));
        }

        [Test]
        public void Preset_Seed12345_Golden()
        {
            string got = string.Join(",", Enumerable.Range(1, 3).Select(f => CellarLayoutPresets.PresetFor(12345, f).ToString()));
            Assert.AreEqual("Hub,Ring,Spine", got, "actual preset pick for seed 12345 floors 1-3 = " + got);
        }

        [Test]
        public void Preset_NeverRepeatsOnConsecutiveFloors_AndSharesFollowTheWeights()
        {
            var count = new int[4]; int total = 0;
            for (uint seed = 0; seed < Seeds; seed++)
            {
                LayoutPreset prev = LayoutPreset.Spine;
                for (int f = 1; f <= 3; f++)
                {
                    LayoutPreset p = CellarLayoutPresets.PresetFor(seed, f);
                    if (f > 1) Assert.AreNotEqual(prev, p, "seed " + seed + " floor " + f);
                    prev = p; count[(int)p]++; total++;
                }
            }
            var sb = new StringBuilder("preset shares over " + Seeds + " seeds x floors 1-3 (weights " + string.Join("/", CellarLayoutPresets.Weights) + "):");
            for (int i = 0; i < 4; i++) sb.Append(" " + (LayoutPreset)i + " " + (100.0 * count[i] / total).ToString("F1") + "%");
            Stat(sb.ToString());
            // floor 1 follows the weights exactly; later floors drop the previous pick, which pulls the shares a little toward each other
            for (int i = 0; i < 4; i++) Assert.That(100.0 * count[i] / total, Is.InRange(15.0, 40.0), sb.ToString());
            Assert.Greater(count[(int)LayoutPreset.Spine], count[(int)LayoutPreset.Hub] * 0.9, sb.ToString());
        }

        [Test]
        public void Exit_IsAFarLeaf_NotNextToTheEntry_AndEveryFloorValidates()
        {
            int fb1 = 0, fb2 = 0, lastResortShort = 0;
            foreach (Sample s in All())
            {
                CellarFloorLayout l = s.Layout;
                Assert.IsEmpty(CellarFloorValidator.Validate(l), "seed " + s.Seed + " floor " + s.Floor + "\n" + l.ToAscii());
                Assert.IsTrue(l.ExitIsLeaf(), "exit is not a leaf: seed " + s.Seed + " floor " + s.Floor + "\n" + l.ToAscii());
                Assert.IsFalse(l.ExitAdjacentToEntry(), "exit next to the entry: seed " + s.Seed + " floor " + s.Floor + "\n" + l.ToAscii());
                int e = l.ExitRoomIndex(), en = l.Rooms.ToList().FindIndex(r => r.Room.Kind == RoomKind.Entry);
                Assert.IsFalse(l.Links.Any(k => k.Touches(e) && k.Touches(en)), "exit directly linked to the entry: seed " + s.Seed + " floor " + s.Floor);
                Assert.AreEqual(1, l.Links.Count(k => k.Touches(e)), "exit link count");
                if (l.LastResort) { if (l.EntryToExitDistance() < CellarFloorGenerator.MinExitDistance) lastResortShort++; }
                else Assert.GreaterOrEqual(l.EntryToExitDistance(), CellarFloorGenerator.MinExitDistance, "seed " + s.Seed + " floor " + s.Floor);
                if (s.Fallback == 1) fb1++; else if (s.Fallback == 2) fb2++;
            }
            double n = All().Count;
            Stat("fallback over " + n + " floors: retry " + fb1 + " (" + (100.0 * fb1 / n).ToString("F2") + "%), last resort " + fb2 + " (" + (100.0 * fb2 / n).ToString("F2") + "%), last resort with a short exit " + lastResortShort);
            Assert.Less(fb2 / n, 0.01, "fallback 2 must stay rare");
        }

        [Test]
        public void Presets_LoopCountsFollowTheirRules()
        {
            var hist = new Dictionary<string, int>();
            foreach (Sample s in All())
            {
                if (s.Fallback > 0) continue;   // a retry may have relaxed the preset to Spine
                int loops = Loops(s.Layout); LayoutPreset p = s.Layout.Preset.Value;
                string key = p + ":" + loops; hist[key] = hist.TryGetValue(key, out int c) ? c + 1 : 1;
                switch (p)
                {
                    case LayoutPreset.Spine: Assert.That(loops, Is.InRange(0, 1), "seed " + s.Seed + " floor " + s.Floor); break;
                    case LayoutPreset.Ring: Assert.That(loops, Is.InRange(1, 2), "seed " + s.Seed + " floor " + s.Floor); break;
                    case LayoutPreset.Hub: Assert.That(loops, Is.InRange(1, 2), "seed " + s.Seed + " floor " + s.Floor); break;
                    case LayoutPreset.Wing: Assert.AreEqual(1, loops, "seed " + s.Seed + " floor " + s.Floor); break;
                }
            }
            Stat("loops per preset: " + string.Join(" ", hist.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)));
        }

        // Wing: the one loop stays inside branch Y (rooms 1..m/2, m = the middle rooms, which are placed in index order) or the entry. With no Y (m == 1) the generator uses room 1 (branch X's
        // single room, the exit's parent) as the branch, so the allowed set is the entry plus room 1 and the loop still cannot join the two halves of the path.
        [Test]
        public void Wing_LoopEndpoints_StayInsideBranchY_OrTheEntry()
        {
            int wings = 0;
            foreach (Sample s in All())
            {
                CellarFloorLayout l = s.Layout;
                if (l.Preset != LayoutPreset.Wing || s.Fallback != 0) continue;
                wings++;
                int m = l.Rooms.Count - 2 - (l.HasLockedRoom ? 1 : 0), ny = m / 2, last = System.Math.Max(ny, 1);
                bool Inside(int r) => r >= 0 && r <= last;   // 0 is the entry
                var loops = l.Links.Where(k => k.RoomB < 0).ToList();
                string who = "seed " + s.Seed + " floor " + s.Floor + " m=" + m + "\n" + l.ToAscii();
                Assert.AreEqual(1, loops.Count, who);
                FloorLink loop = loops[0];
                Assert.IsTrue(Inside(loop.RoomA), "loop starts outside branch Y: room " + loop.RoomA + " " + who);
                CellPos end = loop.Path[loop.Path.Count - 1];
                // the cell it lands on belongs to a link between the allowed rooms only (a branch link, never the spine link of branch X)
                bool onBranchLink = l.Links.Any(k => !ReferenceEquals(k, loop) && Inside(k.RoomA) && Inside(k.RoomB) && k.Path.Any(c => c.X == end.X && c.Y == end.Y));
                Assert.IsTrue(onBranchLink, "loop lands outside branch Y: " + who);
            }
            Assert.Greater(wings, 50, "too few Wing floors to prove anything");
        }

        [Test]
        public void ExitAdjacentToEntry_FloodMatchesTheRoomGraph()
        {
            int adjacent = 0;
            foreach (Sample s in All())
            {
                CellarFloorLayout l = s.Layout;
                int e = l.ExitRoomIndex(), en = l.Rooms.ToList().FindIndex(r => r.Room.Kind == RoomKind.Entry);
                CellarRoomGraph g = CellarRoomGraph.Build(l); bool viaGraph = false;
                for (int d = 0; d < l.Rooms[e].Room.Doors.Count; d++) if (l.DoorIsOpen(e, d) && g.DoorNeighbours(e, d).Contains(en)) viaGraph = true;
                Assert.AreEqual(viaGraph, l.ExitAdjacentToEntry(), "seed " + s.Seed + " floor " + s.Floor);
                if (viaGraph) adjacent++;
            }
            Stat("generated floors with the exit next to the entry (must be 0): " + adjacent);
        }

        [Test]
        public void Ring_Floors_HaveAnArc_AtLeast95Percent()
        {
            int ring = 0, withArc = 0;
            foreach (Sample s in All())
            {
                if (s.Layout.Preset != LayoutPreset.Ring) continue;
                ring++;
                if (CellarFloorRoute.Resolve(s.Layout).Cycle != CycleRole.None) withArc++;
            }
            var other = new Dictionary<LayoutPreset, (int n, int arc)>();
            foreach (Sample s in All())
            {
                LayoutPreset p = s.Layout.Preset.Value; other.TryGetValue(p, out var v);
                other[p] = (v.n + 1, v.arc + (CellarFloorRoute.Resolve(s.Layout).Cycle != CycleRole.None ? 1 : 0));
            }
            Stat("route arc rate per preset: " + string.Join(" ", other.OrderBy(kv => (int)kv.Key).Select(kv => kv.Key + " " + (100.0 * kv.Value.arc / kv.Value.n).ToString("F1") + "% of " + kv.Value.n)));
            Assert.Greater(ring, 100);
            Assert.GreaterOrEqual(100.0 * withArc / ring, 95.0, "Ring floors with CycleRole != None: " + withArc + " of " + ring);
        }

        [Test]
        public void Spine_Floors_AreLongerThanHubFloors_OnAverage()
        {
            var sum = new double[4]; var n = new int[4];
            foreach (Sample s in All())
            {
                int p = (int)s.Layout.Preset.Value; sum[p] += s.Layout.EntryToExitDistance(); n[p]++;
            }
            var sb = new StringBuilder("mean entry-to-exit steps:");
            for (int i = 0; i < 4; i++) sb.Append(" " + (LayoutPreset)i + " " + (sum[i] / n[i]).ToString("F1"));
            Stat(sb.ToString());
            Assert.Greater(sum[(int)LayoutPreset.Spine] / n[(int)LayoutPreset.Spine], sum[(int)LayoutPreset.Hub] / n[(int)LayoutPreset.Hub], sb.ToString());
        }

        [Test]
        public void Spread_DistinctFootprintsAndRouteSequences()
        {
            var foot = new HashSet<ulong>(); var seqs = new HashSet<string>(); var seqNoPreset = new HashSet<string>(); int total = 0;
            foreach (Sample s in All())
            {
                total++; foot.Add(s.Layout.FootprintHash());
                CellarFloorRoute r = CellarFloorRoute.Resolve(s.Layout);
                string hops = string.Join(">", r.Spine.Where(i => i < s.Layout.Rooms.Count).Select(i => s.Layout.Rooms[i].RoomId));
                seqs.Add(PresetName(s.Layout) + "|" + hops); seqNoPreset.Add(hops);
            }
            Stat("spread over " + total + " floors: distinct FootprintHash " + foot.Count + " (" + (100.0 * foot.Count / total).ToString("F1") + "%), distinct (preset, spine room sequence) " + seqs.Count + " (" + (100.0 * seqs.Count / total).ToString("F1") + "%), distinct spine room sequence alone " + seqNoPreset.Count + " (" + (100.0 * seqNoPreset.Count / total).ToString("F1") + "%)");
            Assert.Greater(100.0 * foot.Count / total, 90.0);
        }

        static string PresetName(CellarFloorLayout l) => l.Preset.HasValue ? l.Preset.Value.ToString() : "none";

        [Test]
        public void Floor4_Hash_IsUnchanged_AndHasNoPreset()
        {
            Assert.AreEqual(8894238083671442472UL, CellarFloorGenerator.Generate(12345, 4).LayoutHash());
        }

        [Test]
        public void LayoutHash_IncludesThePreset()
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(12345, 1);
            var other = new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, l.Carrier,
                (LayoutPreset)(((int)l.Preset.Value + 1) % 4));
            Assert.AreNotEqual(l.LayoutHash(), other.LayoutHash());
        }
    }
}
