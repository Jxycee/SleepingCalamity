// T13b rule 3 (earth banks are broken up), the geometry: soil steps. A steep earth face is cut by shelves along its contour lines: each shelf is a level grass tread that comes out of the
// slope 0.3-0.85 m and drops in a ragged, nearly vertical dark-soil riser to the ground below it (roots and rocks sit on top of them). The contours are the iso-lines of the terrain height bent by
// two noise octaves (so the steps wander, thin out, break into dashes and never form straight rows), found by marching squares on a 0.6 m grid over the keep banks, the crag and the hill's west face.
// Geometry, not paint: terrain paint does not show on these 40-70 degree faces (T08c, T13a). One mesh per 16 m cell under Set/BankSteps (generated, externalized by GeneratedMeshes, no collider:
// the terrain collider stays the walkable surface). A partial of HollowmereOrchard for TY, TN, OkBank and Sub; called from BuildBanksT13 before the rocks.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        const float StepX0 = -46f, StepX1 = 46f, StepZ0 = 12f, StepZ1 = 50f, StepGrid = 0.6f, StepH = 0.85f;
        const string StepMatFolder = "Assets/_Project/Content/Materials/Hollowmere";

        sealed class StepCell { public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<Vector4> tan = new List<Vector4>(); public List<Vector2> uv = new List<Vector2>(); public List<int> tread = new List<int>(), riser = new List<int>(); }

        static float StepNoise(float x, float z) => 1.45f * Mathf.PerlinNoise(x * 0.06f + 11f, z * 0.06f + 5f) + 0.55f * Mathf.PerlinNoise(x * 0.21f + 2f, z * 0.21f + 8f) + 0.12f * Mathf.PerlinNoise(x * 0.8f + 6f, z * 0.8f + 1f);   // metres added to the height before the iso-lines are cut
        static uint StepCellHash(Vector2 p) { unchecked { uint h = (uint)(Mathf.FloorToInt(p.x / 1.3f) * 73856093) ^ (uint)(Mathf.FloorToInt(p.y / 1.3f) * 19349663); h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15; return h >> 4; } }
        static float StepWidth(Vector2 p) => Mathf.Lerp(0.22f, 0.62f, Mathf.PerlinNoise(p.x * 0.5f + 9f, p.y * 0.5f + 2f));

        static Material StepMat(string name, string srcName, Color tint)
        {
            Material m = LookTestBuilder.Ensure(StepMatFolder + "/" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit"));
            Material src = AssetDatabase.LoadAssetAtPath<Material>(StepMatFolder + "/" + srcName + ".mat");
            if (src != null) { m.CopyPropertiesFromMaterial(src); foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name); }
            m.SetColor("_BaseColor", tint); m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", 0.05f); m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        static void StepQuad(StepCell c, List<int> tris, Vector3 a, Vector3 b, Vector3 c2, Vector3 d, Vector3 normal, Vector4 tangent, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i0 = c.v.Count;
            c.v.Add(a); c.v.Add(b); c.v.Add(c2); c.v.Add(d);
            for (int k = 0; k < 4; k++) { c.n.Add(normal); c.tan.Add(tangent); }
            c.uv.Add(ua); c.uv.Add(ub); c.uv.Add(uc); c.uv.Add(ud);
            Vector3 fn = Vector3.Cross(b - a, c2 - a);
            if (Vector3.Dot(fn, normal) >= 0f) { tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2); tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3); }
            else { tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1); tris.Add(i0); tris.Add(i0 + 3); tris.Add(i0 + 2); }
        }

        internal static bool BankStepsOn = false;   // T22: shelves off by default (T13 milestone judges rejected them twice)

        // Returns the number of step segments built.
        static int BuildBankSteps(Transform set, TownLayout t)
        {
            Transform root = Sub(set, "BankSteps");
            for (int c = root.childCount - 1; c >= 0; c--) Object.DestroyImmediate(root.GetChild(c).gameObject);
            Material matTread = StepMat("M_HM_BankTread", "M_GD_meadow_grass", new Color(0.52f, 0.60f, 0.34f, 1f)), matRiser = StepMat("M_HM_BankRiser", "M_GD_packed_soil", new Color(0.34f, 0.29f, 0.22f, 1f));
            int nx = (int)((StepX1 - StepX0) / StepGrid) + 1, nz = (int)((StepZ1 - StepZ0) / StepGrid) + 1;
            var F = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = StepX0 + i * StepGrid, z = StepZ0 + j * StepGrid;
                    F[i, j] = TY(new Vector2(x, z)) + StepNoise(x, z);
                }
            var cells = new Dictionary<(int, int), StepCell>();
            int segs = 0;
            var pts = new List<Vector2>(4);
            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                {
                    float f00 = F[i, j], f10 = F[i + 1, j], f11 = F[i + 1, j + 1], f01 = F[i, j + 1];
                    float lo = Mathf.Min(Mathf.Min(f00, f10), Mathf.Min(f11, f01)), hi = Mathf.Max(Mathf.Max(f00, f10), Mathf.Max(f11, f01));
                    float x = StepX0 + i * StepGrid, z = StepZ0 + j * StepGrid;
                    for (int k = Mathf.CeilToInt(lo / StepH) - 1; k <= Mathf.FloorToInt(hi / StepH) + 1; k++)
                    {
                        float L = k * StepH + 0.45f * StepH * (StepCellHash(new Vector2(k * 7.3f, 1.7f)) % 100u / 100f - 0.5f); pts.Clear();   // levels jittered, so no two banks share rows
                        if ((f00 - L) * (f10 - L) < 0f) pts.Add(new Vector2(x + StepGrid * (L - f00) / (f10 - f00), z));
                        if ((f10 - L) * (f11 - L) < 0f) pts.Add(new Vector2(x + StepGrid, z + StepGrid * (L - f10) / (f11 - f10)));
                        if ((f01 - L) * (f11 - L) < 0f) pts.Add(new Vector2(x + StepGrid * (L - f01) / (f11 - f01), z + StepGrid));
                        if ((f00 - L) * (f01 - L) < 0f) pts.Add(new Vector2(x, z + StepGrid * (L - f00) / (f01 - f00)));
                        if (pts.Count != 2) continue;
                        Vector2 a = pts[0], b = pts[1], mid = (a + b) * 0.5f;
                        if ((b - a).sqrMagnitude < 1e-4f) continue;
                        Vector3 nrm = TN(mid);
                        if (nrm.y > 0.72f || nrm.y < 0.30f) continue;   // T13c: 44 to 72 degrees only: a slope that already reads grassy gets no shelves
                        if (mid.x < -30f && nrm.y > 0.55f) continue;   // T13c: HillWest's grassy face under 57 degrees stays one slope
                        if (Mathf.PerlinNoise(mid.x * 0.27f + 5f + k * 13.7f, mid.y * 0.27f + 40f - k * 9.1f) < 0.52f || (StepCellHash(mid + new Vector2(k * 0.61f, k * 1.37f)) % 100u) < 30u) continue;   // T13c: dashes of 0.6-3 m, a different pattern on every level (staggered, never rows)   // ragged dashes, not unbroken rows
                        if (!OkBank(mid, 0.3f) || Tended(mid.x, mid.y) || HollowmereRevetment.Covers(mid.x, mid.y, 0.2f) || HollowmereTerrain.IsPaved(t, mid.x, mid.y)) continue;
                        Vector2 d = new Vector2(nrm.x, nrm.z).normalized;   // downhill
                        float tanT = Mathf.Sqrt(Mathf.Max(0f, 1f - nrm.y * nrm.y)) / Mathf.Max(0.2f, nrm.y), wCap = Mathf.Lerp(0.2f, 0.5f, Mathf.PerlinNoise(mid.x * 0.9f + 17f, mid.y * 0.9f + 3f)) / Mathf.Max(0.3f, tanT);   // the drop below a shelf stays about 0.45 m on any slope
                        float wa = Mathf.Min(StepWidth(a), wCap), wb = Mathf.Min(StepWidth(b), wCap);
                        Vector2 side = new Vector2(-d.y, d.x);
                        Vector2 oa = a + d * wa + side * (Mathf.PerlinNoise(a.x * 2.1f, a.y * 2.1f) - 0.5f) * 0.3f, ob = b + d * wb + side * (Mathf.PerlinNoise(b.x * 2.1f, b.y * 2.1f) - 0.5f) * 0.3f, ia = a - d * 0.14f, ib = b - d * 0.14f;   // T13c: the lip wanders in plan
                        float ya = TY(a) + 0.05f, yb = TY(b) + 0.05f, yoa = ya - 0.02f - 0.05f * Mathf.PerlinNoise(a.x * 1.7f + 4f, a.y * 1.7f), yob = yb - 0.02f - 0.05f * Mathf.PerlinNoise(b.x * 1.7f + 4f, b.y * 1.7f);
                        float ga = TY(oa), gb = TY(ob);
                        if (yoa - ga < 0.10f && yob - gb < 0.10f) continue;   // no drop below the shelf: nothing to build
                        float ba = Mathf.Min(yoa - 0.10f, ga - 0.14f), bb = Mathf.Min(yob - 0.10f, gb - 0.14f);   // riser feet sink into the ground
                        var key = (Mathf.FloorToInt((mid.x - StepX0) / 16f), Mathf.FloorToInt((mid.y - StepZ0) / 16f));
                        if (!cells.TryGetValue(key, out StepCell cell)) cells[key] = cell = new StepCell();
                        Vector2 tv = (b - a).normalized;
                        Vector4 tTan = new Vector4(tv.x, 0f, tv.y, 1f);
                        StepQuad(cell, cell.tread, new Vector3(ia.x, ya - 0.04f, ia.y), new Vector3(ib.x, yb - 0.04f, ib.y), new Vector3(ob.x, yob, ob.y), new Vector3(oa.x, yoa, oa.y), Vector3.up, tTan,
                            ia * 0.5f, ib * 0.5f, ob * 0.5f, oa * 0.5f);
                        float lipDrop = 0.10f + 0.06f * Mathf.PerlinNoise(mid.x * 2.3f, mid.y * 2.3f);   // T13c: a grass lip rolls over the top of the face and hides the hard edge
                        Vector2 la = oa + d * 0.07f, lb = ob + d * 0.07f;
                        StepQuad(cell, cell.tread, new Vector3(oa.x, yoa, oa.y), new Vector3(ob.x, yob, ob.y), new Vector3(lb.x, yob - lipDrop, lb.y), new Vector3(la.x, yoa - lipDrop, la.y), (new Vector3(d.x, 0.5f, d.y)).normalized, tTan,
                            oa * 0.5f, ob * 0.5f, ob * 0.5f, oa * 0.5f);
                        Vector3 rn = new Vector3(d.x * 0.93f, 0.28f, d.y * 0.93f).normalized;
                        float ua = Vector2.Dot(a, tv) * 0.6f, ub = Vector2.Dot(b, tv) * 0.6f;
                                                // the riser in two bands with a bulge between them (soil lips are never planes): the middle row sits 4-11 cm out and 45% down
                        float bulge = 0.04f + 0.14f * Mathf.PerlinNoise(mid.x * 1.3f + 3f, mid.y * 1.3f + 8f);
                        Vector2 ma = oa + d * bulge, mb = ob + d * bulge;
                        float yma = Mathf.Lerp(yoa, ba, 0.45f), ymb = Mathf.Lerp(yob, bb, 0.45f);
                        StepQuad(cell, cell.riser, new Vector3(oa.x, yoa, oa.y), new Vector3(ob.x, yob, ob.y), new Vector3(mb.x, ymb, mb.y), new Vector3(ma.x, yma, ma.y), (rn + Vector3.up * 0.35f).normalized, tTan,
                            new Vector2(ua, yoa * 0.6f), new Vector2(ub, yob * 0.6f), new Vector2(ub, ymb * 0.6f), new Vector2(ua, yma * 0.6f));
                        StepQuad(cell, cell.riser, new Vector3(ma.x, yma, ma.y), new Vector3(mb.x, ymb, mb.y), new Vector3(ob.x, bb, ob.y), new Vector3(oa.x, ba, oa.y), rn, tTan,
                            new Vector2(ua, yma * 0.6f), new Vector2(ub, ymb * 0.6f), new Vector2(ub, bb * 0.6f), new Vector2(ua, ba * 0.6f));
                        segs++;
                    }
                }
            foreach (var kv in cells)
            {
                StepCell c = kv.Value;
                var mesh = new Mesh { name = "BankSteps_" + kv.Key.Item1 + "_" + kv.Key.Item2, indexFormat = c.v.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(c.v); mesh.SetNormals(c.n); mesh.SetTangents(c.tan); mesh.SetUVs(0, c.uv);
                mesh.subMeshCount = 2; mesh.SetTriangles(c.tread, 0); mesh.SetTriangles(c.riser, 1); mesh.RecalculateBounds();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { matTread, matRiser };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                go.isStatic = true;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SC HOLLOWMERE bank steps (T13b): segments " + segs + " in " + cells.Count + " meshes");
            return segs;
        }
    }
}
