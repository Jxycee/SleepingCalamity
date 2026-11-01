// Core/Combat/EnemyModel.cs: the four greybox enemies as data, and the host's per-enemy combat model: HP, tier and party scaling, the broom's bristles,
// rage, the elite's revenge gauge and breakable parts, resolved in one tested function (docs/PHASE1.md 6.5, 6.6). Pure C#.
// Authority: host. The Unity Enemy component owns one EnemyModel and replicates the results; clients never run it.
using System;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Core.Combat
{
    public enum EnemyKind : byte { Slime = 0, RatKing = 1, Broom = 2, DoughWarden = 3 }

    public sealed class PartDefinition
    {
        public readonly string Id;
        public readonly int Hp;
        public readonly float WeakMultiplier;
        public readonly float Height, Radius;     // where the part sits on the body, for picking it with a swing (centre height above the feet, and size)
        public readonly float OffsetForward, OffsetRight;   // in the enemy's local frame
        public PartDefinition(string id, int hp, float weakMultiplier, float height, float radius, float offsetForward, float offsetRight)
        { Id = id; Hp = hp; WeakMultiplier = weakMultiplier; Height = height; Radius = radius; OffsetForward = offsetForward; OffsetRight = offsetRight; }
    }

    public sealed class EnemyDefinition
    {
        public readonly EnemyKind Kind;
        public readonly string Id, Name;
        public readonly Species Species;      // for WeaponGrowth.SpeciesValue
        public readonly int Hp;
        public readonly float Atk, Def;
        public readonly float MoveSpeed, Radius, Height;
        public readonly bool IsElite, CanRage, FrontalBlock, SuperArmorAlways, WeakToFire, IsPest;
        public readonly float StaggerSeconds;   // how long a flinch lasts when not armoured
        public readonly float VisualReach;      // metres from the enemy's centre to the farthest point of its greybox body in front of it (arm, snout, broom length)
        public readonly float PoiseThreshold;   // solo poise to flinch it (PoiseMeter); ignored while it is armoured
        public readonly bool Ripostes;          // its blocked front hits count toward the bristle riposte (GuardCounter)
        public readonly AttackData[] Attacks;
        public readonly PartDefinition[] Parts;
        public readonly float AggroRange, LeashRange, RetargetInterval;   // threat targeting (ThreatTable; docs/research/enemy-ai.md 10.2): notice within Aggro, drop beyond Leash, re-score at most this often
        public readonly TokenCategory TokenCategory;   // the attack-token pool its attacks draw from unless an attack names its own (AttackData.TokenPool; docs/research/enemy-ai.md 2.3, 5)

        public EnemyDefinition(EnemyKind kind, string id, string name, Species species, int hp, float atk, float def, float moveSpeed, float radius, float height,
            AttackData[] attacks, bool isElite = false, bool canRage = true, bool frontalBlock = false, bool superArmorAlways = false, bool weakToFire = false,
            bool isPest = false, float staggerSeconds = 0.4f, PartDefinition[] parts = null, float visualReach = 1f, float poiseThreshold = 0f, bool ripostes = false,
            TokenCategory tokenCategory = TokenCategory.Melee, float aggroRange = ThreatTuning.DefaultAggroRange, float leashRange = ThreatTuning.DefaultLeashRange,
            float retargetInterval = ThreatTuning.DefaultRetargetInterval)
        {
            TokenCategory = tokenCategory;
            AggroRange = aggroRange; LeashRange = leashRange; RetargetInterval = retargetInterval;
            VisualReach = visualReach;
            PoiseThreshold = poiseThreshold;
            Ripostes = ripostes;
            Kind = kind; Id = id; Name = name; Species = species; Hp = hp; Atk = atk; Def = def; MoveSpeed = moveSpeed; Radius = radius; Height = height;
            Attacks = attacks; IsElite = isElite; CanRage = canRage; FrontalBlock = frontalBlock; SuperArmorAlways = superArmorAlways; WeakToFire = weakToFire;
            IsPest = isPest; StaggerSeconds = staggerSeconds; Parts = parts ?? Array.Empty<PartDefinition>();
        }

        // The pool one attack draws from: its own, else the enemy's default.
        public TokenCategory CategoryOf(AttackData attack) => attack != null && attack.TokenPool.HasValue ? attack.TokenPool.Value : TokenCategory;
    }

    public static class EnemyCatalog
    {
        // Tier 2 (floors 3-4): life x3 and defence x3.3 are the DC2 family ratio. Attack was x2.3 (a x3.5 step from tier 1 after the first playtest): against
        // the 100 HP, DEF 10 hero that is a 26-point slime hop and a 113-point Warden pound, a one-shot. DC1's early enemies take 19-46% of the hero's HP per
        // hit, typically 25% (docs/research/combat-timings.md row 22). Now x1.0, a x1.54 step: slime hop 8 -> 11, rat-king spin 14 -> 22, broom sweep 11 -> 19,
        // Warden slam 27 -> 41 and pound 32 -> 50 (the elite's big hits sit at the top of DC1's band). Tier 2 presses through pacing instead: faster
        // cooldowns, a tighter strike gap (AttackPacing.Tier2) and mixed rosters (ArenaWaves). P9 Placeholder.
        public const float Tier2Life = 3f, Tier2Atk = 1.0f, Tier2Def = 3.3f;

        // First playtest (2026-10-30): tier 1 hit too hard to read the fight. Tier 1 attack is x0.65 of the 6.6 table (slime 12 -> 7.8, rat-king 18 -> 11.7,
        // broom 15 -> 9.75, Warden 30 -> 19.5). P9 Placeholder.
        public const float Tier1Atk = 0.65f;

        // 6.6 tier 1 stats. Placeholder: which are weak to fire (the jam, the wooden broom) and which are pests (the rat-king).
        // Combat depth (2026-10-30, "fewer, tougher enemies"): small-enemy life roughly doubles so each takes a few openings (a rat-king about three with
        // the axe): slime 40 -> 80, rat-king 90 -> 180, broom 60 -> 160; the Warden stays 600. Poise thresholds: slime 2, rat-king 4, broom 5.
        public static readonly EnemyDefinition Slime = new EnemyDefinition(EnemyKind.Slime, "slime", "Jam-jar slime", Species.Slime, 80, 12f, 5f, 2.6f, 0.55f, 1.0f,
            new[] { AttackCatalog.SlimeHop, AttackCatalog.SlimeGlob }, weakToFire: true, staggerSeconds: 0.5f, visualReach: 0.5f, poiseThreshold: 2f, tokenCategory: TokenCategory.Ranged);
        public static readonly EnemyDefinition RatKing = new EnemyDefinition(EnemyKind.RatKing, "rat_king", "Rat-king", Species.RatKing, 180, 18f, 10f, 4.6f, 0.5f, 1.2f,
            new[] { AttackCatalog.RatLunge, AttackCatalog.RatSpin }, isPest: true, staggerSeconds: 0.35f, visualReach: 0.85f, poiseThreshold: 4f);
        public static readonly EnemyDefinition Broom = new EnemyDefinition(EnemyKind.Broom, "broom", "Sweeping broom", Species.Broom, 160, 15f, 15f, 2.3f, 0.55f, 1.9f,
            new[] { AttackCatalog.BroomSweep, AttackCatalog.BroomPoke }, frontalBlock: true, weakToFire: true, staggerSeconds: 0.45f, visualReach: 1.9f, poiseThreshold: 5f, ripostes: true);
        public static readonly EnemyDefinition DoughWarden = new EnemyDefinition(EnemyKind.DoughWarden, "dough_warden", "Dough Warden", Species.Elite, 600, 30f, 20f, 2.4f, 1.0f, 2.8f,
            new[] { AttackCatalog.WardenSlam, AttackCatalog.WardenSweep, AttackCatalog.WardenPound, AttackCatalog.WardenQuake }, isElite: true, canRage: false, superArmorAlways: true, staggerSeconds: 0.9f, visualReach: 2.4f, tokenCategory: TokenCategory.Heavy,
            parts: new[]
            {
                new PartDefinition("rolling_pin", 70, 1.5f, 1.4f, 0.5f, 0.2f, 1.3f),    // the arm that slams: reached from its right flank
                new PartDefinition("dough_crown", 70, 1.5f, 2.9f, 0.5f, 0.0f, 0.0f),    // the head, above the ground swings: air attacks and arrows
            });

        public static readonly EnemyDefinition[] All = { Slime, RatKing, Broom, DoughWarden };

        public static EnemyDefinition Get(EnemyKind kind) => All[(int)kind];
        public static EnemyDefinition Get(int kind) => All[Math.Max(0, Math.Min(All.Length - 1, kind))];
    }

    public struct EnemyHitInput
    {
        public AttackData Attack;
        public float Atk, Fire, Pest;        // the attacker's weapon
        public int PriorChainHits;           // connected hits earlier in the attacker's chain (the finisher bonus)
        public float BearingToAttackerDeg;   // angle between the enemy's facing and the direction to the attacker (0 = in front)
        public int PartIndex;                // -1 for the body
        public float CritRoll;               // [0,1), from the host's RNG
        public float CritChance;             // normally DamageFormula.CritChance
        public bool EnemyHasSuperArmor;      // the enemy is inside a super-armour window of its own action right now
        public bool EnemyCommitted;          // the enemy is between its attack's glint and the end of its hitbox (EnemyWindows.Committed): no flinch
        public bool EnemyOpen;               // the enemy is open (recovering after its strike, stunned, flinching): x1.5 damage, double poise
        public bool EnemyStaggered;          // the enemy is already flinching: the hit cannot flinch it again (and adds no poise)
        public bool EnemyAttacking;          // the enemy is inside one of its own attacks: blocked hits do not count toward a riposte it could not start
        public float AbilityDamageBonus;     // hero abilities (AbilityEffects): the whole hit x(1 + this); 0 = none
        public float AbilityFire;            // hero abilities: added to Fire
    }

    public struct EnemyHitResult
    {
        public int Damage;
        public bool Crit, Killed, Blocked;
        public bool ElementMatch, WeakPart, GuardBreakHit;
        public int PartBroken;               // index of the part this hit broke, or -1
        public bool RevengeTriggered, RageStarted;
        public bool Staggered;               // the hit interrupts the enemy's action (its poise broke or its guard was broken; never while armoured or committed)
        public float Knockback;              // metres, already reduced by the victim's armour rules (only a flinching enemy is pushed)
        public bool Opening;                 // landed on an open enemy (paid x1.5)
        public bool PoiseBroken;             // this hit filled the poise count
        public bool GuardBroken;             // a guard-break hit on a guarding front (the broom): a GuardBreakSeconds flinch
        public bool Braced;                  // the enemy's guard took the hit (the broom braces)
        public bool RiposteTriggered;        // this blocked hit completed the guard count: the enemy should riposte
    }

    public sealed class EnemyModel
    {
        public const float SpellFireValue = 10f;   // Placeholder: the staff's spell counts as a Fire 10 weapon (+20% on a fire-weak enemy)

        public readonly EnemyDefinition Def;
        public readonly int Tier;
        public readonly int Players;
        public readonly int MaxHp;
        public readonly float Atk, DefStat;
        public int Hp { get; private set; }
        public bool Alive => Hp > 0;
        public readonly RageTracker Rage = new RageTracker();
        public readonly RevengeGauge Revenge;
        public readonly BossPart[] Parts;
        public readonly PoiseMeter Poise;
        public readonly GuardCounter Guard = new GuardCounter();

        public EnemyModel(EnemyDefinition def, int tier = 1, int players = 1)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Tier = tier >= 2 ? 2 : 1;
            Players = CoopScaling.ClampPlayers(players);
            float life = Tier == 2 ? EnemyCatalog.Tier2Life : 1f;
            MaxHp = (int)MathF.Ceiling(def.Hp * life * CoopScaling.LifeMultiplier(Players) - 1e-4f);
            Atk = def.Atk * (Tier == 2 ? EnemyCatalog.Tier2Atk : EnemyCatalog.Tier1Atk);   // attack is unchanged by the party size (P23)
            DefStat = def.Def * (Tier == 2 ? EnemyCatalog.Tier2Def : 1f);
            Hp = MaxHp;
            Revenge = new RevengeGauge(Players);
            Poise = new PoiseMeter(def.PoiseThreshold, Players);
            Parts = new BossPart[def.Parts.Length];
            for (int i = 0; i < Parts.Length; i++) Parts[i] = new BossPart(def.Parts[i].Id, def.Parts[i].Hp, def.Parts[i].WeakMultiplier);
        }

        // The damage this enemy's attack does to a player with `playerDef`, rage included. Guard and dodge are applied by the victim (GuardMath).
        public int AttackDamage(AttackData attack, float playerDef)
        {
            var i = DamageInput.Basic(Atk, attack.Multiplier, playerDef);
            i.Other = Rage.AttackMultiplier;
            return DamageFormula.Compute(i);
        }

        public EnemyHitResult ApplyHit(in EnemyHitInput h)
        {
            var r = new EnemyHitResult { PartBroken = -1 };
            if (!Alive || h.Attack == null) return r;
            AttackData a = h.Attack;

            // Per-part multiplier (the weak point) and the part's own HP. The part wears down by the damage it would have dealt without the multiplier.
            float partMult = 1f;
            BossPart part = h.PartIndex >= 0 && h.PartIndex < Parts.Length ? Parts[h.PartIndex] : null;
            if (part != null)
            {
                partMult = part.BodyMultiplier;
                r.WeakPart = !part.Broken;
            }

            float block = FrontalBlock.Multiplier(Def.FrontalBlock, h.BearingToAttackerDeg, a.GuardBreak);
            r.Blocked = block < 1f;
            r.Opening = h.EnemyOpen;
            r.GuardBreakHit = Def.FrontalBlock && a.GuardBreak && MathF.Abs(h.BearingToAttackerDeg) <= FrontalBlock.HalfAngleDeg;

            bool crit = DamageFormula.RollCrit(h.CritRoll, h.CritChance > 0f ? h.CritChance : 0f);
            float fire = h.Fire + h.AbilityFire + (a.Fire ? SpellFireValue : 0f);
            float element = DamageFormula.ElementBonus(fire, Def.WeakToFire);
            r.ElementMatch = element > 0f;
            var input = new DamageInput
            {
                Atk = h.Atk,
                Move = DamageFormula.MoveMultiplier(a.Multiplier, a.IsFinisher, h.PriorChainHits),
                Def = DefStat,
                ElementBonus = element,
                Tag = DamageFormula.TagMultiplier(h.Pest, Def.IsPest),
                Part = partMult,
                Crit = crit,
                Other = block * (h.EnemyOpen ? EnemyWindows.OpeningDamage : 1f) * (1f + h.AbilityDamageBonus),
            };
            int dmg = DamageFormula.Compute(input);
            r.Crit = crit;
            r.Damage = Math.Min(dmg, Hp);
            Hp -= r.Damage;
            r.Killed = Hp <= 0;

            if (part != null && !part.Broken)
            {
                // The part takes the hit's damage before the weak-point multiplier.
                int partDmg = partMult > 0f ? (int)MathF.Ceiling(dmg / partMult) : dmg;
                if (part.Damage(partDmg)) r.PartBroken = h.PartIndex;
            }

            if (!r.Killed)
            {
                if (Def.IsElite)
                {
                    if (Revenge.Add(a.Revenge)) r.RevengeTriggered = true;
                }
                // Rage counts only hits outside openings: mashing makes an enemy angrier, punishing it at the right moment never does.
                else if (Def.CanRage && !h.EnemyOpen && Rage.OnHit()) r.RageStarted = true;

                // Armoured (the elite, a super-armour window) or committed (from its glint to the end of its hitbox): the hit lands but never flinches it,
                // and adds no poise. Swinging into a committed attack trades.
                bool armoured = Def.SuperArmorAlways || h.EnemyHasSuperArmor || h.EnemyCommitted;
                bool canFlinch = !armoured && !h.EnemyStaggered;
                if (r.Blocked && Def.Ripostes && canFlinch)
                {
                    r.Braced = true;
                    if (!h.EnemyAttacking && Guard.OnBlocked()) r.RiposteTriggered = true;
                }
                r.GuardBroken = r.GuardBreakHit && canFlinch;
                if (canFlinch && !r.Blocked)
                    r.PoiseBroken = Poise.Add(a.Poise * (h.EnemyOpen ? EnemyWindows.OpeningPoise : 1f));
                r.Staggered = canFlinch && (r.PoiseBroken || r.GuardBroken);
                r.Knockback = r.Staggered ? a.Knockback : 0f;
            }
            return r;
        }

        // A parry stuns the attacker for a moment (the dagger's answer to a telegraph). The elite's armour does not stop it: a parry is not a hit.
        public bool ParryStuns => Alive;

        public void Tick(float dt)
        {
            Rage.Tick(dt);
            Poise.Tick(dt);
            Guard.Tick(dt);
        }
    }
}
