// Import settings for the embedded AetherStrand hair package's textures (Packages/com.aetherstrand.sculptedhair/Textures), applied by path
// like TobinHairMeshImporter: GrooveAO, FlowIDShift and AlphaCard are data maps (linear, Repeat), Normal is a normal map.
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public sealed class AetherStrandTextureImporter : AssetPostprocessor
    {
        public const string TextureFolder = "Packages/com.aetherstrand.sculptedhair/Textures/";
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureFolder)) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("AetherStrand_Normal.png");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = false;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
