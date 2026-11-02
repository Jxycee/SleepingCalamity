// Authority: none (local presentation, every peer animates its own copy; nothing is networked or saved).
// Hollowmere's flying ambient life as pooled cards: crows round the bell tower and the ruins, swallows over the square, gulls over the lake, rooks that sit in a
// field and lift when the hero comes within HollowmereAmbientLife.ScatterDistance, and butterflies and bees wandering over the meadows and the garden. HollowmereAmbientBuilder
// creates the cards (quads with the SC_BirdCard wing-flap shader and the AmbientCard mesh) as children at build time, so nothing is instantiated or allocated while playing;
// this component only moves them. Motion is closed-form (a pure function of the time), the perched flocks add a small state machine. Not networked: positions differ
// between peers, which is fine for ambience. No colliders anywhere. Tuning: HollowmereAmbientLife.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class AmbientCreatures : MonoBehaviour
    {
        public enum Motion : byte { Circle, Wander, Perch }

        [System.Serializable]
        public sealed class Flock
        {
            public string Name;
            public Motion Move;
            public Vector3 Centre;      // world; Perch: the ground point the flock sits around
            public float Radius;        // Circle: orbit radius. Wander: how far one creature roams. Perch: unused
            public float Height;        // above Centre.y (Circle, Wander)
            public float Speed;         // Circle: m/s along the orbit. Wander: rad/s scale of the sway
            public float Spread;        // Wander: how far the creatures' anchors lie apart. Perch: perch scatter
            public float Clump = 6.28f; // Circle: angle (rad) the flock is spread over
            public float Wingspan = 0.5f;
            public float Tilt;          // degrees the card pitches up from its heading (butterflies and bees tilt so their wings show to a camera at eye height)
            public float AloftRadius = 34f, AloftHeight = 30f;   // Perch: the wheel after a lift
            public int Seed;
            public Transform[] Cards = new Transform[0];

            // runtime, built once on first use
            [System.NonSerialized] public bool Ready;
            [System.NonSerialized] public Vector3[] Pos, Last, Vel;
            [System.NonSerialized] public float[] Timer;
            [System.NonSerialized] public byte[] Mode;
            [System.NonSerialized] public Renderer[] Rend;
        }

        public Flock[] Flocks = new Flock[0];
        const byte Perched = 0, Fleeing = 1, Aloft = 2, Returning = 3;

        Transform _hero; float _nextFind;
        MaterialPropertyBlock _mpb;
        static readonly int FoldId = Shader.PropertyToID("_Fold");

        /// <summary>The shared card: wing span along X (unit half-span 1, so scale = wingspan / 2), body forward +Z, uv.x = |x| for the flap.</summary>
        public static Mesh BuildCardMesh()
        {
            var m = new Mesh { name = "AmbientCard" };
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0.55f), new Vector3(0f, 0f, -0.5f),            // 0 nose, 1 tail
                new Vector3(-0.12f, 0f, 0.15f), new Vector3(0.12f, 0f, 0.15f),     // 2, 3 front roots
                new Vector3(-0.12f, 0f, -0.2f), new Vector3(0.12f, 0f, -0.2f),     // 4, 5 back roots
                new Vector3(-1f, 0f, -0.15f), new Vector3(1f, 0f, -0.15f),         // 6, 7 wing tips
                new Vector3(-0.55f, 0f, 0.1f), new Vector3(0.55f, 0f, 0.1f),       // 8, 9 mid leading edge
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0f), new Vector2(0.12f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.55f, 0f), new Vector2(0.55f, 0f),
            };
            m.triangles = new[]
            {
                0, 3, 2,   2, 3, 5,   2, 5, 4,   4, 5, 1,        // body
                2, 8, 4,   8, 6, 4,                              // left wing (root, mid, tip)
                3, 5, 9,   9, 5, 7,                              // right wing
            };
            m.RecalculateBounds();
            m.UploadMeshData(false);
            return m;
        }

        static float H(int seed, int i, int salt) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f + i * 78.233f + salt * 37.719f) * 43758.5453f, 1f);

        void Init(Flock f)
        {
            int n = f.Cards.Length;
            f.Pos = new Vector3[n]; f.Last = new Vector3[n]; f.Vel = new Vector3[n]; f.Timer = new float[n]; f.Mode = new byte[n]; f.Rend = new Renderer[n];
            for (int i = 0; i < n; i++) f.Rend[i] = f.Cards[i] != null ? f.Cards[i].GetComponent<Renderer>() : null;
            f.Ready = true;
            if (f.Move == Motion.Perch) for (int i = 0; i < n; i++) { f.Pos[i] = PerchPos(f, i); f.Last[i] = f.Pos[i]; SetFold(f, i, 1f); }
        }

        void SetFold(Flock f, int i, float fold)
        {
            if (f.Rend[i] == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            f.Rend[i].GetPropertyBlock(_mpb);
            _mpb.SetFloat(FoldId, fold);
            f.Rend[i].SetPropertyBlock(_mpb);
        }

        static Vector3 PerchPos(Flock f, int i) => new Vector3(f.Centre.x + (H(f.Seed, i, 5) - 0.5f) * f.Spread, f.Centre.y + 0.12f, f.Centre.z + (H(f.Seed, i, 6) - 0.5f) * f.Spread);

        static Vector3 CirclePos(Flock f, int i, float t, float radius, float height)
        {
            float r = radius * (0.82f + 0.36f * H(f.Seed, i, 3));
            float a = H(f.Seed, i, 0) * f.Clump + t * f.Speed * (0.93f + 0.14f * H(f.Seed, i, 2)) / Mathf.Max(radius, 1f);
            float h = height * (0.9f + 0.2f * H(f.Seed, i, 4)) + Mathf.Sin(t * 0.37f + H(f.Seed, i, 7) * 6.28f) * height * 0.06f;
            return new Vector3(f.Centre.x + Mathf.Cos(a) * r, f.Centre.y + h, f.Centre.z + Mathf.Sin(a) * r * 0.8f);
        }

        static Vector3 WanderPos(Flock f, int i, float t)
        {
            float k = f.Speed * (0.6f + 0.8f * H(f.Seed, i, 2));
            float ax = f.Centre.x + (H(f.Seed, i, 5) - 0.5f) * f.Spread, az = f.Centre.z + (H(f.Seed, i, 6) - 0.5f) * f.Spread;
            float x = ax + Mathf.Sin(t * k + H(f.Seed, i, 0) * 6.28f) * f.Radius;
            float z = az + Mathf.Sin(t * k * 0.83f + H(f.Seed, i, 1) * 6.28f + 1.3f * Mathf.Sin(t * 0.21f)) * f.Radius * 0.8f;
            float y = f.Centre.y + f.Height + Mathf.Sin(t * k * 1.7f + H(f.Seed, i, 3) * 6.28f) * f.Height * 0.4f;
            return new Vector3(x, y, z);
        }

        void Update()
        {
            if (Flocks.Length == 0) return;
            Pose(Time.time, Time.deltaTime, true);
        }

        /// <summary>One animation step. Also called by the builder (dt 0, no hero) so an edit-mode capture sees the cards in place.</summary>
        public void Pose(float t, float dt, bool live)
        {
            Camera cam = live ? Camera.main : null;
            Vector3 hero = default; bool haveHero = false;
            if (live)
            {
                if (_hero == null && Time.unscaledTime >= _nextFind)
                {
                    _nextFind = Time.unscaledTime + 2f;
                    foreach (var h in FindObjectsByType<SleepingCalamity.Gameplay.HeroLocomotion>())
                        if (_hero == null || h.gameObject.layer == SleepingCalamity.Core.Layers.Player) _hero = h.transform;
                }
                if (_hero != null) { hero = _hero.position; haveHero = true; }
            }
            float cull = HollowmereAmbientLife.FlockCullDistance * HollowmereAmbientLife.FlockCullDistance;
            for (int k = 0; k < Flocks.Length; k++)
            {
                Flock f = Flocks[k];
                if (f.Cards == null || f.Cards.Length == 0) continue;
                if (!f.Ready) Init(f);
                if (cam != null && (cam.transform.position - f.Centre).sqrMagnitude > cull) continue;
                float scale = f.Wingspan * 0.5f;
                for (int i = 0; i < f.Cards.Length; i++)
                {
                    Transform c = f.Cards[i]; if (c == null) continue;
                    Vector3 p;
                    switch (f.Move)
                    {
                        case Motion.Circle: p = CirclePos(f, i, t, f.Radius, f.Height); break;
                        case Motion.Wander: p = WanderPos(f, i, t); break;
                        default: p = StepPerch(f, i, dt, haveHero, hero); break;
                    }
                    if (f.Move != Motion.Perch)
                    {
                        Vector3 ahead = f.Move == Motion.Circle ? CirclePos(f, i, t + 0.1f, f.Radius, f.Height) : WanderPos(f, i, t + 0.1f);
                        Face(c, ahead - p);
                        if (f.Tilt != 0f) c.rotation = c.rotation * Quaternion.Euler(-f.Tilt, 0f, 0f);
                    }
                    else
                    {
                        Vector3 d = p - f.Last[i]; f.Last[i] = p;
                        if (f.Mode[i] == Perched) c.rotation = Quaternion.Euler(0f, H(f.Seed, i, 8) * 360f, 0f);
                        else Face(c, d);
                    }
                    c.position = p;
                    c.localScale = new Vector3(scale, scale, scale);
                }
            }
        }

        static void Face(Transform c, Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-8f) return;
            c.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        Vector3 StepPerch(Flock f, int i, float dt, bool haveHero, Vector3 hero)
        {
            Vector3 perch = PerchPos(f, i), pos = f.Pos[i];
            float trig = HollowmereAmbientLife.ScatterDistance;
            switch (f.Mode[i])
            {
                case Perched:
                    pos = perch;
                    if (haveHero && (hero - perch).sqrMagnitude < trig * trig) Lift(f, hero);
                    break;
                case Fleeing:
                    if (f.Timer[i] < 0f) { f.Timer[i] += dt; if (f.Timer[i] >= 0f) { SetFold(f, i, 0f); } break; }
                    pos += f.Vel[i] * dt; f.Timer[i] += dt;
                    f.Vel[i].y = Mathf.Lerp(f.Vel[i].y, 3f, 1f - Mathf.Exp(-dt * 0.8f));
                    if (f.Timer[i] > 2.2f) { f.Mode[i] = Aloft; f.Timer[i] = 0f; }
                    break;
                case Aloft:
                    {
                        Vector3 target = CirclePos(f, i, Time.time, f.AloftRadius, f.AloftHeight);
                        pos = Vector3.Lerp(pos, target, 1f - Mathf.Exp(-dt * 0.9f));
                        f.Timer[i] += dt;
                        if (f.Timer[i] > HollowmereAmbientLife.AloftSeconds)
                        {
                            float far = trig + 12f;
                            if (haveHero && (hero - perch).sqrMagnitude < far * far) f.Timer[i] = HollowmereAmbientLife.AloftSeconds - 4f;   // the hero is still there: keep wheeling
                            else f.Mode[i] = Returning;
                        }
                        break;
                    }
                case Returning:
                    pos = Vector3.MoveTowards(pos, perch, 7f * dt);
                    if ((pos - perch).sqrMagnitude < 0.05f) { f.Mode[i] = Perched; pos = perch; SetFold(f, i, 1f); }
                    break;
            }
            f.Pos[i] = pos;
            return pos;
        }

        // The hero came close: every perched bird of the flock takes off away from them, a short random delay apiece.
        void Lift(Flock f, Vector3 hero)
        {
            for (int j = 0; j < f.Cards.Length; j++)
            {
                if (f.Mode[j] != Perched) continue;
                Vector3 away = f.Pos[j] - hero; away.y = 0f;
                away = away.sqrMagnitude < 0.01f ? new Vector3(H(f.Seed, j, 9) - 0.5f, 0f, H(f.Seed, j, 10) - 0.5f) : away;
                away.Normalize();
                f.Vel[j] = away * (4.5f + 3f * H(f.Seed, j, 11)) + Vector3.up * (4.5f + 2.5f * H(f.Seed, j, 12));
                f.Timer[j] = -0.7f * H(f.Seed, j, 13);
                f.Mode[j] = Fleeing;
            }
        }
    }
}
