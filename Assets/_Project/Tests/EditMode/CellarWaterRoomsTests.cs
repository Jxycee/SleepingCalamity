// Tests/EditMode/CellarWaterRoomsTests.cs: the water-authored variants of the pool's combat rooms (dive 5b). Core-only.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarWaterRoomsTests
    {
        [Test]
        public void EveryVariant_ValidatesClean()
        {
            foreach (RoomDataV2 r in CellarWaterRooms.All)
                Assert.That(RoomValidatorV2.Validate(r), Is.Empty, r.Base.Id);
        }

        [Test]
        public void VariantsPlusUnsuitable_CoverAllTwelvePoolCombatRooms()
        {
            var combat = CellarRoomPool.All.Where(r => r.Kind == RoomKind.Combat).Select(r => r.Id).ToList();
            Assert.That(combat.Count, Is.EqualTo(12));
            var covered = CellarWaterRooms.All.Select(r => r.Base.Id).Concat(CellarWaterRooms.Unsuitable).ToList();
            Assert.That(covered, Is.EquivalentTo(combat));
            Assert.That(CellarWaterRooms.All.Count, Is.EqualTo(11));
            foreach (string id in CellarWaterRooms.Unsuitable) Assert.That(CellarWaterRooms.Find(id), Is.Null, id);
        }

        [Test]
        public void Variant_SharesThePoolRoomObject_AndItsData()
        {
            foreach (RoomDataV2 r in CellarWaterRooms.All)
            {
                CellarRoom pool = CellarRoomPool.Find(r.Base.Id);
                Assert.That(ReferenceEquals(r.Base, pool), Is.True, r.Base.Id);
                Assert.That(r.Base.Width, Is.EqualTo(pool.Width)); Assert.That(r.Base.Height, Is.EqualTo(pool.Height));
                Assert.That(r.Base.Doors.Count, Is.EqualTo(pool.Doors.Count), r.Base.Id);
                Assert.That(r.Base.Spawns.Count, Is.EqualTo(pool.Spawns.Count), r.Base.Id);
                Assert.That(r.Doors.Count, Is.EqualTo(pool.Doors.Count), r.Base.Id);
            }
        }

        [Test]
        public void EachRoom_HasOneSump_OfTwoToFourCells_AndStatesThatDifferInStandableCells()
        {
            foreach (RoomDataV2 r in CellarWaterRooms.All)
            {
                Assert.That(r.Regions.Count, Is.EqualTo(1), r.Base.Id);
                GimmickRegion g = r.Regions[0];
                Assert.That(g.Kind, Is.EqualTo(GimmickKind.Water), r.Base.Id);
                Assert.That(g.Cells.Count, Is.InRange(2, 4), r.Base.Id);
                Assert.That(g.StateCount, Is.GreaterThanOrEqualTo(2), r.Base.Id);
                int Standable(int s) => g.Cells.Count(c => r.Standable(c.X, c.Y, new[] { s }));
                var counts = Enumerable.Range(0, g.StateCount).Select(Standable).Distinct().Count();
                Assert.That(counts, Is.GreaterThanOrEqualTo(2), r.Base.Id + " states never differ");
                Assert.That(g.StateValues.Contains(0), Is.True, r.Base.Id + " has no drained state");
                Assert.That(g.StateValues.Any(v => v > GameMetrics.WadeMaxCm), Is.True, r.Base.Id + " has no flooded state");
            }
        }

        [Test]
        public void Find_ReturnsTheVariant_OrNull()
        {
            Assert.That(CellarWaterRooms.Find("larder_pantry").Base.Id, Is.EqualTo("larder_pantry"));
            Assert.That(CellarWaterRooms.Find("nope"), Is.Null);
        }

        [Test]
        public void ValveSockets_NeedNoTool()
        {
            foreach (RoomDataV2 r in CellarWaterRooms.All)
                foreach (ToolSocket t in r.Sockets) { Assert.That(t.Tool, Is.EqualTo(ToolVerb.None), t.Id); Assert.That(t.Purpose, Is.EqualTo(SocketPurpose.Control), t.Id); }
        }
    }
}
