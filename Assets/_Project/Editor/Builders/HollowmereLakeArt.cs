// Hollowmere lake and stream dressing (Q10, 2026-10-31): the jetty, reed clumps, shore stones and stream boulders from Content/Art/Town/Ring3/Lake/LakeShore.fbx,
// placed on the built terrain around the SC_Water meshes (HollowmereWater). Why: the water alone is a bare sheet against bare ground; the weirs need rock lips to
// break the falls, the shore needs reeds and stones at the waterline, and the lake wants one human mark (the jetty) in the Cap_HM_LakeJetty frame.
// How: materials M_LS_Rock/Reed/Jetty/Pile (URP Lit from the BaseColor/Normal/ORM sets, ORM unpacked to _ms/_occ like the corner's, GPU instancing on) under
// Content/Materials/Hollowmere; every piece is a MeshFilter + MeshRenderer built from the FBX's meshes (slot names -> materials), parented under one "LakeShore"
// object in the shared Set (both presets). Placement is deterministic (System.Random seed 1031) and samples the built terrain; nothing is placed on lanes, lots,
// landmarks (HollowmereTerrain.IsPaved, footprints with a 2 m margin), on the jetty line, or within 2.5 m of a capture camera.
//   Jetty        root on the north shore near x -22 where the terrain falls through the deck level going south, deck toward the lake centre (+2 deg off true),
//                deck top at WaterY + 0.6. Cap_HM_LakeJetty looks at it from the land side (HollowmereBuilder.Cameras).
//   Reeds        clusters of 3-5 clumps (yaw random, scale 0.85-1.2), 7 m apart, on lake ground -12.35..-11.75 (~60) and on ravine pool edges 0.1-0.4 m below the reach's level (~25).
//   Shore stones clusters of 3-5 on lake ground -12.3..-11.85 (base 3 cm under the ground, so the water stands ~17 cm up the stone; ~40) and on the pool banks (~15).
//   Boulders     a row across the channel on each weir lip (W1, W2) with three gaps where the fall shows, tops 0.2-0.5 m above the upper pool; plus boulders in each
//                pool, below the mill tail and above it (~35 in all).
// Authority: none (editor builder). Called from HollowmereWater.Build after the water.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereLakeArt
    {
        const string Folder = "Assets/_Project/Content/Art/Town/Ring3/Lake";
        const string TexFolder = Folder + "/Textures";
        const string MaterialFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string DerivedFolder = "Assets/_Project/Content/Textures/Hollowmere/LakeShore";
        const int Seed = 1031;
        const float CameraClear = 2.5f;

        sealed class Piece { public Mesh Mesh; public Material[] Mats; public bool Shadows = true; }

        static System.Random rng;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static TownLayout layout;
        static System.Func<float, float, float> ground;
        static Dictionary<string, Piece> pieces;
        static Transform root;
        static Vector2 jettyA, jettyB;
        static readonly List<Vector2> cams = new();
        static readonly Dictionary<string, int> counts = new();

        public static void Build(Transform set, TownLayout t, Terrain terrain)
        {
            rng = new System.Random(Seed);
            layout = t;
            float baseY = terrain.GetPosition().y;
            ground = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + baseY;
            pieces = LoadPieces();
            if (pieces == null) { Debug.LogWarning("SC HOLLOWMERE lake art: LakeShore.fbx missing, water left bare"); return; }
            counts.Clear(); cams.Clear();
            foreach (var c in HollowmereBuilder.Cameras) cams.Add(new Vector2(c.Pos.x, c.Pos.z));

            Transform old = set.Find("LakeShore"); if (old != null) Object.DestroyImmediate(old.gameObject);
            root = new GameObject("LakeShore").transform; root.SetParent(set, false);

            BuildJetty();
            ShoreReeds(60);
            PoolReeds(25);
            ShoreStones(40);
            PoolStones(15);
            Boulders();
            AssetDatabase.SaveAssets();
            Debug.Log("SC HOLLOWMERE lake art: " + string.Join(", ", counts.Select(kv => kv.Key + "=" + kv.Value)) + " jetty " + jettyA + " -> " + jettyB);
        }

        // ---------------------------------------------------------------------------------------------------------------- assets
        static Dictionary<string, Piece> LoadPieces()
        {
            GameObject fbx = BeautyCornerArt.LoadModel(Folder, "LakeShore.fbx", keepMaterialSlots: true);
            if (fbx == null) return null;
            BuilderUtil.EnsureFolder(MaterialFolder); BuilderUtil.EnsureFolder(DerivedFolder);
            var mats = new Dictionary<string, Material>
            {
                ["LS_Rock"] = MakeMat("LS_Rock", "Rock"), ["LS_Reed"] = MakeMat("LS_Reed", "Reed"),
                ["LS_Jetty"] = MakeMat("LS_Jetty", "Jetty"), ["LS_Pile"] = MakeMat("LS_Pile", "Pile"),
            };
            var map = new Dictionary<string, Piece>();
            foreach (MeshFilter mf in fbx.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                Material[] slots = mr.sharedMaterials.Select(m =>
                {
                    string n = m != null ? m.name : ""; int dot = n.IndexOf('.'); if (dot > 0) n = n.Substring(0, dot);
                    if (!mats.TryGetValue(n, out Material mm)) { Debug.LogWarning("SC HOLLOWMERE lake art: unknown slot '" + n + "' on " + mf.name); mm = mats["LS_Rock"]; }
                    return mm;
                }).ToArray();
                map[mf.name] = new Piece { Mesh = mf.sharedMesh, Mats = slots, Shadows = !mf.name.StartsWith("Reed") };
            }
            return map;
        }

        static Material MakeMat(string slot, string set)
        {
            string prefix = TexFolder + "/LakeShore_" + set;
            string bc = prefix + "_BaseColor.png", nm = prefix + "_Normal.png", orm = prefix + "_ORM.png";
            BeautyCornerArt.ImportTexture(bc, srgb: true, normal: false, alphaTransparency: false);
            BeautyCornerArt.ImportTexture(nm, srgb: false, normal: true, alphaTransparency: false);
            BeautyCornerArt.ImportTexture(orm, srgb: false, normal: false, alphaTransparency: false);
            // ORM -> _ms (R metallic, A smoothness) + _occ, imported by LookTestTextureImporter by name.
            string ms = DerivedFolder + "/" + slot + "_ms.png", occ = DerivedFolder + "/" + slot + "_occ.png";
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(src, File.ReadAllBytes(BuilderUtil.Abs(orm)), false)) throw new InvalidDataException("Cannot read " + orm);
            Color32[] px = src.GetPixels32();
            var msPx = new Color32[px.Length]; var occPx = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++) { msPx[i] = new Color32(px[i].b, 0, 0, (byte)(255 - px[i].g)); occPx[i] = new Color32(px[i].r, px[i].r, px[i].r, 255); }
            bool w1 = WritePng(BuilderUtil.Abs(ms), src.width, src.height, msPx), w2 = WritePng(BuilderUtil.Abs(occ), src.width, src.height, occPx);
            Object.DestroyImmediate(src);
            if (w1 || w2 || AssetImporter.GetAtPath(ms) == null) { AssetDatabase.ImportAsset(ms, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(occ, ImportAssetOptions.ForceUpdate); }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found");
            Material m = LookTestBuilder.Ensure(MaterialFolder + "/M_" + slot + ".mat", lit);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bc));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nm)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(occ)); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static bool WritePng(string abs, int w, int h, Color32[] px)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.SetPixels32(px);
            byte[] bytes = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            if (File.Exists(abs) && File.ReadAllBytes(abs).SequenceEqual(bytes)) return false;
            File.WriteAllBytes(abs, bytes);
            return true;
        }

        static GameObject Put(string kind, Transform parent, Vector3 pos, float yaw, float scale, Vector3? scaleXyz = null)
        {
            Piece p = pieces[kind];
            var go = new GameObject(kind); go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scaleXyz ?? Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = p.Mats;
            r.shadowCastingMode = p.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            string type = kind.StartsWith("Reed") ? "reeds" : kind.StartsWith("Rock_Stream") ? "boulders" : kind.StartsWith("Rock") ? "stones" : "jetty";
            counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;
            return go;
        }

        static Transform Group(string name) { var g = new GameObject(name).transform; g.SetParent(root, false); return g; }

        // ---------------------------------------------------------------------------------------------------------------- rules
        static bool Allowed(float x, float z)
        {
            var p = new TownPoint(x, z);
            if (HollowmereTerrain.IsPaved(layout, x, z)) return false;
            foreach (Foundation f in layout.Foundations) if (f.Footprint.Contains(p, 2f)) return false;
            foreach (Landmark l in layout.Landmarks) if (l.Footprint.Contains(p, 2f)) return false;
            foreach (Vector2 c in cams) if ((c.x - x) * (c.x - x) + (c.y - z) * (c.y - z) < CameraClear * CameraClear) return false;
            return true;
        }

        static float DistToJetty(float x, float z)
        {
            Vector2 p = new Vector2(x, z), ab = jettyB - jettyA;
            float f = Mathf.Clamp01(Vector2.Dot(p - jettyA, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, jettyA + ab * f);
        }

        static bool InLakeBasin(float x, float z)
        {
            float lx = (x - HollowmereLayout.LakeCentre.X) / (HollowmereLayout.LakeRadiusX + 8f), lz = (z - HollowmereLayout.LakeCentre.Z) / (HollowmereLayout.LakeRadiusZ + 8f);
            if (lx * lx + lz * lz > 1f) return false;
            HollowmereWater.Project(x, z, out _, out float dist);
            return dist > 14f;
        }

        static bool Spaced(List<Vector2> placed, float x, float z, float min)
        {
            foreach (Vector2 q in placed) if ((q.x - x) * (q.x - x) + (q.y - z) * (q.y - z) < min * min) return false;
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------------- jetty
        static void BuildJetty()
        {
            const float deckTop = HollowmereLayout.WaterY + 0.6f;
            float x = -22f, z = -72f;
            while (z > -100f && ground(x, z) > deckTop) z -= 0.1f;                 // north shore: the terrain falls through the deck level going south
            Vector2 shore = new Vector2(x, z);
            Vector2 toCentre = (new Vector2(HollowmereLayout.LakeCentre.X, HollowmereLayout.LakeCentre.Z) - shore).normalized;
            float yaw = Mathf.Atan2(toCentre.x, toCentre.y) * Mathf.Rad2Deg + 2f;   // 2 deg off true
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector2 rootXZ = shore - new Vector2(fwd.x, fwd.z) * 0.6f;               // the first plank end sits in the bank
            jettyA = rootXZ; jettyB = rootXZ + new Vector2(fwd.x, fwd.z) * 9f;
            GameObject j = Put("Jetty", root, new Vector3(rootXZ.x, deckTop, rootXZ.y), yaw, 1f);
            j.name = "Jetty";
        }

        // ---------------------------------------------------------------------------------------------------------------- reeds and stones
        static readonly string[] ReedKinds = { "Reed_A", "Reed_B", "Reed_C" };
        static readonly string[] ShoreKinds = { "Rock_Shore_A", "Rock_Shore_B", "Rock_Shore_C" };

        // Clusters of 3-5 members on lake ground inside [lo, hi]; spacing keeps gaps between clusters.
        static void LakeClusters(string group, int total, float lo, float hi, float spacing, float radius, string[] kinds, float sMin, float sMax, float sink, bool jettyClear)
        {
            Transform g = Group(group);
            var centres = new List<Vector2>();
            int placed = 0;
            for (int attempt = 0; attempt < 400000 && placed < total; attempt++)
            {
                float x = R(-80f, 100f), z = R(-146f, -58f);
                if (!InLakeBasin(x, z)) continue;
                float gy = ground(x, z);
                if (gy < lo || gy > hi || !Allowed(x, z) || !Spaced(centres, x, z, spacing)) continue;
                if (jettyClear && DistToJetty(x, z) < 4f) continue;
                centres.Add(new Vector2(x, z));
                int members = rng.Next(3, 6);
                for (int m = 0; m < members && placed < total; m++)
                    for (int tries = 0; tries < 8; tries++)
                    {
                        float a = R(0f, 6.2832f), d = m == 0 ? 0f : R(0.5f, radius);
                        float mx = x + Mathf.Cos(a) * d, mz = z + Mathf.Sin(a) * d, my = ground(mx, mz);
                        if (my < lo || my > hi || !Allowed(mx, mz) || (jettyClear && DistToJetty(mx, mz) < 4f)) continue;
                        Put(kinds[rng.Next(kinds.Length)], g, new Vector3(mx, my - sink, mz), R(0f, 360f), R(sMin, sMax));
                        placed++; break;
                    }
            }
        }

        static void ShoreReeds(int total) => LakeClusters("ShoreReeds", total, -12.35f, -11.75f, 7f, 1.6f, ReedKinds, 0.85f, 1.2f, 0f, true);
        static void ShoreStones(int total) => LakeClusters("ShoreStones", total, -12.3f, -11.85f, 5f, 1.4f, ShoreKinds, 0.8f, 1.3f, 0.03f, true);

        static float LevelOfReach(int i) => HollowmereWater.PoolLevels[i];
        // The three ravine reaches as (s0, s1, level).
        static (float s0, float s1, float level)[] Reaches()
        {
            var f = HollowmereWater.Falls;
            return new[] { (HollowmereWater.HeadS + 1.5f, f[0].S - 1f, LevelOfReach(0)), (f[0].S + f[0].Run + 1f, f[1].S - 1f, LevelOfReach(1)), (f[1].S + f[1].Run + 1f, f[2].S - 1f, LevelOfReach(2)) };
        }

        static Vector3 AtReach(float s, float d)
        {
            HollowmereWater.Frame(s, out Vector3 p, out Vector3 f);
            var c = new Vector3(-f.z, 0f, f.x);
            return new Vector3(p.x + c.x * d, 0f, p.z + c.z * d);
        }

        // Clusters along the ravine pool edges: ground between level - hiBelow and level - loBelow.
        static void PoolClusters(string group, int total, float loBelow, float hiBelow, float spacing, float radius, string[] kinds, float sMin, float sMax, float sink)
        {
            Transform g = Group(group);
            var reaches = Reaches();
            var centres = new List<Vector2>();
            int placed = 0;
            for (int attempt = 0; attempt < 200000 && placed < total; attempt++)
            {
                var (s0, s1, level) = reaches[rng.Next(reaches.Length)];
                Vector3 p = AtReach(R(s0, s1), R(-HollowmereWater.HalfWidth + 1f, HollowmereWater.HalfWidth - 1f));
                float gy = ground(p.x, p.z);
                if (gy > level - loBelow || gy < level - hiBelow || !Allowed(p.x, p.z) || !Spaced(centres, p.x, p.z, spacing)) continue;
                centres.Add(new Vector2(p.x, p.z));
                int members = rng.Next(3, 6);
                for (int m = 0; m < members && placed < total; m++)
                    for (int tries = 0; tries < 8; tries++)
                    {
                        float a = R(0f, 6.2832f), d = m == 0 ? 0f : R(0.5f, radius);
                        float mx = p.x + Mathf.Cos(a) * d, mz = p.z + Mathf.Sin(a) * d, my = ground(mx, mz);
                        if (my > level - loBelow || my < level - hiBelow || !Allowed(mx, mz)) continue;
                        Put(kinds[rng.Next(kinds.Length)], g, new Vector3(mx, my - sink, mz), R(0f, 360f), R(sMin, sMax));
                        placed++; break;
                    }
            }
        }

        static void PoolReeds(int total) => PoolClusters("PoolReeds", total, 0.1f, 0.4f, 6f, 1.4f, ReedKinds, 0.85f, 1.2f, 0f);
        static void PoolStones(int total) => PoolClusters("PoolStones", total, -0.05f, 0.4f, 5f, 1.3f, ShoreKinds, 0.8f, 1.3f, 0.03f);

        // ---------------------------------------------------------------------------------------------------------------- stream boulders
        static readonly string[] BoulderKinds = { "Rock_Stream_A", "Rock_Stream_B", "Rock_Stream_C" };
        static readonly float[] BoulderHeight = { 0.92f, 0.77f, 0.61f };

        static void Boulders()
        {
            Transform g = Group("StreamBoulders");
            var f = HollowmereWater.Falls;
            var placed = new List<Vector2>();

            // Weir lips (W1, W2): a row across the channel with three gaps where the fall ribbon shows; tops 0.2-0.5 m above the upper pool.
            for (int w = 0; w < 2; w++)
            {
                float up = HollowmereWater.PoolLevels[w];
                var gaps = new[] { R(-3.6f, -2.6f), R(-0.7f, 0.7f), R(2.4f, 3.6f) };
                for (float d = -6.5f; d <= 6.5f; d += 1.45f)
                {
                    if (gaps.Any(gc => Mathf.Abs(d - gc) < 0.95f)) continue;
                    Vector3 p = AtReach(f[w].S + R(0.05f, 0.3f), d + R(-0.15f, 0.15f));
                    if (!Allowed(p.x, p.z) || ground(p.x, p.z) > up + 1.2f) continue;
                    int k = rng.Next(1, 3);   // B or C on the lip; the big A stays in the pools
                    float sc = R(0.85f, 1.1f), rise = R(0.2f, 0.5f);
                    // Seat on the real terrain (0.15 m in), growing the rock if the bed is lower than the lip so the top still reaches it.
                    float gy = ground(p.x, p.z);
                    float need = (up + rise - gy + 0.15f) / BoulderHeight[k];
                    if (need > 1.5f) continue;
                    sc = Mathf.Max(sc, need);
                    Put(BoulderKinds[k], g, new Vector3(p.x, gy - 0.15f, p.z), R(0f, 360f), sc);
                    placed.Add(new Vector2(p.x, p.z));
                }
            }

            // In the pools, around the mill-tail fall and below it: on the bed in shallows, tops 0.15-0.5 m above the surface.
            var reaches = Reaches();
            var zones = new List<(float s0, float s1, float level, int count)>
            {
                (reaches[0].s0, reaches[0].s1, reaches[0].level, 6), (reaches[1].s0, reaches[1].s1, reaches[1].level, 5), (reaches[2].s0, reaches[2].s1 - 3f, reaches[2].level, 4),
                (f[2].S - 4f, f[2].S - 0.8f, HollowmereWater.PoolLevels[2], 4),
                (f[2].S + f[2].Run + 0.8f, f[2].S + f[2].Run + 8f, HollowmereWater.PoolLevels[3], 6),
            };
            // Counts add up to 25 here; the weir rows give ~15 more.
            foreach (var (s0, s1, level, count) in zones)
            {
                int done = 0;
                for (int attempt = 0; attempt < 20000 && done < count; attempt++)
                {
                    Vector3 p = AtReach(R(s0, s1), R(-HollowmereWater.HalfWidth + 1.5f, HollowmereWater.HalfWidth - 1.5f));
                    float gy = ground(p.x, p.z);
                    if (gy > level - 0.25f || gy < level - 1.3f || !Allowed(p.x, p.z) || !Spaced(placed, p.x, p.z, 3f)) continue;
                    int k = rng.Next(BoulderKinds.Length);
                    float sc = R(0.8f, 1.2f), need = level + R(0.15f, 0.5f) - gy;
                    if (need / BoulderHeight[k] > 1.5f) continue;
                    sc = Mathf.Max(sc, need / BoulderHeight[k]);
                    Put(BoulderKinds[k], g, new Vector3(p.x, gy - 0.03f, p.z), R(0f, 360f), sc);
                    placed.Add(new Vector2(p.x, p.z)); done++;
                }
            }
        }
    }
}
