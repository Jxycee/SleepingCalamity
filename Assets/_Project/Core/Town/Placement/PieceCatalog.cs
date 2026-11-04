// Core/Town/Placement/PieceCatalog.cs: which building and part each dive piece id is (PHASE1 7.1 / P28). Piece ids are DiveHaul.PieceId(floor, index) = floor*100+index
// and exist on floors 1-3; the baker is the floor-3 rescue (id 400), a resident rather than a piece.
// M5 spawns 2 pieces per floor (6 ids) but the bakery has 4 parts, so floor 3 holds a second frame and a second oven: any piece of a part fills that part, and the
// spare stays in stock (nothing is lost; a later session may drop the default to {1,1,2} and the table still holds).
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
        public const int BakerId = 400;   // the baker's rescue entry (the id is kept from the old floor-4 bakery)

        public static readonly IReadOnlyList<PieceInfo> All = new[]
        {
            new PieceInfo(DiveHaul.PieceId(1, 0), TownBuildings.Bakery, BuildingDef.FramePart),
            new PieceInfo(DiveHaul.PieceId(1, 1), TownBuildings.Bakery, "oven"),
            new PieceInfo(DiveHaul.PieceId(2, 0), TownBuildings.Bakery, "door"),
            new PieceInfo(DiveHaul.PieceId(2, 1), TownBuildings.Bakery, "sign"),
            new PieceInfo(DiveHaul.PieceId(3, 0), TownBuildings.Bakery, BuildingDef.FramePart),   // spare frame
            new PieceInfo(DiveHaul.PieceId(3, 1), TownBuildings.Bakery, "oven"),                  // spare oven
        };

        // Where each id lives (plan 3 decision 10). The id no longer encodes a floor (the PieceId(floor, index) numbers are only kept so old saves keep their pieces): 100/101/200/201 land
        // on a random first-half generated floor (2 or 4), the spare frame and oven (300/301) on a random second-half one (6 or 7), the baker (400) is fixed on floor 3.
        // PieceDepths.Assign(worldSeed, Specs) runs once per world and the save keeps the result.
        public static readonly IReadOnlyList<PieceSpec> Specs = new[]
        {
            new PieceSpec(100, 0, false), new PieceSpec(101, 0, false),
            new PieceSpec(200, 0, false), new PieceSpec(201, 0, false),
            new PieceSpec(300, 0, true),  new PieceSpec(301, 0, true),
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
