// M5 dive spine: world-space helpers and the greybox for the dive's interactables, kept apart from CellarFloorBuilder.cs / .Art.cs / (the contents session's) .Contents.cs.
// Not networked. Every peer calls AddDiveMarkers on its own locally built floor (DiveDirector does it when the floor sync finishes a build), so the markers are identical
// by construction and die with the floor's root when it is rebuilt. The markers are tall unlit-looking light columns over the stair exit, the bank cell and (floor 4) the
// way home; the interaction itself is DiveDirector (host-validated range check), these only show where to stand.
using UnityEngine;
using SleepingCalamity.Core;

namespace SleepingCalamity.Presentation
{
    public static partial class CellarFloorBuilder
    {
        public const string DiveMarkerGroup = "DiveMarkers";
        public const float HomeMarkerOffset = 1.8f;   // the floor-4 way-home column stands this far east of the bank cell centre; the interact range covers both

        // World position of a cell centre on a built floor (the floor root sits under the sync object, which may be anywhere).
        public static Vector3 WorldCell(CellarFloorBuild built, CellPos cell) => built.Root.TransformPoint(CellCenter(cell.X, cell.Y));

        public static bool TryStairWorld(CellarFloorLayout layout, CellarFloorBuild built, out Vector3 pos)
        {
            pos = default;
            if (layout == null || built == null || built.Root == null || !layout.StairExit.HasValue) return false;
            pos = WorldCell(built, layout.StairExit.Value); return true;
        }

        public static bool TryBankWorld(CellarFloorLayout layout, CellarFloorBuild built, out Vector3 pos)
        {
            pos = default;
            if (layout == null || built == null || built.Root == null || !layout.BankCell.HasValue) return false;
            pos = WorldCell(built, layout.BankCell.Value); return true;
        }

        // Floor 7's stand-in ending door (EndCell): the dive finishes there (plan 3 task 7).
        public static bool TryEndWorld(CellarFloorLayout layout, CellarFloorBuild built, out Vector3 pos)
        {
            pos = default;
            if (layout == null || built == null || built.Root == null || !layout.EndCell.HasValue) return false;
            pos = WorldCell(built, layout.EndCell.Value); return true;
        }

        // Where the way down stands on this floor, if it has one: the stair on floors 1-4 and 6; on the bank floor the bank room cell (the stair opens there once the party pushed on).
        public static bool TryDescentWorld(CellarFloorLayout layout, CellarFloorBuild built, out Vector3 pos)
        {
            pos = default;
            if (layout == null || !DiveFloorTable.HasStair(layout.Floor, true)) return false;
            return layout.Floor == UndercroftPlan.BankFloor ? TryBankWorld(layout, built, out pos) : TryStairWorld(layout, built, out pos);
        }

        // Where player `slot` (0..3) stands on arrival: the layout's entry start cells in order (wrapping), a hand's width apart so four never share a spot.
        public static Vector3 EntryWorld(CellarFloorLayout layout, CellarFloorBuild built, int slot)
        {
            int n = layout.EntryStarts.Count;
            CellPos s = layout.EntryStarts[(slot < 0 ? 0 : slot) % n];
            int lap = (slot < 0 ? 0 : slot) / n;
            Vector3 p = WorldCell(built, s) + new Vector3(0.8f * lap, 0.1f, 0.8f * lap);
            return p;
        }

        // Idempotent: a floor that already has the group is left alone.
        public static void AddDiveMarkers(CellarFloorLayout layout, CellarFloorBuild built)
        {
            if (layout == null || built == null || built.Root == null || built.Root.Find(DiveMarkerGroup) != null) return;
            Transform group = new GameObject(DiveMarkerGroup).transform; group.SetParent(built.Root, false);
            if (layout.Floor != UndercroftPlan.BankFloor && DiveFloorTable.HasStair(layout.Floor, true) && TryStairWorld(layout, built, out Vector3 stair))
                Column(group, "StairColumn", stair, new Color(0.62f, 0.38f, 0.90f), 3.2f);
            if (TryBankWorld(layout, built, out Vector3 bank))
            {
                Column(group, "BankColumn", bank, new Color(1.0f, 0.82f, 0.30f), 2.6f);
                if (layout.Floor == UndercroftPlan.BankFloor) Column(group, "HomeColumn", bank + new Vector3(HomeMarkerOffset, 0f, 0f), new Color(0.35f, 0.90f, 0.50f), 3.0f);   // the way home stands beside the bank room's lamp
            }
            if (TryEndWorld(layout, built, out Vector3 end)) Column(group, "EndColumn", end, new Color(0.35f, 0.90f, 0.50f), 3.2f);   // floor 7: the stand-in ending door
        }

        static void Column(Transform parent, string name, Vector3 world, Color color, float height)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name; g.transform.SetParent(parent, false);
            g.transform.position = world + new Vector3(0f, height * 0.5f, 0f); g.transform.localScale = new Vector3(0.22f, height * 0.5f, 0.22f);
            Collider col = g.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = "DiveMarker", color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
    }
}
