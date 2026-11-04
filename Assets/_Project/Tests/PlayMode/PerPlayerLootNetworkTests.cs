// Authority: test-only. Per-player loot (plan 2): the host keeps one PlayerBag per player inside DiveHaulSync (DiveState.Bags); pieces stay shared and go into the town stock the moment
// they are picked up (DiveDirector listens to DiveHaulSync.PieceAdded).
// Host + clients in one process (scene management off; the syncs and the director are dynamically spawned network prefabs from the owned list).
// Catches (HostKeepsOneBagPerPlayer_ItemsGoToTheRightBag): items landing in the wrong player's bag, the host's own bag collecting a client's loot, a bag key that is not the roster's PlayerKey
// (a rejoin would lose the bag), a client writing a bag, the no-roster slot order leaving 0-3. NOT covered: the owner-only view that shows the bag to its player (PlayerBagSync, task 5).
// Catches (PiecePickup_GoesToStockAtOnce_SurvivesFail): a piece waiting for the dive's end before it reaches the town stock, a failed dive taking a collected piece back, the Summary
// found/home counters. NOT covered: a real pickup walk (the test calls the same ServerAddPiece the pickup sphere calls) and a save/reload of the stock (TownStockSaveTests).
// Catches (BankFloor_OpensVisitsForEveryone_PicksAreValidated): a bank floor opening no visit or the wrong capacity, a pick without a visit, a repeated or surplus pick, a visit left open after
// the party pushes on, the used bank point reopening for a player who merely carries something. NOT covered: the downed-hero and range checks of ServerPick (no player objects here; the same BankAskerOk as the bank press, whose tests cover them in DiveSpineNetworkTests).
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
    }
}
