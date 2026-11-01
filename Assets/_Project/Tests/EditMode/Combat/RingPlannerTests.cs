// Enemy AI stage 4 (docs/research/enemy-ai.md 3, 10.5): RingPlanner shared slots, front arc half clear, determinism, strafe flip range, separation (no stacking
// with 6 enemies on one hero), no per-tick allocation, and the catalog data. Simulated movement mirrors AiBrain's hover move (toward the slot plus the push).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class RingPlannerTests
    {
        const float Dt = 0.05f;
        const float Pi = (float)Math.PI;

        static float Wrap(float a) { while (a > Pi) a -= 2f * Pi; while (a <= -Pi) a += 2f * Pi; return a; }

        sealed class Sim
        {
            public readonly RingPlanner Planner;
            public readonly int N;
            public readonly float[] X, Z;
            public readonly float Ring, Sep, Speed, FrontAngle;
            public double Now;
            public Sim(int n, ulong seed, float ring = 3.0f, float sep = 1.2f, float speed = 4.6f, float frontAngle = 0f, bool stacked = false)
            {
                Planner = new RingPlanner(seed);
                N = n; Ring = ring; Sep = sep; Speed = speed; FrontAngle = frontAngle;
                X = new float[n]; Z = new float[n];
                var rng = new Pcg32(seed ^ 0xABCD, 7);
                for (int i = 0; i < n; i++)
                {
                    // clustered in front of the hero (the worst case for the front arc) at about the ring radius
                    float ang = frontAngle + (stacked ? 0.02f * i : rng.Range(-0.4f, 0.4f));
                    float r = stacked ? ring : ring + rng.Range(-0.3f, 0.3f);
                    X[i] = (float)Math.Cos(ang) * r; Z[i] = (float)Math.Sin(ang) * r;
                }
            }

            public void Step()
            {
                Now += Dt;
                for (int i = 0; i < N; i++)
                {
                    var q = new RingRequest
                    {
                        SelfId = i + 1, HeroId = 100, HeroX = 0f, HeroZ = 0f, FrontX = (float)Math.Cos(FrontAngle), FrontZ = (float)Math.Sin(FrontAngle),
                        SelfX = X[i], SelfZ = Z[i], Ring = Ring, CircleSpeed = Speed * 0.45f, FlipMin = 1.5f, FlipMax = 3.0f, SeparationRadius = Sep,
                        InitialSign = 0f, Now = Now, Dt = Dt,
                    };
                    RingResult r = Planner.Plan(in q);
                    float tx = r.SlotX - X[i], tz = r.SlotZ - Z[i];
                    float td = (float)Math.Sqrt(tx * tx + tz * tz);
                    float mx = td > 0.05f ? tx / td : 0f, mz = td > 0.05f ? tz / td : 0f;
                    mx += r.SepX * Planner.Tuning.SeparationGain; mz += r.SepZ * Planner.Tuning.SeparationGain;
                    float mag = (float)Math.Sqrt(mx * mx + mz * mz);
                    if (mag < 1e-3f) continue;
                    float circle = Speed * 0.45f;
                    float spd = Math.Min(Speed, circle * (1f + td));
                    mx /= mag; mz /= mag;
                    if (!Planner.Slide(i + 1, Now, spd, Dt, ref mx, ref mz)) continue;
                    X[i] += mx * spd * Dt; Z[i] += mz * spd * Dt;
                }
            }

            public float MinPairDistance()
            {
                float min = float.MaxValue;
                for (int i = 0; i < N; i++)
                    for (int j = i + 1; j < N; j++)
                        min = Math.Min(min, (float)Math.Sqrt((X[i] - X[j]) * (X[i] - X[j]) + (Z[i] - Z[j]) * (Z[i] - Z[j])));
                return min;
            }

            public float MinAngleGap()
            {
                float min = float.MaxValue;
                for (int i = 1; i <= N; i++)
                    for (int j = i + 1; j <= N; j++)
                    {
                        Planner.TryGetAngle(i, out float a); Planner.TryGetAngle(j, out float b);
                        min = Math.Min(min, Math.Abs(Wrap(a - b)));
                    }
                return min;
            }
        }

        [Test]
        public void SixOnOneHero_SlotsStayApart_AtTheSharedGap()
        {
            float gap = 2f * Pi / 6f * RingTuning.Default.SlotGapFactor;
            foreach (ulong seed in new ulong[] { 11, 12, 13, 14 })
            {
                var s = new Sim(6, seed);
                for (int i = 0; i < 400; i++) s.Step();
                // the slots (not the bodies) are what the planner shares out; the earlier claimers hold theirs, so a final-tick reading is exact up to one tick of drift
                Assert.That(s.MinAngleGap(), Is.GreaterThanOrEqualTo(gap * 0.6f), "seed " + seed + ": slots closer than the shared gap");
            }
        }

        [Test]
        public void FrontArc_StaysHalfClear()
        {
            foreach (float front in new[] { 0f, 1.3f, -2.4f })
            {
                var s = new Sim(6, 23, frontAngle: front);
                for (int i = 0; i < 500; i++) s.Step();
                float half = s.Planner.Tuning.FrontHalfArcDeg * Pi / 180f;
                float gap = 2f * Pi / 6f * s.Planner.Tuning.SlotGapFactor;
                int capacity = (int)Math.Floor(2f * half / gap) + 1;
                int allowed = (int)Math.Floor(capacity * s.Planner.Tuning.FrontMaxFraction);
                int inArc = 0;
                for (int k = 1; k <= 6; k++)
                {
                    s.Planner.TryGetAngle(k, out float a);
                    if (Math.Abs(Wrap(a - front)) < half - 0.03f) inArc++;
                }
                Assert.That(inArc, Is.LessThanOrEqualTo(allowed), "front " + front + ": more than half the front arc is used");
            }
        }

        [Test]
        public void SameSeed_SamePlan_DifferentSeed_Differs()
        {
            var a = new Sim(6, 5); var b = new Sim(6, 5); var c = new Sim(6, 6);
            for (int i = 0; i < 200; i++) { a.Step(); b.Step(); c.Step(); }
            for (int k = 0; k < 6; k++) { Assert.That(a.X[k], Is.EqualTo(b.X[k])); Assert.That(a.Z[k], Is.EqualTo(b.Z[k])); }
            bool differs = false;
            for (int k = 0; k < 6; k++) if (a.X[k] != c.X[k]) differs = true;
            Assert.That(differs, Is.True, "another seed flips at other times");
        }

        [Test]
        public void StrafeFlip_FlipsWithinTheConfiguredRange()
        {
            var p = new RingPlanner(99);
            double now = 0, last = 0;
            float lastSign = 0f;
            var gaps = new List<double>();
            for (int i = 0; i < 4000; i++)
            {
                now += 0.02;
                var q = new RingRequest { SelfId = 1, HeroId = 1, SelfX = 3f, SelfZ = 0f, Ring = 3f, CircleSpeed = 2f, FlipMin = 1.5f, FlipMax = 3.0f, SeparationRadius = 1f, InitialSign = 1f, Now = now, Dt = 0.02f };
                RingResult r = p.Plan(in q);
                if (lastSign != 0f && r.Sign != lastSign) { gaps.Add(now - last); last = now; }
                else if (lastSign == 0f) last = now;
                lastSign = r.Sign;
            }
            Assert.That(gaps.Count, Is.GreaterThanOrEqualTo(25), "80 s at 1.5 to 3 s a flip");
            foreach (double g in gaps) Assert.That(g, Is.InRange(1.5 - 0.021, 3.0 + 0.021), "flip interval " + g);
        }

        [Test]
        public void SixEnemies_StartingStacked_NeverEndCloserThanTheSeparationRadius()
        {
            foreach (ulong seed in new ulong[] { 31, 32, 33, 34, 35, 36 })
            {
                var s = new Sim(6, seed, sep: 1.2f, stacked: true);
                float worstAfterSettling = float.MaxValue;
                for (int i = 0; i < 600; i++) { s.Step(); if (i >= 300) worstAfterSettling = Math.Min(worstAfterSettling, s.MinPairDistance()); }
                Assert.That(worstAfterSettling, Is.GreaterThanOrEqualTo(1.2f * 0.6f), "seed " + seed + ": two enemies stacked after settling");
                Assert.That(s.MinPairDistance(), Is.GreaterThanOrEqualTo(1.2f * 0.85f), "seed " + seed + ": final tick");
            }
        }

        [Test]
        public void ExactlyStacked_AreSplitApart()
        {
            var p = new RingPlanner(1);
            p.Observe(1, 5f, 5f, 1f, 1.0, out float ax, out float az);
            p.Observe(2, 5f, 5f, 1f, 1.0, out float bx, out float bz);
            p.Observe(1, 5f, 5f, 1f, 1.0, out ax, out az);
            Assert.That(ax * bx + az * bz, Is.LessThan(0f), "opposite pushes");
        }

        [Test]
        public void RoomClamp_PullsASlotOutsideTheRoomInTowardTheHero()
        {
            var p = new RingPlanner(1);
            var q = new RingRequest { SelfId = 1, HeroId = 1, SelfX = 2.5f, SelfZ = 0f, Ring = 5f, CircleSpeed = 1f, FlipMin = 1.5f, FlipMax = 3f, SeparationRadius = 1f, InitialSign = 1f, Now = 1.0, Dt = 0.05f,
                InBounds = (x, z) => x < 3.2f };
            RingResult r = p.Plan(in q);
            Assert.That(r.SlotX, Is.LessThan(3.2f));
            Assert.That(r.RingRadius, Is.LessThan(5f));
        }

        [Test]
        public void MoreThanOneRing_SpillsOutward()
        {
            var s = new Sim(8, 3);
            for (int i = 0; i < 300; i++) s.Step();
            var radii = new HashSet<int>();
            for (int k = 0; k < 8; k++) radii.Add((int)Math.Round(Math.Sqrt(s.X[k] * s.X[k] + s.Z[k] * s.Z[k]) / 0.8));
            Assert.That(radii.Count, Is.GreaterThanOrEqualTo(2), "the 7th and 8th stand on the next ring out");
        }

        [Test]
        public void Plan_DoesNotAllocate_PerTick()
        {
            var s = new Sim(6, 41);
            for (int i = 0; i < 100; i++) s.Step();   // warm up: members created, JIT done
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 400; i++) s.Step();
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.LessThanOrEqualTo(0L), "allocated bytes over 2400 plans");
        }

        [Test]
        public void Remove_FreesTheMember()
        {
            var s = new Sim(3, 2);
            s.Step();
            Assert.That(s.Planner.Count, Is.EqualTo(3));
            s.Planner.Remove(2);
            Assert.That(s.Planner.Count, Is.EqualTo(2));
            Assert.That(s.Planner.TryGetAngle(2, out _), Is.False);
        }

        [Test]
        public void Catalog_HasRingData_ForEveryEnemy()
        {
            foreach (EnemyDefinition d in EnemyCatalog.All)
            {
                Assert.That(d.PreferredRing, Is.GreaterThan(d.Radius), d.Id);
                Assert.That(d.CircleSpeedScale, Is.InRange(0.2f, 0.7f), d.Id);
                Assert.That(d.StrafeFlipMin, Is.GreaterThan(0f), d.Id);
                Assert.That(d.StrafeFlipMax, Is.GreaterThan(d.StrafeFlipMin), d.Id);
                Assert.That(d.SeparationRadius, Is.GreaterThanOrEqualTo(d.Radius * 2f), d.Id);
            }
            Assert.That(EnemyCatalog.Slime.Role, Is.EqualTo(EnemyRole.Ranged));
            Assert.That(EnemyCatalog.Slime.PreferredRing, Is.GreaterThan(EnemyCatalog.RatKing.PreferredRing), "ranged hover on the far ring");
        }
    }
}
