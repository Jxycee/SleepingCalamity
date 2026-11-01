// World traversal scan: the probe that runs over one ScanSpace. It bakes a NavMesh from the physics colliders with an agent matching the hero capsule
// (UnityEngine.AI.NavMeshBuilder, the engine API NavMeshSurface wraps: the agent comes from the hero prefab without adding a project agent type), rasterises the
// NavMesh into a 0.25 m grid of walk layers, and then runs physics queries against the hero's capsule:
//   reachability  every target within its reach of the spawn's NavMesh component (a separate "open" bake for targets behind a locked door)
//   intended paths every 0.25 m along lane / corridor / door lines: capsule fit, opening width (8-way casts), step height, slope, holes, collider vs mesh floor
//   grid          every walkable cell: visible meshes with no collider (walk-through), invisible colliders, narrow gaps, tight gaps, holes in planned floor
//   islands       NavMesh components cut off from the main one: ground-level islands, and pockets the hero can drop into but not climb or jump out of
//   stairs        every stair object: walkable, and both landings lead somewhere
//   keep-clear    dressing inside doorways, on stairs and door thresholds; colliders far bigger than their mesh
// Visible geometry is probed through temporary MeshColliders on a spare layer, removed before the probe returns. The probe never saves anything.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace SleepingCalamity.EditorTools
{
    public sealed class ScanOptions
    {
        public bool Full = true;           // false: reachability + intended-path fit only (the regression test)
        public float Cell = 0.25f;
        public float VoxelSize = 0.1f;
        public static ScanOptions Quick => new ScanOptions { Full = false };
    }

    // The NavMesh rasterised into a grid: up to two walk layers per cell (a bridge or stair over a lane), each with its component id, plus map flags.
    public sealed class WalkGrid
    {
        public const ushort FGround = 1, FNarrow = 2, FBlocked = 4, FStuck = 8, FWalkThrough = 16, FInvisible = 32, FHole = 64, FPath = 128, FIsland = 256, FTight = 512, FStair = 1024;
        public readonly float Cell; public readonly float MinX, MinZ; public readonly int W, H;
        public readonly float[] Y0, Y1; public readonly int[] C0, C1; public readonly ushort[] Flags;
        public int Main = -1;
        public float[] CompArea; public int Comps;

        public WalkGrid(Bounds area, float cell)
        {
            Cell = cell; MinX = area.min.x; MinZ = area.min.z;
            W = Mathf.Max(1, Mathf.CeilToInt(area.size.x / cell)); H = Mathf.Max(1, Mathf.CeilToInt(area.size.z / cell));
            int n = W * H; Y0 = new float[n]; Y1 = new float[n]; C0 = new int[n]; C1 = new int[n]; Flags = new ushort[n];
            for (int i = 0; i < n; i++) { C0[i] = -1; C1[i] = -1; }
        }

        public bool In(int x, int z) => x >= 0 && z >= 0 && x < W && z < H;
        public int Index(int x, int z) => z * W + x;
        public Vector2 CellCentre(int x, int z) => new Vector2(MinX + (x + 0.5f) * Cell, MinZ + (z + 0.5f) * Cell);
        public bool ToCell(Vector3 p, out int x, out int z) { x = Mathf.FloorToInt((p.x - MinX) / Cell); z = Mathf.FloorToInt((p.z - MinZ) / Cell); return In(x, z); }

        public void Add(int i, float y, int comp)
        {
            if (C0[i] < 0) { Y0[i] = y; C0[i] = comp; return; }
            if (Mathf.Abs(Y0[i] - y) < 0.3f) return;
            if (C1[i] >= 0 && Mathf.Abs(Y1[i] - y) < 0.3f) return;
            if (C1[i] >= 0) { if (y > Y1[i]) { Y1[i] = y; C1[i] = comp; } return; }
            if (y < Y0[i]) { Y1[i] = Y0[i]; C1[i] = C0[i]; Y0[i] = y; C0[i] = comp; } else { Y1[i] = y; C1[i] = comp; }
        }

        // The walk layer of cell i nearest to height y within tol, or -1.
        public int LayerNear(int i, float y, float tol, out float ly)
        {
            ly = 0f; int best = -1; float bd = tol;
            if (C0[i] >= 0 && Mathf.Abs(Y0[i] - y) <= bd) { bd = Mathf.Abs(Y0[i] - y); best = C0[i]; ly = Y0[i]; }
            if (C1[i] >= 0 && Mathf.Abs(Y1[i] - y) <= bd) { best = C1[i]; ly = Y1[i]; }
            return best;
        }

        public bool HasMain(int i) => Main >= 0 && (C0[i] == Main || C1[i] == Main);
        public bool HasAny(int i) => C0[i] >= 0;
    }

    public sealed class ScanResult
    {
        public ScanSpace Space; public WalkGrid Grid; public HeroCapsule Hero;
        public readonly List<TraversalIssue> Issues = new List<TraversalIssue>();
        public float Seconds; public int Colliders, VisualMeshes, Stairs;
        public float MainArea; public int Islands;
        public IEnumerable<TraversalIssue> Of(IssueKind k) => Issues.Where(i => i.Kind == k);
    }

    public static class TraversalProbe
    {
        const string VisualName = "__TraversalVisual";
        static readonly string[] SoftTokens =
        {
            "grass", "ivy", "leaf", "leaves", "foliage", "weed", "moss", "flower", "reed", "vine", "crown", "card", "rope", "washing", "banner", "cloth", "cobweb",
            "web", "light", "glow", "shaft", "haze", "fog", "pollen", "smoke", "decal", "water", "fall", "foam", "splash", "mist", "sky", "cloud", "particle",
            "petal", "shadow", "puddle", "label", "dust", "mote", "flame", "candle", "orb", "sign", "chain", "hanging", "lantern", "bunting", "awning", "branch", "twig",
            "hedge", "bush", "shrub", "fern", "root", "straw", "hay", "sapling", "tree", "cabbage", "crop", "veg", "ridge", "backdrop", "horizon", "distant",
        };
        static readonly string[] SoftShaders = { "decal", "water", "particle", "skybox", "fog", "foliage", "unlit" };
        static readonly string[] DressingTokens =
        {
            "dressing", "clutter", "prop", "barrel", "crate", "sack", "basket", "bucket", "jug", "cart", "planter", "vignette", "pot", "bench", "table", "chair",
            "keg", "shelf", "rack", "box", "stack", "debris", "rubble", "plank", "log", "wheel", "corner", "trough", "flowerbox", "candles", "bottle",
        };

        public static ScanResult Scan(ScanSpace space, HeroCapsule hero, ScanOptions opt)
        {
            float t0 = Time.realtimeSinceStartup;
            var res = new ScanResult { Space = space, Hero = hero };
            int visLayer = SpareLayer();
            int physMask = 0;
            for (int l = 0; l < 32; l++) if (l != visLayer && l != 2 && !Physics.GetIgnoreLayerCollision(hero.Layer, l)) physMask |= 1 << l;
            int visMask = 1 << visLayer;
            NavMesh.RemoveAllNavMeshData();
            Physics.SyncTransforms();

            WalkGrid open = null;
            // ---------------------------------------------------------------- reachability (open pass first, for targets behind a locked door)
            if (space.Locks.Count > 0 && space.Targets.Any(t => t.BehindLock))
            {
                foreach (GameObject g in space.Locks) if (g != null) g.SetActive(false);
                Physics.SyncTransforms();
                open = BakeGrid(space, hero, opt, physMask, visLayer, out _);
                foreach (TraversalTarget t in space.Targets.Where(t => t.BehindLock)) Reach(open, t);
                foreach (GameObject g in space.Locks) if (g != null) g.SetActive(true);
                Physics.SyncTransforms();
                NavMesh.RemoveAllNavMeshData();
            }
            WalkGrid grid = BakeGrid(space, hero, opt, physMask, visLayer, out NavMeshDataInstance inst);
            res.Grid = grid;
            res.MainArea = grid.Main >= 0 ? grid.CompArea[grid.Main] : 0f;
            foreach (TraversalTarget t in space.Targets.Where(t => !t.BehindLock)) Reach(grid, t);
            foreach (TraversalTarget t in space.Targets)
            {
                if (t.Reached) continue;
                Issue(res, IssueKind.Unreachable, t.Pos, float.IsInfinity(t.NearestGap) ? 99f : t.NearestGap, t.Reach, t.Kind + " '" + t.Name + "'" + (t.BehindLock ? " (behind the locked door, door open)" : "")
                    + (float.IsInfinity(t.NearestGap) ? ": no walkable ground of the main area within 6 m" : $": nearest main-area ground {t.NearestGap:0.00} m away (reach {t.Reach:0.0} m)"), null, t.Name);
                if (grid.ToCell(t.Pos, out int cx, out int cz)) grid.Flags[grid.Index(cx, cz)] |= WalkGrid.FBlocked;
            }

            var visuals = new List<GameObject>();
            try
            {
                if (opt.Full) res.VisualMeshes = AddVisuals(space.Root, visLayer, visuals);
                Physics.SyncTransforms();

                // ------------------------------------------------------------ intended paths
                foreach (IntendedPath p in space.Paths) WalkPath(res, grid, p, hero, physMask, visMask, opt.Full);

                if (opt.Full)
                {
                    GridPass(res, grid, hero, physMask, visMask);
                    Islands(res, grid, open, hero, physMask);
                    Stairs(res, grid, hero);
                    KeepClearZones(res, hero, physMask, visMask);
                    Oversized(res, visLayer);
                }
            }
            finally
            {
                foreach (GameObject g in visuals) if (g != null) Object.DestroyImmediate(g);
                inst.Remove();
                NavMesh.RemoveAllNavMeshData();
                Physics.SyncTransforms();
            }
            List<TraversalIssue> clustered = Cluster(res.Issues);
            res.Issues.Clear(); res.Issues.AddRange(clustered);
            res.Seconds = Time.realtimeSinceStartup - t0;
            return res;
        }

        static int SpareLayer()
        {
            for (int l = 31; l >= 8; l--) if (string.IsNullOrEmpty(LayerMask.LayerToName(l))) return l;
            throw new InvalidOperationException("TraversalScan: no spare layer for the visual probe");
        }

        // ---------------------------------------------------------------------------------------------------------------- NavMesh
        static WalkGrid BakeGrid(ScanSpace space, HeroCapsule hero, ScanOptions opt, int physMask, int visLayer, out NavMeshDataInstance inst)
        {
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(space.Area, physMask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled));
            NavMeshBuildSettings st = NavMesh.GetSettingsByID(0);
            st.agentRadius = hero.Radius; st.agentHeight = hero.Height; st.agentClimb = hero.StepOffset; st.agentSlope = hero.SlopeLimit;
            st.overrideVoxelSize = true; st.voxelSize = opt.VoxelSize; st.overrideTileSize = true; st.tileSize = 256; st.minRegionArea = 0.25f;
            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(st, sources, space.Area, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("TraversalScan: NavMesh bake failed for " + space.Name);
            inst = NavMesh.AddNavMeshData(data);
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();

            var grid = new WalkGrid(space.Area, opt.Cell);
            int nt = tri.indices.Length / 3;
            // components: union-find over triangles that share a welded vertex
            var parent = new int[nt];
            for (int i = 0; i < nt; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            var weld = new Dictionary<(int, int, int), int>();
            for (int t = 0; t < nt; t++)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 v = tri.vertices[tri.indices[t * 3 + k]];
                    var key = (Mathf.RoundToInt(v.x * 20f), Mathf.RoundToInt(v.y * 20f), Mathf.RoundToInt(v.z * 20f));
                    if (weld.TryGetValue(key, out int other)) { int a = Find(t), b = Find(other); if (a != b) parent[a] = b; }
                    else weld[key] = t;
                }
            var compId = new Dictionary<int, int>();
            var triComp = new int[nt];
            var area = new List<float>();
            for (int t = 0; t < nt; t++)
            {
                int r = Find(t);
                if (!compId.TryGetValue(r, out int c)) { c = compId.Count; compId[r] = c; area.Add(0f); }
                triComp[t] = c;
                Vector3 a = tri.vertices[tri.indices[t * 3]], b = tri.vertices[tri.indices[t * 3 + 1]], d = tri.vertices[tri.indices[t * 3 + 2]];
                area[c] += Vector3.Cross(b - a, d - a).magnitude * 0.5f;
                Rasterise(grid, a, b, d, c);
            }
            grid.Comps = compId.Count; grid.CompArea = area.ToArray();

            // main component: the spawn's, else the largest
            grid.Main = -1;
            if (space.Spawn.HasValue && NavMesh.SamplePosition(space.Spawn.Value, out NavMeshHit hit, 3f, NavMesh.AllAreas) && grid.ToCell(hit.position, out int sx, out int sz))
                grid.Main = grid.LayerNear(grid.Index(sx, sz), hit.position.y, 0.6f, out _);
            if (grid.Main < 0 && area.Count > 0) { float best = -1f; for (int c = 0; c < area.Count; c++) if (area[c] > best) { best = area[c]; grid.Main = c; } }
            return grid;
        }

        static void Rasterise(WalkGrid g, Vector3 a, Vector3 b, Vector3 c, int comp)
        {
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - g.MinX) / g.Cell)), x1 = Mathf.Min(g.W - 1, Mathf.FloorToInt((maxX - g.MinX) / g.Cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - g.MinZ) / g.Cell)), z1 = Mathf.Min(g.H - 1, Mathf.FloorToInt((maxZ - g.MinZ) / g.Cell));
            float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(den) < 1e-8f) return;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = g.CellCentre(x, z);
                    float w1 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / den;
                    float w2 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / den;
                    float w3 = 1f - w1 - w2;
                    const float e = -1e-4f;
                    if (w1 < e || w2 < e || w3 < e) continue;
                    g.Add(g.Index(x, z), w1 * a.y + w2 * b.y + w3 * c.y, comp);
                }
        }

        static void Reach(WalkGrid g, TraversalTarget t)
        {
            t.Reached = false; t.NearestGap = float.PositiveInfinity;
            if (g.Main < 0) return;
            int rad = Mathf.CeilToInt(6f / g.Cell);
            if (!g.ToCell(t.Pos, out int cx, out int cz)) { cx = Mathf.Clamp(cx, 0, g.W - 1); cz = Mathf.Clamp(cz, 0, g.H - 1); }
            for (int dz = -rad; dz <= rad; dz++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (!g.In(x, z)) continue;
                    int i = g.Index(x, z);
                    if (!g.HasMain(i)) continue;
                    float ly = g.C0[i] == g.Main ? g.Y0[i] : g.Y1[i];
                    if (Mathf.Abs(ly - t.Pos.y) > 1.8f) continue;
                    Vector2 c = g.CellCentre(x, z);
                    float d = Vector2.Distance(c, new Vector2(t.Pos.x, t.Pos.z));
                    if (d < t.NearestGap) t.NearestGap = d;
                }
            t.Reached = t.NearestGap <= t.Reach;
        }

        // ---------------------------------------------------------------------------------------------------------------- visual probe
        static int AddVisuals(Transform root, int layer, List<GameObject> made)
        {
            if (root == null) return 0;
            var skip = new HashSet<Renderer>();
            foreach (LODGroup lg in root.GetComponentsInChildren<LODGroup>())
            {
                LOD[] lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (Renderer r in lods[i].renderers) if (r != null) skip.Add(r);
            }
            int n = 0;
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy || skip.Contains(mr)) continue;
                if (mr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (IsSoft(mr)) continue;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount > 400000) continue;
                if (mr.bounds.size.y < 0.12f) continue;
                var go = new GameObject(VisualName) { layer = layer, hideFlags = HideFlags.DontSave };
                go.transform.SetParent(mr.transform, false);
                MeshCollider mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                made.Add(go); n++;
            }
            return n;
        }

        public static bool IsSoft(Renderer r)
        {
            // Cellars greybox markers (entry starts, stair exit, bank, healing point, carrier, baker, warden) are placeholders, not props; chests are real
            if (r.transform.parent != null && r.transform.parent.name == "Markers" && !r.name.StartsWith("Chest_") && !r.name.StartsWith("KeyChest_")) return true;
            string path = TraversalSources.HierarchyPath(r.transform, 5).ToLowerInvariant();
            foreach (string t in SoftTokens) if (path.Contains(t)) return true;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                string sn = m.shader.name.ToLowerInvariant();
                foreach (string t in SoftShaders) if (sn.Contains(t)) return true;
            }
            return false;
        }

        public static bool IsDressing(Transform t)
        {
            string path = TraversalSources.HierarchyPath(t, 6).ToLowerInvariant();
            foreach (string tok in DressingTokens) if (path.Contains(tok)) return true;
            return false;
        }

        static Transform VisualOwner(Collider c) => c.gameObject.name == VisualName && c.transform.parent != null ? c.transform.parent : c.transform;

        // ---------------------------------------------------------------------------------------------------------------- ground
        static bool Ground(float x, float z, float expected, int mask, out RaycastHit best, float tol = 1.5f)
        {
            best = default; bool found = false; float bd = tol;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, expected + 3f, z), Vector3.down, 3f + tol + 0.5f, mask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
            {
                if (h.normal.y < 0.1f) continue;
                float d = Mathf.Abs(h.point.y - expected);
                if (d <= bd) { bd = d; best = h; found = true; }
            }
            return found;
        }

        static bool VisualGround(float x, float z, float near, int visMask, out float y)
        {
            y = 0f; bool found = false; float bd = 0.6f;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, near + 2f, z), Vector3.down, 2.6f, visMask, QueryTriggerInteraction.Collide))
            {
                float d = Mathf.Abs(h.point.y - near);
                if (d <= bd) { bd = d; y = h.point.y; found = true; }
            }
            foreach (Terrain t in Terrain.activeTerrains)
            {
                float ty = t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;
                Vector3 local = new Vector3(x, 0f, z) - t.transform.position;
                if (local.x < 0 || local.z < 0 || local.x > t.terrainData.size.x || local.z > t.terrainData.size.z) continue;
                float d = Mathf.Abs(ty - near);
                if (d <= bd) { bd = d; y = ty; found = true; }
            }
            return found;
        }

        // The narrowest free width through p at body height, from thin capsule casts along four axes.
        static float WidthAt(Vector3 p, HeroCapsule hero, int mask)
        {
            const float rr = 0.05f, max = 3f;
            Vector3 a = p + Vector3.up * (hero.StepOffset + rr), b = p + Vector3.up * (hero.Height - rr);
            float best = float.PositiveInfinity;
            for (int k = 0; k < 4; k++)
            {
                float ang = k * 45f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float d1 = Physics.CapsuleCast(a, b, rr, dir, out RaycastHit h1, max, mask, QueryTriggerInteraction.Ignore) ? h1.distance + rr : max;
                float d2 = Physics.CapsuleCast(a, b, rr, -dir, out RaycastHit h2, max, mask, QueryTriggerInteraction.Ignore) ? h2.distance + rr : max;
                best = Mathf.Min(best, d1 + d2);
            }
            return best;
        }

        // The widest capsule (radius) that fits centred at p, as a width (diameter).
        static float FitDiameter(Vector3 p, HeroCapsule hero, int mask)
        {
            float lo = 0f, hi = hero.Radius;
            for (int i = 0; i < 8; i++)
            {
                float m = (lo + hi) * 0.5f;
                if (Physics.CheckCapsule(p + Vector3.up * (hero.StepOffset + m), p + Vector3.up * (hero.Height - m), m, mask, QueryTriggerInteraction.Ignore)) hi = m; else lo = m;
            }
            return lo * 2f;
        }

        static bool BodyBlocked(Vector3 p, HeroCapsule hero, float radius, int mask) =>
            Physics.CheckCapsule(p + Vector3.up * (hero.StepOffset + radius), p + Vector3.up * (hero.Height - radius), radius, mask, QueryTriggerInteraction.Ignore);

        static Collider BodyOverlap(Vector3 p, HeroCapsule hero, float radius, int mask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore)
        {
            Collider[] cs = Physics.OverlapCapsule(p + Vector3.up * (hero.StepOffset + radius), p + Vector3.up * (hero.Height - radius), radius, mask, q);
            return cs.Length > 0 ? cs[0] : null;
        }

        // ---------------------------------------------------------------------------------------------------------------- intended paths
        static void WalkPath(ScanResult res, WalkGrid g, IntendedPath p, HeroCapsule hero, int physMask, int visMask, bool full)
        {
            ScanSpace s = res.Space;
            Vector3 d = p.B - p.A; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Vector3 dir = d / len;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / g.Cell));
            float? prevY = null; Vector3 prevP = default;
            for (int i = 0; i <= n; i++)
            {
                float t = len * i / n;
                if (t < p.TrimA || t > len - p.TrimB) { prevY = null; continue; }
                Vector3 q = p.A + dir * t;
                float ab = len > 0 ? t / len : 0f;
                float exp = s.ExpectedGround != null ? s.ExpectedGround(q.x, q.z) : Mathf.Lerp(p.A.y, p.B.y, ab);
                if (Mathf.Abs(p.A.y - p.B.y) > 0.5f) exp = Mathf.Lerp(p.A.y, p.B.y, ab);   // a stair or ramp line: the plan's end heights win
                if (g.ToCell(q, out int cx, out int cz)) g.Flags[g.Index(cx, cz)] |= WalkGrid.FPath;
                if (!Ground(q.x, q.z, exp, physMask, out RaycastHit gh))
                {
                    Issue(res, IssueKind.Hole, new Vector3(q.x, exp, q.z), 1.5f, 0f, $"no ground within 1.5 m of the planned height {exp:0.00} on {p.Name}", null, p.Name);
                    if (g.ToCell(q, out cx, out cz)) g.Flags[g.Index(cx, cz)] |= WalkGrid.FHole;
                    prevY = null; continue;
                }
                Vector3 at = gh.point;
                if (prevY.HasValue && Mathf.Abs(at.y - prevY.Value) > hero.StepOffset)
                    Issue(res, IssueKind.Step, at, Mathf.Abs(at.y - prevY.Value), hero.StepOffset, $"step of {Mathf.Abs(at.y - prevY.Value):0.00} m on {p.Name}", gh.collider, p.Name);
                float slope = Vector3.Angle(gh.normal, Vector3.up);
                if (slope > hero.SlopeLimit + 0.5f)
                    Issue(res, IssueKind.Slope, at, slope, hero.SlopeLimit, $"ground at {slope:0}° on {p.Name}", gh.collider, p.Name, "°");
                prevY = at.y; prevP = at;

                if (BodyBlocked(at, hero, hero.Radius - 0.02f, physMask))
                {
                    Collider c = BodyOverlap(at, hero, hero.Radius - 0.02f, physMask);
                    float fit = FitDiameter(at, hero, physMask);
                    Issue(res, IssueKind.Blocked, at, fit, hero.FitWidth, $"the capsule doesn't fit on {p.Name} (widest fit {fit:0.00} m)" + (p.Width > 0 ? $", plan width {p.Width:0.0} m" : ""), c, p.Name);
                    if (g.ToCell(at, out cx, out cz)) g.Flags[g.Index(cx, cz)] |= WalkGrid.FBlocked;
                }
                else if (BodyBlocked(at, hero, hero.Radius + HeroCapsule.ComfortMargin * 0.5f, physMask))
                {
                    float w = WidthAt(at, hero, physMask);
                    if (w < hero.FitWidth)
                    {
                        Collider c = BodyOverlap(at, hero, hero.Radius + HeroCapsule.ComfortMargin * 0.5f, physMask);
                        Issue(res, IssueKind.Narrow, at, w, hero.FitWidth, $"opening {w:0.00} m wide on {p.Name}" + (p.Width > 0 ? $", plan width {p.Width:0.0} m" : ""), c, p.Name);
                        if (g.ToCell(at, out cx, out cz)) g.Flags[g.Index(cx, cz)] |= WalkGrid.FNarrow;
                    }
                }

                if (full && gh.normal.y > 0.7f)
                {
                    if (!VisualGround(at.x, at.z, at.y, visMask, out float vy))
                        Issue(res, IssueKind.FloorMismatch, at, 0.6f, 0.08f, $"no visible floor within 0.6 m of the floor collider on {p.Name}", gh.collider, p.Name);
                    else if (Mathf.Abs(vy - at.y) > 0.08f)
                        Issue(res, IssueKind.FloorMismatch, at, Mathf.Abs(vy - at.y), 0.08f, $"visible floor {(vy > at.y ? "above" : "below")} its collider by {Mathf.Abs(vy - at.y):0.00} m on {p.Name}", gh.collider, p.Name);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------------- grid pass
        static void GridPass(ScanResult res, WalkGrid g, HeroCapsule hero, int physMask, int visMask)
        {
            ScanSpace s = res.Space;
            float rc = hero.Radius + HeroCapsule.ComfortMargin * 0.5f;
            for (int z = 0; z < g.H; z++)
                for (int x = 0; x < g.W; x++)
                {
                    int i = g.Index(x, z);
                    Vector2 c2 = g.CellCentre(x, z);
                    bool planned = s.ExpectedWalkable == null || s.ExpectedWalkable(c2.x, c2.y);
                    if (g.HasAny(i))
                    {
                        g.Flags[i] |= WalkGrid.FGround;
                        if (!g.HasMain(i)) continue;
                        float y = g.C0[i] == g.Main ? g.Y0[i] : g.Y1[i];
                        var p = new Vector3(c2.x, y, c2.y);
                        Collider vis = BodyOverlap(p, hero, 0.2f, visMask, QueryTriggerInteraction.Collide);
                        if (vis != null)
                        {
                            Transform owner = VisualOwner(vis);
                            g.Flags[i] |= WalkGrid.FWalkThrough;
                            Issue(res, IssueKind.WalkThrough, p, 0f, 0f, "visible mesh with no collider where the hero walks", owner, null);
                        }
                        if (planned && BodyBlocked(p, hero, rc, physMask))
                        {
                            float w = WidthAt(p, hero, physMask);
                            if (w < hero.FitWidth)
                            {
                                g.Flags[i] |= WalkGrid.FNarrow;
                                Issue(res, IssueKind.Narrow, p, w, hero.FitWidth, $"gap {w:0.00} m wide (off the planned lines)", BodyOverlap(p, hero, rc, physMask), "off-path");
                            }
                        }
                        continue;
                    }
                    if (!planned) continue;
                    float exp = s.ExpectedGround != null ? s.ExpectedGround(c2.x, c2.y) : 0f;
                    if (!Ground(c2.x, c2.y, exp, physMask, out RaycastHit gh, 1.0f))
                    {
                        g.Flags[i] |= WalkGrid.FHole;
                        Issue(res, IssueKind.Hole, new Vector3(c2.x, exp, c2.y), 1.0f, 0f, "planned floor with no ground collider within 1 m", null, "floor");
                        continue;
                    }
                    g.Flags[i] |= WalkGrid.FGround;
                    Vector3 gp = gh.point;
                    Collider blocker = BodyOverlap(gp, hero, 0.15f, physMask);
                    if (blocker != null)
                    {
                        if (blocker is TerrainCollider) continue;
                        if (!BodyBlocked(gp, hero, 0.3f, visMask))
                        {
                            g.Flags[i] |= WalkGrid.FInvisible;
                            Issue(res, IssueKind.InvisibleBlocker, gp, 0f, 0f, "collider with nothing visible within 0.3 m", blocker, null);
                        }
                        continue;
                    }
                    // open space the NavMesh doesn't cover: a gap the eye reads as passable but the capsule can't fit (walkable ground on two opposite sides)
                    int axis = OppositeMain(g, x, z, gp.y, hero.StepOffset, 4, gp, hero, physMask, out float tw);
                    if (axis >= 0)
                    {
                        if (tw < hero.Diameter && tw > 0.3f)
                        {
                            g.Flags[i] |= WalkGrid.FTight;
                            Issue(res, IssueKind.TightGap, gp, tw, hero.Diameter, $"gap {tw:0.00} m wide between walkable areas: looks passable, the capsule needs {hero.Diameter:0.00} m", BodyOverlap(gp, hero, 0.45f, physMask), "gap");
                        }
                    }
                }
        }

        // An axis with walkable main-area floor on both sides of (x, z) whose perpendicular free width is the narrowest: a gap between two walkable areas
        // (an inside corner has floor on both diagonal sides too, but open floor along the perpendicular, so it doesn't count). -1 when there is none.
        static int OppositeMain(WalkGrid g, int x, int z, float y, float tol, int reach, Vector3 p, HeroCapsule hero, int mask, out float width)
        {
            int[,] axes = { { 1, 0 }, { 0, 1 }, { 1, 1 }, { 1, -1 } };
            int[] perp = { 1, 0, 3, 2 };
            width = float.PositiveInfinity; int best = -1;
            for (int a = 0; a < 4; a++)
            {
                bool pp = false, m = false;
                for (int k = 1; k <= reach && !(pp && m); k++)
                {
                    int px = x + axes[a, 0] * k, pz = z + axes[a, 1] * k, mx = x - axes[a, 0] * k, mz = z - axes[a, 1] * k;
                    if (!pp && g.In(px, pz) && g.LayerNear(g.Index(px, pz), y, tol + 0.5f, out _) == g.Main) pp = true;
                    if (!m && g.In(mx, mz) && g.LayerNear(g.Index(mx, mz), y, tol + 0.5f, out _) == g.Main) m = true;
                }
                if (!(pp && m)) continue;
                int q = perp[a];
                var dir = new Vector3(axes[q, 0], 0f, axes[q, 1]).normalized;
                float w = WidthAlong(p, dir, hero, mask);
                if (w < width) { width = w; best = a; }
            }
            return best;
        }

        static float WidthAlong(Vector3 p, Vector3 dir, HeroCapsule hero, int mask)
        {
            const float rr = 0.05f, max = 3f;
            Vector3 a = p + Vector3.up * (hero.StepOffset + rr), b = p + Vector3.up * (hero.Height - rr);
            float d1 = Physics.CapsuleCast(a, b, rr, dir, out RaycastHit h1, max, mask, QueryTriggerInteraction.Ignore) ? h1.distance + rr : max;
            float d2 = Physics.CapsuleCast(a, b, rr, -dir, out RaycastHit h2, max, mask, QueryTriggerInteraction.Ignore) ? h2.distance + rr : max;
            return d1 + d2;
        }

        // ---------------------------------------------------------------------------------------------------------------- islands and stuck pockets
        static void Islands(ScanResult res, WalkGrid g, WalkGrid open, HeroCapsule hero, int physMask)
        {
            if (g.Main < 0) return;
            var cells = new Dictionary<int, List<(int x, int z, float y)>>();
            for (int z = 0; z < g.H; z++)
                for (int x = 0; x < g.W; x++)
                {
                    int i = g.Index(x, z);
                    for (int l = 0; l < 2; l++)
                    {
                        int c = l == 0 ? g.C0[i] : g.C1[i];
                        if (c < 0 || c == g.Main || g.CompArea[c] < 0.5f) continue;
                        if (!cells.TryGetValue(c, out var list)) cells[c] = list = new List<(int, int, float)>();
                        list.Add((x, z, l == 0 ? g.Y0[i] : g.Y1[i]));
                    }
                }
            ScanSpace s = res.Space;
            float escape = hero.StepOffset + hero.JumpHeight;
            int rad = Mathf.CeilToInt(1.25f / g.Cell);
            foreach (var kv in cells)
            {
                List<(int x, int z, float y)> list = kv.Value;
                float area = g.CompArea[kv.Key];
                float medY = list.OrderBy(c => c.y).ElementAt(list.Count / 2).y;
                (int x, int z, float y) mid = list[list.Count / 2];
                Vector2 mc = g.CellCentre(mid.x, mid.z);
                float expG = s.ExpectedGround != null ? s.ExpectedGround(mc.x, mc.y) : 0f;
                bool stuck = false;
                int tries = 0;
                foreach (var cell in list)
                {
                    if (stuck || tries > 400) break;
                    for (int dz = -rad; dz <= rad && !stuck; dz++)
                        for (int dx = -rad; dx <= rad && !stuck; dx++)
                        {
                            int x = cell.x + dx, z = cell.z + dz;
                            if (!g.In(x, z)) continue;
                            int i = g.Index(x, z);
                            float my = g.C0[i] == g.Main ? g.Y0[i] : (g.C1[i] == g.Main ? g.Y1[i] : float.NaN);
                            if (float.IsNaN(my) || my - cell.y <= hero.StepOffset) continue;
                            tries++;
                            Vector2 a2 = g.CellCentre(x, z), b2 = g.CellCentre(cell.x, cell.z);
                            Vector3 from = new Vector3(a2.x, my, a2.y);
                            Vector3 to = new Vector3(b2.x, my, b2.y) - from;
                            float dist = to.magnitude;
                            if (dist < 0.01f) continue;
                            bool clear = !Physics.CapsuleCast(from + Vector3.up * (hero.StepOffset + hero.Radius), from + Vector3.up * (hero.Height - hero.Radius), hero.Radius - 0.02f, to / dist, dist, physMask, QueryTriggerInteraction.Ignore);
                            if (!clear) continue;
                            float rise = my - cell.y;
                            if (rise > escape)
                            {
                                stuck = true;
                                var p = new Vector3(b2.x, cell.y, b2.y);
                                Issue(res, IssueKind.Stuck, p, rise, escape, $"pocket of {area:0.0} m² the hero can drop into from {rise:0.00} m above but not climb or jump out of", ColliderBelow(p, physMask), "pocket");
                            }
                        }
                }
                if (stuck) { foreach (var c in list) g.Flags[g.Index(c.x, c.z)] |= WalkGrid.FStuck; continue; }
                foreach (var c in list) g.Flags[g.Index(c.x, c.z)] |= WalkGrid.FIsland;
                if (open != null && open.Main >= 0 && open.W == g.W && list.Count(c => open.HasMain(g.Index(c.x, c.z))) * 2 > list.Count) continue;   // behind a locked door: reachable once it opens
                if (Mathf.Abs(medY - expG) < 0.35f && area >= 1.5f)
                {
                    res.Islands++;
                    var p = new Vector3(mc.x, mid.y, mc.y);
                    Issue(res, IssueKind.Island, p, area, 1.5f, $"{area:0.0} m² of ground-level floor cut off from the main area (a gap too narrow, or a sealed pocket)", ColliderBelow(p, physMask), "island", "m²");
                }
            }
        }

        static Collider ColliderBelow(Vector3 p, int mask) =>
            Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit h, 1.5f, mask, QueryTriggerInteraction.Ignore) ? h.collider : null;

        // ---------------------------------------------------------------------------------------------------------------- stairs
        static void Stairs(ScanResult res, WalkGrid g, HeroCapsule hero)
        {
            Transform root = res.Space.Root;
            if (root == null) return;
            var stairs = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (!IsStairName(t.name) || !t.gameObject.activeInHierarchy) continue;
                bool nested = false;
                for (Transform p = t.parent; p != null; p = p.parent) if (IsStairName(p.name)) { nested = true; break; }
                if (!nested && t.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !(r is ParticleSystemRenderer))) stairs.Add(t);
            }
            res.Stairs = stairs.Count;
            foreach (Transform st in stairs)
            {
                Bounds b = default; bool any = false;
                foreach (Renderer r in st.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any || b.size.y < 0.5f) continue;
                // footprint cells and their walk layers on the stair
                var foot = new List<(int x, int z, float y, int c)>();
                int x0 = Mathf.FloorToInt((b.min.x - g.MinX) / g.Cell), x1 = Mathf.FloorToInt((b.max.x - g.MinX) / g.Cell);
                int z0 = Mathf.FloorToInt((b.min.z - g.MinZ) / g.Cell), z1 = Mathf.FloorToInt((b.max.z - g.MinZ) / g.Cell);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!g.In(x, z)) continue;
                        int i = g.Index(x, z);
                        g.Flags[i] |= WalkGrid.FStair;
                        if (g.C0[i] >= 0 && g.Y0[i] >= b.min.y - 0.15f && g.Y0[i] <= b.max.y + 0.3f) foot.Add((x, z, g.Y0[i], g.C0[i]));
                        if (g.C1[i] >= 0 && g.Y1[i] >= b.min.y - 0.15f && g.Y1[i] <= b.max.y + 0.3f) foot.Add((x, z, g.Y1[i], g.C1[i]));
                    }
                string name = TraversalSources.HierarchyPath(st, 4);
                bool hasCol = st.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger && c.gameObject.name != VisualName);
                var centre = new Vector3(b.center.x, b.min.y, b.center.z);
                if (!foot.Any(f => f.y > b.min.y + 0.3f))
                {
                    Issue(res, IssueKind.StairToNowhere, centre, b.size.y, hero.StepOffset,
                        hasCol ? $"stair {b.size.y:0.0} m high is not walkable: its colliders give no NavMesh on it (risers above the {hero.StepOffset:0.00} m step offset or steeper than {hero.SlopeLimit:0}°)"
                               : $"stair {b.size.y:0.0} m high has no collider: the hero walks through it", st, "stair");
                    continue;
                }
                float top = foot.Max(f => f.y), low = foot.Min(f => f.y);
                bool onMain = foot.Any(f => f.c == g.Main);
                if (!onMain)
                {
                    Issue(res, IssueKind.StairToNowhere, centre, 0f, 0f, "stair can't be reached from the spawn", st, "stair");
                    continue;
                }
                float topOut = Beyond(g, b, foot.Where(f => f.y >= top - 0.35f), hero.StepOffset);
                float lowOut = Beyond(g, b, foot.Where(f => f.y <= low + 0.35f), hero.StepOffset);
                if (topOut < 1.5f)
                    Issue(res, IssueKind.StairToNowhere, new Vector3(b.center.x, top, b.center.z), topOut, 1.5f, $"top landing leads nowhere: {topOut:0.0} m² of walkable floor beyond the stair's top", st, "stair", "m²");
                if (lowOut < 1.5f && top - low > hero.StepOffset)
                    Issue(res, IssueKind.StairToNowhere, new Vector3(b.center.x, low, b.center.z), lowOut, 1.5f, $"bottom landing leads nowhere: {lowOut:0.0} m² of walkable floor beyond the stair's foot", st, "stair", "m²");
            }
        }

        static bool IsStairName(string n)
        {
            string l = n.ToLowerInvariant();
            if (l.Contains("doorstep") || l.Contains("light") || l.Contains("lamp") || l.Contains("marker") || l == "stairexit" || l.Contains("undercroftstair")) return false;
            return l.Contains("stair") || l.Contains("steps");
        }

        // Walkable area (m²) reachable from the start cells without re-entering the stair footprint, within 8 m.
        static float Beyond(WalkGrid g, Bounds b, IEnumerable<(int x, int z, float y, int c)> start, float step)
        {
            var seen = new HashSet<(int, int)>();
            var q = new Queue<(int x, int z, float y)>();
            foreach (var s in start) if (seen.Add((s.x, s.z))) q.Enqueue((s.x, s.z, s.y));
            int outside = 0; int limit = Mathf.CeilToInt(8f / g.Cell);
            Vector2 c0 = new Vector2(b.center.x, b.center.z);
            float pad = g.Cell;
            while (q.Count > 0 && outside < 400)
            {
                var (x, z, y) = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), nz = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!g.In(nx, nz) || seen.Contains((nx, nz))) continue;
                    int ly = g.LayerNear(g.Index(nx, nz), y, step, out float ny);
                    if (ly < 0) continue;
                    Vector2 cc = g.CellCentre(nx, nz);
                    if ((cc - c0).magnitude > limit * g.Cell) continue;
                    seen.Add((nx, nz));
                    bool inFoot = cc.x >= b.min.x - pad && cc.x <= b.max.x + pad && cc.y >= b.min.z - pad && cc.y <= b.max.z + pad;
                    if (!inFoot) outside++;
                    else if (Mathf.Abs(ny - y) > step) continue;
                    q.Enqueue((nx, nz, ny));
                }
            }
            return outside * g.Cell * g.Cell;
        }

        // ---------------------------------------------------------------------------------------------------------------- keep-clear zones and oversized colliders
        static void KeepClearZones(ScanResult res, HeroCapsule hero, int physMask, int visMask)
        {
            foreach (KeepClear k in res.Space.Clear)
            {
                Vector3 c = k.Pos + Vector3.up * (hero.StepOffset + k.Radius);
                Vector3 top = k.Pos + Vector3.up * Mathf.Max(hero.StepOffset + k.Radius, hero.Height - k.Radius);
                var seen = new HashSet<Transform>();
                foreach (Collider col in Physics.OverlapCapsule(c, top, k.Radius, physMask, QueryTriggerInteraction.Ignore))
                {
                    if (!IsDressing(col.transform) || !seen.Add(col.transform)) continue;
                    Issue(res, IssueKind.ClutterInDoorway, k.Pos, k.Radius, 0f, $"dressing with a collider in {k.Name}", col.transform, k.Name);
                }
                foreach (Collider col in Physics.OverlapCapsule(c, top, k.Radius, visMask, QueryTriggerInteraction.Collide))
                {
                    Transform o = VisualOwner(col);
                    if (!IsDressing(o) || !seen.Add(o)) continue;
                    bool ownCollider = o.GetComponentInParent<Collider>() != null && o.GetComponentsInParent<Collider>().Any(x => x.gameObject.name != VisualName);
                    if (ownCollider) continue;
                    Issue(res, IssueKind.ClutterInDoorway, k.Pos, k.Radius, 0f, $"dressing (visual only, no collider) in {k.Name}", o, k.Name);
                }
            }
        }

        static void Oversized(ScanResult res, int visLayer)
        {
            Transform root = res.Space.Root;
            if (root == null) return;
            foreach (Collider col in root.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled || col.isTrigger || col is TerrainCollider || col.gameObject.layer == visLayer || !col.gameObject.activeInHierarchy) continue;
                res.Colliders++;
                Renderer r = col.GetComponent<Renderer>();
                if (r == null || !r.enabled) continue;
                Bounds cb = col.bounds, rb = r.bounds;
                float excess = Mathf.Max(Mathf.Max(cb.max.x - rb.max.x, rb.min.x - cb.min.x), Mathf.Max(cb.max.z - rb.max.z, rb.min.z - cb.min.z));
                float rv = Mathf.Max(0.001f, rb.size.x * rb.size.y * rb.size.z), cv = cb.size.x * cb.size.y * cb.size.z;
                if (excess <= 0.3f || cv / rv < 1.5f) continue;
                ScanSpace s = res.Space;
                if (s.ExpectedWalkable != null && !s.ExpectedWalkable(cb.center.x, cb.center.z)
                    && !s.ExpectedWalkable(cb.min.x, cb.min.z) && !s.ExpectedWalkable(cb.max.x, cb.max.z)) continue;
                Issue(res, IssueKind.OversizedCollider, cb.center, excess, 0.3f, $"collider reaches {excess:0.00} m past its mesh ({cv / rv:0.0}x the mesh volume)", col.transform, null);
            }
        }

        // ---------------------------------------------------------------------------------------------------------------- issues
        static void Issue(ScanResult res, IssueKind kind, Vector3 pos, float measured, float limit, string detail, Component src, string group, string unit = "m")
        {
            ScanSpace s = res.Space;
            var i = new TraversalIssue
            {
                Scene = s.Scene, Variant = s.Variant, Seed = s.Seed, Floor = s.Floor, Kind = kind, Pos = pos, Measured = measured, Limit = limit, Unit = unit,
                Detail = detail, Group = group ?? "",
            };
            if (src != null)
            {
                i.Object = TraversalSources.HierarchyPath(src.transform, 5);
                i.Source = TraversalSources.Find(src.transform);
            }
            if (s.Describe != null) i.Plan = s.Describe(pos) ?? "";
            res.Issues.Add(i);
        }

        public static bool LowerIsWorse(IssueKind k) => k == IssueKind.Narrow || k == IssueKind.Blocked || k == IssueKind.TightGap;

        // Merges samples of one kind and one cause lying within 1 m of each other into one issue at the worst sample.
        static List<TraversalIssue> Cluster(List<TraversalIssue> raw)
        {
            var parent = new int[raw.Count];
            for (int i = 0; i < raw.Count; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            var hash = new Dictionary<(IssueKind, string, int, int), List<int>>();
            for (int i = 0; i < raw.Count; i++)
            {
                TraversalIssue r = raw[i];
                if (r.Kind == IssueKind.Unreachable || r.Kind == IssueKind.StairToNowhere || r.Kind == IssueKind.Island || r.Kind == IssueKind.Stuck) continue;
                string key = r.Group + "|" + (r.Kind == IssueKind.Step || r.Kind == IssueKind.Hole || r.Kind == IssueKind.Narrow || r.Kind == IssueKind.Blocked ? "" : r.Object);
                int ix = Mathf.FloorToInt(r.Pos.x), iz = Mathf.FloorToInt(r.Pos.z);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (hash.TryGetValue((r.Kind, key, ix + dx, iz + dz), out List<int> near))
                            foreach (int j in near)
                                if (Vector3.Distance(raw[j].Pos, r.Pos) <= 1.0f) { int a = Find(i), b = Find(j); if (a != b) parent[a] = b; }
                var k = (r.Kind, key, ix, iz);
                if (!hash.TryGetValue(k, out List<int> l)) hash[k] = l = new List<int>();
                l.Add(i);
            }
            var groups = new Dictionary<int, List<TraversalIssue>>();
            for (int i = 0; i < raw.Count; i++) { int r = Find(i); if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<TraversalIssue>(); g.Add(raw[i]); }
            var outList = new List<TraversalIssue>();
            foreach (List<TraversalIssue> g in groups.Values)
            {
                TraversalIssue worst = g[0];
                bool lower = LowerIsWorse(worst.Kind);
                foreach (TraversalIssue x in g) if (lower ? x.Measured < worst.Measured : x.Measured > worst.Measured) worst = x;
                worst.Samples = g.Count;
                if (string.IsNullOrEmpty(worst.Source)) { TraversalIssue withSrc = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.Source)); if (withSrc != null) { worst.Source = withSrc.Source; worst.Object = withSrc.Object; } }
                outList.Add(worst);
            }
            return outList;
        }
    }
}
