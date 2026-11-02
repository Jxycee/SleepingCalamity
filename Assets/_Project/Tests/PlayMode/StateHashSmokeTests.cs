// Authority: test-only. M7 S1: a host + three clients (the most a world records: 4 players) connect, one dive starts and the party takes a stair; ReplicatedStateHash must agree on all four peers
// after every step. A deliberate local corruption on one peer must make the same assertion FAIL (checked by catching the assertion, so the test itself passes).
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
        public IEnumerator FourPeers_HashesAgree_ThroughDiveStart_PieceBank_AndAStairDescent()
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

            // A piece and the loot, then the bank: the haul summary changes and replicates to everyone.
            DiveHaulSync haul = rig.Host.HaulSync;
            Assert.That(haul.ServerAddPiece(DiveHaul.PieceId(1, 0)), Is.True, "[F1 piece]");
            Assert.That(haul.ServerAddLoot(1, 2), Is.True, "[F1 loot]");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F1 piece and loot carried");

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

            // F2 has the checkpoint bank: bank the carried piece (host-side, no position check) and everyone agrees.
            Assert.That(haul.ServerBank(), Is.True, "[F2 bank] the host-side bank");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Peers, "F2 banked");
            Assert.That(ReplicatedStateHash.Describe(rig.HostNm), Does.Contain("BankedPieces=1"), "the bank shows in the hashed haul");
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
            int real = peerDirector.NetStartFloor.Value;
            ReplicatedStateHash.CorruptLocalCopy(peerDirector.NetStartFloor, real + 2);
            Assert.That(peerDirector.NetStartFloor.Value, Is.EqualTo(real + 2), "test premise: the local copy was changed");
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
            Assert.That(message, Does.Contain("NetStartFloor"), "the failure names the differing field");
            Assert.That(message, Does.Contain("client " + rig.Peers[2].LocalClientId), "the failure names the corrupted peer");
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
