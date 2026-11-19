// Authority: none (pure mesh generation, presentation only).
// T24: the photo-textured wing mesh for butterflies (SC_WingCard): a grid over the photo's rectangle, wing span along X (x in -1..1), body forward +Z (the photo's top edge is the head),
// uv = the photo's own layout. `aspect` is photo height / width, so a 0.2 m butterfly (scale = span / 2) keeps its real proportions.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class AmbientWingMesh
    {
        public static Mesh Build(string name, float aspect, int cols = 16, int rows = 8)
        {
            var verts = new Vector3[(cols + 1) * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols, v = r / (float)rows;
                    verts[r * (cols + 1) + c] = new Vector3(u * 2f - 1f, 0f, (v - 0.5f) * 2f * aspect);
                    uvs[r * (cols + 1) + c] = new Vector2(u, v);
                }
            var tris = new int[cols * rows * 6];
            int k = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int a = r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
                    tris[k++] = a; tris[k++] = d; tris[k++] = b;
                    tris[k++] = b; tris[k++] = d; tris[k++] = e;
                }
            var m = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris };
            m.RecalculateBounds();
            m.RecalculateNormals();
            return m;
        }
    }
}
