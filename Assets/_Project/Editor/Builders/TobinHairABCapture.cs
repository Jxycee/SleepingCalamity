// Same-camera, same-light A/B capture of Tobin's two hair variants (HairAB/Tobin_HairDeck.prefab vs HairAB/Tobin_AetherStrand.prefab,
// built by TobinHairABBuilder). For each variant, in a fresh empty scene:
//   stills  : front / 3/4 / back close-ups after a 1 s settle, in "day" (BeautyCornerBuilder Midday sun, ambient and grade values, copied)
//             and "torch" (sun off, dim ambient, warm point light front-left); "torchoff" shoots the torch setup with the torch disabled
//             (front only), so a hair-region diff shows whether the hair shader receives the point light at all (Forward+ check).
//   motion  : day light, wind 0 for both unless noted: headturn (yaw 0 -> 60 deg in 0.1 s, 3/4-back view), runstop (root at 5 m/s for 1 s,
//             stopped in 0.15 s, side view, root motion only, no run clip), idle (AetherStrand fallback wind 0.55 on, HairDeck has no wind; front).
//   log.txt : per motion frame, hair tip displacement from the head-rigid rest pose (mean / max cm, same measure for both solvers).
// Fixed 60 Hz steps calling each solver's Simulate directly (no LateUpdate in the editor).
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinHairABCapture.Run
//   env: SC_OUT_DIR (default ./hair_ab), SC_SIZE (default 640), SC_VARIANTS (hairdeck,aether), SC_LIGHTS (day,torch,torchoff), SC_PARTS (stills,motion)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AetherStrand;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.EditorTools
{
    public static class TobinHairABCapture
    {
        const float Dt = 1f / 60f;
        static string Env(string k, string d) { var v = System.Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        sealed class Rig
        {
            public GameObject go; public Transform head; public Quaternion head0; public TobinHairSpringChains chains; public AetherStrandPhysics phys;
            public Transform[] tips; public Vector3[] tipRest; public Renderer[] hairRenderers; float t;
            public void Step() { t += Dt; if (chains) chains.Simulate(Dt); if (phys) phys.Simulate(Dt, t); }
            public (float mean, float max) TipDisp()
            {
                float s = 0, m = 0;
                for (int i = 0; i < tips.Length; i++) { float d = (tips[i].position - head.TransformPoint(tipRest[i])).magnitude * 100f; s += d; m = Mathf.Max(m, d); }
                return (s / Mathf.Max(1, tips.Length), m);
            }
        }

        public static void Run()
        {
            int size = int.Parse(Env("SC_SIZE", "640"));
            string dir = Env("SC_OUT_DIR", Path.GetFullPath("hair_ab")); Directory.CreateDirectory(dir);
            var variants = Env("SC_VARIANTS", "hairdeck,aether").Split(',');
            var lights = Env("SC_LIGHTS", "day,torch,torchoff").Split(',');
            var parts = Env("SC_PARTS", "stills,motion").Split(',');
            var log = new StringBuilder(); var savedPipe = QualitySettings.renderPipeline; float savedShadow = QualitySettings.shadowDistance;
            try
            {
                QualitySettings.shadowDistance = 8f;
                foreach (var v in variants)
                {
                    if (parts.Contains("stills")) foreach (var l in lights) Shoot(v, l, size, dir, log, motion: null);
                    if (parts.Contains("motion")) foreach (var m in new[] { "headturn", "runstop", "idle" }) Shoot(v, "day", size, dir, log, motion: m);
                }
            }
            finally
            {
                QualitySettings.renderPipeline = savedPipe; QualitySettings.shadowDistance = savedShadow;
                File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString()); Debug.Log("SC HAIRAB CAPTURE\n" + log);
            }
        }

        static Rig Spawn(string variant)
        {
            string path = variant == "aether" ? TobinHairABBuilder.AetherPrefab : TobinHairABBuilder.HairDeckPrefab;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException(path);
            var r = new Rig { go = Object.Instantiate(src) };
            var anim = r.go.GetComponentInChildren<Animator>(); r.head = anim.GetBoneTransform(HumanBodyBones.Head); anim.enabled = false; r.head0 = r.head.localRotation;
            foreach (var smr in r.go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.updateWhenOffscreen = true; smr.forceMatrixRecalculationPerRender = true; }
            r.chains = r.go.GetComponentInChildren<TobinHairSpringChains>();
            if (r.chains) { r.chains.head = r.head; r.chains.Rebuild(); }
            r.phys = r.go.GetComponent<AetherStrandPhysics>();
            if (r.phys) { r.phys.fallbackWindStrength = 0f; r.phys.RebuildChains(); r.phys.ResetToRestPose(); }
            string prefix = variant == "aether" ? TobinHairABBuilder.AetherBonePrefix : "hair_";
            r.tips = r.head.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(prefix, System.StringComparison.Ordinal)
                       && !t.Cast<Transform>().Any(c => c.name.StartsWith(prefix, System.StringComparison.Ordinal))).ToArray();
            r.tipRest = r.tips.Select(t => r.head.InverseTransformPoint(t.position)).ToArray();
            r.hairRenderers = r.go.transform.Find("Hair").GetComponentsInChildren<Renderer>(true);
            return r;
        }

        static void Shoot(string variant, string light, int size, string dir, StringBuilder log, string motion)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = Spawn(variant);
            var cam = new GameObject("Cam").AddComponent<Camera>(); cam.fieldOfView = 22f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 50f; cam.clearFlags = CameraClearFlags.SolidColor;
            var ucd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = true; ucd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
            var vol = new GameObject("Volume").AddComponent<Volume>(); vol.isGlobal = true; var prof = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = prof;
            prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var ca = prof.Add<ColorAdjustments>(true); ca.contrast.Override(30f); ca.saturation.Override(0f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(54f, -32f, 0f);
            RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.fog = false;
            Light torch = null;
            if (light == "day")
            {   // BeautyCornerBuilder.ApplyPreset(Midday) values
                sun.color = new Color(1.0f, 0.91f, 0.83f); sun.intensity = 4.5f; ca.postExposure.Override(-0.55f);
                RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.64f); RenderSettings.ambientEquatorColor = new Color(0.47f, 0.48f, 0.49f); RenderSettings.ambientGroundColor = new Color(0.29f, 0.26f, 0.22f);
                cam.backgroundColor = new Color(0.70f, 0.78f, 0.90f);
            }
            else
            {
                sun.enabled = false; ca.postExposure.Override(0.2f);
                RenderSettings.ambientSkyColor = new Color(0.05f, 0.05f, 0.06f); RenderSettings.ambientEquatorColor = new Color(0.05f, 0.045f, 0.04f); RenderSettings.ambientGroundColor = new Color(0.03f, 0.025f, 0.02f);
                cam.backgroundColor = new Color(0.03f, 0.025f, 0.02f);
                torch = new GameObject("Torch").AddComponent<Light>(); torch.type = LightType.Point; torch.color = new Color(1f, 0.62f, 0.32f); torch.intensity = 3.0f; torch.range = 4f; torch.shadows = LightShadows.Soft;
                torch.transform.position = rig.head.position + new Vector3(-0.35f, 0.18f, 0.45f);
                torch.enabled = light != "torchoff";
            }
            cam.Render(); cam.Render(); cam.Render();   // warm-up (first renders of a fresh scene come out dark)

            void Snap(string name, float az, float el = 8f)
            {
                Vector3 c = rig.head.position + new Vector3(0, 0.07f, 0);
                cam.transform.position = c + Quaternion.Euler(-el, az, 0) * new Vector3(0, 0, 0.95f); cam.transform.LookAt(c);
                cam.Render(); cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            }

            if (motion == null)
            {
                for (int f = 0; f < 60; f++) rig.Step();
                var views = light == "torchoff" ? new[] { ("f", 0f) } : new[] { ("f", 0f), ("q", 40f), ("b", 180f) };
                foreach (var (vn, az) in views) Snap($"{variant}_{light}_{vn}", az);
                if (light == "torch" || light == "torchoff")
                {   // hair-only mask for the torch diff: same frame with the hair hidden
                    foreach (var r in rig.hairRenderers) r.enabled = false; Snap($"{variant}_{light}_f_nohair", 0f); foreach (var r in rig.hairRenderers) r.enabled = true;
                }
                log.AppendLine($"{variant} {light} stills tips {rig.tips.Length} disp {rig.TipDisp().mean:F2}/{rig.TipDisp().max:F2} cm");
            }
            else
            {
                if (motion == "idle" && rig.phys) rig.phys.fallbackWindStrength = 0.55f;   // the plugin's preset wind (HairDeck has none)
                for (int f = 0; f < 60; f++) rig.Step();
                var shots = new HashSet<int>(new[] { 0, 6, 12, 20, 30, 48, 72 });
                Transform root = rig.go.transform; Vector3 p0 = root.position; float speed = 0f; int runFrames = 60, total = motion == "runstop" ? runFrames + 73 : 73;
                int frameOffset = motion == "runstop" ? runFrames : 0; int k = 0;
                for (int f = 0; f < total; f++)
                {
                    float ts = f * Dt;
                    if (motion == "headturn") { float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ts / 0.1f)); rig.head.localRotation = rig.head0 * Quaternion.Euler(0, 60f * a, 0); }
                    else if (motion == "runstop") { speed = f < runFrames ? 5f : Mathf.Max(0f, speed - 5f / (0.15f / Dt)); root.position += Vector3.forward * speed * Dt + Vector3.up * (f < runFrames ? 0.03f * Mathf.Sin(ts * 2f * Mathf.PI * 2.6f) - 0.03f * Mathf.Sin((ts - Dt) * 2f * Mathf.PI * 2.6f) : 0f); }
                    else { rig.head.localRotation = rig.head0 * Quaternion.Euler(1.2f * Mathf.Sin(ts * 2f * Mathf.PI * 0.25f), 0, 0); }
                    rig.Step();
                    int rel = f - frameOffset; if (rel < 0 || !shots.Contains(rel)) continue;
                    var (mean, max) = rig.TipDisp();
                    log.AppendLine($"{variant} {motion} t+{rel * Dt:F2}s tipdisp mean {mean:F2} max {max:F2} cm");
                    float az = motion == "headturn" ? 145f : motion == "runstop" ? 90f : 0f;
                    Snap($"{variant}_{motion}_{k++}", az, motion == "headturn" ? 14f : 6f);
                }
            }
            rt.Release();
        }
    }
}
