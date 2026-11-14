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
    //    Solo world (SoloWorldClosed), refuses a key of another identity kind and refuses a new key when 4 players are recorded (WorldFull) once the session has started.
    //    Before the start an unrecorded player may wait in a full world's lobby (user decision 2026-11-13): the host renames them as a friend
    //    (RenameRecorded) or sends them away, and TryStartSession refuses (WorldFull) while they are there.
    //    It records nothing, and neither does a connection coming up (NoteConnected only keeps the display name): a player is written into the
    //    world when they PLAY it (user decision 2026-11-13, audit A2) - at the session start for everyone present, or, for a join into a session
    //    that is already running, when their hero spawns (RecordHeroSpawned). A lobby visitor who leaves before Start is never recorded.
    //  - TryStartSession is THE start gate. SceneFlow calls it before loading the first gameplay scene. A Multiplayer world is created at the
    //    first start and every start needs 2 or more players connected (recorded or recordable: the gate records them); a Solo world starts with
    //    its owner alone.
    //    Until it returns Ok the host sits in the lobby: clients can connect but no gameplay scene loads. The 2-player rule is a START rule
    //    only (user ruling 2026-10-30): once a session runs, players dropping to 1 does not end it (no kick mid-dive); ScalePlayers falls to 1.
    // Before the world exists (Players == null) the session mode decides what it will be (PendingKind). The world save flow loads a saved
    // world into Players before the host connects and persists Players whenever Changed fires.
    public sealed class HostWorld
    {
        private readonly List<PlayerKey> _scratch = new List<PlayerKey>(NetworkConstants.MaxPlayers);
        private readonly Dictionary<ulong, PlayerKey> _pendingNew = new Dictionary<ulong, PlayerKey>(NetworkConstants.MaxPlayers);   // approved new players not yet recorded, by client id
        private readonly List<ulong> _pendingDone = new List<ulong>(NetworkConstants.MaxPlayers);                                   // scratch for ForgetPendingOf
        private readonly Dictionary<PlayerKey, string> _namesWhileConnected = new Dictionary<PlayerKey, string>(NetworkConstants.MaxPlayers);   // display names of connected players, kept until they are recorded

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

        // ---- the town sections (stock, town, residents, dive; M6 S2) and the bank-room suspend (plan 3 task 6) ----
        // The host's live town and dive (M6 S3/S4: the hub's TownState, DiveSession and the resident state) implement ITownSource. CaptureTown returns the sections as they
        // stand now (TownStock.CaptureWorld does the work from the live DiveSession); it must not hand out the live objects. A section the source returns as null (or
        // residents it passes as null to Capture) means "I do not know": the host keeps its previous copy of it, and a first-meet beat never goes back to false.
        // RestoreTown is called by RegisterTownSource, before the source is live (so a save flush can never capture its still-empty state over the loaded town): it gets its
        // own cleaned, conserved copy of the loaded sections and builds TownState / bags / checkpoint / DivePieces / resident state from it, once, with TownSaves.ToState /
        // TownStock.RestoreWorld. Sections an older save lacks are null in that copy (the Town property itself is a snapshot whose sections may be null).
        // A mid-dive quit has already been turned into a failed dive (InDive false), unless the snapshot carries a valid suspend section (a bank-room save: Town.Suspend). A source that knows
        // whether a suspend exists sets TownSnapshot.SuspendKnown (Suspend null then means none: the saved one was used and is cleared); one that says nothing keeps the saved section.
        public interface ITownSource { TownSnapshot CaptureTown(); void RestoreTown(TownSnapshot loaded); }

        private TownSnapshot _town = new TownSnapshot();
        private ITownSource _townSource;

        // The sections as last loaded or captured: separate copies, never the file's own objects. Sections an older save lacks are null.
        public TownSnapshot Town => _town;

        // The floor of every piece and person (plan 3 decision 10), decided once per world: the saved depths where the dive section has them, else the world seed's assignment. Resolved by
        // LoadTown and written back into the dive section at every capture, so a later save keeps them and nothing is recomputed. Empty before a world is loaded.
        public IReadOnlyDictionary<int, int> PieceFloors { get; private set; } = new Dictionary<int, int>();

        // The saved sections (cleaned and conserved again) of `saved`, or none for null. Does not raise Changed: it came from disk. The old source is dropped: a new
        // world means the old world's town must not write into it.
        public void LoadTown(WorldSave saved)
        {
            _townSource = null;
            _town = saved == null ? new TownSnapshot()
                : TownSaveRules.CleanSnapshot(new TownSnapshot { Stock = saved.Stock, Town = saved.Town, Residents = saved.Residents, Dive = saved.Dive, Bags = saved.Bags, Suspend = saved.Suspend }, null);
            TownSaves.MigrateLegacyLoot(_town, saved?.Players?.Recorded);   // an old save's town and banked loot become the first recorded player's stash (the next capture no longer writes them)
            PieceFloors = saved != null && WorldSaveRules.TryParseSeed(saved.WorldSeed, out ulong seed) ? TownSaveRules.ResolvePieceDepths(_town.Dive, seed) : new Dictionary<int, int>();
            StampPieceDepths();
        }

        // Writes PieceFloors into the host's dive section so the next save keeps them. Only when a dive section exists: creating one here would make Reconcile read every piece as lost
        // (the dive section is the authority on what is still on the floors), and a world with no dive section (never stamped, untouched) gets its depths re-derived from the seed at each load. That re-derivation is stable only while PieceDepths.Assign is unchanged: D1 reshuffled the non-frame pieces of such worlds (frame on floor 2).
        private void StampPieceDepths()
        {
            if (_town.Dive == null || PieceFloors.Count == 0) return;
            var list = new List<PieceDepthSave>();
            foreach (KeyValuePair<int, int> kv in PieceFloors) list.Add(new PieceDepthSave { Id = kv.Key, Floor = kv.Value });
            _town.Dive.PieceDepths = list;
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
            if (live != null) live.Bags = RecordedBagsOnly(live.Bags);   // only the world's recorded players' bags reach the file (a client-id fallback or unrecorded player's never do)
            if (live != null) RecordedSuspendOnly(live);                 // and the same for the suspend section's bag records
            if (live != null)
            {
                _town = TownSaveRules.CleanSnapshot(TownSaveRules.MergeLive(live, _town), null, null, false);   // what the source did not say (null) keeps the previous copy
                StampPieceDepths();
            }
        }

        // The suspend section with the recorded players' records only. Without a world yet the source's word cannot be checked: it is treated as saying nothing (the saved section stays).
        private void RecordedSuspendOnly(TownSnapshot live)
        {
            if (live.Suspend == null) return;
            if (Players == null) { live.Suspend = null; live.SuspendKnown = false; return; }
            live.Suspend = SuspendSaveRules.OnlyRecorded(live.Suspend, key => Players.IsRecorded(key));
            // The suspend records of players the host let go stay in the file too (same promise as their bags).
            if (live.Suspend != null && _town.Suspend?.Bags != null)
                foreach (SuspendedBag b in _town.Suspend.Bags)
                    if (b != null && !Players.IsRecorded(new PlayerKey((IdentityKind)b.Identity, b.Id)) && !live.Suspend.Bags.Exists(k => k.Identity == b.Identity && k.Id == b.Id)) live.Suspend.Bags.Add(b);
        }

        // The bags of the recorded players only; null (the previous copy is kept) when the source said nothing or the world has no players yet.
        private BagsSave RecordedBagsOnly(BagsSave bags)
        {
            if (bags == null || Players == null) return null;
            var kept = new BagsSave { Extra = bags.Extra };
            foreach (PlayerBagSave p in bags.Players)
                if (p != null && Players.IsRecorded(new PlayerKey((IdentityKind)p.Identity, p.Id))) kept.Players.Add(p);
            // A player the host let go (audit A2) keeps what the file already holds of theirs: "what they grew here waits in the file".
            if (_town.Bags?.Players != null)
                foreach (PlayerBagSave p in _town.Bags.Players)
                    if (p != null && !Players.IsRecorded(new PlayerKey((IdentityKind)p.Identity, p.Id)) && !kept.Players.Exists(k => k.Identity == p.Identity && k.Id == p.Id)) kept.Players.Add(p);
            return kept;
        }

        // Captures and copies each town section into `save` when it differs. A fresh-world (pristine) section is not written for a save that has none, so old saves stay as
        // they were. True when the save changed (mark the file dirty). Never aliases: the save gets its own cleaned copy.
        public bool CaptureTownInto(WorldSave save)
        {
            if (save == null) return false;
            CaptureTownFromSource();
            TownSnapshot copy = TownSaveRules.CleanSnapshot(_town, null, null, false);
            copy.Residents = TownSaveRules.MergeLatches(copy.Residents, save.Residents);   // the file's first-meet beats never roll back either
            bool changed = false;
            changed |= SyncSection(ref save.Stock, copy.Stock, TownSaveRules.IsPristine(copy.Stock));
            changed |= SyncSection(ref save.Town, copy.Town, TownSaveRules.IsPristine(copy.Town));
            changed |= SyncSection(ref save.Residents, copy.Residents, TownSaveRules.IsPristine(copy.Residents));
            changed |= SyncSection(ref save.Dive, copy.Dive, TownSaveRules.IsPristine(copy.Dive));
            changed |= SyncSection(ref save.Bags, copy.Bags, BagsSaveRules.IsPristine(copy.Bags));   // a world with no bags writes no "bags" key
            changed |= SyncOptional(ref save.Suspend, copy.Suspend);                                  // null means none: a used suspend is cleared from the file, and none writes no "suspend" key
            return changed;
        }

        private static bool SyncOptional<T>(ref T file, T captured) where T : class
        {
            if (captured == null && file == null) return false;
            if (captured != null && file != null && JsonConvert.SerializeObject(captured) == JsonConvert.SerializeObject(file)) return false;
            file = captured;
            return true;
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
            _namesWhileConnected.Clear();
        }

        // A new session starts: the world stays, the started flag and a pending kind reset.
        public void Reset(WorldKind pendingKind)
        {
            PendingKind = pendingKind;
            Started = false;
            _pendingNew.Clear();
            _namesWhileConnected.Clear();
        }

        // A player's saved data follows their key: weapons and hero growth (A32), gold, stash and waiting satchel, and a bank-room suspend's record of
        // them (its bag, present id and the dive's extra-slot owner). Shared by the Solo owner rebind and the Multiplayer rename (audit A3).
        private void MovePlayerData(PlayerKey from, PlayerKey to)
        {
            GrowthSaves.Rename(_weapons, from.Id, to.Id);
            BagsSaveRules.Rename(_town.Bags, from, to);
            SuspendSaveRules.Rename(_town.Suspend, from, to);
            if (_town.Dive != null) _town.Dive.ExtraSlotOwner = SuspendSaveRules.RenameId(_town.Dive.ExtraSlotOwner, from, to);
        }

        // The host's "this player is now that player" (audit A3; the front door's host lobby): one recorded player of a Multiplayer world takes a
        // new key (a new PC or a reinstall gave them a new UGS id) and brings their growth, bags and suspend record along. Refused with the
        // verdict, changing nothing, for what WorldPlayers.RenameRecorded refuses (invalid or other-kind key, an old key that is not recorded, a
        // new key that already is, a Solo world) and with WorldInProgress once a session runs (the live heroes then hold that state). Raises Changed.
        public WorldPlayerVerdict RenameRecorded(PlayerKey from, PlayerKey to)
        {
            if (Players == null) return WorldPlayerVerdict.NotRecorded;
            if (Started || _townSource != null || _sources.Count > 0) return WorldPlayerVerdict.WorldInProgress;   // a live source would write the old key back at its next capture
            WorldPlayerVerdict verdict = Players.RenameRecorded(from, to);
            if (verdict != WorldPlayerVerdict.Ok) return verdict;
            MovePlayerData(from, to);
            ForgetPendingOf(Players);   // the new key is recorded now: its approval must not count a second time
            RaiseChanged();
            return WorldPlayerVerdict.Ok;
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
                    if (previousOwner.Id != null) MovePlayerData(previousOwner, key);
                    Debug.LogWarning("HostWorld: the Solo world's owner key did not match this machine's key; the owner is now " + key + ".");
                    RaiseChanged();
                }
                return WorldPlayerVerdict.Ok;
            }

            WorldPlayerVerdict verdict = world.CanJoin(key);
            // A stranger may sit in the lobby of a full world until the session starts (audit A3): the host can then say who they really are
            // (RenameRecorded) or send them away. The start gate still refuses with the world-full words, and JoinRules caps the connections.
            if (verdict == WorldPlayerVerdict.WorldFull && !Started) return WorldPlayerVerdict.Ok;
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

        // A client's connection is up (ConnectionApprover). NOTHING is written into the world here (user decision 2026-11-13, audit A2): a
        // player is recorded when they PLAY the world - at the session start (TryStartSession) or, for a join into a running session, when
        // their hero spawns (RecordHeroSpawned). So a friend who looks at the lobby and leaves takes none of the world's four places. Their
        // display name is kept until the moment they are recorded; their approved place stays taken while they are connected (CancelPending
        // frees it). A player who is already recorded has their name refreshed now, as before.
        public void NoteConnected(PlayerKey key, ulong clientId, string name = null)
        {
            if (!WorldPlayers.IsValidKey(key)) return;
            if (!string.IsNullOrWhiteSpace(name)) _namesWhileConnected[key] = name;
            WorldPlayers world = Players;
            if (world != null && name != null && world.IsRecorded(key) && world.SetName(key, name)) RaiseChanged();
        }

        // The player is playing the world: the key is written into it now, with the display name their connection brought (name null = the kept
        // one). No-op before the world exists. A host whose key cannot be recorded (a full or other-identity Multiplayer world) stays unrecorded;
        // TryStartSession then refuses with NotRecorded / WrongIdentityKind.
        public void RecordEntered(PlayerKey key, ulong clientId, string name = null)
        {
            _pendingNew.Remove(clientId);
            WorldPlayers world = Players;
            if (world == null) return;
            bool changed;
            if (world.Kind == WorldKind.Solo) changed = world.ClaimSoloOwner(key);
            else changed = !world.IsRecorded(key) && world.Record(key) == WorldPlayerVerdict.Ok;
            string display = name ?? (_namesWhileConnected.TryGetValue(key, out string kept) ? kept : null);
            if (display != null && world.SetName(key, display)) changed = true;
            if (changed) RaiseChanged();
        }

        // A hero spawned for a client in a session that is already running (ArenaPlayerSpawner): that is the moment a player who joined after
        // the start counts as having played, so the world records them now (user decision 2026-11-13, audit A2). Before the start gate has run
        // nothing is recorded: the gate records the whole group that is present.
        public void RecordHeroSpawned(PlayerKey key, ulong clientId)
        {
            if (!Started) return;
            RecordEntered(key, clientId);
        }

        // An approved client that never entered (it dropped first): its place is free again. Keyed by client id, so it works whoever
        // else has already forgotten the approval.
        public void CancelPending(ulong clientId) => _pendingNew.Remove(clientId);

        // The start gate. connected: the players connected now (HostRoster.GetConnectedKeys). Creates the world on the first start, and - since
        // 2026-11-13 (user decision, audit A2) - RECORDS the players present here, because starting a session is what makes someone one of the
        // world's players. CanStartWithNewPlayers checks the group with the newcomers counted as recordable, so nothing is written for a start
        // that is refused (a refused start must not spend one of the four places).
        public WorldPlayerVerdict TryStartSession(IReadOnlyList<PlayerKey> connected)
        {
            WorldPlayers world = Players;
            if (world == null)
            {
                WorldPlayerVerdict created = WorldPlayers.TryCreate(PendingKind, connected, out world);
                if (created != WorldPlayerVerdict.Ok) return created;
                Players = world;
                ApplyKeptNames(world, connected);
                ForgetPendingOf(world);
                RaiseChanged();
            }
            else
            {
                WorldPlayerVerdict can = world.CanStartWithNewPlayers(connected);
                if (can != WorldPlayerVerdict.Ok) return can;
                RecordPresent(world, connected);
            }
            Started = true;
            return WorldPlayerVerdict.Ok;
        }

        // Writes every player present into the world (the gate said they all fit), with the name their connection brought.
        private void RecordPresent(WorldPlayers world, IReadOnlyList<PlayerKey> present)
        {
            bool changed = false;
            for (int i = 0; i < present.Count; i++)
            {
                PlayerKey key = present[i];
                if (world.Kind == WorldKind.Solo) changed |= world.ClaimSoloOwner(key);
                else if (!world.IsRecorded(key) && world.Record(key) == WorldPlayerVerdict.Ok) changed = true;
            }
            changed |= ApplyKeptNames(world, present);
            ForgetPendingOf(world);
            if (changed) RaiseChanged();
        }

        // The display names the connections brought, now that the keys are recorded (SetName ignores a key that is not).
        private bool ApplyKeptNames(WorldPlayers world, IReadOnlyList<PlayerKey> present)
        {
            bool changed = false;
            for (int i = 0; i < present.Count; i++)
                if (_namesWhileConnected.TryGetValue(present[i], out string name) && world.SetName(present[i], name)) changed = true;
            return changed;
        }

        // A recorded player holds a place by their record, not by their approval: a pending entry left for them would count twice in Admit.
        private void ForgetPendingOf(WorldPlayers world)
        {
            if (_pendingNew.Count == 0) return;
            _pendingDone.Clear();
            foreach (KeyValuePair<ulong, PlayerKey> pending in _pendingNew)
                if (world.IsRecorded(pending.Value)) _pendingDone.Add(pending.Key);
            for (int i = 0; i < _pendingDone.Count; i++) _pendingNew.Remove(_pendingDone[i]);
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

        // M6 S4: asks the save flow to write the world now (TownSync calls it when the builder leaves placement mode, so a finished build is on disk even if the host then crashes).
        // Its own event, not Changed (Changed means "the players changed"). WorldSaveFlow answers with SaveNow(), which captures the town sections from the live ITownSource.
        public event Action SaveRequested;
        public void RequestSave() => EventIsolation.Invoke(SaveRequested);

        private void RaiseChanged() => EventIsolation.Invoke(Changed);
    }
}
