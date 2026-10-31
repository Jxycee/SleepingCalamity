// Core/Town/TownWallPlan.cs: the module plan for Hollowmere's town wall (Q8 art: Art/Town/Wall kit, 4 m modules). Pure C#, no engine references, so the Editor
// builder (HollowmereTownWall) and the EditMode tests share one deterministic plan.
//
// Input: the pieces of HollowmereLayout.WallRun (short chords with gaps where lanes, lots and landmarks cut the line). Consecutive pieces that touch form one
// contiguous run; every run is tiled with 4 m modules (stretched at most +-10% to fit; when no whole count fits, the tiled part is centred and the wall
// stops short of the cut by the remainder). Every run with modules starts and ends on a Wall_End (a jamb at each opening).
// Frame of a module, as the art contract has it: origin at the centre of the module on the wall's centre line, local X along the wall, local +Z to the
// OUTSIDE of the town (Blender -Y), yaw in degrees (Unity convention). Fittings (Buttress outside, Stair inside) share their module's frame.
// Variants are fixed by Seed and the module's index, not by the preset: Ruined and Rebuilt differ only by the mapping (breach -> patched, crumbled -> straight).
// Authority: none (static plan data used at editor build time).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum WallPiece : byte { StraightA, StraightB, Breach, Crumbled, Patched, Buttress, End, Stair }

    public sealed class WallSlot
    {
        public WallPiece Piece;
        public int Run;               // index of the contiguous run
        public int Index;             // module index along the whole line (fittings carry their module's index; Ends carry -1)
        public float Start, Length;   // arc length along the run where the module starts, and its stretched length (Length = ModuleLength * Stretch)
        public float Stretch = 1f;
        public TownPoint Centre;
        public float Yaw;             // module frame: local +Z outward
        public TownPoint Outward;     // unit vector, to the outside
        public TownPoint Close;       // Ends only: unit vector from the jamb out into the opening
        public TownPoint Chord;       // modules: unit chord direction (module-local -X in the ground plane)
        public float Reach;           // modules: half the chord length (for height sampling along the centre line)
        public bool Mirror;           // Stairs: scale X -1
    }

    public static class TownWallPlan
    {
        public const float ModuleLength = 4f, MaxStretch = 0.10f, EndLength = 2f;   // Wall_End is 2.2 m of wall body behind its cut face
        public const int Seed = 8081;
        public const float BreachShare = 0.08f, CrumbledShare = 0.15f;
        public const float StairMin = 40f, StairMax = 60f;
        public const float DownhillDrop = 0.35f;   // outside ground at least this much lower than the inside, over 3 m, counts as downhill
        public const float LevelInside = 0.30f;    // inside ground may vary this much along the stair's strip

        public static bool IsRuinVariant(WallPiece p) => p == WallPiece.Breach || p == WallPiece.Crumbled;
        public static bool IsModule(WallPiece p) => p != WallPiece.Buttress && p != WallPiece.End && p != WallPiece.Stair;

        // Splits WallRun pieces into polylines of touching pieces.
        public static List<List<TownPoint>> Runs(IList<(TownPoint A, TownPoint B)> pieces)
        {
            var runs = new List<List<TownPoint>>();
            List<TownPoint> cur = null;
            foreach (var (a, b) in pieces)
            {
                if (cur == null || TownPoint.Distance(cur[cur.Count - 1], a) > 0.01f) { cur = new List<TownPoint> { a }; runs.Add(cur); }
                cur.Add(b);
            }
            return runs;
        }

        public static float Length(List<TownPoint> pts)
        {
            float l = 0f;
            for (int i = 1; i < pts.Count; i++) l += TownPoint.Distance(pts[i - 1], pts[i]);
            return l;
        }

        public static TownPoint PointAt(List<TownPoint> pts, float s)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = TownPoint.Distance(pts[i - 1], pts[i]);
                if (s <= seg || i == pts.Count - 1) return pts[i - 1] + (pts[i] - pts[i - 1]) * (seg > 0f ? Math.Min(1f, s / seg) : 0f);
                s -= seg;
            }
            return pts[0];
        }

        // How a run of the given length is tiled: module count, per-module stretch, and the length left uncovered (split over both ends).
        public static void Fit(float length, out int count, out float stretch, out float remainder)
        {
            float lo = 1f - MaxStretch, hi = 1f + MaxStretch;
            int floor = (int)Math.Floor(length / ModuleLength), ceil = floor + 1;
            if (ceil >= 1 && length / (ceil * ModuleLength) >= lo && length / (ceil * ModuleLength) <= hi) { count = ceil; stretch = length / (ceil * ModuleLength); remainder = 0f; return; }
            if (floor >= 1 && length / (floor * ModuleLength) <= hi && length / (floor * ModuleLength) >= lo) { count = floor; stretch = length / (floor * ModuleLength); remainder = 0f; return; }
            count = floor; stretch = floor >= 1 ? hi : 1f; remainder = floor >= 1 ? length - floor * ModuleLength * hi : length;
            if (count < 1) { count = 0; stretch = 1f; remainder = length; }
        }

        // A gatehouse block the wall runs into: centre, unit axis along the pier line, half width of the block (pier outer face to centre).
        public readonly struct WallGate
        {
            public readonly string Name; public readonly TownPoint Centre, Axis; public readonly float HalfWidth;
            public WallGate(string name, TownPoint centre, TownPoint axis, float halfWidth) { Name = name; Centre = centre; Axis = axis; HalfWidth = halfWidth; }
        }

        // The gatehouses: name, the two pier landmark ids, half width of the block (SOURCES: LowerGate 9.0 m, EastGate 8.84 m wide).
        public static readonly (string Name, string PierA, string PierB, float HalfWidth)[] GateDefs =
        {
            ("LowerGate", "LowerGate_W", "LowerGate_E", 4.5f), ("EastGate", "EastGate_N", "EastGate_S", 4.42f),
        };

        public static List<WallGate> Gates(TownLayout t)
        {
            var list = new List<WallGate>();
            foreach (var d in GateDefs)
            {
                Landmark a = t.Landmarks.Find(l => l.Id == d.PierA), b = t.Landmarks.Find(l => l.Id == d.PierB);
                if (a == null || b == null) continue;
                TownPoint pa = a.Footprint.Centre, pb = b.Footprint.Centre, dir = pb - pa;
                list.Add(new WallGate(d.Name, (pa + pb) * 0.5f, dir * (1f / dir.Length), d.HalfWidth));
            }
            return list;
        }

        // Brings the end of a run (index 0 or last) to a gatehouse's side face when it stops within reach of one: extended along its own direction when it
        // stops short, trimmed back along the run when it starts inside the block (the wall line may pass through the passage). True when it met a gate.
        static bool ExtendToGate(List<TownPoint> pts, bool atStart, IList<WallGate> gates)
        {
            if (gates == null) return false;
            int i = atStart ? 0 : pts.Count - 1, j = atStart ? 1 : pts.Count - 2;
            TownPoint p = pts[i], u = p - pts[j]; float ul = u.Length; if (ul < 1e-4f) return false;
            u = u * (1f / ul);
            foreach (WallGate g in gates)
            {
                TownPoint d = p - g.Centre;
                float xl = d.X * g.Axis.X + d.Z * g.Axis.Z, zl = -d.X * g.Axis.Z + d.Z * g.Axis.X, dxl = u.X * g.Axis.X + u.Z * g.Axis.Z;
                if (Math.Abs(xl) < g.HalfWidth && Math.Abs(zl) < 3f)
                {
                    // Inside the block: walk into the run until it leaves the block's side.
                    float total = Length(pts);
                    for (float s = 0.1f; s < total; s += 0.1f)
                    {
                        TownPoint q = PointAt(pts, atStart ? s : total - s), e = q - g.Centre;
                        if (Math.Abs(e.X * g.Axis.X + e.Z * g.Axis.Z) < g.HalfWidth) continue;
                        var kept = new List<TownPoint>();
                        float acc = 0f;
                        if (atStart) { kept.Add(q); for (int k = 1; k < pts.Count; k++) { acc += TownPoint.Distance(pts[k - 1], pts[k]); if (acc > s + 1e-3f) kept.Add(pts[k]); } }
                        else { for (int k = 1; k < pts.Count; k++) { if (acc + TownPoint.Distance(pts[k - 1], pts[k]) < total - s - 1e-3f) { if (kept.Count == 0) kept.Add(pts[0]); kept.Add(pts[k]); } acc += TownPoint.Distance(pts[k - 1], pts[k]); } if (kept.Count == 0) kept.Add(pts[0]); kept.Add(q); }
                        if (kept.Count < 2) return false;
                        pts.Clear(); pts.AddRange(kept);
                        return true;
                    }
                    return false;
                }
                if (TownPoint.Distance(p, g.Centre) > g.HalfWidth + 6f || Math.Abs(dxl) < 0.2f) continue;
                float target = xl > 0f ? g.HalfWidth : -g.HalfWidth, tt = (target - xl) / dxl;
                if (tt < -2f || tt > 6f) continue;
                pts[i] = p + u * Math.Max(0f, tt);
                return true;
            }
            return false;
        }

        // The plan for one wall line. ground(x, z) is the town's ground height (HollowmereLayout.Ground in the builder). The module sequence and fitting
        // choices do not depend on `rebuilt` beyond the variant mapping. Each run starts and ends on a Wall_End (EndLength each, pivot on the cut face) unless
        // that end meets a gatehouse (`gates`): the run is then extended to the gate's side face, no End, and a Stair goes on the module beside the gate.
        public static List<WallSlot> Plan(IList<(TownPoint A, TownPoint B)> pieces, Func<float, float, float> ground, bool rebuilt, IList<WallGate> gates = null, int seed = Seed)
        {
            List<List<TownPoint>> runs = Runs(pieces);
            var modules = new List<WallSlot>();
            var ends = new List<WallSlot>();
            var gateStairs = new Dictionary<int, bool>();   // module index -> mirror
            for (int r = 0; r < runs.Count; r++)
            {
                List<TownPoint> pts = new List<TownPoint>(runs[r]);
                bool gateStart = ExtendToGate(pts, true, gates), gateEnd = ExtendToGate(pts, false, gates);
                float len = Length(pts), lead = gateStart ? 0f : EndLength, tail = gateEnd ? 0f : EndLength;
                Fit(len - lead - tail, out int n, out float stretch, out float rem);
                if (n < 1) continue;
                float step = ModuleLength * stretch, s0 = lead + rem * 0.5f;
                int firstIdx = modules.Count;
                for (int i = 0; i < n; i++)
                {
                    float a = s0 + i * step, b = a + step;
                    TownPoint pa = PointAt(pts, a), pb = PointAt(pts, b), c = pb - pa;
                    float cl = c.Length; if (cl < 1e-4f) continue;
                    var nrm = new TownPoint(c.Z / cl, -c.X / cl);
                    modules.Add(new WallSlot
                    {
                        Piece = WallPiece.StraightA, Run = r, Index = modules.Count, Start = a, Length = step, Stretch = stretch, Chord = c * (1f / cl),
                        Centre = (pa + pb) * 0.5f, Outward = nrm, Yaw = (float)(Math.Atan2(nrm.X, nrm.Z) * 180.0 / Math.PI), Reach = Math.Min(cl, step) * 0.5f,
                    });
                }
                WallSlot last = modules.FindLast(m => m.Run == r), start = modules.Find(m => m.Run == r);
                if (last == null) continue;
                // Direction of travel at a module is its chord direction; module-local +X is its negative (see Outward).
                TownPoint alongStart = new TownPoint(-start.Outward.Z, start.Outward.X), alongEnd = new TownPoint(-last.Outward.Z, last.Outward.X);
                if (!gateStart) ends.Add(new WallSlot { Piece = WallPiece.End, Run = r, Index = -1, Start = 0f, Length = 0f, Centre = pts[0], Outward = start.Outward, Yaw = start.Yaw, Close = alongStart * -1f });
                else gateStairs[start.Index] = false;   // local +X (= -along) points at the gate at the start of a run
                if (!gateEnd) ends.Add(new WallSlot { Piece = WallPiece.End, Run = r, Index = -1, Start = len, Length = 0f, Centre = pts[pts.Count - 1], Outward = last.Outward, Yaw = last.Yaw, Close = alongEnd });
                else gateStairs[last.Index] = true;     // at the end of a run the gate is toward -local X: mirror
            }

            AssignVariants(modules, seed, rebuilt);

            var result = new List<WallSlot>(modules);
            // Buttresses: every 3-4 modules on straight modules where the ground falls away outside, counted per run.
            int sinceButtress = 0, interval = 3;
            int lastRun = -1;
            var stairAt = new HashSet<int>();
            float travelled = 0f, nextStair = StairMin + (StairMax - StairMin) * Hash(seed, 7001, 0);
            int stairCount = 0;
            foreach (WallSlot m in modules)
            {
                if (m.Run != lastRun) { lastRun = m.Run; sinceButtress = 0; interval = 3 + (int)(Hash(seed, 7002, m.Run) * 2f); }
                sinceButtress++;
                travelled += m.Length;
                bool straight = m.Piece == WallPiece.StraightA || m.Piece == WallPiece.StraightB;
                if (gateStairs.TryGetValue(m.Index, out bool gateMirror))
                {
                    WallSlot st = Fitting(m, WallPiece.Stair); st.Mirror = gateMirror;
                    result.Add(st); stairAt.Add(m.Index);
                }
                else if (straight && travelled >= nextStair && InsideLevel(m, ground))
                {
                    WallSlot st = Fitting(m, WallPiece.Stair); st.Mirror = Hash(seed, 7005, m.Index) < 0.5f;
                    result.Add(st);
                    stairAt.Add(m.Index); stairCount++;
                    nextStair = travelled + StairMin + (StairMax - StairMin) * Hash(seed, 7003, stairCount);
                }
                if (straight && sinceButtress >= interval && !stairAt.Contains(m.Index) && Downhill(m, ground))
                {
                    result.Add(Fitting(m, WallPiece.Buttress));
                    sinceButtress = 0; interval = 3 + (int)(Hash(seed, 7004, m.Index) * 2f);
                }
            }
            result.AddRange(ends);
            return result;
        }

        static WallSlot Fitting(WallSlot m, WallPiece piece) => new WallSlot
        {
            Piece = piece, Run = m.Run, Index = m.Index, Start = m.Start, Length = m.Length, Stretch = m.Stretch, Centre = m.Centre, Outward = m.Outward, Yaw = m.Yaw, Chord = m.Chord, Reach = m.Reach,
        };

        static float Ground(Func<float, float, float> g, WallSlot m, float side) => g(m.Centre.X + m.Outward.X * side, m.Centre.Z + m.Outward.Z * side);

        static bool Downhill(WallSlot m, Func<float, float, float> g) => Ground(g, m, -3f) - Ground(g, m, 3f) >= DownhillDrop;

        // The inside strip the flight stands on (0.6 to 1.9 m behind the centre line) is level enough.
        static bool InsideLevel(WallSlot m, Func<float, float, float> g)
        {
            float a = Ground(g, m, -0.6f), b = Ground(g, m, -1.25f), c = Ground(g, m, -1.9f);
            float lo = Math.Min(a, Math.Min(b, c)), hi = Math.Max(a, Math.Max(b, c));
            return hi - lo <= LevelInside;
        }

        // Breach ~8% and crumbled ~15% of all modules, stratified along the line so they spread out, never first or last of a run, never touching each other;
        // the rest alternate A/B by hash (never three alike in a row). Rebuilt: breach -> Patched, crumbled -> Straight.
        static void AssignVariants(List<WallSlot> modules, int seed, bool rebuilt)
        {
            int total = modules.Count;
            var cand = new List<int>();
            for (int i = 0; i < total; i++)
                if (i > 0 && i < total - 1 && modules[i - 1].Run == modules[i].Run && modules[i + 1].Run == modules[i].Run) cand.Add(i);
            var kind = new WallPiece[total];
            var taken = new bool[total];
            Pick(cand, taken, kind, (int)Math.Round(total * BreachShare), WallPiece.Breach, seed, 100);
            Pick(cand, taken, kind, (int)Math.Round(total * CrumbledShare), WallPiece.Crumbled, seed, 200);
            WallPiece prev = WallPiece.StraightB, prev2 = WallPiece.Patched;
            for (int i = 0; i < total; i++)
            {
                WallPiece p = kind[i];
                if (!taken[i])
                {
                    p = Hash(seed, 300, i) < 0.5f ? WallPiece.StraightA : WallPiece.StraightB;
                    if (p == prev && p == prev2) p = p == WallPiece.StraightA ? WallPiece.StraightB : WallPiece.StraightA;
                }
                prev2 = prev; prev = p;
                if (rebuilt) { if (p == WallPiece.Breach) p = WallPiece.Patched; else if (p == WallPiece.Crumbled) p = Hash(seed, 400, i) < 0.5f ? WallPiece.StraightA : WallPiece.StraightB; }
                modules[i].Piece = p;
            }
        }

        static void Pick(List<int> cand, bool[] taken, WallPiece[] kind, int want, WallPiece piece, int seed, int salt)
        {
            int c = cand.Count;
            if (c == 0 || want <= 0) return;
            for (int k = 0; k < want; k++)
            {
                float lo = (float)k * c / want, hi = (float)(k + 1) * c / want;
                int pos = (int)(lo + (0.25f + 0.5f * Hash(seed, salt, k)) * (hi - lo));
                for (int tries = 0; tries < c; tries++)
                {
                    int idx = cand[(pos + tries) % c];
                    bool clash = taken[idx] || (idx > 0 && taken[idx - 1]) || (idx + 1 < taken.Length && taken[idx + 1]);
                    if (clash) continue;
                    taken[idx] = true; kind[idx] = piece; break;
                }
            }
        }

        // A stable 0..1 number from the seed and two ints.
        public static float Hash(int seed, int a, int b)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u; h = (h ^ (uint)a) * 16777619u; h = (h ^ (uint)b) * 16777619u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // A gatehouse between its two piers: centred between them, passage axis perpendicular to the pier line, local +Z toward the outside of the town
        // (the side with the larger distance from the plateau), yaw in degrees.
        public static void GatePlacement(TownPoint pierA, TownPoint pierB, out TownPoint centre, out float yaw, out TownPoint outward)
        {
            centre = (pierA + pierB) * 0.5f;
            TownPoint d = pierB - pierA; float l = d.Length;
            var n = new TownPoint(d.Z / l, -d.X / l);
            TownPoint plus = centre + n * 3f, minus = centre - n * 3f;
            outward = HollowmereLayout.PlateauDistance(plus.X, plus.Z) >= HollowmereLayout.PlateauDistance(minus.X, minus.Z) ? n : n * -1f;
            yaw = (float)(Math.Atan2(outward.X, outward.Z) * 180.0 / Math.PI);
        }
    }
}
