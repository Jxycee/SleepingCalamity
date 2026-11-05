// Core/Cellars/PieceDepths.cs: which floor depth each piece and person lives on, decided once per world (spec decision 10, section 4.4; DC1 Atla style). Pure C#.
// Fixed floors are kept; random ones land on a generated floor of their half (floors 2 and 4, or 6 and 7; never the authored floor 1, a set piece or the boss). Deterministic per world seed.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public readonly struct PieceSpec
    {
        public readonly int Id;
        public readonly int FixedFloor;    // 0 = a random floor in its half
        public readonly bool SecondHalf;   // which half a random piece lands in

        public PieceSpec(int id, int fixedFloor, bool secondHalf) { Id = id; FixedFloor = fixedFloor; SecondHalf = secondHalf; }
    }

    public static class PieceDepths
    {
        public const ulong Stream = 0xA71A;   // keeps piece depths independent of other rolls on the world seed

        public static IReadOnlyList<int> CandidateFloors(bool secondHalf)
        {
            var floors = new List<int>();
            for (int f = UndercroftPlan.FirstFloor; f <= UndercroftPlan.BossFloor; f++)
            {
                if (UndercroftPlan.IsFirstHalf(f) == secondHalf) continue;
                UndercroftFloorKind kind = UndercroftPlan.KindOf(f);
                if (kind == UndercroftFloorKind.Generated) floors.Add(f);   // floor 1 is authored and 3/5 are set pieces: random pieces only land on generated floors (decision 10)
            }
            return floors;
        }

        public static IReadOnlyDictionary<int, int> Assign(ulong worldSeed, IReadOnlyList<PieceSpec> pieces)
        {
            var rng = new Pcg32(worldSeed, Stream);
            IReadOnlyList<int> first = CandidateFloors(false), second = CandidateFloors(true);
            var result = new SortedDictionary<int, int>();
            foreach (PieceSpec p in pieces)
            {
                if (result.ContainsKey(p.Id)) throw new ArgumentException($"piece {p.Id} appears twice", nameof(pieces));
                if (p.FixedFloor != 0)
                {
                    if (!UndercroftPlan.IsFloor(p.FixedFloor)) throw new ArgumentException($"piece {p.Id}: floor {p.FixedFloor} is outside the Undercroft", nameof(pieces));
                    result[p.Id] = p.FixedFloor;
                    continue;
                }
                IReadOnlyList<int> pool = p.SecondHalf ? second : first;
                result[p.Id] = pool[rng.Range(0, pool.Count)];
            }
            return result;
        }
    }
}
