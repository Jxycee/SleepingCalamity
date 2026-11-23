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
            public Vector3[] points; // Unity Head-local space (+X right, +Y up, +Z forward)
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

        private static Mesh BuildVolumetricMesh(
            List<LockSpec> locks,
            Dictionary<int, int[]> lockToChainBoneIndices,
            int totalBoneCount)
        {
            var verts = new List<Vector3>(locks.Count * 80);
            var uvs = new List<Vector2>(locks.Count * 80);
            var colors = new List<Color>(locks.Count * 80);
            var weights = new List<BoneWeight>(locks.Count * 80);
            var tris = new List<int>(locks.Count * 140);

            int sides = ProfileX.Length - 1; // 6 quads around circumference

            for (int lIdx = 0; lIdx < locks.Count; lIdx++)
            {
                LockSpec lk = locks[lIdx];
                Vector3[] pts = lk.points;
                int count = pts.Length;
                if (count < 2) continue;

                int ringCount = count - 1;
                int baseVert = verts.Count;
                float tipClipFlag = lk.isBaseShell ? 0.0f : 1.0f;

                Vector3 refOut = pts[0].sqrMagnitude > 1e-6f ? pts[0].normalized : Vector3.up;

                int[] chainBones = null;
                bool isDynamic = !lk.isBaseShell && lockToChainBoneIndices.TryGetValue(lIdx, out chainBones) && chainBones.Length >= 2;

                for (int k = 0; k < ringCount; k++)
                {
                    Vector3 pt = pts[k];
                    float t = (float)k / Mathf.Max(1, count - 1);
                    Vector3 tang = (k == 0 ? (pts[1] - pt) : (pts[k + 1] - pts[k - 1])).normalized;
                    if (tang.sqrMagnitude < 1e-6f) tang = Vector3.down;

                    Vector3 side = Vector3.Cross(tang, refOut);
                    side = side.sqrMagnitude < 1e-6f ? Vector3.right : side.normalized;
                    Vector3 outVec = Vector3.Cross(side, tang).normalized;
                    refOut = Vector3.Lerp(refOut, outVec, 0.35f).normalized;

                    float rootTuck = 0.70f + 0.30f * Mathf.Sin(Mathf.Min(1.0f, t * 4.5f) * Mathf.PI * 0.5f);
                    float widthTaper = t < 0.44f
                        ? (1.0f - t * 0.14f)
                        : (Mathf.Pow((1.0f - t) / 0.56f, 0.94f) * 0.938f);
                    float thickTaper = Mathf.Pow(1.0f - t, 0.86f);

                    float halfW = lk.width * 0.5f * rootTuck * Mathf.Max(0.04f, widthTaper);
                    float halfT = lk.thickness * 0.47f * rootTuck * thickTaper;

                    BoneWeight bw = ComputeRingWeight(k, t, isDynamic, chainBones);

                    // Duplicate seam vertex (sides + 1) so UV wraps 0..1 cleanly without stretch
                    for (int r = 0; r <= sides; r++)
                    {
                        int ri = r % sides;
                        Vector3 pos = pt + side * (ProfileX[ri] * halfW) + outVec * (ProfileY[ri] * halfT);
                        verts.Add(pos);
                        uvs.Add(new Vector2(ProfileU[r], t));
                        colors.Add(new Color(t, lk.layerAO, lk.seed, tipClipFlag));
                        weights.Add(bw);
                    }
                }

                // Pointed tip vertex
                int tipVertIdx = verts.Count;
                verts.Add(pts[count - 1]);
                uvs.Add(new Vector2(0.5f, 1.0f));
                colors.Add(new Color(1.0f, lk.layerAO, lk.seed, tipClipFlag));
                weights.Add(ComputeRingWeight(ringCount, 1.0f, isDynamic, chainBones));

                int stride = sides + 1;
                for (int k = 0; k < ringCount - 1; k++)
                {
                    int r0 = baseVert + k * stride;
                    int r1 = baseVert + (k + 1) * stride;
                    for (int r = 0; r < sides; r++)
                    {
                        int a = r0 + r;
                        int b = r0 + r + 1;
                        int c = r1 + r + 1;
                        int d = r1 + r;
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(a); tris.Add(c); tris.Add(d);
                    }
                }

                // Tip cap triangles
                int rLast = baseVert + (ringCount - 1) * stride;
                for (int r = 0; r < sides; r++)
                {
                    tris.Add(rLast + r);
                    tris.Add(rLast + r + 1);
                    tris.Add(tipVertIdx);
                }
            }

            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, true);
            mesh.boneWeights = weights.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
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
            var ptsSecRx = new Regex("\"points\"\\s*:\\s*(\\[.*\\])", RegexOptions.Singleline);
            var ptRx    = new Regex("\\[\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*,\\s*([-0-9.eE+]+)\\s*\\]");

            var matches = blockRx.Matches(raw);
            float maxZ = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;

            var parsedRaw = new List<(string zone, float w, float th, float seed, float ao, bool isBase, List<Vector3> pts)>();
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
                    Vector3 up = new Vector3(bx, bz, -by);
                    if (up.y > maxZ) maxZ = up.y;
                    if (up.y < minZ) minZ = up.y;
                    pts.Add(up);
                }

                if (pts.Count >= 2)
                    parsedRaw.Add((zone, w, th, seed, ao, isBase, pts));
            }

            // Auto-normalize if an unscaled 0.97m web-bust JSON is fed in
            float autoNorm = (maxZ - minZ) > 0.45f ? (0.23f / Mathf.Max(0.1f, maxZ)) : 1.0f;
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
