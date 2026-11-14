// Core/Cellars/CellarLoot.cs: the placeholder chest-loot table and the rank roll of the Cellars (PHASE1 7.2). Pure C#, no engine references.
// A chest roll is a Dark Cloud 2 style triangular draw on the item RANK: a symmetric triangular distribution (sum of two uniforms) whose peak moves up with the floor,
// rounded and clamped to 1-5, then one item of that rank is picked. Deterministic per (seed, floor, chestIndex) on its own stream "cellars.loot", so the host and any test
// agree and no other stream moves. The key chest yields the room key instead of loot (RoomKeyId); the gate key (GateKeyId) is dropped by the carrier enemy and never rolled.
// Authority: none (static data); the host rolls when a chest is opened or an enemy dies (RollEnemyDrop, one independent roll per hitter).
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
        // Plan 3 task 5 (decisions 4, 5, 12-14). GoldId is a negative sentinel: a purse roll is diverted to the player's dive gold before PlayerBag.Add ever sees it (Add rejects ids <= 0). BootsId is the one
        // Gear item (+5% run speed, read by the owner), one pair per player. PLACEHOLDERS: the purse size and the roll odds.
        public const int GoldId = -1, BootsId = 120;
        public const int GoldPurse = 25;          // placeholder: gold in one purse
        public const int GoldOneIn = 4;           // placeholder: a purse replaces one chest roll in four
        public const int BootsOneIn = 12;         // placeholder: boots replace one eligible roll in twelve ...
        public const int DeepBootsFactor = 3;     // placeholder: ... three times as often on the deep floors (BootsDeepFloor and below)
        public const int BootsDeepFloor = 6;
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
            if (id == BootsId) return "Sprinter's Boots";
            if (id == GoldId) return "Gold Purse";
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

        // One player's copy of a loot chest (spec decision 39; never for the key chest): the category band is the chest's, the same for every player,
        // but the rank draw and the item pick come from a stream of their own per player slot (0-3), so no other stream moves.
        public static int RollFor(uint seed, int floor, int chestIndex, int playerSlot) => RollFor(seed, floor, chestIndex, playerSlot, 0);

        // rankBonus (momentum, plan 3 decision 13) lifts the rolled rank after the band clamp, capped at MaxRank; 0 (or less) is exactly the roll of before, so no seed's pick moves.
        // The gold purse and the boots are NOT rolled here (that would move every existing pick): RollChest layers them on top.
        public static int RollFor(uint seed, int floor, int chestIndex, int playerSlot, int rankBonus)
        {
            if (playerSlot < 0 || playerSlot > 3) throw new System.ArgumentOutOfRangeException(nameof(playerSlot), playerSlot, "player slot 0-3");
            var rng = new Pcg32(Rng.Derive(seed, "cellars.loot.player", ((ulong)floor << 32) | ((ulong)(uint)chestIndex << 2) | (uint)playerSlot));
            LootCategory cat = CellarLootMix.CategoryFor(seed, floor, chestIndex, CellarLootMix.KeyFlagsOfDive(seed, floor));
            int lo = CellarLootMixTuning.BandLo[(int)cat], hi = CellarLootMixTuning.BandHi[(int)cat];
            int drawn = DrawRank(ref rng, floor);
            int rank = drawn < lo ? lo : drawn > hi ? hi : drawn;
            if (rankBonus > 0) rank = System.Math.Min(MaxRank, rank + rankBonus);
            int n = 0; foreach (LootItem i in Items) if (i.Rank == rank) n++;
            int pick = rng.Range(0, n);
            foreach (LootItem i in Items) if (i.Rank == rank && pick-- == 0) return i.Id;
            return Items[0].Id;
        }

        // What one player's loot chest holds (never for the key chest): GoldId (a purse, one roll in GoldOneIn), else BootsId (a separate roll, only in a Fine or Rare chest, only for a player without
        // boots, more often on the deep floors), else RollFor. Each roll has its own stream per (floor, chest, player slot), so no other stream moves.
        public static int RollChest(uint seed, int floor, int chestIndex, int playerSlot, int rankBonus = 0, bool ownsBoots = false)
        {
            if (playerSlot < 0 || playerSlot > 3) throw new System.ArgumentOutOfRangeException(nameof(playerSlot), playerSlot, "player slot 0-3");
            ulong key = ((ulong)floor << 32) | ((ulong)(uint)chestIndex << 2) | (uint)playerSlot;
            var gold = new Pcg32(Rng.Derive(seed, "cellars.loot.gold", key));
            if (gold.Range(0, GoldOneIn) == 0) return GoldId;
            var boots = new Pcg32(Rng.Derive(seed, "cellars.loot.boots", key));
            int roll = boots.Range(0, BootsOneIn * DeepBootsFactor);   // drawn either way so the stream does not depend on the floor
            if (!ownsBoots && roll < (floor >= BootsDeepFloor ? DeepBootsFactor : 1)
                && CellarLootMix.CategoryFor(seed, floor, chestIndex, CellarLootMix.KeyFlagsOfDive(seed, floor)) != LootCategory.Common) return BootsId;
            return RollFor(seed, floor, chestIndex, playerSlot, rankBonus);
        }

        // ---- enemy drops (per player, host rolls on a real death) ----
        public const int EnemyDropOneIn = 5;      // placeholder: one roll in five gives that player an item
        public const int EnemyDropNoItem = 0;     // RollEnemyDrop returns this for "no drop"

        // One player's own roll when an enemy dies (a slot 0-3, only for a player who hit it). Returns EnemyDropNoItem (0) or an id of the Items table: never gold, boots or a key (the chests carry those).
        // dropKey is a per-floor deterministic id of the kill (CellarContentsSync: room * 1024 + the room's spawn count), so the same (seed, floor, kill, slot) always rolls the same; its stream
        // ("cellars.loot.enemy") touches no other. tier is the enemy's tier: a tier 2 enemy pays one rank more, tier 1 one rank less than the floor's chest rank (placeholder, min 1).
        public static int RollEnemyDrop(uint seed, int floor, int dropKey, int playerSlot, int tier = 1, int rankBonus = 0)
        {
            if (playerSlot < 0 || playerSlot > 3) throw new System.ArgumentOutOfRangeException(nameof(playerSlot), playerSlot, "player slot 0-3");
            var rng = new Pcg32(Rng.Derive(seed, "cellars.loot.enemy", ((ulong)floor << 32) | ((ulong)(uint)dropKey << 2) | (uint)playerSlot));
            if (rng.Range(0, EnemyDropOneIn) != 0) return EnemyDropNoItem;
            int rank = DrawRank(ref rng, floor) + (tier >= 2 ? 1 : -1) + System.Math.Max(0, rankBonus);
            rank = rank < MinRank ? MinRank : rank > MaxRank ? MaxRank : rank;
            int n = 0; foreach (LootItem i in Items) if (i.Rank == rank) n++;
            int pick = rng.Range(0, n);
            foreach (LootItem i in Items) if (i.Rank == rank && pick-- == 0) return i.Id;
            return Items[0].Id;
        }

        // 0 for an unknown id or a key.
        public static int RankOf(int id) { foreach (LootItem i in Items) if (i.Id == id) return i.Rank; return 0; }

        // What a loot id is for the bag and the bank (placeholder: every table item is Large; keys are Keys; an unknown id is Usable).
        public static DiveItemKind KindOf(int itemId)
        {
            if (itemId == RoomKeyId || itemId == GateKeyId) return DiveItemKind.Key;
            if (itemId == BootsId) return DiveItemKind.Gear;
            foreach (LootItem i in Items) if (i.Id == itemId) return DiveItemKind.Large;
            return DiveItemKind.Usable;
        }
    }
}
