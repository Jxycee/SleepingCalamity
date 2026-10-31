// Core/Cellars/CellarFloorGenerator.cs: the M5 seeded floor generator for the Undercroft Cellars (PHASE1 7.2). Pure C#, no engine references.
// Generate(seed, floor) builds floors 1-3 from the room pool; floor 4 is a fixed placeholder (see BuildStub). Authority: none (static; the host sends
// (seed, floor) and every client builds the same floor, so nothing here may depend on the machine).
//
// Determinism: only Pcg32 / Rng / Fnv1a64 are used. No string.GetHashCode, no iteration over a HashSet or Dictionary for a decision (those are only used
// for lookups), no float math in placement; every list is walked in a fixed order. The floor seed is hash(seed, floor) (Rng.Derive), not seed + floor.
//
// Two layers, so a floor needs only (seed, floor):
//   PickRooms(seed, floor)  which combat rooms the floor uses, whether it has a locked room, its chest target. Floor N leaves out the combat rooms
//                           PickRooms(seed, N-1) returned, so consecutive floors never repeat a combat room (entry and exit are shared by design).
//   Generate(seed, floor)   places them: entry first, then the rest in a shuffled order with the locked room last, each new room on the 14 x 14 grid
//                           (rectangle plus a one-cell margin clear of every other room; a margin cell off the grid counts as clear), linked to a random earlier room by a
//                           corridor routed round the rooms; then 1-3 extra corridors (loops) and 1-3 side dead ends. A corridor cell's mask is built from
//                           the links that pass through it (not from mere adjacency), then resolved to a piece by CellarRoomPool.FindCorridor.
// Floor shape: 4-6 rooms (entry, stair exit, 2-4 combat rooms, plus the checkpoint on floor 2). The checkpoint ("after floor 2", PHASE1 7.2) is the lamp
// nook placed ON floor 2 as one of its rooms, not a floor of its own: a dive that clears floor 2 banks there before floor 3, and floor 3 starts at its own
// entry. Floor 2 therefore has one combat room fewer than 1 and 3 for the same room total.
// Locked room (50% of floors): a lockable combat room placed last as a LEAF; only its L door is linked, its other doors stay walled, so it never blocks
// the exit. The key sits in a KeyCandidate chest of another combat room; the gate-key carrier is one random spawn of a non-locked combat room.
// Chests: every chest slot of every room, plus a chest at the end of a side dead end when the rooms alone fall short of the 6-8 target (PickRooms keeps
// the room slots at 3 or more, and a floor has 1-3 dead ends, so the target is always reachable).
// Retries: placement is retried inside one RNG stream (MaxAttempts whole attempts, RoomTries positions per room); if every attempt fails Generate throws,
// so the 10,000-seed test surfaces it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    // What PickRooms decided for one floor.
    public sealed class CellarFloorPlan
    {
        public readonly int Floor; public readonly SubTheme Theme;
        public readonly bool Locked, HasCheckpoint;
        public readonly IReadOnlyList<CellarRoom> Combat;   // 2-4 distinct combat rooms
        public readonly CellarRoom LockedRoom, KeyRoom;      // null unless Locked
        public readonly int TargetChests;                    // 6-8
        public CellarFloorPlan(int floor, SubTheme theme, bool locked, bool hasCheckpoint, IEnumerable<CellarRoom> combat, CellarRoom lockedRoom, CellarRoom keyRoom, int targetChests)
        { Floor = floor; Theme = theme; Locked = locked; HasCheckpoint = hasCheckpoint; Combat = combat.ToList(); LockedRoom = lockedRoom; KeyRoom = keyRoom; TargetChests = targetChests; }
    }

    public static class CellarFloorGenerator
    {
        public const int MaxAttempts = 400, RoomTries = 60, ScanTries = 12, LinkTries = 6, PackFirstAfter = 100, MaxFootprint = 92;
        const int G = CellarFloorLayout.GridSize;
        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        // hash(seed, floor), never seed + floor.
        public static ulong FloorSeed(uint seed, int floor) => Rng.Derive(seed, "cellars.floor", (ulong)floor);

        static void Shuffle<T>(IList<T> list, ref Pcg32 rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); T t = list[i]; list[i] = list[j]; list[j] = t; }
        }

        static CellarRoom Special(RoomKind k) { foreach (CellarRoom r in CellarRoomPool.All) if (r.Kind == k) return r; throw new InvalidOperationException("pool has no " + k + " room"); }

        // ---- room picking ----

        // Pure in (seed, floor). Floors 1-3 only. Own-theme share of the combat rooms is 70% on average (count * 0.7, the fraction rounded by a coin), falling
        // back to other themes when the own theme has nothing left after the previous floor's exclusion.
        public static CellarFloorPlan PickRooms(uint seed, int floor)
        {
            if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor), "PickRooms is for floors 1-3");
            SubTheme theme = (SubTheme)floor;
            var excluded = new HashSet<string>();   // lookups only
            if (floor > 1) foreach (CellarRoom r in PickRooms(seed, floor - 1).Combat) excluded.Add(r.Id);
            var all = new List<CellarRoom>();
            foreach (CellarRoom r in CellarRoomPool.All) if (r.Kind == RoomKind.Combat && !excluded.Contains(r.Id)) all.Add(r);
            SubTheme[] others = new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar }.Where(t => t != theme).ToArray();
            bool cp = floor == 2;
            var rng = new Pcg32(Rng.Derive(seed, "cellars.rooms", (ulong)floor));
            bool wantLocked = rng.Range(0, 2) == 1;             // the 50% coin is flipped once, so the room-set rejections below cannot skew it
            for (int attempt = 0; attempt < 400; attempt++)
            {
                bool locked = wantLocked && attempt < 200;      // if no locked room set turns up in 200 tries, fall back to an unlocked floor
                if (!locked && attempt == 200) wantLocked = false;
                int total = rng.Range(4, 7);                    // 4-6 rooms on the floor
                int count = total - 2 - (cp ? 1 : 0);           // minus entry, exit and the checkpoint
                if (locked && count < 2) count = 2;             // a locked floor needs a lockable room and a key room
                int own = count * 7 / 10;
                if (rng.Range(0, 10) < count * 7 % 10) own++;
                var slotThemes = new List<SubTheme>();
                for (int i = 0; i < count; i++) slotThemes.Add(i < own ? theme : others[rng.Range(0, 2)]);
                Shuffle(slotThemes, ref rng);
                var picked = new List<CellarRoom>();
                bool ok = true;
                for (int i = 0; i < count && ok; i++)
                {
                    bool wantLock = locked && i == 0, wantKey = locked && i == 1;
                    var cand = new List<CellarRoom>();
                    foreach (CellarRoom r in all)
                        if (!picked.Contains(r) && (!wantLock || r.LockedDoor != null) && (!wantKey || r.HasKeyChest) && r.Theme == slotThemes[i]) cand.Add(r);
                    if (cand.Count == 0)
                        foreach (CellarRoom r in all)
                            if (!picked.Contains(r) && (!wantLock || r.LockedDoor != null) && (!wantKey || r.HasKeyChest)) cand.Add(r);
                    if (cand.Count == 0) ok = false; else picked.Add(cand[rng.Range(0, cand.Count)]);
                }
                if (!ok) continue;
                int slots = cp ? Special(RoomKind.Checkpoint).Chests.Count : 0;
                foreach (CellarRoom r in picked) slots += r.Chests.Count;
                if (slots < 3) continue;                        // dead ends can add at most 3
                // door supply: a tree over the N non-locked rooms uses 2 (N-1) doors, the lock 1 more; a loop needs one spare door (it can run to an
                // existing corridor), so keep 2 spare (a short chain of two-door rooms would otherwise leave none)
                int doors = Special(RoomKind.Entry).Doors.Count + Special(RoomKind.StairExit).Doors.Count + (cp ? Special(RoomKind.Checkpoint).Doors.Count : 0), open = cp ? 3 : 2;
                foreach (CellarRoom r in picked) if (!locked || r != picked[0]) { doors += r.Doors.Count; open++; }
                if (doors - (2 * (open - 1) + (locked ? 1 : 0)) < 2) continue;
                // crowding: six big rooms leave the 14 x 14 grid no room for corridors, so cap the total footprint (the failures seen were 96-100 cells)
                int area = Special(RoomKind.Entry).Width * Special(RoomKind.Entry).Height + Special(RoomKind.StairExit).Width * Special(RoomKind.StairExit).Height + (cp ? Special(RoomKind.Checkpoint).Width * Special(RoomKind.Checkpoint).Height : 0);
                foreach (CellarRoom r in picked) area += r.Width * r.Height;
                if (area > MaxFootprint) continue;
                int target = rng.Range(6, 9);
                return new CellarFloorPlan(floor, theme, locked, cp, picked, locked ? picked[0] : null, locked ? picked[1] : null, target);
            }
            throw new InvalidOperationException("PickRooms: no room set for seed " + seed + " floor " + floor);
        }

        // ---- generation ----

        public static CellarFloorLayout Generate(uint seed, int floor)
        {
            if (floor == 4) return BuildStub(seed);
            if (floor < 1 || floor > 4) throw new ArgumentOutOfRangeException(nameof(floor), "floors are 1-4");
            CellarFloorPlan plan = PickRooms(seed, floor);
            var b = new Builder(new Pcg32(FloorSeed(seed, floor)));
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                b.Reset();
                CellarFloorLayout l = b.TryBuild(seed, plan, attempt);
                if (l != null) return l;
            }
            throw new InvalidOperationException("CellarFloorGenerator: no layout for seed " + seed + " floor " + floor + " in " + MaxAttempts + " attempts (lost at placement " + b.Fails[0] + ", loops " + b.Fails[1] + ", dead ends " + b.Fails[2] + ", carriers " + b.Fails[3] + ")");
        }

        // Floor 4 PLACEHOLDER. The real floor 4 is the taken bakery's oven maze with the baker asleep at its heart and the Dough Warden on guard (PHASE1
        // 7.2); that set-piece is the next M5 step. Until then this is a small fixed layout from the same pool so the dive loop can end somewhere:
        // entry, the press hall, the stair hall and a chicane room (the stair hall is the way down/out), no locked room, no gate key, no extra chest
        // top-up. It is exempt from the 6-8 chest rule. The rooms and positions are hard-coded (all unrotated); only the corridor routes are computed.
        static CellarFloorLayout BuildStub(uint seed)
        {
            var b = new Builder(new Pcg32(4444));
            b.Reset();
            int entry = b.PlaceFixed(Special(RoomKind.Entry), 0, 1, 1);
            int hall = b.PlaceFixed(CellarRoomPool.Find("wine_press_hall"), 0, 6, 1);
            int exit = b.PlaceFixed(Special(RoomKind.StairExit), 0, 6, 7);
            int chicane = b.PlaceFixed(CellarRoomPool.Find("coal_stoke_chicane"), 0, 2, 7);
            b.LinkFixed(entry, CellSide.East, hall, CellSide.West);
            b.LinkFixed(hall, CellSide.East, exit, CellSide.North);
            b.LinkFixed(exit, CellSide.West, chicane, CellSide.East);
            return b.Assemble(seed, 4, true, SubTheme.Any, null);
        }

        // All mutable placement state for one attempt. rng is a mutable struct kept in this field and never copied.
        sealed class Builder
        {
            Pcg32 rng;
            readonly List<PlacedRoom> rooms = new List<PlacedRoom>();
            readonly List<bool[]> doorUsed = new List<bool[]>();
            readonly List<FloorLink> links = new List<FloorLink>();
            readonly List<CellPos> deadEnds = new List<CellPos>();
            readonly int[,] roomAt = new int[G, G];
            readonly int[,] mask = new int[G, G];
            readonly bool[,] corr = new bool[G, G];
            readonly int[] parent = new int[G * G];
            readonly int[] queue = new int[G * G];
            int lockedIndex = -1, lockedLink = -1;
            public readonly int[] Fails = new int[5];   // attempts lost per stage: room placement, loops, dead ends, carriers, (spare); shown when Generate gives up

            public Builder(Pcg32 rng) { this.rng = rng; }

            public void Reset()
            {
                rooms.Clear(); doorUsed.Clear(); links.Clear(); deadEnds.Clear(); lockedIndex = -1; lockedLink = -1;
                for (int x = 0; x < G; x++) for (int y = 0; y < G; y++) { roomAt[x, y] = -1; mask[x, y] = 0; corr[x, y] = false; }
            }

            // ---- rotated rooms (cached; rooms are immutable) ----
            static readonly Dictionary<string, CellarRoom> rotCache = new Dictionary<string, CellarRoom>();
            static CellarRoom Rot(CellarRoom r, int q)
            {
                if (q == 0) return r;
                string key = r.Id + "/" + q;
                lock (rotCache)
                {
                    if (!rotCache.TryGetValue(key, out CellarRoom v)) { v = r.Rotated(q); rotCache[key] = v; }
                    return v;
                }
            }

            // ---- placement ----

            bool Fits(int w, int h, int x, int y)
            {
                if (x < 0 || y < 0 || x + w > G || y + h > G) return false;
                for (int cx = Math.Max(0, x - 1); cx <= Math.Min(G - 1, x + w); cx++) for (int cy = Math.Max(0, y - 1); cy <= Math.Min(G - 1, y + h); cy++) if (roomAt[cx, cy] != -1) return false;   // rectangle plus margin clear (margin off the grid counts as clear)
                for (int cx = x; cx < x + w; cx++) for (int cy = y; cy < y + h; cy++) if (corr[cx, cy]) return false;                  // no earlier corridor under the room
                return true;
            }

            int Add(CellarRoom rotated, int q, int x, int y)
            {
                int idx = rooms.Count;
                rooms.Add(new PlacedRoom(rotated, q, x, y)); doorUsed.Add(new bool[rotated.Doors.Count]);
                for (int cx = 0; cx < rotated.Width; cx++) for (int cy = 0; cy < rotated.Height; cy++) roomAt[x + cx, y + cy] = idx;
                return idx;
            }

            void RemoveLast()
            {
                PlacedRoom p = rooms[rooms.Count - 1];
                for (int cx = 0; cx < p.Width; cx++) for (int cy = 0; cy < p.Height; cy++) roomAt[p.X + cx, p.Y + cy] = -1;
                rooms.RemoveAt(rooms.Count - 1); doorUsed.RemoveAt(doorUsed.Count - 1);
            }

            // ---- routing ----

            bool Passable(int x, int y) => x >= 0 && y >= 0 && x < G && y < G && roomAt[x, y] == -1;

            // Shortest path (BFS) over cells outside every room; ties broken by a shuffled side order when `shuffle`. Existing corridor cells are passable
            // (a link may join one). Null when there is no path.
            List<CellPos> Route(CellPos a, CellPos b, bool shuffle)
            {
                if (!Passable(a.X, a.Y) || !Passable(b.X, b.Y)) return null;
                var order = new[] { 0, 1, 2, 3 };
                if (shuffle) Shuffle(order, ref rng);
                for (int i = 0; i < parent.Length; i++) parent[i] = -2;
                int start = a.Y * G + a.X, goal = b.Y * G + b.X, head = 0, tail = 0;
                parent[start] = -1; queue[tail++] = start;
                while (head < tail && parent[goal] == -2)
                {
                    int cur = queue[head++], cx = cur % G, cy = cur / G;
                    for (int k = 0; k < 4; k++)
                    {
                        var (dx, dy) = CellarRoom.Step(Sides[order[k]]); int nx = cx + dx, ny = cy + dy;
                        if (!Passable(nx, ny) || parent[ny * G + nx] != -2) continue;
                        parent[ny * G + nx] = cur; queue[tail++] = ny * G + nx;
                    }
                }
                if (parent[goal] == -2) return null;
                var path = new List<CellPos>();
                for (int c = goal; c != -1; c = parent[c]) path.Add(new CellPos(c % G, c / G));
                path.Reverse();
                return path;
            }

            static CellSide DirOf(CellPos from, CellPos to) => to.X > from.X ? CellSide.East : to.X < from.X ? CellSide.West : to.Y > from.Y ? CellSide.South : CellSide.North;

            void Join(CellPos p, CellPos q)
            {
                CellSide d = DirOf(p, q);
                mask[p.X, p.Y] |= CellarRoom.MaskBit(d); mask[q.X, q.Y] |= CellarRoom.MaskBit(CellarRoom.Opposite(d));
            }

            // rb -1: the path ends on an existing corridor cell instead of a second door (a branch back into the network).
            void Commit(int ra, int da, int rb, int db, bool locked, List<CellPos> path)
            {
                foreach (CellPos c in path) corr[c.X, c.Y] = true;
                for (int i = 1; i < path.Count; i++) Join(path[i - 1], path[i]);
                CellPos first = path[0], last = path[path.Count - 1];
                mask[first.X, first.Y] |= CellarRoom.MaskBit(CellarRoom.Opposite(rooms[ra].Room.Doors[da].Side));
                doorUsed[ra][da] = true;
                if (rb >= 0)
                {
                    mask[last.X, last.Y] |= CellarRoom.MaskBit(CellarRoom.Opposite(rooms[rb].Room.Doors[db].Side));
                    doorUsed[rb][db] = true;
                }
                if (locked) lockedLink = links.Count;
                links.Add(new FloorLink(ra, da, rb, db, locked, path));
            }

            bool TryRoute(int ra, int da, int rb, int db, bool locked)
            {
                List<CellPos> path = Route(rooms[ra].DoorOutside(da), rooms[rb].DoorOutside(db), true);
                if (path == null) return false;
                Commit(ra, da, rb, db, locked, path);
                return true;
            }

            readonly List<int> tmp = new List<int>();

            // Door indices of room r that are still unused (a non-locked room only; the locked room is linked by its L door alone).
            List<int> FreeDoors(int r)
            {
                tmp.Clear();
                if (r == lockedIndex) return tmp;
                for (int d = 0; d < doorUsed[r].Length; d++) if (!doorUsed[r][d]) tmp.Add(d);
                return tmp;
            }

            // Link the newest room (index k > 0) to a random earlier room.
            bool LinkNewest(int k, bool isLocked)
            {
                for (int attempt = 0; attempt < LinkTries; attempt++)
                {
                    var avail = new List<int>();
                    for (int j = 0; j < k; j++) if (FreeDoors(j).Count > 0) avail.Add(j);
                    if (avail.Count == 0) return false;
                    int rj = avail[rng.Range(0, avail.Count)];
                    List<int> fj = FreeDoors(rj); int dj = fj[rng.Range(0, fj.Count)];
                    int dk;
                    if (isLocked) { dk = -1; for (int d = 0; d < rooms[k].Room.Doors.Count; d++) if (rooms[k].Room.Doors[d].Locked) dk = d; if (dk < 0) return false; }
                    else { var fk = new List<int>(); for (int d = 0; d < doorUsed[k].Length; d++) if (!doorUsed[k][d]) fk.Add(d); dk = fk[rng.Range(0, fk.Count)]; }
                    if (TryRoute(rj, dj, k, dk, isLocked)) return true;
                }
                return false;
            }

            bool Linked(int a, int b) { foreach (FloorLink l in links) if (l.Touches(a) && l.Touches(b)) return true; return false; }

            // 1-3 extra corridors between rooms that already connect, so the map has loops. True if at least one was made.
            bool AddLoops()
            {
                int want = rng.Range(1, 4), made = 0;
                for (int t = 0; t < 30 && made < want; t++)
                {
                    var avail = new List<int>();
                    for (int r = 0; r < rooms.Count; r++) if (FreeDoors(r).Count > 0) avail.Add(r);
                    if (avail.Count == 0) break;
                    int a = avail[rng.Range(0, avail.Count)];
                    List<int> fa = FreeDoors(a); int da = fa[rng.Range(0, fa.Count)];
                    if (avail.Count >= 2 && rng.Range(0, 2) == 0)
                    {
                        // door to door between two rooms
                        int b = avail[rng.Range(0, avail.Count)];
                        if (a == b || (t < 20 && Linked(a, b))) continue;   // prefer a pair that is not directly linked yet
                        List<int> fb = FreeDoors(b); int db = fb[rng.Range(0, fb.Count)];
                        if (TryRoute(a, da, b, db, false)) made++;
                    }
                    else
                    {
                        // door back onto a random corridor cell (rooms are two-door mostly, so spare doors are scarce: one is enough for a loop)
                        var cells = new List<CellPos>();
                        for (int y = 0; y < G; y++) for (int x = 0; x < G; x++) if (corr[x, y]) cells.Add(new CellPos(x, y));
                        CellPos target = cells[rng.Range(0, cells.Count)];
                        List<CellPos> path = Route(rooms[a].DoorOutside(da), target, true);
                        if (path == null) continue;
                        Commit(a, da, -1, -1, false, path); made++;
                    }
                }
                return made >= 1;
            }

            // A straight stub of 1-3 corridor cells off an unused door or off a corridor cell. Returns the end cell, or null when nothing fits.
            CellPos? AddDeadEnd()
            {
                var cands = new List<(CellPos start, CellSide dir, bool fromDoor, CellPos from, int room, int door)>();
                for (int r = 0; r < rooms.Count; r++)
                {
                    if (r == lockedIndex) continue;
                    for (int d = 0; d < doorUsed[r].Length; d++)
                    {
                        if (doorUsed[r][d]) continue;
                        CellPos o = rooms[r].DoorOutside(d);
                        if (Passable(o.X, o.Y) && !corr[o.X, o.Y]) cands.Add((o, rooms[r].Room.Doors[d].Side, true, o, r, d));
                    }
                }
                for (int y = 0; y < G; y++) for (int x = 0; x < G; x++)
                {
                    if (!corr[x, y] || deadEnds.Any(e => e.X == x && e.Y == y)) continue;   // never branch off a dead end: it would stop being one
                    foreach (CellSide s in Sides)
                    {
                        CellPos n = new CellPos(x, y).Step(s);
                        if (Passable(n.X, n.Y) && !corr[n.X, n.Y]) cands.Add((n, s, false, new CellPos(x, y), -1, -1));
                    }
                }
                if (cands.Count == 0) return null;
                var c = cands[rng.Range(0, cands.Count)];
                int len = rng.Range(1, 4);
                var cells = new List<CellPos> { c.start };
                while (cells.Count < len)
                {
                    CellPos n = cells[cells.Count - 1].Step(c.dir);
                    if (!Passable(n.X, n.Y) || corr[n.X, n.Y]) break;
                    cells.Add(n);
                }
                foreach (CellPos p in cells) corr[p.X, p.Y] = true;
                for (int i = 1; i < cells.Count; i++) Join(cells[i - 1], cells[i]);
                if (c.fromDoor) { mask[c.start.X, c.start.Y] |= CellarRoom.MaskBit(CellarRoom.Opposite(c.dir)); doorUsed[c.room][c.door] = true; }
                else Join(c.from, c.start);
                deadEnds.Add(cells[cells.Count - 1]);
                return cells[cells.Count - 1];
            }

            // ---- whole floor ----

            public CellarFloorLayout TryBuild(uint seed, CellarFloorPlan plan, int attempt)
            {
                var order = new List<CellarRoom>();
                var middle = new List<CellarRoom>();
                foreach (CellarRoom r in plan.Combat) if (r != plan.LockedRoom) middle.Add(r);
                middle.Add(Special(RoomKind.StairExit));
                if (plan.HasCheckpoint) middle.Add(Special(RoomKind.Checkpoint));
                Shuffle(middle, ref rng);
                if (attempt >= PackFirstAfter)   // a crowded floor: place the big rooms first (stable sort, so the shuffle still breaks ties)
                    for (int i = 1; i < middle.Count; i++)
                        for (int j = i; j > 0 && middle[j].Width * middle[j].Height > middle[j - 1].Width * middle[j - 1].Height; j--) { CellarRoom t = middle[j]; middle[j] = middle[j - 1]; middle[j - 1] = t; }
                order.Add(Special(RoomKind.Entry)); order.AddRange(middle);
                if (plan.Locked) order.Add(plan.LockedRoom);

                for (int i = 0; i < order.Count; i++)
                {
                    bool isLocked = plan.Locked && i == order.Count - 1;
                    bool placed = false;
                    List<(int q, int x, int y)> every = null;   // built only when random positions keep failing (a crowded floor)
                    for (int t = 0; t < RoomTries + ScanTries && !placed; t++)
                    {
                        int q, x, y; CellarRoom rot;
                        if (t < RoomTries)
                        {
                            q = rng.Range(0, 4); rot = Rot(order[i], q);
                            x = rng.Range(0, G - rot.Width + 1); y = rng.Range(0, G - rot.Height + 1);
                            if (!Fits(rot.Width, rot.Height, x, y)) continue;
                        }
                        else
                        {
                            if (every == null)
                            {
                                every = new List<(int q, int x, int y)>();
                                for (int qq = 0; qq < 4; qq++)
                                {
                                    CellarRoom rr = Rot(order[i], qq);
                                    for (int xx = 0; xx <= G - rr.Width; xx++) for (int yy = 0; yy <= G - rr.Height; yy++) if (Fits(rr.Width, rr.Height, xx, yy)) every.Add((qq, xx, yy));
                                }
                            }
                            if (every.Count == 0) break;
                            int pick = rng.Range(0, every.Count);
                            (q, x, y) = every[pick]; every[pick] = every[every.Count - 1]; every.RemoveAt(every.Count - 1);
                            rot = Rot(order[i], q);
                        }
                        int idx = Add(rot, q, x, y);
                        if (isLocked) lockedIndex = idx;
                        if (i == 0 || LinkNewest(idx, isLocked)) placed = true;
                        else { RemoveLast(); if (isLocked) lockedIndex = -1; }
                    }
                    if (!placed) { Fails[0]++; return null; }
                }

                if (!AddLoops()) { Fails[1]++; return null; }

                // chests from the rooms' own slots, then dead ends make up the rest of the target
                int roomChests = 0;
                foreach (PlacedRoom p in rooms) roomChests += p.Room.Chests.Count;
                int need = Math.Min(3, Math.Max(0, plan.TargetChests - roomChests));
                int nDead = Math.Max(rng.Range(1, 4), need);
                for (int i = 0; i < nDead; i++) if (AddDeadEnd() == null) { Fails[2]++; return null; }

                // key chest and gate-key carrier
                int keyRoom = -1;
                if (plan.Locked) for (int i = 0; i < rooms.Count; i++) if (rooms[i].Room.Id == plan.KeyRoom.Id) keyRoom = i;
                var carriers = new List<GateKeyCarrier>();
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (rooms[i].Room.Kind != RoomKind.Combat || i == lockedIndex) continue;
                    for (int s = 0; s < rooms[i].Room.Spawns.Count; s++)
                    {
                        CellarSpawn sp = rooms[i].Room.Spawns[s]; CellPos w = rooms[i].Cell(sp.X, sp.Y);
                        carriers.Add(new GateKeyCarrier(i, s, w.X, w.Y));
                    }
                }
                if (carriers.Count == 0) { Fails[3]++; return null; }
                GateKeyCarrier carrier = carriers[rng.Range(0, carriers.Count)];
                return Assemble(seed, plan.Floor, false, plan.Theme, carrier, keyRoom, need);
            }

            // Fixed placement for the floor 4 stub (no rng, no checks beyond Fits).
            public int PlaceFixed(CellarRoom room, int q, int x, int y)
            {
                CellarRoom rot = Rot(room, q);
                if (!Fits(rot.Width, rot.Height, x, y)) throw new InvalidOperationException("stub room " + room.Id + " does not fit at " + x + "," + y);
                return Add(rot, q, x, y);
            }

            public void LinkFixed(int ra, CellSide sa, int rb, CellSide sb)
            {
                int da = DoorOnSide(ra, sa), db = DoorOnSide(rb, sb);
                List<CellPos> path = Route(rooms[ra].DoorOutside(da), rooms[rb].DoorOutside(db), false);
                if (path == null) throw new InvalidOperationException("stub link has no route");
                Commit(ra, da, rb, db, false, path);
            }

            int DoorOnSide(int r, CellSide s)
            {
                for (int d = 0; d < rooms[r].Room.Doors.Count; d++) if (rooms[r].Room.Doors[d].Side == s) return d;
                throw new InvalidOperationException("room " + rooms[r].RoomId + " has no " + s + " door");
            }

            public CellarFloorLayout Assemble(uint seed, int floor, bool stub, SubTheme theme, GateKeyCarrier carrier, int keyRoom = -1, int deadChests = 0)
            {
                var corridors = new List<PlacedCorridor>();
                for (int y = 0; y < G; y++) for (int x = 0; x < G; x++)
                {
                    if (!corr[x, y]) continue;
                    if (!CellarRoomPool.FindCorridor(mask[x, y], out CellarRoom piece, out int turns)) throw new InvalidOperationException("corridor mask " + mask[x, y] + " at " + x + "," + y + " has no piece");
                    corridors.Add(new PlacedCorridor(x, y, mask[x, y], piece, turns));
                }
                var chests = new List<FloorChest>(); int keyIdx = -1;
                for (int r = 0; r < rooms.Count; r++)
                    foreach (ChestSlot c in rooms[r].Room.Chests)
                    {
                        CellPos w = rooms[r].Cell(c.X, c.Y);
                        bool key = r == keyRoom && c.KeyCandidate && keyIdx < 0;
                        if (key) keyIdx = chests.Count;
                        chests.Add(new FloorChest(w.X, w.Y, r, key));
                    }
                for (int i = 0; i < deadChests && i < deadEnds.Count; i++) chests.Add(new FloorChest(deadEnds[i].X, deadEnds[i].Y, -1, false));
                return new CellarFloorLayout(seed, floor, stub, theme, rooms, corridors, links, deadEnds, chests, lockedIndex, lockedLink, keyIdx, carrier);
            }
        }
    }
}
