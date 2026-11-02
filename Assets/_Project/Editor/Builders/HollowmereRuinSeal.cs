// Seals the slots inside a ruin kit's Footing mesh. The ruin's broken walls leave 0.3-0.7 m wide slots between wall stubs: they look passable, but the hero capsule
// (0.7 m) cannot enter, so the hero is funnelled into a dead end (TraversalScan TightGap, the dominant Hollowmere source). A slot is an empty run of the wall plan
// (cells with a face above the step height) that is closed by wall on both ends and narrower than the hero; each such run becomes part of a box collider (no renderer),
// so the slot is solid, like the walls around it. Editor-only; the lot builder calls Seal once per ruin Footing mesh, after its MeshCollider.
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereRuinSeal
    {
        const float Cell = 0.1f, MaxSlot = 0.75f, WallFrom = 0.6f, SealBottom = -0.5f, SealTop = 0.9f;

        // lot: the lot's frame (pad at local y 0). Returns the number of boxes added.
        public static int Seal(Transform lot, MeshFilter footing)
        {
            Mesh m = footing.sharedMesh;
            if (m == null) return 0;
            Vector3[] vs = m.vertices; int[] tris = m.triangles;
            var p = new Vector3[vs.Length];
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < vs.Length; i++)
            {
                p[i] = lot.InverseTransformPoint(footing.transform.TransformPoint(vs[i]));
                minX = Mathf.Min(minX, p[i].x); maxX = Mathf.Max(maxX, p[i].x); minZ = Mathf.Min(minZ, p[i].z); maxZ = Mathf.Max(maxZ, p[i].z);
            }
            minX -= 0.2f; minZ -= 0.2f;
            int w = Mathf.CeilToInt((maxX + 0.2f - minX) / Cell) + 1, h = Mathf.CeilToInt((maxZ + 0.2f - minZ) / Cell) + 1;
            if (w <= 2 || h <= 2 || w > 400 || h > 400) return 0;
            var solid = new bool[w * h];
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = p[tris[t]], b = p[tris[t + 1]], c = p[tris[t + 2]];
                if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < WallFrom) continue;
                Stamp(solid, w, h, minX, minZ, a, b, c);
            }
            // empty runs closed on both ends and narrower than a hero, along rows and along columns
            var fill = new bool[w * h];
            int maxRun = Mathf.FloorToInt(MaxSlot / Cell);
            for (int z = 0; z < h; z++) Runs(solid, fill, w, h, z * w, 1, w, maxRun);
            for (int x = 0; x < w; x++) Runs(solid, fill, w, h, x, w, h, maxRun);
            // merge the filled cells into rectangles
            int boxes = 0;
            var used = new bool[w * h];
            Transform parent = null;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    if (!fill[z * w + x] || used[z * w + x]) continue;
                    int x1 = x; while (x1 + 1 < w && fill[z * w + x1 + 1] && !used[z * w + x1 + 1]) x1++;
                    int z1 = z;
                    while (z1 + 1 < h && RowFree(fill, used, w, z1 + 1, x, x1)) z1++;
                    for (int zz = z; zz <= z1; zz++) for (int xx = x; xx <= x1; xx++) used[zz * w + xx] = true;
                    if (parent == null) { parent = new GameObject("FootingSeal").transform; parent.SetParent(lot, false); }
                    var box = parent.gameObject.AddComponent<BoxCollider>();
                    box.center = new Vector3(minX + (x + x1 + 1) * 0.5f * Cell, (SealBottom + SealTop) * 0.5f, minZ + (z + z1 + 1) * 0.5f * Cell);
                    box.size = new Vector3((x1 - x + 1) * Cell, SealTop - SealBottom, (z1 - z + 1) * Cell);
                    boxes++;
                }
            return boxes;
        }

        static bool RowFree(bool[] fill, bool[] used, int w, int z, int x0, int x1)
        {
            for (int x = x0; x <= x1; x++) if (!fill[z * w + x] || used[z * w + x]) return false;
            return true;
        }

        // One line of cells (start, stride, count): mark every empty run of 1..maxRun cells that has solid cells at both ends.
        static void Runs(bool[] solid, bool[] fill, int w, int h, int start, int stride, int count, int maxRun)
        {
            int last = -1;   // index (along the line) of the last solid cell
            for (int k = 0; k < count; k++)
            {
                if (!solid[start + k * stride]) continue;
                int gap = k - last - 1;
                if (last >= 0 && gap >= 1 && gap <= maxRun)
                    for (int j = last + 1; j < k; j++) fill[start + j * stride] = true;
                last = k;
            }
        }

        static void Stamp(bool[] solid, int w, int h, float minX, float minZ, Vector3 a, Vector3 b, Vector3 c)
        {
            // edges (vertical faces project to their bottom/top edge lines)
            Line(solid, w, h, minX, minZ, a, b); Line(solid, w, h, minX, minZ, b, c); Line(solid, w, h, minX, minZ, c, a);
            float x0 = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), x1 = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), z0 = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), z1 = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(det) < 1e-6f) return;
            for (int cz = Mathf.Max(0, Mathf.FloorToInt((z0 - minZ) / Cell)); cz <= Mathf.Min(h - 1, Mathf.CeilToInt((z1 - minZ) / Cell)); cz++)
                for (int cx = Mathf.Max(0, Mathf.FloorToInt((x0 - minX) / Cell)); cx <= Mathf.Min(w - 1, Mathf.CeilToInt((x1 - minX) / Cell)); cx++)
                {
                    float px = minX + (cx + 0.5f) * Cell, pz = minZ + (cz + 0.5f) * Cell;
                    float l1 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / det, l2 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / det, l3 = 1f - l1 - l2;
                    if (l1 >= 0f && l2 >= 0f && l3 >= 0f) solid[cz * w + cx] = true;
                }
        }

        static void Line(bool[] solid, int w, int h, float minX, float minZ, Vector3 a, Vector3 b)
        {
            float len = Mathf.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
            int n = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                int cx = Mathf.FloorToInt((Mathf.Lerp(a.x, b.x, t) - minX) / Cell), cz = Mathf.FloorToInt((Mathf.Lerp(a.z, b.z, t) - minZ) / Cell);
                if (cx >= 0 && cx < w && cz >= 0 && cz < h) solid[cz * w + cx] = true;
            }
        }
    }
}
