// Authority: none (pure logic, local). The photosensitivity guard (hit-feedback.md section 12): with "reduce flashes" on, a flash is at most half brightness
// and at most three flashes start in any one second. With it off nothing is limited.
using System;

namespace SleepingCalamity.Core.Feedback
{
    public sealed class FlashLimiter
    {
        public const float ReducedMaxBrightness = 0.5f, WindowSeconds = 1f;
        public const int MaxFlashesPerWindow = 3;

        public bool Reduce;
        private readonly float[] _starts = new float[MaxFlashesPerWindow];
        private int _next, _filled;

        // Brightness 0..1 after the setting: unchanged normally, capped when reduced.
        public float Brightness(float flash)
        {
            if (flash < 0f) return 0f;
            if (flash > 1f) flash = 1f;
            return Reduce ? Math.Min(flash, ReducedMaxBrightness) : flash;
        }

        // Asks to START a flash at `now` (seconds). Always true with reduce off; with it on, false once three flashes began within the last second.
        public bool Allow(float now)
        {
            if (!Reduce) return true;
            if (_filled == MaxFlashesPerWindow && now - _starts[_next] < WindowSeconds) return false;
            _starts[_next] = now;
            _next = (_next + 1) % MaxFlashesPerWindow;
            if (_filled < MaxFlashesPerWindow) _filled++;
            return true;
        }
    }
}
