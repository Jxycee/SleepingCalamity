// World traversal scan: entry points. Run headless with
//   bash Tools/cloud/unity.sh exec --method SleepingCalamity.EditorTools.TraversalScan.RunAll --out <scratch>
// It scans Hollowmere (Ruined and Rebuilt presets) and Cellars floors 1-7 (the built dive floors) over the seed set (SC_TRAVERSAL_SEEDS, default 50: 12345 then 1, 2, ...; seeds
// are added until every layout preset, every twist, the dark floor and a generator fallback appear), then writes Reference/qa/traversal/REPORT.md and one
// top-down map per scene and floor (Cellars: the seed with the most issues). Every seed's map also goes to SC_TRAVERSAL_OUT when that is set.
//   ...TraversalScan.WriteBaseline   rewrites Reference/qa/traversal/baseline.json from the regression set (Hollowmere both presets + 10 seeds x floors 1-7)
// The regression test (TraversalRegressionTests) runs the same RegressionRun and fails on a target that became unreachable or more blocked/narrow spots.
// Nothing is saved to scenes or prefabs: the Hollowmere scene is opened read-only and closed with a fresh empty scene.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace SleepingCalamity.EditorTools
{
    public static class TraversalScan
    {
        public const string OutDir = "Reference/qa/traversal";
        public const string BaselineFile = OutDir + "/baseline.json";
        public static readonly uint[] RegressionSeeds = { 12345, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("Sleeping Calamity/QA/Traversal Scan (full)")]
        public static void RunAll()
        {
            float t0 = Time.realtimeSinceStartup;
            HeroCapsule hero = HeroCapsule.FromPrefab();
            Debug.Log("SC TRAVERSAL hero " + hero);
            string extra = Environment.GetEnvironmentVariable("SC_TRAVERSAL_OUT");
            int seedCount = int.TryParse(Environment.GetEnvironmentVariable("SC_TRAVERSAL_SEEDS"), out int sc) && sc > 0 ? sc : 50;
            bool skipTown = Environment.GetEnvironmentVariable("SC_TRAVERSAL_SKIP_TOWN") == "1";
            string outDir = Path.Combine(Project, OutDir);
            Directory.CreateDirectory(Path.Combine(outDir, "maps"));
            if (!string.IsNullOrEmpty(extra)) Directory.CreateDirectory(extra);

            var report = new TraversalReport(hero);
            if (!skipTown)
            {
                foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                {
                    EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
                    ScanSpace s = TraversalSpaces.Hollowmere(preset);
                    ScanResult r = TraversalProbe.Scan(s, hero, new ScanOptions());
                    Debug.Log($"SC TRAVERSAL {s.Name} issues={r.Issues.Count} targets={s.Targets.Count} reached={s.Targets.Count(x => x.Reached)} paths={s.Paths.Count} stairs={r.Stairs} visual={r.VisualMeshes} {r.Seconds:0}s");
                    report.Add(r);
                    string dump = Environment.GetEnvironmentVariable("SC_TRAVERSAL_DUMP");   // every issue of the town spaces, one line each (REPORT.md keeps only the worst 10 per kind)
                    if (!string.IsNullOrEmpty(dump)) File.WriteAllLines(Path.Combine(dump, s.Name + "_issues.txt"), r.Issues.Select(i => $"{i.Kind} ({i.Pos.x:0.0}, {i.Pos.y:0.0}, {i.Pos.z:0.0}) {i.Measured:0.00}/{i.Limit:0.00} [{i.Object}] {i.Detail}"));
                    TraversalReport.WriteMap(r, Path.Combine(outDir, "maps", s.Name + ".png"));
                }
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Environment.GetEnvironmentVariable("SC_TRAVERSAL_SKIP_CELLARS") == "1") { Debug.Log("SC TRAVERSAL town only (SC_TRAVERSAL_SKIP_CELLARS)"); return; }   // quick town-only dump runs; REPORT.md is not written
            List<uint> seeds = SeedSet(seedCount, out List<string> coverage);
            report.Coverage = coverage; report.Seeds = seeds;
            var worst = new Dictionary<int, (int score, string path)>();
            foreach (uint seed in seeds)
                for (int floor = 1; floor <= DiveFloorTable.LastBuiltFloor; floor++)
                {
                    ScanSpace s = TraversalSpaces.Cellar(seed, floor, out TraversalSpaces.CellarBuild b);
                    ScanResult r;
                    try { r = TraversalProbe.Scan(s, hero, new ScanOptions()); }
                    finally { Object.DestroyImmediate(b.Built.Root.gameObject); }
                    report.Add(r);
                    int score = r.Issues.Sum(i => TraversalIssue.Severity(i.Kind));
                    Debug.Log($"SC TRAVERSAL {s.Name} issues={r.Issues.Count} unreachable={r.Of(IssueKind.Unreachable).Count()} {r.Seconds:0.0}s");
                    if (!string.IsNullOrEmpty(extra)) TraversalReport.WriteMap(r, Path.Combine(extra, s.Name + ".png"));
                    if (!worst.TryGetValue(floor, out var w) || score > w.score)
                    {
                        worst[floor] = (score, s.Name);
                        TraversalReport.WriteMap(r, Path.Combine(outDir, "maps", $"Cellars_F{floor}.png"));
                        report.MapSeed[floor] = (int)seed;
                    }
                }
            report.Seconds = Time.realtimeSinceStartup - t0;
            report.Commit = Git("rev-parse --short HEAD");
            File.WriteAllText(Path.Combine(outDir, "REPORT.md"), report.Markdown());
            Debug.Log($"SC TRAVERSAL done issues={report.Total} in {report.Seconds:0}s -> {OutDir}/REPORT.md");
        }

        // The 50-seed set: 12345 first (the goldens' seed), then 1, 2, ...; then seeds from 1000 up that bring a missing preset, twist, the dark floor or a fallback.
        public static List<uint> SeedSet(int count, out List<string> coverage)
        {
            var seeds = new List<uint> { 12345 };
            for (uint s = 1; seeds.Count < count; s++) seeds.Add(s);
            var want = new HashSet<string>();
            foreach (LayoutPreset p in Enum.GetValues(typeof(LayoutPreset))) want.Add("preset " + p);
            foreach (FloorTwist t in Enum.GetValues(typeof(FloorTwist))) want.Add("twist " + t);
            want.Add("dark floor"); want.Add("fallback");
            var seen = new Dictionary<string, int>();
            void Note(uint seed)
            {
                for (int f = 1; f <= DiveFloorTable.LastBuiltFloor; f++)
                {
                    foreach (string k in Features(seed, f)) seen[k] = seen.TryGetValue(k, out int c) ? c + 1 : 1;
                }
            }
            foreach (uint s in seeds) Note(s);
            for (uint s = 1000; s < 4000 && want.Any(w => !seen.ContainsKey(w)); s++)
            {
                bool adds = false;
                for (int f = 1; f <= DiveFloorTable.LastBuiltFloor && !adds; f++) foreach (string k in Features(s, f)) if (want.Contains(k) && !seen.ContainsKey(k)) adds = true;
                if (adds) { seeds.Add(s); Note(s); }
            }
            coverage = want.OrderBy(x => x).Select(w => w + ": " + (seen.TryGetValue(w, out int c) ? c + " floors" : "NOT COVERED")).ToList();
            return seeds;
        }

        static IEnumerable<string> Features(uint seed, int floor)
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor, out int fb);
            if (l.Preset.HasValue) yield return "preset " + l.Preset.Value;
            yield return "twist " + CellarFloorRoster.TwistOf(seed, floor);
            if (CellarAtmosphere.IsDarkFloor(seed, floor)) yield return "dark floor";
            if (fb > 0) yield return "fallback";
            if (l.HasLockedRoom) yield return "locked room";
        }

        // ---------------------------------------------------------------------------------------------------------------- regression set
        public sealed class QuickEntry { public string Space; public List<string> Unreachable = new List<string>(); public int Blocked, Narrow; public List<string> Spots = new List<string>(); }

        public static List<QuickEntry> RegressionRun()
        {
            HeroCapsule hero = HeroCapsule.FromPrefab();
            var list = new List<QuickEntry>();
            foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
            {
                EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
                list.Add(Quick(TraversalProbe.Scan(TraversalSpaces.Hollowmere(preset), hero, ScanOptions.Quick)));
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (uint seed in RegressionSeeds)
                for (int floor = 1; floor <= DiveFloorTable.LastBuiltFloor; floor++)
                {
                    ScanSpace s = TraversalSpaces.Cellar(seed, floor, out TraversalSpaces.CellarBuild b);
                    try { list.Add(Quick(TraversalProbe.Scan(s, hero, ScanOptions.Quick))); }
                    finally { Object.DestroyImmediate(b.Built.Root.gameObject); }
                }
            return list;
        }

        static QuickEntry Quick(ScanResult r)
        {
            var e = new QuickEntry { Space = r.Space.Name };
            e.Unreachable.AddRange(r.Of(IssueKind.Unreachable).Select(i => i.Group).OrderBy(x => x, StringComparer.Ordinal));
            e.Blocked = r.Of(IssueKind.Blocked).Count();
            e.Narrow = r.Of(IssueKind.Narrow).Count();
            foreach (TraversalIssue i in r.Issues.Where(i => i.Kind == IssueKind.Blocked || i.Kind == IssueKind.Narrow))
                e.Spots.Add($"{i.Kind} ({i.Pos.x:0.0}, {i.Pos.y:0.0}, {i.Pos.z:0.0}) {i.Measured:0.00} m: {i.Detail}");
            return e;
        }

        [Serializable] public sealed class BaselineEntry { public string space; public string[] unreachable; public int blocked, narrow; }
        [Serializable] public sealed class Baseline { public string note; public string hero; public BaselineEntry[] entries; }

        [MenuItem("Sleeping Calamity/QA/Traversal Scan (write regression baseline)")]
        public static void WriteBaseline()
        {
            List<QuickEntry> run = RegressionRun();
            var b = new Baseline
            {
                note = "Known traversal issues the regression test tolerates (TraversalRegressionTests). Rewrite with TraversalScan.WriteBaseline only after fixing issues or with the owner's OK.",
                hero = HeroCapsule.FromPrefab().ToString(),
                entries = run.Select(e => new BaselineEntry { space = e.Space, unreachable = e.Unreachable.ToArray(), blocked = e.Blocked, narrow = e.Narrow }).ToArray(),
            };
            string path = Path.Combine(Project, BaselineFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(b, true) + "\n");
            Debug.Log($"SC TRAVERSAL baseline written: {run.Count} spaces, {run.Sum(e => e.Unreachable.Count)} unreachable, {run.Sum(e => e.Blocked)} blocked, {run.Sum(e => e.Narrow)} narrow -> {BaselineFile}");
        }

        public static Baseline ReadBaseline()
        {
            string path = Path.Combine(Project, BaselineFile);
            return File.Exists(path) ? JsonUtility.FromJson<Baseline>(File.ReadAllText(path)) : null;
        }

        // Evidence for one object (SC_TRAVERSAL_PROBE=<object name>, SC_TRAVERSAL_PRESET=Ruined|Rebuilt): the ground profile along its longest horizontal axis
        // through its centre (0.1 m steps) and the free width across it at body height, so a stair's risers and treads can be read off.
        public static void ProbeObject()
        {
            string name = Environment.GetEnvironmentVariable("SC_TRAVERSAL_PROBE");
            var preset = Environment.GetEnvironmentVariable("SC_TRAVERSAL_PRESET") == "Rebuilt" ? HollowmereBuilder.Preset.Rebuilt : HollowmereBuilder.Preset.Ruined;
            HollowmereSplit.OpenAll(HollowmereBuilder.ScenePath);
            HollowmereBuilder.ApplyPreset(preset);
            Physics.SyncTransforms();
            HeroCapsule hero = HeroCapsule.FromPrefab();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name != name) continue;
                Bounds b = default; bool any = false;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (!any) continue;
                bool alongX = b.size.x >= b.size.z;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"SC PROBE {TraversalSources.HierarchyPath(t)} bounds centre {b.center} size {b.size} colliders: " + string.Join(", ", t.GetComponentsInChildren<Collider>().Select(c => c.GetType().Name + (c is MeshCollider mc && mc.sharedMesh != null ? "(" + mc.sharedMesh.name + ", convex " + mc.convex + ")" : ""))));
                foreach (float off in new[] { -0.6f, 0f, 0.6f })
                {
                    sb.Append($"SC PROBE profile offset {off:+0.0;-0.0} m across: ");
                    float prev = float.NaN;
                    for (float u = -0.5f; u <= 0.5f + 1e-4f; u += 0.1f / Mathf.Max(b.size.x, b.size.z))
                    {
                        float x = alongX ? b.center.x + u * b.size.x : b.center.x + off, z = alongX ? b.center.z + off : b.center.z + u * b.size.z;
                        float y = Physics.Raycast(new Vector3(x, b.max.y + 2f, z), Vector3.down, out RaycastHit h, b.size.y + 6f) ? h.point.y : float.NaN;
                        if (float.IsNaN(prev) || Mathf.Abs(y - prev) > 0.05f) sb.Append($"[{(u + 0.5f) * Mathf.Max(b.size.x, b.size.z):0.0}m y{y:0.00} {(h.collider != null ? h.collider.name : "-")}] ");
                        prev = y;
                    }
                    sb.AppendLine();
                }
                Debug.Log(sb.ToString());
            }
        }

        static string Git(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args) { WorkingDirectory = Project, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using Process p = Process.Start(psi);
                string o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(5000);
                return o;
            }
            catch { return "?"; }
        }
    }
}
