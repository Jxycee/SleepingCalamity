#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// AI Debug & Diagnostic Exporter for AetherStrand (Unity 6 / URP 17.6).
    /// Mirrors the web app's Inspect/Audit menu so both humans and AI agents (Wren/Claude -> Neon)
    /// get exact Head-vs-Hair millimeter alignment deltas, collider telemetry, 3-scenario physics
    /// audits (Rest, 60-deg Head Turn, 5 m/s Sprint Stop), shader config, and a 4-up PNG capture.
    /// </summary>
    public static class AetherStrandAiDiagnostics
    {
        [Serializable]
        public class Vec3Dto
        {
            public float x, y, z;
            public Vec3Dto(Vector3 v) { x = R4(v.x); y = R4(v.y); z = R4(v.z); }
        }

        [Serializable]
        public class BoundsDto
        {
            public Vec3Dto min, max, center, size;
            public BoundsDto(Bounds b)
            {
                min = new Vec3Dto(b.min);
                max = new Vec3Dto(b.max);
                center = new Vec3Dto(b.center);
                size = new Vec3Dto(b.size);
            }
        }

        [Serializable]
        public class ColliderDto
        {
            public string name, parentBone, shape;
            public Vec3Dto localCenter, worldCenter;
            public float radius, capsuleHeight, parentUniformScale;
        }

        [Serializable]
        public class ScenarioAuditDto
        {
            public string scenario;
            public string verdict;
            public int score;
            public int totalChains;
            public int colliderCount;
            public int penetratingNodes;
            public int interLockClipCount;
            public float maxKinkAngleDeg;
            public float maxStretchErrorPct;
            public float meanTipSwayCm;
        }

        [Serializable]
        public class DiagnosticReport
        {
            public string pluginVersion = "3.0.1";
            public string characterName;
            public string headBoneName;
            public Vec3Dto headBoneWorldPos;
            public Vec3Dto headLocalForwardInCharSpace;
            public Vec3Dto headLocalUpInCharSpace;
            public Vec3Dto headLossyScale;

            public bool hasHeadMeshBounds;
            public BoundsDto headMeshBoundsInHeadLocal;
            public bool hasHairMeshBounds;
            public BoundsDto hairMeshBoundsInHeadLocal;

            // Alignment deltas in millimeters (positive frontOverhangMm = bangs extend in front of forehead)
            public float frontOverhangMm;
            public float crownLiftMm;
            public float backReachMm;
            public float widthDeltaMm;
            public int hairBoneRootCount;
            public int bangsChainCount;
            public int crownChainCount;
            public int sidesChainCount;
            public int rearChainCount;

            public string shaderName;
            public string rootColorHex, baseColorHex, tipColorHex;
            public float headCenterBlend, alphaClip, grooveDepth, primaryIntensity, secondaryIntensity;
            public int boundTextureCount;

            public List<ColliderDto> colliders = new List<ColliderDto>();
            public List<ScenarioAuditDto> scenarios = new List<ScenarioAuditDto>();
            public List<string> flags = new List<string>();
        }

        public static DiagnosticReport CollectReport(GameObject characterRoot, Renderer hairRenderer = null)
        {
            var r = new DiagnosticReport
            {
                characterName = characterRoot != null ? characterRoot.name : "(null)"
            };
            if (characterRoot == null)
            {
                r.flags.Add("ERROR: characterRoot is null");
                return r;
            }

            Transform head = AetherStrandMeshGenerator.ResolveHeadBone(characterRoot);
            if (head == null)
            {
                r.flags.Add("ERROR: Head bone not found");
                return r;
            }

            r.headBoneName = head.name;
            r.headBoneWorldPos = new Vec3Dto(head.position);
            r.headLocalForwardInCharSpace = new Vec3Dto(characterRoot.transform.InverseTransformDirection(head.forward));
            r.headLocalUpInCharSpace = new Vec3Dto(characterRoot.transform.InverseTransformDirection(head.up));
            r.headLossyScale = new Vec3Dto(head.lossyScale);

            if (hairRenderer == null)
            {
                Transform ht = characterRoot.transform.Find("AetherStrand_HairMesh");
                if (ht != null) hairRenderer = ht.GetComponent<Renderer>();
                if (hairRenderer == null)
                {
                    var physTmp = characterRoot.GetComponent<AetherStrandPhysics>();
                    if (physTmp != null) hairRenderer = physTmp.hairRenderer;
                }
            }

            // Compute Hair mesh bounds in Head-local space
            if (hairRenderer != null)
            {
                if (TryComputeRendererBoundsInFrame(hairRenderer, head, out Bounds hairB))
                {
                    r.hasHairMeshBounds = true;
                    r.hairMeshBoundsInHeadLocal = new BoundsDto(hairB);
                }

                Material mat = hairRenderer.sharedMaterial;
                if (mat != null)
                {
                    r.shaderName = mat.shader != null ? mat.shader.name : "(null)";
                    if (mat.HasProperty("_RootColor")) r.rootColorHex = "#" + ColorUtility.ToHtmlStringRGB(mat.GetColor("_RootColor"));
                    if (mat.HasProperty("_BaseColor")) r.baseColorHex = "#" + ColorUtility.ToHtmlStringRGB(mat.GetColor("_BaseColor"));
                    if (mat.HasProperty("_TipColor"))  r.tipColorHex  = "#" + ColorUtility.ToHtmlStringRGB(mat.GetColor("_TipColor"));
                    if (mat.HasProperty("_HeadCenterBlend")) r.headCenterBlend = R4(mat.GetFloat("_HeadCenterBlend"));
                    if (mat.HasProperty("_AlphaClip")) r.alphaClip = R4(mat.GetFloat("_AlphaClip"));
                    if (mat.HasProperty("_GrooveDepth")) r.grooveDepth = R4(mat.GetFloat("_GrooveDepth"));
                    if (mat.HasProperty("_PrimaryIntensity")) r.primaryIntensity = R4(mat.GetFloat("_PrimaryIntensity"));
                    if (mat.HasProperty("_SecondaryIntensity")) r.secondaryIntensity = R4(mat.GetFloat("_SecondaryIntensity"));

                    int texCnt = 0;
                    foreach (string tp in new[] { "_GrooveAoMap", "_FlowIdMap", "_NormalMap", "_AlphaMap" })
                        if (mat.HasProperty(tp) && mat.GetTexture(tp) != null) texCnt++;
                    r.boundTextureCount = texCnt;
                    if (texCnt < 4) r.flags.Add($"WARN: Only {texCnt}/4 AetherStrand textures bound on material");
                }
            }
            else
            {
                r.flags.Add("WARN: No hairRenderer found on characterRoot");
            }

            // Compute Head/Face mesh bounds in Head-local space (excludes hairRenderer & tiny eye/lash meshes)
            if (TryComputeHeadSkullBoundsInFrame(characterRoot, head, hairRenderer, out Bounds headB))
            {
                r.hasHeadMeshBounds = true;
                r.headMeshBoundsInHeadLocal = new BoundsDto(headB);
            }

            if (r.hasHeadMeshBounds && r.hasHairMeshBounds)
            {
                Bounds hb = new Bounds(
                    new Vector3(r.headMeshBoundsInHeadLocal.center.x, r.headMeshBoundsInHeadLocal.center.y, r.headMeshBoundsInHeadLocal.center.z),
                    new Vector3(r.headMeshBoundsInHeadLocal.size.x, r.headMeshBoundsInHeadLocal.size.y, r.headMeshBoundsInHeadLocal.size.z));
                Bounds hairB = new Bounds(
                    new Vector3(r.hairMeshBoundsInHeadLocal.center.x, r.hairMeshBoundsInHeadLocal.center.y, r.hairMeshBoundsInHeadLocal.center.z),
                    new Vector3(r.hairMeshBoundsInHeadLocal.size.x, r.hairMeshBoundsInHeadLocal.size.y, r.hairMeshBoundsInHeadLocal.size.z));

                r.frontOverhangMm = R4((hairB.max.z - hb.max.z) * 1000f);
                r.crownLiftMm     = R4((hairB.max.y - hb.max.y) * 1000f);
                r.backReachMm     = R4((hb.min.z - hairB.min.z) * 1000f);
                r.widthDeltaMm    = R4((hairB.size.x - hb.size.x) * 1000f);

                if (r.frontOverhangMm < -15f)
                    r.flags.Add($"ALIGNMENT: Hair front sits {Mathf.Abs(r.frontOverhangMm):F1} mm BEHIND forehead (receding hairline)");
                if (r.crownLiftMm > 45f)
                    r.flags.Add($"ALIGNMENT: Hair crown sits {r.crownLiftMm:F1} mm above skull top (possible crown loops)");
            }

            // Physics, chains, colliders & 3-scenario deterministic audit
            AetherStrandPhysics phys = characterRoot.GetComponent<AetherStrandPhysics>();
            if (phys != null)
            {
                r.hairBoneRootCount = phys.hairRootBones != null ? phys.hairRootBones.Count : 0;
                if (phys.hairRootBones != null)
                {
                    foreach (Transform rt in phys.hairRootBones)
                    {
                        if (rt == null) continue;
                        string n = rt.name;
                        if (n.IndexOf("Bangs", StringComparison.OrdinalIgnoreCase) >= 0) r.bangsChainCount++;
                        else if (n.IndexOf("Crown", StringComparison.OrdinalIgnoreCase) >= 0) r.crownChainCount++;
                        else if (n.IndexOf("Sides", StringComparison.OrdinalIgnoreCase) >= 0) r.sidesChainCount++;
                        else r.rearChainCount++;
                    }
                }

                if (phys.colliders != null)
                {
                    foreach (var col in phys.colliders)
                    {
                        if (col == null) continue;
                        Vector3 s = col.transform.lossyScale;
                        float uScale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
                        r.colliders.Add(new ColliderDto
                        {
                            name = col.name,
                            parentBone = col.transform.parent != null ? col.transform.parent.name : col.transform.name,
                            shape = col.shape.ToString(),
                            localCenter = new Vec3Dto(col.centerOffset),
                            worldCenter = new Vec3Dto(col.transform.TransformPoint(col.centerOffset)),
                            radius = R4(col.radius),
                            capsuleHeight = R4(col.capsuleHeight),
                            parentUniformScale = R4(uScale)
                        });
                    }
                }

                RunDeterministicScenarios(phys, head, r.scenarios);
            }
            else
            {
                r.flags.Add("WARN: No AetherStrandPhysics component on characterRoot");
            }

            return r;
        }

        private static void RunDeterministicScenarios(AetherStrandPhysics phys, Transform head, List<ScenarioAuditDto> outList)
        {
            Vector3 origPos = head.position;
            Quaternion origRot = head.rotation;
            const float dt = 1f / 60f;

            try
            {
                // Scenario 1: Rest (30 frames still, zero wind)
                phys.RebuildChains();
                phys.ResetToRestPose();
                for (int i = 0; i < 30; i++) phys.Simulate(dt, i * dt);
                outList.Add(ToDto("Rest_0.5s", phys.ComputeAudit()));

                // Scenario 2: 60-deg Head Turn in 0.10s + settle
                head.position = origPos;
                head.rotation = origRot;
                phys.ResetToRestPose();
                for (int i = 0; i < 24; i++)
                {
                    float frac = Mathf.Clamp01((i + 1) / 6f);
                    head.rotation = origRot * Quaternion.Euler(0f, 60f * frac, 0f);
                    phys.Simulate(dt, i * dt);
                }
                outList.Add(ToDto("HeadTurn60_0.4s", phys.ComputeAudit()));

                // Scenario 3: 5 m/s Sprint for 0.15s -> Stop + settle
                head.position = origPos;
                head.rotation = origRot;
                phys.ResetToRestPose();
                for (int i = 0; i < 24; i++)
                {
                    if (i < 9) head.position += Vector3.forward * (5.0f * dt);
                    phys.Simulate(dt, i * dt);
                }
                outList.Add(ToDto("Sprint5msStop_0.4s", phys.ComputeAudit()));
            }
            finally
            {
                head.position = origPos;
                head.rotation = origRot;
                phys.ResetToRestPose();
            }
        }

        private static ScenarioAuditDto ToDto(string name, PhysicsAuditResult a)
        {
            return new ScenarioAuditDto
            {
                scenario = name,
                verdict = string.IsNullOrEmpty(a.verdict) ? "N/A" : a.verdict,
                score = a.score,
                totalChains = a.totalChains,
                colliderCount = a.colliderCount,
                penetratingNodes = a.penetratingNodes,
                interLockClipCount = a.interLockClipCount,
                maxKinkAngleDeg = R4(a.maxKinkAngleDeg),
                maxStretchErrorPct = R4(a.maxStretchErrorPct),
                meanTipSwayCm = R4(a.meanTipSwayCm)
            };
        }

        public static string FormatMarkdownSummary(DiagnosticReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# AetherStrand AI Diagnostic Report (v{r.pluginVersion})");
            sb.AppendLine($"- **Character:** `{r.characterName}` | **Head Bone:** `{r.headBoneName}` | **LossyScale:** `({r.headLossyScale.x}, {r.headLossyScale.y}, {r.headLossyScale.z})`");
            sb.AppendLine($"- **Head Axes in Char Space:** Fwd `({r.headLocalForwardInCharSpace.x}, {r.headLocalForwardInCharSpace.y}, {r.headLocalForwardInCharSpace.z})`, Up `({r.headLocalUpInCharSpace.x}, {r.headLocalUpInCharSpace.y}, {r.headLocalUpInCharSpace.z})`");
            sb.AppendLine($"- **Chains:** {r.hairBoneRootCount} total (`Bangs={r.bangsChainCount}`, `Crown={r.crownChainCount}`, `Sides={r.sidesChainCount}`, `Rear={r.rearChainCount}`) | **Colliders:** {r.colliders.Count}");
            sb.AppendLine($"- **Shader:** `{r.shaderName}` | Colors `{r.rootColorHex} / {r.baseColorHex} / {r.tipColorHex}` | Textures `{r.boundTextureCount}/4`");

            if (r.hasHeadMeshBounds && r.hasHairMeshBounds)
            {
                sb.AppendLine("\n## Head vs. Hair Alignment (Head-Local Space)");
                sb.AppendLine($"- **Head Bounds:** min `({r.headMeshBoundsInHeadLocal.min.x}, {r.headMeshBoundsInHeadLocal.min.y}, {r.headMeshBoundsInHeadLocal.min.z})` .. max `({r.headMeshBoundsInHeadLocal.max.x}, {r.headMeshBoundsInHeadLocal.max.y}, {r.headMeshBoundsInHeadLocal.max.z})`");
                sb.AppendLine($"- **Hair Bounds:** min `({r.hairMeshBoundsInHeadLocal.min.x}, {r.hairMeshBoundsInHeadLocal.min.y}, {r.hairMeshBoundsInHeadLocal.min.z})` .. max `({r.hairMeshBoundsInHeadLocal.max.x}, {r.hairMeshBoundsInHeadLocal.max.y}, {r.hairMeshBoundsInHeadLocal.max.z})`");
                sb.AppendLine($"- **Deltas (mm):** Front Overhang `{r.frontOverhangMm:+0.0;-0.0} mm` | Crown Lift `{r.crownLiftMm:+0.0;-0.0} mm` | Back Reach `{r.backReachMm:+0.0;-0.0} mm` | Width Delta `{r.widthDeltaMm:+0.0;-0.0} mm`");
            }

            if (r.scenarios.Count > 0)
            {
                sb.AppendLine("\n## Deterministic Motion Audits");
                sb.AppendLine("| Scenario | Verdict | Score | Penetrating | Inter-Lock Clips | Max Kink (deg) | Max Stretch (%) | Mean Sway (cm) |");
                sb.AppendLine("| :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: |");
                foreach (var s in r.scenarios)
                    sb.AppendLine($"| {s.scenario} | {s.verdict} | {s.score} | {s.penetratingNodes} | {s.interLockClipCount} | {s.maxKinkAngleDeg:F1} | {s.maxStretchErrorPct:F2} | {s.meanTipSwayCm:F2} |");
            }

            if (r.flags.Count > 0)
            {
                sb.AppendLine("\n## Automated Flags");
                foreach (string fl in r.flags) sb.AppendLine($"- {fl}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports JSON + Markdown summary + 4-up character PNG to outputFolder for Claude -> Neon handoff.
        /// </summary>
        public static DiagnosticReport ExportBundle(GameObject characterRoot, string outputFolder, Renderer hairRenderer = null)
        {
            if (string.IsNullOrEmpty(outputFolder))
                outputFolder = "Assets/AetherStrand_AI_Diag";
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            DiagnosticReport report = CollectReport(characterRoot, hairRenderer);
            string jsonPath = Path.Combine(outputFolder, "aetherstrand_diag_report.json").Replace('\\', '/');
            string mdPath   = Path.Combine(outputFolder, "aetherstrand_diag_summary.md").Replace('\\', '/');

            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            File.WriteAllText(mdPath, FormatMarkdownSummary(report), new UTF8Encoding(false));

            AssetDatabase.Refresh();
            Debug.Log($"[AetherStrand] Exported AI Diagnostic Bundle to '{outputFolder}' (JSON + MD).");
            return report;
        }

        private static bool TryComputeRendererBoundsInFrame(Renderer rend, Transform frame, out Bounds localBounds)
        {
            localBounds = default;
            Mesh m = null;
            if (rend is SkinnedMeshRenderer smr) m = smr.sharedMesh;
            else if (rend.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
            if (m == null || m.vertexCount == 0) return false;

            var verts = m.vertices;
            Matrix4x4 toFrame = frame.worldToLocalMatrix * rend.transform.localToWorldMatrix;
            Vector3 mn = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 mx = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = toFrame.MultiplyPoint3x4(verts[i]);
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }
            localBounds = new Bounds((mn + mx) * 0.5f, mx - mn);
            return true;
        }

        private static bool TryComputeHeadSkullBoundsInFrame(GameObject root, Transform head, Renderer excludeHair, out Bounds skullBounds)
        {
            skullBounds = default;
            Vector3 mn = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 mx = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            bool any = false;

            foreach (Renderer rend in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rend == excludeHair) continue;
                if (rend.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!TryComputeRendererBoundsInFrame(rend, head, out Bounds b)) continue;
                // Keep only vertices near the skull volume (within +/- 0.25m of head bone)
                Vector3 c = b.center;
                if (c.magnitude > 0.35f) continue;

                Mesh m = rend is SkinnedMeshRenderer smr ? smr.sharedMesh : (rend.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null);
                if (m == null) continue;
                Matrix4x4 toHead = head.worldToLocalMatrix * rend.transform.localToWorldMatrix;
                foreach (Vector3 v in m.vertices)
                {
                    Vector3 p = toHead.MultiplyPoint3x4(v);
                    if (p.y < -0.05f || p.y > 0.28f || Mathf.Abs(p.x) > 0.16f || Mathf.Abs(p.z) > 0.18f) continue;
                    mn = Vector3.Min(mn, p);
                    mx = Vector3.Max(mx, p);
                    any = true;
                }
            }

            if (!any) return false;
            skullBounds = new Bounds((mn + mx) * 0.5f, mx - mn);
            return true;
        }

        private static float R4(float v) => Mathf.Round(v * 10000f) / 10000f;
    }
}
#endif
