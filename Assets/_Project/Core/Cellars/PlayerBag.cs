// Core/Cellars/PlayerBag.cs: one player's dive loot - carried items, gold, bank-visit picks, home stash, lost satchel, opened-chest mask (spec 4.3, decisions 33-35, 39). Pure C#.
// Authority: the host owns one bag per recorded player (PartyBags in DiveState); nothing here is networked. Carried items are bankable at a bank room (picks, then PushOn);
// GoHome brings everything to the stash, Fail keeps banked items and half the dive gold and packs the rest into the player's one lost satchel.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class PlayerBag
    {
        public const int MaxCarried = 60;     // placeholder: most items one player can carry at once
        public const int SatchelPick = -1;    // the pick that means "the carried satchel"
        public const int MaxChests = 64;      // the opened-chest mask is one ulong

        readonly List<int> _carried = new List<int>();
        readonly Dictionary<int, int> _banked = new Dictionary<int, int>();
        readonly Dictionary<int, int> _stash = new Dictionary<int, int>();
        readonly List<int> _picks = new List<int>();

        public IReadOnlyList<int> Carried => _carried;                  // item ids in pickup order
        public IReadOnlyDictionary<int, int> BankedThisDive => _banked;
        public IReadOnlyDictionary<int, int> Stash => _stash;
        public int Gold { get; private set; }
        public int DiveGold { get; private set; }
        public SatchelSlot Satchel { get; private set; } = new SatchelSlot();

        // Bank visit.
        public IReadOnlyList<int> Picks => _picks;
        public int SlotCapacity { get; private set; }
        public bool BankOpen { get; private set; }

        // Chests opened by this player on the current (seed, floor); transient, never saved.
        public uint ChestSeed { get; private set; }
        public int ChestFloor { get; private set; }
        public ulong OpenedChests { get; private set; }

        public bool Add(int itemId)
        {
            if (itemId <= 0 || _carried.Count >= MaxCarried || !BankSlots.IsBankable(CellarLoot.KindOf(itemId))) return false;
            _carried.Add(itemId);
            return true;
        }

        public bool AddGold(int amount)
        {
            if (amount <= 0) return false;
            DiveGold = (int)System.Math.Min((long)DiveGold + amount, int.MaxValue);
            return true;
        }

        public void OpenBank(int capacity)
        {
            _picks.Clear();
            SlotCapacity = System.Math.Max(0, capacity);
            BankOpen = true;
        }

        public bool Pick(int carriedIndexOrSatchel)
        {
            if (!BankOpen || _picks.Count >= SlotCapacity || _picks.Contains(carriedIndexOrSatchel)) return false;
            if (carriedIndexOrSatchel == SatchelPick) { if (Satchel.Carried == null) return false; }
            else if (carriedIndexOrSatchel < 0 || carriedIndexOrSatchel >= _carried.Count || !BankSlots.IsBankable(CellarLoot.KindOf(_carried[carriedIndexOrSatchel]))) return false;
            _picks.Add(carriedIndexOrSatchel);
            return true;
        }

        public bool Unpick(int carriedIndexOrSatchel) => BankOpen && _picks.Remove(carriedIndexOrSatchel);

        // The party moves on: free slots fill with the most valuable unpicked items (highest rank first, earlier pickup on a tie), picks become safe, the visit closes.
        public void PushOn()
        {
            if (!BankOpen) return;
            if (_picks.Count < SlotCapacity)
            {
                var order = new List<int>();
                for (int i = 0; i < _carried.Count; i++) if (!_picks.Contains(i) && BankSlots.IsBankable(CellarLoot.KindOf(_carried[i]))) order.Add(i);
                order.Sort((a, b) => { int r = CellarLoot.RankOf(_carried[b]).CompareTo(CellarLoot.RankOf(_carried[a])); return r != 0 ? r : a.CompareTo(b); });
                foreach (int i in order) { if (_picks.Count >= SlotCapacity) break; _picks.Add(i); }
            }
            var indexes = new List<int>();
            foreach (int p in _picks)
            {
                if (p == SatchelPick) { foreach (int item in Satchel.EmptyOutsideTheDungeon()) Bump(_banked, item); }
                else indexes.Add(p);
            }
            indexes.Sort();
            foreach (int i in indexes) Bump(_banked, _carried[i]);
            for (int k = indexes.Count - 1; k >= 0; k--) _carried.RemoveAt(indexes[k]);
            CloseBank();
        }

        // Out of the dungeon: everything carried or banked goes to the stash, dive gold is kept in full.
        public void GoHome()
        {
            foreach (int item in _carried) Bump(_stash, item);
            _carried.Clear();
            foreach (var kv in _banked) Bump(_stash, kv.Key, kv.Value);
            _banked.Clear();
            foreach (int item in Satchel.EmptyOutsideTheDungeon()) Bump(_stash, item);
            Gold = (int)System.Math.Min((long)Gold + DiveGold, int.MaxValue);
            DiveGold = 0;
            CloseBank();
        }

        // The dive failed on `floor`: banked items are safe, half the dive gold is kept, the carried items wait in a satchel (a carried satchel is lost).
        public void Fail(int floor)
        {
            foreach (var kv in _banked) Bump(_stash, kv.Key, kv.Value);
            _banked.Clear();
            Gold = (int)System.Math.Min((long)Gold + Core.DiveGold.KeptOnFail(DiveGold), int.MaxValue);
            DiveGold = 0;
            Satchel.OnFail(floor, _carried);
            _carried.Clear();
            CloseBank();
        }

        // What Fail(floor) would leave, as a new bag; this bag is unchanged (used to save a mid-dive state as a failed dive).
        public PlayerBag CopyAsFailed(int floor)
        {
            var c = new PlayerBag { Gold = Gold, DiveGold = DiveGold, ChestSeed = ChestSeed, ChestFloor = ChestFloor, OpenedChests = OpenedChests };
            c._carried.AddRange(_carried);
            foreach (var kv in _banked) c._banked[kv.Key] = kv.Value;
            foreach (var kv in _stash) c._stash[kv.Key] = kv.Value;
            if (Satchel.Carried != null) { c.Satchel.Restore(Satchel.Carried); c.Satchel.PickUp(); }
            else c.Satchel.Restore(Satchel.Waiting);
            c.Fail(floor);
            return c;
        }

        public bool MarkChestOpened(uint seed, int floor, int chestIndex)
        {
            if (chestIndex < 0 || chestIndex >= MaxChests) return false;
            if (seed != ChestSeed || floor != ChestFloor) { ChestSeed = seed; ChestFloor = floor; OpenedChests = 0; }
            ulong bit = 1UL << chestIndex;
            if ((OpenedChests & bit) != 0) return false;
            OpenedChests |= bit;
            return true;
        }

        public bool ChestOpened(uint seed, int floor, int chestIndex)
            => chestIndex >= 0 && chestIndex < MaxChests && seed == ChestSeed && floor == ChestFloor && (OpenedChests & (1UL << chestIndex)) != 0;

        // A bag loaded from a save: gold, home stash and the waiting satchel (no dive in progress).
        public static PlayerBag Restore(int gold, IReadOnlyDictionary<int, int> stash, LostSatchel waiting)
        {
            var b = new PlayerBag { Gold = System.Math.Max(0, gold) };
            if (stash != null) foreach (var kv in stash) if (kv.Value > 0) b._stash[kv.Key] = kv.Value;
            b.Satchel.Restore(waiting);
            return b;
        }

        void CloseBank() { _picks.Clear(); SlotCapacity = 0; BankOpen = false; }

        static void Bump(Dictionary<int, int> d, int id, int n = 1) { d.TryGetValue(id, out int have); d[id] = have + n; }
    }
}
