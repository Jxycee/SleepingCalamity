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
    }

    public static class TownWallPlan
    {
        public const float ModuleLength = 4f, MaxStretch = 0.10f;
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

        // The plan for one wall line. ground(x, z) is the town's ground height (HollowmereLayout.Ground in the builder). The module sequence and fitting
        // choices do not depend on `rebuilt` beyond the variant mapping.
        public static List<WallSlot> Plan(IList<(TownPoint A, TownPoint B)> pieces, Func<float, float, float> ground, bool rebuilt, int seed = Seed)
        {
            List<List<TownPoint>> runs = Runs(pieces);
            var modules = new List<WallSlot>();
            var ends = new List<WallSlot>();
            for (int r = 0; r < runs.Count; r++)
            {
                List<TownPoint> pts = runs[r];
                float len = Length(pts);
                Fit(len, out int n, out float stretch, out float rem);
                if (n < 1) continue;
                float step = ModuleLength * stretch, s0 = rem * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float a = s0 + i * step, b = a + step;
                    TownPoint pa = PointAt(pts, a), pb = PointAt(pts, b), c = pb - pa;
                    float cl = c.Length; if (cl < 1e-4f) continue;
                    var nrm = new TownPoint(c.Z / cl, -c.X / cl);
                    modules.Add(new WallSlot
                    {
                        Piece = WallPiece.StraightA, Run = r, Index = modules.Count, Start = a, Length = step, Stretch = stretch,
                        Centre = (pa + pb) * 0.5f, Outward = nrm, Yaw = (float)(Math.Atan2(nrm.X, nrm.Z) * 180.0 / Math.PI),
                    });
                }
                WallSlot last = modules.FindLast(m => m.Run == r), start = modules.Find(m => m.Run == r);
                if (last == null) continue;
                // Direction of travel at a module is its chord direction c/|c| = (-o.Z, o.X) for the outward vector o = (c.Z, -c.X)/|c|.
                TownPoint alongStart = new TownPoint(-start.Outward.Z, start.Outward.X), alongEnd = new TownPoint(-last.Outward.Z, last.Outward.X);
                ends.Add(new WallSlot { Piece = WallPiece.End, Run = r, Index = -1, Start = start.Start, Length = 0f, Centre = PointAt(pts, start.Start), Outward = start.Outward, Yaw = start.Yaw, Close = alongStart * -1f });
                ends.Add(new WallSlot { Piece = WallPiece.End, Run = r, Index = -1, Start = last.Start + last.Length, Length = 0f, Centre = PointAt(pts, last.Start + last.Length), Outward = last.Outward, Yaw = last.Yaw, Close = alongEnd });
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
                if (straight && travelled >= nextStair && InsideLevel(m, ground))
                {
                    result.Add(Fitting(m, WallPiece.Stair));
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
            Piece = piece, Run = m.Run, Index = m.Index, Start = m.Start, Length = m.Length, Stretch = m.Stretch, Centre = m.Centre, Outward = m.Outward, Yaw = m.Yaw,
        };

        static float Ground(Func<float, float, float> g, WallSlot m, float side) => g(m.Centre.X + m.Outward.X * side, m.Centre.Z + m.Outward.Z * side);

        static bool Downhill(WallSlot m, Func<float, float, float> g) => Ground(g, m, -3f) - Ground(g, m, 3f) >= DownhillDrop;

        // The inside strip behind the module (1.5 to 3.5 m in) is level enough for a flight to land on, and the module itself is not on a drop.
        static bool InsideLevel(WallSlot m, Func<float, float, float> g)
        {
            float a = Ground(g, m, -1.5f), b = Ground(g, m, -2.5f), c = Ground(g, m, -3.5f);
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
