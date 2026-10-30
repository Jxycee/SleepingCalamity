// Authority: none (every peer, local only; nothing is sent). Derives a HeroAnimState from the hero's transform motion each frame and hands it
// to the IHeroAnimationDriver(s) on the visual. Using the transform's own motion means a remote copy animates exactly like the owner's with no
// extra sync; the owner's grounded / locked / dodging flags refine it when HeroLocomotion is running.
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    [DefaultExecutionOrder(100)]
    public sealed class HeroAnimationFeeder : MonoBehaviour
    {
        private const float VelocitySmoothing = 18f;   // 1/s; hides the 30 Hz steps of a replicated transform

        private IHeroAnimationDriver[] _drivers = System.Array.Empty<IHeroAnimationDriver>();
        private HeroLocomotion _locomotion;
        private TargetLockController _lockController;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private bool _hasLast;

        public HeroAnimState Current { get; private set; }

        private void Awake()
        {
            _drivers = GetComponentsInChildren<IHeroAnimationDriver>(true);
            _locomotion = GetComponent<HeroLocomotion>();
            _lockController = GetComponent<TargetLockController>();
        }

        private void OnEnable()
        {
            _hasLast = false;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pos = transform.position;
            if (!_hasLast)
            {
                _lastPosition = pos;
                _hasLast = true;
            }
            Vector3 raw = (pos - _lastPosition) / dt;
            _lastPosition = pos;
            _velocity = Vector3.Lerp(_velocity, raw, 1f - Mathf.Exp(-VelocitySmoothing * dt));

            bool owner = _locomotion != null && _locomotion.enabled;
            bool grounded = owner ? _locomotion.IsGrounded : Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, 0.35f,
                Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Structure), QueryTriggerInteraction.Ignore);
            bool locked = owner && _lockController != null && _lockController.IsLocked;
            bool dodging = owner && _locomotion.IsDodging;

            HeroAnimState state = HeroAnimState.FromWorldVelocity(_velocity.x, _velocity.y, _velocity.z, transform.eulerAngles.y, grounded, locked, dodging);
            Current = state;
            for (int i = 0; i < _drivers.Length; i++)
                if (_drivers[i] != null) _drivers[i].Apply(in state, dt);
        }
    }
}
