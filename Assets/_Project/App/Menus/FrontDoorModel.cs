// Not networked; the front door is local on every peer and only ASKS (PL-3 task 3.2). Authority stays where it was: the host's start gate
// (HostWorld.TryStartSession, reached through SceneFlow.TryStartSession) and the join approval (ConnectionApprover, HostWorld.Admit) decide; this
// model never reads or writes world or session state itself. Plain C# (the only engine call is Debug.LogWarning), so EditMode tests drive it with a fake session.
//
// States: Title, Worlds (the solo list or the multiplayer list), HostLobby (join code, who is in, Start), JoinEntry (type a code), Connecting,
// WaitingForHost (a guest that is in, before the host starts), Ended (the session ended or could not start: the reason in words, then Title on
// Acknowledge). The multiplayer world list also carries the host's own "remove player" action, which lets one of a world's recorded players go so
// their place is free again (audit A2, user decision 2026-11-13); like Delete it asks first, and only one question is open at a time.
// It calls only what App/DevSessionPanel calls today plus that one: WorldSaveFlow Select / ListWorlds / CreateWorld / DeleteWorld / RemovePlayer (IFrontDoorWorlds),
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
    public enum FrontDoorState { Title, Worlds, HostLobby, JoinEntry, Connecting, WaitingForHost, Ended, ConfirmUnsaved }

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

    // One recorded player of a world row: what to show, and the key the host's "remove player" acts on.
    public sealed class FrontDoorPlayerRow
    {
        public string Name;
        public PlayerKey Key;
    }

    // A player of the host lobby's "this player is now that player" action: a key and the name to show.
    public sealed class FrontDoorPerson
    {
        public string Name;
        public PlayerKey Key;
    }

    // One offer of that action: `NewPlayer` (connected, not recorded in the world) is really `Recorded` (in the world, not connected).
    public sealed class FrontDoorRenameOption
    {
        public FrontDoorPerson NewPlayer;
        public FrontDoorPerson Recorded;
    }

    public sealed class FrontDoorWorldRow
    {
        public string Id;
        public string Title;                       // "World 3" (position in the list, newest first)
        public IReadOnlyList<string> PlayerNames;  // the recorded players
        public IReadOnlyList<FrontDoorPlayerRow> Players;   // the same players with their keys (the host's remove action)
        public string LastPlayed;                  // "2026-11-12 10:30", or ""
        public bool Selected;
        public bool Damaged;                       // the world's file cannot be read (audit A6): listed so it can be set aside, never selected or opened
    }

    // What the front door needs of the world library (WorldSaveFlow implements it).
    public interface IFrontDoorWorlds
    {
        string SelectedWorldId { get; }
        void Select(string worldId);
        IReadOnlyList<WorldSummary> ListWorlds(WorldKind kind, IdentityKind identity);
        string CreateWorld(WorldKind kind, IdentityKind identity);   // null when that list is full
        bool DeleteWorld(string worldId);
        bool RemovePlayer(string worldId, PlayerKey key);            // lets one of a multiplayer world's recorded players go (audit A2)
        bool CanSaveWorld(string worldId);                           // false when the world loads read-only (a newer file or a load fault): nothing played in it would be kept (audit A5)
    }

    // What the front door needs of the host's side: the start gate and who is connected (FrontDoorHost adapts SceneFlow and HostRoster).
    public interface IFrontDoorHost
    {
        int ConnectedCount { get; }
        void GetConnectedNames(List<string> into);
        bool TryStartSession();
        WorldPlayerVerdict LastStartVerdict { get; }
        // The host lobby's "this player is now that player" (audit A3): who is connected but not recorded in the hosted world, who is recorded but
        // not connected (both empty unless the world is Multiplayer and its session has not started), and the rename itself (the host's gate decides).
        void GetRenameChoices(List<FrontDoorPerson> newcomers, List<FrontDoorPerson> recordedAway);
        WorldPlayerVerdict RenamePlayer(PlayerKey from, PlayerKey to);
        // A full world's lobby (user decision 2026-11-13): who is connected without a place (not recorded, and the world has none to give), and
        // the host sending one away (disconnects them with EndReason.SentAway; false when they are gone already).
        void GetStrangers(List<FrontDoorPerson> into);
        bool SendAway(PlayerKey key);
        // On a guest: true when the host has told this machine it is connected without a place in a full world (it can only wait to be added).
        bool WaitsForPlace { get; }
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
        private string pendingRemoveName = "";
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

        // Removing a recorded player from a multiplayer world asks first too (it is what the world keeps for good): RequestRemovePlayer sets
        // these, ConfirmRemovePlayer does it, CancelRemovePlayer drops the question. Only one question is open at a time.
        public string PendingRemoveWorldId { get; private set; }
        public PlayerKey PendingRemoveKey { get; private set; }
        public string PendingRemoveWords => PendingRemoveWorldId == null ? null : UiText.RemovePlayerAsk(pendingRemoveName);

        // The host may let a player go from a multiplayer world in the picker (never from a solo world: its one owner follows the machine).
        public bool CanRemovePlayers => state == FrontDoorState.Worlds && worldsKind == WorldKind.Multiplayer;

        // Host lobby (audit A3): a player whose key changed (a new PC, a reinstall) joins as a stranger; the host says "this connected player is
        // that recorded player", which moves the record, growth, bags and suspend to the new key. RenameOptions lists every connected stranger
        // against every recorded player who is not connected (empty when there is no one to choose from); asking changes nothing, the host confirms.
        private readonly List<FrontDoorPerson> renameNew = new List<FrontDoorPerson>();
        private readonly List<FrontDoorPerson> renameAway = new List<FrontDoorPerson>();
        private readonly List<FrontDoorRenameOption> renameOptions = new List<FrontDoorRenameOption>();
        private readonly List<FrontDoorRenameOption> renameOptionPool = new List<FrontDoorRenameOption>();   // reused: the lobby reads RenameOptions every frame
        private FrontDoorRenameOption pendingRename;

        public IReadOnlyList<FrontDoorRenameOption> RenameOptions
        {
            get
            {
                renameOptions.Clear();
                if (state != FrontDoorState.HostLobby) return renameOptions;
                host.GetRenameChoices(renameNew, renameAway);
                foreach (FrontDoorPerson fresh in renameNew)
                    foreach (FrontDoorPerson old in renameAway)
                    {
                        int at = renameOptions.Count;
                        if (renameOptionPool.Count <= at) renameOptionPool.Add(new FrontDoorRenameOption());
                        FrontDoorRenameOption option = renameOptionPool[at];
                        option.NewPlayer = fresh;
                        option.Recorded = old;
                        renameOptions.Add(option);
                    }
                return renameOptions;
            }
        }
        public bool HasPendingRename => pendingRename != null && state == FrontDoorState.HostLobby;
        public PlayerKey PendingRenameNewKey => pendingRename == null ? default : pendingRename.NewPlayer.Key;
        public PlayerKey PendingRenameOldKey => pendingRename == null ? default : pendingRename.Recorded.Key;
        public string PendingRenameWords => HasPendingRename ? UiText.RenamePlayerAsk(pendingRename.NewPlayer.Name, pendingRename.Recorded.Name) : null;
        public string RenameMessage { get; private set; }   // what the last rename (or send-away) did, in words

        // A full world's strangers (user decision 2026-11-13): the host can send one away instead of renaming them. Asks first, like Remove.
        private readonly List<FrontDoorPerson> strangers = new List<FrontDoorPerson>();
        private FrontDoorPerson pendingSendAway;
        public IReadOnlyList<FrontDoorPerson> Strangers
        {
            get
            {
                strangers.Clear();
                if (state == FrontDoorState.HostLobby) host.GetStrangers(strangers);
                return strangers;
            }
        }
        public bool HasPendingSendAway => pendingSendAway != null && state == FrontDoorState.HostLobby;
        public PlayerKey PendingSendAwayKey => pendingSendAway == null ? default : pendingSendAway.Key;
        public string PendingSendAwayWords => HasPendingSendAway ? UiText.SendAwayAsk(pendingSendAway.Name) : null;

        public void RequestSendAway(PlayerKey key)
        {
            if (state != FrontDoorState.HostLobby) return;
            foreach (FrontDoorPerson person in Strangers)
            {
                if (person.Key != key) continue;
                pendingSendAway = new FrontDoorPerson { Key = person.Key, Name = person.Name };
                pendingRename = null;
                RenameMessage = null;
                Touch();
                return;
            }
        }

        public void ConfirmSendAway()
        {
            if (!HasPendingSendAway) return;
            FrontDoorPerson asked = pendingSendAway;
            pendingSendAway = null;
            RenameMessage = host.SendAway(asked.Key) ? UiText.PlayerSentAway(asked.Name) : UiText.PlayerAlreadyLeft(asked.Name);
            Touch();
        }

        public void CancelSendAway()
        {
            if (pendingSendAway == null) return;
            pendingSendAway = null;
            Touch();
        }

        public void RequestRename(PlayerKey newKey, PlayerKey recordedKey)
        {
            if (state != FrontDoorState.HostLobby) return;
            foreach (FrontDoorRenameOption option in RenameOptions)
            {
                if (option.NewPlayer.Key != newKey || option.Recorded.Key != recordedKey) continue;
                pendingRename = new FrontDoorRenameOption   // copies: the host's person objects are reused every frame
                {
                    NewPlayer = new FrontDoorPerson { Key = option.NewPlayer.Key, Name = option.NewPlayer.Name },
                    Recorded = new FrontDoorPerson { Key = option.Recorded.Key, Name = option.Recorded.Name },
                };
                pendingSendAway = null;
                RenameMessage = null;
                Touch();
                return;
            }
        }

        public void ConfirmRename()
        {
            if (!HasPendingRename) return;
            FrontDoorRenameOption asked = pendingRename;
            pendingRename = null;
            WorldPlayerVerdict verdict = host.RenamePlayer(asked.Recorded.Key, asked.NewPlayer.Key);
            RenameMessage = verdict == WorldPlayerVerdict.Ok ? UiText.PlayerRenamed(asked.Recorded.Name) : UiText.PlayerNotRenamed(verdict);
            Touch();
        }

        public void CancelRename()
        {
            if (pendingRename == null) return;
            pendingRename = null;
            Touch();
        }

        // Audit A5: a world that cannot be saved (a newer file, a load fault) asks "play anyway?" first. Play or Host on such a world opens the
        // ConfirmUnsaved screen instead of starting; PlayAnyway starts it, DeclinePlayAnyway (or Back) starts nothing and goes back.
        private Attempt unsavedAttempt = Attempt.None;
        private FrontDoorState unsavedReturn = FrontDoorState.Title;
        public string ConfirmUnsavedWords => state == FrontDoorState.ConfirmUnsaved ? UiText.CannotSaveAsk : null;

        public Task PlayAnyway()
        {
            if (state != FrontDoorState.ConfirmUnsaved) return Done;
            Attempt kind = unsavedAttempt;
            unsavedAttempt = Attempt.None;
            state = unsavedReturn;
            return kind == Attempt.Host ? BeginHost() : BeginSolo();
        }

        public void DeclinePlayAnyway()
        {
            if (state != FrontDoorState.ConfirmUnsaved) return;
            unsavedAttempt = Attempt.None;
            state = unsavedReturn;
            Touch();
        }

        // True (and the question is open) when the world the attempt would open cannot be saved.
        private bool AskIfUnsaved(Attempt kind)
        {
            string id = worlds.SelectedWorldId;
            if (string.IsNullOrEmpty(id) || worlds.CanSaveWorld(id)) return false;
            unsavedAttempt = kind;
            unsavedReturn = state;
            state = FrontDoorState.ConfirmUnsaved;
            Touch();
            return true;
        }

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
        public string WaitingWords => state == FrontDoorState.WaitingForHost ? (host.WaitsForPlace ? UiText.WaitingForPlace : UiText.WaitingForHost) : null;

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
                int readable = 0;
                foreach (FrontDoorWorldRow r in rows) if (!r.Damaged) readable++;   // a damaged world takes no place (audit A6)
                return readable < WorldSummary.MaxPerList ? FrontDoorAction.On : FrontDoorAction.Off(UiText.WorldLibraryFull(worldsKind));
            }
        }

        // Start is the host's: off with the reason while fewer than 2 are in. The gate itself (host side) still has the last word.
        public FrontDoorAction StartAction
        {
            get
            {
                if (state != FrontDoorState.HostLobby) return FrontDoorAction.Off();
                if (host.ConnectedCount < WorldPlayers.MinPlayersToStartMultiplayer) return FrontDoorAction.Off(UiText.StartRefusal(WorldPlayerVerdict.NotEnoughPlayers));
                if (Strangers.Count > 0) return FrontDoorAction.Off(UiText.StartRefusal(WorldPlayerVerdict.WorldFull));   // rename them as a friend or send them away first
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
            ClearPendingRemove();
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
            FrontDoorWorldRow picked = FindRow(worldId);
            if (state != FrontDoorState.Worlds || picked == null || picked.Damaged) return;   // a damaged world can be set aside, never picked (audit A6)
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
            ClearPendingRemove();
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

        // Worlds (multiplayer list): the host asks to let one of a world's recorded players go. Asking changes nothing.
        public void RequestRemovePlayer(string worldId, PlayerKey key)
        {
            if (!CanRemovePlayers) return;
            FrontDoorWorldRow row = FindRow(worldId);
            if (row == null) return;
            FrontDoorPlayerRow player = FindPlayer(row, key);
            if (player == null) return;
            PendingDeleteId = null;
            PendingRemoveWorldId = worldId;
            PendingRemoveKey = key;
            pendingRemoveName = player.Name;
            Touch();
        }

        public void ConfirmRemovePlayer()
        {
            if (!CanRemovePlayers || PendingRemoveWorldId == null) return;
            bool removed = worlds.RemovePlayer(PendingRemoveWorldId, PendingRemoveKey);
            Notice = removed ? UiText.PlayerRemoved(pendingRemoveName) : UiText.PlayerNotRemoved;
            ClearPendingRemove();
            RebuildRows();
            Touch();
        }

        public void CancelRemovePlayer()
        {
            if (PendingRemoveWorldId == null) return;
            ClearPendingRemove();
            Touch();
        }

        private void ClearPendingRemove()
        {
            PendingRemoveWorldId = null;
            PendingRemoveKey = default;
            pendingRemoveName = "";
        }

        // Worlds (solo list): Play the selected world (else the newest).
        public Task PlaySelected() => PlayAction.Enabled ? StartSolo() : Done;

        // Worlds (multiplayer list): host the selected world (else the newest, else a new one) online.
        public Task HostSelected()
        {
            if (!HostAction.Enabled) return Done;
            if (!PrepareHost()) return Done;
            return AskIfUnsaved(Attempt.Host) ? Done : BeginHost();
        }

        private Task BeginHost() => Begin(Attempt.Host, () => session.HostOnlineAsync(CancellationToken.None));

        private bool PrepareHost() => PrepareWorld(WorldKind.Multiplayer, IdentityKind.Ugs);

        // Picks the world a start opens: the selected one, else the newest readable one, else a new one. Audit A6: a selected world that is damaged is never
        // swapped for another one behind the player's back (nothing starts, the notice says why); a list of damaged worlds only counts as empty.
        // False when nothing can start (damaged pick, or no room for a new world).
        private bool PrepareWorld(WorldKind kind, IdentityKind identity)
        {
            IReadOnlyList<WorldSummary> list = worlds.ListWorlds(kind, identity);
            string selected = worlds.SelectedWorldId;
            WorldSummary picked = Find(list, selected);
            if (picked != null && picked.Damaged)
            {
                Notice = UiText.Toast("save.world_damaged");
                Touch();
                return false;
            }
            WorldSummary newest = null;
            foreach (WorldSummary w in list) if (!w.Damaged) { newest = w; break; }
            if (newest == null)
            {
                if (worlds.CreateWorld(kind, identity) == null)
                {
                    Notice = UiText.WorldLibraryFull(kind);
                    Touch();
                    return false;
                }
            }
            else if (picked == null)
            {
                worlds.Select(newest.Id);
            }
            return true;
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
                    if (PendingRemoveWorldId != null)
                    {
                        CancelRemovePlayer();
                        return Done;
                    }
                    state = FrontDoorState.Title;
                    Touch();
                    return Done;
                case FrontDoorState.JoinEntry:
                    state = FrontDoorState.Title;
                    PendingDeleteId = null;
                    ClearPendingRemove();
                    Touch();
                    return Done;
                case FrontDoorState.Connecting:
                    return Leave();
                case FrontDoorState.Ended:
                    Acknowledge();
                    return Done;
                case FrontDoorState.ConfirmUnsaved:
                    DeclinePlayAnyway();
                    return Done;
                case FrontDoorState.HostLobby:
                    CancelRename();
                    CancelSendAway();
                    return Done;
                default:   // Title, WaitingForHost
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
            if (!PrepareSolo()) return Done;
            return AskIfUnsaved(Attempt.Solo) ? Done : BeginSolo();
        }

        private Task BeginSolo() => Begin(Attempt.Solo, () => session.StartLocalAsync(false, CancellationToken.None));

        // Picks the world a solo start opens (the selected one, else the newest, else a new one). False when there is no room for a new one.
        private bool PrepareSolo() => PrepareWorld(WorldKind.Solo, IdentityKind.Local);

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
                case FrontDoorState.ConfirmUnsaved:
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
            ClearPendingRemove();
            pendingRename = null;
            pendingSendAway = null;
            RenameMessage = null;
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
                    Players = PlayerRows(w),
                    LastPlayed = ShortTime(w.LastPlayedUtc),
                    Selected = w.Id == selected && !w.Damaged,
                    Damaged = w.Damaged,
                });
            }
        }

        // The world's recorded players with their keys, so the host's remove action names one of them. A summary from an older build (or a
        // damaged section) may have fewer keys than names: those players are shown but cannot be removed.
        private static List<FrontDoorPlayerRow> PlayerRows(WorldSummary w)
        {
            var players = new List<FrontDoorPlayerRow>();
            if (w.PlayerKeys == null) return players;
            for (int i = 0; i < w.PlayerKeys.Count; i++)
            {
                string name = w.PlayerNames != null && i < w.PlayerNames.Count ? w.PlayerNames[i] : "?";
                players.Add(new FrontDoorPlayerRow { Name = name, Key = w.PlayerKeys[i] });
            }
            return players;
        }

        private static FrontDoorPlayerRow FindPlayer(FrontDoorWorldRow row, PlayerKey key)
        {
            if (row.Players == null) return null;
            foreach (FrontDoorPlayerRow p in row.Players) if (p.Key == key) return p;
            return null;
        }

        private FrontDoorWorldRow FindRow(string id)
        {
            foreach (FrontDoorWorldRow r in rows) if (r.Id == id) return r;
            return null;
        }

        private static WorldSummary Find(IReadOnlyList<WorldSummary> list, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (WorldSummary w in list) if (w.Id == id) return w;
            return null;
        }

        private static string ShortTime(string iso) => string.IsNullOrEmpty(iso) ? "" : (iso.Length >= 16 ? iso.Substring(0, 16).Replace('T', ' ') : iso);

        private void Touch() => Changed?.Invoke();
    }
}
