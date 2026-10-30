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
        public void TheFirstTwoWaves_AreAnOnboarding()
        {
            // First playtest: wave 1 was four slimes and wave 2 two slimes + two rat-kings; "very hard to progress". Now 2 slimes, then 3 slimes + 1 rat-king.
            Assert.That(ArenaWaves.Roster(1, 1), Is.EqualTo(new[] { EnemyKind.Slime, EnemyKind.Slime }));
            Assert.That(ArenaWaves.Roster(2, 1).Count(k => k == EnemyKind.Slime), Is.EqualTo(3));
            Assert.That(ArenaWaves.Roster(2, 1).Count(k => k == EnemyKind.RatKing), Is.EqualTo(1));
            Assert.That(ArenaWaves.Roster(2, 1).Length, Is.EqualTo(4));
            Assert.That(ArenaWaves.Roster(1, 1).Any(k => k != EnemyKind.Slime), Is.False, "wave 1 is slimes only");
        }

        [Test]
        public void WavesThreeAndFour_AreUnchanged()
        {
            Assert.That(ArenaWaves.Roster(3, 1).Count(k => k == EnemyKind.Broom), Is.EqualTo(2));
            Assert.That(ArenaWaves.Roster(3, 1).Count(k => k == EnemyKind.RatKing), Is.EqualTo(2));
            Assert.That(ArenaWaves.Roster(4, 1).Count(k => k == EnemyKind.DoughWarden), Is.EqualTo(1));
            Assert.That(ArenaWaves.Roster(4, 1).Count(k => k == EnemyKind.Slime), Is.EqualTo(2));
        }

        [Test]
        public void WaveFive_StartsTierTwo_AndRepeatsTheCompositions()
        {
            Assert.That(ArenaWaves.TierOf(5), Is.EqualTo(2));
            Assert.That(ArenaWaves.Roster(5, 1), Is.EqualTo(ArenaWaves.Roster(1, 1)));
            Assert.That(ArenaWaves.Roster(8, 1), Is.EqualTo(ArenaWaves.Roster(4, 1)));
        }

        [Test]
        public void ExtraPlayers_AddOneEnemyEach_NeverAnotherElite()
        {
            Assert.That(ArenaWaves.Roster(2, 2).Length, Is.EqualTo(ArenaWaves.Roster(2, 1).Length + 1));
            Assert.That(ArenaWaves.Roster(2, 4).Length, Is.EqualTo(ArenaWaves.Roster(2, 1).Length + 3));
            Assert.That(ArenaWaves.Roster(4, 4).Count(k => k == EnemyKind.DoughWarden), Is.EqualTo(1));
            Assert.That(ArenaWaves.Roster(4, 4).Count(k => k == EnemyKind.Slime), Is.EqualTo(2 + 3), "the extras are the most numerous non-elite kind");
        }
    }
}
