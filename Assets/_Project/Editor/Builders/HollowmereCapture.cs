// Renders the Hollowmere approval captures (Wave 0, design section 7): every HollowmereBuilder play-position camera (heroes' home door, fountain, tithe gate,
// lane mouth, overview without fog) in both presets (Ruined, Rebuilt) at Midday (the four play positions also at LowSun, files <Camera>_<Preset>_LowSun.png), plus the approved corner's Wide camera in both presets, PNG 1920x1080, through the
// beauty corner's own rendering stack (selected for the run only, like BeautyCornerCapture). Output folder: "-scCaptureDir <dir>", else env SC_CAPTURE_DIR.
//   bash Tools/cloud/unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereCapture.CaptureAll --out <scratch>   (env SC_CAPTURE_DIR = the PNG folder)
// Files: <Camera>_<Preset>.png, e.g. HM_HomeDoor_Ruined.png. Builds the scene first unless SC_CAPTURE_NOBUILD is set.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereCapture
    {
        // The design 7 play positions: square entry from the heroes' home, the fountain, the tithe gate, a lane mouth.
        static readonly string[] PlayPositionCameras = { "Cap_HM_HomeDoor", "Cap_HM_Fountain", "Cap_HM_TitheGate", "Cap_HM_LaneMouth",
            "Cap_HM_LakeShore", "Cap_HM_LakeJetty", "Cap_HM_Ravine", "Cap_HM_MillLane", "Cap_HM_Mill" };   // + the water cameras (Q10)

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
                // SC_CAPTURE_SUN=LowSun renders only the low-sun look, every camera, "_LowSun" added to the file names (main's single-light run).
                bool lowOnly = System.Environment.GetEnvironmentVariable("SC_CAPTURE_SUN") == "LowSun";
                string only = System.Environment.GetEnvironmentVariable("SC_CAPTURE_ONLY");   // e.g. "SquareSouth,Fountain": only the cameras whose name contains one of them
                Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                // Default: Midday renders every camera; LowSun (design 7: "at midday and low sun") only the four play-position cameras, files <Camera>_<Preset>_LowSun.png.
                BeautyCornerBuilder.Preset[] lights = lowOnly ? new[] { BeautyCornerBuilder.Preset.LowSun } : new[] { BeautyCornerBuilder.Preset.Midday, BeautyCornerBuilder.Preset.LowSun };
                foreach (BeautyCornerBuilder.Preset light in lights)
                {
                    BeautyCornerBuilder.ApplyPreset(light);
                    string suffix = light == BeautyCornerBuilder.Preset.Midday ? "" : "_" + light;
                    foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                    {
                        HollowmereBuilder.ApplyPreset(preset);
                        foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                        LookTestCapture.WarmUp(cams);
                        foreach (Camera cam in cams)
                        {
                            if (!cam.name.StartsWith("Cap_HM_") && !cam.name.StartsWith("Cap_Corner_")) continue;
                            if (!string.IsNullOrEmpty(only) && !only.Split(',').Any(o => cam.name.Contains(o))) continue;
                            if (!lowOnly && light != BeautyCornerBuilder.Preset.Midday && System.Array.IndexOf(PlayPositionCameras, cam.name) < 0) continue;
                            bool overview = cam.name == "Cap_HM_Overview", fog = RenderSettings.fog;
                            if (overview) RenderSettings.fog = false;   // the corner's haze is tuned for 10-40 m; at 100 m+ it would hide the town
                            try { LookTestCapture.Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + "_" + preset + suffix + ".png")); }
                            finally { RenderSettings.fog = fog; }
                        }
                    }
                }
                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Midday);
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
