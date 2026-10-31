// Authority: none (pure EditMode tests). The M5 Cellars floor generator (Core/Cellars/CellarFloorGenerator, CellarFloorLayout, CellarFloorValidator):
// the same (seed, floor) gives the same layout, a golden hash pins the algorithm, 10,000 seeds x floors 1-3 all validate, the validator fails on a broken
// layout, and the PHASE1 7.2 repetition metric holds over 1,000 seeds.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarFloorGeneratorTests
    {
        const ulong GoldenSeed12345Floor1 = 7160980227004277501UL;   // computed once from the first run; any change to the algorithm changes it on purpose

        [Test]
        public void SameSeedAndFloor_GiveIdenticalLayout()
        {
            foreach (uint seed in new uint[] { 0, 1, 2, 12345, 99999, 0xFFFFFFFF })
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout a = CellarFloorGenerator.Generate(seed, floor), b = CellarFloorGenerator.Generate(seed, floor);
                    Assert.AreEqual(a.LayoutHash(), b.LayoutHash(), "seed " + seed + " floor " + floor);
                    Assert.AreEqual(a.ToAscii(), b.ToAscii());
                    Assert.AreEqual(a.FootprintHash(), b.FootprintHash());
                }
        }

        [Test]
        public void Golden_Seed12345_Floor1()
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(12345, 1);
            Assert.AreEqual(GoldenSeed12345Floor1, l.LayoutHash(), "golden changed; actual = " + l.LayoutHash() + "UL\n" + l.ToAscii());
        }

        [Test]
        public void FloorSeed_IsAHash_NotASum()
        {
            Assert.AreNotEqual(CellarFloorGenerator.FloorSeed(1, 2), CellarFloorGenerator.FloorSeed(2, 1));   // seed + floor would collide (3 = 3)
            Assert.AreNotEqual(CellarFloorGenerator.Generate(1, 2).LayoutHash(), CellarFloorGenerator.Generate(2, 1).LayoutHash());
        }

        [Test]
        public void DifferentSeeds_GiveDifferentLayouts()
        {
            var hashes = new HashSet<ulong>();
            for (uint s = 0; s < 200; s++) hashes.Add(CellarFloorGenerator.Generate(s, 1).LayoutHash());
            Assert.Greater(hashes.Count, 190);
        }

        [Test]
        public void TenThousandSeeds_AllFloorsValidate()
        {
            var sw = Stopwatch.StartNew();
            var errs = new List<string>();
            for (uint seed = 0; seed < 10000 && errs.Count < 8; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l;
                    try { l = CellarFloorGenerator.Generate(seed, floor); }
                    catch (Exception e) { errs.Add("seed " + seed + " floor " + floor + " threw " + e.Message); continue; }
                    List<string> v = CellarFloorValidator.Validate(l);
                    if (v.Count > 0) errs.Add(string.Join("; ", v) + "\n" + l.ToAscii());
                }
            TestContext.WriteLine("10,000 seeds x 3 floors: " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
            Assert.IsEmpty(errs, string.Join("\n", errs));
        }

        [Test]
        public void Floor4_IsAFixedValidStub()
        {
            CellarFloorLayout a = CellarFloorGenerator.Generate(1, 4), b = CellarFloorGenerator.Generate(987654, 4);
            Assert.IsTrue(a.IsStub);
            Assert.AreEqual(a.LayoutHash(), b.LayoutHash(), "the stub does not depend on the seed");
            Assert.IsEmpty(CellarFloorValidator.Validate(a), string.Join("\n", CellarFloorValidator.Validate(a)) + "\n" + a.ToAscii());
        }

        [Test]
        public void BadFloorNumbers_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Generate(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Generate(1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.PickRooms(1, 4));
        }

        [Test]
        public void PickRooms_IsPure_AndExcludesThePreviousFloor()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                var f1 = CellarFloorGenerator.PickRooms(seed, 1);
                Assert.AreEqual(f1.Combat.Select(r => r.Id), CellarFloorGenerator.PickRooms(seed, 1).Combat.Select(r => r.Id));
                var f2 = CellarFloorGenerator.PickRooms(seed, 2);
                var f3 = CellarFloorGenerator.PickRooms(seed, 3);
                Assert.IsFalse(f2.Combat.Any(r => f1.Combat.Contains(r)), "seed " + seed + ": floors 1 and 2 share a combat room");
                Assert.IsFalse(f3.Combat.Any(r => f2.Combat.Contains(r)), "seed " + seed + ": floors 2 and 3 share a combat room");
            }
        }

        [Test]
        public void Validator_FailsOnABrokenLayout()
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(7, 1);
            Assert.IsEmpty(CellarFloorValidator.Validate(l));
            // a corridor cell with mask 0
            PlacedCorridor c0 = l.Corridors[0];
            var bad = new List<PlacedCorridor>(l.Corridors) { [0] = new PlacedCorridor(c0.X, c0.Y, 0, c0.Piece, 0) };
            var broken = new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, bad, l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, l.Carrier);
            Assert.That(CellarFloorValidator.Validate(broken).Any(e => e.Contains("mask 0")));
            // a missing chest
            var fewer = new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests.Take(3).ToList(), -1, -1, -1, l.Carrier);
            Assert.That(CellarFloorValidator.Validate(fewer).Any(e => e.Contains("chests")));
            // a dropped corridor cell leaves rooms disconnected
            var cut = new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, l.Corridors.Skip(1).ToList(), l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, l.Carrier);
            Assert.IsNotEmpty(CellarFloorValidator.Validate(cut));
        }

        [Test]
        public void RepetitionMetric_Over1000Seeds()
        {
            int locked = 0, floors = 0, combatSlots = 0, ownTheme = 0;
            for (uint seed = 0; seed < 1000; seed++)
            {
                var layouts = new CellarFloorLayout[4];
                var plans = new CellarFloorPlan[4];
                for (int f = 1; f <= 3; f++) { layouts[f] = CellarFloorGenerator.Generate(seed, f); plans[f] = CellarFloorGenerator.PickRooms(seed, f); }

                for (int f = 1; f <= 3; f++)
                {
                    var ids = layouts[f].Rooms.Select(r => r.RoomId).ToList();
                    Assert.AreEqual(ids.Count, ids.Distinct().Count(), "seed " + seed + " floor " + f + ": a room appears twice");
                    if (layouts[f].HasLockedRoom) locked++;
                    floors++;
                    foreach (CellarRoom r in plans[f].Combat) { combatSlots++; if (r.Theme == (SubTheme)f) ownTheme++; }
                }
                // consecutive floors share no combat room (entry and exit are shared by design)
                for (int f = 1; f < 3; f++)
                {
                    var a = layouts[f].Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Select(r => r.RoomId);
                    var b = layouts[f + 1].Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Select(r => r.RoomId);
                    Assert.IsEmpty(a.Intersect(b).ToList(), "seed " + seed + ": floors " + f + " and " + (f + 1) + " share a combat room");
                }
                // at least 3 distinct floor silhouettes (footprint hashes), and at least 3 distinct room silhouette tags across floors 1-3
                Assert.GreaterOrEqual(new[] { layouts[1].FootprintHash(), layouts[2].FootprintHash(), layouts[3].FootprintHash() }.Distinct().Count(), 3, "seed " + seed + ": floor footprints repeat");
                int tags = layouts.Skip(1).SelectMany(l => l.Rooms).Select(r => r.Room.Silhouette).Where(s => s.Length > 0).Distinct().Count();
                Assert.GreaterOrEqual(tags, 3, "seed " + seed + ": only " + tags + " distinct room silhouettes across floors 1-3");
            }
            double lockedShare = (double)locked / floors, ownShare = (double)ownTheme / combatSlots;
            TestContext.WriteLine("locked share " + lockedShare.ToString("F3") + ", own sub-theme share " + ownShare.ToString("F3") + " over " + floors + " floors");
            Assert.That(lockedShare, Is.InRange(0.40, 0.60), "about half the floors have a locked room");
            Assert.That(ownShare, Is.InRange(0.60, 0.80), "about 70% of combat rooms come from the floor's sub-theme");
        }

        [Test]
        public void Checkpoint_IsOnFloor2Only()
        {
            for (uint seed = 0; seed < 100; seed++)
            {
                Assert.IsFalse(CellarFloorGenerator.Generate(seed, 1).HasCheckpoint);
                Assert.IsTrue(CellarFloorGenerator.Generate(seed, 2).HasCheckpoint);
                Assert.IsFalse(CellarFloorGenerator.Generate(seed, 3).HasCheckpoint);
            }
        }
    }
}
