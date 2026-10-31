// Core/Cellars/CellarFloorLayout.cs: the result of the M5 floor generator (PHASE1 7.2): one Undercroft Cellars floor on a 14 x 14 grid of 4 m cells.
// Pure C#, no engine references. A layout is plain data: the placed (rotated) rooms, the corridor cells with their 4-bit neighbour mask and the piece
// that mask resolves to, the room-to-room links (one of them may be the locked door), the key chest, the gate-key carrier spawn and the chests.
// Coordinates: x grows east, y grows south (row 0 = north, like the room ASCII); a room's origin is its north-west cell. Authority: none (static data;
// the host sends only (seed, floor) and every client rebuilds the same layout, which LayoutHash() lets a test or a netcode check compare).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SleepingCalamity.Core
{
    public readonly struct CellPos
    {
        public readonly int X, Y;
        public CellPos(int x, int y) { X = x; Y = y; }
        public CellPos Step(CellSide s) { var (dx, dy) = CellarRoom.Step(s); return new CellPos(X + dx, Y + dy); }
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    // A room turned `Rotation` quarter turns clockwise (Room is already the rotated copy) with its north-west cell at (X, Y).
    public sealed class PlacedRoom
    {
        public readonly CellarRoom Room; public readonly int Rotation, X, Y;
        public PlacedRoom(CellarRoom rotated, int rotation, int x, int y) { Room = rotated; Rotation = rotation; X = x; Y = y; }
        public string RoomId => Room.Id;
        public int Width => Room.Width;
        public int Height => Room.Height;
        public CellPos Cell(int localX, int localY) => new CellPos(X + localX, Y + localY);
        public CellPos DoorCell(int doorIndex) { CellDoor d = Room.Doors[doorIndex]; return new CellPos(X + d.X, Y + d.Y); }
        // The corridor cell just outside the door.
        public CellPos DoorOutside(int doorIndex) => DoorCell(doorIndex).Step(Room.Doors[doorIndex].Side);
    }

    // One corridor cell: Mask is the 4-bit neighbour mask (N 1, E 2, S 4, W 8), Piece the corridor room it resolves to and Turns the clockwise quarter
    // turns that piece is rotated by (CellarRoomPool.FindCorridor).
    public sealed class PlacedCorridor
    {
        public readonly int X, Y, Mask, Turns; public readonly CellarRoom Piece;
        public PlacedCorridor(int x, int y, int mask, CellarRoom piece, int turns) { X = x; Y = y; Mask = mask; Piece = piece; Turns = turns; }
    }

    // A door-to-door connection between two rooms through corridor cells (Path runs from the cell outside door A to the cell outside door B).
    public sealed class FloorLink
    {
        public readonly int RoomA, DoorA, RoomB, DoorB; public readonly bool Locked; public readonly IReadOnlyList<CellPos> Path;
        public FloorLink(int roomA, int doorA, int roomB, int doorB, bool locked, IEnumerable<CellPos> path)
        { RoomA = roomA; DoorA = doorA; RoomB = roomB; DoorB = doorB; Locked = locked; Path = path.ToList(); }
        public bool Touches(int room) => RoomA == room || RoomB == room;
    }

    // A chest on the floor: in a room's chest slot (RoomIndex >= 0) or at the end of a side dead end (RoomIndex -1). HoldsKey marks the key chest.
    public sealed class FloorChest
    {
        public readonly int X, Y, RoomIndex; public readonly bool HoldsKey;
        public FloorChest(int x, int y, int roomIndex, bool holdsKey) { X = x; Y = y; RoomIndex = roomIndex; HoldsKey = holdsKey; }
    }

    // The one enemy spawn that carries the gate key to the exit.
    public sealed class GateKeyCarrier
    {
        public readonly int RoomIndex, SpawnIndex, X, Y;
        public GateKeyCarrier(int roomIndex, int spawnIndex, int x, int y) { RoomIndex = roomIndex; SpawnIndex = spawnIndex; X = x; Y = y; }
    }

    public sealed class CellarFloorLayout
    {
        public const int GridSize = 14, CellMeters = 4;

        public readonly uint Seed; public readonly int Floor;
        public readonly bool IsStub;                      // floor 4: the fixed placeholder, exempt from the count rules of floors 1-3
        public readonly SubTheme Theme;
        public readonly IReadOnlyList<PlacedRoom> Rooms;
        public readonly IReadOnlyList<PlacedCorridor> Corridors;   // row-major (y, then x)
        public readonly IReadOnlyList<FloorLink> Links;
        public readonly IReadOnlyList<CellPos> DeadEnds;           // the end cell of each side dead end
        public readonly IReadOnlyList<FloorChest> Chests;
        public readonly int LockedRoomIndex = -1;         // -1 when the floor has no locked room
        public readonly int LockedLinkIndex = -1;         // index into Links of the locked door's link
        public readonly int KeyChestIndex = -1;           // index into Chests
        public readonly GateKeyCarrier Carrier;           // null on the stub
        public readonly IReadOnlyList<CellPos> EntryStarts;
        public readonly CellPos? StairExit, BankCell;
        public bool HasCheckpoint => BankCell.HasValue;
        public bool HasLockedRoom => LockedRoomIndex >= 0;
        public FloorChest KeyChest => KeyChestIndex < 0 ? null : Chests[KeyChestIndex];

        public CellarFloorLayout(uint seed, int floor, bool isStub, SubTheme theme, IEnumerable<PlacedRoom> rooms, IEnumerable<PlacedCorridor> corridors,
            IEnumerable<FloorLink> links, IEnumerable<CellPos> deadEnds, IEnumerable<FloorChest> chests, int lockedRoomIndex, int lockedLinkIndex,
            int keyChestIndex, GateKeyCarrier carrier)
        {
            Seed = seed; Floor = floor; IsStub = isStub; Theme = theme;
            Rooms = rooms.ToList(); Corridors = corridors.ToList(); Links = links.ToList(); DeadEnds = deadEnds.ToList(); Chests = chests.ToList();
            LockedRoomIndex = lockedRoomIndex; LockedLinkIndex = lockedLinkIndex; KeyChestIndex = keyChestIndex; Carrier = carrier;
            var starts = new List<CellPos>();
            foreach (PlacedRoom p in Rooms)
                foreach (RoomFeature f in p.Room.Features)
                {
                    if (f.Kind == FeatureKind.EntryStart) starts.Add(p.Cell(f.X, f.Y));
                    else if (f.Kind == FeatureKind.Stair) StairExit = p.Cell(f.X, f.Y);
                    else if (f.Kind == FeatureKind.Bank) BankCell = p.Cell(f.X, f.Y);
                }
            EntryStarts = starts;
        }

        public PlacedCorridor CorridorAt(int x, int y)
        {
            foreach (PlacedCorridor c in Corridors) if (c.X == x && c.Y == y) return c;
            return null;
        }

        // Index of the room whose rectangle holds the cell, or -1.
        public int RoomAt(int x, int y)
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                PlacedRoom p = Rooms[i];
                if (x >= p.X && y >= p.Y && x < p.X + p.Width && y < p.Y + p.Height) return i;
            }
            return -1;
        }

        // True when the corridor cell outside the door has a mask bit pointing back at the door (the door is open; otherwise it is walled).
        public bool DoorIsOpen(int roomIndex, int doorIndex)
        {
            PlacedRoom p = Rooms[roomIndex]; CellPos o = p.DoorOutside(doorIndex);
            PlacedCorridor c = CorridorAt(o.X, o.Y);
            return c != null && (c.Mask & CellarRoom.MaskBit(CellarRoom.Opposite(p.Room.Doors[doorIndex].Side))) != 0;
        }

        // ---- hashes ----

        // Stable across machines: FNV-1a 64 over an ordered serialization (little-endian ints, UTF-8 ids), no engine or platform hashing involved.
        public ulong LayoutHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(IsStub ? 1 : 0);
            h.AppendInt32LE(Rooms.Count);
            foreach (PlacedRoom p in Rooms) { h.Append(p.RoomId); h.AppendInt32LE(p.Rotation); h.AppendInt32LE(p.X); h.AppendInt32LE(p.Y); }
            h.AppendInt32LE(Corridors.Count);
            foreach (PlacedCorridor c in Corridors) { h.AppendInt32LE(c.X); h.AppendInt32LE(c.Y); h.AppendInt32LE(c.Mask); h.Append(c.Piece.Id); h.AppendInt32LE(c.Turns); }
            h.AppendInt32LE(Links.Count);
            foreach (FloorLink l in Links) { h.AppendInt32LE(l.RoomA); h.AppendInt32LE(l.DoorA); h.AppendInt32LE(l.RoomB); h.AppendInt32LE(l.DoorB); h.AppendInt32LE(l.Locked ? 1 : 0); }
            h.AppendInt32LE(LockedRoomIndex); h.AppendInt32LE(LockedLinkIndex); h.AppendInt32LE(KeyChestIndex);
            h.AppendInt32LE(Chests.Count);
            foreach (FloorChest c in Chests) { h.AppendInt32LE(c.X); h.AppendInt32LE(c.Y); h.AppendInt32LE(c.RoomIndex); h.AppendInt32LE(c.HoldsKey ? 1 : 0); }
            if (Carrier == null) h.AppendInt32LE(-1);
            else { h.AppendInt32LE(Carrier.RoomIndex); h.AppendInt32LE(Carrier.SpawnIndex); h.AppendInt32LE(Carrier.X); h.AppendInt32LE(Carrier.Y); }
            return h.Value;
        }

        // The floor's silhouette: a hash of which grid cells are occupied (room cells that are not solid, plus corridor cells), row by row. Two floors with
        // the same footprint look the same from above whatever the dressing.
        public ulong FootprintHash()
        {
            var occ = new bool[GridSize, GridSize];
            foreach (PlacedRoom p in Rooms)
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++)
                    if (p.Room.CellAt(x, y) != CellKind.Solid) occ[p.X + x, p.Y + y] = true;
            foreach (PlacedCorridor c in Corridors) occ[c.X, c.Y] = true;
            var h = Fnv1a64.Create();
            for (int y = 0; y < GridSize; y++) for (int x = 0; x < GridSize; x++) h.Append((byte)(occ[x, y] ? 1 : 0));
            return h.Value;
        }

        // ---- debug dump ----

        // 14 lines of 14 characters. Rooms use the room ASCII (legend in CellarRoom.cs; D open door, d walled door, L locked door); corridors show - | for
        // straights, o for a dead end, + for corners, T junctions and crossings; K the key chest, G the gate-key carrier's spawn.
        public string ToAscii()
        {
            var g = new char[GridSize, GridSize];
            for (int y = 0; y < GridSize; y++) for (int x = 0; x < GridSize; x++) g[x, y] = ' ';
            for (int i = 0; i < Rooms.Count; i++)
            {
                PlacedRoom p = Rooms[i]; string[] rows = p.Room.ToAscii();
                for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++) g[p.X + x, p.Y + y] = rows[y][x];
                for (int d = 0; d < p.Room.Doors.Count; d++)
                {
                    CellPos c = p.DoorCell(d);
                    bool locked = Links.Any(l => l.Locked && ((l.RoomA == i && l.DoorA == d) || (l.RoomB == i && l.DoorB == d)));
                    g[c.X, c.Y] = locked ? 'L' : DoorIsOpen(i, d) ? 'D' : 'd';
                }
            }
            foreach (PlacedCorridor c in Corridors)
            {
                int bits = 0; for (int m = c.Mask; m != 0; m >>= 1) bits += m & 1;
                g[c.X, c.Y] = bits == 1 ? 'o' : c.Piece.Shape == CorridorShape.Straight ? ((c.Mask & CellarRoom.MaskN) != 0 ? '|' : '-') : '+';
            }
            if (KeyChest != null) g[KeyChest.X, KeyChest.Y] = 'K';
            if (Carrier != null) g[Carrier.X, Carrier.Y] = 'G';
            var sb = new StringBuilder();
            for (int y = 0; y < GridSize; y++) { for (int x = 0; x < GridSize; x++) sb.Append(g[x, y]); sb.Append('\n'); }
            return sb.ToString();
        }
    }
}
