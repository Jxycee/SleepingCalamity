// Core/Save/SuspendSave.cs: the optional world-save section "suspend" (plan 3 task 6, decisions 8-10) and its rules. Pure C#, no Unity types.
// A party that saves in the bank room (before or after its push-on choice) writes one: the dive seed, where the dive stood (floor, phase, which floors were cleared, whether the choice
// was made), the stir meter, momentum and boss-retry states, who was present, and one SuspendedBag per player with the mid-dive half of that player's bag (carried items, banked-this-dive
// items, dive gold, the opened-chest mask, the carried satchel, the bank picks). The home half (gold, stash, waiting satchel) stays in the "bags" section. The world seed is NOT here (it
// stays in the world file). A suspended save is never counted as a failed dive (TownSaveRules.Reconcile); using the section clears it (the host's next capture says "known, none").
// The same section serves the later stair save-and-quit, which is why Phase may also be Floor.
// Clean costs only the bad entry: a bad floor or phase drops the section alone (null, named "suspend"); a bad SuspendedBag (unknown identity, repeated id, bad item id, a count of 0 or
// less, more than PlayerBag.MaxCarried items, a pick index out of range or repeated, a bad satchel or chest floor) drops only that bag's dive record ("suspend.bags.<id>"; the player's
// home bag stays); bad present ids drop singly; masks and numbers are repaired, not rejected. Unknown members are kept in Extra.
// The stir, momentum and retry states are plain classes here (their State structs are get-only and would not read back from JSON); From/To convert.
// Authority: host only (the host's world save).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SleepingCalamity.Core
{
    [Serializable]
    public sealed class SuspendStirSave
    {
        public int FloorMask;
        public int Valuables;
        public int ValuablesBaseline;
        public bool SecondHalf;
        public bool ExtraEnemy;         // DiveStir.State.Extra: the extra enemy is latched
        public bool Hunter;
        public bool CollapseUsed;
        public bool Running;
        public float SecondsLeft;
        public float RewardCarry;
        [JsonExtensionData] public Dictionary<string, object> Extra;

        public static SuspendStirSave From(DiveStir.State s) => new SuspendStirSave
        {
            FloorMask = s.FloorMask, Valuables = s.Valuables, ValuablesBaseline = s.ValuablesBaseline, SecondHalf = s.SecondHalf, ExtraEnemy = s.Extra, Hunter = s.Hunter,
            CollapseUsed = s.CollapseUsed, Running = s.Running, SecondsLeft = s.SecondsLeft, RewardCarry = s.RewardCarry,
        };

        public DiveStir.State ToState() => new DiveStir.State(FloorMask, Valuables, ValuablesBaseline, SecondHalf, ExtraEnemy, Hunter, CollapseUsed, Running, SecondsLeft, RewardCarry);
    }

    [Serializable]
    public sealed class SuspendMomentumSave
    {
        public bool Unbroken;
        public bool PushedOn;
        public bool WalkedToBank;
        [JsonExtensionData] public Dictionary<string, object> Extra;

        public static SuspendMomentumSave From(DiveMomentum.State s) => new SuspendMomentumSave { Unbroken = s.Unbroken, PushedOn = s.PushedOn, WalkedToBank = s.WalkedToBank };
        public DiveMomentum.State ToState() => new DiveMomentum.State(Unbroken, PushedOn, WalkedToBank);
    }

    [Serializable]
    public sealed class SuspendRetrySave
    {
        public bool UsedThisDive;
        public bool IdolActivated;
        [JsonExtensionData] public Dictionary<string, object> Extra;

        public static SuspendRetrySave From(BossRetry.State s) => new SuspendRetrySave { UsedThisDive = s.UsedThisDive, IdolActivated = s.IdolActivated };
        public BossRetry.State ToState() => new BossRetry.State(UsedThisDive, IdolActivated);
    }

    // One player's mid-dive half of a bag.
    [Serializable]
    public sealed class SuspendedBag
    {
        public int Identity = -1;                           // IdentityKind as a number; no valid default (an entry without it is damaged)
        public string Id = "";                              // PlayerKey.Id
        public List<int> Carried = new List<int>();         // item ids in pickup order, at most PlayerBag.MaxCarried
        public List<LootCountSave> BankedThisDive = new List<LootCountSave>();
        public int DiveGold;
        public ulong OpenedChests;                          // the chest mask (bit 63 is a chest)
        public uint ChestSeed;
        public int ChestFloor;                              // 0: none yet, else the floor the mask belongs to
        public SatchelSave CarriedSatchel;                  // null: not carrying the satchel
        public List<int> Picks = new List<int>();           // bank picks: carried indexes, or PlayerBag.SatchelPick
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class SuspendSave
    {
        public uint Seed;                                   // the dive seed (the world seed stays in the world file)
        public int Floor;                                   // 1..UndercroftPlan.BossFloor
        public int Phase;                                   // DivePhase as a number: BankRoom, or Floor (the choice was made, or a later stair quit)
        public bool BankChosen;                             // push-on chosen
        public bool StartedAtBank;
        public int ClearedMask;                             // floors cleared this dive (bit floor-1, ten bits)
        public string ExtraSlotOwner;                       // the 3-player extra slot's owner for this visit (a bag id "Kind:Id"), or null
        public SuspendStirSave Stir = new SuspendStirSave();
        public SuspendMomentumSave Momentum = new SuspendMomentumSave();
        public SuspendRetrySave Retry = new SuspendRetrySave();
        public List<string> PresentIds = new List<string>();    // bag ids ("Kind:Id") of the players present when it was written. Written and cleaned but never read (Resume takes the live lists); kept so old and new files stay compatible (N3)
        public List<SuspendedBag> Bags = new List<SuspendedBag>();
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    public static class SuspendSaveRules
    {
        public const int MaxPicks = 3;   // the most bank slots one player ever has (BankSlots.TotalSlots(1))
        const int AllFloors = (1 << UndercroftPlan.BossFloor) - 1;

        static Dictionary<string, object> CopyExtra(Dictionary<string, object> e) => e != null ? new Dictionary<string, object>(e) : null;

        // ---------------------------------------------------------------- clean
        // A valid copy of raw, or null when raw is null or its floor / phase is bad (named "suspend" in dropped). Each dropped bag adds "suspend.bags.<id>".
        public static SuspendSave Clean(SuspendSave raw, List<string> dropped)
        {
            if (raw == null) return null;
            if (!UndercroftPlan.IsFloor(raw.Floor) || (raw.Phase != (int)DivePhase.Floor && raw.Phase != (int)DivePhase.BankRoom)) { dropped?.Add("suspend"); return null; }
            var c = new SuspendSave
            {
                Seed = raw.Seed, Floor = raw.Floor, Phase = raw.Phase, BankChosen = raw.BankChosen, StartedAtBank = raw.StartedAtBank,
                ClearedMask = raw.ClearedMask & AllFloors, Extra = CopyExtra(raw.Extra),
            };
            if (raw.ExtraSlotOwner != null)
            {
                if (BagsSaveRules.TryParseBagId(raw.ExtraSlotOwner, out _)) c.ExtraSlotOwner = raw.ExtraSlotOwner;
                else dropped?.Add("suspend.extraSlotOwner");
            }
            c.Stir = CleanStir(raw.Stir);
            c.Momentum = raw.Momentum == null ? new SuspendMomentumSave() : new SuspendMomentumSave { Unbroken = raw.Momentum.Unbroken, PushedOn = raw.Momentum.PushedOn, WalkedToBank = raw.Momentum.WalkedToBank, Extra = CopyExtra(raw.Momentum.Extra) };
            c.Retry = raw.Retry == null ? new SuspendRetrySave() : new SuspendRetrySave { UsedThisDive = raw.Retry.UsedThisDive, IdolActivated = raw.Retry.IdolActivated, Extra = CopyExtra(raw.Retry.Extra) };
            if (raw.PresentIds != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in raw.PresentIds)
                {
                    if (id == null || !BagsSaveRules.TryParseBagId(id, out _) || !seen.Add(id) || c.PresentIds.Count >= WorldPlayers.MaxRecorded) { dropped?.Add("suspend.presentIds"); continue; }
                    c.PresentIds.Add(id);
                }
            }
            if (raw.Bags != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (SuspendedBag e in raw.Bags)
                {
                    SuspendedBag ok = CleanBag(e, seen, c.Bags.Count);
                    if (ok == null) dropped?.Add("suspend.bags." + (e?.Id ?? "null"));
                    else c.Bags.Add(ok);
                }
            }
            return c;
        }

        // A suspend that cannot be resumed (bad floor or phase, or no dive in progress) must not take its dive records with it: `bags` holds only the home halves, so the carried items, the
        // banked-this-dive items and the dive gold live nowhere else. This settles raw's bag records as a failed dive at the saved floor (floor 1 when that is unusable), the same
        // PlayerBag.CopyAsFailed a mid-dive quit uses: banked items to the stash, half the dive gold kept, the carried items into the waiting satchel. Returns the new home section
        // (home itself when there is nothing to settle). Records that do not clean are dropped as Clean does.
        public static BagsSave SettleAsFailed(BagsSave home, SuspendSave raw)
        {
            if (raw == null || raw.Bags == null || raw.Bags.Count == 0) return home;
            int floor = UndercroftPlan.IsFloor(raw.Floor) ? raw.Floor : UndercroftPlan.FirstFloor;
            SuspendSave shell = Clean(new SuspendSave { Floor = floor, Phase = (int)DivePhase.BankRoom, Bags = raw.Bags }, null);
            if (shell.Bags.Count == 0) return home;
            var party = new PartyBags();
            BagsSaveRules.RestoreSuspended(home, shell, party);
            var keyOf = new Dictionary<string, PlayerKey>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, PlayerBag> kv in party.All)
                if (BagsSaveRules.TryParseBagId(kv.Key, out PlayerKey key)) keyOf[kv.Key] = key;
            BagsSave settled = BagsSaveRules.Clean(BagsSaveRules.Capture(party, keyOf, true, floor), null);
            if (home != null && home.Extra != null) settled.Extra = CopyExtra(home.Extra);
            return settled;
        }

        static SuspendStirSave CleanStir(SuspendStirSave s)
        {
            if (s == null) return new SuspendStirSave();
            int valuables = Math.Max(0, s.Valuables);
            return new SuspendStirSave
            {
                FloorMask = s.FloorMask & AllFloors, Valuables = valuables, ValuablesBaseline = Math.Min(valuables, Math.Max(0, s.ValuablesBaseline)),
                SecondHalf = s.SecondHalf, ExtraEnemy = s.ExtraEnemy, Hunter = s.Hunter, CollapseUsed = s.CollapseUsed, Running = s.Running,
                SecondsLeft = Finite(s.SecondsLeft), RewardCarry = Finite(s.RewardCarry), Extra = CopyExtra(s.Extra),
            };
        }

        static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) || v < 0f ? 0f : v;

        // Null when the record does not fit (the caller drops it alone).
        static SuspendedBag CleanBag(SuspendedBag e, HashSet<string> seen, int keptSoFar)
        {
            if (e == null || !BagsSaveRules.TryKind(e.Identity, out IdentityKind kind) || !new PlayerKey(kind, e.Id).IsValid) return null;
            if (!seen.Add(e.Identity + ":" + e.Id) || keptSoFar >= WorldPlayers.MaxRecorded) return null;
            var c = new SuspendedBag { Identity = e.Identity, Id = e.Id, DiveGold = Math.Max(0, e.DiveGold), OpenedChests = e.OpenedChests, ChestSeed = e.ChestSeed, Extra = CopyExtra(e.Extra) };
            if (e.ChestFloor != 0 && !UndercroftPlan.IsFloor(e.ChestFloor)) return null;
            c.ChestFloor = e.ChestFloor;
            if (e.Carried != null)
            {
                if (e.Carried.Count > PlayerBag.MaxCarried) return null;
                foreach (int id in e.Carried)
                {
                    if (id <= 0 || !BankSlots.IsBankable(CellarLoot.KindOf(id))) return null;
                    c.Carried.Add(id);
                }
            }
            if (e.BankedThisDive != null)
            {
                var byItem = new SortedDictionary<int, long>();
                Dictionary<int, Dictionary<string, object>> extra = null;
                foreach (LootCountSave r in e.BankedThisDive)
                {
                    if (r == null || r.ItemId <= 0 || r.Count <= 0) return null;
                    byItem.TryGetValue(r.ItemId, out long have);
                    byItem[r.ItemId] = Math.Min(TownSaveRules.MaxLootCount, have + r.Count);
                    if (r.Extra != null) { if (extra == null) extra = new Dictionary<int, Dictionary<string, object>>(); extra[r.ItemId] = CopyExtra(r.Extra); }
                }
                foreach (KeyValuePair<int, long> kv in byItem)
                    c.BankedThisDive.Add(new LootCountSave { ItemId = kv.Key, Count = (int)kv.Value, Extra = extra != null && extra.TryGetValue(kv.Key, out var x) ? x : null });
            }
            if (e.CarriedSatchel != null)
            {
                SatchelSave s = e.CarriedSatchel;
                if (!UndercroftPlan.IsFloor(s.Floor) || s.Items == null || s.Items.Count == 0 || s.Items.Count > PlayerBag.MaxCarried) return null;
                foreach (int id in s.Items) if (id <= 0) return null;
                c.CarriedSatchel = new SatchelSave { Floor = s.Floor, Items = new List<int>(s.Items), Extra = CopyExtra(s.Extra) };
            }
            if (e.Picks != null)
            {
                if (e.Picks.Count > MaxPicks) return null;
                foreach (int p in e.Picks)
                {
                    bool inRange = p == PlayerBag.SatchelPick ? c.CarriedSatchel != null : p >= 0 && p < c.Carried.Count;
                    if (!inRange || c.Picks.Contains(p)) return null;
                    c.Picks.Add(p);
                }
            }
            return c;
        }

        // ---------------------------------------------------------------- live <-> section
        // The section for a session snapshot. presentIds: the bag ids of the players present; idsBySlot: the bag id of each roster slot (index = slot, null for an empty slot), used to name the
        // 3-player extra slot's owner; bags: the records BagsSaveRules.Capture(..., suspended: true, ...) produced. The result shares nothing with its inputs.
        public static SuspendSave Build(DiveSession.SnapshotData snap, uint diveSeed, IEnumerable<string> presentIds, IReadOnlyList<string> idsBySlot, IEnumerable<SuspendedBag> bags)
        {
            var s = new SuspendSave
            {
                Seed = diveSeed, Floor = snap.Floor, Phase = (int)snap.Phase, BankChosen = snap.BankChosenThisDive, StartedAtBank = snap.StartedAtBank, ClearedMask = snap.ClearedMask,
                ExtraSlotOwner = IdOfSlot(snap.ExtraSlotOwner, idsBySlot),
                Stir = SuspendStirSave.From(snap.Stir), Momentum = SuspendMomentumSave.From(snap.Momentum), Retry = SuspendRetrySave.From(snap.Retry),
            };
            if (presentIds != null) s.PresentIds.AddRange(presentIds);
            if (bags != null) s.Bags.AddRange(bags);
            return s;
        }

        // The session snapshot a (cleaned) section stands for; DiveSession.Restore takes it. idsBySlot as in Build.
        // A bank-room save is clamped to what the room needs (plan 3 task 8a): floor 5, floor 5 cleared, the choice not made. A file that says BankRoom on another floor is damaged or
        // hand-edited; the party still returns to the bank room with its items rather than losing them.
        public static DiveSession.SnapshotData ToSnapshot(SuspendSave s, IReadOnlyList<string> idsBySlot)
        {
            int floor = s.Floor, cleared = s.ClearedMask;
            bool chosen = s.BankChosen;
            if (s.Phase == (int)DivePhase.BankRoom)
            {
                floor = UndercroftPlan.BankFloor; cleared |= 1 << (UndercroftPlan.BankFloor - 1); chosen = false;
            }
            return new DiveSession.SnapshotData((DivePhase)s.Phase, floor, cleared, chosen, s.StartedAtBank, SlotOfId(s.ExtraSlotOwner, idsBySlot),
                (s.Stir ?? new SuspendStirSave()).ToState(), (s.Momentum ?? new SuspendMomentumSave()).ToState(), (s.Retry ?? new SuspendRetrySave()).ToState());
        }

        // Whether the host can resume the (cleaned) section today: the bank room, or floor 5 after the push-on. A Phase == Floor save of anything else is the later stair quit (its own plan);
        // until then it is not resumed and the host settles it as a failed dive (DiveSession.Fail) so the carried items are not lost.
        public static bool IsResumable(SuspendSave s)
            => s != null && (s.Phase == (int)DivePhase.BankRoom || (s.Phase == (int)DivePhase.Floor && s.Floor == UndercroftPlan.BankFloor && s.BankChosen));

        // The roster slot of a bag id (the index in idsBySlot), or -1 for none / not there. The saved extra-slot owner feeds BankSlots.PickExtraSlotOwner's previousOwner this way.
        public static int SlotOfId(string id, IReadOnlyList<string> idsBySlot)
        {
            if (id == null || idsBySlot == null) return -1;
            for (int i = 0; i < idsBySlot.Count; i++) if (string.Equals(idsBySlot[i], id, StringComparison.Ordinal)) return i;
            return -1;
        }

        public static string IdOfSlot(int slot, IReadOnlyList<string> idsBySlot) => idsBySlot != null && slot >= 0 && slot < idsBySlot.Count ? idsBySlot[slot] : null;

        // The session a (cleaned) section resumes as (plan 3 task 8a), over the party's restored bags, checkpoint and pieces; null when the section is not resumable (IsResumable) or the stair of the
        // saved floor refuses. The checkpoint is neither spent nor set: a resume is not a start. Push-on already chosen: the saved floor-5 state is restored and the party takes floor 5's stair at once
        // (DiveSession.EnterFloor re-checks it), so it stands at the start of floor 6 with the saved stir, momentum, retry and bank state. The bank room: restored as it was, the visits reopened for the
        // players present now (their saved picks kept, the excess dropped from the end); with nobody present yet the visits stay closed and the host resizes when someone arrives.
        // idsBySlot as in Build; presentSlots[i] / presentIds[i]: the roster slot and bag id of the players present; priorOwner: the 3-player extra slot's previous owner (a slot, -1 for none).
        public static DiveSession Resume(SuspendSave s, PartyBags bags, DiveCheckpoint checkpoint, DivePieces pieces, IReadOnlyList<string> idsBySlot,
            IReadOnlyList<int> presentSlots, IReadOnlyList<string> presentIds, ulong diveSeed, int priorOwner)
        {
            if (!IsResumable(s) || bags == null || checkpoint == null || pieces == null) return null;
            var session = new DiveSession(bags, checkpoint, pieces);
            session.Restore(ToSnapshot(s, idsBySlot));
            if (session.Phase == DivePhase.BankRoom)
            {
                if (presentSlots != null && presentIds != null) session.ResizeBankRoom(presentSlots, presentIds, diveSeed, priorOwner);
                return session;
            }
            return session.EnterFloor(UndercroftPlan.NextFloor(session.Floor, DiveFloorTable.LastBuiltFloor)) ? session : null;
        }

        // ---------------------------------------------------------------- the host's views of the section
        // A copy that keeps only the recorded players' bag records and present ids (the world records at most four; an unrecorded player's never reaches the file).
        public static SuspendSave OnlyRecorded(SuspendSave s, Func<PlayerKey, bool> isRecorded)
        {
            if (s == null) return null;
            SuspendSave c = Copy(s);
            c.Bags = new List<SuspendedBag>();
            foreach (SuspendedBag b in s.Bags ?? new List<SuspendedBag>())
                if (b != null && TryKind(b, out PlayerKey key) && isRecorded(key)) c.Bags.Add(b);
            c.PresentIds = new List<string>();
            foreach (string id in s.PresentIds ?? new List<string>())
                if (id != null && BagsSaveRules.TryParseBagId(id, out PlayerKey key) && isRecorded(key)) c.PresentIds.Add(id);
            if (c.ExtraSlotOwner != null && !(BagsSaveRules.TryParseBagId(c.ExtraSlotOwner, out PlayerKey owner) && isRecorded(owner))) c.ExtraSlotOwner = null;
            return c;
        }

        static bool TryKind(SuspendedBag b, out PlayerKey key)
        {
            key = default;
            if (!BagsSaveRules.TryKind(b.Identity, out IdentityKind kind)) return false;
            key = new PlayerKey(kind, b.Id);
            return true;
        }

        static SuspendSave Copy(SuspendSave s) => new SuspendSave
        {
            Seed = s.Seed, Floor = s.Floor, Phase = s.Phase, BankChosen = s.BankChosen, StartedAtBank = s.StartedAtBank, ClearedMask = s.ClearedMask, ExtraSlotOwner = s.ExtraSlotOwner,
            Stir = s.Stir, Momentum = s.Momentum, Retry = s.Retry, Extra = s.Extra,
        };

        // The id after a Solo world rebinds its owner from `from` to `to` (a bag id "Kind:Id").
        public static string RenameId(string id, PlayerKey from, PlayerKey to) => id != null && id == from.ToString() ? to.ToString() : id;

        // A Solo world rebinding to its owner's new key: the owner's bag record, present id and extra-slot ownership move to `to` (an existing record of `to` is replaced).
        public static void Rename(SuspendSave s, PlayerKey from, PlayerKey to)
        {
            if (s == null || from == to) return;
            if (s.Bags != null)
            {
                bool has = false;
                foreach (SuspendedBag b in s.Bags) if (b != null && b.Identity == (int)from.Kind && b.Id == from.Id) { has = true; break; }
                if (has)
                {
                    s.Bags.RemoveAll(b => b != null && b.Identity == (int)to.Kind && b.Id == to.Id);
                    foreach (SuspendedBag b in s.Bags) if (b != null && b.Identity == (int)from.Kind && b.Id == from.Id) { b.Identity = (int)to.Kind; b.Id = to.Id; }
                }
            }
            if (s.PresentIds != null)
                for (int i = 0; i < s.PresentIds.Count; i++) s.PresentIds[i] = RenameId(s.PresentIds[i], from, to);
            s.ExtraSlotOwner = RenameId(s.ExtraSlotOwner, from, to);
        }
    }
}
