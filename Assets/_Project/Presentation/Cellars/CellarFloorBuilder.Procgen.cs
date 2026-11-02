// Presentation/Cellars/CellarFloorBuilder.Procgen.cs: the procgen track's greybox layer (Reference/cellars/procgen/STATUS.md), kept in its own partial so the spine
// (CellarFloorBuilder.Dive.cs) and contents (CellarFloorBuilder.Contents.cs) work never shares a file with it. Build() calls Procgen() once, after the vignettes.
// Authority: none (built locally from (seed, floor) on every peer, like the rest of the floor).
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed partial class CellarFloorBuild
    {
        public SpecialRoom Special;             // procgen item 1c: the floor's special room tag (greybox marker built when it exists)
        public CellarDoorHints DoorHints;       // procgen item 1b: the door reward hints (an icon per hinted door)
        public int SpecialMarkers, DoorHintIcons;
        public CellarRoomChunks Chunks;         // procgen item 4: the resolved chunk regions (blocking / cover cells drawn as primitives in the Chunks subgroup)
        public int ChunkPieces;
        public CellarFloorTrim Trim;            // procgen item 6b: wall-mounted and overhead dressing (greybox primitives in the Trim subgroup)
        public CellarFloorWarmLights WarmLights; // procgen item 6c: secondary warm lights, placed only for the rows the resolver returned (room light cap)
        public int TrimPieces, WarmLightCount;
        public CellarFloorClutter Clutter;      // the floor clutter the art layer resolved (procgen item 6 reuses it instead of resolving again)
    }

    public static partial class CellarFloorBuilder
    {
        public const string ProcgenGroup = "Procgen";

        // ------------------------------------------------------------------------------------------------ procgen (greybox)
        // Procgen item 1: a coloured pillar with a banner and a name in the floor's special room ("SpecialRoom_<Kind>_r<room>"), and a small icon above every door whose far room has
        // something worth a hint ("DoorHint_r<room>_d<door>"; colour by the strongest flag). Primitives only; the art pass swaps real markers in.
        static void Procgen(CellarFloorLayout layout, CellarFloorDressing dressing, Transform group, Mats mats, CellarFloorBuild result)
        {
            SpecialRoom special = SpecialRoomDive.Resolve(layout, null, dressing); result.Special = special;
            CellarDoorHints hints = CellarDoorHints.Resolve(layout, special, dressing); result.DoorHints = hints;
            if (special.Exists)
            {
                PlacedRoom p = layout.Rooms[special.RoomIndex];
                Color c = special.Kind == SpecialKind.EliteDen ? new Color(0.75f, 0.12f, 0.12f) : special.Kind == SpecialKind.Treasury ? new Color(0.95f, 0.78f, 0.15f)
                    : special.Kind == SpecialKind.Shrine ? new Color(0.35f, 0.55f, 0.95f) : new Color(0.60f, 0.85f, 0.65f);
                var root = new GameObject("SpecialRoom_" + special.Kind + "_r" + special.RoomIndex).transform; root.SetParent(group, false);
                root.position = CellCenter(p.X + p.Width / 2, p.Y + p.Height / 2);
                Prim(root, "Pillar", PrimitiveType.Cylinder, root.position + new Vector3(0, 1.5f, 0), new Vector3(0.5f, 1.5f, 0.5f), mats.Flat(c));   // greybox marker at the room centre: no collider (a 0.5 m post in line with a door narrows it to 0.5 m)
                Prim(root, "Banner", PrimitiveType.Cube, root.position + new Vector3(0, 2.6f, 0), new Vector3(1.2f, 0.6f, 0.05f), mats.Flat(c));
                result.SpecialMarkers++;
            }
            ProcgenChunks(layout, dressing, group, mats, result);
            ProcgenTrim(layout, dressing, group, mats, result);
            foreach (DoorHint h in hints.Hints)
            {
                if (!h.Notable) continue;   // chests-only doors get no icon
                PlacedRoom p = layout.Rooms[h.RoomIndex]; CellPos cell = p.DoorCell(h.DoorIndex);
                Vector3 mid = CellCenter(cell.X, cell.Y) + SideDir(p.Room.Doors[h.DoorIndex].Side) * (CellSize * 0.5f);
                Color c = h.Has(DoorHintFlags.Key) ? new Color(1.0f, 0.78f, 0.15f) : h.Has(DoorHintFlags.Special) ? new Color(0.85f, 0.25f, 0.85f)
                    : h.Has(DoorHintFlags.Exit) ? new Color(0.55f, 0.30f, 0.70f) : h.Has(DoorHintFlags.Checkpoint) ? new Color(0.30f, 0.85f, 0.90f)
                    : h.Has(DoorHintFlags.RewardVignette) ? new Color(0.25f, 0.75f, 0.55f) : new Color(0.55f, 0.35f, 0.15f);
                Prim(group, "DoorHint_r" + h.RoomIndex + "_d" + h.DoorIndex, PrimitiveType.Sphere, mid + new Vector3(0, DoorHeight + 0.35f, 0), new Vector3(0.3f, 0.3f, 0.3f), mats.Flat(c));
                result.DoorHintIcons++;
            }
        }

        // Procgen item 6: wall pieces (Plaque, Hook, WallShelf: a flat box on the wall face, at least 1 m up) and overhead pieces (Beam, Hanging: from the crown, above head height) in a
        // "Trim" subgroup named "Trim_<id>_<kind>", and a small warm light (cube sconce + point light) for every row of CellarFloorWarmLights, "WarmLight_<id>". Primitives only.
        static void ProcgenTrim(CellarFloorLayout layout, CellarFloorDressing dressing, Transform group, Mats mats, CellarFloorBuild result)
        {
            CellarFloorClutter clutter = result.Clutter ?? CellarFloorClutter.Resolve(layout, dressing, result.Chunks);
            CellarFloorTrim trim = CellarFloorTrim.Resolve(layout, dressing, result.Chunks); result.Trim = trim;
            CellarFloorWarmLights lights = CellarFloorWarmLights.Resolve(layout, dressing, result.Chunks, clutter, trim); result.WarmLights = lights;
            if (trim.Rows.Count == 0 && lights.Rows.Count == 0) return;
            var sub = new GameObject("Trim").transform; sub.SetParent(group, false);
            foreach (TrimRow t in trim.Rows)
            {
                Vector3 c = CellCenter(t.CellX, t.CellY); float lo = t.BaseCm / 100f, hi = t.TopCm / 100f, depth = t.DepthCm / 100f;
                Color col = t.Layer == TrimLayer.Wall ? new Color(0.45f, 0.34f, 0.22f) : new Color(0.30f, 0.24f, 0.18f);
                if (t.Layer == TrimLayer.Wall)
                {
                    Vector3 w = SideDir(t.Wall); bool ew = w.x != 0;
                    Vector3 pos = c + w * (CellSize * 0.5f - WallThickness * 0.5f - depth * 0.5f) + new Vector3(0, (lo + hi) * 0.5f, 0);
                    Prim(sub, "Trim_" + t.Id + "_" + t.Kind, PrimitiveType.Cube, pos, ew ? new Vector3(depth, hi - lo, 0.6f) : new Vector3(0.6f, hi - lo, depth), mats.Flat(col));
                }
                else
                {
                    Vector3 pos = c + new Vector3(0, (lo + hi) * 0.5f, 0);
                    Prim(sub, "Trim_" + t.Id + "_" + t.Kind, PrimitiveType.Cube, pos, t.Kind == TrimKind.Beam ? new Vector3(CellSize * 0.9f, hi - lo, depth) : new Vector3(depth, hi - lo, depth), mats.Flat(col));
                }
                result.TrimPieces++;
            }
            int[] real = RoomLightCounts(group.parent, layout); if (dressing.Atmosphere.OddLightRoom >= 0) real[dressing.Atmosphere.OddLightRoom]++;   // the odd light is built after this pass
            var perRoom = new System.Collections.Generic.Dictionary<int, int>();
            foreach (WarmLightRow l in lights.Rows)
            {
                perRoom.TryGetValue(l.RoomIndex, out int placed);
                if (real[l.RoomIndex] + placed >= CellarFloorWarmLights.RoomLightCap) continue;   // never past the room's light cap: the builder's real light count decides (the Core ExistingLights only plans)
                perRoom[l.RoomIndex] = placed + 1;
                Vector3 w = SideDir(l.Wall);
                Vector3 pos = CellCenter(l.CellX, l.CellY) + w * (CellSize * 0.5f - WallThickness * 0.5f - 0.1f) + new Vector3(0, l.HeightCm / 100f, 0);
                var go = new GameObject("WarmLight_" + l.Id + "_" + l.Variant).transform; go.SetParent(sub, false); go.position = pos;
                Prim(go, "Sconce", PrimitiveType.Cube, pos, new Vector3(0.12f, 0.16f, 0.12f), mats.Flat(new Color(0.55f, 0.40f, 0.20f)));
                FillLight(go, "Light", pos - w * 0.25f + new Vector3(0, 0.1f, 0), CandleGold, 1.2f, 3.2f);
                result.WarmLightCount++;
            }
        }

        // Lights under a built floor root per room (a light belongs to the room whose cell rectangle holds its position on the ground plane; corridor lamps belong to none).
        public static int[] RoomLightCounts(Transform root, CellarFloorLayout layout)
        {
            var n = new int[layout.Rooms.Count];
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                Vector3 q = l.transform.position;
                for (int r = 0; r < layout.Rooms.Count; r++)
                {
                    PlacedRoom p = layout.Rooms[r];
                    if (q.x >= p.X * CellSize && q.x <= (p.X + p.Width) * CellSize && q.z <= -p.Y * CellSize && q.z >= -(p.Y + p.Height) * CellSize) { n[r]++; break; }
                }
            }
            return n;
        }

        // Procgen item 4: every blocking cell of a resolved chunk is a full-height block, every cover cell a low block, in a "Chunks" subgroup, named
        // "Chunk_r<room>_<region>_<variant>" (region = its 1-based number in the room; one object per cell, the cell column is appended).
        static void ProcgenChunks(CellarFloorLayout layout, CellarFloorDressing dressing, Transform group, Mats mats, CellarFloorBuild result)
        {
            CellarRoomChunks chunks = CellarRoomChunks.Resolve(layout, dressing); result.Chunks = chunks;
            if (chunks.Chunks.Count == 0) return;
            var sub = new GameObject("Chunks").transform; sub.SetParent(group, false);
            foreach (ResolvedChunk c in chunks.Chunks)
            {
                if (c.IsOpen) continue;
                string name = "Chunk_r" + c.RoomIndex + "_" + (c.RegionIndex + 1) + "_" + c.Variant.Name;
                foreach (var (cell, kind) in CellarRoomChunks.SolidCells(layout, c))
                {
                    bool block = kind == ChunkCell.Block; float h = block ? 1.6f : 0.55f;
                    Color col = block ? new Color(0.36f, 0.32f, 0.28f) : new Color(0.52f, 0.42f, 0.30f);
                    Vector3 pos = CellCenter(cell.X, cell.Y);
                    Prim(sub, name + "_" + cell.X + "x" + cell.Y, PrimitiveType.Cube, pos + new Vector3(0, h * 0.5f, 0), new Vector3(CellSize * 0.8f, h, CellSize * 0.8f), mats.Flat(col)).AddComponent<BoxCollider>();   // floor-standing greybox: solid
                    result.ChunkPieces++;
                }
            }
        }
    }
}
