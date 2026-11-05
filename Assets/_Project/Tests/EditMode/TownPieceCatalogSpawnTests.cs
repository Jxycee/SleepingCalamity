// M6 S1 / plan 3 task 12: ties the Core PieceCatalog to what the dive really places (PieceCatalog.Specs through PieceDepths.Assign), so adding a spec without a catalog entry, or a depth the
// floors cannot hold, fails here. Pure Core + NUnit.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TownPieceCatalogSpawnTests
    {
        [Test]
        public void EveryPieceTheDiveCanPlaceMapsToAPart_OnAFloorThatCanHoldIt()
        {
            for (ulong world = 1; world <= 60; world++)
            {
                IReadOnlyDictionary<int, int> depths = PieceDepths.Assign(world, PieceCatalog.Specs);
                Assert.That(depths.Count, Is.EqualTo(PieceCatalog.Specs.Count), "a depth for every spec, world " + world);
                foreach (PieceSpec spec in PieceCatalog.Specs)
                {
                    Assert.That(UndercroftPlan.IsFloor(depths[spec.Id]), Is.True, "piece " + spec.Id + " world " + world);
                    if (PieceCatalog.IsPerson(spec.Id)) continue;
                    Assert.That(PieceCatalog.TryGet(spec.Id, out PieceInfo info), Is.True, "piece " + spec.Id + " has no building part");
                    Assert.That(TownBuildings.Get(info.Building).PartIndex(info.Part), Is.GreaterThanOrEqualTo(0));
                }
                foreach (IGrouping<int, KeyValuePair<int, int>> floor in depths.Where(kv => !PieceCatalog.IsPerson(kv.Key)).GroupBy(kv => kv.Value))
                    Assert.That(floor.Count(), Is.LessThanOrEqualTo(4), "floor " + floor.Key + " world " + world + " (CellarPieceSpots gives four spots)");
            }
            Assert.That(PieceCatalog.All.Count, Is.EqualTo(PieceCatalog.Specs.Count - 1), "the catalog holds every spec but the baker");
        }
    }
}
