// Authority: none (pure EditMode tests). P49 stir effects in Core: CellarEncounterPlan (extra enemies per room at stir 40, the rat-king placeholder at stir 70).
// Catches: a plan that differs between peers, a count that is not authored spawns + extra, an extra on a door, spawn, chest, socket, vignette, healing point or wall, a hunter in the
// locked room / unreachable / not the farthest, a plan that appears while the stir is low, a plan that moves the layout or dressing goldens (own Rng stream).
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarEncounterPlanTests
    {
        static CellarEncounterPlan Plan(uint seed, int floor, int extra, bool hunter, out CellarFloorLayout l)
        {
            l = CellarFloorGenerator.Generate(seed, floor);
            return CellarEncounterPlan.Build(l, CellarFloorDressing.Resolve(l), extra, hunter);
        }

        [Test]
        public void Plan_IsDeterministic()
        {
            for (uint seed = 0; seed < 40; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    var a = Plan(seed, floor, 2, true, out _); var b = Plan(seed, floor, 2, true, out _);
                    Assert.AreEqual(a.Spawns.Count, b.Spawns.Count);
                    for (int i = 0; i < a.Spawns.Count; i++)
                    {
                        Assert.AreEqual(a.Spawns[i].X, b.Spawns[i].X); Assert.AreEqual(a.Spawns[i].Y, b.Spawns[i].Y);
                        Assert.AreEqual(a.Spawns[i].Kind, b.Spawns[i].Kind);
                    }
                }
        }

        [Test]
        public void LowStir_IsJustTheAuthoredSpawns_AndNoHunter()
        {
            for (uint seed = 0; seed < 30; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    var p = Plan(seed, floor, 0, false, out CellarFloorLayout l);
                    Assert.IsNull(p.Hunter);
                    Assert.IsTrue(p.Spawns.All(s => s.Kind == EncounterKind.Authored));
                    int authored = l.Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Sum(r => r.Room.Spawns.Count);
                    Assert.AreEqual(authored, p.Spawns.Count, "seed " + seed + " floor " + floor);
                }
        }

        [Test]
        public void Counts_AreSpawnsPlusExtra_AndExtraCellsAreLegal()
        {
            int shortRooms = 0, rooms = 0;
            for (uint seed = 0; seed < 40; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    var p = Plan(seed, floor, 1, false, out CellarFloorLayout l);
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellPos? heal = CellarHealing.PointFor(l, d);
                    var seen = new System.Collections.Generic.HashSet<(int, int)>();
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        PlacedRoom pr = l.Rooms[r];
                        if (pr.Room.Kind != RoomKind.Combat) { Assert.AreEqual(0, p.CountIn(r)); continue; }
                        rooms++;
                        Assert.AreEqual(pr.Room.Spawns.Count, p.CountIn(r, EncounterKind.Authored));
                        int extra = p.CountIn(r, EncounterKind.Extra);
                        Assert.LessOrEqual(extra, 1);
                        if (extra == 0) shortRooms++;
                    }
                    foreach (EncounterSpawn s in p.Spawns)
                    {
                        Assert.IsTrue(seen.Add((s.X, s.Y)), "two enemies share a cell, seed " + seed + " floor " + floor);
                        if (s.Kind != EncounterKind.Extra) continue;
                        Assert.AreEqual(SpawnTier.Tier1, s.Tier);
                        PlacedRoom pr = l.Rooms[s.RoomIndex];
                        Assert.AreEqual(s.RoomIndex, l.RoomAt(s.X, s.Y));
                        int lx = s.X - pr.X, ly = s.Y - pr.Y;
                        Assert.AreEqual(CellKind.Floor, pr.Room.CellAt(lx, ly));
                        foreach (CellDoor dr in pr.Room.Doors) Assert.IsFalse(dr.X == lx && dr.Y == ly, "extra on a door");
                        foreach (FloorChest c in l.Chests) Assert.IsFalse(c.X == s.X && c.Y == s.Y, "extra on a chest");
                        foreach (PlacedVignette v in d.Vignettes) Assert.IsFalse(v.Cell.X == s.X && v.Cell.Y == s.Y, "extra on a vignette");
                        foreach (DressedSocket k in d.SocketsOf(s.RoomIndex).Where(q => q.Filled && !q.Socket.WallMounted)) Assert.IsFalse(k.Cell.X == s.X && k.Cell.Y == s.Y, "extra on a socket");
                        foreach (RoomFeature f in pr.Room.Features) Assert.IsFalse(f.X == lx && f.Y == ly, "extra on a feature");
                        if (heal.HasValue) Assert.IsFalse(heal.Value.X == s.X && heal.Value.Y == s.Y, "extra on the healing point");
                    }
                }
            Assert.Less(shortRooms, rooms / 4, "too many rooms found no free cell for an extra");
        }

        [Test]
        public void Hunter_IsTier2_ReachableFarAndNeverInTheLockedRoom()
        {
            int withHunter = 0;
            for (uint seed = 0; seed < 60; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    var p = Plan(seed, floor, 1, true, out CellarFloorLayout l);
                    var h = p.Hunter;
                    if (h == null) continue;
                    withHunter++;
                    Assert.AreEqual(SpawnTier.Tier2, h.Tier); Assert.AreEqual(EncounterKind.Hunter, h.Kind);
                    Assert.AreEqual(1, p.Spawns.Count(s => s.Kind == EncounterKind.Hunter));
                    Assert.AreNotEqual(l.LockedRoomIndex, h.RoomIndex);
                    Assert.AreEqual(RoomKind.Combat, l.Rooms[h.RoomIndex].Room.Kind);
                    int[,] dist = l.WalkDistances();
                    Assert.GreaterOrEqual(dist[h.X, h.Y], 0, "hunter cell unreachable");
                    Assert.AreEqual(1, p.Spawns.Count(s => s.X == h.X && s.Y == h.Y), "hunter shares a cell");
                    Assert.Greater(dist[h.X, h.Y], 0);
                }
            Assert.Greater(withHunter, 100);
        }

        [Test]
        public void Hunter_IsAtLeastAsFarAsEveryUntakenFreeCellOfAnEligibleRoom()
        {
            int checkedPlans = 0;
            for (uint seed = 0; seed < 40; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    var p = Plan(seed, floor, 1, true, out CellarFloorLayout l);
                    if (p.Hunter == null) continue;
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    int[,] dist = l.WalkDistances();
                    int hd = dist[p.Hunter.X, p.Hunter.Y];
                    var taken = new System.Collections.Generic.HashSet<(int, int)>(p.Spawns.Where(s => s.Kind != EncounterKind.Hunter).Select(s => (s.X, s.Y)));
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        if (r == l.LockedRoomIndex) continue;
                        foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(l, d, r))
                            if (!taken.Contains((c.X, c.Y)) && dist[c.X, c.Y] >= 0)
                                Assert.LessOrEqual(dist[c.X, c.Y], hd, "farther free cell, seed " + seed + " floor " + floor);
                    }
                    checkedPlans++;
                }
            Assert.Greater(checkedPlans, 50);
        }

        [Test]
        public void HugeExtraPerRoom_FillsEveryFreeCell_NoSharedCells()
        {
            for (uint seed = 0; seed < 20; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarEncounterPlan p = null; CellarFloorLayout l = null;
                    Assert.DoesNotThrow(() => p = Plan(seed, floor, 99, false, out l));
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    var seen = new System.Collections.Generic.HashSet<(int, int)>();
                    foreach (EncounterSpawn s in p.Spawns) Assert.IsTrue(seen.Add((s.X, s.Y)), "shared cell, seed " + seed + " floor " + floor);
                    for (int r = 0; r < l.Rooms.Count; r++)
                        Assert.AreEqual(CellarEncounterPlan.FreeCellsOf(l, d, r).Count, p.CountIn(r, EncounterKind.Extra), "room " + r + " seed " + seed + " floor " + floor);
                }
        }

        [Test]
        public void PlanDoesNotMoveGoldens()
        {
            for (uint seed = 0; seed < 10; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    ulong lh = l.LayoutHash(), dh = d.DressingHash();
                    CellarEncounterPlan.Build(l, d, 3, true);
                    Assert.AreEqual(lh, l.LayoutHash()); Assert.AreEqual(dh, CellarFloorDressing.Resolve(l).DressingHash());
                }
        }

        [Test]
        public void StirMeter_DrivesThePlanInputs()
        {
            var haul = new DiveHaul(new[] { 3, 3, 3 });
            var stir = new StirMeter(haul, StirTuning.Default);
            Assert.AreEqual(0, stir.ExtraEnemiesPerRoom); Assert.IsFalse(stir.HunterLatched);
            for (int f = 1; f <= 4; f++) stir.OnFloorCleared(f);   // 40
            Assert.AreEqual(1, stir.ExtraEnemiesPerRoom);
        }

        // procgen 3b: no plan spawn (authored, extra, hunter; with or without a twist) stands on a chunk solid cell.
        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void NoSpawn_StandsOnAChunkSolidCell(int seeds)
        {
            int checkedSpawns = 0;
            for (uint seed = 0; seed < seeds; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, floor); CellarFloorDressing d = CellarTestFloors.Dressing(seed, floor);
                    var solid = new System.Collections.Generic.HashSet<(int, int)>();
                    foreach (ResolvedChunk c in CellarTestFloors.ChunkPlan(seed, floor).Chunks) foreach (var sc in CellarRoomChunks.SolidCells(l, c)) solid.Add((sc.cell.X, sc.cell.Y));
                    foreach (CellarEncounterPlan plan in new[] { CellarEncounterPlan.Build(l, d, 2, true), CellarEncounterPlan.BuildForDive(l, d, 1, true) })
                        foreach (EncounterSpawn s in plan.Spawns)
                        {
                            checkedSpawns++;
                            Assert.IsFalse(solid.Contains((s.X, s.Y)), "spawn on a chunk solid: seed " + seed + " floor " + floor + " " + s.Kind + " @" + s.Cell);
                        }
                }
            Assert.Greater(checkedSpawns, seeds >= 1000 ? 1000 : seeds);
        }
    }
}
