// Authority: none (local to the owner; one rig per local player). The hero's Cinemachine 3 rig, instantiated by HeroCameraRigSpawner for the
// owning peer only. Two CinemachineCameras share the player's camera pivot (1.5 m up): FreeCam (orbital follow, boom 5.0 m) and LockCam (orbital
// follow, boom 6.5 m, looking at a target group of the player and the locked target). The Brain on the arena camera blends between them by
// priority. Both have a Deoccluder against Ground and Structure. The rig never reads input: the view yaw and pitch come from HeroLocomotion
// (which already got them from the routed intent), so the camera, the movement direction and the lock all agree.
// Settings: docs/PHASE1.md 6.5 (free boom 5.0, lock boom 6.5, pivot 1.5, FOV 50).
using System.Collections.Generic;
using SleepingCalamity.Gameplay;
using Unity.Cinemachine;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(60)]
    public sealed class HeroCameraRig : MonoBehaviour
    {
        public const float FreeBoom = 5.0f, LockBoom = 6.5f, Fov = 50f;
        public const int FreePriority = 10, LockPriority = 20;

        // The rig of the first local owner in this process (a second owner, only in a one-process test, gets none).
        public static HeroCameraRig Local { get; private set; }

        [SerializeField] private CinemachineCamera freeCam;
        [SerializeField] private CinemachineCamera lockCam;
        [SerializeField] private CinemachineTargetGroup lockGroup;
        [SerializeField] private Transform lockIndicator;

        private CinemachineOrbitalFollow _freeOrbit, _lockOrbit;
        private HeroNetState _hero;
        private HeroLocomotion _locomotion;
        private TargetLockController _lockController;
        private Transform _pivot;
        private Renderer[] _heroRenderers = System.Array.Empty<Renderer>();
        private bool _heroHidden;

        public CinemachineCamera FreeCam => freeCam;
        public CinemachineCamera LockCam => lockCam;
        public CinemachineTargetGroup LockGroup => lockGroup;
        public Transform LockIndicator => lockIndicator;
        public bool LockCamLive => lockCam != null && lockCam.Priority.Value > freeCam.Priority.Value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
        }

        public void Configure(CinemachineCamera free, CinemachineCamera locked, CinemachineTargetGroup group, Transform indicator)
        {
            freeCam = free;
            lockCam = locked;
            lockGroup = group;
            lockIndicator = indicator;
        }

        public void Bind(HeroNetState hero)
        {
            Unbind();
            _hero = hero;
            _locomotion = hero.Locomotion;
            _lockController = hero.LockController;
            _pivot = hero.CameraPivot;
            _heroRenderers = hero.GetComponentsInChildren<Renderer>(true);
            _heroHidden = false;
            _freeOrbit = freeCam.GetComponent<CinemachineOrbitalFollow>();
            _lockOrbit = lockCam.GetComponent<CinemachineOrbitalFollow>();

            freeCam.Follow = _pivot;
            lockCam.Follow = _pivot;
            lockCam.LookAt = lockGroup.transform;
            freeCam.Priority.Value = FreePriority;
            lockCam.Priority.Value = 0;
            if (_locomotion != null) ApplyAxes();

            if (_lockController != null) _lockController.LockChanged += OnLockChanged;
            OnLockChanged(_lockController != null ? _lockController.Locked : null);
            if (Local == null) Local = this;
        }

        private void Unbind()
        {
            SetHeroVisible(true);
            if (_lockController != null) _lockController.LockChanged -= OnLockChanged;
            _hero = null;
            _locomotion = null;
            _lockController = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (Local == this) Local = null;
        }

        private void Update()
        {
            if (_hero == null || _locomotion == null) return;
            ApplyAxes();
            UpdateIndicator();
        }

        // When a wall squeezes the boom until the camera is nearly inside the hero, the hero's own body would fill the screen (or show its inside),
        // so it is hidden locally until the camera backs off again. Hysteresis: hidden under 1.0 m, shown again over 1.4 m. Cosmetic, owner only.
        // M3: a camera kick on impacts (a quick FOV punch that eases back), owner side only. 0..1; stacked kicks keep the larger.
        private float _kick;
        private bool _kickApplied;
        private const float KickFovDegrees = 4.5f, KickDecayPerSecond = 4.5f;

        public void Kick(float strength) { _kick = Mathf.Max(_kick, Mathf.Clamp01(strength)); }

        private void OnEnable() { CombatEvents.CameraKick += OnKick; }
        private void OnDisable() { CombatEvents.CameraKick -= OnKick; }
        private void OnKick(float strength) { if (Local == this) Kick(strength); }

        private void ApplyKick()
        {
            if (_kick <= 0.001f)
            {
                if (_kickApplied) SetFov(Fov);
                _kick = 0f;
                _kickApplied = false;
                return;
            }
            _kick = Mathf.MoveTowards(_kick, 0f, Time.unscaledDeltaTime * KickDecayPerSecond);
            SetFov(Fov - _kick * KickFovDegrees);
            _kickApplied = true;
        }

        private void SetFov(float fov)
        {
            if (freeCam != null) { LensSettings l = freeCam.Lens; l.FieldOfView = fov; freeCam.Lens = l; }
            if (lockCam != null) { LensSettings l = lockCam.Lens; l.FieldOfView = fov; lockCam.Lens = l; }
        }

        private void LateUpdate()
        {
            ApplyKick();
            if (_hero == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float distance = Vector3.Distance(cam.transform.position, _pivot.position);
            if (!_heroHidden && distance < 1.0f) SetHeroVisible(false);
            else if (_heroHidden && distance > 1.4f) SetHeroVisible(true);
        }

        private void SetHeroVisible(bool visible)
        {
            _heroHidden = !visible;
            foreach (Renderer r in _heroRenderers)
                if (r != null) r.enabled = visible;
        }

        private void ApplyAxes()
        {
            float yaw = _locomotion.ViewYawDeg, pitch = _locomotion.ViewPitchDownDeg;
            if (_freeOrbit != null)
            {
                _freeOrbit.HorizontalAxis.Value = yaw;
                _freeOrbit.VerticalAxis.Value = pitch;
            }
            if (_lockOrbit != null)
            {
                _lockOrbit.HorizontalAxis.Value = yaw;
                _lockOrbit.VerticalAxis.Value = pitch;
            }
        }

        private void OnLockChanged(Targetable target)
        {
            bool locked = target != null;
            lockCam.Priority.Value = locked ? LockPriority : 0;
            var members = new List<CinemachineTargetGroup.Target>
            {
                new CinemachineTargetGroup.Target { Object = _pivot, Weight = 1f, Radius = 0.6f },
            };
            if (locked) members.Add(new CinemachineTargetGroup.Target { Object = target.AimTransform, Weight = 1f, Radius = 0.6f });
            lockGroup.Targets = members;
            if (lockIndicator != null) lockIndicator.gameObject.SetActive(locked);
        }

        // The debug lock indicator: a small marker bobbing over the locked target (no UI beyond this in M2).
        private void UpdateIndicator()
        {
            if (lockIndicator == null) return;
            Targetable t = _lockController != null ? _lockController.Locked : null;
            if (t == null) return;
            lockIndicator.position = t.AimPoint + Vector3.up * (1.0f + 0.08f * Mathf.Sin(Time.time * 5f));
            lockIndicator.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        }
    }
}
