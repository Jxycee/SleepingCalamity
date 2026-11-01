// Catches the user's note (2026-10-30) "enemy attacks must read without ground indicators": every enemy attack must stage its wind-up on the body
// (pose, hold, glint 12-24 frames before the hitbox), must stop turning and lunging before the glint, and must respect the wind-up floors and the
// punish window from docs/research/combat-timings.md (DC1, KH1, KH2).
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class EnemyTellTests
    {
        static System.Collections.Generic.IEnumerable<AttackData> RealAttacks()
        {
            foreach (AttackData a in AttackCatalog.AllEnemyAttacks) if (a != AttackCatalog.TestNova) yield return a;
        }

        [Test]
        public void EveryEnemyAttack_HasAnHonestTell()
        {
            foreach (AttackData a in RealAttacks())
                Assert.That(EnemyAttackRules.TellProblem(a), Is.Null, a.Id);
        }

        [Test]
        public void WindUpFloors_SmallTwentySeven_EliteFiftyFour_BreakFreeThirtySix()
        {
            foreach (EnemyDefinition def in EnemyCatalog.All)
                foreach (AttackData a in def.Attacks)
                    Assert.That(a.HitOn.Start, Is.GreaterThanOrEqualTo(EnemyAttackRules.WindUpFloor(a, def)), def.Name + " / " + a.Id);
            Assert.That(AttackCatalog.WardenBreakFree.HitOn.Start, Is.GreaterThanOrEqualTo(36));
            Assert.That(AttackCatalog.WardenSweep.HitOn.Start, Is.GreaterThanOrEqualTo(54), "was 40");
        }

        [Test]
        public void EveryAttack_LeavesAPunishWindow_AtBothTiers()
        {
            foreach (EnemyDefinition def in EnemyCatalog.All)
                foreach (AttackData a in def.Attacks)
                    for (int tier = 1; tier <= 2; tier++)
                        Assert.That(EnemyAttackRules.PunishFrames(a, def.Kind, tier), Is.GreaterThanOrEqualTo(EnemyAttackRules.MinPunishFrames), def.Name + " / " + a.Id + " tier " + tier);
        }

        [Test]
        public void TheEliteSignature_HasTheLongestTell_AChargeGlow_AndAToll()
        {
            AttackData q = AttackCatalog.WardenQuake;
            Assert.That(EnemyCatalog.DoughWarden.Attacks, Does.Contain(q));
            foreach (AttackData a in RealAttacks())
                if (a != q) Assert.That(q.HitOn.Start, Is.GreaterThan(a.HitOn.Start), a.Id);
            Assert.That(q.HitOn.Start / 60f, Is.InRange(1.2f, 1.7f), "KH2's single big strikes give 1.2-1.3 s; DC1's slowest heavy 1.45 s");
            Assert.That(q.Tell.ChargeGlow, Is.True);
            Assert.That(q.Tell.HoldSfx, Is.Not.Null);
            Assert.That(q.Tell.HoldFrames, Is.GreaterThanOrEqualTo(18), "a long, readable hold");
            Assert.That(q.GuardBreak, Is.True);
            Assert.That(System.Array.IndexOf(AttackCatalog.AllEnemyAttacks, q), Is.EqualTo(11),
                "appended after the original eleven: the replicated attack index of every older attack is unchanged");
        }

        [Test]
        public void Sample_WalksThroughTheStages_InOrder()
        {
            AttackData a = AttackCatalog.WardenSlam;
            EnemyTell t = a.Tell;
            Assert.That(TellMath.Sample(a, 1f).Stage, Is.EqualTo(TellStage.Anticipate));
            Assert.That(TellMath.Sample(a, t.PoseEnd).Stage, Is.EqualTo(TellStage.Hold));
            Assert.That(TellMath.Sample(a, t.Commit).Stage, Is.EqualTo(TellStage.Commit));
            Assert.That(TellMath.Sample(a, a.HitOn.Start).Stage, Is.EqualTo(TellStage.Strike));
            Assert.That(TellMath.Sample(a, a.HitOn.End).Stage, Is.EqualTo(TellStage.Recover));
            Assert.That(TellMath.Sample(a, a.TotalFrames).Stage, Is.EqualTo(TellStage.None));
        }

        [Test]
        public void Sample_PeaksAtThePoseEnd_HoldsStill_ThenStrikesForward()
        {
            AttackData a = AttackCatalog.RatLunge;
            EnemyTell t = a.Tell;
            TellPose early = TellMath.Sample(a, t.PoseEnd * 0.5f), peak = TellMath.Sample(a, t.PoseEnd), held = TellMath.Sample(a, t.Commit - 1);
            Assert.That(early.Lean, Is.GreaterThan(0f).And.LessThan(t.LeanDeg));
            Assert.That(peak.Lean, Is.EqualTo(t.LeanDeg).Within(1e-4f), "leaning back at the peak");
            Assert.That(held.Lean, Is.EqualTo(peak.Lean).Within(1e-4f), "the hold does not move");
            Assert.That(held.Squash, Is.EqualTo(1f - t.Crouch).Within(1e-4f));
            Assert.That(held.Tremble, Is.EqualTo(t.Tremble), "it shivers while holding");
            TellPose strike = TellMath.Sample(a, a.HitOn.Start);
            Assert.That(strike.Lean, Is.LessThan(0f), "the strike overshoots forward");
            Assert.That(strike.Squash, Is.GreaterThan(1f), "and stretches");
            TellPose late = TellMath.Sample(a, a.TotalFrames - 1);
            Assert.That(late.Lean, Is.EqualTo(0f).Within(0.01f), "back at rest by the end");
        }

        [Test]
        public void Glint_IsFullAtTheCommit_AndGoneBeforeTheHitboxCloses()
        {
            foreach (AttackData a in RealAttacks())
            {
                EnemyTell t = a.Tell;
                Assert.That(TellMath.Sample(a, t.Commit - 0.5f).Glint, Is.EqualTo(0f), a.Id + ": nothing glints before the commit");
                Assert.That(TellMath.Sample(a, t.Commit).Glint, Is.EqualTo(1f), a.Id);
                Assert.That(TellMath.Sample(a, t.Commit + TellMath.GlintFrames).Glint, Is.EqualTo(0f), a.Id);
            }
        }

        [Test]
        public void ChargeGlow_RisesThroughTheHold()
        {
            AttackData q = AttackCatalog.WardenQuake;
            Assert.That(TellMath.Sample(q, q.Tell.PoseEnd).Charge, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(TellMath.Sample(q, (q.Tell.PoseEnd + q.Tell.Commit) * 0.5f).Charge, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(TellMath.Sample(q, q.Tell.Commit + 1).Charge, Is.EqualTo(1f));
            Assert.That(TellMath.Sample(AttackCatalog.SlimeHop, AttackCatalog.SlimeHop.Tell.PoseEnd + 2).Charge, Is.EqualTo(0f), "only charge tells glow");
        }

        [Test]
        public void Crossed_FiresOnceEvenWhenAFrameIsSkipped()
        {
            Assert.That(TellMath.Crossed(13.2f, 15.9f, 14), Is.True, "a 20 fps step jumps over frame 14");
            Assert.That(TellMath.Crossed(14f, 15f, 14), Is.False, "already past");
            Assert.That(TellMath.Crossed(12f, 13.99f, 14), Is.False);
            Assert.That(TellMath.Crossed(13.99f, 14f, 14), Is.True);
        }
    }

    // Catches overlapping enemy strikes, the "waves too hard" cause in the first arena: tokens per hero, a gap between strikes, the elite's exclusive tell,
    // and that a token can never leak (it lapses when its hit window closes).
    public class AttackTokensTests
    {
        const int Hero = 1, Hero2 = 2;
        // Two attackers, the gap of tier 1: the token rules with room for a second attacker (tier 1 allows one since combat depth, 2026-10-30).
        static readonly PacingProfile P = new PacingProfile(2, AttackPacing.Tier1.MinStrikeGapSeconds, 1f, 1f, 0.3f);

        [Test]
        public void AtMostTwoAttackersPerHero_ButAnotherHeroHasItsOwn()
        {
            var t = new AttackTokens();
            var wide = new PacingProfile(2, 0f, 1f, 1f, 0.3f);
            // strikes at 0.57-0.67 s, 1.20-1.33 s and 1.50-1.67 s: none overlap, so only the count can refuse the third
            Assert.That(t.TryAcquire(Hero, 10, 0.0, AttackCatalog.SlimeHop, wide), Is.True);
            Assert.That(t.TryAcquire(Hero, 11, 0.0, AttackCatalog.WardenPound, wide), Is.True);
            Assert.That(t.TryAcquire(Hero, 12, 0.0, AttackCatalog.WardenQuake, wide), Is.False, "a third waits");
            Assert.That(t.TryAcquire(Hero2, 12, 0.0, AttackCatalog.WardenQuake, wide), Is.True, "per hero");
            Assert.That(t.HoldersOn(Hero, 0.0), Is.EqualTo(2));
        }

        [Test]
        public void TwoStrikesOnOneHero_LandAtLeastTheGapApart()
        {
            var t = new AttackTokens();
            AttackData hop = AttackCatalog.SlimeHop;   // strike 34-40 frames
            Assert.That(t.TryAcquire(Hero, 10, 0.0, hop, P), Is.True);
            Assert.That(t.TryAcquire(Hero, 11, 0.0, hop, P), Is.False, "the same strike frame");
            double gapStart = (hop.HitOn.End - hop.HitOn.Start) / 60.0 + P.MinStrikeGapSeconds;
            Assert.That(t.TryAcquire(Hero, 11, gapStart - 0.02, hop, P), Is.False, "just inside the gap");
            Assert.That(t.TryAcquire(Hero, 11, gapStart + 0.02, hop, P), Is.True, "just outside it");
            // an attack whose strike lands before the other one's (a fast poke started late) must also clear the gap
            var u = new AttackTokens();
            Assert.That(u.TryAcquire(Hero, 10, 0.0, AttackCatalog.WardenPound, P), Is.True);   // strike 1.20-1.33 s
            Assert.That(u.TryAcquire(Hero, 11, 0.5, AttackCatalog.RatLunge, P), Is.False);     // strike 0.97-1.10 s: too close
            Assert.That(u.TryAcquire(Hero, 11, 0.0, AttackCatalog.RatLunge, P), Is.True);      // strike 0.47-0.60 s: 0.6 s clear
        }

        [Test]
        public void ATokenLapses_WhenItsHitWindowCloses_SoItCannotLeak()
        {
            var t = new AttackTokens();
            AttackData a = AttackCatalog.RatSpin;
            Assert.That(t.TryAcquire(Hero, 10, 0.0, a, P), Is.True);
            Assert.That(t.Holds(10, a.HitOn.End / 60.0 - 0.01), Is.True);
            Assert.That(t.Holds(10, a.HitOn.End / 60.0), Is.False, "never Released, still gone");
            Assert.That(t.Count, Is.EqualTo(0));
        }

        [Test]
        public void Release_FreesTheTokenAtOnce_OnStaggerParryDeathOrDespawn()
        {
            var t = new AttackTokens();
            var wide = new PacingProfile(1, 0f, 1f, 1f, 0.3f);
            Assert.That(t.TryAcquire(Hero, 10, 0.0, AttackCatalog.WardenSlam, wide), Is.True);
            Assert.That(t.TryAcquire(Hero, 11, 0.1, AttackCatalog.SlimeHop, wide), Is.False);
            t.Release(10);
            Assert.That(t.TryAcquire(Hero, 11, 0.1, AttackCatalog.SlimeHop, wide), Is.True);
            t.Clear();
            Assert.That(t.Count, Is.EqualTo(0));
        }

        [Test]
        public void AskingAgain_ReplacesTheOldHold()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 10, 0.0, AttackCatalog.SlimeHop, P), Is.True);
            Assert.That(t.TryAcquire(Hero2, 10, 0.1, AttackCatalog.SlimeHop, P), Is.True);
            Assert.That(t.HoldersOn(Hero, 0.1), Is.EqualTo(0), "one enemy attacks one hero at a time");
            Assert.That(t.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheSignatureIsExclusive_NobodyElseStartsDuringIt()
        {
            var t = new AttackTokens();
            AttackData q = AttackCatalog.WardenQuake;
            Assert.That(t.TryAcquire(Hero, 10, 0.0, q, P, exclusive: true), Is.True);
            Assert.That(t.TryAcquire(Hero, 11, 0.0, AttackCatalog.RatLunge, P), Is.False, "its strike would be far from the quake's, but the tell plays alone");
            Assert.That(t.TryAcquire(Hero, 11, q.HitOn.End / 60.0 + P.MinStrikeGapSeconds, AttackCatalog.RatLunge, P), Is.True, "after the quake lands and its gap (a closed window stays on record for the gap, stage 2)");
        }

        [Test]
        public void TheSignature_WaitsForCurrentHolders_AndReservesTheHero_SoItIsNeverStarved()
        {
            var t = new AttackTokens();
            AttackData q = AttackCatalog.WardenQuake;
            Assert.That(t.TryAcquire(Hero, 11, 0.0, AttackCatalog.RatLunge, P), Is.True, "a rat is already winding up");
            Assert.That(t.TryAcquire(Hero, 10, 0.1, q, P, exclusive: true), Is.False, "the quake's tell must not overlap the rat's");
            Assert.That(t.IsReserved(Hero, 0.1), Is.True);
            Assert.That(t.TryAcquire(Hero, 12, 0.2, AttackCatalog.SlimeHop, P), Is.False, "nobody new while the quake waits");
            Assert.That(t.TryAcquire(Hero2, 12, 0.2, AttackCatalog.SlimeHop, P), Is.True, "another hero is not reserved");
            double ratDone = AttackCatalog.RatLunge.HitOn.End / 60.0;
            Assert.That(t.TryAcquire(Hero, 10, ratDone + 0.01, q, P, exclusive: true), Is.True, "the quake starts once the rat's token lapses");
            Assert.That(t.IsReserved(Hero, ratDone + 0.01), Is.False, "taking the token ends the reservation");
        }

        [Test]
        public void AReservation_Lapses_IfTheSignatureNeverComesBack()
        {
            var t = new AttackTokens();
            Assert.That(t.TryAcquire(Hero, 11, 0.0, AttackCatalog.WardenSlam, P), Is.True);
            Assert.That(t.TryAcquire(Hero, 10, 0.0, AttackCatalog.WardenQuake, P, exclusive: true), Is.False);
            double later = AttackTokens.ReserveSeconds + 0.01;
            Assert.That(t.IsReserved(Hero, later), Is.False);
            Assert.That(t.TryAcquire(Hero, 12, later, AttackCatalog.SlimeHop, P), Is.True);
            var u = new AttackTokens();
            Assert.That(u.TryAcquire(Hero, 11, 0.0, AttackCatalog.WardenSlam, P), Is.True);
            Assert.That(u.TryAcquire(Hero, 10, 0.0, AttackCatalog.WardenQuake, P, exclusive: true), Is.False);
            u.Release(10);   // the elite died or despawned
            Assert.That(u.IsReserved(Hero, 0.0), Is.False);
        }

        [Test]
        public void TheBreakFree_TakesAForcedHold_ThatLaterAttacksKeepTheirGapFrom()
        {
            var t = new AttackTokens();
            var one = new PacingProfile(1, 0.5f, 1f, 1f, 0.3f);
            Assert.That(t.TryAcquire(Hero, 11, 0.0, AttackCatalog.WardenPound, one), Is.True);
            t.ForceAcquire(Hero, 10, 0.0, AttackCatalog.WardenBreakFree);   // past the cap of one
            Assert.That(t.HoldersOn(Hero, 0.0), Is.EqualTo(2), "recorded although the cap was full");
            t.Release(11);
            Assert.That(t.TryAcquire(Hero, 12, 0.0, AttackCatalog.SlimeHop, one), Is.False, "the break-free's hold still counts");
            double after = AttackCatalog.WardenBreakFree.HitOn.End / 60.0 + 0.01;
            Assert.That(t.TryAcquire(Hero, 12, after, AttackCatalog.SlimeHop, one), Is.True);
        }

        [Test]
        public void TierTwo_PressesHarder_WithoutLettingStrikesOverlap()
        {
            PacingProfile t1 = AttackPacing.ForTier(1), t2 = AttackPacing.ForTier(2);
            Assert.That(t1.MaxAttackersPerHero, Is.EqualTo(1), "tier 1: one duel at a time (OoT: the others wait)");
            Assert.That(t2.MaxAttackersPerHero, Is.EqualTo(2), "tier 2: two at once, never more");
            Assert.That(t2.MinStrikeGapSeconds, Is.LessThan(t1.MinStrikeGapSeconds).And.GreaterThanOrEqualTo(0.3f), "closer, but every hit still on its own");
            foreach (EnemyKind k in new[] { EnemyKind.Slime, EnemyKind.RatKing, EnemyKind.Broom, EnemyKind.DoughWarden })
                Assert.That(AttackPacing.CooldownSeconds(k, 2), Is.LessThan(AttackPacing.CooldownSeconds(k, 1)), k.ToString());
            Assert.That(AttackPacing.CooldownSeconds(EnemyKind.Slime, 1), Is.EqualTo(1.2f).Within(1e-4f), "tier 1 keeps the greybox values the user passed");
            Assert.That(AttackPacing.RecoverSeconds(EnemyKind.RatKing, 1), Is.EqualTo(0.9f).Within(1e-4f));
        }
    }

    // Catches the signature's cadence breaking (the AI-only path, otherwise covered only by the opt-in bot) and the attack roll being biased by list order.
    public class EnemyAttackChooserTests
    {
        static readonly EnemyDefinition W = EnemyCatalog.DoughWarden;

        [Test]
        public void TheSignature_ComesAfterThreeOtherAttacks_InReach_AndNeverFromTheRoll()
        {
            for (int since = 0; since < AttackPacing.SignatureEvery; since++)
                for (float roll = 0f; roll < 1f; roll += 0.05f)
                    Assert.That(EnemyAttackChooser.Choose(W, 2f, roll, since), Is.Not.SameAs(AttackCatalog.WardenQuake), "since " + since + " roll " + roll);
            Assert.That(EnemyAttackChooser.Choose(W, 2f, 0.5f, AttackPacing.SignatureEvery), Is.SameAs(AttackCatalog.WardenQuake));
            Assert.That(EnemyAttackChooser.Choose(W, 2f, 0.5f, AttackPacing.SignatureEvery + 4), Is.SameAs(AttackCatalog.WardenQuake), "still due later");
            float far = EnemyAttackChooser.Reach(AttackCatalog.WardenQuake) + 0.5f;
            Assert.That(EnemyAttackChooser.Choose(W, far, 0.5f, AttackPacing.SignatureEvery), Is.Not.SameAs(AttackCatalog.WardenQuake), "only in reach");
        }

        [Test]
        public void TheRoll_PicksEvenlyAmongTheOtherAttacks()
        {
            var counts = new System.Collections.Generic.Dictionary<AttackData, int>();
            const int steps = 300;
            for (int i = 0; i < steps; i++)
            {
                AttackData a = EnemyAttackChooser.Choose(W, 1.5f, (i + 0.5f) / steps, 0);
                counts[a] = counts.TryGetValue(a, out int c) ? c + 1 : 1;
            }
            Assert.That(counts.Count, Is.EqualTo(3), "slam, sweep and pound");
            foreach (var kv in counts) Assert.That(kv.Value, Is.EqualTo(steps / 3).Within(2), kv.Key.Id);
        }

        [Test]
        public void TheSignatureCanSitAnywhereInTheList()
        {
            var shuffled = new EnemyDefinition(EnemyKind.DoughWarden, "w2", "Warden copy", W.Species, W.Hp, W.Atk, W.Def, W.MoveSpeed, W.Radius, W.Height,
                new[] { AttackCatalog.WardenQuake, AttackCatalog.WardenSlam, AttackCatalog.WardenSweep, AttackCatalog.WardenPound }, isElite: true);
            for (float roll = 0f; roll < 1f; roll += 0.05f)
                Assert.That(EnemyAttackChooser.Choose(shuffled, 2f, roll, 0), Is.Not.SameAs(AttackCatalog.WardenQuake));
            Assert.That(EnemyAttackChooser.Choose(shuffled, 2f, 0.1f, AttackPacing.SignatureEvery), Is.SameAs(AttackCatalog.WardenQuake));
        }

        [Test]
        public void ASlime_LobsOnlyFromRange_AndNothingIsChosenOutOfReach()
        {
            EnemyDefinition s = EnemyCatalog.Slime;
            for (float roll = 0f; roll < 1f; roll += 0.1f)
            {
                Assert.That(EnemyAttackChooser.Choose(s, 1.5f, roll, 0), Is.SameAs(AttackCatalog.SlimeHop), "too close to lob");
                Assert.That(EnemyAttackChooser.Choose(s, 5f, roll, 0), Is.SameAs(AttackCatalog.SlimeGlob), "only the lob reaches 5 m");
                Assert.That(EnemyAttackChooser.Choose(s, 8f, roll, 0), Is.Null);
            }
        }
    }
}
