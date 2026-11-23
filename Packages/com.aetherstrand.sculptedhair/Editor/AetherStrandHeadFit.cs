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
            var centre = new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);
            fit.scale = scale;
            fit.offset = new Vector3(centre.x, yMin - WebChinY * scale.y, centre.z - WebDepthCentreZ * scale.z);
            fit.height = h; fit.width = width; fit.depth = depth; fit.chinY = yMin;
            return true;
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
