// Authority: none (EditMode tests). One test per combat fix from the alpha playtest (Reference/qa/playtest/BUGS.md items 2, 6, 7, 8, 15, 16).
using System.Reflection;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.EditMode
{
    public class CombatFixesTests
    {
        // Item 6: after the ledger's parry cap is spent, parries on one enemy pay the capped tech base and then nothing.
        [Test]
        public void Parry_OnOneEnemy_StopsPaying()
        {
            var p = new PlayerGrowth("a");
            float total = 0f;
            AbsGain last = default;
            for (int i = 0; i < 60; i++)
            {
                last = CombatRewards.OnParry(p, WeaponCatalog.DaggerShield, "rat1");
                total += last.Abs;
            }
            Assert.That(last.Abs, Is.EqualTo(0f), "the 60th parry on the same enemy pays nothing");
            Assert.That(last.TechPop, Is.False, "and no pop");
            Assert.That(total, Is.LessThan(20f), "the whole farm is bounded");
            Assert.That(p.Weapon(WeaponCatalog.DaggerShield).Abs, Is.EqualTo(total).Within(1e-3f));
        }

        // Item 7: a press buffered before a dodge or stagger must not start a swing when the interrupt ends.
        [Test]
        public void Interrupt_ClearsTheBufferedPress()
        {
            var m = new AttackMachine(AttackCatalog.Axe);
            var press = new AttackInput { Pressed = true, Held = true };
            Assert.That(m.Tick(0.016f, press), Is.True, "the first press swings");
            m.Tick(0.016f, press);   // a second press mid-swing: buffered
            m.Interrupt();           // the dodge
            bool started = false;
            for (int i = 0; i < 30; i++) started |= m.Tick(0.016f, default);
            Assert.That(started, Is.False, "no swing began by itself");
            Assert.That(m.Active, Is.False);
        }

        // Item 15: a raised guard must not eat the dodge press.
        [Test]
        public void Guarding_DoesNotBlockTheDodge()
        {
            var go = new GameObject("HeroGuardDodge");
            try
            {
                var hc = go.AddComponent<HeroCombat>();
                typeof(HeroCombat).GetField("_guarding", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hc, true);
                Assert.That(hc.AllowsDodge, Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // Item 2: melee needs a clear line between the two chests; a Structure collider in between blocks it.
        [Test]
        public void MeleeSight_WallBlocks_OpenAirDoesNot()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.layer = Layers.Structure;
                wall.transform.position = new Vector3(0f, 1f, 2f);
                wall.transform.localScale = new Vector3(6f, 3f, 0.4f);
                Physics.SyncTransforms();
                Assert.That(MeleeSight.Clear(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f)), Is.False, "wall between");
                Assert.That(MeleeSight.Clear(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f)), Is.True, "same side");
                Assert.That(MeleeSight.Clear(new Vector3(9f, 0f, 0f), new Vector3(9f, 0f, 4f)), Is.True, "round the end of the wall");
                wall.layer = Layers.Default;
                Physics.SyncTransforms();
                Assert.That(MeleeSight.Clear(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f)), Is.True, "only Structure blocks");
            }
            finally { Object.DestroyImmediate(wall); }
        }

        // Item 16: seeding an enemy before Spawn writes no NetworkVariable (NGO warns for that) and still carries the seeded values.
        [Test]
        public void ServerSeed_BeforeSpawn_LogsNoNetworkVariableWarning()
        {
            var go = new GameObject("EnemySeed");
            try
            {
                var e = go.AddComponent<Enemy>();
                e.ServerSeed(EnemyKind.RatKing, 1, 1);
                LogAssert.NoUnexpectedReceived();
                Assert.That(e.NetKind.Value, Is.EqualTo((byte)EnemyKind.RatKing));
                Assert.That(e.NetMaxHp.Value, Is.EqualTo(e.Model.MaxHp));
                Assert.That(e.NetHp.Value, Is.EqualTo(e.Model.Hp));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
