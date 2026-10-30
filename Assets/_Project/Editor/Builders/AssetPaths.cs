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
        public const string PlayerPrefab = "Assets/_Project/Prefabs/PlayerCapsule.prefab";   // the M1 stand-in player: a capsule NetworkObject (M2 replaces the body)
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string BootstrapScene = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string ArenaScene = "Assets/_Project/Scenes/Arena_Greybox.unity";

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
