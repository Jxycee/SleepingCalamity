// Authority: none (local presentation, every peer runs its own). Slides the sun's cloud-shadow cookie along the wind (B0 lighting corner, Hollowmere design section 4: a 220 m
// tiling cookie moving 1.2 m/s). Does nothing while the light has no cookie, so the presets without cloud shadows (Midday, LowSun) are unaffected. The cookie offset is in tile
// fractions (URP main-light cookie UV), so the speed is metres per second / tile size.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class CloudShadowDrift : MonoBehaviour
    {
        public float MetresPerSecond = 1.2f;
        public Vector2 WindDirection = new Vector2(1f, 0.35f);   // in the light's cookie plane

        private Light _light;
        private UniversalAdditionalLightData _data;

        private void Update()
        {
            if (_light == null) { _light = GetComponent<Light>(); _data = _light.GetUniversalAdditionalLightData(); }
            if (_data == null || _light.cookie == null) return;
            Vector2 size = _data.lightCookieSize;
            if (size.x <= 0f || size.y <= 0f) return;
            Vector2 step = WindDirection.normalized * (MetresPerSecond * Time.deltaTime);
            Vector2 o = _data.lightCookieOffset + new Vector2(step.x / size.x, step.y / size.y);
            _data.lightCookieOffset = new Vector2(Mathf.Repeat(o.x, 1f), Mathf.Repeat(o.y, 1f));
        }
    }
}
