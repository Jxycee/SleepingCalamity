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

        // Audit A6: a world file is on disk but neither it nor its .bak gives a usable world (unreadable, or its players section is damaged), so its kind is not
        // known. The picker lists it as damaged (it can be set aside, never selected or opened); it appears in every list and does not count toward the cap.
        public bool Damaged;

        // Local and online worlds never mix: a world with no identity yet belongs to whichever list asks. A damaged world's kind is unknown: it matches every list.
        public bool Matches(WorldKind kind, IdentityKind identity)
        {
            if (Damaged) return true;
            return Kind == kind && (Identity < 0 || Identity == (int)identity);
        }
    }
}
