// Core/Save/WorldSaveRules.cs: what a loaded world save may hold. Pure: the Runtime store hands every parsed WorldSave through Clean, so gameplay
// only ever receives a valid one. Each section is checked on its own and a bad one costs only itself: it falls back to its default (the default
// seed, no players recorded) and is named in the rejected list, which the store logs once and uses to keep the original file as .corrupt.
// Nothing here throws, whatever the input. Ported from FishGame's R89 framework.
// Authority: host only (the hosting peer's world).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SleepingCalamity.Core
{
    public static class WorldSaveRules
    {
        public const string SectionSeed = "worldSeed", SectionPlayers = "players";

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

            if (save.Players != null)
            {
                if (TryCleanPlayers(save.Players, out WorldPlayersSave players, out bool droppedEntry))
                {
                    clean.Players = players;
                    if (droppedEntry) rejected?.Add(SectionPlayers);   // the good players kept; the original file kept as .corrupt for the bad entry
                }
                else rejected?.Add(SectionPlayers);
            }
            return clean;
        }

        // The world's players: bad, null or repeated entries are dropped alone (droppedEntry), entries past WorldPlayers.MaxRecorded too; what is
        // left must be a world WorldPlayers could hold (a Solo world with exactly one player, a Multiplayer world with 2 to 4), else the whole
        // section is rejected and the world records no players.
        public static bool TryCleanPlayers(WorldPlayersSave players, out WorldPlayersSave clean, out bool droppedEntry)
        {
            clean = null;
            droppedEntry = false;
            if (players == null || players.Recorded == null || players.Kind < 0 || players.Kind > (int)WorldKind.Multiplayer) return false;

            var keys = new List<PlayerKey>(WorldPlayers.MaxRecorded);
            foreach (WorldPlayerSave p in players.Recorded)
            {
                PlayerKey key = p != null && p.Identity >= 0 && p.Identity <= byte.MaxValue ? new PlayerKey((IdentityKind)p.Identity, p.Id) : default;
                if (p == null || !WorldPlayers.IsValidKey(key) || keys.Contains(key) || keys.Count >= WorldPlayers.MaxRecorded)
                {
                    droppedEntry = true;
                    continue;
                }
                keys.Add(key);
            }

            if (WorldPlayers.TryCreate((WorldKind)players.Kind, keys, out WorldPlayers world) != WorldPlayerVerdict.Ok) return false;
            clean = world.ToSave();
            return true;
        }
    }
}
