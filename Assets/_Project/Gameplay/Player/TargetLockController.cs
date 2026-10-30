// Authority: owner only, and local to the owner. PlayerNetState-style: HeroNetState enables this component on the owning peer and nowhere else.
// Nothing here is networked: which target a player has soft- or hard-locked is that player's own state (docs/PHASE1.md 6.4; the attacker's client
// will send the host a hit intent in M3). Runs the Core TargetLock / SoftTargeting on candidates built from the live Targetable registry, with
// line of sight from a raycast against Ground and Structure. The only writer of IInputService.GameplayContext (Free <-> LockOn).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    [DefaultExecutionOrder(-50)]
    public sealed class TargetLockController : MonoBehaviour
    {
        private readonly TargetLock _lock = new TargetLock();
        private readonly FlickDetector _flick = new FlickDetector();
        private readonly List<TargetCandidate> _candidates = new List<TargetCandidate>();
        private readonly List<Targetable> _targets = new List<Targetable>();

        private HeroLocomotion _hero;
        private IInputService _input;
        private bool _wroteContext;

        // Injected by tests (one input per hero in one process); falls back to the App's input service.
        public IInputService Input
        {
            get => _input ?? HeroInput.ResolveDefault();
            set => _input = value;
        }

        public Targetable Locked { get; private set; }     // the hard-locked target, or null
        public Targetable Soft { get; private set; }       // the soft target (the locked one while locked), or null
        public bool IsLocked => Locked != null;
        public LockDrop LastDrop => _lock.LastDrop;
        public event Action<Targetable> LockChanged;       // new target, or null when the lock ended

        private void Awake()
        {
            _hero = GetComponent<HeroLocomotion>();
        }

        private void OnDisable()
        {
            if (_lock.IsLocked) _lock.Release();
            SetLocked(null);
            Soft = null;
            _flick.Reset();
            ReleaseContext();
        }

        private void Update()
        {
            IInputService input = Input;
            if (input == null || HeroInput.IsPaused()) return;
            PlayerIntent intent = input.Intent;

            BuildCandidates();
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;
            var seeker = new TargetSeeker(pos.x, pos.y, pos.z, fwd.x, fwd.z);

            if (intent.LockPressed)
            {
                if (_lock.IsLocked)
                {
                    _lock.Release();
                    SetLocked(null);
                }
                else if (_lock.TryAcquire(in seeker, _candidates))
                {
                    _flick.Reset();
                    SetLocked(TargetById(_lock.TargetId));
                }
            }

            if (_lock.IsLocked)
            {
                if (_lock.Tick(Time.deltaTime, in seeker, _candidates) != LockDrop.None)
                {
                    SetLocked(null);
                }
                else if (_flick.Update(intent.SwitchX, intent.SwitchY, out float fx, out float fy))
                {
                    float yaw = _hero != null ? _hero.ViewYawDeg : transform.eulerAngles.y;
                    FlickInput.ToWorld(yaw, fx, fy, out float wx, out float wz);
                    if (_lock.TrySwitch(in seeker, wx, wz, _candidates)) SetLocked(TargetById(_lock.TargetId));
                }
            }

            if (_lock.IsLocked) Soft = Locked;
            else
            {
                int soft = SoftTargeting.PickSoft(in seeker, _candidates);
                Soft = soft >= 0 ? _targets[soft] : null;
            }

            WriteContext(input, IsLocked ? InputContext.LockOn : InputContext.Free);
        }

        private void BuildCandidates()
        {
            _candidates.Clear();
            _targets.Clear();
            Vector3 eye = transform.position + Vector3.up * MovementMath.CameraPivotHeight;
            int mask = Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Structure);
            IReadOnlyList<Targetable> all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Targetable t = all[i];
                if (t == null || !t.isActiveAndEnabled) continue;
                Vector3 p = t.transform.position;
                bool sight = !Physics.Linecast(eye, t.AimPoint, mask, QueryTriggerInteraction.Ignore);
                _candidates.Add(new TargetCandidate(t.Id, p.x, p.y, p.z, t.Alive, t.IsElite, sight));
                _targets.Add(t);
            }
        }

        private Targetable TargetById(int id)
        {
            for (int i = 0; i < _targets.Count; i++)
                if (_candidates[i].Id == id) return _targets[i];
            return null;
        }

        private void SetLocked(Targetable target)
        {
            if (Locked == target) return;
            Locked = target;
            LockChanged?.Invoke(target);
        }

        private void WriteContext(IInputService input, InputContext context)
        {
            if (input.GameplayContext != context) input.GameplayContext = context;
            _wroteContext = true;
        }

        private void ReleaseContext()
        {
            if (!_wroteContext) return;
            _wroteContext = false;
            IInputService input = Input;
            if (input == null || (input is UnityEngine.Object o && o == null)) return;
            if (input.GameplayContext != InputContext.Free) input.GameplayContext = InputContext.Free;
        }
    }
}
