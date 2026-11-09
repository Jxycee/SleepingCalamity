// Capture-only (gate 2d): the textured Tobin base face, head and shoulders at about 1 m (50 mm, 36 mm sensor), simple 3-point light, one 1024 px tile per expression.
// Throwaway scene (never saved), not wired into ProjectBuilder.RunAll. Shapes and eyes are driven through TobinFacePlayback, the same component the game uses.
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinFaceGate2dCapture.CaptureAll -OutDir <scratch>
// Env: SC_CAPTURE_DIR = PNG folder (required), SC_GATE2D_ONLY = one tile key (proof run). Files: <key>.png and tiles.txt (key | label). The labelled sheet is assembled outside Unity.
// Render pipeline: uses whichever pipeline the active quality level already has; only if none is set does it borrow the BeautyCorner asset, and it restores the previous value
// without touching ProjectSettings (QualitySettings is only written back when it was changed).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SleepingCalamity.Presentation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class TobinFaceGate2dCapture
    {
        const int Tile = 1024;

        sealed class Tileset
        {
            public string Key, Label; public System.Action<TobinFacePlayback, GameObject> Pose; public float Yaw;
        }

        static void V(TobinFacePlayback f, string shape) => f.SetViseme(shape, 1f);

        static List<Tileset> Tiles(Transform lookTarget, Vector3 eyeMid)
        {
            void Look(TobinFacePlayback f, float x, float y) { lookTarget.position = eyeMid + new Vector3(x, y, 1.0f); f.lookTarget = lookTarget; f.Tick(0.3f); }
            return new List<Tileset>
            {
                new Tileset { Key = "neutral",  Label = "neutral", Pose = (f, g) => { } },
                new Tileset { Key = "blink",    Label = "eyeBlinkLeft/Right = 1", Pose = (f, g) => f.blinkOverride = 1f },
                new Tileset { Key = "jaw05",    Label = "jawOpen 0.5 (cap)", Pose = (f, g) => f.SetJawOpen(0.5f) },
                new Tileset { Key = "jaw10",    Label = "jawOpen 1.0 (raw shape, over the cap)", Pose = (f, g) => f.SetShape("jawOpen", 1f) },
                new Tileset { Key = "aa",       Label = "viseme aa = 1", Pose = (f, g) => V(f, "aa") },
                new Tileset { Key = "ih",       Label = "viseme ih = 1", Pose = (f, g) => V(f, "ih") },
                new Tileset { Key = "ou",       Label = "viseme ou = 1", Pose = (f, g) => V(f, "ou") },
                new Tileset { Key = "E",        Label = "viseme E = 1", Pose = (f, g) => V(f, "E") },
                new Tileset { Key = "oh",       Label = "viseme oh = 1", Pose = (f, g) => V(f, "oh") },
                new Tileset { Key = "smile",    Label = "mouthSmileLeft/Right = 1", Pose = (f, g) => { f.SetShape("mouthSmileLeft", 1f); f.SetShape("mouthSmileRight", 1f); } },
                new Tileset { Key = "lookL",    Label = "eyes look left (bones, 25 deg)", Pose = (f, g) => Look(f, 0.47f, 0f) },
                new Tileset { Key = "lookR",    Label = "eyes look right (bones, 25 deg)", Pose = (f, g) => Look(f, -0.47f, 0f) },
                new Tileset { Key = "lookU",    Label = "eyes look up (bones, 20 deg)", Pose = (f, g) => Look(f, 0f, 0.36f) },
                new Tileset { Key = "tq",       Label = "neutral, 3/4 view", Pose = (f, g) => { }, Yaw = 40f },
            };
        }

        public static void CaptureAll()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR to the folder for the PNGs");
            Directory.CreateDirectory(dir);
            string only = System.Environment.GetEnvironmentVariable("SC_GATE2D_ONLY");

            TobinBaseMaterialBuilder.Build();
            AssetDatabase.Refresh();

            RenderPipelineAsset borrowed = null, previous = QualitySettings.renderPipeline;
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                borrowed = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
                QualitySettings.renderPipeline = borrowed;
            }
            var log = new StringBuilder();
            log.AppendLine("PIPELINE " + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "none") + (borrowed ? " (borrowed)" : " (active)"));
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.30f, 0.31f, 0.33f);
                RenderSettings.skybox = null;
                // Key: front-left above, soft shadows. Fill: front-right, low, no shadows. Rim: behind-right, picks out the jaw and shoulders.
                Light L(string n, Vector3 euler, float intensity, Color c, LightShadows sh)
                {
                    var l = new GameObject(n).AddComponent<Light>();
                    l.type = LightType.Directional; l.intensity = intensity; l.color = c; l.shadows = sh;
                    l.transform.rotation = Quaternion.Euler(euler);
                    return l;
                }
                L("Key", new Vector3(30, 150, 0), 1.7f, new Color(1f, 0.95f, 0.88f), LightShadows.Soft);
                L("Fill", new Vector3(10, -140, 0), 0.55f, new Color(0.80f, 0.88f, 1f), LightShadows.None);
                L("Rim", new Vector3(25, -20, 0), 0.9f, Color.white, LightShadows.None);

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(TobinBaseModelImporter.ModelPath) ?? throw new FileNotFoundException(TobinBaseModelImporter.ModelPath);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(TobinBaseModelImporter.ModelPath).OfType<Avatar>().FirstOrDefault();

                var cam = new GameObject("Cam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.30f, 0.32f, 0.35f);
                cam.usePhysicalProperties = true; cam.sensorSize = new Vector2(36f, 36f); cam.focalLength = 50f;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
                var rt = RenderTexture.GetTemporary(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                var target = new GameObject("LookTarget").transform;

                var labels = new StringBuilder();
                bool first = true;
                var keys = Tiles(target, Vector3.zero).Select(t => t.Key).ToList();
                foreach (string key in keys)
                {
                    if (!string.IsNullOrEmpty(only) && only != key) continue;
                    var go = Object.Instantiate(src);
                    go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                    if (avatar != null) { var an = go.GetComponent<Animator>() ?? go.AddComponent<Animator>(); an.avatar = avatar; an.enabled = false; }
                    string bound = TobinBaseMaterialBuilder.Apply(go);
                    foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;
                    var face = go.AddComponent<TobinFacePlayback>();
                    face.autoBlink = false; face.saccadeAmplitudeDeg = 0f; face.Init();
                    if (first) { log.AppendLine("BOUND\n" + bound); log.AppendLine("SHAPES " + face.ShapeNames.Count() + " eyes " + (face.leftEye ? face.leftEye.name : "none") + "/" + (face.rightEye ? face.rightEye.name : "none")); first = false; }

                    Vector3 eyeMid = (face.leftEye.position + face.rightEye.position) * 0.5f;
                    var tile = Tiles(target, eyeMid).First(t => t.Key == key);
                    tile.Pose(face, go);
                    face.Tick(0.3f);
                    face.Tick(0.3f);   // apply blink override and settle eyes

                    // 1.0 m from the eyes, aimed a little below them so the shoulders show.
                    Vector3 aim = eyeMid + new Vector3(0f, -0.09f, 0f);
                    cam.transform.position = aim + Quaternion.Euler(0f, tile.Yaw, 0f) * new Vector3(0f, 0.0f, 1.0f);
                    cam.transform.LookAt(aim);
                    Shoot(cam, rt, Path.Combine(dir, key + ".png"));
                    labels.AppendLine(key + "|" + tile.Label);
                    log.AppendLine("TILE " + key + " eyeMid " + eyeMid.ToString("F3") + " camDist " + Vector3.Distance(cam.transform.position, eyeMid).ToString("F2"));
                    Object.DestroyImmediate(go);
                }
                File.WriteAllText(Path.Combine(dir, "labels.txt"), labels.ToString());
                RenderTexture.ReleaseTemporary(rt);
            }
            finally
            {
                if (borrowed != null) QualitySettings.renderPipeline = previous;   // only written back when we changed it
                File.WriteAllText(Path.Combine(dir, "tiles.txt"), log.ToString());
                Debug.Log("SC GATE2D\n" + log);
            }
        }

        static void Shoot(Camera cam, RenderTexture rt, string file)
        {
            for (int i = 0; i < 3; i++) cam.Render();   // warm-up: shadows
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
