// Authority: none (local, cosmetic, every peer). A short flinch when a hero is hurt: the body kicks back and dips for a quarter of a second. Added to the
// hero at runtime by CombatFx on the first hurt (no prefab edit), and applied in LateUpdate after the animation driver and HeroWeaponVisual have written
// the visual root for the frame, as an offset on top of whatever they set. It is the MANNEQUIN FALLBACK only: when the hero has a working AnimatorHeroDriver,
// the Animator plays its own Hurt / HurtHeavy flinch (HeroCombat.HurtSerial) and this does nothing, so the two never fight.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(120)]
    public sealed class HeroFlinch : MonoBehaviour
    {
        public const float Seconds = 0.25f, KickDegrees = 18f, DipMetres = 0.08f;

        private Transform _visual;
        private float _age = Seconds;

        private AnimatorHeroDriver _animator;

        // True when the Animator hero is in charge of the flinch.
        private bool AnimatorOwnsFlinch
        {
            get
            {
                if (_animator == null) _animator = GetComponentInChildren<AnimatorHeroDriver>(true);
                return _animator != null && _animator.IsWorking;
            }
        }

        public void Trigger() { if (!AnimatorOwnsFlinch) _age = 0f; }

        private void LateUpdate()
        {
            if (_age >= Seconds) return;
            _age += Time.deltaTime;
            if (_visual == null) _visual = transform.Find("Visual");
            if (_visual == null) return;
            float k = Mathf.Sin(Mathf.Clamp01(_age / Seconds) * Mathf.PI);   // out and back
            _visual.localRotation = _visual.localRotation * Quaternion.Euler(-KickDegrees * k, 0f, 0f);
            _visual.localPosition += new Vector3(0f, -DipMetres * k, -DipMetres * k);
        }
    }
}
