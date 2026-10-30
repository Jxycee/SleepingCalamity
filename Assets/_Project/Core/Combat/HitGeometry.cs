// Core/Combat/HitGeometry.cs: does an attack's hitbox reach a target? Flat (XZ) tests plus a height band. Pure C#.
// Authority: none. The attacker's client uses it for player attacks (it sees the moment that matters), the victim's client for enemy attacks.
using System;

namespace SleepingCalamity.Core.Combat
{
    public static class HitGeometry
    {
        const float DegToRad = MathF.PI / 180f;

        // yaw 0 faces +Z, 90 faces +X (the project's MovementMath.YawDeg).
        public static float YawDegOf(float dx, float dz) => MathF.Atan2(dx, dz) / DegToRad;

        public static float AngleBetweenDeg(float yawA, float yawB)
        {
            float d = (yawB - yawA) % 360f;
            if (d > 180f) d -= 360f;
            else if (d < -180f) d += 360f;
            return MathF.Abs(d);
        }

        // A target is a vertical cylinder of targetRadius. The cone reaches it when its nearest point is inside range and the direction to its centre is
        // inside the half angle, widened by the angle the cylinder itself covers (so a fat target at the edge of the arc still counts).
        public static bool InCone(float ox, float oz, float yawDeg, float tx, float tz, float targetRadius, float range, float halfAngleDeg)
        {
            float dx = tx - ox, dz = tz - oz;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            if (dist - targetRadius > range) return false;
            if (dist <= targetRadius + 0.05f) return true;   // standing inside it
            float slack = MathF.Asin(MathF.Min(1f, targetRadius / dist)) / DegToRad;
            return AngleBetweenDeg(yawDeg, YawDegOf(dx, dz)) <= halfAngleDeg + slack;
        }

        public static bool InCircle(float ox, float oz, float tx, float tz, float targetRadius, float radius)
        {
            float dx = tx - ox, dz = tz - oz;
            float reach = radius + targetRadius;
            return dx * dx + dz * dz <= reach * reach;
        }

        // The attack's shape from the origin (ox, oy, oz) facing yawDeg against a target cylinder at (tx, ty, tz) with the given radius and height.
        // Rays are not tested here (the caller raycasts).
        public static bool Hits(AttackData a, float ox, float oy, float oz, float yawDeg, float tx, float ty, float tz, float targetRadius, float targetHeight = 1.8f)
        {
            if (ty > oy + a.HeightBand || ty + targetHeight < oy - a.HeightBand) return false;
            switch (a.Shape)
            {
                case HitShape.Circle: return InCircle(ox, oz, tx, tz, targetRadius, a.Range);
                case HitShape.Cone: return InCone(ox, oz, yawDeg, tx, tz, targetRadius, a.Range, a.HalfAngleDeg);
                default: return false;
            }
        }
    }
}
