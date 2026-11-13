// Authority: test-only (no networked state). PL-3 review fix: the always-on toast line is on the App object, subscribes to the EventBus after boot and holds the words of a published toast.
// Catches: a release build with nobody listening to ToastRequested (the dev panel is not drawn there), so a save warning never reaches the player.
// NOT covered: how the IMGUI line looks on screen (OnGUI does not run under -nographics).
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class ToastLineTests
    {
        [UnityTearDown]
        public IEnumerator TearDown() => PlayModeHarness.TearDownApp();

        [UnityTest]
        public IEnumerator PublishedSaveWarning_ReachesTheLine_AndEndReasonsDoNot()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            var line = app.GetComponent<ToastLine>();
            Assert.That(line, Is.Not.Null, "the App object has no ToastLine (SceneBuilder must add it)");
            var bus = app.Services.Get<EventBus>();

            yield return PlayModeHarness.WaitUntil(() =>
            {
                bus.Publish(new ToastRequested("save.world_read_only", null, 6f));
                return line.Current.Count > 0;
            }, 5f, "the toast line never picked up the published toast");
            Assert.That(line.Current[0], Is.EqualTo(UiText.Toast("save.world_read_only")));

            bus.Publish(new ToastRequested("err.host_left"));
            Assert.That(line.Current, Has.Count.EqualTo(1), "the end reason is the front door's to say, not the line's");
        }
    }
}
