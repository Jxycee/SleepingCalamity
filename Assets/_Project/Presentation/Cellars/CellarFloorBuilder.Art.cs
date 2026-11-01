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
    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // COLOUR TUNING BLOCK (floor art round 2). Round 0 measured the floor at 48-58% mid-tones against the dungeon corner's 38%, fewer deep darks and more saturated
    // lamp pools (light saturation .57 vs .42-.53): too much flat orange fill. Everything below pulls it toward DungeonCornerBuilder (deep grey-brown darks, paler gold pools).
    // Round 2: ambient 0.35 -> 0.28 | fill 1.8 -> 1.0 | lamp x1.2 -> x1.0, corridor lamps a further x0.8 | lamp gold (1.0,0.85,0.66).
    // Round 1, old -> new:  ambient 0.8 -> 0.35 | per-room fill 4.0 -> 1.8 and (1.0,0.76,0.52) -> (1.0,0.86,0.70) | lamp gold (1.0,0.80,0.54) -> (1.0,0.82,0.58), lamp intensity x1.2
    // (wall 18 -> 21.6, centre 18 -> 21.6, corridor 11 -> 13.2) | candle gold stays the corner's (1.0,0.72,0.44) | fog warm offset x0.5, each channel held within 0.03 of the base.
    // The volume stays DungeonCorner_Profile with no extra warm grade. Applies to every floor built by CellarFloorBuilder (lamps, fills, candles, fog) and, for ambient,
    // to whichever scene builder sets RenderSettings (CellarFloorTestBuilder today; a real dive scene should read CellarArtTuning.Ambient too).
    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    public static class CellarArtTuning
    {
        public const float Ambient = 0.28f;                         // trilight ambient intensity (was 0.8, round 1 0.35; the corner uses 0.23)
        public const float FillIntensity = 1.0f;                    // per-room fill light (was 4.0, round 1 1.8)
        public const float LampBoost = 1.0f, CorridorLampScale = 0.8f, CorridorLampRangeScale = 0.7f, CentreLampScale = 1.15f;   // wall, centre and corridor lamp intensity multiplier (round 1 1.2), corridor lamps a further x0.8
        public const float FogWarmScale = 0.5f, FogWarmClamp = 0.03f;   // the per-floor fog warm offset: halved, then within this of the base colour per channel
        public static readonly Color FillGold = new Color(1.0f, 0.86f, 0.70f);
        public static readonly Color LampGold = new Color(1.0f, 0.85f, 0.66f);
        public static readonly Color CandleGold = new Color(1.0f, 0.72f, 0.44f);

        // Fog colour for a floor: the base (the corner's ~(0.16, 0.13, 0.11)) plus the floor's warm offset, halved and clamped so it stays grey-brown.
        public static Color FogColor(Color baseColor, float dr, float dg, float db)
        {
            float C(float b, float d) => Mathf.Clamp01(b + Mathf.Clamp(d * FogWarmScale, -FogWarmClamp, FogWarmClamp));
            return new Color(C(baseColor.r, dr), C(baseColor.g, dg), C(baseColor.b, db));
        }
    }

    public static partial class CellarFloorBuilder
    {
        public const string VaultGroup = "Vaults", CeilingGroup = "Ceiling", FaceGroup = "Faces", DressingGroup = "Dressing", ClutterGroup = "Clutter";
        public const float RoomSpring = 2.6f, CorridorSpring = 1.6f, VaultHalfSpan = 2.0f, VaultRise = 2.5f;
        public const float WallLampIntensity = 18f * CellarArtTuning.LampBoost, CentreLampIntensity = 18f * CellarArtTuning.LampBoost, CorridorLampIntensity = 11f * CellarArtTuning.LampBoost * CellarArtTuning.CorridorLampScale;
        const float FaceDepth = 0.2f, KitAcross = 0.725f;   // VaultSegment / ArchRib (5 m inside, 6 m outside): 0.725 keeps the outer wall face 2.175 m from the axis, just behind a neighbour's 0.2 m face, inner face 1.83 m (on the wall colliders)
        static readonly Color CandleGold = CellarArtTuning.CandleGold, FillGold = CellarArtTuning.FillGold;
        static readonly CellSide[] Sides = { CellSide.North, CellSide.East, CellSide.South, CellSide.West };

        enum Edge { Closed, Door, Through }
        enum Bay { Room, StraightNS, StraightEW, Junction }

        static bool HasPiece(CellarFloorKit kit, string name) => kit != null && kit.Pieces != null && kit.Pieces.TryGetValue(name, out GameObject g) && g != null;

        static void Dress(CellarFloorLayout layout, CellarFloorDressing resolved, Grid grid, Transform root, CellarFloorKit kit, Mats mats, CellarFloorBuild result)
        {
            Transform vaults = Group(root, VaultGroup), ceiling = Group(root, CeilingGroup), faces = Group(root, FaceGroup), dressing = Group(root, DressingGroup);

            // racks claim their wall face (the niche wall replaces the plain end wall or brick)
            var rackFaces = new HashSet<(int, int, CellSide)>();
            var rackEmpty = new HashSet<(int, int, CellSide)>();
            foreach (DressedSocket s in resolved.Sockets)
                if (s.Filled && s.Kind == SocketKind.Rack)
                {
                    rackFaces.Add((s.Cell.X, s.Cell.Y, CellarRoom.Opposite(s.Facing)));
                    if (s.Variant == "Empty") rackEmpty.Add((s.Cell.X, s.Cell.Y, CellarRoom.Opposite(s.Facing)));
                }

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
                                if (!rackEmpty.Contains((x, y, s))) { CandleCluster(dressing, mats, sill, "niche_" + sid, -d * 0.12f + new Vector3(0, 0.25f, 0)); result.Candles++; }   // an empty rack keeps its niche but not its candle
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

            CellarFloorClutter clutter = CellarFloorClutter.Resolve(layout, resolved, CellarRoomChunks.Resolve(layout, resolved));   // clutter yields to chunk solid cells
            result.Clutter = clutter;
            Sockets(resolved, grid, kit, mats, ceiling, dressing, result);
            Corners(layout, resolved, grid, kit, mats, dressing, result, clutter);
            Clutter(clutter, kit, mats, dressing);

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
                FillLight(dressing, "Fill_r" + r, (a + b) * 0.5f + new Vector3(0, 1.8f, 0), FillGold, CellarArtTuning.FillIntensity, span * 0.75f + 2f);
            }
            if (kit.Mote != null)
                foreach (Transform lamp in root.Find(LampGroup))
                    if (lamp.Find("Hook") != null) Motes(dressing, "Motes_" + lamp.name, lamp.position + new Vector3(0, 0.2f, 0), new Vector3(3f, 2.2f, 3f), 90, kit.Mote);
        }

        // ------------------------------------------------------------------------------------------------ socket dressing
        // Walks the resolved dressing (Core CellarFloorDressing): empty sockets draw nothing, filled ones stand at their chosen cell and the chosen variant picks the
        // look (Barrels: full, a second barrel stacked on top, or the cradle turned the other way; Crates: stacked, open (two crates) or broken (one tipped); Shelf:
        // jars, hams or a sack at the foot; Chute: clear or a heap of coal; Roots: thin or thick). Rack faces are handled with the wall faces (a filled Rack).
        static void Sockets(CellarFloorDressing resolved, Grid grid, CellarFloorKit kit, Mats mats, Transform ceiling, Transform dressing, CellarFloorBuild result)
        {
            foreach (DressedSocket s in resolved.Sockets)
            {
                if (!s.Filled || s.Kind == SocketKind.Lantern) continue;
                int x = s.Cell.X, y = s.Cell.Y, r = s.RoomIndex;
                if (!Grid.In(x, y) || !grid.Occ[x, y]) continue;
                Vector3 c = CellCenter(x, y), w = SideDir(CellarRoom.Opposite(s.Facing));   // the wall behind the socket
                bool wallClosed = EdgeOf(grid, x, y, CellarRoom.Opposite(s.Facing)) == Edge.Closed;
                string id = "r" + r + "_" + s.Socket.Id + "_" + s.Variant;
                Quaternion faceLeft = Quaternion.LookRotation(Vector3.Cross(-w, Vector3.up), Vector3.up);    // local +x points into the room (left-wall pieces)
                Quaternion faceRight = Quaternion.LookRotation(Vector3.Cross(w, Vector3.up), Vector3.up);    // local +x points at the wall (right-wall pieces)
                GameObject g = null;
                switch (s.Kind)
                {
                    case SocketKind.Shelf:   // left-wall piece, back at local x -2.5, z 0..2
                        if (wallClosed)
                        {
                            g = PlacePiece(kit, "LarderShelf", dressing, "LarderShelf_" + id, c - w * 0.7f - faceLeft * new Vector3(0, 0, 1f), faceLeft, Vector3.one);
                            if (g != null && s.Variant == "Sacks")
                                Prim(g.transform, "Sack", PrimitiveType.Sphere, c - w * 0.2f + Vector3.Cross(w, Vector3.up) * 0.9f + new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.6f, 0.6f), mats.Limewash);
                        }
                        break;
                    case SocketKind.Chute:   // right-wall piece at local x +2.5
                        if (wallClosed)
                        {
                            g = PlacePiece(kit, "CoalChute", dressing, "CoalChute_" + id, c - w * 0.7f, faceRight, Vector3.one);
                            if (g != null && s.Variant == "Heaped") Prim(g.transform, "Coal", PrimitiveType.Sphere, c + w * 0.9f + new Vector3(0, 0.25f, 0), new Vector3(1.4f, 0.6f, 1.4f), mats.Iron);
                        }
                        break;
                    case SocketKind.Barrel:  // cradle axes across the wall (heads to the room), the standing barrel toward the room
                    {
                        Quaternion rot = s.Variant == "Tipped" ? Quaternion.AngleAxis(180f, Vector3.up) * faceLeft : faceLeft;   // tipped: the cradle turned the other way
                        g = PlacePiece(kit, "Barrels", dressing, "Barrels_" + id, c + w * 1.3f, rot, Vector3.one);
                        if (g != null)
                        {
                            if (s.Variant == "Stacked") Prim(g.transform, "BarrelTop", PrimitiveType.Cylinder, c + w * 0.4f + new Vector3(0, 0.55f, 0), new Vector3(0.9f, 0.5f, 0.9f), mats.Trim);
                            CandleCluster(dressing, mats, c + w * 1.3f - w * 0.95f + new Vector3(0, 1.0f, 0), "barrel_" + id, new Vector3(0, 0.3f, 0)); result.Candles++;
                        }
                        break;
                    }
                    case SocketKind.Crate:
                    {
                        float yaw = ((x * 7 + y * 13) % 21) - 10;
                        Vector3 at = c + w * 1.25f;
                        if (HasPiece(kit, "CrateStack") && HasPiece(kit, "Crate"))
                        {
                            // kit crates against the wall (local +x into the room, wall at -x): Stacked = the big stack, Open = two crates, Broken = a crate and one tipped over
                            Vector3 wallPos = c + w * (CellSize * 0.5f - 0.2f), side = Vector3.Cross(w, Vector3.up);
                            var kc = new GameObject("Crates_" + id).transform; kc.SetParent(dressing, false); kc.position = wallPos;
                            if (s.Variant == "Open") { PlaceAtWall(kit, "Crate", kc, "CrateA", wallPos - w * 0.36f + side * 0.5f, faceLeft, yaw); PlaceAtWall(kit, "Crate", kc, "CrateB", wallPos - w * 0.36f - side * 0.5f, faceLeft, -yaw * 1.5f); }
                            else if (s.Variant == "Broken")
                            {
                                PlaceAtWall(kit, "Crate", kc, "CrateA", wallPos - w * 0.36f + side * 0.55f, faceLeft, yaw);
                                GameObject tip = PlaceAtWall(kit, "Crate", kc, "CrateB", wallPos - w * 0.36f - side * 0.55f + new Vector3(0, 0.3f, 0), faceLeft, -yaw * 1.5f);
                                if (tip != null) tip.transform.rotation = tip.transform.rotation * Quaternion.Euler(0, 0, 35f);
                            }
                            else PlaceAtWall(kit, "CrateStack", kc, "CrateStack", wallPos - w * 0.405f, faceLeft, yaw * 0.5f);
                            g = kc.gameObject;
                            break;
                        }
                        var crate = new GameObject("Crates_" + id).transform; crate.SetParent(dressing, false); crate.position = at;
                        Box(crate, "CrateA", at + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, yaw, 0);
                        GameObject cb = Box(crate, "CrateB", at + Vector3.Cross(w, Vector3.up) * 0.85f + new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), mats.Crate, 1f, false);
                        cb.transform.rotation = s.Variant == "Broken" ? Quaternion.Euler(0, -yaw * 1.5f, 35f) : Quaternion.Euler(0, -yaw * 1.5f, 0);
                        if (s.Variant != "Open")
                            Box(crate, "CrateC", at + new Vector3(0, 1.1f, 0), new Vector3(0.6f, 0.6f, 0.6f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, yaw + 25f, 0);
                        g = crate.gameObject;
                        break;
                    }
                    case SocketKind.Roots:   // hangs from the crown of the room bay (authored with its origin on a 4.1 m crown)
                        g = PlacePiece(kit, "Roots", ceiling, "Roots_" + id, c + w * 0.4f + new Vector3(0, RoomSpring + VaultRise - 0.05f, 0), Quaternion.Euler(0, (x * 37 + y * 11) % 360, 0),
                            s.Variant == "Thick" ? new Vector3(1.3f, 1f, 1.3f) : Vector3.one);
                        break;
                }
                if (g != null) result.Props++;
            }
        }

        // ------------------------------------------------------------------------------------------------ corner dressing
        // Up to four (every one that passes) free inside corners per room (a floor cell closed on two adjacent sides, holding no chest, start, stair, spawn, carrier or socket) get
        // barrels with a candle or a crate stack, picked by a hash of (seed, cell), so sparse combat rooms gain storage at their edges without new data.
        static void Corners(CellarFloorLayout layout, CellarFloorDressing resolved, Grid grid, CellarFloorKit kit, Mats mats, Transform dressing, CellarFloorBuild result, CellarFloorClutter clutter)
        {
            var busy = new HashSet<(int, int)>();
            foreach (CellarClutterRow cr in clutter.Rows) busy.Add((cr.CellX, cr.CellY));   // a clutter cell keeps its own pieces
            foreach (FloorChest ch in layout.Chests) busy.Add((ch.X, ch.Y));
            foreach (CellPos s in layout.EntryStarts) busy.Add((s.X, s.Y));
            if (layout.StairExit.HasValue) busy.Add((layout.StairExit.Value.X, layout.StairExit.Value.Y));
            if (layout.Carrier != null) busy.Add((layout.Carrier.X, layout.Carrier.Y));
            foreach (PlacedRoom p in layout.Rooms)
            {
                foreach (CellarSpawn sp in p.Room.Spawns) busy.Add((p.X + sp.X, p.Y + sp.Y));
            }
            foreach (DressedSocket so in resolved.Sockets) if (so.Filled) busy.Add((so.Cell.X, so.Cell.Y));   // the chosen cells, empties leave their cell free
            foreach (PlacedVignette pv in resolved.Vignettes) busy.Add((pv.Cell.X, pv.Cell.Y));
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                PlacedRoom p = layout.Rooms[r]; int placed = 0;
                for (int ly = 0; ly < p.Height && placed < 4; ly++)
                    for (int lx = 0; lx < p.Width && placed < 4; lx++)
                    {
                        int x = p.X + lx, y = p.Y + ly;
                        if (p.Room.CellAt(lx, ly) != CellKind.Floor || busy.Contains((x, y))) continue;
                        for (int k = 0; k < 4; k++)
                        {
                            CellSide a = Sides[k], b = Sides[(k + 1) % 4];
                            if (EdgeOf(grid, x, y, a) != Edge.Closed || EdgeOf(grid, x, y, b) != Edge.Closed) continue;
                            Vector3 c = CellCenter(x, y), da = SideDir(a), db = SideDir(b);
                            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ layout.Seed * 83492791u;
                            string id = "r" + r + "_" + x + "_" + y;
                            if ((h & 1u) == 0 && HasPiece(kit, "Barrels"))
                            {
                                // the cradle along wall a, heads toward the room
                                PlacePiece(kit, "Barrels", dressing, "CornerBarrels_" + id, c + da * 1.3f + db * 0.6f, Quaternion.LookRotation(Vector3.Cross(-da, Vector3.up), Vector3.up), Vector3.one);
                                CandleCluster(dressing, mats, c + da * 1.3f + db * 0.6f - da * 0.95f + new Vector3(0, 1.0f, 0), "corner_" + id, new Vector3(0, 0.3f, 0)); result.Candles++;
                            }
                            else
                            {
                                Vector3 at = c + (da + db) * 1.2f; float yaw = (h >> 3) % 30;
                                if (HasPiece(kit, "CrateStack") && HasPiece(kit, "Crate"))
                                {
                                    // a stack tucked into the corner along wall a (1.49 m long, pushed 0.95 m toward wall b) and a single crate beside it
                                    Vector3 wp = c + da * (CellSize * 0.5f - 0.2f);
                                    var kc = new GameObject("CornerCrates_" + id).transform; kc.SetParent(dressing, false); kc.position = wp;
                                    Quaternion fl = Quaternion.LookRotation(Vector3.Cross(-da, Vector3.up), Vector3.up);
                                    PlaceAtWall(kit, "CrateStack", kc, "CrateStack", wp - da * 0.405f + db * 0.95f, fl, yaw * 0.2f - 3f);
                                    PlaceAtWall(kit, "Crate", kc, "Crate", wp - da * 0.36f - db * 0.4f, fl, -yaw * 0.3f);
                                    result.Props++; placed++;
                                    break;
                                }
                                var crate = new GameObject("CornerCrates_" + id).transform; crate.SetParent(dressing, false); crate.position = at;
                                Box(crate, "CrateA", at + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, yaw, 0);
                                Box(crate, "CrateB", at - da * 0.9f + new Vector3(0, 0.35f, 0), new Vector3(0.7f, 0.7f, 0.7f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, -yaw, 0);
                                Box(crate, "CrateC", at + new Vector3(0, 1.1f, 0), new Vector3(0.6f, 0.6f, 0.6f), mats.Crate, 1f, false).transform.rotation = Quaternion.Euler(0, yaw + 20f, 0);
                            }
                            result.Props++; placed++;
                            break;
                        }
                    }
            }
        }

        // ------------------------------------------------------------------------------------------------ wall clutter
        // Core CellarFloorClutter rows (whole centimetres in this builder's frame; yaw clockwise from above, the piece's front into the room) become kit pieces under
        // Dressing/Clutter. The Blender pieces stand with the pivot bottom centre, front +X, wall side -X, +Z along the wall, so a row turns by yaw - 90 degrees.
        // A missing kit piece falls back to a primitive of the row's footprint in the kit's materials. Pure function of (seed, floor): no Random, no Time.
        static void Clutter(CellarFloorClutter clutter, CellarFloorKit kit, Mats mats, Transform dressing)
        {
            if (clutter.Rows.Count == 0) return;
            Transform group = Group(dressing, ClutterGroup);
            foreach (CellarClutterRow r in clutter.Rows)
            {
                Vector3 pos = new Vector3(r.X, 0f, r.Z); Quaternion rot = Quaternion.Euler(0f, r.Yaw - 90f, 0f);
                GameObject go = PlacePiece(kit, r.Piece, group, r.Piece + "_" + r.Id, pos, rot, Vector3.one);
                if (go == null)
                {
                    Material m = r.Piece == CellarFloorClutter.Rubble ? mats.Wall : r.Piece == CellarFloorClutter.SackPile ? mats.Limewash : r.Piece == CellarFloorClutter.JarCluster ? mats.Trim : mats.Crate;
                    go = Box(group, r.Piece + "_" + r.Id, pos + new Vector3(0, r.Height * 0.5f, 0), new Vector3(r.Depth, r.Height, r.Width), m, 1f, false);   // x = off the wall, z = along it
                    go.transform.rotation = rot;
                }
            }
        }

        // The one hook the main builder calls after its own pass (lamps, atmosphere): recolours every lamp light to CellarArtTuning.LampGold and sets the fog colour from the
        // tuning block (the base plus the floor's warm offset, halved and clamped), so the colour tuning lives in this file only.
        static void ArtFinish(Transform root, CellarFloorKit kit, CellarFloorBuild result)
        {
            Transform lamps = root != null ? root.Find(LampGroup) : null;
            if (lamps != null)
                foreach (Light l in lamps.GetComponentsInChildren<Light>(true))
                {
                    l.color = CellarArtTuning.LampGold;
                    string owner = l.transform.parent != null ? l.transform.parent.name : "";
                    if (owner.StartsWith("Lamp_c")) l.range *= CellarArtTuning.CorridorLampRangeScale;                 // corridor pools fall off into darker walls between lamps
                    else if (owner.StartsWith("Lamp_r") && owner.EndsWith("_c")) l.intensity *= CellarArtTuning.CentreLampScale;   // the room's hanging centre lamp
                }
            if (kit != null && kit.FogBaseDensity > 0f && result.Dressing != null)
            {
                result.Dressing.Atmosphere.FogTintOffset(out float fr, out float fg, out float fb);
                RenderSettings.fogColor = CellarArtTuning.FogColor(kit.FogBaseColor, fr, fg, fb);
                HasLastFog = true; LastFogDensity = RenderSettings.fogDensity; LastFogColor = RenderSettings.fogColor;   // the town area look re-applies these when the viewer is in the cellar
            }
        }

        // M6 S3: the fog of the last floor built in this process. The build writes the global RenderSettings fog at once, even while the viewer is still in the town above, so
        // AreaLook (Presentation/Town) keeps its own copy of each area's fog and applies the cellar's from here.
        public static bool HasLastFog { get; private set; }
        public static float LastFogDensity { get; private set; }
        public static Color LastFogColor { get; private set; }

        // A kit piece standing on the floor at pos, turned by the wall-facing rotation plus a yaw jitter (degrees); null when the piece is missing.
        static GameObject PlaceAtWall(CellarFloorKit kit, string piece, Transform parent, string name, Vector3 pos, Quaternion facing, float yawJitter)
            => PlacePiece(kit, piece, parent, name, new Vector3(pos.x, 0f, pos.z), facing * Quaternion.Euler(0f, yawJitter, 0f), Vector3.one);

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
