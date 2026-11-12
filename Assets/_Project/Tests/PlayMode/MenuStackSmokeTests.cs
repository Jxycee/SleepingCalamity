// Authority: test-only (no networked state). PL-3 task 3.0, the UI Toolkit spike: the App object carries a UIDocument built by SceneBuilder, and a
// -nographics run can find the button by name and activate it so the press reaches a plain C# model.
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

        // Catches: no UIDocument / PanelSettings on the App object, a UXML that lacks the button, a view that never binds, or a
        // navigation-submit that does not reach Button.clicked (the path a keyboard Enter or gamepad south press takes).
        [UnityTest]
        public IEnumerator Document_Binds_ButtonClick_ReachesModel()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();

            AppBootstrap app = AppBootstrap.Current;
            var document = app.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null, "the App object has no UIDocument (SceneBuilder must add it)");
            var view = app.GetComponent<MenuSpikeView>();
            Assert.That(view, Is.Not.Null, "the App object has no MenuSpikeView");
            Assert.That(document.panelSettings, Is.Not.Null, "the UIDocument has no PanelSettings");

            Button button = null;
            yield return PlayModeHarness.WaitUntil(
                () => (button = document.rootVisualElement?.Q<Button>(MenuSpikeView.ButtonName)) != null,
                BindTimeoutSeconds,
                "the UIDocument tree has no Button named " + MenuSpikeView.ButtonName);
            Assert.That(view.Model.Activations, Is.EqualTo(0), "nothing pressed yet");

            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            yield return PlayModeHarness.WaitUntil(() => view.Model.Activations == 1, BindTimeoutSeconds, "the navigation-submit never reached the model");
            Assert.That(view.Model.Activations, Is.EqualTo(1));
        }

        // ---- real devices through the Input System (virtual devices; the whole path EventSystem -> UI module -> panel -> Button) ----

        private sealed class SpikeScene
        {
            public AppBootstrap App;
            public UIDocument Document;
            public MenuSpikeView View;
            public Button Button;
        }

        private static IEnumerator LoadSpike(SpikeScene scene)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            scene.App = AppBootstrap.Current;
            scene.Document = scene.App.GetComponent<UIDocument>();
            scene.View = scene.App.GetComponent<MenuSpikeView>();
            Assert.That(scene.Document, Is.Not.Null);
            Assert.That(scene.View, Is.Not.Null);
            yield return PlayModeHarness.WaitUntil(
                () => (scene.Button = scene.Document.rootVisualElement?.Q<Button>(MenuSpikeView.ButtonName)) != null,
                BindTimeoutSeconds, "no button in the UIDocument tree");
            // The panel must have laid the button out and given it focus (the view focuses it when it binds).
            yield return PlayModeHarness.WaitUntil(
                () => scene.Button.worldBound.width > 1f && scene.Button.focusController?.focusedElement == scene.Button,
                BindTimeoutSeconds, "the spike button was never laid out and focused");
        }

        // The module must run on its own default actions asset, not the game's SleepingCalamityInput asset.
        [UnityTest]
        public IEnumerator UiModule_RunsOnItsOwnDefaultActions()
        {
            var scene = new SpikeScene();
            yield return LoadSpike(scene);
            var module = scene.App.GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null, "the App object has no InputSystemUIInputModule");
            Assert.That(scene.App.GetComponent<EventSystem>(), Is.Not.Null, "the App object has no EventSystem");
            Assert.That(module.actionsAsset, Is.Not.Null);
            TestContext.WriteLine("UI module actions asset: " + module.actionsAsset.name);
            Assert.That(module.actionsAsset.name, Is.Not.EqualTo("SleepingCalamityInput"), "the UI module must not borrow the game's input asset");
        }

        [UnityTest]
        public IEnumerator Keyboard_Enter_ActivatesFocusedButton()
        {
            var scene = new SpikeScene();
            yield return LoadSpike(scene);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("SpikeKeyboard");
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return PlayModeHarness.WaitUntil(() => scene.View.Model.Activations >= 1, BindTimeoutSeconds, "Enter never reached the model");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            Assert.That(scene.View.Model.Activations, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Gamepad_South_ActivatesFocusedButton()
        {
            var scene = new SpikeScene();
            yield return LoadSpike(scene);
            Gamepad pad = InputSystem.AddDevice<Gamepad>("SpikeGamepad");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState(GamepadButton.South));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return PlayModeHarness.WaitUntil(() => scene.View.Model.Activations >= 1, BindTimeoutSeconds, "gamepad south never reached the model");
            }
            finally { InputSystem.RemoveDevice(pad); }
            Assert.That(scene.View.Model.Activations, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Mouse_Click_ActivatesButtonUnderPointer()
        {
            var scene = new SpikeScene();
            yield return LoadSpike(scene);
            // Panel coordinates (y down, reference-resolution scaled) to screen pixels (y up).
            Rect rootBound = scene.Document.rootVisualElement.worldBound;
            Vector2 centre = scene.Button.worldBound.center;
            var screen = new Vector2(centre.x * Screen.width / rootBound.width, Screen.height - centre.y * Screen.height / rootBound.height);
            TestContext.WriteLine("mouse target: panel " + centre + " root " + rootBound + " screen " + screen + " of " + Screen.width + "x" + Screen.height);
            Mouse mouse = InputSystem.AddDevice<Mouse>("SpikeMouse");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                yield return PlayModeHarness.WaitUntil(() => scene.View.Model.Activations >= 1, BindTimeoutSeconds, "the mouse click never reached the model");
            }
            finally { InputSystem.RemoveDevice(mouse); }
            Assert.That(scene.View.Model.Activations, Is.EqualTo(1));
        }
    }
}
