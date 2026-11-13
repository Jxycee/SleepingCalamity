// Authority: none (EditMode tests of the host's approval decisions). Drives ConnectionApprover.Handle with encoded payloads against a
// HostWorld and a HostRoster: the world-player rules (Solo refuses joins, the 5th player is refused, a recorded player rejoins, new players
// are recorded only when their client connects) on top of Core JoinRules.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class ConnectionApproverTests
    {
        private const int Protocol = 1;
        private const ulong Hash = 77UL;

        private GameObject _go;
        private NetworkSessionConfig _config;
        private HostWorld _world;
        private HostRoster _roster;
        private ConnectionApprover _approver;

        static PlayerKey K(string id) => new PlayerKey(IdentityKind.Local, id);

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ApproverTest");
            NetworkManager nm = _go.AddComponent<NetworkManager>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            _world = new HostWorld();
            _roster = new HostRoster();
            var settings = new SettingsStore(Path.Combine(Path.GetTempPath(), "sc_approver_test_settings.json")); // never loaded or saved
            _config = new NetworkSessionConfig(nm, new JoinApprovalBridge(), settings, Protocol, Hash, false, _roster, _world);
            _approver = NewApprover(SessionMode.Online);
        }

        [TearDown]
        public void TearDown()
        {
            _approver?.Uninstall();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        private ConnectionApprover NewApprover(SessionMode mode)
        {
            var approver = new ConnectionApprover(_config, mode);
            approver.Install();
            return approver;
        }

        private static WorldPlayers Multi(params string[] ids)
        {
            var keys = new List<PlayerKey>();
            foreach (string id in ids) keys.Add(K(id));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, keys, out WorldPlayers w), Is.EqualTo(WorldPlayerVerdict.Ok));
            return w;
        }

        private NetworkManager.ConnectionApprovalResponse Ask(PlayerKey key, ulong clientId, int protocol = Protocol, ulong hash = Hash)
        {
            var request = new NetworkManager.ConnectionApprovalRequest
            {
                Payload = ConnectPayloadCodec.Encode(new ConnectPayload(protocol, hash, key, "P")),
                ClientNetworkId = clientId,
            };
            var response = new NetworkManager.ConnectionApprovalResponse();
            _approver.Handle(request, response);
            return response;
        }

        // The client's connection comes up: the approver's connection event moves it into the roster and the world records it.
        private void Connect(ulong clientId) => _approver.ClientConnected(clientId);

        private void AssertRefused(NetworkManager.ConnectionApprovalResponse r, EndReason reason)
        {
            Assert.That(r.Approved, Is.False);
            Assert.That(r.Reason, Is.EqualTo(reason.ToString()));
            Assert.That(r.CreatePlayerObject, Is.False);
        }

        // Catches: the 5th unique player getting in, or being refused with the wrong reason (the client shows it).
        [Test]
        public void FifthUniquePlayer_IsRefused_WithFullLobby()
        {
            _world.Load(Multi("a", "b", "c", "d"));
            AssertRefused(Ask(K("e"), 5), EndReason.FullLobby);
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(4));
            Assert.That(_world.Players.IsRecorded(K("e")), Is.False);
        }

        // Catches: a recorded player counted as new (refused at 4) or a rejoin getting a second record.
        [Test]
        public void RecordedPlayer_Rejoins_EvenWhenTheWorldIsFull()
        {
            _world.Load(Multi("a", "b", "c", "d"));
            Assert.That(Ask(K("c"), 3).Approved, Is.True);
            Connect(3);
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(4));
            Assert.That(_roster.ConnectedCount, Is.EqualTo(1));
        }

        // Catches: a Solo world admitting a client, or refusing its own owner as the host.
        [Test]
        public void SoloWorld_RefusesAClient_ButHostsItsOwner()
        {
            Assert.That(WorldPlayers.TryCreate(WorldKind.Solo, new[] { K("owner") }, out WorldPlayers solo), Is.EqualTo(WorldPlayerVerdict.Ok));
            _world.Load(solo);

            Assert.That(Ask(K("owner"), NetworkManager.ServerClientId).Approved, Is.True, "the owner starts their own world as the host");
            Connect(NetworkManager.ServerClientId);
            AssertRefused(Ask(K("friend"), 2), EndReason.JoinsClosed);
            AssertRefused(Ask(K("owner"), 3), EndReason.DuplicateIdentity);   // the owner's key as a second connection: JoinRules says so first
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(1));

            // The host is never refused: a host key that is not the recorded owner is rebound (solo ownership follows the file).
            _approver.Uninstall();
            _approver = NewApprover(SessionMode.Online);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("owner key did not match"));
            Assert.That(Ask(K("newkey"), NetworkManager.ServerClientId).Approved, Is.True);
            Assert.That(_world.Players.IsRecorded(K("newkey")), Is.True);
        }

        // Catches: an approved newcomer who drops BEFORE connecting keeping their place when the session had already forgotten the approval
        // (the real subscription order has the session's connection handler run before the approver's).
        [Test]
        public void ANewcomerWhoDropsBeforeConnecting_FreesTheirPlace_EvenIfTheBridgeEntryIsGone()
        {
            _world.Load(Multi("a", "b", "c"));
            Assert.That(Ask(K("d"), 4).Approved, Is.True);
            AssertRefused(Ask(K("e"), 5), EndReason.FullLobby);
            _config.Bridge.RemovePending(4);      // what a session handler that ran first used to do
            _approver.ClientDisconnected(4);
            Assert.That(Ask(K("e"), 5).Approved, Is.True, "the place is free");
        }

        // Catches (risk review): a player whose link dropped being refused as a duplicate of their own stale connection when they come back.
        [Test]
        public void AReconnectingKey_TakesOverItsOwnStaleConnection()
        {
            _world.Load(Multi("a", "b"));
            Assert.That(Ask(K("b"), 3).Approved, Is.True);
            Connect(3);
            Assert.That(_roster.ConnectedCount, Is.EqualTo(1));

            Assert.That(Ask(K("b"), 4).Approved, Is.True, "not DuplicateIdentity");
            Connect(4);
            Assert.That(_roster.ConnectedCount, Is.EqualTo(1), "still one player");
            Assert.That(_roster.TryGetKey(4, out PlayerKey key) && key == K("b"), Is.True);
            Assert.That(_roster.TryGetKey(3, out _), Is.False, "the stale client is gone");
            _approver.ClientDisconnected(3);   // the old connection's disconnect event arriving late must not remove the new one
            Assert.That(_roster.ConnectedCount, Is.EqualTo(1));

            // The host's own key is never taken over by a client.
            Assert.That(Ask(K("host"), NetworkManager.ServerClientId).Approved, Is.True);
            Connect(NetworkManager.ServerClientId);
            AssertRefused(Ask(K("host"), 7), EndReason.DuplicateIdentity);
        }

        // Catches: a mismatched build kicking a connected player by sending their key.
        [Test]
        public void AMismatchedBuild_CannotTakeOverAConnection()
        {
            _world.Load(Multi("a", "b"));
            Ask(K("b"), 3);
            Connect(3);
            AssertRefused(Ask(K("b"), 4, protocol: Protocol + 1), EndReason.VersionMismatch);
            Assert.That(_roster.TryGetKey(3, out _), Is.True, "the real connection stays");
        }

        // Catches: an Ugs key meeting a Local world being turned away with a reason the player cannot act on.
        [Test]
        public void AWorldOfAnotherIdentityKind_IsRefusedWithItsOwnReason()
        {
            _world.Load(Multi("a", "b"));
            AssertRefused(Ask(new PlayerKey(IdentityKind.Ugs, "online"), 2), EndReason.WrongWorld);
        }

        // Catches (audit A2): a key written into the world when it is merely approved or merely connected. A player is recorded when they PLAY
        // the world (user decision 2026-11-13): the start gate, or their hero spawning in a running session. Their place is held meanwhile, so
        // a lobby of four strangers cannot promise a fifth place, and their name is kept for the record.
        [Test]
        public void ANewPlayer_IsRecordedOnlyWhenTheSessionStarts()
        {
            _world.Load(Multi("a", "b"));
            Assert.That(Ask(K("c"), 3).Approved, Is.True);
            Assert.That(_world.Players.IsRecorded(K("c")), Is.False, "approved is not recorded");
            Connect(3);
            Assert.That(_world.Players.IsRecorded(K("c")), Is.False, "a lobby visit is not recorded");
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(_world.Admit(K("e"), 9, false), Is.EqualTo(WorldPlayerVerdict.Ok), "three recorded players and C: the fourth place is C's while they are here");
            _world.CancelPending(9);

            Assert.That(_world.TryStartSession(_roster), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers), "only C is connected in this test");
            Assert.That(_world.Players.IsRecorded(K("c")), Is.False);
            Assert.That(Ask(K("a"), 1).Approved, Is.True);
            Connect(1);
            Assert.That(_world.TryStartSession(_roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(_world.Players.IsRecorded(K("c")), Is.True, "C played the world");
            Assert.That(_world.Players.NameOf(K("c")), Is.EqualTo("P"), "the name their connection brought");
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(3));
        }

        // Catches (audit A2): a lobby visitor who leaves before Start keeping one of the world's four places for good (nothing in the game
        // could undo it), or their approved place staying taken after they left.
        [Test]
        public void AVisitorWhoLeavesBeforeTheStart_TakesNoPlace()
        {
            _world.Load(Multi("a", "b", "c"));
            Assert.That(Ask(K("d"), 4).Approved, Is.True);
            Connect(4);
            _approver.ClientDisconnected(4);   // D looked around and left
            Assert.That(_world.Players.IsRecorded(K("d")), Is.False, "a lobby visit must not take one of the four places");
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(3));

            Assert.That(Ask(K("e"), 5).Approved, Is.True, "the fourth place is still free for the friend it was meant for");
            Connect(5);
            Assert.That(Ask(K("a"), 1).Approved, Is.True);
            Connect(1);
            Assert.That(_world.TryStartSession(_roster), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(_world.Players.IsRecorded(K("e")), Is.True);
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(4));
        }

        // Catches: two joiners approved together both taking the last place (the second would then be refused after it connected).
        [Test]
        public void TwoNewPlayersApprovedTogether_CannotBothTakeTheLastPlace()
        {
            _world.Load(Multi("a", "b", "c"));
            Assert.That(Ask(K("d"), 4).Approved, Is.True);
            AssertRefused(Ask(K("e"), 5), EndReason.FullLobby);
            Connect(4);
            Assert.That(Ask(K("a"), 1).Approved, Is.True);
            Connect(1);
            Assert.That(_world.TryStartSession(_roster), Is.EqualTo(WorldPlayerVerdict.Ok), "the start records the group that is here");
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(4));
            Assert.That(_world.Players.IsRecorded(K("d")), Is.True);
        }

        // Catches: a joiner that was approved and dropped before connecting keeping its place for good.
        [Test]
        public void AnApprovedPlayerWhoNeverArrives_FreesTheirPlace()
        {
            _world.Load(Multi("a", "b", "c"));
            Assert.That(Ask(K("d"), 4).Approved, Is.True);
            _approver.ClientDisconnected(4);
            Assert.That(Ask(K("e"), 5).Approved, Is.True, "the place is free again");
        }

        // Catches: a Local-identity world admitting an Ugs key (or the other way round): they are separate worlds.
        [Test]
        public void AKeyOfAnotherIdentityKind_IsRefused()
        {
            _world.Load(Multi("a", "b"));
            AssertRefused(Ask(new PlayerKey(IdentityKind.Ugs, "a"), 2), EndReason.WrongWorld);
            Assert.That(_world.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Before the world exists nothing is recorded and the roster rules alone decide (4 at most).
        [Test]
        public void NoWorldYet_AdmitsUpToFour_AndRecordsNothing()
        {
            for (ulong i = 0; i < 4; i++)
            {
                Assert.That(Ask(K("p" + i), i).Approved, Is.True, "player " + i);
                Connect(i);
            }
            AssertRefused(Ask(K("p4"), 4), EndReason.FullLobby);
            Assert.That(_world.Players, Is.Null);
            Assert.That(_roster.ConnectedCount, Is.EqualTo(4));
        }

        [Test]
        public void VersionAndContentMismatch_AreRefusedBeforeTheWorldIsAsked()
        {
            _world.Load(Multi("a", "b"));
            AssertRefused(Ask(K("c"), 3, protocol: Protocol + 1), EndReason.VersionMismatch);
            AssertRefused(Ask(K("c"), 3, hash: Hash + 1), EndReason.VersionMismatch);
            Connect(3);
            Assert.That(_world.Players.IsRecorded(K("c")), Is.False);
        }

        [Test]
        public void ASoloModeSession_RefusesEveryClient()
        {
            _approver.Uninstall();
            _approver = NewApprover(SessionMode.Solo);
            AssertRefused(Ask(K("c"), 3), EndReason.JoinsClosed);
            Assert.That(Ask(K("host"), NetworkManager.ServerClientId).Approved, Is.True);
        }

        [Test]
        public void AnUndecodablePayload_IsRefused()
        {
            var response = new NetworkManager.ConnectionApprovalResponse();
            _approver.Handle(new NetworkManager.ConnectionApprovalRequest { Payload = new byte[] { 1, 2, 3 }, ClientNetworkId = 9 }, response);
            AssertRefused(response, EndReason.VersionMismatch);
        }

        // Catches: the roster not following connections (a rejoin could not reclaim its slot) and Uninstall leaving state for the next session.
        [Test]
        public void TheRoster_FollowsConnections_AndClearsOnUninstall()
        {
            Assert.That(Ask(K("a"), 1).Approved, Is.True);
            Connect(1);
            Assert.That(_roster.ConnectedCount, Is.EqualTo(1));
            Assert.That(_config.Bridge.Provider, Is.SameAs(_roster));
            _approver.ClientDisconnected(1);
            Assert.That(_roster.ConnectedCount, Is.EqualTo(0));
            var entries = new List<RosterEntry>();
            _roster.GetRoster(entries);
            Assert.That(entries.Count, Is.EqualTo(1), "the slot is kept for a rejoin");
            Assert.That(entries[0].Connected, Is.False);

            _approver.Uninstall();
            Assert.That(_config.Bridge.Provider, Is.Null);
            _roster.GetRoster(entries);
            Assert.That(entries.Count, Is.EqualTo(0));
        }
    }
}
