// Capture-only: Tobin base with strand hair, still views and a head-turn motion strip. Throwaway scene (never saved), not wired into RunAll.
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinHairCapture.CaptureStills|CaptureMotion -OutDir <scratch>
// Env: SC_CAPTURE_DIR = PNG folder (required), SC_HAIR_PROOF = 1 renders one tile only. The sim is driven by hand with fixed 1/30 s steps (no play loop in batch mode);
// the sheets are assembled outside Unity from tiles.txt.
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.DemoTeam.Hair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SleepingCalamity.Presentation;
using Debug = UnityEngine.Debug;

namespace SleepingCalamity.EditorTools
{
    public static class TobinHairCapture
    {
        const int TileW = 600, TileH = 640;
        static float Hz = 60f;
        static float Dt => 1f / Hz;

        static StringBuilder log;
        static float stepMs, stepMsMax; static int stepCount;

        static void Step(HairInstance hair, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var sw = Stopwatch.StartNew();
                using (var cmd = HairSimUtility.ScopedCommandBuffer.Get())
                {
                    if (hair.DispatchUpdate(cmd, CommandBufferExecutionFlags.None, Dt)) Graphics.ExecuteCommandBuffer(cmd);
                    else log.AppendLine("DISPATCH FALSE");
                }
                sw.Stop();
                float ms = (float)sw.Elapsed.TotalMilliseconds; stepMs += ms; stepMsMax = Mathf.Max(stepMsMax, ms); stepCount++;
            }
        }

        public static void CaptureStills() => Run(false);
        public static void CaptureMotion() => Run(true);

