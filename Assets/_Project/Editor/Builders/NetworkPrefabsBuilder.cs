// The owned network prefab list. Updated in place (stable GUID); NGO's default list is off (ProjectSettingsBuilder).
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class NetworkPrefabsBuilder
    {
        // Every networked prefab, in one place. A prefab with a root NetworkObject that is missing here fails Validators.NetworkPrefabs.
        public static readonly string[] Prefabs = { AssetPaths.PlayerPrefab, AssetPaths.DummyPrefab };

        public static void Build()
        {
            BuilderUtil.EnsureFolder(BuilderUtil.Folder(AssetPaths.NetworkPrefabsListAsset));
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetPaths.NetworkPrefabsListAsset);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, AssetPaths.NetworkPrefabsListAsset);
            }

            var wanted = new List<GameObject>();
            foreach (string path in Prefabs) wanted.Add(BuilderUtil.LoadNetworkPrefab(path).gameObject);

            bool changed = false;
            foreach (NetworkPrefab entry in new List<NetworkPrefab>(list.PrefabList))
            {
                if (entry == null || entry.Prefab == null || !wanted.Contains(entry.Prefab))
                {
                    list.Remove(entry);
                    changed = true;
                }
            }
            foreach (GameObject prefab in wanted)
            {
                if (list.Contains(prefab)) continue;
                list.Add(new NetworkPrefab { Prefab = prefab });
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
