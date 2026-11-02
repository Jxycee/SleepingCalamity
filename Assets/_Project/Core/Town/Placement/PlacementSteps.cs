// Core/Town/Placement/PlacementSteps.cs: what the next "place" press and the next "pick up" press do for one building (M6 S4 placement mode), as pure functions of the TownState.
// Place: the frame first (when the building is not standing), then the next missing part that has a piece in stock, then the resident once the building is complete.
// Pick up: the resident first, then the last filled part, then the building itself (which returns the frame). Pick-up is never refused (TownState).
// Authority: none (pure). The UI picks the step locally; the host re-checks every rule when the request arrives.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum PlaceStepKind : byte { None, Frame, Part, Resident }

    public readonly struct PlaceStep
    {
        public readonly PlaceStepKind Kind; public readonly string Part;
        public PlaceStep(PlaceStepKind kind, string part = "") { Kind = kind; Part = part; }
    }

    public static class PlacementSteps
    {
        public static bool HasFrameInStock(TownState state, string buildingId)
        {
            foreach (int id in state.StockPieces)
                if (PieceCatalog.TryGet(id, out PieceInfo info) && info.Building == buildingId && info.Part == BuildingDef.FramePart) return true;
            return false;
        }

        public static bool HasPartInStock(TownState state, string buildingId, string part)
        {
            foreach (int id in state.StockPieces)
                if (PieceCatalog.TryGet(id, out PieceInfo info) && info.Building == buildingId && info.Part == part) return true;
            return false;
        }

        public static PlaceStep Next(TownState state, string buildingId)
        {
            BuildingDef def = TownBuildings.Get(buildingId);
            if (state == null || def == null) return new PlaceStep(PlaceStepKind.None);
            PlacedBuilding p = state.PlacedBuildingOf(buildingId);
            if (p == null) return HasFrameInStock(state, buildingId) ? new PlaceStep(PlaceStepKind.Frame) : new PlaceStep(PlaceStepKind.None);
            for (int i = 1; i < def.Parts.Length; i++)
                if (p.PiecesByPart[i] < 0 && HasPartInStock(state, buildingId, def.Parts[i])) return new PlaceStep(PlaceStepKind.Part, def.Parts[i]);
            if (p.Resident == null && state.IsComplete(buildingId) && state.ResidentInStock(def.ResidentId)) return new PlaceStep(PlaceStepKind.Resident);
            return new PlaceStep(PlaceStepKind.None);
        }

        // What a pick-up press returns: Resident, a Part (the last filled one), or Frame (the whole building). None when the building is not standing.
        public static PlaceStep NextPickUp(TownState state, string buildingId)
        {
            BuildingDef def = TownBuildings.Get(buildingId);
            PlacedBuilding p = state != null && def != null ? state.PlacedBuildingOf(buildingId) : null;
            if (p == null) return new PlaceStep(PlaceStepKind.None);
            if (p.Resident != null) return new PlaceStep(PlaceStepKind.Resident);
            for (int i = def.Parts.Length - 1; i >= 1; i--)
                if (p.PiecesByPart[i] >= 0) return new PlaceStep(PlaceStepKind.Part, def.Parts[i]);
            return new PlaceStep(PlaceStepKind.Frame);
        }

        // The foundations of the building's size class, in layout order (the ghost cycles through the free ones).
        public static List<string> CandidateFoundations(TownLayout layout, BuildingDef def)
        {
            var l = new List<string>();
            foreach (Foundation f in layout.Foundations) if (f.Size == def.Size) l.Add(f.Id);
            return l;
        }

        // The nearest candidate foundation to a ground point (x, z) that is free of buildings; null when there is none.
        public static string NearestFree(TownState state, BuildingDef def, float x, float z)
        {
            string best = null; float bestD = float.MaxValue;
            foreach (Foundation f in state.Layout.Foundations)
            {
                if (f.Size != def.Size || state.OccupantOf(f.Id) != null) continue;
                float dx = f.Centre.X - x, dz = f.Centre.Z - z, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = f.Id; }
            }
            return best;
        }
    }
}
