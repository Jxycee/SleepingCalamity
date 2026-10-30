// Core, no engine references. Routed input: what gameplay is allowed to act on this frame.
namespace SleepingCalamity.Core
{
    public struct PlayerIntent
    {
        public float MoveX, MoveY, LookYawDeg, LookPitchDeg;
        public bool JumpPressed, SprintHeld;
        public bool PrimaryHeld, PrimaryPressed, PrimaryReleased;
        public bool SecondaryHeld, SecondaryPressed, SecondaryReleased;
        public bool LockPressed, DodgePressed;
        public float SwitchX, SwitchY;
        public bool InteractPressed, InteractHeld, PausePressed;

        public static readonly PlayerIntent None = default;
    }
}
