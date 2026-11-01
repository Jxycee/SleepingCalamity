// Final art for Hollowmere's buildings (docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 5.2), placed by HollowmereBuilder
// when the export is in, else the builder keeps its greybox: the bakery (ring 1), the smithy and schoolhouse (kit-ready shops on ring-2 lots) and the mill
// landmark. One FBX per building: Frame, Part_* meshes (pivot at their socket), SOCKET_* empties under Frame and a flat FOOTPRINT box; a landmark adds a
// Shell (its ruined state). The Rebuilt preset shows every part placed (P28: a building counts once all its parts are on); the mill shows Shell when ruined.
// Materials: the facade kit's shared-tileable WearLit slots (BeautyCornerArt.BuildFacadeMaterials), the bakery's BK_* set (shared: the smithy's fire is
// BK_Ember) and each building's own slots, built the same way (a B1/B2 tileable tinted as the generator's tile_mat tint, or a flat colour).
// A lot building's FOOTPRINT is checked against the lot's size class and fails the build when it slips past it (design 10, footprint drift).
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

        // One building export: folder, FBX, required parts, and its own slots beyond the facade and bakery sets.
        public sealed class Spec
        {
            public string Name, Folder, Fbx; public string[] Parts; public float Yaw = 180f;   // Yaw: the export's front is local +Z for the lot buildings (turn 180); the keep hall is exported in Unity axes (0)
            public (string Slot, string Source, Color Tint)[] Tinted = new (string, string, Color)[0];
            public (string Slot, Color Linear, float Smooth)[] Flat = new (string, Color, float)[0];
        }

        public static readonly Spec Bakery = new Spec { Name = "Bakery", Folder = BakeryFolder, Fbx = "Bakery.fbx", Parts = BakeryParts };
        public static readonly Spec Smithy = new Spec { Name = "Smithy", Folder = "Assets/_Project/Content/Art/Town/Ring2/Smithy", Fbx = "Smithy.fbx",
            Parts = new[] { "Part_Forge", "Part_Anvil", "Part_Sign" },
            Tinted = new[] { ("BK_Leather", "canvas", new Color(0.50f, 0.30f, 0.18f)), ("BK_Coal", "soot_coal", new Color(0.55f, 0.55f, 0.58f)),
                             ("BK_Ash", "soot_coal", new Color(2.6f, 2.5f, 2.4f)), ("BK_Grindstone", "sandstone", new Color(0.85f, 0.85f, 0.88f)) },
            Flat = new[] { ("BK_Water", new Color(0.025f, 0.04f, 0.03f), 0.94f) } };   // the quench trough: dark, glossy
        public static readonly Spec Schoolhouse = new Spec { Name = "Schoolhouse", Folder = "Assets/_Project/Content/Art/Town/Ring2/Schoolhouse", Fbx = "Schoolhouse.fbx",
            Parts = new[] { "Part_Door", "Part_Bell", "Part_Sign" },
            Tinted = new[] { ("SH_Limewash", "plaster", new Color(0.95f, 0.93f, 0.87f)), ("SH_Slate", "soot_coal", new Color(0.85f, 0.9f, 0.95f)),
                             ("SH_Chalk", "plaster", new Color(1.45f, 1.45f, 1.42f)), ("SH_Rope", "canvas", new Color(1.0f, 0.85f, 0.62f)) },
            // the bell: a flat warm bronze like the bell tower's BT_Bronze (the iron scan tinted stays too dark at distance)
            Flat = new[] { ("SH_Bronze", new Color(0.55f, 0.36f, 0.16f), 0.45f), ("SH_Interior", new Color(0.035f, 0.025f, 0.018f), 0.1f) } };
        public static readonly Spec Mill = new Spec { Name = "Mill", Folder = "Assets/_Project/Content/Art/Town/Ring3/Mill", Fbx = "Mill.fbx",
            Parts = new[] { "Part_Wheel", "Part_Door" },
            Tinted = new[] { ("ML_Rubble", "sandstone", new Color(0.80f, 0.77f, 0.72f)), ("ML_OakWeathered", "oak_timber", new Color(0.62f, 0.60f, 0.58f)),
                             ("ML_WheelOak", "oak_timber", new Color(0.72f, 0.64f, 0.54f)), ("ML_Earth", "plaster", new Color(0.42f, 0.32f, 0.22f)),
                             ("ML_Sack", "canvas", new Color(1.0f, 0.94f, 0.82f)), ("ML_Millstone", "sandstone", new Color(1.1f, 1.08f, 1.0f)) },
            Flat = new[] { ("ML_Interior", new Color(0.035f, 0.025f, 0.018f), 0.1f) } };

        // The keep hall (Q9, sc_keep_hall.py): square side Unity -Z and the west gable door already in Unity axes, so yaw 0.
        public static readonly Spec KeepHall = new Spec { Name = "KeepHall", Folder = "Assets/_Project/Content/Art/Town/Keep/KeepHall", Fbx = "KeepHall.fbx", Yaw = 0f,
            Parts = new string[0],   // Part_Door sits under Frame; checked in TryPlaceKeepHall
            Tinted = new[] { ("KH_Rubble", "sandstone", new Color(0.62f, 0.60f, 0.57f)), ("KH_Slate", "sandstone", new Color(0.30f, 0.36f, 0.47f)),
                             ("KH_OakWeathered", "oak_timber", new Color(0.62f, 0.60f, 0.58f)), ("KH_DoorOak", "oak_timber", new Color(1.25f, 1.05f, 0.85f)),
                             ("KH_Limewash", "plaster", new Color(0.92f, 0.88f, 0.80f)), ("KH_Flag", "sandstone", new Color(0.56f, 0.54f, 0.51f)) },
            Flat = new[] { ("KH_Bronze", new Color(0.55f, 0.36f, 0.16f), 0.45f) } };

        // The keep hall in its row frame g (centre on the terrace): Shell when ruined, Frame (with Part_Door under it) when rebuilt. False when the export is missing.
        public static bool TryPlaceKeepHall(Transform g, bool rebuilt)
        {
            GameObject obj = Instantiate(KeepHall, g, 0f);
            if (obj == null) return false;
            Transform shell = obj.transform.Find("Shell") ?? throw new InvalidOperationException("KeepHall.fbx has no Shell");
            Transform frame = obj.transform.Find("Frame") ?? throw new InvalidOperationException("KeepHall.fbx has no Frame");
            shell.gameObject.SetActive(!rebuilt); frame.gameObject.SetActive(rebuilt);
            Transform door = null;
            foreach (Transform c in obj.GetComponentsInChildren<Transform>(true)) if (c.name == "Part_Door") door = c;
            if (door == null) throw new InvalidOperationException("KeepHall.fbx has no Part_Door");
            if (!door.IsChildOf(frame)) door.gameObject.SetActive(rebuilt);
            Vector2 size = FootprintSize(KeepHall, obj, g);
            Debug.Log("SC HOLLOWMERE keep hall " + (rebuilt ? "restored" : "shell") + " footprint=" + size.x.ToString("0.00") + "x" + size.y.ToString("0.00"));
            return true;
        }

        public static bool TryPlaceBakery(Transform g, Foundation f, float footingTop) => TryPlaceBuilding(Bakery, g, f, footingTop);

        // Places a lot building in the lot frame g (origin at the lot centre on its pad, local -Z = the lot's door side). False when the export is missing.
        public static bool TryPlaceBuilding(Spec spec, Transform g, Foundation f, float footingTop)
        {
            GameObject obj = Instantiate(spec, g, footingTop);
            if (obj == null) return false;
            Vector2 size = FootprintSize(spec, obj, g);
            float max = Foundation.MaxBuilding(f.Size);
            if (size.x > max + 1e-3f || size.y > max + 1e-3f)
                throw new InvalidOperationException(spec.Name + " FOOTPRINT " + size.x.ToString("0.00") + " x " + size.y.ToString("0.00") + " m is larger than the " + f.Size + " class allows (" + max + " m)");
            Debug.Log("SC HOLLOWMERE " + spec.Name.ToLowerInvariant() + " on " + f.Id + " footprint=" + size.x.ToString("0.00") + "x" + size.y.ToString("0.00") + " class=" + f.Size + " parts=" + spec.Parts.Length);
            return true;
        }

        // The mill landmark in its row frame g: Shell when ruined, Frame and its parts when rebuilt. False when the export is missing.
        public static bool TryPlaceMill(Transform g, bool rebuilt)
        {
            GameObject obj = Instantiate(Mill, g, 0f);
            if (obj == null) return false;
            Transform shell = obj.transform.Find("Shell") ?? throw new InvalidOperationException("Mill.fbx has no Shell");
            shell.gameObject.SetActive(!rebuilt);
            foreach (Transform c in obj.transform)
                if (c.name == "Frame" || c.name.StartsWith("Part_", StringComparison.Ordinal)) c.gameObject.SetActive(rebuilt);
            Vector2 size = FootprintSize(Mill, obj, g);
            Debug.Log("SC HOLLOWMERE mill " + (rebuilt ? "restored" : "shell") + " footprint=" + size.x.ToString("0.00") + "x" + size.y.ToString("0.00"));
            return true;
        }

        static GameObject Instantiate(Spec spec, Transform g, float y)
        {
            GameObject fbx = BeautyCornerArt.LoadModel(spec.Folder, spec.Fbx, keepMaterialSlots: true);
            if (fbx == null) return null;
            Dictionary<string, Material> slots = Materials(spec);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, g);
            obj.name = "Art_" + spec.Name;
            obj.transform.localPosition = new Vector3(0f, y, 0f);
            obj.transform.localRotation = Quaternion.Euler(0f, spec.Yaw, 0f);   // the export's door side is local +Z (like the facade): turn it to the frame's -Z
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") { r.enabled = false; continue; }
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) throw new InvalidOperationException(spec.Name + ": no material for slot '" + slot + "' on " + r.name);
                }
                r.sharedMaterials = mats;
            }
            foreach (string p in spec.Parts)
                if (obj.transform.Find(p) == null) throw new InvalidOperationException(spec.Fbx + " has no " + p);
            return obj;
        }

        // The FOOTPRINT's x/z extent in frame g (rotation and any FBX unit scale included): its eight bounds corners through the hierarchy.
        static Vector2 FootprintSize(Spec spec, GameObject obj, Transform g)
        {
            Transform foot = null;
            foreach (Transform t in obj.GetComponentsInChildren<Transform>(true)) if (t.name == "FOOTPRINT") foot = t;
            MeshFilter fm = foot != null ? foot.GetComponent<MeshFilter>() : null;
            if (fm == null || fm.sharedMesh == null) throw new InvalidOperationException(spec.Fbx + " has no FOOTPRINT mesh");
            Bounds b = fm.sharedMesh.bounds;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = g.InverseTransformPoint(foot.TransformPoint(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z)));
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            return new Vector2(x1 - x0, z1 - z0);
        }

        static Dictionary<string, Material> Materials(Spec spec)
        {
            Dictionary<string, Material> map = BakeryMaterials();
            Shader wear = Shader.Find(WearShaderName);
            foreach (var t in spec.Tinted) map[t.Slot] = Tinted(t.Slot, t.Source, t.Tint, wear);
            foreach (var fl in spec.Flat) map[fl.Slot] = Flat(fl.Slot, fl.Linear, fl.Smooth, wear);
            AssetDatabase.SaveAssets();
            return map;
        }

        internal static Material Tinted(string slot, string source, Color tint, Shader wear)
        {
            Material src = LookTestBuilder.Mat(source) ?? throw new InvalidOperationException("LookTest material missing: " + source);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + slot + ".mat", wear);
            m.CopyPropertiesFromMaterial(src);
            foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
            float peak = Mathf.Max(1f, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
            m.SetColor("_BaseColor", new Color(tint.r / peak, tint.g / peak, tint.b / peak, 1f).gamma);   // linear tint handed over as sRGB, hue kept above 1
            m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);   // same bytes with and without graphics
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static Material Flat(string slot, Color linear, float smooth, Shader wear)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + slot + ".mat", wear);
            m.SetColor("_BaseColor", linear.gamma); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Dictionary<string, Material> BakeryMaterials()
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? throw new InvalidOperationException("Shader not found: " + WearShaderName);
            foreach (var s in BakerySlots) map[s.Slot] = Tinted(s.Slot, s.Source, s.Tint, wear);
            map["BK_Interior"] = Flat("BK_Interior", new Color(0.035f, 0.025f, 0.018f), 0.1f, wear);
            // The lit oven mouth: URP Lit with an HDR orange emission (the generator's strength 6 read as about x3 here, the corner's exposure is lower).
            Material ember = LookTestBuilder.Ensure(MaterialsFolder + "/M_BK_Ember.mat", Shader.Find("Universal Render Pipeline/Lit"));
            ember.SetColor("_BaseColor", new Color(0.35f, 0.08f, 0.01f).gamma); ember.SetFloat("_Smoothness", 0.2f);
            ember.SetColor("_EmissionColor", new Color(1.0f, 0.36f, 0.06f) * 3f); ember.EnableKeyword("_EMISSION");
            ember.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            ember.SetColor("_Color", ember.GetColor("_BaseColor"));
            EditorUtility.SetDirty(ember); map["BK_Ember"] = ember;
            AssetDatabase.SaveAssets();
            return map;
        }
    }
}
