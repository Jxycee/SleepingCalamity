// Authority: test-only. M7 S1: a host + three clients (the most a world records: 4 players) connect, one dive starts and the party takes a stair; ReplicatedStateHash must agree on all four peers
// after every step. A deliberate local corruption on one peer must make the same assertion FAIL (checked by catching the assertion, so the test itself passes).
// Bags (owner-only) and the stir (host-only internals) are checked against the HOST by ReplicatedStateHash.AssertBagsAndStirMatchHost: a clean run with items in several bags, an enemy drop, a cleared floor and the
// second half after a push-on; a deliberate corruption of one client's bag view and a deliberate stir drift on the host must each fail it.
// NOT covered: transforms, enemies, real-relay latency (run under NetCondition by the QA walks), visuals.
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class StateHashSmokeTests
    {
        private const float Timeout = 40f;
        private static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);

        [UnityTearDown]
        public IEnumerator TearDown() { yield return PlayModeHarness.TearDownApp(); }   // also leaves and disposes the clients StartClients created

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm;
            public List<PlayModeHarness.TestClient> Clients = new List<PlayModeHarness.TestClient>();
            public List<NetworkManager> Peers = new List<NetworkManager>();   // host first, then the clients
            public DiveDirector Host; public NetworkObject HostHero;
        }

        private static IEnumerator Start(System.Action<Rig> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            var rig = new Rig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            yield return PlayModeHarness.StartClients(rig.App, 3, false, rig.Clients);
            rig.Peers.Add(rig.HostNm);
            foreach (PlayModeHarness.TestClient c in rig.Clients) rig.Peers.Add(c.NetworkManager);

            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(Home, HubStair, Origin), Is.True, "[start] ServerConfigure");
            rig.HostHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, Home, Quaternion.identity);
            for (int i = 0; i < rig.Clients.Count; i++)
                rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.Clients[i].NetworkManager.LocalClientId, false, true, false, Home + new Vector3(2f * (i + 1), 0f, 0f), Quaternion.identity);
            foreach (PlayModeHarness.TestClient c in rig.Clients)
            {
                NetworkManager cnm = c.NetworkManager;
                yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null && cnm.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "[start] client " + cnm.LocalClientId + " never received its hero and the director");
            }
            done(rig);
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static IEnumerator FloorBuiltEverywhere(Rig rig, int floor, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync != null && rig.Host.HaulSync != null && rig.Host.ContentsSync != null, Timeout, step + ": the dive never spawned its syncs");
            foreach (NetworkManager nm in rig.Peers)
            {
                NetworkManager peer = nm;
                yield return PlayModeHarness.WaitUntil(() =>
                {
                    ulong id = rig.Host.FloorSync.NetworkObjectId;
                    return peer.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject o) && o.GetComponent<CellarFloorSync>().BuiltFloor == floor && o.GetComponent<CellarFloorSync>().Built != null;
                }, Timeout, step + ": a peer never built floor " + floor);
            }
        }

        private static void Move(NetworkObject o, Vector3 pos)
        {
            var cc = o.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            o.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        // Catches: any replicated dive / haul / floor / contents / town / hero field that differs between two peers, or a peer that misses a spawned object, with 4 players connected.
        [UnityTest]
        public IEnumerator FourPeers_HashesAgree_ThroughDiveStart_PieceFind_StairDescent_AndTheBankRoom()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            Assert.That(rig.Peers.Count, Is.EqualTo(4), "[start] host + 3 clients");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "connected, before the dive");

            Assert.That(rig.Host.ServerStartDive(), Is.True, "[F1 start] the host starts the dive");
            yield return FloorBuiltEverywhere(rig, 1, "[F1 start]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 dive started");
            string startText = ReplicatedStateHash.Describe(rig.HostNm);
            Assert.That(startText, Does.Contain("DiveDirector#").And.Contain("DiveHaulSync#").And.Contain("CellarFloorSync#").And.Contain("CellarContentsSync#").And.Contain("HeroVitals#"), "the hash covers the dive and hero state it claims to");
            Assert.That(startText, Does.Contain("NetActive = True"), "the hash sees the dive active");

            // A piece and an item: the haul summary changes and replicates to everyone.
            DiveHaulSync haul = rig.Host.HaulSync;
            Assert.That(haul.ServerAddPiece(PieceCatalog.FrameId), Is.True, "[F1 piece]");
            Assert.That(haul.ServerAddItem(rig.HostNm.LocalClientId, 101), Is.True, "[F1 loot]");   // a bag is host-only state: nothing of it replicates (the owner view is PlayerBagSync, plan 2 task 5)
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 piece found and an item in the host bag");

            // A hero takes damage: HeroVitals replicates.
            var vitals = rig.HostHero.GetComponent<SleepingCalamity.Gameplay.HeroVitals>();
            vitals.ServerDamage(15);
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "host hero hurt");

            // One floor transition: open the stair, put the host hero on it, descend.
            rig.Host.ServerOpenStair(1);
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, out Vector3 stair), Is.True, "[F1 stair] the floor has a stair");
            Move(rig.HostHero, stair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ServerTryStair(rig.HostNm.LocalClientId, 1), 10f, "[F1 stair] the host-side descent was refused");
            yield return FloorBuiltEverywhere(rig, 2, "[F2 arrive]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F2 arrived");
            Assert.That(ReplicatedStateHash.Describe(rig.HostNm), Does.Contain("CurrentFloor=2"), "the haul summary says floor 2");

            Assert.That(ReplicatedStateHash.Describe(rig.HostNm), Does.Contain("FoundPieces=1"), "the found piece shows in the hashed haul");

            // The bank room (floor 5): the dive fails, the next one starts at the bank (the checkpoint pick, decision 2) and the host-side bank press opens the room; everyone agrees at every step.
            Assert.That(rig.Host.ServerAbandonDive(), Is.True, "[F2 fail] the dive fails and the party goes home");
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && rig.Host.FloorSync.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "[F2 fail] the floor never cleared");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "failed dive, back home");
            rig.Host.TestHoldCheckpoint(true);
            Assert.That(rig.Host.ServerStartDive(UndercroftPlan.BankFloor), Is.True, "[F5 start] the checkpoint starts the dive at the bank");
            yield return FloorBuiltEverywhere(rig, UndercroftPlan.BankFloor, "[F5 start]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F5 arrived at the bank");
            Assert.That(haul.ServerEnterBankRoom(), Is.True, "[F5 bank] the host-side bank press (no position check) opens the room");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F5 bank room open");
            Assert.That(ReplicatedStateHash.Describe(rig.HostNm), Does.Contain("CurrentFloor=5"), "the bank room shows in the hashed haul");
        }

        // Catches: the hash comparing nothing (an always-equal assertion). A client's local copy of a replicated field is changed behind NGO's back, and the same assertion must fail and name the line.
        [UnityTest]
        public IEnumerator DeliberateCorruption_OnOneClient_MakesAssertAllEqualFail_AndNamesTheField()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[start dive]");
            yield return FloorBuiltEverywhere(rig, 1, "[start dive]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "clean before corruption");
            string cleanHash = ReplicatedStateHash.Hash(rig.HostNm);
            Assert.That(ReplicatedStateHash.Hash(rig.Peers[3]), Is.EqualTo(cleanHash), "[clean] equal hashes");

            NetworkObject dirOnClient = rig.Peers[2].SpawnManager.SpawnedObjects[rig.Host.NetworkObjectId];
            var peerDirector = dirOnClient.GetComponent<DiveDirector>();
            int real = peerDirector.NetPickableMaxFloor.Value;
            ReplicatedStateHash.CorruptLocalCopy(peerDirector.NetPickableMaxFloor, real + 2);
            Assert.That(peerDirector.NetPickableMaxFloor.Value, Is.EqualTo(real + 2), "test premise: the local copy was changed");
            Assert.That(ReplicatedStateHash.Hash(rig.Peers[2]), Is.Not.EqualTo(cleanHash), "the corrupted peer hashes differently");

            string message = null;
            IEnumerator check = ReplicatedStateHash.AssertAllEqual(rig.Peers, "corrupted", 2f);
            while (true)
            {
                bool more;
                try { more = check.MoveNext(); }
                catch (AssertionException e) { message = e.Message; break; }
                if (!more) break;
                yield return check.Current;
            }
            Assert.That(message, Is.Not.Null, "AssertAllEqual must fail when one peer's replicated state differs");
            Assert.That(message, Does.Contain("NetPickableMaxFloor"), "the failure names the differing field");
            Assert.That(message, Does.Contain("client " + rig.Peers[2].LocalClientId), "the failure names the corrupted peer");
        }

        // Runs an assertion coroutine to the end; returns its AssertionException message through `message` (null when it passed).
        private static IEnumerator Catch(IEnumerator check, System.Action<string> message)
        {
            while (true)
            {
                bool more;
                try { more = check.MoveNext(); }
                catch (AssertionException e) { message(e.Message); yield break; }
                if (!more) { message(null); yield break; }
                yield return check.Current;
            }
        }

        private static ulong[] ClientIds(Rig rig)
        {
            var ids = new ulong[rig.Peers.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = rig.Peers[i].LocalClientId;
            return ids;
        }

        // Catches: a bag (owner-only view) that differs from the host's authoritative bag, a stir that drifts from the bags / cleared floors / players, in a clean 4-peer run with items in several bags,
        // an enemy drop (the real per-player roll, paid through the host's own path) and a cleared floor. Bags are owner-only, so the check compares each peer to the HOST (ReplicatedStateHash.BagMismatch / StirMismatch).
        [UnityTest]
        public IEnumerator FourPeers_BagsAndStir_AgreeWithHost_ThroughItems_AnEnemyDrop_AndAClearedFloor()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[start dive]");
            yield return FloorBuiltEverywhere(rig, 1, "[F1]");
            DiveHaulSync haul = rig.Host.HaulSync;
            ulong[] ids = ClientIds(rig);
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 clean");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "F1 empty bags");

            Assert.That(haul.ServerAddItem(ids[0], 101), Is.True); Assert.That(haul.ServerAddItem(ids[1], 104), Is.True); Assert.That(haul.ServerAddItem(ids[1], 105), Is.True); Assert.That(haul.ServerAddItem(ids[3], 110), Is.True);
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 items in three bags");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "F1 items in three bags");
            Assert.That(haul.Current.StirValue, Is.GreaterThan(0), "four valuables are in the stir");
            foreach (NetworkManager nm in rig.Peers) Assert.That(PlayerBagSync.ForLocalPlayer(nm), Is.Not.Null, "every peer has its own bag view to compare");
            Assert.That(ReplicatedStateHash.DescribeLocal(rig.HostNm), Does.Contain("host bag of client " + ids[1]), "the host's description lists every bag");

            // An enemy drop through the real roll: client 2's bag gets exactly what CellarLoot.RollEnemyDrop says for its slot and this kill key; client 0 (a hitter too) rolls its own.
            CellarFloorLayout layout = rig.Host.FloorSync.Layout;
            int slot2 = haul.PlayerSlotOf(ids[2]), key = -1;
            for (int k = 0; k < 2000 && key < 0; k++) if (CellarLoot.RollEnemyDrop(layout.Seed, layout.Floor, k, slot2) != CellarLoot.EnemyDropNoItem) key = k;
            Assert.That(key, Is.GreaterThanOrEqualTo(0), "premise: some kill key drops for client 2");
            int want = CellarLoot.RollEnemyDrop(layout.Seed, layout.Floor, key, slot2);
            Assert.That(rig.Host.ContentsSync.TestQueueEnemyDrop(new[] { haul.PlayerIdOf(ids[2]), haul.PlayerIdOf(ids[0]) }, key), Is.True);
            yield return PlayModeHarness.WaitUntil(() => haul.ServerBagOf(ids[2]).Carried.Count == 1, Timeout, "[drop] client 2's drop was never paid");
            Assert.That(haul.ServerBagOf(ids[2]).Carried[0], Is.EqualTo(want), "the drop is the real per-slot roll");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 after an enemy drop");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "F1 after an enemy drop");
            yield return PlayModeHarness.WaitUntil(() => PlayerBagSync.ForLocalPlayer(rig.Peers[2]).View.Carried.Length == 1, Timeout, "[drop] client 2 never saw its own drop");

            // The cleared floor feeds the stir; the recomputed stir must follow.
            int before = haul.Current.StirValue;
            Assert.That(haul.ServerMarkCleared(1), Is.True, "[F1 clear]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 cleared");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "F1 cleared");
            Assert.That(haul.Current.StirValue, Is.GreaterThan(before), "a cleared floor stirs the dive");
        }

        // Catches: the stir across the dive's second half (after a push-on: baseline valuables, reward carry, the first half's floors no longer counting) drifting from what the bags and floors say.
        [UnityTest]
        public IEnumerator FourPeers_BagsAndStir_AgreeWithHost_AcrossThePushOnIntoTheSecondHalf()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            rig.Host.TestHoldCheckpoint(true);
            Assert.That(rig.Host.ServerStartDive(UndercroftPlan.BankFloor), Is.True, "[F5 start]");
            yield return FloorBuiltEverywhere(rig, UndercroftPlan.BankFloor, "[F5 start]");
            DiveHaulSync haul = rig.Host.HaulSync;
            ulong[] ids = ClientIds(rig);
            Assert.That(haul.ServerAddItem(ids[0], 101), Is.True); Assert.That(haul.ServerAddItem(ids[2], 112), Is.True);
            Assert.That(haul.ServerEnterBankRoom(), Is.True, "[F5 bank]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F5 bank room");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "F5 bank room");

            Assert.That(haul.Session.PushOn(), Is.True, "[push on] the party pushes on (the press rules are PerPlayerLootNetworkTests')");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "second half, nothing new found");
            Assert.That(haul.ServerAddItem(ids[1], 107), Is.True, "[second half] a new valuable");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "second half, one new valuable");
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "second half, one new valuable");
            Assert.That(haul.Session.Stir.Capture().SecondHalf, Is.True, "premise: the stir is in its second half");
            Assert.That(haul.Current.StirValue, Is.GreaterThan(0));
        }

        // Catches: BagMismatch comparing nothing. One client's LOCAL copy of its own owner-only bag view is changed behind NGO's back; the hash across peers cannot see it (owner-only), the host comparison must.
        [UnityTest]
        public IEnumerator DeliberateCorruption_OfOneClientsBagView_IsCaughtAgainstTheHostsBag()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[start dive]");
            yield return FloorBuiltEverywhere(rig, 1, "[start dive]");
            DiveHaulSync haul = rig.Host.HaulSync;
            ulong[] ids = ClientIds(rig);
            Assert.That(haul.ServerAddItem(ids[2], 104), Is.True); Assert.That(haul.ServerAddItem(ids[1], 110), Is.True);
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "clean before corruption");
            yield return PlayModeHarness.WaitUntil(() => PlayerBagSync.ForLocalPlayer(rig.Peers[2]).View.Carried.Length == 1, Timeout, "client 2 never saw its item");

            PlayerBagSync mine = PlayerBagSync.ForLocalPlayer(rig.Peers[2]);
            BagView bad = mine.View;
            bad.Carried.Add(112);   // an item the host's bag does not hold
            ReplicatedStateHash.CorruptLocalCopy(mine.Net, bad);
            Assert.That(mine.View.Carried.Length, Is.EqualTo(2), "test premise: the local copy was changed");

            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "cross-peer hash cannot see an owner-only bag", 3f);   // bags are not in the shared hash: this still passes
            string message = null;
            yield return Catch(ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "corrupted bag", 2f), m => message = m);
            Assert.That(message, Is.Not.Null, "the bag check must fail when a client's bag view differs from the host's bag");
            Assert.That(message, Does.Contain("bag of client " + ids[2]), "the failure names the client whose bag differs");
        }

        // Catches: StirMismatch comparing nothing. The host's stir counts a cleared floor the session never cleared (the stir and the dive's floors drift apart); the published stir then disagrees with the recompute.
        [UnityTest]
        public IEnumerator DeliberateStirDrift_OnTheHost_IsCaughtByTheRecompute()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[start dive]");
            yield return FloorBuiltEverywhere(rig, 1, "[start dive]");
            DiveHaulSync haul = rig.Host.HaulSync;
            yield return ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "clean before drift");
            haul.Session.Stir.OnFloorCleared(2);   // the stir alone, not DiveSession.ClearFloor: floor 2 is not in the session's cleared mask
            Assert.That(haul.Session.Stir.Capture().FloorMask, Is.Not.EqualTo(0), "test premise: the stir counted a floor");
            yield return PlayModeHarness.WaitUntil(() => haul.Current.StirValue > 0, Timeout, "the drifted stir was never published");   // the check polls until clean, so it must start after the publish

            string message = null;
            yield return Catch(ReplicatedStateHash.AssertBagsAndStirMatchHost(rig.Peers, "stir drift", 2f), m => message = m);
            Assert.That(message, Is.Not.Null, "the stir recompute must fail when the stir counts a floor the session did not clear");
            Assert.That(message, Does.Contain("StirValue published"), "the failure names the stir field");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "every peer still agrees on the (drifted) published stir", 5f);   // the cross-peer hash alone cannot see a host-side drift
        }

        // Catches: a fifth player getting into a four-player world (host + 3 clients is the maximum the join rules allow).
        [UnityTest]
        public IEnumerator FifthPlayer_IsRefused_WhenFourAreConnected()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            EndReason got = EndReason.UserLeft;
            yield return PlayModeHarness.JoinExpectingRefusal(rig.App, 40, r => got = r);
            Assert.That(got, Is.EqualTo(EndReason.FullLobby), "the fifth player is told the lobby is full");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(4), "the roster still holds exactly four peers");
        }
    }
}
