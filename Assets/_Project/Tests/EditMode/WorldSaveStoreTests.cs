// Tests/EditMode/WorldSaveStoreTests.cs: the host's world file (Runtime/Save/WorldSaveStore): the pinned v1 format, the round trip, a first run,
// a bad section and a newer file. Ported from FishGame's world save tests (its fishing sections gone, the players section new).
// Every test works under its own directory below the OS temp path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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
            string bags = "{ \"players\": [ { \"identity\": 1, \"id\": \"aaa\", \"gold\": 5 }, { \"identity\": \"x\", \"id\": \"bbb\", \"gold\": 2 }, { \"identity\": 77, \"id\": \"ccc\", \"gold\": 1 } ] }";
            File.WriteAllText(_path, GoldenV1.Replace("\"players\"", "\"bags\": " + bags + ", \"players\""));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }), "named so the original is kept");
            Assert.That(store.Current.Bags.Players.Select(e => e.Id), Is.EqualTo(new[] { "aaa" }));
            Assert.That(store.Current.Bags.Players[0].Gold, Is.EqualTo(5));
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        const string BagsHead = "\"bags\": ";

        static string BagsFile(string playersJson) => GoldenV1.Replace("\"players\"", BagsHead + "{ \"players\": [ " + playersJson + " ] }, \"players\"");

        // Catches: a file written by today's code (WorldSaveStore's own serializer, camelCase, indented) not loading exactly after the bag rows changed. The text is committed as a fixture.
        const string TodaysBagsJson = @"{
      ""players"": [
        {
          ""identity"": 1,
          ""id"": ""aaa"",
          ""gold"": 7,
          ""stash"": [
            { ""itemId"": 104, ""count"": 3 },
            { ""itemId"": 110, ""count"": 1 }
          ],
          ""satchel"": {
            ""floor"": 2,
            ""items"": [ 110, 111 ]
          }
        },
        { ""identity"": 1, ""id"": ""bbb"", ""gold"": 0, ""stash"": [], ""satchel"": null }
      ]
    }";

        [Test]
        public void Bags_FileWrittenByTodaysCode_StillLoadsExactly()
        {
            File.WriteAllText(_path, GoldenV1.Replace("\"players\"", BagsHead + TodaysBagsJson + ", \"players\""));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.Current.Bags.Players.Select(e => e.Id), Is.EqualTo(new[] { "aaa", "bbb" }));
            PlayerBagSave a = store.Current.Bags.Players[0];
            Assert.That(a.Gold, Is.EqualTo(7));
            Assert.That(a.Stash.Select(r => r.ItemId + ":" + r.Count), Is.EqualTo(new[] { "104:3", "110:1" }));
            Assert.That(a.Satchel.Floor, Is.EqualTo(2)); Assert.That(a.Satchel.Items, Is.EqualTo(new[] { 110, 111 }));
        }

        // Catches: one mistyped value inside a bag entry costing the player's whole bag (gold, stash and satchel).
        [Test]
        public void Bags_BadGold_KeepsStashAndSatchel()
        {
            File.WriteAllText(_path, BagsFile("{ \"identity\": 1, \"id\": \"aaa\", \"gold\": \"lots\", \"stash\": [ { \"itemId\": 104, \"count\": 2 } ], \"satchel\": { \"floor\": 2, \"items\": [ 110 ] } }"));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }), "named so the original is kept");
            PlayerBagSave a = store.Current.Bags.Players.Single();
            Assert.That(a.Gold, Is.EqualTo(0));
            Assert.That(a.Stash.Single().ItemId, Is.EqualTo(104)); Assert.That(a.Satchel.Items, Is.EqualTo(new[] { 110 }));
        }

        [Test]
        public void Bags_BadStashRow_KeepsTheOtherRowsGoldAndSatchel()
        {
            File.WriteAllText(_path, BagsFile("{ \"identity\": 1, \"id\": \"aaa\", \"gold\": 9, \"stash\": [ { \"itemId\": 104, \"count\": 2 }, { \"itemId\": 105, \"count\": \"two\" }, { \"itemId\": 106, \"count\": 1 } ], \"satchel\": { \"floor\": 2, \"items\": [ 110 ] } }"));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }));
            PlayerBagSave a = store.Current.Bags.Players.Single();
            Assert.That(a.Gold, Is.EqualTo(9));
            Assert.That(a.Stash.Select(r => r.ItemId), Is.EqualTo(new[] { 104, 106 }));
            Assert.That(a.Satchel.Items, Is.EqualTo(new[] { 110 }));
        }

        [Test]
        public void Bags_ScalarStash_KeepsGoldAndSatchel()
        {
            File.WriteAllText(_path, BagsFile("{ \"identity\": 1, \"id\": \"aaa\", \"gold\": 9, \"stash\": 5, \"satchel\": { \"floor\": 2, \"items\": [ 110 ] } }"));
            WorldSaveStore store = Load();
            PlayerBagSave a = store.Current.Bags.Players.Single();
            Assert.That(a.Gold, Is.EqualTo(9)); Assert.That(a.Stash, Is.Empty); Assert.That(a.Satchel.Items, Is.EqualTo(new[] { 110 }));
        }

        [Test]
        public void Bags_BadSatchelItem_KeepsTheRestOfTheSatchelGoldAndStash()
        {
            File.WriteAllText(_path, BagsFile("{ \"identity\": 1, \"id\": \"aaa\", \"gold\": 9, \"stash\": [ { \"itemId\": 104, \"count\": 2 } ], \"satchel\": { \"floor\": 2, \"items\": [ 110, \"x\" ] } }"));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }));
            PlayerBagSave a = store.Current.Bags.Players.Single();
            Assert.That(a.Gold, Is.EqualTo(9)); Assert.That(a.Stash.Single().ItemId, Is.EqualTo(104)); Assert.That(a.Satchel.Items, Is.EqualTo(new[] { 110 }));
        }

        [Test]
        public void Bags_BadSatchelFloor_DropsOnlyTheSatchel()
        {
            File.WriteAllText(_path, BagsFile("{ \"identity\": 1, \"id\": \"aaa\", \"gold\": 9, \"stash\": [ { \"itemId\": 104, \"count\": 2 } ], \"satchel\": { \"floor\": \"two\", \"items\": [ 110 ] } }"));
            WorldSaveStore store = Load();
            PlayerBagSave a = store.Current.Bags.Players.Single();
            Assert.That(a.Gold, Is.EqualTo(9)); Assert.That(a.Stash.Single().ItemId, Is.EqualTo(104)); Assert.That(a.Satchel, Is.Null);
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

        // ---- plan 3 task 6: the dive section's v2 fields and the optional "suspend" section ----

        static SuspendSave FullSuspend() => new SuspendSave
        {
            Seed = uint.MaxValue, Floor = 5, Phase = (int)DivePhase.BankRoom, BankChosen = false, StartedAtBank = true, ClearedMask = 0b11111, ExtraSlotOwner = "Ugs:bbb",
            Stir = new SuspendStirSave { FloorMask = 0b1111, Valuables = 6, ValuablesBaseline = 2, SecondHalf = true, ExtraEnemy = true, Hunter = true, CollapseUsed = true, Running = true, SecondsLeft = 41.5f, RewardCarry = 12.25f },
            Momentum = new SuspendMomentumSave { Unbroken = true, PushedOn = false, WalkedToBank = true },
            Retry = new SuspendRetrySave { UsedThisDive = true, IdolActivated = true },
            PresentIds = { "Ugs:aaa", "Ugs:bbb" },
            Bags =
            {
                new SuspendedBag
                {
                    Identity = 1, Id = "aaa", Carried = { 104, 105 }, BankedThisDive = { new LootCountSave { ItemId = 101, Count = 2 } }, DiveGold = 17,
                    OpenedChests = (1UL << 63) | 5UL, ChestSeed = uint.MaxValue, ChestFloor = 3, CarriedSatchel = new SatchelSave { Floor = 3, Items = { 106 } }, Picks = { 1, PlayerBag.SatchelPick },
                },
                new SuspendedBag { Identity = 1, Id = "bbb", Carried = { 110 } },
            },
        };

        const string GoodBagJson = "{ \"identity\": 1, \"id\": \"aaa\", \"carried\": [104], \"diveGold\": 3 }";

        string WithSection(string name, string json) => GoldenV1.Replace("\"players\"", "\"" + name + "\": " + json + ", \"players\"");

        // A suspend section in a world whose dive is in progress (plan 3 task 8a: a suspend without a dive in progress is stale and the clean drops it).
        string WithSuspend(string json) => WithSection("suspend", json).Replace("\"players\"", "\"dive\": { \"inDive\": true }, \"players\"");

        // Catches: a build that can no longer read the v1 format because of the plan 3 fields, or the golden file gaining a suspend or dive section on load and save.
        [Test]
        public void GoldenV1Fixture_StillPassesUnchanged()
        {
            File.WriteAllText(_path, GoldenV1);
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.Empty);
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Current.Suspend, Is.Null);
            Assert.That(store.Current.Dive, Is.Null);
            store.MarkDirty(); store.FlushIfDirty();
            Assert.That(File.ReadAllText(_path), Does.Not.Contain("\"suspend\""), "a world with no suspend writes no suspend key");
            Assert.That(File.ReadAllText(_path), Does.Not.Contain("checkpointHeld").And.Not.Contain("pieceDepths"), "and no dive section appears for a world that has none");
            Assert.That(GoldenV1, Does.Not.Contain("suspend"), "and the fixture itself was not edited");
        }

        // Catches: the suspend section altered through the real file path (a uint seed, a ulong chest mask with bit 63, floats, nested states, lists), or Clean changing a good one.
        [Test]
        public void Suspend_RoundTripsThroughTheStore()
        {
            WorldSaveStore store = Load();
            WorldSave w = WorldSaveRules.New(7UL);
            w.Players = WorldSaveStoreTestsPlayers();
            w.Dive = new DiveSave { InDive = true };   // a suspend belongs to a dive in progress
            w.Suspend = FullSuspend();
            store.Replace(w);
            Assert.That(store.FlushIfDirty(), Is.True);
            Assert.That(File.ReadAllText(_path), Does.Contain("\"suspend\""));

            WorldSaveStore again = Load();
            Assert.That(again.Rejected, Is.Empty);
            SuspendSave back = again.Current.Suspend;
            Assert.That(back, Is.Not.Null);
            Assert.That(JsonConvert.SerializeObject(back), Is.EqualTo(JsonConvert.SerializeObject(FullSuspend())), "every field survives the file");
            Assert.That(back.Bags[0].OpenedChests, Is.EqualTo((1UL << 63) | 5UL));
            Assert.That(back.Seed, Is.EqualTo(uint.MaxValue));
            Assert.That(back.Stir.SecondsLeft, Is.EqualTo(41.5f));
        }

        // Catches: the dive section's new fields lost, rewritten or recomputed on the way through the file, and an old dive section (no new fields) being rejected.
        [Test]
        public void DiveV2Fields_RoundTrip_AndAnOldDiveSectionReadsAsNotHeld()
        {
            WorldSaveStore store = Load();
            WorldSave w = WorldSaveRules.New(7UL);
            w.Players = WorldSaveStoreTestsPlayers();
            w.Dive = new DiveSave { ClearedMask = 0b10_0001_0011, CheckpointHeld = true, BossBeaten = true, ExtraSlotOwner = "Ugs:aaa", PieceDepths = new List<PieceDepthSave> { new PieceDepthSave { Id = 100, Floor = 4 }, new PieceDepthSave { Id = 400, Floor = 3 } } };
            store.Replace(w);
            store.FlushIfDirty();
            WorldSaveStore again = Load();
            Assert.That(again.Rejected, Is.Empty);
            DiveSave d = again.Current.Dive;
            Assert.That(d.ClearedMask, Is.EqualTo(0b10_0001_0011));
            Assert.That(d.CheckpointHeld, Is.True); Assert.That(d.BossBeaten, Is.True); Assert.That(d.ExtraSlotOwner, Is.EqualTo("Ugs:aaa"));
            Assert.That(d.PieceDepths.Select(x => x.Id + ":" + x.Floor), Is.EqualTo(new[] { "100:4", "400:3" }));

            // OldSave_NoCheckpointField_ReadsAsNotHeld: a dive section written before these fields
            File.WriteAllText(_path, WithSection("dive", "{ \"inDive\": false, \"clearedMask\": 3, \"checkpointReached\": true }"));
            WorldSaveStore old = Load();
            Assert.That(old.Rejected, Is.Empty, "an old save is not damaged");
            Assert.That(old.Current.Dive.CheckpointHeld, Is.False);
            Assert.That(old.Current.Dive.BossBeaten, Is.False);
            Assert.That(old.Current.Dive.ExtraSlotOwner, Is.Null);
            Assert.That(old.Current.Dive.PieceDepths, Is.Null, "missing, so the host assigns them once and saves them");
            Assert.That(old.Current.Dive.CheckpointReached, Is.False, "the old 4-floor checkpoint reads as not held (task 12)");
            Assert.That(old.Current.Dive.ClearedMask, Is.EqualTo(3));
        }

        // Catches (PARKING_LOT :66, the 3-player extra-slot owner lives in dive.extraSlotOwner): a bad owner value costing the rest of the dive section or the file. A malformed id drops only the owner.
        [Test]
        public void DiveExtraSlotOwner_BadValue_KeepsTheRestOfTheDiveSectionAndFile()
        {
            foreach (string bad in new[] { "\"not-a-bag-id\"", "\"Nope:abc\"", "5", "\"\"" })
            {
                File.WriteAllText(_path, WithSection("dive", "{ \"inDive\": false, \"checkpointHeld\": true, \"bossBeaten\": true, \"extraSlotOwner\": " + bad + " }"));
                WorldSaveStore store = Load();
                Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main), bad);
                Assert.That(store.Current.Dive.ExtraSlotOwner, Is.Null, bad);
                Assert.That(store.Current.Dive.CheckpointHeld, Is.True, bad + ": the rest of the dive section is kept");
                Assert.That(store.Current.Dive.BossBeaten, Is.True, bad);
            }
        }

        // Catches (sc-netcode "World save"): a mistyped suspend section failing the whole WorldSave parse, so the world rolls back to .bak.
        [Test]
        public void Suspend_MistypedSection_KeepsTheRestOfTheFile()
        {
            File.WriteAllText(_path, WithSection("suspend", "\"oops\""));
            WorldSaveStore store = Load();
            Assert.That(store.LoadedFrom, Is.EqualTo(MetaLoadSource.Main));
            Assert.That(store.Rejected, Is.EqualTo(new[] { TownSaveRules.SectionSuspend }));
            Assert.That(store.Current.Suspend, Is.Null);
            Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2));
        }

        // Catches: one mistyped bag record (a string where the dive gold is) costing the other players' records, the section, or the file.
        [Test]
        public void Suspend_OneMistypedBag_KeepsTheOthers()
        {
            string section = "{ \"seed\": 5, \"floor\": 5, \"phase\": 2, \"presentIds\": [\"Ugs:aaa\", 7, \"Ugs:bbb\"], \"bags\": [ " + GoodBagJson + ", { \"identity\": 1, \"id\": \"bbb\", \"diveGold\": \"lots\" }, { \"identity\": 1, \"id\": \"ccc\", \"carried\": [110] } ] }";
            File.WriteAllText(_path, WithSuspend(section));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { TownSaveRules.SectionSuspend }), "named so the original file is kept");
            SuspendSave s = store.Current.Suspend;
            Assert.That(s, Is.Not.Null);
            Assert.That(s.Bags.Select(b => b.Id), Is.EqualTo(new[] { "aaa", "ccc" }));
            Assert.That(s.Bags[0].Carried, Is.EqualTo(new[] { 104 }));
            Assert.That(s.PresentIds, Is.EqualTo(new[] { "Ugs:aaa", "Ugs:bbb" }), "the mistyped present id went alone");
            Assert.That(s.Floor, Is.EqualTo(5));
        }

        // Catches: a bad floor (a string, or out of range) in the suspend section taking the dive and bags sections with it.
        [Test]
        public void Suspend_BadFloor_DropsOnlyTheSuspend()
        {
            foreach (string floor in new[] { "\"five\"", "99" })
            {
                string suspend = "{ \"seed\": 5, \"floor\": " + floor + ", \"phase\": 2, \"bags\": [ " + GoodBagJson + " ] }";
                string text = GoldenV1.Replace("\"players\"", "\"suspend\": " + suspend + ", \"dive\": { \"clearedMask\": 3, \"inDive\": true }, \"bags\": { \"players\": [ { \"identity\": 1, \"id\": \"aaa\", \"gold\": 6 } ] }, \"players\"");
                File.WriteAllText(_path, text);
                WorldSaveStore store = Load();
                Assert.That(store.Rejected, Does.Contain(TownSaveRules.SectionSuspend), floor);
                Assert.That(store.Current.Suspend, Is.Null, floor);
                Assert.That(store.Current.Dive.ClearedMask, Is.EqualTo(3), floor);
                // the dropped suspend's dive record is settled as a failed dive: half of its 3 dive gold (1) kept, the carried 104 waits in the satchel
                PlayerBagSave bag = store.Current.Bags.Players.Single();
                Assert.That(bag.Gold, Is.EqualTo(7), floor);
                Assert.That(bag.Satchel.Items, Is.EqualTo(new[] { 104 }), floor);
                Assert.That(store.Current.Players.Recorded.Count, Is.EqualTo(2), floor);
            }
        }

        // Catches: one mistyped new member of the dive section (checkpointHeld) or one mistyped piece depth costing the dive section, whose loss would put every piece back in stock.
        [Test]
        public void Dive_MistypedNewMember_AndOneBadDepth_KeepTheRest()
        {
            string dive = "{ \"clearedMask\": 3, \"checkpointHeld\": \"yes\", \"bossBeaten\": true, \"pieceDepths\": [ { \"id\": 100, \"floor\": 4 }, { \"id\": 101, \"floor\": \"deep\" }, { \"id\": 400, \"floor\": 3 } ] }";
            File.WriteAllText(_path, WithSection("dive", dive));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { TownSaveRules.SectionDive }));
            DiveSave d = store.Current.Dive;
            Assert.That(d.ClearedMask, Is.EqualTo(3));
            Assert.That(d.CheckpointHeld, Is.False, "the mistyped member alone is gone");
            Assert.That(d.BossBeaten, Is.True);
            Assert.That(d.PieceDepths.Select(x => x.Id), Is.EqualTo(new[] { 100, 400 }));
        }

        // Catches: a scalar where a new list belongs ("pieceDepths": "oops") deleting the whole dive section (its banked pieces and cleared floors with it), or the suspend section.
        [Test]
        public void NewListFields_HoldingAScalar_CostOnlyThatField()
        {
            File.WriteAllText(_path, WithSection("dive", "{ \"clearedMask\": 3, \"bankedPieces\": [100], \"pieceDepths\": \"oops\" }"));
            WorldSaveStore store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { TownSaveRules.SectionDive }));
            Assert.That(store.Current.Dive, Is.Not.Null, "the dive section stays");
            Assert.That(store.Current.Dive.ClearedMask, Is.EqualTo(3));
            Assert.That(store.Current.Dive.BankedPieces, Is.EqualTo(new[] { 100 }));
            Assert.That(store.Current.Dive.PieceDepths, Is.Null);

            File.WriteAllText(_path, WithSuspend("{ \"seed\": 5, \"floor\": 5, \"phase\": 2, \"presentIds\": \"oops\", \"bags\": 7 }"));
            store = Load();
            Assert.That(store.Rejected, Is.EqualTo(new[] { TownSaveRules.SectionSuspend }));
            Assert.That(store.Current.Suspend, Is.Not.Null, "the suspend section stays");
            Assert.That(store.Current.Suspend.Floor, Is.EqualTo(5));
            Assert.That(store.Current.Suspend.Bags, Is.Empty);
            Assert.That(store.Current.Suspend.PresentIds, Is.Empty);
        }

        static WorldPlayersSave WorldSaveStoreTestsPlayers()
        {
            WorldPlayers.TryCreate(WorldKind.Multiplayer, new[] { new PlayerKey(IdentityKind.Ugs, "aaa"), new PlayerKey(IdentityKind.Ugs, "bbb") }, out WorldPlayers players);
            return players.ToSave();
        }

        static HostWorld HostFor(WorldKind kind, params string[] ids)
        {
            var host = new HostWorld();
            var keys = new PlayerKey[ids.Length];
            for (int i = 0; i < ids.Length; i++) keys[i] = new PlayerKey(IdentityKind.Ugs, ids[i]);
            WorldPlayers.TryCreate(kind, keys, out WorldPlayers players);
            host.Load(players);
            return host;
        }

        sealed class SnapshotSource : HostWorld.ITownSource
        {
            public TownSnapshot Live;
            public TownSnapshot CaptureTown() => Live;
            public void RestoreTown(TownSnapshot loaded) { Restored = loaded; }
            public TownSnapshot Restored;
        }

        static WorldSave OldDiveWorld(ulong seed) => new WorldSave
        {
            WorldSeed = WorldSaveRules.SeedToHex(seed),
            Dive = new DiveSave { ClearedMask = 1, BankedPieces = { 100 } },   // a dive section from before the plan 3 fields
        };

        // Catches: depths recomputed instead of saved (an old save assigned twice, a saved map replaced by the seed's), or a dive section invented for a world that has none
        // (Reconcile would then read every piece as lost and hand them out for free).
        [Test]
        public void HostWorld_PieceDepths_AssignedOnceThenSaved_NeverRecomputed()
        {
            ulong seed = 1UL;
            HostWorld host = HostFor(WorldKind.Multiplayer, "aaa", "bbb");
            WorldSave file = OldDiveWorld(seed);
            host.LoadTown(file);
            IReadOnlyDictionary<int, int> assigned = PieceDepths.Assign(seed, PieceCatalog.Specs);
            Assert.That(host.PieceFloors.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + kv.Value), Is.EqualTo(assigned.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + kv.Value)), "an old save with no depths: assigned from the seed");
            Assert.That(file.Dive.PieceDepths, Is.Null, "loading does not write the file");

            Assert.That(host.CaptureTownInto(file), Is.True);
            Assert.That(file.Dive.PieceDepths.Count, Is.EqualTo(PieceCatalog.Specs.Count), "the first save writes them");
            Assert.That(file.Dive.BankedPieces, Is.EqualTo(new[] { 100 }), "and nothing else of the dive section moved");

            // a later load under a seed that would assign differently keeps the saved map
            ulong other = 2UL;
            while (other < 200 && PieceDepths.Assign(other, PieceCatalog.Specs).All(kv => assigned[kv.Key] == kv.Value)) other++;
            Assert.That(PieceDepths.Assign(other, PieceCatalog.Specs).Any(kv => assigned[kv.Key] != kv.Value), Is.True, "premise: another seed assigns differently");
            file.WorldSeed = WorldSaveRules.SeedToHex(other);
            var again = HostFor(WorldKind.Multiplayer, "aaa", "bbb");
            again.LoadTown(file);
            foreach (KeyValuePair<int, int> kv in assigned) Assert.That(again.PieceFloors[kv.Key], Is.EqualTo(kv.Value), "piece " + kv.Key);

            // a world with no dive section at all: depths are known, but no section is made up
            var fresh = HostFor(WorldKind.Multiplayer, "aaa", "bbb");
            var blank = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(seed) };
            fresh.LoadTown(blank);
            Assert.That(fresh.PieceFloors.Count, Is.EqualTo(PieceCatalog.Specs.Count));
            Assert.That(fresh.Town.Dive, Is.Null);
            fresh.CaptureTownInto(blank);
            Assert.That(blank.Dive, Is.Null, "no dive section invented");
            Assert.That(blank.Stock, Is.Null);
        }

        // Catches: a saved frame depth of 4 (pre-D1 worlds put the frame on floor 4) being cleaned away or re-derived to the new floor 2 on the real load path, or lost at the next capture.
        [Test]
        public void HostWorld_SavedFrameOnFloor4_SurvivesLoadAndRecapture()
        {
            ulong seed = 1UL;
            Assert.That(PieceDepths.Assign(seed, PieceCatalog.Specs)[PieceCatalog.FrameId], Is.EqualTo(2), "premise: the seed alone would put the frame on floor 2");
            var depths = new List<PieceDepthSave>();
            foreach (PieceSpec s in PieceCatalog.Specs)
                depths.Add(new PieceDepthSave { Id = s.Id, Floor = s.Id == PieceCatalog.FrameId ? 4 : PieceDepths.Assign(seed, PieceCatalog.Specs)[s.Id] });
            WorldSave file = OldDiveWorld(seed);
            file.Dive.PieceDepths = depths;

            HostWorld host = HostFor(WorldKind.Multiplayer, "aaa", "bbb");
            host.LoadTown(file);   // CleanSnapshot, then ResolvePieceDepths
            Assert.That(host.PieceFloors[PieceCatalog.FrameId], Is.EqualTo(4), "the saved depth wins over the seed");

            Assert.That(host.CaptureTownInto(file), Is.True);
            Assert.That(file.Dive.PieceDepths.Single(p => p.Id == PieceCatalog.FrameId).Floor, Is.EqualTo(4), "re-capture still holds 100:4");
        }

        // Catches: a live capture (which knows none of the saved-only fields) wiping the saved depths, held checkpoint or extra-slot owner, or the saved suspend section.
        [Test]
        public void HostWorld_LegacySource_KeepsTheSavedV2FieldsAndSuspend()
        {
            HostWorld host = HostFor(WorldKind.Multiplayer, "aaa", "bbb");
            WorldSave file = OldDiveWorld(1UL);
            file.Dive.CheckpointHeld = true; file.Dive.BossBeaten = true; file.Dive.ExtraSlotOwner = "Ugs:bbb";
            file.Suspend = FullSuspend();
            file.Dive.InDive = true;   // a suspend belongs to a dive in progress
            host.LoadTown(file);
            Assert.That(host.Town.Suspend, Is.Not.Null);
            var source = new SnapshotSource { Live = new TownSnapshot { Dive = new DiveSave { ClearedMask = 7, InDive = true } } };   // the hub's capture: no suspend, no v2 fields, the party still below
            host.RegisterTownSource(source);
            Assert.That(source.Restored.Suspend, Is.Not.Null, "the restoring source sees the loaded suspend");
            Assert.That(source.Restored.Dive.PieceDepths, Is.Not.Null.And.Count.EqualTo(PieceCatalog.Specs.Count), "and the depths");

            var target = new WorldSave { WorldSeed = file.WorldSeed };
            host.CaptureTownInto(target);
            Assert.That(target.Dive.ClearedMask, Is.EqualTo(7), "the live progress wins");
            Assert.That(target.Dive.CheckpointHeld, Is.True);
            Assert.That(target.Dive.BossBeaten, Is.True);
            Assert.That(target.Dive.ExtraSlotOwner, Is.EqualTo("Ugs:bbb"));
            Assert.That(target.Dive.PieceDepths.Count, Is.EqualTo(PieceCatalog.Specs.Count));
            Assert.That(target.Suspend, Is.Not.Null, "a source that says nothing about the suspend keeps the saved one while its dive is in progress");
            Assert.That(target.Suspend.Bags.Count, Is.EqualTo(2));

            // the same source once the dive is over (plan 3 task 8a): a source that never says "known, none" can no longer keep a stale suspend alive
            source.Live = new TownSnapshot { Dive = new DiveSave { ClearedMask = 0, InDive = false } };
            host.CaptureTownInto(target);
            Assert.That(target.Suspend, Is.Null, "no dive in progress: the saved suspend is stale and goes");
        }

        // Catches: the suspend section not reaching the file from a live source, an unrecorded player's record reaching it, a used suspend never being cleared, or being cleared by a source
        // that merely said nothing.
        [Test]
        public void HostWorld_Suspend_WritesRecordedPlayersOnly_AndKnownNoneClearsIt()
        {
            HostWorld host = HostFor(WorldKind.Multiplayer, "aaa", "bbb");
            host.LoadTown(new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL) });
            SuspendSave live = FullSuspend();
            live.Bags.Add(new SuspendedBag { Identity = 1, Id = "stranger", Carried = { 104 } });
            live.PresentIds.Add("Ugs:stranger");
            var source = new SnapshotSource { Live = new TownSnapshot { Suspend = live, SuspendKnown = true, Dive = new DiveSave { InDive = true } } };
            host.RegisterTownSource(source);

            var target = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL) };
            Assert.That(host.CaptureTownInto(target), Is.True);
            Assert.That(target.Suspend, Is.Not.Null);
            Assert.That(target.Suspend.Bags.Select(b => b.Id), Is.EqualTo(new[] { "aaa", "bbb" }), "the stranger's record does not reach the file");
            Assert.That(target.Suspend.PresentIds, Is.EqualTo(new[] { "Ugs:aaa", "Ugs:bbb" }));
            Assert.That(live.Bags.Count, Is.EqualTo(3), "the source's own section is not changed");

            source.Live = new TownSnapshot();   // says nothing: keeps it
            host.CaptureTownInto(target);
            Assert.That(target.Suspend, Is.Not.Null);

            source.Live = new TownSnapshot { SuspendKnown = true };   // known: none (the suspend was used)
            Assert.That(host.CaptureTownInto(target), Is.True);
            Assert.That(target.Suspend, Is.Null, "a used suspend is gone from the file");
            Assert.That(host.CaptureTownInto(target), Is.False, "and nothing more to write");
        }

        // Catches: a Solo world's owner losing the suspend record, present id, extra-slot ownership or dive-section owner when the world rebinds to the owner's new key.
        [Test]
        public void HostWorld_SoloRebind_MovesTheSuspendOwner()
        {
            var host = new HostWorld();
            WorldPlayers.TryCreate(WorldKind.Solo, new[] { new PlayerKey(IdentityKind.Local, "old") }, out WorldPlayers players);
            host.Load(players);
            var file = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(1UL), Dive = new DiveSave { ExtraSlotOwner = "Local:old", InDive = true } };
            file.Suspend = new SuspendSave
            {
                Seed = 1u, Floor = 5, Phase = (int)DivePhase.BankRoom, PresentIds = { "Local:old" }, ExtraSlotOwner = "Local:old",
                Bags = { new SuspendedBag { Identity = 0, Id = "old", Carried = { 104 } } },
            };
            host.LoadTown(file);
            Assert.That(host.Admit(new PlayerKey(IdentityKind.Local, "new"), 0UL, true), Is.EqualTo(WorldPlayerVerdict.Ok));
            Assert.That(host.Town.Suspend.Bags.Single().Id, Is.EqualTo("new"));
            Assert.That(host.Town.Suspend.PresentIds, Is.EqualTo(new[] { "Local:new" }));
            Assert.That(host.Town.Suspend.ExtraSlotOwner, Is.EqualTo("Local:new"));
            Assert.That(host.Town.Dive.ExtraSlotOwner, Is.EqualTo("Local:new"));
        }
    }
}
