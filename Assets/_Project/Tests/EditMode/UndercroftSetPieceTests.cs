// Authority: none (pure EditMode tests). The stand-in Undercroft floors 1, 3, 5 (UndercroftSetPieces, CellarFloorGenerator.Layout) and the floor-7 EndCell (plan 3 task 4).
// Catches: a set piece that moves with the seed, a floor that fails the validator, a baker / guardian / bank room missing or reachable around its guard, a stair on the bank floor,
// an end cell that is the stair or unreachable, the generator entry points accepting the wrong floors. Does not catch: how the stand-ins look or play.
using System;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class UndercroftSetPieceTests
    {
        [Test]
        public void Floors1_3_5_Validate_ForSeeds1To20()
        {
            for (uint seed = 1; seed <= 20; seed++)
                foreach (int floor in new[] { 1, 3, 5 })
                {
                    CellarFloorLayout l = UndercroftSetPieces.Build(seed, floor);
                    Assert.IsEmpty(CellarFloorValidator.Validate(l), "seed " + seed + " floor " + floor + "\n" + l.ToAscii());
                    Assert.AreEqual(floor, l.Floor);
                    Assert.AreEqual(l.LayoutHash(), CellarFloorGenerator.Layout(seed, floor).LayoutHash(), "Layout dispatches to the set pieces");
                }
        }

        [Test]
        public void SetPieces_AreTheSameForEverySeed()
        {
            foreach (int floor in new[] { 1, 3, 5 })
            {
                CellarFloorLayout first = UndercroftSetPieces.Build(1, floor);
                for (uint seed = 2; seed <= 20; seed++)
                {
                    CellarFloorLayout l = UndercroftSetPieces.Build(seed, floor);
                    Assert.AreEqual(first.LayoutHash(), l.LayoutHash(), "floor " + floor + " seed " + seed);
                    Assert.AreEqual(first.ToAscii(), l.ToAscii());
                }
            }
        }

        [Test]
        public void Floor1_IsTheAuthoredLarder_WithAKeyCarrierInTheHangingHall()
        {
            CellarFloorLayout l = UndercroftSetPieces.Build(1, 1);
            Assert.AreEqual(new[] { "cellar_entry", "larder_pantry", "larder_hanging_hall", "cellar_exit" }, l.Rooms.Select(r => r.RoomId).ToArray());
            Assert.IsFalse(l.IsStub); Assert.AreEqual(SubTheme.Larder, l.Theme); Assert.AreEqual(LayoutPreset.Spine, l.Preset);
            Assert.IsNotNull(l.Carrier); Assert.AreEqual("larder_hanging_hall", l.Rooms[l.Carrier.RoomIndex].RoomId);
            Assert.IsFalse(l.HasLockedRoom);
            Assert.IsTrue(l.StairExit.HasValue); Assert.GreaterOrEqual(l.EntryToExitDistance(), CellarFloorGenerator.MinExitDistance);
            Assert.IsFalse(l.EndCell.HasValue); Assert.IsFalse(l.BankRoomCell.HasValue);
        }

        [Test]
        public void Floor3_HasTheBaker_AndNoGuardianOrBank()
        {
            CellarFloorLayout l = UndercroftSetPieces.Build(1, 3);
            Assert.IsTrue(l.BakerCell.HasValue);
            Assert.IsFalse(l.GuardianCell.HasValue); Assert.IsFalse(l.WardenCell.HasValue); Assert.IsFalse(l.BankRoomCell.HasValue);
            Assert.IsTrue(l.StairExit.HasValue, "a stair exit instead of the way home");
            Assert.IsTrue(l.IsStub);
            Assert.GreaterOrEqual(l.WalkDistances()[l.BakerCell.Value.X, l.BakerCell.Value.Y], 0, "the baker is reachable");
            Assert.GreaterOrEqual(l.EntryToExitDistance(), 0, "the stair is reachable");
        }

        [Test]
        public void Floor5_HasTheGuardianArena_AndTheBankRoomBehindIt_NoStair()
        {
            CellarFloorLayout l = UndercroftSetPieces.Build(1, 5);
            Assert.IsTrue(l.WardenCell.HasValue && l.GuardianCell.HasValue && l.BankRoomCell.HasValue);
            Assert.AreEqual(l.WardenCell.Value.X, l.GuardianCell.Value.X); Assert.AreEqual(l.WardenCell.Value.Y, l.GuardianCell.Value.Y);
            Assert.IsFalse(l.StairExit.HasValue, "no stair: the bank room is the way on");
            Assert.AreEqual(0, l.Rooms.Count(r => r.Room.Kind == RoomKind.StairExit));
            Assert.IsTrue(l.HasCheckpoint);
            int arena = l.RoomAt(l.GuardianCell.Value.X, l.GuardianCell.Value.Y), bank = l.RoomAt(l.BankRoomCell.Value.X, l.BankRoomCell.Value.Y);
            Assert.AreNotEqual(arena, bank);
            Assert.AreEqual(1, l.Links.Count(k => k.Touches(bank) && k.RoomB >= 0), "the bank room has one link");
            Assert.IsTrue(l.Links.Single(k => k.Touches(bank)).Touches(arena), "and it is the arena");
            Assert.GreaterOrEqual(l.WalkDistances()[l.BankRoomCell.Value.X, l.BankRoomCell.Value.Y], 0, "the bank room is reachable");
        }

        [Test]
        public void Floor7_HasAnEndCell_ReachableAndNotTheStair()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, 7);
                Assert.IsTrue(l.EndCell.HasValue, "seed " + seed);
                Assert.IsTrue(l.StairExit.HasValue);
                Assert.IsFalse(l.EndCell.Value.X == l.StairExit.Value.X && l.EndCell.Value.Y == l.StairExit.Value.Y, "the end cell is not the stair");
                Assert.GreaterOrEqual(l.WalkDistances()[l.EndCell.Value.X, l.EndCell.Value.Y], 0, "reachable from the party start, seed " + seed);
                Assert.AreEqual(l.ExitRoomIndex(), l.RoomAt(l.EndCell.Value.X, l.EndCell.Value.Y));
                foreach (int floor in new[] { 1, 2, 3, 4, 5, 6 }) Assert.IsFalse(CellarFloorGenerator.Layout(seed, floor).EndCell.HasValue, "only floor 7 has an end cell");
            }
        }

        [Test]
        public void CollapseTargetCell_UsesTheBankRoomOnFloor5_AndTheEndCellOnFloor7()
        {
            CellarFloorLayout l5 = CellarFloorGenerator.Layout(1, 5), l7 = CellarFloorGenerator.Layout(1, 7), l4 = CellarFloorGenerator.Layout(1, 4);
            Assert.AreEqual(l5.BankRoomCell.Value.X, DiveFloorTable.CollapseTargetCell(l5).Value.X); Assert.AreEqual(l5.BankRoomCell.Value.Y, DiveFloorTable.CollapseTargetCell(l5).Value.Y);
            Assert.AreEqual(l7.EndCell.Value.X, DiveFloorTable.CollapseTargetCell(l7).Value.X); Assert.AreEqual(l7.EndCell.Value.Y, DiveFloorTable.CollapseTargetCell(l7).Value.Y);
            Assert.AreEqual(l4.StairExit.Value.X, DiveFloorTable.CollapseTargetCell(l4).Value.X);
        }

        [Test]
        public void WrongFloors_Throw()
        {
            foreach (int floor in new[] { 1, 3, 5 }) Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Generate(1, floor), "Generate " + floor);
            foreach (int floor in new[] { 2, 4, 6, 7, 8 }) Assert.Throws<ArgumentOutOfRangeException>(() => UndercroftSetPieces.Build(1, floor), "Build " + floor);
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Layout(1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Layout(1, 9));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Layout(1, 10));
        }
    }
}
