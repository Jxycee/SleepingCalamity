// Hair trial audit (2026-11-22): the AetherStrand plugin's own telemetry (ComputeAudit) on Tobin's AetherStrand variant, plus one
// bone-transform measure applied to both variants (HairDeck spring chains and AetherStrand), so the two solvers are compared the same way.
// Loads the HairAB prefabs through AssetDatabase (editor-only test); solvers are stepped synchronously at 60 Hz (components disabled so
// LateUpdate never double-steps; no frame waits, safe under -nographics).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AetherStrand;
using NUnit.Framework;
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class TobinHairAuditTests
    {
        const string AetherPrefab = "Assets/_Project/Content/Characters/TobinBase/HairAB/Tobin_AetherStrand.prefab";
        const string HairDeckPrefab = "Assets/_Project/Content/Characters/TobinBase/HairAB/Tobin_HairDeck.prefab";
        const float Dt = 1f / 60f;
        readonly List<GameObject> spawned = new();

        [TearDown] public void TearDown() { foreach (var g in spawned) if (g) Object.DestroyImmediate(g); spawned.Clear(); }

        GameObject Spawn(string path, out Transform head)
        {
            var src = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(src, Is.Not.Null, path);
            var go = Object.Instantiate(src); spawned.Add(go);
            var anim = go.GetComponentInChildren<Animator>(); anim.enabled = false; head = anim.GetBoneTransform(HumanBodyBones.Head);
            return go;
        }

        AetherStrandPhysics SpawnAether(out Transform head)
        {
            var go = Spawn(AetherPrefab, out head);
            var phys = go.GetComponent<AetherStrandPhysics>(); Assert.That(phys, Is.Not.Null);
            phys.enabled = false; phys.RebuildChains(); phys.ResetToRestPose();
            return phys;
        }

        static string Fmt(AetherStrandAuditMetrics m) =>
            $"chains {m.totalChains} penetrating {m.penetratingNodes} kink {m.maxKinkAngleDeg:F1} deg stretch {m.maxStretchErrorPct:F2} % tipSway {m.meanTipSwayCm:F2} cm";

        [Test]
        public void AetherStrand_Audit_90Frames()
        {
            var phys = SpawnAether(out _);
            float t = 0f; for (int f = 0; f < 90; f++) { t += Dt; phys.Simulate(Dt, t); }
            var m = phys.ComputeAudit();
            Debug.Log("SC HAIR AUDIT aether idle: " + Fmt(m));
            Assert.That(m.totalChains, Is.GreaterThan(0));
            Assert.That(phys.colliders.Count(c => c != null), Is.EqualTo(6), "colliders");
            Assert.That(m.penetratingNodes, Is.EqualTo(0), Fmt(m));
            Assert.That(m.maxKinkAngleDeg, Is.LessThan(28f), Fmt(m));
            Assert.That(m.maxStretchErrorPct, Is.LessThan(3.5f), Fmt(m));
            Assert.That(m.meanTipSwayCm, Is.InRange(0.2f, 5.0f), Fmt(m));
        }

        [Test]
        public void AetherStrand_Audit_HeadTurnSnap()
        {
            var phys = SpawnAether(out var head);
            var r0 = head.localRotation; float t = 0f;
            for (int f = 0; f < 30; f++) { t += Dt; phys.Simulate(Dt, t); }
            for (int f = 0; f < 6; f++) { head.localRotation = r0 * Quaternion.Euler(0, 60f * (f + 1) / 6f, 0); t += Dt; phys.Simulate(Dt, t); }   // 60 deg in 0.1 s
            var mid = phys.ComputeAudit();
            for (int f = 0; f < 54; f++) { t += Dt; phys.Simulate(Dt, t); }
            var m = phys.ComputeAudit();
            Debug.Log("SC HAIR AUDIT aether snap (end of turn): " + Fmt(mid) + " | settled +0.9 s: " + Fmt(m));
            Assert.That(mid.penetratingNodes, Is.EqualTo(0), Fmt(mid));
            Assert.That(mid.maxStretchErrorPct, Is.LessThan(3.5f), Fmt(mid));
            Assert.That(m.penetratingNodes, Is.EqualTo(0), Fmt(m));
            Assert.That(m.maxKinkAngleDeg, Is.LessThan(28f), Fmt(m));
            Assert.That(m.maxStretchErrorPct, Is.LessThan(3.5f), Fmt(m));
        }

        // ---- one bone-transform measure for both solvers ----
        struct BoneMetrics { public int chains; public float kink, stretch, tipSway, tipMax; }

        static List<Transform[]> Chains(Transform head, string prefix)
        {
            return head.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith(prefix, System.StringComparison.Ordinal) && int.TryParse(t.name.Substring(t.name.LastIndexOf('_') + 1), out _))
                .GroupBy(t => t.name.Substring(0, t.name.LastIndexOf('_')))
                .OrderBy(g => g.Key, System.StringComparer.Ordinal)
                .Select(g => g.OrderBy(t => int.Parse(t.name.Substring(t.name.LastIndexOf('_') + 1))).ToArray())
                .Where(c => c.Length >= 2).ToList();
        }

        static BoneMetrics Measure(Transform head, List<Transform[]> chains, List<Vector3[]> restLocal)
        {
            var m = new BoneMetrics { chains = chains.Count }; float sway = 0f;
            for (int c = 0; c < chains.Count; c++)
            {
                var b = chains[c]; var r = restLocal[c];
                for (int i = 1; i < b.Length; i++)
                {
                    float rest = (r[i] - r[i - 1]).magnitude, cur = (b[i].position - b[i - 1].position).magnitude;
                    if (rest > 0.001f) m.stretch = Mathf.Max(m.stretch, Mathf.Abs(cur - rest) / rest * 100f);
                    if (i < b.Length - 1) m.kink = Mathf.Max(m.kink, Vector3.Angle(b[i].position - b[i - 1].position, b[i + 1].position - b[i].position));
                }
                float d = (b[^1].position - head.TransformPoint(r[^1])).magnitude * 100f; sway += d; m.tipMax = Mathf.Max(m.tipMax, d);
            }
            m.tipSway = sway / Mathf.Max(1, chains.Count);
            return m;
        }

        static string Fmt(BoneMetrics m) => $"chains {m.chains} kink {m.kink:F1} deg stretch {m.stretch:F2} % tipSway {m.tipSway:F2} cm (max {m.tipMax:F2})";

        [TestCase("hairdeck")]
        [TestCase("aether")]
        public void BothSolvers_BoneMetrics_IdleAndSnap(string variant)
        {
            bool aether = variant == "aether";
            var go = Spawn(aether ? AetherPrefab : HairDeckPrefab, out var head);
            AetherStrandPhysics phys = null; TobinHairSpringChains springs = null;
            if (aether) { phys = go.GetComponent<AetherStrandPhysics>(); phys.enabled = false; phys.fallbackWindStrength = 0f; phys.RebuildChains(); phys.ResetToRestPose(); }
            else { springs = go.GetComponentInChildren<TobinHairSpringChains>(); Assert.That(springs, Is.Not.Null); springs.enabled = false; springs.head = head; springs.Rebuild(); }
            var chains = Chains(head, aether ? "Hair_" : "hair_");
            Assert.That(chains.Count, Is.GreaterThan(0));
            var rest = chains.Select(c => c.Select(b => head.InverseTransformPoint(b.position)).ToArray()).ToList();
            var r0 = head.localRotation; float t = 0f;
            void Step() { t += Dt; if (phys) phys.Simulate(Dt, t); else springs.Simulate(Dt); }
            for (int f = 0; f < 90; f++) Step();
            var idle = Measure(head, chains, rest);
            for (int f = 0; f < 6; f++) { head.localRotation = r0 * Quaternion.Euler(0, 60f * (f + 1) / 6f, 0); Step(); }
            var snap = Measure(head, chains, rest);
            for (int f = 0; f < 54; f++) Step();
            var settled = Measure(head, chains, rest);
            Debug.Log($"SC HAIR BONES {variant} (wind 0) idle: {Fmt(idle)} | end of 60 deg/0.1 s turn: {Fmt(snap)} | settled +0.9 s: {Fmt(settled)}");
            Assert.That(float.IsNaN(idle.tipSway) || float.IsNaN(snap.tipSway) || float.IsNaN(settled.tipSway), Is.False);
            Assert.That(settled.stretch, Is.LessThan(3.5f), Fmt(settled));
        }
    }
}
#endif
