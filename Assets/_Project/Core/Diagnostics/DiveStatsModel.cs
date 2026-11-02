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
        private bool _maskKnown;   // false until this floor's cleared mask has arrived; the first known mask is a baseline, not fresh clears
        private const double HealWindow = 0.5;   // NetHp and NetLoaves replicate in different frames: a loaf drop and an HP rise this close are one heal
        private double _hpPendingUntil = -1, _loafAbsorbUntil = -1;
        private readonly Dictionary<(int floor, int room), double> _roomEntered = new Dictionary<(int floor, int room), double>();   // keyed by floor: the host can hear the new floor's first RoomEntered before Observe sees the floor change
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
        // floor: the floor that room is on (the contents state's floor); -1 means the floor this model is on now.
        public void NoteRoomEntered(int room, double now, int floor = -1)
        {
            var key = (floor < 0 ? _floor : floor, room);
            if (!_roomEntered.ContainsKey(key)) _roomEntered[key] = now;
        }

        // Call every frame. active: a dive is running; hp: the local hero's life; loaves: the local hero's loaves;
        // clearedMask: the floor's cleared-room mask, or null while this floor's contents state hasn't arrived.
        public void Observe(double now, bool active, int floor, int hp, int loaves, ulong? clearedMask)
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
                DamageTaken = 0; HealsUsed = 0; LoavesEaten = 0; _hpPendingUntil = -1; _loafAbsorbUntil = -1;
                _lastHp = hp; _lastLoaves = loaves;
                StartFloor(floor, now, clearedMask);
                return;
            }

            if (loaves < _lastLoaves)
            {
                int ate = _lastLoaves - loaves; LoavesEaten += ate;
                if (now <= _hpPendingUntil) { HealsUsed += ate - 1; _hpPendingUntil = -1; }   // the HP rise already counted is this loaf's heal
                else { HealsUsed += ate; _loafAbsorbUntil = now + HealWindow; }
            }
            if (hp < _lastHp) DamageTaken += _lastHp - hp;
            else if (hp > _lastHp)
            {
                if (now <= _loafAbsorbUntil) _loafAbsorbUntil = -1;   // the loaf heal already counted covers this rise
                else { HealsUsed++; _hpPendingUntil = now + HealWindow; }   // no loaf yet: counted as a bank/point heal unless a loaf drop lands within the window
            }
            _lastHp = hp; _lastLoaves = loaves;

            if (floor != _floor) { StartFloor(floor, now, clearedMask); return; }
            if (clearedMask == null) return;
            if (!_maskKnown) { _maskKnown = true; _lastMask = clearedMask.Value; return; }   // baseline: rooms already cleared (a revisited floor) are not fresh clears
            ulong gained = clearedMask.Value & ~_lastMask;
            for (int room = 0; room < 64 && gained != 0; room++)
            {
                ulong bit = 1UL << room;
                if ((gained & bit) == 0) continue;
                gained &= ~bit;
                bool spawned = _roomEntered.TryGetValue((_floor, room), out double t0);
                _clears.Add(new RoomClear { Room = room, Seconds = Math.Max(0, now - (spawned ? t0 : _floorStart)), FromFirstSpawn = spawned });
            }
            _lastMask = clearedMask.Value;
        }

        private void StartFloor(int floor, double now, ulong? mask)
        {
            _floor = floor; _floorStart = now; _maskKnown = mask != null; _lastMask = mask ?? 0UL;
            _clears.Clear();
            var stale = new List<(int floor, int room)>();
            foreach (var k in _roomEntered.Keys) if (k.floor != floor) stale.Add(k);
            foreach (var k in stale) _roomEntered.Remove(k);
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
