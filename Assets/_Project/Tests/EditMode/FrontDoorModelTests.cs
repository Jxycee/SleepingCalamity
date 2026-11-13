// PL-3 task 3.2: the front door's rules, with a fake session, fake world library and fake host (FrontDoorFakes.cs). Catches: a button that is
// on when it should be off, a refusal that shows no words, a delete without asking, a screen that stays up after the session ended.
// NOT covered: the views (3.3), the real session, SceneFlow and WorldSaveFlow (PlayMode FrontDoorFlowTests, 3.5).
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class FrontDoorModelTests
    {
        private FakeNetworkSession session;
        private FakeWorlds worlds;
        private FakeHost host;
        private FrontDoorModel model;

        [SetUp]
        public void SetUp()
        {
            session = new FakeNetworkSession();
            worlds = new FakeWorlds();
            host = new FakeHost();
            model = new FrontDoorModel(session, worlds, host);
        }

        [TearDown]
        public void TearDown() => model.Dispose();

        [Test]
        public void Title_PlaySolo_NoWorlds_CreatesAndStarts()
        {
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));

            model.PlaySolo();

            Assert.That(worlds.Created, Has.Count.EqualTo(1), "a first play makes the first world");
            Assert.That(worlds.SelectedWorldId, Is.EqualTo(worlds.Created[0]));
            Assert.That(worlds.ListedIdentities, Has.All.EqualTo(IdentityKind.Local), "solo worlds are local");
            Assert.That(session.Calls, Is.EqualTo(new[] { "StartLocal(false)" }));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting), "the scene loads from here; the door stays in Connecting");
        }

        [Test]
        public void Title_PlaySolo_WithWorlds_OpensTheSelectedElseTheNewest()
        {
            WorldSummary older = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            WorldSummary newest = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            worlds.Select(older.Id);
            model.PlaySolo();
            Assert.That(worlds.SelectedWorldId, Is.EqualTo(older.Id), "the player's pick stands");
            Assert.That(worlds.Created, Is.Empty);

            model.Dispose();
            session = new FakeNetworkSession();
            worlds.Select(null);
            model = new FrontDoorModel(session, worlds, host);
            model.PlaySolo();
            Assert.That(worlds.SelectedWorldId, Is.EqualTo(newest.Id), "nothing picked: the most recently played");
            Assert.That(worlds.Created, Is.Empty);
        }

        [Test]
        public void Worlds_Full_NewWorldDisabled_WithWords()
        {
            for (int i = 0; i < WorldSummary.MaxPerList; i++) worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");

            model.OpenWorlds(WorldKind.Solo);

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Worlds));
            Assert.That(model.Rows, Has.Count.EqualTo(WorldSummary.MaxPerList));
            Assert.That(model.NewWorldAction.Enabled, Is.False);
            Assert.That(model.NewWorldAction.Why, Is.EqualTo(UiText.WorldLibraryFull(WorldKind.Solo)));
            Assert.That(model.NewWorldAction.Why, Does.Contain("10"));

            model.CreateWorld();
            Assert.That(worlds.Created, Is.Empty, "a disabled button asks for nothing");
        }

        [Test]
        public void Worlds_Room_NewWorldEnabled_CreatesSelectsAndListsIt()
        {
            worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            model.OpenWorlds(WorldKind.Solo);
            Assert.That(model.NewWorldAction.Enabled, Is.True);
            Assert.That(model.NewWorldAction.Why, Is.Null);

            model.CreateWorld();

            Assert.That(worlds.Created, Has.Count.EqualTo(1));
            Assert.That(model.Rows, Has.Count.EqualTo(2));
            Assert.That(model.Rows[0].Selected, Is.True, "the new world is the selected one, listed first");
        }

        [Test]
        public void Worlds_Delete_AsksFirst()
        {
            WorldSummary w = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            model.OpenWorlds(WorldKind.Solo);

            model.RequestDelete(w.Id);

            Assert.That(model.PendingDeleteId, Is.EqualTo(w.Id));
            Assert.That(model.PendingDeleteWords, Is.Not.Null.And.Not.Empty, "it says what it is asking");
            Assert.That(worlds.Deleted, Is.Empty, "asking is not deleting");
            Assert.That(model.Rows, Has.Count.EqualTo(2));

            model.ConfirmDelete();

            Assert.That(worlds.Deleted, Is.EqualTo(new[] { w.Id }));
            Assert.That(model.PendingDeleteId, Is.Null);
            Assert.That(model.Rows, Has.Count.EqualTo(1));
        }

        [Test]
        public void Worlds_Delete_Cancel_KeepsTheWorld()
        {
            WorldSummary w = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            model.OpenWorlds(WorldKind.Solo);
            model.RequestDelete(w.Id);

            model.CancelDelete();

            Assert.That(model.PendingDeleteId, Is.Null);
            Assert.That(worlds.Deleted, Is.Empty);
            Assert.That(model.Rows, Has.Count.EqualTo(1));
            model.ConfirmDelete();   // nothing pending: nothing happens
            Assert.That(worlds.Deleted, Is.Empty);
        }

        [Test]
        public void Host_ShowsJoinCode_WhenConnected()
        {
            model.OpenWorlds(WorldKind.Multiplayer);
            Assert.That(model.HostAction.Enabled, Is.True);

            model.HostSelected();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby));
            Assert.That(model.JoinCode, Is.EqualTo("ABC123"));
            Assert.That(session.Calls, Is.EqualTo(new[] { "HostOnline" }));
            Assert.That(worlds.ListedIdentities, Has.All.EqualTo(IdentityKind.Ugs), "the front door hosts online worlds only");
        }

        [Test]
        public void Host_WhileConnecting_NoCodeYet()
        {
            model.OpenWorlds(WorldKind.Multiplayer);
            session.HoldConnect = new System.Threading.Tasks.TaskCompletionSource<bool>();

            model.HostSelected();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));
            Assert.That(model.JoinCode, Is.Null, "no code until the session is up");
            Assert.That(model.HostAction.Enabled, Is.False, "one attempt at a time");
        }

        [Test]
        public void Host_OnePlayer_StartDisabled_SaysNeedsTwo()
        {
            ReachHostLobby();
            host.ConnectedCount = 1;

            Assert.That(model.PlayerCount, Is.EqualTo(1));
            Assert.That(model.StartAction.Enabled, Is.False);
            Assert.That(model.StartAction.Why, Is.EqualTo(UiText.StartRefusal(WorldPlayerVerdict.NotEnoughPlayers)));

            model.StartSession();
            Assert.That(host.StartCalls, Is.EqualTo(0), "a disabled Start does not ask the gate");
        }

        [Test]
        public void Host_TwoPlayers_Start_CallsTryStartSession()
        {
            ReachHostLobby();
            host.ConnectedCount = 2;
            host.Names = new System.Collections.Generic.List<string> { "Ann", "Bo" };

            Assert.That(model.StartAction.Enabled, Is.True);
            Assert.That(model.StartAction.Why, Is.Null);
            Assert.That(model.PlayerNames, Is.EqualTo(new[] { "Ann", "Bo" }));

            model.StartSession();

            Assert.That(host.StartCalls, Is.EqualTo(1));
            Assert.That(model.StartMessage, Is.Null);
        }

        [Test]
        public void Host_Start_RefusedByTheGate_ShowsTheGatesWords()
        {
            ReachHostLobby();
            host.ConnectedCount = 2;
            host.NextVerdict = WorldPlayerVerdict.NotRecorded;

            model.StartSession();

            Assert.That(host.StartCalls, Is.EqualTo(1));
            Assert.That(model.StartMessage, Is.EqualTo(UiText.StartRefusal(WorldPlayerVerdict.NotRecorded)));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby), "the host stays and waits");
            Assert.That(model.StartAction.Enabled, Is.True, "the host can try again");
        }

        [Test]
        public void Join_EmptyCode_Disabled()
        {
            model.OpenJoin();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.JoinEntry));

            Assert.That(model.JoinAction.Enabled, Is.False);
            Assert.That(model.JoinAction.Why, Is.EqualTo(UiText.JoinCodeEmpty));

            model.JoinCodeInput = "   ";
            Assert.That(model.JoinAction.Enabled, Is.False, "spaces are not a code");
            model.Join();
            Assert.That(session.Calls, Is.Empty);

            model.JoinCodeInput = "ab-12";
            Assert.That(model.JoinAction.Enabled, Is.False);
            Assert.That(model.JoinAction.Why, Is.EqualTo(UiText.JoinCodeInvalid));

            model.JoinCodeInput = " abc123 ";
            Assert.That(model.JoinAction.Enabled, Is.True);
            Assert.That(model.JoinAction.Why, Is.Null);
        }

        [Test]
        public void Join_Refused_ShowsWords_BackToJoinEntry()
        {
            model.OpenJoin();
            model.JoinCodeInput = "abc123";
            session.FailNextStartWith = EndReason.FullLobby;

            model.Join();

            Assert.That(session.Calls, Is.EqualTo(new[] { "JoinOnline(ABC123)" }), "the code goes out normalised");
            Assert.That(model.State, Is.EqualTo(FrontDoorState.JoinEntry));
            Assert.That(model.JoinError, Is.EqualTo(UiText.JoinRefusal(EndReason.FullLobby)));
            Assert.That(model.JoinCodeInput, Is.EqualTo("abc123"), "the player does not retype it");
            Assert.That(model.JoinAction.Enabled, Is.True, "and can try again");

            model.JoinCodeInput = "abc124";
            Assert.That(model.JoinError, Is.Null, "typing clears the old refusal");
        }

        [Test]
        public void Client_Connected_NotStarted_WaitingForHost()
        {
            model.OpenJoin();
            model.JoinCodeInput = "ABC123";

            model.Join();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.WaitingForHost));
            Assert.That(model.WaitingWords, Is.EqualTo(UiText.WaitingForHost));
            Assert.That(model.StartAction.Enabled, Is.False, "a guest never starts the game");
        }

        [Test]
        public void Client_WhileConnecting_CanBeCancelled()
        {
            model.OpenJoin();
            model.JoinCodeInput = "ABC123";
            session.HoldConnect = new System.Threading.Tasks.TaskCompletionSource<bool>();

            model.Join();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));
            Assert.That(model.JoinAction.Enabled, Is.False, "one attempt at a time");

            model.Back();

            Assert.That(session.Calls, Does.Contain("Leave"));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.JoinEntry), "a cancelled join is back where it began, with no error");
            Assert.That(model.JoinError, Is.Null);
        }

        [Test]
        public void SessionEnded_ShowsReason_ThenTitle()
        {
            model.OpenJoin();
            model.JoinCodeInput = "ABC123";
            model.Join();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.WaitingForHost));

            session.EndExternally(EndReason.HostLeft);

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Ended));
            Assert.That(model.EndedReason, Is.EqualTo(EndReason.HostLeft));
            Assert.That(model.EndedWords, Is.EqualTo(UiText.EndedBecause(EndReason.HostLeft)));

            model.Acknowledge();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));
            Assert.That(model.EndedWords, Is.Null);
        }

        [Test]
        public void SessionEnded_InHostLobby_ShowsReason_ThenTitle()
        {
            ReachHostLobby();

            session.EndExternally(EndReason.ServicesUnavailable);

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Ended));
            Assert.That(model.EndedWords, Is.EqualTo(UiText.EndedBecause(EndReason.ServicesUnavailable)));
            Assert.That(model.JoinCode, Is.Null, "the old code is gone with the lobby");
            model.Acknowledge();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));
        }

        [Test]
        public void Leave_FromHostLobby_GoesStraightToTitle()
        {
            ReachHostLobby();

            model.Leave();

            Assert.That(session.Calls, Does.Contain("Leave"));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title), "the player chose to go: no reason screen");
        }

        [Test]
        public void SoloStart_RefusedByTheGate_ShowsTheGatesWords()
        {
            // SceneFlow leaves a Solo session whose start was refused (the session ends with UserLeft); the door must say why.
            host.LastStartVerdict = WorldPlayerVerdict.WrongPlayerCount;
            model.PlaySolo();
            session.EndExternally(EndReason.UserLeft);

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Ended));
            Assert.That(model.EndedWords, Is.EqualTo(UiText.StartRefusal(WorldPlayerVerdict.WrongPlayerCount)));
        }

        [Test]
        public void SoloWorld_NotOfferedForHosting()
        {
            worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            WorldSummary together = worlds.Add(WorldKind.Multiplayer, IdentityKind.Ugs, "Ann", "Bo");

            model.OpenWorlds(WorldKind.Solo);
            Assert.That(model.HostAction.Enabled, Is.False, "the solo list offers Play, not Host");
            Assert.That(model.PlayAction.Enabled, Is.True);
            model.HostSelected();
            Assert.That(session.Calls, Is.Empty);

            model.Back();
            model.OpenWorlds(WorldKind.Multiplayer);
            Assert.That(model.Rows, Has.Count.EqualTo(1), "the solo world is not in the multiplayer list");
            Assert.That(model.Rows[0].Id, Is.EqualTo(together.Id));
            Assert.That(model.PlayAction.Enabled, Is.False, "a multiplayer world is not started alone");
            model.PlaySelected();
            Assert.That(session.Calls, Is.Empty);
            Assert.That(model.HostAction.Enabled, Is.True);
        }

        private void ReachHostLobby()
        {
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby), "setup: the lobby is up");
        }
    }
}
