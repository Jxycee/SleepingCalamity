// Authority: none (local presentation, every peer flickers its own copy; nothing is networked or saved).
// Flicker for one fire light (the bell yard brazier): the intensity wanders around its authored value with two noise octaves (a slow swell and a fast lick), so the warm pool of light
// breathes with the flame. One Update with two Mathf.PerlinNoise calls; the authored intensity is kept in Base, so an edit-mode capture (no Update) shows the authored value.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [RequireComponent(typeof(Light))]
    public sealed class AmbientFlicker : MonoBehaviour
    {
        public float Base = 16f;        // the authored intensity
        public float Amount = 0.22f;    // +/- fraction of Base
        public float Speed = 1f;        // 1 = a lick about every 0.15 s
        public float Seed = 3f;

        Light _light;

        void OnEnable() { _light = GetComponent<Light>(); }

        void Update()
        {
            if (_light == null) return;
            float t = Time.time * Speed;
            float n = (Mathf.PerlinNoise(t * 1.3f + Seed, 0.5f) - 0.5f) * 1.2f + (Mathf.PerlinNoise(t * 7.1f + Seed * 2f, 3.7f) - 0.5f) * 0.8f;
            _light.intensity = Base * (1f + Amount * n * 2f);
        }
    }
}
