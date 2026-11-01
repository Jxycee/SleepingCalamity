// Every project-relative path used by the builders and validators. Constants only.
using SleepingCalamity.Runtime;

namespace SleepingCalamity.EditorTools
{
    public static class AssetPaths
    {
        public const string InputActions = InputActionNames.AssetPath;
        public const string ProjectFolder = "Assets/_Project";
        public const string NetworkPrefabsListAsset = "Assets/_Project/Content/Network/SleepingCalamityNetworkPrefabs.asset";
        public const string PrefabsFolder = "Assets/_Project/Prefabs";
        public const string PlayerPrefab = "Assets/_Project/Prefabs/PlayerCapsule.prefab";   // the player: since M2 the mannequin hero (the file keeps its M1 name so its GUID and references survive)
        public const string DummyPrefab = "Assets/_Project/Prefabs/TargetDummy.prefab";      // M2: a host-spawned target dummy (NetworkObject)
        public const string CameraRigPrefab = "Assets/_Project/Prefabs/HeroCameraRig.prefab"; // M2: the owner's Cinemachine rig (not networked)
        public const string CellarFloorSyncPrefab = "Assets/_Project/Prefabs/CellarFloorSync.prefab"; // M5 stage 3: host-spawned holder of the dive seed and floor id (NetworkObject)
        public const string CellarContentsSyncPrefab = "Assets/_Project/Prefabs/CellarContentsSync.prefab"; // M5 contents: host-spawned room enemies, chests, keys, locked door, pieces (NetworkObject)
        public const string DiveHaulSyncPrefab = "Assets/_Project/Prefabs/DiveHaulSync.prefab"; // M5 banking: host-spawned holder of the shared haul summary (NetworkObject)
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string LobbyScene = "Assets/_Project/Scenes/Lobby.unity";
        public const string ArenaScene = "Assets/_Project/Scenes/Arena_Greybox.unity";
        public const string CombatArenaScene = "Assets/_Project/Scenes/Arena_Combat.unity";   // M3: the combat arena
        public const string SlimePrefab = "Assets/_Project/Prefabs/EnemySlime.prefab";       // M3: host-spawned enemies (NetworkObject)
        public const string RatPrefab = "Assets/_Project/Prefabs/EnemyRatKing.prefab";
        public const string BroomPrefab = "Assets/_Project/Prefabs/EnemyBroom.prefab";
        public const string WardenPrefab = "Assets/_Project/Prefabs/EnemyDoughWarden.prefab";
        public const string HealPrefab = "Assets/_Project/Prefabs/HealingDrop.prefab";

        // Generated project files read or written by the builders.
        public const string TemplateInputActions = "Assets/InputSystem_Actions.inputactions";
        public const string DefaultNetworkPrefabs = "Assets/DefaultNetworkPrefabs.asset";
        public const string ManifestFile = "Packages/manifest.json";
        public const string TagManagerFile = "ProjectSettings/TagManager.asset";
        public const string ProjectSettingsFile = "ProjectSettings/ProjectSettings.asset";
        public const string EditorBuildSettingsFile = "ProjectSettings/EditorBuildSettings.asset";
        public const string NetcodeSettingsFile = "ProjectSettings/NetcodeForGameObjects.asset";
    }
}
