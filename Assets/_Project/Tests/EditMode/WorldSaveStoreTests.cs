// Tests/EditMode/WorldSaveStoreTests.cs: the host's world file (Runtime/Save/WorldSaveStore): the pinned v1 format, the round trip, a first run,
// a bad section and a newer file. Ported from FishGame's world save tests (its fishing sections gone, the players section new).
// Every test works under its own directory below the OS temp path.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.EditMode
{
    public class WorldSaveStoreTests
    {
        const ulong DefaultSeed = 0xF15F15F1UL;

        // The v1 format, pinned: every later build must keep loading this exact text (add a migration step, never break it).
        public const string GoldenV1 = @"{
 ""schemaVersion"": 1,
 ""savedAtUtc"": ""2026-10-29T19:40:00Z"",
 ""worldSeed"": ""00000000F15F15F1"",
 ""players"": { ""kind"": 1, ""identity"": 1, ""recorded"": [ { ""identity"": 1, ""id"": ""aaa"" }, { ""identity"": 1, ""id"": ""bbb"" } ] }
}";

        private string _dir, _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sc-world-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, WorldSaveStore.WorldBaseName + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private WorldSaveStore Load()
        {
            var store = new WorldSaveStore(_path, DefaultSeed);
            store.Load();
            return store;
        }

        // Catches: a build that can no longer read the v1 format (a renamed field, a changed type, camelCase lost), or that reads it wrong.
        [Test]
        public void GoldenV1_LoadsExactly()
        {
            File.WriteAllText(_path, GoldenV1);
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.IsReadOnly, Is.False);
            WorldSave w = store.Current;
            Assert.That(w.WorldSeed, Is.EqualTo("00000000F15F15F1"));
            Assert.That(w.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer));
            Assert.That(w.Players.Recorded.Count, Is.EqualTo(2));
            Assert.That(w.Players.Recorded[1].Identity, Is.EqualTo(1));
            Assert.That(w.Players.Recorded[1].Id, Is.EqualTo("bbb"));
            Assert.That(WorldPlayers.TryFromSave(w.Players, out WorldPlayers world), Is.True, "and it is a world the rules accept");
            Assert.That(world.IsRecorded(new PlayerKey(IdentityKind.Ugs, "bbb")), Is.True);
        }

        // Catches: the world-player record lost or altered through the real file path (not only through the in-memory section).
        [Test]
        public void SaveThenLoad_RoundTripsThePlayers()
        {
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Defaults), "a first run");
            WorldSave w = WorldSaveRules.New(0xFEDCBA9876543210UL);
            Assert.That(WorldPlayers.TryCreate(WorldKind.Multiplayer,
                new[] { new PlayerKey(IdentityKind.Ugs, "host"), new PlayerKey(IdentityKind.Ugs, "friend") }, out WorldPlayers players), Is.EqualTo(WorldPlayerVerdict.Ok));
            players.Record(new PlayerKey(IdentityKind.Ugs, "third"));
            w.Players = players.ToSave();
            store.Replace(w);
            Assert.That(store.FlushIfDirty(), Is.True);
            Assert.That(File.ReadAllText(_path), Does.Contain("\"worldSeed\": \"FEDCBA9876543210\"").And.Contain("\"recorded\""));

            WorldSaveStore again = Load();
            Assert.That(again.Rejected, Is.Empty);
            Assert.That(again.Current.WorldSeed, Is.EqualTo("FEDCBA9876543210"));
            Assert.That(WorldPlayers.TryFromSave(again.Current.Players, out WorldPlayers back), Is.True);
            Assert.That(back.Kind, Is.EqualTo(WorldKind.Multiplayer));
            CollectionAssert.AreEqual(players.Recorded, back.Recorded);
        }

        [Test]
        public void SoloWorld_RoundTrips_AndStillRefusesJoins()
        {
            WorldSaveStore store = Load();
            WorldSave w = WorldSaveRules.New(7UL);
            WorldPlayers.TryCreate(WorldKind.Solo, new[] { new PlayerKey(IdentityKind.Local, "me") }, out WorldPlayers solo);
            w.Players = solo.ToSave();
            store.Replace(w);
            store.FlushIfDirty();

            WorldSaveStore again = Load();
            Assert.That(WorldPlayers.TryFromSave(again.Current.Players, out WorldPlayers back), Is.True);
            Assert.That(back.Kind, Is.EqualTo(WorldKind.Solo));
            Assert.That(back.CanJoin(new PlayerKey(IdentityKind.Local, "other")), Is.EqualTo(WorldPlayerVerdict.SoloWorldClosed));
        }

        // Catches: a first run (no file) warned about or not given the default seed, or a missing file reported as corrupt.
        [Test]
        public void NoFile_IsANewWorld()
        {
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Defaults));
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.Current.WorldSeed, Is.EqualTo(WorldSaveRules.SeedToHex(DefaultSeed)));
            Assert.That(store.Current.Players, Is.Null);
        }

        // Catches: a bad players section wiping the seed, the reset not reported, or the original overwritten without its .corrupt copy.
        [Test]
        public void ABadSection_ResetsOnlyItself_AndTheOriginalIsKept()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"kind\": 1", "\"kind\": 9"));   // a kind no build knows
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
            Assert.That(store.Current.Players, Is.Null);
            Assert.That(store.Current.WorldSeed, Is.EqualTo("00000000F15F15F1"), "the seed kept");
            store.MarkDirty();
            Assert.That(store.FlushIfDirty(), Is.True);
            Assert.That(File.ReadAllText(_path + JsonSaveFile<WorldSave>.CorruptSuffix), Does.Contain("aaa"), "the original kept");
            Assert.That(Load().Rejected, Is.Empty, "the rewritten file is clean");
        }

        // Catches (risk review): a record the file load cannot read ("identity": "x") rejecting the whole section, so a Multiplayer world
        // comes back as "no world yet". Only that one record goes; the kind and the other player stay.
        [Test]
        public void OneMistypedPlayerRecord_CostsOnlyThatRecord()
        {
            File.WriteAllText(_path, GoldenV1.Replace("{ \"identity\": 1, \"id\": \"aaa\" }", "{ \"identity\": \"x\", \"id\": \"aaa\" }"));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
            Assert.That(store.Current.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer), "still a Multiplayer world");
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(1));
            Assert.That(store.Current.Players.Recorded[0].Id, Is.EqualTo("bbb"));
            Assert.That(WorldPlayers.TryRestore(store.Current.Players, out WorldPlayers w, out _), Is.True);
            Assert.That(w.CanStart(new[] { new PlayerKey(IdentityKind.Ugs, "bbb") }), Is.EqualTo(WorldPlayerVerdict.NotEnoughPlayers), "it still needs two to start");
        }

        // Catches (risk review): 1.5 in an int field passing the section check (JToken.ToObject rounds it) and then failing the real file load,
        // so one bad number cost the whole file (the .bak or a new world) instead of the one section.
        [Test]
        public void AFractionInAnIntField_CostsOnlyThatSection_NotTheFile()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"kind\": 1", "\"kind\": 1.5"));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main), "the file still loads");
            Assert.That(store.Rejected, Does.Contain(WorldSaveRules.SectionPlayers));
            Assert.That(store.Current.Players, Is.Null);
            Assert.That(store.Current.WorldSeed, Is.EqualTo("00000000F15F15F1"));

            File.WriteAllText(_path, GoldenV1.Replace("\"identity\": 1, \"id\": \"bbb\"", "\"identity\": 1.5, \"id\": \"bbb\""));
            store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(1), "only the record with the fraction goes");
        }

        // Catches (risk review): a member this build does not know (a newer build's section, or a field inside the players section) being
        // dropped when this build loads and saves the file, in the main file and in the .bak the next save rotates it into.
        [Test]
        public void UnknownMembers_SurviveLoadAndSave_InMainAndBak()
        {
            string withFuture = GoldenV1
                .Replace("\"worldSeed\"", "\"futureThing\": { \"coins\": 9 }, \"worldSeed\"")
                .Replace("\"recorded\"", "\"colourScheme\": \"red\", \"recorded\"")
                .Replace("\"id\": \"aaa\"", "\"id\": \"aaa\", \"favourite\": 3");
            File.WriteAllText(_path, withFuture);
            WorldSaveStore store = Load();
            store.MarkDirty();
            Assert.That(store.FlushIfDirty(), Is.True);
            string main = File.ReadAllText(_path);
            Assert.That(main, Does.Contain("futureThing").And.Contain("\"coins\": 9").And.Contain("colourScheme").And.Contain("favourite"));

            store.MarkDirty();
            store.FlushIfDirty();   // the second write rotates the first into the .bak
            Assert.That(File.ReadAllText(_path + ".bak"), Does.Contain("futureThing").And.Contain("colourScheme").And.Contain("favourite"));
            Assert.That(File.ReadAllText(_path), Does.Contain("futureThing"));

            // The .bak alone (main unreadable) loads and keeps them too.
            File.WriteAllText(_path, "{ \"schemaVersion\": 1, \"wor");
            WorldSaveStore fromBak = Load();
            Assert.That(fromBak.LoadedFrom, Is.EqualTo(MetaLoadSource.Backup));
            fromBak.MarkDirty();
            fromBak.FlushIfDirty();
            Assert.That(File.ReadAllText(_path), Does.Contain("futureThing").And.Contain("favourite"));
        }

        // Catches: a newer build's world opened and written by this one (its new fields lost), or treated as corrupt; unknown fields making a
        // same-version file unreadable.
        [Test]
        public void NewerFile_ReadOnly_UnknownFieldsIgnored()
        {
            string newer = GoldenV1.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2").Replace("\"players\":", "\"haul\": {\"coins\": 9}, \"players\":");
            File.WriteAllText(_path, newer);
            WorldSaveStore store = Load();
            Assert.That(store.IsReadOnly, Is.True);
            store.MarkDirty();
            store.FlushIfDirty();
            Assert.That(File.ReadAllText(_path), Is.EqualTo(newer), "never written");
            Assert.That(File.Exists(_path + JsonSaveFile<WorldSave>.CorruptSuffix), Is.False, "not corrupt");

            File.WriteAllText(_path, GoldenV1.Replace("\"players\":", "\"futureThing\": [1, 2], \"players\":"));
            WorldSaveStore same = Load();
            Assert.That(same.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(same.IsReadOnly, Is.False);
            Assert.That(same.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: the migration hook altering a current file, or throwing on junk instead of passing it on.
        [Test]
        public void Upgrade_LeavesCurrentAndUnreadableTextAlone()
        {
            Assert.That(WorldSaveStore.Upgrade(GoldenV1), Is.EqualTo(GoldenV1));
            Assert.That(WorldSaveStore.Upgrade("not json"), Is.EqualTo("not json"));
            Assert.That(WorldSaveStore.Upgrade("{\"schemaVersion\": 0}"), Does.Contain("schemaVersion"));
        }

        // Catches: a type error in one section ("kind": "x" into an int, a recorded list that is a number) making the whole file unreadable
        // (the world lost to the .bak or defaults) instead of resetting that section; the reset not reported.
        [Test]
        public void ATypeErrorInASection_CostsOnlyThatSection()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"kind\": 1", "\"kind\": \"two\""));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main), "the file still loads");
            Assert.That(store.Rejected, Does.Contain(WorldSaveRules.SectionPlayers));
            Assert.That(store.Current.Players, Is.Null);
            Assert.That(store.Current.WorldSeed, Is.EqualTo("00000000F15F15F1"), "the seed kept");

            File.WriteAllText(_path, GoldenV1.Replace("\"recorded\": [", "\"recorded\": 5, \"was\": ["));
            store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Does.Contain(WorldSaveRules.SectionPlayers));

            File.WriteAllText(_path, GoldenV1.Replace("\"worldSeed\": \"00000000F15F15F1\"", "\"worldSeed\": [1]"));
            store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Does.Contain(WorldSaveRules.SectionSeed));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2), "the players kept");
        }

        // Catches: a world loaded from the .bak (main truncated) with a section reset losing that .bak to the second save's rotation, with no
        // copy kept.
        [Test]
        public void AResetLoadedFromTheBak_KeepsTheBak()
        {
            File.WriteAllText(_path, "{ \"schemaVersion\": 1, \"worl");                                                                   // main truncated
            File.WriteAllText(_path + ".bak", GoldenV1.Replace("\"kind\": 1", "\"kind\": 9"));                                                   // the .bak, its players unusable
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Backup));
            Assert.That(store.Rejected, Does.Contain(WorldSaveRules.SectionPlayers));
            store.MarkDirty();
            store.FlushIfDirty();
            store.MarkDirty();
            store.FlushIfDirty();   // the second write rotates main over .bak
            Assert.That(File.ReadAllText(_path + ".bak" + JsonSaveFile<WorldSave>.CorruptSuffix), Does.Contain("aaa"), "the loaded .bak was kept");
            Assert.That(File.ReadAllText(_path + JsonSaveFile<WorldSave>.CorruptSuffix), Does.Contain("\"worl"), "and the truncated main");
        }

        // Catches (the dev New world button): the world deleted instead of set aside, its .bak left behind (the next session would load it and
        // not start fresh), or this session still writing the old world back; a Replace on a read-only file reported as written.
        [Test]
        public void Retire_SetsTheWorldAside_AndStopsWrites()
        {
            File.WriteAllText(_path, GoldenV1);
            File.WriteAllText(_path + ".bak", GoldenV1);
            WorldSaveStore store = Load();
            Assert.That(store.Retire(), Is.True);
            Assert.That(store.LastRetiredPath, Does.StartWith(_path + WorldSaveStore.RetiredSuffix + "-"));
            Assert.That(File.ReadAllText(store.LastRetiredPath), Is.EqualTo(GoldenV1), "set aside");
            Assert.That(File.Exists(store.LastRetiredPath + ".bak"), Is.True, "with its .bak");
            Assert.That(File.Exists(_path) || File.Exists(_path + ".bak"), Is.False, "nothing left to load");
            store.MarkDirty();
            store.FlushIfDirty();
            Assert.That(File.Exists(_path), Is.False, "this session writes nothing more");
            Assert.That(store.Replace(WorldSaveRules.New(1UL)), Is.False, "a replace that will never be written says so");
            Assert.That(Load().LoadedFrom, Is.EqualTo(MetaLoadSource.Defaults), "the next session starts a new world");
        }

        // Catches: New world pressed in two sessions (within the same second, too) replacing the first world set aside with the second, so a
        // world is lost; a .bak-only set-aside landing beside another world's main.
        [Test]
        public void Retire_Twice_KeepsBothWorlds()
        {
            File.WriteAllText(_path, GoldenV1);
            WorldSaveStore first = Load();
            Assert.That(first.Retire(), Is.True);

            const string second = "{ \"schemaVersion\": 1, \"worldSeed\": \"0000000000000002\" }";
            File.WriteAllText(_path, second);
            WorldSaveStore again = Load();
            Assert.That(again.Retire(), Is.True);

            Assert.That(again.LastRetiredPath, Is.Not.EqualTo(first.LastRetiredPath), "a new name each time");
            Assert.That(File.ReadAllText(first.LastRetiredPath), Is.EqualTo(GoldenV1), "the first world is still there");
            Assert.That(File.ReadAllText(again.LastRetiredPath), Is.EqualTo(second));

            File.WriteAllText(_path + ".bak", second);   // a lone .bak (no main) goes under its own name, not beside another world's main
            WorldSaveStore third = Load();
            Assert.That(third.Retire(), Is.True);
            Assert.That(File.Exists(third.LastRetiredPath), Is.False);
            Assert.That(File.Exists(third.LastRetiredPath + ".bak"), Is.True);
            Assert.That(third.LastRetiredPath, Is.Not.EqualTo(first.LastRetiredPath).And.Not.EqualTo(again.LastRetiredPath));
        }

        // Catches: a WorldSave field with no row in Prepare's section table, so one mistyped value in it fails the whole file (the world rolls
        // back to the .bak or defaults). Every new section added to WorldSave must get its row.
        [Test]
        public void EveryWorldSaveField_HasAPrepareRow()
        {
            var rows = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, Type type) in WorldSaveStore.SectionKeys()) rows[key] = type;
            foreach (var f in typeof(WorldSave).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.Name == nameof(WorldSave.SchemaVersion)) continue;   // JsonSaveFile reads it first
                if (f.Name == nameof(WorldSave.Extra)) continue;           // the extension data, not a section
                Assert.That(rows.TryGetValue(f.Name, out Type rowType), Is.True, "WorldSave." + f.Name + " has no row in WorldSaveStore's Sections");
                Assert.That(rowType, Is.EqualTo(f.FieldType), "WorldSave." + f.Name + "'s row parses it as its own type");
            }
        }
    
        // Catches (risk review): one mistyped member of the players section ("identity": "x", "recorded": 5) costing the whole section.
        [Test]
        public void AMistypedMember_CostsOnlyThatMember()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"identity\": 1, \"recorded\"", "\"identity\": \"x\", \"recorded\""));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
            Assert.That(store.Current.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer), "the kind stays");
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2), "so do both records");
            Assert.That(store.Current.Players.Identity, Is.EqualTo(1), "the world's identity kind is read again from its records");

            File.WriteAllText(_path, GoldenV1.Replace("\"recorded\": [", "\"recorded\": 5, \"was\": ["));
            store = Load();
            Assert.That(store.Current.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer), "a bad list keeps the kind");
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(0));
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
        }

        // Catches (risk review): a players section with no kind coming back as a Solo world.
        [Test]
        public void AMissingKind_IsRejected_NotReadAsSolo()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"kind\": 1, ", ""));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
            Assert.That(store.Current.Players, Is.Null);
        }

        // Catches: the world's id not following its file name (a copied file would claim another world's id), or the header fields lost.
        [Test]
        public void TheWorldId_IsTheFileName_AndCreatedAtRoundTrips()
        {
            string id = WorldIds.NewId();
            string path = Path.Combine(_dir, id + ".json");
            File.WriteAllText(path, GoldenV1.Replace("\"worldSeed\"", "\"worldId\": \"" + new string('a', 32) + "\", \"createdAtUtc\": \"2026-10-01T09:00:00Z\", \"worldSeed\""));
            var store = new WorldSaveStore(path, DefaultSeed);
            store.Load();
            Assert.That(store.Current.WorldId, Is.EqualTo(id), "the file name wins over a copied id");
            Assert.That(store.Current.CreatedAtUtc, Is.EqualTo("2026-10-01T09:00:00Z"));
            Assert.That(store.Rejected, Is.Empty);
        }
    }
}
