using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    /// <summary>
    /// Secondary motion for Tobin's mesh hair: verlet spring chains driven from the Head bone. The Blender rig
    /// (Art/Blender/tools/experimental/tobin_hair/hair_rig.py) adds chains of bones named hair_{k}_{i} (i = 0..N-1) under the Head bone;
    /// bone 0 follows the head rigidly, bones 1.. deform the hair strips. Each LateUpdate this component moves the chain points with
    /// gravity, damping and a pull back to the head-rigid rest pose, keeps the segment lengths, and turns the bones to follow.
    /// Presentation only (visual, runs on every client, no authority / no networking).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TobinHairSpringChains : MonoBehaviour
    {
        [Tooltip("Head bone the chains hang from. Auto-found by name (\"Head\") if empty.")]
        public Transform head;
        public int ChainCount => chains.Count;
        [Tooltip("Pull back toward the rigid pose, 1/s^2. Higher = stiffer hair.")]
        public float stiffness = 1500f;
        [Range(0.5f, 1f)] public float damping = 0.93f;
        public float gravity = 9.8f;
        [Range(1, 8)] public int substeps = 4;
        [Range(1, 8)] public int lengthIterations = 4;
        [Tooltip("Master blend: 0 = rigid hair (no motion), 1 = full simulation.")]
        [Range(0f, 1f)] public float weight = 1f;

        sealed class Chain
        {
            public Transform[] bones;       // bones[i] controls the segment p[i] -> p[i+1]
            public Vector3[] restLocal;     // joint positions in head space, rest (N+1 points)
            public Quaternion[] restRotLocal; // bone rotations in head space, rest
            public Vector3[] restDirLocal;
            public float[] segLen;
            public Vector3[] p, prev;
        }

        readonly List<Chain> chains = new();

        void Awake() { Build(); }
        void OnEnable() { if (chains.Count == 0) Build(); }

        /// <summary>Re-reads the chain bones (call after re-parenting the hair rig).</summary>
        public void Rebuild() { Build(); }

        void Build()
        {
            chains.Clear();
            if (!head) head = FindByName(transform, "Head");
            if (!head) { Debug.LogWarning("TobinHairSpringChains: no Head bone found", this); return; }
            var roots = new Dictionary<string, SortedDictionary<int, Transform>>();
            foreach (var t in head.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name;
                if (!n.StartsWith("hair_")) continue;
                int us = n.LastIndexOf('_'); if (us < 6) continue;
                if (!int.TryParse(n.Substring(us + 1), out int idx)) continue;
                string key = n.Substring(0, us);
                if (!roots.TryGetValue(key, out var d)) roots[key] = d = new SortedDictionary<int, Transform>();
                d[idx] = t;
            }
            foreach (var kv in roots)
            {
                var list = new List<Transform>(kv.Value.Values); int nb = list.Count; if (nb < 2) continue;
                var c = new Chain { bones = list.ToArray(), restLocal = new Vector3[nb + 1], restRotLocal = new Quaternion[nb], restDirLocal = new Vector3[nb], segLen = new float[nb], p = new Vector3[nb + 1], prev = new Vector3[nb + 1] };
                for (int i = 0; i < nb; i++) c.restLocal[i] = head.InverseTransformPoint(list[i].position);
                // last joint: tip of the last bone = its position + the previous direction * previous length
                Vector3 lastDir = (c.restLocal[nb - 1] - c.restLocal[nb - 2]);
                c.restLocal[nb] = c.restLocal[nb - 1] + lastDir;
                for (int i = 0; i < nb; i++)
                {
                    Vector3 d = c.restLocal[i + 1] - c.restLocal[i]; c.segLen[i] = d.magnitude; c.restDirLocal[i] = d / Mathf.Max(d.magnitude, 1e-6f);
                    c.restRotLocal[i] = Quaternion.Inverse(head.rotation) * list[i].rotation;
                }
                for (int i = 0; i <= nb; i++) { c.p[i] = c.prev[i] = head.TransformPoint(c.restLocal[i]); }
                chains.Add(c);
            }
        }

        static Transform FindByName(Transform root, string suffix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == suffix || t.name.EndsWith(":" + suffix)) return t;
            return null;
        }

        void LateUpdate() { Simulate(Time.deltaTime); }

        /// <summary>One simulation step (every LateUpdate; editor captures and tests call it with a fixed step).</summary>
        public void Simulate(float deltaTime)
        {
            if (chains.Count == 0 || !head) return;
            float dt = Mathf.Min(deltaTime, 1f / 20f) / substeps; if (dt <= 0f) return;
            Vector3 g = Vector3.down * gravity;
            foreach (var c in chains)
            {
                int n = c.p.Length;
                for (int s = 0; s < substeps; s++)
                {
                    c.p[0] = c.prev[0] = head.TransformPoint(c.restLocal[0]);
                    for (int i = 1; i < n; i++)
                    {
                        Vector3 tgt = head.TransformPoint(c.restLocal[i]);
                        Vector3 vel = (c.p[i] - c.prev[i]) * damping;
                        c.prev[i] = c.p[i];
                        c.p[i] += vel + (g + stiffness * (tgt - c.p[i])) * dt * dt;
                    }
                    for (int it = 0; it < lengthIterations; it++)
                        for (int i = 0; i < n - 1; i++)
                        {
                            Vector3 d = c.p[i + 1] - c.p[i]; float L = d.magnitude + 1e-9f; float corr = (L - c.segLen[i]) / L;
                            if (i == 0) c.p[i + 1] -= d * corr; else { c.p[i] += 0.5f * d * corr; c.p[i + 1] -= 0.5f * d * corr; }
                        }
                }
                for (int i = 0; i < c.bones.Length; i++)
                {
                    Vector3 rigid0 = head.TransformPoint(c.restLocal[i]);
                    Vector3 restDir = head.TransformDirection(c.restDirLocal[i]);
                    Vector3 simDir = (c.p[i + 1] - c.p[i]).normalized;
                    Quaternion rot = Quaternion.FromToRotation(restDir, simDir) * (head.rotation * c.restRotLocal[i]);
                    Vector3 pos = c.p[i];
                    float w = weight;
                    c.bones[i].position = Vector3.Lerp(rigid0, pos, w);
                    c.bones[i].rotation = Quaternion.Slerp(head.rotation * c.restRotLocal[i], rot, w);
                }
            }
        }
    }
}
