// Authority: test-only (host plus one in-process client, real Hollowmere). PL-4 task 4.2: the floor pick is asked by a client and decided by the host against the held checkpoint.
// Catches: a client's pick being trusted (a floor below the bank, or any floor without a checkpoint, starting a dive), a client's allowed pick not starting on that floor, the client's prompt not
// defaulting to the bank floor from the replicated checkpoint. The client is driven by RPC in-process (RequestStartDive is what its Interact press sends); the two-process run is the user's walk.
using System.Collections;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class FloorPickNetworkTests
    {
        private const float Timeout = 40f;
        private PlayModeHarness.TestClient _client;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null) { yield return _client.LeaveAndDestroy(); _client = null; }
            yield return PlayModeHarness.TearDownApp();
        }

        private sealed class Rig { public DiveDirector Host, Peer; public NetworkObject ClientHero, ClientHeroOnHost; }

        private IEnumerator Start(System.Action<Rig> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            SceneFlow.StartSceneOverride = null;
            yield return PlayModeHarness.StartDirectHostWithScenes();
            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the second player never reached the roster");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True, "the multiplayer start gate opens with two players");
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Hollowmere && app.NetworkManager.LocalClient.PlayerObject != null, Timeout, "the host never loaded Hollowmere");
            ServiceRoots.App.Replace<IInputService>(new FakeInputService());   // the real adapter's binding display strings crash a headless Linux editor (keyboard layout lookup)
            var rig = new Rig();
            Transform spawn = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) { Transform t = FindDeep(root.transform, "SpawnPoint"); if (t != null) spawn = t; }
            Assert.That(spawn, Is.Not.Null, "Hollowmere has its spawn point");
            NetworkManager hostNm = app.NetworkManager, clientNm = _client.NetworkManager;
            rig.ClientHeroOnHost = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, clientNm.LocalClientId, false, true, false, spawn.position + new Vector3(1.5f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => clientNm.LocalClient != null && clientNm.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            rig.ClientHero = clientNm.LocalClient.PlayerObject;
            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero, Timeout, "the hub never spawned the director");
            rig.Host = DiveDirector.Instance;
            ulong did = rig.Host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => clientNm.SpawnManager.SpawnedObjects.ContainsKey(did), Timeout, "the client never received the director");
            rig.Peer = clientNm.SpawnManager.SpawnedObjects[did].GetComponent<DiveDirector>();
            yield return TownInputRig.Seconds(1f);
            done(rig);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { Transform r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        private static IEnumerator ClientToTrapdoor(Rig rig)
        {
            Vector3 pos = rig.Peer.NetHubStair.Value + new Vector3(1f, 0.1f, 0f);
            var cc = rig.ClientHero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            rig.ClientHero.transform.position = pos;
            if (cc != null) cc.enabled = true;
            yield return PlayModeHarness.WaitUntil(() => TownInputRig.Flat(rig.ClientHeroOnHost.transform.position, pos) < 1.5f, Timeout, "the client hero never arrived at the trapdoor on the host");
        }

        [UnityTest]
        public IEnumerator FloorPick_ClientPick_HostValidates()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            rig.Host.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.NetCheckpointHeld.Value && rig.Peer.NetPickableMaxFloor.Value == UndercroftPlan.BankFloor, Timeout, "the client never saw the held checkpoint");
            yield return ClientToTrapdoor(rig);
            // The client's own prompt defaults to the bank floor from the replicated checkpoint.
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.Prompt == DiveDirector.PromptKind.Descend && rig.Peer.PickFloor == UndercroftPlan.BankFloor, Timeout, "the client's trapdoor prompt never defaulted to the bank floor (pick " + rig.Peer.PickFloor + ", prompt " + rig.Peer.Prompt + ")");

            // A floor below the bank is refused by the host and the checkpoint stays.
            int handled = rig.Host.RequestsHandled;
            rig.Peer.RequestStartDive(UndercroftPlan.BankFloor + 1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the request never reached the host");
            yield return TownInputRig.Seconds(0.3f);
            Assert.That(rig.Host.NetActive.Value, Is.False, "the host refused a floor below the bank");
            Assert.That(rig.Host.NetCheckpointHeld.Value, Is.True);

            // An allowed pick starts there for the whole party.
            rig.Peer.RequestStartDive(3);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetActive.Value && rig.Peer.NetActive.Value && rig.Host.FloorSync != null && rig.Host.FloorSync.BuiltFloor == 3, Timeout, "the client's allowed pick never started floor 3 on both peers");
            Assert.That(rig.Host.NetCheckpointHeld.Value, Is.False, "the start spent the checkpoint");
        }

        // A peer that cycled its pick, then a dive another peer started and that ended with the checkpoint held, must default to the bank floor again (the touched pick went stale during the dive).
        [UnityTest]
        public IEnumerator FloorPick_TouchedPick_ResetsAfterDive_ReturnDefaultsToBankFloor()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            rig.Host.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.NetCheckpointHeld.Value && rig.Peer.NetPickableMaxFloor.Value == UndercroftPlan.BankFloor, Timeout, "the client never saw the held checkpoint");
            yield return ClientToTrapdoor(rig);
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.Prompt == DiveDirector.PromptKind.Descend && rig.Peer.PickFloor == UndercroftPlan.BankFloor, Timeout, "the client's pick never defaulted to the bank floor");

            // The client cycles the pick with one Lock press (the shared fake input drives every in-process peer).
            var input = (FakeInputService)HeroInput.ResolveDefault();
            input.Frame.LockPressed = true;
            yield return null;
            input.Frame.LockPressed = false;
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.PickFloor != UndercroftPlan.BankFloor, Timeout, "the Lock press never cycled the client's pick");

            // The host starts a dive (spends the checkpoint), then the dive ends with the checkpoint held again.
            Assert.That(rig.Host.ServerStartDive(3), Is.True, "the host starts on floor 3");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetActive.Value && rig.Peer.NetActive.Value && rig.Host.FloorSync != null && rig.Host.FloorSync.BuiltFloor == 3, Timeout, "the dive never started on both peers");
            yield return PlayModeHarness.WaitUntil(() => !rig.Peer.HasPendingTeleport, Timeout, "the client never arrived on the dive floor");
            yield return TownInputRig.Seconds(0.3f);   // the peer's own Update sees the dive running
            rig.Host.TestHoldCheckpoint(true);
            Assert.That(rig.Host.ServerAbandonDive(), Is.True, "the host ended the dive");
            yield return PlayModeHarness.WaitUntil(() => !rig.Peer.NetActive.Value && rig.Peer.NetCheckpointHeld.Value, Timeout, "the client never saw the dive end with the checkpoint held");
            yield return PlayModeHarness.WaitUntil(() => !rig.Peer.HasPendingTeleport, Timeout, "the client never finished its teleport home");
            yield return PlayModeHarness.WaitUntil(() => DiveDirector.InteractClaims == 0, Timeout, "the dive result card never let go of Interact (it hides the trapdoor prompt while it is up)");
            yield return TownInputRig.Seconds(1.5f);   // the host sends the party home: let that teleport land before walking to the trapdoor again
            yield return ClientToTrapdoor(rig);
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.Prompt == DiveDirector.PromptKind.Descend && rig.Peer.PickFloor == UndercroftPlan.BankFloor, Timeout, "the return dive's pick did not default to the bank floor (pick " + rig.Peer.PickFloor + ", prompt " + rig.Peer.Prompt + ")");
        }

        [UnityTest]
        public IEnumerator FloorPick_ClientPick_NoCheckpoint_HostRefuses()
        {
            Rig rig = null;
            yield return Start(r => rig = r);
            yield return ClientToTrapdoor(rig);
            int handled = rig.Host.RequestsHandled;
            rig.Peer.RequestStartDive(UndercroftPlan.BankFloor);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the request never reached the host");
            yield return TownInputRig.Seconds(0.3f);
            Assert.That(rig.Host.NetActive.Value, Is.False, "without a checkpoint a client cannot start on the bank floor");
        }
    }
}
