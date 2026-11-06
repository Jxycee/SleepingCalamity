// Assets/_Project/Tests/EditMode/DiveStirTests.cs: tests for SleepingCalamity.Core.DiveStir (spec decision 19, section 4.5, red-team F2).
using System;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveStirTests
    {
        [Test]
        public void Value_FloorsAndValuables_TimesTheSquareRootOfPlayers()
        {
            var s = new DiveStir();
            s.OnFloorCleared(1);
            s.OnFloorCleared(2);
            s.UnbankedValuables = 2;
            Assert.That(s.Value, Is.EqualTo(30f).Within(0.001f));
            s.PlayersPresent = 4;
            Assert.That(s.Value, Is.EqualTo(60f).Within(0.001f), "x2 for 4 players, not x1.75");
            Assert.That(s.OnFloorCleared(2), Is.False, "a floor counts once");
        }

        [Test]
        public void Latches_StayOnForTheHalf()
        {
            var s = new DiveStir();
            s.UnbankedValuables = 8;
            Assert.That(s.ExtraEnemyLatched, Is.True);
            Assert.That(s.HunterLatched, Is.False);
            s.UnbankedValuables = 0;
            Assert.That(s.ExtraEnemyLatched, Is.True, "latched even after banking items away");
        }

        [Test]
        public void TheDeepCloses_AimsAtThisFloorsStair_OncePerHalf()
        {
            Assert.That(DiveStir.CollapseSeconds(20f), Is.EqualTo(60f), "never under 60 s");
            Assert.That(DiveStir.CollapseSeconds(80f), Is.EqualTo(120f));

            var s = new DiveStir();
            s.UnbankedValuables = 20;
            Assert.That(s.CollapsePending, Is.True);
            s.BeginCollapse(80f);
            Assert.That(s.CollapseRunning, Is.True);
            Assert.That(s.CollapseSecondsLeft, Is.EqualTo(120f));
            s.OnStairTaken();
            Assert.That(s.CollapseRunning, Is.False);
            Assert.That(s.CollapsePending, Is.False, "escaped once: no second collapse this half");
        }

        [Test]
        public void CollapseFactor_ScalesTheMinimumToo_AndOneIsUnchanged()
        {
            float hurried = FloorModifierTuning.HurriedCollapseMultiplier / DiveStir.CollapseMultiplier;
            Assert.That(DiveStir.CollapseSeconds(20f, 1f), Is.EqualTo(DiveStir.CollapseSeconds(20f)));
            Assert.That(DiveStir.CollapseSeconds(80f, 1f), Is.EqualTo(120f));
            Assert.That(DiveStir.CollapseSeconds(20f, hurried), Is.LessThanOrEqualTo(50f), "a short path: the 60 s minimum is scaled too");
            Assert.That(DiveStir.CollapseSeconds(80f, hurried), Is.EqualTo(100f).Within(0.01f), "1.25x of 80");

            var plain = new DiveStir { UnbankedValuables = 20 }; plain.BeginCollapse(20f);
            var quick = new DiveStir { UnbankedValuables = 20 }; quick.BeginCollapse(20f, hurried);
            Assert.That(plain.CollapseSecondsLeft, Is.EqualTo(60f));
            Assert.That(quick.CollapseSecondsLeft, Is.LessThanOrEqualTo(50f));
        }

        [Test]
        public void Tick_FailsWhenTheCountdownRunsOut()
        {
            var s = new DiveStir();
            s.UnbankedValuables = 20;
            s.BeginCollapse(10f);
            Assert.That(s.Tick(59f), Is.False);
            Assert.That(s.Tick(1.5f), Is.True);
            Assert.That(s.CollapseRunning, Is.False);
        }

        [Test]
        public void PushingOn_ResetsTheDanger_ButCarriesTheReward()
        {
            var s = new DiveStir();
            for (int f = 1; f <= 5; f++) s.OnFloorCleared(f);
            s.UnbankedValuables = 4;
            Assert.That(s.Value, Is.EqualTo(70f).Within(0.001f));
            Assert.That(s.RewardRankBonus, Is.EqualTo(2));
            s.UnbankedValuables = 0;   // banked at the bank room
            s.OnPushOn();
            Assert.That(s.Value, Is.EqualTo(0f));
            Assert.That(s.HunterLatched, Is.False);
            Assert.That(s.RewardValue, Is.EqualTo(50f).Within(0.001f), "the reward side starts where the first half left it (value when pushing on)");
            s.OnFloorCleared(6);
            Assert.That(s.RewardValue, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void ResetForNewDive_ClearsEverything()
        {
            var s = new DiveStir();
            s.OnFloorCleared(1);
            s.OnPushOn();
            s.ResetForNewDive();
            Assert.That(s.RewardValue, Is.EqualTo(0f));
            Assert.That(s.Value, Is.EqualTo(0f));
        }

        [Test]
        public void PushOn_WithValuablesCarried_DoesNotRelatch()
        {
            var s = new DiveStir();
            s.PlayersPresent = 4;
            s.UnbankedValuables = 10;
            s.OnPushOn();
            Assert.That(s.Value, Is.EqualTo(0f));
            Assert.That(s.ExtraEnemyLatched, Is.False);
            Assert.That(s.HunterLatched, Is.False);
            s.UnbankedValuables = 12;
            Assert.That(s.Value, Is.EqualTo(2 * 5 * 2f).Within(0.001f), "only the 2 above the baseline stir the second half");
        }

        [Test]
        public void PushOn_RewardCountsCarriedValuablesOnce()
        {
            var s = new DiveStir();
            s.OnFloorCleared(1);
            s.OnFloorCleared(2);
            s.UnbankedValuables = 4;
            Assert.That(s.RewardValue, Is.EqualTo(40f).Within(0.001f));
            s.OnPushOn();
            Assert.That(s.RewardValue, Is.EqualTo(40f).Within(0.001f), "not 80");
        }

        [Test]
        public void OnFloorCleared_RespectsTheHalf()
        {
            var s = new DiveStir();
            Assert.That(s.OnFloorCleared(6), Is.False, "second-half floor before push-on");
            s.OnPushOn();
            Assert.That(s.OnFloorCleared(5), Is.False, "first-half floor after push-on");
            Assert.That(s.OnFloorCleared(6), Is.True);
        }

        [Test]
        public void ResetForNewDive_ClearsTheHalfAndBaseline()
        {
            var s = new DiveStir();
            s.UnbankedValuables = 6;
            s.OnPushOn();
            s.ResetForNewDive();
            Assert.That(s.OnFloorCleared(1), Is.True);
            s.UnbankedValuables = 3;
            Assert.That(s.Value, Is.EqualTo(25f).Within(0.001f), "baseline is gone");
        }

        // The model cases of the retired StirMeterTests that the tests above do not cover (plan 3 task 12).
        [Test]
        public void Value_ClampsTo100_AndNeverGoesBelowOnePlayer()
        {
            var s = new DiveStir();
            s.PlayersPresent = 0;
            Assert.That(s.PlayersPresent, Is.EqualTo(1), "never below one player");
            s.PlayersPresent = 4;
            for (int f = 1; f <= 5; f++) s.OnFloorCleared(f);
            s.UnbankedValuables = 30;
            Assert.That(s.Value, Is.EqualTo(DiveStir.Max), "(50 + 150) x 2 clamps to 100");
            s.UnbankedValuables = -3;
            Assert.That(s.UnbankedValuables, Is.EqualTo(0), "never below zero valuables");
        }

        [Test]
        public void Latches_StayOnWhenThePartyShrinks_AndClearAtPushOn()
        {
            var s = new DiveStir();
            s.PlayersPresent = 4;
            s.OnFloorCleared(1); s.OnFloorCleared(2); s.OnFloorCleared(3); s.OnFloorCleared(4);   // 40 x 2 = 80
            Assert.That(s.HunterLatched, Is.True);
            s.PlayersPresent = 1;                                                                  // 40
            Assert.That(s.Value, Is.LessThan(DiveStir.HunterThreshold));
            Assert.That(s.ExtraEnemyLatched && s.HunterLatched, Is.True, "latched for the half");
            s.OnPushOn();
            Assert.That(s.ExtraEnemyLatched || s.HunterLatched, Is.False, "the second half starts clean");
            s.ResetForNewDive();
            Assert.That(s.ExtraEnemyLatched || s.HunterLatched || s.CollapseRunning || s.CollapsePending, Is.False);
            Assert.That(s.Value, Is.EqualTo(0f));
        }

        [Test]
        public void Collapse_StoppedByTheStair_NeverFailsTheDive_AndAnExpiredOneTicksFalseAfterwards()
        {
            var s = new DiveStir();
            s.UnbankedValuables = 20;
            s.BeginCollapse(80f);
            Assert.That(s.Tick(30f), Is.False);
            Assert.That(s.CollapseSecondsLeft, Is.EqualTo(90f).Within(0.001f));
            s.OnStairTaken();
            Assert.That(s.Tick(500f), Is.False, "a stopped countdown never fails the dive");
            Assert.That(s.ExtraEnemyLatched && s.HunterLatched, Is.True, "the latches stay");
            var e = new DiveStir { UnbankedValuables = 20 };
            e.BeginCollapse(10f);
            Assert.That(e.Tick(60f), Is.True);
            Assert.That(e.Tick(10f), Is.False, "the failure is reported once");
            Assert.That(e.CollapseSecondsLeft, Is.EqualTo(0f));
        }

        [Test]
        public void Numbers_ArePinned()
        {
            Assert.That(new[] { DiveStir.PerFloorCleared, DiveStir.PerUnbankedValuable, DiveStir.Max, DiveStir.ExtraEnemyThreshold, DiveStir.HunterThreshold, DiveStir.DeepClosesThreshold },
                Is.EqualTo(new[] { 10f, 5f, 100f, 40f, 70f, 100f }));
            Assert.That(new[] { DiveStir.CollapseMultiplier, DiveStir.CollapseMinSeconds, DiveStir.RewardRankStep }, Is.EqualTo(new[] { 1.5f, 60f, 35f }));
        }

        [Test]
        public void Deterministic_TwoIdenticalRunsAgree()
        {
            float Run() { var s = new DiveStir { PlayersPresent = 3 }; s.OnFloorCleared(2); s.UnbankedValuables = 4; s.BeginCollapse(5f); s.Tick(5f); return s.Value + s.RewardValue + s.CollapseSecondsLeft; }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void CaptureRestore_RoundTrips()
        {
            var s = new DiveStir();
            s.PlayersPresent = 2;
            for (int f = 1; f <= 5; f++) s.OnFloorCleared(f);
            s.UnbankedValuables = 6;
            s.OnPushOn();
            s.UnbankedValuables = 20;
            s.OnFloorCleared(6);
            s.BeginCollapse(100f);
            s.Tick(10f);
            DiveStir.State st = s.Capture();

            var r = new DiveStir();
            r.PlayersPresent = 2;
            r.Restore(st);
            Assert.That(r.CollapseRunning, Is.True);
            Assert.That(r.CollapseSecondsLeft, Is.EqualTo(s.CollapseSecondsLeft).Within(0.001f));
            Assert.That(r.Value, Is.EqualTo(s.Value).Within(0.001f));
            Assert.That(r.RewardValue, Is.EqualTo(s.RewardValue).Within(0.001f));
            Assert.That(r.ExtraEnemyLatched, Is.EqualTo(s.ExtraEnemyLatched));
            Assert.That(r.HunterLatched, Is.EqualTo(s.HunterLatched));
            Assert.That(r.CollapsePending, Is.EqualTo(s.CollapsePending));
            Assert.That(r.OnFloorCleared(5), Is.False, "the half is restored");
            Assert.That(r.Capture(), Is.EqualTo(st));
        }
    }
}
