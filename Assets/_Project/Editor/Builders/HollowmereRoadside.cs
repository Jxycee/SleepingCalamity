// Area 1 world T10 (2026-11-18): the road out (D10), its boundary, and the hill edge (D11). Same house pattern as HollowmereBellYard (a partial of HollowmereOrchard:
// table-driven, position-hashed, editor time only, authority none, helpers Put/PutSlope/DecalT/WeedT/OkBank shared).
//   Road boundary: the Saltwake road is closed 5.5 m past its node 6 (x about -122) by the T18d large fallen tree across it (root end on the north verge) and the T16a wagon
//     tipped on its broken axle with its load spilled (sacks, a rolled barrel, crates, a basket, straw), logs and branches piled behind; an invisible wall (a child of the
//     EdgeBounds root, so the traversal scan treats it as a world bound) stands 3.5 m behind the trunk across the whole terrain. Nodes 7-9 of the lane lie behind it and
//     are dropped from the traversal targets (TraversalSpaces). The old pad and fence rails at the terrain's west rim (HollowmereRoad.RoadEnd, EdgeBounds) stay as they were.
//   Road out: the Ruined roadside hedge is a row of scanned shrubs (TR05-TR07) instead of the Hedge_A/B leaf cards (their 3 x 3 atlas tiling spilled over the cell borders:
//     square edges and flat yellow quads at 1-3 m), ruts, puddles and a grass crown as a few large decals, verge flowers and weeds both sides (scanned wild plants), about two
//     props per 10 m (sack, stone heap, gate post, branch), flowers and a cup left at the shrine.
//   Hill edge: the keep's west and east flanks (x +-30..60, z 27..62): the terrain's soil is thinned (HillEdge) so grass and blades cover the faces, ferns and plants on a
//     jittered grid, sunk mixed rocks, a log or stump about every 12 m with mushrooms and branches, one mossy fallen tree per flank, moss and leaf decals, an old boundary stone
//     at the west foot and a worn path up the west slope that ends at a stump and a chopped log.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        internal const float RoadBlockWallX = -126f;   // the invisible wall behind the road's blockage (centre x of a 1 m bar)

        static float RsSm(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }

        // The hill flanks' mask (0..1, read by HollowmereTerrain): thins the soil so the 50-70 degree faces carry grass instead of one stretched brown plane.
        internal static float HillEdge(float x, float z)
        {
            float ax = Mathf.Abs(x);
            float flank = RsSm(29f, 33f, ax) * (1f - RsSm(58f, 64f, ax)) * RsSm(25f, 30f, z) * (1f - RsSm(60f, 68f, z));
            float crag = 0.8f * RsSm(17f, 23f, ax) * (1f - RsSm(34f, 40f, ax)) * RsSm(29f, 32f, z) * (1f - RsSm(48f, 56f, z));   // the crag's west and east shoulders behind the keep banks
            return Mathf.Max(flank, crag);
        }

        static TownPoint[] rp; static float rHalf = 2.5f;
        static Vector2 RoadAt(float s, out Vector2 dir)
        {
            HollowmereLayout.PointAlong(rp, s, out TownPoint c, out TownPoint d);
            dir = new Vector2(d.X, d.Z).normalized;
            return new Vector2(c.X, c.Z);
        }
        static Vector2 RightOf(Vector2 d) => new Vector2(d.y, -d.x);   // right of travel: north on the westward road
        static float ArcTo(int idx) { float a = 0f; for (int i = 1; i <= idx; i++) a += TownPoint.Distance(rp[i - 1], rp[i]); return a; }
        static Vector2 AxisOfYaw(float yaw) => new Vector2(Mathf.Cos(yaw * Mathf.Deg2Rad), -Mathf.Sin(yaw * Mathf.Deg2Rad));   // local +X turned by yaw

        static void FixRoadsideMaterials()
        {
            foreach (string c in new[] { "T18d", "T18e", "T16a", "T20o", "T20p", "T20q", "T20r", "T20s" }) MatFix(c, new Color(0.8f, 0.8f, 0.8f, 1f), 0.05f);
            MatFix("T20n", new Color(0.68f, 0.7f, 0.66f, 1f), 0.05f);   // the mossy tree reads lime yellow at white
            AssetDatabase.SaveAssets();
        }

        // +1 when the +Z half of the prefab is the wider (root) end, else -1.
        static float RsRootSign(GameObject pf)
        {
            float lo = 0f, hi = 0f;
            foreach (MeshFilter mf in pf.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name.Contains("LOD1") || mf.name.Contains("LOD2")) continue;
                Matrix4x4 m = pf.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (w.z > 0.6f) hi = Mathf.Max(hi, Mathf.Abs(w.x)); else if (w.z < -0.6f) lo = Mathf.Max(lo, Mathf.Abs(w.x));
                }
            }
            return hi >= lo ? 1f : -1f;
        }

        internal const float RsEyeClear = 4f;   // T10b: nothing the roadside places stands within 4 m of an eye camera (the RoadOut foreground slab, plank and stalks were 1.8-4.5 m away)
        static bool RsFree(Vector2 p, float rad) => HollowmereStreets.FreeOf(p, TY(p), rad) && !eyeCams.Any(c => Vector2.Distance(c, p) < RsEyeClear);

        // T10b: the soil around the road end greens over (the ploughed field's bare dirt): read by HollowmereTerrain
        internal static float RoadEndGrass(float x, float z) { float d = Mathf.Sqrt((x + 122f) * (x + 122f) + (z + 97f) * (z + 97f)); return 1f - RsSm(12f, 34f, d); }

        // ------------------------------------------------------------------------------------------------ entry
        public static void BuildRoadside(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            T = t; setRoot = set; counts.Clear(); int d0 = decals;
            FixRoadsideMaterials();
            Lane road = HollowmereLayout.FindLane(t, SaltwakeRoadPlan.RoadLane);
            rp = road.Points; rHalf = road.Width * 0.5f;
            Physics.SyncTransforms();
            Transform g = Sub(ruined, "Roadside");
            Transform props = Sub(g, "Props"), flora = Sub(g, "Flora"), dec = Sub(g, "Decals");
            float sBlock = ArcTo(6) + 5.5f;
            RoadHedges(Sub(props, "Hedges"));
            Block(Sub(props, "Block"), flora, dec, sBlock);
            Verge(Sub(props, "Verge"), flora, dec, sBlock);
            RoadProps(Sub(props, "RoadProps"), flora, dec, sBlock);
            RoadEndCover(Sub(props, "EndCover"), flora, sBlock);
            TreeBelt(Sub(props, "Belt"));
            RuinFoot(ruined, flora);
            MoveMilestone(ruined);
            ShrineFlowers(ruined, Sub(props, "ShrineGifts"), flora);
            RebuiltBarrier(rebuilt, sBlock);
            HillEdgeBuild(Sub(props, "HillEdge"), Sub(flora, "HillEdge"), Sub(dec, "HillEdge"));
            Debug.Log("SC HOLLOWMERE roadside: " + string.Join(", ", counts.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)) + ", decals added=" + (decals - d0) + " (area total " + decals + " + the earlier builders')");
        }

        // ------------------------------------------------------------------------------------------------ hedges: scanned shrubs, no cards
        static void RoadHedges(Transform parent)
        {
            // TR05-TR07 read as bare saplings (R13 round 1), so the hedge is two staggered rows of the scanned wild bushes: rose hip (T21j), the broad herb mounds (T21m, T19x)
            // with a TR06 behind for height. Heights 1.5-1.9 m, overlapping into one body.
            string[] codes = { "T21j", "T21m", "T19x", "T21j", "T21m", "T21j" };
            foreach (RoadItem i in SaltwakeRoadPlan.Create(T))
            {
                if (i.Kind != RoadItemKind.Hedge || (i.Presets & RoadPresets.Ruined) == 0) continue;
                Vector2 c = new Vector2(i.X, i.Z), axis = AxisOfYaw(i.Yaw), perp = RightOf(axis);
                for (int row = 0; row < 2; row++)
                    for (int k = 0; k < 3; k++)
                    {
                        int key = Mathf.RoundToInt(i.X * 3f) * 131 + Mathf.RoundToInt(i.Z * 7f) + (row * 3 + k) * 17;
                        Vector2 p = c + axis * ((k - 1) * 1.45f + (row == 1 ? 0.7f : 0f) + (H(key, 1, 1600) - 0.5f) * 0.4f) + perp * ((row == 0 ? -0.45f : 0.45f) + (H(key, 2, 1600) - 0.5f) * 0.3f);
                        if (eyeCams.Any(e => Vector2.Distance(e, p) < RsEyeClear)) continue;
                        string code = codes[(int)(H(key, 3, 1600) * codes.Length) % codes.Length];
                        if (row == 1 && k == 1) code = "TR06";
                        GameObject pf = Prefab(code);
                        if (pf == null) { Count("missing_" + code); continue; }
                        Bounds ub = Unit(pf);
                        float h = (code == "TR06" ? Mathf.Lerp(2.0f, 2.5f, H(key, 4, 1600)) : Mathf.Lerp(1.5f, 1.9f, H(key, 4, 1600))) * (i.Scale / 1.2f), s = h / Mathf.Max(0.1f, ub.size.y);
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
                        go.name = "HedgeBush_" + Count2();
                        go.transform.localScale = new Vector3(s * 1.1f, s, s * 1.1f);
                        go.transform.rotation = Quaternion.Euler((H(key, 6, 1600) - 0.5f) * 4f, H(key, 7, 1600) * 360f, (H(key, 8, 1600) - 0.5f) * 4f);
                        go.transform.position = new Vector3(p.x, TY(p) - 0.03f * h, p.y);
                        foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                        Count("hedgeBushes");
                    }
            }
        }
        static int rsSeq; static int Count2() => rsSeq++;

        // ------------------------------------------------------------------------------------------------ the boundary
        static void Block(Transform g, Transform flora, Transform dec, float sBlock)
        {
            rsSeq = 0;
            Vector2 dir; Vector2 B = RoadAt(sBlock, out dir); Vector2 north = RightOf(dir);
            float by = TY(B);
            // the trunk across the road, root end on the north verge
            GameObject tpf = Prefab("T18d");
            float rs = tpf != null ? RsRootSign(tpf) : 1f;
            Vector2 tc = B + north * 0.35f;
            Put(g, "T18d", "BlockTrunk", tc, by, 7.7f, YawFace(north * rs) + 9f, 0f, 0f, 0.3f, true);   // T10b: 7.7 m (was 5.86), sunk 0.3 m: the thin log read as passable
            // T10b: the mossy tree behind it, across the road the other way (crown on the south verge)
            Vector2 mp = B + dir * 2.3f + north * 0.2f;
            Put(g, "T20n", "BlockMossyTree", mp, TY(mp), 8.4f, YawAlong(north) - 14f, 0f, 0f, 0.22f, true);
            // the wagon on the east side, tipped on its broken axle across the south half of the road (T10b: 7 m east of the trunk, 2.6 m closer, so the diagnostic and eye cameras see it with the trunk)
            Vector2 W = RoadAt(sBlock - 7.0f, out Vector2 wd) - RightOf(wd) * 2.6f;   // on the south edge: the lane line (node 6 at 5.5 m east of the trunk) stays clear
            float wyaw = YawFace(wd) + 12f;
            Vector2 u = new Vector2(Mathf.Sin(wyaw * Mathf.Deg2Rad), Mathf.Cos(wyaw * Mathf.Deg2Rad)), v = RightOf(u);
            Put(g, "T16a", "BlockWagon", W, TY(W), 3.1f, wyaw, 5f, -15f, 0.3f, true);
            Vector2 At(float a, float b) => W + u * a + v * b;
            Put(g, "T20w", "SpillSack_0", At(-0.7f, 1.35f), TY(At(-0.7f, 1.35f)), 0.6f, 40f, 0f, 82f, 0.02f);
            Put(g, "T20w", "SpillSack_1", At(0.5f, 1.75f), TY(At(0.5f, 1.75f)), 0.6f, 160f, 0f, 20f, 0.02f);
            Put(g, "T20w", "SpillSack_2", At(1.5f, 1.2f), TY(At(1.5f, 1.2f)), 0.55f, 250f, 0f, 70f, 0.02f);
            Put(g, "T20t", "SpillBarrel", At(2.4f, 0.95f), TY(At(2.4f, 0.95f)), 0.8f, wyaw + 60f, 88f, 0f, 0.02f, true);
            Put(g, "T17a", "SpillCrate_0", At(-2.0f, 0.8f), TY(At(-2.0f, 0.8f)), 0.58f, wyaw + 30f, 0f, 76f, 0.02f);
            Put(g, "T17b", "SpillCrate_1", At(-2.3f, 1.5f), TY(At(-2.3f, 1.5f)), 0.6f, wyaw - 50f, 8f, 4f, 0.02f);
            Put(g, "T08g", "SpillBasket", At(1.0f, 2.1f), TY(At(1.0f, 2.1f)), 0.5f, wyaw + 120f, 72f, 0f, 0.03f);
            Decal(dec, "Hay", At(0.4f, 1.5f), 3.0f, 2.4f, wyaw + 20f, "Hay_Spill_0");
            Decal(dec, "Hay", At(-1.8f, 1.2f), 2.2f, 2.0f, wyaw + 110f, "Hay_Spill_1");
            Decal(dec, "Mud", At(2.0f, 0.6f), 2.6f, 1.3f, wyaw, "Mud_Wagon");
            Decal(dec, "Grime", tc, 3.0f, 2.2f, YawAlong(dir), "Grime_Trunk");
            // logs and branches piled behind the trunk, a mossy windthrow beyond the north hedge, mushrooms at the trunk's foot
            // T10b: a log across each verge end, a third on the south side, and a band of branches over the trunks, so the blockage runs verge to verge
            Vector2 lp = B + dir * 1.0f + north * (rHalf + 1.0f);
            Put(g, "T20r", "BlockLog", lp, TY(lp), 3.6f, YawAlong(north) + 25f, 0f, 0f, 0.1f, true);
            Vector2 lp2 = B + dir * 1.0f - north * (rHalf + 1.1f);
            Put(g, "T18e", "BlockLog2", lp2, TY(lp2), 2.6f, YawAlong(north) - 18f, 0f, 0f, 0.05f, true);
            Vector2 lp3 = B + dir * 3.4f - north * (rHalf - 0.2f);
            Put(g, "T20r", "BlockLog3", lp3, TY(lp3), 3.2f, YawAlong(north) + 80f, 0f, 0f, 0.1f, true);
            string[] br = { "T20o", "T20p", "T20q", "T20q", "T20o", "T20p", "T20q", "T20o", "T20q", "T20p", "T20q", "T20o", "T20p", "T20q" };
            for (int i = 0; i < br.Length; i++)
            {
                Vector2 p = tc + dir * (-1.4f + 4.8f * H(i, 1, 1610)) + north * (-4.4f + 8.8f * H(i, 2, 1610));
                Put(g, br[i], "BlockBranch_" + i, p, TY(p), 1.2f, H(i, 3, 1610) * 360f, 0f, 0f, 0.05f);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = tc - dir * 1.15f + north * (-1.4f + i * 1.3f) + new Vector2(0.1f, 0.1f) * H(i, 4, 1610);
                PutNat(flora, i % 2 == 0 ? "T20j" : "T20k", "BlockMushroom_" + i, p, TY(p), 1f, H(i, 5, 1610) * 360f, 0f, 0f, 0.02f);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 p = tc + dir * (1.2f + 1.5f * H(i, 6, 1610)) + north * (-2.4f + 5.6f * H(i, 7, 1610));
                WeedT(flora, p, 1611, i, 1.0f, 1.8f);
            }
            HedgeSpan(Sub(g, "EndHedge"), sBlock - 13f, sBlock + 5f);   // T10b: the hedges run on up to the blockage on both sides
            // the wall behind: invisible, across the whole terrain, a child of EdgeBounds so the traversal scan reads it as a world bound
            Transform eb = null;
            foreach (Transform tr in setRoot.GetComponentsInChildren<Transform>(true)) if (tr.name == "EdgeBounds") { eb = tr; break; }
            var wall = new GameObject("Road_Block"); wall.transform.SetParent(eb != null ? eb : setRoot, false);
            wall.transform.position = new Vector3(RoadBlockWallX, by, (SaltwakeRoadPlan.TerrainMinZ + SaltwakeRoadPlan.TerrainMaxZ) * 0.5f);
            var bc = wall.AddComponent<BoxCollider>(); bc.size = new Vector3(1f, 80f, SaltwakeRoadPlan.TerrainMaxZ - SaltwakeRoadPlan.TerrainMinZ + 8f);
            Physics.SyncTransforms();
            Debug.Log("SC HOLLOWMERE roadside block: centre " + B + " ground y " + by.ToString("0.00") + " trunk root sign " + rs + " wagon " + W + " wall x " + RoadBlockWallX);
        }

        // Hedge bushes (the RoadHedges recipe) along both verges from s0 to s1: the hedges end at the blockage, not 10 m short of it.
        static void HedgeSpan(Transform parent, float s0, float s1)
        {
            string[] codes = { "T21j", "T21m", "T19x", "T21j", "T21m", "TR06" };
            int n = 0;
            for (float s = s0; s < s1; s += 1.9f, n++)
                for (int side = -1; side <= 1; side += 2)
                {
                    int key = 3000 + n * 7 + (side > 0 ? 1 : 0);
                    Vector2 c = RoadAt(s, out Vector2 d); Vector2 right = RightOf(d);
                    Vector2 p = c + right * (side * (rHalf + 1.15f + (H(key, 2, 1900) - 0.5f) * 0.5f)) + d * ((H(key, 1, 1900) - 0.5f) * 0.6f);
                    if (!RsFree(p, 0.2f)) continue;
                    string code = codes[(int)(H(key, 3, 1900) * codes.Length) % codes.Length];
                    GameObject pf = Prefab(code); if (pf == null) { Count("missing_" + code); continue; }
                    Bounds ub = Unit(pf);
                    float h = code == "TR06" ? Mathf.Lerp(2.0f, 2.5f, H(key, 4, 1900)) : Mathf.Lerp(1.5f, 1.95f, H(key, 4, 1900)), sc = h / Mathf.Max(0.1f, ub.size.y);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
                    go.name = "EndHedgeBush_" + Count2();
                    go.transform.localScale = new Vector3(sc * 1.1f, sc, sc * 1.1f);
                    go.transform.rotation = Quaternion.Euler((H(key, 6, 1900) - 0.5f) * 4f, H(key, 7, 1900) * 360f, (H(key, 8, 1900) - 0.5f) * 4f);
                    go.transform.position = new Vector3(p.x, TY(p) - 0.03f * h, p.y);
                    foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                    Count("endHedgeBushes");
                }
        }

        // T10b: past the blockage the world must read as country, not the backdrop ring's hazed ground (the "grey plane": SC Backdrop GroundHaze is 0.88 at the terrain's edge and the
        // ring lies below the eye there): a belt of real trees over a hedge of shrubs behind the wall, north to south field, crossing the dead road beyond the blockage.
        static void TreeBelt(Transform g)
        {
            string[] shrub = { "TR05", "TR06", "TR07", "T21m", "T21j", "TR06", "T19x" };
            string[] trees = { "TR02", "TR03", "TR01", "TR02", "TR04", "TR03", "TR04", "TR01" };
            const float zLo = -142f, zHi = -58f;
            int n = 0;
            for (float z = zLo; z <= zHi; z += 2.6f, n++)
            {
                Vector2 p = new Vector2(-127.9f + (H(n, 1, 2000) - 0.5f) * 1.1f, z + (H(n, 2, 2000) - 0.5f) * 1.0f);
                BeltPut(g, shrub[(int)(H(n, 3, 2000) * shrub.Length) % shrub.Length], "BeltShrub_" + n, p, Mathf.Lerp(1.8f, 2.8f, H(n, 4, 2000)), H(n, 5, 2000) * 360f);
            }
            for (int row = 0; row < 4; row++)
            {
                n = 0;
                for (float z = zLo + (row % 2) * 2.1f; z <= zHi; z += 4.2f, n++)
                {
                    int key = row * 1000 + n;
                    Vector2 p = new Vector2(-130.4f - row * 3.9f + (H(key, 1, 2100) - 0.5f) * 2.4f, z + (H(key, 2, 2100) - 0.5f) * 2.4f);
                    if (p.x < SaltwakeRoadPlan.TerrainMinX + 3f) continue;
                    string code = trees[(int)(H(key, 3, 2100) * trees.Length) % trees.Length];
                    float h = Mathf.Lerp(9f, 15f, H(key, 4, 2100)) * (row == 0 ? 0.85f : 1f);
                    BeltPut(g, code, "BeltTree_" + row + "_" + n, p, h, H(key, 5, 2100) * 360f);
                }
            }
            Count("beltObjects", g.childCount);
        }

        static void BeltPut(Transform parent, string code, string name, Vector2 p, float h, float yaw)
        {
            GameObject pf = Prefab(code); if (pf == null) { Count("missing_" + code); return; }
            Bounds ub = Unit(pf);
            float sc = h / Mathf.Max(0.1f, ub.size.y);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.name = name;
            go.transform.localScale = new Vector3(sc * 1.05f, sc, sc * 1.05f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = new Vector3(p.x, TY(p) - 0.04f * h, p.y);
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        }

        // T10b: ground cover around the road end: scanned plants, fine tufts and a few stones over the field's bare dirt (the terrain's soil also greens over: RoadEndGrass), off the road.
        static void RoadEndCover(Transform g, Transform flora, float sBlock)
        {
            string[] low = { "T21d", "T21e", "T21f", "T21j", "T21n", "T21o", "T21i", "T21g", "T21d", "T21m" };
            string[] stones = { "T20c", "T20d", "T20e", "T20g", "T20f" };
            Vector2 centre = new Vector2(-119f, -97f);
            int placed = 0, st = 0;
            for (int ix = -10; ix <= 10; ix++)
                for (int iz = -10; iz <= 10; iz++)
                {
                    int key = 4000 + (ix + 10) * 21 + (iz + 10);
                    Vector2 p = centre + new Vector2((ix + 0.15f + 0.7f * H(key, 1, 2200)) * 2.0f, (iz + 0.15f + 0.7f * H(key, 2, 2200)) * 2.0f);
                    if ((p - centre).magnitude > 16f || p.x < RoadBlockWallX + 1f) continue;
                    if (HollowmereLayout.DistanceToPolyline(new TownPoint(p.x, p.y), rp) < rHalf - 0.1f) continue;
                    if (!RsFree(p, 0.15f)) continue;
                    float r = H(key, 3, 2200);
                    if (r < 0.52f) PutNat(flora, low[(int)(H(key, 4, 2200) * low.Length) % low.Length], "EndPlant_" + placed++, p, TY(p), Mathf.Lerp(0.8f, 1.35f, H(key, 5, 2200)), H(key, 6, 2200) * 360f, 0f, 0f, 0.02f);
                    else if (r < 0.80f) WeedT(flora, p, 2201, key, 0.9f, 1.4f);
                    else if (r < 0.88f) Put(g, stones[(int)(H(key, 7, 2200) * stones.Length) % stones.Length], "EndStone_" + st++, p, TY(p), Mathf.Lerp(0.3f, 0.65f, H(key, 8, 2200)), H(key, 9, 2200) * 360f, 3f, -4f, 0.08f);
                }
            Count("endCover", placed + st);
        }

        // T10b RoadOut foreground: the lot's grass-strip row at the R3_02 ruin's wall foot (the bright spiky tufts) becomes irregular clumps of tufts and scanned plants; spill pieces
        // (the pale plank) the ruin dressing dropped within 5.5 m of an eye camera go.
        static void RuinFoot(Transform ruined, Transform flora)
        {
            int dropped = 0;
            foreach (Transform tr in ruined.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (tr == null || !tr.name.StartsWith("Dress_Spill_", System.StringComparison.Ordinal)) continue;
                Vector2 q = new Vector2(tr.position.x, tr.position.z);
                if (eyeCams.Any(c => Vector2.Distance(c, q) < 5.5f)) { Object.DestroyImmediate(tr.gameObject); dropped++; }
            }
            Count("spillsDropped", dropped);
            Transform lot = ruined.Find("Foliage/Edges/R3_02");
            if (lot == null) { Count("missing_R3_02_edges"); return; }
            string[] pl = { "T21d", "T21e", "T21j", "T21f", "T21n", "T21m", "T21d" };
            var strips = new List<Transform>();
            foreach (Transform c in lot) if (c.name.StartsWith("Grass_Strip", System.StringComparison.Ordinal)) strips.Add(c);
            int i = 0, clumps = 0;
            foreach (Transform sTr in strips)
            {
                Vector2 p = new Vector2(sTr.position.x, sTr.position.z); int k0 = i++;
                Object.DestroyImmediate(sTr.gameObject);
                if (k0 % 2 != 1) continue;
                for (int k = 0; k < 4; k++)
                {
                    float a = H(k0, k, 2300) * 6.283f, rr = 0.15f + 0.8f * H(k0, k + 5, 2300);
                    Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    if (eyeCams.Any(c => Vector2.Distance(c, q) < RsEyeClear) || !HollowmereStreets.FreeOf(q, TY(q), 0.05f)) continue;
                    if (k < 2) PutNat(flora, pl[(int)(H(k0, 20, 2300) * pl.Length) % pl.Length], "RuinFootPlant_" + clumps + "_" + k, q, TY(q), Mathf.Lerp(0.7f, 1.1f, H(k0, 21, 2300)), H(k0, 22, 2300) * 360f, 0f, 0f, 0.02f);
                    else WeedT(flora, q, 2301, k0 * 5 + k, 0.9f, 1.5f);
                }
                clumps++;
            }
            Count("ruinFootClumps", clumps);
        }

        // The Rebuilt preset has no fallen tree: three rails across the road in front of the same wall.
        static void RebuiltBarrier(Transform rebuilt, float sBlock)
        {
            Vector2 B = RoadAt(sBlock, out Vector2 dir); Vector2 north = RightOf(dir);
            Transform rb = Sub(Sub(rebuilt, "Road"), "RoadBlock");
            for (int k = 0; k < 3; k++)
            {
                Vector2 p = B + north * ((k - 1) * 3f);
                GameObject rail = HollowmereRoad.Prop("Fence_Rail", rb);
                if (rail == null) break;
                rail.name = "RoadBlock_Rail" + k;
                rail.transform.SetPositionAndRotation(new Vector3(p.x, TY(p), p.y), Quaternion.Euler(0f, YawFace(dir), 0f));
                var bc = rail.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.55f, 0f); bc.size = new Vector3(3f, 1.1f, 0.25f);
                Count("rebuiltRails");
            }
        }

        // ------------------------------------------------------------------------------------------------ the verge, ruts, puddles
        static void Verge(Transform g, Transform flora, Transform dec, float sBlock)
        {
            string[] bloom = { "T21d", "T21d", "T21j", "T21e", "T21f", "T21g", "T21i", "T21n", "T21o", "T19x", "T21m", "T21d" };
            string[] tall = { "T21h" };   // T10b: the dark tall stalks (T19u, T19y) read as black weeds in the RoadOut foreground
            int placed = 0;
            for (float s = 12f; s < sBlock - 2.5f; s += 0.9f)
            {
                int si = Mathf.RoundToInt(s * 10f);
                Vector2 c = RoadAt(s, out Vector2 d); Vector2 right = RightOf(d);
                for (int side = -1; side <= 1; side += 2)
                {
                    int key = si * 2 + (side > 0 ? 1 : 0);
                    // strip A between the road's edge and the hedge or fence line, strip B past it on the field side
                    for (int k = 0; k < 2; k++)
                    {
                        if (H(key, k, 1620) > (k == 0 ? 0.85f : 0.7f)) continue;
                        int cnt = k == 0 ? 1 + (H(key, 5, 1620) < 0.5f ? 1 : 0) : 2;
                        for (int m = 0; m < cnt; m++)
                        {
                            float off = k == 0 ? rHalf + 0.15f + 0.65f * H(key, 10 + k + m * 7, 1620) : rHalf + 3.3f + 4.2f * H(key, 12 + k + m * 7, 1620);
                            Vector2 p = c + right * (side * off) + d * ((H(key, 14 + k + m * 7, 1620) - 0.5f) * 1.2f);
                            if (!RsFree(p, 0.15f)) continue;
                            string code = H(key, 16 + k + m * 7, 1620) < 0.2f ? tall[(int)(H(key, 18 + m, 1620) * tall.Length) % tall.Length] : bloom[(int)(H(key, 20 + k + m * 7, 1620) * bloom.Length) % bloom.Length];
                            PutNat(flora, code, "VergePlant_" + placed++, p, TY(p), Mathf.Lerp(0.8f, 1.4f, H(key, 22 + k + m * 7, 1620)), H(key, 24 + k + m * 7, 1620) * 360f, 0f, 0f, 0.02f);
                        }
                    }
                    if (H(key, 40, 1620) < 0.5f)
                    {
                        Vector2 p = c + right * (side * (rHalf + 0.15f + 0.7f * H(key, 41, 1620)));
                        if (RsFree(p, 0.05f)) WeedT(flora, p, 1621, key, 1.0f, 1.7f);
                    }
                }
            }
            Count("versePlants", placed);
            // ruts: overlapping soft damp strips either side of the crown (the terrain already paints the furrows; these darken them), puddles in the dips, leaf drift at the hedge foot
            int ri = 0;
            foreach (int side in new[] { -1, 1 })
                for (float s = 14f; s < sBlock - 3f; s += 5.5f, ri++)
                {
                    Vector2 c = RoadAt(s, out Vector2 d); Vector2 right = RightOf(d);
                    Decal(dec, "Damp", c + right * (side * 0.85f), 8.0f, 0.8f, YawAlong(d) + (H(ri, 3, 1630) - 0.5f) * 2f, "Rut_" + ri);
                }
            foreach (UnityEngine.Rendering.Universal.DecalProjector dp in dec.GetComponentsInChildren<UnityEngine.Rendering.Universal.DecalProjector>(true))
                if (dp.name.StartsWith("Rut_", System.StringComparison.Ordinal)) dp.fadeFactor = 0.55f;
            ri = 0;
            for (float s = 42f; s < sBlock - 4f; s += 10.5f, ri++)
            {
                Vector2 c = RoadAt(s, out Vector2 d); Vector2 right = RightOf(d);
                float yaw = YawAlong(d);
                if (H(ri, 8, 1630) < 0.55f) Decal(dec, "Puddle", c + right * ((H(ri, 9, 1630) < 0.5f ? -1f : 1f) * 0.85f) + d * 2f, 2.4f, 0.9f, yaw, "RutPuddle_" + ri);
                if (ri % 2 == 0) Decal(dec, ri % 4 == 0 ? "LeavesA" : "LeavesB", c + right * ((ri % 4 == 0 ? 1f : -1f) * (rHalf + 0.9f)), 4.0f, 2.2f, yaw, "HedgeLeaves_" + ri);
            }
        }

        // about two props per 10 m on the verge: a dropped sack, a stone heap, a gate post, a branch, a stone on the road's edge
        static void RoadProps(Transform g, Transform flora, Transform dec, float sBlock)
        {
            string[] heap = { "T20g", "T20e", "T20d", "T20c" };
            int n = 0;
            for (float s = 16f; s < sBlock - 3f; s += 5.0f, n++)
            {
                int side = H(n, 1, 1640) < 0.5f ? -1 : 1;
                Vector2 c = RoadAt(s + (H(n, 2, 1640) - 0.5f) * 2.6f, out Vector2 d); Vector2 right = RightOf(d);
                Vector2 p = c + right * (side * (rHalf + 0.45f + 0.8f * H(n, 3, 1640)));
                if (!RsFree(p, 0.25f)) continue;
                float yaw = H(n, 4, 1640) * 360f; int kind = (int)(H(n, 5, 1640) * 5f) % 5;
                float y = TY(p);
                switch (kind)
                {
                    case 0: Put(g, "T20w", "DroppedSack_" + n, p, y, 0.6f, yaw, 0f, 70f + 20f * H(n, 6, 1640), 0.02f); Decal(dec, "Hay", p, 1.4f, 1.2f, yaw, "Hay_Sack_" + n); break;
                    case 1:
                        for (int k = 0; k < 3; k++) { Vector2 q = p + new Vector2(H(n, 7 + k, 1640) - 0.5f, H(n, 10 + k, 1640) - 0.5f) * 1.1f; Put(g, heap[(n + k) % heap.Length], "StoneHeap_" + n + "_" + k, q, TY(q), Mathf.Lerp(0.35f, 0.7f, H(n, 13 + k, 1640)), H(n, 16 + k, 1640) * 360f, 3f, -4f, 0.08f); }
                        PutNat(flora, "N03", "Scree_" + n, p, y, 1f, yaw, 0f, 0f, 0.05f); break;
                    case 2: Put(g, "T19i", "GatePost_" + n, p, y, 1.05f, YawFace(-right * side) + (H(n, 19, 1640) - 0.5f) * 14f, (H(n, 20, 1640) - 0.5f) * 8f, (H(n, 21, 1640) - 0.5f) * 8f, 0.12f); break;
                    case 3: Put(g, "T20" + (n % 3 == 0 ? "o" : n % 3 == 1 ? "p" : "q"), "RoadBranch_" + n, p, y, 1f, yaw, 0f, 0f, 0.04f); break;
                    default: Put(g, "T18e", "RoadLog_" + n, p, y, 1.46f, YawAlong(d) + (H(n, 22, 1640) - 0.5f) * 40f, 0f, 0f, 0.03f); break;
                }
                Count("roadProps");
            }
        }

        // The first milestone stood on the north verge behind the ruin in the RoadOut frame: it moves to the south verge, facing the road, against the field.
        static void MoveMilestone(Transform ruined)
        {
            Transform road = ruined.Find("Road");
            if (road == null) return;
            foreach (Transform m in road)
            {
                if (m.name != "Milestone_A" || m.position.x > -60f || m.position.x < -80f) continue;
                Vector2 q = new Vector2(m.position.x, m.position.z); float best = float.MaxValue, bs = 0f;
                for (float s = 30f; s < 120f; s += 0.25f) { Vector2 c0 = RoadAt(s, out _); float dd = Vector2.Distance(c0, q); if (dd < best) { best = dd; bs = s; } }
                Vector2 c = RoadAt(bs, out Vector2 d); Vector2 south = -RightOf(d), np = c + south * (rHalf + 0.9f);
                m.position = new Vector3(np.x, TY(np) - 0.05f, np.y);
                m.rotation = Quaternion.Euler(0f, YawFace(-south) + 6f, 0f) * Quaternion.Euler(-9f, 0f, 0f);
                Count("milestoneMoved");
                break;
            }
        }

        // flowers and a cup left at the wayside shrine, on its front (local +Z faces the road)
        static void ShrineFlowers(Transform ruined, Transform g, Transform flora)
        {
            Transform shrine = ruined.Find("Road/Shrine_Ruined");
            if (shrine == null) { Count("missing_shrine"); return; }
            Vector2 f = new Vector2(shrine.forward.x, shrine.forward.z).normalized, r = RightOf(f), c = new Vector2(shrine.position.x, shrine.position.z);
            string[] fl = { "T21d", "T21d", "T21j", "T21d", "T21e", "T21d", "T21n", "T21d", "T21j" };
            for (int i = 0; i < fl.Length; i++)
            {
                Vector2 p = c + f * (0.95f + 0.45f * H(i, 1, 1650)) + r * (-0.9f + 1.8f * (i + 0.5f * H(i, 2, 1650)) / fl.Length);
                PutNat(flora, fl[i], "ShrineFlower_" + i, p, TY(p), Mathf.Lerp(0.7f, 1.05f, H(i, 3, 1650)), H(i, 4, 1650) * 360f, 0f, 0f, 0.01f);
            }
            Vector2 cup = c + f * 0.8f + r * 0.25f;
            Put(g, "C06d", "ShrineCup", cup, TY(cup), 0.22f, 20f, 0f, 0f, 0.02f);
            Vector2 sk = c + f * 1.25f - r * 0.35f;
            Put(g, "T21c", "ShrineRope", sk, TY(sk), 0.5f, 60f, 0f, 0f, 0.02f);
            Count("shrineGifts", fl.Length + 2);
        }

        // ------------------------------------------------------------------------------------------------ D11 the hill edge
        static bool RsHillOk(Vector2 p, float rad) => RsFree(p, rad) && HollowmereStreets.PathDistance(p) > 1.2f;

        static void HillEdgeBuild(Transform g, Transform flora, Transform dec)
        {
            string[] rocks = { "T20a", "T20b", "T20c", "T20d", "T20e", "T20f", "T20g", "T20d" };
            string[] ferns = { "T12b", "T12b", "T21l", "T12b", "T21f" };
            string[] ground = { "T21e", "T21f", "T21j", "T21n", "T21o", "T21i", "T19x", "T21m", "T21d" };
            foreach (int side in new[] { -1, 1 })
            {
                int key = 1700 + (side + 1) * 11;
                int placed = 0;
                // floor scatter on a jittered 3 m grid
                for (int ix = 0; ix < 10; ix++)
                    for (int iz = 0; iz < 12; iz++)
                    {
                        float x = side * (30.5f + (ix + 0.15f + 0.7f * H(ix, iz, key)) * 3f), z = 27f + (iz + 0.15f + 0.7f * H(ix, iz, key + 1)) * 3f;
                        Vector2 p = new Vector2(x, z);
                        if (z < HollowmereLayout.HillFoot(x) - 2.5f || !RsHillOk(p, 0.25f)) continue;
                        float r = H(ix, iz, key + 2); float y = TY(p);
                        if (r < 0.46f) PutNat(flora, ferns[(int)(H(ix, iz, key + 3) * ferns.Length) % ferns.Length], "Fern_" + placed++, p, y, Mathf.Lerp(0.8f, 1.45f, H(ix, iz, key + 4)), H(ix, iz, key + 5) * 360f, 0f, 0f, 0.04f);
                        else if (r < 0.60f)
                        {
                            PutSlopeDim(g, rocks[(int)(H(ix, iz, key + 6) * rocks.Length) % rocks.Length], "SunkRock_" + placed++, p, Mathf.Lerp(0.5f, 1.7f, H(ix, iz, key + 7) * H(ix, iz, key + 8) + 0.1f), H(ix, iz, key + 9) * 360f, (H(ix, iz, key + 10) - 0.5f) * 12f, (H(ix, iz, key + 11) - 0.5f) * 12f, Mathf.Lerp(0.3f, 0.5f, H(ix, iz, key + 12)));
                            Vector2 q = p + new Vector2(0.7f, 0.5f) * (H(ix, iz, key + 13) < 0.5f ? 1f : -1f);
                            if (RsHillOk(q, 0.1f)) PutNat(flora, "T12b", "RockFern_" + placed++, q, TY(q), 0.9f, H(ix, iz, key + 14) * 360f, 0f, 0f, 0.04f);
                        }
                        else if (r < 0.70f) PutNat(flora, ground[(int)(H(ix, iz, key + 15) * ground.Length) % ground.Length], "Plant_" + placed++, p, y, Mathf.Lerp(0.8f, 1.4f, H(ix, iz, key + 16)), H(ix, iz, key + 17) * 360f, 0f, 0f, 0.02f);
                        else if (r < 0.80f) WeedT(flora, p, key + 18, ix * 20 + iz, 1.0f, 1.8f);
                    }
                Count("hillFloor", placed);
                // a log, stump or branch about every 12 m, lying along the contour; mushrooms at its foot
                int li = 0;
                for (int ix = 0; ix < 3; ix++)
                    for (int iz = 0; iz < 3; iz++)
                    {
                        float x = side * (31f + ix * 12f + 8f * H(ix, iz, key + 20)), z = 29f + iz * 12f + 8f * H(ix, iz, key + 21);
                        Vector2 p = new Vector2(x, z);
                        if (z < HollowmereLayout.HillFoot(x) + 0.5f || !RsHillOk(p, 1.0f)) continue;
                        Vector3 nrm = TN(p);
                        if (nrm.y < 0.82f) { p += new Vector2(0f, 3f); if (!RsHillOk(p, 1.0f) || TN(p).y < 0.82f) continue; nrm = TN(p); }
                        float yaw = YawAlong(Perp(new Vector2(nrm.x, nrm.z).normalized)) + (H(ix, iz, key + 22) - 0.5f) * 30f, y = TY(p);
                        float roll = H(ix, iz, key + 23);
                        if (roll < 0.34f) Put(g, "T20r", "HillLog_" + li, p, y, Mathf.Lerp(2.4f, 3.6f, H(ix, iz, key + 24)), yaw, 0f, 0f, 0.1f, true);
                        else if (roll < 0.52f) Put(g, "N05", "HillLogN_" + li, p, y, Mathf.Lerp(2.4f, 3.2f, H(ix, iz, key + 25)), yaw, 0f, 0f, 0.08f, true);
                        else if (roll < 0.74f) Put(g, "N04", "HillStump_" + li, p, y, Mathf.Lerp(1.0f, 1.45f, H(ix, iz, key + 26)), yaw, 0f, 0f, 0.08f, true);
                        else Put(g, "T18e", "HillLogS_" + li, p, y, 1.46f, yaw, 0f, 0f, 0.05f);
                        for (int k = 0; k < 3; k++)
                        {
                            Vector2 q = p + new Vector2(Mathf.Cos(k * 2.1f + 0.5f), Mathf.Sin(k * 2.1f + 0.5f)) * (0.9f + 0.4f * H(li, k, key + 27));
                            if (RsHillOk(q, 0.08f)) PutNat(flora, k == 0 ? "T20j" : k == 1 ? "T20k" : "T20l", "LogMushroom_" + li + "_" + k, q, TY(q), 1f, H(li, k, key + 28) * 360f, 0f, 0f, 0.02f);
                        }
                        Vector2 b = p + new Vector2(H(li, 5, key + 29) - 0.5f, H(li, 6, key + 29) - 0.5f) * 4f;
                        if (RsHillOk(b, 0.3f)) Put(g, li % 3 == 0 ? "T20o" : li % 3 == 1 ? "T20p" : "T20q", "HillBranch_" + li, b, TY(b), 1f, H(li, 7, key + 29) * 360f, 0f, 0f, 0.05f);
                        li++;
                    }
                Count("hillLogs", li);
                // the mossy fallen tree, one hero per flank, lying along the contour
                Vector2 hp = new Vector2(side * 46.5f, 42.5f);
                if (RsHillOk(hp, 1.4f))
                {
                    Vector3 hn = TN(hp);
                    Put(g, "T20n", "HillMossyTree", hp, TY(hp), 7.4f, YawAlong(Perp(new Vector2(hn.x, hn.z).normalized)) + 10f, 0f, 0f, 0.15f, true);
                }
                // moss, leaf drift and damp on the floor in a few big decals
                for (int i = 0; i < 5; i++)
                {
                    Vector2 p = new Vector2(side * Mathf.Lerp(33f, 56f, H(i, 1, key + 40)), Mathf.Lerp(31f, 58f, H(i, 2, key + 40)));
                    DecalT(dec, i % 3 == 0 ? "Moss" : i % 3 == 1 ? "LeavesA" : "Damp", p, 4.2f, 3.6f, H(i, 3, key + 40) * 360f, "HillDecal_" + i);
                }
            }
            // the old boundary stone at the west foot, standing, with moss and a few flowers left at its foot
            Vector2 st = new Vector2(-40.6f, 27.1f);
            if (RsFree(st, 0.5f))
            {
                Put(g, "N02", "BoundaryStone", st, TY(st), 1.95f, 28f, 90f, 4f, 0.28f, true);
                DecalT(dec, "Moss", st, 2.4f, 2.4f, 40f, "BoundaryStone_Moss");
                for (int i = 0; i < 5; i++)
                {
                    Vector2 q = st + new Vector2(Mathf.Cos(i * 1.3f + 0.6f), Mathf.Sin(i * 1.3f + 0.6f)) * (0.7f + 0.3f * H(i, 1, 1730));
                    if (RsFree(q, 0.08f)) PutNat(flora, i % 2 == 0 ? "T21d" : "T21j", "StoneFlower_" + i, q, TY(q), Mathf.Lerp(0.7f, 1.0f, H(i, 2, 1730)), H(i, 3, 1730) * 360f, 0f, 0f, 0.01f);
                }
                for (int i = 0; i < 3; i++) { Vector2 q = st + new Vector2(-0.9f + 0.9f * i, -0.9f - 0.2f * H(i, 4, 1730)); if (RsFree(q, 0.2f)) Put(g, "T20g", "StoneRubble_" + i, q, TY(q), 0.4f, H(i, 5, 1730) * 360f, 3f, -4f, 0.08f); }
            }
            // the worn path up the west slope that goes nowhere: it ends at an old stump and a chopped log
            Vector2[] wp = { new Vector2(-43.5f, 27.4f), new Vector2(-46.5f, 32.5f), new Vector2(-49.5f, 38.5f), new Vector2(-49.0f, 45.0f), new Vector2(-52.5f, 50.5f) };
            int pi = 0;
            for (int i = 0; i + 1 < wp.Length; i++)
            {
                Vector2 a = wp[i], b = wp[i + 1]; Vector2 d = (b - a).normalized; float len = Vector2.Distance(a, b);
                for (float s = 1.4f; s < len; s += 2.9f, pi++) DecalT(dec, "Mud", a + d * s, 3.6f, 1.5f, YawAlong(d), "WornPath_" + pi);
                if (i == 1 || i == 2) { Vector2 q = (a + b) * 0.5f + Perp(d) * 1.4f; if (RsHillOk(q, 0.3f)) PutSlopeDim(g, "T20e", "PathStone_" + i, q, 0.9f, H(i, 1, 1740) * 360f, 0f, 0f, 0.4f); }
            }
            Vector2 end = wp[wp.Length - 1] + new Vector2(-0.5f, 1.2f);
            if (RsFree(end, 0.6f)) { Put(g, "N04", "PathStump", end, TY(end), 1.3f, 40f, 0f, 0f, 0.08f, true); Vector2 ax = end + new Vector2(1.5f, -0.4f); if (RsFree(ax, 0.3f)) Put(g, "T18e", "ChoppedLog", ax, TY(ax), 1.46f, 75f, 0f, 0f, 0.04f); }
            Count("wornPathDecals", pi);
        }
    }
}
