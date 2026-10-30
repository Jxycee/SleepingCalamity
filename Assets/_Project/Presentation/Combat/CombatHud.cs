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
        private GUIStyle _label, _big;
        private bool _help = true;

        private void OnEnable() { CombatEvents.AbsReported += OnAbs; }
        private void OnDisable() { CombatEvents.AbsReported -= OnAbs; }

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
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1) _help = !_help;

            HeroVitals v = me.GetComponent<HeroVitals>();
            float x = 16f, y = 16f;

            // Me: HP, combo, weapon, ABS bar, pips.
            Fill(new Rect(x, y, 280f, 22f), v.Hp / (float)v.MaxHp, new Color(0.25f, 0.8f, 0.35f));
            GUI.Label(new Rect(x + 6, y, 270f, 22f), "HP " + v.Hp + " / " + v.MaxHp + (v.IsUp ? "" : "  DOWN"), _label);
            y += 26f;
            HeroCombat.GrowthMirror g = me.Mirror;
            string weaponName = WeaponCatalog.Default.TryGet(g.WeaponKey, out WeaponDefinition def) ? def.DisplayName : g.WeaponKey;
            GUI.Label(new Rect(x, y, 420f, 22f), "<b>" + weaponName + "</b>  Lv " + g.Level + "  ATK " + g.Atk.ToString("0") + (g.Fire > 0 ? "  Fire " + g.Fire : "") + (g.Pest > 0 ? "  Pest " + g.Pest : ""), _label);
            y += 22f;
            Fill(new Rect(x, y, 280f, 14f), g.LevelCost > 0 ? g.Abs / g.LevelCost : 0f, new Color(0.35f, 0.6f, 1f));
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
                Fill(new Rect(cx, 16f, 400f, 20f), e.NetMaxHp.Value > 0 ? e.NetHp.Value / (float)e.NetMaxHp.Value : 0f, e.Def.IsElite ? new Color(0.9f, 0.5f, 0.2f) : new Color(0.85f, 0.25f, 0.25f));
                GUI.Label(new Rect(cx + 6, 16f, 390f, 20f), e.Def.Name + (e.Tier == 2 ? " (tier 2)" : "") + "  " + e.NetHp.Value + " / " + e.NetMaxHp.Value + (e.NetRage.Value == 2 ? "  RAGE" : e.NetRage.Value == 1 ? "  !" : ""), _label);
                if (e.Def.IsElite)
                {
                    Fill(new Rect(cx, 40f, 400f, 12f), e.NetRevengePercent.Value / 100f, new Color(0.7f, 0.3f, 0.9f));
                    GUI.Label(new Rect(cx + 6, 36f, 390f, 18f), "Revenge " + e.NetRevengePercent.Value + "%", _label);
                }
            }

            // Wave.
            string wave = ArenaStatus.State == WaveState.Countdown ? "Wave " + ArenaStatus.Wave + " incoming..." :
                          ArenaStatus.State == WaveState.Active ? "Wave " + ArenaStatus.Wave + (ArenaStatus.Tier == 2 ? " (tier 2)" : "") + "   enemies left " + ArenaStatus.EnemiesLeft :
                          ArenaStatus.State == WaveState.Cleared ? "Wave " + ArenaStatus.Wave + " cleared" :
                          ArenaStatus.State == WaveState.Wiped ? "PARTY DOWN - restarting the wave" : "";
            GUI.Label(new Rect(Screen.width - 420f, 16f, 400f, 24f), "<b>" + wave + "</b>", _label);

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
