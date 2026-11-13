// Not networked; the front door is local on every peer and only ASKS (PL-3 task 3.2). Authority stays where it was: the host's start gate
// (HostWorld.TryStartSession, reached through SceneFlow.TryStartSession) and the join approval (ConnectionApprover, HostWorld.Admit) decide; this
// model never reads or writes world or session state itself. Plain C# (the only engine call is Debug.LogWarning), so EditMode tests drive it with a fake session.
//
// States: Title, Worlds (the solo list or the multiplayer list), HostLobby (join code, who is in, Start), JoinEntry (type a code), Connecting,
// WaitingForHost (a guest that is in, before the host starts), Ended (the session ended or could not start: the reason in words, then Title on
// Acknowledge). It calls only what App/DevSessionPanel calls today: WorldSaveFlow Select / ListWorlds / CreateWorld / DeleteWorld (IFrontDoorWorlds),
// INetworkSession StartLocalAsync / HostOnlineAsync / JoinOnlineAsync / LeaveAsync, SceneFlow.TryStartSession + LastStartVerdict and HostRoster
// (IFrontDoorHost). Dev-direct host and join stay in the dev panel. The front door hosts and joins ONLINE worlds only (Relay codes).
// The view reads the properties each frame (nothing is cached except the world rows) and calls the commands; Changed fires after any change.
// Commands that start or end a session return the Task, which never faults (a failure becomes the state); the view may ignore it.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.App
{
    public enum FrontDoorState { Title, Worlds, HostLobby, JoinEntry, Connecting, WaitingForHost, Ended }

    // A button: on, or off with the words that say why (null Why when on, and for a button that is simply not part of this screen).
    public readonly struct FrontDoorAction
    {
        public readonly bool Enabled;
        public readonly string Why;

        private FrontDoorAction(bool enabled, string why)
        {
            Enabled = enabled;
            Why = why;
        }

        public static FrontDoorAction On => new FrontDoorAction(true, null);
        public static FrontDoorAction Off(string why = null) => new FrontDoorAction(false, why);
    }

    public sealed class FrontDoorWorldRow
    {
        public string Id;
        public string Title;                       // "World 3" (position in the list, newest first)
        public IReadOnlyList<string> PlayerNames;  // the recorded players
        public string LastPlayed;                  // "2026-11-12 10:30", or ""
        public bool Selected;
    }

    // What the front door needs of the world library (WorldSaveFlow implements it).
    public interface IFrontDoorWorlds
    {
        string SelectedWorldId { get; }
        void Select(string worldId);
        IReadOnlyList<WorldSummary> ListWorlds(WorldKind kind, IdentityKind identity);
        string CreateWorld(WorldKind kind, IdentityKind identity);   // null when that list is full
        bool DeleteWorld(string worldId);
    }

    // What the front door needs of the host's side: the start gate and who is connected (FrontDoorHost adapts SceneFlow and HostRoster).
    public interface IFrontDoorHost
    {
        int ConnectedCount { get; }
        void GetConnectedNames(List<string> into);
        bool TryStartSession();
        WorldPlayerVerdict LastStartVerdict { get; }
    }

    public sealed class FrontDoorModel : IDisposable
    {
        private enum Attempt { None, Solo, Host, Join }

        private static readonly Task Done = Task.CompletedTask;

        private readonly INetworkSession session;
        private readonly IFrontDoorWorlds worlds;
        private readonly IFrontDoorHost host;
        private readonly List<FrontDoorWorldRow> rows = new List<FrontDoorWorldRow>();
        private readonly List<string> names = new List<string>();

        private FrontDoorState state = FrontDoorState.Title;
        private WorldKind worldsKind = WorldKind.Solo;
        private Attempt attempt = Attempt.None;
        private int attemptId;
        private string joinInput = "";
        private bool leaveRequested;   // the player pressed Leave / Cancel for the attempt in flight: its end is their choice, whatever the host's last verdict says
        private bool disposed;

        public FrontDoorModel(INetworkSession session, IFrontDoorWorlds worlds, IFrontDoorHost host)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            session.Ended += HandleEnded;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.Ended -= HandleEnded;
        }

        // Raised after any change (state, rows, texts). The view redraws or just reads each frame.
        public event Action Changed;

        // ---- what to show ----

        public FrontDoorState State => state;

        // Worlds screen: which list. Solo = local solo worlds (Play); Multiplayer = online multiplayer worlds (Host).
        public WorldKind WorldsKind => worldsKind;
        public IReadOnlyList<FrontDoorWorldRow> Rows => rows;

        // The worlds screen's line under the list ("World set aside.", "That world could not be deleted."), or null.
        public string Notice { get; private set; }

        // Delete asks first: RequestDelete sets this id and the words; ConfirmDelete deletes, CancelDelete drops it.
        public string PendingDeleteId { get; private set; }
        public string PendingDeleteWords => PendingDeleteId == null ? null : UiText.DeleteAsk;

        // HostLobby: the code to read out, who is in, and the Start line. Names and count are read live from the host.
        public string JoinCode => state == FrontDoorState.HostLobby ? session.JoinCode : null;
        public int PlayerCount => host.ConnectedCount;
        public IReadOnlyList<string> PlayerNames
        {
            get
            {
                host.GetConnectedNames(names);
                return names;
            }
        }
        public string StartMessage { get; private set; }   // why the last Start press was refused, in words

        // JoinEntry: the text box (any text; it is normalised when sent) and the last refusal in words.
        public string JoinCodeInput
        {
            get => joinInput;
            set
            {
                joinInput = value ?? "";
                JoinError = null;
                Touch();
            }
        }
        public string JoinError { get; private set; }

        public string ConnectingWords => state == FrontDoorState.Connecting ? UiText.Connecting : null;
        public string WaitingWords => state == FrontDoorState.WaitingForHost ? UiText.WaitingForHost : null;

        // Ended: why (words) and the reason; both null again after Acknowledge.
        public string EndedWords { get; private set; }
        public EndReason? EndedReason { get; private set; }

        // ---- which buttons are on ----

        private bool Busy => state == FrontDoorState.Connecting;

        public FrontDoorAction PlayAction => state == FrontDoorState.Worlds && worldsKind == WorldKind.Solo ? FrontDoorAction.On : FrontDoorAction.Off();
        public FrontDoorAction HostAction => state == FrontDoorState.Worlds && worldsKind == WorldKind.Multiplayer ? FrontDoorAction.On : FrontDoorAction.Off();

        public FrontDoorAction NewWorldAction
        {
            get
            {
                if (state != FrontDoorState.Worlds) return FrontDoorAction.Off();
                return rows.Count < WorldSummary.MaxPerList ? FrontDoorAction.On : FrontDoorAction.Off(UiText.WorldLibraryFull(worldsKind));
            }
        }

        // Start is the host's: off with the reason while fewer than 2 are in. The gate itself (host side) still has the last word.
        public FrontDoorAction StartAction
        {
            get
            {
                if (state != FrontDoorState.HostLobby) return FrontDoorAction.Off();
                if (host.ConnectedCount < WorldPlayers.MinPlayersToStartMultiplayer) return FrontDoorAction.Off(UiText.StartRefusal(WorldPlayerVerdict.NotEnoughPlayers));
                return FrontDoorAction.On;
            }
        }

        public FrontDoorAction JoinAction
        {
            get
            {
                if (state != FrontDoorState.JoinEntry) return FrontDoorAction.Off();
                string code = NormalizedCode;
                if (code.Length == 0) return FrontDoorAction.Off(UiText.JoinCodeEmpty);
                if (!Core.JoinCode.IsPlausible(code)) return FrontDoorAction.Off(UiText.JoinCodeInvalid);
                return FrontDoorAction.On;
            }
        }

        private string NormalizedCode => Core.JoinCode.Normalize(joinInput);

        // ---- commands ----

        // Title: Play. Opens the selected solo world, else the newest, else makes the first one, and starts.
        public Task PlaySolo()
        {
            if (state != FrontDoorState.Title && state != FrontDoorState.Worlds) return Done;
            return StartSolo();
        }

        // Title: Host or Join, or the solo world list.
        public void OpenWorlds(WorldKind kind)
        {
            if (state != FrontDoorState.Title && state != FrontDoorState.Worlds) return;
            worldsKind = kind;
            state = FrontDoorState.Worlds;
            Notice = null;
            PendingDeleteId = null;
            RebuildRows();
            Touch();
        }

        public void OpenJoin()
        {
            if (state != FrontDoorState.Title) return;
            state = FrontDoorState.JoinEntry;
            JoinError = null;
            Touch();
        }

        public void SelectWorld(string worldId)
        {
            if (state != FrontDoorState.Worlds || FindRow(worldId) == null) return;
            worlds.Select(worldId);
            RebuildRows();
            Touch();
        }

        public void CreateWorld()
        {
            if (!NewWorldAction.Enabled) return;
            string id = worlds.CreateWorld(worldsKind, KindIdentity(worldsKind));
            Notice = id == null ? UiText.WorldLibraryFull(worldsKind) : null;
            RebuildRows();
            Touch();
        }

        public void RequestDelete(string worldId)
        {
            if (state != FrontDoorState.Worlds || FindRow(worldId) == null) return;
            PendingDeleteId = worldId;
            Touch();
        }

        public void ConfirmDelete()
        {
            if (state != FrontDoorState.Worlds || PendingDeleteId == null) return;
            bool deleted = worlds.DeleteWorld(PendingDeleteId);
            PendingDeleteId = null;
            Notice = deleted ? UiText.WorldDeleted : UiText.WorldNotDeleted;
            RebuildRows();
            Touch();
        }

        public void CancelDelete()
        {
            if (PendingDeleteId == null) return;
            PendingDeleteId = null;
            Touch();
        }

        // Worlds (solo list): Play the selected world (else the newest).
        public Task PlaySelected() => PlayAction.Enabled ? StartSolo() : Done;

        // Worlds (multiplayer list): host the selected world (else the newest, else a new one) online.
        public Task HostSelected()
        {
            if (!HostAction.Enabled) return Done;
            IReadOnlyList<WorldSummary> list = worlds.ListWorlds(WorldKind.Multiplayer, IdentityKind.Ugs);
            if (list.Count == 0)
            {
                if (worlds.CreateWorld(WorldKind.Multiplayer, IdentityKind.Ugs) == null)
                {
                    Notice = UiText.WorldLibraryFull(WorldKind.Multiplayer);
                    Touch();
                    return Done;
                }
            }
            else if (!Contains(list, worlds.SelectedWorldId))
            {
                worlds.Select(list[0].Id);
            }
            return Begin(Attempt.Host, () => session.HostOnlineAsync(CancellationToken.None));
        }

        // HostLobby: ask the host's start gate. A refusal shows its words and the host stays and waits.
        public void StartSession()
        {
            if (!StartAction.Enabled) return;
            StartMessage = host.TryStartSession() ? null : UiText.StartRefusal(host.LastStartVerdict);
            Touch();
        }

        // JoinEntry: join online with the typed code.
        public Task Join()
        {
            if (!JoinAction.Enabled) return Done;
            string code = NormalizedCode;
            return Begin(Attempt.Join, () => session.JoinOnlineAsync(code, CancellationToken.None));
        }

        // HostLobby, WaitingForHost or Connecting: leave (a cancel while connecting). The player chose it, so no reason screen. The door goes back only once the
        // session is Offline; if the session could not stop, the screen stays (the session is still up) and the player can press again.
        public async Task Leave()
        {
            if (state != FrontDoorState.HostLobby && state != FrontDoorState.WaitingForHost && state != FrontDoorState.Connecting) return;
            leaveRequested = true;
            bool left;
            try
            {
                await session.LeaveAsync();
                left = session.State == ConnState.Offline;
            }
            catch (Exception e)
            {
                Debug.LogWarning("FrontDoorModel: leaving the session failed: " + e.GetBaseException().Message);
                left = false;
            }
            if (!left)
            {
                leaveRequested = false;
                return;
            }
            if (state == FrontDoorState.HostLobby || state == FrontDoorState.WaitingForHost || state == FrontDoorState.Connecting) ResetAfterLeave();
        }

        // The Back / Escape / gamepad B of every screen. It never ends a session that is up: a host lobby or a waiting guest is left only by the Leave button
        // (a stray Escape must not drop everyone). It does cancel a connect in progress, answers a delete question with no, and otherwise goes up one screen.
        public Task Back()
        {
            switch (state)
            {
                case FrontDoorState.Worlds:
                    if (PendingDeleteId != null)
                    {
                        CancelDelete();
                        return Done;
                    }
                    state = FrontDoorState.Title;
                    Touch();
                    return Done;
                case FrontDoorState.JoinEntry:
                    state = FrontDoorState.Title;
                    PendingDeleteId = null;
                    Touch();
                    return Done;
                case FrontDoorState.Connecting:
                    return Leave();
                case FrontDoorState.Ended:
                    Acknowledge();
                    return Done;
                default:   // Title, HostLobby, WaitingForHost
                    return Done;
            }
        }

        // Ended: the player has read it.
        public void Acknowledge()
        {
            if (state != FrontDoorState.Ended) return;
            ToTitle();
        }

        // ---- inside ----

        private Task StartSolo()
        {
            IReadOnlyList<WorldSummary> list = worlds.ListWorlds(WorldKind.Solo, IdentityKind.Local);
            if (list.Count == 0)
            {
                if (worlds.CreateWorld(WorldKind.Solo, IdentityKind.Local) == null)
                {
                    Notice = UiText.WorldLibraryFull(WorldKind.Solo);
                    Touch();
                    return Done;
                }
            }
            else if (!Contains(list, worlds.SelectedWorldId))
            {
                worlds.Select(list[0].Id);
            }
            return Begin(Attempt.Solo, () => session.StartLocalAsync(false, CancellationToken.None));
        }

        // One start attempt. The session raises Ended itself when it fails (the reason there wins); the exception is only a fallback.
        private async Task Begin(Attempt kind, Func<Task> start)
        {
            if (Busy) return;
            int id = ++attemptId;
            attempt = kind;
            leaveRequested = false;
            StartMessage = null;
            JoinError = null;
            Notice = null;
            state = FrontDoorState.Connecting;
            Touch();
            try
            {
                await start();
                if (id == attemptId && state == FrontDoorState.Connecting) Connected();
            }
            catch (Exception e)
            {
                if (id == attemptId && state == FrontDoorState.Connecting)
                    End(e.GetBaseException() is NetworkSessionException ex ? ex.Reason : EndReason.Unknown);
                else
                    Debug.LogWarning("FrontDoorModel: a start attempt that was already cancelled or ended failed late: " + e.GetBaseException().Message);
            }
        }

        private void Connected()
        {
            // Solo stays in Connecting: SceneFlow starts it at once and loads the town. A guest waits; a host opens its lobby.
            if (attempt == Attempt.Host) state = FrontDoorState.HostLobby;
            else if (attempt == Attempt.Join) state = FrontDoorState.WaitingForHost;
            Touch();
        }

        private void HandleEnded(EndReason reason)
        {
            if (disposed) return;
            switch (state)
            {
                case FrontDoorState.Title:
                case FrontDoorState.Worlds:
                case FrontDoorState.JoinEntry:
                case FrontDoorState.Ended:
                    return;   // a session this door did not start (the dev panel's), or one already reported
            }
            End(reason);
        }

        private void End(EndReason reason)
        {
            bool joining = state == FrontDoorState.Connecting && attempt == Attempt.Join;
            bool startingSolo = state == FrontDoorState.Connecting && attempt == Attempt.Solo;
            attemptId++;   // a late completion of the attempt that just ended is ignored

            if (joining)
            {
                state = FrontDoorState.JoinEntry;
                JoinError = UiText.JoinRefusal(reason);   // null for a cancel
            }
            else if (startingSolo && !leaveRequested && reason == EndReason.UserLeft && host.LastStartVerdict != WorldPlayerVerdict.Ok)
            {
                // SceneFlow leaves a solo session whose start the gate refused; say why instead of going quietly back.
                ShowEnded(reason, UiText.StartRefusal(host.LastStartVerdict));
            }
            else
            {
                string words = UiText.EndedBecause(reason);
                if (words == null) ToTitle();
                else ShowEnded(reason, words);
            }
            Touch();
        }

        private void ShowEnded(EndReason reason, string words)
        {
            state = FrontDoorState.Ended;
            EndedReason = reason;
            EndedWords = words;
            StartMessage = null;
        }

        private void ResetAfterLeave()
        {
            if (state == FrontDoorState.Connecting && attempt == Attempt.Join)
            {
                attemptId++;
                state = FrontDoorState.JoinEntry;
                JoinError = null;
                Touch();
            }
            else
            {
                ToTitle();
            }
        }

        private void ToTitle()
        {
            attemptId++;
            leaveRequested = false;
            attempt = Attempt.None;
            state = FrontDoorState.Title;
            EndedWords = null;
            EndedReason = null;
            StartMessage = null;
            JoinError = null;
            PendingDeleteId = null;
            Touch();
        }

        private static IdentityKind KindIdentity(WorldKind kind) => kind == WorldKind.Solo ? IdentityKind.Local : IdentityKind.Ugs;

        private void RebuildRows()
        {
            rows.Clear();
            IReadOnlyList<WorldSummary> list = worlds.ListWorlds(worldsKind, KindIdentity(worldsKind));
            string selected = worlds.SelectedWorldId;
            for (int i = 0; i < list.Count; i++)
            {
                WorldSummary w = list[i];
                rows.Add(new FrontDoorWorldRow
                {
                    Id = w.Id,
                    Title = "World " + (i + 1),
                    PlayerNames = w.PlayerNames,
                    LastPlayed = ShortTime(w.LastPlayedUtc),
                    Selected = w.Id == selected,
                });
            }
        }

        private FrontDoorWorldRow FindRow(string id)
        {
            foreach (FrontDoorWorldRow r in rows) if (r.Id == id) return r;
            return null;
        }

        private static bool Contains(IReadOnlyList<WorldSummary> list, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (WorldSummary w in list) if (w.Id == id) return true;
            return false;
        }

        private static string ShortTime(string iso) => string.IsNullOrEmpty(iso) ? "" : (iso.Length >= 16 ? iso.Substring(0, 16).Replace('T', ' ') : iso);

        private void Touch() => Changed?.Invoke();
    }
}
