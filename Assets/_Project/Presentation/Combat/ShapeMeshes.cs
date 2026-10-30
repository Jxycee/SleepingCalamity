// Not networked. Flat meshes for hitbox and telegraph markers on the ground: a wedge (a cone's footprint) and a disc, in the XZ plane, facing +Z.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class ShapeMeshes
    {
        public static Mesh Wedge(float range, float halfAngleDeg, int segments = 18)
        {
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(-halfAngleDeg, halfAngleDeg, i / (float)segments);
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * range;
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Wedge" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Disc(float radius, int segments = 28)
        {
            Mesh m = Wedge(radius, 180f, segments * 2);
            m.name = "Disc";
            return m;
        }
    }
}
