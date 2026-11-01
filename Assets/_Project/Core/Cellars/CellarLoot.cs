// Core/Cellars/CellarLoot.cs: the placeholder chest-loot table and the rank roll of the Cellars (PHASE1 7.2). Pure C#, no engine references.
// A chest roll is a Dark Cloud 2 style triangular draw on the item RANK: a symmetric triangular distribution (sum of two uniforms) whose peak moves up with the floor,
// rounded and clamped to 1-5, then one item of that rank is picked. Deterministic per (seed, floor, chestIndex) on its own stream "cellars.loot", so the host and any test
// agree and no other stream moves. The key chest yields the room key instead of loot (RoomKeyId); the gate key (GateKeyId) is dropped by the carrier enemy and never rolled.
// Authority: none (static data); the host rolls when a chest is opened.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public readonly struct LootItem
    {
        public readonly int Id; public readonly string Name; public readonly int Rank;
        public LootItem(int id, string name, int rank) { Id = id; Name = name; Rank = rank; }
    }

    public static class CellarLoot
    {
        public const int RoomKeyId = 9001, GateKeyId = 9002;   // reserved ids, never in the table
        public const int MinRank = 1, MaxRank = 5;
        const float Spread = 1.5f;                              // half-width of the triangle around the floor's peak rank
        static readonly float[] Peak = { 1.5f, 2.2f, 3.0f, 3.6f };   // floors 1-4

        public static readonly IReadOnlyList<LootItem> Items = new[]
        {
            new LootItem(101, "Chipped Crumb", 1), new LootItem(102, "Stale Rind", 1), new LootItem(103, "Tin Whistle", 1),
            new LootItem(104, "Honey Jar", 2), new LootItem(105, "Copper Ladle", 2), new LootItem(106, "Flour Sack", 2),
            new LootItem(107, "Silver Spoon", 3), new LootItem(108, "Candied Peel", 3), new LootItem(109, "Iron Whisk", 3),
            new LootItem(110, "Golden Wheat", 4), new LootItem(111, "Sugar Crystal", 4),
            new LootItem(112, "Starfire Yeast", 5),
        };

        public static string NameOf(int id)
        {
            if (id == RoomKeyId) return "Room Key";
            if (id == GateKeyId) return "Gate Key";
            foreach (LootItem i in Items) if (i.Id == id) return i.Name;
            return "Item " + id;
        }

        public static float PeakRank(int floor) => Peak[System.Math.Max(1, System.Math.Min(4, floor)) - 1];

        // Callers must skip key chests (Roll handles them first): a key chest has no category and would fall into the Common band here.
        // The rank drawn for this chest (1-5): the category comes from the dive-wide loot-mix steering (CellarLootMix, procgen 1d), the rank is the floor's triangular draw clamped into its band.
        public static int RollRank(uint seed, int floor, int chestIndex)
        {
            var rng = new Pcg32(Rng.Derive(seed, "cellars.loot", ((ulong)floor << 32) | (uint)chestIndex));
            return SteeredRank(ref rng, seed, floor, chestIndex);
        }

        // THE call site of the loot-mix resolver.
        static int SteeredRank(ref Pcg32 rng, uint seed, int floor, int chestIndex)
        {
            int drawn = DrawRank(ref rng, floor);
            LootCategory cat = CellarLootMix.CategoryFor(seed, floor, chestIndex, CellarLootMix.KeyFlagsOfDive(seed, floor));
            int lo = CellarLootMixTuning.BandLo[(int)cat], hi = CellarLootMixTuning.BandHi[(int)cat];
            return drawn < lo ? lo : drawn > hi ? hi : drawn;
        }

        static int DrawRank(ref Pcg32 rng, int floor)
        {
            float tri = rng.NextFloat() + rng.NextFloat() - 1f;   // triangular on (-1, 1), peak 0
            int rank = (int)System.Math.Round(PeakRank(floor) + tri * Spread, System.MidpointRounding.AwayFromZero);
            return rank < MinRank ? MinRank : rank > MaxRank ? MaxRank : rank;
        }

        // The item id a chest holds: the room key for the key chest, else one item of the rolled rank.
        public static int Roll(uint seed, int floor, int chestIndex, bool holdsKey)
        {
            if (holdsKey) return RoomKeyId;
            var rng = new Pcg32(Rng.Derive(seed, "cellars.loot", ((ulong)floor << 32) | (uint)chestIndex));
            int rank = SteeredRank(ref rng, seed, floor, chestIndex);
            int n = 0; foreach (LootItem i in Items) if (i.Rank == rank) n++;
            int pick = rng.Range(0, n);
            foreach (LootItem i in Items) if (i.Rank == rank && pick-- == 0) return i.Id;
            return Items[0].Id;
        }

        public static int RankOf(int id) { foreach (LootItem i in Items) if (i.Id == id) return i.Rank; return 0; }
    }
}
