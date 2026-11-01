// Authority: none (local). M4 greybox weapon-tree window (IMGUI, like StirMeterHud). Self-creates at runtime; B toggles it (Tab is lock-on, F1/F2/F5/F6 are
// taken). It reads the LOCAL hero's host-synced HeroCombat.TreeMirror and formats it with the pure WeaponTreeView; every choice (fit, remove, build-up,
// re-route) is only a request to the host through HeroCombat.OwnerTreeAction, which the host re-checks (WeaponTreeHost) and answers with a new snapshot.
// While open the window sets IInputService.MenuOpen (Menu context: cursor unlocked and visible, gameplay input zeroed, so a click is not an attack) and
// restores the previous value on close. It does not open over another menu.
// Keys while open (edge-triggered through the Input System, so holding a key does not repeat): 1/2/3 fit Ember / Pest-bane / Heartwood, Shift+1/2/3
// remove it, F7 build-up, F8 re-route, Esc or B close. The buttons do the same with the mouse. A key whose view-model flag is off sends nothing.
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SleepingCalamity.UI
{
    public sealed class WeaponTreeHud : MonoBehaviour
    {
        private bool _open;
        private IInputService _menuInput;   // the service whose MenuOpen this window raised
        private bool _prevMenuOpen;
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
        private static readonly Key[] Digits = { Key.Digit1, Key.Digit2, Key.Digit3 };

        private void OnDisable() { SetOpen(false, null); }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            HeroCombat hero = HeroCombat.Local;
            bool heroOk = hero != null && hero.IsSpawned;
            if (_open && !heroOk) { SetOpen(false, null); return; }
            if (kb == null) return;
            if (kb[Key.B].wasPressedThisFrame && heroOk) { SetOpen(!_open, hero); return; }
            if (!_open) return;
            if (kb[Key.Escape].wasPressedThisFrame) { SetOpen(false, hero); return; }

            WeaponTreeView v = WeaponTreeView.Build(hero.TreeMirror);
            bool shift = kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;
            for (int i = 0; i < Kinds.Length; i++)
            {
                if (!kb[Digits[i]].wasPressedThisFrame) continue;
                if (shift ? HasFitted(v, Kinds[i]) : v.CanFit) hero.OwnerTreeAction(shift ? TreeAction.Remove : TreeAction.Fit, (int)Kinds[i]);
            }
            if (kb[Key.F7].wasPressedThisFrame && v.CanBuildUp) hero.OwnerTreeAction(TreeAction.BuildUp);
            if (kb[Key.F8].wasPressedThisFrame && v.CanReroute) hero.OwnerTreeAction(TreeAction.Reroute);
        }

        private static bool HasFitted(WeaponTreeView v, AttachmentKind kind)
        {
            foreach (SlotView s in v.Slots) if (s.Filled && s.Kind == kind) return true;
            return false;
        }

        private void SetOpen(bool open, HeroCombat hero)
        {
            if (open == _open) return;
            if (open)
            {
                IInputService input = hero.Input;
                if (input != null && input.MenuOpen) return;   // another menu is up: do not stack on it
                _menuInput = input;
                if (input != null) { _prevMenuOpen = input.MenuOpen; input.MenuOpen = true; }
                _open = true;
            }
            else
            {
                if (_menuInput != null) _menuInput.MenuOpen = _prevMenuOpen;
                _menuInput = null;
                _open = false;
            }
        }

        private void OnGUI()
        {
            HeroCombat hero = HeroCombat.Local;
            if (!_open || hero == null || !hero.IsSpawned) return;

            WeaponTreeView v = WeaponTreeView.Build(hero.TreeMirror);

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
