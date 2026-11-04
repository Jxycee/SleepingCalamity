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

        // ---- plan 3 task 11: minutes per floor, the bank choice by party size, the fail floor ----
        public struct ChoiceCount
        {
            public int PushOn;
            public int GoHome;
        }

        private readonly Dictionary<int, double> _floorSecs = new Dictionary<int, double>();   // closed time per floor, this dive (a floor entered twice adds up)
        private readonly Dictionary<int, ChoiceCount> _choices = new Dictionary<int, ChoiceCount>();
        private int _openFloor, _lastEntered;
        private double _openSince;
        private bool _stale = true;   // the per-dive records (floor times, fail floor) belong to an ended dive or to none: the next dive's first floor entry clears them

        /// <summary>The floor the last failed dive ended on (0 = no failure in this dive). Per dive.</summary>
        public int FailFloor { get; private set; }

        /// <summary>The confirmed bank choices by party size (1-4): push-on and go-home counts. Adds up across dives, so a playtest session compares them.</summary>
        public IReadOnlyDictionary<int, ChoiceCount> Choices => _choices;

        /// <summary>Seconds spent on `floor` this dive, closed intervals only (the floor entered, until the next floor entry or the dive's end).</summary>
        public double FloorSeconds(int floor) => _floorSecs.TryGetValue(floor, out double s) ? s : 0;

        /// <summary>The same, plus the time since `floor` was entered when it is still the open floor at `now`.</summary>
        public double FloorSeconds(int floor, double now) => FloorSeconds(floor) + (_openFloor == floor ? Math.Max(0, now - _openSince) : 0);

        // Host: the party is on `floor` from `now` (DiveDirector.FloorChanged). The same floor again is ignored, so a peer's Observe and the host's event never count it twice.
        public void NoteFloorEntered(int floor, double now)
        {
            if (floor <= 0) return;
            if (_stale) { _floorSecs.Clear(); FailFloor = 0; _openFloor = 0; _lastEntered = 0; _stale = false; }
            if (_openFloor == floor) return;
            CloseFloor(now);
            _openFloor = floor; _openSince = now; _lastEntered = floor;
        }

        // Host: a bank choice was confirmed (DiveHaulSync.PushedOn / WentHome) with `players` in the party.
        public void NoteBankChoice(bool pushedOn, int players)
        {
            int size = players < 1 ? 1 : players;
            _choices.TryGetValue(size, out ChoiceCount c);
            if (pushedOn) c.PushOn++; else c.GoHome++;
            _choices[size] = c;
        }

        // Host: the dive failed on `floor` (0 = not known: the last floor entered).
        public void NoteFail(int floor) { FailFloor = floor > 0 ? floor : _lastEntered; }

        private void CloseFloor(double now)
        {
            if (_openFloor <= 0) return;
            _floorSecs.TryGetValue(_openFloor, out double have);
            _floorSecs[_openFloor] = have + Math.Max(0, now - _openSince);
            _openFloor = 0;
        }

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
                if (_active) { _active = false; _diveEnd = now; CloseFloor(now); _stale = true; }
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
            NoteFloorEntered(floor, now);   // a client hears no events: its own floor changes feed the floor times (the host's event for the same floor is ignored)
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

        public static string FormatChoice(int players, ChoiceCount c) => players + (players == 1 ? " player" : " players") + ": push on " + c.PushOn + ", home " + c.GoHome;

        public static string FormatRoom(RoomClear c) => "room " + c.Room + "  " + FormatTime(c.Seconds) + (c.FromFirstSpawn ? "" : " (from floor start)");
    }
}
