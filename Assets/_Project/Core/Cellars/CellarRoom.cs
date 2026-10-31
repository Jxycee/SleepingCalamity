// Core/Cellars/CellarRoom.cs: the room format for the Undercroft Cellars (PHASE1 7.2, queue item Q7). Pure C#, no engine references, so the M5 floor
// generator (not written yet), the Editor greybox builder and the EditMode tests all read the same data. M5's generator will place these rooms on the
// 14 x 14 grid of 4 m cells (each room's rectangle plus a one-cell margin must be clear) and join their doors with corridor pieces.
//
// A room is a W x H block of cells (4 m each). Authoring is compact ASCII, one character per cell, row 0 = north, column 0 = west:
//    .  floor                      #  solid (wall block; makes L-shapes, notches, rings)       P  pillar (blocks walking, not a wall)
//    D  floor with a door on the room edge it touches (never a corner cell: a corner touches two edges)
//    L  like D, but this door is the LOCKED one when the generator picks this room as the locked room (at most one per room)
//    s  tier-1 spawn point         S  tier-2 spawn point (combat rooms)
//    C  chest slot                 K  chest slot that may hold the floor's key (a room with K never has L: the key is not behind its own lock)
//    E  party start cell (entry room, 4 of them)   X  the stair down (stair-exit room)   B  the bank point (checkpoint room, or the bakery heart)
//    Z  the sleeping baker (the bakery heart room only)
// Dressing sockets are not in the ASCII: each has an id, a kind, a floor cell, a facing and 2-3 named variants, given beside the rows. A socket sits on
// a plain floor cell (no door, spawn, chest or feature on it); wall-mounted kinds (Shelf, Rack, Chute, Lantern, Roots) need a wall behind them (the
// cell opposite their facing is Solid, a Pillar or the room edge, and not a door on that edge); Barrel and Crate stand free.
// Wall-mounted sockets sit against the wall and never block walking; Barrel and Crate stand on the floor and DO block their cell, so Validate also
// requires every other floor cell to stay reachable from every door with those cells blocked.
// Corridor pieces are 1 x 1 rooms whose doors come from a 4-bit neighbour mask (N 1, E 2, S 4, W 8); the pool keeps the six shapes in one canonical
// orientation and the generator rotates them.
// Unused doors are simply walled by the generator; a combat room has at least two so it can sit in a loop or a chain. The locked room: the generator
// puts the lock on the L door and does not connect the room's other doors.
// Validate() is the rule set the EditMode tests and the (later) builder fail on. Authority: none (static data; the host builds floors from a seed).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SleepingCalamity.Core
{
    // Heart: the floor-4 bakery's heart room (the baker's bed, the rescue bank point); only the bakery set-piece (CellarBakeryPool) uses it.
    public enum RoomKind : byte { Combat = 0, Entry = 1, StairExit = 2, Checkpoint = 3, Corridor = 4, Heart = 5 }

    // Which floor's sub-theme a room dresses as. Any = entry, exit, checkpoint and corridor pieces.
    // Bakery: the fixed floor-4 set-piece rooms (CellarBakeryPool); never drawn for floors 1-3.
    public enum SubTheme : byte { Any = 0, Larder = 1, WineVault = 2, CoalCellar = 3, Bakery = 4 }

    public enum CellKind : byte { Floor = 0, Solid = 1, Pillar = 2 }

    public enum CellSide : byte { North = 0, East = 1, South = 2, West = 3 }

    public enum SocketKind : byte { Shelf, Rack, Barrel, Crate, Chute, Lantern, Roots }

    public enum SpawnTier : byte { Tier1 = 1, Tier2 = 2 }

    public enum FeatureKind : byte { EntryStart, Stair, Bank, Baker }

    public enum CorridorShape : byte { None = 0, Straight, Corner, T, Cross, DeadEnd, Door }

    public sealed class CellDoor
    {
        public readonly int X, Y; public readonly CellSide Side; public readonly bool Locked;
        public CellDoor(int x, int y, CellSide side, bool locked = false) { X = x; Y = y; Side = side; Locked = locked; }
    }

    public sealed class DressingSocket
    {
        public readonly string Id; public readonly SocketKind Kind; public readonly int X, Y; public readonly CellSide Facing; public readonly IReadOnlyList<string> Variants;
        public DressingSocket(string id, SocketKind kind, int x, int y, CellSide facing, params string[] variants)
        { Id = id; Kind = kind; X = x; Y = y; Facing = facing; Variants = (variants ?? new string[0]).ToArray(); }
        public bool WallMounted => Kind == SocketKind.Shelf || Kind == SocketKind.Rack || Kind == SocketKind.Chute || Kind == SocketKind.Lantern || Kind == SocketKind.Roots;
    }

    // A socket as authored beside the ASCII rows; the room gives it its id.
    public readonly struct SocketSpec
    {
        public readonly SocketKind Kind; public readonly int X, Y; public readonly CellSide Facing; public readonly string[] Variants;
        public SocketSpec(SocketKind kind, int x, int y, CellSide facing, params string[] variants) { Kind = kind; X = x; Y = y; Facing = facing; Variants = variants; }
    }

    public sealed class CellarSpawn
    {
        public readonly int X, Y; public readonly SpawnTier Tier;
        public CellarSpawn(int x, int y, SpawnTier tier) { X = x; Y = y; Tier = tier; }
    }

    public sealed class ChestSlot
    {
        public readonly int X, Y; public readonly bool KeyCandidate;
        public ChestSlot(int x, int y, bool keyCandidate = false) { X = x; Y = y; KeyCandidate = keyCandidate; }
    }

    public sealed class RoomFeature
    {
        public readonly int X, Y; public readonly FeatureKind Kind;
        public RoomFeature(int x, int y, FeatureKind kind) { X = x; Y = y; Kind = kind; }
    }

    public sealed class CellarRoom
    {
        public const int MaxVariants = 3, MinVariants = 2;
        public const int MaskN = 1, MaskE = 2, MaskS = 4, MaskW = 8;

        public readonly string Id, Name, Silhouette;
        public readonly RoomKind Kind; public readonly SubTheme Theme;
        public readonly int Width, Height;
        public readonly CorridorShape Shape; public readonly int Mask;
        readonly CellKind[,] cells;                       // [x, y]
        public readonly IReadOnlyList<CellDoor> Doors;
        public readonly IReadOnlyList<DressingSocket> Sockets;
        public readonly IReadOnlyList<CellarSpawn> Spawns;
        public readonly IReadOnlyList<ChestSlot> Chests;
        public readonly IReadOnlyList<RoomFeature> Features;
        public readonly IReadOnlyList<string> AuthoredRows;   // the ASCII this room was parsed from (null for rooms built by hand)

        public CellarRoom(string id, string name, RoomKind kind, SubTheme theme, string silhouette, CellKind[,] cells,
            IEnumerable<CellDoor> doors, IEnumerable<DressingSocket> sockets, IEnumerable<CellarSpawn> spawns, IEnumerable<ChestSlot> chests,
            IEnumerable<RoomFeature> features = null, CorridorShape shape = CorridorShape.None, int mask = 0, string[] authoredRows = null)
        {
            AuthoredRows = authoredRows == null ? null : Array.AsReadOnly((string[])authoredRows.Clone());
            Id = id; Name = name; Kind = kind; Theme = theme; Silhouette = silhouette ?? "";
            this.cells = cells; Width = cells.GetLength(0); Height = cells.GetLength(1);
            Doors = (doors ?? new CellDoor[0]).ToList(); Sockets = (sockets ?? new DressingSocket[0]).ToList();
            Spawns = (spawns ?? new CellarSpawn[0]).ToList(); Chests = (chests ?? new ChestSlot[0]).ToList();
            Features = (features ?? new RoomFeature[0]).ToList(); Shape = shape; Mask = mask;
        }

        public CellKind CellAt(int x, int y) => cells[x, y];
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public CellDoor LockedDoor => Doors.FirstOrDefault(d => d.Locked);
        public bool HasKeyChest => Chests.Any(c => c.KeyCandidate);
        public int FloorCount { get { int n = 0; for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++) if (cells[x, y] == CellKind.Floor) n++; return n; } }

        public static (int dx, int dy) Step(CellSide s)
        {
            switch (s) { case CellSide.North: return (0, -1); case CellSide.East: return (1, 0); case CellSide.South: return (0, 1); default: return (-1, 0); }
        }
        public static CellSide Opposite(CellSide s) => (CellSide)(((int)s + 2) & 3);
        public static int MaskBit(CellSide s) => 1 << (int)s;

        // ---- authoring ----

        // Parse the ASCII rows (legend in the file header). Throws FormatException on a ragged grid, an unknown glyph or a door on a corner cell.
        public static CellarRoom Parse(string id, string name, RoomKind kind, SubTheme theme, string silhouette, string[] rows, params SocketSpec[] sockets)
        {
            if (rows == null) throw new FormatException(id + ": no rows");
            int h = rows.Length, w = h == 0 ? 0 : rows[0].Length;
            if (w == 0) throw new FormatException(id + ": empty grid");
            var cells = new CellKind[w, h];
            var doors = new List<CellDoor>(); var spawns = new List<CellarSpawn>(); var chests = new List<ChestSlot>(); var feats = new List<RoomFeature>();
            for (int y = 0; y < h; y++)
            {
                if (rows[y].Length != w) throw new FormatException(id + ": row " + y + " is " + rows[y].Length + " wide, expected " + w);
                for (int x = 0; x < w; x++)
                {
                    char c = rows[y][x];
                    switch (c)
                    {
                        case '.': break;
                        case '#': cells[x, y] = CellKind.Solid; break;
                        case 'P': cells[x, y] = CellKind.Pillar; break;
                        case 'D': case 'L':
                            {
                                bool n = y == 0, s = y == h - 1, west = x == 0, e = x == w - 1;
                                int edges = (n ? 1 : 0) + (s ? 1 : 0) + (west ? 1 : 0) + (e ? 1 : 0);
                                if (edges != 1) throw new FormatException(id + ": door at (" + x + "," + y + ") must touch exactly one room edge");
                                CellSide side = n ? CellSide.North : e ? CellSide.East : s ? CellSide.South : CellSide.West;
                                doors.Add(new CellDoor(x, y, side, c == 'L'));
                                break;
                            }
                        case 's': spawns.Add(new CellarSpawn(x, y, SpawnTier.Tier1)); break;
                        case 'S': spawns.Add(new CellarSpawn(x, y, SpawnTier.Tier2)); break;
                        case 'C': chests.Add(new ChestSlot(x, y)); break;
                        case 'K': chests.Add(new ChestSlot(x, y, true)); break;
                        case 'E': feats.Add(new RoomFeature(x, y, FeatureKind.EntryStart)); break;
                        case 'X': feats.Add(new RoomFeature(x, y, FeatureKind.Stair)); break;
                        case 'B': feats.Add(new RoomFeature(x, y, FeatureKind.Bank)); break;
                        case 'Z': feats.Add(new RoomFeature(x, y, FeatureKind.Baker)); break;
                        default: throw new FormatException(id + ": unknown glyph '" + c + "' at (" + x + "," + y + ")");
                    }
                }
            }
            var socks = new List<DressingSocket>();
            for (int i = 0; i < sockets.Length; i++)
                socks.Add(new DressingSocket(id + ".s" + (i + 1), sockets[i].Kind, sockets[i].X, sockets[i].Y, sockets[i].Facing, sockets[i].Variants));
            return new CellarRoom(id, name, kind, theme, silhouette, cells, doors, socks, spawns, chests, feats, CorridorShape.None, 0, rows);
        }

        // A 1 x 1 corridor piece: one door on cell (0,0) per bit of the neighbour mask.
        public static CellarRoom Corridor(string id, string name, CorridorShape shape, int mask, params SocketSpec[] sockets)
        {
            var doors = new List<CellDoor>();
            foreach (CellSide s in new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West }) if ((mask & MaskBit(s)) != 0) doors.Add(new CellDoor(0, 0, s));
            var socks = sockets.Select((sp, i) => new DressingSocket(id + ".s" + (i + 1), sp.Kind, sp.X, sp.Y, sp.Facing, sp.Variants)).ToList();
            return new CellarRoom(id, name, RoomKind.Corridor, SubTheme.Any, "", new CellKind[1, 1], doors, socks, null, null, null, shape, mask);
        }

        // ---- rotation ----

        // The mask after turning the piece quarter turns clockwise (N to E to S to W).
        public static int RotateMask(int mask, int quarterTurnsCW)
        {
            int q = ((quarterTurnsCW % 4) + 4) % 4; mask &= 15;
            return ((mask << q) | (mask >> (4 - q))) & 15;
        }

        public static CellSide Rotate(CellSide s, int quarterTurnsCW) => (CellSide)((((int)s + quarterTurnsCW) % 4 + 4) % 4);

        // A new room turned clockwise (seen from above, north up) by whole quarter turns: cells, doors and their sides, sockets and their facing, spawns,
        // chests and marks move with it, and width and height swap on odd turns. Id, name and tags are unchanged; AuthoredRows is dropped.
        public CellarRoom Rotated(int quarterTurnsCW)
        {
            int q = ((quarterTurnsCW % 4) + 4) % 4;
            (int x, int y) At(int x, int y)   // one clockwise turn sends (x, y) of a w x h grid to (h - 1 - y, x) of an h x w grid
            {
                int w = Width, h = Height;
                for (int i = 0; i < q; i++) { int nx = h - 1 - y, ny = x; x = nx; y = ny; int t = w; w = h; h = t; }
                return (x, y);
            }
            var nc = new CellKind[q % 2 == 0 ? Width : Height, q % 2 == 0 ? Height : Width];
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++) { var p = At(x, y); nc[p.x, p.y] = cells[x, y]; }
            return new CellarRoom(Id, Name, Kind, Theme, Silhouette, nc,
                Doors.Select(d => { var p = At(d.X, d.Y); return new CellDoor(p.x, p.y, Rotate(d.Side, q), d.Locked); }),
                Sockets.Select(k => { var p = At(k.X, k.Y); return new DressingSocket(k.Id, k.Kind, p.x, p.y, Rotate(k.Facing, q), k.Variants.ToArray()); }),
                Spawns.Select(k => { var p = At(k.X, k.Y); return new CellarSpawn(p.x, p.y, k.Tier); }),
                Chests.Select(k => { var p = At(k.X, k.Y); return new ChestSlot(p.x, p.y, k.KeyCandidate); }),
                Features.Select(k => { var p = At(k.X, k.Y); return new RoomFeature(p.x, p.y, k.Kind); }),
                Shape, RotateMask(Mask, q));
        }

        // The ASCII rows back (sockets are not in the ASCII). Parse(ToAscii()) gives the same grid, doors, spawns, chests and features.
        public string[] ToAscii()
        {
            var rows = new string[Height];
            for (int y = 0; y < Height; y++)
            {
                var sb = new StringBuilder();
                for (int x = 0; x < Width; x++) sb.Append(Glyph(x, y));
                rows[y] = sb.ToString();
            }
            return rows;
        }

        char Glyph(int x, int y)
        {
            CellDoor d = Doors.FirstOrDefault(q => q.X == x && q.Y == y); if (d != null) return d.Locked ? 'L' : 'D';
            CellarSpawn sp = Spawns.FirstOrDefault(q => q.X == x && q.Y == y); if (sp != null) return sp.Tier == SpawnTier.Tier2 ? 'S' : 's';
            ChestSlot ch = Chests.FirstOrDefault(q => q.X == x && q.Y == y); if (ch != null) return ch.KeyCandidate ? 'K' : 'C';
            RoomFeature f = Features.FirstOrDefault(q => q.X == x && q.Y == y);
            if (f != null) return f.Kind == FeatureKind.EntryStart ? 'E' : f.Kind == FeatureKind.Stair ? 'X' : f.Kind == FeatureKind.Baker ? 'Z' : 'B';
            return cells[x, y] == CellKind.Solid ? '#' : cells[x, y] == CellKind.Pillar ? 'P' : '.';
        }

        // ---- rules ----

        public List<string> Validate()
        {
            var errs = new List<string>();
            void Err(string m) => errs.Add(Id + ": " + m);
            if (string.IsNullOrEmpty(Id)) { errs.Add("a room has an empty id"); return errs; }
            if (string.IsNullOrWhiteSpace(Name)) Err("no name");
            if (Width < 1 || Height < 1) { Err("empty grid"); return errs; }

            // size and theme by kind
            int lo = Math.Min(Width, Height), hi = Math.Max(Width, Height);
            if (Kind == RoomKind.Combat)
            {
                if (lo < 2 || lo > 4 || hi < 3 || hi > 5) Err("combat room is " + Width + "x" + Height + ", outside 2x3 to 5x4 (either orientation)");
                if (Theme == SubTheme.Any) Err("combat room needs a sub-theme");
                if (string.IsNullOrWhiteSpace(Silhouette)) Err("combat room needs a silhouette tag");
            }
            else
            {
                if (Kind == RoomKind.Heart ? Theme != SubTheme.Bakery : Theme != SubTheme.Any) Err(Kind + " room must use SubTheme." + (Kind == RoomKind.Heart ? "Bakery" : "Any"));
                if (Kind != RoomKind.Corridor && (lo < 2 || hi > 6)) Err(Kind + " room is " + Width + "x" + Height + ", outside 2..6 per side");
            }

            // doors
            var seenDoors = new HashSet<(int, int, CellSide)>();
            foreach (CellDoor d in Doors)
            {
                string at = "door (" + d.X + "," + d.Y + " " + d.Side + ")";
                if (!InBounds(d.X, d.Y)) { Err(at + " is outside the grid"); continue; }
                bool onEdge = d.Side == CellSide.North ? d.Y == 0 : d.Side == CellSide.South ? d.Y == Height - 1 : d.Side == CellSide.West ? d.X == 0 : d.X == Width - 1;
                if (!onEdge) Err(at + " is not on the room boundary facing outward");
                if (cells[d.X, d.Y] != CellKind.Floor) Err(at + " is not on a Floor cell");
                if (Kind != RoomKind.Corridor && ((d.X == 0 ? 1 : 0) + (d.X == Width - 1 ? 1 : 0) + (d.Y == 0 ? 1 : 0) + (d.Y == Height - 1 ? 1 : 0)) > 1) Err(at + " is on a corner cell");
                if (!seenDoors.Add((d.X, d.Y, d.Side))) Err(at + " is listed twice");
            }
            int nd = Doors.Count;
            switch (Kind)
            {
                case RoomKind.Combat: if (nd < 2) Err("combat room has " + nd + " door(s), needs at least 2"); break;
                case RoomKind.Entry: case RoomKind.StairExit: if (nd < 1 || nd > 3) Err(Kind + " room has " + nd + " doors, needs 1 to 3"); break;
                case RoomKind.Heart: if (nd != 1) Err("heart room has " + nd + " doors, needs exactly 1 (the antechamber is the only approach)"); break;
                case RoomKind.Checkpoint: if (nd < 2 || nd > 4) Err("checkpoint room has " + nd + " doors, needs 2 to 4"); break;
            }

            // cells that carry a mark must be Floor and not share a cell
            var used = new Dictionary<(int, int), string>();
            if (Kind != RoomKind.Corridor)   // a 1x1 corridor cell is all door; its lantern shares the cell
                foreach (CellDoor d in Doors) if (InBounds(d.X, d.Y)) used[(d.X, d.Y)] = "door";
            void Mark(int x, int y, string what)
            {
                if (!InBounds(x, y)) { Err(what + " at (" + x + "," + y + ") is outside the grid"); return; }
                if (cells[x, y] != CellKind.Floor) Err(what + " at (" + x + "," + y + ") is on a " + cells[x, y] + " cell");
                if (used.TryGetValue((x, y), out string other)) Err(what + " at (" + x + "," + y + ") shares its cell with a " + other);
                else used[(x, y)] = what;
            }
            foreach (CellarSpawn s in Spawns) Mark(s.X, s.Y, "spawn");
            foreach (ChestSlot c in Chests) Mark(c.X, c.Y, "chest");
            foreach (RoomFeature f in Features) Mark(f.X, f.Y, f.Kind.ToString());
            foreach (DressingSocket k in Sockets) Mark(k.X, k.Y, "socket " + k.Id);

            // reachability: flood fill over Floor from the first door that sits on a Floor cell; every Floor cell and every door must be reached
            // (a second pass blocks the Barrel and Crate socket cells, which stand in the way)
            List<string> Unreached(bool blockProps, out bool noStart)
            {
                var blocked = new bool[Width, Height];
                if (blockProps) foreach (DressingSocket k in Sockets) if ((k.Kind == SocketKind.Barrel || k.Kind == SocketKind.Crate) && InBounds(k.X, k.Y)) blocked[k.X, k.Y] = true;
                CellDoor start = Doors.FirstOrDefault(d => InBounds(d.X, d.Y) && cells[d.X, d.Y] == CellKind.Floor && !blocked[d.X, d.Y]);
                noStart = start == null;
                var lost = new List<string>();
                if (start == null) return lost;
                var seen = new bool[Width, Height]; var q = new Queue<(int, int)>();
                seen[start.X, start.Y] = true; q.Enqueue((start.X, start.Y));
                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    foreach (CellSide s in new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West })
                    {
                        var (dx, dy) = Step(s); int nx = cx + dx, ny = cy + dy;
                        if (InBounds(nx, ny) && !seen[nx, ny] && !blocked[nx, ny] && cells[nx, ny] == CellKind.Floor) { seen[nx, ny] = true; q.Enqueue((nx, ny)); }
                    }
                }
                for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++) if (cells[x, y] == CellKind.Floor && !blocked[x, y] && !seen[x, y]) lost.Add("(" + x + "," + y + ")");
                return lost;
            }
            List<string> lostOpen = Unreached(false, out bool noStart);
            if (noStart) Err("no door sits on a Floor cell, so nothing is reachable");
            if (lostOpen.Count > 0) Err("floor cells not reachable from every door: " + string.Join(" ", lostOpen));
            else if (Unreached(true, out _).Count > 0) Err("barrels and crates cut off floor cells: " + string.Join(" ", Unreached(true, out _)));
            if (FloorCount == 0) Err("no floor cells");

            // sockets
            var ids = new HashSet<string>();
            foreach (DressingSocket k in Sockets)
            {
                if (string.IsNullOrEmpty(k.Id) || !ids.Add(k.Id)) Err("socket id '" + k.Id + "' is empty or duplicated");
                if (k.Variants.Count < MinVariants || k.Variants.Count > MaxVariants) Err("socket " + k.Id + " has " + k.Variants.Count + " variants, needs " + MinVariants + " to " + MaxVariants);
                if (k.Variants.Any(string.IsNullOrWhiteSpace) || k.Variants.Distinct(StringComparer.OrdinalIgnoreCase).Count() != k.Variants.Count) Err("socket " + k.Id + " has an empty or repeated variant name");
                if (k.WallMounted && InBounds(k.X, k.Y))
                {
                    var (dx, dy) = Step(Opposite(k.Facing)); int bx = k.X + dx, by = k.Y + dy;
                    // out of the room: the edge is the wall unless this cell has a door on that side (only a corridor cell can, since other rooms keep sockets off door cells)
                    bool wall = InBounds(bx, by) ? cells[bx, by] != CellKind.Floor : !Doors.Any(d => d.X == k.X && d.Y == k.Y && d.Side == Opposite(k.Facing));
                    if (!wall) Err("wall-mounted socket " + k.Id + " (" + k.Kind + ") at (" + k.X + "," + k.Y + ") has no wall behind it");
                }
            }

            // spawns, chests, features and keys by kind
            int starts = Features.Count(f => f.Kind == FeatureKind.EntryStart), stairs = Features.Count(f => f.Kind == FeatureKind.Stair), banks = Features.Count(f => f.Kind == FeatureKind.Bank), bakers = Features.Count(f => f.Kind == FeatureKind.Baker);
            if (Kind == RoomKind.Combat)
            {
                if (Spawns.Count < 2) Err("combat room has " + Spawns.Count + " spawn point(s), needs at least 2");
                if (Chests.Count < 1 || Chests.Count > 3) Err("combat room has " + Chests.Count + " chest slots, needs 1 to 3");
            }
            else if (Spawns.Count > 0) Err(Kind + " room has spawn points");
            if (Kind != RoomKind.Combat && Chests.Count > (Kind == RoomKind.Checkpoint ? 2 : 0)) Err(Kind + " room has too many chest slots");
            if (Kind == RoomKind.Entry ? starts != 4 : starts > 0) Err(Kind == RoomKind.Entry ? "entry room needs exactly 4 party start cells, has " + starts : "only the entry room has party start cells");
            if (Kind == RoomKind.StairExit ? stairs != 1 : stairs > 0) Err(Kind == RoomKind.StairExit ? "stair-exit room needs exactly 1 stair cell, has " + stairs : "only the stair-exit room has a stair cell");
            if (Kind == RoomKind.Checkpoint || Kind == RoomKind.Heart ? banks != 1 : banks > 0) Err(Kind == RoomKind.Checkpoint ? "checkpoint room needs exactly 1 bank cell, has " + banks : Kind == RoomKind.Heart ? "heart room needs exactly 1 bank cell, has " + banks : "only the checkpoint room has a bank cell");
            if (Kind == RoomKind.Heart ? bakers != 1 : bakers > 0) Err(Kind == RoomKind.Heart ? "heart room needs exactly 1 baker cell, has " + bakers : "only the heart room has a baker cell");

            int locked = Doors.Count(d => d.Locked);
            if (locked > 1) Err("more than one locked door");
            if (locked == 1 && Kind != RoomKind.Combat) Err("only a combat room can be the locked room");
            if (locked == 1 && Chests.Count == 0) Err("a locked room needs a chest behind its lock");
            if (locked == 1 && HasKeyChest) Err("a room with a locked door cannot hold the key chest (the key would sit behind its own lock)");
            if (HasKeyChest && Kind != RoomKind.Combat) Err("key chests live in combat rooms");

            // corridor pieces
            if (Kind == RoomKind.Corridor)
            {
                if (Width != 1 || Height != 1) Err("corridor piece must be 1x1");
                int doorMask = 0; foreach (CellDoor d in Doors) doorMask |= MaskBit(d.Side);
                if (doorMask != Mask) Err("corridor mask " + Mask + " does not match its doors (" + doorMask + ")");
                if (Mask < 1 || Mask > 15) Err("corridor mask " + Mask + " is not a 4-bit neighbour mask");
                if (Doors.Any(d => d.Locked)) Err("corridor piece has a locked door");
                int bits = 0; for (int m = Mask; m != 0; m >>= 1) bits += m & 1;
                bool opp = (Mask & (MaskN | MaskS)) == (MaskN | MaskS) || (Mask & (MaskE | MaskW)) == (MaskE | MaskW);
                bool ok;
                switch (Shape)
                {
                    case CorridorShape.Straight: case CorridorShape.Door: ok = bits == 2 && (Mask == (MaskN | MaskS) || Mask == (MaskE | MaskW)); break;
                    case CorridorShape.Corner: ok = bits == 2 && !opp; break;
                    case CorridorShape.T: ok = bits == 3; break;
                    case CorridorShape.Cross: ok = bits == 4; break;
                    case CorridorShape.DeadEnd: ok = bits == 1; break;
                    default: ok = false; break;
                }
                if (!ok) Err("corridor mask " + Mask + " is not valid for shape " + Shape);
            }
            else if (Shape != CorridorShape.None || Mask != 0) Err("only corridor pieces carry a shape and a mask");
            return errs;
        }
    }
}
