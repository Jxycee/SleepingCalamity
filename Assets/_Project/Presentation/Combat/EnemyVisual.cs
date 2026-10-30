// Authority: none (local, cosmetic). The greybox look of one enemy on every peer: its body colour, the hit flash, the telegraph (the body glows as the
// wind-up runs, and the hitbox footprint shows on the ground until the swing lands), the rage pulse, the slime's hop, broken parts disappearing, and the
// death shrink. Reads the Enemy's replicated state only.
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyVisual : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Transform visual;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Color baseColor = Color.gray;
        [SerializeField] private Transform[] partObjects;

        private Enemy _enemy;
        private MaterialPropertyBlock _block;
        private GameObject _marker;
        private MeshFilter _markerFilter;
        private MeshRenderer _markerRenderer;
        private AttackData _markedAttack;
        private float _deadTime;
        private Vector3 _visualBase;

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
            if (visual != null) _visualBase = visual.localScale;
        }

        private void OnDestroy()
        {
            if (_marker != null) Destroy(_marker);
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
            bool telegraphing = a != null && _enemy.Attacking;
            if (telegraphing)
            {
                float pre = Mathf.Clamp01(_enemy.AttackFrame / Mathf.Max(1f, a.HitOn.Start));
                bool live = a.HitOn.Contains(_enemy.AttackFrame);
                Color warn = live ? new Color(1f, 0.05f, 0.02f) : Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.35f, 0.05f), pre);
                tint = Color.Lerp(tint, warn, live ? 0.9f : 0.25f + 0.55f * pre);
                UpdateMarker(a, pre, live);
            }
            else if (_marker != null) _marker.SetActive(false);

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
            float scaleOut = 1f;
            if (phase == EnemyPhase.Dead)
            {
                _deadTime += Time.deltaTime;
                scaleOut = Mathf.Clamp01(1f - _deadTime / 1.2f);
            }
            visual.localPosition = new Vector3(0f, y, 0f);
            visual.localScale = new Vector3(_visualBase.x / Mathf.Sqrt(squash), _visualBase.y * squash, _visualBase.z / Mathf.Sqrt(squash)) * scaleOut;
        }

        // The hitbox footprint on the ground. Fades in during the wind-up, solid red while the hitbox is live.
        private void UpdateMarker(AttackData a, float pre, bool live)
        {
            if (markerMaterial == null) return;
            if (_marker == null)
            {
                _marker = new GameObject("TelegraphMarker");
                _markerFilter = _marker.AddComponent<MeshFilter>();
                _markerRenderer = _marker.AddComponent<MeshRenderer>();
                _markerRenderer.sharedMaterial = markerMaterial;
                _markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _markerRenderer.receiveShadows = false;
            }
            if (_markedAttack != a)
            {
                _markedAttack = a;
                _markerFilter.sharedMesh = a.Shape == HitShape.Circle ? ShapeMeshes.Disc(a.Range) : ShapeMeshes.Wedge(a.Range, a.HalfAngleDeg);
            }
            Vector3 c = _enemy.AttackCentre;
            _marker.transform.SetPositionAndRotation(new Vector3(c.x, c.y + 0.04f, c.z), Quaternion.Euler(0f, a.Origin == HitOrigin.GroundAtTarget ? 0f : _enemy.YawDeg, 0f));
            _marker.SetActive(a.Range < 25f);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, live ? new Color(1f, 0.05f, 0.02f, 0.65f) : new Color(1f, 0.6f, 0.1f, 0.12f + 0.3f * pre));
            _markerRenderer.SetPropertyBlock(block);
        }
    }
}
