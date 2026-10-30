// The Hollowmere beauty corner placement table (M9, docs/art/beauty-corner-brief.md section 1). ONE place to edit positions: every piece of the brief's
// layout table is a Row here. Unity metres, +Z north, origin = fountain centre.
//
// Asset conventions (the art sessions export to these; change the row, not the art, when a pivot differs):
//   - Pivot at ground level in the middle of the piece's footprint. The facade pivot is the middle of its front face, front facing -Z (local +Z runs into the building).
//     The stall pivot is the middle of its footprint, its front (the counter side) facing local +Z. Cobbles and the ring pivot at the middle of the patch / ring.
//   - Art lives in Assets/_Project/Content/Art/Town/<Batch>/<Asset>/ : one FBX plus <name>_BaseColor.png, _Normal.png, _ORM.png (R occlusion, G roughness, B metallic).
//     The batch folder matches by its leading code ("B6", "B6_plaza"), the asset folder by Names (case/underscore-insensitive, exact, then contains).
//   - A row with no art in its folder gets a labelled greybox (PLACEHOLDER_*) built from Placeholder parts at the right size.
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class BeautyCornerTable
    {
        // The brief's layout-table row names (the EditMode test checks every one has at least one Row).
        public const string Cobbles = "Plaza cobbles", Ring = "Fountain flagstone ring", Fountain = "Dry fountain", Banner = "Banner pole + bleached banner",
            Facade = "Facade (2 storeys + roof)", Stall = "Market stall", Props = "Props cluster", Foliage = "Foliage", Lanterns = "Lanterns";

        public const string Neighbours = "Neighbours (greybox massing, not a brief row)";

        public static readonly string[] BriefRows = { Cobbles, Ring, Fountain, Banner, Facade, Stall, Props, Foliage, Lanterns };

        public enum PartShape { Box, Cylinder }

        // One piece of a greybox. Size: Box = metres; Cylinder = (diameter, height, diameter). Pos is the part's centre in the instance's local space.
        // Mat: a LookTest material name (plaster, cobble, ...) or one of the flat tints green / dark / cloth / warm.
        public readonly struct Part
        {
            public readonly PartShape Shape; public readonly Vector3 Pos, Size, Euler; public readonly string Mat;
            public Part(PartShape shape, Vector3 pos, Vector3 size, string mat, Vector3 euler = default) { Shape = shape; Pos = pos; Size = size; Mat = mat; Euler = euler; }
        }

        public readonly struct Instance
        {
            public readonly Vector3 Pos; public readonly float Yaw, Scale;
            public Instance(float x, float y, float z, float yaw = 0f, float scale = 1f) { Pos = new Vector3(x, y, z); Yaw = yaw; Scale = scale; }
        }

        public sealed class Row
        {
            public string Brief, Id, Batch; public string[] Names; public Instance[] Instances; public Part[] Placeholder; public bool Foliage;
        }

        static Part Box(float x, float y, float z, float w, float h, float d, string mat, float rx = 0f, float ry = 0f, float rz = 0f) =>
            new Part(PartShape.Box, new Vector3(x, y, z), new Vector3(w, h, d), mat, new Vector3(rx, ry, rz));
        static Part Cyl(float x, float y, float z, float dia, float h, string mat) =>
            new Part(PartShape.Cylinder, new Vector3(x, y, z), new Vector3(dia, h, dia), mat);
        static Instance At(float x, float y, float z, float yaw = 0f, float scale = 1f) => new Instance(x, y, z, yaw, scale);

        public static readonly Row[] Rows =
        {
            new Row { Brief = Cobbles, Id = "PlazaCobbles", Batch = "B6", Names = new[] { "plaza_cobbles", "cobbles", "cobble_plaza", "plaza" },
                Instances = new[] { At(0f, 0f, 1f) },     // x -8..8, z -6..8 (16 x 14 m); the 5 cm dish is the art's job
                Placeholder = new[] { Box(0f, -0.05f, 0f, 16f, 0.1f, 14f, "cobble") } },
            new Row { Brief = Ring, Id = "FountainRing", Batch = "B6", Names = new[] { "fountain_ring", "flagstone_ring", "ring", "fountain_flagstone" },
                Instances = new[] { At(0f, 0f, 0f) },     // 1.2 m wide ring around the 3.4 m basin: outer diameter 5.8 m
                Placeholder = new[] { Cyl(0f, 0.03f, 0f, 5.8f, 0.06f, "sandstone") } },
            new Row { Brief = Fountain, Id = "DryFountain", Batch = "B10", Names = new[] { "dry_fountain", "fountain" },
                Instances = new[] { At(0f, 0f, 0f) },     // basin outer diameter 3.4 m, rim 0.55 m, spout column to about 2.4 m
                Placeholder = new[] { Cyl(0f, 0.275f, 0f, 3.4f, 0.55f, "sandstone"), Cyl(0f, 0.9f, 0f, 1.1f, 0.7f, "sandstone"), Cyl(0f, 1.6f, 0f, 0.38f, 1.6f, "sandstone"), Cyl(0f, 2.35f, 0f, 0.6f, 0.2f, "sandstone") } },
            new Row { Brief = Banner, Id = "BannerPole", Batch = "B10", Names = new[] { "banner_pole", "banner", "bleached_banner", "banner_pole_banner" },
                Instances = new[] { At(-2.6f, 0f, 1.8f) }, // pole about 4.5 m, banner 0.7 x 2.2 m (moved from the brief's 2.4, 1.2 by the controller, round 2: frames the left of Wide, clears Mid)
                Placeholder = new[] { Cyl(0f, 2.25f, 0f, 0.12f, 4.5f, "dark"), Box(0.4f, 3.15f, 0f, 0.7f, 2.2f, 0.03f, "cloth") } },
            new Row { Brief = Facade, Id = "Facade", Batch = "B4", Names = new[] { "facade", "half_timbered_facade", "timber_facade", "house" },
                Instances = new[] { At(-1.25f, 0f, 7.5f) },   // front 7.5 m wide on z 7.5, x -5..2.5, facing -Z; pivot = front face centre at ground
                Placeholder = new[]
                {
                    Box(0f, 0.3f, 2.95f, 7.7f, 0.6f, 6.1f, "sandstone"),                 // plinth 0.6 m
                    Box(0f, 1.8f, 3f, 7.5f, 2.4f, 6f, "plaster"),                        // ground storey (to y 3.0)
                    Box(0f, 4.5f, 2.775f, 7.5f, 3f, 6.45f, "plaster"),                   // upper storey jettied out 0.45 m (y 3.0..6.0, eave 6 m)
                    Box(0f, 3.05f, -0.46f, 7.5f, 0.3f, 0.1f, "oak_timber"),              // jetty beam
                    Box(0f, 1.85f, -0.05f, 1.4f, 2.4f, 0.12f, "painted_wood"),           // door
                    Box(-2.6f, 1.9f, -0.04f, 0.9f, 1.1f, 0.1f, "painted_wood"),          // shutters
                    Box(2.6f, 1.9f, -0.04f, 0.9f, 1.1f, 0.1f, "painted_wood"),
                    Box(-2.6f, 4.6f, -0.5f, 0.9f, 1.1f, 0.1f, "painted_wood"),
                    Box(2.6f, 4.6f, -0.5f, 0.9f, 1.1f, 0.1f, "painted_wood"),
                    Box(0f, 7.5f, 1.025f, 8.1f, 0.25f, 4.65f, "roof_tile", -40.2f),      // roof, ridge about 9 m
                    Box(0f, 7.5f, 4.575f, 8.1f, 0.25f, 4.65f, "roof_tile", 40.2f),
                } },
            new Row { Brief = Stall, Id = "MarketStall", Batch = "B8", Names = new[] { "market_stall", "stall" },
                Instances = new[] { At(4.5f, 0f, 4.5f, 200f) },   // 2.6 x 1.6 m, awning top about 2.7 m, rotated 200 deg so the counter faces the fountain
                Placeholder = new[]
                {
                    Box(0f, 0.45f, 0.45f, 2.6f, 0.9f, 0.7f, "painted_wood"),             // counter (front = +Z)
                    Box(0f, 1.2f, -0.78f, 2.6f, 2.4f, 0.05f, "oak_timber"),              // back board
                    Box(-1.25f, 1.25f, 0.7f, 0.1f, 2.5f, 0.1f, "oak_timber"), Box(1.25f, 1.25f, 0.7f, 0.1f, 2.5f, 0.1f, "oak_timber"),
                    Box(0f, 2.62f, 0f, 2.8f, 0.08f, 1.8f, "canvas", 8f),                 // awning, top about 2.7 m
                } },
            // Props cluster (B7): 3 barrels, 3 crates, 4 sacks, 2 buckets, 3 baskets, 2 jugs, by the stall and the facade door (door at x -1.25, z 7.5).
            new Row { Brief = Props, Id = "Barrels", Batch = "B7", Names = new[] { "barrel", "barrels" },
                Instances = new[] { At(-0.1f, 0f, 6.95f), At(0.7f, 0f, 7.0f, 40f), At(-3.3f, 0f, 6.9f, 15f) },
                Placeholder = new[] { Cyl(0f, 0.45f, 0f, 0.62f, 0.9f, "barrel_oak") } },
            new Row { Brief = Props, Id = "Crates", Batch = "B7", Names = new[] { "crate", "crates" },
                Instances = new[] { At(1.9f, 0f, 6.95f, 12f), At(2.1f, 0.6f, 6.95f, -20f), At(5.9f, 0f, 3.2f, 70f) },
                Placeholder = new[] { Box(0f, 0.3f, 0f, 0.6f, 0.6f, 0.6f, "painted_wood") } },
            new Row { Brief = Props, Id = "Sacks", Batch = "B7", Names = new[] { "sack", "sacks" },
                Instances = new[] { At(-2.1f, 0f, 6.85f, 20f), At(-2.55f, 0f, 6.8f, -15f), At(6.1f, 0f, 5.6f, 100f), At(5.9f, 0f, 6.0f, 60f) },
                Placeholder = new[] { Box(0f, 0.275f, 0f, 0.42f, 0.55f, 0.3f, "canvas") } },
            new Row { Brief = Props, Id = "Buckets", Batch = "B7", Names = new[] { "bucket", "buckets" },
                Instances = new[] { At(-0.9f, 0f, 6.7f), At(5.4f, 0f, 3.0f) },
                Placeholder = new[] { Cyl(0f, 0.15f, 0f, 0.32f, 0.3f, "oak_timber") } },
            new Row { Brief = Props, Id = "Baskets", Batch = "B7", Names = new[] { "basket", "baskets" },
                Instances = new[] { At(3.0f, 0f, 3.5f), At(6.2f, 0f, 4.3f, 30f), At(1.1f, 0f, 6.5f) },
                Placeholder = new[] { Cyl(0f, 0.17f, 0f, 0.5f, 0.34f, "oak_timber") } },
            new Row { Brief = Props, Id = "Jugs", Batch = "B7", Names = new[] { "jug", "jugs" },
                Instances = new[] { At(3.95f, 0.9f, 4.55f), At(3.55f, 0.9f, 4.75f) },   // on the stall counter
                Placeholder = new[] { Cyl(0f, 0.2f, 0f, 0.24f, 0.4f, "painted_wood") } },
            // Foliage (B9)
            new Row { Brief = Foliage, Id = "Ivy", Batch = "B9", Names = new[] { "ivy", "facade_ivy" }, Foliage = true,
                Instances = new[] { At(-4f, 0f, 7.5f) },      // the facade's left 2 m (x -5..-3), ground to the eave; pivot = bottom centre of the 2 m strip
                Placeholder = new[] { Box(0f, 1.5f, -0.04f, 2f, 3f, 0.06f, "green"), Box(0f, 4.5f, -0.5f, 2f, 3f, 0.06f, "green") } },
            new Row { Brief = Foliage, Id = "CrackWeeds", Batch = "B9", Names = new[] { "crack_weeds", "weeds", "weed", "crack_weed" }, Foliage = true,
                Instances = new[] { At(-4.4f, 0f, 7.3f), At(-3.0f, 0f, 7.35f, 40f), At(-1.9f, 0f, 7.3f, 80f), At(0.2f, 0f, 7.35f, 10f), At(1.6f, 0f, 7.3f, 120f),
                    At(2.3f, 0f, 7.3f, 60f),
                    At(2.75f, 0f, 0.8f, 30f), At(-2.4f, 0f, -1.7f, 100f), At(1.2f, 0f, -2.65f, 160f), At(-2.8f, 0f, 0.9f, 70f) },   // facade foot, then the fountain ring (radius 2.9)
                Placeholder = new[] { Cyl(0f, 0.11f, 0f, 0.32f, 0.22f, "green") } },
            new Row { Brief = Foliage, Id = "BasinMoss", Batch = "B9", Names = new[] { "basin_moss", "moss", "moss_patch" }, Foliage = true,
                Instances = new[] { At(-0.9f, 0.55f, 1.45f, -32f) },   // on the basin rim, the side away from the sun (the sun shines from +x, -z)
                Placeholder = new[] { Box(0f, 0.02f, 0f, 0.9f, 0.04f, 0.5f, "green") } },
            // Lanterns (B8)
            new Row { Brief = Lanterns, Id = "StallLantern", Batch = "B8", Names = new[] { "stall_lantern", "lantern", "hanging_lantern" },
                Instances = new[] { At(3.58f, 0.9f, 4.3f) },  // on the stall counter
                Placeholder = new[] { Box(0f, 0.17f, 0f, 0.2f, 0.34f, 0.2f, "warm") } },
            new Row { Brief = Lanterns, Id = "BracketLantern", Batch = "B8", Names = new[] { "bracket_lantern", "wall_lantern", "lantern_bracket" },
                Instances = new[] { At(0.0f, 2.45f, 7.38f) }, // on the facade wall beside the door (door at x -1.25)
                Placeholder = new[] { Box(0f, 0f, 0f, 0.22f, 0.36f, 0.22f, "warm"), Box(0f, 0.2f, 0.1f, 0.05f, 0.05f, 0.3f, "iron") } },
            // Neighbour massing (round 2): greybox buildings closing the square so Wide has no empty ground. Not brief rows; later B4 variants replace them.
            // Pivot = front face centre at ground, front facing -Z, like the facade. Names stay empty: art never replaces these by folder match.
            new Row { Brief = Neighbours, Id = "NEIGHBOUR_Left", Batch = "B4", Names = new string[0],
                Instances = new[] { At(-9.25f, 0f, 8.5f, -8f) },     // x -13..-5.5, front at z 8.5, turned a little toward the plaza, 3 storeys
                Placeholder = TallHouse() },
            new Row { Brief = Neighbours, Id = "NEIGHBOUR_Right", Batch = "B4", Names = new string[0],
                Instances = new[] { At(7f, 0f, 10f, 6f) },           // x 3..11, front at z 10, 2 storeys, gable to the plaza, lower roof
                Placeholder = GableHouse() },
            new Row { Brief = Neighbours, Id = "NEIGHBOUR_Backdrop", Batch = "B4", Names = new string[0],
                Instances = new[] { At(0f, 0f, 18f) },               // low far row past z 16 with a church and bell tower
                Placeholder = Backdrop() },
        };

        // Left neighbour: three jettied storeys (leaning a little), eave 8.8 m, ridge 11.6 m, chimney.
        static Part[] TallHouse()
        {
            var p = new List<Part>
            {
                Box(0f, 0.35f, 3.6f, 7.7f, 0.7f, 7.3f, "sandstone"),
                Box(0f, 2.05f, 3.5f, 7.5f, 2.7f, 7f, "plaster"),
                Box(0f, 4.75f, 3.325f, 7.5f, 2.7f, 7.35f, "plaster", 0f, 0f, 1.6f),
                Box(0f, 3.5f, -0.37f, 7.5f, 0.3f, 0.1f, "oak_timber"),
                Box(0f, 7.45f, 3.15f, 7.5f, 2.7f, 7.7f, "plaster", 0f, 0f, -1.4f),
                Box(0f, 6.2f, -0.72f, 7.5f, 0.3f, 0.1f, "oak_timber"),
                Box(0f, 1.8f, -0.05f, 1.3f, 2.2f, 0.12f, "painted_wood"),
                Box(0f, 10.2f, 0.975f, 8.1f, 0.25f, 5.17f, "roof_tile", -32.8f),
                Box(0f, 10.2f, 5.325f, 8.1f, 0.25f, 5.17f, "roof_tile", 32.8f),
                Box(2.4f, 11.6f, 4.2f, 0.8f, 2.2f, 0.8f, "sandstone"),
            };
            float[] ys = { 2.3f, 5.0f, 7.7f }, zs = { -0.05f, -0.4f, -0.75f };
            for (int s = 0; s < 3; s++)
                foreach (float x in new[] { -2.4f, 2.4f }) p.Add(Box(x, ys[s], zs[s], 0.9f, 1.2f, 0.1f, "painted_wood"));
            return p.ToArray();
        }

        // Right neighbour: two storeys, the gable end toward the plaza (roof ridge along Z), eave 5.8 m, ridge 8.4 m.
        static Part[] GableHouse()
        {
            return new[]
            {
                Box(0f, 0.25f, 3.3f, 8.2f, 0.5f, 6.7f, "sandstone"),
                Box(0f, 1.85f, 3.25f, 8f, 2.7f, 6.5f, "plaster"),
                Box(0f, 4.5f, 3.1f, 8f, 2.6f, 6.8f, "plaster", 0f, 0f, 1.0f),
                Box(0f, 3.25f, -0.33f, 8f, 0.28f, 0.1f, "oak_timber"),
                Box(0f, 6.2f, -0.18f, 7.6f, 0.8f, 0.25f, "plaster"),
                Box(0f, 7.0f, -0.18f, 5.2f, 0.8f, 0.25f, "plaster"),
                Box(0f, 7.8f, -0.18f, 2.8f, 0.8f, 0.25f, "plaster"),
                Box(0f, 1.8f, -0.05f, 1.3f, 2.2f, 0.12f, "painted_wood"),
                Box(-2.5f, 2.2f, -0.04f, 0.9f, 1.1f, 0.1f, "painted_wood"), Box(2.5f, 2.2f, -0.04f, 0.9f, 1.1f, 0.1f, "painted_wood"),
                Box(-2.2f, 4.6f, -0.4f, 0.9f, 1.1f, 0.1f, "painted_wood"), Box(2.2f, 4.6f, -0.4f, 0.9f, 1.1f, 0.1f, "painted_wood"),
                Box(2.2f, 7.1f, 3.1f, 5.1f, 0.25f, 7.4f, "roof_tile", 0f, 0f, -30.6f),
                Box(-2.2f, 7.1f, 3.1f, 5.1f, 0.25f, 7.4f, "roof_tile", 0f, 0f, 30.6f),
            };
        }

        // Far row: small houses with varied heights and leans along x -42..42, a church with a bell tower and spire in the middle gap (tower top about 16.8 m).
        static Part[] Backdrop()
        {
            var p = new List<Part>();
            int k = 0;
            for (float x = -42f; x <= 42f; x += 7.4f, k++)
            {
                if (x > -10f && x < 10f) continue;
                float h = 4.0f + (k % 4) * 0.8f, rise = 1.9f + (k % 3) * 0.4f, len = Mathf.Sqrt(3.5f * 3.5f + rise * rise), ang = Mathf.Atan2(rise, 3.5f) * Mathf.Rad2Deg;
                p.Add(Box(x, h / 2f, 3f, 6.6f, h, 6f, k % 3 == 1 ? "sandstone" : "plaster", 0f, 0f, k % 2 == 0 ? 0.8f : -0.8f));
                p.Add(Box(x, h + rise / 2f, 1.25f, 7.4f, 0.25f, len, "roof_tile", -ang));
                p.Add(Box(x, h + rise / 2f, 4.75f, 7.4f, 0.25f, len, "roof_tile", ang));
            }
            p.Add(Box(-3f, 5.8f, 2.2f, 4.4f, 11.6f, 4.4f, "sandstone"));
            p.Add(Box(-3f, 12.7f, 2.2f, 4.8f, 2.2f, 4.8f, "oak_timber"));
            float[] widths = { 3.6f, 2.6f, 1.6f, 0.8f };
            for (int i = 0; i < 4; i++) p.Add(Box(-3f, 14.175f + i * 0.75f, 2.2f, widths[i], 0.75f, widths[i], "roof_tile", 0f, 45f, 0f));
            p.Add(Box(4.6f, 3.8f, 5f, 9f, 7.6f, 10f, "sandstone"));
            p.Add(Box(4.6f + 2.45f, 9.1f, 5f, 5.75f, 0.25f, 10.6f, "roof_tile", 0f, 0f, -31.5f));
            p.Add(Box(4.6f - 2.45f, 9.1f, 5f, 5.75f, 0.25f, 10.6f, "roof_tile", 0f, 0f, 31.5f));
            return p.ToArray();
        }

        public static IEnumerable<Row> ForBrief(string brief)
        {
            foreach (Row r in Rows) if (r.Brief == brief) yield return r;
        }

        public static int InstanceCount(string rowId)
        {
            foreach (Row r in Rows) if (r.Id == rowId) return r.Instances.Length;
            return 0;
        }

        // The three capture cameras (brief section 1): position, look-at target, vertical field of view. Wide/Mid/Close.
        public static readonly (string Name, Vector3 Pos, Vector3 Target, float Fov)[] Cameras =
        {
            ("Cap_Corner_Close", new Vector3(1.6f, 1.4f, -1.6f), new Vector3(0f, 1.0f, 0.6f), 38f),
            ("Cap_Corner_Mid", new Vector3(5f, 1.7f, -2.5f), new Vector3(-1.5f, 1.8f, 5f), 42f),
            ("Cap_Corner_Wide", new Vector3(1f, 1.8f, -9f), new Vector3(-1f, 3f, 6f), 46f),
        };
    }
}
