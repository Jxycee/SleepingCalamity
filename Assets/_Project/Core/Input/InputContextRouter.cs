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

        // Placement: the hero stands still and the camera is the placement camera, so Move, Look, Sprint, Dodge and Lock are zeroed. Passed: the switch input (move the ghost to the
        // neighbouring lot), Primary/Secondary pressed (place / pick up), Interact pressed (next free lot), Jump pressed (turn the building a quarter), Pause (leave placement).
        private static PlayerIntent RoutePlacement(in PlayerInputFrame f)
        {
            return new PlayerIntent
            {
                SwitchX = f.SwitchX,
                SwitchY = f.SwitchY,
                PrimaryPressed = f.PrimaryPressed,
                SecondaryPressed = f.SecondaryPressed,
                InteractPressed = f.InteractPressed,
                JumpPressed = f.JumpPressed,
                PausePressed = f.PausePressed,
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
