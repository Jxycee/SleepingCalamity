// Prefabs built from code (an existing prefab is edited in place by BuilderUtil.WritePrefab, so its GUID and NetworkObject hash survive).
// M1 has one: the stand-in player. Authority: the owning client moves it (owner-authoritative NetworkTransform); the host spawns it
// (ArenaPlayerSpawner) and owns nothing else about it. M2 replaces the capsule with the mannequin hero and adds the movement.
using SleepingCalamity.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class PrefabBuilder
    {
        public static void Build()
        {
            MaterialLibrary.BuildAll();
            BuildPlayer();
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.PlayerPrefab);
        }

        static void BuildPlayer()
        {
            Material material = MaterialLibrary.Get("M_GreyboxPlayer");
            BuilderUtil.WritePrefab(AssetPaths.PlayerPrefab, root =>
            {
                BuilderUtil.GetOrAdd<NetworkObject>(root);
                NetworkTransform transform = BuilderUtil.GetOrAdd<NetworkTransform>(root);
                transform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // the owner writes its own pose
                // A 2 m capsule standing on the root's origin: the body is a child so the root stays a bare networked pivot.
                BuilderUtil.EnsurePrimitive(root.transform, "Body", PrimitiveType.Capsule,
                    new Vector3(0f, 1f, 0f), Quaternion.identity, Vector3.one, material, keepCollider: true);
                BuilderUtil.SetLayerRecursive(root, Layers.Player);
            });
        }
    }
}
