// Hollowmere foliage pass (batch B9 on the town, 2026-10-31): grass, weeds, moss and ivy from the B9 models, placed from the layout so two builds give the same scene.
// Four parts, all deterministic (FNV seeds per lot id, no UnityEngine.Random):
//   1. TerrainGrass: instanced grass clumps as Unity Terrain detail prototypes (Grass_A/B/C as mesh details, GPU instancing, VertexLit mode, no custom renderer). The
//      density follows the terrain's grass splat weight (dense on open grass, thin on the worn path, none on soil, cobble, lanes, the square, lot and landmark footprints
//      + 0.3 m or below the lake). Instance-count mode with a dithered 1024 map: a clump is a 0/1 per 0.29 m cell, so the mean density is a clumps-per-m2 number we can
//      budget (see ViewBudget, logged).
//   2. Edges (both presets): Grass_Strip runs lapping over every lot's footing foot (outside, yawed along the edge), Weed_A..E and Grass_A..C clumps at the wall foot and
//      the corners; the doorstep side, lanes, the square and other lots are skipped. Ruined is denser and taller, Rebuilt tidier and sparser. The approved facade lot (F01
//      in Rebuilt) is left alone: it has the corner's own grass strips.
//   3. Ruins (Ruined only): weeds in the footing slab's joints and at the feet of the ruin stubs, Moss_Tuft_A..C along stub bases and tops and in the slab corners.
//      Placed where water and shade gather (wall feet, inner corners), never a uniform coat. Only used for the greybox stubs (HollowmereBuilder.RuinStubs, mirrored here); the ruin kit art carries its own interior foliage.
//   4. Ivy: Ivy_Patch_A/B, Lintel_Ivy and Ivy_Facade on the landmark walls that exist (cistern house, annex, town gate piers, mill; the tithe gate and bell tower art bring their own) and climbing the keep
//      terrace's south retaining wall (dense at the wall ends and beside the buttresses, the stairs' gaps left clear). Ruined carries more, Rebuilt a trimmed-back set.
// The models share one atlas, so everything uses one material (M_B9_Foliage: URP Lit, alpha clip 0.5, both faces, GPU instancing). Pieces are plain mesh objects (no nested
// prefab instances), marked batching-static, under <Preset>/Foliage. Pivots follow B9/SOURCES.md: Blender front -Y = builder -Z, pivots on the wall plane or the ground centre.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereFoliage
    {
        const string AtlasPrefix = "T_B9_Foliage", MaterialName = "B9_Foliage";
        static readonly string[] KitNames = { "Grass_A", "Grass_B", "Grass_C", "Grass_Strip", "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E", "Moss_Tuft_A", "Moss_Tuft_B", "Moss_Tuft_C",
            "Ivy_Patch_A", "Ivy_Patch_B", "Lintel_Ivy", "Ivy_Facade" };
        static readonly string[] Weeds = { "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E" }, Grasses = { "Grass_A", "Grass_B", "Grass_C" }, Mosses = { "Moss_Tuft_A", "Moss_Tuft_B", "Moss_Tuft_C" };

        const float DenseM2 = 3.8f, SparseM2 = 0.3f;   // clumps per m2 in the thick patches and between them; the view budget below fixes the mix
        const int DetailRes = 1024, DetailPatch = 32;
        const float DetailDistance = 60f, AvgGrassTris = 170f;

        sealed class Kit { public string Name; public Mesh[] Meshes; public Matrix4x4[] Local; public int Tris; public Bounds Bounds; }
        sealed class Stats { public int Objects, Tris; }

        static Dictionary<string, Kit> kits;
        static Material mat;
        static Stats cur;
        static int grassClumps;
        static Terrain terr;   // the terrain TerrainGrass dressed: the far hedge roots on its height

        // ------------------------------------------------------------------------------------------------ assets
        static bool Load()
        {
            if (kits != null) return kits.Count == KitNames.Length;
            kits = new Dictionary<string, Kit>();
            string first = BeautyCornerArt.FindFolder("B9", KitNames[0]);
            if (first == null) { Debug.LogWarning("SC HOLLOWMERE foliage: B9 folders missing, no foliage placed"); return false; }
            mat = BeautyCornerArt.MaterialFromSet(first, AtlasPrefix, MaterialName, foliage: true);
            mat.enableInstancing = true;   // terrain details and static batches draw it instanced
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
            foreach (string name in KitNames)
            {
                string folder = BeautyCornerArt.FindFolder("B9", name), fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                GameObject model = fbx != null ? BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx)) : null;
                if (model == null) { Debug.LogWarning("SC HOLLOWMERE foliage: model " + name + " missing"); continue; }
                var k = new Kit { Name = name };
                var meshes = new List<Mesh>(); var mats = new List<Matrix4x4>();
                bool any = false; Bounds b = default;
                foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Matrix4x4 m = model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    meshes.Add(mf.sharedMesh); mats.Add(m);
                    k.Tris += Tris(mf.sharedMesh);
                    Bounds mb = Transformed(mf.sharedMesh.bounds, m);
                    if (!any) { b = mb; any = true; } else b.Encapsulate(mb);
                }
                if (!any) continue;
                k.Meshes = meshes.ToArray(); k.Local = mats.ToArray(); k.Bounds = b;
                kits[name] = k;
                Debug.Log("SC HOLLOWMERE foliage model " + name + " tris=" + k.Tris + " min=" + b.min.ToString("0.00") + " max=" + b.max.ToString("0.00") + (mats.Any(m => !m.isIdentity) ? " (mesh transform not identity)" : ""));
            }
            return kits.Count == KitNames.Length;
        }

        // The shared B9 foliage material (M_B9_Foliage), for other builders' leaf slots (Q15 road hedges); null when the B9 art is missing.
        internal static Material FoliageMaterial() => Load() ? mat : null;

        static int Tris(Mesh m) { int n = 0; for (int i = 0; i < m.subMeshCount; i++) n += (int)m.GetIndexCount(i); return n / 3; }

        static Bounds Transformed(Bounds b, Matrix4x4 m)
        {
            var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return r;
        }

        // ------------------------------------------------------------------------------------------------ small helpers
        static int Seed(string id, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                h ^= (uint)salt * 0x9E3779B9u; h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
                return (int)(h & 0x7fffffff);
            }
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static T Pick<T>(System.Random r, T[] a) => a[r.Next(a.Length)];
        static float YawFacing(float nx, float nz) => Mathf.Atan2(nx, nz) * Mathf.Rad2Deg;   // the imported meshes carry their leaves on local +Z (mesh bounds, first capture showed the back faces)

        // A single B9 grass tuft for other builders (the garden's fence foot): world position, yaw and uniform scale; null when the kit is missing.
        public static GameObject Tuft(Transform parent, string kit, Vector3 worldPos, float yaw, float scale)
        {
            if (!Load()) return null;
            Stats saved = cur; cur = new Stats();
            try { return Put(parent, kit, worldPos, yaw, scale); } finally { cur = saved; }
        }

        static Transform Group(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        static GameObject Put(Transform parent, string kit, Vector3 pos, float yaw, Vector3 scale, bool mirror = false)
        {
            Kit k = kits[kit];
            var go = new GameObject(kit);
            go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(mirror ? -scale.x : scale.x, scale.y, scale.z);
            if (k.Meshes.Length == 1 && k.Local[0].isIdentity) Attach(go, k.Meshes[0], kit);
            else
                for (int i = 0; i < k.Meshes.Length; i++)
                {
                    var c = new GameObject("m" + i); c.transform.SetParent(go.transform, false);
                    c.transform.localPosition = k.Local[i].GetColumn(3); c.transform.localRotation = k.Local[i].rotation; c.transform.localScale = k.Local[i].lossyScale;
                    Attach(c, k.Meshes[i], kit);
                }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            cur.Objects++; cur.Tris += k.Tris;
            return go;
        }

        static GameObject Put(Transform parent, string kit, Vector3 pos, float yaw, float scale, bool mirror = false) => Put(parent, kit, pos, yaw, Vector3.one * scale, mirror);

        static void Attach(GameObject go, Mesh mesh, string kit)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = kit.Contains("Ivy") ? IvyMaterial() : mat;
            r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
        }

        static float G(TownLayout t, float x, float z) => HollowmereLayout.Ground(t, x, z) - 0.03f;   // the terrain sits 0.06 under the layout height: 0.03 down roots the foliage

        static bool InSquare(float x, float z, float pad = 0.2f) =>
            x > HollowmereLayout.SquareMinX - pad && x < HollowmereLayout.SquareMaxX + pad && z > HollowmereLayout.SquareMinZ - pad && z < HollowmereLayout.SquareMaxZ + pad;

        // Not for foliage: the square (the other session dresses it), paving and lanes, under the lake, inside another lot or a landmark (+ margin).
        static bool Blocked(TownLayout t, float x, float z, Foundation self, float margin, bool lanes = true)
        {
            if (InSquare(x, z)) return true;
            if (HollowmereLayout.Ground(t, x, z) < HollowmereLayout.WaterY + 0.5f) return true;
            if (lanes && HollowmereTerrain.IsPaved(t, x, z)) return true;
            var p = new TownPoint(x, z);
            foreach (Foundation f in t.Foundations) if (f != self && f.Footprint.Contains(p, margin)) return true;
            foreach (Landmark l in t.Landmarks) if (l.Footprint.Contains(p, margin)) return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------------ 1. terrain grass
        public static void TerrainGrass(Terrain terrain, TownLayout t)
        {
            grassClumps = 0;
            if (!Load()) return;
            TerrainData data = terrain.terrainData;
            BuilderUtil.EnsureFolder(HollowmereTerrain.Folder);
            terr = terrain;
            var protos = new DetailPrototype[Grasses.Length + 2];
            Color[] healthy = { new Color(0.50f, 0.62f, 0.24f), new Color(0.46f, 0.58f, 0.22f), new Color(0.56f, 0.62f, 0.26f) };
            Color[] dry = { new Color(0.66f, 0.64f, 0.30f), new Color(0.62f, 0.60f, 0.27f), new Color(0.70f, 0.64f, 0.32f) };   // yellow-green family, never blue-green
            for (int i = 0; i < Grasses.Length; i++)
            {
                protos[i] = new DetailPrototype
                {
                    prototype = DetailPrefab(kits[Grasses[i]], Grasses[i]), usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                    minWidth = 0.7f, maxWidth = 1.4f, minHeight = 0.7f, maxHeight = 1.4f, noiseSeed = 11 + i * 7, noiseSpread = 0.4f, holeEdgePadding = 0f,
                    healthyColor = healthy[i], dryColor = dry[i], positionJitter = 1f, alignToGround = 0f, useDensityScaling = true,
                };
            }
            // taller seed-head grass (Grass_C at 1.5-1.9), sparse and mostly in the clumps; a low clover rosette (Weed_D) scattered in low patches
            protos[3] = new DetailPrototype
            {
                prototype = DetailPrefab(kits["Grass_C"], "Grass_C_Tall"), usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = 1.5f, maxWidth = 1.9f, minHeight = 1.5f, maxHeight = 1.9f, noiseSeed = 61, noiseSpread = 0.7f, holeEdgePadding = 0f,
                healthyColor = new Color(0.58f, 0.62f, 0.27f), dryColor = new Color(0.76f, 0.67f, 0.33f), positionJitter = 1f, alignToGround = 0f, useDensityScaling = true,
            };
            protos[4] = new DetailPrototype
            {
                prototype = DetailPrefab(kits["Weed_D"], "Weed_D_Rosette"), usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                minWidth = 0.8f, maxWidth = 1.5f, minHeight = 0.8f, maxHeight = 1.3f, noiseSeed = 73, noiseSpread = 0.5f, holeEdgePadding = 0f,
                healthyColor = new Color(0.42f, 0.56f, 0.22f), dryColor = new Color(0.58f, 0.60f, 0.28f), positionJitter = 1f, alignToGround = 0f, useDensityScaling = true,
            };
            data.SetDetailResolution(DetailRes, DetailPatch);
            data.detailPrototypes = protos;
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            float[,,] splat = data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution);
            int sr = data.alphamapResolution;
            var layers = new int[protos.Length][,];
            for (int i = 0; i < layers.Length; i++) layers[i] = new int[DetailRes, DetailRes];
            float cell = HollowmereTerrain.Size.x / DetailRes, cellArea = cell * (HollowmereTerrain.Size.z / DetailRes);
            var nearCobble = new bool[sr, sr];   // cobble weight above 0 within one alphamap texel (0.6 m): no grass pokes through paving
            for (int y = 0; y < sr; y++)
                for (int x = 0; x < sr; x++)
                    if (splat[y, x, 3] > 0f)
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++) { int yy = y + dy, xx = x + dx; if (yy >= 0 && xx >= 0 && yy < sr && xx < sr) nearCobble[yy, xx] = true; }
            var spots = new List<Vector2>();
            for (int cy = 0; cy < DetailRes; cy++)
                for (int cx = 0; cx < DetailRes; cx++)
                {
                    float u = (cx + 0.5f) / DetailRes, v = (cy + 0.5f) / DetailRes;
                    int sx = Mathf.Min(sr - 1, (int)(u * sr)), sy = Mathf.Min(sr - 1, (int)(v * sr));
                    float w = splat[sy, sx, 0] + 0.3f * splat[sy, sx, 2];
                    if (w <= 0.02f || nearCobble[sy, sx]) continue;
                    float x = HollowmereTerrain.Origin.x + u * HollowmereTerrain.Size.x, z = HollowmereTerrain.Origin.z + v * HollowmereTerrain.Size.z;
                    // round 3: tight clumps. Dense along wall feet, lot and landmark footprint edges and the bank top; two noise octaves (about 7 m / 3.5 m) pick a
                    // few more clumps in the open; bare gaps elsewhere.
                    var pt = new TownPoint(x, z);
                    bool blocked = false, foot = false;
                    foreach (Foundation f in t.Foundations) { if (f.Footprint.Contains(pt, 0.3f)) { blocked = true; break; } if (!foot && f.Footprint.Contains(pt, 2.2f)) foot = true; }
                    if (blocked) continue;
                    foreach (Landmark l in t.Landmarks) { if (l.Footprint.Contains(pt, 0.3f)) { blocked = true; break; } if (!foot && l.Footprint.Contains(pt, 2.2f)) foot = true; }
                    if (blocked) continue;
                    if (Mathf.Abs(HollowmereLayout.PlateauDistance(x, z) - 27.5f) < 2.2f) foot = true;   // the bank top at the town wall's foot
                    float nz = 0.65f * Mathf.PerlinNoise(x * 0.14f + 5f, z * 0.14f + 91f) + 0.35f * Mathf.PerlinNoise(x * 0.29f + 47f, z * 0.29f + 13f);
                    float thick = Mathf.Max(Smooth(0.60f, 0.67f, nz), foot ? Smooth(0.30f, 0.42f, nz) : 0f);
                    float p = Mathf.Lerp(0.18f, 5.5f, thick) * cellArea * Mathf.Clamp01(w);
                    uint h = Hash((uint)cx, (uint)cy);
                    bool lowPatch = Mathf.PerlinNoise(x * 0.21f + 131f, z * 0.21f + 29f) > 0.64f;
                    bool placed = false;
                    if ((h & 0xFFFFFF) / (float)0x1000000 < p && !InSquare(x, z, 0.6f) && !NearPaved(t, x, z)
                        && data.GetInterpolatedHeight(u, v) + HollowmereTerrain.Origin.y >= HollowmereLayout.WaterY + 0.5f)
                    {
                        int pr = PickProto(h >> 24);
                        if (thick > 0.5f && ((h >> 20) & 15u) < 3u) pr = 3;   // seed-head grass mostly inside the clumps
                        else if (thick <= 0.5f && ((h >> 20) & 63u) == 0u) pr = 3;
                        layers[pr][cy, cx] = 1; placed = true;
                    }
                    else if (lowPatch && ((h >> 3) & 0xFFFFFFu) / (float)0x1000000 < 0.9f * cellArea * Mathf.Clamp01(w) && !InSquare(x, z, 0.6f) && !NearPaved(t, x, z)
                        && data.GetInterpolatedHeight(u, v) + HollowmereTerrain.Origin.y >= HollowmereLayout.WaterY + 0.5f)
                    { layers[4][cy, cx] = 1; placed = true; }
                    if (placed) { spots.Add(new Vector2(x, z)); grassClumps++; }
                }
            for (int i = 0; i < layers.Length; i++) data.SetDetailLayer(0, 0, i, layers[i]);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            terrain.detailObjectDistance = DetailDistance;
            terrain.detailObjectDensity = 1f;
            terrain.drawTreesAndFoliage = true;
            Debug.Log("SC HOLLOWMERE foliage grass clumps=" + grassClumps + " prototypes=" + protos.Length);
            ViewBudget(t, spots);
        }

        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }

        static int PickProto(uint h) { uint k = h % 10u; return k < 2u ? 0 : k < 6u ? 1 : 2; }   // Grass_A 20 %, B 40 %, C 40 % (size variety)

        // Paving or a lane within 0.6 m of the point.
        static bool NearPaved(TownLayout t, float x, float z)
        {
            const float m = 0.6f;
            return HollowmereTerrain.IsPaved(t, x, z) || HollowmereTerrain.IsPaved(t, x + m, z) || HollowmereTerrain.IsPaved(t, x - m, z)
                || HollowmereTerrain.IsPaved(t, x, z + m) || HollowmereTerrain.IsPaved(t, x, z - m);
        }

        static uint Hash(uint a, uint b)
        {
            unchecked
            {
                uint h = a * 73856093u ^ b * 19349663u ^ 0x9E3779B9u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return h;
            }
        }

        static GameObject DetailPrefab(Kit k, string name)
        {
            var go = new GameObject("Detail_" + name);
            go.AddComponent<MeshFilter>().sharedMesh = k.Meshes[0];
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, HollowmereTerrain.Folder + "/Detail_" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // Clumps inside a play camera's frustum wedge (detail distance, 16:9) x average clump tris: the cost of the grass in a capture or in play.
        static void ViewBudget(TownLayout t, List<Vector2> spots)
        {
            int worst = 0; string worstCam = "";
            foreach (var c in HollowmereBuilder.Cameras)
            {
                if (c.Name == "Cap_HM_Overview") continue;   // above the detail distance: nothing drawn
                float hfov = 2f * Mathf.Atan(Mathf.Tan(c.Fov * 0.5f * Mathf.Deg2Rad) * 16f / 9f);
                Vector2 pos = new Vector2(c.Pos.x, c.Pos.z), fwd = (new Vector2(c.Target.x, c.Target.z) - pos).normalized;
                float cosHalf = Mathf.Cos(hfov * 0.5f); int n = 0;
                foreach (Vector2 s in spots)
                {
                    Vector2 d = s - pos; float m = d.magnitude;
                    if (m < DetailDistance && (m < 1f || Vector2.Dot(d / m, fwd) > cosHalf)) n++;
                }
                if (n > worst) { worst = n; worstCam = c.Name; }
            }
            int tris = Mathf.RoundToInt(worst * AvgGrassTris);
            Debug.Log("SC HOLLOWMERE foliage grass view worst=" + worstCam + " clumps=" + worst + " ~tris=" + tris + (tris > 400000 ? " OVER the 400k budget" : " (budget 400k)"));
        }

        // ------------------------------------------------------------------------------------------------ the scene pass
        // Called once from HollowmereBuilder.BuildScene after the foundations and landmarks of both presets exist.
        public static void Place(Transform ruined, Transform rebuilt, TownLayout t)
        {
            if (!Load()) return;
            var sr = new Stats(); var sb = new Stats();
            foreach (bool isRuined in new[] { true, false })
            {
                cur = isRuined ? sr : sb;
                Transform root = Group(isRuined ? ruined : rebuilt, "Foliage");
                Transform edges = Group(root, "Edges"), inside = Group(root, "Ruins"), lm = Group(root, "Landmarks"), keep = Group(root, "Keep");
                foreach (Foundation f in t.Foundations)
                {
                    if (!isRuined && HollowmereLayout.RebuiltBuilding(f.Id) == HollowmereLayout.BuildingKind.ApprovedFacade) continue;
                    Edges(edges, t, f, isRuined);
                    if (isRuined && !HasRuinArt(ruined, f)) RuinInterior(inside, t, f);   // the ruin kit art carries its own interior weeds, moss and strips
                }
                foreach (Landmark l in t.Landmarks) LandmarkIvy(lm, t, l, isRuined);
                KeepWall(keep, t, isRuined);
            }
            cur = null;
            Debug.Log("SC HOLLOWMERE foliage ruined=" + sr.Objects + "/" + sr.Tris + " rebuilt=" + sb.Objects + "/" + sb.Tris + " grass=" + grassClumps);
        }

        // ------------------------------------------------------------------------------------------------ 5. far hedge (round 3)
        // A low dark-green hedgerow / tree line in two staggered rows about 125-133 m from (0, -20), just inside the terrain edge, so the bare horizon and the hard sky band are
        // hidden behind it through the fog. B9 leaf-mass cards (Ivy_Patch_A/B stood upright, 3-6x wide, 2.5-6 m tall, facing the town) with Moss_Tuft mounds at the base. Rooted on the
        // terrain height; left open where the ground falls to the lake. One shared material, no shadows. Under Set, so both presets show it.
        // NOT CALLED (2026-10-31): scaled B9 ivy cards read as giant ivy strands, not a hedge, at the horizon; kept for a real tree-line card set (PARKING_LOT).
        public static void FarHedge(Transform set)
        {
            hedgeMat = null;
            if (!Load() || terr == null) return;
            cur = new Stats();
            Transform g = Group(set, "FarHedge");
            var rng = new System.Random(Seed("farhedge", 808));
            var centre = new Vector2(0f, -20f);
            string[] cards = { "Ivy_Patch_A", "Ivy_Patch_B" };
            float cardH = kits["Ivy_Patch_A"].Bounds.max.y;
            float baseY = terr.transform.position.y;
            for (int row = 0; row < 2; row++)
            {
                float radius = row == 0 ? 125f : 131f, arc = 4.6f;
                int n = Mathf.RoundToInt(2f * Mathf.PI * radius / arc);
                for (int k = 0; k < n; k++)
                {
                    float a = (k + (row == 1 ? 0.5f : 0f) + R(rng, -0.25f, 0.25f)) / n * 2f * Mathf.PI, r = radius + R(rng, -2.5f, 2.5f);
                    float x = centre.x + Mathf.Cos(a) * r, z = centre.y + Mathf.Sin(a) * r;
                    if (x < HollowmereTerrain.Origin.x + 6f || x > HollowmereTerrain.Origin.x + HollowmereTerrain.Size.x - 6f
                        || z < HollowmereTerrain.Origin.z + 6f || z > HollowmereTerrain.Origin.z + HollowmereTerrain.Size.z - 6f) continue;
                    float y = terr.SampleHeight(new Vector3(x, 0f, z)) + baseY;
                    float llx = (x - HollowmereLayout.LakeCentre.X) / HollowmereLayout.LakeRadiusX, llz = (z - HollowmereLayout.LakeCentre.Z) / HollowmereLayout.LakeRadiusZ;
                    if (llx * llx + llz * llz < 1.5f * 1.5f || y < HollowmereLayout.WaterY + 0.5f) continue;   // the lake side stays open (the meadow itself sits close to the water level)
                    float lump = Mathf.PerlinNoise(x * 0.07f + 9f, z * 0.07f + 71f);   // long-wave lumps: tree-line heights
                    float height = Mathf.Clamp(Mathf.Lerp(2.5f, 6f, lump) + R(rng, -0.9f, 0.9f) + (row == 1 ? 0.6f : 0f), 2.5f, 6f);
                    float w = R(rng, 3f, 6f);
                    Vector2 toward = (centre - new Vector2(x, z)).normalized;
                    GameObject go = Put(g, cards[rng.Next(2)], new Vector3(x, y - 0.15f, z), YawFacing(toward.x, toward.y) + R(rng, -25f, 25f), new Vector3(w, height / cardH, w * 0.6f), rng.Next(2) == 0);
                    foreach (Renderer rd in go.GetComponentsInChildren<Renderer>()) { rd.shadowCastingMode = ShadowCastingMode.Off; rd.sharedMaterial = HedgeMat(rd.sharedMaterial); }
                    if (row == 0 && k % 2 == 0)   // moss mounds at the base
                    {
                        float ms = R(rng, 10f, 16f);
                        GameObject m = Put(g, Pick(rng, Mosses), new Vector3(x + toward.x * 1.2f, y - 0.05f, z + toward.y * 1.2f), R(rng, 0f, 360f), ms);
                        foreach (Renderer rd in m.GetComponentsInChildren<Renderer>()) rd.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
            Debug.Log("SC HOLLOWMERE foliage hedge objects=" + cur.Objects + " tris=" + cur.Tris + (cur.Tris > 150000 ? " OVER the 150k budget" : " (budget 150k)"));
            cur = null;
        }

        // A4: the wall ivy in the muted 3.1 palette (the atlas's lime young-leaf tone read as flat bright cards): a tinted copy of the foliage material, one shared asset so the ring still
        // instances. Lower value and saturation: sage-olive (0.62, 0.68, 0.50) multiplies the atlas.
        static Material ivyMat;
        static Material IvyMaterial()
        {
            if (ivyMat != null) return ivyMat;
            ivyMat = LookTestBuilder.Ensure("Assets/_Project/Content/Materials/Hollowmere/M_HM_WallIvy.mat", mat.shader);
            ivyMat.CopyPropertiesFromMaterial(mat);
            ivyMat.SetColor("_BaseColor", new Color(0.62f, 0.68f, 0.50f, 1f));
            ivyMat.enableInstancing = true;
            EditorUtility.SetDirty(ivyMat);
            return ivyMat;
        }

        // The far hedge's leaf cards in a darker, deeper green than the wall ivy (the ivy atlas's bright young-leaf tone read as a light strip at 120 m):
        // a copy of the foliage material with a tint, one shared asset so the ring still instances.
        static Material hedgeMat;
        static Material HedgeMat(Material src)
        {
            if (src == null) return null;
            if (hedgeMat != null) return hedgeMat;
            hedgeMat = LookTestBuilder.Ensure("Assets/_Project/Content/Materials/Hollowmere/M_HM_FarHedge.mat", src.shader);
            hedgeMat.CopyPropertiesFromMaterial(src);
            hedgeMat.SetColor("_BaseColor", new Color(0.50f, 0.58f, 0.40f, 1f));
            hedgeMat.enableInstancing = true;
            EditorUtility.SetDirty(hedgeMat);
            return hedgeMat;
        }

        // ------------------------------------------------------------------------------------------------ 2. lot edges
        // Footing slab: side x side, top 0.25 over the pad; on a side where the hill falls away the builder's stone fill (CutAndFill) carries the ground up to the slab top.
        static bool FillSide(Foundation f, LotSide s)
        {
            float half = f.Side * 0.5f; TownPoint dir = Foundation.SideDir(s), along = new TownPoint(-dir.Z, dir.X); float lo = float.MaxValue;
            for (int k = -2; k <= 2; k++)
            {
                TownPoint l = dir * (half + 2.3f) + along * (k * half * 0.45f), w = f.Footprint.ToWorld(l.X, l.Z);
                lo = Mathf.Min(lo, HollowmereLayout.NaturalGround(w.X, w.Z));
            }
            return lo < f.Y - 0.7f;
        }

        static void Edges(Transform parent, TownLayout t, Foundation f, bool ruined)
        {
            var rng = new System.Random(Seed(f.Id, ruined ? 101 : 202));
            Transform g = Group(parent, f.Id);
            Footprint fp = f.Footprint; float half = f.Side * 0.5f;
            float[] baseY = new float[4];
            foreach (LotSide s in TownLayout.AllSides) baseY[(int)s] = FillSide(f, s) ? f.Y + 0.25f : f.Y - 0.03f;
            foreach (LotSide s in TownLayout.AllSides)
            {
                TownPoint dir = Foundation.SideDir(s), along = new TownPoint(-dir.Z, dir.X);
                TownPoint n = fp.AxisX * dir.X + fp.AxisZ * dir.Z;
                float yaw = YawFacing(n.X, n.Z), y = baseY[(int)s];
                bool door = s == f.Facing;
                TownPoint At(float u, float outward) { TownPoint l = dir * (half + outward) + along * u; return fp.ToWorld(l.X, l.Z); }

                int slots = Mathf.Max(3, Mathf.RoundToInt(f.Side / 1.5f)); float step = f.Side / slots;
                for (int k = 0; k < slots; k++)
                {
                    float u = -half + (k + 0.5f) * step + R(rng, -0.12f, 0.12f);
                    if (rng.NextDouble() > (ruined ? 0.8 : 0.45)) continue;
                    if (door && Mathf.Abs(u) < 1.3f) continue;
                    TownPoint p = At(u, 0f);
                    if (Blocked(t, p.X, p.Z, f, 0.1f)) continue;
                    Put(g, "Grass_Strip", new Vector3(p.X, y, p.Z), yaw + R(rng, -5f, 5f), ruined ? R(rng, 0.95f, 1.25f) : R(rng, 0.85f, 1.0f));
                }
                int weeds = Mathf.RoundToInt(f.Side / (ruined ? 2.2f : 4.5f) * (ruined ? 0.85f : 0.3f));   // A3: a tended (Rebuilt) lot keeps about a third of the weeds it had
                float c1 = R(rng, -half + 0.8f, half - 0.8f), c2 = R(rng, -half + 0.8f, half - 0.8f);       // A3: weeds grow in a clump or two along the wall foot, not evenly
                for (int k = 0; k < weeds; k++)
                {
                    float u = Mathf.Clamp((k % 2 == 0 ? c1 : c2) + R(rng, -0.7f, 0.7f), -half + 0.4f, half - 0.4f);
                    if (door && Mathf.Abs(u) < 1.5f) continue;
                    TownPoint p = At(u, R(rng, 0.1f, ruined ? 0.35f : 0.22f));
                    if (Blocked(t, p.X, p.Z, f, 0.1f)) continue;
                    float sc = ruined ? R(rng, 1.0f, 1.4f) : R(rng, 0.75f, 1.0f);
                    Put(g, Pick(rng, Weeds), new Vector3(p.X, y, p.Z), yaw + R(rng, -20f, 20f) + (rng.Next(2) == 0 ? 180f : 0f), new Vector3(sc, sc * (ruined ? R(rng, 1f, 1.25f) : 1f), sc));
                }
                if (ruined && rng.NextDouble() < 0.7)   // a loose clump a little out from the foot
                {
                    float u = R(rng, -half + 0.5f, half - 0.5f);
                    TownPoint p = At(u, R(rng, 0.4f, 0.8f));
                    if (!(door && Mathf.Abs(u) < 1.6f) && !Blocked(t, p.X, p.Z, f, 0.1f))
                        Put(g, Pick(rng, Grasses), new Vector3(p.X, y, p.Z), R(rng, 0f, 360f), R(rng, 0.9f, 1.3f));
                }
            }
            // corners: a grass clump each, a weed on the ruined ones (the outer corner of a footing is where the wind drops seed)
            for (int c = 0; c < 4; c++)
            {
                float sxn = (c == 0 || c == 3) ? -1f : 1f, szn = c < 2 ? -1f : 1f;
                TownPoint p = fp.ToWorld(sxn * (half + 0.18f), szn * (half + 0.18f));
                if (Blocked(t, p.X, p.Z, f, 0.1f)) continue;
                float y = Mathf.Max(baseY[(int)(sxn > 0 ? LotSide.East : LotSide.West)], baseY[(int)(szn > 0 ? LotSide.North : LotSide.South)]);
                Put(g, Pick(rng, Grasses), new Vector3(p.X, y, p.Z), R(rng, 0f, 360f), ruined ? R(rng, 1.0f, 1.35f) : R(rng, 0.8f, 1.0f));
                if (ruined || rng.NextDouble() < 0.3)
                    Put(g, Pick(rng, Weeds), new Vector3(p.X + R(rng, -0.2f, 0.2f), y, p.Z + R(rng, -0.2f, 0.2f)), R(rng, 0f, 360f), ruined ? R(rng, 1.0f, 1.3f) : 0.85f);
            }
        }

        // ------------------------------------------------------------------------------------------------ 3. ruined lots
        // The ruin kit (Content/Art/Town/Ruins) has its weeds, moss and grass inside the S x S footing. Only the greybox stubs (art missing) need the stub-based pass below.
        static bool HasRuinArt(Transform ruined, Foundation f)
        {
            Transform lots = ruined.Find("Foundations");
            if (lots == null) return false;
            foreach (Transform c in lots)
                if (c.name.StartsWith(f.Id + "_", StringComparison.Ordinal)) return c.Find("Ruin_" + f.RuinVariant.Substring(5) + "_T" + f.RuinTurn) != null;
            return false;
        }

        // (centre x/z, size x/z, top height over the pad) of the ruin stubs in the lot frame: mirrors HollowmereBuilder.RuinStubs.
        static List<(float cx, float cz, float sx, float sz, float top)> StubBoxes(Foundation f)
        {
            float S = f.Side, half = S * 0.5f, inset = half - 0.35f;
            char v = f.RuinVariant[f.RuinVariant.Length - 1];
            var l = new List<(float, float, float, float, float)>();
            if (v == 'a') { l.Add((-0.15f * S, inset, 0.65f * S, 0.5f, 1.35f)); l.Add((-inset, 0.2f * S, 0.5f, 0.5f * S, 0.85f)); }
            else if (v == 'b') { l.Add((inset - 0.225f * S, inset, 0.45f * S, 0.5f, 1.85f)); l.Add((inset, inset - 0.18f * S, 0.5f, 0.36f * S, 1.35f)); }
            else
            {
                float w = S * 0.3f;
                l.Add((-half + 0.35f + w * 0.5f, -inset, w, 0.5f, 1.05f)); l.Add((half - 0.35f - w * 0.5f, -inset, w, 0.5f, 0.85f));
            }
            return l;
        }

        static void RuinInterior(Transform parent, TownLayout t, Foundation f)
        {
            var rng = new System.Random(Seed(f.Id, 303));
            Transform g = Group(parent, f.Id);
            float S = f.Side, half = S * 0.5f, slabTop = f.Y + 0.25f - 0.02f;
            Quaternion rot = Quaternion.Euler(0f, f.FacingYaw, 0f); var origin = new Vector3(f.Centre.X, 0f, f.Centre.Z);
            Vector3 W(float lx, float lz, float y) { Vector3 w = origin + rot * new Vector3(lx, 0f, lz); w.y = y; return w; }
            var stubs = StubBoxes(f);
            bool OnStub(float x, float z, float pad) { foreach (var b in stubs) if (Mathf.Abs(x - b.cx) < b.sx * 0.5f + pad && Mathf.Abs(z - b.cz) < b.sz * 0.5f + pad) return true; return false; }
            bool Door(float x, float z) => Mathf.Abs(x) < 1.1f && z < -half + 2.4f;   // the doorway and its way in stay clear

            foreach (var b in stubs)
            {
                bool alongX = b.sx >= b.sz; float len = alongX ? b.sx : b.sz, thick = alongX ? b.sz : b.sx;
                float yawAlong = f.FacingYaw + (alongX ? 0f : 90f);
                for (int side = -1; side <= 1; side += 2)   // the two long faces: water and shade gather at the wall foot
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.2f));
                    for (int k = 0; k < n; k++)
                    {
                        if (rng.NextDouble() > 0.6) continue;
                        float a = R(rng, -len * 0.4f, len * 0.4f), o = side * (thick * 0.5f + R(rng, 0.12f, 0.3f));
                        float lx = b.cx + (alongX ? a : o), lz = b.cz + (alongX ? o : a);
                        if (Mathf.Abs(lx) > half - 0.2f || Mathf.Abs(lz) > half - 0.2f || OnStub(lx, lz, 0.05f) || Door(lx, lz)) continue;
                        if (rng.NextDouble() < 0.55) Put(g, Pick(rng, Weeds), W(lx, lz, slabTop), yawAlong + R(rng, -12f, 12f), R(rng, 0.9f, 1.3f));
                        else Put(g, Pick(rng, Mosses), W(lx, lz, slabTop), R(rng, 0f, 360f), R(rng, 0.9f, 1.4f));
                    }
                }
                // moss on the broken top, off-centre toward one end
                float ta = R(rng, -len * 0.35f, len * 0.35f);
                Put(g, Pick(rng, Mosses), W(b.cx + (alongX ? ta : 0f), b.cz + (alongX ? 0f : ta), f.Y + b.top - 0.02f), R(rng, 0f, 360f), R(rng, 0.8f, 1.1f));
            }
            // slab joints: weeds along two joint lines each way (the slab reads as 2 m flags), clear of stubs and the doorway
            int placed = 0;
            for (int line = 0; line < 4 && placed < 7; line++)
            {
                bool vertical = line < 2; float pos = (line % 2 == 0 ? -1f : 1f) * R(rng, 0.8f, 2.0f);
                for (int k = 0; k < 2; k++)
                {
                    float a = R(rng, -half + 0.6f, half - 0.6f);
                    float lx = vertical ? pos : a, lz = vertical ? a : pos;
                    if (OnStub(lx, lz, 0.4f) || Door(lx, lz) || rng.NextDouble() < 0.25) continue;
                    Put(g, Pick(rng, Weeds), W(lx, lz, slabTop), f.FacingYaw + (vertical ? 90f : 0f) + R(rng, -8f, 8f), R(rng, 0.8f, 1.25f));
                    placed++;
                }
            }
            // slab edge and inner corners (where the rain sheds): moss tufts, the back corners first
            for (int c = 0; c < 4; c++)
            {
                if (rng.NextDouble() < 0.35) continue;
                float sx = (c & 1) == 0 ? -1f : 1f, sz = (c & 2) == 0 ? -1f : 1f;
                float lx = sx * (half - R(rng, 0.2f, 0.5f)), lz = sz * (half - R(rng, 0.2f, 0.5f));
                if (OnStub(lx, lz, 0.1f) || Door(lx, lz)) continue;
                Put(g, Pick(rng, Mosses), W(lx, lz, slabTop), R(rng, 0f, 360f), R(rng, 1.0f, 1.4f));
            }
        }

        // ------------------------------------------------------------------------------------------------ 4. ivy
        // Ivy on a wall of a landmark. (lx, lz) is the point on the wall plane in the landmark frame, n the outward normal there (axis aligned), denseToward the direction along the
        // wall the climber's thick corner side should face. The Ivy_Facade's dense side is its local -x (left seen from outside).
        static void OnLandmarkWall(Transform parent, Landmark l, string kit, float lx, float ly, float lz, Vector3 n, Vector3 denseToward, float scale, bool facade = false)
        {
            Quaternion lr = Quaternion.Euler(0f, l.Footprint.Yaw, 0f);
            Vector3 pos = new Vector3(l.Footprint.Centre.X, l.Y, l.Footprint.Centre.Z) + lr * new Vector3(lx, ly, lz) + lr * (n * 0.02f);
            float localYaw = YawFacing(n.x, n.z);
            Vector3 right = Quaternion.Euler(0f, localYaw, 0f) * Vector3.right;
            bool mirror = denseToward != Vector3.zero && Vector3.Dot(right, denseToward) > 0f;
            // A4: size, width and lean vary per placement (a position hash, so builds match); a facade climber also lets a trailing strand hang from its top.
            uint h = Hash((uint)Mathf.RoundToInt(pos.x * 37f + 40000f), (uint)Mathf.RoundToInt(pos.z * 37f + 40000f + ly * 11f));
            float v1 = (h % 1000u) / 1000f, v2 = ((h >> 10) % 1000u) / 1000f, v3 = ((h >> 20) % 1000u) / 1000f;
            float s1 = scale * (0.8f + 0.5f * v1);
            Put(parent, kit, pos, l.Footprint.Yaw + localYaw + (v3 - 0.5f) * 14f, new Vector3(s1 * (0.8f + 0.45f * v2), s1 * (0.9f + 0.3f * v3), s1), mirror);
            if (kit == "Ivy_Facade" && kits.ContainsKey("Lintel_Ivy"))
            {
                float top = kits["Ivy_Facade"].Bounds.max.y * s1;
                Vector3 hang = pos + lr * (Vector3.Cross(Vector3.up, n).normalized * ((v2 - 0.5f) * 1.2f)) + Vector3.up * (top * (0.85f + 0.12f * v1)) + lr * (n * 0.06f);
                Put(parent, "Lintel_Ivy", hang, l.Footprint.Yaw + localYaw + (v1 - 0.5f) * 20f, new Vector3(0.55f + 0.35f * v3, 0.9f + 0.7f * v2, 0.7f), v1 > 0.5f);
            }
        }

        static void LandmarkIvy(Transform parent, TownLayout t, Landmark l, bool ruined)
        {
            if (!kits.ContainsKey("Ivy_Facade")) return;
            Transform g = Group(parent, l.Id);
            var rng = new System.Random(Seed(l.Id, ruined ? 404 : 505));
            float sx = l.Footprint.HalfX * 2f, sz = l.Footprint.HalfZ * 2f, k = ruined ? 1f : 0.8f;
            Vector3 PX = Vector3.right, NX = Vector3.left, PZ = Vector3.forward, NZ = Vector3.back;
            switch (l.Kind)
            {
                case LandmarkKind.HeroesHome when l.Id == "HeroesHome_Cistern":
                    // door on -z (1.4 x 2.3, a 0.12 slab proud of the wall); patch boards at x = -0.3 sx
                    OnLandmarkWall(g, l, "Lintel_Ivy", 0f, 2.3f, -sz * 0.5f - 0.13f, NZ, Vector3.zero, 0.8f * k);
                    OnLandmarkWall(g, l, "Ivy_Facade", sx * 0.5f, 0f, sz * 0.5f - 1.3f, PX, PZ, k);
                    if (ruined) OnLandmarkWall(g, l, "Ivy_Facade", -sx * 0.5f, 0f, -sz * 0.5f + 1.4f, NX, NZ, 0.95f);
                    OnLandmarkWall(g, l, "Ivy_Patch_A", sx * 0.3f, 0f, -sz * 0.5f, NZ, Vector3.zero, 1.2f * k);
                    if (ruined) OnLandmarkWall(g, l, "Ivy_Patch_B", -sx * 0.1f, 0.2f, sz * 0.5f, PZ, Vector3.zero, 1.3f);
                    break;
                case LandmarkKind.HeroesHome:   // the annex: door on +x
                    OnLandmarkWall(g, l, "Ivy_Facade", -sx * 0.5f + 1.3f, 0f, -sz * 0.5f, NZ, NX, k);
                    OnLandmarkWall(g, l, "Ivy_Patch_A", sx * 0.2f, 0f, -sz * 0.5f, NZ, Vector3.zero, 1.1f * k);
                    if (ruined) OnLandmarkWall(g, l, "Ivy_Facade", -sx * 0.5f, 0f, 0.1f * sz, NX, PZ, 0.9f);
                    break;
                // TitheGate and BellTower: the art brings its own foliage (HollowmereTitheGate, B4 tower), nothing added here.
                case LandmarkKind.TownGate:   // a free-standing pier 2.4 x 2.4, 5 m above its pad
                    OnLandmarkWall(g, l, "Ivy_Patch_A", 0f, 0.2f, -sz * 0.5f, NZ, Vector3.zero, 1.2f * k);
                    if (ruined) { OnLandmarkWall(g, l, "Ivy_Patch_B", sx * 0.5f, 0.2f, 0f, PX, Vector3.zero, 1.3f); OnLandmarkWall(g, l, "Ivy_Patch_B", -sx * 0.5f, 0.2f, 0f, NX, Vector3.zero, 1.3f); }
                    break;
                case LandmarkKind.Mill:   // walls 6 m (rebuilt) or 2.8-6 m (ruined shell); the wheel stands off the +x face
                    OnLandmarkWall(g, l, "Ivy_Facade", -sx * 0.15f, 0f, sz * 0.5f, PZ, NX, k);
                    OnLandmarkWall(g, l, "Ivy_Facade", -sx * 0.5f, 0f, 0.15f * sz, NX, PZ, 0.95f * k);
                    OnLandmarkWall(g, l, "Ivy_Patch_A", 0.5f, 0f, -sz * 0.5f, NZ, Vector3.zero, 1.3f * k);
                    if (ruined) OnLandmarkWall(g, l, "Ivy_Patch_B", sx * 0.3f, 0.3f, sz * 0.5f, PZ, Vector3.zero, 1.4f);
                    break;
            }
        }

        // The keep terrace's retaining wall (HollowmereBuilder.BuildKeep): a 1.2 m thick box row centred on KeepWallZ, south face at KeepWallZ - 0.6, parapet top at
        // KeepHeight + 0.9; buttresses every 6 m from x = -KeepHalfWidth + 3 (where the wall stands and no lot is in the way).
        static void KeepWall(Transform parent, TownLayout t, bool ruined)
        {
            var rng = new System.Random(Seed("keep", ruined ? 606 : 707));
            float kz = HollowmereLayout.KeepWallZ, face = kz - 0.6f, hw = HollowmereLayout.KeepHalfWidth;
            var run = HollowmereLayout.WallRun(t, HollowmereLayout.KeepWallLine(1f), 0.3f);
            var iv = new List<(float a, float b)>();
            foreach (var (a, b) in run)
            {
                float x0 = Mathf.Min(a.X, b.X), x1 = Mathf.Max(a.X, b.X);
                if (iv.Count > 0 && x0 <= iv[iv.Count - 1].b + 0.01f) iv[iv.Count - 1] = (iv[iv.Count - 1].a, Mathf.Max(iv[iv.Count - 1].b, x1)); else iv.Add((x0, x1));
            }
            bool InRun(float x) => iv.Any(i => x >= i.a - 0.01f && x <= i.b + 0.01f);
            var buttress = new List<float>();
            for (float x = -hw + 3f; x < hw; x += 6f)
                if (InRun(x) && !t.Foundations.Exists(f => f.Footprint.Contains(new TownPoint(x, kz - 0.9f), 0.6f))) buttress.Add(x);

            const float halfW = 1.0f;   // half the Ivy_Facade width at scale 0.95
            float fs = 0.95f;           // 5.5 m x 0.95 reaches the parapet (4.9 m)
            var taken = new List<float>();
            Transform g = Group(parent, "Wall");
            bool NearButtress(float x, float w) => buttress.Any(b => Mathf.Abs(b - x) < 0.55f + w);
            bool Taken(float x, float d) => taken.Any(c => Mathf.Abs(c - x) < d);
            void Facade(float x, bool mirror, float s)
            {
                float y = G(t, x, face - 0.3f);
                Put(g, "Ivy_Facade", new Vector3(x, y, face - 0.03f), 180f, s, !mirror);   // yaw 180: leaves to -Z, so local -x (dense side) is world +x
                taken.Add(x);
            }

            foreach (var (a, b) in iv)
            {
                if (b - a < 3.5f) continue;
                if (ruined || rng.NextDouble() < 0.6) { Facade(a + 0.3f + halfW, false, fs); }       // the wall's end is a corner: dense side to the gap
                if (ruined || rng.NextDouble() < 0.6) { Facade(b - 0.3f - halfW, true, fs); }
                for (float x = a + 4.5f; x < b - 4.5f; x += ruined ? R(rng, 5.5f, 8f) : R(rng, 11f, 15f))
                {
                    if (NearButtress(x, halfW) || Taken(x, 2.6f)) continue;
                    Facade(x, rng.Next(2) == 0, R(rng, 0.8f, 1.0f));
                }
            }
            int bi = 0;
            foreach (float bx in buttress)   // the corner a buttress makes: climbers hug it, dense side to the buttress
            {
                if (!ruined && bi++ % 3 != 0) continue;
                bool right = rng.Next(2) == 0; float x = bx + (right ? 1f : -1f) * (0.55f + 0.1f + halfW);
                if (!InRun(x - halfW) || !InRun(x + halfW) || Taken(x, 2.0f)) { right = !right; x = bx + (right ? 1f : -1f) * (0.55f + 0.1f + halfW); }
                if (!InRun(x - halfW) || !InRun(x + halfW) || Taken(x, 2.0f) || NearButtress(x, halfW - 0.05f)) continue;
                Facade(x, !right, fs * R(rng, 0.9f, 1f));
            }
            foreach (var (a, b) in iv)   // patches between, any height; the base gets a strip and weeds where it is not paved
            {
                int patches = Mathf.RoundToInt((b - a) / (ruined ? 3.5f : 9f));
                for (int i = 0; i < patches; i++)
                {
                    float x = R(rng, a + 1f, b - 1f);
                    if (NearButtress(x, 0.7f) || Taken(x, 1.8f)) continue;
                    Put(g, rng.Next(2) == 0 ? "Ivy_Patch_A" : "Ivy_Patch_B", new Vector3(x, G(t, x, face - 0.3f) + R(rng, 0.3f, 2.6f), face - 0.03f), 180f, R(rng, 1.0f, 1.4f), rng.Next(2) == 0);
                }
                for (float x = a + 0.75f; x < b - 0.75f; x += 1.5f)
                {
                    if (rng.NextDouble() > (ruined ? 0.55 : 0.3) || NearButtress(x, 0.75f)) continue;
                    if (Blocked(t, x, face - 0.35f, null, 0f)) continue;
                    Put(g, "Grass_Strip", new Vector3(x, G(t, x, face - 0.3f), face - 0.02f), 180f, R(rng, 0.9f, 1.15f));
                    if (rng.NextDouble() < (ruined ? 0.5 : 0.2)) Put(g, Pick(rng, Weeds), new Vector3(x + R(rng, -0.4f, 0.4f), G(t, x, face - 0.4f), face - 0.3f), 180f + R(rng, -20f, 20f) + (rng.Next(2) == 0 ? 0f : 180f), R(rng, 0.9f, 1.3f));
                }
            }
        }
    }
}
