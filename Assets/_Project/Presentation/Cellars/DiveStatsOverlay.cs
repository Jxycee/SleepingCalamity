// Authority: none. Read-only dev overlay (F4, off by default; editor and development builds only) for balance playtests: per local peer it shows the dive
// time, damage taken, heals used, loaves left, stir, floor, pieces, player count and the current floor's per-room clear times. Every number is read from what this
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

        private void Unhook()
        {
            if (_hooked != null) _hooked.RoomEntered -= OnRoomEntered;
            _hooked = null;
        }

        private void OnRoomEntered(int room) { _model.NoteRoomEntered(room, Time.unscaledTimeAsDouble, _hooked != null ? _hooked.State.Floor : -1); }

        private void Update()
        {
            DiveDirector dd = DiveDirector.Instance;
            _hero = null;
            var all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsOwner) { _hero = all[i]; break; }

            CellarContentsSync contents = dd != null ? dd.ContentsSync : null;
            if (contents != _hooked)
            {
                Unhook();
                if (contents != null && contents.IsServer) { contents.RoomEntered += OnRoomEntered; _hooked = contents; }
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
            GUILayout.BeginArea(new Rect(Screen.width - 292, 12, 280, 460), GUI.skin.box);
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
                GUILayout.Label("Room clear times (this floor)" + (_hooked != null ? ", from first spawn" : ", from floor entry"));
                var clears = _model.RoomClears;
                if (clears.Count == 0) GUILayout.Label("  none yet");
                for (int i = 0; i < clears.Count; i++) GUILayout.Label("  " + DiveStatsModel.FormatRoom(clears[i]));
            }
            GUILayout.EndArea();
        }
    }
}
