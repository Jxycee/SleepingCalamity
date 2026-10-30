// Core/Combat/EnemyRules.cs: the revenge gauge, rage, co-op scaling, boss parts and the downed/revive rules (docs/PHASE1.md 6.1, 6.5, P23, P24). Pure C#.
// Authority: host. The host holds one gauge, one rage tracker and one parts list per enemy, summed over every player's hits.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Combat
{
    // Anti-stunlock for the elite (and later bosses): every hit adds its attack's revenge value; when the gauge reaches the cap the enemy breaks free
    // and the gauge empties. The cap grows with the party so four players do not make it break free nonstop: 12 x (1 + 0.5 x (players - 1)).
    public sealed class RevengeGauge
    {
        public const float BaseCap = 12f;
        public const float PerExtraPlayer = 0.5f;
        public const float Opener = 1f, Finisher = 3f, Charge = 4f;   // the plan's per-hit values (they live in AttackData.Revenge; named here for the tests)

        public float Value { get; private set; }
        public float Cap { get; private set; }
        public int Players { get; private set; }

        public RevengeGauge(int players = 1) { SetPlayers(players); }

        public static float CapFor(int players) => BaseCap * (1f + PerExtraPlayer * (Math.Max(1, players) - 1));

        public void SetPlayers(int players)
        {
            Players = Math.Max(1, players);
            Cap = CapFor(Players);
            if (Value > Cap) Value = Cap;
        }

        // Adds a hit's revenge. True when the gauge filled: the gauge is emptied and the enemy should break free.
        public bool Add(float amount)
        {
            if (!(amount > 0f) || float.IsInfinity(amount)) return false;
            Value += amount;
            if (Value >= Cap)
            {
                Value = 0f;
                return true;
            }
            return false;
        }

        public float Fill => Cap > 0f ? Value / Cap : 0f;
        public void Reset() { Value = 0f; }
    }

    public enum RageState : byte { Calm = 0, Telegraphing = 1, Raging = 2 }

    // After N hits an enemy telegraphs and then gains attack for a while (DC2). Every landed hit on it counts, whoever made it. Not used by the elite
    // (it has the revenge gauge instead).
    public sealed class RageTracker
    {
        public const int HitsToRage = 7;
        public const float AttackBonus = 0.3f;
        public const float TelegraphSeconds = 0.8f, RageSeconds = 15f;

        public RageState State { get; private set; }
        public int Hits { get; private set; }
        public float TimeLeft { get; private set; }   // of the telegraph or the rage

        public float AttackMultiplier => State == RageState.Raging ? 1f + AttackBonus : 1f;

        // Returns true on the hit that starts the telegraph. Hits during a telegraph or rage do not count toward the next.
        public bool OnHit()
        {
            if (State != RageState.Calm) return false;
            Hits++;
            if (Hits < HitsToRage) return false;
            State = RageState.Telegraphing;
            TimeLeft = TelegraphSeconds;
            return true;
        }

        public void Tick(float dt)
        {
            if (State == RageState.Calm) return;
            TimeLeft -= dt;
            if (TimeLeft > 0f) return;
            if (State == RageState.Telegraphing) { State = RageState.Raging; TimeLeft = RageSeconds; }
            else { State = RageState.Calm; Hits = 0; TimeLeft = 0f; }
        }
    }

    public static class CoopScaling
    {
        static readonly float[] Life = { 1.0f, 1.0f, 1.7f, 2.3f, 2.8f };   // index = players (0 is unused), P23

        public static int ClampPlayers(int players) => Math.Max(1, Math.Min(4, players));
        public static float LifeMultiplier(int players) => Life[ClampPlayers(players)];
        public static int ExtraEnemiesPerRoom(int players) => ClampPlayers(players) - 1;   // +1 per extra player; attack is unchanged
        public static int EnemyCount(int baseCount, int players) => Math.Max(0, baseCount) + ExtraEnemiesPerRoom(players);
    }

    // A breakable part of the elite with its own HP. Hitting the part multiplies the damage dealt to the body (a weak point) and wears the part down;
    // when it breaks (once) it drops healing.
    public sealed class BossPart
    {
        public readonly string Id;
        public readonly int MaxHp;
        public readonly float WeakMultiplier;     // body damage multiplier while the part is intact
        public readonly float HealPerDrop;        // HP the dropped healing restores
        public int Hp { get; private set; }
        public bool Broken => Hp <= 0;

        public BossPart(string id, int maxHp, float weakMultiplier = 1.5f, float healPerDrop = 25f)
        {
            Id = id;
            MaxHp = Math.Max(1, maxHp);
            Hp = MaxHp;
            WeakMultiplier = weakMultiplier;
            HealPerDrop = healPerDrop;
        }

        // Wears the part down; true on the hit that breaks it (only once).
        public bool Damage(int amount)
        {
            if (Broken || amount <= 0) return false;
            Hp = Math.Max(0, Hp - amount);
            return Hp == 0;
        }

        public float BodyMultiplier => Broken ? 1f : WeakMultiplier;
        public void Restore() { Hp = MaxHp; }
    }

    // A player's life and the co-op downed state (P24): at 0 HP the player is downed for 20 s; an ally holding revive for 3 s lifts them to 40% HP;
    // when the 20 s run out the player is out. When every player is downed or out, the dive ends.
    public enum VitalState : byte { Up = 0, Downed = 1, Out = 2 }

    public sealed class PlayerVitals
    {
        public const int BaseMaxHp = 100;
        public const float BaseDef = 10f;
        public const float DownedSeconds = 20f, ReviveHoldSeconds = 3f, ReviveHpFraction = 0.4f;

        public int MaxHp { get; }
        public float Def { get; }
        public int Hp { get; private set; }
        public VitalState State { get; private set; }
        public float DownedLeft { get; private set; }
        public float ReviveProgress { get; private set; }   // 0..ReviveHoldSeconds

        public PlayerVitals(int maxHp = BaseMaxHp, float def = BaseDef) { MaxHp = Math.Max(1, maxHp); Def = def; Hp = MaxHp; }

        public bool Up => State == VitalState.Up;

        // Returns the HP actually lost. Damage to a downed or out player does nothing. True in `downedNow` when this hit put the player down.
        public int Damage(int amount, out bool downedNow)
        {
            downedNow = false;
            if (State != VitalState.Up || amount <= 0) return 0;
            int lost = Math.Min(Hp, amount);
            Hp -= lost;
            if (Hp == 0)
            {
                State = VitalState.Downed;
                DownedLeft = DownedSeconds;
                ReviveProgress = 0f;
                downedNow = true;
            }
            return lost;
        }

        public int Heal(int amount)
        {
            if (State != VitalState.Up || amount <= 0) return 0;
            int gain = Math.Min(MaxHp - Hp, amount);
            Hp += gain;
            return gain;
        }

        // Ticks the downed timer. `reviverHolding`: an ally is holding the revive within reach this frame (progress otherwise falls back to 0).
        // Returns true on the tick the player is revived.
        public bool Tick(float dt, bool reviverHolding)
        {
            if (State != VitalState.Downed) return false;
            DownedLeft -= dt;
            if (reviverHolding)
            {
                ReviveProgress += dt;
                if (ReviveProgress >= ReviveHoldSeconds)
                {
                    Revive();
                    return true;
                }
            }
            else ReviveProgress = 0f;
            if (DownedLeft <= 0f)
            {
                DownedLeft = 0f;
                State = VitalState.Out;
                ReviveProgress = 0f;
            }
            return false;
        }

        public void Revive()
        {
            State = VitalState.Up;
            Hp = Math.Max(1, (int)Math.Round(MaxHp * ReviveHpFraction));
            DownedLeft = 0f;
            ReviveProgress = 0f;
        }

        // A new dive or a wipe reset: full life, up.
        public void FullReset()
        {
            State = VitalState.Up;
            Hp = MaxHp;
            DownedLeft = 0f;
            ReviveProgress = 0f;
        }

        // The dive ends when no player is up. An empty party counts as not ended (nothing to lose yet).
        public static bool AllDown(IEnumerable<PlayerVitals> party)
        {
            bool any = false;
            foreach (PlayerVitals p in party)
            {
                any = true;
                if (p.Up) return false;
            }
            return any;
        }
    }
}
