// Core, no engine references, unit-tested. What an animation driver needs each frame: the seam between the hero's motion and the clips that
// arrive later (docs/design/mixamo-clips.md). Everything derives from the hero's transform motion, so a remote copy of a hero animates the
// same way as the owner's with no extra sync. StrafeX/StrafeZ are the local-space velocity divided by RunSpeed (about -1..1).
using System;

namespace SleepingCalamity.Core
{
    public struct HeroAnimState
    {
        public float Speed;          // flat speed, m/s
        public float StrafeX;        // + right of the hero's facing
        public float StrafeZ;        // + along the hero's facing
        public float VerticalSpeed;  // m/s, + up
        public bool Grounded;
        public bool Locked;          // hard lock active (the strafing clip set)
        public bool Dodging;
        public Gait Gait;

        // vx, vy, vz: world velocity; facingYawDeg: the hero's yaw (0 = +Z).
        public static HeroAnimState FromWorldVelocity(float vx, float vy, float vz, float facingYawDeg, bool grounded, bool locked, bool dodging)
        {
            float yaw = facingYawDeg * (MathF.PI / 180f);
            float sin = MathF.Sin(yaw), cos = MathF.Cos(yaw);
            float speed = MathF.Sqrt(vx * vx + vz * vz);
            return new HeroAnimState
            {
                Speed = speed,
                StrafeZ = (vx * sin + vz * cos) / MovementMath.RunSpeed,
                StrafeX = (vx * cos - vz * sin) / MovementMath.RunSpeed,
                VerticalSpeed = vy,
                Grounded = grounded,
                Locked = locked,
                Dodging = dodging,
                Gait = MovementMath.GaitOf(speed),
            };
        }
    }
}
