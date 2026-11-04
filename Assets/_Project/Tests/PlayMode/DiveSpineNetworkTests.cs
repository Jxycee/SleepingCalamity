// Authority: test-only. M5 dive spine: the host-owned DiveDirector (start, stair, bank, go home, fail) over the real CellarFloorSync + DiveHaulSync + player prefab.
// Host + clients in one process (scene management off; the director, the syncs and the heroes are spawned from the owned network prefab list / the app's player prefab).
// Catches: peers building different floors, a stair press that does not advance every peer to the same floor, a stale or out-of-range stair press advancing (or double-advancing) the dive,
// players not teleported to the new entry through their owner (owner-authoritative NetworkTransform), a failed / finished dive that leaves the party on the floor or the dive running,
// the checkpoint / banked pieces lost between dives, a go-home before the heart bank.
// NOT covered by a one-process test: the interact KEY and its prompt (the tests call the same Request* wrappers the key calls), the greybox look, real relay latency.
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
        public IEnumerator Floor4_HostAndTwoClients_BuildTheIdenticalBakeryFloor()
        {
            Rig rig = null;
            yield return Start(2, r => rig = r, withHeroes: false);
            rig.Host.SeedOverride = 424242u;
            Assert.That(rig.Host.ServerStartDive(4), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 4);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(4));
            Assert.That(rig.HostFloor.Layout.BankCell.HasValue, Is.True, "the heart floor has the baker's bank point");
            Assert.That(rig.HostFloor.BuiltSeed, Is.EqualTo(424242u));
            foreach (CellarFloorSync p in rig.PeerFloors) Assert.That(p.BuiltSeed, Is.EqualTo(424242u));
            // Every peer drew the dive markers on its own build (the way-home column only exists on floor 4).
            Assert.That(rig.HostFloor.Built.Root.Find(CellarFloorBuilder.DiveMarkerGroup + "/HomeColumn"), Is.Not.Null);
            Assert.That(rig.Peers[0].NetActive.Value, Is.True);
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
        public IEnumerator DiveFailed_ReturnsEveryoneToTheStart_EndsTheDive_AndKeepsTheSession()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            StirTuning tuning = StirTuning.Default; tuning.DeepClosesSeconds = 3f; tuning.PerFloorCleared = 30f;   // pieces no longer feed the stir (plan 2): three cleared floors x 30 x 1.25 pass 100
            rig.Host.DiveStir = tuning; rig.Host.PiecesPerFloor = new[] { 6, 6, 6 }; rig.Host.SeedOverride = 777u;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-1 entry");

            // Collect the whole haul and clear every floor to push the stir past 100; the countdown runs out and the host fails the dive.
            for (int f = 1; f <= 3; f++) for (int i = 0; i < 6; i++) rig.HostHaul.ServerAddPiece(DiveHaul.PieceId(f, i));
            for (int f = 1; f <= 3; f++) rig.HostHaul.ServerMarkCleared(f);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value, Timeout, "the stir countdown never ended the dive");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(0));
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(18), "the failed dive keeps every collected piece (safe on pickup)");
            yield return PlayModeHarness.WaitUntil(() => !rig.Peers[0].NetActive.Value, Timeout, "the client never saw the dive end");
            yield return NearOnHost(rig, Home, "home after the failed dive");
            Assert.That(Flat(rig.OwnHeroes[0].transform.position, Home), Is.LessThan(8f), "the client's own copy went home");
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == CellarFloorSync.NoFloor && rig.PeerFloors[0].BuiltFloor == CellarFloorSync.NoFloor, Timeout, "the floor was never cleared on every peer");
            Assert.That(rig.HostFloor, Is.Not.Null, "the sync objects stay for the session");

            // A new dive starts clean on the same syncs.
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return AllBuilt(rig, 1);
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

        [UnityTest]
        public IEnumerator Floor4_BankThenGoHome_EndsTheDive_BankedPiecesKept_NoGoHomeBeforeTheBank()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 99u;
            Assert.That(rig.Host.ServerStartDive(4), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 4);
            DiveDirector peer = rig.Peers[0];
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-4 entry"); yield return Seconds(1f);   // the arrival teleports must all have landed before the test moves a hero
            Assert.That(rig.HostHaul.ServerAddPiece(100), Is.True);
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True);
            Move(rig.OwnHeroes[0], bank + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, bank) < 3f, Timeout, "the client hero never reached the baker on the host");

            // Going home before the bank is refused.
            int handled = rig.Host.RequestsHandled;
            peer.RequestGoHome();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the early go-home never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "no way home before banking at the heart");

            // Bank by RPC, every peer sees the heart banked, then go home.
            rig.PeerHauls[0].RequestBank();
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.BankedPieces == 1 && rig.Host.NetHeartBanked.Value, Timeout, "the heart bank never landed");
            yield return PlayModeHarness.WaitUntil(() => peer.NetHeartBanked.Value, Timeout, "the client never saw the heart banked");
            peer.RequestGoHome();
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value, Timeout, "go home never ended the dive");
            yield return PlayModeHarness.WaitUntil(() => !peer.NetActive.Value, Timeout, "the client never saw the dive end");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(0));
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(1), "the banked piece is kept in memory");
            yield return NearOnHost(rig, Home, "home after the rescue");
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "the floor was never cleared");
        }

        [UnityTest]
        public IEnumerator Checkpoint_ReachedOnFloor2_LetsTheNextDiveStartOnFloor3_AbandonGoesHome()
        {
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = 31337u;
            Assert.That(rig.Host.ServerStartDive(2), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 2);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-2 entry"); yield return Seconds(1f);   // the arrival teleports must all have landed before the test moves a hero
            foreach (int piece in new[] { 100, 101, 200, 201 }) Assert.That(rig.HostHaul.ServerAddPiece(piece), Is.True);   // every piece on floors 1-2: only then does the return dive skip them
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True);
            Move(rig.HostHero, bank + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, bank) < 3f, Timeout, "the host hero never reached the checkpoint");
            rig.HostHaul.RequestBank();
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CheckpointReached && rig.Host.NetCheckpoint.Value, Timeout, "the checkpoint was never reached");

            Assert.That(rig.Host.ServerAbandonDive(), Is.True);
            Assert.That(rig.Host.NetActive.Value, Is.False);
            yield return NearOnHost(rig, Home, "home after abandoning");
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(4), "the banked pieces survive an abandoned dive");

            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "the floor was never cleared");
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(3), "a return dive starts on floor 3 once the checkpoint was reached and floors 1-2 are fully collected");
            yield return AllBuilt(rig, 3);
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(4));
            Assert.That(rig.HostFloor.BuiltSeed, Is.EqualTo(31337u), "one seed per session");
        }

        // Contents hook: the director spawns CellarContentsSync, arms it per built floor, opens the stair on the gate key (ExitUnlocked) and clears the enemies when the dive fails.
        [UnityTest]
        public IEnumerator Contents_ArmedByTheDirector_StairNeedsTheGateKey_FailedDiveClearsEnemies()
        {
            uint seed = 0;
            for (uint sd = 1; sd < 400 && seed == 0; sd++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(sd, 1);
                if (l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0) seed = sd;
            }
            Assert.That(seed, Is.Not.EqualTo(0u), "a seed with a carrier");
            Rig rig = null;
            yield return Start(1, r => rig = r);
            rig.Host.SeedOverride = seed;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ContentsSync != null && rig.Host.ContentsSync.State.Seed == seed && rig.Host.ContentsSync.State.Floor == 1, Timeout, "the director never armed the contents for floor 1");
            CellarContentsSync contents = rig.Host.ContentsSync;
            Assert.That(rig.Host.RequireGateKey, Is.True, "contents gate the stair");
            Assert.That(contents.AutoArm, Is.False, "the director arms the contents, not their own build watcher");
            Assert.That(contents.State.ExitUnlocked, Is.False);
            yield return NearOnHost(rig, CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0), "floor-1 entry");

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
            peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the shut stair press never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1), "the gate key is not taken yet: the stair stays shut");

            // The key is picked up: ExitUnlocked opens the stair through the director.
            Move(rig.OwnHeroes[0], contents.State.GateKeyPos + new Vector3(0.5f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => contents.State.ExitUnlocked, Timeout, "picking the key up never unlocked the exit");
            Move(rig.OwnHeroes[0], stair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HeroesOnHost[0].transform.position, stair) < 3f, Timeout, "the client hero never got back to the stair");
            peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CurrentFloor == 2, Timeout, "the opened stair never took the party down");
            yield return PlayModeHarness.WaitUntil(() => contents.State.Floor == 2 && contents.State.Seed == rig.HostFloor.BuiltSeed, Timeout, "the contents were never re-armed for floor 2");

            // A failed dive clears the contents (and their enemies).
            Assert.That(rig.Host.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => contents.State.Floor == 0, Timeout, "the failed dive never cleared the contents");
            int enemies = 0;
            foreach (NetworkObject o in rig.HostNm.SpawnManager.SpawnedObjects.Values) { Enemy en = o.GetComponent<Enemy>(); if (en != null && en.Alive) enemies++; }   // the killed carrier's corpse may linger until its own despawn timer
            Assert.That(enemies, Is.EqualTo(0), "no living enemy survives a failed dive");
        }
    }
}
