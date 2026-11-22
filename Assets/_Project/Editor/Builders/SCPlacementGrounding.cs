// Authority: none (editor build step). Ground-blend step: Neon's TryGroundAndSink, fixed. A placed prop is checked against its host with a five-point footprint (centre plus four
// inset corners), then dropped onto the lowest support and sunk by a fraction of its height, so rubble and caps sit IN the ground instead of hovering on one corner.
// Changes from Neon's version: physics is synced before the rays; hits are filtered by a host predicate (default: terrain only) and the nearest accepted hit wins, so a tree
// or a neighbouring wall is never taken for the ground; own colliders are skipped by hierarchy (no enabling and disabling); the target is left untouched on failure;
// the support and the bounds are measured the same way before and after a tilt. Deterministic: fixed ray order, no randomness.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class SCPlacementGrounding
    {
        public static readonly Predicate<Collider> TerrainOnly = c => c is TerrainCollider;

        /// <summary>
        /// Grounds target onto its host. Returns false (target unchanged) when fewer than minSupportHits of the five rays find the host, the centre ray misses, or the host
        /// is steeper than maxSlopeDeg. sinkFraction is the share of the target's height (after any tilt) that ends up below the lowest support.
        /// </summary>
        public static bool TryGroundAndSink(Transform target, Predicate<Collider> isHost = null, float sinkFraction = 0.20f, float maxSlopeDeg = 45f, bool alignToNormal = false,
            int minSupportHits = 3, float footprintInset = 0.65f, float rayStartAbove = 5f, float rayDistance = 20f)
        {
            if (target == null) return false;
            isHost = isHost ?? TerrainOnly;
            Physics.SyncTransforms();
            if (!TryBounds(target, out Bounds b)) return false;

            float ex = b.extents.x * footprintInset, ez = b.extents.z * footprintInset, startY = b.max.y + rayStartAbove;
            Vector3 c = b.center;
            // fixed order: centre first, then the four corners
            Vector3[] origins =
            {
                new Vector3(c.x, startY, c.z), new Vector3(c.x - ex, startY, c.z - ez), new Vector3(c.x + ex, startY, c.z - ez),
                new Vector3(c.x - ex, startY, c.z + ez), new Vector3(c.x + ex, startY, c.z + ez),
            };
            int hits = 0; bool centreHit = false; float lowest = float.PositiveInfinity; Vector3 normal = Vector3.zero;
            for (int i = 0; i < origins.Length; i++)
            {
                if (!NearestHost(origins[i], target, isHost, rayDistance, out RaycastHit hit)) continue;
                hits++; if (i == 0) centreHit = true;
                if (hit.point.y < lowest) lowest = hit.point.y;
                normal += hit.normal;
            }
            if (!centreHit || hits < minSupportHits) return false;
            normal.Normalize();
            if (Vector3.Angle(Vector3.up, normal) > maxSlopeDeg) return false;

            Quaternion oldRot = target.rotation;
            if (alignToNormal)
            {
                target.rotation = Quaternion.FromToRotation(Vector3.up, normal) * oldRot;   // yaw kept, tilted to the host
                if (!TryBounds(target, out b)) { target.rotation = oldRot; return false; }
            }
            target.position += new Vector3(0f, lowest - b.min.y - Mathf.Clamp01(sinkFraction) * b.size.y, 0f);
            Physics.SyncTransforms();
            return true;
        }

        static bool NearestHost(Vector3 origin, Transform self, Predicate<Collider> isHost, float dist, out RaycastHit best)
        {
            best = default; float bestD = float.PositiveInfinity; bool found = false;
            foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(self) || !isHost(h.collider)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }
            return found;
        }

        static bool TryBounds(Transform root, out Bounds bounds)
        {
            bool any = false; bounds = default;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
