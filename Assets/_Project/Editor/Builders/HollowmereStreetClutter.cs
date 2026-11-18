// Hollowmere street clutter (area 1 world, T06): position-hashed clusters of props against the walls of the square and the lanes, the D4 lane landmarks and the D1/D4/D5/D6 story props.
// (The brief's name HollowmereClutter.cs is taken by the Q14 builder, so T06's clusters live here; HollowmereStreets calls Build.) Preset_Ruined only, authority none (editor-time).
//   - Clusters (plan rule 3, one table row per zone): D1 6-8 props per 10 m of square edge, D4/D5 5-7 per 15 m of lane, D6 6-7 per 10 m at the inn front, always against a wall face
//     (a shell's footprint edge or a lane-kit wall). Off-grid, 2-8 degree tilts, hashed yaw and scale. Every prop passes HollowmereStreets.Valid (flat, free, off lots, doorsteps,
//     the stair, the lane's walking line and every eye camera); only solid props (barrels, crates, chests, the cart) get a box collider, and only where the path graph keeps its clearance.
//   - Landmarks: the smithy anvil and forge stack (R2_01), the schoolhouse bell post (R2_03), the workshop saw frame (R2_08); the collapsed gable at R2_05 is the shell itself.
//   - Story: Tobin's bench and stock pile at the bakery step, Wren's list wall, a market cart with weeds grown through its wheel; a table still set under a fallen beam, a child's cart
//     at a doorstep, a washing line left out; the broken cart at the Market Street mouth, a handcart loaded for a move; benches round a table with jugs and the inn sign face down.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereStreetClutter
    {
        const string MatFolder = "Assets/_Project/Content/Materials/Hollowmere";

        internal sealed class Kind
        {
            public string[] Src; public float Min, Max; public bool Solid, Along, Lay; public float Tilt = 5f, Sink = 0.03f;
            public Kind(float min, float max, params string[] src) { Min = min; Max = max; Src = src; }
        }

        internal static readonly Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>
        {
            ["Barrel"] = new Kind(0.75f, 0.95f, "C:C07b/C07b.prefab") { Solid = true, Tilt = 3f },
            ["Sack"] = new Kind(0.55f, 0.8f, "C:C11b/C11b.prefab") { Sink = 0.06f },
            ["Jug"] = new Kind(0.28f, 0.42f, "C:C06d/C06d.prefab", "C:C06e/C06e.prefab"),
            ["Pot"] = new Kind(0.3f, 0.5f, "C:C06a/C06a.prefab", "C:C06b/C06b.prefab"),
            ["Crate"] = new Kind(0.9f, 1.2f, "Q:CrateStack_Broken", "Q:CrateStack") { Solid = true },
            ["Planks"] = new Kind(1.1f, 2.0f, "A:T05d/T05d.prefab") { Along = true, Tilt = 8f, Sink = 0.12f },
            ["Rubble"] = new Kind(0.45f, 1.1f, "A:T05b/T05b.prefab", "A:T05c/T05c.prefab") { Tilt = 8f, Sink = 0.3f },
            ["Tiles"] = new Kind(0.5f, 0.9f, "A:T05a/T05a.prefab") { Tilt = 8f, Sink = 0.2f },
            ["Logs"] = new Kind(0.7f, 1.2f, "A:T06a/T06a.prefab", "A:T06b/T06b.prefab") { Sink = 0.08f },
            ["Tool"] = new Kind(0.9f, 1.3f, "A:T16k/T16k.prefab", "A:T16l/T16l.prefab", "A:T16m/T16m.prefab") { Lay = true },
            ["Stool"] = new Kind(0.42f, 0.55f, "A:T16j/T16j.prefab") { Tilt = 6f },
            ["Wheel"] = new Kind(0.8f, 1.0f, "A:T16g/T16g.prefab") { Lay = true },
            ["Chest"] = new Kind(0.65f, 0.8f, "C:C12a/C12a.prefab") { Solid = true, Tilt = 3f },
            ["Fabric"] = new Kind(0.6f, 1.0f, "A:T09/T09.prefab") { Sink = 0.1f },
            ["Basket"] = new Kind(0.4f, 0.55f, "A:T08g/T08g.prefab"),
            ["Wreck"] = new Kind(1.6f, 1.9f, "Q:Handcart_Broken") { Solid = true, Along = true, Tilt = 6f },
            ["FlowerBox"] = new Kind(0.8f, 1.0f, "Q:FlowerBox_Broken") { Tilt = 4f },
        };

        static readonly (string K, float W)[] TSquare = { ("Barrel", 2), ("Sack", 3), ("Jug", 3), ("Pot", 2), ("Crate", 2), ("Planks", 2), ("Rubble", 2), ("Basket", 1), ("Stool", 1), ("Logs", 1), ("Tool", 1), ("Wheel", 1), ("Fabric", 1), ("Tiles", 1) };
        static readonly (string K, float W)[] TLane = { ("Barrel", 1), ("Sack", 2), ("Jug", 2), ("Pot", 2), ("Crate", 1), ("Planks", 3), ("Rubble", 3), ("Tiles", 2), ("Logs", 1), ("Tool", 1), ("Stool", 1), ("Chest", 1), ("Fabric", 1), ("Wheel", 1), ("Wreck", 0.5f) };
        static readonly (string K, float W)[] TInn = { ("Barrel", 3), ("Sack", 2), ("Jug", 3), ("Pot", 1), ("Crate", 1), ("Stool", 1), ("Basket", 1), ("Logs", 1), ("Planks", 1), ("Rubble", 1), ("FlowerBox", 1) };

        static (float Step, int Min, int Max, float Spread, float Out) RuleOf(StreetZone z) =>
            z == StreetZone.Square ? (10f, 6, 8, 2.6f, 1.1f) : z == StreetZone.Inn ? (10f, 6, 7, 2.5f, 1.3f) : (15f, 5, 7, 2.0f, 0.9f);

        static (string K, float W)[] TableOf(StreetZone z) => z == StreetZone.Square ? TSquare : z == StreetZone.Inn ? TInn : TLane;

        static Transform story, landmarks;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static void Build(Transform clutter, Transform landmarkRoot, Transform storyRoot, TownLayout t)
        {
            story = storyRoot; landmarks = landmarkRoot; mats.Clear();
            Clusters(clutter);
            Landmarks();
            Story();
        }

        // ------------------------------------------------------------------------------------------------ clusters
        static void Clusters(Transform parent)
        {
            Dictionary<StreetZone, Transform> groups = new Dictionary<StreetZone, Transform>();
            for (int wi = 0; wi < HollowmereStreets.Walls.Count; wi++)
            {
                WallSeg w = HollowmereStreets.Walls[wi];
                if (w.Len < 1.4f) continue;
                Vector2 mid = w.A + w.Dir * (w.Len * 0.5f) + w.N * 0.9f;
                StreetZone z = HollowmereStreets.ZoneOf(mid, out float mle, out Lane ml);
                if (z == StreetZone.None || (ml != null && ml.Id == "NorthWestGinnel" && z != StreetZone.Square)) continue;
                var rule = RuleOf(z); var table = TableOf(z);
                float total = table.Sum(e => e.W);
                int count = Mathf.FloorToInt(w.Len / rule.Step + HollowmereStreets.H(w.Key, 0, 31));
                if (!groups.TryGetValue(z, out Transform zg)) groups[z] = zg = HollowmereStreets.Group(parent, z.ToString());
                for (int c = 0; c < count; c++)
                {
                    float s0 = (c + 0.2f + 0.6f * HollowmereStreets.H(w.Key, c, 32)) * w.Len / count;
                    int n = rule.Min + Mathf.FloorToInt(HollowmereStreets.H(w.Key, c, 33) * (rule.Max - rule.Min + 1));
                    for (int i = 0; i < n; i++)
                    {
                        int hk = c * 16 + i;
                        float pick = HollowmereStreets.H(w.Key, hk, 36) * total, acc = 0f; string kn = table[0].K;
                        foreach (var e in table) { acc += e.W; if (pick <= acc) { kn = e.K; break; } }
                        Kind kind = Kinds[kn];
                        float ext = Mathf.Lerp(kind.Min, kind.Max, HollowmereStreets.H(w.Key, hk, 37));
                        float along = Mathf.Clamp(s0 + (HollowmereStreets.H(w.Key, hk, 34) - 0.5f) * 2f * rule.Spread, 0.3f, w.Len - 0.3f);
                        float outD = 0.35f + rule.Out * HollowmereStreets.H(w.Key, hk, 35) + ext * 0.3f;
                        Vector2 p = w.A + w.Dir * along + w.N * outD;
                        float yaw = (kind.Along ? Mathf.Atan2(w.Dir.x, w.Dir.y) * Mathf.Rad2Deg + (HollowmereStreets.H(w.Key, hk, 38) - 0.5f) * 50f : HollowmereStreets.H(w.Key, hk, 38) * 360f);
                        float tilt = kind.Lay ? 82f + HollowmereStreets.H(w.Key, hk, 39) * 8f : 2f + (kind.Tilt - 2f) * HollowmereStreets.H(w.Key, hk, 39);
                        float roll = (HollowmereStreets.H(w.Key, hk, 40) - 0.5f) * 2f * (kind.Lay ? 20f : kind.Tilt);
                        if (!HollowmereStreets.Valid(p, kind.Solid, ext * 0.5f, out float y)) { HollowmereStreets.Count("clusterRejected"); continue; }
                        string src = kind.Src[(int)(HollowmereStreets.H(w.Key, hk, 41) * kind.Src.Length) % kind.Src.Length];
                        GameObject go = Make(src, zg);
                        if (go == null) continue;
                        Strip(go);
                        Fit(go, kn + "_" + wi + "_" + hk, p, y, ext, yaw, kind.Lay ? tilt : (HollowmereStreets.H(w.Key, hk, 42) - 0.5f) * 2f * tilt, kind.Lay ? roll : roll, kind.Sink, out _);
                        if (kind.Solid && HollowmereClutter.VisibleBounds(go.transform, out Bounds vb)) { var box = go.AddComponent<BoxCollider>(); box.center = vb.center; box.size = vb.size * 0.9f; Physics.SyncTransforms(); HollowmereStreets.Count("clusterSolid"); }
                        HollowmereStreets.Count("cluster" + z); HollowmereStreets.Count("clusterProps");
                    }
                    HollowmereStreets.Count("clusters");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ helpers
        internal static GameObject Make(string src, Transform parent)
        {
            if (src.StartsWith("Q:", System.StringComparison.Ordinal)) return HollowmereClutter.Instantiate(src.Substring(2), parent);
            if (src.StartsWith("R:", System.StringComparison.Ordinal)) return HollowmereRoad.Prop(src.Substring(2), parent);
            GameObject pf = HollowmereStreets.LoadPrefab((src.StartsWith("C:", System.StringComparison.Ordinal) ? HollowmereStreets.CellarAdopted : HollowmereStreets.Adopted) + src.Substring(2));
            return pf == null ? null : (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        }

        internal static void Strip(GameObject go) { foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); }

        internal static Bounds RenderBounds(GameObject go)
        {
            Bounds b = default; bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r.name == "FOOTPRINT" || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b : new Bounds(go.transform.position + Vector3.up * 0.25f, Vector3.one * 0.5f);
        }

        // Scales the instance so its larger horizontal extent is `ext` (<= 0 keeps its own size), turns and tilts it, centres its bounds on `xz` and sits the bottom on `y` (less `sink` of its height).
        internal static GameObject Fit(GameObject go, string name, Vector2 xz, float y, float ext, float yaw, float tiltX, float tiltZ, float sink, out Bounds wb)
        {
            go.name = name;
            go.transform.SetPositionAndRotation(new Vector3(xz.x, y, xz.y), Quaternion.identity); go.transform.localScale = Vector3.one;
            if (ext > 0f) { Bounds b0 = RenderBounds(go); go.transform.localScale = Vector3.one * Mathf.Clamp(ext / Mathf.Max(0.05f, Mathf.Max(b0.size.x, b0.size.z)), 0.02f, 30f); }
            go.transform.rotation = Quaternion.Euler(tiltX, yaw, tiltZ);
            Bounds b = RenderBounds(go);
            go.transform.position += new Vector3(xz.x - b.center.x, y - sink * b.size.y - b.min.y, xz.y - b.center.z);
            wb = RenderBounds(go);
            return go;
        }

        static Material Mat(string name, Color c, float smooth)
        {
            if (mats.TryGetValue(name, out Material cached)) return cached;
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_" + name + ".mat");
            if (m == null || name.StartsWith("ST_", System.StringComparison.Ordinal))
            {
                m = LookTestBuilder.Ensure(MatFolder + "/M_" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c); m.SetColor("_Color", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", name.Contains("Bronze") ? 0.8f : 0f);
                m.enableInstancing = true; MaterialValidation.Validate(m); EditorUtility.SetDirty(m);
            }
            return mats[name] = m;
        }

        static Material Oak() => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/M_CL_OakGrey.mat") ?? Mat("ST_Oak", new Color(0.30f, 0.22f, 0.15f), 0.2f);

        // A box beam in a frame: centre, size, euler, in `frame`'s local space.
        static GameObject Beam(Transform frame, string name, Vector3 c, Vector3 size, Vector3 euler, Material m)
        {
            GameObject g = LookTestBuilder.Prim(frame, name, PrimitiveType.Cube, c, size, m);
            g.transform.localRotation = Quaternion.Euler(euler);
            return g;
        }

        // T13a: a beam, post or plank of the adopted timber props (T05d plank, T06 log) instead of a scaled cube: the prop is turned so its long axis is the holder's +X, stretched to `len` and
        // thinned to `thick` (cross-section keeps its own proportions), centred on `c` in `frame`'s space and turned by `euler` (the holder's +X is the beam axis: euler z = 90 stands it up).
        internal static GameObject Timber(Transform frame, string name, Vector3 c, Vector3 euler, float len, float thick, string src = "A:T05d/T05d.prefab")
        {
            var holder = new GameObject(name).transform; holder.SetParent(frame, false);
            GameObject go = Make(src, holder);
            if (go == null) { Object.DestroyImmediate(holder.gameObject); return null; }
            Strip(go);
            holder.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            Bounds b0 = RenderBounds(go);
            int a = b0.size.x >= b0.size.y && b0.size.x >= b0.size.z ? 0 : b0.size.y >= b0.size.z ? 1 : 2;
            float[] sz = { b0.size.x, b0.size.y, b0.size.z };
            float cross = Mathf.Max(0.01f, a == 0 ? Mathf.Max(sz[1], sz[2]) : a == 1 ? Mathf.Max(sz[0], sz[2]) : Mathf.Max(sz[0], sz[1]));
            float k = thick / cross, sl = len / Mathf.Max(0.01f, sz[a]);
            go.transform.localScale = new Vector3(a == 0 ? sl : k, a == 1 ? sl : k, a == 2 ? sl : k);
            go.transform.localRotation = a == 0 ? Quaternion.identity : a == 1 ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.Euler(0f, 90f, 0f);
            Bounds b2 = RenderBounds(go);
            go.transform.position += -b2.center;
            holder.localPosition = c; holder.localRotation = Quaternion.Euler(euler);
            return holder.gameObject;
        }

        // T13a: one prop of a Kinds row at a chosen spot, with the cluster rules' own hashed size, yaw and tilt; null when the spot fails HollowmereStreets.Valid. Solid kinds get a box collider.
        internal static GameObject PlaceKind(Transform parent, string kn, string name, Vector2 p, int hA, int hB)
        {
            Kind kind = Kinds[kn];
            float ext = Mathf.Lerp(kind.Min, kind.Max, HollowmereStreets.H(hA, hB, 37));
            if (!HollowmereStreets.Valid(p, kind.Solid, ext * 0.5f, out float y)) return null;
            string src = kind.Src[(int)(HollowmereStreets.H(hA, hB, 41) * kind.Src.Length) % kind.Src.Length];
            GameObject go = Make(src, parent);
            if (go == null) return null;
            Strip(go);
            float yaw = HollowmereStreets.H(hA, hB, 38) * 360f;
            float tilt = kind.Lay ? 82f + HollowmereStreets.H(hA, hB, 39) * 8f : 2f + (kind.Tilt - 2f) * HollowmereStreets.H(hA, hB, 39);
            float roll = (HollowmereStreets.H(hA, hB, 40) - 0.5f) * 2f * (kind.Lay ? 20f : kind.Tilt);
            Fit(go, name, p, y, ext, yaw, kind.Lay ? tilt : (HollowmereStreets.H(hA, hB, 42) - 0.5f) * 2f * tilt, roll, kind.Sink, out _);
            if (kind.Solid && HollowmereClutter.VisibleBounds(go.transform, out Bounds vb)) { var box = go.AddComponent<BoxCollider>(); box.center = vb.center; box.size = vb.size * 0.9f; Physics.SyncTransforms(); }
            return go;
        }

        // The wall face of a lot that looks at the lane (or the square): the face whose street side point is nearest a lane edge.
        static bool LaneFace(string lotId, out WallSeg seg)
        {
            seg = default; float best = float.MaxValue; bool found = false;
            foreach (WallSeg w in HollowmereStreets.Walls)
            {
                if (!w.Lot || w.Id != lotId) continue;
                Vector2 p = w.A + w.Dir * (w.Len * 0.5f) + w.N * 1.2f;
                HollowmereStreets.ZoneOf(p, out float le, out _);
                if (le < best) { best = le; seg = w; found = true; }
            }
            return found;
        }

        static bool FaceToward(string lotId, Vector2 want, out WallSeg seg)
        {
            seg = default; float best = -2f; bool found = false;
            foreach (WallSeg w in HollowmereStreets.Walls)
                if (w.Lot && w.Id == lotId && Vector2.Dot(w.N, want) > best) { best = Vector2.Dot(w.N, want); seg = w; found = true; }
            return found;
        }

        static bool NearestWall(Vector2 want, float maxD, out WallSeg seg, out float along)
        {
            seg = default; along = 0f; float best = maxD; bool found = false;
            foreach (WallSeg w in HollowmereStreets.Walls)
            {
                if (w.Len < 2f) continue;
                float s = Mathf.Clamp(Vector2.Dot(want - w.A, w.Dir), 0.5f, w.Len - 0.5f);
                Vector2 q = w.A + w.Dir * s;
                if (Vector2.Dot(want - q, w.N) < -0.3f) continue;
                float d = Vector2.Distance(want, q);
                if (d < best) { best = d; seg = w; along = s; found = true; }
            }
            return found;
        }

        static Vector2 At(WallSeg w, float along, float outD) => w.A + w.Dir * along + w.N * outD;
        static float YawOut(WallSeg w) => Mathf.Atan2(w.N.x, w.N.y) * Mathf.Rad2Deg;
        static float YawAlong(WallSeg w) => Mathf.Atan2(w.Dir.x, w.Dir.y) * Mathf.Rad2Deg;

        static float GroundAt(Vector2 p) { HollowmereStreets.Surface(p.x, p.y, out float y, out _); return y; }

        // A story piece at a chosen spot: no placement rejection (the spot is chosen), a box collider only where the rules allow a solid thing there.
        static GameObject Put(Transform parent, string name, string src, Vector2 p, float ext, float yaw, float tiltX, float tiltZ, bool solid, float sink = 0.03f, float baseY = float.NaN)
        {
            GameObject go = Make(src, parent);
            if (go == null) { Debug.LogWarning("SC HOLLOWMERE streets: story piece " + name + " skipped (" + src + " missing)"); return null; }
            Strip(go);
            float y = float.IsNaN(baseY) ? GroundAt(p) : baseY;
            Fit(go, name, p, y, ext, yaw, tiltX, tiltZ, sink, out _);
            if (solid && HollowmereStreets.Valid(p, true, ext * 0.5f, out _, false) && HollowmereClutter.VisibleBounds(go.transform, out Bounds vb))
            { var box = go.AddComponent<BoxCollider>(); box.center = vb.center; box.size = vb.size * 0.9f; Physics.SyncTransforms(); HollowmereStreets.Count("storySolid"); }
            HollowmereStreets.Count("storyPieces");
            return go;
        }

        static float TopOf(GameObject go) => go == null ? 0f : RenderBounds(go).max.y;

        // ------------------------------------------------------------------------------------------------ lane landmarks (D4)
        static void Landmarks()
        {
            Material oak = Oak();
            Material bronze = Mat("ST_Bronze", new Color(0.42f, 0.28f, 0.10f), 0.5f), rope = Mat("ST_Rope", new Color(0.35f, 0.28f, 0.18f), 0.1f);
            // R2_01 smithy: anvil, forge stack, tongs, toolbox, quench barrel and firewood against the lot's lane face
            if (LaneFace("R2_01", out WallSeg sm))
            {
                Transform g = HollowmereStreets.Group(landmarks, "Smithy");
                float L = sm.Len;
                Put(g, "Anvil", "A:T14a/T14a.prefab", At(sm, L * 0.30f, 0.65f), 0.7f, YawAlong(sm), 0f, 0f, true);
                GameObject forge = Put(g, "ForgeStove", "C:C10b/C10b.prefab", At(sm, L * 0.72f, 0.75f), 1.15f, YawOut(sm) + 180f, 0f, 0f, true);
                float top = TopOf(forge);
                float[] exts = { 0.62f, 0.55f, 0.48f, 0.42f };
                for (int i = 0; i < exts.Length; i++)
                {
                    GameObject piece = Put(g, "ForgeStack_" + i, "A:T05b/T05b.prefab", At(sm, L * 0.72f + 0.04f * (i % 2 == 0 ? 1 : -1), 0.55f), exts[i], 90f * i + 20f, 0f, 0f, false, 0f, top);
                    top = TopOf(piece) - 0.06f;
                }
                Put(g, "Tongs", "A:T14b/T14b.prefab", At(sm, L * 0.30f + 0.8f, 0.5f), 0.5f, 35f, 80f, 0f, false);
                Put(g, "Toolbox", "A:T14c/T14c.prefab", At(sm, L * 0.30f - 0.9f, 0.55f), 0.55f, YawAlong(sm) + 8f, 0f, 0f, false);
                Put(g, "QuenchBarrel", "C:C07b/C07b.prefab", At(sm, L * 0.5f, 0.6f), 0.8f, 10f, 3f, 0f, true);
                Put(g, "Firewood", "A:T06b/T06b.prefab", At(sm, L * 0.9f, 0.55f), 0.9f, YawAlong(sm), 0f, 0f, false);
                HollowmereStreets.Count("landmarkSmithy");
            }
            // R2_03 schoolhouse bell post: two posts, a cross beam, the bell and its rope
            if (LaneFace("R2_03", out WallSeg sc))
            {
                var frame = new GameObject("BellPost").transform; frame.SetParent(HollowmereStreets.Group(landmarks, "School"), false);
                Vector2 p = At(sc, sc.Len * 0.5f, 0.75f);
                frame.SetPositionAndRotation(new Vector3(p.x, GroundAt(p), p.y), Quaternion.Euler(0f, YawAlong(sc), 0f));
                const string log = "A:T06b/T06b.prefab";   // T13a: posts and beams are the adopted log and plank props, not scaled cubes
                Timber(frame, "PostL", new Vector3(-0.65f, 1.2f, 0f), new Vector3(0f, 0f, 91.5f), 2.4f, 0.2f, log);
                Timber(frame, "PostR", new Vector3(0.65f, 1.2f, 0f), new Vector3(0f, 0f, 88.5f), 2.4f, 0.2f, log);
                Timber(frame, "Cross", new Vector3(0f, 2.34f, 0f), new Vector3(0f, 0f, 0f), 1.7f, 0.17f, log);
                Timber(frame, "BraceL", new Vector3(-0.45f, 2.05f, 0f), new Vector3(0f, 0f, 130f), 0.6f, 0.09f);
                Timber(frame, "BraceR", new Vector3(0.45f, 2.05f, 0f), new Vector3(0f, 0f, 50f), 0.6f, 0.09f);
                GameObject bell = LookTestBuilder.Prim(frame, "Bell", PrimitiveType.Sphere, new Vector3(0.05f, 1.95f, 0f), new Vector3(0.42f, 0.46f, 0.42f), bronze);
                bell.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
                LookTestBuilder.Prim(frame, "BellMouth", PrimitiveType.Cylinder, new Vector3(0.04f, 1.74f, 0f), new Vector3(0.44f, 0.03f, 0.44f), bronze);
                LookTestBuilder.Prim(frame, "BellRope", PrimitiveType.Cylinder, new Vector3(0.04f, 1.25f, 0f), new Vector3(0.025f, 0.45f, 0.025f), rope);
                HollowmereStreets.Count("landmarkBellPost"); HollowmereStreets.Count("storyPieces", 8);
            }
            // R2_08 workshop saw frame: two trestles, a log on top, the plane beside it, a plank leaning on the wall
            if (LaneFace("R2_08", out WallSeg ws))
            {
                var frame = new GameObject("SawFrame").transform; frame.SetParent(HollowmereStreets.Group(landmarks, "Workshop"), false);
                Vector2 p = At(ws, ws.Len * 0.5f, 0.8f);
                frame.SetPositionAndRotation(new Vector3(p.x, GroundAt(p), p.y), Quaternion.Euler(0f, YawAlong(ws), 0f));
                foreach (float x in new[] { -0.8f, 0.8f })
                {
                    Timber(frame, "LegA", new Vector3(x, 0.45f, 0.16f), new Vector3(-20f, 0f, 90f), 1.05f, 0.12f);
                    Timber(frame, "LegB", new Vector3(x, 0.45f, -0.16f), new Vector3(20f, 0f, 90f), 1.05f, 0.12f);
                    Timber(frame, "Rung", new Vector3(x, 0.3f, 0f), new Vector3(0f, -90f, 0f), 0.5f, 0.09f);
                    Timber(frame, "Top", new Vector3(x, 0.92f, 0f), new Vector3(0f, -90f, 0f), 0.42f, 0.13f, "A:T06b/T06b.prefab");
                }
                Timber(frame, "Rail", new Vector3(0f, 1.0f, 0.05f), new Vector3(0f, 0f, 1.5f), 2.1f, 0.12f, "A:T06b/T06b.prefab");
                Put(HollowmereStreets.Group(landmarks, "Workshop"), "SawLog", "A:T06a/T06a.prefab", p, 1.6f, YawAlong(ws), 0f, 0f, false, 0f, GroundAt(p) + 1.04f);
                Put(HollowmereStreets.Group(landmarks, "Workshop"), "Plane", "A:T14d/T14d.prefab", At(ws, ws.Len * 0.5f + 1.7f, 0.55f), 0.38f, 20f, 0f, 0f, false);
                Put(HollowmereStreets.Group(landmarks, "Workshop"), "LeaningPlank", "A:T05d/T05d.prefab", At(ws, ws.Len * 0.5f - 1.7f, 0.25f), 1.8f, YawAlong(ws) + 90f, 0f, 24f, false, 0f);
                HollowmereStreets.Count("landmarkSawFrame"); HollowmereStreets.Count("storyPieces", 13);
            }
        }

        // ------------------------------------------------------------------------------------------------ story props
        static void Story()
        {
            Material oak = Oak();
            Transform d1 = HollowmereStreets.Group(story, "D1"), d4 = HollowmereStreets.Group(story, "D4"), d5 = HollowmereStreets.Group(story, "D5"), d6 = HollowmereStreets.Group(story, "D6");
            // ---- D1: Tobin's bench and stock pile at the bakery step (F02's south face; the build point at its doorstep stays clear: bench 3.6 m, pile 3.0 m off)
            if (FaceToward("F02", Vector2.down, out WallSeg bk))
            {
                float mid = bk.Len * 0.5f;
                Put(d1, "TobinBench", "A:T16e/T16e.prefab", At(bk, mid + 3.7f, 0.5f), 1.7f, YawAlong(bk), 0f, 0f, true);
                Vector2 pile = At(bk, mid - 3.2f, 0.6f);
                for (int i = 0; i < 3; i++)
                    Put(d1, "TobinSack_" + i, "C:C11b/C11b.prefab", pile + new Vector2(0.55f * i - 0.5f, 0.12f * (i % 2)), 0.62f, 60f * i, 3f, 0f, false, 0.05f);
                Put(d1, "TobinCrate", "Q:CrateStack_Broken", pile + new Vector2(1.45f, 0.1f), 0.95f, 8f, 0f, 0f, false);
                GameObject basket = Put(d1, "TobinBasket", "A:T08g/T08g.prefab", pile + new Vector2(-1.25f, 0.15f), 0.5f, 20f, 0f, 0f, false);
                Put(d1, "TobinLoaf_0", "A:T08f/T08f.prefab", pile + new Vector2(-1.25f, 0.15f), 0.28f, 30f, 0f, 0f, false, 0f, TopOf(basket) - 0.12f);
                Put(d1, "TobinLoaf_1", "A:T08e/T08e.prefab", pile + new Vector2(-0.7f, 0.5f), 0.22f, 80f, 0f, 0f, false);
                HollowmereStreets.Count("storyTobin");
            }
            // ---- D1: Wren's list wall on the heroes' home's south wall (a board on two posts, papers pinned to it); no collider, the wall stands at z 7.5
            {
                Vector2 b = new Vector2(15.6f, 7.5f - 0.32f);
                var frame = new GameObject("WrenList").transform; frame.SetParent(d1, false);
                frame.SetPositionAndRotation(new Vector3(b.x, GroundAt(b), b.y), Quaternion.Euler(0f, 180f, 0f));   // the board faces the square (south)
                Timber(frame, "PostL", new Vector3(-0.8f, 0.95f, 0f), new Vector3(0f, 0f, 90f), 1.95f, 0.14f, "A:T06b/T06b.prefab");   // T13a: posts and a board of three planks, not scaled cubes
                Timber(frame, "PostR", new Vector3(0.8f, 0.95f, 0f), new Vector3(0f, 0f, 89f), 1.95f, 0.14f, "A:T06b/T06b.prefab");
                for (int pl = 0; pl < 3; pl++) Timber(frame, "BoardPlank_" + pl, new Vector3(0f, 0.95f + 0.4f * pl + 0.2f, -0.04f - 0.005f * pl), new Vector3(0f, 0f, (HollowmereStreets.H(pl, 5, 201) - 0.5f) * 1.6f), 1.8f, 0.4f);
                Timber(frame, "Cap", new Vector3(0f, 2.0f, -0.02f), new Vector3(0f, 0f, 0f), 2.0f, 0.13f);
                Material paper = Mat("ST_Paper", new Color(0.80f, 0.74f, 0.58f), 0.1f), paperB = Mat("ST_PaperOld", new Color(0.62f, 0.52f, 0.34f), 0.1f);
                for (int i = 0; i < 9; i++)
                {
                    int cx = i % 3, cy = i / 3;
                    float px = (cx - 1) * 0.52f + (HollowmereStreets.H(i, 1, 201) - 0.5f) * 0.1f, py = 1.1f + cy * 0.38f + (HollowmereStreets.H(i, 2, 201) - 0.5f) * 0.08f;
                    GameObject sheet = LookTestBuilder.Prim(frame, "Paper_" + i, PrimitiveType.Quad, new Vector3(px, py, -0.1f), new Vector3(0.34f, 0.44f, 1f), i % 3 == 1 ? paperB : paper);
                    sheet.transform.localRotation = Quaternion.Euler(0f, 0f, (HollowmereStreets.H(i, 3, 201) - 0.5f) * 14f);
                }
                HollowmereStreets.Count("storyWrenList"); HollowmereStreets.Count("storyPieces", 13);
            }
            // ---- D1: a market cart with weeds grown through its wheel, in the open south-east of the square (free-standing, searched for a free spot)
            if (FindSpot(new Vector2(9.5f, -11.5f), 3.5f, 1.1f, out Vector2 cp))
            {
                GameObject cart = Put(d1, "MarketCart", "A:T16b/T16b.prefab", cp, 2.3f, 38f, 0f, 7f, true, 0.02f);
                Put(d1, "CartWheel", "A:T16g/T16g.prefab", cp + new Vector2(1.2f, -0.9f), 0.9f, 20f, 78f, 0f, false);
                string[] weedSrc = { "A:T12e/T12e.prefab", "A:T12d/T12d.prefab" };
                for (int i = 0; i < 4; i++)
                    Put(d1, "CartWeed_" + i, weedSrc[i % 2], cp + new Vector2(1.2f, -0.9f) + new Vector2(HollowmereStreets.H(i, 1, 211) - 0.5f, HollowmereStreets.H(i, 2, 211) - 0.5f) * 0.7f, 0.55f + 0.2f * (i % 3), i * 90f, 0f, 0f, false, 0.05f);
                HollowmereStreets.Count("storyMarketCart");
            }
            // ---- D4: a table still set under a fallen beam on R2_06's lane face
            if (LaneFace("R2_06", out WallSeg tb))
            {
                Vector2 tp = At(tb, tb.Len * 0.5f - 1.0f, 0.85f);
                GameObject table = Put(d4, "SetTable", "A:T16i/T16i.prefab", tp, 1.3f, YawAlong(tb), 0f, 0f, true);
                float top = TopOf(table);
                Put(d4, "TableJug", "C:C06d/C06d.prefab", tp + tb.Dir * 0.3f, 0.3f, 10f, 0f, 0f, false, 0f, top);
                Put(d4, "TablePot", "C:C06a/C06a.prefab", tp - tb.Dir * 0.3f, 0.32f, 70f, 0f, 0f, false, 0f, top);
                Put(d4, "TableStool", "A:T16j/T16j.prefab", tp + tb.N * 0.7f, 0.5f, 25f, 0f, 0f, false);
                Put(d4, "FallenBeam", "A:T05d/T05d.prefab", tp + tb.Dir * 0.2f - tb.N * 0.1f, 3.0f, YawAlong(tb) + 8f, 0f, 14f, false, 0f, top - 0.05f);
                HollowmereStreets.Count("storySetTable");
            }
            // ---- D4: a child's cart at R2_07's doorstep (a small wheelbarrow stood 2 m along the wall from the door)
            if (LaneFace("R2_07", out WallSeg cd))
            {
                Put(d4, "ChildCart", "A:T16c/T16c.prefab", At(cd, cd.Len * 0.5f + 2.2f, 0.6f), 0.95f, YawAlong(cd) + 25f, 0f, 0f, false);
                Put(d4, "ChildBall", "A:T08b/T08b.prefab", At(cd, cd.Len * 0.5f + 1.3f, 0.85f), 0.3f, 0f, 0f, 0f, false, 0.05f);
                HollowmereStreets.Count("storyChildCart");
            }
            // ---- D4: a washing line left out for ten years on R2_02's lane face: two posts, a sagging rope, a few rags
            if (LaneFace("R2_02", out WallSeg wl))
            {
                var frame = new GameObject("WashingLine").transform; frame.SetParent(d4, false);
                Vector2 p = At(wl, wl.Len * 0.5f, 0.55f);
                frame.SetPositionAndRotation(new Vector3(p.x, GroundAt(p), p.y), Quaternion.Euler(0f, YawAlong(wl), 0f));
                Material rope = Mat("ST_Rope", new Color(0.35f, 0.28f, 0.18f), 0.1f);
                Timber(frame, "PostL", new Vector3(-1.7f, 1.05f, 0f), new Vector3(0f, 0f, 92f), 2.1f, 0.13f, "A:T06b/T06b.prefab");   // T13a: log posts, a cord of thin cylinders, quad rags
                Timber(frame, "PostR", new Vector3(1.7f, 1.05f, 0f), new Vector3(0f, 0f, 86f), 2.1f, 0.13f, "A:T06b/T06b.prefab");
                const int seg = 8;
                for (int i = 0; i < seg; i++)
                {
                    float x0 = Mathf.Lerp(-1.7f, 1.7f, i / (float)seg), x1 = Mathf.Lerp(-1.7f, 1.7f, (i + 1) / (float)seg);
                    float y0 = 1.98f - 0.28f * Mathf.Sin(Mathf.PI * i / seg), y1 = 1.98f - 0.28f * Mathf.Sin(Mathf.PI * (i + 1) / seg);
                    GameObject cord = LookTestBuilder.Prim(frame, "Rope_" + i, PrimitiveType.Cylinder, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f), new Vector3(0.022f, Vector3.Distance(new Vector3(x0, y0, 0), new Vector3(x1, y1, 0)) * 0.5f, 0.022f), rope);
                    cord.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(y1 - y0, x1 - x0) * Mathf.Rad2Deg - 90f);
                }
                string[] cloth = { "ST_RagGrey", "ST_RagBlue", "ST_RagRed" };
                Color[] cc = { new Color(0.50f, 0.47f, 0.42f), new Color(0.30f, 0.38f, 0.52f), new Color(0.55f, 0.30f, 0.24f) };
                for (int i = 0; i < 4; i++)
                {
                    float x = -1.0f + i * 0.62f, y = 1.98f - 0.28f * Mathf.Sin(Mathf.PI * (x + 1.7f) / 3.4f);
                    float h = 0.5f + 0.25f * HollowmereStreets.H(i, 1, 221);
                    GameObject rag = LookTestBuilder.Prim(frame, "Rag_" + i, PrimitiveType.Quad, new Vector3(x, y - h * 0.5f, 0f), new Vector3(0.4f, h, 1f), Mat(cloth[i % 3], cc[i % 3], 0.05f));
                    rag.transform.localRotation = Quaternion.Euler(0f, (HollowmereStreets.H(i, 2, 221) - 0.5f) * 30f, (HollowmereStreets.H(i, 3, 221) - 0.5f) * 12f);
                    rag.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                }
                HollowmereStreets.Count("storyWashingLine"); HollowmereStreets.Count("storyPieces", 16);
            }
            // ---- D5: the broken cart at the Market Street mouth (against F07's west face), a handcart loaded for a move that never happened further down
            if (NearestWall(new Vector2(3.4f, -23f), 3f, out WallSeg mm, out float ma))
            {
                Put(d5, "MouthCart", "R:Cart_Broken", At(mm, ma, 1.3f), 2.2f, YawAlong(mm) + 18f, 0f, 5f, true);
                HollowmereStreets.Count("storyMouthCart");
            }
            if (NearestWall(new Vector2(-3.0f, -38f), 4f, out WallSeg hc, out float ha))
            {
                Vector2 hp = At(hc, ha, 1.0f);
                GameObject cart = Put(d5, "MoveHandcart", "Q:Handcart", hp, 1.9f, YawAlong(hc), 0f, 0f, true, 0.02f);
                float top = TopOf(cart);
                for (int i = 0; i < 3; i++) Put(d5, "MoveSack_" + i, "C:C11b/C11b.prefab", hp + hc.Dir * (0.45f * i - 0.45f), 0.6f, 40f * i, 0f, 0f, false, 0f, top - 0.35f);
                Put(d5, "MoveChest", "C:C12a/C12a.prefab", hp + hc.Dir * 0.55f + hc.N * 0.05f, 0.6f, YawAlong(hc) + 4f, 0f, 0f, false, 0f, top - 0.3f);
                Put(d5, "MoveStool", "A:T16j/T16j.prefab", hp - hc.Dir * 0.2f, 0.45f, 30f, 0f, 22f, false, 0f, top + 0.15f);
                HollowmereStreets.Count("storyHandcart");
            }
            // ---- D6: benches round a table with jugs on F09's south face (the inn front), the inn sign face down in the weeds
            if (FaceToward("F09", Vector2.down, out WallSeg inn))
            {
                float m = inn.Dir.x > 0f ? 3.2f : inn.Len - 3.2f;   // the square end of the face, in view from the InnFront eye camera
                Vector2 tp = At(inn, m, 1.15f);
                GameObject table = Put(d6, "InnTable", "A:T16i/T16i.prefab", tp, 1.5f, YawAlong(inn), 0f, 0f, true);
                float top = TopOf(table);
                Put(d6, "InnBenchA", "A:T16e/T16e.prefab", tp + inn.N * 0.95f, 1.6f, YawAlong(inn), 0f, 3f, true);
                Put(d6, "InnBenchB", "A:T16e/T16e.prefab", tp - inn.N * 0.95f, 1.6f, YawAlong(inn) + 180f, 0f, -4f, false);
                Put(d6, "InnStool", "A:T16j/T16j.prefab", tp + inn.Dir * 1.2f + inn.N * 0.2f, 0.5f, 10f, 0f, 38f, false);
                string[] jugs = { "C:C06d/C06d.prefab", "C:C06e/C06e.prefab", "C:C06d/C06d.prefab" };
                for (int i = 0; i < 3; i++) Put(d6, "InnJug_" + i, jugs[i], tp + inn.Dir * (-0.4f + 0.4f * i) + inn.N * (i == 1 ? 0.12f : -0.1f), 0.3f, 50f * i, 0f, i == 2 ? 70f : 0f, false, 0f, top);
                GameObject sign = HollowmereClutter.Instantiate("ShopSign_Inn", d6);
                if (sign != null)
                {
                    Strip(sign);
                    Vector2 sp = At(inn, m + 3.4f * Mathf.Sign(inn.Dir.x), 0.8f);
                    Fit(sign, "InnSignDown", sp, GroundAt(sp), 0f, YawAlong(inn) + 12f, 90f, 0f, 0.05f, out _);   // pivot on the wall face: tipped forward, board face down
                    HollowmereStreets.Count("storyPieces");
                }
                for (int i = 0; i < 3; i++) Put(d6, "InnSignWeed_" + i, i % 2 == 0 ? "A:T12e/T12e.prefab" : "A:T12d/T12d.prefab", At(inn, m + 3.4f * Mathf.Sign(inn.Dir.x) + (i - 1) * 0.55f, 0.8f + 0.25f * (i % 2)), 0.6f, i * 70f, 0f, 0f, false, 0.05f);
                HollowmereStreets.Count("storyInnTable");
            }
        }

        // A free spot near `want` (the wanted point first, then rings of hashed candidates) that passes the placement rules for a solid prop of the given half extent.
        static bool FindSpot(Vector2 want, float radius, float halfExt, out Vector2 pos)
        {
            if (HollowmereStreets.Valid(want, true, halfExt, out _, false)) { pos = want; return true; }
            for (int ring = 1; ring <= 3; ring++)
                for (int k = 0; k < 8; k++)
                {
                    float a = (k + HollowmereStreets.H(ring, k, 231)) * Mathf.PI * 0.25f, r = radius * ring / 3f;
                    Vector2 c = want + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (HollowmereStreets.Valid(c, true, halfExt, out _, false)) { pos = c; return true; }
                }
            Debug.LogWarning("SC HOLLOWMERE streets: no free spot within " + radius + " m of " + want + " for a story prop");
            pos = want; return false;
        }
    }
}
