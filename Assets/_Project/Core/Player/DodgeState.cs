// Core, no engine references, unit-tested. The dodge: a direction and a duration (M2), with the i-frame window (frames 5-18 of 30) and the owner's
// leniency from DodgeTiming (M3).
using System;

namespace SleepingCalamity.Core
{
    public struct DodgeState
    {
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public float DirX { get; private set; }
        public float DirZ { get; private set; }
        public float CooldownLeft { get; private set; }

        public bool CanStart => !Active && CooldownLeft <= 0f;

        // M3: invulnerable on frames 5-18 of the 30-frame roll, plus the owner's 100 ms leniency either side (DodgeTiming).
        public bool Invulnerable => Combat.DodgeTiming.IsInvulnerable(Active, Elapsed);
        public float Normalized => Active ? Elapsed / MovementMath.DodgeDuration : 0f;

        // Starts a dodge along the flat direction (normalised here). False when one is running, cooling down, or the direction is zero.
        public bool TryStart(float dirX, float dirZ)
        {
            float len2 = dirX * dirX + dirZ * dirZ;
            if (!CanStart || len2 < 1e-8f) return false;
            float inv = 1f / MathF.Sqrt(len2);
            DirX = dirX * inv;
            DirZ = dirZ * inv;
            Active = true;
            Elapsed = 0f;
            return true;
        }

        public void Tick(float dt)
        {
            if (Active)
            {
                Elapsed += dt;
                if (Elapsed >= MovementMath.DodgeDuration - 1e-4f)
                {
                    Active = false;
                    CooldownLeft = MovementMath.DodgeCooldown;
                }
            }
            else if (CooldownLeft > 0f)
            {
                CooldownLeft = CooldownLeft - dt < 0f ? 0f : CooldownLeft - dt;
            }
        }

        public void Cancel()
        {
            Active = false;
            Elapsed = 0f;
            CooldownLeft = 0f;
        }
    }
}
