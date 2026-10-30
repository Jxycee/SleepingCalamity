// Authority: test-only. The M3 combat arena through the REAL flow (solo start with the dev switch on, Arena_Combat loaded by the host's scene manager):
//  1. The wave director spawns wave 1 (slimes) and the enemy AI chases and hits the hero: telegraphed attacks reach the owner's judging, HP drops on the host.
//  2. A kill pays ABS on the weapon that made it (WeaponGrowth species value), and the owner's growth mirror shows it.
//  3. The Dough Warden's rolling-pin arm breaks once and drops one healing pickup that a hurt hero walks into (host heals).
// Catches: waves that never spawn, enemies that never attack, a kill that pays nobody, a part that drops healing twice or never, a pickup that does not heal.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class CombatArenaTests
    {
        private const float Timeout = 25f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneFlow.DevCombatArena = false;
            yield return PlayModeHarness.TearDownApp();
        }

        private static IEnumerator StartCombatSolo()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.DevCombatArena = true;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.ArenaCombat);
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        [UnityTest]
        public IEnumerator Solo_WaveOne_Spawns_AndTheEnemiesChaseAndHurtAnIdleHero()
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            var hero = app.NetworkManager.LocalClient.PlayerObject;
            HeroVitals vitals = hero.GetComponent<HeroVitals>();

            yield return PlayModeHarness.WaitUntil(() => Enemy.All.Count >= 4, Timeout, "wave 1 never spawned its four slimes");
            int slimes = 0;
            foreach (Enemy e in Enemy.All) if (e.Def.Kind == EnemyKind.Slime) slimes++;
            Assert.That(slimes, Is.EqualTo(4), "wave 1 is four jam-jar slimes (solo)");

            int attacks = 0;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                attacks = 0;
                foreach (Enemy e in Enemy.All) if (e.Serial > 0) attacks++;
                return attacks > 0;
            }, Timeout, "no enemy ever started an attack");
            yield return PlayModeHarness.WaitUntil(() => vitals.Hp < vitals.MaxHp, Timeout, "an idle hero was never hurt by the wave (judging / host damage)");
            Assert.That(vitals.Model.Hp, Is.EqualTo(vitals.Hp), "the host's record and the replicated HP agree");
        }

        [UnityTest]
        public IEnumerator Solo_AKill_PaysTheSpeciesAbs_OnTheWeaponThatMadeIt()
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            var heroObject = app.NetworkManager.LocalClient.PlayerObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            combat.CritChance = 0f;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);

            var input = new FakeInputService();
            heroObject.GetComponent<HeroLocomotion>().Input = input;
            heroObject.GetComponent<TargetLockController>().Input = input;
            HeroCombat hero = heroObject.GetComponent<HeroCombat>();
            hero.Input = input;
            yield return Seconds(0.6f);

            Enemy slime = combat.SpawnEnemy(EnemyKind.Slime, 1, heroObject.transform.position + heroObject.transform.forward * 1.6f, ai: false);
            Assert.That(slime.Model.MaxHp, Is.EqualTo(40), "solo: life x1.0");
            double until = Time.realtimeSinceStartupAsDouble + 6.0;
            while (slime.Alive && Time.realtimeSinceStartupAsDouble < until)
            {
                input.Frame.PrimaryPressed = true;
                input.Frame.PrimaryHeld = true;
                yield return null;
                input.Frame.PrimaryPressed = false;
                input.Frame.PrimaryHeld = false;
                input.Frame.PrimaryReleased = true;
                yield return null;
                input.Frame.PrimaryReleased = false;
                yield return Seconds(0.12f);
            }
            Assert.That(slime.Alive, Is.False, "two axe hits kill a 40 HP slime");

            ulong cid = app.NetworkManager.LocalClientId;
            WeaponState axe = combat.Growth(cid).Weapon(WeaponCatalog.Axe);
            Assert.That(axe.Abs + axe.Level * 0f, Is.GreaterThanOrEqualTo(4f), "the slime's species value (4) went to the axe");
            Assert.That(combat.Growth(cid).Weapon(WeaponCatalog.Staff).Abs, Is.EqualTo(0f), "and to no other weapon");
            yield return PlayModeHarness.WaitUntil(() => hero.Mirror.Abs >= 4f, 3f, "the owner's growth mirror never showed the ABS");
        }

        // Catches: a guard that does not reduce damage (x0.2), a parry window that is wrong, a parry that does not stun the attacker or pay the dagger's ABS.
        [UnityTest]
        public IEnumerator Solo_DaggerGuard_Reduces_AndAParryStopsTheHit()
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            var heroObject = app.NetworkManager.LocalClient.PlayerObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);
            var input = new FakeInputService();
            heroObject.GetComponent<HeroLocomotion>().Input = input;
            heroObject.GetComponent<TargetLockController>().Input = input;
            HeroCombat hero = heroObject.GetComponent<HeroCombat>();
            hero.Input = input;
            HeroVitals vitals = heroObject.GetComponent<HeroVitals>();
            hero.OwnerSetWeapon(2);   // the dagger and shield
            Assert.That(hero.WeaponKey, Is.EqualTo(WeaponCatalog.DaggerShield));
            yield return Seconds(0.6f);

            Enemy slime = combat.SpawnEnemy(EnemyKind.Slime, 1, heroObject.transform.position + heroObject.transform.forward * 2.0f, ai: false);

            // Guarded: the shield has been up for a while when the nova lands: 11 x 0.2 = 2.2 -> 3.
            input.Frame.SecondaryHeld = true;
            input.Frame.SecondaryPressed = true;
            yield return null;
            input.Frame.SecondaryPressed = false;
            yield return Seconds(0.5f);
            Assert.That(hero.Guarding, Is.True);
            slime.ServerForceAttack(AttackCatalog.TestNova, heroObject.transform.position);
            yield return Seconds(1.3f);
            Assert.That(vitals.Model.Hp, Is.EqualTo(97), "a held guard takes a fifth of the damage, rounded up");

            // Parry: the shield comes up just before the hit, inside the 6-frame window: no damage, the attacker is stunned, the dagger earns its parry ABS.
            input.Frame.SecondaryHeld = false;
            yield return Seconds(0.6f);
            vitals.ServerFullReset();
            int serial = slime.Serial;
            slime.ServerForceAttack(AttackCatalog.TestNova, heroObject.transform.position);
            yield return PlayModeHarness.WaitUntil(() => slime.Serial != serial && slime.Attacking, 2f, "the second attack never started");
            yield return Seconds(0.1f);
            input.Frame.SecondaryHeld = true;
            input.Frame.SecondaryPressed = true;
            yield return null;
            input.Frame.SecondaryPressed = false;
            bool stunned = false;
            double until = Time.realtimeSinceStartupAsDouble + 1.2;
            while (Time.realtimeSinceStartupAsDouble < until) { stunned |= slime.Phase == EnemyPhase.Stagger; yield return null; }
            input.Frame.SecondaryHeld = false;
            Assert.That(vitals.Model.Hp, Is.EqualTo(100), "a parry takes no damage");
            Assert.That(stunned, Is.True, "the parried enemy is stunned");
            Assert.That(combat.Growth(app.NetworkManager.LocalClientId).Weapon(WeaponCatalog.DaggerShield).Abs, Is.EqualTo(4.5f).Within(1e-4f), "the dagger's parry (3) with the tech bonus x1.5");
        }

        [UnityTest]
        public IEnumerator Solo_WardenArm_BreaksOnce_DropsHealing_AndAHurtHeroPicksItUp()
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            var heroObject = app.NetworkManager.LocalClient.PlayerObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            combat.CritChance = 0f;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);
            HeroCombat hero = heroObject.GetComponent<HeroCombat>();
            HeroVitals vitals = heroObject.GetComponent<HeroVitals>();
            yield return Seconds(0.6f);

            Enemy warden = combat.SpawnEnemy(EnemyKind.DoughWarden, 1, new Vector3(0f, 0f, 6f), ai: false);
            Assert.That(warden.Model.MaxHp, Is.EqualTo(600));
            int swing = 100;
            for (int i = 0; i < 8; i++)   // 4 break the arm (70 HP, 28 per hit / 1.5); the other 4 prove it breaks once. 8 opener hits stay under the revenge cap (12), or it breaks free
                combat.ServerPlayerHitsEnemy(hero, ++swing, AttackSet.Hit1, warden, 0, new Vector3(1f, 0f, 5f), 90f);
            Assert.That(warden.PartIntact(0), Is.False, "the rolling-pin arm broke");
            Assert.That(warden.PartIntact(1), Is.True, "the crown is untouched");
            yield return Seconds(0.3f);
            Assert.That(Object.FindObjectsByType<HealingDrop>().Length, Is.EqualTo(1), "one healing drop, however many hits followed the break");

            vitals.ServerDamage(60);
            Assert.That(vitals.Model.Hp, Is.EqualTo(40));
            HealingDrop drop = Object.FindAnyObjectByType<HealingDrop>();
            var cc = heroObject.GetComponent<CharacterController>();
            cc.enabled = false;
            heroObject.transform.position = new Vector3(drop.transform.position.x, 0f, drop.transform.position.z);
            cc.enabled = true;
            var numbers = new System.Collections.Generic.List<string>();
            System.Action<DamageNumber> onNumber = n => numbers.Add((n.Heal ? "heal " : n.OnPlayer ? "hurt " : "hit ") + n.Amount);
            CombatEvents.NumberPopped += onNumber;
            // Watch the host's HP for a second: the pickup heals 25 (40 -> 65) and nothing else may touch it.
            int peak = vitals.Model.Hp;
            var seen = new System.Collections.Generic.List<int> { peak };
            double end = Time.realtimeSinceStartupAsDouble + 1.0;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                int hp = vitals.Model.Hp;
                if (hp != seen[seen.Count - 1]) seen.Add(hp);
                peak = Mathf.Max(peak, hp);
                yield return null;
            }
            CombatEvents.NumberPopped -= onNumber;
            Assert.That(peak, Is.EqualTo(65), "walking into the drop heals 25; HP history " + string.Join(">", seen) + "; events " + string.Join(", ", numbers) + "; drops left " + Object.FindObjectsByType<HealingDrop>().Length);
            Assert.That(vitals.Model.Hp, Is.EqualTo(65), "and nothing hurt the hero afterwards; HP history " + string.Join(">", seen));
            yield return Seconds(0.3f);
            Assert.That(Object.FindObjectsByType<HealingDrop>().Length, Is.EqualTo(0), "the pickup is used up");
        }
    }
}
