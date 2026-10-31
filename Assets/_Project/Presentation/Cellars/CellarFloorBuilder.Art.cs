// Presentation/Cellars/CellarFloorBuilder.Art.cs: the Cellars floor art layer (room art pass, 2026-10-31). Dresses a built floor so it reads like the approved
// dungeon corner, from the layout alone (per cell and per room data, no Random), so every seed builds and every client gets the same hierarchy:
//  - Ceilings: rooms get one barrel bay per cell along the room's long axis (springing 2.6 m, the corner's 2.0 x 2.5 m elliptical profile, crown 5.1 m) with oak
//    valley beams between parallel bays, a stone cornice along the side walls and a limewash tympanum over every end wall and door; straight corridor cells get
//    two B5 VaultSegments (scaled 0.725 across into the 4 m cell) with ArchRibs between bays; corridor corners and junctions get a groin bay with the same profile.
//  - Wall faces: every closed cell side draws its own face 0.2 m deep (the wall boxes are colliders only): limewash B5 EndWall (or NicheWall on a Rack socket)
//    across the room's axis, brick along it and in corridors; nothing where a kit VaultSegment already carries the wall.
//  - Dressing from the room's sockets: Shelf -> LarderShelf, Rack -> NicheWall with a candle cluster, Barrel -> Barrels with a candle, Crate -> oak crates,
//    Chute -> CoalChute, Roots -> Roots from the crown; kit piers on Pillar cells; a candle cluster on every chest; dust motes at the wall lanterns; one warm
//    unshadowed fill light per room (the corner's larder light, standing in for bounce).
// Groups: Vaults (kit corridor vaults and ribs), Ceiling (procedural bays, tympana, beams, cornices, roots: the overview capture hides it), Faces, Dressing.
// Authority: none (presentation of static data).
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.Presentation
{
    public static partial class CellarFloorBuilder
    {
        public const string VaultGroup = "Vaults", CeilingGroup = "Ceiling", FaceGroup = "Faces", DressingGroup = "Dressing";
        public const float RoomSpring = 2.6f, CorridorSpring = 1.6f, VaultHalfSpan = 2.0f, VaultRise = 2.5f;
        public const float WallLampIntensity = 18f, CentreLampIntensity = 18f, CorridorLampIntensity = 11f;
        const float FaceDepth = 0.2f, KitAcross = 0.725f;   // VaultSegment / ArchRib (5 m inside, 6 m outside): 0.725 keeps the outer wall face 2.175 m from the axis, just behind a neighbour's 0.2 m face, inner face 1.83 m (on the wall colliders)
        static readonly Color CandleGold = new Color(1.0f, 0.72f, 0.44f), FillGold = new Color(1.0f, 0.76f, 0.52f);
        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        enum Edge { Closed, Door, Through }
        enum Bay { Room, StraightNS, StraightEW, Junction }

        static bool HasPiece(CellarFloorKit kit, string name) => kit != null && kit.Pieces != null && kit.Pieces.TryGetValue(name, out GameObject g) && g != null;

        static void Dress(CellarFloorLayout layout, Grid grid, Transform root, CellarFloorKit kit, Mats mats, CellarFloorBuild result)
        {
            Transform vaults = Group(root, VaultGroup), ceiling = Group(root, CeilingGroup), faces = Group(root, FaceGroup), dressing = Group(root, DressingGroup);

            // racks claim their wall face (the niche wall replaces the plain end wall or brick)
            var rackFaces = new HashSet<(int, int, CellSide)>();
            for (int r = 0; r < layout.Rooms.Count; r++)
                foreach (DressingSocket s in layout.Rooms[r].Room.Sockets)
                    if (s.Kind == SocketKind.Rack) rackFaces.Add((layout.Rooms[r].X + s.X, layout.Rooms[r].Y + s.Y, CellarRoom.Opposite(s.Facing)));

            for (int y = 0; y < CellarFloorLayout.GridSize; y++)
                for (int x = 0; x < CellarFloorLayout.GridSize; x++)
                {
                    if (!grid.Occ[x, y]) continue;
                    Vector3 c = CellCenter(x, y);
                    Bay bay = BayOf(layout, grid, x, y);
                    bool room = bay == Bay.Room;
                    bool alongZ = room ? RoomAlongZ(layout.Rooms[grid.Room[x, y]]) : bay == Bay.StraightNS;   // barrel axis (junctions: unused)
                    float spring = room ? RoomSpring : CorridorSpring, crown = spring + VaultRise;
                    string id = x + "_" + y;

                    // ceiling bay
                    if (bay == Bay.Junction)
                    {
                        Shape(ceiling, "Groin_" + id, c + new Vector3(0, spring, 0), Quaternion.identity, Vector3.one, LookTestGeometry.Shape.GroinVault,
                            new Vector3(VaultHalfSpan, VaultRise, 0f), mats.Ceiling, 16);
                        result.Vaults++;
                    }
                    else if (!room && HasPiece(kit, "VaultSegment"))
                    {
                        Quaternion rot = alongZ ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
                        Vector3 back = alongZ ? new Vector3(0, 0, -CellSize * 0.5f) : new Vector3(-CellSize * 0.5f, 0, 0);
                        for (int k = 0; k < 2; k++)
                            PlacePiece(kit, "VaultSegment", vaults, "VaultSegment_" + id + "_" + k, c + back + (-back) * k, rot, new Vector3(KitAcross, 1f, 1f));
                        result.Vaults++;
                    }
                    else
                    {
                        Quaternion rot = alongZ ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
                        Vector3 back = alongZ ? new Vector3(0, 0, -CellSize * 0.5f) : new Vector3(-CellSize * 0.5f, 0, 0);
                        Shape(ceiling, "Barrel_" + id, c + back + new Vector3(0, spring, 0), rot, new Vector3(1f, VaultRise / VaultHalfSpan, 1f), LookTestGeometry.Shape.VaultInterior,
                            new Vector3(VaultHalfSpan, 0f, CellSize), mats.Ceiling, 24);
                        result.Vaults++;
                    }

                    // sides
                    foreach (CellSide s in Sides)
                    {
                        Edge e = EdgeOf(grid, x, y, s);
                        Vector3 d = SideDir(s);
                        bool ns = s == CellSide.North || s == CellSide.South;
                        bool across = alongZ == ns;   // this side closes the barrel's end (room end walls)
                        Vector3 alongEdge = ns ? new Vector3(CellSize, 0, 0) : new Vector3(0, 0, CellSize);
                        string sid = id + "_" + s;

                        if (e == Edge.Through)
                        {
                            // a valley between two parallel room bays: an oak beam on the springing line, drawn once from the west/north cell
                            if (room && !across && (s == CellSide.East || s == CellSide.South))
                                Box(ceiling, "Beam_" + sid, c + d * (CellSize * 0.5f) + new Vector3(0, spring - 0.17f, 0), Size(alongEdge, 0.36f, 0.34f, ns), mats.Trim, 2f, false);
                            else if (!room && bay != Bay.Junction && across && HasPiece(kit, "ArchRib") && RibAt(layout, grid, x, y, s, bay))
                                PlacePiece(kit, "ArchRib", vaults, "ArchRib_" + sid, c + d * (CellSize * 0.5f), alongZ ? Quaternion.identity : Quaternion.Euler(0, 90, 0), new Vector3(KitAcross, 1f, 1f));
                            continue;
                        }

                        bool kitWall = !room && bay != Bay.Junction && !across && e == Edge.Closed && HasPiece(kit, "VaultSegment");   // straight corridor side
                        Vector3 faceMid = c + d * (CellSize * 0.5f - FaceDepth * 0.5f);

                        // the face under the springing / lintel
                        if (e == Edge.Closed && !kitWall)
                        {
                            Quaternion inward = Quaternion.LookRotation(-d, Vector3.up);
                            Vector3 facePlane = c + d * (CellSize * 0.5f - FaceDepth);
                            Vector3 kitScale = new Vector3(CellSize / 6f, WallHeight / 4.6f, 0.7f);   // the 6 x 4.6 m end walls to one 4 x 3.6 m cell side, 0.35 m deep
                            GameObject face = null;
                            if (room && rackFaces.Contains((x, y, s))) face = PlacePiece(kit, "NicheWall", faces, "NicheWall_" + sid, facePlane, inward, kitScale);
                            if (face != null)
                            {
                                // a candle cluster on a row-1 niche sill (authored at local (1.3, 1.31, -0.08)), lit toward the room
                                Vector3 sill = facePlane + inward * Vector3.Scale(kitScale, new Vector3(1.3f, 1.31f, -0.08f));
                                CandleCluster(dressing, mats, sill, "niche_" + sid, -d * 0.12f + new Vector3(0, 0.25f, 0)); result.Candles++;
                            }
                            else if (room && across) face = PlacePiece(kit, "EndWall", faces, "EndWall_" + sid, facePlane, inward, kitScale);
                            if (face != null)   // the B5 end walls are arch-topped (the 6 m vault's profile): a limewash backing fills the spandrels to 3.6 m
                                Box(faces, "Backing_" + sid, c + d * (CellSize * 0.5f - 0.05f) + new Vector3(0, WallHeight * 0.5f, 0), Size(alongEdge, WallHeight, 0.1f, ns), mats.Limewash, 2f, false);
                            else
                                Box(faces, "Face_" + sid, faceMid + new Vector3(0, WallHeight * 0.5f, 0), Size(alongEdge, WallHeight, FaceDepth, ns),
                                    room && across ? mats.Limewash : mats.Wall, 2f, false);
                            result.Faces++;
                        }

                        // above it: a tympanum where the vault's end meets the side, a cornice along the side walls of a room
                        if (across || bay == Bay.Junction || (!room && e == Edge.Door))
                        {
                            float top = crown + (room ? 0.2f : 0.6f), h = top - WallHeight;
                            Box(ceiling, "Tympanum_" + sid, faceMid + new Vector3(0, WallHeight + h * 0.5f, 0), Size(alongEdge, h, FaceDepth, ns), room ? mats.Limewash : mats.Wall, 2f, false);
                        }
                        else if (room)
                            Box(ceiling, "Cornice_" + sid, c + d * (CellSize * 0.5f - FaceDepth - 0.07f) + new Vector3(0, WallHeight - 0.05f, 0), Size(alongEdge, 0.28f, 0.3f, ns), mats.Ceiling, 2f, false);
                    }

                    // kit piers on pillar cells: three stacked B5 pillars reach the crown
                    if (room && HasPiece(kit, "Pillar") && layout.Rooms[grid.Room[x, y]].Room.CellAt(x - layout.Rooms[grid.Room[x, y]].X, y - layout.Rooms[grid.Room[x, y]].Y) == CellKind.Pillar)
                    {
                        const float pierH = 1.61f; float sy = (crown + 0.15f) / (3f * pierH);
                        for (int k = 0; k < 3; k++)
                            PlacePiece(kit, "Pillar", dressing, "Pier_" + id + "_" + k, c + new Vector3(0, k * pierH * sy, 0), Quaternion.identity, new Vector3(1.35f, sy, 1.35f));
                        result.Props++;
                    }
                }

            Sockets(layout, grid, kit, mats, ceiling, dressing, result);

            // a candle cluster on every chest (the chest marker is 0.8 m tall), and a warm fill per room
            foreach (FloorChest ch in layout.Chests)
            {
                CandleCluster(dressing, mats, CellCenter(ch.X, ch.Y) + new Vector3(0.3f, 0.8f, 0.1f), "chest_" + ch.X + "_" + ch.Y, new Vector3(0, 0.3f, 0)); result.Candles++;
            }
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                Vector3 a = CellCenter(p.X, p.Y), b = CellCenter(p.X + p.Width - 1, p.Y + p.Height - 1);
                float span = Mathf.Max(p.Width, p.Height) * CellSize;
                FillLight(dressing, "Fill_r" + r, (a + b) * 0.5f + new Vector3(0, 1.8f, 0), FillGold, 4f, span * 0.75f + 2f);
            }
            if (kit.Mote != null)
                foreach (Transform lamp in root.Find(LampGroup))
                    if (lamp.Find("Hook") != null) Motes(dressing, "Motes_" + lamp.name, lamp.position + new Vector3(0, 0.2f, 0), new Vector3(3f, 2.2f, 3f), 90, kit.Mote);
        }

        // ------------------------------------------------------------------------------------------------ socket dressing
        static void Sockets(CellarFloorLayout layout, Grid grid, CellarFloorKit kit, Mats mats, Transform ceiling, Transform dressing, CellarFloorBuild result)
        {
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r];
                for (int i = 0; i < p.Room.Sockets.Count; i++)
                {
                    DressingSocket s = p.Room.Sockets[i];
                    int x = p.X + s.X, y = p.Y + s.Y;
                    if (!Grid.In(x, y) || !grid.Occ[x, y]) continue;
                    Vector3 c = CellCenter(x, y), w = SideDir(CellarRoom.Opposite(s.Facing));   // the wall behind the socket
                    bool wallClosed = EdgeOf(grid, x, y, CellarRoom.Opposite(s.Facing)) == Edge.Closed;
                    string id = "r" + r + "_" + i;
                    Quaternion faceLeft = Quaternion.LookRotation(Vector3.Cross(-w, Vector3.up), Vector3.up);    // local +x points into the room (left-wall pieces)
                    Quaternion faceRight = Quaternion.LookRotation(Vector3.Cross(w, Vector3.up), Vector3.up);    // local +x points at the wall (right-wall pieces)
                    GameObject g = null;
                    switch (s.Kind)
                    {
                        case SocketKind.Shelf:   // left-wall piece, back at local x -2.5, z 0..2
                            if (wallClosed) g = PlacePiece(kit, "LarderShelf", dressing, "LarderShelf_" + id, c - w * 0.7f - faceLeft * new Vector3(0, 0, 1f), faceLeft, Vector3.one);
                            break;
                        case SocketKind.Chute:   // right-wall piece at local x +2.5
                            if (wallClosed) g = PlacePiece(kit, "CoalChute", dressing, "CoalChute_" + id, c - w * 0.7f, faceRight, Vector3.one);
                            break;
                        case SocketKind.Barrel:  // cradle axes across the wall (heads to the room), the standing barrel toward the room
                            g = PlacePiece(kit, "Barrels", dressing, "Barrels_" + id, c + w * 1.3f, faceLeft, Vector3.one);
                            if (g != null) { CandleCluster(dressing, mats, c + w * 1.3f - w * 0.95f + new Vector3(0, 1.0f, 0), "barrel_" + id, new Vector3(0, 0.3f, 0)); result.Candles++; }
                            break;
                        case SocketKind.Crate:
                        {
                            float yaw = ((x * 7 + y * 13) % 21) - 10;
                            Vector3 at = c + w * 1.25f;
                            var crate = new GameObject("Crates_" + id).transform; crate.SetParent(dressing, false); crate.position = at;
                            Box(crate, "CrateA", at + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, yaw, 0);
                            Box(crate, "CrateB", at + Vector3.Cross(w, Vector3.up) * 0.85f + new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, -yaw * 1.5f, 0);
                            Box(crate, "CrateC", at + new Vector3(0, 1.1f, 0), new Vector3(0.6f, 0.6f, 0.6f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, yaw + 25f, 0);
                            g = crate.gameObject;
                            break;
                        }
                        case SocketKind.Roots:   // hangs from the crown of the room bay (authored with its origin on a 4.1 m crown)
                            g = PlacePiece(kit, "Roots", ceiling, "Roots_" + id, c + w * 0.4f + new Vector3(0, RoomSpring + VaultRise - 0.05f, 0), Quaternion.Euler(0, (x * 37 + y * 11) % 360, 0), Vector3.one);
                            break;
                    }
                    if (g != null) result.Props++;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ classification
        static Bay BayOf(CellarFloorLayout layout, Grid grid, int x, int y)
        {
            if (grid.Room[x, y] >= 0) return Bay.Room;
            PlacedCorridor c = grid.Corr[x, y];
            int ns = CellarRoom.MaskBit(CellSide.North) | CellarRoom.MaskBit(CellSide.South), ew = CellarRoom.MaskBit(CellSide.East) | CellarRoom.MaskBit(CellSide.West);
            int m = c != null ? c.Mask : 0;
            return m == ns ? Bay.StraightNS : m == ew ? Bay.StraightEW : Bay.Junction;
        }

        // Barrels run along the room's longer side (north-south on a tie).
        static bool RoomAlongZ(PlacedRoom p) => p.Height >= p.Width;

        static Edge EdgeOf(Grid grid, int x, int y, CellSide s)
        {
            var (dx, dy) = CellarRoom.Step(s); int nx = x + dx, ny = y + dy;
            bool nOcc = grid.Occupied(nx, ny);
            bool open = grid.OpenFrom(x, y, s) && (!nOcc || grid.OpenFrom(nx, ny, CellarRoom.Opposite(s)));
            if (!open) return Edge.Closed;
            if (grid.Room[x, y] >= 0) return grid.Room[nx, ny] == grid.Room[x, y] ? Edge.Through : Edge.Door;
            return grid.Room[nx, ny] < 0 ? Edge.Through : Edge.Door;
        }

        // A rib on a straight corridor bay's end that opens into more corridor: always on the north/east end, on the south/west end only when the next cell is
        // not a straight bay of the same axis (that one draws it from its own north/east end).
        static bool RibAt(CellarFloorLayout layout, Grid grid, int x, int y, CellSide s, Bay bay)
        {
            if (s == CellSide.North || s == CellSide.East) return true;
            var (dx, dy) = CellarRoom.Step(s);
            return !grid.Occupied(x + dx, y + dy) || BayOf(layout, grid, x + dx, y + dy) != bay;
        }

        // ------------------------------------------------------------------------------------------------ helpers
        // A box size from a length along the edge, a height and a depth into the cell.
        static Vector3 Size(Vector3 alongEdge, float height, float depth, bool ns) => ns ? new Vector3(alongEdge.x, height, depth) : new Vector3(depth, height, alongEdge.z);

        static GameObject PlacePiece(CellarFloorKit kit, string piece, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (!HasPiece(kit, piece)) return null;
            GameObject go = Object.Instantiate(kit.Pieces[piece], parent, false);
            go.name = name; go.SetActive(true);
            go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = scale;
            return go;
        }

        static GameObject Shape(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, LookTestGeometry.Shape shape, Vector3 size, Material mat, int segments)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = scale;
            go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<LookTestGeometry>().Configure(shape, size, 2f, segments, false);
            return go;
        }

        // Three candles standing at c (the foot of the tallest) and one warm unshadowed point light at c + lightOffset (the dungeon corner's cluster).
        static void CandleCluster(Transform parent, Mats mats, Vector3 c, string tag, Vector3 lightOffset)
        {
            var t = new GameObject("Candles_" + tag).transform; t.SetParent(parent, false); t.position = c;
            var offs = new[] { new Vector3(0, 0, 0), new Vector3(0.14f, 0, 0.06f), new Vector3(-0.12f, 0, 0.08f) };
            var hs = new[] { 0.16f, 0.1f, 0.07f };
            for (int i = 0; i < 3; i++)
            {
                Prim(t, "Candle" + i, PrimitiveType.Cylinder, c + offs[i] + new Vector3(0, hs[i] * 0.5f, 0), new Vector3(0.035f, hs[i] * 0.5f, 0.035f), mats.Wax);
                Prim(t, "Flame" + i, PrimitiveType.Sphere, c + offs[i] + new Vector3(0, hs[i] + 0.02f, 0), new Vector3(0.015f, 0.03f, 0.015f), mats.Flame);
            }
            FillLight(t, "CandleLight", c + lightOffset, CandleGold, 1.6f, 3.8f);
        }

        static void FillLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos;
            Light l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }

        // Warm dust motes in the lantern's pool: a slow prewarmed box emitter (the dungeon corner's settings).
        static void Motes(Transform parent, string name, Vector3 pos, Vector3 box, int count, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 8f; main.startSpeed = 0.02f; main.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.022f); main.maxParticles = count;
            main.startColor = new Color(1.0f, 0.78f, 0.45f, 1.0f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = count / 8f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f); vel.y = new ParticleSystem.MinMaxCurve(-0.01f, 0.04f); vel.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            var rd = go.GetComponent<ParticleSystemRenderer>(); rd.sharedMaterial = mat; rd.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
