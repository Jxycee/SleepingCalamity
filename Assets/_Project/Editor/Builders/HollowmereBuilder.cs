// Builds scene Hollowmere, the Wave 0 greybox town (docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 4), from the layout in
// Core/Town/HollowmereLayout. The build fails (throws) on any TownLayout.Validate() line: a foundation over another foundation, a landmark or a path node,
// a path edge across a footprint, or a lot side with no reachable doorstep.
// The scene holds both presets as sibling roots and ApplyPreset switches them: Ruined (the start: footing slab, doorstep and ruin stubs on every lot; the stalls
// bare frames; the belfry empty; the mill a roofless shell) and Rebuilt (ring 1 by HollowmereLayout.RebuiltBuilding: bakery and inn on L lots, homes on M/S,
// the approved facade on F01, three lots left empty; rings 2-3 as home massing with a few gardens). Each building block faces its lot's default door side.
// Art where it exists: the approved corner's rows (fountain B10, cobbles B6, stall B8, facade B4, props, foliage, lanterns) come in through
// BeautyCornerBuilder.PlaceRow at their corner positions; the B8 stall is reused for stalls 2 and 3; the B4 bell tower when its folder exists. Everything
// else is greybox on the LookTest materials. Light, grade, sky and renderer: the beauty corner's (BeautyCornerBuilder.EnsureLook / ApplyPreset), so the
// approved corner looks the same here. Ground: HollowmereGround (Presentation), a mesh generated from the layout when the scene loads.
// Idempotent: the scene is rebuilt through BuilderUtil.SaveScene.
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Hollowmere.unity";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        public enum Preset { Ruined, Rebuilt }
        public const string RuinedRoot = "Preset_Ruined", RebuiltRoot = "Preset_Rebuilt";

        // Corner rows that belong to the rebuilt F01 house and the dressed stall (absent in the ruined start), and the rows never brought in (the corner's backdrop).
        static readonly string[] RebuiltOnlyRows = { "Facade", "MarketStall", "Barrels", "Crates", "Sacks", "Buckets", "Baskets", "Jugs", "Ivy", "LintelIvy", "GrassStrip", "StallLantern", "BracketLantern" };

        // Play-position capture cameras (design 7: square entry from the heroes' home, fountain, tithe gate, a lane mouth) plus a high oblique overview.
        // The approved corner's own three cameras are added too, unchanged (BeautyCornerTable.Cameras).
        public static readonly (string Name, Vector3 Pos, Vector3 Target, float Fov)[] Cameras =
        {
            ("Cap_HM_HomeDoor", new Vector3(12.0f, 1.65f, 5.4f), new Vector3(-3f, 1.6f, -5f), 50f),
            ("Cap_HM_Fountain", new Vector3(-6.5f, 1.65f, -9.5f), new Vector3(2.5f, 2.6f, 9f), 50f),
            ("Cap_HM_TitheGate", new Vector3(5.2f, 1.65f, 8.2f), new Vector3(5.6f, 5.5f, 22f), 55f),
            ("Cap_HM_LaneMouth", new Vector3(-0.4f, 1.65f, -40f), new Vector3(0f, 3.0f, -8f), 50f),
            ("Cap_HM_Overview", new Vector3(58f, 62f, -92f), new Vector3(2f, -4f, -16f), 42f),
        };

        [MenuItem("SleepingCalamity/Art/Build Hollowmere")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            TownLayout layout = HollowmereLayout.Create();
            List<string> errors = layout.Validate();
            if (errors.Count > 0) throw new InvalidOperationException("Hollowmere layout invalid (" + errors.Count + "): " + string.Join("; ", errors.Take(20)));
            Debug.Log("SC HOLLOWMERE layout ok foundations=" + layout.Foundations.Count + " landmarks=" + layout.Landmarks.Count + " nodes=" + layout.Nodes.Count + " edges=" + layout.Edges.Count
                + " ring1=" + Mix(layout, 1) + " ring2=" + Mix(layout, 2) + " ring3=" + Mix(layout, 3));
            BeautyCornerBuilder.EnsureLook();
            BuildMaterials();
            BuildScene(layout);
        }

        static string Mix(TownLayout t, int ring) => t.Count(ring, FoundationSize.L) + "L/" + t.Count(ring, FoundationSize.M) + "M/" + t.Count(ring, FoundationSize.S) + "S";

        // --------------------------------------------------------------------------------------------- materials
        static void BuildMaterials()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Flat(lit, "meadow", new Color(0.36f, 0.42f, 0.20f), 0.08f, 0f);    // sunlit grass, a little drier than the foliage green
            Flat(lit, "water", new Color(0.07f, 0.16f, 0.18f), 0.92f, 0f);     // deep lake green-blue, glossy (greybox, not the water shader)
            Flat(lit, "bed", new Color(0.24f, 0.18f, 0.12f), 0.05f, 0f);       // turned garden soil
            Flat(lit, "label", new Color(0.85f, 0.25f, 0.18f), 0.2f, 0f);      // door markers on greybox blocks
            AssetDatabase.SaveAssets();
        }

        static void Flat(Shader lit, string key, Color c, float smooth, float metal)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_" + key + ".mat", lit);
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
        }

        static Material M(string key)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_HM_" + key + ".mat");
            return m != null ? m : BeautyCornerBuilder.PlaceholderMat(key);
        }

        // --------------------------------------------------------------------------------------------- scene
        static void BuildScene(TownLayout t)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Hollowmere").transform;
            Transform set = Child(root, "Set");

            BuildGround(set, t);
            BuildWalls(set, t);
            BuildKeep(set, t);

            Transform ruined = Child(root, RuinedRoot), rebuilt = Child(root, RebuiltRoot);
            Transform both = Child(set, "Corner");
            foreach (BeautyCornerTable.Row row in BeautyCornerTable.Rows)
            {
                if (row.Id.StartsWith("NEIGHBOUR_", StringComparison.Ordinal)) continue;
                BeautyCornerBuilder.PlaceRow(RebuiltOnlyRows.Contains(row.Id) ? Child(rebuilt, "Corner") : both, row);
            }
            foreach (Foundation f in t.Foundations) { BuildFoundation(Child(ruined, "Foundations"), f, Preset.Ruined); BuildFoundation(Child(rebuilt, "Foundations"), f, Preset.Rebuilt); }
            foreach (Landmark l in t.Landmarks) { BuildLandmark(Child(ruined, "Landmarks"), l, Preset.Ruined); BuildLandmark(Child(rebuilt, "Landmarks"), l, Preset.Rebuilt); }
            BuildPathDebug(root, t);

            Transform lighting = Child(root, "Lighting");
            Light sun = LookTestBuilder.AddLight(lighting, "Sun", LightType.Directional, new Vector3(0, 10, 0), Quaternion.identity, Color.white, 3f, 10f, LightShadows.Soft);
            LookTestBuilder.AddLight(lighting, "ShadeFill", LightType.Directional, new Vector3(0, 10, 0), Quaternion.Euler(22f, 18f, 0f), new Color(0.78f, 0.84f, 1.0f), 0.5f, 10f, LightShadows.None);
            LookTestBuilder.AddProbe(lighting, new Vector3(0f, 3f, 3f), new Vector3(30f, 12f, 28f));
            LookTestBuilder.AddVolume(lighting, BeautyCornerBuilder.ProfileName);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.sun = sun;

            Transform cams = Child(root, "CaptureCameras");
            foreach (var c in BeautyCornerTable.Cameras) LookTestBuilder.AddCapture(cams, c.Name, c.Pos, c.Target, c.Fov);
            foreach (var c in Cameras)
            {
                Camera cam = LookTestBuilder.AddCapture(cams, c.Name, c.Pos, c.Target, c.Fov);
                cam.farClipPlane = 400f;
            }

            BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Midday);
            ApplyPreset(Preset.Ruined);
            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC HOLLOWMERE scene built " + ScenePath);
        }

        // Shows one preset root and hides the other (the open scene).
        public static void ApplyPreset(Preset preset)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != "Hollowmere") continue;
                Transform r = go.transform.Find(RuinedRoot), b = go.transform.Find(RebuiltRoot);
                if (r == null || b == null) throw new InvalidOperationException("Hollowmere preset roots missing");
                r.gameObject.SetActive(preset == Preset.Ruined); b.gameObject.SetActive(preset == Preset.Rebuilt);
                return;
            }
            throw new InvalidOperationException("No Hollowmere root in the open scene");
        }

        static Transform Child(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // --------------------------------------------------------------------------------------------- ground, water, walls, keep
        static void BuildGround(Transform set, TownLayout t)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(set, false);
            go.transform.localPosition = new Vector3(0f, -0.02f, 0f);   // the corner's surround sat 2 cm down so the B6 setts read above it
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            Material paving = BeautyCornerArt.SurroundGround();
            Material lane = LookTestBuilder.Mat("cobble");
            r.sharedMaterials = new[] { M("meadow"), lane, paving != null ? paving : lane };
            go.AddComponent<HollowmereGround>().Configure(new Vector2(-130f, -150f), new Vector2(140f, 100f), 1f, paving != null ? BeautyCornerArt.FarTileMetres : 3f, true);

            var water = LookTestBuilder.Box(set, "Lake", new Vector3(HollowmereLayout.LakeCentre.X, HollowmereLayout.WaterY - 0.05f, HollowmereLayout.LakeCentre.Z),
                new Vector3(HollowmereLayout.LakeRadiusX * 2.4f, 0.1f, HollowmereLayout.LakeRadiusZ * 2.4f), M("water"), default, 4f);
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Transform stream = Child(set, "Stream");
            TownPoint[] s = HollowmereLayout.Stream;
            for (int i = 1; i < s.Length; i++)
            {
                TownPoint a = s[i - 1], b = s[i], mid = (a + b) * 0.5f;
                float len = TownPoint.Distance(a, b) + 1f, yaw = Mathf.Atan2(b.X - a.X, b.Z - a.Z) * Mathf.Rad2Deg;
                float y = Mathf.Min(HollowmereLayout.Ground(t, a.X, a.Z), HollowmereLayout.Ground(t, b.X, b.Z)) + 0.04f;
                LookTestBuilder.Box(stream, "Stream" + i, new Vector3(mid.X, y, mid.Z), new Vector3(3.5f, 0.1f, len), M("water"), new Vector3(0f, yaw, 0f), 4f);
            }
        }

        static void BuildWalls(Transform set, TownLayout t)
        {
            Wall(Child(set, "TownWall"), t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TownWallOffset, 16f), 0.5f), 2.4f, 1.0f);
            Wall(Child(set, "TerraceWall"), t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TerraceOffset, 16f), 1.0f), 0.8f, 0.6f);
        }

        // A run of wall pieces standing on the ground: each piece reaches 1.5 m below its lowest end and `height` above its highest.
        static void Wall(Transform parent, TownLayout t, List<(TownPoint A, TownPoint B)> run, float height, float thick)
        {
            Material stone = LookTestBuilder.Mat("sandstone");
            int i = 0;
            foreach (var (a, b) in run)
            {
                TownPoint mid = (a + b) * 0.5f;
                float ya = HollowmereLayout.Ground(t, a.X, a.Z), yb = HollowmereLayout.Ground(t, b.X, b.Z), ym = HollowmereLayout.Ground(t, mid.X, mid.Z);
                float lo = Mathf.Min(ya, Mathf.Min(yb, ym)) - 1.5f, hi = Mathf.Max(ya, Mathf.Max(yb, ym)) + height;
                float yaw = Mathf.Atan2(b.X - a.X, b.Z - a.Z) * Mathf.Rad2Deg;
                LookTestBuilder.Box(parent, "Wall" + (i++), new Vector3(mid.X, (lo + hi) * 0.5f, mid.Z), new Vector3(thick, hi - lo, TownPoint.Distance(a, b) + 0.05f), stone, new Vector3(0f, yaw, 0f), 2f);
            }
        }

        // The keep terrace's retaining wall with its parapet, and the stair up from the square's north-east corner (Keep_StairFoot to Keep_StairHead).
        static void BuildKeep(Transform set, TownLayout t)
        {
            Transform keep = Child(set, "Keep");
            Material stone = LookTestBuilder.Mat("sandstone");
            int i = 0;
            foreach (var (a, b) in HollowmereLayout.WallRun(t, HollowmereLayout.KeepWallLine(1f), 0.3f))
            {
                TownPoint mid = (a + b) * 0.5f;
                LookTestBuilder.Box(keep, "KeepWall" + (i++), new Vector3(mid.X, (HollowmereLayout.KeepHeight + 0.9f - 0.6f) * 0.5f, mid.Z),
                    new Vector3(TownPoint.Distance(a, b) + 0.02f, HollowmereLayout.KeepHeight + 0.9f + 0.6f, 1.2f), stone, default, 2f);
            }
            PathNode foot = t.Node("Keep_StairFoot"), head = t.Node("Keep_StairHead");
            const int steps = 16;
            float z0 = foot.Pos.Z + 0.4f, z1 = HollowmereLayout.KeepWallZ - 0.6f, run = (z1 - z0) / steps;
            for (int s = 0; s < steps; s++)
            {
                float top = HollowmereLayout.KeepHeight * (s + 1) / steps;
                LookTestBuilder.Box(keep, "Step" + s, new Vector3(foot.Pos.X, top * 0.5f - 0.1f, z0 + (s + 0.5f) * run), new Vector3(2.2f, top + 0.2f, run + 0.01f), stone, default, 2f);
            }
            LookTestBuilder.Box(keep, "StairLanding", new Vector3(head.Pos.X, HollowmereLayout.KeepHeight * 0.5f - 0.1f, HollowmereLayout.KeepWallZ), new Vector3(2.2f, HollowmereLayout.KeepHeight + 0.2f, 1.3f), stone, default, 2f);
        }

        // --------------------------------------------------------------------------------------------- foundations
        // A stable 0..1 number per lot id and salt, for the greybox variation (block size, eave, roof pitch).
        static float Rand(string id, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                h ^= (uint)salt * 0x9E3779B9u; h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static void BuildFoundation(Transform parent, Foundation f, Preset preset)
        {
            // The lot's facing frame: local -Z is the default door side (Foundation.FacingYaw), origin at the lot centre on its pad.
            var go = new GameObject(f.Id + "_" + f.Size + (preset == Preset.Ruined ? "_" + f.RuinVariant : ""));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(f.Centre.X, f.Y, f.Centre.Z);
            go.transform.localRotation = Quaternion.Euler(0f, f.FacingYaw, 0f);
            Transform g = go.transform;
            float side = f.Side, half = side * 0.5f;
            Material stone = LookTestBuilder.Mat("sandstone");
            HollowmereLayout.BuildingKind kind = preset == Preset.Ruined ? HollowmereLayout.BuildingKind.Empty : HollowmereLayout.RebuiltBuilding(f.Id);

            if (kind != HollowmereLayout.BuildingKind.ApprovedFacade)   // the facade brings its own plinth; a slab would add a ledge in front of the approved look
                LookTestBuilder.Box(g, "Footing", new Vector3(0f, 0.05f, 0f), new Vector3(side, 0.4f, side), stone, default, 2f);
            switch (kind)
            {
                case HollowmereLayout.BuildingKind.Empty:
                    LookTestBuilder.Box(g, "Doorstep", new Vector3(0f, 0.11f, -half - 0.45f), new Vector3(1.6f, 0.22f, 0.9f), stone, default, 2f);
                    RuinStubs(g, f, stone);
                    break;
                case HollowmereLayout.BuildingKind.ApprovedFacade:
                    break;   // the corner's Facade row stands here (Corner group of the rebuilt root)
                case HollowmereLayout.BuildingKind.Garden:
                    Garden(g, f);
                    break;
                default:
                    Building(g, f, kind);
                    break;
            }
        }

        // The ruined lot by its variant: a = back wall stub and a side stub; b = a broken back corner and rubble; c = broken front wall each side of the doorway and rubble.
        static void RuinStubs(Transform g, Foundation f, Material stone)
        {
            float half = f.Side * 0.5f, inset = half - 0.35f;
            char v = f.RuinVariant[f.RuinVariant.Length - 1];
            if (v == 'a')
            {
                LookTestBuilder.Box(g, "Stub_Back", new Vector3(-0.15f * f.Side, 0.25f + 0.55f, inset), new Vector3(f.Side * 0.65f, 1.1f, 0.5f), stone, default, 2f);
                LookTestBuilder.Box(g, "Stub_Side", new Vector3(-inset, 0.25f + 0.3f, 0.2f * f.Side), new Vector3(0.5f, 0.6f, f.Side * 0.5f), stone, default, 2f);
            }
            else if (v == 'b')
            {
                LookTestBuilder.Box(g, "Stub_CornerA", new Vector3(inset - 0.9f * f.Side * 0.25f, 0.25f + 0.8f, inset), new Vector3(f.Side * 0.45f, 1.6f, 0.5f), stone, default, 2f);
                LookTestBuilder.Box(g, "Stub_CornerB", new Vector3(inset, 0.25f + 0.55f, inset - f.Side * 0.18f), new Vector3(0.5f, 1.1f, f.Side * 0.36f), stone, default, 2f);
            }
            else
            {
                float w = f.Side * 0.3f;
                LookTestBuilder.Box(g, "Stub_FrontL", new Vector3(-half + 0.35f + w * 0.5f, 0.25f + 0.4f, -inset), new Vector3(w, 0.8f, 0.5f), stone, default, 2f);
                LookTestBuilder.Box(g, "Stub_FrontR", new Vector3(half - 0.35f - w * 0.5f, 0.25f + 0.3f, -inset), new Vector3(w, 0.6f, 0.5f), stone, default, 2f);
            }
            int n = v == 'a' ? 2 : 4;
            for (int i = 0; i < n; i++)
            {
                float x = (Rand(f.Id, 10 + i) - 0.5f) * f.Side * 0.6f, z = (Rand(f.Id, 20 + i) - 0.3f) * f.Side * 0.5f, s = 0.35f + Rand(f.Id, 30 + i) * 0.45f;
                LookTestBuilder.Box(g, "Rubble" + i, new Vector3(x, 0.25f + s * 0.4f, z), new Vector3(s, s * 0.8f, s * 1.2f), stone, new Vector3(Rand(f.Id, 40 + i) * 25f, Rand(f.Id, 50 + i) * 180f, 0f), 2f);
            }
        }

        static void Garden(Transform g, Foundation f)
        {
            float half = f.Side * 0.5f;
            Material oak = LookTestBuilder.Mat("oak_timber");
            for (int i = 0; i < 3; i++)
                LookTestBuilder.Box(g, "Bed" + i, new Vector3(0f, 0.32f, -half + 1.2f + i * (f.Side - 2f) / 3f + 0.5f), new Vector3(f.Side - 1.6f, 0.15f, 1.1f), M("bed"), default, 2f);
            LookTestBuilder.Box(g, "FenceBack", new Vector3(0f, 0.75f, half - 0.15f), new Vector3(f.Side, 0.9f, 0.08f), oak, default, 2f);
            LookTestBuilder.Box(g, "FenceL", new Vector3(-half + 0.15f, 0.75f, 0f), new Vector3(0.08f, 0.9f, f.Side), oak, default, 2f);
            LookTestBuilder.Box(g, "FenceR", new Vector3(half - 0.15f, 0.75f, 0f), new Vector3(0.08f, 0.9f, f.Side), oak, default, 2f);
        }

        // A building-sized greybox: plinth, walls, gable roof, a door marker on the facing side (local -Z). Bakery adds an oven bay and chimney, the inn a porch;
        // both get a sign bracket. Sizes stay inside Foundation.MaxBuilding for the class.
        static void Building(Transform g, Foundation f, HollowmereLayout.BuildingKind kind)
        {
            float max = Foundation.MaxBuilding(f.Size);
            float w = max * (kind == HollowmereLayout.BuildingKind.Home ? 0.82f + 0.16f * Rand(f.Id, 1) : 0.95f);
            float d = max * (kind == HollowmereLayout.BuildingKind.Home ? 0.72f + 0.22f * Rand(f.Id, 2) : 0.82f);
            float eave = (f.Size == FoundationSize.S ? 3.6f : f.Size == FoundationSize.M ? 5.6f : 6.2f) + 0.8f * Rand(f.Id, 3);
            if (kind == HollowmereLayout.BuildingKind.Inn) eave = 8.4f;
            float pitch = 36f + 10f * Rand(f.Id, 4);
            float zc = -(max - d) * 0.35f;   // the block sits toward the door side; the yard is behind it
            const float plinth = 0.55f;
            Material plaster = LookTestBuilder.Mat("plaster"), roof = LookTestBuilder.Mat("roof_tile"), stone = LookTestBuilder.Mat("sandstone"), wood = LookTestBuilder.Mat("painted_wood"), oak = LookTestBuilder.Mat("oak_timber");

            LookTestBuilder.Box(g, "Plinth", new Vector3(0f, 0.25f + plinth * 0.5f, zc), new Vector3(w + 0.2f, plinth, d + 0.2f), stone, default, 2f);
            LookTestBuilder.Box(g, "Walls", new Vector3(0f, 0.25f + plinth + (eave - plinth) * 0.5f, zc), new Vector3(w, eave - plinth, d), plaster, default, 2f);
            float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * d * 0.5f, slope = (d * 0.5f + 0.35f) / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float ry = 0.25f + eave + rise * 0.5f;
            LookTestBuilder.Box(g, "RoofFront", new Vector3(0f, ry, zc - d * 0.25f), new Vector3(w + 0.6f, 0.25f, slope), roof, new Vector3(-pitch, 0f, 0f), 2f);
            LookTestBuilder.Box(g, "RoofBack", new Vector3(0f, ry, zc + d * 0.25f), new Vector3(w + 0.6f, 0.25f, slope), roof, new Vector3(pitch, 0f, 0f), 2f);
            for (int k = -1; k <= 1; k += 2)   // gable ends: stacked plaster slabs under the roof
                for (int s = 0; s < 3; s++)
                {
                    float frac = (s + 0.5f) / 3f, ww = 0.2f + 0.8f * (1f - frac) * 0.98f;
                    LookTestBuilder.Box(g, "Gable" + (k < 0 ? "L" : "R") + s, new Vector3(k * (w * 0.5f - 0.1f), 0.25f + eave + rise * (s / 3f) + rise / 6f, zc), new Vector3(0.2f, rise / 3f, d * ww), plaster, default, 2f);
                }
            float front = zc - d * 0.5f;
            LookTestBuilder.Box(g, "Door", new Vector3(0f, 0.25f + plinth + 1.1f, front - 0.06f), new Vector3(1.3f, 2.2f, 0.12f), wood, default, 2f);
            LookTestBuilder.Box(g, "DoorMarker", new Vector3(0f, 0.27f, front - 0.6f), new Vector3(1.3f, 0.04f, 0.8f), M("label"), default, 2f);
            for (int k = -1; k <= 1; k += 2)
                if (w > 5f) LookTestBuilder.Box(g, "Shutter" + k, new Vector3(k * w * 0.3f, 0.25f + plinth + 1.5f, front - 0.05f), new Vector3(0.9f, 1.1f, 0.1f), wood, default, 2f);
            if (kind == HollowmereLayout.BuildingKind.Bakery)
            {
                LookTestBuilder.Box(g, "OvenBay", new Vector3(w * 0.5f + 0.6f, 0.25f + 1.4f, zc + d * 0.15f), new Vector3(1.4f, 2.8f, 2.6f), stone, default, 2f);
                LookTestBuilder.Box(g, "Chimney", new Vector3(w * 0.32f, 0.25f + eave + rise + 0.6f, zc + d * 0.2f), new Vector3(1.0f, 2.6f, 1.0f), stone, default, 2f);
            }
            if (kind == HollowmereLayout.BuildingKind.Inn)
            {
                LookTestBuilder.Box(g, "PorchRoof", new Vector3(0f, 0.25f + 3.2f, front - 1.1f), new Vector3(4f, 0.18f, 2.2f), roof, new Vector3(-12f, 0f, 0f), 2f);
                LookTestBuilder.Box(g, "PorchPostL", new Vector3(-1.8f, 0.25f + 1.55f, front - 2.0f), new Vector3(0.2f, 3.1f, 0.2f), oak, default, 2f);
                LookTestBuilder.Box(g, "PorchPostR", new Vector3(1.8f, 0.25f + 1.55f, front - 2.0f), new Vector3(0.2f, 3.1f, 0.2f), oak, default, 2f);
            }
            if (kind == HollowmereLayout.BuildingKind.Bakery || kind == HollowmereLayout.BuildingKind.Inn)
            {
                LookTestBuilder.Box(g, "SignArm", new Vector3(w * 0.3f, 0.25f + 3.4f, front - 0.5f), new Vector3(0.08f, 0.08f, 1.0f), oak, default, 2f);
                LookTestBuilder.Box(g, "Sign", new Vector3(w * 0.3f, 0.25f + 2.95f, front - 0.75f), new Vector3(0.06f, 0.75f, 0.6f), wood, default, 2f);
            }
            else if (Rand(f.Id, 5) > 0.45f)
                LookTestBuilder.Box(g, "Chimney", new Vector3(-w * 0.3f, 0.25f + eave + rise * 0.7f, zc + d * 0.15f), new Vector3(0.7f, 2.0f, 0.7f), stone, default, 2f);
        }

        // --------------------------------------------------------------------------------------------- landmarks
        static void BuildLandmark(Transform parent, Landmark l, Preset preset)
        {
            var go = new GameObject(l.Id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(l.Footprint.Centre.X, l.Y, l.Footprint.Centre.Z);
            go.transform.localRotation = Quaternion.Euler(0f, l.Footprint.Yaw, 0f);
            Transform g = go.transform;
            float sx = l.Footprint.HalfX * 2f, sz = l.Footprint.HalfZ * 2f;
            Material stone = LookTestBuilder.Mat("sandstone"), roof = LookTestBuilder.Mat("roof_tile"), oak = LookTestBuilder.Mat("oak_timber"), iron = LookTestBuilder.Mat("iron"), wood = LookTestBuilder.Mat("painted_wood");
            bool rebuilt = preset == Preset.Rebuilt;
            switch (l.Kind)
            {
                case LandmarkKind.Fountain:
                    break;   // the corner's DryFountain row (B10) stands at the origin in both presets
                case LandmarkKind.Stall:
                    if (rebuilt && l.Id == "Stall_1") break;   // the corner's MarketStall row
                    if (rebuilt) { StallArt(g, l); break; }
                    // Ruined: a bare frame, counter and corner posts, no awning (design 3).
                    LookTestBuilder.Box(g, "Counter", new Vector3(0f, 0.45f, 0.45f), new Vector3(2.6f, 0.9f, 0.7f), wood, default, 2f);
                    foreach (float x in new[] { -1.25f, 1.25f }) foreach (float z in new[] { -0.75f, 0.7f })
                            LookTestBuilder.Box(g, "Post", new Vector3(x, 1.25f, z), new Vector3(0.1f, 2.5f, 0.1f), oak, default, 2f);
                    LookTestBuilder.Box(g, "TornAwning", new Vector3(-0.6f, 2.5f, 0f), new Vector3(1.2f, 0.04f, 1.6f), LookTestBuilder.Mat("canvas"), new Vector3(10f, 0f, 25f), 2f);
                    break;
                case LandmarkKind.BellTower:
                    BellTower(g, l, rebuilt);
                    break;
                case LandmarkKind.TitheGate:
                    foreach (float x in new[] { -2.3f, 2.3f }) LookTestBuilder.Box(g, "Pier", new Vector3(x, 2.4f, 0f), new Vector3(1.0f, 5.4f, 1.6f), stone, default, 2f);
                    LookTestBuilder.Box(g, "Lintel", new Vector3(0f, 4.6f, 0f), new Vector3(5.6f, 1.0f, 1.6f), stone, default, 2f);
                    LookTestBuilder.Box(g, "Glow", new Vector3(0f, 2.0f, 0.5f), new Vector3(3.6f, 4.0f, 0.05f), BeautyCornerBuilder.PlaceholderMat("warm"), default, 2f);
                    for (int i = 0; i < 9; i++) LookTestBuilder.Box(g, "Bar" + i, new Vector3(-1.8f + i * 0.45f, 2.0f, 0f), new Vector3(0.09f, 4.0f, 0.09f), iron, default, 2f);
                    LookTestBuilder.Box(g, "Rail", new Vector3(0f, 2.6f, 0f), new Vector3(3.7f, 0.12f, 0.12f), iron, new Vector3(0f, 0f, 4f), 2f);   // cracked: the crossbar hangs askew
                    break;
                case LandmarkKind.HeroesHome:
                    if (l.Id == "HeroesHome_Cistern")
                    {
                        LookTestBuilder.Box(g, "Walls", new Vector3(0f, l.Height * 0.5f, 0f), new Vector3(sx, l.Height, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "RoofS", new Vector3(0f, l.Height + 1.0f, -sz * 0.25f), new Vector3(sx + 0.5f, 0.25f, sz * 0.62f), roof, new Vector3(-30f, 0f, 0f), 2f);
                        LookTestBuilder.Box(g, "RoofN", new Vector3(0f, l.Height + 1.0f, sz * 0.25f), new Vector3(sx + 0.5f, 0.25f, sz * 0.62f), roof, new Vector3(30f, 0f, 0f), 2f);
                        LookTestBuilder.Prim(g, "CisternDrum", PrimitiveType.Cylinder, new Vector3(sx * 0.32f, (l.Height + 1.5f) * 0.5f, sz * 0.1f), new Vector3(3.2f, (l.Height + 1.5f) * 0.5f, 3.2f), stone);
                        LookTestBuilder.Box(g, "Door", new Vector3(0f, 1.15f, -sz * 0.5f - 0.06f), new Vector3(1.4f, 2.3f, 0.12f), wood, default, 2f);
                        LookTestBuilder.Box(g, "DoorMarker", new Vector3(0f, 0.02f, -sz * 0.5f - 0.6f), new Vector3(1.4f, 0.04f, 0.8f), M("label"), default, 2f);
                        LookTestBuilder.Box(g, "PatchBoards", new Vector3(-sx * 0.3f, 3.6f, -sz * 0.5f - 0.04f), new Vector3(1.6f, 1.2f, 0.08f), oak, new Vector3(0f, 0f, 6f), 2f);
                        var stair = new GameObject("UndercroftStair");   // P25: the way down; a marker for M5/M6 (inside the house)
                        stair.transform.SetParent(g, false);
                        stair.transform.position = new Vector3(HollowmereLayout.UndercroftStair.X, l.Y, HollowmereLayout.UndercroftStair.Z);
                    }
                    else
                    {
                        LookTestBuilder.Box(g, "Walls", new Vector3(0f, l.Height * 0.5f, 0f), new Vector3(sx, l.Height, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "Loft", new Vector3(0f, l.Height + 1.0f, 0f), new Vector3(sx - 0.8f, 2.0f, sz - 0.8f), oak, default, 2f);
                        LookTestBuilder.Box(g, "RoofW", new Vector3(-sx * 0.25f, l.Height + 2.6f, 0f), new Vector3(sx * 0.6f, 0.25f, sz + 0.4f), roof, new Vector3(0f, 0f, 35f), 2f);
                        LookTestBuilder.Box(g, "RoofE", new Vector3(sx * 0.25f, l.Height + 2.6f, 0f), new Vector3(sx * 0.6f, 0.25f, sz + 0.4f), roof, new Vector3(0f, 0f, -35f), 2f);
                        LookTestBuilder.Box(g, "Door", new Vector3(sx * 0.5f + 0.06f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 1.2f), wood, default, 2f);
                    }
                    break;
                case LandmarkKind.Mill:
                    if (rebuilt)
                    {
                        LookTestBuilder.Box(g, "Walls", new Vector3(0f, 3f, 0f), new Vector3(sx, 6f, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "RoofW", new Vector3(0f, 7.2f, -sz * 0.25f), new Vector3(sx + 0.6f, 0.25f, sz * 0.62f), roof, new Vector3(-35f, 0f, 0f), 2f);
                        LookTestBuilder.Box(g, "RoofE", new Vector3(0f, 7.2f, sz * 0.25f), new Vector3(sx + 0.6f, 0.25f, sz * 0.62f), roof, new Vector3(35f, 0f, 0f), 2f);
                    }
                    else
                    {
                        LookTestBuilder.Box(g, "WallW", new Vector3(-sx * 0.5f + 0.35f, 2.4f, 0f), new Vector3(0.7f, 4.8f, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "WallE", new Vector3(sx * 0.5f - 0.35f, 3f, 0f), new Vector3(0.7f, 6f, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "WallN", new Vector3(0f, 2f, sz * 0.5f - 0.35f), new Vector3(sx, 4f, 0.7f), stone, default, 2f);
                        LookTestBuilder.Box(g, "WallS", new Vector3(0.8f, 1.4f, -sz * 0.5f + 0.35f), new Vector3(sx - 1.6f, 2.8f, 0.7f), stone, default, 2f);
                    }
                    var wheel = LookTestBuilder.Prim(g, "Wheel", PrimitiveType.Cylinder, new Vector3(sx * 0.5f + 0.7f, 2.4f, 0f), new Vector3(5f, 0.4f, 5f), oak);
                    wheel.transform.localRotation = Quaternion.Euler(rebuilt ? 0f : 18f, 0f, 90f);
                    break;
                case LandmarkKind.TownGate:
                    LookTestBuilder.Box(g, "Pier", new Vector3(0f, l.Height * 0.5f - 1f, 0f), new Vector3(sx, l.Height + 2f, sz), stone, default, 2f);
                    break;
            }
        }

        static void BellTower(Transform g, Landmark l, bool rebuilt)
        {
            // The B4 BellTower art when its folder is in (pivot = shaft bottom centre, clock face to local +Z, so ArtYaw 180 faces it south to the square).
            if (BeautyCornerArt.FindFolder("B4", "BellTower") != null)
            {
                var row = new BeautyCornerTable.Row
                {
                    Brief = BeautyCornerTable.Neighbours, Id = "BellTower", Batch = "B4", Names = new string[0], ArtYaw = 180f,
                    Instances = new[] { new BeautyCornerTable.Instance(0f, 0f, 0f, 0f, 1f, "BellTower") }, Placeholder = new BeautyCornerTable.Part[0],
                };
                BeautyCornerBuilder.PlaceRow(g, row);
                return;
            }
            Material stone = LookTestBuilder.Mat("sandstone"), oak = LookTestBuilder.Mat("oak_timber"), roof = LookTestBuilder.Mat("roof_tile");
            LookTestBuilder.Box(g, "Base", new Vector3(0f, 0.5f, 0f), new Vector3(6.5f, 1.0f, 6.5f), stone, default, 2f);
            LookTestBuilder.Box(g, "Shaft", new Vector3(0f, 6.3f, 0f), new Vector3(4.4f, 11.6f, 4.4f), stone, default, 2f);
            foreach (float x in new[] { -2.2f, 2.2f }) foreach (float z in new[] { -2.2f, 2.2f })   // open belfry: corner posts under the spire
                    LookTestBuilder.Box(g, "BelfryPost", new Vector3(x, 13.2f, z), new Vector3(0.4f, 2.2f, 0.4f), oak, default, 2f);
            LookTestBuilder.Box(g, "BelfryFloor", new Vector3(0f, 12.15f, 0f), new Vector3(4.8f, 0.15f, 4.8f), oak, default, 2f);
            float[] widths = { 4.6f, 3.4f, 2.2f, 1.0f };
            for (int i = 0; i < 4; i++) LookTestBuilder.Box(g, "Spire" + i, new Vector3(0f, 14.7f + i * 0.8f, 0f), new Vector3(widths[i], 0.8f, widths[i]), roof, new Vector3(0f, 45f, 0f), 2f);
            if (rebuilt) LookTestBuilder.Prim(g, "Bell", PrimitiveType.Cylinder, new Vector3(0f, 13.1f, 0f), new Vector3(1.3f, 0.6f, 1.3f), LookTestBuilder.Mat("iron"));
            LookTestBuilder.Box(g, "Door", new Vector3(0f, 2.1f, 3.3f), new Vector3(1.2f, 2.2f, 0.12f), LookTestBuilder.Mat("painted_wood"), default, 2f);
        }

        // Stalls 2 and 3 reuse the B8 MarketStall row's art (or its greybox) at their own place and turn.
        static void StallArt(Transform g, Landmark l)
        {
            BeautyCornerTable.Row src = BeautyCornerTable.Rows.First(r => r.Id == "MarketStall");
            var row = new BeautyCornerTable.Row
            {
                Brief = src.Brief, Id = src.Id, Batch = src.Batch, Names = src.Names, ArtYaw = src.ArtYaw,
                Instances = new[] { new BeautyCornerTable.Instance(0f, 0f, 0f) }, Placeholder = src.Placeholder,
            };
            BeautyCornerBuilder.PlaceRow(g, row);
        }

        // --------------------------------------------------------------------------------------------- path graph (editor-only gizmo data)
        // The graph as an inactive child of line renderers would bloat the scene; instead keep a small marker per lane node so the graph shows in the hierarchy.
        // The graph itself lives in Core (HollowmereLayout) and the approval map draws it.
        static void BuildPathDebug(Transform root, TownLayout t)
        {
            Transform dbg = Child(root, "PathGraph (markers, hidden)");
            dbg.gameObject.SetActive(false);
            foreach (PathNode n in t.Nodes)
            {
                if (n.Kind != PathNodeKind.Doorstep && n.Kind != PathNodeKind.Door && n.Kind != PathNodeKind.Stair) continue;
                var m = new GameObject(n.Id);
                m.transform.SetParent(dbg, false);
                m.transform.localPosition = new Vector3(n.Pos.X, n.Y, n.Pos.Z);
            }
        }
    }
}
