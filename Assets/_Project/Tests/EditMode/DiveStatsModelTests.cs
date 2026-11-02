using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveStatsModelTests
    {
        [Test]
        public void Damage_AccumulatesLosses_AndIgnoresHeals()
        {
            var m = new DiveStatsModel();
            m.Observe(0, true, 1, 100, 2, 0);
            m.Observe(1, true, 1, 70, 2, 0);     // -30
            m.Observe(2, true, 1, 90, 2, 0);     // heal, no damage
            m.Observe(3, true, 1, 80, 2, 0);     // -10
            Assert.That(m.DamageTaken, Is.EqualTo(40));
        }

        [Test]
        public void Heals_CountLoavesAndOtherJumps_NotDoubled()
        {
            var m = new DiveStatsModel();
            m.Observe(0, true, 1, 50, 2, 0);
            m.Observe(1, true, 1, 80, 1, 0);     // loaf eaten: one heal, one loaf
            m.Observe(2, true, 1, 60, 1, 0);
            m.Observe(3, true, 1, 100, 1, 0);    // bank heal: one heal, no loaf
            Assert.That(m.LoavesEaten, Is.EqualTo(1));
            Assert.That(m.HealsUsed, Is.EqualTo(2));
        }

        [Test]
        public void NewDive_Resets()
        {
            var m = new DiveStatsModel();
            m.Observe(0, true, 1, 100, 2, 0);
            m.Observe(5, true, 1, 50, 2, 0);
            m.Observe(10, false, 1, 50, 2, 0);
            Assert.That(m.DiveSeconds(99), Is.EqualTo(10).Within(1e-6), "frozen at the end");
            m.Observe(20, true, 1, 100, 3, 0);
            Assert.That(m.DamageTaken, Is.EqualTo(0));
            Assert.That(m.DiveSeconds(25), Is.EqualTo(5).Within(1e-6));
            Assert.That(m.RoomClears.Count, Is.EqualTo(0));
        }

        [Test]
        public void RoomClear_UsesFirstSpawn_ElseFloorStart_AndResetsPerFloor()
        {
            var m = new DiveStatsModel();
            m.Observe(0, true, 1, 100, 0, 0);
            m.NoteRoomEntered(2, 10);
            m.NoteRoomEntered(2, 12);            // first one wins
            m.Observe(30, true, 1, 100, 0, 1UL << 2);
            m.Observe(40, true, 1, 100, 0, (1UL << 2) | (1UL << 5));   // peer-style: room 5 never seen entering
            Assert.That(m.RoomClears.Count, Is.EqualTo(2));
            Assert.That(m.RoomClears[0].Room, Is.EqualTo(2));
            Assert.That(m.RoomClears[0].Seconds, Is.EqualTo(20).Within(1e-6));
            Assert.That(m.RoomClears[0].FromFirstSpawn, Is.True);
            Assert.That(m.RoomClears[1].Seconds, Is.EqualTo(40).Within(1e-6));
            Assert.That(m.RoomClears[1].FromFirstSpawn, Is.False);
            m.Observe(50, true, 2, 100, 0, 0);   // next floor
            Assert.That(m.RoomClears.Count, Is.EqualTo(0));
            Assert.That(m.Floor, Is.EqualTo(2));
        }

        [Test]
        public void FormatTime_MinutesAndSeconds()
        {
            Assert.That(DiveStatsModel.FormatTime(0), Is.EqualTo("0:00"));
            Assert.That(DiveStatsModel.FormatTime(65.9), Is.EqualTo("1:05"));
            Assert.That(DiveStatsModel.FormatTime(-3), Is.EqualTo("0:00"));
            Assert.That(DiveStatsModel.FormatTime(3600), Is.EqualTo("60:00"));
        }
    }
}
