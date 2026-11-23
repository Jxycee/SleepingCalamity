#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AetherStrand.Editor
{
    public enum HairArchetype
    {
        LayeredShag,
        SpikyHero,
        LongCascade,
        WolfCut,
        SleekBob,
        AsymCascade,
        DolphinLong,
        HalfUpCascade,
        SweptBack,
        HighPonytail,
        TwinTails,
        SideSweptFringe,
        CoiledAfro,
        TaperFade
    }

    public enum HeroPresetId
    {
        PartedShag,
        SpikyCrown,
        LongCascade,
        SmokySpiky,
        WolfCut
    }

    [Serializable]
    public class HairBuilderState
    {
        public HairArchetype archetype = HairArchetype.LayeredShag;
        public float partPosition = 0.42f;   // -1..+1
        public float bangsLength = 0.64f;    // 0.25..1.0
        public float bangsSweep = -0.45f;    // -1..+1
        public float crownVolume = 0.45f;    // 0..1
        public float spikeLift = 0.14f;      // 0..1
        public float sideLength = 0.68f;     // 0.2..1.0
        public float backLength = 0.62f;     // 0.25..1.0
        public float layerShag = 0.68f;      // 0..1
        public float curlAmplitude = 0.0f;   // 0..1 (waves, coils, afro)
        public float density = 1.0f;         // 0.65..1.35
        public float strandWidth = 0.96f;    // 0.70..1.35
        public int seed = 202619;

        // Character Head-space alignment (exact skull-center-to-skull-center default for MetaPerson)
        public float overallScale = 1.0f;
        public Vector3 headLocalOffset = new Vector3(0f, 0.005f, 0.032f);
        public int targetChainCount = 44;
        public int bonesPerChain = 5;
    }

    public struct HeroPresetDef
    {
        public HeroPresetId Id;
        public string Name;
        public string Subtitle;
        public string RootHex, BaseHex, TipHex, Spec1Hex, Spec2Hex;
        public float Stiffness, Gravity, Damping, Inertia, Bending, RootAnchor;
        public HairBuilderState Builder;
    }

    /// <summary>
    /// Full C# procedural sculpted hair builder ported from AetherStrand's web engine (hairBuilder.ts).
    /// Generates all 5 hero presets + 14 archetypes directly in Unity Head-local meters with exact
    /// skull-center alignment (fixes the chin-vs-skull-base offset bug).
    /// </summary>
    public static class AetherStrandProceduralBuilder
    {
        // Web engine anatomical skull ellipsoid (bust space) mapped cleanly to real human MetaPerson meters (1/3 scale)
        private static readonly Vector3 WebHeadCenter = new Vector3(0f, 0.425f, -0.01f);
        private const float WebScalpRx = 0.282f;
        private const float WebScalpRy = 0.318f;
        private const float WebScalpRz = 0.310f;
        private const float BustToHumanScale = 0.3333f; // 0.285 bust cranium -> 0.095 m real human cranium
        private static readonly Vector3 MetaPersonSkullCenter = new Vector3(0f, 0.112f, 0.004f);
        private const int NodesPerLock = 12;

        private static readonly float[] ProfileU = { 0.00f, 0.18f, 0.36f, 0.50f, 0.64f, 0.82f, 1.00f };
        private static readonly float[] ProfileX = { 0.00f, 0.76f, 1.00f, 0.00f, -1.00f, -0.76f, 0.00f };
        private static readonly float[] ProfileY = { 1.00f, 0.54f, -0.02f, -0.26f, -0.02f, 0.54f, 1.00f };

        public class ProceduralLock
        {
            public string zone;
            public float width;
            public float thickness;
            public float seed;
            public float layerAO;
            public bool isBaseShell;
            public Vector3[] points; // Real-meter Unity Head-local space (+X right, +Y up, +Z front)
        }

        public static readonly HeroPresetDef[] HeroPresets =
        {
            new HeroPresetDef
            {
                Id = HeroPresetId.PartedShag,
                Name = "Parted Shag",
                Subtitle = "Parted chestnut shag & wispy tips",
                RootHex = "#1e1815", BaseHex = "#3a2f28", TipHex = "#54463c", Spec1Hex = "#d8cfc8", Spec2Hex = "#8c7666",
                Stiffness = 0.74f, Gravity = 3.8f, Damping = 0.095f, Inertia = 0.85f, Bending = 0.54f, RootAnchor = 0.64f,
                Builder = new HairBuilderState
                {
                    archetype = HairArchetype.LayeredShag, partPosition = 0.42f, bangsLength = 0.72f, bangsSweep = -0.48f,
                    crownVolume = 0.45f, spikeLift = 0.14f, sideLength = 0.66f, backLength = 0.60f, layerShag = 0.68f,
                    curlAmplitude = 0f, density = 1.0f, strandWidth = 0.96f, seed = 202619
                }
            },
            new HeroPresetDef
            {
                Id = HeroPresetId.SpikyCrown,
                Name = "Spiky Crown",
                Subtitle = "Sculpted layered golden spikes",
                RootHex = "#5c401a", BaseHex = "#b3893b", TipHex = "#d4b05d", Spec1Hex = "#fff0c7", Spec2Hex = "#c48f2e",
                Stiffness = 0.80f, Gravity = 2.8f, Damping = 0.105f, Inertia = 0.76f, Bending = 0.62f, RootAnchor = 0.70f,
                Builder = new HairBuilderState
                {
                    archetype = HairArchetype.SpikyHero, partPosition = 0.15f, bangsLength = 0.56f, bangsSweep = 0.35f,
                    crownVolume = 0.78f, spikeLift = 0.92f, sideLength = 0.52f, backLength = 0.58f, layerShag = 0.85f,
                    curlAmplitude = 0f, density = 1.02f, strandWidth = 1.04f, seed = 199707
                }
            },
            new HeroPresetDef
            {
                Id = HeroPresetId.LongCascade,
                Name = "Long Cascade",
                Subtitle = "Parted long flowing silver tresses",
                RootHex = "#414957", BaseHex = "#919cab", TipHex = "#c2ccd9", Spec1Hex = "#f2f6ff", Spec2Hex = "#92a6c4",
                Stiffness = 0.56f, Gravity = 5.0f, Damping = 0.125f, Inertia = 0.86f, Bending = 0.56f, RootAnchor = 0.64f,
                Builder = new HairBuilderState
                {
                    archetype = HairArchetype.LongCascade, partPosition = 0.0f, bangsLength = 0.78f, bangsSweep = 0.0f,
                    crownVolume = 0.48f, spikeLift = 0.05f, sideLength = 0.92f, backLength = 0.96f, layerShag = 0.36f,
                    curlAmplitude = 0f, density = 1.05f, strandWidth = 1.08f, seed = 999907
                }
            },
            new HeroPresetDef
            {
                Id = HeroPresetId.SmokySpiky,
                Name = "Smoky Spiky",
                Subtitle = "Charcoal layered spikes & wispy nape",
                RootHex = "#090a0c", BaseHex = "#25272b", TipHex = "#4c5057", Spec1Hex = "#c8ccd2", Spec2Hex = "#5c6068",
                Stiffness = 0.74f, Gravity = 3.6f, Damping = 0.100f, Inertia = 0.84f, Bending = 0.64f, RootAnchor = 0.66f,
                Builder = new HairBuilderState
                {
                    archetype = HairArchetype.SpikyHero, partPosition = -0.10f, bangsLength = 0.66f, bangsSweep = -0.35f,
                    crownVolume = 0.44f, spikeLift = 0.62f, sideLength = 0.70f, backLength = 0.68f, layerShag = 0.72f,
                    curlAmplitude = 0f, density = 1.04f, strandWidth = 0.96f, seed = 201615
                }
            },
            new HeroPresetDef
            {
                Id = HeroPresetId.WolfCut,
                Name = "Wolf Cut",
                Subtitle = "Feathered auburn fringe & wispy tail",
                RootHex = "#2e100c", BaseHex = "#69251c", TipHex = "#8f392a", Spec1Hex = "#ebbba0", Spec2Hex = "#a33c24",
                Stiffness = 0.58f, Gravity = 4.6f, Damping = 0.105f, Inertia = 0.88f, Bending = 0.52f, RootAnchor = 0.60f,
                Builder = new HairBuilderState
                {
                    archetype = HairArchetype.WolfCut, partPosition = 0.38f, bangsLength = 0.60f, bangsSweep = -0.32f,
                    crownVolume = 0.52f, spikeLift = 0.18f, sideLength = 0.66f, backLength = 0.84f, layerShag = 0.78f,
                    curlAmplitude = 0f, density = 1.0f, strandWidth = 1.0f, seed = 202013
                }
            }
        };

        public static List<ProceduralLock> BuildLocks(HairBuilderState cfg)
        {
            var locks = new List<ProceduralLock>(360);
            uint rngState = (uint)Mathf.Max(1, cfg.seed);
            Func<float> rand = () =>
            {
                rngState = rngState * 1664525u + 1013904223u;
                return (float)(rngState / 4294967296.0);
            };

            float dens = Mathf.Clamp(cfg.density, 0.55f, 1.40f);
            float wMul = Mathf.Clamp(cfg.strandWidth, 0.65f, 1.45f);
            float scale = BustToHumanScale * Mathf.Clamp(cfg.overallScale, 0.65f, 1.50f);
            float partX = Mathf.Clamp(cfg.partPosition, -1f, 1f) * 0.11f;
            bool isAfro = cfg.archetype == HairArchetype.CoiledAfro;
            bool isFade = cfg.archetype == HairArchetype.TaperFade;
            bool isPonytail = cfg.archetype == HairArchetype.HighPonytail;
            bool isTwinTails = cfg.archetype == HairArchetype.TwinTails;
            bool isSweptBack = cfg.archetype == HairArchetype.SweptBack;
            bool isBob = cfg.archetype == HairArchetype.SleekBob;
            float curl = isAfro ? Mathf.Max(0.75f, cfg.curlAmplitude) : cfg.curlAmplitude;

            // Helper: convert bust-space point into real-meter MetaPerson Head-local space
            Vector3 ToHuman(Vector3 bustPt)
            {
                return (bustPt - WebHeadCenter) * scale + MetaPersonSkullCenter + cfg.headLocalOffset;
            }

            void AddSculpted(
                string zone, float th0, float th1, float ph0, float ph1,
                float baseLift, float midArch, float tipFlick, float tipDrop,
                float sCurve, float napeHug, float widthBust, float thickBust,
                float layerAO, bool isBaseShell)
            {
                Vector3[] bustPts = BuildSculptedLock(th0, th1, ph0, ph1, baseLift, midArch, tipFlick, tipDrop, sCurve, napeHug, curl, rand());
                var pts = new Vector3[bustPts.Length];
                for (int i = 0; i < bustPts.Length; i++) pts[i] = ToHuman(bustPts[i]);
                locks.Add(new ProceduralLock
                {
                    zone = zone,
                    width = widthBust * scale * wMul,
                    thickness = thickBust * scale * (isAfro ? 1.45f : 1.0f),
                    seed = rand(),
                    layerAO = layerAO,
                    isBaseShell = isBaseShell,
                    points = pts
                });
            }

            // 1. Rigid Scalp Base-Shell Cap (44 locks — eliminates scalp gaps, weighted 100% to Head)
            int baseCount = 44;
            for (int i = 0; i < baseCount; i++)
            {
                float az = -Mathf.PI + (i / (float)baseCount) * Mathf.PI * 2f + (rand() - 0.5f) * 0.08f;
                bool isFront = Mathf.Cos(az) > 0.35f;
                float phEnd = isFront ? 1.18f : (isFade ? 1.45f : 1.92f);
                AddSculpted(
                    isFront ? "Bangs" : "Rear",
                    az, az + (rand() - 0.5f) * 0.12f,
                    0.08f + rand() * 0.14f, phEnd,
                    0.008f, 0.014f, 0.0f, 0.0f, 0f, 1.0f,
                    0.092f, 0.015f, 0.62f, true);
            }

            // 2. Front Bangs (parted, swept, framing brow/temples)
            int bangCount = Mathf.RoundToInt((isFade ? 28 : 56) * dens);
            for (int i = 0; i < bangCount; i++)
            {
                float u = (i + 0.5f) / bangCount;
                float side = u < (0.5f + cfg.partPosition * 0.25f) ? -1f : 1f;
                float localU = rand();
                float th0 = partX * 2.2f + side * (0.06f + localU * 0.92f);
                float sweep = isSweptBack ? side * 0.85f : (cfg.bangsSweep * 0.42f + side * 0.18f);
                float th1 = th0 + sweep + (rand() - 0.5f) * 0.16f;
                float ph0 = 0.14f + localU * 0.30f;
                float bangLen = isFade ? 0.34f : (isAfro ? 0.46f : cfg.bangsLength);
                float ph1 = Mathf.Lerp(1.02f, 1.72f, bangLen) + (rand() - 0.5f) * 0.18f * cfg.layerShag;
                float arch = 0.022f + cfg.crownVolume * 0.038f + (isAfro ? 0.055f : 0f);
                float flick = (0.015f + cfg.spikeLift * 0.095f) * (0.5f + 0.5f * rand());

                AddSculpted(
                    "Bangs", th0, th1, ph0, ph1,
                    0.018f + (i % 2) * 0.010f, arch, flick, 0.015f * bangLen,
                    (rand() - 0.5f) * 0.12f, 0.85f,
                    0.050f + rand() * 0.018f, 0.014f + rand() * 0.006f,
                    0.88f + (i % 2) * 0.08f, false);
            }

            // 3. Crown Volume & Hero Spikes
            int crownCount = Mathf.RoundToInt((isAfro ? 72 : 52) * dens);
            for (int i = 0; i < crownCount; i++)
            {
                float az = (i / (float)crownCount) * Mathf.PI * 2f + (rand() - 0.5f) * 0.14f;
                float ph0 = 0.06f + rand() * 0.36f;
                float ph1 = isAfro
                    ? 0.95f + rand() * 0.55f
                    : (isFade ? 0.82f + rand() * 0.35f : 1.25f + cfg.layerShag * 0.48f + rand() * 0.25f);
                float arch = 0.028f + cfg.crownVolume * 0.055f + (isAfro ? 0.075f : 0f);
                float flick = 0.022f + cfg.spikeLift * 0.145f * (0.6f + 0.6f * rand());
                AddSculpted(
                    "Crown", az, az + (rand() - 0.5f) * 0.28f, ph0, ph1,
                    0.024f + rand() * 0.014f, arch, flick, 0.01f,
                    (rand() - 0.5f) * 0.14f, 0.9f,
                    0.054f + rand() * 0.020f, 0.015f + rand() * 0.007f,
                    0.92f + rand() * 0.08f, false);
            }

            // 4. Sides & Temple Layers
            int sideCount = Mathf.RoundToInt((isFade ? 24 : 64) * dens);
            for (int i = 0; i < sideCount; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float u = rand();
                float th0 = side * (0.95f + u * 0.90f);
                float th1 = th0 + (isSweptBack ? side * 0.32f : -side * 0.08f) + (rand() - 0.5f) * 0.14f;
                float ph0 = 0.25f + rand() * 0.58f;
                float sLen = isFade ? 0.26f : (isBob ? 0.62f : cfg.sideLength);
                float ph1 = Mathf.Lerp(1.38f, 2.55f, sLen) + (rand() - 0.5f) * 0.24f * cfg.layerShag;
                float arch = 0.018f + cfg.crownVolume * 0.032f + (isAfro ? 0.065f : 0f);
                float flick = (0.018f + cfg.spikeLift * 0.105f) * (isBob ? 0.2f : 1.0f);

                AddSculpted(
                    "Sides", th0, th1, ph0, ph1,
                    0.018f + (i % 3) * 0.008f, arch, flick, 0.03f * sLen,
                    (rand() - 0.5f) * 0.10f, isBob ? 1.25f : 0.95f,
                    0.056f + rand() * 0.018f, 0.014f + rand() * 0.006f,
                    0.82f + (i % 3) * 0.07f, false);
            }

            // 5. Rear & Nape Layers
            int rearCount = Mathf.RoundToInt((isFade ? 36 : 112) * dens);
            for (int i = 0; i < rearCount; i++)
            {
                float u = (i + 0.5f) / rearCount;
                float th0 = Mathf.Lerp(1.65f, Mathf.PI * 2f - 1.65f, u) + (rand() - 0.5f) * 0.12f;
                float th1 = th0 + (rand() - 0.5f) * 0.18f;
                float ph0 = 0.22f + rand() * 0.85f;
                float bLen = isFade ? 0.22f : (isBob ? 0.58f : cfg.backLength);
                float ph1 = Mathf.Lerp(1.42f, 2.95f, bLen) + (rand() - 0.5f) * 0.30f * cfg.layerShag;
                float arch = 0.020f + cfg.crownVolume * 0.036f + (isAfro ? 0.065f : 0f);
                float flick = (0.020f + cfg.spikeLift * 0.110f) * (isBob ? 0.15f : 1.0f);

                AddSculpted(
                    "Rear", th0, th1, ph0, ph1,
                    0.016f + (i % 3) * 0.009f, arch, flick, 0.045f * bLen,
                    (rand() - 0.5f) * 0.12f, 1.05f,
                    0.062f + rand() * 0.022f, 0.015f + rand() * 0.006f,
                    0.76f + (i % 3) * 0.09f, false);
            }

            // 6. Optional Gathered Tail Locks (Ponytail / Twin-Tails)
            if (isPonytail || isTwinTails)
            {
                int tailCount = Mathf.RoundToInt(36 * dens);
                int tails = isTwinTails ? 2 : 1;
                for (int tIdx = 0; tIdx < tails; tIdx++)
                {
                    float sign = isTwinTails ? (tIdx == 0 ? -1f : 1f) : 0f;
                    float anchorTh = Mathf.PI + sign * 0.72f;
                    float anchorPh = isPonytail ? 0.62f : 0.78f;
                    Vector3 outDir = new Vector3(sign * 0.45f, 0.25f, -0.88f).normalized;
                    for (int i = 0; i < tailCount / tails; i++)
                    {
                        float spreadX = (rand() - 0.5f) * 0.16f;
                        float spreadZ = (rand() - 0.5f) * 0.12f;
                        Vector3[] bustPts = BuildGatheredTailLock(anchorTh, anchorPh, outDir, spreadX, spreadZ, 0.52f + cfg.backLength * 0.38f, 0.14f, 0.035f, rand() * 6.28f);
                        var pts = new Vector3[bustPts.Length];
                        for (int k = 0; k < bustPts.Length; k++) pts[k] = ToHuman(bustPts[k]);
                        locks.Add(new ProceduralLock
                        {
                            zone = "Rear",
                            width = 0.056f * scale * wMul,
                            thickness = 0.015f * scale,
                            seed = rand(),
                            layerAO = 0.85f + (i % 2) * 0.10f,
                            isBaseShell = false,
                            points = pts
                        });
                    }
                }
            }

            return locks;
        }

        private static Vector3 SkullSurfacePoint(float theta, float phi, float lift)
        {
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);
            return new Vector3(
                WebHeadCenter.x + Mathf.Sin(theta) * sinPhi * (WebScalpRx + lift),
                WebHeadCenter.y + cosPhi * (WebScalpRy + lift),
                WebHeadCenter.z + Mathf.Cos(theta) * sinPhi * (WebScalpRz + lift));
        }

        private static Vector3[] BuildSculptedLock(
            float thetaStart, float thetaEnd, float phiStart, float phiEnd,
            float baseLift, float midArch, float tipFlickOut, float tipDropExtra,
            float sCurveAmp, float napeHug, float curlAmp, float curlPhase)
        {
            const float eqPhi = 1.75f;
            var pts = new Vector3[NodesPerLock];

            for (int i = 0; i < NodesPerLock; i++)
            {
                float t = (float)i / (NodesPerLock - 1);
                float smoothT = t * t * (3f - 2f * t);

                float theta = Mathf.Lerp(thetaStart, thetaEnd, smoothT) + Mathf.Sin(t * Mathf.PI * 1.45f) * sCurveAmp;
                float phi = Mathf.Lerp(phiStart, phiEnd, Mathf.Pow(t, 0.94f));

                float arch = Mathf.Sin(t * Mathf.PI) * midArch;
                float rootSink = -0.004f * Mathf.Pow(1f - Mathf.Min(1f, t * 4.5f), 2f);
                float flick = Mathf.Pow(Mathf.Max(0f, (t - 0.62f) / 0.38f), 2f) * tipFlickOut;
                float lift = baseLift + rootSink + arch + flick;

                Vector3 pt;
                if (phi <= eqPhi)
                {
                    pt = SkullSurfacePoint(theta, phi, lift);
                }
                else
                {
                    Vector3 eqPt = SkullSurfacePoint(theta, eqPhi, baseLift + rootSink + arch);
                    float extraPhi = phi - eqPhi;
                    float dropDist = extraPhi * 0.325f;
                    float outX = Mathf.Sin(theta);
                    float outZ = Mathf.Cos(theta);
                    float isRear = Mathf.Clamp01((-outZ + 0.25f) / 1.25f);
                    float inwardHug = napeHug * (isRear * Mathf.Min(0.115f, extraPhi * 0.095f) + (1f - isRear) * Mathf.Min(0.055f, extraPhi * 0.042f));

                    pt = new Vector3(
                        eqPt.x + outX * (flick - inwardHug),
                        eqPt.y - dropDist,
                        eqPt.z + outZ * (flick - inwardHug * (0.85f + 0.35f * isRear)));
                }

                if (tipDropExtra != 0f)
                    pt.y -= Mathf.Pow(t, 1.55f) * tipDropExtra;

                // Optional helical wave/curl/afro coil displacement
                if (curlAmp > 0.01f && i > 0)
                {
                    float freq = Mathf.Lerp(7f, 18f, curlAmp);
                    float amp = curlAmp * 0.032f * Mathf.Sin(Mathf.Min(1f, t * 1.5f) * Mathf.PI * 0.5f);
                    float ang = t * freq + curlPhase * 6.283f;
                    pt.x += Mathf.Cos(ang) * amp;
                    pt.z += Mathf.Sin(ang) * amp;
                }

                pts[i] = pt;
            }
            return pts;
        }

        private static Vector3[] BuildGatheredTailLock(
            float anchorTheta, float anchorPhi, Vector3 outDir,
            float spreadX, float spreadZ, float length, float archOut, float waveAmp, float wavePhase)
        {
            Vector3 root = SkullSurfacePoint(anchorTheta, anchorPhi, 0.015f);
            Vector3 normOut = outDir.normalized;
            var pts = new Vector3[NodesPerLock];
            for (int i = 0; i < NodesPerLock; i++)
            {
                float t = (float)i / (NodesPerLock - 1);
                float bell = Mathf.Sin(Mathf.Min(1f, t * 1.5f) * Mathf.PI * 0.65f);
                float drop = Mathf.Pow(t, 1.28f) * length;
                float outPush = bell * archOut + t * 0.05f;
                float fan = Mathf.Sin(t * Mathf.PI * 0.85f);
                float wave = Mathf.Sin(t * Mathf.PI * 2.1f + wavePhase) * waveAmp * t;
                pts[i] = new Vector3(
                    root.x + normOut.x * outPush + spreadX * fan + wave,
                    root.y + Mathf.Sin(t * Mathf.PI * 0.55f) * (archOut * 0.34f) - drop,
                    root.z + normOut.z * outPush + spreadZ * fan);
            }
            return pts;
        }

        /// <summary>
        /// Builds a standalone static preview Mesh (used inside PreviewRenderUtility 3D viewport).
        /// </summary>
        public static Mesh BuildPreviewMesh(List<ProceduralLock> locks)
        {
            var verts = new List<Vector3>(locks.Count * 80);
            var uvs = new List<Vector2>(locks.Count * 80);
            var colors = new List<Color>(locks.Count * 80);
            var tris = new List<int>(locks.Count * 140);
            int sides = ProfileX.Length - 1;

            for (int lIdx = 0; lIdx < locks.Count; lIdx++)
            {
                ProceduralLock lk = locks[lIdx];
                Vector3[] pts = lk.points;
                int count = pts.Length;
                if (count < 2) continue;

                int ringCount = count - 1;
                int baseVert = verts.Count;
                float tipClipFlag = lk.isBaseShell ? 0f : 1f;
                Vector3 refOut = (pts[0] - MetaPersonSkullCenter).sqrMagnitude > 1e-6f
                    ? (pts[0] - MetaPersonSkullCenter).normalized : Vector3.up;

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

                    float rootTuck = 0.70f + 0.30f * Mathf.Sin(Mathf.Min(1f, t * 4.5f) * Mathf.PI * 0.5f);
                    float widthTaper = t < 0.44f ? (1f - t * 0.14f) : (Mathf.Pow((1f - t) / 0.56f, 0.94f) * 0.938f);
                    float thickTaper = Mathf.Pow(1f - t, 0.86f);
                    float halfW = lk.width * 0.5f * rootTuck * Mathf.Max(0.04f, widthTaper);
                    float halfT = lk.thickness * 0.47f * rootTuck * thickTaper;

                    for (int r = 0; r <= sides; r++)
                    {
                        int ri = r % sides;
                        verts.Add(pt + side * (ProfileX[ri] * halfW) + outVec * (ProfileY[ri] * halfT));
                        uvs.Add(new Vector2(ProfileU[r], t));
                        colors.Add(new Color(t, lk.layerAO, lk.seed, tipClipFlag));
                    }
                }

                int tipVertIdx = verts.Count;
                verts.Add(pts[count - 1]);
                uvs.Add(new Vector2(0.5f, 1.0f));
                colors.Add(new Color(1f, lk.layerAO, lk.seed, tipClipFlag));

                int stride = sides + 1;
                for (int k = 0; k < ringCount - 1; k++)
                {
                    int r0 = baseVert + k * stride;
                    int r1 = baseVert + (k + 1) * stride;
                    for (int r = 0; r < sides; r++)
                    {
                        tris.Add(r0 + r); tris.Add(r0 + r + 1); tris.Add(r1 + r + 1);
                        tris.Add(r0 + r); tris.Add(r1 + r + 1); tris.Add(r1 + r);
                    }
                }
                int rLast = baseVert + (ringCount - 1) * stride;
                for (int r = 0; r < sides; r++)
                {
                    tris.Add(rLast + r); tris.Add(rLast + r + 1); tris.Add(tipVertIdx);
                }
            }

            var mesh = new Mesh { name = "AetherStrand_PreviewHair", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
