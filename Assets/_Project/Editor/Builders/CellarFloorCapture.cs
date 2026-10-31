// Renders the Cellars floor test's capture cameras (Floor_Overview with the Ceiling groups hidden, Floor_Room, Floor_Corridor; SC_CAPTURE_ONLY=<names> limits them) to JPG 1920x1080 through the look-test rendering stack (selected for the run
// only, like DungeonCornerCapture). Output folder: "-scCaptureDir <dir>", else env SC_CAPTURE_DIR, else <repo>/Reference/cellars/floors. Builds the scene first
// unless SC_CAPTURE_NOBUILD is set. Files: floor_overview.jpg, floor_room.jpg, floor_corridor.jpg. A graphics run rewrites materials and the scene: do not commit those.
//   powershell -File Tools/unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.CellarFloorCapture.CaptureAll -OutDir <scratch>
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SleepingCalamity.Presentation;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class CellarFloorCapture
    {
        [MenuItem("SleepingCalamity/Art/Capture Cellar Floor Test")]
        public static void CaptureMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CaptureTo(DefaultDir());
        }

        static string DefaultDir() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reference", "cellars", "floors"));

        public static void CaptureAll()
        {
            string dir = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-scCaptureDir") dir = args[i + 1];
            if (string.IsNullOrEmpty(dir)) dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = DefaultDir();
            CaptureTo(dir);
        }

        public static void CaptureTo(string dir)
        {
            Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) CellarFloorTestBuilder.Build();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LookTestBuilder.RpAssetPath) ?? throw new FileNotFoundException(LookTestBuilder.RpAssetPath);
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                EditorSceneManager.OpenScene(CellarFloorTestBuilder.ScenePath, OpenSceneMode.Single);
                Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                System.Array.Sort(cams, (a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                LookTestCapture.WarmUp(cams);
                string only = System.Environment.GetEnvironmentVariable("SC_CAPTURE_ONLY");
                var ceilings = new System.Collections.Generic.List<GameObject>();
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == CellarFloorBuilder.CeilingGroup) ceilings.Add(t.gameObject);
                foreach (Camera cam in cams)
                {
                    if (!cam.name.StartsWith("Floor_")) continue;
                    if (!string.IsNullOrEmpty(only) && System.Array.IndexOf(only.Split(','), cam.name) < 0) continue;
                    bool overview = cam.name == "Floor_Overview";   // the cutaway: vault ceilings off so the whole floor reads from above
                    foreach (GameObject g in ceilings) g.SetActive(!overview);
                    SaveJpg(cam, Path.Combine(dir, cam.name.Replace("Floor_", "floor_").ToLowerInvariant() + ".jpg"));
                }
                foreach (GameObject g in ceilings) g.SetActive(true);
            }
            finally
            {
                QualitySettings.renderPipeline = savedQuality;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        static void SaveJpg(Camera cam, string path)
        {
            int w = LookTestCapture.Width, h = LookTestCapture.Height;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture prev = RenderTexture.active;
            cam.targetTexture = rt; cam.enabled = true; cam.Render(); cam.enabled = false; cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToJPG(92));
            RenderTexture.active = prev;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            Debug.Log("SC CAPTURE wrote " + path);
        }
    }
}
