// Authority: none (pure EditMode tests). CellarPieceSpots on the Undercroft floors, the baker's spot and the satchel spot (plan 3 task 5, decision 10).
// Catches: a floor that cannot hold the four pieces decision 10 allows, a piece spot on floor 1/3/5, a baker with no spot on floor 3, a satchel spot on a wall, the stair, the party start or a piece spot.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarPieceSpotsTests
    {
        static readonly int[] PieceFloors = { 2, 4, 6, 7 };

        [Test]
        public void PieceSpots_FourPiecesOnOneFloor_HaveFourSpots()
        {
            for (uint seed = 0; seed < CellarTestFloors.FastSeeds; seed++)
                foreach (int floor in PieceFloors)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, floor);
                    IReadOnlyList<CellPos> spots = CellarPieceSpots.Choose(l, CellarTestFloors.Dressing(seed, floor), 4);
                    string tag = $" seed {seed} floor {floor}";
                    Assert.AreEqual(4, spots.Count, "four spots" + tag);
                    Assert.AreEqual(4, spots.Select(s => s.X * 1000 + s.Y).Distinct().Count(), "distinct" + tag);
                }
        }

        [Test]
        public void PieceSpots_FloorsOutsideTheRandomPool_HaveNone()
        {
            for (uint seed = 0; seed < 10; seed++)
                foreach (int floor in new[] { 1, 3, 5, 8 })
                {
                    if (floor > DiveFloorTable.LastBuiltFloor) continue;
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, floor);
                    Assert.AreEqual(0, CellarPieceSpots.Choose(l, CellarFloorDressing.Resolve(l), 4).Count, $"seed {seed} floor {floor}");
                }
        }

        [Test]
        public void BakerSpot_IsAFloorCellOnFloor3_AndNoneElsewhere()
        {
            for (uint seed = 0; seed < 20; seed++)
            {
                CellarFloorLayout l = CellarTestFloors.Layout(seed, 3);
                CellPos? baker = CellarPieceSpots.BakerSpot(l);
                Assert.That(baker.HasValue, "seed " + seed);
                int room = l.RoomAt(baker.Value.X, baker.Value.Y);
                Assert.That(room, Is.GreaterThanOrEqualTo(0));
                Assert.That(l.Rooms[room].Room.CellAt(baker.Value.X - l.Rooms[room].X, baker.Value.Y - l.Rooms[room].Y), Is.EqualTo(CellKind.Floor));
                foreach (int floor in new[] { 1, 2, 4, 5, 6, 7 })
                    Assert.That(CellarPieceSpots.BakerSpot(CellarTestFloors.Layout(seed, floor)).HasValue, Is.False, $"floor {floor}");
            }
        }

        [Test]
        public void SatchelSpot_IsFreeCellOnEveryGeneratedFloor()
        {
            for (uint seed = 0; seed < CellarTestFloors.FastSeeds; seed++)
                foreach (int floor in PieceFloors)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, floor);
                    CellarFloorDressing d = CellarTestFloors.Dressing(seed, floor);
                    CellPos s = CellarPieceSpots.SatchelSpot(l, seed);
                    string tag = $" seed {seed} floor {floor}";
                    int room = l.RoomAt(s.X, s.Y);
                    Assert.That(room, Is.GreaterThanOrEqualTo(0), "in a room" + tag);
                    Assert.That(l.Rooms[room].Room.CellAt(s.X - l.Rooms[room].X, s.Y - l.Rooms[room].Y), Is.EqualTo(CellKind.Floor), "floor cell" + tag);
                    Assert.That(l.EntryStarts.Any(p => p.X == s.X && p.Y == s.Y), Is.False, "party start" + tag);
                    Assert.That(l.StairExit.HasValue && l.StairExit.Value.X == s.X && l.StairExit.Value.Y == s.Y, Is.False, "stair" + tag);
                    Assert.That(l.Chests.Any(c => c.X == s.X && c.Y == s.Y), Is.False, "chest" + tag);
                    Assert.That(CellarPieceSpots.Choose(l, d, 4).Any(p => p.X == s.X && p.Y == s.Y), Is.False, "piece spot" + tag);
                    Assert.That(l.EndCell.HasValue && l.EndCell.Value.X == s.X && l.EndCell.Value.Y == s.Y, Is.False, "end cell" + tag);
                    Assert.That(room, Is.Not.EqualTo(l.RoomAt(l.EntryStarts[0].X, l.EntryStarts[0].Y)), "away from the party start" + tag);
                    CellPos again = CellarPieceSpots.SatchelSpot(l, seed);
                    Assert.That(again.X == s.X && again.Y == s.Y, "deterministic" + tag);
                }
        }
    }
}
