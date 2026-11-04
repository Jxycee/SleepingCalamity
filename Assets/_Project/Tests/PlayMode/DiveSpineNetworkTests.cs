// Authority: test-only. The dive spine (plan 3 task 7): the host-owned DiveDirector (start with the checkpoint pick, stairs 1-6, the bank floor, the floor-7 ending, go home, fail) over the real CellarFloorSync +
// DiveHaulSync + player prefab. Host + clients in one process (scene management off; the director, the syncs and the heroes are spawned from the owned network prefab list / the app's player prefab).
// Catches: peers building different floors, a stair press that does not advance every peer to the same floor, a stale or out-of-range stair press advancing (or double-advancing) the dive,
// players not teleported to the new entry through their owner (owner-authoritative NetworkTransform), a failed / finished dive that leaves the party on the floor or the dive running, a floor pick the
// checkpoint does not allow being accepted, the checkpoint or found pieces lost between dives, a lost satchel at the wrong depth, a late joiner missing the floor and phase.
// NOT covered by a one-process test: the interact KEY and its prompt text on screen (the tests call the same Request* wrappers the key calls), the greybox look, real relay latency, the bank room's picks and
// confirm window (plan 3 task 8: the floor-5 bank press here is the stand-in the haul sync still has), a client rejoin to a dive in progress.
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class DiveSpineNetworkTests
    {
        private const float Timeout = 25f;
        private static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm;
            public readonly List<PlayModeHarness.TestClient> Clients = new List<PlayModeHarness.TestClient>();
            public DiveDirector Host; public readonly List<DiveDirector> Peers = new List<DiveDirector>();
            public NetworkObject HostHero; public readonly List<NetworkObject> HeroesOnHost = new List<NetworkObject>(), OwnHeroes = new List<NetworkObject>();
            public CellarFloorSync HostFloor; public DiveHaulSync HostHaul;
            public readonly List<CellarFloorSync> PeerFloors = new List<CellarFloorSync>();
            public readonly List<DiveHaulSync> PeerHauls = new List<DiveHaulSync>();
        }

        // Host + `clientCount` clients, the director spawned and configured, one hero each placed at the hub.
        private IEnumerator Start(int clientCount, System.Action<Rig> done, bool withHeroes = true)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            var rig = new Rig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            for (int i = 0; i < clientCount; i++)
            {
                var client = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(8 + i));
                _clients.Add(client); rig.Clients.Add(client);
                var cts = new CancellationTokenSource();
                yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
                cts.Dispose();
                yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "client " + i + " never connected");
            }
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(Home, HubStair, Origin), Is.True);
            foreach (var c in rig.Clients)
            {
                yield return PlayModeHarness.WaitUntil(() => c.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "a client never received the director");
                rig.Peers.Add(c.NetworkManager.SpawnManager.SpawnedObjects[dirObj.NetworkObjectId].GetComponent<DiveDirector>());
            }
            if (withHeroes)
            {
                rig.HostHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, Home, Quaternion.identity);
                for (int i = 0; i < clientCount; i++)
                {
                    NetworkManager cnm = rig.Clients[i].NetworkManager;
                    rig.HeroesOnHost.Add(rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, cnm.LocalClientId, false, true, false, Home + new Vector3(2f * (i + 1), 0f, 0f), Quaternion.identity));
                    yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null, Timeout, "client " + i + " never received its hero");
                    rig.OwnHeroes.Add(cnm.LocalClient.PlayerObject);
                }
                yield return Seconds(0.5f);
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

        // After the dive's syncs exist on the host: find the clients' copies by network id.
        private static IEnumerator ResolvePeers(Rig rig)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync != null && rig.Host.HaulSync != null, Timeout, "the dive never spawned its syncs");
            rig.HostFloor = rig.Host.FloorSync; rig.HostHaul = rig.Host.HaulSync;
            foreach (var c in rig.Clients)
            {
                ulong fid = rig.HostFloor.NetworkObjectId, hid = rig.HostHaul.NetworkObjectId;
                yield return PlayModeHarness.WaitUntil(() => c.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(fid) && c.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(hid), Timeout, "a client never received the dive syncs");
                rig.PeerFloors.Add(c.NetworkManager.SpawnManager.SpawnedObjects[fid].GetComponent<CellarFloorSync>());
                rig.PeerHauls.Add(c.NetworkManager.SpawnManager.SpawnedObjects[hid].GetComponent<DiveHaulSync>());
            }
        }

        private static IEnumerator AllBuilt(Rig rig, int floor)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == floor && rig.HostFloor.Built != null, Timeout, "the host never built floor " + floor);
            foreach (CellarFloorSync p in rig.PeerFloors)
            {
                CellarFloorSync peer = p;
                yield return PlayModeHarness.WaitUntil(() => peer.BuiltFloor == floor && peer.Built != null, Timeout, "a client never built floor " + floor);
                Assert.That(peer.BuiltLayoutHash, Is.EqualTo(rig.HostFloor.BuiltLayoutHash), "every peer builds the identical floor " + floor);
                Assert.That(peer.BuiltLayoutHash, Is.Not.EqualTo(0UL));
            }
        }

        private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        // Owner-side move (the stand-in for walking): switch the CharacterController off while writing the pose, as the director's own teleport does.
        private static void Move(NetworkObject ownerCopy, Vector3 pos)
        {
            var cc = ownerCopy.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            ownerCopy.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        private static IEnumerator NearOnHost(Rig rig, Vector3 where, string what)
        {
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, where) < 6f, Timeout, what + ": the host hero is at " + rig.HostHero.transform.position + ", wanted near " + where);
            foreach (NetworkObject h in rig.HeroesOnHost)
            {
                NetworkObject hero = h;
                yield return PlayModeHarness.WaitUntil(() => Flat(hero.transform.position, where) < 6f, Timeout, what + ": a client hero is at " + hero.transform.position + " on the host, wanted near " + where);
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Floor1_HostAndTwoClients_BuildTheIdenticalAuthoredFloor_WithItsMarkers()
        {
            Rig rig = null;
            yield return Start(2, r => rig = r, withHeroes: false);
            rig.Host.SeedOverride = 424242u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(1));
            Assert.That(rig.HostFloor.BuiltSeed, Is.EqualTo(424242u));
            foreach (CellarFloorSync p in rig.PeerFloors) Assert.That(p.BuiltSeed, Is.EqualTo(424242u));
            // Every peer drew the dive markers on its own build (floor 1 has a stair and no bank).
            Assert.That(rig.HostFloor.Built.Root.Find(CellarFloorBuilder.DiveMarkerGroup + "/StairColumn"), Is.Not.Null);
            Assert.That(rig.HostFloor.Built.Root.Find(CellarFloorBuilder.DiveMarkerGroup + "/BankColumn"), Is.Null);
            Assert.That(rig.Peers[0].NetActive.Value, Is.True);
        }

        // Walks the host hero up the stairs from the session's floor to `floor` (host rule, gate key off; floor 5 is cleared and pushed on through the haul sync's bank stand-in).
        private static IEnumerator ToFloor(Rig rig, int floor)
        {
            ulong hostId = rig.HostNm.LocalClientId;
            rig.Host.RequireGateKey = false;
            while (rig.Host.Session.Floor < floor)
            {
                int f = rig.Host.Session.Floor;
                yield return AllBuilt(rig, f);
                yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-" + f + " entry");
                if (f == UndercroftPlan.BankFloor)
                {
                    Assert.That(rig.HostHaul.ServerMarkCleared(f), Is.True, "the guardian's floor is cleared");
                    Assert.That(rig.HostHaul.ServerBank(), Is.True, "the first bank press enters the bank room");
                    Assert.That(rig.HostHaul.ServerBank(), Is.True, "the second pushes on");
                }
                Assert.That(CellarFloorBuilder.TryDescentWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair), Is.True, "floor " + f + " has a way down");
                Move(rig.HostHero, stair + new Vector3(1f, 0.1f, 0f));
                yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, stair) < 3f, Timeout, "the host hero never reached the way down of floor " + f);
                Assert.That(rig.Host.ServerTryStair(hostId, f), Is.True, "the stair from floor " + f);
            }
            yield return AllBuilt(rig, floor);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-" + floor + " entry");
            yield return Seconds(0.5f);   // the arrival teleports must all have landed before the test moves a hero
        }

        [UnityTest]
        public IEnumerator Spine_Floors1To7_StairsInOrder_FinishBringsHaulHome()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r);
            rig.Host.SeedOverride = 12345u;
            ulong hostId = rig.HostNm.LocalClientId;
            DiveEndReason? ended = null; rig.Host.DiveEnded += r => ended = r;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            Assert.That(rig.HostHaul.Current.DivePhase, Is.EqualTo(DivePhase.Floor));
            // Each clear feeds the stir; the stair then takes the party one floor down.
            for (int f = 1; f <= 4; f++)
            {
                Assert.That(rig.HostHaul.ServerMarkCleared(f), Is.True, "floor " + f + " clears");
                yield return ToFloor(rig, f + 1);
                Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(f + 1));
            }
            Assert.That(rig.HostHaul.Current.ClearedMask, Is.EqualTo(0b1111), "floors 1-4 are cleared");
            Assert.That(rig.Host.ServerTryStair(hostId, 5), Is.False, "floor 5 has no way down before the bank choice");
            Assert.That(rig.HostHaul.ServerEnterFloor(6), Is.False, "and the session refuses it too");
            // Something to carry home: a loot item in the host's bag.
            Assert.That(rig.HostHaul.ServerAddItem(hostId, CellarLoot.Items[0].Id), Is.True);
            yield return ToFloor(rig, 6);   // clears floor 5 (its guardian), enters the bank room, pushes on, takes the stair
            Assert.That(rig.HostHaul.Current.BankChosen, Is.True);
            Assert.That((rig.HostHaul.Current.ClearedMask & 0b10000) != 0, Is.True, "floor 5 was cleared before the bank room");
            Assert.That(rig.HostHaul.ServerMarkCleared(6), Is.True);
            yield return ToFloor(rig, 7);
            Assert.That(rig.Host.ServerTryStair(hostId, 7), Is.False, "floor 7 has no stair: it ends through its end door");
            Assert.That(rig.HostFloor.Built.Root.Find(CellarFloorBuilder.DiveMarkerGroup + "/EndColumn"), Is.Not.Null, "the end door is marked");
            Assert.That(rig.Host.ServerTryFinish(hostId), Is.False, "the hero stands at the entry, far from the end door");
            Assert.That(CellarFloorBuilder.TryEndWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 end), Is.True);
            Move(rig.HostHero, end + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, end) < 3f, Timeout, "the host hero never reached the end door");
            Assert.That(rig.Host.ServerTryFinish(hostId), Is.True, "the finish");
            Assert.That(ended, Is.EqualTo(DiveEndReason.Finished));
            Assert.That(rig.Host.NetActive.Value, Is.False);
            Assert.That(rig.Host.Checkpoint.Held, Is.False, "finishing is not a trip home from the bank: no checkpoint");
            Assert.That(rig.HostHaul.ServerBagOf(hostId).Carried, Is.Empty, "everything carried came home");
            Assert.That(rig.HostHaul.ServerBagOf(hostId).Stash.ContainsKey(CellarLoot.Items[0].Id), Is.True, "and sits in the stash");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CurrentFloor == 0 && rig.HostHaul.Current.DivePhase == DivePhase.Town, Timeout, "the summary never went home");
            yield return NearOnHost(rig, Home, "home after the ending");
        }

        [UnityTest]
        public IEnumerator Stair_ClientRequestAdvancesEveryPeer_RangeAndStaleRequestsRefused_PlayersMoveToTheNewEntry()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 12345u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1));
            Assert.That(DiveDirector.Instance, Is.SameAs(rig.Host), "the host's director is the static Instance");
            Assert.That(rig.HostFloor.Kit.PieceList.Count, Is.GreaterThan(0), "the CellarFloorSync prefab carries the dressing kit");
            Assert.That(rig.HostFloor.Built.Vaults, Is.GreaterThan(0), "the art layer built vaults");
            DiveDirector peer = rig.Peers[0];
            Assert.That(peer.ServerStartDive(), Is.False, "a client cannot start a dive");
            Assert.That(peer.ServerTryStair(rig.Clients[0].NetworkManager.LocalClientId, 1), Is.False, "a client cannot run the host rule");

            // Both heroes were sent to the floor-1 entry through their owners.
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-1 entry");
            Assert.That(Flat(rig.OwnHeroes[0].transform.position, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 1)), Is.LessThan(6f), "the client's own copy was moved");

            // Out of range: the host refuses the client's stair press (the stair is not at the entry).
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair1), Is.True);
            Assert.That(Flat(rig.HeroesOnHost[0].transform.position, stair1), Is.GreaterThan(DiveHaulSync.BankRadius), "test premise: the stair is far from the entry");
            int handled = rig.Host.RequestsHandled;
            peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the far stair press never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1), "out of range: no advance");

            // Gate key required: refused even in range until ServerOpenStair(1) (the contents session's API); opening is idempotent.
            Move(rig.OwnHeroes[0], stair1 + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, stair1) < 3f, Timeout, "the client hero never arrived at the stair on the host");
            rig.Host.RequireGateKey = true;
            handled = rig.Host.RequestsHandled;
            peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the shut stair press never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1), "RequireGateKey keeps it shut until the stair is opened");
            rig.Host.ServerOpenStair(1); rig.Host.ServerOpenStair(1);

            // In range and open: the client's press takes everyone down.
            peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CurrentFloor == 2, Timeout, "the host never advanced to floor 2");
            Assert.That((rig.HostHaul.Current.ClearedMask & 1) != 0, Is.True, "floor 1 was marked cleared (feeds the stir meter)");
            yield return AllBuilt(rig, 2);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(2));
            Assert.That(rig.PeerHauls[0].Current.CurrentFloor, Is.EqualTo(2));
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-2 entry");

            // A stale press (still naming floor 1) does not skip floor 2, and a double press cannot double-advance.
            handled = rig.Host.RequestsHandled;
            peer.RequestStair(1); peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled >= handled + 2, Timeout, "the stale presses never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(2));
        }

        // Playtest 5: a downed hero cannot take the stair, bank or call the party home (the host checks the asker is up); up again, the same press works.
        [UnityTest]
        public IEnumerator DownedHero_CannotUseTheStair_UntilRevived()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 5150u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-1 entry");
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair), Is.True);
            Move(rig.OwnHeroes[0], stair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, stair) < 3f, Timeout, "the client hero never arrived at the stair on the host");
            rig.Host.RequireGateKey = false;
            ulong clientId = rig.Clients[0].NetworkManager.LocalClientId;
            HeroVitals vitals = rig.HeroesOnHost[0].GetComponent<HeroVitals>();
            vitals.ServerDamage(100000);
            Assert.That(vitals.IsUp, Is.False, "test premise: the client hero is down");
            Assert.That(rig.Host.ServerTryStair(clientId, 1), Is.False, "a downed hero cannot take the stair");
            Assert.That(rig.Host.ServerTryBank(clientId), Is.False, "a downed hero cannot bank");
            Assert.That(rig.Host.ServerTryGoHome(clientId), Is.False, "a downed hero cannot call the party home");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1), "the party stayed on floor 1");
            vitals.ServerFullReset();
            Assert.That(vitals.IsUp, Is.True);
            Assert.That(rig.Host.ServerTryStair(clientId, 1), Is.True, "up again, the same press takes the party down");
        }

        [UnityTest]
        public IEnumerator Fail_AtFloor4_LeavesSatchelAtDepth4_NextDiveStartsFloor1_PiecesStayFound()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 777u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 4);
            Assert.That(rig.HostHaul.ServerAddItem(hostId, CellarLoot.Items[0].Id), Is.True);
            Assert.That(rig.HostHaul.ServerAddPiece(100), Is.True, "a piece found on the way");
            Assert.That(rig.Host.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peers[0].NetActive.Value, Timeout, "the failed dive never ended on every peer");
            PlayerBag bag = rig.HostHaul.ServerBagOf(hostId);
            Assert.That(bag.Satchel.Waiting, Is.Not.Null, "the carried item waits in a satchel");
            Assert.That(bag.Satchel.Waiting.Floor, Is.EqualTo(4), "at the depth the party fell on");
            Assert.That(rig.Host.Pieces.IsFound(100), Is.True, "a found piece is safe at once and stays found");
            Assert.That(rig.Host.Town.State.InStock(100), Is.True, "and it is in the town stock");
            Assert.That(rig.Host.ServerStartDive(3), Is.False, "no checkpoint after a failure: floor 3 is refused");
            Assert.That(rig.Host.ServerStartDive(), Is.True, "the next dive starts at floor 1");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1));
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(1), "the new dive still knows the piece");
        }

        // RPC path: the hub trapdoor's floor pick. Without the checkpoint only floor 1 starts; the host decides, not the asker.
        [UnityTest]
        public IEnumerator StartDive_WithoutCheckpoint_PickingFloor3_IsRefused_WithCheckpoint_Allowed_AndSpendsIt()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 31337u;
            DiveDirector peer = rig.Peers[0];
            Move(rig.OwnHeroes[0], HubStair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, HubStair) < 3f, Timeout, "the client hero never reached the trapdoor on the host");
            int handled = rig.Host.RequestsHandled;
            peer.RequestStartDive(3);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the floor-3 press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.False, "without the checkpoint floor 3 is refused");
            Assert.That(peer.PickableFloors(), Is.EqualTo(new[] { 1 }));

            rig.Host.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => peer.NetCheckpointHeld.Value && peer.NetPickableMaxFloor.Value == UndercroftPlan.BankFloor, Timeout, "the client never saw the checkpoint");
            Assert.That(peer.PickableFloors(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            handled = rig.Host.RequestsHandled;
            peer.RequestStartDive(3);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled && rig.Host.NetActive.Value, Timeout, "the allowed floor-3 press never started the dive");
            Assert.That(rig.Host.Session.Floor, Is.EqualTo(3));
            Assert.That(rig.Host.Checkpoint.Held, Is.False, "starting on a picked floor spends the checkpoint");
            yield return PlayModeHarness.WaitUntil(() => !peer.NetCheckpointHeld.Value && peer.NetPickableMaxFloor.Value == 1, Timeout, "the client never saw the checkpoint spent");
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 3);
            Assert.That(rig.HostFloor.Layout.BakerCell.HasValue, Is.True, "floor 3 is the bakery");
        }

        // Going home from the cleared bank floor sets the checkpoint (decision 31); the bank floor is floor 5.
        [UnityTest]
        public IEnumerator BankFloor_GoHome_HoldsTheCheckpoint_AndFloor5IsPickable_StartingThereSpendsIt()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r);
            rig.Host.SeedOverride = 99u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 5);
            Assert.That(rig.Host.ServerTryGoHome(hostId), Is.False, "the guardian is not down: no way home yet");
            Assert.That(rig.HostHaul.ServerMarkCleared(5), Is.True);
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True);
            Move(rig.HostHero, bank + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank) < 3f, Timeout, "the host hero never reached the bank room");
            Assert.That(rig.Host.ServerTryGoHome(hostId), Is.True);
            Assert.That(rig.Host.NetActive.Value, Is.False);
            Assert.That(rig.Host.Checkpoint.Held, Is.True, "going home from the bank holds the one-time checkpoint");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetCheckpointHeld.Value && rig.Host.NetPickableMaxFloor.Value == 5, Timeout, "the hub never offered the pick");
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "the floor was never cleared");
            Assert.That(rig.Host.ServerStartDive(5), Is.True, "start at the bank (decision 2)");
            Assert.That(rig.Host.Session.StartedAtBank, Is.True);
            Assert.That(rig.Host.Checkpoint.Held, Is.False, "starting on a picked floor spends it");
            yield return AllBuilt(rig, 5);
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank2), Is.True);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank2) < 4f, Timeout, "the party did not arrive at the bank room");
        }

        // The baker is a person on floor 3: walking onto him finds him, he is safe and in the town stock at once.
        [UnityTest]
        public IEnumerator Floor3_Baker_IsFoundByWalkingOnHim_AndComesHomeAtOnce()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r);
            rig.Host.SeedOverride = 4040u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 3);
            Assert.That(rig.HostHaul.Current.PeopleLeft, Is.EqualTo(1), "the baker is still on floor 3");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.False);
            CellPos baker = rig.HostFloor.Layout.BakerCell.Value;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ContentsSync != null && rig.Host.ContentsSync.State.Floor == 3 && rig.Host.ContentsSync.PieceCount == 1, Timeout, "the baker's spot never appeared");
            Move(rig.HostHero, rig.HostFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(baker.X, baker.Y)) + new Vector3(0f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => rig.Host.Pieces.IsFound(PieceCatalog.BakerId), Timeout, "walking onto the baker never found him");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.True, "he is in the town stock at once");
            Assert.That(rig.HostHaul.Current.PeopleLeft, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DiveInProgress_LateJoiner_GetsFloorAndPhase_NoNotice()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r);
            rig.Host.SeedOverride = 2468u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 2);
            Assert.That(rig.HostHaul.ServerAddPiece(101), Is.True);
            var late = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(9));
            _clients.Add(late);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(late.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => late.NetworkManager.IsConnectedClient, Timeout, "the late client never connected");
            ulong hid = rig.HostHaul.NetworkObjectId, did = rig.Host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => late.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(hid) && late.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(did), Timeout, "the late client never received the dive");
            DiveHaulSync peerHaul = late.NetworkManager.SpawnManager.SpawnedObjects[hid].GetComponent<DiveHaulSync>();
            DiveDirector peerDirector = late.NetworkManager.SpawnManager.SpawnedObjects[did].GetComponent<DiveDirector>();
            yield return PlayModeHarness.WaitUntil(() => peerHaul.Current.CurrentFloor == 2 && peerHaul.Current.DivePhase == DivePhase.Floor && peerHaul.Current.FoundPieces == 1 && peerDirector.NetActive.Value, Timeout, "the late joiner never saw the floor, the phase and the found piece");
            Assert.That(peerHaul.Current.ClearedMask, Is.EqualTo(1), "floor 1 was cleared on the way");
            yield return Seconds(0.5f);
            Assert.That(peerDirector.Notice, Is.Null, "joining a running dive replays no notice");
        }

        [UnityTest]
        public IEnumerator HubPrompt_WithCheckpoint_ListsPickableFloors()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r);
            Move(rig.HostHero, HubStair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => rig.Host.Prompt == DiveDirector.PromptKind.Descend, Timeout, "the hub prompt never appeared at the trapdoor");
            Assert.That(rig.Host.PromptText, Is.EqualTo("Go down the Undercroft"), "no checkpoint: one floor, no list");
            rig.Host.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetCheckpointHeld.Value && rig.Host.PromptText.Contains("pickable: 1, 2, 3, 4, 5"), Timeout, "the prompt never listed the pickable floors: " + rig.Host.PromptText);
            Assert.That(rig.Host.PromptText, Does.Contain("start on floor 1"));
            Assert.That(DiveDirector.HubPromptText(true, rig.Host.PickableFloors(), 3), Does.Contain("start on floor 3"));
        }

        // Wipe: every hero is really downed through HeroVitals (the damage path), mid-dive. The dive must fail, the party must be home, and every hero must be up again.
        [UnityTest]
        public IEnumerator PartyWipe_AllHeroesDowned_FailsTheDive_SendsEveryoneHome_AndResetsTheHeroes()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 4242u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-1 entry");
            var hostVitals = rig.HostHero.GetComponent<HeroVitals>(); var clientVitals = rig.HeroesOnHost[0].GetComponent<HeroVitals>();
            Assert.That(hostVitals.IsUp && clientVitals.IsUp, Is.True, "test premise: both heroes start up");

            // One hero down is not a wipe.
            hostVitals.ServerDamage(100000);
            Assert.That(hostVitals.IsUp, Is.False);
            yield return Seconds(1f);
            Assert.That(rig.Host.NetActive.Value, Is.True, "one hero still stands: the dive goes on");

            clientVitals.ServerDamage(100000);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value, Timeout, "every hero down never failed the dive");
            yield return PlayModeHarness.WaitUntil(() => !rig.Peers[0].NetActive.Value, Timeout, "the client never saw the dive end");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(0), "the wiped dive failed the haul");
            yield return NearOnHost(rig, Home, "home after the wipe");
            yield return PlayModeHarness.WaitUntil(() => hostVitals.IsUp && clientVitals.IsUp, Timeout, "the heroes stayed down after the dive ended");
            Assert.That(hostVitals.Hp, Is.EqualTo(hostVitals.MaxHp), "a wiped hero arrives home at full health");

            // A new dive after the wipe starts with a standing party and is not failed at once.
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return AllBuilt(rig, 1);
            yield return Seconds(1f);
            Assert.That(rig.Host.NetActive.Value, Is.True, "the next dive survives its first second");
        }

        // Contents hook: the director spawns CellarContentsSync, arms it per built floor, opens the stair on the gate key (ExitUnlocked) and clears the enemies when the dive fails.
        [UnityTest]
        public IEnumerator Contents_ArmedByTheDirector_StairNeedsTheGateKey_FailedDiveClearsEnemies()
        {
            uint seed = 0;
            for (uint sd = 1; sd < 400 && seed == 0; sd++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(sd, 2);   // floor 1 is the authored teaching floor: no locked room, no carrier
                if (l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0) seed = sd;
            }
            Assert.That(seed, Is.Not.EqualTo(0u), "a seed with a carrier");
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = seed;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 2);   // gate key off for the walk down
            rig.Host.RequireGateKey = true;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ContentsSync != null && rig.Host.ContentsSync.State.Seed == seed && rig.Host.ContentsSync.State.Floor == 2, Timeout, "the director never armed the contents for floor 2");
            CellarContentsSync contents = rig.Host.ContentsSync;
            Assert.That(rig.Host.RequireGateKey, Is.True, "contents gate the stair");
            Assert.That(contents.AutoArm, Is.False, "the director arms the contents, not their own build watcher");
            Assert.That(contents.State.ExitUnlocked, Is.False);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-2 entry");

            // Shut: in range of the stair, the press is refused until the key is taken.
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair), Is.True);
            DiveDirector peer = rig.Peers[0];
            GateKeyCarrier car = rig.HostFloor.Layout.Carrier;
            Vector3 carrierCell = rig.HostFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(car.X, car.Y));
            Move(rig.OwnHeroes[0], carrierCell);
            yield return PlayModeHarness.WaitUntil(() => contents.GateKeyCarrierEnemy != null, Timeout, "entering the carrier's room never spawned the carrier");
            var hit = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = 100000f, PartIndex = -1, CritChance = 0f };
            Enemy carrier = contents.GateKeyCarrierEnemy;
            // The hero steps back to the stair before the kill: a carrier that chased the hero would otherwise die inside GateKeyRange and the key would be picked up at once (the stair would not stay shut).
            Move(rig.OwnHeroes[0], stair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, stair) < 3f, Timeout, "the client hero never reached the stair");
            for (int i = 0; i < 6 && carrier.Alive; i++) carrier.ServerApplyHit(hit, carrier.transform.position + Vector3.back, Time.timeAsDouble);
            yield return PlayModeHarness.WaitUntil(() => contents.State.GateKey != 0, Timeout, "the carrier never dropped the gate key");
            Assert.That(Flat(contents.State.GateKeyPos, stair), Is.GreaterThan(CellarContentsSync.GateKeyRange + 1f), "the key lies out of reach of the hero at the stair, so the exit stays shut");
            Assert.That(contents.State.GateKey, Is.EqualTo(1), "the key waits on the floor");
            int handled = rig.Host.RequestsHandled;
            peer.RequestStair(2);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the shut stair press never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(2), "the gate key is not taken yet: the stair stays shut");

            // The key is picked up: ExitUnlocked opens the stair through the director.
            Move(rig.OwnHeroes[0], contents.State.GateKeyPos + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => contents.State.ExitUnlocked, Timeout, "picking the key up never unlocked the exit");
            Move(rig.OwnHeroes[0], stair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, stair) < 3f, Timeout, "the client hero never got back to the stair");
            peer.RequestStair(2);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CurrentFloor == 3, Timeout, "the opened stair never took the party down");
            yield return PlayModeHarness.WaitUntil(() => contents.State.Floor == 3 && contents.State.Seed == rig.HostFloor.BuiltSeed, Timeout, "the contents were never re-armed for floor 3");

            // A failed dive clears the contents (and their enemies).
            Assert.That(rig.Host.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => contents.State.Floor == 0, Timeout, "the failed dive never cleared the contents");
            int enemies = 0;
            foreach (NetworkObject o in rig.HostNm.SpawnManager.SpawnedObjects.Values) { Enemy en = o.GetComponent<Enemy>(); if (en != null && en.Alive) enemies++; }   // the killed carrier's corpse may linger until its own despawn timer
            Assert.That(enemies, Is.EqualTo(0), "no living enemy survives a failed dive");
        }
    }
}
