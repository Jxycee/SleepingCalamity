// Catches: the revenge cap not scaling with the party (four players stunlocking the elite, or it breaking free nonstop), rage counting wrong or never
// ending, co-op life and enemy-count scaling drifting from P23, the downed/revive timers (20 s, 3 s hold, all down ends), boss parts that heal twice.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class EnemyRulesTests
    {
        // ---- revenge ----

        [TestCase(1, 12f)]
        [TestCase(2, 18f)]
        [TestCase(3, 24f)]
        [TestCase(4, 30f)]
        public void Revenge_CapScalesWithPlayers(int players, float cap)
        {
            Assert.That(RevengeGauge.CapFor(players), Is.EqualTo(cap));
            Assert.That(new RevengeGauge(players).Cap, Is.EqualTo(cap));
        }

        [Test]
        public void Revenge_FillsOnTheSummedHitsOfEveryPlayer_ThenEmpties()
        {
            var g = new RevengeGauge(2);   // cap 18
            bool broke = false;
            // opener 1, finisher 3, charge 4 repeating (two players alternating): running sum 1, 4, 8, 9, 12, 16, 17, 20 -> the 8th hit crosses 18
            int hits = 0;
            while (!broke && hits < 100)
            {
                hits++;
                broke = g.Add(hits % 3 == 0 ? RevengeGauge.Charge : hits % 3 == 1 ? RevengeGauge.Opener : RevengeGauge.Finisher);
            }
            Assert.That(broke, Is.True);
            Assert.That(g.Value, Is.EqualTo(0f), "the gauge emptied when the enemy broke free");
            Assert.That(hits, Is.EqualTo(8));
            Assert.That(new RevengeGauge(1).Add(20f), Is.True, "the same hits break a solo gauge (cap 12) much sooner");
        }

        [Test]
        public void Revenge_OneHitNeverBreaksAFreshGauge()
        {
            var g = new RevengeGauge(1);
            Assert.That(g.Add(RevengeGauge.Charge), Is.False);
            Assert.That(g.Add(-5f), Is.False);
            Assert.That(g.Add(float.NaN), Is.False);
            Assert.That(g.Value, Is.EqualTo(4f));
        }

        [Test]
        public void Revenge_ShrinkingTheParty_ClampsTheValue()
        {
            var g = new RevengeGauge(4);
            g.Add(20f);
            g.SetPlayers(1);
            Assert.That(g.Cap, Is.EqualTo(12f));
            Assert.That(g.Value, Is.LessThanOrEqualTo(12f));
        }

        // ---- rage ----

        [Test]
        public void Rage_SeventhHit_Telegraphs_ThenRagesForFifteenSeconds()
        {
            var r = new RageTracker();
            for (int i = 0; i < 6; i++) Assert.That(r.OnHit(), Is.False);
            Assert.That(r.State, Is.EqualTo(RageState.Calm));
            Assert.That(r.OnHit(), Is.True, "the 7th hit starts the telegraph");
            Assert.That(r.State, Is.EqualTo(RageState.Telegraphing));
            Assert.That(r.AttackMultiplier, Is.EqualTo(1f), "no bonus yet while it telegraphs");
            r.Tick(RageTracker.TelegraphSeconds + 0.01f);
            Assert.That(r.State, Is.EqualTo(RageState.Raging));
            Assert.That(r.AttackMultiplier, Is.EqualTo(1.3f).Within(1e-5f));
            r.Tick(14.9f);
            Assert.That(r.State, Is.EqualTo(RageState.Raging));
            r.Tick(0.2f);
            Assert.That(r.State, Is.EqualTo(RageState.Calm));
            Assert.That(r.Hits, Is.EqualTo(0), "the count starts over");
        }

        [Test]
        public void Rage_HitsWhileRaging_DoNotStackTheNextOne()
        {
            var r = new RageTracker();
            for (int i = 0; i < 7; i++) r.OnHit();
            r.Tick(1f);
            for (int i = 0; i < 20; i++) Assert.That(r.OnHit(), Is.False);
            r.Tick(16f);
            Assert.That(r.Hits, Is.EqualTo(0));
        }

        // ---- co-op scaling ----

        [TestCase(1, 1.0f)]
        [TestCase(2, 1.7f)]
        [TestCase(3, 2.3f)]
        [TestCase(4, 2.8f)]
        public void Scaling_LifeMultiplier(int players, float mult)
        {
            Assert.That(CoopScaling.LifeMultiplier(players), Is.EqualTo(mult));
        }

        [Test]
        public void Scaling_OneExtraEnemyPerExtraPlayer_AttackUnchanged()
        {
            Assert.That(CoopScaling.EnemyCount(4, 1), Is.EqualTo(4));
            Assert.That(CoopScaling.EnemyCount(4, 2), Is.EqualTo(5));
            Assert.That(CoopScaling.EnemyCount(4, 4), Is.EqualTo(7));
            Assert.That(CoopScaling.LifeMultiplier(0), Is.EqualTo(1f), "clamped to one player");
            Assert.That(CoopScaling.LifeMultiplier(9), Is.EqualTo(2.8f), "clamped to four");
            var solo = new EnemyModel(EnemyCatalog.RatKing, 1, 1);
            var four = new EnemyModel(EnemyCatalog.RatKing, 1, 4);
            Assert.That(four.MaxHp, Is.EqualTo(504), "180 x 2.8");
            Assert.That(four.Atk, Is.EqualTo(solo.Atk), "P23: co-op scales life, not attack");
        }

        // ---- downed and revive ----

        [Test]
        public void Downed_AtZeroHp_RevivesAfterAThreeSecondHold()
        {
            var v = new PlayerVitals();
            Assert.That(v.Damage(60, out bool d1), Is.EqualTo(60));
            Assert.That(d1, Is.False);
            Assert.That(v.Damage(60, out bool d2), Is.EqualTo(40), "only the life that was left");
            Assert.That(d2, Is.True);
            Assert.That(v.State, Is.EqualTo(VitalState.Downed));
            Assert.That(v.DownedLeft, Is.EqualTo(20f));

            for (int i = 0; i < 2 * 60; i++) Assert.That(v.Tick(1f / 60f, true), Is.False);
            Assert.That(v.ReviveProgress, Is.EqualTo(2f).Within(0.05f));
            bool revived = false;
            for (int i = 0; i < 70 && !revived; i++) revived = v.Tick(1f / 60f, true);
            Assert.That(revived, Is.True, "3 s of holding revives");
            Assert.That(v.State, Is.EqualTo(VitalState.Up));
            Assert.That(v.Hp, Is.EqualTo(40), "40% of 100");
        }

        [Test]
        public void Downed_ReleasingTheHold_ResetsTheProgress()
        {
            var v = new PlayerVitals();
            v.Damage(100, out _);
            for (int i = 0; i < 120; i++) v.Tick(1f / 60f, true);
            v.Tick(1f / 60f, false);
            Assert.That(v.ReviveProgress, Is.EqualTo(0f));
            Assert.That(v.State, Is.EqualTo(VitalState.Downed));
        }

        [Test]
        public void Downed_TwentySecondsWithoutHelp_IsOut()
        {
            var v = new PlayerVitals();
            v.Damage(100, out _);
            for (int i = 0; i < 19 * 60; i++) v.Tick(1f / 60f, false);
            Assert.That(v.State, Is.EqualTo(VitalState.Downed));
            for (int i = 0; i < 2 * 60; i++) v.Tick(1f / 60f, false);
            Assert.That(v.State, Is.EqualTo(VitalState.Out));
            Assert.That(v.Damage(5, out _), Is.EqualTo(0), "an out player takes nothing");
            Assert.That(v.Heal(50), Is.EqualTo(0));
        }

        [Test]
        public void AllPlayersDown_EndsTheDive()
        {
            var a = new PlayerVitals();
            var b = new PlayerVitals();
            Assert.That(PlayerVitals.AllDown(new[] { a, b }), Is.False);
            a.Damage(100, out _);
            Assert.That(PlayerVitals.AllDown(new[] { a, b }), Is.False, "one up is enough");
            b.Damage(100, out _);
            Assert.That(PlayerVitals.AllDown(new[] { a, b }), Is.True);
            Assert.That(PlayerVitals.AllDown(new List<PlayerVitals>()), Is.False, "an empty party has not lost");
            a.Revive();
            Assert.That(PlayerVitals.AllDown(new[] { a, b }), Is.False);
        }

        [Test]
        public void Heal_StopsAtMaxHp()
        {
            var v = new PlayerVitals();
            v.Damage(30, out _);
            Assert.That(v.Heal(100), Is.EqualTo(30));
            Assert.That(v.Hp, Is.EqualTo(100));
        }

        // ---- boss parts ----

        [Test]
        public void BossPart_BreaksOnce_AndStopsBeingWeak()
        {
            var p = new BossPart("pin", 50, 1.5f);
            Assert.That(p.BodyMultiplier, Is.EqualTo(1.5f));
            Assert.That(p.Damage(30), Is.False);
            Assert.That(p.Damage(30), Is.True, "the hit that empties it breaks it");
            Assert.That(p.Broken, Is.True);
            Assert.That(p.Damage(30), Is.False, "never breaks twice (no second healing drop)");
            Assert.That(p.BodyMultiplier, Is.EqualTo(1f));
        }
    }
}
