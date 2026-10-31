// Renders the beauty-corner captures (PHASE1 3.7, brief section 1): Wide, Mid and Close at Midday plus Wide at LowSun, PNG 1920x1080, through the LookTest
// A1 rendering stack (LookTest_RPAsset, selected for the run only). Output folder: command line "-scCaptureDir <dir>", else env SC_CAPTURE_DIR.
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
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LookTestBuilder.RpAssetPath) ?? throw new FileNotFoundException(LookTestBuilder.RpAssetPath);
            // Only the quality level's override is set (it wins over the default pipeline): touching GraphicsSettings.defaultRenderPipeline dirties
            // ProjectSettings/GraphicsSettings.asset, and Unity then re-saves it with its newer default fields on exit.
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                Debug.Log("SC CAPTURE pipeline=" + GraphicsSettings.currentRenderPipeline.name);
                EditorSceneManager.OpenScene(BeautyCornerBuilder.ScenePath, OpenSceneMode.Single);
                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Midday);
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                SimulatePollen();
                LookTestCapture.WarmUp(cams);
                foreach (Camera cam in cams) LookTestCapture.Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + "_Midday.png"));
                foreach (Camera cam in cams) LogFrameTime(cam);

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
