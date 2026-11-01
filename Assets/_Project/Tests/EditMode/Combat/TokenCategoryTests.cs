// Enemy AI stage 2 (docs/research/enemy-ai.md 2.3 and the 10.5 rows "Token categories", "Token swap", "Token leak", and the property test):
// per-category pools, the exclusive Heavy signature, swapping an unstarted token to a nearer enemy, leak protection, and random crowds that
// never break the pool caps or the gap between hit windows.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class TokenCategoryTests
    {
        const int Hero = 100, Hero2 = 101;
        // One per category, no gap: only the pool sizes can refuse.
        static readonly PacingProfile NoGap = new PacingProfile(1, 0f, 1f, 1f, 0.3f);

        [Test]
        public void EnemyData_RatIsMelee_SlimeIsRanged_BroomMelee_WardenHeavy()
        {
            Assert.That(EnemyCatalog.RatKing.TokenCategory, Is.EqualTo(TokenCategory.Melee));
            Assert.That(EnemyCatalog.Slime.TokenCategory, Is.EqualTo(TokenCategory.Ranged));
            Assert.That(EnemyCatalog.Broom.TokenCategory, Is.EqualTo(TokenCategory.Melee));
            Assert.That(EnemyCatalog.DoughWarden.TokenCategory, Is.EqualTo(TokenCategory.Heavy));
        }

        [Test]
        public void PoolSize_MeleeAndRangedFollowTheTier_HeavyIsOne()
        {
            Assert.That(AttackPacing.Tier1.PoolSize(TokenCategory.Melee), Is.EqualTo(1));
            Assert.That(AttackPacing.Tier1.PoolSize(TokenCategory.Ranged), Is.EqualTo(1));
            Assert.That(AttackPacing.Tier2.PoolSize(TokenCategory.Melee), Is.EqualTo(2));
            Assert.That(AttackPacing.Tier2.PoolSize(TokenCategory.Ranged), Is.EqualTo(2));
            Assert.That(AttackPacing.Tier2.PoolSize(TokenCategory.Heavy), Is.EqualTo(1));
        }

        [Test]
        public void TokenCategories_AFullMeleePoolDoesNotBlockRanged_ButEachPoolStillCaps()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap, category: TokenCategory.Melee), Is.True);
            Assert.That(t.TryAcquire(Hero, 2, 0.0, AttackCatalog.RatLunge, NoGap, category: TokenCategory.Melee), Is.False, "melee pool of one is full");
            Assert.That(t.TryAcquire(Hero, 3, 0.0, AttackCatalog.SlimeGlob, NoGap, category: TokenCategory.Ranged), Is.True, "ranged has its own pool");
            Assert.That(t.TryAcquire(Hero, 4, 0.0, AttackCatalog.SlimeGlob, NoGap, category: TokenCategory.Ranged), Is.False, "ranged pool of one is full");
            Assert.That(t.HoldersOn(Hero, TokenCategory.Melee, 0.0), Is.EqualTo(1));
            Assert.That(t.HoldersOn(Hero, TokenCategory.Ranged, 0.0), Is.EqualTo(1));
            Assert.That(t.HoldersOn(Hero, 0.0), Is.EqualTo(2));
        }

        [Test]
        public void TokenCategories_TheGapStillSeparatesHitWindowsAcrossCategories()
        {
            var t = new AttackTokens();
            // rat lunge hits 0.47-0.60 s, slime glob hits 0.77-0.87 s: 0.17 s apart, inside the tier-1 gap of 0.5 s
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, AttackPacing.Tier1, category: TokenCategory.Melee), Is.True);
            Assert.That(t.TryAcquire(Hero, 2, 0.0, AttackCatalog.SlimeGlob, AttackPacing.Tier1, category: TokenCategory.Ranged), Is.False);
            // started 0.45 s later, the glob lands 0.77 + 0.45 - 0.60 = 0.62 s after the lunge: both attacks are then under way together
            Assert.That(t.TryAcquire(Hero, 2, 0.45, AttackCatalog.SlimeGlob, AttackPacing.Tier1, category: TokenCategory.Ranged), Is.True);
        }

        [Test]
        public void TokenCategories_PerHero_AnotherHeroHasItsOwnPools()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap), Is.True);
            Assert.That(t.TryAcquire(Hero2, 2, 0.0, AttackCatalog.RatLunge, NoGap), Is.True);
        }

        [Test]
        public void TokenCategories_HeavyStillReservesExclusively_AcrossEveryCategory()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.SlimeGlob, NoGap, category: TokenCategory.Ranged), Is.True);
            // the Warden's quake cannot start while a ranged hold stands on the hero: it reserves instead
            Assert.That(t.TryAcquire(Hero, 9, 0.1, AttackCatalog.WardenQuake, NoGap, exclusive: true, category: TokenCategory.Heavy), Is.False);
            Assert.That(t.IsReserved(Hero, 0.1), Is.True);
            Assert.That(t.TryAcquire(Hero, 2, 0.1, AttackCatalog.RatLunge, NoGap, category: TokenCategory.Melee), Is.False, "nobody new while the signature waits");
            double later = 1.0;   // the glob's window (0.77-0.87 s) has closed
            Assert.That(t.TryAcquire(Hero, 9, later, AttackCatalog.WardenQuake, NoGap, exclusive: true, category: TokenCategory.Heavy), Is.True);
            Assert.That(t.TryAcquire(Hero, 2, later, AttackCatalog.SlimeGlob, NoGap, category: TokenCategory.Ranged), Is.False, "the signature plays alone");
        }

        [Test]
        public void TokenSwap_ANearerEnemyTakesAnUnstartedHoldersToken()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap, begin: false, distance: 8f), Is.True);
            Assert.That(t.TryAcquire(Hero, 2, 0.0, AttackCatalog.RatLunge, NoGap, distance: 3f), Is.False, "pool full");
            Assert.That(t.TrySwap(Hero, 2, 0.0, AttackCatalog.RatLunge, NoGap, TokenCategory.Melee, 3f), Is.True);
            Assert.That(t.Holds(2, 0.0), Is.True);
            Assert.That(t.Holds(1, 0.0), Is.False, "the far enemy lost it and must ask again");
            Assert.That(t.HoldersOn(Hero, TokenCategory.Melee, 0.0), Is.EqualTo(1));
        }

        [Test]
        public void TokenSwap_NeverTakesFromAStartedWindUp()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap, begin: false, distance: 8f), Is.True);
            t.Begin(1);   // its wind-up started
            Assert.That(t.TrySwap(Hero, 2, 0.1, AttackCatalog.RatLunge, NoGap, TokenCategory.Melee, 3f), Is.False);
            Assert.That(t.Holds(1, 0.1), Is.True);
            // an ordinary acquire (begin defaults to true) is started from the first frame
            var u = new AttackTokens();
            Assert.That(u.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap, distance: 8f), Is.True);
            Assert.That(u.TrySwap(Hero, 2, 0.0, AttackCatalog.RatLunge, NoGap, TokenCategory.Melee, 3f), Is.False);
        }

        [Test]
        public void TokenSwap_OnlyWhenStrictlyNearer_SameCategory_AndTheOldHolderKeepsItWhenRefused()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap, begin: false, distance: 4f), Is.True);
            Assert.That(t.TrySwap(Hero, 2, 0.0, AttackCatalog.RatLunge, NoGap, TokenCategory.Melee, 4f), Is.False, "a tie does not swap");
            Assert.That(t.TrySwap(Hero, 2, 0.0, AttackCatalog.RatLunge, NoGap, TokenCategory.Melee, 6f), Is.False, "farther does not swap");
            Assert.That(t.TrySwap(Hero, 3, 0.0, AttackCatalog.SlimeGlob, NoGap, TokenCategory.Ranged, 1f), Is.False, "another category's holder is not swapped");
            Assert.That(t.Holds(1, 0.0), Is.True);
            // a swap the gap would refuse anyway leaves the old holder in place: a forced glob (0.77-0.87 s) stands on the hero, the swapper's hop would hit at 0.57-0.67 s
            var gap = new PacingProfile(1, 0.5f, 1f, 1f, 0.3f);
            var u = new AttackTokens();
            Assert.That(u.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, gap, begin: false, distance: 9f), Is.True);
            u.ForceAcquire(Hero, 7, 0.0, AttackCatalog.SlimeGlob, gap, TokenCategory.Ranged);
            Assert.That(u.TrySwap(Hero, 2, 0.0, AttackCatalog.SlimeHop, gap, TokenCategory.Melee, 2f), Is.False);
            Assert.That(u.Holds(1, 0.0), Is.True, "refused anyway: the far enemy keeps its token");
            Assert.That(u.Holds(2, 0.0), Is.False);
        }

        [Test]
        public void TokenLeak_AHolderThatDiesWithoutReleaseIsGoneAfterStrikeEnd()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap), Is.True);
            Assert.That(t.Holds(1, 0.3), Is.True, "still inside its attack");
            Assert.That(t.Holds(1, 0.61), Is.False, "the hit window (ends 0.60 s) closed: the token lapsed by itself");
            Assert.That(t.TryAcquire(Hero, 2, 0.61, AttackCatalog.RatLunge, NoGap), Is.True, "the pool is free again");
        }

        [Test]
        public void TokenLeak_AuditFreesADeadHolder_AndAnOverlongHold_AndItsReservation()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, NoGap), Is.True);
            Assert.That(t.TryAcquire(Hero2, 2, 0.0, AttackCatalog.SlimeGlob, NoGap, category: TokenCategory.Ranged), Is.True);
            Assert.That(t.Audit(0.1, id => id != 1), Is.EqualTo(1), "holder 1 is gone");
            Assert.That(t.Holds(1, 0.1), Is.False);
            Assert.That(t.Holds(2, 0.1), Is.True);
            Assert.That(t.Audit(0.3, id => true, maxAge: 0.2), Is.EqualTo(1), "holder 2 has held longer than the timeout");
            Assert.That(t.Count, Is.EqualTo(0));
            // a dead elite's reservation does not block the hero for its 2.5 s
            Assert.That(t.TryAcquire(Hero, 3, 1.0, AttackCatalog.RatLunge, NoGap), Is.True);
            Assert.That(t.TryAcquire(Hero, 9, 1.0, AttackCatalog.WardenQuake, NoGap, exclusive: true, category: TokenCategory.Heavy), Is.False);
            Assert.That(t.IsReserved(Hero, 1.0), Is.True);
            t.Audit(1.1, id => id != 9);
            Assert.That(t.IsReserved(Hero, 1.1), Is.False);
        }

        [Test]
        public void ClosedWindow_StaysOnRecordForTheGap()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, AttackPacing.Tier1), Is.True);   // hits 0.47-0.60 s
            Assert.That(t.TryAcquire(Hero, 2, 0.61, AttackCatalog.RatLunge, AttackPacing.Tier1), Is.False, "would hit at 1.08 s: only 0.48 s after the first");
            Assert.That(t.TryAcquire(Hero, 2, 0.64, AttackCatalog.RatLunge, AttackPacing.Tier1), Is.True, "hits at 1.11 s: 0.51 s after");
        }

        [Test]
        public void Release_AfterTheWindowEndedButBeforeAnyExpire_LeavesTheGapTail()
        {
            var t = new AttackTokens();
            var one = new PacingProfile(1, 0.5f, 1f, 1f, 0.3f);
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, one), Is.True);   // hits 0.467-0.600 s
            // the window closed at 0.600 s; nothing asked the tokens since, so Expire has not run when the holder is released (death, a late stagger)
            t.Release(1, 0.62);
            Assert.That(t.TryAcquire(Hero, 2, 0.62, AttackCatalog.RatLunge, one), Is.False, "strikes 0.487 s after the first window: inside the gap");
            Assert.That(t.TryAcquire(Hero, 2, 0.64, AttackCatalog.RatLunge, one), Is.True, "strikes 0.507 s after");
        }

        [Test]
        public void Release_MidWindow_ClosesTheWindowNow_AndKeepsItsGap()
        {
            var t = new AttackTokens();
            var one = new PacingProfile(1, 0.5f, 1f, 1f, 0.3f);
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, one), Is.True);   // hits 0.467-0.600 s
            t.Release(1, 0.50);   // staggered while the hitbox was on: the window closed at 0.50 s
            Assert.That(t.TryAcquire(Hero, 2, 0.50 - 0.467 + 0.45, AttackCatalog.RatLunge, one), Is.False, "strikes at 0.95 s: only 0.45 s after the stagger");
            Assert.That(t.TryAcquire(Hero, 2, 0.50 - 0.467 + 0.52, AttackCatalog.RatLunge, one), Is.True, "strikes at 1.02 s: 0.52 s after");
        }

        [Test]
        public void Release_BeforeTheWindowOpened_LeavesNoTail()
        {
            var t = new AttackTokens();
            var one = new PacingProfile(1, 0.5f, 1f, 1f, 0.3f);
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, one), Is.True);
            t.Release(1, 0.1);   // cut short in the wind-up: no hit was ever due
            Assert.That(t.TryAcquire(Hero, 2, 0.1, AttackCatalog.RatLunge, one), Is.True);
        }

        [Test]
        public void ForceAcquire_LeavesTheNormalTailAfterItsWindow()
        {
            var t = new AttackTokens();
            var one = new PacingProfile(1, 0.5f, 1f, 1f, 0.3f);
            t.ForceAcquire(Hero, 7, 0.0, AttackCatalog.WardenBreakFree, one);   // hits 0.600-0.733 s
            Assert.That(t.TryAcquire(Hero, 2, 0.75, AttackCatalog.RatLunge, one), Is.False, "strike 1.217 s: 0.484 s after the window");
            Assert.That(t.TryAcquire(Hero, 2, 0.78, AttackCatalog.RatLunge, one), Is.True, "strike 1.247 s: 0.514 s after");
        }

        [Test]
        public void TrySwap_RefusedExclusiveSwapper_LeavesNoReservationBehind()
        {
            var t = new AttackTokens();
            var two = new PacingProfile(2, 0f, 1f, 1f, 0.3f);
            Assert.That(t.TryAcquire(Hero, 1, 0.0, AttackCatalog.RatLunge, two, begin: false, distance: 9f, category: TokenCategory.Melee), Is.True);
            Assert.That(t.TryAcquire(Hero, 3, 0.0, AttackCatalog.SlimeGlob, two, category: TokenCategory.Ranged), Is.True);   // an unrelated, started holder
            // the swapper (exclusive) takes holder 1's token away, still sees holder 3 on the hero, reserves, and is refused: the rollback must drop the reservation
            Assert.That(t.TrySwap(Hero, 9, 0.0, AttackCatalog.WardenQuake, two, TokenCategory.Melee, 2f, exclusive: true), Is.False);
            Assert.That(t.IsReserved(Hero, 0.0), Is.False, "no reservation outlives a refused swap");
            Assert.That(t.Holds(1, 0.0), Is.True);
        }

        [Test]
        public void Attack_CanNameItsOwnPool_ElseTheEnemyDefault()
        {
            Assert.That(EnemyCatalog.Slime.CategoryOf(AttackCatalog.SlimeHop), Is.EqualTo(TokenCategory.Melee), "the hop is a light melee attack");
            Assert.That(EnemyCatalog.Slime.CategoryOf(AttackCatalog.SlimeGlob), Is.EqualTo(TokenCategory.Ranged), "the glob is the slime's ranged attack");
            Assert.That(EnemyCatalog.RatKing.CategoryOf(AttackCatalog.RatLunge), Is.EqualTo(TokenCategory.Melee));
            Assert.That(EnemyCatalog.DoughWarden.CategoryOf(AttackCatalog.WardenSlam), Is.EqualTo(TokenCategory.Heavy));
            Assert.That(EnemyCatalog.DoughWarden.CategoryOf(AttackCatalog.WardenQuake), Is.EqualTo(TokenCategory.Heavy));
        }

        // ---- the property test ----

        sealed class Window { public int Holder, Target; public double S, E; public bool Cancelled, Released; }

        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        public void Property_RandomCrowds_NeverExceedAPoolOrBreakTheGap(int seed, int tier)
        {
            var rng = new Random(seed * 7919);
            PacingProfile pacing = AttackPacing.ForTier(tier);
            var t = new AttackTokens();
            var attacks = new[] { AttackCatalog.SlimeHop, AttackCatalog.SlimeGlob, AttackCatalog.RatLunge, AttackCatalog.RatSpin, AttackCatalog.BroomSweep, AttackCatalog.BroomPoke };
            const int heroes = 3, enemies = 10;
            var cat = new TokenCategory[enemies];
            for (int i = 0; i < enemies; i++) cat[i] = (TokenCategory)rng.Next(0, 3);
            var nextAsk = new double[enemies];
            var windows = new List<Window>();
            var last = new Window[enemies];
            double now = 0.0;
            int granted = 0;
            for (int tick = 0; tick < 4000; tick++, now += 0.05)
            {
                for (int e = 0; e < enemies; e++)
                {
                    int id = 1000 + e;
                    if (last[e] != null && !last[e].Released && rng.NextDouble() < 0.04)
                    {
                        // Every Release path: before the window opened (no tail), during it (it closes now), or after it ended with no Expire in between
                        // (a normal attack end, a late stagger or death): Release itself must leave the gap tail.
                        Window w0 = last[e];
                        t.Release(id, now);
                        w0.Released = true;
                        if (now < w0.S) w0.Cancelled = true;
                        else if (now < w0.E) w0.E = now;
                    }
                    if (now < nextAsk[e] || t.Holds(id, now)) continue;
                    AttackData a = attacks[rng.Next(attacks.Length)];
                    int hero = rng.Next(heroes);
                    if (t.TryAcquire(hero, id, now, a, pacing, category: cat[e]))
                    {
                        var w = new Window { Holder = id, Target = hero, S = now + a.HitOn.Start / (double)AttackData.FrameRate, E = now + a.HitOn.End / (double)AttackData.FrameRate };
                        windows.Add(w);
                        last[e] = w;
                        granted++;
                        nextAsk[e] = now + 0.5 + rng.NextDouble();
                    }
                    else nextAsk[e] = now + pacing.RetrySeconds * (1.0 + 0.5 * rng.NextDouble());
                }
                for (int h = 0; h < heroes; h++)
                    for (int c = 0; c < 3; c++)
                        Assert.That(t.HoldersOn(h, (TokenCategory)c, now), Is.LessThanOrEqualTo(pacing.PoolSize((TokenCategory)c)), "pool of category " + c + " on hero " + h + " at " + now);
            }
            Assert.That(granted, Is.GreaterThan(100), "the crowd attacked");
            foreach (Window a in windows)
            {
                if (a.Cancelled) continue;
                foreach (Window b in windows)
                {
                    if (b.Cancelled || ReferenceEquals(a, b) || a.Target != b.Target || a.Holder == b.Holder) continue;
                    bool apart = b.S >= a.E + pacing.MinStrikeGapSeconds - 1e-9 || a.S >= b.E + pacing.MinStrikeGapSeconds - 1e-9;
                    Assert.That(apart, Is.True, "hero " + a.Target + ": windows " + a.S.ToString("F2") + "-" + a.E.ToString("F2") + " and " + b.S.ToString("F2") + "-" + b.E.ToString("F2"));
                }
            }
        }
    }
}
