// Authority: none (local, cosmetic). The Animator-free placeholder for the hero's animation: leans the mannequin into its movement, bobs it
// while it moves, stretches it in the air, crouches and spins it through a dodge. It is only an IHeroAnimationDriver: when the Mixamo clips
// arrive (docs/design/mixamo-clips.md) an Animator-based driver replaces this component and nothing else changes.
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class MannequinAnimDriver : MonoBehaviour, IHeroAnimationDriver
    {
        private const float LeanDeg = 12f, BobAmplitude = 0.035f, SmoothRate = 14f;

        private float _leanForward, _leanSide, _stretch = 1f, _bobPhase, _roll;
        private Vector3 _baseScale = Vector3.one;
        private bool _hasBase;

        public HeroAnimState Last { get; private set; }

        public void Apply(in HeroAnimState state, float deltaTime)
        {
            if (!_hasBase)
            {
                _baseScale = transform.localScale;
                _hasBase = true;
            }
            Last = state;
            float k = 1f - Mathf.Exp(-SmoothRate * deltaTime);

            _leanForward = Mathf.Lerp(_leanForward, Mathf.Clamp(state.StrafeZ, -1f, 1f) * LeanDeg, k);
            _leanSide = Mathf.Lerp(_leanSide, -Mathf.Clamp(state.StrafeX, -1f, 1f) * LeanDeg, k);

            float stretchTarget = 1f;
            if (!state.Grounded) stretchTarget = 1f + Mathf.Clamp(Mathf.Abs(state.VerticalSpeed) * 0.02f, 0f, 0.15f);
            if (state.Dodging) stretchTarget = 0.65f;
            _stretch = Mathf.Lerp(_stretch, stretchTarget, k);

            if (state.Dodging) _roll = (_roll + 720f * deltaTime) % 360f;
            else _roll = 0f;

            float bob = 0f;
            if (state.Grounded && state.Speed > 0.5f)
            {
                _bobPhase += state.Speed * 1.6f * deltaTime * Mathf.PI * 2f * 0.25f;
                bob = Mathf.Abs(Mathf.Sin(_bobPhase)) * BobAmplitude;
            }

            transform.localPosition = new Vector3(0f, bob, 0f);
            transform.localRotation = Quaternion.Euler(_leanForward + _roll, 0f, _leanSide);
            transform.localScale = new Vector3(_baseScale.x / Mathf.Sqrt(_stretch), _baseScale.y * _stretch, _baseScale.z / Mathf.Sqrt(_stretch));
        }
    }
}
