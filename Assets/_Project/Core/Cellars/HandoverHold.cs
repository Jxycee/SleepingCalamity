// Pure rule for the owner-side gravity hold during a dive floor hand-over (DiveDirector.UpdateHold applies it to HeroLocomotion.HoldVertical).
// Key = the floor this peer sees (0 = home / no dive, -1 = a dive is on but the floor is not resolved yet); applied = the floor of the last arrival teleport applied (0 = none / home).
// Hold while they differ: the floor under the hero was swapped but its teleport has not landed. The timer restarts when the key changes and releases the hold after `timeout` (a lost teleport never freezes a hero).
namespace SleepingCalamity.Core
{
    public struct HandoverHoldState
    {
        public bool Active; public int Key; public float Since;
    }

    public static class HandoverHold
    {
        public const int SeedOnlyOffset = 1000;   // a new seed on the same floor number is still a different floor under the hero

        // The key for Decide, from whichever replicated variable changed first (the haul's current floor or the floor sync's (seed, floor); they are two NetworkVariables that can land in different frames).
        // 0 = no dive; -1 = a dive is on but neither is resolved (haulFloor <= 0 and syncFloor <= 0).
        public static int Key(bool diveOn, int haulFloor, int syncFloor, uint syncSeed, int appliedFloor, uint appliedSeed)
        {
            if (!diveOn) return 0;
            if (haulFloor > 0 && haulFloor != appliedFloor) return haulFloor;
            if (syncFloor > 0)
            {
                if (syncFloor != appliedFloor) return syncFloor;
                if (syncSeed != appliedSeed) return syncFloor + SeedOnlyOffset;
            }
            return haulFloor > 0 ? haulFloor : (syncFloor > 0 ? syncFloor : -1);
        }

        public static bool Decide(ref HandoverHoldState s, int key, int appliedFloor, float now, float timeout)
        {
            if (key == appliedFloor) { s.Active = false; return false; }
            if (!s.Active || key != s.Key) { s.Active = true; s.Key = key; s.Since = now; }
            return now - s.Since < timeout;
        }
    }
}
