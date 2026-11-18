// World traversal scan: builds the ScanSpace for each playable space from the plans the builders themselves use, so the targets, intended lines and
// keep-clear spots come from data (HollowmereLayout's path graph; the Cellars floor plan), never from hand-placed lists.
//   Hollowmere  the open scene (Assets/_Project/Scenes/Hollowmere.unity) with one preset shown by HollowmereBuilder.ApplyPreset. Targets: every path node
//               (loop, lane, corner, doorsteps of every lot, building doors, stairs), every landmark without a door node, the cistern-house UndercroftStair.
//               Intended lines: every path edge. Spawn: a SpawnPoint in the scene when there is one (M6 S3 adds the dive hub), else the largest walkable area.
//   Cellars     CellarFloorBuilder.Build(Generate(seed, floor)) with the real dressing kit from the CellarFloorSync prefab, in an empty scene. Targets: entry
//               starts, the stair exit, chests and the key chest, the gate-key carrier's spawn, the locked door (from outside), bakery pieces, the healing
//               point, the checkpoint (floor 2), the heart's bank, baker and warden (floor 4). Chests and pieces inside the locked room are checked with the
//               door open. Intended lines: every corridor arm and every open room door. Spawn: entry start 0.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class TraversalSpaces
    {
        public const string CellarSyncPrefab = "Assets/_Project/Prefabs/CellarFloorSync.prefab";

        // ---------------------------------------------------------------------------------------------------------------- Hollowmere
        // R2_08_C3 (NW corner of the workshop lot): the pad is 3.5 m below the F09 terrace, so a retaining wall (LK0975-LK0978) stands 1 m west of the lot edge and the
        // ground 1.5 m north of the corner climbs 1.6 m; the corner is a 0.5 m crack between lot edge and wall, with no route leading into it.
        static readonly HashSet<string> WedgedCorners = new HashSet<string> { "R2_08_C3" };

        public static ScanSpace Hollowmere(HollowmereBuilder.Preset preset)
        {
            HollowmereBuilder.ApplyPreset(preset);
            Transform root = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Hollowmere").transform;
            TownLayout t = HollowmereLayout.Create();
            var s = new ScanSpace { Scene = "Hollowmere", Variant = preset.ToString(), Root = root };
            s.ExpectedGround = (x, z) => HollowmereLayout.Ground(t, x, z);

            float NodeY(PathNode n) => n.Kind == PathNodeKind.Door || n.Kind == PathNodeKind.Stair || Mathf.Abs(n.Y) > 0.01f ? n.Y : HollowmereLayout.Ground(t, n.Pos.X, n.Pos.Z);
            Vector3 P(PathNode n) => new Vector3(n.Pos.X, NodeY(n), n.Pos.Z);

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (PathNode n in t.Nodes) { minX = Mathf.Min(minX, n.Pos.X); maxX = Mathf.Max(maxX, n.Pos.X); minZ = Mathf.Min(minZ, n.Pos.Z); maxZ = Mathf.Max(maxZ, n.Pos.Z); }
            const float pad = 10f;
            s.Area = new Bounds(new Vector3((minX + maxX) * 0.5f, 15f, (minZ + maxZ) * 0.5f), new Vector3(maxX - minX + pad * 2f, 70f, maxZ - minZ + pad * 2f));

            SpawnPoint sp = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (sp != null) s.Spawn = sp.transform.position;

            // Lot corner nodes that are not places a player can stand are not targets and their ring edges are not intended paths (traversal-4):
            //  - a corner buried in solid geometry (the terrain bulging up in a neighbour's yard, a town-wall piece or gate pier): the capsule probe at the node hits a collider;
            //  - a corner wedged between a lot edge and a terrace retaining wall (WedgedCorners), a crack under 0.8 m wide that no route leads into.
            Physics.SyncTransforms();
            var dropped = new HashSet<string>();
            foreach (PathNode n in t.Nodes)
            {
                if (n.Kind != PathNodeKind.Corner) continue;
                Vector3 p = P(n);
                if (Physics.CheckCapsule(p + Vector3.up * 0.75f, p + Vector3.up * 1.45f, 0.3f, ~0, QueryTriggerInteraction.Ignore) || WedgedCorners.Contains(n.Id)) dropped.Add(n.Id);
            }
            // T10: the Saltwake road is closed by a fallen tree and a wall (HollowmereOrchard.RoadBlockWallX); the lane nodes behind it are not places a player can stand
            foreach (PathNode n in t.Nodes) if (n.Id.StartsWith("SaltwakeRoad_", System.StringComparison.Ordinal) && n.Pos.X < HollowmereOrchard.RoadBlockWallX) dropped.Add(n.Id);
            Debug.Log($"SC TRAVERSAL {preset}: {dropped.Count} lot corner nodes dropped as non-places: {string.Join(", ", dropped.OrderBy(x => x, System.StringComparer.Ordinal))}");

            var doorNodes = new HashSet<string>(t.Nodes.Where(n => n.Kind == PathNodeKind.Door).Select(n => n.Id));
            foreach (PathNode n in t.Nodes)
                if (!dropped.Contains(n.Id))
                s.Targets.Add(new TraversalTarget { Name = n.Id, Kind = n.Kind.ToString(), Pos = P(n), Reach = n.Kind == PathNodeKind.Door ? 1.5f : n.Kind == PathNodeKind.Stair ? 1.2f : 1.0f });
            foreach (Landmark l in t.Landmarks)
            {
                if (doorNodes.Contains(l.Id + "_Door")) continue;
                s.Targets.Add(new TraversalTarget
                {
                    Name = l.Id, Kind = "Landmark", Pos = new Vector3(l.Footprint.Centre.X, l.Y, l.Footprint.Centre.Z), Reach = Mathf.Max(l.Footprint.HalfX, l.Footprint.HalfZ) + 1.2f,
                });
            }
            foreach (Transform tr in root.GetComponentsInChildren<Transform>())
                if (tr.name == "UndercroftStair" && tr.gameObject.activeInHierarchy)
                    s.Targets.Add(new TraversalTarget { Name = "UndercroftStair (cistern house)", Kind = "CisternStair", Pos = tr.position, Reach = 1.5f });

            foreach (PathEdge e in t.Edges)
            {
                PathNode a = t.Node(e.A), b = t.Node(e.B);
                if (a == null || b == null || dropped.Contains(a.Id) || dropped.Contains(b.Id)) continue;
                s.Paths.Add(new IntendedPath
                {
                    Name = e.A + " -> " + e.B, A = P(a), B = P(b),
                    TrimA = a.Kind == PathNodeKind.Door ? 0.6f : 0f, TrimB = b.Kind == PathNodeKind.Door ? 0.6f : 0f,
                });
            }

            foreach (PathNode n in t.Nodes)
                if (n.Kind == PathNodeKind.Door || n.Kind == PathNodeKind.Stair) s.Clear.Add(new KeepClear { Name = n.Kind == PathNodeKind.Door ? "doorway " + n.Id : "stair " + n.Id, Pos = P(n), Radius = 0.55f });
            foreach (Foundation f in t.Foundations)
            {
                PathNode n = t.Node(f.DoorstepId(f.Facing));
                if (n != null) s.Clear.Add(new KeepClear { Name = "front doorstep " + n.Id, Pos = P(n), Radius = 0.55f });
            }

            // planned walkable: within 1.5 m of a path edge, or inside a lane's width
            var segs = new List<(Vector2 a, Vector2 b, float r)>();
            foreach (IntendedPath p in s.Paths) segs.Add((new Vector2(p.A.x, p.A.z), new Vector2(p.B.x, p.B.z), 1.5f));
            foreach (Lane lane in t.Lanes)
                for (int i = 0; i + 1 < lane.Points.Length; i++)
                    segs.Add((new Vector2(lane.Points[i].X, lane.Points[i].Z), new Vector2(lane.Points[i + 1].X, lane.Points[i + 1].Z), lane.Width * 0.5f + 0.25f));
            s.ExpectedWalkable = Raster(s.Area, segs);

            var nodes = t.Nodes;
            s.Describe = p =>
            {
                PathNode best = null; float bd = 8f;
                foreach (PathNode n in nodes) { float d = new Vector2(n.Pos.X - p.x, n.Pos.Z - p.z).magnitude; if (d < bd) { bd = d; best = n; } }
                return best == null ? "" : $"{bd:0.0} m from node {best.Id}";
            };
            return s;
        }

        static System.Func<float, float, bool> Raster(Bounds area, List<(Vector2 a, Vector2 b, float r)> segs)
        {
            const float cell = 0.5f;
            int w = Mathf.CeilToInt(area.size.x / cell), h = Mathf.CeilToInt(area.size.z / cell);
            var bits = new bool[w * h];
            float x0 = area.min.x, z0 = area.min.z;
            foreach (var (a, b, r) in segs)
            {
                int ix0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - r - x0) / cell)), ix1 = Mathf.Min(w - 1, Mathf.FloorToInt((Mathf.Max(a.x, b.x) + r - x0) / cell));
                int iz0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - r - z0) / cell)), iz1 = Mathf.Min(h - 1, Mathf.FloorToInt((Mathf.Max(a.y, b.y) + r - z0) / cell));
                Vector2 ab = b - a; float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                for (int z = iz0; z <= iz1; z++)
                    for (int x = ix0; x <= ix1; x++)
                    {
                        var p = new Vector2(x0 + (x + 0.5f) * cell, z0 + (z + 0.5f) * cell);
                        float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        if ((a + ab * tt - p).sqrMagnitude <= r * r) bits[z * w + x] = true;
                    }
            }
            return (x, z) =>
            {
                int ix = Mathf.FloorToInt((x - x0) / cell), iz = Mathf.FloorToInt((z - z0) / cell);
                return ix >= 0 && iz >= 0 && ix < w && iz < h && bits[iz * w + ix];
            };
        }

        // ---------------------------------------------------------------------------------------------------------------- Cellars
        static CellarFloorKit s_kit;

        public static CellarFloorKit Kit()
        {
            if (s_kit != null) return s_kit;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellarSyncPrefab);
            CellarFloorSync sync = prefab != null ? prefab.GetComponent<CellarFloorSync>() : null;
            if (sync == null || sync.Kit == null) throw new System.InvalidOperationException("TraversalScan: no CellarFloorSync kit at " + CellarSyncPrefab);
            return s_kit = sync.Kit;
        }

        public sealed class CellarBuild { public CellarFloorLayout Layout; public CellarFloorBuild Built; public int Fallback; }

        public static ScanSpace Cellar(uint seed, int floor, out CellarBuild build)
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor, out int fallback);
            CellarFloorBuild b = CellarFloorBuilder.Build(l, null, Kit());
            build = new CellarBuild { Layout = l, Built = b, Fallback = fallback };
            Transform root = b.Root;
            var s = new ScanSpace { Scene = "Cellars", Variant = "F" + floor, Seed = (int)seed, Floor = floor, Root = root };
            Bounds bb = b.Bounds;
            s.Area = new Bounds(new Vector3(bb.center.x, 2f, bb.center.z), new Vector3(bb.size.x + 4f, 12f, bb.size.z + 4f));
            s.ExpectedGround = (x, z) => 0f;
            s.Spawn = CellarFloorBuilder.EntryWorld(l, b, 0);
            const float cs = CellarFloorBuilder.CellSize;

            int lockedRoom = l.LockedRoomIndex;
            bool InLocked(int x, int y)
            {
                if (lockedRoom < 0) return false;
                PlacedRoom r = l.Rooms[lockedRoom];
                return x >= r.X && y >= r.Y && x < r.X + r.Width && y < r.Y + r.Height;
            }
            Vector3 Cell(int x, int y) => CellarFloorBuilder.WorldCell(b, new CellPos(x, y));
            void Target(string name, string kind, Vector3 pos, float reach, bool locked = false) => s.Targets.Add(new TraversalTarget { Name = name, Kind = kind, Pos = new Vector3(pos.x, 0f, pos.z), Reach = reach, BehindLock = locked });

            for (int i = 0; i < l.EntryStarts.Count; i++) Target("EntryStart_" + i, "EntryStart", CellarFloorBuilder.EntryWorld(l, b, i), 0.9f);
            if (CellarFloorBuilder.TryStairWorld(l, b, out Vector3 stair)) Target("StairExit", "Stair", stair, 2.0f);
            if (CellarFloorBuilder.TryBankWorld(l, b, out Vector3 bank)) Target(floor == UndercroftPlan.BankFloor ? "BankRoom" : "Checkpoint", "Checkpoint", bank, 2.0f);
            if (l.BakerCell.HasValue) Target("Baker", "Heart", Cell(l.BakerCell.Value.X, l.BakerCell.Value.Y), 2.0f);
            if (l.WardenCell.HasValue) Target("WardenSpawn", "Enemy", Cell(l.WardenCell.Value.X, l.WardenCell.Value.Y), 2.0f);
            foreach (FloorChest c in l.Chests)
            {
                Transform ct = CellarFloorBuilder.FindChest(root, c);
                Target(CellarFloorBuilder.ChestName(c), c.HoldsKey ? "KeyChest" : "Chest", ct != null ? ct.position : Cell(c.X, c.Y), 1.6f, lockedRoom >= 0 && c.RoomIndex == lockedRoom);
            }
            if (l.Carrier != null) Target("GateKeyCarrier", "GateKey", Cell(l.Carrier.X, l.Carrier.Y), 1.4f);
            Transform gate = CellarFloorBuilder.FindGate(root);
            if (gate != null) { s.Locks.Add(gate.gameObject); Target("LockedDoor", "LockedDoor", gate.position, 1.6f); }
            foreach (CellPos p in CellarPieceSpots.Choose(l, b.Dressing, 2)) Target("Piece_" + p.X + "_" + p.Y, "BakeryPiece", Cell(p.X, p.Y), 1.4f, InLocked(p.X, p.Y));
            CellPos? heal = CellarHealing.PointFor(l, b.Dressing);
            if (heal.HasValue) Target("HealingPoint", "Healing", Cell(heal.Value.X, heal.Value.Y), 1.4f, InLocked(heal.Value.X, heal.Value.Y));

            // intended lines: corridor arms and open room doors
            Vector3 Mid(CellPos cell, CellSide side) => Cell(cell.X, cell.Y) + CellarFloorBuilder.SideDir(side) * (cs * 0.5f);
            Vector3? lockedMid = null;
            if (l.HasLockedRoom)
            {
                FloorLink ll = l.Links[l.LockedLinkIndex];
                int r = ll.RoomA == lockedRoom ? ll.RoomA : ll.RoomB, d = ll.RoomA == lockedRoom ? ll.DoorA : ll.DoorB;
                lockedMid = Mid(l.Rooms[r].DoorCell(d), l.Rooms[r].Room.Doors[d].Side);
            }
            foreach (PlacedCorridor c in l.Corridors)
                for (int side = 0; side < 4; side++)
                {
                    if ((c.Mask & (1 << side)) == 0) continue;
                    Vector3 a = Cell(c.X, c.Y), m = Mid(new CellPos(c.X, c.Y), (CellSide)side);
                    bool toGate = lockedMid.HasValue && (m - lockedMid.Value).sqrMagnitude < 0.01f;
                    s.Paths.Add(new IntendedPath { Name = $"corridor ({c.X},{c.Y}) {(CellSide)side} arm", A = a, B = m, TrimB = toGate ? 0.6f : 0f });
                }
            for (int li = 0; li < l.Links.Count; li++)
            {
                FloorLink link = l.Links[li];
                foreach (var (ri, di) in new[] { (link.RoomA, link.DoorA), (link.RoomB, link.DoorB) })
                {
                    if (ri < 0 || ri >= l.Rooms.Count) continue;
                    PlacedRoom r = l.Rooms[ri];
                    if (di < 0 || di >= r.Room.Doors.Count) continue;
                    CellSide side = r.Room.Doors[di].Side;
                    Vector3 m = Mid(r.DoorCell(di), side), inward = m - CellarFloorBuilder.SideDir(side) * 1.5f;
                    if (li == l.LockedLinkIndex) { s.Clear.Add(new KeepClear { Name = "locked doorway of " + r.RoomId, Pos = m, Radius = 0.7f }); continue; }
                    s.Paths.Add(new IntendedPath { Name = $"door {r.RoomId} #{di}", A = inward, B = m + CellarFloorBuilder.SideDir(side) * 1.0f });
                    s.Clear.Add(new KeepClear { Name = $"doorway {r.RoomId} #{di}", Pos = m, Radius = 0.7f });
                }
            }
            for (int i = 0; i < l.EntryStarts.Count; i++) s.Clear.Add(new KeepClear { Name = "entry start " + i, Pos = CellarFloorBuilder.EntryWorld(l, b, i), Radius = 0.45f });
            if (CellarFloorBuilder.TryStairWorld(l, b, out Vector3 st2)) s.Clear.Add(new KeepClear { Name = "stair exit", Pos = st2 + Vector3.forward * 0f, Radius = 0.3f });

            // planned walkable: floor and pillar cells of rooms, and corridor cells
            int n = CellarFloorLayout.GridSize;
            var plan = new bool[n, n];
            var room = new int[n, n];
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) room[x, y] = -1;
            for (int ri = 0; ri < l.Rooms.Count; ri++)
            {
                PlacedRoom r = l.Rooms[ri];
                for (int x = 0; x < r.Width; x++)
                    for (int y = 0; y < r.Height; y++)
                    {
                        int gx = r.X + x, gy = r.Y + y;
                        if (gx < 0 || gy < 0 || gx >= n || gy >= n) continue;
                        room[gx, gy] = ri;
                        if (r.Room.CellAt(x, y) != CellKind.Solid) plan[gx, gy] = true;
                    }
            }
            foreach (PlacedCorridor c in l.Corridors) if (c.X >= 0 && c.Y >= 0 && c.X < n && c.Y < n) plan[c.X, c.Y] = true;
            Vector3 origin = root.TransformPoint(Vector3.zero);
            bool ToGrid(float x, float z, out int gx, out int gy)
            {
                gx = Mathf.FloorToInt((x - origin.x) / cs); gy = Mathf.FloorToInt(-(z - origin.z) / cs);
                return gx >= 0 && gy >= 0 && gx < n && gy < n;
            }
            s.ExpectedWalkable = (x, z) =>
            {
                if (!ToGrid(x, z, out int gx, out int gy) || !plan[gx, gy]) return false;
                // the wall band on a cell edge is not floor
                float lx = (x - origin.x) - gx * cs, lz = -(z - origin.z) - gy * cs, edge = CellarFloorBuilder.WallThickness * 0.5f + 0.05f;
                return lx > edge && lx < cs - edge && lz > edge && lz < cs - edge || room[gx, gy] >= 0 && InsideRoom(gx, gy, lx, lz, edge);
            };
            bool InsideRoom(int gx, int gy, float lx, float lz, float edge)
            {
                int ri = room[gx, gy];
                bool Same(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && room[x, y] == ri && plan[x, y];
                if (lx <= edge && !Same(gx - 1, gy)) return false;
                if (lx >= cs - edge && !Same(gx + 1, gy)) return false;
                if (lz <= edge && !Same(gx, gy - 1)) return false;
                if (lz >= cs - edge && !Same(gx, gy + 1)) return false;
                return true;
            }
            s.Describe = p =>
            {
                if (!ToGrid(p.x, p.z, out int gx, out int gy)) return "outside the grid";
                int ri = room[gx, gy];
                if (ri >= 0) { PlacedRoom r = l.Rooms[ri]; return $"room {r.RoomId} rot {r.Rotation} cell ({gx - r.X},{gy - r.Y})"; }
                return plan[gx, gy] ? $"corridor ({gx},{gy})" : $"cell ({gx},{gy})";
            };
            return s;
        }
    }
}
