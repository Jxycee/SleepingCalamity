// Authority: none (local, read-only). PL-2 task 2.2: the one "what to do next" line (IMGUI, self-spawning like FloorCardHud) and the two light-column beacons in Hollowmere (at the trapdoor and at the
// build point; HollowmereHub builds them dark, this switches the one the step points at). Every peer works the step out for itself from state it already holds: the town through TownSync.View and the
// dive through DiveHaulSync.Current / DiveDirector.NetActive, by the pure LoopGuide in Core; nothing is written and nothing new is replicated. Hidden while placement mode is up or a dive result card is showing.
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.UI
{
    /// <summary>Finds what a peer's HUDs read, per NetworkManager (several peers share one process in tests).</summary>
    public static class GuideSources
    {
        // The director's own TownSync (the host spawned it); a client's director never sets it, so the client finds the copy NGO spawned in its own manager.
        public static TownSync TownOf(DiveDirector dir)
        {
            if (dir == null) return null;
            if (dir.TownSync != null) return dir.TownSync;
            foreach (TownSync t in Object.FindObjectsByType<TownSync>(FindObjectsSortMode.None))
                if (t.IsSpawned && t.NetworkManager == dir.NetworkManager) return t;
            return null;
        }
    }

    /// <summary>The guide's state without any drawing, so a test can feed it a replicated peer's numbers.</summary>
    public sealed class LoopGuideModel
    {
        public GuideStep Step { get; private set; }
        public string Text { get; private set; } = "";
        public bool Visible { get; private set; }
        public bool StairBeacon { get; private set; }
        public bool BuildBeacon { get; private set; }

        /// <summary>Call every frame. <paramref name="suppressed"/>: placement mode or a result card has the screen, so the line and the beacons rest.</summary>
        public void Tick(TownState town, bool inDive, DiveHaulSync.Summary summary, bool suppressed)
        {
            if (town == null) { Step = default; Text = ""; Visible = false; StairBeacon = false; BuildBeacon = false; return; }
            Step = LoopGuide.Next(GuideSnapshot.FromTown(town, inDive, summary.CurrentFloor, summary.PiecesLeft));
            Text = GuideText.For(Step);
            Visible = !suppressed && Text.Length > 0;
            bool? at = Step.PointsAtStair;
            StairBeacon = Visible && at == true;
            BuildBeacon = Visible && at == false;
        }
    }

    public sealed class LoopGuideHud : MonoBehaviour
    {
        public const string StairBeaconName = "GuideBeacon_Stair", BuildBeaconName = "GuideBeacon_Build";

        public DiveDirector Director;     // this peer's director; DiveDirector.Instance when null (tests set it for a client)
        public readonly LoopGuideModel Model = new LoopGuideModel();
        public GameObject StairBeacon, BuildBeacon;   // Hollowmere's markers, found under the dive entrance and the build point
        private PlacementMode _placement;
        private TownSync _town;
        private float _nextFind;
        private GUIStyle _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<LoopGuideHud>() != null) return;
            var go = new GameObject("LoopGuideHud");
            DontDestroyOnLoad(go);
            go.AddComponent<LoopGuideHud>();
        }

        void Update()
        {
            DiveDirector dir = Director != null ? Director : DiveDirector.Instance;
            if (dir == null || !dir.IsSpawned) { Model.Tick(null, false, default, false); ApplyBeacons(); return; }
            if (Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 0.5f;
                if (_town == null || !_town.IsSpawned) _town = GuideSources.TownOf(dir);
                if (_placement == null) _placement = FindFirstObjectByType<PlacementMode>(FindObjectsInactive.Include);
                if (StairBeacon == null || BuildBeacon == null) FindBeacons();
            }
            DiveHaulSync haul = dir.HaulSync;
            bool suppressed = (_placement != null && _placement.Active) || DiveResultCard.ShowingCount > 0;
            Model.Tick(_town != null && _town.IsSpawned ? _town.View : null, dir.NetActive.Value, haul != null ? haul.Current : default, suppressed);
            ApplyBeacons();
        }

        void FindBeacons()
        {
            if (StairBeacon == null)
            {
                DiveHub hub = FindFirstObjectByType<DiveHub>(FindObjectsInactive.Include);
                Transform t = hub != null && hub.Entrance != null ? hub.Entrance.Find(StairBeaconName) : null;
                if (t != null) StairBeacon = t.gameObject;
            }
            if (BuildBeacon == null && _placement != null && _placement.BuildPoint != null)
            {
                Transform t = _placement.BuildPoint.Find(BuildBeaconName);
                if (t != null) BuildBeacon = t.gameObject;
            }
        }

        void ApplyBeacons()
        {
            if (StairBeacon != null && StairBeacon.activeSelf != Model.StairBeacon) StairBeacon.SetActive(Model.StairBeacon);
            if (BuildBeacon != null && BuildBeacon.activeSelf != Model.BuildBeacon) BuildBeacon.SetActive(Model.BuildBeacon);
        }

        void OnGUI()
        {
            if (!Model.Visible) return;
            float scale = Screen.height / 1440f;
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white }, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _label.fontSize = Mathf.Max(14, Mathf.RoundToInt(24 * scale));
            float w = Mathf.Min(Screen.width - 32f, 900f * scale), pad = 10f * scale, h = _label.CalcHeight(new GUIContent(Model.Text), w - pad * 2f) + pad * 2f;
            var r = new Rect((Screen.width - w) * 0.5f, 14f * scale, w, h);
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + pad, r.y + pad, w - pad * 2f, h - pad * 2f), Model.Text, _label);
        }
    }
}
