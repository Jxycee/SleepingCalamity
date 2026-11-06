// Authority: none. Read-only playtest overlay (F4 or the gamepad combo Back/Select + right-stick click, off by default; available in every build, non-dev included) for balance playtests: per local peer it shows the dive
// time, damage taken, heals used, loaves left, stir, floor, pieces, player count, the time per floor, the fail floor, the bank choices (go home vs push on, by party size; host only, they
// add up across dives) and the current floor's per-room clear times. The host forwards DiveDirector.FloorChanged / DiveFailedOn and DiveHaulSync.PushedOn / WentHome to the model; a client's
// model gets its floor times from the floor it sees. Every peer also reads the other heroes' replicated HP and loaves (Everyone-readable NetworkVariables) into one model per hero, so the overlay shows each player and the party totals; nothing new is synced. When a dive ends it writes the numbers as text and JSON under Application.persistentDataPath/PlaytestSummaries (Core/Diagnostics/DiveSummaryReport). Every number is read from what this
// peer can already see (the local hero's HeroVitals, DiveDirector.NetActive, DiveHaulSync.Summary, CellarContentsSync.State); it never writes game state and
// sends nothing. Core/Diagnostics/DiveStatsModel does the bookkeeping. The host also hears CellarContentsSync.RoomEntered, so its room times run from the
// room's first enemy spawn; a client only sees the cleared-room mask and shows the time from floor entry (marked). Created by a RuntimeInitializeOnLoad hook.
using System.Collections.Generic;
using System.IO;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SleepingCalamity.Presentation
{
    public sealed class DiveStatsOverlay : MonoBehaviour
    {
        private readonly DiveStatsModel _model = new DiveStatsModel();
        private bool _visible;
        public static bool Shown { get; private set; }   // read by FloorCardHud to sit below this panel (both use the top-right corner)
        private CellarContentsSync _hooked;
        private HeroVitals _hero;
        private DiveHaulSync.Summary _sum;
        private bool _haveSum;
        private int _players, _lastHp, _lastLoaves;
        private bool _wasActive;
        private string _savedNote = "";

        private sealed class Other { public DiveStatsModel Model = new DiveStatsModel(); public HeroVitals Hero; public int LastHp, LastLoaves; public int Order; }
        private readonly Dictionary<ulong, Other> _others = new Dictionary<ulong, Other>();   // the heroes this peer does not own, by owner id (kept after they leave so the end-of-dive summary still lists them)
        private readonly List<ulong> _otherKeys = new List<ulong>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<DiveStatsOverlay>() != null) return;
            var go = new GameObject("DiveStatsOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<DiveStatsOverlay>();
        }

        private void OnDestroy() { Unhook(); Shown = false; }

        private DiveDirector _hookedDir;
        private DiveHaulSync _hookedHaul;

        private void Unhook()
        {
            if (_hooked != null) _hooked.RoomEntered -= OnRoomEntered;
            _hooked = null;
            if (_hookedDir != null) { _hookedDir.FloorChanged -= OnFloorChanged; _hookedDir.DiveFailedOn -= OnDiveFailedOn; }
            _hookedDir = null;
            if (_hookedHaul != null) { _hookedHaul.PushedOn -= OnPushedOn; _hookedHaul.WentHome -= OnWentHome; }
            _hookedHaul = null;
        }

        // Host only (the events never fire elsewhere): the floor, the bank choice and the fail floor; a client's model gets its floor times from Observe.
        private void OnFloorChanged(int floor) { _model.NoteFloorEntered(floor, Time.unscaledTimeAsDouble); }
        private void OnDiveFailedOn(int floor) { _model.NoteFail(floor); }
        private void OnPushedOn() { _model.NoteBankChoice(true, PartySize()); }
        private void OnWentHome() { _model.NoteBankChoice(false, PartySize()); }

        // Players in the party now: the host's connected clients (a client counts the heroes it sees).
        private static int PartySize()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening ? (nm.IsServer ? nm.ConnectedClientsIds.Count : HeroVitals.All.Count) : 1;
        }

        private void OnRoomEntered(int room) { _model.NoteRoomEntered(room, Time.unscaledTimeAsDouble, _hooked != null ? _hooked.State.Floor : -1); }

        private void Update()
        {
            DiveDirector dd = DiveDirector.Instance;
            _hero = null;
            var all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsOwner) { _hero = all[i]; break; }

            CellarContentsSync contents = dd != null ? dd.ContentsSync : null;
            DiveHaulSync hookHaul = dd != null ? dd.HaulSync : null;
            bool hostDir = dd != null && dd.IsServer;
            if (contents != _hooked || (hostDir ? dd : null) != _hookedDir || (hostDir && hookHaul != _hookedHaul))
            {
                Unhook();
                if (contents != null && contents.IsServer) { contents.RoomEntered += OnRoomEntered; _hooked = contents; }
                if (hostDir)
                {
                    dd.FloorChanged += OnFloorChanged; dd.DiveFailedOn += OnDiveFailedOn; _hookedDir = dd;
                    if (hookHaul != null) { hookHaul.PushedOn += OnPushedOn; hookHaul.WentHome += OnWentHome; _hookedHaul = hookHaul; }
                }
            }

            DiveHaulSync haul = dd != null ? dd.HaulSync : null;
            _haveSum = haul != null;
            if (_haveSum) _sum = haul.Current;
            NetworkManager nm = NetworkManager.Singleton;
            _players = nm != null && nm.IsListening ? (nm.IsServer ? nm.ConnectedClientsIds.Count : HeroVitals.All.Count) : 1;   // ConnectedClientsIds is host-only in NGO; a client counts the heroes it sees

            bool active = dd != null && dd.NetActive.Value && _haveSum;
            if (_hero != null) { _lastHp = _hero.Hp; _lastLoaves = _hero.Loaves; }   // hero briefly missing (respawn, reconnect): keep the last numbers, don't reset the dive
            double now = Time.unscaledTimeAsDouble;
            if (active && !_wasActive) PruneGone(all);   // a new dive: drop heroes who left earlier (no ghost rows from an old party)
            int floor = _haveSum ? _sum.CurrentFloor : 0;
            ulong? mask = RoomMask(contents, floor);
            _model.Observe(now, active, floor, _lastHp, _lastLoaves, mask);

            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals h = all[i];
                if (h == null || h.IsOwner) continue;
                ulong id = h.OwnerClientId;
                if (!_others.TryGetValue(id, out Other o)) { o = new Other { Order = _others.Count }; _others[id] = o; _otherKeys.Add(id); }
                o.Hero = h; o.LastHp = h.Hp; o.LastLoaves = h.Loaves;
            }
            foreach (ulong id in _otherKeys)
            {
                Other o = _others[id];
                o.Model.Observe(now, active, floor, o.LastHp, o.LastLoaves, mask);
            }

            if (_wasActive && !_model.Active) SaveSummary(now);
            _wasActive = _model.Active;
            HandleToggle();
        }

        private void PruneGone(IReadOnlyList<HeroVitals> all)
        {
            for (int k = _otherKeys.Count - 1; k >= 0; k--)
            {
                ulong id = _otherKeys[k];
                bool present = false;
                for (int i = 0; i < all.Count && !present; i++) present = all[i] != null && all[i].OwnerClientId == id;
                if (present) continue;
                _others.Remove(id); _otherKeys.RemoveAt(k);
            }
            for (int k = 0; k < _otherKeys.Count; k++) _others[_otherKeys[k]].Order = k;
        }

        // F4 on a keyboard, or Back/Select + a right-stick click on a gamepad. Read straight from the devices (not the rebindable asset) so it works in every build.
        private void HandleToggle()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if ((kb != null && kb.f4Key.wasPressedThisFrame) || (pad != null && pad.selectButton.isPressed && pad.rightStickButton.wasPressedThisFrame)) _visible = !_visible;
            Shown = _visible;
        }

        private DiveSummaryReport BuildReport(double now)
        {
            var r = new DiveSummaryReport { DiveSeconds = _model.DiveSeconds(now), FinalFloor = _model.Floor, FailFloor = _model.FailFloor, PartySize = _players };
            for (int f = UndercroftPlan.FirstFloor; f <= DiveFloorTable.LastBuiltFloor; f++)
            {
                double s = _model.FloorSeconds(f, now);
                if (s > 0) r.FloorSeconds.Add(new KeyValuePair<int, double>(f, s));
            }
            r.Players.Add(Row("You", _model, _hero != null ? _hero.Loaves : _lastLoaves));
            foreach (ulong id in _otherKeys) r.Players.Add(Row("P" + (_others[id].Order + 2) + " (client " + id + ")", _others[id].Model, _others[id].LastLoaves));
            return r;
        }

        private static DiveSummaryReport.PlayerRow Row(string name, DiveStatsModel m, int loavesLeft) =>
            new DiveSummaryReport.PlayerRow { Name = name, DamageTaken = m.DamageTaken, HealsUsed = m.HealsUsed, LoavesEaten = m.LoavesEaten, LoavesLeft = loavesLeft };

        // Dive over: write the same numbers as text and JSON for the playtest notes. Local file I/O only; a failure is logged and ignored.
        private void SaveSummary(double now)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "PlaytestSummaries");
                Directory.CreateDirectory(dir);
                string stem = Path.Combine(dir, "dive-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                DiveSummaryReport r = BuildReport(now);
                File.WriteAllText(stem + ".txt", r.ToText());
                File.WriteAllText(stem + ".json", r.ToJson());
                _savedNote = "Summary saved: " + stem + ".txt";
            }
            catch (System.Exception e) { _savedNote = "Summary not saved: " + e.Message; Debug.LogWarning("[DiveStats] " + _savedNote); }
        }

        // The per-room cleared mask of the floor being played (the haul summary's ClearedMask is the cleared-FLOOR mask). Null until the contents state of this floor has arrived.
        private static ulong? RoomMask(CellarContentsSync contents, int floor)
        {
            if (contents == null || floor <= 0) return null;
            CellarContentsSync.ContentsState st = contents.State;
            return st.Floor == floor ? st.Cleared : (ulong?)null;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            GUILayout.BeginArea(new Rect(Screen.width - 292, 12, 280, 640), GUI.skin.box);
            GUILayout.Label("Dive stats (F4 or Back+R3 hides)");
            if (!_model.HasDive) GUILayout.Label("No dive yet.");
            else
            {
                double now = Time.unscaledTimeAsDouble;
                GUILayout.Label((_model.Active ? "Dive time: " : "Dive over, time: ") + DiveStatsModel.FormatTime(_model.DiveSeconds(now)));
                GUILayout.Label("Damage taken: " + _model.DamageTaken);
                GUILayout.Label("Heals used: " + _model.HealsUsed + "  (loaves eaten " + _model.LoavesEaten + ")");
                GUILayout.Label("Loaves left: " + (_hero != null ? _hero.Loaves.ToString() : "-"));
                if (_haveSum)
                {
                    GUILayout.Label("Stir: " + _sum.StirValue + "/100   extra enemies per room: " + _sum.ExtraEnemiesPerRoom
                        + (_sum.HunterLatched ? "   hunter" : "") + (_sum.DeepSecondsLeft > 0 ? "   deep closes in " + _sum.DeepSecondsLeft + "s" : ""));
                    GUILayout.Label("Floor: " + _sum.CurrentFloor);
                    GUILayout.Label("Pieces found: " + _sum.FoundPieces);   // safe on pickup and in the town stock at once
                }
                GUILayout.Label("Players: " + _players);
                if (_others.Count > 0)
                {
                    DiveSummaryReport r = BuildReport(now);
                    foreach (var p in r.Players) GUILayout.Label(p.Name + ": dmg " + p.DamageTaken + ", heals " + p.HealsUsed + ", loaves eaten " + p.LoavesEaten + ", left " + p.LoavesLeft);
                    GUILayout.Label("Party: dmg " + r.TotalDamage + ", heals " + r.TotalHeals + ", loaves eaten " + r.TotalLoaves);
                }
                if (_savedNote.Length > 0) GUILayout.Label(_savedNote);
                string perFloor = "";
                for (int f = UndercroftPlan.FirstFloor; f <= DiveFloorTable.LastBuiltFloor; f++)
                {
                    double s = _model.FloorSeconds(f, now);   // the open floor counts up; a finished dive has none open
                    if (s > 0) perFloor += (perFloor.Length > 0 ? "  " : "") + "F" + f + " " + DiveStatsModel.FormatTime(s);
                }
                GUILayout.Label("Time per floor: " + (perFloor.Length > 0 ? perFloor : "none yet"));
                if (_model.FailFloor > 0) GUILayout.Label("Failed on floor " + _model.FailFloor);
                if (_model.Choices.Count == 0) GUILayout.Label("Bank choice: none yet (host only)");
                else for (int p = 1; p <= 4; p++) if (_model.Choices.TryGetValue(p, out DiveStatsModel.ChoiceCount cc)) GUILayout.Label("Bank choice, " + DiveStatsModel.FormatChoice(p, cc));
                GUILayout.Label("Room clear times (this floor)" + (_hooked != null ? ", from first spawn" : ", from floor entry"));
                var clears = _model.RoomClears;
                if (clears.Count == 0) GUILayout.Label("  none yet");
                for (int i = 0; i < clears.Count; i++) GUILayout.Label("  " + DiveStatsModel.FormatRoom(clears[i]));
            }
            GUILayout.EndArea();
        }
    }
}
