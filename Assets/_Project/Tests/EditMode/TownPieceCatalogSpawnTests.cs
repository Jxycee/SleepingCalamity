// M6 S1: ties the Core PieceCatalog to what M5 really spawns (DiveHaulSync.DefaultPiecesPerFloor in Presentation), so changing the per-floor counts without
// extending the catalog fails here.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownPieceCatalogSpawnTests
    {
        [Test]
        public void EveryPieceTheDefaultDiveCanSpawnMapsToAPart()
        {
            int[] perFloor = DiveHaulSync.DefaultPiecesPerFloor;
            var haul = new DiveHaul(perFloor);
            int spawnable = 0;
            for (int floor = 1; floor <= DiveHaul.PieceFloors; floor++)
                for (int i = 0; i < perFloor[floor - 1]; i++)
                {
                    int id = DiveHaul.PieceId(floor, i);
                    Assert.That(haul.OriginFloor(id), Is.EqualTo(floor), "M5 can spawn piece " + id);
                    Assert.That(PieceCatalog.TryGet(id, out PieceInfo info), Is.True, "piece " + id + " has no building part");
                    Assert.That(TownBuildings.Get(info.Building).PartIndex(info.Part), Is.GreaterThanOrEqualTo(0));
                    spawnable++;
                }
            Assert.That(spawnable, Is.EqualTo(haul.TotalPieces));
        }
    }
}
