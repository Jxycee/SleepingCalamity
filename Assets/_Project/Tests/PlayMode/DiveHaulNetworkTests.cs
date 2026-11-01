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
            Assert.That(peerA.ServerAddLoot(1, 3), Is.False);
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
            Assert.That(host.ServerAddLoot(5, 3), Is.True);
            Assert.That(host.ServerMarkCleared(1), Is.True);
            Assert.That(host.ServerSetFloor(3), Is.True);
            Assert.That(host.ServerAddPiece(200), Is.True);   // piece ids are floor*100+n; 200 is a floor-2 piece found while passing
            yield return WaitSummary(peerA, s => s.UnbankedPieces == 2 && s.UnbankedLoot == 3 && s.LeftFloor1 == 1 && s.LeftFloor2 == 1 && s.ClearedMask == 1 && s.CurrentFloor == 3, "client A never saw the unbanked haul");

            // A bank request on floor 3 (no bank point) changes nothing.
            peerA.RequestBank();
            float until = Time.realtimeSinceStartup + 10f;
            while (host.BankRequestsHandled < 1 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(host.BankRequestsHandled, Is.EqualTo(1), "the floor-3 bank request never reached the host");
            Assert.That(host.Current.UnbankedPieces, Is.EqualTo(2), "no bank on floor 3");
            Assert.That(host.Current.BankedPieces, Is.EqualTo(0));

            // On floor 2 (checkpoint) the client's request banks everything, and every peer sees it.
            Assert.That(host.ServerSetFloor(2), Is.True);
            peerA.RequestBank();
            yield return WaitSummary(host, s => s.BankedPieces == 2, "the host never banked on the client's request");
            yield return WaitSummary(peerA, s => s.BankedPieces == 2 && s.UnbankedPieces == 0 && s.UnbankedLoot == 0 && s.CheckpointReached, "client A never saw the banked summary");

            // Late joiner gets the current summary with the spawn.
            PlayModeHarness.TestClient b = null;
            yield return Join(app, 9, c => b = c);
            yield return PlayModeHarness.WaitUntil(() => b.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client B never received the haul object");
            DiveHaulSync peerB = On(b.NetworkManager, id);
            yield return WaitSummary(peerB, s => s.BankedPieces == 2 && s.CheckpointReached && s.ClearedMask == 1 && s.LeftFloor1 == 1 && s.LeftFloor2 == 1 && s.CurrentFloor == 2, "late client B never saw the current summary");
            Assert.That(peerB.Current, Is.EqualTo(host.Current), "late joiner matches the host exactly");

            // Failed dive: unbanked pieces return to their floors on every peer; banked stay; checkpoint and cleared floors stay.
            Assert.That(host.ServerAddPiece(300), Is.True);
            Assert.That(host.ServerAddPiece(101), Is.True);
            Assert.That(host.ServerAddLoot(5, 4), Is.True);
            yield return WaitSummary(peerB, s => s.UnbankedPieces == 2 && s.LeftFloor3 == 1 && s.LeftFloor1 == 0, "client B never saw the new unbanked pieces");
            Assert.That(host.ServerFailDive(), Is.True);
            yield return WaitSummary(peerA, s => s.UnbankedPieces == 0 && s.UnbankedLoot == 0 && s.LeftFloor3 == 2 && s.LeftFloor1 == 1 && s.BankedPieces == 2 && s.CurrentFloor == 0, "client A never saw the failed dive");
            yield return WaitSummary(peerB, s => s.UnbankedPieces == 0 && s.LeftFloor3 == 2 && s.LeftFloor1 == 1 && s.CheckpointReached && s.ClearedMask == 1, "client B never saw the failed dive");
            Assert.That(peerA.Current, Is.EqualTo(host.Current));
            Assert.That(peerB.Current, Is.EqualTo(host.Current));
        }
    }
}
