// Tests/EditMode/JsonSaveFileTests.cs: the parts of JsonSaveFile<T> the world store doesn't exercise directly: Replace, the corrupt copy of a
// parsed-but-reset file, and the plain schemaVersion read. The shared load and save rules are pinned through WorldSaveStoreTests. Every test works under its own directory below the OS temp path.
using System;
using System.IO;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using NUnit.Framework;

namespace SleepingCalamity.Tests.EditMode
{
    public class JsonSaveFileTests
    {
        private string _dir, _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "sc-jsonsave-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "world.json");
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private JsonSaveFile<WorldSave> File() => new JsonSaveFile<WorldSave>(_path, 1, s => s.SchemaVersion, () => WorldSaveRules.New(0xF15F15F1UL), null, "test file", "read-only");

        // Catches: Replace not marking the file dirty (a new world never written), or a null replacement leaving Current null.
        [Test]
        public void Replace_MarksDirty_NullIsTheDefault()
        {
            JsonSaveFile<WorldSave> f = File();
            f.Load();
            Assert.That(f.IsDirty, Is.False);
            f.Replace(null);
            Assert.That(f.Current, Is.Not.Null);
            Assert.That(f.IsDirty, Is.True);
            Assert.That(f.FlushIfDirty(), Is.True);
            Assert.That(System.IO.File.Exists(_path), Is.True, "written");
        }

        // Catches: a parsed file whose sections the caller had to reset being overwritten without a copy (the player's progress lost for good).
        [Test]
        public void KeepCorruptCopyOnNextSave_CopiesTheOriginalBeforeTheWrite()
        {
            JsonSaveFile<WorldSave> f = File();
            System.IO.File.WriteAllText(_path, "{\"schemaVersion\": 1, \"worldSeed\": \"00000000DEADBEEF\", \"oddField\": 3}");
            f.Load();
            f.KeepCorruptCopyOnNextSave();
            Assert.That(f.Save(), Is.True);
            Assert.That(System.IO.File.ReadAllText(_path + JsonSaveFile<WorldSave>.CorruptSuffix), Does.Contain("oddField"), "the original kept as .corrupt");
        }

        // Catches: the version probe reading a string or a nested field as the schema, or throwing on junk.
        [Test]
        public void TryReadSchemaVersion_OnlyATopLevelInteger()
        {
            Assert.That(JsonSaveFile<WorldSave>.TryReadSchemaVersion("{\"schemaVersion\": 3}", out long v) && v == 3, Is.True);
            Assert.That(JsonSaveFile<WorldSave>.TryReadSchemaVersion("{\"SchemaVersion\": 1}", out v) && v == 1, Is.True, "case-insensitive, as Newtonsoft");
            Assert.That(JsonSaveFile<WorldSave>.TryReadSchemaVersion("{\"schemaVersion\": \"3\"}", out _), Is.False);
            Assert.That(JsonSaveFile<WorldSave>.TryReadSchemaVersion("{\"clock\": {\"schemaVersion\": 3}}", out _), Is.False);
            Assert.That(JsonSaveFile<WorldSave>.TryReadSchemaVersion("{\"schemaVersion\": ", out _), Is.False);
            Assert.That(JsonSaveFile<WorldSave>.TryReadSchemaVersion(null, out _), Is.False);
        }

        // Catches (W1+W2 review): a throwing normalize (or anything else unexpected in a load) escaping into gameplay, or the session then
        // writing over a file it couldn't make sense of.
        [Test]
        public void Load_NeverThrows_AndGoesReadOnlyOnTheUnexpected()
        {
            System.IO.File.WriteAllText(_path, "{\"schemaVersion\": 1, \"worldSeed\": \"00000000DEADBEEF\"}");
            int calls = 0;
            var f = new JsonSaveFile<WorldSave>(_path, 1, s => s.SchemaVersion, () => WorldSaveRules.New(0xF15F15F1UL),
                s => { if (++calls > 1) throw new InvalidOperationException("boom"); return s; }, "test file", "read-only");
            Assert.DoesNotThrow(() => f.Load());
            Assert.That(f.Current, Is.Not.Null);
            Assert.That(f.IsReadOnly, Is.True);
            string before = System.IO.File.ReadAllText(_path);
            f.MarkDirty();
            f.FlushIfDirty();
            Assert.That(System.IO.File.ReadAllText(_path), Is.EqualTo(before), "never written");
        }
    }
}
