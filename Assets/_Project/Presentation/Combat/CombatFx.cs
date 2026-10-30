// Authority: none (local, cosmetic). The arena's feedback layer, driven by the CombatEvents the Gameplay code raises: the hit spark, floating damage numbers,
// the swing footprint (a translucent wedge or disc while a hitbox is live, for every hero), and the combat sounds through the AudioBus (placeholder procedural
// cues, CombatSfx). The camera kick is applied by HeroCameraRig. Hitstop is not here: it slows the attacker's own attack clock (AttackMachine.Freeze) and
// never touches Time.timeScale, so the host simulation and other players never stutter.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class CombatFx : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material sparkMaterial, arcMaterial;

        private struct Spark { public Transform T; public float Born, Life, Size; }
        // Damage numbers (readability pass): normal 26 pt, weak point 32, finisher/charge 38, crit 46, rising for 1.6 s (was 18/26 pt for 1.1 s).
        private struct FloatText { public Vector3 Pos; public string Text; public float Born; public Color Color; public int Size; }
        private struct Arrow { public Transform T; public Vector3 From, To; public float Born, Life; }
        public const float NumberSeconds = 1.6f, ArrowSpeed = 70f;
        private readonly List<Arrow> _arrows = new List<Arrow>();

        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<FloatText> _texts = new List<FloatText>();
        private readonly Dictionary<HeroCombat, ArcView> _arcs = new Dictionary<HeroCombat, ArcView>();
        private readonly List<HeroCombat> _gone = new List<HeroCombat>();
        private Transform _sparkRoot;
        private GUIStyle _style;
        private AudioBus _bus;

        private sealed class ArcView
        {
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public AttackData Shown;
            public int Look;   // 0 whiff / remote, 1 connected
        }

        public void Configure(Material spark, Material arc)
        {
            sparkMaterial = spark;
            arcMaterial = arc;
        }

        private void OnEnable()
        {
            CombatEvents.LocalHitLanded += OnLocalHit;
            CombatEvents.NumberPopped += OnNumber;
            CombatEvents.Cue += OnCue;
            CombatEvents.ArrowFired += OnArrow;
            CombatEvents.HeroHurt += OnHeroHurt;
        }

        private void OnDisable()
        {
            CombatEvents.LocalHitLanded -= OnLocalHit;
            CombatEvents.NumberPopped -= OnNumber;
            CombatEvents.Cue -= OnCue;
            CombatEvents.ArrowFired -= OnArrow;
            CombatEvents.HeroHurt -= OnHeroHurt;
            foreach (Arrow ar in _arrows) if (ar.T != null) Destroy(ar.T.gameObject);
            _arrows.Clear();
            foreach (ArcView v in _arcs.Values) if (v.Go != null) Destroy(v.Go);
            _arcs.Clear();
        }

        private void OnLocalHit(LocalHit hit)
        {
            // A bigger, longer spark than before (0.7 -> 1.1 normal, 1.3 -> 2.0 heavy; 0.14 s -> 0.22 s): the contact has to be unmistakable.
            Burst(hit.Point, hit.Attack.IsFinisher || hit.Attack.Kind == AttackKind.Charge ? 2.0f : 1.1f, hit.Part ? new Color(1f, 0.9f, 0.2f) : Color.white);
            CombatEvents.RaiseCue(hit.Attack.IsFinisher || hit.Attack.Kind == AttackKind.Charge ? CombatCue.HitFinisher : CombatCue.HitNormal, hit.Point);
        }

        private void OnNumber(DamageNumber n)
        {
            string text = n.Heal ? "+" + n.Amount : n.Amount.ToString();
            int size = n.Crit ? 46 : n.Heavy ? 38 : n.WeakPart ? 32 : n.OnPlayer ? 30 : 26;
            Color c = n.Heal ? new Color(0.4f, 1f, 0.5f) : n.OnPlayer ? new Color(1f, 0.35f, 0.3f) : n.Crit ? new Color(1f, 0.9f, 0.15f) : n.Heavy ? new Color(1f, 0.55f, 0.1f) : n.WeakPart ? new Color(1f, 0.75f, 0.25f) : Color.white;
            if (n.Crit) text += "!";
            if (n.Blocked) { text += n.OnPlayer ? " (guarded)" : " (blocked)"; c = new Color(0.7f, 0.8f, 1f); size = 22; }
            _texts.Add(new FloatText { Pos = n.Position, Text = text, Born = Time.time, Color = c, Size = size });
            if (_texts.Count > 40) _texts.RemoveAt(0);
        }

        private void OnCue(CombatCue cue, Vector3 at)
        {
            if (_bus == null)
            {
                ServiceRegistry app = ServiceRoots.App;
                if (app == null || !app.TryGet(out _bus)) return;
            }
            string id = null;
            float pitch = 1f;
            switch (cue)
            {
                case CombatCue.Swing: id = CombatSfx.Whoosh; break;
                case CombatCue.HitNormal: id = CombatSfx.Hit; break;
                case CombatCue.HitFinisher: id = CombatSfx.HitHeavy; break;
                case CombatCue.HitCrit: id = CombatSfx.HitCrit; break;
                case CombatCue.Blocked: id = CombatSfx.Block; break;
                case CombatCue.Guarded: id = CombatSfx.Block; pitch = 0.8f; break;
                case CombatCue.Parry: id = CombatSfx.Parry; break;
                case CombatCue.Dodge: id = CombatSfx.Dodge; break;
                case CombatCue.PlayerHurt: id = CombatSfx.Hurt; break;
                case CombatCue.EnemyTelegraph: id = CombatSfx.Telegraph; break;
                case CombatCue.EnemySlam: id = CombatSfx.Slam; break;
                case CombatCue.EnemyDie: id = CombatSfx.Die; break;
                case CombatCue.Heal: id = CombatSfx.Heal; break;
                case CombatCue.Pop: id = CombatSfx.Pop; break;
                case CombatCue.Shot: id = CombatSfx.Shot; break;
                case CombatCue.Spell: id = CombatSfx.Spell; break;
                case CombatCue.Whiff: id = CombatSfx.Whiff; break;
            }
            if (id != null) _bus.PlayAt(id, at, pitch);
        }

        private void Burst(Vector3 at, float size, Color color)
        {
            if (sparkMaterial == null) return;
            if (_sparkRoot == null) _sparkRoot = new GameObject("Sparks").transform;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(go.GetComponent<Collider>());   // not Destroy: a collider that lives to the end of the frame can shove a character controller
            go.transform.SetParent(_sparkRoot, false);
            go.transform.position = at;
            go.GetComponent<Renderer>().sharedMaterial = sparkMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            _sparks.Add(new Spark { T = go.transform, Born = Time.time, Life = 0.22f, Size = size });
        }

        // A thin bright arrow that flies from the bow to where the shot ended, every shot (hit or miss), so "did it fire?" is never in doubt.
        private void OnArrow(Vector3 from, Vector3 to, bool hit)
        {
            if (sparkMaterial == null) return;
            if (_sparkRoot == null) _sparkRoot = new GameObject("Sparks").transform;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(_sparkRoot, false);
            go.transform.localScale = new Vector3(0.06f, 0.06f, 0.9f);
            go.GetComponent<Renderer>().sharedMaterial = sparkMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, hit ? new Color(1f, 0.95f, 0.5f) : new Color(0.85f, 0.9f, 1f));
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            float life = Mathf.Max(0.05f, Vector3.Distance(from, to) / ArrowSpeed);
            go.transform.position = from;
            if ((to - from).sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(to - from);
            _arrows.Add(new Arrow { T = go.transform, From = from, To = to, Born = Time.time, Life = life });
            if (_arrows.Count > 24) { Destroy(_arrows[0].T.gameObject); _arrows.RemoveAt(0); }
        }

        // A hurt hero flinches (the component is added to the hero the first time, so the hero prefab needs no edit).
        private void OnHeroHurt(HeroCombat hero, bool guarded)
        {
            if (hero == null || guarded) return;
            HeroFlinch f = hero.GetComponent<HeroFlinch>();
            if (f == null) f = hero.gameObject.AddComponent<HeroFlinch>();
            f.Trigger();
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                Spark s = _sparks[i];
                float k = (now - s.Born) / s.Life;
                if (s.T == null || k >= 1f) { if (s.T != null) Destroy(s.T.gameObject); _sparks.RemoveAt(i); continue; }
                s.T.localScale = Vector3.one * (s.Size * Mathf.Lerp(0.25f, 1f, k) * (1f - 0.5f * k));
            }
            for (int i = _texts.Count - 1; i >= 0; i--) if (now - _texts[i].Born > NumberSeconds) _texts.RemoveAt(i);
            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                Arrow ar = _arrows[i];
                float k = (now - ar.Born) / ar.Life;
                if (ar.T == null || k >= 1f) { if (ar.T != null) Destroy(ar.T.gameObject); _arrows.RemoveAt(i); continue; }
                ar.T.position = Vector3.Lerp(ar.From, ar.To, k);
            }
            UpdateArcs();
        }

        // A translucent footprint while a hitbox is live: wedge or disc, at the attacker, along its facing. Rays have none.
        private void UpdateArcs()
        {
            IReadOnlyList<HeroCombat> heroes = HeroCombat.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroCombat h = heroes[i];
                if (h == null || !h.IsSpawned) continue;
                AttackData a = h.HitboxOn ? h.CurrentAttack : null;
                if (!_arcs.TryGetValue(h, out ArcView v))
                {
                    if (a == null || a.Shape == HitShape.Ray || arcMaterial == null) continue;
                    v = new ArcView { Go = new GameObject("SwingArc") };
                    v.Filter = v.Go.AddComponent<MeshFilter>();
                    v.Renderer = v.Go.AddComponent<MeshRenderer>();
                    v.Renderer.sharedMaterial = arcMaterial;
                    v.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    v.Renderer.receiveShadows = false;
                    _arcs[h] = v;
                }
                bool show = a != null && a.Shape != HitShape.Ray;
                v.Go.SetActive(show);
                if (!show) continue;
                if (v.Shown != a)
                {
                    v.Shown = a;
                    v.Look = -1;
                    v.Filter.sharedMesh = a.Shape == HitShape.Circle ? ShapeMeshes.Disc(a.Range) : ShapeMeshes.Wedge(a.Range, a.HalfAngleDeg);
                }
                // A swing that has landed a hit lights up warm and strong; one that has hit nothing (yet) stays a dim cool smudge. (Remote heroes: always dim.)
                int look = h.SwingConnected ? 1 : 0;
                if (v.Look != look)
                {
                    v.Look = look;
                    var block = new MaterialPropertyBlock();
                    bool heavy = a.IsFinisher || a.Kind == AttackKind.Charge;
                    Color c = look == 1 ? (heavy ? new Color(1f, 0.5f, 0.08f, 0.6f) : new Color(1f, 0.85f, 0.35f, 0.5f))
                                        : (a.Kind == AttackKind.Spell ? new Color(1f, 0.3f, 0.1f, 0.35f) : new Color(0.75f, 0.85f, 1f, 0.14f));
                    block.SetColor(BaseColor, c);
                    v.Renderer.SetPropertyBlock(block);
                }
                Vector3 p = h.transform.position;
                v.Go.transform.SetPositionAndRotation(new Vector3(p.x, p.y + 0.06f, p.z), Quaternion.Euler(0f, h.transform.eulerAngles.y, 0f));
            }
            _gone.Clear();
            foreach (KeyValuePair<HeroCombat, ArcView> kv in _arcs) if (kv.Key == null) _gone.Add(kv.Key);
            foreach (HeroCombat g in _gone) { if (_arcs[g].Go != null) Destroy(_arcs[g].Go); _arcs.Remove(g); }
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null || _texts.Count == 0) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            float now = Time.time;
            foreach (FloatText t in _texts)
            {
                float age = now - t.Born;
                Vector3 sp = cam.WorldToScreenPoint(t.Pos + Vector3.up * (age * 0.9f));
                if (sp.z <= 0f) continue;
                float pop = 1f + 0.5f * Mathf.Clamp01(1f - age / 0.12f);   // a quick pop on appearing
                _style.fontSize = Mathf.RoundToInt(t.Size * pop);
                Color c = t.Color;
                c.a = Mathf.Clamp01((NumberSeconds - age) * 2.5f);
                float w = 220f, hgt = t.Size * 1.6f;
                var rect = new Rect(sp.x - w * 0.5f, Screen.height - sp.y - hgt * 0.5f, w, hgt);
                GUI.color = new Color(0f, 0f, 0f, c.a);
                for (int dx = -2; dx <= 2; dx += 4)
                    for (int dy = -2; dy <= 2; dy += 4)
                        GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), t.Text, _style);   // a dark outline so it reads on any floor
                GUI.color = c;
                GUI.Label(rect, t.Text, _style);
            }
            GUI.color = Color.white;
        }
    }
}
