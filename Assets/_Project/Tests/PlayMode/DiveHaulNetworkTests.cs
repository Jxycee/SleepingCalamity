// Authority: test-only. The shared dive session (plan 3 task 7): the host owns the DiveSession (DiveHaulSync); every peer reads the same summary.
// Host + two clients in one process (scene management off; the sync object is a dynamically spawned network prefab from the owned list).
// Catches: a client mutating the session, a bank request from a client not reaching the host or not reaching the other peers, banking off the bank floor, a failed dive not keeping the
// pieces and leaving the satchel, a late joiner missing the current summary, the stir not following the clears.
// NOT covered by a one-process test: the player-position check against a real built bank cell (these clients have no player objects; the range rule
// itself is asserted through DiveHaulSync.InRange), real relay latency.
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
    public class DiveHaulNetworkTests
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
                    if (entry.Prefab != null && entry.Prefab.GetComponent<DiveHaulSync>() != null) return entry.Prefab;
            Assert.Fail("DiveHaulSync is not in the network prefab list (run the project builder)");
            return null;
        }

        private static DiveHaulSync On(NetworkManager nm, ulong id) => nm.SpawnManager.SpawnedObjects[id].GetComponent<DiveHaulSync>();

        private static IEnumerator WaitSummary(DiveHaulSync s, System.Func<DiveHaulSync.Summary, bool> ok, string what) =>
            PlayModeHarness.WaitUntil(() => s != null && ok(s.Current), Timeout, what);

        [Test]
        public void BankRange_IsFlatDistanceWithinRadius()
        {
            Assert.That(DiveHaulSync.InRange(new Vector3(1, 50, 1), new Vector3(0, 0, 0)), Is.True, "height is ignored");
            Assert.That(DiveHaulSync.InRange(new Vector3(DiveHaulSync.BankRadius + 0.5f, 0, 0), Vector3.zero), Is.False);
        }

        // Catches: the stir value or a latch computed on one peer and not another, a clear that does not feed the stir, the first-half latches (extra enemies at 40, the hunter at 70) at the wrong
        // value, the player factor not following who is connected. NOT covered (plan 3 task 10): the collapse countdown and its fail (DiveStir needs the path time and has a 60 s minimum), valuables.
        [UnityTest]
        public IEnumerator Stir_HostOwnsIt_ClientsSeeValueAndLatches_FloorsFeedItPerHalf()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;

            PlayModeHarness.TestClient a = null, b = null;
            yield return Join(app, 8, c => a = c);
            yield return Join(app, 9, c => b = c);

            NetworkObject spawned = hostNm.SpawnManager.InstantiateAndSpawn(SyncPrefab(hostNm).GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveHaulSync host = spawned.GetComponent<DiveHaulSync>();
            ulong id = spawned.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => a.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id) && b.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "clients never received the haul object");
            DiveHaulSync peerA = On(a.NetworkManager, id), peerB = On(b.NetworkManager, id);

            Assert.That(host.ServerBegin(), Is.True);
            Assert.That(peerA.ServerBegin(), Is.False, "a client cannot restart the dive");
            Assert.That(host.ServerStart(1, false), Is.True);

            // Pieces are safe on pickup and never move the stir.
            Assert.That(host.ServerAddPiece(100), Is.True); Assert.That(host.ServerAddPiece(101), Is.True);
            yield return WaitSummary(peerA, s => s.FoundPieces == 2 && s.StirValue == 0 && !s.DeepClosesLatched, "client A never saw two safe pieces with the stir still at 0");

            // Three players present: factor sqrt(3) = 1.73. One floor cleared = 10 x 1.73 = 17.3; two = 34.6.
            Assert.That(host.ServerMarkCleared(1), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 17 && s.ExtraEnemiesPerRoom == 0 && !s.HunterLatched, "client A never saw stir 17");
            Assert.That(host.ServerMarkCleared(1), Is.False, "a floor clears once");
            Assert.That(host.ServerEnterFloor(2), Is.True);
            Assert.That(host.ServerMarkCleared(2), Is.True);
            yield return WaitSummary(peerB, s => s.StirValue == 35 && s.ExtraEnemiesPerRoom == 0, "client B never saw stir 35");

            // Three floors: 52, past the extra-enemy mark only.
            Assert.That(host.ServerEnterFloor(3), Is.True);
            Assert.That(host.ServerMarkCleared(3), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 52 && s.ExtraEnemiesPerRoom == 1 && !s.HunterLatched, "client A never saw the extra-enemy latch");

            // Four floors: 69, just short of the hunter; five: 87 with the hunter latched. A cleared mask of five floors rides along.
            Assert.That(host.ServerEnterFloor(4), Is.True);
            Assert.That(host.ServerMarkCleared(4), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 69 && !s.HunterLatched, "client A never saw stir 69");
            host.Session.Restore(new DiveSession.SnapshotData(DivePhase.Floor, 5, host.Session.Snapshot().ClearedMask, false, false, -1, host.Session.Stir.Capture(), host.Session.Momentum.Capture(), host.Session.Retry.Capture()));   // the bank floor: the guardian is the stand-in for the stair here
            Assert.That(host.ServerMarkCleared(5), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 87 && s.HunterLatched && s.ClearedMask == 0b11111 && s.CurrentFloor == 5, "client A never saw the hunter latch");
            Assert.That(peerA.Current, Is.EqualTo(host.Current));
            Assert.That(peerB.Current, Is.EqualTo(host.Current));

            // A fresh dive (new session over the same pieces): the stir starts again from 0, the found pieces stay found.
            Assert.That(host.ServerBegin(), Is.True);
            Assert.That(host.ServerStart(1, false), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 0 && !s.HunterLatched && s.ExtraEnemiesPerRoom == 0 && s.ClearedMask == 0 && s.FoundPieces == 2, "client A never saw the fresh dive start clean");
        }

        // Catches: a client mutating the session, a bank request from a client not reaching the host or not reaching the other peers, a bank press off the bank floor, a failed dive not leaving the
        // satchel and the pieces on every peer, a late joiner missing the current summary, a push-on press that confirms alone. NOT covered: the bank screen (IMGUI) and a real built bank cell.
        [UnityTest]
        public IEnumerator Haul_HostOwnsIt_ClientsBankByRpc_AllPeersAgree_LateJoinerSyncs()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;

            PlayModeHarness.TestClient a = null;
            yield return Join(app, 8, c => a = c);

            NetworkObject spawned = hostNm.SpawnManager.InstantiateAndSpawn(SyncPrefab(hostNm).GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveHaulSync host = spawned.GetComponent<DiveHaulSync>();
            ulong id = spawned.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => a.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client A never received the haul object");
            DiveHaulSync peerA = On(a.NetworkManager, id);

            // At home: nothing found, nothing left to find, no phase.
            yield return WaitSummary(peerA, s => s.FoundPieces == 0 && s.PiecesLeft == 0 && s.PeopleLeft == 0 && s.DivePhase == DivePhase.Town && s.CurrentFloor == 0, "client A never saw the home summary");

            // A client cannot mutate the session.
            Assert.That(peerA.ServerAddPiece(100), Is.False);
            Assert.That(peerA.ServerAddItem(a.NetworkManager.LocalClientId, 101), Is.False);
            Assert.That(peerA.ServerAddGold(a.NetworkManager.LocalClientId, 3), Is.False);
            Assert.That(peerA.ServerEnterBankRoom(), Is.False);
            Assert.That(peerA.ServerChoosePushOn(a.NetworkManager.LocalClientId), Is.False);
            Assert.That(peerA.ServerChooseGoHome(a.NetworkManager.LocalClientId), Is.False);
            Assert.That(peerA.ServerResizeVisits(), Is.False);
            Assert.That(peerA.ServerFailDive(), Is.False);
            Assert.That(peerA.ServerStart(1, false), Is.False);
            Assert.That(peerA.ServerEnterFloor(2), Is.False);
            Assert.That(peerA.ServerMarkCleared(1), Is.False);
            Assert.That(peerA.ServerGoHome(), Is.False);
            Assert.That(peerA.ServerFinish(), Is.False);
            Assert.That(peerA.ServerBegin(), Is.False);

            // Host: the checkpoint rule decides the start; floor 1 always starts.
            Assert.That(host.ServerStart(3, false), Is.False, "no checkpoint: floor 3 is refused");
            Assert.That(host.ServerStart(1, false), Is.True);
            Assert.That(host.ServerStart(1, false), Is.False, "a dive that runs cannot start again");
            Assert.That(host.ServerAddPiece(100), Is.True);
            Assert.That(host.ServerAddPiece(100), Is.False, "the same piece twice");
            ulong hostId = hostNm.LocalClientId;
            Assert.That(host.ServerAddItem(hostId, 105), Is.True);
            Assert.That(host.ServerMarkCleared(1), Is.True);
            Assert.That(host.ServerEnterFloor(3), Is.False, "the stair only leads to the next floor");
            Assert.That(host.ServerEnterFloor(2), Is.True);
            yield return WaitSummary(peerA, s => s.FoundPieces == 1 && s.ClearedMask == 1 && s.CurrentFloor == 2 && s.DivePhase == DivePhase.Floor, "client A never saw the found piece and floor 2");

            // A bank request off the bank floor changes nothing.
            peerA.RequestBank();
            float until = Time.realtimeSinceStartup + 10f;
            while (host.BankRequestsHandled < 1 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(host.BankRequestsHandled, Is.EqualTo(1), "the floor-2 bank request never reached the host");
            Assert.That(host.Current.DivePhase, Is.EqualTo(DivePhase.Floor), "no bank on floor 2");
            Assert.That(host.ServerBagOf(hostId).Carried, Is.EqualTo(new[] { 105 }), "the host's bag still carries its item");

            // On the bank floor (jumped to by the test hook) the cleared guardian lets a client's request open the bank room, then push on; every peer sees both.
            Assert.That(host.ServerSetFloor(5), Is.True);
            peerA.RequestBank();
            float until2 = Time.realtimeSinceStartup + 3f;
            while (host.BankRequestsHandled < 2 && Time.realtimeSinceStartup < until2) yield return null;
            Assert.That(host.Current.DivePhase, Is.EqualTo(DivePhase.Floor), "the guardian is not down: the bank room stays shut");
            Assert.That(host.ServerMarkCleared(5), Is.True);
            peerA.RequestBank();
            yield return WaitSummary(peerA, s => s.DivePhase == DivePhase.BankRoom && s.CurrentFloor == 5, "client A never saw the bank room open");
            Assert.That(host.ServerBagOf(hostId).BankOpen, Is.True, "the host's bag has a bank visit");
            ulong clientA = a.NetworkManager.LocalClientId;
            int slotA = host.PlayerSlotOf(clientA);
            peerA.RequestBank();   // inside the room the bank press is a push-on press: one of two players opens the confirm window
            yield return WaitSummary(peerA, s => s.ChoiceKind == BankChoiceKind.PushOn && s.ChoiceMask == (1 << slotA) && s.ChoiceSecondsLeft > 0, "client A never saw its push-on press open the window");
            Assert.That(host.Current.BankChosen, Is.False, "one press of two players is not the party's choice");
            Assert.That(host.ServerChoosePushOn(hostId), Is.True, "the second player's press confirms");
            yield return WaitSummary(peerA, s => s.BankChosen && s.DivePhase == DivePhase.Floor && s.ChoiceKind == BankChoiceKind.None, "client A never saw the push-on");
            Assert.That(host.ServerBagOf(hostId).BankOpen, Is.False, "the push-on closed the visit");

            // Late joiner gets the current summary with the spawn.
            PlayModeHarness.TestClient b = null;
            yield return Join(app, 9, c => b = c);
            yield return PlayModeHarness.WaitUntil(() => b.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client B never received the haul object");
            DiveHaulSync peerB = On(b.NetworkManager, id);
            yield return WaitSummary(peerB, s => s.FoundPieces == 1 && s.BankChosen && s.CurrentFloor == 5 && s.DivePhase == DivePhase.Floor, "late client B never saw the current summary");
            // The player factor changed when B joined, so the host republishes the stir value; B converges on it.
            yield return PlayModeHarness.WaitUntil(() => peerB.Current.Equals(host.Current), Timeout, "late joiner never matched the host exactly");

            // Failed dive: the found piece stays found on every peer; a carried bag item waits in a satchel at the floor the party fell on.
            Assert.That(host.ServerAddPiece(101), Is.True);
            Assert.That(host.ServerAddItem(hostId, 106), Is.True);
            yield return WaitSummary(peerB, s => s.FoundPieces == 2, "client B never saw the new piece");
            Assert.That(host.ServerFailDive(), Is.True);
            yield return WaitSummary(peerA, s => s.FoundPieces == 2 && s.CurrentFloor == 0 && s.DivePhase == DivePhase.Ended, "client A never saw the failed dive keep the pieces");
            Assert.That(host.ServerBagOf(hostId).Satchel.Waiting, Is.Not.Null, "the carried bag item waits in a satchel");
            Assert.That(host.ServerBagOf(hostId).Satchel.Waiting.Floor, Is.EqualTo(5), "at the floor the party fell on");
            Assert.That(host.ServerBagOf(hostId).Satchel.Waiting.Items, Does.Contain(106));
            yield return WaitSummary(peerB, s => s.FoundPieces == 2 && s.DivePhase == DivePhase.Ended, "client B never saw the failed dive");
            Assert.That(peerA.Current, Is.EqualTo(host.Current));
            Assert.That(peerB.Current, Is.EqualTo(host.Current));
        }

        // ================================================================================================================================
        // Plan 3 task 8: the bank room's rules on the host (haul sync alone, real clients joining and leaving).
        // Catches: capacity that does not follow who is present (a joiner without a visit, a leaver's picks banked behind their back, excess picks not dropped from the end), the extra-slot owner repeating its
        // previous owner or PreviousExtraOwner being overwritten by a re-pick, a push-on window that never times out, a late joiner not seeing the window and the phase with the spawn.
        // NOT covered: the downed-hero quorum (PerPlayerLootNetworkTests, which has heroes), the director's save and resume (DiveSpineNetworkTests), the bank screen, a real built bank cell.
        // ================================================================================================================================

        private IEnumerator StartRoom(int clientCount, System.Action<AppBootstrap, DiveHaulSync, PlayModeHarness.TestClient[]> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            var joined = new PlayModeHarness.TestClient[clientCount];
            for (int i = 0; i < clientCount; i++) { int at = i; yield return Join(app, 8 + i, c => joined[at] = c); }
            NetworkObject spawned = hostNm.SpawnManager.InstantiateAndSpawn(SyncPrefab(hostNm).GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveHaulSync host = spawned.GetComponent<DiveHaulSync>();
            Assert.That(host.ServerBegin(), Is.True);
            done(app, host, joined);
        }

        private IEnumerator Leave(AppBootstrap app, PlayModeHarness.TestClient client)
        {
            ulong id = client.NetworkManager.LocalClientId;
            yield return PlayModeHarness.Await(client.Session.LeaveAsync(), Timeout);
            yield return PlayModeHarness.WaitUntil(() => !app.NetworkManager.ConnectedClients.ContainsKey(id), Timeout, "the host still lists the dropped client");
        }

        [UnityTest]
        public IEnumerator BankRoom_PartyChanges_CapacityFollowsPresence_PicksDropFromTheEnd_RejoinerGetsAVisit()
        {
            AppBootstrap app = null; DiveHaulSync host = null; PlayModeHarness.TestClient[] cl = null;
            yield return StartRoom(1, (x, h, c) => { app = x; host = h; cl = c; });
            ulong hostId = app.NetworkManager.LocalClientId, idA = cl[0].NetworkManager.LocalClientId;
            foreach (int item in new[] { 101, 102, 103, 104 }) Assert.That(host.ServerAddItem(hostId, item), Is.True);
            Assert.That(host.BankRoomNow(), Is.True);
            Assert.That(host.ServerBagOf(hostId).SlotCapacity, Is.EqualTo(2), "two players: two slots each");
            Assert.That(host.ServerPick(hostId, 0) && host.ServerPick(hostId, 1), Is.True);

            // C joins: three players, {2,1,1}; the host resizes by itself; picks beyond the new capacity drop from the end
            PlayModeHarness.TestClient c = null;
            yield return Join(app, 9, x => c = x);
            ulong idC = c.NetworkManager.LocalClientId;
            Assert.That(host.ServerAddItem(idC, 106), Is.True);
            yield return PlayModeHarness.WaitUntil(() => host.ServerBagOf(idC).BankOpen, Timeout, "the room never gave the joiner a visit");
            int total = 0;
            foreach (ulong id in new[] { hostId, idA, idC }) total += host.ServerBagOf(id).SlotCapacity;
            Assert.That(total, Is.EqualTo(BankSlots.TotalSlots(3)), "three players: the same four slots, shared out as {2,1,1}");
            int capHost = host.ServerBagOf(hostId).SlotCapacity;
            Assert.That(host.ServerBagOf(hostId).Picks.Count, Is.EqualTo(capHost), "the host's picks fit the new capacity");
            for (int i = 0; i < capHost; i++) Assert.That(host.ServerBagOf(hostId).Picks[i], Is.EqualTo(i), "and what was kept is the start of what was picked");
            Assert.That(host.ServerPick(idC, 0), Is.True);

            // C leaves: their visit is cancelled (the pick is not banked behind their back), the others' capacity grows back
            string keyC = c.Key.ToString();
            yield return Leave(app, c);
            yield return PlayModeHarness.WaitUntil(() => host.Bags.TryGet(keyC, out PlayerBag cb) && !cb.BankOpen && host.ServerBagOf(hostId).SlotCapacity == 2 && host.ServerBagOf(idA).SlotCapacity == 2, Timeout, "the room never shrank back to two players");
            host.Bags.TryGet(keyC, out PlayerBag leaver);
            Assert.That(leaver.Picks, Is.Empty, "the leaver's pick was dropped");
            Assert.That(leaver.Carried, Is.EqualTo(new[] { 106 }), "their item stays carried");
            Assert.That(leaver.BankedThisDive, Is.Empty);

            // C comes back (same player, a new connection): a visit again, the item still theirs
            PlayModeHarness.TestClient back = null;
            yield return Join(app, 9, x => back = x);
            ulong idBack = back.NetworkManager.LocalClientId;
            yield return PlayModeHarness.WaitUntil(() => host.ServerBagOf(idBack).BankOpen, Timeout, "the rejoiner never got a visit");
            Assert.That(host.ServerBagOf(idBack).Carried, Is.EqualTo(new[] { 106 }));
            Assert.That(host.ServerPick(idBack, 0), Is.True, "and can pick");
        }

        [UnityTest]
        public IEnumerator BankRoom_ExtraOwnerLeaves_RepicksButKeepsPreviousOwner()
        {
            AppBootstrap app = null; DiveHaulSync host = null; PlayModeHarness.TestClient[] cl = null;
            yield return StartRoom(2, (x, h, c) => { app = x; host = h; cl = c; });
            ulong hostId = app.NetworkManager.LocalClientId, idA = cl[0].NetworkManager.LocalClientId, idB = cl[1].NetworkManager.LocalClientId;
            int slotHost = host.PlayerSlotOf(hostId), slotA = host.PlayerSlotOf(idA), slotB = host.PlayerSlotOf(idB);
            // the previous visit's owner was the host; look for a dive seed whose extra slot goes to B (a client, who can leave)
            bool found = false;
            for (uint seed = 1; seed < 80 && !found; seed++)
            {
                host.DiveSeed = seed;
                host.PreviousExtraOwner = slotHost;
                Assert.That(host.BankRoomNow(), Is.True);
                found = host.Session.ExtraSlotOwner == slotB;
            }
            Assert.That(found, Is.True, "premise: some seed gives B the extra slot");
            Assert.That(host.PreviousExtraOwner, Is.EqualTo(slotB), "set once, when the owner was first picked this visit");

            string keyB = cl[1].Key.ToString();
            yield return Leave(app, cl[1]);
            yield return PlayModeHarness.WaitUntil(() => host.Bags.TryGet(keyB, out PlayerBag bb) && !bb.BankOpen && host.Session.ExtraSlotOwner == -1, Timeout, "the room never resized to two players");
            Assert.That(host.ServerBagOf(hostId).SlotCapacity, Is.EqualTo(2));
            Assert.That(host.ServerBagOf(idA).SlotCapacity, Is.EqualTo(2));
            Assert.That(host.PreviousExtraOwner, Is.EqualTo(slotB), "a resize does not overwrite it");

            // a new player takes the seat: three again, the extra slot is picked again (not the previous visit's owner, the host)
            PlayModeHarness.TestClient c = null;
            yield return Join(app, 10, x => c = x);
            ulong idC = c.NetworkManager.LocalClientId;
            yield return PlayModeHarness.WaitUntil(() => host.ServerBagOf(idC).BankOpen && host.Session.ExtraSlotOwner >= 0, Timeout, "the extra slot was never picked again");
            int slotC = host.PlayerSlotOf(idC);
            Assert.That(host.Session.ExtraSlotOwner == slotA || host.Session.ExtraSlotOwner == slotC, Is.True, "re-picked among the players present");
            Assert.That(host.Session.ExtraSlotOwner, Is.Not.EqualTo(slotHost), "still never the owner the visit started from");
            Assert.That(host.PreviousExtraOwner, Is.EqualTo(slotB), "and PreviousExtraOwner still names the first pick of this visit");
            int twos = 0; foreach (ulong id in new[] { hostId, idA, idC }) if (host.ServerBagOf(id).SlotCapacity == 2) twos++;
            Assert.That(twos, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BankRoom_ConfirmWindow_TimerConfirmsWhenNobodyElsePresses()
        {
            AppBootstrap app = null; DiveHaulSync host = null; PlayModeHarness.TestClient[] cl = null;
            yield return StartRoom(1, (x, h, c) => { app = x; host = h; cl = c; });
            ulong hostId = app.NetworkManager.LocalClientId;
            host.ChoiceWindowSeconds = 1.5f;
            Assert.That(host.ServerAddItem(hostId, 104), Is.True);
            Assert.That(host.BankRoomNow(), Is.True);
            Assert.That(host.ServerPick(hostId, 0), Is.True);
            Assert.That(host.ServerChoosePushOn(hostId), Is.True);
            Assert.That(host.Current.ChoiceKind, Is.EqualTo(BankChoiceKind.PushOn), "two players: one press opens the window");
            Assert.That(host.ServerChoosePushOn(hostId), Is.False, "the same player pressing again changes nothing");
            Assert.That(host.Session.BankChosenThisDive, Is.False);
            yield return PlayModeHarness.WaitUntil(() => host.Session.BankChosenThisDive, Timeout, "the timer never confirmed the push-on");
            Assert.That(host.ServerBagOf(hostId).BankedThisDive.ContainsKey(104), Is.True);
            Assert.That(host.Current.ChoiceKind, Is.EqualTo(BankChoiceKind.None));
        }

        [UnityTest]
        public IEnumerator BankRoom_LateJoiner_SeesWindowAndPhase_NoToast()
        {
            AppBootstrap app = null; DiveHaulSync host = null; PlayModeHarness.TestClient[] cl = null;
            yield return StartRoom(1, (x, h, c) => { app = x; host = h; cl = c; });
            host.ChoiceWindowSeconds = 120f;   // the window must still be open when the late client arrives
            Assert.That(host.BankRoomNow(), Is.True);
            ulong id = host.NetworkObjectId;
            DiveHaulSync peerA = On(cl[0].NetworkManager, id);
            yield return WaitSummary(peerA, s => s.DivePhase == DivePhase.BankRoom && s.CurrentFloor == 5, "client A never saw the bank room");
            int slotA = host.PlayerSlotOf(cl[0].NetworkManager.LocalClientId);
            peerA.RequestPushOn();
            yield return WaitSummary(host, s => s.ChoiceKind == BankChoiceKind.PushOn && s.ChoiceMask == (1 << slotA), "the host never saw A's push-on press");

            PlayModeHarness.TestClient b = null;
            yield return Join(app, 9, c => b = c);
            yield return PlayModeHarness.WaitUntil(() => b.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client B never received the haul object");
            DiveHaulSync peerB = On(b.NetworkManager, id);
            yield return WaitSummary(peerB, s => s.DivePhase == DivePhase.BankRoom && s.ChoiceKind == BankChoiceKind.PushOn && s.ChoiceMask == (1 << slotA) && s.ChoiceSecondsLeft > 100, "the late joiner never saw the open window and the phase");
            Assert.That(host.Current.BankChosen, Is.False, "joining confirmed nothing");
            Assert.That(host.ChoiceRequestsHandled, Is.EqualTo(1), "only A's one press ever reached the host: joining replays no request or toast (the sync has no one-shot to send)");
        }
    }
}
