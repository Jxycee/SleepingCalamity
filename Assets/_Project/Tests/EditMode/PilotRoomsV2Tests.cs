// Tests/EditMode/PilotRoomsV2Tests.cs: the v2 pilot room (hanging hall) validates, keeps its pool seams and shows both water states (metrics gym plan, Task 5).
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class PilotRoomsV2Tests
    {
        [Test] public void Pilot_Validates() => Assert.That(RoomValidatorV2.Validate(PilotRoomsV2.HangingHall), Is.Empty);

        [Test] public void Pilot_KeepsThePoolRoomsSeams()
        {
            var v1 = CellarRoomPool.Find("larder_hanging_hall"); var b = PilotRoomsV2.HangingHall.Base;
            Assert.That(b.Id, Is.EqualTo(v1.Id)); Assert.That(b.Name, Is.EqualTo(v1.Name)); Assert.That(b.Kind, Is.EqualTo(v1.Kind));
            Assert.That(b.Theme, Is.EqualTo(v1.Theme)); Assert.That(b.Silhouette, Is.EqualTo(v1.Silhouette));
            Assert.That(b.Width, Is.EqualTo(v1.Width)); Assert.That(b.Height, Is.EqualTo(v1.Height));
            Assert.That(b.Doors.Count, Is.EqualTo(v1.Doors.Count));
            for (int i = 0; i < v1.Doors.Count; i++)
            {
                Assert.That(b.Doors[i].X, Is.EqualTo(v1.Doors[i].X)); Assert.That(b.Doors[i].Y, Is.EqualTo(v1.Doors[i].Y)); Assert.That(b.Doors[i].Side, Is.EqualTo(v1.Doors[i].Side));
            }
        }

        [Test] public void Pilot_IsMultiLevelAndTwoState()
        {
            var p = PilotRoomsV2.HangingHall;
            Assert.That(p.Affordances & (RoomAffordance.Water | RoomAffordance.Ledge), Is.EqualTo(RoomAffordance.Water | RoomAffordance.Ledge));
            Assert.That(RoomValidatorV2.StateCount(p), Is.EqualTo(2));
        }

        [Test] public void Pilot_FloodCutsOffTheBalcony()
        {
            var p = PilotRoomsV2.HangingHall;
            Assert.That(p.Standable(2, 2, new[] { 1 }), Is.False);
            Assert.That(p.Standable(2, 2, new[] { 0 }), Is.True);
        }

        [Test] public void Pilot_FitsAFullParty()
        {
            Assert.That(FightSpace.Fits(PilotRoomsV2.HangingHall.Base, 4, 8), Is.True);
            Assert.That(PilotRoomsV2.HangingHall.Base.FloorCount, Is.EqualTo(14));
        }

        [Test] public void Pilot_KeepsThePoolRoomsMarks()
        {
            var b = PilotRoomsV2.HangingHall.Base;
            Assert.That(b.Chests.Select(c => (c.X, c.Y)), Is.EquivalentTo(new[] { (3, 0), (1, 3) }));
            Assert.That(b.Spawns.Count, Is.EqualTo(3));
            Assert.That(Enumerable.Range(0, 16).Count(i => b.CellAt(i % 4, i / 4) == CellKind.Pillar), Is.EqualTo(2));
        }

        // The pool's variant names and fill chances are private there; the pilot copies them, so pin the copy to the pool room's own sockets.
        [Test] public void Pilot_DressingMatchesThePool()
        {
            var pool = CellarRoomPool.Find("larder_hanging_hall");
            foreach (DressingSocket s in PilotRoomsV2.HangingHall.Base.Sockets)
            {
                DressingSocket p = pool.Sockets.First(q => q.Kind == s.Kind);
                Assert.That(s.Variants, Is.EqualTo(p.Variants), s.Id);
                Assert.That(s.Chance, Is.EqualTo(p.Chance), s.Id);
            }
        }

        [Test] public void All_ListsThePilot() => Assert.That(PilotRoomsV2.All, Is.EqualTo(new[] { PilotRoomsV2.HangingHall }));
    }
}
