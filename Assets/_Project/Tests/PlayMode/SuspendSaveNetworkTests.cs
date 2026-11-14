// Authority: test-only. The bank-room suspend section on the real save stack (plan 3 task 6): a real solo host, its HostWorld and WorldSaveFlow, a throwaway world library.
// Catches: the `suspend` section not being wired through HostWorld.CaptureTownInto / WorldSaveFlow.SaveNow into the world file (the Core tests cannot see that), the dive section's saved
// piece depths not reaching the file, a bank suspend being read back as a failed dive, a restored party that is not the one that was saved, and a used suspend never leaving the file.
// Does NOT catch: a client rejoining the suspended party (one process, one local player), or the manual 2-PC walk.
// Catches (Director_*): the REAL director's capture and resume on the real stack (plan 3 tasks 8 and 8a): an autosave between the load and the trapdoor wiping the loaded suspend (and the carried items with
// it), a resume that does not bring the bags, picks, seed and phase back, a push-on suspend that outlives the stair, a stale suspend surviving a failed dive.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class SuspendSaveNetworkTests
    {
        private const float Timeout = 40f;
        private string _dir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            WorldSaveStore.PathOverrideForTests = null;
            yield return PlayModeHarness.TearDownApp();
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        private sealed class SuspendSource : HostWorld.ITownSource
        {
            public TownSnapshot Live;
            public TownSnapshot Restored;
            public TownSnapshot CaptureTown() => Live;
            public void RestoreTown(TownSnapshot loaded) { Restored = loaded; }
        }

        private static WorldSave ReadWorld(string dir, string worldId)
            => JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(dir, worldId + ".json")), w => w.SchemaVersion);

        // The owner alone at the cleared floor-5 bank room, carrying two items with one picked, a satchel, dive gold and a chest open; 5 gold and a stash item at home.
        private static DiveSession BankRoomSession(PartyBags bags, string ownerId)
        {
            PlayerBag bag = PlayerBag.Restore(5, new Dictionary<int, int> { { 109, 1 } }, null);
            bag.Add(101); bag.OpenBank(1); bag.Pick(0); bag.PushOn();   // 101 banked this dive
            bag.Add(104); bag.Add(105);
            bag.AddGold(7);
            bag.MarkChestOpened(55u, 3, 63);
            bag.Satchel.Restore(new LostSatchel(3, new[] { 106 }));
            bag.Satchel.PickUp();
            bags.Adopt(ownerId, bag);

            var session = new DiveSession(bags, new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));
            Assert.That(session.TryStart(1, false), Is.True);
            for (int f = 1; f < UndercroftPlan.BankFloor; f++) { session.ClearFloor(f); Assert.That(session.EnterFloor(f + 1), Is.True); }
            session.ClearFloor(UndercroftPlan.BankFloor);
            Assert.That(session.EnterBankRoom(new List<int> { 0 }, 777UL, -1, new[] { ownerId }), Is.True);
            Assert.That(bag.Pick(0), Is.True);                       // 104
            Assert.That(bag.Pick(PlayerBag.SatchelPick), Is.True);   // the carried satchel
            session.Stir.PlayersPresent = 1;
            session.Stir.UnbankedValuables = 3;
            return session;
        }

        [UnityTest]
        public IEnumerator SuspendSave_WrittenInPlay_AndReloads()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            WorldSaveStore.PathOverrideForTests = Path.Combine(_dir, "legacy-single-file", "world.json");   // the old single file: never the user's real world.json
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            string worldId = app.WorldSaveFlow.SelectedWorldId;
            Assert.That(worldId, Is.Not.Null, "the session opened a world in the throwaway library");
            PlayerKey owner = app.World.Players.Recorded[0];
            string ownerId = owner.ToString();
            Assert.That(app.World.PieceFloors.Count, Is.EqualTo(PieceCatalog.Specs.Count), "premise: the host knows the piece depths of this world");

            // ---- session 1: the party is in the bank room, the host saves
            var bags = new PartyBags();
            DiveSession session = BankRoomSession(bags, ownerId);
            var keys = new Dictionary<string, PlayerKey> { { ownerId, owner } };
            var records = new List<SuspendedBag>();
            BagsSave home = BagsSaveRules.Capture(bags, keys, true, session.Floor, suspended: true, suspendedBags: records);
            SuspendSave section = SuspendSaveRules.Build(session.Snapshot(), 4242u, new[] { ownerId }, new[] { ownerId }, records);
            TownSnapshot snap = TownSaves.Capture(null, null, null, section, true);
            snap.Dive.InDive = true;
            snap.Bags = home;
            var source = new SuspendSource { Live = snap };
            app.World.RegisterTownSource(source);
            app.WorldSaveFlow.SaveNow();

            string text = File.ReadAllText(Path.Combine(_dir, worldId + ".json"));
            Assert.That(text, Does.Contain("\"suspend\""), "the section is in the world file");
            WorldSave saved = ReadWorld(_dir, worldId);
            Assert.That(saved.Suspend, Is.Not.Null);
            Assert.That(saved.Suspend.Phase, Is.EqualTo((int)DivePhase.BankRoom));
            Assert.That(saved.Suspend.Floor, Is.EqualTo(5));
            Assert.That(saved.Suspend.Seed, Is.EqualTo(4242u));
            Assert.That(saved.Suspend.PresentIds, Is.EqualTo(new[] { ownerId }));
            Assert.That(saved.Suspend.Bags.Count, Is.EqualTo(1));
            Assert.That(saved.Suspend.Bags[0].Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(saved.Suspend.Bags[0].OpenedChests, Is.EqualTo(1UL << 63));
            Assert.That(saved.Bags.Players[0].Gold, Is.EqualTo(5), "the home half is in the bags section, not failed over");
            Assert.That(saved.Bags.Players[0].Satchel, Is.Null, "the carried items were not turned into a waiting satchel");
            Assert.That(saved.Dive.PieceDepths.Count, Is.EqualTo(PieceCatalog.Specs.Count), "the piece depths were written with the dive section");
            foreach (PieceDepthSave p in saved.Dive.PieceDepths) Assert.That(app.World.PieceFloors[p.Id], Is.EqualTo(p.Floor));

            // ---- the world is closed (the final save on leaving must not undo it) and reopened
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            Assert.That(ReadWorld(_dir, worldId).Suspend, Is.Not.Null, "still there after leaving");

            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "the same world reopened");
            var source2 = new SuspendSource { Live = new TownSnapshot() };   // says nothing about the suspend yet
            app.World.RegisterTownSource(source2);
            TownSnapshot loaded = source2.Restored;
            Assert.That(loaded, Is.Not.Null, "the new host source was handed the loaded sections");
            Assert.That(loaded.Suspend, Is.Not.Null, "a fresh host loads the suspend section");
            Assert.That(loaded.Dive.InDive, Is.True, "and the bank suspend was not counted as a failed dive");

            // the loaded section rebuilds the party that was saved
            var bags2 = new PartyBags();
            BagsSaveRules.RestoreSuspended(loaded.Bags, loaded.Suspend, bags2);
            var session2 = new DiveSession(bags2, new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));
            session2.Restore(SuspendSaveRules.ToSnapshot(loaded.Suspend, new[] { ownerId }));
            Assert.That(session2.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(session2.Floor, Is.EqualTo(5));
            Assert.That(session2.Stir.Capture(), Is.EqualTo(session.Stir.Capture()));
            PlayerBag back = bags2.For(ownerId);
            Assert.That(back.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(back.BankedThisDive, Is.EqualTo(new Dictionary<int, int> { { 101, 1 } }));
            Assert.That(back.DiveGold, Is.EqualTo(7));
            Assert.That(back.Gold, Is.EqualTo(5));
            Assert.That(back.ChestOpened(55u, 3, 63), Is.True);
            Assert.That(back.Picks, Is.EqualTo(new[] { 0, PlayerBag.SatchelPick }));
            Assert.That(back.Satchel.Carried.Items, Is.EqualTo(new[] { 106 }));

            // the suspend is used: the host's next capture says "known, none" and the file loses the section
            source2.Live = new TownSnapshot { SuspendKnown = true };
            app.WorldSaveFlow.SaveNow();
            Assert.That(ReadWorld(_dir, worldId).Suspend, Is.Null, "a used suspend is cleared from the file");
            Assert.That(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), Does.Not.Contain("\"suspend\""));
            Assert.That(ReadWorld(_dir, worldId).Dive.PieceDepths.Count, Is.EqualTo(PieceCatalog.Specs.Count), "the depths stay");
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        // A direct host (no scenes), one client with PlayerId(clientIndex), and a configured director that registers with the real HostWorld.
        private static IEnumerator StartSavedHost(int clientIndex, List<PlayModeHarness.TestClient> clients, System.Action<AppBootstrap, DiveDirector> done, int clientCount = 1)
        {
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            yield return PlayModeHarness.StartClients(app, clientCount, false, clients, clientIndex);
            // The world records its players when they PLAY it (audit A2): with no scene flow here, the start gate does it.
            Assert.That(app.World.TryStartSession(app.Roster), Is.EqualTo(WorldPlayerVerdict.Ok), "setup: the gate records the connected players");

            NetworkManager hostNm = app.NetworkManager;
            NetworkObject o = hostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveDirector dir = o.GetComponent<DiveDirector>();
            Assert.That(dir.ServerConfigure(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            done(app, dir);
        }

        [UnityTest]
        public IEnumerator Director_BankRoomQuit_SurvivesAnAutosaveBeforeTheTrapdoor_ResumesAfterRestart_StairAndFailureWipeIt()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            WorldSaveStore.PathOverrideForTests = Path.Combine(_dir, "legacy-single-file", "world.json");

            // ---- session 1: A and the host are in the bank room, A carrying two items with one picked; the host saves (the quit)
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveDirector dir = null;
            yield return StartSavedHost(20, clients, (a, d) => { app = a; dir = d; });
            string worldId = app.WorldSaveFlow.SelectedWorldId;
            ulong ca = clients[0].NetworkManager.LocalClientId;
            string aId = clients[0].Key.Id;
            dir.SeedOverride = 6201u;
            Assert.That(dir.ServerStartDive(), Is.True);
            DiveHaulSync haul = dir.HaulSync;
            Assert.That(haul.ServerAddItem(ca, 104) && haul.ServerAddItem(ca, 105), Is.True);
            Assert.That(haul.ServerAddGold(ca, 7), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            Assert.That(haul.ServerPick(ca, 0), Is.True);
            app.WorldSaveFlow.SaveNow();
            WorldSave w1 = ReadWorld(_dir, worldId);
            Assert.That(w1.Suspend, Is.Not.Null, "the quit in the bank room wrote a suspend");
            Assert.That(w1.Suspend.Phase, Is.EqualTo((int)DivePhase.BankRoom));
            Assert.That(w1.Suspend.BankChosen, Is.False);
            Assert.That(w1.Suspend.Seed, Is.EqualTo(6201u));
            SuspendedBag rec = w1.Suspend.Bags.Single(b => b.Id == aId);
            Assert.That(rec.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(rec.Picks, Is.EqualTo(new[] { 0 }));
            Assert.That(rec.DiveGold, Is.EqualTo(7));
            Assert.That(w1.Dive.InDive, Is.True);

            yield return PlayModeHarness.LeaveAndWaitForLobby();
            if (clients[0].Session.State != ConnState.Offline) yield return PlayModeHarness.Await(clients[0].Session.LeaveAsync(), Timeout);
            Assert.That(ReadWorld(_dir, worldId).Suspend, Is.Not.Null, "the final save on leaving kept it");

            // ---- session 2: the same world. An autosave before anyone enters the trapdoor must not lose the suspend
            var again = new List<PlayModeHarness.TestClient>();
            AppBootstrap app2 = null; DiveDirector dir2 = null;
            yield return StartSavedHost(20, again, (x, d) => { app2 = x; dir2 = d; });
            Assert.That(app2.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId));
            ulong ca2 = again[0].NetworkManager.LocalClientId;
            Assert.That(dir2.NetSuspendedDive.Value, Is.True, "the director holds the loaded suspend for the trapdoor");
            app2.WorldSaveFlow.SaveNow();
            WorldSave w2 = ReadWorld(_dir, worldId);
            Assert.That(w2.Suspend, Is.Not.Null, "an autosave between the load and the trapdoor did not wipe the suspend");
            Assert.That(w2.Suspend.Bags.Single(b => b.Id == aId).Carried, Is.EqualTo(new[] { 104, 105 }), "nor the carried items");
            Assert.That(w2.Dive.InDive, Is.True);

            dir2.SeedOverride = 9999u;   // a resume ignores it: the saved seed rules
            Assert.That(dir2.ServerStartDive(), Is.True, "the trapdoor resumes the suspended dive");
            DiveHaulSync haul2 = dir2.HaulSync;
            Assert.That(dir2.Session.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(dir2.Session.Floor, Is.EqualTo(5));
            Assert.That(dir2.Seed, Is.EqualTo(6201u));
            Assert.That(dir2.Checkpoint.Held, Is.False, "a resume neither sets nor spends the checkpoint");
            PlayerBag back = haul2.ServerBagOf(ca2);
            Assert.That(back.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(back.DiveGold, Is.EqualTo(7));
            Assert.That(back.BankOpen, Is.True, "the visit is open again");
            Assert.That(back.Picks, Is.EqualTo(new[] { 0 }), "with the saved pick");
            Assert.That(dir2.NetSuspendedDive.Value, Is.False);
            app2.WorldSaveFlow.SaveNow();
            Assert.That(ReadWorld(_dir, worldId).Suspend, Is.Not.Null, "back in the bank room, a quit is a suspend again");

            Assert.That(haul2.PushOnNow(), Is.True);
            app2.WorldSaveFlow.SaveNow();
            SuspendSave afterPush = ReadWorld(_dir, worldId).Suspend;
            Assert.That(afterPush, Is.Not.Null);
            Assert.That(afterPush.BankChosen, Is.True, "push-on writes the suspend");
            Assert.That(afterPush.Bags.Single(b => b.Id == aId).BankedThisDive.Single().ItemId, Is.EqualTo(104));

            Assert.That(haul2.ServerEnterFloor(6), Is.True);
            app2.WorldSaveFlow.SaveNow();
            WorldSave w4 = ReadWorld(_dir, worldId);
            Assert.That(w4.Suspend, Is.Null, "taking the stair wipes the suspend");
            Assert.That(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), Does.Not.Contain("\"suspend\""));
            Assert.That(w4.Dive.InDive, Is.True, "a quit on floor 6 is the old failed-dive rule");

            Assert.That(dir2.ServerAbandonDive(), Is.True);
            app2.WorldSaveFlow.SaveNow();
            WorldSave w5 = ReadWorld(_dir, worldId);
            Assert.That(w5.Suspend, Is.Null);
            Assert.That(w5.Dive.InDive, Is.False, "a failed dive is over");
            Assert.That(w5.Bags.Players.Single(p => p.Id == aId).Satchel, Is.Not.Null, "its carried item waits in a satchel");
        }

        // Catches: the last capture of a real quit (DiveDirector.OnNetworkDespawn -> UnregisterTownSource) running after DiveHaulSync despawned, so a 3-player bank room was saved with ExtraSlotOwner null
        // and no PresentIds, and the resumed visit picked the extra slot again (the owner lost it). Goes through the real shutdown (LeaveAndWaitForLobby), not SaveNow.
        [UnityTest]
        public IEnumerator Director_ThreePlayerBankRoomQuit_SavesTheExtraSlotOwner_ResumeKeepsIt()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            WorldSaveStore.PathOverrideForTests = Path.Combine(_dir, "legacy-single-file", "world.json");

            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveDirector dir = null;
            yield return StartSavedHost(20, clients, (a, d) => { app = a; dir = d; }, 2);
            string worldId = app.WorldSaveFlow.SelectedWorldId;
            ulong[] ids = { app.NetworkManager.LocalClientId, clients[0].NetworkManager.LocalClientId, clients[1].NetworkManager.LocalClientId };
            dir.SeedOverride = 6202u;
            Assert.That(dir.ServerStartDive(), Is.True);
            DiveHaulSync haul = dir.HaulSync;
            foreach (ulong id in ids) Assert.That(haul.ServerAddItem(id, 104), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            int ownerSlot = haul.Session.ExtraSlotOwner;
            Assert.That(ownerSlot, Is.InRange(0, 3), "premise: three players, one has the extra slot");
            string ownerId = haul.IdsBySlot()[ownerSlot];
            Assert.That(ownerId, Is.Not.Null);

            // the quit: Leave runs the ShuttingDown save, then NGO despawns the director (its last capture), then the final save
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            foreach (PlayModeHarness.TestClient c in clients) if (c.Session.State != ConnState.Offline) yield return PlayModeHarness.Await(c.Session.LeaveAsync(), Timeout);
            WorldSave w1 = ReadWorld(_dir, worldId);
            Assert.That(w1.Suspend, Is.Not.Null, "the quit in the bank room wrote a suspend");
            Assert.That(w1.Suspend.ExtraSlotOwner, Is.EqualTo(ownerId), "the extra slot's owner survives the real shutdown");
            Assert.That(w1.Suspend.PresentIds.Count, Is.EqualTo(3), "and so do the players present");
            Assert.That(w1.Dive.ExtraSlotOwner, Is.EqualTo(ownerId), "the previous-owner mark too");

            // session 2: the same three players; the resumed visit keeps the owner (it is not picked again)
            var again = new List<PlayModeHarness.TestClient>();
            AppBootstrap app2 = null; DiveDirector dir2 = null;
            yield return StartSavedHost(20, again, (x, d) => { app2 = x; dir2 = d; }, 2);
            Assert.That(dir2.ServerStartDive(), Is.True, "the trapdoor resumes it");
            DiveHaulSync haul2 = dir2.HaulSync;
            Assert.That(dir2.Session.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(haul2.SlotOfBagId(ownerId), Is.EqualTo(dir2.Session.ExtraSlotOwner), "the same player still has the extra slot");
            ulong[] ids2 = { app2.NetworkManager.LocalClientId, again[0].NetworkManager.LocalClientId, again[1].NetworkManager.LocalClientId };
            int twos = 0;
            foreach (ulong id in ids2) if (haul2.ServerBagOf(id).SlotCapacity == 2) { twos++; Assert.That(haul2.PlayerIdOf(id), Is.EqualTo(ownerId)); }
            Assert.That(twos, Is.EqualTo(1));
        }

        // Catches: ServerAdoptResumed marking PreviousExtraOwner as already written for a resumed visit that had NO extra owner (two players), so a third player joining later picked an owner that was
        // never recorded as the previous one (the same player could then have the extra slot on the next visit too).
        [UnityTest]
        public IEnumerator Director_ResumedTwoPlayerRoom_ThirdPlayerJoins_RecordsTheNewExtraOwner()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            WorldSaveStore.PathOverrideForTests = Path.Combine(_dir, "legacy-single-file", "world.json");

            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveDirector dir = null;
            yield return StartSavedHost(20, clients, (a, d) => { app = a; dir = d; });
            string worldId = app.WorldSaveFlow.SelectedWorldId;
            dir.SeedOverride = 6203u;
            Assert.That(dir.ServerStartDive(), Is.True);
            Assert.That(dir.HaulSync.BankRoomNow(), Is.True);
            Assert.That(dir.Session.ExtraSlotOwner, Is.EqualTo(-1), "premise: two players, no extra owner");
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            foreach (PlayModeHarness.TestClient c in clients) if (c.Session.State != ConnState.Offline) yield return PlayModeHarness.Await(c.Session.LeaveAsync(), Timeout);
            Assert.That(ReadWorld(_dir, worldId).Suspend, Is.Not.Null, "premise: the bank room quit wrote a suspend");

            var again = new List<PlayModeHarness.TestClient>();
            AppBootstrap app2 = null; DiveDirector dir2 = null;
            yield return StartSavedHost(20, again, (x, d) => { app2 = x; dir2 = d; });
            Assert.That(dir2.ServerStartDive(), Is.True, "resumed");
            DiveHaulSync haul2 = dir2.HaulSync;
            Assert.That(haul2.Session.ExtraSlotOwner, Is.EqualTo(-1));
            Assert.That(haul2.PreviousExtraOwner, Is.EqualTo(-1), "premise: nothing recorded yet");

            yield return PlayModeHarness.StartClients(app2, 1, false, again, 22);   // a third player joins the open room
            yield return PlayModeHarness.WaitUntil(() => haul2.Session.ExtraSlotOwner >= 0, Timeout, "the room never gave the three players an extra slot");
            Assert.That(haul2.PreviousExtraOwner, Is.EqualTo(haul2.Session.ExtraSlotOwner), "the owner picked this visit is recorded as the previous one");
        }
    }
}
