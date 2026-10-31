// Core/Combat/EnemyTell.cs: how an enemy stages an attack's wind-up on its own body, so the attack reads without any ground marker (user, 2026-10-30).
// Pure C#. Authority: none (cosmetic; every peer samples it from its own copy of the replicated attack clock).
//
// The staging follows the reference games (docs/research/combat-timings.md, Kingdom Hearts 1 and 2, DC1): an anticipation pose (lean back, crouch, coil or
// rise) that peaks, a short HOLD at the peak (KH1's first enemy holds its pose 8 of its 42 ticks), then the COMMIT: a glint on the attacking part and a
// "shing" 12-24 frames before the hitbox opens (the instant to roll or guard), a fast release into the strike, and an eased recovery. Lead times from the
// research: early KH and DC1 enemies give 0.65-0.7 s from motion start to damage, single big strikes 1.2-1.3 s.
using System;

namespace SleepingCalamity.Core.Combat
{
    // The part of the body that glints when the attack commits (the presentation finds it by name on the greybox rig).
    public enum TellPart : byte { Body = 0, Head = 1, Tail = 2, Bristles = 3, Handle = 4, RollingPin = 5, Crown = 6, Lid = 7 }

    public enum TellStage : byte { None = 0, Anticipate = 1, Hold = 2, Commit = 3, Strike = 4, Recover = 5 }

    public sealed class EnemyTell
    {
        public readonly TellPart Part;
        public readonly int PoseEnd;      // frame the anticipation pose peaks; held from here to Commit
        public readonly int Commit;       // frame of the glint and its sound; the attacker no longer turns after it
        public readonly float LeanDeg;    // + leans back (pitch) at the peak; the strike overshoots forward by half
        public readonly float Crouch;     // 0..0.5 squash at the peak; the strike stretches
        public readonly float Rise;       // metres the body lifts at the peak (slams come down from it)
        public readonly float TwistDeg;   // yaw coil away from the swing at the peak (spins and sweeps unwind past it)
        public readonly float Tremble;    // 0..1 shiver while holding
        public readonly bool ChargeGlow;  // the part glows hotter through the hold (the elite's signature)
        public readonly string WindUpSfx; // played when the attack starts (CombatSfx id)
        public readonly string HoldSfx;   // played when the pose peaks, or null
        public readonly float Pitch;      // pitch of both, per enemy size

        public EnemyTell(TellPart part, int poseEnd, int commit, float leanDeg = 0f, float crouch = 0f, float rise = 0f, float twistDeg = 0f, float tremble = 0f,
            string windUpSfx = null, float pitch = 1f, string holdSfx = null, bool chargeGlow = false)
        {
            Part = part; PoseEnd = poseEnd; Commit = commit; LeanDeg = leanDeg; Crouch = crouch; Rise = rise; TwistDeg = twistDeg; Tremble = tremble;
            WindUpSfx = windUpSfx ?? CombatSfx.WindUp; Pitch = pitch; HoldSfx = holdSfx; ChargeGlow = chargeGlow;
        }

        public int HoldFrames => Commit - PoseEnd;
    }

    // The body's offsets at one frame of an attack, relative to its rest pose. Squash 1 is the rest height.
    public struct TellPose
    {
        public TellStage Stage;
        public float Lean, Squash, Rise, Twist, Tremble;
        public float Glint;    // 1 at the commit frame, fading to 0 over TellMath.GlintFrames
        public float Charge;   // 0..1 through the hold, ChargeGlow tells only
        public static TellPose Rest => new TellPose { Squash = 1f };
    }

    public static class TellMath
    {
        public const int GlintFrames = 10;      // how long the glint flare lasts
        public const int ReleaseFrames = 6;     // the snap from the held pose into the strike, ending as the hitbox opens
        public const int RecoverFrames = 20;    // easing back to rest after the hitbox closes
        public const int MinReactFrames = 12;   // commit to hitbox: 0.2 s, a human reaction; the dodge's i-frames start at once (100 ms leniency)
        public const int MaxReactFrames = 24;   // more than 0.4 s and the glint stops meaning "now"
        public const float StrikeLean = -0.5f, StrikeStretch = 0.2f, StrikeTwist = -0.6f;

