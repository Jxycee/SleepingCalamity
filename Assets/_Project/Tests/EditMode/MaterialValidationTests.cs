// Authority: none (EditMode). Guards "builders write the same bytes with and without graphics": a graphics editor re-validates builder-written materials
// (URP ShaderGUI: MotionVectors pass, particle blend state, _Color mirror) and a capture run then saves them, so every committed material must already be in
// the validated state (MaterialValidation.SettleAll runs at the end of ProjectBuilder.RunAll).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class MaterialValidationTests
    {
        [Test]
        public void EveryCommittedMaterial_IsAlreadyInItsValidatedState()
        {
            var flipped = new List<string>();
            int checkedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Content/Materials" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;
                var clone = new Material(m);
                try
                {
                    string before = MaterialValidation.Signature(clone);
                    if (!MaterialValidation.Validate(clone)) continue;
                    checkedCount++;
                    if (MaterialValidation.Signature(clone) != before) flipped.Add(path);
                }
                finally { Object.DestroyImmediate(clone); }
            }
            Assert.That(checkedCount, Is.GreaterThan(50), "the validation hook found no ShaderGUI: the guard would pass vacuously");
            Assert.That(flipped, Is.Empty, "a graphics editor would rewrite these (run SleepingCalamity/Build All):\n" + string.Join("\n", flipped));
        }

        [Test]
        public void Validation_DisablesTheMotionVectorsPass_OnAnUrpLitMaterial_AndKeepsItOnWearLit()
        {
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                lit.SetShaderPassEnabled("MotionVectors", true);
                Assert.That(MaterialValidation.Validate(lit), Is.True, "URP Lit has a ShaderGUI");
                Assert.That(lit.GetShaderPassEnabled("MotionVectors"), Is.False);
            }
            finally { Object.DestroyImmediate(lit); }
        }
    }
}
