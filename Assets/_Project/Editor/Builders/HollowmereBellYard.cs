// Area 1 world T09 (2026-11-17): the bell yard (D3, the dive pocket), the churchyard, the crag foot and the keep terrace's banks (D2). Same house pattern as HollowmereOrchard
// (a partial of it: table-driven, position-hashed, editor-time only, authority none, helpers Put/Ok/Decal/Weed shared).
//   Bell yard (x 2..21, z 26.5..37, ground 3.9): the dive kit by the hatch (rope coil, chest with a lantern, sack, rusty sword, barrel), a brazier with one warm point light,
//     weeds pushing through the cobble edge, ferns and moss at every wall foot of the church and the annex, moss/grime/leaf decals on the cobbles, scanned plants in the lawn.
//   Churchyard (lawn x -23.6..-9.8, z 23.3..30.6; HollowmereTerrain.KeepLawn takes the paving off it): a low dry-stone wall (T19e/f upright, T19g/h coping) along its south and
//     west edge with a gap for the path, old graves in rough rows under the churchyard tree (T18a-c, T21a-b, T19a), one grave freshly tended (cut grass: Tended() clears the
//     carpet, flowers, a cup), a bench under the tree, long grass and wild flowers between the stones, moss and leaf decals, mushrooms at the roots.
//   Keep banks: the terrace's west and east banks were terrain painted with the stretched cobble layer (a dark streaked plane); the terrain now paints them as earth and grass
//     (HollowmereTerrain.KeepBank) and this file dresses them with mossy boulders, ferns, ivy cards, rubble at the foot, moss and damp decals, and a cool shadowless fill.
//   Crag foot: scree spills, mushrooms, ferns and fallen branches along the foot of the bank behind the yard (the boulders themselves are HollowmereWild.CragFoot).
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        // ------------------------------------------------------------------------------------------------ zones (read by HollowmereTerrain, HollowmereFoliage and HollowmereWild)
        internal static readonly Vector2 GraveTended = new Vector2(-14.7f, 26.4f);
        const float TendedRadius = 1.7f;
        static readonly Vector2 HatchXZ = new Vector2(5.6f, 29.2f);
        static readonly Vector2 ChurchTree = new Vector2(-18.5f, 28.5f);

        // The churchyard lawn mask, 0..1 (1 = grass): the keep top's paving stops 9.8 m west of the hall's gable door and 23.3 m south... see KeepLawnBounds.
        internal static float KeepLawn(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.55f + 11f, z * 0.55f + 3f) - 0.5f;
            float d = Mathf.Min(-9.8f - x, z - 23.3f) + 0.9f * n;
            return Mathf.Clamp01(d / 0.8f);
        }

        // The keep terrace's flanking banks (x 22.5..35.5, both sides, z 18..37): earth and grass, not the stretched cobble layer.
        internal static float KeepBank(float x, float z)
        {
            float ax = Mathf.Abs(x);
            return (ax > 22.5f && ax < 38f && z > 18f && z < 37f) ? 1f : 0f;
        }

        // Where the bell zone's fine carpet and flowers run (the keep terrace's grass, the banks beside it and the foot of the crag), 0.29 m cells.
        internal static bool InBellZone(float x, float z) => Mathf.Abs(x) < 36f && z > 19.5f && z < 38.5f;

        // The tended grave's disc: no carpet grows here (the grass is cut), a few flowers are put by hand.
        internal static bool Tended(float x, float z) => (x - GraveTended.x) * (x - GraveTended.x) + (z - GraveTended.y) * (z - GraveTended.y) < TendedRadius * TendedRadius;

        // ------------------------------------------------------------------------------------------------ helpers
        static GameObject PutNat(Transform parent, string code, string name, Vector2 xz, float y, float scaleMul, float yaw, float tx, float tz, float sinkM, bool solid = false)
        {
            GameObject pf = Prefab(code);
            if (pf == null) { Count("missing_" + code); return null; }
            Bounds ub = Unit(pf);
            return Put(parent, code, name, xz, y, Mathf.Max(ub.size.x, Mathf.Max(ub.size.y, ub.size.z)) * scaleMul, yaw, tx, tz, sinkM, solid);
        }

        // A wall piece that may import lying flat (T19e/f: 4.3 x 0.42 x 2.0): stood upright, length scaled by lenMul, height by hMul, thickness kept (thickMul), turned yaw about Y.
        // The piece's length runs along the yaw direction (yaw 0: world X). Colliders: one box.
        static GameObject PutWall(Transform parent, string code, string name, Vector2 xz, float yaw, float lenMul, float hMul, float thickMul, float roll, float sinkM, float yBase = float.NaN)
        {
            GameObject pf = Prefab(code);
            if (pf == null) { Count("missing_" + code); return null; }
            Bounds ub = Unit(pf);
            bool flat = ub.size.y < ub.size.z * 0.6f;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.name = name;
            go.transform.localScale = flat ? new Vector3(lenMul, thickMul, hMul) : new Vector3(lenMul, hMul, thickMul);
            go.transform.rotation = Quaternion.Euler(flat ? -90f : 0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            float y = float.IsNaN(yBase) ? Gy(xz) : yBase;
            go.transform.position = new Vector3(xz.x, y, xz.y);
            Bounds b = Render(go);
            go.transform.position += new Vector3(xz.x - b.center.x, y - sinkM - b.min.y, xz.y - b.center.z);
            var bc = go.AddComponent<BoxCollider>(); bc.center = ub.center; bc.size = ub.size * 0.92f;
            Physics.SyncTransforms();
            Count("wallPieces"); Count("solidProps");
            return go;
        }

        // On the banks the layout ground is not the terrain (HollowmereStreets.Surface falls back to it when its short ray misses a 45 degree slope): sample the terrain itself.
        static float TY(Vector2 p) { Terrain t = Terrain.activeTerrain; return t.SampleHeight(new Vector3(p.x, 0f, p.y)) + t.transform.position.y; }

        static bool OkBank(Vector2 p, float rad)
        {
            var tp = new TownPoint(p.x, p.y);
            foreach (Foundation f in T.Foundations) if (f.Footprint.Contains(tp, 0.2f)) return false;
            foreach (Landmark l in T.Landmarks) if (l.Footprint.Contains(tp, 0.4f + rad)) return false;
            foreach (Vector2 c in eyeCams) if (Vector2.Distance(c, p) < 1.3f) return false;
            return true;
        }

        static void DecalT(Transform parent, string mat, Vector2 p, float w, float h, float yaw, string name)
        {
            Material m = DM(mat); if (m == null) return;
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(p.x, TY(p) + 1.2f, p.y), Quaternion.Euler(90f, yaw, 0f));
            var dp = go.AddComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            dp.material = m; dp.size = new Vector3(w, h, 3.2f); dp.pivot = new Vector3(0f, 0f, 1.6f);
            dp.drawDistance = 70f; dp.fadeScale = 0.8f; dp.startAngleFade = 60f; dp.endAngleFade = 89f; dp.fadeFactor = 1f;
            decals++; Count("decal" + mat);
        }

        static void WeedT(Transform flora, Vector2 p, int key, int salt, float s0, float s1)
        {
            string[] kits = { "Weed_A", "Weed_B", "Weed_C", "Weed_D", "Weed_E", "Grass_A", "Grass_B", "Grass_C" };
            string kit = kits[(int)(H(key, salt, 701) * kits.Length) % kits.Length];
            if (HollowmereFoliage.Tuft(flora, kit, new Vector3(p.x, TY(p) - 0.02f, p.y), H(key, salt, 702) * 360f, Mathf.Lerp(s0, s1, H(key, salt, 703))) != null) Count("tufts");
        }

        // The scanned rocks T20a-h and T11a have albedo means of 33-54 (sRGB), a third of T11b's: in shade they read black, in sun a wet brown. A base-colour multiplier brings them to
        // a mean near 75, a mossy stone; idempotent (same values every build). The colour property is gamma space: 1.5 here is about 1.8x in linear light.
        static void BrightenRocks()
        {
            var f = new Dictionary<string, float> { ["T20a"] = 1.5f, ["T20b"] = 1.75f, ["T20c"] = 1.5f, ["T20d"] = 1.55f, ["T20e"] = 1.65f, ["T20f"] = 1.7f, ["T20g"] = 1.7f, ["T20h"] = 1.75f, ["T11a"] = 1.75f };
            foreach (var kv in f)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Content/Materials/B1/M_" + kv.Key + ".mat");
                if (m == null) { Count("missing_mat_" + kv.Key); continue; }
                var c = new Color(kv.Value, kv.Value, kv.Value * 0.92f, 1f);
                m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        static Light AddLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            Count("lights");
            return l;
        }

        // ------------------------------------------------------------------------------------------------ entry
        public static void BuildBell(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            T = t; counts.Clear(); decals = 0; graveXZ.Clear();
            BrightenRocks();
            Physics.SyncTransforms();
            Transform g = Sub(ruined, "BellYard");
            Transform props = Sub(g, "Props"), flora = Sub(g, "Flora"), dec = Sub(g, "Decals"), lights = Sub(g, "Lights");
            Yard(props, flora, dec, lights);
            Churchyard(props, flora, dec);
            WallFeet2(props, flora, dec);
            Banks(props, flora, dec, lights);
            CragDressing(props, flora, dec);
            int fills = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(l => l.type != LightType.Directional && l.name.Contains("Fill"));
            Debug.Log("SC HOLLOWMERE bell: " + string.Join(", ", counts.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)) + ", decals=" + decals + ", fill lights in the scene=" + fills);
        }

        // ------------------------------------------------------------------------------------------------ the bell yard
        static void Yard(Transform props, Transform flora, Transform dec, Transform lights)
        {
            Transform g = Sub(props, "DiveKit");
            float gy = HollowmereLayout.KeepHeight;
            // the kit, laid out round the hatch the way a party leaves it after a dive: coil of rope at the hatch edge, a chest with a lantern on it, a sack, a rusty sword, a barrel
            Vector2 rope = HatchXZ + new Vector2(-1.1f, -0.75f);
            if (Ok(rope, 0.2f, true, false)) PutNat(g, "T21c", "RopeCoil", rope, Gy(rope), 1f, 35f, 0f, 0f, 0.02f);
            Vector2 chest = HatchXZ + new Vector2(1.9f, -1.9f);
            if (Ok(chest, 0.4f, true, false))
            {
                GameObject c = Put(g, "C12a", "DiveChest", chest, Gy(chest), 0.85f, 80f, 0f, 0f, 0.02f, true);
                if (c != null) { Bounds b = Render(c); Vector2 lp = new Vector2(b.center.x, b.center.z); Put(g, "C15c", "ChestLantern", lp, b.max.y - 0.02f, 0.38f, 120f, 0f, 0f, 0f); }
                Decal(dec, "Grime", chest + new Vector2(0.1f, 0.2f), 1.6f, 1.2f, 80f, "Grime_Chest");
            }
            Vector2 sack = HatchXZ + new Vector2(2.9f, -1.6f);
            if (Ok(sack, 0.3f, true, false)) PutNat(g, "T20w", "DiveSack", sack, Gy(sack), 1f, 200f, 4f, 3f, 0.03f);
            Vector2 sword = HatchXZ + new Vector2(0.6f, -1.55f);
            if (Ok(sword, 0.3f, true, false)) PutNat(g, "T20z", "RustySword", sword, Gy(sword), 1f, 25f, 0f, 0f, 0.0f);
            Vector2 barrel = new Vector2(3.6f, 27.9f);
            if (Ok(barrel, 0.5f, true, false)) PutNat(g, "T20t", "DiveBarrel", barrel, Gy(barrel), 1f, 40f, 0f, 0f, 0.02f, true);
            Vector2 bucket = new Vector2(3.9f, 28.1f);
            if (Ok(bucket, 0.25f, true, false)) Put(g, "T17c", "DiveBucket", bucket, Gy(bucket), 0.4f, 0f, 0f, 0f, 0.02f);
            // the brazier on the lawn's edge, a warm point light (the zone's one warm light)
            Vector2 brazier = HatchXZ + new Vector2(1.5f, 2.3f);
            GameObject bz = Ok(brazier, 0.5f, false, false) ? Put(g, "T16h", "Brazier", brazier, Gy(brazier), 1.4f, 0f, 0f, 0f, 0.02f, true) : null;
            if (bz != null)
            {
                Bounds fb = Render(bz);
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlit != null)
                {
                    Material fm = LookTestBuilder.Ensure(MatFolder + "/M_HM_BrazierFlame.mat", unlit); fm.SetColor("_BaseColor", new Color(1.0f, 0.52f, 0.14f)); EditorUtility.SetDirty(fm);
                    GameObject flame = LookTestBuilder.Prim(g, "BrazierFlame", PrimitiveType.Sphere, new Vector3(fb.center.x, fb.max.y - 0.04f, fb.center.z), new Vector3(0.34f, 0.5f, 0.34f), fm);
                    foreach (Collider fc in flame.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(fc);
                    flame.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            if (bz != null) { Bounds b = Render(bz); AddLight(lights, "BrazierWarm", new Vector3(b.center.x, b.max.y + 0.15f, b.center.z), new Color(1.0f, 0.62f, 0.30f), 7f, 9f); }
            Decal(dec, "Soot", brazier + new Vector2(0f, -0.2f), 1.8f, 1.8f, 30f, "Soot_Brazier");
            // the wall of the church behind the hatch: ferns, a mossy stone
            // cobbles: moss, grime and leaves at the joints and along the lawn edge (few, large decals)
            for (int i = 0; i < 5; i++)
            {
                float x = 4.5f + i * 3.7f + (H(i, 1, 1101) - 0.5f);
                Decal(dec, i % 2 == 0 ? "MossSoft" : "Moss", new Vector2(x, 30.2f), 3.2f, 1.8f, 90f + H(i, 2, 1101) * 20f, "CobbleMoss_" + i);
                Decal(dec, i % 2 == 0 ? "LeavesA" : "LeavesB", new Vector2(x + 1.2f, 27.3f + H(i, 3, 1101)), 3.0f, 2.6f, H(i, 4, 1101) * 360f, "CobbleLeaves_" + i);
            }
            Decal(dec, "Grime", HatchXZ + new Vector2(0f, 0f), 2.4f, 1.9f, 0f, "Grime_Hatch");
            // weeds pushing through the cobble edge on both sides of the walk
            for (int i = 0; i < 26; i++)
            {
                float x = 2.8f + i * 0.72f + (H(i, 5, 1102) - 0.5f) * 0.4f;
                if (H(i, 6, 1102) < 0.3f) continue;
                Vector2 n = new Vector2(x, 30.55f - H(i, 7, 1102) * 0.5f);
                if (Ok(n, 0.05f, false, false)) Weed(flora, n, 1102, i, 0.7f, 1.3f);
                Vector2 s = new Vector2(x, 26.7f + H(i, 8, 1102) * 0.4f);
                if (x > 8.6f && x < 15.2f && H(i, 9, 1102) < 0.6f && Ok(s, 0.05f, false, false)) Weed(flora, s, 1103, i, 0.6f, 1.1f);
            }
            // the lawn: scanned wild plants and flowers in loose groups, mushrooms at the tree roots, a fallen log and branches, two sunk boulders
            string[] plants = { "T21d", "T21e", "T21f", "T21g", "T21i", "T21j", "T21l", "T21m", "T21n", "T21o", "T19u", "T19x" };
            for (int i = 0; i < 46; i++)
            {
                Vector2 q = new Vector2(Mathf.Lerp(2.6f, 21f, H(i, 10, 1104)), Mathf.Lerp(31.2f, 35.2f, Mathf.Pow(H(i, 11, 1104), 0.8f)));
                if (!Ok(q, 0.1f, false, false) || (q - HatchXZ - new Vector2(1.5f, 2.3f)).magnitude < 0.9f) continue;
                PutNat(flora, plants[(int)(H(i, 12, 1104) * plants.Length) % plants.Length], "LawnPlant_" + i, q, Gy(q), Mathf.Lerp(0.75f, 1.25f, H(i, 13, 1104)), H(i, 14, 1104) * 360f, 0f, 0f, 0.02f);
                Count("plants");
            }
            Vector2 log = new Vector2(9.2f, 33.4f);
            if (Ok(log, 0.6f, false, false)) { PutNat(g, "T20r", "FallenLog", log, Gy(log), 1f, 70f, 0f, 0f, 0.1f); PutNat(g, "T20j", "LogMushrooms", log + new Vector2(0.5f, 0.4f), Gy(log), 1f, 20f, 0f, 0f, 0.02f); }
            Vector2 br = new Vector2(16.3f, 31.7f);
            if (Ok(br, 0.5f, false, false)) PutNat(g, "T20p", "Branch", br, Gy(br), 1f, 150f, 0f, 0f, 0.05f);
            Vector2[] stones = { new Vector2(9.6f, 31.3f), new Vector2(18.6f, 31.8f) };
            string[] sk = { "T20b", "T20e" };
            for (int i = 0; i < stones.Length; i++) if (Ok(stones[i], 0.4f, false, false)) PutNat(g, sk[i], "LawnStone_" + i, stones[i], Gy(stones[i]), 1f, 40f * i + 10f, 4f, -5f, 0.18f);
            Vector2 ivyRock = new Vector2(7.4f, 33.4f);
            if (Ok(ivyRock, 0.4f, false, false)) PutNat(g, "T20c", "BrazierRock", ivyRock, Gy(ivyRock), 1f, 80f, 0f, 0f, 0.2f);
        }

        // ------------------------------------------------------------------------------------------------ the churchyard
        static readonly List<Vector2> graveXZ = new List<Vector2>();

        static void Churchyard(Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "Churchyard"), wall = Sub(g, "Wall"), graves = Sub(g, "Graves");
            float ty = HollowmereLayout.KeepHeight;
            // the low dry-stone wall: south edge (z 23.15) from the west edge to the hall's gable, with a gap where the path comes in; west edge (x -23.85) along the terrace's edge
            string[] kinds = { "T19e", "T19f" };
            int wi = 0;
            for (float x = -22.6f; x < -10.4f; x += 2.2f, wi++)
            {
                if (x + 1.1f > -18.4f && x - 1.1f < -15.4f) continue;   // the stile: the path graph's Keep_WStairHead -> Keep_Walk_W edge crosses this wall at x -16.9 (a piece there blocked the traversal scan)
                float h = H(wi, 1, 1201);
                if (h < 0.1f) continue;                   // a fallen length
                Vector2 p = new Vector2(x + (H(wi, 2, 1201) - 0.5f) * 0.25f, 23.15f);
                GameObject w = PutWall(wall, kinds[wi % 2], "ChurchWallS_" + wi, p, 0f + (H(wi, 3, 1201) - 0.5f) * 4f, 0.5f, Mathf.Lerp(0.40f, 0.58f, H(wi, 4, 1201)), 1f, (H(wi, 5, 1201) - 0.5f) * 5f, 0.04f);
                if (w != null && H(wi, 6, 1201) < 0.6f) { Bounds b = Render(w); PutNat(wall, wi % 2 == 0 ? "T19g" : "T19h", "CopingS_" + wi, new Vector2(b.center.x, b.center.z), b.max.y - 0.03f, 0.7f, 0f, 0f, 0f, 0f); }
                Decal(dec, "Moss", p + new Vector2(0f, 0.55f), 2.0f, 1.2f, 90f, "WallMossS_" + wi);
                if (H(wi, 7, 1201) < 0.7f) Weed(flora, p + new Vector2(0.6f, 0.5f), 1201, wi, 0.8f, 1.4f);
            }
            wi = 0;
            for (float z = 24.0f; z < 30.2f; z += 2.2f, wi++)
            {
                if (H(wi, 8, 1202) < 0.12f) continue;
                Vector2 p = new Vector2(-23.75f, z);
                GameObject w = PutWall(wall, kinds[(wi + 1) % 2], "ChurchWallW_" + wi, p, 90f, 0.5f, Mathf.Lerp(0.42f, 0.6f, H(wi, 9, 1202)), 1f, (H(wi, 10, 1202) - 0.5f) * 5f, 0.04f);
                if (w != null && H(wi, 11, 1202) < 0.5f) { Bounds b = Render(w); PutNat(wall, "T19h", "CopingW_" + wi, new Vector2(b.center.x, b.center.z), b.max.y - 0.03f, 0.7f, 90f, 0f, 0f, 0f); }
                Decal(dec, "MossSoft", p + new Vector2(0.55f, 0f), 2.0f, 1.2f, 0f, "WallMossW_" + wi);
            }
            // rubble from a fallen length at the stile
            for (int i = 0; i < 3; i++) { Vector2 q = new Vector2(-16.9f + (H(i, 1, 1203) - 0.5f) * 1.8f, 22.9f + H(i, 2, 1203) * 0.9f); PutNat(wall, i == 0 ? "T19k" : "T05b", "StileRubble_" + i, q, Gy(q), i == 0 ? 0.55f : 0.6f, H(i, 3, 1203) * 360f, 3f, -4f, 0.06f); }

            // graves in rough rows: four wavering rows running east-west, a stone missing here and there, none on the tended grave, none against the tree
            string[] stones = { "T18a", "T18b", "T18c", "T21a", "T21b", "T18a", "T18b", "T18c" };
            float[] rows = { 24.9f, 26.55f, 28.15f, 29.7f };
            int k = 0, nGraves = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                float x = -22.4f + H(r, 0, 1210) * 0.8f;
                int c = 0;
                while (x < -10.9f)
                {
                    c++;
                    Vector2 p = new Vector2(x, rows[r] + (H(r, c, 1211) - 0.5f) * 0.7f + 0.12f * Mathf.Sin(x * 0.9f + r));
                    x += 1.55f + H(r, c, 1212) * 0.85f;
                    if (H(r, c, 1213) < 0.20f) continue;
                    if (DistToSeg(p, new Vector2(-20.8f, 20.6f), new Vector2(-8f, 29.0f)) < 1.3f) continue;   // keep the path graph's diagonal through the churchyard clear
                    if (Vector2.Distance(p, ChurchTree) < 2.0f || Tended(p.x, p.y) || Vector2.Distance(p, GraveTended + new Vector2(0f, 0.9f)) < 1.2f) continue;
                    if (!Ok(p, 0.35f, false, true)) continue;
                    bool cross = H(r, c, 1214) < 0.14f;
                    string code = cross ? "T19a" : stones[(int)(H(r, c, 1215) * stones.Length) % stones.Length];
                    float size = cross ? Mathf.Lerp(0.95f, 1.15f, H(r, c, 1216)) : Mathf.Lerp(0.72f, 1.05f, H(r, c, 1216));
                    float sink = Mathf.Lerp(0.05f, 0.28f, H(r, c, 1217)) * (H(r, c, 1218) < 0.3f ? 1.6f : 1f);
                    GameObject go = Put(graves, code, "Grave_" + k++, p, Gy(p), size, 180f + (H(r, c, 1219) - 0.5f) * 36f, (H(r, c, 1220) - 0.5f) * 11f, (H(r, c, 1221) - 0.5f) * 20f, sink, true);
                    if (go != null) { graveXZ.Add(p); nGraves++; if (H(r, c, 1222) < 0.5f) Decal(dec, "Moss", p, 0.9f, 0.9f, H(r, c, 1223) * 360f, "GraveMoss_" + k); }
                }
            }
            Count("graves", nGraves);
            // the tended grave: a plain stone upright and true, the earth mounded, a cup at the foot, cut grass round it (Tended), a few cut flowers
            Vector2 tg = GraveTended;
            if (Ok(tg, 0.3f, false, false)) { Put(graves, "T21b", "TendedStone", tg, Gy(tg), 0.9f, 178f, 0f, 0f, 0.12f, true); graveXZ.Add(tg); }
            Decal(dec, "Mud", tg + new Vector2(0f, -0.9f), 1.1f, 1.9f, 0f, "TendedMound");
            Vector2 cup = tg + new Vector2(0.25f, -0.35f);
            Put(graves, "C06d", "TendedCup", cup, Gy(cup), 0.22f, 20f, 0f, 0f, 0.02f);
            string[] fl = { "T21d", "T21j", "T21e", "T21d", "T21j", "T21f", "T21d" };
            for (int i = 0; i < fl.Length; i++)
            {
                float a = i * 0.9f + 0.3f; Vector2 q = tg + new Vector2(Mathf.Cos(a) * (0.55f + 0.25f * H(i, 1, 1230)), -0.6f + Mathf.Sin(a) * (0.85f + 0.3f * H(i, 2, 1230)));
                PutNat(flora, fl[i], "TendedFlower_" + i, q, Gy(q), Mathf.Lerp(0.5f, 0.8f, H(i, 3, 1230)), H(i, 4, 1230) * 360f, 0f, 0f, 0.01f);
            }
            // the bench under the churchyard tree, a mound of leaves, mushrooms at the roots
            Vector2 bench = new Vector2(-20.7f, 25.9f);
            Put(graves, "T19b", "ChurchyardBench", bench, Gy(bench), 2.1f, 10f, 0f, 0f, 0.03f, true);
            Decal(dec, "LeavesA", ChurchTree + new Vector2(-1.2f, -1.5f), 3.4f, 3.0f, 30f, "TreeLeavesA");
            Decal(dec, "LeavesB", ChurchTree + new Vector2(1.6f, -0.8f), 3.0f, 2.6f, 130f, "TreeLeavesB");
            Decal(dec, "MossSoft", ChurchTree, 3.0f, 3.0f, 70f, "TreeMoss");
            for (int i = 0; i < 4; i++) { Vector2 q = ChurchTree + new Vector2(Mathf.Cos(i * 1.9f + 0.4f), Mathf.Sin(i * 1.9f + 0.4f)) * 1.15f; if (Ok(q, 0.1f, false, false)) PutNat(flora, i % 2 == 0 ? "T20k" : "T20m", "RootMushrooms_" + i, q, Gy(q), Mathf.Lerp(0.8f, 1.2f, H(i, 1, 1240)), H(i, 2, 1240) * 360f, 0f, 0f, 0.02f); }
            // long grass and wild flowers between the stones
            string[] plants = { "T21d", "T21e", "T21f", "T21g", "T21h", "T21i", "T21j", "T21l", "T21m", "T21n", "T21o", "T19u", "T19x" };
            int placed = 0;
            for (int i = 0; i < 130 && placed < 62; i++)
            {
                Vector2 q = new Vector2(Mathf.Lerp(-23.4f, -10.6f, H(i, 1, 1250)), Mathf.Lerp(23.8f, 30.4f, H(i, 2, 1250)));
                if (Tended(q.x, q.y) || Vector2.Distance(q, ChurchTree) < 0.9f) continue;
                bool near = false; foreach (Vector2 gp in graveXZ) if ((gp - q).sqrMagnitude < 0.28f) { near = true; break; }
                if (near || !Ok(q, 0.05f, false, false)) continue;
                string code = plants[(int)(H(i, 3, 1250) * plants.Length) % plants.Length];
                PutNat(flora, code, "ChurchyardPlant_" + i, q, Gy(q), Mathf.Lerp(0.7f, 1.25f, H(i, 4, 1250)), H(i, 5, 1250) * 360f, 0f, 0f, 0.02f);
                placed++; Count("plants");
            }
        }

        // ------------------------------------------------------------------------------------------------ wall feet of the hall and the annex (ferns, tufts, moss, damp)
        static void WallFeet2(Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "WallFeet");
            int ei = 0;
            foreach (string id in new[] { "KeepHall", "HeroesHome_Annex" })
            {
                Landmark lm = T.Landmarks.FirstOrDefault(l => l.Id == id);
                if (lm == null) { Count("missing_" + id); continue; }
                TownPoint[] c = lm.Footprint.Corners();
                var ctr = new Vector2((c[0].X + c[2].X) * 0.5f, (c[0].Z + c[2].Z) * 0.5f);
                for (int e = 0; e < 4; e++, ei++)
                {
                    Vector2 a = new Vector2(c[e].X, c[e].Z), b = new Vector2(c[(e + 1) % 4].X, c[(e + 1) % 4].Z);
                    Vector2 mid = (a + b) * 0.5f, d = (b - a); float len = d.magnitude; d /= len;
                    Vector2 n = (mid - ctr).normalized;
                    bool facing = n.y > -0.3f || n.x < -0.7f;   // north, east and west faces: the yard and the churchyard; the south face is on the terrace walk below the camera views
                    if (!facing) continue;
                    int nt = Mathf.RoundToInt(len / 0.85f);
                    for (int k = 0; k < nt; k++)
                    {
                        Vector2 q = a + d * ((k + 0.5f) * len / nt) + n * Mathf.Lerp(0.45f, 0.85f, H(ei, k, 1301));
                        if (H(ei, k, 1302) < 0.2f || !Ok(q, 0.05f, false, false)) continue;
                        if (H(ei, k, 1303) < 0.45f) PutNat(g, "T12b", "FootFern_" + ei + "_" + k, q, Gy(q), Mathf.Lerp(0.8f, 1.2f, H(ei, k, 1304)), H(ei, k, 1305) * 360f, 0f, 0f, 0.03f);
                        else Weed(flora, q, 1306 + ei, k, 0.8f, 1.5f);
                    }
                    int nd = Mathf.Max(1, Mathf.RoundToInt(len / 3.6f));
                    for (int k = 0; k < nd; k++)
                    {
                        Vector2 q = a + d * ((k + 0.5f) * len / nd) + n * 0.55f;
                        Decal(dec, (k + e) % 2 == 0 ? "Moss" : "Damp", q, 3.0f, 1.3f, YawFace(n) + 90f, "FootDecal_" + ei + "_" + k);
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the keep terrace's banks
        static void Banks(Transform props, Transform flora, Transform dec, Transform lights)
        {
            Transform g = Sub(props, "Banks");
            string[] rocks = { "T20a", "T20b", "T20c", "T20d", "T20e", "T20f", "T20g", "T20h" };
            foreach (int side in new[] { -1, 1 })
            {
                int key = 1400 + (side + 1) * 7;
                // boulders half sunk on the slope and at its foot, ferns between, ivy cards on the face, rubble at the toe
                for (int i = 0; i < 12; i++)
                {
                    float dx = Mathf.Lerp(23.2f, 30.4f, Mathf.Pow(H(i, 1, key), 0.9f));
                    Vector2 p = new Vector2(side * dx, Mathf.Lerp(19.6f, 31.8f, H(i, 2, key)));
                    if (!OkBank(p, 0.35f)) continue;
                    PutNat(g, rocks[(int)(H(i, 3, key) * rocks.Length) % rocks.Length], "BankRock_" + side + "_" + i, p, TY(p), Mathf.Lerp(0.55f, 1.15f, H(i, 4, key)), H(i, 5, key) * 360f, (H(i, 6, key) - 0.5f) * 20f, (H(i, 7, key) - 0.5f) * 20f, Mathf.Lerp(0.1f, 0.3f, H(i, 8, key)));
                }
                for (int i = 0; i < 16; i++)
                {
                    Vector2 p = new Vector2(side * Mathf.Lerp(23.0f, 30.2f, H(i, 9, key)), Mathf.Lerp(19.6f, 32.0f, H(i, 10, key)));
                    if (!OkBank(p, 0.2f)) continue;
                    PutNat(flora, i % 3 == 0 ? "T21l" : "T12b", "BankFern_" + side + "_" + i, p, TY(p), Mathf.Lerp(0.7f, 1.2f, H(i, 11, key)), H(i, 12, key) * 360f, 0f, 0f, 0.03f);
                }
                for (int i = 0; i < 7; i++)
                {
                    Vector2 p = new Vector2(side * Mathf.Lerp(23.6f, 27.8f, H(i, 13, key)), 20.4f + i * 1.75f + H(i, 14, key));
                    if (OkBank(p, 0.2f)) Put(flora, "T12c", "BankIvy_" + side + "_" + i, p, TY(p), Mathf.Lerp(1.4f, 2.1f, H(i, 15, key)), side < 0 ? 90f : 270f, 0f, side * 18f, 0.05f);
                }
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = new Vector2(side * Mathf.Lerp(28.4f, 31.2f, H(i, 16, key)), 21.2f + i * 2.9f + H(i, 17, key));
                    if (OkBank(p, 0.3f)) PutNat(g, i % 2 == 0 ? "T19k" : "T05b", "BankRubble_" + side + "_" + i, p, TY(p), i % 2 == 0 ? 0.6f : 0.55f, H(i, 18, key) * 360f, 3f, -4f, 0.08f);
                }
                for (int i = 0; i < 5; i++)
                {
                    Vector2 p = new Vector2(side * (25.3f + 0.8f * H(i, 19, key)), 21.0f + i * 2.6f);
                    DecalT(dec, i % 2 == 0 ? "Moss" : "Damp", p, 3.4f, 3.0f, H(i, 20, key) * 360f, "BankDecal_" + side + "_" + i);
                }
                for (int i = 0; i < 14; i++)
                {
                    Vector2 p = new Vector2(side * Mathf.Lerp(23.2f, 30.5f, H(i, 21, key)), Mathf.Lerp(19.6f, 32f, H(i, 22, key)));
                    if (OkBank(p, 0.05f)) WeedT(flora, p, key, i, 0.9f, 1.6f);
                }
            }
            // the terrace's south bank east of the east stair (the Eye_Climb view, 60 degrees, faces -z, z 17.5-19.5): rocks, ferns, ivy cards, moss and damp, tufts
            Transform sb = Sub(g, "SouthBank");
            for (int i = 0; i < 16; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(24.6f, 34f, (i + 0.5f * H(i, 1, 1460)) / 16f), 18.1f + H(i, 2, 1460) * 1.3f);
                if (!OkBank(p, 0.3f)) continue;
                if (i % 4 == 0) PutNat(sb, rocks[(i * 5) % rocks.Length], "SBankRock_" + i, p, TY(p), Mathf.Lerp(0.6f, 1.1f, H(i, 3, 1460)), H(i, 4, 1460) * 360f, (H(i, 5, 1460) - 0.5f) * 14f, (H(i, 6, 1460) - 0.5f) * 14f, 0.2f);
                else if (i % 4 == 1) PutNat(sb, i % 8 == 1 ? "T21l" : "T12b", "SBankFern_" + i, p, TY(p), Mathf.Lerp(0.8f, 1.3f, H(i, 7, 1460)), H(i, 8, 1460) * 360f, 0f, 0f, 0.03f);
                else if (i % 4 == 2) Put(sb, "T12c", "SBankIvy_" + i, p + new Vector2(0f, 0.6f), TY(p) + 0.1f, Mathf.Lerp(1.6f, 2.3f, H(i, 9, 1460)), 180f, 0f, 0f, 0.05f);
                else WeedT(flora, p, 1461, i, 0.9f, 1.6f);
            }
            for (int i = 0; i < 4; i++) DecalT(dec, i % 2 == 0 ? "Moss" : "Damp", new Vector2(26.5f + i * 2.4f, 18.8f), 3.2f, 2.6f, 0f, "SBankDecal_" + i);
            // the shelf's west scarp (x -35, 3 m of near-vertical earth, the HillWest camera looks straight at it from the ditch): faced with a dry-stone retaining wall of two
            // courses (T19e/f upright, 1.5 m each), ivy on a third of it, moss and damp at its foot, grass overhanging its top, ferns and rocks along the foot
            Transform sw = Sub(g, "ScarpWall");
            float baseY = TY(new Vector2(-35.9f, 25f)) - 0.05f;
            int ci = 0;
            for (float z = 19.3f; z < 31.6f; z += 2.15f, ci++)
            {
                if (H(ci, 1, 1450) < 0.07f) continue;   // one length has fallen
                {
                    // one course (two read as T-beams in the first frame: the 2 m deep blocks overhang when stacked)
                    Vector2 p = new Vector2(-35.55f + (H(ci, 2, 1450) - 0.5f) * 0.15f, z + (H(ci, 5, 1450) - 0.5f) * 0.2f);
                    PutWall(sw, (ci % 2 == 0) ? "T19e" : "T19f", "ScarpWall_" + ci, p, 90f + (H(ci, 8, 1450) - 0.5f) * 3f, 0.5f, 0.75f, 1f, (H(ci, 9, 1450) - 0.5f) * 2f, 0.08f, baseY);
                }
                Vector2 q = new Vector2(-36.5f, z + 1f);
                if (ci % 3 == 1) Put(sw, "T12c", "ScarpIvy_" + ci, new Vector2(-36.1f, z + 1f), baseY + 0.1f, 2.6f, 270f, 0f, 0f, 0.05f);
                if (ci % 2 == 0) PutNat(sw, ci % 4 == 0 ? "T21l" : "T12b", "ScarpFern_" + ci, new Vector2(-36.9f + H(ci, 11, 1450) * 0.6f, z + 0.3f + H(ci, 12, 1450)), baseY + 0.05f, 1.1f, H(ci, 13, 1450) * 360f, 0f, 0f, 0.03f);
                if (ci % 2 == 1) PutNat(sw, rocks[(ci * 3) % rocks.Length], "ScarpFootRock_" + ci, new Vector2(-37.1f, z + 0.6f), TY(new Vector2(-37.1f, z + 0.6f)), 0.55f, H(ci, 14, 1450) * 360f, 0f, 0f, 0.12f);
                DecalT(dec, ci % 2 == 0 ? "Moss" : "Damp", new Vector2(-36.6f, z + 1f), 2.6f, 3.4f, 90f, "ScarpDecal_" + ci);
                WeedT(flora, new Vector2(-35.2f, z + 0.5f), 1451, ci, 0.9f, 1.5f);
                WeedT(flora, new Vector2(-37.0f, z + 1.5f), 1452, ci, 0.8f, 1.3f);
            }
            // the cool, shadowless fills of the shade pocket (zone Z2): over the churchyard and at the scarp's foot (HillWest)
            AddLight(lights, "ChurchyardFill", new Vector3(-17f, HollowmereLayout.KeepHeight + 3.4f, 26.5f), new Color(0.80f, 0.87f, 1.0f), 6.0f, 14f);
            AddLight(lights, "WestBankFill", new Vector3(-38.0f, 0.2f, 25.5f), new Color(0.80f, 0.87f, 1.0f), 4.5f, 11f);
        }

        // ------------------------------------------------------------------------------------------------ the crag's foot behind the yard
        static void CragDressing(Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "CragFoot");
            for (int i = 0; i < 9; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(-22f, 23f, (i + 0.5f * H(i, 1, 1500)) / 9f), 35.2f + (H(i, 2, 1500) - 0.5f) * 1.6f);
                if (OkBank(p, 0.4f)) Put(g, "N03", "ScreeSpill_" + i, p, TY(p), Mathf.Lerp(1.4f, 2.4f, H(i, 3, 1500)), H(i, 4, 1500) * 360f, 0f, 0f, 0.1f);
            }
            for (int i = 0; i < 26; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(-23f, 23.5f, H(i, 5, 1500)), Mathf.Lerp(33.0f, 37.0f, H(i, 6, 1500)));
                if (!OkBank(p, 0.1f)) continue;
                string code = i % 4 == 0 ? "T20j" : i % 4 == 1 ? "T20l" : i % 4 == 2 ? "T12b" : "T21l";
                PutNat(flora, code, "CragPlant_" + i, p, TY(p), Mathf.Lerp(0.8f, 1.3f, H(i, 7, 1500)), H(i, 8, 1500) * 360f, 0f, 0f, 0.03f);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(-20f, 21f, H(i, 9, 1500)), Mathf.Lerp(33.4f, 36.0f, H(i, 10, 1500)));
                if (OkBank(p, 0.4f)) PutNat(g, i % 2 == 0 ? "T20o" : "T20q", "CragBranch_" + i, p, TY(p), 1f, H(i, 11, 1500) * 360f, 0f, 0f, 0.08f);
            }
            for (int i = 0; i < 4; i++)
                DecalT(dec, i % 2 == 0 ? "MossSoft" : "Damp", new Vector2(Mathf.Lerp(-18f, 18f, (i + 0.4f) / 4f), 34.0f), 4.0f, 3.2f, H(i, 12, 1500) * 360f, "CragDecal_" + i);
        }
    }
}
