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
            public const string ScreenUnsaved = "screen-unsaved";
            public const string UnsavedAsk = "lbl-unsaved-ask";
            public const string UnsavedYes = "btn-unsaved-yes";
            public const string UnsavedNo = "btn-unsaved-no";
            public const string RenameList = "rename-list";
            public const string RenameConfirm = "rename-confirm";
            public const string RenameAsk = "lbl-rename-ask";
            public const string RenameYes = "btn-rename-yes";
            public const string RenameNo = "btn-rename-no";
            public const string RenameMessage = "lbl-rename-message";
            public const string SendAwayList = "send-away-list";
            public const string SendAwayConfirm = "send-away-confirm";
            public const string SendAwayAsk = "lbl-send-away-ask";
            public const string SendAwayYes = "btn-send-away-yes";
            public const string SendAwayNo = "btn-send-away-no";
            // The host's "send this waiting player away" button, 1-based: "btn-send-away-1".
            public static string SendAway(int number) => "btn-send-away-" + number;
            // The host's "this connected player is that recorded player" button, 1-based: "btn-rename-1".
            public static string RenameOption(int number) => "btn-rename-" + number;

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
            public const string RemoveConfirm = "remove-confirm";
            public const string RemoveAsk = "lbl-remove-ask";
            public const string RemoveConfirmButton = "btn-remove-confirm";
            public const string RemoveCancelButton = "btn-remove-cancel";
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

            // The host's "remove player" button of world `row`'s recorded player `player`, both 1-based: "btn-remove-player-1-2".
            public static string RemovePlayer(int row, int player) => "btn-remove-player-" + row + "-" + player;
        }

        private const string CopiedWords = "Code copied.";
        private const float CancelIgnoreSeconds = 0.4f;

        private UIDocument document;
        private FrontDoorModel model;
        private INetworkSession session;
        private VisualElement boundRoot;
        private VisualElement root;
        private readonly Dictionary<FrontDoorState, VisualElement> screens = new Dictionary<FrontDoorState, VisualElement>();
        private PanelEventHandler panelHandler;
        private IInputService input;            // the App's input service (null for a door that is not the App's)
        private bool lobbyActive;               // the Lobby scene is the active scene
        private bool menuOpenByDoor;            // this view raised IInputService.MenuOpen and must lower it
        private float connectingSince;          // unscaled time the Connecting screen came up

        private bool shown;
        private bool dirty = true;
        private FrontDoorState? renderedState;
        private bool renderedPending;
        private int pendingNumber;
        private int pendingRowCount;
        private string pendingAskedFrom;        // the button the open question was asked from, so No hands focus back to it
        private string worldsSignature = "";
        private int renderedRowCount;
        private string playersSignature = "";
        private string renameSignature = "";
        private string sendAwaySignature = "";
        private bool renderedRenamePending;
        private bool renderedSendAwayPending;
        // Cached so the lobby's per-frame redraw check allocates nothing while nothing changed (audit nit): the signatures are built in these
        // and compared in place; a string is made only when the lobby really changed.
        private readonly StringBuilder signatureBuilder = new StringBuilder(128);
        private int renderedPlayerCount = -1;
        private string playerCountText = "";
        private string wantedFocus;

        public FrontDoorModel Model => model;

        // True while the door is on screen (the Lobby scene is up and the model is in a screen that belongs to it).
        public bool IsShown => shown;

        // The UIDocument's tree under door-root, for tests; null until the document has built it.
        public VisualElement DoorRoot => root;

        // AppBootstrap hands the model over once it exists; the session decides, with the model, whether the Title screen is up.
        public void Bind(FrontDoorModel frontDoor, INetworkSession networkSession, IInputService inputService = null)
        {
            if (model != null) model.Changed -= MarkDirty;
            ReleaseMenuContext();
            model = frontDoor;
            session = networkSession;
            input = inputService;
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
            ReleaseMenuContext();
            if (model != null) model.Changed -= MarkDirty;
            Release();
            UnbindTree();
        }

        private void MarkDirty() => dirty = true;

        // The Lobby is a menu: while it is the active scene the input service is in the Menu context, which unlocks and shows the cursor (without it a real build's mouse is dead:
        // PlayerInputAdapter locks the cursor in every other context). Written on the Lobby transitions only, and only taken back if this view raised it.
        private void TrackLobby()
        {
            bool lobby = SceneManager.GetActiveScene().name == SceneNames.Lobby;
            if (lobby == lobbyActive) return;
            lobbyActive = lobby;
            if (lobby)
            {
                if (input != null && !input.MenuOpen)
                {
                    input.MenuOpen = true;
                    menuOpenByDoor = true;
                }
            }
            else
            {
                ReleaseMenuContext();
            }
        }

        private void ReleaseMenuContext()
        {
            if (menuOpenByDoor && input != null) input.MenuOpen = false;
            menuOpenByDoor = false;
            lobbyActive = false;
        }

        private void Update()
        {
            if (model != null) TrackLobby();
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

        // The Lobby is the door's room. A session the door did not start (the dev panel's, a test's) leaves the model on Title, Worlds or JoinEntry while it runs;
        // the whole door yields to it, so none of its buttons can be pressed mid-session.
        private bool ShouldShow()
        {
            if (SceneManager.GetActiveScene().name != SceneNames.Lobby) return false;
            bool foreignSession = session != null && session.State != ConnState.Offline
                && (model.State == FrontDoorState.Title || model.State == FrontDoorState.Worlds || model.State == FrontDoorState.JoinEntry);
            return !foreignSession;
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
            screens[FrontDoorState.ConfirmUnsaved] = root.Q<VisualElement>(Ids.ScreenUnsaved);

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
            Hook(Ids.RemoveConfirmButton, () => model?.ConfirmRemovePlayer());
            Hook(Ids.RemoveCancelButton, () => model?.CancelRemovePlayer());

            Hook(Ids.UnsavedYes, () => { if (model != null) Fire(model.PlayAnyway()); });
            Hook(Ids.UnsavedNo, () => model?.DeclinePlayAnyway());
            Hook(Ids.RenameYes, () => model?.ConfirmRename());
            Hook(Ids.RenameNo, () => model?.CancelRename());
            Hook(Ids.SendAwayYes, () => model?.ConfirmSendAway());
            Hook(Ids.SendAwayNo, () => model?.CancelSendAway());

            Hook(Ids.CopyCode, CopyCode);
            Hook(Ids.Start, () => model?.StartSession());
            Hook(Ids.LobbyLeave, Leave);

            Hook(Ids.JoinGo, () => { if (model != null) Fire(model.Join()); });
            Hook(Ids.JoinBack, () => { if (model != null) Fire(model.Back()); });
            TextField code = root.Q<TextField>(Ids.CodeField);
            if (code != null)
            {
                code.RegisterValueChangedCallback(e => { if (model != null) model.JoinCodeInput = e.newValue; });
                // A focused text field handles keyboard keys itself and sends no navigation event for them: Escape is Back here, Return is Join.
                code.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Escape)
                    {
                        GoBack();
                        e.StopPropagation();
                    }
                }, TrickleDown.TrickleDown);
                // Enter on the Input System path arrives as a NavigationSubmit (the UI module's Submit action), which the field does not turn into Join by itself.
                code.RegisterCallback<NavigationSubmitEvent>(e =>
                {
                    if (model != null && model.JoinAction.Enabled) Fire(model.Join());
                    e.StopPropagation();
                }, TrickleDown.TrickleDown);
                code.RegisterCallback<KeyDownEvent>(e =>
                {
                    if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && model != null && model.JoinAction.Enabled)
                    {
                        Fire(model.Join());
                        e.StopPropagation();
                    }
                });
            }

            // The Connecting screen's focused button is Cancel, so a second press of the button that started the attempt would land on it: ignore presses for a moment.
            Hook(Ids.ConnectingCancel, () =>
            {
                if (Time.unscaledTime - connectingSince < CancelIgnoreSeconds) return;
                Leave();
            });
            Hook(Ids.WaitingLeave, Leave);
            Hook(Ids.EndedOk, () => model?.Acknowledge());

            // Escape and gamepad B go back from any screen (trickle down, so a focused control cannot swallow them). They never end a session that is up: on the host lobby
            // and the waiting screen they only move focus to the Leave button.
            root.RegisterCallback<NavigationCancelEvent>(e => { GoBack(); e.StopPropagation(); }, TrickleDown.TrickleDown);

            root.style.display = DisplayStyle.None;   // Update shows it when it should be up
            foreach (VisualElement screen in screens.Values) if (screen != null) screen.style.display = DisplayStyle.None;
            shown = false;
            dirty = true;
            renderedState = null;
            worldsSignature = "";
            playersSignature = "";
            renameSignature = "";
            sendAwaySignature = "";
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

        private void GoBack()
        {
            if (model == null) return;
            if (model.State == FrontDoorState.HostLobby) wantedFocus = Ids.LobbyLeave;
            else if (model.State == FrontDoorState.WaitingForHost) wantedFocus = Ids.WaitingLeave;
            Fire(model.Back());
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
            if (stateChanged && model.State == FrontDoorState.Connecting) connectingSince = Time.unscaledTime;

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
                case FrontDoorState.ConfirmUnsaved: SetText(Ids.UnsavedAsk, model.ConfirmUnsavedWords); break;
            }

            if (stateChanged) wantedFocus = PreferredFocus();
            else if (wantedFocus == null && !FocusIsUsable()) wantedFocus = PreferredFocus();
        }

        private void RenderWorlds()
        {
            SetText(Ids.WorldsHeading, model.WorldsKind == WorldKind.Solo ? "Solo worlds" : "Multiplayer worlds");

            var signature = new StringBuilder();
            foreach (FrontDoorWorldRow row in model.Rows)
            {
                signature.Append(row.Id).Append('|').Append(row.Selected).Append('|').Append(row.LastPlayed).Append('|').Append(string.Join(",", row.PlayerNames));
                if (row.Players != null) foreach (FrontDoorPlayerRow p in row.Players) signature.Append('|').Append(p.Key.Id);   // a player let go must rebuild the row
                signature.Append(';');
            }
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

            // Each question (delete a world, let one of its players go) is modal: while one is open only its own two buttons are on, and the No
            // button hands focus back to the row it was asked about.
            bool deletePending = model.PendingDeleteId != null;
            bool removePending = model.PendingRemoveWorldId != null;
            bool pending = deletePending || removePending;
            SetDisplay(Ids.DeleteConfirm, deletePending);
            SetText(Ids.DeleteAsk, model.PendingDeleteWords);
            SetDisplay(Ids.RemoveConfirm, removePending);
            SetText(Ids.RemoveAsk, model.PendingRemoveWords);
            if (pending != renderedPending)
            {
                renderedPending = pending;
                if (pending)
                {
                    string askedAbout = model.PendingDeleteId ?? model.PendingRemoveWorldId;
                    pendingNumber = 1;
                    for (int i = 0; i < model.Rows.Count; i++) if (model.Rows[i].Id == askedAbout) pendingNumber = i + 1;
                    pendingRowCount = model.Rows.Count;
                    FrontDoorWorldRow asked = pendingNumber <= model.Rows.Count ? model.Rows[pendingNumber - 1] : null;
                    pendingAskedFrom = deletePending || asked == null
                        ? Ids.DeleteWorld(pendingNumber)
                        : Ids.RemovePlayer(pendingNumber, PlayerNumberOf(asked, model.PendingRemoveKey));
                    wantedFocus = deletePending ? Ids.DeleteCancelButton : Ids.RemoveCancelButton;
                }
                else
                {
                    string back = pendingAskedFrom ?? Ids.DeleteWorld(pendingNumber);
                    wantedFocus = model.Rows.Count == pendingRowCount && root.Q<VisualElement>(back) != null ? back : PreferredFocus();
                }
            }
            VisualElement list = root.Q<VisualElement>(Ids.WorldList);
            if (list != null)
                list.Query<Button>().ForEach(b => { if (b.enabledSelf == pending) b.SetEnabled(!pending); });

            SetText(Ids.WorldsNotice, model.Notice);
            SetAction(Ids.NewWorld, model.NewWorldAction, pending);
            SetText(Ids.NewWorldWhy, model.NewWorldAction.Why);

            bool solo = model.WorldsKind == WorldKind.Solo;
            SetDisplay(Ids.Play, solo);
            SetDisplay(Ids.Host, !solo);
            SetAction(Ids.Play, model.PlayAction, pending);
            SetAction(Ids.Host, model.HostAction, pending);
            SetAction(Ids.WorldsBack, FrontDoorAction.On, pending);
        }

        // The 1-based position of a key among a row's recorded players (1 when it is not there: the row's first remove button).
        private static int PlayerNumberOf(FrontDoorWorldRow row, PlayerKey key)
        {
            if (row.Players == null) return 1;
            for (int i = 0; i < row.Players.Count; i++) if (row.Players[i].Key == key) return i + 1;
            return 1;
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

                // The host's own action on a multiplayer world: let one of its recorded players go, so their place is free again (audit A2).
                if (model.CanRemovePlayers && row.Players != null && row.Players.Count > 0)
                {
                    var players = new VisualElement();
                    players.AddToClassList("fd-world-players");
                    int playerNumber = 0;
                    foreach (FrontDoorPlayerRow player in row.Players)
                    {
                        playerNumber++;
                        PlayerKey key = player.Key;
                        var remove = new Button(() => model?.RequestRemovePlayer(id, key))
                        {
                            name = Ids.RemovePlayer(number, playerNumber),
                            text = "Remove " + player.Name,
                            focusable = true,
                        };
                        remove.AddToClassList("fd-button");
                        remove.AddToClassList("fd-remove-player");
                        players.Add(remove);
                    }
                    list.Add(players);
                }
            }
        }

        private void RenderHostLobby(bool stateChanged)
        {
            if (stateChanged) SetText(Ids.CopyNote, "");
            SetText(Ids.JoinCode, model.JoinCode);
            int count = model.PlayerCount;
            if (count != renderedPlayerCount)
            {
                renderedPlayerCount = count;
                playerCountText = "Players in the game: " + count;
            }
            SetText(Ids.PlayerCount, playerCountText);

            IReadOnlyList<string> names = model.PlayerNames;
            StringBuilder sig = signatureBuilder;
            sig.Clear();
            for (int i = 0; i < names.Count; i++) sig.Append(names[i]).Append('\n');
            if (!SameText(sig, playersSignature))
            {
                playersSignature = sig.ToString();
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

            // The host's "this player is now that player" (audit A3): one button per connected stranger and recorded absent player, then a question.
            IReadOnlyList<FrontDoorRenameOption> options = model.RenameOptions;
            sig.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                FrontDoorRenameOption o = options[i];
                sig.Append(o.NewPlayer.Key.Id).Append('>').Append(o.Recorded.Key.Id).Append(o.NewPlayer.Name).Append(o.Recorded.Name).Append(';');
            }
            VisualElement renameList = root.Q<VisualElement>(Ids.RenameList);
            if (renameList != null && !SameText(sig, renameSignature))
            {
                renameSignature = sig.ToString();
                renameList.Clear();
                for (int i = 0; i < options.Count; i++)
                {
                    PlayerKey fresh = options[i].NewPlayer.Key, old = options[i].Recorded.Key;
                    var button = new Button(() => model?.RequestRename(fresh, old))
                    {
                        name = Ids.RenameOption(i + 1),
                        text = options[i].NewPlayer.Name + " is really " + options[i].Recorded.Name,
                        focusable = true,
                    };
                    button.AddToClassList("fd-button");
                    renameList.Add(button);
                }
            }

            // A stranger waiting in a full world's lobby can also be sent away (user decision 2026-11-13); it asks first.
            IReadOnlyList<FrontDoorPerson> strangers = model.Strangers;
            sig.Clear();
            for (int i = 0; i < strangers.Count; i++) sig.Append(strangers[i].Key.Id).Append(strangers[i].Name).Append(';');
            VisualElement sendAwayList = root.Q<VisualElement>(Ids.SendAwayList);
            if (sendAwayList != null && !SameText(sig, sendAwaySignature))
            {
                sendAwaySignature = sig.ToString();
                sendAwayList.Clear();
                for (int i = 0; i < strangers.Count; i++)
                {
                    PlayerKey who = strangers[i].Key;
                    var button = new Button(() => model?.RequestSendAway(who))
                    {
                        name = Ids.SendAway(i + 1),
                        text = "Send " + strangers[i].Name + " away",
                        focusable = true,
                    };
                    button.AddToClassList("fd-button");
                    sendAwayList.Add(button);
                }
            }

            bool renamePending = model.HasPendingRename;
            bool sendAwayPending = model.HasPendingSendAway;
            bool anyPending = renamePending || sendAwayPending;
            SetDisplay(Ids.RenameConfirm, renamePending);
            SetText(Ids.RenameAsk, model.PendingRenameWords);
            SetDisplay(Ids.SendAwayConfirm, sendAwayPending);
            SetText(Ids.SendAwayAsk, model.PendingSendAwayWords);
            SetText(Ids.RenameMessage, model.RenameMessage);
            if (renameList != null) renameList.style.display = options.Count > 0 && !anyPending ? DisplayStyle.Flex : DisplayStyle.None;
            if (sendAwayList != null) sendAwayList.style.display = strangers.Count > 0 && !anyPending ? DisplayStyle.Flex : DisplayStyle.None;
            if (renamePending != renderedRenamePending || sendAwayPending != renderedSendAwayPending)
            {
                renderedRenamePending = renamePending;
                renderedSendAwayPending = sendAwayPending;
                wantedFocus = renamePending ? Ids.RenameNo : sendAwayPending ? Ids.SendAwayNo : PreferredFocus();
            }

            FrontDoorAction start = model.StartAction;
            SetAction(Ids.Start, start, anyPending);
            SetText(Ids.StartWhy, start.Why);
            SetText(Ids.StartMessage, model.StartMessage);
        }

        private static bool SameText(StringBuilder builder, string text)
        {
            if (builder.Length != text.Length) return false;
            for (int i = 0; i < text.Length; i++) if (builder[i] != text[i]) return false;
            return true;
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

        private void SetAction(string name, FrontDoorAction action, bool forcedOff = false)
        {
            VisualElement element = root.Q<VisualElement>(name);
            bool on = action.Enabled && !forcedOff;
            if (element != null && element.enabledSelf != on) element.SetEnabled(on);
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
                case FrontDoorState.ConfirmUnsaved: return Ids.UnsavedNo;   // the safe answer is the one Enter gives
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
