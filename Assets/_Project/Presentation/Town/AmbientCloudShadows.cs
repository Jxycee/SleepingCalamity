// Authority: none (local presentation, every peer animates its own copy).
// Drifting cloud shadows and gust bands across the valley: the town sun carries a tiling light cookie (clouds plus fine bands, built by HollowmereAmbientBuilder), and this
// component slides the sun along the cookie plane with the wind. A directional light's cookie is anchored to its transform, so moving the light's position scrolls the
// pattern while the light direction (and so the shadows) stays put. Sits on the Sun object. Tuning: HollowmereAmbientLife.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class AmbientCloudShadows : MonoBehaviour
    {
        public float Speed = HollowmereAmbientLife.WindSpeed;
        public Vector2 Direction = HollowmereAmbientLife.WindDirection;

        Vector3 _home; bool _have;

        void OnEnable() { _home = transform.position; _have = true; }

        void OnDisable() { if (_have) transform.position = _home; }

        void Update()
        {
            Transform t = transform;
            t.position = _home + (t.right * Direction.x + t.up * Direction.y) * (Speed * Time.time);
        }
    }
}
