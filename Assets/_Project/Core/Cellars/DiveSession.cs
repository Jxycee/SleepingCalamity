// Core/Cellars/DiveSession.cs: the host's rules for one dive (plan 3 task 2; spec decisions 13, 19, 25, 31, 32, 36-38). Pure C#, no clocks (the host ticks Stir).
// Authority: host only (owned by DiveDirector). The director keeps PartyBags, DiveCheckpoint and DivePieces across dives and builds a fresh session per dive.
// Floor 5 is the bank room; push-on and go-home are the one choice there. Owns the host's dive rules (it replaced the 4-floor dive state).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum DivePhase { Town, Floor, BankRoom, Ended }
    public enum DiveEnd { Failed, WentHome, Finished }

    public sealed class DiveSession
    {
        public DiveSession(PartyBags bags, DiveCheckpoint checkpoint, DivePieces pieces)
        {
            Bags = bags ?? throw new ArgumentNullException(nameof(bags));
            Checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            Pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        }

        int _clearedMask;   // floors cleared this dive (bit f-1)

        public DivePhase Phase { get; private set; } = DivePhase.Town;
        public int Floor { get; private set; }
        public DiveStir Stir { get; } = new DiveStir();
        public DiveMomentum Momentum { get; } = new DiveMomentum();
        public BossRetry Retry { get; } = new BossRetry();
        public PartyBags Bags { get; }
        public DiveCheckpoint Checkpoint { get; }
        public DivePieces Pieces { get; }
        public bool BankChosenThisDive { get; private set; }
        public bool StartedAtBank { get; private set; }
        public DiveEnd? Ended { get; private set; }
        public int ExtraSlotOwner { get; private set; } = -1;   // the 3-player extra slot's owner for this visit (the director saves it, Task 6)

        // The states a quit saves as a bank suspend instead of a failed dive (decision 9): the bank room, or floor 5 after the push-on (until the party takes the stair).
        public bool SuspendsAtBank => Phase == DivePhase.BankRoom || (Phase == DivePhase.Floor && Floor == UndercroftPlan.BankFloor && BankChosenThisDive);

        public bool IsCleared(int floor) => UndercroftPlan.IsFloor(floor) && (_clearedMask & (1 << (floor - 1))) != 0;

        public bool TryStart(int floor, bool bossBeaten)
        {
            if (Phase != DivePhase.Town) return false;
            if (!Checkpoint.TryStart(floor, bossBeaten)) return false;
            Stir.ResetForNewDive();
            Momentum.OnDiveStart(floor);
            Retry.OnDiveStart();
            Bags.CancelBankVisits();
            BankChosenThisDive = false;
            Ended = null;
            _clearedMask = 0;
            Floor = floor;
            Phase = DivePhase.Floor;
            StartedAtBank = floor == UndercroftPlan.BankFloor;   // decision 2: the party arrives at the bank room with the guardian beaten
            if (StartedAtBank) _clearedMask |= 1 << (floor - 1);   // cleared without stirring
            return true;
        }

        public bool EnterFloor(int floor)
        {
            if (Phase != DivePhase.Floor || !DiveFloorTable.HasStair(Floor, BankChosenThisDive)) return false;
            if (floor != UndercroftPlan.NextFloor(Floor, DiveFloorTable.LastBuiltFloor)) return false;
            Stir.OnStairTaken();
            Floor = floor;
            return true;
        }

        public void ClearFloor(int floor)
        {
            if (Phase != DivePhase.Floor || floor != Floor || IsCleared(floor)) return;
            _clearedMask |= 1 << (floor - 1);
            Stir.OnFloorCleared(floor);
        }

        // presentSlots are the recorded slots of the players here; playerIds (optional, parallel) are their bag ids. Without ids the bags are paired in id order.
        public bool EnterBankRoom(IReadOnlyList<int> presentSlots, ulong diveSeed, int previousExtraOwner, IReadOnlyList<string> playerIds = null)
        {
            if (Phase != DivePhase.Floor || Floor != UndercroftPlan.BankFloor || !IsCleared(Floor) || BankChosenThisDive) return false;
            if (presentSlots == null || presentSlots.Count < 1 || presentSlots.Count > 4) return false;
            int n = presentSlots.Count;
            ExtraSlotOwner = n == 3 ? BankSlots.PickExtraSlotOwner(diveSeed, presentSlots, previousExtraOwner) : -1;
            var ids = new List<string>();
            if (playerIds != null) ids.AddRange(playerIds);
            else foreach (var kv in Bags.All) ids.Add(kv.Key);
            for (int i = 0; i < n && i < ids.Count; i++) Bags.For(ids[i]).OpenBank(BankSlots.SlotsFor(n, presentSlots[i], ExtraSlotOwner));
            Phase = DivePhase.BankRoom;
            return true;
        }

        // The party changed while the bank room is open (a player joined, left or came back): capacity follows who is present (decision 7). playerIds[i] is the bag id of the player in
        // presentSlots[i]. A bag of anyone not listed has its visit cancelled (its picks are dropped, its items stay carried); a listed bag keeps every pick (BankSlots.Allocate sizes capacity from the present party, never below the picks, and a late joiner gets what remains of
        // TotalSlots); a listed bag with no visit gets a fresh one. An absent bag whose visit is already closed (a restored suspend) keeps its picks until it is back or the party pushes on. The 3-player extra slot keeps its owner while that owner is still present; otherwise it is picked again from the
        // seed, never priorOwner (the owner of the previous visit), and a party of any other size has none.
        public bool ResizeBankRoom(IReadOnlyList<int> presentSlots, IReadOnlyList<string> playerIds, ulong diveSeed, int priorOwner)
        {
            if (Phase != DivePhase.BankRoom || presentSlots == null || playerIds == null) return false;
            int n = presentSlots.Count;
            if (n < 1 || n > 4 || playerIds.Count != n) return false;
            if (n == 3)
            {
                bool still = false;
                foreach (int s in presentSlots) if (s == ExtraSlotOwner) { still = true; break; }
                if (!still) ExtraSlotOwner = BankSlots.PickExtraSlotOwner(diveSeed, presentSlots, priorOwner);
            }
            else ExtraSlotOwner = -1;
            foreach (var kv in Bags.All)
            {
                bool here = false;
                foreach (string id in playerIds) if (id == kv.Key) { here = true; break; }
                if (!here && kv.Value.BankOpen) kv.Value.CancelBank();   // a leaver's open visit ends; an absent bag whose visit is already closed keeps its saved picks (a restored suspend)
            }
            var picks = new int[n];
            for (int i = 0; i < n; i++) picks[i] = Bags.For(playerIds[i]).Picks.Count;
            int[] capacities = BankSlots.Allocate(n, presentSlots, ExtraSlotOwner, picks);   // never below a player's picks; the party never above TotalSlots(n) unless the picks alone are
            for (int i = 0; i < n; i++)
            {
                PlayerBag bag = Bags.For(playerIds[i]);
                int capacity = capacities[i];
                if (bag.BankOpen || bag.Picks.Count > 0) bag.ReopenBank(capacity);   // keeps the picks (a restored suspend holds picks with the visit closed)
                else bag.OpenBank(capacity);
            }
            return true;
        }

        public bool PushOn()
        {
            if (Phase != DivePhase.BankRoom || BankChosenThisDive) return false;
            Bags.PushOnAll();   // picks leave Carried
            Stir.UnbankedValuables = Bags.CarriedValuables();   // the caller orders this: OnPushOn takes it as the baseline
            Stir.OnPushOn();
            Momentum.OnPushOn();
            BankChosenThisDive = true;
            Phase = DivePhase.Floor;
            return true;
        }

        public bool GoHome()
        {
            bool atBank = Phase == DivePhase.BankRoom || (Phase == DivePhase.Floor && Floor == UndercroftPlan.BankFloor && IsCleared(Floor));
            if (!atBank || BankChosenThisDive) return false;
            Bags.GoHomeAll();
            Checkpoint.OnWentHomeFromBank();
            End(DiveEnd.WentHome);
            return true;
        }

        // The Phase 1 stand-in ending after floor 7 brings everyone home; no checkpoint.
        public bool Finish()
        {
            if (Phase != DivePhase.Floor || Floor != DiveFloorTable.LastBuiltFloor) return false;
            Bags.GoHomeAll();
            End(DiveEnd.Finished);
            return true;
        }

        public void Fail()
        {
            if (Phase != DivePhase.Floor && Phase != DivePhase.BankRoom) return;
            Bags.FailAll(Floor);
            End(DiveEnd.Failed);
        }

        void End(DiveEnd how) { Ended = how; Phase = DivePhase.Ended; }

        public readonly struct SnapshotData
        {
            public DivePhase Phase { get; }
            public int Floor { get; }
            public int ClearedMask { get; }
            public bool BankChosenThisDive { get; }
            public bool StartedAtBank { get; }
            public int ExtraSlotOwner { get; }
            public DiveStir.State Stir { get; }
            public DiveMomentum.State Momentum { get; }
            public BossRetry.State Retry { get; }

            public SnapshotData(DivePhase phase, int floor, int clearedMask, bool bankChosen, bool startedAtBank, int extraSlotOwner, DiveStir.State stir, DiveMomentum.State momentum, BossRetry.State retry)
            {
                Phase = phase; Floor = floor; ClearedMask = clearedMask; BankChosenThisDive = bankChosen; StartedAtBank = startedAtBank; ExtraSlotOwner = extraSlotOwner;
                Stir = stir; Momentum = momentum; Retry = retry;
            }
        }

        public SnapshotData Snapshot() => new SnapshotData(Phase, Floor, _clearedMask, BankChosenThisDive, StartedAtBank, ExtraSlotOwner, Stir.Capture(), Momentum.Capture(), Retry.Capture());

        public void Restore(SnapshotData s)
        {
            Phase = s.Phase; Floor = s.Floor; _clearedMask = s.ClearedMask; BankChosenThisDive = s.BankChosenThisDive; StartedAtBank = s.StartedAtBank; ExtraSlotOwner = s.ExtraSlotOwner;
            Ended = null;
            Stir.Restore(s.Stir); Momentum.Restore(s.Momentum); Retry.Restore(s.Retry);
        }
    }
}
