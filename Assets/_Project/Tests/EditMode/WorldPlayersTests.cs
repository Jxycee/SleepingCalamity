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
            Assert.That(w.TryJoin(K("c")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.TryJoin(K("d")), Is.EqualTo(WorldPlayerVerdict.Ok), "the 4th is welcome");
            Assert.That(w.Recorded.Count, Is.EqualTo(4));

            Assert.That(w.TryJoin(K("e")), Is.EqualTo(WorldPlayerVerdict.WorldFull));
            Assert.That(w.Recorded.Count, Is.EqualTo(4));
            Assert.That(w.IsRecorded(K("e")), Is.False);
        }

        // Catches: a recorded player counted as "new" (refused at 4) or recorded twice.
        [Test]
        public void RecordedPlayer_CanRejoin_AtAnyTime()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            Assert.That(w.TryJoin(K("b")), Is.EqualTo(WorldPlayerVerdict.Ok), "full world, recorded player");
            Assert.That(w.Recorded.Count, Is.EqualTo(4), "not recorded twice");

            WorldPlayers two = Multi("a", "b");
            Assert.That(two.TryJoin(K("a")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(two.Recorded.Count, Is.EqualTo(2));
        }

        [Test]
        public void Join_SameIdUnderAnotherIdentityKind_IsADifferentPlayer()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.TryJoin(new PlayerKey(IdentityKind.Ugs, "a")), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(w.Recorded.Count, Is.EqualTo(3));
        }

        [Test]
        public void Join_InvalidKey_IsRefused()
        {
            WorldPlayers w = Multi("a", "b");
            Assert.That(w.TryJoin(K("")), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
            Assert.That(w.TryJoin(default), Is.EqualTo(WorldPlayerVerdict.InvalidPlayer));
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

        // Catches: an unrecorded player starting a session without being admitted; one person twice counting as two.
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
            Assert.That(w.TryJoin(K("friend")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed));
            Assert.That(w.TryJoin(K("owner")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed), "even the owner does not 'join' their own solo world");
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

        // The difficulty scale input: the recorded players who are here.
        [Test]
        public void ScalePlayers_CountsDistinctRecordedPlayersPresent()
        {
            WorldPlayers w = Multi("a", "b", "c", "d");
            Assert.That(w.ScalePlayers(new[] { K("a"), K("b") }), Is.EqualTo(2));
            Assert.That(w.ScalePlayers(new[] { K("a"), K("b"), K("c"), K("d") }), Is.EqualTo(4));
            Assert.That(w.ScalePlayers(new[] { K("a"), K("a"), K("z") }), Is.EqualTo(1), "a repeat and a stranger do not count");
            Assert.That(w.ScalePlayers(null), Is.EqualTo(0));
            Assert.That(Solo("o").ScalePlayers(new[] { K("o") }), Is.EqualTo(1));
        }

        [Test]
        public void ToSave_ThenTryFromSave_RoundTrips()
        {
            WorldPlayers w = Multi("a", "b", "c");
            Assert.That(WorldPlayers.TryFromSave(w.ToSave(), out WorldPlayers back), Is.True);
            Assert.That(back.Kind, Is.EqualTo(WorldKind.Multiplayer));
            CollectionAssert.AreEqual(w.Recorded, back.Recorded);

            WorldPlayers solo = Solo("owner");
            Assert.That(WorldPlayers.TryFromSave(solo.ToSave(), out WorldPlayers soloBack), Is.True);
            Assert.That(soloBack.Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(soloBack.TryJoin(K("x")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed), "the loaded solo world still refuses joins");
        }

        // Catches: the loader accepting a record a real world could not hold (it would bypass the creation rules).
        [Test]
        public void TryFromSave_RefusesWhatNoRealWorldCouldHold()
        {
            Assert.That(WorldPlayers.TryFromSave(null, out _), Is.False);
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 5 }, out _), Is.False, "unknown kind");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 256 }, out _), Is.False, "a kind that would wrap to Solo");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 1, Recorded = new List<WorldPlayerSave> { new WorldPlayerSave { Id = "a" } } }, out _), Is.False, "multiplayer with one");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 0, Recorded = new List<WorldPlayerSave>() }, out _), Is.False, "solo with none");
            Assert.That(WorldPlayers.TryFromSave(new WorldPlayersSave { Kind = 1, Recorded = new List<WorldPlayerSave> { null, new WorldPlayerSave { Id = "a" } } }, out _), Is.False, "null entry");
        }
    }
}
