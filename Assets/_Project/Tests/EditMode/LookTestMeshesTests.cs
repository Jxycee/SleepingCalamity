// Authority: none (pure EditMode tests). The look-test scenes (M8) tile their materials in metres: every greybox shape generates its own UVs so one
// texture tile spans a fixed real-world length, whatever the shape's size.
using NUnit.Framework;
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class LookTestMeshesTests
    {
        [Test]
        public void Box_FrontFace_UvSpansSizeOverTile()
        {
            Mesh mesh = LookTestMeshes.Box(new Vector3(4f, 2f, 0.4f), 2f, false);
            Vector2 max = new Vector2(float.MinValue, float.MinValue), min = new Vector2(float.MaxValue, float.MaxValue);
            Vector3[] normals = mesh.normals; Vector2[] uv = mesh.uv;
            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].z < 0.99f) continue;   // the +Z face
                max = Vector2.Max(max, uv[i]); min = Vector2.Min(min, uv[i]);
            }
            Assert.AreEqual(2.0f, max.x - min.x, 1e-4f, "4 m wide / 2 m tile");
            Assert.AreEqual(1.0f, max.y - min.y, 1e-4f, "2 m high / 2 m tile");
            Assert.AreEqual(24, mesh.vertexCount);
            Assert.AreEqual(36, mesh.triangles.Length);
        }

        [Test]
        public void Box_WindingFacesOutward()
        {
            Mesh mesh = LookTestMeshes.Box(new Vector3(3f, 2f, 1f), 2f, false);
            Vector3[] v = mesh.vertices, n = mesh.normals; int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                // Unity is left-handed: a front-facing triangle's cross(v1-v0, v2-v0) points at the viewer, along the outward normal.
                Assert.Greater(Vector3.Dot(face, n[t[i]]), 0f, "triangle " + i / 3 + " is front-facing seen from outside");
            }
        }

        [Test]
        public void Box_TangentsAreUnitAndPerpendicularToNormals()
        {
            Mesh mesh = LookTestMeshes.Box(new Vector3(34f, 0.3f, 32f), 2f, false);
            Vector4[] t = mesh.tangents; Vector3[] n = mesh.normals;
            Assert.AreEqual(mesh.vertexCount, t.Length, "normal-mapped materials need tangents");
            for (int i = 0; i < t.Length; i++)
            {
                var tv = new Vector3(t[i].x, t[i].y, t[i].z);
                Assert.AreEqual(1f, tv.magnitude, 1e-3f, "tangent " + i + " is a unit vector");
                Assert.AreEqual(0f, Vector3.Dot(tv, n[i]), 1e-3f, "tangent " + i + " is perpendicular to its normal");
                Assert.AreEqual(1f, Mathf.Abs(t[i].w), 1e-4f);
            }
        }

        [Test]
        public void Box_TrimMode_FrontFaceVSpansOneStrip()
        {
            Mesh mesh = LookTestMeshes.Box(new Vector3(3f, 0.25f, 0.1f), 2f, true);
            float vmin = float.MaxValue, vmax = float.MinValue, umax = float.MinValue;
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                if (mesh.normals[i].z < 0.99f) continue;
                vmin = Mathf.Min(vmin, mesh.uv[i].y); vmax = Mathf.Max(vmax, mesh.uv[i].y); umax = Mathf.Max(umax, mesh.uv[i].x);
            }
            Assert.AreEqual(0f, vmin, 1e-4f);
            Assert.AreEqual(1f, vmax, 1e-4f, "trim strips map the full strip height to v 0..1 (the material scales it to one strip of the sheet)");
            Assert.AreEqual(1.5f, umax, 1e-4f, "3 m long / 2 m sheet length");
        }

        [Test]
        public void VaultInterior_NormalsAndWindingFaceTheAxis()
        {
            Mesh mesh = LookTestMeshes.VaultInterior(2.4f, 8f, 24, 2f);
            Vector3[] v = mesh.vertices, n = mesh.normals; int[] t = mesh.triangles;
            Assert.Greater(v.Length, 0);
            for (int i = 0; i < v.Length; i++)
                Assert.Less(Vector3.Dot(n[i], new Vector3(v[i].x, v[i].y, 0f)), 0f, "vertex normal points toward the axis (inward)");
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Vector3 centre = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                // Left-handed: a front-facing triangle's cross(v1-v0, v2-v0) points at the viewer, here inward (toward the axis).
                Assert.Less(Vector3.Dot(face, new Vector3(centre.x, centre.y, 0f)), 0f, "winding is front-facing seen from inside the vault");
            }
        }

        [Test]
        public void VaultInterior_UvIsArcLengthOverTile()
        {
            Mesh mesh = LookTestMeshes.VaultInterior(2f, 6f, 16, 2f);
            float umax = float.MinValue, vmax = float.MinValue;
            foreach (Vector2 uv in mesh.uv) { umax = Mathf.Max(umax, uv.x); vmax = Mathf.Max(vmax, uv.y); }
            Assert.AreEqual(Mathf.PI * 2f / 2f, umax, 1e-3f, "half circumference / tile");
            Assert.AreEqual(3f, vmax, 1e-4f, "6 m length / 2 m tile");
        }
    }
}
