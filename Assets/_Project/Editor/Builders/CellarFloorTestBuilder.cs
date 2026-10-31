// M5 stage 2: CellarFloorTest.unity, the generated Cellars floor (seed 12345, floor 1) built by CellarFloorBuilder (Presentation, the same code stage 3 runs on
// every client) and lit like the approved dungeon corner: its DungeonCorner_Profile volume, trilight ambient, fog, a baked-in-capture reflection probe and the
// warm gold lamp rig (the builder's own point lights; B11 cellar lanterns on the wall hooks). Dressing: B5 CellarVault FloorTile (four per cell) when the FBX is in,
// CV_* WearLit materials from the dungeon corner, coloured greybox markers. A SpawnPoint at the entry makes it walkable; two fixed capture cameras,
// Floor_Overview (high three-quarter view of the whole floor) and Floor_Room (play-height view into the largest combat room).
// Idempotent through BuilderUtil.SaveScene. Wired into ProjectBuilder.RunAll after DungeonCorner (shares its profile, materials and rendering stack).
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class CellarFloorTestBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CellarFloorTest.unity";
        public const uint Seed = 12345;
        public const int FloorNumber = 1;
        const string MaterialsFolder = "Assets/_Project/Content/Materials/CellarFloor";
        const string LanternFolder = "Assets/_Project/Content/Art/Cellar/B11/CellarLantern";

        [MenuItem("SleepingCalamity/Art/Build Cellar Floor Test")]
        public static void Build()
        {
            CellarFloorLayout layout = CellarFloorGenerator.Generate(Seed, FloorNumber);
            Scene scene = DungeonCornerBuilder.NewRoomScene();
            DungeonCornerBuilder.BuildProfile();
            BeautyCornerArt.EnsureCellarLantern();
            Dictionary<string, Material> own = DungeonCornerBuilder.BuildOwnMaterials();
            Dictionary<string, Material> slots = DungeonCornerBuilder.SlotMaterials(own);
            BuilderUtil.EnsureFolder(MaterialsFolder);

            var root = new GameObject("CellarFloorTest").transform;
            var templates = new GameObject("KitTemplates").transform; templates.SetParent(root, false); templates.gameObject.SetActive(false);
            var kit = new CellarFloorKit
            {
                Floor = slots["CV_Flagstone"], Wall = slots["CV_Brick"], Pillar = slots["CV_VaultStone"], Trim = slots["CV_Oak"], Flame = own["flame"],
                LockedGate = Marker("CF_LockedGate", new Color(0.70f, 0.20f, 0.08f), 0.6f), Chest = Marker("CF_Chest", new Color(0.42f, 0.24f, 0.09f), 0f),
                KeyChest = Marker("CF_KeyChest", new Color(0.95f, 0.72f, 0.12f), 0.8f), Carrier = Marker("CF_Carrier", new Color(0.62f, 0.09f, 0.09f), 0.4f),
                EntryStart = Marker("CF_Entry", new Color(0.30f, 0.58f, 0.18f), 0.5f), StairExit = Marker("CF_Stair", new Color(0.55f, 0.30f, 0.70f), 0.6f),
                SpawnT1 = Marker("CF_SpawnT1", new Color(0.60f, 0.58f, 0.52f), 0.2f), SpawnT2 = Marker("CF_SpawnT2", new Color(0.85f, 0.42f, 0.08f), 0.5f),
            };
            GameObject vault = BeautyCornerArt.LoadModel(DungeonCornerBuilder.KitFolder, DungeonCornerBuilder.KitFbx, keepMaterialSlots: true);
            Transform tileSrc = vault != null ? vault.transform.Find("FloorTile") : null;
            if (tileSrc != null)
            {
                var holder = new GameObject("FloorTile").transform; holder.SetParent(templates, false);
                DungeonCornerBuilder.Clone(tileSrc, holder, slots, "FloorTile");
                kit.FloorTile = holder.GetChild(0).gameObject;
            }
            GameObject lantern = BeautyCornerArt.LoadModel(LanternFolder, "CellarLantern.fbx");
            string[] guids = AssetDatabase.FindAssets("M_CellarLantern t:Material");
            Material lanternMat = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            if (lantern != null && lanternMat != null) { kit.Lantern = lantern; kit.LanternMaterial = lanternMat; }
            Debug.Log("SC CELLARFLOOR kit floorTile=" + (kit.FloorTile != null ? "B5 art" : "greybox slab") + " lantern=" + (kit.Lantern != null ? "B11 art" : "flame sphere"));

            CellarFloorBuild built = CellarFloorBuilder.Build(layout, root, kit);
            Object.DestroyImmediate(templates.gameObject);

            var lighting = new GameObject("Lighting").transform; lighting.SetParent(root, false);
            Vector3 c = built.Bounds.center;
            LookTestBuilder.AddProbe(lighting, new Vector3(c.x, 2.0f, c.z), new Vector3(built.Bounds.size.x + 8f, 4.6f, built.Bounds.size.z + 8f));
            lighting.Find("ReflectionProbe").GetComponent<ReflectionProbe>().intensity = 0.35f;
            LookTestBuilder.AddVolume(lighting, DungeonCornerBuilder.ProfileName);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.23f, 0.19f, 0.18f); RenderSettings.ambientEquatorColor = new Color(0.22f, 0.18f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.11f, 0.09f); RenderSettings.ambientIntensity = 1.6f;   // a roofless 50 m floor needs more fill than the 8 m corner (0.23)
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.004f; RenderSettings.fogColor = new Color(0.20f, 0.15f, 0.11f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.sun = null;

            var spawn = new GameObject("SpawnPoint"); spawn.transform.SetParent(root, false);
            spawn.transform.position = built.EntryCenter + new Vector3(0, 0.1f, 0); spawn.transform.rotation = Quaternion.identity;
            spawn.AddComponent<SpawnPoint>();

            Transform cams = new GameObject("CaptureCameras").transform; cams.SetParent(root, false);
            Vector3 ext = built.Bounds.size; float span = Mathf.Max(ext.x, ext.z);
            LookTestBuilder.AddCapture(cams, "Floor_Overview", c + new Vector3(0, span * 0.66f, -span * 0.40f), c, 50f);
            var (camPos, camTarget) = RoomView(layout);
            LookTestBuilder.AddCapture(cams, "Floor_Room", camPos, camTarget, 78f);
            DungeonCornerBuilder.DarkBackground(cams);
            foreach (Camera cc in cams.GetComponentsInChildren<Camera>(true)) cc.backgroundColor = new Color(0.50f, 0.36f, 0.22f);   // warm umber backdrop instead of the corner's teal-black void

            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC CELLARFLOOR scene built " + ScenePath + " hash=" + layout.LayoutHash() + " tiles=" + built.FloorTiles + " walls=" + built.Walls + " doors=" + built.DoorOpenings + " lamps=" + built.Lamps);
        }

        // The largest combat room (first on a tie): the camera stands at play height in its south-west corner cell and looks at its far north-east cell.
        internal static (Vector3 Pos, Vector3 Target) RoomView(CellarFloorLayout layout)
        {
            int best = -1, bestArea = -1;
            for (int i = 0; i < layout.Rooms.Count; i++)
                if (layout.Rooms[i].Room.Kind == RoomKind.Combat && layout.Rooms[i].Room.FloorCount > bestArea) { best = i; bestArea = layout.Rooms[i].Room.FloorCount; }
            if (best < 0) best = 0;
            PlacedRoom p = layout.Rooms[best];
            int sx = -1, sy = -1, tx = -1, ty = -1;
            for (int lx = 0; lx < p.Width; lx++) for (int ly = 0; ly < p.Height; ly++)
            {
                if (p.Room.CellAt(lx, ly) != CellKind.Floor) continue;
                if (sx < 0 || ly > sy || (ly == sy && lx < sx)) { sx = lx; sy = ly; }                    // southernmost, then westernmost
                if (tx < 0 || ly < ty || (ly == ty && lx > tx)) { tx = lx; ty = ly; }                    // northernmost, then easternmost
            }
            Vector3 pos = CellarFloorBuilder.CellCenter(p.X + sx, p.Y + sy) + new Vector3(-1.2f, 1.7f, -1.2f);
            Vector3 target = CellarFloorBuilder.CellCenter(p.X + tx, p.Y + ty) + new Vector3(0, 1.2f, 0);
            if ((target - pos).sqrMagnitude < 9f) target = pos + new Vector3(1f, -0.1f, 1f) * 5f;
            return (pos, target);
        }

        static Material Marker(string name, Color c, float glow)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found"));
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c); m.SetFloat("_Smoothness", 0.25f);
            if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * glow); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
