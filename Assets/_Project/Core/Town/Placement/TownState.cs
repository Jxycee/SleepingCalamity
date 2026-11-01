// Core/Town/Placement/TownState.cs: the runtime placement ledger of the town (PHASE1 7.1; ported from FishGame's VillagePlacements, which kept one lot per building).
// Holds the STOCK (dive pieces by id and rescued residents, banked and brought home) and the PLACED buildings: which foundation each stands on, its turn (0-3 quarter
// turns from the lot's own facing), the parts slotted on its frame (each remembers the piece id it consumed) and its resident.
// Rules (each refusal is a PlaceResult; nothing changes on a refusal):
//   size class must match the foundation; a foundation holds one building; a building is placed once; the frame (a piece of its frame part) goes first, then parts;
//   the resident moves in only when every part is placed; a frame or turn that would cut the NPC path graph is refused (PathRule, through TownLayout.RootNode).
//   Pick-up is ALWAYS allowed and never refused: a building returns its frame, parts and resident to stock; a part returns its piece (and the resident if the building
//   is no longer complete). So no layout can soft-lock the world, and stock + placed never changes by a place or pick-up (Conservation).
// Authority: host writes (M6 S4 TownSync); clients send requests and the host re-checks every rule. Not networked or saved here (S2 does the save sections).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum PlaceResult : byte
    {
        Ok, UnknownBuilding, UnknownFoundation, UnknownPart, UnknownResident, UnknownPiece, AlreadyKnown,
        NoPieceInStock, NoResidentInStock, SizeMismatch, FoundationOccupied, AlreadyPlaced, NotPlaced, NoFrame, PartAlreadyPlaced, NotComplete, ResidentAlreadyPlaced, PathBlocked,
    }

    public sealed class PlacedBuilding
    {
        public string BuildingId, FoundationId;
        public int Turn;
        public readonly int[] PiecesByPart;   // per BuildingDef.Parts index: the piece id consumed, or -1 (index 0 is the frame, always set while placed)
        public string Resident;               // null until the resident moves in

        public PlacedBuilding(BuildingDef def, string foundationId, int turn, int framePiece)
        {
            BuildingId = def.Id; FoundationId = foundationId; Turn = turn;
            PiecesByPart = new int[def.Parts.Length];
            for (int i = 0; i < PiecesByPart.Length; i++) PiecesByPart[i] = -1;
            PiecesByPart[0] = framePiece;
        }
    }

    public sealed class TownState
    {
        private readonly TownLayout _layout;
        private readonly Dictionary<string, List<string>> _adj;
        private readonly SortedSet<int> _stockPieces = new SortedSet<int>();
        private readonly SortedSet<string> _stockResidents = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, PlacedBuilding> _placed = new SortedDictionary<string, PlacedBuilding>(StringComparer.Ordinal);   // by building id

        public TownState(TownLayout layout) { _layout = layout; _adj = layout.Adjacency(); }

        public TownLayout Layout => _layout;
        public IEnumerable<int> StockPieces => _stockPieces;
        public IEnumerable<string> StockResidents => _stockResidents;
        public IEnumerable<PlacedBuilding> Placed => _placed.Values;
        public bool InStock(int pieceId) => _stockPieces.Contains(pieceId);
        public bool ResidentInStock(string residentId) => _stockResidents.Contains(residentId);
        public PlacedBuilding PlacedBuildingOf(string buildingId) => buildingId != null && _placed.TryGetValue(buildingId, out PlacedBuilding p) ? p : null;
        public PlacedBuilding OccupantOf(string foundationId) { foreach (PlacedBuilding p in _placed.Values) if (p.FoundationId == foundationId) return p; return null; }

        public bool IsComplete(string buildingId)
        {
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            if (p == null) return false;
            foreach (int piece in p.PiecesByPart) if (piece < 0) return false;
            return true;
        }

        // ---------------------------------------------------------------- stock
        // A piece or the baker coming home. False for an id the catalog does not know, or one already in stock or placed (a piece exists once).
        public PlaceResult AddPiece(int pieceId)
        {
            if (!PieceCatalog.TryGet(pieceId, out _)) return PlaceResult.UnknownPiece;
            if (_stockPieces.Contains(pieceId) || IsPlacedPiece(pieceId)) return PlaceResult.AlreadyKnown;
            _stockPieces.Add(pieceId); return PlaceResult.Ok;
        }

        public PlaceResult AddResident(string residentId)
        {
            if (TownBuildings.OfResident(residentId) == null) return PlaceResult.UnknownResident;
            if (_stockResidents.Contains(residentId) || IsPlacedResident(residentId)) return PlaceResult.AlreadyKnown;
            _stockResidents.Add(residentId); return PlaceResult.Ok;
        }

        // Everything the town holds, stock plus placed: piece ids and resident ids. A place or a pick-up never changes this set.
        public SortedSet<string> Conservation()
        {
            var all = new SortedSet<string>(StringComparer.Ordinal);
            foreach (int p in _stockPieces) all.Add("piece:" + p);
            foreach (string r in _stockResidents) all.Add("resident:" + r);
            foreach (PlacedBuilding b in _placed.Values)
            {
                foreach (int p in b.PiecesByPart) if (p >= 0) all.Add("piece:" + p);
                if (b.Resident != null) all.Add("resident:" + b.Resident);
            }
            return all;
        }

        // ---------------------------------------------------------------- placing
        public PlaceResult PlaceFrame(string buildingId, string foundationId, int turn)
        {
            PlaceResult can = CanPlaceFrame(buildingId, foundationId, turn);
            if (can != PlaceResult.Ok) return can;
            BuildingDef def = TownBuildings.Get(buildingId);
            int piece = FirstStockPiece(def, def.Frame);
            _stockPieces.Remove(piece);
            _placed[buildingId] = new PlacedBuilding(def, foundationId, WishEvaluator.NormalTurn(turn), piece);
            return PlaceResult.Ok;
        }

        // Turns a placed building (the foundation snap keeps it on its lot); refused if the new door side cuts the path graph.
        public PlaceResult SetTurn(string buildingId, int turn)
        {
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            if (p == null) return PlaceResult.NotPlaced;
            int old = p.Turn; p.Turn = WishEvaluator.NormalTurn(turn);
            if (PathRule.Blocked(_layout, _adj, Plus(null))) { p.Turn = old; return PlaceResult.PathBlocked; }
            return PlaceResult.Ok;
        }

        // Slots a part (a piece of that part from stock) onto the placed frame.
        public PlaceResult PlacePart(string buildingId, string part)
        {
            BuildingDef def = TownBuildings.Get(buildingId);
            if (def == null) return PlaceResult.UnknownBuilding;
            int idx = def.PartIndex(part);
            if (idx < 0) return PlaceResult.UnknownPart;
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            if (p == null) return PlaceResult.NoFrame;
            if (idx == 0) return PlaceResult.PartAlreadyPlaced;
            if (p.PiecesByPart[idx] >= 0) return PlaceResult.PartAlreadyPlaced;
            int piece = FirstStockPiece(def, part);
            if (piece < 0) return PlaceResult.NoPieceInStock;
            _stockPieces.Remove(piece);
            p.PiecesByPart[idx] = piece;
            return PlaceResult.Ok;
        }

        // The resident moves into a complete building.
        public PlaceResult PlaceResident(string buildingId)
        {
            BuildingDef def = TownBuildings.Get(buildingId);
            if (def == null) return PlaceResult.UnknownBuilding;
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            if (p == null) return PlaceResult.NotPlaced;
            if (p.Resident != null) return PlaceResult.ResidentAlreadyPlaced;
            if (!IsComplete(buildingId)) return PlaceResult.NotComplete;
            if (!_stockResidents.Remove(def.ResidentId)) return PlaceResult.NoResidentInStock;
            p.Resident = def.ResidentId;
            return PlaceResult.Ok;
        }

        // ---------------------------------------------------------------- picking up (never refused for a thing that is placed)
        // The whole building: frame, every part and the resident go back to stock. False only when it is not placed.
        public bool PickUpBuilding(string buildingId)
        {
            if (!_placed.TryGetValue(buildingId, out PlacedBuilding p)) return false;
            foreach (int piece in p.PiecesByPart) if (piece >= 0) _stockPieces.Add(piece);
            if (p.Resident != null) _stockResidents.Add(p.Resident);
            _placed.Remove(buildingId);
            return true;
        }

        // One part (not the frame: pick the building up for that). If the building is no longer complete the resident goes back to stock too.
        public bool PickUpPart(string buildingId, string part)
        {
            BuildingDef def = TownBuildings.Get(buildingId);
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            if (def == null || p == null) return false;
            int idx = def.PartIndex(part);
            if (idx == 0) return PickUpBuilding(buildingId);
            if (idx < 0 || p.PiecesByPart[idx] < 0) return false;
            _stockPieces.Add(p.PiecesByPart[idx]); p.PiecesByPart[idx] = -1;
            if (p.Resident != null) { _stockResidents.Add(p.Resident); p.Resident = null; }
            return true;
        }

        public bool PickUpResident(string buildingId)
        {
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            if (p == null || p.Resident == null) return false;
            _stockResidents.Add(p.Resident); p.Resident = null;
            return true;
        }

        // ---------------------------------------------------------------- wishes and the preview
        // The wishes of a placed building as they stand. Null when it is not placed.
        public WishReport Wishes(string buildingId)
        {
            PlacedBuilding p = PlacedBuildingOf(buildingId);
            BuildingDef def = TownBuildings.Get(buildingId);
            return p == null || def == null ? null : WishEvaluator.Evaluate(_layout, def, p.FoundationId, p.Turn);
        }

        // The live preview while a frame is held over a foundation (nothing is placed); also the check a path-blocked or occupied lot would fail.
        public PlaceResult CanPlaceFrame(string buildingId, string foundationId, int turn)
        {
            BuildingDef def = TownBuildings.Get(buildingId);
            if (def == null) return PlaceResult.UnknownBuilding;
            Foundation f = _layout.Foundation(foundationId);
            if (f == null) return PlaceResult.UnknownFoundation;
            if (f.Size != def.Size) return PlaceResult.SizeMismatch;
            if (OccupantOf(foundationId) != null) return PlaceResult.FoundationOccupied;
            if (_placed.ContainsKey(buildingId)) return PlaceResult.AlreadyPlaced;
            int piece = FirstStockPiece(def, def.Frame);
            if (piece < 0) return PlaceResult.NoPieceInStock;
            return PathRule.Blocked(_layout, _adj, Plus(new PlacedBuilding(def, foundationId, WishEvaluator.NormalTurn(turn), piece))) ? PlaceResult.PathBlocked : PlaceResult.Ok;
        }

        // ---------------------------------------------------------------- helpers
        private int FirstStockPiece(BuildingDef def, string part)
        {
            foreach (int id in _stockPieces) if (PieceCatalog.TryGet(id, out PieceInfo info) && info.Building == def.Id && info.Part == part) return id;
            return -1;
        }

        private bool IsPlacedPiece(int pieceId) { foreach (PlacedBuilding b in _placed.Values) foreach (int p in b.PiecesByPart) if (p == pieceId) return true; return false; }
        private bool IsPlacedResident(string id) { foreach (PlacedBuilding b in _placed.Values) if (b.Resident == id) return true; return false; }

        // The placed buildings plus one more (null adds none).
        private List<PlacedBuilding> Plus(PlacedBuilding extra)
        {
            var l = new List<PlacedBuilding>(_placed.Values);
            if (extra != null) l.Add(extra);
            return l;
        }
    }

    // The NPC path rule (PHASE1 7.1 "a building that would block an NPC path is refused"). A placed building walls off the doorsteps on its three non-door sides (its
    // door side stays open), so the lot's ring no longer carries traffic past those sides. The town is refused when, from TownLayout.RootNode over the remaining
    // graph, a landmark door, the door doorstep of a placed building, or any doorstep of an empty foundation can no longer be reached. (Corner nodes of a walled
    // lot are not destinations, so they do not count.)
    public static class PathRule
    {
        public static bool Blocked(TownLayout layout, Dictionary<string, List<string>> adj, IReadOnlyList<PlacedBuilding> placed) => FirstUnreachable(layout, adj, placed) != null;

        // The id of the first node that can no longer be reached, or null when the path graph still serves everything.
        public static string FirstUnreachable(TownLayout layout, Dictionary<string, List<string>> adj, IReadOnlyList<PlacedBuilding> placed)
        {
            var closed = new HashSet<string>();
            var byLot = new Dictionary<string, PlacedBuilding>();
            foreach (PlacedBuilding b in placed)
            {
                Foundation f = layout.Foundation(b.FoundationId);
                if (f == null) continue;
                byLot[f.Id] = b;
                LotSide door = WishEvaluator.DoorSide(f, b.Turn);
                foreach (LotSide s in TownLayout.AllSides) if (s != door) closed.Add(f.DoorstepId(s));
            }

            var seen = new HashSet<string>();
            string root = layout.RootNode;
            if (root != null && adj.ContainsKey(root) && !closed.Contains(root))
            {
                var q = new Queue<string>(); q.Enqueue(root); seen.Add(root);
                while (q.Count > 0) foreach (string n in adj[q.Dequeue()]) if (!closed.Contains(n) && seen.Add(n)) q.Enqueue(n);
            }

            foreach (Landmark l in layout.Landmarks) if (l.DoorNode != null && !seen.Contains(l.DoorNode)) return l.DoorNode;
            foreach (Foundation f in layout.Foundations)
            {
                if (byLot.TryGetValue(f.Id, out PlacedBuilding b))
                {
                    string d = f.DoorstepId(WishEvaluator.DoorSide(f, b.Turn));
                    if (!seen.Contains(d)) return d;
                }
                else
                    foreach (LotSide s in TownLayout.AllSides) { string d = f.DoorstepId(s); if (!seen.Contains(d)) return d; }
            }
            return null;
        }
    }
}
