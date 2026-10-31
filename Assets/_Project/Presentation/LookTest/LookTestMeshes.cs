// Authority: none (presentation only, no networking). Greybox shapes for the M8 look-test scenes with UVs in metres: one texture tile spans
// tileMetres in the world, whatever the shape's size, so a shared tileable keeps its 1024 px/m density (PHASE1 3.3) on every piece.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class LookTestMeshes
    {
        // Box centred on the origin. 24 vertices, flat normals, UVs in tiles (metres / tileMetres).
        // trim: the +Z and -Z faces map v to 0..1 across the face height (a trim-sheet strip: the material scales and offsets v to one strip)
        // and u to metres / tileMetres along the width, so a 2 m sheet length repeats cleanly.
        public static Mesh Box(Vector3 size, float tileMetres, bool trim)
        {
            Vector3 h = size * 0.5f;
            var verts = new Vector3[24]; var normals = new Vector3[24]; var uvs = new Vector2[24]; var tris = new int[36];
            // face: normal, u axis, v axis (right-handed so the winding below is front-facing seen from outside)
            Vector3[] n = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            Vector3[] ua = { Vector3.right, Vector3.left, Vector3.back, Vector3.forward, Vector3.right, Vector3.right };
            Vector3[] va = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.back, Vector3.forward };
            for (int f = 0; f < 6; f++)
            {
                float du = Mathf.Abs(Vector3.Dot(ua[f], size)), dv = Mathf.Abs(Vector3.Dot(va[f], size));
                float dn = Mathf.Abs(Vector3.Dot(n[f], size)) * 0.5f;
                for (int c = 0; c < 4; c++)
                {
                    float s = (c == 1 || c == 2) ? 1f : -1f, t = (c >= 2) ? 1f : -1f;   // corners (-,-), (+,-), (+,+), (-,+)
                    int i = f * 4 + c;
                    verts[i] = n[f] * dn + ua[f] * (s * du * 0.5f) + va[f] * (t * dv * 0.5f);
                    normals[i] = n[f];
                    bool strip = trim && f < 2;
                    uvs[i] = new Vector2((s + 1f) * 0.5f * du / tileMetres, strip ? (t + 1f) * 0.5f : (t + 1f) * 0.5f * dv / tileMetres);
                }
                int b = f * 4, k = f * 6;
                tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 2; tris[k + 3] = b; tris[k + 4] = b + 2; tris[k + 5] = b + 3;   // Unity is left-handed: cross(v1-v0, v2-v0) of a front face points at the viewer
            }
            var mesh = new Mesh { name = "LookTestBox" };
            mesh.vertices = verts; mesh.normals = normals; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        // The inside of a barrel vault: the upper half of a cylinder of the given radius along +Z (from z = 0 to length), centred on the
        // origin line, seen from inside. UV u = arc length / tileMetres, v = z / tileMetres.
        public static Mesh VaultInterior(float radius, float length, int segments, float tileMetres)
        {
            int cols = segments + 1;
            var verts = new Vector3[cols * 2]; var normals = new Vector3[cols * 2]; var uvs = new Vector2[cols * 2];
            for (int i = 0; i < cols; i++)
            {
                float a = Mathf.PI * i / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                var inward = new Vector3(-Mathf.Cos(a), -Mathf.Sin(a), 0f);
                for (int r = 0; r < 2; r++)
                {
                    int v = i * 2 + r;
                    verts[v] = p + Vector3.forward * (r * length); normals[v] = inward;
                    uvs[v] = new Vector2(a * radius / tileMetres, r * length / tileMetres);
                }
            }
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = (i + 1) * 2, b1 = (i + 1) * 2 + 1, k = i * 6;
                tris[k] = a0; tris[k + 1] = a1; tris[k + 2] = b1; tris[k + 3] = a0; tris[k + 4] = b1; tris[k + 5] = b0;   // the cross points inward: front-facing seen from inside
            }
            var mesh = new Mesh { name = "LookTestVault" };
            mesh.vertices = verts; mesh.normals = normals; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        // An open cone for light shafts: apex at the origin, widening to farRadius at z = length. uv.y = 0 at the apex, 1 at the far end.
        public static Mesh Cone(float farRadius, float length, int segments)
        {
            var verts = new Vector3[segments * 2 + 2]; var normals = new Vector3[verts.Length]; var uvs = new Vector2[verts.Length];
            float slope = Mathf.Atan2(farRadius, length);
            for (int i = 0; i <= segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 n = (dir * Mathf.Cos(slope) - Vector3.forward * Mathf.Sin(slope)).normalized;
                if (i < segments + 0)
                {
                    verts[i * 2] = Vector3.zero; normals[i * 2] = n; uvs[i * 2] = new Vector2(i / (float)segments, 0f);
                    verts[i * 2 + 1] = dir * farRadius + Vector3.forward * length; normals[i * 2 + 1] = n; uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
                }
                else
                {
                    verts[segments * 2] = Vector3.zero; normals[segments * 2] = n; uvs[segments * 2] = new Vector2(1f, 0f);
                    verts[segments * 2 + 1] = dir * farRadius + Vector3.forward * length; normals[segments * 2 + 1] = n; uvs[segments * 2 + 1] = new Vector2(1f, 1f);
                }
            }
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = (i + 1) * 2, b1 = (i + 1) * 2 + 1, k = i * 6;
                tris[k] = a0; tris[k + 1] = a1; tris[k + 2] = b1; tris[k + 3] = a0; tris[k + 4] = b1; tris[k + 5] = b0;
            }
            var mesh = new Mesh { name = "LookTestCone" };
            mesh.vertices = verts; mesh.normals = normals; mesh.uv = uvs; mesh.triangles = tris;   // the shader is Cull Off
            mesh.RecalculateBounds();
            return mesh;
        }

        // The underside of a square groin vault (two crossing elliptical barrels, the higher one wins): footprint x, z in -halfSpan..halfSpan,
        // springing at y = 0, crown y = rise. Height = rise * sqrt(1 - (min(|x|, |z|) / halfSpan)^2); the groin creases run along the diagonals,
        // which are vertex lines (segments is rounded up to even), and each half keeps its own barrel normals so the creases stay sharp.
        // Seen from below. UV: along each barrel the arc (mean-radius approximation) / tileMetres, across it the straight axis / tileMetres.
        public static Mesh GroinVault(float halfSpan, float rise, int segments, float tileMetres)
        {
            int n = Mathf.Max(2, segments + (segments & 1)), cols = n + 1, per = cols * cols;
            float rx = Mathf.Max(0.01f, halfSpan), ry = Mathf.Max(0.01f, rise), meanR = (rx + ry) * 0.5f;
            var verts = new Vector3[per * 2]; var normals = new Vector3[per * 2]; var uvs = new Vector2[per * 2];
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < cols; j++)
                {
                    float x = -rx + 2f * rx * i / n, z = -rx + 2f * rx * j / n;
                    float m = Mathf.Min(Mathf.Abs(x), Mathf.Abs(z));
                    float y = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (m / rx) * (m / rx)));
                    var p = new Vector3(x, y, z);
                    int a = i * cols + j, b = per + a;
                    verts[a] = p; verts[b] = p;
                    // half 0: the barrel along x (height from z); half 1: the barrel along z (height from x). Inward ellipse normals.
                    normals[a] = -new Vector3(0f, y / (ry * ry), z / (rx * rx)).normalized;
                    normals[b] = -new Vector3(x / (rx * rx), y / (ry * ry), 0f).normalized;
                    float az = Mathf.Acos(Mathf.Clamp(z / rx, -1f, 1f)) * meanR, ax = Mathf.Acos(Mathf.Clamp(x / rx, -1f, 1f)) * meanR;
                    uvs[a] = new Vector2(x / tileMetres, az / tileMetres);
                    uvs[b] = new Vector2(ax / tileMetres, z / tileMetres);
                }
            var tris = new int[n * n * 6]; int k = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float cx = -rx + 2f * rx * (i + 0.5f) / n, cz = -rx + 2f * rx * (j + 0.5f) / n;
                    int o = Mathf.Abs(cz) <= Mathf.Abs(cx) ? 0 : per;
                    int a = o + i * cols + j, b = o + (i + 1) * cols + j, c = o + (i + 1) * cols + j + 1, d = o + i * cols + j + 1;
                    // the split follows the crease of this quadrant (x = z where x * z > 0, x = -z otherwise); front faces look down
                    if (cx * cz > 0f) { tris[k++] = a; tris[k++] = b; tris[k++] = c; tris[k++] = a; tris[k++] = c; tris[k++] = d; }
                    else { tris[k++] = a; tris[k++] = b; tris[k++] = d; tris[k++] = b; tris[k++] = c; tris[k++] = d; }
                }
            var mesh = new Mesh { name = "LookTestGroinVault" };
            mesh.vertices = verts; mesh.normals = normals; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }
}
