// Authority: test-only. The dive spine (plan 3 task 7): the host-owned DiveDirector (start with the checkpoint pick, stairs 1-6, the bank floor, the floor-7 ending, go home, fail) over the real CellarFloorSync +
// DiveHaulSync + player prefab. Host + clients in one process (scene management off; the director, the syncs and the heroes are spawned from the owned network prefab list / the app's player prefab).
// Catches: peers building different floors, a stair press that does not advance every peer to the same floor, a stale or out-of-range stair press advancing (or double-advancing) the dive,
// players not teleported to the new entry through their owner (owner-authoritative NetworkTransform), a failed / finished dive that leaves the party on the floor or the dive running, a floor pick the
// checkpoint does not allow being accepted, the checkpoint or found pieces lost between dives, a lost satchel at the wrong depth, a late joiner missing the floor and phase.
// NOT covered by a one-process test: the interact KEY and its prompt text on screen (the tests call the same Request* wrappers the key calls), the greybox look, real relay latency, the bank screen
// (IMGUI), a client rejoin to a dive in progress or to a resumed suspend (the manual 2-PC walk). The bank room's own rules are DiveHaulNetworkTests and PerPlayerLootNetworkTests.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
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

        // Walks the host hero up the stairs from the session's floor to `floor` (host rule, gate key off; floor 5 is cleared, the bank room entered and every player's push-on pressed).
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
                    Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True, "floor 5 has a bank room");
                    Move(rig.HostHero, bank + new Vector3(0.5f, 0.1f, 0f));
                    foreach (NetworkObject own in rig.OwnHeroes) Move(own, bank + new Vector3(0.5f, 0.1f, 0.8f));
                    yield return NearOnHost(rig, bank, "the party in the bank room");   // the host checks the asker stands at the bank
                    Assert.That(rig.HostHaul.ServerEnterBankRoom(), Is.True, "the bank press enters the bank room");
                    Assert.That(rig.HostHaul.PushOnNow(), Is.True, "every present player's push-on press confirms the choice");
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

        // ================================================================================================================================
        // Plan 3 task 8 / 8a: the bank room through the director (capture, go home, extra-slot owner across a restart, resume of a loaded suspend).
        // Catches: a quit in the bank room not writing the suspend (or writing it as a failed dive's satchel), the push-on leaving a suspend that the next floor must wipe, the auto-fill of empty slots, the extra
        // slot owner repeating across dives and across a restart, the checkpoint not held after a go home, a loaded suspend resuming on the wrong floor or phase, losing its stir / momentum / bags, or spending
        // the checkpoint, a used suspend staying in the next capture. They jump the session to the bank floor with DiveTestAdapters (the floors 1-4 walk is the spine test) and call the same host methods the
        // RPCs call. NOT covered: a client rejoining the resumed dive (manual 2-PC walk), the bank screen (IMGUI), real disk I/O (SuspendSaveNetworkTests).
        // ================================================================================================================================

        private static PlayerKey KeyOf(Rig rig, ulong clientId)
        {
            Assert.That(rig.App.Roster.TryGetKey(clientId, out PlayerKey key), Is.True, "premise: the client is in the host's roster");
            return key;
        }

        private static TownSnapshot AsLoaded(TownSnapshot captured)
            => TownSaveRules.CleanSnapshot(TownSaveRules.CleanSnapshot(captured, null, null, false), null, null, true);   // the file's clean, then the load's (the failed-dive rule applies)

        [UnityTest]
        public IEnumerator BankRoom_PushOn_LocksBank_SavesSuspend_AutoFillGone_StairWipesIt()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r, withHeroes: false);
            rig.Host.SeedOverride = 5252u;
            ulong hostId = rig.HostNm.LocalClientId;
            PlayerKey hostKey = KeyOf(rig, hostId);
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            DiveHaulSync haul = rig.HostHaul;
            Assert.That(haul.ServerAddItem(hostId, 104) && haul.ServerAddItem(hostId, 105) && haul.ServerAddItem(hostId, 110), Is.True);
            int pushed = 0; haul.PushedOn += () => pushed++;

            TownSnapshot below = rig.Host.CaptureTown();
            Assert.That(below.SuspendKnown, Is.True, "every director capture says what it knows about the suspend");
            Assert.That(below.Suspend, Is.Null, "a dive on floor 1 is not a suspend");

            Assert.That(haul.BankRoomNow(), Is.True);
            TownSnapshot inRoom = rig.Host.CaptureTown();
            Assert.That(inRoom.Suspend, Is.Not.Null, "quitting in the bank room is a normal save-and-quit");
            Assert.That(inRoom.Suspend.Phase, Is.EqualTo((int)DivePhase.BankRoom));
            Assert.That(inRoom.Suspend.BankChosen, Is.False, "push-on not chosen yet");
            Assert.That(inRoom.Suspend.Seed, Is.EqualTo(5252u));
            Assert.That(inRoom.Dive.InDive, Is.True);
            Assert.That(inRoom.Suspend.Bags.Single(b => b.Id == hostKey.Id).Carried, Is.EqualTo(new[] { 104, 105, 110 }), "carried, not a failed dive's satchel");
            Assert.That(inRoom.Bags == null || inRoom.Bags.Players.All(p => p.Satchel == null), Is.True);

            Assert.That(haul.ServerPick(hostId, 2), Is.True);
            Assert.That(haul.ServerBagOf(hostId).SlotCapacity, Is.EqualTo(3), "solo: three slots");
            Assert.That(haul.ServerChoosePushOn(hostId), Is.True, "solo: the first press confirms");
            Assert.That(pushed, Is.EqualTo(1));
            Assert.That(haul.Current.BankChosen, Is.True, "the bank is locked");
            PlayerBag bag = haul.ServerBagOf(hostId);
            Assert.That(bag.BankedThisDive.Keys, Is.EquivalentTo(new[] { 110 }), "only the pick is banked");
            Assert.That(bag.Carried, Is.EqualTo(new[] { 104, 105 }), "the two empty slots stayed empty: nothing was picked for the player");
            Assert.That(haul.ServerEnterBankRoom(), Is.False);

            TownSnapshot afterPush = rig.Host.CaptureTown();
            Assert.That(afterPush.Suspend, Is.Not.Null, "push-on writes the suspend section");
            Assert.That(afterPush.Suspend.BankChosen, Is.True);
            Assert.That(afterPush.Suspend.Phase, Is.EqualTo((int)DivePhase.Floor));
            Assert.That(afterPush.Suspend.Floor, Is.EqualTo(5));
            Assert.That(afterPush.Suspend.Bags.Single().Carried, Is.EqualTo(new[] { 104, 105 }), "bags saved as carried");
            Assert.That(afterPush.Suspend.Bags.Single().BankedThisDive.Single().ItemId, Is.EqualTo(110));

            // the party takes the stair: the suspend must not outlive the bank, or a later mid-dive quit would resume from it
            Assert.That(haul.ServerEnterFloor(6), Is.True);
            TownSnapshot deeper = rig.Host.CaptureTown();
            Assert.That(deeper.SuspendKnown, Is.True);
            Assert.That(deeper.Suspend, Is.Null, "known, none: the stair wipes it");
            Assert.That(deeper.Dive.InDive, Is.True, "a quit on floor 6 is the old failed-dive rule");
            TownSnapshot merged = TownSaveRules.MergeLive(deeper, afterPush);
            Assert.That(TownSaveRules.CleanSnapshot(merged, null, null, false).Suspend, Is.Null, "and merged over the older file copy it stays gone");
        }

        [UnityTest]
        public IEnumerator BankRoom_GoHome_BringsEverythingHome_SetsCheckpoint_NextDiveStartsAtPick()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r, withHeroes: false);
            rig.Host.SeedOverride = 6363u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            DiveHaulSync haul = rig.HostHaul;
            Assert.That(haul.ServerAddItem(hostId, 104) && haul.ServerAddItem(hostId, 110), Is.True);
            Assert.That(haul.ServerAddGold(hostId, 12), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            Assert.That(haul.ServerPick(hostId, 0), Is.True);
            DiveEndReason? ended = null; rig.Host.DiveEnded += r => ended = r;
            Assert.That(haul.ServerChooseGoHome(hostId), Is.True, "solo: the first press confirms and the dive ends");
            Assert.That(ended, Is.EqualTo(DiveEndReason.WentHome));
            Assert.That(rig.Host.NetActive.Value, Is.False);
            PlayerBag bag = haul.ServerBagOf(hostId);
            Assert.That(bag.Carried, Is.Empty);
            Assert.That(bag.Stash.Keys, Is.EquivalentTo(new[] { 104, 110 }), "everything came home, picked or not");
            Assert.That(bag.Gold, Is.EqualTo(12), "dive gold is kept in full");
            Assert.That(rig.Host.Checkpoint.Held, Is.True, "going home from the bank holds the one-time checkpoint");
            Assert.That(rig.Host.CaptureTown().Suspend, Is.Null, "a finished dive leaves no suspend");
            rig.Host.SeedOverride = 6364u;
            Assert.That(rig.Host.ServerStartDive(3), Is.True, "the next dive may start on the picked floor");
            Assert.That(rig.Host.Session.Floor, Is.EqualTo(3));
            Assert.That(rig.Host.Checkpoint.Held, Is.False, "and spends the checkpoint");
        }

        // ---------------------------------------------------------------------------------------------------------------------------------
        // The bank screen driven by REAL input (a non-development build has no dev keys): a FakeInputService replaces the app's input, Frame is routed through the real router and the director's own
        // LocalBankScreen reads Intent. Host only (one process shares one ServiceRoots.App input, so a client rig would make every peer read the same press). The dev keys are never touched.
        // Catches: the bank screen reading only dev keys, a button wired to the wrong request (pick / push on / go home), the cursor repeating while the stick is held or stepping on a stick held on entry,
        // Interact pushing a party on, the hero still walking / swinging while the screen is open (the BankScreen context), the context not handed back when the room closes, a host that stops accepting
        // the owner's requests. NOT covered (one process, host owner): the client-owner RPC path of a remote peer, real WasPressedThisFrame edges and the gamepad bindings, binding display strings and the
        // IMGUI draw (the manual 2-PC walk).
        // ---------------------------------------------------------------------------------------------------------------------------------

        private static IEnumerator Tap(FakeInputService input, PlayerInputFrame down)
        {
            input.Frame = down; yield return null;      // the director's next Update sees the press once
            input.Frame = default; yield return null;
        }

        // atRealBank: the dive starts on the built floor 5 beside its real bank cell (checkpoint start, the real range rules apply to every request, including Go home); otherwise the haul session
        // is jumped to the bank floor on floor 1's build (host rules for picks and push-on only).
        // The built floor's walkable room cell farthest from the bank (a spot a hero can stand on, well outside the bank screen's range).
        private static Vector3 FarthestFloorSpot(Rig rig, Vector3 bank)
        {
            CellarFloorLayout l = rig.HostFloor.Layout; Vector3 best = bank; float bestD = -1f;
            foreach (PlacedRoom p in l.Rooms)
                for (int lx = 0; lx < p.Width; lx++)
                    for (int ly = 0; ly < p.Height; ly++)
                    {
                        if (p.Room.CellAt(lx, ly) != CellKind.Floor) continue;
                        Vector3 w = rig.HostFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(p.X + lx, p.Y + ly)) + new Vector3(0f, 0.1f, 0f);
                        float d = Flat(w, bank);
                        if (d > bestD) { bestD = d; best = w; }
                    }
            Assert.That(bestD, Is.GreaterThan(DiveDirector.BankPromptRadius + 3f), "premise: the floor has a room cell well away from the bank");
            return best;
        }

        // clientCount > 0 (atRealBank only): that many client heroes join the party at the bank; clientsAway moves every client hero to the farthest room cell before the room opens.
        // ONE input is shared by every peer's director in this process, so a single Tap is read by the host's and by each client's director alike (each asks for its own hero).
        private IEnumerator OpenBankScreenOnRealInput(System.Action<Rig, FakeInputService, DiveHaulSync> done, float heldMoveY = 0f, bool atRealBank = false, int clientCount = 0, bool clientsAway = false)
        {
            Rig rig = null;
            yield return Start(clientCount, r => rig = r, withHeroes: true);
            rig.Host.SeedOverride = 7171u;
            ulong hostId = rig.HostNm.LocalClientId;
            var input = new FakeInputService();
            ServiceRoots.App.Replace<IInputService>(input);
            if (atRealBank) rig.Host.Checkpoint.Restore(true);
            Assert.That(rig.Host.ServerStartDive(atRealBank ? UndercroftPlan.BankFloor : UndercroftPlan.FirstFloor), Is.True);
            yield return ResolvePeers(rig);
            DiveHaulSync haul = rig.HostHaul;
            if (atRealBank)
            {
                yield return AllBuilt(rig, UndercroftPlan.BankFloor);
                Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True);
                yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank) < 4f, Timeout, "the party did not arrive at the bank room");
                haul.ServerMarkCleared(UndercroftPlan.BankFloor);
                if (clientCount > 0)
                {
                    // a party of two starts spread around the entry, which is not all inside the bank's range: stand everyone at the bank (after the arrival teleports have landed)
                    yield return Seconds(0.5f);
                    Move(rig.HostHero, bank + new Vector3(0.5f, 0.1f, 0f));
                    foreach (NetworkObject own in rig.OwnHeroes) Move(own, bank + new Vector3(0.5f, 0.1f, 0.8f));
                    yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank) < 3f && Flat(rig.HeroesOnHost[0].transform.position, bank) < 3f, Timeout, "the party never stood at the bank");
                }
                if (clientsAway)
                {
                    Vector3 far = FarthestFloorSpot(rig, bank);
                    foreach (NetworkObject own in rig.OwnHeroes) Move(own, far);
                    yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, bank) > DiveDirector.BankPromptRadius + 3f, Timeout, "the client hero never showed far from the bank on the host");
                }
            }
            Assert.That(haul.ServerAddItem(hostId, 104) && haul.ServerAddItem(hostId, 110) && haul.ServerAddItem(hostId, 110), Is.True);
            input.Frame = new PlayerInputFrame { MoveY = heldMoveY };   // a stick already held as the room opens
            Assert.That(atRealBank ? haul.ServerEnterBankRoom() : haul.BankRoomNow(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BankScreenOpen, Timeout, "the bank screen never opened for the host hero");
            yield return PlayModeHarness.WaitUntil(() => { PlayerBagSync b = PlayerBagSync.ForLocalPlayer(rig.HostNm); return b != null && b.View.Carried.Length == 3 && b.View.BankOpen; }, Timeout, "the hero's bag view never showed the three carried items and the open visit");
            Assert.That(input.GameplayContext, Is.EqualTo(InputContext.BankScreen), "the open screen holds the BankScreen context");
            yield return Seconds(0.7f);   // the real buttons arm 0.4 s after the screen opens
            done(rig, input, haul);
        }

        [UnityTest]
        public IEnumerator BankScreen_RealButtons_StepPickUnpickAndPushOn_NoDevKeys_InteractDoesNothing()
        {
            Rig rig = null; FakeInputService input = null; DiveHaulSync haul = null;
            yield return OpenBankScreenOnRealInput((r, i, h) => { rig = r; input = i; haul = h; }, heldMoveY: -1f);
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.BankCursor, Is.EqualTo(0), "a stick held down as the screen opened is not a step");
            input.Frame = default; yield return null; yield return null;

            yield return Tap(input, new PlayerInputFrame { InteractPressed = true });
            yield return Seconds(0.3f);
            Assert.That(haul.Session.Phase, Is.EqualTo(DivePhase.BankRoom), "Interact does nothing in the room");
            Assert.That(haul.Session.BankChosenThisDive, Is.False, "a stray Interact never pushes a solo party on");

            // down: one step per press, and no repeat while the stick stays down
            input.Frame = new PlayerInputFrame { MoveY = -1f };
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(rig.Host.BankCursor, Is.EqualTo(1), "twelve frames of the stick held down step once");
            input.Frame = default; yield return null;
            yield return Tap(input, new PlayerInputFrame { MoveY = 1f });
            Assert.That(rig.Host.BankCursor, Is.EqualTo(0), "up steps back");
            yield return Tap(input, new PlayerInputFrame { MoveY = -1f });
            Assert.That(rig.Host.BankCursor, Is.EqualTo(1));

            // Jump picks the row under the cursor, again unpicks, again picks
            PlayerBag bag = haul.ServerBagOf(hostId);
            yield return Tap(input, new PlayerInputFrame { JumpPressed = true });
            yield return PlayModeHarness.WaitUntil(() => bag.Picks.Count == 1 && bag.Picks[0] == 1, Timeout, "Jump never picked row 1 on the host");
            yield return Tap(input, new PlayerInputFrame { JumpPressed = true });
            yield return PlayModeHarness.WaitUntil(() => bag.Picks.Count == 0, Timeout, "a second Jump never unpicked it");
            yield return Tap(input, new PlayerInputFrame { JumpPressed = true });
            yield return PlayModeHarness.WaitUntil(() => bag.Picks.Count == 1 && bag.Picks[0] == 1, Timeout, "a third Jump never picked it again");

            // Primary pushes on: solo, so the first press confirms; only the pick is banked
            int pushed = 0; haul.PushedOn += () => pushed++;
            yield return Tap(input, new PlayerInputFrame { PrimaryPressed = true });
            yield return PlayModeHarness.WaitUntil(() => haul.Session.BankChosenThisDive, Timeout, "Primary never pushed the party on");
            Assert.That(pushed, Is.EqualTo(1));
            Assert.That(bag.BankedThisDive.Keys, Is.EquivalentTo(new[] { 110 }), "the picked row (the middle 110) is banked; the empty slots stayed empty");
            Assert.That(bag.Carried.Count, Is.EqualTo(2));
            Assert.That(haul.Session.Phase, Is.EqualTo(DivePhase.Floor), "the room closed");

            yield return PlayModeHarness.WaitUntil(() => !rig.Host.BankScreenOpen && input.GameplayContext == InputContext.Free, Timeout, "the screen closed but the BankScreen context was not handed back");
            Assert.That(input.DevPressed, Is.Empty, "this path never used a dev key");
        }

        [UnityTest]
        public IEnumerator BankScreen_RealSecondaryButton_GoesHome_NoDevKeys()
        {
            Rig rig = null; FakeInputService input = null; DiveHaulSync haul = null;
            yield return OpenBankScreenOnRealInput((r, i, h) => { rig = r; input = i; haul = h; }, atRealBank: true);
            DiveEndReason? ended = null; rig.Host.DiveEnded += r => ended = r;
            yield return Tap(input, new PlayerInputFrame { SecondaryPressed = true });
            yield return PlayModeHarness.WaitUntil(() => ended.HasValue, Timeout, "Secondary never took the party home");
            Assert.That(ended, Is.EqualTo(DiveEndReason.WentHome));
            Assert.That(rig.Host.Checkpoint.Held, Is.True, "going home from the bank holds the checkpoint");
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.BankScreenOpen && input.GameplayContext == InputContext.Free, Timeout, "the BankScreen context was not handed back after the dive ended");
            Assert.That(input.DevPressed, Is.Empty);
        }

        // ---------------------------------------------------------------------------------------------------------------------------------
        // Risk-review fixes of the bank screen's real input (co-op freeze, revive, re-arm) and the floor 5 -> 6 way down after a real-input push-on.
        // Catches: a peer far from the bank holding InputContext.BankScreen (its Move zeroed while the host refuses every pick: frozen), a hero standing at the bank not holding it, the context staying held
        // after the hero walks away, a pause menu closing (or a hero standing up) with a press that pushes the party on at once, a downed teammate that nobody can walk to, and the real push-on (Primary)
        // not leaving a stair that opens on floor 5 (so the party sat on floor 5 for good). NOT covered (one process, ONE shared input for every peer's director): per-peer Move zeroing on a client machine
        // (the director flags stand in: BankScreenOpen / BankRoomHint per peer), the owner-side NetworkTransform of a walking client, the IMGUI hint (the manual 2-PC walk).
        // ---------------------------------------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BankScreen_PeerFarFromTheBank_DoesNotHoldTheScreen_AndHoldsItAgainWhenItWalksIn()
        {
            Rig rig = null; FakeInputService input = null; DiveHaulSync haul = null;
            yield return OpenBankScreenOnRealInput((r, i, h) => { rig = r; input = i; haul = h; }, atRealBank: true, clientCount: 1, clientsAway: true);
            DiveDirector peer = rig.Peers[0];
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True);
            Assert.That(rig.Host.BankScreenOpen, Is.True, "the host hero stands at the bank: its screen is open (hint " + rig.Host.BankRoomHint + ", host hero " + Flat(rig.HostHero.transform.position, bank).ToString("0.0") + " m from the bank, client hero "
                + Flat(rig.HeroesOnHost[0].transform.position, bank).ToString("0.0") + " m, client downed " + rig.HeroesOnHost[0].GetComponent<HeroVitals>().IsDowned + ", host down " + !rig.HostHero.GetComponent<HeroVitals>().IsUp + ")");
            Assert.That(input.GameplayContext, Is.EqualTo(InputContext.BankScreen), "and holds the context");
            yield return PlayModeHarness.WaitUntil(() => peer.BankRoomHint, Timeout, "the far client's director never showed the walk-to-the-bank hint");
            Assert.That(peer.BankScreenOpen, Is.False, "a client far from the bank has no screen to arm (the host refuses its picks)");

            // it walks in: now it holds the screen too, and the hint goes
            Move(rig.OwnHeroes[0], bank + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => peer.BankScreenOpen && !peer.BankRoomHint, Timeout, "the client never got its screen on walking into the bank's range");

            // and out again: released (the shared fake input stays BankScreen because the host's director still holds it; the peer's own flag is the proof)
            Move(rig.OwnHeroes[0], FarthestFloorSpot(rig, bank));
            yield return PlayModeHarness.WaitUntil(() => !peer.BankScreenOpen && peer.BankRoomHint, Timeout, "the client kept its screen after walking away from the bank");
            Assert.That(haul.Session.Phase, Is.EqualTo(DivePhase.BankRoom));
        }

        [UnityTest]
        public IEnumerator BankScreen_HeroFarFromTheBank_StaysFree_MoveNotZeroed_PressesSendNothing()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r, withHeroes: true);
            rig.Host.SeedOverride = 7171u;
            ulong hostId = rig.HostNm.LocalClientId;
            var input = new FakeInputService();
            ServiceRoots.App.Replace<IInputService>(input);
            rig.Host.Checkpoint.Restore(true);
            Assert.That(rig.Host.ServerStartDive(UndercroftPlan.BankFloor), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, UndercroftPlan.BankFloor);
            DiveHaulSync haul = rig.HostHaul;
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank) < 4f, Timeout, "the party did not arrive at the bank room");
            haul.ServerMarkCleared(UndercroftPlan.BankFloor);
            Vector3 far = FarthestFloorSpot(rig, bank);
            Move(rig.HostHero, far);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank) > DiveDirector.BankPromptRadius + 3f, Timeout, "the host hero never moved away");
            Assert.That(haul.ServerAddItem(hostId, 104), Is.True);
            Assert.That(haul.ServerEnterBankRoom(), Is.True, "the party enters the room (no teleport: the hero stays where it stands)");

            yield return PlayModeHarness.WaitUntil(() => rig.Host.BankRoomHint, Timeout, "the far hero never got the hint");
            Assert.That(rig.Host.BankScreenOpen, Is.False);
            Assert.That(input.GameplayContext, Is.EqualTo(InputContext.Free), "out of range the context stays Free");
            input.Frame = new PlayerInputFrame { MoveY = 1f, MoveX = 0.5f };
            Assert.That(input.Intent.MoveY, Is.EqualTo(1f), "Move is not zeroed: the hero can walk to the bank");
            Assert.That(input.Intent.MoveX, Is.EqualTo(0.5f));
            yield return Seconds(0.7f);
            Assert.That(input.GameplayContext, Is.EqualTo(InputContext.Free), "still free after the arm delay would have passed");
            input.Frame = default; yield return null;

            // a Primary press out there is an ordinary attack press: it asks for nothing
            int handled = haul.ChoiceRequestsHandled;
            yield return Tap(input, new PlayerInputFrame { PrimaryPressed = true });
            yield return Seconds(0.3f);
            Assert.That(haul.ChoiceRequestsHandled, Is.EqualTo(handled), "no push-on request left the far hero");
            Assert.That(haul.Session.BankChosenThisDive, Is.False);

            // it walks to the bank: the screen holds, its fields arm after the delay
            Move(rig.HostHero, bank + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BankScreenOpen && input.GameplayContext == InputContext.BankScreen, Timeout, "the hero never got the screen at the bank");
            Assert.That(rig.Host.BankRoomHint, Is.False);

            // and away again: released, not frozen
            Move(rig.HostHero, far);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.BankScreenOpen && input.GameplayContext == InputContext.Free, Timeout, "walking away did not hand the context back");
        }

        // A downed teammate: nobody holds the screen (the Move input must be able to walk to them for the revive hold); the room stays open.
        [UnityTest]
        public IEnumerator BankScreen_ADownedTeammate_ReleasesTheScreenSoTheHeroCanWalkToThemToRevive()
        {
            Rig rig = null; FakeInputService input = null;
            yield return OpenBankScreenOnRealInput((r, i, h) => { rig = r; input = i; }, atRealBank: true, clientCount: 1, clientsAway: true);
            Assert.That(rig.Host.BankScreenOpen && input.GameplayContext == InputContext.BankScreen, Is.True, "premise: the host hero holds the screen");
            rig.HeroesOnHost[0].GetComponent<HeroVitals>().ServerDamage(100000);
            Assert.That(rig.HeroesOnHost[0].GetComponent<HeroVitals>().IsDowned, Is.True, "premise: the client hero is down (not yet out)");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BankRoomHint && input.GameplayContext == InputContext.Free, Timeout, "a downed teammate did not release the host hero's screen");
            input.Frame = new PlayerInputFrame { MoveY = 1f, InteractHeld = true };
            Assert.That(input.Intent.MoveY, Is.EqualTo(1f));
            Assert.That(input.Intent.InteractHeld, Is.True, "the revive hold passes");
            Assert.That(rig.HostHaul.Session.Phase, Is.EqualTo(DivePhase.BankRoom), "the room is still open");
        }

        // A pause menu over the screen zeroes the Bank* fields; the press that closes it (and the one just after) must not push on. The arm delay restarts when the fields come back.
        [UnityTest]
        public IEnumerator BankScreen_AfterThePauseMenuCloses_ReArmsBeforeButtonsCount()
        {
            Rig rig = null; FakeInputService input = null; DiveHaulSync haul = null;
            yield return OpenBankScreenOnRealInput((r, i, h) => { rig = r; input = i; haul = h; }, atRealBank: true);   // armed: 0.7 s have passed
            input.MenuOpen = true;
            yield return Seconds(0.3f);
            Assert.That(haul.Session.BankChosenThisDive, Is.False);
            input.MenuOpen = false;                                   // the menu closes ...
            yield return Tap(input, new PlayerInputFrame { PrimaryPressed = true });   // ... with a Primary press in the next frames
            yield return Seconds(0.2f);
            Assert.That(haul.Session.BankChosenThisDive, Is.False, "a press right after the menu closed must not push the party on");
            yield return Seconds(0.5f);
            yield return Tap(input, new PlayerInputFrame { PrimaryPressed = true });
            yield return PlayModeHarness.WaitUntil(() => haul.Session.BankChosenThisDive, Timeout, "after the delay the button works again");
        }

        // Floor 5 -> 6 after a push-on through the real button (no dev key), solo and with two heroes: the bank floor's way down must open (the gate key never applies to it) and the interact
        // press on the stair prompt must take the party to floor 6, once.
        private IEnumerator PushOnThenTakeTheStair(int clientCount)
        {
            Rig rig = null; FakeInputService input = null; DiveHaulSync haul = null;
            yield return OpenBankScreenOnRealInput((r, i, h) => { rig = r; input = i; haul = h; }, atRealBank: true, clientCount: clientCount);
            Assert.That(rig.Host.RequireGateKey, Is.True, "premise: a real dive gates the gate-key stairs (the director arms the contents)");
            Assert.That(haul.Session.BankChosenThisDive, Is.False);
            yield return Tap(input, new PlayerInputFrame { PrimaryPressed = true });   // with two heroes the shared input presses for the host's and the client's director alike
            yield return PlayModeHarness.WaitUntil(() => haul.Session.BankChosenThisDive, Timeout, "Primary never pushed the party on");
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.BankScreenOpen && input.GameplayContext == InputContext.Free, Timeout, "the screen did not hand the context back");
            Assert.That(haul.Session.Floor, Is.EqualTo(UndercroftPlan.BankFloor));
            Assert.That(input.DevPressed, Is.Empty, "no dev key anywhere on this path");

            Assert.That(CellarFloorBuilder.TryDescentWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair), Is.True, "floor 5 has a way down once the choice is made");
            Move(rig.HostHero, stair + new Vector3(1f, 0.1f, 0f));
            foreach (NetworkObject own in rig.OwnHeroes) Move(own, stair + new Vector3(1f, 0.1f, 0.8f));
            yield return NearOnHost(rig, stair, "the party at the floor-5 stair");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.Prompt == DiveDirector.PromptKind.Stair, Timeout, "the stair prompt never showed on floor 5 after the push-on");
            Assert.That(rig.Host.NetStairOpen.Value, Is.True, "the bank floor's stair is not a gate-key stair: it is open after the push-on");
            Assert.That(rig.Host.PromptText, Does.Contain("Take the stair down"));

            yield return Tap(input, new PlayerInputFrame { InteractPressed = true });
            yield return PlayModeHarness.WaitUntil(() => haul.Current.CurrentFloor == 6, Timeout, "the stair press on floor 5 never took the party to floor 6");
            yield return AllBuilt(rig, 6);
            yield return Seconds(1f);
            Assert.That(haul.Current.CurrentFloor, Is.EqualTo(6), "one stair press, one floor");
        }

        [UnityTest]
        public IEnumerator Floor5_RealButtonPushOn_ThenTheStairOpensAndTakesTheSoloPartyToFloor6() { yield return PushOnThenTakeTheStair(0); }

        [UnityTest]
        public IEnumerator Floor5_RealButtonPushOn_ThenTheStairOpensAndTakesTwoHeroesToFloor6() { yield return PushOnThenTakeTheStair(1); }

        [UnityTest]
        public IEnumerator BankRoom_ThreePlayers_ExtraSlotNeverRepeats_AcrossRestart()
        {
            Rig rig = null;
            yield return Start(2, r => rig = r, withHeroes: false);
            var ids = new[] { rig.HostNm.LocalClientId, rig.Clients[0].NetworkManager.LocalClientId, rig.Clients[1].NetworkManager.LocalClientId };
            var owners = new List<int>();
            for (int dive = 0; dive < 3; dive++)
            {
                rig.Host.SeedOverride = 7100u + (uint)dive * 17u;
                Assert.That(rig.Host.ServerStartDive(), Is.True, "dive " + dive);
                if (dive == 0) yield return ResolvePeers(rig);
                DiveHaulSync haul = rig.HostHaul;
                Assert.That(haul.BankRoomNow(), Is.True, "dive " + dive);
                int owner = haul.Session.ExtraSlotOwner;
                Assert.That(owner, Is.InRange(0, 3), "three players: one has the extra slot");
                int twos = 0; foreach (ulong id in ids) if (haul.ServerBagOf(id).SlotCapacity == 2) twos++;
                Assert.That(twos, Is.EqualTo(1));
                if (dive > 0) Assert.That(owner, Is.Not.EqualTo(owners[dive - 1]), "never the previous owner (dive " + dive + (dive == 2 ? ", after the restart" : "") + ")");
                owners.Add(owner);
                foreach (ulong id in ids) haul.ServerChooseGoHome(id);   // two presses confirm; the third is refused (the dive is over)
                Assert.That(rig.Host.NetActive.Value, Is.False, "the party went home");
                if (dive == 1)
                {
                    // the restart: what the host wrote is what the next host loads
                    TownSnapshot saved = rig.Host.CaptureTown();
                    Assert.That(saved.Dive.ExtraSlotOwner, Is.EqualTo(haul.IdsBySlot()[owner]), "the previous owner is saved as a bag id");
                    rig.Host.RestoreTown(AsLoaded(saved));
                }
            }
            Assert.That(owners[2], Is.Not.EqualTo(owners[1]));
        }

        // The quit-and-reload shared by the two resume tests: the dive state as captured, as the file's clean and the load's clean leave it, the dive thrown away, the loaded world restored into the director.
        private static TownSnapshot QuitAndReload(Rig rig)
        {
            TownSnapshot loaded = AsLoaded(rig.Host.CaptureTown());
            Assert.That(rig.Host.ServerAbandonDive(), Is.True, "(the dive ends with the old process)");
            rig.Host.RestoreTown(loaded);
            Assert.That(rig.Host.NetActive.Value, Is.False);
            return loaded;
        }

        [UnityTest]
        public IEnumerator LoadedSuspend_ResumesAtFloor6_WithStirMomentumAndBags_ClearsTheSection()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r, withHeroes: false);
            rig.Host.SeedOverride = 8181u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            DiveHaulSync haul = rig.HostHaul;
            Assert.That(haul.ServerAddItem(hostId, 104) && haul.ServerAddItem(hostId, 105) && haul.ServerAddItem(hostId, 110), Is.True);
            Assert.That(haul.ServerAddGold(hostId, 9), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            Assert.That(haul.ServerPick(hostId, 0), Is.True);
            Assert.That(haul.ServerChoosePushOn(hostId), Is.True);
            for (int n = 0; n < 6; n++) Assert.That(haul.ServerAddItem(hostId, 101), Is.True);   // six more valuables than the baseline (the carried ones at the push-on): the second half's stir is above 0
            float stirSaved = haul.Session.Stir.Value;
            Assert.That(stirSaved, Is.GreaterThan(0f), "premise: a stir value worth keeping");
            bool heldBefore = rig.Host.Checkpoint.Held;

            TownSnapshot loaded = QuitAndReload(rig);
            Assert.That(loaded.Suspend, Is.Not.Null, "premise: the load kept the suspend");
            Assert.That(loaded.Dive.InDive, Is.True, "and did not count a failed dive");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetSuspendedDive.Value, Timeout, "the hub never offered the suspended dive");
            Assert.That(rig.Host.ServerStartDive(2), Is.True, "the trapdoor resumes it, whatever floor the pick says");

            DiveSession s = rig.Host.Session;
            Assert.That(s.Floor, Is.EqualTo(6), "push-on chosen: the start of the NEXT floor");
            Assert.That(s.Phase, Is.EqualTo(DivePhase.Floor));
            Assert.That(s.BankChosenThisDive, Is.True, "the bank is locked");
            Assert.That(s.Momentum.PushedOn, Is.True);
            Assert.That(s.Stir.Capture().SecondHalf, Is.True);
            Assert.That(s.Stir.Value, Is.EqualTo(stirSaved).Within(0.01f), "the stir value is kept");
            Assert.That(rig.Host.Seed, Is.EqualTo(8181u), "the saved dive seed (chest masks match their seed)");
            Assert.That(rig.Host.Checkpoint.Held, Is.EqualTo(heldBefore), "a resume neither spends nor sets the checkpoint");
            PlayerBag bag = rig.Host.HaulSync.ServerBagOf(hostId);
            Assert.That(bag.Carried, Is.EqualTo(new[] { 105, 110, 101, 101, 101, 101, 101, 101 }), "carried items are back");
            Assert.That(bag.BankedThisDive.Keys, Is.EquivalentTo(new[] { 104 }));
            Assert.That(bag.DiveGold, Is.EqualTo(9));
            Assert.That(rig.Host.NetSuspendedDive.Value, Is.False);
            TownSnapshot after = rig.Host.CaptureTown();
            Assert.That(after.SuspendKnown && after.Suspend == null, Is.True, "using the section clears it from the live copy");
            Assert.That(rig.Host.ServerStartDive(), Is.False, "the dive runs");
            yield return AllBuilt(rig, 6);
        }

        [UnityTest]
        public IEnumerator LoadedSuspend_BeforePushOn_ReturnsToBankRoom_WithItsPicks()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r, withHeroes: false);
            rig.Host.SeedOverride = 8282u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            DiveHaulSync haul = rig.HostHaul;
            Assert.That(haul.ServerAddItem(hostId, 104) && haul.ServerAddItem(hostId, 105), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            Assert.That(haul.ServerPick(hostId, 1), Is.True);
            bool heldBefore = rig.Host.Checkpoint.Held;

            TownSnapshot loaded = QuitAndReload(rig);
            Assert.That(loaded.Suspend.BankChosen, Is.False);
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            DiveSession s = rig.Host.Session;
            Assert.That(s.Phase, Is.EqualTo(DivePhase.BankRoom), "push-on not chosen: back in the bank room");
            Assert.That(s.Floor, Is.EqualTo(5));
            Assert.That(s.BankChosenThisDive, Is.False);
            Assert.That(rig.Host.Checkpoint.Held, Is.EqualTo(heldBefore));
            PlayerBag bag = rig.Host.HaulSync.ServerBagOf(hostId);
            Assert.That(bag.BankOpen, Is.True, "visits reopened");
            Assert.That(bag.SlotCapacity, Is.EqualTo(3));
            Assert.That(bag.Picks, Is.EqualTo(new[] { 1 }), "the saved pick is still picked");
            Assert.That(bag.Carried, Is.EqualTo(new[] { 104, 105 }));
            yield return AllBuilt(rig, 5);
            Assert.That(rig.Host.HaulSync.Current.DivePhase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(rig.Host.HaulSync.ServerChoosePushOn(hostId), Is.True, "and the choice is still to be made");
            Assert.That(bag.BankedThisDive.Keys, Is.EquivalentTo(new[] { 105 }));
        }

        // ================================================================================================================================
        // Plan 3 task 10 on the real floors: the collapse's path time, its target (stair / bank cell / end cell) and its early close. Valuables, halves and the timeout are in DiveHaulNetworkTests.
        // Catches: a collapse begun with no path (always the minimum), a target that is not this floor's stair (floor 5: the bank cell; floor 7: the end cell), the timer closing while a living hero is still away,
        // a downed hero keeping it open, the party at the target not closing it, a closed collapse starting again. NOT covered: the "deep closes" countdown on the HUD (the walk); the 60 s wait itself.
        // ================================================================================================================================

        private static Vector3 CellWorld(CellarFloorSync floor, CellPos cell) => floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y));

        private static void AddValuables(Rig rig, ulong who, int count)
        {
            for (int n = 0; n < count; n++) Assert.That(rig.HostHaul.ServerAddItem(who, 101), Is.True, "valuable " + (n + 1));
        }

        [UnityTest]
        public IEnumerator Collapse_StartsWithPathTime_StaysOpenWhileALivingHeroIsAway_ClosesWhenOnlyTheDownedAreAway()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 6161u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 2);
            AddValuables(rig, hostId, 20);   // 20 x 5 x sqrt 2 = 141: past 100
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.DeepClosesLatched, Timeout, "the collapse never began (stir " + rig.HostHaul.Current.StirValue + ")");

            float path = DiveFloorTable.PathSecondsToTarget(rig.HostFloor.Layout);
            Assert.That(path, Is.GreaterThan(0f), "premise: the floor has a walk to the stair");
            int expected = Mathf.CeilToInt(DiveStir.CollapseSeconds(path));
            Assert.That(rig.HostHaul.Current.DeepSecondsLeft, Is.InRange(expected - 3, expected), "the timer is the floor's walk time (x 1.5, at least the minimum), not a fixed number");
            Assert.That(rig.HostHaul.TryCollapseTargetWorld(out Vector3 target), Is.True);
            Vector3 stairCell = CellWorld(rig.HostFloor, rig.HostFloor.Layout.StairExit.Value);
            Assert.That(Flat(target, stairCell), Is.LessThan(0.1f), "the target on floors 1-4 is the stair's cell");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHauls[0].Current.DeepClosesLatched, Timeout, "the client never saw the timer");

            // The host hero arrives, the client hero does not: still running.
            Move(rig.HostHero, target + new Vector3(1f, 0.1f, 0f));
            yield return Seconds(0.8f);
            Assert.That(rig.HostHaul.Current.DeepClosesLatched, Is.True, "a living hero is still away: the timer keeps running");

            // The away hero goes down: only the living count, and the living are all at the target.
            HeroVitals away = rig.HeroesOnHost[0].GetComponent<HeroVitals>();
            away.ServerDamage(10000);
            Assert.That(away.IsUp, Is.False, "premise: the away hero is down");
            yield return PlayModeHarness.WaitUntil(() => !rig.HostHaul.Current.DeepClosesLatched, Timeout, "the timer never closed with every living hero at the target");
            Assert.That(rig.Host.NetActive.Value, Is.True, "closing early is not a failure");
            Assert.That(rig.HostHaul.Session.Phase, Is.EqualTo(DivePhase.Floor));
            yield return Seconds(0.5f);
            Assert.That(rig.HostHaul.Current.DeepClosesLatched, Is.False, "and it does not begin a second time");
        }

        [UnityTest]
        public IEnumerator Collapse_ClosesEarly_WhenAllLivingPlayersAreAtTheTarget()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 6162u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return ToFloor(rig, 2);
            AddValuables(rig, hostId, 20);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.DeepClosesLatched, Timeout, "the collapse never began");
            Assert.That(rig.HostHaul.TryCollapseTargetWorld(out Vector3 target), Is.True);
            Move(rig.HostHero, target + new Vector3(1f, 0.1f, 0f));
            Move(rig.OwnHeroes[0], target + new Vector3(-1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, target) < 3f, Timeout, "the client hero never arrived on the host");
            yield return PlayModeHarness.WaitUntil(() => !rig.HostHaul.Current.DeepClosesLatched, Timeout, "both heroes stand at the stair and the timer never closed");
            yield return PlayModeHarness.WaitUntil(() => !rig.PeerHauls[0].Current.DeepClosesLatched, Timeout, "the client never saw the timer close");
            Assert.That(rig.Host.NetActive.Value, Is.True);
        }

        [UnityTest]
        public IEnumerator Collapse_TargetIsBankCellOnFloor5_EndCellOnFloor7()
        {
            Rig rig = null;
            yield return Start(0, r => rig = r);
            rig.Host.SeedOverride = 6163u;
            ulong hostId = rig.HostNm.LocalClientId;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);

            // Floor 5: the bank cell. The guardian is not down, so the party is on the floor (Phase Floor) and the collapse runs.
            yield return ToFloor(rig, 5);
            AddValuables(rig, hostId, 20);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.DeepClosesLatched, Timeout, "floor 5: the collapse never began");
            Assert.That(rig.HostHaul.TryCollapseTargetWorld(out Vector3 target5), Is.True);
            Assert.That(Flat(target5, CellWorld(rig.HostFloor, rig.HostFloor.Layout.BankRoomCell.Value)), Is.LessThan(0.1f), "floor 5: the bank cell, not a stair (it has none until the choice)");
            Move(rig.HostHero, target5 + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => !rig.HostHaul.Current.DeepClosesLatched, Timeout, "floor 5: the party at the bank cell never closed the timer");
            Move(rig.HostHero, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0));

            // Floor 7: the end cell (second half: the valuables carried at the push-on are the baseline, so 20 more are needed).
            yield return ToFloor(rig, 7);
            Assert.That(rig.HostHaul.Current.DeepClosesLatched, Is.False, "the push-on reset the danger side");
            AddValuables(rig, hostId, 20);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.DeepClosesLatched, Timeout, "floor 7: the collapse never began (stir " + rig.HostHaul.Current.StirValue + ")");
            Assert.That(rig.HostHaul.TryCollapseTargetWorld(out Vector3 target7), Is.True);
            Assert.That(Flat(target7, CellWorld(rig.HostFloor, rig.HostFloor.Layout.EndCell.Value)), Is.LessThan(0.1f), "floor 7: the end cell (it has no stair)");
            Move(rig.HostHero, target7 + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => !rig.HostHaul.Current.DeepClosesLatched, Timeout, "floor 7: the party at the end cell never closed the timer");
        }
    }
}
