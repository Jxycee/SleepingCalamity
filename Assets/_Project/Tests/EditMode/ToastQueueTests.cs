// PL-3 review fix: the words the always-on toast line shows. Catches: a save warning that reaches no words, a raw key on screen, a toast that never expires, an order that hides the newest.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class ToastQueueTests
    {
        private static List<string> At(ToastQueue q, double now)
        {
            var list = new List<string>();
            q.Lines(now, list);
            return list;
        }

        [Test]
        public void SaveWarning_ShowsItsWords()
        {
            var q = new ToastQueue();
            q.Add("save.world_read_only", null, 4f, 10);
            Assert.That(At(q, 10), Is.EqualTo(new[] { UiText.Toast("save.world_read_only") }));
        }

        [Test]
        public void LibraryFull_UsesItsArgument()
        {
            var q = new ToastQueue();
            q.Add("world.library_full", "Multiplayer", 6f, 0);
            Assert.That(At(q, 0)[0], Is.EqualTo(UiText.Toast("world.library_full", "Multiplayer")));
        }

        [Test]
        public void Toast_ExpiresAfterAboutSixSeconds()
        {
            var q = new ToastQueue();
            q.Add("save.world_load_failed", null, 4f, 100);
            Assert.That(At(q, 105.5), Has.Count.EqualTo(1));
            Assert.That(At(q, 106.1), Is.Empty);
        }

        [Test]
        public void NewestIsFirst()
        {
            var q = new ToastQueue();
            q.Add("save.world_read_only", null, 6f, 0);
            q.Add("save.world_load_failed", null, 6f, 1);
            Assert.That(At(q, 2), Is.EqualTo(new[] { UiText.Toast("save.world_load_failed"), UiText.Toast("save.world_read_only") }));
        }

        [Test]
        public void SameWordsTwice_RenewTheLineInsteadOfStacking()
        {
            var q = new ToastQueue();
            q.Add("save.world_read_only", null, 6f, 0);
            q.Add("save.world_read_only", null, 6f, 5);
            Assert.That(At(q, 8), Has.Count.EqualTo(1));
            Assert.That(At(q, 10), Has.Count.EqualTo(1), "renewed at 5, so still up at 10");
        }

        [Test]
        public void UnknownKey_ShowsNothing_AndIsReportedOnce()
        {
            var seen = new List<string>();
            var q = new ToastQueue(seen.Add);
            q.Add("not.a.key", null, 6f, 0);
            q.Add("not.a.key", null, 6f, 1);
            q.Add(null, null, 6f, 1);
            Assert.That(At(q, 1), Is.Empty);
            Assert.That(seen, Is.EqualTo(new[] { "not.a.key" }));
        }

        [Test]
        public void ManyToasts_KeepTheNewestFour()
        {
            var q = new ToastQueue();
            string[] keys = { "save.world_read_only", "save.world_load_failed", "world.library_full", "err.timeout", "err.network" };
            for (int i = 0; i < keys.Length; i++) q.Add(keys[i], "Solo", 6f, i);
            List<string> now = At(q, 5);
            Assert.That(now, Has.Count.EqualTo(ToastQueue.MaxLines));
            Assert.That(now[0], Is.EqualTo(UiText.Toast("err.network")));
            Assert.That(now, Does.Not.Contain(UiText.Toast("save.world_read_only")));
        }

        [Test]
        public void TheFrontDoorSaysEndReasonsAndStartRefusalsItself()
        {
            Assert.That(ToastLine.ShownByTheFrontDoor("err.host_left"), Is.True);
            Assert.That(ToastLine.ShownByTheFrontDoor("world.need_two_players"), Is.True);
            Assert.That(ToastLine.ShownByTheFrontDoor("world.library_full"), Is.False);
            Assert.That(ToastLine.ShownByTheFrontDoor("save.world_read_only"), Is.False);
            Assert.That(ToastLine.ShownByTheFrontDoor("save.world_load_failed"), Is.False);
        }
    }
}
