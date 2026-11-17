// Hollowmere's ground edges outside the town wall (area 1 world, T08b, 2026-11-17): the slope under the wall, the lake shore, the mill yard and the tree bases.
// R2 found the bank under the wall a smooth pale plane from the wall foot to the meadow, the shore one clean grass line to the water, and the mill standing on bare grass.
// The terrain splat (HollowmereTerrain) now paints dark earth under the wall and a ragged earth-and-shingle band at the waterline; this builder adds the things that stand on it:
//   1. Wall foot (Preset_Ruined only: the Rebuilt wall is whole): rubble and fallen blocks (T05b, T05c, N03 scree) in clumps along a line 30 m off the plateau.
//   2. Bank (R3 note: a mesh skin of dark earth patches was tried and dropped, it read as pale wedges): N02 boulders and N01 outcrops half sunk every 8-15 m along the slope (colliders), companion stones, clumps of shrubs (TR05-TR07) and ferns (T12b).
//   3. Shore: N03 and T11b stones at the waterline, N05 logs and N04 stumps on the shore, one boat drawn up on the shingle by the jetty and one moored (T13, colliders).
//   4. Mill yard: sacks (C11b), a cart (T16b), a Fence_Rail run, firewood (T06b), a millstone leaning on the wall, around the mill's door end and north wall.
//   5. Tree bases: an earth mound (one merged mesh, no collider), 2-3 stones (T11b) and a grass ring under each 3D tree outside the wall within 20 m of a lane or the shore.
// Placement is seeded by position hashes (no shared RNG), so one rejected spot never moves the others and two builds write the same scene. Rules: nothing on a lane, a
// path-graph edge (2.5 m), the jetty line or inside a lot or landmark; nothing within 3 m of a capture camera, 6 m of the Lake, JettyBack, Meadow and Mill eye cameras;
// colliders (boulders, outcrops, boats, cart, fence) only well off every walking line; small stones, rubble, plants and logs have none.
// Authority: none (editor-time scene building). Called from HollowmereBuilder.BuildScene after HollowmereWild.Build.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereShore
    {
        const string Adopted = "Assets/_Project/Content/Art/Town/Adopted/", Trees = "Assets/_Project/Content/Art/Town/Trees/";
        const string CellarAdopted = "Assets/_Project/Content/Art/Cellar/Adopted/";
        const string MeshFolder = "Assets/_Project/Content/Art/Town/Wild";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";

        static Terrain terrain; static TownLayout layout;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<GameObject, Bounds> bounds = new Dictionary<GameObject, Bounds>();
        static readonly List<(Vector2 Pos, Vector2 Dir, float Radius)> cams = new List<(Vector2, Vector2, float)>();
        static readonly List<(Vector2 A, Vector2 B)> pathEdges = new List<(Vector2, Vector2)>();
        static readonly Collider[] hits = new Collider[16];
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static Vector2 jettyA, jettyB;

        static void Count(string k) { counts[k] = counts.TryGetValue(k, out int n) ? n + 1 : 1; }

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            layout = t; terrain = set.GetComponentInChildren<Terrain>();
            if (terrain == null) { Debug.LogWarning("SC HOLLOWMERE shore: no terrain, nothing placed"); return; }
            prefabs.Clear(); bounds.Clear(); cams.Clear(); pathEdges.Clear(); counts.Clear();
            jettyA = HollowmereLakeArt.JettyStart; jettyB = HollowmereLakeArt.JettyEnd;
            var nodes = t.Nodes.ToDictionary(n => n.Id, n => new Vector2(n.Pos.X, n.Pos.Z));
            foreach (PathEdge e in t.Edges) if (nodes.TryGetValue(e.A, out Vector2 a) && nodes.TryGetValue(e.B, out Vector2 b)) pathEdges.Add((a, b));
            foreach (var c in HollowmereBuilder.Cameras.Concat(HollowmereRoad.Cameras))
            {
                Vector2 pos = new Vector2(c.Pos.x, c.Pos.z);
                if (c.Name == "Cap_HM_Eye_JettyBack") pos = jettyB;
                bool strict = c.Name == "Cap_HM_Eye_Lake" || c.Name == "Cap_HM_Eye_JettyBack" || c.Name == "Cap_HM_Eye_Meadow" || c.Name == "Cap_HM_Eye_Mill";
                cams.Add((pos, new Vector2(c.Target.x - pos.x, c.Target.z - pos.y).normalized, strict ? 6f : 3f));
            }
            Physics.SyncTransforms();

            Transform root = Group(set, "Shore");
            foreach (string n in new[] { "Bank", "Shrubs", "Shore", "MillYard", "TreeBases" }) { Transform old = root.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
            Transform ruinedRoot = Group(ruined, "Shore"); { Transform old = ruinedRoot.Find("WallFoot"); if (old != null) Object.DestroyImmediate(old.gameObject); }

            WallFoot(Group(ruinedRoot, "WallFoot"));
            Bank(Group(root, "Bank"), Group(root, "Shrubs"));
            ShoreLine(Group(root, "Shore"));
            MillYard(Group(root, "MillYard"));
            TreeBases(set, Group(root, "TreeBases"));
            Debug.Log("SC HOLLOWMERE shore: " + string.Join(", ", counts.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)));
        }

        // ------------------------------------------------------------------------------------------------ 1. wall foot
        static void WallFoot(Transform parent)
        {
            GameObject[] kinds = { Prefab(Adopted + "T05b/T05b.prefab"), Prefab(Adopted + "T05c/T05c.prefab"), Prefab(Adopted + "N03_scree_cluster/N03.prefab") };
            TownPoint[] line = HollowmereLayout.OffsetLine(29.3f, 16f);
            for (int i = 0; i < line.Length; i++)
            {
                if (Hash01(i, 1, 6001) > 0.5f) continue;
                Vector2 p = new Vector2(line[i].X, line[i].Z), n = Outward(line, i);
                int pieces = 1 + (int)(Hash01(i, 2, 6001) * 3f);
                for (int k = 0; k < pieces; k++)
                {
                    GameObject prefab = kinds[(int)(Hash01(i, 3 + k, 6001) * 2.999f)];
                    if (prefab == null) continue;
                    Vector2 q = p + n * Mathf.Lerp(-0.2f, 1.9f, Hash01(i, 10 + k, 6001)) + new Vector2(-n.y, n.x) * Mathf.Lerp(-1.4f, 1.4f, Hash01(i, 20 + k, 6001));
                    if (!Ok(q.x, q.y, 2.0f, 1.8f, 28.9f, 33f, false)) continue;
                    float ext = Mathf.Lerp(0.9f, 1.9f, Hash01(i, 30 + k, 6001));
                    GameObject go = Put(parent, prefab, q, ext, 0.3f, Hash01(i, 40 + k, 6001) * 360f, (Hash01(i, 50 + k, 6001) - 0.5f) * 36f, (Hash01(i, 60 + k, 6001) - 0.5f) * 36f, false);
                    go.name = "FootRubble_" + i + "_" + k; Count("footRubble");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ 2. bank
        static void Bank(Transform rocks, Transform shrubs)
        {
            GameObject n01 = Prefab(Adopted + "N01_cliff_slab/N01.prefab"), n02 = Prefab(Adopted + "N02_angular_rock/N02.prefab");
            TownPoint[] line = HollowmereLayout.OffsetLine(33f, 16f);
            float since = 0f, next = 6f + 5f * Hash01(0, 1, 6101);
            for (int i = 1; i < line.Length; i++)
            {
                since += Vector2.Distance(new Vector2(line[i].X, line[i].Z), new Vector2(line[i - 1].X, line[i - 1].Z));
                if (since < next) continue;
                Vector2 p = new Vector2(line[i].X, line[i].Z), n = Outward(line, i);
                Vector2 q = p + n * Mathf.Lerp(-3f, 5f, Hash01(i, 2, 6101));
                if (!Ok(q.x, q.y, 3.5f, 3.0f, 28.9f, 40f, true)) continue;   // a rejected spot is skipped and the interval restarts at the next free one
                since = 0f; next = 6f + 5f * Hash01(i, 3, 6101);
                float steep = Steepness(q.x, q.y);
                bool slab = steep > 22f && Hash01(i, 4, 6101) < 0.6f;
                GameObject prefab = slab ? n01 : n02;
                if (prefab == null) continue;
                float ext = slab ? Mathf.Lerp(2.4f, 3.8f, Hash01(i, 5, 6101)) : Mathf.Lerp(1.4f, 2.6f, Hash01(i, 5, 6101));
                if (!FreeOfColliders(q, ext * 0.55f)) continue;
                GameObject go = Put(rocks, prefab, q, ext, 0.38f, Hash01(i, 6, 6101) * 360f, (Hash01(i, 7, 6101) - 0.5f) * 20f, (Hash01(i, 8, 6101) - 0.5f) * 20f, true);
                go.name = (slab ? "Outcrop_" : "Boulder_") + i; Count(slab ? "outcrops" : "boulders");
                if (n02 != null && Hash01(i, 9, 6101) < 0.65f)   // a companion stone
                {
                    Vector2 c = q + new Vector2(Hash01(i, 11, 6101) - 0.5f, Hash01(i, 12, 6101) - 0.5f).normalized * (ext * 0.5f + Mathf.Lerp(0.6f, 1.4f, Hash01(i, 13, 6101)));
                    float ce = Mathf.Lerp(0.6f, 1.1f, Hash01(i, 14, 6101));
                    if (Ok(c.x, c.y, 3.0f, 2.5f, 28.9f, 42f, true) && FreeOfColliders(c, ce * 0.6f))
                    { Put(rocks, n02, c, ce, 0.35f, Hash01(i, 15, 6101) * 360f, (Hash01(i, 16, 6101) - 0.5f) * 25f, (Hash01(i, 17, 6101) - 0.5f) * 25f, false).name = "BankStone_" + i; Count("bankStones"); }
                }
            }

            // loose stones over the slope (no colliders): T11b and N03 pieces, a few per 2 m of the line
            GameObject scree = Prefab(Adopted + "N03_scree_cluster/N03.prefab"), pebble = Prefab(Adopted + "T11b/T11b.prefab");
            TownPoint[] line3 = HollowmereLayout.OffsetLine(35f, 16f);
            for (int i = 0; i < line3.Length; i++)
                for (int k = 0; k < 2; k++)
                {
                    if (Hash01(i, k, 6151) > 0.55f) continue;
                    Vector2 p = new Vector2(line3[i].X, line3[i].Z), n = Outward(line3, i);
                    Vector2 q = p + n * Mathf.Lerp(-5f, 7f, Hash01(i, 5 + k, 6151)) + new Vector2(-n.y, n.x) * Mathf.Lerp(-1f, 1f, Hash01(i, 8 + k, 6151));
                    bool big = Hash01(i, 11 + k, 6151) < 0.35f;
                    GameObject prefab = big ? scree : pebble; if (prefab == null || !Ok(q.x, q.y, 2.0f, 1.8f, 28.9f, 43f, false)) continue;
                    Put(rocks, prefab, q, big ? Mathf.Lerp(0.8f, 1.7f, Hash01(i, 14 + k, 6151)) : Mathf.Lerp(0.4f, 1.0f, Hash01(i, 14 + k, 6151)), 0.3f, Hash01(i, 17 + k, 6151) * 360f,
                        (Hash01(i, 20 + k, 6151) - 0.5f) * 30f, (Hash01(i, 23 + k, 6151) - 0.5f) * 30f, false).name = "SlopeStone_" + i + "_" + k;
                    Count("slopeStones");
                }

            // shrubs and ferns in clumps on the slope below the wall's foot
            GameObject[] shrubKinds = { Tree("TR05"), Tree("TR06"), Tree("TR07") }; GameObject fern = Prefab(Adopted + "T12b/T12b.prefab");
            TownPoint[] line2 = HollowmereLayout.OffsetLine(33.5f, 16f);
            for (int i = 0; i < line2.Length; i++)
            {
                if (Hash01(i, 1, 6201) > 0.34f) continue;
                Vector2 p = new Vector2(line2[i].X, line2[i].Z), n = Outward(line2, i);
                Vector2 centre = p + n * Mathf.Lerp(-2.5f, 5f, Hash01(i, 2, 6201));
                int members = 2 + (int)(Hash01(i, 3, 6201) * 2.99f);
                for (int k = 0; k < members; k++)
                {
                    Vector2 q = centre + new Vector2(Hash01(i, 10 + k, 6201) - 0.5f, Hash01(i, 20 + k, 6201) - 0.5f) * 3.2f;
                    GameObject shrub = shrubKinds[(int)(Hash01(i, 30 + k, 6201) * 2.999f)];
                    if (shrub == null || !Ok(q.x, q.y, 2.0f, 1.8f, 28.9f, 42f, false)) continue;
                    float h = Mathf.Lerp(1.2f, 2.6f, Hash01(i, 40 + k, 6201));
                    PutHeight(shrubs, shrub, q, h, 0.035f, Hash01(i, 50 + k, 6201) * 360f, "Shrub_" + i + "_" + k, false); Count("shrubs");
                }
                if (fern != null)
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 q = centre + new Vector2(Hash01(i, 60 + k, 6201) - 0.5f, Hash01(i, 70 + k, 6201) - 0.5f) * 4f;
                        if (!Ok(q.x, q.y, 2.0f, 1.8f, 28.9f, 42f, false)) continue;
                        PutHeight(shrubs, fern, q, Mathf.Lerp(0.5f, 0.95f, Hash01(i, 80 + k, 6201)), 0.04f, Hash01(i, 90 + k, 6201) * 360f, "Fern_" + i + "_" + k, false); Count("ferns");
                    }
            }
        }

        // ------------------------------------------------------------------------------------------------ 3. shore
        static void ShoreLine(Transform parent)
        {
            // the waterline: for each angle round the lake, the last point going outward from the centre where the ground is still under the water
            var pts = new List<(Vector2 P, Vector2 N)>();
            float cx = HollowmereLayout.LakeCentre.X, cz = HollowmereLayout.LakeCentre.Z, rx = HollowmereLayout.LakeRadiusX, rz = HollowmereLayout.LakeRadiusZ;
            for (int a = 0; a < 180; a++)
            {
                float th = a * 2f * Mathf.PI / 180f, c = Mathf.Cos(th), s = Mathf.Sin(th);
                Vector2 last = Vector2.zero; bool found = false;
                for (float u = 0.55f; u <= 1.7f; u += 0.01f)
                {
                    float x = cx + c * rx * u, z = cz + s * rz * u;
                    if (!SaltwakeRoadPlan.OnTerrain(x, z, 2f)) break;
                    if (GroundY(x, z) < HollowmereLayout.WaterY) { last = new Vector2(x, z); found = true; }
                }
                if (!found) continue;
                var nrm = new Vector2((last.x - cx) / (rx * rx), (last.y - cz) / (rz * rz)).normalized;
                if (HollowmereLayout.DistanceToPolyline(new TownPoint(last.x, last.y), HollowmereLayout.Stream) < 7f) continue;   // the stream mouth
                pts.Add((last, nrm));
            }
            GameObject n03 = Prefab(Adopted + "N03_scree_cluster/N03.prefab"), t11b = Prefab(Adopted + "T11b/T11b.prefab");
            for (int i = 0; i < pts.Count; i++)
            {
                if (Hash01(i, 1, 6301) > 0.6f) continue;
                int stones = 1 + (int)(Hash01(i, 2, 6301) * 2.99f);
                for (int k = 0; k < stones; k++)
                {
                    Vector2 q = pts[i].P + pts[i].N * Mathf.Lerp(-0.5f, 1.8f, Hash01(i, 10 + k, 6301)) + new Vector2(-pts[i].N.y, pts[i].N.x) * Mathf.Lerp(-1.2f, 1.2f, Hash01(i, 20 + k, 6301));
                    bool scree = Hash01(i, 30 + k, 6301) < 0.4f;
                    GameObject prefab = scree ? n03 : t11b; if (prefab == null) continue;
                    if (!OkShore(q, 3.5f)) continue;
                    float ext = scree ? Mathf.Lerp(0.7f, 1.4f, Hash01(i, 40 + k, 6301)) : Mathf.Lerp(0.35f, 0.9f, Hash01(i, 40 + k, 6301));
                    Put(parent, prefab, q, ext, 0.3f, Hash01(i, 50 + k, 6301) * 360f, (Hash01(i, 60 + k, 6301) - 0.5f) * 30f, (Hash01(i, 70 + k, 6301) - 0.5f) * 30f, false).name = "ShoreStone_" + i + "_" + k;
                    Count("shoreStones");
                }
            }

            // logs and stumps at spaced points of the shore, away from the jetty
            GameObject log = Prefab(Adopted + "N05_fallen_log/N05.prefab"), stump = Prefab(Adopted + "N04_stump/N04.prefab");
            var used = new List<Vector2>();
            int logsLeft = 4, stumpsLeft = 3;
            for (int pass = 0; pass < pts.Count && (logsLeft > 0 || stumpsLeft > 0); pass++)
            {
                int i = (pass * 7 + 3) % pts.Count;
                Vector2 q = pts[i].P + pts[i].N * Mathf.Lerp(0.7f, 2.0f, Hash01(i, 1, 6401));
                if (!OkShore(q, 8f) || used.Any(u => (u - q).sqrMagnitude < 18f * 18f)) continue;
                if (logsLeft > 0 && log != null)
                {
                    Vector2 tangent = new Vector2(-pts[i].N.y, pts[i].N.x);
                    Put(parent, log, q, Mathf.Lerp(2.4f, 3.4f, Hash01(i, 2, 6401)), 0.15f, Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + (Hash01(i, 3, 6401) - 0.5f) * 40f, 0f, 0f, false).name = "ShoreLog_" + i;
                    logsLeft--; used.Add(q); Count("logs");
                }
                else if (stumpsLeft > 0 && stump != null)
                {
                    Put(parent, stump, q, Mathf.Lerp(0.7f, 1.0f, Hash01(i, 4, 6401)), 0.1f, Hash01(i, 5, 6401) * 360f, 0f, 0f, false).name = "ShoreStump_" + i;
                    stumpsLeft--; used.Add(q); Count("stumps");
                }
            }

            // the boats (T13): one drawn up on the shingle east of the jetty, one moored off its west side
            GameObject boat = Prefab(Adopted + "T13/T13.prefab");
            if (boat == null) return;
            Bounds bb = MeasureBounds(boat);
            bool longX = bb.size.x >= bb.size.z;
            float boatLen = 3.6f, bsc = boatLen / Mathf.Max(bb.size.x, bb.size.z);
            Vector2 jdir = (jettyB - jettyA).normalized, jperp = new Vector2(-jdir.y, jdir.x);   // jperp: left of the jetty's direction
            int best = -1; float bestScore = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = Vector2.Distance(pts[i].P, jettyA);
                float side = Vector2.Dot(pts[i].P - jettyA, jperp);
                if (d < 10f || d > 20f || !OkShore(pts[i].P + pts[i].N * 1.2f, 3.5f)) continue;
                float score = Mathf.Abs(d - 14f) + (side > 0f ? 0f : 6f);   // prefer the left side (east: the Lake camera looks across it), about 10 m out
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0)
            {
                Vector2 pos = pts[best].P + pts[best].N * 1.1f, toWater = -pts[best].N;
                float y = GroundY(pos.x, pos.y);
                float yBow = GroundY(pos.x + toWater.x * boatLen * 0.5f, pos.y + toWater.y * boatLen * 0.5f), yStern = GroundY(pos.x - toWater.x * boatLen * 0.5f, pos.y - toWater.y * boatLen * 0.5f);
                Quaternion look = Quaternion.LookRotation(new Vector3(toWater.x, Mathf.Clamp((yBow - yStern) / boatLen, -0.2f, 0.2f), toWater.y), Vector3.up) * (longX ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity);
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(boat, parent);
                go.name = "Boat_Beached";
                go.transform.position = new Vector3(pos.x, Mathf.Min(y, yStern) - 0.04f * bb.size.y * bsc, pos.y); go.transform.rotation = look; go.transform.localScale = Vector3.one * bsc;
                AddBox(go, bb); Count("boats");
            }
            for (float away = 5f; away <= 11f; away += 1f)
            {
                Vector2 pos = jettyA + jdir * 4.5f - jperp * away;   // the right-hand side (west) of the deck
                if (GroundY(pos.x, pos.y) > HollowmereLayout.WaterY - 0.7f || DistToJetty(pos) < 3.5f) continue;
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(boat, parent);
                go.name = "Boat_Moored";
                float yaw = Mathf.Atan2(jdir.x, jdir.y) * Mathf.Rad2Deg + 14f;
                go.transform.position = new Vector3(pos.x, HollowmereLayout.WaterY - 0.10f * bb.size.y * bsc, pos.y);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * (longX ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity);
                go.transform.localScale = Vector3.one * bsc;
                AddBox(go, bb); Count("boats");
                break;
            }
        }

        static bool OkShore(Vector2 q, float jettyGap)
        {
            if (!SaltwakeRoadPlan.OnTerrain(q.x, q.y, 1.5f)) return false;
            float y = GroundY(q.x, q.y);
            if (y < HollowmereLayout.WaterY - 0.45f || y > HollowmereLayout.WaterY + 1.6f) return false;
            if (DistToJetty(q) < jettyGap) return false;
            return Ok(q.x, q.y, 1.5f, 1.8f, 0f, 1000f, false, true);
        }

        // ------------------------------------------------------------------------------------------------ 4. mill yard
        static void MillYard(Transform parent)
        {
            Landmark mill = layout.Landmarks.FirstOrDefault(l => l.Kind == LandmarkKind.Mill);
            if (mill == null) return;
            Footprint fp = mill.Footprint;
            float yaw = Mathf.Atan2(-fp.AxisX.Z, fp.AxisX.X) * Mathf.Rad2Deg;   // the mill's local X axis in world yaw
            Vector2 W(float lx, float lz) { TownPoint w = fp.ToWorld(lx, lz); return new Vector2(w.X, w.Z); }
            float zN = fp.HalfZ + 0.55f;   // just off the north wall face

            // sacks (C11b jute sack) by the north wall near the door end
            GameObject sack = Prefab(CellarAdopted + "C11b/C11b.prefab");
            if (sack != null)
            {
                float[] sx = { -5.3f, -4.9f, -5.7f, -4.6f }, sz = { 3.2f, 3.9f, 4.1f, 4.8f };
                for (int k = 0; k < sx.Length; k++)
                {
                    Vector2 q = W(sx[k], sz[k]);
                    if (!Ok(q.x, q.y, 1.5f, 1.2f, 0f, 1000f, false, true, true)) continue;
                    PutHeight(parent, sack, q, k == 2 ? 0.55f : 0.72f, 0.02f, yaw + 90f + (Hash01(k, 1, 6501) - 0.5f) * 50f, "Sack_" + k, true); Count("sacks");
                }
            }
            // firewood (T06b) stacked against the north wall at the east end
            GameObject wood = Prefab(Adopted + "T06b/T06b.prefab");
            if (wood != null)
            {
                Vector2 q = W(2.6f, zN + 0.3f);
                if (Ok(q.x, q.y, 1.5f, 1.2f, 0f, 1000f, false, true, true)) { Put(parent, wood, q, 1.5f, 0.02f, yaw, 0f, 0f, false).name = "Firewood"; Count("firewood"); }
            }
            // a millstone leaning on the north wall: a stone disc, 1.3 m across and 0.3 thick, tipped back about 14 degrees against the wall
            {
                Vector2 q = W(0.7f, zN + 0.1f);
                if (Ok(q.x, q.y, 1.5f, 1.2f, 0f, 1000f, false, true, true)) Millstone(parent, new Vector3(q.x, GroundY(q.x, q.y), q.y), yaw);
            }
            // a cart (T16b) in the yard before the door end
            GameObject cart = Prefab(Adopted + "T16b/T16b.prefab");
            if (cart != null)
            {
                Vector2 q = W(-8.4f, 5.6f);
                if (Ok(q.x, q.y, 2.5f, 2.5f, 0f, 1000f, false, true, true) && FreeOfColliders(q, 1.4f))
                { Put(parent, cart, q, 2.6f, 0.0f, yaw + 18f, 0f, 0f, true).name = "MillCart"; Count("carts"); }
            }
            // a fence run (Fence_Rail, 3 m a piece) along the north edge of the yard, one gap
            for (int k = 0; k < 5; k++)
            {
                if (k == 3) continue;
                Vector2 q = W(-6f + k * 3f + 1.5f, zN + 4.8f);
                if (!Ok(q.x, q.y, 2.5f, 2.5f, 0f, 1000f, false, true, true) || !FreeOfColliders(q, 1.2f)) continue;
                GameObject f = HollowmereRoad.Prop("Fence_Rail", parent);
                if (f == null) break;
                f.name = "MillFence_" + k;
                f.transform.position = new Vector3(q.x, GroundY(q.x, q.y), q.y); f.transform.rotation = Quaternion.Euler(0f, yaw, 0f); f.transform.localScale = Vector3.one;
                var box = f.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.55f, 0f); box.size = new Vector3(3f, 1.1f, 0.2f);
                Count("fence");
            }
        }

        static void Millstone(Transform parent, Vector3 foot, float yaw)
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_Dressed.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_TW_Coping.mat");
            var go = new GameObject("Millstone"); go.transform.SetParent(parent, false);
            go.transform.position = foot + Vector3.up * 0.62f;
            // the disc's flat faces look along local Y; tip it back (toward the wall, +z in the mill's frame) 76 degrees from the horizontal
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-76f, 0f, 0f);
            go.transform.localScale = new Vector3(1.3f, 0.15f, 1.3f);
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.AddComponent<MeshFilter>().sharedMesh = cyl.GetComponent<MeshFilter>().sharedMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = stone; r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
            Object.DestroyImmediate(cyl);
            var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(0.95f, 2f, 0.95f);
            Count("millstone");
        }

        // ------------------------------------------------------------------------------------------------ 5. tree bases
        static void TreeBases(Transform set, Transform parent)
        {
            GameObject stoneA = Prefab(Adopted + "T11b/T11b.prefab");
            var trees = new List<Transform>();
            Transform wild = set.Find("Wild");
            if (wild == null) return;
            foreach (string g in new[] { "TownTrees", "WallBelt", "HillForest" })
            {
                Transform grp = wild.Find(g); if (grp == null) continue;
                foreach (Transform c in grp)
                {
                    string n = c.name; int dot = n.IndexOf(' '); if (dot > 0) n = n.Substring(0, dot);
                    if (!n.StartsWith("TR0") || n == "TR05" || n == "TR06" || n == "TR07") continue;
                    trees.Add(c);
                }
            }
            // shoreline sample for the 20 m rule (the north shore only matters: coarse is fine)
            var mb = new MeshBuf(); int made = 0;
            Material soil = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_KT_Soil.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_GD_packed_soil.mat");
            for (int i = 0; i < trees.Count; i++)
            {
                Vector3 b = trees[i].position; Vector2 p = new Vector2(b.x, b.z);
                if (HollowmereLayout.PlateauDistance(p.x, p.y) < 30f || HollowmereLayout.BackHill(p.x, p.y) > 0.05f) continue;
                if (!NearLaneOrShore(p, 20f)) continue;
                if (cams.Any(c => (c.Pos - p).sqrMagnitude < 3f * 3f)) continue;
                float rad = Mathf.Lerp(1.3f, 1.9f, Hash01(i, 1, 6601));
                Mound(mb, p, rad, i);
                made++;
                int stones = 2 + (Hash01(i, 2, 6601) < 0.5f ? 1 : 0);
                for (int k = 0; k < stones && stoneA != null; k++)
                {
                    float a = (Hash01(i, 10 + k, 6601) + k / (float)stones) * Mathf.PI * 2f, d = rad * Mathf.Lerp(0.8f, 1.15f, Hash01(i, 20 + k, 6601));
                    Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (!Ok(q.x, q.y, 1.0f, 1.2f, 0f, 1000f, false, true, true)) continue;
                    Put(parent, stoneA, q, Mathf.Lerp(0.3f, 0.6f, Hash01(i, 30 + k, 6601)), 0.3f, Hash01(i, 40 + k, 6601) * 360f, (Hash01(i, 50 + k, 6601) - 0.5f) * 25f, (Hash01(i, 60 + k, 6601) - 0.5f) * 25f, false).name = "BaseStone_" + i + "_" + k;
                    Count("baseStones");
                }
                for (int k = 0; k < 6; k++)   // a ring of grass and weeds just outside the mound
                {
                    float a = (k + Hash01(i, 70 + k, 6601)) * Mathf.PI * 2f / 6f, d = rad * Mathf.Lerp(1.05f, 1.7f, Hash01(i, 80 + k, 6601));
                    Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (!Ok(q.x, q.y, 1.0f, 1.2f, 0f, 1000f, false, true, true)) continue;
                    string kit = k % 3 == 0 ? "Weed_B" : k % 3 == 1 ? "Grass_B" : "Grass_A";
                    if (HollowmereFoliage.Tuft(parent, kit, new Vector3(q.x, GroundY(q.x, q.y) - 0.02f, q.y), Hash01(i, 90 + k, 6601) * 360f, 0.8f + 0.5f * Hash01(i, 100 + k, 6601)) != null) Count("baseTufts");
                }
            }
            if (made == 0) return;
            BuilderUtil.EnsureFolder(MeshFolder);
            Mesh mesh = HollowmereB3Ground.SaveMesh(MeshFolder + "/TreeBases_Soil.asset", mb.ToMesh("TreeBases_Soil"));
            var go = new GameObject("TreeBaseMounds"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = soil; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            counts["treeBaseMounds"] = made;
        }

        // A low soil mound (0.14 m high) hugging the terrain: 8 rim points, 2 rings, vertices sampled from the terrain so it sits on slopes.
        static void Mound(MeshBuf mb, Vector2 p, float rad, int seed)
        {
            const int segs = 10, rings = 2;
            int b0 = mb.V.Count;
            float cy = GroundY(p.x, p.y);
            mb.V.Add(new Vector3(p.x, cy + 0.16f, p.y)); mb.N.Add(Vector3.up); mb.UV.Add(new Vector2(p.x, p.y) / 1.5f);
            for (int r = 1; r <= rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    float u = r / (float)rings, a = s * Mathf.PI * 2f / segs;
                    float rr = rad * u * (1f + 0.22f * (Hash01(seed, s, 6701) - 0.5f));
                    float x = p.x + Mathf.Cos(a) * rr, z = p.y + Mathf.Sin(a) * rr;
                    float lift = r == rings ? 0.02f : 0.11f;
                    mb.V.Add(new Vector3(x, GroundY(x, z) + lift, z)); mb.N.Add(Vector3.up); mb.UV.Add(new Vector2(x, z) / 1.5f);
                }
            for (int s = 0; s < segs; s++) { mb.I.Add(b0); mb.I.Add(b0 + 1 + (s + 1) % segs); mb.I.Add(b0 + 1 + s); }
            for (int r = 1; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int a = b0 + 1 + (r - 1) * segs + s, b = b0 + 1 + (r - 1) * segs + (s + 1) % segs, c = a + segs, d = b + segs;
                    mb.I.Add(a); mb.I.Add(b); mb.I.Add(c); mb.I.Add(b); mb.I.Add(d); mb.I.Add(c);
                }
        }

        static bool NearLaneOrShore(Vector2 p, float range)
        {
            if (LaneEdge(new TownPoint(p.x, p.y)) < range) return true;
            float y = GroundY(p.x, p.y);
            if (y < HollowmereLayout.WaterY + 8f)
            {
                // within `range` of the water: look for under-water ground on a ring of samples
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI * 2f / 16f;
                    for (float d = 4f; d <= range; d += 4f) if (GroundY(p.x + Mathf.Cos(a) * d, p.y + Mathf.Sin(a) * d) < HollowmereLayout.WaterY) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ placement helpers
        static Vector2 Outward(TownPoint[] line, int i)
        {
            TownPoint a = line[Mathf.Max(0, i - 1)], b = line[Mathf.Min(line.Length - 1, i + 1)];
            var tg = new Vector2(b.X - a.X, b.Z - a.Z).normalized; var n = new Vector2(-tg.y, tg.x);
            Vector2 p = new Vector2(line[i].X, line[i].Z);
            return HollowmereLayout.PlateauDistance(p.x + n.x, p.y + n.y) >= HollowmereLayout.PlateauDistance(p.x - n.x, p.y - n.y) ? n : -n;
        }

        // The placement rules. `minE`/`maxE` bound the plateau distance (the wall's foot to the slope's foot), laneGap and pathGap are clear distances from a lane's edge and the
        // path graph, `colliders` the extra rock clearance from the jetty. Camera and lot checks always apply. `lowOk` allows the waterline (the shore and yard items).
        static bool Ok(float x, float z, float laneGap, float pathGap, float minE, float maxE, bool bigRock, bool lowOk = false, bool anyE = false)
        {
            if (!SaltwakeRoadPlan.OnTerrain(x, z, 1.5f)) return false;
            var p = new TownPoint(x, z); var p2 = new Vector2(x, z);
            if (!anyE && !lowOk) { float e = HollowmereLayout.PlateauDistance(x, z); if (e < minE || e > maxE) return false; }
            if (HollowmereLayout.BackHill(x, z) > 0.05f && !anyE) return false;
            if (HollowmereTerrain.IsPaved(layout, x, z) || LaneEdge(p) < laneGap || PathDistance(p2) < pathGap) return false;
            foreach (Foundation f in layout.Foundations) if (f.Footprint.Contains(p, 1.2f)) return false;
            foreach (Landmark l in layout.Landmarks) if (l.Footprint.Contains(p, anyE && l.Kind == LandmarkKind.Mill ? -0.1f : 0.6f)) return false;
            if (DistToJetty(p2) < (bigRock ? 5f : 2.5f)) return false;
            foreach (var c in cams)
            {
                float d = Vector2.Distance(c.Pos, p2);
                if (d < c.Radius) return false;
            }
            if (HollowmereLayout.DistanceToPolyline(p, HollowmereLayout.Stream) < (bigRock ? 6f : 3f) && HollowmereLayout.PlateauDistance(x, z) > 30f && !lowOk) return false;
            float y = GroundY(x, z);
            if (!lowOk && y < HollowmereLayout.WaterY + 0.9f) return false;
            return true;
        }

        static bool FreeOfColliders(Vector2 q, float radius)
        {
            float y = GroundY(q.x, q.y);
            int n = Physics.OverlapSphereNonAlloc(new Vector3(q.x, y + radius + 0.3f, q.y), radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider)) return false;
            return true;
        }

        // A prefab at `q` scaled so its larger horizontal extent is `ext`, sunk by `sink` x its height, yawed and tilted; optional box collider from the LOD0 bounds.
        static GameObject Put(Transform parent, GameObject prefab, Vector2 q, float ext, float sink, float yaw, float tiltX, float tiltZ, bool collider)
        {
            Bounds b = MeasureBounds(prefab);
            float s = Mathf.Clamp(ext / Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z)), 0.02f, 20f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = new Vector3(q.x, GroundY(q.x, q.y) - sink * b.size.y * s, q.y);
            go.transform.rotation = Quaternion.Euler(tiltX, yaw, tiltZ);
            go.transform.localScale = Vector3.one * s;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            if (collider) AddBox(go, b);
            return go;
        }

        // A plant or sack scaled to a height (its foot on the ground, sunk a little), colliders stripped unless `collider`.
        static GameObject PutHeight(Transform parent, GameObject prefab, Vector2 q, float height, float sinkFrac, float yaw, string name, bool collider)
        {
            Bounds b = MeasureBounds(prefab);
            float s = height / Mathf.Max(0.05f, b.max.y);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(q.x, GroundY(q.x, q.y) - sinkFrac * height, q.y);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = Vector3.one * s;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        static void AddBox(GameObject go, Bounds b)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center; box.size = b.size * 0.85f;
        }

        static GameObject Prefab(string path)
        {
            if (prefabs.TryGetValue(path, out GameObject cached)) return cached;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) Debug.LogWarning("SC HOLLOWMERE shore: prefab missing, its pieces are skipped: " + path);
            prefabs[path] = p;
            return p;
        }

        static GameObject Tree(string code) => Prefab(Trees + code + "/" + code + ".prefab");

        // The LOD0 bounds of a prefab at identity (pivot at the base).
        static Bounds MeasureBounds(GameObject prefab)
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

        static float GroundY(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

        static float Steepness(float x, float z)
        {
            Vector3 o = terrain.transform.position, size = terrain.terrainData.size;
            return terrain.terrainData.GetSteepness(Mathf.Clamp01((x - o.x) / size.x), Mathf.Clamp01((z - o.z) / size.z));
        }

        static float DistToJetty(Vector2 p)
        {
            Vector2 ab = jettyB - jettyA;
            float f = Mathf.Clamp01(Vector2.Dot(p - jettyA, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector2.Distance(p, jettyA + ab * f);
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

        static float LaneEdge(TownPoint p)
        {
            float d = float.MaxValue;
            foreach (Lane lane in layout.Lanes) d = Mathf.Min(d, HollowmereLayout.DistanceToPolyline(p, lane.Points) - lane.Width * 0.5f);
            return d;
        }

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

        sealed class MeshBuf
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> I = new List<int>();
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(I, 0);
                m.RecalculateBounds(); m.RecalculateTangents();
                return m;
            }
        }
    }
}
