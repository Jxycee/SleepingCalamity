// Tests/EditMode/WorldSaveStoreTests.cs: the host's world file (Runtime/Save/WorldSaveStore): the pinned v1 format, the round trip, a first run,
// a bad section and a newer file. Ported from FishGame's world save tests (its fishing sections gone, the players section new).
// Every test works under its own directory below the OS temp path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
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

        // ---- the weapons section (M6 S0) ----

        static WeaponsSave OneWeapon(string owner, int level)
        {
            WeaponState w = WeaponState.NewHeirloom(AttackCatalog.Heirlooms[0]);
            w.AddAbs(level * 100f);
            var save = new WeaponsSave();
            save.Weapons.Add(w.ToSave(owner + "/" + AttackCatalog.Heirlooms[0], owner));
            save.Heroes.Add(new HeroSave { OwnerId = owner, Abilities = new HeroAbilitiesSave { Unlocked = { "axe_quake" } } });
            return save;
        }

        // Catches: the weapons section not surviving a save and load, or its unknown members (a newer build's) being dropped.
        [Test]
        public void Weapons_RoundTrip()
        {
            WorldSaveStore store = Load();
            WorldSave w = WorldSaveRules.New(7UL);
            w.Weapons = OneWeapon("me", 1);
            w.Weapons.Extra = new Dictionary<string, object> { { "futureField", 3L } };
            store.Replace(w);
            Assert.That(store.FlushIfDirty(), Is.True);
            Assert.That(File.ReadAllText(_path), Does.Contain("\"weapons\""));

            WorldSaveStore again = Load();
            Assert.That(again.Rejected, Is.Empty);
            Assert.That(again.Current.Weapons.Weapons.Count, Is.EqualTo(1));
            Assert.That(again.Current.Weapons.Weapons[0].OwnerId, Is.EqualTo("me"));
            Assert.That(again.Current.Weapons.Weapons[0].Level, Is.EqualTo(w.Weapons.Weapons[0].Level));
            Assert.That(again.Current.Weapons.Heroes[0].Abilities.Unlocked, Is.EqualTo(new[] { "axe_quake" }));
            Assert.That(again.Current.Weapons.Extra, Does.ContainKey("futureField"));
        }

        // Catches: the v1 golden file no longer loading, or an old save (no weapons section) loading with a half-made one.
        [Test]
        public void OldSaveWithoutWeapons_LoadsWithNone()
        {
            File.WriteAllText(_path, GoldenV1);
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.Current.Weapons, Is.Null, "no section: every player starts with fresh heirlooms");
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: a damaged weapons section costing the players or the seed (a bad section resets only itself).
        [Test]
        public void BadWeaponsSection_KeepsTheRest()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"players\"", "\"weapons\": 5, \"players\""));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionWeapons }));
            Assert.That(store.Current.Weapons, Is.Null, "the weapons reset");
            Assert.That(store.Current.WorldSeed, Is.EqualTo("00000000F15F15F1"));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: one mistyped or invalid weapon costing the player's other weapons.
        [Test]
        public void OneBadWeapon_IsDroppedAlone()
        {
            WeaponsSave good = OneWeapon("me", 1);
            string goodJson = Newtonsoft.Json.JsonConvert.SerializeObject(good.Weapons[0]);
            string weapons = "{ \"weapons\": [ " + goodJson + ", { \"key\": 7, \"level\": \"x\" }, { \"key\": \"bad\", \"definitionId\": \"no_such_weapon\" } ] }";
            File.WriteAllText(_path, GoldenV1.Replace("\"players\"", "\"weapons\": " + weapons + ", \"players\""));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionWeapons }), "named so the original is kept");
            Assert.That(store.Current.Weapons.Weapons.Count, Is.EqualTo(1));
            Assert.That(store.Current.Weapons.Weapons[0].OwnerId, Is.EqualTo("me"));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // ---- per-player loot plan task 3: the optional "bags" section ----

        // Catches: bags lost or altered through the real file path, a world with no bags writing a "bags" key, the golden v1 file gaining a bags section on load.
        [Test]
        public void Bags_RoundTripsThroughTheStore()
        {
            File.WriteAllText(_path, GoldenV1);
            WorldSaveStore store = Load();
            Assert.That(store.Current.Bags, Is.Null, "the golden v1 file has no bags");
            store.MarkDirty(); store.FlushIfDirty();
            Assert.That(File.ReadAllText(_path), Does.Not.Contain("\"bags\""), "a world with no bags writes no bags key");

            var bags = new BagsSave();
            var p = new PlayerBagSave { Identity = 1, Id = "aaa", Gold = 7, Satchel = new SatchelSave { Floor = 2, Items = new List<int> { 110, 111 } } };
            p.Stash.Add(new LootCountSave { ItemId = 104, Count = 3 });
            bags.Players.Add(p);
            WorldSave w = store.Current;
            w.Bags = bags;
            store.Replace(w);
            Assert.That(store.FlushIfDirty(), Is.True);
            Assert.That(File.ReadAllText(_path), Does.Contain("\"bags\""));

            WorldSaveStore again = Load();
            Assert.That(again.Rejected, Is.Empty);
            PlayerBagSave back = again.Current.Bags.Players.Single();
            Assert.That(back.Identity, Is.EqualTo(1)); Assert.That(back.Id, Is.EqualTo("aaa")); Assert.That(back.Gold, Is.EqualTo(7));
            Assert.That(back.Stash.Single().ItemId, Is.EqualTo(104)); Assert.That(back.Stash.Single().Count, Is.EqualTo(3));
            Assert.That(back.Satchel.Floor, Is.EqualTo(2)); Assert.That(back.Satchel.Items, Is.EqualTo(new[] { 110, 111 }));
            Assert.That(again.Current.Players.Recorded.Count, Is.EqualTo(2), "the rest of the file is untouched");
        }

        // Catches (sc-netcode "World save"): a mistyped bags section failing the whole WorldSave parse so the world rolls back to .bak.
        [Test]
        public void Bags_MistypedSectionKeepsTheRestOfTheFile()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"players\"", "\"bags\": \"oops\", \"players\""));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }), "the rejection label");
            Assert.That(store.Current.Bags, Is.Null);
            Assert.That(store.Current.WorldSeed, Is.EqualTo("00000000F15F15F1"));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: one mistyped or invalid player entry costing the other players' bags.
        [Test]
        public void Bags_OneBadEntry_IsDroppedAlone()
        {
            string bags = "{ \"players\": [ { \"identity\": 1, \"id\": \"aaa\", \"gold\": 5 }, { \"identity\": 1, \"id\": \"bbb\", \"gold\": \"lots\" }, { \"identity\": 77, \"id\": \"ccc\", \"gold\": 1 } ] }";
            File.WriteAllText(_path, GoldenV1.Replace("\"players\"", "\"bags\": " + bags + ", \"players\""));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }), "named so the original is kept");
            Assert.That(store.Current.Bags.Players.Select(e => e.Id), Is.EqualTo(new[] { "aaa" }));
            Assert.That(store.Current.Bags.Players[0].Gold, Is.EqualTo(5));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: HostWorld dropping the bags section, writing a pristine one into a file that has none, or writing bags of players the world does not record.
        [Test]
        public void HostWorld_CarriesBagsOfRecordedPlayersOnly()
        {
            var host = new HostWorld();
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { new PlayerKey(IdentityKind.Ugs, "aaa"), new PlayerKey(IdentityKind.Ugs, "bbb") }, out WorldPlayers players);
            host.Load(players);
            var saved = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL), Bags = new BagsSave() };
            saved.Bags.Players.Add(new PlayerBagSave { Identity = 1, Id = "aaa", Gold = 4 });
            host.LoadTown(saved);
            Assert.That(host.Town.Bags.Players.Single().Gold, Is.EqualTo(4));

            var target = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL) };
            host.CaptureTownInto(target);
            Assert.That(target.Bags.Players.Single().Id, Is.EqualTo("aaa"), "what was loaded is written back");

            // a world that never had bags: nothing to write
            var fresh = new HostWorld();
            fresh.Load(players);
            var empty = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL) };
            fresh.LoadTown(empty);
            fresh.CaptureTownInto(empty);
            Assert.That(empty.Bags, Is.Null, "no bags key for a world without bags");
        }

        sealed class BagsSource : HostWorld.ITownSource
        {
            public BagsSave Live;
            public TownSnapshot CaptureTown() => new TownSnapshot { Bags = Live };
            public void RestoreTown(TownSnapshot loaded) { }
        }

        // Catches: a client-id fallback bag or an unrecorded player's bag reaching the file, or a recorded player's live bag not being written.
        [Test]
        public void HostWorld_WritesLiveBags_OfRecordedPlayersOnly()
        {
            var host = new HostWorld();
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { new PlayerKey(IdentityKind.Ugs, "aaa"), new PlayerKey(IdentityKind.Ugs, "bbb") }, out WorldPlayers players);
            host.Load(players);
            host.LoadTown(new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL) });
            var live = new BagsSave();
            live.Players.Add(new PlayerBagSave { Identity = 1, Id = "aaa", Gold = 4 });
            live.Players.Add(new PlayerBagSave { Identity = 1, Id = "stranger", Gold = 99 });   // not recorded
            host.RegisterTownSource(new BagsSource { Live = live });
            var target = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL) };
            Assert.That(host.CaptureTownInto(target), Is.True);
            Assert.That(target.Bags.Players.Select(e => e.Id), Is.EqualTo(new[] { "aaa" }));
            Assert.That(target.Bags.Players[0].Gold, Is.EqualTo(4));
        }

        // Catches: a Solo world's owner losing their gold, stash and satchel when the world rebinds to the owner's new key (the weapons already follow, A32).
        [Test]
        public void BagsRename_MovesTheOwnersEntryToTheNewKey()
        {
            var bags = new BagsSave();
            bags.Players.Add(new PlayerBagSave { Identity = 0, Id = "old", Gold = 9 });
            bags.Players.Add(new PlayerBagSave { Identity = 0, Id = "new", Gold = 1 });
            BagsSaveRules.Rename(bags, new PlayerKey(IdentityKind.Local, "old"), new PlayerKey(IdentityKind.Ugs, "new2"));
            Assert.That(bags.Players.Select(e => e.Identity + ":" + e.Id + ":" + e.Gold), Is.EqualTo(new[] { "1:new2:9", "0:new:1" }));
            BagsSaveRules.Rename(bags, new PlayerKey(IdentityKind.Local, "nobody"), new PlayerKey(IdentityKind.Local, "x"));
            Assert.That(bags.Players.Count, Is.EqualTo(2));
            BagsSaveRules.Rename(null, new PlayerKey(IdentityKind.Local, "a"), new PlayerKey(IdentityKind.Local, "b"));
        }
    }
}
