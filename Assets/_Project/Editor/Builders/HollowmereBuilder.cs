// Builds scene Hollowmere, the Wave 0 greybox town (docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 4), from the layout in
// Core/Town/HollowmereLayout. The build fails (throws) on any TownLayout.Validate() line: a foundation over another foundation, a landmark or a path node,
// a path edge across a footprint, or a lot side with no reachable doorstep.
// The scene holds both presets as sibling roots and ApplyPreset switches them: Ruined (the start: footing slab, doorstep and ruin stubs on every lot; the stalls
// bare frames; the belfry empty; the mill a roofless shell) and Rebuilt (ring 1 by HollowmereLayout.RebuiltBuilding: bakery and inn on L lots, homes on M/S,
// the approved facade on F01, three lots left empty; rings 2-3 as home massing with a few gardens). Each building block faces its lot's default door side.
// Art where it exists: the approved corner's rows (fountain B10, cobbles B6, stall B8, facade B4, props, foliage, lanterns) come in through
// BeautyCornerBuilder.PlaceRow at their corner positions; the three stalls are the Wave 1 Ring1/Stalls art (ruined or rebuilt state), else the B8 stall; the B4 bell tower when its folder exists;
// the heroes' home from Ring1/HeroesHome (HeroesHomeArt). Everything else is greybox on the LookTest materials, except the buildings whose final export is in
// (HollowmereArt: the bakery, smithy, schoolhouse and mill; HollowmereInnArt: the inn). Light, grade, sky and renderer: the beauty corner's (BeautyCornerBuilder.EnsureLook / ApplyPreset), so the
// approved corner looks the same here. Ground: a Unity Terrain from the layout heights (HollowmereTerrain).
// Ruins: every empty lot shows its ruin kit variant (RuinVariant, RuinTurn); a placed building or garden switches it off.
// Idempotent: the scene is rebuilt through BuilderUtil.SaveScene.
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
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

        // Play-position capture cameras (design 7: square entry from the heroes' home, fountain, tithe gate, a lane mouth), three low-angle terrain views and a
        // high oblique overview. Pos.y is the eye height above the ground at Pos (Ground, so the cameras follow the terrain); Target is absolute.
        // The approved corner's own three cameras are added too, unchanged (BeautyCornerTable.Cameras).
        public static readonly (string Name, Vector3 Pos, Vector3 Target, float Fov)[] Cameras =
        {
            ("Cap_HM_HomeDoor", new Vector3(12.0f, 1.65f, 5.4f), new Vector3(-3f, 1.6f, -5f), 50f),
            ("Cap_HM_Fountain", new Vector3(-6.5f, 1.65f, -9.5f), new Vector3(2.5f, 2.6f, 9f), 50f),
            ("Cap_HM_TitheGate", new Vector3(5.2f, 1.65f, 8.2f), new Vector3(5.6f, 5.5f, 22f), 55f),
            ("Cap_HM_LaneMouth", new Vector3(-0.4f, 1.65f, -40f), new Vector3(0f, 1.0f, -8f), 50f),
            ("Cap_HM_LakeShore", new Vector3(-26f, 1.7f, -88f), new Vector3(-2f, 3f, -20f), 50f),       // from the lake shore by R3_01 up at the hill town and the keep
            ("Cap_HM_MarketStreet", new Vector3(0.6f, 1.65f, -19.8f), new Vector3(-0.5f, -6.5f, -58f), 55f), // the square's south edge down Market Street to the second terrace and the wall
            ("Cap_HM_KeepStairs", new Vector3(-31f, 1.7f, 13.8f), new Vector3(-20f, 3.0f, 19.6f), 58f),  // along the Wall Ginnel: terrace step, west stair, keep wall
            ("Cap_HM_Smithy", new Vector3(-48.6f, 1.7f, 2.9f), new Vector3(-38.8f, -1.0f, 7.4f), 55f),       // ring 2: the smithy on R2_01 from the Ring Lane (forge bay toward the camera)
            ("Cap_HM_Schoolhouse", new Vector3(-44.4f, 1.7f, -39.7f), new Vector3(-38.2f, -1.5f, -30.9f), 55f), // ring 2: the schoolhouse on R2_03 from the Ring Lane
            ("Cap_HM_Mill", new Vector3(70.8f, 2.0f, -74.3f), new Vector3(71.2f, -6.0f, -59.5f), 55f),        // the mill's front and wheel side from the stream below
            ("Cap_HM_Bakery", new Vector3(-6.2f, 1.65f, 2.6f), new Vector3(-12.5f, 3.4f, 12.2f), 55f),       // from the square at the bakery front on F02 (oven side on the right)
            ("Cap_HM_HeroesHome", new Vector3(1.5f, 1.65f, -2.0f), new Vector3(12.0f, 3.6f, 12.5f), 50f),    // from the fountain at the heroes' home: Wren's list, the lit door, the annex above
            ("Cap_HM_HeroesAnnex", new Vector3(19.0f, 1.65f, 20.8f), new Vector3(11.8f, 6.6f, 24.5f), 55f),  // from the keep stair head at the bell-loft annex door and loft
            ("Cap_HM_KeepWall", new Vector3(-2f, 1.65f, -3f), new Vector3(2f, 3.2f, 19.6f), 60f),          // from the fountain: keep wall, buttresses, planters, stairs (Ruined shows it whole)
            ("Cap_HM_Ruins", new Vector3(-15.5f, 1.65f, -1.5f), new Vector3(-26f, 0.2f, -8f), 50f),          // from the square's west side over the F03 and F05 ruins
            ("Cap_HM_Townhouses", new Vector3(-1f, 1.65f, -4f), new Vector3(-1f, 5.2f, -27f), 64f),        // from the square at the townhouse pair on F06 and F07 (kit family 3)
            ("Cap_HM_TownhouseSide", new Vector3(22f, 1.65f, -11f), new Vector3(5f, 5.0f, -27f), 55f),    // three-quarter on F07's front and east side
            ("Cap_HM_Kits", new Vector3(45.5f, 1.65f, 12f), new Vector3(39f, 2f, -1f), 62f),           // from the Ring Lane's north end looking south along the east run at the stone cottage on R2_09 and the workshop on R2_08 (kit families 2 and 4)
            ("Cap_HM_LowEast", new Vector3(86f, 2.5f, -14f), new Vector3(30f, -2f, -6f), 50f),            // outside the east wall: ditch, bank, wall, terraces, keep
            ("Cap_HM_SquareSouth", new Vector3(-6.0f, 1.65f, -17.0f), new Vector3(4f, 1.2f, -6f), 55f),   // benches, planters, the Market Street mouth and the bunting
            ("Cap_HM_SquareWest", new Vector3(14.0f, 1.65f, -14.0f), new Vector3(-14f, 1.0f, -8f), 55f),   // the square's west half and the West Lane mouth
            ("Cap_HM_LakeJetty", new Vector3(-22f, 1.3f, -80f), new Vector3(30f, -9f, -106f), 55f),      // low over the water by the future jetty, looking SE across the lake
            ("Cap_HM_Ravine", new Vector3(107.8f, 1.7f, -21.4f), new Vector3(91f, -11.5f, -43f), 55f),    // from the ravine's bank at s ~22 downstream along the pools and weirs W1, W2
            ("Cap_HM_MillLane", new Vector3(55f, 1.65f, -44f), new Vector3(70f, -7f, -58f), 60f),         // on Mill Lane toward the mill and the stream
            ("Cap_HM_Overview", new Vector3(30f, 95f, -98f), new Vector3(0f, -5f, -22f), 42f),          // absolute height, rendered without fog (HollowmereCapture)
        };

        [MenuItem("SleepingCalamity/Art/Build Hollowmere")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            RuinCache.Clear(); ruinFoliage = null;
            TownLayout layout = HollowmereLayout.Create();
            List<string> errors = layout.Validate();
            if (errors.Count > 0) throw new InvalidOperationException("Hollowmere layout invalid (" + errors.Count + "): " + string.Join("; ", errors.Take(20)));
            Debug.Log("SC HOLLOWMERE layout ok foundations=" + layout.Foundations.Count + " landmarks=" + layout.Landmarks.Count + " nodes=" + layout.Nodes.Count + " edges=" + layout.Edges.Count
                + " ring1=" + Mix(layout, 1) + " ring2=" + Mix(layout, 2) + " ring3=" + Mix(layout, 3));
            BeautyCornerBuilder.EnsureLook();
            BuildMaterials();
            BuildScene(layout);
            BeautyCornerBuilder.KeepDecalKeywords();   // the facade row rebuilt the facade materials, which strips the decal keyword: put it back last
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
                BeautyCornerTable.Row placed = StallFolder("Stall_1") != null ? WithoutStallDressing(row) : row;   // the produce stall art carries its own goods and lantern
                if (placed != null) BeautyCornerBuilder.PlaceRow(RebuiltOnlyRows.Contains(row.Id) ? Child(rebuilt, "Corner") : both, placed);
            }
            foreach (Foundation f in t.Foundations) { BuildFoundation(Child(ruined, "Foundations"), f, Preset.Ruined); BuildFoundation(Child(rebuilt, "Foundations"), f, Preset.Rebuilt); }
            foreach (Landmark l in t.Landmarks) { BuildLandmark(Child(ruined, "Landmarks"), l, Preset.Ruined); BuildLandmark(Child(rebuilt, "Landmarks"), l, Preset.Rebuilt); }
            HollowmereSquare.Build(set, ruined, rebuilt, t);
            HollowmereFoliage.Place(ruined, rebuilt, t);   // B9 grass, weeds, moss and ivy on both presets (never inside the square)
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
                Vector3 pos = c.Name == "Cap_HM_Overview" ? c.Pos : new Vector3(c.Pos.x, Mathf.Max(HollowmereLayout.Ground(t, c.Pos.x, c.Pos.z), HollowmereLayout.WaterY) + c.Pos.y, c.Pos.z);   // never under the lake
                Camera cam = LookTestBuilder.AddCapture(cams, c.Name, pos, c.Target, c.Fov);
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
            Terrain terrain = HollowmereTerrain.Build(set, t);
            HollowmereFoliage.TerrainGrass(terrain, t);   // a Unity Terrain from the layout heights (+ baked Terrain Tools erosion on the open ground)
            // The square keeps the approved corner's surround paving (B6 FarCobble with its macro breakup) as a slab over the terrain, so the corner reads as approved.
            Material far = BeautyCornerArt.SurroundGround();
            var sq = LookTestBuilder.Box(set, "SquarePaving", new Vector3((HollowmereLayout.SquareMinX + HollowmereLayout.SquareMaxX) * 0.5f, -0.17f, (HollowmereLayout.SquareMinZ + HollowmereLayout.SquareMaxZ) * 0.5f),
                new Vector3(HollowmereLayout.SquareMaxX - HollowmereLayout.SquareMinX, 0.3f, HollowmereLayout.SquareMaxZ - HollowmereLayout.SquareMinZ),
                far != null ? far : LookTestBuilder.Mat("cobble"), default, far != null ? BeautyCornerArt.FarTileMetres : 3f);

            HollowmereWater.Build(set, t, terrain);   // lake + stepped stream pools + weir falls (SC_Water; HollowmereWater.cs)
        }

        static void BuildWalls(Transform set, TownLayout t)
        {
            Wall(Child(set, "TownWall"), t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TownWallOffset, 16f), 0.5f), 2.4f, 1.0f);
            Wall(Child(set, "TerraceWall"), t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TerraceOffset, 16f), 1.0f, 1.0f), 0.9f, 0.8f);
            Wall(Child(set, "LowerTerraceWall"), t, HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.LowerTerraceOffset, 16f), 1.0f, 1.0f), 0.9f, 0.8f);
        }

        // A run of wall pieces standing on the ground: each piece reaches 1 m below the lowest ground on either side (1.5 m out) and `height` above the
        // highest, so a terrace wall retains its whole step and a free-standing wall gets its parapet height.
        static void Wall(Transform parent, TownLayout t, List<(TownPoint A, TownPoint B)> run, float height, float thick)
        {
            Material stone = LookTestBuilder.Mat("sandstone");
            int i = 0;
            foreach (var (a, b) in run)
            {
                TownPoint mid = (a + b) * 0.5f, dir = b - a;
                var nrm = new TownPoint(dir.Z / dir.Length, -dir.X / dir.Length);
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (TownPoint p in new[] { a, b, mid })
                    foreach (float side in new[] { -1.5f, 0f, 1.5f })
                    {
                        TownPoint q = p + nrm * side;
                        float y = HollowmereLayout.Ground(t, q.X, q.Z);
                        lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    }
                lo -= 1f; hi += height;
                float yaw = Mathf.Atan2(b.X - a.X, b.Z - a.Z) * Mathf.Rad2Deg;
                LookTestBuilder.Box(parent, "Wall" + (i++), new Vector3(mid.X, (lo + hi) * 0.5f, mid.Z), new Vector3(thick, hi - lo, TownPoint.Distance(a, b) + 0.05f), stone, new Vector3(0f, yaw, 0f), 2f);
            }
        }

        // The keep terrace's retaining wall with its parapet, broken up so it is not one blank face: buttresses every 6 m, ivy falling from planters on the
        // parapet, and the two stairs (Keep_StairFoot/Head from the square's north-east corner, Keep_WStairFoot/Head along the face from the Wall Ginnel).
        static void BuildKeep(Transform set, TownLayout t)
        {
            Transform keep = Child(set, "Keep");
            Material stone = LookTestBuilder.Mat("sandstone"), green = BeautyCornerBuilder.PlaceholderMat("green");
            float kh = HollowmereLayout.KeepHeight, kz = HollowmereLayout.KeepWallZ;
            int i = 0;
            var run = HollowmereLayout.WallRun(t, HollowmereLayout.KeepWallLine(1f), 0.3f);
            foreach (var (a, b) in run)
            {
                TownPoint mid = (a + b) * 0.5f;
                LookTestBuilder.Box(keep, "KeepWall" + (i++), new Vector3(mid.X, (kh + 0.9f - 0.6f) * 0.5f, mid.Z), new Vector3(TownPoint.Distance(a, b) + 0.02f, kh + 0.9f + 0.6f, 1.2f), stone, default, 2f);
            }
            for (float x = -HollowmereLayout.KeepHalfWidth + 3f; x < HollowmereLayout.KeepHalfWidth; x += 6f)
            {
                var p = new TownPoint(x, kz - 0.9f);
                if (!InRun(run, x)) continue;   // no buttress in a gap (stairs, the tithe gate)
                if (t.Foundations.Exists(f => f.Footprint.Contains(p, 0.6f))) continue;
                LookTestBuilder.Box(keep, "Buttress" + x, new Vector3(x, kh * 0.45f - 0.3f, kz - 0.95f), new Vector3(1.1f, kh * 0.9f + 0.6f, 0.7f), stone, new Vector3(-6f, 0f, 0f), 2f);
            }
            for (float x = -HollowmereLayout.KeepHalfWidth + 1.5f; x < HollowmereLayout.KeepHalfWidth; x += 4.5f)
            {
                if (!InRun(run, x) || !InRun(run, x - 1f) || !InRun(run, x + 1f)) continue;
                LookTestBuilder.Box(keep, "Planter" + x, new Vector3(x, kh + 1.05f, kz + 0.2f), new Vector3(1.8f, 0.3f, 0.6f), green, default, 2f);
            }
            Stair(keep, "EastStair", t.Node("Keep_StairFoot"), t.Node("Keep_StairHead"), 2.2f, stone);
            Stair(keep, "WestStair", t.Node("Keep_WStairFoot"), t.Node("Keep_WStairHead"), 1.8f, stone);
        }

        static bool InRun(List<(TownPoint A, TownPoint B)> run, float x)
        {
            foreach (var (a, b) in run) if (x >= Mathf.Min(a.X, b.X) - 0.01f && x <= Mathf.Max(a.X, b.X) + 0.01f) return true;
            return false;
        }

        // A straight flight from foot to head: solid steps rising from the foot's height to the head's, run along the line between them.
        static void Stair(Transform parent, string name, PathNode foot, PathNode head, float width, Material stone)
        {
            Transform g = Child(parent, name);
            TownPoint d = head.Pos - foot.Pos;
            float len = d.Length, yaw = Mathf.Atan2(d.X, d.Z) * Mathf.Rad2Deg, rise = head.Y - foot.Y;
            int steps = Mathf.Max(4, Mathf.RoundToInt(Mathf.Abs(rise) / 0.22f));
            for (int s = 0; s < steps; s++)
            {
                float f = (s + 0.5f) / steps, top = foot.Y + rise * (s + 1) / steps, bottom = Mathf.Min(foot.Y, head.Y) - 0.3f;
                TownPoint c = foot.Pos + d * f;
                LookTestBuilder.Box(g, "Step" + s, new Vector3(c.X, (top + bottom) * 0.5f, c.Z), new Vector3(width, top - bottom, len / steps + 0.02f), stone, new Vector3(0f, yaw, 0f), 2f);
            }
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

            // An empty lot shows its ruin (the ruin kit, or the greybox stubs when the art is missing); a placed building or garden switches the ruin off
            // (design 5.3), so it stands on a plain footing slab instead.
            bool ruin = kind == HollowmereLayout.BuildingKind.Empty && PlaceRuin(go.transform, f);
            if (kind != HollowmereLayout.BuildingKind.ApprovedFacade && !ruin)   // the facade brings its own plinth; a slab would add a ledge in front of the approved look
                LookTestBuilder.Box(g, "Footing", new Vector3(0f, 0.05f, 0f), new Vector3(side, 0.4f, side), stone, default, 2f);
            CutAndFill(go.transform, f, stone);
            switch (kind)
            {
                case HollowmereLayout.BuildingKind.Empty:
                    if (ruin) break;
                    LookTestBuilder.Box(g, "Doorstep", new Vector3(0f, 0.11f, -half - 0.45f), new Vector3(1.6f, 0.22f, 0.9f), stone, default, 2f);
                    RuinStubs(g, f, stone);
                    break;
                case HollowmereLayout.BuildingKind.ApprovedFacade:
                    break;   // the corner's Facade row stands here (Corner group of the rebuilt root)
                case HollowmereLayout.BuildingKind.Garden:
                    Garden(g, f);
                    break;
                case HollowmereLayout.BuildingKind.Townhouse:   // kit family 3 art (HollowmereTownhouse); greybox home if the FBX is missing
                    if (!HollowmereTownhouse.Place(g, f)) Building(g, f, HollowmereLayout.BuildingKind.Home);
                    break;
                case HollowmereLayout.BuildingKind.StoneCottage:   // kit family 2 art (HollowmereStoneCottage); greybox home if the FBX is missing
                    if (!HollowmereStoneCottage.Place(g, f)) Building(g, f, HollowmereLayout.BuildingKind.Home);
                    break;
                case HollowmereLayout.BuildingKind.Workshop:   // kit family 4 art (HollowmereWorkshop); greybox home if the FBX is missing
                    if (!HollowmereWorkshop.Place(g, f)) Building(g, f, HollowmereLayout.BuildingKind.Home);
                    break;
                case HollowmereLayout.BuildingKind.Bakery when HollowmereArt.TryPlaceBakery(g, f, 0.25f):   // on the footing slab's top
                    break;
                case HollowmereLayout.BuildingKind.Inn when HollowmereInnArt.TryPlaceInn(g, f, 0.25f):   // on the footing slab's top
                    break;
                case HollowmereLayout.BuildingKind.Smithy when HollowmereArt.TryPlaceBuilding(HollowmereArt.Smithy, g, f, 0.25f):
                    break;
                case HollowmereLayout.BuildingKind.Schoolhouse when HollowmereArt.TryPlaceBuilding(HollowmereArt.Schoolhouse, g, f, 0.25f):
                    break;
                default:
                    Building(g, f, kind);
                    break;
            }
        }

        // The lot's pad against the natural slope (both presets): on a side where the hill stands more than 0.7 m above the pad, a retaining wall 1.3 m out
        // holds the cut; where the hill falls more than 0.7 m below, a stone fill bank under the pad's yard edge holds it up. Built in the lot's own frame
        // (yaw = the lot's Yaw, not the facing turn), so walls run along the lot edges.
        static void CutAndFill(Transform lot, Foundation f, Material stone)
        {
            var g = new GameObject("CutAndFill").transform;
            g.SetParent(lot, false);
            g.rotation = Quaternion.Euler(0f, f.Yaw, 0f);
            float half = f.Side * 0.5f;
            foreach (LotSide s in TownLayout.AllSides)
            {
                TownPoint dir = Foundation.SideDir(s), along = new TownPoint(-dir.Z, dir.X);
                float hi = float.MinValue, lo = float.MaxValue;
                for (int k = -2; k <= 2; k++)
                {
                    TownPoint l = dir * (half + 2.3f) + along * (k * half * 0.45f);
                    TownPoint w = f.Footprint.ToWorld(l.X, l.Z);
                    float y = HollowmereLayout.NaturalGround(w.X, w.Z);
                    hi = Mathf.Max(hi, y); lo = Mathf.Min(lo, y);
                }
                Vector3 outward = new Vector3(dir.X, 0f, dir.Z);
                Vector3 size = Mathf.Abs(dir.X) > 0.5f ? new Vector3(0.7f, 1f, f.Side + 2.6f) : new Vector3(f.Side + 2.6f, 1f, 0.7f);
                if (hi > f.Y + 0.7f)
                {
                    float h = hi - f.Y + 0.6f;
                    LookTestBuilder.Box(g, "Retain" + s, outward * (half + 1.3f) + Vector3.up * (h * 0.5f - 0.3f), new Vector3(size.x, h, size.z), stone, default, 2f);
                }
                if (lo < f.Y - 0.7f)
                {
                    float h = f.Y - lo + 0.8f;
                    LookTestBuilder.Box(g, "Fill" + s, outward * (half + 0.65f) + Vector3.up * (0.25f - h * 0.5f), new Vector3(Mathf.Abs(dir.X) > 0.5f ? 1.3f : f.Side + 1.3f, h, Mathf.Abs(dir.X) > 0.5f ? f.Side + 1.3f : 1.3f), stone, default, 2f);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ ruin kit (Content/Art/Town/Ruins, Reference/hollowmere/ruins)
        // Contract: one FBX per Ruin_<S|M|L>_<A|B|C>, pivot at the lot centre on the ground, front (doorstep) toward local +Z, children Footing (slots
        // FK_Sandstone / FK_ChimneyStone / FK_PlinthJoint / FK_Oak, the B4 facade kit's names, WearLit), Foliage (M_B9_Foliage), FOOTPRINT (hidden) and
        // SOCKET_Doorstep. Placed on the pad at the lot's RuinVariant and RuinTurn (layout data), in the lot's own frame.
        const string RuinsFolder = "Assets/_Project/Content/Art/Town/Ruins";
        sealed class RuinArt { public GameObject Fbx; public Dictionary<string, Material> Slots; }
        static readonly Dictionary<string, RuinArt> RuinCache = new Dictionary<string, RuinArt>();

        static RuinArt LoadRuin(string variant)
        {
            if (RuinCache.TryGetValue(variant, out RuinArt cached)) return cached;
            RuinArt art = null;
            string folder = RuinsFolder + "/" + variant;
            GameObject fbx = AssetDatabase.IsValidFolder(folder) ? BeautyCornerArt.LoadModel(folder, variant + ".fbx", keepMaterialSlots: true) : null;
            if (fbx != null)
            {
                var slots = new Dictionary<string, Material>(BeautyCornerArt.BuildFacadeMaterials());
                slots["M_B9_Foliage"] = RuinFoliage();
                art = new RuinArt { Fbx = fbx, Slots = slots };
            }
            Debug.Log("SC HOLLOWMERE ruin " + variant + " art=" + (art != null ? folder + "/" + variant + ".fbx" : "missing, greybox stubs"));
            RuinCache[variant] = art;
            return art;
        }

        // The ruin folders hold byte-identical copies of the B9 atlas (different GUIDs): build the one shared material from a fixed folder, so it does not
        // flip between copies with the order the variants load in.
        static Material ruinFoliage;
        static Material RuinFoliage()
        {
            if (ruinFoliage == null) ruinFoliage = BeautyCornerArt.MaterialFromSet(RuinsFolder + "/Ruin_S_A", "T_B9_Foliage", "HM_RuinFoliage", foliage: true);
            return ruinFoliage;
        }

        static bool PlaceRuin(Transform lot, Foundation f)
        {
            RuinArt art = LoadRuin(f.RuinVariant);
            if (art == null) return false;
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(art.Fbx, lot);
            obj.name = "Ruin_" + f.RuinVariant.Substring(5) + "_T" + f.RuinTurn;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.rotation = Quaternion.Euler(0f, f.Yaw + 90f * f.RuinTurn, 0f);
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("FOOTPRINT", StringComparison.Ordinal)) { r.enabled = false; continue; }   // the collider / NPC-block shape, never drawn
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!art.Slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE ruin: no material for slot '" + slot + "' on " + r.name); mats[k] = LookTestBuilder.Mat("sandstone"); }
                }
                r.sharedMaterials = mats;
            }
            return true;
        }

        // The ruined lot by its variant (greybox, used when the ruin kit art is missing): a = back wall stub and a side stub; b = a broken back corner and rubble; c = broken front wall each side of the doorway and rubble.
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
            // Bakery and inn leave room inside the body limit for their oven bay (side) and porch (front).
            float w = max * (kind == HollowmereLayout.BuildingKind.Home ? 0.82f + 0.16f * Rand(f.Id, 1) : kind == HollowmereLayout.BuildingKind.Bakery ? 0.74f : 0.95f);
            float d = max * (kind == HollowmereLayout.BuildingKind.Home ? 0.72f + 0.22f * Rand(f.Id, 2) : kind == HollowmereLayout.BuildingKind.Inn ? 0.61f : 0.82f);
            float eave = (f.Size == FoundationSize.S ? 3.6f : f.Size == FoundationSize.M ? 5.6f : 6.2f) + 0.8f * Rand(f.Id, 3);
            if (kind == HollowmereLayout.BuildingKind.Inn) eave = 8.4f;
            float pitch = 36f + 10f * Rand(f.Id, 4);
            float zc = kind == HollowmereLayout.BuildingKind.Inn ? max * 0.5f - d * 0.5f : -(max - d) * 0.35f;   // the block sits toward the door side, the yard behind it; the inn sits back behind its porch
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
                LookTestBuilder.Box(g, "OvenBay", new Vector3(w * 0.5f + 0.6f, 0.25f + 1.4f, zc + d * 0.15f), new Vector3(1.2f, 2.8f, 2.6f), stone, default, 2f);
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
                    if (StallArt(g, l.Id, rebuilt)) break;
                    if (rebuilt && l.Id == "Stall_1") break;   // the corner's MarketStall row
                    if (rebuilt) { StallArt(g); break; }
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
                    if (HollowmereTitheGate.TryPlace(g)) break;   // Wave 2 art (sc_tithe_gate.py) at the row's height; the greybox below stays as the fallback
                    foreach (float x in new[] { -2.3f, 2.3f }) LookTestBuilder.Box(g, "Pier", new Vector3(x, 2.4f, 0f), new Vector3(1.0f, 5.4f, 1.6f), stone, default, 2f);
                    LookTestBuilder.Box(g, "Lintel", new Vector3(0f, 4.6f, 0f), new Vector3(5.6f, 1.0f, 1.6f), stone, default, 2f);
                    LookTestBuilder.Box(g, "Glow", new Vector3(0f, 2.0f, 0.5f), new Vector3(3.6f, 4.0f, 0.05f), BeautyCornerBuilder.PlaceholderMat("warm"), default, 2f);
                    for (int i = 0; i < 9; i++) LookTestBuilder.Box(g, "Bar" + i, new Vector3(-1.8f + i * 0.45f, 2.0f, 0f), new Vector3(0.09f, 4.0f, 0.09f), iron, default, 2f);
                    LookTestBuilder.Box(g, "Rail", new Vector3(0f, 2.6f, 0f), new Vector3(3.7f, 0.12f, 0.12f), iron, new Vector3(0f, 0f, 4f), 2f);   // cracked: the crossbar hangs askew
                    break;
                case LandmarkKind.HeroesHome:
                    if (HeroesHomeArt.Place(g, l.Id))
                    {
                        if (l.Id == "HeroesHome_Cistern")
                        {
                            var stairA = new GameObject("UndercroftStair");   // P25: the way down; a marker for M5/M6 (inside the house)
                            stairA.transform.SetParent(g, false);
                            stairA.transform.position = new Vector3(HollowmereLayout.UndercroftStair.X, l.Y, HollowmereLayout.UndercroftStair.Z);
                            HeroesHomeArt.AddStairLights(g, stairA.transform);
                        }
                    }
                    else if (l.Id == "HeroesHome_Cistern")
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
                    if (HollowmereArt.TryPlaceMill(g, rebuilt)) break;   // Q2 art (sc_mill.py): Shell when ruined, restored Frame + wheel and door when rebuilt
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

        // The three fixed stalls (Wave 1): Ring1/Stalls/<Variant>/Stall_<Variant>.fbx holds both states as children Ruined and Rebuilt, each with its own baked
        // set (Stall_<Variant>_<State>_BaseColor/_Normal/_ORM); the preset keeps its state and drops the other. Placed at the landmark (layout row incl. its pad height),
        // counter to local +Z. False when the art is not in, so the greybox below stands in.
        const string StallsFolder = BeautyCornerArt.ArtRoot + "/Ring1/Stalls";
        static readonly Dictionary<string, string> StallVariants = new Dictionary<string, string> { { "Stall_1", "Produce" }, { "Stall_2", "Cloth" }, { "Stall_3", "Tools" } };
        static readonly HashSet<string> OnStallAssets = new HashSet<string> { "Basket_Produce", "Basket_Bread", "Jug_A", "Jug_B" };   // corner B7 goods on the B8 counter

        static string StallFolder(string id) =>
            StallVariants.TryGetValue(id, out string v) && AssetDatabase.IsValidFolder(StallsFolder + "/" + v) ? StallsFolder + "/" + v : null;

        static bool StallArt(Transform g, string id, bool rebuilt)
        {
            string folder = StallFolder(id);
            if (folder == null) return false;
            string v = StallVariants[id], keep = rebuilt ? "Rebuilt" : "Ruined";
            GameObject model = BeautyCornerArt.LoadModel(folder, "Stall_" + v + ".fbx");
            if (model == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, g);
            go.name = "Stall_" + v + "_" + keep;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            Material m = BeautyCornerArt.MaterialFromSet(folder, "Stall_" + v + "_" + keep, "Stall_" + v + "_" + keep);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (Transform c in go.transform.Cast<Transform>().ToList())
            {
                if (c.name != keep) { UnityEngine.Object.DestroyImmediate(c.gameObject); continue; }
                c.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            var col = go.AddComponent<BoxCollider>();   // counter and frame (B8 header); the awning overhang has none
            col.center = new Vector3(0f, 0.8f, 0f); col.size = new Vector3(2.7f, 1.6f, 1.6f);
            return true;
        }

        // A corner row without the B8 stall, its counter lantern and the B7 goods standing on its counter (the produce stall art replaces all three); null = drop the row.
        static BeautyCornerTable.Row WithoutStallDressing(BeautyCornerTable.Row row)
        {
            if (row.Id == "MarketStall" || row.Id == "StallLantern") return null;
            if (!row.Instances.Any(i => i.Asset != null && OnStallAssets.Contains(i.Asset))) return row;
            return new BeautyCornerTable.Row
            {
                Brief = row.Brief, Id = row.Id, Batch = row.Batch, Names = row.Names, Placeholder = row.Placeholder, Foliage = row.Foliage, ArtYaw = row.ArtYaw, Scatter = row.Scatter,
                Instances = row.Instances.Where(i => i.Asset == null || !OnStallAssets.Contains(i.Asset)).ToArray(),
            };
        }

        // Without the Wave 1 art: stalls 2 and 3 reuse the B8 MarketStall row's art (or its greybox) at their own place and turn.
        static void StallArt(Transform g)
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
        // An inactive empty per doorstep, door and stair node, so M6 work can find them in the hierarchy; the full graph lives in Core (HollowmereLayout)
        // and the approval map draws it.
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
