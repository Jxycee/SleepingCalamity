// Authority: none (presentation only). The Hollowmere greybox ground: a height-field mesh built from Core's HollowmereLayout.Ground (hill, keep terrace,
// lake basin, every lot and landmark pad flattened) when the component is enabled, so the scene file holds only the grid settings (no embedded mesh).
// Three submeshes: meadow, lane (within a lane's half width of its spine) and paving (the square and the keep terrace top). Runs in the editor too
// (ExecuteAlways) so captures taken from a loaded scene see it; the collider is added in play mode only. HollowmereBuilder adds it.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.Presentation
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class HollowmereGround : MonoBehaviour
    {
        [SerializeField] Vector2 m_Min = new Vector2(-130f, -150f);
        [SerializeField] Vector2 m_Max = new Vector2(140f, 100f);
        [SerializeField] float m_Cell = 1f;
        [SerializeField] float m_TileMetres = 3f;
        [SerializeField] bool m_Collider;

        Mesh _mesh;

        public void Configure(Vector2 min, Vector2 max, float cell, float tileMetres, bool collider)
        {
            m_Min = min; m_Max = max; m_Cell = Mathf.Max(0.25f, cell); m_TileMetres = Mathf.Max(0.1f, tileMetres); m_Collider = collider;
            Rebuild();
        }

        void OnEnable() { if (_mesh == null) Rebuild(); }   // once per load: the mesh is not saved (HideAndDontSave), Configure forces a rebuild

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }

        void Rebuild()
        {
            if (_mesh != null) { if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh); }
            _mesh = Build(HollowmereLayout.Create(), m_Min, m_Max, m_Cell, m_TileMetres);
            _mesh.hideFlags = HideFlags.HideAndDontSave;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mc = GetComponent<MeshCollider>();
            if (m_Collider && Application.isPlaying) { if (mc == null) mc = gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = _mesh; }   // play only: edit mode never adds components to the scene
        }

        public enum Surface { Meadow = 0, Lane = 1, Paving = 2 }

        public static Surface SurfaceAt(TownLayout t, float x, float z)
        {
            if (x >= HollowmereLayout.SquareMinX && x <= HollowmereLayout.SquareMaxX && z >= HollowmereLayout.SquareMinZ && z <= HollowmereLayout.SquareMaxZ) return Surface.Paving;
            if (z > HollowmereLayout.KeepWallZ + 0.5f && z < HollowmereLayout.KeepWallZ + 12f && Mathf.Abs(x) < HollowmereLayout.KeepHalfWidth - 1f) return Surface.Paving;
            var p = new TownPoint(x, z);
            foreach (Lane lane in t.Lanes)
                for (int i = 1; i < lane.Points.Length; i++)
                    if (DistanceToSegment(p, lane.Points[i - 1], lane.Points[i]) < lane.Width * 0.5f) return Surface.Lane;
            return Surface.Meadow;
        }

        static float DistanceToSegment(TownPoint p, TownPoint a, TownPoint b)
        {
            TownPoint ab = b - a, ap = p - a;
            float len2 = ab.X * ab.X + ab.Z * ab.Z;
            float t = len2 > 0f ? Mathf.Clamp01((ap.X * ab.X + ap.Z * ab.Z) / len2) : 0f;
            return TownPoint.Distance(p, a + ab * t);
        }

        public static Mesh Build(TownLayout t, Vector2 min, Vector2 max, float cell, float tile)
        {
            int nx = Mathf.Max(2, Mathf.RoundToInt((max.x - min.x) / cell) + 1), nz = Mathf.Max(2, Mathf.RoundToInt((max.y - min.y) / cell) + 1);
            var v = new Vector3[nx * nz]; var uv = new Vector2[v.Length];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = min.x + i * cell, z = min.y + j * cell;
                    v[j * nx + i] = new Vector3(x, HollowmereLayout.Ground(t, x, z), z);
                    uv[j * nx + i] = new Vector2(x / tile, z / tile);
                }
            var subs = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    Surface s = SurfaceAt(t, min.x + (i + 0.5f) * cell, min.y + (j + 0.5f) * cell);
                    List<int> list = subs[(int)s];
                    list.Add(a); list.Add(c); list.Add(b);
                    list.Add(b); list.Add(c); list.Add(d);
                }
            var mesh = new Mesh { name = "HollowmereGround", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = v; mesh.uv = uv;
            mesh.subMeshCount = 3;
            for (int k = 0; k < 3; k++) mesh.SetTriangles(subs[k], k);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
