// T13c (the last T13 run): the orchard's lane-kit pier and broken courses become T19 church-ruin stone, the OrchardGap foreground is opened up, the town wall's outer foot on the meadow and
// shore and the Mid shell R3_02's foot on the road out are grounded (rubble spill, plants, moss and damp decals). Editor-time only, position-hashed, a partial of HollowmereOrchard (shares
// Put, PutNat, WeedT, OkBank, TY, Decal and the prefab caches); called from HollowmereBuilder after BuildBanksT13, before Combine and the Mesh LODs.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        static readonly string[] FootRubble = { "T19k", "T05c", "T19g", "N03", "T19l", "T05b" };

        public static void BuildT13c(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            T = t; counts.Clear();
            Physics.SyncTransforms();
            Transform root = Sub(ruined, "T13c");
            Transform props = Sub(root, "Props"), flora = Sub(root, "Flora"), dec = Sub(root, "Decals");
            OrchardLaneKit(ruined, props, flora);
            GapLift(ruined, props, dec);
            ShoreWallFoot(props, flora, dec);
            ShellFoot("R3_02", props, flora, dec, 11);
            RoadTrees(Sub(props, "RoadTrees"));
            Debug.Log("SC HOLLOWMERE T13c: " + string.Join(", ", counts.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)));
        }

        // Lane-kit pieces of a family (name contains) within r of c, under root: removed, their positions and yaws returned.
        static List<(Vector3 pos, float yaw, string name)> TakeLanePieces(Transform root, string contains, Vector2 c, float r)
        {
            var found = new List<(Vector3, float, string)>();
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (tr == null || !tr.name.Contains(contains)) continue;
                if (Vector2.Distance(new Vector2(tr.position.x, tr.position.z), c) > r) continue;
                found.Add((tr.position, tr.eulerAngles.y, tr.name));
                Object.DestroyImmediate(tr.gameObject);
            }
            return found;
        }

        static void OrchardLaneKit(Transform ruined, Transform props, Transform flora)
        {
            Transform g = Sub(props, "OrchardWall");
            Vector2 mid = (PierA + PierB) * 0.5f;
            var piers = TakeLanePieces(ruined, "GardenWallEnd", new Vector2(38.5f, -45.5f), 6.0f);
            piers.AddRange(TakeLanePieces(ruined, "GatePier", new Vector2(38.5f, -45.5f), 6.0f));
            var broken = TakeLanePieces(ruined, "GardenWallBroken", mid, 14f);
            Count("t13cPiersRemoved", piers.Count); Count("t13cBrokenRemoved", broken.Count);
            int k = 0;
            foreach (var p in piers)
            {
                Vector2 q = new Vector2(p.pos.x, p.pos.z);
                float y = Gy(q);
                // a stump of the dry-stone wall: a big skirt block, a coping stone on it and a cracked cap, leaning a little, solid
                Put(g, "T19l", "Pier_" + k, q, y, Mathf.Lerp(1.0f, 1.25f, H(k, 1, 2100)), p.yaw + 90f, (H(k, 2, 2100) - 0.5f) * 10f, (H(k, 3, 2100) - 0.5f) * 10f, 0.12f, true);
                Put(g, "T19g", "PierCap_" + k, q + Dir(H(k, 4, 2100) * 360f) * 0.3f, y + 0.8f, 0.8f, H(k, 5, 2100) * 360f, 0f, (H(k, 6, 2100) - 0.5f) * 30f, 0f);
                for (int j = 0; j < 4; j++) { Vector2 r = q + Dir(j * 90f + 20f + 40f * H(k, 7 + j, 2100)) * Mathf.Lerp(0.8f, 1.5f, H(k, 12 + j, 2100)); if (OkBank(r, 0.1f)) Put(g, FootRubble[(j + k) % FootRubble.Length], "PierSpill_" + k + "_" + j, r, Gy(r), Mathf.Lerp(0.35f, 0.7f, H(k, 17 + j, 2100)), H(k, 22 + j, 2100) * 360f, (H(k, 27 + j, 2100) - 0.5f) * 20f, (H(k, 32 + j, 2100) - 0.5f) * 20f, 0.06f); }
                k++;
            }
            foreach (var p in broken)
            {
                Vector2 q = new Vector2(p.pos.x, p.pos.z);
                Vector2 along = Dir(-p.yaw);   // the piece's own X axis turned by its yaw
                // two or three courses of church-wall stone, stepping down along the old line, with a spill in front on both sides
                for (int c = 0; c < 3; c++)
                {
                    Vector2 q2 = q + along * ((c - 1) * 0.95f);
                    float h = (c == 1 ? 0.55f : c == 0 ? 0.8f : 0.4f) * Mathf.Lerp(0.8f, 1.2f, H(k, 40 + c, 2100));
                    Put(g, c == 1 ? "T19k" : "T19l", "Broken_" + k + "_" + c, q2, Gy(q2), c == 1 ? 1.9f : 1.5f, YawAlong(along) + (H(k, 44 + c, 2100) - 0.5f) * 14f, 0f, (H(k, 48 + c, 2100) - 0.5f) * 6f, 0.1f, true);
                }
                for (int j = 0; j < 5; j++) { Vector2 r = q + along * (j - 2) * 0.8f + Perp(along) * ((H(k, 60 + j, 2100) - 0.5f) * 2.2f); if (OkBank(r, 0.1f)) Put(g, FootRubble[(j + k) % FootRubble.Length], "BrokenSpill_" + k + "_" + j, r, Gy(r), Mathf.Lerp(0.3f, 0.8f, H(k, 66 + j, 2100)), H(k, 70 + j, 2100) * 360f, (H(k, 74 + j, 2100) - 0.5f) * 20f, (H(k, 78 + j, 2100) - 0.5f) * 20f, 0.06f); }
                for (int j = 0; j < 4; j++) { Vector2 r = q + along * ((j - 1.5f) * 0.9f) + Perp(along) * (H(k, 82 + j, 2100) < 0.5f ? 0.7f : -0.7f); if (OkBank(r, 0.05f)) Weed(flora, r, 2110 + k, j, 0.8f, 1.4f); }
                k++;
            }
        }

        // OrchardGap: the foreground in front of the camera (41.9, -52, bearing 38 deg) was a dark mat of clumps. Take out the tall clumps nearest the lens, keep a ground patch of light, add a shadowless fill and readable rubble.
        static void GapLift(Transform ruined, Transform props, Transform dec)
        {
            Vector2 cam = new Vector2(41.9f, -52.0f), fwd = new Vector2(0.616f, -0.788f);
            Transform orchard = ruined.Find("Orchard");
            int removed = 0;
            if (orchard != null)
                foreach (Transform tr in orchard.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (tr == null || tr == orchard || tr.GetComponent<Renderer>() == null && tr.GetComponent<LODGroup>() == null) continue;
                    string n = tr.name;
                    if (n.StartsWith("Gap", System.StringComparison.Ordinal) && !n.Contains("Fern") && !n.Contains("Weed")) continue;   // the rubble stays
                    Vector2 d = new Vector2(tr.position.x, tr.position.z) - cam;
                    float along = Vector2.Dot(d, fwd), side = Mathf.Abs(Vector2.Dot(d, Perp(fwd)));
                    if (along > 1.0f && along < 5.2f && side < 0.55f * along + 1.6f && H(removed, (int)(tr.position.x * 10f), 2200) < 0.78f) { Object.DestroyImmediate(tr.gameObject); removed++; }
                }
            Count("t13cGapFoliageRemoved", removed);
            Transform g = Sub(props, "GapLift");
            // readable low rubble in the cleared patch: flat skirt stones lying toward the camera
            for (int k = 0; k < 7; k++)
            {
                Vector2 q = cam + fwd * (1.8f + 0.45f * k) + Perp(fwd) * ((H(k, 1, 2210) - 0.5f) * (1.4f + 0.5f * k));
                if (!Ok(q, 0.2f, false, false)) continue;
                float size = Mathf.Lerp(0.5f, 1.1f, H(k, 2, 2210));
                Put(g, k % 2 == 0 ? "T19k" : "T19g", "GapPatch_" + k, q, Gy(q), size, H(k, 3, 2210) * 360f, 0f, (H(k, 4, 2210) - 0.5f) * 10f, 0.05f);
            }
            var lg = new GameObject("GapFill"); lg.transform.SetParent(g, false);
            Vector2 lp = cam + fwd * 3.2f;
            lg.transform.position = new Vector3(lp.x, Gy(lp) + 1.6f, lp.y);
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.92f, 0.74f); l.intensity = 3.0f; l.range = 7f; l.shadows = LightShadows.None;
            Decal(dec, "Damp", lp, 3.2f, 2.4f, YawFace(fwd), "T13cGapPatchDamp");
        }

        // Radius from the town wall's centre (24, -26) at which the steep revetment face meets the flat: scanned outward along the angle.
        static float WallFootRadius(float angDeg)
        {
            Vector2 centre = new Vector2(24f, -26f), d = Dir(angDeg);
            float last = -1f;
            for (float r = 34.0f; r < 60f; r += 0.25f) if (HollowmereRevetment.Covers(centre.x + d.x * r, centre.y + d.y * r, 0f)) last = r;
            return last > 0f ? last + 0.35f : -1f;   // the revetment's outer edge (the ragged earth line at the bottom of its fall), or no revetment on this bearing
        }

        static void ShoreWallFoot(Transform props, Transform flora, Transform dec)
        {
            Transform g = Sub(props, "ShoreFoot");
            Vector2 centre = new Vector2(24f, -26f);
            int n = 0, plants = 0;
            string[] ferns = { "T12b", "T21l", "T21e" };
            for (float ang = -136f; ang <= -44f; ang += 1.6f)
            {
                float hk = H((int)(ang * 10f), 1, 2300);
                float fr = WallFootRadius(ang); if (fr < 0f) continue;
                Vector2 d = Dir(ang);
                // rubble spill: bigger pieces at the foot, scattering out, never in a line; a slumped heap now and then
                int cnt = hk > 0.82f ? 4 : hk > 0.25f ? 3 : 2;
                for (int j = 0; j < cnt; j++)
                {
                    Vector2 q = centre + d * (fr + Mathf.Pow(H((int)(ang * 10f), 2 + j, 2300), 1.5f) * 2.4f - 0.2f) + Perp(d) * ((H((int)(ang * 10f), 8 + j, 2300) - 0.5f) * 1.6f);
                    if (!OkBank(q, 0.2f)) continue;
                    float size = (hk > 0.82f && j == 0 ? 2.4f : 0.95f) * Mathf.Lerp(0.7f, 1.4f, H((int)(ang * 10f), 14 + j, 2300));
                    if (PutSlopeDim(g, FootRubble[(int)(H((int)(ang * 10f), 20 + j, 2300) * FootRubble.Length) % FootRubble.Length], "ShoreRubble_" + n, q, size, H((int)(ang * 10f), 26 + j, 2300) * 360f, (H((int)(ang * 10f), 32, 2300) - 0.5f) * 18f, (H((int)(ang * 10f), 33, 2300) - 0.5f) * 18f, 0.3f) != null) n++;
                }
                // grass and nettle band at the foot, ferns in the damp, ivy start on every fifth step
                for (int j = 0; j < 3; j++)
                {
                    Vector2 q = centre + d * (fr - 0.15f + 0.9f * H((int)(ang * 10f), 40 + j, 2300)) + Perp(d) * ((H((int)(ang * 10f), 44 + j, 2300) - 0.5f) * 1.6f);
                    if (!OkBank(q, 0.05f)) continue;
                    WeedT(flora, q, 2310, (int)(ang * 3f) + j, 1.3f, 2.4f); plants++;
                }
                if (hk > 0.5f)
                {
                    Vector2 q = centre + d * (fr + 0.4f);
                    if (OkBank(q, 0.1f)) { PutNat(flora, ferns[(int)(hk * 30f) % ferns.Length], "ShoreFern_" + n + "_" + (int)(ang * 10f), q, TY(q), Mathf.Lerp(0.8f, 1.3f, hk), hk * 3600f, 0f, 0f, 0.04f); plants++; }
                }
                if (((int)((ang + 140f) / 1.6f)) % 4 == 0)
                {
                    Vector2 q = centre + d * (fr + 0.5f);
                    if (OkBank(q, 0.1f))
                    {
                        string m = hk < 0.4f ? "Moss" : hk < 0.75f ? "Damp" : "MossSoft";
                        Decal(dec, m, q, 3.0f, 2.2f, YawFace(-d) + 180f, "T13cShoreFoot_" + (int)(ang * 10f));
                    }
                }
            }
            Count("t13cShoreRubble", n); Count("t13cShorePlants", plants);
        }

        // The ring of a lot's building, grounded: rubble spill and plants round the base, moss and damp decals, an ivy start on two faces.
        static void ShellFoot(string lotId, Transform props, Transform flora, Transform dec, int extra)
        {
            Foundation f = T.Foundations.Find(x => x.Id == lotId);
            if (f == null) { Count("t13cMissingLot"); return; }
            Transform g = Sub(props, "Foot_" + lotId);
            float h = f.Side * 0.5f;
            int n = 0;
            for (int i = 0; i < 40; i++)
            {
                float u = (i + H(i, 1, 2400)) / 40f * 4f;   // 0..4 around the square
                int side = (int)u; float s = (u - side) * 2f - 1f;
                float outd = h + 0.15f + Mathf.Pow(H(i, 2, 2400), 1.4f) * 2.0f;
                float lx = side == 0 ? s * h : side == 1 ? outd : side == 2 ? -s * h : -outd;
                float lz = side == 0 ? -outd : side == 1 ? s * h : side == 2 ? outd : -s * h;
                TownPoint w = f.Footprint.ToWorld(lx, lz);
                Vector2 q = new Vector2(w.X, w.Z);
                if (!OkBank(q, 0.15f) || HollowmereTerrain.IsPaved(T, q.x, q.y)) continue;
                float r = H(i, 3, 2400);
                if (r < 0.45f) { PutSlopeDim(props, FootRubble[(int)(H(i, 4, 2400) * FootRubble.Length) % FootRubble.Length], "ShellRubble_" + lotId + "_" + i, q, Mathf.Lerp(0.35f, 0.9f, H(i, 5, 2400)), H(i, 6, 2400) * 360f, (H(i, 7, 2400) - 0.5f) * 20f, (H(i, 8, 2400) - 0.5f) * 20f, 0.25f); n++; }
                else if (r < 0.85f) { WeedT(flora, q, 2410, i, 1.4f, 2.6f); n++; }
                else { PutNat(flora, "T12b", "ShellFern_" + lotId + "_" + i, q, TY(q), Mathf.Lerp(0.8f, 1.2f, H(i, 9, 2400)), H(i, 10, 2400) * 360f, 0f, 0f, 0.04f); n++; }
                if (i % 5 == 0) Decal(dec, i % 10 == 0 ? "Moss" : "Damp", q, 2.6f, 2.0f, H(i, 11, 2400) * 360f, "T13cShellFoot_" + lotId + "_" + i);
            }
            Count("t13cShellFoot", n);
        }

        // The far-tree cards (crossed impostor quads) within 70 m of the road-out and road-back eyes read as lollipops: HollowmereRoad.Cards skips them (NearRoadEye) and real scanned trees stand at the same plan spots.
        internal static bool NearRoadEye(float x, float z)
        {
            foreach (var c in HollowmereBuilder.Cameras)
                if ((c.Name == "Cap_HM_Eye_RoadOut" || c.Name == "Cap_HM_Eye_RoadBack") && Vector2.Distance(new Vector2(c.Pos.x, c.Pos.z), new Vector2(x, z)) < 70f) return true;
            return false;
        }

        static void RoadTrees(Transform g)
        {
            string[] codes = { "TR02", "TR03", "TR02", "TR06" };
            int n = 0;
            foreach (RoadItem i in SaltwakeRoadPlan.Create(T))
            {
                if (i.Kind == RoadItemKind.FieldHedge && !i.OnRing && NearRoadEye(i.X, i.Z))
                {
                    // the card hedge's lime blobs: three real shrubs along its 4.6 m, uneven heights
                    Vector2 axis = AxisOfYaw(i.Yaw);
                    for (int b = 0; b < 3; b++)
                    {
                        Vector2 hp = new Vector2(i.X, i.Z) + axis * ((b - 1) * 1.6f + (H(n, b + 5, 2500) - 0.5f) * 0.5f);
                        if (H(n, b + 9, 2500) < 0.2f || !OkBank(hp, 0.2f)) continue;
                        Put(g, b == 1 ? "T21m" : "T21j", "FieldHedgeBush_" + n + "_" + b, hp, TY(hp), Mathf.Lerp(1.4f, 2.4f, H(n, b + 13, 2500)), H(n, b + 17, 2500) * 360f, 0f, 0f, 0.05f);
                        Count("t13cFieldHedgeBush");
                    }
                    n++; continue;
                }
                if (i.Kind != RoadItemKind.Tree || i.OnRing || !NearRoadEye(i.X, i.Z)) continue;
                Vector2 p = new Vector2(i.X, i.Z);
                if (eyeCams.Any(e => Vector2.Distance(e, p) < 3f) || !OkBank(p, 0.5f)) continue;
                float h = Mathf.Lerp(5.5f, 8.5f, H(n, 1, 2500));
                if (Put(g, codes[n % codes.Length], "RoadTree_" + n, p, TY(p), h, i.Yaw, 0f, 0f, 0.15f) != null) { Count("t13cRoadTrees"); }
                n++;
            }
        }
    }
}
