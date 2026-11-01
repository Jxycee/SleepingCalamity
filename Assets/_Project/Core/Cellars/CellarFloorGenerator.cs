// Core/Cellars/CellarFloorGenerator.cs: the M5 seeded floor generator for the Undercroft Cellars (PHASE1 7.2). Pure C#, no engine references.
// Generate(seed, floor) builds floors 1-3 from the room pool; floor 4 is the fixed bakery oven maze (see BuildBakery, rooms in CellarBakeryPool). Authority: none (static; the host sends
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
// Retries: placement is retried inside one RNG stream (MaxAttempts whole attempts, RoomTries positions per room). Generate never throws for a valid floor:
// if room picking or placement is exhausted it retries over derived streams (Rng.Derive(seed, "cellars.retry", floor<<32 | n)), and as a last resort
// builds MinimalPlan (the 2-3 smallest unlocked combat rooms) over derived streams. Retry 0 is the original stream, so seeds that worked keep their floor.
// Generate(seed, floor, out fallback) reports 0 / 1 (retry) / 2 (last resort) so tests can count them.
// Exit distance (variety layer 7, research 3.7a): a built floor whose walk from the first party start to the stair exit is shorter than MinExitDistance
// steps is thrown away like any failed attempt (the attempt loop rolls again on the same stream), so the way down is never right next to the way in.
// Floors that were already long enough keep their layout; only the short ones re-roll. The last resort relaxes the bound only after every stream failed it.
// Layout presets (procgen item 2, CellarLayoutPreset.cs): floors 1-3 carry a LayoutPreset (Spine / Ring / Hub / Wing, own stream, never the same twice in a row) that steers
// the placement order, which earlier room each new room links to, and the loop rules inside TryBuild. The stair exit is placed LAST (before the locked room), links to exactly
// one earlier room that is not the entry, and is never offered a second door (FreeDoors, dead ends, loops and the locked room all skip it), so it is a far dead end: a leaf
// that is not next to the entry (an attempt whose exit door shares a corridor network with an entry door is thrown away). The last resort keeps the leaf rules but tries its
// first streams with the real preset and the rest with Spine (fewest constraints). Before it, two retry streams build the PICKED plan as Spine, so a stubborn preset keeps the floor's rooms; the layout reports the preset it really used.
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
        public const int MaxAttempts = 400, RoomTries = 60, ScanTries = 12, LinkTries = 6, PackFirstAfter = 100, MaxFootprint = 92, PlanRetries = 4, BuildRetries = 3, LastResortStreams = 16, LastResortPresetStreams = 8, SpinePlanRetries = 2, MinExitDistance = 12;
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
            return PickRooms(seed, floor, out _);
        }

        // fallback: 0 = original stream, 1 = a retry stream, 2 = the minimal plan.
        public static CellarFloorPlan PickRooms(uint seed, int floor, out int fallback)
        {
            if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor), "PickRooms is for floors 1-3");
            for (int retry = 0; retry <= PlanRetries; retry++)
            {
                CellarFloorPlan p = TryPickRooms(seed, floor, retry);
                if (p != null) { fallback = retry == 0 ? 0 : 1; return p; }
            }
            fallback = 2;
            return MinimalPlan(seed, floor);
        }

        // The guaranteed fallback plan: the smallest combat rooms (area, then id) that the previous floor did not use, unlocked. 3 rooms (2 on floor 2, which
        // also holds the checkpoint). Pure in (seed, floor).
        public static CellarFloorPlan MinimalPlan(uint seed, int floor)
        {
            if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor), "MinimalPlan is for floors 1-3");
            var excluded = new HashSet<string>();   // lookups only
            if (floor > 1) foreach (CellarRoom r in PickRooms(seed, floor - 1).Combat) excluded.Add(r.Id);
            var cand = new List<CellarRoom>();
            foreach (CellarRoom r in CellarRoomPool.All) if (r.Kind == RoomKind.Combat && r.LockedDoor == null && !excluded.Contains(r.Id)) cand.Add(r);
            cand.Sort((a, b) => { int c = (a.Width * a.Height).CompareTo(b.Width * b.Height); return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id); });
            int count = floor == 2 ? 2 : 3;
            return new CellarFloorPlan(floor, (SubTheme)floor, false, floor == 2, cand.GetRange(0, count), null, null, 6);
        }

        // One full pass of the room picking over its own RNG stream (retry 0 is the original stream, so existing seeds keep their plan). Null when the
        // 400 inner attempts all fail.
        static CellarFloorPlan TryPickRooms(uint seed, int floor, int retry)
        {
            SubTheme theme = (SubTheme)floor;
            var excluded = new HashSet<string>();   // lookups only
            if (floor > 1) foreach (CellarRoom r in PickRooms(seed, floor - 1).Combat) excluded.Add(r.Id);
            var all = new List<CellarRoom>();
            foreach (CellarRoom r in CellarRoomPool.All) if (r.Kind == RoomKind.Combat && !excluded.Contains(r.Id)) all.Add(r);
            SubTheme[] others = new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar }.Where(t => t != theme).ToArray();
            bool cp = floor == 2;
            var rng = new Pcg32(retry == 0 ? Rng.Derive(seed, "cellars.rooms", (ulong)floor) : Rng.Derive(seed, "cellars.retry", ((ulong)floor << 32) | (uint)retry));
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
            return null;
        }

        // ---- generation ----

        public static CellarFloorLayout Generate(uint seed, int floor) => Generate(seed, floor, out _);

        // fallback: 0 = the primary stream worked, 1 = a retry stream (room picking or placement) was needed, 2 = the minimal-plan last resort was used.
        // Deterministic in (seed, floor) whatever the fallback.
        public static CellarFloorLayout Generate(uint seed, int floor, out int fallback)
        {
            fallback = 0;
            if (floor == 4) return BuildBakery(seed);
            if (floor < 1 || floor > 4) throw new ArgumentOutOfRangeException(nameof(floor), "floors are 1-4");
            CellarFloorPlan plan = PickRooms(seed, floor, out fallback);
            LayoutPreset preset = CellarLayoutPresets.PresetFor(seed, floor);
            for (int retry = 0; retry <= BuildRetries; retry++)
            {
                ulong stream = retry == 0 ? FloorSeed(seed, floor) : Rng.Derive(seed, "cellars.retry", ((ulong)floor << 32) | (uint)(0x10000 + retry));
                CellarFloorLayout l = BuildWith(seed, plan, stream, MaxAttempts, MinExitDistance, preset);
                if (l != null) { if (retry > 0 && fallback == 0) fallback = 1; return l; }
            }
            // The picked rooms did not place under the preset: keep the rooms (so consecutive floors still share no combat room) and relax the preset to Spine, the loosest.
            for (int n = 0; n < SpinePlanRetries; n++)
            {
                CellarFloorLayout l = BuildWith(seed, plan, Rng.Derive(seed, "cellars.retry", ((ulong)floor << 32) | (uint)(0x20000 + n)), MaxAttempts, MinExitDistance, LayoutPreset.Spine);
                if (l != null) { fallback = 1; return l; }
            }
            fallback = 2;
            return GenerateLastResort(seed, floor);
        }

        // The last resort on its own (public so a test can force it): the minimal plan, built over up to LastResortStreams streams. It cannot fail for
        // the pool's small rooms; the throw is the unreachable end.
        public static CellarFloorLayout GenerateLastResort(uint seed, int floor)
        {
            if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor), "floors are 1-3");
            CellarFloorPlan plan = MinimalPlan(seed, floor);
            LayoutPreset real = CellarLayoutPresets.PresetFor(seed, floor);
            // Two streams get the full exit bound; the minimal plan rarely reaches it, so the rest relax it at once instead of burning every stream first.
            for (int n = 0; n < LastResortStreams; n++)
            {
                int bound = n < 2 ? MinExitDistance : 0;
                LayoutPreset preset = n < LastResortPresetStreams ? real : LayoutPreset.Spine;
                CellarFloorLayout l = BuildWith(seed, plan, Rng.Derive(seed, "cellars.lastresort", ((ulong)floor << 32) | (uint)n), MaxAttempts, bound, preset);
                if (l == null) continue;
                if (l.EntryToExitDistance() < MinExitDistance) l.MarkLastResort();   // a floor with a short exit beats no floor
                return l;
            }
            throw new InvalidOperationException("CellarFloorGenerator: even the minimal plan did not place for seed " + seed + " floor " + floor);
        }

        static CellarFloorLayout BuildWith(uint seed, CellarFloorPlan plan, ulong stream, int attempts, int minExit, LayoutPreset preset)
        {
            var b = new Builder(new Pcg32(stream)) { MinExit = minExit, Preset = preset };
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                b.Reset();
                CellarFloorLayout l = b.TryBuild(seed, plan, attempt);
                if (l != null) return l;
            }
            return null;
        }

        // Floor 4: the taken bakery's oven maze (PHASE1 7.2), a FIXED layout: the seed is ignored (every seed gives the same floor; only the layout's Seed
        // field records it). Rooms and positions are hard-coded, all unrotated; only the corridor routes are computed (unshuffled BFS, so they are fixed too).
        //   entry -> oven mouth -+-> proofing room -+-> kneading hall -> flue antechamber -> baker's hearth
        //                        '-> flour store ---'
        // The mouth, proofing room, kneading hall and flour store form a ring (the loop, two routes through the maze). The antechamber (the Dough Warden's
        // Tier2 spawn) has the hearth's only door, so the heart (baker marker + the rescue Bank cell, a second bank point that leads home) is reached only
        // through it. No stair exit, no locked room, no gate key; 7 chests in the maze rooms and the antechamber (PHASE1: 6-8 per floor).
        static CellarFloorLayout BuildBakery(uint seed)
        {
            var b = new Builder(new Pcg32(4444));
            b.Reset();
            int entry = b.PlaceFixed(Special(RoomKind.Entry), 0, 0, 0);
            int mouth = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.MouthId), 0, 5, 0);
            int proof = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.ProofingId), 0, 9, 0);
            int flour = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.FlourId), 0, 5, 5);
            int knead = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.KneadingId), 0, 9, 5);
            int ante = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.AntechamberId), 0, 9, 9);
            int heart = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.HeartId), 0, 2, 9);
            b.LinkFixed(entry, CellSide.East, mouth, CellSide.West);
            b.LinkFixed(mouth, CellSide.East, proof, CellSide.West);
            b.LinkFixed(mouth, CellSide.South, flour, CellSide.North);
            b.LinkFixed(proof, CellSide.South, knead, CellSide.North);
            b.LinkFixed(flour, CellSide.East, knead, CellSide.West);
            b.LinkFixed(knead, CellSide.South, ante, CellSide.North);
            b.LinkFixed(ante, CellSide.West, heart, CellSide.East);
            return b.Assemble(seed, 4, true, SubTheme.Bakery, null, preset: null);
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
            int lockedIndex = -1, lockedLink = -1, exitIndex = -1;
            readonly int[] roomLink = new int[8];       // per room: index of the link made when it was placed (-1 for the entry)
            public LayoutPreset Preset;
            int ringSrc = -1, ringJoin = -1, wingFrom = -1, wingTo = -1, reserveA = -1, reserveB = -1;   // preset bookkeeping set by TryBuild: ring (loop source, join room), wing (first and last room of the loop's branch)
            public int MinExit;                          // least entry-to-exit walking distance a built floor may have (0 = no bound)
            public readonly int[] Fails = new int[6];   // attempts lost per stage: room placement, loops, dead ends, carriers, exit too close, exit not a far leaf; shown when Generate gives up

            public Builder(Pcg32 rng) { this.rng = rng; }

            public void Reset()
            {
                rooms.Clear(); doorUsed.Clear(); links.Clear(); deadEnds.Clear(); lockedIndex = -1; lockedLink = -1; exitIndex = -1; ringSrc = -1; ringJoin = -1; wingFrom = -1; wingTo = -1; reserveA = -1; reserveB = -1;
                for (int i = 0; i < roomLink.Length; i++) roomLink[i] = -1;
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
                if (r == lockedIndex || r == exitIndex) return tmp;   // both are leaves
                for (int d = 0; d < doorUsed[r].Length; d++) if (!doorUsed[r][d]) tmp.Add(d);
                return tmp;
            }

            // Link the newest room (index k > 0) to an earlier room: the preset's preferred parent on the first PreferredTries attempts (when it still has a free door),
            // else a random one. noEntry (the stair exit) never links to the entry room.
            const int PreferredTries = 3;
            bool LinkNewest(int k, bool isLocked, int preferred = -1, bool noEntry = false)
            {
                for (int attempt = 0; attempt < LinkTries; attempt++)
                {
                    var avail = new List<int>();
                    for (int j = noEntry ? 1 : 0; j < k; j++) if (FreeDoors(j).Count > (isLocked ? (j == reserveA ? 1 : 0) + (j == reserveB ? 1 : 0) : 0)) avail.Add(j);
                    if (avail.Count == 0) return false;
                    int rj = preferred >= 0 && attempt < PreferredTries && avail.Contains(preferred) ? preferred : avail[rng.Range(0, avail.Count)];
                    List<int> fj = FreeDoors(rj); int dj = fj[rng.Range(0, fj.Count)];
                    int dk;
                    if (isLocked) { dk = -1; for (int d = 0; d < rooms[k].Room.Doors.Count; d++) if (rooms[k].Room.Doors[d].Locked) dk = d; if (dk < 0) return false; }
                    else { var fk = new List<int>(); for (int d = 0; d < doorUsed[k].Length; d++) if (!doorUsed[k][d]) fk.Add(d); dk = fk[rng.Range(0, fk.Count)]; }
                    if (TryRoute(rj, dj, k, dk, isLocked)) return true;
                }
                return false;
            }

            bool Linked(int a, int b) { foreach (FloorLink l in links) if (l.Touches(a) && l.Touches(b)) return true; return false; }

            // A loop corridor from a free door of room a back onto the corridor cell `target` (a T junction). False when no free door of a routes there.
            bool LoopToCell(int a, CellPos target)
            {
                List<int> fa = new List<int>(FreeDoors(a));
                Shuffle(fa, ref rng);
                foreach (int da in fa)
                {
                    List<CellPos> path = Route(rooms[a].DoorOutside(da), target, true);
                    if (path == null) continue;
                    Commit(a, da, -1, -1, false, path);
                    return true;
                }
                return false;
            }

            // Ring: the second arc. The loop runs from the end of the second chain (the entry when that chain is empty) onto the corridor just outside the join room's door,
            // so the join room keeps its second door for the exit. Then at most one extra generic loop.
            bool AddRingLoops()
            {
                if (ringSrc < 0 || ringJoin < 0 || roomLink[ringJoin] < 0) return false;
                IReadOnlyList<CellPos> jp = links[roomLink[ringJoin]].Path;
                if (!LoopToCell(ringSrc, jp[jp.Count - 1])) return false;
                return AddLoops(rng.Range(0, 2), 0);
            }

            // Wing: one loop that stays inside the dead branch (rooms wingFrom..wingTo): from a branch room with a free door onto a corridor cell of the link that
            // placed one of the branch's rooms, so it never joins the two halves of the path.
            bool AddWingLoop()
            {
                if (wingFrom < 0) return false;
                for (int t = 0; t < 12; t++)
                {
                    int a = rng.Range(wingFrom, wingTo + 1);
                    if (FreeDoors(a).Count == 0) { if (wingFrom == wingTo) a = 0; else continue; }   // a one-room branch loops from the entry's spare door onto that room's own link
                    int b = rng.Range(wingFrom, wingTo + 1);
                    if (roomLink[b] < 0) continue;
                    IReadOnlyList<CellPos> p = links[roomLink[b]].Path;
                    if (LoopToCell(a, p[rng.Range(0, p.Count)])) return true;
                }
                return false;
            }

            // Extra corridors between rooms that already connect, so the map has loops. True if at least `min` were made (`want` tried for).
            bool AddLoops(int want, int min)
            {
                int made = 0;
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
                return made >= min;
            }

            // A straight stub of 1-3 corridor cells off an unused door or off a corridor cell. Returns the end cell, or null when nothing fits.
            CellPos? AddDeadEnd()
            {
                var cands = new List<(CellPos start, CellSide dir, bool fromDoor, CellPos from, int room, int door)>();
                for (int r = 0; r < rooms.Count; r++)
                {
                    if (r == lockedIndex || r == exitIndex) continue;
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
                if (plan.HasCheckpoint) middle.Add(Special(RoomKind.Checkpoint));
                Shuffle(middle, ref rng);
                if (attempt >= PackFirstAfter)   // a crowded floor: place the big rooms first (stable sort, so the shuffle still breaks ties)
                    for (int i = 1; i < middle.Count; i++)
                        for (int j = i; j > 0 && middle[j].Width * middle[j].Height > middle[j - 1].Width * middle[j - 1].Height; j--) { CellarRoom t = middle[j]; middle[j] = middle[j - 1]; middle[j - 1] = t; }
                if (Preset == LayoutPreset.Hub)   // the hub is the biggest combat room (ties by id), placed right after the entry
                {
                    int best = -1;
                    for (int i = 0; i < middle.Count; i++)
                        if (middle[i].Kind == RoomKind.Combat && (best < 0 || middle[i].Width * middle[i].Height > middle[best].Width * middle[best].Height
                            || (middle[i].Width * middle[i].Height == middle[best].Width * middle[best].Height && string.CompareOrdinal(middle[i].Id, middle[best].Id) < 0))) best = i;
                    if (best > 0) { CellarRoom h = middle[best]; middle.RemoveAt(best); middle.Insert(0, h); }
                }
                order.Add(Special(RoomKind.Entry)); order.AddRange(middle);
                if (plan.Locked) order.Add(plan.LockedRoom);
                order.Add(Special(RoomKind.StairExit));   // placed LAST: nothing links to it afterwards, so it is a far dead end (the locked leaf goes just before it)

                // Preset steering: pref[i] is the preferred parent (a room index = placement position) of the room placed i-th, -1 = any. Positions 1..m are the middle rooms,
                // the locked room (when there is one) m + 1, the exit last.
                int m = middle.Count, lockedPos = plan.Locked ? m + 1 : -1, exitPos = order.Count - 1;
                var pref = new int[order.Count];
                for (int i = 0; i < pref.Length; i++) pref[i] = -1;
                pref[exitPos] = m; reserveA = m;   // the exit's parent keeps a door for it; the preset's loop source keeps one for the loop (the locked room may not take them)
                switch (Preset)
                {
                    case LayoutPreset.Spine:
                        for (int i = 1; i <= m; i++) pref[i] = rng.Range(0, 4) < 3 ? i - 1 : -1;   // mostly the newest room
                        break;
                    case LayoutPreset.Hub:
                        pref[1] = 0;
                        for (int i = 2; i <= m; i++) pref[i] = rng.Range(0, 10) < 7 ? 1 : -1;      // most rooms link to the hub
                        break;
                    case LayoutPreset.Ring:
                    {
                        int p = m / 2;   // chain A = rooms 1..p, chain B = p+1..m-1, the join room is m (the exit hangs off it)
                        for (int i = 1; i <= p; i++) pref[i] = i == 1 ? 0 : i - 1;
                        for (int i = p + 1; i < m; i++) pref[i] = i == p + 1 ? 0 : i - 1;
                        pref[m] = p >= 1 ? p : 0;
                        ringJoin = m; ringSrc = m - 1 >= p + 1 ? m - 1 : 0; reserveB = ringSrc;
                        break;
                    }
                    case LayoutPreset.Wing:
                    {
                        int ny = m / 2;  // branch Y = rooms 1..ny (a dead branch), branch X = ny+1..m (ends in the exit); X is never the smaller one
                        for (int i = 1; i <= ny; i++) pref[i] = i == 1 ? 0 : i - 1;
                        for (int i = ny + 1; i <= m; i++) pref[i] = i == ny + 1 ? 0 : i - 1;
                        wingFrom = 1; wingTo = ny >= 1 ? ny : 1;   // the loop lives in the dead branch Y (never bypasses the spine); with no Y the single room's own link
                        reserveB = ny >= 1 ? ny : 0;
                        break;
                    }
                }

                for (int i = 0; i < order.Count; i++)
                {
                    bool isLocked = i == lockedPos;
                    bool isExit = i == exitPos;
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
                        if (i == 0 || LinkNewest(idx, isLocked, pref[i], isExit))
                        {
                            placed = true;
                            if (i > 0) roomLink[idx] = links.Count - 1;
                            if (isExit) exitIndex = idx;
                        }
                        else { RemoveLast(); if (isLocked) lockedIndex = -1; }
                    }
                    if (!placed) { Fails[0]++; return null; }
                }

                bool loopsOk;
                switch (Preset)
                {
                    case LayoutPreset.Spine: loopsOk = AddLoops(rng.Range(0, 2), 0); break;
                    case LayoutPreset.Ring: loopsOk = AddRingLoops(); break;
                    case LayoutPreset.Hub: loopsOk = AddLoops(rng.Range(1, 3), 1); break;
                    default: loopsOk = AddWingLoop(); break;
                }
                if (!loopsOk) { Fails[1]++; return null; }

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
                CellarFloorLayout built = Assemble(seed, plan.Floor, false, plan.Theme, carrier, keyRoom, need, Preset);
                if (!built.ExitIsLeaf()) { Fails[5]++; return null; }
                if (MinExit > 0 && built.EntryToExitDistance() < MinExit) { Fails[4]++; return null; }
                if (built.ExitAdjacentToEntry()) { Fails[5]++; return null; }   // dearest check last; the same attempts fail, only the cheap ones first
                return built;
            }

            // Fixed placement for floor 4 (no rng, no checks beyond Fits).
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

            public CellarFloorLayout Assemble(uint seed, int floor, bool stub, SubTheme theme, GateKeyCarrier carrier, int keyRoom = -1, int deadChests = 0, LayoutPreset? preset = null)
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
                return new CellarFloorLayout(seed, floor, stub, theme, rooms, corridors, links, deadEnds, chests, lockedIndex, lockedLink, keyIdx, carrier, preset);
            }
        }
    }
}
