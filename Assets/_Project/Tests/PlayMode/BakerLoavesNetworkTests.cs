// Authority: test-only. M6 S5: the baker's service over real NGO peers in one process (host + one client, scene management off). The host evaluates BakerService on its own town at
// ServerStartDive and sets HeroVitals.NetLoaves on every connected hero; the client reads it by replication, eats a loaf by an owner RPC, and sees the first-meet latch (TownSync.NetBakerMet).
// Catches: loaves granted when a wish is unmet (or not granted when all are met), a client hero left out, a count the client cannot read, an eat request the host does not re-check
// (full HP, no loaf), a first-meet latch that never reaches the client or un-latches when the baker is picked up.
// NOT covered by a one-process test: the H key and the on-screen notice/first-meet line (GUI), the latch surviving a real save file (EditMode round-trip covers the section; the world-save
// write is the existing HostWorld path).
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class BakerLoavesNetworkTests
    {
        private const float Timeout = 25f;
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
            public NetworkManager HostNm, ClientNm; public DiveDirector Director; public TownSync Host, Peer;
            public HeroVitals HostHero, ClientHeroOnHost, ClientHero;
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        // Host + one client, a director with the bakery placed on `lot` at `turn`, complete, the baker moved in. Two heroes stand far from anything.
        private IEnumerator Start(System.Action<Rig> done, string lot, int turn)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            var rig = new Rig { HostNm = app.NetworkManager };
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            rig.ClientNm = client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => rig.HostNm.ConnectedClientsIds.Count == 2 && rig.ClientNm.IsConnectedClient, Timeout, "the client never connected");

            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Director = dirObj.GetComponent<DiveDirector>();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.TownSync != null && rig.Director.TownSync.IsSpawned, Timeout, "the director never spawned its TownSync");
            rig.Host = rig.Director.TownSync;
            ulong tid = rig.Host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(tid), Timeout, "the client never received the TownSync");
            rig.Peer = rig.ClientNm.SpawnManager.SpawnedObjects[tid].GetComponent<TownSync>();

            TownState s = rig.Director.Town.State;
            foreach (int p in new[] { 100, 101, 200, 201 }) Assert.That(s.AddPiece(p), Is.EqualTo(PlaceResult.Ok));
            s.AddResident(TownBuildings.BakerResident);
            Assert.That(s.PlaceFrame(TownBuildings.Bakery, lot, turn), Is.EqualTo(PlaceResult.Ok));
            foreach (string part in new[] { "oven", "door", "sign" }) Assert.That(s.PlacePart(TownBuildings.Bakery, part), Is.EqualTo(PlaceResult.Ok));
            Assert.That(s.PlaceResident(TownBuildings.Bakery), Is.EqualTo(PlaceResult.Ok));
            rig.Host.ServerRefresh();

            Vector3 far = new Vector3(400f, 0f, 400f);
            NetworkObject hostHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, far, Quaternion.identity);
            NetworkObject clientHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, rig.ClientNm.LocalClientId, false, true, false, far + new Vector3(8f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.LocalClient != null && rig.ClientNm.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            rig.HostHero = hostHero.GetComponent<HeroVitals>();
            rig.ClientHeroOnHost = clientHero.GetComponent<HeroVitals>();
            rig.ClientHero = rig.ClientNm.LocalClient.PlayerObject.GetComponent<HeroVitals>();
            done(rig);
        }

        [UnityTest]
        public IEnumerator AllWishesMet_EveryHeroGetsThreeLoaves_ClientReadsAndEats_FirstMeetLatchReachesClient()
        {
            Rig rig = null;
            yield return Start(r => rig = r, "F02", 0);
            Assert.That(rig.HostHero.Loaves, Is.EqualTo(0), "no loaves before a dive");

            Assert.That(rig.Director.ServerStartDive(), Is.True);
            Assert.That(rig.HostHero.Loaves, Is.EqualTo(3), "the host's own hero");
            Assert.That(rig.ClientHeroOnHost.Loaves, Is.EqualTo(3), "the client's hero on the host");
            yield return PlayModeHarness.WaitUntil(() => rig.ClientHero.Loaves == 3, Timeout, "the client never read its 3 loaves");

            yield return PlayModeHarness.WaitUntil(() => rig.Peer.NetBakerMet.Value, Timeout, "the first-meet latch never reached the client");
            Assert.That(rig.Director.BakerMet, Is.True);

            // Eating: refused at full HP (the host re-checks), allowed when hurt, and the heal and the count reach the client.
            rig.ClientHero.RequestEatLoaf();
            yield return Seconds(0.5f);
            Assert.That(rig.ClientHeroOnHost.Loaves, Is.EqualTo(3), "a full-HP hero does not waste a loaf");
            int before = rig.ClientHeroOnHost.ServerDamage(50);
            Assert.That(before, Is.EqualTo(50));
            yield return PlayModeHarness.WaitUntil(() => rig.ClientHero.Hp == rig.ClientHero.MaxHp - 50, Timeout, "the client never saw its damage");
            rig.ClientHero.RequestEatLoaf();
            yield return PlayModeHarness.WaitUntil(() => rig.ClientHero.Loaves == 2 && rig.ClientHero.Hp == rig.ClientHero.MaxHp - 50 + HeroVitals.LoafHeal, Timeout, "the client's loaf never healed it");
            Assert.That(rig.ClientHeroOnHost.Loaves, Is.EqualTo(2));
            Assert.That(rig.HostHero.Loaves, Is.EqualTo(3), "the other hero's loaves are untouched");

            // The latch never un-latches: the baker moves out, the flag stays.
            Assert.That(rig.Director.Town.State.PickUpResident(TownBuildings.Bakery), Is.True);
            rig.Host.ServerRefresh();
            yield return Seconds(0.6f);
            Assert.That(rig.Director.BakerMet, Is.True);
            Assert.That(rig.Peer.NetBakerMet.Value, Is.True);
        }

        [UnityTest]
        public IEnumerator WishUnmet_NoLoavesForAnyone()
        {
            Rig rig = null;
            yield return Start(r => rig = r, "F02", 1);   // the door turned away from the square: a wish is unmet
            Assert.That(BakerService.Evaluate(rig.Director.Town.State).Notify, Is.True);
            Assert.That(rig.Director.ServerStartDive(), Is.True);
            Assert.That(rig.HostHero.Loaves, Is.EqualTo(0));
            Assert.That(rig.ClientHeroOnHost.Loaves, Is.EqualTo(0));
            yield return Seconds(0.8f);
            Assert.That(rig.ClientHero.Loaves, Is.EqualTo(0), "the client has none either");
            Assert.That(rig.Director.BakerMet, Is.True, "he is housed in a complete bakery, so the first meeting still happened");
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }
    }
}
