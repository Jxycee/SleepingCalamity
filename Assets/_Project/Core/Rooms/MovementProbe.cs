// Core/Rooms/MovementProbe.cs: steps the hero's real movement rules at a fixed rate and reports how far a dodge and a jump carry (metrics gym plan, Task 1; room-authoring spec).
using System;

namespace SleepingCalamity.Core
{
    // Pure simulation, no engine: it mirrors the order HeroLocomotion ticks (dodge, accelerate, vertical step) so the numbers match the game.
    public static class MovementProbe
    {
        public const float Hz = 60f;
        const float Dt = 1f / Hz;
        const int MaxSteps = 600;   // 10 s guard against a loop that never lands

        // Metres covered by one dodge: constant DodgeSpeed while the DodgeState is active.
        public static float DodgeDistance()
        {
            var d = new DodgeState();
            d.TryStart(0f, 1f);
            float dist = MovementMath.DodgeSpeed * Dt;   // the frame the dodge starts also moves (HeroLocomotion starts it after its Tick)
            for (int i = 0; i < MaxSteps; i++)
            {
                d.Tick(Dt);
                if (!d.Active) break;
                dist += MovementMath.DodgeSpeed * Dt;
            }
            return dist;
        }

        // Highest point of a standing jump above the take-off height.
        public static float JumpApex()
        {
            float vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, MovementMath.Gravity), y = 0f, apex = 0f;
            for (int i = 0; i < MaxSteps; i++)
            {
                y += MovementMath.VerticalStep(ref vy, MovementMath.Gravity, MovementMath.TerminalVelocity, Dt);
                if (y > apex) apex = y;
                if (y <= 0f) break;
            }
            return apex;
        }

        // Flat distance of a jump taken at runSpeed: sprint needs the ground, so in the air the target speed falls to a jog.
        public static float RunJumpDistance(float runSpeed = MovementMath.RunSpeed)
        {
            float vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, MovementMath.Gravity), vx = runSpeed, vz = 0f, y = 0f, x = 0f;
            for (int i = 0; i < MaxSteps; i++)
            {
                MovementMath.Accelerate(ref vx, ref vz, MovementMath.JogSpeed, 0f, MovementMath.AirAccel, Dt);
                x += vx * Dt;
                y += MovementMath.VerticalStep(ref vy, MovementMath.Gravity, MovementMath.TerminalVelocity, Dt);
                if (y <= 0f) break;
            }
            return x;
        }
    }
}
