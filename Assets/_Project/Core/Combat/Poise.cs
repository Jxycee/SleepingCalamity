// Core/Combat/Poise.cs: what makes an enemy answer a mash (docs/design/m3-combat-greybox.md, "Proposal: enemies that answer the mash";
// docs/research/combat-depth.md). Poise (DC2's stagger accumulator), steadiness after a flinch, the committed window from the glint, openings that pay
// extra, the broom's guard count that ends in a riposte. Pure C#. Every number is a P9 Placeholder.
// Authority: host. The host's EnemyModel holds one PoiseMeter and one GuardCounter per enemy; the windows are pure functions of the replicated attack clock,
// so a client may evaluate them for cosmetics (the clank, the opening spark) but only the host's answer changes the fight.
using System;

namespace SleepingCalamity.Core.Combat
{
    // Each hit adds its attack's poise; the count clears ResetSeconds after the last hit (DC2: 60 steps). Reaching the threshold breaks the enemy's poise
    // (it flinches) and empties the count. After the flinch ends the host calls BeginSteady: for SteadySeconds hits add nothing and cannot break it, so
    // a flinch, break, flinch loop cannot stunlock. The threshold grows with the party like the revenge cap: x (1 + 0.5 per extra player).
    public sealed class PoiseMeter
    {
        public const float ResetSeconds = 1.0f;
        public const float SteadySeconds = 2.0f;
        public const float PerExtraPlayer = 0.5f;

        public float Threshold { get; }
        public float Value { get; private set; }
        public float SinceLastHit { get; private set; } = float.MaxValue;
        public float SteadyLeft { get; private set; }
        public bool Steady => SteadyLeft > 0f;

        public PoiseMeter(float baseThreshold, int players = 1) { Threshold = ThresholdFor(baseThreshold, players); }

        public static float ThresholdFor(float baseThreshold, int players) =>
            Math.Max(0f, baseThreshold) * (1f + PerExtraPlayer * (CoopScaling.ClampPlayers(players) - 1));

        // Adds a hit's poise; true when it broke the enemy's poise (the count is emptied). Nothing is added while steady.
        public bool Add(float amount)
        {
            if (Steady || !(amount > 0f) || float.IsInfinity(amount)) return false;
            Value += amount;
            SinceLastHit = 0f;
            if (Value + 1e-4f < Threshold) return false;
            Value = 0f;
            return true;
        }

        public void Tick(float dt)
        {
            if (SteadyLeft > 0f) SteadyLeft = Math.Max(0f, SteadyLeft - dt);
            if (SinceLastHit < float.MaxValue) SinceLastHit += dt;
            if (Value > 0f && SinceLastHit >= ResetSeconds) Value = 0f;
        }

        // The flinch just ended: steady for SteadySeconds, count emptied.
        public void BeginSteady()
        {
            SteadyLeft = SteadySeconds;
            Value = 0f;
        }
    }

    // When an enemy's own attack makes it hard to stop (committed) or easy to punish (open). Frames are the attack's 60 Hz clock.
    public static class EnemyWindows
    {
        public const float OpeningDamage = 1.5f;     // hits on an open enemy deal x1.5 (a named DamageInput.Other factor)
        public const float OpeningPoise = 2f;        // and add double poise
        public const float PerfectDodgeOpenSeconds = 1.0f, PerfectDodgeOpenSecondsElite = 0.7f;
        public const float GuardBreakSeconds = 1.2f; // a guard-break hit on the broom's front: a flinch that is an opening

        // From the glint (the tell's commit frame) to the end of the hitbox: hits deal damage but never flinch it (KH / DMC armour windows).
        // Attacks without a tell (tests) are never committed.
        public static bool Committed(AttackData a, float frame) =>
            a != null && a.Tell != null && frame >= a.Tell.Commit && frame < a.HitOn.End;

        // After the hitbox closes the attack is spent: the rest of it (and the Recover phase after it) is the punish window.
        public static bool Spent(AttackData a, float frame) => a != null && frame >= a.HitOn.End;
    }

    // The broom's guard (change 4): front hits its bristles block are counted; BlocksToRiposte of them inside WindowSeconds start the bristle riposte.
    public sealed class GuardCounter
    {
        public const int BlocksToRiposte = 3;
        public const float WindowSeconds = 2.0f;

        readonly float[] _ages = new float[BlocksToRiposte];
        int _count;

        public int Count => _count;

        // A front hit was blocked. True when this block completes the count (the count empties and the enemy should riposte).
        public bool OnBlocked()
        {
            if (_count == BlocksToRiposte) _count--;   // cannot happen (it empties on the trigger), kept for safety
            _ages[_count++] = 0f;
            if (_count < BlocksToRiposte) return false;
            _count = 0;
            return true;
        }

        public void Tick(float dt)
        {
            int keep = 0;
            for (int i = 0; i < _count; i++)
            {
                float age = _ages[i] + dt;
                if (age < WindowSeconds) _ages[keep++] = age;
            }
            _count = keep;
        }

        public void Reset() { _count = 0; }
    }
}
