// Renders the beauty-corner captures (PHASE1 3.7, brief section 1): Wide, Mid and Close at Midday plus Wide at LowSun, PNG 1920x1080, through the LookTest
// A1 rendering stack (LookTest_RPAsset, selected for the run only). Output folder: command line "-scCaptureDir <dir>", else env SC_CAPTURE_DIR.
//   Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.BeautyCornerCapture.CaptureAll -OutDir <scratch>   (env SC_CAPTURE_DIR = the PNG folder)
// Menu: SleepingCalamity/Art/Capture Beauty Corner (asks for the folder). Files: Corner_Wide_Midday.png, Corner_Mid_Midday.png, Corner_Close_Midday.png, Corner_Wide_LowSun.png.
// Unity can re-save ProjectSettings/GraphicsSettings.asset and QualitySettings.asset when a batch run exits: discard with
//   git checkout -- ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset
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
            RenderPipelineAsset savedGraphics = GraphicsSettings.defaultRenderPipeline, savedQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = rp; QualitySettings.renderPipeline = rp;
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

                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.LowSun);
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                foreach (Camera cam in cams) if (cam.name == "Cap_Corner_Wide") LookTestCapture.Save(cam, Path.Combine(dir, "Corner_Wide_LowSun.png"));
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = savedGraphics; QualitySettings.renderPipeline = savedQuality;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        // Particles do not run in edit mode: step the pollen forward so the frame holds a settled cloud (fixed seed, so every round is identical).
        static void SimulatePollen()
        {
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(14f, true, true);
        }
    }
}
