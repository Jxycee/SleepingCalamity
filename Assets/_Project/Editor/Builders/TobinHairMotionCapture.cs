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
        const int Size = 420;

        public static void Run()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_OUT_DIR"); if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath("hair_capture");
            Directory.CreateDirectory(dir); var log = new StringBuilder(); var savedQuality = QualitySettings.renderPipeline;
            try
            {
                var baseSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TobinBaseModelImporter.ModelPath) ?? throw new FileNotFoundException(TobinBaseModelImporter.ModelPath);
                var hairSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TobinHairMeshImporter.ModelPath) ?? throw new FileNotFoundException(TobinHairMeshImporter.ModelPath);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(TobinBaseModelImporter.ModelPath).OfType<Avatar>().First();
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.64f);
                var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(42, 28, 0);
                var skin = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Skin" }; skin.SetColor("_BaseColor", new Color(0.72f, 0.58f, 0.5f)); skin.SetFloat("_Smoothness", 0.2f);
                var hairM = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hair" }; hairM.SetColor("_BaseColor", new Color(0.32f, 0.19f, 0.1f)); hairM.SetFloat("_Smoothness", 0.35f);

                var tobin = Object.Instantiate(baseSrc); var anim = tobin.GetComponent<Animator>() ?? tobin.AddComponent<Animator>(); anim.avatar = avatar;
                foreach (var r in tobin.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = skin;
                var realHead = anim.GetBoneTransform(HumanBodyBones.Head);
                var hair = Object.Instantiate(hairSrc);
                foreach (var r in hair.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = hairM;
                var hairHead = hair.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                log.AppendLine("realHead " + realHead.position.ToString("F3") + " hairHead " + hairHead.position.ToString("F3") + " delta mm " + ((realHead.position - hairHead.position).magnitude * 1000f).ToString("F2"));
                hairHead.SetParent(realHead, true);
                var chains = hair.AddComponent<TobinHairSpringChains>(); chains.head = realHead; chains.Rebuild();
                log.AppendLine("chains " + chains.ChainCount + " skinned renderers " + hair.GetComponentsInChildren<SkinnedMeshRenderer>().Length + " hair bones " + hair.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("hair_")));

                var cam = new GameObject("Cam").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.76f, 0.80f); cam.fieldOfView = 18f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); cam.targetTexture = rt;
                Vector3 centre = realHead.position + new Vector3(0, 0.07f, 0);
                var rot0 = realHead.localRotation; const float dt = 1f / 30f; int[] shoot = { 0, 14, 28, 42, 56, 83 }; string[] names = { "rest", "yawL", "mid", "yawR", "nod", "settle" };
                for (int f = 0; f < 84; f++)
                {
                    float t = f / 84f; float yaw = 32f * Mathf.Sin(2 * Mathf.PI * t * 1.5f), nod = 10f * Mathf.Sin(2 * Mathf.PI * t * 3f);
                    realHead.localRotation = rot0 * Quaternion.Euler(nod, yaw, 0);
                    chains.Simulate(dt);
                    int si = System.Array.IndexOf(shoot, f); if (si < 0) continue;
                    foreach (var (nm, az) in new[] { ("f", 0f), ("q", 40f) })
                    {
                        cam.transform.position = centre + Quaternion.Euler(0, az, 0) * new Vector3(0, 0, 2.2f); cam.transform.LookAt(centre);
                        cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
                        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply(); RenderTexture.active = prev;
                        File.WriteAllBytes(Path.Combine(dir, names[si] + "_" + nm + ".png"), tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                    }
                    log.AppendLine("frame " + f + " " + names[si] + " headRot " + realHead.eulerAngles.ToString("F1"));
                }
                rt.Release();
            }
            finally { QualitySettings.renderPipeline = savedQuality; File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString()); Debug.Log("SC HAIRCAP\n" + log); }
        }
    }
}
