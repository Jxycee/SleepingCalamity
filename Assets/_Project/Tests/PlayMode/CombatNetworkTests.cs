// Authority: test-only. The M3 co-op hit flow on the real stack (docs/PHASE1.md 6.4): two peers in one process (the App as host, a TestClient as the
// second peer, scene management off on both, Arena_Combat loaded additively), the owner's input driven through FakeInputService.
//  1. The client's swing: its owner detects the contact, plays the spark at once, sends a hit intent; the HOST applies exactly one hit of damage (the
//     two copies of the enemy in one process must not double it) and both peers see the same HP.
//  2. An enemy attack on the client is judged by the client: not dodging, it loses HP on the host and on both peers; dodging (i-frames), it is never hit.
//     The attack is a 30 m circle, so a roll cannot leave it: only the i-frames can save the hero.
//  3. The network simulator (Multiplayer Tools) at 150 ms RTT and 2% loss: a hit registers on the attacker's client within a round trip.
// Catches: a hit intent that never reaches the host, a host that double-applies (two enemy copies), HP that does not replicate, an enemy hit that
// ignores a dodge, a dodge that does nothing, a melee that waits for the host before it can feel like a hit.
// NOT covered by a one-process test (the manual 2-player walk on the PC): two real machines, a second camera, a human's timing.
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class CombatNetworkTests
    {
        private const float Timeout = 20f;

        private PlayModeHarness.TestClient _client;
        private bool _layersChanged;
        private bool[] _savedIgnore;
        private GameObject _simObject;

        private static readonly int[][] LayerPairs =
        {
            new[] { Layers.Player, Layers.RemotePlayer },
            new[] { Layers.Player, Layers.Player },
            new[] { Layers.RemotePlayer, Layers.RemotePlayer },
        };

        private sealed class Ctx
        {
            public AppBootstrap App;
            public NetworkManager HostNm, ClientNm;
            public NetworkObject HostHero, ClientHeroOnClient, ClientHeroOnHost;
            public HeroCombat HostCombat, ClientCombat;
            public FakeInputService HostInput = new FakeInputService(), ClientInput = new FakeInputService();
            public CombatHost Combat;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_simObject != null) { Object.Destroy(_simObject); _simObject = null; }
            if (_layersChanged)
            {
                for (int i = 0; i < LayerPairs.Length; i++) Physics.IgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1], _savedIgnore[i]);
                _layersChanged = false;
            }
            if (_client != null)
            {
                yield return _client.LeaveAndDestroy();
                _client = null;
            }
            yield return PlayModeHarness.TearDownApp();
            Scene arena = SceneManager.GetSceneByName(SceneNames.ArenaCombat);
            if (arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        // Host + client connected, the combat arena loaded additively with waves and AI OFF (the test places enemies and drives their attacks), one hero each:
        // the host's at the origin, the client's at (8, 0, 0), both facing +Z.
        private IEnumerator Setup(Ctx c, bool simulate = false)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            c.App = AppBootstrap.Current;
            if (simulate)
            {
                _simObject = new GameObject("NetSim");
                var sim = _simObject.AddComponent<NetworkSimulator>();
                sim.ConnectionPreset = NetworkSimulatorPreset.Create("test 150 ms / 2%", "M3 walk", packetDelayMs: 37, packetJitterMs: 0, packetLossInterval: 0, packetLossPercent: 2);
            }
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            c.HostNm = c.App.NetworkManager;

            _client = new PlayModeHarness.TestClient(c.App, PlayModeHarness.PlayerId(9));
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            c.ClientNm = _client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => c.HostNm.ConnectedClientsIds.Count == 2 && c.ClientNm.IsConnectedClient, Timeout, "the client never connected");

            foreach (Camera cam in Object.FindObjectsByType<Camera>()) if (cam.name == "LobbyCamera") cam.enabled = false;
            foreach (AudioListener l in Object.FindObjectsByType<AudioListener>()) if (l.name == "LobbyCamera") l.enabled = false;
            yield return SceneManager.LoadSceneAsync(SceneNames.ArenaCombat, LoadSceneMode.Additive);

            _savedIgnore = new bool[LayerPairs.Length];
            for (int i = 0; i < LayerPairs.Length; i++)
            {
                _savedIgnore[i] = Physics.GetIgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1]);
                Physics.IgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1], true);
            }
            _layersChanged = true;

            c.Combat = Object.FindAnyObjectByType<CombatHost>();
            Assert.That(c.Combat, Is.Not.Null, "Arena_Combat has the CombatHost");
            c.Combat.WavesEnabled = false;
            c.Combat.AiEnabled = false;
            c.Combat.CritChance = 0f;
            Assert.That(c.Combat.ServerBegin(c.HostNm), Is.True);

            ulong clientId = c.ClientNm.LocalClientId;
            c.HostHero = c.HostNm.SpawnManager.InstantiateAndSpawn(c.App.PlayerPrefab, c.HostNm.LocalClientId, false, true, false, Vector3.zero, Quaternion.identity);
            c.ClientHeroOnHost = c.HostNm.SpawnManager.InstantiateAndSpawn(c.App.PlayerPrefab, clientId, false, true, false, new Vector3(8f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => c.ClientNm.LocalClient != null && c.ClientNm.LocalClient.PlayerObject != null
                                                          && c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(c.HostHero.NetworkObjectId), Timeout, "the client never received the heroes");
            c.ClientHeroOnClient = c.ClientNm.LocalClient.PlayerObject;

            c.HostCombat = c.HostHero.GetComponent<HeroCombat>();
            c.ClientCombat = c.ClientHeroOnClient.GetComponent<HeroCombat>();
            Hook(c.HostHero, c.HostInput);
            Hook(c.ClientHeroOnClient, c.ClientInput);
            yield return Seconds(0.6f);   // settle on the ground
        }

        private static void Hook(NetworkObject hero, FakeInputService input)
        {
            hero.GetComponent<HeroLocomotion>().Input = input;
            hero.GetComponent<TargetLockController>().Input = input;
            hero.GetComponent<HeroCombat>().Input = input;
        }

        private static Enemy ClientCopy(Ctx c, Enemy hostEnemy)
        {
            return c.ClientNm.SpawnManager.SpawnedObjects[hostEnemy.NetworkObjectId].GetComponent<Enemy>();
        }

        private static IEnumerator PressAttack(FakeInputService input)
        {
            input.Frame.PrimaryPressed = true;
            input.Frame.PrimaryHeld = true;
            yield return null;
            input.Frame.PrimaryPressed = false;
            input.Frame.PrimaryHeld = false;
            input.Frame.PrimaryReleased = true;
            yield return null;
            input.Frame.PrimaryReleased = false;
        }

        // ---- 1. a client swing -> one hit on the host, both peers see the HP ----

        [UnityTest]
        public IEnumerator ClientSwing_SendsHitIntent_HostAppliesDamageOnce_BothPeersSeeHp()
        {
            var c = new Ctx();
            yield return Setup(c);

            Enemy hostEnemy = c.Combat.SpawnEnemy(EnemyKind.Slime, 1, new Vector3(8f, 0f, 1.6f), ai: false);
            Assert.That(hostEnemy, Is.Not.Null);
            yield return PlayModeHarness.WaitUntil(() => c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(hostEnemy.NetworkObjectId), Timeout, "the client never received the enemy");
            Enemy clientEnemy = ClientCopy(c, hostEnemy);
            Assert.That(clientEnemy.NetHp.Value, Is.EqualTo(68), "40 x 1.7 for two players (P23)");

            bool sparked = false;
            System.Action<LocalHit> onHit = h => sparked = true;
            CombatEvents.LocalHitLanded += onHit;
            try
            {
                yield return PressAttack(c.ClientInput);
                yield return PlayModeHarness.WaitUntil(() => sparked, 2f, "the attacker's client never played its own hit (spark) locally");
                yield return PlayModeHarness.WaitUntil(() => hostEnemy.Model.Hp < 68, 3f, "the host never applied the hit intent");
            }
            finally { CombatEvents.LocalHitLanded -= onHit; }

            yield return PlayModeHarness.WaitUntil(() => clientEnemy.NetHp.Value < 68, 3f, "the client never saw the new HP");
            yield return Seconds(0.4f);
            Assert.That(hostEnemy.Model.Hp, Is.EqualTo(47), "22 x 100/105 = 21 damage from 68, applied once (not once per enemy copy)");
            Assert.That(hostEnemy.NetHp.Value, Is.EqualTo(47), "the host's copy shows it");
            Assert.That(clientEnemy.NetHp.Value, Is.EqualTo(47), "the client's copy shows it");
            Assert.That(c.ClientCombat.ComboCount, Is.EqualTo(1), "the attacker's own combo counted the hit");
            Assert.That(c.HostCombat.ComboCount, Is.EqualTo(0), "the host hero swung at nothing");
        }

        // ---- 2. an enemy attack on the client: judged by the client's dodge ----

        [UnityTest]
        public IEnumerator EnemyHit_OnClient_LandsWhenStandingStill_ButIsIgnoredWhileDodging()
        {
            var c = new Ctx();
            yield return Setup(c);

            Enemy hostEnemy = c.Combat.SpawnEnemy(EnemyKind.Slime, 1, new Vector3(8f, 0f, 3f), ai: false);
            yield return PlayModeHarness.WaitUntil(() => c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(hostEnemy.NetworkObjectId), Timeout, "the client never received the enemy");
            Enemy clientEnemy = ClientCopy(c, hostEnemy);
            HeroVitals clientVitalsOnClient = c.ClientHeroOnClient.GetComponent<HeroVitals>();
            HeroVitals clientVitalsOnHost = c.ClientHeroOnHost.GetComponent<HeroVitals>();
            HeroLocomotion clientLoco = c.ClientHeroOnClient.GetComponent<HeroLocomotion>();

            // Control: standing still, the nova hits (slime atk 12 x 1.0 x 100/110 = 10.9 -> 11).
            hostEnemy.ServerForceAttack(AttackCatalog.TestNova, c.ClientHeroOnHost.transform.position);
            yield return PlayModeHarness.WaitUntil(() => clientEnemy.Attacking, 2f, "the client never saw the enemy attack");
            yield return PlayModeHarness.WaitUntil(() => clientVitalsOnHost.Model.Hp < 100, 3f, "an enemy hit on a client that did not dodge never reached the host");
            yield return PlayModeHarness.WaitUntil(() => clientVitalsOnClient.Hp < 100, 3f, "the client never saw its own HP drop");
            Assert.That(clientVitalsOnHost.Model.Hp, Is.EqualTo(89), "the control hit");
            Assert.That(clientVitalsOnClient.Hp, Is.EqualTo(89), "both peers agree");

            // Reset the hero, let the first attack end, and do it again with a roll started as the attack appears.
            yield return Seconds(1.4f);
            clientVitalsOnHost.ServerFullReset();
            yield return PlayModeHarness.WaitUntil(() => clientVitalsOnClient.Hp == 100, 3f, "the reset never replicated");
            int serialBefore = clientEnemy.Serial;
            hostEnemy.ServerForceAttack(AttackCatalog.TestNova, c.ClientHeroOnHost.transform.position);
            yield return PlayModeHarness.WaitUntil(() => clientEnemy.Serial != serialBefore && clientEnemy.Attacking, 2f, "the client never saw the second attack");
            c.ClientInput.Frame.MoveX = 1f;
            c.ClientInput.Frame.DodgePressed = true;
            yield return null;
            c.ClientInput.Frame.DodgePressed = false;
            bool rolled = clientLoco.IsDodging;
            yield return PlayModeHarness.WaitUntil(() => clientLoco.IsDodging || rolled, 0.5f, "the client never started its roll");
            Assert.That(clientEnemy.HitboxOn || clientEnemy.AttackFrame < clientEnemy.CurrentAttack.HitOn.Start, Is.True, "the roll began before the hitbox went live");
            yield return Seconds(1.3f);   // the whole attack: its hitbox was live for 28 frames while the roll's i-frames covered the start
            c.ClientInput.Frame = default;

            Assert.That(clientVitalsOnHost.Model.Hp, Is.EqualTo(100), "a dodging client is never hit (the host's record)");
            Assert.That(clientVitalsOnClient.Hp, Is.EqualTo(100), "and its own copy agrees");
        }

        // ---- 3. the network simulator: 150 ms RTT, 2% loss ----

        [UnityTest]
        public IEnumerator Netsim_150msRtt_2PercentLoss_AClientHitRegistersWithinOneRoundTrip()
        {
            var c = new Ctx();
            yield return Setup(c, simulate: true);

            // The simulator delays every packet at the send and the receive stage of each peer, so 4 x 37 ms gives the 150 ms round trip (75 ms measured 300).
        // A tough target so a dozen hits do not kill it: the elite (600 HP), AI off, standing in front of the client.
            Enemy hostEnemy = c.Combat.SpawnEnemy(EnemyKind.DoughWarden, 1, new Vector3(8f, 0f, 2.6f), ai: false);
            yield return PlayModeHarness.WaitUntil(() => c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(hostEnemy.NetworkObjectId), Timeout, "the client never received the enemy");
            Enemy clientEnemy = ClientCopy(c, hostEnemy);

            var transport = c.ClientNm.NetworkConfig.NetworkTransport as UnityTransport;
            yield return Seconds(1.0f);
            ulong rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);

            var contact = new Queue<double>();
            var latencies = new List<double>();
            System.Action<LocalHit> onHit = h => contact.Enqueue(Time.realtimeSinceStartupAsDouble);
            clientEnemy.NetHp.OnValueChanged += (previous, current) =>
            {
                if (current < previous && contact.Count > 0) latencies.Add((Time.realtimeSinceStartupAsDouble - contact.Dequeue()) * 1000.0);
            };
            CombatEvents.LocalHitLanded += onHit;
            int swings = 8;
            try
            {
                for (int i = 0; i < swings; i++)
                {
                    yield return PressAttack(c.ClientInput);
                    yield return Seconds(1.1f);   // the chain (hit, hit, finisher) is slower than this: each press is a fresh swing or the next hit
                }
                yield return Seconds(1.0f);
            }
            finally { CombatEvents.LocalHitLanded -= onHit; }

            latencies.Sort();
            string report = "SC NETSIM rtt(transport)=" + rtt + " ms; hits sent=" + swings + "; hits seen on the attacker's client=" + latencies.Count + "; latencies(ms)=" + string.Join(",", latencies.ConvertAll(l => ((int)l).ToString()));
            TestContext.WriteLine(report);
            Debug.Log(report);

            Assert.That(latencies.Count, Is.GreaterThanOrEqualTo(swings - 2), "most hits arrived back (2% loss, reliable RPCs): " + report);
            double median = latencies[latencies.Count / 2];
            double max = latencies[latencies.Count - 1];
            Assert.That(median, Is.GreaterThan(60.0), "the simulator is in effect (a local round trip is a frame or two): " + report);
            // One round trip is 150 ms; allow two server ticks (33 ms each at 30 Hz) and a frame for the intent and the replicated value to be sampled.
            Assert.That(median, Is.LessThan(150.0 + 2 * 33.4 + 40.0), "the median hit registers within one round trip: " + report);
            Assert.That(max, Is.LessThan(150.0 * 3 + 200.0), "even a resent packet arrives well inside half a second: " + report);
        }
    }
}
