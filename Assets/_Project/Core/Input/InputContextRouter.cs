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
                case InputContext.LockOn: return RouteLockOn(in frame);
                case InputContext.Placement: return RoutePlacement(in frame);
                case InputContext.BankScreen: return RouteBankScreen(in frame);
                default: return RouteFree(in frame);
            }
        }

        // World interactables (PlayerInteractor): on foot, locked on or not.
        public static bool InteractLive(InputContext context)
        {
            return context == InputContext.Free || context == InputContext.LockOn;
        }

        // Menu -> UI only; everything else -> Player.
        public static InputMaps EnabledMaps(InputContext context)
        {
            return context == InputContext.Menu ? InputMaps.UI : InputMaps.Player;
        }

        // LockOn: everything Free passes, except Look is zeroed (the camera follows the target) and the switch input passes.
        private static PlayerIntent RouteLockOn(in PlayerInputFrame f)
        {
            PlayerIntent i = RouteFree(in f);
            i.LookYawDeg = 0f;
            i.LookPitchDeg = 0f;
            i.SwitchX = f.SwitchX;
            i.SwitchY = f.SwitchY;
            return i;
        }

        // Placement: every gameplay field is zeroed (the hero stands still; nothing swings, jumps, locks or interacts with the world) and the same buttons arrive on the placement-only
        // fields instead: switch input -> PlaceMoveX/Y (to the neighbouring lot), Primary -> PlacePressed, Secondary -> PickUpPressed, Interact -> NextLotPressed, Jump -> TurnPressed.
        // Pause passes so the menu still opens (placement mode leaves when it does).
        private static PlayerIntent RoutePlacement(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                PlaceMoveX = f.SwitchX,
                PlaceMoveY = f.SwitchY,
                PlacePressed = f.PrimaryPressed,
                PickUpPressed = f.SecondaryPressed,
                NextLotPressed = f.InteractPressed,
                TurnPressed = f.JumpPressed,
                PausePressed = f.PausePressed,
            };
        }

        // BankScreen (the bank room's screen): the hero stands still and swings, jumps and interacts with nothing (so a stray press cannot push a party on, and a cursor step does not walk the
        // hero out of the bank's range). Move -> BankMoveY, Jump -> BankPickPressed, Primary -> BankPushOnPressed, Secondary -> BankGoHomePressed. Look and Pause pass (the camera still turns,
        // the menu still opens).
        private static PlayerIntent RouteBankScreen(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                LookYawDeg = f.LookYawDeg,
                LookPitchDeg = f.LookPitchDeg,
                PausePressed = f.PausePressed,
                BankMoveY = f.MoveY,
                BankPickPressed = f.JumpPressed,
                BankPushOnPressed = f.PrimaryPressed,
                BankGoHomePressed = f.SecondaryPressed,
            };
        }

        // Free: the switch input is zeroed (the same stick is the camera's Look).
        private static PlayerIntent RouteFree(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                LockPressed = f.LockPressed,
                DodgePressed = f.DodgePressed,
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
                InteractHeld = f.InteractHeld,
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
