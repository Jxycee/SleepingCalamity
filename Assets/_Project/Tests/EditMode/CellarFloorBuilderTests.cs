// M5 stage 2: the runtime greybox floor builder (Presentation/Cellars/CellarFloorBuilder) turns a Core layout into the expected geometry, markers and openings.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarFloorBuilderTests
    {
        static readonly (uint Seed, int Floor)[] Cases = { (12345, 1), (12345, 2), (12345, 3), (7, 1), (99, 2), (4242, 3), (12345, 4) };

        Scene scene;

        [SetUp] public void SetUp() => scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static int OccupiedCells(CellarFloorLayout l)
        {
            var occ = new HashSet<(int, int)>();
            foreach (PlacedRoom p in l.Rooms)
                for (int x = 0; x < p.Width; x++) for (int y = 0; y < p.Height; y++)
                    if (p.Room.CellAt(x, y) != CellKind.Solid) occ.Add((p.X + x, p.Y + y));
            foreach (PlacedCorridor c in l.Corridors) occ.Add((c.X, c.Y));
            return occ.Count;
        }

        static Transform Group(CellarFloorBuild b, string name) => b.Root.Find(name);

        static IEnumerable<Transform> Children(Transform t) { for (int i = 0; i < t.childCount; i++) yield return t.GetChild(i); }

        // Procgen item 6: the lights the builder really creates stay under the room cap, and the Core plan (ExistingLights + the warm lights placed) matches the real count
        // (no kit here, so the kit-only niche / barrel / corner candles are absent; the builder's own check counts the real lights and is what holds the cap with a kit).
        [Test]
        public void RoomLights_RealCountMatchesThePlan_AndStaysUnderTheCap()
        {
            int warmPlaced = 0;
            for (uint seed = 0; seed < 25; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                    CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                    int[] real = CellarFloorBuilder.RoomLightCounts(b.Root, l);
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        int warm = b.WarmLights.CountInRoom(r), planned = CellarFloorWarmLights.ExistingLights(l, b.Dressing, r) + warm;
                        string at = "seed " + seed + " floor " + floor + " room " + r;
                        Assert.AreEqual(warm, b.Root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("WarmLight_r" + r + "_")), at + " warm lights built");
                        warmPlaced += warm;
                        if (warm > 0) Assert.LessOrEqual(real[r], CellarFloorWarmLights.RoomLightCap, at);
                        if (l.Rooms[r].Room.Kind != RoomKind.Corridor) Assert.AreEqual(planned, real[r], at + ": ExistingLights drifted from the builder");
                    }
                    Object.DestroyImmediate(b.Root.gameObject);
                }
            Assert.Greater(warmPlaced, 0);
        }

        [Test]
        public void OneFloorTilePerOccupiedCell()
        {
            foreach (var (seed, floor) in Cases)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                int expected = OccupiedCells(l);
                Assert.AreEqual(expected, Group(b, CellarFloorBuilder.FloorGroup).childCount, "floor objects, seed " + seed + " floor " + floor);
                Assert.AreEqual(expected, b.FloorTiles, "counted tiles");
                Object.DestroyImmediate(b.Root.gameObject);
            }
        }

        [Test]
        public void EveryDoorHasAnOpeningOrAWall()
        {
            foreach (var (seed, floor) in Cases)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                Physics.SyncTransforms();
                int open = 0;
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    PlacedRoom p = l.Rooms[r];
                    for (int d = 0; d < p.Room.Doors.Count; d++)
                    {
                        CellDoor door = p.Room.Doors[d]; CellPos cell = p.DoorCell(d);
                        Vector3 mid = CellarFloorBuilder.CellCenter(cell.X, cell.Y) + CellarFloorBuilder.SideDir(door.Side) * (CellarFloorBuilder.CellSize * 0.5f);
                        bool ns = door.Side == CellSide.North || door.Side == CellSide.South;
                        bool blocked = Physics.CheckBox(mid + new Vector3(0, 1f, 0), ns ? new Vector3(0.9f, 0.8f, 0.12f) : new Vector3(0.12f, 0.8f, 0.9f));
                        bool isOpen = l.DoorIsOpen(r, d);
                        bool isGate = l.HasLockedRoom && r == l.LockedRoomIndex && l.Links[l.LockedLinkIndex].Locked
                                      && ((l.Links[l.LockedLinkIndex].RoomA == r && l.Links[l.LockedLinkIndex].DoorA == d) || (l.Links[l.LockedLinkIndex].RoomB == r && l.Links[l.LockedLinkIndex].DoorB == d));
                        string at = "seed " + seed + " floor " + floor + " room " + r + " door " + d;
                        if (isOpen)
                        {
                            open++;
                            Assert.IsNotNull(Group(b, CellarFloorBuilder.DoorGroup).Find("DoorOpening_r" + r + "_d" + d), "opening object, " + at);
                            Assert.AreEqual(isGate, blocked, (isGate ? "locked gate must block, " : "open doorway must be free, ") + at);
                        }
                        else Assert.IsTrue(blocked, "walled door must block, " + at);
                    }
                }
                Assert.AreEqual(open, b.DoorOpenings);
                Assert.AreEqual(open, Group(b, CellarFloorBuilder.DoorGroup).childCount);
                Assert.AreEqual(l.HasLockedRoom ? 1 : 0, Group(b, CellarFloorBuilder.DoorGroup).GetComponentsInChildren<Transform>().Count(t => t.name == "LockedGate"));
                Object.DestroyImmediate(b.Root.gameObject);
            }
        }

        [Test]
        public void MarkerCountsMatchTheLayout()
        {
            foreach (var (seed, floor) in Cases)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                List<Transform> m = Children(Group(b, CellarFloorBuilder.MarkerGroup)).ToList();
                string at = "seed " + seed + " floor " + floor;
                Assert.AreEqual(l.Chests.Count, m.Count(t => t.name.StartsWith("Chest_") || t.name.StartsWith("KeyChest_")), "chests, " + at);
                Assert.AreEqual(l.KeyChest != null ? 1 : 0, m.Count(t => t.name.StartsWith("KeyChest_")), "key chest, " + at);
                Assert.AreEqual(l.EntryStarts.Count, m.Count(t => t.name.StartsWith("EntryStart_")), "entry starts, " + at);
                Assert.AreEqual(l.StairExit.HasValue ? 1 : 0, m.Count(t => t.name == "StairExit"), "stair, " + at);
                Assert.AreEqual(l.Carrier != null ? 1 : 0, m.Count(t => t.name.StartsWith("Carrier_")), "carrier, " + at);
                Assert.AreEqual(l.Rooms.Sum(p => p.Room.Spawns.Count), m.Count(t => t.name.StartsWith("Spawn_")), "spawns, " + at);
                Assert.AreEqual(l.BankCell.HasValue ? 1 : 0, m.Count(t => t.name == "Bank"), "bank, " + at);
                Assert.AreEqual(l.BakerCell.HasValue ? 1 : 0, m.Count(t => t.name == "Baker"), "baker, " + at);
                Assert.AreEqual(l.WardenCell.HasValue ? 1 : 0, m.Count(t => t.name == "Warden"), "warden, " + at);
                Object.DestroyImmediate(b.Root.gameObject);
            }
        }

        [Test]
        public void EntryIsWalkableAndTheBuildIsDeterministic()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(12345, 1);
            CellarFloorBuild a = CellarFloorBuilder.Build(l, null);
            Physics.SyncTransforms();
            Vector3 e = a.EntryCenter;
            Assert.IsFalse(Physics.CheckBox(e + new Vector3(0, 1f, 0), new Vector3(0.4f, 0.8f, 0.4f)), "the entry cell is free at body height");
            Assert.IsTrue(Physics.Raycast(e + new Vector3(0, 1f, 0), Vector3.down, out RaycastHit hit, 3f), "floor under the entry");
            Assert.AreEqual(0f, hit.point.y, 0.01f);
            List<string> first = a.Root.GetComponentsInChildren<Transform>().Select(t => t.name + "@" + t.position.ToString("F2")).ToList();
            Object.DestroyImmediate(a.Root.gameObject);
            CellarFloorBuild b = CellarFloorBuilder.Build(CellarFloorGenerator.Layout(12345, 1), null);
            CollectionAssert.AreEqual(first, b.Root.GetComponentsInChildren<Transform>().Select(t => t.name + "@" + t.position.ToString("F2")).ToList());
            Object.DestroyImmediate(b.Root.gameObject);
        }
    }
}
