// T13a ground and props (area 1 world, run 7, user rules 2026-11-18): props come in used groups and every one touches the ground, the cobbles carry wear, the earth banks are broken up.
//   - SquareWear: the wear mask baked into the draped square bed's vertex colours (SleepingCalamity/WearLit reads R grime, G moss, B polish). Walking lines (the path graph) are
//     polished and a little dark, the edges and the lot feet carry damp grime and moss that creeps in, low spots take damp earth, a low-frequency dirt breaks the repeat. No decals.
//     (The lanes get theirs from the terrain paint, HollowmereTerrain.LaneWear.)
//   - Build (called from HollowmereBuilder after the roadside, before Combine and Mesh LODs): the street clutter's lone props are grouped by use (companions beside them) or removed,
//     and every kept prop gets contact: a dirt ring (a flat quad, not a projector), a grass tuft or weed at its base, and chips for the stone and wood kinds.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereGround   // the props half is in HollowmereGroundProps.cs
    {
        static TownLayout cachedT;
        static readonly List<(Vector2 A, Vector2 B)> walk = new List<(Vector2, Vector2)>();

        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }

        static void Prep(TownLayout t)
        {
            if (ReferenceEquals(cachedT, t)) return;
            cachedT = t; walk.Clear();
            var nm = t.Nodes.ToDictionary(n => n.Id, n => new Vector2(n.Pos.X, n.Pos.Z));
            foreach (PathEdge e in t.Edges) if (nm.TryGetValue(e.A, out Vector2 a) && nm.TryGetValue(e.B, out Vector2 b)) walk.Add((a, b));
        }

        static float WalkDistance(float x, float z)
        {
            var p = new Vector2(x, z); float d = 1e9f;
            foreach (var e in walk)
            {
                Vector2 ab = e.B - e.A; float u = Mathf.Clamp01(Vector2.Dot(p - e.A, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                d = Mathf.Min(d, Vector2.Distance(p, e.A + ab * u));
            }
            return d;
        }

        // Wear colour for a point of the square bed at world (x, z); low = 0..1 how far it sits below its surroundings.
        internal static Color SquareWear(TownLayout t, float x, float z, float low)
        {
            Prep(t);
            float n1 = Mathf.PerlinNoise(x * 0.55f + 13f, z * 0.55f + 3f), n2 = Mathf.PerlinNoise(x * 1.7f + 41f, z * 1.7f + 9f), n3 = Mathf.PerlinNoise(x * 0.2f + 5f, z * 0.2f + 77f);
            float dIn = Mathf.Min(Mathf.Min(x - HollowmereLayout.SquareMinX, HollowmereLayout.SquareMaxX - x), Mathf.Min(z - HollowmereLayout.SquareMinZ, HollowmereLayout.SquareMaxZ - z));
            var tp = new TownPoint(x, z);
            float lot = 0f;
            foreach (Foundation f in t.Foundations)
            {
                if (f.Ring > 2) continue;
                if (f.Footprint.Contains(tp, 0.5f)) { lot = 1f; break; }
                if (f.Footprint.Contains(tp, 1.4f)) lot = Mathf.Max(lot, 0.6f);
                else if (f.Footprint.Contains(tp, 2.6f)) lot = Mathf.Max(lot, 0.3f);
            }
            float edge = Mathf.Max(lot, 1f - Smooth(0f, 2.2f + 1.6f * n1, dIn));
            float pd = WalkDistance(x, z);
            float line = 1f - Smooth(0.55f, 1.7f + 0.5f * n2, pd);
            float grime = 0.36f * edge * Mathf.Lerp(0.5f, 1f, n2) * (1f - 0.7f * line) + 0.10f * Smooth(0.45f, 0.82f, n2) * (1f - line) + 0.30f * low + 0.10f * line * Mathf.Lerp(0.5f, 1f, n1);
            float moss = 0.50f * edge * Smooth(0.34f, 0.68f, n1) * (1f - line) + 0.30f * Smooth(0.64f, 0.84f, n3) * (1f - line) * (0.4f + 0.6f * edge);
            float polish = 0.62f * line * Mathf.Lerp(0.55f, 1f, n2);
            return new Color(Mathf.Clamp01(grime), Mathf.Clamp01(moss), Mathf.Clamp01(polish), 1f);
        }
    }
}
