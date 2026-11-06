// Core/Cellars/FloorObjective.cs: what a floor of the 7-floor dive asks of the party (plan 5 task 1; design note 2026-11-04-floor-card-and-modifiers). Pure C#, no engine references.
//   - FloorObjectivePick.Rolled(seed, floor): floor 1 gate key (taught), 3 rescue, 5 guardian; the generated floors (2, 4, 6, 7) draw from the pool GateKey / TwoValves / HoldPump on the
//     new stream "cellars.objective", never the previous floor's objective; floor 7 has no stair or carrier flow, so its pool is the two valve/pump objectives only.
//   - FloorObjectivePlan.For(layout): the pick plus where the valves / the pump sit, a pure function of the layout. Valve and pump rooms are Combat rooms (CellarEncounterPlan.FreeCellsOf)
//     reachable from the entry without the locked link; cell ties draw from "cellars.objective.cells". A layout that cannot host the pick falls back (floors 2/4/6 to GateKey, floor 7 to Ending).
// Every number is a placeholder. No existing Rng stream is drawn from, so the layout, dressing, atmosphere and loot goldens never move.
// Authority: none (static data derived from the dive seed and floor; every peer computes the same answer).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum FloorObjective : byte { None = 0, GateKey = 1, TwoValves = 2, HoldPump = 3, Rescue = 4, Guardian = 5, Ending = 6 }

    // PLACEHOLDER tuning in ONE spot (decisions 7 and 8 of plan 5).
    public static class FloorObjectiveTuning
    {
        public const float ValveHoldSeconds = 2f, ValveReachMeters = 1.8f, ReportInterval = 0.2f, ReportTimeout = 0.6f;   // placeholder
        public const float PumpSeconds = 75f, PumpRingMeters = 4f, PumpStartReach = 2.5f, PumpSlowRadius = 2.5f, PumpSlowFactor = 0.5f;   // placeholder
        public const float WaveStagger = 1.5f, SoloStaggerFactor = 1.25f;   // placeholder
        public const int WaveBase = 2, WaveCount = 3, MaxWavesAlive = 2;   // placeholder
        public static readonly float[] WaveThresholds = { 0f, 0.35f, 0.70f };   // placeholder
    }

    public static class FloorObjectivePick
    {
        // The pick before any layout fallback.
        public static FloorObjective Rolled(uint seed, int floor)
        {
            if (floor == 1) return FloorObjective.GateKey;
            if (floor == 3) return FloorObjective.Rescue;
            if (floor == UndercroftPlan.BankFloor) return FloorObjective.Guardian;
            if (!CellarFloorGenerator.IsGeneratedFloor(floor)) return FloorObjective.None;
            FloorObjective prev = Rolled(seed, floor - 1);   // recursion ends at floor 1
            FloorObjective[] pool = floor == DiveFloorTable.LastBuiltFloor
                ? new[] { FloorObjective.TwoValves, FloorObjective.HoldPump }
                : new[] { FloorObjective.GateKey, FloorObjective.TwoValves, FloorObjective.HoldPump };
            var rng = new Pcg32(Rng.Derive(seed, "cellars.objective", (ulong)floor));
            int n = 0; var pick = new FloorObjective[pool.Length];
            foreach (FloorObjective o in pool) if (o != prev) pick[n++] = o;
            return pick[rng.Range(0, n)];
        }
    }

    public sealed class FloorObjectivePlan
    {
        public readonly FloorObjective Objective;
        public readonly int ValveRoomA, ValveRoomB, PumpRoom;   // -1 when unused
        public readonly CellPos ValveCellA, ValveCellB, PumpCell;

        FloorObjectivePlan(FloorObjective o, int va, int vb, int pr, CellPos ca, CellPos cb, CellPos cp)
        { Objective = o; ValveRoomA = va; ValveRoomB = vb; PumpRoom = pr; ValveCellA = ca; ValveCellB = cb; PumpCell = cp; }

        public static FloorObjectivePlan For(CellarFloorLayout layout) => For(layout, null);

        public static FloorObjectivePlan For(CellarFloorLayout layout, CellarFloorDressing dressing)
        {
            if (layout == null) throw new System.ArgumentNullException(nameof(layout));
            FloorObjective rolled = FloorObjectivePick.Rolled(layout.Seed, layout.Floor);
            if (rolled != FloorObjective.TwoValves && rolled != FloorObjective.HoldPump) return Simple(rolled);
            if (dressing == null) dressing = CellarFloorDressing.Resolve(layout);

            int n = layout.Rooms.Count;
            int entry = layout.EntryStarts.Count > 0 ? layout.RoomAt(layout.EntryStarts[0].X, layout.EntryStarts[0].Y) : -1;
            int exit = layout.ExitRoomIndex();
            FloorObjectivePlan fallback = Simple(layout.Floor == DiveFloorTable.LastBuiltFloor ? FloorObjective.Ending : FloorObjective.GateKey);
            if (entry < 0 || exit < 0) return fallback;

            // room graph over the links, the locked link left out
            var adj = new List<int>[n]; for (int i = 0; i < n; i++) adj[i] = new List<int>();
            for (int i = 0; i < layout.Links.Count; i++)
            {
                if (i == layout.LockedLinkIndex) continue;
                FloorLink k = layout.Links[i];
                if (k.RoomA < 0 || k.RoomB < 0) continue;   // a side dead end joins no second room
                adj[k.RoomA].Add(k.RoomB); adj[k.RoomB].Add(k.RoomA);
            }
            var dist = new int[n]; var parent = new int[n];
            for (int i = 0; i < n; i++) { dist[i] = -1; parent[i] = -1; }
            dist[entry] = 0; var q = new Queue<int>(); q.Enqueue(entry);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                foreach (int o in adj[r]) if (dist[o] < 0) { dist[o] = dist[r] + 1; parent[o] = r; q.Enqueue(o); }
            }

            var rng = new Pcg32(Rng.Derive(layout.Seed, "cellars.objective.cells", (ulong)layout.Floor));
            var freeCells = new IReadOnlyList<CellPos>[n];
            var eligible = new List<int>();
            for (int r = 0; r < n; r++)
            {
                if (r == entry || r == exit || r == layout.LockedRoomIndex || dist[r] < 0) continue;
                IReadOnlyList<CellPos> cells = CellarEncounterPlan.FreeCellsOf(layout, dressing, r);   // Combat rooms only; empty otherwise
                if (cells.Count == 0) continue;
                freeCells[r] = cells; eligible.Add(r);
            }

            if (rolled == FloorObjective.TwoValves)
            {
                // B: farthest from the entry, a dead end (one link) winning a distance tie; A: nearest the entry, not B and sharing no link with B.
                // A non-adjacent pair is preferred (only a room with a non-adjacent partner can be B); when no such pair exists, two distinct adjacent eligible rooms do (B still the farthest, A the nearest other).
                if (eligible.Count < 2) return fallback;
                bool anyPair = false; foreach (int r in eligible) if (HasPartner(r)) anyPair = true;
                bool HasPartner(int r) { foreach (int o in eligible) if (o != r && !adj[r].Contains(o)) return true; return false; }
                int bestKey = -1; var bestB = new List<int>();
                foreach (int r in eligible)
                {
                    if (anyPair && !HasPartner(r)) continue;
                    int key = dist[r] * 2 + (adj[r].Count == 1 ? 1 : 0);
                    if (key > bestKey) { bestKey = key; bestB.Clear(); }
                    if (key == bestKey) bestB.Add(r);
                }
                if (bestB.Count == 0) return fallback;
                int b = bestB[bestB.Count == 1 ? 0 : rng.Range(0, bestB.Count)];
                int bestDist = int.MaxValue; var nearest = new List<int>();
                foreach (int r in eligible)
                {
                    if (r == b || (anyPair && adj[b].Contains(r))) continue;
                    if (dist[r] < bestDist) { bestDist = dist[r]; nearest.Clear(); }
                    if (dist[r] == bestDist) nearest.Add(r);
                }
                if (nearest.Count == 0) return fallback;
                int a = nearest[nearest.Count == 1 ? 0 : rng.Range(0, nearest.Count)];
                CellPos ca = CentreMost(layout, a, freeCells[a], rng), cb = CentreMost(layout, b, freeCells[b], rng);
                return new FloorObjectivePlan(FloorObjective.TwoValves, a, b, -1, ca, cb, default);
            }

            // pump: on or beside the entry-to-exit room path, its distance from the entry in the middle third, nearest the middle
            int len = dist[exit];
            if (len < 0) return fallback;
            var onPath = new bool[n];
            for (int r = exit; r >= 0; r = parent[r]) onPath[r] = true;
            int bestScore = int.MaxValue; var picks = new List<int>();
            foreach (int r in eligible)
            {
                bool beside = onPath[r];
                if (!beside) foreach (int o in adj[r]) if (onPath[o]) beside = true;
                if (!beside || dist[r] * 3 < len || dist[r] * 3 > 2 * len) continue;
                int off = dist[r] * 2 - len; if (off < 0) off = -off;
                int score = off * 2 + (onPath[r] ? 0 : 1);   // closer to len/2 first, then on the path before beside it
                if (score < bestScore) { bestScore = score; picks.Clear(); }
                if (score == bestScore) picks.Add(r);
            }
            if (picks.Count == 0) return fallback;
            int pump = picks[picks.Count == 1 ? 0 : rng.Range(0, picks.Count)];
            return new FloorObjectivePlan(FloorObjective.HoldPump, -1, -1, pump, default, default, CentreMost(layout, pump, freeCells[pump], rng));
        }

        static FloorObjectivePlan Simple(FloorObjective o) => new FloorObjectivePlan(o, -1, -1, -1, default, default, default);

        // The free cell nearest the room centre (in half-cell units, so an even-sized room has a true centre); ties on the cells stream.
        static CellPos CentreMost(CellarFloorLayout layout, int room, IReadOnlyList<CellPos> cells, Pcg32 rng)
        {
            PlacedRoom p = layout.Rooms[room];
            int best = int.MaxValue; var ties = new List<CellPos>();
            foreach (CellPos c in cells)
            {
                int dx = 2 * (c.X - p.X) - (p.Width - 1), dy = 2 * (c.Y - p.Y) - (p.Height - 1), d = dx * dx + dy * dy;
                if (d < best) { best = d; ties.Clear(); }
                if (d == best) ties.Add(c);
            }
            return ties[ties.Count == 1 ? 0 : rng.Range(0, ties.Count)];
        }

        public ulong Hash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE((int)Objective); h.AppendInt32LE(ValveRoomA); h.AppendInt32LE(ValveRoomB); h.AppendInt32LE(PumpRoom);
            foreach (CellPos c in new[] { ValveCellA, ValveCellB, PumpCell }) { h.AppendInt32LE(c.X); h.AppendInt32LE(c.Y); }
            return h.Value;
        }
    }
}
