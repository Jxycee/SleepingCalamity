// Presentation/Cellars/CellarFloorBuilder.cs: M5 stage 2. Turns a Core CellarFloorLayout into a walkable greybox floor under a parent transform: one floor slab per
// occupied cell, walls on every blocked cell edge, a door frame in every open door, pillars, lamps, and coloured markers for chests, the key chest, the locked
// gate, the gate-key carrier, the entry starts, the spawns and the stair exit. Everything is created from the layout alone (no Random, no time, no editor API), in
// a fixed order, so every client that rebuilds (seed, floor) gets an identical hierarchy (stage 3 calls Build at runtime on every client).
// World frame: x grows east, z grows north, so grid y (south) maps to -z; cell (x, y) has its centre at ((x + 0.5) * 4, 0, -(y + 0.5) * 4).
// Variety layers (Core/Cellars/CellarFloorDressing + CellarAtmosphere): the builder also resolves the floor's dressing from the same layout and shows it: greybox
// shapes per filled socket (empty sockets draw nothing), the vignette prop groups, and the atmosphere (lamp count and intensity, fog density and tint, an odd-light
// patch, a dust-motes marker). Still a pure function of the layout, so every peer gets the same hierarchy.
// Authority: none (presentation of static data). Art is optional: pass a CellarFloorKit with materials and prefab templates (the Editor builder does) or null
// for plain URP Lit colours.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    // What the builder dresses with. Every field may be null; a null material falls back to a flat URP Lit colour.
    public sealed class CellarFloorKit
    {
        public Material Floor, Wall, Pillar, Trim, Flame;
        public Material LockedGate, Chest, KeyChest, Carrier, EntryStart, StairExit, SpawnT1, SpawnT2;
        public GameObject FloorTile;            // optional template: a 2 x 2 m flagstone, x -1..1, z 0..2, top at y 0 (four per cell); inactive templates are fine
        public GameObject Lantern;              // optional template: a lantern with its pivot at the bottom centre
        public Material LanternMaterial;        // applied to every renderer of the lantern clones when set
        public float FogBaseDensity;            // > 0: Build sets RenderSettings exponential fog to this density times the floor's FogPercent, tinted from FogBaseColor
        public Color FogBaseColor;              // the scene's fog colour before the floor's warm offset
    }

    public sealed class CellarFloorBuild
    {
        public Transform Root;
        public Bounds Bounds;                   // world bounds of the occupied cells (floor level)
        public int FloorTiles, Walls, DoorOpenings, Pillars, Lamps;
        public CellarFloorDressing Dressing;    // the resolved sockets, vignettes and atmosphere this build shows
        public int DressingProps, VignetteGroups;
        public bool DustMotes;                  // the floor's dust is thick enough to show (a marker object stands in until the particle prefab exists)
        public float FogDensity;                // the density applied when the kit asked for fog, else 0
        public Vector3 EntryCenter;             // the first entry start cell
    }

    public static class CellarFloorBuilder
    {
        public const float CellSize = CellarFloorLayout.CellMeters, WallHeight = 3.6f, WallThickness = 0.4f, DoorWidth = 2.4f, DoorHeight = 2.8f, PillarSize = 1.0f;
        public const string FloorGroup = "Floor", WallGroup = "Walls", DoorGroup = "Doors", PillarGroup = "Pillars", MarkerGroup = "Markers", LampGroup = "Lamps";
        public const string DressingGroup = "Dressing", VignetteGroup = "Vignettes", AtmosphereGroup = "Atmosphere";
        const float Tile = 2f;
        static readonly Color GoldLamp = new Color(0.839f, 0.635f, 0.290f);   // #d6a24a

        public static Vector3 CellCenter(int x, int y) => new Vector3((x + 0.5f) * CellSize, 0f, -(y + 0.5f) * CellSize);

        // Unit step on the ground for a side (north = +z).
        public static Vector3 SideDir(CellSide s)
        {
            switch (s)
            {
                case CellSide.North: return new Vector3(0, 0, 1);
                case CellSide.East: return new Vector3(1, 0, 0);
                case CellSide.South: return new Vector3(0, 0, -1);
                default: return new Vector3(-1, 0, 0);
            }
        }

        // Per-layout lookup: which grid cells are occupied and what holds them.
        sealed class Grid
        {
            public readonly bool[,] Occ = new bool[CellarFloorLayout.GridSize, CellarFloorLayout.GridSize];
            public readonly int[,] Room = new int[CellarFloorLayout.GridSize, CellarFloorLayout.GridSize];   // room index or -1
            public readonly PlacedCorridor[,] Corr = new PlacedCorridor[CellarFloorLayout.GridSize, CellarFloorLayout.GridSize];
            readonly CellarFloorLayout layout;

            public Grid(CellarFloorLayout l)
            {
                layout = l;
                for (int x = 0; x < CellarFloorLayout.GridSize; x++) for (int y = 0; y < CellarFloorLayout.GridSize; y++) Room[x, y] = -1;
                for (int i = 0; i < l.Rooms.Count; i++)
                {
                    PlacedRoom p = l.Rooms[i];
                    for (int lx = 0; lx < p.Width; lx++) for (int ly = 0; ly < p.Height; ly++)
                        if (p.Room.CellAt(lx, ly) != CellKind.Solid && In(p.X + lx, p.Y + ly)) { Occ[p.X + lx, p.Y + ly] = true; Room[p.X + lx, p.Y + ly] = i; }
                }
                foreach (PlacedCorridor c in l.Corridors) if (In(c.X, c.Y)) { Occ[c.X, c.Y] = true; Corr[c.X, c.Y] = c; }
            }

            public static bool In(int x, int y) => x >= 0 && y >= 0 && x < CellarFloorLayout.GridSize && y < CellarFloorLayout.GridSize;
            public bool Occupied(int x, int y) => In(x, y) && Occ[x, y];

            // The open door of the room cell (x, y) on `side`, or -1.
            public int OpenDoorAt(int x, int y, CellSide side)
            {
                int r = Room[x, y]; if (r < 0) return -1;
                PlacedRoom p = layout.Rooms[r];
                for (int d = 0; d < p.Room.Doors.Count; d++)
                {
                    CellDoor door = p.Room.Doors[d];
                    if (p.X + door.X == x && p.Y + door.Y == y && door.Side == side && layout.DoorIsOpen(r, d)) return d;
                }
                return -1;
            }

            // Whether one can walk out of cell (x, y) across `side` (judged from this cell only).
            public bool OpenFrom(int x, int y, CellSide side)
            {
                var (dx, dy) = CellarRoom.Step(side); int nx = x + dx, ny = y + dy;
                if (!Occupied(nx, ny)) return false;
                if (Room[x, y] >= 0) return Room[nx, ny] == Room[x, y] || OpenDoorAt(x, y, side) >= 0;
                PlacedCorridor c = Corr[x, y];
                return c != null && (c.Mask & CellarRoom.MaskBit(side)) != 0;
            }
        }

        public static CellarFloorBuild Build(CellarFloorLayout layout, Transform parent, CellarFloorKit kit = null)
        {
            if (layout == null) throw new System.ArgumentNullException(nameof(layout));
            kit = kit ?? new CellarFloorKit();
            var root = new GameObject("CellarFloor_s" + layout.Seed + "_f" + layout.Floor).transform;
            root.SetParent(parent, false);
            var result = new CellarFloorBuild { Root = root };
            var grid = new Grid(layout);
            var mats = new Mats(kit);

            Transform floor = Group(root, FloorGroup), walls = Group(root, WallGroup), doors = Group(root, DoorGroup), pillars = Group(root, PillarGroup);
            Transform markers = Group(root, MarkerGroup), lamps = Group(root, LampGroup);

            bool any = false; Bounds bounds = default;
            var side4 = new[] { CellSide.North, CellSide.East, CellSide.South, CellSide.West };
            for (int y = 0; y < CellarFloorLayout.GridSize; y++)
                for (int x = 0; x < CellarFloorLayout.GridSize; x++)
                {
                    if (!grid.Occ[x, y]) continue;
                    Vector3 c = CellCenter(x, y);
                    Floor(floor, kit, mats, x, y, c); result.FloorTiles++;
                    var cb = new Bounds(c, new Vector3(CellSize, 0f, CellSize));
                    if (!any) { bounds = cb; any = true; } else bounds.Encapsulate(cb);

                    int r = grid.Room[x, y];
                    if (r >= 0 && layout.Rooms[r].Room.CellAt(x - layout.Rooms[r].X, y - layout.Rooms[r].Y) == CellKind.Pillar)
                    {
                        Box(pillars, "Pillar_" + x + "_" + y, c + new Vector3(0, WallHeight * 0.5f, 0), new Vector3(PillarSize, WallHeight, PillarSize), mats.Pillar, 2f, true); result.Pillars++;
                    }

                    foreach (CellSide s in side4)
                    {
                        var (dx, dy) = CellarRoom.Step(s); int nx = x + dx, ny = y + dy;
                        bool nOcc = grid.Occupied(nx, ny);
                        bool open = grid.OpenFrom(x, y, s) && (!nOcc || grid.OpenFrom(nx, ny, CellarRoom.Opposite(s)));
                        if (open) continue;   // passages draw nothing; door frames come from the room side below
                        if (nOcc && (s == CellSide.North || s == CellSide.West)) continue;   // a wall between two occupied cells is drawn once, from the north/west cell
                        Vector3 mid = c + SideDir(s) * (CellSize * 0.5f);
                        bool ns = s == CellSide.North || s == CellSide.South;
                        Box(walls, "Wall_" + x + "_" + y + "_" + s, mid + new Vector3(0, WallHeight * 0.5f, 0),
                            ns ? new Vector3(CellSize + WallThickness, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, CellSize + WallThickness), mats.Wall, 2f, true);
                        result.Walls++;
                    }
                }
            result.Bounds = bounds;

            // door frames (and the locked gate) in every open door, from the room side
            int lockedRoom = layout.LockedRoomIndex; int lockedDoor = -1;
            if (layout.LockedLinkIndex >= 0)
            {
                FloorLink l = layout.Links[layout.LockedLinkIndex];
                lockedDoor = l.RoomA == lockedRoom ? l.DoorA : l.RoomB == lockedRoom ? l.DoorB : -1;
            }
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                for (int d = 0; d < p.Room.Doors.Count; d++)
                {
                    if (!layout.DoorIsOpen(r, d)) continue;
                    CellDoor door = p.Room.Doors[d]; CellPos cell = p.DoorCell(d);
                    Vector3 mid = CellCenter(cell.X, cell.Y) + SideDir(door.Side) * (CellSize * 0.5f);
                    bool ns = door.Side == CellSide.North || door.Side == CellSide.South;
                    Vector3 along = ns ? Vector3.right : Vector3.forward;
                    var frame = new GameObject("DoorOpening_r" + r + "_d" + d).transform; frame.SetParent(doors, false); frame.position = mid;
                    float jamb = (CellSize + WallThickness - DoorWidth) * 0.5f, jambC = DoorWidth * 0.5f + jamb * 0.5f;
                    for (int k = -1; k <= 1; k += 2)
                        Box(frame, k < 0 ? "JambA" : "JambB", mid + along * (k * jambC) + new Vector3(0, DoorHeight * 0.5f, 0),
                            ns ? new Vector3(jamb, DoorHeight, WallThickness) : new Vector3(WallThickness, DoorHeight, jamb), mats.Trim, 2f, true);
                    Box(frame, "Lintel", mid + new Vector3(0, (DoorHeight + WallHeight) * 0.5f, 0),
                        ns ? new Vector3(CellSize + WallThickness, WallHeight - DoorHeight, WallThickness) : new Vector3(WallThickness, WallHeight - DoorHeight, CellSize + WallThickness), mats.Trim, 2f, true);
                    result.DoorOpenings++;
                    if (r == lockedRoom && d == lockedDoor)
                        Box(frame, "LockedGate", mid + new Vector3(0, DoorHeight * 0.5f, 0),
                            ns ? new Vector3(DoorWidth, DoorHeight, 0.3f) : new Vector3(0.3f, DoorHeight, DoorWidth), mats.Marker(kit.LockedGate, new Color(0.75f, 0.22f, 0.10f)), 2f, true);
                }
            }

            CellarFloorDressing dressing = CellarFloorDressing.Resolve(layout); result.Dressing = dressing;
            Markers(layout, markers, mats, kit, result);
            Lamps(layout, dressing, lamps, kit, mats, result);
            DressSockets(dressing, Group(root, DressingGroup), mats, result);
            Vignettes(dressing, Group(root, VignetteGroup), mats, result);
            Atmosphere(layout, dressing, Group(root, AtmosphereGroup), kit, result);
            return result;
        }

        // ------------------------------------------------------------------------------------------------ floor
        static void Floor(Transform group, CellarFloorKit kit, Mats mats, int x, int y, Vector3 c)
        {
            var cell = new GameObject("Floor_" + x + "_" + y).transform; cell.SetParent(group, false); cell.position = c;
            if (kit.FloorTile == null)
            {
                Box(cell, "Slab", c + new Vector3(0, -0.1f, 0), new Vector3(CellSize, 0.2f, CellSize), mats.Floor, Tile, true);
                return;
            }
            var col = cell.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, -0.1f, 0); col.size = new Vector3(CellSize, 0.2f, CellSize);
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
            {
                GameObject t = Object.Instantiate(kit.FloorTile, cell, false);
                t.name = "Tile_" + i + "_" + j; t.SetActive(true);
                t.transform.position = c + new Vector3(-CellSize * 0.5f + 1f + i * 2f, 0f, -CellSize * 0.5f + j * 2f);   // tile x -1..1, z 0..2
                t.transform.rotation = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------------------------------------ markers
        static void Markers(CellarFloorLayout layout, Transform group, Mats mats, CellarFloorKit kit, CellarFloorBuild result)
        {
            for (int i = 0; i < layout.Chests.Count; i++)
            {
                FloorChest ch = layout.Chests[i]; Vector3 c = CellCenter(ch.X, ch.Y);
                Material m = ch.HoldsKey ? mats.Marker(kit.KeyChest, new Color(1.0f, 0.78f, 0.15f)) : mats.Marker(kit.Chest, new Color(0.45f, 0.26f, 0.10f));
                Box(group, (ch.HoldsKey ? "KeyChest_" : "Chest_") + ch.X + "_" + ch.Y, c + new Vector3(0, 0.4f, 0), new Vector3(1.1f, 0.8f, 0.7f), m, 1f, false);
            }
            if (layout.Carrier != null)
            {
                GameObject g = Prim(group, "Carrier_" + layout.Carrier.X + "_" + layout.Carrier.Y, PrimitiveType.Capsule, CellCenter(layout.Carrier.X, layout.Carrier.Y) + new Vector3(0, 0.9f, 0),
                    new Vector3(0.7f, 0.9f, 0.7f), mats.Marker(kit.Carrier, new Color(0.65f, 0.10f, 0.10f)));
                g.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            }
            for (int i = 0; i < layout.EntryStarts.Count; i++)
            {
                CellPos s = layout.EntryStarts[i];
                Prim(group, "EntryStart_" + i, PrimitiveType.Cylinder, CellCenter(s.X, s.Y) + new Vector3(0, 0.03f, 0), new Vector3(1.4f, 0.03f, 1.4f), mats.Marker(kit.EntryStart, new Color(0.35f, 0.62f, 0.22f)));
                if (i == 0) result.EntryCenter = CellCenter(s.X, s.Y);
            }
            if (layout.StairExit.HasValue)
            {
                CellPos s = layout.StairExit.Value;
                Prim(group, "StairExit", PrimitiveType.Cylinder, CellCenter(s.X, s.Y) + new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.4f, 1.6f), mats.Marker(kit.StairExit, new Color(0.55f, 0.30f, 0.70f)));
            }
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                for (int i = 0; i < p.Room.Spawns.Count; i++)
                {
                    CellarSpawn sp = p.Room.Spawns[i]; Vector3 c = CellCenter(p.X + sp.X, p.Y + sp.Y);
                    bool t2 = sp.Tier == SpawnTier.Tier2;
                    Prim(group, "Spawn_r" + r + "_" + i + (t2 ? "_t2" : "_t1"), PrimitiveType.Cylinder, c + new Vector3(0, 0.02f, 0), new Vector3(t2 ? 0.9f : 0.6f, 0.02f, t2 ? 0.9f : 0.6f),
                        t2 ? mats.Marker(kit.SpawnT2, new Color(0.85f, 0.45f, 0.10f)) : mats.Marker(kit.SpawnT1, new Color(0.62f, 0.60f, 0.55f)));
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ lamps
        // Filled room Lantern sockets hang on their wall; a room with no Lantern socket at all gets a centre lamp; most corridor cells get a ceiling lamp (every third, thinned by
        // the floor's lamp percent). The lamp percent also scales every intensity; on a dark floor combat rooms without a lantern socket keep their centre lamp off.
        static void Lamps(CellarFloorLayout layout, CellarFloorDressing dressing, Transform group, CellarFloorKit kit, Mats mats, CellarFloorBuild result)
        {
            CellarAtmosphere atm = dressing.Atmosphere; float k = Mathf.Clamp(atm.LampPercent, 20, 115) / 100f;
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r]; int n = 0; bool hasSocket = false;
                foreach (DressedSocket s in dressing.SocketsOf(r))
                {
                    if (s.Kind != SocketKind.Lantern) continue;
                    hasSocket = true;
                    if (!s.Filled) continue;
                    Vector3 c = CellCenter(s.Cell.X, s.Cell.Y);
                    Vector3 wall = SideDir(CellarRoom.Opposite(s.Facing));
                    Lamp(group, "Lamp_r" + r + "_" + n++, c + wall * (CellSize * 0.5f - 0.3f) + new Vector3(0, 2.3f, 0), kit, mats, 16.0f * k, 16f, true, GoldLamp);
                    result.Lamps++;
                }
                if (hasSocket || (atm.Dark && p.Room.Kind == RoomKind.Combat)) continue;
                // nearest floor cell to the room's centre
                float cx = p.X + p.Width * 0.5f - 0.5f, cy = p.Y + p.Height * 0.5f - 0.5f; int bx = -1, by = -1; float best = float.MaxValue;
                for (int lx = 0; lx < p.Width; lx++) for (int ly = 0; ly < p.Height; ly++)
                {
                    if (p.Room.CellAt(lx, ly) != CellKind.Floor) continue;
                    float d = (p.X + lx - cx) * (p.X + lx - cx) + (p.Y + ly - cy) * (p.Y + ly - cy);
                    if (d < best) { best = d; bx = p.X + lx; by = p.Y + ly; }
                }
                if (bx < 0) continue;
                Lamp(group, "Lamp_r" + r + "_c", CellCenter(bx, by) + new Vector3(0, 3.0f, 0), kit, mats, 14.0f * k, 14f, false, GoldLamp);
                result.Lamps++;
            }
            foreach (PlacedCorridor c in layout.Corridors)
            {
                if ((c.X + c.Y) % 3 != 0) continue;
                if ((c.X * 31 + c.Y * 17) % 100 >= atm.LampPercent) continue;   // thinned by position, no RNG: the same cells on every peer
                Lamp(group, "Lamp_c" + c.X + "_" + c.Y, CellCenter(c.X, c.Y) + new Vector3(0, 3.0f, 0), kit, mats, 8.0f * k, 10f, false, GoldLamp);
                result.Lamps++;
            }
        }

        static void Lamp(Transform group, string name, Vector3 pos, CellarFloorKit kit, Mats mats, float intensity, float range, bool shadows, Color color)
        {
            var go = new GameObject(name).transform; go.SetParent(group, false); go.position = pos;
            var lg = new GameObject("Light"); lg.transform.SetParent(go, false);
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (shadows) { l.shadowStrength = 1f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.4f; }
            if (kit.Lantern != null)
            {
                GameObject lan = Object.Instantiate(kit.Lantern, go, false); lan.name = "Lantern"; lan.SetActive(true);
                if (kit.LanternMaterial != null)
                    foreach (Renderer rd in lan.GetComponentsInChildren<Renderer>(true))
                    { var m = new Material[Mathf.Max(1, rd.sharedMaterials.Length)]; for (int k = 0; k < m.Length; k++) m[k] = kit.LanternMaterial; rd.sharedMaterials = m; }
                var b = new Bounds(pos, Vector3.zero); bool first = true;   // pivot at the bottom centre: hang the top ring from the lamp point
                foreach (Renderer rd in lan.GetComponentsInChildren<Renderer>(true)) { if (first) { b = rd.bounds; first = false; } else b.Encapsulate(rd.bounds); }
                lan.transform.position = new Vector3(pos.x, pos.y - b.size.y, pos.z);
            }
            else Prim(go, "Flame", PrimitiveType.Sphere, pos - new Vector3(0, 0.15f, 0), new Vector3(0.18f, 0.24f, 0.18f), mats.Marker(kit.Flame, new Color(1.0f, 0.7f, 0.3f)));
        }

        // ------------------------------------------------------------------------------------------------ dressing
        static readonly Color BarrelCol = new Color(0.36f, 0.24f, 0.14f), CrateCol = new Color(0.42f, 0.31f, 0.18f), ShelfCol = new Color(0.33f, 0.24f, 0.16f),
            RackCol = new Color(0.30f, 0.20f, 0.12f), ChuteCol = new Color(0.12f, 0.11f, 0.10f), RootsCol = new Color(0.30f, 0.22f, 0.14f);

        // One greybox shape group per filled socket (Lantern sockets are drawn by Lamps). Wall-mounted kinds sit against the wall behind them (the side opposite their
        // facing); Barrel and Crate stand in their cell, pushed toward that side. Variants change the silhouette: stacked, tipped, open, broken.
        static void DressSockets(CellarFloorDressing dressing, Transform group, Mats mats, CellarFloorBuild result)
        {
            foreach (DressedSocket s in dressing.Sockets)
            {
                if (!s.Filled || s.Kind == SocketKind.Lantern) continue;
                Vector3 c = CellCenter(s.Cell.X, s.Cell.Y); Vector3 back = SideDir(CellarRoom.Opposite(s.Facing));
                bool ns = s.Facing == CellSide.North || s.Facing == CellSide.South;   // a wall piece is long across its facing
                var root = new GameObject("Dress_r" + s.RoomIndex + "_" + s.Socket.Id + "_" + s.Variant).transform; root.SetParent(group, false); root.position = c;
                Vector3 at = c + back * (CellSize * 0.5f - 0.45f);
                switch (s.Kind)
                {
                    case SocketKind.Shelf:
                        Prim(root, "Shelf", PrimitiveType.Cube, at + new Vector3(0, 1.0f, 0), ns ? new Vector3(2.6f, 2.0f, 0.5f) : new Vector3(0.5f, 2.0f, 2.6f), mats.Flat(ShelfCol));
                        if (s.Variant != "Sacks") Prim(root, s.Variant, PrimitiveType.Cylinder, at - back * 0.1f + new Vector3(0, 1.3f, 0), new Vector3(0.4f, 0.2f, 0.4f), mats.Flat(s.Variant == "Hams" ? new Color(0.55f, 0.28f, 0.22f) : new Color(0.55f, 0.50f, 0.38f)));
                        break;
                    case SocketKind.Rack:
                        Prim(root, "Rack", PrimitiveType.Cube, at + new Vector3(0, 1.1f, 0), ns ? new Vector3(2.8f, 2.2f, 0.6f) : new Vector3(0.6f, 2.2f, 2.8f), mats.Flat(RackCol));
                        if (s.Variant != "Empty") Prim(root, s.Variant, PrimitiveType.Cylinder, at - back * 0.15f + new Vector3(0, 1.2f, 0), new Vector3(s.Variant == "Casks" ? 0.7f : 0.25f, 0.5f, s.Variant == "Casks" ? 0.7f : 0.25f), mats.Flat(new Color(0.28f, 0.36f, 0.20f)));
                        break;
                    case SocketKind.Chute:
                        Prim(root, "Chute", PrimitiveType.Cube, at + new Vector3(0, 1.5f, 0), ns ? new Vector3(1.4f, 3.0f, 0.6f) : new Vector3(0.6f, 3.0f, 1.4f), mats.Flat(ChuteCol));
                        if (s.Variant == "Heaped") Prim(root, "Coal", PrimitiveType.Sphere, c + back * 1.1f + new Vector3(0, 0.3f, 0), new Vector3(1.4f, 0.7f, 1.4f), mats.Flat(ChuteCol));
                        break;
                    case SocketKind.Roots:
                    {
                        float thick = s.Variant == "Thick" ? 0.35f : 0.18f;
                        Prim(root, "Roots", PrimitiveType.Cylinder, at + new Vector3(0, 2.0f, 0), new Vector3(thick, 1.5f, thick), mats.Flat(RootsCol));
                        break;
                    }
                    case SocketKind.Barrel:
                    {
                        Vector3 p0 = c + back * 1.0f;
                        if (s.Variant == "Tipped") { GameObject g = Prim(root, "Barrel", PrimitiveType.Cylinder, p0 + new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.55f, 0.9f), mats.Flat(BarrelCol)); g.transform.rotation = Quaternion.Euler(0, 0, 90); }
                        else
                        {
                            Prim(root, "Barrel", PrimitiveType.Cylinder, p0 + new Vector3(0, 0.55f, 0), new Vector3(0.95f, 0.55f, 0.95f), mats.Flat(BarrelCol));
                            if (s.Variant == "Stacked") Prim(root, "BarrelTop", PrimitiveType.Cylinder, p0 + new Vector3(0, 1.65f, 0), new Vector3(0.9f, 0.55f, 0.9f), mats.Flat(BarrelCol));
                        }
                        break;
                    }
                    default:   // Crate
                    {
                        Vector3 p0 = c + back * 1.0f;
                        if (s.Variant == "Open") { Prim(root, "Crate", PrimitiveType.Cube, p0 + new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.6f, 1.1f), mats.Flat(CrateCol)); Prim(root, "Lid", PrimitiveType.Cube, p0 + new Vector3(0.7f, 0.1f, 0.2f), new Vector3(1.0f, 0.08f, 0.9f), mats.Flat(CrateCol)); }
                        else if (s.Variant == "Broken") { GameObject g = Prim(root, "Crate", PrimitiveType.Cube, p0 + new Vector3(0, 0.35f, 0), new Vector3(1.1f, 0.7f, 1.1f), mats.Flat(CrateCol)); g.transform.rotation = Quaternion.Euler(0, 20, 12); }
                        else
                        {
                            Prim(root, "Crate", PrimitiveType.Cube, p0 + new Vector3(0, 0.5f, 0), new Vector3(1.1f, 1.0f, 1.1f), mats.Flat(CrateCol));
                            Prim(root, "CrateTop", PrimitiveType.Cube, p0 + new Vector3(0.1f, 1.5f, 0), new Vector3(0.9f, 0.9f, 0.9f), mats.Flat(CrateCol));
                        }
                        break;
                    }
                }
                result.DressingProps++;
            }
        }

        // The vignette prop groups: simple primitives in flat colours under one transform per vignette, turned by the placement's quarter turns.
        static void Vignettes(CellarFloorDressing dressing, Transform group, Mats mats, CellarFloorBuild result)
        {
            foreach (PlacedVignette pv in dressing.Vignettes)
            {
                var root = new GameObject("Vignette_" + pv.Vignette.Id + "_r" + pv.RoomIndex).transform; root.SetParent(group, false);
                root.position = CellCenter(pv.Cell.X, pv.Cell.Y); root.rotation = Quaternion.Euler(0, 90f * pv.Turns, 0);   // a positive turn about +Y runs north to east: clockwise from above
                foreach (VignetteProp pr in pv.Vignette.Props)
                {
                    PrimitiveType type = pr.Shape == PropShape.Box ? PrimitiveType.Cube : pr.Shape == PropShape.Cylinder ? PrimitiveType.Cylinder : pr.Shape == PropShape.Sphere ? PrimitiveType.Sphere : PrimitiveType.Capsule;
                    Vector3 scale = pr.Shape == PropShape.Cylinder || pr.Shape == PropShape.Capsule ? new Vector3(pr.SizeX, pr.SizeY * 0.5f, pr.SizeX) : new Vector3(pr.SizeX, pr.SizeY, pr.SizeZ);
                    var col = new Color(((pr.Rgb >> 16) & 255) / 255f, ((pr.Rgb >> 8) & 255) / 255f, (pr.Rgb & 255) / 255f);
                    GameObject g = Prim(root, pr.Name, type, root.position, scale, mats.Flat(col));
                    g.transform.localPosition = new Vector3(pr.X, pr.Y, pr.Z);
                    // a capsule lies on its side (its length runs along the yaw); everything else turns about the vertical
                    g.transform.localRotation = pr.Shape == PropShape.Capsule ? Quaternion.Euler(0, pr.YawDegrees, 90) : Quaternion.Euler(0, pr.YawDegrees, 0);
                }
                result.VignetteGroups++;
            }
        }

        // ------------------------------------------------------------------------------------------------ atmosphere
        // Fog (when the kit gives a base), the odd-light patch of one combat room (a warm tinted point light) and the dust-motes marker. TODO: a ParticleSystem prefab for the
        // motes once the dungeon corner kit has one (none exists yet); the empty marker object marks where it goes and lets a test read the toggle.
        static void Atmosphere(CellarFloorLayout layout, CellarFloorDressing dressing, Transform group, CellarFloorKit kit, CellarFloorBuild result)
        {
            CellarAtmosphere atm = dressing.Atmosphere;
            if (kit.FogBaseDensity > 0f)
            {
                atm.FogTintOffset(out float fr, out float fg, out float fb);
                result.FogDensity = kit.FogBaseDensity * atm.FogPercent / 100f;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = result.FogDensity;
                RenderSettings.fogColor = new Color(Mathf.Clamp01(kit.FogBaseColor.r + fr), Mathf.Clamp01(kit.FogBaseColor.g + fg), kit.FogBaseColor.b + fb);
            }
            if (atm.OddLightRoom >= 0)
            {
                PlacedRoom p = layout.Rooms[atm.OddLightRoom];
                var go = new GameObject("OddLight_r" + atm.OddLightRoom + "_" + atm.OddLight).transform; go.SetParent(group, false);
                go.position = CellCenter(p.X + p.Width / 2, p.Y + p.Height / 2) + new Vector3(0, 2.2f, 0);
                Light l = go.gameObject.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = atm.Dark ? 6f : 4f; l.shadows = LightShadows.None;
                int rgb = atm.OddLightRgb; l.color = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
            }
            result.DustMotes = atm.DustVisibleHere;
            if (result.DustMotes) { var dust = new GameObject("DustMotes_" + atm.DustPercent).transform; dust.SetParent(group, false); dust.position = result.Bounds.center + new Vector3(0, 1.8f, 0); }
        }

        // ------------------------------------------------------------------------------------------------ helpers
        static Transform Group(Transform root, string name) { var t = new GameObject(name).transform; t.SetParent(root, false); return t; }

        // A mesh box with a UV tile of `tile` metres; with collider when walkable geometry (floor, walls, pillars, frames).
        static GameObject Box(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat, float tile, bool collider)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = worldPos;
            go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            go.AddComponent<LookTestGeometry>().Configure(LookTestGeometry.Shape.Box, size, tile, 24, collider);
            return go;
        }

        static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 worldPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false); go.transform.position = worldPos; go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // Kit material or a flat fallback, cached per colour so a floor shares a handful of materials.
        sealed class Mats
        {
            readonly Dictionary<Color, Material> flat = new Dictionary<Color, Material>();
            public readonly Material Floor, Wall, Pillar, Trim;
            public Mats(CellarFloorKit k)
            {
                Floor = k.Floor ?? Flat(new Color(0.42f, 0.38f, 0.33f)); Wall = k.Wall ?? Flat(new Color(0.55f, 0.47f, 0.38f));
                Pillar = k.Pillar ?? Flat(new Color(0.50f, 0.46f, 0.42f)); Trim = k.Trim ?? Flat(new Color(0.34f, 0.25f, 0.17f));
            }
            public Material Marker(Material fromKit, Color fallback) => fromKit != null ? fromKit : Flat(fallback);
            public Material Flat(Color c)
            {
                if (flat.TryGetValue(c, out Material m)) return m;
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(sh) { name = "CellarFlat" };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.color = c; if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
                flat[c] = m; return m;
            }
        }
    }
}
