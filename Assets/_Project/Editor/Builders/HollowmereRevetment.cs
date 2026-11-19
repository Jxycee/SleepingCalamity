// Hollowmere's town-wall revetment (area 1 world, T08c round 2, 2026-11-17): the town wall continues down the steep outer bank as a battered fieldstone face.
// Seven terrain-paint rounds failed on the 45-72 degree bank under the wall (every terrain layer stretches into streaks and the morning sun lights it flat), so the bank
// is covered with stone instead (user decision 2026-11-17).
//   Plan (Prepare, before the grass is placed): the wall's outer runs are resampled every 0.5 m. At each station the terrain is probed outward from the wall face; where it
//   falls steeper than about 35 degrees the face is laid from the wall's foot down to the bottom of the fall (the ditch), 0.26 m proud of the terrain (the highest terrain under
//   each point's footprint, so convex shoulders never poke through). The lower 0.3-1.3 m of the face dives into the ground, so the foot is a ragged earth line, never a lip.
//   The face stops where a lane, a path-graph edge or a road crosses, with a pier (a thicker stack of stones and a side return) at each end.
//   Build (Ruined and Rebuilt roots): a merged mesh per 24 m of wall, one pyramid-faceted fieldstone per cell (4 triangles, courses 0.26-0.42 m, running bond), parametrised
//   along the wall run (u) and down the slope (v), so nothing stretches. The mesh carries WearLit wear masks in its vertex colour (R grime/soot, G moss): dark mortar rims,
//   moss and damp on the lower third and on north-facing runs, soot streaks under the cope. Ruined only: slumps every 15-30 m where stones are missing (earth fill,
//   rubble, fallen blocks), ivy curtains, and a rougher cope. Foot: ferns, shrubs and a few boulders right up to the stone; the terrain's grass carpet comes up to the stone.
//   The terrain stays the collider (no collider on the face, none on any piece). Authority: none (editor-time scene building).
// Deviation from the brief: the face follows the terrain (a skin), so its batter is the bank's own angle (about 50-70 degrees), not a fixed 70-75: a plane steeper than the
// ground would float over a void, a shallower one would be buried.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereRevetment
    {
        const string Adopted = "Assets/_Project/Content/Art/Town/Adopted/", Trees = "Assets/_Project/Content/Art/Town/Trees/";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const float Spacing = 0.5f;                 // station spacing along the wall
        const float FaceStart = 0.80f;              // first profile point, metres out from the wall's centre line (the wall's outer face is at 0.4)
        const float Lift = 0.26f;                   // how far the stone surface stands proud of the terrain (vertical)
        const float SteepDeg = 35f, EndDeg = 27f;   // the face starts where the ground falls steeper than SteepDeg, ends where it flattens below EndDeg or rises
        const float LaneMargin = 2.6f, PathMargin = 2.0f;
        const float MinSpan = 4f;
        const float ChunkLength = 24f;

        sealed class Station
        {
            public int Run, Index; public float U;
            public Vector2 C, N, T;       // centre-line point, outward unit normal, unit tangent (run direction)
            public float YTop;            // top of the cope
            public bool Valid, Blocked;
            public float DEnd, DSteep;
            public float[] Px, Py, Pz, V; // the face profile: positions and arc length from the first point
            public float VEnd;
            public float EndDist;         // metres to the nearest end of its span
        }

        sealed class Run { public int Id; public List<Station> S = new List<Station>(); public List<(int A, int B)> Spans = new List<(int, int)>(); }

        static TerrainData data; static TownLayout layout;
        static readonly List<Run> runs = new List<Run>();
        static readonly Dictionary<long, List<Vector2>> cover = new Dictionary<long, List<Vector2>>();
        static readonly List<(Vector2 A, Vector2 B)> pathEdges = new List<(Vector2, Vector2)>();
        static readonly List<(Vector2 Pos, float Radius)> cams = new List<(Vector2, float)>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, Bounds> bounds = new Dictionary<GameObject, Bounds>();
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static Material matGrey, matWarm, matDark, matCope, matEarth;
        static int totalTris, totalStones;

        static float H(float x, float z) => data.GetInterpolatedHeight((x - HollowmereTerrain.Origin.x) / HollowmereTerrain.Size.x, (z - HollowmereTerrain.Origin.z) / HollowmereTerrain.Size.z) + HollowmereTerrain.Origin.y;
        // The highest terrain under a point's footprint: a little inward (the step's shoulder), a little to each side and outward.
        static float HMax(Vector2 p, Vector2 n, Vector2 t)
        {
            float m = H(p.x, p.y);
            m = Mathf.Max(m, H(p.x - n.x * 0.30f, p.y - n.y * 0.30f));
            m = Mathf.Max(m, H(p.x + n.x * 0.12f, p.y + n.y * 0.12f));
            m = Mathf.Max(m, H(p.x + t.x * 0.30f, p.y + t.y * 0.30f));
            m = Mathf.Max(m, H(p.x - t.x * 0.30f, p.y - t.y * 0.30f));
            return m;
        }

        // ------------------------------------------------------------------------------------------------ queries
        public static bool Covers(float x, float z, float margin)
        {
            int cx = Mathf.FloorToInt(x / 2f), cz = Mathf.FloorToInt(z / 2f);
            float r = 0.55f + margin, r2 = r * r;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (cover.TryGetValue(Key(cx + dx, cz + dz), out List<Vector2> l))
                        foreach (Vector2 q in l) { float ex = q.x - x, ez = q.y - z; if (ex * ex + ez * ez < r2) return true; }
            return false;
        }
        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        // ------------------------------------------------------------------------------------------------ plan
        // Runs before the terrain's grass: needs only the terrain heights.
        public static void Prepare(Terrain terrain, TownLayout t)
        {
            data = terrain.terrainData; layout = t;
            runs.Clear(); cover.Clear(); pathEdges.Clear();
            var nodes = new Dictionary<string, Vector2>();
            foreach (var n in t.Nodes) nodes[n.Id] = new Vector2(n.Pos.X, n.Pos.Z);
            foreach (PathEdge e in t.Edges) if (nodes.TryGetValue(e.A, out Vector2 a) && nodes.TryGetValue(e.B, out Vector2 b)) pathEdges.Add((a, b));
            var wallRun = HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TownWallOffset, 16f), 0.5f);
            List<List<TownPoint>> lines = TownWallPlan.Runs(wallRun);
            int stationsTotal = 0, valid = 0, laneDropped = 0;
            for (int r = 0; r < lines.Count; r++)
            {
                List<TownPoint> pts = lines[r];
                float len = TownWallPlan.Length(pts);
                int n = Mathf.Max(2, Mathf.FloorToInt(len / Spacing) + 1);
                var run = new Run { Id = r };
                for (int i = 0; i < n; i++)
                {
                    float s = Mathf.Min(len, i * Spacing);
                    TownPoint p = TownWallPlan.PointAt(pts, s), pa = TownWallPlan.PointAt(pts, Mathf.Max(0f, s - 1.2f)), pb = TownWallPlan.PointAt(pts, Mathf.Min(len, s + 1.2f));
                    var st = new Station { Run = r, Index = i, U = s, C = new Vector2(p.X, p.Z) };
                    Vector2 tg = new Vector2(pb.X - pa.X, pb.Z - pa.Z); if (tg.sqrMagnitude < 1e-6f) tg = Vector2.right; tg.Normalize();
                    Vector2 nn = new Vector2(-tg.y, tg.x);
                    if (HollowmereLayout.PlateauDistance(st.C.x - nn.x, st.C.y - nn.y) > HollowmereLayout.PlateauDistance(st.C.x + nn.x, st.C.y + nn.y)) nn = -nn;
                    st.T = tg; st.N = nn;
                    run.S.Add(st);
                }
                // cope height: the highest ground along the wall's foot, minus the wall pivot rule's 0.1, plus the plinth; smoothed along the run
                var raw = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float m = float.MinValue;
                    for (int k = -4; k <= 4; k++) { Station q = run.S[Mathf.Clamp(i + k, 0, n - 1)]; m = Mathf.Max(m, H(q.C.x, q.C.y)); }
                    raw[i] = m + 0.25f;
                }
                for (int i = 0; i < n; i++) { float sum = 0f; int c = 0; for (int k = -4; k <= 4; k++) { sum += raw[Mathf.Clamp(i + k, 0, n - 1)]; c++; } run.S[i].YTop = sum / c; }
                // probe each station outward
                foreach (Station st in run.S)
                {
                    stationsTotal++;
                    ProbeStation(st);
                    if (st.Valid && LaneBad(st)) { st.Valid = false; st.Blocked = true; laneDropped++; }
                }
                // keep clear of a road: stations within 2.5 m of a blocked one drop too; a short invalid gap between valid ones is bridged
                var drop = new bool[n];
                for (int i = 0; i < n; i++) if (run.S[i].Blocked) for (int k = -5; k <= 5; k++) drop[Mathf.Clamp(i + k, 0, n - 1)] = true;
                for (int i = 0; i < n; i++) if (drop[i] && run.S[i].Valid) { run.S[i].Valid = false; laneDropped++; }
                for (int i = 1; i < n - 1; i++)
                    for (int gap = 1; gap <= 3; gap++)
                    {
                        if (i + gap >= n || run.S[i].Valid || !run.S[i - 1].Valid) break;
                        if (run.S[i + gap].Valid && !AnyBlocked(run, i - 1, i + gap)) { for (int k = i; k < i + gap; k++) { Station q = run.S[k]; q.Valid = true; q.DEnd = Mathf.Lerp(run.S[i - 1].DEnd, run.S[i + gap].DEnd, (k - i + 1f) / (gap + 1f)); } break; }
                    }
                // smooth the end distance along each valid stretch
                var de = new float[n];
                for (int i = 0; i < n; i++)
                {
                    if (!run.S[i].Valid) { de[i] = 0f; continue; }
                    float sum = 0f; int c = 0;
                    for (int k = -4; k <= 4; k++) { int j = i + k; if (j < 0 || j >= n || !run.S[j].Valid) continue; sum += run.S[j].DEnd; c++; }
                    de[i] = sum / c;
                }
                for (int i = 0; i < n; i++) if (run.S[i].Valid) run.S[i].DEnd = de[i];
                // spans
                int a = -1;
                for (int i = 0; i <= n; i++)
                {
                    bool v = i < n && run.S[i].Valid;
                    if (v && a < 0) a = i;
                    if (!v && a >= 0)
                    {
                        int b = i - 1;
                        if ((b - a) * Spacing >= MinSpan) run.Spans.Add((a, b)); else for (int k = a; k <= b; k++) run.S[k].Valid = false;
                        a = -1;
                    }
                }
                foreach (var sp in run.Spans) for (int i = sp.A; i <= sp.B; i++) run.S[i].EndDist = Mathf.Min(i - sp.A, sp.B - i) * Spacing;
                foreach (var sp in run.Spans) for (int i = sp.A; i <= sp.B; i++) BuildProfile(run.S[i]);
                foreach (Station st in run.S) if (st.Valid) { valid++; AddCover(st); }
                runs.Add(run);
            }
            Debug.Log("SC HOLLOWMERE revetment plan: runs=" + runs.Count + " stations=" + stationsTotal + " withFace=" + valid + " droppedAtRoads=" + laneDropped);
        }

        static bool AnyBlocked(Run run, int a, int b) { for (int k = a; k <= b; k++) if (run.S[k].Blocked) return true; return false; }

        static void ProbeStation(Station st)
        {
            const float step = 0.25f; const int count = 36;
            var hh = new float[count];
            for (int k = 0; k < count; k++) { float d = FaceStart + k * step; hh[k] = H(st.C.x + st.N.x * d, st.C.y + st.N.y * d); }
            int kS = -1;
            for (int k = 2; k < count; k++)
            {
                if (FaceStart + k * step > 4.6f) break;
                float deg = Mathf.Atan2(hh[k - 2] - hh[k], 2 * step) * Mathf.Rad2Deg;
                if (deg >= SteepDeg) { kS = k - 2; break; }
            }
            if (kS < 0) return;
            int kE = Mathf.Min(count - 1, kS + 30);
            for (int k = kS + 3; k < count; k++)
            {
                float deg = Mathf.Atan2(hh[k - 2] - hh[k], 2 * step) * Mathf.Rad2Deg;
                if (deg < EndDeg) { kE = k - 1; break; }
                if (hh[k] > hh[k - 1] + 0.01f) { kE = k - 1; break; }
            }
            // never past the lowest point of the fall (the ditch bottom): the counterscarp climbs again
            int lo = kS; for (int k = kS; k <= kE; k++) if (hh[k] < hh[lo]) lo = k;
            kE = Mathf.Min(kE, lo);
            if (kE <= kS + 1) return;
            st.DSteep = FaceStart + kS * step; st.DEnd = FaceStart + kE * step; st.Valid = true;
        }

        static bool LaneBad(Station st)
        {
            for (int k = 0; k < 4; k++)
            {
                float d = Mathf.Lerp(FaceStart, st.DEnd + 1.0f, k / 3f);
                float x = st.C.x + st.N.x * d, z = st.C.y + st.N.y * d;
                if (HollowmereTerrain.LaneEdgeAll(layout, x, z) < LaneMargin) return true;
                if (k > 0 && PathDistance(new Vector2(x, z)) < PathMargin) return true;   // (the path graph also runs just inside the wall: only outside counts)
            }
            return false;
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

        static float Bury(Station st) => 0.30f + 1.0f * Mathf.PerlinNoise(st.U * 0.42f + st.Run * 17.3f, 3.3f);   // metres of stone below the earth at the foot (0.3-1.3)

        static void BuildProfile(Station st)
        {
            int n = Mathf.Max(4, Mathf.CeilToInt((st.DEnd - FaceStart) / 0.25f) + 1);
            st.Px = new float[n]; st.Py = new float[n]; st.Pz = new float[n]; st.V = new float[n];
            var hmax = new float[n];
            for (int k = 0; k < n; k++)
            {
                float d = Mathf.Lerp(FaceStart, st.DEnd, k / (n - 1f));
                Vector2 p = st.C + st.N * d;
                st.Px[k] = p.x; st.Pz[k] = p.y; hmax[k] = HMax(p, st.N, st.T);
                st.Py[k] = hmax[k] + Lift;
            }
            float v = 0f; st.V[0] = 0f;
            for (int k = 1; k < n; k++) { v += new Vector3(st.Px[k] - st.Px[k - 1], st.Py[k] - st.Py[k - 1], st.Pz[k] - st.Pz[k - 1]).magnitude; st.V[k] = v; }
            st.VEnd = v;
            float bury = Bury(st), diveLen = Mathf.Min(0.95f, st.VEnd * 0.5f);
            float pier = 0.34f * (1f - Smooth(0.5f, 1.3f, st.EndDist));
            for (int k = 0; k < n; k++)
            {
                float lift = Lift + pier - (Lift + pier + bury) * Smooth(st.VEnd - diveLen, st.VEnd, st.V[k]);
                st.Py[k] = Mathf.Min(hmax[k] + lift, st.YTop - 0.12f + pier * 0.5f);
            }
        }

        static void AddCover(Station st)
        {
            for (int k = 0; k < st.Px.Length; k++)
            {
                var q = new Vector2(st.Px[k], st.Pz[k]);
                long key = Key(Mathf.FloorToInt(q.x / 2f), Mathf.FloorToInt(q.y / 2f));
                if (!cover.TryGetValue(key, out List<Vector2> l)) cover[key] = l = new List<Vector2>();
                l.Add(q);
            }
            // sample the footprint between profile points too (0.5 m)
            for (int k = 1; k < st.Px.Length; k++)
            {
                var q = new Vector2((st.Px[k] + st.Px[k - 1]) * 0.5f, (st.Pz[k] + st.Pz[k - 1]) * 0.5f);
                long key = Key(Mathf.FloorToInt(q.x / 2f), Mathf.FloorToInt(q.y / 2f));
                if (!cover.TryGetValue(key, out List<Vector2> l)) cover[key] = l = new List<Vector2>();
                l.Add(q);
            }
        }

        // ------------------------------------------------------------------------------------------------ surface evaluation
        static Station At(Run run, float u, out float f)
        {
            float x = Mathf.Clamp(u / Spacing, 0f, run.S.Count - 1.001f);
            int i = (int)x; f = x - i; return run.S[i];
        }

        static Vector3 ProfileAt(Station st, float v)
        {
            v = Mathf.Clamp(v, 0f, st.VEnd);
            int k = System.Array.BinarySearch(st.V, v); if (k < 0) k = ~k;
            if (k <= 0) return new Vector3(st.Px[0], st.Py[0], st.Pz[0]);
            if (k >= st.V.Length) k = st.V.Length - 1;
            float f = (v - st.V[k - 1]) / Mathf.Max(1e-5f, st.V[k] - st.V[k - 1]);
            return new Vector3(Mathf.Lerp(st.Px[k - 1], st.Px[k], f), Mathf.Lerp(st.Py[k - 1], st.Py[k], f), Mathf.Lerp(st.Pz[k - 1], st.Pz[k], f));
        }

        // The face surface at wall coordinate u and slope coordinate v, inside the span [u0, u1].
        static Vector3 S(Run run, float u, float v, float u0, float u1)
        {
            u = Mathf.Clamp(u, u0, u1);
            Station a = At(run, u, out float f);
            Station b = run.S[Mathf.Min(run.S.Count - 1, a.Index + 1)];
            if (!b.Valid || b.Px == null) b = a;
            return Vector3.Lerp(ProfileAt(a, v), ProfileAt(b, v), f);
        }

        static Vector3 SNormal(Run run, float u, float v, float u0, float u1, Station ref0)
        {
            Vector3 du = S(run, u + 0.12f, v, u0, u1) - S(run, u - 0.12f, v, u0, u1);
            Vector3 dv = S(run, u, v + 0.12f, u0, u1) - S(run, u, v - 0.12f, u0, u1);
            Vector3 nrm = Vector3.Cross(du, dv).normalized;
            Vector3 want = new Vector3(ref0.N.x, 0.5f, ref0.N.y);
            if (Vector3.Dot(nrm, want) < 0f) nrm = -nrm;
            return nrm;
        }

        // ------------------------------------------------------------------------------------------------ build
        public static void Build(Transform ruined, Transform rebuilt)
        {
            if (runs.Count == 0) return;
            prefabs.Clear(); bounds.Clear(); counts.Clear(); cams.Clear(); totalTris = 0; totalStones = 0;
            foreach (var c in HollowmereBuilder.Cameras.Concat(HollowmereRoad.Cameras))
            {
                Vector2 pos = new Vector2(c.Pos.x, c.Pos.z);
                if (c.Name == "Cap_HM_Eye_JettyBack") pos = HollowmereLakeArt.JettyEnd;
                bool strict = c.Name == "Cap_HM_Eye_Lake" || c.Name == "Cap_HM_Eye_JettyBack" || c.Name == "Cap_HM_Eye_Meadow" || c.Name == "Cap_HM_Eye_Mill";
                cams.Add((pos, strict ? 6f : 3f));
            }
            Materials();
            foreach (bool isRuined in new[] { true, false })
            {
                Transform parent = Group(isRuined ? ruined : rebuilt, "Revetment");
                for (int c = parent.childCount - 1; c >= 0; c--) Object.DestroyImmediate(parent.GetChild(c).gameObject);
                foreach (Run run in runs)
                    foreach (var sp in run.Spans)
                        BuildSpan(parent, run, sp, isRuined);
            }
            Debug.Log("SC HOLLOWMERE revetment: stones=" + totalStones + " triangles=" + totalTris + " (both presets), " + string.Join(", ", Pairs()));
        }

        static IEnumerable<string> Pairs() { var keys = new List<string>(counts.Keys); keys.Sort(System.StringComparer.Ordinal); foreach (string k in keys) yield return k + "=" + counts[k]; }
        static void Count(string k, int n = 1) { counts.TryGetValue(k, out int c); counts[k] = c + n; }

        static void Materials()
        {
            Shader wear = Shader.Find("SleepingCalamity/WearLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            Material src = LookTestBuilder.Mat("step_stone");
            matGrey = StoneMat("M_HM_Rev_Grey", wear, src, new Color(0.70f, 0.68f, 0.62f));
            matWarm = StoneMat("M_HM_Rev_Warm", wear, src, new Color(0.92f, 0.76f, 0.58f));
            matDark = StoneMat("M_HM_Rev_Dark", wear, src, new Color(0.40f, 0.40f, 0.39f));
            matCope = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_TW_Coping.mat") ?? matWarm;
            Material soil = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_GD_packed_soil.mat");
            matEarth = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_Rev_Earth.mat", Shader.Find("Universal Render Pipeline/Lit"));
            if (soil != null) { matEarth.CopyPropertiesFromMaterial(soil); foreach (var k in soil.enabledKeywords) matEarth.EnableKeyword(k.name); }
            matEarth.SetColor("_BaseColor", new Color(0.58f, 0.42f, 0.29f)); matEarth.SetColor("_Color", matEarth.GetColor("_BaseColor"));
            EditorUtility.SetDirty(matEarth);
            AssetDatabase.SaveAssets();
        }

        static Material StoneMat(string name, Shader wear, Material src, Color tint)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", wear);
            m.CopyPropertiesFromMaterial(src);
            foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
            Color c = new Color(Mathf.LinearToGammaSpace(tint.r), Mathf.LinearToGammaSpace(tint.g), Mathf.LinearToGammaSpace(tint.b), 1f);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetFloat("_BumpScale", 1.5f); m.SetShaderPassEnabled("MotionVectors", true);
            T23Walls.Skin(m, new T23Walls.Look { Set = null, StoneW = 0.55f, Moss = 0.45f, Foot = 0f, FootDark = 0f, Variation = 1f, DetailN = 0.5f });   // T23: the bank under the town wall (Meadow, JettyBack): per-slab tone, moss on the ledges, a second-scale grain
            EditorUtility.SetDirty(m);
            return m;
        }

        sealed class MB
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int>[] I = { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            // a flat triangle (a, b, c) wound to face `outward`, with per-vertex uv and colour
            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Color ca, Color cb, Color cc, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, outward) < 0f) { Vector3 tp = b; b = c; c = tp; Vector2 tu = ub; ub = uc; uc = tu; Color tc = cb; cb = cc; cc = tc; n = -n; }
                n.Normalize();
                int o = V.Count;
                V.Add(a); V.Add(b); V.Add(c); N.Add(n); N.Add(n); N.Add(n); UV.Add(ua); UV.Add(ub); UV.Add(uc); C.Add(ca); C.Add(cb); C.Add(cc);
                I[sub].Add(o); I[sub].Add(o + 1); I[sub].Add(o + 2);
            }
            public int Tris { get { int t = 0; foreach (var l in I) t += l.Count / 3; return t; } }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = V.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetColors(C);
                m.subMeshCount = I.Length;
                for (int s = 0; s < I.Length; s++) m.SetTriangles(I[s], s, false);
                m.RecalculateBounds(); m.RecalculateTangents();
                return m;
            }
        }

        static readonly Color NoWear = new Color(0f, 0f, 0f, 1f);

        static void BuildSpan(Transform parent, Run run, (int A, int B) sp, bool ruined)
        {
            float u0 = run.S[sp.A].U, u1 = run.S[sp.B].U;
            // slumps (Ruined only): centre, half width at the foot, top of the gap
            var breaches = new List<(float U, float W, float V0)>();
            if (ruined)
            {
                float u = u0 + 6f + 14f * Hash01(run.Id, sp.A, 71);
                while (u < u1 - 5f)
                {
                    breaches.Add((u, 2.0f + 2.2f * Hash01(run.Id, (int)(u * 4f), 72), 0.35f + 1.1f * Hash01(run.Id, (int)(u * 4f), 73)));
                    u += 15f + 15f * Hash01(run.Id, (int)(u * 4f), 74);
                }
            }
            // courses and stones, shared along the whole run so chunks line up
            int chunks = Mathf.Max(1, Mathf.CeilToInt((u1 - u0) / ChunkLength));
            for (int ch = 0; ch < chunks; ch++)
            {
                float ca = u0 + ch * (u1 - u0) / chunks, cb = u0 + (ch + 1) * (u1 - u0) / chunks;
                var mb = new MB();
                int stones = 0;
                float maxV = 0f;
                for (int i = Mathf.Max(sp.A, Mathf.FloorToInt(ca / Spacing)); i <= Mathf.Min(sp.B, Mathf.CeilToInt(cb / Spacing)); i++) maxV = Mathf.Max(maxV, run.S[i].VEnd);
                float v = 0f; int course = 0;
                while (v < maxV + 0.3f)
                {
                    float ch0 = 0.26f + 0.16f * Hash01(run.Id, course, 11);
                    float uStart = -Hash01(run.Id, course, 12) * 0.9f;
                    float uu = uStart; int idx = 0;
                    while (uu < cb)
                    {
                        float w = 0.48f + 0.52f * Hash01(run.Id * 1000 + course, idx, 13);
                        float su0 = uu, su1 = uu + w; uu = su1; idx++;
                        float cu = (su0 + su1) * 0.5f;
                        if (cu < ca || cu >= cb) continue;
                        float cu0 = Mathf.Max(su0, u0), cu1 = Mathf.Min(su1, u1);
                        if (cu1 - cu0 < 0.22f) continue;
                        stones += AddStone(mb, run, sp, ruined, breaches, course, idx, cu0, cu1, v, v + ch0 * (0.94f + 0.12f * Hash01(course, idx, 14)), u0, u1);
                    }
                    v += ch0; course++;
                }
                AddCope(mb, run, sp, ca, cb, ruined);
                if (ruined) AddEarth(mb, run, sp, breaches, ca, cb, u0, u1);
                if (mb.V.Count == 0) continue;
                string name = (ruined ? "RevRuined_r" : "RevRebuilt_r") + run.Id + "_s" + sp.A + "_c" + ch;
                Mesh mesh = mb.ToMesh(name);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { matGrey, matWarm, matDark, matCope, matEarth };
                mr.shadowCastingMode = ShadowCastingMode.On;
                totalTris += mb.Tris; totalStones += stones; Count("meshes");
            }
            if (sp.B - sp.A > 0) { AddEnds(parent, run, sp, ruined, u0, u1); Dressing(parent, run, sp, ruined, breaches, u0, u1); }
        }

        static int AddStone(MB mb, Run run, (int A, int B) sp, bool ruined, List<(float U, float W, float V0)> breaches, int course, int idx, float su0, float su1, float sv0, float sv1, float u0, float u1)
        {
            float cu = (su0 + su1) * 0.5f, cv = (sv0 + sv1) * 0.5f;
            Station st = At(run, cu, out _);
            float vEnd = st.VEnd;
            if (sv0 > vEnd - 0.12f) return 0;
            // ragged lower edge: the last course is cut where the earth covers it
            float rag = (Hash01(course, idx, 21) - 0.5f) * 0.5f;
            if (cv > vEnd - 0.2f + rag) return 0;
            float vFrac = cv / Mathf.Max(0.5f, vEnd);
            foreach (var b in breaches)
            {
                float over = (cv - b.V0) / Mathf.Max(0.5f, vEnd - b.V0);
                if (over < 0f) continue;
                float half = b.W * (0.28f + 0.95f * Smooth(0f, 1f, over)) + (Mathf.PerlinNoise(cu * 1.7f + 5f, cv * 1.9f + 9f) - 0.5f) * 1.1f;
                if (Mathf.Abs(cu - b.U) < half) return 0;
            }
            float ev = Mathf.Min(sv1, vEnd);
            Vector3 p0 = S(run, su0, sv0, u0, u1), p1 = S(run, su1, sv0, u0, u1), p2 = S(run, su1, ev, u0, u1), p3 = S(run, su0, ev, u0, u1);
            Vector3 nrm = SNormal(run, cu, cv, u0, u1, st);
            float relief = 0.025f + 0.055f * Hash01(course, idx, 22);
            if (ev < sv1 - 0.05f) relief *= 0.5f;
            float jx = (Hash01(course, idx, 23) - 0.5f) * 0.7f * (su1 - su0), jv = (Hash01(course, idx, 24) - 0.5f) * 0.7f * (ev - sv0);
            Vector3 ctr = S(run, cu + jx, (sv0 + ev) * 0.5f + jv, u0, u1) + nrm * relief;
            // masks
            float north = Smooth(-0.1f, 0.7f, st.N.y);
            float patch = Smooth(0.18f, 0.46f, Mathf.PerlinNoise(cu * 0.45f + run.Id * 9.1f, cv * 0.6f + 31f));
            float moss = (0.30f + 0.55f * Smooth(0.10f, 0.95f, vFrac) + 0.35f * north) * patch * (ruined ? 1f : 0.45f);   // (the ditch hides the lower third from the meadow, so the masks reach the whole face)
            moss = Mathf.Clamp(moss + 0.10f * Hash01(course, idx, 25), 0f, 0.78f);
            float damp = 0.55f * Smooth(0.15f, 0.9f, vFrac) * Mathf.Clamp01(0.3f + Mathf.PerlinNoise(cu * 0.8f + 77f, cv * 0.5f));
            float streak = ruined ? Smooth(0.60f, 0.76f, Mathf.PerlinNoise(cu * 1.35f + run.Id * 3.7f, 12.5f)) * (1f - Smooth(0.2f, Mathf.Min(3.2f, vEnd * 0.6f), cv)) * 0.75f : 0f;
            float grime = Mathf.Clamp01(Mathf.Max(damp, streak) + 0.08f * Hash01(course, idx, 26));
            float rim = 0.55f + 0.15f * Hash01(course, idx, 27);
            Color cC = new Color(grime, moss, 0f, 1f), cE = new Color(Mathf.Max(grime, rim), moss * 0.9f, 0f, 1f);
            // texture: each stone samples one patch of the tileable (a random offset per stone, scaled to sit inside one block)
            float ou = Hash01(course, idx, 28), ov = Hash01(course, idx, 29);
            Vector2 uv0 = new Vector2(ou, ov), uv1 = uv0 + new Vector2((su1 - su0) / 1.9f, 0f), uv2 = uv0 + new Vector2((su1 - su0) / 1.9f, (ev - sv0) / 1.9f), uv3 = uv0 + new Vector2(0f, (ev - sv0) / 1.9f), uvc = (uv0 + uv2) * 0.5f;
            float pick = Hash01(course, idx, 30);
            int sub = pick < 0.40f ? 0 : pick < 0.72f ? 1 : 2;
            if (moss > 0.5f && sub == 1) sub = 0;
            mb.Tri(sub, p0, p1, ctr, uv0, uv1, uvc, cE, cE, cC, nrm);
            mb.Tri(sub, p1, p2, ctr, uv1, uv2, uvc, cE, cE, cC, nrm);
            mb.Tri(sub, p2, p3, ctr, uv2, uv3, uvc, cE, cE, cC, nrm);
            mb.Tri(sub, p3, p0, ctr, uv3, uv0, uvc, cE, cE, cC, nrm);
            return 1;
        }

        // The cope: a flat top into the wall's foot and a chamfer down to the face, one strip per pair of stations.
        static void AddCope(MB mb, Run run, (int A, int B) sp, float ca, float cb, bool ruined)
        {
            int i0 = Mathf.Max(sp.A, Mathf.FloorToInt(ca / Spacing)), i1 = Mathf.Min(sp.B, Mathf.CeilToInt(cb / Spacing));
            Vector3 Pt(Station st, int row)
            {
                float yTop = st.YTop;
                if (row == 0) return new Vector3(st.C.x + st.N.x * 0.38f, yTop + 0.06f, st.C.y + st.N.y * 0.38f);
                if (row == 1) return new Vector3(st.C.x + st.N.x * 0.68f, yTop + 0.06f, st.C.y + st.N.y * 0.68f);
                return new Vector3(st.Px[0], st.Py[0], st.Pz[0]);
            }
            for (int i = i0; i < i1; i++)
            {
                Station a = run.S[i], b = run.S[i + 1];
                if (!a.Valid || !b.Valid || a.Px == null || b.Px == null) continue;
                Vector3 outward = new Vector3(a.N.x, 0.6f, a.N.y);
                for (int row = 0; row < 2; row++)
                {
                    Vector3 a0 = Pt(a, row), a1 = Pt(a, row + 1), b0 = Pt(b, row), b1 = Pt(b, row + 1);
                    float ua = a.U / 1.9f, ub = b.U / 1.9f;
                    Color cw = new Color(row == 0 ? 0.10f : 0.25f, ruined ? 0.12f : 0.05f, 0f, 1f);
                    Vector3 up = row == 0 ? Vector3.up : outward;
                    mb.Tri(3, a0, a1, b1, new Vector2(ua, row * 0.3f), new Vector2(ua, row * 0.3f + 0.3f), new Vector2(ub, row * 0.3f + 0.3f), cw, cw, cw, up);
                    mb.Tri(3, a0, b1, b0, new Vector2(ua, row * 0.3f), new Vector2(ub, row * 0.3f + 0.3f), new Vector2(ub, row * 0.3f), cw, cw, cw, up);
                }
            }
        }

        // Earth under each slump: a lumpy dark skin a little lower than the stones, 0.5 m cells.
        static void AddEarth(MB mb, Run run, (int A, int B) sp, List<(float U, float W, float V0)> breaches, float ca, float cb, float u0, float u1)
        {
            foreach (var b in breaches)
            {
                if (b.U + b.W * 1.4f < ca || b.U - b.W * 1.4f > cb) continue;
                Station st = At(run, b.U, out _);
                float vEnd = st.VEnd;
                for (float v = b.V0 - 0.1f; v < vEnd + 0.3f; v += 0.45f)
                    for (float u = b.U - b.W * 1.3f; u < b.U + b.W * 1.3f; u += 0.45f)
                    {
                        float cu = u + 0.225f, cv = v + 0.225f;
                        if (cu < ca || cu >= cb || cu < u0 || cu > u1) continue;
                        float over = (cv - b.V0) / Mathf.Max(0.5f, vEnd - b.V0);
                        float half = b.W * (0.28f + 0.95f * Smooth(0f, 1f, over)) + 0.5f;
                        if (over < -0.05f || Mathf.Abs(cu - b.U) > half) continue;
                        Station cs = At(run, cu, out _);
                        Vector3 Q(float uq, float vq)
                        {
                            Vector3 s = S(run, uq, Mathf.Min(vq, cs.VEnd), u0, u1);
                            Vector3 nn = new Vector3(cs.N.x, 0.55f, cs.N.y).normalized;
                            return s - nn * (Lift - 0.03f) + nn * 0.12f * (Mathf.PerlinNoise(uq * 1.3f + 3f, vq * 1.3f) - 0.5f);
                        }
                        Vector3 q0 = Q(u, v), q1 = Q(u + 0.45f, v), q2 = Q(u + 0.45f, v + 0.45f), q3 = Q(u, v + 0.45f);
                        Vector3 outward = new Vector3(cs.N.x, 0.6f, cs.N.y);
                        Color none = NoWear;
                        mb.Tri(4, q0, q1, q2, new Vector2(u, v) / 1.6f, new Vector2(u + 0.45f, v) / 1.6f, new Vector2(u + 0.45f, v + 0.45f) / 1.6f, none, none, none, outward);
                        mb.Tri(4, q0, q2, q3, new Vector2(u, v) / 1.6f, new Vector2(u + 0.45f, v + 0.45f) / 1.6f, new Vector2(u, v + 0.45f) / 1.6f, none, none, none, outward);
                    }
            }
        }

        // The span's two ends: a side return closing the skin down to the ground, so the end reads as a buttress, not a thin edge.
        static void AddEnds(Transform parent, Run run, (int A, int B) sp, bool ruined, float u0, float u1)
        {
            var mb = new MB();
            for (int end = 0; end < 2; end++)
            {
                Station st = run.S[end == 0 ? sp.A : sp.B];
                Vector3 outward = new Vector3(st.T.x * (end == 0 ? -1f : 1f), 0f, st.T.y * (end == 0 ? -1f : 1f));
                int n = st.Px.Length;
                for (int k = 1; k < n; k++)
                {
                    Vector3 a = new Vector3(st.Px[k - 1], st.Py[k - 1], st.Pz[k - 1]), b = new Vector3(st.Px[k], st.Py[k], st.Pz[k]);
                    Vector3 a2 = new Vector3(a.x, H(a.x, a.z) - 0.35f, a.z), b2 = new Vector3(b.x, H(b.x, b.z) - 0.35f, b.z);
                    Color c = new Color(0.30f, 0.12f, 0f, 1f);
                    Vector2 ua = new Vector2(st.V[k - 1], a.y) / 1.9f, ub = new Vector2(st.V[k], b.y) / 1.9f, ua2 = new Vector2(st.V[k - 1], a2.y) / 1.9f, ub2 = new Vector2(st.V[k], b2.y) / 1.9f;
                    mb.Tri(2, a, b, b2, ua, ub, ub2, c, c, c, outward);
                    mb.Tri(2, a, b2, a2, ua, ub2, ua2, c, c, c, outward);
                }
                // the cope's end
                Vector3 t0 = new Vector3(st.C.x + st.N.x * 0.38f, st.YTop + 0.06f, st.C.y + st.N.y * 0.38f), t1 = new Vector3(st.C.x + st.N.x * 0.68f, st.YTop + 0.06f, st.C.y + st.N.y * 0.68f);
                Vector3 f0 = new Vector3(st.Px[0], st.Py[0], st.Pz[0]);
                Vector3 g0 = new Vector3(t0.x, st.YTop - 0.5f, t0.z), g1 = new Vector3(t1.x, st.YTop - 0.5f, t1.z);
                Color cc = new Color(0.25f, 0.1f, 0f, 1f);
                mb.Tri(3, t0, t1, g1, Vector2.zero, Vector2.right, Vector2.one, cc, cc, cc, outward);
                mb.Tri(3, t0, g1, g0, Vector2.zero, Vector2.one, Vector2.up, cc, cc, cc, outward);
                mb.Tri(3, t1, f0, g1, Vector2.zero, Vector2.right, Vector2.one, cc, cc, cc, outward);
            }
            if (mb.V.Count == 0) return;
            string name = (ruined ? "RevRuined_r" : "RevRebuilt_r") + run.Id + "_s" + sp.A + "_ends";
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { matGrey, matWarm, matDark, matCope, matEarth };
            totalTris += mb.Tris;
        }

        // ------------------------------------------------------------------------------------------------ dressing: ivy, foot plants, rubble
        static void Dressing(Transform parent, Run run, (int A, int B) sp, bool ruined, List<(float U, float W, float V0)> breaches, float u0, float u1)
        {
            Transform dress = Group(parent, "Dress_r" + run.Id + "_s" + sp.A);
            // ivy curtains
            GameObject ivy = Prefab(Adopted + "T12c/T12c.prefab");
            if (ivy != null)
            {
                float u = u0 + 3f + 8f * Hash01(run.Id, sp.A, 81);
                while (u < u1 - 2f)
                {
                    if (Hash01(run.Id, (int)(u * 3f), 82) < (ruined ? 0.8f : 0.3f))
                    {
                        bool inBreach = false; foreach (var b in breaches) if (Mathf.Abs(u - b.U) < b.W + 1.5f) inBreach = true;
                        if (!inBreach) PlaceIvy(dress, ivy, run, u, u0, u1, 2.2f + 1.6f * Hash01(run.Id, (int)(u * 3f), 83));
                    }
                    u += 4f + 6f * Hash01(run.Id, (int)(u * 3f), 84);
                }
            }
            // foot plants and boulders, rubble and fallen blocks under each slump
            GameObject fern = Prefab(Adopted + "T12b/T12b.prefab");
            GameObject[] shrubs = { Tree("TR05"), Tree("TR06"), Tree("TR07") };
            GameObject boulder = Prefab(Adopted + "N02_angular_rock/N02.prefab");
            GameObject[] rubble = { Prefab(Adopted + "T05b/T05b.prefab"), Prefab(Adopted + "T05c/T05c.prefab"), Prefab(Adopted + "N03_scree_cluster/N03.prefab") };
            float f = u0 + 1f;
            int n = 0;
            while (f < u1 - 1f)
            {
                n++;
                Station st = At(run, f, out _);
                if (st.Px == null) { f += 3f; continue; }
                float roll = Hash01(run.Id, n, 91);
                Vector2 foot = new Vector2(st.Px[st.Px.Length - 1], st.Pz[st.Pz.Length - 1]);
                Vector2 side = st.T;
                if (fern != null && roll < 0.85f)
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 q = foot + st.N * Mathf.Lerp(-0.1f, 1.4f, Hash01(n, k, 92)) + side * Mathf.Lerp(-1.1f, 1.1f, Hash01(n, k, 93));
                        PutHeight(dress, fern, q, Mathf.Lerp(0.5f, 0.95f, Hash01(n, k, 94)), Hash01(n, k, 95) * 360f, "Fern", "ferns");
                    }
                if (roll > 0.45f && roll < 0.80f)
                {
                    GameObject sh = shrubs[(int)(Hash01(run.Id, n, 96) * 2.999f)];
                    if (sh != null) PutHeight(dress, sh, foot + st.N * 1.0f + side * Mathf.Lerp(-0.8f, 0.8f, Hash01(n, 0, 97)), Mathf.Lerp(1.2f, 2.3f, Hash01(n, 1, 97)), Hash01(n, 2, 97) * 360f, "Shrub", "shrubs");
                }
                if (boulder != null && roll > 0.88f)
                    PutExt(dress, boulder, foot + st.N * 0.6f + side * 0.5f, Mathf.Lerp(1.1f, 2.1f, Hash01(n, 0, 98)), 0.4f, Hash01(n, 1, 98) * 360f, "Boulder", "boulders");
                f += 1.4f + 2.2f * Hash01(run.Id, n, 99);
            }
            if (ruined)
                foreach (var b in breaches)
                {
                    Station st = At(run, b.U, out _);
                    if (st.Px == null) continue;
                    int pieces = 5 + (int)(Hash01(run.Id, (int)(b.U * 4f), 101) * 4f);
                    for (int k = 0; k < pieces; k++)
                    {
                        float uu = Mathf.Clamp(b.U + (Hash01(k, run.Id, 102) - 0.5f) * 2f * b.W * 1.5f, u0, u1);
                        Station sk = At(run, uu, out _);
                        float vv = Mathf.Lerp(Mathf.Max(0.2f, sk.VEnd - 3.0f), sk.VEnd + 0.4f, Hash01(k, run.Id, 103));
                        Vector3 pos = S(run, uu, vv, u0, u1);
                        GameObject pf = rubble[(int)(Hash01(k, run.Id, 104) * 2.999f)];
                        if (pf != null) PutExt(dress, pf, new Vector2(pos.x, pos.z), Mathf.Lerp(0.7f, 1.7f, Hash01(k, run.Id, 105)), 0.25f, Hash01(k, run.Id, 106) * 360f, "Rubble", "rubble");
                    }
                }
        }

        static void PlaceIvy(Transform dress, GameObject ivy, Run run, float u, float u0, float u1, float faceW)
        {
            Station st = At(run, u, out _);
            if (st.Px == null) return;
            float targetH = Mathf.Min(st.VEnd * 0.72f, 3.6f);
            Vector3 top = S(run, u, 0.15f, u0, u1);
            Vector3 nrm = SNormal(run, u, 0.8f, u0, u1, st);
            Bounds b0 = Measure(ivy);
            Vector3 sz = b0.size;
            int thin = sz.x <= sz.y && sz.x <= sz.z ? 0 : sz.y <= sz.z ? 1 : 2;
            int tall = -1; float tallV = -1f;
            for (int a = 0; a < 3; a++) if (a != thin && sz[a] > tallV) { tallV = sz[a]; tall = a; }
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            float s = Mathf.Max(0.05f, targetH / Mathf.Max(0.05f, tallV));
            float wide = sz[3 - thin - tall] * s;
            if (wide > faceW * 1.4f) s *= faceW * 1.4f / wide;
            Quaternion src = Quaternion.LookRotation(Axis(thin), Axis(tall)), dst = Quaternion.LookRotation(nrm, Vector3.up);
            Quaternion rot = dst * Quaternion.Inverse(src);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(ivy, dress);
            go.name = "Ivy";
            go.transform.rotation = rot; go.transform.localScale = Vector3.one * s;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            Vector3 want = top + nrm * (sz[thin] * s * 0.5f + 0.12f) - Vector3.up * (tallV * s * 0.5f - 0.1f);
            go.transform.position = want - rot * Vector3.Scale(b0.center, Vector3.one * s);
            Count("ivy");
        }

        static bool CamOk(Vector2 q) { foreach (var c in cams) if (Vector2.Distance(c.Pos, q) < c.Radius) return false; return true; }
        static bool Free(Vector2 q) => CamOk(q) && HollowmereLayout.DistanceToPolyline(new TownPoint(q.x, q.y), HollowmereLayout.Stream) > 2.5f
            && HollowmereTerrain.LaneEdgeAll(layout, q.x, q.y) > 1.5f && PathDistance(q) > 1.5f && H(q.x, q.y) > HollowmereLayout.WaterY + 0.9f;

        static void PutHeight(Transform parent, GameObject prefab, Vector2 q, float height, float yaw, string name, string key)
        {
            if (!Free(q)) return;
            Bounds b = Measure(prefab);
            float s = height / Mathf.Max(0.05f, b.max.y);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(q.x, H(q.x, q.y) - 0.04f * height, q.y);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = Vector3.one * s;
            foreach (Collider cl in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cl);
            Count(key);
        }

        static void PutExt(Transform parent, GameObject prefab, Vector2 q, float ext, float sink, float yaw, string name, string key)
        {
            if (!Free(q)) return;
            Bounds b = Measure(prefab);
            float s = Mathf.Clamp(ext / Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z)), 0.02f, 20f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(q.x, H(q.x, q.y) - sink * b.size.y * s, q.y);
            go.transform.rotation = Quaternion.Euler((Hash01((int)(q.x * 10f), (int)(q.y * 10f), 111) - 0.5f) * 30f, yaw, (Hash01((int)(q.y * 10f), (int)(q.x * 10f), 112) - 0.5f) * 30f);
            go.transform.localScale = Vector3.one * s;
            foreach (Collider cl in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cl);
            Count(key);
        }

        static GameObject Prefab(string path)
        {
            if (prefabs.TryGetValue(path, out GameObject cached)) return cached;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) Debug.LogWarning("SC HOLLOWMERE revetment: prefab missing, skipped: " + path);
            prefabs[path] = p;
            return p;
        }
        static GameObject Tree(string code) => Prefab(Trees + code + "/" + code + ".prefab");

        static Bounds Measure(GameObject prefab)
        {
            if (bounds.TryGetValue(prefab, out Bounds cached)) return cached;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); tmp.transform.localScale = Vector3.one;
            Bounds b = default; bool any = false;
            LODGroup lod = tmp.GetComponent<LODGroup>();
            Renderer[] rs = lod != null && lod.lodCount > 0 ? lod.GetLODs()[0].renderers : tmp.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in rs) { if (r == null) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Object.DestroyImmediate(tmp);
            if (!any) b = new Bounds(Vector3.up * 0.5f, Vector3.one);
            bounds[prefab] = b;
            return b;
        }

        static Transform Group(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        // A stable 0..1 hash of two integers and a salt (integer math only, the same on every machine).
        static float Hash01(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
