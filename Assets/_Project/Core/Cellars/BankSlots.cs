// Core/Cellars/BankSlots.cs: bank slots by party size and what may be banked (spec decisions 4, 5, 34). Pure C#.
// Solo 3; 2p 2 each; 3p 1 each plus one extra for a player picked from the dive seed (never the previous owner; the world save keeps it, plan 3); 4p 1 each.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum DiveItemKind { Weapon, Usable, Gear, Large, Satchel, Key, Progress }

    public static class BankSlots
    {
        public const ulong ExtraSlotStream = 0xB5;   // keeps this pick independent of other rolls on the same dive seed

        public static bool IsBankable(DiveItemKind kind) => kind != DiveItemKind.Key && kind != DiveItemKind.Progress;

        // Spec 4.5: weapons and large items stir the dive; usables, gear, the satchel, keys and progress items do not.
        public static bool IsStirValuable(DiveItemKind kind) => kind == DiveItemKind.Weapon || kind == DiveItemKind.Large;

        public static int TotalSlots(int players) => players switch
        {
            1 => 3,
            2 or 3 or 4 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(players), players, "1-4 players"),
        };

        public static int SlotsFor(int players, int recordedSlot, int extraSlotOwner) => players switch
        {
            1 => 3,
            2 => 2,
            3 => recordedSlot == extraSlotOwner ? 2 : 1,
            4 => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(players), players, "1-4 players"),
        };

        // The bank capacity of every present player when the party changes while the room is open (a join, a leave, a rejoin). picks[i] is how many slots player i has filled already (0 for a
        // player with no visit). Rules, chosen so no player ever loses an item or a pick:
        //  1. Start from SlotsFor (the shares of TotalSlots(players) for the party as it is now; a shrinking party's players get the bigger shares back).
        //  2. A slot with a pick in it is never taken away: capacity is at least the picks already made.
        //  3. If that lifts the party above TotalSlots(players) (a late joiner arriving while others hold more than their new share), the surplus comes out of UNUSED slots only, one at a time
        //     from whoever has the most unused (ties: the later player, so a joiner is sized from what remains). A slot with a pick is never unused, so capacity never goes below the picks.
        // The sum is TotalSlots(players) unless the picks alone already exceed it (they cannot when the previous party's total was no bigger, but a hand-edited save could), then it is the picks.
        public static int[] Allocate(int players, IReadOnlyList<int> presentSlots, int extraSlotOwner, IReadOnlyList<int> picks)
        {
            if (presentSlots == null || presentSlots.Count != players) throw new ArgumentException("one slot per player", nameof(presentSlots));
            var cap = new int[players];
            var used = new int[players];
            int sum = 0;
            for (int i = 0; i < players; i++)
            {
                used[i] = picks != null && i < picks.Count ? Math.Max(0, picks[i]) : 0;
                cap[i] = Math.Max(SlotsFor(players, presentSlots[i], extraSlotOwner), used[i]);
                sum += cap[i];
            }
            int surplus = sum - TotalSlots(players);
            while (surplus > 0)
            {
                int best = -1, bestUnused = 0;
                for (int i = 0; i < players; i++)
                {
                    int unused = cap[i] - used[i];
                    if (unused > 0 && unused >= bestUnused) { best = i; bestUnused = unused; }
                }
                if (best < 0) break;
                cap[best]--; surplus--;
            }
            return cap;
        }

        // The 3-player extra slot's owner: a seeded pick among the present players, skipping the previous owner (-1 = none).
        public static int PickExtraSlotOwner(ulong diveSeed, IReadOnlyList<int> presentSlots, int previousOwner)
        {
            if (presentSlots == null || presentSlots.Count == 0) return -1;
            var candidates = new List<int>();
            foreach (int s in presentSlots) if (s != previousOwner) candidates.Add(s);
            if (candidates.Count == 0) candidates.AddRange(presentSlots);
            var rng = new Pcg32(diveSeed, ExtraSlotStream);
            return candidates[rng.Range(0, candidates.Count)];
        }
    }
}
