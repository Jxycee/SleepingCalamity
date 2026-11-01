// Core/Cellars/DiveHaul.cs: the party's ONE shared haul for a dive (PHASE1 7.2 "Checkpoint and banking"). Pure C#, no engine references.
// Holds bakery pieces (id + origin floor 1-3) and chest loot (item id + count), each either unbanked or banked. The host owns the instance;
// peers only see a summary (Presentation/Cellars/DiveHaulSync). Bank() makes everything carried safe. FailDive() loses the unbanked loot and sends
// unbanked pieces back to their origin floors (the pieces-left counters go up again), so nothing is ever missable. Banked stays.
// Only pieces feed the stir meter (UnbankedPieceCount); loot never does.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class DiveHaul
    {
        public const int PieceFloors = 3;   // pieces live on floors 1-3 (the baker is floor 4)

        private readonly int[] _assigned = new int[PieceFloors + 1];   // index = floor
        private readonly Dictionary<int, int> _originOf = new Dictionary<int, int>();
        private readonly SortedSet<int> _unbanked = new SortedSet<int>(), _banked = new SortedSet<int>();
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
        public int UnbankedPieceCount => _unbanked.Count;     // the stir meter reads this
        public int BankedPieceCount => _banked.Count;
        public int UnbankedLootCount { get { int t = 0; foreach (int c in _lootUnbanked.Values) t += c; return t; } }   // never counts for stir
        public int BankedLootCount { get { int t = 0; foreach (int c in _lootBanked.Values) t += c; return t; } }
        public int OriginFloor(int pieceId) => _originOf.TryGetValue(pieceId, out int f) ? f : 0;
        public bool IsCollected(int pieceId) => _unbanked.Contains(pieceId) || _banked.Contains(pieceId);
        public bool IsBanked(int pieceId) => _banked.Contains(pieceId);
        public IEnumerable<int> UnbankedPieces => _unbanked;
        public IEnumerable<int> BankedPieces => _banked;
        public IEnumerable<KeyValuePair<int, int>> BankedLoot => _lootBanked;   // item id -> count, ascending (the world save reads it)
        public int LootUnbanked(int itemId) => _lootUnbanked.TryGetValue(itemId, out int c) ? c : 0;
        public int LootBanked(int itemId) => _lootBanked.TryGetValue(itemId, out int c) ? c : 0;

        // Pieces assigned to the floor minus pieces collected (banked or unbanked). 0 for floors outside 1-3.
        public int PiecesLeft(int floor)
        {
            if (floor < 1 || floor > PieceFloors) return 0;
            int collected = 0;
            foreach (int id in _unbanked) if (OriginFloor(id) == floor) collected++;
            foreach (int id in _banked) if (OriginFloor(id) == floor) collected++;
            return _assigned[floor] - collected;
        }

        // False for an unknown piece or one already collected.
        public bool AddPiece(int pieceId)
        {
            if (!_originOf.ContainsKey(pieceId) || IsCollected(pieceId)) return false;
            _unbanked.Add(pieceId);
            return true;
        }

        public bool AddLoot(int itemId, int count)
        {
            if (count <= 0) return false;
            _lootUnbanked.TryGetValue(itemId, out int c);
            _lootUnbanked[itemId] = c + count;
            return true;
        }

        // Everything carried becomes safe.
        public void Bank()
        {
            foreach (int id in _unbanked) _banked.Add(id);
            _unbanked.Clear();
            foreach (KeyValuePair<int, int> kv in _lootUnbanked)
            {
                _lootBanked.TryGetValue(kv.Key, out int c);
                _lootBanked[kv.Key] = c + kv.Value;
            }
            _lootUnbanked.Clear();
        }

        // Unbanked loot is dropped; unbanked pieces go back to their floors (they simply stop being collected). Banked stays.
        public void FailDive()
        {
            _unbanked.Clear();
            _lootUnbanked.Clear();
        }
    }
}
