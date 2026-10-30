using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Runtime
{
    // Authority: host only (a plain C# object, never replicated; a client never evaluates the world rules for itself).
    // The host's world-player gate (user decision 2026-10-29, rules in Core/World/WorldPlayers):
    //  - Admit is called by ConnectionApprover for every join, the host's own included, after JoinRules approved it. It refuses a join to a
    //    Solo world (SoloWorldClosed), refuses a key of another identity kind and refuses a new key when 4 players are recorded (WorldFull).
    //    It records nothing: RecordEntered writes a key down once its client has actually connected (the approver's connection event).
    //  - TryStartSession is THE start gate. SceneFlow calls it before loading the first gameplay scene. A Multiplayer world is created at the
    //    first start and every start needs 2 or more of its recorded players connected; a Solo world starts with its owner alone.
    //    Until it returns Ok the host sits in the lobby: clients can connect but no gameplay scene loads. The 2-player rule is a START rule
    //    only (user ruling 2026-10-30): once a session runs, players dropping to 1 does not end it (no kick mid-dive); ScalePlayers falls to 1.
    // Before the world exists (Players == null) the session mode decides what it will be (PendingKind). The world save flow loads a saved
    // world into Players before the host connects and persists Players whenever Changed fires.
    public sealed class HostWorld
    {
        private readonly List<PlayerKey> _scratch = new List<PlayerKey>(NetworkConstants.MaxPlayers);
        private readonly Dictionary<ulong, PlayerKey> _pendingNew = new Dictionary<ulong, PlayerKey>(NetworkConstants.MaxPlayers);   // approved new players not yet recorded, by client id

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

        // Connection approval. JoinRules (and so the host's self-approval, IsServerSelf) ran before this. Nothing is recorded here.
        //  - The HOST is never refused (user ruling 2026-10-30). On a Solo world the owner follows the file: a host key that is not the
        //    recorded owner (a new profile, a copied save) is rebound as the owner and the change is logged. On a Multiplayer world the host is
        //    admitted whatever its key; if its key is not (and cannot be) recorded, the start gate says so with a clear message.
        //  - A joiner goes through WorldPlayers.CanJoin (a Solo world refuses everyone; a key of another identity kind is refused; a new key is
        //    refused when 4 are recorded). Other approved-but-not-yet-entered new players, kept by client id, count against the 4 places, so two
        //    joiners approved together cannot both take the last one.
        public WorldPlayerVerdict Admit(PlayerKey key, ulong clientId, bool isHost)
        {
            if (!WorldPlayers.IsValidKey(key)) return WorldPlayerVerdict.InvalidPlayer;
            WorldPlayers world = Players;
            if (world == null) return WorldPlayerVerdict.Ok;   // no world yet: the start gate decides who it records

            if (isHost)
            {
                if (world.Kind == WorldKind.Solo && !world.IsRecorded(key) && world.RebindSoloOwner(key))
                {
                    Debug.LogWarning("HostWorld: the Solo world's owner key did not match this machine's key; the owner is now " + key + ".");
                    RaiseChanged();
                }
                return WorldPlayerVerdict.Ok;
            }

            WorldPlayerVerdict verdict = world.CanJoin(key);
            if (verdict != WorldPlayerVerdict.Ok) return verdict;
            if (!world.IsRecorded(key))
            {
                int others = 0;
                foreach (KeyValuePair<ulong, PlayerKey> pending in _pendingNew)
                    if (pending.Key != clientId && pending.Value != key) others++;
                if (world.Recorded.Count + others >= WorldPlayers.MaxRecorded) return WorldPlayerVerdict.WorldFull;
                _pendingNew[clientId] = key;
            }
            return WorldPlayerVerdict.Ok;
        }

        // The client has entered the session (its connection is up): the key is written into the world now, with its display name.
        // No-op before the world exists. A host whose key cannot be recorded (a full or other-identity Multiplayer world) stays unrecorded;
        // TryStartSession then refuses with NotRecorded / WrongIdentityKind.
        public void RecordEntered(PlayerKey key, ulong clientId, string name = null)
        {
            _pendingNew.Remove(clientId);
            WorldPlayers world = Players;
            if (world == null) return;
            bool changed;
            if (world.Kind == WorldKind.Solo) changed = world.ClaimSoloOwner(key);
            else changed = !world.IsRecorded(key) && world.Record(key) == WorldPlayerVerdict.Ok;
            if (name != null && world.SetName(key, name)) changed = true;
            if (changed) RaiseChanged();
        }

        // An approved client that never entered (it dropped first): its place is free again. Keyed by client id, so it works whoever
        // else has already forgotten the approval.
        public void CancelPending(ulong clientId) => _pendingNew.Remove(clientId);

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
                case WorldPlayerVerdict.WrongIdentityKind: return EndReason.WrongWorld;
                default: return EndReason.Unknown;
            }
        }

        private void RaiseChanged() => EventIsolation.Invoke(Changed);
    }
}
