// Authority: none (local). The M3 debug HUD (IMGUI): the local hero's HP, combo count, the weapon and its ABS bar, spell pips, the targeted or nearest enemy's
// HP and revenge gauge, the party's HP, the wave, the downed overlay, the latest ABS gains and tech pops, and the controls. Reads replicated state and the owner's
// growth mirror only.
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class CombatHud : MonoBehaviour
    {
        private struct Line { public string Text; public float Born; public bool Pop; }

        private readonly List<Line> _log = new List<Line>();
        private Texture2D _white;
        private GUIStyle _label, _big, _wave, _hpText;
        private bool _help = true;
        private float _hitMarkerUntil, _hurtAt = -10f;
        private bool _hitMarkerHeavy;
        private Vector3 _hurtDir;   // world, flat: from the local hero toward what hurt it

        public const float HurtFlashSeconds = 0.55f, HitMarkerSeconds = 0.18f;
        private static readonly float[] MarkerAngles = { 45f, -45f };

        private void OnEnable()
        {
            CombatEvents.AbsReported += OnAbs;
            CombatEvents.LocalHitLanded += OnLocalHit;
            CombatEvents.HurtFrom += OnHurtFrom;
        }

        private void OnDisable()
        {
            CombatEvents.AbsReported -= OnAbs;
            CombatEvents.LocalHitLanded -= OnLocalHit;
            CombatEvents.HurtFrom -= OnHurtFrom;
        }

        private void OnLocalHit(LocalHit hit)
        {
            _hitMarkerUntil = Time.time + HitMarkerSeconds;
            _hitMarkerHeavy = hit.Attack != null && (hit.Attack.IsFinisher || hit.Attack.Kind == Core.Combat.AttackKind.Charge);
        }

        private void OnHurtFrom(Vector3 toward)
        {
            _hurtAt = Time.time;
            toward.y = 0f;
            _hurtDir = toward;
        }

        private void OnAbs(AbsReport r)
        {
            string name = WeaponCatalog.Default.TryGet(r.WeaponKey, out WeaponDefinition d) ? d.DisplayName : r.WeaponKey;
            string text = "+" + r.Abs.ToString("0.0") + " ABS  " + name + "  (" + r.Why + ")";
            if (r.LevelsGained > 0) text += "  LEVEL UP x" + r.LevelsGained;
            _log.Add(new Line { Text = text, Born = Time.time, Pop = r.TechPop });
            if (_log.Count > 8) _log.RemoveAt(0);
        }

        private void Fill(Rect r, float frac, Color c)
        {
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, _white);
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, Mathf.Max(0f, (r.width - 4f) * Mathf.Clamp01(frac)), r.height - 4f), _white);
            GUI.color = Color.white;
        }

        private void OnGUI()
        {
            HeroCombat me = HeroCombat.Local;
            if (me == null || !me.IsSpawned) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _wave = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _hpText = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1) _help = !_help;

            HeroVitals v = me.GetComponent<HeroVitals>();
            float x = 16f, y = 16f;

            DrawHurtFlash();

            // Me: HP (a big bar: green, then amber, then a pulsing red), combo, weapon, ABS bar, pips.
            float hpFrac = v.MaxHp > 0 ? v.Hp / (float)v.MaxHp : 0f;
            Color hpColor = hpFrac > 0.6f ? new Color(0.25f, 0.85f, 0.35f) : hpFrac > 0.3f ? new Color(0.95f, 0.75f, 0.15f) : Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.45f, 0.35f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
            Fill(new Rect(x, y, 380f, 36f), hpFrac, hpColor);
            DrawOutlined(new Rect(x + 10, y, 360f, 36f), "HP " + v.Hp + " / " + v.MaxHp + (v.IsUp ? "" : "  DOWN"), _hpText, Color.white);
            y += 44f;
            HeroCombat.GrowthMirror g = me.Mirror;
            string weaponName = WeaponCatalog.Default.TryGet(g.WeaponKey, out WeaponDefinition def) ? def.DisplayName : g.WeaponKey;
            GUI.Label(new Rect(x, y, 420f, 22f), "<b>" + weaponName + "</b>  Lv " + g.Level + "  ATK " + g.Atk.ToString("0") + (g.Fire > 0 ? "  Fire " + g.Fire : "") + (g.Pest > 0 ? "  Pest " + g.Pest : ""), _label);
            y += 22f;
            Fill(new Rect(x, y, 380f, 14f), g.LevelCost > 0 ? g.Abs / g.LevelCost : 0f, new Color(0.35f, 0.6f, 1f));
            GUI.Label(new Rect(x + 6, y - 3, 270f, 20f), "ABS " + g.Abs.ToString("0.0") + " / " + g.LevelCost.ToString("0"), _label);
            y += 20f;
            if (me.Family == WeaponFamily.Staff)
            {
                GUI.Label(new Rect(x, y, 300f, 22f), "Spell pips  " + new string('O', Mathf.Max(0, g.Pips)) + new string('.', Mathf.Max(0, g.PipCap - g.Pips)) + "   (RMB casts)", _label);
                y += 22f;
            }
            GUI.Label(new Rect(x, y, 300f, 28f), "Combo <size=24><b>" + me.ComboCount + "</b></size>", _label);
            y += 32f;

            // Party.
            foreach (HeroCombat other in HeroCombat.All)
            {
                if (other == null || other == me || !other.IsSpawned) continue;
                HeroVitals ov = other.GetComponent<HeroVitals>();
                if (ov == null) continue;
                Fill(new Rect(x, y, 200f, 14f), ov.Hp / (float)ov.MaxHp, ov.IsUp ? new Color(0.3f, 0.7f, 0.4f) : new Color(0.8f, 0.3f, 0.2f));
                GUI.Label(new Rect(x + 4, y - 3, 220f, 20f), "Ally " + other.OwnerClientId + "  " + ov.Hp + (ov.IsDowned ? "  DOWN " + ov.DownedLeft.ToString("0") + "s (hold E to revive)" : ""), _label);
                y += 18f;
            }

            // Target enemy: the lock, else the soft target, else the nearest. HP, tier, rage, revenge.
            Enemy e = PickEnemy(me);
            if (e != null)
            {
                float cx = Screen.width * 0.5f - 200f;
                Fill(new Rect(cx, 64f, 400f, 20f), e.NetMaxHp.Value > 0 ? e.NetHp.Value / (float)e.NetMaxHp.Value : 0f, e.Def.IsElite ? new Color(0.9f, 0.5f, 0.2f) : new Color(0.85f, 0.25f, 0.25f));
                GUI.Label(new Rect(cx + 6, 64f, 390f, 20f), e.Def.Name + (e.Tier == 2 ? " (tier 2)" : "") + "  " + e.NetHp.Value + " / " + e.NetMaxHp.Value + (e.NetRage.Value == 2 ? "  RAGE" : e.NetRage.Value == 1 ? "  !" : ""), _label);
                if (e.Def.IsElite)
                {
                    Fill(new Rect(cx, 88f, 400f, 12f), e.NetRevengePercent.Value / 100f, new Color(0.7f, 0.3f, 0.9f));
                    GUI.Label(new Rect(cx + 6, 84f, 390f, 18f), "Revenge " + e.NetRevengePercent.Value + "%", _label);
                }
            }

            // Wave.
            string wave = ArenaStatus.State == WaveState.Countdown ? "Wave " + ArenaStatus.Wave + " - incoming..." :
                          ArenaStatus.State == WaveState.Active ? "Wave " + ArenaStatus.Wave + (ArenaStatus.Tier == 2 ? " (tier 2)" : "") + " - " + ArenaStatus.EnemiesLeft + " left" :
                          ArenaStatus.State == WaveState.Cleared ? "Wave " + ArenaStatus.Wave + " cleared!" :
                          ArenaStatus.State == WaveState.Wiped ? "PARTY DOWN - restarting the wave" : "";
            if (wave.Length > 0) DrawOutlined(new Rect(Screen.width * 0.5f - 300f, 14f, 600f, 44f), wave, _wave, Color.white);
            DrawHitMarker();

            // ABS log.
            float ly = Screen.height - 230f;
            float now = Time.time;
            foreach (Line l in _log)
            {
                float age = now - l.Born;
                if (age > 7f) continue;
                GUI.color = new Color(l.Pop ? 1f : 0.85f, l.Pop ? 0.85f : 0.95f, l.Pop ? 0.2f : 1f, Mathf.Clamp01(7.5f - age));
                GUI.Label(new Rect(16f, ly, 600f, 22f), (l.Pop ? "TECH!  " : "") + l.Text, _label);
                ly += 20f;
            }
            GUI.color = Color.white;

            if (!v.IsUp)
            {
                GUI.color = new Color(1f, 0.3f, 0.2f);
                GUI.Label(new Rect(0f, Screen.height * 0.35f, Screen.width, 50f), v.IsDowned ? "DOWN  " + v.DownedLeft.ToString("0") + " s" : "OUT", _big);
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(Screen.width - 520f, Screen.height - (_help ? 150f : 26f), 510f, 150f), _help
                ? "<b>Controls</b> (F1 hides)\nLMB attack x2 + finisher; hold LMB 0.6 s, release = charge\nLeft Ctrl dodge (i-frames)  Tab lock-on (Q/R switch)  Shift sprint  Space jump (air combo)\nRMB: dagger guard (tap at the hit = parry), bow aim + LMB shoots, staff spell\nF5 swap heirloom   F6 skip wave   hold E near a downed ally = revive"
                : "F1 controls", _label);
        }

        // Text with a dark outline so it reads over any floor.
        private static void DrawOutlined(Rect r, string text, GUIStyle style, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            for (int dx = -2; dx <= 2; dx += 4)
                for (int dy = -2; dy <= 2; dy += 4)
                    GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        // A small white X in the middle of the screen for a moment when the local hero's hit lands (gold and larger on a heavy one).
        private void DrawHitMarker()
        {
            float left = _hitMarkerUntil - Time.time;
            if (left <= 0f) return;
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            float size = _hitMarkerHeavy ? 44f : 30f, thick = _hitMarkerHeavy ? 6f : 4f;
            Color c = _hitMarkerHeavy ? new Color(1f, 0.75f, 0.15f) : Color.white;
            c.a = Mathf.Clamp01(left / HitMarkerSeconds * 1.5f);
            GUI.color = c;
            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Matrix4x4 saved = GUI.matrix;
            foreach (float angle in MarkerAngles)
            {
                GUIUtility.RotateAroundPivot(angle, centre);
                GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - thick * 0.5f, size, thick), _white);
                GUI.matrix = saved;
            }
            GUI.color = Color.white;
        }

        // The red edge flash on the side the hit came from (relative to where the camera looks), fading over HurtFlashSeconds.
        private void DrawHurtFlash()
        {
            float age = Time.time - _hurtAt;
            if (age < 0f || age > HurtFlashSeconds) return;
            if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            float fade = 1f - age / HurtFlashSeconds;
            // The direction in screen terms: 0 deg = up the screen (what the camera looks at), 90 = right.
            Camera cam = Camera.main;
            float camYaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            float rel = _hurtDir.sqrMagnitude > 1e-4f ? Mathf.DeltaAngle(camYaw, Mathf.Atan2(_hurtDir.x, _hurtDir.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad : float.NaN;
            float top = float.IsNaN(rel) ? 1f : Mathf.Max(0f, Mathf.Cos(rel)), right = float.IsNaN(rel) ? 1f : Mathf.Max(0f, Mathf.Sin(rel));
            float bottom = float.IsNaN(rel) ? 1f : Mathf.Max(0f, -Mathf.Cos(rel)), left = float.IsNaN(rel) ? 1f : Mathf.Max(0f, -Mathf.Sin(rel));
            float W = Screen.width, H = Screen.height, depth = Mathf.Min(W, H) * 0.14f;
            const int steps = 8;
            // A faint red wash on the whole screen, then eight stacked bands per edge (strongest at the very edge) weighted by the direction.
            GUI.color = new Color(0.8f, 0f, 0f, 0.10f * fade);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), _white);
            for (int i = 0; i < steps; i++)
            {
                float k = 1f - i / (float)steps;
                float a = 0.5f * k * fade;
                float d0 = depth * i / steps, d1 = depth / steps + 1f;
                GUI.color = new Color(0.9f, 0.05f, 0.03f, a * top); GUI.DrawTexture(new Rect(0f, d0, W, d1), _white);
                GUI.color = new Color(0.9f, 0.05f, 0.03f, a * bottom); GUI.DrawTexture(new Rect(0f, H - d0 - d1, W, d1), _white);
                GUI.color = new Color(0.9f, 0.05f, 0.03f, a * left); GUI.DrawTexture(new Rect(d0, 0f, d1, H), _white);
                GUI.color = new Color(0.9f, 0.05f, 0.03f, a * right); GUI.DrawTexture(new Rect(W - d0 - d1, 0f, d1, H), _white);
            }
            GUI.color = Color.white;
        }

        private static Enemy PickEnemy(HeroCombat me)
        {
            TargetLockController tl = me.GetComponent<TargetLockController>();
            if (tl != null)
            {
                Targetable t = tl.Locked != null ? tl.Locked : tl.Soft;
                if (t != null)
                {
                    Enemy e = t.GetComponent<Enemy>();
                    if (e != null && e.Alive) return e;
                }
            }
            Enemy best = null;
            float bestSq = 400f;
            foreach (Enemy e in Enemy.All)
            {
                if (e == null || !e.IsSpawned || !e.Alive || e.NetworkManager != me.NetworkManager) continue;
                float sq = (e.transform.position - me.transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }
    }
}
