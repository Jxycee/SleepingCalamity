// Not networked itself. The host decides when the gameplay scene loads (NetworkSceneManager); clients follow NGO scene sync.
// The flow: Bootstrap composes the App and hands over to the Lobby scene. A session (solo, dev-direct or online) connects while the Lobby is up. THE START GATE LIVES HERE
// and in HostWorld: TryStartSession asks HostWorld.TryStartSession(HostRoster) (Core WorldPlayers rules) and only on Ok loads the greybox
// arena for everyone. The 2-player rule is a START rule only: once the session runs, players leaving it (down to one) never end it. A Solo session starts by itself when it connects; a multiplayer host starts when its user presses Start, and the gate
// refuses (and says so) until 2 or more recorded players are connected. When the session goes Offline the flow returns to the Lobby.
// Every peer reacts to INetworkSession events. Error toasts come only from Ended, never from start-task exceptions.
using System;
using System.Threading.Tasks;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.App
{
    public sealed class SceneFlow : IDisposable
    {
        private readonly INetworkSession session;
        private readonly NetworkManager networkManager;
        private readonly EventBus bus;
        private readonly HostRoster roster;
        private readonly HostWorld world;
        private bool disposed;

        public SceneFlow(INetworkSession session, NetworkManager networkManager, EventBus bus, HostRoster roster, HostWorld world)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            session.StateChanged += HandleStateChanged;
            session.Ended += HandleEnded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            networkManager.OnServerStarted += InstallSceneFilter;
            networkManager.OnClientStarted += InstallSceneFilter;
        }

        // T24b: Hollowmere's art scenes are loaded locally by HollowmereSceneLoader on every peer. NGO's late-join synchronisation lists every additively loaded scene it passes
        // VerifySceneBeforeLoading, so without this a joiner would load each art scene a second time (once from the sync, once from its own loader). False refuses them on both sides.
        public static bool AllowsNgoSceneLoad(string sceneName, LoadSceneMode mode) => !(mode == LoadSceneMode.Additive && SceneNames.IsHollowmereArt(sceneName));

        private NetworkSceneManager filteredSceneManager;

        private void InstallSceneFilter()
        {
            NetworkSceneManager sm = networkManager.SceneManager;
            if (disposed || sm == null || ReferenceEquals(sm, filteredSceneManager)) return;
            filteredSceneManager = sm;
            NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler previous = sm.VerifySceneBeforeLoading;
            // NGO warns "failed validation on the Host" for every refused scene on every late join (five art scenes: five warnings). Our refusal is the design, so NGO's warning is switched off
            // (public API, no internals) and any refusal that is not an art scene (a previous handler's) is logged here instead.
            sm.DisableValidationWarnings(true);
            sm.VerifySceneBeforeLoading = (index, name, mode) =>
            {
                if (!AllowsNgoSceneLoad(name, mode)) return false;
                if (previous == null || previous(index, name, mode)) return true;
                Debug.LogWarning("Scene " + name + " (build index " + index + ") being loaded in " + mode + " mode failed validation!");
                return false;
            };
        }

        // The scene the first start loads (M6 S3: the town). Precedence: -dev-dive, -dev-combat, the test override, Hollowmere.
        public string FirstGameplayScene => DevDiveArena ? SceneNames.ArenaDive : DevCombatArena ? SceneNames.ArenaCombat : (StartSceneOverride ?? SceneNames.Hollowmere);

        // Tests only: the scene a start loads when no dev flag is set (the PlayMode harness pins the greybox arena so most tests stay fast; a Hollowmere test passes SceneNames.Hollowmere
        // to StartSoloAndWaitForPlayer, which clears the override). Null = the real default, Hollowmere.
        public static string StartSceneOverride { get; set; }

        // Dev only (-dev-combat, the editor menu, the dev panel toggle): start in the M3 combat arena. Off by default, so every test and normal start
        // still lands in the greybox arena.
        public static bool DevCombatArena { get; set; }

        // Dev only (-dev-dive, the editor menu, the dev panel toggle): start in the M5 dive hub (Arena_Dive). Wins over DevCombatArena.
        public static bool DevDiveArena { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { DevCombatArena = false; DevDiveArena = false; StartSceneOverride = null; }

        // The verdict of the last start attempt (Ok after a start); read by the dev panel to say why a start was refused.
        public WorldPlayerVerdict LastStartVerdict { get; private set; } = WorldPlayerVerdict.Ok;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.StateChanged -= HandleStateChanged;
            session.Ended -= HandleEnded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            networkManager.OnServerStarted -= InstallSceneFilter;
            networkManager.OnClientStarted -= InstallSceneFilter;
        }

        // Host only. Sends a connected player away from the lobby: they are disconnected with EndReason.SentAway (their door says why), and the
        // roster and world forget them through the approver's disconnect events. False when they are not connected or the session has started.
        public bool SendAway(PlayerKey key)
        {
            if (disposed || !networkManager.IsServer || world.Started) return false;
            if (!roster.TryFindConnected(key, out ulong clientId)) return false;
            if (clientId == NetworkManager.ServerClientId) return false;   // NGO refuses to disconnect the local host
            networkManager.DisconnectClient(clientId, EndReason.SentAway.ToString());
            return true;
        }

        // Host only. The start gate: false (and LastStartVerdict says why) until the world rules allow a start. A refused Solo start leaves the
        // session, because nothing else can make a Solo start possible (a Multiplayer host keeps waiting in the lobby for more players).
        public bool TryStartSession()
        {
            if (disposed || !networkManager.IsServer || session.State != ConnState.Connected) return false;
            if (world.Started) return true;   // already running: a second press changes nothing

            LastStartVerdict = world.TryStartSession(roster);
            if (LastStartVerdict != WorldPlayerVerdict.Ok)
            {
                Debug.LogWarning("SceneFlow: the session cannot start yet: " + LastStartVerdict + " (" + roster.ConnectedCount + " connected).");
                bus.Publish(new ToastRequested(WorldStartText.ToastKey(LastStartVerdict), LastStartVerdict.ToString()));
                if (session.Mode == SessionMode.Solo) ObserveLeave(session.LeaveAsync());
                return false;
            }
            return LoadFirstGameplayScene();
        }

        // NGO loads with plain LoadSceneAsync, so Unity can still activate a gameplay scene after the session went Offline
        // (cancel while loading, transport stop mid-load, host leaving during scene sync). Guard on Offline only:
        // a client legitimately activates the arena while still Connecting.
        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (disposed) return;
            if (SceneNames.IsGameplay(next.name) && session.State == ConnState.Offline)
            {
                SceneManager.LoadScene(SceneNames.Lobby, LoadSceneMode.Single);
            }
        }

        private void HandleStateChanged(ConnState previous, ConnState current)
        {
            if (disposed) return;

            // A new host session: the world waits for its kind (Solo for a solo session, else Multiplayer) until the first start creates it.
            // Runs before WorldSaveFlow's handler (subscribed later), which loads a saved world over it.
            if (current == ConnState.Connecting && session.IsHost)
            {
                world.Reset(session.Mode == SessionMode.Solo ? WorldKind.Solo : WorldKind.Multiplayer);
                return;
            }

            if (current == ConnState.Connected)
            {
                if (networkManager.IsServer && session.Mode == SessionMode.Solo) TryStartSession();
                return;
            }

            if (current == ConnState.Offline && SceneManager.GetActiveScene().name != SceneNames.Lobby)
            {
                SceneManager.LoadScene(SceneNames.Lobby, LoadSceneMode.Single);
            }
        }

        private bool LoadFirstGameplayScene()
        {
            NetworkSceneManager sceneManager = networkManager.SceneManager;
            if (sceneManager == null)
            {
                Debug.LogError("SceneFlow: NetworkManager.SceneManager is null (scene management disabled?); leaving the session.");
                ObserveLeave(session.LeaveAsync());
                return false;
            }

            string first = FirstGameplayScene;
            SceneEventProgressStatus status = sceneManager.LoadScene(first, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError("SceneFlow: LoadScene(" + first + ") returned " + status + "; leaving the session.");
                ObserveLeave(session.LeaveAsync());
                return false;
            }
            return true;
        }

        private void HandleEnded(EndReason reason)
        {
            if (disposed) return;
            string key = EndReasonText.ToastKey(reason);
            if (key != null) bus.Publish(new ToastRequested(key));
        }

        private static async void ObserveLeave(Task leave)
        {
            try
            {
                await leave;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
