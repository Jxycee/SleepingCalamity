// Pins the decisions the old Enemy.ServerUpdate made (enemy AI stage 1, docs/research/enemy-ai.md section 10): approach when far, hover when a token
// is refused, the order of random draws, the token take and its release, and the nearest-living-hero target rule.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class AiBrainTests
    {
        sealed class Rolls
        {
            readonly Queue<float> _q;
            public int Drawn;
            public Rolls(params float[] v) { _q = new Queue<float>(v); }
            public float Next() { Drawn++; return _q.Count > 0 ? _q.Dequeue() : 0.5f; }
        }

        static AiContext Ctx(EnemyDefinition def, float heroX, int tier = 1, double time = 10.0, float cooldown = 0f, int selfId = 1, int heroId = 100)
        {
            var c = new AiContext { SelfId = selfId, Def = def, Tier = tier, Dt = 0.02f, Time = time, Phase = AiState.Approach, CooldownLeft = cooldown };
            c.Heroes.Add(new AiHero { Id = heroId, X = heroX, Y = 0f, Z = 0f, Up = true });
            c.TargetIndex = 0;
            return c;
        }

        [Test]
        public void NoTarget_IsIdle_AndDrawsNothing()
        {
            var r = new Rolls();
            var brain = new AiBrain(new AttackTokens(), r.Next);
            var c = Ctx(EnemyCatalog.Broom, 3f);
            c.TargetIndex = -1;
            AiAction a = brain.Tick(c);
            Assert.That(a.State, Is.EqualTo(AiState.Idle));
            Assert.That(a.StartAttack, Is.Null);
            Assert.That(r.Drawn, Is.EqualTo(0));
        }

        [Test]
        public void FarAway_Approaches_FacesAtTurnRate_AndStillDrawsTheRollOnce()
        {
            var r = new Rolls();
            var brain = new AiBrain(new AttackTokens(), r.Next);
            AiAction a = brain.Tick(Ctx(EnemyCatalog.Broom, 20f));
            Assert.That(a.State, Is.EqualTo(AiState.Approach));
            Assert.That(a.StartAttack, Is.Null);
            Assert.That(a.MoveX, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(a.MoveZ, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(a.MoveSpeed, Is.EqualTo(EnemyCatalog.Broom.MoveSpeed).Within(1e-5f));
            Assert.That(a.FaceDegPerSec, Is.EqualTo(420f));
            Assert.That(r.Drawn, Is.EqualTo(1), "the attack roll is drawn every chase tick, in reach or not");
            Assert.That(brain.WaitingForToken, Is.False);
        }

        [Test]
        public void TheWarden_TurnsSlower_AndARagingEnemyMovesQuicker()
        {
            var brain = new AiBrain(new AttackTokens(), new Rolls().Next);
            var c = Ctx(EnemyCatalog.DoughWarden, 30f);
            c.Raging = true;
            AiAction a = brain.Tick(c);
            Assert.That(a.FaceDegPerSec, Is.EqualTo(200f));
            Assert.That(a.MoveSpeed, Is.EqualTo(EnemyCatalog.DoughWarden.MoveSpeed * 1.25f).Within(1e-5f));
        }

        [Test]
        public void CloseEnough_StopsMoving()
        {
            var def = EnemyCatalog.Broom;
            var brain = new AiBrain(new AttackTokens(), new Rolls().Next);
            // In reach but on cooldown: no attack, and inside stop distance the enemy holds still.
            AiAction a = brain.Tick(Ctx(def, def.Radius + 0.55f + 0.2f, cooldown: 1f));
            Assert.That(a.StartAttack, Is.Null);
            Assert.That(a.MoveSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void InReach_TakesATokenAndStartsTheRolledAttack_ThenReleaseFreesIt()
        {
            var tokens = new AttackTokens();
            var brain = new AiBrain(tokens, new Rolls(0f).Next);
            var def = EnemyCatalog.Broom;
            AiAction a = brain.Tick(Ctx(def, 1.5f));
            Assert.That(a.StartAttack, Is.Not.Null);
            Assert.That(a.StartAttack, Is.SameAs(EnemyAttackChooser.Choose(def, 1.5f, 0f, 0)), "same pick as the chooser for roll 0");
            Assert.That(a.MoveSpeed, Is.EqualTo(0f), "starting an attack ends the tick without moving");
            Assert.That(tokens.Holds(1, 10.0), Is.True);
            Assert.That(tokens.HoldersOn(100, 10.0), Is.EqualTo(1));
            tokens.Release(1, 10.0);   // what a stagger, parry, death or despawn does
            Assert.That(tokens.Holds(1, 10.0), Is.False);
        }

        [Test]
        public void OnCooldown_DoesNotAsk_AndDoesNotDrawRetryNumbers()
        {
            var tokens = new AttackTokens();
            var r = new Rolls(0f);
            var brain = new AiBrain(tokens, r.Next);
            AiAction a = brain.Tick(Ctx(EnemyCatalog.Broom, 1.5f, cooldown: 0.3f));
            Assert.That(a.StartAttack, Is.Null);
            Assert.That(tokens.Count, Is.EqualTo(0));
            Assert.That(r.Drawn, Is.EqualTo(1));
            Assert.That(brain.WaitingForToken, Is.False);
        }

        [Test]
        public void RefusedToken_HoversAndStrafes_DrawingRetryJitterThenStrafeSide_InThatOrder()
        {
            var tokens = new AttackTokens();
            var def = EnemyCatalog.Broom;
            // Another enemy (id 2) holds the only tier-1 token on the hero.
            Assert.That(tokens.TryAcquire(100, 2, 10.0, AttackCatalog.BroomSweep, AttackPacing.Tier1), Is.True);
            // roll 0 (pick), jitter 1.0 (retry = 0.3 * 1.5), side 0.2 (< 0.5: -1)
            var r = new Rolls(0f, 1f, 0.2f);
            var brain = new AiBrain(tokens, r.Next);
            var c = Ctx(def, 1.5f);   // 1.5 m: inside the hover distance (radius + 0.55 + 1.2 = 2.3), so it drifts out while circling
            AiAction a = brain.Tick(c);
            Assert.That(r.Drawn, Is.EqualTo(3));
            Assert.That(a.StartAttack, Is.Null);
            Assert.That(a.State, Is.EqualTo(AiState.Hover));
            Assert.That(brain.WaitingForToken, Is.True);
            Assert.That(a.MoveSpeed, Is.EqualTo(def.MoveSpeed * 0.45f).Within(1e-5f));
            // dir = (1,0), side = cross(up, dir) * -1 = (0,0,+1)... cross(up,(1,0,0)) = (0,0,-1), times -1 = (0,0,1); radial = -dir*0.6 = (-0.6,0)
            float len = (float)System.Math.Sqrt(0.6 * 0.6 + 1.0);
            Assert.That(a.MoveX, Is.EqualTo(-0.6f / len).Within(1e-5f));
            Assert.That(a.MoveZ, Is.EqualTo(1f / len).Within(1e-5f));
        }

        [Test]
        public void WhileWaiting_RetryTimerBlocksAsking_AndStrafeSideIsKept()
        {
            var tokens = new AttackTokens();
            Assert.That(tokens.TryAcquire(100, 2, 10.0, AttackCatalog.BroomSweep, AttackPacing.Tier1), Is.True);
            var r = new Rolls(0f, 0f, 0.9f, 0f, 0f);
            var brain = new AiBrain(tokens, r.Next);
            AiAction first = brain.Tick(Ctx(EnemyCatalog.Broom, 2.5f));   // refused: draws roll, jitter 0 (retry 0.3), side 0.9 (+1)
            Assert.That(r.Drawn, Is.EqualTo(3));
            Assert.That(brain.WaitingForToken, Is.True);
            AiAction second = brain.Tick(Ctx(EnemyCatalog.Broom, 2.5f));  // retry still running: only the roll is drawn
            Assert.That(r.Drawn, Is.EqualTo(4));
            Assert.That(second.MoveX, Is.EqualTo(first.MoveX).Within(1e-6f));
            Assert.That(second.MoveZ, Is.EqualTo(first.MoveZ).Within(1e-6f), "same strafe side");
        }

        [Test]
        public void OutOfReach_ClearsWaiting()
        {
            var tokens = new AttackTokens();
            Assert.That(tokens.TryAcquire(100, 2, 10.0, AttackCatalog.BroomSweep, AttackPacing.Tier1), Is.True);
            var brain = new AiBrain(tokens, new Rolls(0f, 0f, 0.9f).Next);
            brain.Tick(Ctx(EnemyCatalog.Broom, 1.5f));
            Assert.That(brain.WaitingForToken, Is.True);
            brain.Tick(Ctx(EnemyCatalog.Broom, 25f));
            Assert.That(brain.WaitingForToken, Is.False, "no attack in reach: back to closing in");
        }

        [Test]
        public void TheWardensSignature_CountsAfterThreeOtherAttacks_AndTakesTheTokenExclusively()
        {
            var tokens = new AttackTokens();
            var def = EnemyCatalog.DoughWarden;
            var brain = new AiBrain(tokens, new Rolls().Next);   // always 0.5
            double t = 10.0;
            for (int i = 0; i < AttackPacing.SignatureEvery; i++)
            {
                AiAction a = brain.Tick(Ctx(def, 2f, tier: 1, time: t));
                Assert.That(a.StartAttack, Is.Not.Null.And.Not.SameAs(AttackCatalog.WardenQuake), "attack " + i);
                tokens.Release(1, t);
                t += 5.0;
            }
            AiAction sig = brain.Tick(Ctx(def, 2f, tier: 1, time: t));
            Assert.That(sig.StartAttack, Is.SameAs(AttackCatalog.WardenQuake));
            // The counter restarts: the next one is an ordinary attack again.
            tokens.Release(1, t);
            AiAction after = brain.Tick(Ctx(def, 2f, tier: 1, time: t + 8.0));
            Assert.That(after.StartAttack, Is.Not.SameAs(AttackCatalog.WardenQuake));
        }

        [Test]
        public void Recover_RatKingScatters_OthersFace_AndItEndsWhenTheTimerRunsOut()
        {
            var brain = new AiBrain(new AttackTokens(), new Rolls().Next);
            var c = Ctx(EnemyCatalog.RatKing, 3f);
            c.Phase = AiState.Recover; c.RecoverLeft = 0.5f; c.ScatterX = 0f; c.ScatterZ = 1f;
            AiAction a = brain.Tick(c);
            Assert.That(a.State, Is.EqualTo(AiState.Recover));
            Assert.That(a.RecoverLeft, Is.EqualTo(0.48f).Within(1e-5f));
            Assert.That(a.MoveZ, Is.EqualTo(1f));
            Assert.That(a.MoveSpeed, Is.EqualTo(EnemyCatalog.RatKing.MoveSpeed * 0.9f).Within(1e-5f));
            Assert.That(a.FaceDegPerSec, Is.EqualTo(0f));

            c = Ctx(EnemyCatalog.Broom, 3f);
            c.Phase = AiState.Recover; c.RecoverLeft = 0.01f;
            a = brain.Tick(c);
            Assert.That(a.State, Is.EqualTo(AiState.Approach), "timer out: back to the chase");
            Assert.That(a.FaceDegPerSec, Is.EqualTo(240f));
            Assert.That(a.MoveSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void PickTarget_IsTheNearestLivingHero_FirstWinsATie()
        {
            var heroes = new List<AiHero>
            {
                new AiHero { Id = 1, X = 1f, Up = false },
                new AiHero { Id = 2, X = 5f, Up = true },
                new AiHero { Id = 3, X = -5f, Up = true },
                new AiHero { Id = 4, X = 9f, Up = true },
            };
            Assert.That(AiBrain.PickTarget(heroes, 0f, 0f, 0f), Is.EqualTo(1), "the downed hero is skipped; a tie goes to the first");
            Assert.That(AiBrain.PickTarget(heroes, 8f, 0f, 0f), Is.EqualTo(3));
            heroes.Clear();
            Assert.That(AiBrain.PickTarget(heroes, 0f, 0f, 0f), Is.EqualTo(-1));
        }

        [Test]
        public void PickTarget_UsesHeightToo()
        {
            var heroes = new List<AiHero> { new AiHero { Id = 1, X = 3f, Y = 4f, Up = true }, new AiHero { Id = 2, X = 4.5f, Up = true } };
            Assert.That(AiBrain.PickTarget(heroes, 0f, 0f, 0f), Is.EqualTo(1), "5 m up-and-over loses to 4.5 m flat");
        }
    }
}
