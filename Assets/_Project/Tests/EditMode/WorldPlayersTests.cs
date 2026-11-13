// Tests/EditMode/WorldPlayersTests.cs: the world-player rules (Core/World/WorldPlayers; user decision 2026-10-29). Pure Core.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class WorldPlayersTests
    {
        static PlayerKey K(string id) => new PlayerKey(IdentityKind.Local, id);

        static WorldPlayers Multi(params string[] ids)
        {
            var keys = new List<PlayerKey>();
            foreach (string id in ids) keys.Add(K(id));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, keys, out WorldPlayers w), Is.EqualTo(WorldPlayerVerdict.Ok));
            return w;
        }

        static WorldPlayers Solo(string id)
        {
            Assert.That(WorldPlayers.TryCreate(WorldKind.Solo, new[] { K(id) }, out WorldPlayers w), Is.EqualTo(WorldPlayerVerdict.Ok));
            return w;
        }

        // Joining as the approver does it (CanJoin) and as the session does it once the client is in (Record).
        static WorldPlayerVerdict Join(WorldPlayers w, PlayerKey key)
        {
            WorldPlayerVerdict can = w.CanJoin(key);
            return can != WorldPlayerVerdict.Ok ? can : w.Record(key);
        }

        // Catches: a multiplayer world created with one player (solo play has its own Solo world), or created with nobody.
        [Test]
        public void Multiplayer_CreatedWithOnePlayer_IsRefused()
        {
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a") }, out WorldPlayers w), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(w, Is.Null);
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new PlayerKey[0], out w), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, null, out w), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
        }

        [Test]
        public void Multiplayer_CreatedWithTwoToFour_RecordsThemInOrder()
        {
            for (int n = 2; n <= 4; n++)
            {
                var ids = new List<string>();
                for (int i = 0; i < n; i++) ids.Add("p" + i);
                WorldPlayers w = Multi(ids.ToArray());
                Assert.That(w.Kind, Is.EqualTo(WorldKind.Multiplayer));
                Assert.That(w.Recorded.Count, Is.EqualTo(n));
                for (int i = 0; i < n; i++) Assert.That(w.Recorded[i], Is.EqualTo(K("p" + i)), "order");
            }
        }

        [Test]
        public void Multiplayer_CreatedWithFive_IsRefused()
        {
            var five = new[] { K("a"), K("b"), K("c"), K("d"), K("e") };
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, five, out WorldPlayers w), Is.EqualTo(WorldPlayerVerdict.TooManyPlayers));
            Assert.That(w, Is.Null);
        }

        // Catches: a repeated key counting as two players (one person at two machines would satisfy "2 or more"); an invalid key recorded.
        [Test]
        public void Create_DuplicateOrInvalidKey_IsRefused()
        {
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("a") }, out _), Is.EqualTo(WorldPlayerVerdict.DuplicatePlayer));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("") }, out _), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K(new string('x', PlayerKey.MaxIdLength + 1)) }, out _), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), new PlayerKey((IdentityKind)9, "b") }, out _), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(WorldPlayers.TryCreate((WorldKind)7, new[] { K("a") }, out _), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
        }

        // Catches: the 5th unique id getting in, a 4th being refused, and a refused id leaving a trace in the record.
        [Test]
        public void FifthUniquePlayer_IsRefused_AndNotRecorded()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(Join(w, K("c")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(Join(w, K("d")), Is.EqualTo(WorldPlayerVerdict.Ok), "the 4th is welcome");
            Assert.That(w.Recorded.Count, Is.EqualTo(4));

            Assert.That(w.CanJoin(K("e")), Is.EqualTo(WorldPlayerVerdict.WorldFull));
            Assert.That(w.Record(K("e")), Is.EqualTo(WorldPlayerVerdict.WorldFull));
            Assert.That(w.Recorded.Count, Is.EqualTo(4));
            Assert.That(w.IsRecorded(K("e")), Is.False);
        }

        // Catches: CanJoin writing the key down (an approved client that never arrives would hold a place for good).
        [Test]
        public void CanJoin_IsPure_RecordIsTheWrite()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.CanJoin(K("c")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.CanJoin(K("c")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.Recorded.Count, Is.EqualTo(2), "CanJoin recorded nothing");
            Assert.That(w.Record(K("c")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.Recorded.Count, Is.EqualTo(3));
            Assert.That(w.Record(K("c")), Is.EqualTo(WorldPlayerVerdict.Ok), "recording twice is a no-op");
            Assert.That(w.Recorded.Count, Is.EqualTo(3));
        }

        // Catches: a recorded player counted as "new" (refused at 4) or recorded twice.
        [Test]
        public void RecordedPlayer_CanRejoin_AtAnyTime()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            Assert.That(Join(w, K("b")), Is.EqualTo(WorldPlayerVerdict.Ok), "full world, recorded player");
            Assert.That(w.Recorded.Count, Is.EqualTo(4), "not recorded twice");

            WorldPlayers two = Multi("a", "b");
            Assert.That(Join(two, K("a")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(two.Recorded.Count, Is.EqualTo(2));
        }

        // One identity kind per world: a Local world takes no Ugs keys and an Ugs world no Local ones, even for a recorded id.
        [Test]
        public void OneIdentityKindPerWorld()
        {
            WorldPlayers local = Multi("a", "b");
            Assert.That(local.Identity, Is.EqualTo(IdentityKind.Local));
            Assert.That(local.CanJoin(new PlayerKey(IdentityKind.Ugs, "a")), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind));
            Assert.That(local.Record(new PlayerKey(IdentityKind.Ugs, "z")), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind));
            Assert.That(local.Recorded.Count, Is.EqualTo(2));

            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { new PlayerKey(IdentityKind.Ugs, "a"), new PlayerKey(IdentityKind.Ugs, "b") }, out WorldPlayers online), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(online.Identity, Is.EqualTo(IdentityKind.Ugs));
            Assert.That(online.CanJoin(K("a")), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind));
            Assert.That(online.CanStart(new[] { new PlayerKey(IdentityKind.Ugs, "a"), K("b") }), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind));

            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), new PlayerKey(IdentityKind.Ugs, "b") }, out _), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind), "a mixed group cannot create a world");
        }

        [Test]
        public void Join_InvalidKey_IsRefused()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.CanJoin(K("")), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(w.CanJoin(default), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(w.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: a subset start refused, and a one-player start of a multiplayer world allowed (the 2026-10-29 ruling).
        [Test]
        public void Start_AnySubsetOfTwoOrMoreRecordedPlayers_Works_OneDoesNot()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            Assert.That(w.CanStart(new[] { K("b"), K("d") }), Is.EqualTo(WorldPlayerVerdict.Ok), "2 of 4");
            Assert.That(w.CanStart(new[] { K("a"), K("c"), K("d") }), Is.EqualTo(WorldPlayerVerdict.Ok), "3 of 4");
            Assert.That(w.CanStart(new[] { K("a"), K("b"), K("c"), K("d") }), Is.EqualTo(WorldPlayerVerdict.Ok), "all 4");
            Assert.That(w.CanStart(new[] { K("c") }), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers), "1 of 4");
            Assert.That(w.CanStart(new PlayerKey[0]), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(w.CanStart(null), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
        }

        // Catches: an unrecorded player starting a session without being recorded; one person twice counting as two.
        [Test]
        public void Start_UnrecordedOrRepeatedPlayer_IsRefused()
        {
            WorldPlayers w = Multi("a", "b", "c");
            Assert.That(w.CanStart(new[] { K("a"), K("z") }), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
            Assert.That(w.CanStart(new[] { K("a"), K("a") }), Is.EqualTo(WorldPlayerVerdict.DuplicatePlayer));
            Assert.That(w.Recorded.Count, Is.EqualTo(3), "CanStart never records");
        }

        // Catches: a solo world admitting anyone (even its owner through the join path), or starting without exactly its owner.
        [Test]
        public void SoloWorld_RefusesEveryJoin_AndStartsWithItsOwnerOnly()
        {
            WorldPlayers w = Solo("owner");
            Assert.That(w.Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(w.CanJoin(K("friend")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed));
            Assert.That(w.CanJoin(K("owner")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed), "even the owner does not 'join' their own solo world");
            Assert.That(w.Record(K("friend")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed));
            Assert.That(w.Recorded.Count, Is.EqualTo(1));

            Assert.That(w.CanStart(new[] { K("owner") }), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.CanStart(new[] { K("friend") }), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
            Assert.That(w.CanStart(new[] { K("owner"), K("friend") }), Is.Not.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.CanStart(new PlayerKey[0]), Is.EqualTo(WorldPlayerVerdict.WrongPlayerCount));
        }

        [Test]
        public void SoloWorld_CreatedWithZeroOrTwoPlayers_IsRefused()
        {
            Assert.That(WorldPlayers.TryCreate(WorldKind.Solo, new PlayerKey[0], out _), Is.EqualTo(WorldPlayerVerdict.WrongPlayerCount));
            Assert.That(WorldPlayers.TryCreate(WorldKind.Solo, new[] { K("a"), K("b") }, out _), Is.EqualTo(WorldPlayerVerdict.WrongPlayerCount));
        }

        // The difficulty scale input: the recorded players who are here, never below 1 or above 4.
        [Test]
        public void ScalePlayers_CountsDistinctRecordedPlayersPresent_ClampedToOneToFour()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            Assert.That(w.ScalePlayers(new[] { K("a"), K("b") }), Is.EqualTo(2));
            Assert.That(w.ScalePlayers(new[] { K("a"), K("b"), K("c"), K("d") }), Is.EqualTo(4));
            Assert.That(w.ScalePlayers(new[] { K("a"), K("a"), K("z") }), Is.EqualTo(1), "a repeat and a stranger do not count");
            Assert.That(w.ScalePlayers(null), Is.EqualTo(1), "never 0");
            Assert.That(w.ScalePlayers(new PlayerKey[0]), Is.EqualTo(1));
            Assert.That(Solo("o").ScalePlayers(new[] { K("o") }), Is.EqualTo(1));
        }

        [Test]
        public void ToSave_ThenTryFromSave_RoundTrips()
        {
            WorldPlayers w = Multi("a", "b", "c");
            Assert.That(WorldPlayers.TryFromSave(w.ToSave(), out WorldPlayers back), Is.True);
            Assert.That(back.Kind, Is.EqualTo(WorldKind.Multiplayer));
            Assert.That(back.Identity, Is.EqualTo(IdentityKind.Local));
            CollectionAssert.AreEqual(w.Recorded, back.Recorded);

            WorldPlayers solo = Solo("owner");
            Assert.That(WorldPlayers.TryFromSave(solo.ToSave(), out WorldPlayers soloBack), Is.True);
            Assert.That(soloBack.Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(soloBack.CanJoin(K("x")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed), "the loaded solo world still refuses joins");
        }

        // Catches: damage to the list turning the world into something else. TryRestore keeps the kind however many records survive.
        [Test]
        public void TryRestore_KeepsTheKind_WhateverRecordsSurvive()
        {
            var one = new WorldPlayersSave { Kind = 1, Identity = 0, Recorded = new List<WorldPlayerSave> { new WorldPlayerSave { Identity = 0, Id = "a" } } };
            Assert.That(WorldPlayers.TryRestore(one, out WorldPlayers w, out bool dropped), Is.True);
            Assert.That(dropped, Is.False);
            Assert.That(w.Kind, Is.EqualTo(WorldKind.Multiplayer), "one survivor is still a Multiplayer world");
            Assert.That(w.CanStart(new[] { K("a") }), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers), "and still needs two to start");
            Assert.That(Join(w, K("b")), Is.EqualTo(WorldPlayerVerdict.Ok), "and takes new players");
            Assert.That(w.CanStart(new[] { K("a"), K("b") }), Is.EqualTo(WorldPlayerVerdict.Ok));

            var none = new WorldPlayersSave { Kind = 1, Recorded = new List<WorldPlayerSave> { null, new WorldPlayerSave { Identity = 0, Id = "" } } };
            Assert.That(WorldPlayers.TryRestore(none, out w, out dropped), Is.True);
            Assert.That(dropped, Is.True);
            Assert.That(w.Kind, Is.EqualTo(WorldKind.Multiplayer));
            Assert.That(w.Recorded.Count, Is.EqualTo(0));
            Assert.That(Join(w, new PlayerKey(IdentityKind.Ugs, "u")), Is.EqualTo(WorldPlayerVerdict.Ok), "no identity yet: the first record sets it");
            Assert.That(w.Identity, Is.EqualTo(IdentityKind.Ugs));

            var soloLost = new WorldPlayersSave { Kind = 0, Recorded = new List<WorldPlayerSave>() };
            Assert.That(WorldPlayers.TryRestore(soloLost, out w, out _), Is.True);
            Assert.That(w.Kind, Is.EqualTo(WorldKind.Solo), "a Solo world stays Solo");
            Assert.That(w.CanStart(new[] { K("claimer") }), Is.EqualTo(WorldPlayerVerdict.Ok), "the first single player can claim it");
            Assert.That(w.ClaimSoloOwner(K("claimer")), Is.True);
            Assert.That(w.CanStart(new[] { K("someoneElse") }), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
        }

        // Catches: the strict loader accepting a record a real world could not hold.
        [Test]
        public void TryFromSave_RefusesWhatNoRealWorldCouldHold()
        {
            Assert.That(WorldPlayers.TryFromSave(null, out _), Is.False);
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 5 }, out _), Is.False, "unknown kind");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 256 }, out _), Is.False, "a kind that would wrap to Solo");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 1, Recorded = new List<WorldPlayerSave> { new WorldPlayerSave { Identity = 0, Id = "a" } } }, out _), Is.False, "multiplayer with one");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 0, Recorded = new List<WorldPlayerSave>() }, out _), Is.False, "solo with none");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 1, Recorded = new List<WorldPlayerSave> { null, new WorldPlayerSave { Identity = 0, Id = "a" } } }, out _), Is.False, "null entry");
        }

        // Catches: a newer build's members inside the players section being dropped when this build writes the section back.
        [Test]
        public void UnknownMembers_SurviveTheRoundTrip()
        {
            var save = new WorldPlayersSave
            {
                Kind = 1,
                Recorded = new List<WorldPlayerSave>
                {
                    new WorldPlayerSave { Identity = 0, Id = "a", Extra = new Dictionary<string, object> { { "colour", "red" } } },
                    new WorldPlayerSave { Identity = 0, Id = "b" },
                },
                Extra = new Dictionary<string, object> { { "futureField", 7 } },
            };
            Assert.That(WorldPlayers.TryRestore(save, out WorldPlayers w, out _), Is.True);
            Join(w, K("c"));
            WorldPlayersSave back = w.ToSave();
            Assert.That(back.Extra["futureField"], Is.EqualTo(7));
            Assert.That(back.Recorded[0].Extra["colour"], Is.EqualTo("red"));
            Assert.That(back.Recorded.Count, Is.EqualTo(3));
        }
    
        // Catches: a player's name not being kept with their record (the picker would show nothing), or surviving a Solo owner rebind wrongly.
        [Test]
        public void Names_AreKept_WithTheRecord()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.SetName(K("a"), "Ann"), Is.True);
            Assert.That(w.SetName(K("a"), "Ann"), Is.False, "unchanged");
            Assert.That(w.SetName(K("zzz"), "Nobody"), Is.False, "only recorded players have names");
            Assert.That(w.NameOf(K("a")), Is.EqualTo("Ann"));
            Assert.That(w.NameOf(K("b")), Is.EqualTo(""));
            Assert.That(WorldPlayers.TryRestore(w.ToSave(), out WorldPlayers back, out _), Is.True);
            Assert.That(back.NameOf(K("a")), Is.EqualTo("Ann"));
        }

        [Test]
        public void RebindSoloOwner_ReplacesTheOwner_OnlyOnASoloWorld()
        {
            WorldPlayers solo = Solo("old");
            solo.SetName(K("old"), "Mira");
            Assert.That(solo.RebindSoloOwner(K("old")), Is.False, "already the owner");
            Assert.That(solo.RebindSoloOwner(new PlayerKey(IdentityKind.Ugs, "new")), Is.True);
            Assert.That(solo.Recorded.Count, Is.EqualTo(1));
            Assert.That(solo.Recorded[0], Is.EqualTo(new PlayerKey(IdentityKind.Ugs, "new")));
            Assert.That(solo.Identity, Is.EqualTo(IdentityKind.Ugs));
            Assert.That(solo.NameOf(new PlayerKey(IdentityKind.Ugs, "new")), Is.EqualTo("Mira"));
            Assert.That(Multi("a", "b").RebindSoloOwner(K("c")), Is.False);
        }

        // ---- the start gate records the group (user decision 2026-11-13, audit A2) ----

        // Catches: the start gate refusing a group because the newcomers in it are not recorded YET (nobody is recorded before a start now), or
        // letting a group start that the world cannot hold.
        [Test]
        public void CanStartWithNewPlayers_TreatsNotYetRecordedPlayersAsRecordable()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.CanStart(new[] { K("a"), K("c") }), Is.EqualTo(WorldPlayerVerdict.NotRecorded), "CanStart alone refuses a newcomer");
            Assert.That(w.CanStartWithNewPlayers(new[] { K("a"), K("c") }), Is.EqualTo(WorldPlayerVerdict.Ok), "the gate records C at this start");
            Assert.That(w.Recorded.Count, Is.EqualTo(2), "the check writes nothing");

            Assert.That(w.CanStartWithNewPlayers(new[] { K("a"), K("c"), K("d") }), Is.EqualTo(WorldPlayerVerdict.Ok), "four fit");
            Assert.That(w.CanStartWithNewPlayers(new[] { K("a"), K("c"), K("d"), K("e") }), Is.EqualTo(WorldPlayerVerdict.WorldFull), "five do not");
            Assert.That(w.CanStartWithNewPlayers(new[] { K("a") }), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
            Assert.That(w.CanStartWithNewPlayers(new[] { K("a"), K("a") }), Is.EqualTo(WorldPlayerVerdict.DuplicatePlayer));
            Assert.That(w.CanStartWithNewPlayers(new[] { K("a"), new PlayerKey(IdentityKind.Ugs, "c") }), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind));
            Assert.That(w.CanStartWithNewPlayers(null), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers));
        }

        // Catches: a Solo world whose owner record was lost refusing its own start (nothing records the owner before the gate now), or a Solo
        // world starting with someone who is not its owner.
        [Test]
        public void CanStartWithNewPlayers_SoloWorld()
        {
            WorldPlayers solo = Solo("owner");
            Assert.That(solo.CanStartWithNewPlayers(new[] { K("owner") }), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(solo.CanStartWithNewPlayers(new[] { K("other") }), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
            Assert.That(solo.CanStartWithNewPlayers(new[] { K("owner"), K("other") }), Is.EqualTo(WorldPlayerVerdict.WrongPlayerCount));

            WorldPlayers lost = Restore(new WorldPlayersSave { Kind = (int)WorldKind.Solo, Identity = -1, Recorded = new List<WorldPlayerSave>() });
            Assert.That(lost.CanStartWithNewPlayers(new[] { K("me") }), Is.EqualTo(WorldPlayerVerdict.Ok), "the first player to start claims it");
            Assert.That(lost.CanStartWithNewPlayers(new[] { K("me"), K("you") }), Is.EqualTo(WorldPlayerVerdict.WrongPlayerCount));
        }

        // Catches (audit A2): a world with no way to let go of a player, so a mistaken record keeps one of its four places for good.
        [Test]
        public void Remove_FreesOneOfTheFourPlaces_AndKeepsTheRest()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            w.SetName(K("c"), "Cleo");
            Assert.That(Join(w, K("e")), Is.EqualTo(WorldPlayerVerdict.WorldFull), "setup: the world is full");

            Assert.That(w.Remove(K("c")), Is.True);
            Assert.That(w.IsRecorded(K("c")), Is.False);
            Assert.That(w.NameOf(K("c")), Is.EqualTo(""), "their name goes with the record");
            Assert.That(w.Recorded.Count, Is.EqualTo(3));
            Assert.That(new List<PlayerKey>(w.Recorded), Is.EqualTo(new[] { K("a"), K("b"), K("d") }), "the others keep their order");
            Assert.That(Join(w, K("e")), Is.EqualTo(WorldPlayerVerdict.Ok), "the free place can be filled");

            Assert.That(w.Remove(K("c")), Is.False, "not recorded");
            Assert.That(w.Remove(default), Is.False);
            Assert.That(Solo("owner").Remove(K("owner")), Is.False, "a Solo world belongs to this machine: delete the world instead");
        }

        // Catches (audit A3): a recorded player whose UGS id changed having no way to move to the new key, so a full world locks them out for good.
        [Test]
        public void RenameRecorded_MovesOneRecord_KeepingPlaceNameAndOrder()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            w.SetName(K("b"), "Bo");

            Assert.That(w.RenameRecorded(K("b"), K("b2")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(new List<PlayerKey>(w.Recorded), Is.EqualTo(new[] { K("a"), K("b2"), K("c"), K("d") }), "same place in the order");
            Assert.That(w.IsRecorded(K("b")), Is.False);
            Assert.That(w.NameOf(K("b2")), Is.EqualTo("Bo"));
            Assert.That(w.NameOf(K("b")), Is.EqualTo(""));
            Assert.That(w.CanStart(new[] { K("a"), K("b2") }), Is.EqualTo(WorldPlayerVerdict.Ok), "the new key can play");
            Assert.That(w.CanJoin(K("b")), Is.EqualTo(WorldPlayerVerdict.WorldFull), "the old key is just a stranger now");
        }

        // Catches (audit A3): a rename that overwrites another player's record, accepts a key of another identity kind, or invents a record.
        [Test]
        public void RenameRecorded_Refusals_ChangeNothing()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.RenameRecorded(K("a"), K("b")), Is.EqualTo(WorldPlayerVerdict.DuplicatePlayer), "the new key is already recorded");
            Assert.That(w.RenameRecorded(K("a"), K("a")), Is.EqualTo(WorldPlayerVerdict.DuplicatePlayer), "renaming onto itself");
            Assert.That(w.RenameRecorded(K("a"), default), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(w.RenameRecorded(default, K("x")), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(w.RenameRecorded(K("a"), new PlayerKey(IdentityKind.Ugs, "x")), Is.EqualTo(WorldPlayerVerdict.WrongIdentityKind));
            Assert.That(w.RenameRecorded(K("nobody"), K("x")), Is.EqualTo(WorldPlayerVerdict.NotRecorded));
            Assert.That(Solo("owner").RenameRecorded(K("owner"), K("x")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed), "a Solo owner follows the machine (RebindSoloOwner)");
            Assert.That(new List<PlayerKey>(w.Recorded), Is.EqualTo(new[] { K("a"), K("b") }));
        }

        static WorldPlayers Restore(WorldPlayersSave save)
        {
            Assert.That(WorldPlayers.TryRestore(save, out WorldPlayers w, out _), Is.True);
            return w;
        }
    }
}
