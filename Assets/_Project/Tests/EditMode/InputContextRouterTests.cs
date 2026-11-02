// Authority: none (pure EditMode tests). SleepingCalamity.Core.InputContextRouter: the Free and Menu rows (FishGame's rod, swim, climb and seated rows did not come across).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class InputContextRouterTests
    {
        private static PlayerInputFrame AllActive()
        {
            return new PlayerInputFrame
            {
                MoveX = 0.5f,
                MoveY = -1f,
                LookYawDeg = 5f,
                LookPitchDeg = 2f,
                JumpPressed = true,
                SprintHeld = true,
                PrimaryHeld = true,
                PrimaryPressed = true,
                PrimaryReleased = true,
                SecondaryHeld = true,
                SecondaryPressed = true,
                SecondaryReleased = true,
                InteractPressed = true,
                InteractHeld = true,
                PausePressed = true,
                LockPressed = true,
                DodgePressed = true,
                SwitchX = 1f,
                SwitchY = -0.5f,
            };
        }

        [Test]
        public void InputContextRouter_Free_PassesEveryField()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Free, AllActive());
            Assert.AreEqual(0.5f, i.MoveX, 1e-6f);
            Assert.AreEqual(-1f, i.MoveY, 1e-6f);
            Assert.AreEqual(5f, i.LookYawDeg, 1e-6f);
            Assert.AreEqual(2f, i.LookPitchDeg, 1e-6f);
            Assert.IsTrue(i.JumpPressed, "Jump");
            Assert.IsTrue(i.SprintHeld, "Sprint");
            Assert.IsTrue(i.PrimaryHeld && i.PrimaryPressed && i.PrimaryReleased, "Primary");
            Assert.IsTrue(i.SecondaryHeld && i.SecondaryPressed && i.SecondaryReleased, "Secondary");
            Assert.IsTrue(i.InteractPressed, "Interact");
            Assert.IsTrue(i.InteractHeld, "Interact held (the revive hold)");
            Assert.IsTrue(i.PausePressed, "Pause");
            Assert.IsTrue(i.LockPressed, "Lock");
            Assert.IsTrue(i.DodgePressed, "Dodge");
        }

        // Catches: the camera stick doing double duty in Free (a look turn would also flick a target switch).
        [Test]
        public void InputContextRouter_Free_ZeroesTheSwitchInput()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Free, AllActive());
            Assert.AreEqual(0f, i.SwitchX);
            Assert.AreEqual(0f, i.SwitchY);
        }

        // Catches: LockOn leaking Look (the camera would fight the lock), or losing the switch input / buttons.
        [Test]
        public void InputContextRouter_LockOn_ZeroesLook_PassesSwitchAndTheRest()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.LockOn, AllActive());
            Assert.AreEqual(0f, i.LookYawDeg);
            Assert.AreEqual(0f, i.LookPitchDeg);
            Assert.AreEqual(1f, i.SwitchX, 1e-6f);
            Assert.AreEqual(-0.5f, i.SwitchY, 1e-6f);
            Assert.AreEqual(0.5f, i.MoveX, 1e-6f);
            Assert.AreEqual(-1f, i.MoveY, 1e-6f);
            Assert.IsTrue(i.LockPressed, "Lock toggles it off again");
            Assert.IsTrue(i.DodgePressed && i.JumpPressed && i.SprintHeld && i.PrimaryPressed && i.InteractPressed && i.InteractHeld && i.PausePressed);
        }

        // Catches: a Placement row that lets the hero walk or the camera turn while a building is being placed (Move/Look/Sprint/Dodge/Lock leaking), or one that drops a placement
        // button (switch input, place, pick up, next lot, turn, leave).
        [Test]
        public void InputContextRouter_Placement_ZeroesEveryGameplayField_PassesThePlacementButtonsOnTheirOwnFields()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Placement, AllActive());
            // Nothing the hero acts on gets through: a place press must not swing, a turn press must not jump, the stick must not flick a lock target.
            Assert.AreEqual(0f, i.MoveX); Assert.AreEqual(0f, i.MoveY);
            Assert.AreEqual(0f, i.LookYawDeg); Assert.AreEqual(0f, i.LookPitchDeg);
            Assert.AreEqual(0f, i.SwitchX); Assert.AreEqual(0f, i.SwitchY);
            Assert.IsFalse(i.JumpPressed || i.SprintHeld || i.DodgePressed || i.LockPressed || i.InteractPressed || i.InteractHeld);
            Assert.IsFalse(i.PrimaryHeld || i.PrimaryPressed || i.PrimaryReleased || i.SecondaryHeld || i.SecondaryPressed || i.SecondaryReleased);
            Assert.AreEqual(1f, i.PlaceMoveX, 1e-6f); Assert.AreEqual(-0.5f, i.PlaceMoveY, 1e-6f);
            Assert.IsTrue(i.PlacePressed, "place");
            Assert.IsTrue(i.PickUpPressed, "pick up");
            Assert.IsTrue(i.NextLotPressed, "next lot");
            Assert.IsTrue(i.TurnPressed, "turn");
            Assert.IsTrue(i.PausePressed, "leave");
            // And no other context leaks the placement fields.
            PlayerIntent free = InputContextRouter.Route(InputContext.Free, AllActive());
            Assert.IsFalse(free.PlacePressed || free.PickUpPressed || free.NextLotPressed || free.TurnPressed);
            Assert.AreEqual(0f, free.PlaceMoveX);
            Assert.AreEqual(InputMaps.Player, InputContextRouter.EnabledMaps(InputContext.Placement));
            Assert.IsFalse(InputContextRouter.InteractLive(InputContext.Placement), "world interactables are off while placing");
        }

        // Catches: a Menu row that lets look or move through (the camera would turn behind a menu); Pause zeroed (the menu could not close).
        [Test]
        public void InputContextRouter_Menu_PassesPauseOnly()
        {
            PlayerIntent i = InputContextRouter.Route(InputContext.Menu, AllActive());
            Assert.IsTrue(i.PausePressed, "Pause closes the menu");
            Assert.AreEqual(0f, i.MoveX);
            Assert.AreEqual(0f, i.MoveY);
            Assert.AreEqual(0f, i.LookYawDeg);
            Assert.AreEqual(0f, i.LookPitchDeg);
            Assert.IsFalse(i.JumpPressed || i.SprintHeld || i.InteractPressed || i.InteractHeld);
            Assert.IsFalse(i.PrimaryHeld || i.PrimaryPressed || i.PrimaryReleased);
            Assert.IsFalse(i.SecondaryHeld || i.SecondaryPressed || i.SecondaryReleased);
            Assert.IsFalse(i.LockPressed || i.DodgePressed);
            Assert.AreEqual(0f, i.SwitchX);
            Assert.AreEqual(0f, i.SwitchY);
        }

        [Test]
        public void InputContextRouter_EnabledMaps_MenuIsUiOnly()
        {
            Assert.AreEqual(InputMaps.Player, InputContextRouter.EnabledMaps(InputContext.Free));
            Assert.AreEqual(InputMaps.Player, InputContextRouter.EnabledMaps(InputContext.LockOn));
            Assert.AreEqual(InputMaps.UI, InputContextRouter.EnabledMaps(InputContext.Menu));
        }

        [Test]
        public void InputContextRouter_InteractLive_OnlyOnFoot()
        {
            Assert.IsTrue(InputContextRouter.InteractLive(InputContext.Free));
            Assert.IsTrue(InputContextRouter.InteractLive(InputContext.LockOn));
            Assert.IsFalse(InputContextRouter.InteractLive(InputContext.Menu));
        }
    }
}
