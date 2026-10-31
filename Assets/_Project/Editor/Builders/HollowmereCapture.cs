// Renders the Hollowmere approval captures (Wave 0, design section 7): every HollowmereBuilder play-position camera (heroes' home door, fountain, tithe gate,
// lane mouth, overview without fog) in both presets (Ruined, Rebuilt) at Midday, plus the approved corner's Wide camera in both presets, PNG 1920x1080, through the
// beauty corner's own rendering stack (selected for the run only, like BeautyCornerCapture). Output folder: "-scCaptureDir <dir>", else env SC_CAPTURE_DIR.
//   bash Tools/cloud/unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereCapture.CaptureAll --out <scratch>   (env SC_CAPTURE_DIR = the PNG folder)
// Files: <Camera>_<Preset>.png, e.g. HM_HomeDoor_Ruined.png. Builds the scene first unless SC_CAPTURE_NOBUILD is set.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereCapture
    {
        [MenuItem("SleepingCalamity/Art/Capture Hollowmere")]
        public static void CaptureMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("Hollowmere captures", "", "");
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

        public static void CaptureTo(string dir)
        {
            Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) HollowmereBuilder.Build();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
                // SC_CAPTURE_SUN=LowSun renders the low-sun look and adds "_LowSun" to the file names (default Midday, names unchanged).
                bool low = System.Environment.GetEnvironmentVariable("SC_CAPTURE_SUN") == "LowSun";
                BeautyCornerBuilder.ApplyPreset(low ? BeautyCornerBuilder.Preset.LowSun : BeautyCornerBuilder.Preset.Midday);
                Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                {
                    HollowmereBuilder.ApplyPreset(preset);
                    foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                    LookTestCapture.WarmUp(cams);
                    foreach (Camera cam in cams)
                    {
                        if (!cam.name.StartsWith("Cap_HM_") && !cam.name.StartsWith("Cap_Corner_")) continue;
                        bool overview = cam.name == "Cap_HM_Overview", fog = RenderSettings.fog;
                        if (overview) RenderSettings.fog = false;   // the corner's haze is tuned for 10-40 m; at 100 m+ it would hide the town
                        try { LookTestCapture.Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + "_" + preset + (low ? "_LowSun" : "") + ".png")); }
                        finally { RenderSettings.fog = fog; }
                    }
                }
                HollowmereBuilder.ApplyPreset(HollowmereBuilder.Preset.Ruined);
            }
            finally
            {
                QualitySettings.renderPipeline = savedQuality;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }
    }
}
