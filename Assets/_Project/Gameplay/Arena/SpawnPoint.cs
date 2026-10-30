// Not networked. A marker the host reads when it places players in a gameplay scene (App's ArenaPlayerSpawner). Players are spread on a
// small circle around the point by roster slot, so four players never share a spot.
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class SpawnPoint : MonoBehaviour
    {
        public const float SlotRadius = 1.5f;

        // The world position for the player in `slot` (0..3): slot 0 on the point itself, the rest on the circle.
        public Vector3 PositionForSlot(int slot)
        {
            if (slot <= 0) return transform.position;
            Quaternion around = Quaternion.Euler(0f, (slot - 1) * 120f, 0f);
            return transform.position + around * (transform.forward * SlotRadius);
        }
    }
}
