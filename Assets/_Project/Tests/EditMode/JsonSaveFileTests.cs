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

        // Catches (audit A9): a write that fails (the folder cannot be made, a locked file, a full disk) clearing the dirty mark, so the change is never retried
        // and the next flush finds "nothing to write"; or throwing into gameplay. The folder cannot be made here because a file sits where it should be.
        [Test]
        public void FailedWrite_StaysDirty_AndTheNextFlushWritesIt()
        {
            string blocker = Path.Combine(_dir, "blocker");
            System.IO.File.WriteAllText(blocker, "a file where the folder should be");
            string target = Path.Combine(blocker, "world.json");
            var f = new JsonSaveFile<WorldSave>(target, 1, s => s.SchemaVersion, () => WorldSaveRules.New(0xF15F15F1UL), null, "test file", "read-only");
            f.Load();
            f.Current.SavedAtUtc = "2026-11-14T10:00:00Z";
            f.MarkDirty();

            bool flushed = false;
            Assert.DoesNotThrow(() => flushed = f.FlushIfDirty());
            Assert.That(flushed, Is.True, "a write was due");
            Assert.That(f.IsDirty, Is.True, "and it failed, so it is still due");
            Assert.That(f.Save(), Is.False, "a direct save reports the failure too");

            System.IO.File.Delete(blocker);   // the fault clears
            Assert.That(f.FlushIfDirty(), Is.True);
            Assert.That(f.IsDirty, Is.False, "the retry wrote it");
            Assert.That(System.IO.File.ReadAllText(target), Does.Contain("2026-11-14T10:00:00Z"), "with the change made while the write was failing");
            Assert.That(f.FlushIfDirty(), Is.False, "and now there is nothing left to write");
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
