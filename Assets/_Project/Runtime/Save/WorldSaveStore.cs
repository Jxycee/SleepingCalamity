// Runtime/Save/WorldSaveStore.cs: the host's world save file, world{suffix}.json (ported from FishGame with its fishing sections
// removed). One world per machine profile in v1. The file rules are JsonSaveFile's (main, .bak, defaults; schemaVersion first, a
// newer file read-only and never written; .corrupt; atomic writes; nothing thrown). On top of them: an older file goes through the migration
// chain (Upgrade) before it is read; each section is then tried on its own (Prepare: a section whose JSON doesn't fit its type, "bits": -1 or
// "day": "3", is dropped, so it can't make the whole file unreadable); and every loaded value goes through WorldSaveRules.Clean, so a bad
// section resets only itself. When one does, the file it came from is kept (.corrupt, or .bak.corrupt) and one warning names the sections.
// Authority: host only. Only the hosting peer opens its world; a client joining a friend's world never touches its own file.
using System;
using System.Collections.Generic;
using System.IO;
using SleepingCalamity.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SleepingCalamity.Runtime
{
    public sealed class WorldSaveStore
    {
        public const string WorldBaseName = "world";

        private readonly JsonSaveFile<WorldSave> _file;
        private readonly ulong _defaultSeed;
        private readonly string _filePath;
        private readonly List<string> _rejected = new List<string>();
        private readonly List<string> _scratch = new List<string>();
        private readonly List<string> _parseRejected = new List<string>();   // the last Prepare's dropped sections
        private readonly List<string> _droppedEntries = new List<string>();  // the last Prepare's dropped map entries ("section.key")

        // The v1 sections, each read on its own in Prepare.
        static readonly (string key, Type type, string section)[] Sections =
        {
            ("worldSeed", typeof(string), WorldSaveRules.SectionSeed),
            ("savedAtUtc", typeof(string), "savedAtUtc"),
            ("worldId", typeof(string), WorldSaveRules.SectionWorldId),
            ("createdAtUtc", typeof(string), WorldSaveRules.SectionCreatedAt),
            ("players", typeof(WorldPlayersSave), WorldSaveRules.SectionPlayers),
        };   // every optional WorldSave section needs its row here, or one misfit value costs the whole file (WorldSaveStoreTests checks)

        // The sections every field of WorldSave maps to (by its JSON key), for the test that no section is missing from the table.
        internal static IEnumerable<(string key, Type type)> SectionKeys()
        {
            foreach ((string key, Type type, string _) in Sections) yield return (key, type);
        }
        // Sections that hold an array of records: a record that does not fit is dropped alone (section key, array key, element type).
        static readonly (string section, string array, Type element)[] ArraySections =
        {
            ("players", "recorded", typeof(WorldPlayerSave)),
        };

        // Members of a section that are checked one by one: a member that does not fit is dropped alone (section key, member key, type). A dropped
        // "kind" leaves the section without one, which WorldSaveRules rejects (a missing kind is never read as Solo).
        static readonly (string section, string member, Type type)[] MemberSections =
        {
            ("players", "kind", typeof(int)),
            ("players", "identity", typeof(int)),
            ("players", "recorded", typeof(List<WorldPlayerSave>)),
        };

        public WorldSaveStore(string filePath, ulong defaultSeed)
        {
            _defaultSeed = defaultSeed;
            _filePath = filePath;   // before the file object: its constructor already normalizes a default value through Clean
            _file = new JsonSaveFile<WorldSave>(filePath, WorldSave.CurrentSchemaVersion, w => w.SchemaVersion, () => WorldSaveRules.New(_defaultSeed),
                Clean, "world file", "the world is read-only this session", Prepare);
        }

        // persistentDataPath is shared by MPPM clones and test runs, so the per-instance suffix keeps their files apart (as meta's).
        public static string PathForThisInstance() =>
            PathOverrideForTests ?? Path.Combine(Application.persistentDataPath, WorldBaseName + InstanceProfile.Current.Suffix + ".json");

        // PlayMode tests point the host flow at a temp file, so a run from the Test Runner window (no -dev-profile sctest: the suffix is "")
        // never reads, deletes or overwrites the user's real world. Null outside tests; reset for every play session.
        public static string PathOverrideForTests { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PathOverrideForTests = null;

        public static WorldSaveStore CreateForThisInstance(ulong defaultSeed) => new WorldSaveStore(PathForThisInstance(), defaultSeed);

        public string FilePath => _file.FilePath;
        public WorldSave Current => _file.Current;
        public MetaLoadSource LoadedFrom => _file.LoadedFrom;
        public bool IsReadOnly => _file.IsReadOnly;
        // > 0 when the session is read-only because a newer build wrote the file; 0 for a read-only load fault (JsonSaveFile.Load's guard).
        public long NewerVersionOnDisk => _file.NewerVersionOnDisk;
        public bool IsDirty => _file.IsDirty;

        // The sections the last Load had to reset (empty for a good file).
        public IReadOnlyList<string> Rejected => _rejected;

        public void Load()
        {
            _rejected.Clear();
            _parseRejected.Clear();
            _file.Load();
            if (LoadedFrom != MetaLoadSource.Defaults)   // the last Prepare was of the text that loaded (main, else the .bak)
                foreach (string section in _parseRejected)
                    if (!_rejected.Contains(section)) _rejected.Add(section);
            if (_rejected.Count == 0) return;
            _file.KeepCorruptCopyOnNextSave();
            string entries = _droppedEntries.Count > 0 && LoadedFrom != MetaLoadSource.Defaults ? " (entries " + string.Join(", ", _droppedEntries) + ")" : "";
            Debug.LogWarning("SC SAVE world file: reset " + string.Join(", ", _rejected) + entries + " (unreadable values); the original is kept as .corrupt on the next save (" + FilePath + ")");
        }

        public void MarkDirty() => _file.MarkDirty();
        public bool FlushIfDirty() => _file.FlushIfDirty();
        public bool Save() => _file.Save();

        // Replaces the world (tests; false in a read-only session, where it is never written).
        public bool Replace(WorldSave world) => _file.Replace(world);

        public const string RetiredSuffix = ".old";

        // Where the last Retire put the world file (world.json.old-<UTC stamp>), or null.
        public string LastRetiredPath { get; private set; }

        // The dev New world button (W6): the world file and its .bak move aside to world.json.old-<UTC stamp> (and that plus .bak), one stamp
        // per press, so a second press, or a newer build's read-only file, never replaces a world set aside earlier; this session writes
        // nothing more, so the next hosted session starts a new world. Never deletes a world; never throws.
        public bool Retire()
        {
            LastRetiredPath = null;
            try
            {
                string bak = FilePath + AtomicFile.BackupSuffix;
                string retired = FreeRetiredPath(DateTime.UtcNow);
                if (File.Exists(FilePath)) { File.Move(FilePath, retired); LastRetiredPath = retired; }
                if (File.Exists(bak)) { File.Move(bak, retired + AtomicFile.BackupSuffix); LastRetiredPath = retired; }
                _file.Suspend();
                return true;
            }
            catch (Exception e)
            {
                if (LastRetiredPath != null && !File.Exists(FilePath)) _file.Suspend();   // the main file already moved: never write this world back
                Debug.LogWarning("SC SAVE world file: could not move it aside for a new world (" + e.Message + "); kept as it is (" + FilePath + ")");
                return false;
            }
        }

        // world.json.old-20261025T210000Z, then -2, -3 ... if that name (or its .bak) is taken.
        private string FreeRetiredPath(DateTime utc)
        {
            string stem = FilePath + RetiredSuffix + "-" + utc.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
            string path = stem;
            for (int n = 2; File.Exists(path) || File.Exists(path + AtomicFile.BackupSuffix); n++) path = stem + "-" + n;
            return path;
        }

        // JsonSaveFile's normalize: every loaded or default value is cleaned; the default (no file) rejects nothing.
        private WorldSave Clean(WorldSave save)
        {
            WorldSave clean = WorldSaveRules.Clean(save, _defaultSeed, _scratch);
            _rejected.AddRange(_scratch);
            // The file's name is the world's id (a hand-renamed copy is a different world); a name that is not an id (the old world.json) leaves it blank.
            string nameId = Path.GetFileNameWithoutExtension(_filePath);
            if (WorldIds.IsValid(nameId)) clean.WorldId = nameId;
            return clean;
        }

        // JsonSaveFile's upgrade hook: the migration chain, then each section tried against its type, a misfit dropped (and named). A newer file,
        // or text that isn't a JSON object, passes through for JsonSaveFile to judge. Never throws.
        internal string Prepare(string json)
        {
            _parseRejected.Clear();
            _droppedEntries.Clear();
            string upgraded = Upgrade(json);
            if (!JsonSaveFile<WorldSave>.TryReadSchemaVersion(upgraded, out long version) || version > WorldSave.CurrentSchemaVersion) return upgraded;
            JObject root;
            try { root = JObject.Parse(upgraded); }
            catch (Exception) { return upgraded; }
            bool dropped = false;
            foreach ((string key, Type type, string section) in Sections)
            {
                JProperty p = root.Property(key, StringComparison.OrdinalIgnoreCase);
                if (p == null || p.Value.Type == JTokenType.Null) continue;
                if (type == typeof(Dictionary<string, int>) && p.Value is JObject map && DropMisfitEntries(map, key, _droppedEntries))
                {
                    _parseRejected.Add(section);   // a map's bad entry goes alone, named like a dropped section
                    dropped = true;
                }
                if (p.Value is JObject obj && DropMisfitRecords(obj, key, _droppedEntries))
                {
                    _parseRejected.Add(section);   // a record array's bad element goes alone (one mistyped player keeps the others)
                    dropped = true;
                }
                if (p.Value is JObject members && DropMisfitMembers(members, key, _droppedEntries))
                {
                    _parseRejected.Add(section);   // one mistyped member (identity, recorded) costs only itself
                    dropped = true;
                }
                if (!Fits(p.Value, type))
                {
                    p.Remove();
                    _parseRejected.Add(section);
                    dropped = true;
                }
            }
            return dropped ? root.ToString() : upgraded;
        }

        // The same text path the real load takes (JsonConvert on the token's JSON with the store's settings), so what passes here also loads:
        // JToken.ToObject would round 1.5 into an int field where the file load throws, and the whole file would then be unreadable.
        private static bool Fits(JToken token, Type type)
        {
            try
            {
                JsonConvert.DeserializeObject(token.ToString(Formatting.None), type, JsonSaveFile<WorldSave>.JsonSettings);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Removes the registered members of a section whose JSON does not fit their type. True when any went.
        private static bool DropMisfitMembers(JObject section, string sectionKey, List<string> droppedInto)
        {
            bool any = false;
            foreach ((string sectionName, string member, Type type) in MemberSections)
            {
                if (!string.Equals(sectionName, sectionKey, StringComparison.OrdinalIgnoreCase)) continue;
                JProperty property = section.Property(member, StringComparison.OrdinalIgnoreCase);
                if (property == null || property.Value.Type == JTokenType.Null || Fits(property.Value, type)) continue;
                property.Remove();
                droppedInto.Add(sectionKey + "." + member);
                any = true;
            }
            return any;
        }

        // Removes the array elements of a registered record array whose JSON does not fit the element type. True when any went.
        private static bool DropMisfitRecords(JObject section, string sectionKey, List<string> droppedInto)
        {
            bool any = false;
            foreach ((string sectionName, string arrayKey, Type element) in ArraySections)
            {
                if (!string.Equals(sectionName, sectionKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (!(section.Property(arrayKey, StringComparison.OrdinalIgnoreCase)?.Value is JArray array)) continue;
                for (int i = array.Count - 1; i >= 0; i--)
                {
                    if (Fits(array[i], element)) continue;
                    array.RemoveAt(i);
                    droppedInto.Add(sectionKey + "." + arrayKey + "[" + i + "]");
                    any = true;
                }
            }
            return any;
        }

        // Removes the entries of a string -> int map whose value isn't an int (1.5, "1", 3000000000, [ ]). True when any went.
        private static bool DropMisfitEntries(JObject map, string section, List<string> droppedInto)
        {
            var bad = new List<string>();
            foreach (JProperty entry in map.Properties())
            {
                if (entry.Value.Type != JTokenType.Integer || !Fits(entry.Value, typeof(int))) bad.Add(entry.Name);
            }
            foreach (string name in bad)
            {
                map.Remove(name);
                droppedInto.Add(section + "." + name);
            }
            return bad.Count > 0;
        }

        // The migration chain: an older file's JSON, step by step to the current schema (v1 has none; v2 adds MigrateV1ToV2 here). A file at
        // or above the current version, or one whose version can't be read, passes through untouched for JsonSaveFile to judge.
        internal static string Upgrade(string json)
        {
            if (!JsonSaveFile<WorldSave>.TryReadSchemaVersion(json, out long version) || version >= WorldSave.CurrentSchemaVersion) return json;
            JObject root = JObject.Parse(json);
            // for (long v = version; v < WorldSave.CurrentSchemaVersion; v++) Steps[v](root);   (no steps before v2)
            return root.ToString();
        }
    }
}
