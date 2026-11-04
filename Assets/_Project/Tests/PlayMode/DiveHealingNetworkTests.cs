// Authority: test-only. P47 dive healing: the host heals (DiveHaulSync + HeroVitals), every peer reads the recharge.
// Host + one client in one process (scene management off); a CellarFloorSync builds floor 1 of a seed that has a healing point, a DiveHaulSync holds the dive, two real
// hero prefabs (host's and the client's) stand where the test puts them.
//  1. A hurt hero standing on the point is healed to full by the host, a hurt hero elsewhere is not; the point goes on a short recharge that the client sees
//     (NetHealSecondsLeft > 0, the built orb hidden) and then ready again (orb back).
//  2. While recharging nobody is healed; when it ends, a hero who walked onto the point meanwhile (the client's) is healed, and so is the one still standing there.
//  3. A bank (the client asks by RPC at the floor-2 checkpoint) fully heals every living hero on the host and both peers see the new HP; a downed hero is not touched.
// Catches: a heal that never reaches the host's vitals or the other peer, a point that heals during its recharge or never recharges, a recharge a client cannot see, a bank
// that banks without healing, a heal for heroes of the wrong network manager.
// NOT covered by a one-process test: real relay latency, the owner's own movement onto the point (the client hero is placed by teleport), late join (the variable is a plain
// NetworkVariable, covered by the haul late-join test).
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
    public class DiveHealingNetworkTests
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

        private static GameObject PrefabWith<T>(NetworkManager host) where T : Component
        {
            foreach (NetworkPrefabsList list in host.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab;
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static uint SeedWithPointOnFloor1()
        {
            for (uint seed = 1; seed < 500; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, 1);
                if (CellarHealing.PointFor(l, CellarFloorDressing.Resolve(l)).HasValue) return seed;
            }
            Assert.Fail("no seed below 500 has a healing point on floor 1"); return 0;
        }

        private static void Teleport(NetworkObject hero, Vector3 pos)
        {
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            hero.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        private static IEnumerator WaitDump(System.Func<bool> ok, float seconds, System.Func<string> what)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!ok()) { if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("Timed out: " + what()); yield return null; }
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        [UnityTest]
        public IEnumerator HealingPoint_HealsWhoStandsOnIt_Recharges_AndBankHealsEveryone()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;

            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            NetworkManager clientNm = client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => hostNm.ConnectedClientsIds.Count == 2 && clientNm.IsConnectedClient, Timeout, "the client never connected");

            uint seed = SeedWithPointOnFloor1();
            NetworkObject floorObj = hostNm.SpawnManager.InstantiateAndSpawn(PrefabWith<CellarFloorSync>(hostNm).GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            CellarFloorSync floor = floorObj.GetComponent<CellarFloorSync>();
            NetworkObject haulObj = hostNm.SpawnManager.InstantiateAndSpawn(PrefabWith<DiveHaulSync>(hostNm).GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveHaulSync haul = haulObj.GetComponent<DiveHaulSync>();
            haul.FloorSync = floor;
            yield return PlayModeHarness.WaitUntil(() => clientNm.SpawnManager.SpawnedObjects.ContainsKey(floorObj.NetworkObjectId) && clientNm.SpawnManager.SpawnedObjects.ContainsKey(haulObj.NetworkObjectId), Timeout, "the client never received the sync objects");
            DiveHaulSync peerHaul = clientNm.SpawnManager.SpawnedObjects[haulObj.NetworkObjectId].GetComponent<DiveHaulSync>();
            CellarFloorSync peerFloor = clientNm.SpawnManager.SpawnedObjects[floorObj.NetworkObjectId].GetComponent<CellarFloorSync>();
            peerHaul.FloorSync = peerFloor;   // one process holds both peers' floor syncs: pin the client's so it never finds the host's

            const float Recharge = 3f;
            Assert.That(haul.ServerBegin(new[] { 2, 2, 2 }), Is.True);
            Assert.That(haul.ServerSetHealTuning(new HealTuning { ChancePercent = 51, RechargeSeconds = Recharge, RadiusMeters = 1.6f }), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(floor.ServerSetFloor(seed, 1), Is.True);
            yield return PlayModeHarness.WaitUntil(() => peerFloor.BuiltFloor == 1 && peerFloor.Built != null, Timeout, "the client never built the floor");
            Assert.That(floor.Built.HealingCell.HasValue, Is.True, "the built floor has the healing point");
            Assert.That(peerFloor.Built.HealingCell.HasValue, Is.True, "the client built the same point");
            CellPos hc = floor.Built.HealingCell.Value;
            Vector3 point = floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(hc.X, hc.Y));
            Vector3 far = point + new Vector3(60f, 0f, 60f);
            Assert.That(floor.Built.HealingOrb.activeSelf, Is.True, "ready: the orb shows");

            // Two heroes far from the point.
            NetworkObject hostHero = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, hostNm.LocalClientId, false, true, false, far, Quaternion.identity);
            NetworkObject clientHeroOnHost = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, clientNm.LocalClientId, false, true, false, far + new Vector3(8f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => clientNm.LocalClient != null && clientNm.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            NetworkObject clientHero = clientNm.LocalClient.PlayerObject;
            HeroVitals hostV = hostHero.GetComponent<HeroVitals>(), clientV = clientHeroOnHost.GetComponent<HeroVitals>(), clientVOnClient = clientHero.GetComponent<HeroVitals>();
            yield return Seconds(0.5f);

            // 1. Hurt both; only the host hero walks onto the point.
            hostV.ServerDamage(40); clientV.ServerDamage(30);
            yield return PlayModeHarness.WaitUntil(() => clientVOnClient.Hp == 70, Timeout, "the client never saw its damage");
            Teleport(hostHero, point);
            yield return WaitDump(() => { Teleport(hostHero, point); return hostV.Hp == 100; }, Timeout, () => "the point never healed the hero standing on it (host hero at " + hostHero.transform.position + ", point " + point + ")");
            Assert.That(clientV.Hp, Is.EqualTo(70), "the hero away from the point is not healed");
            yield return PlayModeHarness.WaitUntil(() => peerHaul.NetHealSecondsLeft.Value > 0, Timeout, "the client never saw the recharge");
            Assert.That(peerHaul.NetHealSecondsLeft.Value, Is.LessThanOrEqualTo(Mathf.CeilToInt(Recharge)));
            Assert.That(floor.Built.HealingOrb.activeSelf, Is.False, "recharging: the host's orb is dark");
            yield return PlayModeHarness.WaitUntil(() => !peerFloor.Built.HealingOrb.activeSelf, Timeout, "the client's orb stayed lit during the recharge");

            // 2. Hurt again while recharging: nothing happens. The client hero walks on meanwhile.
            hostV.ServerDamage(25);
            double holdUntil = Time.realtimeSinceStartupAsDouble + 1.0;
            while (Time.realtimeSinceStartupAsDouble < holdUntil) { Teleport(hostHero, point); Teleport(clientHero, point); yield return null; }
            Assert.That(hostV.Hp, Is.EqualTo(75), "a recharging point heals nobody");
            Assert.That(clientV.Hp, Is.EqualTo(70), "...not the client's hero either");
            yield return WaitDump(() => { Teleport(hostHero, point); Teleport(clientHero, point); return hostV.Hp == 100; }, Recharge + 8f, () => "when the recharge ended the host hero on the point was not healed (hp " + hostV.Hp + ", host hero at " + hostHero.transform.position + ", point " + point + ", secs " + haul.NetHealSecondsLeft.Value + ")");
            yield return WaitDump(() => { Teleport(hostHero, point); Teleport(clientHero, point); return clientV.Hp == 100; }, Recharge + 8f, () => "the client hero standing on the point was not healed (hp " + clientV.Hp + ", on host at " + clientHeroOnHost.transform.position + ", on client at " + clientHero.transform.position + ", point " + point + ")");
            yield return PlayModeHarness.WaitUntil(() => clientVOnClient.Hp == 100, Timeout, "the client never saw its heal");
            yield return PlayModeHarness.WaitUntil(() => peerHaul.NetHealSecondsLeft.Value > 0, Timeout, "the second use never started a recharge");
            yield return PlayModeHarness.WaitUntil(() => peerHaul.NetHealSecondsLeft.Value == 0 && peerFloor.Built.HealingOrb.activeSelf, Recharge + 8f, "the point never became ready again on the client");

            // 3. A bank heals everyone. Move the dive to the checkpoint floor (the built floor lags, so no range check) and let the client ask.
            Teleport(hostHero, far); Teleport(clientHero, far + new Vector3(8f, 0f, 0f));
            yield return Seconds(0.3f);
            hostV.ServerDamage(50); clientV.ServerDamage(60);
            Assert.That(haul.ServerSetFloor(2), Is.True);
            int banked = 0; haul.Banked += () => banked++;
            peerHaul.RequestBank();
            yield return PlayModeHarness.WaitUntil(() => haul.Current.CheckpointReached, Timeout, "the bank request never banked");
            Assert.That(banked, Is.EqualTo(1), "Banked was raised once");
            Assert.That(hostV.Hp, Is.EqualTo(100), "the bank healed the host hero");
            Assert.That(clientV.Hp, Is.EqualTo(100), "the bank healed the client hero on the host");
            yield return PlayModeHarness.WaitUntil(() => clientVOnClient.Hp == 100, Timeout, "the client never saw the bank heal");
            Assert.That(peerHaul.ServerBank(), Is.False, "a client cannot bank or heal directly");

            // A downed hero is not healed by a bank (the revive rule stands).
            clientV.ServerDamage(500);
            Assert.That(clientV.IsDowned, Is.True);
            Assert.That(haul.ServerBank(), Is.False, "bank spam at a spent point is refused");
            Assert.That(haul.ServerAddPiece(200), Is.True);   // a piece is safe at once: it does not reopen a spent bank point
            Assert.That(haul.ServerBank(), Is.False, "a piece no longer waits to bank");
            Assert.That(haul.ServerSetFloor(4), Is.True);   // the next bank point
            Assert.That(haul.ServerBank(), Is.True);
            Assert.That(clientV.IsDowned, Is.True, "a bank does not revive");
        }
    }
}
