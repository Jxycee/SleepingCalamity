// Core/Cellars/DiveStir.cs: the stir meter for the 10-floor dive (spec decision 19, section 4.5, red-team F2). Pure C#; time comes in as Tick(dt).
// Value = clamp((PerFloorCleared x floors cleared this half + PerUnbankedValuable x valuables carried) x sqrt(players) + ModifierBonus, 0, Max).
// ModifierBonus (plan 5): the Stirred floor modifier's flat stir, added after the sqrt-of-players scaling, set by the host from (seed, current floor); not part of State, never saved.
//   The bonus feeds Refresh like any stir, so Stirred can latch the extra enemy or the hunter early, and a latch holds for the rest of the half even after the party leaves the floor: that is the hazard (the card says so).
// Latches hold for the half. At DeepClosesThreshold the host calls BeginCollapse with the path time to this floor's stair; once per half.
// Pushing on resets the danger side and carries the reward side (RewardValue).
using System;

namespace SleepingCalamity.Core
{
    public sealed class DiveStir
    {
        public const float PerFloorCleared = 10f, PerUnbankedValuable = 5f, Max = 100f;               // placeholders
        public const float ExtraEnemyThreshold = 40f, HunterThreshold = 70f, DeepClosesThreshold = 100f;
        public const float CollapseMultiplier = 1.5f, CollapseMinSeconds = 60f, RewardRankStep = 35f;

        private int _floorMask;   // floors cleared this half (bit f-1)
        private int _players = 1, _valuables, _baseline;   // _baseline: valuables already carried at push-on (they stirred the first half)
        private bool _extra, _hunter, _collapseUsed, _running, _secondHalf;
        private float _secondsLeft, _rewardCarry;

        public int PlayersPresent { get => _players; set { _players = Math.Max(1, value); Refresh(); } }
        public int UnbankedValuables { get => _valuables; set { _valuables = Math.Max(0, value); Refresh(); } }

        private float _modifierBonus;
        public float ModifierBonus { get => _modifierBonus; set { _modifierBonus = Math.Max(0f, value); Refresh(); } }

        public float Value
        {
            get
            {
                int floors = 0;
                for (int m = _floorMask; m != 0; m >>= 1) floors += m & 1;
                float raw = (PerFloorCleared * floors + PerUnbankedValuable * Math.Max(0, _valuables - _baseline)) * (float)Math.Sqrt(_players) + _modifierBonus;
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

        // factor scales the final seconds, the 60 s minimum included (a Hurried floor passes ~0.833); 1 = the ordinary collapse.
        public static float CollapseSeconds(float pathSecondsToStair, float factor = 1f) => Math.Max(CollapseMinSeconds, CollapseMultiplier * pathSecondsToStair) * factor;

        public bool OnFloorCleared(int floor)
        {
            if (!UndercroftPlan.IsFloor(floor)) return false;
            if (UndercroftPlan.IsFirstHalf(floor) == _secondHalf) return false;   // each half stirs only on its own floors
            int bit = 1 << (floor - 1);
            if ((_floorMask & bit) != 0) return false;
            _floorMask |= bit;
            Refresh();
            return true;
        }

        public void BeginCollapse(float pathSecondsToStair, float factor = 1f)
        {
            if (!CollapsePending) return;
            _running = true;
            _collapseUsed = true;
            _secondsLeft = CollapseSeconds(pathSecondsToStair, factor);
        }

        public void OnStairTaken() { _running = false; _secondsLeft = 0f; }

        // The CALLER sets UnbankedValuables (what is still carried after the bank picks) before this: that count becomes the baseline.
        public void OnPushOn()
        {
            _rewardCarry = RewardValue;   // counts the carried valuables once; the baseline keeps them out of the second half's Value
            _baseline = _valuables;
            _secondHalf = true;
            _floorMask = 0;
            _extra = _hunter = _collapseUsed = _running = false;
            _secondsLeft = 0f;
            _modifierBonus = 0f;   // after the carry above: the floor's flat stir does not follow the party
        }

        public void ResetForNewDive()
        {
            OnPushOn();
            _rewardCarry = 0f;
            _valuables = 0;
            _baseline = 0;
            _secondHalf = false;
        }

        public readonly struct State : IEquatable<State>
        {
            public int FloorMask { get; }
            public int Valuables { get; }
            public int ValuablesBaseline { get; }
            public bool SecondHalf { get; }
            public bool Extra { get; }
            public bool Hunter { get; }
            public bool CollapseUsed { get; }
            public bool Running { get; }
            public float SecondsLeft { get; }
            public float RewardCarry { get; }

            public State(int floorMask, int valuables, int valuablesBaseline, bool secondHalf, bool extra, bool hunter, bool collapseUsed, bool running, float secondsLeft, float rewardCarry)
            {
                FloorMask = floorMask; Valuables = valuables; ValuablesBaseline = valuablesBaseline; SecondHalf = secondHalf; Extra = extra; Hunter = hunter;
                CollapseUsed = collapseUsed; Running = running; SecondsLeft = secondsLeft; RewardCarry = rewardCarry;
            }

            public bool Equals(State o) => FloorMask == o.FloorMask && Valuables == o.Valuables && ValuablesBaseline == o.ValuablesBaseline && SecondHalf == o.SecondHalf
                && Extra == o.Extra && Hunter == o.Hunter && CollapseUsed == o.CollapseUsed && Running == o.Running && SecondsLeft == o.SecondsLeft && RewardCarry == o.RewardCarry;
            public override bool Equals(object obj) => obj is State s && Equals(s);
            public override int GetHashCode() => HashCode.Combine(FloorMask, Valuables, ValuablesBaseline, SecondHalf, Extra, Hunter, HashCode.Combine(CollapseUsed, Running, SecondsLeft, RewardCarry));
        }

        // Players present are not part of the state: the host sets them from who is there.
        public State Capture() => new State(_floorMask, _valuables, _baseline, _secondHalf, _extra, _hunter, _collapseUsed, _running, _secondsLeft, _rewardCarry);

        public void Restore(State s)
        {
            _floorMask = s.FloorMask; _valuables = Math.Max(0, s.Valuables); _baseline = Math.Max(0, s.ValuablesBaseline); _secondHalf = s.SecondHalf;
            _extra = s.Extra; _hunter = s.Hunter; _collapseUsed = s.CollapseUsed; _running = s.Running;
            _secondsLeft = s.SecondsLeft; _rewardCarry = s.RewardCarry;
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
