// Core/Cellars/CellarRoomChunks.cs: the resolved chunk regions of one Cellars floor (procgen item 4). A resolver over the finished layout and its dressing, like CellarFloorDressing;
// LayoutHash and DressingHash are untouched, ChunksHash() is the cross-peer proof of this layer.
//   - per placed room one stream Rng.Derive(seed, "cellars.chunks", floor << 32 | roomIndex); per region, in order, exactly one draw (the starting variant), so a skipped pick
//     never shifts another region's draw;
//   - the floor's heal point (CellarHealing.PointFor, read-only; it depends on layout and dressing, not on chunks) counts as taken, so no variant puts a block or cover on it;
//   - from the drawn variant the resolver tries the variants in order (wrapping round); a variant is legal when none of its blocking or cover cells lands on a filled dressing socket
//     or a vignette cell, and the room stays connected: with the variant's cells, the other regions' picks, the filled barrels and crates and the vignette cells blocked, every
//     remaining floor cell and every door is still reachable from the first door. The open variant is always legal, so it is the fallback;
//   - rooms are already rotated in the layout, so a region is read in the room's own orientation (the variants turned with the room).
// Dressing and vignettes are resolved first and read here (not the other way round), which is why DressingHash is unchanged. Cover counts as walk-blocking for the connectivity test.
// Floor 4 (the bakery set-piece) has no regions. No string.GetHashCode, no float, no set iteration for a decision. Authority: none (every peer derives the same chunks).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public sealed class ResolvedChunk
    {
        public readonly int RoomIndex, RegionIndex, VariantIndex;   // RegionIndex: index in the room's Regions
        public readonly ChunkRegion Region;
        public readonly int SkippedTaken, SkippedCut;   // variants passed over before this one was legal: they hit a dressed socket / vignette, or they cut the room
        public ResolvedChunk(int roomIndex, int regionIndex, ChunkRegion region, int variantIndex, int skippedTaken, int skippedCut) { RoomIndex = roomIndex; RegionIndex = regionIndex; Region = region; VariantIndex = variantIndex; SkippedTaken = skippedTaken; SkippedCut = skippedCut; }
        public int Skipped => SkippedTaken + SkippedCut;
        public ChunkVariant Variant => Region.Variants[VariantIndex];
        public bool IsOpen => Variant.IsOpen;
    }

    public sealed class CellarRoomChunks
    {
        public readonly uint Seed; public readonly int Floor;
        public readonly IReadOnlyList<ResolvedChunk> Chunks;   // room by room, region by region

        CellarRoomChunks(uint seed, int floor, List<ResolvedChunk> chunks) { Seed = seed; Floor = floor; Chunks = chunks; }

        public static CellarRoomChunks Resolve(CellarFloorLayout layout) => Resolve(layout, CellarFloorDressing.Resolve(layout));

        public static CellarRoomChunks Resolve(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var list = new List<ResolvedChunk>();
            CellPos? heal = layout.IsStub ? null : CellarHealing.PointFor(layout, dressing);   // a pure function of (seed, floor); null when the floor has no heal point
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r]; CellarRoom room = p.Room;
                if (room.Regions.Count == 0 || layout.IsStub) continue;
                var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.chunks", ((ulong)layout.Floor << 32) | (uint)r));
                // cells a chunk must not touch: dressed sockets and vignettes (local coordinates); cells that stand in the way of walking: filled barrels and crates and vignettes
                var taken = new bool[room.Width, room.Height]; var props = new bool[room.Width, room.Height];
                if (heal.HasValue)
                {
                    int hx = heal.Value.X - p.X, hy = heal.Value.Y - p.Y;
                    if (hx >= 0 && hy >= 0 && hx < room.Width && hy < room.Height) taken[hx, hy] = true;   // taken only: a heal point is walkable, it just must not be built over
                }
                if (dressing != null)
                {
                    foreach (DressedSocket s in dressing.SocketsOf(r))
                        if (s.Filled) { taken[s.Cell.X - p.X, s.Cell.Y - p.Y] = true; if (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate) props[s.Cell.X - p.X, s.Cell.Y - p.Y] = true; }
                    foreach (PlacedVignette v in dressing.Vignettes)
                        if (v.RoomIndex == r) { taken[v.Cell.X - p.X, v.Cell.Y - p.Y] = true; props[v.Cell.X - p.X, v.Cell.Y - p.Y] = true; }
                }
                var picked = new int[room.Regions.Count];   // -1 until resolved
                for (int i = 0; i < picked.Length; i++) picked[i] = -1;
                for (int g = 0; g < room.Regions.Count; g++)
                {
                    ChunkRegion rg = room.Regions[g]; int n = rg.Variants.Count;
                    int start = rng.Range(0, n), chosen = -1, skipTaken = 0, skipCut = 0;
                    for (int step = 0; step < n && chosen < 0; step++)
                    {
                        int idx = (start + step) % n;
                        int why = Legal(room, room.Regions, picked, g, idx, taken, props);
                        if (why == 0) chosen = idx; else if (why == 1) skipTaken++; else skipCut++;
                    }
                    if (chosen < 0) { chosen = rg.OpenIndex; }   // unreachable (open is legal when the room is connected); kept as the stated fallback
                    picked[g] = chosen;
                    list.Add(new ResolvedChunk(r, g, rg, chosen, skipTaken, skipCut));
                }
            }
            return new CellarRoomChunks(layout.Seed, layout.Floor, list);
        }

        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        // 0 legal, 1 hits a dressed socket or vignette, 2 cuts the room
        static int Legal(CellarRoom room, IReadOnlyList<ChunkRegion> regions, int[] picked, int regionIndex, int variantIndex, bool[,] taken, bool[,] props)
        {
            var blocked = new bool[room.Width, room.Height];
            for (int x = 0; x < room.Width; x++) for (int y = 0; y < room.Height; y++) blocked[x, y] = props[x, y];
            void Stamp(ChunkRegion rg, ChunkVariant v, bool checkTaken, out bool hit)
            {
                hit = false;
                for (int x = 0; x < rg.Width; x++)
                    for (int y = 0; y < rg.Height; y++)
                        if (v.At(x, y) != ChunkCell.Open)
                        {
                            int wx = rg.X + x, wy = rg.Y + y;
                            if (checkTaken && taken[wx, wy]) hit = true;
                            blocked[wx, wy] = true;
                        }
            }
            for (int g = 0; g < regionIndex; g++) Stamp(regions[g], regions[g].Variants[picked[g]], false, out _);
            Stamp(regions[regionIndex], regions[regionIndex].Variants[variantIndex], true, out bool conflict);
            if (conflict) return 1;
            CellDoor start = null;
            foreach (CellDoor d in room.Doors) if (!blocked[d.X, d.Y]) { start = d; break; }
            if (start == null) return 2;
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
            for (int cx = 0; cx < room.Width; cx++)
                for (int cy = 0; cy < room.Height; cy++)
                    if (room.CellAt(cx, cy) == CellKind.Floor && !blocked[cx, cy] && !seen[cx, cy]) return 2;
            return 0;
        }

        // ---- queries and hashes ----

        public IEnumerable<ResolvedChunk> ChunksOf(int roomIndex) => Chunks.Where(c => c.RoomIndex == roomIndex);

        // The resolved cells (world grid coordinates) of one chunk that are not open floor, with their kind.
        public static IEnumerable<(CellPos cell, ChunkCell kind)> SolidCells(CellarFloorLayout layout, ResolvedChunk c)
        {
            PlacedRoom p = layout.Rooms[c.RoomIndex];
            for (int y = 0; y < c.Region.Height; y++)
                for (int x = 0; x < c.Region.Width; x++)
                    if (c.Variant.At(x, y) != ChunkCell.Open) yield return (p.Cell(c.Region.X + x, c.Region.Y + y), c.Variant.At(x, y));
        }

        // A hash of one room's picks alone (region ids and variant indexes, not world cells), so a room that was only placed or turned differently does not count as looking different.
        public ulong RoomSignature(int roomIndex)
        {
            var h = Fnv1a64.Create();
            foreach (ResolvedChunk c in ChunksOf(roomIndex)) { h.Append(c.Region.Id); h.AppendInt32LE(c.VariantIndex); }
            return h.Value;
        }

        // Stable across machines (FNV-1a 64 over an ordered serialization). Separate from LayoutHash and DressingHash.
        public ulong ChunksHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(Chunks.Count);
            foreach (ResolvedChunk c in Chunks) { h.AppendInt32LE(c.RoomIndex); h.Append(c.Region.Id); h.AppendInt32LE(c.VariantIndex); h.Append(c.Variant.Name); }
            return h.Value;
        }
    }
}
