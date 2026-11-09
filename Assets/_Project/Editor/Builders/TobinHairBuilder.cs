// Strand hair experiment on the Tobin base head (Unity "Demo Team Hair" package, locally patched in Packages/com.unity.demoteam.hair).
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.TobinHairBuilder.BuildAssets      (scalp placement mesh + HairAsset)
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinHairBuilder.BuildDemoScene   (TobinHairDemo.unity)
// Not wired into ProjectBuilder.RunAll and not in Build Settings. The scalp comes from TobinScalp.fbx (faces cut from AvatarHead, extraction only);
// the placement mesh is baked to Head-bone local space, so the HairInstance on the Head bone gets rigid roots that follow the head.
using System.IO;
using Unity.DemoTeam.Hair;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SleepingCalamity.Presentation;

namespace SleepingCalamity.EditorTools
{
    // Import settings for TobinScalp.fbx: same scale as TobinBase.fbx, generic rig, readable.
    public sealed class TobinScalpImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessModel()
        {
            if (assetPath != TobinHairBuilder.ScalpFbx) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = TobinBaseModelImporter.GlobalScale;
            mi.useFileScale = false;
            mi.importBlendShapes = false;
            mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
            mi.isReadable = true;
            mi.animationType = ModelImporterAnimationType.Generic;
        }
    }

    public static class TobinHairBuilder
    {
        public const string Folder = "Assets/_Project/Content/Characters/TobinBase";
        public const string ScalpFbx = Folder + "/TobinScalp.fbx";
        public const string PlacementMeshPath = Folder + "/Hair/TobinScalpPlacement.asset";
        public const string HairAssetPath = Folder + "/Hair/TobinHair.asset";
        public const string ScenePath = Folder + "/TobinHairDemo.unity";
        public const string ShaderPath = Folder + "/Hair/TobinHairBrown.shader";
        public const string MaterialPath = Folder + "/Hair/TobinHairBrown.mat";
        public const int StrandCount = 12000;
        public const int ParticleCount = 16;
        public const float StrandLength = 0.24f;

        public static void BuildAssets()
        {
            AssetDatabase.Refresh();
            BuilderUtil.EnsureFolder(Folder + "/Hair");
            Mesh placement = BuildPlacementMesh();
            BuildHairAsset(placement);
            EnsureHairMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Scalp faces baked to Head-bone local space. Normals are bent downwards in world space so strands grow out and down along the skull.
        static Mesh BuildPlacementMesh()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ScalpFbx) ?? throw new FileNotFoundException(ScalpFbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>() ?? throw new System.InvalidOperationException("no SkinnedMeshRenderer in " + ScalpFbx);
                var head = FindDeep(inst.transform, "Head") ?? throw new System.InvalidOperationException("no Head bone in " + ScalpFbx);
                var baked = new Mesh();
                smr.BakeMesh(baked, false);
                Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
                Matrix4x4 toHead = head.worldToLocalMatrix * toWorld;
                Matrix4x4 normalToWorld = toWorld.inverse.transpose;
                var v = baked.vertices; var n = baked.normals;
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 nw = normalToWorld.MultiplyVector(n[i]).normalized;
                    Vector3 dirWorld = (nw + Vector3.down * 0.8f).normalized;
                    v[i] = toHead.MultiplyPoint3x4(v[i]);
                    n[i] = head.InverseTransformDirection(dirWorld).normalized;
                }
                var mesh = new Mesh { name = "TobinScalpPlacement" };
                mesh.indexFormat = baked.vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = v; mesh.normals = n; mesh.uv = baked.uv; mesh.triangles = baked.triangles;
                mesh.RecalculateBounds();
                Debug.Log($"SC HAIR scalp verts {v.Length} tris {mesh.triangles.Length / 3} head lossyScale {head.lossyScale} bounds {mesh.bounds}");
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(PlacementMeshPath);
                if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
                else AssetDatabase.CreateAsset(mesh, PlacementMeshPath);
                Object.DestroyImmediate(baked);
                return mesh;
            }
            finally { Object.DestroyImmediate(inst); }
        }

        static HairAsset BuildHairAsset(Mesh placement)
        {
            var asset = AssetDatabase.LoadAssetAtPath<HairAsset>(HairAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HairAsset>();
                AssetDatabase.CreateAsset(asset, HairAssetPath);
            }
            asset.settingsBasic = HairAsset.SettingsBasic.defaults;
            asset.settingsBasic.type = HairAsset.Type.Procedural;
            asset.settingsBasic.kLODClusters = true;
            var s = HairAsset.SettingsProcedural.defaults;
            s.placement = HairAsset.SettingsProcedural.PlacementMode.Mesh;
            s.placementMesh = placement;
            s.strandCount = StrandCount;
            s.strandParticleCount = ParticleCount;
            s.strandLength = StrandLength;
            s.strandLengthVariation = true; s.strandLengthVariationAmount = 0.35f;
            s.strandDiameter = 1.0f;   // mm; thicker than a real hair so 8k strands cover the scalp
            s.curl = true; s.curlRadius = 0.25f; s.curlSlope = 0.2f;
            s.curlVariation = true; s.curlVariationRadius = 0.1f; s.curlVariationSlope = 0.2f;
            asset.settingsProcedural = s;
            HairAssetBuilder.BuildHairAsset(asset, HairAssetBuilder.BuildFlags.DisableProgress);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            var g = asset.strandGroups;
            Debug.Log($"SC HAIR asset groups {(g == null ? 0 : g.Length)} strands {(g == null ? 0 : g[0].strandCount)} particles/strand {(g == null ? 0 : g[0].strandParticleCount)} lods {(g == null ? 0 : g[0].lodCount)}");
            return asset;
        }

        static Material EnsureHairMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath) ?? throw new FileNotFoundException(ShaderPath);
            mat = new Material(sh) { name = "TobinHairBrown" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        public static Material Clay(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.2f); m.SetFloat("_Metallic", 0f);
            return m;
        }

        // Character + hair instance on the Head bone. Clay body (this branch has no Tobin skin materials).
        public static GameObject SpawnCharacter(Transform parent, out Transform head, out HairInstance hair)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(TobinBaseModelImporter.ModelPath) ?? throw new FileNotFoundException(TobinBaseModelImporter.ModelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            var clay = Clay("TobinClay", new Color(0.62f, 0.55f, 0.50f));
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = clay;
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BaseUnderlay")) t.gameObject.SetActive(true);
            head = FindDeep(go.transform, "Head") ?? throw new System.InvalidOperationException("no Head bone");
            var asset = AssetDatabase.LoadAssetAtPath<HairAsset>(HairAssetPath) ?? throw new FileNotFoundException(HairAssetPath);
            var hg = new GameObject("TobinHair");
            hg.transform.SetParent(head, false);
            hair = hg.AddComponent<HairInstance>();
            hair.strandGroupProviders = new[] { new HairInstance.GroupProvider { hairAsset = asset } };
            hair.settingsExecutive.updateMode = HairInstance.SettingsExecutive.UpdateMode.BuiltinEvent;
            hair.settingsExecutive.updateSimulation = true;
            hair.settingsExecutive.updateSimulationRate = HairInstance.SettingsExecutive.UpdateRate.Fixed120Hz;   // 30 and 60 Hz lose strands on a fast head turn
            hair.settingsExecutive.updateSimulationInEditor = true;
            hair.strandGroupDefaults.settingsRendering.material = true;
            hair.strandGroupDefaults.settingsRendering.materialAsset = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            hair.enabled = false; hair.enabled = true;   // OnEnable ran at AddComponent with no providers: run it again so the strand group instances are built
            return go;
        }

        public static void BuildDemoScene()
        {
            BuildAssets();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath);
            var saved = QualitySettings.renderPipeline;
            if (rp != null) QualitySettings.renderPipeline = rp;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(38, 200, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor"; floor.transform.localScale = new Vector3(2f, 1, 2f);
                floor.GetComponent<Renderer>().sharedMaterial = Clay("Floor", new Color(0.32f, 0.33f, 0.35f));
                var go = SpawnCharacter(null, out Transform head, out HairInstance hair);
                go.name = "Tobin";
                var sway = go.AddComponent<TobinHeadSway>();
                sway.head = head; sway.CacheBase();
                FrameCamera(head);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("SC HAIR demo scene saved " + ScenePath);
            }
            finally { QualitySettings.renderPipeline = saved; }
        }

        // Head and shoulders, about 1.3 m from the face. The character faces +Z in Unity (as in TobinYBotCompareCapture), so the camera sits at +Z.
        static void FrameCamera(Transform head)
        {
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.76f, 0.80f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 40f;
            Vector3 target = head.position + new Vector3(0, 0.06f, 0);
            cam.transform.position = target + new Vector3(0.25f, 0.02f, 1.35f);
            cam.transform.LookAt(target);
        }
    }
}
