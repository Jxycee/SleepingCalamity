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
            Assert.That(world.TryStartSession(Roster("a", "z")), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
            Assert.That(world.ScalePlayers(Roster("b", "d")), Is.EqualTo(2));
            Assert.That(world.ScalePlayers(Roster("b", "d", "z")), Is.EqualTo(2), "a stranger does not scale the difficulty");
        }

        [Test]
        public void ScalePlayers_IsAtLeastOne_EvenBeforeTheWorldExists()
        {
            Assert.That(new HostWorld().ScalePlayers(new HostRoster()), Is.EqualTo(1));
        }

        // Catches: Admit writing into the world (only RecordEntered does), and the Changed event firing for a player who was already recorded.
        [Test]
        public void Admit_WritesNothing_RecordEnteredPersistsOnce()
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

            world.RecordEntered(K("a"), 1);
            Assert.That(changed, Is.EqualTo(0), "already recorded");
            world.RecordEntered(K("c"), 3, "Cleo");
            Assert.That(world.Players.NameOf(K("c")), Is.EqualTo("Cleo"), "the display name is kept with the record");
            Assert.That(world.Players.Recorded.Count, Is.EqualTo(3));
            Assert.That(changed, Is.EqualTo(1));
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
            world.RecordEntered(K("stranger"), 0);
            Assert.That(world.Players.IsRecorded(K("stranger")), Is.False, "a full world cannot take it");
            Assert.That(world.TryStartSession(Roster("stranger", "a")), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
            Assert.That(WorldStartText.ToastKey(WorldPlayerVerdict.NotRecorded), Is.EqualTo("world.not_a_player"));
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
    }
}
