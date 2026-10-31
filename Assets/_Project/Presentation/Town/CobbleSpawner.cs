// Authority: none (presentation only). Spawns the beauty-corner plaza cobbles from cobble_placements.json when it is enabled, so the scene file holds one small
// component instead of 6,777 serialized prefab instances (21 MB, reordered on every rebuild). The stones are ordinary prefab instances of the per-variant LODGroup prefabs
// (so the GPU Resident Drawer and LOD fade behave as for hand-placed ones), children of a hidden container that is never saved (HideFlags.DontSave).
// Runs in the editor too (ExecuteAlways) so a loaded scene captures identically. Positions and rotations are in this object's local space.
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

        [SerializeField] TextAsset m_Placements;
        [SerializeField] string[] m_VariantNames = new string[0];
        [SerializeField] GameObject[] m_VariantPrefabs = new GameObject[0];

        Transform _container;

        public void Configure(TextAsset placements, string[] variantNames, GameObject[] variantPrefabs)
        {
            m_Placements = placements; m_VariantNames = variantNames; m_VariantPrefabs = variantPrefabs;
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

        void Rebuild()
        {
            Clear();
            if (m_Placements == null || m_VariantNames == null || m_VariantPrefabs == null) return;
            var prefabs = new Dictionary<string, GameObject>();
            for (int i = 0; i < m_VariantNames.Length && i < m_VariantPrefabs.Length; i++) if (m_VariantPrefabs[i] != null) prefabs[m_VariantNames[i]] = m_VariantPrefabs[i];
            Placements data = JsonUtility.FromJson<Placements>(m_Placements.text);
            if (data == null || data.stones == null) return;

            var container = new GameObject(ContainerName);
            container.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            container.transform.SetParent(transform, false);
            _container = container.transform;
            foreach (Stone s in data.stones)
            {
                if (!prefabs.TryGetValue(s.v, out GameObject prefab)) continue;
                GameObject go = Instantiate(prefab, _container, false);
                go.name = prefab.name;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                Transform tr = go.transform;
                tr.localPosition = new Vector3(s.p[0], s.p[1], s.p[2]);
                tr.localRotation = new Quaternion(s.q[0], s.q[1], s.q[2], s.q[3]);
                tr.localScale = new Vector3(s.s[0], s.s[1], s.s[2]);
            }
        }
    }
}
