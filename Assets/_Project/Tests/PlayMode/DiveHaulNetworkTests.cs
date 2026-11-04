// Authority: test-only. M5 banking: the host owns the shared haul (DiveHaulSync); every peer reads the same summary.
// Host + two clients in one process (scene management off; the sync object is a dynamically spawned network prefab from the owned list).
// Catches: a client mutating the haul, a bank request from a client not reaching the host or not reaching the other peers, banking on a floor with no
// bank point, a failed dive not returning pieces on every peer, a late joiner missing the current summary.
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

        [UnityTest]
        public IEnumerator Stir_HostOwnsIt_ClientsSeeValueLatchesAndCountdown_BankStopsIt_ZeroFailsTheDive()
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

            StirTuning tuning = StirTuning.Default; tuning.DeepClosesSeconds = 5f; tuning.PerFloorCleared = 30f;   // short countdown; pieces no longer feed the stir (plan 2), so the floor term is raised
            Assert.That(host.ServerBegin(new[] { 6, 6, 6 }, tuning), Is.True);
            Assert.That(peerA.ServerBegin(new[] { 6, 6, 6 }, tuning), Is.False, "a client cannot restart the dive");
            Assert.That(host.ServerSetFloor(1), Is.True);

            // Pieces are safe on pickup and never move the stir.
            Assert.That(host.ServerAddPiece(DiveHaul.PieceId(1, 0)), Is.True); Assert.That(host.ServerAddPiece(DiveHaul.PieceId(1, 1)), Is.True);
            yield return WaitSummary(peerA, s => s.FoundPieces == 2 && s.BankedPieces == 2 && s.StirValue == 0 && !s.DeepClosesLatched, "client A never saw two safe pieces with the stir still at 0");

            // Three players present: factor 1.5. One floor cleared = 30 x 1.5 = 45: past 40 only.
            Assert.That(host.ServerMarkCleared(1), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 45 && s.ExtraEnemiesPerRoom == 1 && !s.HunterLatched && !s.DeepClosesLatched, "client A never saw stir 45 with the extra-enemy latch");
            yield return WaitSummary(peerB, s => s.StirValue == 45 && s.ExtraEnemiesPerRoom == 1, "client B never saw stir 45");

            // Two floors cleared: 60 x 1.5 = 90, hunter latched.
            Assert.That(host.ServerMarkCleared(2), Is.True);
            yield return WaitSummary(peerA, s => s.StirValue == 90 && s.HunterLatched && !s.DeepClosesLatched, "client A never saw the hunter latch");

            // Past 100: the deep closes, clients see the seconds counting down.
            Assert.That(host.ServerMarkCleared(3), Is.True);   // 90 x 1.5 = 135 -> 100
            yield return WaitSummary(peerA, s => s.StirValue == 100 && s.DeepClosesLatched && s.DeepSecondsLeft > 0, "client A never saw the deep close");
            yield return WaitSummary(peerB, s => s.DeepClosesLatched && s.DeepSecondsLeft > 0 && s.DeepSecondsLeft <= 5, "client B never saw the countdown");

            // A bank at the checkpoint stops it; latches stay.
            Assert.That(host.ServerSetFloor(2), Is.True);
            Assert.That(host.ServerBank(), Is.True);
            yield return WaitSummary(peerA, s => s.DeepSecondsLeft == 0 && s.DeepClosesLatched && s.HunterLatched, "client A never saw the countdown stop after the bank");
            yield return new WaitForSecondsRealtime(6f);   // longer than the countdown: the dive must survive
            Assert.That(host.Current.CurrentFloor, Is.EqualTo(2), "a banked countdown must not fail the dive");
            Assert.That(host.Current.BankedPieces, Is.EqualTo(2));

            // A fresh dive (new state, stir re-armed): carry two pieces, push past 100 again and let it run out. The host fails the dive on every peer; the pieces stay safe.
            Assert.That(host.ServerBegin(new[] { 6, 6, 6 }, tuning), Is.True);
            Assert.That(host.ServerSetFloor(1), Is.True);
            Assert.That(host.ServerAddPiece(DiveHaul.PieceId(2, 0)), Is.True); Assert.That(host.ServerAddPiece(DiveHaul.PieceId(3, 0)), Is.True);
            for (int f = 1; f <= 3; f++) Assert.That(host.ServerMarkCleared(f), Is.True);
            yield return WaitSummary(peerA, s => s.DeepSecondsLeft > 0, "client A never saw the second countdown");
            yield return WaitSummary(peerA, s => s.CurrentFloor == 0 && s.StirValue == 0 && s.DeepSecondsLeft == 0 && !s.HunterLatched && !s.DeepClosesLatched && s.ExtraEnemiesPerRoom == 0, "client A never saw the dive fail at 0");
            yield return WaitSummary(peerB, s => s.CurrentFloor == 0 && s.BankedPieces == 2 && s.LeftFloor3 == 5 && s.LeftFloor2 == 5, "client B never saw the pieces stay safe");
            Assert.That(peerA.Current, Is.EqualTo(host.Current));
            Assert.That(peerB.Current, Is.EqualTo(host.Current));
        }

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

            // Fresh dive: 2 pieces on each of floors 1-3.
            yield return WaitSummary(peerA, s => s.LeftFloor1 == 2 && s.LeftFloor2 == 2 && s.LeftFloor3 == 2, "client A never saw the starting pieces-left");

            // A client cannot mutate the haul.
            Assert.That(peerA.ServerAddPiece(100), Is.False);
            Assert.That(peerA.ServerAddItem(a.NetworkManager.LocalClientId, 101), Is.False);
            Assert.That(peerA.ServerAddGold(a.NetworkManager.LocalClientId, 3), Is.False);
            Assert.That(peerA.ServerBank(), Is.False);
            Assert.That(peerA.ServerFailDive(), Is.False);
            Assert.That(peerA.ServerSetFloor(2), Is.False);
            Assert.That(peerA.ServerMarkCleared(1), Is.False);
            Assert.That(peerA.ServerBegin(new[] { 9, 9, 9 }), Is.False);
            Assert.That(peerA.ServerBankInTown(), Is.False);

            // Host collects on floor 1 and 2, then stands on floor 3 (no bank there).
            Assert.That(host.ServerSetFloor(1), Is.True);
            Assert.That(host.ServerAddPiece(100), Is.True);
            Assert.That(host.ServerAddPiece(100), Is.False, "the same piece twice");
            ulong hostId = hostNm.LocalClientId;
            Assert.That(host.ServerAddItem(hostId, 105), Is.True);
            Assert.That(host.ServerMarkCleared(1), Is.True);
            Assert.That(host.ServerSetFloor(3), Is.True);
            Assert.That(host.ServerAddPiece(200), Is.True);   // piece ids are floor*100+n; 200 is a floor-2 piece found while passing
            yield return WaitSummary(peerA, s => s.FoundPieces == 2 && s.BankedPieces == 2 && s.LeftFloor1 == 1 && s.LeftFloor2 == 1 && s.ClearedMask == 1 && s.CurrentFloor == 3, "client A never saw the found pieces");

            // A bank request on floor 3 (no bank point) changes nothing.
            peerA.RequestBank();
            float until = Time.realtimeSinceStartup + 10f;
            while (host.BankRequestsHandled < 1 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(host.BankRequestsHandled, Is.EqualTo(1), "the floor-3 bank request never reached the host");
            Assert.That(host.HostState.CanBankHere, Is.False, "no bank point on floor 3");
            Assert.That(host.Current.BankedPieces, Is.EqualTo(2), "pieces are safe on pickup, bank or no bank");
            Assert.That(host.ServerBagOf(hostId).Carried, Is.EqualTo(new[] { 105 }), "the host's bag still carries its item");

            // On floor 2 (checkpoint) the client's request banks everything, and every peer sees it.
            Assert.That(host.ServerSetFloor(2), Is.True);
            peerA.RequestBank();
            yield return WaitSummary(host, s => s.CheckpointReached, "the host never banked on the client's request");
            yield return WaitSummary(peerA, s => s.BankedPieces == 2 && s.CheckpointReached && s.BankSpent, "client A never saw the banked summary");
            Assert.That(host.ServerBagOf(hostId).BankedThisDive.ContainsKey(105), Is.True, "the bank push-on made the bag item safe (free slot auto-fill)");

            // Late joiner gets the current summary with the spawn.
            PlayModeHarness.TestClient b = null;
            yield return Join(app, 9, c => b = c);
            yield return PlayModeHarness.WaitUntil(() => b.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client B never received the haul object");
            DiveHaulSync peerB = On(b.NetworkManager, id);
            yield return WaitSummary(peerB, s => s.BankedPieces == 2 && s.CheckpointReached && s.ClearedMask == 1 && s.LeftFloor1 == 1 && s.LeftFloor2 == 1 && s.CurrentFloor == 2, "late client B never saw the current summary");
            // The player factor changed when B joined, so the host republishes the stir value; B converges on it.
            yield return PlayModeHarness.WaitUntil(() => peerB.Current.Equals(host.Current), Timeout, "late joiner never matched the host exactly");

            // Failed dive: every collected piece stays collected (safe on pickup) on every peer; checkpoint and cleared floors stay; a carried bag item waits in the satchel.
            Assert.That(host.ServerAddPiece(300), Is.True);
            Assert.That(host.ServerAddPiece(101), Is.True);
            Assert.That(host.ServerAddItem(hostId, 106), Is.True);
            yield return WaitSummary(peerB, s => s.FoundPieces == 4 && s.BankedPieces == 4 && s.LeftFloor3 == 1 && s.LeftFloor1 == 0, "client B never saw the new pieces");
            Assert.That(host.ServerFailDive(), Is.True);
            yield return WaitSummary(peerA, s => s.BankedPieces == 4 && s.LeftFloor3 == 1 && s.LeftFloor1 == 0 && s.CurrentFloor == 0, "client A never saw the failed dive keep the pieces");
            Assert.That(host.ServerBagOf(hostId).Satchel.Waiting, Is.Not.Null, "the carried bag item waits in a satchel");
            Assert.That(host.ServerBagOf(hostId).Satchel.Waiting.Floor, Is.EqualTo(2), "at the floor the party fell on");
            Assert.That(host.ServerBagOf(hostId).Satchel.Waiting.Items, Is.EqualTo(new[] { 106 }));
            yield return WaitSummary(peerB, s => s.LeftFloor3 == 1 && s.LeftFloor1 == 0 && s.CheckpointReached && s.ClearedMask == 1, "client B never saw the failed dive");
            Assert.That(peerA.Current, Is.EqualTo(host.Current));
            Assert.That(peerB.Current, Is.EqualTo(host.Current));
        }
    }
}
