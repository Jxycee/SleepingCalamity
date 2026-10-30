// Authority: test-only. Solo start/leave on the real stack: a host-authoritative solo session on 127.0.0.1 port 7790 (plain UnityTransport),
// Bootstrap -> Lobby -> Arena_Greybox -> Lobby.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class SoloSessionTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        // Catches: the solo start gate not running (the arena never loads), the Solo world not being created with the host as its one
        // recorded player, the player not spawning at the spawn point or not owned by this peer, and the flow not coming back to the Lobby.
        [UnityTest]
        public IEnumerator Solo_StartsInTheArena_SpawnsTheOwnedPlayer_AndLeaveReturnsToTheLobby()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            AppBootstrap app = AppBootstrap.Current;
            var session = app.Services.Get<INetworkSession>();
            Assert.That(session.State, Is.EqualTo(ConnState.Connected));
            Assert.That(session.Mode, Is.EqualTo(SessionMode.Solo));
            Assert.That(app.NetworkManager.IsHost, Is.True, "solo runs as the host");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.ArenaGreybox));

            Assert.That(app.World.Started, Is.True);
            Assert.That(app.World.Players.Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(1), "the host is the one recorded player");

            NetworkObject player = app.NetworkManager.LocalClient.PlayerObject;
            Assert.That(player.IsOwner, Is.True);
            Assert.That(player.transform.position.magnitude, Is.LessThan(0.5f), "at the spawn point");
            Assert.That(player.gameObject.layer, Is.EqualTo(Layers.Player));

            // One frame batch so a duplicate spawn (load event plus synchronize) would show up before counting.
            for (int i = 0; i < 30; i++) yield return null;
            int players = 0;
            foreach (NetworkObject o in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None)) if (o.IsPlayerObject) players++;
            Assert.That(players, Is.EqualTo(1), "exactly one player object");

            yield return PlayModeHarness.LeaveAndWaitForLobby();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Lobby));
            Assert.That(app.Roster.ConnectedCount, Is.EqualTo(0), "the roster is cleared with the session");
        }

        // Catches: a second solo start after a leave failing (a stale approver, a roster or world left over, the port still bound).
        [UnityTest]
        public IEnumerator Solo_CanBeStartedAgainAfterLeaving()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            Assert.That(AppBootstrap.Current.World.Started, Is.True);
            Assert.That(AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject, Is.Not.Null);
        }
    }
}
