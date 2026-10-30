// Authority: none (pure EditMode tests). Core hero movement: gait speeds, turning, dodge stub timing, animation-state derivation.
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class HeroMovementTests
    {
        [Test]
        public void TargetSpeed_DeadZoneWalkJogRun()
        {
            Assert.That(MovementMath.TargetSpeed(0.05f, false), Is.EqualTo(0f), "inside the dead zone");
            Assert.That(MovementMath.TargetSpeed(0.5f, false), Is.EqualTo(MovementMath.WalkSpeed).Within(1e-4f), "half tilt walks");
            Assert.That(MovementMath.TargetSpeed(1f, false), Is.EqualTo(MovementMath.JogSpeed).Within(1e-4f), "full stick jogs");
            Assert.That(MovementMath.TargetSpeed(1f, true), Is.EqualTo(MovementMath.RunSpeed), "sprint runs");
            Assert.That(MovementMath.TargetSpeed(0.75f, false), Is.GreaterThan(MovementMath.WalkSpeed).And.LessThan(MovementMath.JogSpeed));
            Assert.That(MovementMath.TargetSpeed(0f, true), Is.EqualTo(0f), "sprint without input does not move");
        }

        [Test]
        public void GaitOf_MatchesTheGaitSpeeds()
        {
            Assert.That(MovementMath.GaitOf(0f), Is.EqualTo(Gait.Idle));
            Assert.That(MovementMath.GaitOf(MovementMath.WalkSpeed), Is.EqualTo(Gait.Walk));
            Assert.That(MovementMath.GaitOf(MovementMath.JogSpeed), Is.EqualTo(Gait.Jog));
            Assert.That(MovementMath.GaitOf(MovementMath.RunSpeed), Is.EqualTo(Gait.Run));
        }

        [Test]
        public void TurnToward_TakesTheShortWayAndDoesNotOvershoot()
        {
            Assert.That(MovementMath.TurnToward(170f, -170f, 30f), Is.EqualTo(-170f).Within(1e-4f), "20 degrees across the seam, 30 allowed");
            Assert.That(Mathf180(MovementMath.TurnToward(170f, -170f, 10f)), Is.EqualTo(180f).Within(1e-3f), "10 of the 20 degrees");
            Assert.That(MovementMath.TurnToward(0f, 90f, 30f), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(MovementMath.TurnToward(0f, -90f, 30f), Is.EqualTo(-30f).Within(1e-4f));
        }

        private static float Mathf180(float deg) => deg < 0f ? deg + 360f : deg;   // 180 and -180 are the same direction

        [Test]
        public void YawDeg_ZeroIsPlusZ()
        {
            Assert.That(MovementMath.YawDeg(0f, 1f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(MovementMath.YawDeg(1f, 0f), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(Mathf180(MovementMath.YawDeg(0f, -1f)), Is.EqualTo(180f).Within(1e-3f));
            Assert.That(MovementMath.YawDeg(0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void JumpVelocity_PeaksAtJumpHeight()
        {
            float vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, MovementMath.Gravity);
            float peak = 0f, y = 0f;
            for (int i = 0; i < 200; i++)
            {
                y += MovementMath.VerticalStep(ref vy, MovementMath.Gravity, MovementMath.TerminalVelocity, 1f / 30f);
                if (y > peak) peak = y;
            }
            Assert.That(peak, Is.EqualTo(MovementMath.JumpHeight).Within(0.02f));
        }

        [Test]
        public void JumpAssist_CoyoteAndBuffer()
        {
            JumpAssist a = JumpAssist.Create();
            Assert.That(a.Tick(0.016f, false, true), Is.False, "pressed in the air, never grounded");
            Assert.That(a.Tick(0.016f, true, false), Is.True, "lands inside the buffer: jumps");
            Assert.That(a.Tick(0.016f, true, false), Is.False, "fires once");

            a = JumpAssist.Create();
            a.Tick(0.016f, true, false);
            Assert.That(a.Tick(0.05f, false, true), Is.True, "coyote: just left the ground");
        }

        // Catches: a dodge that never ends, one that can be restarted mid-roll, or one with no recovery gap.
        [Test]
        public void Dodge_RunsForHalfASecond_ThenCoolsDown()
        {
            var d = new DodgeState();
            Assert.That(d.TryStart(0f, 0f), Is.False, "no direction");
            Assert.That(d.TryStart(3f, 4f), Is.True);
            Assert.That(d.DirX, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(d.DirZ, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(d.TryStart(1f, 0f), Is.False, "already rolling");

            for (int i = 0; i < 29; i++) d.Tick(1f / 60f);   // 0.483 s
            Assert.That(d.Active, Is.True);
            d.Tick(1f / 60f);                                  // 0.5 s
            Assert.That(d.Active, Is.False);
            Assert.That(d.TryStart(1f, 0f), Is.False, "cooldown");
            for (int i = 0; i < 20; i++) d.Tick(1f / 60f);
            Assert.That(d.TryStart(1f, 0f), Is.True);
        }

        [Test]
        public void AnimState_StrafeIsRelativeToFacing()
        {
            // Facing +X (yaw 90) and moving +X at run speed: all forward.
            HeroAnimState fwd = HeroAnimState.FromWorldVelocity(MovementMath.RunSpeed, 0f, 0f, 90f, true, false, false);
            Assert.That(fwd.StrafeZ, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fwd.StrafeX, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(fwd.Gait, Is.EqualTo(Gait.Run));

            // Facing +Z, moving +X (the hero's right): pure strafe right.
            HeroAnimState right = HeroAnimState.FromWorldVelocity(MovementMath.JogSpeed, 0f, 0f, 0f, true, true, false);
            Assert.That(right.StrafeX, Is.GreaterThan(0.6f));
            Assert.That(right.StrafeZ, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(right.Locked, Is.True);

            HeroAnimState still = HeroAnimState.FromWorldVelocity(0f, -2f, 0f, 0f, false, false, true);
            Assert.That(still.Gait, Is.EqualTo(Gait.Idle));
            Assert.That(still.Grounded, Is.False);
            Assert.That(still.Dodging, Is.True);
        }
    }
}
