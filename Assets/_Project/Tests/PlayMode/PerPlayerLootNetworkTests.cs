// Authority: test-only. Per-player loot (plan 2): the host keeps one PlayerBag per player inside DiveHaulSync (DiveState.Bags); pieces stay shared and go into the town stock the moment
// they are picked up (DiveDirector listens to DiveHaulSync.PieceAdded).
// Host + clients in one process (scene management off; the syncs and the director are dynamically spawned network prefabs from the owned list).
// Catches (HostKeepsOneBagPerPlayer_ItemsGoToTheRightBag): items landing in the wrong player's bag, the host's own bag collecting a client's loot, a bag key that is not the roster's PlayerKey
// (a rejoin would lose the bag), a client writing a bag, the no-roster slot order leaving 0-3. NOT covered: the owner-only view that shows the bag to its player (PlayerBagSync, task 5).
// Catches (PiecePickup_GoesToStockAtOnce_SurvivesFail): a piece waiting for the dive's end before it reaches the town stock, a failed dive taking a collected piece back, the Summary
// found/home counters. NOT covered: a real pickup walk (the test calls the same ServerAddPiece the pickup sphere calls) and a save/reload of the stock (TownStockSaveTests).
// Catches (BankFloor_OpensVisitsForEveryone_PicksAreValidated): a bank floor opening no visit or the wrong capacity, a pick without a visit, a repeated or surplus pick, a visit left open after
// the party pushes on, the used bank point reopening for a player who merely carries something. NOT covered: the downed-hero and range checks of ServerPick (no player objects here; the same BankAskerOk as the bank press, whose tests cover them in DiveSpineNetworkTests).
// Catches (View_OnlyOwnerAndHostSeeIt_LateJoinerGetsOwn): the owner read filter on PlayerBagSync.Net (a client reading another player's bag, or a late joiner getting every bag with the spawn), the host
// not seeing every bag, the owner's own view not arriving (also for a late joiner). NOT covered: the contents of the view beyond the carried ids (BagView EditMode tests), a real player scene.
// Catches (Pick_ByRpcFromTheOwner_HostValidatesCapacity): the pick RPC reaching the host and being re-validated there (capacity), a pick sent from a peer that does not own the object being applied,
// the view following the host's picks and the bank. NOT covered: the bank-range and downed-hero checks (no built floor / vitals in this rig; DiveSpineNetworkTests), UI input.
// Catches (Rejoin_GetsTheSameBagBack): a rejoin with the same PlayerId getting a new or empty bag, a changed player slot, the new owner's view missing the old bag. Runs with the host roster the app's
// session installs, so the bag key is the PlayerKey; a host without a roster would key by client id and lose the bag. NOT covered: a world save in between (SaveFlowTests).
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class PerPlayerLootNetworkTests
    {
        private const float Timeout = 25f;

        [UnityTearDown]
        public IEnumerator TearDown() => PlayModeHarness.TearDownApp();

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        // Host + `clients` TestClients and a spawned DiveHaulSync (a fresh dive, 2 pieces per floor).
        private static IEnumerator StartHaul(int clients, List<PlayModeHarness.TestClient> into, System.Action<AppBootstrap, DiveHaulSync> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            yield return PlayModeHarness.StartClients(app, clients, false, into);
            NetworkManager hostNm = app.NetworkManager;
            NetworkObject spawned = hostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveHaulSync>(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            done(app, spawned.GetComponent<DiveHaulSync>());
        }

        [UnityTest]
        public IEnumerator HostKeepsOneBagPerPlayer_ItemsGoToTheRightBag()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(2, clients, (a, h) => { app = a; haul = h; });
            ulong host = app.NetworkManager.LocalClientId, ca = clients[0].NetworkManager.LocalClientId, cb = clients[1].NetworkManager.LocalClientId;

            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            Assert.That(haul.ServerAddItem(cb, 110), Is.True);
            Assert.That(haul.ServerBagOf(ca).Carried, Is.EqualTo(new[] { 104 }), "A's bag");
            Assert.That(haul.ServerBagOf(cb).Carried, Is.EqualTo(new[] { 110 }), "B's bag");
            Assert.That(haul.ServerBagOf(host).Carried, Is.Empty, "the host's own bag stays empty");

            Assert.That(haul.ServerAddGold(ca, 7), Is.True);
            Assert.That(haul.ServerBagOf(ca).DiveGold, Is.EqualTo(7));
            Assert.That(haul.ServerBagOf(cb).DiveGold, Is.EqualTo(0));
            Assert.That(haul.ServerAddItem(ca, CellarLoot.RoomKeyId), Is.False, "keys are shared, never carried in a bag");
            Assert.That(haul.ServerAddItem(ca, 0), Is.False);

            // The bag key is the roster's PlayerKey (so a rejoin gets the bag back); slots are 0-3 and distinct per player.
            Assert.That(haul.PlayerIdOf(ca), Is.EqualTo(clients[0].Key.ToString()), "bags are keyed by the PlayerKey string");
            Assert.That(haul.PlayerIdOf(cb), Is.EqualTo(clients[1].Key.ToString()));
            var slots = new HashSet<int> { haul.PlayerSlotOf(host), haul.PlayerSlotOf(ca), haul.PlayerSlotOf(cb) };
            Assert.That(slots.Count, Is.EqualTo(3), "three players, three slots");
            foreach (int s in slots) Assert.That(s, Is.InRange(0, 3));
            Assert.That(haul.PlayerSlotOf(ca), Is.EqualTo(haul.PlayerSlotOf(ca)), "stable");

            // A client copy of the sync holds no bags and writes nothing.
            ulong id = haul.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => clients[0].NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client A never received the haul object");
            DiveHaulSync peer = clients[0].NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<DiveHaulSync>();
            Assert.That(peer.ServerBagOf(ca), Is.Null, "a client has no bags");
            Assert.That(peer.ServerAddItem(ca, 104), Is.False);
            Assert.That(peer.ServerAddGold(ca, 5), Is.False);
            Assert.That(peer.ServerPick(ca, 0), Is.False);
            Assert.That(haul.ServerBagOf(ca).Carried, Is.EqualTo(new[] { 104 }), "the client's attempt changed nothing");
        }

        [UnityTest]
        public IEnumerator PiecePickup_GoesToStockAtOnce_SurvivesFail()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            NetworkObject dirObj = hostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveDirector dir = dirObj.GetComponent<DiveDirector>();
            Assert.That(dir.ServerConfigure(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            dir.SeedOverride = 5150u;
            Assert.That(dir.ServerStartDive(), Is.True);
            DiveHaulSync haul = dir.HaulSync;
            Assert.That(haul, Is.Not.Null);

            int piece = DiveHaul.PieceId(1, 0);
            Assert.That(dir.Town.State.InStock(piece), Is.False, "test premise: not home yet");
            Assert.That(haul.ServerAddPiece(piece), Is.True);
            Assert.That(dir.Town.State.InStock(piece), Is.True, "the piece is in the town stock right after the pickup, not at the dive's end");
            Assert.That(haul.Current.FoundPieces, Is.EqualTo(1));
            Assert.That(haul.Current.BankedPieces, Is.EqualTo(1));

            Assert.That(dir.ServerAbandonDive(), Is.True, "the dive fails through the director's own end-of-dive path");
            Assert.That(dir.NetActive.Value, Is.False);
            Assert.That(dir.Town.State.InStock(piece), Is.True, "still there after the failed dive (no double stocking, no loss)");
            Assert.That(haul.HostState.Haul.IsCollected(piece), Is.True, "and the floors never spawn it again");
            int stocked = 0; foreach (int p in dir.Town.State.StockPieces) if (p == piece) stocked++;
            Assert.That(stocked, Is.EqualTo(1));

            // A new dive: the found counter starts again, the piece stays home.
            dir.SeedOverride = 5151u;
            Assert.That(dir.ServerStartDive(), Is.True);
            Assert.That(haul.Current.FoundPieces, Is.EqualTo(0), "FoundPieces counts this dive only");
            Assert.That(haul.Current.BankedPieces, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BankFloor_OpensVisitsForEveryone_PicksAreValidated()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(1, clients, (a, h) => { app = a; haul = h; });
            ulong host = app.NetworkManager.LocalClientId, ca = clients[0].NetworkManager.LocalClientId;

            Assert.That(haul.ServerBegin(new[] { 2, 2, 2 }), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.ServerAddItem(ca, 101), Is.True);
            Assert.That(haul.ServerAddItem(ca, 112), Is.True);
            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            Assert.That(haul.ServerPick(ca, 0), Is.False, "no bank floor, no pick");
            Assert.That(haul.ServerBagOf(ca).BankOpen, Is.False);

            Assert.That(haul.ServerSetFloor(2), Is.True);
            foreach (ulong c in new[] { host, ca })
            {
                Assert.That(haul.ServerBagOf(c).BankOpen, Is.True, "a bank visit opens for every connected player");
                Assert.That(haul.ServerBagOf(c).SlotCapacity, Is.EqualTo(2), "two players: 4 slots, 2 each");
            }
            Assert.That(haul.ServerPick(ca, 0), Is.True);
            Assert.That(haul.ServerPick(ca, 0), Is.False, "the same item twice");
            Assert.That(haul.ServerPick(ca, 9), Is.False, "no such item");
            Assert.That(haul.ServerUnpick(ca, 0), Is.True);
            Assert.That(haul.ServerPick(ca, 1), Is.True);
            Assert.That(haul.ServerPick(ca, 2), Is.True);
            Assert.That(haul.ServerBagOf(ca).Picks, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(haul.ServerPick(ca, 0), Is.False, "the slots are full");

            Assert.That(haul.ServerBank(), Is.True, "the party pushes on");
            PlayerBag a = haul.ServerBagOf(ca);
            Assert.That(a.BankOpen, Is.False);
            Assert.That(a.BankedThisDive.ContainsKey(112) && a.BankedThisDive.ContainsKey(104), Is.True, "the picked items are safe");
            Assert.That(a.Carried, Is.EqualTo(new[] { 101 }));
            Assert.That(haul.ServerPick(ca, 0), Is.False, "the visit is closed");

            Assert.That(haul.ServerAddItem(ca, 105), Is.True);
            Assert.That(haul.ServerBank(), Is.False, "carrying something does not reopen a used bank point");
        }

        // ---- PlayerBagSync (task 5) ----

        private static PlayerBagSync BagSyncOwnedBy(NetworkManager viewer, ulong owner)
        {
            foreach (NetworkObject o in viewer.SpawnManager.SpawnedObjects.Values)
                if (o != null && o.IsPlayerObject && o.OwnerClientId == owner) return o.GetComponent<PlayerBagSync>();
            return null;
        }

        // The host spawns a hero for the client and waits until the client owns it.
        private static IEnumerator SpawnHero(AppBootstrap app, PlayModeHarness.TestClient client, Vector3 pos)
        {
            NetworkManager hostNm = app.NetworkManager;
            NetworkManager cnm = client.NetworkManager;
            ulong id = cnm.LocalClientId;
            hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, id, false, true, false, pos, Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null && BagSyncOwnedBy(cnm, id) != null, Timeout, "client " + id + " never received its hero");
        }

        // Host + `clients` TestClients, a spawned DiveHaulSync, and one hero per peer.
        private static IEnumerator StartParty(int clients, List<PlayModeHarness.TestClient> into, System.Action<AppBootstrap, DiveHaulSync> done)
        {
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(clients, into, (a, h) => { app = a; haul = h; });
            NetworkManager hostNm = app.NetworkManager;
            hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, hostNm.LocalClientId, false, true, false, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < into.Count; i++) yield return SpawnHero(app, into[i], new Vector3(2f * (i + 1), 0f, 0f));
            done(app, haul);
        }

        private static bool Shows(PlayerBagSync s, params int[] ids)
        {
            if (s == null || s.View.Carried.Length != ids.Length) return false;
            for (int i = 0; i < ids.Length; i++) if (s.View.Carried[i] != ids[i]) return false;
            return true;
        }

        private static IEnumerator Settle(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        [UnityTest]
        public IEnumerator View_OnlyOwnerAndHostSeeIt_LateJoinerGetsOwn()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(2, clients, (a, h) => { app = a; haul = h; });
            NetworkManager hostNm = app.NetworkManager;
            NetworkManager nmA = clients[0].NetworkManager, nmB = clients[1].NetworkManager;
            ulong ca = nmA.LocalClientId, cb = nmB.LocalClientId;

            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            Assert.That(haul.ServerAddItem(cb, 110), Is.True);

            PlayerBagSync ownA = BagSyncOwnedBy(nmA, ca), ownB = BagSyncOwnedBy(nmB, cb);
            yield return PlayModeHarness.WaitUntil(() => Shows(ownA, 104), Timeout, "A's own view never showed [104]");
            yield return PlayModeHarness.WaitUntil(() => Shows(ownB, 110), Timeout, "B's own view never showed [110]");
            yield return Settle(0.5f);   // let anything B's bag could leak to A arrive before asserting it did not
            Assert.That(BagSyncOwnedBy(nmA, cb).View.Equals(default(BagView)), Is.True, "A's copy of B's view is default (owner-only read)");
            Assert.That(BagSyncOwnedBy(nmB, ca).View.Equals(default(BagView)), Is.True, "B's copy of A's view is default");

            Assert.That(Shows(BagSyncOwnedBy(hostNm, ca), 104), Is.True, "the host sees A's bag");
            Assert.That(Shows(BagSyncOwnedBy(hostNm, cb), 110), Is.True, "the host sees B's bag");

            // A late joiner gets its own bag with the spawn and nobody else's.
            yield return PlayModeHarness.StartClients(app, 1, false, clients, 30);
            PlayModeHarness.TestClient c = clients[2];
            NetworkManager nmC = c.NetworkManager;
            ulong cc = nmC.LocalClientId;
            Assert.That(haul.ServerAddItem(cc, 105), Is.True);
            yield return SpawnHero(app, c, new Vector3(8f, 0f, 0f));
            PlayerBagSync ownC = BagSyncOwnedBy(nmC, cc);
            yield return PlayModeHarness.WaitUntil(() => Shows(ownC, 105), Timeout, "the late joiner's own view never showed [105]");
            yield return PlayModeHarness.WaitUntil(() => BagSyncOwnedBy(nmC, ca) != null && BagSyncOwnedBy(nmC, cb) != null, Timeout, "the late joiner never saw the other heroes");
            yield return Settle(0.5f);
            Assert.That(BagSyncOwnedBy(nmC, ca).View.Equals(default(BagView)), Is.True, "C's copy of A's view is default");
            Assert.That(BagSyncOwnedBy(nmC, cb).View.Equals(default(BagView)), Is.True, "C's copy of B's view is default");
            Assert.That(Shows(BagSyncOwnedBy(nmA, ca), 104), Is.True, "A's own view is unchanged by the join");
        }

        [UnityTest]
        public IEnumerator Pick_ByRpcFromTheOwner_HostValidatesCapacity()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(1, clients, (a, h) => { app = a; haul = h; });   // two players: 4 slots, 2 each
            NetworkManager hostNm = app.NetworkManager, nmA = clients[0].NetworkManager;
            ulong host = hostNm.LocalClientId, ca = nmA.LocalClientId;

            Assert.That(haul.ServerBegin(new[] { 2, 2, 2 }), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.ServerSetFloor(2), Is.True, "floor 2 has a bank");
            Assert.That(haul.ServerAddItem(ca, 101), Is.True);
            Assert.That(haul.ServerAddItem(ca, 112), Is.True);
            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            Assert.That(haul.ServerAddItem(host, 103), Is.True);
            Assert.That(haul.ServerBagOf(ca).SlotCapacity, Is.EqualTo(2));

            PlayerBagSync ownA = BagSyncOwnedBy(nmA, ca);
            yield return PlayModeHarness.WaitUntil(() => Shows(ownA, 101, 112, 104) && ownA.View.BankOpen && ownA.View.SlotCapacity == 2, Timeout, "A's view never showed the three items and the open visit");

            PlayerBagSync hostSideA = BagSyncOwnedBy(hostNm, ca);
            ownA.RequestPick(0); ownA.RequestPick(1); ownA.RequestPick(2);
            yield return PlayModeHarness.WaitUntil(() => hostSideA.RequestsHandled >= 3, Timeout, "the host never handled the three pick requests");
            Assert.That(haul.ServerBagOf(ca).Picks, Is.EqualTo(new[] { 0, 1 }), "the third pick is refused on the host: only two slots");
            yield return PlayModeHarness.WaitUntil(() => ownA.View.Picks.Length == 2, Timeout, "A's view never showed two picks");
            Assert.That(ownA.View.Picks[0], Is.EqualTo((byte)0));
            Assert.That(ownA.View.Picks[1], Is.EqualTo((byte)1));

            // A pick sent through an object the sender does not own (A, on the host hero's component) is refused: it must not touch either bag.
            PlayerBagSync hostHeroOnA = BagSyncOwnedBy(nmA, host);
            PlayerBagSync hostHeroOnHost = BagSyncOwnedBy(hostNm, host);
            Assert.That(hostHeroOnA, Is.Not.Null);
            hostHeroOnA.RequestPick(0);
            yield return PlayModeHarness.WaitUntil(() => hostHeroOnHost.RequestsHandled >= 1, Timeout, "the host never handled the foreign pick request");
            Assert.That(haul.ServerBagOf(host).Picks, Is.Empty, "a pick from a peer that does not own the object is refused");
            Assert.That(haul.ServerBagOf(ca).Picks, Is.EqualTo(new[] { 0, 1 }), "and A's bag is untouched");

            // Unpick by RPC, then pick the other one.
            ownA.RequestUnpick(0);
            ownA.RequestPick(2);
            yield return PlayModeHarness.WaitUntil(() => hostSideA.RequestsHandled >= 5, Timeout, "the host never handled the unpick and the pick");
            Assert.That(haul.ServerBagOf(ca).Picks, Is.EqualTo(new[] { 1, 2 }));

            Assert.That(haul.ServerBank(), Is.True, "the party pushes on");
            yield return PlayModeHarness.WaitUntil(() => ownA.View.BankedThisDive == 2 && !ownA.View.BankOpen, Timeout, "A's view never showed the two banked items");
            Assert.That(Shows(ownA, 101), Is.True, "the unpicked item is still carried");
            Assert.That(ownA.View.Picks.Length, Is.EqualTo(0));
            Assert.That(haul.ServerBagOf(ca).BankedThisDive.ContainsKey(112) && haul.ServerBagOf(ca).BankedThisDive.ContainsKey(104), Is.True);
        }

        [UnityTest]
        public IEnumerator Rejoin_GetsTheSameBagBack()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(1, clients, (a, h) => { app = a; haul = h; });
            NetworkManager hostNm = app.NetworkManager;
            ulong oldId = clients[0].NetworkManager.LocalClientId;
            Assert.That(haul.ServerAddItem(oldId, 104), Is.True);
            Assert.That(haul.ServerAddGold(oldId, 9), Is.True);
            string pid = haul.PlayerIdOf(oldId);
            int slot = haul.PlayerSlotOf(oldId);
            Assert.That(pid, Is.EqualTo(clients[0].Key.ToString()), "the bag key is the roster's PlayerKey");
            PlayerBagSync own = BagSyncOwnedBy(clients[0].NetworkManager, oldId);
            yield return PlayModeHarness.WaitUntil(() => Shows(own, 104), Timeout, "the view never showed [104] before the drop");

            System.Threading.Tasks.Task leave = clients[0].Session.LeaveAsync();
            yield return PlayModeHarness.Await(leave, Timeout);
            yield return PlayModeHarness.WaitUntil(() => !hostNm.ConnectedClients.ContainsKey(oldId), Timeout, "the host still lists the dropped client");
            yield return PlayModeHarness.WaitUntil(() => BagSyncOwnedBy(hostNm, oldId) == null, Timeout, "the dropped hero is still spawned on the host");

            yield return PlayModeHarness.StartClients(app, 1, false, clients, 20);   // the same PlayerId, a new connection
            PlayModeHarness.TestClient back = clients[1];
            ulong newId = back.NetworkManager.LocalClientId;
            Assert.That(newId, Is.Not.EqualTo(oldId), "the rejoin got a new client id");
            Assert.That(haul.PlayerIdOf(newId), Is.EqualTo(pid), "same player key");
            Assert.That(haul.PlayerSlotOf(newId), Is.EqualTo(slot), "same player slot");
            Assert.That(haul.ServerBagOf(newId).Carried, Is.EqualTo(new[] { 104 }), "the host hands the old bag to the new connection");
            yield return SpawnHero(app, back, new Vector3(2f, 0f, 0f));
            PlayerBagSync ownBack = BagSyncOwnedBy(back.NetworkManager, newId);
            yield return PlayModeHarness.WaitUntil(() => Shows(ownBack, 104) && ownBack.View.DiveGold == 9, Timeout, "the rejoined player's view never showed the old bag");
        }
    }
}
