// Invariants the foundation keeps. The validators read persisted state only (asset files, ProjectSettings text, Packages/manifest.json)
// and the build scenes opened additively and read-only (MeshReferences, MissingScripts, BootstrapReferences): one scene at a time, closed
// without saving, and a scene that was already open is left alone. The separate test-edit process therefore also proves that BuildAll saved
// everything. If a build scene is already open with unsaved edits, the in-memory state is what gets checked.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class Validators
    {
        // The packages networking needs; versions are the project's own business (Packages/manifest.json), only their presence is pinned.
        static readonly string[] RequiredPackages =
        {
            "com.unity.netcode.gameobjects", "com.unity.services.multiplayer", "com.unity.services.authentication", "com.unity.services.core",
            "com.unity.multiplayer.playmode", "com.unity.nuget.newtonsoft-json", "com.unity.inputsystem", "com.unity.test-framework",
        };

        // Services that the Multiplayer Services package pulls in itself: never listed directly.
        static readonly string[] ForbiddenPackages = { "com.unity.services.relay", "com.unity.services.lobby", "com.unity.transport" };

        // Every networked prefab is in the list, has a root NetworkObject with a distinct GlobalObjectIdHash on disk, and no missing scripts;
        // the Bootstrap scene points at the list and the player prefab, the arena has a spawn point.
        public static List<string> NetworkPrefabs()
        {
            var errors = new List<string>();
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetPaths.NetworkPrefabsListAsset);
            if (list == null) { errors.Add("network prefab list missing at " + AssetPaths.NetworkPrefabsListAsset); return errors; }

            foreach (NetworkPrefab entry in list.PrefabList)
                if (entry == null || entry.Prefab == null) errors.Add("network prefab list has an empty entry");

            var seenHashes = new Dictionary<string, string>();
            foreach (string path in NetworkPrefabsBuilder.Prefabs)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { errors.Add("missing prefab " + path); continue; }
                if (go.GetComponent<NetworkObject>() == null) errors.Add(path + " has no root NetworkObject");
                string hash = ValidationText.FirstGroup(path, @"^\s*GlobalObjectIdHash:\s*(\d+)\s*$");
                if (hash == null || hash == "0") errors.Add(path + " has no GlobalObjectIdHash on disk");
                else if (seenHashes.ContainsKey(hash)) errors.Add(path + " shares GlobalObjectIdHash " + hash + " with " + seenHashes[hash]);
                else seenHashes.Add(hash, path);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { AssetPaths.ProjectFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (go.GetComponent<NetworkObject>() != null && !list.Contains(go))
                    errors.Add("networked prefab not in the network prefab list: " + path);
            }

            RequireReference(AssetPaths.BootstrapScene, AssetPaths.NetworkPrefabsListAsset, "Bootstrap NetworkManager does not reference SleepingCalamityNetworkPrefabs.asset", errors);
            RequireReference(AssetPaths.BootstrapScene, AssetPaths.PlayerPrefab, "Bootstrap AppBootstrap does not reference PlayerCapsule.prefab", errors);
            return errors;
        }

        // Bootstrap at index 0, then every scene of SceneNames.BuildOrder, all enabled.
        public static List<string> BuildScenes()
        {
            var errors = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            string[] names = SceneNames.BuildOrder;
            if (scenes.Length != names.Length)
                errors.Add("expected " + names.Length + " build scenes, found " + scenes.Length);
            for (int i = 0; i < names.Length; i++)
            {
                string expected = AssetPaths.ScenesFolder + "/" + names[i] + ".unity";
                if (!File.Exists(BuilderUtil.Abs(expected))) errors.Add("missing scene file " + expected);
                if (i >= scenes.Length) continue;
                if (scenes[i].path != expected) errors.Add("build index " + i + " is '" + scenes[i].path + "', expected " + expected);
                else if (!scenes[i].enabled) errors.Add(expected + " is disabled in the build list");
            }
            return errors;
        }

        // The input asset parses and has every map and action of InputActionNames with the right types.
        public static List<string> InputActions()
        {
            var errors = new List<string>();
            string json = ValidationText.Read(InputActionNames.AssetPath);
            if (json == null) { errors.Add("missing " + InputActionNames.AssetPath); return errors; }
            InputActionAsset asset;
            try { asset = InputActionAsset.FromJson(json); }
            catch (System.Exception e) { errors.Add("inputactions parse failed: " + e.Message); return errors; }

            try
            {
                foreach (string map in new[] { InputActionNames.PlayerMap, InputActionNames.UiMap })
                    if (asset.FindActionMap(map) == null) errors.Add("missing action map " + map);
                if (asset.FindActionMap("SleepingCalamityInput") != null) errors.Add("no action map may be named SleepingCalamityInput");
                foreach (string action in InputActionNames.PlayerActions) CheckType(asset, InputActionNames.PlayerMap + "/" + action, errors);
                foreach (string action in InputActionNames.UiActions)
                    if (asset.FindAction(InputActionNames.UiMap + "/" + action) == null) errors.Add("missing action " + InputActionNames.UiMap + "/" + action);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
            return errors;
        }

        static void CheckType(InputActionAsset asset, string path, List<string> errors)
        {
            InputAction action = asset.FindAction(path);
            if (action == null) { errors.Add("missing action " + path); return; }
            bool ok = path == InputActionNames.Move || path == InputActionNames.Look || path == InputActionNames.SwitchTarget
                ? action.type == InputActionType.Value && action.expectedControlType == "Vector2"
                : action.type == InputActionType.Button;
            if (!ok) errors.Add("unexpected type for " + path + ": " + action.type + "/" + action.expectedControlType);
        }

        // The packages networking needs are listed, and the ones the Multiplayer Services package brings itself are not.
        public static List<string> Manifest()
        {
            var errors = new List<string>();
            string text = ValidationText.Read(AssetPaths.ManifestFile);
            if (text == null) { errors.Add("missing " + AssetPaths.ManifestFile); return errors; }
            if (text.Length > 0 && text[0] == '﻿') errors.Add("manifest.json starts with a BOM");
            foreach (string name in RequiredPackages)
                if (!Regex.IsMatch(text, "\"" + Regex.Escape(name) + "\"\\s*:")) errors.Add("required package not listed: " + name);
            foreach (string name in ForbiddenPackages)
                if (Regex.IsMatch(text, "\"" + Regex.Escape(name) + "\"\\s*:")) errors.Add("forbidden package listed: " + name);
            return errors;
        }

        // What ProjectSettingsBuilder writes: no generated default network prefabs, the layer names, background running, and the project-wide
        // input actions entry cleared.
        public static List<string> ProjectSettingsInvariants()
        {
            var errors = new List<string>();
            ValidationText.ExpectLines(AssetPaths.ProjectSettingsFile, errors, "activeInputHandler: 1", "runInBackground: 1");
            ValidationText.ExpectLines(AssetPaths.NetcodeSettingsFile, errors, "GenerateDefaultNetworkPrefabs: 0");

            string buildSettings = ValidationText.Read(AssetPaths.EditorBuildSettingsFile);
            if (buildSettings != null && buildSettings.Contains(BuilderUtil.ProjectWideActionsConfigKey))
                errors.Add("project-wide input actions entry (" + BuilderUtil.ProjectWideActionsConfigKey + ") is still configured");
            if (UnityEngine.InputSystem.InputSystem.actions != null)
                errors.Add("InputSystem.actions must be null (project-wide actions cleared)");

            if (LayerMask.LayerToName(Layers.Water) != "Water") errors.Add("layer " + Layers.Water + " must be Water");
            for (int i = 0; i < Layers.UserLayerIndices.Length; i++)
            {
                int index = Layers.UserLayerIndices[i];
                if (LayerMask.LayerToName(index) != Layers.UserLayerNames[i])
                    errors.Add("layer " + index + " must be " + Layers.UserLayerNames[i] + ", found '" + LayerMask.LayerToName(index) + "'");
            }
            return errors;
        }

        // Every MeshFilter, MeshCollider and SkinnedMeshRenderer in the build scenes and in Prefabs/ has a mesh (catches destroyed references saved as null).
        public static List<string> MeshReferences() => MeshReferences(AssetPaths.PrefabsFolder);

        // Same check with the prefab folder given (tests point it at a temp folder so no test prefab ever lands in the shipped Prefabs/ folder).
        internal static List<string> MeshReferences(string prefabsFolder)
        {
            var errors = new List<string>();
            VisitAllObjects(errors, prefabsFolder, (assetPath, go) =>
            {
                MeshFilter filter = go.GetComponent<MeshFilter>();
                // LookTestGeometry builds its mesh on enable and the scene stores none (so an inactive greybox, like Hollowmere's Rebuilt preset or the town view's hidden parts, reads null here).
                if (filter != null && filter.sharedMesh == null && go.GetComponent<Presentation.LookTestGeometry>() == null) errors.Add(assetPath + ": " + HierarchyPath(go) + " MeshFilter has no mesh");
                foreach (MeshCollider collider in go.GetComponents<MeshCollider>())
                    if (collider.sharedMesh == null) errors.Add(assetPath + ": " + HierarchyPath(go) + " MeshCollider has no mesh");
                SkinnedMeshRenderer skinned = go.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && skinned.sharedMesh == null) errors.Add(assetPath + ": " + HierarchyPath(go) + " SkinnedMeshRenderer has no mesh");
            });
            return errors;
        }

        // No GameObject in the build scenes or in Prefabs/ has a missing script.
        public static List<string> MissingScripts()
        {
            var errors = new List<string>();
            VisitAllObjects(errors, AssetPaths.PrefabsFolder, (assetPath, go) =>
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0) errors.Add(assetPath + ": " + HierarchyPath(go) + " has " + missing + " missing script(s)");
            });
            return errors;
        }

        // No in-scene NetworkObject's stored GlobalObjectIdHash would change if NGO re-ran OnValidate on it (the hash derives from the fileID,
        // which SceneFileNormalizer renumbers; BuilderUtil.SaveScene refreshes it). Scenes without a NetworkObject pass trivially.
        public static List<string> SceneNetworkObjectHashes()
        {
            var errors = new List<string>();
            foreach (string sceneName in SceneNames.BuildOrder)
            {
                string scenePath = AssetPaths.ScenesFolder + "/" + sceneName + ".unity";
                if (!File.Exists(BuilderUtil.Abs(scenePath))) continue; // BuildScenes reports a missing file
                ForEachSceneObject(scenePath, errors, go =>
                {
                    NetworkObject networkObject = go.GetComponent<NetworkObject>();
                    if (networkObject == null) return;
                    uint stored = BuilderUtil.NetworkObjectHash(networkObject);
                    BuilderUtil.RunNetworkObjectOnValidate(networkObject);
                    uint fresh = BuilderUtil.NetworkObjectHash(networkObject);
                    if (fresh != stored)
                        errors.Add(scenePath + ": " + HierarchyPath(go) + " NetworkObject hash is stale (" + stored + ", OnValidate gives " + fresh + ")");
                });
            }
            return errors;
        }

        // Bootstrap.unity has one AppBootstrap whose inputActions and playerPrefab point at the project assets; the arena has a spawn point.
        public static List<string> BootstrapReferences()
        {
            var errors = new List<string>();
            if (!File.Exists(BuilderUtil.Abs(AssetPaths.BootstrapScene))) return errors; // BuildScenes reports a missing file
            int count = 0;
            ForEachSceneObject(AssetPaths.BootstrapScene, errors, go =>
            {
                foreach (AppBootstrap bootstrap in go.GetComponents<AppBootstrap>())
                {
                    count++;
                    if (count > 1) continue;
                    CheckBootstrapReference(bootstrap, "inputActions", InputActionNames.AssetPath, errors);
                    CheckBootstrapReference(bootstrap, "playerPrefab", AssetPaths.PlayerPrefab, errors);
                }
            });
            if (count == 0 && errors.Count > 0) return errors; // the play-mode guard fired; nothing was visited
            if (count != 1) errors.Add("Bootstrap has " + count + " AppBootstrap components, expected 1");

            foreach (string arenaPath in new[] { AssetPaths.ArenaScene, AssetPaths.CombatArenaScene, HollowmereBuilder.ScenePath })
            {
                if (!File.Exists(BuilderUtil.Abs(arenaPath))) continue;
                int spawnPoints = 0;
                ForEachSceneObject(arenaPath, errors, go =>
                {
                    if (go.GetComponent<Gameplay.SpawnPoint>() != null) spawnPoints++;
                });
                if (spawnPoints != 1 && errors.Count == 0) errors.Add(arenaPath + " has " + spawnPoints + " SpawnPoint components, expected 1");
            }
            return errors;
        }

        static void CheckBootstrapReference(AppBootstrap bootstrap, string property, string expectedPath, List<string> errors)
        {
            using var so = new SerializedObject(bootstrap);
            SerializedProperty serialized = so.FindProperty(property);
            if (serialized == null) { errors.Add("Bootstrap AppBootstrap." + property + " property not found"); return; }
            Object reference = serialized.objectReferenceValue;
            if (reference == null) { errors.Add("Bootstrap AppBootstrap." + property + " is null"); return; }
            string path = AssetDatabase.GetAssetPath(reference);
            if (path != expectedPath) errors.Add("Bootstrap AppBootstrap." + property + " references " + path + ", expected " + expectedPath);
        }

        // Visits every GameObject (inactive included) of every existing build scene, one scene at a time, then of every prefab under prefabsFolder.
        static void VisitAllObjects(List<string> errors, string prefabsFolder, System.Action<string, GameObject> visit)
        {
            foreach (string sceneName in SceneNames.BuildOrder)
            {
                string scenePath = AssetPaths.ScenesFolder + "/" + sceneName + ".unity";
                if (!File.Exists(BuilderUtil.Abs(scenePath))) continue; // BuildScenes reports a missing file
                ForEachSceneObject(scenePath, errors, go => visit(scenePath, go));
            }
            if (!AssetDatabase.IsValidFolder(prefabsFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) visit(path, t.gameObject);
            }
        }

        // Opens the scene additively unless it is already loaded, visits all its objects, and closes it (without saving) only if this call opened it.
        static void ForEachSceneObject(string scenePath, List<string> errors, System.Action<GameObject> visit)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!errors.Contains("scene checks need edit mode")) errors.Add("scene checks need edit mode");
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasValid = scene.IsValid();
            bool opened = !(wasValid && scene.isLoaded);
            if (opened) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) visit(t.gameObject);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, !wasValid);
            }
        }

        static string HierarchyPath(GameObject go)
        {
            string path = go.name;
            for (Transform t = go.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return path;
        }

        static void RequireReference(string fileAssetPath, string referencedAssetPath, string message, List<string> errors)
        {
            string guid = AssetDatabase.AssetPathToGUID(referencedAssetPath);
            string text = ValidationText.Read(fileAssetPath);
            if (string.IsNullOrEmpty(guid) || text == null || !text.Contains("guid: " + guid)) errors.Add(message);
        }
    }
}
