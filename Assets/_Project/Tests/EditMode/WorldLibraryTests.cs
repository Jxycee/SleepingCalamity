// Tests/EditMode/WorldLibraryTests.cs: a player's worlds on this machine (Runtime/Save/WorldLibrary; user decision 2026-10-30): one file per
// world, up to 10 Solo and 10 Multiplayer worlds per list, Local and online worlds never crossing, delete sets aside, one-time migration.
// Every test works under its own directory below the OS temp path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.EditMode
{
    public class WorldLibraryTests
    {
        private string _dir;
        private WorldLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sc-worlds-" + Guid.NewGuid().ToString("N"));
            _library = new WorldLibrary(Path.Combine(_dir, "worlds"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        static PlayerKey K(string id) => new PlayerKey(IdentityKind.Local, id);

        // Catches: a world not getting its own file named by its id, or a new world not being listed with its kind and identity kind.
        [Test]
        public void Create_MakesOneFilePerWorld_AndListsIt()
        {
            WorldSaveStore a = _library.Create(WorldKind.Solo, IdentityKind.Local, 1UL);
            WorldSaveStore b = _library.Create(WorldKind.Solo, IdentityKind.Local, 2UL);
            Assert.That(a.Current.WorldId, Is.Not.EqualTo(b.Current.WorldId));
            Assert.That(WorldIds.IsValid(a.Current.WorldId), Is.True);
            Assert.That(File.Exists(_library.PathOf(a.Current.WorldId)), Is.True);
            Assert.That(Path.GetFileNameWithoutExtension(a.FilePath), Is.EqualTo(a.Current.WorldId));

            List<WorldSummary> list = _library.List(WorldKind.Solo, IdentityKind.Local);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0].Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(list[0].Identity, Is.EqualTo((int)IdentityKind.Local));
            Assert.That(list[0].CreatedAtUtc, Is.Not.Empty);
        }

        // Catches: the 11th world of a list being made, or a full Solo list blocking Multiplayer worlds (separate tabs).
        [Test]
        public void EachList_HoldsTenWorlds_AndTheTabsAreSeparate()
        {
            for (int i = 0; i < WorldSummary.MaxPerList; i++) Assert.That(_library.Create(WorldKind.Solo, IdentityKind.Local, (ulong)i + 1), Is.Not.Null, "solo world " + i);
            Assert.That(_library.Create(WorldKind.Solo, IdentityKind.Local, 99UL), Is.Null, "the 11th solo world");
            Assert.That(_library.HasRoom(WorldKind.Solo, IdentityKind.Local), Is.False);
            Assert.That(_library.List(WorldKind.Solo, IdentityKind.Local).Count, Is.EqualTo(10));

            Assert.That(_library.HasRoom(WorldKind.Multiplayer, IdentityKind.Local), Is.True);
            for (int i = 0; i < WorldSummary.MaxPerList; i++) Assert.That(_library.Create(WorldKind.Multiplayer, IdentityKind.Local, (ulong)i + 1), Is.Not.Null, "multiplayer world " + i);
            Assert.That(_library.Create(WorldKind.Multiplayer, IdentityKind.Local, 99UL), Is.Null);
        }

        // Catches: Local and online worlds crossing in a list (a Local key meeting an online world).
        [Test]
        public void Lists_FilterByIdentityKind()
        {
            _library.Create(WorldKind.Multiplayer, IdentityKind.Local, 1UL);
            _library.Create(WorldKind.Multiplayer, IdentityKind.Ugs, 2UL);
            _library.Create(WorldKind.Multiplayer, IdentityKind.Ugs, 3UL);
            Assert.That(_library.List(WorldKind.Multiplayer, IdentityKind.Local).Count, Is.EqualTo(1));
            Assert.That(_library.List(WorldKind.Multiplayer, IdentityKind.Ugs).Count, Is.EqualTo(2));
            Assert.That(_library.List(WorldKind.Solo, IdentityKind.Local).Count, Is.EqualTo(0));
        }

        // Catches: recorded players and their names not reaching the picker, and the most recent world not coming first.
        [Test]
        public void List_ShowsPlayerNames_AndTheNewestPlayedFirst()
        {
            WorldSaveStore old = _library.Create(WorldKind.Multiplayer, IdentityKind.Local, 1UL);
            old.Current.SavedAtUtc = "2026-01-01T10:00:00.0000000Z";
            old.MarkDirty();
            old.Save();

            WorldSaveStore recent = _library.Create(WorldKind.Multiplayer, IdentityKind.Local, 2UL);
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { K("a"), K("b") }, out WorldPlayers players);
            players.SetName(K("a"), "Ann");
            recent.Current.Players = players.ToSave();
            recent.Current.SavedAtUtc = "2026-06-01T10:00:00.0000000Z";
            recent.MarkDirty();
            recent.Save();

            List<WorldSummary> list = _library.List(WorldKind.Multiplayer, IdentityKind.Local);
            Assert.That(list[0].Id, Is.EqualTo(recent.Current.WorldId), "most recently played first");
            Assert.That(list[0].PlayerNames, Is.EqualTo(new[] { "Ann", "?" }), "a player with no kept name shows as ?");
            Assert.That(list[1].Id, Is.EqualTo(old.Current.WorldId));
        }

        // Catches: a save-and-reopen losing the world, its players or its id; a delete erasing the world instead of setting it aside.
        [Test]
        public void Open_ReturnsTheSavedWorld_AndDeleteSetsItAside()
        {
            WorldSaveStore made = _library.Create(WorldKind.Solo, IdentityKind.Local, 7UL);
            string id = made.Current.WorldId;
            WorldPlayers.TryCreate(WorldKind.Solo, new[] { K("me") }, out WorldPlayers solo);
            made.Current.Players = solo.ToSave();
            made.MarkDirty();
            made.Save();

            WorldSaveStore reopened = _library.Open(id);
            Assert.That(reopened.Current.WorldId, Is.EqualTo(id));
            Assert.That(reopened.Current.Players.Recorded.Count, Is.EqualTo(1));
            Assert.That(_library.Open("not-an-id"), Is.Null);
            Assert.That(_library.Open(WorldIds.NewId()), Is.Null, "no such file");

            Assert.That(_library.Delete(id), Is.True);
            Assert.That(_library.List(WorldKind.Solo, IdentityKind.Local), Is.Empty, "it leaves the list");
            Assert.That(File.Exists(_library.PathOf(id)), Is.False);
            string[] aside = Directory.GetFiles(_library.Directory, id + ".json.old-*");
            Assert.That(aside.Length, Is.GreaterThan(0), "but the files are still on disk");
            Assert.That(_library.HasRoom(WorldKind.Solo, IdentityKind.Local), Is.True);
        }

        // Catches (audit A6): a world whose file cannot be read (junk, or a players section with an unknown kind) vanishing from the picker, so the host
        // flow opens another world and the files are never mentioned; a damaged world taking one of the ten places of every list; and setting it aside
        // not working. NOT covered: what the picker shows (FrontDoorModelTests) and the host flow (WorldSaveFlowTests).
        [Test]
        public void UnreadableWorld_IsListedAsDamaged()
        {
            WorldSaveStore good = _library.Create(WorldKind.Solo, IdentityKind.Local, 1UL);
            string junkId = WorldIds.NewId();
            File.WriteAllText(_library.PathOf(junkId), "not json at all");
            string badKindId = WorldIds.NewId();
            File.WriteAllText(_library.PathOf(badKindId), WorldSaveStoreTests.GoldenV1.Replace("\"kind\": 1", "\"kind\": 9"));   // parses; its players section is unusable

            List<WorldSummary> solo = _library.List(WorldKind.Solo, IdentityKind.Local);
            Assert.That(solo.Select(w => w.Id), Is.EquivalentTo(new[] { good.Current.WorldId, junkId, badKindId }), "all three files are listed");
            Assert.That(solo.Single(w => w.Id == good.Current.WorldId).Damaged, Is.False);
            Assert.That(solo.Single(w => w.Id == junkId).Damaged, Is.True, "unreadable file");
            Assert.That(solo.Single(w => w.Id == badKindId).Damaged, Is.True, "unusable players section");
            Assert.That(_library.List(WorldKind.Multiplayer, IdentityKind.Ugs).Select(w => w.Id), Is.EquivalentTo(new[] { junkId, badKindId }),
                "its kind is unknown, so every list shows it (and only it)");

            for (int i = 1; i < WorldSummary.MaxPerList; i++) Assert.That(_library.Create(WorldKind.Solo, IdentityKind.Local, (ulong)i + 1), Is.Not.Null, "world " + (i + 1) + ": damaged files take no place");
            Assert.That(_library.HasRoom(WorldKind.Solo, IdentityKind.Local), Is.False, "ten good worlds fill the list");

            Assert.That(_library.Delete(junkId), Is.True, "a damaged world can be set aside");
            Assert.That(_library.List(WorldKind.Solo, IdentityKind.Local).Select(w => w.Id), Does.Not.Contain(junkId));
            Assert.That(Directory.GetFiles(_library.Directory, junkId + ".json.old-*").Length, Is.GreaterThan(0), "and its file is still on disk");
        }

        // Catches (security of the file name): an id with path characters reaching the file system.
        [Test]
        public void OnlyValidIds_ReachTheDisk()
        {
            Assert.That(_library.Delete("../escape"), Is.False);
            Assert.That(_library.Open("..\\x"), Is.Null);
            Assert.That(WorldIds.IsValid("0123456789abcdef0123456789abcdef"), Is.True);
            Assert.That(WorldIds.IsValid("0123456789ABCDEF0123456789ABCDEF"), Is.False, "lowercase only");
            Assert.That(WorldIds.IsValid(null), Is.False);
        }

        // Catches: the old single world file being lost or left in place and migrated again on every start.
        [Test]
        public void MigrateLegacy_MovesTheOldFileIntoTheLibrary_Once()
        {
            Directory.CreateDirectory(_dir);
            string legacy = Path.Combine(_dir, "world.json");
            File.WriteAllText(legacy, "{ \"schemaVersion\": 1, \"savedAtUtc\": \"2026-10-01T10:00:00Z\", \"worldSeed\": \"00000000DEADBEEF\", " +
                "\"players\": { \"kind\": 1, \"identity\": 0, \"recorded\": [ { \"identity\": 0, \"id\": \"aaa\" }, { \"identity\": 0, \"id\": \"bbb\" } ] } }");

            Assert.That(_library.MigrateLegacy(legacy), Is.True);
            Assert.That(File.Exists(legacy), Is.False, "renamed, not left");
            Assert.That(File.Exists(legacy + WorldLibrary.MigratedSuffix), Is.True, "and not deleted");
            List<WorldSummary> list = _library.List(WorldKind.Multiplayer, IdentityKind.Local);
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].PlayerNames.Count, Is.EqualTo(2));
            WorldSaveStore opened = _library.Open(list[0].Id);
            Assert.That(opened.Current.WorldSeed, Is.EqualTo("00000000DEADBEEF"), "the seed came along");

            Assert.That(_library.MigrateLegacy(legacy), Is.False, "nothing left to migrate");
            Assert.That(_library.List(WorldKind.Multiplayer, IdentityKind.Local).Count, Is.EqualTo(1));
        }

        // Catches: a migration overflowing a full list, or touching a file it cannot read.
        [Test]
        public void MigrateLegacy_LeavesTheFileWhenTheListIsFull_OrTheFileIsUnreadable()
        {
            Directory.CreateDirectory(_dir);
            string legacy = Path.Combine(_dir, "world.json");
            File.WriteAllText(legacy, "{ \"schemaVersion\": 1, \"players\": { \"kind\": 0, \"identity\": 0, \"recorded\": [ { \"identity\": 0, \"id\": \"aaa\" } ] } }");
            for (int i = 0; i < WorldSummary.MaxPerList; i++) _library.Create(WorldKind.Solo, IdentityKind.Local, (ulong)i + 1);
            Assert.That(_library.MigrateLegacy(legacy), Is.False);
            Assert.That(File.Exists(legacy), Is.True, "untouched");

            string junk = Path.Combine(_dir, "junk.json");
            File.WriteAllText(junk, "not json at all");
            Assert.That(_library.MigrateLegacy(junk), Is.False);
            Assert.That(File.Exists(junk), Is.True);
        }
    }
}
