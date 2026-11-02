// Catches: an Ambush spawn landing within 4 m of a player (playtest 12), the no-qualifying-cell fallback losing spawns or duplicating cells, the distance rule mis-measuring (metres from cell centres),
// and the gate key staying in a wall / off the walkable cells (playtest 18). The host re-arm of an empty ambush room, the wall line-of-sight checks and the hunter respawn delay are PlayMode / host code.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarSpawnSafetyTests
    {
        static bool IsFixed(CellarFloorLayout l, EncounterSpawn s) =>
            (s.Kind == EncounterKind.Authored && l.Carrier != null && l.Carrier.X == s.X && l.Carrier.Y == s.Y) || (l.WardenCell.HasValue && l.WardenCell.Value.X == s.X && l.WardenCell.Value.Y == s.Y);

        static List<(uint seed, int floor)> AmbushFloors(int max)
        {
            var list = new List<(uint, int)>();
            for (uint seed = 0; seed < 3000 && list.Count < max; seed++)
                for (int floor = 2; floor <= 3; floor++)
                    if (CellarFloorRoster.TwistOf(seed, floor) == FloorTwist.Ambush) list.Add((seed, floor));
            return list;
        }

        [Test]
        public void Distance_IsMetresFromTheCellCentre_AndEmptyMeansFar()
        {
            var c = new CellPos(3, 2);
            var at = new PlanePoint(CellarSpawnSafety.CellCenterX(3) + 3f, CellarSpawnSafety.CellCenterY(2) + 4f);
            Assert.AreEqual(5f, CellarSpawnSafety.NearestPlayer(c, new[] { at }), 1e-4f);
            Assert.IsTrue(CellarSpawnSafety.FarFromAll(c, new[] { at }, 5f));
            Assert.IsFalse(CellarSpawnSafety.FarFromAll(c, new[] { at }, 5.01f));
            Assert.IsTrue(CellarSpawnSafety.FarFromAll(c, new PlanePoint[0], 100f));
            Assert.IsTrue(CellarSpawnSafety.FarFromAll(c, null, 100f));
            Assert.IsFalse(CellarSpawnSafety.FarFromAll(c, new[] { new PlanePoint(1000f, 1000f), at }, 6f), "the nearest of several players counts");
        }

        [Test]
        public void Ambush_KeepsClearOfPlayers_WhenEnoughCellsQualify()
        {
            int checkedFloors = 0, movedClear = 0;
            foreach (var (seed, floor) in AmbushFloors(60))
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(l, d, 1, false);
                int room = plan.AmbushRoom; PlacedRoom p = l.Rooms[room];
                CellDoor door = p.Room.Doors[0];
                var player = new PlanePoint(CellarSpawnSafety.CellCenterX(p.X + door.X), CellarSpawnSafety.CellCenterY(p.Y + door.Y));   // the entering player stands in the door cell
                var players = new[] { player };
                var mine = new List<EncounterSpawn>();
                foreach (EncounterSpawn s in plan.Spawns) if (s.RoomIndex == room && s.Kind != EncounterKind.Hunter) mine.Add(s);
                int far = 0; foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(l, d, room)) if (CellarSpawnSafety.FarFromAll(c, players, CellarSpawnSafety.AmbushMinPlayerMeters)) far++;
                if (far < mine.Count) continue;
                CellPos[] cells = CellarAmbush.Cells(l, d, plan, room, players);
                Assert.AreEqual(mine.Count, cells.Length);
                for (int i = 0; i < cells.Length; i++)
                {
                    if (IsFixed(l, mine[i])) continue;
                    Assert.IsTrue(CellarSpawnSafety.FarFromAll(cells[i], players, CellarSpawnSafety.AmbushMinPlayerMeters), "seed " + seed + " floor " + floor + " spawn " + i + " at " + cells[i] + " is within 4 m of the player");
                    movedClear++;
                }
                CollectionAssert.AreEqual(cells, CellarAmbush.Cells(l, d, plan, room, players), "deterministic");
                checkedFloors++;
            }
            Assert.Greater(checkedFloors, 15); Assert.Greater(movedClear, 15);
        }

        [Test]
        public void Ambush_NoPlayers_IsTheOldResult()
        {
            foreach (var (seed, floor) in AmbushFloors(20))
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(l, d, 1, false);
                CollectionAssert.AreEqual(CellarAmbush.Cells(l, d, plan, plan.AmbushRoom), CellarAmbush.Cells(l, d, plan, plan.AmbushRoom, new PlanePoint[0]));
            }
        }

        [Test]
        public void Ambush_NoCellQualifies_FallsBackToTheFarthestCells_KeepingCountAndDistinctCells()
        {
            int floors = 0;
            foreach (var (seed, floor) in AmbushFloors(40))
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(l, d, 1, false);
                int room = plan.AmbushRoom; PlacedRoom p = l.Rooms[room];
                int count = 0, movableCount = 0;
                foreach (EncounterSpawn s in plan.Spawns) if (s.RoomIndex == room && s.Kind != EncounterKind.Hunter) { count++; if (!IsFixed(l, s)) movableCount++; }
                if (movableCount < 2) continue;
                bool[,] walk = CellarKeyDrop.Walkable(l, d);
                // players stand on every room cell except (movable - 1) walkable free cells: those few are 4 m clear (one short of the movable count, so no pool qualifies) while the rest are 0 m, so distances differ
                var leftOut = new HashSet<(int, int)>();
                var free = new List<CellPos>(CellarEncounterPlan.FreeCellsOf(l, d, room));
                for (int i = free.Count - 1; i >= 0 && leftOut.Count < movableCount - 1; i--) if (walk[free[i].X, free[i].Y]) leftOut.Add((free[i].X, free[i].Y));
                if (leftOut.Count < movableCount - 1) continue;
                var players = new List<PlanePoint>();
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++) if (!leftOut.Contains((p.X + x, p.Y + y))) players.Add(new PlanePoint(CellarSpawnSafety.CellCenterX(p.X + x), CellarSpawnSafety.CellCenterY(p.Y + y)));
                CellPos[] cells = CellarAmbush.Cells(l, d, plan, room, players);
                Assert.AreEqual(count, cells.Length);
                var seen = new HashSet<(int, int)>();
                foreach (CellPos c in cells) Assert.IsTrue(seen.Add((c.X, c.Y)), "distinct cells seed " + seed + " floor " + floor);
                // the chosen movable cells are the farthest from the players: no walkable, unfixed room cell that was left out is farther than a chosen one
                // (carrier/warden keep their authored cell and the plan-cell fallback ignores players, so they are not part of this ordering)
                float minChosen = float.PositiveInfinity, maxLeft = 0f; int moved = 0;
                var spawns = new List<EncounterSpawn>();
                foreach (EncounterSpawn s in plan.Spawns) if (s.RoomIndex == room && s.Kind != EncounterKind.Hunter) spawns.Add(s);
                var chosen = new HashSet<(int, int)>();
                for (int i = 0; i < spawns.Count; i++)
                {
                    bool carrier = spawns[i].Kind == EncounterKind.Authored && l.Carrier != null && l.Carrier.X == spawns[i].X && l.Carrier.Y == spawns[i].Y;
                    bool warden = l.WardenCell.HasValue && l.WardenCell.Value.X == spawns[i].X && l.WardenCell.Value.Y == spawns[i].Y;
                    if (carrier || warden) { chosen.Add((spawns[i].X, spawns[i].Y)); continue; }
                    chosen.Add((cells[i].X, cells[i].Y)); moved++;
                    minChosen = System.Math.Min(minChosen, CellarSpawnSafety.NearestPlayer(cells[i], players));
                }
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++)
                {
                    var cell = new CellPos(p.X + x, p.Y + y);
                    if (!walk[cell.X, cell.Y] || chosen.Contains((cell.X, cell.Y))) continue;
                    maxLeft = System.Math.Max(maxLeft, CellarSpawnSafety.NearestPlayer(cell, players));
                }
                Assert.Greater(moved, 0, "the fallback moved a spawn, seed " + seed + " floor " + floor);
                Assert.GreaterOrEqual(minChosen, maxLeft - 0.001f, "the fallback picks the farthest cells, seed " + seed + " floor " + floor);
                foreach (var lo in leftOut) Assert.IsTrue(chosen.Contains(lo), "the 4 m clear cell " + lo + " is chosen over the 0 m cells, seed " + seed + " floor " + floor);
                floors++;
            }
            Assert.Greater(floors, 5);
        }

        [Test]
        public void KeyDrop_WalkableCellStays_WallOrOutsideCellMovesToNearestWalkable_ForceSnapsToTheCell()
        {
            int tested = 0;
            for (uint seed = 1; seed < 200 && tested < 30; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, 1); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                bool[,] ok = CellarKeyDrop.Walkable(l, d);
                int n = CellarFloorLayout.GridSize; CellPos walk = default, wall = default; bool hw = false, hx = false;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { if (ok[x, y] && !hw) { walk = new CellPos(x, y); hw = true; } if (!ok[x, y] && !hx) { wall = new CellPos(x, y); hx = true; } }
                if (!hw || !hx) continue;
                CellPos same = CellarKeyDrop.Settle(l, d, walk, false, out bool moved);
                Assert.AreEqual(walk.X, same.X); Assert.AreEqual(walk.Y, same.Y); Assert.IsFalse(moved);
                same = CellarKeyDrop.Settle(l, d, walk, true, out moved);
                Assert.AreEqual(walk.X, same.X); Assert.AreEqual(walk.Y, same.Y); Assert.IsTrue(moved, "an exact spot inside a wall snaps to the cell centre");
                CellPos got = CellarKeyDrop.Settle(l, d, wall, false, out moved);
                Assert.IsTrue(moved); Assert.IsTrue(ok[got.X, got.Y], "seed " + seed + ": the key lands on a walkable cell");
                long best = long.MaxValue;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (ok[x, y]) best = System.Math.Min(best, (long)(x - wall.X) * (x - wall.X) + (long)(y - wall.Y) * (y - wall.Y));
                Assert.AreEqual(best, (long)(got.X - wall.X) * (got.X - wall.X) + (long)(got.Y - wall.Y) * (got.Y - wall.Y), "nearest walkable cell");
                CellPos off = CellarKeyDrop.Settle(l, d, new CellPos(-1, -1), false, out moved);
                Assert.IsTrue(moved); Assert.IsTrue(ok[off.X, off.Y]);
                tested++;
            }
            Assert.Greater(tested, 10);
        }
    }
}