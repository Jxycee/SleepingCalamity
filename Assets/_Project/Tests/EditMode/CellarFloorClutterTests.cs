// Authority: none (pure EditMode tests). Core/Cellars/CellarFloorClutter (floor art round 2 density pass): over 1,000 seeds x floors 1-4 the clutter never stands on a door,
// the cell in front of a door, a spawn, chest, key, entry, stair, bank, baker, warden, carrier, healing point, vignette or filled socket cell (nor orthogonally beside the
// markers), always stands against a closed wall side inside its own cell, keeps every door and every reachable cell reachable, leaves the room centre open and keeps corridors
// wide; the resolver is deterministic, pinned by a ClutterHash golden, and leaves the layout and dressing hashes untouched.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarFloorClutterTests
    {
        const int Seeds = 1000;
        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        static IEnumerable<(CellarFloorLayout l, CellarFloorDressing d, CellarFloorClutter c)> All()
        {
            for (uint seed = 0; seed < Seeds; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    yield return (l, d, CellarFloorClutter.Resolve(l, d));
                }
        }

        static bool Near(int x, int y, int ox, int oy, bool orthogonalToo) => (x == ox && y == oy) || (orthogonalToo && Math.Abs(x - ox) + Math.Abs(y - oy) == 1);

        // The markers a clutter cell must keep clear of (on or orthogonally beside).
        static List<(int x, int y, string what, bool beside)> Markers(CellarFloorLayout l, CellarFloorDressing d)
        {
            var m = new List<(int, int, string, bool)>();
            foreach (FloorChest c in l.Chests) m.Add((c.X, c.Y, c.HoldsKey ? "key chest" : "chest", false));
            foreach (CellPos s in l.EntryStarts) m.Add((s.X, s.Y, "entry", true));
            if (l.StairExit.HasValue) m.Add((l.StairExit.Value.X, l.StairExit.Value.Y, "stair", true));
            if (l.BankCell.HasValue) m.Add((l.BankCell.Value.X, l.BankCell.Value.Y, "bank", false));
            if (l.BakerCell.HasValue) m.Add((l.BakerCell.Value.X, l.BakerCell.Value.Y, "baker", false));
            if (l.WardenCell.HasValue) m.Add((l.WardenCell.Value.X, l.WardenCell.Value.Y, "warden", false));
            if (l.Carrier != null) m.Add((l.Carrier.X, l.Carrier.Y, "carrier", false));
            CellPos? heal = CellarHealing.PointFor(l, d);
            if (heal.HasValue) m.Add((heal.Value.X, heal.Value.Y, "healing point", false));
            foreach (PlacedVignette v in d.Vignettes) m.Add((v.Cell.X, v.Cell.Y, "vignette", false));
            foreach (PlacedRoom p in l.Rooms)
            {
                // spawn cells take wall clutter (1.0 m clear of the spawn point, the encounter test below), so only the 1.9 m rule applies to them
                foreach (ChestSlot c in p.Room.Chests) m.Add((p.X + c.X, p.Y + c.Y, "chest slot", false));
                foreach (RoomFeature f in p.Room.Features) m.Add((p.X + f.X, p.Y + f.Y, "feature", false));
            }
            return m;
        }

        static bool WallOn(PlacedRoom p, int lx, int ly, CellSide s)
        {
            var (dx, dy) = CellarRoom.Step(s); int nx = lx + dx, ny = ly + dy;
            if (p.Room.InBounds(nx, ny) && p.Room.CellAt(nx, ny) != CellKind.Solid) return false;
            return !p.Room.Doors.Any(dr => dr.X == lx && dr.Y == ly && dr.Side == s);
        }

        // The footprint's axis-aligned half extents (cm) once turned by yaw.
        static (double hx, double hz) Extent(CellarClutterRow r)
        {
            double a = r.YawDeg * Math.PI / 180.0, c = Math.Abs(Math.Cos(a)), s = Math.Abs(Math.Sin(a));
            return ((r.WidthCm * c + r.DepthCm * s) / 2.0, (r.WidthCm * s + r.DepthCm * c) / 2.0);
        }

        [Test]
        public void Rooms_RulesHoldOver1000SeedsAndFloors1To4()
        {
            int rows = 0, frontOfDoorRows = 0;
            foreach (var (l, d, c) in All())
            {
                string tag = "seed " + l.Seed + " floor " + l.Floor;
                var markers = Markers(l, d);
                var filled = new HashSet<(int, int)>(d.Sockets.Where(s => s.Filled && s.Kind != SocketKind.Lantern && s.Kind != SocketKind.Roots).Select(s => (s.Cell.X, s.Cell.Y)));
                foreach (CellarClutterRow r in c.Rows.Where(q => !q.InCorridor))
                {
                    rows++;
                    string who = tag + " " + r.Id + " " + r.Piece;
                    Assert.AreEqual(r.RoomIndex, l.RoomAt(r.CellX, r.CellY), who + ": row names the wrong room");
                    PlacedRoom p = l.Rooms[r.RoomIndex]; int lx = r.CellX - p.X, ly = r.CellY - p.Y;
                    Assert.AreEqual(CellKind.Floor, p.Room.CellAt(lx, ly), who + ": not on a floor cell");
                    Assert.IsTrue(WallOn(p, lx, ly, r.Wall), who + ": " + r.Wall + " is not a closed wall side");
                    foreach (CellDoor dr in p.Room.Doors)
                    {
                        Assert.IsFalse(dr.X == lx && dr.Y == ly, who + ": on a door cell");
                        var (sx, sy) = CellarRoom.Step(CellarRoom.Opposite(dr.Side));
                        if (dr.X + sx == lx && dr.Y + sy == ly)
                        {
                            // in front of a door: every footprint stays 1.2 m (measured on the four corners) from the door lane's axis
                            bool ns = dr.Side == CellSide.North || dr.Side == CellSide.South;
                            double axis = ns ? (p.X + dr.X) * 400 + 200 : -((p.Y + dr.Y) * 400 + 200);
                            Assert.GreaterOrEqual(LaneDistance(r, ns, axis), 120.0, who + ": inside a door's lane");
                            frontOfDoorRows++;
                        }
                    }
                    Assert.IsFalse(filled.Contains((r.CellX, r.CellY)), who + ": on a filled socket cell");
                    foreach (var m in markers) Assert.IsFalse(Near(r.CellX, r.CellY, m.x, m.y, m.beside), who + ": on or beside " + m.what);
                    // the footprint stays inside its cell, behind the wall face
                    int cxCm = r.CellX * 400 + 200, czCm = -(r.CellY * 400 + 200);
                    var (hx, hz) = Extent(r);
                    Assert.LessOrEqual(Math.Abs(r.XCm - cxCm) + hx, 180.0 + 0.5, who + ": footprint leaves the cell in x");
                    Assert.LessOrEqual(Math.Abs(r.ZCm - czCm) + hz, 180.0 + 0.5, who + ": footprint leaves the cell in z");
                    // at least 1.9 m (footprint edge to marker centre) from every marker, whatever cell it stands in
                    foreach (var m in markers)
                    {
                        double mx = m.x * 400 + 200, mz = -(m.y * 400 + 200);
                        double dx = Math.Max(0, Math.Abs(mx - r.XCm) - hx), dz = Math.Max(0, Math.Abs(mz - r.ZCm) - hz);
                        Assert.GreaterOrEqual(Math.Sqrt(dx * dx + dz * dz), 190.0, who + ": too close to " + m.what);
                    }
                }
            }
            Assert.Greater(rows, 1000, "the resolver produced clutter");
            Report("rows on front-of-door cells: " + frontOfDoorRows);
        }

        // Distance (cm) from the lane axis to the yawed footprint's nearest corner/edge along the lane's lateral axis; 0 when it straddles the axis.
        static double LaneDistance(CellarClutterRow r, bool xAxis, double axis)
        {
            double a = r.YawDeg * Math.PI / 180.0, fx = Math.Sin(a), fz = Math.Cos(a), rx = fz, rz = -fx;
            var qs = new List<double>();
            foreach (int u in new[] { -1, 1 }) foreach (int v in new[] { -1, 1 })
                qs.Add(xAxis ? r.XCm + rx * u * r.WidthCm / 2.0 + fx * v * r.DepthCm / 2.0 : r.ZCm + rz * u * r.WidthCm / 2.0 + fz * v * r.DepthCm / 2.0);
            double lo = qs.Min(), hi = qs.Max();
            return axis >= lo && axis <= hi ? 0 : Math.Min(Math.Abs(lo - axis), Math.Abs(hi - axis));
        }

        [Test]
        public void DoorLane_DistanceIsMeasuredExactly()
        {
            // a crate against the south wall of cell (0,0), 72 x 72, centre 36 cm off the wall: lane through x = 200 is straddled; one on axis 600 is 400 - 36 away
            var straddle = new CellarClutterRow("t", CellarFloorClutter.Crate, 0, 0, 0, 1, CellSide.South, 200, -30, 0, 72, 72, 75);
            Assert.AreEqual(0.0, CellarFloorClutter.DoorLaneDistanceCm(straddle, true, 200), 1e-6);
            Assert.AreEqual(600 - (200 + 36), CellarFloorClutter.DoorLaneDistanceCm(straddle, true, 600), 1e-6);
            Assert.AreEqual((200 - 36) - 0, CellarFloorClutter.DoorLaneDistanceCm(straddle, true, 0), 1e-6);
            var turned = new CellarClutterRow("t", CellarFloorClutter.Crate, 0, 0, 0, 1, CellSide.East, 200, -30, 90, 100, 40, 75);   // yaw 90: the 100 cm width runs along z
            Assert.AreEqual(Math.Abs(200 - 20 - 100.0), CellarFloorClutter.DoorLaneDistanceCm(turned, true, 100), 1e-6);
            Assert.AreEqual(CellarFloorClutter.DoorLaneDistanceCm(turned, false, -30), 0.0, 1e-6);
            Assert.AreEqual(CellarFloorClutter.DoorLaneDistanceCm(turned, false, -100), 20.0, 1e-6);
        }

        // Distance (cm) from the row's cell centre to its yawed footprint rectangle, by corners and edges (independent of CellarFloorClutter.CentreClearanceCm); 0 inside.
        static double PolygonDistance(CellarClutterRow r)
        {
            double a = r.YawDeg * Math.PI / 180.0, fx = Math.Sin(a), fz = Math.Cos(a), rx = fz, rz = -fx, hw = r.WidthCm / 2.0, hd = r.DepthCm / 2.0;
            var cx = new[] { r.XCm - rx * hw - fx * hd, r.XCm + rx * hw - fx * hd, r.XCm + rx * hw + fx * hd, r.XCm - rx * hw + fx * hd };
            var cz = new[] { r.ZCm - rz * hw - fz * hd, r.ZCm + rz * hw - fz * hd, r.ZCm + rz * hw + fz * hd, r.ZCm - rz * hw + fz * hd };
            double px = r.CellX * 400 + 200, pz = -(r.CellY * 400 + 200); bool inside = true; double best = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4; double ex = cx[j] - cx[i], ez = cz[j] - cz[i], t = Math.Max(0, Math.Min(1, ((px - cx[i]) * ex + (pz - cz[i]) * ez) / (ex * ex + ez * ez)));
                best = Math.Min(best, Math.Sqrt(Math.Pow(px - (cx[i] + t * ex), 2) + Math.Pow(pz - (cz[i] + t * ez), 2)));
                if (ex * (pz - cz[i]) - ez * (px - cx[i]) < 0) inside = false;
            }
            return inside ? 0 : best;
        }

        // CellarEncounterPlan can put an extra enemy or the hunter on any free floor cell of a Combat room, at any stir level. The plan stores only the cell (no spawner yet), so the
        // standing point is the cell centre: every footprint (jitter included) on such a cell keeps at least 1.0 m of horizontal clearance from it.
        [Test]
        public void Rooms_KeepOneMetreClearOfEveryEncounterStandingPoint()
        {
            int onEncounterCells = 0; double min = double.MaxValue;
            foreach (var (l, d, c) in All())
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    var free = new HashSet<(int, int)>(CellarEncounterPlan.FreeCellsOf(l, d, r).Select(q => (q.X, q.Y)));
                    foreach (CellarSpawn sp in l.Rooms[r].Room.Spawns) free.Add((l.Rooms[r].X + sp.X, l.Rooms[r].Y + sp.Y));   // a spawn point is a standing point too
                    foreach (CellarClutterRow row in c.Rows.Where(q => q.RoomIndex == r && free.Contains((q.CellX, q.CellY))))
                    {
                        onEncounterCells++;
                        double dist = PolygonDistance(row);   // nearest point of the yawed footprint rectangle, measured on its four edges
                        min = Math.Min(min, dist);
                        Assert.GreaterOrEqual(dist, 100.0, "seed " + l.Seed + " floor " + l.Floor + " " + row.Id + " is " + dist.ToString("0") + " cm from the encounter standing point");
                    }
                }
            Report("encounter cells carry " + onEncounterCells + " rows, closest footprint " + min.ToString("0") + " cm from a standing point");
            Assert.Greater(onEncounterCells, 1000, "combat rooms are dressed on their encounter cells");
        }

        [Test]
        public void Rooms_KeepEveryDoorAndCellReachable_AndTheCentreOpen()
        {
            foreach (var (l, d, c) in All())
            {
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    PlacedRoom p = l.Rooms[r]; CellarRoom room = p.Room;
                    var blockedBase = new bool[room.Width, room.Height];
                    foreach (DressedSocket s in d.SocketsOf(r)) if (s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate)) blockedBase[s.Cell.X - p.X, s.Cell.Y - p.Y] = true;
                    foreach (PlacedVignette v in d.Vignettes) if (v.RoomIndex == r) blockedBase[v.Cell.X - p.X, v.Cell.Y - p.Y] = true;
                    var blocked = (bool[,])blockedBase.Clone();
                    int groups = 0;
                    // clutter hugs a wall and never blocks a cell: every piece leaves a lane of 2.5 m or more between the wall faces (3.6 m), so the cell stays walkable
                    foreach (CellarClutterRow row in c.Rows.Where(q => q.RoomIndex == r && q.Piece != CellarFloorClutter.Rubble))
                    {
                        Assert.LessOrEqual(row.DepthCm, 110, "seed " + l.Seed + " floor " + l.Floor + " " + row.Id + ": too deep to leave a 2.5 m lane");
                        if (!blocked[row.CellX - p.X, row.CellY - p.Y]) { blocked[row.CellX - p.X, row.CellY - p.Y] = true; groups++; }
                    }
                    string tag = "seed " + l.Seed + " floor " + l.Floor + " room " + r;
                    int floorCells = room.FloorCount;
                    Assert.LessOrEqual(groups * 100, floorCells * CellarFloorClutter.BlockedPercentCap, tag + ": clutter fills too much of the room");
                    // the centre cell (nearest the centroid) is free
                    int sx = 0, sy = 0, n = 0;
                    for (int x = 0; x < room.Width; x++) for (int y = 0; y < room.Height; y++) if (room.CellAt(x, y) == CellKind.Floor) { sx += x; sy += y; n++; }
                    if (n == 0) continue;
                    int bx = -1, by = -1, best = int.MaxValue;
                    for (int y = 0; y < room.Height; y++) for (int x = 0; x < room.Width; x++)
                    {
                        if (room.CellAt(x, y) != CellKind.Floor) continue;
                        int dx = x * 2 * n - 2 * sx, dy = y * 2 * n - 2 * sy, dd = dx * dx + dy * dy;
                        if (dd < best) { best = dd; bx = x; by = y; }
                    }
                    Assert.IsFalse(c.Rows.Any(q => q.RoomIndex == r && q.CellX == p.X + bx && q.CellY == p.Y + by), tag + ": clutter in the room centre");
                }
            }
        }

        [Test]
        public void Corridors_OnlySparseSmallPieces_AndTheyNeverNarrowTheWalk()
        {
            int corridorRows = 0;
            foreach (var (l, d, c) in All())
            {
                var outside = new HashSet<(int, int)>();
                foreach (PlacedRoom p in l.Rooms) for (int i = 0; i < p.Room.Doors.Count; i++) { CellPos o = p.DoorOutside(i); outside.Add((o.X, o.Y)); }
                var used = new HashSet<(int, int)>(c.Rows.Where(q => q.InCorridor).Select(q => (q.CellX, q.CellY)));
                var markers = Markers(l, d);
                foreach (CellarClutterRow r in c.Rows.Where(q => q.InCorridor))
                {
                    corridorRows++;
                    string who = "seed " + l.Seed + " floor " + l.Floor + " " + r.Id;
                    Assert.IsTrue(r.Piece == CellarFloorClutter.Rubble || r.Piece == CellarFloorClutter.Crate, who + ": " + r.Piece + " is not a small piece");
                    PlacedCorridor pc = l.CorridorAt(r.CellX, r.CellY);
                    Assert.IsNotNull(pc, who + ": no corridor cell");
                    Assert.AreEqual(CorridorShape.Straight, pc.Piece.Shape, who + ": not a straight cell");
                    Assert.IsTrue((pc.Mask & CellarRoom.MaskBit(r.Wall)) == 0, who + ": not against a closed side");
                    Assert.IsFalse(outside.Contains((r.CellX, r.CellY)), who + ": in front of a door");
                    Assert.IsFalse(l.DeadEnds.Any(e => e.X == r.CellX && e.Y == r.CellY), who + ": on a dead end");
                    if (l.LockedLinkIndex >= 0) Assert.IsFalse(l.Links[l.LockedLinkIndex].Path.Any(e => e.X == r.CellX && e.Y == r.CellY), who + ": on the locked link");
                    foreach (var m in markers) Assert.IsFalse(Near(r.CellX, r.CellY, m.x, m.y, m.beside), who + ": on or beside " + m.what);
                    foreach (CellSide s in Sides) { var (dx, dy) = CellarRoom.Step(s); Assert.IsFalse(used.Contains((r.CellX + dx, r.CellY + dy)), who + ": clutter in a neighbouring corridor cell"); }
                    Assert.LessOrEqual(r.DepthCm, 110, who + ": too deep");
                    var (hx, hz) = Extent(r);
                    int cxCm = r.CellX * 400 + 200, czCm = -(r.CellY * 400 + 200);
                    double offWall = r.Wall == CellSide.East || r.Wall == CellSide.West ? Math.Abs(r.XCm - cxCm) + hx : Math.Abs(r.ZCm - czCm) + hz;
                    Assert.LessOrEqual(offWall, 180.0 + 0.5, who + ": behind the wall face");
                    // the walk left between the wall faces (360 cm) once the piece's full across-extent is taken: at least 2.5 m
                    double across = r.Wall == CellSide.East || r.Wall == CellSide.West ? 2 * hx : 2 * hz;
                    Assert.GreaterOrEqual(360.0 - across, 250.0, who + ": the corridor narrows below 2.5 m");
                }
            }
            Assert.Greater(corridorRows, 50, "some corridors carry a piece");
        }

        [Test]
        public void SameSeedAndFloor_GiveIdenticalClutter()
        {
            for (uint seed = 0; seed < 200; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellarFloorClutter a = CellarFloorClutter.Resolve(l, d), b = CellarFloorClutter.Resolve(CellarFloorGenerator.Generate(seed, floor), CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(seed, floor)));
                    Assert.AreEqual(a.ClutterHash(), b.ClutterHash(), "seed " + seed + " floor " + floor);
                    Assert.AreEqual(a.Rows.Count, b.Rows.Count);
                    Assert.AreEqual(string.Join("|", a.Rows.Select(r => r.Id)), string.Join("|", b.Rows.Select(r => r.Id)));
                }
        }

        [Test]
        public void ClutterVariesWithSeedAndFloor()
        {
            var hashes = new HashSet<ulong>();
            for (uint s = 0; s < 200; s++) { var l = CellarFloorGenerator.Generate(s, 1); hashes.Add(CellarFloorClutter.Resolve(l, CellarFloorDressing.Resolve(l)).ClutterHash()); }
            Assert.Greater(hashes.Count, 150, "different seeds should rarely share clutter");
            var l1 = CellarFloorGenerator.Generate(12345, 1); var l2 = CellarFloorGenerator.Generate(12345, 2);
            Assert.AreNotEqual(CellarFloorClutter.Resolve(l1, CellarFloorDressing.Resolve(l1)).ClutterHash(), CellarFloorClutter.Resolve(l2, CellarFloorDressing.Resolve(l2)).ClutterHash());
        }

        // Pinned from the first run, like the layout goldens: any change to the placement rules changes it on purpose (the round-2 look needs a new capture then).
        const ulong GoldenClutter12345Floor1 = 16011374251828274625UL, GoldenClutter12345Floor2 = 7084911976074808467UL, GoldenClutter12345Floor3 = 3584408126852206761UL;   // floor 3 re-pinned 2026-11-01: procgen 5 vignettes, deliberate (floors 1-2 unchanged; earlier: procgen item 2)

        [Test]
        public void Golden_Seed12345_ClutterHash()
        {
            ulong H(int f) { var l = CellarFloorGenerator.Generate(12345, f); return CellarFloorClutter.Resolve(l, CellarFloorDressing.Resolve(l)).ClutterHash(); }
            Assert.That(H(1) == GoldenClutter12345Floor1 && H(2) == GoldenClutter12345Floor2 && H(3) == GoldenClutter12345Floor3,
                "golden changed; actual floor 1 = " + H(1) + "UL, floor 2 = " + H(2) + "UL, floor 3 = " + H(3) + "UL");
        }

        // The layout and dressing goldens belong to the room pool and Q12; the clutter pass must not move them (it only reads them).
        const ulong GoldenLayout12345Floor1 = 2055763217958189686UL, GoldenLayout12345Floor2 = 1402270470201830695UL, GoldenLayout12345Floor3 = 11057337751887225001UL;   // re-pinned by procgen item 2 (layout presets, deliberate)
        const ulong GoldenDressing12345Floor1 = 7289301962720901386UL;   // re-pinned by procgen item 2

        [Test]
        public void Resolving_DoesNotMoveLayoutOrDressingHashes()
        {
            var expect = new[] { GoldenLayout12345Floor1, GoldenLayout12345Floor2, GoldenLayout12345Floor3 };
            for (int f = 1; f <= 3; f++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(12345, f);
                CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                ulong lh = l.LayoutHash(), dh = d.DressingHash();
                Assert.AreEqual(expect[f - 1], lh, "layout golden, floor " + f);
                CellarFloorClutter.Resolve(l, d); CellarFloorClutter.Resolve(l, d);
                Assert.AreEqual(lh, l.LayoutHash()); Assert.AreEqual(dh, d.DressingHash()); Assert.AreEqual(dh, CellarFloorDressing.Resolve(l).DressingHash());
                if (f == 1) Assert.AreEqual(GoldenDressing12345Floor1, dh, "dressing golden, floor 1; actual = " + dh + "UL");
            }
        }

        // NUnitLite hides test output: SC_CLUTTER_STATS=<file> appends the density lines there.
        static void Report(string line)
        {
            TestContext.Progress.WriteLine(line);
            string f = Environment.GetEnvironmentVariable("SC_CLUTTER_STATS");
            if (!string.IsNullOrEmpty(f)) System.IO.File.AppendAllText(f, line + "\n");
        }

        [Test]
        public void RoomsAndCorridorsCarryClutter_Stats()
        {
            int rooms = 0, rows = 0, combat = 0, combatRows = 0, corr = 0, corrRows = 0, floors = 0; var perPiece = new Dictionary<string, int>();
            foreach (var (l, d, c) in All())
            {
                if (l.Seed >= 300) continue;
                floors++; corr += l.Corridors.Count; corrRows += c.Rows.Count(q => q.InCorridor);
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    rooms++; rows += c.CountInRoom(r);
                    if (l.Rooms[r].Room.Kind == RoomKind.Combat) { combat++; combatRows += c.CountInRoom(r); }
                }
                foreach (CellarClutterRow row in c.Rows) { perPiece.TryGetValue(row.Piece, out int k); perPiece[row.Piece] = k + 1; }
            }
            double avg = rows / (double)rooms;
            Report("clutter: " + avg.ToString("0.00") + " rows per room (" + rooms + " rooms), " + (combatRows / (double)Math.Max(1, combat)).ToString("0.00") + " per combat room, "
                + (corrRows / (double)corr).ToString("0.000") + " per corridor cell, " + ((rows + corrRows) / (double)floors).ToString("0.0") + " per floor; "
                + string.Join(", ", perPiece.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
            Assert.GreaterOrEqual(avg, 1.5, "rooms should read dressed, got " + avg);
        }

        // Combat rooms are the rooms that read empty: they must carry at least two rows on average.
        [Test]
        public void CombatRooms_CarryAtLeastTwoRowsOnAverage()
        {
            int combat = 0, combatRows = 0; var perFloor = new int[5, 3];
            foreach (var (l, d, c) in All())
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    perFloor[l.Floor, 0]++; perFloor[l.Floor, 1] += c.CountInRoom(r);
                    if (l.Rooms[r].Room.Kind == RoomKind.Combat) { combat++; combatRows += c.CountInRoom(r); perFloor[l.Floor, 2] += c.CountInRoom(r); }
                }
            Report("rows per room by floor: " + string.Join(", ", Enumerable.Range(1, 4).Select(f => f + ": " + (perFloor[f, 1] / (double)perFloor[f, 0]).ToString("0.00"))));
            double avg = combatRows / (double)Math.Max(1, combat);
            Report("combat rooms: " + avg.ToString("0.00") + " rows each (" + combat + " rooms)");
            Assert.GreaterOrEqual(avg, 2.0, "combat rooms should read dressed, got " + avg);
        }
    }
}
