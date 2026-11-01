// Authority: none (local). M4 greybox weapon-tree window (IMGUI, like StirMeterHud). Self-creates at runtime; B toggles it (Tab is lock-on, F1/F2/F5/F6 are
// taken). It reads the LOCAL hero's host-synced HeroCombat.TreeMirror and formats it with the pure WeaponTreeView; every choice (fit, remove, build-up,
// re-route) is only a request to the host through HeroCombat.OwnerTreeAction, which the host re-checks (WeaponTreeHost) and answers with a new snapshot.
// Keys while open: 1/2/3 fit Ember / Pest-bane / Heartwood, Shift+1/2/3 remove it, F7 build-up, F8 re-route (the buttons do the same with the mouse).
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.UI
{
    public sealed class WeaponTreeHud : MonoBehaviour
    {
        public const KeyCode ToggleKey = KeyCode.B;

        private bool _open;
        private GUIStyle _label, _title;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<WeaponTreeHud>() != null) return;
            var go = new GameObject("WeaponTreeHud");
            DontDestroyOnLoad(go);
            go.AddComponent<WeaponTreeHud>();
        }

        private static readonly AttachmentKind[] Kinds = { AttachmentKind.Ember, AttachmentKind.PestBane, AttachmentKind.Heartwood };
        private static readonly KeyCode[] Digits = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

        private void OnGUI()
        {
            HeroCombat hero = HeroCombat.Local;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == ToggleKey) { _open = !_open; e.Use(); }
            if (!_open || hero == null || !hero.IsSpawned) return;

            WeaponTreeView v = WeaponTreeView.Build(hero.TreeMirror);
            if (e.type == EventType.KeyDown)
            {
                for (int i = 0; i < Kinds.Length; i++)
                    if (e.keyCode == Digits[i]) hero.OwnerTreeAction(e.shift ? TreeAction.Remove : TreeAction.Fit, (int)Kinds[i]);
                if (e.keyCode == KeyCode.F7 && v.CanBuildUp) hero.OwnerTreeAction(TreeAction.BuildUp);
                if (e.keyCode == KeyCode.F8 && v.CanReroute) hero.OwnerTreeAction(TreeAction.Reroute);
            }

            Texture2D white = Texture2D.whiteTexture;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white } };
                _title = new GUIStyle(_label) { fontStyle = FontStyle.Bold };
            }
            float scale = Screen.height / 1440f;
            int fs = Mathf.Max(14, Mathf.RoundToInt(20 * scale));
            _label.fontSize = fs; _title.fontSize = Mathf.RoundToInt(fs * 1.2f);
            float line = fs * 1.5f, w = 560f * scale, x = 24f * scale, y = 80f * scale;
            float h = line * (9 + v.Slots.Count + v.Thresholds.Count * 1.4f) + 24f * scale;
            GUI.color = new Color(0, 0, 0, 0.7f); GUI.DrawTexture(new Rect(x - 10, y - 10, w + 20, h + 20), white); GUI.color = Color.white;

            GUI.Label(new Rect(x, y, w, line), v.Title + "  (B closes)", _title); y += line * 1.2f;
            GUI.Label(new Rect(x, y, w, line), v.LevelLine, _label); y += line;
            Bar(new Rect(x, y, w, 10 * scale), v.AbsFraction, new Color(0.9f, 0.8f, 0.3f), -1f); y += line * 0.6f;
            GUI.Label(new Rect(x, y, w, line), v.StatsLine, _label); y += line * 1.2f;

            GUI.Label(new Rect(x, y, w, line), "Attachments (1/2/3 fit, Shift+n remove)", _title); y += line;
            foreach (SlotView s in v.Slots) { GUI.Label(new Rect(x, y, w, line), "  [" + (s.Filled ? "x" : " ") + "] " + s.Label, _label); y += line; }
            GUI.Label(new Rect(x, y, w, line), v.AbsorbLine, _label); y += line * 1.3f;

            GUI.Label(new Rect(x, y, w, line), v.BuildUpLine, _title); y += line;
            foreach (ThresholdView t in v.Thresholds)
            {
                GUI.Label(new Rect(x, y, w, line), t.Label + (t.Met ? "  OK" : ""), _label); y += line * 0.9f;
                Bar(new Rect(x, y, w, 10 * scale), t.Fraction, t.Met ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.85f, 0.5f, 0.2f), t.Marker); y += line * 0.5f;
            }
            GUI.enabled = v.CanBuildUp;
            if (GUI.Button(new Rect(x, y, w * 0.48f, line * 1.2f), "Build up (F7)")) hero.OwnerTreeAction(TreeAction.BuildUp);
            GUI.enabled = true; y += line * 1.4f;

            GUI.Label(new Rect(x, y, w, line), v.RerouteLine, _label); y += line;
            GUI.enabled = v.CanReroute;
            if (GUI.Button(new Rect(x, y, w * 0.48f, line * 1.2f), "Re-route (F8)")) hero.OwnerTreeAction(TreeAction.Reroute);
            GUI.enabled = true;
        }

        private static void Bar(Rect r, float fill, Color c, float marker)
        {
            Texture2D white = Texture2D.whiteTexture;
            GUI.color = new Color(1, 1, 1, 0.15f); GUI.DrawTexture(r, white);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), white);
            if (marker >= 0f) { GUI.color = Color.white; GUI.DrawTexture(new Rect(r.x + r.width * marker - 1, r.y - 2, 2, r.height + 4), white); }
            GUI.color = Color.white;
        }
    }
}
