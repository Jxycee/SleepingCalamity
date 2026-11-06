// Authority: none (pure EditMode tests). The state -> Animator parameter mapping and the attack-id table (Core HeroAnimMap).
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class HeroAnimMapTests
    {
        [Test]
        public void AttackId_IsUniquePerWeaponAndSlot_AndAirReusesGround()
        {
            var seen = new System.Collections.Generic.HashSet<byte>();
            for (int w = 0; w < HeroAnimMap.WeaponCount; w++)
                for (int slot = 0; slot < HeroAnimMap.SlotsPerWeapon; slot++)
                {
                    byte id = HeroAnimMap.AttackId(w, slot);
                    Assert.That(id, Is.InRange(1, HeroAnimMap.AttackIdCount - 1));
                    Assert.That(seen.Add(id), Is.True, "duplicate id for weapon " + w + " slot " + slot);
                    Assert.That(HeroAnimMap.WeaponOf(id), Is.EqualTo(w));
                    Assert.That(HeroAnimMap.SlotOf(id), Is.EqualTo(slot));
                }
            Assert.That(seen.Count, Is.EqualTo(HeroAnimMap.AttackIdCount - 1));
            Assert.That(HeroAnimMap.AttackId(2, AttackSet.AirHit1), Is.EqualTo(HeroAnimMap.AttackId(2, AttackSet.Hit1)));
            Assert.That(HeroAnimMap.AttackId(0, AttackSet.AirFinisher), Is.EqualTo(HeroAnimMap.AttackId(0, AttackSet.Finisher)));
            Assert.That(HeroAnimMap.AttackId(-1, 0), Is.EqualTo(HeroAnimMap.NoAttack));
            Assert.That(HeroAnimMap.AttackId(0, 0xFF), Is.EqualTo(HeroAnimMap.NoAttack));
        }

        [Test]
        public void AttackProgress_IsClampedFractionOfTheAttack()
        {
            Assert.That(HeroAnimMap.AttackProgress(17f, 34), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(HeroAnimMap.AttackProgress(99f, 34), Is.EqualTo(1f));
            Assert.That(HeroAnimMap.AttackProgress(-3f, 34), Is.EqualTo(0f));
            Assert.That(HeroAnimMap.AttackProgress(5f, 0), Is.EqualTo(0f));
        }

        [Test]
        public void Evaluate_PassesLocomotionThrough_AndClampsStrafe()
        {
            HeroAnimState s = HeroAnimState.FromWorldVelocity(MovementMath.RunSpeed * 2f, -3f, 0f, 90f, false, true, false);
            HeroAnimParams p = HeroAnimMap.Evaluate(in s);
            Assert.That(p.MoveY, Is.EqualTo(1f).Within(1e-4f), "forward, clamped");
            Assert.That(p.Speed, Is.EqualTo(MovementMath.RunSpeed * 2f).Within(1e-3f));
            Assert.That(p.VertSpeed, Is.EqualTo(-3f));
            Assert.That(p.Grounded, Is.False);
            Assert.That(p.Locked, Is.True);
        }

        [Test]
        public void Evaluate_CombatFlagsAndTheAttackLayer()
        {
            var s = new HeroAnimState { Weapon = 2, AttackId = HeroAnimMap.AttackId(2, 1), AttackProgress = 0.4f, Guarding = true, Aiming = true, Grounded = true };
            HeroAnimParams p = HeroAnimMap.Evaluate(in s);
            Assert.That(p.Attack, Is.EqualTo(HeroAnimMap.AttackId(2, 1)));
            Assert.That(p.AttackTime, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(p.Weapon, Is.EqualTo(2));
            Assert.That(p.Guarding && p.Aiming, Is.True);

            var none = new HeroAnimState { Weapon = 9, AttackProgress = 0.7f };
            HeroAnimParams q = HeroAnimMap.Evaluate(in none);
            Assert.That(q.Attack, Is.EqualTo(0));
            Assert.That(q.AttackTime, Is.EqualTo(0f), "no attack, no progress");
            Assert.That(q.Weapon, Is.EqualTo(HeroAnimMap.WeaponCount - 1), "clamped");
        }

        [Test]
        public void Evaluate_Downed_SilencesEverythingElse()
        {
            var s = new HeroAnimState { Downed = true, Dodging = true, Guarding = true, Aiming = true, AttackId = 3, AttackProgress = 0.5f };
            HeroAnimParams p = HeroAnimMap.Evaluate(in s);
            Assert.That(p.Downed, Is.True);
            Assert.That(p.Dodging || p.Guarding || p.Aiming, Is.False);
            Assert.That(p.Attack, Is.EqualTo(0));
            Assert.That(p.AttackTime, Is.EqualTo(0f));
        }
    }
}
