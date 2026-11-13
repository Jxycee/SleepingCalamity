// Not networked; local on every peer, and it only ASKS (PL-3 task 3.3). The view of FrontDoorModel: it reads the model's properties, calls its commands and
// re-renders when the model says Changed (plus every frame while the host lobby is up, because who is connected changes without an event). It holds no
// game logic: which button is on, the words and the state machine are all the model's; the host's start gate and the join approval stay where they were.
// One UIDocument (FrontDoor.uxml / .uss), one visible screen per FrontDoorState. Every control has a stable name (Ids) so tests find it.
// Shown only while the Lobby scene is the active scene (a session that is running has left it), and the Title screen only while the session is Offline
// (a session the door did not start, such as the dev panel's, leaves the model on Title). Hidden means display:none, no panel focus and no EventSystem
// selection, so a gamepad or keyboard press during play never reaches a menu button.
// Input: keyboard and gamepad events go to the EventSystem's SELECTED object, so the view selects the panel's PanelEventHandler object when it shows and
// focuses the first enabled control of each screen on every state change (Escape / gamepad B = Back). The InputSystemUIInputModule runs on its own default
// actions, never on the game's SleepingCalamityInput asset. The UIDocument rebuilds its tree when its UXML or panel changes, so the binding is checked
// each frame by root identity.
using System;
using System.Collections.Generic;
using System.Text;
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
    public sealed class FrontDoorView : MonoBehaviour
    {
        // The control names in FrontDoor.uxml. Tests and this view both use them.
        public static class Ids
        {
            public const string Root = "door-root";

            public const string ScreenTitle = "screen-title";
            public const string ScreenWorlds = "screen-worlds";
            public const string ScreenHostLobby = "screen-hostlobby";
            public const string ScreenJoinEntry = "screen-joinentry";
            public const string ScreenConnecting = "screen-connecting";
            public const string ScreenWaiting = "screen-waiting";
            public const string ScreenEnded = "screen-ended";

            public const string GameName = "lbl-game-name";
            public const string PlaySolo = "btn-play-solo";
            public const string Multiplayer = "btn-multiplayer";
            public const string Join = "btn-join";
            public const string Quit = "btn-quit";

            public const string WorldsHeading = "lbl-worlds-heading";
            public const string WorldsEmpty = "lbl-worlds-empty";
            public const string WorldList = "world-list";
            public const string DeleteConfirm = "delete-confirm";
            public const string DeleteAsk = "lbl-delete-ask";
            public const string DeleteConfirmButton = "btn-delete-confirm";
            public const string DeleteCancelButton = "btn-delete-cancel";
            public const string WorldsNotice = "lbl-worlds-notice";
            public const string NewWorld = "btn-new-world";
            public const string NewWorldWhy = "lbl-new-world-why";
            public const string Play = "btn-play";
            public const string Host = "btn-host";
            public const string WorldsBack = "btn-worlds-back";

            public const string JoinCode = "lbl-join-code";
            public const string CopyCode = "btn-copy-code";
            public const string CopyNote = "lbl-copy-note";
            public const string PlayerCount = "lbl-player-count";
            public const string PlayerList = "player-list";
            public const string Start = "btn-start";
            public const string StartWhy = "lbl-start-why";
            public const string StartMessage = "lbl-start-message";
            public const string LobbyLeave = "btn-lobby-leave";

            public const string CodeField = "field-code";
            public const string JoinGo = "btn-join-go";
            public const string JoinWhy = "lbl-join-why";
            public const string JoinError = "lbl-join-error";
            public const string JoinBack = "btn-join-back";

            public const string Connecting = "lbl-connecting";
            public const string ConnectingCancel = "btn-connecting-cancel";
            public const string Waiting = "lbl-waiting";
            public const string WaitingLeave = "btn-waiting-leave";
            public const string EndedWords = "lbl-ended-words";
            public const string EndedOk = "btn-ended-ok";

            // 1-based, in list order (newest first): "world-row-1", "btn-delete-1", "player-1".
            public static string WorldRow(int number) => "world-row-" + number;
            public static string DeleteWorld(int number) => "btn-delete-" + number;
            public static string Player(int number) => "player-" + number;
        }

        private const string CopiedWords = "Code copied.";

        private UIDocument document;
        private FrontDoorModel model;
        private INetworkSession session;
        private VisualElement boundRoot;
        private VisualElement root;
        private readonly Dictionary<FrontDoorState, VisualElement> screens = new Dictionary<FrontDoorState, VisualElement>();
        private PanelEventHandler panelHandler;

        private bool shown;
        private bool dirty = true;
        private FrontDoorState? renderedState;
        private bool renderedPending;
        private string worldsSignature = "";
        private int renderedRowCount;
        private string playersSignature = "";
        private string wantedFocus;

        public FrontDoorModel Model => model;

        // True while the door is on screen (the Lobby scene is up and the model is in a screen that belongs to it).
        public bool IsShown => shown;

        // The UIDocument's tree under door-root, for tests; null until the document has built it.
        public VisualElement DoorRoot => root;

        // AppBootstrap hands the model over once it exists; the session decides, with the model, whether the Title screen is up.
        public void Bind(FrontDoorModel frontDoor, INetworkSession networkSession)
        {
            if (model != null) model.Changed -= MarkDirty;
            model = frontDoor;
            session = networkSession;
            if (model != null && isActiveAndEnabled) model.Changed += MarkDirty;
            dirty = true;
            renderedState = null;
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
            UnbindTree();
        }

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            if (document != null && document.rootVisualElement != boundRoot) BindTree();
            if (root == null || model == null) return;

            bool show = ShouldShow();
            if (show != shown)
            {
                shown = show;
                root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown)
                {
                    dirty = true;
                    renderedState = null;
                }
                else
                {
                    Release();
                }
            }
            if (!shown) return;

            if (dirty || renderedState != model.State || model.State == FrontDoorState.HostLobby) Render();
            SelectPanel();
            TryFocus();
        }

        // The Lobby is the door's room; the Title screen yields to a session some other part of the app started.
        private bool ShouldShow()
        {
            if (SceneManager.GetActiveScene().name != SceneNames.Lobby) return false;
            if (model.State == FrontDoorState.Title && session != null && session.State != ConnState.Offline) return false;
            return true;
        }

        // ---- tree ----

        private void BindTree()
        {
            UnbindTree();
            VisualElement documentRoot = document != null ? document.rootVisualElement : null;
            if (documentRoot == null) return;
            boundRoot = documentRoot;
            root = documentRoot.Q<VisualElement>(Ids.Root);
            if (root == null) return;

            screens[FrontDoorState.Title] = root.Q<VisualElement>(Ids.ScreenTitle);
            screens[FrontDoorState.Worlds] = root.Q<VisualElement>(Ids.ScreenWorlds);
            screens[FrontDoorState.HostLobby] = root.Q<VisualElement>(Ids.ScreenHostLobby);
            screens[FrontDoorState.JoinEntry] = root.Q<VisualElement>(Ids.ScreenJoinEntry);
            screens[FrontDoorState.Connecting] = root.Q<VisualElement>(Ids.ScreenConnecting);
            screens[FrontDoorState.WaitingForHost] = root.Q<VisualElement>(Ids.ScreenWaiting);
            screens[FrontDoorState.Ended] = root.Q<VisualElement>(Ids.ScreenEnded);

            Hook(Ids.PlaySolo, () => model?.OpenWorlds(WorldKind.Solo));
            Hook(Ids.Multiplayer, () => model?.OpenWorlds(WorldKind.Multiplayer));
            Hook(Ids.Join, () => model?.OpenJoin());
            Hook(Ids.Quit, Quit);

            Hook(Ids.NewWorld, () => model?.CreateWorld());
            Hook(Ids.Play, () => { if (model != null) Fire(model.PlaySelected()); });
            Hook(Ids.Host, () => { if (model != null) Fire(model.HostSelected()); });
            Hook(Ids.WorldsBack, () => { if (model != null) Fire(model.Back()); });
            Hook(Ids.DeleteConfirmButton, () => model?.ConfirmDelete());
            Hook(Ids.DeleteCancelButton, () => model?.CancelDelete());

            Hook(Ids.CopyCode, CopyCode);
            Hook(Ids.Start, () => model?.StartSession());
            Hook(Ids.LobbyLeave, Leave);

            Hook(Ids.JoinGo, () => { if (model != null) Fire(model.Join()); });
            Hook(Ids.JoinBack, () => { if (model != null) Fire(model.Back()); });
            TextField code = root.Q<TextField>(Ids.CodeField);
            if (code != null)
            {
                code.RegisterValueChangedCallback(e => { if (model != null) model.JoinCodeInput = e.newValue; });
                code.RegisterCallback<KeyDownEvent>(e =>
                {
                    if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && model != null && model.JoinAction.Enabled)
                    {
                        Fire(model.Join());
                        e.StopPropagation();
                    }
                });
            }

            Hook(Ids.ConnectingCancel, Leave);
            Hook(Ids.WaitingLeave, Leave);
            Hook(Ids.EndedOk, () => model?.Acknowledge());

            // Escape and gamepad B go back from any screen.
            root.RegisterCallback<NavigationCancelEvent>(e =>
            {
                if (model != null) Fire(model.Back());
                e.StopPropagation();
            });

            root.style.display = DisplayStyle.None;   // Update shows it when it should be up
            foreach (VisualElement screen in screens.Values) if (screen != null) screen.style.display = DisplayStyle.None;
            shown = false;
            dirty = true;
            renderedState = null;
            worldsSignature = "";
            playersSignature = "";
        }

        private void UnbindTree()
        {
            root = null;
            boundRoot = null;
            screens.Clear();
            shown = false;
            wantedFocus = null;
        }

        private void Hook(string name, Action action)
        {
            Button button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        private void Leave()
        {
            if (model != null) Fire(model.Leave());
        }

        private static async void Fire(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Debug.LogException(e);   // the model's tasks never fault; this is for a bug
            }
        }

        private void CopyCode()
        {
            if (model == null || string.IsNullOrEmpty(model.JoinCode)) return;
            GUIUtility.systemCopyBuffer = model.JoinCode;
            SetText(Ids.CopyNote, CopiedWords);
        }

        private static void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ---- drawing ----

        private void Render()
        {
            bool stateChanged = renderedState != model.State;
            renderedState = model.State;
            dirty = false;

            foreach (KeyValuePair<FrontDoorState, VisualElement> screen in screens)
                if (screen.Value != null) screen.Value.style.display = screen.Key == model.State ? DisplayStyle.Flex : DisplayStyle.None;

            switch (model.State)
            {
                case FrontDoorState.Worlds: RenderWorlds(); break;
                case FrontDoorState.HostLobby: RenderHostLobby(stateChanged); break;
                case FrontDoorState.JoinEntry: RenderJoinEntry(); break;
                case FrontDoorState.Connecting: SetText(Ids.Connecting, model.ConnectingWords); break;
                case FrontDoorState.WaitingForHost: SetText(Ids.Waiting, model.WaitingWords); break;
                case FrontDoorState.Ended: SetText(Ids.EndedWords, model.EndedWords); break;
            }

            if (stateChanged) wantedFocus = PreferredFocus();
            else if (wantedFocus == null && !FocusIsUsable()) wantedFocus = PreferredFocus();
        }

        private void RenderWorlds()
        {
            SetText(Ids.WorldsHeading, model.WorldsKind == WorldKind.Solo ? "Solo worlds" : "Multiplayer worlds");

            var signature = new StringBuilder();
            foreach (FrontDoorWorldRow row in model.Rows)
                signature.Append(row.Id).Append('|').Append(row.Selected).Append('|').Append(row.LastPlayed).Append('|').Append(string.Join(",", row.PlayerNames)).Append(';');
            string text = signature.ToString();
            if (text != worldsSignature)
            {
                worldsSignature = text;
                string keep = FocusedName();
                RebuildWorldRows();
                if (model.Rows.Count > renderedRowCount) wantedFocus = PreferredFocus();   // a new world: Enter now plays it
                else if (keep != null && root.Q<VisualElement>(keep) != null) wantedFocus = keep;
                else wantedFocus = PreferredFocus();
                renderedRowCount = model.Rows.Count;
            }
            SetDisplay(Ids.WorldsEmpty, model.Rows.Count == 0);

            bool pending = model.PendingDeleteId != null;
            SetDisplay(Ids.DeleteConfirm, pending);
            SetText(Ids.DeleteAsk, model.PendingDeleteWords);
            if (pending != renderedPending)
            {
                renderedPending = pending;
                wantedFocus = pending ? Ids.DeleteCancelButton : PreferredFocus();
            }

            SetText(Ids.WorldsNotice, model.Notice);
            SetAction(Ids.NewWorld, model.NewWorldAction);
            SetText(Ids.NewWorldWhy, model.NewWorldAction.Why);

            bool solo = model.WorldsKind == WorldKind.Solo;
            SetDisplay(Ids.Play, solo);
            SetDisplay(Ids.Host, !solo);
            SetAction(Ids.Play, model.PlayAction);
            SetAction(Ids.Host, model.HostAction);
        }

        private void RebuildWorldRows()
        {
            VisualElement list = root.Q<VisualElement>(Ids.WorldList);
            if (list == null) return;
            list.Clear();
            int number = 0;
            foreach (FrontDoorWorldRow row in model.Rows)
            {
                number++;
                string id = row.Id;
                var line = new VisualElement();
                line.AddToClassList("fd-world-row");

                string who = row.PlayerNames == null || row.PlayerNames.Count == 0 ? "" : string.Join(", ", row.PlayerNames);
                string label = row.Title + (row.Selected ? "  (selected)" : "") + (who.Length > 0 || row.LastPlayed.Length > 0 ? "\n" + who + (who.Length > 0 && row.LastPlayed.Length > 0 ? "   " : "") + row.LastPlayed : "");
                var select = new Button(() => model?.SelectWorld(id)) { name = Ids.WorldRow(number), text = label, focusable = true };
                select.AddToClassList("fd-button");
                select.AddToClassList("fd-world-select");
                if (row.Selected) select.AddToClassList("fd-selected");
                line.Add(select);

                var delete = new Button(() => model?.RequestDelete(id)) { name = Ids.DeleteWorld(number), text = "Delete", focusable = true };
                delete.AddToClassList("fd-button");
                delete.AddToClassList("fd-world-delete");
                line.Add(delete);

                list.Add(line);
            }
        }

        private void RenderHostLobby(bool stateChanged)
        {
            if (stateChanged) SetText(Ids.CopyNote, "");
            SetText(Ids.JoinCode, model.JoinCode);
            SetText(Ids.PlayerCount, "Players in the game: " + model.PlayerCount);

            IReadOnlyList<string> names = model.PlayerNames;
            string joined = string.Join("\n", names);
            if (joined != playersSignature)
            {
                playersSignature = joined;
                VisualElement list = root.Q<VisualElement>(Ids.PlayerList);
                if (list != null)
                {
                    list.Clear();
                    for (int i = 0; i < names.Count; i++)
                    {
                        var label = new Label(names[i]) { name = Ids.Player(i + 1) };
                        label.AddToClassList("fd-player");
                        list.Add(label);
                    }
                }
            }

            FrontDoorAction start = model.StartAction;
            SetAction(Ids.Start, start);
            SetText(Ids.StartWhy, start.Why);
            SetText(Ids.StartMessage, model.StartMessage);
        }

        private void RenderJoinEntry()
        {
            TextField field = root.Q<TextField>(Ids.CodeField);
            if (field != null && field.value != model.JoinCodeInput && !IsFocusWithin(field)) field.SetValueWithoutNotify(model.JoinCodeInput);
            FrontDoorAction join = model.JoinAction;
            SetAction(Ids.JoinGo, join);
            SetText(Ids.JoinWhy, join.Why);
            SetText(Ids.JoinError, model.JoinError);
        }

        private void SetText(string name, string text)
        {
            Label label = root.Q<Label>(name);
            if (label == null) return;
            string value = text ?? "";
            if (label.text != value) label.text = value;
            label.style.display = value.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void SetDisplay(string name, bool visible)
        {
            VisualElement element = root.Q<VisualElement>(name);
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetAction(string name, FrontDoorAction action)
        {
            VisualElement element = root.Q<VisualElement>(name);
            if (element != null && element.enabledSelf != action.Enabled) element.SetEnabled(action.Enabled);
        }

        // ---- focus ----

        // The control a screen opens on: the one Enter should press.
        private string PreferredFocus()
        {
            switch (model.State)
            {
                case FrontDoorState.Title: return Ids.PlaySolo;
                case FrontDoorState.Worlds:
                    if (model.Rows.Count == 0) return Ids.NewWorld;
                    return model.WorldsKind == WorldKind.Solo ? Ids.Play : Ids.Host;
                case FrontDoorState.HostLobby: return model.StartAction.Enabled ? Ids.Start : Ids.CopyCode;
                case FrontDoorState.JoinEntry: return Ids.CodeField;
                case FrontDoorState.Connecting: return Ids.ConnectingCancel;
                case FrontDoorState.WaitingForHost: return Ids.WaitingLeave;
                case FrontDoorState.Ended: return Ids.EndedOk;
                default: return null;
            }
        }

        private VisualElement FocusedElement() => root?.panel?.focusController?.focusedElement as VisualElement;

        private string FocusedName()
        {
            VisualElement focused = FocusedElement();
            while (focused != null && string.IsNullOrEmpty(focused.name)) focused = focused.parent;
            return focused != null && root.Contains(focused) ? focused.name : null;
        }

        private bool IsFocusWithin(VisualElement element)
        {
            VisualElement focused = FocusedElement();
            return focused != null && (focused == element || element.Contains(focused));
        }

        // Some enabled control of the screen on show still holds focus (a disabled or removed one does not).
        private bool FocusIsUsable()
        {
            VisualElement focused = FocusedElement();
            if (focused == null || !root.Contains(focused)) return false;
            return focused.canGrabFocus;
        }

        private void TryFocus()
        {
            if (wantedFocus == null) return;
            VisualElement target = root.Q<VisualElement>(wantedFocus);
            if (target == null) { wantedFocus = null; return; }
            if (!target.canGrabFocus) return;   // not laid out or not enabled yet: try again next frame
            target.Focus();
            if (IsFocusWithin(target)) wantedFocus = null;
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

        // Hidden: no focus in the panel and nothing selected, so no press can land on an invisible button.
        private void Release()
        {
            wantedFocus = null;
            VisualElement focused = FocusedElement();
            if (focused != null && root != null && root.Contains(focused)) focused.Blur();
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && panelHandler != null && eventSystem.currentSelectedGameObject == panelHandler.gameObject)
                eventSystem.SetSelectedGameObject(null);
        }
    }
}
