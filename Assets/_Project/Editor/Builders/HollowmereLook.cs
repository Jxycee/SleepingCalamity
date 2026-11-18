// Hollowmere's whole-town look (Q12): the beauty corner's light presets (BeautyCornerBuilder.ApplyPreset: sun, fill, ambient, lamps) with the town-scale
// overrides on top. The corner's haze, sky and grade are tuned for 10-40 m inside one plaza; the town is seen at 30-300 m with far more open sky, so
// Hollowmere gets its own copies (HM_* profiles and skies) and never edits the corner's BeautyCorner_* profiles or M_BC_Sky_* materials (the approved corner
// keeps its look). Apply(preset) is the only entry point: HollowmereBuilder's final preset and HollowmereCapture call it.
// Tuning: Look.Parse reads "key=value,..." overrides (HollowmereCapture.CaptureVariants, env SC_LOOK_VARIANTS) so one Unity run compares several looks.
// Authority: none (editor-time scene building).
using System.Globalization;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereLook
    {
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        public const string DayProfileName = "HM_Day_Profile", LowSunProfileName = "HM_LowSun_Profile";

        // One preset's town-scale knobs. The defaults are the Q12 choice; the comments give the corner's value they replace.
        public sealed class Look
        {
            public float Exposure, Contrast, Saturation, Temperature;          // grade (corner midday -0.70 / 30 / 0 / 0, low sun -0.55 / 30 / 0 / 0)
            public float FogDensity; public Color FogColor;                     // exponential squared (corner 0.016 at both presets)
            public float OverviewFog;                                           // the overview camera's own density (corner: fog off there)
            public float SkyThickness, SkyExposure; public Color SkyTint;       // procedural sky
            public float Ambient = 1f;                                          // scales the corner's trilight colours
            public float Sun = 1f;                                              // town Morning only: scales the corner's sun intensity

            public Look Clone() => (Look)MemberwiseClone();

            // "exposure=-0.9,fog=0.01,fogcol=0.8/0.8/0.9" -> a copy with those fields changed (unknown keys throw so a typo can't pass silently).
            public Look Parse(string spec)
            {
                Look l = Clone();
                if (string.IsNullOrEmpty(spec)) return l;
                foreach (string kv in spec.Split(','))
                {
                    string[] p = kv.Split('='); if (p.Length != 2) throw new System.ArgumentException("look override '" + kv + "'");
                    string k = p[0].Trim().ToLowerInvariant(), v = p[1].Trim();
                    switch (k)
                    {
                        case "exposure": l.Exposure = F(v); break;
                        case "contrast": l.Contrast = F(v); break;
                        case "saturation": l.Saturation = F(v); break;
                        case "temperature": l.Temperature = F(v); break;
                        case "fog": l.FogDensity = F(v); break;
                        case "fogcol": l.FogColor = C(v); break;
                        case "overviewfog": l.OverviewFog = F(v); break;
                        case "skythick": l.SkyThickness = F(v); break;
                        case "skyexp": l.SkyExposure = F(v); break;
                        case "skytint": l.SkyTint = C(v); break;
                        case "ambient": l.Ambient = F(v); break;
                        case "sun": l.Sun = F(v); break;
                        default: throw new System.ArgumentException("unknown look key '" + k + "'");
                    }
                }
                return l;
            }

            static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
            static Color C(string s) { string[] c = s.Split('/'); return new Color(F(c[0]), F(c[1]), F(c[2])); }
        }

        public static readonly Look Midday = new Look
        {
            Exposure = -0.85f, Contrast = 36f, Saturation = 0f, Temperature = 6f, Ambient = 0.85f,             // the corner's crops sat +0.07..0.09 brighter in the town
            FogDensity = 0.012f, FogColor = new Color(0.78f, 0.80f, 0.84f), OverviewFog = 0.0025f,             // haze builds over 40-150 m, the colour of the pale horizon
            SkyThickness = 1.0f, SkyExposure = 1.3f, SkyTint = new Color(0.5f, 0.5f, 0.5f),                     // a neutral tint loses the corner sky's lime horizon band
        };

        public static readonly Look LowSun = new Look
        {
            Exposure = -0.55f, Contrast = 30f, Saturation = 0f, Temperature = 8f,
            FogDensity = 0.011f, FogColor = new Color(0.85f, 0.70f, 0.58f), OverviewFog = 0.003f,
            SkyThickness = 1.0f, SkyExposure = 1.0f, SkyTint = new Color(0.5f, 0.45f, 0.5f),
        };

        // B3: Morning and Evening are the B0 states (design section 4) and take the town. They keep the corner's own profile, HDRI sky, fog and ambient (BeautyCornerBuilder.ApplyPreset);
        // the Look only carries the town's overview haze (FogDensity is the play-distance density, from the design table). Midday and LowSun stay for the old captures.
        // Area 1 rework (Run 6, decided 2026-11-16): Morning in the town is no longer the corner's light unchanged. The baseline read flat: the sun (42, -32) lit
        // the north view from the front, the shade sides went black (sky ambient 0.40), the highlights stayed dull (p90 .67) and the HDRI's hard blue sat
        // over a pale band. The town's Morning keeps the corner's sun colour, cookie and profile and changes, on the town scene only:
        //   - the sun lower and from the east (TownMorningSun), so the walk north is side-lit and trees and walls throw long shadows across the ground;
        //   - its own sky copy M_HM_Sky_Morning (the corner's HDRI turned with the sun, SkyExposure, SkyTint as the panoramic tint);
        //   - Ambient = the sky ambient intensity (corner 0.40); Exposure = stops ADDED to the corner's Morning profile (copy HM_Morning_Profile);
        //   - FogDensity (corner 0.0048, plain exponential) and FogColor, mixed half and half with the corner's horizon fog (black = keep the corner's).
        // The corner scene, BeautyCornerBuilder's presets and M_BC_* are never touched.
        public static readonly Quaternion TownMorningSun = Quaternion.Euler(33f, -68f, 0f);
        public const string MorningProfileName = "HM_Morning_Profile";
        public static readonly Look Morning = new Look
        {
            FogDensity = 0.0062f, OverviewFog = 0.0016f, FogColor = new Color(0.84f, 0.82f, 0.74f),
            Exposure = 0.25f, Ambient = 0.56f, SkyExposure = 1.18f, SkyTint = new Color(0.53f, 0.51f, 0.47f),
            Sun = 1.38f, Contrast = 8f,   // round 2: the sunlit side was still dull (p90 .66-.71, target .80-.86): a stronger sun and a little grade contrast (ADDED to the corner's 8).
                                          // Round 3: p90 reached .70-.76 with p10 .26-.30 (over the .21-.25 target), so more sun (1.22 -> 1.38) and a little less sky fill (0.62 -> 0.56)
        };
        public static readonly Look Evening = new Look { FogDensity = 0.006f, OverviewFog = 0.002f };

        public static Look For(BeautyCornerBuilder.Preset preset)
        {
            switch (preset)
            {
                case BeautyCornerBuilder.Preset.Midday: return Midday;
                case BeautyCornerBuilder.Preset.LowSun: return LowSun;
                case BeautyCornerBuilder.Preset.Morning: return Morning;
                case BeautyCornerBuilder.Preset.Evening: return Evening;
                default: throw new System.ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        public static void Apply(BeautyCornerBuilder.Preset preset) => Apply(preset, For(preset));

        // The corner's preset first (sun, fill, ambient colours, lamps), then the town's profile, sky, haze and ambient scale on the open scene.
        public static void Apply(BeautyCornerBuilder.Preset preset, Look look)
        {
            For(preset);   // throws for a preset the town has no look for
            BeautyCornerBuilder.ApplyPreset(preset);
            if (preset == BeautyCornerBuilder.Preset.Morning || preset == BeautyCornerBuilder.Preset.Evening)
            {
                RenderSettings.fogDensity = look.FogDensity;   // Evening: the corner's HDRI sky, profile, fog colour and sky ambient stand as B0 tuned them
                if (preset == BeautyCornerBuilder.Preset.Morning) TownMorning(look);
                Transform sh = GameObject.Find(ShaftsRoot)?.transform;
                if (sh != null && RenderSettings.sun != null) foreach (Transform spot in sh) spot.rotation = RenderSettings.sun.transform.rotation;
                DynamicGI.UpdateEnvironment();
                return;
            }
            bool day = preset == BeautyCornerBuilder.Preset.Midday;
            VolumeProfile profile = EnsureProfile(day ? DayProfileName : LowSunProfileName, day ? BeautyCornerBuilder.ProfileName : BeautyCornerBuilder.LowSunProfileName, look);
            Volume post = LookTestBuilder.GlobalVolume();
            if (post != null) post.sharedProfile = profile;
            RenderSettings.skybox = EnsureSky(day ? "M_HM_Sky_Midday" : "M_HM_Sky_LowSun", day ? "M_BC_Sky_Midday" : "M_BC_Sky_LowSun", look);
            RenderSettings.fogColor = look.FogColor; RenderSettings.fogDensity = look.FogDensity;
            Transform shafts = GameObject.Find(ShaftsRoot)?.transform;   // each spot's cones turn to the sun the corner's preset just set
            if (shafts != null && RenderSettings.sun != null) foreach (Transform spot in shafts) spot.rotation = RenderSettings.sun.transform.rotation;
            RenderSettings.ambientSkyColor *= look.Ambient; RenderSettings.ambientEquatorColor *= look.Ambient; RenderSettings.ambientGroundColor *= look.Ambient;
            DynamicGI.UpdateEnvironment();
        }

        // The town's Morning on top of the corner's preset (see the Morning look above). Runs after BeautyCornerBuilder.ApplyPreset(Morning).
        static void TownMorning(Look look)
        {
            Light sun = RenderSettings.sun;
            float skyTurn = 0f;
            if (sun != null)
            {
                skyTurn = SunAzimuth(TownMorningSun) - SunAzimuth(sun.transform.rotation);
                sun.transform.rotation = TownMorningSun;
                sun.intensity *= look.Sun;   // ApplyPreset just set the corner's value, so this never compounds
            }
            Material cornerSky = RenderSettings.skybox;
            if (cornerSky != null && cornerSky.HasProperty("_Rotation"))
            {
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_Sky_Morning.mat", cornerSky.shader);
                m.CopyPropertiesFromMaterial(cornerSky);
                m.SetFloat("_Rotation", Mathf.Repeat(cornerSky.GetFloat("_Rotation") + skyTurn, 360f));   // B0Fab.SkyRotationDegrees: rotation = the sun's azimuth - the HDRI's own
                if (look.SkyExposure > 0f) m.SetFloat("_Exposure", look.SkyExposure);
                if (look.SkyTint.maxColorComponent > 0f) m.SetColor("_Tint", new Color(look.SkyTint.r, look.SkyTint.g, look.SkyTint.b, 0.5f));
                EditorUtility.SetDirty(m);
                RenderSettings.skybox = m;
            }
            if (look.FogColor.maxColorComponent > 0f) RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, look.FogColor, 0.5f);
            RenderSettings.ambientIntensity = look.Ambient;
            var corner = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookTestBuilder.RenderingFolder + "/" + BeautyCornerBuilder.MorningProfileName + ".asset");
            Volume post = LookTestBuilder.GlobalVolume();
            if (corner != null && post != null)
            {
                bool graded = corner.TryGet(out ColorAdjustments cornerGrade);
                float cornerExposure = graded ? cornerGrade.postExposure.value : 0f, cornerContrast = graded ? cornerGrade.contrast.value : 0f;
                post.sharedProfile = LookTestBuilder.EnsureProfile(MorningProfileName, p =>
                {
                    CopyProfile(corner, p);
                    ColorAdjustments grade = LookTestBuilder.Comp<ColorAdjustments>(p);
                    grade.postExposure.Override(cornerExposure + look.Exposure); grade.contrast.Override(cornerContrast + look.Contrast);
                });
            }
        }

        // The compass angle B0Fab.SkyRotationDegrees turns the sky to: atan2 of the direction toward the sun, in degrees.
        static float SunAzimuth(Quaternion sunRotation)
        {
            Vector3 toSun = -(sunRotation * Vector3.forward);
            return Mathf.Atan2(toSun.z, toSun.x) * Mathf.Rad2Deg;
        }

        static void CopyProfile(VolumeProfile corner, VolumeProfile p)
        {
            foreach (VolumeComponent src in corner.components)
            {
                if (!p.TryGet(src.GetType(), out VolumeComponent dst))
                {
                    dst = p.Add(src.GetType()); dst.name = src.GetType().Name;
                    AssetDatabase.AddObjectToAsset(dst, p);
                }
                dst.active = src.active;
                for (int i = 0; i < src.parameters.Count; i++) { dst.parameters[i].SetValue(src.parameters[i]); dst.parameters[i].overrideState = src.parameters[i].overrideState; }
            }
        }

        // ------------------------------------------------------------------------------------------- atmosphere
        // The corner scene's shafts and pollen sit in BeautyCorner_Hollowmere only; the town gets its own at the places the play cameras look:
        // drifting pollen over the square, the lanes and both gates, and god-ray cones (the corner's M_BC_LightCone cards) through the gate arches and down
        // the lanes, where walls give the beams something to cut past. Spots are layout points; heights come from the terrain.
        public const string ShaftsRoot = "HM_Shafts";
        static readonly (string Name, float X, float Z, float W, float D, float Rate)[] PollenSpots =
        {
            ("Square", 0f, -7f, 40f, 28f, 45f), ("MarketStreet", 0f, -38f, 10f, 26f, 14f), ("LowerGate", -21.3f, -68f, 14f, 14f, 12f),
            ("EastGate", 57f, -48f, 14f, 14f, 12f), ("KeepStairs", -24f, 16f, 16f, 10f, 10f), ("LakeShore", -18f, -84f, 24f, 14f, 14f),
        };
        static readonly (string Name, float X, float Z)[] ShaftSpots =
        {
            ("LowerGate", -21.3f, -64.6f), ("EastGate", 56.6f, -46f), ("KeepStairs", -22f, 17f),   // no open-square or lane spots: beams with no wall beside them float in the sky
        };

        public static void BuildAtmosphere(Transform root, TownLayout t)
        {
            Transform atmos = new GameObject("HM_Atmosphere").transform; atmos.SetParent(root, false);
            Material pollen = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/BeautyCorner/M_Pollen.mat");
            int seed = 11;
            foreach (var s in PollenSpots) Pollen(atmos, s.Name, new Vector3(s.X, HollowmereLayout.Ground(t, s.X, s.Z) + 1.1f, s.Z), new Vector3(s.W, 1.5f, s.D), s.Rate, pollen, seed++);
            Transform shafts = new GameObject(ShaftsRoot).transform; shafts.SetParent(atmos, false);
            Material cone = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/BeautyCorner/M_BC_LightCone.mat");
            float[,] offs = { { -1.0f, 1.4f, 0.9f }, { 0.4f, 0.8f, 1.1f }, { 1.3f, 2.0f, 0.8f } };   // the corner's cone pattern narrowed to a gate passage (about 4 m), three per spot
            foreach (var s in ShaftSpots)
            {
                Transform spot = new GameObject(s.Name).transform; spot.SetParent(shafts, false);
                spot.position = new Vector3(s.X, HollowmereLayout.Ground(t, s.X, s.Z), s.Z);
                for (int i = 0; i < offs.GetLength(0); i++)
                    LookTestBuilder.Shape(spot, "Shaft" + i, SleepingCalamity.Presentation.LookTestGeometry.Shape.Cone, new Vector3(offs[i, 0], offs[i, 1], -6f), new Vector3(offs[i, 2], 0f, 12f), cone, default, 2f, 16, false);
            }
        }

        // T09b scene diet: one pollen prefab, each spot an instance with its own box, rate, particle cap and seed (six 50 KB particle systems became six small overrides).
        internal static void Pollen(Transform parent, string name, Vector3 pos, Vector3 box, float rate, Material mat, int seed)
        {
            GameObject pf = HollowmereAmbientBuilder.ParticlePrefab("Assets/_Project/Content/Prefabs/Hollowmere/HM_Pollen.prefab", () => MakePollen(mat));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent); go.name = "Pollen_" + name; go.transform.position = pos;
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main; main.maxParticles = Mathf.CeilToInt(rate * 14f);
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape; shape.scale = box;
            ps.randomSeed = (uint)seed;
        }

        // The corner's pollen (BeautyCornerBuilder.BuildPollen): soft additive dots below eye height, fixed seed; the box and rate fit the spot (set per instance).
        static GameObject MakePollen(Material mat)
        {
            var go = new GameObject("HM_Pollen");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 12f; main.loop = true; main.prewarm = true; main.startLifetime = 14f; main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f); main.startColor = new Color(1f, 0.93f, 0.72f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200; main.gravityModifier = 0f;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(10f, 1.5f, 10f);
            ParticleSystem.NoiseModule noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.25f; noise.scrollSpeed = 0.1f;
            ps.useAutoRandomSeed = false; ps.randomSeed = 11;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        // The town profile: a copy of the corner's (so later corner grade fixes carry over) with the town's exposure, contrast, saturation and white balance.
        static VolumeProfile EnsureProfile(string name, string cornerName, Look look)
        {
            var corner = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookTestBuilder.RenderingFolder + "/" + cornerName + ".asset") ?? throw new System.IO.FileNotFoundException(cornerName);
            return LookTestBuilder.EnsureProfile(name, p =>
            {
                foreach (VolumeComponent src in corner.components)
                {
                    if (!p.TryGet(src.GetType(), out VolumeComponent dst))
                    {
                        dst = p.Add(src.GetType()); dst.name = src.GetType().Name;
                        AssetDatabase.AddObjectToAsset(dst, p);
                    }
                    dst.active = src.active;
                    for (int i = 0; i < src.parameters.Count; i++) { dst.parameters[i].SetValue(src.parameters[i]); dst.parameters[i].overrideState = src.parameters[i].overrideState; }
                }
                ColorAdjustments ca = LookTestBuilder.Comp<ColorAdjustments>(p);
                ca.postExposure.Override(look.Exposure); ca.contrast.Override(look.Contrast); ca.saturation.Override(look.Saturation);
                LookTestBuilder.Comp<WhiteBalance>(p).temperature.Override(look.Temperature);
            });
        }

        static Material EnsureSky(string name, string cornerName, Look look)
        {
            var corner = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/BeautyCorner/" + cornerName + ".mat") ?? throw new System.IO.FileNotFoundException(cornerName);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", corner.shader);
            m.CopyPropertiesFromMaterial(corner);
            m.SetFloat("_AtmosphereThickness", look.SkyThickness); m.SetFloat("_Exposure", look.SkyExposure); m.SetColor("_SkyTint", look.SkyTint);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
