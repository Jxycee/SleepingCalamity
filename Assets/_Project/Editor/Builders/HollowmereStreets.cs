// Hollowmere's streets inside the wall (area 1 world, T06, 2026-11-17): the square, Ring Lane, Market Street, Lower Lane, East Lane and the inn front stop reading as clean cobbles
// between ruins. Preset_Ruined only (the Rebuilt town is whole), everything under "Streets" of the Ruined root, authority none (editor-time scene building).
//   1. Decals: URP Decal Projectors (the DecalRendererFeature is added to the beauty-corner renderer the captures use and to the game's PC renderer, screen-space technique so the
//      WearLit shader needs no DBuffer support). Moss, grime and damp at wall feet, leaf drift in every third Market Street gutter bay, mud and puddles on the flat stretches,
//      soot under the chimney stacks. Textures: Quixel decals C03b (moss clump), T15a/T15b (leaves), the B3 Moss_BaseColor wash, plus soft generated blobs (mud, puddle, soot, damp, grime)
//      under Content/Art/Town/Decals/Street_*.png. Budget 300 projectors for the whole area.
//   2. Wall-foot strips: grass and weed tufts along every shell wall and lane-kit wall foot beside the lanes and the square, rubble spill where T05 left a wall face without any.
//   3. Clutter clusters (HollowmereStreetClutter), lane landmarks and story props (same file).
// Placement is position-hashed (no shared RNG, no dictionary-order iteration), so one rejected spot moves nothing else and two builds write the same scene. Colliders only where the
// path graph and the lane keep their clearance (the traversal regression counts blocked and narrow spots with the hero's capsule).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.EditorTools
{
    internal enum StreetZone { None, Square, Ring, Market, Inn, EastLane }

    // A wall face: A is the start, Dir runs along it, N points away from the wall (the street side), Len is its length.
    internal struct WallSeg { public Vector2 A, Dir, N; public float Len; public int Key; public bool Lot; public string Id; }

    public static class HollowmereStreets
    {
        internal const string Adopted = "Assets/_Project/Content/Art/Town/Adopted/", CellarAdopted = "Assets/_Project/Content/Art/Cellar/Adopted/";
        const string ArtDecals = "Assets/_Project/Content/Art/Town/Decals", MatFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
        const int DecalBudget = 150;   // T12: was 300 (the area budget is about 300 over all builders: streets 150, orchard 50, bell yard 48, roadside 40, allotments 19)

        internal static TownLayout T;
        internal static readonly List<WallSeg> Walls = new List<WallSeg>();
        internal static readonly List<Vector2> BrokenWallPoints = new List<Vector2>();
        static readonly List<(Vector2 A, Vector2 B)> pathEdges = new List<(Vector2, Vector2)>();
        static readonly List<(Vector2 P, PathNodeKind K)> nodes = new List<(Vector2, PathNodeKind)>();
        static readonly List<Vector2> cams = new List<Vector2>();
        static readonly Collider[] hits = new Collider[24];
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        internal static void Count(string k, int n = 1) { counts[k] = counts.TryGetValue(k, out int c) ? c + n : n; }

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            T = t; counts.Clear(); Walls.Clear(); BrokenWallPoints.Clear(); pathEdges.Clear(); nodes.Clear(); cams.Clear();
            var nm = t.Nodes.ToDictionary(n => n.Id, n => new Vector2(n.Pos.X, n.Pos.Z));
            foreach (PathEdge e in t.Edges) if (nm.TryGetValue(e.A, out Vector2 a) && nm.TryGetValue(e.B, out Vector2 b)) pathEdges.Add((a, b));
            foreach (PathNode n in t.Nodes) nodes.Add((new Vector2(n.Pos.X, n.Pos.Z), n.Kind));
            foreach (var c in HollowmereBuilder.Cameras)
                if (c.Name.StartsWith("Cap_HM_Eye_", System.StringComparison.Ordinal) && !c.Name.Contains("JettyBack")) cams.Add(new Vector2(c.Pos.x, c.Pos.z));
            Physics.SyncTransforms();

            BuilderUtil.EnsureFolder(ArtDecals); BuilderUtil.EnsureFolder(MatFolder);
            Transform root = new GameObject("Streets").transform; root.SetParent(ruined, false);
            CollectWalls(ruined, t);
            Decals(Group(root, "Decals"), ruined, t);
            Strips(Group(root, "Strips"), ruined);
            HollowmereStreetClutter.Build(Group(root, "Clutter"), Group(root, "Landmarks"), Group(root, "Story"), t);
            Debug.Log("SC HOLLOWMERE streets: " + string.Join(", ", counts.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)) + ", wall faces=" + Walls.Count);
        }

        internal static Transform Group(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ------------------------------------------------------------------------------------------------ hashing and queries
        internal static float H(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        internal static int StableHash(string s) { unchecked { uint h = 2166136261u; foreach (char c in s) { h ^= c; h *= 16777619u; } return (int)(h & 0x7FFFFFFF); } }

        internal static bool Surface(float x, float z, out float y, out float ny)
        {
            float g = HollowmereLayout.Ground(T, x, z);
            y = g; ny = 1f;
            if (Physics.Raycast(new Vector3(x, g + 0.7f, z), Vector3.down, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y; ny = hit.normal.y;
                if (Mathf.Abs(y - g) > 0.6f) return false;
            }
            return true;
        }

        internal static float PathDistance(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (var e in pathEdges)
            {
                Vector2 ab = e.B - e.A; float t = Mathf.Clamp01(Vector2.Dot(p - e.A, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                d = Mathf.Min(d, Vector2.Distance(p, e.A + ab * t));
            }
            return d;
        }

        internal static StreetZone ZoneOf(Vector2 p, out float laneEdge, out Lane lane)
        {
            laneEdge = float.MaxValue; lane = null;
            var tp = new TownPoint(p.x, p.y);
            foreach (Lane l in T.Lanes)
            {
                float d = HollowmereLayout.DistanceToPolyline(tp, l.Points) - l.Width * 0.5f;
                if (d < laneEdge) { laneEdge = d; lane = l; }
            }
            if (p.x > -20.6f && p.x < 20.6f && p.y > -21.6f && p.y < 7.6f) return StreetZone.Square;
            if (lane == null || laneEdge > 3.4f) return StreetZone.None;
            switch (lane.Id)
            {
                case "RingLane": case "WestStub": return StreetZone.Ring;
                case "MarketStreet": case "LowerLane": return StreetZone.Market;
                case "EastStub": return p.x < 34f ? StreetZone.Inn : StreetZone.EastLane;
                case "NorthEastStub": return StreetZone.EastLane;
                default: return StreetZone.None;
            }
        }

        // The shared placement rule: a flat, free spot of a street zone, off every lot, landmark, doorstep, eye camera and (for a solid prop) the lane and the path graph.
        internal static bool Valid(Vector2 p, bool solid, float halfExt, out float y, bool cameras = true)
        {
            y = 0f;
            if (!Surface(p.x, p.y, out y, out float ny) || ny < 0.93f) return false;
            if (Mathf.Abs(y - HollowmereLayout.Ground(T, p.x, p.y)) > 0.3f) return false;   // a stair, a kerb
            var tp = new TownPoint(p.x, p.y);
            foreach (Foundation f in T.Foundations) if (f.Footprint.Contains(tp, 0.12f)) return false;
            foreach (Landmark l in T.Landmarks) if (l.Footprint.Contains(tp, 0.4f)) return false;
            StreetZone z = ZoneOf(p, out float le, out Lane lane);
            if (z == StreetZone.None) return false;
            if (solid && le < (lane != null && lane.Width < 4f ? 0.8f : 0.45f) + halfExt * 0.4f && (z != StreetZone.Square || le < 0.3f)) return false;
            if (solid && PathDistance(p) < 1.3f + halfExt * 0.6f) return false;   // small props have no collider: they may lie beside a lot-ring edge
            foreach (var n in nodes)
            {
                float d = Vector2.Distance(n.P, p);
                if ((n.K == PathNodeKind.Doorstep || n.K == PathNodeKind.Door || n.K == PathNodeKind.Stair) && d < 1.8f) return false;
                if (n.K == PathNodeKind.Corner && d < 0.7f) return false;
            }
            if (cameras) foreach (Vector2 c in cams) if (Vector2.Distance(c, p) < 1.8f) return false;
            return FreeOf(p, y, solid ? halfExt * 0.8f : 0.18f);
        }

        internal static bool FreeOf(Vector2 p, float y, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(new Vector3(p.x, y + radius + 0.15f, p.y), radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider)) return false;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ wall faces
        static void CollectWalls(Transform ruined, TownLayout t)
        {
            foreach (Foundation f in t.Foundations)
            {
                if (f.Ring > 2) continue;
                TownPoint[] c = f.Footprint.Corners();
                var ctr = new Vector2(f.Centre.X, f.Centre.Z);
                for (int i = 0; i < 4; i++)
                {
                    var a = new Vector2(c[i].X, c[i].Z); var b = new Vector2(c[(i + 1) % 4].X, c[(i + 1) % 4].Z);
                    Vector2 mid = (a + b) * 0.5f;
                    Walls.Add(new WallSeg { A = a, Dir = (b - a).normalized, N = (mid - ctr).normalized, Len = (b - a).magnitude, Key = StableHash(f.Id) * 8 + i, Lot = true, Id = f.Id });
                }
            }
            Transform lanes = ruined.Find("Lanes");
            if (lanes == null) return;
            var rows = LaneKitPlan.Plan(t).Where(r => r.In(false)).ToDictionary(r => r.Id, r => r);
            foreach (BoxCollider bc in lanes.GetComponentsInChildren<BoxCollider>(false))
            {
                if (!rows.TryGetValue(bc.gameObject.name, out LaneKitRow row) || !row.IsWall) continue;
                Vector3 right = bc.transform.right, ctr3 = bc.transform.TransformPoint(bc.center), sc = bc.transform.lossyScale;
                Vector2 dir = new Vector2(right.x, right.z).normalized, c2 = new Vector2(ctr3.x, ctr3.z), nrm = new Vector2(-dir.y, dir.x);
                float half = bc.size.x * 0.5f * Mathf.Abs(sc.x), thick = bc.size.z * 0.5f * Mathf.Abs(sc.z);
                if (half < 0.5f) continue;
                int key = StableHash(row.Id);
                if (row.Piece == LanePiece.GardenWallBroken || row.Piece == LanePiece.RetainWallBreach) BrokenWallPoints.Add(c2);
                for (int s = 0; s < 2; s++)
                {
                    float sg = s == 0 ? 1f : -1f;
                    Walls.Add(new WallSeg { A = c2 - dir * half + nrm * sg * thick, Dir = dir, N = nrm * sg, Len = half * 2f, Key = key * 2 + s, Lot = false, Id = row.Id });
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ renderer feature
        static void EnsureDecalFeature(string rendererPath)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (data == null) { Debug.LogWarning("SC HOLLOWMERE streets: renderer " + rendererPath + " not found, decals will not draw there"); return; }
            DecalRendererFeature feature = data.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "DecalRendererFeature";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                using var so = new SerializedObject(data);
                SerializedProperty features = so.FindProperty("m_RendererFeatures"), map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var so = new SerializedObject(feature))
            {
                SerializedProperty tech = so.FindProperty("m_Settings.technique");
                if (tech != null && tech.intValue != 2) { tech.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); }   // ScreenSpace: works on every shader, WearLit included
            }
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        }

        // ------------------------------------------------------------------------------------------------ decals
        static float VN(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = H(xi, yi, s), b = H(xi + 1, yi, s), c = H(xi, yi + 1, s), d = H(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        static float Fbm(float x, float y, int s) => 0.55f * VN(x, y, s) + 0.3f * VN(x * 2.3f, y * 2.3f, s + 1) + 0.15f * VN(x * 5.1f, y * 5.1f, s + 2);

        static Texture2D GenTex(string file, int n, System.Func<float, float, Color> f)
        {
            string path = ArtDecals + "/" + file;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = f((x + 0.5f) / n, (y + 0.5f) / n);
            tex.SetPixels32(px);
            byte[] png = tex.EncodeToPNG(); Object.DestroyImmediate(tex);
            string abs = Path.GetFullPath(path);
            if (!File.Exists(abs) || !File.ReadAllBytes(abs).SequenceEqual(png)) { File.WriteAllBytes(abs, png); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize != 256 || !imp.alphaIsTransparency || !imp.sRGBTexture || imp.textureCompression != TextureImporterCompression.Uncompressed))
            {
                imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 256; imp.alphaIsTransparency = true; imp.sRGBTexture = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // A soft organic blob: wobbling radius, alpha falling from `inner` of the radius to the rim.
        static float Blob(float u, float v, int seed, float soft)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f, r = Mathf.Sqrt(x * x + y * y), th = Mathf.Atan2(y, x);
            float R = 0.72f + 0.08f * Mathf.Sin(2f * th + seed) + 0.05f * Mathf.Sin(3f * th + 2f * seed) + 0.16f * (Fbm(x * 2.2f + seed, y * 2.2f - seed, seed) - 0.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(R, R - soft, r));
        }

        static Material DecalMat(string name, Texture2D tex)
        {
            Shader sh = Shader.Find("Shader Graphs/Decal");
            if (sh == null) throw new System.InvalidOperationException("Shader Graphs/Decal not found (URP package)");
            Material m = LookTestBuilder.Ensure(MatFolder + "/M_ST_Decal_" + name + ".mat", sh);
            m.SetTexture("Base_Map", tex); m.SetFloat("Normal_Blend", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        struct DecalSpec { public Material M; public Vector2 P; public float Y, W, Hh, Yaw, Fade; public int Group; public float Order; public string Name; }

        static void Decals(Transform parent, Transform ruined, TownLayout t)
        {
            EnsureDecalFeature(BeautyCornerBuilder.RendererPath);
            EnsureDecalFeature(PcRenderer);
            Texture2D Load(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            Material mMossC = DecalMat("Moss", Load(CellarAdopted + "C03b/C03b_albedo.png")), mMossS = DecalMat("MossSoft", Load(ArtDecals + "/Moss_BaseColor.png"));
            Material mLeafA = DecalMat("LeavesA", Load(ArtDecals + "/T15a_BaseColor.png")), mLeafB = DecalMat("LeavesB", Load(ArtDecals + "/T15b_BaseColor.png"));
            Material mMud = DecalMat("Mud", GenTex("Street_Mud.png", 128, (u, v) => { float a = Blob(u, v, 3, 0.45f) * Mathf.Lerp(0.75f, 1.0f, Fbm(u * 7f, v * 7f, 11)); float s = Fbm(u * 3f, v * 3f, 15); return new Color(Mathf.Lerp(0.15f, 0.26f, s), Mathf.Lerp(0.10f, 0.17f, s), Mathf.Lerp(0.06f, 0.10f, s), a); }));
            Material mPud = DecalMat("Puddle", GenTex("Street_Puddle.png", 128, (u, v) => { float a = Mathf.SmoothStep(0f, 1f, Blob(u, v, 7, 0.10f) * 1.6f) * 0.9f; float s = Fbm(u * 4f, v * 4f, 21); return new Color(Mathf.Lerp(0.05f, 0.09f, s), Mathf.Lerp(0.065f, 0.11f, s), Mathf.Lerp(0.075f, 0.13f, s), a); }));
            Material mSoot = DecalMat("Soot", GenTex("Street_Soot.png", 128, (u, v) => { float x = u * 2f - 1f, y = v * 2f - 1f, r = Mathf.Sqrt(x * x + y * y); float a = Mathf.Clamp01(1f - r) * Mathf.Clamp01(0.4f + 1.4f * Fbm(u * 5f, v * 5f, 31)); a = a * a * 1.1f; return new Color(0.03f, 0.028f, 0.026f, Mathf.Clamp01(a) * 0.9f); }));
            Material mDamp = DecalMat("Damp", GenTex("Street_Damp.png", 128, (u, v) => { float a = Blob(u, v, 5, 0.6f) * Mathf.Lerp(0.6f, 0.95f, Fbm(u * 5f, v * 5f, 41)); return new Color(0.07f, 0.065f, 0.055f, a); }));
            Material mGrime = DecalMat("Grime", GenTex("Street_Grime.png", 128, (u, v) => { float edge = Mathf.SmoothStep(0f, 0.22f, Mathf.Min(u, 1f - u) + 0.1f * (Fbm(u * 6f, v * 3f, 51) - 0.5f)); float vv = Mathf.SmoothStep(0.05f, 1f, v + 0.25f * (Fbm(u * 4f, v * 4f, 57) - 0.5f)); float a = edge * vv * Mathf.Lerp(0.5f, 0.85f, Fbm(u * 9f, v * 9f, 61)); return new Color(0.10f, 0.085f, 0.07f, a); }));
            AssetDatabase.SaveAssets();

            var list = new List<DecalSpec>();
            // gutter leaf drift: every third bay on Market Street, every seventh elsewhere on the lanes
            var gutters = LaneKitPlan.Plan(t).Where(r => r.In(false) && (r.Piece == LanePiece.Gutter || r.Piece == LanePiece.GutterChoked || r.Piece == LanePiece.GutterGrate)).ToList();
            var market = gutters.Where(r => r.Lane == "MarketStreet").OrderByDescending(r => r.Z).ThenBy(r => r.X).ToList();
            for (int i = 0; i < market.Count; i += 8) AddLeaf(list, market[i], i, mLeafA, mLeafB, 1.9f);
            var others = gutters.Where(r => r.Lane != "MarketStreet").OrderBy(r => r.Id, System.StringComparer.Ordinal).ToList();
            for (int i = 3; i < others.Count; i += 14) AddLeaf(list, others[i], i, mLeafA, mLeafB, 1.5f);
            // soot under every chimney stack of the ruined shells
            foreach (Transform tr in ruined.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "Chimney") continue;
                Renderer rd = tr.GetComponentInChildren<Renderer>(true); if (rd == null) continue;
                var p = new Vector2(rd.bounds.center.x, rd.bounds.center.z);
                float y = HollowmereLayout.Ground(t, p.x, p.y);
                int k = StableHash(tr.parent != null ? tr.parent.name + tr.position.x.ToString("F1") : "c");
                if (H(k, 9, 70) > 0.22f) continue;
                list.Add(new DecalSpec { M = mSoot, P = p, Y = y, W = Mathf.Lerp(2.2f, 3.2f, H(k, 1, 71)), Hh = Mathf.Lerp(2.2f, 3.2f, H(k, 2, 71)), Yaw = H(k, 3, 71) * 360f, Fade = 1f, Group = 1, Name = "Soot", Order = k });
            }
            // mud and puddles on the flat stretches of every lane (water stands where the grade is under 5%)
            foreach (Lane lane in t.Lanes)
            {
                if (lane.Id == "NorthWestGinnel" || lane.Ring > 2) continue;
                float len = HollowmereLayout.LaneLength(lane.Points); int lk = StableHash(lane.Id);
                for (float s = 3f; s < len - 3f; s += 3.5f)
                {
                    int si = (int)(s * 10f);
                    if (H(lk, si, 81) > 0.13f) continue;
                    HollowmereLayout.PointAlong(lane.Points, s, out TownPoint p, out TownPoint dir);
                    HollowmereLayout.PointAlong(lane.Points, Mathf.Min(len - 0.1f, s + 1.5f), out TownPoint p2, out _);
                    HollowmereLayout.PointAlong(lane.Points, Mathf.Max(0f, s - 1.5f), out TownPoint p0, out _);
                    float slope = Mathf.Abs(HollowmereLayout.Ground(t, p2.X, p2.Z) - HollowmereLayout.Ground(t, p0.X, p0.Z)) / 3f;
                    if (slope > 0.05f) continue;
                    float side = (H(lk, si, 82) - 0.5f) * (lane.Width - 1.4f);
                    var q = new Vector2(p.X + dir.Z * side, p.Z - dir.X * side);
                    if (!DecalSpot(q, out float y)) continue;
                    float yaw = H(lk, si, 83) * 360f;
                    list.Add(new DecalSpec { M = mMud, P = q, Y = y, W = Mathf.Lerp(2.0f, 3.2f, H(lk, si, 84)), Hh = Mathf.Lerp(1.6f, 2.6f, H(lk, si, 85)), Yaw = yaw, Fade = 1f, Group = 2, Name = "Mud", Order = lk + si });
                    if (H(lk, si, 86) < 0.5f)
                        list.Add(new DecalSpec { M = mPud, P = q, Y = y, W = Mathf.Lerp(0.9f, 1.6f, H(lk, si, 87)), Hh = Mathf.Lerp(0.8f, 1.3f, H(lk, si, 88)), Yaw = yaw + 40f, Fade = 1f, Group = 2, Name = "Puddle", Order = lk + si + 0.5f });
                }
            }
            // lane edges: dirt, damp and leaf drift lying along the kerb on both sides (the cobbles are never clean where they meet the verge)
            foreach (Lane lane in t.Lanes)
            {
                if (lane.Id == "NorthWestGinnel" || lane.Ring > 2) continue;
                float len = HollowmereLayout.LaneLength(lane.Points); int lk = StableHash(lane.Id);
                for (int side = 0; side < 2; side++)
                    for (float s = 2f + 2f * H(lk, side, 141); s < len - 2f; s += 4.2f)
                    {
                        int si = (int)(s * 10f) + side * 100000;
                        if (H(lk, si, 142) > 0.4f) continue;
                        HollowmereLayout.PointAlong(lane.Points, s, out TownPoint p, out TownPoint dir);
                        float sg = side == 0 ? 1f : -1f, off = lane.Width * 0.5f - 0.25f;
                        var q = new Vector2(p.X + dir.Z * sg * off, p.Z - dir.X * sg * off);
                        if (!DecalSpot(q, out float y)) continue;
                        float r = H(lk, si, 143); Material m = r < 0.4f ? mDamp : r < 0.7f ? mMud : (si % 2 == 0 ? mLeafA : mLeafB);
                        float yaw = Mathf.Atan2(dir.X, dir.Z) * Mathf.Rad2Deg + (H(lk, si, 144) - 0.5f) * 30f;
                        list.Add(new DecalSpec { M = m, P = q, Y = y, W = Mathf.Lerp(2.4f, 3.4f, H(lk, si, 145)), Hh = Mathf.Lerp(2.0f, 3.0f, H(lk, si, 146)), Yaw = yaw, Fade = 1f, Group = 2, Name = m == mDamp ? "Damp" : m == mMud ? "Mud" : "Leaves", Order = lk + si });
                    }
            }
            // wall feet: grime, moss, damp and mud, thicker on the square (4 per 10 m of edge) than in the lanes (3 per 10 m, both sides)
            foreach (WallSeg w in Walls)
            {
                if (w.Len < 1.5f) continue;
                Vector2 mid = w.A + w.Dir * (w.Len * 0.5f) + w.N * 0.9f;
                StreetZone z = ZoneOf(mid, out _, out _); if (z == StreetZone.None) continue;
                float spacing = z == StreetZone.Square ? 4.2f : 9.5f;
                int count = Mathf.FloorToInt(w.Len / spacing + H(w.Key, 0, 91));
                for (int c = 0; c < count; c++)
                {
                    float s = Mathf.Clamp((c + 0.15f + 0.7f * H(w.Key, c, 92)) * w.Len / count, 0.3f, w.Len - 0.3f);
                    float r = H(w.Key, c, 93), off = 0.35f + 0.5f * H(w.Key, c, 94);
                    Vector2 wp = w.A + w.Dir * s;
                    Material m; float dw, dh, yaw; Vector2 centre;
                    if (r < 0.42f)
                    {
                        dw = Mathf.Lerp(1.7f, 2.6f, H(w.Key, c, 95)); dh = Mathf.Lerp(0.9f, 1.3f, H(w.Key, c, 96)); m = mGrime;
                        centre = wp + w.N * (dh * 0.5f - 0.05f); yaw = Mathf.Atan2(-w.N.x, -w.N.y) * Mathf.Rad2Deg;   // the dark end of the texture against the wall
                    }
                    else
                    {
                        centre = wp + w.N * off; yaw = H(w.Key, c, 97) * 360f;
                        if (r < 0.62f) { m = mMossC; dw = dh = Mathf.Lerp(1.0f, 1.6f, H(w.Key, c, 98)); }
                        else if (r < 0.76f) { m = mMossS; dw = dh = Mathf.Lerp(1.6f, 2.6f, H(w.Key, c, 98)); }
                        else if (r < 0.90f) { m = mDamp; dw = dh = Mathf.Lerp(1.6f, 2.6f, H(w.Key, c, 98)); }
                        else { m = mMud; dw = dh = Mathf.Lerp(1.5f, 2.4f, H(w.Key, c, 98)); }
                    }
                    if (!DecalSpot(centre, out float y)) continue;
                    list.Add(new DecalSpec { M = m, P = centre, Y = y, W = dw, Hh = dh, Yaw = yaw, Fade = 1f, Group = 3, Name = m == mGrime ? "Grime" : m == mDamp ? "Damp" : m == mMud ? "Mud" : "Moss", Order = H(w.Key, c, 99) });
                }
            }
            int fixedCount = list.Count(d => d.Group < 3);
            var feet = list.Where(d => d.Group == 3).OrderBy(d => d.Order).Take(Mathf.Max(0, DecalBudget - fixedCount)).ToList();
            int idx = 0;
            foreach (DecalSpec d in list.Where(d => d.Group < 3).Concat(feet)) { AddDecal(parent, d, idx++); Count("decal" + d.Name); }
            Count("decals", idx);
            if (list.Count(d => d.Group == 3) > feet.Count) Count("decalsTrimmed", list.Count(d => d.Group == 3) - feet.Count);
        }

        static bool DecalSpot(Vector2 p, out float y)
        {
            y = 0f;
            if (!Surface(p.x, p.y, out y, out float ny) || ny < 0.9f) return false;
            if (Mathf.Abs(y - HollowmereLayout.Ground(T, p.x, p.y)) > 0.3f) return false;
            var tp = new TownPoint(p.x, p.y);
            foreach (Foundation f in T.Foundations) if (f.Footprint.Contains(tp, -0.1f)) return false;
            return true;
        }

        static void AddLeaf(List<DecalSpec> list, LaneKitRow r, int i, Material a, Material b, float size)
        {
            var p = new Vector2(r.X, r.Z);
            if (!DecalSpot(p, out float y)) return;
            list.Add(new DecalSpec { M = i % 2 == 0 ? a : b, P = p, Y = y, W = size, Hh = size, Yaw = H(i, 1, 101) * 360f, Fade = 1f, Group = 0, Name = "Leaves", Order = i });
        }

        static void AddDecal(Transform parent, DecalSpec d, int i)
        {
            var go = new GameObject("D" + i.ToString("000") + "_" + d.Name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(d.P.x, d.Y + 0.5f, d.P.y), Quaternion.Euler(90f, d.Yaw, 0f));
            var dp = go.AddComponent<DecalProjector>();
            dp.material = d.M; dp.size = new Vector3(d.W, d.Hh, 1.5f); dp.pivot = new Vector3(0f, 0f, 0.75f);
            dp.drawDistance = 70f; dp.fadeScale = 0.8f; dp.startAngleFade = 45f; dp.endAngleFade = 75f; dp.fadeFactor = d.Fade;
        }

        // ------------------------------------------------------------------------------------------------ wall-foot strips
        static void Strips(Transform parent, Transform ruined)
        {
            GameObject rubB = LoadPrefab(Adopted + "T05b/T05b.prefab"), rubC = LoadPrefab(Adopted + "T05c/T05c.prefab"), tile = LoadPrefab(Adopted + "T05a/T05a.prefab");
            GameObject grassE = LoadPrefab(Adopted + "T12e/T12e.prefab");
            var weeds = new List<GameObject>();
            foreach (string name in new[] { "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E" })
            {
                string folder = BeautyCornerArt.FindFolder("B9", name);
                string fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                GameObject go = fbx != null ? BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx)) : null;
                if (go != null) weeds.Add(go);
            }
            Material bramble = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_RuinBramble.mat");
            var spills = new List<Vector2>();
            foreach (Transform tr in ruined.GetComponentsInChildren<Transform>(true)) if (tr.name.StartsWith("Dress_Spill_", System.StringComparison.Ordinal)) spills.Add(new Vector2(tr.position.x, tr.position.z));
            Transform tufts = Group(parent, "Tufts"), rubble = Group(parent, "Rubble");
            for (int wi = 0; wi < Walls.Count; wi++)
            {
                WallSeg w = Walls[wi];
                if (w.Len < 1.0f) continue;
                Vector2 mid = w.A + w.Dir * (w.Len * 0.5f) + w.N * 0.9f;
                StreetZone z = ZoneOf(mid, out _, out _); if (z == StreetZone.None) continue;
                // tufts: about one per 1.5 m of wall foot, thinned by a hash
                int nT = Mathf.FloorToInt(w.Len / 1.5f);
                for (int i = 0; i < nT; i++)
                {
                    if (H(w.Key, i, 111) > 0.80f) continue;
                    float s = Mathf.Clamp((i + 0.5f + 0.8f * (H(w.Key, i, 112) - 0.5f)) * w.Len / nT, 0.2f, w.Len - 0.2f);
                    Vector2 p = w.A + w.Dir * s + w.N * Mathf.Lerp(0.18f, 0.5f, H(w.Key, i, 113));
                    if (!StripSpot(p, 1.3f, out float y)) continue;
                    bool grass = grassE != null && H(w.Key, i, 114) < 0.28f;
                    GameObject src = grass ? grassE : (weeds.Count > 0 ? weeds[(int)(H(w.Key, i, 115) * weeds.Count) % weeds.Count] : null);
                    if (src == null) continue;
                    GameObject go = HollowmereStreetClutter.Fit((GameObject)PrefabUtility.InstantiatePrefab(src, tufts), "Tuft_" + wi + "_" + i, p, y, Mathf.Lerp(grass ? 0.55f : 0.45f, grass ? 0.95f : 0.85f, H(w.Key, i, 116)), H(w.Key, i, 117) * 360f, 0f, 0f, 0.05f, out _);
                    if (!grass && bramble != null) foreach (Renderer rd in go.GetComponentsInChildren<Renderer>(true)) { var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)]; for (int q = 0; q < mats.Length; q++) mats[q] = bramble; rd.sharedMaterials = mats; rd.shadowCastingMode = ShadowCastingMode.Off; }
                    Count("tufts");
                }
                // rubble spill: a lot's lane-facing face with no T05 spill piece within 3 m of its middle gets one or two pieces 0.5 to 1.5 m out
                if (w.Lot && z != StreetZone.None && !spills.Any(sp => Vector2.Distance(sp, w.A + w.Dir * (w.Len * 0.5f) + w.N * 1f) < 3.2f) && H(w.Key, 0, 121) < 0.85f)
                {
                    int n = 1 + (H(w.Key, 1, 121) < 0.5f ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        GameObject src = H(w.Key, 2 + i, 121) < 0.55f ? rubB : rubC; if (src == null) continue;
                        Vector2 p = w.A + w.Dir * Mathf.Lerp(0.8f, Mathf.Max(0.9f, w.Len - 0.8f), H(w.Key, 4 + i, 121)) + w.N * Mathf.Lerp(0.5f, 1.5f, H(w.Key, 6 + i, 121));
                        if (!StripSpot(p, 1.6f, out float y)) continue;
                        HollowmereStreetClutter.Fit((GameObject)PrefabUtility.InstantiatePrefab(src, rubble), "Spill_" + wi + "_" + i, p, y, Mathf.Lerp(0.6f, 1.3f, H(w.Key, 8 + i, 121)), H(w.Key, 10 + i, 121) * 360f, (H(w.Key, 12 + i, 121) - 0.5f) * 30f, (H(w.Key, 14 + i, 121) - 0.5f) * 30f, 0.35f, out _);
                        Count("spill");
                    }
                }
            }
            // spill beside every broken garden wall and wall breach
            for (int i = 0; i < BrokenWallPoints.Count; i++)
                for (int k = 0; k < 3; k++)
                {
                    GameObject src = H(i, k, 131) < 0.4f ? rubB : H(i, k, 131) < 0.8f ? rubC : tile; if (src == null) continue;
                    Vector2 p = BrokenWallPoints[i] + new Vector2(H(i, k, 132) - 0.5f, H(i, k, 133) - 0.5f) * 3.2f;
                    if (!StripSpot(p, 1.6f, out float y)) continue;
                    HollowmereStreetClutter.Fit((GameObject)PrefabUtility.InstantiatePrefab(src, rubble), "BreachSpill_" + i + "_" + k, p, y, Mathf.Lerp(0.5f, 1.2f, H(i, k, 134)), H(i, k, 135) * 360f, (H(i, k, 136) - 0.5f) * 30f, (H(i, k, 137) - 0.5f) * 30f, 0.35f, out _);
                    Count("breachSpill");
                }
        }

        // A wall-foot plant or rubble piece: a flat free spot of a street zone, never in the lane's walking line or a doorstep (no collider, so no path clearance).
        static bool StripSpot(Vector2 p, float doorGap, out float y)
        {
            y = 0f;
            if (!Surface(p.x, p.y, out y, out float ny) || ny < 0.85f) return false;
            if (Mathf.Abs(y - HollowmereLayout.Ground(T, p.x, p.y)) > 0.3f) return false;
            var tp = new TownPoint(p.x, p.y);
            foreach (Foundation f in T.Foundations) if (f.Footprint.Contains(tp, 0.1f)) return false;
            foreach (Landmark l in T.Landmarks) if (l.Footprint.Contains(tp, 0.3f)) return false;
            StreetZone z = ZoneOf(p, out float le, out _);
            if (z == StreetZone.None || le < 0.0f && z != StreetZone.Square) return false;
            foreach (var n in nodes) if ((n.K == PathNodeKind.Doorstep || n.K == PathNodeKind.Door || n.K == PathNodeKind.Stair) && Vector2.Distance(n.P, p) < doorGap) return false;
            return true;
        }

        internal static GameObject LoadPrefab(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) Debug.LogWarning("SC HOLLOWMERE streets: prefab missing, its pieces are skipped: " + path);
            return p;
        }
    }
}
