// Demo driver: turns the Head bone left and right about the world up axis so hair that follows the head visibly moves (TobinHairDemo scene).
// Authority: local presentation only, no networking; nothing in the game references it.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class TobinHeadSway : MonoBehaviour
    {
        public Transform head;
        [Tooltip("Largest yaw either side, degrees")] public float maxYaw = 60f;
        [Tooltip("Seconds for one full left-right-left cycle")] public float period = 3.2f;
        [Tooltip("Seconds of the quick part of each turn")] public float turnTime = 0.2f;
        public bool autoSway = true;

        Quaternion baseLocal;
        bool cached;

        public void CacheBase()
        {
            if (head == null) return;
            baseLocal = head.localRotation;
            cached = true;
        }

        // Yaw in degrees about world up, on top of the bone's pose before this component touched it.
        public void Apply(float yawDeg)
        {
            if (head == null) return;
            if (!cached) CacheBase();
            Quaternion parentRot = head.parent != null ? head.parent.rotation : Quaternion.identity;
            head.rotation = Quaternion.AngleAxis(yawDeg, Vector3.up) * (parentRot * baseLocal);
        }

        // Quick turn between the two extremes (smoothstep over turnTime), then hold until the next turn.
        public float YawAt(float t)
        {
            float half = period * 0.5f;
            int leg = Mathf.FloorToInt(t / half);
            float local = t - leg * half;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / Mathf.Max(0.01f, turnTime)));
            float from = (leg % 2 == 0) ? -maxYaw : maxYaw;
            float to = -from;
            return Mathf.Lerp(from, to, k);
        }

        void LateUpdate()
        {
            if (autoSway) Apply(YawAt(Time.time));
        }
    }
}
