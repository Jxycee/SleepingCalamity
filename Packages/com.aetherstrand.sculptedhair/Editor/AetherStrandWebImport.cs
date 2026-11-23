#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// One-call import of an AetherStrand WEB export (PresetGeometry.json + recipe/ExportedLook.json) onto any rigged character.
    /// Fits the web hair to the character's head (AetherStrandHeadFit), builds the mesh and material, and sets up physics.
    /// Pipeline: web recipe -> tools/web_export.ts (PresetGeometry.json) -> this importer. See Reference/sessions/aetherstrand-combo/PIPELINE.md.
    /// </summary>
    public static class AetherStrandWebImport
    {
        /// <summary>Applies a web export to <paramref name="character"/>. Returns the hair renderer or null on failure.</summary>
        public static SkinnedMeshRenderer Apply(GameObject character, string presetGeometryJson, string recipeJson, string outputFolder = "Assets/AetherStrandHair", string baseName = null)
        {
            if (character == null) { Debug.LogError("[AetherStrand] Apply: no character."); return null; }
            if (!File.Exists(presetGeometryJson)) { Debug.LogError("[AetherStrand] Apply: geometry not found: " + presetGeometryJson); return null; }
            baseName = string.IsNullOrEmpty(baseName) ? Path.GetFileName(Path.GetDirectoryName(presetGeometryJson)) : baseName;
            if (string.IsNullOrEmpty(baseName)) baseName = "WebHair";
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                string parent = Path.GetDirectoryName(outputFolder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(outputFolder));
            }

            Material mat = AetherStrandSetupWindow.CreateMaterialAsset(outputFolder + "/" + baseName + ".mat", recipeJson);
            if (mat == null) return null;

            var p = new HairSculptParams
            {
                overallScale = 1f, headLocalOffset = Vector3.zero, bangsLength = 1f, layerLength = 1f,
                widthScale = 1f, thicknessScale = 1f, autoFitToHeadMesh = true, legacyAutoNormalize = false
            };
            SkinnedMeshRenderer smr = AetherStrandMeshGenerator.GenerateAndAttach(character, p, presetGeometryJson, outputFolder + "/" + baseName + ".asset", mat);
            if (smr == null) return null;

            AetherStrandSetupWindow.AutoSetupPhysics(character, "Hair_", recipeJson, 0f, smr);
            EditorUtility.SetDirty(character);
            return smr;
        }

        /// <summary>
        /// Applies a web export with the exact web solver (AetherStrandWebHair): hair is a child of the head bone, the solver runs in web
        /// space and its vertices are mapped through the per-axis head fit into head-bone-local space. Returns the component or null.
        /// </summary>
        public static AetherStrandWebHair ApplyLive(GameObject character, string presetGeometryJson, string recipeJson, string outputFolder = "Assets/AetherStrandHair", string baseName = null)
        {
            if (character == null) { Debug.LogError("[AetherStrand] ApplyLive: no character."); return null; }
            if (!File.Exists(presetGeometryJson)) { Debug.LogError("[AetherStrand] ApplyLive: geometry not found: " + presetGeometryJson); return null; }
            Transform head = AetherStrandMeshGenerator.ResolveHeadBone(character);
            if (head == null) { Debug.LogError("[AetherStrand] ApplyLive: no head bone."); return null; }
            if (!AetherStrandHeadFit.TryCompute(character, head, out AetherStrandHeadFit.Fit fit)) { Debug.LogError("[AetherStrand] ApplyLive: head fit failed (no head-weighted mesh)."); return null; }
            baseName = string.IsNullOrEmpty(baseName) ? Path.GetFileName(Path.GetDirectoryName(presetGeometryJson)) : baseName;
            if (string.IsNullOrEmpty(baseName)) baseName = "WebHair";
            if (!AssetDatabase.IsValidFolder(outputFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(outputFolder).Replace('\\', '/'), Path.GetFileName(outputFolder));

            string geoAsset = outputFolder + "/" + baseName + "_geometry.json";
            File.Copy(presetGeometryJson, geoAsset, true);
            string recipeAsset = null;
            if (!string.IsNullOrEmpty(recipeJson) && File.Exists(recipeJson)) { recipeAsset = outputFolder + "/" + baseName + "_recipe.json"; File.Copy(recipeJson, recipeAsset, true); }
            AssetDatabase.Refresh();

            Material mat = AetherStrandSetupWindow.CreateMaterialAsset(outputFolder + "/" + baseName + ".mat", recipeAsset ?? recipeJson);
            if (mat == null) return null;

            var old = head.Find("AetherStrand_WebHair"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("AetherStrand_WebHair");
            go.transform.SetParent(head, false);
            var hair = go.AddComponent<AetherStrandWebHair>();
            hair.geometryJson = AssetDatabase.LoadAssetAtPath<TextAsset>(geoAsset);
            hair.recipeJson = recipeAsset != null ? AssetDatabase.LoadAssetAtPath<TextAsset>(recipeAsset) : null;
            hair.headPivot = head;
            hair.bustRoot = character.transform;
            hair.unitScale = (fit.scale.x + fit.scale.y + fit.scale.z) / 3f;
            hair.fitBone = head; hair.fitScale = fit.scale; hair.fitOffset = fit.offset;
            hair.material = mat;
            var drv = go.AddComponent<AetherStrandHeadCenterDriver>();
            drv.headBone = head; drv.localCenter = AetherStrandHeadFit.Apply(fit, AetherStrandHeadFit.WebHeadCenter);
            EditorUtility.SetDirty(character);
            Debug.Log("[AetherStrand] ApplyLive: fit " + fit.scale.ToString("F3") + " unitScale " + hair.unitScale.ToString("F3"));
            return hair;
        }

        [MenuItem("Tools/AetherStrand/Apply Web Export To Selected Character...")]
        private static void ApplyFromMenu()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null) { EditorUtility.DisplayDialog("AetherStrand", "Select the character root in the scene first.", "OK"); return; }
            string geo = EditorUtility.OpenFilePanel("PresetGeometry.json (web export)", "", "json");
            if (string.IsNullOrEmpty(geo)) return;
            string recipe = EditorUtility.OpenFilePanel("Recipe / ExportedLook.json (optional, cancel to skip)", Path.GetDirectoryName(geo), "json");
            Apply(target, geo, string.IsNullOrEmpty(recipe) ? null : recipe);
        }
    }
}
#endif
