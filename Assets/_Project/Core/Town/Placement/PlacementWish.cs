// Core/Town/Placement/PlacementWish.cs: a resident's placement wish as plain data (PHASE1 7.1; ported from FishGame Core/Village/Placement.cs, whose wishes were
// AtLot / NearPoint / FarFrom over a lot index). Here a wish is measured by door and footprint geometry (M6 Q3 A), so a wish carries a target id (a landmark or a
// foundation of the layout) or a point, and WishEvaluator measures it for a building on a foundation with a turn. Pure C#, no engine references.
// Authority: none (static data); the host evaluates, every peer shows the same numbers from the same state.
using System;

namespace SleepingCalamity.Core
{
    // AtLot: the building stands on foundation TargetId.
    // NearPoint: the building's lot is within Limit metres of the target (gap to a landmark or foundation footprint, or to the point X,Z).
    // FarFrom: the building's lot is at least Limit metres from the target footprint (gap between the two footprints) or point.
    // NextTo: the lot's gap to the target footprint is at most Limit metres.
    // Facing: the door's outward direction is within Limit degrees of the direction to the target (45 degrees for "door faces the square").
    // InArea: the building's centre is inside the rectangle MinX..MaxX, MinZ..MaxZ.
    public enum PlacementWishKind : byte { AtLot, NearPoint, FarFrom, Facing, NextTo, InArea }

    public readonly struct PlacementWish
    {
        public readonly PlacementWishKind Kind;
        public readonly string TargetId;            // landmark id or foundation id; null when the wish names a point or an area
        public readonly float X, Z;                 // point target (when TargetId is null)
        public readonly float MinX, MinZ, MaxX, MaxZ;   // InArea
        public readonly float Limit;                // metres (NearPoint, FarFrom, NextTo) or degrees (Facing); unused by AtLot and InArea
        public readonly string Hint;                // the resident's words (plain, shown with a live tick)

        private PlacementWish(PlacementWishKind kind, string targetId, float x, float z, float minX, float minZ, float maxX, float maxZ, float limit, string hint)
        {
            Kind = kind; TargetId = targetId; X = x; Z = z; MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; Limit = limit; Hint = hint;
        }

        public static PlacementWish AtLot(string foundationId, string hint) => new PlacementWish(PlacementWishKind.AtLot, foundationId, 0f, 0f, 0f, 0f, 0f, 0f, 0f, hint);
        public static PlacementWish NearPoint(string targetId, float radiusM, string hint) => new PlacementWish(PlacementWishKind.NearPoint, targetId, 0f, 0f, 0f, 0f, 0f, 0f, radiusM, hint);
        public static PlacementWish NearPoint(float x, float z, float radiusM, string hint) => new PlacementWish(PlacementWishKind.NearPoint, null, x, z, 0f, 0f, 0f, 0f, radiusM, hint);
        public static PlacementWish FarFrom(string targetId, float minM, string hint) => new PlacementWish(PlacementWishKind.FarFrom, targetId, 0f, 0f, 0f, 0f, 0f, 0f, minM, hint);
        public static PlacementWish FarFrom(float x, float z, float minM, string hint) => new PlacementWish(PlacementWishKind.FarFrom, null, x, z, 0f, 0f, 0f, 0f, minM, hint);
        public static PlacementWish NextTo(string targetId, float maxGapM, string hint) => new PlacementWish(PlacementWishKind.NextTo, targetId, 0f, 0f, 0f, 0f, 0f, 0f, maxGapM, hint);
        public static PlacementWish Facing(string targetId, float toleranceDeg, string hint) => new PlacementWish(PlacementWishKind.Facing, targetId, 0f, 0f, 0f, 0f, 0f, 0f, toleranceDeg, hint);
        public static PlacementWish Facing(float x, float z, float toleranceDeg, string hint) => new PlacementWish(PlacementWishKind.Facing, null, x, z, 0f, 0f, 0f, 0f, toleranceDeg, hint);
        public static PlacementWish InArea(float minX, float minZ, float maxX, float maxZ, string hint) =>
            new PlacementWish(PlacementWishKind.InArea, null, 0f, 0f, Math.Min(minX, maxX), Math.Min(minZ, maxZ), Math.Max(minX, maxX), Math.Max(minZ, maxZ), 0f, hint);
    }
}
