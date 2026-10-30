// Core, no engine references, unit-tested. Turns the SwitchTarget stick/keys into one-shot "flick" events and maps them to world space.
// The stick fires once when it passes FlickOn after being at rest; it re-arms only when it falls under FlickOff, so a held stick (or a
// keyboard key held down) switches once, not every frame.
using System;

namespace SleepingCalamity.Core
{
    public sealed class FlickDetector
    {
        private bool _armed;

        // Starts disarmed: a stick that is already pushed when the lock begins must return to rest before it can switch.
        public bool Update(float x, float y, out float flickX, out float flickY)
        {
            flickX = 0f;
            flickY = 0f;
            float magnitude = MathF.Sqrt(x * x + y * y);
            if (magnitude < TargetingConfig.FlickOff)
            {
                _armed = true;
                return false;
            }
            if (!_armed || magnitude < TargetingConfig.FlickOn) return false;
            _armed = false;
            flickX = x / magnitude;
            flickY = y / magnitude;
            return true;
        }

        public void Reset() => _armed = false;
    }

    public static class FlickInput
    {
        // Stick (x right, y up on the screen) to a flat world direction for a camera looking along yawDeg (0 = +Z, 90 = +X).
        public static void ToWorld(float cameraYawDeg, float stickX, float stickY, out float worldX, out float worldZ)
        {
            float yaw = cameraYawDeg * (MathF.PI / 180f);
            float sin = MathF.Sin(yaw);
            float cos = MathF.Cos(yaw);
            // forward = (sin, cos); right = (cos, -sin)
            worldX = cos * stickX + sin * stickY;
            worldZ = -sin * stickX + cos * stickY;
        }
    }
}
