// The Bootstrap scene, the Lobby, the greybox test arena and the build list. Each scene starts from NewScene(EmptyScene, Single) and is saved to the
// same path, so its GUID stays stable.
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
            BuildCombatArena();
            DiveArenaBuilder.Build();
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
            AddMenuSpike(app);
            ConfigureNetworkManager(networkManager, transport, prefabList);
            bootstrap.Configure(playerPrefab, inputActions);

            BuilderUtil.SaveScene(scene, AssetPaths.BootstrapScene);
        }

        private const string MenuFolder = "Assets/_Project/App/Menus";
        private const string MenuPanelSettingsPath = MenuFolder + "/MenuPanelSettings.asset";

        // PL-3 task 3.0, the UI Toolkit spike: a UIDocument (PanelSettings + UXML with one button) and its view on the App object. Input comes from
        // an EventSystem with the Input System's UI module on its own default actions (never the SleepingCalamityInput asset): Enter / gamepad south
        // submit, arrows / d-pad / left stick navigate, the mouse clicks.
        private static void AddMenuSpike(GameObject app)
        {
            foreach (string file in new[] { "MenuTheme.tss", "MenuSpike.uss", "MenuSpike.uxml" })
                AssetDatabase.ImportAsset(MenuFolder + "/" + file, ImportAssetOptions.ForceSynchronousImport);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(MenuFolder + "/MenuTheme.tss");
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenuFolder + "/MenuSpike.uxml");
            if (theme == null || tree == null) throw new System.IO.FileNotFoundException("Missing the menu UI assets under " + MenuFolder);

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(MenuPanelSettingsPath);
            if (panelSettings == null)
            {
                if (BuilderUtil.AssetPathOccupied(MenuPanelSettingsPath)) throw new System.InvalidOperationException(MenuPanelSettingsPath + " exists but is not a PanelSettings");
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, MenuPanelSettingsPath);
            }
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssetIfDirty(panelSettings);

            UIDocument document = BuilderUtil.GetOrAdd<UIDocument>(app);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = tree;
            BuilderUtil.GetOrAdd<MenuSpikeView>(app);
            BuilderUtil.GetOrAdd<EventSystem>(app);
            BuilderUtil.GetOrAdd<InputSystemUIInputModule>(app);
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

        // The greybox test arena: ground, a light, a camera with a Cinemachine Brain (the owner's hero rig drives it; without one it overlooks the
        // arena), the spawn point, walls and steps that exercise line of sight, the camera Deoccluder and the height band, and the target dummies
        // (spawned by the host from the markers under TargetDummies).
        public static void BuildArena()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material groundMaterial = MaterialLibrary.Get("M_GreyboxGround");

            var cameraObject = new GameObject("ArenaCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 14f, -16f), Quaternion.Euler(40f, 0f, 0f));
            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.35f);

            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject ground = BuilderUtil.EnsurePrimitive(new GameObject("Ground").transform, "Slab", PrimitiveType.Cube,
                new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(40f, 1f, 40f), groundMaterial, keepCollider: true);
            BuilderUtil.SetLayerRecursive(ground.transform.parent.gameObject, Layers.Ground);

            var spawn = new GameObject("SpawnPoint");
            spawn.AddComponent<SpawnPoint>();
            spawn.transform.position = Vector3.zero;

            BuildArenaStructures();
            BuildTargetDummies();

            BuilderUtil.SaveScene(scene, AssetPaths.ArenaScene);
        }

        // M3: the combat arena. A flat 44 m square with a ring of pillars (cover to dodge around, line-of-sight breakers) inside four boundary walls, the
        // spawn point at the centre, and the CombatHost (waves of the three enemies, then the elite, then tier 2) with its feedback and debug HUD.
        public static void BuildCombatArena()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material groundMaterial = MaterialLibrary.Get("M_GreyboxGround");
            Material wall = MaterialLibrary.Get("M_GreyboxWall");
            Material step = MaterialLibrary.Get("M_GreyboxStep");

            var cameraObject = new GameObject("ArenaCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 22f, -22f), Quaternion.Euler(45f, 0f, 0f));
            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.35f);

            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject ground = BuilderUtil.EnsurePrimitive(new GameObject("Ground").transform, "Slab", PrimitiveType.Cube,
                new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(44f, 1f, 44f), groundMaterial, keepCollider: true);
            BuilderUtil.SetLayerRecursive(ground.transform.parent.gameObject, Layers.Ground);

            var spawn = new GameObject("SpawnPoint");
            spawn.AddComponent<SpawnPoint>();
            spawn.transform.position = Vector3.zero;

            Transform structures = new GameObject("Structures").transform;
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 30f;
                Vector3 p = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 1.5f, 8.5f);
                Block(structures, "Pillar_" + (i + 1), p, new Vector3(1.4f, 3f, 1.4f), step);
            }
            Block(structures, "Wall_N", new Vector3(0f, 1.5f, 22.25f), new Vector3(45f, 3f, 0.5f), wall);
            Block(structures, "Wall_S", new Vector3(0f, 1.5f, -22.25f), new Vector3(45f, 3f, 0.5f), wall);
            Block(structures, "Wall_E", new Vector3(22.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 45f), wall);
            Block(structures, "Wall_W", new Vector3(-22.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 45f), wall);

            var combat = new GameObject("Combat");
            CombatHost host = combat.AddComponent<CombatHost>();
            host.Configure(BuilderUtil.LoadNetworkPrefab(AssetPaths.SlimePrefab), BuilderUtil.LoadNetworkPrefab(AssetPaths.RatPrefab),
                BuilderUtil.LoadNetworkPrefab(AssetPaths.BroomPrefab), BuilderUtil.LoadNetworkPrefab(AssetPaths.WardenPrefab), BuilderUtil.LoadNetworkPrefab(AssetPaths.HealPrefab));
            combat.AddComponent<CombatFx>().Configure(MaterialLibrary.Get("M_Spark"), MaterialLibrary.Get("M_SwingArc"));
            combat.AddComponent<CombatHud>();

            BuilderUtil.SaveScene(scene, AssetPaths.CombatArenaScene);
        }

        // Walls and steps, all on the Structure layer (line-of-sight and camera rays hit them; the player walks on them).
        static void BuildArenaStructures()
        {
            Material wall = MaterialLibrary.Get("M_GreyboxWall");
            Material step = MaterialLibrary.Get("M_GreyboxStep");
            Transform root = new GameObject("Structures").transform;

            // Behind the spawn point: walk back toward it and the camera boom (5 m, 6.5 m locked) runs into it, so the Deoccluder has something to do.
            Block(root, "Wall_Behind", new Vector3(0f, 1.5f, -6.5f), new Vector3(10f, 3f, 0.5f), wall);
            // Between the spawn point and the dummy at (-6, 7): a target you can see over there but cannot see through (line of sight).
            Block(root, "Wall_Sight", new Vector3(-5f, 1.5f, 3.5f), new Vector3(8f, 3f, 0.5f), wall);
            // Four 0.35 m steps (the controller's step offset is 0.4 m) up to a 1.4 m platform that holds a dummy.
            for (int i = 1; i <= 4; i++)
                Block(root, "Step_" + i, new Vector3(4.4f + 0.8f * (i - 1), 0.35f * i * 0.5f, -6f), new Vector3(0.8f, 0.35f * i, 3f), step);
            Block(root, "Platform_Low", new Vector3(9f, 0.7f, -6f), new Vector3(4f, 1.4f, 4f), step);
            // A 2.5 m block with no stairs: a dummy on top is outside the soft target's +-2 m height band, but inside the lock's.
            Block(root, "Block_High", new Vector3(-6f, 1.25f, -5f), new Vector3(4f, 2.5f, 4f), step);
        }

        static void Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject block = BuilderUtil.EnsurePrimitive(parent, name, PrimitiveType.Cube, center, Quaternion.identity, size, material, keepCollider: true);
            block.layer = Layers.Structure;
        }

        // Where the host spawns target dummies. Positions are chosen against the 6.5 numbers: D1 is inside the soft cone, D2/D3 at its edge, the
        // elite sits at 12 m (outside the 10 m acquire range, so you walk up to it, then it never lets go), one dummy is behind a wall, one on
        // the platform and one on the high block.
        static void BuildTargetDummies()
        {
            var root = new GameObject("TargetDummies");
            TargetDummySpawner spawner = root.AddComponent<TargetDummySpawner>();
            spawner.Configure(BuilderUtil.LoadNetworkPrefab(AssetPaths.DummyPrefab));
            Marker(root.transform, "Dummy_Near", new Vector3(0f, 0f, 4f), false);
            Marker(root.transform, "Dummy_Left", new Vector3(-4f, 0f, 7f), false);
            Marker(root.transform, "Dummy_Right", new Vector3(5f, 0f, 8f), false);
            Marker(root.transform, "Elite", new Vector3(0f, 0f, 12f), true);
            Marker(root.transform, "Dummy_BehindWall", new Vector3(-6f, 0f, 7f), false);
            Marker(root.transform, "Dummy_Platform", new Vector3(9f, 1.4f, -6f), false);
            Marker(root.transform, "Dummy_HighBlock", new Vector3(-6f, 2.5f, -5f), false);
        }

        static void Marker(Transform parent, string name, Vector3 position, bool elite)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.AddComponent<TargetDummyMarker>().Configure(elite);
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
