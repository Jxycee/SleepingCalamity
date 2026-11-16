// Capture-only (not in ProjectBuilder.RunAll, changes no asset): renders each rigged enemy prefab's model from its front (+z) and from the side in the Idle pose, then each attack at
// its tell peak and at the middle of its strike window (driven by normalizedTime = frame / TotalFrames, the way EnemyAnimatorDriver places it), into one sheet per enemy.
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.EnemyPoseCapture.CaptureAll -OutDir <scratch>   (env SC_CAPTURE_DIR = PNG folder)
using System.IO;
using SleepingCalamity.Core.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class EnemyPoseCapture
    {
        const int Tile = 480;

        public static void CaptureAll()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR to the folder for the PNGs");
            Directory.CreateDirectory(dir);
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset saved = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.6f; sun.color = new Color(1f, 0.95f, 0.85f);
                sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
                floor.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get("M_GreyboxGround");
                var camGo = new GameObject("Cam");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.15f, 0.16f); cam.fieldOfView = 30f;
                foreach (EnemyArtBuilder.Spec spec in EnemyArtBuilder.Specs) Sheet(spec, cam, dir);
            }
            finally { QualitySettings.renderPipeline = saved; }
        }

        static void Sheet(EnemyArtBuilder.Spec spec, Camera cam, string dir)
        {
            GameObject fbx = EnemyArtBuilder.Model(spec.Kind);
            RuntimeAnimatorController ctrl = EnemyArtBuilder.ControllerOf(spec.Kind);
            if (fbx == null || ctrl == null) return;
            EnemyDefinition def = EnemyCatalog.Get(spec.Kind);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl; animator.applyRootMotion = false;
            AnimationClip[] clips = EnemyArtBuilder.Clips(spec);

            var tiles = new System.Collections.Generic.List<Texture2D>();
            void Pose(string state, float normalized, bool side)
            {
                // The clip is sampled straight onto the transforms (an edit-mode Animator.Update leaves skinned meshes in the bind pose); 1 frame == 1 AttackFrame.
                AnimationClip clip = System.Array.Find(clips, c => c.name == state);
                clip.SampleAnimation(go, normalized * clip.length);
                float h = Mathf.Max(def.Height, 1.2f), d = h * 3.4f;
                Vector3 target = new Vector3(0f, def.Height * 0.5f, 0f);
                cam.transform.position = side ? target + new Vector3(d, 0.2f, 0f) : target + new Vector3(0.6f, 0.3f, d);
                cam.transform.LookAt(target);
                // An edit-mode scene does not re-skin between a sample and a render, so each skinned mesh is baked at this pose and drawn as a plain mesh.
                var baked = new System.Collections.Generic.List<GameObject>();
                var hidden = new System.Collections.Generic.List<SkinnedMeshRenderer>();
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    smr.BakeMesh(mesh, true);
                    var g = new GameObject("baked_" + smr.name);
                    g.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    g.AddComponent<MeshFilter>().sharedMesh = mesh;
                    g.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                    baked.Add(g); smr.enabled = false; hidden.Add(smr);
                }
                tiles.Add(Render(cam));
                foreach (GameObject g in baked) { Object.DestroyImmediate(g.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(g); }
                foreach (SkinnedMeshRenderer smr in hidden) smr.enabled = true;
            }
            Pose("Idle", 0f, false);
            Pose("Idle", 0f, true);
            foreach (AttackData a in def.Attacks)
            {
                string state = "Attack_" + a.Id;
                int peak = a.Tell != null ? a.Tell.PoseEnd : a.HitOn.Start / 2;
                Pose(state, peak / (float)a.TotalFrames, false);
                Pose(state, (a.HitOn.Start + a.HitOn.End) * 0.5f / a.TotalFrames, false);
            }
            Pose("Death", 1f, false);
            Object.DestroyImmediate(go);

            int cols = 4, rows = (tiles.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * Tile, rows * Tile, TextureFormat.RGB24, false);
            for (int i = 0; i < tiles.Count; i++) sheet.SetPixels((i % cols) * Tile, (rows - 1 - i / cols) * Tile, Tile, Tile, tiles[i].GetPixels());
            File.WriteAllBytes(Path.Combine(dir, spec.Name + "_poses.png"), sheet.EncodeToPNG());
            Debug.Log("SC CAPTURE wrote " + Path.Combine(dir, spec.Name + "_poses.png") + " tiles=" + tiles.Count);
        }

        static Texture2D Render(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
