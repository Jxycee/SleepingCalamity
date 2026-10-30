// Shared helpers for the builders and validators.
// Get-or-add keeps components, children and the root NetworkObject (and so its GlobalObjectIdHash) stable across rebuilds.
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    internal static class BuilderUtil
    {
        // Input System 1.20.0 stores the project-wide actions as this EditorBuildSettings config object.
        public const string ProjectWideActionsConfigKey = "com.unity.input.settings.actions";

        static readonly MethodInfo NetworkObjectOnValidate =
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Absolute path for a project-relative path ("Assets/...", "ProjectSettings/...", "Packages/...").
        public static string Abs(string projectRelativePath) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), projectRelativePath));

        // True when something already occupies assetPath: a file on disk, or a GUID the database still holds for it. OnlyExistingAssets: the
        // default also reports recently deleted assets, which would block re-creating a deleted asset. Used by the Ensure-style builders to
        // refuse overwriting a file that exists but did not load as the expected type.
        public static bool AssetPathOccupied(string assetPath) =>
            File.Exists(Abs(assetPath)) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets));

        public static string Folder(string assetPath) => assetPath.Substring(0, assetPath.LastIndexOf('/'));

        // Creates every missing level of "Assets/A/B/C" through the AssetDatabase (folder .meta files included).
        public static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        // Walks "A/B/C" below root, creating missing levels as empty GameObjects.
        public static Transform EnsurePath(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                Transform next = current.Find(segment);
                if (next == null)
                {
                    var go = new GameObject(segment);
                    go.transform.SetParent(current, false);
                    next = go.transform;
                }
                current = next;
            }
            return current;
        }

        // Creates or reuses a primitive child. Its collider is removed unless keepCollider is true.
        public static GameObject EnsurePrimitive(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material, bool keepCollider)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        // An existing prefab is edited through LoadPrefabContents, so its GUID and its components survive.
        public static void WritePrefab(string assetPath, System.Action<GameObject> build)
        {
            EnsureFolder(Folder(assetPath));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    build(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, assetPath, out bool saved);
                    if (!saved) throw new System.InvalidOperationException("Could not save prefab " + assetPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                var root = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
                try
                {
                    build(root);
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath, out bool saved);
                    if (!saved) throw new System.InvalidOperationException("Could not save prefab " + assetPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        // NGO computes GlobalObjectIdHash in NetworkObject.OnValidate (internal in 2.13.3) from the asset's GlobalObjectId.
        // A prefab saved from a temporary object can still hold 0, so run it on the saved asset and write the result to disk.
        public static void RefreshNetworkObjectHash(string prefabPath)
        {
            NetworkObject networkObject = LoadNetworkPrefab(prefabPath);
            RunNetworkObjectOnValidate(networkObject);
            EditorUtility.SetDirty(networkObject); // OnValidate only dirties the asset when the value changed in memory; the file may still hold 0
            AssetDatabase.SaveAssets();
        }

        // The serialized GlobalObjectIdHash (the property is internal in NGO 2.13, so read it through SerializedObject, as it is on disk).
        public static uint NetworkObjectHash(NetworkObject networkObject)
        {
            using var so = new SerializedObject(networkObject);
            SerializedProperty p = so.FindProperty("GlobalObjectIdHash");
            if (p == null) throw new System.MissingFieldException("NetworkObject.GlobalObjectIdHash serialized field not found");
            return p.uintValue;
        }

        public static void RunNetworkObjectOnValidate(NetworkObject networkObject)
        {
            if (NetworkObjectOnValidate == null)
                throw new System.MissingMethodException("NetworkObject.OnValidate not found (expected internal in NGO 2.13.3)");
            NetworkObjectOnValidate.Invoke(networkObject, null);
        }

        public static NetworkObject LoadNetworkPrefab(string prefabPath)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (go == null) throw new FileNotFoundException("Missing prefab " + prefabPath);
            NetworkObject networkObject = go.GetComponent<NetworkObject>();
            if (networkObject == null) throw new System.InvalidOperationException("No root NetworkObject on " + prefabPath);
            return networkObject;
        }

        // Scenes are rebuilt from an empty scene and saved to the same path (same .meta, same GUID).
        public static void SaveScene(Scene scene, string assetPath)
        {
            EnsureFolder(Folder(assetPath));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, assetPath))
                throw new System.InvalidOperationException("Could not save scene " + assetPath);
            NormalizeSceneFile(assetPath);
        }

        // Unity numbers new objects at random on every save, so an unchanged scene would differ in git after each build. Renumber the saved
        // file from the hierarchy (SceneFileNormalizer) so a second build on a clean tree changes nothing.
        static void NormalizeSceneFile(string assetPath)
        {
            string full = System.IO.Path.GetFullPath(assetPath);
            string text = System.IO.File.ReadAllText(full);
            string normal = SceneFileNormalizer.Normalize(text);
            if (normal == text) return;
            System.IO.File.WriteAllText(full, normal, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (normal.Contains("GlobalObjectIdHash:")) RefreshSceneNetworkObjectHashes(assetPath);
        }

        // An in-scene NetworkObject stores a GlobalObjectIdHash derived from its fileID, which the renumbering just changed. Reopen the scene,
        // run NetworkObject.OnValidate on each one, and save again; a resave of a loaded scene keeps the fileIDs, so the file stays normalized.
        static void RefreshSceneNetworkObjectHashes(string assetPath)
        {
            Scene scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    RunNetworkObjectOnValidate(networkObject);
                    EditorUtility.SetDirty(networkObject);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, assetPath))
                throw new System.InvalidOperationException("Could not resave scene " + assetPath);
        }
    }
}
