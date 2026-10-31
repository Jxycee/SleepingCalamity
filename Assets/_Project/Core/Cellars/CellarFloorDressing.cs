// Core/Cellars/CellarFloorDressing.cs: the resolved dressing of one Cellars floor (variety layers 1-5): for every socket of every placed room whether it is filled, which
// variant and which of its candidate cells (world grid coordinates, after rotation), plus the floor's atmosphere and the vignettes with their rooms and cells.
// The layout (geometry, LayoutHash) is untouched; the dressing is a second pure function of the same (seed, floor):
//   - each room draws from its own stream Rng.Derive(seed, "cellars.dressing", floor << 32 | roomIndex): per socket, in order, a fill roll, a variant and a cell,
//     all three always drawn, so changing one socket's chance never shifts another's variant;
//   - Lantern sockets are filled at chance * atmosphere lamp percent (entry, exit and checkpoint lanterns always stand);
//   - vignettes come from VignetteDive.Select (replayed from the seed) and are placed on their own streams in combat rooms, away from doors, spawns, chests,
//     socket cells, and never on a cell whose blocking would cut a floor cell or a door off (barrels and crates already resolved count as blocked).
// No string.GetHashCode, no dictionary or set iteration for a decision, no float. DressingHash() is the cross-peer proof, like CellarFloorLayout.LayoutHash().
// Authority: none (every peer derives the same dressing from (seed, floor)).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    // One socket after the roll. Variant is null when the socket stayed empty.
    public sealed class DressedSocket
    {
        public readonly int RoomIndex; public readonly DressingSocket Socket; public readonly int Option; public readonly CellPos Cell; public readonly string Variant;
        public DressedSocket(int roomIndex, DressingSocket socket, int option, CellPos cell, string variant) { RoomIndex = roomIndex; Socket = socket; Option = option; Cell = cell; Variant = variant; }
        public bool Filled => Variant != null;
        public SocketKind Kind => Socket.Kind;
        public CellSide Facing => Socket.Facing;
    }

    public sealed class PlacedVignette
    {
        public readonly CellarVignette Vignette; public readonly int RoomIndex; public readonly CellPos Cell; public readonly int Turns;   // Turns: clockwise quarter turns of the prop group
        public PlacedVignette(CellarVignette v, int roomIndex, CellPos cell, int turns) { Vignette = v; RoomIndex = roomIndex; Cell = cell; Turns = turns; }
    }

    public sealed class CellarFloorDressing
    {
        public readonly uint Seed; public readonly int Floor;
        public readonly CellarAtmosphere Atmosphere;
        public readonly IReadOnlyList<DressedSocket> Sockets;        // room by room, socket by socket
        public readonly IReadOnlyList<PlacedVignette> Vignettes;
        public readonly int DroppedVignettes;                         // chosen by the dive but without a legal cell on this floor (should stay 0)

        CellarFloorDressing(uint seed, int floor, CellarAtmosphere atm, List<DressedSocket> sockets, List<PlacedVignette> vignettes, int dropped)
        { Seed = seed; Floor = floor; Atmosphere = atm; Sockets = sockets; Vignettes = vignettes; DroppedVignettes = dropped; }

        public int FilledCount => Sockets.Count(s => s.Filled);

        public static CellarFloorDressing Resolve(CellarFloorLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            CellarAtmosphere atm = CellarAtmosphere.Roll(layout);
            var socks = new List<DressedSocket>();
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.dressing", ((ulong)layout.Floor << 32) | (uint)r));
                bool alwaysLit = p.Room.Kind == RoomKind.Entry || p.Room.Kind == RoomKind.StairExit || p.Room.Kind == RoomKind.Checkpoint;
                foreach (DressingSocket k in p.Room.Sockets)
                {
                    int fill = rng.Range(0, 100), variant = rng.Range(0, k.Variants.Count), option = rng.Range(0, k.CellOptions);
                    int chance = k.Chance;
                    if (k.Kind == SocketKind.Lantern && !alwaysLit) chance = chance * atm.LampPercent / 100;
                    CellPos local = k.OptionCell(option);
                    socks.Add(new DressedSocket(r, k, option, p.Cell(local.X, local.Y), fill < chance ? k.Variants[variant] : null));
                }
            }
            var vigs = new List<PlacedVignette>(); int dropped = 0;
            foreach (CellarVignette v in VignetteDive.Select(layout.Seed, layout.Floor).Chosen)
            {
                PlacedVignette pv = Place(layout, v, socks, vigs);
                if (pv != null) vigs.Add(pv); else dropped++;
            }
            return new CellarFloorDressing(layout.Seed, layout.Floor, atm, socks, vigs, dropped);
        }

        // ---- vignette placement ----

        static PlacedVignette Place(CellarFloorLayout layout, CellarVignette v, List<DressedSocket> socks, List<PlacedVignette> placed)
        {
            int poolIndex = 0; for (int i = 0; i < CellarVignettePool.All.Count; i++) if (CellarVignettePool.All[i] == v) poolIndex = i;
            var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.vignette.place", ((ulong)layout.Floor << 32) | (uint)poolIndex));
            int spin = rng.Range(0, 4);
            // Eight passes: rooms without a vignette yet, then (only when none had a legal cell) rooms that already hold one; in each, strict rules then relaxed,
            // over the unlocked combat rooms first and the locked room second; each tier is shuffled on its own. The rooms are dense (nearly every cell carries a
            // spawn, chest, door or socket), so the strict pass rarely finds a cell and the relaxed one does the work.
            for (int pass = 0; pass < 8; pass++)
            {
                int tier = pass & 1; bool strict = ((pass >> 1) & 1) == 0, shared = pass >= 4;
                var cands = new List<(int room, int x, int y)>();
                for (int r = 0; r < layout.Rooms.Count; r++)
                {
                    PlacedRoom p = layout.Rooms[r];
                    if (p.Room.Kind != RoomKind.Combat || (r == layout.LockedRoomIndex) != (tier == 1)) continue;
                    if (!shared && placed.Any(q => q.RoomIndex == r)) continue;
                    for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++) cands.Add((r, x, y));
                }
                for (int i = cands.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = cands[i]; cands[i] = cands[j]; cands[j] = t; }
                foreach (var c in cands)
                {
                    PlacedRoom p = layout.Rooms[c.room];
                    int turns = spin;
                    if (!Legal(p, c.room, c.x, c.y, socks, placed, v.WallHug, strict, out int wallTurns)) continue;
                    if (v.WallHug) turns = wallTurns;
                    return new PlacedVignette(v, c.room, p.Cell(c.x, c.y), turns);
                }
            }
            return null;
        }

        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        // May a vignette stand on local cell (x, y) of the room? wallTurns: the quarter turns that put the group's back (south) to a wall of the cell.
        static bool Legal(PlacedRoom p, int roomIndex, int x, int y, List<DressedSocket> socks, List<PlacedVignette> placed, bool needWall, bool strict, out int wallTurns)
        {
            wallTurns = 0; CellarRoom room = p.Room;
            if (room.CellAt(x, y) != CellKind.Floor) return false;
            // never on a door, chest, feature or a cell a barrel or crate may stand in; strict also keeps off the cells beside doors, spawns and wall-mounted sockets
            foreach (CellDoor d in room.Doors) if (Math.Abs(d.X - x) + Math.Abs(d.Y - y) <= (strict ? 1 : 0)) return false;
            foreach (CellarSpawn s in room.Spawns) if (strict && s.X == x && s.Y == y) return false;
            foreach (ChestSlot c in room.Chests) if (c.X == x && c.Y == y) return false;
            foreach (RoomFeature f in room.Features) if (f.X == x && f.Y == y) return false;
            foreach (DressingSocket k in room.Sockets)
                for (int o = 0; o < k.CellOptions; o++)
                {
                    CellPos c = k.OptionCell(o);
                    if (c.X == x && c.Y == y && (strict || !k.WallMounted)) return false;
                }
            if (needWall)
            {
                bool found = false;
                for (int t = 0; t < 4 && !found; t++)   // the first turn count (0..3) whose south-after-turning side is a wall
                {
                    var (dx, dy) = CellarRoom.Step(CellarRoom.Rotate(CellSide.South, t)); int nx = x + dx, ny = y + dy;
                    if (!room.InBounds(nx, ny) || room.CellAt(nx, ny) != CellKind.Floor) { wallTurns = t; found = true; }
                }
                if (!found) return false;
            }
            // connectivity: with this cell and the filled free-standing props blocked, every floor cell and every door must still be reachable
            var blocked = new bool[room.Width, room.Height]; blocked[x, y] = true;
            foreach (DressedSocket s in socks)
                if (s.RoomIndex == roomIndex && s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate)) blocked[s.Cell.X - p.X, s.Cell.Y - p.Y] = true;
            foreach (PlacedVignette o in placed)   // another vignette already stands in this room: its cell is taken and blocked
                if (o.RoomIndex == roomIndex) { if (o.Cell.X == p.X + x && o.Cell.Y == p.Y + y) return false; blocked[o.Cell.X - p.X, o.Cell.Y - p.Y] = true; }
            CellDoor start = null; foreach (CellDoor d in room.Doors) if (!blocked[d.X, d.Y]) { start = d; break; }
            if (start == null) return false;
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
            for (int cx = 0; cx < room.Width; cx++) for (int cy = 0; cy < room.Height; cy++) if (room.CellAt(cx, cy) == CellKind.Floor && !blocked[cx, cy] && !seen[cx, cy]) return false;
            return true;
        }

        // ---- queries and hashes ----

        public IEnumerable<DressedSocket> SocketsOf(int roomIndex) => Sockets.Where(s => s.RoomIndex == roomIndex);

        // A hash of one room's resolved sockets alone: per socket the chosen option index and variant (not the world cell, so a room that was only placed or
        // turned differently does not count as looking different). Compares how often the same room is dressed differently between floors.
        public ulong RoomSignature(int roomIndex)
        {
            var h = Fnv1a64.Create();
            foreach (DressedSocket s in SocketsOf(roomIndex)) { h.Append(s.Socket.Id); h.AppendInt32LE(s.Option); h.Append(s.Variant ?? "-"); }
            return h.Value;
        }

        static void AppendSocket(ref Fnv1a64 h, DressedSocket s)
        {
            h.Append(s.Socket.Id); h.AppendInt32LE(s.Option); h.AppendInt32LE(s.Cell.X); h.AppendInt32LE(s.Cell.Y); h.Append(s.Variant ?? "-");
        }

        // Stable across machines (FNV-1a 64 over an ordered serialization). Separate from CellarFloorLayout.LayoutHash(), which only covers geometry.
        public ulong DressingHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); Atmosphere.AppendTo(ref h);
            h.AppendInt32LE(Sockets.Count);
            foreach (DressedSocket s in Sockets) { h.AppendInt32LE(s.RoomIndex); AppendSocket(ref h, s); }
            h.AppendInt32LE(Vignettes.Count);
            foreach (PlacedVignette v in Vignettes) { h.Append(v.Vignette.Id); h.AppendInt32LE(v.RoomIndex); h.AppendInt32LE(v.Cell.X); h.AppendInt32LE(v.Cell.Y); h.AppendInt32LE(v.Turns); }
            h.AppendInt32LE(DroppedVignettes);
            return h.Value;
        }
    }
}
