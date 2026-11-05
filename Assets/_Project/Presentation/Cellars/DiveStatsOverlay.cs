// Authority: none. Read-only dev overlay (F4, off by default; editor and development builds only) for balance playtests: per local peer it shows the dive
// time, damage taken, heals used, loaves left, stir, floor, pieces, player count, the time per floor, the fail floor, the bank choices (go home vs push on, by party size; host only, they
// add up across dives) and the current floor's per-room clear times. The host forwards DiveDirector.FloorChanged / DiveFailedOn and DiveHaulSync.PushedOn / WentHome to the model; a client's
// model gets its floor times from the floor it sees. Every number is read from what this
// peer can already see (the local hero's HeroVitals, DiveDirector.NetActive, DiveHaulSync.Summary, CellarContentsSync.State); it never writes game state and
// sends nothing. Core/Diagnostics/DiveStatsModel does the bookkeeping. The host also hears CellarContentsSync.RoomEntered, so its room times run from the
// room's first enemy spawn; a client only sees the cleared-room mask and shows the time from floor entry (marked). Created by a RuntimeInitializeOnLoad hook.
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class DiveStatsOverlay : MonoBehaviour
    {
        private readonly DiveStatsModel _model = new DiveStatsModel();
        private bool _visible;
        private CellarContentsSync _hooked;
        private HeroVitals _hero;
        private DiveHaulSync.Summary _sum;
        private bool _haveSum;
        private int _players, _lastHp, _lastLoaves;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!(Debug.isDebugBuild || Application.isEditor)) return;
            if (FindFirstObjectByType<DiveStatsOverlay>() != null) return;
            var go = new GameObject("DiveStatsOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<DiveStatsOverlay>();
        }

        private void OnDestroy() { Unhook(); }

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
            _model.Observe(Time.unscaledTimeAsDouble, active, _haveSum ? _sum.CurrentFloor : 0, _lastHp,
                _lastLoaves, RoomMask(contents, _haveSum ? _sum.CurrentFloor : 0));
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
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F4) _visible = !_visible;
            if (!_visible) return;
            GUILayout.BeginArea(new Rect(Screen.width - 292, 12, 280, 560), GUI.skin.box);
            GUILayout.Label("Dive stats (dev, F4 hides)");
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
