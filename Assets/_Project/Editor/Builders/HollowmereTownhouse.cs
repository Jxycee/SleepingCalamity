// Home kit family 3, the tall jettied townhouse (spec 2026-10-30 section 5.4 #3), placed by HollowmereBuilder on the lots HollowmereLayout.RebuiltBuilding
// marks Townhouse. Art: Assets/_Project/Content/Art/Town/Ring1/Townhouse/<Variant>/<Variant>.fbx from Art/Blender/town/sc_townhouse.py (SOURCES.md there):
// root <Variant> with Frame, Part_Door, Part_Shutters, SOCKET_Door, SOCKET_Shutters and FOOTPRINT. The FBX front is local +Z (B4 convention), so the art turns
// 180 to face the lot's door side (local -Z). The house stands on the lot's footing slab (top at 0.25 m above the pad; the lot transform already carries the
// pad height). Materials: the B4 facade slots with the variant's plaster tint and shutter/door paint (BeautyCornerArt.BuildNeighbourMaterials).
// FOOTPRINT: renderer off, a BoxCollider from its bounds; the build fails if it does not fit the foundation's size class (spec section 10, footprint drift).
// Parts are visible here because the Rebuilt preset shows every building complete (M6 placement hides them until placed).
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereTownhouse
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Ring1/Townhouse";
        const float FootingTop = 0.25f;

        // Variant per lot, and each variant's plaster tint and paint (linear), as sc_townhouse.py COMBOS gives them.
        static readonly Dictionary<string, string> LotVariant = new Dictionary<string, string> { { "F06", "Townhouse_A" }, { "F07", "Townhouse_B" } };
        static readonly Dictionary<string, (Color Plaster, Color Paint)> Looks = new Dictionary<string, (Color, Color)>
        {
            { "Townhouse_A", (new Color(0.95f, 0.96f, 0.97f), Hex(0x3e, 0x56, 0x70)) },   // cool lime white, slate blue
            { "Townhouse_B", (new Color(1.0f, 0.93f, 0.82f), Hex(0x7a, 0x4a, 0x5c)) },    // warm cream, plum
            { "Townhouse_C", (new Color(1.0f, 0.78f, 0.58f), Hex(0x4a, 0x6a, 0x48)) },    // ochre wash, sage-forest green
        };
        static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f).linear;

        public static string VariantFor(string foundationId) =>
            LotVariant.TryGetValue(foundationId, out string v) ? v : (foundationId[foundationId.Length - 1] & 1) == 0 ? "Townhouse_A" : "Townhouse_C";

        // Places the townhouse on lot g (the lot's facing frame). Returns false when the art is missing (the caller builds the greybox instead).
        public static bool Place(Transform g, Foundation f)
        {
            string variant = VariantFor(f.Id);
            string folder = Folder + "/" + variant;
            GameObject fbx = BeautyCornerArt.LoadModel(folder, variant + ".fbx", keepMaterialSlots: true);
            if (fbx == null) { Debug.LogWarning("SC HOLLOWMERE townhouse art missing: " + folder); return false; }
            var look = Looks[variant];
            Dictionary<string, Material> slots = BeautyCornerArt.BuildNeighbourMaterials(variant, look.Plaster, look.Paint);

            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, g);
            obj.name = "Art_" + variant;
            obj.transform.localPosition = new Vector3(0f, FootingTop, 0f);
            obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") continue;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE townhouse: no material for slot '" + slot + "' on " + r.name); mats[k] = LookTestBuilder.Mat("plaster"); }
                    if (slot.StartsWith("FK_Decal")) r.shadowCastingMode = ShadowCastingMode.Off;
                }
                r.sharedMaterials = mats;
            }

            Transform fp = obj.transform.Find("FOOTPRINT");
            if (fp == null) throw new System.InvalidOperationException("SC HOLLOWMERE townhouse " + variant + ": no FOOTPRINT child");
            var fpr = fp.GetComponent<MeshRenderer>(); if (fpr != null) fpr.enabled = false;
            Mesh m = fp.GetComponent<MeshFilter>().sharedMesh;
            var box = fp.gameObject.AddComponent<BoxCollider>();
            box.center = m.bounds.center; box.size = m.bounds.size;
            Vector3 world = Vector3.Scale(m.bounds.size, fp.lossyScale);
            float sx = Mathf.Abs(world.x), sz = Mathf.Abs(world.z), max = Foundation.MaxBuilding(f.Size);
            if (sx > max + 0.01f || sz > max + 0.01f)
                throw new System.InvalidOperationException("SC HOLLOWMERE townhouse " + variant + " FOOTPRINT " + sx.ToString("0.00") + " x " + sz.ToString("0.00") + " exceeds class " + f.Size + " (" + max + " m)");
            foreach (string s in new[] { "Frame", "Part_Door", "Part_Shutters", "SOCKET_Door", "SOCKET_Shutters" })
                if (obj.transform.Find(s) == null) throw new System.InvalidOperationException("SC HOLLOWMERE townhouse " + variant + ": missing " + s);
            Debug.Log("SC HOLLOWMERE townhouse lot=" + f.Id + " variant=" + variant + " footprint=" + sx.ToString("0.00") + "x" + sz.ToString("0.00") + " padY=" + f.Y.ToString("0.00"));
            return true;
        }
    }
}
