// Authority: test-only. The world save flow on the real stack, against a throwaway library folder (WorldLibrary.DirectoryOverrideForTests):
// the files a real session writes, when it writes them, and the start gate through SceneFlow with a second peer connected.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class SaveFlowTests
    {
        private string _dir;
        private PlayModeHarness.TestClient _client;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null) { yield return _client.LeaveAndDestroy(); _client = null; }
            yield return PlayModeHarness.TearDownApp();
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        private static WorldSave ReadWorldFile(string dir, string worldId)
        {
            string text = File.ReadAllText(Path.Combine(dir, worldId + ".json"));
            return JsonSaveFile<WorldSave>.DeserializeOrNull(text, w => w.SchemaVersion);
        }

        // Catches: a solo session not creating its world file, the creation not reaching the disk until the 10 s tick, the host not being
        // recorded with its name, a second session opening a NEW world instead of the last played one, and New world not being selectable.
        [UnityTest]
        public IEnumerator Solo_WritesItsWorldAtOnce_ReopensIt_AndOpensASelectedNewWorld()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            AppBootstrap app = AppBootstrap.Current;

            yield return PlayModeHarness.StartSoloAndWaitForPlayer();

            string[] files = Directory.GetFiles(_dir, "*.json");
            Assert.That(files.Length, Is.EqualTo(1), "one world file");
            string firstId = Path.GetFileNameWithoutExtension(files[0]);
            Assert.That(WorldIds.IsValid(firstId), Is.True);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(firstId));

            // Nothing waited for the flush tick (10 s): the file already says who the world belongs to.
            WorldSave saved = ReadWorldFile(_dir, firstId);
            Assert.That(saved.Players.Kind, Is.EqualTo((int)WorldKind.Solo));
            Assert.That(saved.Players.Identity, Is.EqualTo((int)IdentityKind.Local));
            Assert.That(saved.Players.Recorded.Count, Is.EqualTo(1), "the host is the owner");
            Assert.That(saved.Players.Recorded[0].Name, Is.Not.Empty, "with the name it entered under");
            Assert.That(saved.SavedAtUtc, Is.Not.Empty);
            Assert.That(saved.CreatedAtUtc, Is.Not.Empty);
            string ownerId = saved.Players.Recorded[0].Id;

            yield return PlayModeHarness.LeaveAndWaitForLobby();

            // The same machine, the same world: the second solo start reopens it.
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            Assert.That(Directory.GetFiles(_dir, "*.json").Length, Is.EqualTo(1), "no second world was made");
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(firstId));
            Assert.That(ReadWorldFile(_dir, firstId).Players.Recorded[0].Id, Is.EqualTo(ownerId));
            yield return PlayModeHarness.LeaveAndWaitForLobby();

            // New world from the picker, selected: the next solo start opens it, and the first is untouched.
            string secondId = app.WorldSaveFlow.CreateWorld(WorldKind.Solo, IdentityKind.Local);
            Assert.That(secondId, Is.Not.Null.And.Not.EqualTo(firstId));
            Assert.That(app.WorldSaveFlow.ListWorlds(WorldKind.Solo, IdentityKind.Local).Count, Is.EqualTo(2));
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(secondId));
            Assert.That(ReadWorldFile(_dir, secondId).Players.Recorded.Count, Is.EqualTo(1));
            Assert.That(app.World.Players.IsRecorded(PlayerKey.Local(app.Services.Get<ISettings>().EnsureLocalPlayerId())), Is.True);
        }

        // Catches: the multiplayer start gate not being the thing that loads the arena, a lobby visitor being written into the world file before
        // the start or the players the start records not reaching the file at once (audit A2), and the 2-player start not creating the
        // Multiplayer arena session. The second peer connects, synchronizes and
        // declines scenes (one process), so the host's load event ends by its timeout for that peer.
        [UnityTest]
        public IEnumerator Multiplayer_StartsThroughSceneFlow_OnceASecondPlayerIsConnected()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithScenes();

            string worldId = app.WorldSaveFlow.SelectedWorldId;
            Assert.That(worldId, Is.Not.Null, "a Multiplayer world was opened (made) for the session");

            // One player connected: refused, still in the lobby, nothing loaded.
            Assert.That(app.SceneFlow.TryStartSession(), Is.False);
            Assert.That(app.SceneFlow.LastStartVerdict, Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Lobby));
            Assert.That(app.World.Started, Is.False);

            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(5), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the second player never reached the roster");

            // Nobody is written into the world by a lobby connection (user decision 2026-11-13, audit A2): the file still records no players.
            WorldSave beforeStart = ReadWorldFile(_dir, worldId);
            Assert.That(beforeStart.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer));
            Assert.That(beforeStart.Players.Recorded.Count, Is.EqualTo(0), "a lobby visit must not be recorded");

            // Two connected: the gate opens through the real flow and the arena loads on the host.
            Assert.That(app.SceneFlow.TryStartSession(), Is.True);
            Assert.That(app.World.Started, Is.True);

            // The start is what records them, and HostWorld.Changed put it on disk at once: host and friend, with names.
            WorldSave afterStart = ReadWorldFile(_dir, worldId);
            Assert.That(afterStart.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(afterStart.Players.Recorded[1].Id, Is.EqualTo(PlayModeHarness.PlayerId(5)));
            Assert.That(afterStart.Players.Recorded[1].Name, Is.Not.Empty, "with the name their connection brought");
            yield return PlayModeHarness.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.ArenaGreybox && app.NetworkManager.LocalClient.PlayerObject != null,
                30f, "the arena never loaded with the host's player");
            Assert.That(app.World.ScalePlayers(app.Roster), Is.EqualTo(2));
        }
    }
}
