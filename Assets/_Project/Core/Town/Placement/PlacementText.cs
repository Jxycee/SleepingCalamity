// Core/Town/Placement/PlacementText.cs: the plain words of placement mode (M6 S4), as pure functions so a test can pin them. PLACEHOLDER copy until the copy pass.
// WishLine: a wish with the measured number and a nudge when it is not met ("Near the fountain: 13 m of 12 m, a little closer"). Refusal: why the host said no.
// Authority: none (pure).
using System;
using System.Globalization;

namespace SleepingCalamity.Core
{
    public static class PlacementText
    {
        public static string WishLine(WishResult r)
        {
            string hint = r.Wish.Hint ?? "";
            if (float.IsNaN(r.Measured)) return hint;
            string m = r.Measured.ToString("0", CultureInfo.InvariantCulture);
            string l = r.Limit.ToString("0.#", CultureInfo.InvariantCulture);
            if (r.Met)
            {
                switch (r.Wish.Kind)
                {
                    case PlacementWishKind.AtLot: case PlacementWishKind.InArea: return hint;
                    case PlacementWishKind.FarFrom: return hint + ": " + m + " " + r.Unit + ", far enough";
                    case PlacementWishKind.Facing: return hint + ": " + m + " " + r.Unit + " off, good";
                    default: return hint + ": " + m + " " + r.Unit + ", close enough";
                }
            }
            switch (r.Wish.Kind)
            {
                case PlacementWishKind.AtLot: return hint + ": not this lot";
                case PlacementWishKind.InArea: return hint + ": " + m + " m outside";
                case PlacementWishKind.FarFrom: return hint + ": " + m + " m of " + l + " m, a little further";
                case PlacementWishKind.Facing: return hint + ": " + m + " " + r.Unit + " off (limit " + l + "), turn a little";
                default: return hint + ": " + m + " m of " + l + " m, a little closer";
            }
        }

        // Byte codes of TownSync results: PlaceResult values, plus 200 (not the builder) and 201 (no town on the host).
        public static string Refusal(byte code)
        {
            if (code == 200) return "Someone else is building, or the lock is gone";
            if (code == 201) return "The town is not ready";
            if (code > (byte)PlaceResult.PathBlocked) return "Not allowed";
            switch ((PlaceResult)code)
            {
                case PlaceResult.Ok: return "";
                case PlaceResult.PathBlocked: return "That would block the way for the townsfolk";
                case PlaceResult.FoundationOccupied: return "Something already stands on that lot";
                case PlaceResult.SizeMismatch: return "That lot is the wrong size";
                case PlaceResult.NoPieceInStock: return "No piece for that in stock";
                case PlaceResult.NoResidentInStock: return "He is not home yet";
                case PlaceResult.NotComplete: return "The building is not finished";
                case PlaceResult.AlreadyPlaced: case PlaceResult.PartAlreadyPlaced: case PlaceResult.ResidentAlreadyPlaced: return "Already there";
                case PlaceResult.NoFrame: case PlaceResult.NotPlaced: return "Place the frame first";
                default: return "Cannot do that";
            }
        }

        // The stock as one short line: "frame, oven x2, door, sign; the baker".
        public static string StockLine(TownState state)
        {
            if (state == null) return "";
            var counts = new System.Collections.Generic.SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (int id in state.StockPieces)
                if (PieceCatalog.TryGet(id, out PieceInfo info)) { counts.TryGetValue(info.Part, out int c); counts[info.Part] = c + 1; }
            var parts = new System.Collections.Generic.List<string>();
            foreach (var kv in counts) parts.Add(kv.Value > 1 ? kv.Key + " x" + kv.Value : kv.Key);
            string line = parts.Count == 0 ? "no pieces" : string.Join(", ", parts);
            foreach (string r in state.StockResidents) line += "; the " + r;
            return line;
        }
    }
}
