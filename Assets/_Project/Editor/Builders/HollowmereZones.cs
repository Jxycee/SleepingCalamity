// Hollowmere's light and fog zones (area 1 world T11, plan docs/design/area1-world-build.md section 5, rule 3.8): each district gets its own light and air.
//  1. One local Volume per zone Z1-Z7 (BoxCollider trigger, priority 2, 8 m blend, weight 1) over the global Morning Volume. A local Volume REPLACES the parameters it overrides, so each
//     zone profile (Content/Rendering/HM_Zone_*.asset) is the global Morning grade (read from HM_Morning_Profile) plus the zone's delta, and only the parameters the plan table names are overridden:
//     exposure, white balance, saturation, contrast and, for Z1 and Z3, the shadows/highlights balance. Boxes are placed so that no two zones overlap at full weight (their blend margins may touch).
//  2. Fog: URP fog is one global setting (RenderSettings), so the table's per-zone fog multipliers (x1.2 .. x1.6) are done with soft mist puffs (the HM_Mist prefab: a camera-faded
//     alpha particle system, 36 particles per instance) at the zone's low points: over the lake and shore (Z5), the lanes (Z3), the road (Z6), the churchyard (Z2), the forest floor (Z7).
//  3. Fill: shadowless point fills where shade reads black, the whole area capped at MaxFills (plan rule 3.8, 12).
//  4. Shafts and motes: god-ray cone cards (the town's M_BC_LightCone, parented under HM_Shafts so HollowmereLook.Apply turns them with the sun) in Z2 (one), Z7 (two), and two canopy
//     shafts (orchard, hill forest), with pollen motes (the HM_Pollen prefab) in each and over Z4.
// Cost at runtime: 7 + 4 Volumes (blended on the CPU per frame by the camera), 3 fills, about 28 mist particle systems of up to 36 particles, 18 cone cards, 8 pollen systems. No per-frame scripts.
// Deterministic (fixed positions, table-driven). Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereZones
    {
        public const string RootName = "HM_Zones";
        public const int MaxFills = 12;
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string MistPrefabPath = "Assets/_Project/Content/Prefabs/Hollowmere/HM_Mist.prefab";

        // One zone: the box (centre x/z, size x/z; y is always -45..75), and its delta against the global Morning grade.
        struct Zone
        {
            public string Name; public float X, Z, W, D; public float Exposure, Temperature, Tint, Saturation, Contrast; public Vector4? Shadows, Highlights;
        }

        // The plan section 5 table. Boxes follow the district bounds (design section 4). Z1 holds the square and the top of Market Street; Z3 the west and south Ring Lane;
        // Z4 the orchard (the allotment lots are added from the layout below); Z5 the meadow, shore, jetty and mill; Z6 the road out; Z7 the hill edge (west flank, north crag foot, east flank).
        static readonly Zone[] Zones =
        {
            new Zone { Name = "Z1_WarmOpen_Square", X = -2f, Z = -17f, W = 44f, D = 50f, Highlights = new Vector4(1.0f, 0.985f, 0.95f, 0.10f) },   // the base look; highlights get a shoulder (+0.10) so the lit stone climbs toward p90 .80
            new Zone { Name = "Z2_ShadePocket", X = -5.5f, Z = 32f, W = 65f, D = 28f, Exposure = 0.30f, Temperature = -7f, Saturation = -2f },        // exposure +0.3, cooler fill (fog x1.4: mist + one shaft)
            new Zone { Name = "Z3_DappledLanes_West", X = -39f, Z = -15f, W = 26f, D = 59f, Temperature = -3f, Tint = -5f, Shadows = new Vector4(0.80f, 0.93f, 0.88f, -0.12f) },   // slightly green-cool shadows (fog x1.2)
            new Zone { Name = "Z3_DappledLanes_South", X = 2f, Z = -50f, W = 64f, D = 12f, Temperature = -3f, Tint = -5f, Shadows = new Vector4(0.80f, 0.93f, 0.88f, -0.12f) },
            new Zone { Name = "Z4_GoldenGreen_Orchard", X = 39f, Z = -49f, W = 24f, D = 20f, Temperature = 10f, Saturation = 8f },                      // warmer white balance, foliage saturation +8, pollen
            new Zone { Name = "Z5_BrightHaze_Shore", X = 19f, Z = -93f, W = 122f, D = 58f, Exposure = -0.15f, Temperature = -8f, Contrast = -4f },       // exposure -0.15, cooler (fog x1.5 near the water: mist cards)
            new Zone { Name = "Z6_Distance_Road", X = -91f, Z = -90f, W = 98f, D = 56f, Contrast = -8f, Saturation = -8f, Temperature = -5f },           // aerial haze: less contrast and colour, cooler, mist rising westward
            new Zone { Name = "Z7_DeepGreen_West", X = -60.5f, Z = 30f, W = 43f, D = 52f, Exposure = -0.12f, Temperature = -4f, Tint = -8f, Saturation = 6f },   // exposure -0.2, deep green (fog x1.6, two shafts)
            new Zone { Name = "Z7_DeepGreen_North", X = -5.5f, Z = 63f, W = 65f, D = 33f, Exposure = -0.20f, Temperature = -4f, Tint = -8f, Saturation = 6f },
            new Zone { Name = "Z7_DeepGreen_East", X = 49f, Z = 30f, W = 42f, D = 52f, Exposure = -0.20f, Temperature = -4f, Tint = -8f, Saturation = 6f },
        };

        // fills: name, x, z, height above ground, colour, intensity, range
        static readonly (string Name, float X, float Z, float Up, Color C, float I, float R)[] Fills =
        {
            ("HillWestFill", -38f, 29f, 5.5f, new Color(0.78f, 0.88f, 0.98f), 13.0f, 20f),      // the HillWest scarp and the crag foot, in shade all morning
            ("TerraceNorthFill", -8f, 27f, 2.6f, new Color(0.80f, 0.87f, 1.0f), 4.5f, 12f),     // the north side of the keep terrace under the church wall
            ("MillShadeFill", 64f, -53f, 2.8f, new Color(0.80f, 0.88f, 1.0f), 5.0f, 11f),       // the mill's shade wall (north-west face)
        };

        // shafts: name, x, z (where the beam meets the ground), narrower cone set for a canopy gap
        static readonly (string Name, float X, float Z)[] ShaftSpots =
        {
            ("Z2_Churchyard", -17f, 27.5f), ("Z7_WestForest", -68f, 46f), ("Z7_EastForest", 47f, 40f), ("Canopy_Orchard", 40f, -52f), ("Canopy_HillForest", 4f, 56f),
        };

        public static void Build(Transform root, TownLayout t)
        {
            Transform old = root.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform zones = new GameObject(RootName).transform; zones.SetParent(root, false);
            var morning = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookTestBuilder.RenderingFolder + "/" + HollowmereLook.MorningProfileName + ".asset");
            Base b = ReadBase(morning);
            Transform vols = Sub(zones, "Volumes");
            foreach (Zone z in Zones) MakeVolume(vols, z, b);

            // fills (the scene's other fills are listed by their names: BellYardFill, ChurchyardFill, WestBankFill, OrchardFill)
            Transform fills = Sub(zones, "Fills");
            foreach (var f in Fills)
            {
                var go = new GameObject(f.Name); go.transform.SetParent(fills, false);
                go.transform.position = new Vector3(f.X, HollowmereLayout.Ground(t, f.X, f.Z) + f.Up, f.Z);
                Light l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = f.C; l.intensity = f.I; l.range = f.R; l.shadows = LightShadows.None;
            }
            int fillCount = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(l => l.type != LightType.Directional && l.name.Contains("Fill"));
            if (fillCount > MaxFills) throw new System.InvalidOperationException("T11: " + fillCount + " fill lights exceed the cap of " + MaxFills);

            BuildMist(Sub(zones, "Mist"), t);
            BuildShafts(root, Sub(zones, "Motes"), t);
            BuildPollen(Sub(zones, "Motes"), t);
            Debug.Log("SC HOLLOWMERE zones: " + Zones.Length + " volumes, fill lights in the scene=" + fillCount + " (cap " + MaxFills + ")");
        }

        static Transform Sub(Transform p, string n) { Transform c = p.Find(n); if (c != null) return c; c = new GameObject(n).transform; c.SetParent(p, false); return c; }

        // ------------------------------------------------------------------------------------------------ volumes
        struct Base { public float Exposure, Contrast, Saturation, Temperature, Tint; }

        static Base ReadBase(VolumeProfile p)
        {
            var b = new Base();
            if (p == null) return b;
            if (p.TryGet(out ColorAdjustments ca)) { b.Exposure = ca.postExposure.value; b.Contrast = ca.contrast.value; b.Saturation = ca.saturation.value; }
            if (p.TryGet(out WhiteBalance wb)) { b.Temperature = wb.temperature.value; b.Tint = wb.tint.value; }
            return b;
        }

        static T Comp<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet(out T c)) return c;
            c = p.Add<T>(false); c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void MakeVolume(Transform parent, Zone z, Base b)
        {
            string profileName = "HM_Zone_" + z.Name;
            VolumeProfile profile = LookTestBuilder.EnsureProfile(profileName, p =>
            {
                ColorAdjustments ca = Comp<ColorAdjustments>(p); WhiteBalance wb = Comp<WhiteBalance>(p); ShadowsMidtonesHighlights smh = Comp<ShadowsMidtonesHighlights>(p);
                foreach (VolumeComponent c in new VolumeComponent[] { ca, wb, smh }) foreach (VolumeParameter vp in c.parameters) vp.overrideState = false;   // a rebuilt zone never keeps a stale override
                if (z.Exposure != 0f) ca.postExposure.Override(b.Exposure + z.Exposure);
                if (z.Contrast != 0f) ca.contrast.Override(b.Contrast + z.Contrast);
                if (z.Saturation != 0f) ca.saturation.Override(b.Saturation + z.Saturation);
                if (z.Temperature != 0f) wb.temperature.Override(b.Temperature + z.Temperature);
                if (z.Tint != 0f) wb.tint.Override(b.Tint + z.Tint);
                if (z.Shadows.HasValue) smh.shadows.Override(z.Shadows.Value);
                if (z.Highlights.HasValue) smh.highlights.Override(z.Highlights.Value);
            });
            var go = new GameObject(z.Name); go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(z.X, 15f, z.Z);
            var col = go.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(z.W, 120f, z.D);
            Volume v = go.AddComponent<Volume>(); v.isGlobal = false; v.priority = 2f; v.weight = 1f; v.blendDistance = 8f; v.sharedProfile = profile;
        }

        // ------------------------------------------------------------------------------------------------ mist
        // (name, x, z, box x, box y, box z, rate per second, lift above the ground or the water, water?)
        static readonly (string Name, float X, float Z, float W, float H, float D, float Rate, float Lift, bool Water)[] MistSpots =
        {
            // Z5: over the lake (surface) and the shore
            ("Lake_W", -28f, -100f, 46f, 1.4f, 16f, 1.6f, 0.5f, true), ("Lake_C", 12f, -104f, 60f, 1.4f, 18f, 1.8f, 0.5f, true), ("Lake_E", 46f, -100f, 40f, 1.4f, 16f, 1.4f, 0.5f, true),
            ("Shore", 8f, -83f, 90f, 1.6f, 10f, 1.4f, 0.6f, false),
            // Z3: the lanes' low points, morning
            ("Lane_West", -40f, -30f, 12f, 1.2f, 22f, 0.8f, 0.5f, false), ("Lane_South", 8f, -50f, 30f, 1.2f, 8f, 0.7f, 0.5f, false),
            // Z6: haze rising westward along the road
            ("Road_2", -78f, -85f, 40f, 3.4f, 24f, 1.6f, 0.8f, false), ("Road_3", -106f, -93f, 28f, 4.6f, 22f, 1.4f, 0.8f, false),
            // Z2: the churchyard and bell yard, low
            ("Churchyard", -22f, 32f, 16f, 1.2f, 10f, 0.6f, 0.6f, false), ("BellYard", 12f, 32f, 8f, 1.0f, 5f, 0.4f, 0.5f, false),
            // Z7: the forest floor
            ("Forest_W", -58f, 38f, 30f, 2.0f, 26f, 1.1f, 0.8f, false), ("Forest_N", 0f, 62f, 50f, 2.0f, 22f, 1.2f, 0.8f, false),
        };

        static void BuildMist(Transform parent, TownLayout t)
        {
            Material mat = MistMat();
            GameObject pf = HollowmereAmbientBuilder.ParticlePrefab(MistPrefabPath, () => MakeMist(mat));
            int seed = 501;
            foreach (var m in MistSpots)
            {
                float y = m.Water ? HollowmereLayout.WaterY : HollowmereLayout.Ground(t, m.X, m.Z);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent); go.name = "Mist_" + m.Name;
                go.transform.position = new Vector3(m.X, y + m.Lift + m.H * 0.5f, m.Z);
                var ps = go.GetComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = m.Rate;
                ParticleSystem.ShapeModule sh = ps.shape; sh.scale = new Vector3(m.W, m.H, m.D);
                ps.randomSeed = (uint)seed++;
            }
        }

        // Large soft low puffs (9-15 m) at very low alpha, drifting slowly with the wind; the material fades them out near the camera so a puff never fills the frame.
        static GameObject MakeMist(Material mat)
        {
            var go = new GameObject("HM_Mist");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 24f; main.loop = true; main.prewarm = true; main.startLifetime = new ParticleSystem.MinMaxCurve(22f, 30f); main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(9f, 15f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.2832f);
            main.startColor = new Color(0.88f, 0.92f, 0.96f, 1f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 36; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = 1f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(20f, 1.5f, 20f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector2 w = HollowmereAmbientLife.WindDirection;
            vel.x = new ParticleSystem.MinMaxCurve(0.35f * w.x); vel.y = new ParticleSystem.MinMaxCurve(0.03f); vel.z = new ParticleSystem.MinMaxCurve(0.35f * w.y);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            ps.useAutoRandomSeed = false; ps.randomSeed = 501u;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        static Material MistMat()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? throw new System.InvalidOperationException("URP Particles/Unlit not found");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Content/Textures/Hollowmere/Ambient/T_HM_SmokePuff.png") ?? throw new System.IO.FileNotFoundException("T_HM_SmokePuff.png (built by HollowmereAmbientBuilder)");
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_Mist.mat", sh);
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(0.86f, 0.90f, 0.95f, 0.15f));   // with the sprite's alpha working a puff peaks at about 0.76 of this and falls off to its edge
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 10f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetFloat("_CameraFadingEnabled", 1f); m.SetFloat("_CameraNearFadeDistance", 4f); m.SetFloat("_CameraFarFadeDistance", 14f); m.EnableKeyword("_FADING_ON");
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------------ shafts and motes
        static void BuildShafts(Transform root, Transform motes, TownLayout t)
        {
            Transform shafts = GameObject.Find(HollowmereLook.ShaftsRoot)?.transform;
            if (shafts == null) { Debug.LogWarning("SC HOLLOWMERE zones: HM_Shafts missing, no zone shafts"); return; }
            Material beam = ShaftBeamMat();
            Quaternion sunRot = RenderSettings.sun != null ? RenderSettings.sun.transform.rotation : HollowmereLook.TownMorningSun;
            float[,] offs = { { -1.2f, 1.6f, 1.0f }, { 0.5f, 1.0f, 1.3f }, { 1.6f, 2.2f, 0.9f } };   // the gate cones' pattern, a little wider for a canopy gap
            Material pollen = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/BeautyCorner/M_Pollen.mat");
            int seed = 90;
            foreach (var s in ShaftSpots)
            {
                if (shafts.Find("Zone_" + s.Name) != null) continue;
                Transform spot = new GameObject("Zone_" + s.Name).transform; spot.SetParent(shafts, false);
                spot.position = new Vector3(s.X, HollowmereLayout.Ground(t, s.X, s.Z), s.Z); spot.rotation = sunRot;
                // T11: two crossed soft beams per cone position (the gate cones' flat end caps read as hard translucent triangles from 12-20 m): quads along the sun direction, a soft profile across and along, no caps
                for (int i = 0; i < 1; i++)
                    for (int k = 0; k < 2; k++)
                    {
                        GameObject q = LookTestBuilder.Prim(spot, "Beam" + i + "_" + k, PrimitiveType.Quad, new Vector3(offs[i, 0], offs[i, 1], -6f), new Vector3(offs[i, 2] * 2.6f, 12f, 1f), beam);
                        q.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * k) * Quaternion.Euler(90f, 0f, 0f);
                        q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                // motes drifting in the beam: a small pollen box at the beam's foot (the two canopy shafts have none: scene budget)
                if (s.Name.StartsWith("Z"))
                HollowmereLook.Pollen(motes, "Shaft_" + s.Name, new Vector3(s.X, HollowmereLayout.Ground(t, s.X, s.Z) + 1.3f, s.Z), new Vector3(5f, 2.6f, 5f), 7f, pollen, seed++);
            }
        }

        // The beam card: additive, a soft bell across the width, fading in from the sun end and out before the ground, so no edge or end is ever a line.
        static Material ShaftBeamMat()
        {
            const int W = 64, H = 128;
            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float u = (i + 0.5f) / W * 2f - 1f, v = (j + 0.5f) / H;
                    float across = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.7f);
                    float along = Mathf.SmoothStep(0f, 1f, v / 0.28f) * (1f - Mathf.SmoothStep(0f, 1f, (v - 0.55f) / 0.45f));
                    px[j * W + i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(across * along * 255f), 0, 255));
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false); tex.SetPixels32(px); tex.Apply();
            string path = "Assets/_Project/Content/Textures/Hollowmere/Ambient/T_HM_ShaftBeam.png";
            byte[] png = tex.EncodeToPNG(); Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(path);
            if (!System.IO.File.Exists(abs) || !System.IO.File.ReadAllBytes(abs).SequenceEqual(png)) { System.IO.File.WriteAllBytes(abs, png); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            else if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.alphaSource != TextureImporterAlphaSource.FromInput) { imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.SaveAndReimport(); }
            return HollowmereAmbientBuilder.AdditiveMat("M_HM_ZoneBeam", AssetDatabase.LoadAssetAtPath<Texture2D>(path), new Color(0.45f, 0.36f, 0.21f, 1f));   // 0.9 drew a hard golden streak through the Churchyard tree
        }

        static void BuildPollen(Transform motes, TownLayout t)
        {
            Material pollen = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/BeautyCorner/M_Pollen.mat");
            int seed = 120;
            // Z4: the orchard (the allotments' butterflies are the garden flocks of the ambient builder; their pollen is the town's Square-pattern drift)
            HollowmereLook.Pollen(motes, "Z4_Orchard", new Vector3(39f, HollowmereLayout.Ground(t, 39f, -49f) + 1.2f, -49f), new Vector3(22f, 2.2f, 18f), 18f, pollen, seed++);
        }
    }
}
