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

        public CinemachineCamera FreeCam => freeCam;
        public CinemachineCamera LockCam => lockCam;
        public CinemachineTargetGroup LockGroup => lockGroup;
        public bool LockCamLive => lockCam != null && lockCam.Priority.Value > freeCam.Priority.Value;

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
            _freeOrbit = freeCam.GetComponent<CinemachineOrbitalFollow>();
            _lockOrbit = lockCam.GetComponent<CinemachineOrbitalFollow>();

            freeCam.Follow = _pivot;
            lockCam.Follow = _pivot;
            lockCam.LookAt = lockGroup.transform;
            freeCam.Priority.Value = FreePriority;
            lockCam.Priority.Value = 0;
            if (_locomotion != null) ApplyAxes();
            // Start already behind the hero (no swoop on spawn).
            freeCam.OnTargetObjectWarped(_pivot, Vector3.zero);
            lockCam.OnTargetObjectWarped(_pivot, Vector3.zero);

            if (_lockController != null) _lockController.LockChanged += OnLockChanged;
            OnLockChanged(_lockController != null ? _lockController.Locked : null);
            if (Local == null) Local = this;
        }

        private void Unbind()
        {
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
