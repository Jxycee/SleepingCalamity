// Authority: owner only. HeroNetState enables this component (after the CharacterController) on the owning peer and nowhere else.
// Moves the owner's CharacterController from the routed PlayerIntent; NetworkTransform (AuthorityMode Owner) replicates the result to everyone.
// The host never moves players (docs/PHASE1.md 6.4, P11). Ported from FishGame's PlayerLocomotion (intent pattern, jump assist, vertical step),
// reworked for a third-person hero: walk / jog / run by stick tilt and sprint, camera-relative movement from ViewYawDeg, a dodge roll (i-frames and cancels live in HeroCombat), and
// strafing (facing the target) while hard-locked. The view yaw / pitch live here, fed by the routed look intent (zero while locked), so
// movement needs no camera at all; the Presentation camera rig only reads them. Animation is not driven from here: HeroAnimationFeeder
// derives it from the transform's motion, which also covers remote copies.
using System;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class HeroLocomotion : MonoBehaviour
    {
        public const float FreePitchDownDeg = 15f, MinPitchDownDeg = 2f, MaxPitchDownDeg = 55f, LockPitchDownDeg = 18f;
        private const float LockYawSmoothSeconds = 0.14f, LockPitchSmoothSeconds = 0.25f;

        private CharacterController _cc;
        private TargetLockController _lockController;
        private HeroCombat _combat;          // M3: gates the dodge, scales the speed, supplies lunges, facing and knockback; null-safe
        private float _dodgeBuffer;
        private IInputService _input;
        private JumpAssist _jump;
        private DodgeState _dodge;
        private float _vx, _vz, _vy;
        private float _yawVelocity, _pitchVelocity;

        // Injected by tests (one input per hero in one process); falls back to the App's input service.
        public IInputService Input
        {
            get => _input ?? HeroInput.ResolveDefault();
            set => _input = value;
        }

        public float ViewYawDeg { get; private set; }        // where the camera looks (0 = +Z); movement is relative to this
        public float ViewPitchDownDeg { get; private set; }  // how far the camera looks down; + = down
        // Owner copy only; set by the DiveDirector while a dive-floor hand-over is unresolved (the floor under the hero was swapped, the arrival teleport has not landed).
        // Zeroes vertical velocity and the vertical step so the hero hangs instead of sinking through the missing cells. The director clears it on teleport, timeout and despawn.
        public bool HoldVertical { get; set; }
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsDodging => _dodge.Active;
        public bool IsInvulnerable => _dodge.Invulnerable;   // M3: dodge i-frames (frames 5-18 of 30, plus the owner's 100 ms leniency)
        public float DodgeElapsed => _dodge.Active ? _dodge.Elapsed : 0f;
        public Vector3 DodgeDirection => new Vector3(_dodge.DirX, 0f, _dodge.DirZ);
        public Vector3 Velocity { get; private set; }         // world velocity of the last frame (measured from the position)

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _lockController = GetComponent<TargetLockController>();
            _combat = GetComponent<HeroCombat>();
        }

        private void OnEnable()
        {
            ViewYawDeg = MovementMath.WrapDeg(transform.eulerAngles.y);
            ViewPitchDownDeg = FreePitchDownDeg;
            _vx = _vz = _vy = 0f;
            _yawVelocity = _pitchVelocity = 0f;
            _jump = JumpAssist.Create();
            _dodge.Cancel();
            IsGrounded = false;
            IsSprinting = false;
        }

        // Teleports and tests: sets where the view looks without turning the body.
        public void SetView(float yawDeg, float pitchDownDeg)
        {
            ViewYawDeg = MovementMath.WrapDeg(yawDeg);
            ViewPitchDownDeg = Mathf.Clamp(pitchDownDeg, MinPitchDownDeg, MaxPitchDownDeg);
        }

        private void Update()
        {
            IInputService input = Input;
            if (input == null || _cc == null || !_cc.enabled) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (HeroInput.IsPaused())
            {
                Velocity = Vector3.zero;
                IsSprinting = false;
                return;
            }

            PlayerIntent intent = input.Intent;
            Vector3 pos = transform.position;

            // ---- view: free look, or glued to the target while locked ----
            bool locked = _lockController != null && _lockController.IsLocked;
            Vector3 flatToTarget = Vector3.zero;
            if (locked)
            {
                Vector3 t = _lockController.Locked.transform.position;
                flatToTarget = new Vector3(t.x - pos.x, 0f, t.z - pos.z);
                if (flatToTarget.sqrMagnitude > 0.04f)
                    ViewYawDeg = MovementMath.WrapDeg(Mathf.SmoothDampAngle(ViewYawDeg, MovementMath.YawDeg(flatToTarget.x, flatToTarget.z), ref _yawVelocity, LockYawSmoothSeconds));
                ViewPitchDownDeg = Mathf.SmoothDamp(ViewPitchDownDeg, LockPitchDownDeg, ref _pitchVelocity, LockPitchSmoothSeconds);
            }
            else
            {
                _yawVelocity = _pitchVelocity = 0f;
                ViewYawDeg = MovementMath.WrapDeg(ViewYawDeg + intent.LookYawDeg);
                ViewPitchDownDeg = Mathf.Clamp(ViewPitchDownDeg - intent.LookPitchDeg, MinPitchDownDeg, MaxPitchDownDeg);
            }

            // ---- wish direction, relative to the view ----
            float yawRad = ViewYawDeg * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 wish = right * intent.MoveX + forward * intent.MoveY;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            float stick = wish.magnitude;

            bool grounded = _cc.isGrounded;
            IsGrounded = grounded;

            // ---- dodge: a direction and a duration; HeroCombat gates when it may start and its machine owns the i-frames ----
            _dodge.Tick(dt);
            // A press the attack is not ready to give way to yet (mid-swing, before its cancel frame) waits 0.2 s for it.
            if (intent.DodgePressed) _dodgeBuffer = 0.2f;
            else if (_dodgeBuffer > 0f) _dodgeBuffer -= dt;
            if (_dodgeBuffer > 0f && grounded && _dodge.CanStart && (_combat == null || _combat.AllowsDodge))
            {
                Vector3 dir = stick > MovementMath.MoveDeadZone ? wish : -transform.forward;   // no input: a back-step
                dir.y = 0f;
                if (_dodge.TryStart(dir.x, dir.z)) _dodgeBuffer = 0f;
            }
            bool dodging = _dodge.Active;
            float moveScale = _combat != null ? _combat.MoveScale : 1f;

            // ---- horizontal velocity ----
            bool sprint = grounded && intent.SprintHeld && !dodging;
            IsSprinting = sprint && stick > MovementMath.MoveDeadZone;
            float speed = MovementMath.TargetSpeed(stick, sprint) * moveScale;
            float lunge = _combat != null && !dodging ? _combat.LungeSpeed : 0f;
            float targetX, targetZ;
            if (dodging)
            {
                targetX = _dodge.DirX * MovementMath.DodgeSpeed;
                targetZ = _dodge.DirZ * MovementMath.DodgeSpeed;
                _vx = targetX;   // a roll is instant, not accelerated
                _vz = targetZ;
            }
            else if (lunge > 0f)
            {
                _vx = transform.forward.x * lunge;   // an attack's lunge is a slide along the swing's facing, not accelerated
                _vz = transform.forward.z * lunge;
            }
            else
            {
                Vector3 dirN = stick > MovementMath.MoveDeadZone ? wish / stick : Vector3.zero;
                targetX = dirN.x * speed;
                targetZ = dirN.z * speed;
                MovementMath.Accelerate(ref _vx, ref _vz, targetX, targetZ, grounded ? MovementMath.GroundAccel : MovementMath.AirAccel, dt);
            }

            // ---- facing ----
            float yaw = transform.eulerAngles.y;
            if (!dodging && _combat != null && _combat.WantsFacing(out float combatYaw, out float combatRate))
            {
                yaw = combatRate > 0f ? MovementMath.TurnToward(yaw, combatYaw, combatRate * dt) : yaw;   // a swing owns the facing (rate 0 holds it)
            }
            else if (dodging)
            {
                yaw = MovementMath.TurnToward(yaw, MovementMath.YawDeg(_dodge.DirX, _dodge.DirZ), MovementMath.LockedTurnRateDegPerSec * dt);
            }
            else if (locked && flatToTarget.sqrMagnitude > 0.04f)
            {
                yaw = MovementMath.TurnToward(yaw, MovementMath.YawDeg(flatToTarget.x, flatToTarget.z), MovementMath.LockedTurnRateDegPerSec * dt);
            }
            else if (!locked && stick > MovementMath.MoveDeadZone)
            {
                yaw = MovementMath.TurnToward(yaw, MovementMath.YawDeg(wish.x, wish.z), MovementMath.TurnRateDegPerSec * dt);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // ---- vertical ----
            if (HoldVertical) _vy = 0f;   // floor hand-over: no gravity, no jump until the arrival teleport lands
            else if (grounded && _vy < 0f) _vy = MovementMath.GroundedPush(dt);
            bool frozen = _combat != null && _combat.Frozen;   // a downed or out hero cannot jump
            if (_jump.Tick(dt, grounded, intent.JumpPressed && !dodging && !frozen && !HoldVertical))
                _vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, MovementMath.Gravity);
            float dy = HoldVertical ? 0f : MovementMath.VerticalStep(ref _vy, MovementMath.Gravity, MovementMath.TerminalVelocity, dt);

            Vector3 push = _combat != null ? _combat.ConsumeImpulse(dt) : Vector3.zero;   // knockback and guard pushback
            CollisionFlags flags = _cc.Move(new Vector3((_vx + push.x) * dt, dy, (_vz + push.z) * dt));
            if ((flags & CollisionFlags.Above) != 0 && _vy > 0f) _vy = 0f;
            Velocity = (transform.position - pos) / dt;
        }
    }
}
