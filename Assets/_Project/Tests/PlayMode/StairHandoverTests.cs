// Authority: test-only. Stair / go-home hand-over: when the host swaps the dive floor, no hero (host copy or owner copy) may sink through the cells the new floor lacks before its arrival teleport lands.
// Host + 1 client in one process (DiveSpineNetworkTests rig), pinned seed 12345 (floor 2's stair cell has no slab on floor 3, the QA finding in Reference/qa/m7/FLOOR3_FALL.md),
// every hero's locomotion driven by a FakeInputService so real gravity runs. Y of every hero copy is sampled EVERY frame across F1->F2->F3 (through the real RequestStair path) and an abandon-home.
// Catches: the 0.2 s placement gap (host), the RPC + client floor-build gap (owner), a gravity hold that never clears.
// NOT covered: the hub has no ground in this rig (so nothing is asserted about standing at home), real relay latency beyond NetCondition.
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    public class StairHandoverTests
    {
        private const float Timeout = 30f;
        private const float MaxSink = 0.1f;   // far tighter than the 1 m brief: with test-rate frames a 0.2 s placement gap only drops a hero ~0.2 m; a latent hero drops metres
        private static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient c in _clients) yield return c.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list");
            return null;
        }

        private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        private static void Move(NetworkObject ownerCopy, Vector3 pos)
        {
            var cc = ownerCopy.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            ownerCopy.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm; public PlayModeHarness.TestClient Client;
            public DiveDirector Host, Peer;
            public NetworkObject HostHero, ClientHeroOnHost, ClientHeroOwn;
            public CellarFloorSync HostFloor, PeerFloor;
            public DiveHaulSync HostHaul;
            public readonly FakeInputService HostInput = new FakeInputService(), ClientInput = new FakeInputService();
            // Per-frame Y sampling of every hero copy.
            public float MinY = float.MaxValue; public string MinWhere = ""; public int Frames;
            public bool ClientHoldSeen; public int ClientHeldFrames, HeldNotGrounded;   // the client owner copy's gravity hold, per leg (HeldNotGrounded: held 3+ frames yet IsGrounded false)
            public NetworkObject[] Copies => new[] { HostHero, ClientHeroOnHost, ClientHeroOwn };
            public readonly string[] Names = { "host hero (host copy)", "client hero (host copy)", "client hero (owner copy)" };
        }

        private IEnumerator Start(NetCondition net, System.Action<Rig> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            PlayModeHarness.AddNetworkSimulator(net);
            var rig = new Rig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            var client = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(8));
            _clients.Add(client); rig.Client = client;
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "[start] the client never connected");
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(Home, HubStair, Origin), Is.True);
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "[start] the client never received the director");
            rig.Peer = client.NetworkManager.SpawnManager.SpawnedObjects[dirObj.NetworkObjectId].GetComponent<DiveDirector>();
            rig.HostHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, Home, Quaternion.identity);
            NetworkManager cnm = client.NetworkManager;
            rig.ClientHeroOnHost = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, cnm.LocalClientId, false, true, false, Home + new Vector3(2f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null, Timeout, "[start] the client never received its hero");
            rig.ClientHeroOwn = cnm.LocalClient.PlayerObject;
            yield return Seconds(0.5f);
            done(rig);
        }

        private static void Hook(NetworkObject hero, FakeInputService input)
        {
            hero.GetComponent<HeroLocomotion>().Input = input;
            hero.GetComponent<TargetLockController>().Input = input;
            hero.GetComponent<HeroCombat>().Input = input;
        }

        // One frame of sampling: every hero copy's Y, tracked as the run's minimum.
        private static void Sample(Rig r)
        {
            r.Frames++;
            var ownLoco = r.ClientHeroOwn.GetComponent<HeroLocomotion>();
            if (ownLoco.HoldVertical)
            {
                r.ClientHoldSeen = true; r.ClientHeldFrames++;
                if (r.ClientHeldFrames >= 3 && !ownLoco.IsGrounded) r.HeldNotGrounded++;
            }
            else r.ClientHeldFrames = 0;
            NetworkObject[] copies = r.Copies;
            for (int i = 0; i < copies.Length; i++)
            {
                float y = copies[i].transform.position.y;
                if (y < r.MinY) { r.MinY = y; r.MinWhere = r.Names[i] + " frame " + r.Frames + " at " + copies[i].transform.position; }
            }
        }

        // Waits for `cond` while sampling every frame.
        private static IEnumerator Track(Rig r, System.Func<bool> cond, string what)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + Timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("Timed out: " + what);
                Sample(r);
                PlayModeHarness.Pace();
                yield return null;
            }
            Sample(r);
        }

        private static IEnumerator AllBuilt(Rig r, int floor)
        {
            yield return Track(r, () => r.HostFloor.BuiltFloor == floor && r.HostFloor.Built != null && r.PeerFloor.BuiltFloor == floor && r.PeerFloor.Built != null, "floor " + floor + " built on both peers");
        }

        // Every hero (host copy of each) within 6 m flat of its entry, the owner copy too, and the owner's gravity hold released.
        private static IEnumerator Landed(Rig r, int floor)
        {
            yield return Track(r, () =>
            {
                Vector3 e0 = CellarFloorBuilder.EntryWorld(r.HostFloor.Layout, r.HostFloor.Built, 0), e1 = CellarFloorBuilder.EntryWorld(r.HostFloor.Layout, r.HostFloor.Built, 1);
                return Flat(r.HostHero.transform.position, e0) < 6f && Flat(r.ClientHeroOnHost.transform.position, e1) < 6f && Flat(r.ClientHeroOwn.transform.position, e1) < 6f;
            }, "every hero at the floor-" + floor + " entry (host " + r.HostHero.transform.position + ", client on host " + r.ClientHeroOnHost.transform.position + ", client own " + r.ClientHeroOwn.transform.position + ")");
            // The hold is released and the heroes stand on the new floor.
            yield return Track(r, () => !r.HostHero.GetComponent<HeroLocomotion>().HoldVertical && !r.ClientHeroOwn.GetComponent<HeroLocomotion>().HoldVertical
                                        && r.HostHero.GetComponent<HeroLocomotion>().IsGrounded && r.ClientHeroOwn.GetComponent<HeroLocomotion>().IsGrounded,
                "floor " + floor + ": heroes grounded and the gravity hold released (host hold " + r.HostHero.GetComponent<HeroLocomotion>().HoldVertical + ", client hold " + r.ClientHeroOwn.GetComponent<HeroLocomotion>().HoldVertical + ")");
        }

        private static readonly List<string> Failures = new List<string>();

        // Logs the leg's min Y, records a failure when a hero sank, and starts a fresh leg. The failures are asserted together at the end so one run reports every leg.
        private static void AssertNoSink(Rig r, string leg, bool expectClientHold = false, NetCondition net = NetCondition.None)
        {
            Debug.Log("SC_STAIR " + leg + ": client hold seen " + r.ClientHoldSeen);
            if (expectClientHold && !r.ClientHoldSeen && leg.Contains("F1")) Failures.Add(leg + ": the client owner copy's HoldVertical was never true between the floor change and the teleport");
            if (r.HeldNotGrounded > 0) Failures.Add(leg + ": IsGrounded was false for " + r.HeldNotGrounded + " frames while HoldVertical was held (dodge/sprint need a grounded hero)");
            r.ClientHoldSeen = false; r.ClientHeldFrames = 0; r.HeldNotGrounded = 0;
            string line = leg + ": min Y " + r.MinY.ToString("F2") + " (" + r.MinWhere + ", " + r.Frames + " frames sampled)";
            Debug.Log("SC_STAIR " + line);
            if (!(r.MinY > Origin.y - MaxSink)) Failures.Add("a hero sank: " + line);
            r.MinY = float.MaxValue; r.Frames = 0;
        }

        [UnityTest]
        public IEnumerator Stairs_F1ToF3_ThenHome_NoHeroSinksAndTheHoldClears([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            Failures.Clear();
            Rig r = null;
            yield return Start(net, x => r = x);
            r.Host.SeedOverride = 12345u;
            Assert.That(r.Host.ServerStartDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => r.Host.FloorSync != null && r.Host.HaulSync != null, Timeout, "the dive never spawned its syncs");
            r.HostFloor = r.Host.FloorSync; r.HostHaul = r.Host.HaulSync;
            ulong fid = r.HostFloor.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => r.Client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(fid), Timeout, "the client never received the floor sync");
            r.PeerFloor = r.Client.NetworkManager.SpawnManager.SpawnedObjects[fid].GetComponent<CellarFloorSync>();
            yield return AllBuilt(r, 1);

            // Heroes arrive (no locomotion input yet, so nothing falls in the hub), then real gravity is switched on.
            yield return Track(r, () =>
            {
                Vector3 e0 = CellarFloorBuilder.EntryWorld(r.HostFloor.Layout, r.HostFloor.Built, 0), e1 = CellarFloorBuilder.EntryWorld(r.HostFloor.Layout, r.HostFloor.Built, 1);
                return Flat(r.HostHero.transform.position, e0) < 6f && Flat(r.ClientHeroOwn.transform.position, e1) < 6f && Flat(r.ClientHeroOnHost.transform.position, e1) < 6f;
            }, "floor-1 arrival");
            Hook(r.HostHero, r.HostInput);
            Hook(r.ClientHeroOwn, r.ClientInput);
            yield return Track(r, () => r.HostHero.GetComponent<HeroLocomotion>().IsGrounded && r.ClientHeroOwn.GetComponent<HeroLocomotion>().IsGrounded, "heroes grounded on floor 1");
            r.MinY = float.MaxValue; r.Frames = 0;   // from here every frame counts
            AssertNoSink(r, "floor 1 settle");

            for (int floor = 1; floor <= 2; floor++)
            {
                Assert.That(CellarFloorBuilder.TryStairWorld(r.HostFloor.Layout, r.HostFloor.Built, out Vector3 stair), Is.True, "floor " + floor + " has a stair");
                Move(r.ClientHeroOwn, stair + new Vector3(1f, 0.1f, 0f));
                Move(r.HostHero, stair + new Vector3(-1f, 0.1f, 0f));   // the host hero stands over the stair cell too: it is the one the 0.2 s placement gap drops
                yield return Track(r, () => Flat(r.ClientHeroOnHost.transform.position, stair) < 3f, "the client hero reaching the floor-" + floor + " stair on the host");
                int f = floor;
                r.Host.ServerOpenStair(floor);   // the gate key is on by default once the contents spawn
                r.ClientHoldSeen = false; r.ClientHeldFrames = 0; r.HeldNotGrounded = 0;
                r.Host.TestDelayPlacement(5f);   // the 0.2 s tick can never place anyone in time now: only the swap-frame placement can
                if (floor == 1) r.Peer.TestDelayApply(1f);   // the client owner copy holds its arrival teleport back 1 s: the gravity-hold window is open for real
                if (floor == 1) r.Peer.RequestStair(1);   // the real client request path
                else
                {
                    Assert.That(r.Host.ServerTryStair(r.HostNm.LocalClientId, 2), Is.True, "host stair press F2");   // the host hero presses, through the host rule directly
                    Assert.That(r.Host.TestPlacedThisGen(r.HostNm.LocalClientId) && r.Host.TestPlacedThisGen(r.Client.NetworkManager.LocalClientId), Is.True, "every hero was placed in the swap frame (before the call returned)");
                    Assert.That(r.Host.HasPendingTeleport, Is.True, "the host-owner copy has its arrival teleport in hand in the swap frame");
                }
                yield return Track(r, () => r.HostHaul.Current.CurrentFloor == f + 1, "the host advancing to floor " + (f + 1));
                yield return AllBuilt(r, f + 1);
                yield return Landed(r, f + 1);
                yield return Seconds(0.3f);
                AssertNoSink(r, "stair F" + floor + " -> F" + (floor + 1), true, net);
            }

            // Go home (the abandon path ends the dive like a fail): both heroes are sent to the hub; the floor stays up for the clear delay.
            var ground = new GameObject("TestHubGround"); ground.transform.position = new Vector3(0f, -0.5f, -6f);
            ground.AddComponent<BoxCollider>().size = new Vector3(60f, 1f, 60f);   // the rig has no hub, so give the arrival teleport something to stand on
            Assert.That(r.Host.ServerAbandonDive(), Is.True);
            yield return Track(r, () => Flat(r.HostHero.transform.position, Home) < 3f && Flat(r.ClientHeroOnHost.transform.position, Home) < 8f && Flat(r.ClientHeroOwn.transform.position, Home) < 8f, "both heroes sent home");
            AssertNoSink(r, "abandon-home hand-over (up to arrival)");
            yield return Track(r, () => !r.HostHero.GetComponent<HeroLocomotion>().HoldVertical && !r.ClientHeroOwn.GetComponent<HeroLocomotion>().HoldVertical, "the gravity hold released after the trip home");
            Object.Destroy(ground);
            Assert.That(Failures, Is.Empty, string.Join(" | ", Failures));
        }
    }
}
