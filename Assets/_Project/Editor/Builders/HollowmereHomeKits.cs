// Home kit families 2 (stone cottage, S) and 4 (workshop with lean-to, S/M), placed by HollowmereBuilder on the ring 2 lots HollowmereLayout.RebuiltBuilding marks
// StoneCottage and Workshop. Art: Assets/_Project/Content/Art/Town/Ring1/<Family>/<Variant>/<Variant>.fbx from Art/Blender/town/sc_cottage.py and sc_workshop.py
// (SOURCES.md in each family folder): root <Variant> with Frame, Part_Door, Part_Shutters, SOCKET_Door, SOCKET_Shutters and FOOTPRINT. The FBX front is local +Z
// (the workshop's front is its always-open bay), so the art turns 180 to face the lot's door side (local -Z). Built exactly like HollowmereTownhouse: the building
// stands on the lot's footing slab (top 0.25 m above the pad), every part is shown (the Rebuilt preset shows all buildings complete), FOOTPRINT gets a BoxCollider
// and no renderer, and the build fails when it does not fit the foundation's size class (spec section 10, footprint drift).
// Materials: the facade kit's slots (BeautyCornerArt.BuildFacadeMaterials, FK_*) plus the family's own SC_* / WS_* slots from each SOURCES.md slot table, built as
// HollowmereArt builds BK_*: a B1 tileable copied from its LookTest material, tinted with the generator's linear tint (tints above 1 keep their hue, scaled to
// fit), paint slots in WearLit's _SC_PAINT mode as BuildNeighbourMaterials. _Color mirrors _BaseColor and the MotionVectors pass is on, so every .mat has the same
// bytes with and without graphics.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class HomeKitBuild
    {
        public const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string WearShaderName = "SleepingCalamity/WearLit";
        const float PaintLum = 0.17f;   // the generators' PAINT_LUM
        public const float FootingTop = 0.25f;

        static Shader Wear => Shader.Find(WearShaderName) ?? throw new System.InvalidOperationException("Shader not found: " + WearShaderName);

        // A tinted B1 tileable slot. Material name M_<matName>. normalScale multiplies the source's bump scale; roughAdd lowers the smoothness by that much.
        public static Material Tinted(string matName, string source, Color tint, float normalScale, float roughAdd = 0f, float tiling = 1f)
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material src = LookTestBuilder.Mat(source) ?? throw new System.InvalidOperationException("LookTest material missing: " + source);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + matName + ".mat", Wear);
            m.CopyPropertiesFromMaterial(src);
            foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
            float peak = Mathf.Max(1f, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
            m.SetColor("_BaseColor", new Color(tint.r / peak, tint.g / peak, tint.b / peak, 1f).gamma);   // linear tint handed over as sRGB, as BuildFacadeMaterials
            m.SetColor("_Color", m.GetColor("_BaseColor"));
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", src.GetFloat("_BumpScale") * normalScale);
            if (roughAdd > 0f && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp01(src.GetFloat("_Smoothness") - roughAdd));
            if (tiling != 1f) foreach (string prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) m.SetTextureScale(prop, src.GetTextureScale(prop) * tiling);   // smaller stones
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        // A paint slot: the facade's painted_wood material (map["FK_Shutter"]) in _SC_PAINT mode with the paint colour (linear), as BuildNeighbourMaterials.
        public static Material Paint(Dictionary<string, Material> facade, string matName, Color paintLinear, float normalScale = 1f)
        {
            Material src = facade["FK_Shutter"];
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + matName + ".mat", Wear);
            m.CopyPropertiesFromMaterial(src);
            foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
            m.EnableKeyword("_SC_PAINT");
            m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
            m.SetVector("_SpecColor", new Vector4(paintLinear.r / PaintLum, paintLinear.g / PaintLum, paintLinear.b / PaintLum, 1f));   // raw linear, SetVector skips the colour-space conversion
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", src.GetFloat("_BumpScale") * normalScale);
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        // A flat colour slot (linear colour, roughness), as BeautyCornerArt.Flat and HollowmereArt's interior: _Color mirrors _BaseColor and the MotionVectors pass is on,
        // so the .mat has the same bytes with and without graphics.
        public static Material Flat(string matName, Color linear, float roughness)
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + matName + ".mat", Wear);
            m.SetColor("_BaseColor", linear.gamma); m.SetFloat("_Smoothness", 1f - roughness); m.SetFloat("_Metallic", 0f);
            m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f).linear;

        // Instantiates folder/variant/variant.fbx in lot frame g, assigns slots per renderer, adds the FOOTPRINT collider and checks the class. False when the art is missing.
        public static bool Place(Transform g, Foundation f, string folder, string variant, int turn, string label, System.Func<Dictionary<string, Material>> materials)
        {
            string dir = folder + "/" + variant;
            GameObject fbx = BeautyCornerArt.LoadModel(dir, variant + ".fbx", keepMaterialSlots: true);
            if (fbx == null) { Debug.LogWarning("SC HOLLOWMERE " + label + " art missing: " + dir); return false; }
            fbx = HollowmereClutter.ReimportIfDefaultLit(fbx, dir + "/" + variant + ".fbx", label + " " + variant);
            Dictionary<string, Material> slots = materials();

            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, g);
            obj.name = "Art_" + variant;
            obj.transform.localPosition = new Vector3(0f, FootingTop, 0f);
            obj.transform.localRotation = Quaternion.Euler(0f, 180f + 90f * turn, 0f);   // turn = quarter turns from the door side (HollowmereLayout.HomeAssignment.Turn)
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") continue;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE " + label + ": no material for slot '" + slot + "' on " + r.name); mats[k] = LookTestBuilder.Mat("plaster"); }
                }
                r.sharedMaterials = mats;
            }

            Transform fp = obj.transform.Find("FOOTPRINT");
            if (fp == null) throw new System.InvalidOperationException("SC HOLLOWMERE " + label + " " + variant + ": no FOOTPRINT child");
            var fpr = fp.GetComponent<MeshRenderer>(); if (fpr != null) fpr.enabled = false;
            Mesh m = fp.GetComponent<MeshFilter>().sharedMesh;
            // No FOOTPRINT box collider: it stood 2 m+ off the eaves and filled the doorway; HollowmereArt.AddWallColliders gives the Frame its own mesh colliders.
            Vector3 world = Vector3.Scale(m.bounds.size, fp.lossyScale);
            float sx = Mathf.Abs(world.x), sz = Mathf.Abs(world.z), max = Foundation.MaxBuilding(f.Size);
            if (sx > max + 0.01f || sz > max + 0.01f)
                throw new System.InvalidOperationException("SC HOLLOWMERE " + label + " " + variant + " FOOTPRINT " + sx.ToString("0.00") + " x " + sz.ToString("0.00") + " exceeds class " + f.Size + " (" + max + " m)");
            foreach (string s in new[] { "Frame", "Part_Door", "Part_Shutters", "SOCKET_Door", "SOCKET_Shutters" })
                if (obj.transform.Find(s) == null) throw new System.InvalidOperationException("SC HOLLOWMERE " + label + " " + variant + ": missing " + s);
            HollowmereArt.AddWallColliders(obj);
            Debug.Log("SC HOLLOWMERE " + label + " lot=" + f.Id + " variant=" + variant + " class=" + f.Size + " footprint=" + sx.ToString("0.00") + "x" + sz.ToString("0.00") + " padY=" + f.Y.ToString("0.00"));
            return true;
        }
    }

    // Kit family 2, the stone cottage (S class, 6 m lot, body inside 5 m).
    public static class HollowmereStoneCottage
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Ring1/StoneCottage";

        // The variant and turn per lot come from HollowmereLayout.RebuiltHome (the seeded ring 2 fill). Per variant: SC_Rubble tint, SC_Thatch tint (B and C only), door and shutter paint (linear), as SOURCES.md.
        static readonly Dictionary<string, (Color Rubble, Color Thatch, Color Paint)> Looks = new Dictionary<string, (Color, Color, Color)>
        {
            { "StoneCottage_A", (new Color(0.56f, 0.64f, 0.80f), Color.white, new Color(0.0482f, 0.0931f, 0.162f)) },     // slate blue, red tile roof (no thatch)
            { "StoneCottage_B", (new Color(0.60f, 0.65f, 0.74f), new Color(2.55f, 2.35f, 1.20f), new Color(0.0685f, 0.1441f, 0.0648f)) },   // sage green
            { "StoneCottage_C", (new Color(0.58f, 0.63f, 0.78f), new Color(2.05f, 1.92f, 1.30f), new Color(0.095f, 0.040f, 0.032f)) },   // muted oxblood
        };

        public static bool Place(Transform g, Foundation f)
        {
            HollowmereLayout.HomeAssignment a = HollowmereLayout.RebuiltHome(f.Id);
            string variant = a.VariantName;
            return HomeKitBuild.Place(g, f, Folder, variant, a.Turn, "stonecottage", () => Materials(variant));
        }

        static Dictionary<string, Material> Materials(string variant)
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            var look = Looks[variant];
            map["SC_Rubble"] = HomeKitBuild.Tinted(variant + "_SC_Rubble", "sandstone", look.Rubble, 0.5f, 0f, 1.7f);   // A6: flatter relief (was 1.4) and smaller stones (1.7x the texel scale): the rubble read as bread rolls
            if (variant != "StoneCottage_A") map["SC_Thatch"] = HomeKitBuild.Tinted(variant + "_SC_Thatch", "oak_timber", look.Thatch, 1.1f, 0.2f);
            map["FK_Shutter"] = HomeKitBuild.Paint(map, variant + "_FK_Shutter", look.Paint);
            map["FK_DoorPaint"] = HomeKitBuild.Paint(map, variant + "_FK_DoorPaint", look.Paint);
            // The window pane bands (SOURCES.md): pale sky glare on top, a darker reflection step, a half-drawn curtain; the dark FK_Glass fills the rest.
            map["SC_GlassHi"] = HomeKitBuild.Flat("SC_GlassHi", new Color(0.34f, 0.40f, 0.46f), 0.05f);
            map["SC_GlassSky"] = HomeKitBuild.Flat("SC_GlassSky", new Color(0.13f, 0.17f, 0.21f), 0.07f);
            map["SC_Curtain"] = HomeKitBuild.Flat("SC_Curtain", new Color(0.52f, 0.47f, 0.37f), 0.9f);
            AssetDatabase.SaveAssets();
            return map;
        }
    }

    // Kit family 4, the workshop with lean-to (A is S class, B and C are M class). The front is the always-open bay; Part_Door is a side-wall man door.
    public static class HollowmereWorkshop
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Ring1/Workshop";

        // Linear paint (SOURCES.md): A sage, B oxblood, C slate blue.
        static readonly Dictionary<string, Color> PaintLinear = new Dictionary<string, Color>
        {
            { "Workshop_A", new Color(0.0685f, 0.1441f, 0.0648f) }, { "Workshop_B", new Color(0.095f, 0.040f, 0.032f) }, { "Workshop_C", new Color(0.0482f, 0.0931f, 0.1620f) },
        };

        // Class per variant: only A fits an S lot.
        public static FoundationSize ClassOf(string variant) => variant == "Workshop_A" ? FoundationSize.S : FoundationSize.M;

        public static bool Place(Transform g, Foundation f)
        {
            HollowmereLayout.HomeAssignment a = HollowmereLayout.RebuiltHome(f.Id);
            string variant = a.VariantName;
            return HomeKitBuild.Place(g, f, Folder, variant, a.Turn, "workshop", () => Materials(variant));
        }

        static Dictionary<string, Material> Materials(string variant)
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            map["FK_Oak"] = HomeKitBuild.Tinted(variant + "_FK_Oak", "oak_timber", new Color(0.48f, 0.45f, 0.42f), 1.2f);   // darker than the facade's so the frame reads against the boards
            Color paint = PaintLinear[variant];
            map["FK_Shutter"] = HomeKitBuild.Paint(map, variant + "_FK_Shutter", paint);
            map["FK_DoorPaint"] = HomeKitBuild.Paint(map, variant + "_FK_DoorPaint", paint);
            if (variant == "Workshop_B") map["WS_BoardPaint"] = HomeKitBuild.Paint(map, variant + "_WS_BoardPaint", HomeKitBuild.Hex(0x9c, 0x7f, 0x4c), 0.9f);
            else map["WS_Board"] = HomeKitBuild.Tinted(variant + "_WS_Board", "oak_timber", variant == "Workshop_A" ? new Color(3.2f, 2.9f, 2.5f) : new Color(1.75f, 1.5f, 1.28f), 1.1f);
            if (variant != "Workshop_A") map["WS_RoofBoard"] = HomeKitBuild.Tinted(variant + "_WS_RoofBoard", "oak_timber", variant == "Workshop_B" ? new Color(1.05f, 0.95f, 0.85f) : new Color(0.85f, 0.78f, 0.70f), 1.3f);
            map["WS_Floor"] = HomeKitBuild.Tinted("WS_Floor", "sandstone", new Color(0.52f, 0.48f, 0.43f), 1f);
            map["WS_Inner"] = HomeKitBuild.Tinted("WS_Inner", "oak_timber", new Color(0.78f, 0.70f, 0.63f), 1f);
            AssetDatabase.SaveAssets();
            return map;
        }
    }
}
