// Core/Cellars/DiveHaul.cs: the party's ONE shared haul for a dive (PHASE1 7.2 "Checkpoint and banking"). Pure C#, no engine references.
// Per-player loot plan (task 3, spec 4.4): a bakery piece (id + origin floor 1-3) is safe the moment it is picked up (AddPiece banks it at once) and a failed dive keeps
// every collected piece, so pieces never return to a floor and never feed the stir meter. The piece set stays shared; keys, pieces and people are one copy per party.
// The legacy chest-loot members (AddLoot, unbanked/banked counts, Bank, FailDive dropping unbanked loot) stay ONLY so old saves (stock.loot, dive.bankedLoot) restore;
// chest loot still reaches them through DiveHaulSync.ServerAddLoot until plan 2 task 6 moves chests to the per-player bags; plan 3 deletes them together with this class's loot path. The host owns the instance; peers only see a summary
// (Presentation/Cellars/DiveHaulSync).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class DiveHaul
    {
        public const int PieceFloors = 3;   // pieces live on floors 1-3 (the baker is floor 4)

        private readonly int[] _assigned = new int[PieceFloors + 1];   // index = floor
        private readonly Dictionary<int, int> _originOf = new Dictionary<int, int>();
        private readonly SortedSet<int> _banked = new SortedSet<int>();   // collected pieces: safe from the moment they are picked up
        private readonly SortedSet<int> _home = new SortedSet<int>();   // restored from a save as already in the town; never banked, never lost to FailDive
        private readonly SortedDictionary<int, int> _lootUnbanked = new SortedDictionary<int, int>(), _lootBanked = new SortedDictionary<int, int>();

        // piecesPerFloor[0..2] = pieces assigned to floors 1-3. Ids are deterministic: PieceId(floor, index).
        public DiveHaul(IReadOnlyList<int> piecesPerFloor)
        {
            for (int f = 1; f <= PieceFloors; f++)
            {
                int n = f - 1 < piecesPerFloor.Count ? System.Math.Max(0, piecesPerFloor[f - 1]) : 0;
                _assigned[f] = n;
                for (int i = 0; i < n; i++) _originOf[PieceId(f, i)] = f;
            }
        }

        public static int PieceId(int floor, int index) => floor * 100 + index;

        public int TotalPieces { get { int t = 0; for (int f = 1; f <= PieceFloors; f++) t += _assigned[f]; return t; } }
        public int BankedPieceCount => _banked.Count;         // every piece picked up (or restored as banked), all safe
        public int FoundThisDive { get; private set; }        // pieces AddPiece took since this haul was made (a world load's restored pieces do not count); the stats overlay reads it
        public int UnbankedLootCount { get { int t = 0; foreach (int c in _lootUnbanked.Values) t += c; return t; } }   // never counts for stir
        public int BankedLootCount { get { int t = 0; foreach (int c in _lootBanked.Values) t += c; return t; } }
        public int OriginFloor(int pieceId) => _originOf.TryGetValue(pieceId, out int f) ? f : 0;
        public bool IsCollected(int pieceId) => _banked.Contains(pieceId) || _home.Contains(pieceId);
        public bool IsBanked(int pieceId) => _banked.Contains(pieceId);
        public bool IsHome(int pieceId) => _home.Contains(pieceId);   // restored from a save as already in the town
        public int AssignedOn(int floor) => floor >= 1 && floor <= PieceFloors ? _assigned[floor] : 0;   // pieces assigned to the floor (ids PieceId(floor, 0..n-1), some possibly home); a floor's spots map to the ids that are not home
        public IEnumerable<int> BankedPieces => _banked;
        public IEnumerable<KeyValuePair<int, int>> BankedLoot => _lootBanked;   // item id -> count, ascending (the world save reads it)
        public IEnumerable<KeyValuePair<int, int>> BankedLootItems => _lootBanked;   // same as BankedLoot; the town stock reads it (running banked total for the session)
        public int LootUnbanked(int itemId) => _lootUnbanked.TryGetValue(itemId, out int c) ? c : 0;
        public int LootBanked(int itemId) => _lootBanked.TryGetValue(itemId, out int c) ? c : 0;

        // Pieces assigned to the floor minus pieces collected (banked or home). 0 for floors outside 1-3.
        public int PiecesLeft(int floor)
        {
            if (floor < 1 || floor > PieceFloors) return 0;
            int collected = 0;
            foreach (int id in _banked) if (OriginFloor(id) == floor) collected++;
            foreach (int id in _home) if (OriginFloor(id) == floor) collected++;
            return _assigned[floor] - collected;
        }

        // A piece picked up: it is safe at once (banked), a failed dive keeps it. False for an unknown piece or one already collected.
        public bool AddPiece(int pieceId)
        {
            if (!RestoreBankedPiece(pieceId)) return false;
            FoundThisDive++;
            return true;
        }

        // A new dive starts: the found-this-dive counter goes back to 0 (the pieces themselves stay collected).
        public void ResetFoundThisDive() { FoundThisDive = 0; }

        // A world load: this piece was banked in the saved haul. Same as AddPiece without counting as found this dive. False for an unknown piece or one already collected.
        public bool RestoreBankedPiece(int pieceId)
        {
            if (!_originOf.ContainsKey(pieceId) || IsCollected(pieceId)) return false;
            _banked.Add(pieceId);
            return true;
        }

        // A world load: this piece is already in the town (stock or placed), so it counts as collected (the cellar must not spawn it again) without being banked or
        // carried. False for an unknown piece or one already collected.
        public bool MarkHome(int pieceId)
        {
            if (!_originOf.ContainsKey(pieceId) || IsCollected(pieceId)) return false;
            _home.Add(pieceId);
            return true;
        }

        public bool AddLoot(int itemId, int count)
        {
            if (count <= 0) return false;
            _lootUnbanked.TryGetValue(itemId, out int c);
            _lootUnbanked[itemId] = c + count;
            return true;
        }

        // The legacy unbanked chest loot becomes banked (pieces are already safe).
        public void Bank()
        {
            foreach (KeyValuePair<int, int> kv in _lootUnbanked)
            {
                _lootBanked.TryGetValue(kv.Key, out int c);
                _lootBanked[kv.Key] = c + kv.Value;
            }
            _lootUnbanked.Clear();
        }

        // Legacy unbanked loot is dropped. Every collected piece stays collected (spec 4.4: pieces are safe on pickup), banked loot stays.
        public void FailDive()
        {
            _lootUnbanked.Clear();
        }
    }
}
