// Tests/EditMode/CellarWallDepthTests.cs: a floor wall with no walkable cell behind it is deep enough that a hero shoved into it (an enemy stepping into a hero pinned to the wall,
// the character controller backing out of the overlap) cannot be pushed out the far side into the void and fall (DiveBot seed 7 floor 4, wine_aisles).
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarWallDepthTests
    {
        [SetUp] public void SetUp() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void WallsFacingTheVoid_AreAtLeastAMetreDeep_AndKeepTheirInnerFace()
        {
            foreach ((uint seed, int floor) in new[] { (7u, 4), (7u, 3), (12345u, 3), (31u, 4) })
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                Transform walls = b.Root.Find(CellarFloorBuilder.WallGroup);
                int checkedWalls = 0;
                foreach (Transform w in walls)
                {
                    string[] n = w.name.Split('_');   // Wall_<x>_<y>_<Side>
                    int x = int.Parse(n[1]), y = int.Parse(n[2]);
                    CellSide side = (CellSide)System.Enum.Parse(typeof(CellSide), n[3]);
                    (int dx, int dy) = CellarRoom.Step(side);
                    if (CellOccupied(l, x + dx, y + dy)) continue;
                    BoxCollider col = w.GetComponent<BoxCollider>();
                    Assert.That(col, Is.Not.Null, w.name);
                    bool ns = side == CellSide.North || side == CellSide.South;
                    float depth = ns ? col.bounds.size.z : col.bounds.size.x;
                    Assert.That(depth, Is.GreaterThanOrEqualTo(1f), seed + " f" + floor + " " + w.name);
                    // the face toward the walkable cell stays where it was: half a wall thickness off the cell edge
                    Vector3 cellEdge = CellarFloorBuilder.CellCenter(x, y) + new Vector3(dx * CellarFloorBuilder.CellSize * 0.5f, 0f, -dy * CellarFloorBuilder.CellSize * 0.5f);
                    float inner = ns ? Mathf.Abs(col.bounds.center.z - cellEdge.z) - depth * 0.5f : Mathf.Abs(col.bounds.center.x - cellEdge.x) - depth * 0.5f;
                    Assert.That(inner, Is.EqualTo(-CellarFloorBuilder.WallThickness * 0.5f).Within(0.02f), w.name + " inner face");
                    checkedWalls++;
                }
                Assert.That(checkedWalls, Is.GreaterThan(0), "no void-facing wall found for seed " + seed + " floor " + floor);
            }
        }

        static bool CellOccupied(CellarFloorLayout l, int x, int y)
        {
            if (x < 0 || y < 0 || x >= CellarFloorLayout.GridSize || y >= CellarFloorLayout.GridSize) return false;
            foreach (PlacedRoom p in l.Rooms)
            {
                int lx = x - p.X, ly = y - p.Y;
                if (lx >= 0 && ly >= 0 && lx < p.Width && ly < p.Height && p.Room.CellAt(lx, ly) != CellKind.Solid) return true;
            }
            foreach (PlacedCorridor c in l.Corridors) if (c.X == x && c.Y == y) return true;
            return false;
        }
    }
}
