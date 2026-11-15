// B3 square ground (design docs/design/area1-hollowmere.md section 7 S2, decisions DH4, DH1). Editor time, authority: none. Deterministic: every position comes from a
// seeded System.Random or a fixed table, meshes and textures are rewritten only when their contents changed.
//  - SquareGroundMaterial: the T01 cobble scan (2 m tile) with a second, low-contrast macro tint (the Surround macro noise in the detail slot) so the repeat does not show.
//  - BuildEdges: a ~1.5 m draped band of [T01b] river cobbles along the square's four borders (left out where a lane opens the border) and a 0.5 m gutter line beside it on
//    the S and W edges (a darker, wetter copy of the same scan, 6 mm under the band).
//  - BuildDecals: "SquareDecals" under Set (both presets): 12 leaf drifts ([T15a]/[T15b]), 5 puddles, a cart rut line from the Market Street mouth to the bakery lot,
//    soft moss and dirt patches. One merged draped mesh per material (6 draw calls), alpha blended, 4-20 mm above the draped ground, batching-static.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereB3Ground
    {
        public const float BedLift = 0.004f;   // the square's cobble bed sits this far above the draped ground (its lowest vertex); the slab (top -0.02) and terrain lie below
        public const string MatFolder = "Assets/_Project/Content/Materials/Hollowmere";
        public const string DecalFolder = "Assets/_Project/Content/Art/Town/Decals";
        const float EdgeBand = 1.5f, GutterWidth = 0.5f, EdgeUvMetres = 1.6f;

        // ------------------------------------------------------------------------------------------------ materials
        public static Material SquareGroundMaterial()
        {
            Material src = B0Fab.CobbleGround();
            if (src == null) return null;
            B0Fab.TexSet row = B0Fab.Megascans("T01c_cobblestone", "T01c_cobble");   // round 2: T01 is fan-laid and its arcs repeat every 2 m; T01c is coursed like H1 (spec leeway: DH4 says concept cobbles)

            BuilderUtil.EnsureFolder(MatFolder);
            Material m = LookTestBuilder.Ensure(MatFolder + "/M_SquareGround.mat", src.shader);
            m.CopyPropertiesFromMaterial(src);
            foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
            if (row != null) { B0Fab.Apply(m, row, 1f); m.SetFloat("_BumpScale", 1.6f); }   // on the square's own copy: the B0 material stays T01
            var macro = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Content/Textures/BeautyCorner/Surround/SurroundMacro.png");
            if (macro != null)
            {
                m.SetTexture("_DetailAlbedoMap", macro); m.SetFloat("_DetailAlbedoMapScale", 1f); m.EnableKeyword("_DETAIL_MULX2");
                m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (2f / 37f));   // detail uv = base uv (1 per 2 m) x this: one repeat per 37 m, not lined up with the corner's 23 m
            }
            Texture2D graded = GradedCobbleAlbedo();   // round 3: per-stone value and hue, dark joints with moss, edge rims (baked from the T01c albedo + displacement)
            if (graded != null) m.SetTexture("_BaseMap", graded);
            Color neutral = new Color(1.0f, 0.97f, 0.92f, 1f);
            m.SetColor("_BaseColor", neutral); m.SetColor("_Color", neutral);
            m.enableInstancing = true;
            MaterialValidation.Validate(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Round 3: the T01c scan reads as one grey-tan sheet. Bake a graded copy: stones are the connected high regions of the displacement map; each gets its own tone (warm tan,
        // cool grey, a few rust) and value, the joints go darker with olive moss in them, and a thin dark rim sits on every stone edge. Tiles (wrap-around fill and noise).
        static Texture2D GradedCobbleAlbedo()
        {
            string srcDir = BuilderUtil.Abs("Art/Source/Fab/T01c_cobblestone");
            if (!Directory.Exists(srcDir)) return null;
            string Find(string tail) => Directory.GetFiles(srcDir, "*_" + tail + ".jpg").OrderBy(f => f, System.StringComparer.Ordinal).FirstOrDefault();
            string bcf = Find("BaseColor"), dpf = Find("Displacement");
            if (bcf == null || dpf == null) return null;
            Color32[] bc = DecodeJpg(bcf, out int N, out int h0), dp = DecodeJpg(dpf, out int N2, out int h2);
            if (N != h0 || N != N2 || h2 != N) return null;
            int total = N * N;
            var hist = new int[256];
            for (int i = 0; i < total; i++) hist[dp[i].r]++;
            int acc = 0, thr = 0, target = Mathf.RoundToInt(total * 0.38f);
            for (; thr < 256; thr++) { acc += hist[thr]; if (acc >= target) break; }
            var mask = new bool[total];
            for (int i = 0; i < total; i++) mask[i] = dp[i].r > thr;
            var lab = new int[total]; for (int i = 0; i < total; i++) lab[i] = -1;
            int nLab = 0; var stack = new Stack<int>();
            for (int i = 0; i < total; i++)
            {
                if (!mask[i] || lab[i] >= 0) continue;
                lab[i] = nLab; stack.Push(i);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(), cx = c % N, cy = c / N;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = (cx + (d == 0 ? 1 : d == 1 ? N - 1 : 0)) % N, ny = (cy + (d == 2 ? 1 : d == 3 ? N - 1 : 0)) % N, ni = ny * N + nx;
                        if (mask[ni] && lab[ni] < 0) { lab[ni] = nLab; stack.Push(ni); }
                    }
                }
                nLab++;
            }
            // soft mask (1 inside a stone, 0 in a joint): separable wrap-around box blur, three passes
            var soft = new float[total]; var tmp = new float[total];
            for (int i = 0; i < total; i++) soft[i] = mask[i] ? 1f : 0f;
            int rad = 4;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < N; y++)
                {
                    float sum = 0f; for (int k = -rad; k <= rad; k++) sum += soft[y * N + (k + N) % N];
                    for (int x = 0; x < N; x++) { tmp[y * N + x] = sum / (2 * rad + 1); sum += soft[y * N + (x + rad + 1) % N] - soft[y * N + (x - rad + N) % N]; }
                }
                for (int x = 0; x < N; x++)
                {
                    float sum = 0f; for (int k = -rad; k <= rad; k++) sum += tmp[((k + N) % N) * N + x];
                    for (int y = 0; y < N; y++) { soft[y * N + x] = sum / (2 * rad + 1); sum += tmp[((y + rad + 1) % N) * N + x] - tmp[((y - rad + N) % N) * N + x]; }
                }
            }
            // per-stone tone and value
            var tones = new[] { new Vector3(1.06f, 1.0f, 0.88f), new Vector3(1.0f, 0.96f, 0.84f), new Vector3(0.94f, 0.97f, 1.01f), new Vector3(0.90f, 0.93f, 0.98f), new Vector3(1.10f, 0.90f, 0.80f), Vector3.one };
            float[] cum = { 0.22f, 0.44f, 0.64f, 0.82f, 0.90f, 1f };
            var stoneTone = new Vector3[Mathf.Max(1, nLab)];
            for (int l = 0; l < nLab; l++)
            {
                float r = H(l, 3, 0xB30), v = Mathf.Lerp(0.84f, 1.24f, H(l, 5, 0xB31)) * 1.10f;
                int ti = 0; while (ti < cum.Length - 1 && r > cum[ti]) ti++;
                stoneTone[l] = tones[ti] * v;
            }
            var px = new Color32[total];
            int cells = 16;   // joint-moss noise lattice, wraps over the tile
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    float u = x / (float)N, w = y / (float)N;
                    Vector3 c = new Vector3(bc[i].r, bc[i].g, bc[i].b) / 255f;
                    if (mask[i]) { Vector3 t = stoneTone[lab[i]]; c = new Vector3(Mathf.Clamp01(c.x * t.x), Mathf.Clamp01(c.y * t.y), Mathf.Clamp01(c.z * t.z)); }
                    float jw = 1f - soft[i];
                    float nz = 0.5f * TNoise(u, w, cells, 0xB41) + 0.3f * TNoise(u, w, cells * 2 + 8, 0xB42) + 0.2f * TNoise(u, w, cells * 6 + 4, 0xB43);
                    float moss = Mathf.Clamp01((nz - 0.55f) * 5f) * Mathf.Clamp01(jw * 1.3f);
                    c *= 1f - 0.55f * jw;
                    Vector3 mc = new Vector3(0.24f, 0.30f, 0.10f) * (0.7f + 0.6f * TNoise(u, w, 60, 0xB44));
                    c = c * (1f - moss) + mc * moss;
                    if (mask[i]) c *= 1f - 0.25f * Mathf.Clamp01(1f - soft[i]);   // bevel rim darkening
                    px[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.x) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.y) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.z) * 255f), 255);
                }
            Debug.Log("SC HOLLOWMERE square cobble albedo: " + nLab + " stones graded at " + N + " px");
            return WritePng2("Assets/_Project/Content/Art/Town/Decals/SquareCobble_Albedo.png", N, px);
        }

        static Texture2D WritePng2(string path, int n, Color32[] px) { BuilderUtil.EnsureFolder(DecalFolder); return WriteTex(path, n, n, px, true, false, false, false, 2048); }

        // Value noise on a lattice that wraps in both axes (cells per tile), u and w in 0..1.
        static float TNoise(float u, float w, int cells, int seed)
        {
            float x = u * cells, y = w * cells;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            int a = ((x0 % cells) + cells) % cells, b = ((x0 + 1) % cells + cells) % cells, c = ((y0 % cells) + cells) % cells, d = ((y0 + 1) % cells + cells) % cells;
            return Mathf.Lerp(Mathf.Lerp(H(a, c, seed), H(b, c, seed), sx), Mathf.Lerp(H(a, d, seed), H(b, d, seed), sx), sy);
        }

        static Material RiverMaterial(bool gutter)
        {
            B0Fab.TexSet s = B0Fab.Megascans("T01b_river_cobblestone", "T01b_river");
            if (s == null) return null;
            BuilderUtil.EnsureFolder(MatFolder);
            Material m = LookTestBuilder.Ensure(MatFolder + (gutter ? "/M_SquareGutter.mat" : "/M_SquareEdge.mat"), Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found"));
            B0Fab.Apply(m, s, 1f);
            m.SetFloat("_BumpScale", 1.4f);
            Color c = gutter ? new Color(0.50f, 0.46f, 0.42f, 1f) : new Color(0.98f, 0.90f, 0.78f, 1f);   // the gutter reads wet and dark
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.enableInstancing = true;
            MaterialValidation.Validate(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material DecalMaterial(string name, Texture2D albedo, Texture2D normal, Color tint, float smoothness, bool reflections, int queueOffset)
        {
            BuilderUtil.EnsureFolder(MatFolder);
            Material m = LookTestBuilder.Ensure(MatFolder + "/M_" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found"));
            BeautyCornerArt.AlphaBlend(m);
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", tint); m.SetColor("_Color", tint);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Smoothness", smoothness);
            if (reflections)
            {
                m.SetFloat("_SpecularHighlights", 1f); m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 1f); m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            m.SetFloat("_QueueOffset", queueOffset);   // drawn after the A2 tone patches (HollowmereSurfaces), which sit at queue 3000
            m.enableInstancing = true;
            MaterialValidation.Validate(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------------ mesh helpers
        sealed class MeshBuf
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> I = new List<int>();
            public int Add(Vector3 p, Vector2 uv) { V.Add(p); UV.Add(uv); return V.Count - 1; }
            public void Tri(int a, int b, int c)
            {
                Vector3 n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (n.y >= 0f) { I.Add(a); I.Add(b); I.Add(c); } else { I.Add(a); I.Add(c); I.Add(b); }   // always faces up
            }
            public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }   // a b c d around the quad
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(I, 0);
                m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
                return m;
            }
        }

        // Saves the mesh as an asset, rewriting it only when the vertices, UVs or triangles changed. Returns the asset.
        internal static Mesh SaveMesh(string path, Mesh fresh)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && existing.vertexCount == fresh.vertexCount && existing.vertices.SequenceEqual(fresh.vertices) && existing.uv.SequenceEqual(fresh.uv)
                && existing.triangles.SequenceEqual(fresh.triangles) && existing.normals.SequenceEqual(fresh.normals)) { Object.DestroyImmediate(fresh); return existing; }
            if (existing != null) { EditorUtility.CopySerialized(fresh, existing); Object.DestroyImmediate(fresh); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets(); return existing; }
            AssetDatabase.CreateAsset(fresh, path); AssetDatabase.SaveAssets();
            return fresh;
        }

        static float GroundY(TownLayout t, float x, float z) => HollowmereLayout.Ground(t, x, z);

        // True when a lane runs within `margin` metres of its own edge at (x, z): the border opens there (lane mouth, stair foot).
        static bool NearLane(TownLayout t, float x, float z, float margin)
        {
            var p = new TownPoint(x, z);
            foreach (Lane lane in t.Lanes)
                if (HollowmereLayout.DistanceToPolyline(p, lane.Points) - lane.Width * 0.5f < margin) return true;
            return false;
        }

        static GameObject Place(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ------------------------------------------------------------------------------------------------ border band and gutter
        // holder = Set/SquareGround (at the shelf height); vertices are holder-local. Returns the number of band metres laid.
        public static void BuildEdges(Transform holder, TownLayout t, string folder)
        {
            Material band = RiverMaterial(false), gutter = RiverMaterial(true);
            if (band == null) { Debug.LogWarning("SC HOLLOWMERE square edge: Art/Source/Fab/T01b_river_cobblestone is missing, no edge band"); return; }
            float x0 = HollowmereLayout.SquareMinX, x1 = HollowmereLayout.SquareMaxX, z0 = HollowmereLayout.SquareMinZ, z1 = HollowmereLayout.SquareMaxZ;
            // (start, direction along the side, inward normal, length, gutter)
            var sides = new (Vector2 a, Vector2 d, Vector2 n, float len, bool gut)[]
            {
                (new Vector2(x0, z0), new Vector2(1f, 0f), new Vector2(0f, 1f), x1 - x0, true),    // south
                (new Vector2(x0, z0), new Vector2(0f, 1f), new Vector2(1f, 0f), z1 - z0, true),    // west
                (new Vector2(x0, z1), new Vector2(1f, 0f), new Vector2(0f, -1f), x1 - x0, false),  // north
                (new Vector2(x1, z0), new Vector2(0f, 1f), new Vector2(-1f, 0f), z1 - z0, false),  // east
            };
            var mb = new MeshBuf(); var mg = new MeshBuf();
            const float step = 0.5f;
            int segs = 0, skipped = 0;
            foreach (var s in sides)
            {
                int n = Mathf.RoundToInt(s.len / step);
                for (int i = 0; i < n; i++)
                {
                    float a = i * step, b = (i + 1) * step, mid = (a + b) * 0.5f;
                    Vector2 m = s.a + s.d * mid + s.n * 0.8f;
                    if (NearLane(t, m.x, m.y, 0.6f)) { skipped++; continue; }
                    Strip(t, holder, mb, s.a, s.d, s.n, a, b, 0f, EdgeBand, 3, 0.015f);
                    if (s.gut) Strip(t, holder, mg, s.a, s.d, s.n, a, b, EdgeBand, EdgeBand + GutterWidth, 1, 0.009f);
                    segs++;
                }
            }
            if (segs == 0) return;
            Mesh bandMesh = SaveMesh(folder + "/SquareEdgeBand.asset", mb.ToMesh("SquareEdgeBand"));
            Place(holder, "EdgeBand", bandMesh, band, true);
            if (gutter != null && mg.V.Count > 0) Place(holder, "EdgeGutter", SaveMesh(folder + "/SquareEdgeGutter.asset", mg.ToMesh("SquareEdgeGutter")), gutter, false);
            Debug.Log("SC HOLLOWMERE square edge: " + segs + " band segments of " + step + " m (" + skipped + " left open at lane mouths), band " + EdgeBand + " m wide, gutter on S and W");
        }

        static void Strip(TownLayout t, Transform holder, MeshBuf buf, Vector2 a0, Vector2 d, Vector2 n, float a, float b, float o0, float o1, int across, float lift)
        {
            int[,] ids = new int[2, across + 1];
            for (int k = 0; k <= across; k++)
            {
                float o = Mathf.Lerp(o0, o1, k / (float)across);
                for (int e = 0; e < 2; e++)
                {
                    Vector2 p = a0 + d * (e == 0 ? a : b) + n * o;
                    float y = GroundY(t, p.x, p.y) - holder.position.y + BedLift + lift;
                    ids[e, k] = buf.Add(new Vector3(p.x - holder.position.x, y, p.y - holder.position.z), new Vector2(p.x / EdgeUvMetres, p.y / EdgeUvMetres));
                }
            }
            for (int k = 0; k < across; k++) buf.Quad(ids[0, k], ids[1, k], ids[1, k + 1], ids[0, k + 1]);
        }

        // ------------------------------------------------------------------------------------------------ decals
        sealed class DecalTextures { public Texture2D Blob, Moss, MossJoint, RutAlbedo, RutNormal, LeafA, LeafANormal, LeafB, LeafBNormal; }

        public static void BuildDecals(Transform set, TownLayout t)
        {
            BuilderUtil.EnsureFolder(DecalFolder);
            DecalTextures tx = MakeTextures();
            Transform root = new GameObject("SquareDecals").transform; root.SetParent(set, false);
            var rnd = new System.Random(0xB3D3);
            float lift0 = BedLift;

            // 1. leaf drifts: along the square's kerbs (border), the lee side (the east face of each lot front is picked by the seeded side order)
            var leafA = new MeshBuf(); var leafB = new MeshBuf();
            int drifts = 0;
            for (int i = 0; i < 34; i++)   // round 3: 20 -> 34 drifts along the kerbs and footings
            {
                float pr = (i + 0.15f + 0.7f * (float)rnd.NextDouble()) / 34f;
                Vector2 pos = default, n = default, d = default; bool ok = false;
                for (int tries = 0; tries < 24 && !ok; tries++)
                {
                    Perimeter((pr + tries * 0.013f) % 1f, out pos, out d, out n);
                    ok = !NearLane(t, pos.x, pos.y, 1.6f);
                }
                if (!ok) continue;
                float inward = 0.55f + 0.5f * (float)rnd.NextDouble();
                Vector2 c = pos + n * inward;
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + ((float)rnd.NextDouble() - 0.5f) * 30f;
                float lenA = 2.2f + 1.4f * (float)rnd.NextDouble(), lenB = 1.0f + 0.6f * (float)rnd.NextDouble();
                AddGrid(t, i % 2 == 0 ? leafA : leafB, c, yaw, lenB, lenA, 5, lift0 + 0.016f, 1f);
                drifts++;
            }

            // 1b. round 3: drifts round the fountain foot and along the rut (leaf litter gathers where the stone is lower and wetter)
            for (int i = 0; i < 9; i++)
            {
                float a = (i / 9f) * 6.283f + 0.4f * (float)rnd.NextDouble(), rr = 3.4f + 1.4f * (float)rnd.NextDouble();
                AddGrid(t, i % 2 == 0 ? leafB : leafA, new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr), a * Mathf.Rad2Deg + 90f, 1.1f + 0.5f * (float)rnd.NextDouble(), 1.8f + 1.2f * (float)rnd.NextDouble(), 5, lift0 + 0.017f, 1f);
                drifts++;
            }
            var rutDrift = new[] { new Vector2(0f, -16f), new Vector2(-1.6f, -11f), new Vector2(-3.6f, -5.5f), new Vector2(-5f, -0.5f), new Vector2(-8.6f, 3.4f), new Vector2(-10.5f, 5.8f) };
            foreach (Vector2 rd in rutDrift)
            {
                AddGrid(t, leafA, rd + new Vector2(0.5f * (float)rnd.NextDouble(), 0.5f * (float)rnd.NextDouble()), (float)rnd.NextDouble() * 360f, 1.0f, 2.2f, 5, lift0 + 0.017f, 1f);
                drifts++;
            }

            // 2. seven puddles (H3: wet sky mirrors in the joints): a small organic blob each, one by the gutter (S edge); the others in the open cobbles away from the stalls.
            // Each gets a wider dark wet halo (the dirt blob) under it so the cobbles round the water read soaked.
            var pud = new MeshBuf(); var halo = new MeshBuf();
            var puddles = new (float x, float z, float sx, float sz, float yaw)[]
            {
                (-9.5f, -18.7f, 2.8f, 1.3f, 8f), (9.8f, -14.2f, 2.4f, 1.8f, 35f), (-14.5f, -6.5f, 3.0f, 1.9f, 100f), (13.5f, 1.5f, 2.0f, 1.5f, 62f), (-3.2f, -11.5f, 3.6f, 2.2f, 160f),
                (4.5f, -9.0f, 2.6f, 1.4f, 120f), (-8.0f, -2.5f, 2.2f, 1.2f, 20f),
                (-1.1f, -12.4f, 2.4f, 0.8f, 100f), (-4.0f, -4.6f, 2.2f, 0.7f, 70f), (-9.0f, 4.0f, 2.6f, 0.9f, 35f), (7.5f, -4.0f, 3.2f, 1.6f, 20f), (0.8f, -16.5f, 2.0f, 0.8f, 95f)
            };
            foreach (var p in puddles)
            {
                AddGrid(t, pud, new Vector2(p.x, p.z), p.yaw, p.sx, p.sz, 4, lift0 + 0.020f, 1f);
                AddGrid(t, halo, new Vector2(p.x, p.z), p.yaw, p.sx * 1.9f, p.sz * 1.9f, 5, lift0 + 0.014f, 1f);
            }

            // 3. the cart rut: the Market Street mouth (0, -21) to the bakery lot F02's doorstep, bending round the fountain and the cloth stall
            var rut = new MeshBuf();
            var way = new[] { new Vector2(0.4f, -21.2f), new Vector2(0f, -17f), new Vector2(-1.2f, -12f), new Vector2(-3f, -6f), new Vector2(-4.2f, -1f), new Vector2(-7.5f, 3f), new Vector2(-11f, 6f), new Vector2(-12.0f, 6.9f) };
            float rutLen = Ribbon(t, rut, way, 2.2f, 4f, lift0 + 0.012f);

            // 4. moss patches heavier at the north shade and the fountain foot, plus large faint dirt patches against the tile repeat
            var moss = new MeshBuf(); var dirt = new MeshBuf();
            var mossAt = new (float x, float z, float s, float yaw)[]
            {
                (-14f, 5.2f, 5.5f, 20f), (-2f, 5.6f, 4.5f, 95f), (12.5f, 5.0f, 5f, 140f), (1.9f, -1.6f, 3.4f, 40f), (-1.7f, 1.9f, 3.0f, 130f), (-16.5f, -12f, 3.8f, 70f), (15f, -15f, 3.4f, 10f)
            };
            foreach (var m in mossAt) AddGrid(t, moss, new Vector2(m.x, m.z), m.yaw, m.s, m.s * 0.75f, 5, lift0 + 0.010f, 1f);
            for (int i = 0; i < 24; i++)   // polish: 14 -> 24 dark patches, and below 14 light dusty ones, so the ground varies in value, not just in colour
            {
                float x = Mathf.Lerp(HollowmereLayout.SquareMinX + 4f, HollowmereLayout.SquareMaxX - 4f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(HollowmereLayout.SquareMinZ + 4f, HollowmereLayout.SquareMaxZ - 3f, (float)rnd.NextDouble());
                float s = 4f + 6f * (float)rnd.NextDouble();
                AddGrid(t, dirt, new Vector2(x, z), (float)rnd.NextDouble() * 360f, s, s * (0.55f + 0.3f * (float)rnd.NextDouble()), 6, lift0 + 0.008f, 1f);
            }
            var dust = new MeshBuf();
            for (int i = 0; i < 14; i++)
            {
                float x = Mathf.Lerp(HollowmereLayout.SquareMinX + 3f, HollowmereLayout.SquareMaxX - 3f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(HollowmereLayout.SquareMinZ + 3f, HollowmereLayout.SquareMaxZ - 3f, (float)rnd.NextDouble());
                float s = 3f + 5f * (float)rnd.NextDouble();
                AddGrid(t, dust, new Vector2(x, z), (float)rnd.NextDouble() * 360f, s, s * (0.5f + 0.4f * (float)rnd.NextDouble()), 6, lift0 + 0.007f, 1f);
            }
            // round 3: large (9-17 m) tone patches, warm tan / cool slate / rust, so the Overview ground is not one grey and the open square varies at the 10 m scale
            var warm = new MeshBuf(); var cool = new MeshBuf(); var rust = new MeshBuf();
            var rnd2 = new System.Random(0xB3E1);
            for (int i = 0; i < 24; i++)
            {
                float x = Mathf.Lerp(HollowmereLayout.SquareMinX, HollowmereLayout.SquareMaxX, (float)rnd2.NextDouble());
                float z = Mathf.Lerp(HollowmereLayout.SquareMinZ, HollowmereLayout.SquareMaxZ, (float)rnd2.NextDouble());
                float s = 9f + 8f * (float)rnd2.NextDouble();
                AddGrid(t, i % 3 == 0 ? cool : i % 3 == 1 ? warm : (i % 6 == 2 ? rust : warm), new Vector2(x, z), (float)rnd2.NextDouble() * 360f, s, s * (0.5f + 0.35f * (float)rnd2.NextDouble()), 6, lift0 + 0.006f, 1f);
            }
            // moss in the joints: many small mottled patches (0.6-1.5 m), clustered at kerbs, the fountain foot and the damp (west/south) side, a few in the open
            var jointMoss = new MeshBuf();
            for (int i = 0; i < 60; i++)
            {
                float x, z;
                if (i < 28) { Perimeter((float)rnd.NextDouble(), out Vector2 pp, out Vector2 dd, out Vector2 nn); Vector2 q = pp + nn * (0.6f + 2.2f * (float)rnd.NextDouble()); x = q.x; z = q.y; }
                else if (i < 42) { float a = (float)rnd.NextDouble() * 6.283f, rr = 2.6f + 2.4f * (float)rnd.NextDouble(); x = Mathf.Cos(a) * rr; z = Mathf.Sin(a) * rr; }
                else { x = Mathf.Lerp(HollowmereLayout.SquareMinX + 3f, HollowmereLayout.SquareMaxX - 3f, (float)rnd.NextDouble()); z = Mathf.Lerp(HollowmereLayout.SquareMinZ + 3f, HollowmereLayout.SquareMaxZ - 3f, (float)rnd.NextDouble()); }
                if (NearLane(t, x, z, 1.0f)) continue;
                float s = 0.6f + 0.9f * (float)rnd.NextDouble();
                AddGrid(t, jointMoss, new Vector2(x, z), (float)rnd.NextDouble() * 360f, s * 1.5f, s, 3, lift0 + 0.011f, 1f);
            }

            Material mDirt = DecalMaterial("SquareDirt", tx.Blob, null, new Color(0.20f, 0.17f, 0.14f, 0.26f), 0f, false, 1);
            Material mDust = DecalMaterial("SquareDust", tx.Blob, null, new Color(0.78f, 0.74f, 0.66f, 0.17f), 0f, false, 1);
            Material mHalo = DecalMaterial("SquareWetHalo", tx.Blob, null, new Color(0.10f, 0.10f, 0.10f, 0.36f), 0.35f, false, 1);
            Material mMoss = DecalMaterial("SquareMoss", tx.Moss, null, new Color(0.46f, 0.56f, 0.34f, 0.50f), 0f, false, 2);   // polish: round 1 moss read as flat neon green strips
            Material mJoint = DecalMaterial("SquareJointMoss", tx.MossJoint, null, new Color(0.55f, 0.64f, 0.38f, 0.95f), 0f, false, 2);
            Material mRut = DecalMaterial("SquareRut", tx.RutAlbedo, tx.RutNormal, new Color(1f, 1f, 1f, 1f), 0.3f, false, 3);
            Material mLeafA = tx.LeafA != null ? DecalMaterial("SquareLeavesA", tx.LeafA, tx.LeafANormal, new Color(1f, 0.96f, 0.88f, 0.95f), 0.15f, false, 4) : null;
            Material mLeafB = tx.LeafB != null ? DecalMaterial("SquareLeavesB", tx.LeafB, tx.LeafBNormal, new Color(1f, 0.96f, 0.88f, 0.95f), 0.15f, false, 5) : null;
            Material mPud = DecalMaterial("SquarePuddle", tx.Blob, null, new Color(0.07f, 0.09f, 0.11f, 0.94f), 0.97f, true, 6);
            Material mWarm = DecalMaterial("SquareToneWarm", tx.Blob, null, new Color(0.66f, 0.50f, 0.30f, 0.34f), 0f, false, 1);
            Material mCool = DecalMaterial("SquareToneCool", tx.Blob, null, new Color(0.36f, 0.42f, 0.50f, 0.30f), 0f, false, 1);
            Material mRust = DecalMaterial("SquareToneRust", tx.Blob, null, new Color(0.55f, 0.28f, 0.16f, 0.26f), 0f, false, 1);
            string folder = DecalFolder;
            Place(root, "ToneWarm", SaveMesh(folder + "/SquareDecal_ToneWarm.asset", warm.ToMesh("SquareDecal_ToneWarm")), mWarm, false);
            Place(root, "ToneCool", SaveMesh(folder + "/SquareDecal_ToneCool.asset", cool.ToMesh("SquareDecal_ToneCool")), mCool, false);
            Place(root, "ToneRust", SaveMesh(folder + "/SquareDecal_ToneRust.asset", rust.ToMesh("SquareDecal_ToneRust")), mRust, false);
            Place(root, "Dirt", SaveMesh(folder + "/SquareDecal_Dirt.asset", dirt.ToMesh("SquareDecal_Dirt")), mDirt, false);
            Place(root, "Dust", SaveMesh(folder + "/SquareDecal_Dust.asset", dust.ToMesh("SquareDecal_Dust")), mDust, false);
            Place(root, "WetHalo", SaveMesh(folder + "/SquareDecal_WetHalo.asset", halo.ToMesh("SquareDecal_WetHalo")), mHalo, false);
            Place(root, "JointMoss", SaveMesh(folder + "/SquareDecal_JointMoss.asset", jointMoss.ToMesh("SquareDecal_JointMoss")), mJoint, false);
            Place(root, "Moss", SaveMesh(folder + "/SquareDecal_Moss.asset", moss.ToMesh("SquareDecal_Moss")), mMoss, false);
            Place(root, "CartRut", SaveMesh(folder + "/SquareDecal_Rut.asset", rut.ToMesh("SquareDecal_Rut")), mRut, false);
            if (mLeafA != null && leafA.V.Count > 0) Place(root, "LeafDriftsA", SaveMesh(folder + "/SquareDecal_LeavesA.asset", leafA.ToMesh("SquareDecal_LeavesA")), mLeafA, false);
            if (mLeafB != null && leafB.V.Count > 0) Place(root, "LeafDriftsB", SaveMesh(folder + "/SquareDecal_LeavesB.asset", leafB.ToMesh("SquareDecal_LeavesB")), mLeafB, false);
            Place(root, "Puddles", SaveMesh(folder + "/SquareDecal_Puddles.asset", pud.ToMesh("SquareDecal_Puddles")), mPud, false);
            Debug.Log("SC HOLLOWMERE square decals: " + drifts + " leaf drifts (T15a/T15b " + (mLeafA != null && mLeafB != null ? "ok" : "MISSING") + "), " + puddles.Length + " puddles, cart rut " + rutLen.ToString("F1") + " m, "
                + mossAt.Length + " moss patches, 14 dirt patches");
        }

        // A point along the square's border at 0..1 of its perimeter (S west to east, E south to north, N east to west, W north to south): position, direction, inward normal.
        static void Perimeter(float p, out Vector2 pos, out Vector2 dir, out Vector2 inward)
        {
            float x0 = HollowmereLayout.SquareMinX, x1 = HollowmereLayout.SquareMaxX, z0 = HollowmereLayout.SquareMinZ, z1 = HollowmereLayout.SquareMaxZ;
            float w = x1 - x0, h = z1 - z0, s = p * 2f * (w + h);
            if (s < w) { pos = new Vector2(x0 + s, z0); dir = new Vector2(1f, 0f); inward = new Vector2(0f, 1f); return; }
            s -= w;
            if (s < h) { pos = new Vector2(x1, z0 + s); dir = new Vector2(0f, 1f); inward = new Vector2(-1f, 0f); return; }
            s -= h;
            if (s < w) { pos = new Vector2(x1 - s, z1); dir = new Vector2(-1f, 0f); inward = new Vector2(0f, -1f); return; }
            s -= w;
            pos = new Vector2(x0, z1 - s); dir = new Vector2(0f, -1f); inward = new Vector2(1f, 0f);
        }

        // A draped quad: centre c, rotated by yaw (degrees clockwise from +Z), sx across (local X) by sz along (local Z), n x n cells, y = ground + lift. uv 0..1.
        static void AddGrid(TownLayout t, MeshBuf buf, Vector2 c, float yaw, float sx, float sz, int n, float lift, float uvScale)
        {
            float r = yaw * Mathf.Deg2Rad, sin = Mathf.Sin(r), cos = Mathf.Cos(r);
            var ids = new int[n + 1, n + 1];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n, v = j / (float)n;
                    float lx = (u - 0.5f) * sx, lz = (v - 0.5f) * sz;
                    float x = c.x + lx * cos + lz * sin, z = c.y - lx * sin + lz * cos;
                    ids[i, j] = buf.Add(new Vector3(x, GroundY(t, x, z) + lift, z), new Vector2(u * uvScale, v * uvScale));
                }
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) buf.Quad(ids[i, j], ids[i + 1, j], ids[i + 1, j + 1], ids[i, j + 1]);
        }

        // A draped ribbon along a Catmull-Rom curve through the waypoints, `width` wide, v = arc length / vMetres. Returns the length.
        static float Ribbon(TownLayout t, MeshBuf buf, Vector2[] way, float width, float vMetres, float lift)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < way.Length - 1; i++)
            {
                Vector2 p0 = way[Mathf.Max(0, i - 1)], p1 = way[i], p2 = way[i + 1], p3 = way[Mathf.Min(way.Length - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.5f));
                for (int k = 0; k < n; k++)
                {
                    float s = k / (float)n, s2 = s * s, s3 = s2 * s;
                    pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * s2 + (-p0 + 3f * p1 - 3f * p2 + p3) * s3));
                }
            }
            pts.Add(way[way.Length - 1]);
            float len = 0f; int prevL = -1, prevR = -1;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) len += Vector2.Distance(pts[i - 1], pts[i]);
                Vector2 tan = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized, side = new Vector2(tan.y, -tan.x) * (width * 0.5f);
                Vector2 a = pts[i] - side, b = pts[i] + side;
                int ia = buf.Add(new Vector3(a.x, GroundY(t, a.x, a.y) + lift, a.y), new Vector2(0f, len / vMetres));
                int ib = buf.Add(new Vector3(b.x, GroundY(t, b.x, b.y) + lift, b.y), new Vector2(1f, len / vMetres));
                if (prevL >= 0) buf.Quad(prevL, prevR, ib, ia);
                prevL = ia; prevR = ib;
            }
            return len;
        }

        // ------------------------------------------------------------------------------------------------ textures
        static DecalTextures MakeTextures()
        {
            var tx = new DecalTextures();
            string dir = DecalFolder;

            // organic blob: alpha only (white colour), a wobbling radius with a soft inner falloff
            const int bn = 256; var blob = new Color32[bn * bn]; var moss = new Color32[bn * bn]; var mossJ = new Color32[bn * bn];
            for (int y = 0; y < bn; y++)
                for (int x = 0; x < bn; x++)
                {
                    float u = (x + 0.5f) / bn * 2f - 1f, v = (y + 0.5f) / bn * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                    float R = 0.70f + 0.10f * Mathf.Sin(2f * th + 0.7f) + 0.07f * Mathf.Sin(3f * th + 2.1f) + 0.04f * Mathf.Sin(5f * th + 4.0f) + 0.10f * (VNoise(u * 2.4f + 5f, v * 2.4f + 9f, 3) - 0.5f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(R, R - 0.38f, r));
                    byte ab = (byte)Mathf.RoundToInt(a * 255f);
                    blob[y * bn + x] = new Color32(255, 255, 255, ab);
                    float mott = 0.55f * VNoise(u * 5f + 3f, v * 5f + 1f, 5) + 0.3f * VNoise(u * 11f + 7f, v * 11f + 2f, 6) + 0.15f * VNoise(u * 23f, v * 23f, 7);
                    float ma = a * Mathf.Clamp01(0.35f + 1.1f * mott);
                    Color g = Color.Lerp(new Color(0.30f, 0.44f, 0.16f), new Color(0.52f, 0.62f, 0.28f), mott);
                    moss[y * bn + x] = new Color32((byte)(g.r * 255f), (byte)(g.g * 255f), (byte)(g.b * 255f), (byte)Mathf.RoundToInt(ma * 255f));
                    // joint moss: a broken network of fine specks (moss lives in the joints, not as a wash), darker and more varied than the soft patches
                    float fine = 0.5f * VNoise(u * 14f + 4f, v * 14f + 8f, 21) + 0.32f * VNoise(u * 31f + 1f, v * 31f + 3f, 22) + 0.18f * VNoise(u * 63f, v * 63f, 23);
                    float ja = a * Mathf.SmoothStep(0.50f, 0.64f, fine);
                    Color gj = Color.Lerp(new Color(0.20f, 0.30f, 0.10f), new Color(0.46f, 0.55f, 0.22f), VNoise(u * 9f, v * 9f, 24));
                    mossJ[y * bn + x] = new Color32((byte)(gj.r * 255f), (byte)(gj.g * 255f), (byte)(gj.b * 255f), (byte)Mathf.RoundToInt(ja * 255f));
                }
            tx.Blob = WriteTex(dir + "/Blob_BaseColor.png", bn, bn, blob, true, false, true, true, 256);
            tx.Moss = WriteTex(dir + "/Moss_BaseColor.png", bn, bn, moss, true, false, true, true, 256);
            tx.MossJoint = WriteTex(dir + "/MossJoint_BaseColor.png", bn, bn, mossJ, true, false, true, true, 256);

            // cart rut: 128 across x 256 along (tiles along v every 4 m): two worn tracks 1.1 m apart in a 1.7 m ribbon, a faint hump between, broken by periodic noise
            const int rw = 128, rh = 256; var ra = new Color32[rw * rh]; var height = new float[rw * rh];
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    float u = (x + 0.5f) / rw, v = (y + 0.5f) / rh;
                    float tk = 0f;
                    foreach (float c in new[] { 0.5f - 0.32f, 0.5f + 0.32f })
                    {
                        float e = (u - c) / 0.075f; tk = Mathf.Max(tk, Mathf.Exp(-e * e));
                    }
                    float wob = PNoise(v * 6f, 0.5f, 6, 11), cut = PNoise(v * 12f, u * 3f, 12, 13);
                    float a = tk * Mathf.Clamp01(0.55f + 0.7f * (wob - 0.5f) + 0.5f * (cut - 0.5f));
                    a *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, Mathf.Min(u, 1f - u)));   // never a hard ribbon edge
                    height[y * rw + x] = -tk * (0.6f + 0.4f * wob);
                    ra[y * rw + x] = new Color32(34, 26, 20, (byte)Mathf.RoundToInt(a * 1.0f * 255f));
                }
            tx.RutAlbedo = WriteTex(dir + "/Rut_BaseColor.png", rw, rh, ra, true, false, true, false, 256);
            var rn = new Color32[rw * rh];
            for (int y = 0; y < rh; y++)
                for (int x = 0; x < rw; x++)
                {
                    float hl = height[y * rw + (x + rw - 1) % rw], hr = height[y * rw + (x + 1) % rw];
                    var nv = new Vector3(-(hr - hl) * 1.6f, 0f, 1f); nv.y = -(height[((y + 1) % rh) * rw + x] - height[((y + rh - 1) % rh) * rw + x]) * 0.4f; nv.Normalize();
                    rn[y * rw + x] = new Color32((byte)Mathf.RoundToInt((nv.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nv.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nv.z * 0.5f + 0.5f) * 255f), 255);
                }
            tx.RutNormal = WriteTex(dir + "/Rut_Normal.png", rw, rh, rn, false, true, false, false, 256);

            LeafSet("T15a_autumn_leaves", "T15a", out tx.LeafA, out tx.LeafANormal);
            LeafSet("T15b_fallen_leaves", "T15b", out tx.LeafB, out tx.LeafBNormal);
            return tx;
        }

        // Megascans leaf decal -> 1024 px albedo with alpha = Opacity x a soft rounded edge (a drift is one quad, no tiling), and the normal map copied.
        static void LeafSet(string srcName, string prefix, out Texture2D albedo, out Texture2D normal)
        {
            albedo = normal = null;
            string srcDir = BuilderUtil.Abs("Art/Source/Fab/" + srcName);
            if (!Directory.Exists(srcDir)) return;
            string Find(string tail) => Directory.GetFiles(srcDir, "*_" + tail + ".jpg").OrderBy(f => f, System.StringComparer.Ordinal).FirstOrDefault();
            string bc = Find("BaseColor"), op = Find("Opacity"), nr = Find("Normal");
            if (bc == null || op == null) return;
            Color32[] c = DecodeJpg(bc, out int w, out int h), o = DecodeJpg(op, out int w2, out int h2);
            if (w != w2 || h != h2 || w % 2 != 0) return;
            int ow = w / 2, oh = h / 2; var px = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { int i = (2 * y + dy) * w + 2 * x + dx; r += c[i].r; g += c[i].g; b += c[i].b; a += o[i].r; }
                    float u = (x + 0.5f) / ow * 2f - 1f, v = (y + 0.5f) / oh * 2f - 1f;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.62f, Mathf.Sqrt(u * u + v * v)));
                    px[y * ow + x] = new Color32((byte)(r / 4), (byte)(g / 4), (byte)(b / 4), (byte)Mathf.RoundToInt(a / 4f * edge));
                }
            albedo = WriteTex(DecalFolder + "/" + prefix + "_BaseColor.png", ow, oh, px, true, false, true, true, 1024);
            if (nr != null)
            {
                string dst = DecalFolder + "/" + prefix + "_Normal.jpg";
                if (B0Fab.CopyIfDifferent(nr, BuilderUtil.Abs(dst))) AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                SetImport(dst, false, true, false, true, 1024);
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            }
        }

        static Color32[] DecodeJpg(string file, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(t, File.ReadAllBytes(file), false)) throw new InvalidDataException("Cannot read " + file);
            w = t.width; h = t.height; Color32[] px = t.GetPixels32(); Object.DestroyImmediate(t);
            return px;
        }

        static Texture2D WriteTex(string path, int w, int h, Color32[] px, bool srgb, bool normal, bool alpha, bool clamp, int max)
        {
            bool wrote = BeautyCornerArt.WritePng(BuilderUtil.Abs(path), w, h, px);
            if (wrote || AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            SetImport(path, srgb, normal, alpha, clamp, max);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The decal folder is outside Content/Textures, so LookTestTextureImporter's suffix rules do not touch these files.
        static void SetImport(string path, bool srgb, bool normal, bool alpha, bool clamp, int max)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            TextureWrapMode wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            bool dirty = ti.textureType != type || ti.sRGBTexture != srgb || ti.alphaIsTransparency != alpha || ti.wrapMode != wrap || ti.maxTextureSize != max || !ti.mipmapEnabled || ti.anisoLevel < 8;
            if (!dirty) return;
            ti.textureType = type; ti.sRGBTexture = srgb; ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None; ti.alphaIsTransparency = alpha;
            ti.wrapMode = wrap; ti.maxTextureSize = max; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 8;
            ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.isReadable = false;
            ti.SaveAndReimport();
        }

        static float H(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041) + 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        static float VNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0, seed), H(x0 + 1, y0, seed), sx), Mathf.Lerp(H(x0, y0 + 1, seed), H(x0 + 1, y0 + 1, seed), sx), sy);
        }

        // Value noise on an integer lattice that wraps in x (period cells), so a texture tiles along v.
        static float PNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            int a = ((x0 % period) + period) % period, b = (((x0 + 1) % period) + period) % period;
            return Mathf.Lerp(Mathf.Lerp(H(a, y0, seed), H(b, y0, seed), sx), Mathf.Lerp(H(a, y0 + 1, seed), H(b, y0 + 1, seed), sx), sy);
        }
    }
}
