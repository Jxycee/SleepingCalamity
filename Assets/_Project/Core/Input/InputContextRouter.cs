// Core, no engine references, unit-tested. Decides which raw frame fields count in each context.
// Free passes everything; Menu passes Pause only (the UI map reads the rest). New contexts (lock-on, guard, dodge, seated) add a row here and a router test.
namespace SleepingCalamity.Core
{
    public static class InputContextRouter
    {
        public static PlayerIntent Route(InputContext context, in PlayerInputFrame frame)
        {
            switch (context)
            {
                case InputContext.Menu: return RouteMenu(in frame);
                default: return RouteFree(in frame);
            }
        }

        // World interactables (PlayerInteractor): only on foot.
        public static bool InteractLive(InputContext context)
        {
            return context == InputContext.Free;
        }

        // Menu -> UI only; everything else -> Player.
        public static InputMaps EnabledMaps(InputContext context)
        {
            return context == InputContext.Menu ? InputMaps.UI : InputMaps.Player;
        }

        private static PlayerIntent RouteFree(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                MoveX = f.MoveX,
                MoveY = f.MoveY,
                LookYawDeg = f.LookYawDeg,
                LookPitchDeg = f.LookPitchDeg,
                JumpPressed = f.JumpPressed,
                SprintHeld = f.SprintHeld,
                PrimaryHeld = f.PrimaryHeld,
                PrimaryPressed = f.PrimaryPressed,
                PrimaryReleased = f.PrimaryReleased,
                SecondaryHeld = f.SecondaryHeld,
                SecondaryPressed = f.SecondaryPressed,
                SecondaryReleased = f.SecondaryReleased,
                InteractPressed = f.InteractPressed,
                PausePressed = f.PausePressed,
            };
        }

        // Menu: Pause passes. Everything else zeroed (look included).
        private static PlayerIntent RouteMenu(in PlayerInputFrame f)
        {
            return new PlayerIntent { PausePressed = f.PausePressed };
        }
    }
}
