// Authority: none (pure EditMode tests). SleepingCalamity.Core.BankScreenInput: what the bank screen's real buttons (routed through InputContext.BankScreen) and the dev keys mean each frame.
// Catches: a stick that repeats while held or fires on entry, a button wired to the wrong request, the dev keys no longer an additional path, a cursor that does not wrap, and a held button
// reaching the screen before it is armed. NOT covered (PlayMode, BankScreenRealInput tests in DiveSpineNetworkTests): the director applying the action and the host accepting the request.
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class BankScreenInputTests
    {
        private static readonly BankScreenDevPresses NoDev = default;

        // Route a raw frame through the real router (BankScreen context) so these tests prove the whole chain from raw buttons to the action.
        private static PlayerIntent Routed(PlayerInputFrame f) => InputContextRouter.Route(InputContext.BankScreen, in f);

        private static BankScreenAction Read(float prevY, PlayerInputFrame f, BankScreenDevPresses dev = default, bool armed = true)
        {
            PlayerIntent i = Routed(f);
            return BankScreenInput.Read(prevY, in i, in dev, armed);
        }

        [Test]
        public void StickUp_CrossingTheThreshold_StepsTheCursorUpOnce()
        {
            BankScreenAction a = Read(0f, new PlayerInputFrame { MoveY = 1f });
            Assert.AreEqual(-1, a.CursorDelta);
            Assert.IsFalse(a.Pick || a.PushOn || a.GoHome);
        }

        [Test]
        public void StickDown_CrossingTheThreshold_StepsTheCursorDownOnce()
        {
            Assert.AreEqual(1, Read(0f, new PlayerInputFrame { MoveY = -1f }).CursorDelta);
        }

        [Test]
        public void StickHeld_DoesNotRepeat_AndAFreshPressAfterReleaseSteps()
        {
            float prev = 0f;
            int steps = 0;
            for (int frame = 0; frame < 30; frame++)
            {
                BankScreenAction a = Read(prev, new PlayerInputFrame { MoveY = 1f });
                prev = a.MoveY; steps += a.CursorDelta;
            }
            Assert.AreEqual(-1, steps, "thirty held frames step once");
            BankScreenAction released = Read(prev, new PlayerInputFrame { MoveY = 0f });
            Assert.AreEqual(0, released.CursorDelta);
            Assert.AreEqual(-1, Read(released.MoveY, new PlayerInputFrame { MoveY = 1f }).CursorDelta, "pressing again steps again");
        }

        [Test]
        public void StickBelowThreshold_Or_CreepingAcrossIt_StepsOnceOnTheCrossing()
        {
            Assert.AreEqual(0, Read(0f, new PlayerInputFrame { MoveY = 0.59f }).CursorDelta, "a half push is not a step");
            Assert.AreEqual(-1, Read(0.59f, new PlayerInputFrame { MoveY = 0.61f }).CursorDelta, "the crossing steps");
            Assert.AreEqual(0, Read(0.61f, new PlayerInputFrame { MoveY = 0.9f }).CursorDelta, "already over: no second step");
        }

        [Test]
        public void StickFlippingFromUpToDown_InOneFrame_StepsDown()
        {
            Assert.AreEqual(1, Read(1f, new PlayerInputFrame { MoveY = -1f }).CursorDelta);
        }

        [Test]
        public void EachButton_SetsItsOwnFlag_AndNothingElse()
        {
            BankScreenAction pick = Read(0f, new PlayerInputFrame { JumpPressed = true });
            Assert.IsTrue(pick.Pick); Assert.IsFalse(pick.PushOn || pick.GoHome); Assert.AreEqual(0, pick.CursorDelta);
            BankScreenAction push = Read(0f, new PlayerInputFrame { PrimaryPressed = true });
            Assert.IsTrue(push.PushOn); Assert.IsFalse(push.Pick || push.GoHome);
            BankScreenAction home = Read(0f, new PlayerInputFrame { SecondaryPressed = true });
            Assert.IsTrue(home.GoHome); Assert.IsFalse(home.Pick || home.PushOn);
        }

        // Catches: Interact (or any other raw button) doing something in the room: a stray press must never push a solo party on.
        [Test]
        public void InteractSprintDodgeAndHeldButtons_DoNothing()
        {
            var f = new PlayerInputFrame { InteractPressed = true, InteractHeld = true, SprintHeld = true, DodgePressed = true, LockPressed = true, PrimaryHeld = true, SecondaryHeld = true, MoveX = 1f };
            BankScreenAction a = Read(0f, f);
            Assert.AreEqual(0, a.CursorDelta);
            Assert.IsFalse(a.Pick || a.PushOn || a.GoHome);
        }

        [Test]
        public void NotArmed_IntentFiresNothing_ButStillReportsTheStick()
        {
            BankScreenAction a = Read(0f, new PlayerInputFrame { MoveY = 1f, JumpPressed = true, PrimaryPressed = true, SecondaryPressed = true }, NoDev, armed: false);
            Assert.AreEqual(0, a.CursorDelta);
            Assert.IsFalse(a.Pick || a.PushOn || a.GoHome);
            Assert.AreEqual(1f, a.MoveY, 1e-6f, "the caller keeps it as prev, so a stick held on entry is not a fresh press once armed");
            Assert.AreEqual(0, Read(a.MoveY, new PlayerInputFrame { MoveY = 1f }).CursorDelta, "armed next frame with the stick still up: no step");
        }

        [Test]
        public void DevKeys_AreAnAdditionalPath_NotGatedByArming()
        {
            var dev = new BankScreenDevPresses { Prev = true };
            Assert.AreEqual(-1, Read(0f, default, dev).CursorDelta);
            dev = new BankScreenDevPresses { Next = true };
            Assert.AreEqual(1, Read(0f, default, dev, armed: false).CursorDelta);
            Assert.IsTrue(Read(0f, default, new BankScreenDevPresses { Pick = true }, armed: false).Pick);
            Assert.IsTrue(Read(0f, default, new BankScreenDevPresses { PushOn = true }, armed: false).PushOn);
            Assert.IsTrue(Read(0f, default, new BankScreenDevPresses { GoHome = true }, armed: false).GoHome);
        }

        [Test]
        public void DevKeyAndButton_InTheSameFrame_Combine()
        {
            BankScreenAction a = Read(0f, new PlayerInputFrame { MoveY = 1f, PrimaryPressed = true }, new BankScreenDevPresses { Next = true, PushOn = true });
            Assert.AreEqual(0, a.CursorDelta, "one up and one down cancel");
            Assert.IsTrue(a.PushOn, "either path pushes on (once: it is one flag)");
        }

        [Test]
        public void Step_WrapsBothWays_AndSurvivesNoRows()
        {
            Assert.AreEqual(2, BankScreenInput.Step(0, -1, 3));
            Assert.AreEqual(0, BankScreenInput.Step(2, 1, 3));
            Assert.AreEqual(1, BankScreenInput.Step(1, 3, 3));
            Assert.AreEqual(2, BankScreenInput.Step(0, -4, 3));
            Assert.AreEqual(0, BankScreenInput.Step(5, 1, 0));
        }
    }
}
