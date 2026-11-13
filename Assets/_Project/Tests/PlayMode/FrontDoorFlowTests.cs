// Authority: test-only (no networked state). PL-3 task 3.5: the front door driven through the REAL view of the real App (FrontDoorView on the App object, built by
// SceneBuilder; the real FrontDoorModel composed by AppBootstrap; the real SceneFlow, WorldSaveFlow, HostWorld start gate and ConnectionApprover), by pressing its named controls
// (a NavigationSubmit event on the button, or a real keyboard Enter on the focused one). Worlds are written to a throwaway folder (PlayModeHarness.BeginSaveTest).
//
// What is REAL: the screens, the model, the world library and its files, the solo start (StartLocalAsync(false), NGO scene load into Hollowmere), the host's start gate and
// player recording, the roster the lobby reads, and the host leaving.
// What is INJECTED, because Relay / UGS cannot run in a test: the online backend. AppBootstrap.OnlineSessionFactoryOverride hands the composite session a DirectAsOnlineSession,
// which maps HostOnlineAsync to the dev-direct host on loopback (StartLocalAsync(true)) with a fixed join code, and JoinOnlineAsync to JoinDirectAsync. Two consequences:
//  - the session reports its mode as DevDirect, so the world that opens is a Local-identity multiplayer world; the door's own pick of an online (Ugs) world is exercised only up to
//    "New world" and "Host", not through the save that opens. (Spoofing Online here would open a Ugs-identity world that the Local approver refuses.)
//  - the join-code lookup is a string compare in the stand-in, not UGS; a wrong code is refused with EndReason.InvalidCode the way UgsNetworkSession reports it.
// The GUEST is a second process-local peer (PlayModeHarness.TestClient) with no App, SceneFlow or scenes, so its door is a second FrontDoorView on a test-made UIDocument bound to a
// FrontDoorModel over the TestClient's session (through the same stand-in) with an empty host side. That proves the guest screens and the model against a real NGO client; it does
// NOT prove AppBootstrap's wiring on a guest (the App is the host in every test here), a guest's scene sync into Hollowmere (the TestClient declines scenes), or Relay itself.
// Those stay on the manual 2-player walk (docs/superpowers/handoff/USER_TODO.md).
// Device input needs the editor to treat a batch-mode editor as focused (SetUp overrides, TearDown restores). No test presses Quit (it would stop the test editor).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SleepingCalamity.Tests.PlayMode
{
    public class FrontDoorFlowTests
    {
        private const float StepTimeout = 10f;
        private const float SceneTimeout = 45f;

        private string saveDir;
        private AppBootstrap app;
        private FrontDoorView door;
        private PlayModeHarness.TestClient client;
        private DirectAsOnlineSession guestSession;
        private FrontDoorModel guestModel;
        private GameObject guestObject;
        private FrontDoorView guestDoor;

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
            AppBootstrap.OnlineSessionFactoryOverride = null;
            DevSessionPanel.DebugBuildOverrideForTests = null;
            if (guestObject != null) UnityEngine.Object.Destroy(guestObject);
            guestObject = null;
            guestModel?.Dispose();
            guestModel = null;
            guestSession?.Dispose();
            guestSession = null;
            if (client != null) { yield return client.LeaveAndDestroy(); client = null; }
            yield return PlayModeHarness.TearDownApp();
            if (saveDir != null) { PlayModeHarness.EndSaveTest(saveDir); saveDir = null; }
        }

        // ---- the stand-in for Relay ----

        private const string Code = "TEST42";

        // Maps the online calls onto the direct transport on loopback. forwardEvents is off for the App's composite (it already forwards the LocalNetworkSession's
        // events, so a second copy would double every one) and on for the guest, whose model hears the TestClient's LocalNetworkSession through this.
        private sealed class DirectAsOnlineSession : INetworkSession
        {
            private readonly LocalNetworkSession local;
            private readonly Action<ConnState, ConnState> onState;
            private readonly Action<EndReason> onEnded;

            public DirectAsOnlineSession(LocalNetworkSession local, bool forwardEvents)
            {
                this.local = local;
                if (!forwardEvents) return;
                onState = (a, b) => StateChanged?.Invoke(a, b);
                onEnded = r => Ended?.Invoke(r);
                local.StateChanged += onState;
                local.Ended += onEnded;
            }

            public ConnState State => local.State;
            public SessionMode Mode => local.Mode;
            public bool IsHost => local.IsHost;
            public string JoinCode => local.State == ConnState.Connected && local.IsHost ? Code : null;
            public event Action<ConnState, ConnState> StateChanged;
            public event Action<EndReason> Ended;

            public async Task<string> HostOnlineAsync(CancellationToken ct)
            {
                await local.StartLocalAsync(true, ct);
                return Code;
            }

            public async Task JoinOnlineAsync(string code, CancellationToken ct)
            {
                if (!string.Equals(code, Code, StringComparison.OrdinalIgnoreCase)) throw new NetworkSessionException(EndReason.InvalidCode, "no game has that code");
                await local.JoinDirectAsync(NetworkConstants.LoopbackAddress, NetworkConstants.TestPort, ct);
            }

            public Task StartLocalAsync(bool allowRemote, CancellationToken ct) => local.StartLocalAsync(allowRemote, ct);
            public Task JoinDirectAsync(string ip, ushort port, CancellationToken ct) => local.JoinDirectAsync(ip, port, ct);
            public Task SetJoinableAsync(bool open) => local.SetJoinableAsync(open);
            public Task LeaveAsync() => local.LeaveAsync();

            public void Dispose()
            {
                if (onState != null) local.StateChanged -= onState;
                if (onEnded != null) local.Ended -= onEnded;
            }
        }

        // A guest has no host side: nobody is connected to it and it can start nothing.
        private sealed class NoHost : IFrontDoorHost
        {
            public int ConnectedCount => 0;
            public void GetConnectedNames(List<string> into) => into.Clear();
            public bool TryStartSession() => false;
            public WorldPlayerVerdict LastStartVerdict => WorldPlayerVerdict.Ok;
        }

        // ---- set-up and driving helpers ----

        // Bootstrap up, a throwaway world folder, the test port, the door on its Title screen.
        private IEnumerator Boot(bool hollowmere)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            saveDir = PlayModeHarness.BeginSaveTest();
            app = AppBootstrap.Current;
            if (hollowmere) SceneFlow.StartSceneOverride = null;   // the real default: the town
            app.Services.Get<LocalNetworkSession>().Port = NetworkConstants.TestPort;
            door = app.GetComponent<FrontDoorView>();
            Assert.That(door, Is.Not.Null, "the App object has no FrontDoorView (SceneBuilder must add it)");
            yield return WaitShown(door);
            Assert.That(app.FrontDoor.State, Is.EqualTo(FrontDoorState.Title));
        }

        private static IEnumerator WaitShown(FrontDoorView view)
        {
            yield return PlayModeHarness.WaitUntil(() => view.IsShown && view.DoorRoot != null, StepTimeout, "the front door never showed");
        }

        private static bool Displayed(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.parent)
                if (e.style.display == DisplayStyle.None) return false;
            return true;
        }

        private static T Find<T>(FrontDoorView view, string name) where T : VisualElement => view.DoorRoot?.Q<T>(name);

        // The named control, once it is on screen.
        private static IEnumerator WaitControl<T>(FrontDoorView view, string name, Action<T> found) where T : VisualElement
        {
            T element = null;
            yield return PlayModeHarness.WaitUntil(
                () => (element = Find<T>(view, name)) != null && Displayed(element), StepTimeout, "no control named " + name + " on screen");
            found(element);
        }

        // Presses an enabled button the way Enter / gamepad south does: a NavigationSubmit event on it.
        private static IEnumerator Press(FrontDoorView view, string name)
        {
            Button button = null;
            yield return WaitControl<Button>(view, name, b => button = b);
            yield return PlayModeHarness.WaitUntil(() => button.enabledInHierarchy, StepTimeout, name + " stayed disabled");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return null;
        }

        private static IEnumerator WaitState(FrontDoorModel model, FrontDoorState state, string why, float timeout = StepTimeout)
        {
            yield return PlayModeHarness.WaitUntil(() => model.State == state, timeout, why + " (door state is " + model.State + ")");
        }

        private static IEnumerator WaitLabel(FrontDoorView view, string name, Func<string, bool> ok, string why)
        {
            yield return PlayModeHarness.WaitUntil(() =>
            {
                Label label = Find<Label>(view, name);
                return label != null && Displayed(label) && ok(label.text);
            }, StepTimeout, why);
        }

        private static IEnumerator TapKey(Key key)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("FlowKeyboard");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        // The Lobby is a menu: the input service must say Menu while it is up (that unlocks and shows the cursor), and Free once the town has loaded.
        private static IEnumerator ExpectMenuContext(AppBootstrap host)
        {
            var input = host.Services.Get<IInputService>();
            yield return PlayModeHarness.WaitUntil(() => input.Context == InputContext.Menu, StepTimeout, "the Lobby never opened the Menu input context; it is " + input.Context);
        }

        private static IEnumerator ExpectFreeContextInTheTown(AppBootstrap host)
        {
            var input = host.Services.Get<IInputService>();
            yield return PlayModeHarness.WaitUntil(() => input.Context == InputContext.Free, StepTimeout, "the Menu context stayed on in the town; it is " + input.Context);
        }

        private static IEnumerator WaitInHollowmere(AppBootstrap host)
        {
            yield return PlayModeHarness.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Hollowmere && host.NetworkManager.LocalClient != null && host.NetworkManager.LocalClient.PlayerObject != null,
                SceneTimeout, "the host never spawned in Hollowmere");
        }

        // Title -> Multiplayer -> New world -> Host, with the online backend swapped for the direct transport. Ends in the host lobby.
        private IEnumerator HostThroughTheDoor()
        {
            LocalNetworkSession local = app.Services.Get<LocalNetworkSession>();
            AppBootstrap.OnlineSessionFactoryOverride = () => new DirectAsOnlineSession(local, false);
            app.NetworkManager.NetworkConfig.LoadSceneTimeOut = 3;   // a guest that declines scenes (one process) must not hold the host's scene load for minutes
            yield return Press(door, FrontDoorView.Ids.Multiplayer);
            yield return WaitState(app.FrontDoor, FrontDoorState.Worlds, "Multiplayer did not open the worlds list");
            yield return Press(door, FrontDoorView.Ids.NewWorld);
            yield return WaitControl<Button>(door, FrontDoorView.Ids.WorldRow(1), _ => { });
            yield return Press(door, FrontDoorView.Ids.Host);
            yield return WaitState(app.FrontDoor, FrontDoorState.HostLobby, "Host did not reach the lobby");
        }

        // A TestClient (declines scenes) whose own door is a second FrontDoorView over its session, sitting on the Title screen.
        private IEnumerator MakeGuestDoor()
        {
            client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(5), declineScenes: true);
            guestSession = new DirectAsOnlineSession(client.Session, true);
            guestModel = new FrontDoorModel(guestSession, app.WorldSaveFlow, new NoHost());

            var appDocument = app.GetComponent<UIDocument>();
            guestObject = new GameObject("GuestDoor");
            guestObject.SetActive(false);
            var document = guestObject.AddComponent<UIDocument>();
            document.panelSettings = appDocument.panelSettings;
            document.visualTreeAsset = appDocument.visualTreeAsset;
            guestDoor = guestObject.AddComponent<FrontDoorView>();
            guestDoor.Bind(guestModel, guestSession);
            guestObject.SetActive(true);
            yield return WaitShown(guestDoor);
        }

        // Guest: Title -> Join -> a wrong code is refused with words -> the right code -> waiting for the host.
        private IEnumerator GuestJoinsThroughTheDoor()
        {
            yield return Press(guestDoor, FrontDoorView.Ids.Join);
            yield return WaitState(guestModel, FrontDoorState.JoinEntry, "Join did not open the code screen");

            var join = Find<Button>(guestDoor, FrontDoorView.Ids.JoinGo);
            Assert.That(join.enabledSelf, Is.False, "Join is off with no code typed");
            yield return WaitLabel(guestDoor, FrontDoorView.Ids.JoinWhy, t => t == UiText.JoinCodeEmpty, "the empty-code reason words");

            var field = Find<TextField>(guestDoor, FrontDoorView.Ids.CodeField);
            field.value = "WRONG1";
            yield return PlayModeHarness.WaitUntil(() => join.enabledSelf, StepTimeout, "Join stayed off with a plausible code");
            yield return Press(guestDoor, FrontDoorView.Ids.JoinGo);
            yield return WaitLabel(guestDoor, FrontDoorView.Ids.JoinError, t => t == UiText.JoinRefusal(EndReason.InvalidCode), "the wrong-code refusal words");
            Assert.That(guestModel.State, Is.EqualTo(FrontDoorState.JoinEntry), "a refused join goes back to the code screen");

            field.value = Code.ToLowerInvariant();   // the model normalises
            yield return Press(guestDoor, FrontDoorView.Ids.JoinGo);
            yield return WaitState(guestModel, FrontDoorState.WaitingForHost, "the guest never reached the waiting screen", 20f);
        }

        // ---- solo ----

        // Catches: a Title that does not lead to a playable solo game through its own buttons: Play solo -> world list -> New world -> Play (a real Enter press) must make a Local
        // solo world file, start the solo host, load Hollowmere with the player in it, and hide the door.
        [UnityTest]
        public IEnumerator Solo_NewWorld_Play_LandsInHollowmere()
        {
            yield return Boot(hollowmere: true);

            yield return ExpectMenuContext(app);
            Label name = Find<Label>(door, FrontDoorView.Ids.GameName);
            Assert.That(name.text, Is.EqualTo("Sleeping Calamity"));
            yield return Press(door, FrontDoorView.Ids.PlaySolo);
            yield return WaitState(app.FrontDoor, FrontDoorState.Worlds, "Play solo did not open the worlds list");
            Assert.That(app.FrontDoor.WorldsKind, Is.EqualTo(WorldKind.Solo));
            Assert.That(app.FrontDoor.Rows.Count, Is.EqualTo(0), "a fresh profile has no worlds");

            yield return Press(door, FrontDoorView.Ids.NewWorld);
            yield return WaitControl<Button>(door, FrontDoorView.Ids.WorldRow(1), _ => { });
            Assert.That(app.FrontDoor.Rows.Count, Is.EqualTo(1));
            Assert.That(app.FrontDoor.Rows[0].Selected, Is.True, "the new world is selected");

            // Focus moved to Play, so a real keyboard Enter plays the world.
            Button play = Find<Button>(door, FrontDoorView.Ids.Play);
            yield return PlayModeHarness.WaitUntil(() => play.focusController?.focusedElement == play, StepTimeout, "Play never got focus after New world");
            yield return TapKey(Key.Enter);

            yield return WaitInHollowmere(app);
            yield return ExpectFreeContextInTheTown(app);
            Assert.That(app.World.Started, Is.True);
            Assert.That(app.World.Players.Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(app.FrontDoor.Rows[0].Id), "it opened the world the list showed");
            yield return PlayModeHarness.WaitUntil(() => !door.IsShown, StepTimeout, "the door stayed on screen over Hollowmere");
        }

        // Catches: a second press of Play landing on Cancel (the Connecting screen's focused button) and abandoning the start the player just made.
        [UnityTest]
        public IEnumerator Solo_DoublePressPlay_DoesNotCancelTheStart()
        {
            yield return Boot(hollowmere: true);
            yield return Press(door, FrontDoorView.Ids.PlaySolo);
            yield return WaitState(app.FrontDoor, FrontDoorState.Worlds, "Play solo did not open the worlds list");
            yield return Press(door, FrontDoorView.Ids.NewWorld);
            yield return WaitControl<Button>(door, FrontDoorView.Ids.WorldRow(1), _ => { });
            yield return Press(door, FrontDoorView.Ids.Play);

            // The second press arrives as soon as the Connecting screen is up: on Cancel, which must ignore it for a moment.
            Button cancel = null;
            yield return WaitControl<Button>(door, FrontDoorView.Ids.ConnectingCancel, b => cancel = b);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = cancel;
                cancel.SendEvent(submit);
            }
            yield return WaitInHollowmere(app);
            Assert.That(app.World.Started, Is.True, "the second press abandoned the start");
        }

        // Catches: Play opening the newest world instead of the one picked in the list. The OLDER of two worlds is selected, so the default (newest) could not pass.
        [UnityTest]
        public IEnumerator Solo_Continue_OpensTheSelectedWorld()
        {
            yield return Boot(hollowmere: true);
            yield return Press(door, FrontDoorView.Ids.PlaySolo);
            yield return WaitState(app.FrontDoor, FrontDoorState.Worlds, "Play solo did not open the worlds list");
            yield return Press(door, FrontDoorView.Ids.NewWorld);
            yield return WaitControl<Button>(door, FrontDoorView.Ids.WorldRow(1), _ => { });
            yield return Press(door, FrontDoorView.Ids.NewWorld);
            yield return WaitControl<Button>(door, FrontDoorView.Ids.WorldRow(2), _ => { });
            Assert.That(app.FrontDoor.Rows.Count, Is.EqualTo(2));

            string newest = app.FrontDoor.Rows[0].Id;
            string older = app.FrontDoor.Rows[1].Id;
            Assert.That(older, Is.Not.EqualTo(newest));
            yield return Press(door, FrontDoorView.Ids.WorldRow(2));
            yield return PlayModeHarness.WaitUntil(() => app.FrontDoor.Rows[1].Selected && !app.FrontDoor.Rows[0].Selected, StepTimeout, "clicking the second row did not select it");
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(older));

            yield return Press(door, FrontDoorView.Ids.Play);
            yield return WaitInHollowmere(app);
            Assert.That(app.WorldSaveFlow.Store, Is.Not.Null);
            Assert.That(app.WorldSaveFlow.Store.Current.WorldId, Is.EqualTo(older), "Play opened a different world than the selected one");
        }

        // ---- host ----

        // Catches: the host lobby not showing the join code, not listing who is in, Start being on with one player, Start not going through SceneFlow's gate with two, or the door
        // staying on screen once Hollowmere is loaded.
        [UnityTest]
        public IEnumerator Host_TwoPlayers_Start_BothInHollowmere()
        {
            yield return Boot(hollowmere: true);
            yield return HostThroughTheDoor();
            yield return ExpectMenuContext(app);

            yield return WaitLabel(door, FrontDoorView.Ids.JoinCode, t => t == Code, "the join code");
            Assert.That(app.Services.Get<INetworkSession>().JoinCode, Is.EqualTo(Code));
            yield return Press(door, FrontDoorView.Ids.CopyCode);
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(Code), "Copy put the code on the clipboard");
            yield return WaitControl<Label>(door, FrontDoorView.Ids.Player(1), _ => { });
            Assert.That(Find<Button>(door, FrontDoorView.Ids.Start).enabledSelf, Is.False, "Start is off with one player");

            // The second player is in the host's roster when they connect (the world records them at the start, audit A2); the lobby lists them and Start comes on.
            client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(5), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the second player never reached the roster");
            yield return WaitControl<Label>(door, FrontDoorView.Ids.Player(2), _ => { });
            yield return PlayModeHarness.WaitUntil(() => Find<Button>(door, FrontDoorView.Ids.Start).enabledSelf, StepTimeout, "Start stayed off with two players");

            yield return Press(door, FrontDoorView.Ids.Start);
            yield return WaitInHollowmere(app);
            yield return ExpectFreeContextInTheTown(app);
            Assert.That(app.World.Started, Is.True);
            Assert.That(app.World.Players.Kind, Is.EqualTo(WorldKind.Multiplayer));
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(2), "both players are recorded in the world");
            Assert.That(app.World.ScalePlayers(app.Roster), Is.EqualTo(2));
            // The guest stays connected through the start. Its own hero is not asserted: the in-process TestClient declines the scene load, so the host's spawner (clients that
            // completed the load) never makes one for it; SquareLoopWalkQaTests spawns that hero by hand for the same reason.
            Assert.That(client.NetworkManager.IsConnectedClient, Is.True, "the guest dropped when the host started");
            Assert.That(app.Roster.ConnectedCount, Is.EqualTo(2));
            yield return PlayModeHarness.WaitUntil(() => !door.IsShown, StepTimeout, "the door stayed on screen over Hollowmere");
        }

        // Catches: Start being pressable alone, or the host's gate letting a one-player multiplayer world start. The button is off with the reason in words; even a forced
        // press (the model's own command, SceneFlow's gate) starts nothing and the host stays in the lobby.
        [UnityTest]
        public IEnumerator Host_Alone_CannotStart()
        {
            yield return Boot(hollowmere: true);
            yield return HostThroughTheDoor();

            Button start = Find<Button>(door, FrontDoorView.Ids.Start);
            yield return PlayModeHarness.WaitUntil(() => !start.enabledSelf, StepTimeout, "Start should be off with one player");
            yield return WaitLabel(door, FrontDoorView.Ids.StartWhy, t => t == UiText.StartRefusal(WorldPlayerVerdict.NotEnoughPlayers), "the reason words next to Start");
            Assert.That(app.FrontDoor.StartAction.Enabled, Is.False);

            app.FrontDoor.StartSession();   // a forced press: the model refuses ...
            Assert.That(app.SceneFlow.TryStartSession(), Is.False, "... and so does the host's own gate");
            Assert.That(app.SceneFlow.LastStartVerdict, Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(app.World.Started, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Lobby));
            Assert.That(app.FrontDoor.State, Is.EqualTo(FrontDoorState.HostLobby), "the host is still waiting in the lobby");
            Assert.That(door.IsShown, Is.True);
        }

        // ---- guest ----

        // Catches: a guest door that misses a wrong code's refusal words, goes past the waiting screen before the host starts, shows names it does not have, or stays on screen after
        // the host starts. The guest's screens are the second door described in the header.
        [UnityTest]
        public IEnumerator Client_WaitsUntilHostStarts()
        {
            yield return Boot(hollowmere: true);
            yield return HostThroughTheDoor();
            yield return MakeGuestDoor();
            yield return GuestJoinsThroughTheDoor();

            yield return WaitLabel(guestDoor, FrontDoorView.Ids.Waiting, t => t == UiText.WaitingForHost, "the waiting words");
            Assert.That(Displayed(Find<VisualElement>(guestDoor, FrontDoorView.Ids.PlayerList)), Is.False, "a guest is not shown names it does not have (the roster is host-only)");
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the host never saw the guest");
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(guestModel.State, Is.EqualTo(FrontDoorState.WaitingForHost), "the guest keeps waiting while the host has not started");
            Assert.That(app.World.Started, Is.False);
            Assert.That(guestDoor.IsShown, Is.True);

            // The host (this App's door) starts; the guest's waiting screen goes away with the lobby.
            yield return PlayModeHarness.WaitUntil(() => Find<Button>(door, FrontDoorView.Ids.Start).enabledSelf, StepTimeout, "Start stayed off with the guest in");
            yield return Press(door, FrontDoorView.Ids.Start);
            yield return WaitInHollowmere(app);
            yield return PlayModeHarness.WaitUntil(() => !guestDoor.IsShown && !door.IsShown, StepTimeout, "a door stayed on screen over Hollowmere");
            Assert.That(guestModel.State, Is.EqualTo(FrontDoorState.WaitingForHost), "the guest's door did not invent a state of its own");
        }

        // Catches: a guest whose host went away being dropped into a silent Lobby. The guest reads why in words on the Ended screen, and OK takes it back to Title; the host that
        // left (a real press of the lobby's Leave) is back at Title too.
        [UnityTest]
        public IEnumerator HostLeaves_ClientSeesEnded_ThenTitle()
        {
            yield return Boot(hollowmere: true);
            yield return HostThroughTheDoor();
            yield return MakeGuestDoor();
            yield return GuestJoinsThroughTheDoor();

            yield return Press(door, FrontDoorView.Ids.LobbyLeave);   // the host chooses to leave
            yield return WaitState(guestModel, FrontDoorState.Ended, "the guest never saw the Ended screen", 20f);
            Assert.That(guestModel.EndedReason, Is.Not.Null.And.Not.EqualTo(EndReason.UserLeft), "the guest did not leave; the host did");
            string words = UiText.EndedBecause(guestModel.EndedReason.Value);
            Assert.That(words, Is.Not.Null.And.Not.Empty);
            TestContext.WriteLine("guest end reason: " + guestModel.EndedReason + " -> " + words);
            yield return WaitLabel(guestDoor, FrontDoorView.Ids.EndedWords, t => t == words, "the reason words on the Ended screen");
            Assert.That(guestDoor.IsShown, Is.True, "the guest is back in the Lobby with the reason on screen");

            yield return Press(guestDoor, FrontDoorView.Ids.EndedOk);
            yield return WaitState(guestModel, FrontDoorState.Title, "OK did not return the guest to Title");
            yield return WaitControl<Button>(guestDoor, FrontDoorView.Ids.PlaySolo, _ => { });

            // The host chose to leave, so no reason screen: straight back to Title.
            yield return WaitState(app.FrontDoor, FrontDoorState.Title, "the host that left did not return to Title");
        }

        // ---- the join code field and the keyboard / gamepad (review round 1) ----

        private static string FocusedName(FrontDoorView view)
        {
            var focused = view.DoorRoot?.panel?.focusController?.focusedElement as VisualElement;
            while (focused != null && string.IsNullOrEmpty(focused.name)) focused = focused.parent;
            return focused?.name;
        }

        private static IEnumerator TapPad(GamepadButton button)
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>("FlowGamepad");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState(button));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
            }
            finally { InputSystem.RemoveDevice(pad); }
        }

        // The join screen with the code field focused; a wrong code is refused by the stand-in, so a submit shows words and starts nothing real.
        private IEnumerator OpenJoinScreen()
        {
            yield return Boot(hollowmere: false);
            LocalNetworkSession local = app.Services.Get<LocalNetworkSession>();
            AppBootstrap.OnlineSessionFactoryOverride = () => new DirectAsOnlineSession(local, false);
            yield return Press(door, FrontDoorView.Ids.Join);
            yield return WaitState(app.FrontDoor, FrontDoorState.JoinEntry, "Join did not open the code screen");
            yield return PlayModeHarness.WaitUntil(() => FocusedName(door) == FrontDoorView.Ids.CodeField, StepTimeout, "the code field never got focus; focused: " + FocusedName(door));
        }

        // Catches: a typed code not reaching the model, and Return in the field not submitting.
        // The letters are put in the field with field.value: neither a virtual Input System keyboard's text events (QueueTextEvent) nor a synthetic KeyDownEvent with a character
        // inserts text into a UI Toolkit field in this setup (the engine feeds text from its own event queue), so real typing is on the manual walk. Return is a real device key
        // pressed while the field has focus.
        [UnityTest]
        public IEnumerator JoinField_Typing_ReachesTheModel_AndReturnSubmits()
        {
            yield return OpenJoinScreen();
            Find<TextField>(door, FrontDoorView.Ids.CodeField).value = "wrong1";
            yield return PlayModeHarness.WaitUntil(() => app.FrontDoor.JoinCodeInput == "wrong1", StepTimeout, "the field's value never reached the model; it has '" + app.FrontDoor.JoinCodeInput + "'");
            Assert.That(FocusedName(door), Is.EqualTo(FrontDoorView.Ids.CodeField), "the field keeps focus while it changes");

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("FlowTypist");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            yield return WaitLabel(door, FrontDoorView.Ids.JoinError, t => t == UiText.JoinRefusal(EndReason.InvalidCode), "Return in the field did not submit the code");
        }

        // Catches: the text field eating Escape, so a keyboard player is stuck on the join screen.
        [UnityTest]
        public IEnumerator JoinField_Escape_ReturnsToTitle()
        {
            yield return OpenJoinScreen();
            yield return TapKey(Key.Escape);
            yield return WaitState(app.FrontDoor, FrontDoorState.Title, "Escape in the code field did not go back");
        }

        // Catches: the text field eating gamepad B (the cancel action).
        [UnityTest]
        public IEnumerator JoinField_GamepadB_ReturnsToTitle()
        {
            yield return OpenJoinScreen();
            yield return TapPad(GamepadButton.East);
            yield return WaitState(app.FrontDoor, FrontDoorState.Title, "gamepad B in the code field did not go back");
        }

        // Catches: a gamepad focus trap in the text field (d-pad down must reach the buttons below it).
        [UnityTest]
        public IEnumerator JoinField_GamepadDown_LeavesTheField()
        {
            yield return OpenJoinScreen();
            yield return TapPad(GamepadButton.DpadDown);
            yield return PlayModeHarness.WaitUntil(() => FocusedName(door) != FrontDoorView.Ids.CodeField && Find<Button>(door, FrontDoorView.Ids.JoinBack) != null
                && (FocusedName(door) == FrontDoorView.Ids.JoinBack || FocusedName(door) == FrontDoorView.Ids.JoinGo), StepTimeout,
                "d-pad down did not leave the code field; focused: " + FocusedName(door));
        }

        // ---- other review fixes ----

        // Catches: the door's buttons staying pressable while a session the door did not start runs (the dev panel's, or a test's), on any screen, not just Title.
        [UnityTest]
        public IEnumerator Door_HidesOnEveryScreen_WhileAForeignSessionRuns()
        {
            yield return Boot(hollowmere: false);
            yield return Press(door, FrontDoorView.Ids.Multiplayer);
            yield return WaitState(app.FrontDoor, FrontDoorState.Worlds, "Multiplayer did not open the worlds list");
            Assert.That(door.IsShown, Is.True);

            yield return PlayModeHarness.StartDirectHostWithoutScenes();   // a session the door did not start

            yield return PlayModeHarness.WaitUntil(() => !door.IsShown, StepTimeout, "the Worlds screen stayed up over a running session");
            Assert.That(Displayed(Find<Button>(door, FrontDoorView.Ids.Host)), Is.False);
        }

        // Catches: a delete question that leaves the rest of the screen live, or a Cancel that loses the player's place.
        [UnityTest]
        public IEnumerator Delete_Ask_DisablesTheRest_CancelReturnsFocusToTheRow_EscapeAnswersNo()
        {
            yield return Boot(hollowmere: false);
            yield return Press(door, FrontDoorView.Ids.PlaySolo);
            yield return WaitState(app.FrontDoor, FrontDoorState.Worlds, "Play solo did not open the worlds list");
            yield return Press(door, FrontDoorView.Ids.NewWorld);
            yield return WaitControl<Button>(door, FrontDoorView.Ids.WorldRow(1), _ => { });

            yield return Press(door, FrontDoorView.Ids.DeleteWorld(1));
            yield return WaitControl<Button>(door, FrontDoorView.Ids.DeleteCancelButton, _ => { });
            yield return PlayModeHarness.WaitUntil(() => FocusedName(door) == FrontDoorView.Ids.DeleteCancelButton, StepTimeout, "focus did not go to the question; focused: " + FocusedName(door));
            foreach (string id in new[] { FrontDoorView.Ids.WorldRow(1), FrontDoorView.Ids.DeleteWorld(1), FrontDoorView.Ids.NewWorld, FrontDoorView.Ids.Play, FrontDoorView.Ids.WorldsBack })
                Assert.That(Find<Button>(door, id).enabledSelf, Is.False, id + " must be off while the question is open");
            Assert.That(Find<Button>(door, FrontDoorView.Ids.DeleteConfirmButton).enabledSelf, Is.True);

            yield return Press(door, FrontDoorView.Ids.DeleteCancelButton);
            yield return PlayModeHarness.WaitUntil(() => FocusedName(door) == FrontDoorView.Ids.DeleteWorld(1), StepTimeout, "Cancel did not return focus to the row; focused: " + FocusedName(door));
            Assert.That(Find<Button>(door, FrontDoorView.Ids.Play).enabledSelf, Is.True);

            yield return Press(door, FrontDoorView.Ids.DeleteWorld(1));
            yield return WaitControl<Button>(door, FrontDoorView.Ids.DeleteCancelButton, _ => { });
            yield return TapKey(Key.Escape);
            yield return PlayModeHarness.WaitUntil(() => app.FrontDoor.PendingDeleteId == null, StepTimeout, "Escape did not answer the question with no");
            Assert.That(app.FrontDoor.State, Is.EqualTo(FrontDoorState.Worlds), "Escape cancelled the question and did not also leave the screen");
            Assert.That(app.FrontDoor.Rows.Count, Is.EqualTo(1));
        }

        // ---- release path ----

        // Catches: the IMGUI dev panel drawing for players. In the editor Debug.isDebugBuild is always true, so the gate is checked with the build pretended to be a release one;
        // -dev-panel brings it back. The front door is what the player sees either way.
        [UnityTest]
        public IEnumerator ReleaseBuildPath_DevPanelNotDrawn()
        {
            yield return Boot(hollowmere: false);
            var panel = app.GetComponent<DevSessionPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(app.DevFlags.DevPanel, Is.False, "the test run has no -dev-panel flag");

            Assert.That(DevSessionPanel.ShouldDraw(false, DevFlags.None), Is.False, "a release build without the flag");
            Assert.That(DevSessionPanel.ShouldDraw(false, DevFlags.Parse(new[] { "-dev-panel" })), Is.True, "a release build with -dev-panel");
            Assert.That(DevSessionPanel.ShouldDraw(true, DevFlags.None), Is.True, "a debug build");

            DevSessionPanel.DebugBuildOverrideForTests = false;
            Assert.That(panel.IsDrawn, Is.False, "the panel's own gate, as in a release build");
            int before = panel.DrawCount;
            double until = Time.realtimeSinceStartupAsDouble + 1.0;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(panel.DrawCount, Is.EqualTo(before), "OnGUI drew the dev panel in a release build");
            Assert.That(door.IsShown, Is.True, "the front door is what a release build shows");

            // Positive control: the same counter moves when the gate is open (if this editor mode runs OnGUI at all; the gate assertions above hold regardless).
            DevSessionPanel.DebugBuildOverrideForTests = true;
            Assert.That(panel.IsDrawn, Is.True);
            until = Time.realtimeSinceStartupAsDouble + 2.0;
            while (panel.DrawCount == before && Time.realtimeSinceStartupAsDouble < until) yield return null;
            TestContext.WriteLine("dev panel OnGUI draws with the gate open: " + (panel.DrawCount - before) + (panel.DrawCount == before ? " (OnGUI does not run in this editor mode)" : ""));
        }
    }
}
