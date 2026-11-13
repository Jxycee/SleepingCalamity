// App/WorldSaveFlow.cs: the host's world save flow. Not networked itself.
// Authority: host only. A player keeps up to 10 Solo and 10 Multiplayer worlds (WorldLibrary: one file per world). Before connecting, the host
// picks a world (Select) or makes one (CreateWorld); when it starts a session as the host (state Connecting) this flow opens that world and loads
// its players into HostWorld before the host's own approval runs (the approver reads them). With nothing selected it opens the most recently
// played world of the session's kind and identity kind, or makes one. Local worlds (solo, dev-direct) and online worlds never cross.
// While hosting it saves:
//  - at once when the world is created or a player is recorded (HostWorld.Changed), so a crash right after never loses who the world belongs to,
//  - weapon growth (the "weapons" section) is captured from the live CombatHosts at every save below and marks the file dirty only when it changed,
//  - on the first session start (last-played time) and then at most every FlushSeconds when anything else marks the file dirty,
//  - when the session ends: on ShuttingDown or Failed (before NGO shuts down), again on Ended, and on quit.
// A client never opens a world file. A newer build's world loads read-only (a toast says so). One-time migration: the old single
// world{suffix}.json becomes a world in the library.
using System;
using System.Collections.Generic;
using System.Globalization;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.App
{
    public sealed class WorldSaveFlow : IDisposable, IFrontDoorWorlds
    {
        public const float FlushSeconds = 10f;       // a dirty file is on disk within this

        private readonly INetworkSession session;
        private readonly NetworkManager networkManager;
        private readonly EventBus bus;
        private readonly HostWorld world;
        private WorldLibrary library;
        private WorldSaveStore store;
        private float nextFlush;
        private bool disposed;
        private bool migrated;

        // PlayMode tests switch it off, so one test's world never seeds the next one's session (and none touches the profile's real worlds)
        // unless it switches this on (with WorldLibrary.DirectoryOverrideForTests). Reset to on for every play session.
        public static bool Enabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Enabled = true;

        public WorldSaveFlow(INetworkSession session, NetworkManager networkManager, EventBus bus, HostWorld world)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            session.StateChanged += HandleStateChanged;
            session.Ended += HandleEnded;
            world.Changed += HandleWorldChanged;
            world.SaveRequested += HandleSaveRequested;
        }

        // The town builder left placement mode (TownSync): the finished build goes to disk now.
        private void HandleSaveRequested() { if (!disposed) SaveNow(); }

        // The attached store while hosting (tests read it), else null.
        public WorldSaveStore Store => store;

        // The world the next hosted session opens (set by Select / CreateWorld); null = the most recently played, or a new one.
        public string SelectedWorldId { get; private set; }

        // The library of this machine's worlds (migrates the old single file once, on first use).
        public WorldLibrary Library
        {
            get
            {
                if (library == null)
                {
                    library = WorldLibrary.ForThisInstance();
                    if (Enabled && !migrated)
                    {
                        migrated = true;
                        library.MigrateLegacy(WorldSaveStore.PathForThisInstance());
                    }
                }
                return library;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            world.Changed -= HandleWorldChanged;
            world.SaveRequested -= HandleSaveRequested;
            session.StateChanged -= HandleStateChanged;
            session.Ended -= HandleEnded;
        }

        // ---- picking and managing worlds (used before a session starts) ----

        public void Select(string worldId) => SelectedWorldId = worldId;

        public IReadOnlyList<WorldSummary> ListWorlds(WorldKind kind, IdentityKind identity) => Library.List(kind, identity);

        // A new empty world of the kind and identity kind, selected for the next session. Null (and nothing changes) when that list is full.
        public string CreateWorld(WorldKind kind, IdentityKind identity)
        {
            WorldSaveStore created = Library.Create(kind, identity, NewSeed());
            if (created == null) return null;
            SelectedWorldId = created.Current.WorldId;
            return SelectedWorldId;
        }

        // Lets one of a multiplayer world's recorded players go, so one of its four places is free again (the host's own action in the world
        // picker; audit A2, user decision 2026-11-13). Refused while that world is the one being hosted (its players are live in HostWorld), for
        // a world that is not there or loads read-only, for a Solo world and for a key that is not recorded (WorldPlayers.Remove).
        // The player's own weapons and bags stay in the file: nothing reads them while they are not recorded, and adding them back finds them.
        public bool RemovePlayer(string worldId, PlayerKey key)
        {
            if (store != null && store.Current.WorldId == worldId) return false;
            WorldSaveStore target = Library.Open(worldId);
            if (target == null || target.IsReadOnly) return false;
            if (!WorldPlayers.TryRestore(target.Current.Players, out WorldPlayers players, out _)) return false;
            if (!players.Remove(key)) return false;
            target.Current.Players = players.ToSave();
            target.MarkDirty();
            return target.Save();
        }

        // Sets a world aside (never erases it). Not while it is the world being hosted.
        public bool DeleteWorld(string worldId)
        {
            if (store != null && store.Current.WorldId == worldId) return false;
            if (!Library.Delete(worldId)) return false;
            if (SelectedWorldId == worldId) SelectedWorldId = null;
            return true;
        }

        // ---- hosting ----

        private void HandleStateChanged(ConnState previous, ConnState current)
        {
            if (disposed) return;
            // Leave (ShuttingDown) and UnexpectedStop (Failed) fire before NGO shuts down: the last save happens here.
            if (current == ConnState.ShuttingDown || current == ConnState.Failed) { SaveNow(); return; }
            if (!Enabled || current != ConnState.Connecting || !session.IsHost) return;
            Open();
        }

        private void Open()
        {
            WorldKind kind = session.Mode == SessionMode.Solo ? WorldKind.Solo : WorldKind.Multiplayer;
            IdentityKind identity = session.Mode == SessionMode.Online ? IdentityKind.Ugs : IdentityKind.Local;
            store = Resolve(kind, identity);
            if (store == null)
            {
                bus.Publish(new ToastRequested("world.library_full", kind.ToString(), 6f));   // 10 worlds of this kind: pick one or delete one
                world.LoadWeapons(null);   // no stale section from an earlier world
                world.LoadTown(null);
                return;
            }
            SelectedWorldId = store.Current.WorldId;
            world.Load(RestoreWorld(store.Current.Players));
            world.LoadWeapons(store.Current.Weapons);   // each returning player's growth is seeded from it when the CombatHost first needs them
            world.LoadTown(store.Current);   // the town, stock, residents and dive sections for the hub's ITownSource to restore from
            Write();   // stamps the last-played time at once
            nextFlush = Time.unscaledTime + FlushSeconds;
            if (store.IsReadOnly)
                bus.Publish(new ToastRequested(store.NewerVersionOnDisk > 0 ? "save.world_read_only" : "save.world_load_failed", null, 6f));
        }

        // The selected world if it is one of this kind and identity kind; else the newest of them; else a new one (null when the list is full).
        private WorldSaveStore Resolve(WorldKind kind, IdentityKind identity)
        {
            List<WorldSummary> worlds = Library.List(kind, identity);
            string id = null;
            if (!string.IsNullOrEmpty(SelectedWorldId))
                foreach (WorldSummary w in worlds) if (w.Id == SelectedWorldId) id = w.Id;
            if (id == null && worlds.Count > 0) id = worlds[0].Id;   // most recently played first
            if (id != null) return Library.Open(id, NewSeed());
            return Library.Create(kind, identity, NewSeed());
        }

        // The saved players section as a world, or null when the file has none.
        private static WorldPlayers RestoreWorld(WorldPlayersSave saved)
        {
            if (saved == null) return null;
            return WorldPlayers.TryRestore(saved, out WorldPlayers restored, out _) ? restored : null;
        }

        // A new world's seed: random per world, written once into the new file (an existing file keeps its own).
        private static ulong NewSeed()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            ulong seed = BitConverter.ToUInt64(bytes, 0);
            return seed == 0 ? 1UL : seed;
        }

        // The world was created or a player recorded or renamed: on disk now, not on the next tick.
        private void HandleWorldChanged()
        {
            if (store == null) return;
            store.Current.Players = world.Players?.ToSave();
            store.MarkDirty();
            SaveNow();
        }

        // AppBootstrap.Update: the flush while hosting.
        public void Tick()
        {
            if (disposed || store == null) return;
            if (!networkManager.IsServer && session.State == ConnState.Offline)   // the session ended without Ended reaching us: save, then let go
            {
                SaveNow();
                store = null;
                return;
            }
            float now = Time.unscaledTime;
            if (now >= nextFlush)
            {
                nextFlush = now + FlushSeconds;
                CaptureSections();
                if (store.IsDirty) Write();
            }
        }

        // Writes if anything changed (the last-played time moves with it). Host only; never throws (quit and session end call it).
        public void SaveNow()
        {
            if (store == null) return;
            try
            {
                CaptureSections();   // weapon growth and the town change constantly, so they are compared at each save, not marked per change
                if (store.IsDirty) Write();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Brings the live weapons and town into the file's value. Each is guarded alone: a failing one keeps its old section and the other still runs. "Town" here is every HostWorld town
        // section: stock, town, residents, dive, bags and the bank-room suspend (plan 3 task 6; HostWorld.CaptureTownInto writes it, or clears it once it was used).
        private void CaptureSections()
        {
            try { if (world.CaptureWeaponsInto(store.Current)) store.MarkDirty(); }
            catch (Exception e) { Debug.LogException(e); }
            try { if (world.CaptureTownInto(store.Current)) store.MarkDirty(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void Write()
        {
            store.Current.SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            store.MarkDirty();
            store.FlushIfDirty();
        }

        private void HandleEnded(EndReason reason)
        {
            if (disposed) return;
            SaveNow();
            store = null;
        }
    }
}
