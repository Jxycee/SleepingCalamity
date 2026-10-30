// Catches the bow bug from the first arena playtest ("no arrow fires at all, sometimes"):
//  - the aimed shot's hitbox is ONE 60 Hz frame (frames 4-5). The owner only looked at the machine's clock once per rendered frame, so at any frame rate
//    under 60 (the editor, a hitch) the clock stepped over the window and the shot never fired, while its sound had already played at the press.
//  - a click during the shot's 20-frame recovery was thrown away instead of buffered, so fast clicking fired every other shot.
// Fixed in AttackMachine: HitboxSwept covers every frame the clock passed through this tick, and RequestSpecial buffers a press like the swings do.
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class BowReadabilityTests
    {
        static int SweptTicksOfOneShot(float dt)
        {
            var m = new AttackMachine(AttackCatalog.Bow);
            Assert.That(m.TryStartSpecial(), Is.True);
            int swept = 0;
            for (int i = 0; i < 400 && m.Active; i++)
            {
                m.Tick(dt, default);
                if (m.Active && m.HitboxSwept) swept++;
            }
            return swept;
        }

        [Test]
        public void TheShotsOneFrameHitbox_IsSeenAtEveryFrameRate()
        {
            // 144 fps down to 10 fps, including the steps that jump straight over frames 4-5 (dt 0.05 = 3 frames per tick, 0.045 = 2.7).
            foreach (float dt in new[] { 1f / 144f, 1f / 120f, 1f / 60f, 1f / 45f, 1f / 35f, 1f / 30f, 0.04f, 0.045f, 0.05f, 0.06f, 0.1f })
                Assert.That(SweptTicksOfOneShot(dt), Is.GreaterThanOrEqualTo(1), "the shot's hit window was stepped over at dt " + dt);
        }

        [Test]
        public void SweptHitbox_AgreesWithTheInstantOne_AtSixtyHertz()
        {
            var m = new AttackMachine(AttackCatalog.Axe);
            m.Tick(1f / 60f, new AttackInput { Pressed = true, Held = true });
            int on = 0, swept = 0;
            for (int i = 0; i < 40 && m.Active; i++)
            {
                m.Tick(1f / 60f, default);
                if (m.Active && m.HitboxOn) on++;
                if (m.Active && m.HitboxSwept) swept++;
            }
            Assert.That(on, Is.InRange(4, 6), "axe hit 1: frames 11-16 (float noise may move one tick)");
            Assert.That(swept, Is.InRange(on, on + 1), "at the native rate the sweep adds at most the tick that crosses the window's end");
        }

        [Test]
        public void SweptHitbox_IsOffBeforeAndAfterTheWindow()
        {
            var m = new AttackMachine(AttackCatalog.Bow);
            Assert.That(m.TryStartSpecial(), Is.True);
            m.Tick(1f / 60f, default);   // frame 1
            Assert.That(m.HitboxSwept, Is.False, "before frame 4");
            for (int i = 0; i < 20; i++) m.Tick(1f / 60f, default);   // frame 21
            Assert.That(m.HitboxSwept, Is.False, "long after frame 5");
        }

        [Test]
        public void AShotPressedDuringTheRecovery_IsBufferedAndFires()
        {
            var m = new AttackMachine(AttackCatalog.Bow);
            Assert.That(m.RequestSpecial(), Is.True, "free: starts at once");
            for (int i = 0; i < 8; i++) m.Tick(1f / 60f, default);   // frame 8: inside the 20-frame recovery
            int firstSwing = m.SwingId;
            Assert.That(m.RequestSpecial(), Is.False, "busy: cannot start yet");
            bool started = false;
            for (int i = 0; i < 30 && !started; i++) started = m.Tick(1f / 60f, default);
            Assert.That(started, Is.True, "the buffered press starts the next shot as soon as the first can be cancelled");
            Assert.That(m.SwingId, Is.EqualTo(firstSwing + 1));
            Assert.That(m.Current.Kind, Is.EqualTo(AttackKind.Shot));
        }

        [Test]
        public void ABufferedShot_ExpiresIfNotWantedAnymore()
        {
            var m = new AttackMachine(AttackCatalog.Bow);
            Assert.That(m.RequestSpecial(), Is.True);
            m.Tick(1f / 60f, default);
            Assert.That(m.RequestSpecial(), Is.False);
            m.ClearSpecialBuffer();   // the player let go of the aim button
            bool started = false;
            for (int i = 0; i < 60; i++) started |= m.Tick(1f / 60f, default);
            Assert.That(started, Is.False);
        }
    }
}
