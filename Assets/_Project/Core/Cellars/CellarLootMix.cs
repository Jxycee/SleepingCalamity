// Core/Cellars/CellarLootMix.cs: the loot-mix steering of a Cellars dive (procgen item 1d; research procedural-dungeons section 3, the Hades reward-mix feedback rule). Pure C#.
// Every non-key chest of a dive is first assigned a loot CATEGORY (a band of item ranks: Common 1-2, Fine 3, Rare 4-5); CellarLoot then draws the rank inside that band with the
// floor's own triangular draw (clamped into the band) and picks one item. The category pick is a feedback rule: weight(c) = max(Floor, target(c) + K x (expectedShare(c) - shareSoFar(c))),
// where target(c) is the row of the chest's floor (rows follow the old floor curve, so floor 1 stays mostly Common and floor 4 mostly Fine/Rare), shareSoFar is the share of c among the
// chests already rolled this dive, and expectedShare is what the targets of those earlier chests predicted (with one constant row this is exactly target + K x (target - share)).
// Dive replay like SpecialRoomDive/VignetteDive: floor N's answer is a pure function of (seed, N, which chests of floors 1..N are key chests); chests are replayed in index order of each
// floor whatever order the players open them, the key chest is never rolled and never counted. Own stream Rng.Derive(seed, "cellars.lootmix", floor << 32 | chestIndex). Authority: none.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum LootCategory : byte { Common = 0, Fine = 1, Rare = 2 }

    // ---- THE one tuning spot. PLACEHOLDERS: first guesses for the user's three-run check; none is tested for feel. ----
    public static class CellarLootMixTuning
    {
        // Target share per category (Common, Fine, Rare) for a chest rolled on floor 1..4 (index = floor - 1; later floors use the last row). Each row sums to 1.
        // Integer arithmetic only (no float in a decision): shares are per-10000 ints, each row sums to Scale.
        public const int Scale = 10000;
        public static readonly int[][] Targets =
        {
            new[] { 9000, 1000, 0 },
            new[] { 6000, 3500, 500 },
            new[] { 3000, 4500, 2500 },
            new[] { 1500, 4000, 4500 },
        };
        public const int K = 2;                  // feedback strength (Hades uses 10: nearly forced; this is a gentle nudge)
        public const int MinWeight = 200;        // per-10000: no category ever drops below this weight (a 0-target category stays possible but rare)
        // Inclusive rank band of each category.
        public static readonly int[] BandLo = { 1, 3, 4 };
        public static readonly int[] BandHi = { 2, 3, 5 };
    }

    public static class CellarLootMix
    {
        public const int CategoryCount = 3;

        // Per-10000 target shares of a floor's row (sums to CellarLootMixTuning.Scale).
        public static int[] TargetRow(int floor) =>
            CellarLootMixTuning.Targets[Math.Max(1, Math.Min(CellarLootMixTuning.Targets.Length, floor)) - 1];

        // The category of every chest of the dive, floors 1..keyFlags.Length in order; keyFlags[f-1][i] = chest i of floor f holds the room key. Key chests get Common and are not counted.
        // Integer form: with T chests rolled so far, weight(c) x T = max(MinWeight x T, row[c] x T + k x (expected[c] - count[c] x Scale)), where expected[c] is the sum of the earlier chests' rows;
        // the pick is rng.Range(0, sum of weights) walked over the categories. The first chest (T = 0) weighs the row itself.
        public static LootCategory[][] Replay(uint seed, IReadOnlyList<bool[]> keyFlags, int k = CellarLootMixTuning.K)
        {
            var result = new LootCategory[keyFlags.Count][];
            var count = new long[CategoryCount]; var expected = new long[CategoryCount]; long total = 0;
            var w = new long[CategoryCount];
            for (int f = 1; f <= keyFlags.Count; f++)
            {
                bool[] keys = keyFlags[f - 1]; result[f - 1] = new LootCategory[keys.Length];
                int[] row = TargetRow(f);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i]) continue;
                    var rng = new Pcg32(Rng.Derive(seed, "cellars.lootmix", ((ulong)f << 32) | (uint)i));
                    long sum = 0;
                    for (int c = 0; c < CategoryCount; c++)
                    {
                        w[c] = total == 0 ? row[c]
                            : Math.Max(CellarLootMixTuning.MinWeight * total, row[c] * total + k * (expected[c] - count[c] * CellarLootMixTuning.Scale));
                        sum += w[c];
                    }
                    long x = rng.Range(0, (int)sum); int pick = CategoryCount - 1;
                    for (int c = 0; c < CategoryCount; c++) { if (x < w[c]) { pick = c; break; } x -= w[c]; }
                    result[f - 1][i] = (LootCategory)pick;
                    count[pick]++; total++;
                    for (int c = 0; c < CategoryCount; c++) expected[c] += row[c];
                }
            }
            return result;
        }

        // The category of chest `chestIndex` on `floor` (floor >= 1; keyFlags must cover floors 1..floor). A key chest answers Common.
        public static LootCategory CategoryFor(uint seed, int floor, int chestIndex, IReadOnlyList<bool[]> keyFlags, int k = CellarLootMixTuning.K)
        {
            if (floor < 1 || floor > keyFlags.Count) throw new ArgumentOutOfRangeException(nameof(floor));
            var upTo = new bool[floor][];
            for (int f = 1; f <= floor; f++) upTo[f - 1] = keyFlags[f - 1];
            LootCategory[][] all = Replay(seed, upTo, k);
            return chestIndex >= 0 && chestIndex < all[floor - 1].Length ? all[floor - 1][chestIndex] : LootCategory.Common;
        }

        // Cross-peer proof of the whole dive's category sequence.
        public static ulong LootMixHash(uint seed, IReadOnlyList<bool[]> keyFlags, int k = CellarLootMixTuning.K)
        {
            LootCategory[][] all = Replay(seed, keyFlags, k);
            var h = Fnv1a64.Create();
            for (int f = 0; f < all.Length; f++)
            {
                h.AppendInt32LE(f + 1); h.AppendInt32LE(all[f].Length);
                for (int i = 0; i < all[f].Length; i++) h.AppendInt32LE(keyFlags[f][i] ? -1 : (int)all[f][i]);
            }
            return h.Value;
        }

        // The key-chest flags of floors 1..floor of a dive, from the generated layouts (cached: the generator is the expensive part).
        static readonly Dictionary<(uint, int), bool[]> KeyCache = new Dictionary<(uint, int), bool[]>();
        public static bool[][] KeyFlagsOfDive(uint seed, int floor)
        {
            var r = new bool[floor][];
            lock (KeyCache)
            {
                if (KeyCache.Count > 4096) KeyCache.Clear();
                for (int f = 1; f <= floor; f++)
                {
                    if (!KeyCache.TryGetValue((seed, f), out bool[] flags))
                    {
                        CellarFloorLayout l = CellarFloorGenerator.Generate(seed, f);
                        flags = new bool[l.Chests.Count];
                        for (int i = 0; i < flags.Length; i++) flags[i] = l.Chests[i].HoldsKey;
                        KeyCache[(seed, f)] = flags;
                    }
                    r[f - 1] = flags;
                }
            }
            return r;
        }
    }
}
