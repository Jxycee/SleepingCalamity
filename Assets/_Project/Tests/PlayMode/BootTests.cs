// Authority: test-only (no networked state). Bootstrap composes the App services and lands on the Lobby with the session Offline.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class BootTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        // Catches: a service missing from the registry, NGO already running in the Lobby (a dev auto-start leaking in), the world gate not wired.
        [UnityTest]
        public IEnumerator Bootstrap_ComposesTheApp_AndLandsOnAnOfflineLobby()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Lobby));
            AppBootstrap app = AppBootstrap.Current;
            Assert.That(app, Is.Not.Null);
            Assert.That(app.NetworkManager, Is.Not.Null);
            Assert.That(ServiceRoots.App, Is.SameAs(app.Services), "ServiceRoots.App must be the AppBootstrap registry");

            Assert.That(app.Services.Contains<IInputService>(), Is.True);
            Assert.That(app.Services.Contains<ISettings>(), Is.True);
            Assert.That(app.Services.Contains<EventBus>(), Is.True);
            Assert.That(app.Services.Contains<INetworkSession>(), Is.True);
            Assert.That(app.Services.Contains<LocalNetworkSession>(), Is.True);
            Assert.That(app.Services.Contains<HostRoster>(), Is.True);
            Assert.That(app.Services.Contains<HostWorld>(), Is.True);

            Assert.That(app.Services.Get<INetworkSession>().State, Is.EqualTo(ConnState.Offline));
            Assert.That(app.NetworkManager.IsListening, Is.False, "NGO must not be running in the Lobby (dev auto-start must be suppressed)");
            Assert.That(app.World.Players, Is.Null, "no world until a session starts");
            Assert.That(app.NetworkManager.NetworkConfig.ConnectionApproval, Is.True);
            Assert.That(app.SceneFlow, Is.Not.Null);
        }
    }
}
