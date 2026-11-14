// Core/Save/WorldSaveRules.cs: what a loaded world save may hold. Pure: the Runtime store hands every parsed WorldSave through Clean, so gameplay
// only ever receives a valid one. Each section is checked on its own and a bad one costs only itself: it falls back to its default (the default
// seed, no players recorded) and is named in the rejected list, which the store logs once and uses to keep the original file as .corrupt.
// Nothing here throws, whatever the input. Ported from FishGame's world save framework.
// Authority: host only (the hosting peer's world).
using System;
using System.Collections.Generic;
using System.Globalization;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Core
{
    public static class WorldSaveRules
    {
        public const string SectionSeed = "worldSeed", SectionPlayers = "players", SectionWorldId = "worldId", SectionWeapons = "weapons", SectionCreatedAt = "createdAtUtc", SectionBags = "bags";

        // A new world: the given seed, nothing else yet.
        public static WorldSave New(ulong seed) => new WorldSave { WorldSeed = SeedToHex(seed) };

        public static string SeedToHex(ulong seed) => seed.ToString("X16", CultureInfo.InvariantCulture);

        // Exactly 16 hex digits (either case), nothing else.
        public static bool TryParseSeed(string hex, out ulong seed)
        {
            seed = 0;
            if (hex == null || hex.Length != 16) return false;
            foreach (char c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out seed);
        }

        // A valid copy of save (a new default world for null): bad sections reset, each named in rejected (cleared first; may be null). The
        // schema version is kept as read (the store decides read-only for a newer one).
        public static WorldSave Clean(WorldSave save, ulong defaultSeed, List<string> rejected)
        {
            rejected?.Clear();
            if (save == null) return New(defaultSeed);
            var clean = new WorldSave { SchemaVersion = save.SchemaVersion, SavedAtUtc = save.SavedAtUtc ?? "" };

            if (TryParseSeed(save.WorldSeed, out ulong seed)) clean.WorldSeed = SeedToHex(seed);
            else
            {
                clean.WorldSeed = SeedToHex(defaultSeed);
                rejected?.Add(SectionSeed);
            }

            // The id is the file's name: a malformed one is blanked and the store fills in the real one (not a rejection, nothing is lost).
            clean.WorldId = WorldIds.IsValid(save.WorldId) ? save.WorldId : "";
            clean.CreatedAtUtc = save.CreatedAtUtc ?? "";

            if (save.Players != null)
            {
                if (TryCleanPlayers(save.Players, out WorldPlayersSave players, out bool droppedEntry))
                {
                    clean.Players = players;
                    if (droppedEntry) rejected?.Add(SectionPlayers);   // the good players kept; the original file kept as .corrupt for the bad entry
                }
                else rejected?.Add(SectionPlayers);
            }
            if (save.Weapons != null)
            {
                // A weapon that does not fit is dropped alone; the section keeps the rest. A dropped entry still keeps the original file as .corrupt.
                var droppedWeapons = new List<string>();
                clean.Weapons = WeaponsSaveRules.Clean(save.Weapons, null, droppedWeapons);
                if (droppedWeapons.Count > 0) rejected?.Add(SectionWeapons);
            }
            if (save.Stock != null || save.Town != null || save.Residents != null || save.Dive != null || save.Bags != null || save.Suspend != null)
            {
                // Each town section is cleaned on its own (a bad entry is dropped alone, named for the .corrupt copy), then the conservation pass
                // (TownSaveRules.Reconcile) puts every piece and resident in exactly one place; a mid-dive quit counts as a failed dive.
                // The per-player bags section is cleaned the same way (an entry that does not fit is dropped alone) and is outside the piece conservation.
                // So is the bank-room suspend (a bad floor or phase drops only it; a bad bag record only that record), and a valid one keeps the dive from counting as failed.
                var town = new List<string>();
                TownSnapshot snap = TownSaveRules.CleanSnapshot(new TownSnapshot { Stock = save.Stock, Town = save.Town, Residents = save.Residents, Dive = save.Dive, Bags = save.Bags, Suspend = save.Suspend }, town);
                clean.Stock = snap.Stock; clean.Town = snap.Town; clean.Residents = snap.Residents; clean.Dive = snap.Dive; clean.Bags = snap.Bags; clean.Suspend = snap.Suspend;
                foreach (string section in town) if (!(rejected?.Contains(section) ?? true)) rejected.Add(section);
            }
            if (save.Extra != null) clean.Extra = new Dictionary<string, object>(save.Extra);   // a newer build's members, written back as read
            return clean;
        }

        // What the world picker shows about a (cleaned) world. Null for a save without a usable players section (not a world a picker can
        // offer: a world is created with its kind written down). Never throws.
        public static WorldSummary Summarize(WorldSave save)
        {
            if (save == null || save.Players == null || !WorldPlayers.TryRestore(save.Players, out WorldPlayers world, out _)) return null;
            var summary = new WorldSummary
            {
                Id = save.WorldId ?? "",
                Kind = world.Kind,
                Identity = world.Identity.HasValue ? (int)world.Identity.Value : -1,
                CreatedAtUtc = save.CreatedAtUtc ?? "",
                LastPlayedUtc = save.SavedAtUtc ?? "",
            };
            foreach (PlayerKey key in world.Recorded)
            {
                string name = world.NameOf(key);
                summary.PlayerNames.Add(string.IsNullOrEmpty(name) ? "?" : name);
                summary.PlayerKeys.Add(key);
            }
            return summary;
        }

        // The world's players: bad, null or repeated records, records of another identity kind and records past WorldPlayers.MaxRecorded
        // (a Solo world keeps one) are dropped alone (droppedEntry); the world KEEPS its kind however many records survive (a Multiplayer world
        // with fewer than two still needs two to start and takes new players; WorldPlayers.TryRestore). Only an unknown kind rejects the section
        // (clean is then null and the world records no players).
        public static bool TryCleanPlayers(WorldPlayersSave players, out WorldPlayersSave clean, out bool droppedEntry)
        {
            clean = null;
            if (!WorldPlayers.TryRestore(players, out WorldPlayers world, out droppedEntry)) return false;
            clean = world.ToSave();
            return true;
        }
    }
}
