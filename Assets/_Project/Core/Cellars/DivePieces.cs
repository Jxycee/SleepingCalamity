// Core/Cellars/DivePieces.cs: which bakery pieces and people the party has found, and the floor each one lives on (spec 4.4, decision 10). Pure C#.
// Authority: host only (owned by DiveDirector; a DiveSession reads it). Depths come from PieceDepths.Assign(worldSeed, PieceCatalog.Specs), saved once per world; a person is a
// PieceCatalog.IsPerson id (the baker). A found piece is safe at once: it is never banked and never lost on failure.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class DivePieces
    {
        readonly SortedDictionary<int, int> _depth = new SortedDictionary<int, int>();   // id -> floor
        readonly SortedSet<int> _found = new SortedSet<int>();

        public DivePieces(IReadOnlyDictionary<int, int> depths)
        {
            if (depths != null) foreach (var kv in depths) _depth[kv.Key] = kv.Value;
        }

        // The pieces of a fresh world: depths assigned from the world seed (the save stores the result and never recomputes it).
        public static DivePieces ForWorld(ulong worldSeed) => new DivePieces(PieceDepths.Assign(worldSeed, PieceCatalog.Specs));

        public bool IsFound(int id) => _found.Contains(id);
        public IEnumerable<int> FoundIds => _found;
        public int DepthOf(int id) => _depth.TryGetValue(id, out int f) ? f : 0;

        // False for an unknown id or one already found.
        public bool Found(int id)
        {
            if (!_depth.ContainsKey(id)) return false;
            return _found.Add(id);
        }

        // A piece picked up: safe at once (never banked, never lost on failure), so it is the same as Found.
        public bool Pick(int id) => Found(id);

        public int PiecesLeft(int floor) => Left(floor, false);
        public int PeopleLeft(int floor) => Left(floor, true);

        public IEnumerable<int> OnFloor(int floor)
        {
            foreach (var kv in _depth) if (kv.Value == floor && !_found.Contains(kv.Key)) yield return kv.Key;
        }

        public void Restore(IEnumerable<int> found)
        {
            _found.Clear();
            if (found == null) return;
            foreach (int id in found) if (_depth.ContainsKey(id)) _found.Add(id);
        }

        int Left(int floor, bool people)
        {
            int n = 0;
            foreach (var kv in _depth) if (kv.Value == floor && PieceCatalog.IsPerson(kv.Key) == people && !_found.Contains(kv.Key)) n++;
            return n;
        }
    }
}
