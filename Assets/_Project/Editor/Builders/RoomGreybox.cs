// Editor/Builders/RoomGreybox.cs: greyboxes one RoomDataV2 in one water/heat state from primitives (metrics gym plan, Task 7; room-authoring spec). Deterministic: fixed loop order, no randomness.
// Room cell (x, y) maps to local (4x + 2, 4(H-1-y) + 2): row 0 is north (+Z). A level is 2 m. Authority: none (editor only).
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class RoomGreybox
    {
        const float Cell = 4f, Tile = 0.1f, MarkerSize = 0.5f;

        public static GameObject Build(RoomDataV2 room, IReadOnlyList<int> state, Vector3 origin, Transform parent)
        {
            CellarRoom b = room.Base;
            var root = new GameObject("Room_" + b.Id);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = origin;
            Material ground = MaterialLibrary.Get("M_GreyboxGround"), wall = MaterialLibrary.Get("M_GreyboxWall"), step = MaterialLibrary.Get("M_GreyboxStep"),
                water = MaterialLibrary.Get("M_GreyboxDummy"), mark = MaterialLibrary.Get("M_GreyboxElite"), player = MaterialLibrary.Get("M_GreyboxPlayer");
            float levelM = GameMetrics.LevelHeightCm / 100f, wallH = GameMetrics.WallHeight;

            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    Vector3 c = Centre(b, x, y);
                    string id = x + "_" + y;
                    switch (b.CellAt(x, y))
                    {
                        case CellKind.Floor:
                            {
                                float top = room.LevelAt(x, y) * levelM;
                                Box(root.transform, "Floor_" + id, new Vector3(c.x, top - Tile / 2, c.z), new Vector3(Cell, Tile, Cell), ground);
                                int depth = room.DepthCm(x, y, state);
                                if (depth > 0) Box(root.transform, "Water_" + id, new Vector3(c.x, top + depth / 200f, c.z), new Vector3(Cell, depth / 100f, Cell), water);
                                if (room.Burning(x, y, state)) Box(root.transform, "Fire_" + id, new Vector3(c.x, top + 0.5f, c.z), new Vector3(Cell, 1f, Cell), mark);
                                break;
                            }
                        case CellKind.Solid:
                            Box(root.transform, "Solid_" + id, new Vector3(c.x, wallH / 2, c.z), new Vector3(Cell, wallH, Cell), wall); break;
                        default:
                            Box(root.transform, "Pillar_" + id, new Vector3(c.x, wallH / 2, c.z), new Vector3(1f, wallH, 1f), wall); break;
                    }
                }

            foreach (StairCell s in room.Stairs)
            {
                Vector3 c = Centre(b, s.X, s.Y), dir = SideDir(s.Up);
                float baseY = room.LevelAt(s.X, s.Y) * levelM;
                Vector3 slope = new Vector3(dir.x * Cell, levelM, dir.z * Cell);
                GameObject ramp = Box(root.transform, "Stair_" + s.X + "_" + s.Y, new Vector3(c.x, baseY + levelM / 2f - Tile / 2, c.z), new Vector3(Cell, Tile, slope.magnitude), step);
                ramp.transform.localRotation = Quaternion.LookRotation(slope.normalized, Vector3.up);
            }
            foreach (ChestSlot ch in b.Chests) Marker(root.transform, "Chest_" + ch.X + "_" + ch.Y, room, b, ch.X, ch.Y, mark);
            foreach (CellarSpawn sp in b.Spawns) Marker(root.transform, "Spawn_" + sp.X + "_" + sp.Y, room, b, sp.X, sp.Y, player);
            foreach (ToolSocket ts in room.Sockets) Marker(root.transform, "Control_" + ts.Id, room, b, ts.X, ts.Y, water);
            return root;
        }

        static Vector3 Centre(CellarRoom b, int x, int y) => new Vector3(x * Cell + Cell / 2f, 0f, (b.Height - 1 - y) * Cell + Cell / 2f);

        static Vector3 SideDir(CellSide s) => s == CellSide.North ? new Vector3(0, 0, 1) : s == CellSide.East ? new Vector3(1, 0, 0) : s == CellSide.South ? new Vector3(0, 0, -1) : new Vector3(-1, 0, 0);

        static void Marker(Transform parent, string name, RoomDataV2 room, CellarRoom b, int x, int y, Material m)
        {
            Vector3 c = Centre(b, x, y);
            Box(parent, name, new Vector3(c.x, room.LevelAt(x, y) * (GameMetrics.LevelHeightCm / 100f) + MarkerSize / 2, c.z), Vector3.one * MarkerSize, m, collider: false);
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider = true) =>
            BuilderUtil.EnsurePrimitive(parent, name, PrimitiveType.Cube, pos, Quaternion.identity, size, m, keepCollider: collider);
    }
}
