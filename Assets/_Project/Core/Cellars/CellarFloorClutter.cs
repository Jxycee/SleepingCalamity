// Core/Cellars/CellarFloorClutter.cs: the wall clutter of one Cellars floor (floor art round 2, density pass): crate stacks, crates, jar clusters, sack piles and rubble
// that stand against the walls so the rooms stop reading empty. A third pure function of the same (seed, floor), after the layout and the dressing: it reads the finished
// CellarFloorLayout and the resolved CellarFloorDressing and changes neither (LayoutHash and DressingHash are untouched; nothing here feeds back into room data).
//   - every room draws from its own stream Rng.Derive(seed, "cellars.clutter", floor << 32 | roomIndex), the corridors from index floor << 32 | 0xFFFF; every roll of
//     a candidate is always drawn, so changing one rule never shifts another cell's pick. No string.GetHashCode, no float in a decision (positions are whole centimetres).
//   - rooms: only floor cells that touch a closed wall side carry clutter, pushed to the wall face. Never on a door cell or the cell in front of a door, never on a
//     spawn, chest, key, entry start, stair exit, bank, baker, warden, carrier, the healing point, a vignette cell, a feature cell or a filled socket cell (Lantern and
//     Roots sockets hang overhead and leave the floor free), and not on the cell orthogonally beside the entry start or the stair exit. A piece hugs the wall inside its own
//     4 m cell, so it is always 1.9 m or more from the centre of any marker in a neighbouring cell ("adjacent" is met by that clearance, which the tests measure). The cell
//     nearest the room's centre stays open.
//     A group (primary piece plus an optional companion) blocks its cell; a group is kept only when every door and every cell that was reachable still is, and groups
//     never cover more than two fifths of the room's floor cells. Cells beside a filled barrel, shelf, chute or crate socket are tried first, so storage clusters.
//     0-2 Rubble per room go on other wall cells (low, they never block).
//   - corridors: only straight cells, not the cell in front of a door, a dead end, the locked link, the bank or the healing cell; about one in eight gets one small
//     piece (Rubble or Crate) against a side wall, never in two neighbouring cells. A piece takes at most 1.1 m of the 3.6 m between the wall faces (2.5 m or more stay walkable).
// Frame: the builder's world frame in whole centimetres (x east, z north: cell (x, y) has its centre at ((x * 4 + 2) m, -(y * 4 + 2) m)); yaw in degrees clockwise from above
// with the piece's front (local +Z) facing into the room; Width is along the piece's own X (along the wall), Depth along its own Z (off the wall).
// Authority: none (every peer derives the same clutter from (seed, floor)).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class CellarClutterRow
    {
        public readonly string Id, Piece;
        public readonly int RoomIndex;            // -1 in a corridor
        public readonly int CellX, CellY;         // grid cell
        public readonly int Group;                // groups in one cell share the number (room cluster index); corridor pieces and rubble use their own
        public readonly CellSide Wall;            // the closed side the piece stands against
        public readonly int XCm, ZCm, YawDeg, WidthCm, DepthCm, HeightCm;
        public CellarClutterRow(string id, string piece, int roomIndex, int cellX, int cellY, int group, CellSide wall, int xCm, int zCm, int yawDeg, int w, int d, int h)
        { Id = id; Piece = piece; RoomIndex = roomIndex; CellX = cellX; CellY = cellY; Group = group; Wall = wall; XCm = xCm; ZCm = zCm; YawDeg = yawDeg; WidthCm = w; DepthCm = d; HeightCm = h; }
        public float X => XCm / 100f;
        public float Z => ZCm / 100f;
        public float Yaw => YawDeg;
        public float Width => WidthCm / 100f;
        public float Depth => DepthCm / 100f;
        public float Height => HeightCm / 100f;
        public bool InCorridor => RoomIndex < 0;
    }

    public sealed class CellarFloorClutter
    {
        public const string StreamName = "cellars.clutter";
        public const string Crate = "Crate", CrateStack = "CrateStack", JarCluster = "JarCluster", Rubble = "Rubble", SackPile = "SackPile";
        public const int CellCm = CellarFloorLayout.CellMeters * 100, WallFaceCm = 180, WallGapCm = 10;   // the wall face stands 1.8 m from the cell centre
        public const int CorridorChancePercent = 12, MaxRubblePerRoom = 2, BlockedPercentCap = 40, EncounterClearCm = 100;

        // Footprints (cm) of CellarClutter.fbx (Assets/_Project/Content/Art/Cellar/B5/CellarClutter/; pivot bottom centre, front local +X, wall side -X): width along the wall, depth off it, height.
        static readonly string[] Names = { Crate, CrateStack, JarCluster, Rubble, SackPile };
        static readonly int[][] Sizes = { new[] { 72, 72, 75 }, new[] { 149, 81, 145 }, new[] { 96, 79, 75 }, new[] { 146, 106, 41 }, new[] { 115, 84, 56 } };
        public static IReadOnlyList<string> PieceNames => Names;
        public static (int w, int d, int h) SizeOf(string piece)
        {
            for (int i = 0; i < Names.Length; i++) if (Names[i] == piece) return (Sizes[i][0], Sizes[i][1], Sizes[i][2]);
            throw new ArgumentException("unknown clutter piece " + piece);
        }

        static readonly CellSide[] AllSides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        public readonly uint Seed; public readonly int Floor;
        public readonly IReadOnlyList<CellarClutterRow> Rows;

        CellarFloorClutter(uint seed, int floor, List<CellarClutterRow> rows) { Seed = seed; Floor = floor; Rows = rows; }

        public int CountInRoom(int roomIndex) { int n = 0; foreach (CellarClutterRow r in Rows) if (r.RoomIndex == roomIndex) n++; return n; }

        // Stable across machines: FNV-1a 64 over an ordered serialization, like LayoutHash and DressingHash.
        public ulong ClutterHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(Rows.Count);
            foreach (CellarClutterRow r in Rows)
            {
                h.Append(r.Piece); h.AppendInt32LE(r.RoomIndex); h.AppendInt32LE(r.CellX); h.AppendInt32LE(r.CellY); h.AppendInt32LE(r.Group); h.AppendInt32LE((int)r.Wall);
                h.AppendInt32LE(r.XCm); h.AppendInt32LE(r.ZCm); h.AppendInt32LE(r.YawDeg);
            }
            return h.Value;
        }

        public static CellarFloorClutter Resolve(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (dressing == null) throw new ArgumentNullException(nameof(dressing));
            int n = CellarFloorLayout.GridSize;
            var rows = new List<CellarClutterRow>();

            // cells no clutter may stand on or beside (world grid)
            var onlyBan = new bool[n, n];       // never on it
            var nearBan = new bool[n, n];       // never on it nor orthogonally beside it
            void Ban(bool[,] a, int x, int y) { if (x >= 0 && y >= 0 && x < n && y < n) a[x, y] = true; }
            void BanNear(int x, int y) { Ban(nearBan, x, y); Ban(nearBan, x + 1, y); Ban(nearBan, x - 1, y); Ban(nearBan, x, y + 1); Ban(nearBan, x, y - 1); }
            foreach (FloorChest c in layout.Chests) Ban(onlyBan, c.X, c.Y);
            foreach (CellPos s in layout.EntryStarts) BanNear(s.X, s.Y);
            if (layout.StairExit.HasValue) BanNear(layout.StairExit.Value.X, layout.StairExit.Value.Y);
            if (layout.BankCell.HasValue) Ban(onlyBan, layout.BankCell.Value.X, layout.BankCell.Value.Y);
            if (layout.BakerCell.HasValue) Ban(onlyBan, layout.BakerCell.Value.X, layout.BakerCell.Value.Y);
            if (layout.WardenCell.HasValue) Ban(onlyBan, layout.WardenCell.Value.X, layout.WardenCell.Value.Y);
            if (layout.Carrier != null) Ban(onlyBan, layout.Carrier.X, layout.Carrier.Y);
            CellPos? heal = CellarHealing.PointFor(layout, dressing);
            if (heal.HasValue) Ban(onlyBan, heal.Value.X, heal.Value.Y);
            foreach (PlacedVignette v in dressing.Vignettes) Ban(onlyBan, v.Cell.X, v.Cell.Y);
            foreach (DressedSocket s in dressing.Sockets) if (s.Filled && s.Kind != SocketKind.Lantern && s.Kind != SocketKind.Roots) Ban(onlyBan, s.Cell.X, s.Cell.Y);   // lanterns and roots hang overhead: nothing on the floor
            var doorOutside = new bool[n, n];   // the corridor cell in front of any door
            foreach (PlacedRoom p in layout.Rooms)
            {
                foreach (CellarSpawn sp in p.Room.Spawns) Ban(onlyBan, p.X + sp.X, p.Y + sp.Y);
                foreach (ChestSlot c in p.Room.Chests) Ban(onlyBan, p.X + c.X, p.Y + c.Y);
                foreach (RoomFeature f in p.Room.Features) Ban(onlyBan, p.X + f.X, p.Y + f.Y);
                for (int d = 0; d < p.Room.Doors.Count; d++)
                {
                    CellPos o = p.DoorOutside(d); Ban(doorOutside, o.X, o.Y);
                    CellDoor door = p.Room.Doors[d];
                    Ban(onlyBan, p.X + door.X, p.Y + door.Y);   // the door cell
                    CellPos inFront = new CellPos(p.X + door.X, p.Y + door.Y).Step(CellarRoom.Opposite(door.Side));   // the cell inside, in front of the door
                    Ban(onlyBan, inFront.X, inFront.Y);
                }
            }

            // Every cell CellarEncounterPlan could ever put an extra enemy or the rat-king placeholder on (its free-cell rule, whatever the stir level). The plan stores only the
            // cell and no spawner exists yet, so the standing point is taken as the cell centre: clutter may use such a cell only when every footprint keeps EncounterClearCm from it.
            var enc = new bool[n, n];
            for (int r = 0; r < layout.Rooms.Count; r++)
                foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(layout, dressing, r)) enc[c.X, c.Y] = true;

            for (int r = 0; r < layout.Rooms.Count; r++) ResolveRoom(layout, dressing, r, onlyBan, nearBan, enc, rows);
            ResolveCorridors(layout, onlyBan, nearBan, doorOutside, heal, rows);
            return new CellarFloorClutter(layout.Seed, layout.Floor, rows);
        }

        // ---- rooms ----

        static bool WallSide(PlacedRoom p, int lx, int ly, CellSide s)
        {
            var (dx, dy) = CellarRoom.Step(s); int nx = lx + dx, ny = ly + dy;
            if (p.Room.InBounds(nx, ny) && p.Room.CellAt(nx, ny) != CellKind.Solid) return false;   // floor or pillar beyond: not a wall face
            foreach (CellDoor d in p.Room.Doors) if (d.X == lx && d.Y == ly && d.Side == s) return false;
            return true;
        }

        static void ResolveRoom(CellarFloorLayout layout, CellarFloorDressing dressing, int r, bool[,] onlyBan, bool[,] nearBan, bool[,] enc, List<CellarClutterRow> rows)
        {
            PlacedRoom p = layout.Rooms[r]; CellarRoom room = p.Room;
            var rng = new Pcg32(Rng.Derive(layout.Seed, StreamName, ((ulong)layout.Floor << 32) | (uint)r));

            // the floor cell nearest the room's centre stays open for combat (twice the coordinates, so no fraction)
            int floorCount = 0, sx = 0, sy = 0;
            for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++) if (room.CellAt(x, y) == CellKind.Floor) { floorCount++; sx += x; sy += y; }
            if (floorCount == 0) return;
            int centreX = -1, centreY = -1, bestD = int.MaxValue;
            for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++)
            {
                if (room.CellAt(x, y) != CellKind.Floor) continue;
                int dx = x * 2 * floorCount - 2 * sx, dy = y * 2 * floorCount - 2 * sy, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; centreX = x; centreY = y; }
            }

            // cells beside a filled bulky socket come first
            var bulky = new bool[p.Width, p.Height];
            foreach (DressedSocket s in dressing.SocketsOf(r))
                if (s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate || s.Kind == SocketKind.Shelf || s.Kind == SocketKind.Chute))
                    foreach (CellSide sd in AllSides)
                    {
                        var (dx, dy) = CellarRoom.Step(sd); int bx = s.Cell.X - p.X + dx, by = s.Cell.Y - p.Y + dy;
                        if (room.InBounds(bx, by)) bulky[bx, by] = true;
                    }

            var tier0 = new List<(int x, int y)>(); var tier1 = new List<(int x, int y)>();
            for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++)
            {
                if (room.CellAt(x, y) != CellKind.Floor || (x == centreX && y == centreY)) continue;
                int wx = p.X + x, wy = p.Y + y;
                if (onlyBan[wx, wy] || nearBan[wx, wy]) continue;
                bool wall = false; foreach (CellSide sd in AllSides) if (WallSide(p, x, y, sd)) wall = true;
                if (!wall) continue;
                (bulky[x, y] ? tier0 : tier1).Add((x, y));
            }
            Shuffle(tier0, ref rng); Shuffle(tier1, ref rng);
            var cands = new List<(int x, int y)>(tier0); cands.AddRange(tier1);
            int cap = Math.Min(room.Kind == RoomKind.Combat ? cands.Count : (cands.Count + 1) / 2, floorCount * BlockedPercentCap / 100);   // one group per two free wall cells at most (the rooms are crowded with markers, so the free ones are few); Combat rooms, the ones that read empty, may fill every free wall cell up to the cover cap

            // blocked cells before any clutter: filled free-standing barrels and crates, vignette cells
            var blocked = new bool[p.Width, p.Height];
            foreach (DressedSocket s in dressing.SocketsOf(r)) if (s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate)) blocked[s.Cell.X - p.X, s.Cell.Y - p.Y] = true;
            foreach (PlacedVignette v in dressing.Vignettes) if (v.RoomIndex == r) blocked[v.Cell.X - p.X, v.Cell.Y - p.Y] = true;
            bool[,] baseline = Reach(room, blocked);

            var taken = new bool[p.Width, p.Height]; int groups = 0;
            foreach (var c in cands)
            {
                // all rolls drawn for every candidate
                int accept = rng.Range(0, 100), sideRoll = rng.Range(0, 4), pieceRoll = rng.Range(0, 100), companionRoll = rng.Range(0, 100), sign = rng.Range(0, 2) * 2 - 1;
                int jit = rng.Range(0, 21), yawJit = rng.Range(0, 11) - 5;
                if (groups >= cap) continue;
                bool nearGroup = false;
                foreach (CellSide sd in AllSides) { var (dx, dy) = CellarRoom.Step(sd); int nx = c.x + dx, ny = c.y + dy; if (room.InBounds(nx, ny) && taken[nx, ny]) nearGroup = true; }
                int threshold = bulky[c.x, c.y] ? 100 : nearGroup ? 90 : 75;
                if (accept >= threshold) continue;
                var walls = new List<CellSide>(); foreach (CellSide sd in AllSides) if (WallSide(p, c.x, c.y, sd)) walls.Add(sd);
                CellSide wall = walls[sideRoll % walls.Count];

                string primary = pieceRoll < 35 ? CrateStack : pieceRoll < 60 ? Crate : pieceRoll < 80 ? JarCluster : SackPile;
                string companion = null;
                switch (primary)
                {
                    case CrateStack: companion = companionRoll < 50 ? JarCluster : companionRoll < 80 ? Crate : null; break;
                    case Crate: companion = companionRoll < 50 ? JarCluster : companionRoll < 75 ? Crate : null; break;
                    case JarCluster: companion = companionRoll < 60 ? Crate : null; break;
                    default: companion = companionRoll < 40 ? Crate : null; break;
                }
                int jl = jit - 10;   // -10..10 cm
                string id = "r" + r + "_" + (p.X + c.x) + "_" + (p.Y + c.y);
                var made = new List<CellarClutterRow>();
                if (!enc[p.X + c.x, p.Y + c.y])
                {
                    if (companion == null)
                        made.Add(Make(id + "_0", primary, r, p.X + c.x, p.Y + c.y, groups + 1, wall, (jit - 10) * 6, yawJit));
                    else
                    {
                        made.Add(Make(id + "_0", primary, r, p.X + c.x, p.Y + c.y, groups + 1, wall, -sign * 92 + jl, yawJit));
                        made.Add(Make(id + "_1", companion, r, p.X + c.x, p.Y + c.y, groups + 1, wall, sign * 70 - jl, -yawJit));
                    }
                }
                else
                {
                    // an encounter-candidate cell: slide each piece along the wall until it keeps EncounterClearCm from the cell centre; a pair that cannot, falls back to the primary
                    // alone, then to a single Crate, then to nothing (the cell stays bare)
                    CellarClutterRow a = null, b = null;
                    if (companion != null)
                    {
                        a = Fit(id + "_0", primary, r, p.X + c.x, p.Y + c.y, groups + 1, wall, -sign * 92 + jl, yawJit);
                        b = Fit(id + "_1", companion, r, p.X + c.x, p.Y + c.y, groups + 1, wall, sign * 70 - jl, -yawJit);
                        if (a == null || b == null || Math.Abs(LateralOf(a, wall) - LateralOf(b, wall)) * 2 < a.WidthCm + b.WidthCm + 10) a = b = null;
                    }
                    if (a == null) a = Fit(id + "_0", primary, r, p.X + c.x, p.Y + c.y, groups + 1, wall, (jit - 10) * 6, yawJit);
                    if (a == null) a = Fit(id + "_0", Crate, r, p.X + c.x, p.Y + c.y, groups + 1, wall, (jit - 10) * 6, yawJit);
                    if (a == null) continue;
                    made.Add(a); if (b != null) made.Add(b);
                }

                blocked[c.x, c.y] = true;
                if (!StillReachable(room, blocked, baseline)) { blocked[c.x, c.y] = false; continue; }
                taken[c.x, c.y] = true; groups++;
                rows.AddRange(made);
            }

            // rubble: 0-2 on other wall cells; low, so it never blocks
            int rubble = rng.Range(0, MaxRubblePerRoom + 1);
            for (int i = 0; i < rubble; i++)
            {
                int pick = rng.Range(0, 1 << 20), sideRoll = rng.Range(0, 4), jit = rng.Range(0, 21), yawJit = rng.Range(0, 11) - 5;
                var free = new List<(int x, int y)>(); foreach (var c in cands) if (!taken[c.x, c.y]) free.Add(c);
                if (free.Count == 0) continue;
                var c2 = free[pick % free.Count];
                var walls = new List<CellSide>(); foreach (CellSide sd in AllSides) if (WallSide(p, c2.x, c2.y, sd)) walls.Add(sd);
                string rid = "r" + r + "_" + (p.X + c2.x) + "_" + (p.Y + c2.y) + "_R";
                CellarClutterRow heap = enc[p.X + c2.x, p.Y + c2.y]
                    ? Fit(rid, Rubble, r, p.X + c2.x, p.Y + c2.y, 1000 + i, walls[sideRoll % walls.Count], (jit - 10) * 6, yawJit)
                    : Make(rid, Rubble, r, p.X + c2.x, p.Y + c2.y, 1000 + i, walls[sideRoll % walls.Count], (jit - 10) * 6, yawJit);
                if (heap == null) continue;   // a heap is too deep to keep clear of an encounter cell's centre
                taken[c2.x, c2.y] = true;   // one rubble heap per cell; later heaps pick another
                rows.Add(heap);
            }
        }

        // ---- corridors ----

        static void ResolveCorridors(CellarFloorLayout layout, bool[,] onlyBan, bool[,] nearBan, bool[,] doorOutside, CellPos? heal, List<CellarClutterRow> rows)
        {
            int n = CellarFloorLayout.GridSize;
            var rng = new Pcg32(Rng.Derive(layout.Seed, StreamName, ((ulong)layout.Floor << 32) | 0xFFFFu));
            var bad = new bool[n, n];
            foreach (CellPos d in layout.DeadEnds) bad[d.X, d.Y] = true;
            if (layout.LockedLinkIndex >= 0) foreach (CellPos c in layout.Links[layout.LockedLinkIndex].Path) if (c.X >= 0 && c.Y >= 0 && c.X < n && c.Y < n) bad[c.X, c.Y] = true;
            var used = new bool[n, n];
            int ns = CellarRoom.MaskBit(CellSide.North) | CellarRoom.MaskBit(CellSide.South), ew = CellarRoom.MaskBit(CellSide.East) | CellarRoom.MaskBit(CellSide.West);
            foreach (PlacedCorridor c in layout.Corridors)
            {
                int chance = rng.Range(0, 100), pieceRoll = rng.Range(0, 100), sideRoll = rng.Range(0, 2), jit = rng.Range(0, 21), yawJit = rng.Range(0, 11) - 5;
                if (c.Piece.Shape != CorridorShape.Straight || (c.Mask != ns && c.Mask != ew)) continue;
                if (c.X < 0 || c.Y < 0 || c.X >= n || c.Y >= n || bad[c.X, c.Y] || doorOutside[c.X, c.Y] || onlyBan[c.X, c.Y] || nearBan[c.X, c.Y]) continue;
                if (chance >= CorridorChancePercent) continue;
                bool neighbour = false; foreach (CellSide sd in AllSides) { var (dx, dy) = CellarRoom.Step(sd); int nx = c.X + dx, ny = c.Y + dy; if (nx >= 0 && ny >= 0 && nx < n && ny < n && used[nx, ny]) neighbour = true; }
                if (neighbour) continue;
                CellSide a = c.Mask == ns ? CellSide.East : CellSide.North, b = CellarRoom.Opposite(a);   // the two closed sides
                used[c.X, c.Y] = true;
                rows.Add(Make("c" + c.X + "_" + c.Y, pieceRoll < 60 ? Rubble : Crate, -1, c.X, c.Y, 2000 + rows.Count, sideRoll == 0 ? a : b, (jit - 10) * 5, 0));
            }
        }

        // ---- placement maths ----

        // Positions in the builder frame: the piece stands on the wall side of its cell, WallGapCm clear of the wall face, shifted along the wall by lateralCm.
        static CellarClutterRow Make(string id, string piece, int room, int cx, int cy, int group, CellSide wall, int lateralCm, int yawJitter)
        {
            var (w, d, h) = SizeOf(piece);
            int wx, wz, yaw, lx, lz;   // wall direction, base yaw (front into the room) and the lateral axis (the piece's right)
            switch (wall)
            {
                case CellSide.North: wx = 0; wz = 1; yaw = 180; lx = -1; lz = 0; break;
                case CellSide.East: wx = 1; wz = 0; yaw = 270; lx = 0; lz = 1; break;
                case CellSide.South: wx = 0; wz = -1; yaw = 0; lx = 1; lz = 0; break;
                default: wx = -1; wz = 0; yaw = 90; lx = 0; lz = -1; break;
            }
            int off = WallFaceCm - WallGapCm - d / 2;
            int x = cx * CellCm + CellCm / 2 + wx * off + lx * lateralCm;
            int z = -(cy * CellCm + CellCm / 2) + wz * off + lz * lateralCm;
            int jitter = piece == CrateStack ? yawJitter / 2 : yawJitter;
            return new CellarClutterRow(id, piece, room, cx, cy, group, wall, x, z, ((yaw + jitter) % 360 + 360) % 360, w, d, h);
        }

        // Horizontal distance (cm) from the cell centre to the footprint's axis-aligned bound (conservative once the piece is yawed).
        public static double CentreClearanceCm(CellarClutterRow r)
        {
            double ang = r.YawDeg * Math.PI / 180.0, co = Math.Abs(Math.Cos(ang)), si = Math.Abs(Math.Sin(ang));
            double hx = (r.WidthCm * co + r.DepthCm * si) / 2.0, hz = (r.WidthCm * si + r.DepthCm * co) / 2.0;
            double dx = Math.Max(0, Math.Abs(r.CellX * CellCm + CellCm / 2 - r.XCm) - hx), dz = Math.Max(0, Math.Abs(-(r.CellY * CellCm + CellCm / 2) - r.ZCm) - hz);
            return Math.Sqrt(dx * dx + dz * dz);
        }

        // Along-the-wall offset of a row from its cell centre, in the same signed sense Make takes (cm).
        static int LateralOf(CellarClutterRow r, CellSide wall)
        {
            int dx = r.XCm - (r.CellX * CellCm + CellCm / 2), dz = r.ZCm + (r.CellY * CellCm + CellCm / 2);
            switch (wall) { case CellSide.North: return -dx; case CellSide.East: return dz; case CellSide.South: return dx; default: return -dz; }
        }

        // Make, but slid along the wall (nearest offset to the wanted one first, 5 cm steps) until the footprint keeps EncounterClearCm from the cell centre and stays inside the cell; null when none does.
        static CellarClutterRow Fit(string id, string piece, int room, int cx, int cy, int group, CellSide wall, int lateralCm, int yawJitter)
        {
            for (int step = 0; step <= 2 * (WallFaceCm / 5); step++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    if (step == 0 && sgn > 0) continue;
                    CellarClutterRow row = Make(id, piece, room, cx, cy, group, wall, lateralCm + sgn * step * 5, yawJitter);
                    double ang = row.YawDeg * Math.PI / 180.0, co = Math.Abs(Math.Cos(ang)), si = Math.Abs(Math.Sin(ang));
                    double hx = (row.WidthCm * co + row.DepthCm * si) / 2.0, hz = (row.WidthCm * si + row.DepthCm * co) / 2.0;
                    if (Math.Abs(row.XCm - (cx * CellCm + CellCm / 2)) + hx > WallFaceCm || Math.Abs(row.ZCm + (cy * CellCm + CellCm / 2)) + hz > WallFaceCm) continue;
                    if (CentreClearanceCm(row) >= EncounterClearCm) return row;
                }
            return null;
        }

        static void Shuffle(List<(int x, int y)> l, ref Pcg32 rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
        }

        // ---- reachability (room local) ----

        static bool[,] Reach(CellarRoom room, bool[,] blocked)
        {
            var seen = new bool[room.Width, room.Height];
            CellDoor start = null; foreach (CellDoor d in room.Doors) if (!blocked[d.X, d.Y]) { start = d; break; }
            if (start == null) return seen;
            var q = new Queue<(int, int)>(); seen[start.X, start.Y] = true; q.Enqueue((start.X, start.Y));
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue();
                foreach (CellSide sd in AllSides)
                {
                    var (dx, dy) = CellarRoom.Step(sd); int nx = cx + dx, ny = cy + dy;
                    if (room.InBounds(nx, ny) && !seen[nx, ny] && !blocked[nx, ny] && room.CellAt(nx, ny) == CellKind.Floor) { seen[nx, ny] = true; q.Enqueue((nx, ny)); }
                }
            }
            return seen;
        }

        // Every cell that was reachable before the clutter (and is not clutter itself) still is, doors included.
        static bool StillReachable(CellarRoom room, bool[,] blocked, bool[,] baseline)
        {
            bool[,] now = Reach(room, blocked);
            for (int x = 0; x < room.Width; x++) for (int y = 0; y < room.Height; y++)
                if (baseline[x, y] && !blocked[x, y] && !now[x, y]) return false;
            foreach (CellDoor d in room.Doors) if (baseline[d.X, d.Y] && !now[d.X, d.Y]) return false;
            return true;
        }
    }
}
