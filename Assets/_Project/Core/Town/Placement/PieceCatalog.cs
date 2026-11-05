// Core/Town/Placement/PieceCatalog.cs: which building and part each dive piece id is (PHASE1 7.1 / P28). The ids (100, 101, 200, 201, 300, 301) are kept from the first dive so old saves keep
// their pieces; they no longer encode a floor (Specs says where each lives, PieceDepths.Assign decides it once per world). The baker is the floor-3 rescue (id 400), a resident rather than a piece.
// The bakery has 4 parts but there are 6 piece ids: a second frame and a second oven (300, 301): any piece of a part fills that part, and the spare stays in stock (nothing is lost).
// Authority: none (static data).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public readonly struct PieceInfo
    {
        public readonly int Id; public readonly string Building, Part;
        public PieceInfo(int id, string building, string part) { Id = id; Building = building; Part = part; }
    }

    public static class PieceCatalog
    {
        public const int FrameId = 100, OvenId = 101, DoorId = 200, SignId = 201, SpareFrameId = 300, SpareOvenId = 301;   // the six bakery piece ids
        public const int BakerId = 400;   // the baker's rescue entry (the id is kept from the old floor-4 bakery)

        public static readonly IReadOnlyList<PieceInfo> All = new[]
        {
            new PieceInfo(FrameId, TownBuildings.Bakery, BuildingDef.FramePart),
            new PieceInfo(OvenId, TownBuildings.Bakery, "oven"),
            new PieceInfo(DoorId, TownBuildings.Bakery, "door"),
            new PieceInfo(SignId, TownBuildings.Bakery, "sign"),
            new PieceInfo(SpareFrameId, TownBuildings.Bakery, BuildingDef.FramePart),
            new PieceInfo(SpareOvenId, TownBuildings.Bakery, "oven"),
        };

        // Where each id lives (plan 3 decision 10). The id no longer encodes a floor (the PieceId(floor, index) numbers are only kept so old saves keep their pieces): 100/101/200/201 land
        // on a random first-half generated floor (2 or 4), the spare frame and oven (300/301) on a random second-half one (6 or 7), the baker (400) is fixed on floor 3.
        // PieceDepths.Assign(worldSeed, Specs) runs once per world and the save keeps the result.
        public static readonly IReadOnlyList<PieceSpec> Specs = new[]
        {
            new PieceSpec(FrameId, 0, false), new PieceSpec(OvenId, 0, false),
            new PieceSpec(DoorId, 0, false), new PieceSpec(SignId, 0, false),
            new PieceSpec(SpareFrameId, 0, true),  new PieceSpec(SpareOvenId, 0, true),
            new PieceSpec(BakerId, BakerFloor, false),
        };

        public const int BakerFloor = 3;   // the baker sleeps in the bakery set piece

        public static bool TryGet(int pieceId, out PieceInfo info)
        {
            foreach (PieceInfo p in All) if (p.Id == pieceId) { info = p; return true; }
            info = default; return false;
        }

        public static bool IsBaker(int pieceId) => pieceId == BakerId;
        public static bool IsPerson(int id) => id == BakerId;   // the only rescued person so far; a person is never a building piece
        public static string ResidentOf(int pieceId) => pieceId == BakerId ? TownBuildings.BakerResident : null;
    }
}
