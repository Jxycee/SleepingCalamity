// Authority: owner writes the stance flags (HeroNetState.NetStance), every peer reads them; nothing else is sent. Derives a HeroAnimState from the
// hero's transform motion each frame and hands it to the IHeroAnimationDriver(s) on the visual. Speed, strafe and vertical speed come from the
// transform's own motion (so a remote copy moves like the owner's); Locked and Dodging come from the owner's stance flags; Grounded is the
// owner's CharacterController on the owner and a short ray on a remote copy.
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    [DefaultExecutionOrder(100)]
    public sealed class HeroAnimationFeeder : MonoBehaviour
    {
        private const float VelocitySmoothing = 18f;   // 1/s; hides the 30 Hz steps of a replicated transform

        private IHeroAnimationDriver[] _drivers = System.Array.Empty<IHeroAnimationDriver>();
        private HeroNetState _net;
        private HeroLocomotion _locomotion;
        private TargetLockController _lockController;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private bool _hasLast;

        public HeroAnimState Current { get; private set; }

        private void Awake()
        {
            _drivers = GetComponentsInChildren<IHeroAnimationDriver>(true);
            _net = GetComponent<HeroNetState>();
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
            bool locked, dodging;
            if (owner)
            {
                locked = _lockController != null && _lockController.IsLocked;
                dodging = _locomotion.IsDodging;
                if (_net != null) _net.OwnerSetStance(locked, dodging);
            }
            else
            {
                locked = _net != null && _net.IsSpawned && _net.StanceIsLocked;
                dodging = _net != null && _net.IsSpawned && _net.StanceIsDodging;
            }

            HeroAnimState state = HeroAnimState.FromWorldVelocity(_velocity.x, _velocity.y, _velocity.z, transform.eulerAngles.y, grounded, locked, dodging);
            Current = state;
            for (int i = 0; i < _drivers.Length; i++)
                if (_drivers[i] != null) _drivers[i].Apply(in state, dt);
        }
    }
}
