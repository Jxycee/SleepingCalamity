// Flat URP Lit placeholder materials for the greybox. Updated in place, so GUIDs never change. The real PBR materials come with the
// style bible work (docs/PHASE1.md section 3); nothing here is final art.
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class MaterialLibrary
    {
        public const string MaterialsFolder = "Assets/_Project/Content/Materials";
        const string LitShaderName = "Universal Render Pipeline/Lit";
        const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void BuildAll()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find(LitShaderName);
            if (lit == null) throw new System.InvalidOperationException("Shader not found: " + LitShaderName);

            Ensure(lit, "M_GreyboxGround", new Color(0.46f, 0.50f, 0.42f));
            Ensure(lit, "M_GreyboxPlayer", new Color(0.86f, 0.62f, 0.28f));
            Ensure(lit, "M_GreyboxDummy", new Color(0.50f, 0.60f, 0.78f));
            Ensure(lit, "M_GreyboxElite", new Color(0.86f, 0.26f, 0.20f));
            Ensure(lit, "M_GreyboxWall", new Color(0.58f, 0.54f, 0.50f));
            Ensure(lit, "M_GreyboxStep", new Color(0.66f, 0.62f, 0.52f));
            Shader unlit = Shader.Find(UnlitShaderName);
            if (unlit == null) throw new System.InvalidOperationException("Shader not found: " + UnlitShaderName);
            Ensure(unlit, "M_LockIndicator", new Color(1f, 0.85f, 0.15f));
        }

        // name without extension, e.g. "M_GreyboxGround".
        public static Material Get(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/" + name + ".mat");
            if (material == null) throw new System.InvalidOperationException("Material not built: " + name + " (run MaterialLibrary.BuildAll first)");
            return material;
        }

        static void Ensure(Shader shader, string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool changed = false;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }
            if (material.shader != shader) { material.shader = shader; changed = true; }
            if (material.GetColor(BaseColorId) != color) { material.SetColor(BaseColorId, color); changed = true; }
            if (changed) EditorUtility.SetDirty(material);
        }
    }
}
