// Catches: the anti-mash rules (docs/design/m3-combat-greybox.md, "Proposal: enemies that answer the mash") drifting: light hits stunlocking enemies
// again, poise not clearing or not scaling with the party, a flinch loop with no steadiness, a committed enemy flinching, openings not paying x1.5,
// rage counting good play, the broom's guard never answering, the riposte breaking the tell rules or shifting replicated indexes, the parry lockout and
// the perfect dodge's tight, unchainable window.
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class CombatDepthTests
    {
        static EnemyHitInput Hit(AttackData a, float atk = 5f, float bearing = 180f, bool committed = false, bool open = false, bool staggered = false) => new EnemyHitInput
        {
            Attack = a, Atk = atk, BearingToAttackerDeg = bearing, PartIndex = -1, CritRoll = 0.9f, CritChance = 0.05f,
            EnemyCommitted = committed, EnemyOpen = open, EnemyStaggered = staggered,
        };

        static AttackData Axe(int i) => AttackCatalog.Axe.Attacks[i];
        static AttackData Dagger(int i) => AttackCatalog.Dagger.Attacks[i];
        static AttackData Staff(int i) => AttackCatalog.Staff.Attacks[i];

        // ---- poise ----

        [Test]
        public void LightHits_NoLongerFlinchEveryEnemy()
        {
            var rat = new EnemyModel(EnemyCatalog.RatKing);
            Assert.That(rat.ApplyHit(Hit(Dagger(AttackSet.Hit1))).Staggered, Is.False, "dagger 0.5 of 4");
            Assert.That(rat.ApplyHit(Hit(Dagger(AttackSet.Hit2))).Staggered, Is.False, "1 of 4");
            Assert.That(rat.ApplyHit(Hit(Dagger(AttackSet.Finisher))).Staggered, Is.False, "3 of 4: a whole dagger chain does not flinch a rat-king");
            Assert.That(rat.ApplyHit(Hit(Dagger(AttackSet.Hit1))).Staggered, Is.False, "3.5");
            EnemyHitResult r = rat.ApplyHit(Hit(Dagger(AttackSet.Hit2)));
            Assert.That(r.Staggered && r.PoiseBroken, Is.True, "4 of 4: the second chain does");
            Assert.That(r.Knockback, Is.GreaterThan(0f));
        }

        [Test]
        public void AxePair_FlinchesARatKing_AndTheCountClearsAfterASecond()
        {
            var rat = new EnemyModel(EnemyCatalog.RatKing);
            Assert.That(rat.ApplyHit(Hit(Axe(AttackSet.Hit1))).Staggered, Is.False);
            Assert.That(rat.ApplyHit(Hit(Axe(AttackSet.Hit2))).Staggered, Is.True, "axe 2 + 2 = the threshold 4");
            var slow = new EnemyModel(EnemyCatalog.RatKing);
            slow.ApplyHit(Hit(Axe(AttackSet.Hit1)));
            slow.Tick(PoiseMeter.ResetSeconds + 0.01f);
            Assert.That(slow.Poise.Value, Is.EqualTo(0f), "DC2: the count clears a second after the last hit");
            Assert.That(slow.ApplyHit(Hit(Axe(AttackSet.Hit2))).Staggered, Is.False);
        }

        [Test]
        public void NoKnockback_UnlessItFlinches()
        {
            var rat = new EnemyModel(EnemyCatalog.RatKing);
            EnemyHitResult r = rat.ApplyHit(Hit(Axe(AttackSet.Hit1)));
            Assert.That(r.Staggered, Is.False);
            Assert.That(r.Knockback, Is.EqualTo(0f), "a hit that does not flinch does not push it out of its attack either");
        }

        [Test]
        public void AfterAFlinch_ItIsSteady_SoFlinchLoopsCannotStunlock()
        {
            var slime = new EnemyModel(EnemyCatalog.Slime);
            Assert.That(slime.ApplyHit(Hit(Axe(AttackSet.Hit1))).Staggered, Is.True);
            slime.Poise.BeginSteady();   // the host calls this when the flinch ends
            Assert.That(slime.Poise.Steady, Is.True);
            for (int i = 0; i < 4; i++) Assert.That(slime.ApplyHit(Hit(Axe(AttackSet.Hit1))).Staggered, Is.False, "steady hit " + i);
            slime.Tick(PoiseMeter.SteadySeconds + 0.01f);
            Assert.That(slime.ApplyHit(Hit(Axe(AttackSet.Hit1))).Staggered, Is.True, "steadiness wears off");
        }

        [Test]
        public void AlreadyFlinching_ItCannotBeFlinchedAgain()
        {
            var slime = new EnemyModel(EnemyCatalog.Slime);
            EnemyHitResult r = slime.ApplyHit(Hit(Axe(AttackSet.Hit1), staggered: true));
            Assert.That(r.Staggered, Is.False, "no stagger extension while staggered");
            Assert.That(slime.Poise.Value, Is.EqualTo(0f));
        }

        [Test]
        public void PoiseThreshold_GrowsWithTheParty_LikeTheRevengeCap()
        {
            Assert.That(PoiseMeter.ThresholdFor(4f, 1), Is.EqualTo(4f));
            Assert.That(PoiseMeter.ThresholdFor(4f, 2), Is.EqualTo(6f));
            Assert.That(PoiseMeter.ThresholdFor(4f, 4), Is.EqualTo(10f));
            Assert.That(new EnemyModel(EnemyCatalog.RatKing, 1, 3).Poise.Threshold, Is.EqualTo(8f));
        }

        [Test]
        public void HeroPoise_IsWeaponData_HeavyOpensFaster()
        {
            Assert.That(Axe(AttackSet.Hit1).Poise, Is.EqualTo(2f));
            Assert.That(Axe(AttackSet.Charge).Poise, Is.EqualTo(6f));
            Assert.That(Dagger(AttackSet.Hit1).Poise, Is.EqualTo(0.5f));
            Assert.That(Staff(AttackSet.Special).Poise, Is.EqualTo(4f), "the spell flinches a rat-king in one");
            Assert.That(AttackCatalog.Bow.Attacks[AttackSet.Special].Poise, Is.EqualTo(2f), "the shot");
            Assert.That(Axe(AttackSet.AirHit1).Poise, Is.EqualTo(Axe(AttackSet.Hit1).Poise), "air attacks keep the ground poise");
            Assert.That(Axe(AttackSet.AirFinisher).Poise, Is.EqualTo(Axe(AttackSet.Finisher).Poise));
        }

        // ---- committed and open ----

        [Test]
        public void Committed_FromTheGlintToTheEndOfTheHitbox()
        {
            AttackData a = AttackCatalog.RatLunge;   // glint 14, hitbox 28-36
            Assert.That(EnemyWindows.Committed(a, a.Tell.Commit - 1), Is.False, "the hold can still be interrupted");
            Assert.That(EnemyWindows.Committed(a, a.Tell.Commit), Is.True);
            Assert.That(EnemyWindows.Committed(a, a.HitOn.End - 0.5f), Is.True);
            Assert.That(EnemyWindows.Committed(a, a.HitOn.End), Is.False);
            Assert.That(EnemyWindows.Spent(a, a.HitOn.End), Is.True, "the rest of the attack is the punish window");
            Assert.That(EnemyWindows.Committed(AttackCatalog.TestNova, 20f), Is.False, "no tell, never committed");
        }

        [Test]
        public void HittingACommittedEnemy_Trades_ItDoesNotFlinch()
        {
            var slime = new EnemyModel(EnemyCatalog.Slime);
            EnemyHitResult r = slime.ApplyHit(Hit(Axe(AttackSet.Charge), committed: true));
            Assert.That(r.Damage, Is.GreaterThan(0), "the hit still lands");
            Assert.That(r.Staggered, Is.False, "but the attack goes on");
            Assert.That(r.Knockback, Is.EqualTo(0f));
            Assert.That(slime.Poise.Value, Is.EqualTo(0f), "and adds no poise");
        }

        [Test]
        public void Openings_PayOneAndAHalf_AndDoublePoise()
        {
            var plain = new EnemyModel(EnemyCatalog.RatKing);
            var open = new EnemyModel(EnemyCatalog.RatKing);
            int p = plain.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 22f)).Damage;
            EnemyHitResult o = open.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 22f, open: true));
            Assert.That(p, Is.EqualTo(20), "22 x 100/110");
            Assert.That(o.Damage, Is.EqualTo(30), "x1.5");
            Assert.That(o.Opening, Is.True);
            Assert.That(o.Staggered, Is.True, "axe 2 x 2 = 4 fills a rat-king in one opening hit");
        }

        [Test]
        public void Rage_CountsOnlyHitsOutsideOpenings()
        {
            var good = new EnemyModel(EnemyCatalog.RatKing, 2, 1);
            bool raged = false;
            for (int i = 0; i < 12; i++) raged |= good.ApplyHit(Hit(Dagger(AttackSet.Hit1), open: true)).RageStarted;
            Assert.That(raged, Is.False, "punishing at the right moment never angers it");
            var mash = new EnemyModel(EnemyCatalog.RatKing, 2, 1);
            for (int i = 0; i < RageTracker.HitsToRage; i++) raged |= mash.ApplyHit(Hit(Dagger(AttackSet.Hit1))).RageStarted;
            Assert.That(raged, Is.True, "mashing does");
        }

        // ---- the broom's guard ----

        [Test]
        public void Broom_ThreeBlockedHitsInTwoSeconds_Riposte()
        {
            var broom = new EnemyModel(EnemyCatalog.Broom);
            EnemyHitResult a = broom.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 0f));
            Assert.That(a.Blocked && a.Braced, Is.True);
            Assert.That(broom.Poise.Value, Is.EqualTo(0f), "blocked front hits add no poise");
            Assert.That(broom.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 0f)).RiposteTriggered, Is.False);
            Assert.That(broom.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 0f)).RiposteTriggered, Is.True, "the third");
            Assert.That(broom.Guard.Count, Is.EqualTo(0), "the count starts again");

            var slow = new EnemyModel(EnemyCatalog.Broom);
            slow.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 0f));
            slow.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 0f));
            slow.Tick(GuardCounter.WindowSeconds + 0.01f);
            Assert.That(slow.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 0f)).RiposteTriggered, Is.False, "older blocks expired");
        }

        [Test]
        public void Broom_GuardBreakHit_BreaksItsGuard_AndFlankHitsBuildPoise()
        {
            var broom = new EnemyModel(EnemyCatalog.Broom);
            EnemyHitResult r = broom.ApplyHit(Hit(Axe(AttackSet.Finisher), bearing: 0f));
            Assert.That(r.GuardBroken && r.Staggered, Is.True);
            Assert.That(r.Braced, Is.False);
            var flank = new EnemyModel(EnemyCatalog.Broom);
            flank.ApplyHit(Hit(Axe(AttackSet.Hit1), bearing: 120f));
            Assert.That(flank.Poise.Value, Is.EqualTo(2f));
            Assert.That(flank.Guard.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnlyTheBroomRipostes()
        {
            var rat = new EnemyModel(EnemyCatalog.RatKing);
            for (int i = 0; i < 5; i++) Assert.That(rat.ApplyHit(Hit(Dagger(AttackSet.Hit1), bearing: 0f)).Braced, Is.False);
        }

        [Test]
        public void Riposte_KeepsTheTellRules_AndIsAppendedLast()
        {
            AttackData r = AttackCatalog.BroomRiposte;
            Assert.That(EnemyAttackRules.TellProblem(r), Is.Null);
            Assert.That(r.HitOn.Start, Is.GreaterThanOrEqualTo(EnemyAttackRules.MinWindUpFrames));
            Assert.That(EnemyAttackRules.PunishFrames(r, EnemyKind.Broom, 2), Is.GreaterThanOrEqualTo(EnemyAttackRules.MinPunishFrames));
            Assert.That(EnemyAttackRules.ReachMatchesLook(r, EnemyCatalog.Broom), Is.True);
            Assert.That(System.Array.IndexOf(AttackCatalog.AllEnemyAttacks, r), Is.EqualTo(AttackCatalog.AllEnemyAttacks.Length - 1),
                "appended last: replicated indexes of older attacks unchanged");
            Assert.That(System.Array.IndexOf(EnemyCatalog.Broom.Attacks, r), Is.EqualTo(-1), "it answers a hammered guard, the AI never rolls it");
        }

        // ---- parry lockout and perfect dodge ----

        [Test]
        public void Parry_OnlyAfterTheGuardWasDownLongEnough()
        {
            Assert.That(GuardMath.ParryArmed(GuardMath.ParryLockoutSeconds), Is.True);
            Assert.That(GuardMath.ParryArmed(0.2f), Is.False);
            Assert.That(GuardMath.Resolve(true, 0.02f, 0f, false, true, parryArmed: true), Is.EqualTo(GuardOutcome.Parried));
            Assert.That(GuardMath.Resolve(true, 0.02f, 0f, false, true, parryArmed: false), Is.EqualTo(GuardOutcome.Guarded), "a tapped re-raise still guards");
        }

        [Test]
        public void PerfectDodge_IsTheFirstThirdOfTheRoll_AndNeverChained()
        {
            Assert.That(DodgeTiming.IsPerfect(true, 0.05f, 1f), Is.True);
            Assert.That(DodgeTiming.IsPerfect(true, DodgeTiming.PerfectSeconds, 1f), Is.True);
            Assert.That(DodgeTiming.IsPerfect(true, 0.2f, 1f), Is.False, "still safe (DodgeTiming.IsInvulnerable) but not perfect");
            Assert.That(DodgeTiming.IsInvulnerable(true, 0.2f), Is.True);
            Assert.That(DodgeTiming.IsPerfect(true, 0.05f, 0.2f), Is.False, "chained onto the last roll");
            Assert.That(DodgeTiming.IsPerfect(false, 0.05f, 1f), Is.False);
            Assert.That(DodgeTiming.PerfectSeconds / (DodgeTiming.IFrameEndSeconds + DodgeTiming.LenienceSeconds), Is.LessThan(0.5f),
                "a much tighter window than the safe roll, so rolling repeatedly earns nothing");
        }

        // ---- fewer, tougher ----

        [Test]
        public void ARatKing_TakesAboutThreeAxeOpenings()
        {
            // An axe pair in an opening: (22 + 24.2) x 100/110 x 1.5 = 63. A rat-king's 180 life is three of those (plus chip hits).
            var rat = new EnemyModel(EnemyCatalog.RatKing);
            int openings = 0;
            while (rat.Alive && openings < 10)
            {
                rat.ApplyHit(Hit(Axe(AttackSet.Hit1), atk: 22f, open: true));
                rat.ApplyHit(Hit(Axe(AttackSet.Hit2), atk: 22f, open: true));
                openings++;
            }
            Assert.That(openings, Is.InRange(3, 4));
        }
    }
}
