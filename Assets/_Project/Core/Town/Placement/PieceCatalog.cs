// Core/Town/Placement/PieceCatalog.cs: which building and part each dive piece id is (PHASE1 7.1 / P28). Piece ids are DiveHaul.PieceId(floor, index) = floor*100+index
// and exist on floors 1-3; the baker is the floor-4 rescue (id 400), a resident rather than a piece.
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
        public const int BakerId = 400;   // floor 4 * 100 + 0: the baker's rescue entry in the haul

        public static readonly IReadOnlyList<PieceInfo> All = new[]
        {
            new PieceInfo(DiveHaul.PieceId(1, 0), TownBuildings.Bakery, BuildingDef.FramePart),
            new PieceInfo(DiveHaul.PieceId(1, 1), TownBuildings.Bakery, "oven"),
            new PieceInfo(DiveHaul.PieceId(2, 0), TownBuildings.Bakery, "door"),
            new PieceInfo(DiveHaul.PieceId(2, 1), TownBuildings.Bakery, "sign"),
            new PieceInfo(DiveHaul.PieceId(3, 0), TownBuildings.Bakery, BuildingDef.FramePart),   // spare frame
            new PieceInfo(DiveHaul.PieceId(3, 1), TownBuildings.Bakery, "oven"),                  // spare oven
        };

        public static bool TryGet(int pieceId, out PieceInfo info)
        {
            foreach (PieceInfo p in All) if (p.Id == pieceId) { info = p; return true; }
            info = default; return false;
        }

        public static bool IsBaker(int pieceId) => pieceId == BakerId;
        public static string ResidentOf(int pieceId) => pieceId == BakerId ? TownBuildings.BakerResident : null;
    }
}
