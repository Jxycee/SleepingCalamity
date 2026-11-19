// Authority: none (local presentation, every peer animates its own copy; nothing is networked or saved).
// T24: washing-line cloths and rags (children named Cloth_* / Rag_*, hanging -Y from the rope with the rope along local +X) swing in the town's wind: a gust (two sines plus Perlin
// noise, the same WindDirection as the chimney smoke) pitches each cloth about its rope axis, with a small flutter twist. Done with transforms, not a vertex shader, so no shared material
// or shader changes (no .mat churn). Costs one rotation per cloth per frame, skipped beyond CullDistance of the camera. The builder fills Cloths and Rest at build time.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class AmbientClothSway : MonoBehaviour
    {
        public Transform[] Cloths = new Transform[0];
        public Quaternion[] Rest = new Quaternion[0];
        public float CullDistance = 70f;

        /// <summary>Gust strength 0..1 at time t for a position: slow swell, mid sway and noise, travelling along the wind.</summary>
        public static float Gust(float t, Vector3 p)
        {
            Vector2 w = HollowmereAmbientLife.WindDirection;
            float along = p.x * w.x + p.z * w.y;
            float g = 0.5f + 0.5f * Mathf.Sin(t * 0.55f - along * 0.08f);
            g = g * g;
            float n = Mathf.PerlinNoise(t * 0.8f + along * 0.05f, 11.3f);
            return Mathf.Clamp01(0.25f + 0.55f * g + 0.4f * (n - 0.5f));
        }

        void Update() => Pose(Time.time, Camera.main);

        public void Pose(float t, Camera cam)
        {
            Vector3 cp = cam != null ? cam.transform.position : default;
            float cull = CullDistance * CullDistance;
            Vector2 w = HollowmereAmbientLife.WindDirection;
            for (int i = 0; i < Cloths.Length; i++)
            {
                Transform c = Cloths[i]; if (c == null) continue;
                if (cam != null && (cp - c.position).sqrMagnitude > cull) continue;
                float ph = i * 1.7f;
                float g = Gust(t + ph * 0.05f, c.position);
                // wind blows the cloth about its rope axis: the side it leans to is the side of the cloth facing away from the wind
                Vector3 fwd = c.parent != null ? c.parent.rotation * (Rest[i] * Vector3.forward) : Rest[i] * Vector3.forward;
                float side = Mathf.Sign(fwd.x * w.x + fwd.z * w.y + 1e-4f), face = 0.45f + 0.55f * Mathf.Abs(fwd.x * w.x + fwd.z * w.y);
                float flutter = Mathf.Sin(t * (3.1f + 0.4f * (i % 5)) + ph) * 4f * g;
                float swing = side * (4f + 26f * g) * face + flutter;
                float twist = Mathf.Sin(t * 1.3f + ph) * 7f * g;
                c.localRotation = Rest[i] * Quaternion.Euler(swing, twist, 0f);
            }
        }
    }
}
