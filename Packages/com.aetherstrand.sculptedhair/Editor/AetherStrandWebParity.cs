using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AetherStrand.Editor
{
    /// <summary>
    /// Replays a web trace (tools/web_trace.ts output: trace.json, trace.bin, init.json, PresetGeometry.json) through
    /// AetherStrandWebSolver and reports the per-frame particle position error in millimetres (web units = meters).
    /// Pure C# (the Unity entry point lives in AetherStrandWebParityBatch); also compiled by a throwaway console project.
    /// </summary>
    public static class AetherStrandWebParity
    {
        public class Result
        {
            public string name, mode, init;
            public int frames, particles;
            public double meanOfMeansMm, maxMm, finalMeanMm, finalMaxMm, worstFrameMeanMm;
            public int graphMismatch;
            public double msPerStep;
            public double[] frameMean, frameMax;
            public override string ToString() =>
                $"{name,-22} {mode,-5} {init,-6} locks={particles / 12,4} mean={meanOfMeansMm:F4}mm max={maxMm:F4}mm finalMean={finalMeanMm:F4} finalMax={finalMaxMm:F4} graphMismatch={graphMismatch} {msPerStep:F3}ms/step";
        }

        /// <param name="strict">true: init from init.json (full precision); false: from PresetGeometry.json (rounded, product path)</param>
        public static Result Run(string dir, bool strict, bool timeIt = true)
        {
            var meta = (Dictionary<string, object>)AetherStrandJson.Parse(File.ReadAllText(Path.Combine(dir, "trace.json")));
            int frames = (int)AetherStrandJson.D(meta["frames"]);
            int L = (int)AetherStrandJson.D(meta["lockCount"]);
            double dt = AetherStrandJson.D(meta["dt"]);
            var cfg = AetherStrandWebData.LoadConfig(meta["physics"]);
            var hp = (List<object>)meta["headPos"];
            var hq = (List<object>)meta["headQuat"];
            var imp = (List<object>)meta["impulse"];

            byte[] raw = File.ReadAllBytes(Path.Combine(dir, "trace.bin"));
            int frameFloats = L * 12 * 3;
            float[] trace = new float[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, trace, 0, raw.Length);

            var solver = new AetherStrandWebSolver();
            var initRoot = (Dictionary<string, object>)AetherStrandJson.Parse(File.ReadAllText(Path.Combine(dir, "init.json")));
            int graphMismatch = 0;
            if (strict) solver.Load(LocksFromInit(initRoot));
            else
            {
                string[] z;
                solver.Load(AetherStrandWebData.LoadGeometry(File.ReadAllText(Path.Combine(dir, "PresetGeometry.json")), out z));
            }
            // compare the C#-built cohesion graph against the web's dump, then always run on the web's graph in strict mode
            {
                int[] c = ToInt(initRoot["neighborCount"]), l = ToInt(initRoot["neighborLockIdx"]), n = ToInt(initRoot["neighborNodeIdx"]);
                double[] w = AetherStrandJson.Arr(initRoot["neighborWeight"]);
                var mc = solver.NeighborCount; var ml = solver.NeighborLock; var mn = solver.NeighborNode; var mw = solver.NeighborWeight;
                for (int i = 0; i < c.Length; i++)
                {
                    bool bad = mc[i] != c[i];
                    for (int m = 0; !bad && m < c[i]; m++)
                        if (ml[i * 4 + m] != l[i * 4 + m] || mn[i * 4 + m] != n[i * 4 + m] || Math.Abs(mw[i * 4 + m] - w[i * 4 + m]) > 1e-6) bad = true;
                    if (bad) graphMismatch++;
                }
                if (strict)
                {
                    var wf = new float[w.Length];
                    for (int i = 0; i < w.Length; i++) wf[i] = (float)w[i];
                    solver.OverrideCohesionGraph(c, l, n, wf);
                }
            }

            var res = new Result { name = Path.GetFileName(Path.GetDirectoryName(dir.TrimEnd('/', '\\'))), mode = (string)meta["mode"], init = strict ? "strict" : "json",
                frames = frames, particles = L * 12, graphMismatch = graphMismatch, frameMean = new double[frames + 1], frameMax = new double[frames + 1] };

            Pose(hp, hq, 0, out double x, out double y, out double zz, out double qx, out double qy, out double qz, out double qw);
            solver.SnapToHead(x, y, zz, qx, qy, qz, qw, cfg.strandWidthScale);
            Compare(solver, trace, 0, frameFloats, res);

            long ticks = 0;
            for (int f = 0; f < frames; f++)
            {
                double[] im = AetherStrandJson.Arr(imp[f + 1]);
                if (im[0] != 0 || im[1] != 0 || im[2] != 0) solver.ApplyImpulse(im[0], im[1], im[2]);
                Pose(hp, hq, f + 1, out x, out y, out zz, out qx, out qy, out qz, out qw);
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                solver.Step(dt, x, y, zz, qx, qy, qz, qw, 0, 0, 0, 0, 0, 0, 1, cfg);
                ticks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                Compare(solver, trace, f + 1, frameFloats, res);
            }
            res.msPerStep = ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames;
            double sum = 0;
            for (int f = 1; f <= frames; f++) { sum += res.frameMean[f]; if (res.frameMax[f] > res.maxMm) res.maxMm = res.frameMax[f]; if (res.frameMean[f] > res.worstFrameMeanMm) res.worstFrameMeanMm = res.frameMean[f]; }
            res.meanOfMeansMm = sum / frames;
            res.finalMeanMm = res.frameMean[frames];
            res.finalMaxMm = res.frameMax[frames];
            return res;
        }

        static int[] ToInt(object o)
        {
            var l = (List<object>)o; var r = new int[l.Count];
            for (int i = 0; i < r.Length; i++) r[i] = (int)AetherStrandJson.D(l[i]);
            return r;
        }

        static void Pose(List<object> hp, List<object> hq, int f, out double x, out double y, out double z, out double qx, out double qy, out double qz, out double qw)
        {
            double[] p = AetherStrandJson.Arr(hp[f]), q = AetherStrandJson.Arr(hq[f]);
            x = p[0]; y = p[1]; z = p[2]; qx = q[0]; qy = q[1]; qz = q[2]; qw = q[3];
        }

        static void Compare(AetherStrandWebSolver s, float[] trace, int f, int frameFloats, Result r)
        {
            double sum = 0, max = 0;
            int P = s.ParticleCount;
            int o = f * frameFloats;
            for (int i = 0; i < P; i++)
            {
                double dx = s.PosX[i] - trace[o + i * 3], dy = s.PosY[i] - trace[o + i * 3 + 1], dz = s.PosZ[i] - trace[o + i * 3 + 2];
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz) * 1000.0;
                sum += d; if (d > max) max = d;
            }
            r.frameMean[f] = sum / P; r.frameMax[f] = max;
        }

        static AetherStrandWebSolver.LockData[] LocksFromInit(Dictionary<string, object> init)
        {
            var list = (List<object>)init["locks"];
            var res = new AetherStrandWebSolver.LockData[list.Count];
            for (int s = 0; s < res.Length; s++)
            {
                var d = (Dictionary<string, object>)list[s];
                var ld = new AetherStrandWebSolver.LockData();
                var rl = (List<object>)d["restLocal"];
                for (int k = 0; k < 12; k++)
                {
                    double[] p = AetherStrandJson.Arr(rl[k]);
                    ld.rest[k * 3] = p[0]; ld.rest[k * 3 + 1] = p[1]; ld.rest[k * 3 + 2] = p[2];
                }
                ld.stiffnessWeight = AetherStrandJson.Arr(d["stiffnessWeight"]);
                ld.baseWidth = AetherStrandJson.D(d["baseWidth"]); ld.baseThickness = AetherStrandJson.D(d["baseThickness"]);
                ld.seed = AetherStrandJson.D(d["seed"]); ld.layerAO = AetherStrandJson.D(d["layerAO"]); ld.scalpRoot = AetherStrandJson.D(d["scalpRoot"]);
                ld.mass = AetherStrandJson.D(d["mass"]); ld.layerOrder = (int)AetherStrandJson.D(d["layerOrder"]);
                ld.restMidR = AetherStrandJson.D(d["restMidR"]); ld.rootRawY = AetherStrandJson.D(d["rootRawY"]);
                ld.isBaseShell = (bool)d["isBaseShell"]; ld.lockNormal = (bool)d["lockNormal"];
                ld.rootNormal = AetherStrandJson.Arr(d["rootNormalLocal"]);
                res[s] = ld;
            }
            return res;
        }

        public static string FormatTable(List<Result> rs)
        {
            var sb = new StringBuilder();
            foreach (var r in rs) sb.AppendLine(r.ToString());
            return sb.ToString();
        }
    }
}
