// Core/Cellars/CellarEncounterPlan.cs: where the enemies of one Cellars floor stand once the stir meter has bitten (PHASE1 7.2, P49). Pure C#, no engine references.
// A pure function of (layout, dressing, extraEnemiesPerRoom, hunterLatched): every peer derives the same plan; the host alone will later spawn from it (no spawner exists yet).
//   - every Combat room lists its authored spawn points (Kind Authored), then extraEnemiesPerRoom extras (Kind Extra, Tier1), each on a distinct free floor cell of that room
//     chosen from the room's own stream Rng.Derive(seed, "cellars.encounter", floor << 32 | roomIndex): a floor cell that is not a door, a spawn, chest, feature,
//     barrel/crate cell (any option; wall-mounted dressing does not block), vignette cell, the healing point, the gate-key carrier, a dead end or a party start / stair / bank / baker cell. A room with too few free cells
//     gets fewer extras (never an illegal cell).
//   - hunterLatched adds ONE Tier2 Hunter spawn (the rat-king placeholder): the free floor cell of an unlocked Combat room with the greatest walk distance from the party start
//     (ties broken by the stream Rng.Derive(seed, "cellars.encounter", floor << 32 | 0xFFFFFFFF)); cells already taken by extras or authored spawns are skipped. None when
//     nothing is reachable.
//   - floor 4 (the bakery set-piece) is treated like any floor: its Combat rooms take extras and the hunter may roam it. Its fixed rooms keep every authored cell, so a legal cell
//     is simply the room's remaining free floor.
// Own Rng stream, never touches the layout/dressing goldens. No float, no string hashing for decisions.
// Authority: none (static data); the host consumes it.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum EncounterKind : byte { Authored = 0, Extra = 1, Hunter = 2 }

    public sealed class EncounterSpawn
    {
        public readonly int X, Y, RoomIndex; public readonly SpawnTier Tier; public readonly EncounterKind Kind;
        public EncounterSpawn(int x, int y, int roomIndex, SpawnTier tier, EncounterKind kind) { X = x; Y = y; RoomIndex = roomIndex; Tier = tier; Kind = kind; }
        public CellPos Cell => new CellPos(X, Y);
    }

    public sealed class CellarEncounterPlan
    {
        public readonly uint Seed; public readonly int Floor, ExtraPerRoom; public readonly bool HunterLatched;
        public readonly IReadOnlyList<EncounterSpawn> Spawns;     // room by room (authored, then extras), then the hunter last
        public EncounterSpawn Hunter { get; }                     // null when not latched or no legal cell
        CellarEncounterPlan(uint seed, int floor, int extra, bool hunterLatched, List<EncounterSpawn> spawns, EncounterSpawn hunter)
        { Seed = seed; Floor = floor; ExtraPerRoom = extra; HunterLatched = hunterLatched; Spawns = spawns; Hunter = hunter; }

        // Spawns of one room (the hunter is never counted); kind filters when given.
        public int CountIn(int roomIndex, EncounterKind? kind = null)
        {
            int n = 0;
            foreach (EncounterSpawn s in Spawns) if (s.RoomIndex == roomIndex && s.Kind != EncounterKind.Hunter && (!kind.HasValue || s.Kind == kind.Value)) n++;
            return n;
        }

        public static CellarEncounterPlan Build(CellarFloorLayout layout, CellarFloorDressing dressing, int extraEnemiesPerRoom, bool hunterLatched)
        {
            if (layout == null) throw new System.ArgumentNullException(nameof(layout));
            int extra = extraEnemiesPerRoom < 0 ? 0 : extraEnemiesPerRoom;
            int n = CellarFloorLayout.GridSize;
            bool[,] blocked = BlockedCells(layout, dressing);

            var spawns = new List<EncounterSpawn>();
            var taken = new bool[n, n];
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                if (p.Room.Kind != RoomKind.Combat) continue;
                foreach (CellarSpawn sp in p.Room.Spawns)
                {
                    CellPos w = p.Cell(sp.X, sp.Y); taken[w.X, w.Y] = true;
                    spawns.Add(new EncounterSpawn(w.X, w.Y, r, sp.Tier, EncounterKind.Authored));
                }
                if (extra == 0) continue;
                List<CellPos> free = FreeCells(p, blocked);
                var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.encounter", ((ulong)layout.Floor << 32) | (uint)r));
                for (int k = 0; k < extra && free.Count > 0; k++)
                {
                    int i = rng.Range(0, free.Count);
                    CellPos c = free[i]; free.RemoveAt(i); taken[c.X, c.Y] = true;
                    spawns.Add(new EncounterSpawn(c.X, c.Y, r, SpawnTier.Tier1, EncounterKind.Extra));
                }
            }

            EncounterSpawn hunter = null;
            if (hunterLatched)
            {
                int[,] dist = layout.WalkDistances();
                if (dist != null)
                {
                    var best = new List<CellPos>(); var bestRoom = new List<int>(); int bestD = -1;
                    for (int r = 0; r < layout.Rooms.Count; r++)
                    {
                        PlacedRoom p = layout.Rooms[r];
                        if (p.Room.Kind != RoomKind.Combat || r == layout.LockedRoomIndex) continue;
                        foreach (CellPos c in FreeCells(p, blocked))
                        {
                            int d = dist[c.X, c.Y];
                            if (d < 0 || taken[c.X, c.Y]) continue;
                            if (d > bestD) { bestD = d; best.Clear(); bestRoom.Clear(); }
                            if (d == bestD) { best.Add(c); bestRoom.Add(r); }
                        }
                    }
                    if (best.Count > 0)
                    {
                        var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.encounter", ((ulong)layout.Floor << 32) | 0xFFFFFFFFUL));
                        int i = rng.Range(0, best.Count);
                        hunter = new EncounterSpawn(best[i].X, best[i].Y, bestRoom[i], SpawnTier.Tier2, EncounterKind.Hunter);
                        spawns.Add(hunter);
                    }
                }
            }
            return new CellarEncounterPlan(layout.Seed, layout.Floor, extra, hunterLatched, spawns, hunter);
        }

        static bool[,] BlockedCells(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            int n = CellarFloorLayout.GridSize;
            var blocked = new bool[n, n];
            void Ban(int x, int y) { if (x >= 0 && y >= 0 && x < n && y < n) blocked[x, y] = true; }
            foreach (FloorChest c in layout.Chests) Ban(c.X, c.Y);
            if (layout.Carrier != null) Ban(layout.Carrier.X, layout.Carrier.Y);
            if (dressing != null)
            {
                foreach (PlacedVignette v in dressing.Vignettes) Ban(v.Cell.X, v.Cell.Y);
                CellPos? heal = CellarHealing.PointFor(layout, dressing);
                if (heal.HasValue) Ban(heal.Value.X, heal.Value.Y);
            }
            foreach (CellPos d in layout.DeadEnds) Ban(d.X, d.Y);
            foreach (CellPos s in layout.EntryStarts) Ban(s.X, s.Y);
            if (layout.StairExit.HasValue) Ban(layout.StairExit.Value.X, layout.StairExit.Value.Y);
            if (layout.BankCell.HasValue) Ban(layout.BankCell.Value.X, layout.BankCell.Value.Y);
            if (layout.BakerCell.HasValue) Ban(layout.BakerCell.Value.X, layout.BakerCell.Value.Y);
            return blocked;
        }

        // The free-cell rule the plan uses (before any spawn is taken): free floor cells of one room, row-major. Empty for a non-Combat room.
        public static IReadOnlyList<CellPos> FreeCellsOf(CellarFloorLayout layout, CellarFloorDressing dressing, int roomIndex)
        {
            PlacedRoom p = layout.Rooms[roomIndex];
            if (p.Room.Kind != RoomKind.Combat) return new List<CellPos>();
            return FreeCells(p, BlockedCells(layout, dressing));
        }

        // Free floor cells of a combat room in row-major order: floor, not a door, not a room-authored object, not a banned world cell.
        static List<CellPos> FreeCells(PlacedRoom p, bool[,] blocked)
        {
            var list = new List<CellPos>();
            CellarRoom room = p.Room;
            for (int y = 0; y < p.Height; y++)
                for (int x = 0; x < p.Width; x++)
                {
                    if (room.CellAt(x, y) != CellKind.Floor || NearDoorOrObject(room, x, y)) continue;
                    CellPos w = p.Cell(x, y);
                    if (blocked[w.X, w.Y]) continue;
                    list.Add(w);
                }
            return list;
        }

        static bool NearDoorOrObject(CellarRoom room, int x, int y)
        {
            foreach (CellDoor d in room.Doors) if (d.X == x && d.Y == y) return true;
            foreach (CellarSpawn s in room.Spawns) if (s.X == x && s.Y == y) return true;
            foreach (ChestSlot c in room.Chests) if (c.X == x && c.Y == y) return true;
            foreach (RoomFeature f in room.Features) if (f.X == x && f.Y == y) return true;
            foreach (DressingSocket k in room.Sockets)   // wall-mounted dressing never blocks walking; barrels and crates do (any option cell)
                if (!k.WallMounted)
                    for (int o = 0; o < k.CellOptions; o++) { CellPos c = k.OptionCell(o); if (c.X == x && c.Y == y) return true; }
            return false;
        }
    }
}
