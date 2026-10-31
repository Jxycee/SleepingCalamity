// Hollowmere water (Q10, 2026-10-31): replaces the greybox "Lake" box and "Stream" boxes with the SC_Water shader on procedural meshes saved as assets.
// Why: the ravine floor along the Stream is flat at about -14 m, BELOW the lake (-12), so the stream cannot be one sloped sheet. It is a flight of stepped pools
// held by rock weirs (design: the data table below), each pool a corridor mesh at its own level, each weir a short sloped water ribbon (foam on the fall and
// a run-out below it) over a stone sill; the last fall is the mill tail, and below it the stream joins the lake as one surface. The lake is a 1 m grid at
// the lake level, clipped to cells whose ground is below level + ClipAbove, flood-filled from the lake centre (so no stray hollow gets water) and cut off
// upstream of the mill tail. Heights are sampled from the built terrain; neither the terrain nor the layout is changed.
// Meshes: Assets/_Project/Content/Terrain/Hollowmere/HM_Water_*.asset; material M_HM_WaterSurface (SC_Water; not M_HM_Water, which would collide with the
// greybox M_HM_water on case-insensitive file systems). Reflection probes: one over the lake, one along the ravine (baked at capture time like the corner's).
// Round 2 knob changes (one per knob; before -> after):
//   falls: flow foam was saturate((R*0.95 - streak*1.1 + 0.05)*2.2) = a solid slab on the fall; now saturate((R*0.5 - streak)*6 + 0.5), streak noise stretched along the flow
//          (u freq 1.8/4.7 -> 3.0/6.5, v freq 0.55/1.3 -> 0.3/0.8): ~50% broken streaks at R = 1, fading with the run-out (SC_Water.shader)
//   shore foam: new gate smoothstep(0.02, 0.04, water column), so a sheet over flat ground (column < 3 cm) shows none (SC_Water.shader)
//   FlatClip: new, 0 -> 0.05 m: lake and pool cells whose four corners are all within 0.05 m of the level are not built (no sheet lying on flat ground)
//   pool colour (EnsureMaterial): _Absorb (0.85,0.30,0.42) -> (0.60,0.30,0.50); _ShallowColor (0.30,0.40,0.16) -> (0.30,0.34,0.14);
//          _DeepColor (0.015,0.13,0.15) -> (0.035,0.115,0.12); warmer olive/brown where the bed shows, muted blue-green in depth
// Round 2b (coordinator addendum):
//   ripples: _NormalScale 0.2 -> 0.08 (shader multiplies by 1 + 1.5 * saturate(G * 3.3): lake x1, pools x2.5 = 0.2), _Tile1 7 -> 16 m, _Tile2 3.1 -> 9 m (larger, softer)
//   colour/reflection: _Absorb (0.60,0.30,0.50) -> (0.55,0.34,0.42); _ShallowColor (0.30,0.34,0.14) -> (0.26,0.28,0.18); _DeepColor (0.035,0.115,0.12) -> (0.06,0.10,0.12);
//          _Scatter 0.55 -> 0.40; _FresnelMin 0.025 -> 0.06; _ReflStrength 1.0 -> 1.3 (the sky does most of the work, like Reference/hollowmere/lake/jetty_small_lake_pier.jpg)
//   falls: vertex R along a fall 1 everywhere -> 0.3 + 0.7 * (2f - 1)^2 (foam at the lip and the base only; the boulders break the ribbon)
//   mill-tail shore band: _FoamWidth 0.10 -> 0.06, _ShoreFoam 0.5 -> 0.4 (thin, broken)
// Authority: none (editor builder, no networking). Called from HollowmereBuilder.BuildGround.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereWater
    {
        // ---------------------------------------------------------------------------------------------------------------- tuning data
        // s = arc length in metres along HollowmereLayout.Stream from its first point (the spring head at (130,-15)); negative s extends the first segment backwards.
        public const float HeadS = -8f;                  // the head bowl: the first pool starts here (clipped by the ground)
        public const float HalfWidth = 8f;               // corridor half width: the channel is ~12 m wide at -11, rims 6-7 m out
        public const float ClipAbove = 0.4f;             // a cell gets water when any corner ground is below level + this
        public const float FlatClip = 0.05f;             // a cell whose four corners are all within this of the level is dropped (flat ground at the waterline)
        public const float RunoutLength = 4f;            // foam run-out below each fall (m)
        // Pool surface levels, top to bottom: Reach 1 (spring, s 0..30), Reach 2 (30..52), Reach 3 (52..mill, under the mill culvert), then the lake.
        public static readonly float[] PoolLevels = { -10.85f, -11.15f, -11.45f, -12.0f };
        // The falls: W1, W2 (rock weirs) and W3 (the mill tail, where the ravine re-opens below the pad at s ~79-81). Fall i drops PoolLevels[i] -> [i+1].
        // S = start of the fall's run (upstream edge), Run = its length along the stream. W3 starts from -11.45 (Reach 3's level) rather than the briefed -11.15 so the
        // water is continuous under the mill; set PoolLevels[2] to -11.15 to get the briefed 0.85 m drop.
        public static readonly (string Name, float S, float Run)[] Falls = { ("W1", 30f, 1.2f), ("W2", 52f, 1.2f), ("W3", 79f, 1.6f) };

        public const string MeshFolder = HollowmereTerrain.Folder;
        public const string MaterialFolder = "Assets/_Project/Content/Materials/Hollowmere";
        public const string MaterialPath = MaterialFolder + "/M_HM_WaterSurface.mat";
        public const string NormalPath = "Assets/_Project/Content/Textures/Water/T_Water_N.png";
        const float WeirBottom = -16f;

        // ---------------------------------------------------------------------------------------------------------------- entry point
        public static void Build(Transform set, TownLayout t, Terrain terrain)
        {
            BuilderUtil.EnsureFolder(MeshFolder); BuilderUtil.EnsureFolder(MaterialFolder);
            Material water = EnsureMaterial();
            Material stone = LookTestBuilder.Mat("sandstone");
            float baseY = terrain.GetPosition().y;
            System.Func<float, float, float> ground = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + baseY;

            Transform lake = Child(set, "Lake"), stream = Child(set, "Stream");
            Place(lake, "LakeSurface", LakeMesh(ground), water, false);
            for (int i = 0; i < 3; i++)
            {
                float s0 = i == 0 ? HeadS : Falls[i - 1].S + Falls[i - 1].Run, s1 = Falls[i].S;
                Place(stream, "Reach" + (i + 1), Corridor("HM_Water_Reach" + (i + 1), ground, s0, s1, PoolLevels[i]), water, false);
            }
            Place(stream, "Falls", FallsMesh(ground), water, false);
            Place(stream, "Weirs", WeirsMesh(), stone, true);
            HollowmereLakeArt.Build(set, t, terrain);   // jetty, reeds, stones and boulders on and around the water (HollowmereLakeArt.cs)

            // Reflection probes: baked at capture time (LookTestCapture.RenderProbe), realtime-by-script in the scene like the corner's.
            Transform probes = Child(lake, "Probes");
            AddProbe(probes, "ReflectionProbe_Lake", new Vector3(10f, -8f, -100f), new Vector3(190f, 60f, 90f));
            Frame(24f, out Vector3 rp, out _);
            AddProbe(probes, "ReflectionProbe_Ravine", new Vector3(rp.x - 4f, PoolLevels[1] + 3f, rp.z - 6f), new Vector3(70f, 24f, 90f));
            AssetDatabase.SaveAssets();
            Debug.Log("SC HOLLOWMERE water built: lake + 3 reaches + 3 falls");
        }

        static Material EnsureMaterial()
        {
            Shader sh = Shader.Find("SleepingCalamity/Water") ?? throw new System.InvalidOperationException("SleepingCalamity/Water shader not found");
            TextureImporter imp = AssetImporter.GetAtPath(NormalPath) as TextureImporter ?? throw new System.IO.FileNotFoundException(NormalPath);
            if (imp.textureType != TextureImporterType.NormalMap || imp.wrapMode != TextureWrapMode.Repeat || !imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.NormalMap; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.anisoLevel = 8;
                imp.SaveAndReimport();
            }
            Material m = LookTestBuilder.Ensure(MaterialPath, sh);
            m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
            // The look numbers live here (a material keeps the defaults it was created with, so they are set on every build).
            m.SetFloat("_NormalScale", 0.08f); m.SetFloat("_Tile1", 16f); m.SetFloat("_Tile2", 9f); m.SetFloat("_NormalFadeStart", 18f);
            m.SetFloat("_FoamWidth", 0.06f); m.SetFloat("_ShoreFoam", 0.4f); m.SetFloat("_FlowFoam", 0.35f); m.SetColor("_FoamColor", new Color(0.74f, 0.80f, 0.78f, 1f));
            m.SetColor("_Absorb", new Color(0.55f, 0.34f, 0.42f, 1f)); m.SetColor("_ShallowColor", new Color(0.26f, 0.28f, 0.18f, 1f)); m.SetColor("_DeepColor", new Color(0.06f, 0.10f, 0.12f, 1f));
            m.SetFloat("_Scatter", 0.40f); m.SetFloat("_FresnelMin", 0.06f); m.SetFloat("_ReflStrength", 1.3f);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Child(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        static void Place(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static void AddProbe(Transform parent, string name, Vector3 worldPos, Vector3 size)
        {
            LookTestBuilder.AddProbe(parent, Vector3.zero, size);
            Transform p = parent.GetChild(parent.childCount - 1);
            p.name = name; p.position = worldPos;
            var probe = p.GetComponent<ReflectionProbe>();
            probe.importance = 2; probe.resolution = 256;
        }

        // ---------------------------------------------------------------------------------------------------------------- the stream path
        static TownPoint[] Path => HollowmereLayout.Stream;
        static float[] cumulative;

        static void EnsureArcs()
        {
            if (cumulative != null) return;
            TownPoint[] p = Path;
            cumulative = new float[p.Length];
            for (int i = 1; i < p.Length; i++) cumulative[i] = cumulative[i - 1] + TownPoint.Distance(p[i - 1], p[i]);
        }

        // Point on the polyline at arc length s (extended along the first / last segment beyond the ends), at y 0.
        static Vector3 PosAt(float s)
        {
            EnsureArcs();
            TownPoint[] p = Path;
            int i = 1;
            while (i < p.Length - 1 && s > cumulative[i]) i++;
            TownPoint a = p[i - 1], b = p[i];
            float len = cumulative[i] - cumulative[i - 1], f = (s - cumulative[i - 1]) / len;
            return new Vector3(a.X + (b.X - a.X) * f, 0f, a.Z + (b.Z - a.Z) * f);
        }

        // Position and smoothed horizontal flow direction at s.
        internal static void Frame(float s, out Vector3 pos, out Vector3 fwd)
        {
            pos = PosAt(s);
            fwd = PosAt(s + 1.5f) - PosAt(s - 1.5f);
            fwd.y = 0f; fwd.Normalize();
        }

        // Nearest point on the polyline: its arc length and distance (the lake's cut-off and the foam run-out use it).
        internal static void Project(float x, float z, out float s, out float dist)
        {
            EnsureArcs();
            TownPoint[] p = Path;
            s = 0f; dist = float.MaxValue;
            for (int i = 1; i < p.Length; i++)
            {
                float ax = p[i - 1].X, az = p[i - 1].Z, dx = p[i].X - ax, dz = p[i].Z - az, l2 = dx * dx + dz * dz;
                float f = Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / l2);
                float d = Mathf.Sqrt((x - ax - dx * f) * (x - ax - dx * f) + (z - az - dz * f) * (z - az - dz * f));
                if (d < dist) { dist = d; s = cumulative[i - 1] + f * Mathf.Sqrt(l2); }
            }
        }

        // Foam (vertex colour R) below a fall: strongest at the fall's foot, gone after RunoutLength, thinner toward the sides.
        static float Runout(float s, float lateral)
        {
            float best = 0f;
            foreach (var f in Falls)
            {
                float t = (s - (f.S + f.Run)) / RunoutLength;
                if (t < 0f || t >= 1f) continue;
                best = Mathf.Max(best, Mathf.Pow(1f - t, 1.5f) * 0.5f * (1f - 0.55f * Mathf.Clamp01(lateral / HalfWidth)));
            }
            return best;
        }

        // ---------------------------------------------------------------------------------------------------------------- mesh plumbing
        sealed class Data
        {
            public readonly List<Vector3> P = new(), N = new(); public readonly List<Vector4> T = new(); public readonly List<Vector2> UV = new(); public readonly List<Color> C = new();
            public readonly List<int> Tris = new();
            public int Add(Vector3 p, Vector3 n, Vector3 tan, float w, Vector2 uv, Color c) { P.Add(p); N.Add(n); T.Add(new Vector4(tan.x, tan.y, tan.z, w)); UV.Add(uv); C.Add(c); return P.Count - 1; }
            // Triangle wound so its face looks up (water) .
            public void TriUp(int a, int b, int c)
            {
                if (Vector3.Cross(P[b] - P[a], P[c] - P[a]).y < 0f) { int x = b; b = c; c = x; }
                Tris.Add(a); Tris.Add(b); Tris.Add(c);
            }
            public void QuadUp(int a, int b, int c, int d) { TriUp(a, b, c); TriUp(a, c, d); }
        }

        static Mesh Save(string name, Data d, bool recalc)
        {
            string path = MeshFolder + "/" + name + ".asset";
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); } else m.Clear();
            m.name = name;
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(d.P); m.SetUVs(0, d.UV); m.SetColors(d.C); m.SetTriangles(d.Tris, 0);
            if (recalc) { m.RecalculateNormals(); m.RecalculateTangents(); }
            else { m.SetNormals(d.N); m.SetTangents(d.T); }
            m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------------------------------------------------------------------------------------------------------- lake
        static Mesh LakeMesh(System.Func<float, float, float> ground)
        {
            const float x0 = -85f, x1 = 105f, z0 = -150f, z1 = -55f, h = 1f;
            float level = PoolLevels[3];
            float cutS = Falls[2].S + Falls[2].Run - 1.2f;   // the lake starts a little above the mill-tail fall's foot (overlaps its foot, hidden by the sill)
            int nx = Mathf.RoundToInt((x1 - x0) / h), nz = Mathf.RoundToInt((z1 - z0) / h);
            var g = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++) g[i, j] = ground(x0 + i * h, z0 + j * h);

            var ok = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float lo = Mathf.Min(Mathf.Min(g[i, j], g[i + 1, j]), Mathf.Min(g[i, j + 1], g[i + 1, j + 1]));
                    if (lo >= level + ClipAbove) continue;
                    if (Flat(g[i, j], g[i + 1, j], g[i, j + 1], g[i + 1, j + 1], level)) continue;
                    Project(x0 + (i + 0.5f) * h, z0 + (j + 0.5f) * h, out float s, out float dist);
                    if (dist <= HalfWidth + 1.5f && s < cutS) continue;   // the ravine upstream of the mill tail belongs to the reaches
                    ok[i, j] = true;
                }
            // keep only what is connected to the lake centre
            var keep = new bool[nx, nz];
            int ci = Mathf.RoundToInt((HollowmereLayout.LakeCentre.X - x0) / h), cj = Mathf.RoundToInt((HollowmereLayout.LakeCentre.Z - z0) / h);
            if (!ok[ci, cj]) throw new System.InvalidOperationException("Lake centre cell has no water: ground " + g[ci, cj]);
            var q = new Queue<(int, int)>(); q.Enqueue((ci, cj)); keep[ci, cj] = true;
            int cells = 0;
            while (q.Count > 0)
            {
                var (i, j) = q.Dequeue(); cells++;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, b = j + dj;
                    if (a < 0 || b < 0 || a >= nx || b >= nz || keep[a, b] || !ok[a, b]) continue;
                    keep[a, b] = true; q.Enqueue((a, b));
                }
            }

            var d = new Data();
            var idx = new int[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++) idx[i, j] = -1;
            int V(int i, int j)
            {
                if (idx[i, j] >= 0) return idx[i, j];
                float x = x0 + i * h, z = z0 + j * h;
                Project(x, z, out float s, out float dist);
                // u = world x, v = -world z (a runout below the mill tail flows south); G = 0: the lake only drifts.
                idx[i, j] = d.Add(new Vector3(x, level, z), Vector3.up, Vector3.right, 1f, new Vector2(x, -z), new Color(Runout(s, dist), 0f, 0f, 1f));
                return idx[i, j];
            }
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    if (keep[i, j]) d.QuadUp(V(i, j), V(i + 1, j), V(i + 1, j + 1), V(i, j + 1));
            Debug.Log("SC HOLLOWMERE lake cells=" + cells);
            return Save("HM_Water_Lake", d, false);
        }

        // ---------------------------------------------------------------------------------------------------------------- pools and falls
        // A grid of rows along the stream (about 1 m) by columns across it (1 m), at one level, clipped where the ground is above level + ClipAbove.
        static Mesh Corridor(string name, System.Func<float, float, float> ground, float s0, float s1, float level)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt((s1 - s0))), cols = Mathf.RoundToInt(HalfWidth * 2f);
            var d = new Data();
            var pos = new Vector3[rows + 1, cols + 1]; var gr = new float[rows + 1, cols + 1]; var idx = new int[rows + 1, cols + 1];
            var rs = new float[rows + 1];
            for (int r = 0; r <= rows; r++)
            {
                rs[r] = Mathf.Lerp(s0, s1, r / (float)rows);
                Frame(rs[r], out Vector3 p, out Vector3 f);
                var c = new Vector3(-f.z, 0f, f.x);
                for (int k = 0; k <= cols; k++)
                {
                    float dd = -HalfWidth + k;
                    pos[r, k] = new Vector3(p.x + c.x * dd, level, p.z + c.z * dd);
                    gr[r, k] = ground(pos[r, k].x, pos[r, k].z);
                    idx[r, k] = -1;
                }
            }
            int V(int r, int k)
            {
                if (idx[r, k] >= 0) return idx[r, k];
                Frame(rs[r], out _, out Vector3 f);
                float dd = -HalfWidth + k;
                idx[r, k] = d.Add(pos[r, k], Vector3.up, new Vector3(-f.z, 0f, f.x), 1f, new Vector2(dd, rs[r]), new Color(Runout(rs[r], Mathf.Abs(dd)), 0.3f, 0f, 1f));
                return idx[r, k];
            }
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    float lo = Mathf.Min(Mathf.Min(gr[r, k], gr[r + 1, k]), Mathf.Min(gr[r, k + 1], gr[r + 1, k + 1]));
                    if (lo >= level + ClipAbove) continue;
                    if (Flat(gr[r, k], gr[r + 1, k], gr[r, k + 1], gr[r + 1, k + 1], level)) continue;
                    d.QuadUp(V(r, k), V(r + 1, k), V(r + 1, k + 1), V(r, k + 1));
                }
            return Save(name, d, false);
        }

        static bool Flat(float a, float b, float c, float d, float level) =>
            Mathf.Abs(a - level) < FlatClip && Mathf.Abs(b - level) < FlatClip && Mathf.Abs(c - level) < FlatClip && Mathf.Abs(d - level) < FlatClip;

        static float Ease(float t) => t * t * (3f - 2f * t);

        // The three sloped ribbons: level eases from the upper pool to the lower over Run; vertex colour R = 1 (foam), G = 1 (fast).
        static Mesh FallsMesh(System.Func<float, float, float> ground)
        {
            var d = new Data();
            const int rows = 5;
            int cols = Mathf.RoundToInt(HalfWidth * 2f);
            for (int w = 0; w < Falls.Length; w++)
            {
                var (_, s, run) = Falls[w];
                float up = PoolLevels[w], lo = PoolLevels[w + 1];
                var idx = new int[rows + 1, cols + 1]; var gr = new float[rows + 1, cols + 1];
                for (int r = 0; r <= rows; r++)
                    for (int k = 0; k <= cols; k++)
                    {
                        float f01 = r / (float)rows, ss = s + run * f01, dd = -HalfWidth + k;
                        Frame(ss, out Vector3 p, out Vector3 f);
                        var c = new Vector3(-f.z, 0f, f.x);
                        float y = Mathf.Lerp(up, lo, Ease(f01));
                        float slope = (lo - up) * 6f * f01 * (1f - f01) / run;         // dy/ds of the eased drop
                        Vector3 n = (Vector3.up - slope * f).normalized;
                        var pos = new Vector3(p.x + c.x * dd, y, p.z + c.z * dd);
                        gr[r, k] = ground(pos.x, pos.z);
                        float edge = 2f * f01 - 1f;   // foam at the lip and the base only
                        idx[r, k] = d.Add(pos, n, c, 1f, new Vector2(dd, ss), new Color(0.2f + 0.4f * edge * edge, 1f, 0f, 1f));
                    }
                for (int r = 0; r < rows; r++)
                    for (int k = 0; k < cols; k++)
                    {
                        float low = Mathf.Min(Mathf.Min(gr[r, k], gr[r + 1, k]), Mathf.Min(gr[r, k + 1], gr[r + 1, k + 1]));
                        if (low >= up + ClipAbove) continue;
                        d.QuadUp(idx[r, k], idx[r + 1, k], idx[r + 1, k + 1], idx[r, k + 1]);
                    }
            }
            return Save("HM_Water_Falls", d, false);
        }

        // ---------------------------------------------------------------------------------------------------------------- weir sills
        // A stone wedge under each fall (the ravine floor is ~3 m below the pools, so without it the ribbon would hang in the air): upstream face, crest at the
        // upper level - 0.15, the sloped top under the ribbon, a 0.5 m apron, the downstream face. Ends are buried in the banks.
        static Mesh WeirsMesh()
        {
            var d = new Data();
            const float half = HalfWidth + 0.7f;
            for (int w = 0; w < Falls.Length; w++)
            {
                var (_, s, run) = Falls[w];
                float up = PoolLevels[w] - 0.15f, lo = PoolLevels[w + 1] - 0.15f;
                var prof = new (float s, float y)[] { (s - 0.4f, WeirBottom), (s - 0.4f, up), (s, up), (s + run, lo), (s + run + 0.5f, lo), (s + run + 0.5f, WeirBottom) };
                Vector3 centre = Vector3.zero; int cnt = 0;
                var pts = new List<(Vector3 a, Vector3 b)>();
                foreach (var (ps, py) in prof)
                {
                    Frame(ps, out Vector3 p, out Vector3 f);
                    var c = new Vector3(-f.z, 0f, f.x);
                    Vector3 a = new Vector3(p.x - c.x * half, py, p.z - c.z * half), b = new Vector3(p.x + c.x * half, py, p.z + c.z * half);
                    pts.Add((a, b)); centre += a + b; cnt += 2;
                }
                centre /= cnt;
                float run2 = 0f;
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    var (a0, b0) = pts[i]; var (a1, b1) = pts[i + 1];
                    float len = Vector3.Distance(a0, a1);
                    int i0 = d.Add(a0, Vector3.up, Vector3.right, 1f, new Vector2(0f, run2 * 0.5f), Color.white), i1 = d.Add(b0, Vector3.up, Vector3.right, 1f, new Vector2(half, run2 * 0.5f), Color.white);
                    int i2 = d.Add(b1, Vector3.up, Vector3.right, 1f, new Vector2(half, (run2 + len) * 0.5f), Color.white), i3 = d.Add(a1, Vector3.up, Vector3.right, 1f, new Vector2(0f, (run2 + len) * 0.5f), Color.white);
                    run2 += len;
                    // wound to face away from the solid's centre
                    Vector3 mid = (a0 + b0 + a1 + b1) * 0.25f;
                    bool flip = Vector3.Dot(Vector3.Cross(d.P[i1] - d.P[i0], d.P[i2] - d.P[i0]), mid - centre) < 0f;
                    if (flip) { d.Tris.AddRange(new[] { i0, i2, i1, i0, i3, i2 }); }
                    else { d.Tris.AddRange(new[] { i0, i1, i2, i0, i2, i3 }); }
                }
            }
            return Save("HM_Water_Weirs", d, true);
        }
    }
}
