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

        // M6 S4 placement mode only: filled by the Placement router row (every gameplay field above stays zero there), so a place or turn press never jumps or swings.
        public float PlaceMoveX, PlaceMoveY;                  // move the ghost to the neighbouring lot (the switch stick)
        public bool PlacePressed, PickUpPressed, TurnPressed, NextLotPressed;

        public static readonly PlayerIntent None = default;
    }
}
