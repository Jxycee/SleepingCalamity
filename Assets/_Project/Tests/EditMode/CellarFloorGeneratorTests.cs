// Authority: none (pure EditMode tests). The M5 Cellars floor generator (Core/Cellars/CellarFloorGenerator, CellarFloorLayout, CellarFloorValidator) as the new dive uses it
// (plan 3 tasks 3 and 4): the GENERATED floors 2, 4, 6, 7 come from Generate, the fixed floors 1, 3, 5 from Layout (UndercroftSetPieces). The same (seed, floor) gives the same
// layout, a golden hash pins the algorithm, thousands of seeds x floors 1-7 all validate, the validator fails on a broken layout, and the PHASE1 7.2 repetition metric holds.
// Catches: a generated floor that repeats the previous generated floor's combat rooms, a floor outside the table accepted, a set piece that is not fixed for every seed, a bakery heart
// or bank room reachable around its guard. Does not catch: how the floors look or play (no dressing, no encounters here).
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
        static readonly int[] GeneratedFloors = { 2, 4, 6, 7 };

        const ulong GoldenSeed12345Floor1 = 8462212200985256396UL;   // floor 1 is authored: the same for every seed (plan 3 task 4); pinned from the failure message

        [Test]
        public void SameSeedAndFloor_GiveIdenticalLayout()
        {
            foreach (uint seed in new uint[] { 0, 1, 2, 12345, 99999, 0xFFFFFFFF })
                for (int floor = 1; floor <= 7; floor++)
                {
                    CellarFloorLayout a = CellarFloorGenerator.Layout(seed, floor), b = CellarFloorGenerator.Layout(seed, floor);
                    Assert.AreEqual(a.LayoutHash(), b.LayoutHash(), "seed " + seed + " floor " + floor);
                    Assert.AreEqual(a.ToAscii(), b.ToAscii());
                    Assert.AreEqual(a.FootprintHash(), b.FootprintHash());
                }
        }

        [Test]
        public void Generate_Floors2_4_6_7_AreValidAndDeterministic()
        {
            for (uint seed = 1; seed <= 40; seed++)
                foreach (int floor in GeneratedFloors)
                {
                    CellarFloorLayout a = CellarFloorGenerator.Generate(seed, floor), b = CellarFloorGenerator.Generate(seed, floor);
                    Assert.IsEmpty(CellarFloorValidator.Validate(a), "seed " + seed + " floor " + floor + "\n" + a.ToAscii());
                    Assert.AreEqual(a.LayoutHash(), b.LayoutHash(), "seed " + seed + " floor " + floor);
                    Assert.IsFalse(a.IsStub);
                    Assert.That(a.Rooms.Count, Is.InRange(4, 6));
                    Assert.IsFalse(a.HasCheckpoint, "no lamp nook on a generated floor");
                }
        }

        [Test]
        public void Golden_Seed12345_Floor1()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(12345, 1);
            Assert.AreEqual(GoldenSeed12345Floor1, l.LayoutHash(), "golden changed; actual = " + l.LayoutHash() + "UL\n" + l.ToAscii());
        }

        const ulong GoldenSeed12345Floor2 = 16607234616487640366UL, GoldenSeed12345Floor4 = 4666321676584969224UL, GoldenSeed12345Floor6 = 1444249743357433504UL, GoldenSeed12345Floor7 = 12024394103596814071UL;   // pinned from the failure message

        [Test]
        public void Golden_Seed12345_GeneratedFloors()
        {
            var got = GeneratedFloors.Select(f => CellarFloorGenerator.Generate(12345, f)).ToArray();
            Assert.That(got[0].LayoutHash() == GoldenSeed12345Floor2 && got[1].LayoutHash() == GoldenSeed12345Floor4 && got[2].LayoutHash() == GoldenSeed12345Floor6 && got[3].LayoutHash() == GoldenSeed12345Floor7,
                "golden changed; actual floor 2 = " + got[0].LayoutHash() + "UL, floor 4 = " + got[1].LayoutHash() + "UL, floor 6 = " + got[2].LayoutHash() + "UL, floor 7 = " + got[3].LayoutHash() + "UL\n"
                + string.Join("\n", got.Select(g => g.ToAscii())));
        }

        [Test]
        public void EdgeSeeds_NeverThrow_AndValidate()
        {
            foreach (uint seed in new uint[] { 0, 1, 0xFFFFFFFF, 0x80000000, 0x7FFFFFFF, unchecked((uint)-1), unchecked((uint)int.MinValue), (uint)int.MaxValue })
                for (int floor = 1; floor <= 7; floor++)
                {
                    CellarFloorLayout l = null;
                    Assert.DoesNotThrow(() => l = CellarFloorGenerator.Layout(seed, floor), "seed " + seed + " floor " + floor);
                    Assert.IsEmpty(CellarFloorValidator.Validate(l), "seed " + seed + " floor " + floor + "\n" + l.ToAscii());
                }
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(4000, Category = "Soak"), Timeout(300000)]   // presets made a floor a little dearer (retries on locked chains); the seed count is sized to fit well inside this
        public void RandomSeeds_NeverThrow_AndValidate(int count)
        {
            var rng = new Pcg32(20261031UL);   // fixed stream across the full uint range
            var errs = new List<string>();
            int retry = 0, lastResort = 0, total = 0;
            for (int i = 0; i < count && errs.Count < 8; i++)
            {
                uint seed = rng.NextUInt();
                for (int floor = 1; floor <= 7; floor++)
                {
                    CellarFloorLayout l; int fb;
                    try { l = CellarFloorGenerator.Layout(seed, floor, out fb); }
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
                foreach (int floor in GeneratedFloors)
                {
                    CellarFloorLayout a = CellarFloorGenerator.GenerateLastResort(seed, floor), b = CellarFloorGenerator.GenerateLastResort(seed, floor);
                    int ex = a.EntryToExitDistance(); min = Math.Min(min, ex); max = Math.Max(max, ex); sum += ex; n++; if (a.LastResort) marked++;
                    if (!a.LastResort) Assert.GreaterOrEqual(ex, CellarFloorGenerator.MinExitDistance, "an unmarked last resort meets the exit bound");
                    Assert.AreEqual(a.LayoutHash(), b.LayoutHash());
                    Assert.AreEqual(a.LastResort, b.LastResort);
                    Assert.IsEmpty(CellarFloorValidator.Validate(a), "seed " + seed + " floor " + floor + "\n" + a.ToAscii());
                    int prev = CellarFloorGenerator.PreviousGeneratedFloor(floor);
                    if (prev > 0) Assert.IsFalse(CellarFloorGenerator.MinimalPlan(seed, floor).Combat.Any(r => CellarFloorGenerator.PickRooms(seed, prev).Combat.Contains(r)));
                }
            }
            TestContext.WriteLine("last resort exit distance: min " + min + ", mean " + (sum / (double)n).ToString("F1") + ", max " + max + ", relaxed (marked) " + marked + " of " + n);
        }

        [Test]
        public void MinimalPlan_And_LastResort_AcceptEveryGeneratedFloor()
        {
            foreach (int floor in GeneratedFloors)
            {
                CellarFloorPlan plan = CellarFloorGenerator.MinimalPlan(5, floor);
                Assert.AreEqual(3, plan.Combat.Count, "floor " + floor);
                Assert.IsFalse(plan.Locked);
                Assert.IsEmpty(CellarFloorValidator.Validate(CellarFloorGenerator.GenerateLastResort(5, floor)), "floor " + floor);
            }
            foreach (int floor in new[] { 1, 3, 5, 8 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.MinimalPlan(5, floor), "MinimalPlan " + floor);
                Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.GenerateLastResort(5, floor), "GenerateLastResort " + floor);
            }
        }

        [Test]
        public void FloorSeed_IsAHash_NotASum()
        {
            Assert.AreNotEqual(CellarFloorGenerator.FloorSeed(1, 2), CellarFloorGenerator.FloorSeed(2, 1));   // seed + floor would collide (3 = 3)
            Assert.AreNotEqual(CellarFloorGenerator.Layout(1, 2).LayoutHash(), CellarFloorGenerator.Layout(2, 1).LayoutHash());
        }

        [Test]
        public void DifferentSeeds_GiveDifferentLayouts()
        {
            var hashes = new HashSet<ulong>();
            for (uint s = 0; s < 200; s++) hashes.Add(CellarFloorGenerator.Generate(s, 2).LayoutHash());
            Assert.Greater(hashes.Count, 190);
        }

        [TestCase(CellarTestFloors.FastSeeds), TestCase(2000, Category = "Soak"), Timeout(300000)]   // presets made a floor a little dearer (retries on locked chains); the seed count is sized to fit well inside this
        public void ManySeeds_AllFloorsValidate(int seeds)
        {
            var sw = Stopwatch.StartNew();
            var errs = new List<string>();
            for (uint seed = 0; seed < seeds && errs.Count < 8; seed++)
                for (int floor = 1; floor <= 7; floor++)
                {
                    CellarFloorLayout l;
                    try { l = CellarTestFloors.Layout(seed, floor); }
                    catch (Exception e) { errs.Add("seed " + seed + " floor " + floor + " threw " + e.Message); continue; }
                    List<string> v = CellarFloorValidator.Validate(l);
                    if (v.Count > 0) errs.Add(string.Join("; ", v) + "\n" + l.ToAscii());
                }
            TestContext.WriteLine(seeds + " seeds x 7 floors: " + sw.Elapsed.TotalSeconds.ToString("F1") + " s");
            Assert.IsEmpty(errs, string.Join("\n", errs));
        }

        const ulong GoldenFloor3 = 3485229907679376271UL;   // the fixed bakery stand-in; the same for every seed; pinned from the failure message

        static readonly uint[] Floor3Seeds = { 0, 1, 12345, 987654, 0x80000000, 0xFFFFFFFF };

        [Test]
        public void Floor3_IsTheFixedBakery_ForEverySeed()
        {
            CellarFloorLayout first = CellarFloorGenerator.Layout(Floor3Seeds[0], 3);
            Assert.IsTrue(first.IsStub, "IsStub marks the fixed set pieces");
            Assert.AreEqual(SubTheme.Bakery, first.Theme);
            foreach (uint seed in Floor3Seeds)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, 3);
                Assert.AreEqual(first.LayoutHash(), l.LayoutHash(), "the layout does not depend on the seed " + seed);
                Assert.AreEqual(first.ToAscii(), l.ToAscii());
                Assert.IsEmpty(CellarFloorValidator.Validate(l), string.Join("\n", CellarFloorValidator.Validate(l)) + "\n" + l.ToAscii());
            }
        }

        [Test]
        public void Golden_Floor3_Bakery()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(12345, 3);
            Assert.AreEqual(GoldenFloor3, l.LayoutHash(), "golden changed; actual = " + l.LayoutHash() + "UL\n" + l.ToAscii());
        }

        static int IndexOf(CellarFloorLayout l, System.Func<PlacedRoom, bool> pick)
        {
            for (int i = 0; i < l.Rooms.Count; i++) if (pick(l.Rooms[i])) return i;
            return -1;
        }

        [Test]
        public void Floor3_HasAStairAndABaker_ButNoKeyLockCarrierBankOrWarden()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(5, 3);
            Assert.IsTrue(l.StairExit.HasValue, "a stair exit instead of the way home");
            Assert.AreEqual(1, l.Rooms.Count(r => r.Room.Kind == RoomKind.StairExit));
            Assert.IsFalse(l.HasLockedRoom); Assert.IsNull(l.KeyChest); Assert.IsNull(l.Carrier); Assert.IsFalse(l.Links.Any(k => k.Locked));
            Assert.IsTrue(l.BakerCell.HasValue);
            Assert.IsFalse(l.BankCell.HasValue || l.BankRoomCell.HasValue, "no bank on floor 3");
            Assert.IsFalse(l.WardenCell.HasValue, "the antechamber is calm");
            int heart = IndexOf(l, p => p.Room.Kind == RoomKind.Heart), ante = IndexOf(l, p => p.RoomId == CellarBakeryPool.AntechamberCalmId);
            Assert.AreEqual(heart, l.RoomAt(l.BakerCell.Value.X, l.BakerCell.Value.Y), "the baker is in the heart");
            Assert.GreaterOrEqual(ante, 0);
            Assert.IsFalse(l.Rooms[ante].Room.Spawns.Any(sp => sp.Tier == SpawnTier.Tier2), "no Tier2 spawn in the calm antechamber");
        }

        // Room-level graph of the links, skipping link `skip` and entering no link of room `blockRoom`; true when `to` is reached from `from`.
        static bool RoomsConnected(CellarFloorLayout l, int from, int to, int skip = -1, int blockRoom = -1)
        {
            var seen = new HashSet<int> { from }; var q = new Queue<int>(); q.Enqueue(from);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                for (int k = 0; k < l.Links.Count; k++)
                {
                    FloorLink lk = l.Links[k];
                    if (k == skip || lk.RoomB < 0 || !lk.Touches(r)) continue;
                    int o = lk.RoomA == r ? lk.RoomB : lk.RoomA;
                    if (o == blockRoom || !seen.Add(o)) continue;
                    q.Enqueue(o);
                }
            }
            return seen.Contains(to);
        }

        [Test]
        public void Floor3_HeartAndStairAreOnlyApproachedThroughTheAntechamber()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(1, 3);
            int entry = IndexOf(l, p => p.Room.Kind == RoomKind.Entry), heart = IndexOf(l, p => p.Room.Kind == RoomKind.Heart), ante = IndexOf(l, p => p.RoomId == CellarBakeryPool.AntechamberCalmId);
            int exit = l.ExitRoomIndex();
            Assert.AreEqual(1, l.Rooms[heart].Room.Doors.Count, "the heart has a single door");
            Assert.AreEqual(1, l.Links.Count(k => k.Touches(heart)), "one link into the heart");
            Assert.IsTrue(l.Links.Single(k => k.Touches(heart)).Touches(ante), "and it comes from the antechamber");
            Assert.IsTrue(RoomsConnected(l, entry, heart), "the heart is reachable");
            Assert.IsFalse(RoomsConnected(l, entry, heart, blockRoom: ante), "but not round the antechamber");
            Assert.IsTrue(RoomsConnected(l, entry, exit), "the stair hall is reachable");
            Assert.IsFalse(RoomsConnected(l, entry, exit, blockRoom: ante), "but not round the antechamber");
            Assert.IsEmpty(CellarFloorValidator.Validate(l));   // the validator's cell-level walk checks the same on the real corridors
        }

        [Test]
        public void Floor3_MazeHasALoop_SoThereAreTwoRoutesThroughIt()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(1, 3);
            int entry = IndexOf(l, p => p.Room.Kind == RoomKind.Entry), ante = IndexOf(l, p => p.RoomId == CellarBakeryPool.AntechamberCalmId);
            int ovens = l.Rooms.Count(p => p.Room.Kind == RoomKind.Combat && p.Room.Theme == SubTheme.Bakery && p.RoomId != CellarBakeryPool.AntechamberCalmId);
            Assert.That(ovens, Is.InRange(4, 6), "the oven maze has 4-6 rooms");
            Assert.GreaterOrEqual(l.Links.Count - (l.Rooms.Count - 1), 1, "at least one loop");
            int ring = 0;   // links inside the maze whose loss still leaves a route entry -> antechamber
            for (int k = 0; k < l.Links.Count; k++)
                if (!l.Links[k].Touches(entry) && !l.Links[k].Touches(ante) && RoomsConnected(l, entry, ante, skip: k)) ring++;
            Assert.GreaterOrEqual(ring, 4, "the four ring links each leave a second route");
        }

        [Test]
        public void Floor3_HasSixToEightChests_AllInRoomsOnFloorCells()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(1, 3);
            Assert.That(l.Chests.Count, Is.InRange(6, 8));
            Assert.IsTrue(l.Chests.All(c => c.RoomIndex >= 0 && !c.HoldsKey));
        }

        [Test]
        public void GeneratedFloors_NeverDrawBakeryRooms_AndThePoolStaysSeparate()
        {
            Assert.IsEmpty(CellarBakeryPool.Validate(), string.Join("\n", CellarBakeryPool.Validate()));
            foreach (CellarRoom r in CellarBakeryPool.All) Assert.IsNull(CellarRoomPool.Find(r.Id), r.Id + " must not be in the cellar pool");
            Assert.IsFalse(CellarRoomPool.All.Any(r => r.Theme == SubTheme.Bakery || r.Kind == RoomKind.Heart));
            for (uint seed = 0; seed < 100; seed++)
                foreach (int floor in GeneratedFloors)
                    Assert.IsFalse(CellarFloorGenerator.Generate(seed, floor).Rooms.Any(p => CellarBakeryPool.Find(p.RoomId) != null), "seed " + seed + " floor " + floor);
        }

        [Test]
        public void Validator_CatchesABrokenFloor3()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(1, 3);
            var fewer = new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests.Take(3).ToList(), -1, -1, -1, null);
            Assert.IsTrue(CellarFloorValidator.Validate(fewer).Any(e => e.Contains("chests")));
            var cut = new CellarFloorLayout(l.Seed, l.Floor, l.IsStub, l.Theme, l.Rooms, l.Corridors.Skip(1).ToList(), l.Links, l.DeadEnds, l.Chests, -1, -1, -1, null);
            Assert.IsNotEmpty(CellarFloorValidator.Validate(cut));
        }

        [Test]
        public void BadFloorNumbers_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Generate(1, 0));
            foreach (int floor in new[] { 1, 3, 5, 8, 9, 10 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Generate(1, floor), "Generate " + floor);
                Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.PickRooms(1, floor), "PickRooms " + floor);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Layout(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Layout(1, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellarFloorGenerator.Layout(1, 10));
        }

        [Test]
        public void PickRooms_IsPure_AndExcludesThePreviousGeneratedFloor()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                var f2 = CellarFloorGenerator.PickRooms(seed, 2);
                Assert.AreEqual(f2.Combat.Select(r => r.Id), CellarFloorGenerator.PickRooms(seed, 2).Combat.Select(r => r.Id));
                var f4 = CellarFloorGenerator.PickRooms(seed, 4);
                var f6 = CellarFloorGenerator.PickRooms(seed, 6);
                var f7 = CellarFloorGenerator.PickRooms(seed, 7);
                Assert.IsFalse(f4.Combat.Any(r => f2.Combat.Contains(r)), "seed " + seed + ": floors 2 and 4 share a combat room");
                Assert.IsFalse(f6.Combat.Any(r => f4.Combat.Contains(r)), "seed " + seed + ": floors 4 and 6 share a combat room");
                Assert.IsFalse(f7.Combat.Any(r => f6.Combat.Contains(r)), "seed " + seed + ": floors 6 and 7 share a combat room");
            }
        }

        [Test]
        public void ConsecutiveGeneratedFloors_ShareNoCombatRoom()
        {
            for (uint seed = 1; seed <= 60; seed++)
                for (int i = 1; i < GeneratedFloors.Length; i++)
                {
                    var a = CellarFloorGenerator.Generate(seed, GeneratedFloors[i - 1]).Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Select(r => r.RoomId);
                    var b = CellarFloorGenerator.Generate(seed, GeneratedFloors[i]).Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Select(r => r.RoomId);
                    Assert.IsEmpty(a.Intersect(b).ToList(), "seed " + seed + ": floors " + GeneratedFloors[i - 1] + " and " + GeneratedFloors[i] + " share a combat room");
                }
        }

        [Test]
        public void Floor6And7_UseCoalSubTheme()
        {
            for (uint seed = 1; seed <= 40; seed++)
                foreach (int floor in new[] { 6, 7 })
                {
                    Assert.AreEqual(SubTheme.CoalCellar, CellarFloorGenerator.PickRooms(seed, floor).Theme, "plan, seed " + seed + " floor " + floor);
                    Assert.AreEqual(SubTheme.CoalCellar, CellarFloorGenerator.Generate(seed, floor).Theme, "layout, seed " + seed + " floor " + floor);
                }
            Assert.AreEqual(SubTheme.Larder, CellarFloorGenerator.Generate(1, 2).Theme);
            Assert.AreEqual(SubTheme.WineVault, CellarFloorGenerator.Generate(1, 4).Theme);
            // floors 2 and 4 draw only larder and wine rooms
            for (uint seed = 1; seed <= 60; seed++)
                foreach (int floor in new[] { 2, 4 })
                    foreach (CellarRoom r in CellarFloorGenerator.PickRooms(seed, floor).Combat)
                        Assert.That(r.Theme, Is.EqualTo(SubTheme.Larder).Or.EqualTo(SubTheme.WineVault).Or.EqualTo(SubTheme.CoalCellar), "fallback is the only way past the two themes");
        }

        [Test]
        public void Roster_Tier1Only_OnEveryUndercroftFloor()
        {
            int tier1 = CellarFloorRoster.SlimeBit | CellarFloorRoster.BroomBit | CellarFloorRoster.RatKingBit;
            for (int floor = 1; floor <= 7; floor++)
            {
                int e = CellarFloorRoster.EligibleMask(floor);
                Assert.AreNotEqual(0, e, "floor " + floor);
                Assert.AreEqual(0, e & ~tier1, "a non-Tier1 kind is eligible on floor " + floor);
                for (uint seed = 0; seed < 50; seed++)
                {
                    int m = CellarFloorRoster.RosterMask(seed, floor);
                    Assert.AreNotEqual(0, m); Assert.AreEqual(0, m & ~e);
                    if (!CellarFloorGenerator.IsGeneratedFloor(floor)) Assert.AreEqual(e, m, "set pieces keep the full roster, floor " + floor);
                }
            }
        }

        [Test]
        public void Validator_FailsOnABrokenLayout()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(7, 1);
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
                foreach (int floor in GeneratedFloors)
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

        [TestCase(CellarTestFloors.FastSeeds), TestCase(1000, Category = "Soak")]
        public void RepetitionMetric(int seeds)
        {
            int locked = 0, floors = 0, combatSlots = 0, ownTheme = 0;
            for (uint seed = 0; seed < seeds; seed++)
            {
                var layouts = GeneratedFloors.Select(f => CellarTestFloors.Layout(seed, f)).ToArray();
                var plans = GeneratedFloors.Select(f => CellarFloorGenerator.PickRooms(seed, f)).ToArray();

                for (int i = 0; i < GeneratedFloors.Length; i++)
                {
                    var ids = layouts[i].Rooms.Select(r => r.RoomId).ToList();
                    Assert.AreEqual(ids.Count, ids.Distinct().Count(), "seed " + seed + " floor " + GeneratedFloors[i] + ": a room appears twice");
                    if (layouts[i].HasLockedRoom) locked++;
                    floors++;
                    foreach (CellarRoom r in plans[i].Combat) { combatSlots++; if (r.Theme == plans[i].Theme) ownTheme++; }
                }
                // consecutive generated floors share no combat room (entry and exit are shared by design)
                for (int i = 0; i + 1 < layouts.Length; i++)
                {
                    var a = layouts[i].Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Select(r => r.RoomId);
                    var b = layouts[i + 1].Rooms.Where(r => r.Room.Kind == RoomKind.Combat).Select(r => r.RoomId);
                    Assert.IsEmpty(a.Intersect(b).ToList(), "seed " + seed + ": floors " + GeneratedFloors[i] + " and " + GeneratedFloors[i + 1] + " share a combat room");
                }
                // at least 3 distinct floor silhouettes (footprint hashes), and at least 3 distinct room silhouette tags across the generated floors
                Assert.GreaterOrEqual(layouts.Select(l => l.FootprintHash()).Distinct().Count(), 3, "seed " + seed + ": floor footprints repeat");
                int tags = layouts.SelectMany(l => l.Rooms).Select(r => r.Room.Silhouette).Where(s => s.Length > 0).Distinct().Count();
                Assert.GreaterOrEqual(tags, 3, "seed " + seed + ": only " + tags + " distinct room silhouettes across the generated floors");
            }
            double lockedShare = (double)locked / floors, ownShare = (double)ownTheme / combatSlots;
            TestContext.WriteLine("locked share " + lockedShare.ToString("F3") + ", own sub-theme share " + ownShare.ToString("F3") + " over " + floors + " floors");
            double slack = seeds >= 1000 ? 0 : 0.12;   // the share bands are statistical: the fast twin's few fixed seeds get a wider band
            Assert.That(lockedShare, Is.InRange(0.40 - slack, 0.60 + slack), "about half the floors have a locked room");
            Assert.That(ownShare, Is.InRange(0.60 - slack, 0.95 + slack), "about 70% (more where the theme pool is small) of combat rooms come from the floor's sub-theme");
        }

        [Test]
        public void Checkpoint_IsOnTheBankRoomFloorOnly()
        {
            for (uint seed = 0; seed < 50; seed++)
                for (int floor = 1; floor <= 7; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                    Assert.AreEqual(floor == UndercroftPlan.BankFloor, l.HasCheckpoint, "seed " + seed + " floor " + floor);
                    Assert.AreEqual(floor == UndercroftPlan.BankFloor, l.BankRoomCell.HasValue);
                }
        }
    }
}
