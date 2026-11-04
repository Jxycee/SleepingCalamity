// Authority: test-only. The bank-room suspend section on the real save stack (plan 3 task 6): a real solo host, its HostWorld and WorldSaveFlow, a throwaway world library.
// Catches: the `suspend` section not being wired through HostWorld.CaptureTownInto / WorldSaveFlow.SaveNow into the world file (the Core tests cannot see that), the dive section's saved
// piece depths not reaching the file, a bank suspend being read back as a failed dive, a restored party that is not the one that was saved, and a used suspend never leaving the file.
// Does NOT catch: a client rejoining the suspended party (one process, one local player), the director's own capture and resume (task 7 and 8a), or the manual 2-PC walk.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
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
            TownSnapshot snap = TownSaves.Capture(null, null, null, null, null, section, true);
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
    }
}
