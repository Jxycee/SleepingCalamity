// Builds the URP Lit materials for the Tobin base model from the MetaPerson GLB textures (Content/Characters/TobinBase/Textures, original resolution,
// extracted unchanged) and binds them to renderers by mesh name. Base maps import as sRGB, the supplied normal maps as Normal map (no generated normals).
//   Body, Head: skin base colour + normal; LeftEye/RightEye: eye base + normal (one shared image pair); TeethUpper/Lower: teeth base + normal (shared);
//   Eyelashes: opaque black (the GLB gives a flat black factor and no texture); BaseUnderlay_*: flat skin tone (GLB factor, converted linear to gamma).
// Throwaway-safe: Apply() only changes the instance's renderers, it never edits the FBX.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class TobinBaseMaterialBuilder
    {
        public const string Folder = "Assets/_Project/Content/Characters/TobinBase";
        public const string TexFolder = Folder + "/Textures";
        public const string MatFolder = Folder + "/Materials";

        /// <summary>Slight tan (user, 2026-11-08): multiplies the _BaseColor of head skin, body skin and the underlay. Textures stay untouched; eyes, teeth and lashes are not tinted.</summary>
        public static readonly Color SkinTint = new Color(0.95f, 0.87f, 0.78f, 1f);

        // Menu-free entry: unity.ps1 exec -Method SleepingCalamity.EditorTools.TobinBaseMaterialBuilder.Build
        public static void Build()
        {
            Directory.CreateDirectory(MatFolder);
            foreach (var f in Directory.GetFiles(TexFolder, "*.png"))
            {
                string p = f.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                bool normal = Path.GetFileNameWithoutExtension(p).EndsWith("_Normal");
                var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != want || ti.sRGBTexture == normal || ti.maxTextureSize < 2048)
                {
                    ti.textureType = want;
                    ti.sRGBTexture = !normal;
                    ti.maxTextureSize = 2048;
                    ti.mipmapEnabled = true;
                    ti.SaveAndReimport();
                }
            }
            Make("Skin_Body", "Body", 0.30f, SkinTint);
            Make("Skin_Head", "Head", 0.34f, SkinTint);
            Make("Eye", "Eye", 0.90f, Color.white);
            Make("Teeth", "Teeth", 0.55f, Color.white);
            var lash = Material("Eyelashes");
            lash.SetColor("_BaseColor", Color.black); lash.SetFloat("_Smoothness", 0.25f);
            var under = Material("Underlay");
            under.SetColor("_BaseColor", new Color(0.7082978f, 0.3940831f, 0.2801244f, 1f).gamma * SkinTint); under.SetFloat("_Smoothness", 0.5f);
            AssetDatabase.SaveAssets();
        }

        static Material Material(string name)
        {
            string path = MatFolder + "/Tobin_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Make(string matName, string texPrefix, float smooth, Color tint)
        {
            var m = Material(matName);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", Tex(texPrefix + "_BaseColor"));
            m.SetTexture("_BumpMap", Tex(texPrefix + "_Normal"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smooth);
        }

        static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "/" + n + ".png") ?? throw new FileNotFoundException(n);

        static Material Load(string n) => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Tobin_" + n + ".mat") ?? throw new FileNotFoundException("run TobinBaseMaterialBuilder.Build first: " + n);

        /// <summary>Material for a renderer by mesh name, or null when the name is not one of ours.</summary>
        public static Material For(string rendererName)
        {
            string n = rendererName;
            if (n.Contains("Underlay")) return Load("Underlay");
            if (n.Contains("Eyelash")) return Load("Eyelashes");
            if (n.Contains("Eyeball") || n.Contains("Eye")) return Load("Eye");
            if (n.Contains("Teeth")) return Load("Teeth");
            if (n.Contains("Head")) return Load("Skin_Head");
            if (n.Contains("Body")) return Load("Skin_Body");
            return null;
        }

        /// <summary>Assigns materials by renderer name on an instance. Returns the list "name -> material" for logging.</summary>
        public static string Apply(GameObject instance)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var m = For(r.name);
                if (m == null) { sb.AppendLine("UNBOUND " + r.name); continue; }
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
                sb.AppendLine(r.name + " -> " + m.name);
            }
            return sb.ToString();
        }
    }
}
