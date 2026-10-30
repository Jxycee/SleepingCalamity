// Authority: test-only. NOT part of the normal run ([Explicit]; it needs a graphics device). Renders the hero front and side mid-walk, mid-swing and mid-dodge
// (and downed, and a guard) from a camera that follows it, for the animation review:
//   powershell -File Tools\unity.ps1 test-play -Graphics -Filter "HeroAnimCaptureTests" -OutDir <scratch>      (set SC_CAPTURE_DIR first)
// The test asserts only that the files were written.
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HeroAnimCaptureTests
    {
        private const int Width = 960, Height = 720;
        private Camera _cam;
        private Transform _hero;
        private string _dir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_cam != null) Object.Destroy(_cam.gameObject);
            yield return PlayModeHarness.TearDownApp();
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        private IEnumerator Shoot(string name, bool front)
        {
            yield return null;
            Vector3 focus = _hero.position + Vector3.up * 1.0f;
            Vector3 dir = front ? _hero.forward : _hero.right;
            _cam.transform.position = focus + dir * 3.6f + Vector3.up * 0.4f;
            _cam.transform.LookAt(focus);
            var rt = new RenderTexture(Width, Height, 24);
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            _cam.targetTexture = rt;
            _cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            _cam.targetTexture = null;
            RenderTexture.active = previous;
            string path = Path.Combine(_dir, name + (front ? "_front" : "_side") + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("SC CAPTURE " + path);
            Object.Destroy(rt);
            Object.Destroy(tex);
        }

        [Explicit("needs a graphics device; run with test-play -Graphics -Filter HeroAnimCaptureTests")]
        [UnityTest]
        public IEnumerator Capture_Walk_Swing_Dodge()
        {
            _dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) _dir = Path.Combine(Path.GetTempPath(), "sc-anim-captures");
            Directory.CreateDirectory(_dir);

            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            var player = AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject;
            _hero = player.transform;
            var loco = player.GetComponent<HeroLocomotion>();
            var combat = player.GetComponent<HeroCombat>();
            var input = new FakeInputService();
            loco.Input = input;
            combat.Input = input;
            var camGo = new GameObject("AnimCaptureCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 40f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.enabled = false;
            yield return Seconds(1.0f);

            yield return Shoot("idle", true);
            yield return Shoot("idle", false);

            input.Frame.MoveY = 0.5f;
            yield return Seconds(1.0f);
            yield return Shoot("walk", true);
            yield return Shoot("walk", false);
            input.Frame.MoveY = 1f;
            yield return Seconds(1.0f);
            yield return Shoot("jog", true);
            yield return Shoot("jog", false);
            input.Frame.MoveY = 0f;
            yield return Seconds(0.6f);

            for (int weapon = 0; weapon < 4; weapon++)
            {
                combat.OwnerSetWeapon(weapon);
                yield return Seconds(0.8f);
                input.Frame.PrimaryPressed = input.Frame.PrimaryHeld = true;
                yield return null;
                input.Frame.PrimaryPressed = false;
                yield return Seconds(0.1f);
                input.Frame.PrimaryHeld = false;
                yield return Seconds(0.06f);
                yield return Shoot("swing_w" + weapon, true);
                yield return Shoot("swing_w" + weapon, false);
                yield return Seconds(1.0f);
            }

            combat.OwnerSetWeapon(2);
            yield return Seconds(0.8f);
            input.Frame.MoveY = 1f;
            yield return Seconds(0.3f);
            input.Frame.DodgePressed = true;
            yield return null;
            input.Frame.DodgePressed = false;
            yield return Seconds(0.2f);
            yield return Shoot("dodge", true);
            yield return Shoot("dodge", false);
            input.Frame.MoveY = 0f;
            yield return Seconds(1.0f);

            input.Frame.SecondaryHeld = input.Frame.SecondaryPressed = true;
            yield return null;
            input.Frame.SecondaryPressed = false;
            yield return Seconds(0.5f);
            yield return Shoot("guard", true);
            yield return Shoot("guard", false);
            input.Frame.SecondaryHeld = false;

            Assert.That(Directory.GetFiles(_dir, "*.png").Length, Is.GreaterThanOrEqualTo(10));
            TestContext.WriteLine("captures: " + _dir);
        }
    }
}
