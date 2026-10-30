// Core, no engine references, unit-tested. What an animation driver needs each frame: the seam between the hero's motion and the clips that
// arrive later (docs/design/mixamo-clips.md). Speed, strafe and vertical speed derive from the hero's transform motion, so a remote copy moves
// the same way as the owner's; Locked and Dodging are the owner's stance (a replicated flag byte, HeroNetState.NetStance), because motion
// alone cannot tell a strafe from a walk. StrafeX/StrafeZ are the local-space velocity divided by RunSpeed (about -1..1).
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

        // Combat, filled by the feeder from HeroCombat / HeroVitals (all replicated already: NetSwing, the stance flags, the vitals state, the hurt RPC).
        public byte Weapon;          // heirloom index: 0 axe, 1 staff, 2 dagger, 3 bow
        public byte AttackId;        // HeroAnimMap.AttackId of the swing being shown, 0 = none
        public float AttackProgress; // 0..1 through that swing
        public bool Guarding;
        public bool Aiming;
        public bool Downed;          // downed or out: lying down
        public int HurtSerial;       // counts up on every flinch-worthy hit; a change fires the hurt trigger
        public bool HurtHeavy;       // the latest hurt was a heavy one

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
