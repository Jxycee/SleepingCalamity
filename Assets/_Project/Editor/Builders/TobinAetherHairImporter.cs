// Import settings for the AetherStrand trial hair FBX (AetherHair/TobinAetherHair.fbx, from
// Art/Blender/tools/experimental/tobin_hair/aetherstrand/tobin_aetherstrand.py), applied by path like TobinHairMeshImporter:
// same globalScale as TobinBase.fbx (the rig's Head bone sits on TobinBase's Head bone), generic rig (Head + Hair_<Zone>_<c>_<k> chains).
using UnityEditor;

namespace SleepingCalamity.EditorTools
{
    public sealed class TobinAetherHairImporter : AssetPostprocessor
    {
        public const string ModelPath = "Assets/_Project/Content/Characters/TobinBase/AetherHair/TobinAetherHair.fbx";
        public override uint GetVersion() => 1;
        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = TobinBaseModelImporter.GlobalScale;
            mi.useFileScale = false;
            mi.importBlendShapes = false; mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
            mi.isReadable = true;
            mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;   // AetherStrand shader: v runs root to tip, flow = bitangent
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Generic;
        }
    }
}
