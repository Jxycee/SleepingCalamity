// The Q4 dungeon corner (Reference/hollowmere/dungeon_corner/CONTRACT.md): DungeonCorner_Cellars.unity, an 8 m barrel-vault cellar built from the
// CellarVault kit (Assets/_Project/Content/Art/Cellar/B5/CellarVault/CellarVault.fbx, one child mesh per piece, each with its pivot at the world origin,
// cloned to every placement), with a greybox stand-in for each piece while the FBX (or that child) is missing. Kit slots (CV_*) map to WearLit materials
// tinted from the LookTest B2 tileables; an unknown slot throws, naming it. The two B11 cellar lanterns hang on wall hooks and are lit (plus a candle
// cluster, the breath grate's glow and a cool-neutral daylight shaft from the coal hatch), warm dust motes, trilight ambient, fog, a tuned copy of the
// cellar volume profile and a reflection probe. Three capture cameras (DungeonCornerCapture renders them) and a hidden 1.75 m debug figure.
// Idempotent: the scene is rebuilt through BuilderUtil.SaveScene, materials and the profile are updated in place.
using System;
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
    public static class DungeonCornerBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/DungeonCorner_Cellars.unity";
        public const string KitFolder = "Assets/_Project/Content/Art/Cellar/B5/CellarVault";
        public const string KitFbx = "CellarVault.fbx";
        const string LanternFolder = "Assets/_Project/Content/Art/Cellar/B11/CellarLantern";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/DungeonCorner";
        internal const string ProfileName = "DungeonCorner_Profile";
        const string WearShaderName = "SleepingCalamity/WearLit";

        // piece -> LookTest source material and linear tint (the generator's tile_mat tint). Add a row when the controller sees "no material for slot".
        static readonly (string Slot, string Source, Color Tint)[] Slots =
        {
            ("CV_Brick", "old_brick", new Color(0.64f, 0.84f, 0.55f)), ("CV_VaultStone", "vault_stone", new Color(0.66f, 0.83f, 0.57f)),   // round 4: (0.70,1,0.84) and (0.80,1,0.55) both stayed salmon: red cut to half so R~G>B reads honey-tan/ochre
            ("CV_Limewash", "limewash", new Color(1.06f, 1.0f, 0.94f)), ("CV_Flagstone", "flagstone", new Color(1.0f, 0.95f, 0.9f)),
            ("CV_BarrelOak", "barrel_oak", new Color(1.1f, 1.0f, 0.9f)), ("CV_Soot", "soot_coal", new Color(1.1f, 1.05f, 1.0f)),
            ("CV_RootBark", "root_bark", new Color(2.0f, 1.65f, 1.25f)), ("CV_Oak", "oak_timber", new Color(0.85f, 0.74f, 0.62f)),
            ("CV_Iron", "iron", new Color(0.62f, 0.58f, 0.55f)), ("CV_Mortar", "vault_stone", new Color(0.22f, 0.19f, 0.16f)),
            ("CV_Clay", "limewash", new Color(1.05f, 0.58f, 0.34f)),
        };

        // B0: the Quixel sets that replace a LookTest tileable on a slot, with the repeats per UV unit. The kit's UVs are metres / 2 (one repeat = 2 m, checked by LogUvDensity),
        // so a 2 m scan (C02 Stone Floor) takes 1 and a 1.8 m scan (C01 Old Brick Wall) takes 2 / 1.8 = 1.111, which keeps both at their scanned 1024 px/m.
        static readonly (string Slot, string Folder, string Prefix, float Tiles)[] FabSlots =
        {
            ("CV_Brick", "C01_old_brick_wall", "C01_brick", 2f / 1.8f), ("CV_VaultStone", "C01_old_brick_wall", "C01_brick", 2f / 1.8f),
            ("CV_Flagstone", "C02_stone_floor", "C02_floor", 1f),
        };

        // flat slots: linear colour, smoothness = 1 - roughness
        static readonly (string Slot, Color Linear, float Smooth)[] FlatSlots =
        {
            ("CV_GlazeAmber", new Color(0.26f, 0.095f, 0.022f), 0.83f), ("CV_GlazeCream", new Color(0.62f, 0.46f, 0.24f), 0.75f),
            ("CV_Cheese", new Color(0.55f, 0.30f, 0.05f), 0.45f), ("CV_Glass", new Color(0.10f, 0.15f, 0.03f), 0.95f),
        };

        // piece, position, yaw (degrees). Unity +Z runs along the vault. Contract "Scene" section.
        static readonly (string Piece, Vector3 Pos, float Yaw)[] Placements = BuildPlacements();

        static (string, Vector3, float)[] BuildPlacements()
        {
            var l = new List<(string, Vector3, float)>();
            for (int i = 0; i < 4; i++) l.Add(("VaultSegment", new Vector3(0, 0, 2 * i), 0));
            for (int c = -1; c <= 1; c++) for (int i = 0; i < 4; i++) l.Add(("FloorTile", new Vector3(2 * c, 0, 2 * i), 0));   // 3 wide: the 5 m span plus the wall feet
            foreach (float z in new[] { 0f, 4f, 8f }) l.Add(("ArchRib", new Vector3(0, 0, z), 0));
            l.Add(("StairEntry", Vector3.zero, 0));
            l.Add(("NicheWall", new Vector3(0, 0, 8), 180));
            l.Add(("LarderShelf", new Vector3(0, 0, 1), 0)); l.Add(("LarderShelf", new Vector3(0, 0, 3), 0));
            l.Add(("CoalChute", new Vector3(0, 0, 5.5f), 0));
            l.Add(("BreathGrate", new Vector3(0.6f, 0, 6.2f), 0));
            l.Add(("Roots", new Vector3(-0.4f, 4.1f, 3.2f), 0)); l.Add(("Roots", new Vector3(0.9f, 4.1f, 5.0f), 0));
            l.Add(("Barrels", new Vector3(-1.9f, 0, 6.6f), 0));
            return l.ToArray();
        }

        // What another builder needs from the room: the scene groups, the shared materials, and the CV_* slot map (null while the vault FBX is missing).
        internal sealed class RoomKit
        {
            public Transform Set, Lighting;
            public Dictionary<string, Material> Own;
            public Dictionary<string, Material> Slots;   // null when CellarVault.fbx is missing
            public GameObject Kit;
            Dictionary<string, Material> built;
            // The slot map built on demand, so a second kit (CellarStair) can map its CV_* slots even when the vault FBX is missing.
            public Dictionary<string, Material> SlotMap() => Slots ?? (built ?? (built = SlotMaterials(Own)));
        }

        [MenuItem("SleepingCalamity/Art/Build Dungeon Corner")]
        public static void Build()
        {
            Scene scene = NewRoomScene();
            var root = new GameObject("DungeonCorner_Cellars").transform;
            RoomKit room = BuildRoom(root);
            WetPatch(room.Set);

            Transform cams = new GameObject("CaptureCameras").transform; cams.SetParent(root, false);
            LookTestBuilder.AddCapture(cams, "Cap_Cellar_Wide", new Vector3(0.3f, 1.7f, 0.6f), new Vector3(0f, 1.6f, 8f), 62f);
            LookTestBuilder.AddCapture(cams, "Cap_Cellar_Mid", new Vector3(1.6f, 1.6f, 2.4f), new Vector3(-2.0f, 1.2f, 5.5f), 55f);
            LookTestBuilder.AddCapture(cams, "Cap_Cellar_Close", new Vector3(0.4f, 1.3f, 6.4f), new Vector3(-0.5f, 1.4f, 7.9f), 48f);
            DarkBackground(cams);

            Transform dbg = new GameObject("Debug").transform; dbg.SetParent(root, false);
            LookTestBuilder.Prim(dbg, "Figure_1.75m", PrimitiveType.Capsule, new Vector3(0.9f, 0.875f, 3.0f), new Vector3(0.5f, 0.875f, 0.5f), LookTestBuilder.Mat("iron"));
            dbg.gameObject.SetActive(false);

            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC DUNGEON scene built " + ScenePath);
        }

        internal static void DarkBackground(Transform cams)
        {
            foreach (Camera c in cams.GetComponentsInChildren<Camera>(true)) { c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.02f, 0.015f, 0.012f); }
        }

        // Inputs the room needs, then a new empty scene (NewScene(Single) unloads unused assets, so everything the scene uses is loaded after it: SceneBuilder's rule).
        internal static Scene NewRoomScene()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/LookTest/M_old_brick.mat") == null) LookTestBuilder.BuildMaterials();
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(LookTestBuilder.RpAssetPath) == null) LookTestBuilder.EnsureRendering();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            BuilderUtil.EnsureFolder(LookTestBuilder.RenderingFolder);
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // The room: profile, materials, kit placements (Set), lanterns, candles, lights, probe, volume, ambient and fog (Lighting). Call after NewRoomScene.
        internal static RoomKit BuildRoom(Transform root)
        {
            BuildProfile();
            BeautyCornerArt.EnsureCellarLantern();
            Dictionary<string, Material> own = BuildOwnMaterials();

            GameObject kit = BeautyCornerArt.LoadModel(KitFolder, KitFbx, keepMaterialSlots: true);
            Debug.Log("SC DUNGEON kit " + (kit != null ? KitFolder + "/" + KitFbx : "FBX missing, greybox for every piece"));
            Dictionary<string, Material> slots = kit != null ? SlotMaterials(own) : null;

            if (kit != null) LogUvDensity(kit);
            Transform set = new GameObject("Set").transform; set.SetParent(root, false);
            var report = new SortedDictionary<string, string>();
            int n = 0;
            foreach (var p in Placements)
            {
                var go = new GameObject(p.Piece + "_" + n++);
                go.transform.SetParent(set, false); go.transform.localPosition = p.Pos; go.transform.localRotation = Quaternion.Euler(0, p.Yaw, 0);
                Transform src = kit != null ? kit.transform.Find(p.Piece) : null;
                if (src != null) { Clone(src, go.transform, slots, p.Piece); report[p.Piece] = "art"; }
                else { Greybox(p.Piece, go.transform, own); report[p.Piece] = "greybox"; }
            }
            foreach (var kv in report) Debug.Log("SC DUNGEON piece " + kv.Key + " " + kv.Value);

            Transform lighting = new GameObject("Lighting").transform; lighting.SetParent(root, false);
            Lanterns(set, lighting, own, RoomHooks, "", LanternIntensity, LanternRange);
            Candles(set, lighting, own);
            Lights(set, lighting, own);
            LookTestBuilder.AddProbe(lighting, new Vector3(0f, 2.0f, 4f), new Vector3(6f, 4.6f, 9f));
            // A cellar reflects little: the baked probe sees the HDR hatch and lanterns, and at full strength it washed bevel speculars white.
            lighting.Find("ReflectionProbe").GetComponent<ReflectionProbe>().intensity = 0.35f;
            LookTestBuilder.AddVolume(lighting, ProfileName);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Floor 1 (Cellars design section 3): ambient top #3a2c26 to bottom #2a1c18, warm umber, no blue-green. Intensity 0.23 as tuned before (sRGB values, passed as they are).
            RenderSettings.ambientSkyColor = new Color(0.25f, 0.20f, 0.16f); RenderSettings.ambientEquatorColor = new Color(0.22f, 0.17f, 0.13f);   // round 3: a little less red
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.13f, 0.10f); RenderSettings.ambientIntensity = 0.85f;   // round 2: 0.40 (wide/mid p50 was 0.12); round 3: mid and close still dark
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.007f; RenderSettings.fogColor = new Color(0.16f, 0.13f, 0.11f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.sun = null;
            return new RoomKit { Set = set, Lighting = lighting, Own = own, Slots = slots, Kit = kit };
        }

        // --------------------------------------------------------------------------------------------- materials
        static Material Lit(string name) => LookTestBuilder.Ensure(MaterialsFolder + "/M_" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found"));

        internal static Dictionary<string, Material> BuildOwnMaterials()
        {
            var d = new Dictionary<string, Material>();
            Material breath = Lit("CV_Breath");   // faint warm emissive under the grate bars (the town's tithe-gate breath motif)
            breath.SetColor("_BaseColor", new Color(0.10f, 0.04f, 0.02f)); breath.EnableKeyword("_EMISSION"); breath.SetColor("_EmissionColor", new Color(1.0f, 0.30f, 0.05f) * 2.2f);
            breath.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            d["CV_Breath"] = breath;
            Material day = Lit("CV_Daylight");   // the coal hatch opening
            day.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.18f)); day.EnableKeyword("_EMISSION"); day.SetColor("_EmissionColor", new Color(1.0f, 0.97f, 0.90f) * 6f);
            day.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            d["CV_Daylight"] = day;
            Material dark = Lit("DC_Dark"); dark.SetColor("_BaseColor", new Color(0.03f, 0.022f, 0.016f)); dark.SetFloat("_Smoothness", 0.1f); d["dark"] = dark;
            Material wax = Lit("DC_Wax"); wax.SetColor("_BaseColor", new Color(0.62f, 0.55f, 0.42f)); wax.SetFloat("_Smoothness", 0.3f); d["wax"] = wax;
            Material flame = Lit("DC_Flame"); flame.SetColor("_BaseColor", new Color(0.1f, 0.08f, 0.05f)); flame.EnableKeyword("_EMISSION");
            flame.SetColor("_EmissionColor", new Color(3.0f, 1.9f, 0.7f)); flame.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; d["flame"] = flame;
            Shader cone = Shader.Find("SleepingCalamity/LightCone") ?? throw new InvalidOperationException("Shader not found: SleepingCalamity/LightCone");
            Material shaft = LookTestBuilder.Ensure(MaterialsFolder + "/M_DC_DaylightCone.mat", cone);
            shaft.SetColor("_Color", new Color(0.86f, 0.88f, 0.95f)); shaft.SetFloat("_Intensity", 0.12f); shaft.renderQueue = 3100; d["shaft"] = shaft;
            Material lampCone = LookTestBuilder.Ensure(MaterialsFolder + "/M_DC_LampCone.mat", cone);
            lampCone.SetColor("_Color", new Color(1.0f, 0.72f, 0.36f)); lampCone.SetFloat("_Intensity", 0.09f); lampCone.renderQueue = 3100; d["lampcone"] = lampCone;
            Shader pu = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? throw new InvalidOperationException("URP Particles/Unlit not found");
            Material mote = LookTestBuilder.Ensure(MaterialsFolder + "/M_DC_Mote.mat", pu);
            mote.SetFloat("_Surface", 1f); mote.SetFloat("_Blend", 2f); mote.SetFloat("_SrcBlend", 5f); mote.SetFloat("_DstBlend", 1f); mote.SetFloat("_SrcBlendAlpha", 1f); mote.SetFloat("_DstBlendAlpha", 1f);
            mote.SetFloat("_ZWrite", 0f); mote.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mote.SetOverrideTag("RenderType", "Transparent"); mote.renderQueue = 3000;
            mote.SetColor("_BaseColor", new Color(1.0f, 0.75f, 0.4f)); d["mote"] = mote;
            mote.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(UndercroftStairBuilder.RoundMoteTexturePath()));   // B1: untextured, the particles drew as hard squares
            foreach (Material m in d.Values) { if (m.HasProperty("_BaseColor")) m.SetColor("_Color", m.GetColor("_BaseColor")); EditorUtility.SetDirty(m); }
            AssetDatabase.SaveAssets();
            return d;
        }

        // CV_* slot name -> WearLit material (a B2 tileable tinted), built only when the FBX is in.
        internal static Dictionary<string, Material> SlotMaterials(Dictionary<string, Material> own)
        {
            Shader wear = Shader.Find(WearShaderName) ?? throw new InvalidOperationException("Shader not found: " + WearShaderName);
            var map = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var s in Slots)
            {
                Material src = LookTestBuilder.Mat(s.Source);
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                foreach (var fs in FabSlots)
                    if (fs.Slot == s.Slot) { B0Fab.TexSet set = B0Fab.Megascans(fs.Folder, fs.Prefix); if (set != null) B0Fab.Apply(m, set, fs.Tiles); }
                // round 3: the scans' normals read as melted plaster / chunky bricks at full strength
                float bumpMul = s.Slot == "CV_Brick" || s.Slot == "CV_VaultStone" ? 0.30f : s.Slot == "CV_Limewash" ? 0.5f : 1f;
                if (bumpMul != 1f && m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", m.GetFloat("_BumpScale") * bumpMul);
                float peak = Mathf.Max(1f, Mathf.Max(s.Tint.r, Mathf.Max(s.Tint.g, s.Tint.b)));
                m.SetColor("_BaseColor", new Color(s.Tint.r / peak, s.Tint.g / peak, s.Tint.b / peak, 1f).gamma);
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);   // same bytes with and without graphics
                EditorUtility.SetDirty(m); map[s.Slot] = m;
            }
            foreach (var f in FlatSlots)
            {
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + f.Slot + ".mat", wear);
                m.SetColor("_BaseColor", f.Linear.gamma); m.SetFloat("_Smoothness", f.Smooth); m.SetFloat("_Metallic", 0f);
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m); map[f.Slot] = m;
            }
            map["CV_Breath"] = own["CV_Breath"]; map["CV_Daylight"] = own["CV_Daylight"];
            map["CV_Flame"] = own["flame"];   // B8: SconceCage / LanternOil flame slot (emissive, same as the candle flame)
            AssetDatabase.SaveAssets();
            return map;
        }

        internal static void Clone(Transform src, Transform parent, Dictionary<string, Material> slots, string piece)
        {
            GameObject clone = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
            clone.name = piece;
            foreach (Renderer r in clone.GetComponentsInChildren<Renderer>(true))
            {
                Material[] cur = r.sharedMaterials;
                var mats = new Material[Mathf.Max(1, cur.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < cur.Length && cur[k] != null ? cur[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) throw new InvalidOperationException("DungeonCorner: no material for slot '" + slot + "' on " + piece + "/" + r.name + " (add it to DungeonCornerBuilder.Slots)");
                }
                r.sharedMaterials = mats;
            }
        }

        // --------------------------------------------------------------------------------------------- volume profile
        // A copy of LookTest_Cellar_Profile tuned to the A24 band (darker, no teal), so tuning never moves the LookTest scenes.
        internal static void BuildProfile() => LookTestBuilder.EnsureProfile(ProfileName, p =>
        {
            LookTestBuilder.Comp<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
            Bloom b = LookTestBuilder.Comp<Bloom>(p); b.threshold.Override(1.0f); b.intensity.Override(0.35f); b.scatter.Override(0.85f); b.clamp.Override(65000f);
            ColorAdjustments c = LookTestBuilder.Comp<ColorAdjustments>(p); c.postExposure.Override(0.70f); c.contrast.Override(40f); c.saturation.Override(-22f);   // round 3: exposure 0.35 -> 0.70 and contrast 48 -> 40 because Mid and Close read too dark
            ShadowsMidtonesHighlights s = LookTestBuilder.Comp<ShadowsMidtonesHighlights>(p); s.shadows.Override(new Vector4(0.86f, 0.82f, 0.80f, 0f));   // grey-brown shadow fill, pushed down
            WhiteBalance w = LookTestBuilder.Comp<WhiteBalance>(p); w.temperature.Override(-3f); w.tint.Override(12f);
            LiftGammaGain lg = LookTestBuilder.Comp<LiftGammaGain>(p); lg.lift.Override(new Vector4(1.0f, 0.97f, 0.97f, 0.0f));
            Vignette v = LookTestBuilder.Comp<Vignette>(p); v.intensity.Override(0.34f); v.smoothness.Override(0.5f);
        });

        // --------------------------------------------------------------------------------------------- lanterns, candles, lights
        // Lamp recipe (Cellars design section 6): gold #d6a24a at the flame (sRGB, as entered), the floor-1 candle tint #F2D58A for candles, pools 5 to 6 m across (range 5.5).
        static readonly Color Gold = new Color(0.839f, 0.635f, 0.290f), CandleTint = new Color(0.949f, 0.835f, 0.541f);
        const float LanternIntensity = 15f, LanternRange = 8.5f;   // round 2: wider, brighter pools

        // hook position + the direction the wall lies in (the hook plate sits 0.1 m that way)
        static readonly (Vector3 Hook, Vector3 Side)[] RoomHooks =
        {
            (new Vector3(-2.3f, 2.3f, 1.2f), new Vector3(-1, 0, 0)), (new Vector3(2.3f, 2.3f, 4.2f), new Vector3(1, 0, 0)),   // pools about 3 m apart down the 8 m room: z 1.2, 4.2, the sconce at 6.8, the candle at 8
        };

        internal static void Lanterns(Transform set, Transform lighting, Dictionary<string, Material> own, (Vector3 Hook, Vector3 Side)[] hooks, string tag = "", float intensity = 6.0f, float range = 10f)
        {
            GameObject fbx = BeautyCornerArt.LoadModel(LanternFolder, "CellarLantern.fbx");
            string[] guids = AssetDatabase.FindAssets("M_CellarLantern t:Material");
            Material lm = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            Debug.Log("SC DUNGEON lanterns " + (fbx != null && lm != null ? "art (B11)" : "greybox"));
            for (int i = 0; i < hooks.Length; i++)
            {
                Vector3 hook = hooks[i].Hook;
                Vector3 side = hooks[i].Side;
                LookTestBuilder.Box(set, "LanternHook_" + tag + i, hook + side * 0.1f + new Vector3(0, 0.02f, 0), new Vector3(0.2f, 0.04f, 0.04f), LookTestBuilder.Mat("iron"));
                Vector3 flame;
                if (fbx != null && lm != null)
                {
                    var l = (GameObject)PrefabUtility.InstantiatePrefab(fbx, set);
                    l.name = "CellarLantern_" + tag + i;
                    foreach (Renderer r in l.GetComponentsInChildren<Renderer>(true)) { var m = new Material[Mathf.Max(1, r.sharedMaterials.Length)]; for (int k = 0; k < m.Length; k++) m[k] = lm; r.sharedMaterials = m; }
                    l.transform.position = hook;   // pivot bottom centre: measure, then hang the top ring from the hook
                    Bounds b = new Bounds(hook, Vector3.zero); bool first = true;
                    foreach (Renderer r in l.GetComponentsInChildren<Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                    l.transform.position = new Vector3(hook.x, hook.y - b.size.y, hook.z);
                    flame = l.transform.position + new Vector3(0, 0.147f, 0);
                }
                else
                {
                    LookTestBuilder.Prim(set, "CellarLantern_" + tag + i, PrimitiveType.Sphere, hook - new Vector3(0, 0.2f, 0), Vector3.one * 0.22f, own["flame"]);
                    flame = hook - new Vector3(0, 0.2f, 0);
                }
                LookTestBuilder.AddLight(lighting, "LanternLight_" + tag + i, LightType.Point, flame, Quaternion.identity, Gold, intensity, range, LightShadows.Soft);
                Motes(lighting, "Motes_Lantern" + tag + i, flame + new Vector3(0, 0.3f, 0), new Vector3(3f, 2.2f, 3f), 90, own);
            }
        }

        static void Candles(Transform set, Transform lighting, Dictionary<string, Material> own)
        {
            // one cluster on the sill of a niche (NicheWall at z 8 is turned 180 degrees: world x = -niche x; the row-1 sill top is y 1.31, the
            // sill front lip is at z ~7.97, bottles stand deeper at z 8.04+). Niche column cx +1.3 -> world x -1.3.
            CandleCluster(set, lighting, own, new Vector3(-1.3f, 1.31f, 8.08f), "", new Vector3(0, 0.25f, -0.12f));
        }

        // Three candles standing at c (the foot of the tallest), one warm point light at c + lightOffset.
        internal static void CandleCluster(Transform set, Transform lighting, Dictionary<string, Material> own, Vector3 c, string tag, Vector3 lightOffset)
        {
            var offs = new[] { new Vector3(0, 0, 0), new Vector3(0.14f, 0, 0.06f), new Vector3(-0.12f, 0, 0.08f) };
            var hs = new[] { 0.16f, 0.1f, 0.07f };
            for (int i = 0; i < 3; i++)
            {
                LookTestBuilder.Prim(set, "Candle_" + tag + i, PrimitiveType.Cylinder, c + offs[i] + new Vector3(0, hs[i] * 0.5f, 0), new Vector3(0.035f, hs[i] * 0.5f, 0.035f), own["wax"]);
                LookTestBuilder.Prim(set, "Flame_" + tag + i, PrimitiveType.Sphere, c + offs[i] + new Vector3(0, hs[i] + 0.02f, 0), new Vector3(0.015f, 0.03f, 0.015f), own["flame"]);
            }
            LookTestBuilder.AddLight(lighting, "CandleLight" + tag, LightType.Point, c + lightOffset, Quaternion.identity, CandleTint, 1.6f, 3.8f, LightShadows.None);
        }

        // Six lights in the room, the cap (Cellars design section 6): two lanterns, the candle cluster, the grate, the wall sconce and the daylight spot; shadows on the lanterns,
        // the sconce and the daylight spot (4, the top of the 2 to 4 rule). The sconce takes the larder light's place.
        static void Lights(Transform set, Transform lighting, Dictionary<string, Material> own)
        {
            LookTestBuilder.AddLight(lighting, "GrateLight", LightType.Point, new Vector3(0.6f, 0.45f, 6.2f), Quaternion.identity, new Color(0.95f, 0.30f, 0.12f), 0.8f, 3.2f, LightShadows.None);
            if (!Sconce(set, lighting))   // no C15 source: the old larder candle stays
                LookTestBuilder.AddLight(lighting, "LarderLight", LightType.Point, new Vector3(-1.75f, 1.45f, 5.0f), Quaternion.identity, new Color(1.0f, 0.76f, 0.52f), 2.6f, 5.5f, LightShadows.None);
            // Daylight shaft from the coal hatch: the CV_Daylight quad centre is (2.5 cos 21, 1.6 + 2.5 sin 21, 5.5) = (2.33, 2.50, 5.5); start just inside it.
            Vector3 hatch = new Vector3(2.27f, 2.52f, 5.5f), dir = new Vector3(-0.55f, -0.83f, 0.0f).normalized;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Light spot = LookTestBuilder.AddLight(lighting, "DaylightShaft", LightType.Spot, hatch, rot, new Color(0.86f, 0.89f, 0.96f), 6f, 6.5f, LightShadows.Soft);
            spot.spotAngle = 16f; spot.innerSpotAngle = 8f;
            LookTestBuilder.Shape(lighting, "DaylightCone", LookTestGeometry.Shape.Cone, hatch, new Vector3(0.7f, 0f, 3.6f), own["shaft"], rot.eulerAngles, 2f, 16, false);
            Motes(lighting, "Motes_Shaft", hatch + dir * 2.2f, new Vector3(0.9f, 0.9f, 3.2f), 220, own, rot);
        }

        // Warm dust motes: a slow box emitter, prewarmed (the capture also simulates a few seconds so the edit-mode render shows them).
        internal static void Motes(Transform parent, string name, Vector3 pos, Vector3 box, int count, Dictionary<string, Material> own, Quaternion? rot = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = rot ?? Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 8f; main.startSpeed = 0.02f; main.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.022f); main.maxParticles = count;
            main.startColor = new Color(1.0f, 0.78f, 0.45f, 1.0f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = count / 8f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f); vel.y = new ParticleSystem.MinMaxCurve(-0.01f, 0.04f); vel.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = own["mote"]; r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // [C15] the wall sconce on the left wall above the barrels (x -2.5 inner face), one soft-shadow gold light at its flame. The model's size and axes are unknown, so it is scaled
        // to SconceHeight on its tallest extent, turned so its thinnest horizontal extent faces the wall, and placed by its bounds (not its pivot). Logs the measured extents.
        const float SconceHeight = 0.45f, SconceYaw = 0f;
        static bool Sconce(Transform set, Transform lighting)
        {
            GameObject fbx = B0Fab.Sconce(out Material mat);
            if (fbx == null || mat == null) { Debug.Log("SC B0 sconce C15 absent (Art/Source/Fab/C15_wall_sconce), keeping the larder light"); return false; }
            var holder = new GameObject("Sconce_C15").transform; holder.SetParent(set, false);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, holder);
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true)) { var m = new Material[Mathf.Max(1, r.sharedMaterials.Length)]; for (int k = 0; k < m.Length; k++) m[k] = mat; r.sharedMaterials = m; }
            Bounds Measure() { Bounds bb = new Bounds(); bool first = true; foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true)) { if (first) { bb = r.bounds; first = false; } else bb.Encapsulate(r.bounds); } return bb; }
            Bounds raw = Measure();
            float sc = SconceHeight / Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
            obj.transform.localScale = Vector3.one * sc;
            Bounds b0 = Measure();
            bool depthIsX = b0.size.x <= b0.size.z;   // the plate is thin in the wall-normal direction; the wall normal is world X
            obj.transform.rotation = Quaternion.Euler(0f, depthIsX ? SconceYaw : SconceYaw + 90f, 0f);
            Bounds b = Measure();
            Vector3 wallTarget = new Vector3(-2.5f + 0.01f, 1.9f, 6.8f);   // back of the bounds on the wall, centred on y and z
            obj.transform.position += new Vector3(wallTarget.x - b.min.x, wallTarget.y - b.center.y, wallTarget.z - b.center.z);
            b = Measure();
            Debug.Log("SC B0 sconce raw extents=" + raw.size.ToString("F3") + " scale=" + sc.ToString("F4") + " placed centre=" + b.center.ToString("F2") + " size=" + b.size.ToString("F2") + " depthIsX=" + depthIsX);
            LookTestBuilder.AddLight(lighting, "SconceLight", LightType.Point, new Vector3(b.max.x + 0.28f, b.center.y + 0.08f, b.center.z), Quaternion.identity, Gold, 5f, 4.5f, LightShadows.Soft);
            return true;
        }

        // One wet patch on the floor (wet weight 0.6): a thin flat disc, base colour the floor tone darkened by lerp(1, 0.5, 0.6) = 0.7 and smoothness lerp(0.30, 0.95, 0.6) = 0.69.
        // A material variant instead of a decal: no decal renderer feature is confirmed in this URP stack.
        static void WetPatch(Transform set)
        {
            const float wet = 0.6f;
            Material m = Lit("DC_WetPatch");
            float dark = Mathf.Lerp(1f, 0.5f, wet);
            m.SetColor("_BaseColor", new Color(0.16f * dark, 0.13f * dark, 0.11f * dark)); m.SetFloat("_Smoothness", Mathf.Lerp(0.30f, 0.95f, wet)); m.SetFloat("_Metallic", 0f);
            m.SetColor("_Color", m.GetColor("_BaseColor")); EditorUtility.SetDirty(m);
            GameObject patch = LookTestBuilder.Prim(set, "WetPatch", PrimitiveType.Cylinder, new Vector3(-0.3f, 0.003f, 3.4f), new Vector3(1.5f, 0.002f, 1.0f), m);
            patch.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            patch.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        }

        // The kit's UV density: UV area / world area over the triangles of a piece (expected 0.25 UV^2 per m^2 when one UV unit is 2 m). Logged, so the tiling above can be checked.
        static void LogUvDensity(GameObject kit)
        {
            foreach (string piece in new[] { "VaultSegment", "FloorTile", "ArchRib" })
            {
                Transform t = kit.transform.Find(piece);
                if (t == null) continue;
                double uvArea = 0, worldArea = 0;
                foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = mf.sharedMesh; if (mesh == null) continue;
                    Vector3[] v = mesh.vertices; Vector2[] uv = mesh.uv; int[] tri = mesh.triangles;
                    if (uv == null || uv.Length != v.Length) continue;
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                    {
                        worldArea += 0.5 * Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]).magnitude;
                        Vector2 a = uv[tri[i + 1]] - uv[tri[i]], b = uv[tri[i + 2]] - uv[tri[i]];
                        uvArea += 0.5 * Mathf.Abs(a.x * b.y - a.y * b.x);
                    }
                }
                Debug.Log("SC B0 uv density " + piece + ": uvArea/worldArea=" + (uvArea / System.Math.Max(1e-9, worldArea)).ToString("F4") + " (0.25 = 2 m per UV unit) -> metres per UV unit=" + (1.0 / System.Math.Sqrt(System.Math.Max(1e-9, uvArea / worldArea))).ToString("F2"));
            }
        }

        // --------------------------------------------------------------------------------------------- greybox pieces
        // Stand-ins in the same Unity extents as the contract table, built from LookTest shapes and the B2 materials.
        internal static void Greybox(string piece, Transform t, Dictionary<string, Material> own)
        {
            Material brick = LookTestBuilder.Mat("old_brick"), vault = LookTestBuilder.Mat("vault_stone"), lime = LookTestBuilder.Mat("limewash"), flag = LookTestBuilder.Mat("flagstone"),
                oak = LookTestBuilder.Mat("barrel_oak"), coal = LookTestBuilder.Mat("soot_coal"), root = LookTestBuilder.Mat("root_bark"), iron = LookTestBuilder.Mat("iron");
            switch (piece)
            {
                case "VaultSegment":
                    LookTestBuilder.Box(t, "WallL", new Vector3(-2.75f, 0.8f, 1f), new Vector3(0.5f, 1.6f, 2f), brick);
                    LookTestBuilder.Box(t, "WallR", new Vector3(2.75f, 0.8f, 1f), new Vector3(0.5f, 1.6f, 2f), brick);
                    LookTestBuilder.Shape(t, "Vault", LookTestGeometry.Shape.VaultInterior, new Vector3(0, 1.6f, 0), new Vector3(2.5f, 0, 2f), vault, default, 2f, 32);
                    break;
                case "ArchRib":
                    LookTestBuilder.Shape(t, "Rib", LookTestGeometry.Shape.VaultInterior, new Vector3(0, 1.6f, -0.225f), new Vector3(2.38f, 0, 0.45f), brick, default, 2f, 32);
                    LookTestBuilder.Box(t, "PillarL", new Vector3(-2.35f, 0.8f, 0), new Vector3(0.3f, 1.6f, 0.45f), vault);
                    LookTestBuilder.Box(t, "PillarR", new Vector3(2.35f, 0.8f, 0), new Vector3(0.3f, 1.6f, 0.45f), vault);
                    break;
                case "Pillar": LookTestBuilder.Box(t, "Pier", new Vector3(0, 0.8f, 0), new Vector3(0.6f, 1.6f, 0.6f), vault); break;
                case "EndWall": LookTestBuilder.Box(t, "Wall", new Vector3(0, 2.3f, -0.25f), new Vector3(6f, 4.6f, 0.5f), lime); break;
                case "NicheWall":
                    LookTestBuilder.Box(t, "Wall", new Vector3(0, 2.3f, -0.25f), new Vector3(6f, 4.6f, 0.5f), lime);
                    for (int ix = -1; ix <= 1; ix++) for (int iy = 0; iy < 3; iy++)
                        LookTestBuilder.Box(t, "Niche_" + ix + "_" + iy, new Vector3(ix * 0.9f, 0.7f + iy * 0.75f, 0.012f), new Vector3(0.6f, 0.55f, 0.02f), own["dark"]);
                    break;
                case "StairEntry":
                    LookTestBuilder.Box(t, "WallL", new Vector3(-1.8f, 2.3f, -0.25f), new Vector3(2.4f, 4.6f, 0.5f), lime);
                    LookTestBuilder.Box(t, "WallR", new Vector3(1.8f, 2.3f, -0.25f), new Vector3(2.4f, 4.6f, 0.5f), lime);
                    LookTestBuilder.Box(t, "Lintel", new Vector3(0, 3.1f, -0.25f), new Vector3(1.2f, 3.0f, 0.5f), lime);
                    for (int i = 0; i < 6; i++)
                        LookTestBuilder.Box(t, "Step" + i, new Vector3(0, (i + 1) * 0.43f * 0.5f, -0.5f - 0.6f * (i + 0.5f) - 0.0f), new Vector3(1.2f, (i + 1) * 0.43f, 0.6f), flag);
                    break;
                case "FloorTile": LookTestBuilder.Box(t, "Tile", new Vector3(0, -0.1f, 1f), new Vector3(2f, 0.2f, 2f), flag); break;
                case "CoalChute":
                    LookTestBuilder.Box(t, "Hatch", new Vector3(2.4f, 2.75f, 0), new Vector3(0.2f, 0.6f, 0.7f), iron, new Vector3(0, 0, 25f));
                    LookTestBuilder.Box(t, "Chute", new Vector3(2.0f, 1.5f, 0), new Vector3(0.1f, 2.9f, 0.7f), oak, new Vector3(0, 0, 32f));
                    LookTestBuilder.Prim(t, "CoalHeap", PrimitiveType.Sphere, new Vector3(1.5f, 0.15f, 0), new Vector3(1.4f, 0.6f, 1.2f), coal);
                    break;
                case "LarderShelf":
                    foreach (float y in new[] { 0.55f, 1.2f, 1.85f }) LookTestBuilder.Box(t, "Shelf" + y, new Vector3(-2.28f, y, 1f), new Vector3(0.45f, 0.06f, 1.9f), oak);
                    LookTestBuilder.Box(t, "Post", new Vector3(-2.3f, 1.1f, 0.05f), new Vector3(0.1f, 2.2f, 0.1f), iron);
                    for (int i = 0; i < 6; i++) LookTestBuilder.Prim(t, "Jar" + i, PrimitiveType.Cylinder, new Vector3(-2.25f, 0.7f + (i % 3) * 0.65f, 0.4f + (i / 3) * 0.9f), new Vector3(0.16f, 0.1f, 0.16f), own["wax"]);
                    break;
                case "BreathGrate":
                    LookTestBuilder.Box(t, "Glow", new Vector3(0, 0.004f, 0), new Vector3(1.1f, 0.008f, 1.1f), own["CV_Breath"]);
                    for (int i = -2; i <= 2; i++) LookTestBuilder.Box(t, "Bar" + i, new Vector3(i * 0.24f, 0.006f, 0), new Vector3(0.05f, 0.012f, 1.2f), iron);
                    LookTestBuilder.Box(t, "FrameA", new Vector3(0, 0.006f, 0.58f), new Vector3(1.2f, 0.012f, 0.05f), iron);
                    LookTestBuilder.Box(t, "FrameB", new Vector3(0, 0.006f, -0.58f), new Vector3(1.2f, 0.012f, 0.05f), iron);
                    break;
                case "Roots":
                    LookTestBuilder.Prim(t, "RootA", PrimitiveType.Cylinder, new Vector3(0, -0.8f, 0), new Vector3(0.12f, 0.8f, 0.12f), root);
                    LookTestBuilder.Prim(t, "RootB", PrimitiveType.Cylinder, new Vector3(0.12f, -0.55f, 0.05f), new Vector3(0.07f, 0.55f, 0.07f), root);
                    LookTestBuilder.Prim(t, "RootC", PrimitiveType.Cylinder, new Vector3(-0.1f, -0.45f, -0.05f), new Vector3(0.06f, 0.45f, 0.06f), root);
                    break;
                case "Barrels":
                    LookTestBuilder.Prim(t, "BarrelA", PrimitiveType.Cylinder, new Vector3(0, 0.4f, -0.5f), new Vector3(0.8f, 0.5f, 0.8f), oak).transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    LookTestBuilder.Prim(t, "BarrelB", PrimitiveType.Cylinder, new Vector3(0, 0.4f, 0.5f), new Vector3(0.8f, 0.5f, 0.8f), oak).transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    LookTestBuilder.Prim(t, "BarrelC", PrimitiveType.Cylinder, new Vector3(0.95f, 0.5f, 0), new Vector3(0.85f, 0.5f, 0.85f), oak);
                    break;
                default: throw new InvalidOperationException("DungeonCorner: no greybox for piece '" + piece + "'");
            }
        }
    }
}
