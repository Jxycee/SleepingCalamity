// Authority: none (local, cosmetic; nothing here is networked or touches host state). Turns the CombatEvents this peer sees into camera shake (via the hero
// camera rig) and controller rumble, from the profile table (Core FeedbackProfiles, optionally overridden by Resources/HitFeedbackLibrary). Every peer
// plays feedback for what it sees; only events that concern the LOCAL hero shake or rumble, and world events (kills, slams) are scaled by distance to the
// camera. Settings (shake scale, rumble scale, reduce flashes) are read live from ISettings. Rumble is zeroed on menu open, pause, focus loss, a device
// change and disable. Created on demand by the hero camera rig (EnsureExists), so no scene needs it.
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Feedback;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(70)]
    public sealed class FeedbackDirector : MonoBehaviour
    {
        public const float WorldRadius = 12f;

        public static FeedbackDirector Instance { get; private set; }
        // Shared flash guard: the enemy visuals ask it for brightness and whether a new flash may start.
        public static readonly FlashLimiter Flash = new FlashLimiter();

        private readonly PulseRumble _rumble = new PulseRumble();
        private IInputService _input;
        private ISettings _settings;
        private HitFeedbackLibrary _library;
        private InputDeviceKind _device;
        private float _sentLow, _sentHigh;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("FeedbackDirector");
            go.AddComponent<FeedbackDirector>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Flash.Reduce = false;
        }

        private void OnEnable()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _library = Resources.Load<HitFeedbackLibrary>("HitFeedbackLibrary");
            CombatEvents.LocalHitLanded += OnLocalHit;
            CombatEvents.HeroHurt += OnHeroHurt;
            CombatEvents.Cue += OnCue;
        }

        private void OnDisable()
        {
            CombatEvents.LocalHitLanded -= OnLocalHit;
            CombatEvents.HeroHurt -= OnHeroHurt;
            CombatEvents.Cue -= OnCue;
            if (_settings != null) _settings.Changed -= OnSettings;
            _settings = null;
            StopRumble();
            if (Instance == this) Instance = null;
        }

        private FeedbackProfileData Profile(string id)
        {
            FeedbackProfileData p = _library != null ? _library.Find(id) : null;
            return p ?? FeedbackProfiles.Get(id);
        }

        private float ShakeScale => _settings != null ? _settings.Current.shakeScale : 1f;

        private void Resolve()
        {
            if (_settings == null && ServiceRoots.App != null && ServiceRoots.App.TryGet(out ISettings s))
            {
                _settings = s;
                _settings.Changed += OnSettings;
                OnSettings(_settings.Current);
            }
            if (_input == null && ServiceRoots.App != null) ServiceRoots.App.TryGet(out _input);
        }

        private void OnSettings(SettingsData d)
        {
            if (d == null) return;
            _rumble.Scale = d.rumbleScale;
            Flash.Reduce = d.reduceFlashes;
        }

        // ---- events -------------------------------------------------------------------------------------------------------------------------------

        private void OnLocalHit(LocalHit hit)
        {
            AttackData a = hit.Attack;
            bool heavy = a.IsFinisher || a.Kind == AttackKind.Charge;
            HitWeight w = HitWeights.Classify(heavy, a.Kind == AttackKind.Shot, a.HitstopFrames);
            Play(Profile(FeedbackProfiles.IdFor(w)), 1f);
        }

        private void OnHeroHurt(HeroCombat hero, bool guarded)
        {
            if (hero == null || !hero.IsOwner) return;
            Play(Profile(guarded ? FeedbackProfiles.HeroHurtGuarded : FeedbackProfiles.HeroHurt), 1f);
        }

        private void OnCue(CombatCue cue, Vector3 at)
        {
            string id = cue == CombatCue.EnemyDie ? FeedbackProfiles.Kill : cue == CombatCue.EnemySlam ? FeedbackProfiles.EnemySlam : null;
            if (id == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float k = 1f - Vector3.Distance(cam.transform.position, at) / WorldRadius;
            if (k <= 0f) return;
            Play(Profile(id), k);
        }

        private void Play(FeedbackProfileData p, float k)
        {
            if (p == null) return;
            Resolve();
            HeroCameraRig rig = HeroCameraRig.Local;
            if (rig != null && p.Trauma > 0f) rig.AddTrauma(p.Trauma * k, ShakeScale);
            if (p.RumbleMs > 0 && RumbleAllowed()) _rumble.Add(p.RumbleLow * k, p.RumbleHigh * k, p.RumbleMs / 1000f);
        }

        // ---- rumble output ------------------------------------------------------------------------------------------------------------------------

        // Gamepad in use, window focused, no menu open, game not paused.
        private bool RumbleAllowed()
        {
            return _input != null && _input.HasFocus && !_input.MenuOpen && Time.timeScale > 0f && _input.LastDevice == InputDeviceKind.Gamepad;
        }

        private void Update()
        {
            Resolve();
            if (_input == null) return;
            if (_input.LastDevice != _device)
            {
                _device = _input.LastDevice;
                StopRumble();   // a device change: never carry a pulse onto the new device
            }
            if (!RumbleAllowed()) { StopRumble(); return; }
            _rumble.Tick(Time.unscaledDeltaTime);
            Send(_rumble.Low, _rumble.High);
        }

        private void StopRumble()
        {
            _rumble.Stop();
            Send(0f, 0f);
        }

        private void Send(float low, float high)
        {
            if (_input == null) return;
            if (Mathf.Abs(low - _sentLow) < 0.005f && Mathf.Abs(high - _sentHigh) < 0.005f && !(low == 0f && high == 0f && (_sentLow != 0f || _sentHigh != 0f))) return;
            _sentLow = low;
            _sentHigh = high;
            _input.SetRumble(low, high);
        }
    }
}
