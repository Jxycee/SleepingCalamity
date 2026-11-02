// Core/Cellars/DiveState.cs: the minimal host-side dive state around the shared haul (PHASE1 7.2). Pure C#, no engine references.
// Current floor (0 = town, 1-4), whether the party has banked at the floor-2 checkpoint (return dives may start from it) and the cleared floors
// (bitmask, bit f-1; floors stay cleared once cleared, even after a failed dive). Bank points: the floor-2 checkpoint and the floor-4 heart.
// No dive flow here; M6 adds the town UI on top of BankInTown().
namespace SleepingCalamity.Core
{
    public sealed class DiveState
    {
        public const int Town = 0, CheckpointFloor = 2, HeartFloor = 4, MaxFloor = 4;

        public DiveHaul Haul { get; }
        public int CurrentFloor { get; private set; }
        public bool CheckpointReached { get; private set; }
        public int ClearedMask { get; private set; }

        public StirMeter Stir { get; }   // floors cleared / unbanked pieces / banks feed it; town bank and failed dive reset it

        public DiveState(DiveHaul haul) : this(haul, StirTuning.Default) { }
        public DiveState(DiveHaul haul, StirTuning tuning) { Haul = haul; Stir = new StirMeter(haul, tuning); }

        public static bool FloorHasBank(int floor) => floor == CheckpointFloor || floor == HeartFloor;

        public bool SetFloor(int floor)
        {
            if (floor < Town || floor > MaxFloor) return false;
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
            if (!FloorHasBank(CurrentFloor)) return false;
            Haul.Bank();
            Stir.OnBank();
            if (CurrentFloor == CheckpointFloor) CheckpointReached = true;
            return true;
        }

        // For the M6 town flow and the stir reset: banking at home needs no bank point.
        public bool BankInTown()
        {
            if (CurrentFloor != Town) return false;
            Haul.Bank();
            Stir.ResetAll();
            return true;
        }

        // The party lost the dive: unbanked haul gone, pieces back on their floors, party home. Cleared floors and the checkpoint stay.
        public void FailDive()
        {
            Haul.FailDive();
            Stir.ResetAll();
            CurrentFloor = Town;
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
