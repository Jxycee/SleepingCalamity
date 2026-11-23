using UnityEngine;

namespace AetherStrand
{
    /// <summary>
    /// Global & turbulent wind zone for AetherStrandPhysics hair chains.
    /// </summary>
    public class AetherStrandWindZone : MonoBehaviour
    {
        [Range(0f, 15f)] public float windStrength = 0.55f;
        [Range(0f, 2f)]  public float turbulence = 0.24f;
        [Range(0.2f, 8f)] public float gustFrequency = 2.3f;

        private Vector3 _extraGust;

        private void Update()
        {
            _extraGust = Vector3.Lerp(_extraGust, Vector3.zero, 1f - Mathf.Exp(-4.5f * Time.deltaTime));
        }

        public void TriggerGust(Vector3 worldImpulse) => _extraGust += worldImpulse;

        public Vector3 SampleWindAcceleration(Vector3 worldPos, float strandSeed, float time)
        {
            Vector3 dir = transform.forward;
            float primaryWave = 0.65f + 0.35f * Mathf.Sin(time * gustFrequency + strandSeed);
            float perlin = (Mathf.PerlinNoise(worldPos.x * 1.3f + time * 1.4f, worldPos.z * 1.3f + strandSeed) - 0.5f) * 2f;

            Vector3 lateral = transform.right * (perlin * turbulence * 1.6f);
            Vector3 vertical = Vector3.up * (Mathf.Sin(time * 3.1f + strandSeed) * turbulence * 0.55f);
            return dir * (windStrength * primaryWave) + lateral + vertical + _extraGust;
        }
    }
}
