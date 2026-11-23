#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AetherStrand.Editor
{
    [Serializable]
    public class HairSculptParams
    {
        public float overallScale = 1.0f;
        public Vector3 headLocalOffset = Vector3.zero;
        public float bangsLength = 0.88f;
        public float layerLength = 0.96f;
        public float widthScale = 1.0f;
        public float thicknessScale = 1.0f;
        public int targetChainCount = 44;
        public int bonesPerChain = 5;
        public bool autoFitToHeadMesh = true;
        /// <summary>Legacy: rescale by 0.23/maxZ when the JSON is in web-bust units. False = use overallScale only (parity mode).</summary>
        public bool legacyAutoNormalize = true;
    }

    /// <summary>
    /// Pure-Unity deterministic sculpted volumetric hair mesh & clustered bone-chain generator.
    /// Reads PresetGeometry.json directly in C# (no Blender round-trip required) and builds:
    ///   - 3D crescent-profile volumetric locks with root-to-tip UVs and linear vertex-color masks
    ///   - Rigid scalp base-shell weighted 100% to Head (prevents crown loops)
    ///   - Per-zone k-means clustered Hair_<Zone>_<k>_<i> bone chains parented to Head
    ///   - Smooth multi-bone skin weights with root ring 0 pinned 100% to Head
    /// </summary>
    public static class AetherStrandMeshGenerator
    {
        // Sleek crescent sculpted cross-section profile (matches AetherStrand_Hair.shader UVs 0.34/0.66)
        private static readonly float[] ProfileU = { 0.00f, 0.18f, 0.36f, 0.50f, 0.64f, 0.82f, 1.00f };
        private static readonly float[] ProfileX = { 0.00f, 0.76f, 1.00f, 0.00f, -1.00f, -0.76f, 0.00f };
        private static readonly float[] ProfileY = { 1.00f, 0.54f, -0.02f, -0.26f, -0.02f, 0.54f, 1.00f };

        private class LockSpec
        {
            public string zone;
            public float width;
            public float thickness;
            public float seed;
            public float layerAO;
            public bool isBaseShell;
            public float scalpRoot = 0.65f;
            public bool lockNormal;
            public Vector3 rootNormal = Vector3.up;
            public Vector3[] points; // Unity Head-local space (+X right, +Y up, +Z forward)
        }

                public static Mesh BuildStaticPreviewMeshFromJson(string jsonPath, float scale, Vector3 offset)
        {
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)) return null;
            var p = new HairSculptParams { overallScale = scale, headLocalOffset = offset, bangsLength = 1f, layerLength = 1f, widthScale = 1f, thicknessScale = 1f };
            List<LockSpec> locks = LoadLocksFromJson(jsonPath, p);
            if (locks.Count == 0) return null;
            return BuildVolumetricMesh(locks, new Dictionary<int, int[]>(), 1);
        }

        public static SkinnedMeshRenderer GenerateAndAttach(
            GameObject characterRoot,
            HairSculptParams p,
            string presetGeometryJsonPath,
            string meshAssetPath,
            Material hairMaterial)
        {
            if (characterRoot == null)
            {
                Debug.LogError("[AetherStrand] GenerateAndAttach requires a Character Root.");
                return null;
            }

            Transform headBone = ResolveHeadBone(characterRoot);
            if (headBone == null)
            {
                Debug.LogError("[AetherStrand] Could not find a Humanoid Head bone or 'Head' child under Character Root.");
                return null;
            }

            if (string.IsNullOrEmpty(presetGeometryJsonPath) || !File.Exists(presetGeometryJsonPath))
            {
                presetGeometryJsonPath = ResolveDefaultPresetGeometryPath();
            }

            if (string.IsNullOrEmpty(presetGeometryJsonPath) || !File.Exists(presetGeometryJsonPath))
            {
                Debug.LogError("[AetherStrand] PresetGeometry.json not found in package Presets/ folder.");
                return null;
            }

            List<LockSpec> locks = LoadLocksFromJson(presetGeometryJsonPath, p);
            if (locks.Count == 0)
            {
                Debug.LogError("[AetherStrand] PresetGeometry.json contained 0 valid locks.");
                return null;
            }

            if (p.autoFitToHeadMesh && p.headLocalOffset == Vector3.zero)
            {
                AutoSeatLocksToHeadMesh(characterRoot, headBone, locks);
            }

            // 1. Clean previous generated rig & mesh object under Head / characterRoot
            RemovePreviousGeneratedHair(characterRoot, headBone);

            // 2. Cluster outer (non-base-shell) locks per zone into shared bone chains
            int bonesPerChain = Mathf.Clamp(p.bonesPerChain, 3, 6);
            int targetChains = Mathf.Clamp(p.targetChainCount, 12, 80);

            var allBones = new List<Transform> { headBone }; // index 0 = Head
            var lockToChainBoneIndices = new Dictionary<int, int[]>();

            string[] zoneOrder = { "Bangs", "Crown", "Sides", "Rear" };
            var zoneIndices = new Dictionary<string, List<int>>();
            foreach (string z in zoneOrder) zoneIndices[z] = new List<int>();

            int totalOuter = 0;
            for (int i = 0; i < locks.Count; i++)
            {
                if (locks[i].isBaseShell) continue;
                string z = NormalizeZone(locks[i].zone);
                zoneIndices[z].Add(i);
                totalOuter++;
            }

            // Allocate chain budget proportionally per zone
            foreach (string zone in zoneOrder)
            {
                List<int> members = zoneIndices[zone];
                if (members.Count == 0) continue;
                int kZone = Mathf.Max(1, Mathf.RoundToInt((float)members.Count / Mathf.Max(1, totalOuter) * targetChains));
                kZone = Mathf.Min(kZone, members.Count);

                List<List<int>> clusters = ClusterDeterministically(locks, members, kZone);

                // Sort clusters deterministically by root azimuth & height
                clusters.Sort((a, b) =>
                {
                    Vector3 ra = MeanPoint(locks, a, 0);
                    Vector3 rb = MeanPoint(locks, b, 0);
                    float angA = Mathf.Atan2(ra.x, ra.z);
                    float angB = Mathf.Atan2(rb.x, rb.z);
                    int cmp = angA.CompareTo(angB);
                    return cmp != 0 ? cmp : rb.y.CompareTo(ra.y);
                });

                for (int c = 0; c < clusters.Count; c++)
                {
                    List<int> cl = clusters[c];
                    Vector3[] chainSpine = BuildMeanSpine(locks, cl, bonesPerChain + 1);
                    int[] boneIndices = new int[bonesPerChain];

                    Transform parentT = headBone;
                    for (int b = 0; b < bonesPerChain; b++)
                    {
                        var bGo = new GameObject($"Hair_{zone}_{c:00}_{b}");
                        Transform bt = bGo.transform;
                        bt.SetParent(parentT, false);
                        bt.position = headBone.TransformPoint(chainSpine[b]);

                        Vector3 segDirW = headBone.TransformPoint(chainSpine[b + 1]) - bt.position;
                        if (segDirW.sqrMagnitude > 1e-8f)
                            bt.rotation = Quaternion.LookRotation(segDirW.normalized, headBone.up);
                        else
                            bt.rotation = parentT.rotation;

                        bt.localScale = Vector3.one;
                        int boneIdx = allBones.Count;
                        allBones.Add(bt);
                        boneIndices[b] = boneIdx;
                        parentT = bt;
                    }

                    foreach (int lockIdx in cl)
                        lockToChainBoneIndices[lockIdx] = boneIndices;
                }
            }

            // 3. Create / reuse SkinnedMeshRenderer GameObject under characterRoot
            var hairGo = new GameObject("AetherStrand_HairMesh");
            hairGo.transform.SetParent(characterRoot.transform, false);
            hairGo.transform.position = headBone.position;
            hairGo.transform.rotation = headBone.rotation;
            hairGo.transform.localScale = Vector3.one;

            // 4. Build crescent-profile 3D Mesh in hairGo local space (= headBone local space)
            Mesh mesh = BuildVolumetricMesh(locks, lockToChainBoneIndices, allBones.Count);
            mesh.name = "AetherStrand_SculptedHair";

            // Compute bindposes relative to hairGo
            var bindposes = new Matrix4x4[allBones.Count];
            Matrix4x4 hairLocalToWorld = hairGo.transform.localToWorldMatrix;
            for (int i = 0; i < allBones.Count; i++)
            {
                bindposes[i] = allBones[i].worldToLocalMatrix * hairLocalToWorld;
            }
            mesh.bindposes = bindposes;

            // Save mesh in-place as a Unity asset so prefab/scene serialization stays deterministic
            Mesh savedMesh = SaveMeshInPlace(mesh, meshAssetPath);

            var smr = hairGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = savedMesh;
            smr.bones = allBones.ToArray();
            smr.rootBone = headBone;
            smr.sharedMaterial = hairMaterial;
            smr.updateWhenOffscreen = false;
            smr.localBounds = savedMesh.bounds;

            EditorUtility.SetDirty(characterRoot);
            Debug.Log($"[AetherStrand] Generated {locks.Count} volumetric locks ({savedMesh.triangles.Length / 3} tris) on {allBones.Count - 1} Hair_ bones under '{headBone.name}'.");
            return smr;
        }

        private static void AutoSeatLocksToHeadMesh(GameObject characterRoot, Transform headBone, List<LockSpec> locks)
        {
            if (characterRoot == null || headBone == null || locks == null || locks.Count == 0) return;

            Matrix4x4 worldToHairFrame = Matrix4x4.TRS(headBone.position, headBone.rotation, Vector3.one).inverse;
            float skullMaxY = float.NegativeInfinity;
            int skullVertCount = 0;

            foreach (Renderer rend in characterRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == null) continue;
                string rName = rend.name;
                if (rName.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rName.IndexOf("AetherStrand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rName.StartsWith("AS_Col_", StringComparison.Ordinal))
                    continue;

                Mesh tempBaked = null;
                Mesh meshToRead = null;
                if (rend is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    tempBaked = new Mesh();
                    smr.BakeMesh(tempBaked, false);
                    meshToRead = tempBaked;
                }
                else if (rend.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                {
                    meshToRead = mf.sharedMesh;
                }

                if (meshToRead != null && meshToRead.vertexCount > 0)
                {
                    Matrix4x4 toFrame = worldToHairFrame * rend.transform.localToWorldMatrix;
                    Vector3[] verts = meshToRead.vertices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 pt = toFrame.MultiplyPoint3x4(verts[i]);
                        if (Mathf.Abs(pt.x) > 0.16f || pt.y < -0.14f || pt.y > 0.32f || Mathf.Abs(pt.z) > 0.19f)
                            continue;
                        if (pt.y > skullMaxY) skullMaxY = pt.y;
                        skullVertCount++;
                    }
                }

                if (tempBaked != null) UnityEngine.Object.DestroyImmediate(tempBaked);
            }

            if (skullVertCount < 30 || float.IsNegativeInfinity(skullMaxY)) return;

            float hairMaxY = float.NegativeInfinity;
            for (int i = 0; i < locks.Count; i++)
            {
                Vector3[] pts = locks[i].points;
                for (int k = 0; k < pts.Length; k++)
                {
                    if (pts[k].y > hairMaxY) hairMaxY = pts[k].y;
                }
            }
            if (float.IsNegativeInfinity(hairMaxY)) return;

            float clearance = hairMaxY - skullMaxY;
            // Seat crown to +2.2 cm above actual skull top whenever clearance floats > 3.2 cm or sinks < 0.8 cm
            if (clearance > 0.032f || clearance < 0.008f)
            {
                float targetCrownY = skullMaxY + 0.022f;
                float deltaY = Mathf.Clamp(targetCrownY - hairMaxY, -0.14f, 0.10f);
                Vector3 shift = new Vector3(0f, deltaY, 0f);
                for (int i = 0; i < locks.Count; i++)
                {
                    Vector3[] pts = locks[i].points;
                    for (int k = 0; k < pts.Length; k++) pts[k] += shift;
                }
                Debug.Log($"[AetherStrand] Auto-seated crown to skull top (skullMaxY={skullMaxY:F4}m, hairMaxY={hairMaxY:F4}m -> {hairMaxY + deltaY:F4}m, deltaY={deltaY:F4}m).");
            }
        }

        // ---- Exact port of AetherStrand web ring construction (hairPhysics.ts updateGeometry) ----
        // Frames are computed in WEB space (x mirrored vs Unity head-local) so cross products match, then x is mirrored back.
        private const int NodesPerLock = 12;
        private static readonly float[] PU = { 0.0f, 0.17f, 0.34f, 0.5f, 0.66f, 0.83f, 1.0f };
        private static readonly float[] PX = { 0.0f, 0.76f, 1.0f, 0.0f, -1.0f, -0.76f, 0.0f };
        private static readonly float[] PY = { 1.0f, 0.42f, -0.32f, -0.45f, -0.32f, 0.42f, 1.0f };
        private static readonly float[] PNX = { 0.0f, 0.64409f, 0.94451f, 0.0f, -0.94451f, -0.64409f, 0.0f };
        private static readonly float[] PNY = { 1.0f, 0.76495f, -0.32849f, -1.0f, -0.32849f, 0.76495f, 1.0f };
        // Web HEAD_CENTER_LOCAL (0,0.425,-0.01): points are already head-pivot-local, so use it as is.
        private static readonly Vector3 WebHeadCenterLocal = new Vector3(0f, 0.425f, -0.01f);

        private static Mesh BuildVolumetricMesh(
            List<LockSpec> locks,
            Dictionary<int, int[]> lockToChainBoneIndices,
            int totalBoneCount)
        {
            int ringVerts = PX.Length; // 7 (seam duplicated)
            var verts = new List<Vector3>(locks.Count * 80);
            var norms = new List<Vector3>(locks.Count * 80);
            var tans = new List<Vector4>(locks.Count * 80);
            var uvs = new List<Vector2>(locks.Count * 80);
            var uv2 = new List<Vector2>(locks.Count * 80);
            var colors = new List<Color>(locks.Count * 80);
            var weights = new List<BoneWeight>(locks.Count * 80);
            var tris = new List<int>(locks.Count * 140);

            for (int lIdx = 0; lIdx < locks.Count; lIdx++)
            {
                LockSpec lk = locks[lIdx];
                Vector3[] pts = lk.points;
                int count = pts.Length;
                if (count < 2) continue;
                int ringsPerLock = count - 1;
                int baseVert = verts.Count;
                float maskVal = lk.isBaseShell ? 0.0f : 1.0f;
                int[] chainBones = null;
                bool isDynamic = !lk.isBaseShell && lockToChainBoneIndices.TryGetValue(lIdx, out chainBones) && chainBones.Length >= 2;

                // web-space copies
                var w = new Vector3[count];
                for (int i = 0; i < count; i++) w[i] = new Vector3(-pts[i].x, pts[i].y, pts[i].z);
                Vector3 hc = new Vector3(-WebHeadCenterLocal.x, WebHeadCenterLocal.y, WebHeadCenterLocal.z);

                bool isBase = lk.isBaseShell;
                float rootMinW = lk.scalpRoot >= 0.5f ? 0.36f : 0.08f;
                float effThick = isBase ? 1.0f : 0.9f; // web effThickScale at strandWidthScale 1
                float baseHalfW = lk.width * 0.5f;
                float baseHalfT = lk.thickness * 0.5f * effThick;

                Vector3 od = new Vector3(-lk.rootNormal.x, lk.rootNormal.y, lk.rootNormal.z).normalized;
                if (od.sqrMagnitude < 1e-6f) od = Vector3.up;
                Vector3 o = Vector3.up;

                for (int k = 0; k < ringsPerLock; k++)
                {
                    float t = (float)k / (NodesPerLock - 1);
                    Vector3 p = w[k];
                    Vector3 tang = (k == 0 ? (w[1] - p) : (w[k + 1] - w[k - 1]));
                    tang = tang.sqrMagnitude < 1e-12f ? Vector3.down : tang.normalized;
                    Vector3 hn = (p - hc); hn = hn.sqrMagnitude < 1e-12f ? Vector3.up : hn.normalized;

                    Vector3 s;
                    if (!lk.lockNormal)
                    {
                        s = Vector3.Cross(tang, hn);
                        if (s.sqrMagnitude >= 0.0025f) od = hn;
                        else
                        {
                            od = (od + (hn - od) * 0.5f).normalized;
                            s = Vector3.Cross(tang, od);
                        }
                    }
                    else s = Vector3.Cross(tang, od);
                    s = s.sqrMagnitude < 0.0001f ? Vector3.right : s.normalized;
                    o = Vector3.Cross(s, tang);

                    float rSin = Mathf.Sin(Mathf.Min(1f, t * 4.2f) * Mathf.PI * 0.5f);
                    float rootTuck = isBase ? 0.88f + 0.12f * rSin : rootMinW + (1f - rootMinW) * rSin;
                    float rootThickTuck = isBase ? 1.0f : 0.2f + 0.8f * rSin;
                    float tipTaper = isBase
                        ? Mathf.Max(0.04f, t < 0.42f ? 1f - t * 0.16f : Mathf.Pow((1f - t) / 0.58f, 1.12f) * 0.933f)
                        : Mathf.Max(0.06f, t < 0.44f ? 1f - t * 0.14f : Mathf.Pow((1f - t) / 0.56f, 0.94f) * 0.938f);
                    float halfW = baseHalfW * rootTuck * tipTaper;
                    float halfT = baseHalfT * rootThickTuck * Mathf.Pow(Mathf.Max(0.04f, 1f - t), 0.86f);

                    BoneWeight bw = ComputeRingWeight(k, t, isDynamic, chainBones);
                    Vector3 tangU = new Vector3(-tang.x, tang.y, tang.z);
                    for (int r = 0; r < ringVerts; r++)
                    {
                        Vector3 pos = p + s * (PX[r] * halfW) + o * (PY[r] * halfT);
                        Vector3 nrm = s * PNX[r] + o * PNY[r];
                        verts.Add(new Vector3(-pos.x, pos.y, pos.z));
                        norms.Add(new Vector3(-nrm.x, nrm.y, nrm.z));
                        tans.Add(new Vector4(tangU.x, tangU.y, tangU.z, 1f));
                        uvs.Add(new Vector2(PU[r], t));
                        uv2.Add(new Vector2(lk.scalpRoot, 0f));
                        float under = PY[r] < -0.1f ? 0.85f : 1.0f;
                        colors.Add(new Color(t, lk.layerAO * under, lk.seed, maskVal));
                        weights.Add(bw);
                    }
                }

                // Pointed tip vertex
                int tipVertIdx = verts.Count;
                Vector3 tp = w[count - 1];
                Vector3 tt = (tp - w[count - 2]); tt = tt.sqrMagnitude < 1e-12f ? Vector3.down : tt.normalized;
                verts.Add(new Vector3(-tp.x, tp.y, tp.z));
                norms.Add(new Vector3(-o.x, o.y, o.z));
                tans.Add(new Vector4(-tt.x, tt.y, tt.z, 1f));
                uvs.Add(new Vector2(0.5f, 1.0f));
                uv2.Add(new Vector2(lk.scalpRoot, 0f));
                colors.Add(new Color(1f, lk.layerAO, lk.seed, maskVal));
                weights.Add(ComputeRingWeight(ringsPerLock, 1.0f, isDynamic, chainBones));

                // Index order reversed vs web: the X mirror (RH -> LH) flips winding, and Unity treats clockwise as front
                for (int k = 0; k < ringsPerLock - 1; k++)
                {
                    int r0 = baseVert + k * ringVerts;
                    int r1 = baseVert + (k + 1) * ringVerts;
                    for (int r = 0; r < ringVerts - 1; r++)
                    {
                        int a = r0 + r, b = r0 + r + 1, c = r1 + r, d = r1 + r + 1;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }
                }
                int lastRow = baseVert + (ringsPerLock - 1) * ringVerts;
                for (int r = 0; r < ringVerts - 1; r++)
                {
                    tris.Add(lastRow + r); tris.Add(tipVertIdx); tris.Add(lastRow + r + 1);
                }
            }

            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTangents(tans);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, uv2);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, true);
            mesh.boneWeights = weights.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static BoneWeight ComputeRingWeight(int ringIndex, float t, bool isDynamic, int[] chainBones)
        {
            // Base shell or root ring 0 -> 100% pinned to Head (boneIndex 0)
            if (!isDynamic || ringIndex == 0)
            {
                return new BoneWeight { boneIndex0 = 0, weight0 = 1.0f };
            }

            // Deform bones are chainBones[1..N-1] (chainBones[0] is non-deforming scalp anchor)
            int nDef = chainBones.Length - 1;
            float headW = Mathf.Max(0.0f, 1.0f - t * 4.5f);
            float chainW = 1.0f - headW;

            float fIdx = Mathf.Clamp(t * (nDef - 1), 0.0f, nDef - 1);
            int b0 = Mathf.FloorToInt(fIdx);
            int b1 = Mathf.Min(nDef - 1, b0 + 1);
            float frac = fIdx - b0;

            int boneA = chainBones[1 + b0];
            int boneB = chainBones[1 + b1];

            var bw = new BoneWeight();
            if (b0 == b1)
            {
                bw.boneIndex0 = boneA; bw.weight0 = chainW;
                bw.boneIndex1 = 0;     bw.weight1 = headW;
            }
            else
            {
                bw.boneIndex0 = boneA; bw.weight0 = chainW * (1.0f - frac);
                bw.boneIndex1 = boneB; bw.weight1 = chainW * frac;
                bw.boneIndex2 = 0;     bw.weight2 = headW;
            }
            return bw;
        }

        private static List<LockSpec> LoadLocksFromJson(string jsonPath, HairSculptParams p)
        {
            string raw = File.ReadAllText(jsonPath);
            var list = new List<LockSpec>();

            // Zero-dependency, key-order-independent deterministic parser for PresetGeometry.json locks array
            var blockRx = new Regex("\\{[^{}]*\"points\"\\s*:[^{}]*\\}", RegexOptions.Singleline);
            var zoneRx  = new Regex("\"zone\"\\s*:\\s*\"([^\"]+)\"");
            var widthRx = new Regex("\"width\"\\s*:\\s*([-0-9.eE+]+)");
            var thickRx = new Regex("\"thickness\"\\s*:\\s*([-0-9.eE+]+)");
            var seedRx  = new Regex("\"seed\"\\s*:\\s*([-0-9.eE+]+)");
            var aoRx    = new Regex("\"layerAO\"\\s*:\\s*([-0-9.eE+]+)");
            var baseRx  = new Regex("\"isBaseShell\"\\s*:\\s*(true|false)");
            var scalpRx = new Regex("\"scalpRoot\"\\s*:\\s*([-0-9.eE+]+)");
            var lockNRx = new Regex("\"lockNormal\"\\s*:\\s*(true|false)");
            var rootNRx = new Regex("\"rootNormal\"\\s*:\\s*\\[\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*\\]");
            var ptsSecRx = new Regex("\"points\"\\s*:\\s*(\\[.*\\])", RegexOptions.Singleline);
            var ptRx    = new Regex("\\[\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*\\]");

            var matches = blockRx.Matches(raw);
            float maxZ = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;

            var parsedRaw = new List<(string zone, float w, float th, float seed, float ao, bool isBase, List<Vector3> pts, float scalp, bool lockN, Vector3 rootN)>();
            foreach (Match m in matches)
            {
                string blk = m.Value;
                Match zm = zoneRx.Match(blk);
                Match wm = widthRx.Match(blk);
                Match tm = thickRx.Match(blk);
                Match sm = seedRx.Match(blk);
                Match am = aoRx.Match(blk);
                Match bm = baseRx.Match(blk);
                Match psm = ptsSecRx.Match(blk);
                if (!psm.Success) continue;

                string zone = zm.Success ? zm.Groups[1].Value : "Sides";
                float w = wm.Success ? ParseF(wm.Groups[1].Value, 0.024f) : 0.024f;
                float th = tm.Success ? ParseF(tm.Groups[1].Value, 0.010f) : 0.010f;
                float seed = sm.Success ? ParseF(sm.Groups[1].Value, 0.5f) : 0.5f;
                float ao = am.Success ? ParseF(am.Groups[1].Value, 0.92f) : 0.92f;
                bool isBase = bm.Success && bm.Groups[1].Value == "true";

                var pts = new List<Vector3>();
                foreach (Match pm in ptRx.Matches(psm.Groups[1].Value))
                {
                    float bx = ParseF(pm.Groups[1].Value, 0f);
                    float by = ParseF(pm.Groups[2].Value, 0f);
                    float bz = ParseF(pm.Groups[3].Value, 0f);
                    // Blender Z-up (-Y front) -> Unity Y-up (+Z front)
                    Vector3 up = new Vector3(-bx, bz, -by); // handedness flip: Blender RH (+X = viewer right from front) -> Unity LH
                    if (up.y > maxZ) maxZ = up.y;
                    if (up.y < minZ) minZ = up.y;
                    pts.Add(up);
                }

                Match sc = scalpRx.Match(blk); Match ln = lockNRx.Match(blk); Match rn = rootNRx.Match(blk);
                float scalp = sc.Success ? ParseF(sc.Groups[1].Value, 0.65f) : 0.65f;
                bool lockN = ln.Success && ln.Groups[1].Value == "true";
                Vector3 rootN = Vector3.up;
                if (rn.Success)
                {
                    float rx = ParseF(rn.Groups[1].Value, 0f), ry = ParseF(rn.Groups[2].Value, 0f), rz = ParseF(rn.Groups[3].Value, 1f);
                    rootN = new Vector3(-rx, rz, -ry);
                }
                if (pts.Count >= 2)
                    parsedRaw.Add((zone, w, th, seed, ao, isBase, pts, scalp, lockN, rootN));
            }

            // Auto-normalize if an unscaled 0.97m web-bust JSON is fed in
            float autoNorm = (p.legacyAutoNormalize && (maxZ - minZ) > 0.45f) ? (0.23f / Mathf.Max(0.1f, maxZ)) : 1.0f;
            float s = autoNorm * Mathf.Clamp(p.overallScale, 0.5f, 1.8f);

            foreach (var item in parsedRaw)
            {
                var pts = new Vector3[item.pts.Count];
                for (int i = 0; i < item.pts.Count; i++)
                    pts[i] = item.pts[i] * s + p.headLocalOffset;

                if (!item.isBase)
                {
                    float trim = string.Equals(item.zone, "Bangs", StringComparison.OrdinalIgnoreCase)
                        ? Mathf.Clamp(p.bangsLength, 0.35f, 1.35f)
                        : Mathf.Clamp(p.layerLength, 0.40f, 1.40f);
                    if (Mathf.Abs(trim - 1.0f) > 0.005f)
                        pts = ResampleTrimmed(pts, trim);
                }

                list.Add(new LockSpec
                {
                    zone = item.zone,
                    width = item.w * s * Mathf.Clamp(p.widthScale, 0.4f, 1.8f),
                    thickness = item.th * s * Mathf.Clamp(p.thicknessScale, 0.4f, 1.8f),
                    seed = item.seed,
                    layerAO = item.ao,
                    isBaseShell = item.isBase,
                    scalpRoot = item.scalp,
                    lockNormal = item.lockN,
                    rootNormal = item.rootN,
                    points = pts,
                });
            }

            return list;
        }

        private static Vector3[] ResampleTrimmed(Vector3[] pts, float lengthScale)
        {
            int n = pts.Length;
            var outPts = new Vector3[n];
            if (lengthScale <= 1.0f)
            {
                for (int i = 0; i < n; i++)
                {
                    float u = ((float)i / (n - 1)) * lengthScale * (n - 1);
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 1);
                    int i1 = Mathf.Min(n - 1, i0 + 1);
                    outPts[i] = Vector3.Lerp(pts[i0], pts[i1], u - i0);
                }
            }
            else
            {
                Vector3 root = pts[0];
                for (int i = 0; i < n; i++)
                    outPts[i] = root + (pts[i] - root) * lengthScale;
            }
            return outPts;
        }

        private static List<List<int>> ClusterDeterministically(List<LockSpec> locks, List<int> indices, int k)
        {
            int n = indices.Count;
            var features = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3[] pts = locks[indices[i]].points;
                features[i] = pts[0] * 0.55f + pts[pts.Length / 2] * 0.30f + pts[pts.Length - 1] * 0.15f;
            }

            // Deterministic furthest-point seeding
            var centers = new Vector3[k];
            var chosen = new HashSet<int> { 0 };
            centers[0] = features[0];
            for (int c = 1; c < k; c++)
            {
                int best = 0;
                float bestDist = -1f;
                for (int i = 0; i < n; i++)
                {
                    if (chosen.Contains(i)) continue;
                    float minD = float.PositiveInfinity;
                    for (int prev = 0; prev < c; prev++)
                    {
                        float d = (features[i] - centers[prev]).sqrMagnitude;
                        if (d < minD) minD = d;
                    }
                    if (minD > bestDist) { bestDist = minD; best = i; }
                }
                chosen.Add(best);
                centers[c] = features[best];
            }

            var assign = new int[n];
            for (int iter = 0; iter < 10; iter++)
            {
                for (int i = 0; i < n; i++)
                {
                    int bestC = 0;
                    float bestD = float.PositiveInfinity;
                    for (int c = 0; c < k; c++)
                    {
                        float d = (features[i] - centers[c]).sqrMagnitude;
                        if (d < bestD) { bestD = d; bestC = c; }
                    }
                    assign[i] = bestC;
                }
                for (int c = 0; c < k; c++)
                {
                    Vector3 sum = Vector3.zero;
                    int cnt = 0;
                    for (int i = 0; i < n; i++)
                        if (assign[i] == c) { sum += features[i]; cnt++; }
                    if (cnt > 0) centers[c] = sum / cnt;
                }
            }

            var result = new List<List<int>>();
            for (int c = 0; c < k; c++)
            {
                var cl = new List<int>();
                for (int i = 0; i < n; i++)
                    if (assign[i] == c) cl.Add(indices[i]);
                if (cl.Count > 0) result.Add(cl);
            }
            return result;
        }

        private static Vector3 MeanPoint(List<LockSpec> locks, List<int> members, int ptIdx)
        {
            Vector3 sum = Vector3.zero;
            foreach (int i in members) sum += locks[i].points[Mathf.Min(ptIdx, locks[i].points.Length - 1)];
            return sum / Mathf.Max(1, members.Count);
        }

        private static Vector3[] BuildMeanSpine(List<LockSpec> locks, List<int> members, int targetPoints)
        {
            var spine = new Vector3[targetPoints];
            for (int s = 0; s < targetPoints; s++)
            {
                float frac = (float)s / Mathf.Max(1, targetPoints - 1);
                Vector3 acc = Vector3.zero;
                foreach (int idx in members)
                {
                    Vector3[] pts = locks[idx].points;
                    float u = frac * (pts.Length - 1);
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, pts.Length - 1);
                    int i1 = Mathf.Min(pts.Length - 1, i0 + 1);
                    acc += Vector3.Lerp(pts[i0], pts[i1], u - i0);
                }
                spine[s] = acc / Mathf.Max(1, members.Count);
            }
            return spine;
        }

        private static void RemovePreviousGeneratedHair(GameObject characterRoot, Transform headBone)
        {
            Transform oldMesh = characterRoot.transform.Find("AetherStrand_HairMesh");
            if (oldMesh != null) UnityEngine.Object.DestroyImmediate(oldMesh.gameObject);

            var toDestroy = new List<GameObject>();
            for (int i = 0; i < headBone.childCount; i++)
            {
                Transform ch = headBone.GetChild(i);
                if (ch.name.StartsWith("Hair_", StringComparison.Ordinal))
                    toDestroy.Add(ch.gameObject);
            }
            foreach (var go in toDestroy) UnityEngine.Object.DestroyImmediate(go);
        }

        private static Mesh SaveMeshInPlace(Mesh mesh, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                assetPath = "Assets/AetherStrand_GeneratedHair.asset";

            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        public static Transform ResolveHeadBone(GameObject root)
        {
            var anim = root.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform h = anim.GetBoneTransform(HumanBodyBones.Head);
                if (h != null) return h;
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        public static string ResolveDefaultPresetGeometryPath()
        {
            string[] guids = AssetDatabase.FindAssets("AetherStrandMeshGenerator t:MonoScript");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string editorDir = Path.GetDirectoryName(scriptPath);
                string pkgRoot = string.IsNullOrEmpty(editorDir) ? null : Path.GetDirectoryName(editorDir);
                if (!string.IsNullOrEmpty(pkgRoot))
                {
                    string candidate = Path.Combine(pkgRoot, "Presets", "PresetGeometry.json").Replace('\\', '/');
                    if (File.Exists(candidate)) return candidate;
                }
            }
            string fallback = "Packages/com.aetherstrand.sculptedhair/Presets/PresetGeometry.json";
            return File.Exists(fallback) ? fallback : null;
        }

        private static string NormalizeZone(string z)
        {
            if (string.Equals(z, "Bangs", StringComparison.OrdinalIgnoreCase)) return "Bangs";
            if (string.Equals(z, "Crown", StringComparison.OrdinalIgnoreCase)) return "Crown";
            if (string.Equals(z, "Rear", StringComparison.OrdinalIgnoreCase)) return "Rear";
            return "Sides";
        }

        private static float ParseF(string s, float def)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def;
        }
    }
}
#endif
