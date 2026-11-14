// Authority: test-only (no networked state). PL-4 task 4.1: the pause menu driven through the REAL App: the PauseMenuView on the App's PauseMenu child object (built by
// SceneBuilder), the real PauseMenuModel AppBootstrap composes, the real PlayerInputAdapter (a real keyboard device presses Escape), the real SceneFlow and WorldSaveFlow, and
// the real solo session (StartLocalAsync(false), the greybox arena). Controls are pressed the way Enter does: a NavigationSubmit event on the named button.
// Catches: a menu that does not open on Pause, that leaves the cursor locked or the hero walking while it is up, a host that leaves without being asked, a leave that does not
// reach the Lobby with the world on disk, a downed hero that cannot open it, a client's leave that takes the host down.
// NOT covered: a gamepad press on a real pad, the Relay session, a client's own scene flow (the TestClient has no App, SceneFlow or scenes, so "the client at title" is its session
// going Offline and its menu closing; the title screen itself is FrontDoorFlowTests). Those stay on the manual 2-player walk (docs/superpowers/handoff/USER_TODO.md).
// Device input needs the editor to treat a batch-mode editor as focused (SetUp overrides, TearDown restores). No test presses Quit (it would stop the test editor).
using System;
using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SleepingCalamity.Tests.PlayMode
{
    public class PauseMenuFlowTests
    {
        private const float StepTimeout = 10f;

        private string saveDir;
        private AppBootstrap app;
        private PauseMenuView view;
        private IInputService input;
        private INetworkSession session;
        private PlayModeHarness.TestClient client;
        private Keyboard keyboard;

        private InputSettings.BackgroundBehavior savedBackground;
        private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;

        [SetUp]
        public void AcceptDeviceInputWithoutFocus()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (client != null) { yield return client.LeaveAndDestroy(); client = null; }
            yield return PlayModeHarness.TearDownApp();
            if (saveDir != null) { PlayModeHarness.EndSaveTest(saveDir); saveDir = null; }
        }

        // The App up with a throwaway world folder, a solo session running in the greybox arena, the local hero spawned.
        private IEnumerator BootSolo()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            saveDir = PlayModeHarness.BeginSaveTest();
            app = AppBootstrap.Current;
            view = app.GetComponentInChildren<PauseMenuView>(true);
            Assert.That(view, Is.Not.Null, "the App has no PauseMenuView (SceneBuilder must add it)");
            input = app.Services.Get<IInputService>();
            app.GetComponent<PlayerInputAdapter>().ForceFocusForTests();   // a batch-mode editor reports no window focus
            session = app.Services.Get<INetworkSession>();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            keyboard = InputSystem.AddDevice<Keyboard>("PauseKeyboard");
        }

        private IEnumerator TapKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static bool Displayed(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.parent)
                if (e.style.display == DisplayStyle.None) return false;
            return true;
        }

        private IEnumerator Press(string name)
        {
            Button button = null;
            yield return PlayModeHarness.WaitUntil(
                () => (button = view.PauseRoot?.Q<Button>(name)) != null && Displayed(button) && button.enabledInHierarchy, StepTimeout, "no pause control named " + name + " on screen");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return null;
        }

        private IEnumerator OpenWithEscape()
        {
            yield return TapKey(Key.Escape);
            yield return PlayModeHarness.WaitUntil(() => view.IsShown, StepTimeout, "Escape did not open the pause menu (context " + input.Context + ")");
        }

        [UnityTest]
        public IEnumerator Open_FreesCursor_HeroStopsTakingInput()
        {
            yield return BootSolo();
            Assert.That(input.Context, Is.EqualTo(InputContext.Free), "premise: the hero is on foot");

            yield return OpenWithEscape();
            Assert.That(input.Context, Is.EqualTo(InputContext.Menu));
            Assert.That(UnityEngine.Cursor.lockState, Is.EqualTo(CursorLockMode.None), "the cursor is free while the menu is up");   // headless editors report None always; the real check is the Menu context, which PlayerInputAdapter.ApplyCursor maps to a free cursor
            Assert.That(UnityEngine.Cursor.visible, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "the pause menu never stops time");

            // W held down: the hero does not move while the menu is up, and walks again once it is closed.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return null;
            Assert.That(input.Intent.MoveY, Is.EqualTo(0f), "the menu is up but the hero still gets Move");
            yield return Press(PauseMenuView.Ids.Resume);
            yield return PlayModeHarness.WaitUntil(() => !view.IsShown && input.Context == InputContext.Free, StepTimeout, "Resume did not close the menu");
            yield return PlayModeHarness.WaitUntil(() => input.Intent.MoveY > 0.5f, StepTimeout, "the hero did not get Move back after Resume");
        }

        // Gamepad Start opens the menu and, with the Player map off in the Menu context, must still close it (Player/Pause stays enabled on its own).
        [UnityTest]
        public IEnumerator GamepadStart_OpensAndCloses()
        {
            yield return BootSolo();
            Gamepad pad = InputSystem.AddDevice<Gamepad>("PausePad");
            try
            {
                yield return TapStart(pad);
                yield return PlayModeHarness.WaitUntil(() => view.IsShown, StepTimeout, "gamepad Start did not open the pause menu (context " + input.Context + ")");
                yield return TapStart(pad);
                yield return PlayModeHarness.WaitUntil(() => !view.IsShown && input.Context == InputContext.Free, StepTimeout, "gamepad Start did not close the pause menu (context " + input.Context + ")");
            }
            finally { InputSystem.RemoveDevice(pad); }
        }

        private static IEnumerator TapStart(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.Start));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Leave_AsHost_WorldSaved_SessionEnds()
        {
            yield return BootSolo();
            // The start already wrote the world once. Change the world in memory after that (a sentinel stamp plus a dirty mark). Any Write stamps SavedAtUtc, so the 10 s Tick flush would replace the
            // sentinel too: the files are asserted unchanged just before the leave is confirmed, so a rewrite after that point can only come from the leave itself.
            const string Sentinel = "1999-01-01T00:00:00.0000000Z";
            Assert.That(app.WorldSaveFlow.Store, Is.Not.Null, "no world store while hosting");
            string[] before = Directory.GetFiles(saveDir, "*.json", SearchOption.AllDirectories);
            Assert.That(before.Length, Is.GreaterThan(0), "the start wrote no world file");
            var textBefore = new System.Collections.Generic.Dictionary<string, string>();
            foreach (string f in before) textBefore[f] = File.ReadAllText(f);
            app.WorldSaveFlow.Store.Current.SavedAtUtc = Sentinel;
            app.WorldSaveFlow.Store.MarkDirty();
            yield return OpenWithEscape();

            yield return Press(PauseMenuView.Ids.Leave);
            Assert.That(app.PauseMenu.State, Is.EqualTo(PauseMenuState.Confirm), "the host is asked first");
            yield return null;
            Assert.That(session.State, Is.EqualTo(ConnState.Connected), "nothing has left yet");
            yield return Press(PauseMenuView.Ids.ConfirmNo);
            Assert.That(app.PauseMenu.State, Is.EqualTo(PauseMenuState.Main));
            Assert.That(session.State, Is.EqualTo(ConnState.Connected), "Stay keeps the game going");

            yield return Press(PauseMenuView.Ids.Leave);
            foreach (string f in before) Assert.That(File.ReadAllText(f), Is.EqualTo(textBefore[f]), "a file was rewritten before the leave was confirmed (the tick flush), so the test cannot tell the leave saved: " + f);
            yield return Press(PauseMenuView.Ids.ConfirmYes);
            yield return PlayModeHarness.WaitUntil(
                () => session.State == ConnState.Offline && SceneManager.GetActiveScene().name == SceneNames.Lobby, 30f, "the host's leave did not end the session and reach the Lobby");
            Assert.That(app.PauseMenu.IsOpen, Is.False);
            string[] files = Directory.Exists(saveDir) ? Directory.GetFiles(saveDir, "*.json", SearchOption.AllDirectories) : new string[0];
            Assert.That(files.Length, Is.GreaterThan(0), "no world file on disk after the host left");
            Assert.That(app.WorldSaveFlow.ListWorlds(WorldKind.Solo, IdentityKind.Local).Count, Is.EqualTo(1), "the solo world is in the library");
            bool rewritten = false;
            foreach (string f in files)
            {
                string now = File.ReadAllText(f);
                if (textBefore.TryGetValue(f, out string old) && now != old && !now.Contains(Sentinel)) rewritten = true;
            }
            Assert.That(rewritten, Is.True, "the leave did not write the world: no file changed from its start-of-session content");
        }

        [UnityTest]
        public IEnumerator Leave_AsClient_HostKeepsPlaying_ClientAtTitle()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            saveDir = PlayModeHarness.BeginSaveTest();
            app = AppBootstrap.Current;
            session = app.Services.Get<INetworkSession>();
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            var clients = new System.Collections.Generic.List<PlayModeHarness.TestClient>();
            yield return PlayModeHarness.StartClients(app, 1, declineScenes: true, into: clients);
            client = null;   // owned by the harness (TearDownApp disposes started clients)
            PlayModeHarness.TestClient guest = clients[0];
            Assert.That(guest.Session.State, Is.EqualTo(ConnState.Connected));

            // The client's own pause menu, over its own session and a fake input service on foot.
            var clientInput = new FakeInputService { GameplayContext = InputContext.Free };
            bool quit = false;
            var menu = new PauseMenuModel(guest.Session, clientInput, () => quit = true);
            try
            {
                Assert.That(menu.Pause(InputContext.Free), Is.EqualTo(PauseOutcome.Opened));
                Assert.That(clientInput.MenuOpen, Is.True);
                menu.Leave();
                Assert.That(menu.State, Is.EqualTo(PauseMenuState.Closed), "a client is not asked");
                yield return PlayModeHarness.WaitUntil(() => guest.Session.State == ConnState.Offline, 20f, "the client's leave did not take it offline");
                Assert.That(clientInput.MenuOpen, Is.False);
                Assert.That(quit, Is.False);
            }
            finally { menu.Dispose(); }

            yield return PlayModeHarness.WaitUntil(() => app.NetworkManager.ConnectedClientsIds.Count == 1, 20f, "the host did not see the client go");
            Assert.That(session.State, Is.EqualTo(ConnState.Connected), "the host keeps its session");
            Assert.That(app.NetworkManager.IsListening, Is.True, "the host keeps playing");
        }

        [UnityTest]
        public IEnumerator Downed_CanStillOpen()
        {
            yield return BootSolo();
            HeroVitals vitals = app.NetworkManager.LocalClient.PlayerObject.GetComponent<HeroVitals>();
            vitals.ServerDamage(100000);
            Assert.That(vitals.IsDowned, Is.True, "premise: the hero is down");
            yield return OpenWithEscape();
            Assert.That(input.Context, Is.EqualTo(InputContext.Menu));
            yield return Press(PauseMenuView.Ids.Resume);
            yield return PlayModeHarness.WaitUntil(() => !view.IsShown, StepTimeout, "Resume did not close the menu on a downed hero");
        }
    }
}
