// Core/Cellars/CellarDoorHints.cs: reward hints on the doors of one Cellars floor (procgen item 1b). Cellars floors have one stair exit, so a Hades-style "pick a door by its reward"
// does not map literally; the reading used here: every open room door shows what lies in the room on the other side of it. A pure resolver over the finished layout (plus the
// special-room tag and the dressing's vignettes); the layout and its hash are untouched. One hint per open door, in room order then door order; walled doors get none.
// "The other side" is the room(s) reached FIRST through the door: walk the corridor cells from the door's outside cell (BFS in fixed side order) and stop expanding at the
// outside cell of any other room's open door, collecting that room. Usually one room; a T junction before the next door can give two or more. The door's own room is never
// included, and the locked room is added only on the gate door (the locked room's own door sees the room outside the gate). Earlier versions merged every room on the whole
// corridor network, which flagged ~97% of doors and made the hint no choice at all. The hint is the union of those first-reached rooms (neighbours sorted). Flags: Chests (room chest slots in the neighbours; ChestCount), Key (a neighbour holds the key chest), Special (a neighbour
// is the floor's special room; SpecialKind), RewardVignette (a neighbour holds a Reward or RiskReward vignette), Exit (the stair room), Checkpoint (the bank room), Locked (the
// locked door). Dead-end chests in corridors (FloorChest.RoomIndex -1) belong to no room and are not counted. Floor 4 (the fixed bakery) returns no hints. HintsHash() is the
// cross-peer proof. Authority: none (every peer derives the same hints).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    [Flags] public enum DoorHintFlags : byte { None = 0, Chests = 1, Key = 2, Special = 4, RewardVignette = 8, Exit = 16, Checkpoint = 32, Locked = 64 }

    public sealed class DoorHint
    {
        public readonly int RoomIndex, DoorIndex; public readonly IReadOnlyList<int> Neighbours; public readonly DoorHintFlags Flags; public readonly int ChestCount; public readonly SpecialKind SpecialKind;
        public DoorHint(int room, int door, IReadOnlyList<int> neighbours, DoorHintFlags flags, int chestCount, SpecialKind special)
        { RoomIndex = room; DoorIndex = door; Neighbours = neighbours; Flags = flags; ChestCount = chestCount; SpecialKind = special; }
        public bool Has(DoorHintFlags f) => (Flags & f) == f;
    }

    public sealed class CellarDoorHints
    {
        public readonly int Floor; public readonly IReadOnlyList<DoorHint> Hints;
        CellarDoorHints(int floor, List<DoorHint> hints) { Floor = floor; Hints = hints; }

        public DoorHint Find(int room, int door) { foreach (DoorHint h in Hints) if (h.RoomIndex == room && h.DoorIndex == door) return h; return null; }

        public static CellarDoorHints Resolve(CellarFloorLayout layout, SpecialRoom special = null, CellarFloorDressing dressing = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var hints = new List<DoorHint>();
            if (layout.IsStub) return new CellarDoorHints(layout.Floor, hints);
            special = special ?? SpecialRoomDive.Resolve(layout);
            dressing = dressing ?? CellarFloorDressing.Resolve(layout);
            CellarRoomGraph graph = CellarRoomGraph.Build(layout);
            int lockedDoorRoom = -1, lockedDoor = -1;
            if (layout.LockedLinkIndex >= 0)
            {
                FloorLink ll = layout.Links[layout.LockedLinkIndex];
                lockedDoorRoom = ll.RoomA == layout.LockedRoomIndex ? ll.RoomB : ll.RoomA; lockedDoor = ll.RoomA == layout.LockedRoomIndex ? ll.DoorB : ll.DoorA;
            }
            Reach reach = new Reach(layout);
            for (int r = 0; r < layout.Rooms.Count; r++)
                for (int d = 0; d < layout.Rooms[r].Room.Doors.Count; d++)
                {
                    List<int> nb = graph.DoorNeighbours(r, d);
                    if (nb.Count == 0) continue;
                    if (r != layout.LockedRoomIndex)
                    {
                        nb = reach.FirstReached(r, d);
                        if (layout.LockedLinkIndex >= 0 && r == lockedDoorRoom && d == lockedDoor && !nb.Contains(layout.LockedRoomIndex)) { nb.Add(layout.LockedRoomIndex); nb.Sort(); }
                        if (nb.Count == 0) continue;
                    }
                    hints.Add(Describe(layout, special, dressing, r, d, nb, r == lockedDoorRoom && d == lockedDoor));
                }
            return new CellarDoorHints(layout.Floor, hints);
        }

        // Corridor walk for FirstReached. stopAt[x, y] lists the rooms whose open door has its outside cell at (x, y), in room order (the locked room's door joins no network).
        sealed class Reach
        {
            const int G = CellarFloorLayout.GridSize;
            readonly CellarFloorLayout layout; readonly PlacedCorridor[,] corr = new PlacedCorridor[G, G]; readonly List<int>[,] stopAt = new List<int>[G, G];
            static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

            public Reach(CellarFloorLayout l)
            {
                layout = l; foreach (PlacedCorridor c in l.Corridors) corr[c.X, c.Y] = c;
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    if (r == l.LockedRoomIndex) continue;
                    for (int d = 0; d < l.Rooms[r].Room.Doors.Count; d++)
                    {
                        if (!l.DoorIsOpen(r, d)) continue;
                        CellPos o = l.Rooms[r].DoorOutside(d);
                        if (o.X < 0 || o.Y < 0 || o.X >= G || o.Y >= G || corr[o.X, o.Y] == null) continue;
                        (stopAt[o.X, o.Y] ?? (stopAt[o.X, o.Y] = new List<int>())).Add(r);
                    }
                }
            }

            public List<int> FirstReached(int room, int door)
            {
                var found = new List<int>(); var seen = new bool[G, G];
                CellPos o = layout.Rooms[room].DoorOutside(door);
                if (o.X < 0 || o.Y < 0 || o.X >= G || o.Y >= G || corr[o.X, o.Y] == null) return found;
                var q = new Queue<CellPos>(); seen[o.X, o.Y] = true; q.Enqueue(o);
                while (q.Count > 0)
                {
                    CellPos c = q.Dequeue(); bool stop = false;
                    List<int> here = stopAt[c.X, c.Y];
                    if (here != null) foreach (int other in here) if (other != room) { stop = true; if (!found.Contains(other)) found.Add(other); }
                    if (stop) continue;   // a door of another room: that room is the first reached on this branch
                    foreach (CellSide s in Sides)
                    {
                        if ((corr[c.X, c.Y].Mask & CellarRoom.MaskBit(s)) == 0) continue;
                        CellPos m = c.Step(s);
                        if (m.X < 0 || m.Y < 0 || m.X >= G || m.Y >= G || corr[m.X, m.Y] == null || seen[m.X, m.Y]) continue;
                        seen[m.X, m.Y] = true; q.Enqueue(m);
                    }
                }
                found.Sort(); return found;
            }
        }

        static DoorHint Describe(CellarFloorLayout layout, SpecialRoom special, CellarFloorDressing dressing, int room, int door, List<int> nb, bool locked)
        {
            DoorHintFlags f = DoorHintFlags.None; int chests = 0; SpecialKind kind = SpecialKind.None;
            foreach (int other in nb)
            {
                foreach (FloorChest c in layout.Chests)
                {
                    if (c.RoomIndex != other) continue;
                    chests++; if (c.HoldsKey) f |= DoorHintFlags.Key;
                }
                if (special.Exists && special.RoomIndex == other) { kind = special.Kind; f |= DoorHintFlags.Special; }
                foreach (PlacedVignette pv in dressing.Vignettes)
                    if (pv.RoomIndex == other && (pv.Vignette.Category == VignetteCategory.Reward || pv.Vignette.Category == VignetteCategory.RiskReward)) f |= DoorHintFlags.RewardVignette;
                RoomKind rk = layout.Rooms[other].Room.Kind;
                if (rk == RoomKind.StairExit) f |= DoorHintFlags.Exit;
                if (rk == RoomKind.Checkpoint) f |= DoorHintFlags.Checkpoint;
            }
            if (chests > 0) f |= DoorHintFlags.Chests;
            if (locked) f |= DoorHintFlags.Locked;
            return new DoorHint(room, door, nb, f, chests, kind);
        }

        public ulong HintsHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE(Hints.Count);
            foreach (DoorHint x in Hints)
            {
                h.AppendInt32LE(x.RoomIndex); h.AppendInt32LE(x.DoorIndex); h.AppendInt32LE(x.Neighbours.Count);
                foreach (int n in x.Neighbours) h.AppendInt32LE(n);
                h.AppendInt32LE((int)x.Flags); h.AppendInt32LE(x.ChestCount); h.AppendInt32LE((int)x.SpecialKind);
            }
            return h.Value;
        }
    }
}
