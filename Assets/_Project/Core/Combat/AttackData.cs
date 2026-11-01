// Core/Combat/AttackData.cs: an attack as data with frame windows (docs/PHASE1.md 6.1, KH). Pure C#, no engine.
// Every number in the catalogs that use these types is a P9 Placeholder: tuning is a data edit, never a code change. Frames are 60 Hz.
// Authority: none (static data; the owner reads it to swing, the host reads it to judge a hit intent, enemies use the same type).
using System;

namespace SleepingCalamity.Core.Combat
{
    // A half-open frame range [Start, End). An empty window (End <= Start) never contains a frame.
    public readonly struct FrameWindow
    {
        public readonly int Start, End;
        public FrameWindow(int start, int end) { Start = start; End = end; }
        public bool IsEmpty => End <= Start;
        public bool Contains(float frame) => frame >= Start && frame < End;
        public bool Contains(int frame) => frame >= Start && frame < End;
        public static readonly FrameWindow None = new FrameWindow(0, 0);
        public override string ToString() => IsEmpty ? "none" : Start + "-" + End;
    }

    public enum AttackKind : byte
    {
        Normal = 0,     // hit 1 and hit 2 of a combo (hit 1 is the "opener" for revenge)
        Finisher = 1,   // the combo finisher: +10% per prior hit, knockback, revenge 3
        Charge = 2,     // after a 0.6 s hold
        Shot = 3,       // the bow's aimed shot: a ray, not a swing
        Spell = 4,      // the staff's pip spell
        Enemy = 5,
    }

    public enum HitShape : byte { Cone = 0, Circle = 1, Ray = 2 }

    // Where an attack is centred when its hitbox is evaluated.
    public enum HitOrigin : byte
    {
        Self = 0,           // the attacker's own position, facing along its yaw
        GroundAtTarget = 1, // an enemy's lobbed attack: the spot the target stood on when the attack began (replicated with the attack)
    }

    public sealed class AttackData
    {
        public const int FrameRate = 60;

        public readonly string Id, Name;
        public readonly AttackKind Kind;
        public readonly int TotalFrames;
        public readonly FrameWindow HitOn;            // the hitbox is live
        public readonly FrameWindow ComboOpen;        // a press here chains into the next normal hit
        public readonly FrameWindow FinisherAllowed;  // a press here chains into the finisher
        public readonly int CancelFrom;               // from this frame on the attack can be cancelled into the next attack or a dodge
        public readonly FrameWindow IFrames;          // the attacker cannot be hit (rare: dodge-attacks, charge armour)
        public readonly FrameWindow SuperArmor;       // the attacker is not staggered by hits
        public readonly FrameWindow Turn;             // the attacker turns toward its target at TurnDegPerSec
        public readonly float TurnDegPerSec;
        public readonly FrameWindow Lunge;            // the attacker slides forward LungeMeters over this window
        public readonly float LungeMeters;
        public readonly float Multiplier;             // the move multiplier in the damage formula (6.5)
        public readonly HitShape Shape;
        public readonly HitOrigin Origin;
        public readonly float Range;                  // cone / ray length, or the circle radius (m)
        public readonly float HalfAngleDeg;           // cone half angle
        public readonly float HeightBand;             // +- metres around the attacker's feet
        public readonly float Knockback;              // metres the victim is pushed (halved on a heavy enemy by its own rules)
        public readonly float Revenge;                // what a hit adds to the revenge gauge (opener 1, finisher 3, charge 4)
        public readonly int HitstopFrames;            // cosmetic, attacker side
        public readonly float CameraKick;             // cosmetic, attacker side (0..1)
        public readonly bool GuardBreak;              // passes a guard and a broom's bristles
        public readonly bool Guardable;               // enemy attacks only: a guard or parry can answer it
        public readonly bool Fire;                    // spells: the hit carries the staff's element
        public readonly int SpellPips;                // pips it costs
        public readonly EnemyTell Tell;               // enemy attacks: how the body stages the wind-up (EnemyTell); null for hero attacks
        public readonly float Poise;                  // hero attacks: what a hit adds to the enemy's poise count (PoiseMeter); defaults to Revenge
        public readonly TokenCategory? TokenPool;     // enemy attacks: which attack-token pool this attack draws from; null = the enemy's own default (EnemyDefinition.CategoryOf)

