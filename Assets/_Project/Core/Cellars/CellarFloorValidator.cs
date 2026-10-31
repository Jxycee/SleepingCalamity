// Core/Cellars/CellarFloorValidator.cs: the rule set the EditMode tests (and a later host-side assert) run over a generated floor (PHASE1 7.2, M5).
// Pure C#. Validate(layout) returns a list of error strings, empty when the floor is sound. It reads only the layout's public data (never the
// generator's internals), so a bug in the generator cannot hide from it. Floor 4 is the fixed placeholder stub: it is checked for geometry and
// connectivity but not for the floors-1-3 counts (rooms, chests, loops, dead ends, carrier).
// Walking model: cells are walkable room Floor cells and corridor cells; neighbours join inside a room, along corridor mask bits, and through a door
// whose outside corridor cell points back at it. The locked door is the only edge the "no lock" search refuses. Authority: none.
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public static class CellarFloorValidator
    {
        const int G = CellarFloorLayout.GridSize;
        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        public static List<string> Validate(CellarFloorLayout l)
        {
            var errs = new List<string>();
            void Err(string m) => errs.Add("floor " + l.Floor + " seed " + l.Seed + ": " + m);
            bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < G && y < G;

            // ---- rooms: bounds, overlap, margin, ids, kinds ----
            var roomAt = new int[G, G];
            for (int x = 0; x < G; x++) for (int y = 0; y < G; y++) roomAt[x, y] = -1;
            for (int i = 0; i < l.Rooms.Count; i++)
            {
                PlacedRoom p = l.Rooms[i];
                if (p.X < 0 || p.Y < 0 || p.X + p.Width > G || p.Y + p.Height > G) { Err("room " + p.RoomId + " leaves the grid"); continue; }
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++)
                {
                    if (roomAt[p.X + x, p.Y + y] != -1) Err("room " + p.RoomId + " overlaps room " + l.Rooms[roomAt[p.X + x, p.Y + y]].RoomId);
                    roomAt[p.X + x, p.Y + y] = i;
                }
            }
            for (int i = 0; i < l.Rooms.Count; i++)
            {
                PlacedRoom p = l.Rooms[i];
                for (int x = p.X - 1; x <= p.X + p.Width; x++) for (int y = p.Y - 1; y <= p.Y + p.Height; y++)
                    if (InGrid(x, y) && roomAt[x, y] != -1 && roomAt[x, y] != i) { Err("room " + p.RoomId + " margin is not clear of " + l.Rooms[roomAt[x, y]].RoomId); x = p.X + p.Width + 1; break; }
            }
            if (l.Rooms.Select(r => r.RoomId).Distinct().Count() != l.Rooms.Count) Err("a room appears twice");
            int entries = l.Rooms.Count(r => r.Room.Kind == RoomKind.Entry), exits = l.Rooms.Count(r => r.Room.Kind == RoomKind.StairExit);
            if (entries != 1) Err("needs exactly one entry room, has " + entries);
            if (exits != 1) Err("needs exactly one stair-exit room, has " + exits);
            if (l.EntryStarts.Count != 4) Err("entry has " + l.EntryStarts.Count + " start cells, needs 4");
            if (!l.StairExit.HasValue) Err("no stair exit cell");
            int checkpoints = l.Rooms.Count(r => r.Room.Kind == RoomKind.Checkpoint);
            if (!l.IsStub && checkpoints != (l.Floor == 2 ? 1 : 0)) Err("floor " + l.Floor + " has " + checkpoints + " checkpoint room(s)");
            if (!l.IsStub && (l.Rooms.Count < 4 || l.Rooms.Count > 6)) Err("has " + l.Rooms.Count + " rooms, needs 4 to 6");
            if (!l.IsStub) foreach (PlacedRoom p in l.Rooms) { string e = p.Room.Validate().FirstOrDefault(); if (e != null) Err("room data: " + e); }

            // ---- corridors: masks, pieces, reciprocity ----
            var corr = new PlacedCorridor[G, G];
            foreach (PlacedCorridor c in l.Corridors)
            {
                if (!InGrid(c.X, c.Y)) { Err("corridor cell outside the grid"); continue; }
                if (corr[c.X, c.Y] != null) Err("corridor cell " + c.X + "," + c.Y + " listed twice");
                corr[c.X, c.Y] = c;
                if (roomAt[c.X, c.Y] != -1) Err("corridor cell " + c.X + "," + c.Y + " is inside room " + l.Rooms[roomAt[c.X, c.Y]].RoomId);
                if (c.Mask == 0) Err("corridor cell " + c.X + "," + c.Y + " has mask 0");
                else if (c.Mask < 1 || c.Mask > 15) Err("corridor cell " + c.X + "," + c.Y + " has mask " + c.Mask);
                else if (!CellarRoomPool.FindCorridor(c.Mask, out CellarRoom piece, out int turns) || piece.Id != c.Piece.Id || turns != c.Turns) Err("corridor mask " + c.Mask + " at " + c.X + "," + c.Y + " does not resolve to its piece");
            }
            foreach (PlacedCorridor c in l.Corridors)
            {
                if (!InGrid(c.X, c.Y)) continue;
                foreach (CellSide s in Sides)
                {
                    if ((c.Mask & CellarRoom.MaskBit(s)) == 0) continue;
                    CellPos n = new CellPos(c.X, c.Y).Step(s);
                    string at = "corridor " + c.X + "," + c.Y + " bit " + s;
                    if (!InGrid(n.X, n.Y)) { Err(at + " points off the grid"); continue; }
                    if (corr[n.X, n.Y] != null)
                    {
                        if ((corr[n.X, n.Y].Mask & CellarRoom.MaskBit(CellarRoom.Opposite(s))) == 0) Err(at + " is not answered by its neighbour");
                    }
                    else if (roomAt[n.X, n.Y] != -1)
                    {
                        PlacedRoom p = l.Rooms[roomAt[n.X, n.Y]];
                        if (!Enumerable.Range(0, p.Room.Doors.Count).Any(d => p.DoorCell(d).X == n.X && p.DoorCell(d).Y == n.Y && p.Room.Doors[d].Side == CellarRoom.Opposite(s)))
                            Err(at + " points at a wall of room " + p.RoomId + ", not a door");
                    }
                    else Err(at + " points at empty ground");
                }
            }

            // ---- links ----
            var usedDoors = new HashSet<(int, int)>();   // lookups only
            for (int k = 0; k < l.Links.Count; k++)
            {
                FloorLink lk = l.Links[k];
                bool good = true;
                var ends = lk.RoomB < 0 ? new[] { (lk.RoomA, lk.DoorA) } : new[] { (lk.RoomA, lk.DoorA), (lk.RoomB, lk.DoorB) };   // RoomB -1: a branch onto an existing corridor
                foreach (var (r, d) in ends)
                {
                    if (r < 0 || r >= l.Rooms.Count || d < 0 || d >= l.Rooms[r].Room.Doors.Count) { Err("link " + k + " names a missing door"); good = false; continue; }
                    if (!usedDoors.Add((r, d))) Err("a door is used by two links (room " + l.Rooms[r].RoomId + " door " + d + ")");
                    if (!l.DoorIsOpen(r, d)) Err("link " + k + " door of room " + l.Rooms[r].RoomId + " has no corridor pointing at it");
                }
                if (good && lk.RoomA == lk.RoomB) Err("link " + k + " joins a room to itself");
            }

            // ---- walking graph ----
            bool[,] walk = new bool[G, G];
            foreach (PlacedRoom p in l.Rooms)
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++) if (p.Room.CellAt(x, y) == CellKind.Floor && p.X + x < G && p.Y + y < G) walk[p.X + x, p.Y + y] = true;
            foreach (PlacedCorridor c in l.Corridors) if (InGrid(c.X, c.Y)) walk[c.X, c.Y] = true;

            var lockedDoors = new HashSet<(int, int)>();
            if (l.LockedLinkIndex >= 0 && l.LockedLinkIndex < l.Links.Count) { FloorLink lk = l.Links[l.LockedLinkIndex]; lockedDoors.Add((lk.RoomA, lk.DoorA)); lockedDoors.Add((lk.RoomB, lk.DoorB)); }

            // Edges as a function so both searches share it: (from cell, side) -> may step?
            bool Step(int x, int y, CellSide s, bool refuseLock)
            {
                CellPos n = new CellPos(x, y).Step(s);
                if (!InGrid(n.X, n.Y) || !walk[n.X, n.Y]) return false;
                int ra = roomAt[x, y], rb = roomAt[n.X, n.Y];
                if (ra != -1 && ra == rb) return true;                                                         // inside one room
                if (ra == -1 && rb == -1)                                                                      // corridor to corridor
                    return corr[x, y] != null && corr[n.X, n.Y] != null && (corr[x, y].Mask & CellarRoom.MaskBit(s)) != 0 && (corr[n.X, n.Y].Mask & CellarRoom.MaskBit(CellarRoom.Opposite(s))) != 0;
                // door edge: one side is a room door cell facing the corridor cell on the other side, which must point back
                int room = ra != -1 ? ra : rb; CellPos door = ra != -1 ? new CellPos(x, y) : n; CellPos outside = ra != -1 ? n : new CellPos(x, y);
                PlacedRoom pr = l.Rooms[room];
                for (int d = 0; d < pr.Room.Doors.Count; d++)
                {
                    CellPos dc = pr.DoorCell(d);
                    if (dc.X != door.X || dc.Y != door.Y) continue;
                    CellPos o = pr.DoorOutside(d);
                    if (o.X != outside.X || o.Y != outside.Y || !l.DoorIsOpen(room, d)) continue;
                    if (refuseLock && lockedDoors.Contains((room, d))) continue;
                    return true;
                }
                return false;
            }

            bool[,] Reach(CellPos from, bool refuseLock)
            {
                var seen = new bool[G, G]; var q = new Queue<CellPos>();
                if (!InGrid(from.X, from.Y) || !walk[from.X, from.Y]) return seen;
                seen[from.X, from.Y] = true; q.Enqueue(from);
                while (q.Count > 0)
                {
                    CellPos c = q.Dequeue();
                    foreach (CellSide s in Sides)
                    {
                        CellPos n = c.Step(s);
                        if (InGrid(n.X, n.Y) && !seen[n.X, n.Y] && Step(c.X, c.Y, s, refuseLock)) { seen[n.X, n.Y] = true; q.Enqueue(n); }
                    }
                }
                return seen;
            }

            if (l.EntryStarts.Count > 0)
            {
                CellPos start = l.EntryStarts[0];
                bool[,] all = Reach(start, false), open = Reach(start, true);
                int lost = 0;
                for (int x = 0; x < G; x++) for (int y = 0; y < G; y++) if (walk[x, y] && !all[x, y]) lost++;
                if (lost > 0) Err(lost + " walkable cell(s) cannot be reached from the entry (rooms not all connected)");
                if (l.StairExit.HasValue && !open[l.StairExit.Value.X, l.StairExit.Value.Y]) Err("the stair exit is not reachable from the entry without passing the locked door");
                if (!l.IsStub && !l.LastResort && l.StairExit.HasValue && l.EntryToExitDistance() < CellarFloorGenerator.MinExitDistance)
                    Err("the stair exit is only " + l.EntryToExitDistance() + " steps from the entry, needs at least " + CellarFloorGenerator.MinExitDistance);
                if (l.KeyChest != null && !open[l.KeyChest.X, l.KeyChest.Y]) Err("the key chest is not reachable without passing the locked door");
                if (l.Carrier != null && !open[l.Carrier.X, l.Carrier.Y]) Err("the gate-key carrier is not reachable without passing the locked door");
            }

            // ---- locked room, key, carrier ----
            if (l.HasLockedRoom)
            {
                int lr = l.LockedRoomIndex;
                if (lr >= l.Rooms.Count || l.Rooms[lr].Room.Kind != RoomKind.Combat) Err("the locked room is not a combat room");
                else
                {
                    int touching = l.Links.Count(k => k.Touches(lr));
                    if (touching != 1) Err("the locked room has " + touching + " connections, needs exactly 1 (a leaf)");
                    if (l.LockedLinkIndex < 0 || !l.Links[l.LockedLinkIndex].Touches(lr)) Err("the locked link does not touch the locked room");
                    else
                    {
                        FloorLink lk = l.Links[l.LockedLinkIndex]; int d = lk.RoomA == lr ? lk.DoorA : lk.DoorB;
                        if (!lk.Locked || !l.Rooms[lr].Room.Doors[d].Locked) Err("the locked link is not on the room's L door");
                    }
                    for (int d = 0; d < l.Rooms[lr].Room.Doors.Count; d++) if (!l.Rooms[lr].Room.Doors[d].Locked && l.DoorIsOpen(lr, d)) Err("the locked room has an open door besides its lock");
                }
                if (l.KeyChest == null) Err("a locked floor has no key chest");
                else if (l.KeyChest.RoomIndex == lr || (l.KeyChest.RoomIndex >= 0 && l.Rooms[l.KeyChest.RoomIndex].Room.Chests.All(c => !c.KeyCandidate))) Err("the key chest is in the locked room or in a room with no key slot");
                if (l.Links.Count(k => k.Locked) != 1) Err("a locked floor needs exactly one locked link");
            }
            else
            {
                if (l.Links.Any(k => k.Locked)) Err("an unlocked floor has a locked link");
                if (l.KeyChest != null) Err("an unlocked floor has a key chest");
            }
            if (l.Carrier != null)
            {
                if (l.Carrier.RoomIndex == l.LockedRoomIndex) Err("the gate-key carrier is in the locked room");
                else if (l.Carrier.RoomIndex < 0 || l.Carrier.RoomIndex >= l.Rooms.Count) Err("the gate-key carrier names a missing room");
                else if (l.Rooms[l.Carrier.RoomIndex].Room.Spawns.Count <= l.Carrier.SpawnIndex) Err("the gate-key carrier names a missing spawn");
            }
            else if (!l.IsStub) Err("no gate-key carrier");

            // ---- chests, loops, dead ends (floors 1-3) ----
            var chestCells = new HashSet<(int, int)>();
            foreach (FloorChest c in l.Chests)
            {
                if (!chestCells.Add((c.X, c.Y))) Err("two chests share cell " + c.X + "," + c.Y);
                if (c.RoomIndex >= 0) { if (!walk[c.X, c.Y] || roomAt[c.X, c.Y] != c.RoomIndex) Err("chest " + c.X + "," + c.Y + " is not on a floor cell of its room"); }
                else if (corr[c.X, c.Y] == null || !l.DeadEnds.Any(e => e.X == c.X && e.Y == c.Y)) Err("chest " + c.X + "," + c.Y + " is not at a dead end");
            }
            if (!l.IsStub)
            {
                if (l.Chests.Count < 6 || l.Chests.Count > 8) Err("has " + l.Chests.Count + " chests, needs 6 to 8");
                int loops = l.Links.Count - (l.Rooms.Count - 1);
                if (loops < 1 || loops > 3) Err("has " + loops + " loop corridor(s), needs 1 to 3");
                int ends = l.Corridors.Count(c => c.Piece.Shape == CorridorShape.DeadEnd);
                if (ends < 1 || ends > 3 || l.DeadEnds.Count != ends) Err("has " + ends + " dead end corridor cell(s) (" + l.DeadEnds.Count + " recorded), needs 1 to 3");
            }
            return errs;
        }
    }
}
