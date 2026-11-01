// Authority: none (pure EditMode tests). Procgen item 6 (floor-art follow-ups): the room-pool socket density in the bare rooms (6a), the wall-mounted and overhead trim (6b,
// Core/Cellars/CellarFloorTrim) and the secondary warm lights (6c, CellarFloorWarmLights). Over 1,000 seeds x floors 1-4: nothing on a door cell, a door's lane, a chest, the healing
// point, a vignette or a chunk solid cell; wall pieces start 1 m up and stay 0.4 m deep, overhead pieces hang above head height; light count per room stays under the cap;
// every layer is deterministic and pinned by a seed-12345 golden.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarTrimTests
    {
        const int Seeds = 1000;

        static IEnumerable<(CellarFloorLayout l, CellarFloorDressing d, CellarRoomChunks ch, CellarFloorClutter c, CellarFloorTrim t, CellarFloorWarmLights w)> All(int maxFloor = 4)
        {
            for (uint seed = 0; seed < Seeds; seed++)
                for (int floor = 1; floor <= maxFloor; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellarRoomChunks ch = CellarRoomChunks.Resolve(l, d); CellarFloorClutter c = CellarFloorClutter.Resolve(l, d, ch);
                    CellarFloorTrim t = CellarFloorTrim.Resolve(l, d, ch);
                    yield return (l, d, ch, c, t, CellarFloorWarmLights.Resolve(l, d, ch, c, t));
                }
        }

        // The cells the trim legality names: door cells, the cell in front of a door, chests, the healing point, vignettes, chunk solid cells.
        static HashSet<(int, int)> Banned(CellarFloorLayout l, CellarFloorDressing d, CellarRoomChunks ch)
        {
            var b = new HashSet<(int, int)>();
            foreach (FloorChest c in l.Chests) b.Add((c.X, c.Y));
            CellPos? heal = CellarHealing.PointFor(l, d); if (heal.HasValue) b.Add((heal.Value.X, heal.Value.Y));
            foreach (PlacedVignette v in d.Vignettes) b.Add((v.Cell.X, v.Cell.Y));
            foreach (ResolvedChunk c in ch.Chunks) if (!c.IsOpen) foreach (var (cell, _) in CellarRoomChunks.SolidCells(l, c)) b.Add((cell.X, cell.Y));
            foreach (PlacedRoom p in l.Rooms)
                foreach (CellDoor dr in p.Room.Doors)
                {
                    b.Add((p.X + dr.X, p.Y + dr.Y));
                    CellPos f = new CellPos(p.X + dr.X, p.Y + dr.Y).Step(CellarRoom.Opposite(dr.Side)); b.Add((f.X, f.Y));
                }
            return b;
        }

        static bool ClosedWall(PlacedRoom p, int wx, int wy, CellSide s)
        {
            int lx = wx - p.X, ly = wy - p.Y; var (dx, dy) = CellarRoom.Step(s); int nx = lx + dx, ny = ly + dy;
            if (p.Room.InBounds(nx, ny) && p.Room.CellAt(nx, ny) != CellKind.Solid) return false;
            return !p.Room.Doors.Any(dr => dr.X == lx && dr.Y == ly && dr.Side == s);
        }

        [Test]
        public void Trim_NeverOnDoorLaneChestHealVignetteOrChunkSolid_AndNeverReachesTheWalkingLane()
        {
            int wall = 0, over = 0;
            foreach (var (l, d, ch, c, t, w) in All())
            {
                var ban = Banned(l, d, ch);
                if (l.IsStub) Assert.AreEqual(0, t.Rows.Count, "floor 4 stub takes no trim");
                foreach (TrimRow r in t.Rows)
                {
                    string at = "seed " + l.Seed + " floor " + l.Floor + " " + r.Id;
                    PlacedRoom p = l.Rooms[r.RoomIndex];
                    Assert.AreNotEqual(RoomKind.Corridor, p.Room.Kind, at);
                    Assert.IsFalse(ban.Contains((r.CellX, r.CellY)), at + " on a banned cell " + r.CellX + "," + r.CellY);
                    Assert.AreEqual(CellKind.Floor, p.Room.CellAt(r.CellX - p.X, r.CellY - p.Y), at);
                    if (r.Layer == TrimLayer.Wall)
                    {
                        wall++;
                        Assert.IsTrue(ClosedWall(p, r.CellX, r.CellY, r.Wall), at + " has no closed wall on " + r.Wall);
                        Assert.GreaterOrEqual(r.BaseCm, CellarFloorTrim.WallMinBaseCm, at + " starts below 1 m");
                        Assert.LessOrEqual(r.DepthCm, CellarFloorTrim.WallMaxDepthCm, at + " stands too deep off the wall");
                        foreach (DressedSocket s in d.SocketsOf(r.RoomIndex))
                            if (s.Filled && s.Socket.WallMounted && s.Cell.X == r.CellX && s.Cell.Y == r.CellY) Assert.AreNotEqual(CellarRoom.Opposite(s.Facing), r.Wall, at + " shares a wall face with " + s.Socket.Id);
                    }
                    else
                    {
                        over++;
                        Assert.GreaterOrEqual(r.BaseCm, CellarFloorTrim.OverheadMinBaseCm, at + " hangs below head height");
                        foreach (DressedSocket s in d.SocketsOf(r.RoomIndex))
                            if (s.Filled && (s.Kind == SocketKind.Lantern || s.Kind == SocketKind.Roots)) Assert.IsFalse(s.Cell.X == r.CellX && s.Cell.Y == r.CellY, at + " shares a cell with an overhead socket");
                    }
                }
                var seen = new HashSet<(int, int, TrimLayer)>();
                foreach (TrimRow r in t.Rows) Assert.IsTrue(seen.Add((r.CellX, r.CellY, r.Layer)), "two " + r.Layer + " pieces in cell " + r.CellX + "," + r.CellY);
            }
            Assert.Greater(wall, 5000); Assert.Greater(over, 3000);
        }

        [Test]
        public void WarmLights_RespectTheRoomCapAndTheLegalityRules()
        {
            int total = 0, rooms = 0, withLight = 0;
            foreach (var (l, d, ch, c, t, w) in All())
            {
                var ban = Banned(l, d, ch);
                if (l.IsStub) Assert.AreEqual(0, w.Rows.Count);
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    int n = w.CountInRoom(r), existing = CellarFloorWarmLights.ExistingLights(l, d, r);
                    Assert.LessOrEqual(n, CellarFloorWarmLights.MaxPerRoom, "seed " + l.Seed + " floor " + l.Floor + " room " + r);
                    if (n > 0) Assert.LessOrEqual(existing + n, CellarFloorWarmLights.RoomLightCap, "seed " + l.Seed + " floor " + l.Floor + " room " + r + ": " + existing + " existing + " + n);
                    if (!l.IsStub && l.Rooms[r].Room.Kind != RoomKind.Corridor) { rooms++; if (n > 0) withLight++; }
                }
                foreach (WarmLightRow r in w.Rows)
                {
                    total++; string at = "seed " + l.Seed + " floor " + l.Floor + " " + r.Id; PlacedRoom p = l.Rooms[r.RoomIndex];
                    Assert.IsFalse(ban.Contains((r.CellX, r.CellY)), at + " on a banned cell");
                    Assert.IsTrue(ClosedWall(p, r.CellX, r.CellY, r.Wall), at + " has no closed wall");
                    foreach (TrimRow tr in t.Rows) Assert.IsFalse(tr.Layer == TrimLayer.Wall && tr.CellX == r.CellX && tr.CellY == r.CellY && tr.Wall == r.Wall, at + " shares a wall face with trim " + tr.Id);
                    foreach (DressedSocket s in d.SocketsOf(r.RoomIndex)) if (s.Filled && s.Kind == SocketKind.Lantern) Assert.IsFalse(s.Cell.X == r.CellX && s.Cell.Y == r.CellY, at + " under a lantern");
                    foreach (WarmLightRow o in w.Rows) if (o != r && o.RoomIndex == r.RoomIndex) Assert.GreaterOrEqual(Math.Abs(o.CellX - r.CellX) + Math.Abs(o.CellY - r.CellY), 2, at + " touches " + o.Id);
                }
            }
            Assert.Greater(total, 2000, "the rule places lights");
            Assert.Greater(withLight * 100 / rooms, 30, "a good share of rooms get a secondary light");
            TestContext.WriteLine("warm lights: " + total + " rows, " + withLight + " of " + rooms + " rooms lit");
        }

        [Test]
        public void TrimAndLights_AreDeterministic()
        {
            for (uint seed = 0; seed < 60; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    var a = One(seed, floor); var b = One(seed, floor);
                    Assert.AreEqual(a.t.TrimHash(), b.t.TrimHash()); Assert.AreEqual(a.w.LightsHash(), b.w.LightsHash());
                }
        }

        static (CellarFloorTrim t, CellarFloorWarmLights w) One(uint seed, int floor)
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
            CellarRoomChunks ch = CellarRoomChunks.Resolve(l, d); CellarFloorClutter c = CellarFloorClutter.Resolve(l, d, ch); CellarFloorTrim t = CellarFloorTrim.Resolve(l, d, ch);
            return (t, CellarFloorWarmLights.Resolve(l, d, ch, c, t));
        }

        // Pinned from the first run: a change to the trim or light rules changes them on purpose.
        const ulong GoldenTrim12345Floor1 = 12390903618228696707UL, GoldenTrim12345Floor2 = 8570854068830070284UL, GoldenTrim12345Floor3 = 16509569210543829933UL;
        const ulong GoldenLights12345Floor1 = 15025239194188478436UL, GoldenLights12345Floor2 = 17738642315203381894UL, GoldenLights12345Floor3 = 5281186032598449350UL;

        [Test]
        public void Golden_Seed12345_TrimAndLightHashes()
        {
            var f1 = One(12345, 1); var f2 = One(12345, 2); var f3 = One(12345, 3);
            Assert.That(f1.t.TrimHash() == GoldenTrim12345Floor1 && f2.t.TrimHash() == GoldenTrim12345Floor2 && f3.t.TrimHash() == GoldenTrim12345Floor3,
                "trim golden changed; actual floor 1 = " + f1.t.TrimHash() + "UL, floor 2 = " + f2.t.TrimHash() + "UL, floor 3 = " + f3.t.TrimHash() + "UL");
            Assert.That(f1.w.LightsHash() == GoldenLights12345Floor1 && f2.w.LightsHash() == GoldenLights12345Floor2 && f3.w.LightsHash() == GoldenLights12345Floor3,
                "lights golden changed; actual floor 1 = " + f1.w.LightsHash() + "UL, floor 2 = " + f2.w.LightsHash() + "UL, floor 3 = " + f3.w.LightsHash() + "UL");
        }

        // 6a/6b density (pool sockets: the free cells of the barest rooms are the piece-spot and clutter cells, so adding sockets there starved CellarPieceSpots and lowered cellar_entry; the density comes from trim instead): pieces per floor cell = filled non-lantern sockets + wall clutter (the floor pieces) [+ trim rows]. Before this item the pool's barest rooms read (floor pieces per
        // floor cell, 1,000 seeds x floors 1-3): wine_press_hall .394, wine_vault_l .449, coal_boiler_hall .476, cellar_entry .503, larder_pantry .539. The band is 0.55 and up for every room once trim counts.
        [Test]
        public void Density_EveryRoomReadsDressed_AfterThePoolSocketsAndTheTrim()
        {
            // per room id: sums of (floor pieces, wall trim, overhead trim) per floor cell, and the room count
            var acc = new Dictionary<string, (double floor, double wall, double over, int n)>();
            foreach (var (l, d, ch, c, t, w) in All(3))
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    PlacedRoom p = l.Rooms[r]; if (p.Room.Kind == RoomKind.Corridor) continue;
                    int fc = p.Room.FloorCount, pieces = d.SocketsOf(r).Count(s => s.Filled && s.Kind != SocketKind.Lantern) + c.CountInRoom(r);
                    int wallN = t.Rows.Count(x => x.RoomIndex == r && x.Layer == TrimLayer.Wall), overN = t.Rows.Count(x => x.RoomIndex == r && x.Layer == TrimLayer.Overhead);
                    acc.TryGetValue(p.Room.Id, out var a);
                    acc[p.Room.Id] = (a.floor + (double)pieces / fc, a.wall + (double)wallN / fc, a.over + (double)overN / fc, a.n + 1);
                }
            var sb = new StringBuilder("density per floor cell (room: floor pieces + wall trim + overhead = total)\n");
            var totals = new Dictionary<string, double>();
            foreach (var k in acc.Keys.OrderBy(x => x))
            {
                var v = acc[k]; double f = v.floor / v.n, wl = v.wall / v.n, ov = v.over / v.n; totals[k] = f + wl + ov;
                sb.AppendLine(k + ": " + f.ToString("F3") + " + " + wl.ToString("F3") + " + " + ov.ToString("F3") + " = " + (f + wl + ov).ToString("F3"));
            }
            TestContext.WriteLine(sb.ToString());
            foreach (var k in totals.Keys) Assert.GreaterOrEqual(totals[k], 0.55, "room " + k + " still reads bare\n" + sb);
        }
    }
}
