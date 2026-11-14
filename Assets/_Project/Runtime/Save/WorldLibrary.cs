// Runtime/Save/WorldLibrary.cs: a player's worlds on this machine (user decision 2026-10-30): up to 10 Solo and up to 10 Multiplayer worlds,
// shown as separate tabs, one save file per world: worlds{profile suffix}/<worldId>.json, each with its own .bak / .corrupt / Retire
// (WorldSaveStore). The picker lists worlds by scanning the files' headers (WorldSaveRules.Summarize: id, kind, identity kind, the recorded
// players' names, created and last-played time), so no index file can disagree with the folder. Lists filter by identity kind: Local worlds
// (solo and dev-direct) and online worlds never cross. A "delete" sets the world aside (WorldSaveStore.Retire), never erases it.
// Authority: host only (a client never opens a world file). Main thread only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Runtime
{
    public sealed class WorldLibrary
    {
        public const string DirectoryBaseName = "worlds";
        public const string MigratedSuffix = ".migrated";

        private readonly string _directory;

        public WorldLibrary(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory is required", nameof(directory));
            _directory = directory;
        }

        // persistentDataPath is shared by MPPM clones and test runs, so the per-instance suffix keeps their worlds apart.
        public static string DirectoryForThisInstance() =>
            DirectoryOverrideForTests ?? Path.Combine(Application.persistentDataPath, DirectoryBaseName + InstanceProfile.Current.Suffix);

        // PlayMode tests point the world save at a temp folder, so a run never reads, deletes or overwrites the user's real worlds.
        // Null outside tests; reset for every play session.
        public static string DirectoryOverrideForTests { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DirectoryOverrideForTests = null;

        public static WorldLibrary ForThisInstance() => new WorldLibrary(DirectoryForThisInstance());

        public string Directory => _directory;

        public string PathOf(string worldId) => Path.Combine(_directory, worldId + ".json");

        // The worlds of one kind and identity kind, most recently played first. A world file that neither loads nor has a usable .bak, or whose players
        // section is unusable, is listed as Damaged (audit A6; its kind is unknown, so every list shows it, after the readable ones): the picker can offer
        // to set it aside, and no start ever opens another world in its place.
        public List<WorldSummary> List(WorldKind kind, IdentityKind identity)
        {
            var list = new List<WorldSummary>();
            if (!System.IO.Directory.Exists(_directory)) return list;
            foreach (string path in System.IO.Directory.GetFiles(_directory, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (!WorldIds.IsValid(id)) continue;
                WorldSummary summary = Read(path, id);
                if (summary != null && summary.Matches(kind, identity)) list.Add(summary);
            }
            list.Sort((a, b) => string.CompareOrdinal(b.LastPlayedUtc, a.LastPlayedUtc));   // ISO 8601 sorts as text
            return list;
        }

        // A damaged world takes no place: it is in every list and could never be played.
        public bool HasRoom(WorldKind kind, IdentityKind identity)
        {
            int readable = 0;
            foreach (WorldSummary w in List(kind, identity)) if (!w.Damaged) readable++;
            return readable < WorldSummary.MaxPerList;
        }

        // True when a world of this id has a file (or a .bak) on disk, readable or not.
        public bool Exists(string worldId) => WorldIds.IsValid(worldId) && (File.Exists(PathOf(worldId)) || File.Exists(PathOf(worldId) + AtomicFile.BackupSuffix));

        // Makes a new empty world of the kind and identity kind and returns its (loaded, saved) store; null when that list is full (10).
        public WorldSaveStore Create(WorldKind kind, IdentityKind identity, ulong seed)
        {
            if (!HasRoom(kind, identity)) return null;
            string id = WorldIds.NewId();
            var store = new WorldSaveStore(PathOf(id), seed);
            store.Load();
            WorldSave world = store.Current;
            world.WorldId = id;
            world.CreatedAtUtc = Now();
            world.SavedAtUtc = world.CreatedAtUtc;
            world.Players = new WorldPlayersSave { Kind = (int)kind, Identity = (int)identity, Recorded = new List<WorldPlayerSave>() };
            store.MarkDirty();
            store.Save();
            return store;
        }

        // Opens a world by id (loaded); null when the id is not valid or the file is not there.
        public WorldSaveStore Open(string worldId, ulong seedIfNew = 0UL)
        {
            if (!WorldIds.IsValid(worldId) || !File.Exists(PathOf(worldId)) && !File.Exists(PathOf(worldId) + AtomicFile.BackupSuffix)) return null;
            var store = new WorldSaveStore(PathOf(worldId), seedIfNew);
            store.Load();
            return store;
        }

        // Sets a world aside (world.json.old-<stamp>, with its .bak). It leaves the list; the files stay on disk.
        public bool Delete(string worldId)
        {
            if (!WorldIds.IsValid(worldId)) return false;
            var store = new WorldSaveStore(PathOf(worldId), 0UL);
            store.Load();
            return store.Retire();
        }

        // One-time migration of the old single file (world{suffix}.json) into the library: it becomes one world (kind and identity kind from
        // its players section; no players section = an empty Solo world of this machine). The old file is renamed *.migrated (and its .bak),
        // never deleted. Skipped (file untouched) when the target list is full. Returns true when a world was migrated.
        public bool MigrateLegacy(string legacyPath)
        {
            try
            {
                if (string.IsNullOrEmpty(legacyPath) || !File.Exists(legacyPath)) return false;
                var legacy = new WorldSaveStore(legacyPath, 0UL);
                legacy.Load();
                if (legacy.LoadedFrom == MetaLoadSource.Defaults || legacy.IsReadOnly) return false;   // unreadable or newer: leave it alone
                WorldSave old = legacy.Current;
                WorldKind kind = WorldKind.Solo;
                IdentityKind identity = IdentityKind.Local;
                if (old.Players != null && WorldPlayers.TryRestore(old.Players, out WorldPlayers players, out _))
                {
                    kind = players.Kind;
                    if (players.Identity.HasValue) identity = players.Identity.Value;
                }
                if (!HasRoom(kind, identity)) return false;

                string id = WorldIds.NewId();
                var store = new WorldSaveStore(PathOf(id), 0UL);
                store.Load();
                WorldSave world = store.Current;
                world.WorldSeed = old.WorldSeed;
                world.WorldId = id;
                world.CreatedAtUtc = string.IsNullOrEmpty(old.SavedAtUtc) ? Now() : old.SavedAtUtc;
                world.SavedAtUtc = world.CreatedAtUtc;
                world.Players = old.Players ?? new WorldPlayersSave { Kind = (int)kind, Identity = (int)identity, Recorded = new List<WorldPlayerSave>() };
                world.Extra = old.Extra;
                store.MarkDirty();
                if (!store.Save()) return false;
                File.Move(legacyPath, legacyPath + MigratedSuffix);
                string bak = legacyPath + AtomicFile.BackupSuffix;
                if (File.Exists(bak)) File.Move(bak, legacyPath + MigratedSuffix + AtomicFile.BackupSuffix);
                Debug.Log("SC SAVE migrated " + legacyPath + " into the world library as " + id);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SC SAVE world library: migrating " + legacyPath + " failed (" + e.Message + "); the old file is kept.");
                return false;
            }
        }

        // The header of one world file, read the way the world itself loads (so a repaired file lists too). A file that is not a usable world (neither it
        // nor its .bak could be read, or its players section is unusable) comes back as a Damaged summary, never null.
        private static WorldSummary Read(string path, string id)
        {
            var store = new WorldSaveStore(path, 0UL);
            store.Load();
            WorldSummary summary = null;
            if (store.LoadedFrom != MetaLoadSource.Defaults)
            {
                WorldSave save = store.Current;
                save.WorldId = id;
                summary = WorldSaveRules.Summarize(save);
            }
            return summary ?? new WorldSummary { Id = id, Damaged = true };
        }

        private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }
}
