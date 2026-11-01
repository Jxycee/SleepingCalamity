// Core/Cellars/CellarFloorRoute.cs: the route read of one finished Cellars floor (procgen item 1a, research procedural-dungeons takeaways: a main path with loops that cross it).
// A pure resolver over the CellarFloorLayout, like CellarFloorDressing.Resolve: the layout and its LayoutHash are untouched.
//   - the graph is rooms plus corridor cells (CellarRoomGraph; some links end on an existing corridor, so room-to-room links alone would miss T junctions); the locked room
//     is a leaf behind its gate and is on no route;
//   - the main path (Spine) is the fewest-steps route from the entry room to the stair-exit room; the BFS visits nodes in index order, so ties break the same way everywhere;
//   - an arc is an alternate route between two Spine nodes that runs only through rooms and corridor cells off the Spine: the floor's existing loops (a second corridor
//     between two Spine nodes is an arc with no room on it). One shortest arc is kept per pair of Spine nodes. Rooms on any arc are Arc, other unlocked rooms are Branch (off-path, dead-end leaves included), the locked room is Locked;
//   - the floor's cycle role is for ONE chosen arc, drawn on its own stream Rng.Derive(seed, "cellars.route", floor): KeyArc (the arc holds the key chest's room, floor locked;
//     preferred with KeyArcPercent chance when such an arc exists), else ShortcutBack (the arc rejoins the Spine in its last third and skips at least one Spine room: label
//     only, no mechanic yet), else Plain; None when no arc crosses the path.
// The three draws are always made, so the stream position never depends on the layout. No string.GetHashCode (only Rng.Derive labels), no dictionary or set iteration for a decision, no float.
// Floor 4 (the fixed bakery) returns the empty route. RouteHash() is the cross-peer proof. Authority: none (every peer derives the same route from the layout).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public enum RoomRole : byte { Spine = 0, Arc = 1, Branch = 2, Locked = 3 }

    public enum CycleRole : byte { None = 0, Plain = 1, KeyArc = 2, ShortcutBack = 3 }

    // One alternate route: it leaves the Spine at node From, runs through Rooms (the rooms on it, in walking order; may be empty for a second corridor) and rejoins at node To.
    // Node ids are the room index when below the room count, else CellarRoomGraph.CellNode.
    public sealed class RouteArc
    {
        public readonly int From, To; public readonly IReadOnlyList<int> Rooms;
        public RouteArc(int from, int to, IEnumerable<int> rooms) { From = from; To = to; Rooms = rooms.ToList(); }
    }

    // The room graph of a layout. A FloorLink with RoomB -1 is a branch that ends on an existing corridor cell (a T junction), so room-to-room links alone miss rooms that
    // share a corridor network; this graph reads the corridor cells instead: two doors are connected when their outside cells lie in one connected piece of corridor (cells
    // joined by their neighbour masks). The locked room is kept out of every network (it is a leaf behind its gate); DoorNeighbours reports the gate pair explicitly, so the door
    // outside the gate sees the locked room in addition to its network. Neighbour lists are in room-index order.
    public sealed class CellarRoomGraph
    {
        readonly int[][] doorComp; readonly List<int>[] adjacent; readonly int[][][] byComp; readonly CellarFloorLayout layout;

        CellarRoomGraph(CellarFloorLayout l, int[][] doorComp, List<int>[] adjacent, int[][][] byComp) { layout = l; this.doorComp = doorComp; this.adjacent = adjacent; this.byComp = byComp; }

        public List<int> Adjacent(int room) => adjacent[room];

        // The walking graph the route uses: nodes 0..Rooms-1 are rooms, Rooms + y * GridSize + x is the corridor cell (x, y). A room touches the corridor cell outside each of its
        // open doors (the locked room touches nothing); corridor cells touch the cells their neighbour masks open onto. A cycle in this graph is a real loop of walking routes.
        // Lists are in node order.
        public static int CellNode(CellarFloorLayout layout, int x, int y) => layout.Rooms.Count + y * CellarFloorLayout.GridSize + x;

        public List<int>[] NodeAdjacency()
        {
            const int G = CellarFloorLayout.GridSize;
            int n = layout.Rooms.Count, total = n + G * G;
            var adj = new List<int>[total]; for (int i = 0; i < total; i++) adj[i] = new List<int>();
            var corr = new PlacedCorridor[G, G]; foreach (PlacedCorridor c in layout.Corridors) corr[c.X, c.Y] = c;
            var sides = new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West };
            foreach (PlacedCorridor c in layout.Corridors)
                foreach (CellSide s in sides)
                {
                    if ((c.Mask & CellarRoom.MaskBit(s)) == 0) continue;
                    CellPos m = new CellPos(c.X, c.Y).Step(s);
                    if (m.X >= 0 && m.Y >= 0 && m.X < G && m.Y < G && corr[m.X, m.Y] != null) adj[CellNode(layout, c.X, c.Y)].Add(CellNode(layout, m.X, m.Y));
                }
            for (int r = 0; r < n; r++)
            {
                if (r == layout.LockedRoomIndex) continue;
                for (int d = 0; d < doorComp[r].Length; d++)
                {
                    if (doorComp[r][d] < 0) continue;
                    CellPos o = layout.Rooms[r].DoorOutside(d); int cn = CellNode(layout, o.X, o.Y);
                    adj[r].Add(cn); adj[cn].Add(r);
                }
            }
            foreach (List<int> a in adj) a.Sort();
            return adj;
        }

        // Rooms reached through this door: every other room with a door on the same corridor network (or the locked room across the locked door). Empty for walled doors.
        public List<int> DoorNeighbours(int room, int door)
        {
            var result = new List<int>();
            int comp = doorComp[room][door];
            if (comp >= 0) foreach (int[] rd in byComp[comp]) if (rd[0] != room && !result.Contains(rd[0])) result.Add(rd[0]);
            int lk = layout.LockedLinkIndex;
            if (lk >= 0)
            {
                FloorLink l = layout.Links[lk];
                if (l.RoomA == room && l.DoorA == door && !result.Contains(l.RoomB)) result.Add(l.RoomB);
                if (l.RoomB == room && l.DoorB == door && !result.Contains(l.RoomA)) result.Add(l.RoomA);
            }
            result.Sort();
            return result;
        }

        public static CellarRoomGraph Build(CellarFloorLayout layout)
        {
            const int G = CellarFloorLayout.GridSize;
            int n = layout.Rooms.Count;
            var corr = new PlacedCorridor[G, G]; foreach (PlacedCorridor c in layout.Corridors) corr[c.X, c.Y] = c;
            var comp = new int[G, G]; for (int x = 0; x < G; x++) for (int y = 0; y < G; y++) comp[x, y] = -1;
            int count = 0; var sides = new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West };
            for (int y = 0; y < G; y++)
                for (int x = 0; x < G; x++)
                {
                    if (corr[x, y] == null || comp[x, y] >= 0) continue;
                    var q = new Queue<CellPos>(); comp[x, y] = count; q.Enqueue(new CellPos(x, y));
                    while (q.Count > 0)
                    {
                        CellPos c = q.Dequeue();
                        foreach (CellSide s in sides)
                        {
                            if ((corr[c.X, c.Y].Mask & CellarRoom.MaskBit(s)) == 0) continue;
                            CellPos m = c.Step(s);
                            if (m.X < 0 || m.Y < 0 || m.X >= G || m.Y >= G || corr[m.X, m.Y] == null || comp[m.X, m.Y] >= 0) continue;
                            comp[m.X, m.Y] = count; q.Enqueue(m);
                        }
                    }
                    count++;
                }
            var doorComp = new int[n][]; var lists = new List<int[]>[count]; for (int i = 0; i < count; i++) lists[i] = new List<int[]>();
            for (int r = 0; r < n; r++)
            {
                PlacedRoom p = layout.Rooms[r]; doorComp[r] = new int[p.Room.Doors.Count];
                for (int d = 0; d < doorComp[r].Length; d++)
                {
                    doorComp[r][d] = -1;
                    if (r == layout.LockedRoomIndex || !layout.DoorIsOpen(r, d)) continue;   // the locked room joins no network: it is reached only through its own gate
                    CellPos o = p.DoorOutside(d);
                    if (o.X < 0 || o.Y < 0 || o.X >= G || o.Y >= G || comp[o.X, o.Y] < 0) continue;
                    doorComp[r][d] = comp[o.X, o.Y]; lists[comp[o.X, o.Y]].Add(new[] { r, d });
                }
            }
            var byComp = new int[count][][]; for (int i = 0; i < count; i++) byComp[i] = lists[i].ToArray();
            var adj = new List<int>[n]; for (int i = 0; i < n; i++) adj[i] = new List<int>();
            for (int i = 0; i < count; i++)
                foreach (int[] a in byComp[i]) foreach (int[] b in byComp[i]) if (a[0] != b[0] && !adj[a[0]].Contains(b[0])) adj[a[0]].Add(b[0]);
            foreach (List<int> a in adj) a.Sort();
            return new CellarRoomGraph(layout, doorComp, adj, byComp);
        }
    }

    public sealed class CellarFloorRoute
    {
        // PLACEHOLDERS (tuning, not tested for feel): KeyArcPercent is the chance to choose an arc that holds the key chest when one exists; ShortcutMinSteps is how many
        // walking nodes (rooms and corridor cells) of the Spine the arc must skip to count as a shortcut.
        public const int KeyArcPercent = 70, ShortcutMinSteps = 6;

        public readonly uint Seed; public readonly int Floor;
        public readonly IReadOnlyList<int> Spine;          // room indices, entry first, exit last (empty on floor 4 or when no route exists)
        public readonly IReadOnlyList<RoomRole> Roles;     // one per room (empty on floor 4)
        public readonly IReadOnlyList<RouteArc> Arcs;      // every arc found, sorted by (Spine position of From, Spine position of To)
        public readonly int ChosenArc;                     // index into Arcs, -1 when there is none
        public readonly CycleRole Cycle;

        CellarFloorRoute(uint seed, int floor, List<int> spine, List<RoomRole> roles, List<RouteArc> arcs, int chosen, CycleRole cycle)
        { Seed = seed; Floor = floor; Spine = spine; Roles = roles; Arcs = arcs; ChosenArc = chosen; Cycle = cycle; }

        public bool IsEmpty => Roles.Count == 0;
        public RouteArc Chosen => ChosenArc < 0 ? null : Arcs[ChosenArc];
        public RoomRole RoleOf(int room) => room >= 0 && room < Roles.Count ? Roles[room] : RoomRole.Branch;

        public static CellarFloorRoute Resolve(CellarFloorLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.IsStub) return new CellarFloorRoute(layout.Seed, layout.Floor, new List<int>(), new List<RoomRole>(), new List<RouteArc>(), -1, CycleRole.None);
            int n = layout.Rooms.Count, locked = layout.LockedRoomIndex;
            CellarRoomGraph graph = CellarRoomGraph.Build(layout);
            List<int>[] adj = graph.NodeAdjacency(); int total = adj.Length;   // nodes: rooms 0..n-1, then the corridor networks
            int entry = -1, exit = -1;
            for (int i = 0; i < n; i++)
            {
                RoomKind kind = layout.Rooms[i].Room.Kind;
                if (kind == RoomKind.Entry && entry < 0) entry = i;
                else if (kind == RoomKind.StairExit && exit < 0) exit = i;
            }
            var roles = new List<RoomRole>(); for (int i = 0; i < n; i++) roles.Add(i == locked ? RoomRole.Locked : RoomRole.Branch);
            var spine = new List<int>(); var nodePath = new List<int>();
            if (entry >= 0 && exit >= 0)
            {
                var parent = new int[total]; for (int i = 0; i < total; i++) parent[i] = -2;
                var q = new Queue<int>(); parent[entry] = -1; q.Enqueue(entry);
                while (q.Count > 0)
                {
                    int c = q.Dequeue();
                    foreach (int m in adj[c]) if (parent[m] == -2) { parent[m] = c; q.Enqueue(m); }
                }
                if (parent[exit] != -2) { for (int c = exit; c >= 0; c = parent[c]) nodePath.Add(c); nodePath.Reverse(); foreach (int c in nodePath) if (c < n) spine.Add(c); }
            }
            var arcs = new List<RouteArc>();
            if (spine.Count > 0)
            {
                var pos = new int[total]; for (int i = 0; i < total; i++) pos[i] = -1;
                for (int s = 0; s < nodePath.Count; s++) { pos[nodePath[s]] = s; if (nodePath[s] < n) roles[nodePath[s]] = RoomRole.Spine; }
                for (int s = 0; s < nodePath.Count; s++)
                {
                    // BFS from Spine node s through off-Spine rooms and corridor cells; reaching a later Spine node closes an arc (first, i.e. shortest, per target). An arc may
                    // pass no room at all (a second corridor between two Spine nodes) but always runs through at least one node that is off the Spine.
                    var par = new int[total]; for (int i = 0; i < total; i++) par[i] = -2;
                    var q = new Queue<int>(); var closed = new bool[nodePath.Count];
                    foreach (int m in adj[nodePath[s]]) if (pos[m] < 0 && par[m] == -2) { par[m] = -1; q.Enqueue(m); }
                    var found = new List<RouteArc>();
                    while (q.Count > 0)
                    {
                        int c = q.Dequeue();
                        foreach (int m in adj[c])
                        {
                            if (pos[m] > s && !closed[pos[m]])
                            {
                                closed[pos[m]] = true;
                                var path = new List<int>(); for (int x = c; x >= 0; x = par[x]) if (x < n) path.Add(x);
                                path.Reverse(); found.Add(new RouteArc(nodePath[s], m, path));
                            }
                            else if (pos[m] < 0 && par[m] == -2) { par[m] = c; q.Enqueue(m); }
                        }
                    }
                    found.Sort((a, b) => pos[a.To].CompareTo(pos[b.To]));
                    arcs.AddRange(found);
                }
                foreach (RouteArc a in arcs) foreach (int r in a.Rooms) roles[r] = RoomRole.Arc;
            }

            var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.route", (ulong)layout.Floor));
            int keyRoll = rng.Range(0, 100), keyPick = (int)(rng.NextUInt() & 0x7FFFFFFF), anyPick = (int)(rng.NextUInt() & 0x7FFFFFFF);
            int chosen = -1; var cycle = CycleRole.None;
            if (arcs.Count > 0)
            {
                int keyRoom = layout.KeyChest != null ? layout.KeyChest.RoomIndex : -1;
                var keyArcs = new List<int>();
                if (layout.HasLockedRoom && keyRoom >= 0) for (int i = 0; i < arcs.Count; i++) if (arcs[i].Rooms.Contains(keyRoom)) keyArcs.Add(i);
                if (keyArcs.Count > 0 && keyRoll < KeyArcPercent) { chosen = keyArcs[keyPick % keyArcs.Count]; cycle = CycleRole.KeyArc; }
                else
                {
                    chosen = anyPick % arcs.Count;
                    int posTo = nodePath.IndexOf(arcs[chosen].To), posFrom = nodePath.IndexOf(arcs[chosen].From);
                    bool lastThird = posTo * 3 >= (nodePath.Count - 1) * 2;
                    cycle = lastThird && posTo - posFrom >= ShortcutMinSteps ? CycleRole.ShortcutBack : CycleRole.Plain;
                }
            }
            return new CellarFloorRoute(layout.Seed, layout.Floor, spine, roles, arcs, chosen, cycle);
        }

        public ulong RouteHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(Spine.Count);
            foreach (int s in Spine) h.AppendInt32LE(s);
            h.AppendInt32LE(Roles.Count);
            foreach (RoomRole r in Roles) h.AppendInt32LE((int)r);
            h.AppendInt32LE(Arcs.Count);
            foreach (RouteArc a in Arcs) { h.AppendInt32LE(a.From); h.AppendInt32LE(a.To); h.AppendInt32LE(a.Rooms.Count); foreach (int r in a.Rooms) h.AppendInt32LE(r); }
            h.AppendInt32LE(ChosenArc); h.AppendInt32LE((int)Cycle);
            return h.Value;
        }
    }
}
