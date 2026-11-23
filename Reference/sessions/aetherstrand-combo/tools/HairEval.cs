// Scratch evaluator: how well does fitted hair sit on a character head?
// Metrics (all in metres, head-bone local space): vertices inside the head surface (penetration), root-ring gap to the scalp.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class HairEval
{
    public class Result
    {
        public int hairVerts, headVerts;
        public float penetratingPct, maxDepthMm, meanDepthMm;
        public float rootGapMeanMm, rootGapP95Mm, rootGapMaxMm, rootPenetratingPct;
        public override string ToString()
        {
            return "verts=" + hairVerts + " penetrating=" + penetratingPct.ToString("F2") + "% depth(mean/max mm)=" + meanDepthMm.ToString("F1") + "/" + maxDepthMm.ToString("F1")
                 + " rootGap(mean/p95/max mm)=" + rootGapMeanMm.ToString("F1") + "/" + rootGapP95Mm.ToString("F1") + "/" + rootGapMaxMm.ToString("F1") + " rootPen=" + rootPenetratingPct.ToString("F1") + "%";
        }
    }

    public static Result Evaluate(GameObject character, Transform head, Mesh hairMeshInHeadSpace, int vertsPerLock = 78, int ringVerts = 7)
    {
        // head surface in head-bone space (rest pose)
        var hp = new List<Vector3>(8192); var hn = new List<Vector3>(8192);
        foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string n = smr.name.ToLowerInvariant();
            if (!n.Contains("head") || n.Contains("hair")) continue;
            var mesh = smr.sharedMesh; int hi = System.Array.IndexOf(smr.bones, head);
            if (hi < 0) continue;
            var bp = mesh.bindposes[hi]; var v = mesh.vertices; var nr = mesh.normals;
            var w = mesh.GetAllBoneWeights(); var npv = mesh.GetBonesPerVertex(); int wi = 0;
            for (int i = 0; i < v.Length; i++)
            {
                float ww = 0; for (int k = 0; k < npv[i]; k++) if (w[wi + k].boneIndex == hi) ww += w[wi + k].weight; wi += npv[i];
                if (ww > 0.5f) { hp.Add(bp.MultiplyPoint3x4(v[i])); hn.Add(bp.MultiplyVector(nr[i]).normalized); }
            }
        }
        var res = new Result { headVerts = hp.Count };
        if (hp.Count == 0) return res;
        // convex hull collider of the head (head-bone space object), inside test + depth along the radial ray
        var meshH = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        meshH.SetVertices(hp);
        int nt = (hp.Count / 3) * 3; var tri = new int[nt]; for (int i = 0; i < nt; i++) tri[i] = i;
        meshH.SetTriangles(tri, 0);
        var goH = new GameObject("__hairEvalHead"); goH.transform.SetParent(head, false);
        var mc = goH.AddComponent<MeshCollider>(); mc.sharedMesh = meshH; mc.convex = true;
        Vector3 centre = Vector3.zero; foreach (var q in hp) centre += q; centre /= hp.Count;
        System.Func<Vector3, float> signedDist = (pLocal) =>
        {
            Vector3 w = head.TransformPoint(pLocal);
            Vector3 cp = mc.ClosestPoint(w);
            float d = Vector3.Distance(cp, w);
            if (d > 1e-6f) return d;
            // inside: depth to the surface along the radial direction from the head centre
            Vector3 c = head.TransformPoint(centre); Vector3 dir = (w - c).normalized; if (dir.sqrMagnitude < 1e-8f) return -0.05f;
            Vector3 origin = w + dir * 0.6f; Ray ray = new Ray(origin, -dir); RaycastHit hit;
            if (mc.Raycast(ray, out hit, 1.2f)) return -Mathf.Max(0f, (w - hit.point).magnitude);
            return -0.001f;
        };

        var verts = hairMeshInHeadSpace.vertices; res.hairVerts = verts.Length;
        int pen = 0; float depthSum = 0, depthMax = 0;
        var rootGaps = new List<float>(); int rootPen = 0, roots = 0;
        int locks = verts.Length / vertsPerLock;
        for (int i = 0; i < verts.Length; i++)
        {
            float sd = signedDist(verts[i]);
            if (sd < -0.0015f) { pen++; float d = -sd; depthSum += d; if (d > depthMax) depthMax = d; }
        }
        for (int l = 0; l < locks; l++)
        {
            Vector3 root = Vector3.zero; for (int r = 0; r < ringVerts - 1; r++) root += verts[l * vertsPerLock + r]; root /= (ringVerts - 1);
            float sd = signedDist(root); roots++; if (sd < 0) rootPen++; rootGaps.Add(sd);
        }
        rootGaps.Sort();
        res.penetratingPct = 100f * pen / Mathf.Max(1, verts.Length);
        res.meanDepthMm = pen > 0 ? 1000f * depthSum / pen : 0; res.maxDepthMm = 1000f * depthMax;
        float sum = 0; foreach (var g in rootGaps) sum += g;
        res.rootGapMeanMm = 1000f * sum / Mathf.Max(1, rootGaps.Count);
        res.rootGapP95Mm = rootGaps.Count > 0 ? 1000f * rootGaps[Mathf.Min(rootGaps.Count - 1, (int)(rootGaps.Count * 0.95f))] : 0;
        res.rootGapMaxMm = rootGaps.Count > 0 ? 1000f * rootGaps[rootGaps.Count - 1] : 0;
        res.rootPenetratingPct = 100f * rootPen / Mathf.Max(1, roots);
        Object.DestroyImmediate(goH); Object.DestroyImmediate(meshH);
        return res;
    }
}
