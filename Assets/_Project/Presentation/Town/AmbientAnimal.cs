// Authority: none, local per peer (every peer animates its own copy; nothing is networked or saved; no colliders, no gameplay effect).
// T24: a left-behind animal of Hollowmere (a feral goat or Tuppence the cat) as a rigged mesh whose clips are mixed by a PlayableGraph (no AnimatorController asset: the builder hands in
// the clips). A small state machine picks what it does next with a seeded random stream, so no two peers need agree:
//   Goat   Graze (head down, the Eat clip) -> Look (Idle; the head turns toward the hero within LookDistance, the body follows once the hero is near) -> sometimes Amble to the next of
//          3-6 waypoints (turn in place on Idle, then Walk at WalkSpeed with the clip speed matched to it so the hooves do not slide) -> Graze again.
//   Cat    Idle on its spot (the home sill or the fountain rim) with the head following the hero; every 35-80 s, with nobody close, it walks to the other spot and rests there.
// The animal stands on the ground heights the builder baked into the waypoints (Y is interpolated along a leg). Costs nothing beyond LookDistance*8 of the camera: the graph is stopped there.
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SleepingCalamity.Presentation
{
    public sealed class AmbientAnimal : MonoBehaviour
    {
        public enum Kind : byte { Goat, Cat }
        enum State : byte { Graze, Look, Turn, Amble }

        public Kind Species;
        public AnimationClip Idle, Graze, Walk;
        public Vector3[] Waypoints = new Vector3[0];   // world; the animal starts on [0]
        public Transform Head;                         // the Head bone (looked up by name by the builder)
        public float WalkSpeed = 0.55f;                // m/s the body travels
        public float WalkClipSpeed = 1f;               // playback speed of the Walk clip at that travel speed (the builder sets the matched pair)
        public float LookDistance = 8f;
        public float MaxHeadYaw = 55f;
        public int Seed = 1;
        public float ActiveDistance = 70f;             // the graph runs only within this of the camera

        PlayableGraph _graph; AnimationMixerPlayable _mix;
        AnimationClipPlayable[] _clip = new AnimationClipPlayable[3];
        AnimationClip[] _src = new AnimationClip[3];
        float[] _time = new float[3], _weight = new float[3], _target = new float[3];
        bool _ready, _running = true;
        State _state; float _stateLeft; int _wp, _next;
        float _legT, _legLen, _lookW;
        Vector3 _from, _to;
        System.Random _rnd;
        Transform _hero; float _nextFind;
        Quaternion _headBase;

        float Rand(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

        void OnEnable()
        {
            _rnd = new System.Random(Seed * 7919 + 13);
            var animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || Idle == null) return;
            _src[0] = Idle; _src[1] = Graze != null ? Graze : Idle; _src[2] = Walk != null ? Walk : Idle;
            _graph = PlayableGraph.Create("AmbientAnimal_" + name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mix = AnimationMixerPlayable.Create(_graph, 3);
            for (int i = 0; i < 3; i++)
            {
                _clip[i] = AnimationClipPlayable.Create(_graph, _src[i]);
                _clip[i].SetApplyFootIK(false);
                _graph.Connect(_clip[i], 0, _mix, i);
                _time[i] = Rand(0f, _src[i].length);
            }
            AnimationPlayableOutput.Create(_graph, "out", animator).SetSourcePlayable(_mix);
            _graph.Play();
            _ready = true;
            if (Waypoints.Length > 0) { transform.position = Waypoints[0]; _wp = 0; }
            if (Species == Kind.Goat) Enter(State.Graze); else Enter(State.Look);
            if (Head != null) _headBase = Head.localRotation;
        }

        void OnDisable() { if (_graph.IsValid()) _graph.Destroy(); _ready = false; }

        void Enter(State s)
        {
            _state = s;
            switch (s)
            {
                case State.Graze: _stateLeft = Rand(9f, 18f); Blend(1); break;
                case State.Look: _stateLeft = Species == Kind.Cat ? Rand(35f, 80f) : Rand(3f, 7f); Blend(0); break;
                case State.Turn: _stateLeft = 6f; Blend(0); break;
                case State.Amble: Blend(2); break;
            }
        }

        void Blend(int i) { for (int k = 0; k < 3; k++) _target[k] = k == i ? 1f : 0f; }

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
            if (!_ready) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                bool near = (cam.transform.position - transform.position).sqrMagnitude < ActiveDistance * ActiveDistance;
                if (near != _running) { _running = near; if (near) _graph.Play(); else _graph.Stop(); }
                if (!near) return;
            }
            float dt = Time.deltaTime;
            bool haveHero = FindHero();
            Vector3 toHero = haveHero ? _hero.position - transform.position : Vector3.zero;
            toHero.y = 0f;
            bool heroNear = haveHero && toHero.sqrMagnitude < LookDistance * LookDistance;

            _stateLeft -= dt;
            switch (_state)
            {
                case State.Graze:
                    if (_stateLeft <= 0f || (heroNear && toHero.sqrMagnitude < 16f)) Enter(State.Look);
                    break;
                case State.Look:
                    if (heroNear && toHero.sqrMagnitude > 2.25f && Species == Kind.Goat) TurnToward(toHero, 35f, dt);
                    else if (heroNear && Species == Kind.Cat) TurnToward(toHero, 20f, dt);
                    if (_stateLeft <= 0f && !heroNear)
                    {
                        if (Waypoints.Length > 1 && _rnd.NextDouble() < (Species == Kind.Cat ? 1.0 : 0.55)) { _next = (_wp + 1 + _rnd.Next(Waypoints.Length - 1)) % Waypoints.Length; _from = transform.position; _to = Waypoints[_next]; _legLen = Mathf.Max(0.01f, Vector2.Distance(new Vector2(_from.x, _from.z), new Vector2(_to.x, _to.z))); _legT = 0f; Enter(State.Turn); }
                        else Enter(State.Graze);
                    }
                    break;
                case State.Turn:
                    {
                        Vector3 d = _to - transform.position; d.y = 0f;
                        if (Vector3.Angle(transform.forward, d) < 6f || _stateLeft <= 0f) Enter(State.Amble);
                        else TurnToward(d, 70f, dt);
                        break;
                    }
                case State.Amble:
                    {
                        _legT += WalkSpeed * dt;
                        float f = Mathf.Clamp01(_legT / _legLen);
                        Vector3 p = Vector3.Lerp(_from, _to, f);
                        Vector3 d = _to - transform.position; d.y = 0f;
                        if (d.sqrMagnitude > 1e-4f) TurnToward(d, 90f, dt);
                        transform.position = p;
                        if (f >= 1f) { _wp = _next; Enter(Species == Kind.Cat ? State.Look : State.Graze); }
                        else if (heroNear && toHero.sqrMagnitude < 2.25f) { _wp = _next; Enter(State.Look); }   // stops when the hero walks up to it
                        break;
                    }
            }

            // mix weights and clip clocks
            float k2 = 1f - Mathf.Exp(-dt * 6f);
            for (int i = 0; i < 3; i++)
            {
                _weight[i] = Mathf.Lerp(_weight[i], _target[i], k2);
                _mix.SetInputWeight(i, _weight[i]);
                if (_weight[i] < 0.002f && _target[i] == 0f) continue;
                float speed = i == 2 ? WalkClipSpeed : 1f;
                _time[i] = Mathf.Repeat(_time[i] + dt * speed, Mathf.Max(0.01f, _src[i].length));
                _clip[i].SetTime(_time[i]);
            }
            _lookW = Mathf.Lerp(_lookW, heroNear && _state != State.Amble ? 1f : 0f, 1f - Mathf.Exp(-dt * 3f));
        }

        void TurnToward(Vector3 dir, float degPerSec, float dt)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) return;
            Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degPerSec * dt);
        }

        // After the animator has posed the bones: turn the head toward the hero by a world-space delta on top of the clip pose (no assumption about the bone's axes).
        void LateUpdate()
        {
            if (!_ready || Head == null || _lookW < 0.01f || _hero == null) return;
            Vector3 d = _hero.position + Vector3.up * 0.8f - Head.position;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 0.01f) return;
            Vector3 fwd = transform.forward;
            float yaw = Vector3.SignedAngle(fwd, flat, Vector3.up);
            float pitch = Mathf.Clamp(-Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg, -20f, 25f);
            yaw = Mathf.Clamp(yaw, -MaxHeadYaw, MaxHeadYaw);
            Quaternion delta = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, transform.right);
            Head.rotation = Quaternion.Slerp(Quaternion.identity, delta, _lookW) * Head.rotation;
        }
    }
}
