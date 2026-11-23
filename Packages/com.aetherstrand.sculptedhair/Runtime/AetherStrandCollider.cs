using UnityEngine;

namespace AetherStrand
{
    public enum HairColliderShape
    {
        Sphere,
        Capsule
    }

    /// <summary>
    /// Fast analytical Sphere / Capsule collider for head, face/jaw, neck, chest, and shoulder hair collision.
    /// </summary>
    public class AetherStrandCollider : MonoBehaviour
    {
        public HairColliderShape shape = HairColliderShape.Sphere;
        public Vector3 centerOffset = Vector3.zero;
        [Min(0.0001f)] public float radius = 0.12f;
        [Min(0.0f)] public float capsuleHeight = 0.22f;
        public Vector3 capsuleAxis = Vector3.up;

        private float GetUniformLossyScale()
        {
            Vector3 s = transform.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return avg > 1e-5f ? avg : 1f;
        }

        /// <summary>
        /// Exact signed distance from pointWS to collider surface (negative inside, 0 on surface, positive outside).
        /// </summary>
        public float SignedDistance(Vector3 pointWS)
        {
            float worldScale = GetUniformLossyScale();
            float worldRadius = radius * worldScale;
            Vector3 centerWS = transform.TransformPoint(centerOffset);
            if (shape == HairColliderShape.Sphere)
            {
                return Vector3.Distance(pointWS, centerWS) - worldRadius;
            }

            Vector3 axisDir = transform.TransformDirection(capsuleAxis.sqrMagnitude > 1e-6f ? capsuleAxis.normalized : Vector3.up);
            if (axisDir.sqrMagnitude > 1e-6f) axisDir.Normalize();
            else axisDir = Vector3.up;

            float halfSeg = Mathf.Max(0f, capsuleHeight * worldScale * 0.5f);
            Vector3 p0 = centerWS - axisDir * halfSeg;
            Vector3 p1 = centerWS + axisDir * halfSeg;

            Vector3 seg = p1 - p0;
            float segLenSq = seg.sqrMagnitude;
            float t = segLenSq > 0.00001f
                ? Mathf.Clamp01(Vector3.Dot(pointWS - p0, seg) / segLenSq)
                : 0.5f;

            Vector3 closest = p0 + seg * t;
            return Vector3.Distance(pointWS, closest) - worldRadius;
        }

        public void ResolveCollision(ref Vector3 pointWS, float hairRadius)
        {
            float worldScale = GetUniformLossyScale();
            float worldRadius = radius * worldScale;
            float minDist = worldRadius + hairRadius;

            if (shape == HairColliderShape.Sphere)
            {
                Vector3 centerWS = transform.TransformPoint(centerOffset);
                Vector3 delta = pointWS - centerWS;
                float sqrMag = delta.sqrMagnitude;
                if (sqrMag < minDist * minDist && sqrMag > 0.000001f)
                    pointWS = centerWS + delta * (minDist / Mathf.Sqrt(sqrMag));
                return;
            }

            Vector3 axisDir = transform.TransformDirection(capsuleAxis.sqrMagnitude > 1e-6f ? capsuleAxis.normalized : Vector3.up);
            if (axisDir.sqrMagnitude > 1e-6f) axisDir.Normalize();
            else axisDir = Vector3.up;

            Vector3 center = transform.TransformPoint(centerOffset);
            float halfSeg = Mathf.Max(0f, capsuleHeight * worldScale * 0.5f);
            Vector3 p0 = center - axisDir * halfSeg;
            Vector3 p1 = center + axisDir * halfSeg;

            Vector3 seg = p1 - p0;
            float segLenSq = seg.sqrMagnitude;
            float t = segLenSq > 0.00001f
                ? Mathf.Clamp01(Vector3.Dot(pointWS - p0, seg) / segLenSq)
                : 0.5f;

            Vector3 closest = p0 + seg * t;
            Vector3 diff = pointWS - closest;
            float dSq = diff.sqrMagnitude;
            if (dSq < minDist * minDist && dSq > 0.000001f)
                pointWS = closest + diff * (minDist / Mathf.Sqrt(dSq));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.22f, 0.74f, 0.97f, 0.55f);
            float worldScale = GetUniformLossyScale();
            float worldRadius = radius * worldScale;
            Vector3 center = transform.TransformPoint(centerOffset);
            if (shape == HairColliderShape.Sphere)
            {
                Gizmos.DrawWireSphere(center, worldRadius);
            }
            else
            {
                Vector3 axisDir = transform.TransformDirection(capsuleAxis.sqrMagnitude > 1e-6f ? capsuleAxis.normalized : Vector3.up);
                if (axisDir.sqrMagnitude > 1e-6f) axisDir.Normalize();
                else axisDir = Vector3.up;

                float halfSeg = Mathf.Max(0f, capsuleHeight * worldScale * 0.5f);
                Vector3 p0 = center - axisDir * halfSeg;
                Vector3 p1 = center + axisDir * halfSeg;
                Gizmos.DrawWireSphere(p0, worldRadius);
                Gizmos.DrawWireSphere(p1, worldRadius);
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
