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
        };

        public static readonly BuntingSpan[] Bunting =
        {
            new BuntingSpan { Id = "Bunting_Banners", Preset = Rb, AX = -3.2f, AZ = -19.6f, AH = 4.2f, BX = 3.2f, BZ = -19.6f, BH = 4.2f, Sag = 0.55f },
            new BuntingSpan { Id = "Bunting_WestLamp", Preset = Rb, AX = -9.5f, AZ = -18.8f, AH = 2.9f, BX = -3.2f, BZ = -19.6f, BH = 4.0f, Sag = 0.45f },
            new BuntingSpan { Id = "Bunting_EastLamp", Preset = Rb, AX = 3.2f, AZ = -19.6f, AH = 4.0f, BX = 9.5f, BZ = -18.8f, BH = 2.9f, Sag = 0.45f },
            new BuntingSpan { Id = "Bunting_Inn", Preset = Rb, AX = 21f, AZ = -3f, AH = 4.5f, BX = 15f, BZ = -3.5f, BH = 2.9f, Sag = 0.5f },
            new BuntingSpan { Id = "Bunting_WestLane", Preset = Rb, AX = -15f, AZ = -3f, AH = 2.9f, BX = -15f, BZ = -13f, BH = 2.9f, Sag = 0.6f },
            new BuntingSpan { Id = "Bunting_EastLane", Preset = Rb, AX = 15f, AZ = -3.5f, AH = 2.9f, BX = 15f, BZ = -11f, BH = 2.9f, Sag = 0.5f },
            new BuntingSpan { Id = "Bunting_Torn", Preset = Ru, AX = -3.2f, AZ = -19.6f, AH = 3.6f, BX = -6.5f, BZ = -19.9f, BH = 2.7f, Sag = 0.9f, Torn = true },
        };

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
