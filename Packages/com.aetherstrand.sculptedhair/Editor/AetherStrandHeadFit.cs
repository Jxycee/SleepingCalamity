#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// Deterministic fit of the web hair (built around the web's stylised bust head) onto any skinned character head.
    /// Measures the character's head-bone-weighted head mesh in head-bone space and maps the web head's landmarks
    /// (chin-to-crown height, cranium width, cranium depth) onto it with a per-axis scale + offset.
    /// Same landmark definition is measured on both heads: the upper 45% of the head mesh (above 0.55 of chin-to-crown).
    /// </summary>
    public static class AetherStrandHeadFit
    {
        // Web bust head, head-pivot-local (measured from createStylizedBust, mesh 0): chin y=0, crown y=0.742,
        // upper-region cranium x in [-0.2716,0.2716], z in [-0.3052,0.3052] (centre 0).
        public const float WebHeight = 0.742f;
        public const float WebWidth = 0.5432f;
        public const float WebDepth = 0.6104f;
        public const float WebChinY = 0.0f;
        public const float WebDepthCentreZ = 0.0f;
        // Web hair scalp-ellipsoid centre (HEAD_CENTER_LOCAL), pivot-local.
        public static readonly Vector3 WebHeadCenter = new Vector3(0f, 0.425f, -0.01f);

        public struct Fit
        {
            public Vector3 scale;
            public Vector3 offset;
            public float height, width, depth, chinY;
        }

        /// <summary>Applies the fit to a point given in web head-pivot-local space with X already mirrored (Unity head-local convention).</summary>
        public static Vector3 Apply(Fit f, Vector3 p) { return Vector3.Scale(p, f.scale) + f.offset; }

        public static bool TryCompute(GameObject characterRoot, Transform headBone, out Fit fit)
        {
            fit = default(Fit);
            if (characterRoot == null || headBone == null) return false;

            var pts = new List<Vector3>(8192);
            var smrs = characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // Prefer renderers whose name says "head"; fall back to everything weighted to the head bone.
            for (int pass = 0; pass < 2 && pts.Count < 200; pass++)
            {
                pts.Clear();
                foreach (var smr in smrs)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    string n = smr.name.ToLowerInvariant();
                    if (n.Contains("hair") || n.Contains("lash") || n.Contains("teeth") || n.Contains("eye")) continue;
                    if (pass == 0 && !n.Contains("head")) continue;
                    CollectHeadWeighted(smr, headBone, pts);
                }
            }
            if (pts.Count < 200) return false;

            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var p in pts) { if (p.y < yMin) yMin = p.y; if (p.y > yMax) yMax = p.y; }
            float h = yMax - yMin;
            if (h < 0.05f) return false;
            float cut = yMin + 0.55f * h;
            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue; int n2 = 0;
            foreach (var p in pts)
            {
                if (p.y < cut) continue;
                n2++;
                if (p.x < xMin) xMin = p.x; if (p.x > xMax) xMax = p.x;
                if (p.z < zMin) zMin = p.z; if (p.z > zMax) zMax = p.z;
            }
            if (n2 < 50) return false;

            float width = xMax - xMin, depth = zMax - zMin;
            var scale = new Vector3(width / WebWidth, h / WebHeight, depth / WebDepth);
            // keep the temples from pinching: X scale never below 94% of the larger of Y/Z (judged on the renders, Neon round 2)
            scale.x = Mathf.Max(scale.x, 0.94f * Mathf.Max(scale.y, scale.z));
            var centre = new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);
            fit.scale = scale;
            fit.offset = new Vector3(centre.x, yMin - WebChinY * scale.y, centre.z - WebDepthCentreZ * scale.z);
            fit.height = h; fit.width = width; fit.depth = depth; fit.chinY = yMin;
            return true;
        }

        /// <summary>Head-bone-weighted head surface points in head-bone space (rest pose); empty if the character has no head mesh.</summary>
        public static List<Vector3> HeadPoints(GameObject characterRoot, Transform headBone)
        {
            var pts = new List<Vector3>(8192);
            if (characterRoot == null || headBone == null) return pts;
            var smrs = characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int pass = 0; pass < 2 && pts.Count < 200; pass++)
            {
                pts.Clear();
                foreach (var smr in smrs)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    string n = smr.name.ToLowerInvariant();
                    if (n.Contains("hair") || n.Contains("lash") || n.Contains("teeth") || n.Contains("eye")) continue;
                    if (pass == 0 && !n.Contains("head")) continue;
                    CollectHeadWeighted(smr, headBone, pts);
                }
            }
            return pts;
        }

        /// <summary>
        /// Pushes points (head-bone space) out of the character's head (convex hull of the head mesh) to at least <paramref name="margin"/> metres.
        /// Returns the number of points moved. Used after the fit so hair never intersects the real head surface.
        /// </summary>
        /// <summary>Reduces a point cloud to its extreme point along <paramref name="dirs"/> Fibonacci directions (hull of the result approximates the full hull, under Unity's convex polygon cap).</summary>
        public static List<Vector3> ExtremePoints(List<Vector3> pts, int dirs = 120)
        {
            var c = Vector3.zero; foreach (var q in pts) c += q; c /= Mathf.Max(1, pts.Count);
            var picked = new HashSet<int>(); float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < dirs; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / dirs, r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), th = ga * i;
                var d = new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
                int best = -1; float bd = float.MinValue;
                for (int k = 0; k < pts.Count; k++) { float v = Vector3.Dot(pts[k] - c, d); if (v > bd) { bd = v; best = k; } }
                if (best >= 0) picked.Add(best);
            }
            var res = new List<Vector3>(picked.Count); foreach (int k in picked) res.Add(pts[k]);
            return res;
        }

        public static int PushOutOfHead(GameObject characterRoot, Transform headBone, IList<Vector3[]> pointSets, float margin, IList<float> margins = null, IList<bool> pullRoots = null, float rootGapMax = 0.009f, float rootGapKeep = 0.007f, float hugStrength = 0f, float hugStart = 0.008f)
        {
            var hp = HeadPoints(characterRoot, headBone);
            if (hp.Count < 200) return 0;
            {
                // exclude the nose protrusion from the hull so bangs are not pushed off the brow in profile
                float yMin = float.MaxValue, yMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
                foreach (var q in hp) { yMin = Mathf.Min(yMin, q.y); yMax = Mathf.Max(yMax, q.y); zMin = Mathf.Min(zMin, q.z); zMax = Mathf.Max(zMax, q.z); }
                float hh = yMax - yMin, dd = zMax - zMin, zc = (zMin + zMax) * 0.5f;
                hp.RemoveAll(q => q.y < yMin + 0.58f * hh && q.z > zc + 0.35f * dd);
                if (hp.Count < 200) return 0;
            }
            hp = ExtremePoints(hp, 120);
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(hp);
            int nt = (hp.Count / 3) * 3; var tri = new int[nt]; for (int i = 0; i < nt; i++) tri[i] = i;
            mesh.SetTriangles(tri, 0);
            var go = new GameObject("__hairPushOutHead"); go.transform.SetParent(headBone, false);
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
            Vector3 centre = Vector3.zero; foreach (var q in hp) centre += q; centre /= hp.Count;
            Vector3 centreW = headBone.TransformPoint(centre);
            int moved = 0;
            int setIdx = -1;
            foreach (var set in pointSets)
            {
                setIdx++;
                float mg = margins != null && setIdx < margins.Count ? margins[setIdx] : margin;
                for (int i = 0; i < set.Length; i++)
                {
                    Vector3 w = headBone.TransformPoint(set[i]);
                    Vector3 cp = mc.ClosestPoint(w); float d = Vector3.Distance(cp, w);
                    Vector3 target = w; bool move = false;
                    if (d > 1e-6f)
                    {
                        if (d < mg) { target = w + (w - cp).normalized * (mg - d); move = true; }
                    }
                    else
                    {
                        Vector3 dir = (w - centreW).normalized;
                        var ray = new Ray(w + dir * 0.6f, -dir);
                        if (dir.sqrMagnitude > 1e-8f && mc.Raycast(ray, out RaycastHit hit, 1.2f)) { target = hit.point + dir * mg; move = true; }
                    }
                    if (move) { set[i] = headBone.InverseTransformPoint(target); moved++; }
                }
                // roots that float too far above the scalp are pulled back toward it (like the web's root pull-in), keeping a small gap
                if (pullRoots != null && setIdx < pullRoots.Count && pullRoots[setIdx] && set.Length >= 3)
                {
                    Vector3 w0 = headBone.TransformPoint(set[0]); Vector3 cp0 = mc.ClosestPoint(w0); float d0 = Vector3.Distance(cp0, w0);
                    if (d0 > rootGapMax)
                    {
                        Vector3 dir0 = (cp0 - w0).normalized; float pull = d0 - rootGapKeep;
                        set[0] = headBone.InverseTransformPoint(w0 + dir0 * pull);
                        set[1] = headBone.InverseTransformPoint(headBone.TransformPoint(set[1]) + dir0 * pull * 0.55f);
                        set[2] = headBone.InverseTransformPoint(headBone.TransformPoint(set[2]) + dir0 * pull * 0.22f);
                        moved++;
                    }
                }
            }
            // skull hug: pulls cranium-level points that stand off the head back toward it (crops, buns, braids that read as a helmet)
            if (hugStrength > 0.001f)
            {
                float yLo = float.MaxValue, yHi = float.MinValue; foreach (var q in hp) { yLo = Mathf.Min(yLo, q.y); yHi = Mathf.Max(yHi, q.y); }
                float yCut = yLo + 0.4f * (yHi - yLo);
                foreach (var set in pointSets)
                {
                    for (int i = 0; i < set.Length; i++)
                    {
                        if (set[i].y < yCut) continue;
                        Vector3 w = headBone.TransformPoint(set[i]);
                        Vector3 cp = mc.ClosestPoint(w); float d = Vector3.Distance(cp, w);
                        if (d <= hugStart || d > 0.07f) continue;
                        float nd = hugStart + (d - hugStart) * (1f - Mathf.Clamp01(hugStrength));
                        set[i] = headBone.InverseTransformPoint(cp + (w - cp).normalized * nd); moved++;
                    }
                }
            }
            Object.DestroyImmediate(go); Object.DestroyImmediate(mesh);
            return moved;
        }

        private static void CollectHeadWeighted(SkinnedMeshRenderer smr, Transform headBone, List<Vector3> outPts)
        {
            var mesh = smr.sharedMesh;
            var bones = smr.bones;
            int hi = System.Array.IndexOf(bones, headBone);
            if (hi < 0 || hi >= mesh.bindposes.Length) return;
            var bindpose = mesh.bindposes[hi];
            var weights = mesh.GetAllBoneWeights();
            var perVertex = mesh.GetBonesPerVertex();
            var verts = mesh.vertices;
            int wi = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                float w = 0f;
                for (int k = 0; k < perVertex[i]; k++)
                    if (weights[wi + k].boneIndex == hi) w += weights[wi + k].weight;
                wi += perVertex[i];
                if (w > 0.5f) outPts.Add(bindpose.MultiplyPoint3x4(verts[i]));
            }
        }
    }
}
#endif
