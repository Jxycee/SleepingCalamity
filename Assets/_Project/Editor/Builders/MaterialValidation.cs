// Authority: none (editor-only). Settles every builder-written material into the state Unity's own material validation produces.
// In a graphics editor the URP ShaderGUI validation (BaseShaderGUI.ValidateMaterial: MotionVectors pass off for URP shaders that have one, blend
// state of the particle shaders, _Color mirrors _BaseColor, emission keyword) rewrites a material as soon as something touches it, and a later capture run
// then saves the difference. -nographics never runs it, so the same builder wrote different bytes in the two modes. ProjectBuilder runs
// SettleAll before its final save, and MaterialValidationTests fails when a committed material is not in this state, so both modes write the same bytes.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    internal static class MaterialValidation
    {
        const string MaterialsRoot = "Assets/_Project/Content/Materials";
        static readonly PropertyInfo CustomShaderGui = typeof(MaterialEditor).GetProperty("customShaderGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        static readonly string[] Passes = { "MotionVectors", "ShadowCaster", "DepthOnly", "DepthNormals", "XRMotionVectors", "ForwardLit", "UniversalForward" };

        // Runs the shader's ShaderGUI validation on the material in place. False when the shader has no custom GUI (nothing to settle).
        public static bool Validate(Material m)
        {
            if (m == null || m.shader == null) return false;
            var editor = Editor.CreateEditor(m) as MaterialEditor;
            if (editor == null) return false;
            try
            {
                var gui = CustomShaderGui?.GetValue(editor) as ShaderGUI;
                if (gui == null) return false;
                gui.ValidateMaterial(m);
                return true;
            }
            finally { Object.DestroyImmediate(editor); }
        }

        // Validates every material under Content/Materials (and any extra roots) and marks the ones that changed dirty. Returns how many changed.
        public static int SettleAll()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsRoot }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g), System.StringComparer.Ordinal))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                string before = Signature(m);
                if (!Validate(m)) continue;
                if (Signature(m) != before) { EditorUtility.SetDirty(m); changed++; }
            }
            return changed;
        }

        // Everything validation can touch, as one comparable string.
        public static string Signature(Material m)
        {
            var sb = new StringBuilder();
            sb.Append(m.shader.name).Append('|').Append(m.renderQueue).Append('|').Append((int)m.globalIlluminationFlags).Append('|').Append(m.GetTag("RenderType", false, ""));
            sb.Append("|kw:").Append(string.Join(",", m.shaderKeywords.OrderBy(k => k, System.StringComparer.Ordinal)));
            sb.Append("|passes:");
            foreach (string p in Passes) sb.Append(m.GetShaderPassEnabled(p) ? '1' : '0');
            Shader s = m.shader;
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                string n = s.GetPropertyName(i);
                switch (s.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float: case ShaderPropertyType.Range: sb.Append('|').Append(n).Append('=').Append(m.GetFloat(n).ToString("R")); break;
                    case ShaderPropertyType.Int: sb.Append('|').Append(n).Append('=').Append(m.GetInt(n)); break;
                    case ShaderPropertyType.Color: sb.Append('|').Append(n).Append('=').Append(m.GetColor(n).ToString("F6")); break;
                    case ShaderPropertyType.Vector: sb.Append('|').Append(n).Append('=').Append(m.GetVector(n).ToString("F6")); break;
                }
            }
            return sb.ToString();
        }
    }
}
