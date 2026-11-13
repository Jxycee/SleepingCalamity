// Core/Game/LoopGuide.cs: the one "what to do next" line of the playable loop (PL-2 task 2.1). Pure C#, no engine, no network.
// Authority: none (a pure function). Every peer already holds the inputs (the town through TownSync.View, the dive through DiveHaulSync.Current), so each computes the same step locally.
// The words live in GuideText (a table, so a copy pass touches one file). A HUD fills a GuideSnapshot with GuideSnapshot.FromTown(...) and shows GuideText.For(LoopGuide.Next(...)).
namespace SleepingCalamity.Core
{
    public enum GuideKind : byte
    {
        GoToStair,        // at home with nothing to place: find the stair down
        PiecesLeftHere,   // in a dive, the floor still holds Count pieces
        StairDown,        // in a dive, the floor holds none: take the stair down
        GoToBuildPoint,   // at home, a frame waits in stock and no building stands
        PlaceParts,       // the frame stands; Count parts wait in stock
        DiveForMore,      // pieces are missing (Count of them); nothing in stock helps
        PlaceTheBaker,    // the bakery is whole and the baker waits in stock
        FindTheBaker,     // the bakery is whole and the baker has not come home
        MeetTheWishes,    // the baker lives there but the service is not open: his wishes
        DiveWithLoaves,   // the service is open: dive, loaves are waiting
    }

    // The inputs, small on purpose. Counts are what the player can act on.
    public struct GuideSnapshot
    {
        public bool InDive;
        public int Floor;               // the dive floor, 0 at home
        public int PiecesLeftOnFloor;   // not yet found on this floor (DiveHaulSync.Summary.PiecesLeft)
        public bool FrameInStock;       // a frame piece waits in the town stock
        public int PartsInStock;        // pieces in stock that the standing building can still use (when no building stands: all stock pieces)
        public bool FramePlaced;        // a building's frame stands
        public int PartsMissing;        // parts the standing building still needs (0 when whole, or when none stands)
        public bool BakerInStock, BakerPlaced;
        public bool ServiceOpen;        // the baker's service would grant loaves at the next dive start (BakerService.Evaluate(...).Granted)

        // From the host's or a peer's TownState (the bakery is the one building today) and the dive numbers every peer holds.
        public static GuideSnapshot FromTown(TownState town, bool inDive, int floor, int piecesLeftOnFloor)
        {
            var s = new GuideSnapshot { InDive = inDive, Floor = floor, PiecesLeftOnFloor = piecesLeftOnFloor };
            if (town == null) return s;
            BuildingDef def = TownBuildings.Get(TownBuildings.Bakery);
            PlacedBuilding placed = town.PlacedBuildingOf(TownBuildings.Bakery);
            s.FramePlaced = placed != null;
            s.BakerInStock = town.ResidentInStock(TownBuildings.BakerResident);
            s.BakerPlaced = placed != null && placed.Resident == TownBuildings.BakerResident;
            s.ServiceOpen = BakerService.Evaluate(town).Granted;
            foreach (int piece in town.StockPieces)
            {
                if (!PieceCatalog.TryGet(piece, out PieceInfo info) || info.Building != TownBuildings.Bakery) continue;
                if (info.Part == BuildingDef.FramePart) { s.FrameInStock = true; if (placed == null) s.PartsInStock++; continue; }
                if (placed == null) { s.PartsInStock++; continue; }
                int at = def.PartIndex(info.Part);
                if (at >= 0 && placed.PiecesByPart[at] < 0) s.PartsInStock++;
            }
            if (placed != null) for (int i = 0; i < placed.PiecesByPart.Length; i++) if (placed.PiecesByPart[i] < 0) s.PartsMissing++;
            return s;
        }
    }

    public readonly struct GuideStep
    {
        public readonly GuideKind Kind;
        public readonly int Count;      // the number the words carry (pieces left, parts to place, parts missing); 0 when the step has none
        public GuideStep(GuideKind kind, int count = 0) { Kind = kind; Count = count; }
        // Where the beacon belongs: true at the trapdoor, false at the build point; null when no beacon applies (in a dive).
        public bool? PointsAtStair => Kind == GuideKind.GoToStair || Kind == GuideKind.DiveForMore || Kind == GuideKind.FindTheBaker || Kind == GuideKind.DiveWithLoaves ? true
            : Kind == GuideKind.GoToBuildPoint || Kind == GuideKind.PlaceParts || Kind == GuideKind.PlaceTheBaker || Kind == GuideKind.MeetTheWishes ? false : (bool?)null;
    }

    public static class LoopGuide
    {
        public static GuideStep Next(in GuideSnapshot s)
        {
            if (s.InDive) return s.PiecesLeftOnFloor > 0 ? new GuideStep(GuideKind.PiecesLeftHere, s.PiecesLeftOnFloor) : new GuideStep(GuideKind.StairDown);
            if (s.ServiceOpen) return new GuideStep(GuideKind.DiveWithLoaves);
            if (!s.FramePlaced) return s.FrameInStock ? new GuideStep(GuideKind.GoToBuildPoint, s.PartsInStock) : new GuideStep(GuideKind.GoToStair);
            if (s.PartsMissing > 0) return s.PartsInStock > 0 ? new GuideStep(GuideKind.PlaceParts, s.PartsInStock) : new GuideStep(GuideKind.DiveForMore, s.PartsMissing);
            if (s.BakerPlaced) return new GuideStep(GuideKind.MeetTheWishes);
            return s.BakerInStock ? new GuideStep(GuideKind.PlaceTheBaker) : new GuideStep(GuideKind.FindTheBaker);
        }
    }
}
