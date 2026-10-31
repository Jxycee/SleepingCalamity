// Authority: test-only. NOT part of the normal run ([Explicit]; it needs a graphics device). Captures every enemy attack's body tell (EnemyTell: pose,
// hold, glint, strike) frame by frame for review, with the ground footprint assist OFF, so the frames show only what the enemy itself does:
//   set SC_CAPTURE_DIR=<dir>  then  Tools\unity.ps1 test-play -Graphics -Filter "TellCaptureTests" -OutDir <scratch>
// Time is stepped at exactly 1/60 s (Time.captureDeltaTime), so frame NNN in a file name is the attack's own frame. Asserts only that files were written.
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class TellCaptureTests
    {
        private const int Width = 960, Height = 540, Every = 2;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            SceneFlow.DevCombatArena = false;
            yield return PlayModeHarness.TearDownApp();
        }

        private static void Shoot(string path)
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(Width, Height, 24);
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(tex);
        }

        [Explicit("needs a graphics device; run with test-play -Graphics -Filter TellCaptureTests")]
        [UnityTest, Timeout(600000)]
        public IEnumerator Capture_EveryEnemyAttackTell()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "sc-tell-captures");
            Directory.CreateDirectory(dir);
            CombatAssists.GroundTelegraph = false;

            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.DevCombatArena = true;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.ArenaCombat);
            var hero = AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject;
            CombatHost combat = Object.FindAnyObjectByType<CombatHost>();
            combat.WavesEnabled = false;
            foreach (Enemy stray in new System.Collections.Generic.List<Enemy>(Enemy.All)) if (stray.IsSpawned) stray.NetworkObject.Despawn(true);
            var input = new FakeInputService();
            var loco = hero.GetComponent<HeroLocomotion>();
            loco.Input = input;
            hero.GetComponent<TargetLockController>().Input = input;
            hero.GetComponent<HeroCombat>().Input = input;
            yield return null;

            var shots = new (EnemyKind kind, AttackData attack, float dist)[]
            {
                (EnemyKind.Slime, AttackCatalog.SlimeHop, 4.5f), (EnemyKind.Slime, AttackCatalog.SlimeGlob, 5f),
                (EnemyKind.RatKing, AttackCatalog.RatLunge, 5.5f), (EnemyKind.RatKing, AttackCatalog.RatSpin, 3.5f),
                (EnemyKind.Broom, AttackCatalog.BroomSweep, 3.8f), (EnemyKind.Broom, AttackCatalog.BroomPoke, 3.8f),
                (EnemyKind.DoughWarden, AttackCatalog.WardenSlam, 5.5f), (EnemyKind.DoughWarden, AttackCatalog.WardenSweep, 5.5f),
                (EnemyKind.DoughWarden, AttackCatalog.WardenPound, 5.5f), (EnemyKind.DoughWarden, AttackCatalog.WardenBreakFree, 5.5f),
                (EnemyKind.DoughWarden, AttackCatalog.WardenQuake, 5.5f),
            };
            int written = 0;
            Time.captureDeltaTime = 1f / AttackData.FrameRate;
            try
            {
                foreach (var s in shots)
                {
                    Vector3 h = hero.transform.position;
                    // Seen from the side: the enemy ahead and well to the right, facing the hero, so a lean or coil reads in profile.
                    Vector3 at = h + Quaternion.Euler(0f, 62f, 0f) * Vector3.forward * (s.dist * 0.8f);
                    Enemy e = combat.SpawnEnemy(s.kind, 1, at, ai: false);
                    Vector3 face = h - at;
                    face.y = 0f;
                    e.transform.rotation = Quaternion.LookRotation(face.normalized);
                    loco.SetView(48f, 10f);
                    for (int i = 0; i < 40; i++) yield return null;   // settle the camera
                    // A lob is aimed beside the hero so the splash shows; everything else at the hero.
                    e.ServerForceAttack(s.attack, s.attack.Origin == HitOrigin.GroundAtTarget ? h + Vector3.right * 1.2f : h);
                    int end = Mathf.Min(s.attack.TotalFrames - 1, s.attack.HitOn.End + 14);
                    yield return null;
                    while (e != null && e.Attacking && e.AttackFrame <= end)
                    {
                        int f = Mathf.RoundToInt(e.AttackFrame);
                        if (f % Every == 0)
                        {
                            Shoot(Path.Combine(dir, $"{s.attack.Id}_{f:000}.png"));
                            written++;
                        }
                        yield return null;
                    }
                    if (e != null && e.IsSpawned) e.NetworkObject.Despawn(true);
                    for (int i = 0; i < 10; i++) yield return null;
                }
            }
            finally { Time.captureDeltaTime = 0f; }
            Debug.Log("SC CAPTURE tells: " + written + " frames in " + dir);
            Assert.That(written, Is.GreaterThan(50));
        }
    }
}
