// Core/Game/SceneNames.cs: scene names and build order. Bootstrap is always build index 0.
// Bootstrap holds the App object (it stays alive across loads) and is also the lobby; the greybox arena is the first gameplay scene.
using System;

namespace SleepingCalamity.Core
{
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string ArenaGreybox = "Arena_Greybox";
        public const string ArenaPrefix = "Arena_";
        public static readonly string[] BuildOrder = { Bootstrap, ArenaGreybox };

        // True when sceneName starts with ArenaPrefix (ordinal, case-sensitive). Null -> false. Later milestones add their own gameplay prefixes here.
        public static bool IsGameplay(string sceneName)
        {
            return sceneName != null && sceneName.StartsWith(ArenaPrefix, StringComparison.Ordinal);
        }
    }
}
