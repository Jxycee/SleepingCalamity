// Core/Cellars/StirMeter.cs: the dive's stir meter (PHASE1 7.2 "The stir meter"). Pure C#, deterministic, no engine references; time comes in as Tick(dt).
// Value = clamp((PerFloorCleared x floorsClearedThisDive + PerUnbankedPiece x unbankedPieces) x (1 + PerExtraPlayer x (playersPresent - 1)), 0, Max).
// The piece term reads the haul live, so a checkpoint bank lowers it; the floor term only goes back to 0 on ResetAll (town bank or failed dive). Chest loot never counts.
// Thresholds latch for the dive (stay on if the value drops): extra enemy, hunter, deep closes. Deep closes also starts a countdown; Tick returns true when it
// hits 0 (the owner fails the dive); OnBank stops it. After a stop it re-arms only once the value has dropped below the threshold again.
// Owned by DiveState (host). Numbers: StirTuning.
using System;

namespace SleepingCalamity.Core
{
    public sealed class StirMeter
    {
        private readonly DiveHaul _haul;
        private int _floorMask;      // floors counted this dive (bit f-1)
        private int _players = 1;
        private bool _armed = true, _running;
        private float _secondsLeft;

        public StirTuning Tuning { get; }
        private bool _extra, _hunter, _deep;
        public bool ExtraEnemyLatched { get { Refresh(); return _extra; } }
        public bool HunterLatched { get { Refresh(); return _hunter; } }
        public bool DeepClosesLatched { get { Refresh(); return _deep; } }
        public event Action HunterReleased;   // fires once per latch; the real hunter enemy is later work

        public StirMeter(DiveHaul haul, StirTuning tuning) { _haul = haul; Tuning = tuning; }

        public int PlayersPresent { get => _players; set { _players = Math.Max(1, value); Refresh(); } }
        public int FloorsCleared { get { int n = 0; for (int m = _floorMask; m != 0; m >>= 1) n += m & 1; return n; } }
        public bool DeepClosesRunning { get { Refresh(); return _running; } }
        public float DeepClosesSecondsLeft { get { Refresh(); return _running ? _secondsLeft : 0f; } }
        public int ExtraEnemiesPerRoom => ExtraEnemyLatched ? Tuning.ExtraEnemiesPerRoom : 0;

        public float Value
        {
            get
            {
                float raw = Tuning.PerFloorCleared * FloorsCleared + Tuning.PerUnbankedPiece * _haul.UnbankedPieceCount;
                float v = raw * (1f + Tuning.PerExtraPlayer * (_players - 1));
                return Math.Max(0f, Math.Min(Tuning.Max, v));
            }
        }

        // Re-evaluates latches after anything that can move the value (piece added, floor cleared, player count, tick).
        public void Refresh()
        {
            float v = Value;
            if (v >= Tuning.ExtraEnemyThreshold) _extra = true;
            if (v >= Tuning.HunterThreshold && !_hunter) { _hunter = true; HunterReleased?.Invoke(); }
            if (v >= Tuning.DeepClosesThreshold)
            {
                _deep = true;
                if (_armed && !_running) { _running = true; _secondsLeft = Tuning.DeepClosesSeconds; _armed = false; }
            }
            else _armed = true;
        }

        // Counts a floor once per dive. False when already counted.
        public bool OnFloorCleared(int floor)
        {
            if (floor < 1 || floor > DiveState.MaxFloor) return false;
            int bit = 1 << (floor - 1);
            if ((_floorMask & bit) != 0) return false;
            _floorMask |= bit; Refresh(); return true;
        }

        // Any successful bank (checkpoint, heart or town): the countdown stops. The haul has already moved its pieces.
        public void OnBank() { _running = false; _secondsLeft = 0f; Refresh(); }

        // Town bank and failed dive: floor term, latches and countdown all clear.
        public void ResetAll()
        {
            _floorMask = 0; _extra = _hunter = _deep = false;
            _running = false; _secondsLeft = 0f; _armed = true;
        }

        // Advances the countdown. True when it just reached 0 (the owner must fail the dive).
        public bool Tick(float dt)
        {
            Refresh();
            if (!_running) return false;
            _secondsLeft -= dt;
            if (_secondsLeft > 0f) return false;
            _running = false; _secondsLeft = 0f;
            return true;
        }
    }
}