        // True when the clock passed `mark` this step (prev < mark <= curr). Compare by crossing, never by equality: a frame can be skipped at low fps.
        public static bool Crossed(float prev, float curr, int mark) => prev < mark && curr >= mark;

        public static TellPose Sample(AttackData a, float frame)
        {
            TellPose p = TellPose.Rest;
            EnemyTell t = a?.Tell;
            if (t == null || frame < 0f || frame >= a.TotalFrames) return p;

            float strikeStart = a.HitOn.Start, strikeEnd = a.HitOn.End;
            float releaseFrom = Math.Max(t.Commit, strikeStart - ReleaseFrames);
            if (frame < t.PoseEnd)
            {
                p.Stage = TellStage.Anticipate;
                Peak(t, Smooth(t.PoseEnd <= 0 ? 1f : frame / t.PoseEnd), ref p);
            }
            else if (frame < t.Commit)
            {
                p.Stage = TellStage.Hold;
                Peak(t, 1f, ref p);
                p.Tremble = t.Tremble;
                if (t.ChargeGlow) p.Charge = t.HoldFrames <= 0 ? 1f : (frame - t.PoseEnd) / t.HoldFrames;
            }
            else if (frame < strikeStart)
            {
                p.Stage = TellStage.Commit;
                if (frame < releaseFrom) { Peak(t, 1f, ref p); p.Tremble = t.Tremble * 0.5f; }
                else Blend(t, Smooth((frame - releaseFrom) / Math.Max(1f, strikeStart - releaseFrom)), ref p);
                if (t.ChargeGlow) p.Charge = 1f;
            }
            else if (frame < strikeEnd)
            {
                p.Stage = TellStage.Strike;
                Strike(t, ref p);
            }
            else
            {
                p.Stage = TellStage.Recover;
                float k = Smooth(Math.Min(1f, (frame - strikeEnd) / Math.Max(1f, Math.Min(RecoverFrames, a.TotalFrames - strikeEnd))));
                Strike(t, ref p);
                p.Lean *= 1f - k; p.Squash = 1f + (p.Squash - 1f) * (1f - k); p.Rise *= 1f - k; p.Twist *= 1f - k;
            }
            p.Glint = frame >= t.Commit ? Math.Max(0f, 1f - (frame - t.Commit) / GlintFrames) : 0f;
            return p;
        }

        private static void Peak(EnemyTell t, float k, ref TellPose p)
        {
            p.Lean = t.LeanDeg * k;
            p.Squash = 1f - t.Crouch * k;
            p.Rise = t.Rise * k;
            p.Twist = t.TwistDeg * k;
        }

        private static void Strike(EnemyTell t, ref TellPose p)
        {
            p.Lean = t.LeanDeg * StrikeLean;
            p.Squash = 1f + StrikeStretch * (t.Crouch > 0f || t.Rise > 0f ? 1f : 0.5f);
            p.Rise = 0f;
            p.Twist = t.TwistDeg * StrikeTwist;
        }

        // From the held peak (k = 0) to the strike pose (k = 1).
        private static void Blend(EnemyTell t, float k, ref TellPose p)
        {
            TellPose a = default, b = default;
            Peak(t, 1f, ref a);
            Strike(t, ref b);
            p.Lean = a.Lean + (b.Lean - a.Lean) * k;
            p.Squash = a.Squash + (b.Squash - a.Squash) * k;
            p.Rise = a.Rise + (b.Rise - a.Rise) * k;
            p.Twist = a.Twist + (b.Twist - a.Twist) * k;
        }

        private static float Smooth(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }
    }
}
