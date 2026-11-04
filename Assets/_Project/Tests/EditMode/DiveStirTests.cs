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
    }
}
