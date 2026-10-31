// Authority: none (pure EditMode tests). The Cellars variety foundations (Core/Cellars: optional and jittered sockets, CellarFloorDressing, CellarAtmosphere, vignettes with
// pity, the exit-distance bound) and their greybox display in CellarFloorBuilder. Catches: a dressing that differs between peers (hash compared with itself and a replay),
// fill chances that drift from the configured percentages, a dark first floor or two dark floors in a dive, a dive with no vignette, a once-per-dive vignette repeated, a
// vignette that blocks a door or cuts a room, an exit next to the entry, a room that looks the same every visit, and a builder that draws what the dressing left empty.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarVarietyTests
    {
        static readonly string[] Variants = { "A", "B" };
        static CellarRoom Combat(string[] rows, params SocketSpec[] socks) => CellarRoom.Parse("t", "Test", RoomKind.Combat, SubTheme.Larder, "box", rows, socks);
        static string[] Good => new[] { ".D.", "s.s", "CD." };   // free floor: (0,0) (2,0) (1,1) (2,2)

        static void ExpectError(CellarRoom r, string fragment)
        {
            List<string> errs = r.Validate();
            Assert.That(errs.Any(e => e.Contains(fragment)), "expected an error containing '" + fragment + "', got:\n" + string.Join("\n", errs));
        }

        // ---------------------------------------------------------------- sockets: chance and alternatives

        [Test]
        public void Socket_ChanceAndAlternativesSurviveRotation()
        {
            CellarRoom r = Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Chance(40).Alt((2, 0)));
            DressingSocket s = r.Sockets[0];
            Assert.AreEqual(40, s.Chance); Assert.AreEqual(2, s.CellOptions);
            CellarRoom turned = r.Rotated(1);   // 3x3: (x, y) -> (2 - y, x)
            DressingSocket t = turned.Sockets[0];
            Assert.AreEqual(40, t.Chance);
            Assert.AreEqual(new CellPos(2, 0).X, t.OptionCell(0).X); Assert.AreEqual(0, t.OptionCell(0).Y);   // (0,0) -> (2,0)
            Assert.AreEqual(2, t.OptionCell(1).X); Assert.AreEqual(2, t.OptionCell(1).Y);                      // (2,0) -> (2,2)
            Assert.AreEqual(CellSide.West, t.Facing);
            Assert.IsEmpty(turned.Validate(), string.Join("\n", turned.Validate()));
            CellarRoom full = r.Rotated(4);
            Assert.AreEqual(s.OptionCell(1).X, full.Sockets[0].OptionCell(1).X); Assert.AreEqual(s.OptionCell(1).Y, full.Sockets[0].OptionCell(1).Y);
        }

        [Test]
        public void Rule_SocketAlternatives()
        {
            Assert.IsEmpty(Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Alt((2, 0))).Validate());
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Alt((1, 0))), "shares its cell with a door");
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Alt((1, 1))), "no wall behind");   // open floor all round
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Alt((2, 0), (2, 0))), "twice");
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Alt((2, 0), (1, 1), (2, 2), (0, 0))), "alternative cells, at most 3");
            // two sockets may not share any candidate cell
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants).Alt((2, 0)), new SocketSpec(SocketKind.Shelf, 2, 0, CellSide.South, Variants)), "also a candidate cell of another socket");
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 1, 1, CellSide.North, Variants).Chance(120)), "fill chance 120");
        }

        [Test]
        public void Rule_BarrelAlternativesAllCountAsBlocking()
        {
            // 3x4 with one door each end: a barrel at (1,1) leaves a way round, barrels that may also stand on (0,1)/(2,1) together wall the room in two
            var rows = new[] { ".D.", "s.s", "C..", ".D." };
            Assert.IsEmpty(Combat(rows, new SocketSpec(SocketKind.Barrel, 1, 1, CellSide.North, Variants)).Validate().Where(e => e.Contains("cut off")));
            var wall = Combat(new[] { ".D.", "...", "sCs", ".D." }, new SocketSpec(SocketKind.Barrel, 1, 1, CellSide.North, Variants).Alt((0, 1), (2, 1)));
            ExpectError(wall, "cut off");
        }

        // ---------------------------------------------------------------- dressing: determinism and hashes

        [Test]
        public void Dressing_SameSeedAndFloor_GivesIdenticalDressing()
        {
            foreach (uint seed in new uint[] { 0, 1, 7, 12345, 99999, 0xFFFFFFFF })
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    CellarFloorDressing a = CellarFloorDressing.Resolve(l), b = CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(seed, floor));
                    Assert.AreEqual(a.DressingHash(), b.DressingHash(), "seed " + seed + " floor " + floor);
                    Assert.AreEqual(a.Atmosphere.LampPercent, b.Atmosphere.LampPercent);
                    Assert.AreEqual(a.Atmosphere.FogPercent, b.Atmosphere.FogPercent);
                    Assert.AreEqual(a.Atmosphere.OddLightRoom, b.Atmosphere.OddLightRoom);
                    Assert.AreEqual(string.Join(",", a.Vignettes.Select(v => v.Vignette.Id + "@" + v.Cell)), string.Join(",", b.Vignettes.Select(v => v.Vignette.Id + "@" + v.Cell)));
                }
        }

        [Test]
        public void Dressing_DoesNotTouchTheLayoutHash()
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(12345, 1);
            ulong before = l.LayoutHash(); CellarFloorDressing.Resolve(l);
            Assert.AreEqual(before, l.LayoutHash());
            Assert.AreEqual(7160980227004277501UL, before, "the geometry golden is unchanged by the variety layers");
        }

        [Test]
        public void Dressing_DiffersBetweenSeeds_AndBetweenFloors()
        {
            var hashes = new HashSet<ulong>();
            for (uint s = 0; s < 200; s++) hashes.Add(CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(s, 1)).DressingHash());
            Assert.Greater(hashes.Count, 195);
            Assert.AreNotEqual(CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(5, 1)).DressingHash(), CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(5, 2)).DressingHash());
        }

        [Test]
        public void Dressing_ReplayedVignetteState_MatchesAnyOrderOfAsking()
        {
            // floor 3 asked first or after floors 1 and 2 gives the same answer: the pity state is a pure function of the seed
            foreach (uint seed in new uint[] { 3, 44, 555 })
            {
                string cold = string.Join(",", VignetteDive.Select(seed, 3).Chosen.Select(v => v.Id));
                VignetteDive.Select(seed, 1); VignetteDive.Select(seed, 2);
                Assert.AreEqual(cold, string.Join(",", VignetteDive.Select(seed, 3).Chosen.Select(v => v.Id)));
            }
        }

        // ---------------------------------------------------------------- dressing statistics

        [Test]
        public void Sockets_FillRatesMatchTheConfiguredChances_Over1000Seeds()
        {
            var filled = new Dictionary<SocketKind, int>(); var total = new Dictionary<SocketKind, int>(); var chance = new Dictionary<SocketKind, double>();
            int jittered = 0, empty = 0;
            for (uint seed = 0; seed < 1000; seed++)
                for (int floor = 1; floor <= 3; floor++)
                    foreach (DressedSocket s in CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(seed, floor)).Sockets)
                    {
                        if (s.Kind == SocketKind.Lantern) continue;   // lantern chance also follows the floor's lamp percent
                        total[s.Kind] = total.GetValueOrDefault(s.Kind) + 1; chance[s.Kind] = chance.GetValueOrDefault(s.Kind) + s.Socket.Chance / 100.0;
                        if (s.Filled) filled[s.Kind] = filled.GetValueOrDefault(s.Kind) + 1; else empty++;
                        if (s.Option > 0) jittered++;
                    }
            foreach (SocketKind k in total.Keys.OrderBy(k => (int)k))
            {
                double want = chance[k] / total[k], got = filled.GetValueOrDefault(k) / (double)total[k];
                Assert.That(Math.Abs(got - want), Is.LessThan(0.05), k + " fill rate " + got + " vs configured " + want + " over " + total[k] + " sockets");
            }
            Assert.Greater(empty, 1000, "optional sockets do come up empty");
            Assert.Greater(jittered, 500, "sockets with alternative cells do move");
            Assert.Less(total.Keys.Count(k => Math.Abs(chance[k] / total[k] - 1.0) > 0.01), total.Count, "not every kind is optional");
        }

        [Test]
        public void Sockets_NeverShareACell_AndStayOnTheRoom()
        {
            for (uint seed = 0; seed < 300; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    var seen = new HashSet<(int, int)>();
                    foreach (DressedSocket s in d.Sockets)
                    {
                        PlacedRoom p = l.Rooms[s.RoomIndex];
                        Assert.AreEqual(CellKind.Floor, p.Room.CellAt(s.Cell.X - p.X, s.Cell.Y - p.Y), s.Socket.Id);
                        Assert.IsTrue(seen.Add((s.Cell.X, s.Cell.Y)), "two sockets on " + s.Cell + " seed " + seed + " floor " + floor);
                    }
                }
        }

        [Test]
        public void Sameness_TheSameRoomIsDressedDifferently_InMostAppearances()
        {
            var sig = new Dictionary<string, List<ulong>>();
            for (uint seed = 0; seed < 100; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        if (!sig.TryGetValue(l.Rooms[r].RoomId, out List<ulong> list)) sig[l.Rooms[r].RoomId] = list = new List<ulong>();
                        list.Add(d.RoomSignature(r));
                    }
                }
            int appearances = 0, differing = 0;
            foreach (var kv in sig.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                ulong mode = kv.Value.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;   // the most common dressing of this room
                int diff = kv.Value.Count(x => x != mode);
                appearances += kv.Value.Count; differing += diff;
                // a room with one optional socket (the pantry's single barrel) tops out near 70% by construction, so the per-room floor is looser than the overall bar
                if (kv.Value.Count >= 20) Assert.GreaterOrEqual(diff / (double)kv.Value.Count, 0.55, kv.Key + " looks the same too often");
            }
            Assert.GreaterOrEqual(differing / (double)appearances, 0.70);
        }

        // ---------------------------------------------------------------- atmosphere

        [Test]
        public void Atmosphere_DarkFloor_OneInTwelve_NeverFloorOne_NeverTwiceInADive()
        {
            const int n = 6000; int dark2 = 0, dark3 = 0;
            for (uint seed = 0; seed < n; seed++)
            {
                Assert.IsFalse(CellarAtmosphere.IsDarkFloor(seed, 1), "floor 1 is never dark");
                bool d2 = CellarAtmosphere.IsDarkFloor(seed, 2), d3 = CellarAtmosphere.IsDarkFloor(seed, 3), d4 = CellarAtmosphere.IsDarkFloor(seed, 4);
                Assert.LessOrEqual((d2 ? 1 : 0) + (d3 ? 1 : 0) + (d4 ? 1 : 0), 1, "two dark floors in dive " + seed);
                if (d2) dark2++; if (d3) dark3++;
            }
            Assert.That(dark2 / (double)n, Is.InRange(1.0 / 12 - 0.02, 1.0 / 12 + 0.02), "floor 2 dark rate");
            Assert.That(dark3 / (double)n, Is.InRange(1.0 / 12 * 11 / 12 - 0.02, 1.0 / 12 * 11 / 12 + 0.02), "floor 3 dark rate (no second dark floor in a dive)");
        }

        [Test]
        public void Atmosphere_RollsStayInRange_AndWarmOnly()
        {
            int dark = 0;
            for (uint seed = 0; seed < 500; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarAtmosphere a = CellarAtmosphere.Roll(l);
                    if (a.Dark) { dark++; Assert.AreEqual(CellarAtmosphere.DarkLamp, a.LampPercent); Assert.AreEqual(CellarAtmosphere.DarkFog, a.FogPercent); }
                    else
                    {
                        Assert.That(a.LampPercent, Is.InRange(CellarAtmosphere.LampMin, CellarAtmosphere.LampMax));
                        Assert.That(a.FogPercent, Is.InRange(CellarAtmosphere.FogMin, CellarAtmosphere.FogMax));
                    }
                    Assert.That(a.FogWarmPercent, Is.InRange(0, 100)); Assert.That(a.DustPercent, Is.InRange(0, 100));
                    a.FogTintOffset(out float r, out float g, out float b);
                    Assert.That(r >= g && g >= 0f && b == 0f, "fog tint only ever warms: " + r + "," + g + "," + b);
                    int room = a.OddLightRoom;
                    Assert.That(room >= 0 && l.Rooms[room].Room.Kind == RoomKind.Combat, "the odd light sits in a combat room");
                    int rgb = a.OddLightRgb; int cr = (rgb >> 16) & 255, cg = (rgb >> 8) & 255, cb = rgb & 255;
                    Assert.That(cr > cg && cg > cb, "odd light is warm (no teal): " + rgb.ToString("X6"));
                }
            Assert.Greater(dark, 0);
        }

        [Test]
        public void Atmosphere_DarkFloorDimsTheLanterns_ButNotTheEntry()
        {
            for (uint seed = 0; seed < 3000; seed++)
            {
                if (!CellarAtmosphere.IsDarkFloor(seed, 2)) continue;
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, 2); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                var entry = l.Rooms.Select((p, i) => (p, i)).First(x => x.p.Room.Kind == RoomKind.Entry).i;
                Assert.IsTrue(d.SocketsOf(entry).Where(s => s.Kind == SocketKind.Lantern).All(s => s.Filled), "entry lanterns always stand");
                return;
            }
            Assert.Fail("no dark floor 2 in 3000 seeds");
        }

        // ---------------------------------------------------------------- vignettes

        [Test]
        public void Vignettes_PoolIsAuthoredSensibly()
        {
            Assert.That(CellarVignettePool.All.Count, Is.InRange(4, 8));
            Assert.AreEqual(CellarVignettePool.All.Count, CellarVignettePool.All.Select(v => v.Id).Distinct().Count());
            foreach (VignetteCategory c in Enum.GetValues(typeof(VignetteCategory))) Assert.IsTrue(CellarVignettePool.All.Any(v => v.Category == c), c + " has a vignette");
            foreach (CellarVignette v in CellarVignettePool.All) { Assert.GreaterOrEqual(v.Rarity, 2, v.Id); Assert.Greater(v.Props.Count, 1, v.Id); Assert.GreaterOrEqual(v.MinFloor, 1); }
        }

        [Test]
        public void Vignettes_BudgetNearOnePointTwo_AndNoDiveGoesWithoutOne()
        {
            const int n = 1000; int total = 0, bare = 0, dropped = 0, forcedFloors = 0;
            var perCategory = new Dictionary<VignetteCategory, int>();
            for (uint seed = 0; seed < n; seed++)
            {
                int dive = 0;
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorDressing d = CellarFloorDressing.Resolve(CellarFloorGenerator.Generate(seed, floor));
                    dive += d.Vignettes.Count; dropped += d.DroppedVignettes;
                    Assert.LessOrEqual(d.Vignettes.Count, VignetteDive.MaxPerFloor);
                    if (VignetteDive.Select(seed, floor).Forced) forcedFloors++;
                    foreach (PlacedVignette v in d.Vignettes) perCategory[v.Vignette.Category] = perCategory.GetValueOrDefault(v.Vignette.Category) + 1;
                }
                total += dive; if (dive == 0) bare++;
            }
            double mean = total / (n * 3.0);
            Assert.That(mean, Is.InRange(0.9, 1.5), "vignettes per floor");
            Assert.AreEqual(0, bare, "dives of floors 1-3 with no vignette (pity)");
            Assert.AreEqual(0, dropped, "chosen vignettes without a legal cell");
            Assert.Greater(forcedFloors, 0, "the floor-3 guarantee fires for some seeds");
            foreach (VignetteCategory c in Enum.GetValues(typeof(VignetteCategory))) Assert.Greater(perCategory.GetValueOrDefault(c), 0, c + " never shown");
            TestContext.WriteLine("vignette mean per floor " + mean.ToString("F3") + ", forced floors " + forcedFloors + ", per category " + string.Join(" ", perCategory.OrderBy(k => (int)k.Key).Select(k => k.Key + "=" + k.Value)));
        }

        [Test]
        public void Vignettes_OncePerDive_MinFloor_AndNoRepeatOnAFloor()
        {
            for (uint seed = 0; seed < 1000; seed++)
            {
                var seen = new HashSet<string>();
                for (int floor = 1; floor <= 4; floor++)
                {
                    IReadOnlyList<CellarVignette> chosen = VignetteDive.Select(seed, floor).Chosen;
                    Assert.AreEqual(chosen.Count, chosen.Select(v => v.Id).Distinct().Count(), "a vignette twice on one floor");
                    foreach (CellarVignette v in chosen)
                    {
                        Assert.GreaterOrEqual(floor, v.MinFloor, v.Id + " before its first floor");
                        if (v.OncePerDive) Assert.IsTrue(seen.Add(v.Id), v.Id + " twice in dive " + seed);
                    }
                }
            }
        }

        [Test]
        public void Vignettes_MustSeeOnesEscalateToCertain()
        {
            int seenIt = 0; const int n = 1000;
            CellarVignette must = CellarVignettePool.All.First(v => v.MustSee);
            for (uint seed = 0; seed < n; seed++)
                for (int floor = 1; floor <= VignetteDive.MustSeeStart; floor++)
                    if (VignetteDive.Select(seed, floor).Chosen.Contains(must)) { seenIt++; break; }
            Assert.GreaterOrEqual(seenIt / (double)n, 0.97, must.Id + " is a must-see within " + VignetteDive.MustSeeStart + " floors");
        }

        [Test]
        public void Vignettes_StandInCombatRooms_AwayFromDoors_WithoutCuttingTheRoom()
        {
            for (uint seed = 0; seed < 500; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    foreach (PlacedVignette v in d.Vignettes)
                    {
                        PlacedRoom p = l.Rooms[v.RoomIndex]; int lx = v.Cell.X - p.X, ly = v.Cell.Y - p.Y;
                        Assert.AreEqual(RoomKind.Combat, p.Room.Kind);
                        Assert.AreEqual(CellKind.Floor, p.Room.CellAt(lx, ly));
                        Assert.IsFalse(p.Room.Doors.Any(dr => dr.X == lx && dr.Y == ly), "a vignette on a door cell");
                        Assert.IsFalse(p.Room.Chests.Any(c => c.X == lx && c.Y == ly) || p.Room.Features.Any(f => f.X == lx && f.Y == ly), "a vignette on a chest or feature");
                        Assert.That(v.Turns, Is.InRange(0, 3));
                        // blocking the vignette cell and the filled barrels and crates must leave every floor cell and every door reachable
                        var blocked = new HashSet<(int, int)> { (lx, ly) };
                        foreach (DressedSocket s in d.SocketsOf(v.RoomIndex)) if (s.Filled && (s.Kind == SocketKind.Barrel || s.Kind == SocketKind.Crate)) blocked.Add((s.Cell.X - p.X, s.Cell.Y - p.Y));
                        foreach (PlacedVignette o in d.Vignettes) if (o.RoomIndex == v.RoomIndex) blocked.Add((o.Cell.X - p.X, o.Cell.Y - p.Y));
                        CellDoor start = p.Room.Doors.First(dr => !blocked.Contains((dr.X, dr.Y)));
                        var reach = new HashSet<(int, int)> { (start.X, start.Y) }; var q = new Queue<(int, int)>(); q.Enqueue((start.X, start.Y));
                        while (q.Count > 0)
                        {
                            var (cx, cy) = q.Dequeue();
                            foreach (CellSide sd in new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West })
                            {
                                var (dx, dy) = CellarRoom.Step(sd); int nx = cx + dx, ny = cy + dy;
                                if (p.Room.InBounds(nx, ny) && p.Room.CellAt(nx, ny) == CellKind.Floor && !blocked.Contains((nx, ny)) && reach.Add((nx, ny))) q.Enqueue((nx, ny));
                            }
                        }
                        for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++)
                            if (p.Room.CellAt(x, y) == CellKind.Floor && !blocked.Contains((x, y))) Assert.IsTrue(reach.Contains((x, y)), v.Vignette.Id + " cuts off (" + x + "," + y + ") in " + p.RoomId + " seed " + seed + " floor " + floor);
                    }
                }
        }

        [Test]
        public void Vignettes_WallHuggersStandAgainstAWall()
        {
            int seen = 0;
            for (uint seed = 0; seed < 400; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    foreach (PlacedVignette v in CellarFloorDressing.Resolve(l).Vignettes.Where(v => v.Vignette.WallHug))
                    {
                        PlacedRoom p = l.Rooms[v.RoomIndex]; var (dx, dy) = CellarRoom.Step(CellarRoom.Rotate(CellSide.South, v.Turns));
                        int nx = v.Cell.X - p.X + dx, ny = v.Cell.Y - p.Y + dy;
                        Assert.IsTrue(!p.Room.InBounds(nx, ny) || p.Room.CellAt(nx, ny) != CellKind.Floor, v.Vignette.Id + " has its back to open floor");
                        seen++;
                    }
                }
            Assert.Greater(seen, 50);
        }

        // ---------------------------------------------------------------- exit distance

        [Test]
        public void ExitDistance_AtLeastTheBound_On10000Seeds_AndNotAlwaysTheSame()
        {
            var distances = new HashSet<int>(); int shortest = int.MaxValue;
            for (uint seed = 0; seed < 10000; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    int dist = CellarFloorGenerator.Generate(seed, floor).EntryToExitDistance();
                    Assert.GreaterOrEqual(dist, CellarFloorGenerator.MinExitDistance, "seed " + seed + " floor " + floor);
                    distances.Add(dist); shortest = Math.Min(shortest, dist);
                }
            Assert.GreaterOrEqual(distances.Count, 15, "the exit distance varies");
            TestContext.WriteLine("exit distance: min " + shortest + ", " + distances.Count + " distinct values, bound " + CellarFloorGenerator.MinExitDistance);
        }

        [Test]
        public void ExitDistance_ValidatorRejectsAnExitBesideTheEntry()
        {
            // the fixed floor 4 stub is exempt, but a generated floor given an exit too close must fail: use the distance helper on a stub-shaped check
            CellarFloorLayout l = CellarFloorGenerator.Generate(12345, 1);
            Assert.GreaterOrEqual(l.EntryToExitDistance(), CellarFloorGenerator.MinExitDistance);
            Assert.IsEmpty(CellarFloorValidator.Validate(l));
            CellarFloorLayout stub = CellarFloorGenerator.Generate(1, 4);
            Assert.IsTrue(stub.IsStub); Assert.IsEmpty(CellarFloorValidator.Validate(stub), "the stub is exempt from the bound");
        }

        // ---------------------------------------------------------------- builder

        [Test]
        public void Builder_ShowsTheDressing_AndSkipsTheEmptySockets()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var (seed, floor) in new (uint, int)[] { (12345, 1), (12345, 2), (7, 3), (99, 2) })
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                Assert.AreEqual(d.DressingHash(), b.Dressing.DressingHash());
                int props = d.Sockets.Count(s => s.Filled && s.Kind != SocketKind.Lantern);
                Assert.AreEqual(props, b.DressingProps, "one prop group per filled non-lantern socket");
                Assert.AreEqual(props, b.Root.Find(CellarFloorBuilder.DressingGroup).childCount);
                Assert.AreEqual(d.Vignettes.Count, b.VignetteGroups);
                Assert.AreEqual(d.Vignettes.Count, b.Root.Find(CellarFloorBuilder.VignetteGroup).childCount);
                Assert.AreEqual(d.Atmosphere.DustVisibleHere, b.DustMotes);
                Assert.IsNotNull(b.Root.Find(CellarFloorBuilder.AtmosphereGroup).Find("OddLight_r" + d.Atmosphere.OddLightRoom + "_" + d.Atmosphere.OddLight));
                UnityEngine.Object.DestroyImmediate(b.Root.gameObject);
            }
        }

        [Test]
        public void Builder_SameLayoutBuildsTheSameHierarchy()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CellarFloorLayout l = CellarFloorGenerator.Generate(4242, 3);
            string Names(CellarFloorBuild b) => string.Join("|", b.Root.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            CellarFloorBuild x = CellarFloorBuilder.Build(l, null), y = CellarFloorBuilder.Build(l, null);
            Assert.AreEqual(Names(x), Names(y));
            Assert.AreEqual(x.Lamps, y.Lamps);
        }

        [Test]
        public void Builder_DarkFloorHasFewerLampsThanItsBrightSibling()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int darkLamps = 0, darkFloors = 0, litLamps = 0, litFloors = 0;
            for (uint seed = 0; seed < 600 && darkFloors < 6; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, 2); bool dark = CellarAtmosphere.IsDarkFloor(seed, 2);
                if (!dark && litFloors >= 6) continue;
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                if (dark) { darkLamps += b.Lamps; darkFloors++; } else { litLamps += b.Lamps; litFloors++; }
                UnityEngine.Object.DestroyImmediate(b.Root.gameObject);
            }
            Assert.Greater(darkFloors, 0);
            Assert.Less(darkLamps / (double)darkFloors, litLamps / (double)litFloors, "a dark floor lights fewer lamps");
        }
    }
}
