// Core/Save/WorldSave.cs: the host-owned world save, v1. Plain serializable data with public fields and no Unity types: the Runtime
// WorldSaveStore serializes it with Newtonsoft (camelCase), and the key names come from these field names. Ported from FishGame's R89 framework
// with its fishing sections removed. v1 holds the world seed and the world's players (WorldPlayers); later sections (weapons, haul, placements)
// are added as new optional fields, each with its own clean rule in WorldSaveRules and its own row in WorldSaveStore's section table, and only
// a type change to an existing field bumps SchemaVersion.
// Authority: host only (only the hosting peer reads or writes its world; a client never touches its own world file).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    [Serializable]
    public sealed class WorldSave
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string SavedAtUtc = "";            // informational (ISO 8601); never read back as logic
        public string WorldSeed = "";             // 16 hex digits (WorldSaveRules.SeedToHex): a full 64-bit value survives any JSON reader
        public WorldPlayersSave Players;          // null: no players recorded yet (the host flow creates the world's kind and players on first start)
    }

    // The save form of WorldPlayers: Kind is a WorldKind number (0 Solo, 1 Multiplayer); Recorded lists the recorded players in order.
    [Serializable]
    public sealed class WorldPlayersSave
    {
        public int Kind;
        public List<WorldPlayerSave> Recorded = new List<WorldPlayerSave>();
    }

    [Serializable]
    public sealed class WorldPlayerSave
    {
        public int Identity;                      // IdentityKind as a number
        public string Id = "";                    // PlayerKey.Id
    }
}
