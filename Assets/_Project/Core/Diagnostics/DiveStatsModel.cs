// Core/Diagnostics/DiveStatsModel.cs: the numbers behind the read-only dev overlay (docs/design/m7-proposal.md section 4, Q3). Pure C#, no engine references.
// Authority: none (a local observer; every peer feeds it only what that peer can already read: its own hero's HP and loaves, the dive flag, the floor, the
// cleared-room mask). It never writes game state. One dive = one run of Observe(...) with active == true; a new dive resets everything.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class DiveStatsModel
    {
        public struct RoomClear
        {
            public int Room;
            public double Seconds;
            public bool FromFirstSpawn;   // true: first enemy spawn to clear (host saw the room entered); false: floor entry to clear (a peer only sees the mask)
        }

        private bool _active, _have;
        private double _diveStart, _diveEnd, _floorStart;
        private int _lastHp, _lastLoaves, _floor;
        private ulong _lastMask;
        private readonly Dictionary<int, double> _roomEntered = new Dictionary<int, double>();
        private readonly List<RoomClear> _clears = new List<RoomClear>();

        public bool Active => _active;
        public bool HasDive => _have;
        public int DamageTaken { get; private set; }
        public int HealsUsed { get; private set; }       // HP jumps up that were not a loaf, plus loaves eaten
        public int LoavesEaten { get; private set; }
        public int Floor => _floor;
        public IReadOnlyList<RoomClear> RoomClears => _clears;

        public double DiveSeconds(double now) => !_have ? 0 : Math.Max(0, (_active ? now : _diveEnd) - _diveStart);

        // Host only: the first enemy of a room spawned (CellarContentsSync.RoomEntered). Later calls for the same room on the same floor keep the first time.
        public void NoteRoomEntered(int room, double now)
        {
            if (!_roomEntered.ContainsKey(room)) _roomEntered[room] = now;
        }

        // Call every frame. active: a dive is running; hp: the local hero's life; loaves: the local hero's loaves.
        public void Observe(double now, bool active, int floor, int hp, int loaves, ulong clearedMask)
        {
            if (!active)
            {
                if (_active) { _active = false; _diveEnd = now; }
                return;
            }
            if (!_active)
            {
                // A new dive (or the first sight of one in progress): reset.
                _active = true; _have = true; _diveStart = now; _diveEnd = now;
                DamageTaken = 0; HealsUsed = 0; LoavesEaten = 0;
                _lastHp = hp; _lastLoaves = loaves;
                StartFloor(floor, now, clearedMask);
                return;
            }

            if (loaves < _lastLoaves) { int ate = _lastLoaves - loaves; LoavesEaten += ate; HealsUsed += ate; }
            if (hp < _lastHp) DamageTaken += _lastHp - hp;
            else if (hp > _lastHp && loaves >= _lastLoaves) HealsUsed++;   // a jump up with no loaf eaten: bank heal, healing point, revive
            _lastHp = hp; _lastLoaves = loaves;

            if (floor != _floor) { StartFloor(floor, now, clearedMask); return; }
            ulong gained = clearedMask & ~_lastMask;
            for (int room = 0; room < 64 && gained != 0; room++)
            {
                ulong bit = 1UL << room;
                if ((gained & bit) == 0) continue;
                gained &= ~bit;
                bool spawned = _roomEntered.TryGetValue(room, out double t0);
                _clears.Add(new RoomClear { Room = room, Seconds = Math.Max(0, now - (spawned ? t0 : _floorStart)), FromFirstSpawn = spawned });
            }
            _lastMask = clearedMask;
        }

        private void StartFloor(int floor, double now, ulong mask)
        {
            _floor = floor; _floorStart = now; _lastMask = mask;
            _clears.Clear(); _roomEntered.Clear();
        }

        // "m:ss" (minutes unbounded); negative is shown as 0:00.
        public static string FormatTime(double seconds)
        {
            int s = seconds <= 0 ? 0 : (int)seconds;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public static string FormatRoom(RoomClear c) => "room " + c.Room + "  " + FormatTime(c.Seconds) + (c.FromFirstSpawn ? "" : " (from floor start)");
    }
}
