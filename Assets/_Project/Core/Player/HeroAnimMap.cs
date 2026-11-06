// Core, no engine references, unit-tested. The pure mapping from a HeroAnimState to the Animator's parameters, and the table that turns "this weapon,
// this attack index" into an attack-animation id. The Animator controller (built by HeroAnimatorBuilder, which reads the same names and ids) is the
// only other place these numbers appear, and an EditMode test keeps the two in step.
using System;

namespace SleepingCalamity.Core
{
    // The Animator parameter names, in one place so the driver, the builder and the tests agree.
    public static class HeroAnimParam
    {
        public const string Speed = "Speed", MoveX = "MoveX", MoveY = "MoveY", VertSpeed = "VertSpeed";
        public const string Grounded = "Grounded", Locked = "Locked", Dodging = "Dodging", Guarding = "Guarding", Aiming = "Aiming", Downed = "Downed";
        public const string Attack = "Attack", AttackTime = "AttackTime", DodgeTime = "DodgeTime", Weapon = "Weapon";
        public const string Hurt = "Hurt", HurtHeavy = "HurtHeavy";   // triggers
    }

    public struct HeroAnimParams
    {
        public float Speed, MoveX, MoveY, VertSpeed, AttackTime;
        public bool Grounded, Locked, Dodging, Guarding, Aiming, Downed;
        public int Weapon, Attack;
    }

    public static class HeroAnimMap
    {
        public const byte NoAttack = 0;
        public const int WeaponCount = 4, SlotsPerWeapon = 5, AttackIdCount = WeaponCount * SlotsPerWeapon + 1;   // id 0 = none

        // Weapon index (AttackCatalog: 0 axe, 1 staff, 2 dagger, 3 bow) and attack index (AttackSet: 0..4 ground, 5..7 air) -> a stable id 1..20, or 0.
        // The air chain reuses the ground clips until a real air attack arrives.
        public static byte AttackId(int weapon, int attackIndex)
        {
            if (weapon < 0 || weapon >= WeaponCount || attackIndex < 0 || attackIndex > 7) return NoAttack;
            int slot = attackIndex >= 5 ? attackIndex - 5 : attackIndex;
            return (byte)(1 + weapon * SlotsPerWeapon + slot);
        }

        public static int WeaponOf(byte attackId) => attackId == 0 ? -1 : (attackId - 1) / SlotsPerWeapon;
        public static int SlotOf(byte attackId) => attackId == 0 ? -1 : (attackId - 1) % SlotsPerWeapon;

        // Progress through an attack, 0..1 (the attack states play their clip by this, not by wall time, so the clip tracks the combat clock).
        public static float AttackProgress(float frame, int totalFrames) => totalFrames <= 0 ? 0f : Math.Clamp(frame / totalFrames, 0f, 1f);

        public static HeroAnimParams Evaluate(in HeroAnimState s)
        {
            return new HeroAnimParams
            {
                Speed = Math.Max(0f, s.Speed),
                MoveX = Math.Clamp(s.StrafeX, -1f, 1f),
                MoveY = Math.Clamp(s.StrafeZ, -1f, 1f),
                VertSpeed = s.VerticalSpeed,
                Grounded = s.Grounded,
                Locked = s.Locked,
                Dodging = s.Dodging && !s.Downed,
                Guarding = s.Guarding && !s.Downed,
                Aiming = s.Aiming && !s.Downed,
                Downed = s.Downed,
                Weapon = Math.Clamp((int)s.Weapon, 0, WeaponCount - 1),
                Attack = s.Downed ? NoAttack : s.AttackId,
                AttackTime = s.Downed || s.AttackId == NoAttack ? 0f : Math.Clamp(s.AttackProgress, 0f, 1f),
            };
        }
    }
}
