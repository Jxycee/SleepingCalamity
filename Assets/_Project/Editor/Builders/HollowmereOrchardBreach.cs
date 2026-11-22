// Pass A (wall kit): dressing of the orchard breach and of the Eye_Orchard foreground with scanned pieces (partial of HollowmereOrchard).
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        // The Wall_Breach FBX keeps only a low talus skirt, so the heap is scanned rock: mossy outcrop boulders on the flanks, mixed 0.3-1.8 m pieces stacked on a cone profile that falls from the
        // wall line (1.1 m) out to 3.4 m, and a ramp of small pieces left lower through the middle. No colliders (Put strips them), so the route over the breach stays walkable.
        // Raised pieces sit on the virtual heap surface and sink 22% of their size into it; the outer scatter is grounded and sunk by SCPlacementGrounding (GapRubble_*).
        // Ivy hangs from, and stones sit on, the broken wall tops found from the module's own vertices.
        // Pass A round 2: the scanned rocks rendered as glossy plastic; scale their smoothness down (and lift the albedo a touch) on these placements only, through scene-owned material variants.
        static void Matte(GameObject go, float smooth, float lift)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string nm = mats[i].name.Replace(" (Instance)", "").Replace("_Matte", "") + "_Matte";
                    Material v = Variant(mats[i], nm, new Color(lift, lift, lift));
                    if (v != null && v.HasProperty("_Smoothness")) { v.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(v); }
                    mats[i] = v; ch = true;
                }
                if (ch) r.sharedMaterials = mats;
            }
        }

        static GameObject PutM(Transform g, string code, string name, Vector2 q, float y, float size, float yaw, float tx, float tz, float sink)
        {
            GameObject go = Put(g, code, name, q, y, size, yaw, tx, tz, sink);
            if (code.StartsWith("T19", System.StringComparison.Ordinal)) return go;   // church-wall blocks were already matte and lit
            Matte(go, 0.22f, 1.3f); return go;
        }

        static int BreachDressing(Transform ruined, Transform g, Transform flora, Transform dec, Vector2 c, Vector2 n, Vector2 t)
        {
            Vector2 cam = new Vector2(41.9f, -52.0f);
            Transform mod = null; float best = 12f;
            foreach (Transform m in ruined.GetComponentsInChildren<Transform>(true))
            {
                if (!m.name.EndsWith("_Breach", System.StringComparison.Ordinal) && !m.name.EndsWith("_Crumbled", System.StringComparison.Ordinal)) continue;
                float d = Vector2.Distance(new Vector2(m.position.x, m.position.z), c); if (d < best) { best = d; mod = m; }
            }
            Vector2 bc = c, tt = t;
            if (mod != null)
            {
                Bounds mb = Render(mod.gameObject);
                bc = new Vector2(mb.center.x, mb.center.z);
                Vector3 r = mod.right; var rt = new Vector2(r.x, r.z); if (rt.sqrMagnitude > 0.01f) tt = rt.normalized;
                Count("breachModuleFound");
            }
            Vector2 o = new Vector2(-tt.y, tt.x); if (Vector2.Dot(cam - bc, o) < 0f) o = -o;   // out of the wall toward the orchard (and the eye camera)
            const float H0 = 1.7f, Dmax = 3.6f;
            float Prof(float u, float d)
            {
                float f = Mathf.Pow(Mathf.Clamp01(1f - d / Dmax), 1.25f);
                float side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 3.8f, Mathf.Abs(u)));
                float route = Mathf.Abs(u - 0.3f) < 0.7f ? 0.45f : 1f;   // the climbable ramp is lower
                return H0 * f * side * route;
            }
            string[] big = { "T19k", "T19l", "T05b", "T19k", "T19l", "T20c" };   // the jp_ rock set (T20d-h) renders glossy: left out
            string[] mid = { "T19l", "T05b", "T19k", "T20c", "T05b", "T19l" };
            string[] small = { "T05b", "T05c", "T05b", "N03", "T05c" };
            int made = 0, placedHeap = 0;
            for (int k = 0; k < 6; k++)   // flank boulders, half buried
            {
                float u = (k % 2 == 0 ? -1f : 1f) * Mathf.Lerp(2.3f, 3.3f, H(k, 1, 1301)); float d = Mathf.Lerp(0.3f, 1.4f, H(k, 2, 1301));
                Vector2 q = bc + tt * u + o * d; if (!Ok(q, 0.2f, false, false)) continue;
                float size = Mathf.Lerp(1.3f, 1.8f, H(k, 3, 1301));
                PutM(g, big[k % big.Length], "GapHeapTop_B" + k, q, Gy(q) + Prof(u, d) * 0.3f, size * 0.75f, H(k, 4, 1301) * 360f, (H(k, 5, 1301) - 0.5f) * 20f, (H(k, 6, 1301) - 0.5f) * 20f, size * 0.25f);
                made++;
            }
            for (int k = 0; k < 120; k++)   // the heap: stacked mixed pieces, big at the wall line and smaller going out, nothing large on the ramp
            {
                float u = (H(k, 11, 1302) - 0.5f) * 6.2f; float d = Mathf.Pow(H(k, 12, 1302), 1.15f) * Dmax;
                float h = Prof(u, d); if (h < 0.05f) continue;
                if (placedHeap >= 46) break;
                Vector2 q = bc + tt * u + o * d; if (!Ok(q, 0.2f, false, false)) continue;
                float size = Mathf.Lerp(2.0f, 0.55f, Mathf.Clamp01(d / Dmax + 0.25f * (H(k, 13, 1302) - 0.5f))) * Mathf.Lerp(0.75f, 1.25f, H(k, 14, 1302));
                bool ramp = Mathf.Abs(u - 0.3f) < 0.7f; if (ramp) size = Mathf.Min(size, 0.7f);
                placedHeap++;
                string[] pool = size > 0.9f ? big : size > 0.5f ? mid : small;
                PutM(g, pool[(int)(H(k, 15, 1302) * pool.Length) % pool.Length], "GapHeapTop_" + k, q, Gy(q) + h * Mathf.Lerp(0.55f, 0.95f, H(k, 16, 1302)), size, H(k, 17, 1302) * 360f, (H(k, 18, 1302) - 0.5f) * 40f, (H(k, 19, 1302) - 0.5f) * 40f, size * 0.22f);
                made++;
            }
            for (int k = 0; k < 9; k++)   // outer scatter, grounded on the terrain
            {
                float u = (H(k, 21, 1303) - 0.5f) * 8f; float d = Mathf.Lerp(2.4f, 5.2f, H(k, 22, 1303));
                Vector2 q = bc + tt * u + o * d; if (!Ok(q, 0.2f, false, false)) continue;
                float size = Mathf.Lerp(0.45f, 1.0f, H(k, 23, 1303));
                PutM(g, small[k % small.Length], "GapRubble_" + k, q, Gy(q), size, H(k, 24, 1303) * 360f, (H(k, 25, 1303) - 0.5f) * 20f, (H(k, 26, 1303) - 0.5f) * 20f, size * 0.2f);
                made++;
            }
            for (int k = 0; k < 9; k++)   // ferns through the heap
            {
                float u = (H(k, 31, 1304) - 0.5f) * 5.2f; float d = Mathf.Lerp(0.4f, 3.2f, H(k, 32, 1304));
                Vector2 q = bc + tt * u + o * d; if (!Ok(q, 0.05f, false, false)) continue;
                Put(g, k % 3 == 0 ? "T12b" : "T21j", "GapHeapFern_" + k, q, Gy(q) + Prof(u, d) * 0.5f, Mathf.Lerp(0.5f, 0.85f, H(k, 33, 1304)), H(k, 34, 1304) * 360f, 0f, 0f, 0.05f);
            }
            if (mod != null)   // wall tops from the module's own vertices: highest point per 0.3 m of wall
            {
                const int bins = 22; var top = new Vector3[bins]; var have = new bool[bins];
                foreach (MeshFilter mf in mod.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh me = mf.sharedMesh; if (me == null) continue;
                    foreach (Vector3 lv in me.vertices)
                    {
                        Vector3 wv = mf.transform.TransformPoint(lv);
                        float u = Vector2.Dot(new Vector2(wv.x, wv.z) - bc, tt); int bi = Mathf.FloorToInt((u + 3.3f) / 0.3f);
                        if (bi < 0 || bi >= bins) continue;
                        if (!have[bi] || wv.y > top[bi].y) { top[bi] = wv; have[bi] = true; }
                    }
                }
                float gy = Gy(bc); int placed = 0;
                for (int bi = 1; bi < bins - 1 && placed < 12; bi++)
                {
                    if (!have[bi] || top[bi].y < gy + 1.4f) continue;
                    bool edge = !have[bi - 1] || top[bi - 1].y < top[bi].y - 0.5f || !have[bi + 1] || top[bi + 1].y < top[bi].y - 0.5f;   // a broken end of a course
                    float r01 = H(bi, 1, 1305);
                    Vector2 xz = new Vector2(top[bi].x, top[bi].z);
                    if (edge) { GameObject iv = Put(g, "T12c", "GapIvyTop_" + bi, xz + o * 0.12f, top[bi].y - 1.5f, Mathf.Lerp(1.7f, 2.3f, r01), YawFace(o) + 180f + (r01 - 0.5f) * 40f, 0f, 0f, 0f); Recolor(iv, "IvyLit", new Color(2.1f, 2.4f, 1.3f)); placed++; }
                    else if (r01 < 0.4f) { PutM(g, r01 < 0.2f ? "T20c" : "T19l", "GapTopStone_" + bi, xz, top[bi].y, Mathf.Lerp(0.3f, 0.5f, r01 * 2.5f), H(bi, 2, 1305) * 360f, (H(bi, 3, 1305) - 0.5f) * 20f, (H(bi, 4, 1305) - 0.5f) * 20f, 0.06f); placed++; }
                }
                Count("breachTopDress", placed);
            }
            return made;
        }
    }
}
