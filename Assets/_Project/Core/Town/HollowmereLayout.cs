// Core/Town/HollowmereLayout.cs: the Hollowmere town plan as data (Wave 0 of docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md,
// section 4; STORY 8.1-8.3; PHASE1 7.1). ONE place to edit the town: foundations, landmarks, lanes, the NPC path graph and the ground heights.
// Same frame as the beauty corner (BeautyCornerTable): metres, +Z north, origin = the dry fountain's centre.
//
// The plan, a castle village on a hill above a lake (user direction, 2026-10-30):
//  - Ring 1, the square: a 40 x 28 m plaza (x -20..20, z -21..7) on the hilltop shelf around the fountain, 10 old foundations on its edges (3 L, 4 M,
//    3 S), three fixed market stalls in a crescent round the fountain. The approved corner keeps its place: its facade stands on F01, its stall is Stall_1.
//  - The keep, on the high north side: a 4 m stone terrace (retaining wall at z 19.6) carrying the bell tower and the heroes' bell-loft annex; the sealed
//    tithe gate is in the terrace wall under the tower, at the end of a short paved approach from the square. The heroes' cistern house stands at the square's
//    north-east corner against the terrace (Undercroft stair inside), joined to the annex above, so the home is the old stone of the keep.
//  - Ring 2, the lanes: four stubs leave the square (west, south "Market Street", east, north-east) and a north-west ginnel; they meet the Ring Lane, a
//    contour lane round the hill about 12 m out from the square edge. Houses pack both sides of it, doors to the lane; the south-east bend is left for gardens.
//  - The old town wall rings the hill about 28 m out (ring 2 / ring 3 edge). The Lower Lane drops from the Ring Lane to the Lower Gate in the south-west wall,
//    where the road leaves for Saltwake along the lake; the Mill Lane leaves by the east gate to the mill on the lake stream. The lake lies south, below.
// Ring 1 rows are written out by hand; ring 2 and 3 rows are placed along their lane (Along: arc length, side, class) so editing a lane moves its houses.
// Authority: none (static layout data). The builder fails the build on any TownLayout.Validate() line; HollowmereLayoutTests cover the rules.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class HollowmereLayout
    {
        // ------------------------------------------------------------------------------------------------ fixed numbers
        public const float SquareMinX = -20f, SquareMaxX = 20f, SquareMinZ = -21f, SquareMaxZ = 7f;
        public const float KeepWallZ = 19.6f, KeepHeight = 4f, KeepHalfWidth = 24f;   // the keep terrace: north of KeepWallZ, |x| < KeepHalfWidth
        public const float WaterY = -10.5f;                                            // the lake surface
        public static readonly TownPoint UndercroftStair = new TownPoint(15f, 12.5f); // inside the cistern house, the way down (P25)
        public static readonly TownPoint LakeCentre = new TownPoint(10f, -106f);
        public const float LakeRadiusX = 72f, LakeRadiusZ = 27f;
        const float LotSetback = 1.0f;   // lane edge to lot edge

        // Plateau (ring 1 shelf) for the ground: x -34..34, south to z -36, open to the north, south corners rounded (radius 10).
        const float PlateauHalfX = 34f, PlateauMinZ = -36f, PlateauCorner = 10f;
        // Lines drawn by the builder (greybox walls, no path rule): the old town wall and the ring 1 terrace edge, as offsets from the plateau.
        public const float TownWallOffset = 28f, TerraceOffset = 0.5f;

        // ------------------------------------------------------------------------------------------------ ground
        // Natural ground height (no lot pads): the shelf at 0, falling away round the hill to the low meadow (-9.5), the keep terrace stepping up 4 m in the
        // north, the hill rising behind it, and the lake basin dipping under the water in the south.
        public static float NaturalGround(float x, float z)
        {
            float e = PlateauDistance(x, z);
            float fall = e < 4f ? -0.25f * e * e / 8f : -0.25f * (e - 2f);
            fall = SoftMax(fall, -9.5f, 1.5f);
            float ax = Math.Abs(x);
            float keep = KeepHeight * Smooth(KeepWallZ - 0.4f, KeepWallZ + 0.4f, z) * (1f - Smooth(KeepHalfWidth, KeepHalfWidth + 6f, ax));
            float hill = 0.12f * Math.Max(0f, z - 32f) * (1f - Smooth(34f, 60f, ax));
            float lx = (x - LakeCentre.X) / LakeRadiusX, lz = (z - LakeCentre.Z) / LakeRadiusZ;
            double th = Math.Atan2(lz, lx);   // a ragged shore, not an oval
            float shore = 1f + 0.07f * (float)Math.Sin(3 * th + 1.0) + 0.05f * (float)Math.Sin(5 * th + 2.3) + 0.03f * (float)Math.Sin(9 * th);
            float lake = -4.5f * (1f - Smooth(0.75f, 1.0f, (float)Math.Sqrt(lx * lx + lz * lz) / shore));
            return fall + keep + hill + lake;
        }

        // Distance outside the plateau (0 inside); offsets of this give the terrace edge and the town wall lines.
        public static float PlateauDistance(float x, float z)
        {
            float dx = Math.Max(0f, Math.Abs(x) - (PlateauHalfX - PlateauCorner));
            float dz = Math.Max(0f, (PlateauMinZ + PlateauCorner) - z);
            if (dz <= 0f) return Math.Max(0f, Math.Abs(x) - PlateauHalfX);
            if (dx <= 0f) return Math.Max(0f, PlateauMinZ - z);
            return Math.Max(0f, (float)Math.Sqrt(dx * dx + dz * dz) - PlateauCorner);
        }

        // A closed-ish polyline at a fixed distance outside the plateau, from the north end of the west side round the south to the east side.
        public static TownPoint[] OffsetLine(float offset, float northZ, float step = 2f)
        {
            var pts = new List<TownPoint>();
            float hx = PlateauHalfX + offset, r = PlateauCorner + offset, cz = PlateauMinZ + PlateauCorner, cx = PlateauHalfX - PlateauCorner;
            for (float z = northZ; z > cz; z -= step) pts.Add(new TownPoint(-hx, z));
            int arcSteps = Math.Max(4, (int)(r * Math.PI / 2 / step));
            for (int i = 0; i <= arcSteps; i++) { double a = Math.PI + i * (Math.PI / 2) / arcSteps; pts.Add(new TownPoint(-cx + r * (float)Math.Cos(a), cz + r * (float)Math.Sin(a))); }
            for (float x = -cx + step; x < cx; x += step) pts.Add(new TownPoint(x, PlateauMinZ - offset));
            for (int i = 0; i <= arcSteps; i++) { double a = 1.5 * Math.PI + i * (Math.PI / 2) / arcSteps; pts.Add(new TownPoint(cx + r * (float)Math.Cos(a), cz + r * (float)Math.Sin(a))); }
            for (float z = cz + step; z <= northZ; z += step) pts.Add(new TownPoint(hx, z));
            return pts.ToArray();
        }

        // The pieces of a greybox wall line that stay clear of the town: a piece is dropped where a path edge crosses it (lanes and stairs get their
        // opening), where it comes within `clearance` of a foundation (lots keep their yard), or where it runs into a landmark (the tithe gate and the
        // gate piers stand in the gap). Walls are scenery: they are not path blockers.
        public static List<(TownPoint A, TownPoint B)> WallRun(TownLayout t, TownPoint[] line, float clearance)
        {
            var run = new List<(TownPoint, TownPoint)>();
            for (int i = 1; i < line.Length; i++)
            {
                TownPoint a = line[i - 1], b = line[i], mid = (a + b) * 0.5f;
                bool keep = true;
                foreach (Foundation f in t.Foundations)
                    if (f.Footprint.Contains(a, clearance) || f.Footprint.Contains(b, clearance) || f.Footprint.Contains(mid, clearance)) { keep = false; break; }
                if (keep) foreach (Landmark l in t.Landmarks) if (l.Footprint.Contains(a) || l.Footprint.Contains(b) || l.Footprint.Contains(mid) || l.Footprint.SegmentCrosses(a, b)) { keep = false; break; }
                if (keep)
                    foreach (PathEdge e in t.Edges)
                    {
                        PathNode na = t.Node(e.A), nb = t.Node(e.B);
                        if (na != null && nb != null && TownLayout.SegmentsCross(a, b, na.Pos, nb.Pos)) { keep = false; break; }
                    }
                if (keep) run.Add((a, b));
            }
            return run;
        }

        public static TownPoint[] KeepWallLine(float step = 2f)
        {
            var pts = new List<TownPoint>();
            for (float x = -KeepHalfWidth; x <= KeepHalfWidth + 1e-3f; x += step) pts.Add(new TownPoint(x, KeepWallZ));
            return pts.ToArray();
        }

        // Ground with every lot and landmark pad flattened to its own height (blended over PadBlend metres): the ground mesh and the builder use this.
        public const float PadBlend = 3f;
        public static float Ground(TownLayout layout, float x, float z)
        {
            float g = NaturalGround(x, z), wSum = 0f, hSum = 0f;
            var p = new TownPoint(x, z);
            foreach (Foundation f in layout.Foundations) Pad(f.Footprint, f.Y, 1.0f, p, ref wSum, ref hSum);
            foreach (Landmark l in layout.Landmarks) if (l.Kind != LandmarkKind.TitheGate) Pad(l.Footprint, l.Y, 1.0f, p, ref wSum, ref hSum);
            if (wSum <= 0f) return g;
            float w = Math.Min(1f, wSum);
            return g * (1f - w) + (hSum / wSum) * w;
        }

        static void Pad(Footprint fp, float y, float margin, TownPoint p, ref float wSum, ref float hSum)
        {
            TownPoint l = fp.ToLocal(p);
            float ox = Math.Abs(l.X) - fp.HalfX - margin, oz = Math.Abs(l.Z) - fp.HalfZ - margin;
            float d = (float)Math.Sqrt(Math.Max(0f, ox) * Math.Max(0f, ox) + Math.Max(0f, oz) * Math.Max(0f, oz));
            if (d >= PadBlend) return;
            float w = 1f - Smooth(0f, PadBlend, d);
            wSum += w; hSum += w * y;
        }

        static float Smooth(float a, float b, float x) { float t = Math.Max(0f, Math.Min(1f, (x - a) / (b - a))); return t * t * (3f - 2f * t); }
        static float SoftMax(float a, float b, float k) { float h = Math.Max(0f, Math.Min(1f, 0.5f + 0.5f * (a - b) / k)); return b + (a - b) * h + k * h * (1f - h) * 0.5f; }

        // ------------------------------------------------------------------------------------------------ lanes
        static readonly (string Id, string Name, int Ring, float Width, float[] Xz)[] LaneRows =
        {
            ("RingLane", "Ring Lane", 2, 4.0f, new[] { -46f, 12f, -45.5f, 2f, -46.5f, -8f, -46f, -17f, -45.8f, -26f, -44.3f, -34.4f, -39.6f, -41.6f, -32.4f, -46.3f, -24f, -48f,
                -12f, -48.8f, 0f, -49.3f, 12f, -48.6f, 24f, -48f, 32.4f, -46.3f, 39.6f, -41.6f, 44.3f, -34.4f, 46f, -26f, 46.5f, -17f, 45.6f, -8f, 46f, 2f, 45.5f, 12f }),
            ("WestStub", "West Lane", 2, 4.5f, new[] { -19.4f, -7.25f, -27f, -7.25f, -34f, -7.6f, -40f, -8.3f, -46.5f, -8f }),
            ("MarketStreet", "Market Street", 2, 5.0f, new[] { 0f, -20.6f, 0f, -28f, -0.5f, -36f, -0.6f, -43f, 0f, -49.3f }),
            ("EastStub", "East Lane", 2, 4.5f, new[] { 19.6f, -16f, 27f, -16.2f, 34f, -16.5f, 40f, -17f, 46.5f, -17f }),
            ("NorthEastStub", "Keep Lane", 2, 4.0f, new[] { 18.6f, 6.1f, 19f, 10.5f, 27f, 11.5f, 36f, 11f, 45.5f, 12f }),
            ("NorthWestGinnel", "Wall Ginnel", 2, 2.5f, new[] { -19.4f, 6.1f, -19.25f, 15f, -30f, 16.5f, -38f, 14.5f, -46f, 12f }),
            ("LowerLane", "Lower Lane", 2, 3.5f, new[] { -12f, -48.8f, -15f, -55f, -18.5f, -60f, -20.5f, -63f, -24f, -70f }),
            ("SaltwakeRoad", "Saltwake Road", 3, 5.0f, new[] { -24f, -70f, -36f, -76f, -55f, -79f, -80f, -80f, -110f, -81f }),
            ("MillLane", "Mill Lane", 3, 3.5f, new[] { 44.3f, -34.4f, 51f, -38.5f, 55.5f, -44.5f, 59.5f, -50f, 63.6f, -54f }),
        };

        // The lake stream the mill wheel sits in (drawn as a water strip; not a lane).
        public static readonly TownPoint[] Stream =
        {
            new TownPoint(130f, -15f), new TownPoint(100f, -35f), new TownPoint(82f, -50f), new TownPoint(75.5f, -60f), new TownPoint(70f, -72f), new TownPoint(64f, -84f), new TownPoint(58f, -94f),
        };

        // ------------------------------------------------------------------------------------------------ foundations
        // Ring 1: written out. (id, size, centre x, z, yaw, facing). F01 carries the approved corner's facade (front on z 7.5, x -5..2.5); F02 is the L nearest
        // the fountain (its nearest edge is 10 m from the fountain, for the baker's "near the fountain" wish).
        static readonly (string Id, FoundationSize Size, float X, float Z, float Yaw, LotSide Facing)[] Ring1 =
        {
            ("F01", FoundationSize.M, -1.25f, 11.2f, 0f, LotSide.South),
            ("F02", FoundationSize.L, -12.5f, 12.7f, 0f, LotSide.South),
            ("F03", FoundationSize.L, -26.5f, 0f, 0f, LotSide.East),
            ("F04", FoundationSize.S, -23.5f, 10.3f, 0f, LotSide.South),
            ("F05", FoundationSize.M, -25f, -13f, 0f, LotSide.East),
            ("F06", FoundationSize.M, -10f, -26f, 0f, LotSide.North),
            ("F07", FoundationSize.M, 8f, -26f, 0f, LotSide.North),
            ("F08", FoundationSize.S, 17f, -25f, 0f, LotSide.North),
            ("F09", FoundationSize.L, 26.5f, -6f, 0f, LotSide.West),
            ("F10", FoundationSize.S, 24f, 4.5f, 0f, LotSide.West),
        };

        // Rings 2 and 3: (id, lane, arc length along the lane in metres, side +1 = right of the lane's direction / -1 = left, size).
        // Ring Lane runs from its north-west end south, east and back north, so +1 is the downhill (wall) side and -1 the uphill (square) side.
        static readonly (string Id, int Ring, string Lane, float S, int Side, FoundationSize Size)[] AlongRows =
        {
            ("R2_01", 2, "RingLane", 5f, -1, FoundationSize.M),
            ("R2_02", 2, "RingLane", 32f, -1, FoundationSize.M),
            ("R2_03", 2, "RingLane", 47f, -1, FoundationSize.M),
            ("R2_04", 2, "RingLane", 62f, -1, FoundationSize.M),
            ("R2_05", 2, "RingLane", 80f, -1, FoundationSize.L),
            ("R2_06", 2, "RingLane", 110f, -1, FoundationSize.M),
            ("R2_07", 2, "RingLane", 124f, -1, FoundationSize.S),
            ("R2_08", 2, "RingLane", 178f, -1, FoundationSize.M),
            ("R2_09", 2, "RingLane", 189f, -1, FoundationSize.S),
            ("R2_10", 2, "RingLane", 3f, 1, FoundationSize.S),
            ("R2_11", 2, "RingLane", 14f, 1, FoundationSize.M),
            ("R2_12", 2, "RingLane", 28f, 1, FoundationSize.L),
            ("R2_13", 2, "RingLane", 42f, 1, FoundationSize.M),
            ("R2_14", 2, "RingLane", 56f, 1, FoundationSize.M),
            ("R2_15", 2, "RingLane", 66f, 1, FoundationSize.M),
            ("R2_16", 2, "RingLane", 100f, 1, FoundationSize.L),
            ("R2_17", 2, "RingLane", 114f, 1, FoundationSize.M),
            ("R2_18", 2, "RingLane", 126f, 1, FoundationSize.S),
            ("R2_19", 2, "RingLane", 158f, 1, FoundationSize.M),
            ("R2_20", 2, "RingLane", 172f, 1, FoundationSize.L),
            ("R2_21", 2, "RingLane", 187f, 1, FoundationSize.M),
            ("R2_22", 2, "LowerLane", 12f, 1, FoundationSize.S),
            ("R2_23", 2, "LowerLane", 10f, -1, FoundationSize.M),
            ("R3_01", 3, "SaltwakeRoad", 22f, -1, FoundationSize.M),
            ("R3_02", 3, "SaltwakeRoad", 34f, 1, FoundationSize.S),
            ("R3_03", 3, "MillLane", 10f, -1, FoundationSize.S),
        };

        // ------------------------------------------------------------------------------------------------ landmarks
        // (id, name, kind, centre x, z, size x, z, yaw, height, door node position or NaN). Stall yaws turn each counter (local +Z) to the fountain.
        static readonly (string Id, string Name, LandmarkKind Kind, float X, float Z, float SX, float SZ, float Yaw, float H, float DoorX, float DoorZ, float Y)[] LandmarkRows =
        {
            ("DryFountain", "Dry fountain", LandmarkKind.Fountain, 0f, 0f, 3.4f, 3.4f, 0f, 2.5f, float.NaN, float.NaN, 0f),
            ("Stall_1", "Market stall (produce)", LandmarkKind.Stall, 4.5f, 4.5f, 2.8f, 1.8f, 200f, 2.7f, float.NaN, float.NaN, 0f),
            ("Stall_2", "Market stall (cloth)", LandmarkKind.Stall, -7f, -3f, 2.8f, 1.8f, 66.8f, 2.7f, float.NaN, float.NaN, 0f),
            ("Stall_3", "Market stall (tools)", LandmarkKind.Stall, 5.5f, -8.5f, 2.8f, 1.8f, -32.9f, 2.7f, float.NaN, float.NaN, 0f),
            ("BellTower", "Bell tower", LandmarkKind.BellTower, 5.6f, 24f, 6.5f, 6.5f, 0f, 17f, 5.6f, 28.0f, KeepHeight),
            ("TitheGate", "Sealed tithe gate", LandmarkKind.TitheGate, 5.6f, KeepWallZ, 5f, 1.2f, 0f, 4.5f, 5.6f, 18.4f, 0f),
            ("HeroesHome_Cistern", "Heroes' home: cistern house", LandmarkKind.HeroesHome, 12.5f, 11.25f, 9f, 7.5f, 0f, 6.5f, 12.5f, 6.75f, 0f),
            ("HeroesHome_Annex", "Heroes' home: bell-loft annex", LandmarkKind.HeroesHome, 11.9f, 24f, 5.5f, 5f, 0f, 7f, 15.3f, 24f, KeepHeight),
            ("Mill", "Mill", LandmarkKind.Mill, 69.5f, -58.5f, 9f, 7f, 30f, 8f, 64.6f, -55.9f, float.NaN),
            ("LowerGate_W", "Lower Gate (west pier)", LandmarkKind.TownGate, -24.6f, -64.6f, 2.4f, 2.4f, 0f, 5f, float.NaN, float.NaN, float.NaN),
            ("LowerGate_E", "Lower Gate (east pier)", LandmarkKind.TownGate, -18.0f, -64.6f, 2.4f, 2.4f, 0f, 5f, float.NaN, float.NaN, float.NaN),
            ("EastGate_N", "East Gate (north pier)", LandmarkKind.TownGate, 59.2f, -44.1f, 2.4f, 2.4f, 37f, 5f, float.NaN, float.NaN, float.NaN),
            ("EastGate_S", "East Gate (south pier)", LandmarkKind.TownGate, 54.0f, -47.9f, 2.4f, 2.4f, 37f, 5f, float.NaN, float.NaN, float.NaN),
        };

        // ------------------------------------------------------------------------------------------------ path graph pieces
        // The square's outer loop (just inside the frontages) and the fountain loop (radius 4.2, outside the 5.8 m flagstone ring's basin).
        static readonly float[] SquareLoop =
        {
            -19.4f, 6.1f, -12.5f, 6.1f, -6.1f, 6.1f, -1.25f, 6.1f, 5.5f, 6.1f, 12.5f, 6.1f, 18.6f, 6.1f,
            19.6f, 4.5f, 19.6f, 0f, 19.6f, -6f, 19.6f, -16f, 19.6f, -20.6f,
            17f, -20.6f, 8f, -20.6f, 0f, -20.6f, -10f, -20.6f, -19.4f, -20.6f,
            -19.4f, -13f, -19.4f, -7.25f, -19.4f, 0f,
        };
        public const float FountainLoopRadius = 4.2f;
        // Spokes from the fountain loop (index 0 = north, clockwise in 45 degree steps) to square-loop points.
        static readonly (int Fl, float X, float Z)[] Spokes = { (0, -1.25f, 6.1f), (2, 19.6f, 0f), (4, 0f, -20.6f), (6, -19.4f, 0f), (5, -19.4f, -20.6f), (7, -12.5f, 6.1f) };
        // The keep: stair from the square's north-east corner up the terrace wall, the terrace walk to the annex and round the tower to its door.
        static readonly (string Id, float X, float Z, float Y, PathNodeKind Kind)[] KeepNodes =
        {
            ("Keep_StairFoot", 19.2f, 14.0f, 0f, PathNodeKind.Stair),
            ("Keep_StairHead", 19.2f, 20.6f, KeepHeight, PathNodeKind.Stair),
            ("Keep_Walk_E", 17.8f, 28.6f, KeepHeight, PathNodeKind.Lane),
            ("Keep_Walk_N", 5.6f, 29.2f, KeepHeight, PathNodeKind.Lane),
        };
        static readonly (string A, string B)[] KeepEdges =
        {
            ("NorthEastStub_1", "Keep_StairFoot"), ("Keep_StairFoot", "Keep_StairHead"), ("Keep_StairHead", "HeroesHome_Annex_Door"),
            ("Keep_StairHead", "Keep_Walk_E"), ("Keep_Walk_E", "Keep_Walk_N"), ("Keep_Walk_N", "BellTower_Door"),
        };

        // ------------------------------------------------------------------------------------------------ the rebuilt preset (ring 1 assignment; rings 2-3 homes)
        public enum BuildingKind : byte { Empty, Home, Bakery, Inn, ApprovedFacade, Garden }
        static readonly (string Foundation, BuildingKind Kind)[] RebuiltRing1 =
        {
            ("F01", BuildingKind.ApprovedFacade), ("F02", BuildingKind.Bakery), ("F09", BuildingKind.Inn),
            ("F04", BuildingKind.Home), ("F05", BuildingKind.Home), ("F07", BuildingKind.Home), ("F08", BuildingKind.Home),
            ("F03", BuildingKind.Empty), ("F06", BuildingKind.Empty), ("F10", BuildingKind.Empty),
        };
        static readonly string[] RebuiltGardens = { "R2_07", "R2_18", "R2_22", "R3_02" };   // left as allotments in the rebuilt massing

        public static BuildingKind RebuiltBuilding(string foundationId)
        {
            foreach (var r in RebuiltRing1) if (r.Foundation == foundationId) return r.Kind;
            if (Array.IndexOf(RebuiltGardens, foundationId) >= 0) return BuildingKind.Garden;
            return foundationId.StartsWith("F", StringComparison.Ordinal) ? BuildingKind.Empty : BuildingKind.Home;
        }

        // ------------------------------------------------------------------------------------------------ build the layout
        public const string RootNodeId = "FL_0";

        public static TownLayout Create()
        {
            var t = new TownLayout { RootNode = RootNodeId };
            foreach (var l in LaneRows)
            {
                var pts = new TownPoint[l.Xz.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = new TownPoint(l.Xz[2 * i], l.Xz[2 * i + 1]);
                t.Lanes.Add(new Lane { Id = l.Id, Name = l.Name, Ring = l.Ring, Width = l.Width, Points = pts });
            }

            char[] variants = { 'a', 'b', 'c' };
            int vi = 0;
            foreach (var r in Ring1)
                t.Foundations.Add(new Foundation { Id = r.Id, Ring = 1, Size = r.Size, Centre = new TownPoint(r.X, r.Z), Yaw = r.Yaw, Facing = r.Facing, RuinVariant = Variant(r.Size, variants[vi++ % 3]) });
            foreach (var r in AlongRows)
            {
                Lane lane = FindLane(t, r.Lane);
                PointAlong(lane.Points, r.S, out TownPoint p, out TownPoint dir);
                var right = new TownPoint(dir.Z, -dir.X);
                TownPoint n = right * r.Side;   // from the lane toward the lot
                float half = Foundation.SideLength(r.Size) * 0.5f;
                TownPoint c = p + n * (lane.Width * 0.5f + LotSetback + half);
                float yaw = (float)(Math.Atan2(n.X, n.Z) * 180.0 / Math.PI);   // lot +Z points away from the lane, so its South side faces it
                t.Foundations.Add(new Foundation { Id = r.Id, Ring = r.Ring, Size = r.Size, Centre = c, Yaw = (float)Math.Round(yaw, 2), Facing = LotSide.South, RuinVariant = Variant(r.Size, variants[vi++ % 3]) });
            }
            foreach (Foundation f in t.Foundations) f.Y = Round(NaturalGround(f.Centre.X, f.Centre.Z));

            foreach (var r in LandmarkRows)
            {
                var lm = new Landmark { Id = r.Id, Name = r.Name, Kind = r.Kind, Footprint = new Footprint(new TownPoint(r.X, r.Z), r.SX, r.SZ, r.Yaw), Height = r.H };
                lm.Y = float.IsNaN(r.Y) ? Round(NaturalGround(r.X, r.Z)) : r.Y;
                t.Landmarks.Add(lm);
            }

            BuildGraph(t);
            return t;
        }

        static string Variant(FoundationSize s, char v) => "Ruin_" + s + "_" + v;
        static float Round(float v) => (float)Math.Round(v, 2);

        public static Lane FindLane(TownLayout t, string id)
        {
            foreach (Lane l in t.Lanes) if (l.Id == id) return l;
            throw new ArgumentException("no lane " + id);
        }

        public static float LaneLength(TownPoint[] pts) { float s = 0f; for (int i = 1; i < pts.Length; i++) s += TownPoint.Distance(pts[i - 1], pts[i]); return s; }

        public static void PointAlong(TownPoint[] pts, float s, out TownPoint p, out TownPoint dir)
        {
            for (int i = 1; i < pts.Length; i++)
            {
                float len = TownPoint.Distance(pts[i - 1], pts[i]);
                if (s <= len || i == pts.Length - 1)
                {
                    dir = (pts[i] - pts[i - 1]) * (1f / len);
                    p = pts[i - 1] + dir * Math.Min(s, len);
                    return;
                }
                s -= len;
            }
            throw new ArgumentException("a lane needs two points");
        }

        // ------------------------------------------------------------------------------------------------ graph
        static void BuildGraph(TownLayout t)
        {
            var blockers = new List<Footprint>();
            foreach (Foundation f in t.Foundations) blockers.Add(f.Footprint);
            foreach (Landmark l in t.Landmarks) blockers.Add(l.Footprint);

            // Fountain loop.
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                AddNode(t, "FL_" + i, new TownPoint(FountainLoopRadius * (float)Math.Sin(a), FountainLoopRadius * (float)Math.Cos(a)), PathNodeKind.Loop);
            }
            for (int i = 0; i < 8; i++) t.Edges.Add(new PathEdge("FL_" + i, "FL_" + ((i + 1) % 8)));

            // Square loop.
            int nSq = SquareLoop.Length / 2;
            for (int i = 0; i < nSq; i++) AddNode(t, "SQ_" + i, new TownPoint(SquareLoop[2 * i], SquareLoop[2 * i + 1]), PathNodeKind.Loop);
            for (int i = 0; i < nSq; i++) t.Edges.Add(new PathEdge("SQ_" + i, "SQ_" + ((i + 1) % nSq)));
            foreach (var s in Spokes) t.Edges.Add(new PathEdge("FL_" + s.Fl, NodeAt(t, new TownPoint(s.X, s.Z)).Id));

            // Lane spines: a node per lane point; a point within 0.3 m of an existing node joins it (lane junctions and the square loop).
            foreach (Lane lane in t.Lanes)
            {
                string prev = null;
                for (int i = 0; i < lane.Points.Length; i++)
                {
                    PathNode n = NodeAt(t, lane.Points[i]) ?? AddNode(t, lane.Id + "_" + i, lane.Points[i], PathNodeKind.Lane);
                    if (prev != null) t.Edges.Add(new PathEdge(prev, n.Id));
                    prev = n.Id;
                }
            }

            // Landmark doors and the keep.
            foreach (var r in LandmarkRows)
            {
                if (float.IsNaN(r.DoorX)) continue;
                string id = r.Id + "_Door";
                float y = r.Kind == LandmarkKind.BellTower || r.Id == "HeroesHome_Annex" ? KeepHeight : float.NaN;
                PathNode n = AddNode(t, id, new TownPoint(r.DoorX, r.DoorZ), PathNodeKind.Door, y);
                t.Landmark(r.Id).DoorNode = id;
            }
            foreach (var k in KeepNodes) AddNode(t, k.Id, new TownPoint(k.X, k.Z), k.Kind, k.Y);
            foreach (var e in KeepEdges) t.Edges.Add(new PathEdge(e.A, e.B));
            var network = new List<PathNode>();
            foreach (PathNode n in t.Nodes) if (n.Kind == PathNodeKind.Loop || n.Kind == PathNodeKind.Lane) network.Add(n);
            foreach (Landmark l in t.Landmarks)
                if (l.DoorNode != null && !HasEdge(t, l.DoorNode)) Link(t, t.Node(l.DoorNode), network, blockers, 14f);

            // Each foundation: a doorstep in the middle of each side and a corner node off each corner, linked round the lot; the front doorstep links to the
            // nearest lane or loop node it can see, and any other doorstep within 3.5 m of one links too.
            foreach (Foundation f in t.Foundations)
            {
                float y = f.Y;
                for (int c = 0; c < 4; c++) AddNode(t, f.CornerId(c), f.CornerPoint(c), PathNodeKind.Corner, y);
                foreach (LotSide s in TownLayout.AllSides) AddNode(t, f.DoorstepId(s), f.DoorstepPoint(s), PathNodeKind.Doorstep, y);
                string[] ring = { f.CornerId(0), f.DoorstepId(LotSide.South), f.CornerId(1), f.DoorstepId(LotSide.East), f.CornerId(2), f.DoorstepId(LotSide.North), f.CornerId(3), f.DoorstepId(LotSide.West) };
                for (int i = 0; i < ring.Length; i++) t.Edges.Add(new PathEdge(ring[i], ring[(i + 1) % ring.Length]));
            }
            foreach (Foundation f in t.Foundations)
            {
                Link(t, t.Node(f.DoorstepId(f.Facing)), network, blockers, 14f);
                foreach (LotSide s in TownLayout.AllSides) if (s != f.Facing) Link(t, t.Node(f.DoorstepId(s)), network, blockers, 3.5f);
            }
        }

        static PathNode AddNode(TownLayout t, string id, TownPoint p, PathNodeKind kind, float y = float.NaN)
        {
            var n = new PathNode { Id = id, Pos = p, Kind = kind, Y = float.IsNaN(y) ? Round(NaturalGround(p.X, p.Z)) : y };
            t.Nodes.Add(n);
            return n;
        }

        static PathNode NodeAt(TownLayout t, TownPoint p)
        {
            foreach (PathNode n in t.Nodes) if (TownPoint.Distance(n.Pos, p) < 0.3f) return n;
            return null;
        }

        static bool HasEdge(TownLayout t, string id) { foreach (PathEdge e in t.Edges) if (e.A == id || e.B == id) return true; return false; }

        // Links n to the nearest node of `network` within maxDist whose straight segment crosses no footprint. No candidate leaves n unlinked here:
        // Validate() then reports it unreachable, which is the build failure we want.
        static void Link(TownLayout t, PathNode n, List<PathNode> network, List<Footprint> blockers, float maxDist)
        {
            PathNode best = null; float bestD = maxDist;
            foreach (PathNode m in network)
            {
                if (m == n) continue;
                float d = TownPoint.Distance(n.Pos, m.Pos);
                if (d >= bestD) continue;
                bool clear = true;
                foreach (Footprint fp in blockers) if (fp.SegmentCrosses(n.Pos, m.Pos)) { clear = false; break; }
                if (clear) { best = m; bestD = d; }
            }
            if (best != null) t.Edges.Add(new PathEdge(n.Id, best.Id));
        }
    }
}
