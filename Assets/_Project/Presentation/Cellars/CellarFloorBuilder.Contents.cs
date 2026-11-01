// Presentation/Cellars/CellarFloorBuilder.Contents.cs: M5 contents hooks of the greybox floor builder (partial of CellarFloorBuilder). Authority: none (presentation).
// The base builder already draws the chests ("Chest_x_y" / "KeyChest_x_y" boxes in the Markers group) and the "LockedGate" box in its door frame; this file gives
// CellarContentsSync stable ways to find them (handles are looked up by those names), builds the bakery-piece pickups at the CellarPieceSpots cells ("Piece_i" under a
// "Contents" group) and the gold gate-key cube a dead carrier leaves ("GateKeyPickup"). Every peer builds the same objects from the same layout; the host alone decides
// what is collected or opened. Colours are flat URP Lit fallbacks cached per colour.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static partial class CellarFloorBuilder
    {
        // Build() places every part by WORLD position (CellCenter), so it builds the root unparented at the world origin (world == root-local) and calls this last:
        // the floor then sits at parent.TransformPoint(CellCenter) wherever the parent is (the spine builds 200 m east of the hub), root.TransformPoint(CellCenter) is
        // where each cell really is, and Bounds / EntryCenter are moved to world space with it.
        static void ParentBuiltFloor(Transform root, Transform parent, CellarFloorBuild result)
        {
            root.SetParent(parent, false);
            if (parent == null) return;
            result.Bounds = new Bounds(root.TransformPoint(result.Bounds.center), result.Bounds.size);
            result.EntryCenter = root.TransformPoint(result.EntryCenter);
        }

        public const string ContentsGroup = "Contents", GateName = "LockedGate", GateKeyName = "GateKeyPickup";
        static readonly Dictionary<Color, Material> s_contentsMats = new Dictionary<Color, Material>();

        static Material ContentsMat(Color c)
        {
            if (s_contentsMats.TryGetValue(c, out Material m) && m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh) { name = "CellarContentsFlat" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c; if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            s_contentsMats[c] = m; return m;
        }

        public static string ChestName(FloorChest c) => (c.HoldsKey ? "KeyChest_" : "Chest_") + c.X + "_" + c.Y;

        // The built chest marker of chest `c`, or null.
        public static Transform FindChest(Transform floorRoot, FloorChest c) => FindNamed(floorRoot, ChestName(c));

        // The locked room's gate box, or null when the floor has none (or its door opening was not built).
        public static Transform FindGate(Transform floorRoot) => FindNamed(floorRoot, GateName);

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // One sphere per piece spot (index = piece index), parented under a "Contents" group of the floor root. Placed at floorRoot.TransformPoint(CellCenter) so they sit where the host checks them under any root.
        public static GameObject[] BuildPieces(Transform floorRoot, IReadOnlyList<CellPos> spots)
        {
            var result = new GameObject[spots.Count];
            if (floorRoot == null || spots.Count == 0) return result;
            Transform group = FindNamed(floorRoot, ContentsGroup) ?? Group(floorRoot, ContentsGroup);
            for (int i = 0; i < spots.Count; i++)
                result[i] = Prim(group, "Piece_" + i, PrimitiveType.Sphere, floorRoot.TransformPoint(CellCenter(spots[i].X, spots[i].Y) + new Vector3(0, 0.6f, 0)), new Vector3(0.5f, 0.5f, 0.5f), ContentsMat(new Color(0.98f, 0.86f, 0.45f)));
            return result;
        }

        // The gold cube the carrier leaves; `worldPos` is where it died (lifted a little). Parent is the sync object so it is not part of the floor build.
        public static GameObject BuildGateKey(Transform parent, Vector3 worldPos)
        {
            GameObject g = Prim(parent, GateKeyName, PrimitiveType.Cube, worldPos + new Vector3(0, 0.6f, 0), new Vector3(0.4f, 0.4f, 0.4f), ContentsMat(new Color(1.0f, 0.82f, 0.1f)));
            g.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            return g;
        }

        // Shows a chest as opened: lowered lid height and a duller colour (per renderer, the shared material is untouched).
        public static void SetChestOpened(Transform chest, bool opened)
        {
            if (chest == null) return;
            chest.localScale = opened ? new Vector3(1f, 0.55f, 1f) : Vector3.one;
            var r = chest.GetComponent<Renderer>();
            if (r == null) return;
            if (!opened) { r.SetPropertyBlock(null); return; }
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.2f, 0.17f, 0.14f)); block.SetColor("_Color", new Color(0.2f, 0.17f, 0.14f));
            r.SetPropertyBlock(block);
        }
    }
}
