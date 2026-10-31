// Renders the dungeon corner's three capture cameras (Cap_Cellar_Wide, _Mid, _Close; PHASE1 3.7) to PNG 1920x1080 through the look-test rendering stack
// (selected for the run only, like HollowmereCapture). Output folder: "-scCaptureDir <dir>", else env SC_CAPTURE_DIR. Builds the scene first unless SC_CAPTURE_NOBUILD is set.
//   SC_CAPTURE_DIR=<dir> bash Tools/cloud/unity.sh exec --graphics --method SleepingCalamity.EditorTools.DungeonCornerCapture.CaptureAll --out <scratch>
// Files: Cellar_Wide.png, Cellar_Mid.png, Cellar_Close.png.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class DungeonCornerCapture
    {
        [MenuItem("SleepingCalamity/Art/Capture Dungeon Corner")]
        public static void CaptureMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("Dungeon corner captures", "", "");
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
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) DungeonCornerBuilder.Build();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LookTestBuilder.RpAssetPath) ?? throw new FileNotFoundException(LookTestBuilder.RpAssetPath);
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                EditorSceneManager.OpenScene(DungeonCornerBuilder.ScenePath, OpenSceneMode.Single);
                Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                System.Array.Sort(cams, (a, b) => string.CompareOrdinal(a.name, b.name));   // deterministic warm-up and save order
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(8f, true, true);   // motes exist in the edit-mode render
                LookTestCapture.WarmUp(cams);
                foreach (Camera cam in cams)
                    if (cam.name.StartsWith("Cap_Cellar_")) LookTestCapture.Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + ".png"));
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
