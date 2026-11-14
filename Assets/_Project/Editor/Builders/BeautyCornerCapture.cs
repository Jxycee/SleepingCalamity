// Renders the beauty-corner captures (PHASE1 3.7, brief section 1): Wide, Mid and Close at Midday plus Wide at LowSun, PNG 1920x1080, through the beauty corner's
// own copy of the LookTest A1 rendering stack (BeautyCorner_RPAsset, selected for the run only). Output folder: command line "-scCaptureDir <dir>", else env SC_CAPTURE_DIR.
//   Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.BeautyCornerCapture.CaptureAll -OutDir <scratch>   (env SC_CAPTURE_DIR = the PNG folder)
// Menu: SleepingCalamity/Art/Capture Beauty Corner (asks for the folder). Files: Corner_Wide_Midday.png, Corner_Mid_Midday.png, Corner_Close_Midday.png, Corner_Wide_LowSun.png.
// The run leaves ProjectSettings untouched (the pipeline is swapped through QualitySettings.renderPipeline only and put back).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class BeautyCornerCapture
    {
        [MenuItem("SleepingCalamity/Art/Capture Beauty Corner")]
        public static void CaptureMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("Beauty corner captures", "", "");
            if (string.IsNullOrEmpty(dir)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CaptureTo(dir);
        }

        public static void CaptureAll()
        {
            string dir = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-scCaptureDir") dir = args[i + 1];
            if (string.IsNullOrEmpty(dir)) dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR (or pass -scCaptureDir) to the folder for the PNGs");
            CaptureTo(dir);
        }

        // Builds the scene first (idempotent, picks up art that landed since) unless SC_CAPTURE_NOBUILD is set, then renders.
        public static void CaptureTo(string dir)
        {
            Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) BeautyCornerBuilder.Build();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            // Only the quality level's override is set (it wins over the default pipeline): touching GraphicsSettings.defaultRenderPipeline dirties
            // ProjectSettings/GraphicsSettings.asset, and Unity then re-saves it with its newer default fields on exit.
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                Debug.Log("SC CAPTURE pipeline=" + GraphicsSettings.currentRenderPipeline.name);
                EditorSceneManager.OpenScene(BeautyCornerBuilder.ScenePath, OpenSceneMode.Single);
                string b0 = System.Environment.GetEnvironmentVariable("SC_CAPTURE_SUN");
                if (b0 == "Morning" || b0 == "Evening" || b0 == "Elev" || b0 == "B0") { CaptureB0(dir, b0); return; }
                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Midday);
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                SimulatePollen();
                LookTestCapture.WarmUp(cams);
                foreach (Camera cam in cams) LookTestCapture.Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + "_Midday.png"));
                foreach (Camera cam in cams) LogFrameTime(cam);
                foreach (Camera cam in cams) LogCobbleLods(cam);

                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.LowSun);
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                foreach (Camera cam in cams) if (cam.name == "Cap_Corner_Wide") LookTestCapture.Save(cam, Path.Combine(dir, "Corner_Wide_LowSun.png"));
            }
            finally
            {
                QualitySettings.renderPipeline = savedQuality;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        // The B0 lighting-test captures (Hollowmere design section 4), chosen by SC_CAPTURE_SUN and narrowed by SC_CAPTURE_ONLY (camera names containing one of the comma-separated words):
        //   Morning: every camera at Morning -> Corner_<Wide|Mid|Close>_Morning.png     Evening: Cap_Corner_Wide at Evening -> Corner_Wide_Evening.png
        //   Elev: Cap_Corner_Wide at the Morning colour with the sun at 54, 46 and 38 degrees -> Corner_Wide_Elev54/46/38.png     B0: all three.
        // The default (SC_CAPTURE_SUN unset, Midday or LowSun) is the unchanged approved-corner run above.
        static void CaptureB0(string dir, string mode)
        {
            string only = System.Environment.GetEnvironmentVariable("SC_CAPTURE_ONLY");
            string[] words = string.IsNullOrEmpty(only) ? null : only.Split(',');
            bool Pick(Camera c) { if (words == null) return true; foreach (string w in words) if (c.name.Contains(w.Trim())) return true; return false; }
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(cams, (a, b) => string.CompareOrdinal(a.name, b.name));
            bool all = mode == "B0";
            void Probes() { foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe); }
            SimulatePollen();
            LookTestCapture.WarmUp(cams);
            if (all || mode == "Morning")
            {
                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Morning); Probes(); BeautyCornerBuilder.LogSkySunCheck(BeautyCornerBuilder.Preset.Morning);
                foreach (Camera cam in cams) if (cam.name.StartsWith("Cap_Corner_") && Pick(cam)) LookTestCapture.Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + "_Morning.png"));
            }
            if (all || mode == "Elev")
                foreach (int el in new[] { 54, 46, 38 })
                {
                    BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Morning); BeautyCornerBuilder.SetSunElevation(el); Probes();
                    foreach (Camera cam in cams) if (cam.name == "Cap_Corner_Wide" && Pick(cam)) LookTestCapture.Save(cam, Path.Combine(dir, "Corner_Wide_Elev" + el + ".png"));
                }
            if (all || mode == "Evening")
            {
                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Evening); Probes(); BeautyCornerBuilder.LogSkySunCheck(BeautyCornerBuilder.Preset.Evening);
                foreach (Camera cam in cams) if (cam.name == "Cap_Corner_Wide" && Pick(cam)) LookTestCapture.Save(cam, Path.Combine(dir, "Corner_Wide_Evening.png"));
            }
            BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Midday);
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        // Proof that the spawned cobbles render with their LODGroups: how many setts each capture camera sees at LOD0, LOD1 or culled (-1).
        static void LogCobbleLods(Camera cam)
        {
            int l0 = 0, l1 = 0, culled = 0, other = 0, groups = 0;
            foreach (LODGroup g in Resources.FindObjectsOfTypeAll<LODGroup>())   // the spawned setts are hidden, DontSave objects: only this finds them
            {
                if (!g.gameObject.scene.IsValid()) continue;   // prefab assets
                groups++; if (!g.name.StartsWith("Sett_")) continue;
                // The LODGroup rule: relative screen height = size x largest scale / (2 x distance x tan(fov / 2)); the first LOD whose threshold it reaches is shown.
                LOD[] lods = g.GetLODs(); Transform t = g.transform; Vector3 sc = t.lossyScale;
                float dist = Vector3.Distance(cam.transform.position, t.TransformPoint(g.localReferencePoint));
                float h = g.size * Mathf.Max(sc.x, Mathf.Max(sc.y, sc.z)) / (2f * Mathf.Max(0.01f, dist) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                int lod = -1;
                for (int i = 0; i < lods.Length; i++) if (h >= lods[i].screenRelativeTransitionHeight) { lod = i; break; }
                if (lod == 0) l0++; else if (lod == 1) l1++; else if (lod < 0) culled++; else other++;
            }
            Debug.Log("SC CAPTURE cobble lods " + cam.name + " lod0=" + l0 + " lod1=" + l1 + " culled=" + culled + " other=" + other + " (of " + groups + " LODGroups in the scene)");
        }

        // Rough frame cost of one capture camera: the mean wall time of 10 renders to a 1920x1080 target, each ended by a ReadPixels (which waits for the GPU).
        // It includes the CPU render-loop cost and, in a batch run, the editor's own overhead: use it to compare rounds, not as a game frame time.
        static void LogFrameTime(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = cam.targetTexture, prevActive = RenderTexture.active;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                cam.targetTexture = rt;
                for (int i = 0; i < 3; i++) cam.Render();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                const int n = 10;
                for (int i = 0; i < n; i++)
                {
                    cam.Render();
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                }
                sw.Stop();
                Debug.Log("SC CAPTURE frame " + cam.name + " ms=" + (sw.Elapsed.TotalMilliseconds / n).ToString("F1"));
            }
            finally
            {
                cam.targetTexture = prevTarget; RenderTexture.active = prevActive; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex);
            }
        }

        // Particles do not run in edit mode: step the pollen forward so the frame holds a settled cloud (fixed seed, so every round is identical).
        static void SimulatePollen()
        {
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(14f, true, true);
        }
    }
}
