// Authority: none (pure EditMode tests). Plan 5 task 3: the valve hold and pump hold rules (ValveHold, PumpHold in Core/Cellars/FloorObjectiveState.cs).
// Catches: a valve that turns without a hold, keeps progress after reports stop, un-turns, or needs an order; a pump that moves before the start, resets when the party leaves,
// ignores the slow rule, spawns a third live wave or pushes the floor above the live-enemy cap.
// Does not catch: the host wiring, reach checks and enemy spawning (Task 4, PlayMode).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class FloorObjectiveStateTests
    {
        // Holds valve v for `seconds` of game time starting at `now`, reporting every ReportInterval; returns the time after.
        static float Hold(ValveHold h, int v, float now, float seconds, out bool turned)
        {
            turned = false;
            const float dt = 0.05f; float nextReport = now;
            for (float t = 0f; t < seconds - 1e-4f; t += dt)
            {
                if (now >= nextReport - 1e-4f) { h.Report(v, now); nextReport += FloorObjectiveTuning.ReportInterval; }
                if (h.Tick(now, dt)) turned = true;
                now += dt;
            }
            return now;
        }

        [Test]
        public void Valve_TurnsAfterHold()
        {
            var h = new ValveHold();
            Hold(h, 0, 0f, FloorObjectiveTuning.ValveHoldSeconds - 0.3f, out bool early);
            Assert.IsFalse(early); Assert.AreEqual(0, h.TurnedCount);
            Assert.Greater(h.Progress(0), 0.5f);
            Hold(h, 0, 5f, FloorObjectiveTuning.ValveHoldSeconds + 0.2f, out bool turned);
            Assert.IsTrue(turned); Assert.AreEqual(1, h.TurnedMask); Assert.AreEqual(1f, h.Progress(0));
        }

        [Test]
        public void Valve_ResetsWhenReportsStop()
        {
            var h = new ValveHold();
            float now = Hold(h, 1, 0f, FloorObjectiveTuning.ValveHoldSeconds - 0.4f, out _);
            Assert.Greater(h.Progress(1), 0f);
            h.Tick(now + FloorObjectiveTuning.ReportTimeout + 0.1f, 0.1f);   // no report for longer than the timeout
            Assert.AreEqual(0f, h.Progress(1));
            Hold(h, 1, now + 1f, FloorObjectiveTuning.ValveHoldSeconds - 0.4f, out bool turned);   // starts from zero again: not enough
            Assert.IsFalse(turned);
        }

        [Test]
        public void Valve_StaysTurned()
        {
            var h = new ValveHold();
            float now = Hold(h, 0, 0f, FloorObjectiveTuning.ValveHoldSeconds + 0.2f, out _);
            for (int i = 0; i < 100; i++) { h.Tick(now, 0.1f); now += 0.1f; }
            Assert.AreEqual(1, h.TurnedCount); Assert.AreEqual(1f, h.Progress(0));
        }

        [Test]
        public void Valves_AnyOrder_AllTurned()
        {
            foreach (int first in new[] { 0, 1 })
            {
                var h = new ValveHold();
                float now = Hold(h, first, 0f, FloorObjectiveTuning.ValveHoldSeconds + 0.2f, out _);
                Assert.IsFalse(h.AllTurned);
                Hold(h, 1 - first, now + 1f, FloorObjectiveTuning.ValveHoldSeconds + 0.2f, out _);
                Assert.IsTrue(h.AllTurned); Assert.AreEqual(3, h.TurnedMask);
            }
        }

        [Test]
        public void Valves_TwoPlayersAtOnce_BothTurn()
        {
            var h = new ValveHold(); float now = 0f;
            for (int i = 0; i < 60; i++) { h.Report(0, now); h.Report(1, now); h.Tick(now, 0.05f); now += 0.05f; }
            Assert.IsTrue(h.AllTurned);
            h.Reset();
            Assert.AreEqual(0, h.TurnedMask); Assert.AreEqual(0f, h.Progress(0));
        }

        [Test]
        public void Pump_NotStarted_NoProgress()
        {
            var p = new PumpHold();
            for (int i = 0; i < 100; i++) { PumpTick t = p.Tick(0.1f, true, false, 0, 0, 0); Assert.AreEqual(-1, t.StartWave); }
            Assert.IsFalse(p.Started); Assert.AreEqual(0f, p.Progress); Assert.AreEqual(0, p.WavesSpawned);
        }

        [Test]
        public void Pump_FillsIn75s_WithHeroInRing()
        {
            var p = new PumpHold(); p.Start();
            int done = 0;
            for (int i = 0; i < 750 + 20 && !p.Done; i++) if (p.Tick(0.1f, true, false, 0, 0, 0).JustDone) done++;
            Assert.IsTrue(p.Done); Assert.AreEqual(1, done); Assert.AreEqual(1f, p.Progress);
            for (int i = 0; i < 50; i++) Assert.IsFalse(p.Tick(0.1f, true, false, 0, 0, 0).JustDone);
            Assert.AreEqual(3, p.WavesSpawned);
        }

        [Test]
        public void Pump_PausesWithoutHero_NeverResets()
        {
            var p = new PumpHold(); p.Start();
            for (int i = 0; i < 300; i++) p.Tick(0.1f, true, false, 0, 0, 0);   // 30 s
            float at = p.Progress;
            Assert.AreEqual(30f / 75f, at, 0.01f);
            for (int i = 0; i < 300; i++) p.Tick(0.1f, false, false, 0, 0, 0);
            Assert.AreEqual(at, p.Progress);
            p.Tick(0.1f, true, false, 0, 0, 0);
            Assert.Greater(p.Progress, at);
        }

        [Test]
        public void Pump_HalfRate_WhenEnemyAtPump()
        {
            var fast = new PumpHold(); fast.Start();
            var slow = new PumpHold(); slow.Start();
            for (int i = 0; i < 100; i++) { fast.Tick(0.1f, true, false, 0, 0, 0); slow.Tick(0.1f, true, true, 0, 0, 0); }
            Assert.AreEqual(fast.Progress * FloorObjectiveTuning.PumpSlowFactor, slow.Progress, 0.001f);
        }

        [Test]
        public void Pump_WavesAtThresholds_AtMostTwoAlive()
        {
            var p = new PumpHold(); p.Start();
            Assert.AreEqual(0, p.Tick(0.1f, true, false, 0, 0, 0).StartWave);   // wave 0 at progress 0
            // wave 1 waits for 0.35, wave 0 still alive (one earlier alive: allowed)
            int w1 = -1;
            for (int i = 0; i < 400 && w1 < 0; i++) w1 = p.Tick(0.1f, true, false, 2, 0, 0).StartWave;
            Assert.AreEqual(1, w1); Assert.GreaterOrEqual(p.Progress, 0.35f); Assert.Less(p.Progress, 0.40f);
            // wave 2 at 0.70 with waves 0 and 1 both alive: it waits
            for (int i = 0; i < 400 && p.Progress < 0.72f; i++) Assert.AreEqual(-1, p.Tick(0.1f, true, false, 2, 2, 0).StartWave);
            Assert.GreaterOrEqual(p.Progress, 0.72f); Assert.AreEqual(2, p.WavesSpawned);
            for (int i = 0; i < 10; i++) Assert.AreEqual(-1, p.Tick(0.1f, true, false, 1, 2, 0).StartWave);
            Assert.AreEqual(2, p.Tick(0.1f, true, false, 0, 2, 0).StartWave, "wave 0 died: wave 2 starts");
            Assert.AreEqual(3, p.WavesSpawned);
            Assert.AreEqual(-1, p.Tick(0.1f, true, false, 0, 2, 2).StartWave, "no fourth wave");
        }

        [Test]
        public void PumpHold_WaveSizes_RespectLiveCap()
        {
            Assert.AreEqual(2, PumpHold.WaveSize(4, 6));
            Assert.AreEqual(0, PumpHold.WaveSize(4, GameMetrics.MaxLiveEnemies));
            Assert.AreEqual(0, PumpHold.WaveSize(4, GameMetrics.MaxLiveEnemies + 3));
            Assert.AreEqual(2, PumpHold.WaveSize(1, 0));
            Assert.AreEqual(5, PumpHold.WaveSize(4, 0));
            Assert.AreEqual(3, PumpHold.WaveSize(2, 0));
            for (int players = 1; players <= 4; players++)
                for (int live = 0; live <= 12; live++) Assert.LessOrEqual(live + PumpHold.WaveSize(players, live), System.Math.Max(live, GameMetrics.MaxLiveEnemies));
        }

        [Test]
        public void Stagger_SoloIsSlower()
        {
            Assert.AreEqual(1.875f, PumpHold.Stagger(1), 0.0001f);
            Assert.AreEqual(1.5f, PumpHold.Stagger(2), 0.0001f);
            Assert.AreEqual(1.5f, PumpHold.Stagger(4), 0.0001f);
            Assert.Greater(PumpHold.Stagger(1), PumpHold.Stagger(2));
        }
    }
}
