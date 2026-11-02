// Authority: none (pure EditMode tests). Procgen item 4: swappable chunk regions in room grids (Core/Cellars: CellarChunks, CellarRoomChunks, regions in CellarRoomPool) and their greybox.
// Catches: a chunk pick that differs between peers or depends on call order, a golden that drifts, a pick that cuts a room in two or lands on a door, spawn, chest, feature, dressed
// socket or vignette, a region whose variants stop turning with the room, a variant that is overused, the resolver moving LayoutHash or DressingHash, and a builder that draws pieces
// the Core data does not have. Spread numbers are written to procgen_stats.txt in the temp folder.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarChunkTests
    {

        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        [Test]
        public void Regions_AreAuthoredOnSixCombatRooms_AndPoolIsValid()
        {
            Assert.IsEmpty(CellarRoomPool.Validate());
            var withRegions = CellarRoomPool.All.Where(r => r.Regions.Count > 0).ToList();
            Assert.AreEqual(6, withRegions.Count);
            Assert.AreEqual(8, withRegions.Sum(r => r.Regions.Count));
            Assert.IsTrue(withRegions.All(r => r.Kind == RoomKind.Combat));
            foreach (CellarRoom r in withRegions)
                foreach (ChunkRegion g in r.Regions)
                {
                    Assert.GreaterOrEqual(g.Variants.Count, 3); Assert.LessOrEqual(g.Variants.Count, 8);
                    Assert.IsTrue(g.Variants[g.OpenIndex].IsOpen, g.Id);
                    Assert.AreEqual(0, g.OpenIndex, g.Id + ": variant 0 is the open one");
                }
        }

        [Test]
        public void Regions_TurnWithTheRoom()
        {
            foreach (CellarRoom room in CellarRoomPool.All.Where(r => r.Regions.Count > 0))
            {
                for (int q = 1; q < 4; q++)
                {
                    CellarRoom t = room.Rotated(q);
                    Assert.IsEmpty(t.Validate(), room.Id + " turned " + q);
                    Assert.AreEqual(room.Regions.Count, t.Regions.Count);
                }
                CellarRoom back = room.Rotated(1).Rotated(1).Rotated(1).Rotated(1);   // four turns give the room back
                for (int i = 0; i < room.Regions.Count; i++)
                {
                    ChunkRegion a = room.Regions[i], b = back.Regions[i];
                    Assert.AreEqual((a.X, a.Y, a.Width, a.Height), (b.X, b.Y, b.Width, b.Height), room.Id);
                    for (int v = 0; v < a.Variants.Count; v++)
                        for (int x = 0; x < a.Width; x++) for (int y = 0; y < a.Height; y++) Assert.AreEqual(a.Variants[v].At(x, y), b.Variants[v].At(x, y), room.Id + " variant " + v);
                }
            }
            // an end cell stays an end cell: the west piece of a horizontal domino is the north piece once turned clockwise
            ChunkRegion h = ChunkRegion.Author("t", ChunkKind.Casks, 0, 0, 2, 1); ChunkRegion vert = h.Rotated(1, (x, y) => (0 - y, x));
            Assert.AreEqual((1, 2), (vert.Width, vert.Height));
            Assert.AreEqual(ChunkCell.Block, vert.Variants[1].At(0, 0), "StackWest turned clockwise: the blocked end is north");
            Assert.AreEqual(ChunkCell.Open, vert.Variants[1].At(0, 1));
        }

        [Test]
        public void Chunks_SameSeedTwice_AndCallOrderDoesNotMatter()
        {
            foreach (uint seed in new uint[] { 0, 1, 7, 12345, 99999, 0xC0FFEE12 })
            {
                var fwd = new ulong[4]; var back = new ulong[4];
                for (int f = 1; f <= 4; f++) fwd[f - 1] = Hash(seed, f);
                for (int f = 4; f >= 1; f--) back[f - 1] = Hash(seed, f);
                for (int f = 0; f < 4; f++) { Assert.AreEqual(fwd[f], back[f], "seed " + seed + " floor " + (f + 1)); Assert.AreEqual(fwd[f], Hash(seed, f + 1)); }
            }
        }

        static ulong Hash(uint seed, int floor) => CellarRoomChunks.Resolve(CellarFloorGenerator.Generate(seed, floor)).ChunksHash();

        [Test]
        public void Chunks_GoldenHashes_Seed12345()
        {
            // re-pinned after item 2 layout presets (deliberate: the seed-12345 layout moved); floor 4 has none
            ulong[] expect = { 15269764869822355049UL, 1827858871628724571UL, 14701427894773662475UL, 3232700585171816769UL };   // floors 1-4
            var sb = new StringBuilder();
            for (int f = 1; f <= 4; f++) sb.Append("floor " + f + ": " + Hash(12345, f) + "UL\n");
            for (int f = 1; f <= 4; f++) Assert.AreEqual(expect[f - 1], Hash(12345, f), "floor " + f + "\n" + sb);
        }

        [Test]
        public void Chunks_ResolveIsReadOnly_LayoutAndDressingHashesStayPut()
        {
            // Proves only that Resolve does not mutate the layout or dressing it reads (the pinned LayoutHash / DressingHash goldens live in CellarFloorGeneratorTests / CellarVarietyTests).
            foreach (uint seed in new uint[] { 12345, 7, 99 })
                for (int f = 1; f <= 4; f++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, f); CellarFloorDressing d0 = CellarFloorDressing.Resolve(l);
                    ulong lh = l.LayoutHash(), dh = d0.DressingHash();
                    CellarRoomChunks.Resolve(l, d0);
                    Assert.AreEqual(lh, l.LayoutHash()); Assert.AreEqual(dh, d0.DressingHash());
                }
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void Chunks_NeverCoverTheHealPoint(int seeds)
        {
            int withHeal = 0;
            for (int f = 1; f <= 3; f++)
                for (uint seed = 1; seed <= seeds; seed++)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, f); CellarFloorDressing d = CellarTestFloors.Dressing(seed, f);
                    CellPos? heal = CellarHealing.PointFor(l, d);
                    if (!heal.HasValue) continue;
                    withHeal++;
                    CellarRoomChunks c = CellarTestFloors.ChunkPlan(seed, f);
                    foreach (ResolvedChunk ch in c.Chunks)
                        foreach (var (cell, kind) in CellarRoomChunks.SolidCells(l, ch))
                            Assert.IsFalse(cell.X == heal.Value.X && cell.Y == heal.Value.Y, "seed " + seed + " floor " + f + ": " + ch.Region.Id + " " + kind + " sits on the heal point");
                }
            Assert.Greater(withHeal, seeds >= 1000 ? 500 : seeds / 4);
        }

        [Test]
        public void Chunks_Floor4HasNone()
        {
            for (uint seed = 1; seed <= 20; seed++) Assert.AreEqual(0, CellarRoomChunks.Resolve(CellarFloorGenerator.Generate(seed, 4)).Chunks.Count);
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void Chunks_Spread_Connectivity_AndNoOverlap(int seeds)
        {
            var variantCounts = new Dictionary<string, int[]>();            // "roomId|regionId" -> picks per variant
            var combos = new Dictionary<string, Dictionary<string, int>>(); // roomId -> chunk combination -> visits
            var why = new Dictionary<string, int[]>(); int visits = 0, regionPicks = 0, nonOpen = 0, skippedPicks = 0, roomsWithRegions = 0;
            for (int f = 1; f <= 3; f++)
                for (uint seed = 1; seed <= seeds; seed++)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, f); CellarFloorDressing d = CellarTestFloors.Dressing(seed, f); CellarRoomChunks c = CellarTestFloors.ChunkPlan(seed, f);
                    ulong dh = d.DressingHash();
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        PlacedRoom p = l.Rooms[r]; CellarRoom room = p.Room;
                        var mine = c.ChunksOf(r).ToList();
                        Assert.AreEqual(room.Regions.Count, mine.Count, "a pick per region");
                        if (mine.Count == 0) continue;
                        roomsWithRegions++;
                        var blocked = new bool[room.Width, room.Height];
                        foreach (DressedSocket s in d.SocketsOf(r)) if (s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate)) blocked[s.Cell.X - p.X, s.Cell.Y - p.Y] = true;
                        foreach (PlacedVignette v in d.Vignettes) if (v.RoomIndex == r) blocked[v.Cell.X - p.X, v.Cell.Y - p.Y] = true;
                        var marks = new HashSet<(int, int)>();
                        foreach (CellDoor dr in room.Doors) marks.Add((dr.X, dr.Y));
                        foreach (CellarSpawn s in room.Spawns) marks.Add((s.X, s.Y));
                        foreach (ChestSlot s in room.Chests) marks.Add((s.X, s.Y));
                        foreach (RoomFeature s in room.Features) marks.Add((s.X, s.Y));
                        foreach (DressedSocket s in d.SocketsOf(r)) if (s.Filled) marks.Add((s.Cell.X - p.X, s.Cell.Y - p.Y));
                        foreach (PlacedVignette v in d.Vignettes) if (v.RoomIndex == r) marks.Add((v.Cell.X - p.X, v.Cell.Y - p.Y));
                        foreach (ResolvedChunk ch in mine)
                        {
                            regionPicks++; skippedPicks += ch.Skipped > 0 ? 1 : 0; if (!ch.IsOpen) nonOpen++;
                            string key = room.Id + "|" + ch.Region.Id;
                            if (!variantCounts.TryGetValue(key, out int[] arr)) variantCounts[key] = arr = new int[ch.Region.Variants.Count];
                            arr[ch.VariantIndex]++;
                            if (!why.TryGetValue(key, out int[] w)) why[key] = w = new int[2];
                            w[0] += ch.SkippedTaken; w[1] += ch.SkippedCut;
                            foreach (var (cell, kind) in CellarRoomChunks.SolidCells(l, ch))
                            {
                                int lx = cell.X - p.X, ly = cell.Y - p.Y;
                                Assert.IsFalse(marks.Contains((lx, ly)), "seed " + seed + " floor " + f + " room " + r + " " + ch.Region.Id + " covers a door, spawn, chest, feature, filled socket or vignette at (" + lx + "," + ly + ")");
                                blocked[lx, ly] = true;
                            }
                        }
                        // connectivity: every unblocked floor cell and every door reachable from the first door
                        CellDoor start = room.Doors.First(dr => !blocked[dr.X, dr.Y]);
                        var seen = new bool[room.Width, room.Height]; var q = new Queue<(int, int)>(); seen[start.X, start.Y] = true; q.Enqueue((start.X, start.Y));
                        while (q.Count > 0)
                        {
                            var (cx, cy) = q.Dequeue();
                            foreach (CellSide sd in Sides)
                            {
                                var (dx, dy) = CellarRoom.Step(sd); int nx = cx + dx, ny = cy + dy;
                                if (room.InBounds(nx, ny) && !seen[nx, ny] && !blocked[nx, ny] && room.CellAt(nx, ny) == CellKind.Floor) { seen[nx, ny] = true; q.Enqueue((nx, ny)); }
                            }
                        }
                        for (int x = 0; x < room.Width; x++) for (int y = 0; y < room.Height; y++)
                            if (room.CellAt(x, y) == CellKind.Floor && !blocked[x, y]) Assert.IsTrue(seen[x, y], "seed " + seed + " floor " + f + " room " + r + " (" + room.Id + "): cell (" + x + "," + y + ") cut off");
                        foreach (CellDoor dr in room.Doors) Assert.IsTrue(seen[dr.X, dr.Y], "door cut off");
                        foreach (CellarSpawn s in room.Spawns) Assert.IsTrue(seen[s.X, s.Y], "spawn cut off");
                        foreach (ChestSlot s in room.Chests) Assert.IsTrue(seen[s.X, s.Y], "chest cut off");
                        foreach (RoomFeature s in room.Features) Assert.IsTrue(seen[s.X, s.Y], "feature cut off");
                        visits++;
                        string combo = string.Join(",", mine.Select(m => m.VariantIndex));
                        if (!combos.TryGetValue(room.Id, out var cm)) combos[room.Id] = cm = new Dictionary<string, int>();
                        cm[combo] = (cm.TryGetValue(combo, out int n) ? n : 0) + 1;
                    }
                    Assert.AreEqual(dh, CellarFloorDressing.Resolve(l).DressingHash());
                }

            var sb = new StringBuilder();
            sb.Append("chunks: " + seeds + " seeds x floors 1-3: " + visits + " room visits with regions, " + regionPicks + " region picks, " + (100.0 * nonOpen / regionPicks).ToString("0.0") + "% non-open, "
                + (100.0 * skippedPicks / regionPicks).ToString("0.0") + "% picks had to skip a variant\n");
            double worst = 0; var over = new List<string>();
            foreach (var kv in variantCounts.OrderBy(k => k.Key, System.StringComparer.Ordinal))
            {
                int total = kv.Value.Sum(), max = kv.Value.Max(); double share = 100.0 * max / total; worst = System.Math.Max(worst, share);
                sb.Append("  " + kv.Key + ": n=" + total + " max variant " + share.ToString("0.0") + "% [" + string.Join(" ", kv.Value) + "] skips: socket/vignette " + why[kv.Key][0] + ", cut " + why[kv.Key][1] + "\n");
                if (total >= 200 && share > 60.0) over.Add(kv.Key + " " + string.Join(" ", kv.Value));
            }
            int different = 0, all = 0;
            foreach (var kv in combos)
            {
                int modal = kv.Value.Values.Max(), tot = kv.Value.Values.Sum();
                different += tot - modal; all += tot;
                sb.Append("  room " + kv.Key + ": " + kv.Value.Count + " combinations, modal " + (100.0 * modal / tot).ToString("0.0") + "% of " + tot + " visits\n");
            }
            sb.Append("chunks: " + (100.0 * different / all).ToString("0.0") + "% of room visits with regions look different from the room's most common chunk combination; worst variant share " + worst.ToString("0.0") + "%");
            Report(sb.ToString());
            Assert.IsEmpty(over, "variants above 60%");
            Assert.Greater(100.0 * different / all, seeds >= 1000 ? 40.0 : 20.0);   // statistical: lower bar for the fast twin
        }

        [Test]
        public void Builder_DrawsOnePiecePerSolidCell()
        {
            foreach (var (seed, floor) in new (uint, int)[] { (12345, 1), (12345, 2), (12345, 3), (7, 2), (99, 3), (4242, 1), (12345, 4) })
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                try
                {
                    CellarRoomChunks c = CellarRoomChunks.Resolve(l);
                    int cells = c.Chunks.Sum(ch => CellarRoomChunks.SolidCells(l, ch).Count());
                    Assert.AreEqual(cells, b.ChunkPieces, "seed " + seed + " floor " + floor);
                    Assert.AreEqual(c.ChunksHash(), b.Chunks.ChunksHash());
                    Transform sub = b.Root.transform.Find(CellarFloorBuilder.ProcgenGroup + "/Chunks");
                    if (cells > 0)
                    {
                        Assert.IsNotNull(sub); Assert.AreEqual(cells, sub.childCount);
                        Assert.IsTrue(Enumerable.Range(0, sub.childCount).All(i => sub.GetChild(i).name.StartsWith("Chunk_r")));
                    }
                }
                finally { Object.DestroyImmediate(b.Root.gameObject); }
            }
        }

        static void Report(string text)
        {
            Debug.Log(text);
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "procgen_stats.txt"), text + "\n"); } catch (IOException) { }
        }
    }
}
