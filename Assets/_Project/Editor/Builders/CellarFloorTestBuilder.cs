// M5 stage 2: CellarFloorTest.unity, the generated Cellars floor (seed 12345, floor 1) built by CellarFloorBuilder (Presentation, the same code stage 3 runs on
// every client) and lit like the approved dungeon corner: its DungeonCorner_Profile volume, trilight ambient, fog, a baked-in-capture reflection probe and the
// warm gold lamp rig (the builder's own point lights; B11 cellar lanterns on the wall hooks). Dressing: B5 CellarVault FloorTile (four per cell) when the FBX is in,
// CV_* WearLit materials from the dungeon corner, coloured greybox markers. A SpawnPoint at the entry makes it walkable; three fixed capture cameras,
// Floor_Overview (high three-quarter view of the whole floor, ceilings hidden by the capture), Floor_Room (play-height view into the largest combat room) and
// Floor_Corridor (down the longest straight corridor). The art layer (vaults, B5 faces and dressing, candles) lives in CellarFloorBuilder.Art.cs.
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
        const string ScenePathBase = "Assets/_Project/Scenes/CellarFloorTest";
        public const uint Seed = 12345;
        public const int FloorNumber = 1;   // the default floor; SC_FLOOR=<1-7> builds another (look-table checks: floors 1, 4, 6), into CellarFloorTest_f<N>.unity, floor 1 keeps the plain name
        // The floor this run builds: env SC_FLOOR (1-7), default FloorNumber.
        public static int Floor => int.TryParse(System.Environment.GetEnvironmentVariable("SC_FLOOR"), out int f) ? Mathf.Clamp(f, CellarFloorLook.FirstFloor, CellarFloorLook.LastFloor) : FloorNumber;
        public static string ScenePath => Floor == FloorNumber ? ScenePathBase + ".unity" : ScenePathBase + "_f" + Floor + ".unity";
        const float Ambient = CellarArtTuning.Ambient;   // the colour tuning block lives in CellarFloorBuilder.Art.cs (was 0.8)
        const string ClutterFolder = "Assets/_Project/Content/Art/Cellar/B5/CellarClutter";
        static readonly string[] ClutterPieces = { "Crate", "CrateStack", "JarCluster", "Rubble", "SackPile" };
        const string MaterialsFolder = "Assets/_Project/Content/Materials/CellarFloor";
        const string LanternFolder = "Assets/_Project/Content/Art/Cellar/B11/CellarLantern";
        static readonly string[] ArtPieces = { "VaultSegment", "ArchRib", "Pillar", "EndWall", "NicheWall", "LarderShelf", "CoalChute", "Barrels", "Roots", "BreathGrate", "GroinBay", "PillarRound", "WallAshlar", "WallBrickArch", "WallSoot", "WallLime", "SconceCage", "LanternChain", "LanternOil" };

        [MenuItem("SleepingCalamity/Art/Build Cellar Floor Test")]
        public static void Build()
        {
            CellarFloorLookRow look = CellarFloorLook.For(Floor);
            CellarFloorLayout layout = CellarFloorGenerator.Layout(Seed, Floor);
            Scene scene = DungeonCornerBuilder.NewRoomScene();
            DungeonCornerBuilder.BuildProfile();

            var root = new GameObject("CellarFloorTest").transform;
            var templates = new GameObject("KitTemplates").transform; templates.SetParent(root, false); templates.gameObject.SetActive(false);
            CellarFloorKit kit = BuildKit(templates);
            CellarFloorBuild built = CellarFloorBuilder.Build(layout, root, kit);
            HideQaMarkers(built.Root);
            Object.DestroyImmediate(templates.gameObject);

            var lighting = new GameObject("Lighting").transform; lighting.SetParent(root, false);
            Vector3 c = built.Bounds.center;
            LookTestBuilder.AddProbe(lighting, new Vector3(c.x, 2.0f, c.z), new Vector3(built.Bounds.size.x + 8f, 4.6f, built.Bounds.size.z + 8f));
            lighting.Find("ReflectionProbe").GetComponent<ReflectionProbe>().intensity = 0.35f;
            LookTestBuilder.AddVolume(lighting, DungeonCornerBuilder.ProfileName);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientTop; RenderSettings.ambientEquatorColor = look.AmbientEquator;   // B8: the floor's trilight from the look table (design section 3)
            RenderSettings.ambientGroundColor = look.AmbientBottom; RenderSettings.ambientIntensity = Ambient;   // vaulted rooms: nearer the corner's 0.23 than the roofless greybox's 1.6
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
            var (corPos, corTarget) = CorridorView(layout);
            LookTestBuilder.AddCapture(cams, "Floor_Corridor", corPos, corTarget, 70f);
            AddFixtureViews(cams, root, built, layout);
            DungeonCornerBuilder.DarkBackground(cams);
            cams.Find("Floor_Overview").GetComponent<Camera>().backgroundColor = OverviewBackdrop(Floor);   // per floor, so the 320 px thumbnails tell the floors apart (the roofless cutaway)

            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC CELLARFLOOR scene built " + ScenePath + " floor=" + Floor + " overlays=" + built.Overlays + " hash=" + layout.LayoutHash() + " tiles=" + built.FloorTiles + " walls=" + built.Walls + " doors=" + built.DoorOpenings + " lamps=" + built.Lamps);
        }

        // The art-layer kit of the real Cellars floor: floor tile, B5 vault pieces, the floor-art-2 clutter (Crate, CrateStack, JarCluster, Rubble, SackPile), lantern, materials
        // and fog. `templates` (an inactive child of whatever carries the kit) holds the piece holders. Shared by this scene and the CellarFloorSync prefab (SaveRuntimeKit).
        internal static CellarFloorKit BuildKit(Transform templates)
        {
            BeautyCornerArt.EnsureCellarLantern();
            Dictionary<string, Material> own = DungeonCornerBuilder.BuildOwnMaterials();
            Dictionary<string, Material> slots = DungeonCornerBuilder.SlotMaterials(own);
            BuilderUtil.EnsureFolder(MaterialsFolder);
            var kit = new CellarFloorKit
            {
                Floor = slots["CV_Flagstone"], Wall = slots["CV_Brick"], Pillar = slots["CV_VaultStone"], Trim = slots["CV_Oak"], Flame = own["flame"],
                // B2: the locked gate and plain chests wear the cellar oak instead of glowing red / flat brown greybox blocks; the spawn, entry, special-room and door-hint markers are QA-only and draw nothing (CellarFloorBuilder.ShowDebugMarkers; the spawn discs read as white discs on the floor)
                LockedGate = slots["CV_Oak"], Chest = slots["CV_BarrelOak"],
                KeyChest = slots["CV_Oak"], Carrier = Marker("CF_Carrier", new Color(0.62f, 0.09f, 0.09f), 0.4f),
                EntryStart = Marker("CF_Entry", new Color(0.30f, 0.58f, 0.18f), 0.5f), StairExit = Marker("CF_Stair", new Color(0.55f, 0.30f, 0.70f), 0.6f),
                SpawnT1 = Marker("CF_SpawnT1", new Color(0.62f, 0.60f, 0.55f), 0f), SpawnT2 = Marker("CF_SpawnT2", new Color(0.85f, 0.45f, 0.10f), 0f),
                FogBaseDensity = 0.007f, FogBaseColor = new Color(0.16f, 0.13f, 0.11f),   // the corner's fog; the floor's atmosphere roll scales and warms this
                Ceiling = slots["CV_VaultStone"], Limewash = slots["CV_Limewash"], Iron = slots["CV_Iron"], Crate = slots["CV_BarrelOak"],
                Wax = own["wax"], Mote = own["mote"],
                // B8 overlays: the adopted C03 alpha-clip sets (C03a moss patches, C03b moss clump, C03c water streak, C03d mud/damp); null when the asset is not there (cobwebs C04 has none)
                DecalMoss = AdoptedMaterial("C03a"), DecalMossClump = AdoptedMaterial("C03b"), DecalLeak = AdoptedMaterial("C03c"), DecalDamp = AdoptedMaterial("C03d"),
                Pieces = new Dictionary<string, GameObject>(),
            };
            GameObject vault = BeautyCornerArt.LoadModel(DungeonCornerBuilder.KitFolder, DungeonCornerBuilder.KitFbx, keepMaterialSlots: true);
            Transform tileSrc = vault != null ? vault.transform.Find("FloorTile") : null;
            if (tileSrc != null)
            {
                var holder = new GameObject("FloorTile").transform; holder.SetParent(templates, false);
                DungeonCornerBuilder.Clone(tileSrc, holder, slots, "FloorTile");
                kit.FloorTile = holder.GetChild(0).gameObject;
            }
            // the art layer's B5 pieces: one holder per piece, the clone keeps its authored transform inside it
            foreach (string piece in ArtPieces)
            {
                Transform src = vault != null ? vault.transform.Find(piece) : null;
                if (src == null) continue;
                var holder = new GameObject(piece).transform; holder.SetParent(templates, false);
                DungeonCornerBuilder.Clone(src, holder, slots, piece);
                kit.Pieces[piece] = holder.gameObject;
            }
            // the floor-art-2 clutter kit (Crate, CrateStack, JarCluster, Rubble, SackPile): same CV_* material slots, loaded by path; a missing FBX leaves the greybox fallback
            GameObject clutterFbx = BeautyCornerArt.LoadModel(ClutterFolder, "CellarClutter.fbx", keepMaterialSlots: true);
            foreach (string piece in ClutterPieces)
            {
                Transform src = clutterFbx != null ? clutterFbx.transform.Find(piece) : null;
                if (src == null) continue;
                var holder = new GameObject(piece).transform; holder.SetParent(templates, false);
                DungeonCornerBuilder.Clone(src, holder, slots, piece);
                kit.Pieces[piece] = holder.gameObject;
            }
            Debug.Log("SC CELLARFLOOR art pieces " + kit.Pieces.Count + "/" + (ArtPieces.Length + ClutterPieces.Length) + ": " + string.Join(",", kit.Pieces.Keys));
            GameObject lantern = BeautyCornerArt.LoadModel(LanternFolder, "CellarLantern.fbx");
            string[] guids = AssetDatabase.FindAssets("M_CellarLantern t:Material");
            Material lanternMat = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            if (lantern != null && lanternMat != null) { kit.Lantern = lantern; kit.LanternMaterial = lanternMat; }
            Debug.Log("SC CELLARFLOOR kit floorTile=" + (kit.FloorTile != null ? "B5 art" : "greybox slab") + " lantern=" + (kit.Lantern != null ? "B11 art" : "flame sphere"));
            return kit;
        }

        // B8 round 2: the greybox QA markers (carrier capsule, stair disc, bank disc, healing disc and orb, baker, warden, entry and spawn discs) stand in the hierarchy for tests but draw
        // nothing in this look-test scene, so captures show the floor, not placeholders. Chests keep their oak boxes.
        static void HideQaMarkers(Transform floorRoot)
        {
            Transform markers = floorRoot.Find(CellarFloorBuilder.MarkerGroup);
            if (markers == null) return;
            string[] hide = { "Carrier", "StairExit", "Bank", "HealingPoint", "HealingOrb", "Baker", "Warden", "EntryStart", "Spawn" };
            foreach (Transform t in markers)
                foreach (string h in hide)
                    if (t.name.StartsWith(h)) { foreach (MeshRenderer r in t.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false; break; }
        }

        // B8 round 3: the overview backdrop per floor: cool grey limewash (1-2), honey gold (3-5), dark soot (6-7).
        static Color OverviewBackdrop(int floor) => floor <= 2 ? new Color(0.46f, 0.45f, 0.43f) : floor <= 5 ? new Color(0.55f, 0.38f, 0.14f) : new Color(0.12f, 0.08f, 0.07f);

        // Floor_Groin: play height under the first groin bay (an interior cell of a room 3+ cells each way); Floor_Fixture: close on the first wall sconce. Both fall back to the room view / skip when absent.
        static void AddFixtureViews(Transform cams, Transform root, CellarFloorBuild built, CellarFloorLayout layout)
        {
            Transform ceiling = built.Root.Find(CellarFloorBuilder.CeilingGroup);
            Transform groin = null;
            if (ceiling != null) foreach (Transform t in ceiling) if (t.name.StartsWith("GroinBay_")) { groin = t; break; }
            if (groin != null) LookTestBuilder.AddCapture(cams, "Floor_Groin", groin.position + new Vector3(-3.2f, 1.7f, -3.2f), groin.position + new Vector3(0.6f, 3.2f, 0.6f), 78f);
            Transform lamps = built.Root.Find(CellarFloorBuilder.LampGroup), sconce = null;
            if (lamps != null) foreach (Transform l in lamps) { sconce = l.Find("Sconce"); if (sconce != null) break; }
            if (sconce != null) LookTestBuilder.AddCapture(cams, "Floor_Fixture", sconce.position + sconce.forward * 4.2f + new Vector3(0.8f, -0.5f, 0f), sconce.position, 38f);
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
            Vector3 pos = CellarFloorBuilder.CellCenter(p.X + sx, p.Y + sy) + new Vector3(-0.7f, 1.7f, -0.7f);   // 0.99 m from the cell centre: the clutter keeps 1.0 m clear of it, so the camera never sits inside a piece
            Vector3 target = CellarFloorBuilder.CellCenter(p.X + tx, p.Y + ty) + new Vector3(0, 1.2f, 0);
            if ((target - pos).sqrMagnitude < 9f) target = pos + new Vector3(1f, -0.1f, 1f) * 5f;
            return (pos, target);
        }

        // The longest straight corridor run (first found on a tie, scanning rows then columns): the camera stands at play height half a cell inside one end
        // and looks down the run to the far end. Falls back to the room view when the floor has no straight corridor cell.
        internal static (Vector3 Pos, Vector3 Target) CorridorView(CellarFloorLayout layout)
        {
            int n = CellarFloorLayout.GridSize;
            var mask = new int[n, n]; var has = new bool[n, n];
            foreach (PlacedCorridor c in layout.Corridors) if (c.X >= 0 && c.Y >= 0 && c.X < n && c.Y < n) { mask[c.X, c.Y] = c.Mask; has[c.X, c.Y] = true; }
            int ns = CellarRoom.MaskBit(CellSide.North) | CellarRoom.MaskBit(CellSide.South), ew = CellarRoom.MaskBit(CellSide.East) | CellarRoom.MaskBit(CellSide.West);
            int best = 0; Vector3 a = default, b = default;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    int run = 0;
                    for (int j = 0; j <= n; j++)
                    {
                        int x = pass == 0 ? j : i, y = pass == 0 ? i : j;
                        bool straight = j < n && has[x, y] && mask[x, y] == (pass == 0 ? ew : ns);
                        if (straight) { run++; continue; }
                        if (run > best)
                        {
                            best = run;
                            int x0 = pass == 0 ? j - run : i, y0 = pass == 0 ? i : j - run, x1 = pass == 0 ? j - 1 : i, y1 = pass == 0 ? i : j - 1;
                            a = CellarFloorBuilder.CellCenter(x0, y0); b = CellarFloorBuilder.CellCenter(x1, y1);
                        }
                        run = 0;
                    }
                }
            if (best == 0) return RoomView(layout);
            Vector3 dir = (b - a).sqrMagnitude > 0.01f ? (b - a).normalized : Vector3.forward;
            Vector3 pos = a - dir * (CellarFloorBuilder.CellSize * 0.5f - 0.6f) + new Vector3(0.5f, 1.6f, 0f);
            Vector3 target = b + dir * CellarFloorBuilder.CellSize + new Vector3(0, 1.5f, 0);
            return (pos, target);
        }

        const string RuntimeKitFolder = "Assets/_Project/Content/Art/Cellar/FloorKit";

        // The same kit for the game: every template saved as a prefab asset (so a prefab can reference it) and the result written into the CellarFloorSync prefab's Kit,
        // which makes every peer's runtime floor the dressed one. Idempotent (SaveAsPrefabAsset overwrites).
        public static void SaveRuntimeKit()
        {
            BuilderUtil.EnsureFolder(RuntimeKitFolder);
            var holderRoot = new GameObject("CellarFloorKitTemplates");
            try
            {
                CellarFloorKit kit = BuildKit(holderRoot.transform);
                GameObject Save(GameObject template, string name)
                {
                    template.SetActive(true);   // a saved prefab is a plain active asset (the builder also activates its clones)
                    return PrefabUtility.SaveAsPrefabAsset(template, RuntimeKitFolder + "/" + name + ".prefab");
                }
                if (kit.FloorTile != null) { kit.FloorTile.transform.SetParent(holderRoot.transform, false); kit.FloorTile = Save(kit.FloorTile, "FloorTile"); }   // the tile itself, not its holder (the builder sets its pose)
                var saved = new List<CellarFloorKit.NamedPiece>();
                foreach (KeyValuePair<string, GameObject> piece in kit.Pieces) saved.Add(new CellarFloorKit.NamedPiece { Name = piece.Key, Prefab = Save(piece.Value, piece.Key) });
                kit.PieceList = saved; kit.Pieces = null;
                // the lantern is the FBX asset itself (not a scene clone), so it is referenced as is
                BuilderUtil.WritePrefab(AssetPaths.CellarFloorSyncPrefab, root => BuilderUtil.GetOrAdd<CellarFloorSync>(root).Kit = kit);
                Debug.Log("SC CELLARFLOOR runtime kit pieces=" + saved.Count + " floorTile=" + (kit.FloorTile != null) + " lantern=" + (kit.Lantern != null));
            }
            finally { Object.DestroyImmediate(holderRoot); }
        }

        static Material AdoptedMaterial(string code) => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/B2/M_" + code + ".mat");

        static Material Marker(string name, Color c, float glow)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found"));
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c); m.SetFloat("_Smoothness", 0.25f);
            if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * glow); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
