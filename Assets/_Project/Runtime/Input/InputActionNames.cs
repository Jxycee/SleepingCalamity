// Runtime. Single source of action map/action/scheme names for SleepingCalamityInput.inputactions. No engine types.
namespace SleepingCalamity.Runtime
{
    public static class InputActionNames
    {
        public const string AssetPath = "Assets/_Project/Runtime/Input/SleepingCalamityInput.inputactions";

        public const string PlayerMap = "Player", UiMap = "UI";

        public const string Move = "Player/Move", Look = "Player/Look", Jump = "Player/Jump", Sprint = "Player/Sprint",
            Primary = "Player/Primary", Secondary = "Player/Secondary", Interact = "Player/Interact", Pause = "Player/Pause",
            Lock = "Player/Lock", SwitchTarget = "Player/SwitchTarget", Dodge = "Player/Dodge",   // M2. Primary is the attack button (M3).
            UseItem = "Player/UseItem";   // PL-2 2.5: eat a loaf (F, gamepad D-pad up; Q is the lock-on switch)

        public const string Navigate = "UI/Navigate", Submit = "UI/Submit", Cancel = "UI/Cancel", Point = "UI/Point", Click = "UI/Click";

        public const string KeyboardMouseScheme = "Keyboard&Mouse", GamepadScheme = "Gamepad";

        public static readonly string[] PlayerActions =
            { "Move", "Look", "Jump", "Sprint", "Primary", "Secondary", "Interact", "Pause", "Lock", "SwitchTarget", "Dodge", "UseItem" };
        public static readonly string[] UiActions = { "Navigate", "Submit", "Cancel", "Point", "Click" };
    }
}
