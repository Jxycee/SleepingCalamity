// Authority: none (local, read-only). Plan 5 task 6: the floor card HUD (IMGUI, like StirMeterHud). Self-creates at runtime; reads the replicated CellarContentsSync state and DiveHaulSync's summary and never writes
// network state. The card (FloorCard.For) is computed once per floor change and cached, never per frame; the text comes from Core's FloorCardText so a number changes in one place. On a floor change the full
// card is centred for FullCardSeconds (placeholder), then a compact panel stays in the top-right corner (objective with live progress, one line per modifier with its reward, pieces left here).
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.UI
{
    public enum FloorCardMode { Hidden, Full, Compact }

    /// <summary>The HUD's state without any drawing: which card is cached, whether it is shown full or compact, and the lines to print. Public so a test can drive it with a fake clock.</summary>
    public sealed class FloorCardHudModel
    {
        public const float FullCardSeconds = 6f;   // placeholder

        FloorCard _card;
        uint _seed; int _floor;
        float _fullUntil;
        public int ComputeCount { get; private set; }
        public FloorCard Card => _card;
        public int Floor => _floor;
        public int PiecesLeft { get; private set; }
        public string ObjectiveLine { get; private set; } = "";
        public FloorCardMode Mode { get; private set; } = FloorCardMode.Hidden;

        /// <summary>Call every frame (cheap): recomputes the card only when (seed, floor) or the objective changed; refreshes the live objective line and the mode.</summary>
        public void Tick(CellarContentsSync contents, int piecesLeft, float now)
        {
            if (contents == null || !contents.IsSpawned) { Mode = FloorCardMode.Hidden; _card = null; _floor = 0; return; }
            CellarContentsSync.ContentsState s = contents.State;
            if (s.Floor < 1) { Mode = FloorCardMode.Hidden; _card = null; _floor = 0; return; }
            FloorObjective obj = contents.CurrentObjective;
            if (_card == null || s.Seed != _seed || s.Floor != _floor || _card.Objective != obj)
            {
                bool newFloor = _card == null || s.Seed != _seed || s.Floor != _floor;
                _card = FloorCard.For(s.Seed, s.Floor, obj);
                _seed = s.Seed; _floor = s.Floor; ComputeCount++;
                if (newFloor) _fullUntil = now + FullCardSeconds;
            }
            PiecesLeft = piecesLeft;
            string line = contents.ObjectiveLine;
            ObjectiveLine = _floor == DiveFloorTable.LastBuiltFloor && (obj == FloorObjective.TwoValves || obj == FloorObjective.HoldPump) ? FloorCardText.WithWayOut(line) : line;
            Mode = now < _fullUntil ? FloorCardMode.Full : FloorCardMode.Compact;
        }

        /// <summary>Header, objective, then each modifier's title and effect; the full card also prints the hazards' reward lines. Compact prints title + reward.</summary>
        public List<string> Lines(bool full)
        {
            var l = new List<string>();
            if (_card == null) return l;
            l.Add(_card.Name + (full ? "    Floor " + _card.Floor : ""));
            if (ObjectiveLine.Length > 0) l.Add(ObjectiveLine);
            AddModifier(l, _card.Modifiers.A, full);
            AddModifier(l, _card.Modifiers.B, full);
            if (PiecesLeft > 0) l.Add("Pieces left here: " + PiecesLeft);
            return l;
        }

        static void AddModifier(List<string> l, FloorModifier m, bool full)
        {
            if (m == FloorModifier.None) return;
            string reward = FloorCardText.RewardLine(m);
            if (full)
            {
                l.Add(FloorCardText.Title(m) + ": " + FloorCardText.EffectLine(m));
                if (reward.Length > 0) l.Add("    " + reward);
            }
            else l.Add(FloorCardText.Title(m) + (reward.Length > 0 ? " (" + reward + ")" : ""));
        }
    }

    public sealed class FloorCardHud : MonoBehaviour
    {
        public CellarContentsSync Contents;   // found in the scene when null (tests set it)
        public DiveHaulSync Haul;
        public readonly FloorCardHudModel Model = new FloorCardHudModel();
        float _nextFind;
        GUIStyle _label, _title;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<FloorCardHud>() != null) return;
            var go = new GameObject("FloorCardHud");
            DontDestroyOnLoad(go);
            go.AddComponent<FloorCardHud>();
        }

        void Update()
        {
            if ((Contents == null || !Contents.IsSpawned || Haul == null || !Haul.IsSpawned) && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 0.5f;
                if (Contents == null || !Contents.IsSpawned) Contents = FindFirstObjectByType<CellarContentsSync>();
                if (Haul == null || !Haul.IsSpawned) Haul = FindFirstObjectByType<DiveHaulSync>();
            }
            Model.Tick(Contents, Haul != null && Haul.IsSpawned ? Haul.Current.PiecesLeft : 0, Time.unscaledTime);
        }

        void OnGUI()
        {
            if (Model.Mode == FloorCardMode.Hidden) return;
            bool full = Model.Mode == FloorCardMode.Full;
            List<string> lines = Model.Lines(full);
            if (lines.Count == 0) return;
            float scale = Screen.height / 1440f;
            if (_label == null) { _label = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white }, wordWrap = true }; _title = new GUIStyle(_label) { fontStyle = FontStyle.Bold }; }
            _label.fontSize = Mathf.Max(13, Mathf.RoundToInt((full ? 26 : 18) * scale));
            _title.fontSize = Mathf.Max(15, Mathf.RoundToInt((full ? 36 : 20) * scale));
            float w = (full ? 760f : 400f) * scale, pad = 12f * scale, tw = w - pad * 2f, th = _title.CalcHeight(new GUIContent(lines[0]), tw);
            var heights = new float[lines.Count];   // wrapped lines get their real height, so long objective lines never overlap the next one
            float h = pad * 2f + th;
            for (int i = 1; i < lines.Count; i++) { heights[i] = _label.CalcHeight(new GUIContent(lines[i]), tw); h += heights[i]; }
            float topRight = DiveStatsOverlay.Shown ? 12f + 640f + 8f : 12f * scale;   // below the F4 playtest overlay when it is up (same corner)
            float x = full ? (Screen.width - w) * 0.5f : Screen.width - w - 16f * scale, y = full ? Screen.height * 0.28f : topRight;
            GUI.color = new Color(0, 0, 0, full ? 0.72f : 0.5f); GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float ly = y + pad;
            GUI.Label(new Rect(x + pad, ly, tw, th), lines[0], _title);
            ly += th;
            for (int i = 1; i < lines.Count; i++) { GUI.Label(new Rect(x + pad, ly, tw, heights[i]), lines[i], _label); ly += heights[i]; }
        }
    }
}
