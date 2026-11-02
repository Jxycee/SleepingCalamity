// Authority: test-only (no networked state). Drives the real App through Bootstrap -> Lobby -> solo host -> arena -> leave, and adds a second
// peer in the same process (TestClient) for host + client tests. Ported from FishGame's PlayModeHarness, which had no in-process client.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.PlayMode
{
    // Simulated link conditions for the QA walks (Multiplayer Tools NetworkSimulator, applied to the host's transport; the delay is per direction).
    public enum NetCondition { None, Rtt150Loss2, Rtt300Loss5 }

    public static class PlayModeHarness
    {
        public const float DefaultTimeoutSeconds = 10f;
        private static GameObject _simObject;

        // True while a NetworkSimulator is installed: the wait loops then sleep ~16 ms a frame like a real client, instead of spinning at ~1 ms
        // frames under -nographics (which would burn the CPU and shrink a 300 ms round trip into hundreds of frames).
        public static bool Simulating => _simObject != null;

        // Adds the NetworkSimulator BEFORE the host starts. Destroyed by TearDownApp. Returns the object (null for None).
        public static GameObject AddNetworkSimulator(NetCondition condition)
        {
            if (_simObject != null) { UnityEngine.Object.Destroy(_simObject); _simObject = null; }
            if (condition == NetCondition.None) return null;
            int delayMs = condition == NetCondition.Rtt150Loss2 ? 37 : 75;
            int loss = condition == NetCondition.Rtt150Loss2 ? 2 : 5;
            _simObject = new GameObject("NetSim_" + condition);
            var sim = _simObject.AddComponent<Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulator>();
            sim.ConnectionPreset = Unity.Multiplayer.Tools.NetworkSimulator.Runtime.NetworkSimulatorPreset.Create(
                "walk " + condition, "M7 S0", packetDelayMs: delayMs, packetJitterMs: 0, packetLossInterval: 0, packetLossPercent: loss);
            return _simObject;
        }

        public static void Pace()
        {
            if (_simObject != null) Thread.Sleep(16);
        }

        private const float SessionTimeoutSeconds = 20f;
        private const float TeardownTimeoutSeconds = 20f;
        private const float RecoveryTimeoutSeconds = 10f;
        private const string ResetSceneName = "SC_TestReset";

        // Loads Bootstrap and waits for the Lobby. The world save is switched off so no test reads or writes the profile's real world file.
        public static IEnumerator LoadBootstrapAndWaitForLobby()
        {
            WorldSaveFlow.Enabled = false;
            // The shipped StreamingAssets/Tuning files (solo balance) must not move the number expectations of the regression tests: they run on the code defaults. The dive bot (SC_BOT=1) plays the tuned game.
            if (Environment.GetEnvironmentVariable("SC_BOT") != "1") SleepingCalamity.Core.Tuning.TuningStore.Reset();
            // AppBootstrap's duplicate guard would destroy the NEW App if a stale one survived, so refuse to continue on a leak.
            string leak = null;
            if (AppBootstrap.Current != null) leak = "A previous test leaked the App (AppBootstrap.Current is not null); fix the first failing test's teardown first";
            else if (NetworkManager.Singleton != null) leak = "A previous test leaked a NetworkManager (NetworkManager.Singleton is not null)";
            if (leak != null)
            {
                AppBootstrap stale = AppBootstrap.Current;
                if (stale != null)
                {
                    try { stale.DestroyForTests(); }
                    catch (Exception e) { TestContext.WriteLine("leak recovery: DestroyForTests threw: " + e.GetBaseException().Message); }
                }
                TestContext.WriteLine("LoadBootstrapAndWaitForLobby: " + leak);
                yield return RecoverFromLeakedScenes();
                Assert.Fail(leak);
            }

            AppBootstrap.SuppressDevAutoStart = true;
            SceneFlow.StartSceneOverride = SceneNames.ArenaGreybox;   // M6 S3: a real start loads the town; most tests stay on the small arena (a town test sets it to null)
            SceneManager.LoadScene(SceneNames.Bootstrap, LoadSceneMode.Single);
            yield return WaitUntil(
                () => AppBootstrap.Current != null && SceneManager.GetActiveScene().name == SceneNames.Lobby,
                SessionTimeoutSeconds,
                "Bootstrap did not reach the Lobby scene");
            yield return null;
        }

        // A solo session: host on loopback, the world created at once, the arena loaded, this peer's player spawned.
        public static IEnumerator StartSoloAndWaitForPlayer(string expectedScene = SceneNames.ArenaGreybox)
        {
            if (expectedScene != SceneNames.ArenaGreybox) SceneFlow.StartSceneOverride = expectedScene == SceneNames.Hollowmere ? null : expectedScene;
            AppBootstrap app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "StartSolo: no App; call LoadBootstrapAndWaitForLobby first");
            var local = app.Services.Get<LocalNetworkSession>();
            local.Port = NetworkConstants.TestPort;
            var session = app.Services.Get<INetworkSession>();
            Assert.That(session.State, Is.EqualTo(ConnState.Offline), "Solo start requested while the session is not Offline");

            var cts = new CancellationTokenSource();
            yield return Await(session.StartLocalAsync(false, cts.Token), SessionTimeoutSeconds, cts);
            cts.Dispose();
            yield return WaitUntil(
                () => SceneManager.GetActiveScene().name == expectedScene && app.NetworkManager.LocalClient != null
                      && app.NetworkManager.LocalClient.PlayerObject != null,
                SessionTimeoutSeconds,
                "The local player never spawned in the arena after solo start");
            yield return null;
        }

        // A dev-direct host (loopback, TestPort) with scene management off, so a second NetworkManager in the same process can join without
        // loading scenes of its own. Returns once the host is Connected.
        public static IEnumerator StartDirectHostWithoutScenes()
        {
            AppBootstrap app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "no App; call LoadBootstrapAndWaitForLobby first");
            app.NetworkManager.NetworkConfig.EnableSceneManagement = false;
            var local = app.Services.Get<LocalNetworkSession>();
            local.Port = NetworkConstants.TestPort;
            var session = app.Services.Get<INetworkSession>();
            var cts = new CancellationTokenSource();
            yield return Await(session.StartLocalAsync(true, cts.Token), SessionTimeoutSeconds, cts);
            cts.Dispose();
            Assert.That(session.State, Is.EqualTo(ConnState.Connected));
        }

        // A dev-direct host that KEEPS scene management (so SceneFlow can load the arena). The load-scene timeout is shortened so a second
        // in-process peer that declines scenes (TestClient declineScenes) does not hold the host's load event for the default two minutes.
        public static IEnumerator StartDirectHostWithScenes()
        {
            AppBootstrap app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "no App; call LoadBootstrapAndWaitForLobby first");
            app.NetworkManager.NetworkConfig.LoadSceneTimeOut = 3;
            var local = app.Services.Get<LocalNetworkSession>();
            local.Port = NetworkConstants.TestPort;
            var session = app.Services.Get<INetworkSession>();
            var cts = new CancellationTokenSource();
            yield return Await(session.StartLocalAsync(true, cts.Token), SessionTimeoutSeconds, cts);
            cts.Dispose();
            Assert.That(session.State, Is.EqualTo(ConnState.Connected));
        }

        public static IEnumerator LeaveAndWaitForLobby()
        {
            AppBootstrap app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null, "LeaveAndWaitForLobby: no App");
            var session = app.Services.Get<INetworkSession>();
            NetworkManager nm = app.NetworkManager;

            yield return Await(session.LeaveAsync(), SessionTimeoutSeconds);
            yield return WaitUntil(
                () => session.State == ConnState.Offline && !nm.ShutdownInProgress && !nm.IsListening
                      && SceneManager.GetActiveScene().name == SceneNames.Lobby,
                SessionTimeoutSeconds,
                "Did not return to an idle Lobby after leave");
            yield return null;
        }

        // ---- N clients (M7 S1) ----

        private static readonly List<TestClient> StartedClients = new List<TestClient>();

        // Joins `count` extra TestClients (PlayerId(firstIndex) ... PlayerId(firstIndex + count - 1)) to the running direct host and waits until each is connected.
        // They are added to `into` (if given) and disposed by TearDownApp, so the test needs no teardown of its own for them. A multiplayer world holds 4 players: host + 3 clients is the most.
        public static IEnumerator StartClients(AppBootstrap host, int count, bool declineScenes, List<TestClient> into = null, int firstIndex = 20, float timeoutSeconds = 40f)
        {
            for (int i = 0; i < count; i++)
            {
                var client = new TestClient(host, PlayerId(firstIndex + i), declineScenes);
                StartedClients.Add(client);
                if (into != null) into.Add(client);
                var cts = new CancellationTokenSource();
                yield return Await(client.JoinAsync(cts.Token), timeoutSeconds, cts);
                cts.Dispose();
                yield return WaitUntil(() => client.NetworkManager.IsConnectedClient, timeoutSeconds, "StartClients: client " + i + " never connected");
            }
        }

        // A client that must be refused (full world, closed joins): returns the reason the client saw. Registered for teardown like StartClients.
        public static IEnumerator JoinExpectingRefusal(AppBootstrap host, int index, Action<EndReason> reason, float timeoutSeconds = 40f)
        {
            var client = new TestClient(host, PlayerId(index));
            StartedClients.Add(client);
            var cts = new CancellationTokenSource();
            yield return AwaitRefusal(client.JoinAsync(cts.Token), timeoutSeconds, reason);
            cts.Dispose();
        }

        private static IEnumerator DisposeStartedClients()
        {
            var list = new List<TestClient>(StartedClients);
            StartedClients.Clear();
            foreach (TestClient c in list)
            {
                bool ok = true;
                IEnumerator leave = null;
                try { leave = c.LeaveAndDestroy(); } catch (Exception e) { ok = false; TestContext.WriteLine("client teardown threw: " + e.GetBaseException().Message); }
                if (ok && leave != null)
                {
                    while (true)
                    {
                        bool more;
                        try { more = leave.MoveNext(); } catch (Exception e) { TestContext.WriteLine("client teardown threw: " + e.GetBaseException().Message); try { c.Dispose(); } catch (Exception) { } break; }
                        if (!more) break;
                        yield return leave.Current;
                    }
                }
            }
        }

        // Never asserts before DestroyForTests: a failing teardown must still remove the App, or every later test is poisoned.
        public static IEnumerator TearDownApp()
        {
            yield return DisposeStartedClients();
            try
            {
                string problem = null;
                AppBootstrap app = AppBootstrap.Current;
                if (app != null)
                {
                    INetworkSession session = null;
                    if (app.Services != null && app.Services.TryGet<INetworkSession>(out INetworkSession found)) session = found;

                    if (session != null && session.State != ConnState.Offline)
                    {
                        Task leave = null;
                        try { leave = session.LeaveAsync(); }
                        catch (Exception e) { problem = "LeaveAsync threw: " + e.GetBaseException().Message; }

                        if (leave != null)
                        {
                            double deadline = Time.realtimeSinceStartupAsDouble + TeardownTimeoutSeconds;
                            while (!leave.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                            if (!leave.IsCompleted) problem = $"LeaveAsync did not complete within {TeardownTimeoutSeconds} s";
                            else if (leave.IsFaulted) problem = "LeaveAsync faulted: " + leave.Exception.GetBaseException().Message;
                        }
                    }
                    app.DestroyForTests();
                }

                double until = Time.realtimeSinceStartupAsDouble + TeardownTimeoutSeconds;
                while ((AppBootstrap.Current != null || NetworkManager.Singleton != null || ServiceRoots.App != null)
                       && Time.realtimeSinceStartupAsDouble < until)
                {
                    yield return null;
                }
                yield return null;
                yield return null;

                if (problem == null && AppBootstrap.Current != null) problem = "AppBootstrap.Current is still set after DestroyForTests";
                if (problem == null && NetworkManager.Singleton != null) problem = "NetworkManager.Singleton is still set after DestroyForTests";
                if (problem == null && ServiceRoots.App != null) problem = "ServiceRoots.App is still set after DestroyForTests";
                if (ServiceRoots.App != null)
                {
                    TestContext.WriteLine("TearDownApp: " + problem);
                    yield return RecoverFromLeakedScenes();
                }
                if (problem != null) Assert.Fail("TearDownApp: " + problem);
            }
            finally
            {
                if (_simObject != null) { UnityEngine.Object.Destroy(_simObject); _simObject = null; }
                AppBootstrap.SuppressDevAutoStart = false;
                SceneFlow.StartSceneOverride = null;
                WorldSaveFlow.Enabled = true;
            }
        }

        // Puts a fresh empty scene in place, unloads every other loaded scene, then waits (bounded) for the service roots to clear.
        // Used only after a leak was detected; the caller still fails the test with the original message.
        private static IEnumerator RecoverFromLeakedScenes()
        {
            Scene reset = SceneManager.CreateScene(ResetSceneName);
            SceneManager.SetActiveScene(reset);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != reset && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
            double deadline = Time.realtimeSinceStartupAsDouble + RecoveryTimeoutSeconds;
            while ((AppBootstrap.Current != null || ServiceRoots.App != null) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            ServiceRoots.SetApp(null);
            yield return null;
        }

        // ---- waiting ----

        public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string failMessage)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("Timed out after " + timeoutSeconds + " s: " + failMessage);
                Pace();
                yield return null;
            }
        }

        // Waits for a Task; a fault fails the test with the exception. The token source (optional) is cancelled on timeout.
        public static IEnumerator Await(Task task, float timeoutSeconds, CancellationTokenSource cts = null)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline)
                {
                    cts?.Cancel();
                    Assert.Fail("Timed out after " + timeoutSeconds + " s waiting for a network task");
                }
                Pace();
                yield return null;
            }
            if (task.IsFaulted) Assert.Fail("The task faulted: " + task.Exception.GetBaseException());
        }

        // Waits for a Task that is expected to fault with a NetworkSessionException; returns the reason through the holder.
        public static IEnumerator AwaitRefusal(Task task, float timeoutSeconds, Action<EndReason> reason)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("Timed out after " + timeoutSeconds + " s waiting for the refusal");
                yield return null;
            }
            Assert.That(task.IsFaulted, Is.True, "the join was expected to be refused");
            var ex = task.Exception.GetBaseException() as NetworkSessionException;
            Assert.That(ex, Is.Not.Null, "expected a NetworkSessionException, got " + task.Exception.GetBaseException());
            reason(ex.Reason);
        }

        // ---- the second peer ----

        // A second NetworkManager (its own UnityTransport and LocalNetworkSession) in the same process, configured like the App's so NGO's
        // config hash matches. It is a client only; scene management is off on both peers in these tests.
        public sealed class TestClient : IDisposable
        {
            private readonly GameObject _go;
            private readonly string _settingsPath;

            public NetworkManager NetworkManager { get; }
            public LocalNetworkSession Session { get; }
            public PlayerKey Key { get; }

            // declineScenes: the client connects and synchronizes but never loads a scene (one process cannot load the same scene twice), so a
            // scene-managed host is not confused by a second copy of its own scene. The host's load event then completes by its timeout for
            // this client (hostConfig.LoadSceneTimeOut), which a test shortens.
            public TestClient(AppBootstrap host, string localPlayerId, bool declineScenes = false)
            {
                _go = new GameObject("TestClient_" + localPlayerId.Substring(0, 6));
                _go.SetActive(false);   // configure before NGO's OnEnable
                NetworkManager = _go.AddComponent<NetworkManager>();
                var transport = _go.AddComponent<UnityTransport>();
                NetworkConfig hostConfig = host.NetworkManager.NetworkConfig;
                var config = new NetworkConfig
                {
                    NetworkTransport = transport,
                    TickRate = hostConfig.TickRate,
                    ConnectionApproval = hostConfig.ConnectionApproval,
                    EnableSceneManagement = hostConfig.EnableSceneManagement,
                    PlayerPrefab = null,
                    ProtocolVersion = hostConfig.ProtocolVersion,
                    ForceSamePrefabs = hostConfig.ForceSamePrefabs,
                    LoadSceneTimeOut = hostConfig.LoadSceneTimeOut,
                    Prefabs = new NetworkPrefabs(),
                };
                foreach (NetworkPrefabsList list in hostConfig.Prefabs.NetworkPrefabsLists) config.Prefabs.NetworkPrefabsLists.Add(list);
                NetworkManager.NetworkConfig = config;
                _go.SetActive(true);
                if (declineScenes)
                {
                    NetworkManager nm = NetworkManager;
                    nm.OnClientStarted += () => nm.SceneManager.VerifySceneBeforeLoading = (index, name, mode) => false;
                }

                _settingsPath = Path.Combine(Path.GetTempPath(), "sc-testclient-" + Guid.NewGuid().ToString("N") + ".json");
                var settings = new SettingsStore(_settingsPath);
                settings.Current.localPlayerId = localPlayerId;
                int protocol = BuildInfo.EffectiveProtocolVersion(host.DevFlags.ProtocolOffset);
                var sessionConfig = new NetworkSessionConfig(NetworkManager, new JoinApprovalBridge(), settings, protocol, host.ContentHashValue, false);
                Session = new LocalNetworkSession(sessionConfig) { Port = NetworkConstants.TestPort };
                Key = PlayerKey.Local(localPlayerId);
            }

            public Task JoinAsync(CancellationToken ct) => Session.JoinDirectAsync(NetworkConstants.LoopbackAddress, NetworkConstants.TestPort, ct);

            public IEnumerator LeaveAndDestroy()
            {
                if (Session.State != ConnState.Offline) yield return Await(Session.LeaveAsync(), TeardownTimeoutSeconds);
                Dispose();
                yield return null;
            }

            public void Dispose()
            {
                Session.Dispose();
                if (_go != null) UnityEngine.Object.Destroy(_go);
                try { if (File.Exists(_settingsPath)) File.Delete(_settingsPath); } catch (IOException) { }
            }
        }

        // Runs a session with the world save ON against a throwaway library folder, so a test can check the files a real session writes.
        // Returns the folder; pass it to EndSaveTest when done.
        public static string BeginSaveTest()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sc-playmode-worlds-" + Guid.NewGuid().ToString("N"));
            WorldLibrary.DirectoryOverrideForTests = dir;
            WorldSaveFlow.Enabled = true;
            return dir;
        }

        public static void EndSaveTest(string dir)
        {
            WorldLibrary.DirectoryOverrideForTests = null;
            WorldSaveFlow.Enabled = false;
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        // A lowercase GUID "N" id for a test player; a fixed index gives a fixed id so a test can seed the world with it.
        public static string PlayerId(int index) => index.ToString("x2") + new string('0', 30);
    }
}
