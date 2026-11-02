// Hollowmere's ring 2 lane kit in the scene (Q11): garden walls, gates, terrace and lot retaining walls, lane steps, kerbs, gutters and wall lanterns, placed per
// preset from Core/Town/LaneKitPlan (rows with position, yaw, X scale and tilt; the plan decides what goes where, this file only places it).
// Art: Content/Art/Town/Lanes/LaneKit/LaneKit.fbx, root LaneKit with one direct child mesh per piece, each at its own pivot at the origin (contract
// Reference/hollowmere/lanes/CONTRACT.md). A piece is cloned by its child name and stood at the row: Quaternion.Euler(0, Yaw, Tilt), scale (ScaleX, 1, 1).
// Without the FBX (or a missing child) the piece is a greybox box at the contract's extents (BoxOf), on the sandstone / oak / iron look-test materials.
// Materials: the LK_* slot table below mirrors the kit's SOURCES.md (shared B1 tileable, tint, normal scale) on the WearLit shader; LK_GlassLit is emissive. Unknown
// slots log a warning and take plain stone. Colliders: walls, piers and steps get one box (BoxOf); gutters, kerbs, grates, gate leaves and lanterns none.
// Rebuilt lanterns carry a small warm point light (low intensity, no shadows).
// Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereLanes
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Lanes/LaneKit";
        const string FbxName = "LaneKit.fbx", MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere", WearShaderName = "SleepingCalamity/WearLit";
        public const float LanternIntensity = 0.9f, LanternRange = 6f;

        // LK_* slots (LaneKit/SOURCES.md, slots table): shared B1 tileable, linear tint, normal scale. LK_Earth and LK_GlassLit are flat (see BuildMaterials).
        static readonly (string Slot, string Source, Color Tint, float Bump)[] LkSlots =
        {
            ("LK_Rubble", "sandstone", new Color(0.57f, 0.60f, 0.66f), 0.8f), ("LK_Dressed", "sandstone", new Color(1.00f, 0.95f, 0.86f), 0.8f),   // A6: flatter relief (were 1.5, 1.3)
            ("LK_Mortar", "sandstone", new Color(0.26f, 0.245f, 0.21f), 0.6f), ("LK_Oak", "oak_timber", new Color(0.80f, 0.76f, 0.72f), 1.2f),
            ("LK_GatePaint", "painted_wood", new Color(0.66f, 0.72f, 0.28f), 1.0f), ("LK_Iron", "iron", new Color(1f, 1f, 1f), 1.0f),
            ("LK_GutterStone", "sandstone", new Color(0.50f, 0.48f, 0.44f), 1.5f),   // gutter channel: LK_Mortar read as a near-black ribbon
        };

        // Local box (centre, size) of each piece at the contract extents: the greybox mesh, and the collider of walls, piers and steps.
        static (Vector3 C, Vector3 S) BoxOf(LanePiece p)
        {
            switch (p)
            {
                case LanePiece.GardenWall: return (new Vector3(0, 0.275f, 0), new Vector3(2, 2.15f, 0.5f));
                case LanePiece.GardenWallBroken: return (new Vector3(0, 0, 0), new Vector3(2, 1.6f, 0.5f));
                case LanePiece.GardenWallEnd: return (new Vector3(0, 0.375f, 0), new Vector3(0.6f, 2.35f, 0.6f));
                case LanePiece.RetainWall: return (new Vector3(0, 1f, 0), new Vector3(2, 4f, 0.7f));
                case LanePiece.RetainWallLow: return (new Vector3(0, 0.45f, 0), new Vector3(2, 2.1f, 0.7f));
                case LanePiece.RetainWallBreach: return (new Vector3(0, 0.5f, 0), new Vector3(2, 3f, 0.7f));
                case LanePiece.RetainButtress: return (new Vector3(0, 0.8f, -0.675f), new Vector3(1, 3.6f, 0.65f));
                case LanePiece.LaneStep: return (new Vector3(0, -0.32f, 0.275f), new Vector3(2, 0.96f, 0.55f));
                case LanePiece.LaneKerb: return (new Vector3(0, -0.09f, 0), new Vector3(2, 0.42f, 0.3f));
                case LanePiece.Gutter: case LanePiece.GutterChoked: return (new Vector3(0, -0.115f, 0), new Vector3(2, 0.27f, 0.4f));
                case LanePiece.GutterGrate: return (new Vector3(0, -0.09f, 0), new Vector3(0.6f, 0.22f, 0.6f));
                case LanePiece.WallLantern: case LanePiece.WallLanternBroken: return (new Vector3(0, 0f, -0.35f), new Vector3(0.25f, 0.6f, 0.5f));
                case LanePiece.GatePier: return (new Vector3(0, 0.65f, 0), new Vector3(0.6f, 2.9f, 0.6f));
                default: return (new Vector3(0.7f, 0.65f, 0), new Vector3(1.4f, 1.2f, 0.08f));   // GateLeaf, GateLeafBroken (hinge at the origin)
            }
        }

        static bool HasCollider(LanePiece p) => p == LanePiece.GardenWall || p == LanePiece.GardenWallBroken || p == LanePiece.GardenWallEnd || p == LanePiece.RetainWall
            || p == LanePiece.RetainWallLow || p == LanePiece.RetainWallBreach || p == LanePiece.RetainButtress || p == LanePiece.LaneStep || p == LanePiece.GatePier;

        static string GreyMat(LanePiece p)
        {
            switch (p)
            {
                case LanePiece.GateLeaf: case LanePiece.GateLeafBroken: return "oak_timber";
                case LanePiece.WallLantern: case LanePiece.WallLanternBroken: return "iron";
                case LanePiece.Gutter: case LanePiece.GutterChoked: case LanePiece.GutterGrate: return "cobble";
                default: return "sandstone";
            }
        }

        public static bool ArtPresent => AssetImporter.GetAtPath(Folder + "/" + FbxName) != null;

        // Places every row in both presets under a "Lanes" group of each preset root. `set` is unused (the kit lives in the preset roots) but kept for the
        // shared Build(set, ruined, rebuilt, t) call shape of the other placers.
        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            List<LaneKitRow> rows = LaneKitPlan.Plan(t);
            GameObject fbx = ArtPresent ? BeautyCornerArt.LoadModel(Folder, FbxName, keepMaterialSlots: true) : null;
            Dictionary<string, Material> slots = fbx != null ? BuildMaterials() : null;
            var tally = new List<string>();
            foreach (bool isRebuilt in new[] { false, true })
            {
                Transform group = ChildGroup(isRebuilt ? rebuilt : ruined, "Lanes");
                var sub = new Dictionary<string, Transform>();
                int placed = 0, art = 0;
                foreach (LaneKitRow r in rows)
                {
                    if (!r.In(isRebuilt)) continue;
                    string family = Family(r);
                    if (!sub.TryGetValue(family, out Transform parent)) sub[family] = parent = ChildGroup(group, family);
                    GameObject obj = TryClone(fbx, r.Piece, slots, parent);
                    if (obj != null) art++; else obj = Greybox(r.Piece, parent);
                    obj.name = r.Id;
                    bool isGutter = r.Piece == LanePiece.Gutter || r.Piece == LanePiece.GutterChoked || r.Piece == LanePiece.GutterGrate;
                    obj.transform.position = new Vector3(r.X, r.Y - (isGutter ? 0.02f : 0f), r.Z);
                    obj.transform.rotation = Quaternion.Euler(0f, r.Yaw, r.Tilt);
                    obj.transform.localScale = new Vector3(r.ScaleX, 1f, 1f);
                    if (HasCollider(r.Piece)) { var b = obj.AddComponent<BoxCollider>(); (Vector3 c, Vector3 s) = BoxOf(r.Piece); b.center = c; b.size = s; }
                    if (r.Piece == LanePiece.LaneStep) AddStepReadability(obj.transform);
                    if (isRebuilt && r.Piece == LanePiece.WallLantern) AddLanternLight(obj.transform);
                    placed++;
                }
                tally.Add((isRebuilt ? "Rebuilt " : "Ruined ") + placed + " (art " + art + ")");
            }
            Debug.Log("SC HOLLOWMERE lane kit placed: " + string.Join(", ", tally) + " rows=" + rows.Count + (fbx == null ? " [greybox]" : ""));
        }

        // A9: tread and riser shapes read in the stone pattern. Local frame of a LaneStep (BoxOf): the tread top at y 0.16 spans z 0..0.55 uphill, the riser is the
        // z = 0 face. A pale nosing along the tread's front edge and a dark shadow band hanging on the riser; both visual only (no collider, no shadow casting).
        static Material _nosing, _riserShade;
        static void AddStepReadability(Transform step)
        {
            if (_nosing == null)
            {
                Shader wear = Shader.Find(WearShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
                _nosing = BeautyCornerArt.Flat("LK_StepNosing", new Color(0.42f, 0.39f, 0.33f), 0.15f, wear);
                _riserShade = BeautyCornerArt.Flat("LK_RiserShade", new Color(0.018f, 0.016f, 0.014f), 0.05f, wear);
                foreach (Material m in new[] { _nosing, _riserShade }) { m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true); }
            }
            GameObject n = LookTestBuilder.Prim(step, "Nosing", PrimitiveType.Cube, new Vector3(0f, 0.158f, 0.045f), new Vector3(2f, 0.03f, 0.1f), _nosing);
            GameObject r = LookTestBuilder.Prim(step, "RiserShade", PrimitiveType.Cube, new Vector3(0f, 0.045f, -0.006f), new Vector3(2f, 0.2f, 0.012f), _riserShade);
            foreach (GameObject go in new[] { n, r }) go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static string Family(LaneKitRow r)
        {
            if (r.IsRetaining || r.Piece == LanePiece.RetainButtress) return "Retaining";
            switch (r.Piece)
            {
                case LanePiece.LaneStep: case LanePiece.LaneKerb: return "Steps";
                case LanePiece.Gutter: case LanePiece.GutterChoked: case LanePiece.GutterGrate: return "Gutters";
                case LanePiece.WallLantern: case LanePiece.WallLanternBroken: return "Lanterns";
                case LanePiece.GatePier: case LanePiece.GateLeaf: case LanePiece.GateLeafBroken: return "Gates";
                default: return "GardenWalls";
            }
        }

        static GameObject TryClone(GameObject fbx, LanePiece piece, Dictionary<string, Material> slots, Transform parent)
        {
            if (fbx == null) return null;
            Transform child = fbx.transform.Find(piece.ToString());
            if (child == null) { Debug.LogWarning("SC HOLLOWMERE lanes: LaneKit.fbx has no child '" + piece + "', greybox used"); return null; }
            GameObject obj = Object.Instantiate(child.gameObject, parent);
            bool gutter = piece == LanePiece.Gutter || piece == LanePiece.GutterChoked || piece == LanePiece.GutterGrate;
            ApplyMaterials(obj, gutter ? GutterSlots(slots) : slots);
            return obj;
        }

        static Dictionary<string, Material> GutterSlots(Dictionary<string, Material> slots)
        {
            var g = new Dictionary<string, Material>(slots);
            if (slots.TryGetValue("LK_GutterStone", out Material gs) && gs != null) g["LK_Mortar"] = gs;
            return g;
        }

        static GameObject Greybox(LanePiece piece, Transform parent)
        {
            (Vector3 c, Vector3 s) = BoxOf(piece);
            GameObject root = new GameObject(piece.ToString()); root.transform.SetParent(parent, false);
            Material m = LookTestBuilder.Mat(GreyMat(piece));
            GameObject box = LookTestBuilder.Prim(root.transform, "Box", PrimitiveType.Cube, c, s, m);
            if (piece == LanePiece.GateLeafBroken) box.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            if (piece == LanePiece.WallLantern || piece == LanePiece.WallLanternBroken)
            {
                Material glow = piece == LanePiece.WallLantern ? LookTestBuilder.Mat("LampGlow") : null;
                if (glow != null) LookTestBuilder.Prim(root.transform, "Glass", PrimitiveType.Cube, new Vector3(0, 0, -0.5f), new Vector3(0.18f, 0.4f, 0.18f), glow);
            }
            return root;
        }

        static void AddLanternLight(Transform lantern)
        {
            var go = new GameObject("WarmLight"); go.transform.SetParent(lantern, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.45f);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.42f); l.intensity = LanternIntensity; l.range = LanternRange; l.shadows = LightShadows.None;
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var map = new Dictionary<string, Material>();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            foreach (var s in LkSlots)
            {
                Material src = LookTestBuilder.Mat(s.Source);
                if (src == null) { Debug.LogWarning("SC HOLLOWMERE lanes: LK slot source '" + s.Source + "' missing for " + s.Slot); continue; }
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                m.SetColor("_BaseColor", new Color(Mathf.LinearToGammaSpace(s.Tint.r), Mathf.LinearToGammaSpace(s.Tint.g), Mathf.LinearToGammaSpace(s.Tint.b), 1f));
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
                m.SetFloat("_BumpScale", s.Bump);
                EditorUtility.SetDirty(m);
                map[s.Slot] = m;
            }
            map["LK_Earth"] = BeautyCornerArt.Flat("LK_Earth", new Color(0.034f, 0.025f, 0.017f), 0.05f, wear);
            Material glass = BeautyCornerArt.Flat("LK_GlassLit", new Color(0.9f, 0.55f, 0.2f), 0.7f, wear);   // warm lit glass: emissive linear (1, 0.62, 0.28) x 3
            glass.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.28f) * 3f); glass.EnableKeyword("_EMISSION");
            glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; EditorUtility.SetDirty(glass);
            map["LK_GlassLit"] = glass;
            AssetDatabase.SaveAssets();
            return map;
        }

        static void ApplyMaterials(GameObject obj, Dictionary<string, Material> slots)
        {
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k]) || mats[k] == null)
                    {
                        Debug.LogWarning("SC HOLLOWMERE lanes: no material for slot '" + slot + "' on " + r.name);
                        mats[k] = LookTestBuilder.Mat("sandstone");
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        static Transform ChildGroup(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }
    }
}
