// Core, no engine references. Raw per-frame input sample built by PlayerInputAdapter (named "frame", never "PlayerInput": that is a Unity type).
namespace SleepingCalamity.Core
{
    public struct PlayerInputFrame
    {
        public float MoveX, MoveY;                 // -1..1 (MoveY forward)
        public float LookYawDeg, LookPitchDeg;     // degrees this frame, already scaled/inverted by the adapter; pitch positive = look up
        public bool JumpPressed, SprintHeld;
        public bool PrimaryHeld, PrimaryPressed, PrimaryReleased;
        public bool SecondaryHeld, SecondaryPressed, SecondaryReleased;
        public bool InteractPressed;
        public bool PausePressed;                  // Player/Pause in gameplay contexts; UI/Cancel in Menu
    }
}
