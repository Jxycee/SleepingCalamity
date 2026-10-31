// Core/Town/SaltwakeRoadPlan.cs: what stands outside the Lower Gate (QUEUE Q15), as data. The Saltwake Road (HollowmereLayout lane "SaltwakeRoad") winds
// down the western fall; along it: milestones, a wayside shrine at the first bend, a cart on the verge (broken in Ruined, parked in Rebuilt), a hedge on one
// side and a post-and-rail fence on the other (they swap sides at the shrine). Beyond the town wall the open ground is cut into a patchwork of fields
// (Field): hedgerows on the field edges and hedgerow trees on them, on the terrain (Ground items) and on the backdrop ring past the terrain's edge (Ring
// items), so the low cameras get a framed horizon instead of bare meadow.
// Positions are town metres (+Z north, origin = the fountain); the builder takes the height from HollowmereLayout.Ground / the terrain. Deterministic: no
// UnityEngine.Random, hashes of fixed salts only, so two builds give the same scene.
// Authority: none (static layout data).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum RoadItemKind { Milestone, Shrine, Cart, Fence, Hedge, FieldHedge, Tree }

    [Flags] public enum RoadPresets { Ruined = 1, Rebuilt = 2, Both = 3 }

    public struct RoadItem
    {
        public RoadItemKind Kind;
        public string Piece;        // the art piece name (Content/Art/Town/Road/<Piece>); empty for card items (FieldHedge, Tree)
        public float X, Z, Yaw;     // yaw in degrees about +Y, Unity convention: 0 = local +Z faces north
        public float Scale, Tilt;   // uniform scale; Tilt = degrees of lean (Ruined milestones, broken posts), about the local X axis
        public int Variant;         // card items: atlas cell (trees 0..3) or hedge cell; roadside pieces: 0
        public RoadPresets Presets;
        public bool OnRing;         // past the terrain's edge (the backdrop ring), else on the terrain
        public override string ToString() => Kind + " " + Piece + " (" + X.ToString("0.0") + ", " + Z.ToString("0.0") + ") " + Presets;
    }

    public enum FieldKind { Pasture, Hay, Ploughed }

    public static class SaltwakeRoadPlan
    {
        // ------------------------------------------------------------------------------------------------ numbers
        public const string RoadLane = "SaltwakeRoad";
        public const float RoadsideStart = 40f;         // arc length (m) from the gate where the roadside dressing starts (past the R3 lots at s 22 and 34)
        public const float ShrineBendS = -1f;           // computed: the shrine stands on the outside of the road's second point after RoadsideStart
        public const float HedgeOffset = 1.6f, FenceOffset = 1.4f, MilestoneOffset = 0.8f;   // past the road's edge
        public const float HedgeStep = 3.8f, FenceStep = 3.0f;                              // segment pitch: Hedge_A/B are 4 m, Fence_Rail 3 m
        public const float MilestonePitch = 42f;
        public const float FieldAngleDeg = 17f, FieldU = 36f, FieldV = 48f;               // the patchwork grid: rotated, brick-bonded rows
        public const float FieldStartOffset = 38f;      // fields start this far outside the plateau (past the wall bank 26-29.5, the ditch 31 and its outer slope)
        public const float TerrainMinX = -150f, TerrainMaxX = 150f, TerrainMinZ = -170f, TerrainMaxZ = 130f;   // HollowmereTerrain.Origin / Size
        public const float RingRadius = 620f, RingHedgeRadius = 300f, CopseChance = 0.035f;           // the backdrop ring's trees and fields stop here (fogged out long before)
        public static readonly TownPoint RingCentre = new TownPoint(0f, -20f);

        // ------------------------------------------------------------------------------------------------ fields
        static readonly float Ca = (float)Math.Cos(FieldAngleDeg * Math.PI / 180.0), Sa = (float)Math.Sin(FieldAngleDeg * Math.PI / 180.0);
        static void ToField(float x, float z, out float u, out float v) { u = x * Ca - z * Sa; v = x * Sa + z * Ca; }
        static void FromField(float u, float v, out float x, out float z) { x = u * Ca + v * Sa; z = -u * Sa + v * Ca; }

        static float RowOffset(int row) => Hash01(row, 7) * FieldU;                                  // brick bond: each row's field edges shift
        static float ColumnWobble(int row, int col) => (Hash01(row * 131 + col, 11) - 0.5f) * 0.35f * FieldU;

        // The field under a point: its kind, its cell id, and the distance (m) to the nearest field edge (where the hedgerows run).
        public static FieldKind Field(float x, float z, out float edgeDistance, out int cell)
        {
            ToField(x, z, out float u, out float v);
            int row = (int)Math.Floor(v / FieldV);
            float dv = Math.Min(v - row * FieldV, (row + 1) * FieldV - v);
            float uu = u + RowOffset(row);
            int col = (int)Math.Floor(uu / FieldU);
            float left = col * FieldU + ColumnWobble(row, col), right = (col + 1) * FieldU + ColumnWobble(row, col + 1);
            if (uu < left) { col--; right = left; left = col * FieldU + ColumnWobble(row, col); }
            else if (uu > right) { col++; left = right; right = (col + 1) * FieldU + ColumnWobble(row, col + 1); }
            float du = Math.Min(uu - left, right - uu);
            edgeDistance = Math.Max(0f, Math.Min(du, dv));
            cell = row * 7919 + col;
            float h = Hash01(cell, 3);
            return h < 0.5f ? FieldKind.Pasture : h < 0.78f ? FieldKind.Hay : FieldKind.Ploughed;
        }

        // The furrow direction of a ploughed field (degrees, Unity yaw): along or across the grid, per cell.
        public static float FurrowYaw(int cell) => (Hash01(cell, 5) < 0.5f ? 0f : 90f) - FieldAngleDeg;

        // 0..1: how much of the patchwork shows here (0 inside the town, the wall's outer slope, the lake and its shore, the stream ravine, near lanes and lots).
        public static float FieldWeight(TownLayout t, float x, float z)
        {
            float e = HollowmereLayout.PlateauDistance(x, z);
            float w = Smooth(FieldStartOffset - 4f, FieldStartOffset + 4f, e);
            if (w <= 0f) return 0f;
            float lx = (x - HollowmereLayout.LakeCentre.X) / HollowmereLayout.LakeRadiusX, lz = (z - HollowmereLayout.LakeCentre.Z) / HollowmereLayout.LakeRadiusZ;
            w *= Smooth(1.12f, 1.35f, (float)Math.Sqrt(lx * lx + lz * lz));
            w *= Smooth(9f, 16f, HollowmereLayout.DistanceToPolyline(new TownPoint(x, z), HollowmereLayout.Stream));
            if (w <= 0f) return 0f;
            var p = new TownPoint(x, z);
            foreach (Lane lane in t.Lanes) w *= Smooth(lane.Width * 0.5f + 1.5f, lane.Width * 0.5f + 5f, HollowmereLayout.DistanceToPolyline(p, lane.Points));
            foreach (Foundation f in t.Foundations) if (f.Footprint.Contains(p, 5f)) return 0f;
            foreach (Landmark l in t.Landmarks) if (l.Footprint.Contains(p, 6f)) return 0f;
            return w;
        }

        public static bool OnTerrain(float x, float z, float margin = 0f) =>
            x > TerrainMinX + margin && x < TerrainMaxX - margin && z > TerrainMinZ + margin && z < TerrainMaxZ - margin;

        // ------------------------------------------------------------------------------------------------ the plan
        public static List<RoadItem> Create(TownLayout t)
        {
            var items = new List<RoadItem>();
            Lane road = HollowmereLayout.FindLane(t, RoadLane);
            Roadside(t, road, items);
            FieldEdges(t, road, items);
            return items;
        }

        static void Roadside(TownLayout t, Lane road, List<RoadItem> items)
        {
            TownPoint[] pts = road.Points;
            float len = HollowmereLayout.LaneLength(pts), half = road.Width * 0.5f;
            float roadEnd = LengthTo(pts, TerrainMinX + 4f);   // stop the dressing a little inside the terrain's west edge
            float bend = ShrineS(pts);
            float sideFlip = bend;                              // hedge north / fence south before the shrine, swapped after it

            // Hedge and fence runs, both sides, gaps at the shrine and the milestones.
            for (int side = -1; side <= 1; side += 2)
            {
                bool hedgeFirst = side == 1;
                for (float s = RoadsideStart; s < roadEnd; )
                {
                    bool hedge = (s < sideFlip) == hedgeFirst;
                    float step = hedge ? HedgeStep : FenceStep;
                    float mid = s + step * 0.5f;
                    if (Math.Abs(mid - bend) < 9f) { s += step; continue; }                          // the shrine's lay-by
                    HollowmereLayout.PointAlong(pts, mid, out TownPoint c, out TownPoint dir);
                    var n = new TownPoint(dir.Z, -dir.X) * side;   // right of travel * side: side +1 = right = north on the westward road
                    float off = half + (hedge ? HedgeOffset : FenceOffset);
                    TownPoint p = c + n * off;
                    if (!Clear(t, p, 2.5f)) { s += step; continue; }
                    float yaw = Deg(Math.Atan2(-n.X, -n.Z));   // front (local +Z, Blender -Y) toward the road, so the run (local X) lies along it
                    int k = (int)(s * 10f) + side * 1000;
                    if (hedge)
                    {
                        string piece = Hash01(k, 21) < 0.5f ? "Hedge_A" : "Hedge_B";
                        bool gapR = Hash01(k, 23) < 0.14f;   // Ruined: an overgrown run with the odd gap
                        if (!gapR) items.Add(Item(RoadItemKind.Hedge, piece, p, yaw + (Hash01(k, 25) < 0.5f ? 0f : 180f), 1.12f + 0.2f * Hash01(k, 27), 0f, RoadPresets.Ruined));
                        items.Add(Item(RoadItemKind.Hedge, piece, p, yaw + (Hash01(k, 25) < 0.5f ? 0f : 180f), 0.94f + 0.06f * Hash01(k, 29), 0f, RoadPresets.Rebuilt));
                    }
                    else
                    {
                        float h = Hash01(k, 31);
                        if (h >= 0.12f) items.Add(Item(RoadItemKind.Fence, h < 0.42f ? "Fence_Rail_Broken" : "Fence_Rail", p, yaw, 1f, 0f, RoadPresets.Ruined));   // Ruined: a third broken, some gone
                        items.Add(Item(RoadItemKind.Fence, "Fence_Rail", p, yaw, 1f, 0f, RoadPresets.Rebuilt));
                    }
                    s += step;
                }
            }

            // Milestones on the north verge every MilestonePitch from the gate (the first one past the lots), A and B alternating.
            int mi = 0;
            for (float s = RoadsideStart + 6f; s < roadEnd; s += MilestonePitch, mi++)
            {
                if (Math.Abs(s - bend) < 10f) s += 12f;
                HollowmereLayout.PointAlong(pts, s, out TownPoint c, out TownPoint dir);
                var n = new TownPoint(dir.Z, -dir.X);
                TownPoint p = c + n * (half + MilestoneOffset);
                float yaw = Deg(Math.Atan2(-n.X, -n.Z));   // carved face (local +Z) toward the road
                string piece = mi % 2 == 0 ? "Milestone_A" : "Milestone_B";
                items.Add(Item(RoadItemKind.Milestone, piece, p, yaw + 8f * (Hash01(mi, 41) - 0.5f), 1f, 9f + 8f * Hash01(mi, 43), RoadPresets.Ruined));
                items.Add(Item(RoadItemKind.Milestone, piece, p, yaw, 1f, 0f, RoadPresets.Rebuilt));
            }

            // The wayside shrine on the outside of the first bend past the lots, set back in its own lay-by, facing the road.
            {
                HollowmereLayout.PointAlong(pts, bend, out TownPoint c, out TownPoint dir);
                TownPoint outside = OutsideOfBend(pts, bend);
                TownPoint p = c + outside * (half + 3.2f);
                float yaw = Deg(Math.Atan2(-outside.X, -outside.Z));
                items.Add(Item(RoadItemKind.Shrine, "Shrine_Ruined", p, yaw, 1f, 0f, RoadPresets.Ruined));
                items.Add(Item(RoadItemKind.Shrine, "Shrine", p, yaw, 1f, 0f, RoadPresets.Rebuilt));
            }

            // The cart, on the south verge between the first milestone and the shrine: tipped into the verge in Ruined, parked square to the road in Rebuilt.
            {
                float s = (RoadsideStart + bend) * 0.5f + 4f;
                HollowmereLayout.PointAlong(pts, s, out TownPoint c, out TownPoint dir);
                var n = new TownPoint(-dir.Z, dir.X);   // left of travel = south
                float yaw = Deg(Math.Atan2(dir.X, dir.Z));
                items.Add(Item(RoadItemKind.Cart, "Cart_Broken", c + n * (half + 2.4f), yaw + 28f, 1f, 0f, RoadPresets.Ruined));
                items.Add(Item(RoadItemKind.Cart, "Cart", c + n * (half + 1.9f), yaw + 4f, 1f, 0f, RoadPresets.Rebuilt));
            }
        }

        // Hedgerows (cards) along the field edges and hedgerow trees on them, on the terrain and the backdrop ring. Never on the road or its dressing.
        static void FieldEdges(TownLayout t, Lane road, List<RoadItem> items)
        {
            const float step = 4f;
            int rows = (int)Math.Ceiling(RingRadius / FieldV) + 2, cols = (int)Math.Ceiling(RingRadius / FieldU) + 3;
            for (int row = -rows; row <= rows; row++)
            {
                // the row's long edge (v = row * FieldV), walked in u
                for (float u = -RingRadius - FieldU; u < RingRadius + FieldU; u += step)
                    EdgeSample(t, road, items, u + step * 0.5f, row * FieldV, row * 1000003 + (int)Math.Floor(u / step), true);
                // the short edges between the row's fields
                for (int col = -cols; col <= cols; col++)
                {
                    float uu = col * FieldU + ColumnWobble(row, col) - RowOffset(row);
                    for (float v = row * FieldV; v < (row + 1) * FieldV; v += step)
                        EdgeSample(t, road, items, uu, v + step * 0.5f, (row * 977 + col) * 101 + (int)Math.Floor((v - row * FieldV) / step), false);
                }
            }
        }

        static void EdgeSample(TownLayout t, Lane road, List<RoadItem> items, float u, float v, int key, bool along)
        {
            FromField(u, v, out float x, out float z);
            var p = new TownPoint(x, z);
            if (TownPoint.Distance(p, RingCentre) > RingRadius) return;
            bool onTerrain = OnTerrain(x, z, 2f);
            if (!onTerrain && OnTerrain(x, z, -2f)) return;                                          // keep cards off the terrain's edge seam
            float dRoad = HollowmereLayout.DistanceToPolyline(p, road.Points);
            if (onTerrain)
            {
                if (FieldWeight(t, x, z) < 0.6f) return;
                if (dRoad < road.Width * 0.5f + 7f) return;                                          // the roadside has its own mesh hedges and fences
            }
            else if (dRoad < 10f || RingRoadDistance(x, z) < 8f) return;
            float yaw = (along ? 90f : 0f) - FieldAngleDeg;
            float gap = Hash01(key, 51);
            bool hedgeHere = onTerrain || TownPoint.Distance(p, RingCentre) < RingHedgeRadius;   // further out the ring's field texture draws the hedge lines
            if (gap > 0.1f && hedgeHere) items.Add(new RoadItem { Kind = RoadItemKind.FieldHedge, X = x, Z = z, Yaw = yaw, Scale = 0.85f + 0.4f * Hash01(key, 53), Variant = (int)(Hash01(key, 55) * 4f) % 4, Presets = RoadPresets.Both, OnRing = !onTerrain });
            // a hedgerow tree every ~6 samples (about 24 m), further from the road on the terrain so the cards are never seen close
            if (Hash01(key, 57) < 0.17f && (!onTerrain || dRoad > 26f))
                items.Add(new RoadItem { Kind = RoadItemKind.Tree, X = x, Z = z, Yaw = Hash01(key, 59) * 360f, Scale = 0.95f + 0.55f * Hash01(key, 61), Variant = (int)(Hash01(key, 63) * 4f) % 4, Presets = RoadPresets.Both, OnRing = !onTerrain });
            // now and then a copse in the field corner: 6-13 trees in a loose clump 8-16 m into the field (the horizon's mass, not only lines)
            if (along && Hash01(key, 71) < CopseChance)
            {
                int n = 6 + (int)(Hash01(key, 73) * 8f);
                float into = (Hash01(key, 75) < 0.5f ? -1f : 1f) * (8f + 8f * Hash01(key, 77));
                for (int i = 0; i < n; i++)
                {
                    float a = Hash01(key + i, 79) * 6.2832f, r = 2f + 9f * (float)Math.Sqrt(Hash01(key + i, 81));
                    FromField(u + r * (float)Math.Cos(a), v + into + r * 0.7f * (float)Math.Sin(a), out float cx, out float cz);
                    bool cOn = OnTerrain(cx, cz, 2f);
                    if (cOn != onTerrain) continue;
                    if (cOn && (FieldWeight(t, cx, cz) < 0.8f || HollowmereLayout.DistanceToPolyline(new TownPoint(cx, cz), road.Points) < 30f)) continue;
                    items.Add(new RoadItem { Kind = RoadItemKind.Tree, X = cx, Z = cz, Yaw = Hash01(key + i, 83) * 360f, Scale = 0.85f + 0.6f * Hash01(key + i, 85), Variant = (int)(Hash01(key + i, 87) * 4f) % 4, Presets = RoadPresets.Both, OnRing = !cOn });
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the road past the terrain
        // The ring carries the road on from the lane's last point: a gentle winding line west toward Saltwake (the builder lays a ribbon on it).
        public static TownPoint[] RingRoad(TownLayout t)
        {
            TownPoint[] pts = HollowmereLayout.FindLane(t, RoadLane).Points;
            TownPoint a = pts[pts.Length - 1];
            var list = new List<TownPoint> { pts[pts.Length - 2], a };
            for (int i = 1; i <= 24; i++)
            {
                float x = a.X - i * 22f;
                list.Add(new TownPoint(x, a.Z + 14f * (float)Math.Sin(i * 0.42f) + 0.06f * (x - a.X)));
            }
            return list.ToArray();
        }

        static TownPoint[] ringRoadCache;
        static float RingRoadDistance(float x, float z)
        {
            if (ringRoadCache == null) ringRoadCache = RingRoad(HollowmereLayout.Create());
            return HollowmereLayout.DistanceToPolyline(new TownPoint(x, z), ringRoadCache);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        // Arc length of the road's first point past RoadsideStart where the road turns by more than 25 degrees: the first bend, where the shrine stands.
        public static float ShrineS(TownPoint[] pts)
        {
            float s = 0f;
            for (int i = 1; i < pts.Length - 1; i++)
            {
                s += TownPoint.Distance(pts[i - 1], pts[i]);
                if (s < RoadsideStart + 12f) continue;
                if (Turn(pts[i - 1], pts[i], pts[i + 1]) > 25f) return s;
            }
            return Math.Min(HollowmereLayout.LaneLength(pts), RoadsideStart + 40f);
        }

        static float Turn(TownPoint a, TownPoint b, TownPoint c)
        {
            TownPoint d1 = b - a, d2 = c - b;
            double a1 = Math.Atan2(d1.Z, d1.X), a2 = Math.Atan2(d2.Z, d2.X), d = a2 - a1;
            while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
            return (float)Math.Abs(d * 180.0 / Math.PI);
        }

        static TownPoint OutsideOfBend(TownPoint[] pts, float s)
        {
            HollowmereLayout.PointAlong(pts, s - 6f, out TownPoint a, out _);
            HollowmereLayout.PointAlong(pts, s + 6f, out TownPoint b, out _);
            HollowmereLayout.PointAlong(pts, s, out TownPoint c, out _);
            TownPoint mid = (a + b) * 0.5f, o = c - mid;
            float l = o.Length;
            if (l < 1e-3f) { TownPoint d = b - a; float dl = d.Length; return new TownPoint(d.Z / dl, -d.X / dl); }
            return o * (1f / l);
        }

        static float LengthTo(TownPoint[] pts, float minX)
        {
            float s = 0f;
            for (int i = 1; i < pts.Length; i++)
            {
                float len = TownPoint.Distance(pts[i - 1], pts[i]);
                if (pts[i].X < minX) { float f = (pts[i - 1].X - minX) / Math.Max(1e-3f, pts[i - 1].X - pts[i].X); return s + len * Math.Max(0f, Math.Min(1f, f)); }
                s += len;
            }
            return s;
        }

        static bool Clear(TownLayout t, TownPoint p, float margin)
        {
            foreach (Foundation f in t.Foundations) if (f.Footprint.Contains(p, margin)) return false;
            foreach (Landmark l in t.Landmarks) if (l.Footprint.Contains(p, margin)) return false;
            foreach (Lane lane in t.Lanes)
                if (lane.Id != RoadLane && HollowmereLayout.DistanceToPolyline(p, lane.Points) < lane.Width * 0.5f + margin) return false;
            return true;
        }

        static RoadItem Item(RoadItemKind k, string piece, TownPoint p, float yaw, float scale, float tilt, RoadPresets presets) =>
            new RoadItem { Kind = k, Piece = piece, X = p.X, Z = p.Z, Yaw = Norm(yaw), Scale = scale, Tilt = tilt, Presets = presets };

        static float Norm(float yaw) { yaw %= 360f; return yaw < 0f ? yaw + 360f : yaw; }
        static float Deg(double rad) => (float)(rad * 180.0 / Math.PI);
        static float Smooth(float a, float b, float x) { float u = Math.Max(0f, Math.Min(1f, (x - a) / (b - a))); return u * u * (3f - 2f * u); }

        public static float Hash01(int k, int salt)
        {
            unchecked
            {
                uint h = (uint)k * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
