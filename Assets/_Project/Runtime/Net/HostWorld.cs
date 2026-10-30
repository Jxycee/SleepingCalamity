using System;
using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.Runtime
{
    // Authority: host only (a plain C# object, never replicated; a client never evaluates the world rules for itself).
    // The host's world-player gate (user decision 2026-10-29, rules in Core/World/WorldPlayers):
    //  - Admit is called by ConnectionApprover for every join, the host's own included, after JoinRules approved it. It refuses a join to a
    //    Solo world (SoloWorldClosed), refuses a key of another identity kind and refuses a new key when 4 players are recorded (WorldFull).
    //    It records nothing: RecordEntered writes a key down once its client has actually connected (the approver's connection event).
    //  - TryStartSession is THE start gate. SceneFlow calls it before loading the first gameplay scene. A Multiplayer world is created at the
    //    first start and every start needs 2 or more of its recorded players connected; a Solo world starts with its owner alone.
    //    Until it returns Ok the host sits in the lobby (the Bootstrap scene): clients can connect but no gameplay scene loads.
    // Before the world exists (Players == null) the session mode decides what it will be (PendingKind). The world save flow loads a saved
    // world into Players before the host connects and persists Players whenever Changed fires.
    public sealed class HostWorld
    {
        private readonly List<PlayerKey> _scratch = new List<PlayerKey>(NetworkConstants.MaxPlayers);
        private readonly List<PlayerKey> _pendingNew = new List<PlayerKey>(NetworkConstants.MaxPlayers);   // approved new players not yet recorded

        // The recorded players of this machine's world; null until the world exists (first start, or loaded from the save).
        public WorldPlayers Players { get; private set; }

        // What a world created now will be. Solo sessions create a Solo world, every other session a Multiplayer one.
        public WorldKind PendingKind { get; set; } = WorldKind.Multiplayer;

        // True once a session was started on this world (the gate returned Ok since the last Reset).
        public bool Started { get; private set; }

        // Raised when Players is created, replaced or recorded a new player (persist it). Main thread.
        public event Action Changed;

        // Loads a world (from the save), or null for "no world yet". Does not raise Changed: it came from disk.
        public void Load(WorldPlayers players)
        {
            Players = players;
            Started = false;
            _pendingNew.Clear();
        }

        // A new session starts: the world stays, the started flag and a pending kind reset.
        public void Reset(WorldKind pendingKind)
        {
            PendingKind = pendingKind;
            Started = false;
            _pendingNew.Clear();
        }

        // Connection approval. Pure: nothing is recorded here. JoinRules (and so the host's self-approval, IsServerSelf) ran before this.
        // A joiner goes through WorldPlayers.CanJoin (a Solo world refuses everyone; a key of another identity kind is refused; a new key is
        // refused when 4 are recorded) and also counts the other approved-but-not-yet-entered new players against the 4 places, so two joiners
        // approved together cannot both take the last one. The host is the world's owner or creator, never a joiner: on a Solo world it must be
        // the owner (or the world has no owner record left), on a Multiplayer world its key must fit (identity, and a place if new).
        public WorldPlayerVerdict Admit(PlayerKey key, bool isHost)
        {
            if (!WorldPlayers.IsValidKey(key)) return WorldPlayerVerdict.InvalidPlayer;
            WorldPlayers world = Players;
            if (world == null) return WorldPlayerVerdict.Ok;   // no world yet: the start gate decides who it records

            WorldPlayerVerdict verdict;
            if (world.Kind == WorldKind.Solo)
            {
                bool owner = world.IsRecorded(key) || world.Recorded.Count == 0;
                verdict = isHost && owner ? WorldPlayerVerdict.Ok : WorldPlayerVerdict.SoloWorldClosed;
                if (verdict == WorldPlayerVerdict.Ok && world.Identity.HasValue && key.Kind != world.Identity.Value) verdict = WorldPlayerVerdict.WrongIdentityKind;
                return verdict;
            }

            verdict = world.CanJoin(key);
            if (verdict != WorldPlayerVerdict.Ok) return verdict;
            if (!world.IsRecorded(key))
            {
                int others = 0;
                foreach (PlayerKey pending in _pendingNew) if (pending != key) others++;
                if (world.Recorded.Count + others >= WorldPlayers.MaxRecorded) return WorldPlayerVerdict.WorldFull;
                if (!_pendingNew.Contains(key)) _pendingNew.Add(key);
            }
            return WorldPlayerVerdict.Ok;
        }

        // The client has entered the session (its connection is up): the key is written into the world now. No-op before the world exists.
        public void RecordEntered(PlayerKey key)
        {
            _pendingNew.Remove(key);
            WorldPlayers world = Players;
            if (world == null) return;
            bool known = world.IsRecorded(key);
            bool added;
            if (world.Kind == WorldKind.Solo) added = world.ClaimSoloOwner(key);
            else added = !known && world.Record(key) == WorldPlayerVerdict.Ok;
            if (added) RaiseChanged();
        }

        // An approved client that never entered (it dropped first): its place is free again.
        public void CancelPending(PlayerKey key) => _pendingNew.Remove(key);

        // The start gate. connected: the players connected now (HostRoster.GetConnectedKeys). Creates the world on the first start.
        public WorldPlayerVerdict TryStartSession(IReadOnlyList<PlayerKey> connected)
        {
            WorldPlayers world = Players;
            if (world == null)
            {
                WorldPlayerVerdict created = WorldPlayers.TryCreate(PendingKind, connected, out world);
                if (created != WorldPlayerVerdict.Ok) return created;
                Players = world;
                RaiseChanged();
            }
            else
            {
                WorldPlayerVerdict can = world.CanStart(connected);
                if (can != WorldPlayerVerdict.Ok) return can;
            }
            Started = true;
            return WorldPlayerVerdict.Ok;
        }

        // Convenience for the host flow: the gate against a roster.
        public WorldPlayerVerdict TryStartSession(HostRoster roster)
        {
            _scratch.Clear();
            roster.GetConnectedKeys(_scratch);
            return TryStartSession(_scratch);
        }

        // The difficulty scale input for the players connected now (clamped to 1..4).
        public int ScalePlayers(HostRoster roster)
        {
            if (Players == null) return 1;
            _scratch.Clear();
            roster.GetConnectedKeys(_scratch);
            return Players.ScalePlayers(_scratch);
        }

        // The EndReason a refused join is reported to the client with (DisconnectClassifier reads it from the approval Reason string).
        public static EndReason ReasonFor(WorldPlayerVerdict verdict)
        {
            switch (verdict)
            {
                case WorldPlayerVerdict.SoloWorldClosed: return EndReason.JoinsClosed;
                case WorldPlayerVerdict.WorldFull: return EndReason.FullLobby;
                case WorldPlayerVerdict.WrongIdentityKind: return EndReason.Kicked;
                default: return EndReason.Unknown;
            }
        }

        private void RaiseChanged() => EventIsolation.Invoke(Changed);
    }
}
