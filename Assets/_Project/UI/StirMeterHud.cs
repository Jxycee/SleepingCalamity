// Authority: none (local, read-only). M5 greybox stir meter HUD (IMGUI, like CombatHud). Self-creates at runtime; shows only while a spawned DiveHaulSync
// exists, reading its replicated Summary (DiveHaulSync.Current). Never writes network state. Labels come from the latch flags, not value-vs-threshold
// (the replicated value is rounded). The value is DiveStir's (plan 3 task 10); mark positions come from DiveStir's thresholds.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using PrimeTween;
using UnityEngine;

namespace SleepingCalamity.UI
{
    public static class StirMeterText
    {
        public static List<string> Labels(DiveHaulSync.Summary s)
        {
            var l = new List<string>();
            if (s.ExtraEnemiesPerRoom == 1) l.Add("+1 enemy per room");
            else if (s.ExtraEnemiesPerRoom > 1) l.Add("+" + s.ExtraEnemiesPerRoom + " enemies per room");
            if (s.HunterLatched) l.Add("The rat-king hunts");
            if (s.DeepClosesLatched && s.DeepSecondsLeft > 0) l.Add("The deep closes: " + s.DeepSecondsLeft + "s");
            return l;
        }

        // Pieces are safe on pickup and go to the town stock at once: the line says how many the party has found so far (all dives).
        // The count behind that line, from the host's pieces: banked, stocked and placed pieces all count, also after a reload (people do not).
        public static int HomeCount(DivePieces pieces) => pieces == null ? 0 : pieces.FoundPieceCount;

        public static string Pieces(DiveHaulSync.Summary s) => "Pieces found: " + s.FoundPieces;

        // The local player's own bag (owner-only PlayerBagSync view): items carried, items safe in the bank this dive, and all their gold (home + this dive's).
        public static string Bag(BagView v) => "Bag: " + v.Carried.Length + " carried, " + v.BankedThisDive + " safe, " + (int)System.Math.Min((long)v.Gold + v.DiveGold, int.MaxValue) + " gold";

        // "Pieces left on this floor: N" while the floor holds a piece not found yet, "Someone is waiting on this floor" while it holds a person; null otherwise (town, nothing left).
        public static string PiecesLeftHere(DiveHaulSync.Summary s) =>
            s.CurrentFloor < 1 ? null : s.PiecesLeft > 0 ? "Pieces left on this floor: " + s.PiecesLeft : s.PeopleLeft > 0 ? "Someone is waiting on this floor" : null;

        public static float Fill(float value, float max) => max <= 0f ? 0f : Mathf.Clamp01(value / max);

        public static float[] MarkFractions()
        {
            return new[] { Fill(DiveStir.ExtraEnemyThreshold, DiveStir.Max), Fill(DiveStir.HunterThreshold, DiveStir.Max), Fill(DiveStir.DeepClosesThreshold, DiveStir.Max) };
        }
    }

    public sealed class StirMeterHud : MonoBehaviour
    {
        private DiveHaulSync _sync;
        private float _nextFind;
        private GUIStyle _label;
        private float _shownFill = -1f, _targetFill;
        private Tween _fillTween;

        public bool Visible => _sync != null && _sync.IsSpawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<StirMeterHud>() != null) return;
            var go = new GameObject("StirMeterHud");
            DontDestroyOnLoad(go);
            go.AddComponent<StirMeterHud>();
        }

        private void Update()
        {
            if (Visible || Time.unscaledTime < _nextFind) return;
            _nextFind = Time.unscaledTime + 0.5f;
            _sync = FindFirstObjectByType<DiveHaulSync>();
        }

        private void OnGUI()
        {
            if (!Visible) return;
            Texture2D white = Texture2D.whiteTexture;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white } };
            var s = _sync.Current;
            float scale = Screen.height / 1440f;
            float w = 420f * scale, h = 26f * scale, x = (Screen.width - w) * 0.5f, y = 12f * scale;
            float target = StirMeterText.Fill(s.StirValue, DiveStir.Max);
            if (_shownFill < 0f) _shownFill = _targetFill = target;
            else if (!Mathf.Approximately(target, _targetFill))
            {
                // PrimeTween smoke use: ease the bar to the new replicated value instead of snapping.
                _targetFill = target;
                if (_fillTween.isAlive) _fillTween.Stop();
                _fillTween = Tween.Custom(this, _shownFill, target, 0.25f, (self, v) => self._shownFill = v, Ease.OutQuad, useUnscaledTime: true);
            }
            float fill = _shownFill;
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, h + 8), white);
            GUI.color = Color.Lerp(new Color(0.9f, 0.8f, 0.3f), new Color(0.85f, 0.2f, 0.15f), fill);
            GUI.DrawTexture(new Rect(x, y, w * fill, h), white);
            GUI.color = Color.white;
            foreach (float f in StirMeterText.MarkFractions()) GUI.DrawTexture(new Rect(x + w * f - 1, y - 3, 2, h + 6), white);
            _label.fontSize = Mathf.Max(14, Mathf.RoundToInt(20 * scale));
            float ly = y + h + 8f;
            GUI.Label(new Rect(x, ly, w, 28 * scale), "Stir " + s.StirValue + " / " + (int)DiveStir.Max + "    " + StirMeterText.Pieces(s), _label);
            string here = StirMeterText.PiecesLeftHere(s);
            if (here != null) { ly += 26 * scale; GUI.Label(new Rect(x, ly, w, 28 * scale), here, _label); }
            PlayerBagSync bag = PlayerBagSync.Local;   // one peer per process in play; tests with several peers do not read this line
            if (bag != null && bag.IsSpawned) { ly += 26 * scale; GUI.Label(new Rect(x, ly, w, 28 * scale), StirMeterText.Bag(bag.View), _label); }
            foreach (string line in StirMeterText.Labels(s)) { ly += 26 * scale; GUI.Label(new Rect(x, ly, w, 28 * scale), line, _label); }
        }
    }
}
