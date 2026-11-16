// Core/Town/HollowmereDressing.cs: the Hollowmere square's free-grid decorations (design docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md
// section 5.3 and section 6 "Square dressing"): benches, planters, pots, lantern posts, banner poles, B7 prop clusters and the bunting spans, for both
// presets. Pure C#, no engine references; the Editor builder (HollowmereSquare) places the rows, the EditMode tests and later M6 read the same data.
// Frame as TownLayout: metres, +Z north, origin = fountain centre, yaw in degrees clockwise from above (forward = (sin yaw, cos yaw)).
// A row's footprint is centred on (X, Z), SizeX along its own X, SizeZ along its own Z, turned by Yaw. Decorations are free-grid: they never block the NPC path
// graph (Validate checks every path node and edge against the footprint inflated by Clearance), never overlap a lot or landmark, never sit in the approved
// beauty-corner rectangle, and stay on the square (+1 m) or beside a lane stub. Bunting is overhead and has no ground footprint.
// Authority: none (static layout data).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public enum DecoPreset : byte { Ruined, Rebuilt, Both }

    public enum DecoKind : byte { Bench, Planter, Pot, LanternPost, BannerPole, Prop }

    public sealed class DecoRow
    {
        public string Id, Asset, Batch; public DecoKind Kind; public DecoPreset Preset; public bool Broken;
        public float X, Z, Yaw, SizeX, SizeZ;
        public float TiltX, TiltZ;   // euler degrees for a toppled B7 prop (the builder sits the tilted art on the ground)
        public int Plants;           // B9 grass / weed instances stood on a planter or pot
        public bool Light;           // a hanging lantern at the post's hook point
        public float Height;         // collider height

        public bool Tilted => TiltX != 0f || TiltZ != 0f;
    }

    // B4 (docs/design/area1-hollowmere.md S1, S3, S4, section 6): the home door set, the bakery lot and the market row. Stage: -1 = both presets, 0 = Ruined (stage 0),
    // 1 = Frame (the bakery's stage 1, built hidden: the placed frame shows it), 2 = Rebuilt (stage 2). Piece is an adopted code ("T06a"), a B4Kit piece ("Woodshed") or one of ours.
    // Size > 0 scales an adopted piece so its longest extent is that many metres; Y lifts it (on a table or a crate). Loose rows (scattered debris, produce on a counter) skip the
    // pairwise overlap rule; Anchor names the landmark or lot whose footprint the row may stand on or in (stall counters, a lot's own footing).
    public sealed class SpotRow
    {
        public string Id, Spot, Piece, Anchor; public int Stage; public bool Loose;
        public float X, Z, Yaw, Size, Y, SizeX, SizeZ, TiltX, TiltZ;
        public bool Tilted => TiltX != 0f || TiltZ != 0f;
    }

    public sealed class BuntingSpan
    {
        public string Id; public DecoPreset Preset; public bool Torn;
        public float AX, AZ, AH, BX, BZ, BH;   // end positions and heights above the ground there
        public float Sag;                      // metres of droop at the middle
        public float Length => (float)Math.Sqrt((BX - AX) * (BX - AX) + (BZ - AZ) * (BZ - AZ));
    }

    public static class HollowmereDressing
    {
        public const float StallMargin = 0.4f;               // market stalls keep a wider berth (their art overhangs the footprint)
        public const float Clearance = 0.25f;               // gap kept between a footprint and any path node / edge
        public const float MinBuntingHeight = 2.6f, MaxBuntingSpan = 14f;
        public const float SquareMargin = 1f;
        // The approved beauty corner (x -12..8, z -6..8): nothing here may stand in it.
        public static readonly Footprint CornerRect = new Footprint(new TownPoint(-2f, 1f), 20f, 14f, 0f);

        static DecoRow R(string id, string asset, string batch, DecoKind kind, DecoPreset preset, float x, float z, float yaw, float sx, float sz, float h, bool broken = false,
            int plants = 0, float tiltX = 0f, float tiltZ = 0f, bool light = false) =>
            new DecoRow { Id = id, Asset = asset, Batch = batch, Kind = kind, Preset = preset, X = x, Z = z, Yaw = yaw, SizeX = sx, SizeZ = sz, Height = h, Broken = broken, Plants = plants, TiltX = tiltX, TiltZ = tiltZ, Light = light };

        const DecoPreset Rb = DecoPreset.Rebuilt, Ru = DecoPreset.Ruined;

        public static readonly DecoRow[] Rows =
        {
            // ---- REBUILT: lived in ----
            R("Bench_1", "Bench_A", "Square", DecoKind.Bench, Rb, -3f, -15f, 90f, 1.7f, 0.6f, 0.9f),
            R("Bench_2", "Bench_A", "Square", DecoKind.Bench, Rb, 3f, -15f, -90f, 1.7f, 0.6f, 0.9f),
            R("Bench_3", "Bench_A", "Square", DecoKind.Bench, Rb, -12f, -9f, 90f, 1.7f, 0.6f, 0.9f),
            R("Bench_4", "Bench_A", "Square", DecoKind.Bench, Rb, 12f, -8f, -90f, 1.7f, 0.6f, 0.9f),
            R("Bench_5", "Bench_A", "Square", DecoKind.Bench, Rb, -14.4f, -17.1f, 45f, 1.7f, 0.6f, 0.9f),
            R("Bench_6", "Bench_A", "Square", DecoKind.Bench, Rb, 14.5f, -17.5f, -45f, 1.7f, 0.6f, 0.9f),

            R("Planter_1", "Planter_A", "Square", DecoKind.Planter, Rb, -3f, -12.8f, 90f, 1.2f, 0.55f, 0.6f, plants: 3),
            R("Planter_2", "Planter_B", "Square", DecoKind.Planter, Rb, 3f, -12.8f, 90f, 1.2f, 0.55f, 0.6f, plants: 3),
            R("Planter_3", "Planter_A", "Square", DecoKind.Planter, Rb, -3f, -17.2f, 90f, 1.2f, 0.55f, 0.6f, plants: 3),
            R("Planter_4", "Planter_B", "Square", DecoKind.Planter, Rb, 3f, -17.2f, 90f, 1.2f, 0.55f, 0.6f, plants: 3),
            R("Planter_5", "Planter_A", "Square", DecoKind.Planter, Rb, -12f, -7.3f, 0f, 1.2f, 0.55f, 0.6f, plants: 3),
            R("Planter_6", "Planter_B", "Square", DecoKind.Planter, Rb, 12f, -6.3f, 0f, 1.2f, 0.55f, 0.6f, plants: 3),

            R("Pot_1", "Pot_Clay", "Square", DecoKind.Pot, Rb, -3.9f, -13.9f, 0f, 0.5f, 0.5f, 0.6f, plants: 1),
            R("Pot_2", "Pot_Clay", "Square", DecoKind.Pot, Rb, 3.9f, -13.9f, 40f, 0.5f, 0.5f, 0.6f, plants: 2),
            R("Pot_3", "Pot_Clay", "Square", DecoKind.Pot, Rb, -13.6f, 5.2f, 0f, 0.5f, 0.5f, 0.6f, plants: 1),
            R("Pot_4", "Pot_Clay", "Square", DecoKind.Pot, Rb, -14.3f, 5.3f, 70f, 0.5f, 0.5f, 0.6f, plants: 2),
            R("Pot_5", "Pot_Clay", "Square", DecoKind.Pot, Rb, 18.9f, -5.2f, 0f, 0.5f, 0.5f, 0.6f, plants: 1),
            R("Pot_6", "Pot_Clay", "Square", DecoKind.Pot, Rb, 18.9f, -6.8f, 25f, 0.5f, 0.5f, 0.6f, plants: 2),
            R("Pot_7", "Pot_Clay", "Square", DecoKind.Pot, Rb, -11.2f, -9.8f, 0f, 0.5f, 0.5f, 0.6f, plants: 1),

            R("Lamp_1", "LanternPost_A", "Square", DecoKind.LanternPost, Rb, -15f, -3f, 90f, 0.5f, 0.5f, 3.2f, light: true),
            R("Lamp_2", "LanternPost_A", "Square", DecoKind.LanternPost, Rb, 15f, -3.5f, -90f, 0.5f, 0.5f, 3.2f, light: true),
            R("Lamp_3", "LanternPost_A", "Square", DecoKind.LanternPost, Rb, -9.5f, -18.8f, 0f, 0.5f, 0.5f, 3.2f, light: true),
            R("Lamp_4", "LanternPost_A", "Square", DecoKind.LanternPost, Rb, 9.5f, -18.8f, 0f, 0.5f, 0.5f, 3.2f, light: true),
            R("Lamp_5", "LanternPost_A", "Square", DecoKind.LanternPost, Rb, -15f, -13f, 0f, 0.5f, 0.5f, 3.2f, light: true),
            R("Lamp_6", "LanternPost_A", "Square", DecoKind.LanternPost, Rb, 15f, -11f, 0f, 0.5f, 0.5f, 3.2f, light: true),

            R("Banner_1", "BannerPole", "B10", DecoKind.BannerPole, Rb, -3.2f, -19.6f, 0f, 0.4f, 0.4f, 4.5f),
            R("Banner_2", "BannerPole", "B10", DecoKind.BannerPole, Rb, 3.2f, -19.6f, 180f, 0.4f, 0.4f, 4.5f),
            R("Banner_3", "BannerPole", "B10", DecoKind.BannerPole, Rb, -17.8f, -4.6f, 90f, 0.4f, 0.4f, 4.5f),

            // B7 prop clusters: by the tool stall, by the bakery door, by the inn door.
            R("Prop_S3_Barrel", "Barrel_A", "B7", DecoKind.Prop, Rb, 7.5f, -9.5f, 20f, 0.65f, 0.65f, 0.9f),
            R("Prop_S3_Crate", "Crate_A", "B7", DecoKind.Prop, Rb, 8.3f, -9.9f, -10f, 0.6f, 0.6f, 0.6f),
            R("Prop_S3_Sack", "Sack_B", "B7", DecoKind.Prop, Rb, 7.1f, -10.3f, 150f, 0.45f, 0.35f, 0.55f),
            R("Prop_S3b_Crate", "Crate_B", "B7", DecoKind.Prop, Rb, 3.6f, -10f, 90f, 0.9f, 0.5f, 0.5f),
            R("Prop_S3b_Basket", "Basket_Produce", "B7", DecoKind.Prop, Rb, 2.9f, -10.1f, 30f, 0.45f, 0.45f, 0.35f),
            R("Prop_S3b_Bucket", "Bucket_A", "B7", DecoKind.Prop, Rb, 4.4f, -10.6f, 0f, 0.35f, 0.35f, 0.3f),
            R("Prop_S3b_SackPair", "Sack_Pair", "B7", DecoKind.Prop, Rb, 3.9f, -11.1f, 100f, 0.8f, 0.4f, 0.55f),
            R("Prop_Bakery_Bread", "Basket_Bread", "B7", DecoKind.Prop, Rb, -15.2f, 5.1f, 10f, 0.45f, 0.45f, 0.35f),
            R("Prop_Bakery_Lidded", "Basket_Lidded", "B7", DecoKind.Prop, Rb, -15.9f, 5.2f, 40f, 0.45f, 0.45f, 0.4f),
            R("Prop_Bakery_SackA", "Sack_A", "B7", DecoKind.Prop, Rb, -16.7f, 5.0f, 165f, 0.45f, 0.35f, 0.55f),
            R("Prop_Bakery_SackC", "Sack_C", "B7", DecoKind.Prop, Rb, -17.4f, 5.2f, 110f, 0.45f, 0.35f, 0.55f),
            R("Prop_Inn_BarrelB", "Barrel_B", "B7", DecoKind.Prop, Rb, 18.6f, -3.6f, 35f, 0.65f, 0.65f, 0.9f),
            R("Prop_Inn_BarrelC", "Barrel_C", "B7", DecoKind.Prop, Rb, 18.9f, -8.4f, 10f, 0.65f, 0.65f, 0.9f),
            R("Prop_Inn_BucketB", "Bucket_B", "B7", DecoKind.Prop, Rb, 18.5f, -9.2f, 0f, 0.35f, 0.35f, 0.3f),
            R("Prop_Inn_JugA", "Jug_A", "B7", DecoKind.Prop, Rb, 18.5f, -2.9f, 0f, 0.3f, 0.3f, 0.4f),

            // ---- RUINED: sparse and broken ----
            R("Bench_Broken_1", "Bench_Broken", "Square", DecoKind.Bench, Ru, -3f, -15f, 80f, 1.7f, 0.6f, 0.6f, broken: true),
            R("Bench_Broken_2", "Bench_Broken", "Square", DecoKind.Bench, Ru, 12f, -8f, -100f, 1.7f, 0.6f, 0.6f, broken: true),
            R("Planter_Broken_1", "Planter_Broken", "Square", DecoKind.Planter, Ru, 3f, -12.8f, 95f, 1.2f, 0.55f, 0.5f, broken: true, plants: 3),
            R("Planter_Broken_2", "Planter_Broken", "Square", DecoKind.Planter, Ru, -12f, -7.3f, 10f, 1.2f, 0.55f, 0.5f, broken: true, plants: 3),
            R("Pot_Broken_1", "Pot_Broken", "Square", DecoKind.Pot, Ru, -2.3f, -16.2f, 0f, 0.5f, 0.5f, 0.4f, broken: true),
            R("Pot_Broken_2", "Pot_Broken", "Square", DecoKind.Pot, Ru, 11.6f, -6.6f, 60f, 0.5f, 0.5f, 0.4f, broken: true),
            R("Pot_Broken_3", "Pot_Broken", "Square", DecoKind.Pot, Ru, -13.6f, 5.2f, 120f, 0.5f, 0.5f, 0.4f, broken: true),
            R("Lamp_Broken_1", "LanternPost_Broken", "Square", DecoKind.LanternPost, Ru, -15f, -3f, 90f, 0.5f, 0.5f, 2.6f, broken: true),
            R("Lamp_Broken_2", "LanternPost_Broken", "Square", DecoKind.LanternPost, Ru, 9.5f, -18.8f, 0f, 0.5f, 0.5f, 2.6f, broken: true),
            R("Lamp_Broken_3", "LanternPost_Broken", "Square", DecoKind.LanternPost, Ru, 15f, -11f, 0f, 0.5f, 0.5f, 2.6f, broken: true),
            R("Banner_Ruin", "BannerPole", "B10", DecoKind.BannerPole, Ru, -3.2f, -19.6f, 0f, 0.4f, 0.4f, 4.5f),
            R("Prop_Ruin_Barrel", "Barrel_A", "B7", DecoKind.Prop, Ru, 6.8f, -10.2f, 25f, 0.65f, 0.95f, 0.65f, broken: true, tiltX: 90f),
            R("Prop_Ruin_Crate", "Crate_C", "B7", DecoKind.Prop, Ru, -14f, -10f, 35f, 0.6f, 0.6f, 0.6f, broken: true, tiltX: 8f, tiltZ: 12f),
            R("Prop_Ruin_Sack", "Sack_B", "B7", DecoKind.Prop, Ru, 13.2f, -15f, 70f, 0.5f, 0.45f, 0.3f, broken: true, tiltX: 80f),
            R("Prop_Ruin_Bucket", "Bucket_A", "B7", DecoKind.Prop, Ru, -1.5f, -17.5f, 0f, 0.4f, 0.4f, 0.35f, broken: true, tiltX: 90f),
            // B3 round 3: the open middle ground, dropped goods in small groups (crates split, sacks slumped, a barrel on its side)
            R("Drop_A_Crate", "Crate_B", "B7", DecoKind.Prop, Ru, -1.8f, -9.6f, 25f, 0.9f, 0.5f, 0.5f, broken: true, tiltX: 6f, tiltZ: 10f),
            R("Drop_A_Sack", "Sack_Pair", "B7", DecoKind.Prop, Ru, -1.9f, -11.3f, 100f, 0.8f, 0.4f, 0.3f, broken: true, tiltX: 70f),
            R("Drop_A_Basket", "Basket_Produce", "B7", DecoKind.Prop, Ru, -3.1f, -9.5f, 0f, 0.45f, 0.45f, 0.3f, broken: true, tiltX: 90f),
            R("Drop_B_Crate", "Crate_A", "B7", DecoKind.Prop, Ru, 1.8f, -14.2f, -30f, 0.6f, 0.6f, 0.6f, broken: true, tiltX: 12f, tiltZ: -8f),
            R("Drop_B_SackA", "Sack_A", "B7", DecoKind.Prop, Ru, 2.3f, -14.8f, 40f, 0.45f, 0.35f, 0.3f, broken: true, tiltX: 80f),
            R("Drop_B_SackC", "Sack_C", "B7", DecoKind.Prop, Ru, 0.9f, -13.2f, 200f, 0.45f, 0.35f, 0.3f, broken: true, tiltX: 75f),
            R("Drop_C_Barrel", "Barrel_C", "B7", DecoKind.Prop, Ru, 10.6f, -12.8f, 70f, 0.65f, 0.95f, 0.65f, broken: true, tiltX: 90f),
            R("Drop_C_Crate", "Crate_C", "B7", DecoKind.Prop, Ru, 9.4f, -12.2f, 10f, 0.6f, 0.6f, 0.6f, broken: true, tiltX: 8f, tiltZ: 14f),
            R("Drop_D_Crate", "Crate_B", "B7", DecoKind.Prop, Ru, 9.8f, 3.2f, -15f, 0.9f, 0.5f, 0.5f, broken: true, tiltX: 5f, tiltZ: -9f),
            R("Drop_D_Sack", "Sack_B", "B7", DecoKind.Prop, Ru, 10.8f, 2.2f, 95f, 0.5f, 0.45f, 0.3f, broken: true, tiltX: 80f),
            R("Drop_D_Bucket", "Bucket_B", "B7", DecoKind.Prop, Ru, 9.2f, 1.6f, 0f, 0.4f, 0.4f, 0.35f, broken: true, tiltX: 90f),
            R("Drop_E_Crate", "Crate_A", "B7", DecoKind.Prop, Ru, -9.8f, -7.0f, 55f, 0.6f, 0.6f, 0.6f, broken: true, tiltX: 10f, tiltZ: 6f),
            R("Drop_E_Sack", "Sack_Pair", "B7", DecoKind.Prop, Ru, -8.7f, -7.2f, 10f, 0.8f, 0.4f, 0.3f, broken: true, tiltX: 70f),
        };

        public static readonly BuntingSpan[] Bunting =
        {
            new BuntingSpan { Id = "Bunting_Banners", Preset = Rb, AX = -3.2f, AZ = -19.6f, AH = 4.2f, BX = 3.2f, BZ = -19.6f, BH = 4.2f, Sag = 0.7f },
            new BuntingSpan { Id = "Bunting_WestLamp", Preset = Rb, AX = -9.5f, AZ = -18.8f, AH = 2.9f, BX = -3.2f, BZ = -19.6f, BH = 4.0f, Sag = 0.6f },
            new BuntingSpan { Id = "Bunting_EastLamp", Preset = Rb, AX = 3.2f, AZ = -19.6f, AH = 4.0f, BX = 9.5f, BZ = -18.8f, BH = 2.9f, Sag = 0.6f },
            new BuntingSpan { Id = "Bunting_Inn", Preset = Rb, AX = 21f, AZ = -3f, AH = 4.5f, BX = 15f, BZ = -3.5f, BH = 2.9f, Sag = 0.65f },
            new BuntingSpan { Id = "Bunting_WestLane", Preset = Rb, AX = -15f, AZ = -3f, AH = 3.1f, BX = -15f, BZ = -13f, BH = 3.1f, Sag = 0.8f },
            new BuntingSpan { Id = "Bunting_EastLane", Preset = Rb, AX = 15f, AZ = -3.5f, AH = 3.1f, BX = 15f, BZ = -11f, BH = 3.1f, Sag = 0.7f },
            new BuntingSpan { Id = "Bunting_Torn", Preset = Ru, AX = -3.2f, AZ = -19.6f, AH = 3.6f, BX = -6.5f, BZ = -19.9f, BH = 2.7f, Sag = 0.9f, Torn = true },
        };

        // ------------------------------------------------------------------------------------------------ B4 spots
        public const int Always = -1, StageRuined = 0, StageFrame = 1, StageRebuilt = 2;

        /// <summary>DH8: the spawn faces the fountain and the haze gap over the square's south side (the Cap_HM_HomeDoor aim point), degrees clockwise from +Z.</summary>
        public static float SpawnYaw(TownLayout t)
        {
            Landmark home = t.Landmarks.First(l => l.Id == "HeroesHome_Cistern");
            PathNode door = t.Nodes.First(n => n.Id == home.DoorNode);
            return (float)(Math.Atan2(-3f - door.Pos.X, -5f - (door.Pos.Z - 3f)) * 180.0 / Math.PI);
        }

        static SpotRow P(string id, string spot, string piece, int stage, float x, float z, float yaw, float sx, float sz, float size = 0f, float y = 0f, bool loose = false, string anchor = null, float tx = 0f, float tz = 0f) =>
            new SpotRow { Id = id, Spot = spot, Piece = piece, Stage = stage, X = x, Z = z, Yaw = yaw, SizeX = sx, SizeZ = sz, Size = size, Y = y, Loose = loose, Anchor = anchor, TiltX = tx, TiltZ = tz };

        public static readonly SpotRow[] Spots = BuildSpots();

        static SpotRow[] BuildSpots()
        {
            var l = new List<SpotRow>();
            // ---- S1 home door (the cistern house: x 8..17, front z 7.5, door x 12.5). The path along its east wall (x 19, to the keep stair) takes the east side, so the lean-to
            // stands on the west wall (decided 2026-11-15, B4; the spec says east) with the log piles in it; the ivy still climbs the east gable. Door-side rows keep to the doorstep strip (z 6.7-7.4).
            l.Add(P("Home_Woodshed", "S1", "Woodshed", Always, 7.05f, 11.6f, -90f, 3.2f, 1.9f));
            l.Add(P("Home_LogPile_A", "S1", "T06a", Always, 7.4f, 10.6f, -90f, 1.1f, 1.0f, size: 1.3f, loose: true));
            l.Add(P("Home_LogPile_B", "S1", "T06a", Always, 7.4f, 12.7f, -90f, 1.1f, 1.0f, size: 1.2f, loose: true));
            for (int i = 0; i < 6; i++)
                l.Add(P("Home_Firewood_" + i, "S1", "T06b", Always, 6.35f - 0.2f * (i % 2), 10.0f + 0.6f * i, 40f * i, 0.4f, 0.3f, size: 0.45f, loose: true));
            l.Add(P("Home_WashingLine", "S1", "WashingLine", StageRebuilt, 6.2f, 9.6f, -90f, 0.4f, 2.2f, loose: true));
            l.Add(P("Home_CartWheel", "S1", "T16g", Always, 15.5f, 7.05f, 0f, 0.9f, 0.25f, size: 0.95f, tx: 78f));
            l.Add(P("Home_Crate_A", "S1", "Crate_A", Always, 16.6f, 6.95f, -10f, 0.6f, 0.6f));
            l.Add(P("Home_Crate_B", "S1", "Crate_B", Always, 16.6f, 6.95f, 24f, 0.9f, 0.5f, y: 0.6f, loose: true));
            l.Add(P("Home_Crate_C", "S1", "Crate_C", Always, 17.25f, 6.8f, 8f, 0.6f, 0.6f, loose: true));
            l.Add(P("Home_Bench", "S1", "T16e", Always, 9.4f, 6.85f, 180f, 1.6f, 0.6f, size: 1.7f));
            l.Add(P("Home_FlowerBox", "S1", "FlowerBox", StageRebuilt, 14.2f, 7.2f, 180f, 1.4f, 0.5f));
            l.Add(P("Home_Pot_A", "S1", "Pot_Clay", StageRebuilt, 11.4f, 6.95f, 0f, 0.5f, 0.5f));
            l.Add(P("Home_Pot_B", "S1", "Pot_Clay", StageRebuilt, 13.05f, 6.95f, 40f, 0.5f, 0.5f));
            l.Add(P("Home_Pot_C", "S1", "Pot_Clay", StageRebuilt, 10.65f, 7.0f, 70f, 0.5f, 0.5f));
            l.Add(P("Home_Ivy_East", "S1", "T12c", Always, 17.1f, 13.2f, 90f, 0.5f, 2.4f, size: 3.2f, loose: true));
            // ---- S3 bakery lot F02 (L, centre -12.5, 12.7, footing x -18..-7, z 7.2..18.2; the doorstep is at x -12.5, z 6.6).
            for (int i = 0; i < 12; i++)
            {
                double a = 2.399963 * i; float r = 1.1f + 0.55f * (float)Math.Sqrt(i);
                l.Add(P("Bakery_Tile_" + i, "S3", "T05a", StageRuined, -14.6f + r * (float)Math.Cos(a), 11.4f + 0.8f * r * (float)Math.Sin(a), 47f * i, 0.4f, 0.3f, size: 0.4f, loose: true, anchor: "F02"));
            }
            l.Add(P("Bakery_Rubble_A", "S3", "T05b", StageRuined, -16.0f, 9.2f, 20f, 1.0f, 0.8f, size: 0.9f, anchor: "F02", loose: true));
            l.Add(P("Bakery_Rubble_B", "S3", "T05b", StageRuined, -9.4f, 10.2f, 130f, 0.9f, 0.7f, size: 0.8f, anchor: "F02", loose: true));
            l.Add(P("Bakery_Plank_A", "S3", "T05d", StageRuined, -15.2f, 12.6f, 70f, 1.8f, 0.3f, size: 1.9f, anchor: "F02", loose: true));
            l.Add(P("Bakery_Plank_B", "S3", "T05d", StageRuined, -10.4f, 13.4f, -25f, 1.8f, 0.3f, size: 1.7f, anchor: "F02", loose: true));
            l.Add(P("Bakery_Plank_C", "S3", "T05d", StageRuined, -13.4f, 9.8f, 105f, 1.8f, 0.3f, size: 1.6f, anchor: "F02", loose: true));
            l.Add(P("Bakery_Plank_D", "S3", "T05d", StageRuined, -16.4f, 14.8f, 15f, 1.8f, 0.3f, size: 1.8f, anchor: "F02", loose: true));
            // Tobin's bench, 3 m south-west of the step
            l.Add(P("Tobin_Table", "S3", "T16i", StageRuined, -14.9f, 4.3f, 15f, 1.5f, 0.9f, size: 1.5f));
            l.Add(P("Tobin_Toolbox", "S3", "T14c", StageRuined, -15.3f, 4.35f, 40f, 0.4f, 0.25f, size: 0.45f, y: 0.74f, loose: true));
            l.Add(P("Tobin_Plane", "S3", "T14d", StageRuined, -14.3f, 4.2f, 100f, 0.3f, 0.12f, size: 0.3f, y: 0.74f, loose: true));
            l.Add(P("Tobin_Sawhorse_A", "S3", "Sawhorse", StageRuined, -14.0f, 2.7f, 95f, 1.1f, 0.5f));
            l.Add(P("Tobin_Sawhorse_B", "S3", "Sawhorse", StageRuined, -15.6f, 2.5f, 80f, 1.1f, 0.5f));
            // stock pile, 3 x 3 m
            l.Add(P("Stock_Crate_A", "S3", "Crate_A", StageRuined, -17.9f, 3.0f, 10f, 0.6f, 0.6f));
            l.Add(P("Stock_Crate_B", "S3", "Crate_B", StageRuined, -17.0f, 3.1f, -15f, 0.9f, 0.5f));
            l.Add(P("Stock_Crate_C", "S3", "Crate_C", StageRuined, -18.4f, 3.7f, 40f, 0.6f, 0.6f));
            l.Add(P("Stock_Boards_A", "S3", "Board_Stack", StageRuined, -17.8f, 4.5f, 0f, 1.4f, 0.55f));
            l.Add(P("Stock_Boards_B", "S3", "Board_Stack", StageRuined, -17.6f, 5.3f, 8f, 1.4f, 0.55f));
            l.Add(P("Stock_Plank_A", "S3", "T05d", StageRuined, -18.5f, 4.9f, 85f, 1.6f, 0.3f, size: 1.7f, loose: true));
            // scaffold, stage 1 (loose: the lot ring and square edge paths run through the doorstep strip it stands on; it carries no collider, and the stage is not a built preset yet): south face (front) and west face of the footing; ladder, tool crate
            l.Add(P("Scaffold_South", "S3", "Scaffold_Bay", StageFrame, -15.8f, 6.7f, 0f, 3.0f, 1.2f, loose: true));
            l.Add(P("Scaffold_Corner", "S3", "Scaffold_Corner", StageFrame, -18.3f, 6.7f, 0f, 1.2f, 1.2f, loose: true));
            l.Add(P("Scaffold_West", "S3", "Scaffold_Bay", StageFrame, -18.4f, 9.3f, -90f, 3.0f, 1.2f, loose: true));
            l.Add(P("Scaffold_Ladder", "S3", "T04b", StageFrame, -9.8f, 6.8f, 180f, 0.5f, 0.4f, size: 3.2f, tx: 16f, loose: true));
            l.Add(P("Scaffold_ToolCrate", "S3", "Crate_A", StageFrame, -10.6f, 6.3f, 25f, 0.6f, 0.6f, loose: true));
            // flour sacks (stage 1 on)
            foreach (int st in new[] { StageFrame, StageRebuilt })
            {
                string k = st == StageFrame ? "F" : "R";
                l.Add(P("Flour_" + k + "_SackA", "S3", "Sack_A", st, -11.4f, 6.5f, 160f, 0.45f, 0.35f, loose: true));
                l.Add(P("Flour_" + k + "_SackB", "S3", "Sack_B", st, -11.0f, 6.2f, 110f, 0.5f, 0.45f, loose: true));
                l.Add(P("Flour_" + k + "_SackC", "S3", "Sack_C", st, -11.7f, 6.0f, 190f, 0.45f, 0.35f, loose: true));
            }
            // stage 2: bread and baskets by the door
            l.Add(P("Bakery_Baskets_A", "S3", "Basket_Bread", StageRebuilt, -9.8f, 6.6f, 15f, 0.45f, 0.45f, loose: true));
            l.Add(P("Bakery_Baskets_B", "S3", "Basket_Bread", StageRebuilt, -9.2f, 6.2f, 70f, 0.45f, 0.45f, loose: true));
            l.Add(P("Bakery_Board", "S3", "Crate_B", StageRebuilt, -8.4f, 6.4f, 90f, 0.9f, 0.5f, loose: true));
            for (int i = 0; i < 6; i++)
                l.Add(P("Bakery_Loaf_" + i, "S3", i % 2 == 0 ? "T08f" : "T08e", StageRebuilt, -8.4f + 0.1f * (i % 3 - 1), 6.4f + 0.15f * (i / 3 - 0.5f), 60f * i, 0.2f, 0.15f, size: 0.2f, y: 0.5f, loose: true));
            // ---- S4 market row: the tools stall (Stall_3 at 5.5, -8.5; the produce and cloth stalls stand in the approved corner, so their goods sit just outside it), clusters of five
            l.Add(P("Tools_Tongs", "S4", "T14b", Always, 5.1f, -8.2f, 20f, 0.4f, 0.15f, size: 0.45f, y: 0.9f, loose: true, anchor: "Stall_3"));
            l.Add(P("Tools_Rake", "S4", "T16k", Always, 6.9f, -7.6f, -35f, 0.3f, 1.6f, size: 1.7f, tz: 14f, loose: true, anchor: "Stall_3"));
            l.Add(P("Tools_Shovel", "S4", "T16l", Always, 7.25f, -7.3f, -35f, 0.3f, 1.3f, size: 1.35f, tz: 12f, loose: true, anchor: "Stall_3"));
            l.Add(P("Tools_Scythe", "S4", "T16m", Always, 7.6f, -7.0f, -35f, 0.3f, 1.8f, size: 1.85f, tz: 10f, loose: true, anchor: "Stall_3"));
            for (int i = 0; i < 3; i++)
                l.Add(P("Produce_Basket_" + i, "S4", "T08g", StageRebuilt, 3.3f + 0.55f * i, -6.6f - 0.15f * i, 30f * i, 0.4f, 0.4f, size: 0.4f));
            string[] veg = { "T08a", "T08b", "T08c", "T08d", "T08a", "T08d" };
            for (int i = 0; i < 6; i++)
                l.Add(P("Produce_" + i, "S4", veg[i], StageRebuilt, 3.3f + 0.55f * (i % 3) + 0.05f, -6.6f - 0.15f * (i % 3), 50f * i, 0.2f, 0.2f, size: i == 1 ? 0.28f : 0.14f, y: 0.2f, loose: true));
            // three clusters of five: tools stall side, cloth stall side, the inn wall
            Cluster(l, "Back_Tools", 11.0f, -11.2f, 0f, "Crate_A", "Barrel_A", "Sack_B", "Crate_B", "Bucket_A");
            Cluster(l, "Inn_Wall", 17.6f, 2.2f, 90f, "Barrel_B", "Crate_C", "Sack_A", "Barrel_C", "Basket_Produce");
            Cluster(l, "Back_Cloth", -6.2f, -10.6f, 0f, "Barrel_A", "Sack_C", "Crate_A", "Sack_B", "Jug_A");
            l.Add(P("Cloth_Pile_A", "S4", "T09", StageRebuilt, -7.2f, -11.9f, 20f, 0.6f, 0.5f, size: 0.7f, loose: true));
            l.Add(P("Cloth_Pile_B", "S4", "T09", StageRebuilt, -5.6f, -11.9f, -30f, 0.6f, 0.5f, size: 0.7f, loose: true));
            // inn front, stage 2 (F09, 26.5, -6; door west): the path along x 19.6 keeps the benches on the square side of it
            l.Add(P("Inn_Bench_A", "S4", "T16e", StageRebuilt, 18.9f, -1.5f, 90f, 1.7f, 0.6f, size: 1.7f));
            l.Add(P("Inn_Bench_B", "S4", "T16e", StageRebuilt, 18.9f, -10.7f, 90f, 1.7f, 0.6f, size: 1.7f));
            l.Add(P("Inn_Barrel", "S4", "T16d", StageRebuilt, 18.3f, -12.4f, 0f, 0.7f, 0.7f, size: 0.95f));
            return l.ToArray();
        }

        static void Cluster(List<SpotRow> l, string id, float cx, float cz, float yaw, params string[] pieces)
        {
            float[,] o = { { -1.0f, 0f }, { 0f, 0.05f }, { 1.0f, 0f }, { -0.5f, 1.0f }, { 0.5f, 1.0f } };
            double r = yaw * Math.PI / 180.0; float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            for (int i = 0; i < pieces.Length; i++)
            {
                float lx = o[i, 0], lz = o[i, 1];
                bool barrel = pieces[i].StartsWith("Barrel"), crate = pieces[i].StartsWith("Crate");
                l.Add(P(id + "_" + i, "S4", pieces[i], StageRebuilt, cx + lx * c + lz * s, cz - lx * s + lz * c, yaw + 37f * i, barrel ? 0.65f : crate ? 0.7f : 0.5f, barrel ? 0.65f : crate ? 0.7f : 0.45f));
            }
        }

        public static bool SpotInPreset(SpotRow r, DecoPreset p) => r.Stage == Always || (r.Stage == StageRuined && p == DecoPreset.Ruined) || (r.Stage == StageRebuilt && p == DecoPreset.Rebuilt);

        public static Footprint FootprintOf(SpotRow r) => new Footprint(new TownPoint(r.X, r.Z), r.SizeX, r.SizeZ, r.Yaw);

        // The B4 rules: unique ids, positive footprints; no solid row on a path node or across an edge (0.25 m gap); none in a foundation or landmark unless anchored there; none in the
        // approved beauty corner; no overlap with another solid row or a square decoration of a stage that can be on screen together (Frame stands on the Ruined bakery).
        public static List<string> ValidateSpots(TownLayout t) => ValidateSpots(t, Spots);

        public static List<string> ValidateSpots(TownLayout t, IReadOnlyList<SpotRow> rows)
        {
            var errors = new List<string>(); var ids = new HashSet<string>();
            foreach (SpotRow r in rows)
            {
                if (!ids.Add(r.Id)) errors.Add("duplicate spot id " + r.Id);
                if (r.SizeX <= 0f || r.SizeZ <= 0f) { errors.Add(r.Id + ": footprint has no area"); continue; }
                if (string.IsNullOrEmpty(r.Piece)) errors.Add(r.Id + ": no piece");
                if (r.Loose) continue;
                Footprint fp = FootprintOf(r), pad = new Footprint(new TownPoint(r.X, r.Z), r.SizeX + 0.5f, r.SizeZ + 0.5f, r.Yaw);
                foreach (PathNode n in t.Nodes) if (pad.Contains(new TownPoint(n.Pos.X, n.Pos.Z))) errors.Add(r.Id + " covers path node " + n.Id);
                foreach (PathEdge e in t.Edges)
                {
                    PathNode a = t.Node(e.A), b = t.Node(e.B);
                    if (a != null && b != null && pad.SegmentCrosses(a.Pos, b.Pos)) errors.Add(r.Id + " blocks path edge " + e.A + "-" + e.B);
                }
                foreach (Foundation f in t.Foundations) if (r.Anchor != f.Id && fp.Overlaps(f.Footprint)) errors.Add(r.Id + " overlaps foundation " + f.Id);
                foreach (Landmark lm in t.Landmarks) if (r.Anchor != lm.Id && fp.Overlaps(lm.Footprint)) errors.Add(r.Id + " overlaps landmark " + lm.Id);
                if (fp.Overlaps(CornerRect)) errors.Add(r.Id + " stands in the approved beauty corner");
                foreach (SpotRow o in rows)
                {
                    if (string.CompareOrdinal(o.Id, r.Id) <= 0 || o.Loose || o.SizeX <= 0f || o.SizeZ <= 0f) continue;
                    if (StagesMeet(r.Stage, o.Stage) && fp.Overlaps(FootprintOf(o))) errors.Add(r.Id + " overlaps " + o.Id);
                }
                foreach (DecoRow d in Rows)
                    if (StagesMeet(r.Stage, d.Preset == DecoPreset.Ruined ? StageRuined : d.Preset == DecoPreset.Rebuilt ? StageRebuilt : Always) && fp.Overlaps(FootprintOf(d))) errors.Add(r.Id + " overlaps decoration " + d.Id);
            }
            return errors;
        }

        // Frame stands on the Ruined bakery (the frame replaces the ruin's footing): it meets Ruined and Always, never Rebuilt.
        static bool StagesMeet(int a, int b) => a == Always || b == Always || a == b || (a == StageFrame && b == StageRuined) || (b == StageFrame && a == StageRuined);

        public static bool InPreset(DecoPreset p, DecoPreset wanted) => p == DecoPreset.Both || p == wanted;

        public static Footprint FootprintOf(DecoRow r) => new Footprint(new TownPoint(r.X, r.Z), r.SizeX, r.SizeZ, r.Yaw);

        static Footprint Inflated(DecoRow r, float by) => new Footprint(new TownPoint(r.X, r.Z), r.SizeX + 2f * by, r.SizeZ + 2f * by, r.Yaw);

        // Every broken rule as one line (empty = valid).
        public static List<string> Validate(TownLayout t) => Validate(t, Rows, Bunting);

        public static List<string> Validate(TownLayout t, IReadOnlyList<DecoRow> rows, IReadOnlyList<BuntingSpan> bunting)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (DecoRow r in rows)
            {
                if (!ids.Add(r.Id)) errors.Add("duplicate decoration id " + r.Id);
                if (r.SizeX <= 0f || r.SizeZ <= 0f) { errors.Add(r.Id + ": footprint has no area"); continue; }
                Footprint fp = FootprintOf(r), pad = Inflated(r, Clearance);
                foreach (PathNode n in t.Nodes) if (pad.Contains(new TownPoint(n.Pos.X, n.Pos.Z))) errors.Add(r.Id + " covers path node " + n.Id);
                foreach (PathEdge e in t.Edges)
                {
                    PathNode a = t.Node(e.A), b = t.Node(e.B);
                    if (a != null && b != null && pad.SegmentCrosses(a.Pos, b.Pos)) errors.Add(r.Id + " blocks path edge " + e.A + "-" + e.B);
                }
                foreach (Foundation f in t.Foundations) if (pad.Overlaps(f.Footprint)) errors.Add(r.Id + " overlaps foundation " + f.Id);
                foreach (Landmark l in t.Landmarks)
                    if ((l.Kind == LandmarkKind.Stall ? Inflated(r, StallMargin) : pad).Overlaps(l.Footprint)) errors.Add(r.Id + " overlaps landmark " + l.Id);
                if (fp.Overlaps(CornerRect)) errors.Add(r.Id + " stands in the approved beauty corner");
                foreach (TownPoint c in fp.Corners()) if (!OnSquareOrStub(t, c)) { errors.Add(r.Id + " is off the square and its lane stubs"); break; }
                foreach (DecoRow o in rows)
                {
                    if (string.CompareOrdinal(o.Id, r.Id) <= 0 || o.SizeX <= 0f || o.SizeZ <= 0f) continue;
                    if (SharesPreset(r.Preset, o.Preset) && fp.Overlaps(FootprintOf(o))) errors.Add(r.Id + " overlaps " + o.Id);
                }
            }
            foreach (BuntingSpan b in bunting)
            {
                if (!ids.Add(b.Id)) errors.Add("duplicate decoration id " + b.Id);
                if (b.AH < MinBuntingHeight || b.BH < MinBuntingHeight) errors.Add(b.Id + ": an end is lower than " + MinBuntingHeight + " m");
                if (b.Length > MaxBuntingSpan) errors.Add(b.Id + ": span " + b.Length.ToString("0.0") + " m is over " + MaxBuntingSpan + " m");
                if (b.Length < 0.5f) errors.Add(b.Id + ": span is too short");
            }
            return errors;
        }

        static bool SharesPreset(DecoPreset a, DecoPreset b) => a == DecoPreset.Both || b == DecoPreset.Both || a == b;

        static bool OnSquareOrStub(TownLayout t, TownPoint p)
        {
            if (p.X >= HollowmereLayout.SquareMinX - SquareMargin && p.X <= HollowmereLayout.SquareMaxX + SquareMargin
                && p.Z >= HollowmereLayout.SquareMinZ - SquareMargin && p.Z <= HollowmereLayout.SquareMaxZ + SquareMargin) return true;
            foreach (Lane lane in t.Lanes)
                if (HollowmereLayout.DistanceToPolyline(p, lane.Points) <= lane.Width * 0.5f + SquareMargin) return true;
            return false;
        }
    }
}
