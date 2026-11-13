// PL-3 task 3.2: the front door's rules, with a fake session, fake world library and fake host (FrontDoorFakes.cs). Catches: a button that is
// on when it should be off, a refusal that shows no words, a delete without asking, a screen that stays up after the session ended.
// NOT covered: the views (3.3), the real session, SceneFlow and WorldSaveFlow (PlayMode FrontDoorFlowTests, 3.5).
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

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

        // Catches (audit A2): the host having no way to let a player go from a multiplayer world, a removal that happens without asking, the
        // question not naming the player, or the row still listing them afterwards.
        [Test]
        public void Worlds_RemovePlayer_AsksFirst_ThenFreesThePlace()
        {
            WorldSummary w = worlds.Add(WorldKind.Multiplayer, IdentityKind.Ugs, "Ann", "Bo", "Cleo", "Dag");
            model.OpenWorlds(WorldKind.Multiplayer);
            Assert.That(model.CanRemovePlayers, Is.True);
            FrontDoorWorldRow row = model.Rows[0];
            Assert.That(row.Players, Has.Count.EqualTo(4), "the row carries the recorded players with their keys");
            PlayerKey cleo = row.Players[2].Key;

            model.RequestRemovePlayer(w.Id, cleo);

            Assert.That(model.PendingRemoveWorldId, Is.EqualTo(w.Id));
            Assert.That(model.PendingRemoveKey, Is.EqualTo(cleo));
            Assert.That(model.PendingRemoveWords, Does.Contain("Cleo"), "the question names the player");
            Assert.That(worlds.RemovedPlayers, Is.Empty, "asking is not removing");

            model.ConfirmRemovePlayer();

            Assert.That(worlds.RemovedPlayers, Is.EqualTo(new[] { w.Id + ":" + cleo.Id }));
            Assert.That(model.PendingRemoveWorldId, Is.Null);
            Assert.That(model.Notice, Does.Contain("Cleo"));
            Assert.That(model.Rows[0].Players, Has.Count.EqualTo(3), "the row no longer lists them");
            Assert.That(model.Rows[0].PlayerNames, Does.Not.Contain("Cleo"));
        }

        // Catches: a removal that happens on a cancel or on a stray Confirm, a question that survives leaving the screen, a failure shown as a
        // success, and a solo world (one owner, bound to this machine) offering the action at all.
        [Test]
        public void Worlds_RemovePlayer_Cancel_AndRefusals()
        {
            WorldSummary multi = worlds.Add(WorldKind.Multiplayer, IdentityKind.Ugs, "Ann", "Bo");
            model.OpenWorlds(WorldKind.Multiplayer);
            PlayerKey bo = model.Rows[0].Players[1].Key;

            model.RequestRemovePlayer(multi.Id, bo);
            model.CancelRemovePlayer();
            Assert.That(model.PendingRemoveWorldId, Is.Null);
            Assert.That(worlds.RemovedPlayers, Is.Empty);
            model.ConfirmRemovePlayer();   // nothing pending
            Assert.That(worlds.RemovedPlayers, Is.Empty);

            // A player the row does not hold, and a world that is not listed: no question is opened.
            model.RequestRemovePlayer(multi.Id, new PlayerKey(IdentityKind.Ugs, "stranger"));
            Assert.That(model.PendingRemoveWorldId, Is.Null);
            model.RequestRemovePlayer("no-such-world", bo);
            Assert.That(model.PendingRemoveWorldId, Is.Null);

            // Leaving the screen drops the question.
            model.RequestRemovePlayer(multi.Id, bo);
            model.OpenWorlds(WorldKind.Multiplayer);
            Assert.That(model.PendingRemoveWorldId, Is.Null, "the question does not survive the screen");

            // A refusal says so instead of claiming the place is free.
            worlds.RemoveFails = true;
            model.RequestRemovePlayer(multi.Id, bo);
            model.ConfirmRemovePlayer();
            Assert.That(model.Notice, Is.EqualTo(UiText.PlayerNotRemoved));
            Assert.That(model.Rows[0].Players, Has.Count.EqualTo(2));

            // The solo list never offers it.
            worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            model.OpenWorlds(WorldKind.Solo);
            Assert.That(model.CanRemovePlayers, Is.False);
            model.RequestRemovePlayer(model.Rows[0].Id, model.Rows[0].Players[0].Key);
            Assert.That(model.PendingRemoveWorldId, Is.Null, "a solo world's owner follows the machine; delete the world instead");
        }

        // Catches: two questions open at once (Delete it would answer the one the player is not looking at), and Back not answering the
        // remove question with no.
        [Test]
        public void Worlds_OneQuestionAtATime_AndBackAnswersIt()
        {
            WorldSummary w = worlds.Add(WorldKind.Multiplayer, IdentityKind.Ugs, "Ann", "Bo");
            model.OpenWorlds(WorldKind.Multiplayer);
            PlayerKey bo = model.Rows[0].Players[1].Key;

            model.RequestDelete(w.Id);
            model.RequestRemovePlayer(w.Id, bo);
            Assert.That(model.PendingDeleteId, Is.Null, "asking about a player closes the delete question");
            Assert.That(model.PendingRemoveWorldId, Is.EqualTo(w.Id));

            model.RequestDelete(w.Id);
            Assert.That(model.PendingRemoveWorldId, Is.Null, "and the other way round");
            Assert.That(model.PendingDeleteId, Is.EqualTo(w.Id));
            model.CancelDelete();

            model.RequestRemovePlayer(w.Id, bo);
            model.Back();
            Assert.That(model.PendingRemoveWorldId, Is.Null, "Back (Escape) answers it with no");
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Worlds), "and does not also leave the screen");
            Assert.That(worlds.RemovedPlayers, Is.Empty);
        }

        // Catches (audit A5): a world that cannot be saved starting without a word (hours of play lost), "no" still starting it, "yes" not starting it.
        [Test]
        public void Play_OnAWorldThatCannotBeSaved_AsksFirst_AndStartsOnlyOnYes()
        {
            WorldSummary w = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            worlds.Unsavable.Add(w.Id);
            model.OpenWorlds(WorldKind.Solo);

            model.PlaySelected();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.ConfirmUnsaved));
            Assert.That(model.ConfirmUnsavedWords, Is.EqualTo(UiText.CannotSaveAsk));
            Assert.That(UiText.CannotSaveAsk, Is.EqualTo("This world can't be saved. Play anyway?"));
            Assert.That(session.Calls, Is.Empty, "asking starts nothing");

            model.DeclinePlayAnyway();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Worlds), "no goes back to where the player was");
            Assert.That(session.Calls, Is.Empty, "no starts nothing");

            model.PlaySelected();
            model.Back();   // Escape answers with no
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Worlds));
            Assert.That(session.Calls, Is.Empty);

            model.PlaySelected();
            model.PlayAnyway();
            Assert.That(session.Calls, Is.EqualTo(new[] { "StartLocal(false)" }));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));
        }

        // Catches (audit A5): the question missing from Host, from the Title's Play solo, or asked for a world that saves fine.
        [Test]
        public void Host_AndTitlePlay_AskToo_ButASavableWorldStartsAtOnce()
        {
            WorldSummary multi = worlds.Add(WorldKind.Multiplayer, IdentityKind.Ugs, "Ann", "Bo");
            worlds.Unsavable.Add(multi.Id);
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.ConfirmUnsaved));
            Assert.That(session.Calls, Is.Empty);
            model.PlayAnyway();
            Assert.That(session.Calls, Is.EqualTo(new[] { "HostOnline" }));

            model.Dispose();
            session = new FakeNetworkSession();
            worlds = new FakeWorlds();
            model = new FrontDoorModel(session, worlds, host);
            WorldSummary solo = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            worlds.Unsavable.Add(solo.Id);
            model.PlaySolo();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.ConfirmUnsaved), "Play solo on the title asks too");
            model.DeclinePlayAnyway();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));
            worlds.Unsavable.Clear();
            model.PlaySolo();
            Assert.That(session.Calls, Is.EqualTo(new[] { "StartLocal(false)" }), "a world that saves starts without a question");
        }

        // Catches (user decision 2026-11-13): a stranger in a full world's lobby that the host cannot send away, or one sent without asking,
        // or one whose disconnect carries no reason of its own.
        [Test]
        public void Host_FullWorld_Stranger_SendAway_Disconnects()
        {
            ReachHostLobby();
            PlayerKey stranger = new PlayerKey(IdentityKind.Ugs, "fifth");
            Assert.That(model.Strangers, Is.Empty);
            host.Strangers.Add(new FrontDoorPerson { Key = stranger, Name = "Eve" });
            Assert.That(model.Strangers, Has.Count.EqualTo(1));

            model.RequestSendAway(new PlayerKey(IdentityKind.Ugs, "nobody"));
            Assert.That(model.HasPendingSendAway, Is.False, "only a listed stranger can be asked about");
            model.RequestSendAway(stranger);
            Assert.That(model.HasPendingSendAway, Is.True);
            Assert.That(model.PendingSendAwayWords, Does.Contain("Eve"));
            Assert.That(host.SentAway, Is.Empty, "asking is not sending away");

            model.CancelSendAway();
            model.ConfirmSendAway();   // nothing pending
            Assert.That(host.SentAway, Is.Empty);

            model.RequestSendAway(stranger);
            model.ConfirmSendAway();
            Assert.That(host.SentAway, Is.EqualTo(new[] { "fifth" }));
            Assert.That(model.HasPendingSendAway, Is.False);
            Assert.That(model.RenameMessage, Is.EqualTo(UiText.PlayerSentAway("Eve")));

            Assert.That(EndReasonText.ToastKey(EndReason.SentAway), Is.Not.EqualTo("err.generic"), "the sent-away player is told why");
            Assert.That(UiText.EndedBecause(EndReason.SentAway), Does.Contain("host"));
        }

        // Catches: the host saying "was sent away" when SendAway returned false (the stranger had already left, or the host refused).
        [Test]
        public void Host_SendAway_ReturnsFalse_NoSentAwayMessage()
        {
            ReachHostLobby();
            PlayerKey stranger = new PlayerKey(IdentityKind.Ugs, "fifth");
            host.Strangers.Add(new FrontDoorPerson { Key = stranger, Name = "Eve" });
            Assert.That(model.Strangers, Has.Count.EqualTo(1));
            model.RequestSendAway(stranger);
            Assert.That(model.HasPendingSendAway, Is.True);

            host.Strangers.Clear();   // they left before the host confirmed
            model.ConfirmSendAway();

            Assert.That(model.HasPendingSendAway, Is.False);
            Assert.That(model.RenameMessage, Is.Not.EqualTo(UiText.PlayerSentAway("Eve")));
            Assert.That(model.RenameMessage, Is.EqualTo(UiText.PlayerAlreadyLeft("Eve")));
        }

        // Catches (user decision 2026-11-13): the unrecorded player in a full world seeing the plain "waiting for the start" line, and Start
        // staying on (or refusing without naming the two choices) while they are there.
        [Test]
        public void Host_FullWorld_Stranger_SeesWaitingWords()
        {
            host.WaitsForPlace = true;
            model.OpenJoin();
            model.JoinCodeInput = "ABC123";
            model.Join();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.WaitingForHost));
            Assert.That(model.WaitingWords, Is.EqualTo(UiText.WaitingForPlace));
            Assert.That(UiText.WaitingForPlace, Is.EqualTo("This world is full. Waiting for the host to add you."));

            host.WaitsForPlace = false;
            Assert.That(model.WaitingWords, Is.EqualTo(UiText.WaitingForHost));

            // The host's side: Start is off while a stranger is present, and the words name both choices.
            model.Dispose();
            host = new FakeHost();
            host.Strangers.Add(new FrontDoorPerson { Key = new PlayerKey(IdentityKind.Ugs, "fifth"), Name = "Eve" });
            session = new FakeNetworkSession();
            model = new FrontDoorModel(session, worlds, host);
            ReachHostLobby();
            host.ConnectedCount = 3;
            Assert.That(model.StartAction.Enabled, Is.False);
            Assert.That(model.StartAction.Why, Does.Contain("friend").And.Contain("send"));
        }

        // Catches (audit A3): the host having no way to say "this connected player is that recorded one", a rename without asking, one the
        // lobby did not offer, a refusal shown as a success.
        [Test]
        public void Lobby_RenamePlayer_AsksFirst_ThenRenamesOne()
        {
            ReachHostLobby();
            PlayerKey fresh = new PlayerKey(IdentityKind.Ugs, "new-id");
            PlayerKey old = new PlayerKey(IdentityKind.Ugs, "old-id");
            Assert.That(model.RenameOptions, Is.Empty, "nobody to choose between");
            host.Newcomers.Add(new FrontDoorPerson { Key = fresh, Name = "Ann" });
            host.RecordedAway.Add(new FrontDoorPerson { Key = old, Name = "Annie" });
            Assert.That(model.RenameOptions, Has.Count.EqualTo(1));

            model.RequestRename(new PlayerKey(IdentityKind.Ugs, "stranger"), old);
            Assert.That(model.HasPendingRename, Is.False, "only offered pairs can be asked");

            model.RequestRename(fresh, old);
            Assert.That(model.HasPendingRename, Is.True);
            Assert.That(model.PendingRenameWords, Does.Contain("Ann").And.Contain("Annie"));
            Assert.That(host.Renames, Is.Empty, "asking is not renaming");

            model.CancelRename();
            model.ConfirmRename();   // nothing pending
            Assert.That(host.Renames, Is.Empty);

            model.RequestRename(fresh, old);
            model.Back();
            Assert.That(model.HasPendingRename, Is.False, "Back answers with no");
            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby), "and does not leave the lobby");

            model.RequestRename(fresh, old);
            model.ConfirmRename();
            Assert.That(host.Renames, Is.EqualTo(new[] { "old-id->new-id" }));
            Assert.That(model.RenameMessage, Is.EqualTo(UiText.PlayerRenamed("Annie")));

            host.RenameVerdict = WorldPlayerVerdict.DuplicatePlayer;
            model.RequestRename(fresh, old);
            model.ConfirmRename();
            Assert.That(model.RenameMessage, Is.EqualTo(UiText.PlayerNotRenamed(WorldPlayerVerdict.DuplicatePlayer)));
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

        // ---- review round 1 ----

        [Test]
        public void Worlds_Back_WithDeletePending_CancelsTheAskAndStaysOnWorlds()
        {
            WorldSummary w = worlds.Add(WorldKind.Solo, IdentityKind.Local, "Ann");
            model.OpenWorlds(WorldKind.Solo);
            model.RequestDelete(w.Id);
            Assert.That(model.PendingDeleteId, Is.EqualTo(w.Id));

            model.Back();

            Assert.That(model.PendingDeleteId, Is.Null, "Back (Escape) answers the question with no");
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Worlds), "and does not also leave the screen");
            Assert.That(worlds.Deleted, Is.Empty);
            model.Back();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title), "a second Back leaves");
        }

        [Test]
        public void HostLobby_Back_DoesNotEndTheSession()
        {
            ReachHostLobby();

            model.Back();

            Assert.That(session.Calls, Does.Not.Contain("Leave"), "Escape / gamepad B must not drop a lobby; only the Leave button does");
            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby));
            model.Leave();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title), "the explicit Leave still leaves");
        }

        [Test]
        public void WaitingForHost_Back_DoesNotEndTheSession()
        {
            ReachWaiting();

            model.Back();

            Assert.That(session.Calls, Does.Not.Contain("Leave"));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.WaitingForHost));
            model.Leave();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));
        }

        [Test]
        public void Connecting_Back_StillCancels()
        {
            session.HoldConnect = new System.Threading.Tasks.TaskCompletionSource<bool>();
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));

            model.Back();

            Assert.That(session.Calls, Does.Contain("Leave"));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));
        }

        // The verdict of an earlier refused start is still on the host side; a Leave while a later solo start is Connecting is the player's own choice.
        [Test]
        public void Solo_LeaveWhileConnecting_AfterAnEarlierRefusal_EndsOnTitle()
        {
            host.LastStartVerdict = WorldPlayerVerdict.NotEnoughPlayers;   // stale: from a multiplayer lobby the player left
            session.HoldConnect = new System.Threading.Tasks.TaskCompletionSource<bool>();
            model.PlaySolo();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));

            model.Leave();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title), "not an Ended screen that blames a start the player never made");
            Assert.That(model.EndedWords, Is.Null);
        }

        [Test]
        public void Leave_WhenTheSessionFaults_StaysOnTheScreen()
        {
            ReachHostLobby();
            session.LeaveFaults = true;

            model.Leave();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby), "the session is still up, so the lobby stays");
            Assert.That(model.JoinCode, Is.Not.Null);
        }

        [Test]
        public void Leave_WhenTheSessionStaysUp_StaysOnTheScreen()
        {
            ReachWaiting();
            session.LeaveDoesNothing = true;

            model.Leave();

            Assert.That(model.State, Is.EqualTo(FrontDoorState.WaitingForHost));
        }

        [Test]
        public void SoloStart_RefusedByTheGate_InSceneFlowOrder_ShowsTheGatesWords()
        {
            // The fake session asks the host's gate on connecting and leaves on a refusal, the way SceneFlow does.
            host.NextVerdict = WorldPlayerVerdict.WrongPlayerCount;
            session.SoloHost = host;

            model.PlaySolo();

            Assert.That(host.StartCalls, Is.EqualTo(1));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Ended));
            Assert.That(model.EndedWords, Is.EqualTo(UiText.StartRefusal(WorldPlayerVerdict.WrongPlayerCount)));
        }

        [Test]
        public void SoloStart_Accepted_InSceneFlowOrder_StaysConnecting()
        {
            session.SoloHost = host;

            model.PlaySolo();

            Assert.That(host.StartCalls, Is.EqualTo(1));
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));
        }

        // ---- races between a held start and the player backing out ----

        [Test]
        public void Race_SoloBack_ThenHeldStartReleased_StaysOnTitle()
        {
            var hold = new System.Threading.Tasks.TaskCompletionSource<bool>();
            session.HoldConnect = hold;
            model.PlaySolo();
            model.Back();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));

            hold.SetResult(true);

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title), "a start that finishes after the cancel does not bring the door back to life");
            Assert.That(model.EndedWords, Is.Null);
        }

        [Test]
        public void Race_HostCancel_ThenHeldStartFaults_StaysOnTitle()
        {
            var hold = new System.Threading.Tasks.TaskCompletionSource<bool>();
            session.HoldConnect = hold;
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            model.Leave();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));

            hold.SetException(new NetworkSessionException(EndReason.Timeout));

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title), "no Ended screen for an attempt the player cancelled");
            Assert.That(model.JoinCode, Is.Null);
        }

        [Test]
        public void Race_HostCancel_ThenHeldStartReleased_DoesNotOpenTheLobby()
        {
            var hold = new System.Threading.Tasks.TaskCompletionSource<bool>();
            session.HoldConnect = hold;
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            model.Back();

            hold.SetResult(true);

            Assert.That(model.State, Is.EqualTo(FrontDoorState.Title));
            Assert.That(model.JoinCode, Is.Null);
        }

        [Test]
        public void Race_JoinCancel_ThenHeldStartFaults_StaysOnJoinEntryWithoutError()
        {
            var hold = new System.Threading.Tasks.TaskCompletionSource<bool>();
            session.HoldConnect = hold;
            model.OpenJoin();
            model.JoinCodeInput = "ABC123";
            model.Join();
            model.Back();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.JoinEntry));

            hold.SetException(new NetworkSessionException(EndReason.Timeout));

            Assert.That(model.State, Is.EqualTo(FrontDoorState.JoinEntry));
            Assert.That(model.JoinError, Is.Null, "the player cancelled; a late timeout is not news");
        }

        [Test]
        public void DoublePress_Play_StartsOnce()
        {
            session.HoldConnect = new System.Threading.Tasks.TaskCompletionSource<bool>();
            model.PlaySolo();
            model.PlaySolo();
            model.OpenWorlds(WorldKind.Solo);
            model.PlaySelected();

            Assert.That(session.Calls, Is.EqualTo(new[] { "StartLocal(false)" }));
            Assert.That(worlds.Created, Has.Count.EqualTo(1), "a second press makes no second world");
            Assert.That(model.State, Is.EqualTo(FrontDoorState.Connecting));
        }

        [Test]
        public void DoublePress_Host_StartsOnce()
        {
            session.HoldConnect = new System.Threading.Tasks.TaskCompletionSource<bool>();
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            model.HostSelected();

            Assert.That(session.Calls, Is.EqualTo(new[] { "HostOnline" }));
        }

        private void ReachWaiting()
        {
            model.OpenJoin();
            model.JoinCodeInput = "ABC123";
            model.Join();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.WaitingForHost), "setup: the guest waits");
        }

        private void ReachHostLobby()
        {
            model.OpenWorlds(WorldKind.Multiplayer);
            model.HostSelected();
            Assert.That(model.State, Is.EqualTo(FrontDoorState.HostLobby), "setup: the lobby is up");
        }
    }
}
