// Authority: none (local, cosmetic, every peer). A short flinch when a hero is hurt: the body kicks back and dips for a quarter of a second. Added to the
// hero at runtime by CombatFx on the first hurt (no prefab edit), and applied in LateUpdate after the animation driver and HeroWeaponVisual have written
// the visual root for the frame, as an offset on top of whatever they set. Whoever owns hero animation next: this only touches the "Visual" child's local
// rotation and position for 0.25 s after a hit; delete this file to drop it.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(120)]
    public sealed class HeroFlinch : MonoBehaviour
    {
        public const float Seconds = 0.25f, KickDegrees = 18f, DipMetres = 0.08f;

        private Transform _visual;
        private float _age = Seconds;

        public void Trigger() { _age = 0f; }

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
