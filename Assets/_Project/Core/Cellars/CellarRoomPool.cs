// Core/Cellars/CellarRoomPool.cs: the Phase 1 room pool of the Undercroft Cellars (PHASE1 7.2), authored as ASCII rows (legend in CellarRoom.cs).
// 12 combat rooms (4 per sub-theme, 2x3 to 5x4 cells, ten different silhouettes), 1 entry, 1 stair-exit, 1 checkpoint and the 6 corridor pieces.
// Row 0 is north. Authority: none (static data; the host builds a floor from a seed and clients rebuild the same one).
//
// Why four combat rooms per theme: a floor has 4-6 rooms and about 70% of them come from its own theme, so 3 of 4 (or 4 of 6) own-theme rooms; four own
// rooms are enough to draw them with no repeat, and the rest come from the 8 rooms of the other two themes. Each theme has a lockable room (L) and a key
// chest room (K), so a floor can have its locked room and key without leaving its theme.
// Chests: every combat room has 1-2 chest slots (4 rooms hold at least 4), but a floor wants 6-8, so the M5 generator tops the count up with chest slots
// in the 1-3 side dead ends it adds per floor (PHASE1 7.2) and at corridor ends; the rooms alone do not guarantee 6.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public static class CellarRoomPool
    {
        public const int CombatPerThemeMin = 4;

        public static readonly IReadOnlyList<CellarRoom> All = Build().AsReadOnly();

        // The corridor piece that makes `mask` (N 1, E 2, S 4, W 8) after `turns` clockwise quarter turns (fewest turns first). The Door piece is never
        // returned: it is a straight with a door in it, which the generator chooses on purpose. False when mask is not 1..15.
        public static bool FindCorridor(int mask, out CellarRoom piece, out int turns)
        {
            for (int t = 0; t < 4; t++)
                foreach (CellarRoom r in All)
                    if (r.Kind == RoomKind.Corridor && r.Shape != CorridorShape.Door && CellarRoom.RotateMask(r.Mask, t) == mask) { piece = r; turns = t; return true; }
            piece = null; turns = 0; return false;
        }

        public static CellarRoom Find(string id) => All.FirstOrDefault(r => r.Id == id);
        public static IEnumerable<CellarRoom> Combat(SubTheme theme) => All.Where(r => r.Kind == RoomKind.Combat && r.Theme == theme);

        const CellSide N = CellSide.North, E = CellSide.East, S = CellSide.South, W = CellSide.West;

        // Dressing variants per kind (2-3 named variants each; the art pass swaps kit pieces in by these names).
        static string[] V(SocketKind k)
        {
            switch (k)
            {
                case SocketKind.Shelf: return new[] { "Jars", "Hams", "Sacks" };
                case SocketKind.Rack: return new[] { "Bottles", "Casks", "Empty" };
                case SocketKind.Barrel: return new[] { "Full", "Stacked" };
                case SocketKind.Crate: return new[] { "Stacked", "Open", "Broken" };
                case SocketKind.Chute: return new[] { "Clear", "Heaped" };
                case SocketKind.Lantern: return new[] { "Lit", "Dim", "Out" };
                default: return new[] { "Thin", "Thick" };
            }
        }

        static SocketSpec Sk(SocketKind k, int x, int y, CellSide facing) => new SocketSpec(k, x, y, facing, V(k));

        static CellarRoom Combat(string id, string name, SubTheme t, string sil, string[] rows, params SocketSpec[] socks) => CellarRoom.Parse(id, name, RoomKind.Combat, t, sil, rows, socks);
        static CellarRoom Special(string id, string name, RoomKind k, string sil, string[] rows, params SocketSpec[] socks) => CellarRoom.Parse(id, name, k, SubTheme.Any, sil, rows, socks);

        static List<CellarRoom> Build()
        {
            const SubTheme L = SubTheme.Larder, WV = SubTheme.WineVault, CC = SubTheme.CoalCellar;
            const SocketKind Shelf = SocketKind.Shelf, Rack = SocketKind.Rack, Barrel = SocketKind.Barrel, Crate = SocketKind.Crate, Chute = SocketKind.Chute, Lantern = SocketKind.Lantern, Roots = SocketKind.Roots;
            return new List<CellarRoom>
            {
                // ---- floor 1: larders ----
                Combat("larder_pantry", "Pantry closet", L, "closet", new[] {
                    "sC",
                    "DD",
                    ".s" },
                    Sk(Barrel, 0, 2, N)),
                Combat("larder_cold_store", "Cold store", L, "box", new[] {   // lockable: the south door
                    ".D.",
                    "s..",
                    "C.s",
                    ".L." },
                    Sk(Shelf, 0, 0, S), Sk(Shelf, 2, 0, S), Sk(Barrel, 0, 3, N), Sk(Crate, 2, 3, N)),
                Combat("larder_hanging_hall", "Hanging hall", L, "pillared-hall", new[] {
                    "..DC",
                    ".P.s",
                    "DsP.",
                    "..CS" },
                    Sk(Shelf, 0, 0, S), Sk(Shelf, 1, 0, S), Sk(Lantern, 0, 3, E), Sk(Roots, 1, 3, N)),
                Combat("larder_smoke_gallery", "Smoke gallery", L, "gallery", new[] {   // key chest
                    "...K.",
                    "D.s.D",
                    ".s.SC" },
                    Sk(Shelf, 0, 0, S), Sk(Shelf, 2, 0, S), Sk(Shelf, 4, 0, S), Sk(Barrel, 0, 2, N)),

                // ---- floor 2: wine vaults ----
                Combat("wine_cask_row", "Cask row", WV, "box", new[] {   // lockable: the north door
                    "CL..",
                    ".s.s",
                    "..DS" },
                    Sk(Rack, 2, 0, S), Sk(Rack, 3, 0, S), Sk(Barrel, 0, 1, N), Sk(Barrel, 2, 1, N), Sk(Barrel, 0, 2, N), Sk(Lantern, 1, 2, N)),
                Combat("wine_vault_l", "L vault", WV, "L-shape", new[] {   // key chest
                    "..##",
                    "D.##",
                    "s.sD",
                    "SCK." },
                    Sk(Rack, 0, 0, S), Sk(Rack, 1, 0, S), Sk(Lantern, 1, 1, W), Sk(Crate, 3, 3, N)),
                Combat("wine_press_hall", "Press hall", WV, "cross", new[] {   // key chest
                    "#.D.#",
                    "..s..",
                    "D.S.D",
                    "#CK.#" },
                    Sk(Rack, 1, 0, S), Sk(Rack, 3, 0, S), Sk(Barrel, 0, 1, N), Sk(Barrel, 4, 1, N), Sk(Barrel, 1, 1, N), Sk(Lantern, 3, 3, N)),
                Combat("wine_aisles", "Rack aisles", WV, "aisles", new[] {
                    ".D..",
                    ".#s.",
                    "D#.C",
                    "C#s.",
                    "S.D." },
                    Sk(Rack, 3, 0, S), Sk(Rack, 3, 1, W), Sk(Rack, 3, 3, W), Sk(Crate, 2, 2, N), Sk(Lantern, 3, 4, N)),

                // ---- floor 3: coal cellars ----
                Combat("coal_bunker", "Coal bunker", CC, "box", new[] {   // lockable: the north door
                    ".L.",
                    "D.s",
                    "SC." },
                    Sk(Chute, 0, 0, S), Sk(Chute, 2, 0, S), Sk(Crate, 1, 1, N), Sk(Lantern, 2, 2, N)),
                Combat("coal_furnace_ring", "Furnace ring", CC, "ring", new[] {   // key chest
                    "..D.",
                    ".##s",
                    "s##.",
                    "CDKS" },
                    Sk(Chute, 0, 0, S), Sk(Chute, 1, 0, S), Sk(Lantern, 3, 0, S), Sk(Lantern, 0, 1, E), Sk(Crate, 3, 2, N)),
                Combat("coal_stoke_chicane", "Stoke chicane", CC, "chicane", new[] {
                    "sC",
                    ".#",
                    "DD",
                    "#.",
                    "Ss" },
                    Sk(Lantern, 0, 1, E), Sk(Chute, 1, 3, W)),
                Combat("coal_boiler_hall", "Boiler hall", CC, "stepped", new[] {
                    "##D..",
                    "#..s.",
                    "D.S.D",
                    "C.C.." },
                    Sk(Chute, 3, 0, S), Sk(Chute, 4, 0, S), Sk(Lantern, 1, 1, E), Sk(Crate, 4, 1, N), Sk(Lantern, 1, 3, N)),

                // ---- the three specials ----
                Special("cellar_entry", "Stair landing", RoomKind.Entry, "landing", new[] {
                    ".D..",
                    "EE..",
                    "EE.D",
                    "...." },
                    Sk(Lantern, 0, 0, S), Sk(Lantern, 2, 0, S), Sk(Barrel, 0, 3, N), Sk(Crate, 3, 3, N)),
                Special("cellar_exit", "Stair hall", RoomKind.StairExit, "landing", new[] {
                    ".D..",
                    "....",
                    "D...",
                    "...X" },
                    Sk(Lantern, 0, 0, S), Sk(Lantern, 2, 0, S), Sk(Crate, 0, 3, N), Sk(Barrel, 1, 3, N)),
                Special("cellar_checkpoint", "Lamp nook", RoomKind.Checkpoint, "nook", new[] {
                    ".D.",
                    ".B.",
                    "..C",
                    ".D." },
                    Sk(Lantern, 0, 0, S), Sk(Lantern, 2, 0, S), Sk(Shelf, 0, 1, E), Sk(Shelf, 2, 1, W), Sk(Crate, 0, 2, N)),

                // ---- the six corridor pieces (canonical orientation; the generator rotates them) ----
                CellarRoom.Corridor("corridor_straight", "Straight", CorridorShape.Straight, CellarRoom.MaskN | CellarRoom.MaskS, Sk(Lantern, 0, 0, E)),
                CellarRoom.Corridor("corridor_corner", "Corner", CorridorShape.Corner, CellarRoom.MaskN | CellarRoom.MaskE, Sk(Lantern, 0, 0, N)),
                CellarRoom.Corridor("corridor_t", "T junction", CorridorShape.T, CellarRoom.MaskN | CellarRoom.MaskE | CellarRoom.MaskW, Sk(Lantern, 0, 0, N)),
                CellarRoom.Corridor("corridor_cross", "Crossing", CorridorShape.Cross, 15),
                CellarRoom.Corridor("corridor_dead_end", "Dead end", CorridorShape.DeadEnd, CellarRoom.MaskN, Sk(Lantern, 0, 0, N)),
                CellarRoom.Corridor("corridor_door", "Door piece", CorridorShape.Door, CellarRoom.MaskN | CellarRoom.MaskS, Sk(Lantern, 0, 0, E)),
            };
        }

        public static List<string> Validate() => ValidatePool(All);

        // Room rules for every room, then the pool rules: ids unique, one entry / exit / checkpoint, at least 4 combat rooms per theme, every corridor
        // shape present, each theme has a lockable room and a key-chest room, at least 6 distinct combat silhouettes.
        public static List<string> ValidatePool(IReadOnlyList<CellarRoom> rooms)
        {
            var errs = new List<string>();
            foreach (CellarRoom r in rooms) errs.AddRange(r.Validate());
            foreach (var g in rooms.GroupBy(r => r.Id).Where(g => g.Count() > 1)) errs.Add("pool: room id '" + g.Key + "' is used " + g.Count() + " times");
            foreach (var g in rooms.SelectMany(r => r.Sockets).GroupBy(s => s.Id).Where(g => g.Count() > 1)) errs.Add("pool: socket id '" + g.Key + "' is used " + g.Count() + " times");
            foreach (RoomKind k in new[] { RoomKind.Entry, RoomKind.StairExit, RoomKind.Checkpoint })
            {
                int n = rooms.Count(r => r.Kind == k);
                if (n != 1) errs.Add("pool: needs exactly one " + k + " room, has " + n);
            }
            foreach (SubTheme t in new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar })
            {
                var c = rooms.Where(r => r.Kind == RoomKind.Combat && r.Theme == t).ToList();
                if (c.Count < CombatPerThemeMin) errs.Add("pool: " + t + " has " + c.Count + " combat rooms, needs at least " + CombatPerThemeMin);
                if (!c.Any(r => r.LockedDoor != null)) errs.Add("pool: " + t + " has no lockable room");
                if (!c.Any(r => r.HasKeyChest)) errs.Add("pool: " + t + " has no key-chest room");
            }
            foreach (CorridorShape s in Enum.GetValues(typeof(CorridorShape)))
                if (s != CorridorShape.None && !rooms.Any(r => r.Kind == RoomKind.Corridor && r.Shape == s)) errs.Add("pool: no " + s + " corridor piece");
            int sil = rooms.Where(r => r.Kind == RoomKind.Combat).Select(r => r.Silhouette).Distinct().Count();
            if (sil < 6) errs.Add("pool: only " + sil + " distinct combat silhouettes, needs at least 6");
            return errs;
        }
    }
}
