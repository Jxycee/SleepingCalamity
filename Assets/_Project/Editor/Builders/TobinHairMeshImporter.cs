// Import settings for the Tobin mesh-hair FBX (HairMesh/TobinHairMesh.fbx), applied by path like TobinBaseModelImporter.
// Same globalScale as TobinBase.fbx so the hair is in the head's frame; generic rig (own Head + hair_<k>_<i> chain bones, re-parented under TobinBase's Head bone at runtime).
using UnityEditor;

namespace SleepingCalamity.EditorTools
{
    public sealed class TobinHairMeshImporter : AssetPostprocessor
    {
        public const string ModelPath = "Assets/_Project/Content/Characters/TobinBase/HairMesh/TobinHairMesh.fbx";
        public override uint GetVersion() => 1;
        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = TobinBaseModelImporter.GlobalScale;
            mi.useFileScale = false;
            mi.importBlendShapes = false; mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
            mi.isReadable = true;
            mi.animationType = ModelImporterAnimationType.Generic;
        }
    }
}
