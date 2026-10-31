// Core/Combat/Telegraph.cs: the ground footprint an enemy attack shows during its wind-up, computed from the same AttackData the hit test uses
// (docs/design/m3-combat-greybox.md). Pure C#. Authority: none (cosmetic; every peer derives it from the replicated attack).
//
// What you see is where your CENTRE must not be: the hit test (HitGeometry) reaches a target whose nearest point is inside the range, so the footprint
// is the range grown by the target's radius, and a cone's edge is widened by the angle the target's body covers at that distance.
// A lunging attack strikes where the lunge ends (its window must end before the hit window opens, EnemyAttackRules), so the footprint is drawn around
// that landing spot and slides with the enemy's remaining lunge.
using System;

namespace SleepingCalamity.Core.Combat
{
    public static class Telegraph
    {
        const float RadToDeg = 180f / MathF.PI;
        public const float InsideSlack = 0.05f;   // HitGeometry.InCone: "standing inside it" counts within the radius + 0.05

        // Distance from the footprint's centre to its edge in the direction thetaDeg (0 = straight ahead, + to either side), for a target of targetRadius.
        // A point at (distance d, angle theta) is hit exactly when d <= Radius(theta) (up to the 0.05 m inside slack), the test in TelegraphTests.
        public static float Radius(AttackData a, float thetaDeg, float targetRadius)
        {
            float reach = a.Range + targetRadius;
            if (a.Shape == HitShape.Circle) return reach;
            float off = Math.Abs(thetaDeg) - a.HalfAngleDeg;   // degrees outside the nominal cone
            if (off <= 0f) return reach;
            // The edge sits where half + asin(r / d) = |theta|, i.e. d = r / sin(off). At 90 degrees beyond the cone's edge that is r itself;
            // past that only the "standing inside it" disc around the centre remains.
            float inside = targetRadius + InsideSlack;
            if (off >= 90f) return inside;
            float sin = MathF.Sin(off / RadToDeg);
            float d = sin <= 1e-6f ? reach : targetRadius / sin;
            return Math.Min(reach, Math.Max(inside, d));
        }

        // The largest angle (either side) the footprint covers: the cone's half angle plus what the target's body adds at the far edge.
        public static float MaxHalfAngleDeg(AttackData a, float targetRadius)
        {
            if (a.Shape == HitShape.Circle) return 180f;
            float reach = a.Range + targetRadius;
            float widen = reach <= targetRadius ? 90f : MathF.Asin(Math.Min(1f, targetRadius / reach)) * RadToDeg;
            return Math.Min(180f, a.HalfAngleDeg + widen);
        }

        // Metres of lunge still to come at `frame`: the footprint's centre is the enemy's position plus this far along its facing.
        // A lob (ground target) and an attack without a lunge have none.
        public static float RemainingLunge(AttackData a, float frame)
        {
            if (a.LungeMeters <= 0f || a.Lunge.IsEmpty || a.Origin == HitOrigin.GroundAtTarget) return 0f;
            float span = a.Lunge.End - a.Lunge.Start;
            float progress = span <= 0f ? 1f : Math.Min(1f, Math.Max(0f, (frame - a.Lunge.Start) / span));
            return a.LungeMeters * (1f - progress);
        }

        // 0..1 through the wind-up (the footprint fills as this rises); 1 from the strike frame on.
        public static float Fill(AttackData a, float frame) => a.HitOn.Start <= 0 ? 1f : Math.Min(1f, Math.Max(0f, frame / a.HitOn.Start));

        // Frames the footprint stays after the hitbox closes (a short afterglow so the strike reads), then it is hidden.
        public const int AfterglowFrames = 8;
        public static bool Visible(AttackData a, float frame) => frame < a.HitOn.End + AfterglowFrames;
    }

    // The data rules that keep enemy attacks readable, asserted by EnemyAttackRulesTests so a tuning edit cannot silently break them.
    public static class EnemyAttackRules
    {
        // An attack may reach this far past what its enemy visibly extends (hero contact slack plus the splash of a landing); see EnemyDefinition.VisualReach.
        public const float ReachSlack = 0.6f;

        // Frames the player gets to see before the hitbox opens (60 Hz), at least: 27 (0.45 s) for small enemies, 54 (0.9 s) for the elite's own attacks,
        // 36 for its revenge break-free (research recommendation 4; DC1's early median is 0.65 s, KH's first enemies 0.67-0.7 s).
        public const int MinWindUpFrames = 27, EliteWindUpFrames = 54, BreakFreeWindUpFrames = 36;

        // Frames after the hitbox closes plus the AI's recover time, at least: the punish window (research recommendation 6: >= 0.5 s).
        public const int MinPunishFrames = 30;

        public static int WindUpFloor(AttackData a, EnemyDefinition def) =>
            a == AttackCatalog.WardenBreakFree ? BreakFreeWindUpFrames : def != null && def.IsElite ? EliteWindUpFrames : MinWindUpFrames;

        public static int PunishFrames(AttackData a, EnemyKind kind, int tier) =>
            a.TotalFrames - a.HitOn.End + (int)(AttackPacing.RecoverSeconds(kind, tier) * AttackData.FrameRate);

        // The body's tell is complete and honest: the pose peaks, holds, then glints 12-24 frames before the hitbox, and the enemy no longer turns or
        // starts its lunge before the glint (so where it faces at the glint is where it strikes). Null for a valid tell, else what is wrong.
        public static string TellProblem(AttackData a)
        {
            EnemyTell t = a.Tell;
            if (t == null) return "no tell";
            if (t.PoseEnd <= 0 || t.PoseEnd > t.Commit) return "pose must peak before the commit";
            if (t.HoldFrames < 4) return "the hold before the glint is shorter than 4 frames";
            int lead = a.HitOn.Start - t.Commit;
            if (lead < TellMath.MinReactFrames || lead > TellMath.MaxReactFrames) return "glint leads the hitbox by " + lead + " frames (12-24)";
            if (!a.Turn.IsEmpty && a.Turn.End > t.Commit) return "still turning after the glint (turn ends " + a.Turn.End + ")";
            if (a.LungeMeters > 0f && a.Lunge.Start < t.Commit) return "the lunge starts before the glint";
            return null;
        }

        // The lunge must finish before the hitbox opens, so the footprint drawn around the landing spot is the whole truth.
        public static bool LungeEndsBeforeStrike(AttackData a) => a.LungeMeters <= 0f || a.Lunge.End <= a.HitOn.Start;

        public static bool ReachMatchesLook(AttackData a, EnemyDefinition def) => a.Origin == HitOrigin.GroundAtTarget || a.Range <= def.VisualReach + ReachSlack + 1e-4f;
    }
}
