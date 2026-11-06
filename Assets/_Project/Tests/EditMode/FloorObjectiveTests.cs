// Authority: none (pure EditMode tests). Plan 5 task 1: which objective a floor of the 7-floor dive has (FloorObjectivePick.Rolled) and where its valves / pump sit (FloorObjectivePlan).
// Catches: an objective that repeats the previous floor's, a pool that never offers one objective, valve rooms that are adjacent / the entry / the exit / the locked room / behind the lock,
// a pump room outside the middle third of the entry-to-exit path, a plan that differs between two calls, a layout that cannot host its objective (the soak sweep counts fallbacks).
// Does not catch: how the live dive reads the plan (Task 4).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class FloorObjectiveTests
    {
        static readonly int[] GeneratedFloors = { 2, 4, 6, 7 };

        [Test]
        public void Rolled_FixedFloors()
        {
            for (uint s = 1; s <= 50; s++)
            {
                Assert.AreEqual(FloorObjective.GateKey, FloorObjectivePick.Rolled(s, 1));
                Assert.AreEqual(FloorObjective.Rescue, FloorObjectivePick.Rolled(s, 3));
                Assert.AreEqual(FloorObjective.Guardian, FloorObjectivePick.Rolled(s, 5));
            }
        }

        [Test]
        public void Rolled_NeverRepeatsPreviousFloor()
        {
            var pool = new[] { FloorObjective.GateKey, FloorObjective.TwoValves, FloorObjective.HoldPump };
            for (uint s = 1; s <= 2000; s++)
            {
                Assert.AreNotEqual(FloorObjective.GateKey, FloorObjectivePick.Rolled(s, 2), "seed " + s);
                Assert.AreNotEqual(FloorObjectivePick.Rolled(s, 6), FloorObjectivePick.Rolled(s, 7), "seed " + s);
                Assert.AreNotEqual(FloorObjective.GateKey, FloorObjectivePick.Rolled(s, 7), "seed " + s);
                foreach (int f in new[] { 2, 4, 6, 7 }) CollectionAssert.Contains(pool, FloorObjectivePick.Rolled(s, f), "seed " + s + " floor " + f);
            }
        }

        [Test]
        public void Rolled_AllThreeAppear()
        {
            var counts = new Dictionary<FloorObjective, int>();
            for (uint s = 1; s <= 300; s++)
            {
                FloorObjective o = FloorObjectivePick.Rolled(s, 4);
                counts[o] = counts.TryGetValue(o, out int n) ? n + 1 : 1;
            }
            foreach (FloorObjective o in new[] { FloorObjective.GateKey, FloorObjective.TwoValves, FloorObjective.HoldPump })
                Assert.GreaterOrEqual(counts.TryGetValue(o, out int n) ? n : 0, 60, o.ToString());
        }

        // Room graph over the layout's links, the locked link left out. Distances from the entry room (-1 unreachable).
        static int[] Distances(CellarFloorLayout l, out int entry)
        {
            entry = l.RoomAt(l.EntryStarts[0].X, l.EntryStarts[0].Y);
            var d = Enumerable.Repeat(-1, l.Rooms.Count).ToArray();
            d[entry] = 0; var q = new Queue<int>(); q.Enqueue(entry);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                for (int i = 0; i < l.Links.Count; i++)
                {
                    if (i == l.LockedLinkIndex || !l.Links[i].Touches(r) || l.Links[i].RoomA < 0 || l.Links[i].RoomB < 0) continue;
                    int o = l.Links[i].RoomA == r ? l.Links[i].RoomB : l.Links[i].RoomA;
                    if (d[o] < 0) { d[o] = d[r] + 1; q.Enqueue(o); }
                }
            }
            return d;
        }

        static bool InRoomOnFreeFloor(CellarFloorLayout l, int room, CellPos c)
        {
            PlacedRoom p = l.Rooms[room];
            if (l.RoomAt(c.X, c.Y) != room) return false;
            if (p.Room.CellAt(c.X - p.X, c.Y - p.Y) != CellKind.Floor) return false;
            for (int d = 0; d < p.Room.Doors.Count; d++) { CellPos dc = p.DoorCell(d); if (dc.X == c.X && dc.Y == c.Y) return false; }
            return true;
        }

        // Eligible rooms as the plan sees them (Combat, free cell, not the locked room); true when two of them share no link.
        static bool HasNonAdjacentPair(CellarFloorLayout l, FloorObjectivePlan _)
        {
            CellarFloorDressing d = CellarFloorDressing.Resolve(l);
            var el = new List<int>();
            for (int r = 0; r < l.Rooms.Count; r++) if (r != l.LockedRoomIndex && CellarEncounterPlan.FreeCellsOf(l, d, r).Count > 0) el.Add(r);
            foreach (int x in el) foreach (int y in el) if (x < y && !l.Links.Any(k => k.Touches(x) && k.Touches(y))) return true;
            return false;
        }

        [Test]
        public void Plan_ValveRooms_DistinctNonAdjacentReachable()
        {
            int seen = 0, adjacentSeen = 0;
            for (uint s = 1; s <= 300; s++)
                foreach (int f in GeneratedFloors)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(s, f);
                    FloorObjectivePlan p = FloorObjectivePlan.For(l);
                    if (p.Objective != FloorObjective.TwoValves) continue;
                    seen++;
                    string at = "seed " + s + " floor " + f;
                    int[] dist = Distances(l, out int entry);
                    Assert.AreNotEqual(p.ValveRoomA, p.ValveRoomB, at);
                    bool adjacent = l.Links.Any(k => k.Touches(p.ValveRoomA) && k.Touches(p.ValveRoomB));
                    if (adjacent) { adjacentSeen++; Assert.IsFalse(HasNonAdjacentPair(l, FloorObjectivePlan.For(l)), at + " valves adjacent although a non-adjacent pair exists"); }
                    foreach (int r in new[] { p.ValveRoomA, p.ValveRoomB })
                    {
                        Assert.AreNotEqual(entry, r, at); Assert.AreNotEqual(l.ExitRoomIndex(), r, at); Assert.AreNotEqual(l.LockedRoomIndex, r, at);
                        Assert.GreaterOrEqual(dist[r], 0, at + " valve room not reachable without the lock");
                        Assert.AreEqual(RoomKind.Combat, l.Rooms[r].Room.Kind, at);
                    }
                    Assert.IsTrue(InRoomOnFreeFloor(l, p.ValveRoomA, p.ValveCellA), at + " cell A");
                    Assert.IsTrue(InRoomOnFreeFloor(l, p.ValveRoomB, p.ValveCellB), at + " cell B");
                    Assert.AreEqual(-1, p.PumpRoom, at);
                    Assert.IsEmpty(CellarFloorValidator.Validate(l), at);
                }
            Assert.Greater(seen, 100, "the sweep must reach valve floors");
            Assert.Greater(adjacentSeen, 0, "the sweep must reach the adjacent-pair relaxation");
        }

        [Test]
        public void Plan_PumpRoom_InMiddleThird()
        {
            int seen = 0;
            for (uint s = 1; s <= 300; s++)
                foreach (int f in GeneratedFloors)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(s, f);
                    FloorObjectivePlan p = FloorObjectivePlan.For(l);
                    if (p.Objective != FloorObjective.HoldPump) continue;
                    seen++;
                    string at = "seed " + s + " floor " + f;
                    int[] dist = Distances(l, out int entry);
                    int len = dist[l.ExitRoomIndex()], d = dist[p.PumpRoom];
                    Assert.GreaterOrEqual(d, 0, at);
                    Assert.IsTrue(3 * d >= len && 3 * d <= 2 * len, at + " d " + d + " len " + len);
                    Assert.AreNotEqual(entry, p.PumpRoom, at); Assert.AreNotEqual(l.ExitRoomIndex(), p.PumpRoom, at); Assert.AreNotEqual(l.LockedRoomIndex, p.PumpRoom, at);
                    Assert.IsTrue(InRoomOnFreeFloor(l, p.PumpRoom, p.PumpCell), at + " pump cell");
                    Assert.AreEqual(-1, p.ValveRoomA, at);
                }
            Assert.Greater(seen, 100, "the sweep must reach pump floors");
        }

        [Test]
        public void Plan_IsDeterministic()
        {
            for (uint s = 1; s <= 100; s++)
                foreach (int f in new[] { 1, 2, 3, 4, 5, 6, 7 })
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(s, f);
                    Assert.AreEqual(FloorObjectivePlan.For(l).Hash(), FloorObjectivePlan.For(l, CellarFloorDressing.Resolve(l)).Hash(), "seed " + s + " floor " + f);
                    Assert.AreEqual(FloorObjectivePlan.For(l).Hash(), FloorObjectivePlan.For(CellarFloorGenerator.Layout(s, f)).Hash());
                }
        }

        [Test]
        public void Plan_UnusedFields_AreMinusOne_AndFixedFloorsFollowRolled()
        {
            foreach (int f in new[] { 1, 3, 5 })
            {
                FloorObjectivePlan p = FloorObjectivePlan.For(CellarFloorGenerator.Layout(7, f));
                Assert.AreEqual(FloorObjectivePick.Rolled(7, f), p.Objective);
                Assert.AreEqual(-1, p.ValveRoomA); Assert.AreEqual(-1, p.ValveRoomB); Assert.AreEqual(-1, p.PumpRoom);
            }
        }

        // The layout goldens do not move: this plan adds no draw to any generator stream (the golden test itself is CellarFloorGeneratorTests.Golden_Seed12345_GeneratedFloors, run unedited).
        [Test]
        public void GeneratorGolden_Unchanged()
        {
            CellarFloorLayout a = CellarFloorGenerator.Layout(12345, 2);
            ulong before = a.LayoutHash();
            FloorObjectivePlan.For(a);
            Assert.AreEqual(before, CellarFloorGenerator.Layout(12345, 2).LayoutHash());
        }

        // Decision 5 (relaxed 2026-11-06): valves prefer a non-adjacent pair, else two adjacent eligible rooms; a fallback is allowed only with fewer than 2 eligible rooms (never a pump).
        [Test, Category("Soak")]
        public void Plan_FallbackRate_2000Seeds()
        {
            int fallbacks = 0, adjacentPairs = 0; var bad = new List<string>();
            for (uint s = 1; s <= 2000; s++)
                foreach (int f in GeneratedFloors)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(s, f);
                    FloorObjectivePlan p = FloorObjectivePlan.For(l);
                    Assert.IsEmpty(CellarFloorValidator.Validate(l), "seed " + s + " floor " + f);
                    FloorObjective rolled = FloorObjectivePick.Rolled(s, f);
                    if (p.Objective == rolled)
                    {
                        if (rolled == FloorObjective.TwoValves && l.Links.Any(k => k.Touches(p.ValveRoomA) && k.Touches(p.ValveRoomB))) adjacentPairs++;
                        continue;
                    }
                    fallbacks++;
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    int eligible = 0;
                    for (int r = 0; r < l.Rooms.Count; r++) if (r != l.LockedRoomIndex && CellarEncounterPlan.FreeCellsOf(l, d, r).Count > 0) eligible++;
                    if (rolled != FloorObjective.TwoValves || eligible >= 2) bad.Add(s + "/" + f + " rolled " + rolled + " eligible " + eligible);
                    Assert.AreEqual(f == DiveFloorTable.LastBuiltFloor ? FloorObjective.Ending : FloorObjective.GateKey, p.Objective, "seed " + s + " floor " + f);
                }
            TestContext.WriteLine("objective fallbacks: " + fallbacks + " adjacent valve pairs: " + adjacentPairs + " of " + 2000 * GeneratedFloors.Length + " floors");
            Assert.IsEmpty(bad, "fallbacks a layout could have hosted (" + fallbacks + " in all, " + adjacentPairs + " adjacent pairs): " + string.Join(", ", bad.Take(30)));
        }
    }
}
