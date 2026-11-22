using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherStrand
{
    public enum HairZone
    {
        Bangs,
        Crown,
        Sides,
        Rear
    }

    [Serializable]
    public struct AetherStrandAuditMetrics
    {
        public int totalChains;
        public int penetratingNodes;
        public float maxKinkAngleDeg;
        public float meanTipSwayCm;
        public float maxStretchErrorPct;
    }

    /// <summary>
    /// Real-time XPBD / Verlet Hair Bone Chain Simulator for layered sculpted hair.
    /// Features substepped framerate-independent integration, per-zone stiffness/drag,
    /// rest-curvature bending smoothness, collider-projected rest poses,
    /// zero-restitution sliding friction, rotation-only write-back, and PlayMode audit telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public class AetherStrandPhysics : MonoBehaviour
    {
        [Header("Rig References")]
        [Tooltip("Character Head bone transform used for inertia and rest-pose orientation.")]
        public Transform headTransform;

        [Tooltip("Optional SkinnedMeshRenderer to receive _HeadCenterWS via MaterialPropertyBlock.")]
        public Renderer hairRenderer;

        [Tooltip("Local offset from headTransform to skull center for radial normal blending.")]
        public Vector3 headCenterLocalOffset = new Vector3(0f, 0.11f, -0.01f);

        [Tooltip("Root bone of each hair lock chain (auto-traverses children).")]
        public List<Transform> hairRootBones = new List<Transform>();

        [Tooltip("Head, jaw, neck, chest, and shoulder analytical colliders.")]
        public List<AetherStrandCollider> colliders = new List<AetherStrandCollider>();

        [Tooltip("Optional global wind controller.")]
        public AetherStrandWindZone windZone;

        [Header("Silhouette & Physics")]
        [Range(0.05f, 0.95f)]
        [Tooltip("Rest-pose shape retention. High (0.60-0.82) holds sculpted layered shapes; low (0.25-0.4) flows freely.")]
        public float stiffness = 0.62f;

        [Tooltip("Downward gravity acceleration in m/s^2.")]
        public float gravity = 4.40f;

        [Range(0.01f, 0.35f)]
        [Tooltip("Velocity damping coefficient (framerate-independent, calibrated at 60 Hz).")]
        public float damping = 0.095f;

        [Range(0.0f, 1.5f)]
        [Tooltip("Lag/drag from character head linear movement (scaled separately from head rotation).")]
        public float headInertia = 0.88f;

        [Range(0.0f, 0.95f)]
        [Tooltip("Rest-curvature-preserving bending smoothness. Eliminates accordion kinks across colliding bangs.")]
        public float bendingSmoothness = 0.52f;

        [Range(0.002f, 0.030f)]
        [Tooltip("Minimum surface clearance outside head/neck/shoulder colliders (meters).")]
        public float collisionMargin = 0.011f;

        [Range(0.1f, 1.0f)]
        [Tooltip("Upper scalp parting anchor strength across root bones 1..3.")]
        public float rootAnchor = 0.62f;

        [Range(1, 6)]
        [Tooltip("Integration substeps per frame for fast head turns and dodges.")]
        public int solverSubsteps = 3;

        [Range(1, 6)]
        public int solverIterations = 4;

        [Tooltip("Virtual tip extension length beyond the final leaf bone.")]
        public float leafExtensionLength = 0.06f;

        [Header("Per-Zone Multipliers (Auto-Classified)")]
        public float bangsStiffnessMul = 1.25f;
        public float crownStiffnessMul = 1.35f;
        public float sidesStiffnessMul = 0.95f;
        public float rearStiffnessMul  = 0.80f;

        [Header("Fallback Local Wind")]
        public float fallbackWindStrength = 0.55f;
        public float fallbackTurbulence = 0.24f;
        public float fallbackWindAzimuthDeg = 30.0f;

        [Header("Debug Visualization")]
        public bool drawGizmos = true;

        private class HairNode
        {
            public Transform bone;
            public Vector3 worldPos;
            public Vector3 prevWorldPos;
            public Vector3 restLocalPos;
            public Quaternion restLocalRot;
            public float segmentLength;
            public float tipFraction;
            public bool isVirtualLeaf;
        }

        private class HairChain
        {
            public readonly List<HairNode> nodes = new List<HairNode>();
            public HairZone zone;
            public float seedPhase;
        }

        private readonly List<HairChain> _chains = new List<HairChain>();
        private Vector3 _prevHeadPos;
        private Vector3 _impulseVelocity;
        private bool _initialized;
        private MaterialPropertyBlock _mpb;
        private static readonly int IdHeadCenterWS = Shader.PropertyToID("_HeadCenterWS");

        public int ChainCount => _chains.Count;

        private void Awake()
        {
            if (headTransform == null) headTransform = transform;
            RebuildChains();
        }

        [ContextMenu("Rebuild Hair Chains")]
        public void RebuildChains()
        {
            _chains.Clear();
            if (headTransform == null) headTransform = transform;

            int chainIndex = 0;
            foreach (Transform root in hairRootBones)
            {
                if (root == null) continue;

                var linearBones = new List<Transform>();
                Transform current = root;
                while (current != null)
                {
                    linearBones.Add(current);
                    current = current.childCount > 0 ? current.GetChild(0) : null;
                }

                if (linearBones.Count < 1) continue;

                var chain = new HairChain
                {
                    seedPhase = (chainIndex++ * 1.618033f) * 12.5f,
                    zone = ClassifyZone(root)
                };

                int totalNodes = linearBones.Count + 1;
                for (int i = 0; i < linearBones.Count; i++)
                {
                    Transform b = linearBones[i];
                    float segLen = i == 0 ? 0f : Vector3.Distance(b.position, linearBones[i - 1].position);
                    chain.nodes.Add(new HairNode
                    {
                        bone = b,
                        worldPos = b.position,
                        prevWorldPos = b.position,
                        restLocalPos = headTransform.InverseTransformPoint(b.position),
                        restLocalRot = b.localRotation,
                        segmentLength = segLen,
                        tipFraction = (float)i / (totalNodes - 1),
                        isVirtualLeaf = false
                    });
                }

                Transform lastBone = linearBones[linearBones.Count - 1];
                float tipLen = linearBones.Count >= 2
                    ? Mathf.Min(leafExtensionLength, Vector3.Distance(lastBone.position, linearBones[linearBones.Count - 2].position))
                    : leafExtensionLength;
                tipLen = Mathf.Max(0.015f, tipLen);

                Vector3 tipDir = linearBones.Count >= 2
                    ? (lastBone.position - linearBones[linearBones.Count - 2].position).normalized
                    : -lastBone.up;
                Vector3 virtualTipWorld = lastBone.position + tipDir * tipLen;

                chain.nodes.Add(new HairNode
                {
                    bone = null,
                    worldPos = virtualTipWorld,
                    prevWorldPos = virtualTipWorld,
                    restLocalPos = headTransform.InverseTransformPoint(virtualTipWorld),
                    restLocalRot = Quaternion.identity,
                    segmentLength = tipLen,
                    tipFraction = 1f,
                    isVirtualLeaf = true
                });

                _chains.Add(chain);
            }

            _prevHeadPos = headTransform.position;
            _initialized = true;
            UpdateHeadCenterShaderProperty();
        }

        public void ResetToRestPose()
        {
            if (!_initialized) RebuildChains();
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!nodes[i].isVirtualLeaf && nodes[i].bone != null)
                        nodes[i].bone.localRotation = nodes[i].restLocalRot;
                    Vector3 restW = headTransform.TransformPoint(nodes[i].restLocalPos);
                    nodes[i].worldPos = restW;
                    nodes[i].prevWorldPos = restW;
                }
            }
            _prevHeadPos = headTransform.position;
            _impulseVelocity = Vector3.zero;
        }

        public void AddImpulse(Vector3 worldImpulse) => _impulseVelocity += worldImpulse;

        private void LateUpdate()
        {
            Simulate(Time.deltaTime, Time.time);
        }

        /// <summary>
        /// Deterministic simulation step callable from LateUpdate or PlayMode tests.
        /// </summary>
        public void Simulate(float deltaTime, float simTime)
        {
            if (!_initialized || _chains.Count == 0 || headTransform == null) return;

            float dt = Mathf.Min(deltaTime, 0.05f);
            if (dt <= 0.00001f) return;

            int substeps = Mathf.Clamp(solverSubsteps, 1, 6);
            float subDt = dt / substeps;
            float subDtSq = subDt * subDt;

            // Framerate-independent exponential damping calibrated to 60 Hz reference
            float dampRate = -Mathf.Log(1f - Mathf.Clamp(damping, 0.01f, 0.35f)) * 60f;
            float dampMul = Mathf.Exp(-dampRate * subDt);

            Vector3 totalHeadDelta = headTransform.position - _prevHeadPos;
            Vector3 subHeadDelta = totalHeadDelta / substeps;
            _prevHeadPos = headTransform.position;

            float azRad = fallbackWindAzimuthDeg * Mathf.Deg2Rad;
            Vector3 fallbackDir = new Vector3(Mathf.Cos(azRad), 0f, Mathf.Sin(azRad));

            // Restore rest rotations once before sampling animated root positions
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!nodes[i].isVirtualLeaf && nodes[i].bone != null)
                        nodes[i].bone.localRotation = nodes[i].restLocalRot;
                }
            }

            for (int step = 0; step < substeps; step++)
            {
                float stepTime = simTime - dt + (step + 1) * subDt;
                _impulseVelocity *= Mathf.Exp(-5.0f * subDt);

                for (int c = 0; c < _chains.Count; c++)
                {
                    StepChain(_chains[c], subDt, subDtSq, dampMul, subHeadDelta, fallbackDir, stepTime);
                }
            }

            // Rotation-only write-back (zero mesh stretch)
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                int count = nodes.Count;
                for (int i = 0; i < count - 1; i++)
                {
                    HairNode curr = nodes[i];
                    HairNode next = nodes[i + 1];
                    if (curr.isVirtualLeaf || curr.bone == null) continue;

                    Vector3 restChildPos = next.isVirtualLeaf
                        ? headTransform.TransformPoint(next.restLocalPos)
                        : next.bone.position;

                    Vector3 restDir = (restChildPos - curr.bone.position).normalized;
                    Vector3 simDir = (next.worldPos - curr.worldPos).normalized;

                    if (restDir.sqrMagnitude > 0.0001f && simDir.sqrMagnitude > 0.0001f)
                        curr.bone.rotation = Quaternion.FromToRotation(restDir, simDir) * curr.bone.rotation;
                }
            }

            UpdateHeadCenterShaderProperty();
        }

        private void StepChain(
            HairChain chain,
            float subDt,
            float subDtSq,
            float dampMul,
            Vector3 subHeadDelta,
            Vector3 fallbackDir,
            float stepTime)
        {
            var nodes = chain.nodes;
            int count = nodes.Count;
            float zoneMul = GetZoneStiffnessMultiplier(chain.zone);
            float effStiffness = Mathf.Clamp(stiffness * zoneMul, 0.05f, 0.98f);

            nodes[0].worldPos = nodes[0].bone.position;
            nodes[0].prevWorldPos = nodes[0].worldPos;

            for (int i = 1; i < count; i++)
            {
                HairNode node = nodes[i];
                Vector3 velocity = (node.worldPos - node.prevWorldPos) * dampMul;
                velocity -= subHeadDelta * (0.35f * headInertia * node.tipFraction);
                node.prevWorldPos = node.worldPos;

                Vector3 windAccel;
                if (windZone != null)
                {
                    windAccel = windZone.SampleWindAcceleration(node.worldPos, chain.seedPhase, stepTime)
                                * (0.18f + 0.82f * node.tipFraction * node.tipFraction);
                }
                else
                {
                    float gust = 0.65f + 0.35f * Mathf.Sin(stepTime * 2.3f + chain.seedPhase + i * 0.42f)
                               + fallbackTurbulence * 0.45f * Mathf.Sin(stepTime * 5.4f + chain.seedPhase * 1.9f);
                    windAccel = fallbackDir * (fallbackWindStrength * gust * (0.18f + 0.82f * node.tipFraction * node.tipFraction));
                }

                Vector3 restWorld = headTransform.TransformPoint(node.restLocalPos);
                for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                {
                    AetherStrandCollider col = colliders[colIdx];
                    if (col != null && col.enabled)
                        col.ResolveCollision(ref restWorld, collisionMargin);
                }

                float tipFalloff = Mathf.Pow(1f - node.tipFraction, 1.45f);
                float tipCube = node.tipFraction * node.tipFraction * node.tipFraction;
                Vector3 extAccel = windAccel * 0.64f
                                 + Vector3.down * (gravity * 0.14f * tipCube)
                                 + _impulseVelocity * (1.35f * (0.1f + 0.9f * node.tipFraction * node.tipFraction));

                node.worldPos += velocity + extAccel * subDtSq;

                // XPBD compliance-style unconditionally stable rest-pose tether
                float tetherCompliance = Mathf.Lerp(0.015f, 0.00025f, effStiffness) / Mathf.Max(0.12f, 0.18f + 0.82f * tipFalloff);
                float alpha = tetherCompliance / Mathf.Max(1e-7f, subDtSq);
                float tetherFactor = 1f / (1f + alpha);
                node.worldPos = Vector3.Lerp(node.worldPos, restWorld, tetherFactor);

                if (i <= 3)
                {
                    float kFade = i == 1 ? 0.42f : (i == 2 ? 0.22f : 0.09f);
                    float pinRate = kFade * effStiffness * (rootAnchor / 0.6f) * 45f;
                    float rootPin = Mathf.Clamp(1f - Mathf.Exp(-pinRate * subDt), 0f, 0.82f);
                    node.worldPos = Vector3.Lerp(node.worldPos, restWorld, rootPin);
                }
            }

            float bendAlpha = Mathf.Clamp01(bendingSmoothness) * 0.30f;
            for (int iter = 0; iter < solverIterations; iter++)
            {
                if (bendAlpha > 0.005f && iter < solverIterations - 1)
                {
                    for (int i = 1; i < count - 1; i++)
                    {
                        Vector3 restMidLocal = 0.5f * (nodes[i - 1].restLocalPos + nodes[i + 1].restLocalPos);
                        Vector3 restCurvWorld = headTransform.TransformVector(nodes[i].restLocalPos - restMidLocal);
                        Vector3 targetMid = 0.5f * (nodes[i - 1].worldPos + nodes[i + 1].worldPos) + restCurvWorld;
                        nodes[i].worldPos = Vector3.Lerp(nodes[i].worldPos, targetMid, bendAlpha);
                    }
                }

                for (int i = 1; i < count; i++)
                {
                    HairNode prev = nodes[i - 1];
                    HairNode curr = nodes[i];

                    Vector3 diff = curr.worldPos - prev.worldPos;
                    float dist = diff.magnitude;
                    if (dist > 0.00001f)
                    {
                        float err = (dist - curr.segmentLength) / dist;
                        if (i == 1)
                        {
                            curr.worldPos -= diff * err;
                        }
                        else
                        {
                            prev.worldPos += diff * (err * 0.20f);
                            curr.worldPos -= diff * (err * 0.80f);
                        }
                    }

                    bool hitCollider = false;
                    for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                    {
                        AetherStrandCollider col = colliders[colIdx];
                        if (col != null && col.enabled && col.ResolveCollision(ref curr.worldPos, collisionMargin))
                            hitCollider = true;
                    }

                    if (hitCollider && iter == solverIterations - 1)
                    {
                        // Zero-restitution sliding friction so hair drapes over shoulders without bouncing
                        curr.prevWorldPos = Vector3.Lerp(curr.prevWorldPos, curr.worldPos, 0.70f);
                    }
                }
            }
        }

        public AetherStrandAuditMetrics ComputeAudit()
        {
            var m = new AetherStrandAuditMetrics { totalChains = _chains.Count };
            if (!_initialized || _chains.Count == 0 || headTransform == null) return m;

            float tipSwaySumM = 0f;
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                int count = nodes.Count;

                for (int i = 1; i < count; i++)
                {
                    Vector3 p = nodes[i].worldPos;
                    for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                    {
                        var col = colliders[colIdx];
                        if (col != null && col.enabled && col.SignedDistance(p, collisionMargin * 0.5f) < -0.002f)
                        {
                            m.penetratingNodes++;
                            break;
                        }
                    }

                    float segLen = nodes[i].segmentLength;
                    if (segLen > 0.001f)
                    {
                        float d = Vector3.Distance(p, nodes[i - 1].worldPos);
                        float pct = Mathf.Abs(d - segLen) / segLen * 100f;
                        if (pct > m.maxStretchErrorPct) m.maxStretchErrorPct = pct;
                    }

                    if (i < count - 1)
                    {
                        Vector3 d0 = (p - nodes[i - 1].worldPos).normalized;
                        Vector3 d1 = (nodes[i + 1].worldPos - p).normalized;
                        float kink = Vector3.Angle(d0, d1);
                        if (kink > m.maxKinkAngleDeg) m.maxKinkAngleDeg = kink;
                    }
                }

                HairNode tip = nodes[count - 1];
                Vector3 tipRestW = headTransform.TransformPoint(tip.restLocalPos);
                tipSwaySumM += Vector3.Distance(tip.worldPos, tipRestW);
            }

            m.meanTipSwayCm = (tipSwaySumM / _chains.Count) * 100f;
            return m;
        }

        private HairZone ClassifyZone(Transform root)
        {
            string n = root.name.ToLowerInvariant();
            if (n.Contains("bang") || n.Contains("front")) return HairZone.Bangs;
            if (n.Contains("crown") || n.Contains("top")) return HairZone.Crown;
            if (n.Contains("side") || n.Contains("temple")) return HairZone.Sides;
            if (n.Contains("rear") || n.Contains("back") || n.Contains("nape")) return HairZone.Rear;

            Vector3 local = headTransform.InverseTransformPoint(root.position);
            if (local.z > 0.03f) return HairZone.Bangs;
            if (local.y > 0.14f && Mathf.Abs(local.x) < 0.055f) return HairZone.Crown;
            if (local.z < -0.03f) return HairZone.Rear;
            return HairZone.Sides;
        }

        private float GetZoneStiffnessMultiplier(HairZone zone)
        {
            switch (zone)
            {
                case HairZone.Bangs: return bangsStiffnessMul;
                case HairZone.Crown: return crownStiffnessMul;
                case HairZone.Sides: return sidesStiffnessMul;
                default:             return rearStiffnessMul;
            }
        }

        private void UpdateHeadCenterShaderProperty()
        {
            if (hairRenderer == null || headTransform == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Vector3 centerWS = headTransform.TransformPoint(headCenterLocalOffset);
            hairRenderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(IdHeadCenterWS, new Vector4(centerWS.x, centerWS.y, centerWS.z, 1f));
            hairRenderer.SetPropertyBlock(_mpb);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _chains == null) return;
            Gizmos.color = new Color(0.96f, 0.68f, 0.20f, 0.9f);
            foreach (HairChain chain in _chains)
            {
                for (int i = 0; i < chain.nodes.Count - 1; i++)
                {
                    Gizmos.DrawLine(chain.nodes[i].worldPos, chain.nodes[i + 1].worldPos);
                    Gizmos.DrawSphere(chain.nodes[i + 1].worldPos, 0.006f);
                }
            }
        }
    }
}
