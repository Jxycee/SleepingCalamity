// Not networked. Composition root on the root "App" object in Bootstrap; runs on every peer.
// Server only: the host spawns each client's player object in the arena (ArenaPlayerSpawner, attached from NetworkManager.OnServerStarted).
// Uses GetComponent<NetworkManager>() only, never the NGO static accessor: this Awake runs at order -2000, before NetworkManager's own Awake/OnEnable.
using System;
using System.Collections;
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.App
{
    [DefaultExecutionOrder(-2000)]
    public sealed class AppBootstrap : MonoBehaviour
    {
        private const ulong SfxSeed = 1UL;        // fixed so generated SFX are reproducible
        private const int ContentDataVersion = 1; // bumped by hand when networked content changes in a way the scene list does not show

        // Runtime check (true in the editor and in Development Builds); replaces the deprecated DEVELOPMENT_BUILD define.
        private static bool IsDevBuild => Debug.isDebugBuild;

        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private InputActionAsset inputActions;

        private ServiceRegistry services;
        private INetworkSession session;
        private SceneFlow sceneFlow;
        private WorldSaveFlow worldSaveFlow;
        private ArenaPlayerSpawner spawner;
        private bool composed;

        public static AppBootstrap Current { get; private set; }
        public static bool SuppressDevAutoStart { get; set; }
        // Test seam: null in production; read lazily when the first online attempt creates the backend.
        public static Func<INetworkSession> OnlineSessionFactoryOverride { get; set; }
        public ServiceRegistry Services => services;
        public NetworkManager NetworkManager { get; private set; }
        public ulong ContentHashValue { get; private set; }
        public DevFlags DevFlags { get; private set; }
        public SceneFlow SceneFlow => sceneFlow;
        public WorldSaveFlow WorldSaveFlow => worldSaveFlow;
        public FrontDoorModel FrontDoor { get; private set; }
        public PauseMenuModel PauseMenu { get; private set; }   // PL-4 4.1: local on every peer; its view sits on the App's PauseMenu child object
        public HostRoster Roster { get; private set; }
        public HostWorld World { get; private set; }
        public NetworkObject PlayerPrefab => playerPrefab;

        public void Configure(NetworkObject playerPrefab, InputActionAsset inputActions)
        {
            this.playerPrefab = playerPrefab;
            this.inputActions = inputActions;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            OnlineSessionFactoryOverride = null;
            SuppressDevAutoStart = false;
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                // A second Bootstrap load (not part of the flow, but a stray load must not make a second App). Deactivate first so this copy's NetworkManager, input and UI never wake up.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            try
            {
                Compose();
                composed = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError("AppBootstrap: composition failed; the app cannot start. Run SleepingCalamity > Build All and fix the error above.", this);
                enabled = false; // Start will not run
            }
        }

        private void Compose()
        {
            if (playerPrefab == null || inputActions == null)
            {
                throw new InvalidOperationException("AppBootstrap is not configured (playerPrefab or inputActions is missing). SceneBuilder must call Configure(...) before saving Bootstrap.");
            }
            if (transform.parent != null)
            {
                throw new InvalidOperationException("AppBootstrap: the App object must be a root object (DontDestroyOnLoad and NGO both require it).");
            }
            var networkManager = Require<NetworkManager>();
            var input = Require<PlayerInputAdapter>();
            var audio = Require<AudioBus>();

            DontDestroyOnLoad(gameObject);
            Current = this;
            Application.runInBackground = true; // MPPM clones and alt-tabbed hosts must keep ticking

            NetworkManager = networkManager;
            var config = NetworkManager.NetworkConfig;
            if (config.NetworkTransport == null) config.NetworkTransport = GetComponent<NetworkTransport>();
            if (config.NetworkTransport == null) throw new InvalidOperationException("AppBootstrap: no NetworkTransport (UnityTransport) on the App object.");
            config.TickRate = NetworkConstants.TickRate;
            config.ConnectionApproval = true;
            config.EnableSceneManagement = true;
            // NGO's own protocol version stays at its constant (0). The dev protocol offset is applied only to the
            // payload protocol version passed to NetworkSessionConfig below.
            config.ProtocolVersion = NetworkConstants.NgoProtocolVersion;
            config.PlayerPrefab = null;

            // Subscribe before any StartHost can happen (LocalNetworkSession, or the host the UGS SDK starts).
            NetworkManager.OnServerStarted += HandleServerStarted;
            NetworkManager.OnServerStopped += HandleServerStopped;

            DevFlags = DevFlags.Parse(DevStartup.CollectArgs());

            services = new ServiceRegistry();
            var bus = new EventBus();
            services.Register(bus);

            var settings = SettingsStore.CreateForThisInstance();
            settings.EnsureLocalPlayerId();
            services.Register<ISettings>(settings);

            input.Initialize(inputActions, settings);
            services.Register<IInputService>(input);
            services.Register<IInputBindings>(input);

            var sfx = new SfxGenerator();
            sfx.GenerateAll(SfxSeed);
            sfx.ApplyFileClips();   // recorded CC0 clips (docs/audio/SOURCES.md) replace the generated cues that have a file
            services.Register(sfx);
            audio.Initialize(sfx, settings);
            services.Register<AudioBus>(audio); // concrete type

            // The content handshake: host and clients must run the same scene list and data version.
            ContentHashValue = ContentHash.Compute(
                new List<ContentCategoryIds> { new ContentCategoryIds("scenes", SceneNames.BuildOrder) }, ContentDataVersion);

            var bridge = new JoinApprovalBridge();
            services.Register(bridge);
            services.Register(this.DevFlags);
            Roster = new HostRoster();
            World = new HostWorld();
            services.Register(Roster);
            services.Register(World);

            int protocolVersion = BuildInfo.EffectiveProtocolVersion(IsDevBuild ? this.DevFlags.ProtocolOffset : 0);
            bool longTimeout = IsDevBuild && this.DevFlags.LongTimeout;
            var sessionConfig = new NetworkSessionConfig(NetworkManager, bridge, settings, protocolVersion, ContentHashValue, longTimeout, Roster, World);
            var local = new LocalNetworkSession(sessionConfig);
            services.Register(local);
            session = new CompositeNetworkSession(local,
                () => OnlineSessionFactoryOverride != null ? OnlineSessionFactoryOverride() : new UgsNetworkSession(sessionConfig));
            services.Register<INetworkSession>(session);

            ServiceRoots.SetApp(services);
            Application.quitting += HandleQuitting;
            // Order matters: SceneFlow resets the world for the new session before WorldSaveFlow loads the saved one over it.
            sceneFlow = new SceneFlow(session, NetworkManager, bus, Roster, World);
            worldSaveFlow = new WorldSaveFlow(session, NetworkManager, bus, World);
            spawner = new ArenaPlayerSpawner(NetworkManager, playerPrefab, Roster);

            // The front door (PL-3): local on every peer, it only asks. Built after SceneFlow, the roster and the world library exist, and subscribed to
            // the session after theirs, so by the time it hears Ended the flow has already saved and gone back to the Lobby.
            FrontDoor = new FrontDoorModel(session, worldSaveFlow, new FrontDoorHost(sceneFlow, Roster));
            FrontDoorView view = GetComponent<FrontDoorView>();
            if (view != null) view.Bind(FrontDoor, session, input);
            PauseMenu = new PauseMenuModel(session, input, PauseMenuView.QuitGame);
            PauseMenuView pauseView = GetComponentInChildren<PauseMenuView>(true);
            if (pauseView != null) pauseView.Bind(PauseMenu, input);
        }

        private void Update()
        {
            worldSaveFlow?.Tick();
        }

        // Hands over to the Lobby scene, then lets a dev auto-start (command line, MPPM tag or the editor's Auto Solo) begin a session.
        private IEnumerator Start()
        {
            if (!composed) yield break;
            SceneManager.LoadScene(SceneNames.Lobby, LoadSceneMode.Single);
            // Wait until the plain Lobby load has finished, then one more frame, so a dev auto-start can never race it with an NGO scene load.
            while (SceneManager.GetActiveScene().name != SceneNames.Lobby) yield return null;
            yield return null;
            if (!SuppressDevAutoStart) DevStartup.TryAutoStart(session, this.DevFlags);
        }

        // Raised synchronously inside StartHost after host self-approval; the scene manager exists. Keep it exception-free.
        private void HandleServerStarted()
        {
            spawner?.Attach();
        }

        private void HandleServerStopped(bool wasHost)
        {
            spawner?.Detach();
        }

        private void HandleQuitting()
        {
            try
            {
                worldSaveFlow?.SaveNow(); // the host's world on quit
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (session == null) return;
            try
            {
                _ = session.LeaveAsync(); // best effort
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private T Require<T>() where T : Component
        {
            var component = GetComponent<T>();
            if (component == null) throw new InvalidOperationException("AppBootstrap: the App object has no " + typeof(T).Name + " component.");
            return component;
        }

        private void OnDestroy()
        {
            if (Current != this) return;

            Application.quitting -= HandleQuitting;
            if (NetworkManager != null)
            {
                NetworkManager.OnServerStarted -= HandleServerStarted;
                NetworkManager.OnServerStopped -= HandleServerStopped;
            }

            spawner?.Detach();
            spawner = null;
            FrontDoorView view = GetComponent<FrontDoorView>();
            if (view != null) view.Bind(null, null);
            FrontDoor?.Dispose();
            FrontDoor = null;
            PauseMenuView pauseViewOnExit = GetComponentInChildren<PauseMenuView>(true);
            if (pauseViewOnExit != null) pauseViewOnExit.Bind(null, null);
            PauseMenu?.Dispose();
            PauseMenu = null;
            sceneFlow?.Dispose();
            sceneFlow = null;
            worldSaveFlow?.Dispose();
            worldSaveFlow = null;
            if (services != null)
            {
                if (ServiceRoots.App == services) ServiceRoots.SetApp(null);
                services.Dispose();
                services = null;
            }
            session = null;
            Current = null;
        }

        public void DestroyForTests()
        {
            if (NetworkManager != null && !NetworkManager.ShutdownInProgress &&
                (NetworkManager.IsListening || NetworkManager.IsServer || NetworkManager.IsClient))
            {
                NetworkManager.Shutdown();
            }
            Destroy(gameObject);
        }
    }
}
