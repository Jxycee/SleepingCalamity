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
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereSquare
    {
        const string SquareBatch = "Square";
        const float DefaultSoilHeight = 0.45f, PotSoilHeight = 0.4f, DriftWarn = 0.02f, PennantSpacing = 0.33f, RopeSegment = 0.5f;
        static readonly Vector3 DefaultHook = new Vector3(0f, 2.85f, 0.6f);   // lantern post hook point (Unity terms, post-local) when SOURCES.md does not say

        [System.Serializable] sealed class PStone { public string v; public float[] p; }
        [System.Serializable] sealed class PDrain { public float[] p; public float yaw; }
        [System.Serializable] sealed class PFile { public PStone[] stones; public PDrain[] drains; }

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            BuildGround(set, t);
            BuildDressing(set, ruined, rebuilt, t);
        }

        static Transform Child(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ---------------------------------------------------------------------------------------------------- ground
        static void BuildGround(Transform set, TownLayout t)
        {
            string folder = BeautyCornerArt.FindFolder("B6", "SquareCobbles");
            string json = folder != null ? folder + "/square_placements.json" : null;
            if (json == null || !File.Exists(BuilderUtil.Abs(json))) { Debug.LogWarning("SC HOLLOWMERE square ground: B6/SquareCobbles/square_placements.json is missing, the square keeps the paving slab only"); return; }
            PFile data = JsonUtility.FromJson<PFile>(File.ReadAllText(BuilderUtil.Abs(json)));
            if (data == null || data.stones == null || data.stones.Length == 0) { Debug.LogWarning("SC HOLLOWMERE square ground: no stones in " + json); return; }

            float shelf = HollowmereLayout.Ground(t, 0f, 0f);
            Transform holder = new GameObject("SquareGround").transform; holder.SetParent(set, false);
            holder.localPosition = new Vector3(0f, shelf, 0f);

            // The stones are laid in shelf-relative metres: check the terrain really is the shelf under them (sampled every ~2 m).
            float maxDrift = 0f; Vector2 worst = default; var cells = new HashSet<long>();
            foreach (PStone s in data.stones)
            {
                if (s.p == null || s.p.Length < 3) continue;
                long key = ((long)Mathf.FloorToInt(s.p[0] / 2f) << 32) ^ (uint)Mathf.FloorToInt(s.p[2] / 2f);
                if (!cells.Add(key)) continue;
                float dev = Mathf.Abs(HollowmereLayout.Ground(t, s.p[0], s.p[2]) - shelf);
                if (dev > maxDrift) { maxDrift = dev; worst = new Vector2(s.p[0], s.p[2]); }
            }
            string drift = "SC HOLLOWMERE square ground: shelf y " + shelf.ToString("F3") + ", max |Ground - shelf| " + maxDrift.ToString("F3") + " m (worst at x " + worst.x.ToString("F1") + ", z " + worst.y.ToString("F1") + ") over " + cells.Count + " samples";
            if (maxDrift > DriftWarn) Debug.LogWarning(drift + " (over " + DriftWarn + " m: the stones float or sink somewhere)"); else Debug.Log(drift);

            Dictionary<string, GameObject> prefabs = BeautyCornerGround.SettPrefabs(data.stones.Select(s => s.v).Where(v => !string.IsNullOrEmpty(v)).Distinct(), out Material bed);
            if (prefabs == null) { Debug.LogWarning("SC HOLLOWMERE square ground: the B6 Cobbles art (CobbleFamily.fbx) is missing, no setts"); return; }

            GameObject bedFbx = BeautyCornerArt.LoadModel(folder, "SquareBed.fbx");
            if (bedFbx != null)
            {
                var bedGo = (GameObject)PrefabUtility.InstantiatePrefab(bedFbx, holder); bedGo.name = "Art_SquareBed";
                BeautyCornerGround.Assign(bedGo, bed);
            }
            else Debug.LogWarning("SC HOLLOWMERE square ground: SquareBed.fbx is missing");

            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(json);
            string[] names = prefabs.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray();
            var spawner = new GameObject("Setts").AddComponent<SleepingCalamity.Presentation.CobbleSpawner>();
            spawner.transform.SetParent(holder, false);
            spawner.Configure(textAsset, names, names.Select(n => prefabs[n]).ToArray());
            Debug.Log("SC HOLLOWMERE square ground: spawner with " + data.stones.Length + " stones from " + prefabs.Count + " variant prefabs");

            PlaceDrains(holder, data.drains);
        }

        static void PlaceDrains(Transform holder, PDrain[] drains)
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
                var pos = new Vector3(d.p[0], d.p[1], d.p[2]);
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

            if (r.Plants > 0) plants.Stand(root, r, r.Kind == DecoKind.Pot ? PotSoilHeight : SoilHeight(r.Asset));
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
                Match m = Regex.Match(File.ReadAllText(src), @"soil[^0-9\r\n]{0,40}([0-9]+(?:\.[0-9]+)?)\s*m", RegexOptions.IgnoreCase);
                if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0.05f && v < 2f) return v;
            }
            return DefaultSoilHeight;
        }

        sealed class PlantKit
        {
            readonly List<GameObject> _models = new List<GameObject>();
            Material _mat; bool _loaded;

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
                    if (_mat == null) _mat = BeautyCornerArt.MaterialFromSet(folder, "T_B9_Foliage", "CrackWeeds", foliage: true);   // the B9 folders share one atlas
                }
            }

            // Stands r.Plants instances on the soil top of the planter or pot at root (its local frame), seeded by the row id.
            public void Stand(Transform root, DecoRow r, float soilY)
            {
                if (!_loaded) Load();
                if (_models.Count == 0 || _mat == null) return;
                var rnd = new System.Random(r.Id.Aggregate(17, (a, c) => a * 31 + c));
                for (int i = 0; i < r.Plants; i++)
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
            // The same B8 art path as the corner's StallLantern row; the art's origin is its base, so it hangs 0.34 m below the hook.
            var row = new BeautyCornerTable.Row
            {
                Brief = "Square dressing", Id = "Lantern_" + r.Id, Batch = "B8", Names = new[] { "stall_lantern", "lantern", "hanging_lantern" },
                Instances = new[] { new BeautyCornerTable.Instance(hook.x, hook.y - 0.34f, hook.z) },
                Placeholder = new[] { new BeautyCornerTable.Part(BeautyCornerTable.PartShape.Box, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.34f, 0.2f), "warm") },
            };
            BeautyCornerBuilder.PlaceRow(post, row);
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
