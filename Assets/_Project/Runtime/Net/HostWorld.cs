using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
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

        // ---- weapon growth (the world save's "weapons" section; M6 S0) ----
        // The host's copy of every recorded player's growth, by OwnerId (PlayerKey.Id). Live CombatHosts register as sources and write their
        // players' growth into it at capture time (the save flush) and when they go away, so a player who left this session keeps their growth.

        public interface IGrowthSource { void CaptureGrowth(WeaponsSave into); }

        private WeaponsSave _weapons = new WeaponsSave();
        private readonly List<IGrowthSource> _sources = new List<IGrowthSource>();

        // The section as last loaded or captured (never null). Read-only use: CombatHost restores a player's growth from it.
        public WeaponsSave Weapons => _weapons;

        // The saved section (cleaned) or null for an older save; does not raise Changed: it came from disk.
        // The section is always a separate copy (never the file's own object, or capture would compare the save with itself). Sources are cleared:
        // a new world means the old world's CombatHosts must not write into it.
        public void LoadWeapons(WeaponsSave saved)
        {
            _sources.Clear();
            _weapons = saved != null ? WeaponsSaveRules.Clean(saved, null, null) : new WeaponsSave();
        }

        // Only one live source at a time: a newer CombatHost (a scene change that overlaps the old one's teardown) takes over. The others'
        // growth is captured first, and the newcomer restores from the section, so no increment is lost and a stale host cannot overwrite it.
        public void RegisterGrowthSource(IGrowthSource source)
        {
            if (source == null || _sources.Contains(source)) return;
            foreach (IGrowthSource other in _sources) other.CaptureGrowth(_weapons);
            _sources.Clear();
            _sources.Add(source);
        }

        // The source goes away (its CombatHost is leaving): its players' growth is kept first.
        public void UnregisterGrowthSource(IGrowthSource source)
        {
            if (source == null || !_sources.Remove(source)) return;
            source.CaptureGrowth(_weapons);
        }

        // The restored growth of a recorded player (their saved weapons, abilities and pips), or null when the world has nothing for them.
        public PlayerGrowth RestoreGrowth(string playerId, PlayerKey key) => GrowthSaves.Restore(playerId, key.Id, _weapons);

        // Brings the live sources' growth into the section and returns it.
        public WeaponsSave CaptureWeapons()
        {
            foreach (IGrowthSource s in _sources) s.CaptureGrowth(_weapons);
            return _weapons;
        }

        // Captures and copies the section into `save` (the file's value) when it differs. An empty section is not written for a save that has none
        // (old saves stay as they were). True when the save changed (mark the file dirty).
        public bool CaptureWeaponsInto(WorldSave save)
        {
            if (save == null) return false;
            WeaponsSave copy = WeaponsSaveRules.Clean(CaptureWeapons(), null, null);   // a copy: the live section keeps changing
            bool empty = copy.Weapons.Count == 0 && copy.Heroes.Count == 0 && copy.Extra == null;
            if (empty && save.Weapons == null) return false;
            if (save.Weapons != null && JsonConvert.SerializeObject(copy) == JsonConvert.SerializeObject(save.Weapons)) return false;
            save.Weapons = copy;
            return true;
        }

        // ---- the town sections (stock, town, residents, dive; M6 S2) ----
        // The host's live town and haul (M6 S3/S4: the hub's TownState, DiveState and the resident state) implement ITownSource. CaptureTown returns the sections as they
        // stand now (TownSaves.Capture does the work from a TownState and DiveState); it must not hand out the live objects. At load the cleaned, conserved sections are
        // in Town (never null; empty sections for an older save): the provider builds its state from them with TownSaves.ToState / RestoreHaul /
        // DiveState.RestoreProgress, once, when it starts. A mid-dive quit has already been turned into a failed dive (InDive false).
        // RestoreTown is called by RegisterTownSource, before the source is live (so a save flush can never capture its still-empty state over the loaded town): it gets its
        // own cleaned copy of the loaded sections (sections an older save lacks are null) and builds TownState / DiveHaul / DiveState / resident state from it.
        public interface ITownSource { TownSnapshot CaptureTown(); void RestoreTown(TownSnapshot loaded); }

        private TownSnapshot _town = new TownSnapshot();
        private ITownSource _townSource;

        // The sections as last loaded or captured: separate copies, never the file's own objects. Sections an older save lacks are null.
        public TownSnapshot Town => _town;

        // The saved sections (cleaned and conserved again) of `saved`, or none for null. Does not raise Changed: it came from disk. The old source is dropped: a new
        // world means the old world's town must not write into it.
        public void LoadTown(WorldSave saved)
        {
            _townSource = null;
            _town = saved == null ? new TownSnapshot()
                : TownSaveRules.CleanSnapshot(new TownSnapshot { Stock = saved.Stock, Town = saved.Town, Residents = saved.Residents, Dive = saved.Dive }, null);
        }

        // One live source at a time (a newer hub takes over); the old one is captured first so nothing is lost.
        public void RegisterTownSource(ITownSource source)
        {
            if (source == null || source == _townSource) return;
            CaptureTownFromSource();
            source.RestoreTown(TownSaveRules.CleanSnapshot(_town, null, null, false));   // restore first, then go live
            _townSource = source;
        }

        // The source goes away (its scene is leaving): its town is kept first.
        public void UnregisterTownSource(ITownSource source)
        {
            if (source == null || source != _townSource) return;
            CaptureTownFromSource();
            _townSource = null;
        }

        private void CaptureTownFromSource()
        {
            if (_townSource == null) return;
            TownSnapshot live = _townSource.CaptureTown();
            if (live != null) _town = TownSaveRules.CleanSnapshot(live, null, null, false);
        }

        // Captures and copies each town section into `save` when it differs. A fresh-world (pristine) section is not written for a save that has none, so old saves stay as
        // they were. True when the save changed (mark the file dirty). Never aliases: the save gets its own cleaned copy.
        public bool CaptureTownInto(WorldSave save)
        {
            if (save == null) return false;
            CaptureTownFromSource();
            TownSnapshot copy = TownSaveRules.CleanSnapshot(_town, null, null, false);
            bool changed = false;
            changed |= SyncSection(ref save.Stock, copy.Stock, TownSaveRules.IsPristine(copy.Stock));
            changed |= SyncSection(ref save.Town, copy.Town, TownSaveRules.IsPristine(copy.Town));
            changed |= SyncSection(ref save.Residents, copy.Residents, TownSaveRules.IsPristine(copy.Residents));
            changed |= SyncSection(ref save.Dive, copy.Dive, TownSaveRules.IsPristine(copy.Dive));
            return changed;
        }

        private static bool SyncSection<T>(ref T file, T captured, bool pristine) where T : class
        {
            if (captured == null) return false;
            if (pristine && file == null) return false;
            if (file != null && JsonConvert.SerializeObject(captured) == JsonConvert.SerializeObject(file)) return false;
            file = captured;
            return true;
        }

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
                PlayerKey previousOwner = world.Recorded.Count > 0 ? world.Recorded[0] : default;
                if (world.Kind == WorldKind.Solo && !world.IsRecorded(key) && world.RebindSoloOwner(key))
                {
                    if (previousOwner.Id != null) GrowthSaves.Rename(_weapons, previousOwner.Id, key.Id);   // the Solo world's weapons follow its owner (A32)
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
