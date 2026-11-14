// Core/Save/WorldSummary.cs: what a world picker shows about a world without opening it (user decision 2026-10-30: a player keeps up to 10
// Solo worlds and up to 10 Multiplayer worlds, shown as separate tabs). Built by WorldSaveRules.Summarize from a world's save.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class WorldSummary
    {
        public const int MaxPerList = 10;   // per kind and identity kind (a tab)

        public string Id = "";
        public WorldKind Kind;
        public int Identity = -1;                       // IdentityKind as a number; -1 = not set yet (matches any list of its kind)
        public string CreatedAtUtc = "";
        public string LastPlayedUtc = "";
        public List<string> PlayerNames = new List<string>();   // the recorded players' display names, in record order ("?" when unknown)
        public List<PlayerKey> PlayerKeys = new List<PlayerKey>();   // the same players' keys, same order (the picker's "remove player" needs them)

        // Local and online worlds never mix: a world with no identity yet belongs to whichever list asks.
        public bool Matches(WorldKind kind, IdentityKind identity)
        {
            return Kind == kind && (Identity < 0 || Identity == (int)identity);
        }
    }
}
