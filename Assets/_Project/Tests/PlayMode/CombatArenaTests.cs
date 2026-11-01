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
using Unity.Netcode;
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
            Time.captureDeltaTime = 0f;
            yield return PlayModeHarness.TearDownApp();
        }

        // Catches the bow bug from the first playtest ("no arrow fires at all, sometimes"): the shot's hitbox is one 60 Hz frame, so on a slow frame rate the
        // attack clock stepped over it. Here every frame lasts 50 ms (3 attack frames per tick), which skipped the window every time before the fix. The shot
        // must draw an arrow and damage a (short) slime six metres away with no lock-on.
        [UnityTest]
        public IEnumerator Solo_Bow_FiresAndHits_EvenWhenTheFrameRateIsLow()
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
            hero.OwnerSetWeapon(3);   // the bow and sword
            yield return PlayModeHarness.WaitUntil(() => hero.WeaponKey == WeaponCatalog.BowSword, 3f, "the bow never became the held weapon");
            yield return Seconds(0.6f);

            Enemy slime = combat.SpawnEnemy(EnemyKind.Slime, 1, heroObject.transform.position + heroObject.transform.forward * 6f, ai: false);
            int arrows = 0, landed = 0;
            System.Action<Vector3, Vector3, bool> onArrow = (from, to, hit) => { arrows++; if (hit) landed++; };
            CombatEvents.ArrowFired += onArrow;
            int hp = slime.NetHp.Value;
            Time.captureDeltaTime = 0.05f;
            try
            {
                input.Frame.SecondaryHeld = true;
                yield return Seconds(0.4f);
                Assert.That(hero.Aiming, Is.True, "holding the secondary button aims the bow");
                input.Frame.PrimaryPressed = true;
                input.Frame.PrimaryHeld = true;
                yield return null;
                input.Frame.PrimaryPressed = false;
                input.Frame.PrimaryHeld = false;
                input.Frame.PrimaryReleased = true;
                yield return null;
                input.Frame.PrimaryReleased = false;
                yield return PlayModeHarness.WaitUntil(() => arrows >= 1, 3f, "no arrow was fired at 20 fps");
                yield return PlayModeHarness.WaitUntil(() => slime.NetHp.Value < hp, 3f, "the arrow flew but did not hurt the slime six metres ahead");
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                CombatEvents.ArrowFired -= onArrow;
                input.Frame = default;
            }
            Assert.That(landed, Is.GreaterThanOrEqualTo(1), "the arrow reported a hit");
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

        // Catches overlapping enemy attacks (enemy polish, 2026-10-30): four slimes crowd an idle hero; the host's attack tokens must keep at most two of them
        // winding up at once, make the rest wait (hover and strafe), and open no two strikes closer than the tier's gap.
        [UnityTest, Timeout(60000)]
        public IEnumerator Solo_AttackTokens_KeepACrowdToTwoAttackers_AndTheirStrikesApart()
        {
            yield return StartCombatSolo();
            var hero = AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);
            yield return Seconds(0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                combat.SpawnEnemy(EnemyKind.Slime, 1, hero.transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.6f);
            }
            int maxWindUps = 0, strikes = 0;
            bool someoneWaited = false;
            float minGap = float.MaxValue, lastStrike = -1f;
            var seen = new System.Collections.Generic.HashSet<(ulong, int)>();
            double until = Time.realtimeSinceStartupAsDouble + 8.0;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                int windUps = 0;
                foreach (Enemy e in Enemy.All)
                {
                    if (!e.IsSpawned || !e.Alive) continue;
                    if (e.WindingUp) windUps++;
                    if (e.WaitingForToken) someoneWaited = true;
                    if (e.HitboxOn && seen.Add((e.NetworkObjectId, e.Serial)))
                    {
                        if (lastStrike >= 0f) minGap = Mathf.Min(minGap, Time.time - lastStrike);
                        lastStrike = Time.time;
                        strikes++;
                    }
                }
                maxWindUps = Mathf.Max(maxWindUps, windUps);
                yield return null;
            }
            Assert.That(strikes, Is.GreaterThanOrEqualTo(3), "the crowd attacked");
            Assert.That(maxWindUps, Is.LessThanOrEqualTo(AttackPacing.Tier1.MaxAttackersPerHero), "never more than two winding up at the hero");
            Assert.That(someoneWaited, Is.True, "with four in reach, some had to wait for a token");
            Assert.That(minGap, Is.GreaterThanOrEqualTo(AttackPacing.Tier1.MinStrikeGapSeconds - 0.05f), "two strikes opened " + minGap.ToString("F2") + " s apart");
        }

        [UnityTest]
        public IEnumerator Solo_WaveOne_Spawns_AndTheEnemiesChaseAndHurtAnIdleHero()
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            var hero = app.NetworkManager.LocalClient.PlayerObject;
            HeroVitals vitals = hero.GetComponent<HeroVitals>();

            yield return PlayModeHarness.WaitUntil(() => Enemy.All.Count >= 2, Timeout, "wave 1 never spawned its two slimes");
            int slimes = 0;
            foreach (Enemy e in Enemy.All) if (e.Def.Kind == EnemyKind.Slime) slimes++;
            Assert.That(slimes, Is.EqualTo(2), "wave 1 is two jam-jar slimes (solo; was four before the readability pass)");

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
            Assert.That(slime.Model.MaxHp, Is.EqualTo(80), "solo: life x1.0 (80 since combat depth)");
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
            Assert.That(slime.Alive, Is.False, "a few axe hits kill an 80 HP slime (the first flinches it open)");

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
            Assert.That(vitals.Model.Hp, Is.EqualTo(98), "a held guard takes a fifth of the damage, rounded up: the nova hits for 8 (12 x 0.65 x 100/110 = 7.09 -> 8), x0.2 = 1.6 -> 2 (was 11 -> 3, hp 97)");

            // Parry: the shield comes up just before the hit, inside the 6-frame window: no damage, the attacker is stunned, the dagger earns its parry ABS.
            input.Frame.SecondaryHeld = false;
            yield return Seconds(0.6f);
            vitals.ServerFullReset();
            int serial = slime.Serial;
            bool stunned = false;
            // Deterministic: game time steps at exactly 1/60 s (a real-time 0.1 s sleep left about one frame of margin and flaked on a hitch), and the raise
            // is keyed to the attack's own frame counter (3 frames before the hitbox), not to a wall-clock wait.
            Time.captureDeltaTime = 1f / AttackData.FrameRate;
            try
            {
                slime.ServerForceAttack(AttackCatalog.TestNova, heroObject.transform.position);
                yield return PlayModeHarness.WaitUntil(() => slime.Serial != serial && slime.Attacking, 2f, "the second attack never started");
                float raiseAt = AttackCatalog.TestNova.HitOn.Start - 3;
                for (int guardLoop = 0; slime.AttackFrame < raiseAt && guardLoop < 600; guardLoop++) yield return null;
                Assert.That(slime.AttackFrame, Is.LessThan(AttackCatalog.TestNova.HitOn.Start), "still winding up when the shield goes up");
                input.Frame.SecondaryHeld = true;
                input.Frame.SecondaryPressed = true;
                yield return null;
                input.Frame.SecondaryPressed = false;
                for (int f = 0; f < 72; f++) { stunned |= slime.Phase == EnemyPhase.Stagger; yield return null; }
                input.Frame.SecondaryHeld = false;
            }
            finally { Time.captureDeltaTime = 0f; }
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

        // ---- review round 1 fixes ----

        private static IEnumerator ArenaWithoutWave(System.Action<CombatHost, GameObject, HeroCombat, HeroVitals, FakeInputService> body)
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            GameObject heroObject = app.NetworkManager.LocalClient.PlayerObject.gameObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            combat.AiEnabled = false;
            combat.CritChance = 0f;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);
            var input = new FakeInputService();
            heroObject.GetComponent<HeroLocomotion>().Input = input;
            heroObject.GetComponent<TargetLockController>().Input = input;
            HeroCombat hero = heroObject.GetComponent<HeroCombat>();
            hero.Input = input;
            yield return Seconds(0.6f);
            body(combat, heroObject, hero, heroObject.GetComponent<HeroVitals>(), input);
        }

        // Catches: attack serials that restart at 1 on every enemy, so a recycled NetworkObjectId (Bootstrap recycles ids after 120 s) makes the victim's client and the host
        // read the new enemy's first attack as the old enemy's already-judged one, and the hero is never hurt by it.
        [UnityTest]
        public IEnumerator Solo_RecycledEnemyId_TheNewEnemysFirstAttackStillLands()
        {
            yield return StartCombatSolo();
            AppBootstrap app = AppBootstrap.Current;
            NetworkManager nm = app.NetworkManager;
            GameObject heroObject = nm.LocalClient.PlayerObject.gameObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            combat.AiEnabled = false;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);
            var input = new FakeInputService();
            heroObject.GetComponent<HeroLocomotion>().Input = input;
            heroObject.GetComponent<TargetLockController>().Input = input;
            heroObject.GetComponent<HeroCombat>().Input = input;
            HeroVitals vitals = heroObject.GetComponent<HeroVitals>();
            yield return Seconds(0.6f);

            float oldDelay = nm.NetworkConfig.NetworkIdRecycleDelay;
            nm.NetworkConfig.NetworkIdRecycleDelay = 0f;
            try
            {
                Vector3 spot = heroObject.transform.position + heroObject.transform.forward * 3f;
                Enemy first = combat.SpawnEnemy(EnemyKind.Slime, 1, spot, ai: false);
                ulong id = first.NetworkObjectId;
                first.ServerForceAttack(AttackCatalog.TestNova, heroObject.transform.position);
                yield return PlayModeHarness.WaitUntil(() => vitals.Model.Hp < vitals.MaxHp, 3f, "the first enemy's attack never hurt the hero (control)");
                int serialOfFirst = first.Serial;
                yield return Seconds(1.4f);   // its attack ends
                first.NetworkObject.Despawn(true);
                yield return null;
                vitals.ServerFullReset();
                yield return null;

                // The id queue is FIFO: spawn and despawn until the old id comes round again (bounded).
                Enemy second = null;
                for (int i = 0; i < 16 && (second == null || second.NetworkObjectId != id); i++)
                {
                    if (second != null) { second.NetworkObject.Despawn(true); yield return null; }
                    second = combat.SpawnEnemy(EnemyKind.Slime, 1, spot, ai: false);
                }
                Assert.That(second.NetworkObjectId, Is.EqualTo(id), "precondition: the new enemy got the old enemy's NetworkObjectId back");
                second.ServerForceAttack(AttackCatalog.TestNova, heroObject.transform.position);
                Assert.That(second.Serial, Is.Not.EqualTo(serialOfFirst), "an attack serial is never reused, by this enemy id or any other");
                yield return PlayModeHarness.WaitUntil(() => vitals.Model.Hp < vitals.MaxHp, 3f, "the recycled-id enemy's first attack never hurt the hero (its serial was read as already judged)");
            }
            finally { nm.NetworkConfig.NetworkIdRecycleDelay = oldDelay; }
        }

        // Catches: ServerClearEnemies (F6 / a wipe) and any other despawn that skips ForgetEnemy, so a recycled id inherits the old enemy's style-ABS cap and tech cap.
        [UnityTest]
        public IEnumerator Solo_EveryRemovalPath_ForgetsTheEnemysRewardLedgers()
        {
            Enemy[] enemies = new Enemy[2];
            string[] keys = new string[2];
            CombatHost combat = null;
            PlayerGrowth growth = null;
            yield return ArenaWithoutWave((c, hero, hc, v, input) =>
            {
                combat = c;
                growth = c.Growth(AppBootstrap.Current.NetworkManager.LocalClientId);
                for (int i = 0; i < 2; i++)
                {
                    enemies[i] = c.SpawnEnemy(EnemyKind.Slime, 1, hero.transform.position + new Vector3(4f + i * 2f, 0f, 4f), ai: false);
                    keys[i] = enemies[i].NetworkObjectId.ToString();
                    growth.Ledger.Award(keys[i], WeaponCatalog.DaggerShield, StyleEvent.Hit);
                    growth.TechBase(keys[i]);
                    Assert.That(growth.Ledger.Paid(keys[i]), Is.GreaterThan(0f), "setup: the ledger has an entry");
                    Assert.That(growth.TechPaid(keys[i]), Is.GreaterThan(0f), "setup: the tech cap has an entry");
                }
            });
            combat.ServerClearEnemies();   // path 1: the F6 / wipe clear
            Assert.That(growth.Ledger.Paid(keys[0]), Is.EqualTo(0f), "ServerClearEnemies forgot the style ledger");
            Assert.That(growth.TechPaid(keys[0]), Is.EqualTo(0f), "and the tech cap");
            Assert.That(growth.Ledger.Paid(keys[1]), Is.EqualTo(0f));
            yield return null;

            Enemy third = combat.SpawnEnemy(EnemyKind.Slime, 1, new Vector3(5f, 0f, 5f), ai: false);   // path 2: any other despawn
            string key = third.NetworkObjectId.ToString();
            growth.Ledger.Award(key, WeaponCatalog.DaggerShield, StyleEvent.Hit);
            growth.TechBase(key);
            third.NetworkObject.Despawn(true);
            Assert.That(growth.Ledger.Paid(key), Is.EqualTo(0f), "a plain despawn forgot the style ledger");
            Assert.That(growth.TechPaid(key), Is.EqualTo(0f), "and the tech cap");
        }

        // Catches: a downed hero that can still jump (HeroLocomotion ignored HeroCombat.Frozen). The control jump proves the test can see a jump.
        [UnityTest]
        public IEnumerator Solo_ADownedHero_CannotJump()
        {
            GameObject heroObject = null;
            HeroVitals vitals = null;
            FakeInputService input = null;
            yield return ArenaWithoutWave((c, h, hc, v, i) => { heroObject = h; vitals = v; input = i; });
            float ground = heroObject.transform.position.y;

            float peak = ground;
            input.Frame.JumpPressed = true;
            double until = Time.realtimeSinceStartupAsDouble + 0.6;
            while (Time.realtimeSinceStartupAsDouble < until) { yield return null; input.Frame.JumpPressed = false; peak = Mathf.Max(peak, heroObject.transform.position.y); }
            Assert.That(peak - ground, Is.GreaterThan(0.3f), "control: an upright hero jumps");
            yield return Seconds(1.2f);   // lands

            vitals.ServerDamage(1000);
            Assert.That(vitals.IsDowned, Is.True);
            ground = heroObject.transform.position.y;
            peak = ground;
            input.Frame.JumpPressed = true;
            until = Time.realtimeSinceStartupAsDouble + 0.6;
            while (Time.realtimeSinceStartupAsDouble < until) { yield return null; peak = Mathf.Max(peak, heroObject.transform.position.y); }
            input.Frame.JumpPressed = false;
            Assert.That(peak - ground, Is.LessThan(0.05f), "a downed hero stays on the ground");
        }

        // Catches: a hero left out for good in the arena: a wave clear stands the out hero up (and still heals the others).
        [UnityTest]
        public IEnumerator Solo_WaveClear_StandsAnOutHeroBackUp()
        {
            HeroVitals vitals = null;
            CombatHost combat = null;
            yield return ArenaWithoutWave((c, h, hc, v, i) => { combat = c; vitals = v; });
            vitals.ServerDamage(1000);
            vitals.Model.Tick(PlayerVitals.DownedSeconds + 1f, false);   // the downed timer runs out on the host
            Assert.That(vitals.Model.State, Is.EqualTo(VitalState.Out));
            combat.ServerOnWaveCleared();   // same frame: the wipe check has not run yet
            Assert.That(vitals.Model.State, Is.EqualTo(VitalState.Up), "an out hero stands up at wave clear");
            Assert.That(vitals.Model.Hp, Is.GreaterThan(0));
            yield return PlayModeHarness.WaitUntil(() => vitals.IsUp && vitals.Hp > 0, 3f, "the revived state never replicated");
        }

        // Catches: a staff spell that misses (hits nothing) costing no pip on the host, so only the owner's mirror paid.
        [UnityTest]
        public IEnumerator Solo_StaffSpellThatHitsNothing_StillSpendsAPipOnTheHost()
        {
            HeroCombat hero = null;
            FakeInputService input = null;
            CombatHost combat = null;
            yield return ArenaWithoutWave((c, h, hc, v, i) => { combat = c; hero = hc; input = i; });
            hero.OwnerSetWeapon(1);   // the staff
            Assert.That(hero.WeaponKey, Is.EqualTo(WeaponCatalog.Staff));
            yield return PlayModeHarness.WaitUntil(() => hero.Mirror.Pips == SpellPips.StartCap, 3f, "the owner's pip mirror never synced");
            ulong cid = AppBootstrap.Current.NetworkManager.LocalClientId;
            PlayerGrowth growth = combat.Growth(cid);
            Assert.That(growth.Pips.Current, Is.EqualTo(SpellPips.StartCap));

            input.Frame.SecondaryPressed = true;
            yield return null;
            input.Frame.SecondaryPressed = false;
            yield return Seconds(1.0f);   // the whole cast; there is nothing to hit
            Assert.That(growth.Pips.Current, Is.EqualTo(SpellPips.StartCap - 1), "the host spent the pip although the spell hit nothing");
            yield return PlayModeHarness.WaitUntil(() => hero.Mirror.Pips == SpellPips.StartCap - 1, 3f, "the owner's mirror shows the host's count");
        }

        // Catches: a finisher bonus that counts hits of earlier combos. A combo started within the 1.2 s grace rebases like AttackMachine.Begin: the second
        // finisher has the same two prior hits as the first.
        [UnityTest]
        public IEnumerator Solo_SecondComboWithinTheGrace_FinisherCountsOnlyItsOwnChain()
        {
            Enemy warden = null;
            HeroCombat hero = null;
            CombatHost combat = null;
            yield return ArenaWithoutWave((c, h, hc, v, i) =>
            {
                combat = c;
                hero = hc;
                warden = c.SpawnEnemy(EnemyKind.DoughWarden, 1, new Vector3(0f, 0f, 6f), ai: false);
            });
            int swing = 200;
            Vector3 at = new Vector3(1f, 0f, 5f);
            // What the owner's AttackMachine reports at each swing start (PriorHits rebases at every chain start, even inside the grace): 0, 1, 2, then 0, 1, 2 again.
            int[] priors = { 0, 1, 2, 0, 1, 2 };
            int[] attacks = { AttackSet.Hit1, AttackSet.Hit2, AttackSet.Finisher, AttackSet.Hit1, AttackSet.Hit2, AttackSet.Finisher };
            for (int i = 0; i < attacks.Length; i++)
            {
                combat.ServerPlayerHitsEnemy(hero, ++swing, attacks[i], warden, -1, at, 90f, priors[i]);
                Assert.That(combat.LastAppliedPrior, Is.EqualTo(priors[i]), "swing " + i + " used the owner's prior-hit count");
                if (i == 2) Assert.That(warden.Model.Hp, Is.LessThan(600), "the first combo hit");
            }
            Assert.That(combat.LastAppliedPrior, Is.EqualTo(2), "a second combo inside the grace counts only its own two hits (not the first combo's)");
        }
    }
}
