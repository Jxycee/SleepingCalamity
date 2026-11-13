// Authority: test-only (no networked state). The UI stack of the front door (PL-3 tasks 3.0 and 3.3): the App object carries a UIDocument built by SceneBuilder, the real
// FrontDoorView binds it to the real FrontDoorModel, and a -nographics run can find a control by name and activate it with a navigation event, a keyboard, a gamepad or a mouse.
// The control used is the Title screen's Join button: opening the join screen touches no world file and no session, so these tests need no save folder and no port.
// NOT covered: a real display (layout at other resolutions) and a real focused window; the device tests override the editor's focus rules (SetUp) the way FrontDoorFlowTests does.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SleepingCalamity.Tests.PlayMode
{
    public class MenuStackSmokeTests
    {
        private const float BindTimeoutSeconds = 5f;

        private InputSettings.BackgroundBehavior savedBackground;
        private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
            yield return PlayModeHarness.TearDownApp();
        }

        // A batch-mode editor is never the focused application, so by default the Input System drops events from keyboards, mice and gamepads
        // that a test queues. A real player has focus; the override only makes the test editor behave like one.
        [SetUp]
        public void AcceptDeviceInputWithoutFocus()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        private sealed class DoorScene
        {
            public AppBootstrap App;
            public UIDocument Document;
            public FrontDoorView View;
            public Button Join;
        }

        private static IEnumerator LoadDoor(DoorScene scene)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            scene.App = AppBootstrap.Current;
            scene.Document = scene.App.GetComponent<UIDocument>();
            scene.View = scene.App.GetComponent<FrontDoorView>();
            Assert.That(scene.Document, Is.Not.Null, "the App object has no UIDocument (SceneBuilder must add it)");
            Assert.That(scene.View, Is.Not.Null, "the App object has no FrontDoorView");
            Assert.That(scene.Document.panelSettings, Is.Not.Null, "the UIDocument has no PanelSettings");
            Assert.That(scene.App.FrontDoor, Is.Not.Null, "AppBootstrap did not compose the FrontDoorModel");
            Assert.That(scene.View.Model, Is.SameAs(scene.App.FrontDoor), "the view is not bound to the App's model");
            yield return PlayModeHarness.WaitUntil(
                () => scene.View.IsShown && (scene.Join = scene.Document.rootVisualElement?.Q<Button>(FrontDoorView.Ids.Join)) != null,
                BindTimeoutSeconds, "the front door never showed its Title screen");
            // Laid out, and the view has put focus on the first control.
            yield return PlayModeHarness.WaitUntil(
                () => scene.Join.worldBound.width > 1f && scene.Join.focusController?.focusedElement != null,
                BindTimeoutSeconds, "the Title screen was never laid out and focused");
        }

        // Catches: no UIDocument / PanelSettings on the App object, a UXML that lacks the button, a view that never binds, or a
        // navigation-submit that does not reach Button.clicked (the path a keyboard Enter or gamepad south press takes).
        [UnityTest]
        public IEnumerator Document_Binds_ButtonClick_ReachesModel()
        {
            var scene = new DoorScene();
            yield return LoadDoor(scene);
            Assert.That(scene.App.FrontDoor.State, Is.EqualTo(FrontDoorState.Title), "nothing pressed yet");

            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = scene.Join;
                scene.Join.SendEvent(submit);
            }
            yield return PlayModeHarness.WaitUntil(() => scene.App.FrontDoor.State == FrontDoorState.JoinEntry, BindTimeoutSeconds, "the navigation-submit never reached the model");
        }

        // The module must run on its own default actions asset, not the game's SleepingCalamityInput asset.
        [UnityTest]
        public IEnumerator UiModule_RunsOnItsOwnDefaultActions()
        {
            var scene = new DoorScene();
            yield return LoadDoor(scene);
            var module = scene.App.GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null, "the App object has no InputSystemUIInputModule");
            Assert.That(scene.App.GetComponent<EventSystem>(), Is.Not.Null, "the App object has no EventSystem");
            Assert.That(module.actionsAsset, Is.Not.Null);
            TestContext.WriteLine("UI module actions asset: " + module.actionsAsset.name);
            Assert.That(module.actionsAsset.name, Is.Not.EqualTo("SleepingCalamityInput"), "the UI module must not borrow the game's input asset");
        }

        // ---- real devices through the Input System (virtual devices; the whole path EventSystem -> UI module -> panel -> Button) ----

        [UnityTest]
        public IEnumerator Keyboard_Enter_ActivatesFocusedButton()
        {
            var scene = new DoorScene();
            yield return LoadDoor(scene);
            scene.Join.Focus();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("DoorKeyboard");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return PlayModeHarness.WaitUntil(() => scene.App.FrontDoor.State == FrontDoorState.JoinEntry, BindTimeoutSeconds, "Enter never reached the model");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator Gamepad_South_ActivatesFocusedButton()
        {
            var scene = new DoorScene();
            yield return LoadDoor(scene);
            scene.Join.Focus();
            Gamepad pad = InputSystem.AddDevice<Gamepad>("DoorGamepad");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.South));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return PlayModeHarness.WaitUntil(() => scene.App.FrontDoor.State == FrontDoorState.JoinEntry, BindTimeoutSeconds, "gamepad south never reached the model");
            }
            finally { InputSystem.RemoveDevice(pad); }
        }

        // The d-pad moves focus down the Title screen's column (the first press lands on the second control).
        [UnityTest]
        public IEnumerator Gamepad_DpadDown_MovesFocusToTheNextButton()
        {
            var scene = new DoorScene();
            yield return LoadDoor(scene);
            VisualElement root = scene.Document.rootVisualElement;
            var first = root.Q<Button>(FrontDoorView.Ids.PlaySolo);
            first.Focus();
            Gamepad pad = InputSystem.AddDevice<Gamepad>("DoorGamepadNav");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.DpadDown));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return PlayModeHarness.WaitUntil(
                    () => (scene.Join.focusController.focusedElement as VisualElement)?.name == FrontDoorView.Ids.Multiplayer,
                    BindTimeoutSeconds, "d-pad down did not move focus from Play solo to Multiplayer; focused: " + (scene.Join.focusController.focusedElement as VisualElement)?.name);
            }
            finally { InputSystem.RemoveDevice(pad); }
        }

        [UnityTest]
        public IEnumerator Mouse_Click_ActivatesButtonUnderPointer()
        {
            var scene = new DoorScene();
            yield return LoadDoor(scene);
            // Panel coordinates (y down, reference-resolution scaled) to screen pixels (y up).
            Rect rootBound = scene.Document.rootVisualElement.worldBound;
            Vector2 centre = scene.Join.worldBound.center;
            var screen = new Vector2(centre.x * Screen.width / rootBound.width, Screen.height - centre.y * Screen.height / rootBound.height);
            TestContext.WriteLine("mouse target: panel " + centre + " root " + rootBound + " screen " + screen + " of " + Screen.width + "x" + Screen.height);
            Mouse mouse = InputSystem.AddDevice<Mouse>("DoorMouse");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return PlayModeHarness.WaitUntil(() => scene.App.FrontDoor.State == FrontDoorState.JoinEntry, BindTimeoutSeconds, "the mouse click never reached the model");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
