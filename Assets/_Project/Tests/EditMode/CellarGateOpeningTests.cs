// Floor 3 (seed 12345) soak finding, 2026-11-02: the host DiveBot reported "DoorOpen but the gate still blocks" and walked into Wall_12_2_East. These tests pin what the builder
// really builds: the locked gate is the ONLY collider in its opening once hidden, the opening is wide enough for a hero capsule, and no wall piece closes the doorway.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarGateOpeningTests
    {
        static readonly (uint Seed, int Floor)[] Cases = { (12345, 1), (12345, 2), (12345, 3), (7, 1), (99, 2), (4242, 3) };

        [SetUp] public void SetUp() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // The mid-point of the locked room's door (the same cell maths as the builder).
        static bool GateMid(CellarFloorLayout l, out Vector3 mid, out Vector3 along, out Vector3 through)
        {
            mid = along = through = default;
            if (l.LockedLinkIndex < 0) return false;
            FloorLink k = l.Links[l.LockedLinkIndex];
            int r = l.LockedRoomIndex, d = k.RoomA == r ? k.DoorA : k.DoorB;
            PlacedRoom p = l.Rooms[r]; CellDoor door = p.Room.Doors[d]; CellPos cell = p.DoorCell(d);
            through = CellarFloorBuilder.SideDir(door.Side);
            mid = CellarFloorBuilder.CellCenter(cell.X, cell.Y) + through * (CellarFloorBuilder.CellSize * 0.5f);
            along = Vector3.Cross(Vector3.up, through);
            return true;
        }

        [Test]
        public void LockedGate_IsTheOnlySolidThingInItsOpening_AndTheOpeningIsClearWhenItIsHidden()
        {
            int checkedFloors = 0;
            foreach (var (seed, floor) in Cases)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                if (!GateMid(l, out Vector3 mid, out Vector3 along, out Vector3 through)) continue;
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                try
                {
                    Physics.SyncTransforms();
                    string at = "seed " + seed + " floor " + floor + " gate at " + mid;
                    Transform gate = CellarFloorBuilder.FindGate(b.Root);
                    Assert.IsNotNull(gate, at + ": gate built");
                    Assert.IsTrue(gate.GetComponent<Collider>() != null && gate.GetComponent<Collider>().enabled, at + ": gate has a collider");
                    // a hero-sized capsule (r 0.4, h 1.8) on the door line, from 1.5 m before to 1.5 m after the opening, must be free once the gate is hidden
                    gate.gameObject.SetActive(false); Physics.SyncTransforms();
                    for (float t = -1.5f; t <= 1.5f; t += 0.25f)
                    {
                        Vector3 c = mid + through * t;
                        bool hit = Physics.CheckCapsule(c + Vector3.up * 0.5f, c + Vector3.up * 1.4f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                        if (hit)
                        {
                            Collider[] cs = Physics.OverlapCapsule(c + Vector3.up * 0.5f, c + Vector3.up * 1.4f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                            string names = ""; foreach (Collider x in cs) names += x.transform.parent.name + "/" + x.name + " ";
                            Assert.Fail(at + ": opening blocked at t=" + t + " by " + names);
                        }
                    }
                    gate.gameObject.SetActive(true); Physics.SyncTransforms();
                    Assert.IsTrue(Physics.CheckCapsule(mid + Vector3.up * 0.5f, mid + Vector3.up * 1.4f, 0.4f, ~0, QueryTriggerInteraction.Ignore), at + ": a shut gate blocks");
                    checkedFloors++;
                }
                finally { Object.DestroyImmediate(b.Root.gameObject); }
            }
            Assert.Greater(checkedFloors, 0, "no locked floor among the cases");
        }
    }
}
