// URP Lit materials for Tobin's hair (HairMesh/Textures, 2K, written by Art/Blender/tools/experimental/tobin_hair/shell/hair_atlas2.py):
//   Tobin_Hair       lock cards (Tobin_Hair renderer): alpha-cut strand atlas, single-sided (every card has a reversed twin, see build_shell_hair.py).
//   Tobin_HairShell  the modelled hair shell lying on the skull (Tobin_HairShell renderer) and the solid clump locks (Tobin_HairClumps): opaque clump texture.
//   Tobin_HairCap    flat scalp colour (older meshes' Tobin_HairCap object; HairDeck hair's scalp layer, FBX material named *HairCap*).
//   Tobin_HairCards  HairDeck hair cards (FBX material named *HairCards*): SleepingCalamity/HairCards, vertex colour + Kajiya-Kay, two-sided.
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

        public const string CardsPath = Folder + "/Materials/Tobin_HairCards.mat";
        /// <summary>HairDeck cards: SC_HairCards.shader (colour from the mesh's vertex colours, strands and highlights procedural).</summary>
        public static Material BuildCards()
        {
            var sh = Shader.Find("SleepingCalamity/HairCards");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CardsPath);
            if (mat == null) { mat = new Material(sh) { name = "Tobin_HairCards" }; AssetDatabase.CreateAsset(mat, CardsPath); }
            mat.shader = sh; mat.SetFloat("_Cull", 0f);
            // strand texture (hd_strand_tex.py): linear RGBA tiles, alpha = hair coverage; mips keep the coverage so distant hair does not thin out
            string sp = Folder + "/Textures/Hair_Strands.png"; AssetDatabase.ImportAsset(sp);
            if (AssetImporter.GetAtPath(sp) is TextureImporter sti)
            {
                sti.sRGBTexture = false; sti.alphaSource = TextureImporterAlphaSource.FromInput; sti.alphaIsTransparency = false; sti.mipmapEnabled = true;
                sti.mipMapsPreserveCoverage = true; sti.alphaTestReferenceValue = 0.42f; sti.wrapMode = TextureWrapMode.Clamp; sti.maxTextureSize = 2048;
                sti.textureCompression = TextureImporterCompression.CompressedHQ; sti.SaveAndReimport();
            }
            mat.SetTexture("_StrandTex", AssetDatabase.LoadAssetAtPath<Texture2D>(sp)); mat.SetFloat("_StrandVariants", 8f); mat.SetFloat("_AlphaCut", 0.42f);
            var look = LoadLook(); mat.SetFloat("_AlphaCut", look.alpha_cut);
            mat.SetFloat("_StrandTexBright", 0.85f); mat.SetFloat("_StrandBump", 0.7f); mat.SetFloat("_ShiftJitter", 0.3f); mat.SetFloat("_StrandDepth", 0.1f);
            // colour gradient from the reference hair (sRGB): deep brown roots, warm brown middle, light brown tips, ~1 in 7 strands lighter (reference: a little blonde, leans brown)
            mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_GradientMix", 1f); mat.SetFloat("_GradMid", 0.4f);
            // sweep pick w_trim60_core70_tintB: root #3a281c, tip #7a563c, mid chestnut between them
            mat.SetColor("_RootColor", Hex(look.root_tint, new Color32(0x3a, 0x28, 0x1c, 255))); mat.SetColor("_MidColor", Hex(look.mid_tint, new Color32(0x5c, 0x40, 0x2c, 255))); mat.SetColor("_TipColor", Hex(look.tip_tint, new Color32(0x7a, 0x56, 0x3c, 255)));
            mat.SetFloat("_ShadowLift", look.shadow_lift); mat.SetFloat("_HueShift", look.hue_shift); mat.SetFloat("_Saturation", look.saturation); mat.SetFloat("_Value", look.value);
            mat.SetColor("_BlondeColor", new Color(0.68f, 0.55f, 0.40f)); mat.SetFloat("_BlondeChance", 0.06f); mat.SetFloat("_StrandVar", 0.22f); mat.SetFloat("_RootDark", 0.25f); mat.SetFloat("_Ambient", 1.15f);
            // back of the head read orange under the key light: cooler, weaker highlights, less tip lift
            mat.SetColor("_Spec1Color", new Color(0.28f, 0.26f, 0.25f)); mat.SetColor("_Spec2Color", new Color(0.14f, 0.12f, 0.11f)); mat.SetFloat("_TipLift", 0.04f); mat.SetFloat("_ShadowStrength", 0.55f); mat.SetFloat("_ShadowNormalOffset", 0.008f);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); return mat;
        }

        /// <summary>Hair look knobs for the cards material, read from one JSON file (defaults = the values that were hard-coded before, so a missing file changes nothing).</summary>
        [System.Serializable] public class HairLook
        {
            public float shadow_lift = 0f, hue_shift = 0f, saturation = 1f, value = 1f, alpha_cut = 0.42f;
            public string root_tint = "#3a281c", mid_tint = "#5c402c", tip_tint = "#7a563c";
        }
        public const string LookPath = "Art/Blender/tools/experimental/tobin_hair/sweep/unity_look.json";
        public static HairLook LoadLook()
        {
            string p = System.Environment.GetEnvironmentVariable("SC_HAIR_LOOK"); if (string.IsNullOrEmpty(p)) p = LookPath;
            return File.Exists(p) ? JsonUtility.FromJson<HairLook>(File.ReadAllText(p)) : new HairLook();
        }
        static Color Hex(string h, Color fallback) => ColorUtility.TryParseHtmlString(h, out var c) ? c : fallback;

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
            var m = Build(); var cap = BuildCap(); var shell = BuildShell(); var cards = BuildCards(); string slots = "";
            foreach (var r in hair.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Length > 1)
                {   // the clump object carries two slots (from Blender): solid clumps (Tobin_HairShell) + alpha wisps (Tobin_Hair)
                    for (int i = 0; i < mats.Length; i++) { slots += r.name + "[" + i + "]=" + (mats[i] ? mats[i].name : "null") + " "; mats[i] = mats[i] != null && mats[i].name.Contains("HairShell") ? shell : m; }
                    r.sharedMaterials = mats; continue;
                }
                string mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (mn.Contains("HairCards")) { r.sharedMaterial = cards; slots += r.name + "=cards "; continue; }   // HairDeck hair (hd_export.py names the materials)
                if (mn.Contains("HairCap")) { r.sharedMaterial = cap; slots += r.name + "=cap "; continue; }
                r.sharedMaterial = r.name.Contains("HairCap") ? cap : (r.name.Contains("HairShell") || r.name.Contains("HairClumps")) ? shell : m;   // solid clump locks share the opaque shell material
            }
            return m.name + "+" + shell.name + " " + slots;
        }
    }
}
