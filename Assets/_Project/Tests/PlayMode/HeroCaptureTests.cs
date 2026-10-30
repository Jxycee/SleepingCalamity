// Authority: test-only. NOT part of the normal run ([Explicit]; it needs a graphics device). Drives a solo session through the free camera and the
// lock camera and writes PNGs of what the player sees, for the milestone review:
//   powershell -File Tools\unity.ps1 test-play -Graphics -Filter "HeroCaptureTests" -OutDir <scratch>      (set SC_CAPTURE_DIR first)
// The images go to %SC_CAPTURE_DIR% (default: the temp folder's sc-m2-captures). The test asserts only that the files were written.
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HeroCaptureTests
    {
        private const int Width = 1280, Height = 720;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        private static string OutputDir()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "sc-m2-captures");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static IEnumerator Shoot(string dir, string name)
        {
            // Not WaitForEndOfFrame: in a batchmode test run it can wait forever. The Brain has already posed the camera in an earlier LateUpdate.
            yield return null;
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null, "no main camera");
            var rt = new RenderTexture(Width, Height, 24);
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = previousActive;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            HeroNetState hero = Object.FindAnyObjectByType<HeroNetState>();
            Transform t = cam.transform;
            string note = name + ": camera " + t.position.ToString("F2") + " euler " + t.eulerAngles.ToString("F0") + " fov " + cam.fieldOfView
                          + (hero != null ? "; hero " + hero.transform.position.ToString("F2") + " view yaw/pitchDown "
                              + hero.Locomotion.ViewYawDeg.ToString("F0") + "/" + hero.Locomotion.ViewPitchDownDeg.ToString("F0") : "");
            TestContext.WriteLine(note);
            Debug.Log("SC CAPTURE " + note);
            Object.Destroy(rt);
            Object.Destroy(tex);
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        [Explicit("needs a graphics device; run with test-play -Graphics -Filter HeroCaptureTests")]
        [UnityTest]
        public IEnumerator Capture_FreeCamera_And_LockCamera()
        {
            string dir = OutputDir();
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            AppBootstrap app = AppBootstrap.Current;
            var player = app.NetworkManager.LocalClient.PlayerObject;
            var loco = player.GetComponent<HeroLocomotion>();
            var lockCtl = player.GetComponent<TargetLockController>();
            var input = new FakeInputService();
            loco.Input = input;
            lockCtl.Input = input;
            yield return PlayModeHarness.WaitUntil(() => Targetable.All.Count == 7 && HeroCameraRig.Local != null, 10f, "arena not ready");
            yield return Seconds(1.5f);

            // Free camera: behind the hero at spawn, then orbited to the side.
            yield return Shoot(dir, "free_1_spawn");
            loco.SetView(40f, 18f);
            yield return Seconds(1.2f);
            yield return Shoot(dir, "free_2_orbit_right");
            loco.SetView(55f, 22f);   // aimed at the corner of the high block: the camera must stay outside it
            yield return Seconds(1.2f);
            yield return Shoot(dir, "free_2b_orbit_block_corner");
            loco.SetView(0f, 15f);
            yield return Seconds(1.0f);

            // Lock camera on the dummy ahead.
            input.Frame.LockPressed = true;
            yield return null;
            input.Frame.LockPressed = false;
            yield return Seconds(1.4f);
            yield return Shoot(dir, "lock_1_near_dummy");

            // Strafing around it, then a flick to the next dummy to the right.
            input.Frame.MoveX = 1f;
            yield return Seconds(0.9f);
            input.Frame.MoveX = 0f;
            yield return Seconds(0.8f);
            yield return Shoot(dir, "lock_2_strafe_right");
            input.Frame.SwitchX = 1f;
            yield return null;
            input.Frame.SwitchX = 0f;
            yield return Seconds(1.4f);
            yield return Shoot(dir, "lock_3_switched_target");

            // Back to the free camera, then walk backward toward the wall behind the spawn: the boom is pulled in by the Deoccluder.
            input.Frame.LockPressed = true;
            yield return null;
            input.Frame.LockPressed = false;
            yield return Seconds(0.5f);
            loco.SetView(0f, 15f);
            input.Frame.MoveX = -1f;   // back to the middle of the arena first
            yield return Seconds(0.75f);
            input.Frame.MoveX = 0f;
            input.Frame.MoveY = -1f;
            yield return Seconds(1.6f);
            input.Frame.MoveY = 0f;
            yield return Seconds(1.0f);
            yield return Shoot(dir, "free_3_wall_deoccluded");

            Assert.That(Directory.GetFiles(dir, "*.png").Length, Is.GreaterThanOrEqualTo(6));
            TestContext.WriteLine("captures: " + dir);
        }
    }
}
