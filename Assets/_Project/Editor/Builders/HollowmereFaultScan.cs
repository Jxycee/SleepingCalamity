// Capture-free fault scan of the built Hollowmere scene (both presets): lists candidate visual faults for a later capture round to confirm. Not wired into
// ProjectBuilder.RunAll; the scene is opened, never saved (temporary hull colliders are removed again).
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.HollowmereFaultScan.Scan -OutDir <scratch>   (env SC_SCAN_DIR = report folder; SC_CAPTURE_NOBUILD=1 skips the build)
// Checks per preset over active MeshRenderers (LOD0 only; terrain, backdrop ring, water, particles, grass cards and ambient cards skipped):
//   float / sunk       pieces (the highest ancestor below the group level that fits 6 m, else the renderer) raycast down at 5 points against the scene colliders,
//                      the AABB tops of other solid pieces and the layout ground: float when every point gaps > 0.25 m (props carry few colliders), sunk when every
//                      point sinks below -0.6 m against colliders/layout only (building class: -2.0 m, deep-by-design kit pieces skipped)
//   overlap            props of different pieces whose AABB intersection is > 35% of the smaller one's (same family, one composite, ropes/cloth and rock piles
//                      excluded); m2 "inside" = share of the smaller piece's vertices embedded in the other's render mesh (a contact pair stays near 0)
//   foliage            foliage whose AABB overlaps a building renderer's by > 20% of the foliage AABB (its own composite excluded), with the same "inside"
//                      share and "crossing" (the building mesh both above and below the foliage's vertices)
//   uv                 per tiling PBR material (_BaseMap on a Lit/WearLit shader): texel density vs that texture's town median (> 2.5x either way) and the
//                      area-weighted anisotropic stretch (singular-value ratio of the uv->world Jacobian, > 2.5)
//   zfight             thin renderers (< 0.02 m on one axis) whose plane sits within 3 mm of another surface (collider hit or another thin renderer)
// Files in SC_SCAN_DIR: scan_<kind>.tsv, scan_census.tsv, scan_summary.json. Log lines: "SC SCAN <kind> count=N".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereFaultScan
    {
        const float FloatGap = 0.25f, SunkGap = -0.6f, SunkGapBuilding = -2.0f, OverlapShare = 0.35f, FoliageShare = 0.20f;
        const float DensityRatio = 2.5f, StretchRatio = 2.5f, ThinAxis = 0.02f, Coplanar = 0.003f;

        static readonly string[] SkipTokens = { "backdropring", "capturecameras", "pathgraph", "/lake/", "/stream/", "water", "shaft", "pollen", "godray", "ambient", "edgebounds",
                                                "/hub/", "townview", "placementstation", "bird", "butterfl", "bee_", "smoke", "fishring", "/lighting/", "/country/ring/", "/country/cards/" };
        static readonly string[] GroundTokens = { "ground", "meadow", "terrain", "setts", "squarebed", "ringroad", "gd_soil", "draped", "roadsurface", "paving", "cobbles" };
        static readonly string[] GrassTokens = { "grass", "detail_", "tuft", "crackweed" };
        static readonly string[] FoliageTokens = { "tree", "bush", "ivy", "grass", "weed", "moss", "hedge", "foliage", "leaf", "leaves", "fern", "plant", "tuft", "vine", "crown", "flower", "reed", "shrub" };
        static readonly string[] BuildingTokens = { "facade", "building", "house", "ruin", "bakery", "smithy", "mill", "school", "tower", "belltower", "walls",
                                                    "wall", "terrace", "step", "stair", "gate", "arch", "roof", "chimney", "slab", "retain", "kerb", "curb", "gutter", "bridge", "well", "fountain", "tread",
                                                    "footing", "plinth", "core", "pier", "buttress" };
        static readonly string[] BuildingGroups = { "/townwall/", "/keep/", "/lanes/retaining/", "/lanes/steps/", "/lanes/gardenwalls/", "/lanes/gates/", "/lanes/gutters/" };
        static readonly string[] DeepByDesign = { "retain", "core", "footing", "plinth", "foundation", "slab", "stair", "step", "kerb", "gutter", "pier", "buttress", "wall" };
        static readonly string[] HangTokens = { "lantern", "sign", "washing", "line", "rope", "banner", "flag", "awning", "hang", "bracket", "lamp", "window", "door", "shutter", "beam", "lintel",
                                                "bunting", "ivy", "vine", "chain", "pole", "light", "chimney", "pennant", "peg", "cloth_", "roof", "gutter", "eave", "hook" };
        static readonly string[] AttachTokens = { "rope", "pennant", "peg", "cloth_", "bunting", "chain", "washing", "/line_" };
        static readonly string[] DecalTokens = { "decal", "/surfaces/", "contactao", "squaremacro", "_ao", "stain", "puddle", "grime", "treadshade", "contactstrip" };

        enum Cat { Prop, Building, Foliage, Grass, Ground, Decal }

        class Piece
        {
            public Transform Root; public string Path, Family, Lower; public Cat Cat; public Bounds B; public List<MeshRenderer> Rs = new List<MeshRenderer>();
            public bool Hang, Thin;
        }

        class Row { public string Kind, Preset, Family, Path; public Vector3 Pos; public float M1, M2; public string Info; }

        static readonly List<Row> Rows = new List<Row>();
        static readonly Dictionary<MeshRenderer, MeshCollider> Hulls = new Dictionary<MeshRenderer, MeshCollider>();

        public static void Scan()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string dir = System.Environment.GetEnvironmentVariable("SC_SCAN_DIR");
            if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("Set SC_SCAN_DIR to the report folder");
            Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) HollowmereBuilder.Build();
            Rows.Clear();
            TownLayout layout = HollowmereLayout.Create();
            var census = new StringBuilder("preset\tpath\trenderers\tcolliders\n");
            var counts = new Dictionary<string, int>();
            bool savedBack = Physics.queriesHitBackfaces;
            try
            {
                HollowmereSplit.OpenAll(HollowmereBuilder.ScenePath);
                foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                {
                    HollowmereBuilder.ApplyPreset(preset);
                    Physics.SyncTransforms();
                    string p = preset.ToString();
                    Census(census, p);
                    List<Piece> pieces = Collect(out List<MeshRenderer> all);
                    Debug.Log("SC SCAN preset " + p + " renderers=" + all.Count + " pieces=" + pieces.Count + " " + string.Join(" ", Enum.GetValues(typeof(Cat)).Cast<Cat>().Select(c => c + "=" + pieces.Count(x => x.Cat == c))));
                    FloatSunk(p, pieces, layout);
                    Overlaps(p, pieces);
                    FoliageThrough(p, pieces);
                    foreach (MeshCollider c in Hulls.Values) if (c != null) UnityEngine.Object.DestroyImmediate(c);   // before the z-fight raycasts
                    Hulls.Clear();
                    Physics.SyncTransforms();
                    Uv(p, all, new HashSet<MeshRenderer>(pieces.Where(x => x.Cat == Cat.Foliage || x.Cat == Cat.Grass || x.Cat == Cat.Decal).SelectMany(x => x.Rs)));
                    ZFight(p, all);
                }
            }
            finally
            {
                foreach (MeshCollider c in Hulls.Values) if (c != null) UnityEngine.Object.DestroyImmediate(c);
                Hulls.Clear();
                Physics.queriesHitBackfaces = savedBack;
            }
            File.WriteAllText(Path.Combine(dir, "scan_census.tsv"), census.ToString());
            foreach (var g in Rows.GroupBy(r => r.Kind))
            {
                var sb = new StringBuilder("kind\tpreset\tfamily\tpath\tx\ty\tz\tm1\tm2\tinfo\n");
                foreach (Row r in g) sb.Append(r.Kind).Append('\t').Append(r.Preset).Append('\t').Append(r.Family).Append('\t').Append(r.Path).Append('\t')
                    .Append(F(r.Pos.x)).Append('\t').Append(F(r.Pos.y)).Append('\t').Append(F(r.Pos.z)).Append('\t').Append(F(r.M1)).Append('\t').Append(F(r.M2)).Append('\t').Append(r.Info).Append('\n');
                File.WriteAllText(Path.Combine(dir, "scan_" + g.Key + ".tsv"), sb.ToString());
                counts[g.Key] = g.Count();
            }
            foreach (string k in new[] { "float", "sunk", "sunk_building", "float_building", "overlap", "foliage", "uv_density", "uv_stretch", "zfight" })
                Debug.Log("SC SCAN " + k + " count=" + (counts.TryGetValue(k, out int n) ? n : 0));
            File.WriteAllText(Path.Combine(dir, "scan_summary.json"), "{" + string.Join(",", counts.Select(kv => "\"" + kv.Key + "\":" + kv.Value)) + ",\"seconds\":" + F((float)sw.Elapsed.TotalSeconds) + "}");
            Debug.Log("SC SCAN DONE seconds=" + sw.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " dir=" + dir);
        }

        static string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);

        static void Add(string kind, string preset, Piece pc, float m1, float m2, string info) =>
            Rows.Add(new Row { Kind = kind, Preset = preset, Family = pc.Family, Path = pc.Path, Pos = pc.B.center, M1 = m1, M2 = m2, Info = info });

        // ------------------------------------------------------------------------------------------------ collection
        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return "/" + string.Join("/", parts) + "/";
        }

        static int Depth(Transform t) { int d = 0; for (; t != null; t = t.parent) d++; return d; }

        static bool Any(string s, string[] tokens) { foreach (string t in tokens) if (s.Contains(t)) return true; return false; }

        static void Census(StringBuilder sb, string preset)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)   // T24c: the main scene and the art scenes OpenAll opened (rows repeat the shared paths, one per scene)
            {
                UnityEngine.SceneManagement.Scene sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (GameObject go in sc.GetRootGameObjects()) Walk(sb, preset, go.transform, 0);
            }
        }

        static void Walk(StringBuilder sb, string preset, Transform t, int depth)
        {
            if (!t.gameObject.activeInHierarchy || depth > 3) return;
            int rs = t.GetComponentsInChildren<MeshRenderer>(false).Length, cs = t.GetComponentsInChildren<Collider>(false).Length;
            if (rs == 0 && cs == 0) return;
            sb.Append(preset).Append('\t').Append(PathOf(t)).Append('\t').Append(rs).Append('\t').Append(cs).Append('\n');
            foreach (Transform c in t) Walk(sb, preset, c, depth + 1);
        }

        static HashSet<Renderer> LowLods()
        {
            var set = new HashSet<Renderer>();
            foreach (LODGroup g in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                LOD[] lods = g.GetLODs();
                for (int i = 1; i < lods.Length; i++) if (lods[i].renderers != null) foreach (Renderer r in lods[i].renderers) if (r != null) set.Add(r);
            }
            return set;
        }

        static List<Piece> Collect(out List<MeshRenderer> all)
        {
            HashSet<Renderer> low = LowLods();
            all = new List<MeshRenderer>();
            // Subtree bounds of every transform (active, enabled, LOD0 renderers): a piece is the highest ancestor below the group level (depth >= 4) that still
            // fits PieceMax, so a planter with its soil and plants, or a bench with its collider parent, is one piece; a lot or a stone pile is not.
            const float PieceMax = 6f;
            var sub = new Dictionary<Transform, Bounds>();
            var live = new List<MeshRenderer>();
            foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || low.Contains(r) || System.Text.RegularExpressions.Regex.IsMatch(r.name, "_LOD[1-9]$")) continue;
                if (!r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                live.Add(r);
                Bounds rb = r.bounds;
                for (Transform t = r.transform; t != null; t = t.parent)
                {
                    if (sub.TryGetValue(t, out Bounds b)) { b.Encapsulate(rb); sub[t] = b; } else sub[t] = rb;
                }
            }
            var byRoot = new Dictionary<Transform, Piece>();
            foreach (MeshRenderer r in live)
            {
                string path = PathOf(r.transform), lower = path.ToLowerInvariant();
                if (Any(lower, SkipTokens)) continue;
                all.Add(r);
                Transform root = r.transform;
                for (Transform t = r.transform.parent; t != null && Depth(t) >= 4; t = t.parent)
                {
                    Vector3 s = sub[t].size;
                    if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) > PieceMax) break;
                    root = t;
                }
                if (!byRoot.TryGetValue(root, out Piece pc))
                {
                    pc = new Piece { Root = root, Path = PathOf(root), B = r.bounds };
                    pc.Lower = pc.Path.ToLowerInvariant();
                    pc.Family = Family(root, PrefabUtility.GetOutermostPrefabInstanceRoot(root.gameObject));
                    byRoot[root] = pc;
                }
                else pc.B.Encapsulate(r.bounds);
                pc.Rs.Add(r);
            }
            foreach (Piece pc in byRoot.Values)
            {
                MeshRenderer dom = pc.Rs.OrderByDescending(r => Vol(r.bounds.size)).First();
                string mats = string.Join(" ", dom.sharedMaterials.Where(m => m != null).Select(m => m.name.ToLowerInvariant()));
                string key = (pc.Root.name + " " + dom.name + " " + mats).ToLowerInvariant();
                Vector3 s = pc.B.size;
                float minAxis = Mathf.Min(s.x, Mathf.Min(s.y, s.z)), maxAxis = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                pc.Thin = minAxis < ThinAxis;
                pc.Hang = Any(key, HangTokens);
                if (Any(key, GroundTokens) && maxAxis > 6f) pc.Cat = Cat.Ground;
                else if (Any(key, GrassTokens) || Any(pc.Lower, GrassTokens)) pc.Cat = Cat.Grass;
                else if (Any(key, DecalTokens) || Any(pc.Lower, DecalTokens) || (pc.Thin && s.y < ThinAxis)) pc.Cat = Cat.Decal;
                else if ((Any(key, FoliageTokens) && !key.Contains("gateleaf")) || pc.Lower.Contains("/foliage/")) pc.Cat = Cat.Foliage;
                else if (Any(key, BuildingTokens) || Any(pc.Lower, BuildingGroups) || maxAxis > 4f) pc.Cat = Cat.Building;
                else pc.Cat = Cat.Prop;
            }
            return byRoot.Values.ToList();
        }

        static string Family(Transform root, GameObject prefabRoot)
        {
            if (prefabRoot != null)
            {
                UnityEngine.Object src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefabRoot);
                string ap = src != null ? AssetDatabase.GetAssetPath(src) : null;
                if (!string.IsNullOrEmpty(ap)) return Path.GetFileNameWithoutExtension(ap);
            }
            string n = root.name.Replace("(Clone)", "").Trim();
            n = System.Text.RegularExpressions.Regex.Replace(n, @"(\s*\(\d+\))|(_T\d+)|(_LOD\d)|([_\- ]?\d+)$", "");
            n = System.Text.RegularExpressions.Regex.Replace(n, @"[_\- ]?\d+$", "");
            return string.IsNullOrEmpty(n) ? root.name : n;
        }

        // ------------------------------------------------------------------------------------------------ 1. float / sunk
        static void FloatSunk(string preset, List<Piece> pieces, TownLayout layout)
        {
            // Props carry few colliders, so the AABB tops of other solid pieces count as support too (a sack on a crate, a jug on a counter).
            var supports = pieces.Where(p => (p.Cat == Cat.Prop || p.Cat == Cat.Building) && p.B.size.x < 60f && p.B.size.z < 60f).ToList();
            const float cell = 4f;
            var grid = new Dictionary<(int, int), List<Piece>>();
            foreach (Piece s in supports)
                for (int x = Mathf.FloorToInt(s.B.min.x / cell); x <= Mathf.FloorToInt(s.B.max.x / cell); x++)
                    for (int z = Mathf.FloorToInt(s.B.min.z / cell); z <= Mathf.FloorToInt(s.B.max.z / cell); z++)
                    {
                        if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<Piece>();
                        l.Add(s);
                    }
            foreach (Piece pc in pieces)
            {
                if (pc.Cat != Cat.Prop && pc.Cat != Cat.Building) continue;
                if (pc.Hang || pc.B.size.x > 60f) continue;
                bool building = pc.Cat == Cat.Building;
                float probe = building ? 3f : 1f, lift = probe + 2f;
                Bounds b = pc.B;
                float minGap = float.MaxValue, maxGap = float.MinValue;
                int hitCount = 0;
                string groundName = "";
                Vector2[] offs = { Vector2.zero, new Vector2(0.35f, 0.35f), new Vector2(-0.35f, 0.35f), new Vector2(0.35f, -0.35f), new Vector2(-0.35f, -0.35f) };
                foreach (Vector2 o in offs)
                {
                    float x = b.center.x + o.x * b.size.x, z = b.center.z + o.y * b.size.z;
                    var origin = new Vector3(x, b.min.y + lift, z);
                    float groundY = float.NaN; string gname = null;
                    foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, lift + 12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.transform.IsChildOf(pc.Root)) continue;
                        if (h.point.y > b.min.y + probe) continue;
                        if (float.IsNaN(groundY) || h.point.y > groundY) { groundY = h.point.y; gname = h.collider.name; }
                    }
                    if (float.IsNaN(groundY)) { groundY = HollowmereLayout.Ground(layout, x, z); gname = "layout"; } else hitCount++;
                    maxGap = Mathf.Max(maxGap, b.min.y - groundY);   // sunk: colliders and layout ground only (an AABB top above a prop's foot is not proof)
                    if (grid.TryGetValue((Mathf.FloorToInt(x / cell), Mathf.FloorToInt(z / cell)), out var near))
                        foreach (Piece s in near)
                        {
                            if (s == pc || s.Root.IsChildOf(pc.Root) || pc.Root.IsChildOf(s.Root)) continue;
                            Bounds sb = s.B;
                            if (x < sb.min.x || x > sb.max.x || z < sb.min.z || z > sb.max.z) continue;
                            if (SameComposite(pc.Root, s.Root) && sb.min.y <= b.min.y + 0.05f && sb.max.y >= b.min.y) { groundY = b.min.y; gname = "part-of:" + s.Family; continue; }   // a belfry on its tower
                            if (sb.max.y > b.min.y + probe || sb.max.y <= groundY) continue;
                            groundY = sb.max.y; gname = "aabb:" + s.Family;
                        }
                    float gap = b.min.y - groundY;
                    if (gap < minGap) { minGap = gap; groundName = gname; }
                }
                string info = "cat=" + pc.Cat + " size=" + V(b.size) + " ground=" + groundName + " hits=" + hitCount + "/5";
                if (minGap > FloatGap) Add(building ? "float_building" : "float", preset, pc, minGap, maxGap, info);
                else if (maxGap < (building ? SunkGapBuilding : SunkGap) && !(building && Any((pc.Root.name + " " + pc.Rs[0].name).ToLowerInvariant(), DeepByDesign))) Add(building ? "sunk_building" : "sunk", preset, pc, maxGap, minGap, info);
            }
        }

        static string V(Vector3 v) => v.x.ToString("F2", CultureInfo.InvariantCulture) + "x" + v.y.ToString("F2", CultureInfo.InvariantCulture) + "x" + v.z.ToString("F2", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------------------------------------ hull inside test
        static MeshCollider Hull(MeshRenderer r)
        {
            if (Hulls.TryGetValue(r, out MeshCollider c)) return c;
            c = null;
            try
            {
                Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
                if (m != null && m.vertexCount >= 4)
                {
                    c = r.gameObject.AddComponent<MeshCollider>();
                    c.sharedMesh = m; c.convex = false;
                    if (c.bounds.size.sqrMagnitude < 1e-6f) { UnityEngine.Object.DestroyImmediate(c); c = null; }
                }
            }
            catch (Exception) { if (c != null) UnityEngine.Object.DestroyImmediate(c); c = null; }
            Hulls[r] = c;
            return c;
        }

        static readonly Dictionary<Mesh, Vector3[]> SampleCache = new Dictionary<Mesh, Vector3[]>();

        static IEnumerable<Vector3> Samples(Piece pc)
        {
            foreach (MeshRenderer r in pc.Rs)
            {
                Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
                if (!SampleCache.TryGetValue(m, out Vector3[] s))
                {
                    Vector3[] v;
                    try { v = m.vertices; } catch (Exception) { v = new Vector3[0]; }
                    int step = Mathf.Max(1, v.Length / 120);
                    var l = new List<Vector3>();
                    for (int i = 0; i < v.Length; i += step) l.Add(v[i]);
                    s = l.ToArray();
                    SampleCache[m] = s;
                }
                Matrix4x4 mx = r.localToWorldMatrix;
                foreach (Vector3 v in s) yield return mx.MultiplyPoint3x4(v);
            }
        }

        // Share of `small`'s sampled vertices embedded (more than 2 cm deep) in the render mesh of any of `others`: a temporary non-convex mesh collider is cast at
        // from the vertex up and down with backface hits on; a backface hit first in both directions means the vertex sits inside that solid.
        // `crossing` = min(share with the mesh above, share with the mesh below): foliage that has the other mesh both over and under it (a crown through a roof).
        static float InsideShare(Piece small, IEnumerable<MeshRenderer> others, out float crossing)
        {
            crossing = 0f;
            var hulls = others.Select(Hull).Where(h => h != null).ToList();
            if (hulls.Count == 0) return -1f;
            Physics.SyncTransforms();
            bool savedBack = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            int n = 0, inside = 0, under = 0, over = 0;
            try
            {
                foreach (Vector3 p in Samples(small))
                {
                    n++;
                    bool isIn = false, isUnder = false, isOver = false;
                    foreach (MeshCollider h in hulls)
                    {
                        Bounds hb = h.bounds;
                        if (p.x < hb.min.x || p.x > hb.max.x || p.z < hb.min.z || p.z > hb.max.z) continue;
                        bool upHit = h.Raycast(new Ray(p, Vector3.up), out RaycastHit hu, 12f);
                        bool dnHit = h.Raycast(new Ray(p, Vector3.down), out RaycastHit hd, 12f);
                        if (upHit) isUnder = true;
                        if (dnHit) isOver = true;
                        if (upHit && dnHit && Vector3.Dot(hu.normal, Vector3.up) > 0f && Vector3.Dot(hd.normal, Vector3.down) > 0f && hu.distance > 0.02f && hd.distance > 0.02f) isIn = true;
                    }
                    if (isIn) inside++;
                    if (isUnder) under++;
                    if (isOver) over++;
                }
            }
            finally { Physics.queriesHitBackfaces = savedBack; }
            if (n == 0) return -1f;
            crossing = Mathf.Min(under, over) / (float)n;
            return (float)inside / n;
        }

        // Pieces of one composite (a ruin and its own foliage, a tower and its belfry) share an ancestor at depth >= 5.
        static bool SameComposite(Transform a, Transform b)
        {
            for (Transform t = a; t != null && Depth(t) >= 5; t = t.parent) if (b.IsChildOf(t)) return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------------ 2. interpenetration
        static float Vol(Vector3 s) => Mathf.Max(s.x, 0.05f) * Mathf.Max(s.y, 0.05f) * Mathf.Max(s.z, 0.05f);

        // Share of `of`'s AABB that lies inside `other`'s AABB (per-axis product, so flat pieces are measured fairly).
        static float Share(Bounds of, Bounds other)
        {
            Vector3 lo = Vector3.Max(of.min, other.min), hi = Vector3.Min(of.max, other.max), d = hi - lo;
            if (d.x <= 0f || d.y <= 0f || d.z <= 0f) return 0f;
            float s = 1f;
            for (int k = 0; k < 3; k++) s *= Mathf.Min(1f, d[k] / Mathf.Max(of.size[k], 1e-4f));
            return s;
        }

        static void Overlaps(string preset, List<Piece> pieces)
        {
            var props = pieces.Where(p => p.Cat == Cat.Prop && !p.Thin).ToList();
            const float cell = 4f;
            var grid = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < props.Count; i++)
            {
                Bounds b = props[i].B;
                for (int x = Mathf.FloorToInt(b.min.x / cell); x <= Mathf.FloorToInt(b.max.x / cell); x++)
                    for (int z = Mathf.FloorToInt(b.min.z / cell); z <= Mathf.FloorToInt(b.max.z / cell); z++)
                    {
                        if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<int>();
                        l.Add(i);
                    }
            }
            var seen = new HashSet<long>();
            foreach (List<int> l in grid.Values)
                for (int a = 0; a < l.Count; a++)
                    for (int c = a + 1; c < l.Count; c++)
                    {
                        int i = Mathf.Min(l[a], l[c]), j = Mathf.Max(l[a], l[c]);
                        if (!seen.Add(((long)i << 32) | (uint)j)) continue;
                        Piece p = props[i], q = props[j];
                        if (p.Family == q.Family) continue;
                        if (p.Root.IsChildOf(q.Root) || q.Root.IsChildOf(p.Root)) continue;
                        if (p.Root.parent == q.Root.parent && (p.Lower.Contains("lanes") || p.Lower.Contains("wall") || Depth(p.Root.parent) >= 4)) continue;   // one composite (a washing line, a bunting run)
                        if (Any(p.Lower, AttachTokens) || Any(q.Lower, AttachTokens)) continue;   // ropes, pennants, pegs and cloth are tied to things by design
                        if (p.Family.StartsWith("Rock") && q.Family.StartsWith("Rock")) continue;   // stone piles nest by design
                        float vp = Vol(p.B.size), vq = Vol(q.B.size);
                        Piece small = vp <= vq ? p : q, big = small == p ? q : p;
                        float share = Share(small.B, big.B);
                        if (share <= OverlapShare) continue;
                        float inside = InsideShare(small, big.Rs, out float crossing);
                        Add("overlap", preset, small, share, inside, "crossing=" + F(crossing) + " with=" + big.Family + " withPath=" + big.Path + " withPos=" + V(big.B.center) + " size=" + V(small.B.size));
                    }
        }

        // ------------------------------------------------------------------------------------------------ 3. foliage through buildings
        static void FoliageThrough(string preset, List<Piece> pieces)
        {
            var buildRs = pieces.Where(p => p.Cat == Cat.Building).SelectMany(p => p.Rs.Select(r => (p, r))).Where(x => x.r.bounds.size.x < 60f && x.r.bounds.size.z < 60f).ToList();
            foreach (Piece f in pieces.Where(p => p.Cat == Cat.Foliage))
            {
                float best = 0f; Piece bestP = null; var hit = new List<MeshRenderer>();
                foreach (var (bp, r) in buildRs)
                {
                    if (bp == f || SameComposite(f.Root, bp.Root)) continue;   // a ruin's or gate's own foliage is placed on it by design
                    float share = Share(f.B, r.bounds);
                    if (share > FoliageShare) { hit.Add(r); if (share > best) { best = share; bestP = bp; } }
                }
                if (bestP == null) continue;
                float inside = InsideShare(f, hit, out float crossing);
                Add("foliage", preset, f, best, inside, "crossing=" + F(crossing) + " building=" + bestP.Family + " buildingPath=" + bestP.Path + " renderers=" + hit.Count + " size=" + V(f.B.size));
            }
        }

        // ------------------------------------------------------------------------------------------------ 4. uv texel density and stretch
        struct UvStat { public float World, Uv, StretchW; }

        static readonly Dictionary<(Mesh, int, Vector3Int), UvStat> UvCache = new Dictionary<(Mesh, int, Vector3Int), UvStat>();

        static UvStat Measure(Mesh m, int sub, Matrix4x4 mx)
        {
            Vector3[] v = m.vertices; Vector2[] uv = m.uv; int[] t = m.GetTriangles(sub);
            var s = new UvStat();
            if (uv == null || uv.Length != v.Length) return s;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = mx.MultiplyPoint3x4(v[t[i]]), b = mx.MultiplyPoint3x4(v[t[i + 1]]), c = mx.MultiplyPoint3x4(v[t[i + 2]]);
                Vector2 ua = uv[t[i]], ub = uv[t[i + 1]], uc = uv[t[i + 2]];
                Vector3 e1 = b - a, e2 = c - a; Vector2 d1 = ub - ua, d2 = uc - ua;
                float wa = Vector3.Cross(e1, e2).magnitude * 0.5f, det = d1.x * d2.y - d1.y * d2.x, ua2 = Mathf.Abs(det) * 0.5f;
                if (wa < 1e-8f) continue;
                s.World += wa; s.Uv += ua2;
                if (ua2 < 1e-12f) { s.StretchW += wa * 10f; continue; }   // collapsed uv: counted as extreme stretch
                // J = [e1 e2] * inv([d1 d2]): columns are dP/du and dP/dv
                float inv = 1f / det;
                Vector3 pu = (e1 * d2.y - e2 * d1.y) * inv, pv = (e2 * d1.x - e1 * d2.x) * inv;
                float A = Vector3.Dot(pu, pu), B = Vector3.Dot(pu, pv), C = Vector3.Dot(pv, pv);
                float tr = A + C, disc = Mathf.Sqrt(Mathf.Max(0f, (A - C) * (A - C) + 4f * B * B));
                float l1 = (tr + disc) * 0.5f, l2 = Mathf.Max((tr - disc) * 0.5f, 1e-12f);
                s.StretchW += wa * Mathf.Min(10f, Mathf.Sqrt(l1 / l2));
            }
            return s;
        }

        // Foliage cards, grass and decals use atlas or blob textures, not tiling PBR sets: skipped, as are alpha-clipped and transparent materials.
        static void Uv(string preset, List<MeshRenderer> all, HashSet<MeshRenderer> skip)
        {
            var items = new List<(MeshRenderer r, Material m, string tex, float density, float stretch, float world)>();
            int unreadable = 0;
            foreach (MeshRenderer r in all)
            {
                if (skip.Contains(r)) continue;
                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Material[] mats = r.sharedMaterials;
                Vector3 ls = r.transform.lossyScale;
                var key3 = new Vector3Int(Mathf.RoundToInt(ls.x * 100f), Mathf.RoundToInt(ls.y * 100f), Mathf.RoundToInt(ls.z * 100f));
                bool uniform = Mathf.Abs(ls.x - ls.y) < 0.01f * Mathf.Abs(ls.x) && Mathf.Abs(ls.x - ls.z) < 0.01f * Mathf.Abs(ls.x);
                for (int sub = 0; sub < Mathf.Min(mats.Length, mesh.subMeshCount); sub++)
                {
                    Material m = mats[sub];
                    if (m == null || m.shader == null) continue;
                    string sh = m.shader.name;
                    if (!(sh.Contains("Lit") || sh.Contains("Wear")) || !m.HasProperty("_BaseMap")) continue;
                    if (m.renderQueue >= 2450 || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f)) continue;
                    Texture tex = m.GetTexture("_BaseMap");
                    if (tex == null) continue;
                    UvStat st;
                    try
                    {
                        if (uniform)
                        {
                            var k = (mesh, sub, Vector3Int.zero);
                            if (!UvCache.TryGetValue(k, out st)) UvCache[k] = st = Measure(mesh, sub, Matrix4x4.identity);
                            float sc = Mathf.Abs(ls.x);
                            st.World *= sc * sc; st.StretchW *= sc * sc;
                        }
                        else
                        {
                            var k = (mesh, sub, key3);
                            if (!UvCache.TryGetValue(k, out st)) UvCache[k] = st = Measure(mesh, sub, Matrix4x4.Scale(ls));
                        }
                    }
                    catch (Exception) { unreadable++; continue; }
                    if (st.World < 1e-4f || st.Uv < 1e-9f) continue;
                    Vector2 tiling = m.GetTextureScale("_BaseMap");
                    float texels = Mathf.Sqrt(st.Uv * Mathf.Abs(tiling.x * tiling.y) * tex.width * tex.height / st.World);   // texels per metre
                    items.Add((r, m, tex.name, texels, st.StretchW / st.World, st.World));
                }
            }
            if (unreadable > 0) Debug.LogWarning("SC SCAN uv unreadable submeshes=" + unreadable);
            foreach (var g in items.GroupBy(x => x.tex))
            {
                var d = g.Select(x => x.density).OrderBy(x => x).ToList();
                float median = d[d.Count / 2];
                foreach (var it in g)
                {
                    float ratio = it.density / median;
                    var pc = new Piece { Path = PathOf(it.r.transform), Family = Family(it.r.transform, PrefabUtility.GetOutermostPrefabInstanceRoot(it.r.gameObject)), B = it.r.bounds };
                    string info = "mat=" + it.m.name + " tex=" + it.tex + " texelsPerM=" + it.density.ToString("F0", CultureInfo.InvariantCulture) + " median=" + median.ToString("F0", CultureInfo.InvariantCulture)
                                  + " users=" + g.Count() + " worldArea=" + it.world.ToString("F1", CultureInfo.InvariantCulture) + " size=" + V(it.r.bounds.size);
                    if (g.Count() >= 3 && (ratio > DensityRatio || ratio < 1f / DensityRatio)) Add("uv_density", preset, pc, ratio, it.stretch, info);
                    if (it.stretch > StretchRatio) Add("uv_stretch", preset, pc, it.stretch, ratio, info);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ 5. z-fight
        static void ZFight(string preset, List<MeshRenderer> all)
        {
            var thin = new List<(MeshRenderer r, int axis)>();
            foreach (MeshRenderer r in all)
            {
                Vector3 s = r.bounds.size;
                int ax = s.x < s.y ? (s.x < s.z ? 0 : 2) : (s.y < s.z ? 1 : 2);
                if (s[ax] >= ThinAxis) continue;
                if (s[(ax + 1) % 3] < 0.05f || s[(ax + 2) % 3] < 0.05f) continue;
                if (Any(PathOf(r.transform).ToLowerInvariant(), GrassTokens)) continue;
                if (Planar(r, ax) < 0.8f) continue;   // a rope or a sagging line is thin but not a plane
                thin.Add((r, ax));
            }
            int transparentPairs = 0;
            // thin vs thin, same plane
            for (int i = 0; i < thin.Count; i++)
                for (int j = i + 1; j < thin.Count; j++)
                {
                    var (a, ax) = thin[i]; var (b, bx) = thin[j];
                    if (ax != bx) continue;
                    if (Mathf.Abs(a.bounds.center[ax] - b.bounds.center[ax]) > Coplanar) continue;
                    int u = (ax + 1) % 3, w = (ax + 2) % 3;
                    float ou = Mathf.Min(a.bounds.max[u], b.bounds.max[u]) - Mathf.Max(a.bounds.min[u], b.bounds.min[u]);
                    float ow = Mathf.Min(a.bounds.max[w], b.bounds.max[w]) - Mathf.Max(a.bounds.min[w], b.bounds.min[w]);
                    if (ou <= 0f || ow <= 0f) continue;
                    float smallArea = Mathf.Min(a.bounds.size[u] * a.bounds.size[w], b.bounds.size[u] * b.bounds.size[w]);
                    if (ou * ow < Mathf.Max(1e-4f, 0.05f * smallArea)) continue;   // edges that only touch (consecutive step strips)
                    if (Queue(a) >= 2450 && Queue(b) >= 2450) { transparentPairs++; continue; }   // blended, no depth write: sort order, not z-fight
                    if (a.sharedMaterial == b.sharedMaterial && a.GetComponent<MeshFilter>().sharedMesh == b.GetComponent<MeshFilter>().sharedMesh && (a.bounds.center - b.bounds.center).sqrMagnitude < 1e-6f)
                    { AddZ(preset, a, "duplicate=" + PathOf(b.transform)); continue; }
                    AddZ(preset, a, "thinPeer=" + PathOf(b.transform) + " dPlane=" + F(Mathf.Abs(a.bounds.center[ax] - b.bounds.center[ax])));
                }
            Debug.Log("SC SCAN zfight " + preset + " thin=" + thin.Count + " transparentCoplanarPairs=" + transparentPairs + " (not listed)");
            // thin vs the rendered surfaces under it: most visible ground (the square bed, lane treads) carries no collider, so every opaque, non-thin renderer whose
            // bounds touch the thin one gets a temporary mesh collider (its own render mesh) and is ray-tested directly along the thin axis.
            var thinSet = new HashSet<MeshRenderer>(thin.Select(x => x.r));
            var targets = all.Where(r => !thinSet.Contains(r) && Queue(r) < 2450).ToList();
            const float cell = 8f;
            var grid = new Dictionary<(int, int), List<MeshRenderer>>();
            foreach (MeshRenderer r in targets)
            {
                Bounds b = r.bounds;
                if (b.size.x > 400f || b.size.z > 400f) continue;
                for (int x = Mathf.FloorToInt(b.min.x / cell); x <= Mathf.FloorToInt(b.max.x / cell); x++)
                    for (int z = Mathf.FloorToInt(b.min.z / cell); z <= Mathf.FloorToInt(b.max.z / cell); z++)
                    {
                        if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<MeshRenderer>();
                        l.Add(r);
                    }
            }
            var temp = new Dictionary<MeshRenderer, MeshCollider>();
            try
            {
                foreach (var (r, ax) in thin)
                {
                    Vector3 n = Vector3.zero; n[ax] = 1f;
                    Bounds b = r.bounds, bx = b; bx.Expand(0.02f);
                    int u = (ax + 1) % 3, w = (ax + 2) % 3;
                    var cands = new HashSet<MeshRenderer>();
                    for (int x = Mathf.FloorToInt(bx.min.x / cell); x <= Mathf.FloorToInt(bx.max.x / cell); x++)
                        for (int z = Mathf.FloorToInt(bx.min.z / cell); z <= Mathf.FloorToInt(bx.max.z / cell); z++)
                            if (grid.TryGetValue((x, z), out var l)) foreach (MeshRenderer c in l) if (c != r && c.bounds.Intersects(bx) && !c.transform.IsChildOf(r.transform)) cands.Add(c);
                    foreach (MeshRenderer c in cands)
                    {
                        if (!temp.TryGetValue(c, out MeshCollider mc))
                        {
                            try { mc = c.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = c.GetComponent<MeshFilter>().sharedMesh; mc.convex = false; }
                            catch (Exception) { if (mc != null) UnityEngine.Object.DestroyImmediate(mc); mc = null; }
                            temp[c] = mc;
                        }
                        if (mc == null) continue;
                        int close = 0, tried = 0; float bestD = 1f;
                        foreach (Vector2 o in new[] { Vector2.zero, new Vector2(0.3f, 0.3f), new Vector2(-0.3f, 0.3f), new Vector2(0.3f, -0.3f), new Vector2(-0.3f, -0.3f) })
                        {
                            Vector3 p = b.center; p[u] += o.x * b.size[u]; p[w] += o.y * b.size[w];
                            tried++;
                            foreach (Vector3 dir in new[] { -n, n })
                                if (mc.Raycast(new Ray(p - dir * 0.05f, dir), out RaycastHit h, 0.1f))
                                {
                                    float d = Mathf.Abs(h.point[ax] - b.center[ax]);
                                    if (d <= Coplanar) { close++; bestD = Mathf.Min(bestD, d); break; }
                                }
                        }
                        if (close >= 3) { AddZ(preset, r, "surface=" + PathOf(c.transform) + " hits=" + close + "/" + tried + " d=" + F(bestD)); break; }
                    }
                }
            }
            finally { foreach (MeshCollider mc in temp.Values) if (mc != null) UnityEngine.Object.DestroyImmediate(mc); }
        }

        static int Queue(Renderer r) => r.sharedMaterial != null ? r.sharedMaterial.renderQueue : 2000;

        // Area-weighted share of the renderer's triangles whose world normal is within ~25 degrees of the thin axis.
        static float Planar(MeshRenderer r, int ax)
        {
            Mesh m = r.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v; int[] t;
            try { v = m.vertices; t = m.triangles; } catch (Exception) { return 0f; }
            if (t.Length > 30000) return 0f;
            Matrix4x4 mx = r.localToWorldMatrix;
            float all = 0f, flat = 0f;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = mx.MultiplyPoint3x4(v[t[i]]), b = mx.MultiplyPoint3x4(v[t[i + 1]]), c = mx.MultiplyPoint3x4(v[t[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                float area = n.magnitude;
                if (area < 1e-10f) continue;
                all += area;
                if (Mathf.Abs(n[ax]) / area > 0.9f) flat += area;
            }
            return all > 0f ? flat / all : 0f;
        }

        static void AddZ(string preset, MeshRenderer r, string info)
        {
            Material m = r.sharedMaterial;
            var pc = new Piece { Path = PathOf(r.transform), Family = Family(r.transform, PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject)), B = r.bounds };
            Add("zfight", preset, pc, r.bounds.size[Array.IndexOf(new[] { r.bounds.size.x, r.bounds.size.y, r.bounds.size.z }, Mathf.Min(r.bounds.size.x, Mathf.Min(r.bounds.size.y, r.bounds.size.z)))], 0f,
                info + " mat=" + (m != null ? m.name : "none") + " shader=" + (m != null && m.shader != null ? m.shader.name : "none") + " queue=" + (m != null ? m.renderQueue : 0));
        }
    }
}
