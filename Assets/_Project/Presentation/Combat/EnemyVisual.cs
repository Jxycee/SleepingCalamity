// Authority: none (local, cosmetic). The greybox look of one enemy on every peer: its body colour, the white hit flash and the squash/shove on a landed hit,
// the telegraph, the rage pulse, the slime's hop, broken parts disappearing, and the death shrink. Reads the Enemy's replicated state only.
//
// The telegraph (readability pass, 2026-10-30): while an attack winds up, a footprint on the ground shows exactly where the hit will land, built from the SAME
// AttackData the hit test uses (Core Telegraph: range grown by the hero's radius, the cone's edge widened the way HitGeometry does). It fills from the
// centre outward up to the strike frame, then turns solid red while the hitbox is live. A lunge or hop is drawn around its LANDING spot with a lane to it;
// a lobbed glob is drawn in flight and the footprint is where it lands.
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyVisual : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private const float FootprintY = 0.04f;

        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Transform visual;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Color baseColor = Color.gray;
        [SerializeField] private Transform[] partObjects;

        private Enemy _enemy;
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _markerBlock;   // the footprint's colour, created once in Awake (not a field initializer) and reused every frame
        private MaterialPropertyBlock _globBlock;
        private Marker _outline, _fill, _lane;
        private GameObject _glob;
        private AttackData _markedAttack;
        private float _deadTime;
        private Vector3 _visualBase;

        private sealed class Marker
        {
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
        }

        public void Configure(Renderer[] renderers, Transform visualRoot, Material marker, Color color, Transform[] parts)
        {
            bodyRenderers = renderers;
            visual = visualRoot;
            markerMaterial = marker;
            baseColor = color;
            partObjects = parts;
        }

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _block = new MaterialPropertyBlock();
            _markerBlock = new MaterialPropertyBlock();
            _globBlock = new MaterialPropertyBlock();
            if (visual != null) _visualBase = visual.localScale;
        }

        private void OnDestroy()
        {
            if (_outline != null && _outline.Go != null) Destroy(_outline.Go);
            if (_fill != null && _fill.Go != null) Destroy(_fill.Go);
            if (_lane != null && _lane.Go != null) Destroy(_lane.Go);
            if (_glob != null) Destroy(_glob);
        }

        private void Update()
        {
            if (!_enemy.IsSpawned) return;
            float t = Time.time;
            Color tint = baseColor;

            // Rage: a slow pulse toward red while raging, a fast flicker while it telegraphs.
            RageState rage = (RageState)_enemy.NetRage.Value;
            if (rage == RageState.Telegraphing) tint = Color.Lerp(tint, new Color(1f, 0.1f, 0.05f), 0.5f + 0.5f * Mathf.Sin(t * 30f));
            else if (rage == RageState.Raging) tint = Color.Lerp(tint, new Color(1f, 0.15f, 0.1f), 0.35f + 0.15f * Mathf.Sin(t * 6f));

            AttackData a = _enemy.CurrentAttack;
            bool telegraphing = a != null && _enemy.Attacking && Telegraph.Visible(a, _enemy.AttackFrame) && !_enemy.Phase.Equals(EnemyPhase.Dead);
            if (telegraphing)
            {
                float f = _enemy.AttackFrame;
                float pre = Telegraph.Fill(a, f);
                bool live = a.HitOn.Contains(f);
                Color warn = live ? new Color(1f, 0.05f, 0.02f) : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.35f, 0.05f), pre);
                tint = Color.Lerp(tint, warn, live ? 0.9f : 0.25f + 0.55f * pre);
                UpdateFootprint(a, f, pre, live);
                UpdateGlob(a, f);
            }
            else
            {
                HideFootprint();
                if (_glob != null) _glob.SetActive(false);
            }

            if (_enemy.Flash > 0f) tint = Color.Lerp(tint, Color.white, _enemy.Flash);
            _block.SetColor(BaseColor, tint);
            if (bodyRenderers != null)
                for (int i = 0; i < bodyRenderers.Length; i++)
                    if (bodyRenderers[i] != null) bodyRenderers[i].SetPropertyBlock(_block);

            if (partObjects != null)
                for (int i = 0; i < partObjects.Length; i++)
                    if (partObjects[i] != null) partObjects[i].gameObject.SetActive(_enemy.PartIntact(i));

            Animate(t);
        }

        private void Animate(float t)
        {
            if (visual == null) return;
            EnemyPhase phase = _enemy.Phase;
            float y = 0f, squash = 1f;
            if (phase == EnemyPhase.Chase && _enemy.Def.Kind == EnemyKind.Slime) y = Mathf.Abs(Mathf.Sin(t * 5f + _enemy.NetworkObjectId)) * 0.25f;
            else if (phase == EnemyPhase.Chase) y = Mathf.Abs(Mathf.Sin(t * 8f + _enemy.NetworkObjectId)) * 0.05f;
            AttackData a = _enemy.CurrentAttack;
            if (phase == EnemyPhase.Attack && a != null)
            {
                float f = _enemy.AttackFrame;
                if (f < a.HitOn.Start) squash = 1f - 0.25f * (f / a.HitOn.Start);          // crouch into the wind-up
                else if (f < a.HitOn.End) squash = 1.2f;                                      // stretch on the swing
            }
            if (phase == EnemyPhase.Stagger) squash = 0.8f + 0.1f * Mathf.Sin(t * 40f);

            // A landed hit: squash flat and shove away from the attacker, both easing out (bigger for heavy hits).
            float react = _enemy.HitReact;
            Vector3 shove = Vector3.zero;
            if (react > 0f)
            {
                float k = react * react;
                squash *= 1f - (0.18f + 0.22f * _enemy.HitHeavy) * k;
                shove = transform.InverseTransformDirection(_enemy.HitDirection) * ((0.18f + 0.35f * _enemy.HitHeavy) * k);
            }

            float scaleOut = 1f;
            if (phase == EnemyPhase.Dead)
            {
                _deadTime += Time.deltaTime;
                scaleOut = Mathf.Clamp01(1f - _deadTime / 1.2f);
            }
            visual.localPosition = new Vector3(shove.x, y, shove.z);
            visual.localScale = new Vector3(_visualBase.x / Mathf.Sqrt(squash), _visualBase.y * squash, _visualBase.z / Mathf.Sqrt(squash)) * scaleOut;
        }

        // ---- the telegraph footprint ----

        private Marker NewMarker(string name)
        {
            var m = new Marker { Go = new GameObject(name) };
            m.Filter = m.Go.AddComponent<MeshFilter>();
            m.Renderer = m.Go.AddComponent<MeshRenderer>();
            m.Renderer.sharedMaterial = markerMaterial;
            m.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m.Renderer.receiveShadows = false;
            return m;
        }

        private void Paint(Marker m, Color c)
        {
            _markerBlock.SetColor(BaseColor, c);
            m.Renderer.SetPropertyBlock(_markerBlock);
        }

        private void HideFootprint()
        {
            if (_outline != null) _outline.Go.SetActive(false);
            if (_fill != null) _fill.Go.SetActive(false);
            if (_lane != null) _lane.Go.SetActive(false);
        }

        // The strike area (and, for a lunge, the lane to it). `pre` is 0..1 through the wind-up.
        private void UpdateFootprint(AttackData a, float frame, float pre, bool live)
        {
            if (markerMaterial == null || a.Range >= 25f) return;   // the test nova is bigger than the arena: no footprint
            if (_outline == null)
            {
                _outline = NewMarker("TelegraphFootprint");
                _fill = NewMarker("TelegraphFill");
                _lane = NewMarker("TelegraphLane");
                _lane.Filter.sharedMesh = ShapeMeshes.Lane();
            }
            if (_markedAttack != a)
            {
                _markedAttack = a;
                Mesh m = ShapeMeshes.Footprint(a, MovementMath.CapsuleRadius);
                _outline.Filter.sharedMesh = m;
                _fill.Filter.sharedMesh = m;
            }

            bool lob = a.Origin == HitOrigin.GroundAtTarget;
            Vector3 pos = transform.position;
            float yaw = _enemy.YawDeg;
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float remaining = Telegraph.RemainingLunge(a, frame);
            Vector3 c = lob ? _enemy.AttackCentre : pos + fwd * remaining;
            Quaternion rot = Quaternion.Euler(0f, lob ? 0f : yaw, 0f);   // a lob's footprint is a disc (any cone would face the thrower, but lobs are circles)

            _outline.Go.SetActive(true);
            _outline.Go.transform.SetPositionAndRotation(new Vector3(c.x, c.y + FootprintY, c.z), rot);
            _outline.Go.transform.localScale = Vector3.one;
            Paint(_outline, live ? new Color(1f, 0.05f, 0.02f, 0.55f) : new Color(1f, 0.55f, 0.1f, 0.16f));

            // Fills from the centre out to the full footprint at the strike frame; while the hitbox is live it is solid red.
            _fill.Go.SetActive(true);
            _fill.Go.transform.SetPositionAndRotation(new Vector3(c.x, c.y + FootprintY + 0.015f, c.z), rot);
            float s = live ? 1f : Mathf.Max(0.02f, pre);
            _fill.Go.transform.localScale = new Vector3(s, 1f, s);
            Paint(_fill, live ? new Color(1f, 0.05f, 0.02f, 0.75f) : Color.Lerp(new Color(1f, 0.8f, 0.15f, 0.30f), new Color(1f, 0.3f, 0.05f, 0.5f), pre));

            // The lane a lunge or hop travels along.
            bool lane = remaining > 0.15f && !lob;
            _lane.Go.SetActive(lane);
            if (lane)
            {
                float width = _enemy.Def.Radius * 2f;
                _lane.Go.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y + FootprintY + 0.005f, pos.z), rot);
                _lane.Go.transform.localScale = new Vector3(width, 1f, remaining);
                Paint(_lane, new Color(1f, 0.55f, 0.1f, 0.14f + 0.2f * pre));
            }
        }

        // A lobbed glob: a ball flying from the thrower to the footprint, landing on the strike frame (so a hit from range is never invisible).
        private void UpdateGlob(AttackData a, float frame)
        {
            bool lob = a.Origin == HitOrigin.GroundAtTarget && frame < a.HitOn.Start;
            if (!lob) { if (_glob != null) _glob.SetActive(false); return; }
            if (_glob == null)
            {
                _glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(_glob.GetComponent<Collider>());   // not Destroy: a collider that lives to the end of the frame can shove a character controller
                _glob.name = "Glob";
                _glob.transform.localScale = Vector3.one * 0.5f;
                if (bodyRenderers != null && bodyRenderers.Length > 0 && bodyRenderers[0] != null) _glob.GetComponent<Renderer>().sharedMaterial = bodyRenderers[0].sharedMaterial;
                _globBlock.SetColor(BaseColor, baseColor);
                _glob.GetComponent<Renderer>().SetPropertyBlock(_globBlock);
            }
            float k = Mathf.Clamp01(frame / Mathf.Max(1f, a.HitOn.Start));
            Vector3 from = transform.position + Vector3.up * 1.0f, to = _enemy.AttackCentre + Vector3.up * 0.25f;
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += 4f * 2.2f * k * (1f - k);   // an arc 2.2 m above the straight line at its peak
            _glob.SetActive(true);
            _glob.transform.position = p;
        }
    }
}
