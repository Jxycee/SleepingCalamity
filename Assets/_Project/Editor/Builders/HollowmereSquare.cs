// Hollowmere square ground and dressing (design docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 6 rows "Square ground" and
// "Square dressing", section 5.3). Called once by HollowmereBuilder.BuildScene.
//  - Ground: "Set/SquareGround" (both presets share it) holds SquareBed.fbx (the corner's CobbleBed material) and a second CobbleSpawner that lays the 35k setts of
//    B6/SquareCobbles/square_placements.json with the SAME Sett_<v> prefabs the corner uses; the drains of that file get the Square/Drain_A art (origin at its top
//    centre, flush with the setts) or a dark greybox slab. The holder stands at the shelf height HollowmereLayout.Ground(t, 0, 0). The corner's PlazaCobbles row is untouched.
//  - Dressing: the rows of Core HollowmereDressing become "Deco_<Id>" roots under Preset_Ruined/Dressing, Preset_Rebuilt/Dressing or (preset Both) Set/SquareDressing,
//    placed at the ground height of their spot. Art goes through BeautyCornerBuilder.PlaceRow (real FBX when Content/Art/Town/<Batch>/<Asset>/ exists, else a greybox
//    sized to the footprint). Each root carries a BoxCollider of its footprint (colliders only, no runtime component) so M6 can find it later.
//  - Bunting: composed along a parabola (the catenary's low-sag shape) from the Square/Bunting FBX child meshes Pennant_0..3 (Pennant_T0/T1 for torn spans) and
//    Rope_1m (origin at its start, running along +X, 1 m long), one shared material; thin dark cylinders and small coloured quads when the art is missing.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereSquare
    {
        const string SquareBatch = "Square";
        const float DefaultSoilHeight = 0.45f, PotSoilHeight = 0.4f, PotScale = 1.5f, DriftWarn = 0.02f, PennantSpacing = 0.33f, RopeSegment = 0.5f;
        static readonly Vector3 DefaultHook = new Vector3(0f, 2.85f, 0.6f);   // lantern post hook point (Unity terms, post-local) when SOURCES.md does not say

        [System.Serializable] sealed class PStone { public string v; public float[] p; }
        [System.Serializable] sealed class PDrain { public float[] p; public float yaw; }
        [System.Serializable] sealed class PSlot { public float[] p; public string kind; }
        [System.Serializable] sealed class PFile { public PStone[] stones; public PDrain[] drains; public PSlot[] weed_slots; }

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            BuildGround(set, ruined, t);
            HollowmereB3Ground.BuildDecals(set, t);   // B3 (S2 table): leaf drifts, puddles, cart rut, moss and dirt patches, shared by both presets
            BuildDressing(set, ruined, rebuilt, t);
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

        static Transform Child(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ---------------------------------------------------------------------------------------------------- ground
        static void BuildGround(Transform set, Transform ruined, TownLayout t)
        {
            string folder = BeautyCornerArt.FindFolder("B6", "SquareCobbles");
            string json = folder != null ? folder + "/square_placements.json" : null;
            if (json == null || !File.Exists(BuilderUtil.Abs(json))) { Debug.LogWarning("SC HOLLOWMERE square ground: B6/SquareCobbles/square_placements.json is missing, the square keeps the paving slab only"); return; }
            PFile data = JsonUtility.FromJson<PFile>(File.ReadAllText(BuilderUtil.Abs(json)));
            if (data == null || data.stones == null || data.stones.Length == 0) { Debug.LogWarning("SC HOLLOWMERE square ground: no stones in " + json); return; }

            float shelf = HollowmereLayout.Ground(t, 0f, 0f);
            Transform holder = new GameObject("SquareGround").transform; holder.SetParent(set, false);
            holder.localPosition = new Vector3(0f, shelf, 0f);

            // The stones are laid in shelf-relative metres on a flat shelf; the town's ground is not flat under the whole square (the lane stubs slope away), so the setts and
            // the bed are draped: a 0.5 m grid of Ground - shelf over the stones' bounds (+1 m), added to every stone's y by the spawner and to the bed's vertices here.
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (PStone s in data.stones)
            {
                if (s.p == null || s.p.Length < 3) continue;
                minX = Mathf.Min(minX, s.p[0]); maxX = Mathf.Max(maxX, s.p[0]); minZ = Mathf.Min(minZ, s.p[2]); maxZ = Mathf.Max(maxZ, s.p[2]);
            }
            const float step = 0.5f;
            var origin = new Vector2(Mathf.Floor((minX - 1f) / step) * step, Mathf.Floor((minZ - 1f) / step) * step);
            int nx = Mathf.CeilToInt((maxX + 1f - origin.x) / step) + 1, nz = Mathf.CeilToInt((maxZ + 1f - origin.y) / step) + 1;
            var drop = new float[nx * nz];
            float maxCorr = 0f, maxCornerCorr = 0f, maxSlope = 0f;
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = origin.x + ix * step, z = origin.y + iz * step;
                    float c = HollowmereLayout.Ground(t, x, z) - shelf;
                    drop[iz * nx + ix] = c;
                    maxCorr = Mathf.Max(maxCorr, Mathf.Abs(c));
                    if (x >= -12f && x <= 8f && z >= -6f && z <= 8f) maxCornerCorr = Mathf.Max(maxCornerCorr, Mathf.Abs(c));
                    if (ix > 0) maxSlope = Mathf.Max(maxSlope, Mathf.Abs(c - drop[iz * nx + ix - 1]) / step);
                    if (iz > 0) maxSlope = Mathf.Max(maxSlope, Mathf.Abs(c - drop[(iz - 1) * nx + ix]) / step);
                }
            float residual = 0f; var cells = new HashSet<long>();
            foreach (PStone s in data.stones)
            {
                if (s.p == null || s.p.Length < 3) continue;
                long key = ((long)Mathf.FloorToInt(s.p[0] / 2f) << 32) ^ (uint)Mathf.FloorToInt(s.p[2] / 2f);
                if (!cells.Add(key)) continue;
                residual = Mathf.Max(residual, Mathf.Abs(HollowmereLayout.Ground(t, s.p[0], s.p[2]) - shelf - CobbleSpawner.SampleDrop(drop, origin, step, nx, nz, s.p[0], s.p[2])));
            }
            Debug.Log("SC HOLLOWMERE square ground: shelf y " + shelf.ToString("F3") + ", draped on a " + nx + "x" + nz + " grid (" + step + " m): max correction " + maxCorr.ToString("F3") + " m, max slope " + maxSlope.ToString("F3")
                + " (" + (Mathf.Atan(maxSlope) * Mathf.Rad2Deg).ToString("F1") + " deg), residual after drape " + residual.ToString("F3") + " m over " + cells.Count + " samples");
            string corner = "SC HOLLOWMERE square ground: max correction inside the corner rect (x -12..8, z -6..8) " + maxCornerCorr.ToString("F3") + " m";
            if (maxCornerCorr > 0.01f) Debug.LogWarning(corner + " (over 0.01 m: the draped square would move the approved corner's seam)"); else Debug.Log(corner);
            if (residual > DriftWarn) Debug.LogWarning("SC HOLLOWMERE square ground: residual " + residual.ToString("F3") + " m after the drape (over " + DriftWarn + " m)");

            // B3 / DH4: the 35k setts are gone; the draped bed carries the T01 cobble scan (UV = metres / 2, macro tint in the detail slot). The old corner bed material is only the
            // fallback when the scan is missing.
            Material ground = HollowmereB3Ground.SquareGroundMaterial();
            if (ground == null)
            {
                BeautyCornerGround.SettPrefabs(data.stones.Select(s => s.v).Where(v => !string.IsNullOrEmpty(v)).Distinct(), out Material bed);
                if (bed != null) ground = SquareBedMaterial(bed);
                Debug.LogWarning("SC HOLLOWMERE square ground: Art/Source/Fab/T01_cobblestone_floor is missing, the bed keeps " + (ground != null ? "the old grey-brown corner material" : "no material"));
            }
            GameObject bedFbx = BeautyCornerArt.LoadModel(folder, "SquareBed.fbx");
            if (bedFbx != null)
            {
                var bedGo = (GameObject)PrefabUtility.InstantiatePrefab(bedFbx, holder); bedGo.name = "Art_SquareBed";
                if (ground != null) BeautyCornerGround.Assign(bedGo, ground);
                DrapeBed(bedGo, holder, folder, drop, origin, step, nx, nz);
            }
            else Debug.LogWarning("SC HOLLOWMERE square ground: SquareBed.fbx is missing");
            HollowmereB3Ground.BuildEdges(holder, t, folder);

            PlaceDrains(holder, data.drains, t, shelf);
            ScatterEdges(holder, ruined, data.weed_slots, drop, origin, step, nx, nz, t);
        }

        // A copy of the shared CobbleBed material (the beauty corner keeps its own) tinted from olive to a dark warm brown like the mortar between the setts.
        static Material SquareBedMaterial(Material bed)
        {
            const string folder = "Assets/_Project/Content/Materials/Hollowmere";
            BuilderUtil.EnsureFolder(folder);
            Material m = LookTestBuilder.Ensure(folder + "/M_SquareBed.mat", bed.shader);
            m.CopyPropertiesFromMaterial(bed);
            foreach (var k in bed.enabledKeywords) m.EnableKeyword(k.name);
            Color tint = new Color(0.92f, 0.76f, 1.0f, 1f);
            m.SetColor("_BaseColor", tint); m.SetColor("_Color", tint);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // B3 tint for the tufts: the shared M_CrackWeeds atlas reads lime on the square, so the square gets its own olive copy (the corner keeps the original).
        static Material OliveTufts(Material src)
        {
            Material m = LookTestBuilder.Ensure("Assets/_Project/Content/Materials/Hollowmere/M_SquareTufts.mat", src.shader);
            m.CopyPropertiesFromMaterial(src);
            foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
            m.renderQueue = src.renderQueue;
            Color olive = new Color(0.70f, 0.74f, 0.52f, 1f);
            m.SetColor("_BaseColor", olive); m.SetColor("_Color", olive);
            m.enableInstancing = true;
            MaterialValidation.Validate(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // B9 tufts on the json's weed slots, like BeautyCornerGround.Weeds does for the corner: lot_edge / edge_grass (the worn grass-to-cobble edges) get a dense mix of
        // moss tufts and grass clumps, missing_sett gets weeds and moss, joint gets small tufts. One shared foliage material, seeded scale and yaw, heights capped.
        static void ScatterEdges(Transform holder, Transform ruined, PSlot[] slots, float[] drop, Vector2 origin, float step, int nx, int nz, TownLayout t)
        {
            if (slots == null || slots.Length == 0) return;
            var moss = new List<GameObject>(); var grass = new List<GameObject>(); var weeds = new List<GameObject>();
            Material mat = null;
            void Load(string prefix, string letters, List<GameObject> into)
            {
                foreach (char c in letters)
                {
                    string folder = BeautyCornerArt.FindFolder("B9", prefix + c);
                    string fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                    GameObject go = fbx != null ? BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx)) : null;
                    if (go == null) continue;
                    into.Add(go);
                    if (mat == null) mat = BeautyCornerArt.CrackWeedsMaterial();   // the same atlas and material as the corner's weeds
                }
            }
            Load("Moss_Tuft_", "ABC", moss); Load("Grass_", "ABC", grass); Load("Weed_", "ABCDE", weeds);
            if (mat == null) { Debug.LogWarning("SC HOLLOWMERE square edges: no B9 tuft art, edges stay bare"); return; }
            mat = OliveTufts(mat);
            float Height(GameObject go) { Bounds b = default; bool any = false; foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); } return any ? Mathf.Max(0.01f, b.size.y) : 1f; }
            var heights = new Dictionary<GameObject, float>();
            foreach (var l in new[] { moss, grass, weeds }) foreach (GameObject g in l) heights[g] = Height(g);

            // A3: the weeds cluster (value noise over the slot positions, ~3 m blobs) and thin out on the joints of the open centre; the shared "EdgeTufts" group is what a tended
            // (Rebuilt) square keeps (about a fifth), "EdgeTufts_RuinedOnly" under the Ruined root holds the rest. Keep tests use a per-slot hash, so builds are identical.
            Transform group = Child(holder, "EdgeTufts");
            Transform ruinedGroup = new GameObject("EdgeTufts_RuinedOnly").transform;
            ruinedGroup.SetParent(ruined, false); ruinedGroup.position = holder.position; ruinedGroup.rotation = holder.rotation;
            var rnd = new System.Random(23);
            int n = 0, nEdge = 0, nMissing = 0, nJoint = 0;
            foreach (PSlot s in slots)
            {
                if (s.p == null || s.p.Length < 3) continue;
                float blob = Mathf.PerlinNoise(s.p[0] * 0.33f + 17.3f, s.p[2] * 0.33f + 5.1f);
                float cluster = 0.02f + 0.98f * Mathf.SmoothStep(0.48f, 0.66f, blob);   // blobs along the edges, hardly any stray between them (round 2: steeper)
                float baseKeep = s.kind == "joint" ? 0.08f : s.kind == "missing_sett" ? 0.9f : 1f;   // the open-centre joints thin hardest, cracks (missing setts) and wall edges keep theirs
                float keepRoll = (float)(Hash((uint)Mathf.RoundToInt(s.p[0] * 50f + 50000f), (uint)Mathf.RoundToInt(s.p[2] * 50f + 50000f)) % 10007u) / 10007f;
                // round 2: weeds belong at walls and kerbs. Distance from the slot to the square's border (the lots' fronts and kerbs): inside 1.2 m everything stays, beyond 3.5 m only strays.
                Vector3 wp = holder.TransformPoint(new Vector3(s.p[0], 0f, s.p[2]));
                float dEdge = Mathf.Min(Mathf.Min(wp.x - HollowmereLayout.SquareMinX, HollowmereLayout.SquareMaxX - wp.x), Mathf.Min(wp.z - HollowmereLayout.SquareMinZ, HollowmereLayout.SquareMaxZ - wp.z));
                // B3 (DH4): tufts only within ~1.5 m of the border, at the lots' footings (1.5 m margin) and at the fountain foot (3.5 m); the open middle stays clean cobble.
                bool atFooting = false;
                foreach (Foundation f in t.Foundations) if (f.Footprint.Contains(new TownPoint(wp.x, wp.z), 1.5f)) { atFooting = true; break; }
                bool atFountain = wp.x * wp.x + wp.z * wp.z < 3.5f * 3.5f;
                if (dEdge > 1.5f && !atFooting && !atFountain) continue;
                float near = 1f;
                float pKeep = baseKeep * cluster * 0.45f;   // about half the old density
                if (keepRoll >= pKeep) continue;
                bool tended = keepRoll < pKeep * 0.1f;   // survives into Rebuilt
                List<GameObject> pool; float lo, hi, cap;
                switch (s.kind)
                {
                    case "lot_edge": case "edge_grass":
                        pool = rnd.Next(2) == 0 && moss.Count > 0 ? moss : (grass.Count > 0 ? grass : moss); lo = 0.7f; hi = 1.2f; cap = 0.42f; nEdge++; break;
                    case "missing_sett":
                        pool = rnd.Next(3) > 0 && weeds.Count > 0 ? weeds : (moss.Count > 0 ? moss : weeds); lo = 0.8f; hi = 1.2f; cap = 0.25f; nMissing++; break;
                    case "joint":
                        pool = rnd.Next(2) == 0 && moss.Count > 0 ? moss : (weeds.Count > 0 ? weeds : moss); lo = 0.5f; hi = 0.8f; cap = 0.2f; nJoint++; break;
                    default: continue;
                }
                if (pool.Count == 0) continue;
                GameObject model = pool[rnd.Next(pool.Count)];
                var w = (GameObject)PrefabUtility.InstantiatePrefab(model, tended ? group : ruinedGroup);
                w.name = s.kind + "_" + n;
                foreach (Renderer rd in w.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                    for (int k = 0; k < mats.Length; k++) mats[k] = mat;
                    rd.sharedMaterials = mats; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                float scale = lo + (float)rnd.NextDouble() * (hi - lo);
                scale = Mathf.Min(scale, cap / heights[model]);
                w.transform.localPosition = new Vector3(s.p[0], HollowmereB3Ground.BedLift + CobbleSpawner.SampleDrop(drop, origin, step, nx, nz, s.p[0], s.p[2]), s.p[2]);   // on the bed (the json's y was for sett tops)
                w.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                w.transform.localScale = Vector3.one * scale;
                n++;
            }
            Debug.Log("SC HOLLOWMERE square edges: " + n + " tufts on " + slots.Length + " slots (" + nEdge + " edge, " + nMissing + " missing sett, " + nJoint + " joint)");
        }

        // Copies each bed mesh with its vertices moved up by the drape (vertex -> holder space -> + correction -> back), saved as <folder>/SquareBed_Draped[_n].asset
        // (rewritten only when the vertices changed), and puts the copy on the instance's MeshFilter.
        static void DrapeBed(GameObject bedGo, Transform holder, string folder, float[] drop, Vector2 origin, float step, int nx, int nz)
        {
            int n = 0;
            foreach (MeshFilter mf in bedGo.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh src = mf.sharedMesh;
                if (src == null) continue;
                Matrix4x4 m = holder.worldToLocalMatrix * mf.transform.localToWorldMatrix, inv = m.inverse;
                Vector3[] v = src.vertices;
                // B3: no setts sit on the bed any more and the paving slab (top -0.02) must not show through it: the bed's lowest vertex goes to BedLift above the draped ground.
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < v.Length; i++) { float y = m.MultiplyPoint3x4(v[i]).y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(v[i]);
                    p.y += SleepingCalamity.Presentation.CobbleSpawner.SampleDrop(drop, origin, step, nx, nz, p.x, p.z) + HollowmereB3Ground.BedLift - minY;
                    uv[i] = new Vector2((p.x + holder.position.x) / 2f, (p.z + holder.position.z) / 2f);   // world metres / 2: the T01 tile is 2 m
                    v[i] = inv.MultiplyPoint3x4(p);
                }
                if (n == 0) Debug.Log("SC HOLLOWMERE square bed: source y " + minY.ToString("F3") + ".." + maxY.ToString("F3") + " (holder space), lifted to " + HollowmereB3Ground.BedLift + " above the draped ground (slab top -0.02)");
                string path = folder + "/SquareBed_Draped" + (n == 0 ? "" : "_" + n) + ".asset"; n++;
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh result;
                if (existing != null && existing.vertexCount == v.Length && existing.vertices.SequenceEqual(v) && existing.uv.SequenceEqual(uv) && existing.subMeshCount == src.subMeshCount) result = existing;
                else
                {
                    var copy = Object.Instantiate(src); copy.name = "SquareBed_Draped"; copy.vertices = v; copy.uv = uv;
                    copy.RecalculateBounds();
                    if (src.normals != null && src.normals.Length > 0) copy.RecalculateNormals();   // the drape tilts the bed a little: keep the lighting honest
                    copy.RecalculateTangents();   // the UVs were rewritten: the FBX's tangents no longer match
                    if (existing != null) { EditorUtility.CopySerialized(copy, existing); Object.DestroyImmediate(copy); result = existing; EditorUtility.SetDirty(result); }
                    else { AssetDatabase.CreateAsset(copy, path); result = copy; }
                    AssetDatabase.SaveAssets();
                }
                mf.sharedMesh = result;
            }
        }

        static void PlaceDrains(Transform holder, PDrain[] drains, TownLayout t, float shelf)
        {
            if (drains == null) return;
            Transform group = Child(holder, "Drains");
            string folder = BeautyCornerArt.FindFolder(SquareBatch, "Drain_A");
            BeautyCornerArt.Found found = folder != null ? BeautyCornerArt.FindIn(folder) : null;
            GameObject fbx = null; Material mat = null;
            if (found != null) fbx = BeautyCornerArt.Prepare(found, "Drain_A", false, out mat);
            for (int i = 0; i < drains.Length; i++)
            {
                PDrain d = drains[i];
                if (d.p == null || d.p.Length < 3) continue;
                var pos = new Vector3(d.p[0], HollowmereB3Ground.BedLift + HollowmereLayout.Ground(t, d.p[0], d.p[2]) - shelf, d.p[2]);   // on the draped bed (B3: flush with the cobble bed, no setts)
                if (fbx != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, group); go.name = "Drain_" + i;
                    go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, d.yaw, 0f));   // origin at the top centre, flush with the setts
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int k = 0; k < mats.Length; k++) mats[k] = mat;
                        r.sharedMaterials = mats;
                    }
                }
                else
                {
                    GameObject b = LookTestBuilder.Box(group, "PLACEHOLDER_Drain_" + i, pos + new Vector3(0f, -0.04f, 0f), new Vector3(0.5f, 0.08f, 0.5f), BeautyCornerBuilder.PlaceholderMat("dark"), new Vector3(0f, d.yaw, 0f), 1f);
                    b.name = "PLACEHOLDER_Drain_" + i;
                }
            }
            Debug.Log("SC HOLLOWMERE square drains: " + drains.Length + (fbx != null ? " with Drain_A art" : " as greybox slabs"));
        }

        // ---------------------------------------------------------------------------------------------------- dressing
        static void BuildDressing(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            Transform both = Child(set, "SquareDressing"), ru = Child(ruined, "Dressing"), rb = Child(rebuilt, "Dressing");
            var plants = new PlantKit();
            int placed = 0, art = 0;
            foreach (DecoRow r in HollowmereDressing.Rows)
            {
                Transform parent = r.Preset == DecoPreset.Both ? both : r.Preset == DecoPreset.Ruined ? ru : rb;
                if (PlaceDeco(parent, r, t, plants)) art++;
                placed++;
            }
            int spans = 0;
            Dictionary<string, Mesh> bunting = LoadBunting(out Material buntingMat);
            foreach (BuntingSpan b in HollowmereDressing.Bunting)
            {
                BuildBunting(b.Preset == DecoPreset.Both ? both : b.Preset == DecoPreset.Ruined ? ru : rb, b, t, bunting, buntingMat);
                spans++;
            }
            Debug.Log("SC HOLLOWMERE square dressing: " + placed + " decorations (" + art + " with real art), " + spans + " bunting spans (" + (bunting != null ? "Square/Bunting art" : "greybox") + ")");
        }

        // Returns true when the real art was used.
        static bool PlaceDeco(Transform parent, DecoRow r, TownLayout t, PlantKit plants)
        {
            var row = new BeautyCornerTable.Row
            {
                Brief = "Square dressing", Id = "Deco_" + r.Id, Batch = r.Batch, Names = new[] { r.Asset },
                Instances = new[] { new BeautyCornerTable.Instance(0f, 0f, 0f, 0f, 1f, r.Asset) },
                Placeholder = Greybox(r),
            };
            BeautyCornerBuilder.PlaceRow(parent, row);
            Transform root = parent.GetChild(parent.childCount - 1);
            Transform body = root.GetChild(0);
            bool real = !body.name.StartsWith("PLACEHOLDER_");
            float ground = HollowmereLayout.Ground(t, r.X, r.Z);
            root.SetPositionAndRotation(new Vector3(r.X, ground, r.Z), Quaternion.Euler(0f, r.Yaw, 0f));
            if (r.Tilted) SitOnGround(root, body, r, ground);

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, r.Height * 0.5f, 0f); box.size = new Vector3(r.SizeX, r.Height, r.SizeZ);

            float soil = r.Kind == DecoKind.Pot ? PotSoilHeight : SoilHeight(r.Asset);
            if (r.Kind == DecoKind.Pot && real)
            {
                body.localScale = Vector3.one * PotScale;   // the art pot is 0.30 m: storybook scale wants it bigger, the footprint stays 0.5
                soil = SoilHeight(r.Asset);
                if (soil == DefaultSoilHeight) soil = PotSoilHeight;
                soil *= PotScale;
            }
            if (real && HollowmereClutter.VisibleBounds(root, out Bounds vb))   // fit the box to the visible mesh (never beyond the footprint): the footprint box stood 0.3 m+ off pots, lamps and banners
            {
                Vector3 lo = Vector3.Max(vb.min, new Vector3(-r.SizeX * 0.5f, 0f, -r.SizeZ * 0.5f)), hi = Vector3.Min(vb.max, new Vector3(r.SizeX * 0.5f, vb.max.y, r.SizeZ * 0.5f));
                if (hi.x > lo.x && hi.y > lo.y && hi.z > lo.z) { box.center = (lo + hi) * 0.5f; box.size = hi - lo; }
            }
            if (r.Plants > 0) plants.Stand(root, r, soil);
            if (r.Light) HangLantern(root, r);
            return real;
        }

        // A toppled prop: tilt the art about its base, then drop it onto the ground and centre it on the footprint.
        static void SitOnGround(Transform root, Transform body, DecoRow r, float ground)
        {
            body.localRotation = Quaternion.Euler(r.TiltX, 0f, r.TiltZ);
            Bounds b = default; bool any = false;
            foreach (Renderer rd in body.GetComponentsInChildren<Renderer>(true))
            {
                if (rd.bounds.size == Vector3.zero) continue;
                if (!any) { b = rd.bounds; any = true; } else b.Encapsulate(rd.bounds);
            }
            if (!any) return;
            body.position += new Vector3(root.position.x - b.center.x, ground - b.min.y, root.position.z - b.center.z);
        }

        static BeautyCornerTable.Part[] Greybox(DecoRow r)
        {
            float sx = r.SizeX, sz = r.SizeZ, h = r.Height;
            BeautyCornerTable.Part B(float x, float y, float z, float w, float hh, float d, string m) => new BeautyCornerTable.Part(BeautyCornerTable.PartShape.Box, new Vector3(x, y, z), new Vector3(w, hh, d), m);
            BeautyCornerTable.Part C(float x, float y, float z, float dia, float hh, string m) => new BeautyCornerTable.Part(BeautyCornerTable.PartShape.Cylinder, new Vector3(x, y, z), new Vector3(dia, hh, dia), m);
            string wood = r.Broken ? "dark" : "oak_timber";
            switch (r.Kind)
            {
                case DecoKind.Bench:
                    return new[] { B(0f, h * 0.5f, 0f, sx, 0.08f, sz * 0.8f, wood), B(0f, h * 0.8f, -sz * 0.45f, sx, h * 0.4f, 0.06f, wood), B(-sx * 0.4f, h * 0.25f, 0f, 0.08f, h * 0.5f, sz * 0.8f, "iron"), B(sx * 0.4f, h * 0.25f, 0f, 0.08f, h * 0.5f, sz * 0.8f, "iron") };
                case DecoKind.Planter:
                    return new[] { B(0f, h * 0.5f, 0f, sx, h, sz, wood), B(0f, h + 0.01f, 0f, sx - 0.1f, 0.03f, sz - 0.1f, "dark") };
                case DecoKind.Pot:
                    return new[] { C(0f, h * 0.5f, 0f, sx, h, r.Broken ? "dark" : "roof_tile") };
                case DecoKind.LanternPost:
                    return new[] { B(0f, 0.15f, 0f, sx, 0.3f, sz, "sandstone"), C(0f, h * 0.5f, 0f, 0.12f, h, "iron"), B(0f, h - 0.25f, 0.3f, 0.06f, 0.06f, 0.6f, "iron") };
                case DecoKind.BannerPole:
                    return new[] { C(0f, 2.25f, 0f, 0.12f, 4.5f, "dark"), B(0.4f, 3.15f, 0f, 0.7f, 2.2f, 0.03f, "cloth") };
                default:
                    if (r.Asset.StartsWith("Barrel")) return new[] { C(0f, h * 0.5f, 0f, sx, h, "barrel_oak") };
                    if (r.Asset.StartsWith("Basket") || r.Asset.StartsWith("Bucket") || r.Asset.StartsWith("Jug")) return new[] { C(0f, h * 0.5f, 0f, Mathf.Min(sx, sz), h, "oak_timber") };
                    if (r.Asset.StartsWith("Sack")) return new[] { B(0f, h * 0.5f, 0f, sx, h, sz, "canvas") };
                    return new[] { B(0f, h * 0.5f, 0f, sx, h, sz, "painted_wood") };
            }
        }

        // ---- planters and pots ----
        static float SoilHeight(string asset)
        {
            string folder = BeautyCornerArt.FindFolder(SquareBatch, asset);
            string src = folder != null ? BuilderUtil.Abs(folder + "/SOURCES.md") : null;
            if (src != null && File.Exists(src))
            {
                string text = File.ReadAllText(src);
                Match m = Regex.Match(text, @"soil top height:\s*([0-9]+(?:\.[0-9]+)?)\s*m", RegexOptions.IgnoreCase);
                if (!m.Success) m = Regex.Match(text, @"soil[^0-9\r\n]{0,40}([0-9]+(?:\.[0-9]+)?)\s*m", RegexOptions.IgnoreCase);
                if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0.05f && v < 2f) return v;
            }
            return DefaultSoilHeight;
        }

        sealed class PlantKit
        {
            readonly List<GameObject> _models = new List<GameObject>();
            readonly List<GameObject> _ivy = new List<GameObject>();
            Material _mat, _ivyMat; bool _loaded;

            void Load()
            {
                _loaded = true;
                foreach (string name in new[] { "Grass_A", "Grass_B", "Grass_C", "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E" })
                {
                    string folder = BeautyCornerArt.FindFolder("B9", name);
                    string fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                    GameObject go = fbx != null ? BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx)) : null;
                    if (go == null) continue;
                    _models.Add(go);
                    if (_mat == null) _mat = BeautyCornerArt.CrackWeedsMaterial();   // the B9 folders share one atlas
                }
                foreach (string name in new[] { "Ivy_Patch_A", "Ivy_Patch_B" })
                {
                    string folder = BeautyCornerArt.FindFolder("B9", name);
                    string fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                    GameObject go = fbx != null ? BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx)) : null;
                    if (go == null) continue;
                    _ivy.Add(go);
                    if (_ivyMat == null) _ivyMat = BeautyCornerArt.MaterialFromSet(folder, "T_B9_Foliage", "IvyPatch", foliage: true);
                    if (_ivyMat != null) { _ivyMat.SetColor("_BaseColor", new Color(0.62f, 0.68f, 0.50f, 1f)); EditorUtility.SetDirty(_ivyMat); }   // A4 palette
                }
            }

            // Stands r.Plants instances on the soil top of the planter or pot at root (its local frame), seeded by the row id.
            public void Stand(Transform root, DecoRow r, float soilY)
            {
                if (!_loaded) Load();
                if (_models.Count == 0 || _mat == null) return;
                var rnd = new System.Random(r.Id.Aggregate(17, (a, c) => a * 31 + c));
                // Fuller mixed planting: a lived-in planter carries 3-5 clumps (grass, herb and weed shapes) and ivy trailing down its front face.
                int count = r.Kind == DecoKind.Planter && !r.Broken ? 3 + rnd.Next(3) : r.Plants;
                if (r.Kind == DecoKind.Planter && !r.Broken && _ivy.Count > 0 && _ivyMat != null)
                    for (int k = 0; k < 2; k++)
                    {
                        var iv = (GameObject)PrefabUtility.InstantiatePrefab(_ivy[(k + rnd.Next(2)) % _ivy.Count], root);
                        iv.name = "Ivy_" + k;
                        foreach (Renderer rd in iv.GetComponentsInChildren<Renderer>(true))
                        {
                            var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                            for (int q = 0; q < mats.Length; q++) mats[q] = _ivyMat;
                            rd.sharedMaterials = mats; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                        float sc = 0.32f + (float)rnd.NextDouble() * 0.12f;   // the patch leaves face local +Z: laid on the front face from the rim down
                        iv.transform.localPosition = new Vector3((k == 0 ? -0.25f : 0.2f) * r.SizeX + ((float)rnd.NextDouble() - 0.5f) * 0.1f, Mathf.Max(0.02f, soilY - 0.42f * sc * 1.4f - 0.02f), r.SizeZ * 0.5f + 0.015f);
                        iv.transform.localRotation = Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 14f, 0f);
                        iv.transform.localScale = Vector3.one * sc;
                    }
                for (int i = 0; i < count; i++)
                {
                    GameObject model = _models[rnd.Next(_models.Count)];
                    var w = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                    w.name = "Plant_" + i;
                    foreach (Renderer rd in w.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                        for (int k = 0; k < mats.Length; k++) mats[k] = _mat;
                        rd.sharedMaterials = mats; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    float scale = 0.55f + (float)rnd.NextDouble() * 0.25f;
                    w.transform.localPosition = new Vector3(((float)rnd.NextDouble() - 0.5f) * r.SizeX * 0.6f, soilY, ((float)rnd.NextDouble() - 0.5f) * r.SizeZ * 0.5f);
                    w.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    w.transform.localScale = Vector3.one * scale;
                    float h = 0f; bool any = false; Bounds b = default;
                    foreach (Renderer rd in w.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = rd.bounds; any = true; } else b.Encapsulate(rd.bounds); }
                    if (any) h = b.size.y;
                    if (h > 0.4f) w.transform.localScale = Vector3.one * (scale * 0.4f / h);   // a planter's greenery stays under 40 cm
                }
            }
        }

        // ---- lanterns ----
        static void HangLantern(Transform post, DecoRow r)
        {
            Vector3 hook = DefaultHook;
            string folder = BeautyCornerArt.FindFolder(SquareBatch, r.Asset);
            string src = folder != null ? BuilderUtil.Abs(folder + "/SOURCES.md") : null;
            if (src != null && File.Exists(src))
            {
                Match m = Regex.Match(File.ReadAllText(src), @"hook[^\r\n(]*\(\s*(-?[0-9.]+)\s*,\s*(-?[0-9.]+)\s*,\s*(-?[0-9.]+)\s*\)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    hook = new Vector3(float.Parse(m.Groups[1].Value, ci), float.Parse(m.Groups[2].Value, ci), float.Parse(m.Groups[3].Value, ci));
                }
            }
            // The same B8 art path as the corner's StallLantern row (same row id, so the same material and textures, none per post); the art's origin is its base, so it hangs 0.34 m below the hook.
            var row = new BeautyCornerTable.Row
            {
                Brief = "Square dressing", Id = "StallLantern", Batch = "B8", Names = new[] { "stall_lantern", "lantern", "hanging_lantern" },
                Instances = new[] { new BeautyCornerTable.Instance(hook.x, hook.y - 0.34f, hook.z) },
                Placeholder = new[] { new BeautyCornerTable.Part(BeautyCornerTable.PartShape.Box, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.34f, 0.2f), "warm") },
            };
            BeautyCornerBuilder.PlaceRow(post, row);
            // A warm pool under the lit lantern: about 2700 K, 6 m, no shadows. Named LampLight; BeautyCornerBuilder.ApplyPreset switches it on for LowSun only.
            Light l = LookTestBuilder.AddLight(post, "LampLight", LightType.Point, hook + new Vector3(0f, -0.2f, 0f), Quaternion.identity, new Color(1.0f, 0.69f, 0.40f), 4f, 6f, LightShadows.None);
            l.enabled = false;
        }

        // ---------------------------------------------------------------------------------------------------- bunting
        static Dictionary<string, Mesh> LoadBunting(out Material mat)
        {
            mat = null;
            string folder = BeautyCornerArt.FindFolder(SquareBatch, "Bunting");
            BeautyCornerArt.Found found = folder != null ? BeautyCornerArt.FindIn(folder) : null;
            if (found == null) return null;
            GameObject fbx = BeautyCornerArt.Prepare(found, "Bunting", false, out mat);
            if (fbx == null) return null;
            var meshes = new Dictionary<string, Mesh>(System.StringComparer.OrdinalIgnoreCase);
            foreach (MeshFilter mf in fbx.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) meshes[mf.name] = mf.sharedMesh;
            if (mat != null)   // a slight fade for the one shared bunting material
            {
                mat.SetColor("_BaseColor", new Color(0.92f, 0.90f, 0.86f, 1f)); mat.SetColor("_Color", new Color(0.92f, 0.90f, 0.86f, 1f));
                EditorUtility.SetDirty(mat);
            }
            if (!meshes.ContainsKey("Rope_1m") || !meshes.ContainsKey("Pennant_0")) { Debug.LogWarning("SC HOLLOWMERE bunting: " + found.Fbx + " lacks Rope_1m or Pennant_0 (has " + string.Join(", ", meshes.Keys) + "), using the greybox"); return null; }
            return meshes;
        }

        static void BuildBunting(Transform parent, BuntingSpan b, TownLayout t, Dictionary<string, Mesh> meshes, Material mat)
        {
            var root = new GameObject("Deco_" + b.Id).transform; root.SetParent(parent, false);
            var a = new Vector3(b.AX, HollowmereLayout.Ground(t, b.AX, b.AZ) + b.AH, b.AZ);
            var e = new Vector3(b.BX, HollowmereLayout.Ground(t, b.BX, b.BZ) + b.BH, b.BZ);
            // Parabola between the ends, sagging b.Sag at the middle.
            Vector3 P(float s) { Vector3 p = Vector3.Lerp(a, e, s); p.y -= 4f * b.Sag * s * (1f - s); return p; }

            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, e) / RopeSegment));
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = P(i / (float)n);
            float[] arc = new float[n + 1];
            for (int i = 1; i <= n; i++) arc[i] = arc[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            float total = arc[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 d = pts[i + 1] - pts[i]; float len = d.magnitude;
                if (meshes != null) Piece(root, "Rope_" + i, meshes["Rope_1m"], mat, pts[i], Quaternion.FromToRotation(Vector3.right, d), new Vector3(len, 1f, 1f));
                else
                {
                    var go = LookTestBuilder.Prim(root, "PLACEHOLDER_Rope_" + i, PrimitiveType.Cylinder, (pts[i] + pts[i + 1]) * 0.5f, new Vector3(0.025f, len * 0.5f, 0.025f), BeautyCornerBuilder.PlaceholderMat("dark"));
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d);
                }
            }

            // Pennants every 0.33 m along the rope, the variant cycle reshuffled every round with a seed from the span id.
            var rnd = new System.Random(b.Id.Aggregate(7, (x, c) => x * 31 + c));
            var order = new List<int>(); int k = 0;
            string[] colours = { "warm", "green", "cloth", "roof_tile" };
            for (float s = PennantSpacing * 0.5f; s < total - 0.1f; s += PennantSpacing, k++)
            {
                if (order.Count == 0) order.AddRange(new[] { 0, 1, 2, 3 }.OrderBy(_ => rnd.Next()));
                int v = order[0]; order.RemoveAt(0);
                string mesh = "Pennant_" + v;
                if (b.Torn)
                {
                    if (meshes != null && meshes.ContainsKey("Pennant_T" + (v % 2))) mesh = "Pennant_T" + (v % 2);
                    else if (k % 2 == 1) continue;   // no torn art: every other pennant is gone
                }
                int seg = 1; while (seg < n && arc[seg] < s) seg++;
                float f = Mathf.InverseLerp(arc[seg - 1], arc[seg], s);
                Vector3 pos = Vector3.Lerp(pts[seg - 1], pts[seg], f), tan = (pts[seg] - pts[seg - 1]).normalized;
                Vector3 up = (Vector3.up - tan * Vector3.Dot(Vector3.up, tan)).normalized;
                Quaternion rot = Quaternion.LookRotation(Vector3.Cross(tan, up), up);   // local +X along the rope, hanging -Y
                if (meshes != null && meshes.TryGetValue(mesh, out Mesh m)) Piece(root, mesh + "_" + k, m, mat, pos, rot, Vector3.one);
                else
                {
                    GameObject q = LookTestBuilder.Box(root, "PLACEHOLDER_Pennant_" + k, pos + rot * new Vector3(0f, -0.11f, 0f), new Vector3(0.2f, 0.22f, 0.01f), BeautyCornerBuilder.PlaceholderMat(colours[v]), default, 1f);
                    q.transform.rotation = rot;
                }
            }
        }

        static void Piece(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
