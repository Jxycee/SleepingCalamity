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

            Float(HeroAnimParam.Speed, p.Speed, MoveDamp, deltaTime);
            Float(HeroAnimParam.MoveX, p.MoveX, MoveDamp, deltaTime);
            Float(HeroAnimParam.MoveY, p.MoveY, MoveDamp, deltaTime);
            Float(HeroAnimParam.VertSpeed, p.VertSpeed);
            Bool(HeroAnimParam.Grounded, p.Grounded);
            Bool(HeroAnimParam.Locked, p.Locked);
            Bool(HeroAnimParam.Dodging, p.Dodging);
            Bool(HeroAnimParam.Guarding, p.Guarding);
            Bool(HeroAnimParam.Aiming, p.Aiming);
            Bool(HeroAnimParam.Downed, p.Downed);
            Int(HeroAnimParam.Weapon, p.Weapon);
            Int(HeroAnimParam.Attack, p.Attack);
            Float(HeroAnimParam.AttackTime, p.AttackTime);

            _dodgeTime = p.Dodging ? Mathf.Min(1f, _dodgeTime + deltaTime / MovementMath.DodgeDuration) : 0f;
            Float(HeroAnimParam.DodgeTime, _dodgeTime);

            // A flinch fires once per new hurt, never at spawn (the first frame only records the counter) and never while lying down.
            if (!_hasHurt) { _lastHurt = state.HurtSerial; _hasHurt = true; }
            if (state.HurtSerial != _lastHurt)
            {
                _lastHurt = state.HurtSerial;
                if (!p.Downed) Trigger(state.HurtHeavy ? HeroAnimParam.HurtHeavy : HeroAnimParam.Hurt);
            }
            if (p.Downed && !_wasDowned)
            {
                ResetTrigger(HeroAnimParam.Hurt);
                ResetTrigger(HeroAnimParam.HurtHeavy);
            }
            _wasDowned = p.Downed;
        }

        private void Float(string name, float value) { int h = Animator.StringToHash(name); if (_params.Contains(h)) animator.SetFloat(h, value); }
        private void Float(string name, float value, float damp, float dt) { int h = Animator.StringToHash(name); if (_params.Contains(h)) animator.SetFloat(h, value, damp, dt); }
        private void Bool(string name, bool value) { int h = Animator.StringToHash(name); if (_params.Contains(h)) animator.SetBool(h, value); }
        private void Int(string name, int value) { int h = Animator.StringToHash(name); if (_params.Contains(h)) animator.SetInteger(h, value); }
        private void Trigger(string name) { int h = Animator.StringToHash(name); if (_params.Contains(h)) animator.SetTrigger(h); }
        private void ResetTrigger(string name) { int h = Animator.StringToHash(name); if (_params.Contains(h)) animator.ResetTrigger(h); }
    }
}
