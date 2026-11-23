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
    public struct PhysicsAuditResult
    {
        public int totalChains;
        public int colliderCount;
        public int penetratingNodes;
        public float maxKinkAngleDeg;
        public float meanTipSwayCm;
        public float maxStretchErrorPct;
        public int interLockClipCount;
        public int score;
        public string verdict;
    }

    /// <summary>
    /// Real-time XPBD / Verlet Hair Bone Chain Simulator for sculpted volumetric hair.
    /// Features substepped head-pose interpolation, framerate-independent exponential damping & root pinning,
    /// XPBD rest-pose compliance tethers, per-zone stiffness multipliers, and deterministic verification audit hooks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public class AetherStrandPhysics : MonoBehaviour
    {
        [Header("Rig References")]
        [Tooltip("Character Head bone transform used for inertia and rest-pose orientation.")]
        public Transform headTransform;

        [Tooltip("Root bone of each hair lock chain (auto-traverses children).")]
        public List<Transform> hairRootBones = new List<Transform>();

        [Tooltip("Head, face/jaw, neck, chest, and shoulder analytical colliders.")]
        public List<AetherStrandCollider> colliders = new List<AetherStrandCollider>();

        [Tooltip("Optional global wind controller.")]
        public AetherStrandWindZone windZone;

        [Tooltip("Optional hair renderer whose _HeadCenterWS shader property is synced each step.")]
        public Renderer hairRenderer;

        [Header("Silhouette & XPBD Physics")]
        [Range(0.05f, 0.95f)]
        [Tooltip("Rest-pose shape retention. High (0.60-0.85) holds sculpted layered/spiky silhouettes; low (0.25-0.45) flows freely.")]
        public float stiffness = 0.74f;

        [Tooltip("Downward gravity acceleration in m/s^2.")]
        public float gravity = 3.60f;

        [Range(0.01f, 0.35f)]
        [Tooltip("Framerate-independent velocity damping calibrated at 60 Hz.")]
        public float damping = 0.100f;

        [Range(0.0f, 1.5f)]
        [Tooltip("Lag/drag from character head movement.")]
        public float headInertia = 0.84f;

        [Range(0.0f, 0.95f)]
        [Tooltip("Rest-curvature-preserving bending smoothness. Eliminates accordion kinks across colliding bangs.")]
        public float bendingSmoothness = 0.64f;

        [Range(0.002f, 0.025f)]
        [Tooltip("Minimum surface clearance outside head/neck/chest/shoulder colliders (meters).")]
        public float collisionMargin = 0.0090f;

        [Header("Inter-Lock Hair-vs-Hair Separation")]
        [Tooltip("Prevents neighboring heavy hair clumps from merging through each other.")]
        public bool enableInterLockPush = true;
        [Range(0.005f, 0.045f)]
        public float interLockRadius = 0.016f;
        [Range(0.0f, 1.0f)]
        public float interLockStrength = 0.35f;

        [Range(0.1f, 1.0f)]
        [Tooltip("Upper scalp parting anchor strength across root bones 1..3.")]
        public float rootAnchor = 0.66f;

        [Range(1, 6)]
        [Tooltip("Integration substeps per frame for fast head turns and smooth collider sliding.")]
        public int solverSubsteps = 3;

        [Range(1, 6)]
        public int solverIterations = 4;

        [Tooltip("Virtual tip extension length beyond the final leaf bone (meters).")]
        public float leafExtensionLength = 0.06f;

        [Header("Per-Zone Stiffness Multipliers")]
        [Range(0.25f, 2.0f)] public float bangsStiffnessMultiplier = 1.15f;
        [Range(0.25f, 2.0f)] public float crownStiffnessMultiplier = 1.20f;
        [Range(0.25f, 2.0f)] public float sidesStiffnessMultiplier = 0.95f;
        [Range(0.25f, 2.0f)] public float rearStiffnessMultiplier = 0.85f;

        [Header("Fallback Local Wind")]
        [Tooltip("Default 0 so hair stays still indoors/in caves when no AetherStrandWindZone is active.")]
        public float fallbackWindStrength = 0f;
        public float fallbackTurbulence = 0.24f;
        public float fallbackWindAzimuthDeg = 28.0f;

        [Header("Debug Visualization")]
        public bool drawGizmos = true;

        private class HairNode
        {
            public Transform bone;
            public Vector3 worldPos;
            public Vector3 prevWorldPos;
            public Vector3 restLocalPos;
            public Vector3 restCurvatureLocal;
            public Quaternion restLocalRot;
            public Vector3 restLocalAimDir;
            public float restColliderAllowance;
            public float segmentLength;
            public float tipFraction;
            public float stiffnessWeight;
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
        private Quaternion _prevHeadRot = Quaternion.identity;
        private Vector3 _impulseVelocity;
        private MaterialPropertyBlock _mpb;
        private bool _initialized;

        private static readonly int HeadCenterWsId = Shader.PropertyToID("_HeadCenterWS");

        private void Awake()
        {
            if (headTransform == null) headTransform = transform;
            RebuildChains();
        }

        private static HairZone DetectZone(Transform root, Vector3 restLocalPos, Vector3 tipLocalPos)
        {
            if (root != null)
            {
                string n = root.name;
                if (n.IndexOf("Bangs", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Bangs;
                if (n.IndexOf("Crown", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Crown;
                if (n.IndexOf("Sides", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Sides;
                if (n.IndexOf("Rear", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Rear;
            }

            if (tipLocalPos.z > 0.08f && Mathf.Abs(tipLocalPos.x) < 0.20f) return HairZone.Bangs;
            if (restLocalPos.y > 0.16f && tipLocalPos.z > -0.10f) return HairZone.Crown;
            if (restLocalPos.z < -0.04f || tipLocalPos.z < -0.08f) return HairZone.Rear;
            return HairZone.Sides;
        }

        private float GetZoneStiffnessMultiplier(HairZone zone)
        {
            switch (zone)
            {
                case HairZone.Bangs: return bangsStiffnessMultiplier;
                case HairZone.Crown: return crownStiffnessMultiplier;
                case HairZone.Sides: return sidesStiffnessMultiplier;
                case HairZone.Rear:  return rearStiffnessMultiplier;
                default:             return 1.0f;
            }
        }

        [ContextMenu("Rebuild Hair Chains")]
        public void RebuildChains()
        {
            if (headTransform == null) headTransform = transform;
            _chains.Clear();
            int chainIndex = 0;

            foreach (Transform root in hairRootBones)
            {
                if (root == null) continue;
                var chain = new HairChain { seedPhase = (chainIndex++ * 1.618033f) * 12.5f };

                Transform current = root;
                var linearBones = new List<Transform>();
                while (current != null)
                {
                    linearBones.Add(current);
                    current = current.childCount > 0 ? current.GetChild(0) : null;
                }

                if (linearBones.Count < 1) continue;

                int totalNodes = linearBones.Count + 1;
                for (int i = 0; i < linearBones.Count; i++)
                {
                    Transform b = linearBones[i];
                    float segLen = i == 0 ? 0f : Vector3.Distance(b.position, linearBones[i - 1].position);
                    float tipFrac = (float)i / (totalNodes - 1);
                    chain.nodes.Add(new HairNode
                    {
                        bone = b,
                        worldPos = b.position,
                        prevWorldPos = b.position,
                        restLocalPos = headTransform.InverseTransformPoint(b.position),
                        restLocalRot = b.localRotation,
                        segmentLength = segLen,
                        tipFraction = tipFrac,
                        stiffnessWeight = Mathf.Clamp(0.18f + 0.82f * Mathf.Pow(1f - tipFrac, 1.35f), 0.12f, 1f),
                        isVirtualLeaf = false
                    });
                }

                Transform lastBone = linearBones[linearBones.Count - 1];
                Vector3 tipDir = linearBones.Count >= 2
                    ? (lastBone.position - linearBones[linearBones.Count - 2].position).normalized
                    : -lastBone.up;
                Vector3 virtualTipWorld = lastBone.position + tipDir * leafExtensionLength;

                chain.nodes.Add(new HairNode
                {
                    bone = null,
                    worldPos = virtualTipWorld,
                    prevWorldPos = virtualTipWorld,
                    restLocalPos = headTransform.InverseTransformPoint(virtualTipWorld),
                    restLocalRot = Quaternion.identity,
                    segmentLength = leafExtensionLength,
                    tipFraction = 1f,
                    stiffnessWeight = 0.12f,
                    isVirtualLeaf = true
                });

                for (int i = 0; i < chain.nodes.Count - 1; i++)
                {
                    HairNode curr = chain.nodes[i];
                    HairNode next = chain.nodes[i + 1];
                    if (curr.bone != null)
                    {
                        Vector3 nextPosW = next.worldPos;
                        Vector3 aimW = (nextPosW - curr.bone.position);
                        curr.restLocalAimDir = aimW.sqrMagnitude > 1e-8f
                            ? (Quaternion.Inverse(curr.bone.rotation) * aimW.normalized).normalized
                            : Vector3.down;
                    }
                }

                for (int i = 1; i < chain.nodes.Count; i++)
                {
                    HairNode n = chain.nodes[i];
                    float minSd = 0f;
                    for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                    {
                        var col = colliders[colIdx];
                        if (col != null && col.enabled)
                        {
                            float sd = col.SignedDistance(n.worldPos) - collisionMargin;
                            if (sd < minSd) minSd = sd;
                        }
                    }
                    // If a sculpted rest node sits inside a collider margin at bind time, allow its bind depth
                    // so colliders never pop crown roots upward into loops at rest.
                    n.restColliderAllowance = Mathf.Min(0f, minSd);
                }

                for (int i = 1; i < chain.nodes.Count - 1; i++)
                {
                    Vector3 midLocal = 0.5f * (chain.nodes[i - 1].restLocalPos + chain.nodes[i + 1].restLocalPos);
                    chain.nodes[i].restCurvatureLocal = chain.nodes[i].restLocalPos - midLocal;
                }

                chain.zone = DetectZone(root, chain.nodes[0].restLocalPos, chain.nodes[chain.nodes.Count - 1].restLocalPos);
                _chains.Add(chain);
            }

            _prevHeadPos = headTransform.position;
            _prevHeadRot = headTransform.rotation;
            _initialized = true;
        }

        [ContextMenu("Reset To Rest Pose")]
        public void ResetToRestPose()
        {
            if (headTransform == null) headTransform = transform;
            if (!_initialized) RebuildChains();

            _impulseVelocity = Vector3.zero;
            _prevHeadPos = headTransform.position;
            _prevHeadRot = headTransform.rotation;

            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    HairNode n = nodes[i];
                    if (!n.isVirtualLeaf && n.bone != null)
                        n.bone.localRotation = n.restLocalRot;
                    Vector3 restW = headTransform.TransformPoint(n.restLocalPos);
                    n.worldPos = restW;
                    n.prevWorldPos = restW;
                }
            }
        }

        public void AddImpulse(Vector3 worldImpulse) => _impulseVelocity += worldImpulse;

        private void LateUpdate()
        {
            Simulate(Time.deltaTime, Time.time);
        }

        /// <summary>
        /// Public deterministic simulation step callable from LateUpdate or automated verification suites.
        /// </summary>
        public void Simulate(float dt, float simTime)
        {
            if (!_initialized || _chains.Count == 0 || headTransform == null) return;

            float clampedDt = Mathf.Min(dt, 0.033f);
            if (clampedDt <= 0.00001f) return;

            int substeps = Mathf.Clamp(solverSubsteps, 1, 6);
            float subDt = clampedDt / substeps;
            float subDtSq = subDt * subDt;

            // Framerate-independent exponential damping rate calibrated at 60 Hz
            float clampedDamp = Mathf.Clamp(damping, 0.01f, 0.35f);
            float dampingRate = -Mathf.Log(1f - clampedDamp) * 60f;
            float dampMul = Mathf.Exp(-dampingRate * subDt);
            float impulseDecay = Mathf.Exp(-4.5f * subDt);

            Vector3 targetHeadPos = headTransform.position;
            Quaternion targetHeadRot = headTransform.rotation;

            float azRad = fallbackWindAzimuthDeg * Mathf.Deg2Rad;
            Vector3 fallbackDir = new Vector3(Mathf.Cos(azRad), 0f, Mathf.Sin(azRad));
            float bendAlpha = Mathf.Clamp01(bendingSmoothness) * 0.30f;

            // Restore rest bone rotations once so bone.position yields exact rest-pose directions
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                for (int i = 0; i < nodes.Count; i++)
                    if (!nodes[i].isVirtualLeaf && nodes[i].bone != null)
                        nodes[i].bone.localRotation = nodes[i].restLocalRot;
            }

            Vector3 headScale = headTransform.lossyScale;

            for (int sub = 0; sub < substeps; sub++)
            {
                float alpha0 = (float)sub / substeps;
                float alpha1 = (float)(sub + 1) / substeps;
                float subTime = simTime - clampedDt + subDt * (sub + 1);

                Vector3 subPrevPos = Vector3.Lerp(_prevHeadPos, targetHeadPos, alpha0);
                Vector3 subHeadPos = Vector3.Lerp(_prevHeadPos, targetHeadPos, alpha1);
                Quaternion subPrevRot = Quaternion.Slerp(_prevHeadRot, targetHeadRot, alpha0);
                Quaternion subHeadRot = Quaternion.Slerp(_prevHeadRot, targetHeadRot, alpha1);

                Matrix4x4 subPrevMat = Matrix4x4.TRS(subPrevPos, subPrevRot, headScale);
                Matrix4x4 subCurrMat = Matrix4x4.TRS(subHeadPos, subHeadRot, headScale);

                _impulseVelocity *= impulseDecay;

                for (int c = 0; c < _chains.Count; c++)
                {
                    HairChain chain = _chains[c];
                    var nodes = chain.nodes;
                    int count = nodes.Count;
                    float zoneStiff = Mathf.Clamp(stiffness * GetZoneStiffnessMultiplier(chain.zone), 0.04f, 0.96f);

                    // Root node pinned strictly to head scalp
                    Vector3 rootWorld = subCurrMat.MultiplyPoint3x4(nodes[0].restLocalPos);
                    nodes[0].worldPos = rootWorld;
                    nodes[0].prevWorldPos = rootWorld;

                    // 1. Substepped Verlet Integration with Rigid-Follow Inertia & Wind
                    for (int i = 1; i < count; i++)
                    {
                        HairNode node = nodes[i];
                        Vector3 prevTargetW = subPrevMat.MultiplyPoint3x4(node.restLocalPos);
                        Vector3 currTargetW = subCurrMat.MultiplyPoint3x4(node.restLocalPos);
                        Vector3 targetDelta = currTargetW - prevTargetW;

                        // Separate linear sprint translation from angular head rotation:
                        // clamp linear drag speed to <= 1.6 m/s so 5 m/s sprinting doesn't strip hair backward.
                        Vector3 linDelta = subHeadPos - subPrevPos;
                        Vector3 rotDelta = targetDelta - linDelta;
                        float maxLinDrag = 1.6f * subDt;
                        float linMag = linDelta.magnitude;
                        Vector3 rigidLinCarry = linMag > maxLinDrag ? linDelta * ((linMag - maxLinDrag) / linMag) : Vector3.zero;
                        Vector3 dragDelta = (linDelta - rigidLinCarry) + rotDelta;

                        Vector3 velocity = (node.worldPos - node.prevWorldPos) * dampMul;
                        float followRigid = Mathf.Clamp(1f - headInertia * 0.42f * Mathf.Pow(node.tipFraction, 1.35f), 0.40f, 1f);
                        Vector3 shiftedPos = node.worldPos + rigidLinCarry + dragDelta * followRigid;
                        node.prevWorldPos = shiftedPos;

                        float tipSq = node.tipFraction * node.tipFraction;
                        float tipCube = tipSq * node.tipFraction;

                        Vector3 windAccel;
                        if (windZone != null)
                        {
                            windAccel = windZone.SampleWindAcceleration(shiftedPos, chain.seedPhase, subTime)
                                        * (0.14f + 0.86f * tipSq);
                        }
                        else
                        {
                            float gust = 0.65f + 0.35f * Mathf.Sin(subTime * 2.3f + chain.seedPhase + i * 0.42f)
                                       + fallbackTurbulence * 0.45f * Mathf.Sin(subTime * 5.4f + chain.seedPhase * 1.9f);
                            windAccel = fallbackDir * (fallbackWindStrength * gust * (0.14f + 0.86f * tipSq));
                        }

                        Vector3 extAccel = windAccel * 0.62f
                                           + Vector3.down * (gravity * 0.12f * tipCube)
                                           + _impulseVelocity * (1.35f * (0.1f + 0.9f * tipSq));

                        node.worldPos = shiftedPos + velocity + extAccel * subDtSq;

                        // Framerate-independent exponential root anchor pinning
                        if (i <= 3)
                        {
                            float kFade = i == 1 ? 0.42f : (i == 2 ? 0.22f : 0.09f);
                            float rootPinRate = kFade * zoneStiff * (rootAnchor / 0.6f) * 60f;
                            float rootPin = Mathf.Clamp(1f - Mathf.Exp(-rootPinRate * subDt), 0f, 0.78f);
                            node.worldPos = Vector3.Lerp(node.worldPos, currTargetW, rootPin);
                        }
                    }

                    // 2. XPBD Rest-Pose Compliance Tether + Rest-Curvature Anti-Kink + Distance & Collider Projection
                    int iters = Mathf.Clamp(solverIterations, 1, 6);
                    for (int iter = 0; iter < iters; iter++)
                    {
                        bool isFinalIter = iter == iters - 1;

                        // Rest-curvature-preserving anti-kink smoothing
                        if (bendAlpha > 0.005f || !isFinalIter)
                        {
                            for (int i = 1; i < count - 1; i++)
                            {
                                Vector3 a = nodes[i].worldPos - nodes[i - 1].worldPos;
                                Vector3 b = nodes[i + 1].worldPos - nodes[i].worldPos;
                                float lenA = a.magnitude;
                                float lenB = b.magnitude;
                                float alpha = !isFinalIter ? bendAlpha : 0f;
                                float curvScale = 1f;
                                if (lenA > 0.0001f && lenB > 0.0001f)
                                {
                                    float cosAng = Vector3.Dot(a, b) / (lenA * lenB);
                                    if (cosAng < 0.935f)
                                    {
                                        float u = Mathf.Clamp01((0.935f - cosAng) / 0.17f);
                                        float smoothU = u * u * (3f - 2f * u);
                                        alpha = Mathf.Max(alpha, 0.76f * smoothU);
                                        curvScale = 1f - smoothU;
                                    }
                                }
                                if (alpha > 0.005f)
                                {
                                    Vector3 curvW = (subHeadRot * nodes[i].restCurvatureLocal) * curvScale;
                                    Vector3 targetMid = 0.5f * (nodes[i - 1].worldPos + nodes[i + 1].worldPos) + curvW;
                                    nodes[i].worldPos = Vector3.Lerp(nodes[i].worldPos, targetMid, alpha);
                                }
                            }
                        }

                        // XPBD compliant rest-pose tether + inextensible segment length + analytical collision
                        for (int i = 1; i < count; i++)
                        {
                            HairNode prev = nodes[i - 1];
                            HairNode curr = nodes[i];

                            // XPBD compliance rest-pose tether (unconditionally stable for any subDt & stiffness)
                            Vector3 restWorld = subCurrMat.MultiplyPoint3x4(curr.restLocalPos);
                            for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                            {
                                AetherStrandCollider col = colliders[colIdx];
                                if (col != null && col.enabled)
                                    col.ResolveCollision(ref restWorld, Mathf.Max(0f, collisionMargin + curr.restColliderAllowance));
                            }

                            float shapedStiff = Mathf.Clamp01(zoneStiff * curr.stiffnessWeight);
                            float compliance = Mathf.Lerp(0.085f, 0.00035f, shapedStiff * shapedStiff);
                            float alphaTilde = (compliance / subDtSq) * iters;
                            float xpbdLambda = 1f / (1f + alphaTilde);
                            curr.worldPos += (restWorld - curr.worldPos) * xpbdLambda;

                            // Distance constraint projection
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

                            // Inelastic collider projection
                            bool hitCollider = false;
                            for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                            {
                                AetherStrandCollider col = colliders[colIdx];
                                if (col != null && col.enabled)
                                {
                                    Vector3 before = curr.worldPos;
                                    col.ResolveCollision(ref curr.worldPos, Mathf.Max(0f, collisionMargin + curr.restColliderAllowance));
                                    if ((curr.worldPos - before).sqrMagnitude > 1e-8f) hitCollider = true;
                                }
                            }
                            if (hitCollider && isFinalIter)
                            {
                                curr.prevWorldPos = Vector3.Lerp(curr.prevWorldPos, curr.worldPos, 0.86f);
                            }
                        }
                    }
                }
                // 2b. Lightweight inter-lock hair-vs-hair separation (heavy clumps push neighbors instead of merging)
                if (enableInterLockPush && interLockStrength > 0.001f && _chains.Count > 1)
                {
                    float minD = Mathf.Max(0.004f, interLockRadius);
                    float minDSq = minD * minD;
                    float pushK = 0.5f * Mathf.Clamp01(interLockStrength);
                    int nChains = _chains.Count;
                    for (int a = 0; a < nChains; a++)
                    {
                        var nodesA = _chains[a].nodes;
                        int cntA = nodesA.Count;
                        for (int b = a + 1; b < nChains; b++)
                        {
                            var nodesB = _chains[b].nodes;
                            int cntB = nodesB.Count;
                            if ((nodesA[0].worldPos - nodesB[0].worldPos).sqrMagnitude > 0.012f) continue;
                            int maxI = Mathf.Min(cntA, cntB);
                            for (int i = 2; i < maxI; i++)
                            {
                                Vector3 d = nodesB[i].worldPos - nodesA[i].worldPos;
                                float dSq = d.sqrMagnitude;
                                if (dSq < minDSq && dSq > 1e-8f)
                                {
                                    float dist = Mathf.Sqrt(dSq);
                                    Vector3 corr = d * (((minD - dist) / dist) * pushK);
                                    nodesA[i].worldPos -= corr;
                                    nodesB[i].worldPos += corr;
                                }
                            }
                        }
                    }
                }
            }

            _prevHeadPos = targetHeadPos;
            _prevHeadRot = targetHeadRot;

            // 3. Aim Bone Transforms Along Simulated Chains (uses cached restLocalAimDir so parent rotations never invert tips)
            for (int c = 0; c < _chains.Count; c++)
            {
                HairChain chain = _chains[c];
                var nodes = chain.nodes;
                int count = nodes.Count;
                float zoneMaxDeg = (chain.zone == HairZone.Bangs || chain.zone == HairZone.Crown) ? 38f : 58f;

                for (int i = 0; i < count - 1; i++)
                {
                    HairNode curr = nodes[i];
                    HairNode next = nodes[i + 1];
                    if (curr.isVirtualLeaf || curr.bone == null) continue;

                    Vector3 currentAimDir = (curr.bone.rotation * curr.restLocalAimDir).normalized;
                    Vector3 simDir = (next.worldPos - curr.worldPos).normalized;

                    if (currentAimDir.sqrMagnitude > 0.0001f && simDir.sqrMagnitude > 0.0001f)
                    {
                        curr.bone.rotation = Quaternion.FromToRotation(currentAimDir, simDir) * curr.bone.rotation;
                        // Hard swing clamp relative to restLocalRot (tighter at root i==0 so scalp locks never lift)
                        float maxSwing = (i == 0) ? (zoneMaxDeg * 0.45f) : zoneMaxDeg;
                        curr.bone.localRotation = Quaternion.RotateTowards(curr.restLocalRot, curr.bone.localRotation, maxSwing);
                    }
                }
            }

            // 4. Sync optional _HeadCenterWS property block for spherical normal smoothing
            if (hairRenderer != null && headTransform != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                hairRenderer.GetPropertyBlock(_mpb);
                Vector3 centerWS = headTransform.TransformPoint(new Vector3(0f, 0.12f, -0.01f));
                _mpb.SetVector(HeadCenterWsId, new Vector4(centerWS.x, centerWS.y, centerWS.z, 0f));
                hairRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Evaluates real-time geometric quality telemetry across all active hair bone chains.
        /// </summary>
        public PhysicsAuditResult ComputeAudit()
        {
            int activeColliders = 0;
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (colliders[i] != null && colliders[i].enabled) activeColliders++;
                }
            }

            var result = new PhysicsAuditResult
            {
                totalChains = _chains.Count,
                colliderCount = activeColliders
            };
            if (!_initialized || _chains.Count == 0 || headTransform == null) return result;

            int penetrating = 0;
            float maxKink = 0f;
            float tipSwaySumCm = 0f;
            float maxStretchPct = 0f;

            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                int count = nodes.Count;
                if (count < 2) continue;

                for (int i = 1; i < count; i++)
                {
                    HairNode curr = nodes[i];
                    HairNode prev = nodes[i - 1];

                    if (curr.segmentLength > 0.0001f)
                    {
                        float dist = Vector3.Distance(curr.worldPos, prev.worldPos);
                        float stretchPct = Mathf.Abs(dist - curr.segmentLength) / curr.segmentLength * 100f;
                        if (stretchPct > maxStretchPct) maxStretchPct = stretchPct;
                    }

                    if (i < count - 1)
                    {
                        Vector3 a = curr.worldPos - prev.worldPos;
                        Vector3 b = nodes[i + 1].worldPos - curr.worldPos;
                        if (a.sqrMagnitude > 1e-8f && b.sqrMagnitude > 1e-8f)
                        {
                            float ang = Vector3.Angle(a, b);
                            if (ang > maxKink) maxKink = ang;
                        }
                    }

                    bool isPenetrating = false;
                    for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                    {
                        AetherStrandCollider col = colliders[colIdx];
                        if (col != null && col.enabled && col.SignedDistance(curr.worldPos) < -0.001f)
                        {
                            isPenetrating = true;
                            break;
                        }
                    }
                    if (isPenetrating) penetrating++;
                }

                HairNode tip = nodes[count - 1];
                Vector3 restTipWS = headTransform.TransformPoint(tip.restLocalPos);
                tipSwaySumCm += Vector3.Distance(tip.worldPos, restTipWS) * 100f;
            }

            result.penetratingNodes = penetrating;
            result.maxKinkAngleDeg = maxKink;
            result.meanTipSwayCm = _chains.Count > 0 ? tipSwaySumCm / _chains.Count : 0f;
            result.maxStretchErrorPct = maxStretchPct;

            int clips = 0;
            float clipThrSq = (interLockRadius * 0.65f) * (interLockRadius * 0.65f);
            for (int a = 0; a < _chains.Count; a++)
            {
                var na = _chains[a].nodes;
                for (int b = a + 1; b < _chains.Count; b++)
                {
                    var nb = _chains[b].nodes;
                    if ((na[0].worldPos - nb[0].worldPos).sqrMagnitude > 0.012f) continue;
                    int maxI = Mathf.Min(na.Count, nb.Count);
                    for (int i = 2; i < maxI; i++)
                        if ((nb[i].worldPos - na[i].worldPos).sqrMagnitude < clipThrSq) clips++;
                }
            }
            result.interLockClipCount = clips;

            int sc = 100;
            if (result.totalChains == 0) sc -= 50;
            if (result.colliderCount == 0) sc -= 25;
            sc -= Mathf.Min(40, result.penetratingNodes * 6);
            if (result.maxKinkAngleDeg > 28f) sc -= Mathf.Min(25, Mathf.RoundToInt((result.maxKinkAngleDeg - 28f) * 0.8f));
            if (result.maxStretchErrorPct > 3.5f) sc -= Mathf.Min(25, Mathf.RoundToInt((result.maxStretchErrorPct - 3.5f) * 2.0f));
            sc -= Mathf.Min(15, clips);
            result.score = Mathf.Clamp(sc, 0, 100);
            result.verdict = (result.score >= 85 && result.penetratingNodes == 0 && result.colliderCount > 0)
                ? "PASS" : (result.score >= 65 ? "WARN" : "FAIL");
            return result;
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
                    Gizmos.DrawSphere(chain.nodes[i + 1].worldPos, 0.007f);
                }
            }
        }
    }
}
