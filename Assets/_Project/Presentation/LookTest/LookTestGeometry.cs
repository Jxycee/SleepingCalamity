// Authority: none (presentation only). Builds a greybox mesh for the look-test scenes when it is enabled, so the scene files hold only
// a size and a tile length (deterministic YAML, no embedded meshes). Runs in the editor too (ExecuteAlways) so captures taken from a loaded scene see it.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LookTestGeometry : MonoBehaviour
    {
        public enum Shape { Box, TrimBox, VaultInterior, Cone, GroinVault }

        [SerializeField] Shape m_Shape = Shape.Box;
        [SerializeField] Vector3 m_Size = Vector3.one;     // Box: size in metres. VaultInterior: x = radius, z = length. Cone: x = far radius, z = length. GroinVault: x = half span, y = rise.
        [SerializeField] float m_TileMetres = 2f;
        [SerializeField] int m_Segments = 24;
        [SerializeField] bool m_Collider;

        Mesh _mesh;

        public void Configure(Shape shape, Vector3 size, float tileMetres, int segments = 24, bool collider = false)
        {
            m_Shape = shape; m_Size = size; m_TileMetres = Mathf.Max(0.01f, tileMetres); m_Segments = Mathf.Max(3, segments); m_Collider = collider;
            Rebuild();
        }

        void OnEnable() => Rebuild();

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }

        void Rebuild()
        {
            if (_mesh != null) { if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh); }
            switch (m_Shape)
            {
                case Shape.VaultInterior: _mesh = LookTestMeshes.VaultInterior(m_Size.x, m_Size.z, m_Segments, m_TileMetres); break;
                case Shape.Cone: _mesh = LookTestMeshes.Cone(m_Size.x, m_Size.z, m_Segments); break;
                case Shape.GroinVault: _mesh = LookTestMeshes.GroinVault(m_Size.x, m_Size.y, m_Segments, m_TileMetres); break;
                case Shape.TrimBox: _mesh = LookTestMeshes.Box(m_Size, m_TileMetres, true); break;
                default: _mesh = LookTestMeshes.Box(m_Size, m_TileMetres, false); break;
            }
            _mesh.hideFlags = HideFlags.HideAndDontSave;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var box = GetComponent<BoxCollider>();
            if (m_Collider && (m_Shape == Shape.Box || m_Shape == Shape.TrimBox))
            {
                if (box == null) box = gameObject.AddComponent<BoxCollider>();
                box.size = m_Size;
            }
        }
    }
}