        public AttackData(string id, string name, AttackKind kind, int totalFrames, FrameWindow hitOn, float multiplier, HitShape shape, float range,
            float halfAngleDeg, FrameWindow comboOpen = default, FrameWindow finisherAllowed = default, int cancelFrom = -1, FrameWindow iFrames = default,
            FrameWindow superArmor = default, FrameWindow turn = default, float turnDegPerSec = 0f, FrameWindow lunge = default, float lungeMeters = 0f,
            float heightBand = 2f, float knockback = 0f, float revenge = 1f, int hitstopFrames = 4, float cameraKick = 0.3f, bool guardBreak = false,
            bool guardable = true, bool fire = false, int spellPips = 0, HitOrigin origin = HitOrigin.Self, EnemyTell tell = null, float poise = -1f, TokenCategory? tokenPool = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("an attack needs an id", nameof(id));
            Id = id;
            Name = name ?? id;
            Kind = kind;
            TotalFrames = Math.Max(1, totalFrames);
            HitOn = hitOn;
            ComboOpen = comboOpen;
            FinisherAllowed = finisherAllowed;
            CancelFrom = cancelFrom < 0 ? Math.Max(hitOn.End, TotalFrames) : cancelFrom;   // default: no early cancel
            IFrames = iFrames;
            SuperArmor = superArmor;
            Turn = turn;
            TurnDegPerSec = turnDegPerSec;
            Lunge = lunge;
            LungeMeters = lungeMeters;
            Multiplier = multiplier;
            Shape = shape;
            Origin = origin;
            Range = range;
            HalfAngleDeg = halfAngleDeg;
            HeightBand = heightBand;
            Knockback = knockback;
            Revenge = revenge;
            HitstopFrames = hitstopFrames;
            CameraKick = cameraKick;
            GuardBreak = guardBreak;
            Guardable = guardable;
            Fire = fire;
            SpellPips = spellPips;
            Tell = tell;
            Poise = poise >= 0f ? poise : revenge;
            TokenPool = tokenPool;
        }

        public float TotalSeconds => TotalFrames / (float)FrameRate;
        public bool IsFinisher => Kind == AttackKind.Finisher;

        // The same attack in the air: quicker (x0.85 frames), a touch weaker (x0.9), and the revenge values of KH's air hits (1.5, finisher 3.5). Poise is
        // the ground attack's.
        public AttackData AsAir()
        {
            int Scale(int f) => (int)Math.Round(f * 0.85);
            FrameWindow ScaleW(FrameWindow w) => w.IsEmpty ? w : new FrameWindow(Scale(w.Start), Math.Max(Scale(w.Start) + 1, Scale(w.End)));
            return new AttackData(Id + "_air", Name + " (air)", Kind, Scale(TotalFrames), ScaleW(HitOn), Multiplier * 0.9f, Shape, Range, HalfAngleDeg,
                ScaleW(ComboOpen), ScaleW(FinisherAllowed), CancelFrom >= TotalFrames ? Scale(TotalFrames) : Scale(CancelFrom), ScaleW(IFrames),
                ScaleW(SuperArmor), ScaleW(Turn), TurnDegPerSec, FrameWindow.None, 0f, HeightBand + 1f, Knockback, Kind == AttackKind.Finisher ? 3.5f : 1.5f,
                HitstopFrames, CameraKick, GuardBreak, Guardable, Fire, SpellPips, Origin, poise: Poise);
        }
    }

    // The attacks of one weapon family. Index layout (the hit intent sends the index): 0..2 the ground chain (hit 1, hit 2, finisher), 3 the charge,
    // 4 the special (the bow's shot or the staff's spell; null for the others), 5..7 the air chain.
    public sealed class AttackSet
    {
        public const int Hit1 = 0, Hit2 = 1, Finisher = 2, Charge = 3, Special = 4, AirHit1 = 5, AirHit2 = 6, AirFinisher = 7, Count = 8;

        public readonly string Name;
        public readonly AttackData[] Attacks = new AttackData[Count];

        public AttackSet(string name, AttackData hit1, AttackData hit2, AttackData finisher, AttackData charge, AttackData special)
        {
            Name = name;
            Attacks[Hit1] = hit1;
            Attacks[Hit2] = hit2;
            Attacks[Finisher] = finisher;
            Attacks[Charge] = charge;
            Attacks[Special] = special;
            Attacks[AirHit1] = hit1.AsAir();
            Attacks[AirHit2] = hit2.AsAir();
            Attacks[AirFinisher] = finisher.AsAir();
        }

        public AttackData Get(int index) => index >= 0 && index < Count ? Attacks[index] : null;

        // The next step of a chain: ground 0 -> 1 -> 2, air 5 -> 6 -> 7. -1 after the finisher.
        public static int NextInChain(int index)
        {
            switch (index)
            {
                case Hit1: return Hit2;
                case Hit2: return Finisher;
                case AirHit1: return AirHit2;
                case AirHit2: return AirFinisher;
                default: return -1;
            }
        }

        public static bool IsChainStart(int index) => index == Hit1 || index == AirHit1;
    }
}
