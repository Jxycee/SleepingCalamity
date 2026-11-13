// Authority: test-only. Host + client on the local session (dev-direct on loopback, no UGS): the App is the host, a second NetworkManager in
// the same process is the client (PlayModeHarness.TestClient). Scene management is off on both peers (one process cannot load a scene twice),
// so these tests cover connection approval, the roster, the world-player rules and the start gate; the arena flow is SoloSessionTests.
// Catches: approval rules that unit tests miss because they only call Handle (NGO's real request path, the Reason string reaching the client,
// the client's DisconnectClassifier, the connection events that move a player into the roster and the world). NOT covered by a one-process
// test: owner-side NGO settings, scene sync to a real second machine, late join into the arena, Relay: those are the manual 2-player walk.
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HostClientTests
    {
        private const float Timeout = 20f;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        private static PlayerKey HostKey(AppBootstrap app) => PlayerKey.Local(app.Services.Get<ISettings>().EnsureLocalPlayerId());

        private static PlayerKey Player(int index) => PlayerKey.Local(PlayModeHarness.PlayerId(index));

        private static WorldPlayers World(WorldKind kind, params PlayerKey[] keys)
        {
            Assert.That(WorldPlayers.TryCreate(kind, keys, out WorldPlayers world), Is.EqualTo(WorldPlayerVerdict.Ok));
            return world;
        }

        private PlayModeHarness.TestClient NewClient(AppBootstrap app, int index)
        {
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(index));
            _clients.Add(client);
            return client;
        }

        private static IEnumerator Connect(PlayModeHarness.TestClient client)
        {
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            Assert.That(client.Session.State, Is.EqualTo(ConnState.Connected));
        }

        private static IEnumerator ExpectRefused(PlayModeHarness.TestClient client, EndReason expected)
        {
            EndReason got = EndReason.UserLeft;
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.AwaitRefusal(client.JoinAsync(cts.Token), Timeout, r => got = r);
            cts.Dispose();
            Assert.That(got, Is.EqualTo(expected), "the reason the client sees");
            Assert.That(client.Session.State, Is.EqualTo(ConnState.Offline).Or.EqualTo(ConnState.ShuttingDown).Or.EqualTo(ConnState.Failed));
        }

        // Catches: a join that never gets approved over the real NGO path, the roster not following the connection, a new player not being
        // recorded when it connects, and the start gate letting one connected player start a multiplayer world.
        [UnityTest]
        public IEnumerator HostAndClient_Connect_RosterFills_AndTheGateNeedsTwoPlayers()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            Assert.That(app.Roster.ConnectedCount, Is.EqualTo(1), "the host is in its own roster");

            // One player connected: the gate refuses, creates nothing, and the host stays in the lobby.
            Assert.That(app.SceneFlow.TryStartSession(), Is.False);
            Assert.That(app.SceneFlow.LastStartVerdict, Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(app.World.Players, Is.Null);
            Assert.That(app.World.Started, Is.False);
            Assert.That(app.Services.Get<INetworkSession>().State, Is.EqualTo(ConnState.Connected), "a refused start does not end the session");

            PlayModeHarness.TestClient client = NewClient(app, 1);
            yield return Connect(client);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the client never reached the host's roster");

            // Two connected: the gate opens, the Multiplayer world is created with both, and is persisted through Changed.
            int changed = 0;
            app.World.Changed += () => changed++;
            Assert.That(app.World.TryStartSession(app.Roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(app.World.Players.Kind, Is.EqualTo(WorldKind.Multiplayer));
            Assert.That(app.World.Players.IsRecorded(HostKey(app)), Is.True);
            Assert.That(app.World.Players.IsRecorded(client.Key), Is.True);
            Assert.That(app.World.ScalePlayers(app.Roster), Is.EqualTo(2));
            Assert.That(changed, Is.EqualTo(1));
        }

        // Catches (audit A3, user decision 2026-11-13): the 5th unique player being refused in the lobby of a full world (they may wait so the
        // host can rename or send them away), being recorded by waiting, or the start gate letting a session start with them unrecorded.
        [UnityTest]
        public IEnumerator FifthUniquePlayer_MayWaitInTheLobby_ButIsNeverRecordedAndStartIsRefused()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            app.World.Load(World(WorldKind.Multiplayer, HostKey(app), Player(1), Player(2), Player(3)));   // four recorded: the host and three friends
            yield return PlayModeHarness.StartDirectHostWithoutScenes();

            PlayModeHarness.TestClient fifth = NewClient(app, 9);   // not recorded
            yield return Connect(fifth);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the waiting stranger never reached the roster");
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(4));
            Assert.That(app.World.Players.IsRecorded(fifth.Key), Is.False);
            Assert.That(app.World.TryStartSession(app.Roster), Is.Not.EqualTo(WorldPlayerVerdict.Ok), "start is refused while an unrecorded player is here");
            Assert.That(app.World.Started, Is.False);
            Assert.That(app.World.Players.IsRecorded(fifth.Key), Is.False);
        }

        // Catches: the 5th unique player getting into a full world whose session is running, or being refused with a reason the client cannot show.
        [UnityTest]
        public IEnumerator FifthUniquePlayer_IsRefusedByApproval_OnceTheSessionHasStarted()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            app.World.Load(World(WorldKind.Multiplayer, HostKey(app), Player(1), Player(2), Player(3)));
            yield return PlayModeHarness.StartDirectHostWithoutScenes();

            PlayModeHarness.TestClient friend = NewClient(app, 1);   // recorded
            yield return Connect(friend);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the recorded player never reached the roster");
            Assert.That(app.World.TryStartSession(app.Roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(app.World.Started, Is.True);

            PlayModeHarness.TestClient fifth = NewClient(app, 9);   // not recorded
            yield return ExpectRefused(fifth, EndReason.FullLobby);
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(4));
            Assert.That(app.World.Players.IsRecorded(fifth.Key), Is.False);
            Assert.That(app.Roster.ConnectedCount, Is.EqualTo(2), "the host and the friend");
        }

        // Catches: a recorded player counted as new (refused when the world is full) and not recorded twice.
        [UnityTest]
        public IEnumerator RecordedPlayer_Rejoins_EvenWhenTheWorldIsFull()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            app.World.Load(World(WorldKind.Multiplayer, HostKey(app), Player(1), Player(2), Player(3)));
            yield return PlayModeHarness.StartDirectHostWithoutScenes();

            PlayModeHarness.TestClient friend = NewClient(app, 2);   // recorded
            yield return Connect(friend);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the recorded player never reached the roster");
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(4));

            // 2 of 4 can start (the host and one friend), and the session scales to the 2 present.
            Assert.That(app.World.TryStartSession(app.Roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(app.World.ScalePlayers(app.Roster), Is.EqualTo(2));
        }

        // Catches: a Solo world admitting a client over the real path, or refusing its own owner as the host.
        [UnityTest]
        public IEnumerator SoloWorld_RefusesAClient_ButHostsItsOwner()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            app.World.Load(World(WorldKind.Solo, HostKey(app)));
            yield return PlayModeHarness.StartDirectHostWithoutScenes();   // the owner hosts: approved
            Assert.That(app.Roster.ConnectedCount, Is.EqualTo(1));

            PlayModeHarness.TestClient client = NewClient(app, 1);
            yield return ExpectRefused(client, EndReason.JoinsClosed);
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(1));
            Assert.That(app.Roster.ConnectedCount, Is.EqualTo(1));
        }

        // Catches (audit A2): a new player with room in the world being refused, or written into the world at approval or when their connection
        // comes up. A player is recorded when they PLAY the world (user decision 2026-11-13): here, when the host starts the session.
        [UnityTest]
        public IEnumerator ANewPlayer_IsRecordedWhenTheSessionStarts_NotWhenTheyConnect()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            app.World.Load(World(WorldKind.Multiplayer, HostKey(app), Player(1)));
            yield return PlayModeHarness.StartDirectHostWithoutScenes();

            PlayModeHarness.TestClient newcomer = NewClient(app, 7);   // not recorded, room for it
            yield return Connect(newcomer);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the new player never reached the roster");
            Assert.That(app.World.Players.IsRecorded(newcomer.Key), Is.False, "a lobby visit is not playing the world");
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(2));

            Assert.That(app.World.TryStartSession(app.Roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(app.World.Players.IsRecorded(newcomer.Key), Is.True, "the start records who is here");
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(3));
        }

        // Catches (audit A2): a lobby visitor who leaves before the start keeping one of the world's four places for good, so the friend the
        // place was meant for is refused as FullLobby in every later session. Over the real approval path.
        [UnityTest]
        public IEnumerator AVisitorWhoLeavesBeforeTheStart_LeavesThePlaceFree()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            app.World.Load(World(WorldKind.Multiplayer, HostKey(app), Player(1), Player(2)));   // three recorded: one place left
            yield return PlayModeHarness.StartDirectHostWithoutScenes();

            PlayModeHarness.TestClient visitor = NewClient(app, 8);
            yield return Connect(visitor);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the visitor never reached the roster");
            yield return PlayModeHarness.Await(visitor.Session.LeaveAsync(), Timeout);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 1, Timeout, "the visitor's seat never went away");
            Assert.That(app.World.Players.IsRecorded(visitor.Key), Is.False, "the visitor was written into the world");
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(3));

            // The fourth friend still gets in and is recorded by the start.
            PlayModeHarness.TestClient friend = NewClient(app, 9);
            yield return Connect(friend);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, Timeout, "the fourth friend never reached the roster");
            Assert.That(app.World.TryStartSession(app.Roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(app.World.Players.IsRecorded(friend.Key), Is.True);
            Assert.That(app.World.Players.Recorded.Count, Is.EqualTo(4));
        }
    }
}
