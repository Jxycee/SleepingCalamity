// Authority: test-only. ArenaPlayerSpawner's repair tick (audit A4, closes N7) on the real stack: a scene-managed host, a second NetworkManager in the same process
// that declines scenes (PlayModeHarness.TestClient declineScenes), so the host's load event ends by its timeout for that client and it is in clientsTimedOut.
// Catches: a connected client left in the hub without a hero for the rest of the session because the one-shot load event listed it as timed out and nothing retried,
// and the repair spawning a second hero for a client that already has one.
// NOT covered by a one-process test: a client that really loads the scene slowly (the harness client never loads it), failure path 2 (a joiner whose OnSynchronizeComplete
// arrives while the host's active scene is still the lobby: the same repair covers it, but the ordering needs a real second machine), a hero spawned into a scene the client
// is still loading (add to the manual 2-player walk: a friend on a slow PC reaches the town with a hero, no rejoin needed).
using System.Collections;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class ArenaSpawnerTickTests
    {
        private const float Timeout = 30f;
        private PlayModeHarness.TestClient _client;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null) { yield return _client.LeaveAndDestroy(); _client = null; }
            yield return PlayModeHarness.TearDownApp();
        }

        private static int PlayerObjectsOf(NetworkManager nm, ulong clientId)
        {
            int count = 0;
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == clientId) count++;
            return count;
        }

        [UnityTest]
        public IEnumerator ConnectedClientWithoutPlayerObject_GetsHeroFromSpawnerTick()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithScenes();
            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(5), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the second player never reached the roster");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True);
            yield return PlayModeHarness.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.ArenaGreybox && app.NetworkManager.LocalClient != null && app.NetworkManager.LocalClient.PlayerObject != null,
                Timeout, "the host never loaded the arena with its hero");

            NetworkManager host = app.NetworkManager, client = _client.NetworkManager;
            ulong clientId = client.LocalClientId;
            // The load event spawned the host's hero and gave up on the client (it declined the scene): nothing but the repair tick can give it one.
            yield return PlayModeHarness.WaitUntil(
                () => host.ConnectedClients.TryGetValue(clientId, out NetworkClient c) && c.PlayerObject != null,
                Timeout, "the connected client never got a hero: the spawner has no repair for a client the load event timed out");
            yield return PlayModeHarness.WaitUntil(() => client.LocalClient != null && client.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");

            yield return TownInputRig.Seconds(1.5f);   // a few more ticks: the repair must not stack a second hero
            Assert.That(PlayerObjectsOf(host, clientId), Is.EqualTo(1), "one hero for the client, however often the tick runs");
            Assert.That(PlayerObjectsOf(host, host.LocalClientId), Is.EqualTo(1), "and the host's own hero is untouched");
        }
    }
}
