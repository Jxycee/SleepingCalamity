// Authority: none (pure EditMode tests). Procgen item 5: the enlarged greybox vignette pool and the state-conditioned odds (Core/Cellars/CellarVignette.cs, CellarVignetteTuning.cs).
// Catches: a pool outside 12-20, a vignette that is never shown or dominates (> 25% of placements), a repeat inside one dive, conditions that do not move the odds (or that read
// anything but seed and floor), a selection that depends on the order floors are asked in, a vignette that does not fit its rooms or lands on a chest, key, stair, heal point,
// door, chunk solid cell or clutter, and a vignette-layer golden that drifts. Spread numbers go to the test log.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarVignetteTests
    {
        const int Seeds = 4000;

        [Test]
        public void Pool_HasTwelveToTwenty_AllOncePerDive_UniqueIds_AndFamilies()
        {
            IReadOnlyList<CellarVignette> all = CellarVignettePool.All;
            Assert.That(all.Count, Is.InRange(12, 20));
            Assert.AreEqual(all.Count, all.Select(v => v.Id).Distinct().Count());
            foreach (CellarVignette v in all) { Assert.IsTrue(v.OncePerDive, v.Id + " must not repeat in a dive"); Assert.GreaterOrEqual(v.Props.Count, 3, v.Id); }
        }

        [Test]
        public void Select_NoRepeatInADive_MinFloorHonoured_AndOrderIndependent()
        {
            for (uint seed = 0; seed < 1500; seed++)
            {
                var seen = new HashSet<string>(); var forward = new List<string>();
                for (int f = 1; f <= 4; f++)
                {
                    foreach (CellarVignette v in VignetteDive.Select(seed, f).Chosen) { Assert.IsTrue(seen.Add(v.Id), v.Id + " repeated in dive " + seed); Assert.GreaterOrEqual(f, v.MinFloor); }
                    forward.Add(string.Join(",", VignetteDive.Select(seed, f).Chosen.Select(v => v.Id)));
                }
                for (int f = 4; f >= 1; f--) Assert.AreEqual(forward[f - 1], string.Join(",", VignetteDive.Select(seed, f).Chosen.Select(v => v.Id)), "order of asking, seed " + seed);
            }
        }

        [Test]
        public void Select_Spread_EveryVignetteAppears_NoneDominates()
        {
            var count = new Dictionary<string, int>(); int total = 0, bare = 0;
            for (uint seed = 0; seed < Seeds; seed++)
            {
                int dive = 0;
                for (int f = 1; f <= 3; f++) foreach (CellarVignette v in VignetteDive.Select(seed, f).Chosen) { count[v.Id] = count.GetValueOrDefault(v.Id) + 1; total++; dive++; }
                if (dive == 0) bare++;
            }
            TestContext.WriteLine("vignette spread over " + Seeds + " seeds x floors 1-3 (" + total + " placements, " + bare + " bare dives): " + string.Join(" ", CellarVignettePool.All.Select(v => v.Id + "=" + (100.0 * count.GetValueOrDefault(v.Id) / total).ToString("F1") + "%")));
            foreach (CellarVignette v in CellarVignettePool.All)
            {
                Assert.Greater(count.GetValueOrDefault(v.Id), 0, v.Id + " never shown");
                Assert.LessOrEqual(count.GetValueOrDefault(v.Id), total / 4, v.Id + " is more than 25% of placements");
            }
            Assert.AreEqual(0, bare, "bare dives");
        }

        [Test]
        public void Conditions_ShiftTheOdds_BySpecialKind_Depth_AndFamilySpacing()
        {
            int Cat(IEnumerable<CellarVignette> l, params VignetteCategory[] cs) => l.Count(v => cs.Contains(v.Category));
            int fluffQuietOn = 0, fluffQuietOff = 0, rewardTreasuryOn = 0, rewardTreasuryOff = 0, dangerDeepOn = 0, dangerDeepOff = 0, dangerShallowOn = 0, dangerShallowOff = 0, famOn = 0, famOff = 0;
            for (uint seed = 0; seed < Seeds; seed++)
            {
                IReadOnlyList<CellarVignette> prevOn = null, prevOff = null;
                for (int f = 1; f <= 3; f++)
                {
                    IReadOnlyList<CellarVignette> on = VignetteDive.Select(seed, f, true).Chosen, off = VignetteDive.Select(seed, f, false).Chosen;
                    SpecialKind k = SpecialRoomDive.KindFor(seed, f, out _);
                    if (k == SpecialKind.Quiet) { fluffQuietOn += Cat(on, VignetteCategory.Fluff); fluffQuietOff += Cat(off, VignetteCategory.Fluff); }
                    if (k == SpecialKind.Treasury) { rewardTreasuryOn += Cat(on, VignetteCategory.Reward, VignetteCategory.RiskReward); rewardTreasuryOff += Cat(off, VignetteCategory.Reward, VignetteCategory.RiskReward); }
                    if (f == 3) { dangerDeepOn += Cat(on, VignetteCategory.Danger); dangerDeepOff += Cat(off, VignetteCategory.Danger); }
                    if (f == 1) { dangerShallowOn += Cat(on, VignetteCategory.Danger); dangerShallowOff += Cat(off, VignetteCategory.Danger); }
                    if (prevOn != null) { famOn += on.Count(v => prevOn.Any(p => CellarVignetteTuning.FamilyOf(p.Id) == CellarVignetteTuning.FamilyOf(v.Id))); famOff += off.Count(v => prevOff.Any(p => CellarVignetteTuning.FamilyOf(p.Id) == CellarVignetteTuning.FamilyOf(v.Id))); }
                    prevOn = on; prevOff = off;
                }
            }
            TestContext.WriteLine("quiet-floor fluff " + fluffQuietOff + " -> " + fluffQuietOn + "; treasury-floor reward " + rewardTreasuryOff + " -> " + rewardTreasuryOn + "; floor-3 danger " + dangerDeepOff + " -> " + dangerDeepOn
                + "; floor-1 danger " + dangerShallowOff + " -> " + dangerShallowOn + "; back-to-back family repeats " + famOff + " -> " + famOn);
            Assert.Greater(fluffQuietOn, fluffQuietOff * 1.10, "a Quiet floor shows more fluff");
            Assert.Less(rewardTreasuryOn, rewardTreasuryOff * 0.90, "a Treasury floor shows fewer extra rewards");
            Assert.Greater(dangerDeepOn, dangerDeepOff * 1.05, "floor 3 shows more danger");
            Assert.Less(dangerShallowOn, dangerShallowOff * 0.95, "floor 1 shows less danger");
            Assert.Less(famOn, famOff * 0.90, "family spacing lowers back-to-back repeats");
        }

        [Test]
        public void Percent_DependsOnlyOnSeedFloorHistory_AndStaysInBounds()
        {
            var none = new HashSet<string>(); var cats = new int[4];
            foreach (CellarVignette v in CellarVignettePool.All)
                foreach (SpecialKind k in new[] { SpecialKind.None, SpecialKind.EliteDen, SpecialKind.Treasury, SpecialKind.Shrine, SpecialKind.Quiet })
                    for (int f = 1; f <= 4; f++)
                    {
                        int p = CellarVignetteTuning.Percent(v, f, k, cats, none);
                        Assert.That(p, Is.InRange(CellarVignetteTuning.MinPercent, CellarVignetteTuning.MaxPercent));
                        Assert.AreEqual(p, CellarVignetteTuning.Percent(v, f, k, cats, none));
                    }
            CellarVignette first = CellarVignettePool.All[0];
            Assert.Less(CellarVignetteTuning.Percent(first, 2, SpecialKind.None, cats, new HashSet<string> { CellarVignetteTuning.FamilyOf(first.Id) }), CellarVignetteTuning.Percent(first, 2, SpecialKind.None, cats, none));
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void Placement_EveryVignetteFitsItsRoomAndAvoidsReservedCells(int seeds)
        {
            var placed = new Dictionary<string, int>(); int dropped = 0, total = 0;
            for (uint seed = 0; seed < seeds; seed++)
                foreach (int f in CellarTestFloors.DiveFloors)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, f); CellarFloorDressing d = CellarTestFloors.Dressing(seed, f);
                    dropped += d.DroppedVignettes;
                    foreach (PlacedVignette v in d.Vignettes)
                    {
                        total++; placed[v.Vignette.Id] = placed.GetValueOrDefault(v.Vignette.Id) + 1;
                        PlacedRoom p = l.Rooms[v.RoomIndex]; int lx = v.Cell.X - p.X, ly = v.Cell.Y - p.Y;
                        Assert.AreEqual(RoomKind.Combat, p.Room.Kind);
                        Assert.AreEqual(CellKind.Floor, p.Room.CellAt(lx, ly));
                        Assert.IsFalse(p.Room.Doors.Any(dr => dr.X == lx && dr.Y == ly), v.Vignette.Id + " on a door");
                        Assert.IsFalse(p.Room.Chests.Any(c => c.X == lx && c.Y == ly) || p.Room.Features.Any(ft => ft.X == lx && ft.Y == ly), v.Vignette.Id + " on a chest/feature");
                        Assert.IsFalse(p.Room.Spawns.Any(s => s.X == lx && s.Y == ly), v.Vignette.Id + " on a spawn");
                    }
                }
            TestContext.WriteLine("placed " + total + ", dropped " + dropped + ": " + string.Join(" ", placed.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value)));
            if (seeds >= 1000) foreach (CellarVignette v in CellarVignettePool.All) Assert.Greater(placed.GetValueOrDefault(v.Id), 0, v.Id + " never placed");   // coverage of every vignette needs the full sweep
            Assert.LessOrEqual(dropped, Math.Max(3, seeds * 9 / 100), "chosen vignettes without a legal cell");
            foreach (var kv in placed) Assert.LessOrEqual(kv.Value, seeds >= 1000 ? total / 4 : total / 2, kv.Key + " dominates placements");
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(300, Category = "Soak")]
        public void Placement_ClearOfHealPoint_StairExit_AndEntry(int seeds)
        {
            for (uint seed = 0; seed < seeds; seed++)
                foreach (int f in CellarTestFloors.DiveFloors)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, f); CellarFloorDressing d = CellarTestFloors.Dressing(seed, f);
                    var heal = CellarHealing.PointFor(l, d);
                    foreach (PlacedVignette v in d.Vignettes)
                    {
                        if (heal.HasValue) Assert.IsFalse(heal.Value.X == v.Cell.X && heal.Value.Y == v.Cell.Y, "vignette on the heal point");
                        if (l.StairExit.HasValue) Assert.IsFalse(l.StairExit.Value.X == v.Cell.X && l.StairExit.Value.Y == v.Cell.Y, "vignette on the stair exit");
                        foreach (CellPos e in l.EntryStarts) Assert.IsFalse(e.X == v.Cell.X && e.Y == v.Cell.Y, "vignette on an entry start");
                    }
                }
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void Placement_NoVignetteOnChunkSolidCells(int seeds)
        {
            for (uint seed = 0; seed < seeds; seed++)
                foreach (int f in CellarTestFloors.DiveFloors)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, f); CellarFloorDressing d = CellarTestFloors.Dressing(seed, f);
                    if (d.Vignettes.Count == 0) continue;
                    var solid = new HashSet<(int, int)>();
                    foreach (ResolvedChunk c in CellarTestFloors.ChunkPlan(seed, f).Chunks)
                        foreach (var sc in CellarRoomChunks.SolidCells(l, c)) solid.Add((sc.cell.X, sc.cell.Y));
                    foreach (PlacedVignette v in d.Vignettes) Assert.IsFalse(solid.Contains((v.Cell.X, v.Cell.Y)), v.Vignette.Id + " on a chunk block/cover cell, seed " + seed + " floor " + f);
                }
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void Placement_NoVignetteOnClutterCells(int seeds)
        {
            for (uint seed = 0; seed < seeds; seed++)
                foreach (int f in CellarTestFloors.DiveFloors)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, f); CellarFloorDressing d = CellarTestFloors.Dressing(seed, f);
                    if (d.Vignettes.Count == 0) continue;
                    var clutter = new HashSet<(int, int)>();
                    foreach (CellarClutterRow r in CellarFloorClutter.Resolve(l, d).Rows) clutter.Add((r.CellX, r.CellY));
                    foreach (PlacedVignette v in d.Vignettes) Assert.IsFalse(clutter.Contains((v.Cell.X, v.Cell.Y)), v.Vignette.Id + " on a clutter cell, seed " + seed + " floor " + f);
                }
        }

        [Test]
        public void VignetteLayer_Golden_Seed12345()
        {
            var got = new ulong[8];
            foreach (int f in CellarTestFloors.DiveFloors)
            {
                var h = Fnv1a64.Create();
                foreach (PlacedVignette v in CellarFloorDressing.Resolve(CellarFloorGenerator.Layout(12345, f)).Vignettes) { h.Append(v.Vignette.Id); h.AppendInt32LE(v.RoomIndex); h.AppendInt32LE(v.Cell.X); h.AppendInt32LE(v.Cell.Y); h.AppendInt32LE(v.Turns); }
                got[f] = h.Value;
            }
            string all = got[1] + "UL, " + got[2] + "UL, " + got[4] + "UL, " + got[6] + "UL, " + got[7] + "UL";   // pinned by procgen 5 (vignettes), deliberate; re-pinned by rooms-v2 and by plan 3 task 3 (the dive floors are 1, 2, 4, 6, 7)
            Assert.AreEqual(1912106121001027624UL, got[1], "floor 1; actual " + all); Assert.AreEqual(16908555656275810401UL, got[2], "floor 2; actual " + all); Assert.AreEqual(3617152541879621164UL, got[4], "floor 4; actual " + all);
            Assert.AreEqual(1977110918901667070UL, got[6], "floor 6; actual " + all); Assert.AreEqual(14695981039346656037UL, got[7], "floor 7; actual " + all);
        }
    }
}
