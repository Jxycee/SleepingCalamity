// Core/Cellars/DiveState.cs: the minimal host-side dive state around the shared haul (PHASE1 7.2) and the party's per-player bags (Bags, plan 2 task 3). Pure C#, no engine references.
// The haul keeps the shared pieces (safe on pickup); each recorded player's loot lives in their PlayerBag inside Bags. A bank point pushes every open bank visit on, the town bank
// brings every bag home, a failed dive fails every bag at the floor the party fell on.
// Current floor (0 = town, 1-4), whether the party has banked at the floor-2 checkpoint (return dives may start from it) and the cleared floors
// (bitmask, bit f-1; floors stay cleared once cleared, even after a failed dive). Bank points: the floor-2 checkpoint and the floor-4 heart.
// No dive flow here; M6 adds the town UI on top of BankInTown().
namespace SleepingCalamity.Core
{
    public sealed class DiveState
    {
        public const int Town = 0, CheckpointFloor = 2, HeartFloor = 4, MaxFloor = 4;

        public DiveHaul Haul { get; }
        public PartyBags Bags { get; } = new PartyBags();   // the host's PlayerBag per player id (PlayerKey string, or the client id without a roster)
        public int CurrentFloor { get; private set; }
        public bool CheckpointReached { get; private set; }
        public int ClearedMask { get; private set; }

        private int _bankedMask;   // bank points used in the current dive (bit f-1); cleared when a dive starts or ends
        public bool BankUsed(int floor) => floor >= 1 && floor <= MaxFloor && (_bankedMask & (1 << (floor - 1))) != 0;
        // A bank press is free to heal and reset the stir only once per bank point per dive, unless a player carries something new (pieces are safe on pickup and never wait).
        public bool CanBankHere => FloorHasBank(CurrentFloor) && (!BankUsed(CurrentFloor) || Bags.AnyCarried);
        // The floor a new dive starts on: the checkpoint floor only once floors 1-2 are fully collected.
        public int StartFloor => DiveRoute.StartFloor(CheckpointReached, Haul.PiecesLeft(1) + Haul.PiecesLeft(2));

        public StirMeter Stir { get; }   // floors cleared feed it (pieces no longer do, spec 4.5); a bank stops the countdown; town bank and failed dive reset it

        public DiveState(DiveHaul haul) : this(haul, StirTuning.Default) { }
        public DiveState(DiveHaul haul, StirTuning tuning) { Haul = haul; Stir = new StirMeter(haul, tuning); }

        public static bool FloorHasBank(int floor) => floor == CheckpointFloor || floor == HeartFloor;

        public bool SetFloor(int floor)
        {
            if (floor < Town || floor > MaxFloor) return false;
            if (CurrentFloor == Town || floor == Town) _bankedMask = 0;   // a dive starts or ends
            CurrentFloor = floor;
            return true;
        }

        public bool IsCleared(int floor) => floor >= 1 && floor <= MaxFloor && (ClearedMask & (1 << (floor - 1))) != 0;

        public bool MarkCleared(int floor)
        {
            if (floor < 1 || floor > MaxFloor) return false;
            ClearedMask |= 1 << (floor - 1);
            Stir.OnFloorCleared(floor);
            return true;
        }

        // Banks at the current floor's bank point. False (nothing changes) when this floor has none.
        public bool BankAtPoint()
        {
            if (!CanBankHere) return false;
            _bankedMask |= 1 << (CurrentFloor - 1);
            Haul.Bank();
            Bags.PushOnAll();
            Stir.OnBank();
            if (CurrentFloor == CheckpointFloor) CheckpointReached = true;
            return true;
        }

        // For the M6 town flow and the stir reset: banking at home needs no bank point.
        public bool BankInTown()
        {
            if (CurrentFloor != Town) return false;
            Haul.Bank();
            Bags.GoHomeAll();
            Stir.ResetAll();
            return true;
        }

        // The party lost the dive: the legacy unbanked loot is gone, every bag fails at the floor the party fell on (carried items wait in a satchel), collected pieces stay safe,
        // party home. Cleared floors and the checkpoint stay.
        public void FailDive()
        {
            Haul.FailDive();
            Bags.FailAll(CurrentFloor);
            Stir.ResetAll();
            CurrentFloor = Town;
            _bankedMask = 0;
        }

        // Save restore (M6 S2): the cleared floors and the checkpoint come back as saved. The party is in town, so the stir meter is not touched.
        public void RestoreProgress(int clearedMask, bool checkpointReached)
        {
            ClearedMask = clearedMask & ((1 << MaxFloor) - 1);
            CheckpointReached = checkpointReached;
        }

        // Host clock: advances the stir countdown. True when the deep closed and the dive was failed.
        public bool Tick(float dt)
        {
            if (!Stir.Tick(dt)) return false;
            FailDive();
            return true;
        }
    }
}
