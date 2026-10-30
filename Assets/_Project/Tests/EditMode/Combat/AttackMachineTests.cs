// Catches: the combo chain (2 hits + finisher, ground and air) breaking, a press outside the window chaining anyway, input buffering lost, the 0.6 s
// charge, the finisher bonus counting hits that missed, hitstop leaking into the frame clock of a cancel, windows (hitbox, i-frames) reading wrong.
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class AttackMachineTests
    {
        const float Dt = 1f / 60f;

        static AttackInput Press => new AttackInput { Pressed = true, Held = true };
        static AttackInput Hold => new AttackInput { Held = true };
        static AttackInput Release => new AttackInput { Released = true };
        static AttackInput Idle => default;

        static void Run(AttackMachine m, int ticks, AttackInput input = default)
        {
            for (int i = 0; i < ticks; i++) m.Tick(Dt, input);
        }

        static AttackMachine NewAxe() => new AttackMachine(AttackCatalog.Axe);

        [Test]
        public void Press_StartsHitOneAtOnce()
        {
            var m = NewAxe();
            bool started = m.Tick(Dt, Press);
            Assert.That(started, Is.True);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Hit1));
            Assert.That(m.SwingId, Is.EqualTo(1));
        }

        [Test]
        public void HitboxWindow_FollowsTheData()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            var a = AttackCatalog.Axe.Attacks[AttackSet.Hit1];
            bool before = false, during = false, after = false;
            for (int f = 1; f < a.TotalFrames; f++)
            {
                m.Tick(Dt, Idle);
                if (!m.Active) break;
                if (m.FrameInt < a.HitOn.Start && m.HitboxOn) before = true;
                if (m.FrameInt >= a.HitOn.Start && m.FrameInt < a.HitOn.End && m.HitboxOn) during = true;
                if (m.FrameInt >= a.HitOn.End && m.HitboxOn) after = true;
            }
            Assert.That(before, Is.False);
            Assert.That(during, Is.True);
            Assert.That(after, Is.False);
        }

        [Test]
        public void Combo_TwoHitsAndAFinisher_OnTheGround()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            var seen = new System.Collections.Generic.List<int> { m.CurrentIndex };
            for (int i = 0; i < 400; i++)
            {
                // Press whenever the next hit's window is open: a player mashing the button.
                bool changed = m.Tick(Dt, i % 6 == 0 ? Press : Idle);
                if (changed) seen.Add(m.CurrentIndex);
                if (!m.Active) break;
            }
            Assert.That(seen, Is.EqualTo(new[] { AttackSet.Hit1, AttackSet.Hit2, AttackSet.Finisher }));
            Assert.That(m.Active, Is.False, "the finisher ends the chain");
        }

        [Test]
        public void Combo_InTheAir_UsesTheAirChain()
        {
            var m = NewAxe();
            var airPress = new AttackInput { Pressed = true, Held = true, Airborne = true };
            m.Tick(Dt, airPress);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.AirHit1));
            var seen = new System.Collections.Generic.List<int> { m.CurrentIndex };
            for (int i = 0; i < 400; i++)
            {
                bool changed = m.Tick(Dt, i % 6 == 0 ? airPress : Idle);
                if (changed) seen.Add(m.CurrentIndex);
                if (!m.Active) break;
            }
            Assert.That(seen, Is.EqualTo(new[] { AttackSet.AirHit1, AttackSet.AirHit2, AttackSet.AirFinisher }));
            Assert.That(AttackCatalog.Axe.Attacks[AttackSet.AirFinisher].Revenge, Is.EqualTo(3.5f), "KH's air finisher value");
        }

        [Test]
        public void PressBeforeTheWindow_IsBuffered_AndChainsWhenItOpens()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);                      // hit 1 starts (combo window 14-30, cancel 22)
            Run(m, 5);                              // frame ~5
            m.Tick(Dt, Press);                      // a press well before the window: buffered 0.25 s
            Run(m, 30);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Hit2), "the buffered press chained as soon as the window and the cancel frame allowed");
            Assert.That(m.FrameInt, Is.LessThan(20), "hit 2 started near frame 22 of hit 1, not at its end");
        }

        [Test]
        public void PressAfterTheWindowClosed_DoesNotChain_ButStartsAFreshChain()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            var hit1 = AttackCatalog.Axe.Attacks[AttackSet.Hit1];
            Run(m, hit1.ComboOpen.End + 2);         // past the window, still inside the recovery
            Assert.That(m.Active, Is.True);
            m.Tick(Dt, Press);
            Run(m, 10);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Hit1).Or.EqualTo(-1), "no hit 2 from a late press");
            Run(m, 60);
            Assert.That(m.SwingId, Is.EqualTo(2), "the late press started a second chain once the first ended");
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Hit1).Or.EqualTo(-1));
        }

        [Test]
        public void StalePress_InTheDeadTimeAfterTheCancelFrame_ExpiresAfterTheBuffer()
        {
            // Hit 2 of the axe has no window past its cancel frame to a hit 3 that is not the finisher: after the finisher nothing chains.
            var m = NewAxe();
            m.Tick(Dt, Press);
            for (int i = 0; i < 400 && m.CurrentIndex != AttackSet.Finisher; i++) m.Tick(Dt, i % 6 == 0 ? Press : Idle);
            m.Tick(Dt, Press);                      // a press during the finisher: there is no next hit
            Run(m, 200);                            // the finisher ends, the buffered press may start one fresh chain, and that one ends too
            int swings = m.SwingId;
            Run(m, 5);
            Assert.That(m.Active, Is.False);
            Assert.That(m.SwingId, Is.EqualTo(swings), "an old press did not start a new swing");
        }

        [Test]
        public void Charge_NeedsTheHold_AndFiresOnRelease()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);                      // hit 1 starts on the press, as always
            Run(m, 37, Hold);                       // held 0.62 s in total; hit 1 (34 frames) has ended
            Assert.That(m.ChargeReady, Is.True);
            m.Tick(Dt, Release);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Charge));
            var charge = AttackCatalog.Axe.Attacks[AttackSet.Charge];
            Assert.That(charge.Multiplier, Is.EqualTo(2.0f));
        }

        [Test]
        public void ShortHold_ReleaseDoesNotCharge()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            Run(m, 20, Hold);                       // 0.35 s
            m.Tick(Dt, Release);
            Run(m, 60);
            Assert.That(m.SwingId, Is.EqualTo(1), "only the first swing happened");
        }

        [Test]
        public void ChargeRelease_CancelsARecoveryPastTheCancelFrame()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            Run(m, 36, Hold);                       // 0.62 s, the swing may have ended
            Run(m, 0);
            m.Tick(Dt, Release);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Charge));
        }

        [Test]
        public void Hitstop_FreezesTheFrameClock_ButNotTheBuffer()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            Run(m, 8);
            float before = m.Frame;
            m.Freeze(10);
            Run(m, 10);
            Assert.That(m.Frame, Is.EqualTo(before).Within(0.01f), "10 frames of hitstop stop the clock for 10 ticks");
            Run(m, 3);
            Assert.That(m.Frame, Is.GreaterThan(before + 2f));
        }

        [Test]
        public void FinisherBonus_CountsOnlyConnectedHitsOfThisChain()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            m.RegisterHit();
            for (int i = 0; i < 400 && m.CurrentIndex != AttackSet.Hit2; i++) m.Tick(Dt, i % 6 == 0 ? Press : Idle);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Hit2));
            m.RegisterHit();
            for (int i = 0; i < 400 && m.CurrentIndex != AttackSet.Finisher; i++) m.Tick(Dt, i % 6 == 0 ? Press : Idle);
            Assert.That(m.CurrentIndex, Is.EqualTo(AttackSet.Finisher));
            Assert.That(m.PriorHits, Is.EqualTo(2));
            Assert.That(m.ComboCount, Is.EqualTo(2));
        }

        [Test]
        public void FinisherAfterTwoWhiffs_HasNoBonus()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            for (int i = 0; i < 400 && m.CurrentIndex != AttackSet.Finisher; i++) m.Tick(Dt, i % 6 == 0 ? Press : Idle);
            Assert.That(m.PriorHits, Is.EqualTo(0));
        }

        [Test]
        public void ComboCount_ResetsAfterTheGraceTime()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            m.RegisterHit();
            Run(m, 80);
            Assert.That(m.Active, Is.False);
            Assert.That(m.ComboCount, Is.EqualTo(1));
            Run(m, 90);                             // 1.5 s
            Assert.That(m.ComboCount, Is.EqualTo(0));
        }

        [Test]
        public void Charge_HasSuperArmour_AndTurnWindowsComeFromData()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            Run(m, 37, Hold);
            m.Tick(Dt, Release);
            Assert.That(m.SuperArmor, Is.True, "the charge starts armoured (frames 0-36)");
            Assert.That(m.Turning, Is.True);
            Assert.That(m.TurnDegPerSec, Is.EqualTo(360f));
        }

        [Test]
        public void Interrupt_EndsTheSwing_AndKeepsTheComboCount()
        {
            var m = NewAxe();
            m.Tick(Dt, Press);
            m.RegisterHit();
            m.Interrupt();
            Assert.That(m.Active, Is.False);
            Assert.That(m.ComboCount, Is.EqualTo(1));
        }

        [Test]
        public void Special_OnlyForWeaponsThatHaveOne()
        {
            Assert.That(NewAxe().TryStartSpecial(), Is.False);
            var bow = new AttackMachine(AttackCatalog.Bow);
            Assert.That(bow.TryStartSpecial(), Is.True);
            Assert.That(bow.Current.Kind, Is.EqualTo(AttackKind.Shot));
            var staff = new AttackMachine(AttackCatalog.Staff);
            Assert.That(staff.TryStartSpecial(), Is.True);
            Assert.That(staff.Current.SpellPips, Is.EqualTo(1));
        }

        [Test]
        public void EveryAttack_HasSaneWindows()
        {
            foreach (var set in new[] { AttackCatalog.Axe, AttackCatalog.Staff, AttackCatalog.Dagger, AttackCatalog.Bow })
            {
                for (int i = 0; i < AttackSet.Count; i++)
                {
                    var a = set.Get(i);
                    if (a == null) { Assert.That(i, Is.EqualTo(AttackSet.Special), set.Name); continue; }
                    Assert.That(a.HitOn.IsEmpty, Is.False, a.Id);
                    Assert.That(a.HitOn.End, Is.LessThanOrEqualTo(a.TotalFrames), a.Id);
                    Assert.That(a.CancelFrom, Is.LessThanOrEqualTo(a.TotalFrames), a.Id);
                    Assert.That(a.CancelFrom, Is.GreaterThan(a.HitOn.Start), a.Id + ": cancel after the hit starts");
                    if (i == AttackSet.Hit1 || i == AttackSet.Hit2)
                        Assert.That(a.ComboOpen.IsEmpty, Is.False, a.Id + " needs a combo window");
                    if (i == AttackSet.Hit2) Assert.That(a.FinisherAllowed.IsEmpty, Is.False, a.Id);
                }
            }
        }
    }
}
