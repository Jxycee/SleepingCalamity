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

        // The 3-player extra slot's owner: a seeded pick among the present players, skipping the previous owner (-1 = none).
        public static int PickExtraSlotOwner(ulong diveSeed, IReadOnlyList<int> presentSlots, int previousOwner)
        {
            var candidates = new List<int>();
            foreach (int s in presentSlots) if (s != previousOwner) candidates.Add(s);
            if (candidates.Count == 0) candidates.AddRange(presentSlots);
            var rng = new Pcg32(diveSeed, ExtraSlotStream);
            return candidates[rng.Range(0, candidates.Count)];
        }
    }
}
