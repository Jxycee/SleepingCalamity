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
        private struct FloatText { public Vector3 Pos; public string Text; public float Born; public Color Color; public bool Big; }

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
        }

        private void OnDisable()
        {
            CombatEvents.LocalHitLanded -= OnLocalHit;
            CombatEvents.NumberPopped -= OnNumber;
            CombatEvents.Cue -= OnCue;
            foreach (ArcView v in _arcs.Values) if (v.Go != null) Destroy(v.Go);
            _arcs.Clear();
        }

        private void OnLocalHit(LocalHit hit)
        {
            Burst(hit.Point, hit.Attack.IsFinisher || hit.Attack.Kind == AttackKind.Charge ? 1.3f : 0.7f, hit.Part ? new Color(1f, 0.9f, 0.2f) : Color.white);
            CombatEvents.RaiseCue(hit.Attack.IsFinisher || hit.Attack.Kind == AttackKind.Charge ? CombatCue.HitFinisher : CombatCue.HitNormal, hit.Point);
        }

        private void OnNumber(DamageNumber n)
        {
            string text = n.Heal ? "+" + n.Amount : n.Amount.ToString();
            Color c = n.Heal ? new Color(0.4f, 1f, 0.5f) : n.OnPlayer ? new Color(1f, 0.35f, 0.3f) : n.Crit ? new Color(1f, 0.85f, 0.2f) : n.WeakPart ? new Color(1f, 0.7f, 0.2f) : Color.white;
            if (n.Blocked) { text += n.OnPlayer ? " (guarded)" : " (blocked)"; c = new Color(0.7f, 0.8f, 1f); }
            _texts.Add(new FloatText { Pos = n.Position, Text = text, Born = Time.time, Color = c, Big = n.Crit });
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
            }
            if (id != null) _bus.PlayAt(id, at, pitch);
        }

        private void Burst(Vector3 at, float size, Color color)
        {
            if (sparkMaterial == null) return;
            if (_sparkRoot == null) _sparkRoot = new GameObject("Sparks").transform;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_sparkRoot, false);
            go.transform.position = at;
            go.GetComponent<Renderer>().sharedMaterial = sparkMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            _sparks.Add(new Spark { T = go.transform, Born = Time.time, Life = 0.14f, Size = size });
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
            for (int i = _texts.Count - 1; i >= 0; i--) if (now - _texts[i].Born > 1.1f) _texts.RemoveAt(i);
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
                    v.Filter.sharedMesh = a.Shape == HitShape.Circle ? ShapeMeshes.Disc(a.Range) : ShapeMeshes.Wedge(a.Range, a.HalfAngleDeg);
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColor, a.IsFinisher || a.Kind == AttackKind.Charge ? new Color(1f, 0.55f, 0.1f, 0.45f) : a.Kind == AttackKind.Spell ? new Color(1f, 0.3f, 0.1f, 0.45f) : new Color(0.9f, 0.95f, 1f, 0.3f));
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
                Vector3 sp = cam.WorldToScreenPoint(t.Pos + Vector3.up * (age * 1.2f));
                if (sp.z <= 0f) continue;
                _style.fontSize = t.Big ? 26 : 18;
                Color c = t.Color;
                c.a = Mathf.Clamp01(1.4f - age);
                var rect = new Rect(sp.x - 70f, Screen.height - sp.y - 15f, 140f, 30f);
                GUI.color = new Color(0f, 0f, 0f, c.a);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), t.Text, _style);
                GUI.color = c;
                GUI.Label(rect, t.Text, _style);
            }
            GUI.color = Color.white;
        }
    }
}
