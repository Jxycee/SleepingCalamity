// App/WorldSaveFlow.cs: the host's world save flow. Not networked itself.
// Authority: host only. When this peer starts a session as the host (state Connecting), it opens this machine's world file (WorldSaveStore,
// world{suffix}.json) and loads the saved world's players into HostWorld before the host's own approval runs (the approver reads them).
// While hosting it saves:
//  - shortly after a change (HostWorld.Changed marks the file dirty; a dirty file flushes at most every FlushSeconds),
//  - when the session ends: on ShuttingDown or Failed (before NGO shuts down), again on Ended, and on quit.
// A client never opens its own world file. A newer build's file loads read-only (a toast says so). The dev New world button
// (DevNewWorldRequested) moves the file aside for the next session. One world file per machine profile in v1.
using System;
using System.Globalization;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.App
{
    public sealed class WorldSaveFlow : IDisposable
    {
        public const float FlushSeconds = 10f;       // a change is on disk within this

        private readonly INetworkSession session;
        private readonly NetworkManager networkManager;
        private readonly EventBus bus;
        private readonly HostWorld world;
        private readonly IDisposable newWorldSubscription;
        private WorldSaveStore store;
        private float nextFlush;
        private bool disposed;

        // PlayMode tests switch it off, so one test's world never seeds the next one's session (and none touches the profile's real world
        // file) unless it switches this on. Reset to on for every play session.
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
            newWorldSubscription = bus.Subscribe<DevNewWorldRequested>(HandleNewWorld);
        }

        // The attached store while hosting (tests read it), else null.
        public WorldSaveStore Store => store;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            newWorldSubscription?.Dispose();
            world.Changed -= HandleWorldChanged;
            session.StateChanged -= HandleStateChanged;
            session.Ended -= HandleEnded;
        }

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
            store = WorldSaveStore.CreateForThisInstance(NewSeed());
            store.Load();   // never throws
            world.Load(RestoreWorld(store.Current.Players));
            nextFlush = Time.unscaledTime + FlushSeconds;
            if (store.IsReadOnly)
                bus.Publish(new ToastRequested(store.NewerVersionOnDisk > 0 ? "save.world_read_only" : "save.world_load_failed", null, 6f));
        }

        // The saved players section as a world, or null when the file has none (a first run).
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

        private void HandleWorldChanged()
        {
            if (store == null) return;
            store.Current.Players = world.Players?.ToSave();
            store.MarkDirty();
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
                if (store.IsDirty) Write();
            }
        }

        // Writes if anything changed. Host only; never throws (quit and session end call it).
        public void SaveNow()
        {
            if (store == null) return;
            try
            {
                if (store.IsDirty) Write();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Write()
        {
            store.Current.SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            store.FlushIfDirty();
        }

        private void HandleEnded(EndReason reason)
        {
            if (disposed) return;
            SaveNow();
            store = null;
        }

        private void HandleNewWorld(DevNewWorldRequested msg)
        {
            if (disposed || !Debug.isDebugBuild) return;
            if (store == null || !networkManager.IsServer)
            {
                bus.Publish(new ToastRequested("dev.host_only"));
                return;
            }
            bus.Publish(new ToastRequested(store.Retire() ? "save.new_world" : "save.new_world_failed", null, 6f));
        }
    }
}
