// Authority: test-only. Plan 3 task 7: PlayMode tests that predate the new dive position the host's session by hand. This keeps their call sites compiling while plan 3 task 12 ports them to the
// real flow (start, stair, bank room); it is a hook for tests, never a game path. Delete it with the last caller.
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;

namespace SleepingCalamity.Tests.PlayMode
{
    internal static class DiveTestAdapters
    {
        // The old "heart banked" flag, now: this dive pushed on at the bank room.
        public static bool BankChosenNow(this DiveDirector d) => d.HaulSync != null && d.HaulSync.Current.BankChosen;

        // Puts the host's session on `floor` (0 = home, a fresh Town-phase session over the same bags, checkpoint and pieces) without walking there. Cleared floors, stir and the bank state are
        // kept; the call starts a dive on floor 1 first when none runs. Floors may be jumped (the rules of EnterFloor do not apply: this is a test hook).
        public static bool ServerSetFloor(this DiveHaulSync haul, int floor)
        {
            DiveSession s = haul.Session;
            if (s == null || !haul.IsServer || !haul.IsSpawned) return false;
            if (floor <= 0) return haul.ServerAdopt(new DiveSession(s.Bags, s.Checkpoint, s.Pieces));
            if (s.Phase == DivePhase.Town && !haul.ServerStart(UndercroftPlan.FirstFloor, false)) return false;
            DiveSession.SnapshotData snap = s.Snapshot();
            s.Bags.CancelBankVisits();
            s.Restore(new DiveSession.SnapshotData(DivePhase.Floor, floor, snap.ClearedMask, snap.BankChosenThisDive, snap.StartedAtBank, snap.ExtraSlotOwner, snap.Stir, snap.Momentum, snap.Retry));
            return haul.ServerAdopt(s);
        }
    }
}
