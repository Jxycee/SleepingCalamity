// Editor/Builders/CellarFloorKitBuilder.cs: gives the real game floor (the CellarFloorSync network prefab) the same art-layer kit the CellarFloorTest scene uses, so play stops
// rendering the flat-colour greybox: floor tile, B5 vault pieces, the floor-art-2 clutter (Crate, CrateStack, JarCluster, Rubble, SackPile), the B11 lantern, wear materials, fog.
// The piece holders live under an inactive "KitTemplates" child of the prefab; CellarFloorSync.Kit points at them (CellarFloorKit is serializable, Pieces as parallel lists).
// Runs after CellarFloorTest in ProjectBuilder.RunAll (needs the DungeonCorner materials and profile). Idempotent: the child is rebuilt each run.
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class CellarFloorKitBuilder
    {
        public const string TemplatesName = "KitTemplates";

        public static void Build()
        {
            BuilderUtil.WritePrefab(AssetPaths.CellarFloorSyncPrefab, root =>
            {
                Transform old = root.transform.Find(TemplatesName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var templates = new GameObject(TemplatesName).transform; templates.SetParent(root.transform, false); templates.gameObject.SetActive(false);
                CellarFloorSync sync = BuilderUtil.GetOrAdd<CellarFloorSync>(root);
                sync.Kit = CellarFloorTestBuilder.BuildKit(templates);
                Debug.Log("SC CELLARFLOORKIT real-floor kit pieces=" + sync.Kit.Pieces.Count + ": " + string.Join(",", sync.Kit.Pieces.Keys));
            });
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.CellarFloorSyncPrefab);
        }
    }
}
