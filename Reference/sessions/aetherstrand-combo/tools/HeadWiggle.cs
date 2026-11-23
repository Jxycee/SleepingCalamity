using UnityEngine;

// Test-only: turns and nods the Head bone so the web hair solver has something to react to.
public class HeadWiggle : MonoBehaviour
{
    public Transform head;
    public float yawDeg = 35f, pitchDeg = 8f, hz = 0.45f;
    Quaternion rest;
    float t;
    void Start() { if (head != null) rest = head.localRotation; }
    void LateUpdate()
    {
        if (head == null) return;
        t += Time.deltaTime;
        float yaw = Mathf.Sin(t * hz * Mathf.PI * 2f) * yawDeg;
        float pitch = Mathf.Sin(t * hz * Mathf.PI * 4f) * pitchDeg;
        head.localRotation = rest * Quaternion.Euler(pitch, yaw, 0f);
    }
}
