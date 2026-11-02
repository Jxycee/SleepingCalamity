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
        public const float WaterY = -12f;                                              // the lake surface, 12 m below the square
        public static readonly TownPoint UndercroftStair = new TownPoint(15f, 12.5f); // inside the cistern house, the way down (P25)
        public static readonly TownPoint LakeCentre = new TownPoint(10f, -102f);
        public const float LakeRadiusX = 70f, LakeRadiusZ = 24f;
        const float LotSetback = 1.0f, MinLotSetback = 0.2f;   // lane edge to lot edge: the default, and the least a lot may be pulled in to clear the town wall behind it
        // The town wall (TownWallOffset, on its bank) is a hard limit for every lot: the farthest lot corner stays LotWallClearance inside the wall's inner face.
        public const float WallHalfThickness = 0.4f, LotWallClearance = 1.0f;
        public const float LotWallLimit = TownWallOffset - WallHalfThickness - LotWallClearance;   // max PlateauDistance of any lot corner (26.6)
        public const float MinLotLaneClearance = MinLotSetback;                                  // lane edge to lot edge, the floor
        public const float MaxLaneGrade = 0.30f;   // lanes are relaxed to this rise per metre (about 17 deg), under TownLayout.MaxGrade (20 deg)

        // Plateau (ring 1 shelf) for the ground: x -34..34, south to z -36, open to the north, south corners rounded (radius 10).
        const float PlateauHalfX = 34f, PlateauMinZ = -36f, PlateauCorner = 10f;
        // Lines drawn by the builder (greybox walls, no path rule), as offsets from the plateau: the ring 1 terrace edge, the second terrace edge below the
        // square's south side, and the old town wall on its bank.
        public const float TerraceOffset = 0.75f, LowerTerraceOffset = 2.6f, TownWallOffset = 28f;
        public const float TerraceStep = 3f;   // each terrace drops about 3 m

        // ------------------------------------------------------------------------------------------------ ground
        // Natural ground height (before lane corridors and lot pads), a castle hill in terraces:
        //   the square's shelf at 0 (ring 1), the keep terrace 4 m higher on its north side and the hill rising behind it;
        //   one TerraceStep down at the shelf edge (TerraceOffset) for the lanes on the west, east and north flanks;
        //   on the south side a second TerraceStep just below (LowerTerraceOffset), so the Ring Lane spirals down about 3 m from the flanks to the south;
        //   the town wall on a raised bank (offset 26-29.5), a dry ditch outside it (offset 31), then the land falls to the meadow and the lake basin
        //   (water 12 m below the square), with the stream in a ravine running down to the lake.
        public static float NaturalGround(float x, float z)
        {
            float e = PlateauDistance(x, z), ax = Math.Abs(x);
            float south = Smooth(-24f, -40f, z);   // 0 on the north and flanks, 1 on the south slope (the orchard side)
            float y = -0.04f * Math.Min(e, 29.5f) - TerraceStep * Smooth(TerraceOffset - 0.3f, TerraceOffset + 0.3f, e)
                - TerraceStep * south * Smooth(LowerTerraceOffset - 0.3f, LowerTerraceOffset + 0.3f, e);
            y += 1.0f * Smooth(25.5f, 27f, e) * (1f - Smooth(29.5f, 30.5f, e));   // the wall's raised bank
            if (e > 29.5f)
            {
                float rim = y;
                float outer = rim - 2.5f - 0.14f * Math.Max(0f, e - 33f) * (0.5f + 0.5f * south);
                y = rim + (outer - rim) * Smooth(29.5f, 33f, e);
                y = SoftMax(y, -10.5f, 1.2f);
                y -= 3.0f * (1f - Smooth(1.0f, 3.0f, Math.Abs(e - 31f)));   // the dry ditch
            }
            float keep = KeepHeight * Smooth(KeepWallZ - 0.4f, KeepWallZ + 0.4f, z) * (1f - Smooth(KeepHalfWidth, KeepHalfWidth + 6f, ax));
            float hill = 0.14f * Math.Max(0f, z - 32f) * (1f - Smooth(34f, 60f, ax));
            float lx = (x - LakeCentre.X) / LakeRadiusX, lz = (z - LakeCentre.Z) / LakeRadiusZ;
            double th = Math.Atan2(lz, lx);   // a ragged shore, not an oval
            float shore = 1f + 0.07f * (float)Math.Sin(3 * th + 1.0) + 0.05f * (float)Math.Sin(5 * th + 2.3) + 0.03f * (float)Math.Sin(9 * th);
            float lake = -6f * (1f - Smooth(0.7f, 1.0f, (float)Math.Sqrt(lx * lx + lz * lz) / shore));
            float ravine = e > 30f ? -3.5f * (1f - Smooth(1.5f, 7f, DistanceToPolyline(new TownPoint(x, z), Stream))) : 0f;
            return y + keep + hill + Math.Min(lake, 0f) + ravine + OuterFall(x, z);
        }

        // Q15: west of the lake the meadow falls away toward Saltwake, so the road winds down a real hillside into a shallow valley (0 east of x -72, so the
        // town, the lots by the road and the lake are untouched; about FallDepth lower at the terrain's west edge, x -150, with a gentle north-south roll).
        // HollowmereTerrain blends its baked erosion RAW on top of this term (the RAW predates it), and the backdrop ring continues it past the terrain edge.
        public const float FallDepth = 9f, FallStartX = -72f, FallEndX = -150f;
        public static float OuterFall(float x, float z)
        {
            if (x >= FallStartX) return 0f;
            float w = Smooth(FallStartX, FallEndX, x);
            float roll = 1f + 0.16f * (float)Math.Sin(z * 0.043f + 0.6f) + 0.08f * (float)Math.Sin(z * 0.11f + x * 0.03f);
            return -FallDepth * w * roll;
        }

        public static float DistanceToPolyline(TownPoint p, TownPoint[] pts)
        {
            float best = float.MaxValue;
            for (int i = 1; i < pts.Length; i++) best = Math.Min(best, TownPoint.Distance(p, ClosestOnSegment(p, pts[i - 1], pts[i], out _)));
            return best;
        }

        public static TownPoint ClosestOnSegment(TownPoint p, TownPoint a, TownPoint b, out float t)
        {
            TownPoint ab = b - a, ap = p - a;
            float len2 = ab.X * ab.X + ab.Z * ab.Z;
            t = len2 > 0f ? Math.Max(0f, Math.Min(1f, (ap.X * ab.X + ap.Z * ab.Z) / len2)) : 0f;
            return a + ab * t;
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
        // minStep > 0 keeps only pieces where the natural ground steps at least that much across the line (a terrace wall fades out with its terrace).
        public static List<(TownPoint A, TownPoint B)> WallRun(TownLayout t, TownPoint[] line, float clearance, float minStep = 0f)
        {
            var run = new List<(TownPoint, TownPoint)>();
            for (int i = 1; i < line.Length; i++)
            {
                TownPoint a = line[i - 1], b = line[i], mid = (a + b) * 0.5f;
                bool keep = true;
                if (minStep > 0f)
                {
                    TownPoint dir = b - a; float len = dir.Length; var nrm = new TownPoint(dir.Z / len, -dir.X / len);
                    TownPoint p1 = mid + nrm * 1.5f, p2 = mid - nrm * 1.5f;
                    keep = Math.Abs(NaturalGround(p1.X, p1.Z) - NaturalGround(p2.X, p2.Z)) >= minStep;
                }
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

        // Ground with every lane corridor following its relaxed profile and every lot and landmark pad flattened to its own height (blended over PadBlend
        // metres): the ground mesh, the builder's walls and the cameras use this.
        public const float PadBlend = 3f;
        public static float Ground(TownLayout layout, float x, float z)
        {
            float g = NaturalGround(x, z), wSum = 0f, hSum = 0f;
            var p = new TownPoint(x, z);
            // Inside a lot's footprint plus its 1 m yard margin the pad wins outright: lots are flat whatever lane runs past.
            foreach (Foundation f in layout.Foundations) if (f.Footprint.Contains(p, 1.0f)) return f.Y;
            foreach (Lane lane in layout.Lanes)
            {
                if (lane.Heights == null) continue;
                float bestD = float.MaxValue, bestH = 0f;
                for (int i = 1; i < lane.Points.Length; i++)
                {
                    TownPoint c = ClosestOnSegment(p, lane.Points[i - 1], lane.Points[i], out float t);
                    float d = TownPoint.Distance(p, c);
                    if (d < bestD) { bestD = d; bestH = lane.Heights[i - 1] + (lane.Heights[i] - lane.Heights[i - 1]) * t; }
                }
                float od = bestD - (lane.Width * 0.5f + 0.75f);
                if (od >= PadBlend) continue;
                if (Math.Abs(x) < KeepHalfWidth && z > KeepWallZ - 0.6f) continue;   // lanes never pull the keep terrace down
                float w = od <= 0f ? 1f : 1f - Smooth(0f, PadBlend, od);
                wSum += w; hSum += w * bestH;
            }
            foreach (Foundation f in layout.Foundations) Pad(f.Footprint, f.Y, 1.0f, p, ref wSum, ref hSum);
            foreach (Landmark l in layout.Landmarks) if (l.Kind != LandmarkKind.TitheGate && l.Kind != LandmarkKind.TownGate) Pad(l.Footprint, l.Y, 1.0f, p, ref wSum, ref hSum);
            if (wSum <= 0f) return g;
            float wt = Math.Min(1f, wSum);
            return g * (1f - wt) + (hSum / wSum) * wt;
        }

        static void Pad(Footprint fp, float y, float margin, TownPoint p, ref float wSum, ref float hSum)
        {
            // A pad never blends across the keep wall: the tower's terrace must not ramp down into the square, nor a square lot drag the terrace down.
            if (Math.Abs(p.X) < KeepHalfWidth && (p.Z > KeepWallZ) != (fp.Centre.Z > KeepWallZ)) return;
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
            // Q15: past R3_02 the road winds down the western fall (OuterFall) in two easy S-bends and leaves the terrain's west edge (the backdrop ring carries it on).
            ("SaltwakeRoad", "Saltwake Road", 3, 5.0f, new[] { -24f, -70f, -36f, -76f, -55f, -79f, -80f, -80f, -93f, -85f, -104f, -93.5f, -117f, -98.5f, -131f, -95f, -142f, -88.5f, -152f, -86f }),
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
            ("KeepHall", "Keep hall", LandmarkKind.KeepHall, -3.0f, 24.5f, 9f, 6f, 0f, 10f, -9.2f, 25.0f, KeepHeight),   // Q9: art yaw 0, west gable door; the node sits outside its porch (reaches x -8.75)
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
        // The keep: stairs up the terrace wall from the square's north-east corner and from the Wall Ginnel in the west, the terrace walk to the annex and round
        // the tower to its door.
        static readonly (string Id, float X, float Z, float Y, PathNodeKind Kind)[] KeepNodes =
        {
            ("Keep_StairFoot", 19.2f, 14.0f, 0f, PathNodeKind.Stair),
            ("Keep_StairHead", 19.2f, 20.6f, KeepHeight, PathNodeKind.Stair),
            ("Keep_Walk_E", 17.8f, 28.6f, KeepHeight, PathNodeKind.Lane),
            ("Keep_Walk_N", 5.6f, 29.2f, KeepHeight, PathNodeKind.Lane),
            ("Keep_WStairFoot", -26.5f, 17.6f, 0f, PathNodeKind.Stair),   // the west stair climbs along the terrace wall's face from the Wall Ginnel
            ("Keep_WStairHead", -20.8f, 20.6f, KeepHeight, PathNodeKind.Stair),
            ("Keep_Walk_W", -8f, 29.0f, KeepHeight, PathNodeKind.Lane),
        };
        static readonly (string A, string B)[] KeepEdges =
        {
            ("NorthEastStub_1", "Keep_StairFoot"), ("Keep_StairFoot", "Keep_StairHead"), ("Keep_StairHead", "HeroesHome_Annex_Door"),
            ("Keep_StairHead", "Keep_Walk_E"), ("Keep_Walk_E", "Keep_Walk_N"), ("Keep_Walk_N", "BellTower_Door"),
            ("NorthWestGinnel_2", "Keep_WStairFoot"), ("Keep_WStairFoot", "Keep_WStairHead"), ("Keep_WStairHead", "Keep_Walk_W"), ("Keep_Walk_W", "Keep_Walk_N"), ("KeepHall_Door", "Keep_Walk_W"),
        };

        // ------------------------------------------------------------------------------------------------ the rebuilt preset (ring 1 assignment; rings 2-3 homes)
        public enum BuildingKind : byte { Empty, Home, Bakery, Inn, ApprovedFacade, Garden, Townhouse, StoneCottage, Workshop, Smithy, Schoolhouse }
        static readonly (string Foundation, BuildingKind Kind)[] RebuiltRing1 =
        {
            ("F01", BuildingKind.ApprovedFacade), ("F02", BuildingKind.Bakery), ("F09", BuildingKind.Inn),
            ("F04", BuildingKind.Home), ("F05", BuildingKind.Home), ("F08", BuildingKind.Home),
            ("F06", BuildingKind.Townhouse), ("F07", BuildingKind.Townhouse),   // kit family 3 (sc_townhouse.py): the pair on the south side, both facing the fountain
            ("F03", BuildingKind.Empty), ("F10", BuildingKind.Empty),
        };
        // Ring 2 lots fixed by hand: R2_09 (S, 6 m) the stone cottage B and R2_08 (M, 8 m) the workshop B (Q1), east side of the Ring Lane, fronts to the lane;
        // the kit-ready shops (design 6, queue Q2) on M lots: R2_01 the smithy, R2_03 the schoolhouse (no variant). Their pads keep the layout heights.
        // The seeded fill below never touches them (and uses them as neighbours).
        static readonly (string Foundation, BuildingKind Kind, char Variant, int Turn)[] RebuiltRing2Fixed =
        {
            ("R2_09", BuildingKind.StoneCottage, 'B', 0), ("R2_08", BuildingKind.Workshop, 'B', 0),
            ("R2_01", BuildingKind.Smithy, ' ', 0), ("R2_03", BuildingKind.Schoolhouse, ' ', 0),
        };
        // Ring 1 townhouses (kit family 3): the pair on the south side, both facing the fountain.
        static readonly (string Foundation, char Variant, int Turn)[] RebuiltTownhouses = { ("F06", 'A', 0), ("F07", 'B', 0) };
        static readonly string[] RebuiltGardens = { "R2_07", "R2_18", "R2_22", "R3_02" };   // left as allotments in the rebuilt massing

        // ---- the seeded ring 2 fill (Q3) ----
        // Pool = every ring 2 lot that is not fixed above, not a garden, not an L lot (no home kit fits: the large lots wait for the schoolhouse and the like) and
        // not reserved. Nothing is reserved now: R2_17 was held for the smithy until Q2 put it on R2_01 (fixed above).
        public static readonly string[] Ring2Reserved = new string[0];
        public const int Ring2Seed = 20261031;
        public const float Ring2EmptyShare = 0.30f;      // about 30% of the pool stay ruins (the tests accept 25-35%)
        public const float NeighbourDistance = 22f;      // "neighbouring": lot centres closer than this (about one lot and a half plus the lane across), on either side of the lane

        // Kit variants by class (the FBXs that exist): S lots take the three cottages or Workshop_A, M lots the three townhouses or Workshop_B / C.
        public static IEnumerable<(BuildingKind Kind, char Variant)> HomeVariants(FoundationSize size)
        {
            if (size == FoundationSize.S)
            {
                yield return (BuildingKind.StoneCottage, 'A'); yield return (BuildingKind.StoneCottage, 'B'); yield return (BuildingKind.StoneCottage, 'C');
                yield return (BuildingKind.Workshop, 'A');
            }
            else if (size == FoundationSize.M)
            {
                yield return (BuildingKind.Townhouse, 'A'); yield return (BuildingKind.Townhouse, 'B'); yield return (BuildingKind.Townhouse, 'C');
                yield return (BuildingKind.Workshop, 'B'); yield return (BuildingKind.Workshop, 'C');
            }
        }

        /// <summary>One placed (or deliberately empty) lot of the Rebuilt preset: the family, the variant letter (A-C) and the turn in quarter turns (0 = front to the door side).</summary>
        public readonly struct HomeAssignment
        {
            public readonly BuildingKind Kind; public readonly char Variant; public readonly int Turn;
            public HomeAssignment(BuildingKind kind, char variant, int turn) { Kind = kind; Variant = variant; Turn = turn; }
            /// <summary>The art name, e.g. "Townhouse_A" (the FBX folder).</summary>
            public string VariantName => Kind + "_" + Variant;
        }

        /// <summary>The ring 2 lots the seeded fill considers, in table order (see the comment on Ring2Reserved).</summary>
        public static List<Foundation> Ring2Pool(TownLayout t)
        {
            var pool = new List<Foundation>();
            foreach (Foundation f in t.Foundations)
            {
                if (f.Ring != 2 || f.Size == FoundationSize.L) continue;
                if (Array.IndexOf(RebuiltGardens, f.Id) >= 0 || Array.IndexOf(Ring2Reserved, f.Id) >= 0) continue;
                bool fixedLot = false;
                foreach (var r in RebuiltRing2Fixed) if (r.Foundation == f.Id) fixedLot = true;
                if (!fixedLot) pool.Add(f);
            }
            return pool;
        }

        /// <summary>The deterministic ring 2 assignment for a layout: pool lot id to its home or Empty. Empties are picked by farthest-point sampling (so they spread round the ring); the rest take a variant that fits their class and differs from every assigned lot within NeighbourDistance (the fixed R2_08 / R2_09
        /// and the ring 1 townhouses count), so no neighbour repeats a variant (and a variant + turn pair can never repeat along a lane). Turn is 0 everywhere: fronts to the lane.</summary>
        public static Dictionary<string, HomeAssignment> AssignRing2(TownLayout t, int seed)
        {
            List<Foundation> pool = Ring2Pool(t);
            var rng = new Lcg((uint)seed);
            int empties = (int)Math.Round(pool.Count * Ring2EmptyShare);
            // Farthest-point sampling: a seeded first empty, then each next one is the pool lot farthest from every empty so far, so the ruins spread round the ring.
            var empty = new HashSet<string>();
            var picked = new List<Foundation> { pool[rng.Next(pool.Count)] };
            while (picked.Count < empties)
            {
                Foundation best = null; float bestD = -1f;
                foreach (Foundation f in pool)
                {
                    if (picked.Contains(f)) continue;
                    float d = float.MaxValue;
                    foreach (Foundation q in picked) d = Math.Min(d, TownPoint.Distance(f.Centre, q.Centre));
                    if (d > bestD + 1e-3f) { bestD = d; best = f; }
                }
                picked.Add(best);
            }
            foreach (Foundation f in picked) empty.Add(f.Id);

            var result = new Dictionary<string, HomeAssignment>();
            var placed = new List<(TownPoint Centre, BuildingKind Kind, char Variant)>();
            foreach (var r in RebuiltRing2Fixed) placed.Add((t.Foundation(r.Foundation).Centre, r.Kind, r.Variant));
            foreach (var r in RebuiltTownhouses) placed.Add((t.Foundation(r.Foundation).Centre, BuildingKind.Townhouse, r.Variant));
            var todo = new List<Foundation>();
            foreach (Foundation f in pool) { if (empty.Contains(f.Id)) result[f.Id] = new HomeAssignment(BuildingKind.Empty, 'A', 0); else todo.Add(f); }

            var order = new List<List<(BuildingKind, char)>>();
            foreach (Foundation f in todo)
            {
                var c = new List<(BuildingKind, char)>(HomeVariants(f.Size));
                for (int i = c.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = c[i]; c[i] = c[j]; c[j] = tmp; }
                order.Add(c);
            }
            if (!Fill(todo, order, 0, placed, result)) throw new InvalidOperationException("ring 2 fill: no variant assignment avoids neighbouring duplicates");
            return result;
        }

        static bool Fill(List<Foundation> todo, List<List<(BuildingKind, char)>> order, int i, List<(TownPoint Centre, BuildingKind Kind, char Variant)> placed, Dictionary<string, HomeAssignment> result)
        {
            if (i == todo.Count) return true;
            Foundation f = todo[i];
            foreach (var (kind, variant) in order[i])
            {
                bool clash = false;
                foreach (var p in placed) if (p.Kind == kind && p.Variant == variant && TownPoint.Distance(p.Centre, f.Centre) < NeighbourDistance) { clash = true; break; }
                if (clash) continue;
                placed.Add((f.Centre, kind, variant)); result[f.Id] = new HomeAssignment(kind, variant, 0);
                if (Fill(todo, order, i + 1, placed, result)) return true;
                placed.RemoveAt(placed.Count - 1); result.Remove(f.Id);
            }
            return false;
        }

        // A tiny deterministic generator (the assignment must not depend on System.Random's implementation).
        sealed class Lcg
        {
            uint _s;
            public Lcg(uint seed) { _s = seed * 2654435761u + 1u; }
            public int Next(int bound) { unchecked { _s = _s * 1664525u + 1013904223u; return (int)((_s >> 8) % (uint)bound); } }
        }

        static Dictionary<string, HomeAssignment> _ring2;
        static Dictionary<string, HomeAssignment> Ring2() => _ring2 ?? (_ring2 = AssignRing2(Create(), Ring2Seed));

        public static HomeAssignment RebuiltHome(string foundationId)
        {
            foreach (var r in RebuiltTownhouses) if (r.Foundation == foundationId) return new HomeAssignment(BuildingKind.Townhouse, r.Variant, r.Turn);
            foreach (var r in RebuiltRing2Fixed) if (r.Foundation == foundationId) return new HomeAssignment(r.Kind, r.Variant, r.Turn);
            if (Ring2().TryGetValue(foundationId, out HomeAssignment a)) return a;
            return new HomeAssignment(RebuiltBuilding(foundationId), 'A', 0);
        }

        public static BuildingKind RebuiltBuilding(string foundationId)
        {
            foreach (var r in RebuiltRing1) if (r.Foundation == foundationId) return r.Kind;
            foreach (var r in RebuiltRing2Fixed) if (r.Foundation == foundationId) return r.Kind;
            if (Array.IndexOf(RebuiltGardens, foundationId) >= 0) return BuildingKind.Garden;
            if (Ring2().TryGetValue(foundationId, out HomeAssignment a)) return a.Kind;
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

            char[] variants = { 'A', 'B', 'C' };
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
                // General clearance rule: the default setback, then pulled toward the lane by whatever shortfall the town wall behind the lot leaves (never under MinLotSetback).
                float setback = LotSetback;
                TownPoint c = p + n * (lane.Width * 0.5f + setback + half);
                float yaw = (float)(Math.Atan2(n.X, n.Z) * 180.0 / Math.PI);   // lot +Z points away from the lane, so its South side faces it
                for (int pass = 0; pass < 6 && setback > MinLotSetback; pass++)
                {
                    float outer = 0f, inner = float.MaxValue;
                    foreach (TownPoint k in new Footprint(c, half * 2f, half * 2f, yaw).Corners()) { float d = PlateauDistance(k.X, k.Z); outer = Math.Max(outer, d); inner = Math.Min(inner, d); }
                    float excess = outer - LotWallLimit;
                    if (excess <= 0.001f || inner >= TownWallOffset) break;   // clear already, or a lot wholly outside the wall (ring 3)
                    float step = Math.Min(excess, setback - MinLotSetback);
                    setback -= step;
                    c = c - n * step;
                }
                // Lane clearance: where the lane bends, a footprint corner can end up on the carriageway; push the lot away from the lane by the shortfall (never past the town wall limit).
                for (int pass = 0; pass < 6; pass++)
                {
                    float shortfall = 0f, outer = 0f, inner = float.MaxValue;
                    foreach (TownPoint k in new Footprint(c, half * 2f, half * 2f, yaw).Corners())
                    {
                        float need = lane.Width * 0.5f + 0.1f;
                        for (int i = 1; i < lane.Points.Length; i++) shortfall = Math.Max(shortfall, need - TownPoint.Distance(k, ClosestOnSegment(k, lane.Points[i - 1], lane.Points[i], out _)));
                        float d = PlateauDistance(k.X, k.Z); outer = Math.Max(outer, d); inner = Math.Min(inner, d);
                    }
                    if (shortfall <= 0.001f) break;
                    float room = (outer <= LotWallLimit && inner < TownWallOffset) ? Math.Max(0f, LotWallLimit - outer) : shortfall;
                    float push = Math.Min(shortfall, room);
                    if (push <= 0.001f) break;
                    c = c + n * push;
                }
                t.Foundations.Add(new Foundation { Id = r.Id, Ring = r.Ring, Size = r.Size, Centre = c, Yaw = (float)Math.Round(yaw, 2), Facing = LotSide.South, RuinVariant = Variant(r.Size, variants[vi++ % 3]) });
            }

            foreach (Foundation f in t.Foundations) f.RuinTurn = Turn(f.Id);
            foreach (var r in LandmarkRows)
            {
                var lm = new Landmark { Id = r.Id, Name = r.Name, Kind = r.Kind, Footprint = new Footprint(new TownPoint(r.X, r.Z), r.SX, r.SZ, r.Yaw), Height = r.H };
                lm.Y = float.IsNaN(r.Y) ? Round(NaturalGround(r.X, r.Z)) : r.Y;
                t.Landmarks.Add(lm);
            }

            BuildGraph(t);
            return t;
        }

        static string Variant(FoundationSize s, char v) => "Ruin_" + s + "_" + v;   // the ruin kit folder name (Content/Art/Town/Ruins/Ruin_<S|M|L>_<A|B|C>)

        // A stable 0..3 turn per lot id (FNV-1a), so the same ruin variant reads differently from lot to lot.
        static int Turn(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                return (int)((h ^ (h >> 16)) % 4u);
            }
        }
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

            // Fountain loop and square loop: the square's shelf, fixed at 0.
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                AddNode(t, "FL_" + i, new TownPoint(FountainLoopRadius * (float)Math.Sin(a), FountainLoopRadius * (float)Math.Cos(a)), PathNodeKind.Loop, 0f);
            }
            for (int i = 0; i < 8; i++) t.Edges.Add(new PathEdge("FL_" + i, "FL_" + ((i + 1) % 8)));
            int nSq = SquareLoop.Length / 2;
            for (int i = 0; i < nSq; i++) AddNode(t, "SQ_" + i, new TownPoint(SquareLoop[2 * i], SquareLoop[2 * i + 1]), PathNodeKind.Loop, 0f);
            for (int i = 0; i < nSq; i++) t.Edges.Add(new PathEdge("SQ_" + i, "SQ_" + ((i + 1) % nSq)));
            foreach (var s in Spokes) t.Edges.Add(new PathEdge("FL_" + s.Fl, NodeAt(t, new TownPoint(s.X, s.Z)).Id));

            // Lane spines: a node per lane point; a point within 0.3 m of an existing node joins it (lane junctions and the square loop).
            var laneNodes = new Dictionary<Lane, PathNode[]>();
            foreach (Lane lane in t.Lanes)
            {
                var nodes = new PathNode[lane.Points.Length];
                for (int i = 0; i < lane.Points.Length; i++)
                {
                    nodes[i] = NodeAt(t, lane.Points[i]) ?? AddNode(t, lane.Id + "_" + i, lane.Points[i], PathNodeKind.Lane);
                    if (i > 0) t.Edges.Add(new PathEdge(nodes[i - 1].Id, nodes[i].Id));
                }
                laneNodes[lane] = nodes;
            }
            RelaxLanes(t);
            foreach (var kv in laneNodes) { kv.Key.Heights = new float[kv.Value.Length]; for (int i = 0; i < kv.Value.Length; i++) kv.Key.Heights[i] = kv.Value[i].Y; }

            // The keep (fixed heights) and the landmark doors.
            foreach (var k in KeepNodes) AddNode(t, k.Id, new TownPoint(k.X, k.Z), k.Kind, k.Y);
            foreach (var e in KeepEdges) t.Edges.Add(new PathEdge(e.A, e.B));
            foreach (var r in LandmarkRows)
            {
                if (float.IsNaN(r.DoorX)) continue;
                string id = r.Id + "_Door";
                Landmark lm = t.Landmark(r.Id);
                PathNode n = AddNode(t, id, new TownPoint(r.DoorX, r.DoorZ), PathNodeKind.Door, float.IsNaN(r.Y) ? 0f : r.Y);
                lm.DoorNode = id;
                if (!HasEdge(t, id))
                {
                    PathNode target = LinkToNetwork(t, n, blockers, 14f, float.IsNaN(r.Y));
                    if (target != null && float.IsNaN(r.Y)) { n.Y = target.Y; lm.Y = target.Y; }   // a landmark off the shelf (the mill) stands at the level of its lane
                }
            }

            // Each foundation: a doorstep in the middle of each side and a corner node off each corner, linked round the lot. The front doorstep links to the
            // nearest point of a lane or the square it can see, and the lot takes that point's height: its pad is cut into the slope (or built up on fill)
            // level with its lane. Any other doorstep within 3.5 m of a lane links too, when that link is not too steep.
            foreach (Foundation f in t.Foundations)
            {
                for (int c = 0; c < 4; c++) AddNode(t, f.CornerId(c), f.CornerPoint(c), PathNodeKind.Corner, 0f);
                foreach (LotSide s in TownLayout.AllSides) AddNode(t, f.DoorstepId(s), f.DoorstepPoint(s), PathNodeKind.Doorstep, 0f);
                string[] ring = { f.CornerId(0), f.DoorstepId(LotSide.South), f.CornerId(1), f.DoorstepId(LotSide.East), f.CornerId(2), f.DoorstepId(LotSide.North), f.CornerId(3), f.DoorstepId(LotSide.West) };
                for (int i = 0; i < ring.Length; i++) t.Edges.Add(new PathEdge(ring[i], ring[(i + 1) % ring.Length]));
            }
            foreach (Foundation f in t.Foundations)
            {
                PathNode front = t.Node(f.DoorstepId(f.Facing));
                PathNode target = LinkToNetwork(t, front, blockers, 14f, true);
                f.Y = target != null ? target.Y : Round(NaturalGround(f.Centre.X, f.Centre.Z));
                for (int c = 0; c < 4; c++) t.Node(f.CornerId(c)).Y = f.Y;
                foreach (LotSide s in TownLayout.AllSides) t.Node(f.DoorstepId(s)).Y = f.Y;
            }
            foreach (Foundation f in t.Foundations)
                foreach (LotSide s in TownLayout.AllSides) if (s != f.Facing) LinkToNetwork(t, t.Node(f.DoorstepId(s)), blockers, 3.5f, false);
        }

        // Lane profiles: every lane and loop edge eased to MaxLaneGrade by moving its free ends toward each other. The square, the fountain loop and the
        // keep are fixed; everything else starts on the natural ground. Junction nodes are shared, so the lanes meet at one height.
        static void RelaxLanes(TownLayout t)
        {
            var edges = new List<(PathNode A, PathNode B, float D)>();
            foreach (PathEdge e in t.Edges)
            {
                PathNode a = t.Node(e.A), b = t.Node(e.B);
                if (a.Kind == PathNodeKind.Lane || b.Kind == PathNodeKind.Lane) edges.Add((a, b, TownPoint.Distance(a.Pos, b.Pos)));
            }
            for (int iter = 0; iter < 400; iter++)
            {
                bool moved = false;
                foreach (var (a, b, d) in edges)
                {
                    float diff = b.Y - a.Y, max = MaxLaneGrade * d;
                    if (Math.Abs(diff) <= max) continue;
                    float excess = Math.Abs(diff) - max, sign = Math.Sign(diff);
                    bool fa = a.Kind != PathNodeKind.Lane, fb = b.Kind != PathNodeKind.Lane;
                    if (fa && fb) continue;
                    if (fa) b.Y -= sign * excess;
                    else if (fb) a.Y += sign * excess;
                    else { a.Y += sign * excess * 0.5f; b.Y -= sign * excess * 0.5f; }
                    moved = true;
                }
                if (!moved) break;
            }
            foreach (var (a, b, _) in edges) { a.Y = Round(a.Y); b.Y = Round(b.Y); }
        }

        // Links n to the nearest point, within maxDist, of a lane, loop or keep-walk edge whose straight segment from n crosses no footprint. A point in the
        // middle of an edge splits it with a new node at the interpolated height. With matchHeight the caller sets n to the target's height (a lot pad);
        // otherwise a link steeper than MaxGrade is not made. No candidate leaves n unlinked here: Validate() then reports it, the build failure we want.
        static PathNode LinkToNetwork(TownLayout t, PathNode n, List<Footprint> blockers, float maxDist, bool matchHeight)
        {
            PathEdge bestEdge = default; TownPoint bestP = default; float bestT = 0f, bestD = maxDist; bool found = false;
            foreach (PathEdge e in t.Edges)
            {
                PathNode a = t.Node(e.A), b = t.Node(e.B);
                if (!IsNetwork(a) || !IsNetwork(b)) continue;
                TownPoint c = ClosestOnSegment(n.Pos, a.Pos, b.Pos, out float tt);
                float d = TownPoint.Distance(n.Pos, c);
                if (d >= bestD) continue;
                bool clear = true;
                foreach (Footprint fp in blockers) if (fp.SegmentCrosses(n.Pos, c)) { clear = false; break; }
                if (!clear) continue;
                float y = a.Y + (b.Y - a.Y) * tt;
                if (!matchHeight && Math.Abs(y - n.Y) > TownLayout.MaxGrade * d) continue;
                bestEdge = e; bestP = c; bestT = tt; bestD = d; found = true;
            }
            if (!found) return null;
            PathNode na = t.Node(bestEdge.A), nb = t.Node(bestEdge.B), target;
            if (TownPoint.Distance(bestP, na.Pos) < 0.5f) target = na;
            else if (TownPoint.Distance(bestP, nb.Pos) < 0.5f) target = nb;
            else
            {
                target = AddNode(t, "J_" + n.Id, bestP, na.Kind == PathNodeKind.Loop && nb.Kind == PathNodeKind.Loop ? PathNodeKind.Loop : PathNodeKind.Lane, Round(na.Y + (nb.Y - na.Y) * bestT));
                t.Edges.Remove(bestEdge);
                t.Edges.Add(new PathEdge(na.Id, target.Id)); t.Edges.Add(new PathEdge(target.Id, nb.Id));
            }
            if (matchHeight) n.Y = target.Y;
            t.Edges.Add(new PathEdge(n.Id, target.Id));
            return target;
        }

        static bool IsNetwork(PathNode n) => n.Kind == PathNodeKind.Loop || n.Kind == PathNodeKind.Lane;

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

    }
}
