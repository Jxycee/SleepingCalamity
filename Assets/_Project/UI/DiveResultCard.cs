// Authority: none (local, read-only). PL-2 task 2.3: the card that says what a dive brought home (IMGUI, self-spawning). The HOST writes only how the dive ended (DiveDirector.NetEnd: reason + a counter);
// the numbers are each OWNER's own: while a dive runs this peer remembers its own owner-read BagView (PlayerBagSync) and the party's found pieces (DiveHaulSync.Summary) and the town's stocked people, and
// when NetEnd's counter moves it builds its DiveResult (Core, pure) from the last of those. A peer that joins after a dive baselines on the counter it spawns with, so it never shows that dive's card.
// Shown until Interact or ShowSeconds, then the guide line takes over (LoopGuideHud hides itself while ShowingCount > 0). While it is up it claims Interact from the DiveDirector (ClaimInteract), so the dismissing press
// cannot start a dive at the trapdoor. A new dive hides it too.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.UI
{
    /// <summary>The card's state without any drawing. Public so a test drives it with a peer's own objects.</summary>
    public sealed class DiveResultModel
    {
        public const float ShowSeconds = 10f;   // placeholder

        public DiveResult Result { get; private set; }
        public List<string> Lines { get; private set; } = new List<string>();
        public bool Ready { get; private set; }       // the counter baseline is taken
        public bool Recording { get; private set; }   // this dive's last bag snapshot is held
        public bool HasBaseline => _haveFoundBase && _haveResBase;   // the found-pieces and stocked-people baselines of this dive are taken

        private uint _seen;
        private DiveResultInput _pre;
        private bool _hasPre, _hasBag, _fresh;
        // What the party already held when this peer first saw the dive running (the first dive summary and the town it had then). The haul sync is spawned lazily by the dive's start, so a
        // baseline taken "between dives" would be taken before it exists; a peer that joins mid-dive baselines on what it joins to, and counts only what is found after that.
        private bool _haveFoundBase, _haveResBase;
        private int _foundBase;
        private readonly List<string> _residentsBase = new List<string>(), _residentsPre = new List<string>();
        private float _hideAt;

        public void Reset()
        {
            Ready = false; Recording = false; _hasPre = false; _hasBag = false; _haveFoundBase = false; _haveResBase = false; _pre = default;
            Result = null; Lines = new List<string>();
        }

        // True when `v` looks like the bag an ending leaves behind (nothing carried, banked or earned, no satchel carried) right after `prev` held something. Items only leave a bag mid-dive by being
        // banked (which raises the banked count) and gold never falls, so a view that empties while the dive is still running is the end's bag arriving ahead of NetActive=false: keep the last real snapshot.
        public static bool IsPostEnd(in DiveResultInput prev, BagView v)
        {
            bool prevHeld = prev.Carried > 0 || prev.Banked > 0 || prev.DiveGold > 0 || prev.SatchelCarried;
            bool nowEmpty = v.Carried.Length == 0 && v.BankedThisDive == 0 && v.DiveGold == 0 && !v.SatchelCarried;
            return prevHeld && nowEmpty;
        }

        public void Tick(DiveDirector dir, PlayerBagSync bag, DiveHaulSync haul, TownState town, float now, bool interactPressed)
        {
            if (dir == null || !dir.IsSpawned) { Reset(); return; }
            DiveEndNet end = dir.NetEnd.Value;
            if (!Ready) { Ready = true; _seen = end.Count; }
            bool active = dir.NetActive.Value;

            if (Result != null && !_fresh && (interactPressed || now >= _hideAt || active)) { Result = null; Lines = new List<string>(); }   // a new dive hides the old card
            _fresh = false;

            if (end.Count != _seen)
            {
                _seen = end.Count;
                Show(Build((DiveEnding)end.Reason, town), now);
            }

            if (active)
            {
                DiveHaulSync.Summary s = haul != null ? haul.Current : default;
                bool running = haul != null && s.CurrentFloor > 0;   // a published dive summary: not the default value of a sync that has not published yet
                if (running && !_haveFoundBase) { _haveFoundBase = true; _foundBase = s.FoundPieces; }
                if (running && town != null && !_haveResBase) { _haveResBase = true; _residentsBase.Clear(); foreach (string r in town.StockResidents) _residentsBase.Add(r); }
                if (running)
                {
                    _pre.ClearedMask = s.ClearedMask; _pre.LastFloor = s.CurrentFloor;
                    if (_haveFoundBase) _pre.PiecesFound = System.Math.Max(0, s.FoundPieces - _foundBase);
                }
                if (bag != null)
                {
                    BagView v = bag.View;
                    if (!(_hasBag && IsPostEnd(_pre, v)))
                    {
                        _pre.Carried = v.Carried.Length; _pre.SatchelCarried = v.SatchelCarried; _pre.SatchelItems = v.SatchelItems; _pre.Banked = v.BankedThisDive; _pre.DiveGold = v.DiveGold;
                        _hasBag = true;
                    }
                }
                if (town != null) { _residentsPre.Clear(); foreach (string r in town.StockResidents) _residentsPre.Add(r); }
                _hasPre = true; Recording = _hasBag;
            }
            else
            {
                _hasPre = false; _hasBag = false; Recording = false; _pre = default; _haveFoundBase = false; _haveResBase = false;
            }
        }

        private DiveResult Build(DiveEnding ending, TownState town)
        {
            DiveResultInput input = _hasPre ? _pre : default;
            input.Ending = ending;
            var people = new List<string>();
            if (_haveResBase)
            {
                foreach (string r in _residentsPre) if (!_residentsBase.Contains(r) && !people.Contains(r)) people.Add(r);
                if (town != null) foreach (string r in town.StockResidents) if (!_residentsBase.Contains(r) && !people.Contains(r)) people.Add(r);
            }
            input.PeopleFound = people;
            return DiveResult.Build(input);
        }

        private void Show(DiveResult r, float now)
        {
            Result = r; Lines = GuideText.ResultLines(r); _hideAt = now + ShowSeconds; _fresh = true;
        }
    }

    public sealed class DiveResultCard : MonoBehaviour
    {
        // Cards showing right now in this process (the guide line rests while any is up).
        public static int ShowingCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ShowingCount = 0; }

        public DiveDirector Director;     // this peer's director; DiveDirector.Instance when null (tests set it for a client)
        public PlayerBagSync Bag;         // this peer's own bag sync; PlayerBagSync.Local when null
        public IInputService Input;       // HeroInput.ResolveDefault() when null
        public readonly DiveResultModel Model = new DiveResultModel();
        private TownSync _town;
        private float _nextFind;
        private bool _counted;
        private DiveDirector _lastDir;
        private GUIStyle _label, _title;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<DiveResultCard>() != null) return;
            var go = new GameObject("DiveResultCard");
            DontDestroyOnLoad(go);
            go.AddComponent<DiveResultCard>();
        }

        void Update()
        {
            DiveDirector dir = Director != null ? Director : DiveDirector.Instance;
            if (dir != _lastDir) { _lastDir = dir; _town = null; Model.Reset(); }   // another director (a new session): take the baselines again
            if (dir != null && dir.IsSpawned && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 0.5f;
                if (_town == null || !_town.IsSpawned) _town = GuideSources.TownOf(dir);
            }
            IInputService input = Input ?? HeroInput.ResolveDefault();
            bool interact = input != null && input.Intent.InteractPressed;
            Model.Tick(dir, Bag != null ? Bag : PlayerBagSync.Local, dir != null ? dir.HaulSync : null, _town != null && _town.IsSpawned ? _town.View : null, Time.unscaledTime, interact);
            bool showing = Model.Result != null;
            if (showing != _counted) { ShowingCount += showing ? 1 : -1; _counted = showing; DiveDirector.ClaimInteract(showing); }
        }

        void OnDestroy() { if (_counted) { ShowingCount--; _counted = false; DiveDirector.ClaimInteract(false); } }

        void OnGUI()
        {
            if (Model.Result == null || Model.Lines.Count == 0) return;
            float scale = Screen.height / 1440f;
            if (_label == null) { _label = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white }, wordWrap = true }; _title = new GUIStyle(_label) { fontStyle = FontStyle.Bold }; }
            _label.fontSize = Mathf.Max(13, Mathf.RoundToInt(24 * scale));
            _title.fontSize = Mathf.Max(16, Mathf.RoundToInt(36 * scale));
            float w = 720f * scale, pad = 14f * scale, tw = w - pad * 2f;
            IInputService input = Input ?? HeroInput.ResolveDefault();
            string key = input != null ? input.GetBindingDisplayString(InputActionNames.Interact) : "";
            string hint = "[" + (string.IsNullOrEmpty(key) ? "E" : key) + "]  Continue";
            float th = _title.CalcHeight(new GUIContent(Model.Lines[0]), tw), h = pad * 2f + th + _label.CalcHeight(new GUIContent(hint), tw);
            for (int i = 1; i < Model.Lines.Count; i++) h += _label.CalcHeight(new GUIContent(Model.Lines[i]), tw);
            float x = (Screen.width - w) * 0.5f, y = Screen.height * 0.25f;
            GUI.color = new Color(0, 0, 0, 0.75f); GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float ly = y + pad;
            GUI.Label(new Rect(x + pad, ly, tw, th), Model.Lines[0], _title); ly += th;
            for (int i = 1; i < Model.Lines.Count; i++) { float lh = _label.CalcHeight(new GUIContent(Model.Lines[i]), tw); GUI.Label(new Rect(x + pad, ly, tw, lh), Model.Lines[i], _label); ly += lh; }
            GUI.Label(new Rect(x + pad, ly, tw, _label.CalcHeight(new GUIContent(hint), tw)), hint, _label);
        }
    }
}
