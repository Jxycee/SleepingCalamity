// Final art for Hollowmere's ring-1 buildings (docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 5.2), placed by HollowmereBuilder
// on their lots when the export is in, else the builder keeps its greybox block. One FBX per building: Frame, Part_* meshes (pivot at their socket),
// SOCKET_* empties under Frame and a flat FOOTPRINT box. The Rebuilt preset shows every part placed (P28: a building counts once all its parts are on).
// Materials: the facade kit's shared-tileable WearLit slots (BeautyCornerArt.BuildFacadeMaterials) plus the building's own BK_* slots, built the same way
// (a B1/B2 tileable tinted as the generator's tile_mat tint), so frame and parts share one look with the vertex-colour wear.
// The FOOTPRINT is checked against the lot's size class and fails the build when it slips past it (design 10, footprint drift).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereArt
    {
        public const string BakeryFolder = "Assets/_Project/Content/Art/Town/Ring1/Bakery";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string WearShaderName = "SleepingCalamity/WearLit";
        public static readonly string[] BakeryParts = { "Part_Door", "Part_Oven", "Part_Sign" };

        // slot -> LookTest source material and the generator's linear tint (sc_bakery.py EXTRA). Tints above 1 keep their hue, scaled to fit.
        static readonly (string Slot, string Source, Color Tint)[] BakerySlots =
        {
            ("BK_Brick", "old_brick", new Color(1.08f, 0.96f, 0.90f)), ("BK_Soot", "soot_coal", new Color(0.8f, 0.75f, 0.7f)),
            ("BK_Limewash", "plaster", new Color(1.04f, 1.03f, 1.0f)), ("BK_Bread", "plaster", new Color(1.0f, 0.68f, 0.36f)),
            ("BK_Crust", "plaster", new Color(0.62f, 0.36f, 0.17f)), ("BK_Flour", "plaster", new Color(1.35f, 1.33f, 1.28f)),
            ("BK_Wicker", "oak_timber", new Color(1.55f, 1.25f, 0.85f)), ("BK_Sack", "canvas", new Color(1.0f, 0.94f, 0.82f)),
            ("BK_Cloth", "canvas", new Color(1.05f, 0.62f, 0.55f)), ("BK_Gilt", "plaster", new Color(1.15f, 0.80f, 0.30f)),
        };

        // Places the bakery art in the lot frame g (origin at the lot centre on its pad, local -Z = the lot's door side). False when the export is missing.
        public static bool TryPlaceBakery(Transform g, Foundation f, float footingTop)
        {
            GameObject fbx = BeautyCornerArt.LoadModel(BakeryFolder, "Bakery.fbx", keepMaterialSlots: true);
            if (fbx == null) return false;
            Dictionary<string, Material> slots = BakeryMaterials();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, g);
            obj.name = "Art_Bakery";
            obj.transform.localPosition = new Vector3(0f, footingTop, 0f);
            obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the export's door side is local +Z (like the facade): turn it to the lot's -Z
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") { r.enabled = false; continue; }
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) throw new InvalidOperationException("Bakery: no material for slot '" + slot + "' on " + r.name);
                }
                r.sharedMaterials = mats;
            }
            Transform foot = null;
            foreach (Transform t in obj.GetComponentsInChildren<Transform>(true)) if (t.name == "FOOTPRINT") foot = t;
            MeshFilter fm = foot != null ? foot.GetComponent<MeshFilter>() : null;
            if (fm == null || fm.sharedMesh == null) throw new InvalidOperationException("Bakery.fbx has no FOOTPRINT mesh");
            // Its extent in the lot frame (rotation and any FBX unit scale included): the eight bounds corners through the hierarchy.
            Bounds b = fm.sharedMesh.bounds;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = g.InverseTransformPoint(foot.TransformPoint(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z)));
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            Vector3 size = new Vector3(x1 - x0, 0f, z1 - z0);
            float max = Foundation.MaxBuilding(f.Size);
            if (size.x > max + 1e-3f || size.z > max + 1e-3f)
                throw new InvalidOperationException("Bakery FOOTPRINT " + size.x.ToString("0.00") + " x " + size.z.ToString("0.00") + " m is larger than the " + f.Size + " class allows (" + max + " m)");
            foreach (string p in BakeryParts)
                if (obj.transform.Find(p) == null) throw new InvalidOperationException("Bakery.fbx has no " + p);
            Debug.Log("SC HOLLOWMERE bakery on " + f.Id + " footprint=" + size.x.ToString("0.00") + "x" + size.z.ToString("0.00") + " class=" + f.Size + " parts=" + BakeryParts.Length);
            return true;
        }

        static Dictionary<string, Material> BakeryMaterials()
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? throw new InvalidOperationException("Shader not found: " + WearShaderName);
            foreach (var s in BakerySlots)
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
            Material interior = LookTestBuilder.Ensure(MaterialsFolder + "/M_BK_Interior.mat", wear);
            interior.SetColor("_BaseColor", new Color(0.035f, 0.025f, 0.018f).gamma); interior.SetFloat("_Smoothness", 0.1f); interior.SetFloat("_Metallic", 0f);
            interior.SetColor("_Color", interior.GetColor("_BaseColor")); interior.SetShaderPassEnabled("MotionVectors", true);   // same bytes with and without graphics
            EditorUtility.SetDirty(interior); map["BK_Interior"] = interior;
            // The lit oven mouth: URP Lit with an HDR orange emission (the generator's strength 6 read as about x3 here, the corner's exposure is lower).
            Material ember = LookTestBuilder.Ensure(MaterialsFolder + "/M_BK_Ember.mat", Shader.Find("Universal Render Pipeline/Lit"));
            ember.SetColor("_BaseColor", new Color(0.35f, 0.08f, 0.01f).gamma); ember.SetFloat("_Smoothness", 0.2f);
            ember.SetColor("_EmissionColor", new Color(1.0f, 0.36f, 0.06f) * 3f); ember.EnableKeyword("_EMISSION");
            ember.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            ember.SetColor("_Color", ember.GetColor("_BaseColor")); ember.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(ember); map["BK_Ember"] = ember;
            AssetDatabase.SaveAssets();
            return map;
        }
    }
}
