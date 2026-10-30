// The Bootstrap scene, the Lobby, the greybox test arena and the build list. Each scene starts from NewScene(EmptyScene, Single) and is saved to the
// same path, so its GUID stays stable.
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class SceneBuilder
    {
        // ProjectBuilder.RunAll calls ApplyBuildSettings once, after this.
        public static void BuildAll()
        {
            BuildBootstrap();
            BuildLobby();
            BuildArena();
        }

        public static void BuildBootstrap()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: NewScene(Single) unloads unused assets and would leave destroyed references.
            NetworkObject playerPrefab = BuilderUtil.LoadNetworkPrefab(AssetPaths.PlayerPrefab);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionNames.AssetPath);
            if (inputActions == null) throw new System.IO.FileNotFoundException("Missing " + InputActionNames.AssetPath);
            var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetPaths.NetworkPrefabsListAsset);
            if (prefabList == null) throw new System.IO.FileNotFoundException("Missing " + AssetPaths.NetworkPrefabsListAsset);

            // App: AppBootstrap, NetworkManager, UnityTransport, PlayerInputAdapter, AudioBus, the dev panel.
            var app = new GameObject("App");
            AppBootstrap bootstrap = BuilderUtil.GetOrAdd<AppBootstrap>(app);
            NetworkManager networkManager = BuilderUtil.GetOrAdd<NetworkManager>(app);
            UnityTransport transport = BuilderUtil.GetOrAdd<UnityTransport>(app);
            BuilderUtil.GetOrAdd<PlayerInputAdapter>(app);
            BuilderUtil.GetOrAdd<AudioBus>(app);
            BuilderUtil.GetOrAdd<DevSessionPanel>(app);
            ConfigureNetworkManager(networkManager, transport, prefabList);
            bootstrap.Configure(playerPrefab, inputActions);

            BuilderUtil.SaveScene(scene, AssetPaths.BootstrapScene);
        }

        // The lobby: a plain camera and light behind the dev panel. Sessions start here and the flow comes back here when one ends.
        public static void BuildLobby()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("LobbyCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.10f, 0.14f);
            cameraObject.AddComponent<AudioListener>();

            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            BuilderUtil.SaveScene(scene, AssetPaths.LobbyScene);
        }

        // The greybox test arena: ground, a light, a camera that overlooks it and the spawn point. Nothing else (M2 adds the hero and camera rig).
        public static void BuildArena()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material groundMaterial = MaterialLibrary.Get("M_GreyboxGround");

            var cameraObject = new GameObject("ArenaCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 14f, -16f), Quaternion.Euler(40f, 0f, 0f));

            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject ground = BuilderUtil.EnsurePrimitive(new GameObject("Ground").transform, "Slab", PrimitiveType.Cube,
                new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(40f, 1f, 40f), groundMaterial, keepCollider: true);
            BuilderUtil.SetLayerRecursive(ground.transform.parent.gameObject, Layers.Ground);

            var spawn = new GameObject("SpawnPoint");
            spawn.AddComponent<SpawnPoint>();
            spawn.transform.position = Vector3.zero;

            BuilderUtil.SaveScene(scene, AssetPaths.ArenaScene);
        }

        // Build list in SceneNames.BuildOrder order (Bootstrap at index 0).
        public static void ApplyBuildSettings()
        {
            string[] names = SceneNames.BuildOrder;
            var entries = new EditorBuildSettingsScene[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = AssetPaths.ScenesFolder + "/" + names[i] + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new System.IO.FileNotFoundException("Missing scene " + path);
                entries[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = entries;
        }

        static void ConfigureNetworkManager(NetworkManager networkManager, UnityTransport transport, NetworkPrefabsList prefabList)
        {
            // Null right after AddComponent when GenerateDefaultNetworkPrefabs is off (NGO's reset hook only builds it when on).
            if (networkManager.NetworkConfig == null) networkManager.NetworkConfig = new NetworkConfig();
            NetworkConfig config = networkManager.NetworkConfig;
            config.NetworkTransport = transport;
            config.TickRate = NetworkConstants.TickRate;
            config.ConnectionApproval = true;
            config.EnableSceneManagement = true;
            config.PlayerPrefab = null;
            config.ProtocolVersion = NetworkConstants.NgoProtocolVersion;
            config.ForceSamePrefabs = true;
            if (config.Prefabs == null) config.Prefabs = new NetworkPrefabs();
            config.Prefabs.NetworkPrefabsLists.Clear();
            config.Prefabs.NetworkPrefabsLists.Add(prefabList);
        }
    }
}
