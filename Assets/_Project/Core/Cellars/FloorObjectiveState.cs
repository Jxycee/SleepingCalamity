// Core/Cellars/FloorObjectiveState.cs: the hold rules of the two new floor objectives (plan 5 task 3, decisions 7 and 8). Pure C#; time comes in as arguments, no engine references.
//   ValveHold: two valves, each turned by holding the interact button within reach for ValveHoldSeconds. The owner reports "holding at valve i" every ReportInterval; the host
//     accumulates while the last report is at most ReportTimeout old and resets that valve's progress to 0 when reports stop. A turned valve stays turned, in any order.
//   PumpHold: one press starts it; progress fills 1/PumpSeconds per second while a living hero is inside the ring, pauses (never resets) when none is, and runs at PumpSlowFactor while a
//     pump-wave enemy is at the pump. Three waves spawn at WaveThresholds; a wave waits while MaxWavesAlive earlier waves still have living members. Wave size and spacing live here as statics.
//     At 100% it is done (JustDone once); the pump cannot fail. The tuning consts are FloorObjectiveTuning (all placeholders).
// Authority: host only (the host's CellarContentsSync owns an instance per floor and feeds it reach, liveness and enemy counts; clients only see the result in ContentsState).
using System;

namespace SleepingCalamity.Core
{
    public sealed class ValveHold
    {
        public const int Count = 2;

        readonly float[] _progress = new float[Count];
        readonly float[] _lastReport = new float[Count];
        readonly bool[] _reported = new bool[Count];
        int _mask;

        public int TurnedMask => _mask;
        public int TurnedCount { get { int n = 0; for (int i = 0; i < Count; i++) if ((_mask & (1 << i)) != 0) n++; return n; } }
        public bool AllTurned => TurnedCount == Count;

        // 0..1 of the hold; 1 once the valve is turned.
        public float Progress(int valve)
        {
            if (valve < 0 || valve >= Count) return 0f;
            return (_mask & (1 << valve)) != 0 ? 1f : Math.Min(1f, _progress[valve] / FloorObjectiveTuning.ValveHoldSeconds);
        }

        public void Report(int valve, float now)
        {
            if (valve < 0 || valve >= Count) return;
            _lastReport[valve] = now; _reported[valve] = true;
        }

        // True when a valve turned this tick.
        public bool Tick(float now, float dt)
        {
            bool turned = false;
            for (int v = 0; v < Count; v++)
            {
                if ((_mask & (1 << v)) != 0) continue;
                if (_reported[v] && now - _lastReport[v] <= FloorObjectiveTuning.ReportTimeout)
                {
                    _progress[v] += dt;
                    if (_progress[v] >= FloorObjectiveTuning.ValveHoldSeconds) { _mask |= 1 << v; turned = true; }
                }
                else _progress[v] = 0f;
            }
            return turned;
        }

        public void Reset()
        {
            _mask = 0;
            for (int v = 0; v < Count; v++) { _progress[v] = 0f; _lastReport[v] = 0f; _reported[v] = false; }
        }
    }

    public readonly struct PumpTick
    {
        public readonly int StartWave;   // -1 none, else the wave index to spawn now
        public readonly bool JustDone;
        public PumpTick(int startWave, bool justDone) { StartWave = startWave; JustDone = justDone; }
    }

    public sealed class PumpHold
    {
        float _progress;
        int _waves;

        public bool Started { get; private set; }
        public float Progress => _progress;
        public bool Done { get; private set; }
        public int WavesSpawned => _waves;

        public void Start() { if (!Done) Started = true; }

        // livingPerWaveN: how many members of wave N are still alive (waves that have not spawned pass 0). At most one wave starts per tick.
        public PumpTick Tick(float dt, bool heroInRing, bool enemyAtPump, int livingPerWave0, int livingPerWave1, int livingPerWave2)
        {
            if (!Started || Done) return new PumpTick(-1, false);
            if (heroInRing)
            {
                float rate = 1f / FloorObjectiveTuning.PumpSeconds * (enemyAtPump ? FloorObjectiveTuning.PumpSlowFactor : 1f);
                _progress = Math.Min(1f, _progress + rate * dt);
            }
            if (_progress >= 1f) { Done = true; return new PumpTick(-1, true); }
            int start = -1;
            if (_waves < FloorObjectiveTuning.WaveCount && _progress >= FloorObjectiveTuning.WaveThresholds[_waves])
            {
                int aliveEarlier = 0;
                int[] living = { livingPerWave0, livingPerWave1, livingPerWave2 };
                for (int w = 0; w < _waves; w++) if (living[w] > 0) aliveEarlier++;
                if (aliveEarlier < FloorObjectiveTuning.MaxWavesAlive) { start = _waves; _waves++; }
            }
            return new PumpTick(start, false);
        }

        // Enemies in the next wave: WaveBase + (players - 1), trimmed so the floor stays within GameMetrics.MaxLiveEnemies.
        public static int WaveSize(int players, int liveEnemiesNow)
            => Math.Max(0, Math.Min(FloorObjectiveTuning.WaveBase + Math.Max(1, players) - 1, GameMetrics.MaxLiveEnemies - liveEnemiesNow));

        // Seconds between enemies of one wave: slower solo.
        public static float Stagger(int players) => players <= 1 ? FloorObjectiveTuning.WaveStagger * FloorObjectiveTuning.SoloStaggerFactor : FloorObjectiveTuning.WaveStagger;
    }
}
