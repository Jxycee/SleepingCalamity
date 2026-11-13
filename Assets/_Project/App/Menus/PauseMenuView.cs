// Not networked; local on every peer, and it only ASKS (PL-4 task 4.1). The view of PauseMenuModel on the App's own UIDocument (PauseMenu.uxml / .uss, the same
// PanelSettings as the front door, so there is one panel and one PanelEventHandler). It reads the Pause button from the input service's intent (in the Menu context
// that is the UI cancel, so Escape / gamepad B also closes it or steps back), and passes the model the gameplay context of the frame BEFORE, so a Pause that has
// just left placement mode (PlacementMode reads the same press) does not also open the menu. Opens only while a gameplay scene is the active scene.
// Input: keyboard and gamepad events reach the panel only through the EventSystem's selected object, so the view selects the panel's PanelEventHandler object while
// open (and the first control of each screen is focused); the InputSystemUIInputModule runs on its own default actions, never on SleepingCalamityInput.
// Hidden means display:none and no selection, so no press reaches an invisible button. Every control has a stable name (Ids) so tests find it.
using System;
using System.Threading.Tasks;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SleepingCalamity.App
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenuView : MonoBehaviour
    {
        public static class Ids
        {
            public const string Root = "pause-root";
            public const string ScreenMain = "pause-main";
            public const string ScreenControls = "pause-controls";
            public const string ScreenConfirm = "pause-confirm";

            public const string Resume = "btn-pause-resume";
            public const string Controls = "btn-pause-controls";
            public const string Leave = "btn-pause-leave";
            public const string Quit = "btn-pause-quit";
            public const string ControlsList = "controls-list";
            public const string ControlsBack = "btn-controls-back";
            public const string ConfirmAsk = "lbl-confirm-ask";
            public const string ConfirmYes = "btn-confirm-yes";
            public const string ConfirmNo = "btn-confirm-no";
        }

        private UIDocument document;
        private PauseMenuModel model;
        private IInputService input;
        private VisualElement boundRoot;
        private VisualElement root;
        private VisualElement screenMain, screenControls, screenConfirm;
        private PanelEventHandler panelHandler;
        private InputContext lastGameplayContext = InputContext.Free;
        private PauseMenuState renderedState = PauseMenuState.Closed;
        private bool dirty = true;
        private string wantedFocus;

        public PauseMenuModel Model => model;

        // The UIDocument's tree under pause-root, for tests; null until the document has built it.
        public VisualElement PauseRoot => root;

        // True while the menu is on screen.
        public bool IsShown => model != null && model.IsOpen && root != null && root.style.display != DisplayStyle.None;

        public void Bind(PauseMenuModel pauseModel, IInputService inputService)
        {
            if (model != null) model.Changed -= MarkDirty;
            model = pauseModel;
            input = inputService;
            if (model != null && isActiveAndEnabled) model.Changed += MarkDirty;
            dirty = true;
        }

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            if (model != null)
            {
                model.Changed -= MarkDirty;
                model.Changed += MarkDirty;
            }
            BindTree();
        }

        private void OnDisable()
        {
            if (model != null) model.Changed -= MarkDirty;
            Release();
            root = null;
            boundRoot = null;
        }

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            if (model == null || input == null) return;
            if (document != null && document.rootVisualElement != boundRoot) BindTree();

            if (input.HasFocus && input.Intent.PausePressed && SceneNames.IsGameplay(SceneManager.GetActiveScene().name))
                model.Pause(lastGameplayContext);

            if (root == null) return;
            if (dirty || renderedState != model.State) Render();
            if (model.IsOpen)
            {
                SelectPanel();
                TryFocus();
            }
        }

        // Recorded after every other script's Update, so the next frame's Pause press sees what the context was BEFORE that frame's scripts ran.
        private void LateUpdate()
        {
            if (input != null) lastGameplayContext = input.GameplayContext;
        }

        private void BindTree()
        {
            VisualElement documentRoot = document != null ? document.rootVisualElement : null;
            if (documentRoot == null) return;
            boundRoot = documentRoot;
            root = documentRoot.Q<VisualElement>(Ids.Root);
            if (root == null) return;
            screenMain = root.Q<VisualElement>(Ids.ScreenMain);
            screenControls = root.Q<VisualElement>(Ids.ScreenControls);
            screenConfirm = root.Q<VisualElement>(Ids.ScreenConfirm);

            Hook(Ids.Resume, () => model?.Resume());
            Hook(Ids.Controls, () => model?.ShowControls());
            Hook(Ids.Leave, () => { if (model != null) Fire(model.Leave()); });
            Hook(Ids.Quit, () => model?.Quit());
            Hook(Ids.ControlsBack, () => model?.Back());
            Hook(Ids.ConfirmYes, () => { if (model != null) Fire(model.Confirm()); });
            Hook(Ids.ConfirmNo, () => model?.Back());

            // Escape / gamepad B reach the model through the input service's Pause (UI cancel in the Menu context); the panel's own cancel is swallowed so it counts once.
            root.RegisterCallback<NavigationCancelEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);

            root.style.display = DisplayStyle.None;
            renderedState = PauseMenuState.Closed;
            dirty = true;
        }

        private void Hook(string name, Action action)
        {
            Button button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        private static async void Fire(Task task)
        {
            try { await task; }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Render()
        {
            bool changed = renderedState != model.State;
            renderedState = model.State;
            dirty = false;

            bool open = model.IsOpen;
            root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            SetDisplay(screenMain, model.State == PauseMenuState.Main);
            SetDisplay(screenControls, model.State == PauseMenuState.Controls);
            SetDisplay(screenConfirm, model.State == PauseMenuState.Confirm);
            if (!open)
            {
                Release();
                return;
            }

            if (model.State == PauseMenuState.Controls) RenderControls();
            if (model.State == PauseMenuState.Confirm)
            {
                SetText(Ids.ConfirmAsk, model.ConfirmWords);
                Button yes = root.Q<Button>(Ids.ConfirmYes);
                if (yes != null) yes.text = model.ConfirmButtonWords;
            }
            if (changed)
            {
                switch (model.State)
                {
                    case PauseMenuState.Main: wantedFocus = Ids.Resume; break;
                    case PauseMenuState.Controls: wantedFocus = Ids.ControlsBack; break;
                    case PauseMenuState.Confirm: wantedFocus = Ids.ConfirmNo; break;   // the safe answer is the default
                }
            }
        }

        private void RenderControls()
        {
            VisualElement list = root.Q<VisualElement>(Ids.ControlsList);
            if (list == null) return;
            list.Clear();
            foreach (var row in model.Controls)
            {
                var line = new VisualElement();
                line.AddToClassList("pm-row");
                var action = new Label(row.Key);
                action.AddToClassList("pm-row-action");
                var key = new Label(row.Value);
                key.AddToClassList("pm-row-key");
                line.Add(action);
                line.Add(key);
                list.Add(line);
            }
        }

        private static void SetDisplay(VisualElement element, bool on)
        {
            if (element != null) element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetText(string name, string text)
        {
            Label label = root.Q<Label>(name);
            if (label != null && label.text != text) label.text = text ?? "";
        }

        private void TryFocus()
        {
            if (wantedFocus == null) return;
            VisualElement target = root.Q<VisualElement>(wantedFocus);
            if (target == null) { wantedFocus = null; return; }
            if (!target.canGrabFocus) return;
            target.Focus();
            VisualElement focused = root.panel?.focusController?.focusedElement as VisualElement;
            if (focused != null && (focused == target || target.Contains(focused))) wantedFocus = null;
        }

        // Keyboard and gamepad events reach the panel only through the EventSystem's selected object.
        private void SelectPanel()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            if (panelHandler == null)
            {
                PanelEventHandler[] handlers = FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None);
                if (handlers.Length == 0) return;
                panelHandler = handlers[0];
            }
            if (eventSystem.currentSelectedGameObject != panelHandler.gameObject) eventSystem.SetSelectedGameObject(panelHandler.gameObject);
        }

        private void Release()
        {
            wantedFocus = null;
            VisualElement focused = root?.panel?.focusController?.focusedElement as VisualElement;
            if (focused != null && root.Contains(focused)) focused.Blur();
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && panelHandler != null && eventSystem.currentSelectedGameObject == panelHandler.gameObject)
                eventSystem.SetSelectedGameObject(null);
        }
    }
}
