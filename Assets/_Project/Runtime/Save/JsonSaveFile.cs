// Runtime/Save/JsonSaveFile.cs: one JSON save file's load and save rules, shared by the meta save (SaveService, meta.json) and the world save
// (R89; WorldSaveStore). Extracted from SaveService (world save W2) so both files follow the same rules instead of two copies drifting:
//  - Load order main -> .bak -> defaults. The schemaVersion is probed first, so a file newer than this build loads read-only and is never written,
//    even when this build cannot deserialize it (then it is not treated as corrupt either).
//  - An unparsable main file is copied to path + ".corrupt" and removed before the first Save can push it into .bak and overwrite it.
//  - Writes go through AtomicFile (tmp, flush, File.Replace with .bak). Nothing ever throws into gameplay: load and save catch everything.
//  - Deferred writes: MarkDirty / FlushIfDirty; a failed write stays dirty for the next flush.
// Newtonsoft as M3-D30: camelCase property names, dictionary keys unchanged, unknown members ignored. Authority: local (the caller's file).
using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace SleepingCalamity.Runtime
{
    public sealed class JsonSaveFile<T> where T : class
    {
        public const string CorruptSuffix = ".corrupt";

        internal static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true },
            },
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,   // never append to the DTO's initialiser collections
            MissingMemberHandling = MissingMemberHandling.Ignore,      // members from a newer build are skipped, not fatal
            NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 32,
        };

        private readonly int _currentVersion;
        private readonly Func<T, int> _version;
        private readonly Func<T> _createDefault;
        private readonly Func<T, T> _normalize;
        private readonly Func<string, string> _upgrade;
        private readonly string _label, _readOnlyNote;
        private bool _unreadableOnDisk;
        private bool _keepBackupCopy;   // the loaded .bak must be kept as .bak.corrupt before the next write rotates over it
        private bool _writeWarned;

        // label names the file in the logs ("meta file"); readOnlyNote says what a newer file means for the session ("journal is read-only this
        // session"). normalize runs on every loaded or default value (fill nulls a JSON file may carry; it may return a replacement). upgrade
        // (optional) rewrites an older file's text to the current schema before it is deserialized (the migration chain); it must not throw.
        public JsonSaveFile(string filePath, int currentVersion, Func<T, int> version, Func<T> createDefault, Func<T, T> normalize, string label, string readOnlyNote,
                            Func<string, string> upgrade = null)
        {
            _upgrade = upgrade;
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath is required", nameof(filePath));
            FilePath = filePath;
            _currentVersion = currentVersion;
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _createDefault = createDefault ?? throw new ArgumentNullException(nameof(createDefault));
            _normalize = normalize ?? (v => v);
            _label = label ?? "save file";
            _readOnlyNote = readOnlyNote ?? "the file is read-only this session";
            Current = _normalize(_createDefault());
        }

        public string FilePath { get; }
        public T Current { get; private set; }
        public MetaLoadSource LoadedFrom { get; private set; } = MetaLoadSource.Defaults;

        // True when the file on disk has a newer schema than this build understands; Save does nothing then.
        public bool IsReadOnly { get; private set; }

        // The schema version the newer file on disk declared (0 when the file is not newer).
        public long NewerVersionOnDisk { get; private set; }

        public bool IsDirty { get; private set; }

        // Never throws: anything unexpected (a throwing normalize or version, an I/O fault the reads didn't catch) leaves the defaults loaded
        // and the session read-only, so nothing this build doesn't understand is overwritten.
        public void Load()
        {
            try
            {
                LoadUnguarded();
            }
            catch (Exception e)
            {
                Debug.LogWarning("SC SAVE " + _label + " failed to load (" + e.Message + "); using defaults, read-only this session (" + FilePath + ")");
                try { Current = _normalize(_createDefault()); }
                catch (Exception) { Current = _createDefault(); }
                LoadedFrom = MetaLoadSource.Defaults;
                IsReadOnly = true;
                NewerVersionOnDisk = 0;   // a load fault, not a newer file (the host flow's toast tells them apart)
                _unreadableOnDisk = false;
            }
        }

        private void LoadUnguarded()
        {
            _keepBackupCopy = false;
            string mainText = AtomicFile.ReadAllTextOrNull(FilePath);
            T loaded = DeserializeOrNull(mainText);
            MetaLoadSource source = MetaLoadSource.Main;
            _unreadableOnDisk = false;
            // A main file with a newer schemaVersion that this build cannot deserialize (a field changed type) is not corrupt: it is left
            // untouched on disk and the session runs read-only, so an older build never damages a newer build's save.
            bool newerUnparsed = false;
            long newerVersion = 0;

            if (loaded == null)
            {
                newerUnparsed = TryReadNewerSchemaVersion(mainText, _currentVersion, out newerVersion);
                string backupText = AtomicFile.ReadAllTextOrNull(FilePath + AtomicFile.BackupSuffix);
                loaded = DeserializeOrNull(backupText);
                // A main file that exists but did not load (corrupt, or locked so that it read as missing) is copied to .corrupt and removed
                // before the first Save, so it is neither rotated into .bak over a good backup nor replaced by an empty file.
                bool mainExists = File.Exists(FilePath);
                _unreadableOnDisk = mainExists && !newerUnparsed;
                if (newerUnparsed)
                {
                    source = loaded != null ? MetaLoadSource.Backup : MetaLoadSource.Defaults;
                }
                else if (loaded != null)
                {
                    source = MetaLoadSource.Backup;
                    Debug.LogWarning("SC SAVE main " + _label + " unreadable; loaded the .bak (" + FilePath + ")");
                }
                else
                {
                    source = MetaLoadSource.Defaults;
                    if (mainExists || backupText != null)
                    {
                        Debug.LogWarning("SC SAVE " + _label + " and .bak both unreadable; starting from defaults, the old file is kept as .corrupt on the next save (" + FilePath + ")");
                    }
                }
            }

            Current = _normalize(loaded ?? _createDefault());
            LoadedFrom = source;

            IsReadOnly = newerUnparsed || _version(Current) > _currentVersion;
            NewerVersionOnDisk = IsReadOnly ? (newerUnparsed ? newerVersion : _version(Current)) : 0;
            if (IsReadOnly)
            {
                Debug.LogWarning("SC SAVE " + _label + " schema " + NewerVersionOnDisk + " is newer than this build; " + _readOnlyNote + " (" + FilePath + ")");
            }

            Debug.Log("SC SAVE loaded " + source + " " + FilePath);
        }

        // Replaces the value. Marks it dirty; nothing is written until a Save or flush (and never in a read-only session: false then).
        public bool Replace(T value)
        {
            Current = _normalize(value ?? _createDefault());
            IsDirty = true;
            return !IsReadOnly;
        }

        // Stops all writes for the rest of the session (the file was moved aside: a new world starts next session).
        public void Suspend()
        {
            IsReadOnly = true;
            IsDirty = false;
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        // Saves only when something was marked dirty since the last write. Returns true when a save was due (written, failed and kept dirty for
        // a retry, or skipped because the session is read-only), false when there was nothing to write.
        public bool FlushIfDirty()
        {
            if (!IsDirty) return false;
            Save();
            return true;
        }

        // Writes the whole of Current. True when written (or read-only, which never writes and clears the mark); false on a failed write.
        public bool Save()
        {
            if (IsReadOnly)
            {
                IsDirty = false;
                return true;
            }
            try
            {
                string backup = FilePath + AtomicFile.BackupSuffix;
                if (_keepBackupCopy && File.Exists(backup))
                {
                    File.Copy(backup, backup + CorruptSuffix, true);   // before this write rotates main into .bak over it
                    _keepBackupCopy = false;
                }
                if (_unreadableOnDisk && File.Exists(FilePath))
                {
                    // A failed copy or delete aborts the write; the next Save retries. With the main file gone, AtomicFile moves the tmp file
                    // in and does not rotate the unreadable file into .bak.
                    File.Copy(FilePath, FilePath + CorruptSuffix, true);
                    File.Delete(FilePath);
                }
                _unreadableOnDisk = false;
                AtomicFile.WriteAllText(FilePath, Serialize(Current));
                IsDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                WarnWriteFailed(ex);   // Save also runs from OnDisable, coroutines and quitting: nothing may escape into gameplay
                return false;
            }
        }

        // Keeps a copy of the file the value was loaded from before the next write replaces it (a load that had to reset part of a parsed file):
        // the main file as .corrupt, or, when the value came from the .bak, the .bak as .bak.corrupt (the next write rotates main over it).
        public void KeepCorruptCopyOnNextSave()
        {
            if (IsReadOnly) return;
            if (LoadedFrom == MetaLoadSource.Backup) _keepBackupCopy = true;
            else if (File.Exists(FilePath)) _unreadableOnDisk = true;
        }

        public static string Serialize(T value) => JsonConvert.SerializeObject(value, JsonSettings);

        // null for empty text, malformed JSON, a non-object, or schemaVersion <= 0 (all count as unreadable). Never throws.
        public T DeserializeOrNull(string json)
        {
            if (_upgrade != null && json != null)
            {
                try { json = _upgrade(json); }
                catch (Exception) { return null; }   // a failed migration reads as unreadable (.bak, then defaults; the file kept as .corrupt)
            }
            return DeserializeOrNull(json, _version);
        }

        public static T DeserializeOrNull(string json, Func<T, int> version)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                T value = JsonConvert.DeserializeObject<T>(json, JsonSettings);
                return value != null && version(value) >= 1 ? value : null;
            }
            catch (Exception)
            {
                return null;   // loads run inside AppBootstrap.Compose and the host's session start: nothing may escape
            }
        }

        // True when json is a well-formed JSON object whose integer schemaVersion is greater than currentVersion (matched case-insensitively, as
        // Newtonsoft does when deserializing). Reads only the version, so it works when the rest of the file does not fit T.
        public static bool TryReadNewerSchemaVersion(string json, int currentVersion, out long version)
        {
            version = 0;
            if (!TryReadSchemaVersion(json, out long read)) return false;
            version = read;
            return read > currentVersion;
        }

        // The integer schemaVersion of a JSON object, read before anything else (R89: "schemaVersion is read first").
        public static bool TryReadSchemaVersion(string json, out long version)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                JObject root = JObject.Parse(json);
                foreach (JProperty p in root.Properties())
                {
                    if (!string.Equals(p.Name, "schemaVersion", StringComparison.OrdinalIgnoreCase)) continue;
                    if (p.Value.Type != JTokenType.Integer) return false;
                    version = (long)p.Value;
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;   // malformed, truncated or not an object: genuinely corrupt
            }
        }

        private void WarnWriteFailed(Exception ex)
        {
            if (_writeWarned) return;
            _writeWarned = true;
            Debug.LogWarning("SC SAVE write failed: " + ex.Message);
        }
    }
}
