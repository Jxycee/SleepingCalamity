// Core/Cellars/DiveStir.cs: the stir meter for the 10-floor dive (spec decision 19, section 4.5, red-team F2). Pure C#; time comes in as Tick(dt).
// Value = clamp((PerFloorCleared x floors cleared this half + PerUnbankedValuable x valuables carried) x sqrt(players), 0, Max).
// Latches hold for the half. At DeepClosesThreshold the host calls BeginCollapse with the path time to this floor's stair; once per half.
// Pushing on resets the danger side and carries the reward side (RewardValue). Replaces StirMeter in plan 3.
using System;

namespace SleepingCalamity.Core
{
    public sealed class DiveStir
    {
        public const float PerFloorCleared = 10f, PerUnbankedValuable = 5f, Max = 100f;               // placeholders
        public const float ExtraEnemyThreshold = 40f, HunterThreshold = 70f, DeepClosesThreshold = 100f;
        public const float CollapseMultiplier = 1.5f, CollapseMinSeconds = 60f, RewardRankStep = 35f;

        private int _floorMask;   // floors cleared this half (bit f-1)
        private int _players = 1, _valuables;
        private bool _extra, _hunter, _collapseUsed, _running;
        private float _secondsLeft, _rewardCarry;

        public int PlayersPresent { get => _players; set { _players = Math.Max(1, value); Refresh(); } }
        public int UnbankedValuables { get => _valuables; set { _valuables = Math.Max(0, value); Refresh(); } }

        public float Value
        {
            get
            {
                int floors = 0;
                for (int m = _floorMask; m != 0; m >>= 1) floors += m & 1;
                float raw = (PerFloorCleared * floors + PerUnbankedValuable * _valuables) * (float)Math.Sqrt(_players);
                return Math.Max(0f, Math.Min(Max, raw));
            }
        }

        public float RewardValue => Math.Min(Max, _rewardCarry + Value);
        public int RewardRankBonus => (int)(RewardValue / RewardRankStep);
        public bool ExtraEnemyLatched { get { Refresh(); return _extra; } }
        public bool HunterLatched { get { Refresh(); return _hunter; } }
        public bool CollapsePending => !_collapseUsed && !_running && Value >= DeepClosesThreshold;
        public bool CollapseRunning => _running;
        public float CollapseSecondsLeft => _running ? _secondsLeft : 0f;

        public static float CollapseSeconds(float pathSecondsToStair) => Math.Max(CollapseMinSeconds, CollapseMultiplier * pathSecondsToStair);

        public bool OnFloorCleared(int floor)
        {
            if (!UndercroftPlan.IsFloor(floor)) return false;
            int bit = 1 << (floor - 1);
            if ((_floorMask & bit) != 0) return false;
            _floorMask |= bit;
            Refresh();
            return true;
        }

        public void BeginCollapse(float pathSecondsToStair)
        {
            if (!CollapsePending) return;
            _running = true;
            _collapseUsed = true;
            _secondsLeft = CollapseSeconds(pathSecondsToStair);
        }

        public void OnStairTaken() { _running = false; _secondsLeft = 0f; }

        public void OnPushOn()
        {
            _rewardCarry = RewardValue;
            _floorMask = 0;
            _extra = _hunter = _collapseUsed = _running = false;
            _secondsLeft = 0f;
        }

        public void ResetForNewDive()
        {
            OnPushOn();
            _rewardCarry = 0f;
            _valuables = 0;
        }

        // True when the countdown just reached 0: the host fails the dive.
        public bool Tick(float dt)
        {
            if (!_running) return false;
            _secondsLeft -= dt;
            if (_secondsLeft > 0f) return false;
            _running = false;
            _secondsLeft = 0f;
            return true;
        }

        private void Refresh()
        {
            float v = Value;
            if (v >= ExtraEnemyThreshold) _extra = true;
            if (v >= HunterThreshold) _hunter = true;
        }
    }
}
