// Authority: none (local, cosmetic, every peer). The red hurt pulse while a hero's post-hit grace runs (combat-timings recommendation 2): the hero's renderers
// tint toward red by HeroCombat.HurtPulse (a sine half-cycle over the window) and flicker for the first 0.4 s. It reads the grace clock every peer keeps
// (the owner's gameplay window; the others' cosmetic one started by HurtClientRpc), so everyone sees the same thing. The reduce-flashes setting goes
// through the shared FeedbackDirector.Flash limiter like the enemy flashes: capped at half brightness, no flicker, at most 3 new flashes a second.
// Added to the hero at runtime by CombatFx on the first hurt (no prefab edit). Uses a MaterialPropertyBlock on _BaseColor and puts back whatever block
// each renderer had when the pulse ends.
using System.Collections.Generic;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(130)]
    public sealed class HeroHurtPulse : MonoBehaviour
    {
        public const float MaxRed = 0.65f;   // how far toward red the pulse's peak goes

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color Red = new Color(1f, 0.12f, 0.1f);

        private HeroCombat _hero;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock[] _saved;
        private MaterialPropertyBlock _block;
        private bool _applied, _flashAllowed, _wasActive;

        public bool Applied => _applied;

        private void Awake()
        {
            _hero = GetComponent<HeroCombat>();
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (_hero == null) return;
            bool active = _hero.HurtGraceActive;
            if (active && !_wasActive) _flashAllowed = FeedbackDirector.Flash.Allow(Time.unscaledTime);
            _wasActive = active;
            float pulse = active && _flashAllowed ? _hero.HurtPulse : 0f;
            float strength = pulse * MaxRed;
            if (strength > 0f && _hero.HurtFlickering && !FeedbackDirector.Flash.Reduce) strength = _hero.HurtFlickerOn ? Mathf.Max(strength, 0.5f) : strength * 0.25f;
            strength = FeedbackDirector.Flash.Brightness(strength);
            if (strength <= 0.001f) { Restore(); return; }
            Capture();
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                _block.Clear();
                if (_saved[i] != null) r.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, Color.Lerp(_baseColors[i], Red, strength));
                r.SetPropertyBlock(_block);
            }
            _applied = true;
        }

        private void Capture()
        {
            if (_renderers != null) return;
            Transform root = transform.Find("Visual");
            var list = new List<Renderer>();
            (root != null ? root : transform).GetComponentsInChildren(false, list);
            list.RemoveAll(r => r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
            _renderers = list.ToArray();
            _baseColors = new Color[_renderers.Length];
            _saved = new MaterialPropertyBlock[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                Material m = r.sharedMaterial;
                _baseColors[i] = m != null && m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.white;
                if (r.HasPropertyBlock())
                {
                    _saved[i] = new MaterialPropertyBlock();
                    r.GetPropertyBlock(_saved[i]);
                    if (_saved[i].HasColor(BaseColor)) _baseColors[i] = _saved[i].GetColor(BaseColor);
                }
            }
        }

        private void Restore()
        {
            if (!_applied) return;
            _applied = false;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].SetPropertyBlock(_saved[i]);   // null clears the pulse's block
            }
        }

        private void OnDisable() => Restore();
    }
}
