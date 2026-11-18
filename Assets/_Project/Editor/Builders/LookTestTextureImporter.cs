// Import settings for the look-test and material textures, applied by suffix so nobody edits .meta files by hand (M8, PHASE1 3.3).
//   *_albedo  sRGB colour (decal albedo keeps its alpha as transparency, clamped); *_normal  normal map (OpenGL Y+, as the packer writes it);
//   *_ms      linear, R metallic, A smoothness (URP Lit Metallic map); *_occ  linear occlusion;
//   *_mask    linear RGBA, URP Terrain Lit mask map (R metallic, G occlusion, B height, A smoothness).
// All 2048 max, trilinear, 8x aniso, mipmaps, high-quality compression.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public sealed class LookTestTextureImporter : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Content/Textures/";

        // Decals are clamped and keep alpha: moss, grime, drip, crack (folder names under B3_trims).
        public static bool IsDecal(string assetPath) =>
            assetPath.Contains("/B3_trims/moss/") || assetPath.Contains("/B3_trims/grime/") ||
            assetPath.Contains("/B3_trims/drip/") || assetPath.Contains("/B3_trims/crack/");

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool albedo = name.EndsWith("_albedo"), normal = name.EndsWith("_normal"), ms = name.EndsWith("_ms") || name.EndsWith("_mask");
            bool decal = IsDecal(assetPath);
            // T11: the ambient particle sprites (smoke puff, fish ring, flame, shaft beam: white RGB, the shape is the alpha) were forced to AlphaSource None here, so every one drew as a solid square
            // (the squares in the RoadOut sky, the chimney smoke and the rings). They keep their alpha and clamp. The sun cookie in the same folder keeps the old path (EnsureCookie sets its type).
            bool particle = assetPath.Contains("/Textures/Hollowmere/Ambient/") && !name.Contains("Cookie");

            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = albedo;
            ti.alphaSource = (albedo && decal) || ms || particle ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = (albedo && decal) || particle;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            ti.wrapMode = decal || particle ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.maxTextureSize = 2048;   // user rule 2026-10-30: the game targets 1440p, nothing above 2048 px
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.isReadable = false;
            ti.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
