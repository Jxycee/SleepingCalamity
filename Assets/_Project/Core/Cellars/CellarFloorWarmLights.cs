// Core/Cellars/CellarFloorWarmLights.cs: secondary warm light sockets of one Cellars floor (procgen item 6c; floor art follow-up "add the corner's secondary warm sources"): up to
// MaxPerRoom small candle/sconce lights per room beyond the main lanterns, set on a wall face near the clutter. A pure function of (seed, floor) after the layout, dressing,
// chunks, clutter and trim; it reads them and changes none.
//   - Budget: a room holds at most RoomLightCap lights in all. ExistingLights counts what the builder already places there (filled wall lanterns, the hanging centre lamp, the
//     warm fill, a candle cluster per chest, the odd light); the room gets min(MaxPerRoom, RoomLightCap - existing) new ones at most (a crowded room gets none). URP caps the
//     additional lights per object, so the cap stays well under that limit; the builder places a light only for a row this resolver returned.
//   - Rule: a floor cell with a closed wall face, off the trim legality bans (door, door lane, chest, healing point, vignette, chunk solid), not under a filled lantern, not on a wall face
//     that already carries a trim piece or a wall-mounted socket; cells that hold wall clutter or sit beside a filled bulky socket come first; two lights of one room never share a cell
//     or touch.
//   - Stream: Rng.Derive(seed, "cellars.warmlight", floor << 32 | roomIndex). Corridors and the floor-4 stub get none.
// Authority: none (every peer derives the same lights from (seed, floor)).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class WarmLightRow
    {
        public readonly string Id, Variant; public readonly int RoomIndex, CellX, CellY, HeightCm; public readonly CellSide Wall;
        public WarmLightRow(string id, string variant, int roomIndex, int cellX, int cellY, CellSide wall, int heightCm)
        { Id = id; Variant = variant; RoomIndex = roomIndex; CellX = cellX; CellY = cellY; Wall = wall; HeightCm = heightCm; }
    }

    public sealed class CellarFloorWarmLights
    {
        public const string StreamName = "cellars.warmlight";
        public const int RoomLightCap = 6, MaxPerRoom = 2, BigRoomCells = 12, HeightCm = 140;
        static readonly string[] Names = { "Candle", "Sconce" };
        static readonly CellSide[] AllSides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        public readonly uint Seed; public readonly int Floor;
        public readonly IReadOnlyList<WarmLightRow> Rows;
        CellarFloorWarmLights(uint seed, int floor, List<WarmLightRow> rows) { Seed = seed; Floor = floor; Rows = rows; }

        public int CountInRoom(int roomIndex) { int n = 0; foreach (WarmLightRow r in Rows) if (r.RoomIndex == roomIndex) n++; return n; }

        public ulong LightsHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(Rows.Count);
            foreach (WarmLightRow r in Rows) { h.Append(r.Id); h.Append(r.Variant); h.AppendInt32LE(r.RoomIndex); h.AppendInt32LE(r.CellX); h.AppendInt32LE(r.CellY); h.AppendInt32LE((int)r.Wall); }
            return h.Value;
        }

        // The lights CellarFloorBuilder already puts in a room (mirrors Lamps, the warm fill, the chest candle clusters and the odd light).
        public static int ExistingLights(CellarFloorLayout layout, CellarFloorDressing dressing, int roomIndex)
        {
            PlacedRoom p = layout.Rooms[roomIndex]; int n = 0; bool hasSocket = false;
            foreach (DressedSocket s in dressing.SocketsOf(roomIndex))
            {
                if (s.Kind != SocketKind.Lantern) continue;
                hasSocket = true; if (s.Filled) n++;
            }
            int wall = n;
            bool centre = !(hasSocket && (wall == 0 || p.Room.FloorCount < BigRoomCells)) && !(dressing.Atmosphere.Dark && p.Room.Kind == RoomKind.Combat);
            if (centre) n++;
            n++;   // the room's warm fill
            foreach (FloorChest c in layout.Chests) if (c.RoomIndex == roomIndex) n++;
            if (dressing.Atmosphere.OddLightRoom == roomIndex) n++;
            return n;
        }

        public static CellarFloorWarmLights Resolve(CellarFloorLayout layout, CellarFloorDressing dressing, CellarRoomChunks chunks, CellarFloorClutter clutter, CellarFloorTrim trim)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (dressing == null) throw new ArgumentNullException(nameof(dressing));
            var rows = new List<WarmLightRow>();
            bool[,] ban = CellarFloorTrim.BannedCells(layout, dressing, chunks);
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r]; CellarRoom room = p.Room;
                if (!CellarFloorTrim.Takes(layout, p)) continue;
                var rng = new Pcg32(Rng.Derive(layout.Seed, StreamName, ((ulong)layout.Floor << 32) | (uint)r));
                int floorCount = room.FloorCount; if (floorCount == 0) continue;
                int dropRoll = rng.Range(0, 100);
                int allowed = Math.Max(0, Math.Min(MaxPerRoom, RoomLightCap - ExistingLights(layout, dressing, r)));
                int want = Math.Min(allowed, 1 + (floorCount >= 10 ? 1 : 0) - (dropRoll < 20 ? 1 : 0));
                if (want <= 0) continue;

                var clutterCell = new bool[p.Width, p.Height];
                if (clutter != null) foreach (CellarClutterRow c in clutter.Rows) if (c.RoomIndex == r) clutterCell[c.CellX - p.X, c.CellY - p.Y] = true;
                foreach (DressedSocket s in dressing.SocketsOf(r))
                    if (s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate || s.Kind == SocketKind.Shelf || s.Kind == SocketKind.Chute))
                        foreach (CellSide sd in AllSides)
                        {
                            var (dx, dy) = CellarRoom.Step(sd); int bx = s.Cell.X - p.X + dx, by = s.Cell.Y - p.Y + dy;
                            if (room.InBounds(bx, by)) clutterCell[bx, by] = true;
                        }
                var tier0 = new List<(int x, int y, int side)>(); var tier1 = new List<(int x, int y, int side)>();
                for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++)
                {
                    if (room.CellAt(x, y) != CellKind.Floor || ban[p.X + x, p.Y + y]) continue;
                    bool lantern = false;
                    foreach (DressedSocket s in dressing.SocketsOf(r)) if (s.Filled && s.Kind == SocketKind.Lantern && s.Cell.X == p.X + x && s.Cell.Y == p.Y + y) lantern = true;
                    if (lantern) continue;
                    foreach (CellSide sd in AllSides)
                    {
                        if (!CellarFloorTrim.WallSide(p, x, y, sd) || CellarFloorTrim.SocketOnWall(dressing, r, p.X + x, p.Y + y, sd)) continue;
                        bool trimmed = false;
                        if (trim != null) foreach (TrimRow t in trim.Rows) if (t.RoomIndex == r && t.Layer == TrimLayer.Wall && t.CellX == p.X + x && t.CellY == p.Y + y && t.Wall == sd) trimmed = true;
                        if (trimmed) continue;
                        (clutterCell[x, y] ? tier0 : tier1).Add((x, y, (int)sd));
                    }
                }
                Shuffle(tier0, ref rng); Shuffle(tier1, ref rng);
                var cands = new List<(int x, int y, int side)>(tier0); cands.AddRange(tier1);
                var pick = new List<(int x, int y, int side)>();
                foreach (var c in cands)
                {
                    if (pick.Count >= want) break;
                    bool near = false; foreach (var q in pick) if (Math.Abs(q.x - c.x) + Math.Abs(q.y - c.y) < 2) near = true;
                    if (near) continue;
                    pick.Add(c);
                }
                for (int i = 0; i < pick.Count; i++)
                    rows.Add(new WarmLightRow("r" + r + "_l" + i, Names[rng.Range(0, Names.Length)], r, p.X + pick[i].x, p.Y + pick[i].y, (CellSide)pick[i].side, HeightCm));
            }
            return new CellarFloorWarmLights(layout.Seed, layout.Floor, rows);
        }

        static void Shuffle(List<(int x, int y, int side)> l, ref Pcg32 rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; }
        }
    }
}
