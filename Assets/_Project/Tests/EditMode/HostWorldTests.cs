// Authority: none (EditMode tests). The host's world gate (Runtime/Net/HostWorld) and the roster it reads (HostRoster): where "a multiplayer
// session needs 2 or more recorded players connected to start" lives.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.EditMode
{
    public class HostWorldTests
    {
        static PlayerKey K(string id) => new PlayerKey(IdentityKind.Local, id);

        static ApprovedJoin Join(ulong clientId, string id, byte slot) => new ApprovedJoin(clientId, K(id), id, slot, false);

        static HostRoster Roster(params string[] ids)
        {
            var roster = new HostRoster();
            for (int i = 0; i < ids.Length; i++) roster.Connect(Join((ulong)i, ids[i], (byte)i));
            return roster;
        }

        // Catches: a multiplayer session starting with one player (the 2026-10-29 ruling), or creating the world before it may start.
        [Test]
        public void Multiplayer_FirstStart_NeedsTwoConnectedPlayers_AndCreatesTheWorld()
        {
            var world = new HostWorld { PendingKind = WorldKind.Multiplayer };
            Assert.That(world.TryStartSession(Roster("host")), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(world.Players, Is.Null, "a refused start creates nothing");
            Assert.That(world.Started, Is.False);

            int changed = 0;
            world.Changed += () => changed++;
            Assert.That(world.TryStartSession(Roster("host", "friend")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Players.Kind, Is.EqualTo(WorldKind.Multiplayer));
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(world.Started, Is.True);
            Assert.That(changed, Is.EqualTo(1), "the new world is persisted");
        }

        [Test]
        public void Solo_Start_CreatesASoloWorldWithTheHost()
        {
            var world = new HostWorld { PendingKind = WorldKind.Solo };
            Assert.That(world.TryStartSession(Roster("host", "friend")), Is.EqualTo(WorldPlayerVerdict.WrongPlayerCount));
            Assert.That(world.TryStartSession(Roster("host")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Players.Kind, Is.EqualTo(WorldKind.Solo));
        }

        // Catches: a later session of an existing multiplayer world starting with one recorded player, or refusing a 2-of-4 subset.
        [Test]
        public void ExistingMultiplayerWorld_EverySessionNeedsTwoRecordedPlayers()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b"), K("c"), K("d") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            Assert.That(world.TryStartSession(Roster("c")), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(world.TryStartSession(Roster("b", "d")), Is.EqualTo(WorldPlayerVerdict.Ok), "2 of 4");
            // A stranger here with the four places taken: the gate would have to record them and cannot (audit A2; with a free place they would
            // simply become one of the world's players).
            Assert.That(world.TryStartSession(Roster("a", "z")), Is.EqualTo(WorldPlayerVerdict.WorldFull));
            Assert.That(world.Players.IsRecorded(K("z")), Is.False);
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(4));
            Assert.That(world.ScalePlayers(Roster("b", "d")), Is.EqualTo(2));
            Assert.That(world.ScalePlayers(Roster("b", "d", "z")), Is.EqualTo(2), "a stranger does not scale the difficulty");
        }

        [Test]
        public void ScalePlayers_IsAtLeastOne_EvenBeforeTheWorldExists()
        {
            Assert.That(new HostWorld().ScalePlayers(new HostRoster()), Is.EqualTo(1));
        }

        // Catches: Admit or a connection coming up writing into the world (only playing it does: the start gate, or a hero spawn in a running
        // session; user decision 2026-11-13, audit A2), and the Changed event firing for a player who was already recorded.
        [Test]
        public void Admit_AndAConnection_WriteNothing_TheStartGateRecordsOnce()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            int changed = 0;
            world.Changed += () => changed++;

            Assert.That(world.Admit(K("c"), 3, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Admit(K("a"), 1, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(changed, Is.EqualTo(0));

            world.NoteConnected(K("a"), 1);
            world.NoteConnected(K("c"), 3, "Cleo");   // the lobby connection: the name is kept, the record is not written
            Assert.That(world.Players.IsRecorded(K("c")), Is.False, "a connection is not playing the world");
            Assert.That(changed, Is.EqualTo(0));

            Assert.That(world.TryStartSession(Roster("a", "c")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Players.IsRecorded(K("c")), Is.True, "the start records who is present");
            Assert.That(world.Players.NameOf(K("c")), Is.EqualTo("Cleo"), "the display name their connection brought goes with the record");
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(3));
            Assert.That(changed, Is.EqualTo(1), "one write, one save");

            world.RecordHeroSpawned(K("c"), 3);
            Assert.That(changed, Is.EqualTo(1), "already recorded");
        }

        // Catches (audit A2): a player who joins a session that is already running never being recorded, so the world forgets who played it.
        [Test]
        public void AJoinerIntoARunningSession_IsRecordedWhenTheirHeroSpawns()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            Assert.That(world.TryStartSession(Roster("a", "b")), Is.EqualTo(WorldPlayerVerdict.Ok));

            Assert.That(world.Admit(K("c"), 3, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            world.NoteConnected(K("c"), 3, "Cleo");
            Assert.That(world.Players.IsRecorded(K("c")), Is.False, "connected is not playing");

            int changed = 0;
            world.Changed += () => changed++;
            world.RecordHeroSpawned(K("c"), 3);
            Assert.That(world.Players.IsRecorded(K("c")), Is.True, "their hero is in the world: they played it");
            Assert.That(world.Players.NameOf(K("c")), Is.EqualTo("Cleo"));
            Assert.That(changed, Is.EqualTo(1));

            // A hero that spawns before any start (no session yet) records nobody.
            var fresh = new HostWorld();
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b") }, out WorldPlayers other);
            fresh.Load(other);
            fresh.RecordHeroSpawned(K("c"), 3);
            Assert.That(fresh.Players.IsRecorded(K("c")), Is.False);
        }

        // Catches (audit A2): a start that is refused spending one of the world's four places anyway (the gate records the group, so it must
        // write nothing until it knows the whole group fits).
        [Test]
        public void ARefusedStart_RecordsNobody()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b"), K("c") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            int changed = 0;
            world.Changed += () => changed++;

            Assert.That(world.TryStartSession(Roster("a", "d", "e")), Is.EqualTo(WorldPlayerVerdict.WorldFull), "three recorded plus two newcomers is five");
            Assert.That(WorldStartText.ToastKey(WorldPlayerVerdict.WorldFull), Is.EqualTo("world.world_full"), "and the host is told so");
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(3), "neither newcomer was written down");
            Assert.That(world.Started, Is.False);
            Assert.That(changed, Is.EqualTo(0));

            Assert.That(world.TryStartSession(Roster("d")), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers), "one player cannot start a multiplayer world");
            Assert.That(world.Players.IsRecorded(K("d")), Is.False);

            Assert.That(world.TryStartSession(Roster("a", "d")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(4));
        }

        [Test]
        public void SoloWorld_LostItsOwnerRecord_IsClaimedByTheFirstHost()
        {
            WorldPlayers.TryRestore(new WorldPlayersSave { Kind = 0, Recorded = new List<WorldPlayerSave>() }, out WorldPlayers players, out _);
            var world = new HostWorld();
            world.Load(players);
            int changed = 0;
            world.Changed += () => changed++;
            Assert.That(world.Admit(K("me"), 0, true), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Admit(K("you"), 2, false), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed));
            world.RecordEntered(K("me"), 0);
            Assert.That(world.Players.IsRecorded(K("me")), Is.True);
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void ReasonFor_MapsRefusalsToEndReasons()
        {
            Assert.That(HostWorld.ReasonFor(WorldPlayerVerdict.SoloWorldClosed), Is.EqualTo(EndReason.JoinsClosed));
            Assert.That(HostWorld.ReasonFor(WorldPlayerVerdict.WorldFull), Is.EqualTo(EndReason.FullLobby));
            Assert.That(HostWorld.ReasonFor(WorldPlayerVerdict.WrongIdentityKind), Is.EqualTo(EndReason.WrongWorld));
            Assert.That(HostWorld.ReasonFor(WorldPlayerVerdict.InvalidPlayer), Is.EqualTo(EndReason.Unknown));
        }

        // Catches (risk review): a Solo world locking its own player out after a profile or identity reset ("solo ownership follows the file").
        [Test]
        public void SoloWorld_WithAChangedHostKey_RebindsTheOwner_AndStillStarts()
        {
            WorldPlayers.TryCreate(WorldKind.Solo, new[] { K("old") }, out WorldPlayers solo);
            solo.SetName(K("old"), "Mira");
            var world = new HostWorld();
            world.Load(solo);
            int changed = 0;
            world.Changed += () => changed++;
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("owner key did not match"));

            Assert.That(world.Admit(K("new"), 0, true), Is.EqualTo(WorldPlayerVerdict.Ok), "the host is never refused");
            Assert.That(world.Players.IsRecorded(K("new")), Is.True);
            Assert.That(world.Players.IsRecorded(K("old")), Is.False);
            Assert.That(world.Players.NameOf(K("new")), Is.EqualTo("Mira"), "the owner keeps their name");
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(world.TryStartSession(Roster("new")), Is.EqualTo(WorldPlayerVerdict.Ok));
        }

        // Catches: the host being refused at approval on a Multiplayer world it is not recorded in. It is admitted; the start gate then says why not.
        [Test]
        public void MultiplayerWorld_HostThatIsNotRecorded_IsAdmitted_AndTheGateSaysWhy()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b"), K("c"), K("d") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);

            Assert.That(world.Admit(K("stranger"), 0, true), Is.EqualTo(WorldPlayerVerdict.Ok), "never refused at approval");
            world.NoteConnected(K("stranger"), 0);
            Assert.That(world.Players.IsRecorded(K("stranger")), Is.False, "a connection records nobody");
            world.RecordEntered(K("stranger"), 0);
            Assert.That(world.Players.IsRecorded(K("stranger")), Is.False, "a full world cannot take it");
            Assert.That(world.TryStartSession(Roster("stranger", "a")), Is.EqualTo(WorldPlayerVerdict.WorldFull), "the gate would have to record the host, and the four places are taken");
            Assert.That(WorldStartText.ToastKey(WorldPlayerVerdict.WorldFull), Is.EqualTo("world.world_full"));
            Assert.That(world.Started, Is.False);
        }

        // Catches: an approved newcomer who drops before connecting keeping a place when another part of the stack already forgot the
        // approval: the place is keyed by client id.
        [Test]
        public void AnApprovedNewcomerWhoDrops_FreesThePlace_ByClientId()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b"), K("c") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            Assert.That(world.Admit(K("d"), 4, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Admit(K("e"), 5, false), Is.EqualTo(WorldPlayerVerdict.WorldFull));
            world.CancelPending(4);
            Assert.That(world.Admit(K("e"), 5, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Admit(K("e"), 6, false), Is.EqualTo(WorldPlayerVerdict.Ok), "the same key again just moves its pending place to the new client");
        }

        // Catches (audit A2): a joiner who connects to a multiplayer lobby and leaves before Start being written into the world for good and
        // taking one of its four places.
        // NOT covered: the real ConnectionApprover/NGO path (this drives Admit, NoteConnected and CancelPending in the order the approver does).
        [Test]
        public void JoinerWhoLeavesBeforeStart_IsNotRecorded()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("host"), K("a") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            HostRoster roster = Roster("host", "a");   // host and A are connected, so the gate could start later

            Assert.That(world.Admit(K("c"), 7, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            world.NoteConnected(K("c"), 7, "C");   // the lobby connection comes up
            world.CancelPending(7);                // C leaves before TryStartSession (ClientDisconnected)

            Assert.That(world.Players.IsRecorded(K("c")), Is.False, "a lobby visit must not take one of the world's places");
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(world.TryStartSession(roster), Is.EqualTo(WorldPlayerVerdict.Ok));
        }

        // Catches (audit A2, risk review): the first lobby of a brand-new world recording every visitor, because WorldLibrary.Create writes
        // an empty players list and the start gate then takes the CanStart branch, not TryCreate.
        // NOT covered: the WorldSaveFlow.Open wiring (the save is built by hand in WorldLibrary.Create's shape).
        [Test]
        public void FirstLobbyVisitor_OfANewWorld_IsNotRecorded()
        {
            // What WorldLibrary.Create writes for a new multiplayer world.
            var fresh = new WorldPlayersSave { Kind = (int)WorldKind.Multiplayer, Identity = (int)IdentityKind.Local, Recorded = new List<WorldPlayerSave>() };
            Assert.That(WorldPlayers.TryRestore(fresh, out WorldPlayers players, out _), Is.True);
            var world = new HostWorld();
            world.Load(players);
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(0), "setup: a new world records nobody");

            Assert.That(world.Admit(K("visitor"), 4, false), Is.EqualTo(WorldPlayerVerdict.Ok));
            world.NoteConnected(K("visitor"), 4, "Visitor");
            world.CancelPending(4);

            Assert.That(world.Players.IsRecorded(K("visitor")), Is.False, "a lobby visit must not record anyone before a start");
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(0));

            // The host and one friend then start it: the two who play are recorded, with their names.
            world.NoteConnected(K("host"), 0, "Hollis");
            world.NoteConnected(K("friend"), 1, "Fen");
            Assert.That(world.TryStartSession(Roster("host", "friend")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(world.Players.NameOf(K("friend")), Is.EqualTo("Fen"));
            Assert.That(world.Players.IsRecorded(K("visitor")), Is.False, "the visitor who left is still nobody");
        }

        // The 2-player rule is a START rule (user ruling 2026-10-30): a session that dropped to one player carries on and scales down.
        [Test]
        public void TheTwoPlayerRule_AppliesAtStartOnly()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b") }, out WorldPlayers players);
            var world = new HostWorld();
            world.Load(players);
            HostRoster roster = Roster("a", "b");
            Assert.That(world.TryStartSession(roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            roster.Disconnect(1);   // b leaves mid-session
            Assert.That(world.Started, Is.True, "the session is not ended");
            Assert.That(world.ScalePlayers(roster), Is.EqualTo(1), "difficulty scales down to the one present");
            Assert.That(world.TryStartSession(roster), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers), "a fresh start would be refused, which is why SceneFlow never asks twice");
        }

        // ---- HostRoster ----

        // Catches: a rejoin not reclaiming its slot, a disconnected player counted as connected, a takeover leaving two entries on one slot.
        [Test]
        public void HostRoster_KeepsSlots_ForRejoin()
        {
            HostRoster roster = Roster("a", "b");
            roster.Disconnect(0);
            Assert.That(roster.ConnectedCount, Is.EqualTo(1));
            var keys = new List<PlayerKey>();
            roster.GetConnectedKeys(keys);
            CollectionAssert.AreEqual(new[] { K("b") }, keys);

            roster.Connect(new ApprovedJoin(9, K("a"), "a", 0, true));   // a's rejoin takes slot 0 back under a new client id
            roster.GetConnectedKeys(keys);
            CollectionAssert.AreEqual(new[] { K("a"), K("b") }, keys, "in slot order");
            var entries = new List<RosterEntry>();
            roster.GetRoster(entries);
            Assert.That(entries.Count, Is.EqualTo(2));

            Assert.That(roster.TryGetKey(9, out PlayerKey key), Is.True);
            Assert.That(key, Is.EqualTo(K("a")));
            Assert.That(roster.TryGetKey(0, out _), Is.False, "the old client id is gone");

            Assert.That(roster.TryGetSlot(9, out byte slot) && slot == 0, Is.True, "a rejoin keeps its slot");
            Assert.That(roster.TryFindConnected(K("b"), out ulong bClient) && bClient == 1, Is.True);
            Assert.That(roster.TryFindConnected(K("zzz"), out _), Is.False);

            roster.Clear();
            Assert.That(roster.ConnectedCount, Is.EqualTo(0));
        }

        // Catches: the lobby listing a left player, names out of slot order, or a blank name showing as nothing (PL-3 host lobby).
        [Test]
        public void HostRoster_GetConnectedNames_SlotOrder_ConnectedOnly_BlankBecomesPlayerN()
        {
            var roster = new HostRoster();
            roster.Connect(new ApprovedJoin(5, K("c"), "Cy", 2, false));
            roster.Connect(new ApprovedJoin(6, K("a"), "Ann", 0, false));
            roster.Connect(new ApprovedJoin(7, K("b"), "  ", 1, false));
            roster.Connect(new ApprovedJoin(8, K("d"), "Dee", 3, false));
            roster.Disconnect(8);

            var names = new List<string> { "stale" };
            roster.GetConnectedNames(names);

            CollectionAssert.AreEqual(new[] { "Ann", "Player 2", "Cy" }, names);
        }
    }
}
