// Core/Cellars/CellarChunks.cs: swappable chunk regions inside a room's cell grid (procgen item 4; research: Spelunky's marker chunks with several hand-authored variants per marker).
// A ChunkRegion is a small rectangle of floor cells in a combat room plus a kind (rubble pile, cask stack, collapsed arch, clutter) and 5-6 hand-authored variants. A variant is a
// cell pattern: '.' open floor, '#' blocking (walk-blocking, full height), 'c' low cover (walk-blocking here, shoot-over). Every kind has an "Open" variant (all floor).
// The room pool is dense (nearly every cell carries a door, spawn, chest or socket), so regions are single cells, dominoes and short lines (1x1, 2x1, 3x1 and their quarter turns),
// not 2x2 or 3x2 blocks: no 2x2 block of a combat room is free of marks. Regions never cover a door, spawn, chest or feature cell (CellarRoom.Validate). They MAY cover socket cells: the
// resolver (CellarRoomChunks) skips any variant that would put a blocking or cover cell on a dressed socket or a vignette, which keeps DressingHash and the vignette cells unchanged.
// Authority: none (static data; CellarRoomChunks resolves the picks from (seed, floor) on every peer).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public enum ChunkKind : byte { Rubble = 0, Casks = 1, Arch = 2, Clutter = 3 }

    public enum ChunkCell : byte { Open = 0, Block = 1, Cover = 2 }

    public sealed class ChunkVariant
    {
        public readonly string Name; readonly ChunkCell[,] cells;   // [x, y], in the region's current orientation
        public ChunkVariant(string name, ChunkCell[,] cells) { Name = name; this.cells = cells; }
        public int Width => cells.GetLength(0);
        public int Height => cells.GetLength(1);
        public ChunkCell At(int x, int y) => cells[x, y];
        public bool IsOpen { get { foreach (ChunkCell c in cells) if (c != ChunkCell.Open) return false; return true; } }
        public int NonOpenCount { get { int n = 0; foreach (ChunkCell c in cells) if (c != ChunkCell.Open) n++; return n; } }

        public static ChunkVariant Parse(string name, string[] rows)
        {
            int h = rows.Length, w = rows[0].Length; var g = new ChunkCell[w, h];
            for (int y = 0; y < h; y++)
            {
                if (rows[y].Length != w) throw new FormatException("chunk variant " + name + " is ragged");
                for (int x = 0; x < w; x++)
                    g[x, y] = rows[y][x] == '.' ? ChunkCell.Open : rows[y][x] == '#' ? ChunkCell.Block : rows[y][x] == 'c' ? ChunkCell.Cover : throw new FormatException("chunk variant " + name + ": unknown glyph '" + rows[y][x] + "'");
            }
            return new ChunkVariant(name, g);
        }

        // One clockwise quarter turn (w x h to h x w): (x, y) goes to (h - 1 - y, x).
        public ChunkVariant Turned(int quarterTurnsCW)
        {
            int q = ((quarterTurnsCW % 4) + 4) % 4; ChunkCell[,] g = cells;
            for (int i = 0; i < q; i++)
            {
                int w = g.GetLength(0), h = g.GetLength(1); var n = new ChunkCell[h, w];
                for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) n[h - 1 - y, x] = g[x, y];
                g = n;
            }
            return new ChunkVariant(Name, g);
        }
    }

    public sealed class ChunkRegion
    {
        public const int MinVariants = 3, MaxVariants = 8;
        public readonly string Id; public readonly ChunkKind Kind; public readonly int X, Y, Width, Height;
        public readonly IReadOnlyList<ChunkVariant> Variants;

        public ChunkRegion(string id, ChunkKind kind, int x, int y, int width, int height, IEnumerable<ChunkVariant> variants)
        { Id = id; Kind = kind; X = x; Y = y; Width = width; Height = height; Variants = variants.ToList(); }

        // A region from the hand-authored library; w x h is the footprint in the room (a vertical line is the library's horizontal line turned once).
        public static ChunkRegion Author(string id, ChunkKind kind, int x, int y, int w, int h)
        {
            int turns = w >= h ? 0 : 1;
            return new ChunkRegion(id, kind, x, y, w, h, CellarChunkLibrary.Variants(kind, Math.Max(w, h), Math.Min(w, h)).Select(v => v.Turned(turns)));
        }

        public int OpenIndex { get { for (int i = 0; i < Variants.Count; i++) if (Variants[i].IsOpen) return i; return -1; } }
        public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;

        // The region after its room turned clockwise: `at` maps a room cell to its turned cell, quarterTurns is the same turn count (variants turn with it).
        public ChunkRegion Rotated(int quarterTurnsCW, Func<int, int, (int x, int y)> at)
        {
            var a = at(X, Y); var b = at(X + Width - 1, Y + Height - 1);
            int nx = Math.Min(a.x, b.x), ny = Math.Min(a.y, b.y);
            return new ChunkRegion(Id, Kind, nx, ny, Math.Abs(a.x - b.x) + 1, Math.Abs(a.y - b.y) + 1, Variants.Select(v => v.Turned(quarterTurnsCW)));
        }
    }

    // The hand-authored variants, per kind and footprint (long side, short side; the long side runs west-east here). Variant 0 is always the open one.
    public static class CellarChunkLibrary
    {
        public static IReadOnlyList<ChunkVariant> Variants(ChunkKind kind, int longSide, int shortSide)
        {
            string[][] rows; string[] names;
            if (longSide == 1 && shortSide == 1) Single(kind, out names, out rows);
            else if (longSide == 2 && shortSide == 1) Domino(kind, out names, out rows);
            else if (longSide == 3 && shortSide == 1) Line(kind, out names, out rows);
            else throw new ArgumentException("no chunk variants authored for " + longSide + "x" + shortSide);
            var list = new List<ChunkVariant>();
            for (int i = 0; i < names.Length; i++) list.Add(ChunkVariant.Parse(names[i], rows[i]));
            return list;
        }

        public static bool HasFootprint(int a, int b) { int l = Math.Max(a, b), s = Math.Min(a, b); return s == 1 && l >= 1 && l <= 3; }

        static string[][] Rows(params string[] patterns) => patterns.Select(p => new[] { p }).ToArray();

        // one cell
        static void Single(ChunkKind kind, out string[] names, out string[][] rows)
        {
            switch (kind)
            {
                case ChunkKind.Rubble: names = new[] { "Open", "Heap", "Scree" }; rows = Rows(".", "#", "c"); break;
                case ChunkKind.Casks: names = new[] { "Open", "Cask", "Lying" }; rows = Rows(".", "#", "c"); break;
                case ChunkKind.Arch: names = new[] { "Open", "Pier", "Slab" }; rows = Rows(".", "#", "c"); break;
                default: names = new[] { "Open", "Pile", "Crate" }; rows = Rows(".", "#", "c"); break;
            }
        }

        // two cells
        static void Domino(ChunkKind kind, out string[] names, out string[][] rows)
        {
            switch (kind)
            {
                case ChunkKind.Rubble: names = new[] { "Open", "HeapWest", "HeapEast", "ScreeWest", "ScreeEast", "Ridge" }; rows = Rows("..", "#.", ".#", "c.", ".c", "##"); break;
                case ChunkKind.Casks: names = new[] { "Open", "StackWest", "StackEast", "Pair", "Lying" }; rows = Rows("..", "#.", ".#", "##", "cc"); break;
                case ChunkKind.Arch: names = new[] { "Open", "Pier", "FallenWest", "FallenEast", "Slab" }; rows = Rows("..", "#.", "c#", "#c", "cc"); break;
                default: names = new[] { "Open", "Crate", "Sacks", "Mixed", "Heap" }; rows = Rows("..", "c.", ".c", "c#", "#."); break;
            }
        }

        // three cells in a line
        static void Line(ChunkKind kind, out string[] names, out string[][] rows)
        {
            switch (kind)
            {
                case ChunkKind.Rubble: names = new[] { "Open", "HeapWest", "HeapEast", "Mound", "Drift", "Fan" }; rows = Rows("...", "#..", "..#", ".#.", "c#c", "cc."); break;
                case ChunkKind.Casks: names = new[] { "Open", "RowWest", "RowEast", "Single", "Gapped", "Lying" }; rows = Rows("...", "##.", ".##", ".#.", "#.#", "ccc"); break;
                case ChunkKind.Arch: names = new[] { "Open", "PierWest", "PierEast", "Fallen", "Lintel", "Collapse" }; rows = Rows("...", "#..", "..#", "#c.", ".cc", "#c#"); break;
                default: names = new[] { "Open", "CrateWest", "CrateEast", "Middle", "Pair", "Pile" }; rows = Rows("...", "c..", "..c", ".c.", "c.c", "#c."); break;
            }
        }
    }
}
