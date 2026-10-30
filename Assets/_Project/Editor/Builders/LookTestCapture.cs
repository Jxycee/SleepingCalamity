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
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.42f, 0.52f); RenderSettings.ambientEquatorColor = new Color(0.46f, 0.38f, 0.32f);
            foreach (Camera cam in cams) if (cam.name == "Cap_Day_Wide") Save(cam, Path.Combine(dir, "Day_Wide_LowSun.png"));
        }

        // The first renders after a scene opens draw with textures that have not finished uploading (every material sampled the same one):
        // render each camera twice and discard the result, pumping the editor loop in between.
        static void WarmUp(Camera[] cams)
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
        static void RenderProbe(ReflectionProbe probe)
        {
            int id = probe.RenderProbe();
            for (int i = 0; i < 400 && !probe.IsFinishedRendering(id); i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                System.Threading.Thread.Sleep(25);
            }
            Debug.Log("SC CAPTURE probe " + probe.transform.root.name + " finished=" + probe.IsFinishedRendering(id));
        }

        static void Save(Camera cam, string path)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture prevActive = RenderTexture.active;
            cam.targetTexture = rt; cam.enabled = true;
            cam.Render();
            cam.enabled = false; cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            Debug.Log("SC CAPTURE wrote " + path);
        }
    }
}
