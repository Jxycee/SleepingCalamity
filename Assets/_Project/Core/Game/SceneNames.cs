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
        public const string Hollowmere = "Hollowmere";   // M6 S3: the town is the session (hub) scene; the Cellars floors are built under it, no scene loads during play
        // T24b: Hollowmere's static art lives in additive scenes the main scene loads on every peer (HollowmereSceneLoader). They are not gameplay scenes and not in BuildOrder
        // (NGO never loads them); they are in Build Settings after it, because LoadSceneAsync by name needs that. Colliders, NetworkObjects, cameras, the directional light, volumes and probes stay in Hollowmere itself; local point lights travel with the art they light (T24c).
        public const string HollowmereTown = "Hollowmere_Town";
        public const string HollowmereRebuilt = "Hollowmere_Rebuilt";   // the Preset_Rebuilt copies of the town art (hidden until the town is rebuilt)
        public const string HollowmereLanes = "Hollowmere_Lanes";   // the lane kit's art (both presets): the biggest single group
        public const string HollowmereOutskirts = "Hollowmere_Outskirts";
        public const string HollowmereBackdrop = "Hollowmere_Backdrop";
        public static readonly string[] HollowmereArt = { HollowmereTown, HollowmereRebuilt, HollowmereLanes, HollowmereOutskirts, HollowmereBackdrop };
        public const string ArenaPrefix = "Arena_";
        public static readonly string[] BuildOrder = { Bootstrap, Lobby, ArenaGreybox, ArenaCombat, ArenaDive, Hollowmere };

        // True for one of Hollowmere's additive art scenes (ordinal, case-sensitive). Null -> false.
        public static bool IsHollowmereArt(string sceneName)
        {
            if (sceneName == null) return false;
            foreach (string n in HollowmereArt) if (string.Equals(n, sceneName, StringComparison.Ordinal)) return true;
            return false;
        }

        // True when sceneName starts with ArenaPrefix or is the town (ordinal, case-sensitive). Null -> false.
        public static bool IsGameplay(string sceneName)
        {
            return sceneName != null && (sceneName.StartsWith(ArenaPrefix, StringComparison.Ordinal) || string.Equals(sceneName, Hollowmere, StringComparison.Ordinal));
        }
    }
}
