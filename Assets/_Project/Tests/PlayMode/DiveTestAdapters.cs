// Authority: test-only. Hooks for the PlayMode tests that run a DiveHaulSync ALONE (no director, so no real start, stair or bank walk): they position the host's session by hand. The tests with a
// director (the walks, the disconnect cases, the hub, the bots) use the real flow since plan 3 task 12; this file keeps only what the haul-only rigs still call. Never a game path.
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;

namespace SleepingCalamity.Tests.PlayMode
{
    internal static class DiveTestAdapters
    {
        // The bank room, the way the party gets there: floor 5 cleared (its guardian), then the bank press. False when the session refuses.
        public static bool BankRoomNow(this DiveHaulSync haul)
        {
            if (!haul.ServerSetFloor(UndercroftPlan.BankFloor)) return false;
            haul.ServerMarkCleared(UndercroftPlan.BankFloor);
            return haul.ServerEnterBankRoom();
        }

        // Every connected player presses "push on" (the real choice, one press each): a solo party confirms at once, a larger one on the second press. True once the choice stands.
        public static bool PushOnNow(this DiveHaulSync haul)
        {
            if (!haul.IsServer || haul.Session == null) return false;
            foreach (ulong id in new System.Collections.Generic.List<ulong>(haul.NetworkManager.ConnectedClientsIds)) haul.ServerChoosePushOn(id);
            return haul.Session.BankChosenThisDive;
        }

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
