// Core/Combat/AttackPacing.cs: how often enemies may attack and how many at once, per tier, and the host's attack tokens (docs/research/combat-timings.md,
// recommendation 9). Pure C#. P9 Placeholders.
// Authority: host. Only the host's enemy AI asks for tokens; nothing here is replicated (clients see the resulting attacks through the attack serial).
//
// Why: deaths in the first arena came from attacks overlapping, not from damage. DC1 never lets more than 3 monsters stand near each other and a monster
// re-attacks about every 1.8 s; KH lets only a few enemies swing at the player at once while the rest hover. So each hero holds at most N attack tokens,
// and two strikes on the same hero must land at least a gap apart, so every hit can be read and answered on its own. Later tiers press harder through
// shorter cooldowns and a tighter gap (and mixed wave rosters, ArenaWaves), never through hits that land together.
using System.Collections.Generic;

namespace SleepingCalamity.Core.Combat
{
    public readonly struct PacingProfile
    {
        public readonly int MaxAttackersPerHero;   // enemies winding up or striking at one hero at once
        public readonly float MinStrikeGapSeconds; // between the hit windows of two enemies' attacks on the same hero
        public readonly float CooldownScale;       // x the per-kind cooldown after an attack
        public readonly float RecoverScale;        // x the per-kind recover time after an attack
        public readonly float RetrySeconds;        // an enemy refused a token asks again after this (plus up to half again, so they never sync)

        public PacingProfile(int maxAttackers, float minGap, float cooldownScale, float recoverScale, float retry)
        { MaxAttackersPerHero = maxAttackers; MinStrikeGapSeconds = minGap; CooldownScale = cooldownScale; RecoverScale = recoverScale; RetrySeconds = retry; }
    }

    public static class AttackPacing
    {
        // Tier 1 (waves 1-4, which the user passed): the tokens barely bite. Tier 2: the same two attackers, but they come back faster and closer together.
        public static readonly PacingProfile Tier1 = new PacingProfile(2, 0.5f, 1.0f, 1.0f, 0.3f);
        public static readonly PacingProfile Tier2 = new PacingProfile(2, 0.35f, 0.7f, 0.8f, 0.2f);

        public static PacingProfile ForTier(int tier) => tier >= 2 ? Tier2 : Tier1;

        // Seconds before the enemy may start another attack (it chases meanwhile). Values from the M3 greybox, now scaled per tier.
        public static float CooldownSeconds(EnemyKind kind, int tier)
        {
            float b = kind == EnemyKind.DoughWarden ? 1.4f : kind == EnemyKind.RatKing ? 0.8f : 1.2f;
            return b * ForTier(tier).CooldownScale;
        }

        // Seconds the enemy stands (or, the rat-king, scatters) after its attack ends: part of the punish window with the attack's own recovery frames.
        public static float RecoverSeconds(EnemyKind kind, int tier)
        {
            float b = kind == EnemyKind.RatKing ? 0.9f : 0.35f;
            return b * ForTier(tier).RecoverScale;
        }

        // The Warden's signature after this many other attacks (and first after this many, so the fight opens with the basics).
        public const int SignatureEvery = 3;
        public const float SignatureExtraRecoverSeconds = 1.0f;   // stuck in the floor after the quake
    }

    // The host's attack tokens: who may attack which hero right now. Keys are opaque ids (the host uses Unity instance ids, unique across every
    // NetworkManager in one process). A token is held from the attack's start until its hit window closes and then lapses by itself, so a missed
    // Release can never leak one; the host still releases early when an attack is cut short (stagger, parry, death, despawn).
    public sealed class AttackTokens
    {
        private struct Hold
        {
            public int Target, Holder;
            public double StrikeStart, StrikeEnd;
            public bool Exclusive;
        }

        private readonly List<Hold> _holds = new List<Hold>();

        // Ask to start `attack` on `target` now. False: wait and ask again (AttackPacing RetrySeconds). Exclusive (the elite's signature): while held, nobody
        // else gets a token on that hero, so the big tell plays alone. An earlier hold by the same holder is replaced.
        public bool TryAcquire(int target, int holder, double now, AttackData attack, in PacingProfile pacing, bool exclusive = false)
        {
            if (attack == null) return false;
            Expire(now);
            double s = now + attack.HitOn.Start / (double)AttackData.FrameRate, e = now + attack.HitOn.End / (double)AttackData.FrameRate;
            int others = 0;
            for (int i = 0; i < _holds.Count; i++)
            {
                Hold h = _holds[i];
                if (h.Target != target || h.Holder == holder) continue;
                if (h.Exclusive) return false;
                others++;
                if (s < h.StrikeEnd + pacing.MinStrikeGapSeconds && h.StrikeStart < e + pacing.MinStrikeGapSeconds) return false;
            }
            if (others >= pacing.MaxAttackersPerHero) return false;
            Release(holder);
            _holds.Add(new Hold { Target = target, Holder = holder, StrikeStart = s, StrikeEnd = e, Exclusive = exclusive });
            return true;
        }

        public void Release(int holder)
        {
            for (int i = _holds.Count - 1; i >= 0; i--) if (_holds[i].Holder == holder) _holds.RemoveAt(i);
        }

        public void Clear() => _holds.Clear();

        public int HoldersOn(int target, double now)
        {
            Expire(now);
            int n = 0;
            for (int i = 0; i < _holds.Count; i++) if (_holds[i].Target == target) n++;
            return n;
        }

        public bool Holds(int holder, double now)
        {
            Expire(now);
            for (int i = 0; i < _holds.Count; i++) if (_holds[i].Holder == holder) return true;
            return false;
        }

        public int Count => _holds.Count;

        private void Expire(double now)
        {
            for (int i = _holds.Count - 1; i >= 0; i--) if (_holds[i].StrikeEnd <= now) _holds.RemoveAt(i);
        }
    }
}
