// Authority: none (presentation only). Spawns the beauty-corner plaza cobbles from cobble_placements.json when it is enabled, so the scene file holds one small
// component instead of 8,673 serialized prefab instances (21 MB, reordered on every rebuild). The stones are ordinary prefab instances of the per-variant LODGroup prefabs
// (so the GPU Resident Drawer and LOD fade behave as for hand-placed ones), children of a hidden container that is never saved (HideFlags.DontSave).
// Runs in the editor too (ExecuteAlways) so a loaded scene captures identically. Positions and rotations are in this object's local space.
// Cost: the json is parsed once per distinct TextAsset (cached per file across enables and scene loads in the same domain) into flat arrays; a Rebuild is then one
// instantiate loop plus one hide-flag pass. The time of each rebuild is logged ("SC COBBLES") so a scene-load regression shows up in the editor log.
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CobbleSpawner : MonoBehaviour
    {
        const string ContainerName = "Setts (spawned, not saved)";

        [System.Serializable] sealed class Stone { public string v; public float[] p, q, s; }
        [System.Serializable] sealed class Placements { public Stone[] stones; }

        // A validated placement: stones with a missing or short p/q/s array are dropped at parse time, so the spawn loop cannot throw.
        readonly struct Sett
        {
            public readonly string Variant; public readonly Vector3 Pos, Scale; public readonly Quaternion Rot;
            public Sett(string v, Vector3 p, Quaternion q, Vector3 s) { Variant = v; Pos = p; Rot = q; Scale = s; }
        }

        sealed class Parsed { public EntityId AssetId; public long Size; public Sett[] Setts; public int Skipped; }
        static readonly Dictionary<(EntityId, long), Parsed> s_Cache = new Dictionary<(EntityId, long), Parsed>();   // one parse per placements file (the corner's and the square's); cleared by the domain reload

        [SerializeField] TextAsset m_Placements;
        [SerializeField] string[] m_VariantNames = new string[0];
        [SerializeField] GameObject[] m_VariantPrefabs = new GameObject[0];

        // Optional height correction (the square draped onto the town's ground): a grid of y offsets in this object's local x/z, bilinear; empty = none (the corner).
        [SerializeField] float[] m_DropY = new float[0];
        [SerializeField] Vector2 m_DropOrigin;
        [SerializeField] float m_DropStep;
        [SerializeField] int m_DropNX, m_DropNZ;

        Transform _container;
        static readonly List<Transform> s_Scratch = new List<Transform>(4096);

        // The y offset of the correction grid at local (x, z): bilinear, clamped to the grid edge; 0 when there is no grid. Also used by the editor to drape meshes the same way.
        public static float SampleDrop(float[] dropY, Vector2 origin, float step, int nx, int nz, float x, float z)
        {
            if (dropY == null || step <= 0f || nx < 2 || nz < 2 || dropY.Length < nx * nz) return 0f;
            float fx = Mathf.Clamp((x - origin.x) / step, 0f, nx - 1f), fz = Mathf.Clamp((z - origin.y) / step, 0f, nz - 1f);
            int ix = Mathf.Min((int)fx, nx - 2), iz = Mathf.Min((int)fz, nz - 2);
            float tx = fx - ix, tz = fz - iz;
            int i = iz * nx + ix;
            float a = dropY[i] + (dropY[i + 1] - dropY[i]) * tx, b = dropY[i + nx] + (dropY[i + nx + 1] - dropY[i + nx]) * tx;
            return a + (b - a) * tz;
        }

        public void Configure(TextAsset placements, string[] variantNames, GameObject[] variantPrefabs, float[] dropY = null, Vector2 dropOrigin = default, float dropStep = 0f, int dropNX = 0, int dropNZ = 0)
        {
            m_Placements = placements; m_VariantNames = variantNames; m_VariantPrefabs = variantPrefabs;
            m_DropY = dropY ?? new float[0]; m_DropOrigin = dropOrigin; m_DropStep = dropStep; m_DropNX = dropNX; m_DropNZ = dropNZ;
            if (isActiveAndEnabled) Rebuild();
        }

        void OnEnable() => Rebuild();
        void OnDisable() => Clear();

        void Clear()
        {
            // Also sweeps a container a domain reload orphaned (DontSave objects survive it).
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == ContainerName) { if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject); }
            }
            _container = null;
        }

        static Parsed Parse(TextAsset asset)
        {
            EntityId id = asset.GetEntityId(); long size = asset.dataSize;
            if (s_Cache.TryGetValue((id, size), out Parsed cached)) return cached;
            Placements data = JsonUtility.FromJson<Placements>(asset.text);
            var parsed = new Parsed { AssetId = id, Size = size, Setts = new Sett[0] };
            if (data != null && data.stones != null)
            {
                var list = new List<Sett>(data.stones.Length);
                foreach (Stone s in data.stones)
                {
                    if (s == null || string.IsNullOrEmpty(s.v) || s.p == null || s.p.Length < 3 || s.q == null || s.q.Length < 4 || s.s == null || s.s.Length < 3) { parsed.Skipped++; continue; }
                    list.Add(new Sett(s.v, new Vector3(s.p[0], s.p[1], s.p[2]), new Quaternion(s.q[0], s.q[1], s.q[2], s.q[3]), new Vector3(s.s[0], s.s[1], s.s[2])));
                }
                parsed.Setts = list.ToArray();
            }
            s_Cache[(id, size)] = parsed;
            return parsed;
        }

        void Rebuild()
        {
            Clear();
            if (m_Placements == null || m_VariantNames == null || m_VariantPrefabs == null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var prefabs = new Dictionary<string, GameObject>();
            for (int i = 0; i < m_VariantNames.Length && i < m_VariantPrefabs.Length; i++) if (m_VariantPrefabs[i] != null) prefabs[m_VariantNames[i]] = m_VariantPrefabs[i];
            Parsed data = Parse(m_Placements);
            double parseMs = sw.Elapsed.TotalMilliseconds;
            if (data.Setts.Length == 0) return;

            var container = new GameObject(ContainerName);
            container.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            container.transform.SetParent(transform, false);
            _container = container.transform;
            int spawned = 0;
            bool drop = m_DropY != null && m_DropY.Length > 0;
            GameObject lastPrefab = null; string lastVariant = null;
            foreach (Sett s in data.Setts)
            {
                if (s.Variant != lastVariant) { prefabs.TryGetValue(s.Variant, out lastPrefab); lastVariant = s.Variant; }
                if (lastPrefab == null) continue;
                GameObject go = Instantiate(lastPrefab, _container, false);
                go.name = lastPrefab.name;
                Vector3 pos = s.Pos;
                if (drop) pos.y += SampleDrop(m_DropY, m_DropOrigin, m_DropStep, m_DropNX, m_DropNZ, pos.x, pos.z);
                go.transform.SetLocalPositionAndRotation(pos, s.Rot);
                go.transform.localScale = s.Scale;
                spawned++;
            }
            // One hide-flag pass over everything spawned (the container is DontSave already; the stones carry the flags too so nothing is saved or listed).
            _container.GetComponentsInChildren(true, s_Scratch);
            foreach (Transform t in s_Scratch) t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            s_Scratch.Clear();
            Debug.Log("SC COBBLES spawned " + spawned + " of " + data.Setts.Length + " setts (skipped " + data.Skipped + " malformed) in " + sw.Elapsed.TotalMilliseconds.ToString("F0") + " ms (parse " + parseMs.ToString("F0") + " ms)");
        }
    }
}
