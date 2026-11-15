// Capture of Tobin's spring-chain hair: spawns TobinBase + TobinHairMesh, re-parents the hair rig's Head bone under TobinBase's Head bone,
// yaws/nods the head over a fixed-step loop calling TobinHairSpringChains.Simulate, and writes PNG tiles + a log (counts, bone check).
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinHairMotionCapture.Run   (output dir: env SC_OUT_DIR, default ./hair_capture)
using System.IO;
using System.Linq;
using System.Text;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class TobinHairMotionCapture
    {
        static int Size = 420;

        public static void Run()
        {
            { var sz = System.Environment.GetEnvironmentVariable("SC_SIZE"); if (!string.IsNullOrEmpty(sz)) Size = int.Parse(sz); }
            string dir = System.Environment.GetEnvironmentVariable("SC_OUT_DIR"); if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath("hair_capture");
            Directory.CreateDirectory(dir); var log = new StringBuilder(); var savedQuality = QualitySettings.renderPipeline; float savedShadowDist = QualitySettings.shadowDistance;
            try
            {
                var baseSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TobinBaseModelImporter.ModelPath) ?? throw new FileNotFoundException(TobinBaseModelImporter.ModelPath);
                var hairSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TobinHairMeshImporter.ModelPath) ?? throw new FileNotFoundException(TobinHairMeshImporter.ModelPath);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(TobinBaseModelImporter.ModelPath).OfType<Avatar>().First();
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.64f);
                bool hq = System.Environment.GetEnvironmentVariable("SC_HQ") == "1";
                Light MakeLight(string n, float intensity, Color c, Vector3 euler, bool shadows) { var l = new GameObject(n).AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.color = c; l.transform.rotation = Quaternion.Euler(euler); l.shadows = shadows ? LightShadows.Soft : LightShadows.None; l.shadowStrength = 0.85f; return l; }
                if (hq)
                {   // three-point: warm key (front-left, above), cool fill (right), rim from behind-above
                    MakeLight("Key", 2.1f, new Color(1f, 0.93f, 0.84f), new Vector3(32, 28, 0), true);
                    MakeLight("Fill", 0.55f, new Color(0.78f, 0.86f, 1f), new Vector3(12, -62, 0), false);
                    MakeLight("Rim", 1.3f, new Color(1f, 0.97f, 0.92f), new Vector3(38, 200, 0), true);
                    RenderSettings.ambientLight = new Color(0.40f, 0.37f, 0.34f);
                }
                else MakeLight("Sun", 1.3f, Color.white, new Vector3(42, 28, 0), false);
                var skin = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Skin" }; skin.SetColor("_BaseColor", new Color(0.72f, 0.58f, 0.5f)); skin.SetFloat("_Smoothness", 0.2f);
                var hairM = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hair" }; hairM.SetColor("_BaseColor", new Color(0.32f, 0.19f, 0.1f)); hairM.SetFloat("_Smoothness", 0.35f);

                var tobin = Object.Instantiate(baseSrc); var anim = tobin.GetComponent<Animator>() ?? tobin.AddComponent<Animator>(); anim.avatar = avatar;
                bool real = System.Environment.GetEnvironmentVariable("SC_REAL") == "1";   // SC_REAL=1 keeps TobinBase's own materials
                if (real) { TobinBaseMaterialBuilder.Build(); log.AppendLine("bound: " + TobinBaseMaterialBuilder.Apply(tobin)); }
                else foreach (var r in tobin.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = skin;
                foreach (var t in tobin.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BaseUnderlay")) t.gameObject.SetActive(true);
                var realHead = anim.GetBoneTransform(HumanBodyBones.Head);
                anim.enabled = false;
                foreach (var smr in tobin.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.updateWhenOffscreen = true; smr.forceMatrixRecalculationPerRender = true; log.AppendLine("SMR " + smr.name + " bones " + smr.bones.Length + " hasHead " + smr.bones.Contains(realHead) + " root " + (smr.rootBone ? smr.rootBone.name : "null")); }
                log.AppendLine("realHead name " + realHead.name + " path " + string.Join("/", realHead.GetComponentsInParent<Transform>().Select(t => t.name).Reverse()));
                var hair = Object.Instantiate(hairSrc);
                if (real)
                {
                    log.AppendLine("hair mat " + TobinHairMaterialBuilder.Apply(hair));
                    string mv = System.Environment.GetEnvironmentVariable("SC_MATVAR");   // material experiments on a copy (asset untouched): nonorm | nomr | nonorm,nomr
                    if (!string.IsNullOrEmpty(mv))
                        foreach (var r in hair.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r.name.Contains("HairCap")) continue;
                            var mi = new Material(r.sharedMaterial);
                            if (mv.Contains("cullback")) mi.SetFloat("_Cull", 2f);
                            if (mv.Contains("cullfront")) mi.SetFloat("_Cull", 1f);
                            if (mv.Contains("nonorm")) { mi.SetTexture("_BumpMap", null); mi.DisableKeyword("_NORMALMAP"); }
                            if (mv.Contains("nomr")) { mi.SetTexture("_MetallicGlossMap", null); mi.DisableKeyword("_METALLICSPECGLOSSMAP"); mi.SetFloat("_Metallic", 0f); mi.SetFloat("_Smoothness", 0.35f); }
                            r.sharedMaterial = mi; log.AppendLine("matvar " + mv);
                        }
                } else foreach (var r in hair.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = hairM;
                var hairHead = hair.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                log.AppendLine("realHead " + realHead.position.ToString("F3") + " hairHead " + hairHead.position.ToString("F3") + " delta mm " + ((realHead.position - hairHead.position).magnitude * 1000f).ToString("F2"));
                hairHead.SetParent(realHead, true);
                var chains = hair.AddComponent<TobinHairSpringChains>(); chains.head = realHead; if (System.Environment.GetEnvironmentVariable("SC_HAIRSHADOW") == "1") chains.hairReceivesShadows = true; chains.Rebuild();   // SC_HAIRSHADOW=1: hair receives (self) shadows
                string sw = System.Environment.GetEnvironmentVariable("SC_W"); if (!string.IsNullOrEmpty(sw)) chains.weight = float.Parse(sw, System.Globalization.CultureInfo.InvariantCulture);   // 0 = rigid hair test
                string ss = System.Environment.GetEnvironmentVariable("SC_STIFF"); if (!string.IsNullOrEmpty(ss)) chains.stiffness = float.Parse(ss, System.Globalization.CultureInfo.InvariantCulture);
                log.AppendLine("chains " + chains.ChainCount + " skinned renderers " + hair.GetComponentsInChildren<SkinnedMeshRenderer>().Length + " hair bones " + hair.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("hair_")));

                if (System.Environment.GetEnvironmentVariable("SC_NOSHAD") == "1") { foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None; log.AppendLine("shadows off"); }
                var cam = new GameObject("Cam").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.76f, 0.80f); cam.fieldOfView = hq ? 9f : 14f; { var fv = System.Environment.GetEnvironmentVariable("SC_FOV"); if (!string.IsNullOrEmpty(fv)) cam.fieldOfView = float.Parse(fv, System.Globalization.CultureInfo.InvariantCulture); }   // SC_FOV widens the frame (taller hair) cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = hq ? 8 : 1 }; cam.targetTexture = rt;
                if (hq)
                {
                    var ucd = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ucd.renderPostProcessing = true; ucd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    var vol = new GameObject("Volume").AddComponent<Volume>(); vol.isGlobal = true; var prof = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = prof;
                    var tm = prof.Add<UnityEngine.Rendering.Universal.Tonemapping>(true); tm.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
                    var bl = prof.Add<UnityEngine.Rendering.Universal.Bloom>(true); bl.intensity.Override(0.18f); bl.threshold.Override(1.1f);
                    var ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true); ca.postExposure.Override(0.15f); ca.contrast.Override(8f); ca.saturation.Override(6f);
                    QualitySettings.shadowDistance = 6f;
                }
                cam.Render(); cam.Render(); cam.Render();   // warm-up: the first renders of a fresh scene came out dark / glossy (textures and shaders still loading)
                Vector3 centre = realHead.position + new Vector3(0, hq ? 0.075f : 0.07f, 0);
                var rot0 = realHead.localRotation; const float dt = 1f / 30f; int[] shoot = hq ? new[] { 0, 14, 28, 42, 56, 83 } : new[] { 0, 14, 28, 42, 56, 83 }; string[] names = { "rest", "yawL", "mid", "yawR", "nod", "settle" };
                // SC_CAPTURE_ONLY=rest_f,rest_s,rest_b (tile names) or a pose name (rest) shoots only those tiles; SC_HAIRMASK=1 also writes <tile>_nohair.png (hair hidden) for a hair-region mask
                var only = (System.Environment.GetEnvironmentVariable("SC_CAPTURE_ONLY") ?? "").Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                bool hairMask = System.Environment.GetEnvironmentVariable("SC_HAIRMASK") == "1";
                int lastFrame = 83; if (only.Length > 0) { lastFrame = -1; for (int k = 0; k < shoot.Length; k++) if (only.Any(o => o == names[k] || o.StartsWith(names[k] + "_"))) lastFrame = System.Math.Max(lastFrame, shoot[k]); }
                for (int f = 0; f <= lastFrame; f++)
                {
                    float t = f / 84f; float yaw = 32f * Mathf.Sin(2 * Mathf.PI * t * 1.5f), nod = 10f * Mathf.Sin(2 * Mathf.PI * t * 3f);
                    realHead.localRotation = rot0 * Quaternion.Euler(nod, yaw, 0);
                    chains.Simulate(dt);
                    int si = System.Array.IndexOf(shoot, f); if (si < 0) continue;
                    foreach (var (nm, az) in new[] { ("f", 0f), ("q", 40f), ("s", 90f), ("b", 180f) })
                    {
                        if (only.Length > 0 && !only.Any(o => o == names[si] || o == names[si] + "_" + nm)) continue;
                        cam.transform.position = centre + Quaternion.Euler(0, az, 0) * new Vector3(0, 0, 2.2f); cam.transform.LookAt(centre);
                        cam.Render(); cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply(); RenderTexture.active = prev;
                        File.WriteAllBytes(Path.Combine(dir, names[si] + "_" + nm + ".png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                        if (hairMask)
                        {
                            hair.SetActive(false); cam.Render(); cam.Render(); var pv2 = RenderTexture.active; RenderTexture.active = rt;
                            var t2 = new Texture2D(Size, Size, TextureFormat.RGB24, false); t2.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); t2.Apply(); RenderTexture.active = pv2;
                            File.WriteAllBytes(Path.Combine(dir, names[si] + "_" + nm + "_nohair.png"), t2.EncodeToPNG()); Object.DestroyImmediate(t2); hair.SetActive(true);
                        }
                    }
                    log.AppendLine("frame " + f + " " + names[si] + " headRot " + realHead.eulerAngles.ToString("F1"));
                }
                rt.Release();
            }
            finally { QualitySettings.renderPipeline = savedQuality; QualitySettings.shadowDistance = savedShadowDist; File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString()); Debug.Log("SC HAIRCAP\n" + log); }
        }
    }
}
