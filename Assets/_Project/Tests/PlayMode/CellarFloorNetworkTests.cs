// Authority: test-only. M5 stage 3: the host sets (seed, floor) on CellarFloorSync and every peer builds the same Cellars floor locally.
// Host + two clients in one process (scene management off; the sync object is a dynamically spawned network prefab from the owned list).
// Catches: a floor that differs between peers (LayoutHash and built object counts compared with each other and with the Core generator),
// a floor change (floor 1 -> 2, new seed) not reaching a connected client, and a late joiner not getting the current (seed, floor) with the spawn.
// NOT covered by a one-process test: real relay latency, scene loads with the floor in the dungeon scene (the manual 2-player walk).
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
    public class CellarFloorNetworkTests
    {
        private const float Timeout = 20f;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        private IEnumerator Join(AppBootstrap app, int index, System.Action<PlayModeHarness.TestClient> done)
        {
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(index));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "client " + index + " never connected");
            done(client);
        }

        private static GameObject SyncPrefab(NetworkManager host)
        {
            foreach (NetworkPrefabsList list in host.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<CellarFloorSync>() != null) return entry.Prefab;
            Assert.Fail("CellarFloorSync is not in the network prefab list (run the project builder)");
            return null;
        }

        private static CellarFloorSync On(NetworkManager nm, ulong id) => nm.SpawnManager.SpawnedObjects[id].GetComponent<CellarFloorSync>();

        private static IEnumerator WaitBuilt(CellarFloorSync s, uint seed, int floor, string who) =>
            PlayModeHarness.WaitUntil(() => s != null && s.BuiltFloor == floor && s.BuiltSeed == seed && s.Built != null, Timeout, who + " never built seed " + seed + " floor " + floor);

        private static int Children(CellarFloorBuild b, string group) => b.Root.Find(group).childCount;

        private static void AssertSame(CellarFloorSync host, CellarFloorSync peer, uint seed, int floor, string who)
        {
            ulong expected = CellarFloorGenerator.Generate(seed, floor).LayoutHash();
            Assert.That(host.BuiltLayoutHash, Is.EqualTo(expected), "host hash vs generator");
            Assert.That(peer.BuiltLayoutHash, Is.EqualTo(expected), who + " hash vs generator");
            Assert.That(peer.BuiltLayoutHash, Is.EqualTo(host.BuiltLayoutHash), who + " hash vs host");
            CellarFloorBuild h = host.Built, p = peer.Built;
            Assert.That(p.FloorTiles, Is.EqualTo(h.FloorTiles), who + " floor tiles");
            Assert.That(p.Walls, Is.EqualTo(h.Walls), who + " walls");
            Assert.That(p.DoorOpenings, Is.EqualTo(h.DoorOpenings), who + " door openings");
            Assert.That(p.Pillars, Is.EqualTo(h.Pillars), who + " pillars");
            Assert.That(p.Lamps, Is.EqualTo(h.Lamps), who + " lamps");
            foreach (string g in new[] { CellarFloorBuilder.FloorGroup, CellarFloorBuilder.WallGroup, CellarFloorBuilder.DoorGroup, CellarFloorBuilder.PillarGroup, CellarFloorBuilder.MarkerGroup, CellarFloorBuilder.LampGroup })
                Assert.That(Children(p, g), Is.EqualTo(Children(h, g)), who + " objects in group " + g);
            Assert.That(h.FloorTiles, Is.GreaterThan(0), "a floor was actually built");
        }

        // Catches: seed/floor not reaching connected clients, a floor change not rebuilding, a late joiner missing the state.
        [UnityTest]
        public IEnumerator HostSetsSeedAndFloor_EveryPeerBuildsTheSameFloor_IncludingLateJoin()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;

            PlayModeHarness.TestClient a = null;
            yield return Join(app, 8, c => a = c);

            NetworkObject spawned = hostNm.SpawnManager.InstantiateAndSpawn(SyncPrefab(hostNm).GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            CellarFloorSync host = spawned.GetComponent<CellarFloorSync>();
            ulong id = spawned.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => a.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client A never received the sync object");
            CellarFloorSync peerA = On(a.NetworkManager, id);
            Assert.That(host.BuiltFloor, Is.EqualTo(0), "no floor before the host sets one");
            Assert.That(peerA.BuiltFloor, Is.EqualTo(0));

            Assert.That(peerA.ServerSetFloor(1u, 1), Is.False, "a client cannot set the floor");

            const uint seedA = 12345u, seedB = 0xC0FFEE12u;   // the second seed has the high bit set: the full 32 bits travel
            Assert.That(host.ServerSetFloor(seedA, 1), Is.True);
            yield return WaitBuilt(peerA, seedA, 1, "client A");
            AssertSame(host, peerA, seedA, 1, "client A f1");
            Assert.That(host.BuiltLayoutHash, Is.EqualTo(2055763217958189686UL), "the golden hash of seed 12345 floor 1");

            Assert.That(host.ServerSetFloor(seedA, 2), Is.True);
            yield return WaitBuilt(peerA, seedA, 2, "client A");
            AssertSame(host, peerA, seedA, 2, "client A f2");

            Assert.That(host.ServerSetFloor(seedB, 1), Is.True);
            yield return WaitBuilt(peerA, seedB, 1, "client A");
            AssertSame(host, peerA, seedB, 1, "client A seedB f1");

            Assert.That(host.ServerSetFloor(seedB, 2), Is.True);
            yield return WaitBuilt(peerA, seedB, 2, "client A");
            AssertSame(host, peerA, seedB, 2, "client A seedB f2");
            Assert.That(peerA.BuildCount, Is.EqualTo(4), "one rebuild per change, not one per variable");
            Assert.That(host.BuildCount, Is.EqualTo(4), "the host builds once per change, no double build");

            // Late join: client B connects after the seed was set and must build the current floor from the spawn alone.
            PlayModeHarness.TestClient b = null;
            yield return Join(app, 9, c => b = c);
            yield return PlayModeHarness.WaitUntil(() => b.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client B never received the sync object");
            CellarFloorSync peerB = On(b.NetworkManager, id);
            yield return WaitBuilt(peerB, seedB, 2, "late client B");
            AssertSame(host, peerB, seedB, 2, "late client B");
            Assert.That(peerB.BuildCount, Is.EqualTo(1), "the late joiner builds once");

            // Floor 4, the fixed bakery set-piece: both peers build it from (seed, floor) and agree with the Core layout hash (the stair-less floor has a Bank, a baker and a Warden marker).
            Assert.That(host.ServerSetFloor(seedB, 4), Is.True);
            yield return WaitBuilt(peerA, seedB, 4, "client A");
            yield return WaitBuilt(peerB, seedB, 4, "late client B");
            AssertSame(host, peerA, seedB, 4, "client A f4");
            AssertSame(host, peerB, seedB, 4, "late client B f4");
            Assert.That(host.BuiltLayoutHash, Is.EqualTo(CellarFloorGenerator.Generate(0u, 4).LayoutHash()), "floor 4 is the same fixed layout whatever the seed");
            Assert.That(host.Built.Root.Find(CellarFloorBuilder.MarkerGroup).Find("Baker"), Is.Not.Null, "the baker marker is built on floor 4");

            // Back to no floor: all peers clear.
            GameObject[] oldRoots = { host.Built.Root.gameObject, peerA.Built.Root.gameObject, peerB.Built.Root.gameObject };
            Assert.That(host.ServerSetFloor(seedB, 0), Is.True);
            yield return PlayModeHarness.WaitUntil(() => peerA.BuiltFloor == 0 && peerB.BuiltFloor == 0 && host.BuiltFloor == 0, Timeout, "floor 0 never cleared the peers");
            yield return null;   // Destroy takes effect at the end of the frame
            Assert.That(host.Built == null && peerA.Built == null && peerB.Built == null, Is.True, "Built is null on all 3 peers after floor 0");
            Assert.That(oldRoots[0] == null && oldRoots[1] == null && oldRoots[2] == null, Is.True, "the previous floor root is destroyed on all 3 peers");
        }
    }
}
