// Not networked; words only. The pause menu's own text (PL-4 task 4.1), kept apart from Core/Game/UiText so that file stays the front door's.
namespace SleepingCalamity.App
{
    public static class PauseMenuText
    {
        public const string Heading = "Paused";
        public const string ControlsHeading = "Controls";
        public const string Resume = "Resume";
        public const string Controls = "Controls";
        public const string LeaveToTitle = "Leave to title";
        public const string Quit = "Quit";
        public const string Back = "Back";
        public const string StayButton = "Stay";
        public const string ConfirmLeaveButton = "Leave";
        public const string ConfirmQuitButton = "Quit";
        public const string TimeKeepsGoing = "The game keeps going while this is open.";

        // Asked of the host before it leaves or quits: the session ends for everyone, and the world is saved first.
        public const string HostLeaveAsk = "You are hosting. If you leave, the game ends for everyone. Your world is saved.";
        public const string HostQuitAsk = "You are hosting. If you quit, the game ends for everyone. Your world is saved.";
        public const string SoloLeaveAsk = "Leave to the title screen? Your world is saved.";
        public const string SoloQuitAsk = "Quit the game? Your world is saved.";

        // The rows of the Controls screen: the words for each action in the game's action asset (the key is read from the asset, never typed here).
        public static readonly string[][] ControlRows =
        {
            new[] { "Move", "Player/Move" },
            new[] { "Look", "Player/Look" },
            new[] { "Jump", "Player/Jump" },
            new[] { "Sprint", "Player/Sprint" },
            new[] { "Attack", "Player/Primary" },
            new[] { "Guard", "Player/Secondary" },
            new[] { "Dodge", "Player/Dodge" },
            new[] { "Lock on", "Player/Lock" },
            new[] { "Switch target", "Player/SwitchTarget" },
            new[] { "Interact", "Player/Interact" },
            new[] { "Eat a loaf", "Player/UseItem" },
            new[] { "Pause", "Player/Pause" },
        };
    }
}
