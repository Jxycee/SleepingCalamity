// Hollowmere's orchard (D7) and the Ruined allotments (D8), area 1 world T07 (2026-11-17). Everything is table-driven and position-hashed (plan rule 3.3), editor-time only (authority none).
//   Orchard (x 34..51, z -61..-42, south of the Ring Lane's garden wall, whose gate piers and broken courses are the lane kit's, LK0469-LK0481, and stay the orchard's north wall):
//     - seven gnarled apple crowns (TR02/TR03 at 4.6-5.8 m, wide girth, a yellow-green leaf variant) in both presets, each with a low earth mound; the Wild builder skips the box
//       (Reserved) so the old 9-15 m town trees no longer stand there; in the Ruined preset also a fallen apple tree with a torn-up root pit;
//     - bases (stones, grass ring, leaf litter), windfalls clustered under three crowns and along the fallen trunk, the ladder, baskets, a half-loaded wheelbarrow, crates, tools at the
//       gate pier, a bench under the best crown, two nooks (R2_18's corner and the pocket behind the fallen tree), moss/damp/grime at the wall feet, spill at the wall gap;
//     - the worn path (mud decals + a cleared strip in the terrain grass mask, see GrassMask) from the gate to the wall gap; one shadowless fill light under the crowns.
//   Allotments (Ruined): the four garden lots R2_07, R2_18, R2_22, R3_02 each get a plot on the free side of the lot (pen-wall fence runs, fallen or leaning, a gate off one hinge, bolted
//   beds gone to seed, a toppled scarecrow, a water butt with a bucket, tools where work stopped; one bed on R2_18 weeded and replanted).
//   Every prop is Megascans / Poly Haven from the adopted kit (T17a-d included); the procedural garden kit is used for the scarecrow only. Colliders: trunks, the fallen tree, large props.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        const string Ad = HollowmereStreets.Adopted, Cel = HollowmereStreets.CellarAdopted;
        const string MatFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string TreesFolder = "Assets/_Project/Content/Art/Town/Trees/";
        const string HayTex = "Assets/_Project/Content/Art/Town/Decals/T17d_BaseColor.png";

        // ------------------------------------------------------------------------------------------------ masks (also read by HollowmereWild and HollowmereFoliage)
        static readonly Vector2[] Poly =
        {
            new Vector2(34.3f, -48.0f), new Vector2(39.3f, -44.9f), new Vector2(44.3f, -40.0f), new Vector2(49.5f, -42.5f), new Vector2(51.5f, -48.5f), new Vector2(49.4f, -53.0f),
            new Vector2(46.0f, -56.2f), new Vector2(43.0f, -58.0f), new Vector2(38.0f, -60.0f), new Vector2(34.0f, -59.8f), new Vector2(33.9f, -56.0f), new Vector2(34.0f, -50.5f),
        };
        // The worn path: the gate (between the piers at (39.2,-45.0) and (40.9,-43.9)) to the foot of the wall gap.
        static readonly Vector2[] PathLine = { new Vector2(40.7f, -45.4f), new Vector2(41.8f, -48.5f), new Vector2(41.9f, -52.0f), new Vector2(41.5f, -55.0f), new Vector2(43.0f, -57.6f) };

        static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        internal static bool InOrchard(float x, float z, float margin = 0f)
        {
            var p = new Vector2(x, z); bool inside = false;
            for (int i = 0, j = Poly.Length - 1; i < Poly.Length; j = i++)
                if ((Poly[i].y > p.y) != (Poly[j].y > p.y) && p.x < (Poly[j].x - Poly[i].x) * (p.y - Poly[i].y) / (Poly[j].y - Poly[i].y) + Poly[i].x) inside = !inside;
            if (inside) return true;
            if (margin <= 0f) return false;
            for (int i = 0, j = Poly.Length - 1; i < Poly.Length; j = i++) if (DistToSeg(p, Poly[j], Poly[i]) < margin) return true;
            return false;
        }

        internal static float PathDist(float x, float z)
        {
            var p = new Vector2(x, z); float d = float.MaxValue;
            for (int i = 1; i < PathLine.Length; i++) d = Mathf.Min(d, DistToSeg(p, PathLine[i - 1], PathLine[i]));
            return d;
        }

        // True on the cleared strip of the path (ragged edge: 0.5-0.95 m half width from a noise of the position).
        // T07b: worn ground under the harvest tree and round the bench, so the story props stand on bare earth and hay, not in long grass.
        static readonly Vector3[] WornDiscs = { new Vector3(39.5f, -53.0f, 2.7f), new Vector3(43.6f, -51.0f, 1.5f), new Vector3(39.4f, -50.9f, 0f), new Vector3(36.3f, -51.0f, 2.1f) };
        // T07c: the crates, sack and tipped basket stand here, 6.8 m out from Eye_Orchard (33.2,-44.7) and 8 degrees right of its axis, on a worn patch in the lit lawn; the ladder and full basket stay at the trunk behind.
        static readonly Vector2 HarvestSpot = new Vector2(36.1f, -50.9f);
        internal static bool OnPath(float x, float z)
        {
            foreach (Vector3 d in WornDiscs) if (Vector2.Distance(new Vector2(x, z), new Vector2(d.x, d.y)) < d.z * Mathf.Lerp(0.7f, 1.05f, Mathf.PerlinNoise(x * 2.3f + 5f, z * 2.3f + 1f))) return true;
            return OnPathLine(x, z);
        }

        static bool OnPathLine(float x, float z) => PathDist(x, z) < Mathf.Lerp(0.5f, 0.95f, Mathf.PerlinNoise(x * 1.9f + 3f, z * 1.9f + 9f));

        // True at wall feet and trunk bases: the only places the orchard carpet takes the tall spiky accent clumps (T07b).
        internal static bool AccentSpot(float x, float z)
        {
            var p = new Vector2(x, z);
            for (int i = 0, j = Poly.Length - 1; i < Poly.Length; j = i++) if (DistToSeg(p, Poly[j], Poly[i]) < 1.3f) return true;
            for (int i = 0; i < Trees.Length; i++) if (Vector2.Distance(p, new Vector2(Trees[i].X, Trees[i].Z)) < 1.6f) return true;
            return false;
        }

        // The trees of the Wild builder stay out of the box (and 2.5 m round it).
        internal static bool Reserved(float x, float z) => InOrchard(x, z, 2.5f);

        // ------------------------------------------------------------------------------------------------ tables
        struct TreeDef { public float X, Z, H; public string Code; public float Yaw, Girth; }
        // T07b: four crowns (was seven) at 6-10 m spacing so sun and sky come through; crowns no wider than tall (Girth 0.9-1.0). Index 0 carries the harvest story.
        static readonly TreeDef[] Trees =
        {
            new TreeDef { X = 39.6f, Z = -54.6f, H = 6.0f, Code = "TR03", Yaw = 20f, Girth = 0.85f },    // 0 the harvest tree: ladder, baskets, crates
            new TreeDef { X = 44.0f, Z = -45.4f, H = 5.6f, Code = "TR03", Yaw = 250f, Girth = 0.85f },
            new TreeDef { X = 44.6f, Z = -51.6f, H = 6.0f, Code = "TR02", Yaw = 80f, Girth = 0.85f },   // 2 the bench
            new TreeDef { X = 36.6f, Z = -57.6f, H = 5.2f, Code = "TR03", Yaw = 60f, Girth = 0.85f },    // 3 R2_18's corner (left frame)
        };
        static readonly Vector2 FallenRoot = new Vector2(47.0f, -53.2f), FallenCrown = new Vector2(47.4f, -47.6f);
        static readonly Vector2 PierA = new Vector2(39.2f, -45.0f), PierB = new Vector2(40.9f, -43.9f);
        static readonly Vector2 OrchardNormal = new Vector2(0.54f, -0.84f);   // out of the garden wall into the orchard at the gate
        static readonly Vector2 Mill = new Vector2(69.5f, -58.5f);

        // ------------------------------------------------------------------------------------------------ state
        static TownLayout T;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, Bounds> unit = new Dictionary<GameObject, Bounds>();
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static readonly List<Vector2> eyeCams = new List<Vector2>();
        static readonly List<Vector2> trunkXZ = new List<Vector2>();
        static int decals, thinned;
        static void Count(string k, int n = 1) { counts[k] = counts.TryGetValue(k, out int c) ? c + n : n; }
        static float H(int a, int b, int salt) => HollowmereStreets.H(a, b, salt);

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            T = t; scarecrowDone = false; AllotmentCam = null; counts.Clear(); trunkXZ.Clear(); eyeCams.Clear(); decals = 0; prefabs.Clear(); unit.Clear();
            foreach (var c in HollowmereBuilder.Cameras) if (c.Name.StartsWith("Cap_HM_Eye_", System.StringComparison.Ordinal) && !c.Name.Contains("JettyBack")) eyeCams.Add(new Vector2(c.Pos.x, c.Pos.z));
            Physics.SyncTransforms();
            Transform wild = set.Find("Wild") ?? set;
            Transform treeRoot = Sub(wild, "Orchard");
            Transform g = Sub(ruined, "Orchard");
            Trees_(treeRoot);
            Physics.SyncTransforms();
            Transform props = Sub(g, "Props"), flora = Sub(g, "Flora"), dec = Sub(g, "Decals");
            FallenTree(props, flora, dec);
            Physics.SyncTransforms();
            TreeBases(props, flora, dec);
            Windfalls(props);
            Story(props, flora, dec);
            Nooks(props, flora, dec);
            Path(flora, dec);
            WallFeet(props, flora, dec);
            WallGap(ruined, props, flora, dec);
            FillLight(g);
            EyeClear(ruined, props, flora);
            PathFeather(props, flora);
            Allotments(Sub(ruined, "Allotments"));
            SolidPier(ruined, "R2_22");
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(l => l.type != LightType.Directional);
            Debug.Log("SC HOLLOWMERE orchard: " + string.Join(", ", counts.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)) + ", decals=" + decals + ", non-directional lights in the scene=" + lights);
        }

        static Transform Sub(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ------------------------------------------------------------------------------------------------ helpers
        static GameObject Prefab(string code)
        {
            if (prefabs.TryGetValue(code, out GameObject p)) return p;
            string path = code.StartsWith("TR", System.StringComparison.Ordinal) ? TreesFolder + code + "/" + code + ".prefab"
                : code == "N04" ? Ad + "N04_stump/N04.prefab" : code == "N05" ? Ad + "N05_fallen_log/N05.prefab" : code == "N03" ? Ad + "N03_scree_cluster/N03.prefab" : code == "N02" ? Ad + "N02_angular_rock/N02.prefab"
                : (code.Length > 2 && code[0] == 'C' && char.IsDigit(code[1])) ? Cel + code + "/" + code + ".prefab" : Ad + code + "/" + code + ".prefab";
            return prefabs[code] = HollowmereStreets.LoadPrefab(path);
        }

        static Bounds Render(GameObject go)
        {
            Bounds b = default; bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds(go.transform.position, Vector3.one * 0.3f);
        }

        static Bounds Unit(GameObject pf)
        {
            if (unit.TryGetValue(pf, out Bounds b)) return b;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tmp.transform.localScale = Vector3.one;
            LODGroup lod = tmp.GetComponent<LODGroup>(); bool any = false; b = default;
            Renderer[] rs = lod != null && lod.lodCount > 0 ? lod.GetLODs()[0].renderers : tmp.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rs) { if (r == null) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Object.DestroyImmediate(tmp);
            return unit[pf] = any ? b : new Bounds(Vector3.up * 0.3f, Vector3.one * 0.6f);
        }

        static float Gy(Vector2 p) { HollowmereStreets.Surface(p.x, p.y, out float y, out _); return y; }

        // Ground-blend step: SCPlacementGrounding on every direct child of `group` whose name starts with one of `prefixes` (hierarchy order, so two builds agree). Pieces that find no support stay put.
        static void GroundChildren(Transform group, string[] prefixes, float sink, int minHits)
        {
            int ok = 0, skipped = 0;
            foreach (Transform c in group)
            {
                bool match = false;
                foreach (string p in prefixes) if (c.name.StartsWith(p, System.StringComparison.Ordinal)) { match = true; break; }
                if (!match) continue;
                if (SCPlacementGrounding.TryGroundAndSink(c, null, sink, 55f, false, minHits)) ok++; else skipped++;
            }
            Count("groundSunk", ok); Count("groundSkipped", skipped);
        }

        // A prefab scaled so its largest dimension is `maxDim`, turned (Euler: Z, then X, then yaw), centred on xz and standing on y (sunk by `sinkM`); colliders stripped, a box when solid.
        static GameObject Put(Transform parent, string code, string name, Vector2 xz, float y, float maxDim, float yaw, float tx, float tz, float sinkM, bool solid = false)
        {
            GameObject pf = Prefab(code);
            if (pf == null) { Count("missing_" + code); return null; }
            Bounds ub = Unit(pf);
            float s = maxDim / Mathf.Max(0.02f, Mathf.Max(ub.size.x, Mathf.Max(ub.size.y, ub.size.z)));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.name = name;
            go.transform.localScale = Vector3.one * s;
            go.transform.SetPositionAndRotation(new Vector3(xz.x, y, xz.y), Quaternion.Euler(tx, yaw, tz));
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Bounds b = Render(go);
            go.transform.position += new Vector3(xz.x - b.center.x, y - sinkM - b.min.y, xz.y - b.center.z);
            if (solid) { var bc = go.AddComponent<BoxCollider>(); bc.center = ub.center; bc.size = ub.size * 0.9f; Physics.SyncTransforms(); Count("solidProps"); }
            Count("props");
            return go;
        }

        // A thin prop that lies along local +X when unturned (ladder, rake, shovel, scythe, plank) leaning against something: foot at `foot`, top toward `dir` (unit xz), phi degrees off vertical.
        static GameObject Lean(Transform parent, string code, string name, Vector2 foot, Vector2 dir, float length, float phi, bool flip, float sinkM)
        {
            GameObject pf = Prefab(code);
            if (pf == null) { Count("missing_" + code); return null; }
            float reach = length * Mathf.Sin(phi * Mathf.Deg2Rad);
            Vector2 d = flip ? -dir : dir;
            float yaw = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
            Vector2 centre = foot + dir * (reach * 0.5f);   // top end toward dir in both cases: the flip only turns the object's own head end
            GameObject go = Put(parent, code, name, centre, Gy(foot), length, flip ? yaw : yaw, 0f, flip ? -(90f - phi) : (90f - phi), sinkM);
            return go;
        }

        static void Weed(Transform flora, Vector2 p, int key, int salt, float s0 = 0.9f, float s1 = 1.5f)
        {
            string[] kits = { "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E", "Grass_A", "Grass_B", "Grass_C" };
            string kit = kits[(int)(H(key, salt, 701) * kits.Length) % kits.Length];
            float y = Gy(p);
            GameObject go = HollowmereFoliage.Tuft(flora, kit, new Vector3(p.x, y - 0.02f, p.y), H(key, salt, 702) * 360f, Mathf.Lerp(s0, s1, H(key, salt, 703)));
            if (go != null) Count("tufts");
        }

        static Material Variant(Material src, string name, Color tint)
        {
            if (src == null) return null;
            string path = MatFolder + "/M_HM_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            m.shader = src.shader; m.CopyPropertiesFromMaterial(src);
            if (src.HasProperty("_BaseColor")) { Color c = src.GetColor("_BaseColor"); var nc = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a); m.SetColor("_BaseColor", nc); if (m.HasProperty("_Color")) m.SetColor("_Color", nc); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Recolor(GameObject go, string suffix, Color tint, string only = null)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && (only == null || mats[i].name.Contains(only))) { mats[i] = Variant(mats[i], mats[i].name.Replace(" (Instance)", "") + "_" + suffix, tint); ch = true; }
                if (ch) r.sharedMaterials = mats;
            }
        }

        // -------- decals (the materials T06 made, plus the T17d hay pile)
        static readonly Dictionary<string, Material> decalMats = new Dictionary<string, Material>();
        static Material DM(string n)
        {
            if (decalMats.TryGetValue(n, out Material m)) return m;
            if (n == "Hay")
            {
                var imp = AssetImporter.GetAtPath(HayTex) as TextureImporter;
                if (imp != null && (imp.wrapMode != TextureWrapMode.Clamp || !imp.alphaIsTransparency || !imp.sRGBTexture || imp.maxTextureSize != 1024))
                { imp.wrapMode = TextureWrapMode.Clamp; imp.alphaIsTransparency = true; imp.sRGBTexture = true; imp.maxTextureSize = 1024; imp.SaveAndReimport(); }
                Shader sh = Shader.Find("Shader Graphs/Decal");
                m = LookTestBuilder.Ensure(MatFolder + "/M_ST_Decal_Hay.mat", sh);
                m.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(HayTex)); m.SetFloat("Normal_Blend", 0f);
                EditorUtility.SetDirty(m);
            }
            else m = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_ST_Decal_" + n + ".mat");
            if (m == null) Debug.LogWarning("SC HOLLOWMERE orchard: decal material missing: " + n);
            return decalMats[n] = m;
        }

        static void Decal(Transform parent, string mat, Vector2 p, float w, float h, float yaw, string name)
        {
            if (HollowmereDecalThin.Drop(name)) { thinned++; return; }
            Material m = DM(mat); if (m == null) return;
            float y = Gy(p);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(p.x, y + 0.5f, p.y), Quaternion.Euler(90f, yaw, 0f));
            var dp = go.AddComponent<DecalProjector>();
            dp.material = m; dp.size = new Vector3(w, h, 1.5f); dp.pivot = new Vector3(0f, 0f, 0.75f);
            dp.drawDistance = 70f; dp.fadeScale = 0.8f; dp.startAngleFade = 45f; dp.endAngleFade = 75f; dp.fadeFactor = 1f;
            decals++; Count("decal" + mat);
        }

        static bool Ok(Vector2 p, float rad, bool flatOnly = true, bool free = true)
        {
            if (!HollowmereStreets.Surface(p.x, p.y, out float y, out float ny)) return false;
            if (flatOnly && ny < 0.86f) return false;
            if (Mathf.Abs(y - HollowmereLayout.Ground(T, p.x, p.y)) > 0.35f) return false;
            var tp = new TownPoint(p.x, p.y);
            foreach (Foundation f in T.Foundations) if (f.Footprint.Contains(tp, 0.1f)) return false;
            foreach (Landmark l in T.Landmarks) if (l.Footprint.Contains(tp, 0.3f)) return false;
            foreach (Vector2 c in eyeCams) if (Vector2.Distance(c, p) < 1.3f) return false;
            return !free || HollowmereStreets.FreeOf(p, y, rad);
        }

        static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        static Vector2 Perp(Vector2 d) => new Vector2(-d.y, d.x);
        static float YawAlong(Vector2 d) => Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;   // local +X turned along d
        static float YawFace(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;     // local +Z turned along d

        // ------------------------------------------------------------------------------------------------ trees (both presets)
        sealed class MB
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> I = new List<int>();
            public Mesh ToMesh(string name) { var m = new Mesh { name = name }; m.indexFormat = V.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16; m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(I, 0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); return m; }
            public int Add(Vector3 v) { V.Add(v); UV.Add(new Vector2(v.x, v.z) / 1.5f); return V.Count - 1; }
        }

        static void Mound(MB mb, Vector2 p, float rad, float peak, int seed)
        {
            const int segs = 12, rings = 2;
            int b0 = mb.Add(new Vector3(p.x, Gy(p) + peak, p.y));
            for (int r = 1; r <= rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    float u = r / (float)rings, a = s * Mathf.PI * 2f / segs;
                    float rr = rad * u * (1f + 0.25f * (H(seed, s, 811) - 0.5f));
                    var q = new Vector2(p.x + Mathf.Cos(a) * rr, p.y + Mathf.Sin(a) * rr);
                    mb.Add(new Vector3(q.x, Gy(q) + (r == rings ? 0.015f : peak * 0.66f), q.y));
                }
            for (int s = 0; s < segs; s++) { mb.I.Add(b0); mb.I.Add(b0 + 1 + (s + 1) % segs); mb.I.Add(b0 + 1 + s); }
            for (int r = 1; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int a = b0 + 1 + (r - 1) * segs + s, b = b0 + 1 + (r - 1) * segs + (s + 1) % segs, c = a + segs, d = b + segs;
                    mb.I.Add(a); mb.I.Add(b); mb.I.Add(c); mb.I.Add(b); mb.I.Add(d); mb.I.Add(c);
                }
        }

        // A torn-root pit seen from above: a raised berm of spoil round a hollow (the hollow is the ground itself, darkened by decals).
        static void Berm(MB mb, Vector2 p, float rin, float rout, float h, int seed)
        {
            const int segs = 16;
            int i0 = mb.V.Count;
            for (int s = 0; s < segs; s++)
            {
                float a = s * Mathf.PI * 2f / segs, w = 1f + 0.3f * (H(seed, s, 821) - 0.5f), hh = h * (0.6f + 0.8f * H(seed, s, 822));
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 q0 = p + d * rin * w, q1 = p + d * (rin + (rout - rin) * 0.35f) * w, q2 = p + d * rout * w;
                mb.Add(new Vector3(q0.x, Gy(q0) + 0.01f, q0.y)); mb.Add(new Vector3(q1.x, Gy(q1) + hh, q1.y)); mb.Add(new Vector3(q2.x, Gy(q2) + 0.01f, q2.y));
            }
            for (int s = 0; s < segs; s++)
            {
                int n = (s + 1) % segs;
                for (int k = 0; k < 2; k++)
                {
                    int a = i0 + s * 3 + k, b = i0 + n * 3 + k, c = a + 1, d = b + 1;
                    mb.I.Add(a); mb.I.Add(c); mb.I.Add(b); mb.I.Add(b); mb.I.Add(c); mb.I.Add(d);
                }
            }
        }

        static Material Soil() => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_KT_Soil.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_GD_packed_soil.mat");

        static void Trees_(Transform root)
        {
            var mb = new MB();
            Material leafVar2 = null, leafVar3 = null;
            Material L(string code) => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/Trees/M_" + code + "_Leaves.mat");
            leafVar2 = Variant(L("TR02"), "AppleLeaf_TR02", new Color(1.3f, 1.27f, 0.72f)); leafVar3 = Variant(L("TR03"), "AppleLeaf_TR03", new Color(1.3f, 1.27f, 0.72f));
            for (int i = 0; i < Trees.Length; i++)
            {
                TreeDef d = Trees[i];
                var p = new Vector2(d.X, d.Z);
                GameObject pf = Prefab(d.Code); if (pf == null) { Count("treeMissing"); continue; }
                if (!HollowmereStreets.Surface(p.x, p.y, out float y, out float ny) || !HollowmereStreets.FreeOf(p, y, 0.6f)) { Count("treeRejected"); var hs = Physics.OverlapSphere(new Vector3(p.x, y + 0.75f, p.y), 0.6f, ~0, QueryTriggerInteraction.Ignore).Where(c => !(c is TerrainCollider)).Select(c => c.transform.root.name + "/" + c.transform.parent?.name + "/" + c.name); Debug.LogWarning("SC HOLLOWMERE orchard: apple tree " + i + " rejected at " + p + " hit " + string.Join(";", hs)); continue; }
                Bounds ub = Unit(pf); float s = d.H / Mathf.Max(0.1f, ub.max.y);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root);
                go.name = "Apple_" + i + "_" + d.Code;
                go.transform.position = new Vector3(p.x, y - 0.04f, p.y);
                go.transform.rotation = Quaternion.Euler((H(i, 1, 831) - 0.5f) * 10f, d.Yaw, (H(i, 2, 831) - 0.5f) * 10f);
                go.transform.localScale = new Vector3(s * d.Girth, s, s * d.Girth);
                Material lv = d.Code == "TR02" ? leafVar2 : leafVar3;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials; bool ch = false;
                    for (int k = 0; k < mats.Length; k++) if (mats[k] != null && mats[k].name.Contains("Leaves") && lv != null) { mats[k] = lv; ch = true; }
                    if (ch) r.sharedMaterials = mats;
                }
                trunkXZ.Add(p); Count("appleTrees");
                Mound(mb, p, Mathf.Lerp(1.2f, 1.7f, H(i, 3, 831)), 0.17f, i);
            }
            var mo = new GameObject("OrchardMounds"); mo.transform.SetParent(root, false);
            mo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("OR_Mounds");
            var mr = mo.AddComponent<MeshRenderer>(); mr.sharedMaterial = Soil(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
        }

        // ------------------------------------------------------------------------------------------------ the fallen tree (Ruined)
        static void FallenTree(Transform props, Transform flora, Transform dec)
        {
            var mb = new MB();
            Berm(mb, FallenRoot, 0.9f, 2.0f, 0.3f, 91);
            var mo = new GameObject("FallenTreePit"); mo.transform.SetParent(props, false);
            mo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("OR_FallenPit");
            var mr = mo.AddComponent<MeshRenderer>(); mr.sharedMaterial = Soil(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            GameObject pf = Prefab("TR02"); if (pf == null) return;
            Bounds ub = Unit(pf); float s = 5.6f / Mathf.Max(0.1f, ub.max.y);
            Vector2 axis = (FallenCrown - FallenRoot).normalized;
            float y = Gy(FallenRoot);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, props);
            go.name = "FallenApple";
            Vector3 up = new Vector3(axis.x, 0.14f, axis.y).normalized;   // the trunk lies 8 degrees up from the ground toward the crown
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, 30f, 0f);
            go.transform.localScale = new Vector3(s * 1.2f, s, s * 1.2f);
            go.transform.position = new Vector3(FallenRoot.x, y + 0.28f, FallenRoot.y);   // the root end stands on the pit's lip, the crown settles into the grass
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var cap = go.AddComponent<CapsuleCollider>(); cap.direction = 1; cap.radius = 0.5f; cap.height = 8.5f; cap.center = new Vector3(0f, 4.4f, 0f);   // the trunk to the first forks
            Material lv = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_HM_AppleLeaf_TR02.mat");
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) { var mats = r.sharedMaterials; for (int k = 0; k < mats.Length; k++) if (mats[k] != null && mats[k].name.Contains("Leaves") && lv != null) mats[k] = lv; r.sharedMaterials = mats; }
            Physics.SyncTransforms(); Count("fallenTree"); Count("solidProps");
            // root wad and spoil: the N04 stump on its side in the pit, stones, the pit and litter decals
            Put(props, "N04", "FallenRootWad", FallenRoot + new Vector2(-0.3f, 0.2f), y, 1.7f, 40f, 28f, 20f, 0.15f);
            for (int i = 0; i < 3; i++)
            {
                float a = (0.7f + i * 2.1f) * 1f; Vector2 q = FallenRoot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(1.5f, 2.3f, H(i, 1, 841));
                if (Ok(q, 0.3f)) Put(props, "T11a", "PitStone_" + i, q, Gy(q), Mathf.Lerp(0.45f, 0.8f, H(i, 2, 841)), H(i, 3, 841) * 360f, (H(i, 4, 841) - 0.5f) * 14f, (H(i, 5, 841) - 0.5f) * 14f, 0.12f);
            }
            Decal(dec, "Mud", FallenRoot, 3.4f, 3.4f, 20f, "Pit_Mud"); Decal(dec, "Damp", FallenRoot + new Vector2(0.2f, -0.2f), 2.2f, 2.2f, 110f, "Pit_Damp");
            for (int i = 0; i < 6; i++) { float a = i * 1.05f; Weed(flora, FallenRoot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(1.9f, 2.7f, H(i, 6, 841)), 91, i, 1.0f, 1.7f); }
            for (int i = 0; i < 3; i++)
            {
                Vector2 q = Vector2.Lerp(FallenRoot, FallenCrown, 0.35f + 0.25f * i) + Perp(axis) * (i % 2 == 0 ? 0.9f : -0.8f);
                Decal(dec, i % 2 == 0 ? "LeavesA" : "LeavesB", q, 2.6f, 2.6f, H(i, 7, 841) * 360f, "Fallen_Litter" + i);
            }
        }

        // ------------------------------------------------------------------------------------------------ tree bases (Ruined): stones, grass ring, leaf litter, moss
        static void TreeBases(Transform props, Transform flora, Transform dec)
        {
            for (int i = 0; i < trunkXZ.Count; i++)
            {
                Vector2 p = trunkXZ[i]; int n = 1 + (int)(H(i, 1, 851) * 2.99f);
                for (int k = 0; k < n; k++)
                {
                    float a = (H(i, 2 + k, 851) + k / (float)n) * Mathf.PI * 2f; Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(0.9f, 1.4f, H(i, 5 + k, 851));
                    if (Ok(q, 0.3f)) Put(props, "T11a", "BaseStone_" + i + "_" + k, q, Gy(q), Mathf.Lerp(0.3f, 0.55f, H(i, 8 + k, 851)), H(i, 11 + k, 851) * 360f, (H(i, 14 + k, 851) - 0.5f) * 20f, (H(i, 17 + k, 851) - 0.5f) * 20f, 0.06f);
                }
                for (int k = 0; k < 7; k++)
                {
                    float a = (k + H(i, 20 + k, 851)) * Mathf.PI * 2f / 7f; Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(0.8f, 1.9f, H(i, 30 + k, 851));
                    if (!OnPath(q.x, q.y)) Weed(flora, q, i, 40 + k, 0.9f, 1.5f);
                }
                Decal(dec, i % 2 == 0 ? "LeavesA" : "LeavesB", p + new Vector2(0.3f, -0.2f), 3.6f, 3.6f, H(i, 50, 851) * 360f, "Litter_" + i + "a");
                Decal(dec, i % 2 == 0 ? "LeavesB" : "LeavesA", p + new Vector2(-1.2f, 1.0f), 2.6f, 2.6f, H(i, 51, 851) * 360f, "Litter_" + i + "b");
                Decal(dec, "MossSoft", p, 2.2f, 2.2f, H(i, 52, 851) * 360f, "TrunkMoss_" + i);
            }
        }

        // ------------------------------------------------------------------------------------------------ windfalls: 20, clustered
        static GameObject Apple(Transform props, Vector2 p, int key, int salt, bool rotten, float y0 = float.NaN)
        {
            float y = float.IsNaN(y0) ? Gy(p) : y0;
            GameObject a = Put(props, "T08d", (rotten ? "AppleRot_" : "Apple_") + key + "_" + salt, p, y, Mathf.Lerp(0.075f, 0.095f, H(key, salt, 861)), H(key, salt, 862) * 360f, (H(key, salt, 863) - 0.5f) * 40f, (H(key, salt, 864) - 0.5f) * 40f, float.IsNaN(y0) ? 0.018f : 0f);
            if (a != null && rotten) Recolor(a, "Rotten", new Color(0.42f, 0.32f, 0.22f));
            return a;
        }

        static void Cluster(Transform props, Vector2 centre, float spread, int count, int key, int rotten, ref int made)
        {
            for (int i = 0; i < count; i++)
            {
                float a = H(key, i, 871) * Mathf.PI * 2f, r = spread * Mathf.Sqrt(H(key, i, 872));
                Vector2 q = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (OnPathLine(q.x, q.y) || !Ok(q, 0.1f)) continue;
                if (Apple(props, q, key, i, i < rotten) != null) made++;
            }
        }

        static void Windfalls(Transform props)
        {
            Transform g = Sub(props, "Windfalls"); int made = 0;
            Cluster(g, new Vector2(Trees[0].X + 1.1f, Trees[0].Z - 0.5f), 1.3f, 6, 1, 1, ref made);
            Cluster(g, new Vector2(Trees[2].X - 0.9f, Trees[2].Z - 0.8f), 1.2f, 6, 2, 1, ref made);
            Cluster(g, new Vector2(Trees[0].X + 0.3f, Trees[0].Z + 1.9f), 1.0f, 4, 3, 0, ref made);
            Cluster(g, Vector2.Lerp(FallenRoot, FallenCrown, 0.5f) + new Vector2(-0.9f, 0f), 2.4f, 6, 4, 2, ref made);
            Cluster(g, new Vector2(Trees[3].X + 0.8f, Trees[3].Z + 0.9f), 0.7f, 2, 5, 2, ref made);
            Count("windfalls", made);
        }

        // ------------------------------------------------------------------------------------------------ story props
        static void Story(Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "Story"); int key = 7001;
            // the ladder against a trunk of tree 5 and a full basket at its foot
            {
                Vector2 tp = trunkXZ.Count > 0 ? trunkXZ[0] : new Vector2(Trees[0].X, Trees[0].Z);
                Vector2 toTree = new Vector2(-0.35f, -0.94f).normalized;   // T07b: the ladder stands on the camera side of the harvest tree, leaning south onto the trunk
                float phi = 13f, L = 2.7f, reach = L * Mathf.Sin(phi * Mathf.Deg2Rad);
                Vector2 foot = tp - toTree * (reach + 0.27f);
                if (Ok(foot, 0.2f, false, false)) { Lean(g, "T04b", "Ladder", foot, toTree, L, phi, false, 0.02f); Decal(dec, "Hay", foot + toTree * 0.1f, 1.7f, 1.7f, 35f, "Hay_Ladder"); }
                Vector2 bp = foot - toTree * 0.5f + Perp(toTree) * 0.6f;
                if (Ok(bp, 0.2f, true, false))
                {
                    float by = Gy(bp);
                    Put(g, "T08g", "BasketFull", bp, by, 0.5f, 20f, 0f, 0f, 0.03f);
                    for (int i = 0; i < 8; i++) { float a = i * 2.4f; Vector2 q = bp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i < 5 ? 0.10f : 0.03f); Apple(g, q, 11, i, i == 6, by + (i < 5 ? 0.275f : 0.335f)); }
                }
            }
            // a tipped basket with apples spilled downhill (toward the wall) under tree 4
            {
                Vector2 tp = new Vector2(Trees[0].X, Trees[0].Z), d = new Vector2(-0.3f, 0.95f).normalized;   // T07b: east of the harvest trunk, apples spilling toward the lane
                Vector2 bp = HarvestSpot + new Vector2(1.55f, -0.2f);
                if (Ok(bp, 0.25f))
                {
                    float by = Gy(bp);
                    Put(g, "T08g", "BasketTipped", bp, by, 0.5f, YawFace(d), 72f, 0f, 0.03f);
                    Decal(dec, "Hay", bp + d * 0.5f, 1.4f, 1.4f, 200f, "Hay_Basket");
                    for (int i = 0; i < 9; i++)
                    {
                        float t = 0.35f + 1.3f * Mathf.Pow(H(key, i, 881), 0.8f); float lat = (H(key, i, 882) - 0.5f) * (0.3f + 0.55f * t);
                        Vector2 q = bp + d * t + Perp(d) * lat;
                        if (Ok(q, 0.1f)) Apple(g, q, 12, i, i == 7);
                    }
                }
            }
            // the half-loaded wheelbarrow parked on the garden wall's orchard face, with a sack and a crate stack beside it
            {
                Vector2 wallPt = new Vector2(43.0f, -41.3f), n = new Vector2(0.81f, -0.58f), t = new Vector2(0.58f, 0.81f);
                Vector2 wp = wallPt + n * 0.95f;
                if (Ok(wp, 0.5f, false, false))
                {
                    float wy = Gy(wp);
                    Put(g, "T16c", "Wheelbarrow", wp, wy, 1.55f, YawAlong(t) + 8f, 0f, 3f, 0.03f, true);
                    Decal(dec, "Hay", wp - t * 0.5f, 1.6f, 1.6f, 80f, "Hay_Barrow");
                    for (int i = 0; i < 7; i++) { Vector2 q = wp + t * ((H(key, i, 891) - 0.5f) * 0.55f) + n * ((H(key, i, 892) - 0.5f) * 0.25f) + t * 0.08f; Apple(g, q, 13, i, i == 5, wy + 0.32f + 0.04f * (i % 3)); }
                }
                Vector2 sp = wallPt + t * -1.2f + n * 0.5f;
                if (Ok(sp, 0.3f)) Put(g, "C11b", "SackLeaning", sp, Gy(sp), 0.7f, YawFace(-n), 6f, 4f, 0.04f);
            }
            // crate stack, a tipped crate with spilled apples, a broken-open crate: along the wall east of the gate
            {
                Vector2 wallPt = new Vector2(42.0f, -42.95f), n = new Vector2(0.81f, -0.58f), t = new Vector2(0.58f, 0.81f);
                Vector2 cp = wallPt + n * 0.45f;
                if (Ok(cp, 0.4f, false, false))
                {
                    float cy = Gy(cp);
                    Put(g, "T17a", "CrateBase", cp, cy, 0.58f, YawAlong(t) + 4f, 0f, 0f, 0.03f, true);
                    Put(g, "T17b", "CrateTop", cp + t * 0.03f, cy + 0.22f, 0.6f, YawAlong(t) - 9f, 0f, 2f, 0f);
                    Decal(dec, "Hay", cp + n * 0.2f, 1.8f, 1.8f, 120f, "Hay_Crates");
                }
                Vector2 tp = wallPt - t * 1.15f + n * 0.7f;
                if (Ok(tp, 0.3f))
                {
                    float ty = Gy(tp);
                    Put(g, "T17a", "CrateTipped", tp, ty, 0.55f, YawAlong(t) + 40f, 0f, 78f, 0.02f);
                    for (int i = 0; i < 4; i++) { Vector2 q = tp + n * (0.3f + 0.25f * i) + Perp(n) * ((H(key, i, 895) - 0.5f) * 0.4f); if (Ok(q, 0.1f)) Apple(g, q, 14, i, i == 3); }
                }
            }
            // T07b: a crate stack and a sack at the harvest tree's west side, in the frame's mid-ground
            {
                Vector2 tp = new Vector2(Trees[0].X, Trees[0].Z), t = new Vector2(0.9f, -0.44f);
                Vector2 cp = HarvestSpot;
                if (Ok(cp, 0.35f, false, false))
                {
                    float cy = Gy(cp);
                    Put(g, "T17a", "HarvestCrateBase", cp, cy, 0.58f, YawAlong(t) + 12f, 0f, 0f, 0.03f, true);
                    Put(g, "T17b", "HarvestCrateTop", cp + t * 0.04f, cy + 0.22f, 0.6f, YawAlong(t) - 14f, 0f, 2f, 0f);
                    Put(g, "T17a", "HarvestCrateSide", cp + new Vector2(-0.45f, 0.15f), Gy(cp), 0.55f, 40f, 0f, 78f, 0.02f);
                    Decal(dec, "Hay", cp + new Vector2(0.1f, 0.2f), 1.8f, 1.8f, 160f, "Hay_Harvest");
                    for (int i = 0; i < 4; i++) { Vector2 q = cp + new Vector2(-0.7f - 0.2f * i, 0.5f - 0.15f * i); if (Ok(q, 0.1f)) Apple(g, q, 16, i, i == 3); }
                }
                Vector2 sp = HarvestSpot + new Vector2(0.95f, 0.75f); if (Ok(sp, 0.3f, false, false)) Put(g, "C11b", "HarvestSack", sp, Gy(sp), 0.7f, 120f, 6f, 4f, 0.04f);
            }
            // tools leaning on the gate pier (orchard face)
            {
                Vector2 centre = PierB + OrchardNormal * 0.62f; Vector2 u = new Vector2(0.84f, 0.54f);
                string[] tools = { "T16k", "T16l", "T16m" }; float[] len = { 1.5f, 1.25f, 1.45f };
                for (int i = 0; i < 3; i++)
                {
                    Vector2 foot = centre + u * (-0.26f + 0.26f * i);
                    if (Ok(foot, 0.1f, false)) Lean(g, tools[i], "GateTool_" + tools[i], foot, -OrchardNormal, len[i], 20f + 3f * i, i == 1, 0.02f);
                }
                Vector2 bkp = centre + u * 0.85f + OrchardNormal * 0.1f;
                if (Ok(bkp, 0.2f)) Put(g, "T17c", "GateBucket", bkp, Gy(bkp), 0.34f, 40f, 0f, 6f, 0.025f);
                Decal(dec, "Hay", centre + u * 0.2f, 1.2f, 1.2f, 300f, "Hay_Gate");
            }
            // the bench under the best crown
            {
                Vector2 tp = trunkXZ.Count > 2 ? trunkXZ[2] : new Vector2(Trees[2].X, Trees[2].Z), f = new Vector2(-0.95f, 0.3f).normalized;
                Vector2 bp = tp + f * 1.0f;
                if (Ok(bp, 0.4f, false, false))
                {
                    Put(g, "T16e", "Bench", bp, Gy(bp), 1.4f, YawFace(f), 0f, 3f, 0.04f, true);
                    Decal(dec, "LeavesB", bp + f * 0.3f, 2.6f, 2.6f, 15f, "Litter_Bench");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ two nooks
        static void Nooks(Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "Nooks");
            // A: the corner of R2_18's shell and the garden wall's end, under the seventh crown: ferns, a mossy stone, a basket half under ivy
            Vector2 a = new Vector2(34.4f, -50.6f);
            Vector2[] fern = { a + new Vector2(-0.2f, -0.9f), a + new Vector2(0.9f, 0.7f), a + new Vector2(0.7f, -1.1f), a + new Vector2(-0.1f, 1.0f) };
            float[] fs = { 0.95f, 0.75f, 0.85f, 0.7f };
            for (int i = 0; i < fern.Length; i++) if (Ok(fern[i], 0.3f, false)) Put(g, "T12b", "NookFern_A" + i, fern[i], Gy(fern[i]), fs[i], H(i, 1, 901) * 360f, 0f, 0f, 0.03f);
            Vector2 sp = a + new Vector2(0.2f, 0.3f); if (Ok(sp, 0.3f, false)) Put(g, "T11a", "NookStone_A", sp, Gy(sp), 0.75f, 35f, 4f, -5f, 0.15f);
            Vector2 bp = a + new Vector2(0.55f, -0.35f);
            if (Ok(bp, 0.25f, false)) { Put(g, "T08g", "NookBasket_A", bp, Gy(bp), 0.46f, 150f, 25f, 6f, 0.03f); Put(g, "T12c", "NookIvy_A", bp + new Vector2(-0.25f, 0.25f), Gy(bp), 0.95f, 215f, 0f, 3f, 0.0f); }
            Vector2 cp = a + new Vector2(-0.5f, 0.4f); if (Ok(cp, 0.3f, false)) Put(g, "T17a", "NookCrate_A", cp, Gy(cp), 0.5f, 70f, 0f, 76f, 0.02f);
            Decal(dec, "Hay", a + new Vector2(0.2f, -0.4f), 1.6f, 1.6f, 200f, "Hay_NookA"); Decal(dec, "Moss", a + new Vector2(-0.4f, 0.2f), 1.8f, 1.8f, 40f, "Moss_NookA"); Decal(dec, "LeavesA", a, 2.8f, 2.8f, 260f, "Litter_NookA");
            for (int i = 0; i < 4; i++) Weed(flora, a + new Vector2(Mathf.Cos(i * 1.6f), Mathf.Sin(i * 1.6f)) * 1.3f, 901, i, 1.0f, 1.6f);
            // B: the pocket behind the fallen tree, between its trunk and the crumbled wall
            Vector2 b = new Vector2(49.3f, -49.6f);
            Vector2[] fern2 = { b + new Vector2(0.2f, -1.2f), b + new Vector2(1.1f, 0.9f), b + new Vector2(-0.9f, 1.4f) };
            for (int i = 0; i < fern2.Length; i++) if (Ok(fern2[i], 0.3f, false)) Put(g, "T12b", "NookFern_B" + i, fern2[i], Gy(fern2[i]), 0.9f - 0.1f * i, H(i, 2, 901) * 360f, 0f, 0f, 0.03f);
            Vector2 s1 = b + new Vector2(-0.6f, 0.1f); if (Ok(s1, 0.3f, false)) Put(g, "T11a", "NookStone_B1", s1, Gy(s1), 0.8f, 100f, 3f, 5f, 0.14f);
            Vector2 s2 = b + new Vector2(0.9f, -0.2f); if (Ok(s2, 0.3f, false)) Put(g, "T11a", "NookStone_B2", s2, Gy(s2), 0.5f, 10f, -4f, 4f, 0.1f);
            Vector2 cb = b + new Vector2(0.2f, 0.2f);
            if (Ok(cb, 0.3f, false))
            {
                Put(g, "T17b", "NookCrateBroken", cb, Gy(cb), 0.58f, 25f, 0f, 62f, 0.02f);
                for (int i = 0; i < 4; i++) { Vector2 q = cb + new Vector2(0.3f + 0.2f * i, -0.15f * i + 0.1f); if (Ok(q, 0.1f)) Apple(g, q, 15, i, i >= 2); }
            }
            Vector2 lg = b + new Vector2(-1.3f, -1.0f); if (Ok(lg, 0.5f, false)) Put(g, "T18e", "NookLog_B", lg, Gy(lg), 1.9f, 60f, 2f, 3f, 0.08f);
            Vector2 r1 = b + new Vector2(0.4f, 1.5f); if (Ok(r1, 0.4f, false)) Put(g, "T05b", "NookRubble_B", r1, Gy(r1), 0.9f, 130f, 4f, -6f, 0.1f);
            Decal(dec, "Hay", b + new Vector2(0.5f, 0.0f), 1.7f, 1.7f, 330f, "Hay_NookB"); Decal(dec, "Damp", b, 2.4f, 2.4f, 90f, "Damp_NookB"); Decal(dec, "LeavesB", b + new Vector2(-0.3f, 0.6f), 2.6f, 2.6f, 10f, "Litter_NookB");
            for (int i = 0; i < 4; i++) Weed(flora, b + new Vector2(Mathf.Cos(i * 1.7f + 0.5f), Mathf.Sin(i * 1.7f + 0.5f)) * 1.5f, 902, i, 1.0f, 1.6f);
        }

        // ------------------------------------------------------------------------------------------------ the path (Ruined decals; the grass strip is cleared in the terrain mask)
        static void Path(Transform flora, Transform dec)
        {
            for (int i = 1; i < PathLine.Length; i++)
            {
                Vector2 a = PathLine[i - 1], b = PathLine[i]; float len = Vector2.Distance(a, b); Vector2 d = (b - a) / len;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.8f));
                for (int k = 0; k < n; k++)
                {
                    Vector2 q = a + d * ((k + 0.5f) * len / n) + Perp(d) * ((H(i, k, 911) - 0.5f) * 0.25f);
                    Decal(dec, "Mud", q, 2.0f + 0.5f * H(i, k + 20, 911), 1.5f + 0.4f * H(i, k + 40, 911), YawAlong(d) + (H(i, k, 912) - 0.5f) * 40f, "PathMud_" + i + "_" + k);
                }
            }
            Decal(dec, "Puddle", PathLine[2] + new Vector2(0.15f, 0.2f), 1.2f, 0.9f, 60f, "PathPuddle");
            // grass and weeds invading the edge from both sides
            for (int i = 1; i < PathLine.Length; i++)
            {
                Vector2 a = PathLine[i - 1], b = PathLine[i]; float len = Vector2.Distance(a, b); Vector2 d = (b - a) / len;
                for (int k = 0; k < 4; k++)
                {
                    float s = (k + H(i, k, 921)) * len / 4f; float side = (k % 2 == 0 ? 1f : -1f);
                    Vector2 q = a + d * s + Perp(d) * side * Mathf.Lerp(0.75f, 1.15f, H(i, k + 9, 921));
                    if (Ok(q, 0.1f)) Weed(flora, q, 921, i * 10 + k, 0.8f, 1.3f);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ wall feet: the garden wall, its gate piers and the town wall in the orchard stretch
        static void WallFeet(Transform props, Transform flora, Transform dec)
        {
            // garden wall, orchard side: tufts in the joints and at the pier feet, moss, damp and grime
            var runs = new (Vector2 A, Vector2 B, Vector2 N)[] { (new Vector2(35.9f, -47.2f), new Vector2(38.9f, -45.2f), new Vector2(0.55f, -0.83f)), (new Vector2(41.1f, -43.8f), new Vector2(44.1f, -39.6f), new Vector2(0.81f, -0.58f)) };
            int ri = 0;
            foreach (var r in runs)
            {
                float len = Vector2.Distance(r.A, r.B); Vector2 d = (r.B - r.A) / len;
                int nt = Mathf.RoundToInt(len / 0.9f);
                for (int k = 0; k < nt; k++) { Vector2 q = r.A + d * ((k + 0.5f) * len / nt) + r.N * Mathf.Lerp(0.3f, 0.6f, H(ri, k, 931)); if (H(ri, k, 932) < 0.85f && Ok(q, 0.05f, false)) Weed(flora, q, 931 + ri, k, 0.6f, 1.2f); }
                for (int k = 0; k < 3; k++)
                {
                    float s = (k + 0.5f) * len / 3f; Vector2 q = r.A + d * s + r.N * 0.4f;
                    string m = k == 0 ? "Grime" : k == 1 ? "Moss" : "Damp";
                    Decal(dec, m, m == "Grime" ? q + r.N * 0.15f : q, 1.8f, m == "Grime" ? 1.0f : 1.5f, m == "Grime" ? YawFace(-r.N) + 180f : H(ri, k, 933) * 360f, "WallFoot_" + ri + "_" + k);
                }
                ri++;
            }
            foreach (Vector2 pier in new[] { PierA, PierB })
            {
                Vector2 f = pier + OrchardNormal * 0.55f;
                for (int k = 0; k < 4; k++) { Vector2 q = f + Dir(k * 90f + 30f) * 0.5f; if (Ok(q, 0.05f, false)) Weed(flora, q, 941 + (pier == PierA ? 0 : 1), k, 0.7f, 1.2f); }
                if (pier == PierA)
                {   // T07b: the pier in the foreground of Eye_Orchard stands on the lane side: ivy up its face, a fern and tufts at its foot
                    Vector2 lf = pier - OrchardNormal * 0.65f;
                    if (Ok(lf, 0.2f, false, false)) { Put(props, "T12c", "PierIvy", lf, Gy(lf), 1.7f, YawFace(OrchardNormal) + 180f, 0f, 3f, 0f); Put(props, "T12b", "PierFern", lf + new Vector2(-0.5f, 0.1f), Gy(lf), 0.8f, 40f, 0f, 0f, 0.03f); }
                    for (int k = 0; k < 4; k++) { Vector2 q = lf + Dir(k * 90f + 50f) * 0.55f; if (Ok(q, 0.05f, false, false)) Weed(flora, q, 947, k, 0.8f, 1.3f); }
                    Decal(dec, "MossSoft", lf, 1.8f, 1.8f, 90f, "PierMossLane");
                }
                Decal(dec, "MossSoft", f, 1.6f, 1.6f, pier == PierA ? 30f : 200f, "PierMoss" + (pier == PierA ? "A" : "B")); Decal(dec, "Grime", f + OrchardNormal * 0.1f, 1.5f, 0.9f, YawFace(-OrchardNormal) + 180f, "PierGrime" + (pier == PierA ? "A" : "B"));
            }
            // town wall inner face (the arc of radius 38 about (24,-26), inner face 37.35) from x 34 to 51
            Vector2 centre = new Vector2(24f, -26f);
            for (int k = 0; k < 11; k++)
            {
                float th = Mathf.Lerp(-74.5f, -45f, (k + 0.5f) / 11f) * Mathf.Deg2Rad;
                Vector2 dirOut = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                Vector2 q = centre + dirOut * 36.95f;
                string m = k % 3 == 0 ? "Grime" : k % 3 == 1 ? "Moss" : "Damp";
                Decal(dec, m, q, 2.4f, m == "Grime" ? 1.1f : 1.8f, m == "Grime" ? YawFace(dirOut) : H(k, 1, 951) * 360f, "TownWallFoot_" + k);
            }
            for (int k = 0; k < 22; k++)
            {
                float th = Mathf.Lerp(-74.5f, -45f, (k + H(k, 2, 952)) / 22f) * Mathf.Deg2Rad;
                Vector2 q = centre + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * Mathf.Lerp(36.6f, 37.0f, H(k, 3, 952));
                if (Ok(q, 0.05f, false)) Weed(flora, q, 952, k, 0.7f, 1.4f);
            }
        }

        // ------------------------------------------------------------------------------------------------ the wall gap: rubble spill 1-2 m out on both sides, ferns and grass at its edges
        static void WallGap(Transform ruined, Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "WallGap");
            Vector2 c = new Vector2(44.2f, -57.4f), n = new Vector2(0.51f, -0.86f), t = new Vector2(0.86f, 0.51f);   // 40-42: breach at (43.4,-58.6), crumbled at (46.7,-56.4) and (49.7,-53.9)
            // T07b: the wall fell years ago. The breach's own modules give the courses; the spill is real scanned rubble (T05b/T05c/N03/T11a) stepping down from the wall foot in a
            // heap that buries the module's base, with grass, ferns and moss through it. Larger pieces near the wall, smaller and sparser out into the grass.
            int made = 0;
            made += BreachDressing(ruined, g, flora, dec, c, n, t);   // Pass A: scanned heap with a low route, replaces GapRubble/GapHeap
            // the ground east of the breach (the bare cracked plane): rubble, ferns, tufts, litter
            for (int k = 0; k < 7; k++) { Vector2 q = c + t * (3.2f + 0.7f * k) - n * (0.6f + 2.2f * H(k, 16, 969)); if (Ok(q, 0.2f, false, false)) { Put(g, k % 2 == 0 ? "T05c" : "T19k", "GapEast_" + k, q, Gy(q), Mathf.Lerp(0.5f, 1.2f, H(k, 17, 969)), H(k, 18, 969) * 360f, (H(k, 19, 969) - 0.5f) * 20f, (H(k, 20, 969) - 0.5f) * 20f, 0.15f); made++; } }
            for (int k = 0; k < 12; k++) { Vector2 q = c + t * (2.6f + 0.45f * k) - n * (0.5f + 2.6f * H(k, 21, 969)); if (Ok(q, 0.05f, false, false)) Weed(flora, q, 969, k, 0.9f, 1.6f); }
            for (int k = 0; k < 4; k++) { Vector2 q = c + t * (3.5f + 1.1f * k) - n * (1.0f + 1.5f * H(k, 22, 969)); if (Ok(q, 0.3f, false, false)) Put(g, "T12b", "GapFernE_" + k, q, Gy(q), 0.75f, H(k, 23, 969) * 360f, 0f, 0f, 0.03f); }
            Decal(dec, "LeavesB", c + t * 4.2f - n * 1.6f, 3.2f, 3.2f, 30f, "GapLitterE"); Decal(dec, "MossSoft", c + t * 3.4f - n * 0.9f, 2.6f, 2.6f, 80f, "GapMossE"); Decal(dec, "Damp", c + t * 5.0f - n * 1.3f, 3.0f, 2.0f, 20f, "GapDampE");
            // T07c: the flagged ledge of the breach module west of the gap (the right of the frame) stood bare: moss, litter and damp over it, small stones, ferns and tufts through it
            Decal(dec, "MossSoft", new Vector2(40.2f, -58.7f), 3.4f, 3.0f, 20f, "GapMossW"); Decal(dec, "LeavesB", new Vector2(39.0f, -58.6f), 3.4f, 3.0f, 110f, "GapLitterW");
            Decal(dec, "Damp", new Vector2(41.2f, -58.6f), 3.0f, 2.0f, 60f, "GapDampW"); Decal(dec, "LeavesA", new Vector2(38.2f, -59.2f), 3.0f, 3.0f, 200f, "GapLitterW2"); Decal(dec, "Moss", new Vector2(37.6f, -58.0f), 2.6f, 2.6f, 75f, "GapMossW2");
            for (int k = 0; k < 16; k++) { Vector2 q = new Vector2(36.8f + k * 0.34f + (H(k, 51, 970) - 0.5f) * 0.3f, -58.9f + (H(k, 52, 970) - 0.5f) * 1.2f); Weed(flora, q, 970, k, 0.9f, 1.5f); }
            for (int k = 0; k < 5; k++) { Vector2 q = new Vector2(37.6f + k * 0.95f, -58.6f + (H(k, 53, 970) - 0.5f) * 0.8f); Put(g, "T12b", "GapFernW_" + k, q, Gy(q), 0.85f, H(k, 54, 970) * 360f, 0f, 0f, 0.03f); }
            for (int k = 0; k < 6; k++) { Vector2 q = new Vector2(37.4f + k * 0.8f, -58.4f + (H(k, 55, 970) - 0.5f) * 0.9f); Put(g, k % 2 == 0 ? "T05c" : "T19k", "GapStoneW_" + k, q, Gy(q), 0.45f + 0.35f * H(k, 56, 970), H(k, 57, 970) * 360f, (H(k, 58, 970) - 0.5f) * 12f, (H(k, 59, 970) - 0.5f) * 12f, 0.06f); made++; }
            // ground-blend step: the breach rubble sits IN the ground (five-point footprint on the terrain, sunk 20% of its height); a piece with no support is left as Put placed it
            GroundChildren(g, new[] { "GapRubble_", "GapEast_", "GapStoneW_" }, 0.20f, 3);
            Count("gapRubble", made);
            for (int k = 0; k < 10; k++) { Vector2 q = c + t * (k * 0.95f - 3.3f) - n * (1.0f + 2.0f * H(k, 4, 965)); if (Ok(q, 0.3f, false, false)) Put(g, "T12b", "GapFern_" + k, q, Gy(q), 0.65f + 0.35f * H(k, 1, 965), H(k, 2, 965) * 360f, 0f, 0f, 0.03f); }
            for (int k = 0; k < 26; k++) { Vector2 q = c + t * (k * 0.42f - 3.5f) - n * Mathf.Lerp(0.2f, 3.6f, H(k, 2, 966)); if (Ok(q, 0.05f, false, false)) Weed(flora, q, 966, k, 0.9f, 1.6f); }
            for (int k = 0; k < 4; k++) { Vector2 q = c + t * (k * 2.1f - 2.8f) - n * (1.0f + 1.4f * H(k, 5, 967)); if (Ok(q, 0.2f, false, false)) Put(g, "T12c", "GapIvy_" + k, q, Gy(q), 1.1f, H(k, 6, 967) * 360f, 0f, 3f, 0f); }
            Decal(dec, "Damp", c - n * 1.0f, 4.0f, 3.0f, YawAlong(t), "GapDamp"); Decal(dec, "Moss", c - n * 1.8f + t * 2f, 2.6f, 2.6f, 40f, "GapMoss"); Decal(dec, "Moss", c - n * 1.2f - t * 2.2f, 2.4f, 2.4f, 130f, "GapMoss2");
            Decal(dec, "LeavesA", c - n * 3.0f, 3.0f, 3.0f, 70f, "GapLitterA"); Decal(dec, "LeavesB", c - n * 2.6f + t * 3f, 3.0f, 3.0f, 10f, "GapLitterB"); Decal(dec, "LeavesA", c - n * 3.4f - t * 2f, 2.6f, 2.6f, 200f, "GapLitterC");
            for (int k = 0; k < 4; k++) { Vector2 q = c - n * (2.2f + 0.6f * k) + t * (k * 1.5f - 2f); if (Ok(q, 0.1f, false, false)) Apple(g, q, 17, k, k == 2); }
        }

        // ------------------------------------------------------------------------------------------------ T07c: clear the Orchard eye line, a solid pier for the allotment view
        static Transform FindByName(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // The lane clutter's broken handcart stood 4.9 m in front of Eye_Orchard on the harvest cluster's bearing (a 1.1 m prop hides ground 5-16 m behind it from a 1.65 m eye).
        // It moves to the nook at the frame's right edge, still lying in the grass, half cut by the frame.
        static void EyeClear(Transform ruined, Transform props, Transform flora)
        {
            Transform cart = FindByName(ruined, "Clutter_Clutter_Ru_Wreck1_Handcart");
            if (cart == null) { Count("cartMissing"); return; }
            // Pass A: the procedural handcart (blocky planks) goes. Scanned wood stands in its place off the lens axis at the frame's right edge: barrel, crates, sack, a log and a plank, with ferns and plants.
            Object.DestroyImmediate(cart.gameObject);
            Physics.SyncTransforms();
            Transform g = Sub(props, "EyeCluster");
            Vector2 anchor = new Vector2(33.5f, -49.2f);
            foreach (Vector2 to in new[] { new Vector2(33.5f, -49.2f), new Vector2(33.3f, -49.8f), new Vector2(33.9f, -48.6f), new Vector2(33.7f, -50.4f) })
                if (Ok(to, 0.2f, false, false)) { anchor = to; break; }
            string[] codes = { "T20t", "T27l", "T20r", "T17b", "T06a" };
            float[] sizes = { 0.95f, 0.6f, 1.5f, 0.55f, 0.9f };
            for (int k = 0; k < codes.Length; k++)
            {
                Vector2 q = anchor + Dir(H(k, 1, 1401) * 360f) * Mathf.Lerp(0.35f, 1.5f, H(k, 2, 1401));
                if (!Ok(q, 0.2f, false, false)) continue;
                bool lying = codes[k] == "T20r";
                Put(g, codes[k], "EyeCluster_" + k, q, Gy(q), sizes[k], H(k, 3, 1401) * 360f, lying ? (H(k, 4, 1401) - 0.5f) * 8f : (H(k, 4, 1401) - 0.5f) * 14f, (H(k, 5, 1401) - 0.5f) * 10f, sizes[k] * 0.1f, false);
            }
            for (int k = 0; k < 6; k++)
            {
                Vector2 q = anchor + Dir(H(k, 6, 1401) * 360f) * Mathf.Lerp(0.5f, 1.9f, H(k, 7, 1401));
                if (!Ok(q, 0.05f, false, false)) continue;
                Put(g, k % 2 == 0 ? "T12b" : "T21m", "EyeClusterPlant_" + k, q, Gy(q), Mathf.Lerp(0.5f, 0.9f, H(k, 8, 1401)), H(k, 9, 1401) * 360f, 0f, 0f, 0.04f);
            }
            GroundChildren(g, new[] { "EyeCluster_" }, 0.18f, 3);
            Count("cartReplaced");
        }

        // Pass A: the grass-to-path edge was a razor line (OnPath). A band either side of the ragged edge gets scanned small plants, stones and sprawling leaf plants so the transition reads as worn earth
        // working into the lawn. Weeds first on the path side, plants and stones on the grass side; never on the cleared strip itself.
        static void PathFeather(Transform props, Transform flora)
        {
            Transform g = Sub(props, "PathFeather");
            string[] plants = { "T21e", "T21g", "T21i", "T19u", "T19w", "T21k", "T21m", "T12b" };
            string[] stones = { "N03", "T20i", "T05c", "T11b" };
            int made = 0;
            for (int i = 1; i < PathLine.Length; i++)
            {
                Vector2 a = PathLine[i - 1], b = PathLine[i]; float len = Vector2.Distance(a, b); Vector2 dir = (b - a) / len, nr = Perp(dir);
                for (float s = 0f; s < len; s += 0.4f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int key = i * 1000 + (int)(s * 10f) * 2 + (side > 0 ? 1 : 0);
                        if (H(key, 1, 1411) > 0.62f) continue;
                        Vector2 pc = a + dir * s;
                        float hw = Mathf.Lerp(0.5f, 0.95f, Mathf.PerlinNoise(pc.x * 1.9f + 3f, pc.y * 1.9f + 9f));
                        float off = hw + Mathf.Lerp(-0.3f, 0.65f, H(key, 2, 1411));
                        Vector2 q = pc + nr * (side * off) + dir * ((H(key, 3, 1411) - 0.5f) * 0.4f);
                        if (!Ok(q, 0.05f, false, false)) continue;
                        float r = H(key, 4, 1411);
                        if (r < 0.2f) Put(g, stones[key % stones.Length], "PathFeather_S" + key, q, Gy(q), Mathf.Lerp(0.12f, 0.3f, H(key, 5, 1411)), H(key, 6, 1411) * 360f, (H(key, 7, 1411) - 0.5f) * 20f, (H(key, 8, 1411) - 0.5f) * 20f, 0.03f);
                        else Put(g, plants[(int)(H(key, 9, 1411) * plants.Length) % plants.Length], "PathFeather_P" + key, q, Gy(q), Mathf.Lerp(0.22f, 0.5f, H(key, 10, 1411)), H(key, 11, 1411) * 360f, 0f, 0f, 0.02f);
                        made++;
                    }
                }
            }
            GroundChildren(g, new[] { "PathFeather_S" }, 0.2f, 1);
            Count("pathFeather", made);
        }

        // R2_22's chimney stack (the pillar of Eye_Allotment): the shell kit's mesh has gaps between its courses and the dressing's loose corbels float over the top, so sky shows
        // between the blocks. Its renderers go; a solid stack of touching courses in the same stone stands in its place (the kit's own collider stays).
        static void SolidPier(Transform ruined, string lotId)
        {
            Transform lot = null;
            foreach (Transform t in ruined.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(lotId + "_", System.StringComparison.Ordinal) && t.parent != null && t.parent.name == "Foundations") { lot = t; break; }
            if (lot == null) { Count("pierLotMissing"); return; }
            Transform chim = FindByName(lot, "Chimney");
            if (chim == null) { Count("pierMissing"); return; }
            Material stone = null; Bounds b = default; bool any = false;
            foreach (Renderer r in chim.GetComponentsInChildren<Renderer>(true)) { if (stone == null) stone = r.sharedMaterial; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); r.enabled = false; }
            Transform dress = lot.Find("Dress");
            if (dress != null) foreach (Transform c in dress.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Corbel_", System.StringComparison.Ordinal)).ToArray()) Object.DestroyImmediate(c.gameObject);
            if (stone == null || !any) { Count("pierNoStone"); return; }
            var root = new GameObject("SolidPier").transform; root.SetParent(chim.parent, true);
            float[] hs = { 0.46f, 0.42f, 0.40f, 0.38f, 0.36f }, ws = { 1.04f, 1.0f, 0.96f, 0.9f, 0.82f };
            float y = Gy(new Vector2(b.center.x, b.center.z)) - 0.06f;
            for (int i = 0; i < hs.Length; i++)
            {
                float jx = (H(i, 1, 1101) - 0.5f) * 0.06f, jz = (H(i, 2, 1101) - 0.5f) * 0.06f;
                Vector3 size = new Vector3(ws[i] * Mathf.Lerp(0.96f, 1.04f, H(i, 3, 1101)), hs[i], ws[i] * 0.94f * Mathf.Lerp(0.96f, 1.04f, H(i, 4, 1101)));
                var go = LookTestBuilder.Box(root, "Course_" + i, Vector3.zero, size, stone, default, 2f);
                go.transform.position = new Vector3(b.center.x + jx, y + hs.Take(i).Sum() * 0.99f + hs[i] * 0.5f, b.center.z + jz);
                go.transform.rotation = Quaternion.Euler((H(i, 5, 1101) - 0.5f) * 2f, (H(i, 6, 1101) - 0.5f) * 10f, (H(i, 7, 1101) - 0.5f) * 2f);
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            // the broken top: one slanted slab on half the last course, one fallen block at the foot
            float topY = y + hs.Sum() * 0.99f;
            var cap = LookTestBuilder.Box(root, "BrokenCap", Vector3.zero, new Vector3(0.5f, 0.24f, 0.56f), stone, default, 2f);
            cap.transform.position = new Vector3(b.center.x + 0.18f, topY + 0.1f, b.center.z - 0.1f); cap.transform.rotation = Quaternion.Euler(8f, 25f, -12f);
            var fall = LookTestBuilder.Box(root, "FallenBlock", Vector3.zero, new Vector3(0.55f, 0.34f, 0.5f), stone, default, 2f);
            fall.transform.position = new Vector3(b.center.x + 0.95f, y + 0.15f, b.center.z + 0.35f); fall.transform.rotation = Quaternion.Euler(4f, 60f, 9f);
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Count("solidPier");
        }

        // ------------------------------------------------------------------------------------------------ one shadowless fill under the crowns
        static void FillLight(Transform g)
        {
            var go = new GameObject("OrchardFill"); go.transform.SetParent(g, false);
            go.transform.position = new Vector3(39.4f, Gy(new Vector2(39.4f, -53.3f)) + 2.7f, -53.3f);   // T07b: over the harvest cluster
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.86f, 0.93f, 0.72f); l.intensity = 3.2f; l.range = 12f; l.shadows = LightShadows.None;
            Count("lights");
        }

        // ------------------------------------------------------------------------------------------------ the allotments (D8)
        sealed class Plot
        {
            public Foundation F; public Vector2 O, Fw, Rt; public float Depth = 4.2f, Half = 3.3f; public int Key; public int Score;
            public Vector2 W(float a, float d) => O + Rt * a + Fw * d;
        }

        static bool PlotOk(Vector2 p, float rad)
        {
            if (!Ok(p, rad)) return false;
            if (HollowmereStreets.PathDistance(p) < 1.0f) return false;
            var tp = new TownPoint(p.x, p.y);
            foreach (Lane l in T.Lanes) if (HollowmereLayout.DistanceToPolyline(tp, l.Points) - l.Width * 0.5f < 0.5f) return false;
            return true;
        }

        static Plot ChoosePlot(Foundation f)
        {
            TownPoint ax = f.Footprint.AxisX, az = f.Footprint.AxisZ;
            Vector2[] fws = { new Vector2(az.X, az.Z), new Vector2(ax.X, ax.Z), new Vector2(-az.X, -az.Z), new Vector2(-ax.X, -ax.Z) };
            Plot best = null;
            for (int k = 0; k < 4; k++)
            {
                var p = new Plot { F = f, Fw = fws[k], Rt = new Vector2(fws[k].y, -fws[k].x), Key = HollowmereStreets.StableHash(f.Id) };
                p.O = new Vector2(f.Centre.X, f.Centre.Z) + p.Fw * (f.Side * 0.5f + 0.35f);
                int score = 0;
                for (float d = 0.5f; d <= 3.8f; d += 0.9f) for (float a = -3.0f; a <= 3.0f; a += 0.9f) if (PlotOk(p.W(a, d), 0.3f)) score++;
                p.Score = score;
                if (best == null || score > best.Score) best = p;
            }
            return best;
        }

        static void Allotments(Transform root)
        {
            var built = new List<Plot>();
            foreach (string id in new[] { "R2_07", "R2_18", "R2_22", "R3_02" })
            {
                Foundation f = T.Foundations.Find(x => x.Id == id); if (f == null) continue;
                Plot p = ChoosePlot(f);
                Transform g = Sub(root, id);
                Debug.Log("SC HOLLOWMERE allotment " + id + ": plot score " + p.Score + " facing (" + p.Fw.x.ToString("F2") + "," + p.Fw.y.ToString("F2") + ") origin " + p.O);
                Count("plots");
                if (p.Score < 4) { Count("plotsEmpty"); continue; }
                Fence(g, p); Beds(g, p, id); Cluster(g, p, id);
                built.Add(p);
            }
            AllotmentCamera(built);
        }

        // T07b: Cap_HM_Eye_Allotment, 1.65 m: the plot (of those built) with the clearest line to its beds from a spot 3.6-6 m out on free ground.
        static void AllotmentCamera(List<Plot> built)
        {
            var big = new List<Bounds>();
            foreach (Renderer rr in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) { Bounds bb = rr.bounds; if (bb.size.y > 1.8f) big.Add(bb); }
            float bestScore = -1f;
            foreach (Plot p in built)
            {
                Vector2 tg = p.W(0f, 1.8f); float gyT = Gy(tg) + 0.8f; Vector3 tgv = new Vector3(tg.x, gyT, tg.y);
                for (int a = 0; a < 16; a++)
                    foreach (float dist in new[] { 6.0f, 5.2f, 4.4f, 3.6f })
                    {
                        Vector2 cand = tg + Dir(a * 22.5f) * dist; var eye = new Vector3(cand.x, Gy(cand) + 1.65f, cand.y);
                        if (Physics.CheckSphere(eye, 0.45f, ~0, QueryTriggerInteraction.Ignore)) { Count("camSphere"); continue; }
                        Ray er = new Ray(eye, (tgv - eye).normalized); float el = Vector3.Distance(eye, tgv), clear = el; bool inside = false;
                        foreach (Bounds bb in big)
                        {
                            if (Mathf.Abs(bb.center.x - tg.x) > 20f || Mathf.Abs(bb.center.z - tg.y) > 20f) continue;
                            var ex = bb; ex.Expand(0.4f); if (ex.Contains(eye)) { inside = true; break; }
                            if (bb.IntersectRay(er, out float hd) && hd < clear) clear = hd;
                        }
                        if (inside) { Count("camInside"); continue; }
                        if (Physics.Linecast(eye, tgv, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && !(hit.collider is TerrainCollider)) clear = Mathf.Min(clear, hit.distance);
                        float score = clear / el * 10f + dist * 0.1f;
                        if (score > bestScore) { bestScore = score; AllotmentCam = (eye, tgv); }
                    }
            }
            if (bestScore < 0f) Count("allotCamFailed");
        }

        static void Fence(Transform g, Plot p)
        {
            var spots = new List<(Vector2 pos, Vector2 along, int slot)>();
            for (float a = -p.Half + 0.5f; a <= p.Half - 0.4f; a += 1.0f) spots.Add((p.W(a, p.Depth), p.Rt, spots.Count));
            for (float d = p.Depth - 1.0f; d >= 0.5f; d -= 1.0f) { spots.Add((p.W(-p.Half, d), p.Fw, spots.Count)); spots.Add((p.W(p.Half, d), p.Fw, spots.Count)); }
            for (int i = 0; i < spots.Count; i++) spots[i] = (spots[i].pos + Perp(spots[i].along) * ((H(p.Key, spots[i].slot, 978) - 0.5f) * 0.40f), spots[i].along, spots[i].slot);   // T07c: posts wander off the line
            int gateSlot = spots.Count - 2;   // a side piece near the lot: the gate that hangs off one hinge
            foreach (var s in spots)
            {
                float r = H(p.Key, s.slot, 971);
                if (r < 0.16f) { Count("fenceMissing"); continue; }
                if (!PlotOk(s.pos, 0.25f)) { Count("fenceRejected"); continue; }
                float y = Gy(s.pos);
                float yaw = YawAlong(s.along) + (H(p.Key, s.slot, 972) - 0.5f) * 28f;
                if (s.slot == gateSlot)
                {
                    Vector2 hinge = s.pos - s.along * 0.5f; Vector2 outN = Perp(s.along) * (Vector2.Dot(Perp(s.along), p.W(0, p.Depth * 0.5f) - s.pos) > 0f ? -1f : 1f);
                    Vector2 centre = hinge + (s.along * Mathf.Cos(0.5f) + outN * Mathf.Sin(0.5f)) * 0.52f;
                    Put(g, "T16f", "FenceGate", centre, y, 1.22f, YawAlong((s.along * Mathf.Cos(0.5f) + outN * Mathf.Sin(0.5f))), 0f, -24f, 0.03f);
                    Count("fenceGate"); continue;
                }
                if (r < 0.36f) { Put(g, "T16f", "FenceFallen_" + s.slot, s.pos, y, 1.22f, yaw, (H(p.Key, s.slot, 973) < 0.5f ? 1f : -1f) * (80f + 8f * H(p.Key, s.slot, 974)), 0f, 0.02f); Count("fenceFallen"); }
                else { Put(g, "T16f", "Fence_" + s.slot, s.pos, y, 1.22f, yaw, (H(p.Key, s.slot, 973) - 0.5f) * (6f + 24f * H(p.Key, s.slot, 975)), (H(p.Key, s.slot, 976) - 0.5f) * 5f, 0.03f); Count("fenceStanding"); }
                if (H(p.Key, s.slot, 977) < 0.55f) Wild(g, s.pos + Perp(s.along) * 0.2f, p.Key, 100 + s.slot, 0.8f);
            }
        }

        static void Beds(Transform g, Plot p, string id)
        {
            bool replanted = id == "R2_18";
            (float a, float d, float len)[] beds = { (-1.45f, 1.25f, 2.3f), (1.45f, 1.25f, 2.3f), (0f, 2.95f, 3.1f) };
            for (int i = 0; i < beds.Length; i++)
            {
                var b = beds[i]; Vector2 c = p.W(b.a, b.d);
                if (!PlotOk(c, 0.55f)) { Count("bedRejected"); continue; }
                Bed(g, p, c, p.Rt, b.len, replanted && i == 0, p.Key + i * 31);
            }
            // a bean trellis built of planks on the back edge (two plots), a scarecrow toppled on R2_07
            if (id == "R2_18" || id == "R2_22" || id == "R3_02") Trellis(g, p.W(id == "R2_22" ? -1.7f : 1.9f, p.Depth - 0.55f), p.Rt, p.Key);
            if ((id == "R2_07" || id == "R3_02") && !scarecrowDone) Scarecrow(g, p);
            Sunflowers(g, p);
        }

        static bool scarecrowDone;
        internal static (Vector3 Pos, Vector3 Target)? AllotmentCam;
        // T07b: the first free spot inside the plot (T07's single spot failed placement): toppled and weathered, hay round it.
        static void Scarecrow(Transform g, Plot p)
        {
            float[] aa = { 2.0f, -2.2f, 0f, 2.5f, -2.6f, 1.0f, -1.0f }, dd = { 3.2f, 2.2f, 0.8f, 3.6f, 1.2f, 3.0f };
            foreach (float d in dd)
                foreach (float a in aa)
                {
                    Vector2 sp = p.W(a, d);
                    if (!Ok(sp, 0.35f, false)) continue;
                    GameObject s = HollowmereGarden.Loose(g, "Scarecrow", new Vector3(sp.x, Gy(sp), sp.y), 40f);
                    if (s == null) continue;
                    foreach (Collider c in s.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    s.transform.rotation = Quaternion.Euler(78f, 55f, 12f); s.transform.position = new Vector3(sp.x, Gy(sp) + 0.24f, sp.y);
                    Recolor(s, "Worn", new Color(0.62f, 0.58f, 0.52f));
                    Decal(g, "Hay", sp, 2.4f, 2.4f, 25f, "Hay_Scarecrow"); Count("scarecrow"); scarecrowDone = true;
                    return;
                }
            Count("scarecrowFailed");
        }

        static void Bed(Transform g, Plot p, Vector2 c, Vector2 along, float len, bool replanted, int key)
        {
            Vector2 perp = Perp(along); float y = Gy(c), yaw = YawAlong(along);
            Decal(g, replanted ? "Damp" : "Mud", c, len * 0.95f, 1.0f, yaw, "BedSoil_" + key);
            // frame: two long planks and two short ones, tidy on the replanted bed, tilted and shifted on the others
            for (int k = 0; k < 4; k++)
            {
                bool longSide = k < 2; float sgn = (k % 2 == 0) ? 1f : -1f;
                Vector2 pos = longSide ? c + perp * sgn * 0.52f : c + along * sgn * (len * 0.5f);
                float jitter = replanted ? 0f : 1f;
                if (!replanted && k == 3 && H(key, 1, 981) < 0.7f) pos += along * 0.35f + perp * 0.3f;   // one plank has slipped
                Put(g, "T05d", "BedPlank_" + key + "_" + k, pos, y, longSide ? len : 1.05f, longSide ? yaw : yaw + 90f, jitter * (H(key, k, 982) - 0.5f) * 8f, jitter * (H(key, k, 983) - 0.5f) * 6f, 0.03f);
            }
            if (replanted)
            {
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                    Put(g, "T08a", "Seedling_" + key + "_" + i + j, c + along * ((i - 1) * 0.62f) + perp * ((j - 1) * 0.28f), y, 0.17f, H(key, i * 3 + j, 984) * 360f, 0f, 0f, 0.015f);
                Vector2 bk = c + along * (len * 0.5f + 0.5f) + perp * 0.1f; if (PlotOk(bk, 0.2f)) Put(g, "T17c", "ReplantBucket", bk, Gy(bk), 0.34f, 25f, 0f, 3f, 0.025f);
                Vector2 sh = c + along * (len * 0.5f + 0.8f) - perp * 0.5f; if (PlotOk(sh, 0.15f)) Lean(g, "T16l", "ReplantShovel", sh, along * -1f, 1.25f, 62f, false, 0.02f);
                Count("bedReplanted");
                return;
            }
            // gone to seed: split cabbages, rotting pumpkins, a few potatoes, weeds over the frame
            for (int i = 0; i < 3; i++)
            {
                Vector2 q = c + along * ((i - 1) * len * 0.3f + (H(key, i, 985) - 0.5f) * 0.25f) + perp * ((H(key, i + 5, 985) - 0.5f) * 0.35f);
                bool split = i == 1;
                Put(g, "T08a", "CabbageSeed_" + key + "_" + i, q, y, split ? 0.38f : 0.3f, H(key, i, 986) * 360f, split ? 18f : 0f, split ? 14f : 0f, 0.03f);
            }
            for (int i = 0; i < 2; i++)
            {
                Vector2 q = c + along * ((i == 0 ? -1f : 1f) * len * 0.42f) + perp * ((H(key, i, 987) - 0.5f) * 0.5f) + (i == 1 ? perp * 0.7f : Vector2.zero);
                GameObject pm = Put(g, "T08b", "PumpkinRot_" + key + "_" + i, q, y, 0.34f, H(key, i, 988) * 360f, (H(key, i, 989) - 0.5f) * 16f, (H(key, i, 990) - 0.5f) * 16f, 0.04f);
                Recolor(pm, "Rot", new Color(0.36f, 0.30f, 0.22f));
            }
            for (int i = 0; i < 4; i++) { Vector2 q = c + along * ((H(key, i, 991) - 0.5f) * len * 0.6f) + perp * ((H(key, i + 9, 991) - 0.5f) * 0.6f); Put(g, "T08c", "Potato_" + key + "_" + i, q, y, 0.1f, H(key, i, 992) * 360f, 0f, 0f, 0.015f); }
            for (int i = 0; i < 7; i++) { Vector2 q = c + along * ((H(key, i, 993) - 0.5f) * len * 0.95f) + perp * (Mathf.Sign(H(key, i, 994) - 0.5f) * Mathf.Lerp(0.2f, 0.75f, H(key, i, 995))); Wild(g, q, key, i, 1f); }   // T07c: bolted vegetables and allotment weeds, not spiky clumps
            Count("bedBolted");
        }

        // T07c: allotment growth in the plots (T19u sowthistle, T19v hemp nettle, T19w yellow flower, T19x white windflower) instead of the spiky grass clumps.
        static readonly string[] AllotKinds = { "T19u", "T19v", "T19w", "T19x", "T19v", "T19w", "T19u" };
        static readonly float[] AllotSize = { 1.35f, 0.42f, 0.32f, 0.95f, 0.45f, 0.34f, 1.1f };
        static void Wild(Transform g, Vector2 p, int key, int salt, float scale)
        {
            int k = (int)(H(key, salt, 1201) * AllotKinds.Length) % AllotKinds.Length;
            Put(g, AllotKinds[k], "Allot_" + AllotKinds[k] + "_" + salt, p, Gy(p), AllotSize[k] * Mathf.Lerp(0.8f, 1.2f, H(key, salt, 1202)) * scale, H(key, salt, 1203) * 360f, 0f, 0f, 0.02f);
            Count("allotPlants");
        }

        // A leaning row of old sunflowers (T19y) along the back edge of the plot, all gone over the same way.
        static void Sunflowers(Transform g, Plot p)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2 q = p.W(-2.6f + i * 1.3f + (H(p.Key, i, 1211) - 0.5f) * 0.5f, p.Depth - 0.25f - H(p.Key, i, 1212) * 0.3f);
                if (!PlotOk(q, 0.2f)) continue;
                Put(g, "T19y", "Sunflower_" + i, q, Gy(q), 1.3f * Mathf.Lerp(0.85f, 1.15f, H(p.Key, i, 1213)), YawAlong(p.Rt) + (H(p.Key, i, 1214) - 0.5f) * 24f, 0f, 12f + 12f * H(p.Key, i, 1215), 0.03f);
                Count("sunflowers");
            }
        }

        static void Trellis(Transform g, Vector2 b, Vector2 along, int key)
        {
            if (!PlotOk(b, 0.4f)) { Count("trellisRejected"); return; }
            float y = Gy(b), yaw = YawAlong(along); Vector2 face = Perp(along);
            for (int i = 0; i < 2; i++)
            {
                Vector2 q = b + along * (i == 0 ? -0.75f : 0.75f);
                Put(g, "T05d", "TrellisPost_" + i, q, y, 1.9f, yaw + 90f, (H(key, i, 996) - 0.5f) * 7f, 90f, 0.04f, false);
            }
            Put(g, "T04a", "TrellisBarTop", b, y + 1.62f, 1.7f, yaw, 0f, (H(key, 3, 996) - 0.5f) * 6f, 0f);
            Put(g, "T04a", "TrellisBarMid", b + face * 0.02f, y + 0.95f, 1.6f, yaw + 3f, 0f, 2f, 0f);
            Put(g, "T12c", "TrellisVines_0", b + face * 0.06f - along * 0.2f, y + 0.1f, 1.25f, yaw, 0f, 0f, 0f);
            Put(g, "T12c", "TrellisVines_1", b - face * 0.06f + along * 0.3f, y + 0.4f, 1.0f, yaw + 180f, 0f, 0f, 0f);
            Count("trellis");
        }

        // water butt with a bucket against the lot's wall, tools where work stopped, hay and crates; the wheelbarrow on R2_22
        static void Cluster(Transform g, Plot p, string id)
        {
            Vector2 bp = p.W(-p.Half + 0.8f, 0.7f);
            if (PlotOk(bp, 0.45f))
            {
                float y = Gy(bp);
                Put(g, "T16d", "WaterButt", bp, y, 0.95f, H(p.Key, 1, 1001) * 360f, 0f, 2f, 0.04f, true);
                Vector2 bk = bp + new Vector2(0.55f, -0.2f); if (PlotOk(bk, 0.2f)) Put(g, "T17c", "ButtBucket", bk, Gy(bk), 0.34f, 140f, 0f, 4f, 0.025f);
                Decal(g, "Moss", bp, 1.6f, 1.6f, 70f, "ButtMoss"); Decal(g, "Damp", bp + new Vector2(0.1f, 0.1f), 2.0f, 2.0f, 10f, "ButtDamp");
                for (int i = 0; i < 4; i++) Wild(g, bp + Dir(i * 90f + 20f) * 0.55f, p.Key, 200 + i, 0.8f);
            }
            Vector2 tp = p.W(0.8f, p.Depth - 0.5f);
            if (PlotOk(tp, 0.15f))
            {
                Lean(g, "T16k", "PlotRake", tp, p.Fw, 1.5f, 19f, false, 0.02f);
                Vector2 t2 = tp + p.Rt * 0.3f; if (PlotOk(t2, 0.1f)) Lean(g, "T16m", "PlotScythe", t2, p.Fw, 1.45f, 24f, true, 0.02f);
            }
            Vector2 hp = p.W(p.Half - 0.9f, 0.9f);
            if (id != "R2_22" && PlotOk(hp, 0.4f))
            {
                float y = Gy(hp);
                Put(g, "T17a", "PlotCrate_A", hp, y, 0.56f, H(p.Key, 2, 1001) * 90f, 0f, 0f, 0.03f, true);
                Put(g, "T17b", "PlotCrate_B", hp + p.Rt * 0.05f, y + 0.22f, 0.58f, H(p.Key, 3, 1001) * 90f + 20f, 0f, 3f, 0f);
                Decal(g, "Hay", hp, 1.8f, 1.8f, 50f, "Hay_Crates");
            }
            if (id == "R2_22")
            {
                Vector2 wp = p.W(2.0f, 3.5f);
                if (PlotOk(wp, 0.5f)) { Put(g, "T16c", "PlotWheelbarrow", wp, Gy(wp), 1.55f, YawAlong(p.Rt) + 25f, 0f, 5f, 0.03f, true); Decal(g, "Hay", wp, 1.8f, 1.8f, 140f, "Hay_Barrow"); }
            }
            // hay strewn along the beds
            for (int i = 0; i < 2; i++) Decal(g, "Hay", p.W(-1.2f + 2.4f * i, 2.1f), 2.0f, 2.0f, 70f + 110f * i, "Hay_Beds" + i);
        }
    }
}
