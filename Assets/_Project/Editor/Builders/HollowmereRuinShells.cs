// Area 1 world T05: the ruin shell kit (Content/Art/Town/Ruins/Ruin_<S|M|L>_<A|B|C>/Ruin_<S|M|L>_<A|B|C>_<Mid|Tall>.fbx), taller burnt-out houses that replace the knee-high
// ruin on most lots so the town has a skyline. Editor time, authority: none. The height class of every lot is this table (STATUS "What T05 needs to know"); Core's lot data
// and RuinVariant stay as they are, the class only picks the file. Every lot not listed is Low (today's kit). Shell colliders: each child mesh named FOOTPRINT_COL_<n> becomes
// a BoxCollider fitted to its bounds on a renderer-less child (the camera boom and the hero cannot pass through the walls and stacks).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal enum RuinClass { Low, Mid, Tall }

    internal static class HollowmereRuinShells
    {
        // Ring 1, ring 2 inner, ring 2 outer, ring 3. Never two Tall side by side on one lane side; F01 low so the spawn sees the bakery lot;
        // R2_15 and R2_17 tall for the skyline from the lake.
        static readonly Dictionary<string, RuinClass> Table = new Dictionary<string, RuinClass>
        {
            { "F01", RuinClass.Low }, { "F02", RuinClass.Mid }, { "F03", RuinClass.Tall }, { "F04", RuinClass.Mid }, { "F05", RuinClass.Tall },
            { "F06", RuinClass.Tall }, { "F07", RuinClass.Tall }, { "F08", RuinClass.Mid }, { "F09", RuinClass.Tall }, { "F10", RuinClass.Mid },
            { "R2_01", RuinClass.Mid }, { "R2_02", RuinClass.Tall }, { "R2_03", RuinClass.Mid }, { "R2_04", RuinClass.Tall }, { "R2_05", RuinClass.Tall },
            { "R2_06", RuinClass.Mid }, { "R2_07", RuinClass.Low }, { "R2_08", RuinClass.Tall }, { "R2_09", RuinClass.Mid },
            { "R2_10", RuinClass.Low }, { "R2_11", RuinClass.Tall }, { "R2_12", RuinClass.Mid }, { "R2_13", RuinClass.Tall }, { "R2_14", RuinClass.Mid },
            { "R2_15", RuinClass.Tall }, { "R2_16", RuinClass.Low }, { "R2_17", RuinClass.Tall }, { "R2_18", RuinClass.Low }, { "R2_19", RuinClass.Tall },
            { "R2_20", RuinClass.Mid }, { "R2_21", RuinClass.Tall }, { "R2_22", RuinClass.Low }, { "R2_23", RuinClass.Mid },
            { "R3_01", RuinClass.Mid }, { "R3_02", RuinClass.Low }, { "R3_03", RuinClass.Mid },
        };

        // What was actually built per lot id (a missing file falls back to Low).
        static readonly Dictionary<string, RuinClass> built = new Dictionary<string, RuinClass>();
        public static int ShellCount, Colliders;

        public static void Reset() { built.Clear(); ShellCount = 0; Colliders = 0; Tallest = float.MinValue; TallestLot = ""; }

        public static RuinClass Planned(string lotId) => Table.TryGetValue(lotId, out RuinClass c) ? c : RuinClass.Low;
        public static RuinClass Built(string lotId) => built.TryGetValue(lotId, out RuinClass c) ? c : RuinClass.Low;
        public static void Record(string lotId, RuinClass c) { built[lotId] = c; if (c != RuinClass.Low) ShellCount++; }
        public static IEnumerable<KeyValuePair<string, RuinClass>> BuiltAll => built;

        // "Ruin_M_A" + Mid -> "Ruin_M_A_Mid"; Low keeps the base name.
        public static string FileName(string variant, RuinClass c) => c == RuinClass.Low ? variant : variant + "_" + c;

        // Turns every FOOTPRINT_COL_<n> child of the (already unpacked) shell into a BoxCollider fitted to its mesh bounds on a renderer-less child. Returns the count.
        public static int MakeColliders(Transform shell)
        {
            var cols = new List<Transform>();
            foreach (Transform t in shell.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("FOOTPRINT_COL_", StringComparison.Ordinal)) cols.Add(t);
            int n = 0;
            foreach (Transform t in cols)
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                var r = t.GetComponent<MeshRenderer>();
                if (r != null) UnityEngine.Object.DestroyImmediate(r);
                UnityEngine.Object.DestroyImmediate(mf);
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.center = b.center; box.size = b.size;
                n++;
            }
            Colliders += n;
            return n;
        }

        public static float Tallest = float.MinValue; public static string TallestLot = "";

        // Logs the shell's top above the pad and the renderers' and colliders' counts; tracks the tallest point in the scene (world y).
        public static void Measure(string lotId, RuinClass c, Transform shell, float padY)
        {
            bool any = false; Bounds b = default; int rend = 0;
            foreach (Renderer r in shell.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r.name.StartsWith("FOOTPRINT", StringComparison.Ordinal)) continue;
                rend++;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            int nb = shell.GetComponentsInChildren<BoxCollider>(true).Length;
            if (any && b.max.y > Tallest) { Tallest = b.max.y; TallestLot = lotId; }
            Debug.Log("SC HOLLOWMERE shell " + lotId + " " + c + " " + shell.name + " top " + (any ? (b.max.y - padY).ToString("F2") : "none") + " m above pad (world y " + (any ? b.max.y.ToString("F2") : "-") + ") renderers " + rend + " boxes " + nb);
        }

        // The shell's collision boxes, for the dressing (trees, debris and ivy keep clear of walls).
        public static List<BoxCollider> Boxes(Transform shell)
        {
            var list = new List<BoxCollider>();
            foreach (Transform t in shell.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("FOOTPRINT_COL_", StringComparison.Ordinal)) { var b = t.GetComponent<BoxCollider>(); if (b != null) list.Add(b); }
            return list;
        }
    }
}
