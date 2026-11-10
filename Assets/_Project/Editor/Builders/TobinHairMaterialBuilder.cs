// URP Lit materials for Tobin's hair (HairMesh/Textures, 2K, written by Art/Blender/tools/experimental/tobin_hair/shell/hair_atlas2.py):
//   Tobin_Hair       lock cards (Tobin_Hair renderer): alpha-cut strand atlas, single-sided (every card has a reversed twin, see build_shell_hair.py).
//   Tobin_HairShell  the modelled hair shell lying on the skull (Tobin_HairShell renderer) and the solid clump locks (Tobin_HairClumps): opaque clump texture.
//   Tobin_HairCap    flat scalp colour, only for the older hair meshes that still carry a Tobin_HairCap object.
// Menu-free entry: unity.ps1 exec -Method SleepingCalamity.EditorTools.TobinHairMaterialBuilder.Build
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class TobinHairMaterialBuilder
    {
        public const string Folder = "Assets/_Project/Content/Characters/TobinBase/HairMesh";
        public const string MatPath = Folder + "/Materials/Tobin_Hair.mat";

        public static Material Build()
        {
            Directory.CreateDirectory(Folder + "/Materials");
            Import("Hair_Cards_BaseColor", srgb: true, normal: false); Import("Hair_Cards_Normal", srgb: false, normal: true); Import("Hair_Cards_MetallicSmooth", srgb: false, normal: false);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tobin_Hair" }; AssetDatabase.CreateAsset(mat, MatPath); }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", Tex("Hair_Cards_BaseColor")); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", Tex("Hair_Cards_Normal")); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f);
            mat.SetTexture("_MetallicGlossMap", Tex("Hair_Cards_MetallicSmooth")); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 1f); mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.38f); mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = 2450; mat.SetOverrideTag("RenderType", "TransparentCutout");   // hair cards: alpha-cut strand atlas
            mat.SetFloat("_Cull", 2f);   // single-sided (back faces were the black/navy chunks); the Blender step orients every strip face outward
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); return mat;
        }

        public const string CapPath = Folder + "/Materials/Tobin_HairCap.mat";
        /// <summary>Flat dark root colour for the scalp cap under the strips (hides gaps between sparse strips).</summary>
        public static Material BuildCap()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CapPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tobin_HairCap" }; AssetDatabase.CreateAsset(mat, CapPath); }
            mat.SetColor("_BaseColor", new Color(0.30f, 0.18f, 0.105f, 1f)); mat.SetFloat("_Smoothness", 0.25f); mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat); return mat;
        }

        public const string ShellPath = Folder + "/Materials/Tobin_HairShell.mat";
        /// <summary>Opaque material for the modelled hair shell (clump strands along the hair flow, normal map, low smoothness).</summary>
        public static Material BuildShell()
        {
            Import("Hair_Shell_BaseColor", srgb: true, normal: false); Import("Hair_Shell_Normal", srgb: false, normal: true); Import("Hair_Shell_MetallicSmooth", srgb: false, normal: false);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ShellPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tobin_HairShell" }; AssetDatabase.CreateAsset(mat, ShellPath); }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", Tex("Hair_Shell_BaseColor")); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", Tex("Hair_Shell_Normal")); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f);
            mat.SetTexture("_MetallicGlossMap", Tex("Hair_Shell_MetallicSmooth")); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 1f); mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); return mat;
        }

        static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + n + ".png");

        static void Import(string n, bool srgb, bool normal)
        {
            string p = Folder + "/Textures/" + n + ".png"; AssetDatabase.ImportAsset(p);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti == null) return;
            ti.sRGBTexture = srgb; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = srgb; if (srgb) { ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.38f; } ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.maxTextureSize = 2048; ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }

        /// <summary>Applies the hair material to every renderer under the instance; returns its name.</summary>
        public static string Apply(GameObject hair)
        {
            var m = Build(); var cap = BuildCap(); var shell = BuildShell();
            foreach (var r in hair.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Length > 1)
                {   // the clump object carries two slots (from Blender): solid clumps (Tobin_HairShell) + alpha wisps (Tobin_Hair)
                    for (int i = 0; i < mats.Length; i++) mats[i] = mats[i] != null && mats[i].name.Contains("HairShell") ? shell : m;
                    r.sharedMaterials = mats; continue;
                }
                r.sharedMaterial = r.name.Contains("HairCap") ? cap : (r.name.Contains("HairShell") || r.name.Contains("HairClumps")) ? shell : m;   // solid clump locks share the opaque shell material
            }
            return m.name + "+" + shell.name;
        }
    }
}
