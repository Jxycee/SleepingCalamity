// Core/Town/Placement/TownBuildings.cs: the placeable buildings of the town as static data (PHASE1 7.1; ported from FishGame's Buildings table). M6 has one: the
// bakery (size class L, four parts: frame first, then oven, door and sign, plus the baker). A resident belongs to a building and moves in only when every part
// is placed (P28, DC1 completion gate). Wishes are the resident's, equal weight (DC1), words PLACEHOLDER until the copy pass.
// Authority: none (static data).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class BuildingDef
    {
        public readonly string Id, DisplayName, ResidentId;
        public readonly FoundationSize Size;
        public readonly string[] Parts;          // Parts[0] is the frame; the rest slot onto the frame in any order
        public readonly PlacementWish[] Wishes;

        public BuildingDef(string id, string displayName, FoundationSize size, string[] parts, string residentId, PlacementWish[] wishes)
        {
            Id = id; DisplayName = displayName; Size = size; Parts = parts; ResidentId = residentId; Wishes = wishes;
        }

        public const string FramePart = "frame";
        public string Frame => Parts[0];
        public int PartIndex(string part) { for (int i = 0; i < Parts.Length; i++) if (Parts[i] == part) return i; return -1; }
    }

    public static class TownBuildings
    {
        public const string Bakery = "bakery", BakerResident = "baker";
        public const string Fountain = "DryFountain", Inn = "F09";          // HollowmereLayout landmark and the inn's foundation (rebuilt preset)
        public const float BakerNearFountainM = 12f, BakerFacingDeg = 45f, BakerInnAwayM = 8f;   // PHASE1 7.1 [tune]

        // "The square" is the middle of HollowmereLayout's 40 x 30 m square (the fountain sits off-centre at 0,0, so facing the fountain is a stricter wish).
        public const float SquareCentreX = (HollowmereLayout.SquareMinX + HollowmereLayout.SquareMaxX) * 0.5f, SquareCentreZ = (HollowmereLayout.SquareMinZ + HollowmereLayout.SquareMaxZ) * 0.5f;

        public static readonly IReadOnlyList<BuildingDef> All = new[]
        {
            new BuildingDef(Bakery, "The bakery", FoundationSize.L, new[] { BuildingDef.FramePart, "oven", "door", "sign" }, BakerResident, new[]
            {
                PlacementWish.NearPoint(Fountain, BakerNearFountainM, "Near the fountain, so folk pass my door"),
                PlacementWish.Facing(SquareCentreX, SquareCentreZ, BakerFacingDeg, "My door facing the square"),
                PlacementWish.FarFrom(Inn, BakerInnAwayM, "Not right against the inn"),
            }),
        };

        public static BuildingDef Get(string id) { foreach (BuildingDef b in All) if (b.Id == id) return b; return null; }
        public static BuildingDef OfResident(string residentId) { foreach (BuildingDef b in All) if (b.ResidentId == residentId) return b; return null; }
    }
}
