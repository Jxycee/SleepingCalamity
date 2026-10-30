// Authority: test-only. The hero prefab spawns with a working Animator (the Y Bot and its controller), the placeholder mannequin driver stands down, and
// the Animator reaches locomotion when the hero is moved.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HeroAnimatorTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        private static string Clips(Animator a)
        {
            var infos = a.GetCurrentAnimatorClipInfo(0);
            return infos.Length > 0 ? infos[0].clip.name : "none";
        }

        private static bool InLocomotion(Animator a)
        {
            AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(0);
            return st.IsName("Free") || st.IsName("Strafe");
        }

        [UnityTest]
        public IEnumerator Hero_SpawnsWithAWorkingAnimator_ThatReachesLocomotionWhenMoved()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer();
            var player = AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject;
            var driver = player.GetComponentInChildren<AnimatorHeroDriver>();
            Assert.That(driver, Is.Not.Null, "the hero visual has no AnimatorHeroDriver");
            Assert.That(driver.IsWorking, Is.True, "no Humanoid Animator with a controller on the hero");
            Animator animator = driver.Animator;
            Assert.That(animator.GetLayerIndex("Attack"), Is.EqualTo(1));

            var input = new FakeInputService();
            player.GetComponent<HeroLocomotion>().Input = input;
            player.GetComponent<TargetLockController>().Input = input;
            player.GetComponent<HeroCombat>().Input = input;
            yield return PlayModeHarness.WaitUntil(() => HeroCameraRig.Local != null, 10f, "camera rig not ready");
            yield return Seconds(0.6f);
            // The hero spawns a hair above the floor and lands first (JumpStart, JumpLand): give the landing time to play out.
            yield return PlayModeHarness.WaitUntil(() => InLocomotion(animator), 6f, "the Animator never reached locomotion after spawning");
            Assert.That(InLocomotion(animator), Is.True, "idle hero is in a locomotion state (Free or Strafe), clip " + Clips(animator) + " grounded " + animator.GetBool(HeroAnimParam.Grounded) + " locked " + animator.GetBool(HeroAnimParam.Locked));
            Assert.That(animator.GetFloat(HeroAnimParam.Speed), Is.LessThan(0.5f));

            input.Frame.MoveY = 1f;
            // The dummy 4 m ahead stops the hero within a second, so take the peak speed while it runs.
            float peak = 0f;
            double until = Time.realtimeSinceStartupAsDouble + 1.0;
            while (Time.realtimeSinceStartupAsDouble < until) { peak = Mathf.Max(peak, animator.GetFloat(HeroAnimParam.Speed)); yield return null; }
            Assert.That(peak, Is.GreaterThan(2f), "moving hero feeds Speed to the Animator");
            Assert.That(InLocomotion(animator), Is.True, "still in locomotion while running");
            Assert.That(animator.GetBool(HeroAnimParam.Grounded), Is.True);
            input.Frame.MoveY = 0f;
        }
    }
}
