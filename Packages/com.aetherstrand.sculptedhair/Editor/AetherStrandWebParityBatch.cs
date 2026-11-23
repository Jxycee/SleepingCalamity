using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// Batch entry point: Tools/unity.ps1 exec -Method AetherStrand.Editor.AetherStrandWebParityBatch.Run
    /// Env: AS_TRACES (default C:/Dev/scratch/ptraces; every &lt;recipe&gt;/&lt;mode&gt; subfolder holding trace.json is replayed),
    ///      AS_RESULTS (output text file, default &lt;AS_TRACES&gt;/unity_results.txt).
    /// Replays each web trace through the C# solver (strict = init.json state, json = PresetGeometry.json) and reports error in mm,
    /// checks the dynamic mesh against the static generator at rest, and times solver + mesh update.
    /// Fails (throws) if any json-path run exceeds mean 0.5 mm / max 3 mm.
    /// </summary>
    public static class AetherStrandWebParityBatch
    {
        public static void Run()
        {
            string root = Environment.GetEnvironmentVariable("AS_TRACES") ?? "C:/Dev/scratch/ptraces";
            string outFile = Environment.GetEnvironmentVariable("AS_RESULTS") ?? Path.Combine(root, "unity_results.txt");
            var sb = new StringBuilder();
            var all = new List<AetherStrandWebParity.Result>();
            bool fail = false;
            foreach (string recipeDir in Directory.GetDirectories(root))
            foreach (string modeDir in Directory.GetDirectories(recipeDir))
            {
                if (!File.Exists(Path.Combine(modeDir, "trace.json"))) continue;
                foreach (bool strict in new[] { true, false })
                {
                    var r = AetherStrandWebParity.Run(modeDir, strict);
                    all.Add(r);
                    sb.AppendLine(r.ToString());
                    if (!strict && (r.meanOfMeansMm > 0.5 || r.maxMm > 3.0)) fail = true;
                }
            }
            sb.AppendLine();
            sb.AppendLine(MeshCheck(Path.Combine(root, "halfup059", "idle")));
            File.WriteAllText(outFile, sb.ToString());
            Debug.Log("PHYSICS_PARITY\n" + sb);
            if (fail) throw new Exception("AetherStrand web solver parity exceeded 0.5 mm mean / 3 mm max (see " + outFile + ")");
        }

        static string MeshCheck(string dir)
        {
            string json = File.ReadAllText(Path.Combine(dir, "PresetGeometry.json"));
            string[] zones;
            var locks = AetherStrandWebData.LoadGeometry(json, out zones);
            var solver = new AetherStrandWebSolver();
            solver.Load(locks);
            solver.SnapToHead(0, 0.44, 0, 0, 0, 0, 1, 1.0);
            var mb = new AetherStrandWebMesh(solver, locks);
            mb.Update(0, 0.44, 0, 0, 0, 0, 1, 1.0, 1f);

            // reference: the static generator (vertex-exact vs the web), legacy auto-normalise off
            var gen = typeof(AetherStrandMeshGenerator);
            var p = new HairSculptParams { overallScale = 1f, bangsLength = 1f, layerLength = 1f, widthScale = 1f, thicknessScale = 1f, legacyAutoNormalize = false };
            string tmp = Path.Combine(dir, "PresetGeometry.json");
            object lockList = gen.GetMethod("LoadLocksFromJson", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { tmp, p });
            var refMesh = (Mesh)gen.GetMethod("BuildVolumetricMesh", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { lockList, new Dictionary<int, int[]>(), 1 });
            var rv = refMesh.vertices;
            var dv = mb.Vertices;
            double maxMm = 0; int bad = 0;
            if (rv.Length != dv.Length) return $"MESH vertex count mismatch ref={rv.Length} dyn={dv.Length}";
            for (int i = 0; i < rv.Length; i++)
            {
                // dynamic mesh is in hair space (head pivot at (0,0.44,0)); static mesh is head-pivot-local
                Vector3 d = dv[i] - (rv[i] + new Vector3(0, 0.44f, 0));
                double mm = d.magnitude * 1000.0;
                if (mm > maxMm) maxMm = mm;
                if (mm > 0.05) bad++;
            }
            var ruv = refMesh.uv; var rcol = refMesh.colors; var rtri = refMesh.triangles;
            bool uvOk = true, colOk = true, triOk = rtri.Length == mb.mesh.triangles.Length;
            var duv = mb.mesh.uv; var dcol = mb.mesh.colors; var dtri = mb.mesh.triangles;
            for (int i = 0; i < ruv.Length && uvOk; i++) if ((ruv[i] - duv[i]).sqrMagnitude > 1e-10f) uvOk = false;
            for (int i = 0; i < rcol.Length && colOk; i++) if (Mathf.Abs(rcol[i].g - dcol[i].g) > 1e-5f || Mathf.Abs(rcol[i].b - dcol[i].b) > 1e-5f || Mathf.Abs(rcol[i].r - dcol[i].r) > 1e-5f || Mathf.Abs(rcol[i].a - dcol[i].a) > 1e-5f) colOk = false;
            for (int i = 0; i < rtri.Length && triOk; i++) if (rtri[i] != dtri[i]) triOk = false;

            // timing (300 frames of idle-ish motion, editor Mono)
            var cfg = new AetherStrandWebSolver.Config();
            double sMs = 0, mMs = 0; int n = 300;
            var sw = new System.Diagnostics.Stopwatch();
            for (int f = 0; f < n; f++)
            {
                double t = f / 60.0;
                sw.Restart();
                solver.Step(1 / 60.0, Math.Sin(t * 0.68) * 0.005, 0.44 + Math.Sin(t * 1.45) * 0.006, 0, 0, Math.Sin(Math.Sin(t * 0.65) * 0.125 / 2), 0, Math.Cos(Math.Sin(t * 0.65) * 0.125 / 2), 0, 0, 0, 0, 0, 0, 1, cfg);
                sMs += sw.Elapsed.TotalMilliseconds;
                sw.Restart();
                mb.Update(0, 0.44, 0, 0, 0, 0, 1, 1.0, 1f);
                mMs += sw.Elapsed.TotalMilliseconds;
            }
            return $"MESH static-vs-dynamic ({locks.Length} locks, {dv.Length} verts): max vertex diff {maxMm:F4} mm, verts>0.05mm={bad}, uv={(uvOk ? "ok" : "DIFF")} colors={(colOk ? "ok" : "DIFF")} tris={(triOk ? "ok" : "DIFF")}\n" +
                   $"TIMING Unity editor (Mono) {locks.Length} locks: solver step {sMs / n:F3} ms, mesh rebuild+upload {mMs / n:F3} ms";
        }
    }
}
