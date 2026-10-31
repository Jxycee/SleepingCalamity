// Not networked. Local, cosmetic events the combat code raises for Presentation (sparks, sounds, damage numbers, the HUD, the camera kick) so Gameplay never
// touches Presentation. Every peer raises them for what it sees: the attacker's client for its own hits (instantly, before the host answers), every peer for
// the host's results. Static, so they are reset on SubsystemRegistration (domain reload may be off).
using System;
using SleepingCalamity.Core.Combat;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    // EnemyTelegraph is no longer raised (each attack's own wind-up, hold and glint sounds come from EnemyVisual); kept so the values after it do not move.
    public enum CombatCue : byte
    {
        Swing, HitNormal, HitFinisher, HitCrit, Blocked, Parry, Dodge, PlayerHurt, Guarded, EnemyTelegraph, EnemySlam, EnemyDie, Heal, Pop, Shot, Spell, Down, Revive, Whiff,
    }

    public readonly struct LocalHit
    {
        public readonly Vector3 Point;
        public readonly AttackData Attack;
        public readonly bool Crit, Part;
        public readonly float Kick;
        public LocalHit(Vector3 point, AttackData attack, bool part, float kick) { Point = point; Attack = attack; Part = part; Kick = kick; Crit = false; }
    }

    public readonly struct DamageNumber
    {
        public readonly Vector3 Position;
        public readonly int Amount;
        public readonly bool Crit, Blocked, OnPlayer, Heal, WeakPart, Heavy;   // Heavy: a finisher or charge (bigger, orange)
        public DamageNumber(Vector3 position, int amount, bool crit, bool blocked, bool onPlayer, bool heal, bool weakPart, bool heavy = false)
        { Position = position; Amount = amount; Crit = crit; Blocked = blocked; OnPlayer = onPlayer; Heal = heal; WeakPart = weakPart; Heavy = heavy; }
    }

    public readonly struct AbsReport
    {
        public readonly string WeaponKey;
        public readonly float Abs;
        public readonly bool TechPop;
        public readonly int LevelsGained;
        public readonly string Why;
        public AbsReport(string weaponKey, float abs, bool techPop, int levelsGained, string why) { WeaponKey = weaponKey; Abs = abs; TechPop = techPop; LevelsGained = levelsGained; Why = why; }
    }

    public static class CombatEvents
    {
        public static event Action<HeroCombat, AttackData> SwingStarted;
        public static event Action<LocalHit> LocalHitLanded;           // the attacker's own spark/hitstop moment
        public static event Action<CombatCue, Vector3> Cue;            // a sound/effect at a place
        public static event Action<DamageNumber> NumberPopped;
        public static event Action<AbsReport> AbsReported;             // owner only: ABS and tech pops (the debug HUD's "ABS gains")
        public static event Action<float> CameraKick;                  // owner only: 0..1
        public static event Action<Vector3, Vector3, bool> ArrowFired; // an aimed shot: from, to (the hit point or the end of its range), whether it hit; every peer
        public static event Action<HeroCombat, bool> HeroHurt;         // a hero was hurt (guarded or not): the flinch; every peer
        public static event Action<Vector3> HurtFrom;                  // owner only: world direction (flat) from the local hero toward what hurt it (the screen-edge flash)

        public static void RaiseSwing(HeroCombat hero, AttackData attack) => SwingStarted?.Invoke(hero, attack);
        public static void RaiseLocalHit(in LocalHit hit) => LocalHitLanded?.Invoke(hit);
        public static void RaiseCue(CombatCue cue, Vector3 at) => Cue?.Invoke(cue, at);
        public static void RaiseNumber(in DamageNumber n) => NumberPopped?.Invoke(n);
        public static void RaiseAbs(in AbsReport r) => AbsReported?.Invoke(r);
        public static void RaiseKick(float strength) => CameraKick?.Invoke(strength);
        public static void RaiseArrow(Vector3 from, Vector3 to, bool hit) => ArrowFired?.Invoke(from, to, hit);
        public static void RaiseHeroHurt(HeroCombat hero, bool guarded) => HeroHurt?.Invoke(hero, guarded);
        public static void RaiseHurtFrom(Vector3 towardSource) => HurtFrom?.Invoke(towardSource);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SwingStarted = null;
            LocalHitLanded = null;
            Cue = null;
            NumberPopped = null;
            AbsReported = null;
            CameraKick = null;
            ArrowFired = null;
            HeroHurt = null;
            HurtFrom = null;
        }
    }
}
