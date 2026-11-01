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

        const ulong GoldenSeed12345Floor2 = 18317817182715499079UL, GoldenSeed12345Floor3 = 10285151120262563894UL;   // computed once from the first run (floors 2 and 3 pin PickRooms' exclusion chain too)

        [Test]
        public void Golden_Seed12345_Floors2And3()
        {
            CellarFloorLayout l2 = CellarFloorGenerator.Generate(12345, 2), l3 = CellarFloorGenerator.Generate(12345, 3);
            Assert.That(l2.LayoutHash() == GoldenSeed12345Floor2 && l3.LayoutHash() == GoldenSeed12345Floor3,
                "golden changed; actual floor 2 = " + l2.LayoutHash() + "UL, floor 3 = " + l3.LayoutHash() + "UL\n" + l2.ToAscii() + "\n" + l3.ToAscii());
        }

        [Test]
        public void EdgeSeeds_NeverThrow_AndValidate()
        {
            foreach (uint seed in new uint[] { 0, 1, 0xFFFFFFFF, 0x80000000, 0x7FFFFFFF, unchecked((uint)-1), unchecked((uint)int.MinValue), (uint)int.MaxValue })
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = null;
                    Assert.DoesNotThrow(() => l = CellarFloorGenerator.Generate(seed, floor), "seed " + seed + " floor " + floor);
                    Assert.IsEmpty(CellarFloorValidator.Validate(l), "seed " + seed + " floor " + floor + "\n" + l.ToAscii());
                }
        }

        [Test]
        public void TwentyThousandRandomSeeds_NeverThrow_AndValidate()
        {
            var rng = new Pcg32(20261031UL);   // fixed stream across the full uint range
            var errs = new List<string>();
            int retry = 0, lastResort = 0, total = 0;
            for (int i = 0; i < 20000 && errs.Count < 8; i++)
            {
                uint seed = rng.NextUInt();
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l; int fb;
                    try { l = CellarFloorGenerator.Generate(seed, floor, out fb); }
                    catch (Exception e) { errs.Add("seed " + seed + " floor " + floor + " threw " + e.Message); continue; }
                    total++; if (fb == 1) retry++; else if (fb == 2) lastResort++;
                    List<string> v = CellarFloorValidator.Validate(l);
                    if (v.Count > 0) errs.Add(string.Join("; ", v) + "\n" + l.ToAscii());
                }
            }
            TestContext.WriteLine("SWEEP floors generated " + total + ", retry stream " + retry + ", last resort " + lastResort);
            Assert.IsEmpty(errs, string.Join("\n", errs));
        }

        [Test]
        public void LastResort_IsDeterministic_AndValid()
        {
            var rng = new Pcg32(77UL);
            int min = int.MaxValue, max = 0, marked = 0, n = 0; long sum = 0;
            for (int i = 0; i < 300; i++)
            {
                uint seed = rng.NextUInt();
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout a = CellarFloorGenerator.GenerateLastResort(seed, floor), b = CellarFloorGenerator.GenerateLastResort(seed, floor);
                    int ex = a.EntryToExitDistance(); min = Math.Min(min, ex); max = Math.Max(max, ex); sum += ex; n++; if (a.LastResort) marked++;
                    if (!a.LastResort) Assert.GreaterOrEqual(ex, CellarFloorGenerator.MinExitDistance, "an unmarked last resort meets the exit bound");
                    Assert.AreEqual(a.LayoutHash(), b.LayoutHash());
                    Assert.AreEqual(a.LastResort, b.LastResort);
                    Assert.IsEmpty(CellarFloorValidator.Validate(a), "seed " + seed + " floor " + floor + "\n" + a.ToAscii());
                    if (floor > 1) Assert.IsFalse(CellarFloorGenerator.MinimalPlan(seed, floor).Combat.Any(r => CellarFloorGenerator.PickRooms(seed, floor - 1).Combat.Contains(r)));
                }
            }
            TestContext.WriteLine("last resort exit distance: min " + min + ", mean " + (sum / (double)n).ToString("F1") + ", max " + max + ", relaxed (marked) " + marked + " of " + n);
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

        // ---- lock/key rule mutations: each breaks one rule of a real locked layout and the validator must say so ----

        static CellarFloorLayout RealLockedLayout()
        {
            for (uint seed = 0; seed < 500; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    if (l.HasLockedRoom && l.KeyChest != null && l.Carrier != null) { Assert.IsEmpty(CellarFloorValidator.Validate(l)); return l; }
                }
            Assert.Fail("no locked layout in 500 seeds"); return null;
        }

        static CellarFloorLayout With(CellarFloorLayout l, IEnumerable<FloorLink> links = null, IEnumerable<FloorChest> chests = null, int? lockedLink = null, int? keyIdx = null, GateKeyCarrier carrier = null)
            => new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, l.Corridors, links ?? l.Links, l.DeadEnds, chests ?? l.Chests, l.LockedRoomIndex,
                lockedLink ?? l.LockedLinkIndex, keyIdx ?? l.KeyChestIndex, carrier ?? l.Carrier);

        [Test]
        public void Validator_ExitOnlyBehindTheLock_Fails()
        {
            CellarFloorLayout l = RealLockedLayout();
            // moving the lock onto a link that the exit depends on makes the exit reachable only through the locked door
            bool caught = false;
            for (int k = 0; k < l.Links.Count && !caught; k++)
            {
                if (k == l.LockedLinkIndex) continue;
                caught = CellarFloorValidator.Validate(With(l, lockedLink: k)).Any(e => e.Contains("stair exit is not reachable"));
            }
            Assert.IsTrue(caught, "no link on the way to the exit made the validator complain\n" + l.ToAscii());
        }

        [Test]
        public void Validator_LockedRoomWithSecondLink_Fails()
        {
            CellarFloorLayout l = RealLockedLayout();
            int lr = l.LockedRoomIndex, other = (lr + 1) % l.Rooms.Count;
            var links = new List<FloorLink>(l.Links) { new FloorLink(lr, 0, other, 0, false, new CellPos[0]) };
            Assert.That(CellarFloorValidator.Validate(With(l, links: links)).Any(e => e.Contains("needs exactly 1")), "a second link on the locked room must fail");
        }

        [Test]
        public void Validator_KeyChestInTheLockedRoom_Fails()
        {
            CellarFloorLayout l = RealLockedLayout();
            var chests = new List<FloorChest>(l.Chests);
            FloorChest k = chests[l.KeyChestIndex];
            chests[l.KeyChestIndex] = new FloorChest(k.X, k.Y, l.LockedRoomIndex, true);
            Assert.That(CellarFloorValidator.Validate(With(l, chests: chests)).Any(e => e.Contains("key chest is in the locked room")));
        }

        [Test]
        public void Validator_CarrierBehindTheLock_Fails()
        {
            CellarFloorLayout l = RealLockedLayout();
            PlacedRoom lr = l.Rooms[l.LockedRoomIndex];
            CellarSpawn sp = lr.Room.Spawns[0]; CellPos w = lr.Cell(sp.X, sp.Y);
            List<string> errs = CellarFloorValidator.Validate(With(l, carrier: new GateKeyCarrier(l.LockedRoomIndex, 0, w.X, w.Y)));
            Assert.That(errs.Any(e => e.Contains("carrier is in the locked room")), string.Join("\n", errs));
            Assert.That(errs.Any(e => e.Contains("carrier is not reachable without passing the locked door")), string.Join("\n", errs));
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
