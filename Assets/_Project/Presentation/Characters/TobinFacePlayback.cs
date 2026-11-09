// Local-only presentation: drives Tobin's face (blinks, micro-saccades, eye look-at, viseme and jaw shapes) on this machine's own copy of the character.
// No network authority, no NetworkBehaviour, nothing here is synchronised; each client animates its own copy from whatever input it has (audio analysis, a script).
// Shape names are the MetaPerson ones (ARKit 51 + visemes aa, ih, ou, E, oh ...). Every shape write goes to EVERY face mesh that has a shape of that name
// (head, teeth and eyelash meshes carry matching jaw/viseme keys), so the mouth parts never disagree.
// The pure timing and clamping logic lives in TobinFaceLogic and BlinkController (EditMode tested); the MonoBehaviour only applies it.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.Presentation.Characters
{
    /// <summary>Pure face-playback maths: the jaw cap, eye-look clamp and blink curve.</summary>
    public static class TobinFaceLogic
    {
        /// <summary>The widest jawOpen weight (0..1) any driver may apply. One place; the uncapped 1.0 shape looks over-wide on this face.</summary>
        public const float JawOpenCap = 0.5f;

        public static float ClampJaw(float weight) => Mathf.Clamp(weight, 0f, JawOpenCap);

        /// <summary>Clamps a look direction (character-root space, +Z forward, +Y up) to yaw/pitch limits in degrees and returns the clamped angles.</summary>
        public static void ClampLook(Vector3 localDir, float maxYawDeg, float maxPitchDeg, out float yawDeg, out float pitchDeg)
        {
            if (localDir.sqrMagnitude < 1e-8f) { yawDeg = 0f; pitchDeg = 0f; return; }
            yawDeg = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float flat = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
            pitchDeg = -Mathf.Atan2(localDir.y, flat) * Mathf.Rad2Deg;   // positive = looking down (rotation about +X)
            yawDeg = Mathf.Clamp(yawDeg, -maxYawDeg, maxYawDeg);
            pitchDeg = Mathf.Clamp(pitchDeg, -maxPitchDeg, maxPitchDeg);
        }

        /// <summary>Lid weight for a blink that began <paramref name="t"/> seconds ago: ramps 0 to 1 over closeTime, back to 0 over openTime; 0 outside.</summary>
        public static float BlinkWeight(float t, float closeTime, float openTime)
        {
            if (t <= 0f) return 0f;
            if (t < closeTime) return Mathf.SmoothStep(0f, 1f, t / closeTime);
            float u = (t - closeTime) / openTime;
            return u >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, u);
        }
    }

    /// <summary>Random blink schedule: a blink every minInterval..maxInterval seconds (measured from the end of the previous one).</summary>
    public sealed class BlinkController
    {
        public readonly float MinInterval, MaxInterval, CloseTime, OpenTime;
        readonly Func<float> _rand01;
        float _untilNext;
        float _inBlink = -1f;   // seconds since the current blink began, or -1 when idle

        public BlinkController(Func<float> rand01, float minInterval = 2.5f, float maxInterval = 6f, float closeTime = 0.07f, float openTime = 0.12f)
        {
            _rand01 = rand01; MinInterval = minInterval; MaxInterval = maxInterval; CloseTime = closeTime; OpenTime = openTime;
            _untilNext = NextInterval();
        }

        public float NextInterval() => Mathf.Lerp(MinInterval, MaxInterval, _rand01());
        public bool Blinking => _inBlink >= 0f;
        public float SecondsUntilNext => _untilNext;

        /// <summary>Advances by dt and returns the lid weight (0 open, 1 closed).</summary>
        public float Advance(float dt)
        {
            if (_inBlink < 0f)
            {
                _untilNext -= dt;
                if (_untilNext > 0f) return 0f;
                _inBlink = -_untilNext; _untilNext = 0f;   // carry the overshoot into the blink
            }
            else _inBlink += dt;
            float w = TobinFaceLogic.BlinkWeight(_inBlink, CloseTime, OpenTime);
            if (_inBlink >= CloseTime + OpenTime) { _inBlink = -1f; _untilNext = NextInterval(); return 0f; }
            return w;
        }

        public void ForceBlinkNow() { if (_inBlink < 0f) _untilNext = 0f; }
    }

    [DisallowMultipleComponent]
    public sealed class TobinFacePlayback : MonoBehaviour
    {
        [Header("Rig (auto-found under this object when left empty)")]
        public SkinnedMeshRenderer[] faceMeshes;
        public Transform leftEye, rightEye;

        [Header("Eyes")]
        public Transform lookTarget;                       // null = look straight ahead (character root forward)
        public float maxLookYawDeg = 35f, maxLookPitchDeg = 25f;
        public float saccadeAmplitudeDeg = 0.8f;
        public float lookSpeedDeg = 540f;

        [Header("Blink")]
        public bool autoBlink = true;
        [Range(-1f, 1f)] public float blinkOverride = -1f; // >= 0 holds the lids at this weight and suspends random blinks (capture / scripted)

        public int Seed = 12345;

        readonly Dictionary<string, List<(SkinnedMeshRenderer mesh, int index)>> _shapes = new Dictionary<string, List<(SkinnedMeshRenderer, int)>>();
        System.Random _rng;
        BlinkController _blink;
        Quaternion _leftRest, _rightRest;
        float _yaw, _pitch;
        Vector2 _saccade; float _untilSaccade;

        void Awake() { Init(); }

        public void Init()
        {
            _rng = new System.Random(Seed);
            _blink = new BlinkController(() => (float)_rng.NextDouble());
            if (faceMeshes == null || faceMeshes.Length == 0) faceMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _shapes.Clear();
            foreach (var m in faceMeshes)
            {
                if (m == null || m.sharedMesh == null) continue;
                for (int i = 0; i < m.sharedMesh.blendShapeCount; i++)
                {
                    string n = Strip(m.sharedMesh.GetBlendShapeName(i));
                    if (!_shapes.TryGetValue(n, out var list)) _shapes[n] = list = new List<(SkinnedMeshRenderer, int)>();
                    list.Add((m, i));
                }
            }
            if (leftEye == null) leftEye = FindDeep(transform, "LeftEye");
            if (rightEye == null) rightEye = FindDeep(transform, "RightEye");
            if (leftEye) _leftRest = leftEye.localRotation;
            if (rightEye) _rightRest = rightEye.localRotation;
        }

        static string Strip(string n) { int i = n.LastIndexOf('.'); return i >= 0 ? n.Substring(i + 1) : n; }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        public bool HasShape(string name) => _shapes.ContainsKey(name);
        public IEnumerable<string> ShapeNames => _shapes.Keys;

        /// <summary>Sets a shape (0..1 scale; the Unity blendshape range is 0..100) on every face mesh that has it. Returns false if no mesh has it.</summary>
        public bool SetShape(string name, float weight01)
        {
            if (!_shapes.TryGetValue(name, out var list)) return false;
            float w = Mathf.Clamp01(weight01) * 100f;
            foreach (var (mesh, index) in list) mesh.SetBlendShapeWeight(index, w);
            return true;
        }

        /// <summary>jawOpen, always capped at <see cref="TobinFaceLogic.JawOpenCap"/>.</summary>
        public void SetJawOpen(float weight01) => SetShape("jawOpen", TobinFaceLogic.ClampJaw(weight01));

        /// <summary>A viseme shape (aa, ih, ou, E, oh ...) on every face mesh.</summary>
        public bool SetViseme(string shape, float weight01) => SetShape(shape, weight01);

        public void ClearVisemes(IEnumerable<string> shapes) { foreach (var s in shapes) SetShape(s, 0f); }

        void LateUpdate() { Tick(Time.deltaTime); }

        /// <summary>One presentation step: blink, saccade, eye aim. Public so editor captures can step it without a player loop.</summary>
        public void Tick(float dt)
        {
            if (_blink == null) Init();
            float lid = blinkOverride >= 0f ? blinkOverride : (autoBlink ? _blink.Advance(dt) : 0f);
            SetShape("eyeBlinkLeft", lid); SetShape("eyeBlinkRight", lid);
            UpdateEyes(dt);
        }

        void UpdateEyes(float dt)
        {
            if (leftEye == null && rightEye == null) return;
            _untilSaccade -= dt;
            if (_untilSaccade <= 0f)
            {
                _untilSaccade = Mathf.Lerp(0.3f, 1.4f, (float)_rng.NextDouble());
                _saccade = new Vector2(((float)_rng.NextDouble() * 2f - 1f) * saccadeAmplitudeDeg, ((float)_rng.NextDouble() * 2f - 1f) * saccadeAmplitudeDeg);
            }
            float tyaw = 0f, tpitch = 0f;
            if (lookTarget != null)
            {
                Vector3 eyeCentre = leftEye && rightEye ? (leftEye.position + rightEye.position) * 0.5f : (leftEye ? leftEye.position : rightEye.position);
                Vector3 local = transform.InverseTransformDirection(lookTarget.position - eyeCentre);
                TobinFaceLogic.ClampLook(local, maxLookYawDeg, maxLookPitchDeg, out tyaw, out tpitch);
            }
            tyaw += _saccade.x; tpitch += _saccade.y;
            _yaw = Mathf.MoveTowards(_yaw, tyaw, lookSpeedDeg * dt);
            _pitch = Mathf.MoveTowards(_pitch, tpitch, lookSpeedDeg * dt);
            // Aim offset in character-root space applied on top of each eye's rest orientation.
            Quaternion aim = transform.rotation * Quaternion.Euler(_pitch, _yaw, 0f) * Quaternion.Inverse(transform.rotation);
            ApplyEye(leftEye, _leftRest, aim); ApplyEye(rightEye, _rightRest, aim);
        }

        static void ApplyEye(Transform eye, Quaternion restLocal, Quaternion worldAim)
        {
            if (eye == null) return;
            Quaternion restWorld = eye.parent.rotation * restLocal;
            eye.rotation = worldAim * restWorld;
        }
    }
}
