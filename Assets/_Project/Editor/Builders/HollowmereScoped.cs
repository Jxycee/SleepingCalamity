// T26 speed tooling for the Hollowmere art loop. None of it changes scene content: the scoped build runs the same HollowmereBuilder.Build as ProjectBuilder.BuildAll's "Hollowmere" step,
// so Hollowmere.unity and its generated assets come out as the same bytes (proved by diff, T26 report). BuildAll stays the gate and merge build.
//
//   Build only Hollowmere (about a fifth of a BuildAll):
//     powershell -NoProfile -ExecutionPolicy Bypass -File Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.HollowmereScoped.Build -OutDir <scratch>
//   Build, then capture in the same editor process (no second boot, no second asset refresh; needs -Graphics; env as HollowmereCapture: SC_CAPTURE_DIR, SC_CAPTURE_ONLY, SC_CAPTURE_PRESET, SC_CAPTURE_SUN, ...):
//     ... exec -Graphics -Method SleepingCalamity.EditorTools.HollowmereScoped.BuildAndCapture -OutDir <scratch>
//   Which cameras see what a task touched (env SC_PICK_ROOTS=BankSteps,GroundProps: object names or name prefixes anywhere under the Hollowmere root; SC_PICK_DIST metres, default 120; SC_PICK_CAMS=Cap_HM_Eye_ limits the cameras by name prefix):
//     ... exec -Method SleepingCalamity.EditorTools.HollowmereScoped.Pick -OutDir <scratch>      logs "SC PICK <cameras>", ready for SC_CAPTURE_ONLY
//
// T24b: Hollowmere is the main scene plus additive art scenes (Hollowmere_Town, _Outskirts, _Backdrop; HollowmereSplit). SC_SCOPED_SCENES=Town,Backdrop (Main = Hollowmere.unity) saves only those
// scene files; the in-memory build, which the builders' collider and cache dependencies need, still runs in full, and the rest of the files stay as they are on disk (same bytes as BuildAll).
// Steps of the scoped build: Hollowmere (HollowmereBuilder.Build), MaterialValidation.SettleAll (the same settle BuildAll ends with), SaveAssets. Optional: SC_SCOPED_TREES=1 also runs AreaTreesIntake
// (the tree prefabs; normally unchanged), SC_SCOPED_VALIDATE=1 also runs ValidationRunner (BuildAll's validators). No section option: HollowmereBuilder.Build creates the scene fresh and the scene-diet
// passes (Combine, mesh externalize, LODs) run over all of it at the end, so rerunning single sections cannot give the same bytes.
// It needs the other builders' output on disk (materials, look assets, prefabs): run BuildAll once on a fresh checkout or after changing anything outside Hollowmere.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereScoped
    {
        public static void Build()
        {
            if (!Run(out _) && Application.isBatchMode) EditorApplication.Exit(1);
        }

        public static void BuildAndCapture()
        {
            string dir = HollowmereCapture.ResolveDir();
            if (!Run(out double buildSecs)) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            var sw = Stopwatch.StartNew();
            HollowmereCapture.CaptureTo(dir, false);
            Debug.Log("SC SCOPED BUILDANDCAPTURE PASS build=" + buildSecs.ToString("F0") + "s capture=" + sw.Elapsed.TotalSeconds.ToString("F0") + "s");
        }

        static bool Run(out double secs)
        {
            var sw = Stopwatch.StartNew();
            var errors = new List<string>();
            HollowmereBuilder.ScopedScenes = HollowmereSplit.ScopedFromEnvironment();   // throws on a bad name before the long build starts
            try { Step(errors, "Hollowmere", HollowmereBuilder.Build); }
            finally { HollowmereBuilder.ScopedScenes = null; }
            Step(errors, "BuildSettings", SceneBuilder.ApplyBuildSettings);   // T24b: the art scenes' Build Settings entries
            if (Environment.GetEnvironmentVariable("SC_SCOPED_TREES") == "1") Step(errors, "AreaTrees", AreaTreesIntake.Run);
            Step(errors, "MaterialValidation", () => Debug.Log("SC SCOPED materials settled by ShaderGUI validation: " + MaterialValidation.SettleAll()));
            Step(errors, "Save", AssetDatabase.SaveAssets);
            if (Environment.GetEnvironmentVariable("SC_SCOPED_VALIDATE") == "1") Step(errors, "Validate", () => errors.AddRange(ValidationRunner.Run()));
            secs = sw.Elapsed.TotalSeconds;
            if (errors.Count == 0) { Debug.Log("SC SCOPED BUILD PASS " + secs.ToString("F0") + "s"); return true; }
            Debug.Log("SC SCOPED BUILD FAIL errors=" + errors.Count);
            foreach (string e in errors) Debug.Log("SC SCOPED BUILD ERROR " + e);
            return false;
        }

        static void Step(List<string> errors, string label, Action action)
        {
            try { action(); }
            catch (Exception e) { errors.Add(label + ": " + e.GetType().Name + ": " + e.Message); Debug.LogException(e); }
        }

        // ------------------------------------------------------------------------------------------------ changed-camera picker
        public static void Pick()
        {
            string rootsEnv = Environment.GetEnvironmentVariable("SC_PICK_ROOTS");
            if (string.IsNullOrWhiteSpace(rootsEnv)) throw new InvalidOperationException("Set SC_PICK_ROOTS to comma-separated object names or name prefixes (e.g. BankSteps,GroundProps)");
            string[] roots = rootsEnv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            float maxDist = float.TryParse(Environment.GetEnvironmentVariable("SC_PICK_DIST"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) && d > 0f ? d : 120f;
            string camPrefix = Environment.GetEnvironmentVariable("SC_PICK_CAMS") is string cp && cp.Length > 0 ? cp : "Cap_HM_";   // SC_PICK_CAMS=Cap_HM_Eye_ lists only the eye set
            HollowmereSplit.OpenAll(HollowmereBuilder.ScenePath);
            List<GameObject> towns = HollowmereSplit.AllRoots();   // the main scene's root and the art scenes' copies of it
            if (towns.Count == 0) throw new InvalidOperationException("No Hollowmere root in the scene");
            var bounds = new List<Bounds>(); var matched = new List<string>();
            foreach (Transform t in towns.SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            {
                if (t.name == "CaptureCameras" || t.name.StartsWith("Cap_", StringComparison.Ordinal)) continue;
                if (!roots.Any(r => t.name.StartsWith(r, StringComparison.Ordinal))) continue;
                Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                matched.Add(t.name);
                foreach (Renderer r in rs) bounds.Add(r.bounds);   // nested matches add their renderers twice: harmless for a containment test
            }
            var picked = new List<string>();
            foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(c => c.name, StringComparer.Ordinal))
            {
                if (!cam.name.StartsWith(camPrefix, StringComparison.Ordinal)) continue;
                cam.aspect = 16f / 9f;
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
                float reach = Mathf.Min(maxDist, cam.farClipPlane);
                foreach (Bounds b in bounds)
                    if (b.SqrDistance(cam.transform.position) <= reach * reach && GeometryUtility.TestPlanesAABB(planes, b)) { picked.Add(cam.name); break; }
            }
            Debug.Log("SC PICK matched " + matched.Count + " objects (" + string.Join(",", matched.Distinct().Take(8)) + (matched.Distinct().Count() > 8 ? ",..." : "") + "), " + bounds.Count + " renderers, within " + maxDist + " m");
            Debug.Log("SC PICK " + string.Join(",", picked));
        }
    }
}
