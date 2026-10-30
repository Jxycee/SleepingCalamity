// The M8 look-test scenes (PHASE1 3.5 and 3.7, sc-scene-look): LookTest_Day (a half-timbered facade on cobbles with trims, decals and a sphere set of
// the B1 materials) and LookTest_Cellar (a barrel-vault room with the B2 materials, lamps, an ember grate and light shafts). Built from greybox
// shapes with UVs in metres (LookTestGeometry), the B1-B3 textures imported as URP Lit, and the A1 stack (Forward+, 4 cascades 100 m 4096 soft High,
// SSAO depth-normals radius 0.35, Neutral tonemapper + grade, bloom, fog, a reflection probe, light-cone shafts). Three fixed capture cameras per
// corner at 1920x1080 (LookTestCapture renders them). Idempotent: everything is updated in place, scenes are rebuilt through BuilderUtil.SaveScene.
// The stack lives in LookTest_RPAsset / LookTest_Renderer (copies of the PC asset), selected by the capture method, so no shipping setting changes.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class LookTestBuilder
    {
        public const string DayScene = "Assets/_Project/Scenes/LookTest_Day.unity";
        public const string CellarScene = "Assets/_Project/Scenes/LookTest_Cellar.unity";
        public const string RenderingFolder = "Assets/_Project/Content/Rendering";
        public const string RpAssetPath = RenderingFolder + "/LookTest_RPAsset.asset";
        public const string RendererPath = RenderingFolder + "/LookTest_Renderer.asset";
        const string SourceRpAsset = "Assets/Settings/PC_RPAsset.asset";
        const string SourceRenderer = "Assets/Settings/PC_Renderer.asset";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/LookTest";
        const string ConeShaderName = "SleepingCalamity/LightCone";
        const int Gut = 12, SheetPx = 2048, StripPx = 256;

        public static void Build()
        {
            EnsureRendering();
            BuildMaterials();
            BuildProfiles();
            BuildDay();
            BuildCellar();
        }

        // --------------------------------------------------------------------------------------------- rendering stack
        public static void EnsureRendering()
        {
            BuilderUtil.EnsureFolder(RenderingFolder);
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath) == null)
                if (!AssetDatabase.CopyAsset(SourceRenderer, RendererPath)) throw new System.InvalidOperationException("Cannot copy " + SourceRenderer);
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(RpAssetPath) == null)
                if (!AssetDatabase.CopyAsset(SourceRpAsset, RpAssetPath)) throw new System.InvalidOperationException("Cannot copy " + SourceRpAsset);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            if (renderer == null || rp == null) throw new System.InvalidOperationException("Look-test rendering assets did not load");

            // Renderer: Forward+, no depth priming (a decision-A1 stack), plus SSAO (depth normals, radius 0.35, low direct-lighting strength).
            bool changed = false;
            using (var so = new SerializedObject(renderer))
            {
                changed |= SetInt(so, "m_RenderingMode", 2);        // Forward+
                changed |= SetInt(so, "m_DepthPrimingMode", 0);
                if (so.ApplyModifiedPropertiesWithoutUndo()) changed = true;
            }
            ScreenSpaceAmbientOcclusion ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (ssao == null) { ssao = AddFeature(renderer); changed = true; }
            using (var so = new SerializedObject(ssao))
            {
                SetInt(so, "m_Settings.Source", 1);                  // DepthNormals
                SetFloat(so, "m_Settings.Radius", 0.35f);
                SetFloat(so, "m_Settings.Intensity", 1.2f);
                SetFloat(so, "m_Settings.DirectLightingStrength", 0.15f);
                if (so.ApplyModifiedPropertiesWithoutUndo()) changed = true;
            }
            if (changed) { EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(ssao); }

            // RP asset: point at the look-test renderer, HDR, 4 cascades, 100 m, 4096, soft High (PHASE1 3.5 day numbers).
            using (var so = new SerializedObject(rp))
            {
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                if (list.arraySize != 1) list.arraySize = 1;
                if (list.GetArrayElementAtIndex(0).objectReferenceValue != renderer) list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                SetInt(so, "m_DefaultRendererIndex", 0);
                SetInt(so, "m_SupportsHDR", 1);
                SetInt(so, "m_MainLightShadowmapResolution", 4096);
                SetFloat(so, "m_ShadowDistance", 100f);
                SetInt(so, "m_ShadowCascadeCount", 4);
                SetInt(so, "m_SoftShadowQuality", 3);
                SetInt(so, "m_GPUResidentDrawerMode", 0);   // off for the edit-mode batch captures: with it on, draws picked wrong textures (every material showed the ground's)
                if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(rp);
            }
            AssetDatabase.SaveAssets();
        }

        // The editor's "add renderer feature" steps: the feature becomes a sub-asset, listed with its local file id in the feature map.
        static ScreenSpaceAmbientOcclusion AddFeature(UniversalRendererData data)
        {
            var feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            feature.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            using var so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures"), map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return feature;
        }

        static bool SetInt(SerializedObject so, string path, int v)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) throw new System.MissingFieldException(so.targetObject.GetType().Name + "." + path);
            if (p.propertyType == SerializedPropertyType.Boolean) { bool b = v != 0; if (p.boolValue == b) return false; p.boolValue = b; return true; }
            if (p.intValue == v) return false;
            p.intValue = v; return true;
        }

        static bool SetFloat(SerializedObject so, string path, float v)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) throw new System.MissingFieldException(so.targetObject.GetType().Name + "." + path);
            if (Mathf.Approximately(p.floatValue, v)) return false;
            p.floatValue = v; return true;
        }

        // --------------------------------------------------------------------------------------------- materials
        sealed class MatSpec
        {
            public string Name, Folder, Set; public int Strip = -1; public bool Decal;
            public MatSpec(string name, string folder, string set, int strip = -1, bool decal = false) { Name = name; Folder = folder; Set = set; Strip = strip; Decal = decal; }
        }

        static readonly MatSpec[] Specs =
        {
            new MatSpec("plaster", "B1_town", "plaster"), new MatSpec("oak_timber", "B1_town", "oak_timber"), new MatSpec("cobble", "B1_town", "cobble"),
            new MatSpec("sandstone", "B1_town", "sandstone"), new MatSpec("roof_tile", "B1_town", "roof_tile"), new MatSpec("painted_wood", "B1_town", "painted_wood"),
            new MatSpec("canvas", "B1_town", "canvas"), new MatSpec("iron", "B1_town", "iron"),
            new MatSpec("old_brick", "B2_cellar", "old_brick"), new MatSpec("vault_stone", "B2_cellar", "vault_stone"), new MatSpec("limewash", "B2_cellar", "limewash"),
            new MatSpec("flagstone", "B2_cellar", "flagstone"), new MatSpec("barrel_oak", "B2_cellar", "barrel_oak"), new MatSpec("soot_coal", "B2_cellar", "soot_coal"),
            new MatSpec("root_bark", "B2_cellar", "root_bark"),
            // trim sheets: strip index into the 8 strips of a sheet (town: 0 timber chamfer, 3 stone cope, 6 iron strap; cellar: 0 beam, 2 stone cope, 6 iron strap)
            new MatSpec("trim_town_timber", "B3_trims", "trim_town", 0), new MatSpec("trim_town_stone", "B3_trims", "trim_town", 3), new MatSpec("trim_town_iron", "B3_trims", "trim_town", 6),
            new MatSpec("trim_cellar_beam", "B3_trims", "trim_cellar", 0), new MatSpec("trim_cellar_stone", "B3_trims", "trim_cellar", 2), new MatSpec("trim_cellar_iron", "B3_trims", "trim_cellar", 6),
            new MatSpec("moss", "B3_trims", "moss", -1, true), new MatSpec("grime", "B3_trims", "grime", -1, true),
            new MatSpec("drip", "B3_trims", "drip", -1, true), new MatSpec("crack", "B3_trims", "crack", -1, true),
        };

        public static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_" + name + ".mat")
            ?? throw new System.InvalidOperationException("Look-test material not built: " + name);

        public static void BuildMaterials()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found");
            foreach (MatSpec s in Specs)
            {
                string dir = LookTestTextureImporter.Root + s.Folder + "/" + s.Set + "/" + s.Set;
                Texture2D Tex(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + suffix + ".png")
                    ?? throw new System.IO.FileNotFoundException("Texture not imported: " + dir + suffix + ".png (run sc_tex_to_unity.py)");
                Material m = Ensure(MaterialsFolder + "/M_" + s.Name + ".mat", lit);
                m.SetTexture("_BaseMap", Tex("_albedo")); m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BumpMap", Tex("_normal")); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", Tex("_ms")); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", Tex("_occ")); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
                Vector2 scale = Vector2.one, offset = Vector2.zero;
                if (s.Strip >= 0)   // the inner rows of one strip (the 12 px gutters stay out of the mapping)
                {
                    scale = new Vector2(1f, (StripPx - 2 * Gut) / (float)SheetPx);
                    offset = new Vector2(0f, (7 - s.Strip) / 8f + Gut / (float)SheetPx);
                }
                foreach (string prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) { m.SetTextureScale(prop, scale); m.SetTextureOffset(prop, offset); }
                if (s.Decal) MakeTransparent(m);
                EditorUtility.SetDirty(m);
            }
            Mat("cobble").SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.78f));   // the ground fills half the frame: a darker tint keeps the day median in band
            Material cone = Ensure(MaterialsFolder + "/M_LightCone.mat", Shader.Find(ConeShaderName) ?? throw new System.InvalidOperationException("Shader not found: " + ConeShaderName));
            cone.SetColor("_Color", new Color(1.0f, 0.84f, 0.58f)); cone.SetFloat("_Intensity", 0.10f); cone.renderQueue = 3100;
            Material coneCellar = Ensure(MaterialsFolder + "/M_LightConeCellar.mat", cone.shader);
            coneCellar.SetColor("_Color", new Color(1.0f, 0.72f, 0.36f)); coneCellar.SetFloat("_Intensity", 0.07f); coneCellar.renderQueue = 3100;
            Material figure = Ensure(MaterialsFolder + "/M_Figure.mat", lit); figure.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f)); figure.SetFloat("_Smoothness", 0.2f);
            Material lamp = Ensure(MaterialsFolder + "/M_LampGlow.mat", lit);
            lamp.SetColor("_BaseColor", new Color(0.1f, 0.08f, 0.05f)); lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(3.0f, 2.1f, 1.0f));
            lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Material ember = Ensure(MaterialsFolder + "/M_Ember.mat", lit);
            ember.SetColor("_BaseColor", new Color(0.12f, 0.03f, 0.02f)); ember.EnableKeyword("_EMISSION"); ember.SetColor("_EmissionColor", new Color(0.7f, 0.12f, 0.04f));
            ember.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Material sky = Ensure(MaterialsFolder + "/M_LookTestSky.mat", Shader.Find("Skybox/Procedural") ?? throw new System.InvalidOperationException("Skybox/Procedural not found"));
            sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_SunSizeConvergence", 5f); sky.SetFloat("_AtmosphereThickness", 0.6f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.80f)); sky.SetColor("_GroundColor", new Color(0.42f, 0.40f, 0.36f)); sky.SetFloat("_Exposure", 0.9f);
            AssetDatabase.SaveAssets();
        }

        static Material Ensure(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            if (m.shader != shader) m.shader = shader;
            return m;
        }

        // URP Lit alpha-blended surface (what the Lit inspector writes for Surface Type = Transparent, Blend = Alpha), no shadow casting.
        static void MakeTransparent(Material m)
        {
            // Premultiply blend (_Blend 1): URP multiplies the colour by alpha in the shader, which equals alpha blending for these straight-alpha decals,
            // and it is the state Unity's own material validation settles on (any other value flips back and forth between builds and dirties the .mat).
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 1f); m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 10f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("ShadowCaster", false); m.renderQueue = 3000;
        }

        // --------------------------------------------------------------------------------------------- volume profiles
        public static VolumeProfile EnsureProfile(string name, System.Action<VolumeProfile> set)
        {
            string path = RenderingFolder + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            set(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Comp<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet(out T existing)) return existing;
            T c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        public static void BuildProfiles()
        {
            BuilderUtil.EnsureFolder(RenderingFolder);
            // Day: Neutral tonemapper, modest bloom (high scatter), a muted grade with slate-teal shadows (3.1: #2f3a44).
            EnsureProfile("LookTest_Day_Profile", p =>
            {
                Comp<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                Bloom b = Comp<Bloom>(p); b.threshold.Override(1.0f); b.intensity.Override(0.25f); b.scatter.Override(0.8f); b.clamp.Override(65000f);
                ColorAdjustments c = Comp<ColorAdjustments>(p); c.postExposure.Override(0.0f); c.contrast.Override(8f); c.saturation.Override(-4f);
                ShadowsMidtonesHighlights s = Comp<ShadowsMidtonesHighlights>(p); s.shadows.Override(new Vector4(0.80f, 0.92f, 1.0f, 0f));
                LiftGammaGain lg = Comp<LiftGammaGain>(p); lg.lift.Override(new Vector4(0.94f, 0.98f, 1.0f, 0.02f));
                Vignette v = Comp<Vignette>(p); v.intensity.Override(0.18f); v.smoothness.Override(0.5f);
            });
            // Cellars: same stack, warmer white balance, the grade's shadow tint is teal and the lights stay lamp gold.
            EnsureProfile("LookTest_Cellar_Profile", p =>
            {
                Comp<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                Bloom b = Comp<Bloom>(p); b.threshold.Override(1.0f); b.intensity.Override(0.35f); b.scatter.Override(0.85f); b.clamp.Override(65000f);
                // Warm firelight, no teal anywhere (PHASE1 3.1, user 2026-10-30): neutral white balance, shadows pulled toward warm umber to plum,
                // clean saturation, crisper contrast so lit stone, brick and shadow separate.
                ColorAdjustments c = Comp<ColorAdjustments>(p); c.postExposure.Override(-0.15f); c.contrast.Override(26f); c.saturation.Override(-6f);
                ShadowsMidtonesHighlights s = Comp<ShadowsMidtonesHighlights>(p); s.shadows.Override(new Vector4(1.0f, 0.86f, 0.90f, 0f));
                WhiteBalance w = Comp<WhiteBalance>(p); w.temperature.Override(0f);
                LiftGammaGain lg = Comp<LiftGammaGain>(p); lg.lift.Override(new Vector4(1.0f, 0.93f, 0.95f, 0.0f));
                Vignette v = Comp<Vignette>(p); v.intensity.Override(0.28f); v.smoothness.Override(0.5f);
            });
            AssetDatabase.SaveAssets();
        }

        // --------------------------------------------------------------------------------------------- scene helpers
        static GameObject Shape(Transform parent, string name, LookTestGeometry.Shape shape, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default, float tile = 2f, int segments = 24, bool cast = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.AddComponent<MeshRenderer>(); go.AddComponent<MeshFilter>(); r.sharedMaterial = mat;
            if (!cast) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<LookTestGeometry>().Configure(shape, size, tile, segments);
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default, float tile = 2f) =>
            Shape(parent, name, LookTestGeometry.Shape.Box, pos, size, mat, euler, tile);

        static GameObject Trim(Transform parent, string name, Vector3 pos, Vector3 size, Material mat) =>
            Shape(parent, name, LookTestGeometry.Shape.TrimBox, pos, size, mat, default, 2f);

        static GameObject Decal(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float tile, Vector3 euler = default) =>
            Shape(parent, name, LookTestGeometry.Shape.Box, pos, size, mat, euler, tile, 24, false);

        static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Light AddLight(Transform parent, string name, LightType type, Vector3 pos, Quaternion rot, Color color, float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot;
            Light l = go.AddComponent<Light>();
            l.type = type; l.color = color; l.intensity = intensity; l.range = range; l.shadows = shadows;
            if (shadows != LightShadows.None) { l.shadowStrength = 1f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.4f; }
            return l;
        }

        static Camera AddCapture(Transform parent, string name, Vector3 pos, Vector3 target, float fov)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.LookRotation(target - pos, Vector3.up);
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f; cam.allowHDR = true; cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.Skybox; cam.enabled = false;
            UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.requiresDepthTexture = true;
            data.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        static void AddVolume(Transform parent, string profileName)
        {
            var go = new GameObject("PostFX"); go.transform.SetParent(parent, false);
            Volume v = go.AddComponent<Volume>(); v.isGlobal = true; v.priority = 1f;
            v.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingFolder + "/" + profileName + ".asset");
        }

        static void AddProbe(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("ReflectionProbe"); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            ReflectionProbe p = go.AddComponent<ReflectionProbe>();
            p.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime; p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.resolution = 256; p.hdr = true; p.size = size; p.boxProjection = false; p.importance = 1; p.intensity = 1f;
        }

        // --------------------------------------------------------------------------------------------- Day
        public static void BuildDay()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("LookTest_Day").transform;

            Material plaster = Mat("plaster"), oak = Mat("oak_timber"), cobble = Mat("cobble"), sand = Mat("sandstone"), roof = Mat("roof_tile"),
                paint = Mat("painted_wood"), canvas = Mat("canvas");

            Transform set = new GameObject("Set").transform; set.SetParent(root, false);
            Box(set, "Ground", new Vector3(0f, -0.15f, 4f), new Vector3(34f, 0.3f, 32f), cobble);
            Box(set, "Plinth", new Vector3(0f, 0.3f, 12f), new Vector3(8.6f, 0.6f, 0.9f), sand);
            Trim(set, "PlinthCope", new Vector3(0f, 0.72f, 11.54f), new Vector3(8.6f, 0.25f, 0.12f), Mat("trim_town_stone"));
            Box(set, "Wall", new Vector3(0f, 2.8f, 12.1f), new Vector3(8f, 4.4f, 0.5f), plaster);
            foreach (float x in new[] { -3.85f, -1.3f, 1.3f, 3.85f }) Box(set, "Post" + x, new Vector3(x, 2.8f, 11.8f), new Vector3(0.3f, 4.4f, 0.35f), oak, default, 2f);
            Box(set, "BeamLow", new Vector3(0f, 1.0f, 11.8f), new Vector3(8f, 0.3f, 0.35f), oak);
            Box(set, "BeamMid", new Vector3(0f, 3.1f, 11.8f), new Vector3(8f, 0.3f, 0.35f), oak);
            Box(set, "BeamTop", new Vector3(0f, 4.85f, 11.8f), new Vector3(8f, 0.3f, 0.35f), oak);
            Box(set, "BraceL", new Vector3(-2.6f, 3.9f, 11.78f), new Vector3(0.22f, 2.3f, 0.3f), oak, new Vector3(0, 0, 50f));
            Box(set, "BraceR", new Vector3(2.6f, 3.9f, 11.78f), new Vector3(0.22f, 2.3f, 0.3f), oak, new Vector3(0, 0, -50f));
            Trim(set, "TimberTrim", new Vector3(0f, 5.1f, 11.6f), new Vector3(8.4f, 0.25f, 0.1f), Mat("trim_town_timber"));
            Box(set, "Door", new Vector3(0f, 1.95f, 11.7f), new Vector3(1.4f, 2.4f, 0.12f), paint);
            Trim(set, "DoorStrapLow", new Vector3(0f, 1.2f, 11.62f), new Vector3(1.4f, 0.2f, 0.04f), Mat("trim_town_iron"));
            Trim(set, "DoorStrapHigh", new Vector3(0f, 2.65f, 11.62f), new Vector3(1.4f, 0.2f, 0.04f), Mat("trim_town_iron"));
            Box(set, "ShutterL", new Vector3(-2.6f, 3.5f, 11.68f), new Vector3(0.8f, 1.2f, 0.1f), paint);
            Box(set, "ShutterR", new Vector3(2.6f, 3.5f, 11.68f), new Vector3(0.8f, 1.2f, 0.1f), paint);
            Box(set, "RoofFront", new Vector3(0f, 6.35f, 11.65f), new Vector3(9.0f, 0.3f, 2.4f), roof, new Vector3(-36f, 0, 0));
            Box(set, "RoofBack", new Vector3(0f, 6.35f, 13.6f), new Vector3(9.0f, 0.3f, 2.4f), roof, new Vector3(36f, 0, 0));
            Box(set, "Awning", new Vector3(0f, 2.95f, 10.9f), new Vector3(3.2f, 0.08f, 1.7f), canvas, new Vector3(18f, 0, 0), 1.6f);
            Decal(set, "MossOnCobble", new Vector3(2.4f, 0.006f, 10.4f), new Vector3(1.5f, 0.004f, 1.5f), Mat("moss"), 1.5f);
            Decal(set, "GrimeOnWall", new Vector3(-2.6f, 2.6f, 11.84f), new Vector3(1.5f, 1.5f, 0.004f), Mat("grime"), 1.5f);
            Decal(set, "DripOnWall", new Vector3(2.7f, 2.2f, 11.84f), new Vector3(1.5f, 1.5f, 0.004f), Mat("drip"), 1.5f);
            Decal(set, "CrackOnWall", new Vector3(-0.3f, 3.9f, 11.84f), new Vector3(1.5f, 1.5f, 0.004f), Mat("crack"), 1.5f);
            Prim(set, "Figure_1.75m", PrimitiveType.Capsule, new Vector3(-1.4f, 0.875f, 8.0f), new Vector3(0.5f, 0.875f, 0.5f), AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Figure.mat"));

            // The sphere set: every B1 material on a sphere, in a row on the cobbles in front of the facade.
            Transform spheres = new GameObject("SphereSet").transform; spheres.SetParent(root, false);
            string[] names = { "plaster", "oak_timber", "cobble", "sandstone", "roof_tile", "painted_wood", "canvas", "iron" };
            for (int i = 0; i < names.Length; i++)
                Prim(spheres, "Sphere_" + names[i], PrimitiveType.Sphere, new Vector3(-3.15f + i * 0.9f, 0.45f, 4.6f), Vector3.one * 0.8f, Mat(names[i]));

            // Light: one warm sun (about 5200 K), cool sky fill (trilight ambient), horizon-matched exponential fog, a reflection probe, light shafts.
            Transform lighting = new GameObject("Lighting").transform; lighting.SetParent(root, false);
            AddLight(lighting, "Sun", LightType.Directional, new Vector3(0, 10, 0), Quaternion.Euler(54f, -32f, 0f), new Color(1.0f, 0.92f, 0.80f), 3.0f, 10f, LightShadows.Soft);
            for (int i = 0; i < 4; i++)
            {
                Shape(lighting, "Shaft" + i, LookTestGeometry.Shape.Cone, new Vector3(-6f + i * 3.2f, 11f, 3f + i * 2.2f), new Vector3(1.4f, 0f, 16f),
                    AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LightCone.mat"), new Vector3(54f, -32f, 0f), 2f, 16, false);
            }
            AddProbe(lighting, new Vector3(0f, 3f, 6f), new Vector3(34f, 12f, 30f));
            AddVolume(lighting, "LookTest_Day_Profile");
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LookTestSky.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.55f, 0.70f); RenderSettings.ambientEquatorColor = new Color(0.50f, 0.50f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.22f); RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.0085f; RenderSettings.fogColor = new Color(0.72f, 0.77f, 0.84f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.sun = lighting.Find("Sun").GetComponent<Light>();

            // Capture cameras (P39: wide, mid, close; 1920x1080 by LookTestCapture).
            Transform cams = new GameObject("CaptureCameras").transform; cams.SetParent(root, false);
            AddCapture(cams, "Cap_Day_Wide", new Vector3(0f, 2.0f, -6.0f), new Vector3(0f, 3.2f, 12f), 46f);
            AddCapture(cams, "Cap_Day_Mid", new Vector3(3.5f, 1.7f, 3.0f), new Vector3(-0.5f, 2.2f, 12f), 42f);
            AddCapture(cams, "Cap_Day_Close", new Vector3(-0.4f, 1.55f, 8.4f), new Vector3(-0.6f, 1.55f, 11.7f), 38f);
            BuilderUtil.SaveScene(scene, DayScene);
        }

        // --------------------------------------------------------------------------------------------- Cellar
        public static void BuildCellar()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("LookTest_Cellar").transform;
            Material brick = Mat("old_brick"), vault = Mat("vault_stone"), lime = Mat("limewash"), flag = Mat("flagstone"), barrel = Mat("barrel_oak"),
                coal = Mat("soot_coal"), roots = Mat("root_bark"), iron = Mat("iron");
            var load = (System.Func<string, Material>)(n => AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_" + n + ".mat"));

            Transform set = new GameObject("Set").transform; set.SetParent(root, false);
            Box(set, "Floor", new Vector3(0f, -0.1f, 5f), new Vector3(5.2f, 0.2f, 10.4f), flag);
            Box(set, "WallL", new Vector3(-2.6f, 0.8f, 5f), new Vector3(0.4f, 1.6f, 10f), brick);
            Box(set, "WallR", new Vector3(2.6f, 0.8f, 5f), new Vector3(0.4f, 1.6f, 10f), brick);
            Shape(set, "Vault", LookTestGeometry.Shape.VaultInterior, new Vector3(0f, 1.6f, 0f), new Vector3(2.4f, 0f, 10f), vault, default, 2f, 32);
            Box(set, "BackWall", new Vector3(0f, 2.1f, 10.2f), new Vector3(5.2f, 4.2f, 0.4f), lime);
            Box(set, "NicheBack", new Vector3(-1.3f, 1.0f, 9.95f), new Vector3(1.2f, 1.1f, 0.1f), brick);
            Trim(set, "TrimBeam", new Vector3(0f, 1.62f, 9.98f), new Vector3(4.8f, 0.25f, 0.1f), load("trim_cellar_beam"));
            Trim(set, "TrimStone", new Vector3(-2.36f, 0.16f, 5f), new Vector3(9.6f, 0.25f, 0.1f), load("trim_cellar_stone")).transform.localRotation = Quaternion.Euler(0, 90f, 0);
            Trim(set, "IronStrap", new Vector3(0f, 3.4f, 9.98f), new Vector3(4.8f, 0.2f, 0.05f), load("trim_cellar_iron"));
            Box(set, "ShelfL", new Vector3(-2.15f, 1.0f, 8.2f), new Vector3(0.5f, 0.08f, 1.8f), barrel);
            Box(set, "ShelfL2", new Vector3(-2.15f, 1.5f, 8.2f), new Vector3(0.5f, 0.08f, 1.8f), barrel);
            Decal(set, "GrimeBack", new Vector3(1.2f, 2.0f, 9.98f), new Vector3(1.5f, 1.5f, 0.004f), load("grime"), 1.5f);
            Decal(set, "DripBack", new Vector3(-0.2f, 2.4f, 9.98f), new Vector3(1.5f, 1.5f, 0.004f), load("drip"), 1.5f);
            Decal(set, "MossFloor", new Vector3(1.4f, 0.006f, 3.0f), new Vector3(1.5f, 0.004f, 1.5f), load("moss"), 1.5f);
            Decal(set, "CrackFloor", new Vector3(-0.6f, 0.007f, 6.0f), new Vector3(1.5f, 0.004f, 1.5f), load("crack"), 1.5f);
            Prim(set, "Barrel_A", PrimitiveType.Cylinder, new Vector3(-1.75f, 0.5f, 2.6f), new Vector3(0.85f, 0.5f, 0.85f), barrel);
            Prim(set, "Barrel_B", PrimitiveType.Cylinder, new Vector3(-1.05f, 0.5f, 3.0f), new Vector3(0.85f, 0.5f, 0.85f), barrel);
            Prim(set, "Barrel_C", PrimitiveType.Cylinder, new Vector3(-1.45f, 1.5f, 2.8f), new Vector3(0.85f, 0.5f, 0.85f), barrel);
            Prim(set, "CoalPile", PrimitiveType.Sphere, new Vector3(1.55f, 0.2f, 7.2f), new Vector3(1.5f, 0.7f, 1.2f), coal);
            Prim(set, "RootA", PrimitiveType.Cylinder, new Vector3(0.5f, 3.3f, 5.8f), new Vector3(0.14f, 0.7f, 0.14f), roots);
            Prim(set, "RootB", PrimitiveType.Cylinder, new Vector3(0.75f, 3.5f, 6.1f), new Vector3(0.09f, 0.55f, 0.09f), roots);
            Prim(set, "RootC", PrimitiveType.Cylinder, new Vector3(-0.9f, 3.45f, 4.3f), new Vector3(0.11f, 0.6f, 0.11f), roots);
            Prim(set, "Figure_1.75m", PrimitiveType.Capsule, new Vector3(0.9f, 0.875f, 4.0f), new Vector3(0.5f, 0.875f, 0.5f), load("Figure"));
            Prim(set, "LampA", PrimitiveType.Sphere, new Vector3(0f, 2.9f, 2.5f), Vector3.one * 0.22f, load("LampGlow"));
            Prim(set, "LampB", PrimitiveType.Sphere, new Vector3(0f, 2.9f, 7.0f), Vector3.one * 0.22f, load("LampGlow"));
            Box(set, "BreathGrate", new Vector3(-0.8f, 0.012f, 8.5f), new Vector3(1.0f, 0.02f, 1.0f), load("Ember"));
            Transform spheres = new GameObject("SphereSet").transform; spheres.SetParent(root, false);
            string[] names = { "old_brick", "vault_stone", "limewash", "flagstone", "barrel_oak", "soot_coal", "root_bark", "iron" };
            for (int i = 0; i < names.Length; i++)
                Prim(spheres, "Sphere_" + names[i], PrimitiveType.Sphere, new Vector3(-1.75f + i * 0.5f, 0.25f, 1.1f), Vector3.one * 0.46f, load(names[i]));

            Transform lighting = new GameObject("Lighting").transform; lighting.SetParent(root, false);
            Color gold = new Color(1.0f, 0.78f, 0.42f);   // saturated clean lamp gold (#d6a24a family), not a brown haze
            AddLight(lighting, "LampLightA", LightType.Point, new Vector3(0f, 2.8f, 2.5f), Quaternion.identity, gold, 10f, 9f, LightShadows.Soft);
            AddLight(lighting, "LampLightB", LightType.Point, new Vector3(0f, 2.8f, 7.0f), Quaternion.identity, gold, 10f, 9f, LightShadows.Soft);
            AddLight(lighting, "EmberLight", LightType.Point, new Vector3(-0.8f, 0.5f, 8.5f), Quaternion.identity, new Color(0.95f, 0.26f, 0.12f), 4f, 3.5f, LightShadows.None);
            for (int i = 0; i < 2; i++)
                Shape(lighting, "Shaft" + i, LookTestGeometry.Shape.Cone, new Vector3(0.9f - i * 1.6f, 3.95f, 4.2f + i * 2.4f), new Vector3(0.9f, 0f, 4.2f),
                    load("LightConeCellar"), new Vector3(78f, 0f, 0f), 2f, 16, false);
            AddProbe(lighting, new Vector3(0f, 1.8f, 5f), new Vector3(5.2f, 4f, 10.4f));
            AddVolume(lighting, "LookTest_Cellar_Profile");
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.14f, 0.13f); RenderSettings.ambientEquatorColor = new Color(0.19f, 0.13f, 0.10f);   // warm umber to plum, no blue-green
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.07f, 0.05f); RenderSettings.ambientIntensity = 0.7f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.007f; RenderSettings.fogColor = new Color(0.15f, 0.10f, 0.08f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.sun = null;

            Transform cams = new GameObject("CaptureCameras").transform; cams.SetParent(root, false);
            AddCapture(cams, "Cap_Cellar_Wide", new Vector3(0f, 1.7f, -0.2f), new Vector3(0f, 1.9f, 9f), 62f);
            AddCapture(cams, "Cap_Cellar_Mid", new Vector3(-0.2f, 1.45f, 1.6f), new Vector3(-0.9f, 1.0f, 3.6f), 55f);
            AddCapture(cams, "Cap_Cellar_Close", new Vector3(1.0f, 1.1f, 5.2f), new Vector3(0.2f, 0.4f, 8.4f), 48f);
            foreach (Camera c in cams.GetComponentsInChildren<Camera>(true)) c.clearFlags = CameraClearFlags.SolidColor;
            foreach (Camera c in cams.GetComponentsInChildren<Camera>(true)) c.backgroundColor = new Color(0.02f, 0.03f, 0.035f);
            BuilderUtil.SaveScene(scene, CellarScene);
        }
    }
}
