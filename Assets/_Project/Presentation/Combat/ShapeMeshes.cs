// Not networked. Flat meshes for hitbox and telegraph markers on the ground: a wedge (a cone's footprint) and a disc, in the XZ plane, facing +Z, the
// telegraph footprint of an enemy attack (the exact region its hit test reaches, Core Telegraph) and the lane a lunge travels along.
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class ShapeMeshes
    {
        private static readonly Dictionary<(AttackData, int), Mesh> s_footprints = new Dictionary<(AttackData, int), Mesh>();
        private static Mesh s_lane;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_footprints.Clear(); s_lane = null; }

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

        // The region an enemy attack reaches against a target of targetRadius, as a fan around the attack's centre (Core Telegraph.Radius per direction).
        // Cached per attack. The same mesh is scaled about the centre to show the wind-up filling.
        public static Mesh Footprint(AttackData a, float targetRadius)
        {
            var key = (a, Mathf.RoundToInt(targetRadius * 1000f));
            if (s_footprints.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            // Angles: every 4 degrees, plus the kinks where the cone's edge starts and where the "inside it" disc takes over.
            var angles = new List<float>();
            for (float t = -180f; t < 180f - 0.01f; t += 4f) angles.Add(t);
            angles.Add(180f);
            foreach (float k in new[] { a.HalfAngleDeg, a.HalfAngleDeg + 90f })
                foreach (float sgn in new[] { -1f, 1f })
                {
                    float t = sgn * k;
                    if (t > -180f && t < 180f) angles.Add(t);
                }
            angles.Sort();

            var verts = new List<Vector3>(angles.Count + 1) { Vector3.zero };
            foreach (float t in angles)
            {
                float r = Telegraph.Radius(a, t, targetRadius);
                float rad = t * Mathf.Deg2Rad;
                verts.Add(new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * r);
            }
            var tris = new List<int>();
            for (int i = 1; i < verts.Count - 1; i++) { tris.Add(0); tris.Add(i + 1); tris.Add(i); tris.Add(0); tris.Add(i); tris.Add(i + 1); }   // both windings: visible from above whatever the material culls
            var mesh = new Mesh { name = "Footprint_" + a.Id };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            s_footprints[key] = mesh;
            return mesh;
        }

        // A unit lane from the origin forward (z 0..1), one unit wide (x -0.5..0.5); scale it to the lunge's width and length.
        public static Mesh Lane()
        {
            if (s_lane != null) return s_lane;
            s_lane = new Mesh { name = "Lane" };
            s_lane.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f) };
            s_lane.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            s_lane.RecalculateNormals();
            s_lane.RecalculateBounds();
            return s_lane;
        }
    }
}
