// Renders the fixed look-test capture cameras (PHASE1 3.7, P39): three per corner (wide, mid, close) at 1920x1080, plus one low-sun shot of the town.
// Run through the wrapper (needs a graphics device):
//   Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.LookTestCapture.CaptureAll -OutDir <scratch>   (env SC_CAPTURE_DIR = the PNG folder)
// The A1 stack is selected here (LookTest_RPAsset), for the run only. Unity can still re-save ProjectSettings/GraphicsSettings.asset and
// QualitySettings.asset when the batch run exits (a format bump plus the override): discard that with
//   git checkout -- ProjectSettings/GraphicsSettings.asset ProjectSettings/QualitySettings.asset
// Textures upload lazily in a fresh editor session (the first renders sampled one texture for every material), so each camera is rendered twice
// and discarded before the real captures (WarmUp).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class LookTestCapture
    {
        public const int Width = 1920, Height = 1080;

        public static void CaptureAll()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR to the folder for the PNGs");
            Directory.CreateDirectory(dir);

            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LookTestBuilder.RpAssetPath) ?? throw new FileNotFoundException(LookTestBuilder.RpAssetPath);
            RenderPipelineAsset savedGraphics = GraphicsSettings.defaultRenderPipeline, savedQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = rp; QualitySettings.renderPipeline = rp;
            try
            {
                Debug.Log("SC CAPTURE pipeline=" + GraphicsSettings.currentRenderPipeline.name);
                CaptureScene(LookTestBuilder.DayScene, dir, true);
                CaptureScene(LookTestBuilder.CellarScene, dir, false);
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = savedGraphics; QualitySettings.renderPipeline = savedQuality;
                CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        static void CaptureScene(string scenePath, string dir, bool day)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) RenderProbe(probe);
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(cams, (a, b) => string.CompareOrdinal(a.name, b.name));
            WarmUp(cams);
            foreach (Camera cam in cams) Save(cam, Path.Combine(dir, cam.name.Replace("Cap_", "") + ".png"));

            if (!day) return;
            // Low-sun shot: the sun near the horizon, warm and weaker, a warmer haze. Same wide camera.
            Light sun = RenderSettings.sun;
            sun.transform.rotation = Quaternion.Euler(13f, -32f, 0f); sun.color = new Color(1.0f, 0.62f, 0.36f); sun.intensity = 2.6f;
            RenderSettings.fogColor = new Color(0.82f, 0.68f, 0.58f);
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.47f, 0.50f); RenderSettings.ambientEquatorColor = new Color(0.50f, 0.44f, 0.37f);   // neutral-warm fill: a blue sky fill under an orange key read purple on the grey cobbles
            foreach (Camera cam in cams) if (cam.name == "Cap_Day_Wide") Save(cam, Path.Combine(dir, "Day_Wide_LowSun.png"));
        }

        // The first renders after a scene opens draw with textures that have not finished uploading (every material sampled the same one):
        // render each camera twice and discard the result, pumping the editor loop in between.
        internal static void WarmUp(Camera[] cams)
        {
            for (int pass = 0; pass < 2; pass++)
                foreach (Camera cam in cams)
                {
                    var rt = new RenderTexture(Width / 4, Height / 4, 24, RenderTextureFormat.ARGB32);
                    cam.targetTexture = rt; cam.enabled = true; cam.Render(); cam.enabled = false; cam.targetTexture = null;
                    rt.Release(); Object.DestroyImmediate(rt);
                    for (int i = 0; i < 10; i++) { EditorApplication.QueuePlayerLoopUpdate(); System.Threading.Thread.Sleep(30); }
                }
        }

        // A realtime probe in the editor renders asynchronously: ask, then pump until it reports finished.
        // The realtime probe never finishes in a batch run, so it is baked (blocking) into a temporary EXR, switched to Custom for the capture and
        // deleted afterwards (the scene file is never saved here, so nothing refers to it).
        internal static void RenderProbe(ReflectionProbe probe)
        {
            const string folder = "Assets/_Project/Content/Rendering/_ProbeTmp";
            BuilderUtil.EnsureFolder(folder);
            string path = folder + "/" + probe.transform.root.name + (probe.name == "ReflectionProbe" ? "" : "_" + probe.name) + ".exr";   // named probes (the water's) get their own file
            bool ok = Lightmapping.BakeReflectionProbe(probe, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (ok && tex != null) { probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = tex; }
            Debug.Log("SC CAPTURE probe " + probe.transform.root.name + " baked=" + ok + " texture=" + (tex != null));
        }

        internal static void CleanProbes()
        {
            AssetDatabase.DeleteAsset("Assets/_Project/Content/Rendering/_ProbeTmp");
            AssetDatabase.Refresh();
        }

        internal static void Save(Camera cam, string path, System.Action<Texture2D> withPixels = null, bool writeFile = true)   // withPixels sees the frame before it is destroyed (T26 crops and motion strips)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture prevActive = RenderTexture.active;
            cam.targetTexture = rt; cam.enabled = true;
            cam.Render();
            cam.enabled = false; cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); tex.Apply();
            if (writeFile) File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prevActive;
            withPixels?.Invoke(tex);
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            if (writeFile) Debug.Log("SC CAPTURE wrote " + path);
        }
    }
}
