// Authority: none, local per peer (presentation only; nothing is networked or saved; no colliders).
// T24: a photo-textured 3D crow sitting on a shell top or the bell frame, moved by code (the mesh is one static model, so the motions are whole-body): it holds still with a slow
// breathing bob, turns on the spot to look about (and toward the hero within LookDistance), pecks (pitch down and back, two or three quick dips) and now and then hops to one of a few nearby
// perch points. Seeded per bird. The graph of motions is a small state machine like AmbientAnimal's; it stops updating beyond ActiveDistance of the camera.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class AmbientPerchedBird : MonoBehaviour
    {
        enum State : byte { Rest, Look, Peck, Hop }

        public Vector3[] Perches = new Vector3[0];   // world points on the same shell top; the bird starts on [0]
        public float ModelYaw;                       // degrees added so the beak points along local +Z
        public float LookDistance = 10f;
        public float ActiveDistance = 45f;
        public int Seed = 1;
        public float StartYaw = float.NaN;           // the first heading (degrees); NaN = seeded

        Transform _model;          // child that carries the pitch, bob and yaw offset
        State _state; float _left, _t, _hopT; int _at, _to;
        float _yaw, _yawTo, _pitch;
        Vector3 _from;
        System.Random _rnd;
        Transform _hero; float _nextFind;

        float Rand(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

        void OnEnable()
        {
            _rnd = new System.Random(Seed * 6151 + 7);
            _model = transform;
            if (Perches.Length > 0) transform.position = Perches[0];
            _yaw = _yawTo = float.IsNaN(StartYaw) ? Rand(0f, 360f) : StartYaw;
            Enter(State.Rest);
        }

        void Enter(State s)
        {
            _state = s;
            switch (s)
            {
                case State.Rest: _left = Rand(2f, 6f); break;
                case State.Look: _left = Rand(1.2f, 2.5f); _yawTo = _yaw + Rand(-70f, 70f); break;
                case State.Peck: _left = Rand(1.4f, 2.6f); _t = 0f; break;
                case State.Hop: _hopT = 0f; _from = transform.position; break;
            }
        }

        bool FindHero()
        {
            if (_hero == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 2f;
                foreach (var h in FindObjectsByType<SleepingCalamity.Gameplay.HeroLocomotion>())
                    if (_hero == null || h.gameObject.layer == SleepingCalamity.Core.Layers.Player) _hero = h.transform;
            }
            return _hero != null;
        }

        void Update()
        {
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > ActiveDistance * ActiveDistance) return;
            float dt = Time.deltaTime, time = Time.time;
            bool heroNear = false; Vector3 toHero = default;
            if (FindHero()) { toHero = _hero.position - transform.position; toHero.y = 0f; heroNear = toHero.sqrMagnitude < LookDistance * LookDistance; }

            _left -= dt;
            float bob = 0f, pitch = 0f;
            switch (_state)
            {
                case State.Rest:
                    if (heroNear) _yawTo = Mathf.Atan2(toHero.x, toHero.z) * Mathf.Rad2Deg;
                    if (_left <= 0f)
                    {
                        double r = _rnd.NextDouble();
                        if (r < 0.45) Enter(State.Peck); else if (r < 0.85 || Perches.Length < 2) Enter(State.Look); else { _to = (_at + 1 + _rnd.Next(Perches.Length - 1)) % Perches.Length; Enter(State.Hop); }
                    }
                    break;
                case State.Look:
                    if (_left <= 0f) Enter(State.Rest);
                    break;
                case State.Peck:
                    {
                        _t += dt;
                        float cyc = Mathf.Repeat(_t * 2.6f, 1f);
                        pitch = (cyc < 0.35f ? Mathf.SmoothStep(0f, 1f, cyc / 0.35f) : Mathf.SmoothStep(1f, 0f, (cyc - 0.35f) / 0.65f)) * 34f;
                        if (_left <= 0f) Enter(State.Rest);
                        break;
                    }
                case State.Hop:
                    {
                        _hopT += dt / 0.38f;
                        float f = Mathf.Clamp01(_hopT);
                        Vector3 p = Vector3.Lerp(_from, Perches[_to], f); p.y += Mathf.Sin(f * Mathf.PI) * 0.22f;
                        Vector3 d = Perches[_to] - _from; d.y = 0f;
                        if (d.sqrMagnitude > 1e-4f) _yawTo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        pitch = -Mathf.Sin(f * Mathf.PI) * 14f;
                        transform.position = p;
                        if (f >= 1f) { _at = _to; Enter(State.Rest); }
                        break;
                    }
            }
            bob = Mathf.Sin(time * 2.1f + Seed) * 0.004f;
            _yaw = Mathf.MoveTowardsAngle(_yaw, _yawTo, (_state == State.Hop ? 420f : 95f) * dt);
            _pitch = Mathf.Lerp(_pitch, pitch, 1f - Mathf.Exp(-dt * 14f));
            if (_state != State.Hop && Perches.Length > 0) transform.position = Perches[_at] + Vector3.up * bob;
            transform.rotation = Quaternion.Euler(0f, _yaw + ModelYaw, 0f) * Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
