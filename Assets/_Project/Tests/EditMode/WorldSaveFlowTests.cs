// Tests/EditMode/WorldSaveFlowTests.cs: the host's world save flow (App/WorldSaveFlow) opening a world when a hosted session starts, on a fake session
// (FrontDoorFakes.cs), a real HostWorld and EventBus, and a throwaway worlds folder. Authority: test-only.
// Catches (audit 2026-11-12): A6 a damaged or missing selected world silently replaced by another one; A7 a fault while opening the world leaving
// the session running on the previous session's world with no word to the player; A11 the new world's file attached before its loads succeeded, so
// the next save writes the old world's town into it.
// NOT covered: the real session and scene flow (PlayMode SaveFlowTests), the front door's rows (FrontDoorModelTests).
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class WorldSaveFlowTests
    {
        private string _dir;
        private GameObject _go;
        private FakeNetworkSession _session;
        private HostWorld _world;
        private EventBus _bus;
        private IDisposable _toastSubscription;
        private List<string> _toasts;
        private WorldSaveFlow _flow;
        private bool _wasEnabled;
        private string _wasLibraryOverride, _wasPathOverride;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sc-saveflow-" + Guid.NewGuid().ToString("N"));
            // The flow's Library getter migrates the machine's old single world file: both paths point into the temp folder, so a test never touches the real profile.
            _wasLibraryOverride = WorldLibrary.DirectoryOverrideForTests;
            _wasPathOverride = WorldSaveStore.PathOverrideForTests;
            _wasEnabled = WorldSaveFlow.Enabled;
            WorldLibrary.DirectoryOverrideForTests = Path.Combine(_dir, "worlds");
            WorldSaveStore.PathOverrideForTests = Path.Combine(_dir, "legacy", "world.json");
            WorldSaveFlow.Enabled = true;
            HostWorld.LoadTownFaultForTests = null;

            _go = new GameObject("SaveFlowTest");
            NetworkManager nm = _go.AddComponent<NetworkManager>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            _session = new FakeNetworkSession();
            _world = new HostWorld();
            _bus = new EventBus();
            _toasts = new List<string>();
            _toastSubscription = _bus.Subscribe<ToastRequested>(t => _toasts.Add(t.Key));
            _flow = new WorldSaveFlow(_session, nm, _bus, _world);
        }

        [TearDown]
        public void TearDown()
        {
            HostWorld.LoadTownFaultForTests = null;
            _flow?.Dispose();
            _toastSubscription?.Dispose();
            WorldLibrary.DirectoryOverrideForTests = _wasLibraryOverride;
            WorldSaveStore.PathOverrideForTests = _wasPathOverride;
            WorldSaveFlow.Enabled = _wasEnabled;
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        // The host starts a session. The real session raises StateChanged inside EventIsolation, so an exception never reaches the caller there; this fake
        // does not isolate, so a throw is caught here and returned (null when the start went through).
        private Exception StartSolo() => Start(false);
        private Exception StartDirectHost() => Start(true);

        private Exception Start(bool allowRemote)
        {
            try { _session.StartLocalAsync(allowRemote, CancellationToken.None); return null; }
            catch (Exception e) { return e; }
        }

        private void Leave() => _session.LeaveAsync();

        private string PathOf(string worldId) => _flow.Library.PathOf(worldId);

        private static WorldPlayers OnePlayer(string id)
        {
            WorldPlayers.TryCreate(WorldKind.Solo, new[] { new PlayerKey(IdentityKind.Local, id) }, out WorldPlayers players);
            return players;
        }

        // A6 ----------------------------------------------------------------------------------------------------------------------------------

        // Catches: the host flow opening the player's second world (and writing to it) when the world they picked is damaged.
        [Test]
        public void SelectedWorldDamaged_DoesNotOpenAnotherWorld()
        {
            string healthy = _flow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            string damaged = WorldIds.NewId();
            File.WriteAllText(PathOf(damaged), "not json at all");
            string healthyBefore = File.ReadAllText(PathOf(healthy));
            _flow.Select(damaged);
            _world.Load(OnePlayer("stale"));   // what an earlier session left in the host's memory

            Assert.That(StartSolo(), Is.Null);

            Assert.That(_flow.Store, Is.Null, "no world was attached");
            Assert.That(_flow.SelectedWorldId, Is.EqualTo(damaged), "the pick is not changed behind the player's back");
            Assert.That(File.ReadAllText(PathOf(healthy)), Is.EqualTo(healthyBefore), "the other world was neither opened nor written");
            Assert.That(File.ReadAllText(PathOf(damaged)), Is.EqualTo("not json at all"), "and the damaged one is left as it is");
            Assert.That(_toasts, Does.Contain("save.world_damaged"), "the player is told");
            Assert.That(_world.Players, Is.Null, "nothing of an earlier session is left to play on");
            Assert.That(Directory.GetFiles(_flow.Library.Directory, "*.json").Length, Is.EqualTo(2), "no world was made either");
        }

        // Catches: a selected world whose file is gone being replaced by the newest other world.
        [Test]
        public void SelectedWorldMissing_DoesNotOpenAnotherWorld()
        {
            string healthy = _flow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            string healthyBefore = File.ReadAllText(PathOf(healthy));
            _flow.Select(WorldIds.NewId());

            Assert.That(StartSolo(), Is.Null);

            Assert.That(_flow.Store, Is.Null);
            Assert.That(File.ReadAllText(PathOf(healthy)), Is.EqualTo(healthyBefore), "the other world was not opened");
            Assert.That(_toasts, Does.Contain("save.world_damaged"));
            Assert.That(Directory.GetFiles(_flow.Library.Directory, "*.json").Length, Is.EqualTo(1), "and no world was made");
        }

        // Catches: the fix for A6 breaking a start from the other list: a selection left over from a Solo world is no pick for a Multiplayer start
        // (it opens the newest world of its own kind, as before); it is only a damaged or missing pick that is never replaced.
        [Test]
        public void SelectedWorldOfAnotherKind_StillOpensTheNewestOfThisKind()
        {
            string solo = _flow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            string multi = _flow.CreateWorld(WorldKind.Multiplayer, IdentityKind.Local);
            _flow.Select(solo);

            Assert.That(StartDirectHost(), Is.Null);

            Assert.That(_flow.Store, Is.Not.Null);
            Assert.That(_flow.SelectedWorldId, Is.EqualTo(multi));
            Assert.That(_toasts, Does.Not.Contain("save.world_damaged"));
        }

        // Catches: "the newest world" including a damaged file when nothing was picked.
        [Test]
        public void NothingPicked_OpensTheNewestHealthyWorld_NeverADamagedOne()
        {
            string healthy = _flow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            File.WriteAllText(PathOf(WorldIds.NewId()), "not json at all");
            _flow.Select(null);

            Assert.That(StartSolo(), Is.Null);

            Assert.That(_flow.SelectedWorldId, Is.EqualTo(healthy));
            Assert.That(_flow.Store, Is.Not.Null);
            Assert.That(_toasts, Does.Not.Contain("save.world_damaged"));
        }

        // A7 ----------------------------------------------------------------------------------------------------------------------------------

        // Catches: a fault while opening the world (the worlds folder cannot be listed) escaping into the session and leaving the previous session's world
        // in HostWorld with no toast. The directory override "" makes the library fail to build, as a locked or unreachable folder does.
        [Test]
        public void OpenFault_ClearsWorldAndToasts()
        {
            _world.Load(OnePlayer("stale"));
            _world.LoadTown(new WorldSave { Stock = new StockSave { Pieces = { 100 } } });
            Assert.That(_world.Town.Stock, Is.Not.Null, "the previous session's town is in memory");
            WorldLibrary.DirectoryOverrideForTests = "";

            Exception thrown = StartSolo();

            Assert.That(thrown, Is.Null, "the session carries on: the fault stays inside the save flow");
            Assert.That(_flow.Store, Is.Null, "nothing is attached, so nothing can be written");
            Assert.That(_world.Players, Is.Null, "HostWorld holds no world of an earlier session");
            Assert.That(_world.Town.Stock, Is.Null);
            Assert.That(_world.Weapons.Weapons, Is.Empty);
            Assert.That(_toasts, Does.Contain("save.world_load_failed"), "the player is told that nothing will be saved");
            Assert.That(Directory.Exists(Path.Combine(_dir, "worlds")), Is.False, "nothing was written");
        }

        // A11 ---------------------------------------------------------------------------------------------------------------------------------

        // Catches: world B's file attached before its town loaded; a throw in the load then left world A's town in HostWorld, and the next save wrote
        // it into B's file.
        [Test]
        public void LoadFault_AfterAnotherWorld_NeverWritesTheOldWorldIntoTheNewFile()
        {
            string a = _flow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            WorldSaveStore aFile = _flow.Library.Open(a);
            aFile.Current.Stock = new StockSave { Pieces = { 100 } };
            aFile.MarkDirty();
            Assert.That(aFile.Save(), Is.True);
            Assert.That(StartSolo(), Is.Null);
            Assert.That(_flow.SelectedWorldId, Is.EqualTo(a));
            Assert.That(_world.Town.Stock?.Pieces, Does.Contain(100), "world A's town is loaded");
            Leave();

            string b = _flow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            string bBefore = File.ReadAllText(PathOf(b));
            HostWorld.LoadTownFaultForTests = () => throw new IOException("the town load failed");

            StartSolo();   // on the old code the exception reaches this caller; the real session swallows it
            _flow.SaveNow();

            Assert.That(_flow.Store, Is.Null, "B is not attached: its loads did not succeed");
            Assert.That(File.ReadAllText(PathOf(b)), Is.EqualTo(bBefore), "B's file was not written");
            Assert.That(_world.Town.Stock, Is.Null, "and world A's town is not left in memory for the next world");
            Assert.That(_toasts, Does.Contain("save.world_load_failed"));
        }
    }
}
