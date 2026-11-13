// Authority: test-only. PL-2 task 2.3: the dive result over real NGO peers in one process (host + clients, scene management off). The host writes only the end reason and a dive counter
// (DiveDirector.NetEnd); each peer builds its own DiveResult from its own owner-read BagView, snapshotted while the dive ran, so a player sees only their own numbers.
// Catches: a result built from the wrong player's bag, a client that never learns the ending, a repeat ending that shows no card (the counter), a late joiner shown the last dive's card, the lost
// satchel missing from the words, a card that stays up forever or ignores Interact.
// NOT covered by a one-process test: the IMGUI drawing, a dive that ends through the stir collapse or all heroes down (the same EndDive path; the test uses the abandon path), the Interact
// key itself (the test drives the fake input's edge), a real second machine.
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class DiveResultNetworkTests
    {
        private const float Timeout = 25f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        private sealed class Peer
        {
            public NetworkManager Nm; public ulong Id; public DiveDirector Dir; public PlayerBagSync Bag; public DiveResultCard Card; public FakeInputService Input;
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm; public DiveDirector Director;
            public Peer Host; public List<Peer> Clients = new List<Peer>();
            public List<PlayModeHarness.TestClient> TestClients = new List<PlayModeHarness.TestClient>();
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static Peer MakePeer(NetworkManager nm, DiveDirector dir)
        {
            var p = new Peer { Nm = nm, Id = nm.LocalClientId, Dir = dir, Bag = PlayerBagSync.ForLocalPlayer(nm), Input = new FakeInputService() };
            var go = new GameObject("TestResultCard_" + nm.LocalClientId);
            p.Card = go.AddComponent<DiveResultCard>();
            p.Card.Director = dir; p.Card.Bag = p.Bag; p.Card.Input = p.Input;
            return p;
        }

        // The host spawns a hero for the peer and the peer receives it; returns the peer's owner-side PlayerBagSync.
        private static IEnumerator SpawnHero(Rig r, NetworkManager owner, Vector3 pos)
        {
            r.HostNm.SpawnManager.InstantiateAndSpawn(r.App.PlayerPrefab, owner.LocalClientId, false, true, false, pos, Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => PlayerBagSync.ForLocalPlayer(owner) != null, Timeout, "peer " + owner.LocalClientId + " never received its hero");
        }

        private static IEnumerator AddClient(Rig r, int index, System.Action<Peer> done)
        {
            yield return PlayModeHarness.StartClients(r.App, 1, false, r.TestClients, index);
            NetworkManager cnm = r.TestClients[r.TestClients.Count - 1].NetworkManager;
            yield return SpawnHero(r, cnm, new Vector3(420f + index, 0f, 400f));
            ulong dirId = r.Director.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => cnm.SpawnManager.SpawnedObjects.ContainsKey(dirId), Timeout, "the client never received the director");
            Peer p = MakePeer(cnm, cnm.SpawnManager.SpawnedObjects[dirId].GetComponent<DiveDirector>());
            r.Clients.Add(p);
            done(p);
        }

        // Host with a hero and a director, one client with a hero; a result card per peer (each bound to its own peer's objects) is ticking before any dive starts.
        private static IEnumerator Start(Rig rig, int clients)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            rig.App = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Director = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Director.ServerConfigure(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            rig.Director.SeedOverride = 5150u;
            yield return PlayModeHarness.WaitUntil(() => rig.Director.TownSync != null && rig.Director.TownSync.IsSpawned, Timeout, "the director never spawned its TownSync");
            yield return SpawnHero(rig, rig.HostNm, new Vector3(400f, 0f, 400f));
            rig.Host = MakePeer(rig.HostNm, rig.Director);
            for (int i = 0; i < clients; i++) yield return AddClient(rig, 20 + i, p => { });
            yield return TownInputRig.Seconds(0.3f);   // every card has ticked outside a dive: its baseline is taken
        }

        private static IEnumerator StartDive(Rig rig)
        {
            Assert.That(rig.Director.ServerStartDive(), Is.True, "the dive starts");
            DiveHaulSync haul = rig.Director.HaulSync;
            Assert.That(haul, Is.Not.Null);
            foreach (Peer c in rig.Clients)
            {
                Peer peer = c;
                yield return PlayModeHarness.WaitUntil(() => peer.Dir.NetActive.Value && peer.Dir.HaulSync != null && GuideSources.TownOf(peer.Dir) != null, Timeout, "a client never saw the dive start");
            }
        }

        private static IEnumerator WaitRecording(params Peer[] peers)
        {
            foreach (Peer p in peers)
            {
                Peer peer = p;
                yield return PlayModeHarness.WaitUntil(() => peer.Card.Model.Recording, Timeout, "peer " + peer.Id + "'s card never recorded the bag it will report");
            }
        }

        private static IEnumerator WaitBagShows(Peer p, int carried, int diveGold)
        {
            yield return PlayModeHarness.WaitUntil(() => p.Bag.View.Carried.Length == carried && p.Bag.View.DiveGold == diveGold, Timeout,
                "peer " + p.Id + "'s own view never showed " + carried + " items and " + diveGold + " gold (shows " + p.Bag.View.Carried.Length + ", " + p.Bag.View.DiveGold + ")");
        }

        [UnityTest]
        public IEnumerator EachPlayer_GetsTheirOwnResult_HostAndClient()
        {
            var rig = new Rig();
            yield return Start(rig, 1);
            Peer host = rig.Host, client = rig.Clients[0];
            yield return StartDive(rig);
            DiveHaulSync haul = rig.Director.HaulSync;
            Assert.That(haul.ServerAddItem(host.Id, 104), Is.True);
            Assert.That(haul.ServerAddGold(host.Id, 10), Is.True);
            Assert.That(haul.ServerAddItem(client.Id, 110), Is.True);
            Assert.That(haul.ServerAddItem(client.Id, 105), Is.True);
            Assert.That(haul.ServerAddGold(client.Id, 41), Is.True);
            Assert.That(haul.ServerAddPiece(PieceCatalog.FrameId), Is.True);
            Assert.That(haul.ServerAddPiece(PieceCatalog.BakerId), Is.True);
            rig.Director.TownSync.ServerRefresh();
            yield return WaitBagShows(host, 1, 10);
            yield return WaitBagShows(client, 2, 41);
            yield return PlayModeHarness.WaitUntil(() => client.Dir.HaulSync.Current.FoundPieces == 1 && GuideSources.TownOf(client.Dir).View.ResidentInStock(TownBuildings.BakerResident), Timeout, "the client never saw the found piece and person");
            yield return WaitRecording(host, client);
            yield return TownInputRig.Seconds(0.3f);   // the last view is the one the cards hold

            Assert.That(host.Card.Model.Result, Is.Null, "no card while the dive runs");
            Assert.That(rig.Director.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => host.Card.Model.Result != null && client.Card.Model.Result != null, Timeout, "a card never appeared (host " + (host.Card.Model.Result != null) + ", client " + (client.Card.Model.Result != null) + ")");

            DiveResult h = host.Card.Model.Result, c = client.Card.Model.Result;
            Assert.That(h.Ending, Is.EqualTo(DiveEnding.Abandoned));
            Assert.That(c.Ending, Is.EqualTo(DiveEnding.Abandoned), "the client learned the ending from the host's one value");
            Assert.That(h.ItemsLost, Is.EqualTo(1), "the host's own bag");
            Assert.That(c.ItemsLost, Is.EqualTo(2), "the client's own bag, not the host's");
            Assert.That(h.GoldKept, Is.EqualTo(5));
            Assert.That(c.GoldKept, Is.EqualTo(20));
            Assert.That(c.GoldLost, Is.EqualTo(21));
            Assert.That(h.PiecesFound, Is.EqualTo(1));
            Assert.That(c.PiecesFound, Is.EqualTo(1), "found pieces are the party's, every peer reads them");
            Assert.That(c.PeopleFound, Is.EqualTo(new[] { TownBuildings.BakerResident }), "the person found this dive");
            Assert.That(h.PeopleFound, Is.EqualTo(new[] { TownBuildings.BakerResident }));
        }

        [UnityTest]
        public IEnumerator Fail_ShowsTheLostSatchel()
        {
            var rig = new Rig();
            yield return Start(rig, 1);
            Peer client = rig.Clients[0];
            yield return StartDive(rig);
            Assert.That(rig.Director.HaulSync.ServerAddItem(client.Id, 104), Is.True);
            Assert.That(rig.Director.HaulSync.ServerAddItem(client.Id, 105), Is.True);
            Assert.That(rig.Director.HaulSync.ServerAddItem(client.Id, 110), Is.True);
            yield return WaitBagShows(client, 3, 0);
            yield return WaitRecording(client);
            yield return TownInputRig.Seconds(0.3f);
            Assert.That(rig.Director.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => client.Card.Model.Result != null, Timeout, "the client's card never appeared");
            Assert.That(client.Card.Model.Result.ItemsLost, Is.EqualTo(3));
            string text = string.Join("\n", client.Card.Model.Lines);
            StringAssert.Contains("satchel", text, "the card says what waits in the lost satchel");
            StringAssert.Contains("3", text);
            yield return PlayModeHarness.WaitUntil(() => client.Bag.View.SatchelWaiting, Timeout, "the owner's own view never showed the waiting satchel the card talks about");
        }

        [UnityTest]
        public IEnumerator Card_Dismisses_OnInteract()
        {
            var rig = new Rig();
            yield return Start(rig, 0);
            Peer host = rig.Host;
            yield return StartDive(rig);
            yield return WaitRecording(host);
            Assert.That(rig.Director.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => host.Card.Model.Result != null, Timeout, "the card never appeared");
            int showing = DiveResultCard.ShowingCount;   // the app's own self-spawned card counts too
            Assert.That(showing, Is.GreaterThan(0), "the guide line can see that a card is up");
            yield return TownInputRig.Seconds(0.5f);
            Assert.That(host.Card.Model.Result, Is.Not.Null, "it stays up without a press");
            host.Input.Frame = default; host.Input.Frame.InteractPressed = true;
            yield return null;
            host.Input.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => host.Card.Model.Result == null, Timeout, "Interact never dismissed the card");
            yield return null;
            Assert.That(DiveResultCard.ShowingCount, Is.EqualTo(showing - 1), "this card no longer counts as showing");
        }

        [UnityTest]
        public IEnumerator Card_Hides_AfterTenSeconds()
        {
            var rig = new Rig();
            yield return Start(rig, 0);
            Peer host = rig.Host;
            yield return StartDive(rig);
            yield return WaitRecording(host);
            Assert.That(rig.Director.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => host.Card.Model.Result != null, Timeout, "the card never appeared");
            double shown = Time.realtimeSinceStartupAsDouble;
            yield return PlayModeHarness.WaitUntil(() => host.Card.Model.Result == null, DiveResultModel.ShowSeconds + 5f, "the card never timed out");
            double up = Time.realtimeSinceStartupAsDouble - shown;
            Assert.That(up, Is.InRange(DiveResultModel.ShowSeconds - 0.5, DiveResultModel.ShowSeconds + 2.0), "the card stays about " + DiveResultModel.ShowSeconds + " s");
        }

        [UnityTest]
        public IEnumerator LateJoiner_GetsNoCard()
        {
            var rig = new Rig();
            yield return Start(rig, 0);
            Peer host = rig.Host;
            yield return StartDive(rig);
            yield return WaitRecording(host);
            Assert.That(rig.Director.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => host.Card.Model.Result != null && rig.Director.NetEnd.Value.Count == 1, Timeout, "the first dive's card never appeared");

            Peer late = null;
            yield return AddClient(rig, 30, p => late = p);
            yield return PlayModeHarness.WaitUntil(() => late.Dir.NetEnd.Value.Count == 1, Timeout, "the late joiner never read the dive counter with the spawn");
            yield return TownInputRig.Seconds(1f);
            Assert.That(late.Card.Model.Result, Is.Null, "a late joiner gets no card for a dive it was not in");
            Assert.That(late.Card.Model.Ready, Is.True, "and the card did look");

            // The counter still works afterwards: the next ending (the same reason again) shows a card to the late joiner.
            yield return TownInputRig.Seconds(1.2f);   // the first dive's floor is cleared and the party is home
            yield return StartDive(rig);
            yield return PlayModeHarness.WaitUntil(() => late.Dir.NetActive.Value, Timeout, "the late joiner never saw the second dive");
            yield return WaitRecording(late);
            Assert.That(rig.Director.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => late.Card.Model.Result != null, Timeout, "the same ending twice showed no card the second time");
            Assert.That(late.Card.Model.Result.Ending, Is.EqualTo(DiveEnding.Abandoned));
        }
    }
}
