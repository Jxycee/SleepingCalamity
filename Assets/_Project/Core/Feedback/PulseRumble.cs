// Authority: none (pure logic, local). The rumble pulse mixer (hit-feedback.md section 4): each event adds a short pulse (low and high motor strength that
// fades linearly to zero over its duration); the output of the two motors is the MAX of the live pulses, scaled by the player's rumble setting and never above 1.
// The Presentation director ticks it and sends the result to IInputService.SetRumble; Stop() zeroes it (pause, focus loss, device change).
using System;

namespace SleepingCalamity.Core.Feedback
{
    public sealed class PulseRumble
    {
        public const int MaxPulses = 12;

        private struct Pulse { public float Low, High, Duration, Left; }
        private readonly Pulse[] _pulses = new Pulse[MaxPulses];
        private int _count;

        public float Scale = 1f;   // the rumble setting, 0..1
        public float Low { get; private set; }
        public float High { get; private set; }
        public int Active => _count;

        public void Add(float low, float high, float seconds)
        {
            if (seconds <= 0f || (low <= 0f && high <= 0f)) return;
            var p = new Pulse { Low = Clamp01(low), High = Clamp01(high), Duration = seconds, Left = seconds };
            if (_count == MaxPulses)
            {
                // Full: replace the pulse with the least time left (it is the closest to done).
                int weakest = 0;
                for (int i = 1; i < _count; i++) if (_pulses[i].Left < _pulses[weakest].Left) weakest = i;
                _pulses[weakest] = p;
            }
            else _pulses[_count++] = p;
            Mix();
        }

        public void Tick(float dt)
        {
            if (dt > 0f)
            {
                for (int i = _count - 1; i >= 0; i--)
                {
                    _pulses[i].Left -= dt;
                    if (_pulses[i].Left <= 0f) _pulses[i] = _pulses[--_count];
                }
            }
            Mix();
        }

        public void Stop()
        {
            _count = 0;
            Low = High = 0f;
        }

        private void Mix()
        {
            float lo = 0f, hi = 0f;
            for (int i = 0; i < _count; i++)
            {
                float k = _pulses[i].Left / _pulses[i].Duration;
                lo = Math.Max(lo, _pulses[i].Low * k);
                hi = Math.Max(hi, _pulses[i].High * k);
            }
            float s = Clamp01(Scale);
            Low = Clamp01(lo * s);
            High = Clamp01(hi * s);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
