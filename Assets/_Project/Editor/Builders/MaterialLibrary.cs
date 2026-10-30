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

            // M3 combat greybox: enemy bodies (the per-instance tint comes from a MaterialPropertyBlock), weapons, sparks and translucent footprints.
            Ensure(lit, "M_EnemySlime", new Color(0.80f, 0.15f, 0.30f));
            Ensure(lit, "M_EnemyRat", new Color(0.45f, 0.36f, 0.30f));
            Ensure(lit, "M_EnemyBroom", new Color(0.62f, 0.46f, 0.26f));
            Ensure(lit, "M_EnemyWarden", new Color(0.90f, 0.78f, 0.52f));
            Ensure(lit, "M_EnemyPart", new Color(0.95f, 0.55f, 0.25f));
            Ensure(lit, "M_WeaponWood", new Color(0.45f, 0.30f, 0.16f));
            Ensure(lit, "M_WeaponSteel", new Color(0.78f, 0.80f, 0.84f));
            Ensure(lit, "M_HealDrop", new Color(0.35f, 1f, 0.5f));
            Ensure(unlit, "M_Spark", new Color(1f, 1f, 1f));
            EnsureTransparentUnlit(unlit, "M_Telegraph", new Color(1f, 0.5f, 0.1f, 0.3f));
            EnsureTransparentUnlit(unlit, "M_SwingArc", new Color(0.9f, 0.95f, 1f, 0.3f));
        }

        // name without extension, e.g. "M_GreyboxGround".
        public static Material Get(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/" + name + ".mat");
            if (material == null) throw new System.InvalidOperationException("Material not built: " + name + " (run MaterialLibrary.BuildAll first)");
            return material;
        }

        // URP Unlit set to the Transparent surface (alpha blend, no depth write), the way the material inspector would; the colour (and its alpha) is
        // overridden per instance with a MaterialPropertyBlock.
        static void EnsureTransparentUnlit(Shader shader, string name, Color color)
        {
            Ensure(shader, name, color);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/" + name + ".mat");
            bool changed = false;
            if (m.GetFloat("_Surface") != 1f) { m.SetFloat("_Surface", 1f); changed = true; }
            if (m.GetFloat("_Blend") != 0f) { m.SetFloat("_Blend", 0f); changed = true; }
            if (m.GetFloat("_SrcBlend") != (float)UnityEngine.Rendering.BlendMode.SrcAlpha) { m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); changed = true; }
            if (m.GetFloat("_DstBlend") != (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha) { m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); changed = true; }
            if (m.GetFloat("_ZWrite") != 0f) { m.SetFloat("_ZWrite", 0f); changed = true; }
            if (!m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) { m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); changed = true; }
            if (m.renderQueue != 3000) { m.renderQueue = 3000; changed = true; }
            if (m.GetTag("RenderType", false) != "Transparent") { m.SetOverrideTag("RenderType", "Transparent"); changed = true; }
            if (changed) EditorUtility.SetDirty(m);
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
