// Core, no engine references, unit-tested. The hard lock (docs/PHASE1.md 6.1/6.5): one locked target, owner-local (never networked).
//   Acquire: the best target inside 10 m (SoftTargeting.PickAcquire).  Hold: until the target is over 13 m away (hysteresis), or has been
//   out of sight for 1.0 s in a row, or died or vanished.  Elites and bosses (IsElite) never lose the lock to range or sight; only the
//   player's own toggle, their death or their removal ends it.  Flick: switches to the nearest target in a stick direction.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum LockDrop : byte
    {
        None = 0,
        Released = 1,    // the player toggled the lock off
        Dead = 2,
        Missing = 3,     // not in the candidate list any more (despawned)
        OutOfRange = 4,  // further than ReleaseRange
        LostSight = 5    // no line of sight for LosGraceSeconds
    }

    public sealed class TargetLock
    {
        public const int None = -1;

        public int TargetId { get; private set; } = None;
        public bool IsLocked => TargetId != None;
        public float SecondsWithoutSight { get; private set; }
        public LockDrop LastDrop { get; private set; }

        // Locks the best target around the seeker. False (and no change) when nothing qualifies. Replaces an existing lock.
        public bool TryAcquire(in TargetSeeker seeker, IReadOnlyList<TargetCandidate> candidates)
        {
            int index = SoftTargeting.PickAcquire(in seeker, candidates);
            if (index < 0) return false;
            TargetId = candidates[index].Id;
            SecondsWithoutSight = 0f;
            LastDrop = LockDrop.None;
            return true;
        }

        // The player's own toggle: always ends the lock, elite or not.
        public void Release()
        {
            if (!IsLocked) return;
            Clear(LockDrop.Released);
        }

        // Once per frame while locked. Returns the reason when this tick dropped the lock, LockDrop.None otherwise.
        public LockDrop Tick(float dt, in TargetSeeker seeker, IReadOnlyList<TargetCandidate> candidates)
        {
            if (!IsLocked) return LockDrop.None;
            int index = IndexOf(candidates, TargetId);
            if (index < 0) return Clear(LockDrop.Missing);

            TargetCandidate c = candidates[index];
            if (!c.Alive) return Clear(LockDrop.Dead);
            if (c.IsElite)
            {
                SecondsWithoutSight = 0f;
                return LockDrop.None;
            }

            float distance = SoftTargeting.FlatDistance(c.X - seeker.X, c.Z - seeker.Z);
            if (distance > TargetingConfig.ReleaseRange) return Clear(LockDrop.OutOfRange);

            if (c.HasLineOfSight)
            {
                SecondsWithoutSight = 0f;
            }
            else
            {
                SecondsWithoutSight += dt;
                if (SecondsWithoutSight >= TargetingConfig.LosGraceSeconds) return Clear(LockDrop.LostSight);
            }
            return LockDrop.None;
        }

        // Right-stick flick: (dirX, dirZ) is the WORLD flat direction of the flick (see FlickInput.ToWorld). Picks the nearest other target
        // whose direction from the CURRENT target lies within FlickConeHalfAngleDeg of the flick. False (lock kept) when there is none.
        public bool TrySwitch(in TargetSeeker seeker, float dirX, float dirZ, IReadOnlyList<TargetCandidate> candidates)
        {
            if (!IsLocked) return false;
            if (dirX * dirX + dirZ * dirZ < 1e-6f) return false;
            int currentIndex = IndexOf(candidates, TargetId);
            if (currentIndex < 0) return false;
            TargetCandidate current = candidates[currentIndex];

            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i == currentIndex) continue;
                TargetCandidate c = candidates[i];
                if (!c.Alive || !c.HasLineOfSight) continue;
                if (SoftTargeting.FlatDistance(c.X - seeker.X, c.Z - seeker.Z) > TargetingConfig.AcquireRange) continue;
                if (MathF.Abs(c.Y - seeker.Y) > TargetingConfig.HardHeightBand) continue;

                float dx = c.X - current.X;
                float dz = c.Z - current.Z;
                float distance = SoftTargeting.FlatDistance(dx, dz);
                if (distance < 1e-4f) continue;   // stacked on the current target: not "in a direction"
                if (SoftTargeting.AngleDeg(dirX, dirZ, dx, dz) > TargetingConfig.FlickConeHalfAngleDeg) continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            if (best < 0) return false;
            TargetId = candidates[best].Id;
            SecondsWithoutSight = 0f;
            return true;
        }

        private LockDrop Clear(LockDrop reason)
        {
            TargetId = None;
            SecondsWithoutSight = 0f;
            LastDrop = reason;
            return reason;
        }

        private static int IndexOf(IReadOnlyList<TargetCandidate> candidates, int id)
        {
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Id == id) return i;
            return -1;
        }
    }
}
