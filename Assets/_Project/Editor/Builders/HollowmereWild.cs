// Hollowmere's trees and wild growth (area 1 rework, Run 6, decided 2026-11-16): the town stood bare, with nothing taller than a chimney, so every view ended
// in one straight horizon line. This builder plants the adopted tree scans (AreaTreesIntake: Content/Art/Town/Trees/<code>/<code>.prefab, pivot at the trunk
// base, LODGroup, trunk capsule on TR01-TR04) and the growth that goes with them:
//   1. old trees: the great tree of the square in an earth pit, two churchyard trees on the keep terrace;
//   2. town trees: young and middle-aged trees on the unpaved ground between the lots (both presets: the trees outlived the fall of the town);
//   3. the wall belt: a broken line of tall trees on the ditch's outer slope, so the town wall no longer draws the horizon; hedge-line trees past it;
//   4. Bell Hill: boulders and ferns along the crag foot, a 3D forest on the lower hillside, card trees (the road's far-tree atlas) from there to the crest;
//   5. square drift (Ruined only): grass and weed clumps creeping in from the square's edges.
// Everything is seeded by position (a hash of the grid cell), so one rejected spot never moves the others and two builds write the same scene.
// Missing tree prefabs fall back to the next code in the slot, and a slot with no prefab at all plants nothing (logged), so the build never fails on art.
// Placement rules (trees with a collider must never block a walk): 2.2 m clear of every lane edge, 1.6 m of every path-graph edge, 2.5 m of lots and
// landmarks, 7 m of the stream, off paving, water and slopes over 38 degrees, clear of any non-terrain collider, 3.5 m between trunks.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereWild
    {
        const string TreesFolder = "Assets/_Project/Content/Art/Town/Trees/";
        const string AdoptedFolder = "Assets/_Project/Content/Art/Town/Adopted/";
        const string MeshFolder = "Assets/_Project/Content/Art/Town/Wild";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";

        // Slots: the first code whose prefab exists wins.
        static readonly string[] Big = { "TR02", "TR03", "TR01" }, Medium = { "TR03", "TR02", "TR01" }, Small = { "TR01", "TR03", "TR02" },
            Conifer = { "TR04", "TR08", "TR03" }, TallShrub = { "TR05", "TR06" }, LowShrub = { "TR06", "TR07", "TR05" }, Bush = { "TR07", "TR06" };

        public static readonly Vector2 GreatTree = new Vector2(-15f, -11f);
        const float GreatTreeHeight = 14.5f, PitRadius = 2.5f;
        static readonly (float X, float Z, float H)[] ChurchyardTrees = { (-18.5f, 28.5f, 11.5f), (21.8f, 26.5f, 9.5f) };

        static readonly Dictionary<string, (GameObject Prefab, float Height, float Radius)> prefabs = new Dictionary<string, (GameObject, float, float)>();
        static readonly Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();
        static readonly List<Vector2> trunks = new List<Vector2>();
        static readonly List<(Vector2 A, Vector2 B)> pathEdges = new List<(Vector2, Vector2)>();
        static readonly Collider[] hits = new Collider[16];
        static Terrain terrain; static TownLayout layout;
        static int nOld, nTown, nBelt, nHill, nFar, nRocks, nFerns, nDrift, nMissing;

        public static void Reset()
        {
            prefabs.Clear(); boundsCache.Clear(); trunks.Clear(); pathEdges.Clear(); terrain = null; layout = null;
            nOld = nTown = nBelt = nHill = nFar = nRocks = nFerns = nDrift = nMissing = 0;
        }

        // ------------------------------------------------------------------------------------------------ entry
        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            Reset();
            layout = t; terrain = set.GetComponentInChildren<Terrain>();
            if (terrain == null) { Debug.LogWarning("SC HOLLOWMERE wild: no terrain, nothing planted"); return; }
            var nodes = t.Nodes.ToDictionary(n => n.Id, n => new Vector2(n.Pos.X, n.Pos.Z));
            foreach (PathEdge e in t.Edges) if (nodes.TryGetValue(e.A, out Vector2 a) && nodes.TryGetValue(e.B, out Vector2 b)) pathEdges.Add((a, b));
            Physics.SyncTransforms();   // the walls, ruins and props built so far answer the overlap checks

            Transform wild = Group(set, "Wild");
            OldTrees(Group(wild, "OldTrees"));
            TownTrees(Group(wild, "TownTrees"));
            WallBelt(Group(wild, "WallBelt"));
            CragFoot(Group(wild, "CragFoot"));
            HillForest(Group(wild, "HillForest"));
            FarForest(wild);
            SquareDrift(Group(Group(ruined, "Wild"), "SquareDrift"));
            Debug.Log("SC HOLLOWMERE wild: " + nOld + " old trees, " + nTown + " town trees, " + nBelt + " belt trees and shrubs, " + nHill + " hill trees (3D), " + nFar
                + " far card trees, " + nRocks + " crag boulders, " + nFerns + " ferns, " + nDrift + " square tufts" + (nMissing > 0 ? ", " + nMissing + " spots skipped for a missing prefab" : "")
                + "; tree prefabs found: " + string.Join(",", prefabs.Where(p => p.Value.Prefab != null).Select(p => p.Key).OrderBy(s => s)));
        }

        // ------------------------------------------------------------------------------------------------ 1. old trees
        static void OldTrees(Transform parent)
        {
            float y = SurfaceY(GreatTree.x, GreatTree.y, 0f);
            WarnIfOccupied("great tree", GreatTree.x, GreatTree.y, y, 1.2f);
            GameObject great = Plant(parent, Big, new Vector3(GreatTree.x, y + 0.1f, GreatTree.y), GreatTreeHeight, 205f, 1.0f, true, 0f);
            if (great != null) { great.name = "GreatTree"; nOld++; TreePit(parent, new Vector3(GreatTree.x, y, GreatTree.y)); }
            int k = 0;
            foreach (var c in ChurchyardTrees)
            {
                float cy = SurfaceY(c.X, c.Z, HollowmereLayout.Ground(layout, c.X, c.Z));
                WarnIfOccupied("churchyard tree " + k, c.X, c.Z, cy, 0.9f);
                GameObject go = Plant(parent, k == 0 ? Big : Medium, new Vector3(c.X, cy, c.Z), c.H, 40f + 170f * k, 1.0f, true, 2.5f);
                if (go != null) { go.name = "ChurchyardTree_" + k; nOld++; }
                k++;
            }
        }

        // The great tree stands in a round earth bed let into the paving: a low soil mound with a ring of kerb stones (no collider on the kerb: 0.2 m high).
        static void TreePit(Transform parent, Vector3 centre)
        {
            Material soil = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_KT_Soil.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_GD_packed_soil.mat");
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_Dressed.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_TW_Coping.mat");
            BuilderUtil.EnsureFolder(MeshFolder);
            var mb = new MeshBuf();
            const int segs = 28, rings = 4;
            mb.V.Add(new Vector3(0f, 0.24f, 0f)); mb.N.Add(Vector3.up); mb.UV.Add(Vector2.zero);
            for (int r = 1; r <= rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    float u = r / (float)rings, a = s * Mathf.PI * 2f / segs;
                    float rad = PitRadius * u * (1f + 0.05f * Mathf.Sin(a * 3f + 1.1f));
                    float h = 0.24f * (1f - u * u) + 0.03f * Mathf.Sin(a * 5f + u * 4f) * (1f - u);
                    mb.V.Add(new Vector3(Mathf.Cos(a) * rad, h, Mathf.Sin(a) * rad)); mb.N.Add(new Vector3(Mathf.Cos(a) * 0.18f * u, 1f, Mathf.Sin(a) * 0.18f * u).normalized);
                    mb.UV.Add(new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad) / 1.5f);
                }
            for (int s = 0; s < segs; s++) { mb.I.Add(0); mb.I.Add(1 + (s + 1) % segs); mb.I.Add(1 + s); }
            for (int r = 1; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int a = 1 + (r - 1) * segs + s, b = 1 + (r - 1) * segs + (s + 1) % segs, c = a + segs, d = b + segs;
                    mb.I.Add(a); mb.I.Add(b); mb.I.Add(c); mb.I.Add(b); mb.I.Add(d); mb.I.Add(c);
                }
            Mesh soilMesh = HollowmereB3Ground.SaveMesh(MeshFolder + "/TreePit_Soil.asset", mb.ToMesh("TreePit_Soil"));
            var pit = new GameObject("TreePit"); pit.transform.SetParent(parent, false); pit.transform.position = centre + Vector3.up * 0.01f;
            pit.AddComponent<MeshFilter>().sharedMesh = soilMesh;
            var mr = pit.AddComponent<MeshRenderer>(); mr.sharedMaterial = soil; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            pit.AddComponent<MeshCollider>().sharedMesh = soilMesh;

            var kb = new MeshBuf();
            const int stones = 17;
            for (int s = 0; s < stones; s++)
            {
                float a = (s + 0.5f * Hash01(s, 3, 71)) * Mathf.PI * 2f / stones;
                float rad = PitRadius * (1f + 0.05f * Mathf.Sin(a * 3f + 1.1f)) + 0.02f;
                var size = new Vector3(0.62f + 0.22f * Hash01(s, 5, 71), 0.20f + 0.08f * Hash01(s, 7, 71), 0.30f + 0.08f * Hash01(s, 9, 71));
                Quaternion rot = Quaternion.Euler((Hash01(s, 11, 71) - 0.5f) * 9f, -a * Mathf.Rad2Deg + 90f + (Hash01(s, 13, 71) - 0.5f) * 10f, (Hash01(s, 15, 71) - 0.5f) * 7f);
                kb.Box(new Vector3(Mathf.Cos(a) * rad, size.y * 0.5f - 0.05f, Mathf.Sin(a) * rad), size, rot);
            }
            var kerb = new GameObject("TreePit_Kerb"); kerb.transform.SetParent(pit.transform, false);
            kerb.AddComponent<MeshFilter>().sharedMesh = HollowmereB3Ground.SaveMesh(MeshFolder + "/TreePit_Kerb.asset", kb.ToMesh("TreePit_Kerb"));
            var kr = kerb.AddComponent<MeshRenderer>(); kr.sharedMaterial = stone; kr.shadowCastingMode = ShadowCastingMode.On; kr.receiveShadows = true;

            for (int i = 0; i < 16; i++)   // grass and weeds on the mound, thicker toward the rim
            {
                float a = Hash01(i, 21, 73) * Mathf.PI * 2f, rad = PitRadius * (0.45f + 0.5f * Hash01(i, 23, 73));
                float h = 0.24f * (1f - (rad / PitRadius) * (rad / PitRadius));
                string kit = i % 3 == 0 ? "Weed_B" : i % 3 == 1 ? "Grass_B" : "Grass_A";
                if (HollowmereFoliage.Tuft(pit.transform, kit, centre + new Vector3(Mathf.Cos(a) * rad, h - 0.02f, Mathf.Sin(a) * rad), Hash01(i, 25, 73) * 360f, 0.8f + 0.5f * Hash01(i, 27, 73)) != null) nDrift++;
            }
        }

        // ------------------------------------------------------------------------------------------------ 2. town trees
        static void TownTrees(Transform parent)
        {
            const float cell = 6f;
            for (int ix = -9; ix <= 9; ix++)
                for (int iz = -11; iz <= 5; iz++)
                {
                    if (Hash01(ix, iz, 101) > 0.42f) continue;
                    float x = (ix + 0.15f + 0.7f * Hash01(ix, iz, 103)) * cell, z = (iz + 0.15f + 0.7f * Hash01(ix, iz, 105)) * cell;
                    if (HollowmereLayout.PlateauDistance(x, z) > 25f || z > HollowmereLayout.KeepWallZ - 1.5f) continue;   // inside the wall, below the keep terrace
                    if (Mathf.PerlinNoise(x * 0.045f + 31.7f, z * 0.045f + 8.3f) < 0.40f) continue;                          // groves and gaps, not an even scatter
                    if (!Clear(x, z, 2.5f, 3.5f)) continue;
                    float roll = Hash01(ix, iz, 107);
                    string[] slot = roll < 0.5f ? Small : roll < 0.9f ? Medium : Big;
                    float h = roll < 0.5f ? Mathf.Lerp(4.5f, 6.5f, Hash01(ix, iz, 109)) : roll < 0.9f ? Mathf.Lerp(7f, 9.5f, Hash01(ix, iz, 109)) : Mathf.Lerp(10f, 12f, Hash01(ix, iz, 109));
                    if (Plant(parent, slot, new Vector3(x, GroundY(x, z), z), h, Hash01(ix, iz, 111) * 360f, 0.92f + 0.16f * Hash01(ix, iz, 113), true, 3f) != null) { trunks.Add(new Vector2(x, z)); nTown++; }
                }
        }

        // ------------------------------------------------------------------------------------------------ 3. the wall belt
        // Tall trees on the ditch's outer slope (PlateauDistance 34.5-45), in clumps with gaps; past them only on the field hedge lines. The ground there lies
        // 8-11 m under the square, so the trees are tall (12-18 m) to stand 3-8 m over the wall top from inside the town. The lake foreshore stays open.
        static void WallBelt(Transform parent)
        {
            const float cell = 5.5f;
            for (int ix = -19; ix <= 19; ix++)
                for (int iz = -22; iz <= 6; iz++)
                {
                    float x = (ix + 0.1f + 0.8f * Hash01(ix, iz, 201)) * cell, z = (iz + 0.1f + 0.8f * Hash01(ix, iz, 203)) * cell;
                    float e = HollowmereLayout.PlateauDistance(x, z);
                    if (e < 34.5f || e > 60f) continue;
                    if (HollowmereLayout.BackHill(x, z) > 0.05f) continue;                 // the hill forest takes over
                    if (z < -62f && x > -45f && x < 60f) continue;                         // the lake foreshore: the south view stays open over the water
                    float clump = Mathf.PerlinNoise(x * 0.028f + 5.1f, z * 0.028f + 19.4f);
                    bool hedgeLine = false;
                    if (e > 45f)
                    {
                        if (SaltwakeRoadPlan.FieldWeight(layout, x, z) > 0.2f)
                        {
                            SaltwakeRoadPlan.Field(x, z, out float edge, out _);
                            if (edge > 2.2f) continue;                                     // never in the middle of a field
                            hedgeLine = true;
                        }
                        if (Hash01(ix, iz, 205) > (hedgeLine ? 0.30f : 0.22f)) continue;   // thin past the belt
                    }
                    else if (clump < 0.36f) continue;                                      // the gaps in the belt
                    if (!Clear(x, z, 2.5f, hedgeLine ? 5f : 4f)) continue;
                    float roll = Hash01(ix, iz, 207), y = GroundY(x, z);
                    if (clump < 0.43f && e <= 45f)
                    {
                        // the ragged edge of a clump: shrubs and saplings
                        if (Plant(parent, roll < 0.5f ? TallShrub : LowShrub, new Vector3(x, y, z), roll < 0.5f ? Mathf.Lerp(1.8f, 3.2f, Hash01(ix, iz, 209)) : Mathf.Lerp(0.8f, 1.4f, Hash01(ix, iz, 209)),
                            Hash01(ix, iz, 211) * 360f, 1f, false, 2f) != null) nBelt++;
                        continue;
                    }
                    string[] slot = roll < 0.55f ? Medium : roll < 0.82f ? Big : roll < 0.92f ? Conifer : Small;
                    float h = slot == Small ? Mathf.Lerp(7f, 9f, Hash01(ix, iz, 209)) : Mathf.Lerp(12f, 18f, Hash01(ix, iz, 209)) * (hedgeLine ? 0.8f : 1f);
                    if (Plant(parent, slot, new Vector3(x, y, z), h, Hash01(ix, iz, 211) * 360f, 0.9f + 0.2f * Hash01(ix, iz, 213), true, 3.5f) != null) { trunks.Add(new Vector2(x, z)); nBelt++; }
                }
        }

        // ------------------------------------------------------------------------------------------------ 4. Bell Hill
        // Boulders and ferns along the foot of the crag bank, so the cliff meets the level ground in a broken line and not in a crease.
        static void CragFoot(Transform parent)
        {
            GameObject[] rocks = { Adopted("T11a"), Adopted("T11b") }; GameObject fern = Adopted("T12b");
            for (int i = -47; i <= 47; i++)
            {
                float x = (i + 0.8f * Hash01(i, 1, 301)) * 3f;
                float z = HollowmereLayout.HillFoot(x) + Mathf.Lerp(-1.6f, 1.4f, Hash01(i, 2, 301));
                if (!SaltwakeRoadPlan.OnTerrain(x, z, 2f)) continue;
                float roll = Hash01(i, 3, 301);
                if (roll < 0.34f) continue;
                if (!Clear(x, z, 1.5f, 0f, steepLimit: 75f)) continue;
                float y = GroundY(x, z);
                if (roll < 0.62f)
                {
                    GameObject rock = rocks[Hash01(i, 4, 301) < 0.5f ? 0 : 1] ?? rocks[0] ?? rocks[1];
                    if (rock == null) continue;
                    float h = Mathf.Lerp(0.6f, 2.0f, Hash01(i, 5, 301) * Hash01(i, 6, 301) + 0.15f);
                    var go = Instance(parent, rock, new Vector3(x, y - 0.22f * h, z), h / Mathf.Max(0.05f, Height(rock)), Hash01(i, 7, 301) * 360f);
                    go.transform.rotation = Quaternion.Euler((Hash01(i, 8, 301) - 0.5f) * 30f, Hash01(i, 7, 301) * 360f, (Hash01(i, 9, 301) - 0.5f) * 30f);
                    nRocks++;
                }
                else if (fern != null)
                    for (int k = 0; k < 3; k++)
                    {
                        float fx = x + (Hash01(i, 10 + k, 301) - 0.5f) * 2.2f, fz = z + (Hash01(i, 14 + k, 301) - 0.5f) * 2.2f;
                        if (!Clear(fx, fz, 1.0f, 0f, steepLimit: 75f)) continue;
                        float h = Mathf.Lerp(0.5f, 0.95f, Hash01(i, 18 + k, 301));
                        GameObject f = Instance(parent, fern, new Vector3(fx, GroundY(fx, fz) - 0.04f, fz), h / Mathf.Max(0.05f, Height(fern)), Hash01(i, 22 + k, 301) * 360f);
                        foreach (Collider c in f.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                        nFerns++;
                    }
            }
        }

        // The lower hillside, on the terrain: real trees from the top of the crag to z 92, conifers thicker with height. Trunks 4.2 m apart at least, so crowns of
        // 11-16 m trees close into one canopy seen from the town. No colliders here would be wrong at the crag top, so the trunk capsules stay.
        static void HillForest(Transform parent)
        {
            const float cell = 7.2f;
            for (int ix = -19; ix <= 19; ix++)
                for (int iz = 2; iz <= 12; iz++)
                {
                    float x = (ix + 0.1f + 0.8f * Hash01(ix, iz, 401)) * cell, z = (iz + 0.1f + 0.8f * Hash01(ix, iz, 403)) * cell;
                    if (z > 92f || Mathf.Abs(x) > 136f) continue;
                    float hill = HollowmereLayout.BackHill(x, z);
                    if (hill < 0.8f) continue;
                    if (Hash01(ix, iz, 405) > 0.9f) continue;                                // a few holes
                    if (!Clear(x, z, 2.5f, 4.2f, steepLimit: 44f)) continue;
                    float up = Mathf.InverseLerp(6f, 30f, hill);                            // 0 at the crag top, 1 high on the slope
                    float roll = Hash01(ix, iz, 407);
                    bool conifer = roll < Mathf.Lerp(0.30f, 0.60f, up);
                    string[] slot = conifer ? Conifer : roll > 0.85f ? Big : Medium;
                    float h = conifer ? Mathf.Lerp(11f, 17f, Hash01(ix, iz, 409)) : Mathf.Lerp(10f, 15f, Hash01(ix, iz, 409));
                    if (Plant(parent, slot, new Vector3(x, GroundY(x, z), z), h, Hash01(ix, iz, 411) * 360f, 0.9f + 0.2f * Hash01(ix, iz, 413), true, 2f) != null) { trunks.Add(new Vector2(x, z)); nHill++; }
                    // undergrowth between the trunks near the crag top, where the town looks straight at the forest floor
                    if (hill < 14f && Hash01(ix, iz, 415) < 0.55f)
                    {
                        float sx = x + (Hash01(ix, iz, 417) - 0.5f) * 5f, sz = z + (Hash01(ix, iz, 419) - 0.5f) * 5f;
                        if (HollowmereLayout.BackHill(sx, sz) > 0.8f && Clear(sx, sz, 1.5f, 0f, steepLimit: 44f)
                            && Plant(parent, Hash01(ix, iz, 421) < 0.5f ? TallShrub : LowShrub, new Vector3(sx, GroundY(sx, sz), sz), Mathf.Lerp(1.0f, 2.6f, Hash01(ix, iz, 423)), Hash01(ix, iz, 425) * 360f, 1f, false, 2f) != null) nHill++;
                    }
                }
        }

        // From z 86 to the crest: card trees with the road's far-tree atlas (HollowmereRoad.CardForest), on the terrain to its north edge and on the backdrop
        // ring past it. The grid opens with distance (9 m near, 14 m by the crest): the haze closes the gaps.
        static void FarForest(Transform wild)
        {
            var trees = new List<(Vector3 Base, float Yaw, float Height, int Variant, bool OnRing)>();
            for (int ix = -70; ix <= 70; ix++)
                for (int iz = 9; iz <= 62; iz++)
                {
                    float x0 = ix * 9f, z0 = iz * 9f;
                    float d = z0 - HollowmereLayout.HillFoot(x0);
                    if (d > 230f && ((ix + iz) & 1) == 1) continue;                         // every other tree far up
                    float x = x0 + (Hash01(ix, iz, 501) - 0.5f) * 7.5f, z = z0 + (Hash01(ix, iz, 503) - 0.5f) * 7.5f;
                    if (z < 86f || d > 500f) continue;
                    if (HollowmereLayout.BackHill(x, z) < 6f) continue;
                    if (Hash01(ix, iz, 505) > 0.93f) continue;
                    bool onTerrain = SaltwakeRoadPlan.OnTerrain(x, z, 1f);
                    float y = onTerrain ? GroundY(x, z) : HollowmereRoad.RingHeight(x, z);
                    trees.Add((new Vector3(x, y, z), Hash01(ix, iz, 507) * 360f, Mathf.Lerp(12f, 19f, Hash01(ix, iz, 509)) * (d > 230f ? 1.25f : 1f), (int)(Hash01(ix, iz, 511) * 3.999f), !onTerrain));
                }
            nFar = HollowmereRoad.CardForest(wild, "HillCards", trees);
        }

        // ------------------------------------------------------------------------------------------------ 5. square drift
        // Ruined only: the square has been empty for years, so grass and weeds creep in from its edges and gather in clumps (never on the walked lines: 1.6 m
        // clear of path edges, 5.8 m of the fountain). Rooted on whatever surface a ray finds (the cobble bed), skipped where a prop stands.
        static void SquareDrift(Transform parent)
        {
            string[] kits = { "Grass_A", "Grass_B", "Grass_C", "Weed_A", "Weed_C", "Weed_D", "Grass_B", "Moss_Tuft_A" };
            for (int c = 0; c < 46; c++)
            {
                // clump centres hug the square's rim: pick a side, a place along it, and a depth of 0.6-4.5 m (deeper ones rarer)
                int side = (int)(Hash01(c, 1, 601) * 3.999f); float along = Hash01(c, 2, 601), depth = 0.6f + 3.9f * Hash01(c, 3, 601) * Hash01(c, 4, 601);
                float cx = side == 0 ? HollowmereLayout.SquareMinX + depth : side == 1 ? HollowmereLayout.SquareMaxX - depth : Mathf.Lerp(HollowmereLayout.SquareMinX, HollowmereLayout.SquareMaxX, along);
                float cz = side == 2 ? HollowmereLayout.SquareMinZ + depth : side == 3 ? HollowmereLayout.SquareMaxZ - depth : Mathf.Lerp(HollowmereLayout.SquareMinZ, HollowmereLayout.SquareMaxZ, along);
                int n = 3 + (int)(Hash01(c, 5, 601) * 5f);
                for (int i = 0; i < n; i++)
                {
                    float a = Hash01(c, 10 + i, 603) * Mathf.PI * 2f, r = 1.3f * Mathf.Sqrt(Hash01(c, 20 + i, 603));
                    float x = cx + Mathf.Cos(a) * r, z = cz + Mathf.Sin(a) * r;
                    if (x < HollowmereLayout.SquareMinX + 0.2f || x > HollowmereLayout.SquareMaxX - 0.2f || z < HollowmereLayout.SquareMinZ + 0.2f || z > HollowmereLayout.SquareMaxZ - 0.2f) continue;
                    if (x * x + z * z < 5.8f * 5.8f) continue;
                    if (x > -8f && x < 6f && z > 2f) continue;                              // the approved corner's own paving and crack weeds stand as they are
                    if (PathDistance(new Vector2(x, z)) < 1.6f) continue;
                    if (!Physics.Raycast(new Vector3(x, 3f, z), Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.y > 0.3f || hit.point.y < -0.4f) continue;                // a prop, a kerb or a hole: not the square's floor
                    string kit = kits[(int)(Hash01(c, 30 + i, 603) * (kits.Length - 0.001f))];
                    float s = (kit.StartsWith("Moss") ? 1.0f : 0.75f) + 0.55f * Hash01(c, 40 + i, 603);
                    if (HollowmereFoliage.Tuft(parent, kit, new Vector3(x, hit.point.y - 0.02f, z), Hash01(c, 50 + i, 603) * 360f, s) != null) nDrift++;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ placement helpers
        static Transform Group(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        static GameObject Adopted(string code) => AssetDatabase.LoadAssetAtPath<GameObject>(AdoptedFolder + code + "/" + code + ".prefab");

        // The tree prefab of a code with its height and footprint radius at unit scale (pivot at the trunk base); a null prefab is cached too.
        static (GameObject Prefab, float Height, float Radius) Tree(string code)
        {
            if (prefabs.TryGetValue(code, out var cached)) return cached;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreesFolder + code + "/" + code + ".prefab");
            (GameObject, float, float) entry = (null, 1f, 1f);
            if (prefab != null)
            {
                Bounds b = PrefabBounds(prefab);
                entry = (prefab, Mathf.Max(0.1f, b.max.y), Mathf.Max(b.extents.x, b.extents.z));
                Debug.Log("SC HOLLOWMERE wild tree " + code + " height " + b.max.y.ToString("F2") + " crown radius " + Mathf.Max(b.extents.x, b.extents.z).ToString("F2"));
            }
            prefabs[code] = entry;
            return entry;
        }

        static float Height(GameObject prefab) => PrefabBounds(prefab).size.y;

        // A young tree for a ruined lot (HollowmereRuinDressing): no collider (the lot is a build site and the tree goes with the ruin), a little lean.
        // Null when no tree prefab has landed (the caller falls back to its card sapling).
        internal static GameObject LotTree(Transform parent, Vector3 worldPos, float height, float yaw) =>
            Plant(parent, height < 5.6f ? Small : Medium, worldPos, height, yaw, 0.95f, false, 4f);

        static Bounds PrefabBounds(GameObject prefab)
        {
            if (boundsCache.TryGetValue(prefab, out Bounds cached)) return cached;
            Bounds fresh = MeasureBounds(prefab);
            boundsCache[prefab] = fresh;
            return fresh;
        }

        static Bounds MeasureBounds(GameObject prefab)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tmp.transform.localScale = Vector3.one;
            Bounds b = default; bool any = false;
            LODGroup lod = tmp.GetComponent<LODGroup>();
            Renderer[] rs = lod != null && lod.lodCount > 0 ? lod.GetLODs()[0].renderers : tmp.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rs) { if (r == null) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Object.DestroyImmediate(tmp);
            return any ? b : new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        // One tree or shrub of the slot at `pos` (its foot on the ground), scaled to `height`, sunk a little so the root flare meets the soil, with a slight seeded lean.
        static GameObject Plant(Transform parent, string[] slot, Vector3 pos, float height, float yaw, float girth, bool keepCollider, float leanDeg)
        {
            (GameObject Prefab, float Height, float Radius) tree = default;
            foreach (string code in slot) { tree = Tree(code); if (tree.Prefab != null) break; }
            if (tree.Prefab == null) { nMissing++; return null; }
            float s = height / tree.Height;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(tree.Prefab, parent);
            go.transform.position = pos - Vector3.up * (0.035f * height);
            float lx = (Hash01(Mathf.RoundToInt(pos.x * 10f), Mathf.RoundToInt(pos.z * 10f), 901) - 0.5f) * 2f * leanDeg, lz = (Hash01(Mathf.RoundToInt(pos.x * 10f), Mathf.RoundToInt(pos.z * 10f), 903) - 0.5f) * 2f * leanDeg;
            go.transform.rotation = Quaternion.Euler(lx, yaw, lz);
            go.transform.localScale = new Vector3(s * girth, s, s * girth);
            if (!keepCollider) foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        static GameObject Instance(Transform parent, GameObject prefab, Vector3 pos, float scale, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static float GroundY(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

        // The top of whatever is built at (x, z) near the expected height (paving slab, terrace floor), else the terrain.
        static float SurfaceY(float x, float z, float expected)
        {
            if (Physics.Raycast(new Vector3(x, expected + 3f, z), Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return GroundY(x, z);
        }

        static float Steepness(float x, float z)
        {
            Vector3 o = terrain.transform.position, size = terrain.terrainData.size;
            return terrain.terrainData.GetSteepness(Mathf.Clamp01((x - o.x) / size.x), Mathf.Clamp01((z - o.z) / size.z));
        }

        static float PathDistance(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (var e in pathEdges)
            {
                Vector2 ab = e.B - e.A; float t = Mathf.Clamp01(Vector2.Dot(p - e.A, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                d = Mathf.Min(d, Vector2.Distance(p, e.A + ab * t));
            }
            return d;
        }

        static float LaneEdge(TownPoint p)
        {
            float d = float.MaxValue;
            foreach (Lane lane in layout.Lanes) d = Mathf.Min(d, HollowmereLayout.DistanceToPolyline(p, lane.Points) - lane.Width * 0.5f);
            return d;
        }

        // The placement rules of the file header. `margin` is the clearance from lots and landmarks, `trunkGap` the distance to other trunks (0: not a tree).
        static bool Clear(float x, float z, float margin, float trunkGap, float steepLimit = 38f)
        {
            if (!SaltwakeRoadPlan.OnTerrain(x, z, 1.5f)) return false;
            var p = new TownPoint(x, z); var p2 = new Vector2(x, z);
            if (HollowmereTerrain.IsPaved(layout, x, z) || LaneEdge(p) < 2.2f) return false;
            if (PathDistance(p2) < 1.6f) return false;
            foreach (Foundation f in layout.Foundations) if (f.Footprint.Contains(p, margin)) return false;
            foreach (Landmark l in layout.Landmarks) if (l.Footprint.Contains(p, margin)) return false;
            if (HollowmereLayout.DistanceToPolyline(p, HollowmereLayout.Stream) < 7f) return false;
            float y = GroundY(x, z);
            if (y < HollowmereLayout.WaterY + 1.2f) return false;
            if (Steepness(x, z) > steepLimit) return false;
            if (trunkGap > 0f) foreach (Vector2 q in trunks) if ((q - p2).sqrMagnitude < trunkGap * trunkGap) return false;
            int n = Physics.OverlapSphereNonAlloc(new Vector3(x, y + 1.3f, z), 1.0f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider)) return false;
            return true;
        }

        static void WarnIfOccupied(string what, float x, float z, float y, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(new Vector3(x, y + radius + 0.4f, z), radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!(hits[i] is TerrainCollider) && hits[i].gameObject.name != "SquarePaving")
                    Debug.LogWarning("SC HOLLOWMERE wild: the " + what + " at (" + x + ", " + z + ") stands inside " + hits[i].gameObject.name + " (parent " + (hits[i].transform.parent != null ? hits[i].transform.parent.name : "-") + ")");
        }

        // A stable 0..1 hash of two integers and a salt (same on every machine: integer math only).
        static float Hash01(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        sealed class MeshBuf
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> I = new List<int>();

            public void Box(Vector3 centre, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 n in dirs)
                {
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up, w = Vector3.Cross(n, u);
                    int b = V.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float su = k == 0 || k == 3 ? -1f : 1f, sw = k < 2 ? -1f : 1f;
                        Vector3 local = Vector3.Scale(n + u * su + w * sw, h);
                        V.Add(centre + rot * local); N.Add(rot * n);
                        UV.Add(new Vector2(Vector3.Dot(local, u), Vector3.Dot(local, w)) / 0.8f + new Vector2(centre.x, centre.z));
                    }
                    I.Add(b); I.Add(b + 1); I.Add(b + 2); I.Add(b); I.Add(b + 2); I.Add(b + 3);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(I, 0);
                m.RecalculateBounds(); m.RecalculateTangents();
                return m;
            }
        }
    }
}
