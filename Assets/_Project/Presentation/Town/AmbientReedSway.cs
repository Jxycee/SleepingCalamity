// Authority: none (local presentation, every peer animates its own copy).
// Reed sway: the lake and pool reed clumps are plain static meshes, so each one leans a degree or two about its base, in a wave that travels with the wind (the phase follows
// the clump's position along HollowmereAmbientLife.WindDirection), plus a slow second beat so a gust reads as a gust. No allocations: the base rotations are cached once.
// HollowmereAmbientBuilder fills Reeds with every clump of the lake art. Tuning: HollowmereAmbientLife.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class AmbientReedSway : MonoBehaviour
    {
        public Transform[] Reeds = new Transform[0];

        Quaternion[] _base; float[] _phase;
        Vector3 _axisA, _axisB;   // lean axes in world space: along the wind and across it

        void Init()
        {
            _base = new Quaternion[Reeds.Length]; _phase = new float[Reeds.Length];
            Vector2 w = HollowmereAmbientLife.WindDirection;
            _axisA = new Vector3(-w.y, 0f, w.x);   // a lean towards the wind rotates about the horizontal axis across it
            _axisB = new Vector3(w.x, 0f, w.y);
            for (int i = 0; i < Reeds.Length; i++)
            {
                if (Reeds[i] == null) continue;
                _base[i] = Reeds[i].rotation;
                Vector3 p = Reeds[i].position;
                _phase[i] = (p.x * w.x + p.z * w.y) * 0.18f;
            }
        }

        void Update() => Pose(Time.time);

        public void Pose(float t)
        {
            if (_base == null || _base.Length != Reeds.Length) Init();
            float amp = HollowmereAmbientLife.ReedLeanDegrees;
            for (int i = 0; i < Reeds.Length; i++)
            {
                Transform r = Reeds[i]; if (r == null) continue;
                float lean = (Mathf.Sin(t * 1.3f - _phase[i]) * 0.6f + Mathf.Sin(t * 0.45f - _phase[i] * 0.5f) * 0.4f + 0.35f) * amp;   // mostly downwind
                float side = Mathf.Sin(t * 0.9f - _phase[i] + 1.7f) * amp * 0.3f;
                r.rotation = Quaternion.AngleAxis(lean, _axisA) * Quaternion.AngleAxis(side, _axisB) * _base[i];
            }
        }
    }
}
