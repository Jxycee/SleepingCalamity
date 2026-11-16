// B3 stage 0 of a ruined lot (design docs/design/area1-hollowmere.md section 6, decision DH1). Editor time, authority: none. Called by HollowmereBuilder.BuildFoundation right
// after PlaceRuin succeeded, for the Ruined preset only. Everything is parented under the lot's "Dress" child (so the Ruined/Rebuilt switch hides it with the ruin), seeded by
// the lot id (Id.Aggregate(17, ...) as the planters use), and has no collider (the lot's SolidLotMasses only adds colliders to meshes named Footing/Walls/Stub_/Rubble/Chimney).
//  - ivy [T12c] climbing the chimney stack on the face turned most toward the Morning sun (never the firebox face);
//  - saplings (crossed cards from the road's far-tree atlas, 1.2-2.2 m): one on an S lot, two on M and L lots, planted in the footing interior;
//  - bramble clumps: the B9 weed prefabs scaled up (olive copy of the shared foliage material) in the two corners farthest from the door;
//  - debris half buried in the grass: [T05a] roof tiles, [T05c] ruin scatter, [T05d] planks, 3-6 pieces, off the doorstep line;
//  - one perched crow (small generated low-poly mesh saved as an asset) on the chimney cap, else on the highest piece of the ruin.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereRuinDressing
    {
        const string Folder = "Assets/_Project/Content/Art/Town/RuinDressing";
        const string Adopted = "Assets/_Project/Content/Art/Town/Adopted/";
        static readonly Quaternion MorningSun = Quaternion.Euler(42f, -32f, 0f);   // BeautyCornerBuilder.MorningSun

        static bool loaded;
        static GameObject ivy, roof, scatter, plank;
        static readonly List<GameObject> weeds = new List<GameObject>();
        static readonly Dictionary<GameObject, Bounds> prefabBounds = new Dictionary<GameObject, Bounds>();
        static Material brambleMat, crowMat, treeMat;
        static Mesh crowMesh; static Mesh[] sapMeshes;
        static int lots, withChimney, saplings, crows, debris, brambles, ivies;

        static GameObject LoadPrefab(string code) => AssetDatabase.LoadAssetAtPath<GameObject>(Adopted + code + "/" + code + ".prefab");

        static void Load()
        {
            loaded = true;
            ivy = LoadPrefab("T12c"); roof = LoadPrefab("T05a"); scatter = LoadPrefab("T05c"); plank = LoadPrefab("T05d");
            foreach (string name in new[] { "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E" })
            {
                string folder = BeautyCornerArt.FindFolder("B9", name);
                string fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                GameObject go = fbx != null ? BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx)) : null;
                if (go != null) weeds.Add(go);
            }
            BuilderUtil.EnsureFolder(Folder);
            Material shared = BeautyCornerArt.CrackWeedsMaterial();
            if (shared != null)
            {
                brambleMat = LookTestBuilder.Ensure("Assets/_Project/Content/Materials/Hollowmere/M_RuinBramble.mat", shared.shader);
                brambleMat.CopyPropertiesFromMaterial(shared);
                foreach (var k in shared.enabledKeywords) brambleMat.EnableKeyword(k.name);
                brambleMat.renderQueue = shared.renderQueue;
                Color c = new Color(0.50f, 0.58f, 0.36f, 1f); brambleMat.SetColor("_BaseColor", c); brambleMat.SetColor("_Color", c);
                brambleMat.enableInstancing = true;
                MaterialValidation.Validate(brambleMat); EditorUtility.SetDirty(brambleMat);
            }
            treeMat = AssetDatabase.LoadAssetAtPath<Material>(HollowmereB3Ground.MatFolder + "/M_RD_FarTrees.mat");
            if (treeMat != null && treeMat.GetTexture("_BaseMap") == null) treeMat = null;   // the atlas has not landed: no saplings
            crowMat = LookTestBuilder.Ensure(HollowmereB3Ground.MatFolder + "/M_RuinCrow.mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found"));
            Color cc = new Color(0.07f, 0.07f, 0.085f, 1f); crowMat.SetColor("_BaseColor", cc); crowMat.SetColor("_Color", cc); crowMat.SetFloat("_Smoothness", 0.4f); crowMat.SetFloat("_Metallic", 0f);
            crowMat.enableInstancing = true; MaterialValidation.Validate(crowMat); EditorUtility.SetDirty(crowMat);
            crowMesh = BuildCrow();
            sapMeshes = new Mesh[4];
            for (int v = 0; v < 4; v++) sapMeshes[v] = BuildSapling(v);
        }

        // The statics (loaded assets, counters) outlive a build in the same editor session: HollowmereBuilder.BuildScene calls this first so every build starts clean.
        public static void Reset()
        {
            loaded = false; ivy = roof = scatter = plank = null;
            weeds.Clear(); prefabBounds.Clear();
            brambleMat = crowMat = treeMat = null; crowMesh = null; sapMeshes = null;
            lots = withChimney = saplings = crows = debris = brambles = ivies = 0;
        }

        public static void LogSummary() =>
            Debug.Log("SC HOLLOWMERE ruin dressing: " + lots + " lots, " + withChimney + " with a chimney found, " + ivies + " ivy, " + saplings + " saplings, " + brambles + " bramble clumps, " + debris + " debris pieces, " + crows + " crows"
                + (ivy == null || roof == null || scatter == null || plank == null ? " (an adopted prefab is MISSING)" : ""));

        // ------------------------------------------------------------------------------------------------ one lot
        public static void Dress(Transform lot, Foundation f)
        {
            if (!loaded) Load();
            Transform ruin = null;
            foreach (Transform c in lot) if (c.name.StartsWith("Ruin_", System.StringComparison.Ordinal)) { ruin = c; break; }
            if (ruin == null) return;
            lots++;
            var rnd = new System.Random(f.Id.Aggregate(17, (a, c) => a * 31 + c));
            Transform dress = new GameObject("Dress").transform; dress.SetParent(lot, false);
            float half = f.Side * 0.5f;
            Transform chim = FindDeep(ruin, "Chimney"), door = FindDeep(ruin, "SOCKET_Doorstep");
            Vector3 doorL = lot.InverseTransformPoint(door != null ? door.position : ruin.position + ruin.forward * half);
            var door2 = new Vector2(doorL.x, doorL.z);
            Vector2 chim2 = Vector2.zero; float chimR = 0f; bool hasChim = false;
            Bounds chimWorld = default;
            if (chim != null && TryBounds(chim, out Bounds chimPre))
            {
                // round 3: a hint of extra lean (seeded, 2.5-4 degrees about the stack's own base, away from the door) so the stack stops reading as a plumb slab
                Vector3 basePt = new Vector3(chimPre.center.x, chimPre.min.y, chimPre.center.z);
                Vector3 away = new Vector3(chimPre.center.x - ruin.position.x, 0f, chimPre.center.z - ruin.position.z);
                if (away.sqrMagnitude < 0.01f) away = ruin.right;
                Vector3 axis = Vector3.Cross(Vector3.up, away.normalized);
                chim.RotateAround(basePt, axis, -(2.5f + 1.5f * (float)rnd.NextDouble()));
            }
            if (chim != null && TryBounds(chim, out chimWorld))
            {
                hasChim = true; withChimney++;
                Vector3 cl = lot.InverseTransformPoint(chimWorld.center); chim2 = new Vector2(cl.x, cl.z); chimR = Mathf.Max(chimWorld.extents.x, chimWorld.extents.z) + 0.9f;
            }

            bool Free(Vector2 p, float doorClear)
            {
                if (Mathf.Abs(p.x) > half - 0.9f || Mathf.Abs(p.y) > half - 0.9f) return false;
                if (Vector2.Distance(p, door2) < doorClear) return false;
                Vector2 ab = Vector2.zero - door2; float tt = Mathf.Clamp01(Vector2.Dot(p - door2, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                if (Vector2.Distance(p, door2 + ab * tt) < 1.0f) return false;   // the line from the doorstep to the middle stays clear
                if (hasChim && Vector2.Distance(p, chim2) < chimR) return false;
                return true;
            }
            Vector2 Pick(float doorClear)
            {
                for (int k = 0; k < 24; k++)
                {
                    var p = new Vector2(((float)rnd.NextDouble() * 2f - 1f) * (half - 0.9f), ((float)rnd.NextDouble() * 2f - 1f) * (half - 0.9f));
                    if (Free(p, doorClear)) return p;
                }
                return new Vector2(float.NaN, float.NaN);
            }

            if (hasChim) ChimneyDress(chim, chimWorld, dress, rnd);
            // ivy on the chimney's sunny face
            if (hasChim && ivy != null && TryIvy(ruin, chim, chimWorld, dress, rnd)) ivies++;

            // saplings
            // Area 1 rework (Run 6): a real young tree has grown up through each ruin since the town fell (S 1, M 1, L 2; 3.8-8 m; HollowmereWild.LotTree, no
            // collider), so the ruined rows carry height and cast shade. The crossed-card sapling stays as the fallback while the tree prefabs are missing.
            int nSap = f.Size == FoundationSize.L ? 2 : 1;
            for (int i = 0; i < nSap; i++)
            {
                Vector2 p = Pick(2.4f);
                if (float.IsNaN(p.x)) continue;
                int variant = rnd.Next(4); float h = 1.7f + (float)rnd.NextDouble() * 0.6f;
                float treeH = f.Size == FoundationSize.S ? 3.8f + (float)rnd.NextDouble() * 1.7f : 4.6f + (float)rnd.NextDouble() * 3.4f, treeYaw = (float)rnd.NextDouble() * 360f;
                GameObject lotTree = HollowmereWild.LotTree(dress, dress.TransformPoint(new Vector3(p.x, 0f, p.y)), treeH, treeYaw);
                if (lotTree != null) { lotTree.name = "Dress_Tree_" + i; saplings++; continue; }
                if (treeMat == null || f.Size != FoundationSize.L || i > 0) continue;   // the card fallback keeps the old rule: one on L lots only
                var go = new GameObject("Dress_Sapling_" + i); go.transform.SetParent(dress, false);
                go.transform.localPosition = new Vector3(p.x, -0.06f * h, p.y);
                go.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = new Vector3(h, h, h);
                go.AddComponent<MeshFilter>().sharedMesh = sapMeshes[variant];
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = treeMat; r.shadowCastingMode = ShadowCastingMode.On;
                saplings++;
            }

            // bramble clumps in the two corners farthest from the door
            if (brambleMat != null && weeds.Count > 0)
            {
                float c0 = half - 1.1f;
                var corners = new[] { new Vector2(-c0, -c0), new Vector2(c0, -c0), new Vector2(c0, c0), new Vector2(-c0, c0) };
                var order = Enumerable.Range(0, 4).OrderByDescending(i => Vector2.Distance(corners[i], door2)).ThenBy(i => i).Take(2).ToArray();
                foreach (int ci in order)
                {
                    int count = 3 + rnd.Next(2);
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 p = corners[ci] + new Vector2(((float)rnd.NextDouble() - 0.5f) * 1.4f, ((float)rnd.NextDouble() - 0.5f) * 1.4f);
                        if (hasChim && Vector2.Distance(p, chim2) < 0.8f) continue;
                        GameObject model = weeds[rnd.Next(weeds.Count)];
                        var w = (GameObject)PrefabUtility.InstantiatePrefab(model, dress);
                        w.name = "Dress_Bramble_" + ci + "_" + k;
                        foreach (Renderer rd in w.GetComponentsInChildren<Renderer>(true))
                        {
                            var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                            for (int q = 0; q < mats.Length; q++) mats[q] = brambleMat;
                            rd.sharedMaterials = mats; rd.shadowCastingMode = ShadowCastingMode.Off;
                        }
                        float hh = Mathf.Max(0.05f, PrefabBounds(model).size.y), target = 0.45f + (float)rnd.NextDouble() * 0.3f;   // polish: 0.7-1.15 m rosettes read as agave from the overview
                        w.transform.localPosition = new Vector3(p.x, 0f, p.y);
                        w.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                        w.transform.localScale = Vector3.one * (target / hh);
                        brambles++;
                    }
                }
            }

            // debris
            int nDeb = 3 + rnd.Next(4);
            for (int i = 0; i < nDeb; i++)
            {
                int kind = rnd.Next(3);
                GameObject prefab = kind == 0 ? roof : kind == 1 ? scatter : plank;
                if (prefab == null) continue;
                Vector2 p = Pick(1.8f);
                if (float.IsNaN(p.x)) continue;
                Bounds b0 = PrefabBounds(prefab);
                float ext = Mathf.Max(0.05f, Mathf.Max(b0.size.x, b0.size.z));
                float target = (kind == 0 ? 0.6f : kind == 1 ? 0.9f : 1.5f) * (0.8f + 0.5f * (float)rnd.NextDouble());
                float sc = Mathf.Clamp(target / ext, 0.05f, 8f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, dress);
                go.name = "Dress_Debris_" + i;
                go.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * sc;
                go.transform.localPosition = new Vector3(p.x, -b0.min.y * sc - 0.4f * b0.size.y * sc, p.y);   // lowest point 40% of the piece's height under the grass
                debris++;
            }

            // one perched crow
            Vector3 perch; bool ok = false; float yaw = (float)rnd.NextDouble() * 360f;
            if (hasChim)
            {
                perch = new Vector3(chimWorld.center.x, chimWorld.max.y, chimWorld.center.z);   // the cap (SOCKET_Smoke, when present, is the flue mouth just inside it)
                Vector3 side = ruin.right * (Mathf.Min(chimWorld.extents.x, chimWorld.extents.z) * 0.5f * ((rnd.Next(2) == 0) ? 1f : -1f));
                perch += new Vector3(side.x, -0.03f, side.z); ok = true;
            }
            else if (TryHighest(ruin, out perch)) ok = true;
            if (ok && crowMesh != null)
            {
                var go = new GameObject("Dress_Crow"); go.transform.SetParent(dress, false);
                go.transform.position = perch;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = crowMesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = crowMat; r.shadowCastingMode = ShadowCastingMode.On;
                crows++;
            }
        }


        // Round 3: break the slab look. Corbel stones overhang the top (one slipped, one missing), and a rubble apron of block-sized stones lies round the foot, heavier on the side
        // the top fell toward. Stone material taken from the stack itself.
        static void ChimneyDress(Transform chim, Bounds b, Transform dress, System.Random rnd)
        {
            Material stone = null;
            foreach (Renderer r in chim.GetComponentsInChildren<Renderer>(true)) { if (r.sharedMaterial != null) { stone = r.sharedMaterial; break; } }
            if (stone == null) return;
            var root = new GameObject("Dress_ChimneyStones").transform; root.SetParent(dress, false);
            float top = b.max.y, hx = b.extents.x, hz = b.extents.z;
            // corbel / cap stones
            int caps = 5;
            int missing = rnd.Next(caps);
            for (int i = 0; i < caps; i++)
            {
                if (i == missing) continue;
                float a = (i / (float)caps) * 6.283f + 0.3f;
                Vector3 pos = new Vector3(b.center.x + Mathf.Cos(a) * (hx + 0.02f), top - 0.04f, b.center.z + Mathf.Sin(a) * (hz + 0.02f));
                Vector3 size = new Vector3(0.42f + 0.18f * (float)rnd.NextDouble(), 0.2f + 0.08f * (float)rnd.NextDouble(), 0.34f + 0.12f * (float)rnd.NextDouble());
                var go = LookTestBuilder.Box(root, "Corbel_" + i, Vector3.zero, size, stone, default, 2f);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 14f, -a * Mathf.Rad2Deg + 90f + ((float)rnd.NextDouble() - 0.5f) * 20f, ((float)rnd.NextDouble() - 0.5f) * 14f);
            }
            // rubble apron
            float fallA = (float)rnd.NextDouble() * 6.283f;
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float a = fallA + ((float)rnd.NextDouble() - 0.5f) * (i < 6 ? 2.2f : 6.283f), d = Mathf.Max(hx, hz) + 0.25f + (i < 6 ? 0.9f : 0.5f) * (float)rnd.NextDouble();
                Vector3 pos = new Vector3(b.center.x + Mathf.Cos(a) * d, b.min.y + 0.08f, b.center.z + Mathf.Sin(a) * d);
                float s = 0.28f + 0.3f * (float)rnd.NextDouble();
                var go = LookTestBuilder.Box(root, "Rubble_" + i, Vector3.zero, new Vector3(s * (1f + 0.5f * (float)rnd.NextDouble()), s * 0.7f, s), stone, default, 2f);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 40f, (float)rnd.NextDouble() * 360f, ((float)rnd.NextDouble() - 0.5f) * 40f);
            }
        }

        // ------------------------------------------------------------------------------------------------ helpers
        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { Transform r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        static bool TryBounds(Transform t, out Bounds b)
        {
            b = default; bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("FOOTPRINT", System.StringComparison.Ordinal)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        // The highest point of any ruin renderer (top of the tallest bounds, over its centre).
        static bool TryHighest(Transform ruin, out Vector3 p)
        {
            p = default; float best = float.MinValue; bool any = false;
            foreach (Renderer r in ruin.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("FOOTPRINT", System.StringComparison.Ordinal) || r.name.StartsWith("Foliage", System.StringComparison.Ordinal) || !r.enabled) continue;
                Bounds b = r.bounds;
                if (b.max.y > best) { best = b.max.y; p = new Vector3(b.center.x, b.max.y - 0.02f, b.center.z); any = true; }
            }
            return any;
        }

        // Local-space bounds of a prefab at scale 1 (instantiated at the origin, measured, destroyed).
        static Bounds PrefabBounds(GameObject prefab)
        {
            if (prefabBounds.TryGetValue(prefab, out Bounds cached)) return cached;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tmp.transform.localScale = Vector3.one;
            Bounds b = default; bool any = false;
            foreach (Renderer r in tmp.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Object.DestroyImmediate(tmp);
            if (!any) b = new Bounds(Vector3.up * 0.5f, Vector3.one);
            prefabBounds[prefab] = b;
            Debug.Log("SC HOLLOWMERE ruin dressing prefab " + prefab.name + " bounds centre " + b.center.ToString("F2") + " size " + b.size.ToString("F2"));
            return b;
        }

        // Ivy stands on the chimney face (in the ruin's frame: +-X and -Z faces; the +Z face is the firebox) turned most toward the Morning sun, thin axis of the card on the
        // wall normal, its long axis up, from about a third of the way up the stack.
        static bool TryIvy(Transform ruin, Transform chim, Bounds chimWorld, Transform dress, System.Random rnd)
        {
            // chimney extents in the ruin's own frame
            Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
            foreach (Renderer r in chim.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = ruin.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    lo = Vector3.Min(lo, c); hi = Vector3.Max(hi, c);
                }
            }
            Vector3 toSun = -(MorningSun * Vector3.forward);
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.back };   // never Vector3.forward: the firebox
            Vector3 best = normals[0]; float bestDot = float.MinValue;
            foreach (Vector3 n in normals) { float d = Vector3.Dot(ruin.TransformDirection(n), toSun); if (d > bestDot) { bestDot = d; best = n; } }
            Vector3 centreL = (lo + hi) * 0.5f, ext = (hi - lo) * 0.5f;
            Vector3 faceL = centreL + Vector3.Scale(best, ext);
            Vector3 N = ruin.TransformDirection(best).normalized;
            float stackH = hi.y - lo.y;

            Bounds b0 = PrefabBounds(ivy);
            Vector3 sz = b0.size;
            int thin = sz.x <= sz.y && sz.x <= sz.z ? 0 : sz.y <= sz.z ? 1 : 2;
            int tall = -1; float tallV = -1f;
            for (int a = 0; a < 3; a++) if (a != thin && sz[a] > tallV) { tallV = sz[a]; tall = a; }
            Vector3 thinAxis = Axis(thin), tallAxis = Axis(tall);
            float targetH = Mathf.Min(0.55f * stackH, 2.6f);
            float s = Mathf.Max(0.05f, targetH / Mathf.Max(0.05f, tallV));
            float wide = sz[3 - thin - tall] * s, faceW = (best == Vector3.back ? ext.x : ext.z) * 2f;
            if (wide > faceW * 1.4f) s *= faceW * 1.4f / wide;
            Quaternion src = Quaternion.LookRotation(thinAxis, tallAxis), dst = Quaternion.LookRotation(N, Vector3.up);
            Quaternion rot = dst * Quaternion.Inverse(src);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(ivy, dress);
            go.name = "Dress_ChimneyIvy";
            go.transform.rotation = rot; go.transform.localScale = Vector3.one * s;
            float lateral = ((float)rnd.NextDouble() - 0.5f) * faceW * 0.3f;
            Vector3 faceW3 = ruin.TransformPoint(faceL);
            float baseY = ruin.TransformPoint(new Vector3(0f, lo.y + 0.33f * stackH, 0f)).y;
            Vector3 want = new Vector3(faceW3.x, baseY + tallV * s * 0.5f, faceW3.z) + Vector3.Cross(Vector3.up, N) * lateral + N * (sz[thin] * s * 0.5f + 0.03f);
            go.transform.position = want - rot * Vector3.Scale(b0.center, Vector3.one * s);
            return true;
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        // ------------------------------------------------------------------------------------------------ generated meshes
        sealed class Buf
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector3> N = new List<Vector3>(); public readonly List<int> I = new List<int>();
            // A closed shape given as vertices and faces; faces are flipped so they face away from the shape's centre.
            public void Solid(List<Vector3> v, List<int> tri)
            {
                Vector3 c = Vector3.zero; foreach (Vector3 p in v) c += p; c /= v.Count;
                int b = V.Count; V.AddRange(v);
                for (int i = 0; i < tri.Count; i += 3)
                {
                    int a = tri[i] + b, bb = tri[i + 1] + b, cc = tri[i + 2] + b;
                    Vector3 n = Vector3.Cross(V[bb] - V[a], V[cc] - V[a]);
                    Vector3 mid = (V[a] + V[bb] + V[cc]) / 3f;
                    if (Vector3.Dot(n, mid - c) >= 0f) { I.Add(a); I.Add(bb); I.Add(cc); } else { I.Add(a); I.Add(cc); I.Add(bb); }
                }
            }
            public void Ellipsoid(Vector3 centre, Vector3 radii, int lat, int lon, Quaternion rot)
            {
                var v = new List<Vector3>(); var tri = new List<int>();
                for (int i = 0; i <= lat; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        float th = i * Mathf.PI / lat, ph = j * 2f * Mathf.PI / lon;
                        v.Add(centre + rot * Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)), radii));
                    }
                for (int i = 0; i < lat; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        int a = i * lon + j, b = i * lon + (j + 1) % lon, c = (i + 1) * lon + j, d = (i + 1) * lon + (j + 1) % lon;
                        tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
                    }
                Solid(v, tri);
            }
            public void Cone(Vector3 baseC, Vector3 tip, float r, int seg)
            {
                var v = new List<Vector3>(); var tri = new List<int>();
                Vector3 ax = (tip - baseC).normalized, u = Vector3.Cross(ax, Vector3.up).normalized; if (u.sqrMagnitude < 0.01f) u = Vector3.right; Vector3 w = Vector3.Cross(ax, u);
                for (int i = 0; i < seg; i++) { float a = i * 2f * Mathf.PI / seg; v.Add(baseC + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * r); }
                v.Add(tip); v.Add(baseC);
                for (int i = 0; i < seg; i++) { tri.Add(i); tri.Add((i + 1) % seg); tri.Add(seg); tri.Add(i); tri.Add(seg + 1); tri.Add((i + 1) % seg); }
                Solid(v, tri);
            }
            public void Box(Vector3 c, Vector3 size)
            {
                var v = new List<Vector3>(); var tri = new List<int>();
                for (int i = 0; i < 8; i++) v.Add(c + Vector3.Scale(new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f), size));
                int[] f = { 0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3 };
                tri.AddRange(f);
                Solid(v, tri);
            }
        }

        // A perched crow, about 0.55 m beak to tail, feet at y 0, facing +Z.
        static Mesh BuildCrow()
        {
            var b = new Buf();
            b.Ellipsoid(new Vector3(0f, 0.14f, 0f), new Vector3(0.075f, 0.07f, 0.15f), 6, 9, Quaternion.Euler(-12f, 0f, 0f));
            b.Ellipsoid(new Vector3(0f, 0.225f, 0.125f), new Vector3(0.048f, 0.048f, 0.055f), 5, 8, Quaternion.identity);
            b.Cone(new Vector3(0f, 0.22f, 0.168f), new Vector3(0f, 0.208f, 0.255f), 0.02f, 6);
            b.Ellipsoid(new Vector3(0f, 0.11f, -0.2f), new Vector3(0.04f, 0.012f, 0.12f), 4, 8, Quaternion.Euler(-14f, 0f, 0f));
            b.Ellipsoid(new Vector3(-0.078f, 0.145f, -0.02f), new Vector3(0.014f, 0.05f, 0.13f), 4, 7, Quaternion.Euler(-12f, 0f, 0f));
            b.Ellipsoid(new Vector3(0.078f, 0.145f, -0.02f), new Vector3(0.014f, 0.05f, 0.13f), 4, 7, Quaternion.Euler(-12f, 0f, 0f));
            b.Box(new Vector3(-0.03f, 0.04f, 0f), new Vector3(0.012f, 0.08f, 0.012f));
            b.Box(new Vector3(0.03f, 0.04f, 0f), new Vector3(0.012f, 0.08f, 0.012f));
            var m = new Mesh { name = "Crow_Perched" };
            m.SetVertices(b.V); m.SetTriangles(b.I, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return HollowmereB3Ground.SaveMesh(Folder + "/Crow_Perched.asset", m);
        }

        // Three crossed cards (0, 60, 120 degrees) of one whole-tree cell of the far-tree atlas (4 columns), unit height; the trunk foot is a little under y 0 in the cell.
        static Mesh BuildSapling(int variant)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>(); var idx = new List<int>();
            float u0 = (variant % 4) * 0.25f; Vector3 crown = Vector3.up * 0.62f; float w = 0.5f;
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 60f, 0f); Vector3 right = q * Vector3.right, face = q * Vector3.back;
                Vector3[] c = { -right * (w * 0.5f), right * (w * 0.5f), right * (w * 0.5f) + Vector3.up, -right * (w * 0.5f) + Vector3.up };
                Vector2[] t = { new Vector2(u0, 0f), new Vector2(u0 + 0.25f, 0f), new Vector2(u0 + 0.25f, 1f), new Vector2(u0, 1f) };
                int b = v.Count;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 radial = (c[i] - crown).normalized;
                    v.Add(c[i]); uv.Add(t[i]); tg.Add(new Vector4(right.x, right.y, right.z, 1f));
                    n.Add(Vector3.Slerp(face, radial + Vector3.up * 0.35f, 0.7f).normalized);
                }
                idx.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var m = new Mesh { name = "Sapling_" + variant };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTangents(tg); m.SetTriangles(idx, 0); m.RecalculateBounds();
            return HollowmereB3Ground.SaveMesh(Folder + "/Sapling_" + variant + ".asset", m);
        }
    }
}
