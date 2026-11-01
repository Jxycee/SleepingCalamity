// Core/Town/Placement/WishEvaluator.cs: measures a building's wishes from its door and footprint (M6 Q3 A), as pure functions of the layout, a foundation and a turn.
// Each wish returns met + the measured number + its limit (for the live preview, e.g. "13.0 m, limit 12 m"); the score is the share of wishes met (equal weight, DC1).
// Door: the middle of the building body's side (Foundation.MaxBuilding), on side (Foundation.Facing + turn) in the lot's frame; Facing is the only door-measured
// kind. NearPoint, FarFrom and NextTo measure the gap between the building's lot square and the target footprint (or point): the nearest distance, 0 when they touch
// or overlap. (A door-to-fountain measure put the bakery's own lot F02 at 12.2 m against the 12 m limit, so no lot could meet the baker's wish.)
// Authority: none (pure); the host evaluates and every peer shows the same numbers from the same state.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public readonly struct WishResult
    {
        public readonly PlacementWish Wish;
        public readonly bool Met;
        public readonly float Measured;   // metres, or degrees for Facing; NaN when the target does not exist
        public readonly float Limit;      // the wish's limit in the same unit (0 for AtLot and InArea)
        public readonly string Unit;      // "m", "deg" or ""
        public WishResult(PlacementWish wish, bool met, float measured, float limit, string unit) { Wish = wish; Met = met; Measured = measured; Limit = limit; Unit = unit; }
        public string Detail => float.IsNaN(Measured) ? Wish.Hint : Wish.Hint + " (" + Measured.ToString("0.0") + " " + Unit + (Limit > 0f ? ", limit " + Limit.ToString("0.#") : "") + ")";
    }

    public sealed class WishReport
    {
        public readonly IReadOnlyList<WishResult> Results;
        public WishReport(IReadOnlyList<WishResult> results) { Results = results; }
        public int Count => Results.Count;
        public int MetCount { get { int n = 0; foreach (WishResult r in Results) if (r.Met) n++; return n; } }
        public bool AllMet => Count > 0 && MetCount == Count;
        public float Score => Count == 0 ? 0f : (float)MetCount / Count;   // equal weight per wish
    }

    public static class WishEvaluator
    {
        public const float DefaultFacingDeg = 45f;

        // The turn in 0-3 whatever was passed (a negative turn wraps).
        public static int NormalTurn(int turn) => ((turn % 4) + 4) % 4;

        public static LotSide DoorSide(Foundation f, int turn) => (LotSide)(((int)f.Facing + NormalTurn(turn)) & 3);

        public static TownPoint DoorPoint(Foundation f, int turn)
        {
            TownPoint d = Foundation.SideDir(DoorSide(f, turn)) * (Foundation.MaxBuilding(f.Size) * 0.5f);
            return f.Footprint.ToWorld(d.X, d.Z);
        }

        // The unit vector pointing out of the door in world space.
        public static TownPoint DoorOutward(Foundation f, int turn)
        {
            TownPoint d = Foundation.SideDir(DoorSide(f, turn)); Footprint fp = f.Footprint;
            TownPoint ax = fp.AxisX, az = fp.AxisZ;
            return new TownPoint(ax.X * d.X + az.X * d.Z, ax.Z * d.X + az.Z * d.Z);
        }

        public static WishReport Evaluate(TownLayout layout, IReadOnlyList<PlacementWish> wishes, string foundationId, int turn)
        {
            var list = new List<WishResult>(wishes.Count);
            Foundation f = layout.Foundation(foundationId);
            foreach (PlacementWish w in wishes) list.Add(f == null ? new WishResult(w, false, float.NaN, w.Limit, UnitOf(w.Kind)) : EvaluateOne(layout, w, f, turn));
            return new WishReport(list);
        }

        public static WishReport Evaluate(TownLayout layout, BuildingDef building, string foundationId, int turn) => Evaluate(layout, building.Wishes, foundationId, turn);

        public static WishResult EvaluateOne(TownLayout layout, PlacementWish w, Foundation f, int turn)
        {
            string unit = UnitOf(w.Kind);
            switch (w.Kind)
            {
                case PlacementWishKind.AtLot:
                {
                    Foundation t = layout.Foundation(w.TargetId);
                    float d = t == null ? float.NaN : TownPoint.Distance(f.Centre, t.Centre);
                    return new WishResult(w, t != null && f.Id == t.Id, d, 0f, unit);
                }
                case PlacementWishKind.InArea:
                {
                    float dx = Math.Max(Math.Max(w.MinX - f.Centre.X, f.Centre.X - w.MaxX), 0f), dz = Math.Max(Math.Max(w.MinZ - f.Centre.Z, f.Centre.Z - w.MaxZ), 0f);
                    float outside = (float)Math.Sqrt(dx * dx + dz * dz);   // 0 inside; else how far the centre is from the area
                    return new WishResult(w, outside <= 0f, outside, 0f, unit);
                }
            }

            if (!Resolve(layout, w, out Footprint? tfp, out TownPoint tpt)) return new WishResult(w, false, float.NaN, w.Limit, unit);
            switch (w.Kind)
            {
                case PlacementWishKind.NearPoint:
                {
                    float d = tfp.HasValue ? WishGeometry.Gap(f.Footprint, tfp.Value) : WishGeometry.PointToFootprint(tpt, f.Footprint);
                    return new WishResult(w, d <= w.Limit, d, w.Limit, unit);
                }
                case PlacementWishKind.FarFrom:
                {
                    float gap = tfp.HasValue ? WishGeometry.Gap(f.Footprint, tfp.Value) : WishGeometry.PointToFootprint(tpt, f.Footprint);
                    return new WishResult(w, gap >= w.Limit, gap, w.Limit, unit);
                }
                case PlacementWishKind.NextTo:
                {
                    float gap = tfp.HasValue ? WishGeometry.Gap(f.Footprint, tfp.Value) : WishGeometry.PointToFootprint(tpt, f.Footprint);
                    return new WishResult(w, gap <= w.Limit, gap, w.Limit, unit);
                }
                default:   // Facing
                {
                    TownPoint door = DoorPoint(f, turn), to = (tfp.HasValue ? tfp.Value.Centre : tpt) - door, out_ = DoorOutward(f, turn);
                    float len = to.Length;
                    float tol = w.Limit > 0f ? w.Limit : DefaultFacingDeg;
                    float angle = len < 1e-4f ? 0f : (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, (out_.X * to.X + out_.Z * to.Z) / len))) * 180.0 / Math.PI);
                    return new WishResult(w, angle <= tol + 1e-3f, angle, tol, unit);
                }
            }
        }

        static string UnitOf(PlacementWishKind k) => k == PlacementWishKind.Facing ? "deg" : k == PlacementWishKind.AtLot ? "m" : "m";

        // A wish target: a landmark or a foundation by id (its footprint), else the point X,Z.
        static bool Resolve(TownLayout layout, PlacementWish w, out Footprint? fp, out TownPoint pt)
        {
            fp = null; pt = new TownPoint(w.X, w.Z);
            if (w.TargetId == null) return true;
            Landmark l = layout.Landmark(w.TargetId);
            if (l != null) { fp = l.Footprint; return true; }
            Foundation f = layout.Foundation(w.TargetId);
            if (f != null) { fp = f.Footprint; return true; }
            return false;
        }
    }

    // Distances between oriented squares and points on the town plane.
    public static class WishGeometry
    {
        // Distance from a point to the footprint (0 inside).
        public static float PointToFootprint(TownPoint p, Footprint fp)
        {
            TownPoint l = fp.ToLocal(p);
            float dx = Math.Max(Math.Abs(l.X) - fp.HalfX, 0f), dz = Math.Max(Math.Abs(l.Z) - fp.HalfZ, 0f);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        // Nearest distance between two footprints (0 when they touch or overlap). For convex shapes that do not overlap the nearest pair involves a corner.
        public static float Gap(Footprint a, Footprint b)
        {
            if (a.Overlaps(b)) return 0f;
            float best = float.MaxValue;
            foreach (TownPoint c in a.Corners()) best = Math.Min(best, PointToFootprint(c, b));
            foreach (TownPoint c in b.Corners()) best = Math.Min(best, PointToFootprint(c, a));
            return best;
        }
    }
}
