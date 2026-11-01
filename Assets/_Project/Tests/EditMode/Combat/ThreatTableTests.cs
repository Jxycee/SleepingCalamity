// Enemy AI stage 3 (docs/research/enemy-ai.md 6.2, 6.3, 6.4 and the 10.5 rows "Threat spread", "Stickiness", "No switch mid-attack", "Downed hero",
// "Global strike cap", "Utility hysteresis"): the ThreatTable's load balance, stickiness, ranges and recent-hit pull, the TargetLedger, and the host-wide
// cap on simultaneous strikes in AttackTokens.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class ThreatTableTests
    {
        const float Dt = 0.1f;

        sealed class Foe
        {
            public int Id;
            public float X, Z;
            public ThreatTable Table = new ThreatTable();
        }

        static List<AiHero> Heroes(TargetLedger ledger, int self, params (int id, float x, float z, bool up)[] hs)
        {
            var list = new List<AiHero>();
            foreach (var h in hs) list.Add(new AiHero { Id = h.id, X = h.x, Z = h.z, Up = h.up, Load = ledger.CountOn(h.id, self) });
            return list;
        }

        static int Tick(Foe f, TargetLedger ledger, float dt, bool locked, params (int id, float x, float z, bool up)[] hs)
        {
            List<AiHero> heroes = Heroes(ledger, f.Id, hs);
            int idx = f.Table.Pick(heroes, f.X, 0f, f.Z, dt, locked);
            if (idx >= 0) ledger.Set(f.Id, heroes[idx].Id); else ledger.Remove(f.Id);
            return idx < 0 ? -1 : heroes[idx].Id;
        }

        [Test]
        public void ThreatSpread_ThreeEnemiesTwoEqualHeroes_SplitTwoOne_NeverThreeZero()
        {
            var ledger = new TargetLedger();
            var foes = new[] { new Foe { Id = 1 }, new Foe { Id = 2 }, new Foe { Id = 3 } };   // all at the origin: both heroes 5 m away
            var heroes = new[] { (10, 5f, 0f, true), (11, -5f, 0f, true) };
            var count = new Dictionary<int, int> { { 10, 0 }, { 11, 0 } };
            foreach (Foe f in foes) count[Tick(f, ledger, Dt, false, heroes)]++;
            Assert.That(Math.Max(count[10], count[11]), Is.EqualTo(2));
            Assert.That(Math.Min(count[10], count[11]), Is.EqualTo(1));
            // and it stays that way as time passes (stickiness, no flip-flop)
            for (int t = 0; t < 50; t++) foreach (Foe f in foes) Tick(f, ledger, Dt, false, heroes);
            Assert.That(ledger.CountOn(10, -1) + ledger.CountOn(11, -1), Is.EqualTo(3));
            Assert.That(Math.Abs(ledger.CountOn(10, -1) - ledger.CountOn(11, -1)), Is.EqualTo(1));
        }

        [Test]
        public void ThreatSpread_FourEnemiesTwoHeroes_TwoTwo()
        {
            var ledger = new TargetLedger();
            var heroes = new[] { (10, 5f, 0f, true), (11, -5f, 0f, true) };
            for (int i = 1; i <= 4; i++) Tick(new Foe { Id = i }, ledger, Dt, false, heroes);
            Assert.That(ledger.CountOn(10, -1), Is.EqualTo(2));
            Assert.That(ledger.CountOn(11, -1), Is.EqualTo(2));
        }

        [Test]
        public void Stickiness_ARivalThatIsOnlySlightlyBetter_NeverTakesOver()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            Assert.That(Tick(f, ledger, Dt, false, (10, 4f, 0f, true), (11, -6f, 0f, true)), Is.EqualTo(10));
            for (int t = 0; t < 60; t++)   // 6 s: hero 11 is now a bit nearer than the current target, which the sticky bonus outweighs
                Assert.That(Tick(f, ledger, Dt, false, (10, 4.5f, 0f, true), (11, -4f, 0f, true)), Is.EqualTo(10));
        }

        [Test]
        public void Stickiness_ABigRivalWaitsForTheRetargetInterval()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            Tick(f, ledger, Dt, false, (10, 3f, 0f, true), (11, -11f, 0f, true));
            // the current target runs off to the edge of the leash; the rival is right on top of the enemy (score gap far beyond the sticky bonus)
            var now = new[] { (10, 19f, 0f, true), (11, 0.5f, 0f, true) };
            for (int t = 0; t < 6; t++) Assert.That(Tick(f, ledger, Dt, false, now), Is.EqualTo(10), "inside the 0.75 s interval, tick " + t);
            int after = -1;
            for (int t = 0; t < 4; t++) after = Tick(f, ledger, Dt, false, now);
            Assert.That(after, Is.EqualTo(11), "once the interval has passed the rival takes over");
        }

        [Test]
        public void NoSwitchMidAttack_LockedKeepsTheTarget_ThenItCanSwitch()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            Tick(f, ledger, Dt, false, (10, 3f, 0f, true), (11, -11f, 0f, true));
            var now = new[] { (10, 19f, 0f, true), (11, 0.5f, 0f, true) };
            for (int t = 0; t < 30; t++) Assert.That(Tick(f, ledger, Dt, true, now), Is.EqualTo(10), "Tell or Strike: locked, tick " + t);
            Assert.That(Tick(f, ledger, Dt, false, now), Is.EqualTo(11), "unlocked and the interval long gone");
        }

        [Test]
        public void NoSwitchMidAttack_ButTheLockLapsesWhenTheTargetGoesDown()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            Tick(f, ledger, Dt, false, (10, 3f, 0f, true), (11, -4f, 0f, true));
            Assert.That(Tick(f, ledger, Dt, true, (10, 3f, 0f, false), (11, -4f, 0f, true)), Is.EqualTo(11));
        }

        [Test]
        public void DownedHero_IsNeverChosenWhileAnotherIsUp_NobodyUpMeansNoTarget()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            Assert.That(Tick(f, ledger, Dt, false, (10, 1f, 0f, false), (11, 9f, 0f, true)), Is.EqualTo(11), "the downed hero is nearer and ignored");
            Assert.That(Tick(f, ledger, Dt, false, (10, 1f, 0f, false), (11, 9f, 0f, false)), Is.EqualTo(-1));
            Assert.That(ledger.CountOn(11, -1), Is.EqualTo(0), "no target, no load");
        }

        [Test]
        public void Ranges_NoticeWithinAggro_KeepUntilLeash()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            Assert.That(Tick(f, ledger, Dt, false, (10, 15f, 0f, true)), Is.EqualTo(-1), "beyond the 12 m aggro range: not noticed");
            Assert.That(Tick(f, ledger, Dt, false, (10, 11f, 0f, true)), Is.EqualTo(10));
            Assert.That(Tick(f, ledger, Dt, false, (10, 19f, 0f, true)), Is.EqualTo(10), "still inside the 20 m leash");
            Assert.That(Tick(f, ledger, Dt, false, (10, 21f, 0f, true)), Is.EqualTo(-1), "past the leash: dropped");
            Assert.That(Tick(f, ledger, Dt, false, (10, 19f, 0f, true)), Is.EqualTo(-1), "and it must come back inside aggro to be noticed again");
        }

        [Test]
        public void Ranges_FollowPerEnemyData()
        {
            Assert.That(EnemyCatalog.RatKing.AggroRange, Is.EqualTo(12f));
            Assert.That(EnemyCatalog.RatKing.LeashRange, Is.EqualTo(20f));
            Assert.That(EnemyCatalog.RatKing.RetargetInterval, Is.EqualTo(0.75f));
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1, Table = new ThreatTable(aggroRange: 5f, leashRange: 6f) };
            Assert.That(Tick(f, ledger, Dt, false, (10, 7f, 0f, true)), Is.EqualTo(-1));
            Assert.That(Tick(f, ledger, Dt, false, (10, 4f, 0f, true)), Is.EqualTo(10));
        }

        [Test]
        public void RecentHits_PullTheEnemyToTheHero_ThatHurtIt()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            var heroes = new[] { (10, 5f, 0f, true), (11, -5f, 0f, true) };   // equal distance
            Assert.That(Tick(f, ledger, Dt, false, heroes), Is.EqualTo(10));
            for (int t = 0; t < 20; t++) Assert.That(Tick(f, ledger, Dt, false, heroes), Is.EqualTo(10), "no hits: it stays");
            f.Table.RecordHit(11, 30f);
            int picked = -1;
            for (int t = 0; t < 10; t++) picked = Tick(f, ledger, Dt, false, heroes);
            Assert.That(picked, Is.EqualTo(11), "hit pull 0.8 beats the sticky 0.6 once the retarget interval has passed");
        }

        [Test]
        public void UtilityHysteresis_NearTieDoesNotAlternate_Over100Ticks()
        {
            var ledger = new TargetLedger();
            var f = new Foe { Id = 1 };
            int switches = 0, last = -1;
            for (int t = 0; t < 100; t++)
            {
                float wobble = (t % 2 == 0 ? 0.05f : -0.05f);   // the heroes trade places by a hair every tick
                int now = Tick(f, ledger, Dt, false, (10, 5f + wobble, 0f, true), (11, -5f + wobble, 0f, true));
                if (last >= 0 && now != last) switches++;
                last = now;
            }
            Assert.That(switches, Is.EqualTo(0));
        }

        // ---- review fixes: hunters keep the distance scale, absolute hit decay, room scope, starvation ----

        static List<AiHero> One(params (int id, float x, bool inRoom)[] hs)
        {
            var l = new List<AiHero>();
            foreach (var h in hs) l.Add(new AiHero { Id = h.id, X = h.x, Up = true, InRoom = h.inRoom });
            return l;
        }

        [Test]
        public void Hunter_HasNoRangeFilter_ButStillPrefersTheNearerHero()
        {
            var t = new ThreatTable(unlimitedRange: true);
            Assert.That(t.Pick(One((10, 90f, false)), 0f, 0f, 0f, Dt, false), Is.EqualTo(0), "far beyond any leash, yet a candidate");
            var t2 = new ThreatTable(unlimitedRange: true);
            Assert.That(t2.Pick(One((10, 5f, false), (11, 100f, false)), 0f, 0f, 0f, Dt, false), Is.EqualTo(0));
            Assert.That(new ThreatTable().Pick(One((10, 90f, false)), 0f, 0f, 0f, Dt, false), Is.EqualTo(-1), "a normal enemy still ignores them");
        }

        [Test]
        public void HitPull_DecaysByHalfLife_SoALoneAttackersPullFades()
        {
            // Hero 11 is the farther one (near 0.33 vs 0.67 for hero 10); a fresh hit of 20 gives it pull 0.8 and the pick.
            var heroes = One((10, 4f, false), (11, 8f, false));
            var fresh = new ThreatTable();
            fresh.RecordHit(11, 20f);
            Assert.That(fresh.Pick(heroes, 0f, 0f, 0f, Dt, false), Is.EqualTo(1));

            var half = new ThreatTable();   // one half-life (6 s): pull 0.4, still enough
            half.RecordHit(11, 20f);
            half.Pick(new List<AiHero>(), 0f, 0f, 0f, 6f, false);
            Assert.That(half.Pick(heroes, 0f, 0f, 0f, 0.01f, false), Is.EqualTo(1));

            var two = new ThreatTable();    // two half-lives (12 s): pull 0.2 < the 0.33 distance gap, the nearer hero wins
            two.RecordHit(11, 20f);
            two.Pick(new List<AiHero>(), 0f, 0f, 0f, 12f, false);
            Assert.That(two.Pick(heroes, 0f, 0f, 0f, 0.01f, false), Is.EqualTo(0), "a lone attacker's pull no longer holds for 40 s");
        }

        [Test]
        public void RoomScope_AHeroInMyRoomIsNoticedAtAnyDistance_AndNeverLeashed()
        {
            var t = new ThreatTable();
            Assert.That(t.Pick(One((10, 30f, false)), 0f, 0f, 0f, Dt, false), Is.EqualTo(-1), "outside the room and beyond aggro: ignored");
            Assert.That(t.Pick(One((10, 30f, true)), 0f, 0f, 0f, Dt, false), Is.EqualTo(0), "inside the room: engaged from the far wall");
            Assert.That(t.Pick(One((10, 40f, true)), 0f, 0f, 0f, Dt, false), Is.EqualTo(0), "and kept past the leash while still in the room");
            Assert.That(t.Pick(One((10, 40f, false)), 0f, 0f, 0f, Dt, false), Is.EqualTo(-1), "once out of the room the leash applies");
        }

        [Test]
        public void Starvation_ALoneDistantEnemyUnderTheGlobalCapStillGetsAnAttackWithinTenSeconds()
        {
            var tokens = new AttackTokens { GlobalStrikeCap = CoopScaling.GlobalStrikeCap(2) };
            // Other enemies hold the whole cap at the start (three strikes on other heroes).
            for (int h = 0; h < 3; h++) tokens.TryAcquire(200 + h, 300 + h, 0.0, AttackCatalog.RatLunge, Pool2NoGap);
            var def = EnemyCatalog.RatKing;
            var brain = new AiBrain(tokens, () => 0.1f, def);
            float x = 0f; const float heroX = 10f; double time = 0.0; double attackAt = -1.0;
            for (int i = 0; i < 400 && attackAt < 0.0; i++)   // 20 s at 0.05
            {
                time += 0.05;
                var c = new AiContext { SelfId = 1, Def = def, Tier = 1, Dt = 0.05f, Time = time, Phase = AiState.Approach, SelfX = x };
                c.Heroes.Add(new AiHero { Id = 100, X = heroX, Up = true });
                c.TargetIndex = 0;
                AiAction a = brain.Tick(c);
                if (a.StartAttack != null) { attackAt = time; break; }
                x += a.MoveX * a.MoveSpeed * 0.05f;
            }
            Assert.That(attackAt, Is.GreaterThan(0.0), "it must get an attack");
            Assert.That(attackAt, Is.LessThan(10.0));
        }

        // ---- the global strike cap ----

        static readonly PacingProfile Pool2NoGap = new PacingProfile(2, 0f, 1f, 1f, 0.3f);

        [Test]
        public void GlobalStrikeCap_Values_FollowThePlayerCount()
        {
            Assert.That(CoopScaling.GlobalStrikeCap(1), Is.EqualTo(int.MaxValue), "solo: no cap");
            Assert.That(CoopScaling.GlobalStrikeCap(2), Is.EqualTo(3));
            Assert.That(CoopScaling.GlobalStrikeCap(3), Is.EqualTo(4));
            Assert.That(CoopScaling.GlobalStrikeCap(4), Is.EqualTo(5));
            Assert.That(CoopScaling.GlobalStrikeCap(9), Is.EqualTo(5), "clamped to four");
        }

        [Test]
        public void GlobalStrikeCap_FourHeroes_EightWouldStrikeButOnlyTheCapIsAllowed()
        {
            var tokens = new AttackTokens { GlobalStrikeCap = CoopScaling.GlobalStrikeCap(4) };
            int granted = 0;
            // Each hero's second attacker starts 0.3 s after its first (their hit windows do not touch, so the per-hero gap allows both): 8 under way at 0.3 s.
            for (int k = 0; k < 2; k++)
                for (int h = 0; h < 4; h++)
                    if (tokens.TryAcquire(100 + h, 1000 + h * 10 + k, k * 0.3, AttackCatalog.RatLunge, Pool2NoGap)) granted++;
            Assert.That(granted, Is.EqualTo(5), "four heroes x two tokens = 8 asked; the cap lets 5 through");
            Assert.That(tokens.StrikesAt(0.3), Is.EqualTo(5));

            var uncapped = new AttackTokens();
            int free = 0;
            for (int k = 0; k < 2; k++)
                for (int h = 0; h < 4; h++)
                    if (uncapped.TryAcquire(100 + h, 1000 + h * 10 + k, k * 0.3, AttackCatalog.RatLunge, Pool2NoGap)) free++;
            Assert.That(free, Is.EqualTo(8), "without the cap all eight go");
        }

        [Test]
        public void GlobalStrikeCap_FreesWhenAStrikeEnds_AndRandomCrowdsNeverExceedIt()
        {
            var rng = new Random(7);
            for (int players = 2; players <= 4; players++)
            {
                int cap = CoopScaling.GlobalStrikeCap(players);
                var tokens = new AttackTokens { GlobalStrikeCap = cap };
                double now = 0.0;
                for (int tick = 0; tick < 600; tick++)
                {
                    now += 0.05;
                    int enemy = rng.Next(0, 10);
                    if (rng.NextDouble() < 0.1) tokens.Release(2000 + enemy, now);
                    tokens.TryAcquire(100 + rng.Next(0, players), 2000 + enemy, now, AttackCatalog.RatLunge, Pool2NoGap);
                    for (double t = now; t < now + 1.5; t += 0.02)
                        Assert.That(tokens.StrikesAt(t), Is.LessThanOrEqualTo(cap), players + " players, tick " + tick + ", t " + t);
                }
            }
        }

        [Test]
        public void GlobalStrikeCap_Default_IsNoCap()
        {
            var tokens = new AttackTokens();
            int granted = 0;
            for (int h = 0; h < 6; h++) if (tokens.TryAcquire(100 + h, 1000 + h, 0.0, AttackCatalog.RatLunge, AttackPacing.Tier1)) granted++;
            Assert.That(granted, Is.EqualTo(6));
        }

        [Test]
        public void ForceAcquire_IgnoresTheCap()
        {
            var tokens = new AttackTokens { GlobalStrikeCap = 1 };
            Assert.That(tokens.TryAcquire(100, 1, 0.0, AttackCatalog.RatLunge, AttackPacing.Tier1), Is.True);
            Assert.That(tokens.TryAcquire(101, 2, 0.0, AttackCatalog.RatLunge, AttackPacing.Tier1), Is.False, "a second hero's attack is refused by the cap");
            tokens.ForceAcquire(101, 2, 0.0, AttackCatalog.RatLunge, AttackPacing.Tier1);
            Assert.That(tokens.Holds(2, 0.0), Is.True);
        }

        [Test]
        public void TargetLedger_CountsOthers_AndForgetsTheRemoved()
        {
            var l = new TargetLedger();
            l.Set(1, 10); l.Set(2, 10); l.Set(3, 11);
            Assert.That(l.CountOn(10, -1), Is.EqualTo(2));
            Assert.That(l.CountOn(10, 1), Is.EqualTo(1));
            l.Set(2, 11);
            Assert.That(l.CountOn(10, -1), Is.EqualTo(1));
            l.Remove(3);
            Assert.That(l.CountOn(11, -1), Is.EqualTo(1));
        }
    }
}
