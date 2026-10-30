// Core/Save/WorldSave.cs: the host-owned world save, v1. Plain serializable data with public fields and no Unity types: the Runtime
// WorldSaveStore serializes it with Newtonsoft (camelCase), and the key names come from these field names. Ported from FishGame's world save
// framework with its fishing sections removed. v1 holds the world seed and the world's players (WorldPlayers); later sections (weapons, haul,
// placements) are added as new optional fields, each with its own clean rule in WorldSaveRules and its own row in WorldSaveStore's section
// table, and only a type change to an existing field bumps SchemaVersion.
// Unknown members (a section or a field a newer build added) are kept in Extra ([JsonExtensionData]) on the file and on every section type,
// so an older build that loads and saves the file writes them back instead of dropping them.
// Authority: host only (only the hosting peer reads or writes its world; a client never touches its own world file).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SleepingCalamity.Core
{
    [Serializable]
    public sealed class WorldSave
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string SavedAtUtc = "";            // informational (ISO 8601); never read back as logic
        public string WorldSeed = "";             // 16 hex digits (WorldSaveRules.SeedToHex): a full 64-bit value survives any JSON reader
        public WorldPlayersSave Players;          // null: no world created yet (the host flow creates its kind and players on the first start)

        [JsonExtensionData] public Dictionary<string, object> Extra;   // unknown top-level members, round-tripped untouched
    }

    // The save form of WorldPlayers. Kind (a WorldKind number: 0 Solo, 1 Multiplayer) and Identity (an IdentityKind number, -1 = none yet) are
    // stored apart from the list, so damage to the list never changes what kind of world this is.
    [Serializable]
    public sealed class WorldPlayersSave
    {
        public int Kind;
        public int Identity = -1;
        public List<WorldPlayerSave> Recorded = new List<WorldPlayerSave>();

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class WorldPlayerSave
    {
        public int Identity;                      // IdentityKind as a number
        public string Id = "";                    // PlayerKey.Id

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }
}
