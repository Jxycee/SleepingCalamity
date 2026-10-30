// Core/Save/WorldIds.cs: the id of one world (and so the name of its save file). A lowercase GUID in "N" format: safe in a file name,
// never reused, and checkable without touching the disk.
using System;

namespace SleepingCalamity.Core
{
    public static class WorldIds
    {
        public const int Length = 32;

        public static string NewId() => Guid.NewGuid().ToString("N");

        // 32 lowercase hex digits and nothing else (so an id can never carry a path separator or a dot).
        public static bool IsValid(string id)
        {
            if (id == null || id.Length != Length) return false;
            foreach (char c in id)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
