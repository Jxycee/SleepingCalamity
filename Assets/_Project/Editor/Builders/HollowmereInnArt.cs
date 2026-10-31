// Final art for Hollowmere's inn on its L lot (docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 5.2), placed by HollowmereBuilder
// on the lot when the export is in, else the builder keeps its greybox block. One FBX: Frame, Part_* meshes (pivot at their socket), SOCKET_* empties under
// Frame and a flat FOOTPRINT box. The Rebuilt preset shows every part placed (P28: a building counts once all its parts are on).
// Materials: the facade kit's shared-tileable WearLit slots (BeautyCornerArt.BuildFacadeMaterials) plus the inn's own IN_* slots, built the same way as the
// bakery's BK_* slots (a LookTest tileable tinted as the generator's tile_mat tint), so frame and parts share one look with the vertex-colour wear.
// The FOOTPRINT is checked against the lot's size class and fails the build when it slips past it (design 10, footprint drift).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereInnArt
    {
        public const string InnFolder = "Assets/_Project/Content/Art/Town/Ring1/Inn";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string WearShaderName = "SleepingCalamity/WearLit";
        const float PaintLum = 0.17f;   // the generator's PAINT_LUM, as BeautyCornerArt
        public static readonly string[] InnParts = { "Part_Door", "Part_Sign", "Part_Porch", "Part_Lantern" };

        // slot -> LookTest source material and the generator's linear tint. Tints above 1 keep their hue, scaled to fit.
        static readonly (string Slot, string Source, Color Tint)[] InnSlots =
        {
            ("IN_Timber", "oak_timber", new Color(0.95f, 0.44f, 0.35f)), ("IN_Gilt", "plaster", new Color(1.15f, 0.80f, 0.30f)),
        };

        // Places the inn art in the lot frame g (origin at the lot centre on its pad, local -Z = the lot's door side). False when the export is missing.
        public static bool TryPlaceInn(Transform g, Foundation f, float footingTop)
        {
            GameObject fbx = BeautyCornerArt.LoadModel(InnFolder, "Inn.fbx", keepMaterialSlots: true);
            if (fbx == null) return false;
            Dictionary<string, Material> slots = InnMaterials();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, g);
            obj.name = "Art_Inn";
            obj.transform.localPosition = new Vector3(0f, footingTop, 0f);
            obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the export's door side is local +Z (like the facade): turn it to the lot's -Z
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") { r.enabled = false; r.shadowCastingMode = ShadowCastingMode.Off; continue; }
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) throw new InvalidOperationException("Inn: no material for slot '" + slot + "' on " + r.name);
                }
                r.sharedMaterials = mats;
            }
            Transform foot = obj.transform.Find("FOOTPRINT") ?? throw new InvalidOperationException("Inn.fbx has no FOOTPRINT");
            Bounds b = foot.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 size = Vector3.Scale(b.size, foot.localScale);
            float max = Foundation.MaxBuilding(f.Size);
            if (size.x > max + 1e-3f || size.z > max + 1e-3f)
                throw new InvalidOperationException("Inn FOOTPRINT " + size.x.ToString("0.00") + " x " + size.z.ToString("0.00") + " m is larger than the " + f.Size + " class allows (" + max + " m)");
            foreach (string p in InnParts)
                if (obj.transform.Find(p) == null) throw new InvalidOperationException("Inn.fbx has no " + p);
            Debug.Log("SC HOLLOWMERE inn on " + f.Id + " footprint=" + size.x.ToString("0.00") + "x" + size.z.ToString("0.00") + " class=" + f.Size + " parts=" + InnParts.Length);
            return true;
        }

        static Dictionary<string, Material> InnMaterials()
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? throw new InvalidOperationException("Shader not found: " + WearShaderName);
            foreach (var s in InnSlots)
            {
                Material src = LookTestBuilder.Mat(s.Source) ?? throw new InvalidOperationException("LookTest material missing: " + s.Source);
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                float peak = Mathf.Max(1f, Mathf.Max(s.Tint.r, Mathf.Max(s.Tint.g, s.Tint.b)));
                Color tint = new Color(s.Tint.r / peak, s.Tint.g / peak, s.Tint.b / peak, 1f);
                m.SetColor("_BaseColor", tint.gamma);   // linear tint handed over as sRGB, as BuildFacadeMaterials
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m);
                map[s.Slot] = m;
            }
            // The sign board: painted wood in WearLit paint mode, #2c3e52, as the facade's FK_Shutter.
            Material field = LookTestBuilder.Ensure(MaterialsFolder + "/M_IN_SignField.mat", wear);
            Material paintSrc = LookTestBuilder.Mat("painted_wood") ?? throw new InvalidOperationException("LookTest material missing: painted_wood");
            field.CopyPropertiesFromMaterial(paintSrc);
            foreach (var k in paintSrc.enabledKeywords) field.EnableKeyword(k.name);
            field.EnableKeyword("_SC_PAINT");
            field.SetColor("_BaseColor", Color.white); field.SetColor("_Color", Color.white);
            Color paint = ((Color)new Color32(0x2c, 0x3e, 0x52, 255)).linear;
            field.SetVector("_SpecColor", new Vector4(paint.r / PaintLum, paint.g / PaintLum, paint.b / PaintLum, 1f));   // raw linear, SetVector skips the colour-space conversion
            field.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(field); map["IN_SignField"] = field;
            Material interior = LookTestBuilder.Ensure(MaterialsFolder + "/M_IN_Interior.mat", wear);
            interior.SetColor("_BaseColor", new Color(0.035f, 0.025f, 0.018f).gamma); interior.SetFloat("_Smoothness", 0.1f); interior.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(interior); map["IN_Interior"] = interior;
            // The lit lantern panes: URP Lit with an HDR warm emission.
            Material flame = LookTestBuilder.Ensure(MaterialsFolder + "/M_IN_Flame.mat", Shader.Find("Universal Render Pipeline/Lit"));
            flame.SetColor("_BaseColor", new Color(0.40f, 0.22f, 0.06f).gamma); flame.SetFloat("_Smoothness", 0.2f);
            flame.SetColor("_EmissionColor", new Color(1.0f, 0.62f, 0.22f) * 2f); flame.EnableKeyword("_EMISSION");
            flame.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(flame); map["IN_Flame"] = flame;
            AssetDatabase.SaveAssets();
            return map;
        }
    }
}
