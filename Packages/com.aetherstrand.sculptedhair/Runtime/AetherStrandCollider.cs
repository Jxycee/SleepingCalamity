using UnityEngine;

namespace AetherStrand
{
    public enum HairColliderShape
    {
        Sphere,
        Capsule
    }

    /// <summary>
    /// Fast analytical Sphere / Capsule collider for head, jaw, neck, chest, and shoulder hair collision.
    /// </summary>
    public class AetherStrandCollider : MonoBehaviour
    {
        public HairColliderShape shape = HairColliderShape.Sphere;
        public Vector3 centerOffset = Vector3.zero;
        [Min(0.005f)] public float radius = 0.11f;
        [Min(0.0f)] public float capsuleHeight = 0.16f;
        public Vector3 capsuleAxis = Vector3.up;

        private float GetUniformScale()
        {
            Vector3 s = transform.lossyScale;
            return Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
        }

        public float SignedDistance(Vector3 pointWS, float hairRadius)
        {
            float scale = GetUniformScale();
            float minDist = radius * scale + hairRadius;

            if (shape == HairColliderShape.Sphere)
            {
                Vector3 centerWS = transform.TransformPoint(centerOffset);
                return Vector3.Distance(pointWS, centerWS) - minDist;
            }

            Vector3 axisDir = transform.TransformDirection(capsuleAxis.normalized);
            Vector3 center = transform.TransformPoint(centerOffset);
            float halfSeg = Mathf.Max(0f, capsuleHeight * scale * 0.5f);
            Vector3 p0 = center - axisDir * halfSeg;
            Vector3 p1 = center + axisDir * halfSeg;

            Vector3 seg = p1 - p0;
            float segLenSq = seg.sqrMagnitude;
            float t = segLenSq > 0.00001f
                ? Mathf.Clamp01(Vector3.Dot(pointWS - p0, seg) / segLenSq)
                : 0.5f;

            Vector3 closest = p0 + seg * t;
            return Vector3.Distance(pointWS, closest) - minDist;
        }

        public bool ResolveCollision(ref Vector3 pointWS, float hairRadius)
        {
            float scale = GetUniformScale();
            float minDist = radius * scale + hairRadius;

            if (shape == HairColliderShape.Sphere)
            {
                Vector3 centerWS = transform.TransformPoint(centerOffset);
                Vector3 delta = pointWS - centerWS;
                float sqrMag = delta.sqrMagnitude;
                if (sqrMag < minDist * minDist && sqrMag > 0.000001f)
                {
                    pointWS = centerWS + delta * (minDist / Mathf.Sqrt(sqrMag));
                    return true;
                }
                return false;
            }

            Vector3 axisDir = transform.TransformDirection(capsuleAxis.normalized);
            Vector3 center = transform.TransformPoint(centerOffset);
            float halfSeg = Mathf.Max(0f, capsuleHeight * scale * 0.5f);
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
            {
                pointWS = closest + diff * (minDist / Mathf.Sqrt(dSq));
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.22f, 0.74f, 0.97f, 0.55f);
            float scale = GetUniformScale();
            Vector3 center = transform.TransformPoint(centerOffset);
            if (shape == HairColliderShape.Sphere)
            {
                Gizmos.DrawWireSphere(center, radius * scale);
            }
            else
            {
                Vector3 axisDir = transform.TransformDirection(capsuleAxis.normalized);
                float halfSeg = Mathf.Max(0f, capsuleHeight * scale * 0.5f);
                Vector3 p0 = center - axisDir * halfSeg;
                Vector3 p1 = center + axisDir * halfSeg;
                Gizmos.DrawWireSphere(p0, radius * scale);
                Gizmos.DrawWireSphere(p1, radius * scale);
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
