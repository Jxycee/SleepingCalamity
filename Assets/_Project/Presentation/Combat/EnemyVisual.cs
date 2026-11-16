// Authority: none (local, cosmetic). The greybox look of one enemy on every peer: its body colour, the white hit flash and the squash/shove on a landed hit,
// the attack tell, the rage pulse, the slime's hop, broken parts disappearing, and the death shrink. Reads the Enemy's replicated state only.
//
// The tell (enemy polish, 2026-10-30; user: "readable without ground indicators"): every attack is staged on the body from its Core EnemyTell, sampled
// from this peer's own copy of the attack clock (TellMath): an anticipation pose (lean back, crouch, coil, rise) that peaks and holds with a shiver, then
// at the COMMIT a white-hot glint on the attacking part (lid, crown, tail, bristles, handle, rolling pin) with a star flare and a "shing", 12-24 frames
// before the hitbox opens, a snap into the strike, an eased recovery. Each attack also plays its own wind-up sound when it starts (a squelch, a scratch,
// a wooden creak, a low groan) and the elite's signature tolls a bell as its pose peaks while its crown glows hotter through the hold.
//
// A rigged enemy (an Animator under Visual: Slime, RatKing, Broom) is played by EnemyAnimatorDriver and skips the procedural squash, lean, twist and hop;
// the hit tint is then added as emission so the model keeps its own materials. Greybox enemies are unchanged.
//
// The ground footprint (readability pass) is now an optional assist, off by default (CombatAssists.GroundTelegraph, F2 in the arena): built from the SAME
// AttackData the hit test uses (Core Telegraph), it fills to the strike frame and turns solid red while the hitbox is live. A lobbed glob always shows
// in flight and casts a growing shadow where it will land (a shadow, not a marker), so a hit from range is never invisible with the assist off.
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyVisual : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public const float DeathClipSeconds = 1.2f;
        private const float DeathShrinkSeconds = 0.2f;   // a rigged corpse plays its clip, then shrinks before the 1.4 s despawn
        private const float FootprintY = 0.04f;
        private static readonly Color GlintColor = new Color(1f, 0.97f, 0.8f);
        private static readonly Color ChargeColor = new Color(1f, 0.55f, 0.12f);

        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Transform visual;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Color baseColor = Color.gray;
        [SerializeField] private Transform[] partObjects;

        private Enemy _enemy;
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _partBlock;     // the attacking part's colour during a tell (its own block, so only it glints)
        private MaterialPropertyBlock _markerBlock;   // the footprint's colour, created once in Awake (not a field initializer) and reused every frame
        private MaterialPropertyBlock _globBlock;
        private Marker _outline, _fill, _lane, _shadow;
        private GameObject _glob;
        private Transform _flare;
        private Renderer[] _flareRenderers;
        private AttackData _markedAttack, _shadowAttack;
        private float _deadTime;
        private Vector3 _visualBase;
        private Quaternion _visualRotBase = Quaternion.identity;
        private readonly Transform[] _tellParts = new Transform[8];   // by TellPart; null = the whole body
        private Transform _activePart;
        private AudioBus _bus;
        private int _soundSerial;
        private float _prevFrame = -1f;
        private float _prevFlash;
        private bool _flashAllowed = true;
        private EnemyAnimatorDriver _anim;   // a rigged model (an Animator under Visual): the clips replace the procedural squash/lean/hop below
        private Transform _lid;

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
            _partBlock = new MaterialPropertyBlock();
            _markerBlock = new MaterialPropertyBlock();
            _globBlock = new MaterialPropertyBlock();
            if (visual != null)
            {
                _visualBase = visual.localScale;
                _visualRotBase = visual.localRotation;
                // The greybox rig's parts by name (PrefabBuilder.BuildEnemies), found at runtime so the prefabs need no rebuild.
                // A rigged model nests its meshes under the FBX root, so the search is recursive (a direct child still wins).
                _tellParts[(int)TellPart.Head] = FindPart("Head");
                _tellParts[(int)TellPart.Tail] = FindPart("Tail");
                _tellParts[(int)TellPart.Bristles] = FindPart("Bristles");
                _tellParts[(int)TellPart.Handle] = FindPart("Handle");
                _tellParts[(int)TellPart.RollingPin] = FindPart("Part_RollingPin");
                _tellParts[(int)TellPart.Crown] = FindPart("Part_DoughCrown") ?? FindPart("Crown");
                _tellParts[(int)TellPart.Lid] = FindPart("Lid");
                _lid = _tellParts[(int)TellPart.Lid];
                Animator animator = visual.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.runtimeAnimatorController != null) _anim = new EnemyAnimatorDriver(animator);
            }
        }

        private Transform FindPart(string name)
        {
            Transform t = visual.Find(name);
            if (t != null) return t;
            // The recursive search is for rigged models only (their parts sit under Visual/Model); greybox rigs keep the direct-child lookup.
            if (visual.GetComponentInChildren<Animator>(true) == null) return null;
            foreach (Transform c in visual.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }

        // A rigged model keeps its own material colours, so the tint is added as emission instead of overwriting _BaseColor (the materials carry a
        // faint _EmissionColor so the keyword stays on). No tint clears the block, so an ember eye keeps its own glow.
        private void PaintBlock(MaterialPropertyBlock block, Color tint)
        {
            if (_anim == null) { block.SetColor(BaseColor, tint); return; }
            Color add = new Color(Mathf.Max(0f, tint.r - baseColor.r), Mathf.Max(0f, tint.g - baseColor.g), Mathf.Max(0f, tint.b - baseColor.b), 1f);
            block.Clear();
            if (add.maxColorComponent > 0.002f) block.SetColor(EmissionColor, add);
        }

        private void OnDestroy()
        {
            if (_outline != null && _outline.Go != null) Destroy(_outline.Go);
            if (_fill != null && _fill.Go != null) Destroy(_fill.Go);
            if (_lane != null && _lane.Go != null) Destroy(_lane.Go);
            if (_shadow != null && _shadow.Go != null) Destroy(_shadow.Go);
            if (_glob != null) Destroy(_glob);
            if (_flare != null) Destroy(_flare.gameObject);
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
            bool attacking = a != null && _enemy.Attacking && _enemy.Phase != EnemyPhase.Dead;
            TellPose pose = attacking ? TellMath.Sample(a, _enemy.AttackFrame) : TellPose.Rest;
            _activePart = attacking && a.Tell != null ? PartFor(a.Tell.Part) : null;
            if (attacking)
            {
                float f = _enemy.AttackFrame;
                PlayTellSounds(a, f);
                float pre = Telegraph.Fill(a, f);
                bool live = a.HitOn.Contains(f);
                // The body warms a little through the wind-up and flashes red on the strike; the attacking part carries the real signal (below).
                if (live) tint = Color.Lerp(tint, new Color(1f, 0.05f, 0.02f), 0.6f);
                else if (f < a.HitOn.Start) tint = Color.Lerp(tint, new Color(1f, 0.6f, 0.2f), 0.08f + 0.17f * pre);
                if (CombatAssists.GroundTelegraph && Telegraph.Visible(a, f)) UpdateFootprint(a, f, pre, live);
                else HideFootprint();
                UpdateGlob(a, f);
            }
            else
            {
                HideFootprint();
                if (_glob != null) _glob.SetActive(false);
                if (_shadow != null) _shadow.Go.SetActive(false);
                _prevFrame = -1f;
            }

            // Reduce flashes: a new flash may be refused (3 a second at most) and any flash is capped at half brightness (FlashLimiter).
            float flash = _enemy.Flash;
            if (flash > 0f && _prevFlash <= 0f) _flashAllowed = FeedbackDirector.Flash.Allow(Time.unscaledTime);
            _prevFlash = flash;
            if (flash > 0f && _flashAllowed) tint = Color.Lerp(tint, Color.white, FeedbackDirector.Flash.Brightness(flash));
            PaintBlock(_block, tint);

            // The attacking part: glows hotter through a charge hold, then flashes white-hot at the commit (the glint).
            Color partTint = tint;
            if (pose.Charge > 0f) partTint = Color.Lerp(partTint, ChargeColor, 0.35f + 0.5f * pose.Charge + 0.15f * Mathf.Sin(t * (14f + 20f * pose.Charge)));
            if (pose.Glint > 0f) partTint = Color.Lerp(partTint, GlintColor * 2f, Mathf.Sqrt(pose.Glint));   // over 1: brighter than lit white, so it reads as a flash
            PaintBlock(_partBlock, partTint);
            bool partLit = pose.Charge > 0f || pose.Glint > 0f;

            if (bodyRenderers != null)
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    Renderer r = bodyRenderers[i];
                    if (r == null) continue;
                    bool isPart = partLit && (_activePart == null ? pose.Glint > 0f : r.transform == _activePart);
                    MaterialPropertyBlock use = isPart ? _partBlock : _block;
                    r.SetPropertyBlock(_anim != null && use.isEmpty ? null : use);
                }

            if (partObjects != null)
                for (int i = 0; i < partObjects.Length; i++)
                    if (partObjects[i] != null) partObjects[i].gameObject.SetActive(_enemy.PartIntact(i));

            _anim?.Tick(_enemy, t);
            Animate(t, pose, a);
            UpdateFlare(pose);
        }

        // The named part, or null (the whole body) when the rig has none or it has been broken off.
        private Transform PartFor(TellPart part)
        {
            Transform p = _tellParts[(int)part];
            return p != null && p.gameObject.activeInHierarchy ? p : null;
        }

        private void PlayTellSounds(AttackData a, float f)
        {
            EnemyTell tell = a.Tell;
            if (_enemy.Serial != _soundSerial) { _soundSerial = _enemy.Serial; _prevFrame = -1f; }
            float prev = _prevFrame;
            _prevFrame = f;
            if (tell == null || f >= a.HitOn.Start) return;
            if (_bus == null)
            {
                ServiceRegistry app = ServiceRoots.App;
                if (app == null || !app.TryGet(out _bus)) return;
            }
            Vector3 at = transform.position;
            // By crossing, so a skipped frame never loses a cue; a cue more than a few frames stale (a late join) stays silent.
            if (TellMath.Crossed(prev, f, 0) && f < 8f) _bus.PlayAt(tell.WindUpSfx, at, tell.Pitch);
            if (tell.HoldSfx != null && TellMath.Crossed(prev, f, tell.PoseEnd) && f < tell.PoseEnd + 8f) _bus.PlayAt(tell.HoldSfx, at, 1f);
            if (TellMath.Crossed(prev, f, tell.Commit) && f < tell.Commit + 6f) _bus.PlayAt(CombatSfx.Glint, at, tell.Pitch > 1f ? 1.1f : tell.Pitch < 0.85f ? 0.85f : 1f);
        }

        private void Animate(float t, TellPose pose, AttackData a)
        {
            if (visual == null) return;
            EnemyPhase phase = _enemy.Phase;
            if (_anim != null) { AnimateRigged(phase); return; }
            float y = 0f, squash = 1f;
            Vector3 jitter = Vector3.zero;
            if (phase == EnemyPhase.Chase && _enemy.Def.Kind == EnemyKind.Slime) y = Mathf.Abs(Mathf.Sin(t * 5f + _enemy.NetworkObjectId)) * 0.25f;
            else if (phase == EnemyPhase.Chase) y = Mathf.Abs(Mathf.Sin(t * 8f + _enemy.NetworkObjectId)) * 0.05f;
            float lean = 0f, twist = 0f;
            if (phase == EnemyPhase.Attack && a != null)
            {
                squash = pose.Squash;
                y += pose.Rise;
                lean = pose.Lean;
                twist = pose.Twist;
                if (pose.Tremble > 0f)
                {
                    float amp = 0.035f * pose.Tremble * Mathf.Max(1f, _enemy.Def.Radius * 1.5f);
                    jitter = new Vector3(Mathf.Sin(t * 57f), 0f, Mathf.Cos(t * 43f)) * amp;
                }
                // A hop rides an arc over its lunge window (the slime's spring).
                float f = _enemy.AttackFrame;
                if (_enemy.Def.Kind == EnemyKind.Slime && a.LungeMeters > 0f && a.Lunge.Contains(f))
                    y += Mathf.Sin(Mathf.PI * (f - a.Lunge.Start) / Mathf.Max(1f, a.Lunge.End - a.Lunge.Start)) * 0.7f;
            }
            if (phase == EnemyPhase.Stagger) squash = 0.8f + 0.1f * Mathf.Sin(t * 40f);
            else if (_enemy.Braced) squash *= 0.9f;   // combat depth: the broom braces behind its bristles when its guard takes a hit

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
            visual.localPosition = new Vector3(shove.x + jitter.x, y, shove.z + jitter.z);
            // Lean back is a pitch about the feet (the visual root sits at the feet): negative X tips the top away from the facing.
            visual.localRotation = _visualRotBase * Quaternion.Euler(-lean, twist, 0f);
            visual.localScale = new Vector3(_visualBase.x / Mathf.Sqrt(squash), _visualBase.y * squash, _visualBase.z / Mathf.Sqrt(squash)) * scaleOut;
        }

        // A rigged enemy: the clips do the squash, lean, twist and hop, so only the shove from a landed hit and the death shrink stay procedural.
        private void AnimateRigged(EnemyPhase phase)
        {
            Vector3 shove = Vector3.zero;
            float react = _enemy.HitReact;
            if (react > 0f) shove = transform.InverseTransformDirection(_enemy.HitDirection) * ((0.18f + 0.35f * _enemy.HitHeavy) * react * react);
            float scaleOut = 1f;
            if (phase == EnemyPhase.Dead)
            {
                _deadTime += Time.deltaTime;
                scaleOut = Mathf.Clamp01(1f - (_deadTime - DeathClipSeconds) / DeathShrinkSeconds);
            }
            visual.localPosition = new Vector3(shove.x, 0f, shove.z);
            visual.localRotation = _visualRotBase;
            visual.localScale = _visualBase * scaleOut;
        }

        // ---- the glint flare: a four-point star that pops on the attacking part at the commit ----

        private void UpdateFlare(TellPose pose)
        {
            float g = pose.Glint;
            float charge = pose.Charge > 0f && pose.Glint <= 0f ? pose.Charge : 0f;
            if ((g <= 0f && charge <= 0f) || markerMaterial == null)
            {
                if (_flare != null) _flare.gameObject.SetActive(false);
                return;
            }
            if (_flare == null) BuildFlare();
            Camera cam = Camera.main;
            Bounds b = PartBounds();
            Vector3 toCam = cam != null ? (cam.transform.position - b.center).normalized : -transform.forward;
            Vector3 at = b.center + Vector3.up * (b.extents.y * 0.5f) + toCam * Mathf.Min(0.6f, b.extents.magnitude * 0.6f);
            float size = 0.8f + 0.5f * _enemy.Def.Radius;
            float s = g > 0f ? size * Mathf.Sqrt(g) * (1f + 0.4f * (1f - g)) : size * 0.35f * charge * (0.8f + 0.2f * Mathf.Sin(Time.time * 30f));
            _flare.gameObject.SetActive(true);
            _flare.position = at;
            if (cam != null) _flare.rotation = Quaternion.LookRotation(-toCam, cam.transform.up) * Quaternion.Euler(0f, 0f, 45f * (1f - g));
            _flare.localScale = Vector3.one * Mathf.Max(0.001f, s);
            Color c = g > 0f ? new Color(GlintColor.r, GlintColor.g, GlintColor.b, Mathf.Clamp01(0.4f + g)) : new Color(ChargeColor.r, ChargeColor.g, ChargeColor.b, 0.6f);
            _globBlock.SetColor(BaseColor, c);
            for (int i = 0; i < _flareRenderers.Length; i++) _flareRenderers[i].SetPropertyBlock(_globBlock);
        }

        private Bounds PartBounds()
        {
            if (_activePart != null)
            {
                Renderer r = _activePart.GetComponent<Renderer>();
                if (r != null) return r.bounds;
            }
            float h = _enemy.Def.Height;
            return new Bounds(transform.position + Vector3.up * h * 0.7f, new Vector3(_enemy.Def.Radius, h * 0.3f, _enemy.Def.Radius));
        }

        private void BuildFlare()
        {
            _flare = new GameObject("TellGlint").transform;
            Transform Ray(string name, PrimitiveType type, Vector3 scale)
            {
                GameObject go = GameObject.CreatePrimitive(type);
                DestroyImmediate(go.GetComponent<Collider>());   // not Destroy: a collider that lives to the end of the frame can shove a character controller
                go.name = name;
                go.transform.SetParent(_flare, false);
                go.transform.localScale = scale;
                Renderer r = go.GetComponent<Renderer>();
                r.sharedMaterial = markerMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                return go.transform;
            }
            Ray("RayV", PrimitiveType.Cube, new Vector3(0.09f, 1f, 0.01f));
            Ray("RayH", PrimitiveType.Cube, new Vector3(1f, 0.09f, 0.01f));
            Ray("Core", PrimitiveType.Sphere, new Vector3(0.3f, 0.3f, 0.3f));
            _flareRenderers = _flare.GetComponentsInChildren<Renderer>(true);
        }

        // ---- the telegraph footprint (assist) ----

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
            // After the strike the footprint fades out in the same red (never back to the orange of a wind-up, which would read as a second attack coming).
            bool after = frame >= a.HitOn.End;
            float fade = after ? Mathf.Clamp01(1f - (frame - a.HitOn.End) / Telegraph.AfterglowFrames) : 1f;
            Paint(_outline, live ? new Color(1f, 0.05f, 0.02f, 0.55f) : after ? new Color(0.9f, 0.1f, 0.08f, 0.45f * fade) : new Color(1f, 0.55f, 0.1f, 0.16f));

            // Fills from the centre out to the full footprint at the strike frame; while the hitbox is live it is solid red.
            _fill.Go.SetActive(true);
            _fill.Go.transform.SetPositionAndRotation(new Vector3(c.x, c.y + FootprintY + 0.015f, c.z), rot);
            float s = live ? 1f : Mathf.Max(0.02f, pre);
            _fill.Go.transform.localScale = new Vector3(s, 1f, s);
            Paint(_fill, live ? new Color(1f, 0.05f, 0.02f, 0.75f) : after ? new Color(0.9f, 0.1f, 0.08f, 0.55f * fade) : Color.Lerp(new Color(1f, 0.8f, 0.15f, 0.30f), new Color(1f, 0.3f, 0.05f, 0.5f), pre));

            // The lane a lunge or hop travels along.
            bool lane = remaining > 0.15f && !lob && !after;
            _lane.Go.SetActive(lane);
            if (lane)
            {
                float width = _enemy.Def.Radius * 2f;
                _lane.Go.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y + FootprintY + 0.005f, pos.z), rot);
                _lane.Go.transform.localScale = new Vector3(width, 1f, remaining);
                Paint(_lane, new Color(1f, 0.55f, 0.1f, 0.14f + 0.2f * pre));
            }
        }

        private Material JamMaterial()
        {
            Material first = null;
            if (bodyRenderers != null)
                foreach (Renderer r in bodyRenderers)
                    if (r != null)
                        foreach (Material m in r.sharedMaterials)
                        {
                            if (m == null) continue;
                            if (m.name.Contains("Jam")) return m;
                            if (first == null) first = m;
                        }
            return first;
        }

        // A lobbed glob: it swells on the slime's lid through the wind-up, is thrown at the glint and flies to where it lands on the strike frame, casting a
        // shadow that grows as it falls (so a hit from range is never invisible, even with the footprint assist off).
        private void UpdateGlob(AttackData a, float frame)
        {
            bool lob = a.Origin == HitOrigin.GroundAtTarget && frame < a.HitOn.End;
            if (!lob)
            {
                if (_glob != null) _glob.SetActive(false);
                if (_shadow != null) _shadow.Go.SetActive(false);
                return;
            }
            if (_glob == null)
            {
                _glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(_glob.GetComponent<Collider>());   // not Destroy: a collider that lives to the end of the frame can shove a character controller
                _glob.name = "Glob";
                if (_anim != null) _glob.GetComponent<Renderer>().sharedMaterial = JamMaterial();   // the model's own jam material, untinted
                else
                {
                    if (bodyRenderers != null && bodyRenderers.Length > 0 && bodyRenderers[0] != null) _glob.GetComponent<Renderer>().sharedMaterial = bodyRenderers[0].sharedMaterial;
                    _globBlock.SetColor(BaseColor, baseColor);
                    _glob.GetComponent<Renderer>().SetPropertyBlock(_globBlock);
                }
            }
            int launch = a.Tell != null ? a.Tell.Commit : 0;
            Vector3 lid = _lid != null && _anim != null ? _lid.position + Vector3.up * 0.05f
                : transform.position + Vector3.up * (1.15f * (visual != null ? visual.localScale.y / Mathf.Max(0.01f, _visualBase.y) : 1f));
            Vector3 to = _enemy.AttackCentre + Vector3.up * 0.25f;
            float k = 0f;
            if (frame < launch)
            {
                // Swelling on the lid.
                float grow = launch <= 0 ? 1f : frame / launch;
                _glob.SetActive(true);
                _glob.transform.position = lid + Vector3.up * 0.15f * grow;
                _glob.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.5f, grow);
            }
            else if (frame < a.HitOn.Start)
            {
                k = Mathf.Clamp01((frame - launch) / Mathf.Max(1f, a.HitOn.Start - launch));
                Vector3 p = Vector3.Lerp(lid, to, k);
                p.y += 4f * 2.2f * k * (1f - k);   // an arc 2.2 m above the straight line at its peak
                _glob.SetActive(true);
                _glob.transform.position = p;
                _glob.transform.localScale = Vector3.one * 0.5f;
            }
            else { _glob.SetActive(false); k = 1f; }

            // Its shadow on the ground at the landing spot, from the throw to the splash.
            if (markerMaterial == null || frame < launch) { if (_shadow != null) _shadow.Go.SetActive(false); return; }
            if (_shadow == null) _shadow = NewMarker("GlobShadow");
            if (_shadowAttack != a) { _shadowAttack = a; _shadow.Filter.sharedMesh = ShapeMeshes.Footprint(a, 0f); }
            Vector3 c = _enemy.AttackCentre;
            _shadow.Go.SetActive(true);
            _shadow.Go.transform.SetPositionAndRotation(new Vector3(c.x, c.y + FootprintY - 0.01f, c.z), Quaternion.identity);
            float sk = 0.25f + 0.55f * k;
            _shadow.Go.transform.localScale = new Vector3(sk, 1f, sk);
            Paint(_shadow, new Color(0.05f, 0.03f, 0.03f, 0.25f + 0.3f * k));
        }
    }
}
