// Core/Game/SceneNames.cs: scene names and build order. Bootstrap is always build index 0.
// Bootstrap holds the App object (DontDestroyOnLoad, so it is never loaded twice) and hands over to the Lobby scene at once; the Lobby is where
// sessions are started and where the flow returns when one ends; the greybox arena is the first gameplay scene.
using System;

namespace SleepingCalamity.Core
{
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string Lobby = "Lobby";
        public const string ArenaGreybox = "Arena_Greybox";
        public const string ArenaCombat = "Arena_Combat";   // M3: the combat test arena (waves of the three enemies and the elite)
        public const string ArenaDive = "Arena_Dive";   // M5: the Cellars dive hub (Undercroft stair stand-in); the dive floors are built far from it
        public const string ArenaPrefix = "Arena_";
        public static readonly string[] BuildOrder = { Bootstrap, Lobby, ArenaGreybox, ArenaCombat, ArenaDive };

        // True when sceneName starts with ArenaPrefix (ordinal, case-sensitive). Null -> false. Later milestones add their own gameplay prefixes here.
        public static bool IsGameplay(string sceneName)
        {
            return sceneName != null && sceneName.StartsWith(ArenaPrefix, StringComparison.Ordinal);
        }
    }
}
