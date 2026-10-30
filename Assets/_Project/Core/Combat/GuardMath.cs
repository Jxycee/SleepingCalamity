// Core/Combat/GuardMath.cs: the dagger-and-shield guard (P46), the dodge's invulnerability window with the owner's leniency, the broom's bristles
// and the stacked-hitstop budget (docs/PHASE1.md 6.5). Pure C#.
// Authority: owner (the victim's client judges enemy hits against its own guard and dodge, so what the player saw is what happened).
using System;

namespace SleepingCalamity.Core.Combat
{
    public enum GuardOutcome : byte { None = 0, Guarded = 1, Parried = 2 }

    public static class GuardMath
    {
        public const float FrontHalfAngleDeg = 60f;   // a 120 degree frontal arc
        public const float GuardedDamage = 0.2f;      // guarded damage x0.2
        public const int ParryFrames = 6;             // the first 6 frames of a guard (60 Hz) parry
        public const float ParrySeconds = ParryFrames / 60f;

        // guardHeldSeconds: how long the guard has been held (0 = not guarding, use guarding=false). bearingDeg: the angle between the guarder's facing
        // and the direction to the attacker, 0 = dead ahead. A guard-break or an unguardable attack passes straight through.
        public static GuardOutcome Resolve(bool guarding, float guardHeldSeconds, float bearingDeg, bool guardBreak, bool guardable)
        {
            if (!guarding || guardBreak || !guardable) return GuardOutcome.None;
            if (MathF.Abs(bearingDeg) > FrontHalfAngleDeg) return GuardOutcome.None;
            return guardHeldSeconds <= ParrySeconds + 1e-4f ? GuardOutcome.Parried : GuardOutcome.Guarded;
        }
    }

    public static class DodgeTiming
    {
        public const int TotalFrames = 30, IFrameStart = 5, IFrameEnd = 18;   // frames 5-18 of 30: a 0.5 s roll
        public const float LenienceSeconds = 0.1f;                            // the owner's dodge state wins within 100 ms of its window, either side
        public const float IFrameStartSeconds = IFrameStart / 60f, IFrameEndSeconds = IFrameEnd / 60f;

        // True when a hit judged now is dodged. `elapsed` is the roll's time since it began; `active` is whether a roll is running. The window is
        // frames 5-18 widened by LenienceSeconds on both sides (so the whole of the first 0.4 s of a roll is safe), and never outlives the roll.
        public static bool IsInvulnerable(bool active, float elapsed)
        {
            if (!active) return false;
            return elapsed >= IFrameStartSeconds - LenienceSeconds && elapsed <= IFrameEndSeconds + LenienceSeconds;
        }

        // The strict window, with no leniency (what the animation shows).
        public static bool IsInIFrames(bool active, float elapsed) => active && elapsed >= IFrameStartSeconds && elapsed <= IFrameEndSeconds;
    }

    // The broom's bristle front (6.6): 80% of frontal damage is blocked unless the hit comes from the flank or behind or is a guard-breaker.
    public static class FrontalBlock
    {
        public const float HalfAngleDeg = 70f;     // a 140 degree front
        public const float PassedDamage = 0.2f;    // 80% blocked

        public static float Multiplier(bool hasBlock, float bearingToAttackerDeg, bool guardBreak)
        {
            if (!hasBlock || guardBreak) return 1f;
            return MathF.Abs(bearingToAttackerDeg) <= HalfAngleDeg ? PassedDamage : 1f;
        }
    }

    // Stacked hitstop is capped so a flurry (or a teammate's hits) never stutters the view: at most 16 frames granted in any 0.5 s (was 12 before the
    // readability pass lengthened heavy hits to 14: a finisher still gets all of its frames and the hit after it gets a couple).
    public sealed class HitstopBudget
    {
        public const int CapFrames = 16;
        public const float WindowSeconds = 0.5f;

        readonly System.Collections.Generic.Queue<(float t, int frames)> _grants = new System.Collections.Generic.Queue<(float, int)>();

        // The frames actually granted for a request made at `now` (seconds, any monotonic clock): 0..requested, never above the remaining budget.
        public int Request(int frames, float now)
        {
            if (frames <= 0) return 0;
            while (_grants.Count > 0 && now - _grants.Peek().t >= WindowSeconds) _grants.Dequeue();
            int used = 0;
            foreach (var g in _grants) used += g.frames;
            int give = Math.Min(frames, Math.Max(0, CapFrames - used));
            if (give > 0) _grants.Enqueue((now, give));
            return give;
        }

        public void Clear() => _grants.Clear();
    }
}
