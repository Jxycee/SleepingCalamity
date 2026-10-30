// Authority: none (local, cosmetic, every peer). Drives the hero's Animator from the HeroAnimState the feeder derives each frame: locomotion and strafing,
// jump, dodge, hurt, downed and the attack layer all come from parameters (HeroAnimMap does the mapping, HeroAnimatorBuilder wired the controller to the
// same names). Attack and dodge clips play by progress (Motion Time), so a remote copy shows the owner's swing from the replicated swing clock without any
// extra traffic. MannequinAnimDriver stands down while this one has a working Animator.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class AnimatorHeroDriver : MonoBehaviour, IHeroAnimationDriver
    {
        private const float MoveDamp = 0.08f;   // seconds; smooths the speed and strafe parameters

        private static readonly int HSpeed = Animator.StringToHash(HeroAnimParam.Speed);
        private static readonly int HMoveX = Animator.StringToHash(HeroAnimParam.MoveX);
        private static readonly int HMoveY = Animator.StringToHash(HeroAnimParam.MoveY);
        private static readonly int HVertSpeed = Animator.StringToHash(HeroAnimParam.VertSpeed);
        private static readonly int HGrounded = Animator.StringToHash(HeroAnimParam.Grounded);
        private static readonly int HLocked = Animator.StringToHash(HeroAnimParam.Locked);
        private static readonly int HDodging = Animator.StringToHash(HeroAnimParam.Dodging);
        private static readonly int HGuarding = Animator.StringToHash(HeroAnimParam.Guarding);
        private static readonly int HAiming = Animator.StringToHash(HeroAnimParam.Aiming);
        private static readonly int HDowned = Animator.StringToHash(HeroAnimParam.Downed);
        private static readonly int HWeapon = Animator.StringToHash(HeroAnimParam.Weapon);
        private static readonly int HAttack = Animator.StringToHash(HeroAnimParam.Attack);
        private static readonly int HAttackTime = Animator.StringToHash(HeroAnimParam.AttackTime);
        private static readonly int HDodgeTime = Animator.StringToHash(HeroAnimParam.DodgeTime);
        private static readonly int HHurt = Animator.StringToHash(HeroAnimParam.Hurt);
        private static readonly int HHurtHeavy = Animator.StringToHash(HeroAnimParam.HurtHeavy);

        [SerializeField] private Animator animator;

        private readonly HashSet<int> _params = new HashSet<int>();
        private int _lastHurt;
        private bool _hasHurt;
        private float _dodgeTime;
        private bool _wasDowned;

        public HeroAnimParams LastParams { get; private set; }
        public Animator Animator => animator;

        // True while there is an Animator with a controller to drive: the mannequin placeholder stays out of the way only then.
        public bool IsWorking => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null && animator.isHuman;

        public void Configure(Animator target) { animator = target; }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            animator.applyRootMotion = false;                              // the CharacterController moves the hero, never the clips
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;      // a headless run draws nothing, and a culled Animator would not evolve its states
            CacheParams();
        }

        private void CacheParams()
        {
            _params.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter p in animator.parameters) _params.Add(p.nameHash);
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 1f);
        }

        public void Apply(in HeroAnimState state, float deltaTime)
        {
            if (!IsWorking) return;
            if (_params.Count == 0) CacheParams();
            HeroAnimParams p = HeroAnimMap.Evaluate(in state);
            LastParams = p;

            Float(HSpeed, p.Speed, MoveDamp, deltaTime);
            Float(HMoveX, p.MoveX, MoveDamp, deltaTime);
            Float(HMoveY, p.MoveY, MoveDamp, deltaTime);
            Float(HVertSpeed, p.VertSpeed);
            Bool(HGrounded, p.Grounded);
            Bool(HLocked, p.Locked);
            Bool(HDodging, p.Dodging);
            Bool(HGuarding, p.Guarding);
            Bool(HAiming, p.Aiming);
            Bool(HDowned, p.Downed);
            Int(HWeapon, p.Weapon);
            Int(HAttack, p.Attack);
            Float(HAttackTime, p.AttackTime);

            _dodgeTime = p.Dodging ? Mathf.Min(1f, _dodgeTime + deltaTime / MovementMath.DodgeDuration) : 0f;
            Float(HDodgeTime, _dodgeTime);

            // A flinch fires once per new hurt, never at spawn (the first frame only records the counter) and never while lying down.
            if (!_hasHurt) { _lastHurt = state.HurtSerial; _hasHurt = true; }
            if (state.HurtSerial != _lastHurt)
            {
                _lastHurt = state.HurtSerial;
                if (!p.Downed) Trigger(state.HurtHeavy ? HHurtHeavy : HHurt);
            }
            if (p.Downed && !_wasDowned)
            {
                ResetTrigger(HHurt);
                ResetTrigger(HHurtHeavy);
            }
            _wasDowned = p.Downed;
        }

        private void Float(int h, float value) { if (_params.Contains(h)) animator.SetFloat(h, value); }
        private void Float(int h, float value, float damp, float dt) { if (_params.Contains(h)) animator.SetFloat(h, value, damp, dt); }
        private void Bool(int h, bool value) { if (_params.Contains(h)) animator.SetBool(h, value); }
        private void Int(int h, int value) { if (_params.Contains(h)) animator.SetInteger(h, value); }
        private void Trigger(int h) { if (_params.Contains(h)) animator.SetTrigger(h); }
        private void ResetTrigger(int h) { if (_params.Contains(h)) animator.ResetTrigger(h); }
    }
}
