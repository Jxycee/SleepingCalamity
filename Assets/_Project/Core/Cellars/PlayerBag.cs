// Core/Cellars/PlayerBag.cs: one player's dive loot - carried items, gold, bank-visit picks, home stash, lost satchel, opened-chest mask (spec 4.3, decisions 33-35, 39). Pure C#.
// Authority: the host owns one bag per recorded player (PartyBags in the DiveDirector); nothing here is networked. Carried items are bankable at a bank room (picks, then PushOn);
// GoHome brings everything to the stash, Fail keeps banked items and half the dive gold and packs the rest into the player's one lost satchel.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class PlayerBag
    {
        public const int MaxCarried = 60;     // placeholder: most items one player can carry at once; a carried satchel takes one of them (plan 3 decision 6)
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

        // Bumped by every mutation made through this class (Add, AddGold, bank visit, GoHome, Fail, chest mask), never by a refused call. A watcher that holds this bag (PlayerBagSync) rebuilds its
        // snapshot only when the version moved. The Satchel is a public object, but only this class mutates it (the save restore builds a whole new bag), so it needs no version of its own.
        public int Version { get; private set; }

        // Item counts (not kinds) in the home stash / banked this dive; plain loops over the concrete dictionaries, so a per-frame caller allocates nothing.
        public int StashTotal => Sum(_stash);
        public int BankedTotal => Sum(_banked);

        // Bank visit.
        public IReadOnlyList<int> Picks => _picks;
        public int SlotCapacity { get; private set; }
        public bool BankOpen { get; private set; }

        // Chests opened by this player on the current (seed, floor); transient, never saved.
        public uint ChestSeed { get; private set; }
        public int ChestFloor { get; private set; }
        public ulong OpenedChests { get; private set; }

        // The player's boots (+5% run speed, plan 3 decision 12): carried, banked this dive or in the home stash. A pair in the lost satchel does not count until recovered.
        public bool HasBoots => _carried.Contains(CellarLoot.BootsId) || _banked.ContainsKey(CellarLoot.BootsId) || _stash.ContainsKey(CellarLoot.BootsId);

        // No room for another carried item: the carried list plus the carried satchel (one slot) fill MaxCarried.
        public bool IsFull => _carried.Count + (Satchel.Carried != null ? 1 : 0) >= MaxCarried;

        public bool Add(int itemId)
        {
            if (itemId <= 0 || IsFull || !BankSlots.IsBankable(CellarLoot.KindOf(itemId))) return false;
            if (itemId == CellarLoot.BootsId && HasBoots) return false;   // one pair per player
            _carried.Add(itemId);
            Version++;
            return true;
        }

        // The player walked onto the lost satchel waiting on this floor: it is carried now and takes one carry slot. False with no satchel waiting or no free slot (it stays on the floor).
        public bool PickUpSatchel()
        {
            if (Satchel.Waiting == null || IsFull || !Satchel.PickUp()) return false;
            Version++;
            return true;
        }

        public bool AddGold(int amount)
        {
            if (amount <= 0) return false;
            DiveGold = (int)System.Math.Min((long)DiveGold + amount, int.MaxValue);
            Version++;
            return true;
        }

        public void OpenBank(int capacity)
        {
            _picks.Clear();
            SlotCapacity = System.Math.Max(0, capacity);
            BankOpen = true;
            Version++;
        }

        // Closes the bank visit without banking anything (the party left the bank floor): picks are dropped, carried items stay carried.
        public void CancelBank()
        {
            if (!BankOpen && _picks.Count == 0) return;   // picks can outlive a closed visit in a restored suspend
            CloseBank();
            Version++;
        }

        public bool Pick(int carriedIndexOrSatchel)
        {
            if (!BankOpen || _picks.Count >= SlotCapacity || _picks.Contains(carriedIndexOrSatchel)) return false;
            if (carriedIndexOrSatchel == SatchelPick) { if (Satchel.Carried == null) return false; }
            else if (carriedIndexOrSatchel < 0 || carriedIndexOrSatchel >= _carried.Count || !BankSlots.IsBankable(CellarLoot.KindOf(_carried[carriedIndexOrSatchel]))) return false;
            _picks.Add(carriedIndexOrSatchel);
            Version++;
            return true;
        }

        public bool Unpick(int carriedIndexOrSatchel)
        {
            if (!BankOpen || !_picks.Remove(carriedIndexOrSatchel)) return false;
            Version++;
            return true;
        }

        // The party moves on: the picks become safe, the visit closes. Empty slots stay empty (plan 3 task 8: the player chooses, nothing is picked for them).
        public void PushOn()
        {
            if (!BankOpen) return;
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
            Version++;
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
            OpenedChests = 0;   // the dive is over: a re-armed floor with the same seed offers its loot chests again
            CloseBank();
            Version++;
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
            OpenedChests = 0;
            CloseBank();
            Version++;
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
            Version++;
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

        // A bag from a bank-room suspend (plan 3 task 6, decision 9): the home part (gold, stash, waiting satchel) from the bags section and the mid-dive part (carried, banked this dive, dive gold,
        // opened-chest mask, carried satchel, bank picks) from the suspend record. Either may be null. Both are cleaned by the rules first; this stays tolerant (it skips what does not fit)
        // and shares nothing with the records. The bank visit is NOT opened here: the host opens it with ReopenBank(capacity) once it knows who is present, and the picks stay.
        public static PlayerBag RestoreSuspended(PlayerBagSave home, SuspendedBag dive)
        {
            var b = new PlayerBag();
            LostSatchel waiting = null;
            if (home != null)
            {
                b.Gold = System.Math.Max(0, home.Gold);
                if (home.Stash != null)
                    foreach (LootCountSave r in home.Stash) if (r != null && r.ItemId > 0 && r.Count > 0) Bump(b._stash, r.ItemId, r.Count);
                if (home.Satchel != null && home.Satchel.Items != null && home.Satchel.Items.Count > 0) waiting = new LostSatchel(home.Satchel.Floor, home.Satchel.Items);
            }
            b.Satchel.Restore(waiting);
            if (dive == null) return b;
            if (dive.Carried != null)
                foreach (int id in dive.Carried) if (id > 0 && b._carried.Count < MaxCarried) b._carried.Add(id);
            if (dive.BankedThisDive != null)
                foreach (LootCountSave r in dive.BankedThisDive) if (r != null && r.ItemId > 0 && r.Count > 0) Bump(b._banked, r.ItemId, r.Count);
            b.DiveGold = System.Math.Max(0, dive.DiveGold);
            b.ChestSeed = dive.ChestSeed; b.ChestFloor = dive.ChestFloor; b.OpenedChests = dive.OpenedChests;
            if (dive.CarriedSatchel != null && dive.CarriedSatchel.Items != null && dive.CarriedSatchel.Items.Count > 0)
            {
                b.Satchel.Restore(new LostSatchel(dive.CarriedSatchel.Floor, dive.CarriedSatchel.Items));   // a carried satchel and a waiting one never coexist: the carried one wins
                b.Satchel.PickUp();
            }
            if (dive.Picks != null)
                foreach (int p in dive.Picks)
                {
                    bool ok = p == SatchelPick ? b.Satchel.Carried != null : p >= 0 && p < b._carried.Count;
                    if (ok && !b._picks.Contains(p)) b._picks.Add(p);
                }
            return b;
        }

        // Opens the bank visit again after a restore, keeping the picks that were made (a smaller party than before drops the excess from the end).
        public void ReopenBank(int capacity)
        {
            SlotCapacity = System.Math.Max(0, capacity);
            BankOpen = true;
            while (_picks.Count > SlotCapacity) _picks.RemoveAt(_picks.Count - 1);
            Version++;
        }

        void CloseBank() { _picks.Clear(); SlotCapacity = 0; BankOpen = false; }

        static int Sum(Dictionary<int, int> d)
        {
            long sum = 0;
            foreach (int n in d.Values) sum += n;   // the concrete Dictionary's struct enumerator: no boxing
            return (int)System.Math.Min(sum, int.MaxValue);
        }

        static void Bump(Dictionary<int, int> d, int id, int n = 1) { d.TryGetValue(id, out int have); d[id] = have + n; }
    }
}
