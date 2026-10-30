// Core, no engine references, unit-tested. Hero movement as pure functions. Ported from FishGame's MovementMath (spec 7.3): the ground/air
// acceleration, the exact-integration vertical step and the jump formula; the water, ladder and carry rules did not come across.
// New in M2: the walk / jog / run gaits (analog stick -> speed), the dodge numbers and the turn rates.
using System;

namespace SleepingCalamity.Core
{
    public enum Gait : byte { Idle = 0, Walk = 1, Jog = 2, Run = 3 }

    public static class MovementMath
    {
        // Gaits (m/s). A half-tilted stick walks, a full stick jogs, sprint runs. Tune in M3 and M7.
        public const float WalkSpeed = 2.2f, JogSpeed = 4.8f, RunSpeed = 7.0f;
        public const float WalkStickMax = 0.5f;    // stick magnitude up to which the hero walks
        public const float MoveDeadZone = 0.1f;
        public const float GroundAccel = 40f, AirAccel = 6f;
        public const float Gravity = -22f, TerminalVelocity = -30f, GroundedStickVelocity = -2f;
        public const float JumpHeight = 1.2f, CoyoteTime = 0.1f, JumpBufferTime = 0.1f;
        public const float TurnRateDegPerSec = 720f, LockedTurnRateDegPerSec = 900f;
        public const float DodgeDuration = 0.5f, DodgeSpeed = 7.0f, DodgeCooldown = 0.15f;   // 0.5 s roll (6.5); i-frames arrive with M3
        public const float CapsuleHeight = 1.8f, CapsuleRadius = 0.35f, StepOffset = 0.4f, SlopeLimit = 45f, SkinWidth = 0.05f;
        public const float CameraPivotHeight = 1.5f;

        // sqrt(2 * |gravity| * jumpHeight)
        public static float JumpVelocity(float jumpHeight, float gravity) => MathF.Sqrt(2f * MathF.Abs(gravity) * jumpHeight);

        // Wish speed from the stick magnitude (0..1) and the sprint button. Zero inside the dead zone.
        public static float TargetSpeed(float stickMagnitude, bool sprint)
        {
            if (!(stickMagnitude > MoveDeadZone)) return 0f;
            if (sprint) return RunSpeed;
            float m = stickMagnitude > 1f ? 1f : stickMagnitude;
            if (m <= WalkStickMax) return WalkSpeed * (m / WalkStickMax);
            float t = (m - WalkStickMax) / (1f - WalkStickMax);
            return WalkSpeed + (JogSpeed - WalkSpeed) * t;
        }

        public static Gait GaitOf(float speed)
        {
            if (speed < 0.2f) return Gait.Idle;
            if (speed <= 3.2f) return Gait.Walk;
            if (speed <= 5.9f) return Gait.Jog;
            return Gait.Run;
        }

        // Moves (vx, vz) toward (targetX, targetZ) by at most accel * dt (vector length); arrives exactly when closer.
        public static void Accelerate(ref float vx, ref float vz, float targetX, float targetZ, float accel, float dt)
        {
            float dx = targetX - vx;
            float dz = targetZ - vz;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            float maxStep = accel * dt;
            if (dist <= maxStep)
            {
                vx = targetX;
                vz = targetZ;
                return;
            }
            float k = maxStep / dist;
            vx += dx * k;
            vz += dz * k;
        }

        // Returns dy = vy*dt + 0.5*gravity*dt*dt, then sets vy = max(vy + gravity*dt, terminalVelocity). Exact for constant gravity, so a
        // JumpVelocity launch peaks at JumpHeight whatever the frame rate.
        public static float VerticalStep(ref float vy, float gravity, float terminalVelocity, float dt)
        {
            float dy = vy * dt + 0.5f * gravity * dt * dt;
            vy = MathF.Max(vy + gravity * dt, terminalVelocity);
            return dy;
        }

        // Yaw (degrees, 0 = +Z, 90 = +X) of a flat vector; 0 for a zero vector.
        public static float YawDeg(float x, float z) => (x == 0f && z == 0f) ? 0f : MathF.Atan2(x, z) * (180f / MathF.PI);

        // Wraps to [-180, 180).
        public static float WrapDeg(float deg)
        {
            deg %= 360f;
            if (deg >= 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
        }

        // Turns `current` toward `target` by at most maxStep degrees, the short way round.
        public static float TurnToward(float current, float target, float maxStep)
        {
            float delta = WrapDeg(target - current);
            if (MathF.Abs(delta) <= maxStep) return target;
            return WrapDeg(current + (delta > 0f ? maxStep : -maxStep));
        }
    }
}
