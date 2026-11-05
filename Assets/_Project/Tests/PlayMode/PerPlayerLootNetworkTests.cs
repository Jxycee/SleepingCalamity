// Authority: test-only. Per-player loot (plan 2): the host keeps one PlayerBag per player inside DiveHaulSync (the session's PartyBags); pieces stay shared and go into the town stock the moment
// they are picked up (DiveDirector listens to DiveHaulSync.PieceAdded).
// Host + clients in one process (scene management off; the syncs and the director are dynamically spawned network prefabs from the owned list).
// Catches (HostKeepsOneBagPerPlayer_ItemsGoToTheRightBag): items landing in the wrong player's bag, the host's own bag collecting a client's loot, a bag key that is not the roster's PlayerKey
// (a rejoin would lose the bag), a client writing a bag, the no-roster slot order leaving 0-3. NOT covered: the owner-only view that shows the bag to its player (PlayerBagSync, task 5).
// Catches (PiecePickup_GoesToStockAtOnce_SurvivesFail): a piece waiting for the dive's end before it reaches the town stock, a failed dive taking a collected piece back, the Summary
// found/home counters. NOT covered: a real pickup walk (the test calls the same ServerAddPiece the pickup sphere calls) and a save/reload of the stock (TownStockSaveTests).
// Catches (BankFloor_OpensVisitsForEveryone_PicksAreValidated): a bank floor opening no visit or the wrong capacity, a pick without a visit, a repeated or surplus pick, a visit left open after
// the party pushes on, the used bank point reopening for a player who merely carries something. NOT covered: the downed-hero and range checks of ServerPick (no player objects here; the same BankAskerOk as the bank press, whose tests cover them in DiveSpineNetworkTests).
// Catches (View_OnlyOwnerAndHostSeeIt_LateJoinerGetsOwn): the owner read filter on PlayerBagSync.Net (a client reading another player's bag), the host not seeing every bag, the owner's own view
// arriving after the hero spawned (also for a late joiner, whose bag was filled before its hero spawned). NOT covered: that the spawn payload itself carries the view (the join and the first
// host write can land in either order here, so a missing spawn sync would be hidden by the next write), the contents of the view beyond the carried ids (BagView EditMode tests), a real player scene.
// Catches (Pick_ByRpcFromTheOwner_HostValidatesCapacity): the pick RPC reaching the host and being re-validated there (capacity), a pick sent from a peer that does not own the object being applied,
// the view following the host's picks and the bank. NOT covered: the bank-range and downed-hero checks (no built floor / vitals in this rig; DiveSpineNetworkTests), UI input.
// Catches (Rejoin_GetsTheSameBagBack): a rejoin with the same PlayerId getting a new or empty bag, a changed player slot, the new owner's view missing the old bag. Runs with the host roster the app's
// session installs, so the bag key is the PlayerKey; a host without a roster would key by client id and lose the bag. NOT covered: a world save in between (SaveFlowTests).
// Catches (Chest_EachPlayerOpensOwnCopy_KeyChestStaysShared): a loot chest paying the first opener only, a second open by the same player paying again, the roll using the wrong player's slot, a loot chest
// setting the shared opened bit, the key chest paying each player a key (or a key landing in a bag), ChestOpened not firing for the refused opens, a full bag opening the chest and eating the item (plan 3: it is
// refused and only that player is told). Drives the real
// RequestOpenChestRpc path with PlayersOverride positions. NOT covered: the Interact input edge, real player positions and walls between players and chests (CellarContentsNetworkTests).
// Catches (Chest_LoweredOnlyForThePlayerWhoOpenedIt): a peer drawing a chest lowered for everyone (the shared bit), the per-NetworkManager PlayerBagSync lookup picking the wrong peer's view (several peers
// share this process), the Interact prompt (WouldUseInteract) still offering a chest the local player opened, the key chest not lowering for the whole party. NOT covered: a real renderer, the HUD line.
// Catches (BankFloor_LateJoiner_GetsAVisitOnItsFirstPick_AndWhenThePartyBanks): a client who joined after the bank floor was entered being unable to pick or to bank (no visit), the push-on skipping its
// carried items, a used bank point being reopened by a joiner. NOT covered: the downed-hero and range checks (no hero in this rig).
// Catches (LeavingTheBankFloor_ClosesEveryVisit_ViewDoesNotLie): a visit staying open after the party left for a floor with no bank (picks possible, BagView.BankOpen true), items lost on the way, the
// visit not reopening on return. NOT covered: the town/home exits, which close visits through GoHome/Fail (PlayerBagTests).
// Catches (ThreePlayers_ExtraSlotGoesToADifferentPlayerOnTheNextBankFloor): the 3-player capacities not being {2,1,1}, two owners of the extra slot, the same owner twice in a row. NOT covered: the choice among
// the other two (a seeded pick, BankSlotsTests), saving the previous owner (plan 3).
// Catches (ClientWithoutARosterKey_GetsNoBag): an RPC in flight from a client the roster just dropped (or never knew) creating a throwaway bag, adding loot or gold to it, or throwing on a pick. NOT covered:
// the no-roster fallback to the client id (tests without a session; there is no way to remove the roster in this rig).
// Catches (Save_BagsSurviveAWorldReload): a bag's gold, stash or waiting satchel not reaching the world file through the real host flow (DiveDirector.CaptureTown -> HostWorld -> WorldSaveFlow), the
// final save on leaving overwriting the file with a load-time copy, the restore on the next start not handing the bags back to the same PlayerKey (rejoining with a new client id), and a client-id
// fallback bag or an unrecorded player's bag reaching the file. NOT covered: a mid-dive save (EditMode BagsSave tests and TownSaves.Reconcile), a hand-edited or damaged file (BagsSaveTests), the
// real Hollowmere scene with its town hub, or a save-and-quit at a stair (plan 3).
// Catches (Satchel_*, Boots_*; plan 3 task 9): the lost satchel drawn for anyone but its owner, on another depth, or never picked up; another player's pick-up taking it; a late joiner missing the prop or getting a
// toast; the owner's boots not reaching its hero (HeroLocomotion.BootsOn) or reaching another hero. These prove the host's gating (the owner-only bag view each peer draws from, and the host's refusal), not the
// renderer: the prop is a local cube whose look is judged on the walk.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Networking;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class PerPlayerLootNetworkTests
    {
        private const float Timeout = 25f;

        private string _dir;   // the throwaway world library of a save test

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
            WorldSaveStore.PathOverrideForTests = null;
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

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

            int piece = PieceCatalog.FrameId;
            Assert.That(dir.Town.State.InStock(piece), Is.False, "test premise: not home yet");
            Assert.That(haul.ServerAddPiece(piece), Is.True);
            Assert.That(dir.Town.State.InStock(piece), Is.True, "the piece is in the town stock right after the pickup, not at the dive's end");
            Assert.That(haul.Current.FoundPieces, Is.EqualTo(1));

            Assert.That(dir.ServerAbandonDive(), Is.True, "the dive fails through the director's own end-of-dive path");
            Assert.That(dir.NetActive.Value, Is.False);
            Assert.That(dir.Town.State.InStock(piece), Is.True, "still there after the failed dive (no double stocking, no loss)");
            Assert.That(haul.Session.Pieces.IsFound(piece), Is.True, "and the floors never spawn it again");
            int stocked = 0; foreach (int p in dir.Town.State.StockPieces) if (p == piece) stocked++;
            Assert.That(stocked, Is.EqualTo(1));

            // A new dive: the piece stays home and stays counted as found.
            dir.SeedOverride = 5151u;
            Assert.That(dir.ServerStartDive(), Is.True);
            Assert.That(haul.Current.FoundPieces, Is.EqualTo(1), "found pieces stay found: the counter counts every dive's finds (plan 3 task 7)");
        }

        [UnityTest]
        public IEnumerator BankFloor_OpensVisitsForEveryone_PicksAreValidated()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(1, clients, (a, h) => { app = a; haul = h; });
            ulong host = app.NetworkManager.LocalClientId, ca = clients[0].NetworkManager.LocalClientId;

            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.ServerAddItem(ca, 101), Is.True);
            Assert.That(haul.ServerAddItem(ca, 112), Is.True);
            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            Assert.That(haul.ServerPick(ca, 0), Is.False, "no bank floor, no pick");
            Assert.That(haul.ServerBagOf(ca).BankOpen, Is.False);

            Assert.That(haul.BankRoomNow(), Is.True, "the cleared bank floor's bank press enters the bank room");
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

            Assert.That(haul.ServerChoosePushOn(host), Is.True, "one press opens the confirm window");
            Assert.That(haul.Session.BankChosenThisDive, Is.False, "two players: one press is not the party's choice");
            Assert.That(haul.ServerChoosePushOn(ca), Is.True, "the second player's press confirms");
            Assert.That(haul.Session.BankChosenThisDive, Is.True, "the party pushes on");
            PlayerBag a = haul.ServerBagOf(ca);
            Assert.That(a.BankOpen, Is.False);
            Assert.That(a.BankedThisDive.ContainsKey(112) && a.BankedThisDive.ContainsKey(104), Is.True, "the picked items are safe");
            Assert.That(a.Carried, Is.EqualTo(new[] { 101 }));
            Assert.That(haul.ServerPick(ca, 0), Is.False, "the visit is closed");

            Assert.That(haul.ServerAddItem(ca, 105), Is.True);
            Assert.That(haul.ServerEnterBankRoom(), Is.False, "carrying something does not reopen a used bank point");
            Assert.That(haul.ServerBagOf(host).Carried, Is.Empty, "the host had nothing to carry");
            Assert.That(haul.ServerBagOf(host).BankedThisDive, Is.Empty, "and the free slots stayed empty (plan 3 task 8: no auto-fill)");
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

            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.ServerAddItem(ca, 101), Is.True);
            Assert.That(haul.ServerAddItem(ca, 112), Is.True);
            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            Assert.That(haul.ServerAddItem(host, 103), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True, "the bank floor has a bank room");
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

            Assert.That(haul.PushOnNow(), Is.True, "the party pushes on");
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

        // ---- bank visits (task 4-5 review fixes) ----

        [UnityTest]
        public IEnumerator BankFloor_LateJoiner_GetsAVisitWhenThePartyChanges_AndNothingIsFilledAtThePushOn()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(1, clients, (a, h) => { app = a; haul = h; });
            ulong hostId = app.NetworkManager.LocalClientId;
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True, "the visits open for the host and A");
            yield return PlayModeHarness.StartClients(app, 1, false, clients, 30);   // C joins after the room was entered
            yield return PlayModeHarness.StartClients(app, 1, false, clients, 31);   // and D (another player id: the same id would take C's seat over)
            ulong cc = clients[1].NetworkManager.LocalClientId, cd = clients[2].NetworkManager.LocalClientId;

            Assert.That(haul.ServerAddItem(cc, 101), Is.True);
            Assert.That(haul.ServerAddItem(cc, 112), Is.True);
            Assert.That(haul.ServerAddItem(cd, 104), Is.True);
            yield return PlayModeHarness.WaitUntil(() => haul.ServerBagOf(cc).BankOpen && haul.ServerBagOf(cd).BankOpen, Timeout, "the room never gave the joiners a visit (the host resizes when the party changes)");
            Assert.That(haul.ServerBagOf(cc).SlotCapacity, Is.EqualTo(1), "four players now: one slot each");
            Assert.That(haul.ServerBagOf(hostId).SlotCapacity, Is.EqualTo(1), "and the others' visits shrank with the party");
            Assert.That(haul.ServerPick(cc, 0), Is.True);
            Assert.That(haul.ServerPick(cc, 1), Is.False, "the visit holds one pick");
            PlayerBag c = haul.ServerBagOf(cc);

            Assert.That(haul.PushOnNow(), Is.True, "the party pushes on");
            Assert.That(c.BankedThisDive.ContainsKey(101), Is.True, "C's pick is safe");
            Assert.That(c.Carried, Is.EqualTo(new[] { 112 }));
            PlayerBag d = haul.ServerBagOf(cd);
            Assert.That(d.BankedThisDive, Is.Empty, "D never picked: its free slot stayed empty (no auto-fill)");
            Assert.That(d.Carried, Is.EqualTo(new[] { 104 }));
            Assert.That(d.BankOpen, Is.False);
            Assert.That(haul.ServerAddItem(cc, 105), Is.True);
            Assert.That(haul.ServerPick(cc, 1), Is.False, "the bank point is used: a joiner cannot reopen it");
            Assert.That(haul.ServerBagOf(cc).BankOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator LeavingTheBankFloor_ClosesEveryVisit_ViewDoesNotLie()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(1, clients, (a, h) => { app = a; haul = h; });
            NetworkManager nmA = clients[0].NetworkManager;
            ulong host = app.NetworkManager.LocalClientId, ca = nmA.LocalClientId;
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.ServerAddItem(ca, 101), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            Assert.That(haul.ServerPick(ca, 0), Is.True);
            PlayerBagSync ownA = BagSyncOwnedBy(nmA, ca);
            yield return PlayModeHarness.WaitUntil(() => ownA.View.BankOpen && ownA.View.Picks.Length == 1, Timeout, "A's view never showed the open visit and its pick");

            Assert.That(haul.ServerSetFloor(6), Is.True, "floor 6 has no bank room");
            foreach (ulong id in new[] { host, ca })
            {
                Assert.That(haul.ServerBagOf(id).BankOpen, Is.False, "the visit closed for " + id);
                Assert.That(haul.ServerBagOf(id).Picks, Is.Empty);
            }
            Assert.That(haul.ServerBagOf(ca).Carried, Is.EqualTo(new[] { 101 }), "the picked item is still carried, nothing was banked");
            Assert.That(haul.ServerBagOf(ca).BankedThisDive, Is.Empty);
            yield return PlayModeHarness.WaitUntil(() => !ownA.View.BankOpen && ownA.View.Picks.Length == 0 && ownA.View.SlotCapacity == 0, Timeout, "A's view still showed a bank visit on a floor without a bank");
            Assert.That(haul.ServerPick(ca, 0), Is.False, "no bank here");

            Assert.That(haul.BankRoomNow(), Is.True, "the bank room opens again when the party returns and presses the bank");
            Assert.That(haul.ServerBagOf(ca).BankOpen, Is.True);
            yield return PlayModeHarness.WaitUntil(() => ownA.View.BankOpen, Timeout, "A's view never showed the reopened visit");
        }

        [UnityTest]
        public IEnumerator ThreePlayers_ExtraSlotGoesToADifferentPlayerOnTheNextBankFloor()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(2, clients, (a, h) => { app = a; haul = h; });
            ulong[] ids = { app.NetworkManager.LocalClientId, clients[0].NetworkManager.LocalClientId, clients[1].NetworkManager.LocalClientId };
            haul.DiveSeed = 424242u;
            Assert.That(haul.ServerBegin(), Is.True);

            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            int owner1 = ExtraSlotOwner(haul, ids, "first bank room");
            Assert.That(haul.ServerSetFloor(6), Is.True);
            haul.Session.Restore(new DiveSession.SnapshotData(DivePhase.Floor, 5, 0b11111, false, false, -1, haul.Session.Stir.Capture(), haul.Session.Momentum.Capture(), haul.Session.Retry.Capture()));   // back on the cleared bank floor, the choice not made
            haul.DiveSeed = 424243u;
            Assert.That(haul.ServerEnterBankRoom(), Is.True);
            int owner2 = ExtraSlotOwner(haul, ids, "next bank room");
            Assert.That(owner2, Is.Not.EqualTo(owner1), "the extra slot never goes to the previous owner twice in a row");
        }

        // The capacities of three players are {2,1,1}: returns the index (in ids) of the one with two slots.
        private static int ExtraSlotOwner(DiveHaulSync haul, ulong[] ids, string where)
        {
            var caps = new List<int>();
            for (int i = 0; i < ids.Length; i++) { Assert.That(haul.ServerBagOf(ids[i]).BankOpen, Is.True, where + ": visit for player " + i); caps.Add(haul.ServerBagOf(ids[i]).SlotCapacity); }
            caps.Sort();
            Assert.That(caps, Is.EqualTo(new[] { 1, 1, 2 }), where + ": capacities");
            int owner = -1;
            for (int i = 0; i < ids.Length; i++) if (haul.ServerBagOf(ids[i]).SlotCapacity == 2) owner = i;
            return owner;
        }

        [UnityTest]
        public IEnumerator ClientWithoutARosterKey_GetsNoBag()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartHaul(1, clients, (a, h) => { app = a; haul = h; });
            NetworkManager hostNm = app.NetworkManager;
            ulong host = hostNm.LocalClientId, oldId = clients[0].NetworkManager.LocalClientId;
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            Assert.That(app.Roster.TryGetKey(host, out PlayerKey hostKey), Is.True, "the host is in its own roster (else a solo player's bag would be keyed by client id and never saved)");
            Assert.That(haul.PlayerIdOf(host), Is.EqualTo(hostKey.ToString()), "the host's bag key is its roster PlayerKey string, not the client-id fallback");
            Assert.That(haul.ServerAddItem(oldId, 104), Is.True);
            int bags = CountBags(haul);

            // A client id the roster never knew: an RPC from nowhere.
            const ulong stranger = 777;
            Assert.That(haul.PlayerIdOf(stranger), Is.Null, "the roster has no key for it");
            Assert.That(haul.ServerBagOf(stranger), Is.Null);
            Assert.That(haul.ServerAddItem(stranger, 101), Is.False);
            Assert.That(haul.ServerAddGold(stranger, 3), Is.False);
            Assert.That(haul.ServerPick(stranger, 0), Is.False, "a pick for a bagless client is refused, not thrown");
            Assert.That(haul.ServerUnpick(stranger, 0), Is.False);
            Assert.That(CountBags(haul), Is.EqualTo(bags), "no throwaway bag was made");

            // A client that just dropped: the roster marks it disconnected, then a late RPC arrives.
            System.Threading.Tasks.Task leave = clients[0].Session.LeaveAsync();
            yield return PlayModeHarness.Await(leave, Timeout);
            yield return PlayModeHarness.WaitUntil(() => !hostNm.ConnectedClients.ContainsKey(oldId) && haul.PlayerIdOf(oldId) == null, Timeout, "the host still lists the dropped client");
            Assert.That(haul.ServerAddItem(oldId, 105), Is.False, "loot for a client that just left is refused");
            Assert.That(haul.ServerAddGold(oldId, 5), Is.False);
            Assert.That(haul.ServerPick(oldId, 0), Is.False);
            Assert.That(CountBags(haul), Is.EqualTo(bags), "and no bag appears for it");
            Assert.That(haul.ServerBagOf(host), Is.Not.Null, "the host keeps its own");
        }

        private static int CountBags(DiveHaulSync haul)
        {
            int n = 0;
            foreach (KeyValuePair<string, PlayerBag> kv in haul.Bags.All) n++;
            return n;
        }

        // ---- per-player chests (task 6) ----

        private sealed class ChestRig
        {
            public AppBootstrap App; public NetworkManager HostNm;
            public CellarFloorSync Floor; public DiveHaulSync Haul; public CellarContentsSync Contents, PeerA, PeerB;
            public CellarFloorLayout Layout; public uint Seed;
            public ulong Host, A, B;
            public readonly Dictionary<ulong, Vector3> Players = new Dictionary<ulong, Vector3>();
            public readonly List<(int Chest, int Item)> Opened = new List<(int, int)>();
        }

        private const int ChestFloor = 2;   // floor 1 is the authored floor (no locked room, carrier or key chest since plan 3 task 4); floor 2 is the first generated one

        // A seed whose floor 2 has a locked room with a gate, a key chest and a carrier (the same premise as CellarContentsNetworkTests).
        private static uint FindSeed()
        {
            for (uint s = 1; s < 400; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, ChestFloor);
                if (l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0) return s;
            }
            Assert.Fail("no seed with a locked room, a carrier and a key chest"); return 0;
        }

        private static CellarContentsSync ContentsOf(NetworkManager nm, ulong id)
            => nm.SpawnManager.SpawnedObjects[id].GetComponent<CellarContentsSync>();

        // Host + 2 clients, each with a hero (so each has its own PlayerBagSync), a floor built 200 m east, the haul and the contents; every peer's contents points at its own floor sync.
        private static IEnumerator StartChestRig(List<PlayModeHarness.TestClient> clients, System.Action<ChestRig> done)
        {
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(2, clients, (a, h) => { app = a; haul = h; });
            var r = new ChestRig { App = app, HostNm = app.NetworkManager, Haul = haul };
            NetworkManager hostNm = r.HostNm;
            r.Host = hostNm.LocalClientId; r.A = clients[0].NetworkManager.LocalClientId; r.B = clients[1].NetworkManager.LocalClientId;
            NetworkObject floorObj = hostNm.SpawnManager.InstantiateAndSpawn(Prefab<CellarFloorSync>(hostNm), hostNm.LocalClientId, false, false, false, new Vector3(200f, 0f, 0f), Quaternion.identity);
            NetworkObject contentsObj = hostNm.SpawnManager.InstantiateAndSpawn(Prefab<CellarContentsSync>(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            r.Floor = floorObj.GetComponent<CellarFloorSync>(); r.Contents = contentsObj.GetComponent<CellarContentsSync>();
            ulong contentsId = contentsObj.NetworkObjectId;
            foreach (PlayModeHarness.TestClient c in clients)
            {
                NetworkManager cnm = c.NetworkManager;
                yield return PlayModeHarness.WaitUntil(() => cnm.SpawnManager.SpawnedObjects.ContainsKey(contentsId), Timeout, "a client never received the contents object");
                CellarContentsSync peer = ContentsOf(cnm, contentsId);
                // The peer's own floor sync: its Resolve() may already have picked the host's (FindFirstObjectByType), so the pointer is set unconditionally.
                CellarFloorSync own = null;
                float until = Time.realtimeSinceStartup + Timeout;
                while (own == null && Time.realtimeSinceStartup < until)
                {
                    foreach (CellarFloorSync f in Object.FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (f.NetworkManager == cnm) own = f;
                    if (own == null) { PlayModeHarness.Pace(); yield return null; }
                }
                Assert.That(own, Is.Not.Null, "a client has its own floor sync");
                peer.FloorSync = own;
                if (c == clients[0]) r.PeerA = peer; else r.PeerB = peer;
            }
            r.Contents.FloorSync = r.Floor; r.Contents.HaulSync = r.Haul;
            r.Contents.PlayersOverride = list => { foreach (KeyValuePair<ulong, Vector3> kv in r.Players) list.Add(new CellarContentsSync.PlayerPos { ClientId = kv.Key, Position = kv.Value }); };
            r.Contents.ChestOpened += (chest, item) => r.Opened.Add((chest, item));
            r.Seed = FindSeed();
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(ChestFloor), Is.True);
            Assert.That(r.Floor.ServerSetFloor(r.Seed, ChestFloor), Is.True);
            yield return PlayModeHarness.WaitUntil(() => r.PeerA.FloorSync.Built != null && r.PeerA.FloorSync.BuiltFloor == ChestFloor && r.PeerB.FloorSync.Built != null && r.PeerB.FloorSync.BuiltFloor == ChestFloor
                && r.Floor.Built != null && r.Contents.PieceCount > 0, Timeout * 2, "the floors were not built");
            r.Layout = r.Floor.Layout;
            done(r);
        }

        // Every player stands next to chest `chest`, then `peer` (that player's own client) asks to open it through the real RPC; returns when the host has handled the request.
        private static IEnumerator Ask(ChestRig r, CellarContentsSync peer, int chest)
        {
            Assert.That(r.Contents.TryChestPosition(chest, out Vector3 at), Is.True, "chest " + chest + " exists");
            r.Players[r.Host] = at + new Vector3(1f, 0f, 0f); r.Players[r.A] = at + new Vector3(1f, 0f, 0f); r.Players[r.B] = at + new Vector3(1f, 0f, 0f);
            int handled = r.Contents.ChestRequestsHandled;
            peer.RequestOpenChest(chest);
            yield return PlayModeHarness.WaitUntil(() => r.Contents.ChestRequestsHandled > handled, Timeout, "the host never handled the open request for chest " + chest);
        }

        // A loot chest where players A and B roll different items (so "own copy" is visible), else the first loot chest.
        private static int PickLootChest(ChestRig r)
        {
            int slotA = r.Haul.PlayerSlotOf(r.A), slotB = r.Haul.PlayerSlotOf(r.B), first = -1;
            for (int i = 0; i < r.Layout.Chests.Count && i < 64; i++)
            {
                if (r.Layout.Chests[i].HoldsKey) continue;
                if (first < 0) first = i;
                if (CellarLoot.RollChest(r.Seed, ChestFloor, i, slotA) != CellarLoot.RollChest(r.Seed, ChestFloor, i, slotB)) return i;
            }
            Assert.Fail("premise: no loot chest on floor " + ChestFloor + " of seed " + r.Seed + " rolls different items for slots " + slotA + " and " + slotB + " (first loot chest " + first + ")");
            return -1;
        }

        [UnityTest]
        public IEnumerator Chest_EachPlayerOpensOwnCopy_KeyChestStaysShared_FullBagIsRefused()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            int i = PickLootChest(r), key = r.Layout.KeyChestIndex;
            Assert.That(key, Is.GreaterThanOrEqualTo(0));
            int rollA = CellarLoot.RollChest(r.Seed, ChestFloor, i, r.Haul.PlayerSlotOf(r.A)), rollB = CellarLoot.RollChest(r.Seed, ChestFloor, i, r.Haul.PlayerSlotOf(r.B));
            Assert.That(rollA, Is.Not.EqualTo(rollB), "premise: the chosen chest rolls different items for A and B");

            yield return Ask(r, r.PeerA, i);
            Assert.That(r.Haul.ServerBagOf(r.A).Carried, Is.EqualTo(new[] { rollA }), "A's bag holds A's own roll");
            Assert.That(r.Haul.ServerBagOf(r.B).Carried, Is.Empty);
            Assert.That(r.Opened, Is.EqualTo(new[] { (i, rollA) }), "ChestOpened fired once");
            Assert.That(r.Contents.LastLootItem, Is.EqualTo(rollA));
            Assert.That(r.Contents.State.Chests, Is.EqualTo(0UL), "a loot chest never sets the shared bit");

            yield return Ask(r, r.PeerA, i);
            Assert.That(r.Haul.ServerBagOf(r.A).Carried, Is.EqualTo(new[] { rollA }), "A cannot open the same chest twice");
            Assert.That(r.Opened.Count, Is.EqualTo(1), "the refused open raised no event");

            yield return Ask(r, r.PeerB, i);
            Assert.That(r.Haul.ServerBagOf(r.B).Carried, Is.EqualTo(new[] { rollB }), "B opens the same chest and gets B's own roll");
            Assert.That(r.Haul.ServerBagOf(r.A).Carried, Is.EqualTo(new[] { rollA }), "A's bag is unchanged");
            Assert.That(r.Opened, Is.EqualTo(new[] { (i, rollA), (i, rollB) }));
            Assert.That(r.Contents.State.Chests, Is.EqualTo(0UL));

            // The key chest is one for the party.
            yield return Ask(r, r.PeerA, key);
            Assert.That(r.Contents.State.RoomKeyHeld, Is.True, "A's open gave the room key");
            Assert.That((r.Contents.State.Chests & (1UL << key)) != 0, Is.True, "and set the shared bit");
            Assert.That(r.Opened[r.Opened.Count - 1], Is.EqualTo((key, CellarLoot.RoomKeyId)));
            int events = r.Opened.Count;
            yield return Ask(r, r.PeerB, key);
            Assert.That(r.Opened.Count, Is.EqualTo(events), "B's open of the key chest is refused: one key for the party");
            Assert.That(r.Haul.ServerBagOf(r.A).Carried, Is.EqualTo(new[] { rollA }), "no key in A's bag");
            Assert.That(r.Haul.ServerBagOf(r.B).Carried, Is.EqualTo(new[] { rollB }), "no key in B's bag");

            // A FULL bag refuses the chest (plan 3 decision 6): it stays closed for that player, nothing is lost, and only that player is told (an owner-only RPC). A purse would still be taken (no slot),
            // so the chest is one whose host roll is an item.
            int hostSlot = r.Haul.PlayerSlotOf(r.Host), full = -1;
            for (int k = 0; k < r.Layout.Chests.Count && k < 64 && full < 0; k++)
                if (!r.Layout.Chests[k].HoldsKey && k != i && CellarLoot.RollChest(r.Seed, ChestFloor, k, hostSlot) != CellarLoot.GoldId) full = k;
            Assert.That(full, Is.GreaterThanOrEqualTo(0), "premise: a second loot chest that pays the host an item");
            for (int n = 0; n < PlayerBag.MaxCarried; n++) Assert.That(r.Haul.ServerAddItem(r.Host, 101), Is.True);
            int openedBefore = r.Opened.Count;
            yield return Ask(r, r.Contents, full);
            Assert.That(r.Haul.ServerBagOf(r.Host).ChestOpened(r.Seed, ChestFloor, full), Is.False, "a full bag leaves the chest closed");
            Assert.That(r.Haul.ServerBagOf(r.Host).Carried.Count, Is.EqualTo(PlayerBag.MaxCarried));
            Assert.That(r.Opened.Count, Is.EqualTo(openedBefore), "a refused open raises no ChestOpened");
            yield return PlayModeHarness.WaitUntil(() => r.Contents.NoticeCount == 1, Timeout, "the host's player was never told its bag is full");
            Assert.That(r.Contents.LastNotice, Is.EqualTo(CellarContentsSync.BagFullText));
            yield return Settle(0.5f);
            Assert.That(r.PeerA.NoticeCount + r.PeerB.NoticeCount, Is.EqualTo(0), "the other players' peers heard nothing");
        }

        [UnityTest]
        public IEnumerator Chest_LoweredOnlyForThePlayerWhoOpenedIt()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            int i = PickLootChest(r), key = r.Layout.KeyChestIndex;
            Assert.That(CellarLoot.RollChest(r.Seed, ChestFloor, i, r.Haul.PlayerSlotOf(r.A)), Is.Not.EqualTo(CellarLoot.RollChest(r.Seed, ChestFloor, i, r.Haul.PlayerSlotOf(r.B))), "premise: the chosen chest rolls different items for A and B");
            Assert.That(r.Contents.TryChestPosition(i, out Vector3 at), Is.True);
            Assert.That(r.PeerA.ChestShownOpen(i) || r.PeerB.ChestShownOpen(i), Is.False, "premise: closed everywhere");

            yield return Ask(r, r.PeerA, i);
            NetworkManager nmA = clients[0].NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => { PlayerBagSync v = PlayerBagSync.ForLocalPlayer(nmA); return v != null && (v.View.OpenedChests & (1UL << i)) != 0; }, Timeout,
                "A's own bag view never showed chest " + i + " opened (view: " + (PlayerBagSync.ForLocalPlayer(nmA) == null ? "no local player bag" : "seed " + PlayerBagSync.ForLocalPlayer(nmA).View.ChestSeed + " floor " + PlayerBagSync.ForLocalPlayer(nmA).View.ChestFloor + " mask " + PlayerBagSync.ForLocalPlayer(nmA).View.OpenedChests) + ", seed " + r.Seed + ")");
            yield return PlayModeHarness.WaitUntil(() => r.PeerA.ChestShownOpen(i), Timeout, "A's client never lowered the chest A opened (its view had the bit; state seed " + r.PeerA.State.Seed + " floor " + r.PeerA.State.Floor + "; view seed " + PlayerBagSync.ForLocalPlayer(nmA).View.ChestSeed + " floor " + PlayerBagSync.ForLocalPlayer(nmA).View.ChestFloor
                + "; ChestOpenForLocal " + r.PeerA.ChestOpenForLocal(i) + "; peer NM is client A's: " + (r.PeerA.NetworkManager == nmA) + "; built " + r.PeerA.FloorSync.BuiltSeed + "/" + r.PeerA.FloorSync.BuiltFloor + ")");
            yield return Settle(0.5f);   // let anything that would lower it for B arrive before asserting it did not
            Assert.That(r.PeerB.ChestShownOpen(i), Is.False, "B's client still shows the chest closed");
            Assert.That(r.Contents.ChestShownOpen(i), Is.False, "and so does the host's own player's view");
            Assert.That(r.PeerA.ChestOpenForLocal(i), Is.True);
            Assert.That(r.PeerB.ChestOpenForLocal(i), Is.False);

            // The Interact prompt skips a chest that is already open for the local player.
            Vector3 beside = at + new Vector3(1f, 0f, 0f);
            Assert.That(r.PeerA.WouldUseInteract(beside), Is.False, "A has nothing left to open here");
            Assert.That(r.PeerB.WouldUseInteract(beside), Is.True, "B still can");

            yield return Ask(r, r.PeerB, i);
            yield return PlayModeHarness.WaitUntil(() => r.PeerB.ChestShownOpen(i), Timeout, "B's client never lowered the chest B opened");
            Assert.That(r.PeerB.WouldUseInteract(beside), Is.False);

            // The key chest is shared: once anyone opens it, every client shows it lowered.
            yield return Ask(r, r.PeerA, key);
            yield return PlayModeHarness.WaitUntil(() => r.PeerA.ChestShownOpen(key) && r.PeerB.ChestShownOpen(key), Timeout, "the key chest did not lower on both clients");
        }

        // ---- save round trip (task 7) ----

        private static string Stamp(string path) => File.Exists(path) ? new FileInfo(path).Length + ":" + File.GetLastWriteTimeUtc(path).Ticks : "absent";

        private static WorldSave ReadWorld(string dir, string worldId)
            => JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(dir, worldId + ".json")), w => w.SchemaVersion);

        // A direct host (no scenes), one client with PlayerId(clientIndex), and a configured director.
        private static IEnumerator StartSavedHost(int clientIndex, List<PlayModeHarness.TestClient> clients, System.Action<AppBootstrap, DiveDirector> done)
        {
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            yield return PlayModeHarness.StartClients(app, 1, false, clients, clientIndex);
            NetworkManager hostNm = app.NetworkManager;
            NetworkObject o = hostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveDirector dir = o.GetComponent<DiveDirector>();
            Assert.That(dir.ServerConfigure(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            done(app, dir);
        }

        [UnityTest]
        public IEnumerator Save_BagsSurviveAWorldReload()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            string realPath = WorldSaveStore.PathForThisInstance();
            string realBefore = Stamp(realPath);
            WorldSaveStore.PathOverrideForTests = Path.Combine(_dir, "legacy-single-file", "world.json");   // the old single file: never the user's real world.json

            // ---- session 1: A banks 5 gold and item 104 at home, then fails a dive on floor 2 carrying item 110
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveDirector dir = null;
            yield return StartSavedHost(20, clients, (a, d) => { app = a; dir = d; });
            string worldId = app.WorldSaveFlow.SelectedWorldId;
            Assert.That(worldId, Is.Not.Null, "the session opened a world in the throwaway library");
            ulong ca = clients[0].NetworkManager.LocalClientId;
            string aKey = clients[0].Key.ToString();

            dir.SeedOverride = 6101u;
            Assert.That(dir.ServerStartDive(), Is.True);
            DiveHaulSync haul = dir.HaulSync;
            Assert.That(haul.PlayerIdOf(ca), Is.EqualTo(aKey), "premise: A's bag is keyed by the roster's PlayerKey");
            Assert.That(haul.ServerAddGold(ca, 5), Is.True);
            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            // Two bags that must never reach the file: a client-id fallback id, and a well-formed key of a player the world does not record.
            string unrecorded = PlayerKey.Local(PlayModeHarness.PlayerId(30)).ToString();
            haul.Bags.For("777").AddGold(3);
            haul.Bags.For(unrecorded).AddGold(4);
            haul.Bags.GoHomeAll();   // the bags come home the way the bank room's go-home does (plan 3 task 12 ports this test to the real flow)
            Assert.That(dir.ServerAbandonDive(), Is.True, "closes the dive (nothing is carried and no dive gold is left, so it changes no bag)");
            PlayerBag a = haul.ServerBagOf(ca);
            Assert.That(a.Gold, Is.EqualTo(5));
            Assert.That(a.Stash, Is.EqualTo(new Dictionary<int, int> { { 104, 1 } }));
            Assert.That(a.Satchel.Waiting, Is.Null, "premise: no satchel yet");

            dir.SeedOverride = 6102u;
            Assert.That(dir.ServerStartDive(), Is.True);
            Assert.That(haul.ServerAddItem(ca, 110), Is.True);
            Assert.That(dir.ServerAbandonDive(), Is.True, "the dive fails on floor 1 with 110 carried");
            Assert.That(a.Satchel.Waiting, Is.Not.Null);
            Assert.That(a.Satchel.Waiting.Floor, Is.EqualTo(1));
            Assert.That(a.Satchel.Waiting.Items, Is.EqualTo(new[] { 110 }));

            app.WorldSaveFlow.SaveNow();
            AssertFileHoldsOnlyA(ReadWorld(_dir, worldId), "after SaveNow");

            // ---- the world is closed (the final save on leaving must not undo it) and reopened
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            if (clients[0].Session.State != ConnState.Offline) yield return PlayModeHarness.Await(clients[0].Session.LeaveAsync(), Timeout);
            AssertFileHoldsOnlyA(ReadWorld(_dir, worldId), "after leaving");

            // ---- session 2: the same world, the same player under a new connection
            var again = new List<PlayModeHarness.TestClient>();
            AppBootstrap app2 = null; DiveDirector dir2 = null;
            yield return StartSavedHost(20, again, (x, d) => { app2 = x; dir2 = d; });
            Assert.That(app2.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "the same world reopened");
            ulong ca2 = again[0].NetworkManager.LocalClientId;
            dir2.SeedOverride = 6103u;
            Assert.That(dir2.ServerStartDive(), Is.True);
            DiveHaulSync haul2 = dir2.HaulSync;
            Assert.That(haul2.PlayerIdOf(ca2), Is.EqualTo(aKey), "the rejoined player has the same bag key");
            PlayerBag back = haul2.ServerBagOf(ca2);
            Assert.That(back.Gold, Is.EqualTo(5), "A's gold");
            Assert.That(back.Stash, Is.EqualTo(new Dictionary<int, int> { { 104, 1 } }), "A's stash");
            Assert.That(back.Satchel.Waiting, Is.Not.Null, "A's waiting satchel");
            Assert.That(back.Satchel.Waiting.Floor, Is.EqualTo(1));
            Assert.That(back.Satchel.Waiting.Items, Is.EqualTo(new[] { 110 }));
            Assert.That(back.Carried, Is.Empty);
            Assert.That(haul2.Bags.TryGet("777", out _), Is.False, "the fallback bag was not saved");
            Assert.That(haul2.Bags.TryGet(unrecorded, out _), Is.False, "the unrecorded player's bag was not saved");

            Assert.That(Stamp(realPath), Is.EqualTo(realBefore), "the real world.json was not touched");
        }

        private static void AssertFileHoldsOnlyA(WorldSave file, string when)
        {
            Assert.That(file, Is.Not.Null, when + ": the world file reads");
            Assert.That(file.Bags, Is.Not.Null, when + ": the bags section was written");
            Assert.That(file.Bags.Players.Count, Is.EqualTo(1), when + ": only the recorded player with something to keep (no fallback or unrecorded bag, no empty host bag)");
            PlayerBagSave p = file.Bags.Players[0];
            Assert.That(p.Identity, Is.EqualTo((int)IdentityKind.Local), when);
            Assert.That(p.Id, Is.EqualTo(PlayModeHarness.PlayerId(20)), when);
            Assert.That(p.Gold, Is.EqualTo(5), when);
            Assert.That(p.Stash.Count, Is.EqualTo(1), when);
            Assert.That((p.Stash[0].ItemId, p.Stash[0].Count), Is.EqualTo((104, 1)), when);
            Assert.That(p.Satchel, Is.Not.Null, when + ": the waiting satchel");
            Assert.That(p.Satchel.Floor, Is.EqualTo(1), when);
            Assert.That(p.Satchel.Items, Is.EqualTo(new[] { 110 }), when);
        }

        // ---- plan 3 task 8: the shared choice with real heroes, and the owner's view following a roster key ----
        // Catches (BankRoom_ConfirmWindow_SecondPressConfirms_OtherChoiceSwaps_DownedNotCounted): one press confirming alone in a party of three, a second player's press not confirming, the other choice not
        // swapping the window (or keeping the old presser), a downed hero's press being accepted, a downed hero keeping the quorum from one living player. NOT covered: the window timer (DiveHaulNetworkTests),
        // the bank cell range (no built floor in this rig).
        // Catches (PlayerBagSync_BagId_FollowsRosterKeyChange): the owner's view staying on the bag of the key it first resolved after the roster's key for it changed. NOT covered: a real Solo-world rebind.

        [UnityTest]
        public IEnumerator BankRoom_ConfirmWindow_SecondPressConfirms_OtherChoiceSwaps_DownedNotCounted()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(2, clients, (a, h) => { app = a; haul = h; });
            NetworkManager hostNm = app.NetworkManager;
            ulong host = hostNm.LocalClientId, ca = clients[0].NetworkManager.LocalClientId, cb = clients[1].NetworkManager.LocalClientId;
            haul.ChoiceWindowSeconds = 120f;
            int wentHome = 0; haul.WentHome += () => wentHome++;
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);

            Assert.That(haul.ServerChoosePushOn(host), Is.True);
            Assert.That(haul.Current.ChoiceKind, Is.EqualTo(BankChoiceKind.PushOn));
            Assert.That(haul.Current.ChoiceMask, Is.EqualTo(1 << haul.PlayerSlotOf(host)));
            Assert.That(haul.Session.BankChosenThisDive, Is.False, "three players: one press opens a window, it does not decide");

            Assert.That(haul.ServerChooseGoHome(ca), Is.True, "A presses the other choice: the window swaps");
            Assert.That(haul.Current.ChoiceKind, Is.EqualTo(BankChoiceKind.GoHome));
            Assert.That(haul.Current.ChoiceMask, Is.EqualTo(1 << haul.PlayerSlotOf(ca)), "the old presser's press is gone");
            Assert.That(haul.ServerChooseGoHome(ca), Is.False, "the same player again");
            Assert.That(wentHome, Is.EqualTo(0));

            Assert.That(haul.ServerChooseGoHome(host), Is.True, "a second player's press for the same choice confirms");
            Assert.That(wentHome, Is.EqualTo(1));
            Assert.That(haul.Session.Ended, Is.EqualTo(DiveEnd.WentHome));
            Assert.That(haul.Current.ChoiceKind, Is.EqualTo(BankChoiceKind.None));

            // a new dive in the bank room: B and A are down, only the host is alive
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.BankRoomNow(), Is.True);
            HeroVitals hvA = BagSyncOwnedBy(hostNm, ca).GetComponent<HeroVitals>(), hvB = BagSyncOwnedBy(hostNm, cb).GetComponent<HeroVitals>();
            hvA.ServerDamage(100000); hvB.ServerDamage(100000);
            Assert.That(hvA.IsUp || hvB.IsUp, Is.False, "premise: both clients' heroes are down");
            Assert.That(haul.ServerChoosePushOn(ca), Is.False, "a downed hero's press is refused");
            Assert.That(haul.ServerChoosePushOn(host), Is.True);
            Assert.That(haul.Session.BankChosenThisDive, Is.True, "one living player is the whole quorum: the downed are not counted");
        }

        private sealed class FakeIdentity : IJoinContextProvider
        {
            public readonly Dictionary<ulong, PlayerKey> Keys = new Dictionary<ulong, PlayerKey>();
            public bool JoinsOpen => true;
            public void GetRoster(List<RosterEntry> into) { into.Clear(); }
            public bool TryGetKey(ulong clientId, out PlayerKey key) => Keys.TryGetValue(clientId, out key);
            public bool TryGetSlot(ulong clientId, out byte slot) { slot = 0; return false; }
        }

        [UnityTest]
        public IEnumerator PlayerBagSync_BagId_FollowsRosterKeyChange()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            AppBootstrap app = null; DiveHaulSync haul = null;
            yield return StartParty(1, clients, (a, h) => { app = a; haul = h; });
            NetworkManager nmA = clients[0].NetworkManager;
            ulong ca = nmA.LocalClientId;
            var identity = new FakeIdentity();
            PlayerKey first = PlayerKey.Local(PlayModeHarness.PlayerId(40)), second = PlayerKey.Local(PlayModeHarness.PlayerId(41));
            identity.Keys[ca] = first;
            haul.Identity = identity;
            Assert.That(haul.PlayerIdOf(ca), Is.EqualTo(first.ToString()));
            Assert.That(haul.ServerAddItem(ca, 104), Is.True);
            PlayerBagSync ownA = BagSyncOwnedBy(nmA, ca);
            yield return PlayModeHarness.WaitUntil(() => Shows(ownA, 104), Timeout, "A's view never showed its bag under the first key");

            identity.Keys[ca] = second;   // the roster now knows the owner under another key (a Solo world rebinding its owner, say)
            Assert.That(haul.PlayerIdOf(ca), Is.EqualTo(second.ToString()));
            Assert.That(haul.ServerAddItem(ca, 105), Is.True, "the host now fills the bag of the new key");
            Assert.That(haul.Bags.TryGet(first.ToString(), out PlayerBag oldBag) && oldBag.Carried.Count == 1, Is.True, "premise: the old key's bag is a different bag");
            yield return PlayModeHarness.WaitUntil(() => Shows(ownA, 105), Timeout, "A's view stayed on the old key's bag after the roster key changed");
            haul.Identity = null;
        }

        // ---- lost satchel and boots (plan 3 task 9) ----

        // What a failed dive leaves: the unbanked haul waits in a satchel at the depth where `who` fell.
        private static void LoseSatchel(ChestRig r, ulong who, int floor, params int[] items)
        {
            foreach (int item in items) Assert.That(r.Haul.ServerAddItem(who, item), Is.True);
            r.Haul.ServerBagOf(who).Fail(floor);
            Assert.That(r.Haul.ServerBagOf(who).Satchel.Waiting, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Satchel_PlacedOnItsDepth_OnlyOwnerCanPickUp()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            LoseSatchel(r, r.A, ChestFloor, 101, 104);
            yield return PlayModeHarness.WaitUntil(() => r.PeerA.SatchelShownForLocal, Timeout, "A's client never drew its satchel on its depth");
            Assert.That(r.PeerA.TrySatchelPosition(out Vector3 at), Is.True);
            yield return Settle(0.5f);
            Assert.That(r.PeerB.SatchelShownForLocal || r.Contents.SatchelShownForLocal, Is.False, "nobody else draws it");

            // B and the host's player stand on it: they have no satchel here, so nothing happens. A is far away.
            PlayerBag bagA = r.Haul.ServerBagOf(r.A);
            r.Players[r.A] = at + new Vector3(30f, 0f, 0f); r.Players[r.B] = at; r.Players[r.Host] = at;
            Assert.That(r.Contents.ServerTryPickUpSatchel(r.B), Is.False, "a stranger cannot take A's satchel");
            Assert.That(r.Contents.ServerTryPickUpSatchel(r.Host), Is.False);
            Assert.That(r.Contents.ServerTryPickUpSatchel(r.A), Is.False, "A is out of range");
            yield return Settle(0.5f);   // the host's tick runs with B and the host on the spot
            Assert.That(bagA.Satchel.Waiting, Is.Not.Null, "still lying there");
            Assert.That(bagA.Satchel.Carried, Is.Null);

            // A walks onto it.
            int carriedBefore = bagA.Carried.Count;
            var picked = new List<ulong>(); r.Contents.SatchelPickedUp += who => picked.Add(who);
            r.Players[r.A] = at;
            yield return PlayModeHarness.WaitUntil(() => bagA.Satchel.Carried != null, Timeout, "A walking onto its satchel never picked it up");
            Assert.That(bagA.Satchel.Waiting, Is.Null);
            Assert.That(bagA.Satchel.Carried.Items, Is.EqualTo(new[] { 101, 104 }));
            Assert.That(bagA.Carried.Count, Is.EqualTo(carriedBefore), "the satchel is carried as one item (a slot), not unpacked");
            Assert.That(picked, Is.EqualTo(new[] { r.A }));
            yield return PlayModeHarness.WaitUntil(() => !r.PeerA.SatchelShownForLocal, Timeout, "the prop stayed after the pick-up");
        }

        [UnityTest]
        public IEnumerator Satchel_HiddenFromOtherClients()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            NetworkManager nmA = clients[0].NetworkManager, nmB = clients[1].NetworkManager;
            LoseSatchel(r, r.A, ChestFloor, 105);
            yield return PlayModeHarness.WaitUntil(() => { PlayerBagSync v = BagSyncOwnedBy(nmA, r.A); return v != null && v.View.SatchelWaiting && v.View.SatchelFloor == ChestFloor; }, Timeout, "A's own view never showed the waiting satchel");
            yield return Settle(0.5f);
            Assert.That(BagSyncOwnedBy(nmB, r.A).View.SatchelWaiting, Is.False, "B's copy of A's view is the default one: B is never told A has a satchel (the owner-only read)");
            Assert.That(BagSyncOwnedBy(r.HostNm, r.A).View.SatchelWaiting, Is.True, "the host sees every bag");
            Assert.That(r.PeerB.SatchelShownForLocal, Is.False);
            Assert.That(r.PeerA.SatchelShownForLocal, Is.True);
            Assert.That(r.PeerA.TrySatchelPosition(out Vector3 at), Is.True);
            r.Players[r.B] = at; r.Players[r.A] = at + new Vector3(30f, 0f, 0f); r.Players[r.Host] = at + new Vector3(30f, 0f, 0f);
            Assert.That(r.Contents.ServerTryPickUpSatchel(r.B), Is.False, "the host refuses B's pick-up of A's satchel");
            Assert.That(r.Haul.ServerPickUpSatchel(r.B), Is.False, "and the haul sync has no satchel of B's to pick up");
            Assert.That(r.Haul.ServerBagOf(r.A).Satchel.Waiting, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Satchel_NotOnOtherDepths()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            const int otherDepth = 4;
            LoseSatchel(r, r.A, otherDepth, 101);
            NetworkManager nmA = clients[0].NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => { PlayerBagSync v = BagSyncOwnedBy(nmA, r.A); return v != null && v.View.SatchelWaiting && v.View.SatchelFloor == otherDepth; }, Timeout, "A's view never showed the satchel waiting on floor " + otherDepth);
            yield return Settle(0.5f);
            Assert.That(r.PeerA.SatchelShownForLocal, Is.False, "it waits on floor " + otherDepth + ", the party is on floor " + ChestFloor);
            // Standing on the cell where it would lie on THIS floor changes nothing either.
            CellPos cell = CellarPieceSpots.SatchelSpot(r.Layout, r.Seed);
            r.Players[r.A] = r.Floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(cell.X, cell.Y));
            Assert.That(r.Contents.ServerTryPickUpSatchel(r.A), Is.False);
            yield return Settle(0.5f);
            Assert.That(r.Haul.ServerBagOf(r.A).Satchel.Waiting, Is.Not.Null, "not picked up on the wrong depth");
            Assert.That(r.Haul.ServerBagOf(r.A).Satchel.Carried, Is.Null);
        }

        [UnityTest]
        public IEnumerator Satchel_LateJoiner_NoToast_StateCorrect()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            LoseSatchel(r, r.A, ChestFloor, 101);
            yield return PlayModeHarness.WaitUntil(() => r.PeerA.SatchelShownForLocal, Timeout, "A's client never drew its satchel");

            // C joins late with a satchel of its own waiting on this depth (its bag is filled before its hero spawns, like a rejoin).
            yield return PlayModeHarness.StartClients(r.App, 1, false, clients, 30);
            PlayModeHarness.TestClient c = clients[2];
            NetworkManager nmC = c.NetworkManager;
            ulong cc = nmC.LocalClientId;
            LoseSatchel(r, cc, ChestFloor, 102);
            yield return SpawnHero(r.App, c, new Vector3(8f, 0f, 0f));
            ulong contentsId = r.Contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => nmC.SpawnManager.SpawnedObjects.ContainsKey(contentsId), Timeout, "C never received the contents object");
            CellarContentsSync peerC = ContentsOf(nmC, contentsId);
            CellarFloorSync own = null;
            float until = Time.realtimeSinceStartup + Timeout;
            while (own == null && Time.realtimeSinceStartup < until)
            {
                foreach (CellarFloorSync f in Object.FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (f.NetworkManager == nmC) own = f;
                if (own == null) { PlayModeHarness.Pace(); yield return null; }
            }
            Assert.That(own, Is.Not.Null, "C has its own floor sync");
            peerC.FloorSync = own;
            yield return PlayModeHarness.WaitUntil(() => peerC.SatchelShownForLocal, Timeout * 2, "the late joiner never saw its own satchel on this depth");
            yield return Settle(0.5f);
            Assert.That(peerC.NoticeCount, Is.EqualTo(0), "a late join replays no toast");
            Assert.That(r.PeerA.NoticeCount + r.PeerB.NoticeCount, Is.EqualTo(0));
            Assert.That(r.PeerA.SatchelShownForLocal, Is.True, "A's own prop is unchanged by the join");
            Assert.That(r.PeerB.SatchelShownForLocal, Is.False);
            Assert.That(peerC.TrySatchelPosition(out Vector3 cAt) && r.PeerA.TrySatchelPosition(out Vector3 aAt) && (cAt - aAt).sqrMagnitude < 0.01f, Is.True, "the same spot for everyone's own satchel");
        }

        [UnityTest]
        public IEnumerator Boots_OwnerHeroFollowsItsBag_NoOtherHeroDoes()
        {
            var clients = new List<PlayModeHarness.TestClient>();
            ChestRig r = null; yield return StartChestRig(clients, x => r = x);
            NetworkManager nmA = clients[0].NetworkManager, nmB = clients[1].NetworkManager;
            HeroLocomotion heroA = nmA.LocalClient.PlayerObject.GetComponent<HeroLocomotion>(), heroB = nmB.LocalClient.PlayerObject.GetComponent<HeroLocomotion>();
            HeroLocomotion heroHost = r.HostNm.LocalClient.PlayerObject.GetComponent<HeroLocomotion>();
            Assert.That(heroA, Is.Not.Null);
            Assert.That(heroA.BootsOn || heroB.BootsOn || heroHost.BootsOn, Is.False, "premise: no boots yet");

            Assert.That(r.Haul.ServerAddItem(r.A, CellarLoot.BootsId), Is.True);
            Assert.That(r.Haul.ServerAddItem(r.A, CellarLoot.BootsId), Is.False, "one pair per player");
            yield return PlayModeHarness.WaitUntil(() => heroA.BootsOn, Timeout, "A's own hero never put the boots on");
            yield return Settle(0.5f);
            Assert.That(heroB.BootsOn || heroHost.BootsOn, Is.False, "nobody else's hero is faster");
            Assert.That(MovementMath.TargetSpeed(1f, true, heroA.BootsOn), Is.EqualTo(MovementMath.RunSpeed * 1.05f).Within(1e-4f));

            Assert.That(r.Haul.ServerFailDive(), Is.True, "a failed dive keeps what the boots were banked as: here they were only carried, so they wait in the satchel");
            yield return PlayModeHarness.WaitUntil(() => !heroA.BootsOn, Timeout, "the boots stayed on after they went into the satchel");
        }
    }
}
