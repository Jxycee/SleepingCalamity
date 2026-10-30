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
        }

        // The scene the first start loads.
        public string FirstGameplayScene => SceneNames.ArenaGreybox;

        // The verdict of the last start attempt (Ok after a start); read by the dev panel to say why a start was refused.
        public WorldPlayerVerdict LastStartVerdict { get; private set; } = WorldPlayerVerdict.Ok;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.StateChanged -= HandleStateChanged;
            session.Ended -= HandleEnded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
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
