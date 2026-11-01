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
                Prim(root, "Pillar", PrimitiveType.Cylinder, root.position + new Vector3(0, 1.5f, 0), new Vector3(0.5f, 1.5f, 0.5f), mats.Flat(c));
                Prim(root, "Banner", PrimitiveType.Cube, root.position + new Vector3(0, 2.6f, 0), new Vector3(1.2f, 0.6f, 0.05f), mats.Flat(c));
                result.SpecialMarkers++;
            }
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
    }
}
