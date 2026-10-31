// Builds BeautyCorner_Hollowmere (M9, docs/art/beauty-corner-brief.md, PHASE1 3.1/3.5/3.7): the half-timbered facade, dry fountain, stall and props on cobbles,
// every piece at its brief footprint (BeautyCornerTable). Each row uses the real art when BeautyCornerArt finds it under Content/Art/Town/<Batch>/<Asset>/,
// else a labelled greybox (PLACEHOLDER_*), so art drops in as it lands: rerun the builder (SleepingCalamity/Art/Build Beauty Corner, or ProjectBuilder.RunAll).
// Light: PHASE1 3.5 day numbers through the LookTest A1 stack (Forward+, SSAO, 4 cascades 100 m 4096 soft High; BeautyCornerBuilder.RpAssetPath, a copy of the look-test assets with SSAO 1.8 / 0.3), a 5200 K sun,
// cool sky fill, exponential haze, bloom + Neutral tonemap in BeautyCorner_Day_Profile. No volumetric package is in Packages/manifest.json, so god rays are
// authored additive light-cone cards (the Shafts root is aligned to the sun by ApplyPreset), plus drifting pollen. Presets: Midday and LowSun.
// Idempotent: the scene is rebuilt through BuilderUtil.SaveScene; materials and textures are updated in place.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class BeautyCornerBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/BeautyCorner_Hollowmere.unity";
        public const string ProfileName = "BeautyCorner_Day_Profile", LowSunProfileName = "BeautyCorner_LowSun_Profile";
        // The corner's own copy of the look-test renderer / RP asset with the stronger SSAO (1.8 / 0.3), so the LookTest scenes keep 1.2 / 0.15.
        public const string RendererPath = LookTestBuilder.RenderingFolder + "/BeautyCorner_Renderer.asset";
        public const string RpAssetPath = LookTestBuilder.RenderingFolder + "/BeautyCorner_RPAsset.asset";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/BeautyCorner";
        const string TexturesFolder = "Assets/_Project/Content/Textures/BeautyCorner";
        public enum Preset { Midday, LowSun }

        [MenuItem("SleepingCalamity/Art/Build Beauty Corner")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            EnsureLookAssets();
            BuildScene();
            KeepDecalKeywords();
        }

        // The corner's rendering stack, grade profiles, skies and support materials, without its scene: HollowmereBuilder reuses the same look
        // (and calls KeepDecalKeywords after its own scene, which builds the facade materials again).
        public static void EnsureLook() => EnsureLookAssets();

        static void EnsureLookAssets()
        {
            LookTestBuilder.EnsureRendering();   // the look-test stack is the copy source of the corner's own assets (and is put back to its own numbers)
            LookTestBuilder.EnsureRendering(RendererPath, RpAssetPath, LookTestBuilder.RendererPath, LookTestBuilder.RpAssetPath, 1.8f, 0.3f);
            LookTestBuilder.BuildMaterials();
            BuildProfile();
            BuildSupportMaterials();
            BeautyCornerArt.EnsureCellarLantern();
        }

        // Unity's material validation strips _ALPHAPREMULTIPLY_ON from the look-test decals while the facade materials are built from them; put the committed
        // state back last so a build leaves those three .mat files as they are in git.
        internal static void KeepDecalKeywords()
        {
            foreach (string n in new[] { "moss", "grime", "drip", "crack" })
            {
                Material m = LookTestBuilder.Mat(n);
                if (m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) continue;
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        // --------------------------------------------------------------------------------------------- support assets
        static void BuildProfile()
        {
            // The low-sun look is the approved one (user, polish1): its own profile with the pre-polish grade; midday carries the polish1 highlight lift.
            LookTestBuilder.EnsureProfile(LowSunProfileName, p =>
            {
                LookTestBuilder.Comp<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                Bloom b = LookTestBuilder.Comp<Bloom>(p); b.threshold.Override(1.0f); b.intensity.Override(0.25f); b.scatter.Override(0.8f); b.clamp.Override(65000f);
                ColorAdjustments c = LookTestBuilder.Comp<ColorAdjustments>(p); c.postExposure.Override(-0.55f); c.contrast.Override(30f); c.saturation.Override(0f);
                ShadowsMidtonesHighlights s = LookTestBuilder.Comp<ShadowsMidtonesHighlights>(p); s.shadows.Override(new Vector4(0.95f, 1.0f, 1.0f, 0f));
                LiftGammaGain lg = LookTestBuilder.Comp<LiftGammaGain>(p); lg.lift.Override(new Vector4(0.97f, 1.0f, 1.0f, 0.0f));
                Vignette v = LookTestBuilder.Comp<Vignette>(p); v.intensity.Override(0.18f); v.smoothness.Override(0.5f);
            });
            LookTestBuilder.EnsureProfile(ProfileName, p =>
            {
                LookTestBuilder.Comp<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                Bloom b = LookTestBuilder.Comp<Bloom>(p); b.threshold.Override(1.0f); b.intensity.Override(0.25f); b.scatter.Override(0.8f); b.clamp.Override(65000f);
                ColorAdjustments c = LookTestBuilder.Comp<ColorAdjustments>(p); c.postExposure.Override(-0.70f); c.contrast.Override(30f); c.saturation.Override(0f);
                ShadowsMidtonesHighlights s = LookTestBuilder.Comp<ShadowsMidtonesHighlights>(p); s.shadows.Override(new Vector4(0.95f, 1.0f, 1.0f, 0f));   // slate-teal shadows (3.1)
                s.highlights.Override(new Vector4(1f, 1f, 1f, 0.6f)); s.highlightsStart.Override(0.40f); s.highlightsEnd.Override(1.1f);   // polish1: lift the sunlit highlights only
                LiftGammaGain lg = LookTestBuilder.Comp<LiftGammaGain>(p); lg.lift.Override(new Vector4(0.97f, 1.0f, 1.0f, 0.0f));
                Vignette v = LookTestBuilder.Comp<Vignette>(p); v.intensity.Override(0.18f); v.smoothness.Override(0.5f);
            });
            AssetDatabase.SaveAssets();
        }

        static void BuildSupportMaterials()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder); BuilderUtil.EnsureFolder(TexturesFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Tint(lit, "green", new Color(0.435f, 0.54f, 0.20f), 0.15f);    // foliage yellow-green #6f8a34
            Tint(lit, "dark", new Color(0.18f, 0.15f, 0.13f), 0.25f);
            Tint(lit, "cloth", new Color(0.80f, 0.77f, 0.68f), 0.1f);      // bleached banner
            // Skies per preset (procedural, the sun follows RenderSettings.sun) and the god-ray card material (the LookTest cone shader, fainter than the look test).
            Shader skyShader = Shader.Find("Skybox/Procedural") ?? throw new System.InvalidOperationException("Skybox/Procedural not found");
            Material skyMid = LookTestBuilder.Ensure(MaterialsFolder + "/M_BC_Sky_Midday.mat", skyShader);
            skyMid.SetFloat("_SunSize", 0.04f); skyMid.SetFloat("_SunSizeConvergence", 5f); skyMid.SetFloat("_AtmosphereThickness", 0.78f);   // above about 0.9 the procedural horizon turns lime
            skyMid.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.92f)); skyMid.SetColor("_GroundColor", new Color(0.42f, 0.40f, 0.36f)); skyMid.SetFloat("_Exposure", 1.25f);
            Material skyLow = LookTestBuilder.Ensure(MaterialsFolder + "/M_BC_Sky_LowSun.mat", skyShader);
            skyLow.SetFloat("_SunSize", 0.06f); skyLow.SetFloat("_SunSizeConvergence", 5f); skyLow.SetFloat("_AtmosphereThickness", 1.0f);
            skyLow.SetColor("_SkyTint", new Color(0.66f, 0.56f, 0.64f)); skyLow.SetColor("_GroundColor", new Color(0.42f, 0.38f, 0.33f)); skyLow.SetFloat("_Exposure", 1.0f);
            EditorUtility.SetDirty(skyMid); EditorUtility.SetDirty(skyLow);
            Material cone = LookTestBuilder.Ensure(MaterialsFolder + "/M_BC_LightCone.mat", Shader.Find("SleepingCalamity/LightCone") ?? throw new System.InvalidOperationException("Shader not found: SleepingCalamity/LightCone"));
            cone.SetColor("_Color", new Color(1.0f, 0.86f, 0.62f)); cone.SetFloat("_Intensity", 0.14f); cone.SetFloat("_LengthFade", 0.7f); cone.renderQueue = 3100;
            cone.SetFloat("_HeightFadeTop", 10f); cone.SetFloat("_HeightFadeRange", 5f);   // alpha 1 below 5 m, 0 at 10 m: the beams stop at the roofline
            EditorUtility.SetDirty(cone);
            // Pollen: a soft round dot (RGB falloff, additive), the URP particle unlit shader set to additive transparent.
            string dot = TexturesFolder + "/pollen_dot_albedo.png";
            if (!File.Exists(BuilderUtil.Abs(dot)))
            {
                const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false, true); var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f));
                        byte v = (byte)Mathf.RoundToInt(Mathf.Pow(1f - d, 2f) * 255f); px[y * n + x] = new Color32(v, v, v, 255);
                    }
                t.SetPixels32(px); File.WriteAllBytes(BuilderUtil.Abs(dot), t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(dot, ImportAssetOptions.ForceUpdate);
            }
            Material pollen = LookTestBuilder.Ensure(MaterialsFolder + "/M_Pollen.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? lit);
            pollen.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dot)); pollen.SetColor("_BaseColor", new Color(1f, 0.93f, 0.72f, 1f));
            pollen.SetFloat("_Surface", 1f); pollen.SetFloat("_Blend", 2f); pollen.SetFloat("_SrcBlend", 5f); pollen.SetFloat("_DstBlend", 1f);
            pollen.SetFloat("_SrcBlendAlpha", 1f); pollen.SetFloat("_DstBlendAlpha", 1f); pollen.SetFloat("_ZWrite", 0f);
            pollen.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); pollen.SetOverrideTag("RenderType", "Transparent"); pollen.renderQueue = 3000;
            EditorUtility.SetDirty(pollen);
            AssetDatabase.SaveAssets();
        }

        static void Tint(Shader lit, string key, Color c, float smooth)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_BC_" + key + ".mat", lit);
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }

        internal static Material PlaceholderMat(string key) =>
            AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_BC_" + key + ".mat")
            ?? (key == "warm" ? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/LookTest/M_LampGlow.mat") : LookTestBuilder.Mat(key));

        // --------------------------------------------------------------------------------------------- scene
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("BeautyCorner").transform;
            Transform set = new GameObject("Set").transform; set.SetParent(root, false);

            BeautyCornerArt.EnforceTextureCap();
            Material far = BeautyCornerArt.SurroundGround();   // B6 FarCobble with a macro breakup; the LookTest cobble when that art is missing
            LookTestBuilder.Box(set, "Surround_Ground (scene fill, not a brief row)", new Vector3(0f, -0.17f, 0f), new Vector3(140f, 0.3f, 140f),
                far != null ? far : LookTestBuilder.Mat("cobble"), default, far != null ? BeautyCornerArt.FarTileMetres : 3f);
            foreach (BeautyCornerTable.Row row in BeautyCornerTable.Rows) PlaceRow(set, row);

            Transform lighting = new GameObject("Lighting").transform; lighting.SetParent(root, false);
            Light sun = LookTestBuilder.AddLight(lighting, "Sun", LightType.Directional, new Vector3(0, 10, 0), Quaternion.identity, Color.white, 3f, 10f, LightShadows.Soft);
            LookTestBuilder.AddLight(lighting, "ShadeFill", LightType.Directional, new Vector3(0, 10, 0), Quaternion.Euler(22f, 18f, 0f), new Color(0.78f, 0.84f, 1.0f), 0.5f, 10f, LightShadows.None);   // polish1: soft cool fill from the camera side, no shadows, keeps shaded paint readable
            LookTestBuilder.AddProbe(lighting, new Vector3(0f, 3f, 3f), new Vector3(30f, 12f, 28f));
            LookTestBuilder.AddVolume(lighting, ProfileName);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;   // crisp near field, haze building toward the far buildings (TotK study)
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.sun = sun;

            Transform atmos = new GameObject("Atmosphere").transform; atmos.SetParent(root, false);
            BuildShafts(atmos);
            BuildPollen(atmos);

            Transform cams = new GameObject("CaptureCameras").transform; cams.SetParent(root, false);
            foreach (var c in BeautyCornerTable.Cameras) LookTestBuilder.AddCapture(cams, c.Name, c.Pos, c.Target, c.Fov);

            ApplyPreset(Preset.Midday);
            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC BEAUTY scene built " + ScenePath);
        }

        // Art for one instance: its own folder (a B7 prop variant, Instance.Asset) or the row's folder match. Cached per folder so 3 barrels import 3 variants once each.
        sealed class RowArt { public GameObject Fbx; public Material Mat; public BeautyCornerArt.Found Found; public Dictionary<string, Material> Slots; }

        static RowArt LoadArt(BeautyCornerTable.Row row, string asset, Dictionary<string, RowArt> cache)
        {
            string key = asset ?? "";
            if (cache.TryGetValue(key, out RowArt cached)) return cached;
            RowArt art = null;
            BeautyCornerArt.Found found = null;
            if (asset != null)
            {
                string folder = BeautyCornerArt.FindFolder(row.Batch, asset);
                found = folder != null ? BeautyCornerArt.FindIn(folder) : null;
            }
            else found = BeautyCornerArt.Find(row);
            if (found != null)
            {
                try
                {
                    art = new RowArt { Found = found };
                    string folderName = Path.GetFileName(found.Folder);
                    if (row.Id == "Facade" || BeautyCornerArt.IsNeighbour(folderName))   // multi-material: keep the FBX slot names and map them to the shared-tileable materials
                    {
                        art.Fbx = BeautyCornerArt.LoadModel(found.Folder, Path.GetFileName(found.Fbx), keepMaterialSlots: true);
                        art.Slots = BeautyCornerArt.IsNeighbour(folderName) ? BeautyCornerArt.BuildNeighbourMaterials(folderName) : BeautyCornerArt.BuildFacadeMaterials();
                    }
                    else art.Fbx = BeautyCornerArt.Prepare(found, asset ?? row.Id, row.Foliage, out art.Mat);
                    if (art.Fbx == null) art = null;
                }
                catch (System.Exception e) { Debug.LogWarning("SC BEAUTY art for " + row.Id + (asset != null ? "/" + asset : "") + " failed, using placeholder: " + e.Message); art = null; }
            }
            Debug.Log("SC BEAUTY row=" + row.Id + (asset != null ? " asset=" + asset : "") + " batch=" + row.Batch + " art=" + (art != null ? found.Fbx : "placeholder"));
            cache[key] = art;
            return art;
        }

        // Also used by HollowmereBuilder for the corner rows that stand in the town square (same art lookup, same greybox fallback).
        internal static void PlaceRow(Transform set, BeautyCornerTable.Row row)
        {
            Transform group = new GameObject(row.Id).transform; group.SetParent(set, false);
            if (row.Scatter && BeautyCornerGround.TryPlace(group, row)) { Debug.Log("SC BEAUTY row=" + row.Id + " batch=" + row.Batch + " art=scatter"); return; }
            var cache = new Dictionary<string, RowArt>();
            for (int i = 0; i < row.Instances.Length; i++)
            {
                BeautyCornerTable.Instance inst = row.Instances[i];
                RowArt art = LoadArt(row, inst.Asset, cache);
                string tag = row.Instances.Length > 1 ? row.Id + "_" + (i + 1) : row.Id;
                var go = new GameObject((art != null || row.Id.StartsWith("NEIGHBOUR_") ? "" : "PLACEHOLDER_") + tag);
                go.transform.SetParent(group, false);
                go.transform.localPosition = inst.Pos; go.transform.localRotation = Quaternion.Euler(0f, inst.Yaw, 0f); go.transform.localScale = Vector3.one * inst.Scale;
                if (art != null)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(art.Fbx, go.transform);
                    obj.name = "Art_" + (inst.Asset ?? row.Id);
                    obj.transform.localRotation = Quaternion.Euler(0f, row.ArtYaw, 0f);
                    foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int k = 0; k < mats.Length; k++)
                        {
                            if (art.Slots == null) { mats[k] = art.Mat; continue; }
                            string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                            int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);   // "FK_Plaster.001"
                            if (!art.Slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC BEAUTY facade: no material for slot '" + slot + "' on " + r.name); mats[k] = PlaceholderMat("dark"); }
                            if (slot.StartsWith("FK_Decal")) r.shadowCastingMode = ShadowCastingMode.Off;
                        }
                        r.sharedMaterials = mats;
                    }
                    if (art.Slots != null && i == 0)   // the wear shader needs the FBX vertex colour: say how many kit meshes kept it
                    {
                        int total = 0, withColor = 0;
                        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>(true))
                        { total++; if (mf.sharedMesh != null && mf.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)) withColor++; }
                        Debug.Log("SC BEAUTY facade meshes=" + total + " with vertex colour (wear)=" + withColor);
                    }
                    continue;
                }
                int p = 0;
                foreach (BeautyCornerTable.Part part in row.Placeholder)
                {
                    string pn = "part" + (p++);
                    Material m = PlaceholderMat(part.Mat);
                    if (part.Shape == BeautyCornerTable.PartShape.Box)
                        LookTestBuilder.Box(go.transform, pn, part.Pos, part.Size, m, part.Euler, 2f);
                    else
                        LookTestBuilder.Prim(go.transform, pn, PrimitiveType.Cylinder, part.Pos, new Vector3(part.Size.x, part.Size.y * 0.5f, part.Size.z), m);
                }
            }
        }

        // God rays without a volumetric package: additive light-cone cards. The Shafts root sits on the plaza and is rotated to the sun by ApplyPreset;
        // each cone's apex is 10 m up-sun of its lateral offset and runs 15 m along the light.
        static void BuildShafts(Transform atmos)
        {
            Transform shafts = new GameObject("Shafts").transform; shafts.SetParent(atmos, false);
            shafts.position = new Vector3(-1f, 0f, 7.5f);   // the lit ground at the facade foot: beams cross the facade front and the two gaps between buildings
            Material cone = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_BC_LightCone.mat");
            float[,] offs = { { -5.5f, 1.2f, 1.1f }, { -3.6f, 2.8f, 1.4f }, { -1.0f, 0.8f, 1.2f }, { 1.4f, 2.4f, 1.5f }, { 3.4f, 1.0f, 1.1f } };
            for (int i = 0; i < offs.GetLength(0); i++)
                LookTestBuilder.Shape(shafts, "Shaft" + i, SleepingCalamity.Presentation.LookTestGeometry.Shape.Cone, new Vector3(offs[i, 0], offs[i, 1], -6f), new Vector3(offs[i, 2], 0f, 12f), cone, default, 2f, 16, false);
        }

        static void BuildPollen(Transform atmos)
        {
            var go = new GameObject("Pollen"); go.transform.SetParent(atmos, false); go.transform.localPosition = new Vector3(-1f, 1.1f, 2f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 12f; main.loop = true; main.prewarm = true; main.startLifetime = 14f; main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f); main.startColor = new Color(1f, 0.93f, 0.72f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 260; main.gravityModifier = 0f;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 20f;
            ParticleSystem.ShapeModule shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24f, 1.5f, 20f);   // below eye height (go.y 1.1 -> 0.35..1.85 m): against the sky they read as stars
            ParticleSystem.NoiseModule noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.25f; noise.scrollSpeed = 0.1f;
            ps.useAutoRandomSeed = false; ps.randomSeed = 7;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Pollen.mat"); r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        // --------------------------------------------------------------------------------------------- lighting presets
        // Midday: PHASE1 3.5 day (sun about 5200 K, cool sky fill). LowSun: the sun about 12 degrees up, warmer and weaker, warmer haze, a neutral-warm fill.
        public static void ApplyPreset(Preset preset)
        {
            Light sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindFirstObjectByType<Light>();
            if (sun == null) throw new System.InvalidOperationException("No sun in the open scene");
            Quaternion rot;
            if (preset == Preset.Midday)
            {
                rot = Quaternion.Euler(54f, -32f, 0f);
                sun.color = new Color(1.0f, 0.91f, 0.83f); sun.intensity = 4.5f;     // about 5200 K
                RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.90f); RenderSettings.fogDensity = 0.016f;
                RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.64f); RenderSettings.ambientEquatorColor = new Color(0.47f, 0.48f, 0.49f);
                RenderSettings.ambientGroundColor = new Color(0.29f, 0.26f, 0.22f);
            }
            else
            {
                rot = Quaternion.Euler(12f, -32f, 0f);
                sun.color = new Color(1.0f, 0.68f, 0.42f); sun.intensity = 2.8f;
                RenderSettings.fogColor = new Color(0.82f, 0.68f, 0.58f); RenderSettings.fogDensity = 0.016f;
                RenderSettings.ambientSkyColor = new Color(0.32f, 0.33f, 0.38f); RenderSettings.ambientEquatorColor = new Color(0.34f, 0.29f, 0.24f);
                RenderSettings.ambientGroundColor = new Color(0.20f, 0.17f, 0.14f);
            }
            Volume post = Object.FindFirstObjectByType<Volume>();
            if (post != null) post.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Content/Rendering/" + (preset == Preset.Midday ? ProfileName : LowSunProfileName) + ".asset");
            GameObject fillGo = GameObject.Find("ShadeFill");
            if (fillGo != null) { Light fill = fillGo.GetComponent<Light>(); fill.intensity = preset == Preset.Midday ? 0.35f : 0.22f; fill.color = preset == Preset.Midday ? new Color(0.78f, 0.84f, 1.0f) : new Color(1.0f, 0.86f, 0.72f); }
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + (preset == Preset.Midday ? "/M_BC_Sky_Midday.mat" : "/M_BC_Sky_LowSun.mat"));
            sun.transform.rotation = rot;
            GameObject shafts = GameObject.Find("Shafts");
            if (shafts != null) shafts.transform.rotation = rot;
            DynamicGI.UpdateEnvironment();
        }
    }
}
