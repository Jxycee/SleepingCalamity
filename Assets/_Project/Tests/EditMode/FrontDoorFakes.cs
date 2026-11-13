// Test doubles for FrontDoorModelTests (PL-3 task 3.2). The fake session completes its start tasks at once unless a test holds them
// (HoldConnect), raises the same events in the same order the real sessions do (StateChanged, then Ended once per failed attempt), and
// logs every call so a test can say "the model asked for exactly this". No engine types.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.EditMode
{
    internal sealed class FakeNetworkSession : INetworkSession
    {
        public ConnState State { get; private set; } = ConnState.Offline;
        public SessionMode Mode { get; private set; } = SessionMode.None;
        public bool IsHost { get; private set; }
        public string JoinCode { get; private set; }
        public event Action<ConnState, ConnState> StateChanged;
        public event Action<EndReason> Ended;

        public readonly List<string> Calls = new List<string>();

        // When set, the next start task fails: Ended(reason) fires, then the task faults with NetworkSessionException(reason).
        public EndReason? FailNextStartWith;
        // When set, the next start task stays pending (state Connecting) until the test completes this source.
        public TaskCompletionSource<bool> HoldConnect;
        // The code HostOnlineAsync hands out.
        public string CodeToIssue = "ABC123";
        // Set to make a Solo session do what SceneFlow does when it connects: ask the host's start gate at once, and leave (Ended UserLeft) when it refuses.
        public IFrontDoorHost SoloHost;
        // LeaveAsync faults without going offline / completes without going offline (a session that could not stop).
        public bool LeaveFaults;
        public bool LeaveDoesNothing;

        public Task<string> HostOnlineAsync(CancellationToken ct)
        {
            Calls.Add("HostOnline");
            return Start(SessionMode.Online, true, CodeToIssue, () => CodeToIssue);
        }

        public Task JoinOnlineAsync(string code, CancellationToken ct)
        {
            Calls.Add("JoinOnline(" + code + ")");
            return Start<object>(SessionMode.Online, false, null, () => null);
        }

        public Task StartLocalAsync(bool allowRemote, CancellationToken ct)
        {
            Calls.Add("StartLocal(" + (allowRemote ? "true" : "false") + ")");
            return Start<object>(allowRemote ? SessionMode.DevDirect : SessionMode.Solo, true, null, () => null);
        }

        public Task JoinDirectAsync(string ip, ushort port, CancellationToken ct)
        {
            Calls.Add("JoinDirect");
            return Start<object>(SessionMode.DevDirect, false, null, () => null);
        }

        public Task SetJoinableAsync(bool open) => Task.CompletedTask;

        public Task LeaveAsync()
        {
            Calls.Add("Leave");
            if (LeaveFaults) return Task.FromException(new InvalidOperationException("leave failed"));
            if (LeaveDoesNothing) return Task.CompletedTask;
            if (State != ConnState.Offline) GoOffline(EndReason.UserLeft);
            return Task.CompletedTask;
        }

        // The session ends under the model (the host left, the network dropped).
        public void EndExternally(EndReason reason)
        {
            if (State != ConnState.Offline) GoOffline(reason);
        }

        public void Dispose() { }

        private Task<T> Start<T>(SessionMode mode, bool host, string code, Func<T> result)
        {
            Mode = mode;
            IsHost = host;
            JoinCode = null;
            SetState(ConnState.Connecting);

            if (FailNextStartWith.HasValue)
            {
                EndReason reason = FailNextStartWith.Value;
                FailNextStartWith = null;
                GoOffline(reason);
                return Task.FromException<T>(new NetworkSessionException(reason));
            }

            if (HoldConnect != null)
            {
                TaskCompletionSource<bool> hold = HoldConnect;
                HoldConnect = null;
                // Like the real sessions: a start that is released or faulted after the attempt was left ends in a fault (UserLeft), never in Connected.
                return hold.Task.ContinueWith(t =>
                {
                    if (t.IsFaulted) throw t.Exception.GetBaseException();
                    if (State != ConnState.Connecting) throw new NetworkSessionException(EndReason.UserLeft);
                    Connect(mode, code);
                    return result();
                }, TaskContinuationOptions.ExecuteSynchronously);
            }

            Connect(mode, code);
            return Task.FromResult(result());
        }

        private void Connect(SessionMode mode, string code)
        {
            JoinCode = code;
            SetState(ConnState.Connected);
            // SceneFlow: a Solo session starts by itself when it connects; a refused start leaves the session.
            if (mode == SessionMode.Solo && SoloHost != null && !SoloHost.TryStartSession()) GoOffline(EndReason.UserLeft);
        }

        private void GoOffline(EndReason reason)
        {
            JoinCode = null;
            Mode = SessionMode.None;
            IsHost = false;
            SetState(ConnState.Offline);
            Ended?.Invoke(reason);
        }

        private void SetState(ConnState next)
        {
            ConnState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }

    internal sealed class FakeWorlds : IFrontDoorWorlds
    {
        public readonly List<WorldSummary> All = new List<WorldSummary>();
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Deleted = new List<string>();
        public readonly List<string> RemovedPlayers = new List<string>();   // "<worldId>:<playerId>"
        public bool RemoveFails;
        public readonly List<WorldKind> Listed = new List<WorldKind>();
        public readonly List<IdentityKind> ListedIdentities = new List<IdentityKind>();
        private int nextId = 1;

        public string SelectedWorldId { get; private set; }

        public WorldSummary Add(WorldKind kind, IdentityKind identity, params string[] players)
        {
            var w = new WorldSummary { Id = "w" + nextId++, Kind = kind, Identity = (int)identity, LastPlayedUtc = "2026-11-12T10:30:00Z" };
            w.PlayerNames.AddRange(players);
            foreach (string name in players) w.PlayerKeys.Add(new PlayerKey(identity, "id-" + name));   // the same order as the names
            All.Insert(0, w);   // newest first, like WorldLibrary.List
            return w;
        }

        public void Select(string worldId) => SelectedWorldId = worldId;

        public IReadOnlyList<WorldSummary> ListWorlds(WorldKind kind, IdentityKind identity)
        {
            Listed.Add(kind);
            ListedIdentities.Add(identity);
            var list = new List<WorldSummary>();
            foreach (WorldSummary w in All) if (w.Matches(kind, identity)) list.Add(w);
            return list;
        }

        public string CreateWorld(WorldKind kind, IdentityKind identity)
        {
            int count = 0;
            foreach (WorldSummary w in All) if (w.Matches(kind, identity)) count++;
            if (count >= WorldSummary.MaxPerList) return null;
            WorldSummary created = Add(kind, identity, "?");
            Created.Add(created.Id);
            SelectedWorldId = created.Id;
            return created.Id;
        }

        public readonly HashSet<string> Unsavable = new HashSet<string>();   // ids of worlds that load read-only
        public bool CanSaveWorld(string worldId) => !Unsavable.Contains(worldId);

        public bool RemovePlayer(string worldId, PlayerKey key)
        {
            RemovedPlayers.Add(worldId + ":" + key.Id);
            if (RemoveFails) return false;
            foreach (WorldSummary w in All)
            {
                if (w.Id != worldId) continue;
                int at = w.PlayerKeys.IndexOf(key);
                if (at < 0) return false;
                w.PlayerKeys.RemoveAt(at);
                if (at < w.PlayerNames.Count) w.PlayerNames.RemoveAt(at);
                return true;
            }
            return false;
        }

        public bool DeleteWorld(string worldId)
        {
            Deleted.Add(worldId);
            int removed = All.RemoveAll(w => w.Id == worldId);
            if (SelectedWorldId == worldId) SelectedWorldId = null;
            return removed > 0;
        }
    }

    internal sealed class FakeHost : IFrontDoorHost
    {
        public int ConnectedCount { get; set; } = 1;
        public List<string> Names = new List<string> { "Host" };
        public int StartCalls;
        public WorldPlayerVerdict NextVerdict = WorldPlayerVerdict.Ok;
        public WorldPlayerVerdict LastStartVerdict { get; set; } = WorldPlayerVerdict.Ok;

        public void GetConnectedNames(List<string> into)
        {
            into.Clear();
            into.AddRange(Names);
        }

        public readonly List<FrontDoorPerson> Newcomers = new List<FrontDoorPerson>();
        public readonly List<FrontDoorPerson> RecordedAway = new List<FrontDoorPerson>();
        public readonly List<string> Renames = new List<string>();   // "<from>-><to>"
        public WorldPlayerVerdict RenameVerdict = WorldPlayerVerdict.Ok;

        public void GetRenameChoices(List<FrontDoorPerson> newcomers, List<FrontDoorPerson> recordedAway)
        {
            newcomers.Clear();
            newcomers.AddRange(Newcomers);
            recordedAway.Clear();
            recordedAway.AddRange(RecordedAway);
        }

        public WorldPlayerVerdict RenamePlayer(PlayerKey from, PlayerKey to)
        {
            Renames.Add(from.Id + "->" + to.Id);
            return RenameVerdict;
        }

        public bool TryStartSession()
        {
            StartCalls++;
            LastStartVerdict = NextVerdict;
            return NextVerdict == WorldPlayerVerdict.Ok;
        }
    }
}
