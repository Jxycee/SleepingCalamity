// Authority: none (pure EditMode tests). The Undercroft Cellars room format (Core/Cellars/CellarRoom) and the Phase 1 pool (CellarRoomPool): the pool is
// valid, ASCII parses and round-trips, every room rule fails on a small bad room, and the pool has the composition PHASE1 7.2 asks for.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarRoomPoolTests
    {
        static readonly string[] Variants = { "A", "B" };

        // A valid 3x3 combat room to break one rule at a time.
        static CellarRoom Combat(string[] rows, params SocketSpec[] socks) => CellarRoom.Parse("t", "Test", RoomKind.Combat, SubTheme.Larder, "box", rows, socks);
        static string[] Good => new[] { ".D.", "s.s", "CD." };   // 3x3, doors N (1,0) and S (1,2), two spawns, one chest

        static void ExpectError(CellarRoom r, string fragment)
        {
            List<string> errs = r.Validate();
            Assert.That(errs.Any(e => e.Contains(fragment)), "expected an error containing '" + fragment + "', got:\n" + string.Join("\n", errs));
        }

        [Test]
        public void GoodRoom_IsValid()
        {
            var r = Combat(Good, new SocketSpec(SocketKind.Shelf, 0, 0, CellSide.South, Variants));
            Assert.IsEmpty(r.Validate(), string.Join("\n", r.Validate()));
        }

        [Test]
        public void Pool_IsValid()
        {
            List<string> errs = CellarRoomPool.Validate();
            Assert.IsEmpty(errs, string.Join("\n", errs));
        }

        // ---- ASCII ----

        [Test]
        public void Ascii_ParsesAndRoundTrips()
        {
            string[] rows = { "..##", "L.##", "s.PD", "S.K." };
            CellarRoom r = Combat(rows);
            Assert.AreEqual(4, r.Width); Assert.AreEqual(4, r.Height);
            Assert.AreEqual(CellKind.Solid, r.CellAt(2, 0));
            Assert.AreEqual(CellKind.Pillar, r.CellAt(2, 2));
            Assert.AreEqual(2, r.Doors.Count);
            CellDoor locked = r.LockedDoor;
            Assert.AreEqual((0, 1, CellSide.West), (locked.X, locked.Y, locked.Side));
            Assert.AreEqual(CellSide.East, r.Doors.First(d => !d.Locked).Side);
            Assert.AreEqual(2, r.Spawns.Count); Assert.AreEqual(SpawnTier.Tier2, r.Spawns.First(s => s.X == 0 && s.Y == 3).Tier);
            Assert.IsTrue(r.Chests.Single().KeyCandidate);
            CollectionAssert.AreEqual(rows, r.ToAscii());
            CollectionAssert.AreEqual(rows, CellarRoom.Parse("t2", "T", RoomKind.Combat, SubTheme.Larder, "x", r.ToAscii()).ToAscii());
        }

        [Test]
        public void Ascii_RejectsBadInput()
        {
            Assert.Throws<FormatException>(() => Combat(new[] { "..", "..." }), "ragged rows");
            Assert.Throws<FormatException>(() => Combat(new[] { "...", ".?.", "..." }), "unknown glyph");
            Assert.Throws<FormatException>(() => Combat(new[] { "D..", "...", "..." }), "door on a corner");
            Assert.Throws<FormatException>(() => Combat(new[] { "...", ".D.", "..." }), "door in the middle");
        }

        [Test]
        public void Ascii_DoorSideComesFromTheEdge()
        {
            CellarRoom r = Combat(new[] { ".D.", "D.D", ".D." });
            CollectionAssert.AreEquivalent(new[] { CellSide.North, CellSide.West, CellSide.East, CellSide.South }, r.Doors.Select(d => d.Side));
        }

        // ---- one failing case per rule ----

        [Test]
        public void Rule_DoorNotOnBoundary()
        {
            var cells = new CellKind[3, 3];
            var r = new CellarRoom("t", "T", RoomKind.Combat, SubTheme.Larder, "box", cells,
                new[] { new CellDoor(1, 1, CellSide.North), new CellDoor(1, 2, CellSide.South) }, null, new[] { new CellarSpawn(0, 0, SpawnTier.Tier1), new CellarSpawn(2, 0, SpawnTier.Tier1) }, new[] { new ChestSlot(0, 1) });
            ExpectError(r, "not on the room boundary");
        }

        [Test]
        public void Rule_DoorFacesInward()
        {
            var cells = new CellKind[3, 3];
            var r = new CellarRoom("t", "T", RoomKind.Combat, SubTheme.Larder, "box", cells,
                new[] { new CellDoor(1, 0, CellSide.South), new CellDoor(1, 2, CellSide.South) }, null, new[] { new CellarSpawn(0, 0, SpawnTier.Tier1), new CellarSpawn(2, 0, SpawnTier.Tier1) }, new[] { new ChestSlot(0, 1) });
            ExpectError(r, "not on the room boundary facing outward");
        }

        [Test]
        public void Rule_DoorOnPillarCell()
        {
            var r = new CellarRoom("t", "T", RoomKind.Combat, SubTheme.Larder, "box", Cells(3, 3, (1, 0, CellKind.Pillar)),
                new[] { new CellDoor(1, 0, CellSide.North), new CellDoor(1, 2, CellSide.South) }, null, new[] { new CellarSpawn(0, 0, SpawnTier.Tier1), new CellarSpawn(2, 0, SpawnTier.Tier1) }, new[] { new ChestSlot(0, 1) });
            ExpectError(r, "is not on a Floor cell");
        }

        static CellKind[,] Cells(int w, int h, params (int x, int y, CellKind k)[] set)
        {
            var c = new CellKind[w, h];
            foreach (var s in set) c[s.x, s.y] = s.k;
            return c;
        }

        [Test]
        public void Rule_CombatRoomNeedsTwoDoors()
            => ExpectError(Combat(new[] { ".D.", "s.s", "C.." }), "needs at least 2");

        [Test]
        public void Rule_UnreachableFloorPocket()
            => ExpectError(Combat(new[] { ".D.#", "s.#C", "D#.s", "..#." }), "not reachable");

        [Test]
        public void Rule_SocketOnSolidCell()
            => ExpectError(Combat(new[] { ".D.", "s#s", "CD." }, new SocketSpec(SocketKind.Barrel, 1, 1, CellSide.North, Variants)), "socket t.s1 at (1,1) is on a Solid");

        [Test]
        public void Rule_SocketOnDoorSpawnOrChestCell()
        {
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 1, 0, CellSide.North, Variants)), "shares its cell with a door");
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 0, 1, CellSide.North, Variants)), "shares its cell with a spawn");
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 0, 2, CellSide.North, Variants)), "shares its cell with a chest");
        }

        [Test]
        public void Rule_SocketOutsideGrid()
            => ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 5, 5, CellSide.North, Variants)), "outside the grid");

        [Test]
        public void Rule_WallMountedSocketNeedsAWall()
        {
            // (1,1) is open floor on every side: a shelf there has nothing behind it
            ExpectError(Combat(new[] { ".D.", "...", "sCs", ".D." }, new SocketSpec(SocketKind.Shelf, 1, 1, CellSide.North, Variants)), "no wall behind");
            // a shelf on a door edge cell facing away from the door has the room edge behind it and is fine
            Assert.IsEmpty(Combat(Good, new SocketSpec(SocketKind.Shelf, 2, 0, CellSide.South, Variants)).Validate().Where(e => e.Contains("wall")));
        }

        [Test]
        public void Rule_SocketVariantCount()
        {
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 2, 2, CellSide.North, "A", "B", "C", "D")), "4 variants");
            ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 2, 2, CellSide.North, "A")), "1 variants");
        }

        [Test]
        public void Rule_SocketVariantNamesUnique()
            => ExpectError(Combat(Good, new SocketSpec(SocketKind.Barrel, 2, 2, CellSide.North, "A", "a")), "repeated variant");

        [Test]
        public void Rule_CombatRoomNeedsTwoSpawns()
            => ExpectError(Combat(new[] { ".D.", "s..", "CD." }), "spawn point");

        [Test]
        public void Rule_CombatRoomNeedsAChest()
            => ExpectError(Combat(new[] { ".D.", "s.s", ".D." }), "chest slots");

        [Test]
        public void Rule_CombatSizeRange()
        {
            ExpectError(Combat(new[] { "sC", ".s" }), "outside 2x3 to 5x4");                              // 2x2
            ExpectError(Combat(new[] { "sC...", ".....", ".s...", ".....", "....." }), "outside 2x3 to 5x4");   // 5x5
            Assert.IsEmpty(Combat(new[] { "sC", "DD", ".s" }).Validate());                                    // 2x3
            Assert.IsFalse(Combat(new[] { "sC.", ".s." }).Validate().Any(e => e.Contains("outside")));        // 3x2 is the same size turned
            Assert.IsFalse(Combat(new[] { "sC...", "D...D", ".s...", "....." }).Validate().Any(e => e.Contains("outside")));   // 5x4
            Assert.IsFalse(Combat(new[] { "sC..", "D..D", ".s..", "....", "...." }).Validate().Any(e => e.Contains("outside")));   // 4x5
        }

        [Test]
        public void Rule_CombatNeedsThemeAndSilhouette()
        {
            ExpectError(CellarRoom.Parse("t", "T", RoomKind.Combat, SubTheme.Any, "box", Good), "sub-theme");
            ExpectError(CellarRoom.Parse("t", "T", RoomKind.Combat, SubTheme.Larder, "", Good), "silhouette");
        }

        [Test]
        public void Rule_StairAndBankAndStartFeatures()
        {
            ExpectError(CellarRoom.Parse("e", "E", RoomKind.Entry, SubTheme.Any, "x", new[] { ".D..", "EE..", "....", "...." }), "needs 4 party start cells");
            ExpectError(CellarRoom.Parse("x", "X", RoomKind.StairExit, SubTheme.Any, "x", new[] { ".D..", "....", "....", "...." }), "exactly 1 stair");
            ExpectError(CellarRoom.Parse("c", "C", RoomKind.Checkpoint, SubTheme.Any, "x", new[] { ".D.", "...", ".D." }), "exactly 1 bank");
            ExpectError(CellarRoom.Parse("t", "T", RoomKind.Combat, SubTheme.Larder, "box", new[] { ".D.", "sXs", "CD." }), "only the stair-exit");
        }

        [Test]
        public void Rule_NonCombatRoomHasNoSpawns()
            => ExpectError(CellarRoom.Parse("x", "X", RoomKind.StairExit, SubTheme.Any, "x", new[] { ".D..", "s...", "....", "...X" }), "has spawn points");

        [Test]
        public void Rule_LockedRoomRules()
        {
            Assert.IsEmpty(Combat(new[] { ".L.", "s.s", "CD." }).Validate());
            ExpectError(Combat(new[] { ".L.", "s.s", "KD." }), "cannot hold the key chest");
            ExpectError(Combat(new[] { ".L.", "s.s", ".D." }), "needs a chest");
            ExpectError(Combat(new[] { ".L.", "L.s", "CD." }), "more than one locked");
            ExpectError(CellarRoom.Parse("c", "C", RoomKind.Checkpoint, SubTheme.Any, "x", new[] { ".L.", ".B.", ".D." }), "only a combat room can be the locked");
        }

        [Test]
        public void Rule_KeyChestOnlyInCombatRooms()
            => ExpectError(CellarRoom.Parse("c", "C", RoomKind.Checkpoint, SubTheme.Any, "x", new[] { ".D.", "KB.", ".D." }), "key chests live in combat");

        [Test]
        public void Rule_EmptyOrDuplicateSocketIds()
        {
            var a = new DressingSocket("dup", SocketKind.Barrel, 2, 2, CellSide.North, "A", "B");
            var b = new DressingSocket("dup", SocketKind.Crate, 2, 1, CellSide.North, "A", "B");
            var cells = new CellKind[3, 3];
            var r = new CellarRoom("t", "T", RoomKind.Combat, SubTheme.Larder, "box", cells, new[] { new CellDoor(1, 0, CellSide.North), new CellDoor(1, 2, CellSide.South) },
                new[] { a, b }, new[] { new CellarSpawn(0, 0, SpawnTier.Tier1), new CellarSpawn(2, 0, SpawnTier.Tier1) }, new[] { new ChestSlot(0, 1) });
            ExpectError(r, "empty or duplicated");
        }

        // ---- corridor pieces ----

        [Test]
        public void Corridors_BuildTheirDoorsFromTheMask()
        {
            CellarRoom t = CellarRoom.Corridor("c", "T", CorridorShape.T, CellarRoom.MaskN | CellarRoom.MaskE | CellarRoom.MaskW);
            Assert.AreEqual(3, t.Doors.Count);
            CollectionAssert.AreEquivalent(new[] { CellSide.North, CellSide.East, CellSide.West }, t.Doors.Select(d => d.Side));
            Assert.IsEmpty(t.Validate());
        }

        [Test]
        public void Rule_CorridorMaskMustMatchItsDoors()
        {
            var r = new CellarRoom("c", "C", RoomKind.Corridor, SubTheme.Any, "", new CellKind[1, 1], new[] { new CellDoor(0, 0, CellSide.North), new CellDoor(0, 0, CellSide.South) },
                null, null, null, null, CorridorShape.Straight, CellarRoom.MaskN | CellarRoom.MaskE);
            ExpectError(r, "does not match its doors");
        }

        [Test]
        public void Rule_CorridorMaskMustFitItsShape()
        {
            ExpectError(CellarRoom.Corridor("c", "C", CorridorShape.Straight, CellarRoom.MaskN | CellarRoom.MaskE), "not valid for shape Straight");
            ExpectError(CellarRoom.Corridor("c", "C", CorridorShape.Corner, CellarRoom.MaskN | CellarRoom.MaskS), "not valid for shape Corner");
            ExpectError(CellarRoom.Corridor("c", "C", CorridorShape.DeadEnd, 3), "not valid for shape DeadEnd");
            ExpectError(CellarRoom.Corridor("c", "C", CorridorShape.Cross, 7), "not valid for shape Cross");
            ExpectError(CellarRoom.Corridor("c", "C", CorridorShape.T, 15), "not valid for shape T");
        }

        [Test]
        public void Rule_DeadEndHasExactlyOneDoor()
        {
            Assert.AreEqual(1, CellarRoomPool.Find("corridor_dead_end").Doors.Count);
            ExpectError(CellarRoom.Corridor("c", "C", CorridorShape.DeadEnd, CellarRoom.MaskN | CellarRoom.MaskS), "not valid for shape DeadEnd");
        }

        [Test]
        public void Rule_OnlyCorridorsCarryAMask()
        {
            var r = new CellarRoom("t", "T", RoomKind.Combat, SubTheme.Larder, "box", new CellKind[3, 3], new[] { new CellDoor(1, 0, CellSide.North), new CellDoor(1, 2, CellSide.South) },
                null, new[] { new CellarSpawn(0, 0, SpawnTier.Tier1), new CellarSpawn(2, 0, SpawnTier.Tier1) }, new[] { new ChestSlot(0, 1) }, null, CorridorShape.None, 5);
            ExpectError(r, "only corridor pieces");
        }

        // ---- pool composition ----

        [Test]
        public void Pool_Composition()
        {
            var pool = CellarRoomPool.All;
            Assert.AreEqual(12, pool.Count(r => r.Kind == RoomKind.Combat));
            Assert.AreEqual(1, pool.Count(r => r.Kind == RoomKind.Entry));
            Assert.AreEqual(1, pool.Count(r => r.Kind == RoomKind.StairExit));
            Assert.AreEqual(1, pool.Count(r => r.Kind == RoomKind.Checkpoint));
            Assert.AreEqual(6, pool.Count(r => r.Kind == RoomKind.Corridor));
            Assert.AreEqual(21, pool.Count);
        }

        [Test]
        public void Pool_EachSubThemeHasFourCombatRooms()
        {
            foreach (SubTheme t in new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar })
                Assert.AreEqual(4, CellarRoomPool.Combat(t).Count(), t.ToString());
        }

        [Test]
        public void Pool_AFloorCanDrawSeventyPercentOwnThemeWithNoRepeats()
        {
            // 4-6 rooms, about 70% own theme: 3 of 4, 4 of 6 (rounded) from the 4 own rooms, the rest from the other 8
            foreach (SubTheme t in new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar })
            {
                int own = CellarRoomPool.Combat(t).Count(), other = CellarRoomPool.All.Count(r => r.Kind == RoomKind.Combat && r.Theme != t);
                for (int rooms = 4; rooms <= 6; rooms++)
                {
                    int wantOwn = (int)Math.Round(rooms * 0.7);
                    Assert.LessOrEqual(wantOwn, own, t + " " + rooms);
                    Assert.LessOrEqual(rooms - wantOwn, other, t + " " + rooms);
                }
            }
        }

        [Test]
        public void Pool_SizesCoverTheRange_AndSilhouettesDiffer()
        {
            var combat = CellarRoomPool.All.Where(r => r.Kind == RoomKind.Combat).ToList();
            Assert.That(combat.Any(r => Math.Min(r.Width, r.Height) == 2), "a 2-wide room");
            Assert.That(combat.Any(r => r.Width * r.Height >= 20), "a 5x4 room");
            Assert.That(combat.Select(r => r.Silhouette).Distinct().Count(), Is.GreaterThanOrEqualTo(6));
            Assert.That(combat.Any(r => r.Silhouette == "L-shape"), "an L-shape");
            Assert.That(combat.Any(r => r.Silhouette == "gallery"), "a gallery");
            Assert.That(combat.Any(r => r.Silhouette == "pillared-hall" && Enumerable.Range(0, r.Width).Any(x => Enumerable.Range(0, r.Height).Any(y => r.CellAt(x, y) == CellKind.Pillar))), "a pillared hall with pillars");
        }

        [Test]
        public void Pool_EachThemeHasALockableRoomAndAKeyChestRoom()
        {
            foreach (SubTheme t in new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar })
            {
                Assert.That(CellarRoomPool.Combat(t).Any(r => r.LockedDoor != null), t + " lockable");
                Assert.That(CellarRoomPool.Combat(t).Any(r => r.HasKeyChest), t + " key chest");
            }
        }

        [Test]
        public void Pool_ChestSlotsCoverAFloorsSixToEightChests()
        {
            // a floor has 4-6 rooms and 6-8 chests: the rooms of one theme hold enough slots to draw from
            foreach (SubTheme t in new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar })
                Assert.That(CellarRoomPool.Combat(t).Sum(r => r.Chests.Count), Is.GreaterThanOrEqualTo(6), t.ToString());
        }

        [Test]
        public void Pool_AllSixCorridorShapesArePresent()
        {
            foreach (CorridorShape s in new[] { CorridorShape.Straight, CorridorShape.Corner, CorridorShape.T, CorridorShape.Cross, CorridorShape.DeadEnd, CorridorShape.Door })
                Assert.AreEqual(1, CellarRoomPool.All.Count(r => r.Shape == s), s.ToString());
        }

        [Test]
        public void Pool_EveryDressingSocketHasTwoOrThreeVariants()
        {
            foreach (CellarRoom r in CellarRoomPool.All)
                foreach (DressingSocket s in r.Sockets)
                    Assert.That(s.Variants.Length, Is.InRange(2, 3), s.Id);
        }

        [Test]
        public void Pool_EveryRoomSurvivesAnAsciiRoundTrip()
        {
            foreach (CellarRoom r in CellarRoomPool.All.Where(r => r.Kind != RoomKind.Corridor))
            {
                CellarRoom again = CellarRoom.Parse(r.Id, r.Name, r.Kind, r.Theme, r.Silhouette, r.ToAscii());
                CollectionAssert.AreEqual(r.ToAscii(), again.ToAscii(), r.Id);
                Assert.AreEqual(r.Doors.Count, again.Doors.Count, r.Id);
            }
        }

        // ---- pool-level rules ----

        [Test]
        public void PoolRule_DuplicateIds()
        {
            var list = CellarRoomPool.All.Concat(new[] { CellarRoomPool.Find("larder_pantry") }).ToList();
            Assert.That(CellarRoomPool.ValidatePool(list).Any(e => e.Contains("room id 'larder_pantry' is used 2 times")));
        }

        [Test]
        public void PoolRule_MissingSpecialsAndTooFewCombatRooms()
        {
            var noEntry = CellarRoomPool.All.Where(r => r.Kind != RoomKind.Entry).ToList();
            Assert.That(CellarRoomPool.ValidatePool(noEntry).Any(e => e.Contains("exactly one Entry")));
            var fewWine = CellarRoomPool.All.Where(r => r.Id != "wine_aisles").ToList();
            Assert.That(CellarRoomPool.ValidatePool(fewWine).Any(e => e.Contains("WineVault has 3 combat rooms")));
        }

        [Test]
        public void PoolRule_MissingCorridorShape()
        {
            var noCross = CellarRoomPool.All.Where(r => r.Shape != CorridorShape.Cross).ToList();
            Assert.That(CellarRoomPool.ValidatePool(noCross).Any(e => e.Contains("no Cross corridor piece")));
        }

        [Test]
        public void PoolRule_ThemeNeedsALockableAndAKeyRoom()
        {
            var noKeys = CellarRoomPool.All.Where(r => r.Id != "coal_furnace_ring").ToList();
            Assert.That(CellarRoomPool.ValidatePool(noKeys).Any(e => e.Contains("CoalCellar has no key-chest room")));
        }
    }
}
