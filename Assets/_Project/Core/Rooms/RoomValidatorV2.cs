// Core/Rooms/RoomValidatorV2.cs: the state-aware room validator (metrics gym plan, Task 4; spec 11.5 F7). It proves no gimmick state can strand a hero:
// a search over (cell, state vector) nodes from every door in every state. Pure C#. Authority: none (static data checked at build/test time).
// Rules 1-2 stop the run on failure; rules 3-8 collect; rule 9 (the softlock search) runs only when 3-8 found nothing, because it assumes sane data.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public static class RoomValidatorV2
    {
        const int MaxRegions = 4, MinRegionStates = 2, MaxRegionStates = 4;

        public static int StateCount(RoomDataV2 room) { int n = 1; foreach (GimmickRegion r in room.Regions) n *= Math.Max(1, r.StateCount); return n; }

        public static List<string> ValidateAll(IEnumerable<RoomDataV2> rooms)
        {
            var errs = new List<string>();
            foreach (RoomDataV2 r in rooms) errs.AddRange(Validate(r));
            return errs;
        }

        // partyTools: the tools the party owns; null or empty = none (a Control with a Tool other than None then does nothing).
        public static List<string> Validate(RoomDataV2 room, IReadOnlyCollection<ToolVerb> partyTools = null)
        {
            var errs = new List<string>();
            string id = room.Base.Id;
            void Err(string m) => errs.Add(id + ": " + m);

            // 1. version
            if (room.Version != RoomDataV2.SchemaVersion) { Err("room data version " + room.Version + " is not supported (this build reads " + RoomDataV2.SchemaVersion + ")"); return errs; }

            // 2. the v1 rules, and one v2 door per v1 door
            CellarRoom b = room.Base;
            errs.AddRange(b.Validate());
            if (room.Doors.Count != b.Doors.Count) Err("has " + room.Doors.Count + " v2 doors for " + b.Doors.Count + " room doors");
            var seenIdx = new HashSet<int>();
            foreach (DoorV2 d in room.Doors)
                if (d.DoorIndex < 0 || d.DoorIndex >= b.Doors.Count) Err("v2 door index " + d.DoorIndex + " is outside the room's " + b.Doors.Count + " doors");
                else if (!seenIdx.Add(d.DoorIndex)) Err("v2 door index " + d.DoorIndex + " appears twice");
            if (seenIdx.Count != b.Doors.Count) Err("not every door has a v2 door");
            if (errs.Count > 0) return errs;

            // 3. shape
            int W = b.Width, H = b.Height;
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                    if (room.LevelAt(x, y) < 0 || room.LevelAt(x, y) > GameMetrics.MaxValidatedLevel) Err("cell (" + x + "," + y + ") is on level " + room.LevelAt(x, y) + ", only 0 to " + GameMetrics.MaxValidatedLevel + " are validated");
            foreach (DoorV2 d in room.Doors)
            {
                CellDoor cd = b.Doors[d.DoorIndex]; string at = "door (" + cd.X + "," + cd.Y + " " + cd.Side + ")";
                if (room.LevelAt(cd.X, cd.Y) != 0 || d.Level != 0) Err(at + " is not on level 0");
                if (d.Type == DoorType.Gate)
                {
                    int gi = d.GateRegion == null ? -1 : room.RegionIndex(d.GateRegion);
                    if (gi < 0) Err(at + " is a gate for region '" + d.GateRegion + "', which this room does not have");
                    else if (d.GateOpenState < 0 || d.GateOpenState >= room.Regions[gi].StateCount) Err(at + " gate open state " + d.GateOpenState + " is outside region " + d.GateRegion + "'s states");
                }
                if (d.Type == DoorType.ToolSeal && d.Tool == ToolVerb.None) Err(at + " is a tool seal with no tool");
            }
            var regionIds = new HashSet<string>();
            if (room.Regions.Count > MaxRegions) Err("has " + room.Regions.Count + " gimmick regions, at most " + MaxRegions);
            foreach (GimmickRegion r in room.Regions)
            {
                string at = "region " + r.Id;
                if (string.IsNullOrEmpty(r.Id) || !regionIds.Add(r.Id)) Err(at + " has an empty or repeated id");
                if (r.StateCount < MinRegionStates || r.StateCount > MaxRegionStates) Err(at + " has " + r.StateCount + " states, needs " + MinRegionStates + " to " + MaxRegionStates);
                if (r.Cells.Count == 0) Err(at + " covers no cells");
                foreach (int v in r.StateValues)
                    if (r.Kind == GimmickKind.Heat ? (v != 0 && v != 1) : (v < 0 || v > GameMetrics.LevelHeightCm * (GameMetrics.MaxValidatedLevel + 1)))
                        Err(at + " state value " + v + " is out of range for " + r.Kind);
                foreach (CellPos c in r.Cells) if (b.CellAt(c.X, c.Y) != CellKind.Floor) Err(at + " cell " + c + " is on a " + b.CellAt(c.X, c.Y) + " cell");
            }

            // marks: every cell that carries something, for the stair / socket / escape rules
            var marks = new Dictionary<(int, int), string>();
            void AddMark(int x, int y, string what) { if (!marks.ContainsKey((x, y))) marks[(x, y)] = what; }
            foreach (CellDoor d in b.Doors) AddMark(d.X, d.Y, "door");
            foreach (CellarSpawn s in b.Spawns) AddMark(s.X, s.Y, "spawn");
            foreach (ChestSlot c in b.Chests) AddMark(c.X, c.Y, "chest");
            foreach (RoomFeature f in b.Features) AddMark(f.X, f.Y, f.Kind.ToString() + " feature");
            foreach (DressingSocket k in b.Sockets) for (int o = 0; o < k.CellOptions; o++) AddMark(k.OptionCell(o).X, k.OptionCell(o).Y, "dressing socket " + k.Id);
            foreach (ChunkRegion cr in b.Regions) for (int x = cr.X; x < cr.X + cr.Width; x++) for (int y = cr.Y; y < cr.Y + cr.Height; y++) AddMark(x, y, "chunk region " + cr.Id);
            var toolMarks = new Dictionary<(int, int), string>();
            foreach (ToolSocket t in room.Sockets) if (b.InBounds(t.X, t.Y) && !toolMarks.ContainsKey((t.X, t.Y))) toolMarks[(t.X, t.Y)] = "tool socket " + t.Id;

            // 4. stairs
            var stairCells = new HashSet<(int, int)>();
            foreach (StairCell s in room.Stairs)
            {
                string at = "stair (" + s.X + "," + s.Y + " up " + s.Up + ")";
                if (!b.InBounds(s.X, s.Y)) { Err(at + " is outside the grid"); continue; }
                if (!stairCells.Add((s.X, s.Y))) Err(at + " is listed twice");
                if (b.CellAt(s.X, s.Y) != CellKind.Floor) Err(at + " is on a " + b.CellAt(s.X, s.Y) + " cell");
                var (dx, dy) = CellarRoom.Step(s.Up); int ux = s.X + dx, uy = s.Y + dy;
                if (!b.InBounds(ux, uy) || b.CellAt(ux, uy) != CellKind.Floor) Err(at + " has no Floor cell on its up side");
                else if (room.LevelAt(ux, uy) != room.LevelAt(s.X, s.Y) + 1) Err(at + " does not lead to a cell exactly one level higher");
                if (marks.TryGetValue((s.X, s.Y), out string m)) Err(at + " sits on a " + m);
                if (toolMarks.TryGetValue((s.X, s.Y), out string tm)) Err(at + " sits on a " + tm);
            }

            // 5. tool sockets
            var socketIds = new HashSet<string>();
            foreach (ToolSocket t in room.Sockets)
            {
                string at = "tool socket " + t.Id + " (" + t.X + "," + t.Y + ")";
                if (string.IsNullOrEmpty(t.Id) || !socketIds.Add(t.Id)) Err(at + " has an empty or repeated id");
                if (!b.InBounds(t.X, t.Y)) { Err(at + " is outside the grid"); continue; }
                if (b.CellAt(t.X, t.Y) != CellKind.Floor) Err(at + " is on a " + b.CellAt(t.X, t.Y) + " cell");
                if (marks.TryGetValue((t.X, t.Y), out string m)) Err(at + " shares its cell with a " + m);
                if (stairCells.Contains((t.X, t.Y))) Err(at + " sits on a stair");
                if (toolMarks.TryGetValue((t.X, t.Y), out string other) && other != "tool socket " + t.Id) Err(at + " shares its cell with a " + other);
                if (t.Purpose == SocketPurpose.Control && (t.Region == null || room.RegionIndex(t.Region) < 0)) Err(at + " is a control for region '" + t.Region + "', which this room does not have");
                var (bx, by) = CellarRoom.Step(CellarRoom.Opposite(t.Facing)); bx += t.X; by += t.Y;
                bool wall = !b.InBounds(bx, by) || b.CellAt(bx, by) != CellKind.Floor;   // a door on that cell was already reported as a shared cell
                if (!wall) Err(at + " has no wall behind it");
            }

            // rules 6-9 index state values; stop here when a region's state count (or the region count) is outside what rule 3 allows
            if (room.Regions.Count > MaxRegions || room.Regions.Any(r => r.StateCount < MinRegionStates || r.StateCount > MaxRegionStates)) return errs;

            int states = StateCount(room);
            var vectors = new int[states][];
            for (int s = 0; s < states; s++) vectors[s] = Decode(room, s);

            // 6. marks never blocked
            var mustStand = new List<(int x, int y, string what)>();
            foreach (CellDoor d in b.Doors) mustStand.Add((d.X, d.Y, "door"));
            foreach (ChestSlot c in b.Chests) mustStand.Add((c.X, c.Y, "chest"));
            foreach (CellarSpawn s in b.Spawns) mustStand.Add((s.X, s.Y, "spawn"));
            foreach (RoomFeature f in b.Features) mustStand.Add((f.X, f.Y, f.Kind + " feature"));
            foreach (ToolSocket t in room.Sockets) if (b.InBounds(t.X, t.Y)) mustStand.Add((t.X, t.Y, "tool socket " + t.Id));
            foreach (var m in mustStand)
                for (int s = 0; s < states; s++)
                    if (!room.Standable(m.x, m.y, vectors[s])) { Err(m.what + " at (" + m.x + "," + m.y + ") is not standable in state " + Show(vectors[s])); break; }

            // 7. escape: a cell that stops being standable has a standable neighbour in that state
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
                {
                    if (b.CellAt(x, y) != CellKind.Floor || room.RegionIndexAt(x, y) < 0) continue;
                    for (int s = 0; s < states; s++)
                    {
                        if (room.Standable(x, y, vectors[s])) continue;
                        bool exit = false;
                        foreach (CellSide side in Sides)
                        {
                            var (dx, dy) = CellarRoom.Step(side); int nx = x + dx, ny = y + dy;
                            if (b.InBounds(nx, ny) && room.Standable(nx, ny, vectors[s])) { exit = true; break; }
                        }
                        if (!exit) { Err("cell (" + x + "," + y + ") has no standable neighbour in state " + Show(vectors[s]) + " to move a hero to"); break; }
                    }
                }

            // 8. each region matters: two of its states differ in what can be stood on
            for (int ri = 0; ri < room.Regions.Count; ri++)
            {
                GimmickRegion r = room.Regions[ri]; bool differs = false;
                for (int a = 0; a < r.StateCount && !differs; a++)
                    for (int c = a + 1; c < r.StateCount && !differs; c++)
                    {
                        var va = new int[room.Regions.Count]; var vc = new int[room.Regions.Count]; va[ri] = a; vc[ri] = c;
                        foreach (CellPos p in r.Cells) if (room.Standable(p.X, p.Y, va) != room.Standable(p.X, p.Y, vc)) { differs = true; break; }
                    }
                if (!differs) Err("region " + r.Id + " never changes what can be walked on (no two of its states differ)");
            }

            // 9. no softlock
            if (errs.Count == 0) Softlock(room, partyTools, vectors, Err);
            return errs;
        }

        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        static string Show(int[] state) => "[" + string.Join(",", state) + "]";

        static int[] Decode(RoomDataV2 room, int key)
        {
            var v = new int[room.Regions.Count];
            for (int i = 0; i < v.Length; i++) { int n = room.Regions[i].StateCount; v[i] = key % n; key /= n; }
            return v;
        }

        static int Encode(RoomDataV2 room, int[] v)
        {
            int key = 0, mult = 1;
            for (int i = 0; i < v.Length; i++) { key += v[i] * mult; mult *= room.Regions[i].StateCount; }
            return key;
        }

        sealed class Target { public int Cell; public int Region = -1, OpenState; public string Text; }

        static void Softlock(RoomDataV2 room, IReadOnlyCollection<ToolVerb> partyTools, int[][] vectors, Action<string> err)
        {
            CellarRoom b = room.Base; int W = b.Width, H = b.Height, WH = W * H, S = vectors.Length, N = S * WH;
            var stand = new bool[S][];
            for (int s = 0; s < S; s++) { stand[s] = new bool[WH]; for (int c = 0; c < WH; c++) stand[s][c] = room.Standable(c % W, c / W, vectors[s]); }

            // barrels and crates stand in the way (every candidate cell counts, as in v1)
            var blocked = new bool[WH];
            foreach (DressingSocket k in b.Sockets)
                if (k.Kind == SocketKind.Barrel || k.Kind == SocketKind.Crate)
                    for (int o = 0; o < k.CellOptions; o++) { CellPos p = k.OptionCell(o); if (b.InBounds(p.X, p.Y)) blocked[p.Y * W + p.X] = true; }

            var up = new int[WH]; for (int c = 0; c < WH; c++) up[c] = -1;
            foreach (StairCell s in room.Stairs) up[s.Y * W + s.X] = (int)s.Up;
            var control = new ToolSocket[WH];
            foreach (ToolSocket t in room.Sockets)
                if (t.Purpose == SocketPurpose.Control && (t.Tool == ToolVerb.None || (partyTools != null && partyTools.Contains(t.Tool)))) control[t.Y * W + t.X] = t;
            var cycled = new int[S][];   // cycled[s][region] = the state key after that region's Control
            for (int s = 0; s < S; s++)
            {
                cycled[s] = new int[room.Regions.Count];
                for (int r = 0; r < room.Regions.Count; r++) { var v = (int[])vectors[s].Clone(); v[r] = (v[r] + 1) % room.Regions[r].StateCount; cycled[s][r] = Encode(room, v); }
            }

            void Edges(int node, List<int> outList)
            {
                outList.Clear(); int s = node / WH, c = node % WH, x = c % W, y = c / W;
                for (int i = 0; i < 4; i++)
                {
                    var (dx, dy) = CellarRoom.Step(Sides[i]); int nx = x + dx, ny = y + dy;
                    if (!b.InBounds(nx, ny)) continue;
                    int nc = ny * W + nx;
                    if (blocked[nc] || !stand[s][nc]) continue;
                    int la = room.LevelAt(x, y), lb = room.LevelAt(nx, ny);
                    if (lb <= la || (lb == la + 1 && up[c] == i)) outList.Add(s * WH + nc);
                }
                ToolSocket t = control[c];
                if (t != null)
                {
                    int ns = cycled[s][room.RegionIndex(t.Region)];
                    if (stand[ns][c]) outList.Add(ns * WH + c);
                }
            }

            // targets: every door, chest, feature and Control cell (a gate door only counts in its open state)
            var targets = new List<Target>();
            for (int i = 0; i < b.Doors.Count; i++)
            {
                CellDoor d = b.Doors[i]; DoorV2 v2 = room.Doors.First(q => q.DoorIndex == i);
                var tg = new Target { Cell = d.Y * W + d.X, Text = "door (" + d.X + "," + d.Y + " " + d.Side + ")" };
                if (v2.Type == DoorType.Gate) { tg.Region = room.RegionIndex(v2.GateRegion); tg.OpenState = v2.GateOpenState; tg.Text = "gate " + tg.Text + " in its open state"; }
                targets.Add(tg);
            }
            foreach (ChestSlot c in b.Chests) targets.Add(new Target { Cell = c.Y * W + c.X, Text = "chest (" + c.X + "," + c.Y + ")" });
            foreach (RoomFeature f in b.Features) targets.Add(new Target { Cell = f.Y * W + f.X, Text = f.Kind + " feature (" + f.X + "," + f.Y + ")" });
            foreach (ToolSocket t in room.Sockets) if (t.Purpose == SocketPurpose.Control) targets.Add(new Target { Cell = t.Y * W + t.X, Text = "control " + t.Id + " (" + t.X + "," + t.Y + ")" });
            var byCell = new List<int>[WH];
            for (int i = 0; i < targets.Count; i++) { (byCell[targets[i].Cell] ?? (byCell[targets[i].Cell] = new List<int>())).Add(i); }

            // reachable set from every door cell in every state where it is standable
            var reach = new bool[N]; var order = new List<int>(); var queue = new Queue<int>(); var buf = new List<int>();
            for (int s = 0; s < S; s++)
                foreach (CellDoor d in b.Doors)
                {
                    int c = d.Y * W + d.X, n = s * WH + c;
                    if (stand[s][c] && !blocked[c] && !reach[n]) { reach[n] = true; queue.Enqueue(n); order.Add(n); }
                }
            while (queue.Count > 0)
            {
                int n = queue.Dequeue(); Edges(n, buf);
                foreach (int m in buf) if (!reach[m]) { reach[m] = true; queue.Enqueue(m); order.Add(m); }
            }

            // from every reachable node, every target must be reachable (a node that reaches a known-good node is good)
            var good = new bool[N]; var seen = new int[N]; int stamp = 0; var done = new bool[targets.Count];
            foreach (int start in order)
            {
                stamp++; Array.Clear(done, 0, done.Length); int left = targets.Count; bool ok = left == 0;
                queue.Clear(); seen[start] = stamp; queue.Enqueue(start);
                while (queue.Count > 0 && !ok)
                {
                    int n = queue.Dequeue();
                    if (good[n]) { ok = true; break; }
                    int s = n / WH, c = n % WH;
                    if (byCell[c] != null)
                        foreach (int ti in byCell[c])
                        {
                            Target tg = targets[ti];
                            if (!done[ti] && (tg.Region < 0 || vectors[s][tg.Region] == tg.OpenState)) { done[ti] = true; left--; }
                        }
                    if (left == 0) { ok = true; break; }
                    Edges(n, buf);
                    foreach (int m in buf) if (seen[m] != stamp) { seen[m] = stamp; queue.Enqueue(m); }
                }
                if (ok) { good[start] = true; continue; }
                int miss = Array.IndexOf(done, false);
                int ss = start / WH, cc = start % WH;
                err("softlock: from (" + (cc % W) + "," + (cc / W) + ") in state " + Show(vectors[ss]) + " the " + targets[miss].Text + " cannot be reached");
                return;
            }
        }
    }
}
