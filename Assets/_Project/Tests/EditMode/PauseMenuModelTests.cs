// PL-4 task 4.1: the pause menu's rules, with the front door's fake session and a fake input service. Catches: a menu that opens in placement or on the bank
// screen, a host that leaves without being asked, a client that is asked, a menu that leaves the cursor free after it closes, a leave that skips INetworkSession.
// NOT covered: the view and the real session, scenes and saves (PlayMode PauseMenuFlowTests).
using System;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.EditMode
{
    public class PauseMenuModelTests
    {
        private sealed class FakeInput : IInputService
        {
            private bool menuOpen;
            public InputContext Context => menuOpen ? InputContext.Menu : GameplayContext;
            public InputContext GameplayContext { get; set; } = InputContext.Free;
            public bool MenuOpen { get => menuOpen; set => menuOpen = value; }
            public PlayerIntent Intent => PlayerIntent.None;
            public bool HasFocus => true;
            public InputDeviceKind LastDevice => InputDeviceKind.KeyboardMouse;
            public event Action<InputContext> ContextChanged { add { } remove { } }
            public void SetRumble(float low, float high) { }
            public string GetBindingDisplayString(string actionPath) => actionPath == "Player/Interact" ? "E" : "";
        }

        private FakeNetworkSession session;
        private FakeInput input;
        private PauseMenuModel model;
        private int quits;

        [SetUp]
        public void SetUp()
        {
            session = new FakeNetworkSession();
            input = new FakeInput();
            quits = 0;
            model = new PauseMenuModel(session, input, () => quits++);
        }

        [TearDown]
        public void TearDown() => model.Dispose();

        private void Connect(bool host, bool solo = false)
        {
            if (host) session.StartLocalAsync(!solo, CancellationToken.None);
            else session.JoinDirectAsync("127.0.0.1", 1, CancellationToken.None);
        }

        [Test]
        public void Pause_InFree_Opens()
        {
            Connect(true);
            Assert.That(model.Pause(InputContext.Free), Is.EqualTo(PauseOutcome.Opened));
            Assert.That(model.State, Is.EqualTo(PauseMenuState.Main));
            Assert.That(input.MenuOpen, Is.True, "the Menu context frees the cursor");
            Assert.That(model.Pause(InputContext.Menu), Is.EqualTo(PauseOutcome.Closed), "a second Pause closes it");
            Assert.That(input.MenuOpen, Is.False);
        }

        [Test]
        public void Pause_InLockOn_Opens()
        {
            Connect(true);
            Assert.That(model.Pause(InputContext.LockOn), Is.EqualTo(PauseOutcome.Opened));
        }

        [Test]
        public void Pause_InPlacement_LeavesPlacementInstead()
        {
            Connect(true);
            Assert.That(model.Pause(InputContext.Placement), Is.EqualTo(PauseOutcome.LeftToPlacement));
            Assert.That(model.IsOpen, Is.False);
            Assert.That(input.MenuOpen, Is.False);
        }

        [Test]
        public void Pause_InBankScreen_DoesNotOpen()
        {
            Connect(true);
            Assert.That(model.Pause(InputContext.BankScreen), Is.EqualTo(PauseOutcome.Ignored));
            Assert.That(model.IsOpen, Is.False);
            Assert.That(input.MenuOpen, Is.False);
        }

        [Test]
        public void Pause_WhileAnotherMenuIsUp_DoesNotOpen()
        {
            Connect(true);
            input.MenuOpen = true;
            Assert.That(model.Pause(InputContext.Free), Is.EqualTo(PauseOutcome.Ignored));
            Assert.That(model.IsOpen, Is.False);
        }

        [Test]
        public void Pause_Offline_DoesNotOpen()
        {
            Assert.That(model.Pause(InputContext.Free), Is.EqualTo(PauseOutcome.Ignored));
        }

        [Test]
        public void HostLeave_AsksFirst()
        {
            Connect(true);
            model.Pause(InputContext.Free);
            model.Leave();
            Assert.That(model.State, Is.EqualTo(PauseMenuState.Confirm));
            Assert.That(model.ConfirmWords, Does.Contain("everyone").Or.Contain("saved"));
            Assert.That(session.Calls, Does.Not.Contain("Leave"), "nothing leaves before the host says yes");

            model.Back();
            Assert.That(model.State, Is.EqualTo(PauseMenuState.Main), "Stay returns to the menu");
            Assert.That(session.Calls, Does.Not.Contain("Leave"));

            model.Leave();
            model.Confirm();
            Assert.That(session.Calls, Does.Contain("Leave"));
            Assert.That(model.IsOpen, Is.False);
            Assert.That(input.MenuOpen, Is.False);
        }

        [Test]
        public void ClientLeave_DoesNotAsk()
        {
            Connect(false);
            model.Pause(InputContext.Free);
            model.Leave();
            Assert.That(session.Calls, Does.Contain("Leave"));
            Assert.That(model.IsOpen, Is.False);
            Assert.That(input.MenuOpen, Is.False, "the title's own menu hold is not stolen");
        }

        [Test]
        public void HostQuit_AsksFirst_ThenQuits()
        {
            Connect(true);
            model.Pause(InputContext.Free);
            model.Quit();
            Assert.That(model.State, Is.EqualTo(PauseMenuState.Confirm));
            Assert.That(quits, Is.EqualTo(0));
            model.Confirm();
            Assert.That(quits, Is.EqualTo(1));
        }

        [Test]
        public void ClientQuit_QuitsAtOnce()
        {
            Connect(false);
            model.Pause(InputContext.Free);
            model.Quit();
            Assert.That(quits, Is.EqualTo(1));
        }

        [Test]
        public void Controls_ReadBindingsFromTheInputService()
        {
            Connect(true);
            model.Pause(InputContext.Free);
            model.ShowControls();
            Assert.That(model.State, Is.EqualTo(PauseMenuState.Controls));
            bool found = false;
            foreach (var row in model.Controls) if (row.Key == "Interact") { found = true; Assert.That(row.Value, Is.EqualTo("E")); }
            Assert.That(found, Is.True);
            Assert.That(model.Pause(InputContext.Menu), Is.EqualTo(PauseOutcome.Back), "Pause steps back from Controls");
            Assert.That(model.State, Is.EqualTo(PauseMenuState.Main));
        }

        [Test]
        public void SessionEnding_ClosesTheMenu()
        {
            Connect(false);
            model.Pause(InputContext.Free);
            session.EndExternally(EndReason.HostLeft);
            Assert.That(model.IsOpen, Is.False);
            Assert.That(input.MenuOpen, Is.False);
        }
    }
}
