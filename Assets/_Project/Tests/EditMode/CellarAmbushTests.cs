// Catches: the Ambush room's spring cells (CellarAmbush.Cells, procgen 3b) landing on a door / chest / vignette / heal / chunk-solid / clutter cell, a count that differs from the plan,
// two enemies on one cell, a non-deterministic pick, and the placeholder twist numbers drifting. The host spawn delay and the client view are PlayMode (not covered here).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarAmbushTests
    {
        static List<(uint seed, int floor)> AmbushFloors(int max)
        {
            var list = new List<(uint, int)>();
            for (uint seed = 0; seed < 3000 && list.Count < max; seed++)
                for (int floor = 2; floor <= 3; floor++)
                    if (CellarFloorRoster.TwistOf(seed, floor) == FloorTwist.Ambush) list.Add((seed, floor));
            return list;
        }

        [Test]
        public void Tuning_IsTheTwelvePercentPlaceholder_WithDelay()
        {
            Assert.AreEqual(12, FloorTwistTuning.AmbushPercent);
            Assert.AreEqual(0.75f, FloorTwistTuning.AmbushDelaySeconds);
        }

        [Test]
        public void AmbushCells_Legal_CountMatchesPlan_Distinct_Deterministic()
        {
            int floors = 0, moved = 0, strictFallbacks = 0;
            foreach (var (seed, floor) in AmbushFloors(60))
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(l, d, 1, false);
                Assert.AreEqual(FloorTwist.Ambush, plan.Twist);
                int room = plan.AmbushRoom;
                CellPos[] cells = CellarAmbush.Cells(l, d, plan, room);
                Assert.AreEqual(plan.CountIn(room), cells.Length, "count seed " + seed + " floor " + floor);
                CollectionAssert.AreEqual(cells, CellarAmbush.Cells(l, d, plan, room), "deterministic");

                var chunks = CellarRoomChunks.Resolve(l, d);
                var solid = new HashSet<(int, int)>();
                foreach (ResolvedChunk c in chunks.ChunksOf(room)) foreach (var sc in CellarRoomChunks.SolidCells(l, c)) solid.Add((sc.cell.X, sc.cell.Y));
                var clutter = new HashSet<(int, int)>();
                foreach (CellarClutterRow row in CellarFloorClutter.Resolve(l, d).Rows) if (row.RoomIndex == room) clutter.Add((row.CellX, row.CellY));
                var doors = new HashSet<(int, int)>(); var chests = new HashSet<(int, int)>();
                PlacedRoom p = l.Rooms[room];
                foreach (CellDoor dr in p.Room.Doors) doors.Add((p.X + dr.X, p.Y + dr.Y));
                foreach (FloorChest ch in l.Chests) chests.Add((ch.X, ch.Y));
                CellPos? heal = CellarHealing.PointFor(l, d);

                var seen = new HashSet<(int, int)>();
                var mine = new List<EncounterSpawn>();
                foreach (EncounterSpawn s in plan.Spawns) if (s.RoomIndex == room && s.Kind != EncounterKind.Hunter) mine.Add(s);
                for (int i = 0; i < cells.Length; i++)
                {
                    var key = (cells[i].X, cells[i].Y);
                    Assert.IsTrue(seen.Add(key), "distinct cells seed " + seed + " floor " + floor + " room " + room + " cell " + key + " plan " + string.Join(";", mine.ConvertAll(m => m.Kind + "@" + m.Cell)) + " out " + string.Join(";", System.Array.ConvertAll(cells, q => q.ToString())));
                    Assert.IsTrue(cells[i].X >= p.X && cells[i].X < p.X + p.Width && cells[i].Y >= p.Y && cells[i].Y < p.Y + p.Height, "inside the room");
                    Assert.AreEqual(CellKind.Floor, p.Room.CellAt(cells[i].X - p.X, cells[i].Y - p.Y), "floor");
                    Assert.IsFalse(doors.Contains(key), "door"); Assert.IsFalse(chests.Contains(key), "chest");
                    // A cell that is itself one of the room's plan spawn cells is the documented scarcity fallback (the room had too few strictly legal cells); every other cell is strict.
                    bool planCell = mine.Exists(m => m.X == cells[i].X && m.Y == cells[i].Y);
                    if (!planCell) { Assert.IsFalse(solid.Contains(key), "chunk solid"); Assert.IsFalse(clutter.Contains(key), "clutter"); }
                    else strictFallbacks++;
                    if (heal.HasValue) Assert.IsFalse(heal.Value.X == key.Item1 && heal.Value.Y == key.Item2, "heal");
                    foreach (PlacedVignette v in d.Vignettes) Assert.IsFalse(v.Cell.X == key.Item1 && v.Cell.Y == key.Item2, "vignette");
                    if (cells[i].X != mine[i].X || cells[i].Y != mine[i].Y) moved++;
                }
                floors++;
            }
            Assert.Greater(floors, 20);
            Assert.Greater(moved, 0, "ambush must move some spawns");
            TestContext.WriteLine("ambush cells that fell back to a plan cell: " + strictFallbacks);
        }

        [Test]
        public void AmbushCells_CarrierAndWardenKeepTheirCell()
        {
            foreach (var (seed, floor) in AmbushFloors(60))
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(l, d, 1, false);
                CellPos[] cells = CellarAmbush.Cells(l, d, plan, plan.AmbushRoom);
                int i = 0;
                foreach (EncounterSpawn s in plan.Spawns)
                {
                    if (s.RoomIndex != plan.AmbushRoom || s.Kind == EncounterKind.Hunter) continue;
                    bool carrier = s.Kind == EncounterKind.Authored && l.Carrier != null && l.Carrier.X == s.X && l.Carrier.Y == s.Y;
                    bool warden = l.WardenCell.HasValue && l.WardenCell.Value.X == s.X && l.WardenCell.Value.Y == s.Y;
                    if (carrier || warden) Assert.AreEqual((s.X, s.Y), (cells[i].X, cells[i].Y));
                    i++;
                }
            }
        }
    }
}
