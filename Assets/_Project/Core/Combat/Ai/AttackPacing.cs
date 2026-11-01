// Core/Combat/AttackPacing.cs: how often enemies may attack and how many at once, per tier, and the host's attack tokens (docs/research/combat-timings.md,
// recommendation 9). Pure C#. P9 Placeholders.
// Authority: host. Only the host's enemy AI asks for tokens; nothing here is replicated (clients see the resulting attacks through the attack serial).
//
// Why: deaths in the first arena came from attacks overlapping, not from damage. DC1 never lets more than 3 monsters stand near each other and a monster
// re-attacks about every 1.8 s; KH lets only a few enemies swing at the player at once while the rest hover. So each hero holds at most N attack tokens,
// and two strikes on the same hero must land at least a gap apart, so every hit can be read and answered on its own. Later tiers press harder through
// shorter cooldowns and a tighter gap (and mixed wave rosters, ArenaWaves), never through hits that land together.
using System;
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

        // Holders one hero allows per token category: melee and ranged each get the tier cap, heavy is the one elite (docs/research/enemy-ai.md 2.3).
        public int PoolSize(TokenCategory c) => c == TokenCategory.Heavy ? 1 : MaxAttackersPerHero;
    }

    public static class AttackPacing
    {
        // Tier 1 (waves 1-4): one attacker per hero, a duel at a time while the other enemy circles (OoT: the others wait; combat depth 2026-10-30, was
        // 2). Tier 2: two attackers, and they come back faster and closer together.
        public static readonly PacingProfile Tier1 = new PacingProfile(1, 0.5f, 1.0f, 1.0f, 0.3f);
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

    // Which attack an enemy starts at a distance (host AI, pure so EditMode tests cover the cadence). The elite's signature comes on a cadence (after
    // SignatureEvery other AI-chosen attacks) and is never part of the roll; the roll picks evenly among the other attacks, starting at roll x count and
    // taking the first one in reach, so the order of the attack list does not bias it.
    public static class EnemyAttackChooser
    {
        public const float LobReach = 6.5f;         // how far a glob can be lobbed (was 8.5: read as "hit from a distance" with nothing flying at you)
        public const float MinLobDistance = 3.2f;   // closer than this a lobber uses its other attacks

        public static bool IsSignature(AttackData a) => a == AttackCatalog.WardenQuake;

        public static bool SignatureDue(int attacksSinceSignature) => attacksSinceSignature >= AttackPacing.SignatureEvery;

        // How far the AI starts an attack from: a lob's throw, else the strike's range around where a lunge or hop lands (up to LungeMeters ahead).
        public static float Reach(AttackData a) => a.Origin == HitOrigin.GroundAtTarget ? LobReach : a.Range + a.LungeMeters * 0.7f + 0.2f;

        public static AttackData Choose(EnemyDefinition def, float dist, float roll01, int attacksSinceSignature)
        {
            AttackData[] attacks = def.Attacks;
            int n = 0;
            AttackData signature = null;
            for (int i = 0; i < attacks.Length; i++)
                if (IsSignature(attacks[i])) signature = attacks[i];
                else n++;
            if (signature != null && SignatureDue(attacksSinceSignature) && dist <= Reach(signature)) return signature;
            if (n == 0) return null;
            int start = Math.Max(0, Math.Min(n - 1, (int)Math.Floor(roll01 * n)));
            for (int k = 0; k < n; k++)
            {
                AttackData a = NthRolled(attacks, (start + k) % n);
                if (a.Origin == HitOrigin.GroundAtTarget && dist < MinLobDistance) continue;
                if (dist > Reach(a)) continue;
                return a;
            }
            return null;
        }

        private static AttackData NthRolled(AttackData[] attacks, int index)
        {
            for (int i = 0; i < attacks.Length; i++)
            {
                if (IsSignature(attacks[i])) continue;
                if (index-- == 0) return attacks[i];
            }
            return null;
        }
    }

    // Which of a hero's token pools an attack draws from (docs/research/enemy-ai.md 2.3): a full melee pool does not block a slime's glob. Heavy is the
    // elite's pool; its signature is still exclusive on top of that.
    public enum TokenCategory : byte { Melee = 0, Ranged = 1, Heavy = 2 }

    // The host's attack tokens: who may attack which hero right now. Keys are opaque ids (the host uses the Unity objects' hash codes, their instance
    // ids, unique across every NetworkManager in one process). A token is held from the attack's start until its hit window closes and then lapses by
    // itself, so a missed Release can never leak one; the host still releases early when an attack is cut short (stagger, parry, death, despawn), and
    // Audit frees any hold whose holder is gone or that outlived a timeout.
    //
    // Pools are per hero AND per category (PacingProfile.PoolSize); the gap between hit windows is per hero across every category, so a rat's lunge and a
    // slime's glob can wind up together but never land together. ONE AttackTokens serves the whole CombatHost (every room's crowd shares it), so a hero's pools
    // and gap hold however many rooms its attackers come from. A window that has closed stays on record for the gap (a "tail"), so the next attack
    // cannot open inside it either.
    //
    // The elite's signature is EXCLUSIVE: its tell plays alone on that hero. When others still hold tokens it cannot start yet, so it RESERVES the hero:
    // from then on nobody new gets a token there, the current holders' tokens lapse within one hit window, and the signature takes the hero on its next
    // ask. A reservation lapses after ReserveSeconds (the elite walked off or chose something else), so it can never block a hero for good.
    //
    // Swap: a hold taken with begin=false is provisional (the holder has not started its wind-up). A nearer enemy may take it over (TrySwap); once the
    // holder calls Begin, or took the token with the default begin=true, nobody can take it.
    public sealed class AttackTokens
    {
        public const double ReserveSeconds = 2.5;
        public const double DefaultAuditMaxAgeSeconds = 6.0;   // no attack runs this long; an older hold is stuck

        private struct Hold
        {
            public int Target, Holder;
            public double Acquired, StrikeStart, StrikeEnd;
            public double Gap;
            public bool Exclusive, Started;
            public float Distance;
            public TokenCategory Category;
        }

        private struct Reservation
        {
            public int Target, Holder;
            public double Until;
        }

        private readonly List<Hold> _holds = new List<Hold>();
        private readonly List<Hold> _tails = new List<Hold>();   // closed hit windows still inside their gap
        private readonly List<Reservation> _reserved = new List<Reservation>();

        // Ask to start `attack` on `target` now. False: wait and ask again (AttackPacing RetrySeconds). An earlier hold by the same holder is replaced.
        // begin=false: the hold is provisional and may be swapped away until Begin(holder); distance (holder to target) ranks swappers.
        public bool TryAcquire(int target, int holder, double now, AttackData attack, in PacingProfile pacing, bool exclusive = false,
            TokenCategory category = TokenCategory.Melee, bool begin = true, float distance = 0f)
        {
            if (attack == null) return false;
            Expire(now);
            int reservedBy = ReservedBy(target);
            if (reservedBy != 0 && reservedBy != holder) return false;   // someone's signature is waiting for this hero
            double s = now + attack.HitOn.Start / (double)AttackData.FrameRate, e = now + attack.HitOn.End / (double)AttackData.FrameRate;
            int others = 0, inPool = 0;
            bool clash = false;
            for (int i = 0; i < _holds.Count; i++)
            {
                Hold h = _holds[i];
                if (h.Target != target || h.Holder == holder) continue;
                if (h.Exclusive) return false;
                others++;
                if (h.Category == category) inPool++;
                if (Clashes(s, e, h.StrikeStart, h.StrikeEnd, pacing.MinStrikeGapSeconds)) clash = true;
            }
            for (int i = 0; i < _tails.Count; i++)
            {
                Hold h = _tails[i];
                if (h.Target == target && h.Holder != holder && Clashes(s, e, h.StrikeStart, h.StrikeEnd, pacing.MinStrikeGapSeconds)) clash = true;
            }
            if (exclusive && others > 0)
            {
                if (reservedBy == 0) _reserved.Add(new Reservation { Target = target, Holder = holder, Until = now + ReserveSeconds });
                return false;
            }
            if (clash || inPool >= pacing.PoolSize(category)) return false;
            Release(holder, now);
            _holds.Add(new Hold
            {
                Target = target, Holder = holder, Acquired = now, StrikeStart = s, StrikeEnd = e, Gap = pacing.MinStrikeGapSeconds,
                Exclusive = exclusive, Started = begin, Distance = distance, Category = category,
            });
            return true;
        }

        private static bool Clashes(double s, double e, double hs, double he, double gap) => s < he + gap && hs < e + gap;

        // A nearer enemy takes the token of a provisional holder (one that has not begun its wind-up) on the same hero and category. The old holder loses
        // its hold and must ask again. False when there is nobody to take it from, the swapper is not strictly nearer, or the token would still be refused
        // (gap, exclusive signature, reservation).
        public bool TrySwap(int target, int holder, double now, AttackData attack, in PacingProfile pacing, TokenCategory category, float distance, bool exclusive = false)
        {
            if (attack == null) return false;
            Expire(now);
            int at = -1;
            float far = distance;
            for (int i = 0; i < _holds.Count; i++)
            {
                Hold h = _holds[i];
                if (h.Target != target || h.Category != category || h.Started || h.Exclusive || h.Holder == holder) continue;
                if (h.Distance > far) { far = h.Distance; at = i; }
            }
            if (at < 0) return false;
            Hold saved = _holds[at];
            _holds.RemoveAt(at);
            int reservedBefore = _reserved.Count;
            if (TryAcquire(target, holder, now, attack, pacing, exclusive, category, begin: true, distance: distance)) return true;
            // Refused anyway: the old holder keeps it, and an exclusive swapper's refused ask must not leave its reservation behind (it would block the hero).
            if (_reserved.Count > reservedBefore) _reserved.RemoveRange(reservedBefore, _reserved.Count - reservedBefore);
            _holds.Insert(Math.Min(at, _holds.Count), saved);
            return false;
        }

        // The holder's wind-up has started: its token can no longer be swapped away.
        public void Begin(int holder)
        {
            for (int i = 0; i < _holds.Count; i++)
            {
                if (_holds[i].Holder != holder) continue;
                Hold h = _holds[i];
                h.Started = true;
                _holds[i] = h;
            }
        }

        // An attack that must start now whatever the tokens say (the elite's revenge break-free answers being chained): it is recorded without checking the
        // cap, the gap or a reservation, so the attacks that come after it still keep their gap from it: it leaves the normal tail (pacing's gap) once its
        // hit window closes, like any other hold.
        public void ForceAcquire(int target, int holder, double now, AttackData attack, in PacingProfile pacing, TokenCategory category = TokenCategory.Melee)
        {
            if (attack == null) return;
            Release(holder, now);
            _holds.Add(new Hold
            {
                Target = target, Holder = holder, Acquired = now, Started = true, Category = category, Gap = pacing.MinStrikeGapSeconds,
                StrikeStart = now + attack.HitOn.Start / (double)AttackData.FrameRate, StrikeEnd = now + attack.HitOn.End / (double)AttackData.FrameRate,
            });
        }

        // Frees the holder's hold and reservation at once (stagger, parry, death, despawn, a normal attack end). If its hit window had already opened, the
        // window closes now (or at its own end, if that came first) and stays on record for the gap, so Release itself leaves the tail; it does not depend
        // on Expire having run in between. A hold released before its window opened leaves no tail (no hit ever landed or was due).
        public void Release(int holder, double now)
        {
            Expire(now);
            for (int i = _holds.Count - 1; i >= 0; i--)
            {
                Hold h = _holds[i];
                if (h.Holder != holder) continue;
                _holds.RemoveAt(i);
                if (h.Gap > 0.0 && now >= h.StrikeStart)
                {
                    h.StrikeEnd = Math.Min(h.StrikeEnd, now);
                    _tails.Add(h);
                }
            }
            for (int i = _reserved.Count - 1; i >= 0; i--) if (_reserved[i].Holder == holder) _reserved.RemoveAt(i);
        }

        // Periodic safety net (the host calls it about every 2 s): frees the holds and reservations of holders that no longer exist, and any hold older than
        // maxAge. Time already lapses a hold at StrikeEnd, so this only matters for a hold whose clock cannot lapse (a stuck StrikeEnd). Returns how many it freed.
        public int Audit(double now, Func<int, bool> isAlive, double maxAge = DefaultAuditMaxAgeSeconds)
        {
            Expire(now);
            int freed = 0;
            for (int i = _holds.Count - 1; i >= 0; i--)
            {
                Hold h = _holds[i];
                if ((isAlive != null && !isAlive(h.Holder)) || now - h.Acquired > maxAge) { _holds.RemoveAt(i); freed++; }
            }
            for (int i = _reserved.Count - 1; i >= 0; i--)
                if (isAlive != null && !isAlive(_reserved[i].Holder)) { _reserved.RemoveAt(i); freed++; }
            return freed;
        }

        public void Clear() { _holds.Clear(); _tails.Clear(); _reserved.Clear(); }

        public int HoldersOn(int target, double now)
        {
            Expire(now);
            int n = 0;
            for (int i = 0; i < _holds.Count; i++) if (_holds[i].Target == target) n++;
            return n;
        }

        public int HoldersOn(int target, TokenCategory category, double now)
        {
            Expire(now);
            int n = 0;
            for (int i = 0; i < _holds.Count; i++) if (_holds[i].Target == target && _holds[i].Category == category) n++;
            return n;
        }

        public bool Holds(int holder, double now)
        {
            Expire(now);
            for (int i = 0; i < _holds.Count; i++) if (_holds[i].Holder == holder) return true;
            return false;
        }

        public bool IsReserved(int target, double now)
        {
            Expire(now);
            return ReservedBy(target) != 0;
        }

        public int Count => _holds.Count;

        private int ReservedBy(int target)
        {
            for (int i = 0; i < _reserved.Count; i++) if (_reserved[i].Target == target) return _reserved[i].Holder;
            return 0;
        }

        private void Expire(double now)
        {
            for (int i = _holds.Count - 1; i >= 0; i--)
            {
                Hold h = _holds[i];
                if (h.StrikeEnd > now) continue;
                _holds.RemoveAt(i);
                if (h.Gap > 0.0) _tails.Add(h);   // its window closed by time: keep it on record for the gap
            }
            for (int i = _tails.Count - 1; i >= 0; i--) if (_tails[i].StrikeEnd + _tails[i].Gap <= now) _tails.RemoveAt(i);
            for (int i = _reserved.Count - 1; i >= 0; i--) if (_reserved[i].Until <= now) _reserved.RemoveAt(i);
        }
    }
}
