// Authority: none (pure EditMode tests). P47 dive healing in Core: CellarHealing (which floors hold a healing point and where), HealTuning, HealingPointState.
// Catches: a point that differs between peers, a share far from the 51% tuning, a point on floor 4, a point on a door, spawn, chest, socket, dead end, locked room or locked
// corridor, a point the party cannot walk to, a point that moves the layout or dressing goldens (own Rng stream), a recharge that never ends or restarts early.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarHealingTests
    {
        static CellPos? Point(uint seed, int floor)
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
            return CellarHealing.PointFor(l, CellarFloorDressing.Resolve(l));
        }

        [Test]
        public void Tuning_DefaultsAreThePhase1Numbers()
        {
            HealTuning t = HealTuning.Default;
            Assert.AreEqual(51, t.ChancePercent); Assert.AreEqual(30f, t.RechargeSeconds); Assert.Greater(t.RadiusMeters, 0.5f);
        }

        [Test]
        public void Point_IsDeterministic()
        {
            for (uint seed = 0; seed < 60; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellPos? a = Point(seed, floor), b = Point(seed, floor);
                    Assert.AreEqual(a.HasValue, b.HasValue, "seed " + seed + " floor " + floor);
                    if (a.HasValue) { Assert.AreEqual(a.Value.X, b.Value.X); Assert.AreEqual(a.Value.Y, b.Value.Y); }
                }
        }

        [Test]
        public void Share_OverAThousandSeeds_IsAboutHalf()
        {
            int with = 0, total = 0;
            for (uint seed = 1; seed <= 1000; seed++)
                for (int floor = 1; floor <= 3; floor++) { total++; if (Point(seed, floor).HasValue) with++; }
            double share = (double)with / total;
            Assert.That(share, Is.InRange(0.45, 0.57), "share " + share + " (" + with + "/" + total + ")");
        }

        [Test]
        public void EachFloor_1To3_HasItsOwnShareAboutHalf()
        {
            for (int floor = 1; floor <= 3; floor++)
            {
                int with = 0;
                for (uint seed = 1; seed <= 1000; seed++) if (Point(seed, floor).HasValue) with++;
                Assert.That(with / 1000.0, Is.InRange(0.44, 0.58), "floor " + floor + ": " + with + "/1000");
            }
        }

        [Test]
        public void Floor4_NeverHasAHealingPoint_AndTheStubIsNotRolled()
        {
            for (uint seed = 1; seed <= 200; seed++) Assert.IsFalse(Point(seed, 4).HasValue, "seed " + seed);
        }

        [Test]
        public void ChanceZero_NeverAndHundred_AlwaysWhenACellExists()
        {
            HealTuning none = HealTuning.Default; none.ChancePercent = 0;
            HealTuning all = HealTuning.Default; all.ChancePercent = 100;
            for (uint seed = 1; seed <= 100; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    Assert.IsFalse(CellarHealing.PointFor(l, d, none).HasValue);
                    Assert.IsTrue(CellarHealing.PointFor(l, d, all).HasValue, "seed " + seed + " floor " + floor + ": no legal cell");
                }
        }

        [Test]
        public void ChanceChange_DoesNotMoveTheCell()
        {
            HealTuning lo = HealTuning.Default; lo.ChancePercent = 60;
            for (uint seed = 1; seed <= 200; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellPos? a = CellarHealing.PointFor(l, d), b = CellarHealing.PointFor(l, d, lo);
                    if (a.HasValue) { Assert.IsTrue(b.HasValue); Assert.AreEqual(a.Value.X, b.Value.X); Assert.AreEqual(a.Value.Y, b.Value.Y); }
                }
        }

        [Test]
        public void Cell_IsLegal_Reachable_AndClearOfEverythingElse()
        {
            int checkedPoints = 0;
            for (uint seed = 1; seed <= 300; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellPos? p = CellarHealing.PointFor(l, d);
                    if (!p.HasValue) continue;
                    checkedPoints++;
                    string who = "seed " + seed + " floor " + floor + " at " + p.Value;
                    int[,] dist = l.WalkDistances();
                    Assert.GreaterOrEqual(dist[p.Value.X, p.Value.Y], 0, who + ": not reachable from the entry");
                    Assert.IsTrue(CellarHealing.Candidates(l, d).Any(c => c.X == p.Value.X && c.Y == p.Value.Y), who);
                    int room = l.RoomAt(p.Value.X, p.Value.Y);
                    if (room >= 0)
                    {
                        Assert.AreEqual(RoomKind.Combat, l.Rooms[room].Room.Kind, who + ": not a combat room");
                        Assert.AreNotEqual(l.LockedRoomIndex, room, who + ": in the locked room");
                        PlacedRoom pr = l.Rooms[room]; int lx = p.Value.X - pr.X, ly = p.Value.Y - pr.Y;
                        Assert.AreEqual(CellKind.Floor, pr.Room.CellAt(lx, ly), who);
                        foreach (CellDoor dr in pr.Room.Doors) Assert.Greater(System.Math.Abs(dr.X - lx) + System.Math.Abs(dr.Y - ly), 1, who + ": beside a door");
                        foreach (CellarSpawn s in pr.Room.Spawns) Assert.IsFalse(s.X == lx && s.Y == ly, who + ": on a spawn");
                        foreach (ChestSlot c in pr.Room.Chests) Assert.IsFalse(c.X == lx && c.Y == ly, who + ": on a chest slot");
                        foreach (DressingSocket k in pr.Room.Sockets)
                            for (int o = 0; o < k.CellOptions; o++) Assert.IsFalse(k.OptionCell(o).X == lx && k.OptionCell(o).Y == ly, who + ": on a socket cell");
                    }
                    else
                    {
                        PlacedCorridor c = l.CorridorAt(p.Value.X, p.Value.Y);
                        Assert.IsNotNull(c, who + ": neither room nor corridor");
                        Assert.AreEqual(CorridorShape.Straight, c.Piece.Shape, who);
                        if (l.LockedLinkIndex >= 0) Assert.IsFalse(l.Links[l.LockedLinkIndex].Path.Any(q => q.X == p.Value.X && q.Y == p.Value.Y), who + ": on the locked corridor");
                    }
                    foreach (FloorChest c in l.Chests) Assert.IsFalse(c.X == p.Value.X && c.Y == p.Value.Y, who + ": on a chest");
                    if (l.Carrier != null) Assert.IsFalse(l.Carrier.X == p.Value.X && l.Carrier.Y == p.Value.Y, who + ": on the key carrier");
                    foreach (PlacedVignette v in d.Vignettes) Assert.IsFalse(v.Cell.X == p.Value.X && v.Cell.Y == p.Value.Y, who + ": on a vignette");
                    foreach (CellPos de in l.DeadEnds) Assert.IsFalse(de.X == p.Value.X && de.Y == p.Value.Y, who + ": on a dead end");
                    for (int r = 0; r < l.Rooms.Count; r++)
                        for (int dd = 0; dd < l.Rooms[r].Room.Doors.Count; dd++)
                        {
                            CellPos o = l.Rooms[r].DoorOutside(dd);
                            Assert.IsFalse(o.X == p.Value.X && o.Y == p.Value.Y, who + ": outside a door");
                        }
                }
            Assert.Greater(checkedPoints, 300, "too few points to prove anything");
        }

        [Test]
        public void Goldens_Seed12345_AfterItem2()
        {
            Assert.AreEqual(2055763217958189686UL, CellarFloorGenerator.Generate(12345, 1).LayoutHash());
            Assert.AreEqual(1402270470201830695UL, CellarFloorGenerator.Generate(12345, 2).LayoutHash());
            Assert.AreEqual(11057337751887225001UL, CellarFloorGenerator.Generate(12345, 3).LayoutHash());
            Assert.AreEqual(8894238083671442472UL, CellarFloorGenerator.Generate(12345, 4).LayoutHash());
        }

        [Test]
        public void ComputingThePoint_DoesNotChangeLayoutOrDressingHashes()
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(777, 2);
            ulong lh = l.LayoutHash(); CellarFloorDressing d = CellarFloorDressing.Resolve(l); ulong dh = d.DressingHash();
            CellarHealing.PointFor(l, d);
            Assert.AreEqual(lh, l.LayoutHash());
            Assert.AreEqual(dh, CellarFloorDressing.Resolve(l).DressingHash());
        }

        [Test]
        public void WalkDistances_MatchesEntryToExitDistance()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, 1);
                Assert.AreEqual(l.EntryToExitDistance(), l.WalkDistances()[l.StairExit.Value.X, l.StairExit.Value.Y]);
            }
        }

        [Test]
        public void Recharge_SpendsThePointForTheTuningTime_ThenReadiesIt()
        {
            var s = new HealingPointState(30f);
            Assert.IsTrue(s.Ready);
            Assert.IsTrue(s.TryUse());
            Assert.IsFalse(s.Ready); Assert.IsFalse(s.TryUse(), "cannot reuse while recharging");
            s.Tick(29.9f); Assert.IsFalse(s.Ready);
            s.Tick(0.2f); Assert.IsTrue(s.Ready); Assert.AreEqual(0f, s.SecondsLeft);
            Assert.IsTrue(s.TryUse());
        }
    }
}
