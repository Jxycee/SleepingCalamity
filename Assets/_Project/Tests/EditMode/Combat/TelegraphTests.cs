// Catches the playtest complaints "I can't see enemy attacks coming" and "they hit me from a distance":
//  - the telegraph footprint must be exactly the region the hit test reaches (so what you see is what hurts), including a lunge's landing spot;
//  - no enemy attack may reach much farther than the enemy looks (EnemyDefinition.VisualReach), and every wind-up must last long enough to react to.
using System;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class TelegraphTests
    {
        const float R = MovementMath.CapsuleRadius;

        static AttackData[] RealAttacks()
        {
            var list = new System.Collections.Generic.List<AttackData>();
            foreach (AttackData a in AttackCatalog.AllEnemyAttacks) if (a != AttackCatalog.TestNova) list.Add(a);
            return list.ToArray();
        }

        // Is a target at (distance d, angle theta from the attack's facing) hit by `a`, using the real hit test?
        static bool Hit(AttackData a, float d, float thetaDeg)
        {
            float t = thetaDeg * MathF.PI / 180f;
            // the attack sits at the origin facing +Z; the target is at (sin t, cos t) * d
            return HitGeometry.Hits(a, 0f, 0f, 0f, 0f, MathF.Sin(t) * d, 0f, MathF.Cos(t) * d, R, MovementMath.CapsuleHeight);
        }

        [Test]
        public void Footprint_IsExactlyTheRegionTheHitTestReaches()
        {
            foreach (AttackData a in RealAttacks())
            {
                for (float theta = -180f; theta <= 180f; theta += 7f)
                {
                    float edge = Telegraph.Radius(a, theta, R);
                    Assert.That(edge, Is.GreaterThan(0f), a.Id);
                    // just inside the edge is a hit, just outside is not (the 0.05 m "standing inside it" disc is the one place the hit test is more generous)
                    if (edge - 0.03f > R + Telegraph.InsideSlack + 0.01f)
                        Assert.That(Hit(a, edge - 0.03f, theta), Is.True, a.Id + " just inside the footprint at " + theta + " deg must be a hit");
                    if (edge > R + Telegraph.InsideSlack + 0.01f)
                        Assert.That(Hit(a, edge + 0.03f, theta), Is.False, a.Id + " just outside the footprint at " + theta + " deg must be safe");
                }
            }
        }

        [Test]
        public void Footprint_CoversEveryPointTheAttackCanHit()
        {
            foreach (AttackData a in RealAttacks())
                for (float d = 0.1f; d < a.Range + 1.5f; d += 0.1f)
                    for (float theta = -180f; theta <= 180f; theta += 3f)
                        if (Hit(a, d, theta))
                            Assert.That(d, Is.LessThanOrEqualTo(Telegraph.Radius(a, theta, R) + 0.06f), a.Id + " hits at d " + d + " theta " + theta + " outside the drawn footprint");
        }

        [Test]
        public void Footprint_IsNotMuchBiggerThanTheHitArea()
        {
            foreach (AttackData a in RealAttacks())
            {
                Assert.That(Telegraph.Radius(a, 0f, R), Is.EqualTo(a.Range + R).Within(1e-4f), a.Id + ": straight ahead the footprint reaches Range + the hero's radius");
                Assert.That(Telegraph.MaxHalfAngleDeg(a, R), Is.LessThanOrEqualTo(a.HalfAngleDeg + 30f + 1e-3f).Or.EqualTo(180f), a.Id + ": the cone is not widened by more than the body's slack");
            }
        }

        [Test]
        public void ALunge_IsDrawnAtItsLandingSpot_AndItsFootprintCoversEveryFrameOfTheStrike()
        {
            foreach (AttackData a in RealAttacks())
            {
                if (a.LungeMeters <= 0f) continue;
                Assert.That(EnemyAttackRules.LungeEndsBeforeStrike(a), Is.True, a.Id + ": the lunge must end before the hitbox opens, or the footprint around the landing spot lies");
                Assert.That(Telegraph.RemainingLunge(a, 0f), Is.EqualTo(a.LungeMeters).Within(1e-4f), a.Id + ": at the start the whole lunge is still to come");
                Assert.That(Telegraph.RemainingLunge(a, a.Lunge.End), Is.EqualTo(0f).Within(1e-4f), a.Id);
                float mid = (a.Lunge.Start + a.Lunge.End) * 0.5f;
                Assert.That(Telegraph.RemainingLunge(a, mid), Is.EqualTo(a.LungeMeters * 0.5f).Within(1e-3f), a.Id);

                // Simulate: the enemy starts at the origin facing +Z, the footprint is drawn around (0, LungeMeters) from the first frame; a target is hit at some
                // frame of the hit window. Every such hit must be inside the footprint drawn around the landing spot.
                for (int frame = a.HitOn.Start; frame < a.HitOn.End; frame++)
                {
                    float travelled = a.LungeMeters * Math.Min(1f, Math.Max(0f, (frame - a.Lunge.Start) / (float)(a.Lunge.End - a.Lunge.Start)));
                    for (float x = -6f; x <= 6f; x += 0.25f)
                        for (float z = -6f; z <= 9f; z += 0.25f)
                        {
                            bool hit = HitGeometry.Hits(a, 0f, 0f, travelled, 0f, x, 0f, z, R, MovementMath.CapsuleHeight);
                            if (!hit) continue;
                            float dx = x, dz = z - a.LungeMeters;
                            float d = MathF.Sqrt(dx * dx + dz * dz);
                            float theta = MathF.Atan2(dx, dz) * 180f / MathF.PI;
                            Assert.That(d, Is.LessThanOrEqualTo(Telegraph.Radius(a, theta, R) + 0.06f), a.Id + " frame " + frame + ": a hit at (" + x + "," + z + ") is outside the footprint at the landing spot");
                        }
                }
            }
        }

        [Test]
        public void EveryEnemyAttack_HasAWindUpYouCanReactTo()
        {
            foreach (AttackData a in RealAttacks())
                Assert.That(a.HitOn.Start, Is.GreaterThanOrEqualTo(EnemyAttackRules.MinWindUpFrames), a.Id + ": the hitbox opens after at least " + EnemyAttackRules.MinWindUpFrames + " frames (0.4 s)");
        }

        [Test]
        public void NoEnemyAttack_ReachesFarPastWhatTheEnemyLooksLike()
        {
            foreach (EnemyDefinition def in EnemyCatalog.All)
                foreach (AttackData a in def.Attacks)
                    Assert.That(EnemyAttackRules.ReachMatchesLook(a, def), Is.True,
                        def.Name + " / " + a.Id + ": range " + a.Range + " m against a body that reaches " + def.VisualReach + " m (+" + EnemyAttackRules.ReachSlack + ")");
        }

        [Test]
        public void TheLobIsBoundedAndTheAiReachesNoFartherThanTheData()
        {
            Assert.That(SleepingCalamity.Gameplay.Enemy.LobReach, Is.LessThanOrEqualTo(6.5f));
            Assert.That(AttackCatalog.SlimeGlob.Origin, Is.EqualTo(HitOrigin.GroundAtTarget));
            Assert.That(AttackCatalog.SlimeGlob.Range, Is.LessThanOrEqualTo(1.3f + 1e-4f), "the glob's splash is small");
        }

        [Test]
        public void Fill_RisesFromZeroToOneAtTheStrikeFrame()
        {
            AttackData a = AttackCatalog.RatLunge;
            Assert.That(Telegraph.Fill(a, 0f), Is.EqualTo(0f));
            Assert.That(Telegraph.Fill(a, a.HitOn.Start * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(Telegraph.Fill(a, a.HitOn.Start), Is.EqualTo(1f));
            Assert.That(Telegraph.Fill(a, a.HitOn.End), Is.EqualTo(1f));
            Assert.That(Telegraph.Visible(a, a.HitOn.End + Telegraph.AfterglowFrames - 1), Is.True);
            Assert.That(Telegraph.Visible(a, a.HitOn.End + Telegraph.AfterglowFrames), Is.False, "hidden once the strike has read");
        }
    }
}
