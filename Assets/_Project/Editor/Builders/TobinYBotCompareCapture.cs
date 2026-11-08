// Capture-only (gate 2c): Tobin base and Y Bot side by side as neutral grey clay, posed by the same Humanoid clip at the same time, front three-quarter view.
// Throwaway scene (never saved), not wired into ProjectBuilder.RunAll. Both rigs are retargeted through their own Humanoid avatars (Animator + PlayableGraph).
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinYBotCompareCapture.CaptureAll -OutDir <scratch>
// Env: SC_CAPTURE_DIR = PNG folder (required), SC_GATE2C_ONLY = one row key (e.g. walk, overhead), SC_GATE2C_FRAMES = number of frames per clip (default 4, 1 for a proof run).
// Files: <key>_<n>.png, one tile per frame (Y Bot left, Tobin right); tiles.txt lists key, tile, clip frame and seconds. The sheet is assembled outside Unity.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class TobinYBotCompareCapture
    {
        const string Anim = "Assets/_Project/Content/Animation";
        const string Ual1 = Anim + "/Quaternius/UAL1_Standard.fbx";
        const string Mx = Anim + "/Mixamo";
        const int TileW = 600, TileH = 640;

        struct Row { public string Key, File, Clip; public int[] Frames; }

        // Frame numbers are the source-clip frames of the earlier Blender test (rig_v2 clips/*_frames.json); four of the six, spread across the clip.
        static readonly Row[] Rows =
        {
            new Row { Key = "walk",   File = Ual1, Clip = "Walk_Loop",         Frames = new[] { 1, 12, 21, 35 } },
            new Row { Key = "run",    File = Ual1, Clip = "Sprint_Loop",       Frames = new[] { 4, 9, 14, 18 } },
            new Row { Key = "sword",  File = Ual1, Clip = "Sword_Attack",      Frames = new[] { 7, 14, 19, 35 } },
            new Row { Key = "punch",  File = Ual1, Clip = "Punch_Cross",       Frames = new[] { 5, 10, 21, 27 } },
            new Row { Key = "sit",    File = Ual1, Clip = "Sitting_Idle_Loop", Frames = new[] { 1, 15, 18, 30 } },
            new Row { Key = "crouch", File = Ual1, Clip = "Crouch_Fwd_Loop",   Frames = new[] { 9, 26, 39, 53 } },
            new Row { Key = "jump",   File = Ual1, Clip = "Jump_Loop",         Frames = new[] { 13, 32, 43, 69 } },
            new Row { Key = "climb",  File = Mx + "/Climb.fbx", Clip = null,   Frames = new[] { 4, 14, 17, 23 } },
            new Row { Key = "cast",   File = Mx + "/Cast.fbx",  Clip = null,   Frames = new[] { 20, 45, 58, 133 } },
            new Row { Key = "warrior_idle", File = Mx + "/WarriorIdle.fbx", Clip = null, Frames = new[] { 151, 206, 218, 227 } },
        };

        public static void CaptureAll()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR to the folder for the PNGs");
            Directory.CreateDirectory(dir);
            string only = System.Environment.GetEnvironmentVariable("SC_GATE2C_ONLY");
            int nFrames = int.TryParse(System.Environment.GetEnvironmentVariable("SC_GATE2C_FRAMES"), out int nf) ? nf : 4;

            AnimationImportBuilder.Build();   // configures the Humanoid clip imports (new Climb/Cast copy the Y Bot avatar)
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            var log = new StringBuilder();
            GameObject ybot = null, tobin = null, rig = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.64f);
                RenderSettings.skybox = null;
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(42, 28, 0);

                var clay = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Clay" };
                clay.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.57f));
                clay.SetFloat("_Smoothness", 0.15f); clay.SetFloat("_Metallic", 0f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = new Vector3(4f, 1, 4f);
                floor.GetComponent<Renderer>().sharedMaterial = new Material(clay) { color = new Color(0.32f, 0.33f, 0.35f) };

                rig = new GameObject("Rig");
                Avatar yAvatar = null, tAvatar = null;
                void Respawn()   // fresh instances per tile: a reused Animator kept stale poses between clips
                {
                    if (ybot) Object.DestroyImmediate(ybot);
                    if (tobin) Object.DestroyImmediate(tobin);
                    ybot = Spawn(Mx + "/YBot.fbx", rig.transform, 0.62f, clay, out yAvatar);
                    tobin = Spawn(TobinBaseModelImporter.ModelPath, rig.transform, -0.62f, clay, out tAvatar);
                    foreach (var r in tobin.GetComponentsInChildren<Renderer>(true)) r.enabled = true;   // both BaseUnderlay meshes on
                    foreach (var t in tobin.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BaseUnderlay")) t.gameObject.SetActive(true);
                }
                Respawn();
                log.AppendLine("TOBIN RENDERERS " + string.Join(",", tobin.GetComponentsInChildren<Renderer>(true).Select(r => r.name + (r.enabled && r.gameObject.activeInHierarchy ? "" : "(off)"))));

                var cam = new GameObject("Cam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.76f, 0.80f);
                cam.fieldOfView = 28f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 40f;
                var rt = RenderTexture.GetTemporary(TileW, TileH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;

                foreach (Row row in Rows)
                {
                    if (!string.IsNullOrEmpty(only) && only != row.Key) continue;
                    AnimationClip clip = LoadClip(row);
                    if (clip == null) { log.AppendLine("MISSING " + row.Key + " " + row.File + " " + row.Clip + " available: " + string.Join(",", AssetDatabase.LoadAllAssetsAtPath(row.File).OfType<AnimationClip>().Select(c => c.name))); continue; }
                    log.AppendLine("CLIP " + row.Key + " " + clip.name + " len " + clip.length.ToString("F2") + " fps " + clip.frameRate + " from " + row.File);
                    for (int i = 0; i < Mathf.Min(nFrames, row.Frames.Length); i++)
                    {
                        float t = Mathf.Min((row.Frames[i] - 1) / clip.frameRate, clip.length);
                        Respawn();
                        PoseClip(ybot, yAvatar, clip, t, out var g1);
                        PoseClip(tobin, tAvatar, clip, t, out var g2);
                        Frame(cam, 35f, ybot, tobin); Shoot(cam, rt, Path.Combine(dir, row.Key + "_" + i + ".png"));
                        g1.Destroy(); g2.Destroy();
                        log.AppendLine("TILE " + row.Key + " " + i + " frame " + row.Frames[i] + " t " + t.ToString("F3") + " ybot hips " + Hips(ybot) + " tobin hips " + Hips(tobin));
                    }
                }
                if (string.IsNullOrEmpty(only) || only == "overhead")
                {
                    Respawn();
                    PoseOverhead(ybot, yAvatar); PoseOverhead(tobin, tAvatar);
                    float[] yaws = { 35f, 180f, 0f, 90f }; string[] names = { "three_quarter", "back", "front", "side" };
                    for (int i = 0; i < Mathf.Min(nFrames, 4); i++)
                    {
                        Frame(cam, yaws[i], ybot, tobin); Shoot(cam, rt, Path.Combine(dir, "overhead_" + i + ".png"));
                        log.AppendLine("TILE overhead " + i + " view " + names[i]);
                    }
                }
                RenderTexture.ReleaseTemporary(rt);
            }
            finally
            {
                QualitySettings.renderPipeline = savedQuality;
                File.WriteAllText(Path.Combine(dir, "tiles.txt"), log.ToString());
                Debug.Log("SC GATE2C\n" + log);
            }
        }

        static string Hips(GameObject go) { var a = go.GetComponent<Animator>(); return a.GetBoneTransform(HumanBodyBones.Hips).position.ToString("F2") + " lfoot " + a.GetBoneTransform(HumanBodyBones.LeftFoot).position.ToString("F2"); }

        static GameObject Spawn(string path, Transform parent, float x, Material clay, out Avatar avatar)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException(path);
            avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault() ?? throw new System.InvalidOperationException("no Avatar in " + path);
            var go = Object.Instantiate(src, parent);
            go.transform.localPosition = new Vector3(x, 0, 0);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.avatar = avatar; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = clay;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            return go;
        }

        // Clip file importers are set by AnimationImportBuilder (Quaternius, Mixamo folder); the clip is read from the model asset.
        static AnimationClip LoadClip(Row row)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(row.File).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            return row.Clip == null ? clips.FirstOrDefault() : clips.FirstOrDefault(c => c.name == row.Clip || c.name.EndsWith("|" + row.Clip));
        }

        static void PoseClip(GameObject go, Avatar avatar, AnimationClip clip, float time, out PlayableGraph graph)
        {
            var anim = go.GetComponent<Animator>();
            anim.Rebind();
            graph = PlayableGraph.Create("sc2c");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "o", anim);
            var cp = AnimationClipPlayable.Create(graph, clip);
            cp.SetApplyFootIK(false);
            output.SetSourcePlayable(cp);
            cp.SetTime(time);
            cp.Pause();
            graph.Evaluate(0f);
        }

        // Arms straight overhead from muscles: everything else keeps the model's own default (bind) muscles so legs, spine and forearms stay straight;
        // both arms Down-Up fully up, no front/back or twist, shoulders half raised.
        static void PoseOverhead(GameObject go, Avatar avatar)
        {
            var h = new HumanPoseHandler(avatar, go.transform);
            var pose = new HumanPose();
            h.GetHumanPose(ref pose);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string n = HumanTrait.MuscleName[i];
                if (n == "Left Arm Down-Up" || n == "Right Arm Down-Up") pose.muscles[i] = 1f;
                else if (n == "Left Arm Front-Back" || n == "Right Arm Front-Back" || n == "Left Arm Twist In-Out" || n == "Right Arm Twist In-Out") pose.muscles[i] = 0f;
                else if (n == "Left Shoulder Down-Up" || n == "Right Shoulder Down-Up") pose.muscles[i] = 0.5f;
            }
            h.SetHumanPose(ref pose);
            h.Dispose();
        }

        // Fits both characters (humanoid bone bounds plus a margin) so lifted poses (climb, jump) stay in frame; never closer than a standing figure.
        static void Frame(Camera cam, float yawDeg, GameObject a, GameObject b)
        {
            Bounds bb = default; bool first = true;
            foreach (var go in new[] { a, b })
            {
                var anim = go.GetComponent<Animator>();
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    var t = anim.GetBoneTransform((HumanBodyBones)i);
                    if (t == null) continue;
                    if (first) { bb = new Bounds(t.position, Vector3.zero); first = false; } else bb.Encapsulate(t.position);
                }
            }
            bb.Encapsulate(new Vector3(bb.center.x, 0f, bb.center.z)); bb.Encapsulate(new Vector3(bb.center.x, 1.95f, bb.center.z));
            bb.Expand(new Vector3(0.5f, 0.3f, 0.5f));
            float aspect = (float)TileW / TileH;
            float halfV = Mathf.Max(bb.extents.y, bb.extents.x * 1.1f / aspect, 1.15f);
            float dist = halfV / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + 0.5f;
            cam.transform.position = bb.center + Quaternion.Euler(8f, yawDeg, 0) * new Vector3(0, 0, dist);   // yaw 0 = in front of characters facing +Z
            cam.transform.LookAt(bb.center);
        }

        static void Shoot(Camera cam, RenderTexture rt, string file)
        {
            for (int i = 0; i < 3; i++) cam.Render();   // warm-up: shadows / SSAO history
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
