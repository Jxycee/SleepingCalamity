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
            yield return Seconds(0.6f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Free"), Is.True, "idle hero is in the Free locomotion state");
            Assert.That(animator.GetFloat(HeroAnimParam.Speed), Is.LessThan(0.5f));

            input.Frame.MoveY = 1f;
            yield return Seconds(1.2f);
            Assert.That(animator.GetFloat(HeroAnimParam.Speed), Is.GreaterThan(2f), "moving hero feeds Speed to the Animator");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Free"), Is.True, "still in locomotion while running");
            Assert.That(animator.GetBool(HeroAnimParam.Grounded), Is.True);
            input.Frame.MoveY = 0f;
        }
    }
}
