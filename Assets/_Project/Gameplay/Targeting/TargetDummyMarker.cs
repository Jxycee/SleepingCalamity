// Not networked. Marks where TargetDummySpawner places a dummy, and whether it is the elite.
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class TargetDummyMarker : MonoBehaviour
    {
        [SerializeField] private bool elite;

        public bool Elite => elite;

        public void Configure(bool isElite)
        {
            elite = isElite;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = elite ? new Color(1f, 0.4f, 0.2f) : new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        }
    }
}
