using UnityEngine;
using UnityEditor;

namespace SleepingCalamity.EditorTools
{
    // AAA palette pass (Reference/qa/visual/AAA_CONSISTENCY.md H1, H2). One place for the town's shared surfaces, so a greybox stand-in and the art beside it read as the same stuff.
    //  - TownSurface: the stone, timber, dark wood and green the greybox fallbacks (lot footings, doorsteps, ruin stubs, walls, clutter blocks) use. They are the kit's own
    //    M_KH_/M_CL_ materials (the same tinted sandstone / oak maps the cottages and walls use), falling back to the LookTest tileable only when a kit material is missing.
    //  - TownGround: the ground colour table. The flat greybox tints (M_HM_meadow/bed) and the HM_Surf_ decal tints both read it, so meadow, lot soil and the decals share hue families
    //    (soil ~30 deg, dry earth ~36 deg, grass ~72 deg). Values come from the previous approved tints; only moss (was hue 85) and the two dry-earth decals (hue 38 / 34) moved.
    static class TownSurface
    {
        const string Folder = "Assets/_Project/Content/Materials/Hollowmere";

        static Material Art(string slot, string lookTest)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/M_" + slot + ".mat");
            return m != null ? m : LookTestBuilder.Mat(lookTest);
        }

        public static Material Stone() => Art("KH_Rubble", "sandstone");
        public static Material Timber(bool weathered = false) => weathered ? Art("KH_OakWeathered", "oak_timber") : Art("KH_DoorOak", "oak_timber");
        public static Material Dark() => Art("KH_OakWeathered", "oak_timber");   // broken or burnt wood: the weathered oak, not a flat dark grey
        public static Material Green() => Art("CL_Paint", "plaster");             // painted green (flower-box foliage stand-in): the kit's sage paint
    }

    static class TownGround
    {
        public static readonly Color Soil = new Color(0.24f, 0.18f, 0.12f);     // turned soil / lot bed (hue ~30)
        public static readonly Color Meadow = new Color(0.36f, 0.42f, 0.20f);   // sunlit grass (hue ~72)
        public static readonly Color Water = new Color(0.07f, 0.16f, 0.18f);
        public static readonly Color Label = new Color(0.85f, 0.25f, 0.18f);
        public static readonly Color Dry = new Color(0.55f, 0.49f, 0.40f);      // dry bare earth, wear decal (hue ~36)
        public static readonly Color Worn = new Color(0.64f, 0.55f, 0.43f);     // trodden run (hue ~36, a step lighter than Dry)
        public static readonly Color Moss = new Color(0.17f, 0.21f, 0.10f);     // meadow's hue, ~half its value
        public static Color Damp => Soil * 0.09f;
        public static Color Stain => Soil * 0.165f;
        public static Color ContactAo => Soil * 0.0625f;
    }
}