        static void Run(bool motion)
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR");
            Directory.CreateDirectory(dir);
            bool proof = System.Environment.GetEnvironmentVariable("SC_HAIR_PROOF") == "1";
            log = new StringBuilder();
            if (float.TryParse(System.Environment.GetEnvironmentVariable("SC_HAIR_HZ"), out float hz) && hz > 0) Hz = hz;
            ShaderUtil.allowAsyncCompilation = false;   // batch mode never finishes async shader compiles; the hair graph would stay on the placeholder
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset saved = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            RenderTexture rt = null;
            try
            {
                log.AppendLine("GRAPHICS " + SystemInfo.graphicsDeviceType + " " + SystemInfo.graphicsDeviceName + " compute " + SystemInfo.supportsComputeShaders);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);
                RenderSettings.skybox = null;
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(38, 200, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = new Vector3(2f, 1, 2f);
                floor.GetComponent<Renderer>().sharedMaterial = TobinHairBuilder.Clay("Floor", new Color(0.32f, 0.33f, 0.35f));

                var go = TobinHairBuilder.SpawnCharacter(null, out Transform head, out HairInstance hair);
                hair.settingsExecutive.updateSimulationRate = HairInstance.SettingsExecutive.UpdateRate.CustomTimeStep; hair.settingsExecutive.updateTimeStep = Dt;
                var sway = go.AddComponent<TobinHeadSway>(); sway.autoSway = false; sway.head = head; sway.CacheBase();

                var cam = new GameObject("Cam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.76f, 0.80f);
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
                rt = RenderTexture.GetTemporary(TileW, TileH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;

                cam.Render();   // first render sets the current pipeline, which the hair default material lookup needs
                Step(hair, 1);
                var mr = hair.GetComponentsInChildren<MeshRenderer>(true);
                var waitSw = Stopwatch.StartNew();   // the hair graph compiles its passes asynchronously; keep stepping until the placeholder shader is gone (bounded wait)
                while (mr.Length > 0 && mr[0].sharedMaterial != null && mr[0].sharedMaterial.shader.name.Contains("Replace") && waitSw.Elapsed.TotalSeconds < 240)
                {
                    System.Threading.Thread.Sleep(500);
                    Step(hair, 1);
                    mr = hair.GetComponentsInChildren<MeshRenderer>(true);
                }
                log.AppendLine("SHADER WAIT " + waitSw.Elapsed.TotalSeconds.ToString("F1") + " s");
                log.AppendLine("HAIR RENDERERS " + mr.Length + " head scale " + head.lossyScale);
                foreach (var r in mr) log.AppendLine("  " + r.name + " mat " + (r.sharedMaterial ? r.sharedMaterial.name + " shader " + r.sharedMaterial.shader.name : "null") + " bounds " + r.bounds);
                if (hair.solverData != null && hair.solverData.Length > 0) log.AppendLine("SOLVER groups " + hair.solverData.Length);
                Step(hair, Mathf.RoundToInt(1.5f * Hz));   // settle at rest
                sway.Apply(0f);

                Vector3 headC = head.position + new Vector3(0, 0.05f, 0);
                if (!motion)
                {
                    float[] yaws = { 0f, 40f, 90f, 180f }; string[] names = { "front", "threequarter", "side", "back" };
                    for (int d = 0; d < 2; d++)
                    {
                        for (int i = 0; i < yaws.Length; i++)
                        {
                            if (proof && (d != 0 || i != 1)) continue;
                            if (d == 0) Aim(cam, headC, yaws[i], 1.0f, 30f, 4f);
                            else Aim(cam, new Vector3(0, 1.0f, 0), yaws[i], 5.0f, 28f, 8f);
                            Step(hair, 1);
                            Shoot(cam, rt, Path.Combine(dir, (d == 0 ? "near_" : "far_") + names[i] + ".png"));
                            log.AppendLine("TILE " + (d == 0 ? "near_" : "far_") + names[i]);
                        }
                    }
                }
                else
                {
                    Aim(cam, headC, 40f, 1.0f, 30f, 4f);
                    float maxYaw = float.TryParse(System.Environment.GetEnvironmentVariable("SC_HAIR_MAXYAW"), out float my) ? my : 60f;
                    float[] frameTimes = { 0f, 0.1f, 0.2f, 0.4f, 0.8f, 1.6f };
                    int fi = 0;
                    int total = Mathf.RoundToInt(1.6f * Hz);
                    for (int step = 0; step <= total && fi < frameTimes.Length; step++)
                    {
                        float t = step * Dt;
                        float yaw = maxYaw * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f));
                        sway.Apply(yaw);
                        Step(hair, 1);   // at step 0 this is a zero-motion step: the strands only draw after an update
                        if (Mathf.Abs(t - frameTimes[fi]) < 0.5f * Dt)
                        {
                            Shoot(cam, rt, Path.Combine(dir, "motion_" + fi + ".png"));
                            log.AppendLine("TILE motion_" + fi + " t " + t.ToString("F2") + " yaw " + yaw.ToString("F1"));
                            fi++;
                        }
                        if (proof && fi >= 1) break;
                    }
                }
                log.AppendLine("SIM STEP dispatch ms avg " + (stepMs / Mathf.Max(1, stepCount)).ToString("F2") + " max " + stepMsMax.ToString("F2") + " steps " + stepCount + " (CPU-side recording only, not GPU time)");
            }
            finally
            {
                if (rt) RenderTexture.ReleaseTemporary(rt);
                QualitySettings.renderPipeline = saved;
                File.WriteAllText(Path.Combine(dir, motion ? "motion_tiles.txt" : "tiles.txt"), log.ToString());
                Debug.Log("SC HAIRCAP\n" + log);
            }
        }

        // Character faces +Z; yaw 0 puts the camera in front.
        static void Aim(Camera cam, Vector3 center, float yawDeg, float dist, float fov, float pitchDeg)
        {
            cam.fieldOfView = fov;
            cam.transform.position = center + Quaternion.Euler(pitchDeg, yawDeg, 0) * new Vector3(0, 0, dist);
            cam.transform.LookAt(center);
        }

        static void Shoot(Camera cam, RenderTexture rt, string file)
        {
            for (int i = 0; i < 3; i++) cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            RenderTexture.active = prev;
        }
    }
}
