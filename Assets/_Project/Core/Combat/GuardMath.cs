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
        // Combat depth (change 5): a raise only arms the parry if the guard was down at least this long before it; a quicker re-raise still guards. Tapping
        // RMB no longer parries (Critical Points' "primed, then locked out").
        public const float ParryLockoutSeconds = 0.5f;

        public static bool ParryArmed(float guardDownSeconds) => guardDownSeconds >= ParryLockoutSeconds - 1e-4f;

        // guardHeldSeconds: how long the guard has been held (0 = not guarding, use guarding=false). bearingDeg: the angle between the guarder's facing
        // and the direction to the attacker, 0 = dead ahead. A guard-break or an unguardable attack passes straight through. parryArmed: this raise came
        // after the lockout (ParryArmed), so its first frames may parry.
        public static GuardOutcome Resolve(bool guarding, float guardHeldSeconds, float bearingDeg, bool guardBreak, bool guardable, bool parryArmed = true)
        {
            if (!guarding || guardBreak || !guardable) return GuardOutcome.None;
            if (MathF.Abs(bearingDeg) > FrontHalfAngleDeg) return GuardOutcome.None;
            return parryArmed && guardHeldSeconds <= ParrySeconds + 1e-4f ? GuardOutcome.Parried : GuardOutcome.Guarded;
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

        // Combat depth (change 5): a dodge is perfect when the hit it dodged met the roll within its first PerfectSeconds (the roll stays safe for 0.4 s;
        // perfect is the first third), and only if the previous roll ended at least ChainLockoutSeconds before this one began (chained rolls never count).
        public const float PerfectSeconds = 0.15f;
        public const float ChainLockoutSeconds = 0.3f;

        // hitboxOpenSeconds: how long the hitbox had been open when it met the roll. It must have opened after the roll began (rolling into a hitbox that
        // is already open is a dodge, not a perfect one); one 60 Hz frame of slack for the judging tick.
        public static bool IsPerfect(bool active, float elapsed, float gapBeforeRollSeconds, float hitboxOpenSeconds = 0f) =>
            active && elapsed >= 0f && elapsed <= PerfectSeconds + 1e-4f && gapBeforeRollSeconds >= ChainLockoutSeconds - 1e-4f &&
            hitboxOpenSeconds <= elapsed + 1f / 60f;
    }

    // Post-hit invulnerability grace (docs/research/combat-timings.md "Recommended M3 values" 1-3): after a hit lands the hero ignores further hits for a
    // window (light 0.8 s; heavy 1.2 s = the 0.9 s knockdown + 0.3 s; one hit of a multi-hit attack 0.15 s), tinted red by one sine half-cycle over the same
    // window. A heavy hit (knockback >= 3, or a guard break on a raised guard) also knocks the hero down. Authority: owner (the victim's client judges grace
    // and enemy hits, GuardMath.cs:3; the host applies the damage). Other peers run a cosmetic GraceTimer only, started by HurtClientRpc.
    public enum HurtKind : byte { Light = 0, Heavy = 1, MultiHit = 2 }

    public static class HurtGrace
    {
        // Tunable (Core/Tuning, "hurtGrace"); the defaults (3, 0.8, 1.2, 0.15, 0.9, 0.4) are in HurtGraceTuning. Heavy 1.2 s = the 0.9 s knockdown + 0.3 s;
        // the knockdown is research recommendation 3 (DC1's 2.2 s is too long for us); the flicker is recommendation 2 (the red pulse plus a flicker).
        public static float HeavyKnockback => Tuning.HurtGraceTuning.HeavyKnockback;   // attack knockback at or above this is a heavy hit
        public static float LightSeconds => Tuning.HurtGraceTuning.LightSeconds;
        public static float HeavySeconds => Tuning.HurtGraceTuning.HeavySeconds;
        public static float MultiHitSeconds => Tuning.HurtGraceTuning.MultiHitSeconds;
        public static float KnockdownSeconds => Tuning.HurtGraceTuning.KnockdownSeconds;
        public static float FlickerSeconds => Tuning.HurtGraceTuning.FlickerSeconds;

        // No data flag marks a multi-hit attack today (AttackData has none), so callers pass multiHit=false and get light / heavy only.
        // A guard break (on a raised guard) counts as heavy.
        public static HurtKind Classify(float knockback, bool multiHit = false, bool guardBreak = false) =>
            knockback >= HeavyKnockback || guardBreak ? HurtKind.Heavy : multiHit ? HurtKind.MultiHit : HurtKind.Light;

        public static float Seconds(HurtKind kind) => kind == HurtKind.Heavy ? HeavySeconds : kind == HurtKind.MultiHit ? MultiHitSeconds : LightSeconds;

        // True while `elapsed` seconds after a hit is still inside that hit's grace window (the window is [0, Seconds); the edge itself is open again).
        public static bool IsActive(bool hasHit, HurtKind kind, float elapsed) => hasHit && elapsed >= 0f && elapsed < Seconds(kind);

        // Red tint strength 0..1: one sine half-cycle over the window (0 at the start, 1 at the midpoint, 0 at the end); 0 outside it.
        public static float Pulse(bool hasHit, HurtKind kind, float elapsed)
        {
            if (!IsActive(hasHit, kind, elapsed)) return 0f;
            return MathF.Sin(MathF.PI * elapsed / Seconds(kind));
        }

        // A heavy hit: knockback >= 3 or a guard-breaker. The host sends this to every peer so all of them play the same reaction.
        public static bool IsHeavyHit(float knockback, bool guardBreak) => knockback >= HeavyKnockback || guardBreak;
    }

    // The grace window as a countdown (not a sum of floats), so expiry is frame-accurate: 0.8 s at 60 Hz ends on exactly tick 48.
    public sealed class GraceTimer
    {
        private const float Epsilon = 1e-4f;   // float slack: 48 ticks of 1/60 s leave ~1e-7, not a whole extra frame

        private float _remaining;
        private HurtKind _kind;

        public HurtKind Kind => _kind;
        public bool Active => _remaining > Epsilon;
        public float Remaining => Active ? _remaining : 0f;
        public float Elapsed => Active ? HurtGrace.Seconds(_kind) - _remaining : 0f;

        // Begins a window. A hit during a running window is ignored (it neither restarts nor lengthens it, whatever its kind). True when it began.
        public bool Start(HurtKind kind)
        {
            if (Active) return false;
            _kind = kind;
            _remaining = HurtGrace.Seconds(kind);
            return true;
        }

        public void Tick(float dt)
        {
            if (_remaining <= 0f) return;
            _remaining -= dt;
            if (_remaining <= Epsilon) _remaining = 0f;
        }

        public void Clear() => _remaining = 0f;

        // Red tint strength 0..1 (a half sine over the window), 0 when not active.
        public float Pulse => Active ? MathF.Sin(MathF.PI * Elapsed / HurtGrace.Seconds(_kind)) : 0f;

        // True during the first FlickerSeconds of the window, on alternate 1/30 s steps (the on/off beat of the flicker).
        public bool FlickerOn => Active && Elapsed < HurtGrace.FlickerSeconds && ((int)(Elapsed * 30f) & 1) == 0;
        public bool Flickering => Active && Elapsed < HurtGrace.FlickerSeconds;
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
