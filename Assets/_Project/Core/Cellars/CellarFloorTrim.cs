// Core/Cellars/CellarFloorTrim.cs: wall-mounted and overhead dressing of one Cellars floor (procgen item 6b, the floor-art follow-up "wall/overhead dressing adds density without
// floor cost"). A pure function of the same (seed, floor), after the layout, the dressing and the chunks; it reads them and changes none (LayoutHash and DressingHash are untouched).
//   - Wall layer: Plaque, Hook, WallShelf hang on a closed wall face of a floor cell. They start at least WallMinBaseCm above the floor and stand at most WallMaxDepthCm off the wall,
//     so they never reach into the walking lane between the wall faces.
//   - Overhead layer: Beam, Hanging hang from the crown of a floor cell, their lowest point at least OverheadMinBaseCm above the floor (above head height); they never block walking.
//   - Never on a door cell, the cell in front of a door (the door's lane), a chest, the healing point, a vignette cell or a chunk solid cell; a wall piece never shares a wall face with
//     a filled wall-mounted socket (Shelf, Rack, Chute, Lantern) and an overhead piece never shares a cell with a filled Lantern or Roots socket. Corridors and the floor-4 stub get none.
//   - Each room draws from its own stream Rng.Derive(seed, "cellars.trim", floor << 32 | roomIndex). No string.GetHashCode, no float, no dictionary iteration for a decision.
// Authority: none (every peer derives the same trim from (seed, floor)).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum TrimLayer : byte { Wall = 0, Overhead = 1 }
    public enum TrimKind : byte { Plaque = 0, Hook = 1, WallShelf = 2, Beam = 3, Hanging = 4 }

    public sealed class TrimRow
    {
        public readonly string Id, Variant;
        public readonly TrimKind Kind; public readonly int RoomIndex, CellX, CellY;
        public readonly CellSide Wall;        // Wall layer: the closed side it hangs on; Overhead: North (unused)
        public readonly int BaseCm, TopCm, DepthCm;
        public TrimRow(string id, TrimKind kind, string variant, int roomIndex, int cellX, int cellY, CellSide wall, int baseCm, int topCm, int depthCm)
        { Id = id; Kind = kind; Variant = variant; RoomIndex = roomIndex; CellX = cellX; CellY = cellY; Wall = wall; BaseCm = baseCm; TopCm = topCm; DepthCm = depthCm; }
        public TrimLayer Layer => Kind == TrimKind.Beam || Kind == TrimKind.Hanging ? TrimLayer.Overhead : TrimLayer.Wall;
    }

    public sealed class CellarFloorTrim
    {
        public const string StreamName = "cellars.trim";
        public const int WallMinBaseCm = 100, WallMaxDepthCm = 40, OverheadMinBaseCm = 200;
        public const int MaxWallPerRoom = 4, MaxOverheadPerRoom = 3;

        static readonly TrimKind[] WallKinds = { TrimKind.Plaque, TrimKind.Hook, TrimKind.WallShelf };
        static readonly TrimKind[] OverheadKinds = { TrimKind.Beam, TrimKind.Hanging };
        static readonly string[][] Variants = { new[] { "Crest", "Sign" }, new[] { "Ladle", "Keys" }, new[] { "Jars", "Books" }, new[] { "Plain", "Cobweb" }, new[] { "Herbs", "Sausages" } };
        // base, top, depth in cm per kind
        static readonly int[][] Sizes = { new[] { 140, 190, 6 }, new[] { 160, 200, 20 }, new[] { 120, 150, 30 }, new[] { 270, 300, 25 }, new[] { 200, 300, 30 } };

        static readonly CellSide[] AllSides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        public readonly uint Seed; public readonly int Floor;
        public readonly IReadOnlyList<TrimRow> Rows;
        CellarFloorTrim(uint seed, int floor, List<TrimRow> rows) { Seed = seed; Floor = floor; Rows = rows; }

        public int CountInRoom(int roomIndex, TrimLayer layer) { int n = 0; foreach (TrimRow r in Rows) if (r.RoomIndex == roomIndex && r.Layer == layer) n++; return n; }
        public int CountInRoom(int roomIndex) { int n = 0; foreach (TrimRow r in Rows) if (r.RoomIndex == roomIndex) n++; return n; }

        public ulong TrimHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(Rows.Count);
            foreach (TrimRow r in Rows) { h.Append(r.Id); h.AppendInt32LE((int)r.Kind); h.Append(r.Variant); h.AppendInt32LE(r.RoomIndex); h.AppendInt32LE(r.CellX); h.AppendInt32LE(r.CellY); h.AppendInt32LE((int)r.Wall); }
            return h.Value;
        }

        // The cells (world grid) no wall or overhead piece may use, and the cells in front of a door (inside the room): the shared legality rule of the trim and the warm lights.
        internal static bool[,] BannedCells(CellarFloorLayout layout, CellarFloorDressing dressing, CellarRoomChunks chunks)
        {
            int n = CellarFloorLayout.GridSize; var ban = new bool[n, n];
            void Ban(int x, int y) { if (x >= 0 && y >= 0 && x < n && y < n) ban[x, y] = true; }
            foreach (FloorChest c in layout.Chests) Ban(c.X, c.Y);
            CellPos? heal = CellarHealing.PointFor(layout, dressing); if (heal.HasValue) Ban(heal.Value.X, heal.Value.Y);
            foreach (PlacedVignette v in dressing.Vignettes) Ban(v.Cell.X, v.Cell.Y);
            if (chunks != null)
                foreach (ResolvedChunk ch in chunks.Chunks)
                    if (!ch.IsOpen) foreach (var (cell, _) in CellarRoomChunks.SolidCells(layout, ch)) Ban(cell.X, cell.Y);
            foreach (PlacedRoom p in layout.Rooms)
            {
                foreach (ChestSlot c in p.Room.Chests) Ban(p.X + c.X, p.Y + c.Y);
                foreach (CellDoor d in p.Room.Doors)
                {
                    Ban(p.X + d.X, p.Y + d.Y);
                    CellPos inFront = new CellPos(p.X + d.X, p.Y + d.Y).Step(CellarRoom.Opposite(d.Side)); Ban(inFront.X, inFront.Y);   // the door's lane
                }
            }
            return ban;
        }

        // A closed wall face: the neighbour on that side is outside the room or Solid, and the side carries no door of this cell.
        internal static bool WallSide(PlacedRoom p, int lx, int ly, CellSide s)
        {
            var (dx, dy) = CellarRoom.Step(s); int nx = lx + dx, ny = ly + dy;
            if (p.Room.InBounds(nx, ny) && p.Room.CellAt(nx, ny) != CellKind.Solid) return false;
            foreach (CellDoor d in p.Room.Doors) if (d.X == lx && d.Y == ly && d.Side == s) return false;
            return true;
        }

        // True for the rooms that take trim: floors 1-3, every kind except the corridor pieces.
        internal static bool Takes(CellarFloorLayout layout, PlacedRoom p) => !layout.IsStub && p.Room.Kind != RoomKind.Corridor;

        // The (cell, wall) faces already taken by a filled wall-mounted socket: the socket's wall is the opposite of its facing.
        internal static bool SocketOnWall(CellarFloorDressing dressing, int roomIndex, int worldX, int worldY, CellSide wall)
        {
            foreach (DressedSocket s in dressing.SocketsOf(roomIndex))
                if (s.Filled && s.Socket.WallMounted && s.Cell.X == worldX && s.Cell.Y == worldY && CellarRoom.Opposite(s.Facing) == wall) return true;
            return false;
        }

        public static CellarFloorTrim Resolve(CellarFloorLayout layout, CellarFloorDressing dressing, CellarRoomChunks chunks = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (dressing == null) throw new ArgumentNullException(nameof(dressing));
            var rows = new List<TrimRow>();
            bool[,] ban = BannedCells(layout, dressing, chunks);
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r]; CellarRoom room = p.Room;
                if (!Takes(layout, p)) continue;
                var rng = new Pcg32(Rng.Derive(layout.Seed, StreamName, ((ulong)layout.Floor << 32) | (uint)r));
                int floorCount = room.FloorCount; if (floorCount == 0) continue;
                int wantWall = Math.Min(MaxWallPerRoom, Math.Max(1, (floorCount + rng.Range(0, 5)) / 5));
                int wantOver = Math.Min(MaxOverheadPerRoom, Math.Max(1, (floorCount + rng.Range(0, 6)) / 6));

                var wallCands = new List<(int x, int y, int side)>(); var overCands = new List<(int x, int y)>();
                for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++)
                {
                    if (room.CellAt(x, y) != CellKind.Floor || ban[p.X + x, p.Y + y]) continue;
                    foreach (CellSide sd in AllSides) if (WallSide(p, x, y, sd) && !SocketOnWall(dressing, r, p.X + x, p.Y + y, sd)) wallCands.Add((x, y, (int)sd));
                    bool hung = false;   // a filled lantern or roots socket already hangs here
                    foreach (DressedSocket s in dressing.SocketsOf(r)) if (s.Filled && (s.Kind == SocketKind.Lantern || s.Kind == SocketKind.Roots) && s.Cell.X == p.X + x && s.Cell.Y == p.Y + y) hung = true;
                    if (!hung) overCands.Add((x, y));
                }
                ShuffleWall(wallCands, ref rng); Shuffle(overCands, ref rng);
                var usedWallCell = new bool[p.Width, p.Height]; int placed = 0;
                foreach (var c in wallCands)
                {
                    if (placed >= wantWall) break;
                    if (usedWallCell[c.x, c.y]) continue;
                    TrimKind k = WallKinds[rng.Range(0, WallKinds.Length)]; string v = Variants[(int)k][rng.Range(0, 2)];
                    int[] sz = Sizes[(int)k];
                    rows.Add(new TrimRow("r" + r + "_w" + placed, k, v, r, p.X + c.x, p.Y + c.y, (CellSide)c.side, sz[0], sz[1], sz[2]));
                    usedWallCell[c.x, c.y] = true; placed++;
                }
                placed = 0;
                foreach (var c in overCands)
                {
                    if (placed >= wantOver) break;
                    TrimKind k = OverheadKinds[rng.Range(0, OverheadKinds.Length)]; string v = Variants[(int)k][rng.Range(0, 2)];
                    int[] sz = Sizes[(int)k];
                    rows.Add(new TrimRow("r" + r + "_o" + placed, k, v, r, p.X + c.x, p.Y + c.y, CellSide.North, sz[0], sz[1], sz[2]));
                    placed++;
                }
            }
            return new CellarFloorTrim(layout.Seed, layout.Floor, rows);
        }

        static void ShuffleWall(List<(int x, int y, int side)> l, ref Pcg32 rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
        }

        static void Shuffle(List<(int x, int y)> l, ref Pcg32 rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
        }
    }
}
