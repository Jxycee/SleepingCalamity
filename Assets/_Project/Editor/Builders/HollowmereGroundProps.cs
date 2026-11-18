// T13a, props half of HollowmereGround: the street clutter's lone props are grouped by use (companions beside them) or removed, three heaps of market goods stand by every stall, and
// every prop that stays gets contact: a dirt ring (a flat quad, not a projector), a plant at its base and chips of what it is made of. Called by HollowmereBuilder after the roadside
// (every collider exists) and before HollowmereCombine / HollowmereLods, so the new small pieces are merged and Mesh-LOD'd with the rest. Preset_Ruined only, editor time.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereGround
    {
        // What goes with what (street clutter Kinds): a prop with nothing within LoneRadius takes companions from its row, or goes.
        static readonly Dictionary<string, string[]> Uses = new Dictionary<string, string[]>
        {
            ["Barrel"] = new[] { "Crate", "Sack", "Basket", "Barrel" }, ["Sack"] = new[] { "Sack", "Basket", "Jug", "Crate" }, ["Jug"] = new[] { "Pot", "Jug", "Basket", "Stool" },
            ["Pot"] = new[] { "Jug", "Pot", "Basket", "Stool" }, ["Crate"] = new[] { "Sack", "Barrel", "Crate", "Basket" }, ["Planks"] = new[] { "Logs", "Rubble", "Planks", "Tool" },
            ["Rubble"] = new[] { "Rubble", "Tiles", "Planks", "Logs" }, ["Tiles"] = new[] { "Rubble", "Tiles", "Planks", "Rubble" }, ["Logs"] = new[] { "Logs", "Tool", "Planks", "Rubble" },
            ["Tool"] = new[] { "Logs", "Planks", "Stool", "Tool" }, ["Stool"] = new[] { "Jug", "Basket", "Pot", "Sack" }, ["Wheel"] = new[] { "Planks", "Crate", "Sack", "Rubble" },
            ["Fabric"] = new[] { "Sack", "Basket", "Fabric", "Jug" }, ["Basket"] = new[] { "Sack", "Jug", "Basket", "Crate" }, ["Chest"] = new[] { "Sack", "Jug", "Crate", "Basket" },
            ["FlowerBox"] = new[] { "Pot", "Jug", "Pot", "Basket" },
        };
        static readonly string[] Plants = { "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E", "Grass_A", "Grass_B", "Grass_C" };
        const float LoneRadius = 1.9f;
        static Material aoMat;
        static readonly List<(Vector3 P, float W, float Yaw)> rings = new List<(Vector3, float, float)>();
        static int nRemoved, nGrouped, nCompanions, nStallGoods, nRings, nTufts, nChips;

        static Vector2 CentreOf(GameObject go) { Bounds b = HollowmereStreetClutter.RenderBounds(go); return new Vector2(b.center.x, b.center.z); }

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            nRemoved = nGrouped = nCompanions = nStallGoods = nRings = nTufts = nChips = 0; rings.Clear();
            Physics.SyncTransforms();
            Transform streets = ruined.Find("Streets");
            if (streets == null) { Debug.LogWarning("SC HOLLOWMERE ground: no Streets root, props not grouped"); return; }
            aoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/Hollowmere/M_HM_Surf_ContactAO.mat");
            Transform grp = HollowmereStreets.Group(streets, "Grounding");
            var cluster = new List<GameObject>();
            Transform clutter = streets.Find("Clutter");
            if (clutter != null) foreach (Transform zone in clutter) foreach (Transform c in zone) cluster.Add(c.gameObject);
            var fixedPts = new List<Vector2>();   // story and landmark pieces count as company
            foreach (string gname in new[] { "Story", "Landmarks" })
            {
                Transform g = streets.Find(gname); if (g == null) continue;
                foreach (Transform sub in g) foreach (Transform c in sub) fixedPts.Add(CentreOf(c.gameObject));
            }
            foreach (Landmark l in t.Landmarks) fixedPts.Add(new Vector2(l.Footprint.Centre.X, l.Footprint.Centre.Z));
            var centres = cluster.Select(CentreOf).ToList();
            var alive = new List<GameObject>(cluster);
            for (int i = 0; i < cluster.Count; i++)
            {
                GameObject go = cluster[i]; Vector2 c = centres[i];
                bool lone = true;
                for (int j = 0; j < cluster.Count && lone; j++) if (j != i && cluster[j] != null && Vector2.Distance(c, centres[j]) < LoneRadius) lone = false;
                for (int j = 0; j < fixedPts.Count && lone; j++) if (Vector2.Distance(c, fixedPts[j]) < LoneRadius + 0.6f) lone = false;
                if (!lone) continue;
                string kn = go.name.Split('_')[0];
                if (!Uses.TryGetValue(kn, out string[] companions)) continue;
                int seed = HollowmereStreets.StableHash(go.name), made = 0;
                Transform parent = go.transform.parent;
                for (int k = 0; k < 10 && made < 2; k++)
                {
                    float a = (k * 0.7f + HollowmereStreets.H(seed, k, 301)) * Mathf.PI * 0.62f, r = 0.65f + 0.5f * HollowmereStreets.H(seed, k, 302);
                    Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    string ck = companions[(k + made) % companions.Length];
                    GameObject comp = HollowmereStreetClutter.PlaceKind(parent, ck, ck + "_g" + (seed % 1000) + "_" + k, q, seed, k + 50);
                    if (comp == null) continue;
                    alive.Add(comp); made++; nCompanions++;
                }
                if (made >= 1) nGrouped++;
                else { alive.Remove(go); Object.DestroyImmediate(go); cluster[i] = null; nRemoved++; }
            }
            // market goods at the three stalls: a heap on each side (crates, sacks, a barrel, a basket, jugs), the use of a stall
            string[][] heaps = { new[] { "Crate", "Sack", "Sack", "Basket" }, new[] { "Barrel", "Sack", "Jug", "Jug" } };
            foreach (Landmark l in t.Landmarks)
            {
                if (l.Kind != LandmarkKind.Stall) continue;
                int sk = HollowmereStreets.StableHash(l.Id);
                for (int side = 0; side < 2; side++)
                {
                    float sg = side == 0 ? 1f : -1f; int made = 0;
                    for (int attempt = 0; attempt < 6 && made == 0; attempt++)
                    {
                        float lz = (attempt - 2) * 0.35f, lx = sg * (l.Footprint.HalfX + 0.85f + 0.15f * attempt);
                        TownPoint wp = l.Footprint.ToWorld(lx, lz); var anchor = new Vector2(wp.X, wp.Z);
                        for (int k = 0; k < heaps[side].Length; k++)
                        {
                            float a = k * 1.57f + HollowmereStreets.H(sk, side * 10 + k, 311) * 1.2f, rr = k == 0 ? 0f : 0.5f + 0.35f * HollowmereStreets.H(sk, side * 10 + k, 312);
                            GameObject comp = HollowmereStreetClutter.PlaceKind(grp, heaps[side][k], "StallGoods_" + l.Id + "_" + side + "_" + k, anchor + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, sk, side * 10 + k);
                            if (comp != null) { alive.Add(comp); made++; nStallGoods++; }
                        }
                    }
                }
            }
            // the square's dressing rows: the "Drop_<group>_*" pieces sat 1.5-2 m apart in the open middle and read as singles; each group is pulled together (to 55% of its spread, its
            // ContactAO quads with it) and the two lone Prop_Ruin pieces (a sack and a bucket in open cobbles) go
            var decoByName = new Dictionary<string, Transform>();
            foreach (Transform tr in ruined.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("Deco_Drop_") || tr.name.StartsWith("AO_Deco_Drop_") || tr.name == "Deco_Prop_Ruin_Sack" || tr.name == "Deco_Prop_Ruin_Bucket" || tr.name == "AO_Deco_Prop_Ruin_Sack" || tr.name == "AO_Deco_Prop_Ruin_Bucket") decoByName[tr.name] = tr;
            foreach (string gone in new[] { "Deco_Prop_Ruin_Sack", "Deco_Prop_Ruin_Bucket", "AO_Deco_Prop_Ruin_Sack", "AO_Deco_Prop_Ruin_Bucket" })
                if (decoByName.TryGetValue(gone, out Transform gt)) { Object.DestroyImmediate(gt.gameObject); decoByName.Remove(gone); nRemoved++; }
            foreach (string key in decoByName.Keys.Where(k => k.Contains("Drop_") && k.Contains("Sack")).ToList())   // the slumped sack pieces read as giant gourds in the open middle (Postcard): the crates and baskets of each group stay
            { Object.DestroyImmediate(decoByName[key].gameObject); decoByName.Remove(key); nRemoved++; }
            var dropGroups = decoByName.Keys.Where(k => k.StartsWith("Deco_Drop_")).GroupBy(k => k.Split('_')[2]).ToList();
            foreach (var grpKeys in dropGroups)
            {
                var members = grpKeys.Select(k => decoByName[k]).ToList();
                Vector3 centroid = Vector3.zero; foreach (Transform m in members) centroid += m.position; centroid /= members.Count;
                foreach (Transform m in members)
                {
                    Vector3 d = (m.position - centroid) * -0.45f; d.y = 0f;
                    m.position += d;
                    if (decoByName.TryGetValue("AO_" + m.name, out Transform ao)) ao.position += d;
                    string kn = m.name.Contains("Crate") ? "Crate" : m.name.Contains("Barrel") ? "Barrel" : m.name.Contains("Sack") ? "Sack" : "Basket";
                    Physics.SyncTransforms();
                    Contact(grp, m.gameObject, kn, HollowmereStreets.StableHash(m.name), 9000 + nCompanions++, false);
                }
            }
            // contact on everything that stays: a dirt ring, a plant at the base, chips of what it is made of
            int gi = 0;
            foreach (GameObject go in alive) { if (go == null) continue; Contact(grp, go, go.name.Split('_')[0], HollowmereStreets.StableHash(go.name), gi++); }
            BakeRings(grp);
            Debug.Log("SC HOLLOWMERE ground (T13a): street props " + cluster.Count + ", lone props grouped " + nGrouped + " (companions " + nCompanions + "), removed " + nRemoved + ", stall goods " + nStallGoods
                + ", contact rings " + nRings + ", tufts " + nTufts + ", chips " + nChips);
        }

        // The contact rings of a 12 m cell are one mesh (a quad each): a ring object is 2.5 KB of scene text and the alpha-blended material keeps HollowmereCombine from merging them.
        static void BakeRings(Transform grp)
        {
            foreach (var cell in rings.GroupBy(r => (Mathf.FloorToInt(r.P.x / 12f), Mathf.FloorToInt(r.P.z / 12f))).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
            {
                var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var ns = new List<Vector3>(); var tris = new List<int>();
                var origin = new Vector3((cell.Key.Item1 + 0.5f) * 12f, cell.Average(r => r.P.y), (cell.Key.Item2 + 0.5f) * 12f);
                foreach (var r in cell)
                {
                    Quaternion q = Quaternion.Euler(0f, r.Yaw, 0f); float h = r.W * 0.5f; int i0 = vs.Count;
                    foreach (var c in new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) }) { vs.Add(r.P - origin + q * c); ns.Add(Vector3.up); }
                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
                var mesh = new Mesh { name = "Rings_" + cell.Key.Item1 + "_" + cell.Key.Item2 };
                mesh.SetVertices(vs); mesh.SetNormals(ns); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
                var go = new GameObject(mesh.name); go.transform.SetParent(grp, false); go.transform.position = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = aoMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            }
        }

        static void Contact(Transform grp, GameObject prop, string kn, int seed, int idx, bool ring = true)
        {
            Bounds b = HollowmereStreetClutter.RenderBounds(prop);
            float ext = Mathf.Max(b.size.x, b.size.z);
            if (ext < 0.2f || ext > 4f) return;
            if (ring && aoMat != null && ext >= 0.3f)
            {
                float w = Mathf.Min(2.4f, ext * 1.45f + 0.25f);
                rings.Add((new Vector3(b.center.x, b.min.y + 0.034f, b.center.z), w, HollowmereStreets.H(seed, 1, 321) * 360f));
                nRings++;
            }
            int plants = (kn == "Rubble" || kn == "Tiles") ? 1 : ext > 1.2f ? 2 : 1;
            for (int i = 0; i < plants; i++)
            {
                if (HollowmereStreets.H(seed, i, 322) < 0.5f) continue;
                float a = (HollowmereStreets.H(seed, i, 323) + i * 0.5f) * Mathf.PI * 2f, rr = ext * 0.5f + 0.05f;
                var pos = new Vector3(b.center.x + Mathf.Cos(a) * rr, b.min.y - 0.02f, b.center.z + Mathf.Sin(a) * rr);
                if (HollowmereFoliage.Tuft(grp, Plants[(int)(HollowmereStreets.H(seed, i, 324) * Plants.Length) % Plants.Length], pos, HollowmereStreets.H(seed, i, 325) * 360f, Mathf.Lerp(0.8f, 1.35f, HollowmereStreets.H(seed, i, 326))) != null) nTufts++;
            }
            if ((kn == "Crate" || kn == "Barrel" || kn == "Planks" || kn == "Logs" || kn == "Wheel" || kn == "Chest") && HollowmereStreets.H(seed, 4, 327) < 0.3f)
            {
                GameObject chip = HollowmereStreetClutter.Make(HollowmereStreets.H(seed, 5, 328) < 0.5f ? "A:T05b/T05b.prefab" : "A:T05c/T05c.prefab", grp);
                if (chip != null)
                {
                    HollowmereStreetClutter.Strip(chip);
                    float a = HollowmereStreets.H(seed, 6, 329) * Mathf.PI * 2f, rr = ext * 0.5f + 0.22f;
                    HollowmereStreetClutter.Fit(chip, "Chip_" + idx, new Vector2(b.center.x + Mathf.Cos(a) * rr, b.center.z + Mathf.Sin(a) * rr), b.min.y, 0.14f + 0.12f * HollowmereStreets.H(seed, 7, 330), HollowmereStreets.H(seed, 8, 331) * 360f, 6f, 4f, 0.3f, out _);
                    nChips++;
                }
            }
        }
    }
}
