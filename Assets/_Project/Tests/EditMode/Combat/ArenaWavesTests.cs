// Catches: the arena waves dropping an enemy type or the elite, tier switching at the wrong wave, the co-op "one extra enemy per extra player" not applying.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class ArenaWavesTests
    {
        [Test]
        public void Waves1To4_CoverEveryEnemyAndTheElite_AtTierOne()
        {
            var kinds = Enumerable.Range(1, 4).SelectMany(w => ArenaWaves.Roster(w, 1)).Distinct().ToList();
            Assert.That(kinds, Is.EquivalentTo(new[] { EnemyKind.Slime, EnemyKind.RatKing, EnemyKind.Broom, EnemyKind.DoughWarden }));
            for (int w = 1; w <= 4; w++) Assert.That(ArenaWaves.TierOf(w), Is.EqualTo(1));
            Assert.That(ArenaWaves.Roster(4, 1).Count(k => k == EnemyKind.DoughWarden), Is.EqualTo(1));
        }

        [Test]
        public void TierOne_IsFewerTougherDuels()
        {
            // Combat depth (2026-10-30, "fewer, tougher enemies"): two enemies per tier-1 wave (each about twice the life), the Warden alone in wave 4.
            Assert.That(ArenaWaves.Roster(1, 1), Is.EqualTo(new[] { EnemyKind.Slime, EnemyKind.Slime }), "wave 1 is the slime onboarding");
            Assert.That(ArenaWaves.Roster(2, 1), Is.EquivalentTo(new[] { EnemyKind.RatKing, EnemyKind.Slime }));
            Assert.That(ArenaWaves.Roster(3, 1), Is.EquivalentTo(new[] { EnemyKind.Broom, EnemyKind.RatKing }));
            Assert.That(ArenaWaves.Roster(4, 1), Is.EqualTo(new[] { EnemyKind.DoughWarden }), "the Warden alone");
            for (int w = 1; w <= 4; w++) Assert.That(ArenaWaves.Roster(w, 1).Length, Is.LessThanOrEqualTo(2), "wave " + w);
        }

        [Test]
        public void TierTwo_MixesKinds_InEveryWave_AndKeepsTheEliteInItsFourth()
        {
            // Enemy polish (2026-10-30): tier 2 was waves 1-4 again at x3.5 attack; now every tier-2 wave mixes at least three kinds and the attack step is
            // x1.54 (EnemyModelTests), so later waves are harder through mix and pacing.
            Assert.That(ArenaWaves.TierOf(5), Is.EqualTo(2));
            // Combat depth: three enemies per tier-2 wave, at least two kinds.
            for (int w = 5; w <= 12; w++)
            {
                Assert.That(ArenaWaves.Roster(w, 1).Distinct().Count(), Is.GreaterThanOrEqualTo(2), "wave " + w + " mixes kinds");
                Assert.That(ArenaWaves.Roster(w, 1).Length, Is.LessThanOrEqualTo(3), "wave " + w + ": a few tough enemies, not a crowd");
            }
            for (int w = 5; w <= 7; w++) Assert.That(ArenaWaves.Roster(w, 1).Contains(EnemyKind.DoughWarden), Is.False, "wave " + w);
            Assert.That(ArenaWaves.Roster(8, 1).Count(k => k == EnemyKind.DoughWarden), Is.EqualTo(1));
            Assert.That(ArenaWaves.Roster(9, 1), Is.EqualTo(ArenaWaves.Roster(5, 1)), "tier 2 repeats its own four");
        }

        [Test]
        public void ExtraPlayers_AddOneEnemyEach_NeverAnotherElite()
        {
            Assert.That(ArenaWaves.Roster(2, 2).Length, Is.EqualTo(ArenaWaves.Roster(2, 1).Length + 1));
            Assert.That(ArenaWaves.Roster(2, 4).Length, Is.EqualTo(ArenaWaves.Roster(2, 1).Length + 3));
            Assert.That(ArenaWaves.Roster(4, 4).Count(k => k == EnemyKind.DoughWarden), Is.EqualTo(1));
            Assert.That(ArenaWaves.Roster(3, 4).Count(k => k == EnemyKind.Broom), Is.EqualTo(1 + 3), "the extras are the first most numerous non-elite kind");
            Assert.That(ArenaWaves.Roster(4, 4).Count(k => k == EnemyKind.Slime), Is.EqualTo(3), "the Warden alone gets slimes as extras");
        }
    }
}
