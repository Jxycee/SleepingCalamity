// M5 stir meter (Core). Catches: wrong formula or player factor, missing clamp, checkpoint bank wiping the floor term, town bank not resetting, latches
// un-latching, chest loot raising stir, the deep-closes countdown surviving a bank or not failing the dive at 0, tuning numbers scattered outside StirTuning.
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class StirMeterTests
    {
        private static DiveState Dive(StirTuning? t = null, int[] pieces = null) =>
            new DiveState(new DiveHaul(pieces ?? new[] { 6, 6, 6 }), t ?? StirTuning.Default);

        private static void Pieces(DiveState d, int n, int floor = 1) { for (int i = 0; i < n; i++) Assert.That(d.Haul.AddPiece(DiveHaul.PieceId(floor, i)), Is.True); }

        // Per-player loot plan (spec 4.5): pieces are safe on pickup and no longer feed the meter; stir comes from floors cleared only. Valuables-driven stir is plan 3 (DiveStir).
        // With the default numbers four floors at four players reach 70, so the tests that need the deep-closes threshold use a stronger floor term.
        private static StirTuning Strong() { StirTuning t = StirTuning.Default; t.PerFloorCleared = 40f; return t; }

        [Test] public void Value_IsFloorsOnly_SoloAndWithPlayerFactor()
        {
            DiveState d = Dive();
            Assert.That(d.Stir.Value, Is.EqualTo(0f));
            d.MarkCleared(1);
            Assert.That(d.Stir.Value, Is.EqualTo(10f).Within(1e-4f), "10 x 1");
            d.Stir.PlayersPresent = 3;
            Assert.That(d.Stir.Value, Is.EqualTo(15f).Within(1e-4f), "x1.5 for three players");
            d.Stir.PlayersPresent = 0;
            Assert.That(d.Stir.PlayersPresent, Is.EqualTo(1), "never below one player");
        }

        [Test] public void Stir_ComesFromFloorsClearedOnly_PiecesNoLongerCount()
        {
            DiveState d = Dive();
            d.MarkCleared(1);
            float before = d.Stir.Value;
            Pieces(d, 6);
            Assert.That(d.Stir.Value, Is.EqualTo(before), "six pieces picked up add nothing");
            Assert.That(d.Stir.Value, Is.EqualTo(10f).Within(1e-4f));
            d.SetFloor(DiveState.CheckpointFloor); d.BankAtPoint();
            Assert.That(d.Stir.Value, Is.EqualTo(10f).Within(1e-4f), "a bank does not move it either");
        }

        [Test] public void Value_ClampsTo0And100()
        {
            DiveState d = Dive(Strong());
            d.Stir.PlayersPresent = 4;
            d.MarkCleared(1); d.MarkCleared(2); d.MarkCleared(3);
            Assert.That(d.Stir.Value, Is.EqualTo(100f));
        }

        [Test] public void FloorTerm_CountsEachFloorOncePerDive()
        {
            DiveState d = Dive();
            d.MarkCleared(1); d.MarkCleared(1);
            Assert.That(d.Stir.FloorsCleared, Is.EqualTo(1));
            Assert.That(d.Stir.Value, Is.EqualTo(10f));
        }

        [Test] public void CheckpointBank_KeepsFloorTerm()
        {
            DiveState d = Dive();
            d.MarkCleared(1); Pieces(d, 3);
            d.SetFloor(DiveState.CheckpointFloor);
            Assert.That(d.Stir.Value, Is.EqualTo(10f));
            Assert.That(d.BankAtPoint(), Is.True);
            Assert.That(d.Stir.Value, Is.EqualTo(10f));
        }

        [Test] public void TownBank_ResetsFloorTermAndLatches()
        {
            DiveState d = Dive();
            d.MarkCleared(1); d.MarkCleared(2); d.MarkCleared(3); d.MarkCleared(4); Pieces(d, 4);   // 40
            Assert.That(d.Stir.ExtraEnemyLatched, Is.True);
            d.SetFloor(DiveState.Town);
            Assert.That(d.BankInTown(), Is.True);
            Assert.That(d.Stir.Value, Is.EqualTo(0f));
            Assert.That(d.Stir.FloorsCleared, Is.EqualTo(0));
            Assert.That(d.Stir.ExtraEnemyLatched, Is.False);
            Assert.That(d.Stir.ExtraEnemiesPerRoom, Is.EqualTo(0));
        }

        [Test] public void Latches_StayOnWhenTheValueDrops_AndClearOnFail()
        {
            StirTuning t = StirTuning.Default; t.PerExtraPlayer = 1f;   // four players = x4, so two floors reach the hunter threshold and one player drops back under the first
            DiveState d = Dive(t);
            int hunters = 0; d.Stir.HunterReleased += () => hunters++;
            d.Stir.PlayersPresent = 4;
            d.MarkCleared(1); Pieces(d, 3);                      // 10 x 4 = 40
            Assert.That(d.Stir.ExtraEnemyLatched, Is.True);
            Assert.That(d.Stir.HunterLatched, Is.False);
            Assert.That(d.Stir.ExtraEnemiesPerRoom, Is.EqualTo(1));
            d.MarkCleared(2);                                    // 20 x 4 = 80
            Assert.That(d.Stir.HunterLatched, Is.True);
            Assert.That(hunters, Is.EqualTo(1));
            d.Stir.PlayersPresent = 1;                           // 20
            d.SetFloor(DiveState.CheckpointFloor); d.BankAtPoint();
            Assert.That(d.Stir.Value, Is.LessThan(40f));
            Assert.That(d.Stir.ExtraEnemyLatched && d.Stir.HunterLatched, Is.True, "latched for the dive");
            d.FailDive();
            Assert.That(d.Stir.ExtraEnemyLatched || d.Stir.HunterLatched || d.Stir.DeepClosesLatched, Is.False);
            Assert.That(d.Stir.Value, Is.EqualTo(0f));
        }

        [Test] public void Loot_NeverAddsStir()
        {
            DiveState d = Dive();
            d.Haul.AddLoot(1, 50);
            Assert.That(d.Stir.Value, Is.EqualTo(0f));
        }

        private static DiveState DeepClosed(StirTuning t)
        {
            t.PerFloorCleared = 40f;   // the default floor term cannot reach the deep-closes threshold without the piece term (spec 4.5)
            DiveState d = Dive(t);
            d.Stir.PlayersPresent = 4;
            d.MarkCleared(1); d.MarkCleared(2); d.MarkCleared(3); Pieces(d, 6);
            return d;
        }

        [Test] public void DeepCloses_StartsCountdown_AndABankStopsIt()
        {
            DiveState d = DeepClosed(StirTuning.Default);
            Assert.That(d.Stir.DeepClosesLatched, Is.True);
            Assert.That(d.Stir.DeepClosesRunning, Is.True);
            Assert.That(d.Stir.DeepClosesSecondsLeft, Is.EqualTo(90f));
            Assert.That(d.Tick(30f), Is.False);
            Assert.That(d.Stir.DeepClosesSecondsLeft, Is.EqualTo(60f).Within(1e-3f));
            d.SetFloor(DiveState.CheckpointFloor); d.BankAtPoint();
            Assert.That(d.Stir.DeepClosesRunning, Is.False);
            Assert.That(d.Tick(500f), Is.False, "stopped countdown never fails the dive");
            Assert.That(d.Stir.DeepClosesLatched, Is.True);
        }

        [Test] public void DeepCloses_AtZero_FailsTheDive_AndKeepsThePieces()
        {
            DiveState d = DeepClosed(StirTuning.Default);
            d.SetFloor(3);
            Assert.That(d.Tick(89f), Is.False);
            Assert.That(d.Tick(1f), Is.True);
            Assert.That(d.CurrentFloor, Is.EqualTo(DiveState.Town));
            Assert.That(d.Haul.BankedPieceCount, Is.EqualTo(6));
            Assert.That(d.Haul.PiecesLeft(1), Is.EqualTo(0), "pieces are safe on pickup: a failed dive keeps them");
            Assert.That(d.Stir.Value, Is.EqualTo(0f));
            Assert.That(d.Stir.DeepClosesRunning, Is.False);
            Assert.That(d.Tick(10f), Is.False);
        }

        [Test] public void Tuning_LivesInOnePlace_AndShortensTheCountdown()
        {
            StirTuning t = StirTuning.Default; t.DeepClosesSeconds = 2f; t.ExtraEnemiesPerRoom = 2;
            DiveState d = DeepClosed(t);
            Assert.That(d.Stir.ExtraEnemiesPerRoom, Is.EqualTo(2));
            Assert.That(d.Tick(2f), Is.True);
            StirTuning def = StirTuning.Default;
            Assert.That(new[] { def.PerFloorCleared, def.PerUnbankedPiece, def.PerExtraPlayer, def.ExtraEnemyThreshold, def.HunterThreshold, def.DeepClosesThreshold, def.DeepClosesSeconds },
                Is.EqualTo(new[] { 10f, 5f, 0.25f, 40f, 70f, 100f, 90f }));
        }

        [Test] public void Deterministic_TwoIdenticalRunsAgree()
        {
            float Run() { DiveState d = Dive(); d.Stir.PlayersPresent = 3; d.MarkCleared(2); Pieces(d, 4); d.Tick(5f); return d.Stir.Value; }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
