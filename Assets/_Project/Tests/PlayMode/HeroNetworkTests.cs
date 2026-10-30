// Authority: test-only. The M2 hero on the real stack.
//  1. Two peers in one process (the App as host, a TestClient as the second peer, scene management off on both, the arena loaded additively by the
//     test): each owner moves its own hero, the movement replicates to the other peer, and each owner hard-locks its own target.
//  2. A solo session through the real flow: the host spawns the arena's target dummies with the player, the owner rig and camera rig come up, and
//     lock, flick-switch and release work from the routed intent.
// Catches: owner movement not replicating (NetworkTransform owner authority), one player's input moving the other, the lock state leaking between
// players, the owner-only components running on a remote copy, a camera rig for a remote hero, dummies not reaching the arena or losing the
// elite flag, and the LockOn input context not following the lock.
// NOT covered by a one-process test (the manual 2-player walk on the PC): latency and loss, two machines' cameras, a real second Cinemachine Brain.
// One-process caveats handled here: every hero exists twice (an owner copy and a remote copy on the other peer) at about the same place, so
// Player / RemotePlayer layer collisions are switched off for the test; each owner gets its own FakeInputService so the two never share a stick.
using System.Collections;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HeroNetworkTests
    {
        private const float Timeout = 20f;

        private PlayModeHarness.TestClient _client;
        private bool _layersChanged;
        private bool[] _savedIgnore;

        private static readonly int[][] LayerPairs =
        {
            new[] { Layers.Player, Layers.RemotePlayer },
            new[] { Layers.Player, Layers.Player },
            new[] { Layers.RemotePlayer, Layers.RemotePlayer },
        };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            Scene arena = SceneManager.GetSceneByName(SceneNames.ArenaGreybox);
            if (arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }

        private void IgnoreHeroCollisions()
        {
            _savedIgnore = new bool[LayerPairs.Length];
            for (int i = 0; i < LayerPairs.Length; i++)
            {
                _savedIgnore[i] = Physics.GetIgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1]);
                Physics.IgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1], true);
            }
            _layersChanged = true;
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

        private sealed class HeroRig
        {
            public NetworkObject Object;
            public HeroNetState State;
            public HeroLocomotion Locomotion;
            public TargetLockController Lock;
            public FakeInputService Input;

            public HeroRig(NetworkObject obj)
            {
                Object = obj;
                State = obj.GetComponent<HeroNetState>();
                Locomotion = obj.GetComponent<HeroLocomotion>();
                Lock = obj.GetComponent<TargetLockController>();
                Input = new FakeInputService();
            }

            public void TakeOverInput()
            {
                Locomotion.Input = Input;
                Lock.Input = Input;
            }

            public Vector3 Position => Object.transform.position;
        }

        private static IEnumerator LoadArenaAdditively()
        {
            // The Lobby's camera and listener would sit beside the arena's.
            foreach (Camera c in Object.FindObjectsByType<Camera>()) if (c.name == "LobbyCamera") c.enabled = false;
            foreach (AudioListener l in Object.FindObjectsByType<AudioListener>()) if (l.name == "LobbyCamera") l.enabled = false;
            yield return SceneManager.LoadSceneAsync(SceneNames.ArenaGreybox, UnityEngine.SceneManagement.LoadSceneMode.Additive);
        }

        // ---- two peers ----

        [UnityTest]
        public IEnumerator TwoPlayers_MoveIndependently_AndEachLocksItsOwnTarget()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;

            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(7));
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            NetworkManager clientNm = _client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => hostNm.ConnectedClientsIds.Count == 2 && clientNm.IsConnectedClient, Timeout, "the client never connected");

            yield return LoadArenaAdditively();
            IgnoreHeroCollisions();

            // The host spawns the dummies and both heroes (the host's at the origin, the client's well away from it, so their nearest targets differ).
            var spawner = Object.FindAnyObjectByType<TargetDummySpawner>();
            Assert.That(spawner, Is.Not.Null, "the arena has the dummy spawner");
            Assert.That(spawner.SpawnAll(hostNm), Is.EqualTo(7), "seven dummies");
            Assert.That(spawner.SpawnAll(hostNm), Is.EqualTo(0), "spawning is idempotent");

            ulong clientId = clientNm.LocalClientId;
            NetworkObject hostOwn = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, hostNm.LocalClientId, false, true, false, Vector3.zero, Quaternion.identity);
            NetworkObject clientProxyOnHost = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, clientId, false, true, false, new Vector3(9f, 0f, 2f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(
                () => clientNm.LocalClient != null && clientNm.LocalClient.PlayerObject != null
                      && clientNm.SpawnManager.SpawnedObjects.ContainsKey(hostOwn.NetworkObjectId),
                Timeout, "the client never received the players");
            NetworkObject clientOwn = clientNm.LocalClient.PlayerObject;
            NetworkObject hostProxyOnClient = clientNm.SpawnManager.SpawnedObjects[hostOwn.NetworkObjectId];

            // The client's copies of the dummies carry the host's elite flag from the spawn on (written before Spawn).
            yield return PlayModeHarness.WaitUntil(() => CountDummies(clientNm, out _) == 7, Timeout, "the client never received the seven dummies");
            Assert.That(CountDummies(clientNm, out Vector3 eliteAt), Is.EqualTo(7));
            AssertNear(eliteAt, new Vector3(0f, 0f, 12f), "the client's copy of the elite (exactly one dummy there is elite)");

            // Owner / remote split: only the owner's copy runs the movement and the lock.
            var host = new HeroRig(hostOwn);
            var client = new HeroRig(clientOwn);
            Assert.That(host.Object.IsOwner && client.Object.IsOwner, Is.True);
            Assert.That(host.State.OwnerRigActive && client.State.OwnerRigActive, Is.True, "owner copies run the hero");
            Assert.That(hostProxyOnClient.GetComponent<HeroNetState>().OwnerRigActive, Is.False, "a remote copy does not run the hero");
            Assert.That(clientProxyOnHost.GetComponent<HeroNetState>().OwnerRigActive, Is.False);
            Assert.That(hostProxyOnClient.GetComponent<CharacterController>().enabled, Is.False);
            Assert.That(hostProxyOnClient.gameObject.layer, Is.EqualTo(Layers.RemotePlayer));
            Assert.That(hostOwn.gameObject.layer, Is.EqualTo(Layers.Player));

            // One camera rig for the process, on the first owner; none on a remote copy.
            Assert.That(HeroCameraRig.Local, Is.Not.Null, "the first owner built the camera rig");
            Assert.That(hostOwn.GetComponent<HeroCameraRigSpawner>().Rig, Is.SameAs(HeroCameraRig.Local));
            Assert.That(clientOwn.GetComponent<HeroCameraRigSpawner>().Rig, Is.Null, "a second owner in one process builds no second rig");
            Assert.That(hostProxyOnClient.GetComponent<HeroCameraRigSpawner>().Rig, Is.Null);

            host.TakeOverInput();
            client.TakeOverInput();
            yield return Seconds(0.5f);   // settle on the ground
            Vector3 hostStart = host.Position, clientStart = client.Position;
            Assert.That(hostStart.y, Is.LessThan(0.2f), "on the floor");

            // ---- each locks its own target, in the same frame ----
            host.Input.Frame.LockPressed = true;
            client.Input.Frame.LockPressed = true;
            yield return null;
            host.Input.Frame.LockPressed = false;
            client.Input.Frame.LockPressed = false;
            yield return Frames(3);

            Assert.That(host.Lock.IsLocked && client.Lock.IsLocked, Is.True, "both locked");
            AssertNear(host.Lock.Locked.transform.position, new Vector3(0f, 0f, 4f), "the host locks the dummy straight ahead");
            AssertNear(client.Lock.Locked.transform.position, new Vector3(5f, 0f, 8f), "the client locks a different dummy");
            Assert.That(host.Input.Context, Is.EqualTo(InputContext.LockOn));
            Assert.That(client.Input.Context, Is.EqualTo(InputContext.LockOn));
            Assert.That(HeroCameraRig.Local.LockCamLive, Is.True, "the owner rig switched to the lock camera");

            // The lock stance reaches the other peer's copy of each hero (for its animation), and nothing else of the lock does.
            yield return Seconds(0.4f);
            Assert.That(hostProxyOnClient.GetComponent<HeroNetState>().StanceIsLocked, Is.True, "the host's stance on the client's copy");
            Assert.That(clientProxyOnHost.GetComponent<HeroNetState>().StanceIsLocked, Is.True, "the client's stance on the host's copy");

            // ---- the host lets go; the client keeps its lock ----
            host.Input.Frame.LockPressed = true;
            yield return null;
            host.Input.Frame.LockPressed = false;
            yield return Frames(3);
            Assert.That(host.Lock.IsLocked, Is.False);
            Assert.That(host.Input.Context, Is.EqualTo(InputContext.Free));
            Assert.That(client.Lock.IsLocked, Is.True, "the host's release did not touch the client's lock");
            Assert.That(client.Input.Context, Is.EqualTo(InputContext.LockOn));
            Assert.That(HeroCameraRig.Local.LockCamLive, Is.False, "the host's rig went back to the free camera");
            yield return Seconds(0.4f);
            Assert.That(hostProxyOnClient.GetComponent<HeroNetState>().StanceIsLocked, Is.False, "the release reached the client's copy");
            Assert.That(clientProxyOnHost.GetComponent<HeroNetState>().StanceIsLocked, Is.True, "the client is still locked");

            // A dodge shows on the other peer's copy.
            host.Input.Frame.MoveX = 1f;
            host.Input.Frame.DodgePressed = true;
            yield return null;
            host.Input.Frame.DodgePressed = false;
            string why = "grounded=" + host.Locomotion.IsGrounded + " context=" + host.Input.Context + " pos=" + host.Position;
            yield return PlayModeHarness.WaitUntil(() => host.Locomotion.IsDodging, 1f, "the host never started dodging (" + why + ")");
            yield return PlayModeHarness.WaitUntil(() => hostProxyOnClient.GetComponent<HeroNetState>().StanceIsDodging || !host.Locomotion.IsDodging, 1f, "the dodge flag never arrived");
            Assert.That(hostProxyOnClient.GetComponent<HeroNetState>().StanceIsDodging, Is.True, "the dodge reached the client's copy before it ended");
            yield return Seconds(0.7f);
            Assert.That(host.Locomotion.IsDodging, Is.False, "a dodge lasts half a second");

            // ---- independent movement: the host strolls right (free), the client walks toward its locked target ----
            host.Input.Frame.MoveX = 1f;
            client.Input.Frame.MoveY = 1f;
            yield return Seconds(0.8f);
            host.Input.Frame = default;
            client.Input.Frame = default;
            yield return Seconds(1.0f);   // let the last poses replicate

            Vector3 hostMoved = host.Position - hostStart, clientMoved = client.Position - clientStart;
            Assert.That(hostMoved.x, Is.GreaterThan(2.5f), "the host moved right");
            Assert.That(Mathf.Abs(hostMoved.z), Is.LessThan(0.5f), "the client's input did not move the host");
            Assert.That(clientMoved.z, Is.GreaterThan(1.5f), "the client moved toward its target");
            Assert.That(clientMoved.x, Is.LessThan(-0.8f), "the client's way is toward the target's side");
            Assert.That(Mathf.DeltaAngle(host.Object.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(15f), "free: the host faces its way");
            float toTarget = MovementMath.YawDeg(5f - client.Position.x, 8f - client.Position.z);
            Assert.That(Mathf.DeltaAngle(client.Object.transform.eulerAngles.y, toTarget), Is.EqualTo(0f).Within(15f), "locked: the client faces its target");

            // Owner movement replicated to the other peer.
            Assert.That(Vector3.Distance(clientProxyOnHost.transform.position, client.Position), Is.LessThan(0.6f), "the client's pose reached the host");
            Assert.That(Vector3.Distance(hostProxyOnClient.transform.position, host.Position), Is.LessThan(0.6f), "the host's pose reached the client");
            Assert.That(clientProxyOnHost.transform.position.x, Is.LessThan(clientStart.x - 0.8f), "and it is the moved pose, not the spawn");
        }

        // Dummies spawned on this peer's NetworkManager; eliteAt is where the elite one stands (asserts there is exactly one when there are any).
        private static int CountDummies(NetworkManager nm, out Vector3 eliteAt)
        {
            int dummies = 0, elites = 0;
            eliteAt = Vector3.zero;
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values)
            {
                if (o == null || o.GetComponent<TargetDummy>() == null) continue;
                dummies++;
                if (o.GetComponent<Targetable>().IsElite)
                {
                    elites++;
                    eliteAt = o.transform.position;
                }
            }
            if (dummies == 7) Assert.That(elites, Is.EqualTo(1), "exactly one elite among the dummies this peer holds");
            return dummies;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.1f), message + " (was " + actual + ", expected " + expected + ")");
        }

        // ---- solo, through the real flow ----

        [UnityTest]
        public IEnumerator Solo_SpawnsDummiesAndRig_AndLockFlickAndReleaseWork()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            AppBootstrap app = AppBootstrap.Current;

            yield return PlayModeHarness.WaitUntil(() => Targetable.All.Count == 7, Timeout, "the host never spawned the seven dummies");
            int elites = 0;
            foreach (Targetable t in Targetable.All) if (t.IsElite) elites++;
            Assert.That(elites, Is.EqualTo(1), "exactly one elite");

            var hero = new HeroRig(app.NetworkManager.LocalClient.PlayerObject);
            Assert.That(hero.State.OwnerRigActive, Is.True);
            Assert.That(HeroCameraRig.Local, Is.Not.Null);
            Assert.That(HeroCameraRig.Local.FreeCam.Priority.Value, Is.GreaterThan(HeroCameraRig.Local.LockCam.Priority.Value));
            hero.TakeOverInput();
            yield return Seconds(0.5f);

            // Soft target: the dummy straight ahead, with no input at all.
            AssertNear(hero.Lock.Soft.transform.position, new Vector3(0f, 0f, 4f), "soft target");
            Assert.That(hero.Lock.IsLocked, Is.False);

            hero.Input.Frame.LockPressed = true;
            yield return null;
            hero.Input.Frame.LockPressed = false;
            yield return Frames(2);
            Assert.That(hero.Lock.IsLocked, Is.True);
            AssertNear(hero.Lock.Locked.transform.position, new Vector3(0f, 0f, 4f), "locked");

            // Flick right: the next dummy to the right of it (and in reach).
            yield return Frames(2);   // stick at rest arms the flick
            hero.Input.Frame.SwitchX = 1f;
            yield return null;
            hero.Input.Frame.SwitchX = 0f;
            yield return Frames(2);
            AssertNear(hero.Lock.Locked.transform.position, new Vector3(5f, 0f, 8f), "flicked right");

            // The look input is ignored while locked (router row), and free again after release.
            hero.Input.Frame.LockPressed = true;
            yield return null;
            hero.Input.Frame.LockPressed = false;
            yield return Frames(2);
            Assert.That(hero.Lock.IsLocked, Is.False);
            Assert.That(hero.Input.Context, Is.EqualTo(InputContext.Free));
        }

        private static IEnumerator SoloLockNearDummy(System.Action<HeroRig> hero)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.WaitUntil(() => Targetable.All.Count == 7 && HeroCameraRig.Local != null, Timeout, "arena not ready");
            var rig = new HeroRig(app.NetworkManager.LocalClient.PlayerObject);
            rig.TakeOverInput();
            yield return Seconds(0.5f);
            rig.Input.Frame.LockPressed = true;
            yield return null;
            rig.Input.Frame.LockPressed = false;
            yield return Frames(3);
            Assert.That(rig.Lock.IsLocked, Is.True, "locked");
            Assert.That(HeroCameraRig.Local.LockCamLive, Is.True, "lock camera live");
            Assert.That(HeroCameraRig.Local.LockIndicator.gameObject.activeSelf, Is.True, "indicator shown");
            hero(rig);
        }

        // Catches: a destroyed locked target being a Unity fake null that swallows LockChanged(null) (the camera stayed in lock mode with a frozen marker).
        [UnityTest]
        public IEnumerator Solo_LockedDummyDespawns_ReleasesTheLockTheCameraAndTheMarker()
        {
            HeroRig hero = null;
            yield return SoloLockNearDummy(h => hero = h);
            var spawner = Object.FindAnyObjectByType<TargetDummySpawner>();
            NetworkObject locked = null;
            foreach (NetworkObject o in spawner.Spawned) if (o != null && Vector3.Distance(o.transform.position, new Vector3(0f, 0f, 4f)) < 0.1f) locked = o;
            Assert.That(locked, Is.Not.Null);
            locked.Despawn(true);
            yield return Frames(3);

            Assert.That(hero.Lock.IsLocked, Is.False, "the lock ended");
            Assert.That(hero.Lock.LastDrop, Is.EqualTo(LockDrop.Missing));
            Assert.That(HeroCameraRig.Local.LockCamLive, Is.False, "the camera left lock mode");
            Assert.That(HeroCameraRig.Local.LockIndicator.gameObject.activeSelf, Is.False, "the marker is hidden");
            Assert.That(hero.Input.Context, Is.EqualTo(InputContext.Free));
        }

        // Catches: a hero that despawns while locked leaving its camera rig behind, the static rig pointer set, or the input stuck in LockOn.
        [UnityTest]
        public IEnumerator Solo_HeroDespawnsWhileLocked_CleansUpTheRigAndTheContext()
        {
            HeroRig hero = null;
            yield return SoloLockNearDummy(h => hero = h);
            hero.Object.Despawn(true);
            yield return Frames(3);

            Assert.That(HeroCameraRig.Local, Is.Null, "the rig was destroyed with its hero");
            Assert.That(hero.Input.Context, Is.EqualTo(InputContext.Free), "the input context went back to Free");
            Assert.That(Object.FindObjectsByType<HeroCameraRig>().Length, Is.EqualTo(0), "no rig left in the scene");
        }
    }
}
