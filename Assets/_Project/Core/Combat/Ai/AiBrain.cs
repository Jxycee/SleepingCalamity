// Core/Combat/Ai/AiBrain.cs: the enemy's decision FSM, pure C# (no UnityEngine), stage 1 of docs/research/enemy-ai.md section 10.
// Authority: host. Only the host's Enemy builds an AiContext and applies the AiAction; nothing here is replicated.
// Stage 1 reproduces the old Enemy.ServerUpdate decisions 1:1, including the order of random draws: the chase tick always draws the attack roll first
// (even on cooldown), a refused token draws the retry jitter, and only the first refusal of a streak draws the strafe side.
// The attack run itself (tell, strike, recover frames), stagger, knock and movement stay in Enemy; the brain decides while the enemy is not in them.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Combat
{
    public enum AiState : byte { Idle = 0, Approach = 1, Hover = 2, Tell = 3, Strike = 4, Recover = 5, Stagger = 6, Reposition = 7, Dead = 8 }

    public struct AiHero
    {
        public int Id;          // opaque key (the host uses the hero's hash code); also the token key
        public float X, Y, Z;
        public bool Up;         // alive and not downed
        public bool InRoom;     // inside the enemy's own room (room enemies only; the host fills it): always noticed, never leashed
        public int Load;        // OTHER enemies already targeting this hero (host-wide TargetLedger); the load-balance input of ThreatTable
    }

    // What the host fills each tick. Heroes are the ones in the enemy's own world (already filtered by the host).
    public sealed class AiContext
    {
        public int SelfId;
        public EnemyDefinition Def;
        public int Tier;
        public bool Raging;
        public float SelfX, SelfY, SelfZ;
        public readonly List<AiHero> Heroes = new List<AiHero>();
        public float Dt;
        public double Time;
        public AiState Phase;           // Recover while recovering, anything else (Approach) for the chase
        public float CooldownLeft;      // after this tick's decrement
        public float RecoverLeft;       // before this tick's decrement
        public float ScatterX, ScatterZ;
        public int TargetIndex = -1;    // set by AiBrain.PickTarget
        public bool HasTarget => TargetIndex >= 0;
    }

    // Desired result of one tick. Enemy applies it in the order: state, face, start attack (then stop), move.
    public struct AiAction
    {
        public AiState State;           // Idle / Approach (-> Chase phase) / Hover (-> Chase phase) / Recover (stay)
        public float FaceX, FaceZ, FaceDegPerSec;   // FaceDegPerSec 0: no turn
        public AttackData StartAttack;  // non-null: begin this attack on the target now (the token is already taken)
        public float MoveX, MoveZ, MoveSpeed;       // MoveSpeed 0: no move
        public float RecoverLeft;       // new recover timer
    }

    public sealed class AiBrain
    {
        private readonly AttackTokens _tokens;
        private readonly Func<float> _rand;     // 0..1, the host's Random.value
        private float _tokenRetry;              // seconds before asking for a token again after a refusal
        private bool _waiting;                  // refused last time: hover and strafe instead of closing in
        private float _strafeSign = 1f;
        private int _attacksSinceSignature;     // elite: its signature comes after AttackPacing.SignatureEvery other attacks

        private readonly EnemyDefinition _def;
        private ThreatTable _threat;

        private readonly bool _hunts;   // always knows where the heroes are (arena waves, the hunter): no aggro or leash limit

        public AiBrain(AttackTokens tokens, Func<float> rand, EnemyDefinition def = null, bool hunts = false) { _tokens = tokens; _rand = rand; _def = def; _hunts = hunts; }

        // Who this enemy goes for (stage 3). Created on first use from the enemy's own ranges.
        // Hunters keep the normal distance scale (their near/role terms still differ per hero) and only skip the range filter.
        public ThreatTable Threat => _threat ??= _def != null
            ? new ThreatTable(_def.AggroRange, _def.LeashRange, _def.RetargetInterval, unlimitedRange: _hunts)
            : new ThreatTable(unlimitedRange: _hunts);

        // Threat-based pick: sets c.TargetIndex. locked = in Tell or Strike (no switch). Ranged enemies prefer the farthest hero.
        public int PickTarget(AiContext c, bool locked)
        {
            bool ranged = c.Def != null && c.Def.TokenCategory == TokenCategory.Ranged;
            c.TargetIndex = Threat.Pick(c.Heroes, c.SelfX, c.SelfY, c.SelfZ, c.Dt, locked, ranged);
            return c.TargetIndex;
        }

        public bool WaitingForToken => _waiting;
        public AiState State { get; private set; }

        // Stateless nearest living hero (3D distance, first wins a tie); -1 when none is up. Fallback for callers with no threat state (force-attacks).
        public static int PickTarget(IReadOnlyList<AiHero> heroes, float x, float y, float z)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < heroes.Count; i++)
            {
                AiHero h = heroes[i];
                if (!h.Up) continue;
                float dx = h.X - x, dy = h.Y - y, dz = h.Z - z;
                float sq = dx * dx + dy * dy + dz * dz;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }

        public AiAction Tick(AiContext c)
        {
            var a = new AiAction { State = AiState.Idle, RecoverLeft = c.RecoverLeft };
            if (!c.HasTarget) { State = AiState.Idle; return a; }
            AiHero t = c.Heroes[c.TargetIndex];
            float toX = t.X - c.SelfX, toZ = t.Z - c.SelfZ;
            float dist = (float)Math.Sqrt(toX * toX + toZ * toZ);

            if (c.Phase == AiState.Recover)
            {
                a.RecoverLeft = c.RecoverLeft - c.Dt;
                a.State = AiState.Recover;
                if (c.Def.Kind == EnemyKind.RatKing) { a.MoveX = c.ScatterX; a.MoveZ = c.ScatterZ; a.MoveSpeed = c.Def.MoveSpeed * 0.9f; }   // scatter and regroup
                else { a.FaceX = toX; a.FaceZ = toZ; a.FaceDegPerSec = 240f; }
                if (a.RecoverLeft <= 0f) a.State = AiState.Approach;
                State = a.State;
                return a;
            }

            a.State = AiState.Approach;
            a.FaceX = toX; a.FaceZ = toZ;
            a.FaceDegPerSec = c.Def.Kind == EnemyKind.DoughWarden ? 200f : 420f;
            if (_tokenRetry > 0f) _tokenRetry -= c.Dt;
            AttackData next = EnemyAttackChooser.Choose(c.Def, dist, _rand(), _attacksSinceSignature);
            if (next == null) _waiting = false;
            else if (c.CooldownLeft <= 0f && _tokenRetry <= 0f)
            {
                PacingProfile pacing = AttackPacing.ForTier(c.Tier);
                if (_tokens.TryAcquire(t.Id, c.SelfId, c.Time, next, pacing, exclusive: next == AttackCatalog.WardenQuake, category: c.Def.CategoryOf(next)))
                {
                    _waiting = false;
                    a.StartAttack = next;
                    // Only AI-chosen attacks count toward the signature's cadence (not the break-free, not a test's forced attack).
                    if (EnemyAttackChooser.IsSignature(next)) _attacksSinceSignature = 0;
                    else if (c.Def.IsElite) _attacksSinceSignature++;
                    State = AiState.Tell;
                    return a;
                }
                _tokenRetry = pacing.RetrySeconds * (1f + 0.5f * _rand());
                if (!_waiting) _strafeSign = _rand() < 0.5f ? -1f : 1f;
                _waiting = true;
            }
            float denom = Math.Max(0.001f, dist);
            float dirX = toX / denom, dirZ = toZ / denom;
            float speed = c.Def.MoveSpeed * (c.Raging ? 1.25f : 1f);
            float stop = c.Def.Radius + 0.55f;
            if (_waiting)
            {
                // Waiting for a turn: hold at the edge of reach and circle the hero (KH's hovering crowd), drifting out if crowded in.
                float sideX = dirZ * _strafeSign, sideZ = -dirX * _strafeSign;   // cross(up, dir) * sign
                float hover = stop + 1.2f;
                float rx = 0f, rz = 0f;
                if (dist < hover) { rx = -dirX * 0.6f; rz = -dirZ * 0.6f; }
                else if (dist > hover + 1.5f) { rx = dirX; rz = dirZ; }
                float mx = sideX + rx, mz = sideZ + rz;
                float mag = (float)Math.Sqrt(mx * mx + mz * mz);
                if (mag > 1e-5f) { mx /= mag; mz /= mag; } else { mx = 0f; mz = 0f; }
                a.MoveX = mx; a.MoveZ = mz; a.MoveSpeed = speed * 0.45f;
                a.State = AiState.Hover;
            }
            else if (dist > stop + 0.3f) { a.MoveX = dirX; a.MoveZ = dirZ; a.MoveSpeed = speed; }
            State = a.State;
            return a;
        }
    }
}
