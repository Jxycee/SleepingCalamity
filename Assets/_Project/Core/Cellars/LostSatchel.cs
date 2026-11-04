// Core/Cellars/LostSatchel.cs: what a failed dive costs (spec decisions 7, 33-35, 39). Pure C#.
// DiveGold: half the dive's earnings kept. SatchelSlot (one per player): the unbanked haul waits as one satchel at the depth where the player fell;
// it opens only outside the dungeon (into town storage); failing while carrying it loses it; one satchel at a time.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class DiveGold
    {
        public static int KeptOnFail(int earnedThisDive) => Math.Max(0, earnedThisDive) / 2;
    }

    public sealed class LostSatchel
    {
        public int Floor { get; }
        public IReadOnlyList<int> Items { get; }

        public LostSatchel(int floor, IEnumerable<int> items)
        {
            Floor = floor;
            Items = new List<int>(items).AsReadOnly();
        }
    }

    public sealed class SatchelSlot
    {
        private static readonly IReadOnlyList<int> None = new List<int>().AsReadOnly();

        public LostSatchel Waiting { get; private set; }
        public LostSatchel Carried { get; private set; }

        public void Restore(LostSatchel waiting) { Waiting = waiting; Carried = null; }

        // The player's dive failed on `floor` with these items unbanked (the satchel itself never counts as one).
        public void OnFail(int floor, IReadOnlyList<int> unbankedItems)
        {
            Carried = null;
            if (unbankedItems != null && unbankedItems.Count > 0) Waiting = new LostSatchel(floor, unbankedItems);
        }

        public bool ShouldPlaceOn(int floor) => Waiting != null && Waiting.Floor == floor;

        public bool PickUp()
        {
            if (Waiting == null) return false;
            Carried = Waiting;
            Waiting = null;
            return true;
        }

        // The carried satchel left the dungeon (home from the bank, the end, or banked in a slot): its items, for town storage.
        public IReadOnlyList<int> EmptyOutsideTheDungeon()
        {
            IReadOnlyList<int> items = Carried?.Items ?? None;
            Carried = null;
            return items;
        }
    }
}
