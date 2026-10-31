// Core/Town/HollowmereClutter.cs: Q14 market and lane clutter for Hollowmere rings 1 and 2 (contract Reference/hollowmere/clutter/CONTRACT.md): handcarts, crate and
// sack stacks, rain barrels, flower boxes and the well on the ground, shop signs on the building faces and washing lines between neighbouring houses overhead.
// Pure C#, no engine references; the Editor builder (Builders/HollowmereClutter) places the rows, the EditMode tests read the same data.
// Frame as TownLayout: metres, +Z north, yaw in degrees clockwise from above (forward = (sin yaw, cos yaw)). A ground row's footprint is centred on (X, Z), SizeX along
// its own X, SizeZ along its own Z (the art's front is local +Z), turned by Yaw.
// Design rule: free-grid decoration never blocks the NPC path graph. Every ground row, inflated by Clearance, covers no path node and crosses no path edge (lot
// ring edges and doorstep links included), stands outside every lot (lot yards belong to the garden pieces: no clutter there), outside
// landmarks, the approved beauty corner, the lots' cut and fill walls, the keep terrace wall strip and every HollowmereDressing row of its preset, on ring 1 (the square +1 m) or ring 2 (beside a
// lane), on ground flat enough not to float it. Signs and washing lines are overhead: no ground footprint, but a sign must sit on the door-side face of the right
// rebuilt building with its board above head height, and a washing line must run wall to wall between two rebuilt houses without crossing a house, high enough
// that its lowest cloth clears the ground under it.
// The rows are derived (not typed): wishes per lot are searched outward from a preferred spot in fixed steps and the first spot that passes the same rules
// Validate applies is taken, so the result is deterministic and stays valid if the layout moves. Rebuilt is dense; Ruined keeps roughly a third as many ground
// rows, as the *_Broken variants at the same spots (some tilted), the Well_Ruined at the Well's spot, and no signs or washing lines (no standing walls).
// Authority: none (static layout data).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.Core
{
    public enum ClutterKind : byte { Handcart, CrateStack, SackStack, RainBarrel, FlowerBox, Well }

    public sealed class ClutterRow
    {
        public string Id, Asset, Lot; public ClutterKind Kind; public DecoPreset Preset; public bool Broken;
        public int Ring;               // 1 = on the square, 2 = beside a lane
        public float X, Z, Yaw, SizeX, SizeZ, Height;
        public float TiltX, TiltZ;     // euler degrees for a leaning or toppled prop (the builder sits the tilted art on the ground)
        public bool Tilted => TiltX != 0f || TiltZ != 0f;
    }

    // A shop sign: the pivot is the wall plate's centre on the building face, SignHeight above the ground there; the arm runs out along (sin Yaw, cos Yaw).
    public sealed class ClutterSign
    {
        public string Id, Asset, Trade, Lot; public DecoPreset Preset;
        public float X, Z, Yaw, PivotHeight;
    }

    // A washing line between two house walls: end positions and heights above the ground there, sag at the middle (a parabola, like the bunting).
    public sealed class WashingSpan
    {
        public string Id, LotA, LotB; public DecoPreset Preset; public bool Torn;
        public float AX, AZ, AH, BX, BZ, BH, Sag;
        public float Length => (float)Math.Sqrt((BX - AX) * (BX - AX) + (BZ - AZ) * (BZ - AZ));
    }

    public sealed class ClutterPlan
    {
        public readonly List<ClutterRow> Rows = new List<ClutterRow>();
        public readonly List<ClutterSign> Signs = new List<ClutterSign>();
        public readonly List<WashingSpan> Spans = new List<WashingSpan>();
    }

    public static class HollowmereClutter
    {
        public const float Clearance = HollowmereDressing.Clearance;   // gap kept between a footprint and any path node / edge
        public const float StallMargin = HollowmereDressing.StallMargin;
        public const float MaxSlope = 0.35f;            // corner ground heights of a footprint may differ by this much
        public const float LaneMargin = 4f;             // beside a lane: within half its width plus this
        public const float SignHeight = 3.0f, SignDrop = 0.75f, SignArm = 0.9f, MinSignBoard = 2.25f;
        public const float MinLineEnd = 2.8f, MinLineSpan = 3f, MaxLineSpan = 10f, ClothHang = 1.0f, MinClothClearance = 2.0f, LineSag = 0.4f;
        const float DressingGap = 0.1f, RowGap = 0.05f;
        // The keep terrace's south wall strip (HollowmereBuilder.BuildKeep: wall, buttresses and planters on KeepWallZ, stair feet): no ground clutter there.
        static readonly Footprint KeepStrip = new Footprint(Pt(0f, HollowmereLayout.KeepWallZ - 0.4f), 2f * (HollowmereLayout.KeepHalfWidth + 1f), 4f, 0f);
        const DecoPreset Rb = DecoPreset.Rebuilt, Ru = DecoPreset.Ruined;

        // ---------------------------------------------------------------------------------------------------- assets
        sealed class Spec
        {
            public string Asset, Broken; public ClutterKind Kind; public float X, Z, H, BX, BZ, BH;
            public Spec(string asset, string broken, ClutterKind kind, float x, float z, float h, float bx, float bz, float bh) { Asset = asset; Broken = broken; Kind = kind; X = x; Z = z; H = h; BX = bx; BZ = bz; BH = bh; }
        }

        // Footprints and heights from the contract table.
        static readonly Spec[] Specs =
        {
            new Spec("Handcart", "Handcart_Broken", ClutterKind.Handcart, 1.0f, 1.9f, 1.0f, 1.2f, 1.9f, 0.8f),
            new Spec("CrateStack", "CrateStack_Broken", ClutterKind.CrateStack, 1.3f, 0.9f, 1.4f, 1.4f, 1.1f, 0.7f),
            new Spec("SackStack", "SackStack_Broken", ClutterKind.SackStack, 1.2f, 0.9f, 0.9f, 1.2f, 1.0f, 0.4f),
            new Spec("RainBarrel", "RainBarrel_Broken", ClutterKind.RainBarrel, 0.8f, 0.8f, 1.1f, 0.9f, 0.9f, 0.9f),
            new Spec("FlowerBox", "FlowerBox_Broken", ClutterKind.FlowerBox, 1.4f, 0.45f, 0.7f, 1.4f, 0.5f, 0.45f),
            new Spec("Well", "Well_Ruined", ClutterKind.Well, 2.0f, 2.0f, 3.0f, 2.0f, 2.0f, 1.4f),
        };

        static Spec SpecOf(string asset) { foreach (Spec s in Specs) if (s.Asset == asset || s.Broken == asset) return s; throw new ArgumentException("no clutter asset " + asset); }
        public static IEnumerable<string> RebuiltAssets() { foreach (Spec s in Specs) yield return s.Asset; }
        /// <summary>The ruined counterpart of a Rebuilt asset (Handcart_Broken, Well_Ruined, ShopSign_Broken ...), or null when there is none.</summary>
        public static string RuinedAsset(string asset)
        {
            if (asset != null && asset.StartsWith("ShopSign_", StringComparison.Ordinal)) return "ShopSign_Broken";
            foreach (Spec s in Specs) if (s.Asset == asset) return s.Broken;
            return null;
        }

        public static readonly string[] Trades = { "Bakery", "Inn", "Smithy", "Workshop", "Tailor", "Grocer" };
        /// <summary>The trades that get a board in the town: the Bakery, Inn and Smithy buildings (and the schoolhouse) carry their own Part_Sign, so they get no second one.</summary>
        public static readonly string[] SignedTrades = { "Workshop", "Tailor", "Grocer" };

        /// <summary>True for a building whose kit art already has a Part_Sign (HollowmereArt / HollowmereInnArt Spec.Parts).</summary>
        public static bool HasOwnSignArt(HollowmereLayout.BuildingKind kind) =>
            kind == HollowmereLayout.BuildingKind.Bakery || kind == HollowmereLayout.BuildingKind.Inn || kind == HollowmereLayout.BuildingKind.Smithy || kind == HollowmereLayout.BuildingKind.Schoolhouse;

        // The building kind a trade sign hangs on (Tailor and Grocer go on ring 1 homes and townhouses).
        public static bool TradeFits(string trade, HollowmereLayout.BuildingKind kind, int ring)
        {
            switch (trade)
            {
                case "Bakery": return kind == HollowmereLayout.BuildingKind.Bakery;
                case "Inn": return kind == HollowmereLayout.BuildingKind.Inn;
                case "Smithy": return kind == HollowmereLayout.BuildingKind.Smithy;
                case "Workshop": return kind == HollowmereLayout.BuildingKind.Workshop;
                case "Tailor": case "Grocer": return ring == 1 && (kind == HollowmereLayout.BuildingKind.Home || kind == HollowmereLayout.BuildingKind.Townhouse);
                default: return false;
            }
        }

        // ---------------------------------------------------------------------------------------------------- the wishes
        // (lot, asset, side, u, v, yaw relative to outward). Side F = the door side, R / L = the lot's right / left as seen from outside. u = metres beyond the
        // lot edge to the item's centre, v = metres along the edge from the lot's centre line (F: + = right; R / L: + = outward along the door direction).
        // yaw 0 = the art's front out, X along the edge; 90 = length along the edge (the handcart).
        // Each wish is searched outward from its spot (Reach) and dropped when nothing near passes the rules. Never inside a lot: yards belong to the garden pieces.
        const float Reach = 2.25f, Step = 0.25f;

        struct Wish
        {
            public string Lot, Asset; public char Side; public float U, V, Yaw;
            public Wish(string lot, string asset, char side, float u, float v, float yaw) { Lot = lot; Asset = asset; Side = side; U = u; V = v; Yaw = yaw; }
        }

        static Wish W(string lot, string asset, char side, float u, float v, float yaw = 0f) => new Wish(lot, asset, side, u, v, yaw);

        static readonly Wish[] Wishes =
        {
            // ---- ring 1: the square's frontages. The bakery (flour sacks, a handcart), the inn, the homes and the two townhouses.
            W("F02", "SackStack", 'F', 1.6f, -3.5f), W("F02", "Handcart", 'F', 2.2f, 4.2f, 90f), W("F02", "CrateStack", 'F', 1.5f, 0.5f), W("F02", "RainBarrel", 'L', 1.4f, 1.0f),
            W("F09", "RainBarrel", 'F', 1.5f, -3.5f), W("F09", "CrateStack", 'F', 1.4f, 3.5f), W("F09", "FlowerBox", 'F', 1.4f, -1.9f),
            W("F04", "FlowerBox", 'F', 1.5f, 0.0f), W("F04", "RainBarrel", 'F', 1.6f, 2.4f),
            W("F05", "FlowerBox", 'F', 1.5f, 0.0f), W("F05", "RainBarrel", 'F', 1.6f, 3.0f),
            W("F06", "FlowerBox", 'F', 1.5f, -1.8f), W("F06", "RainBarrel", 'F', 1.6f, 3.0f),
            W("F07", "FlowerBox", 'F', 1.5f, 1.8f), W("F07", "RainBarrel", 'F', 1.6f, -3.0f), W("F07", "SackStack", 'F', 1.6f, 3.2f),
            W("F08", "FlowerBox", 'F', 1.5f, 0.0f), W("F08", "RainBarrel", 'F', 1.6f, 2.2f),

            // ---- ring 2: the Ring Lane's houses, shops and workshops.
            W("R2_01", "CrateStack", 'F', 1.4f, -2.8f), W("R2_01", "RainBarrel", 'F', 1.4f, 2.8f),
            W("R2_02", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_02", "RainBarrel", 'F', 1.4f, 2.6f),
            W("R2_03", "FlowerBox", 'F', 1.4f, -1.8f), W("R2_03", "RainBarrel", 'F', 1.4f, 2.8f),
            W("R2_04", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_04", "RainBarrel", 'F', 1.4f, -2.8f),
            W("R2_05", "FlowerBox", 'F', 1.4f, -2.5f), W("R2_05", "RainBarrel", 'F', 1.4f, 3.8f),
            W("R2_06", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_06", "RainBarrel", 'F', 1.4f, 2.6f),
            W("R2_08", "CrateStack", 'F', 1.4f, -2.6f), W("R2_08", "Handcart", 'F', 1.9f, 2.6f, 90f), W("R2_08", "SackStack", 'F', 1.4f, 0.2f),
            W("R2_09", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_09", "RainBarrel", 'F', 1.4f, 2.2f),
            W("R2_11", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_11", "RainBarrel", 'F', 1.4f, -2.6f),
            W("R2_12", "FlowerBox", 'F', 1.4f, -2.6f), W("R2_12", "RainBarrel", 'F', 1.4f, 3.8f), W("R2_12", "CrateStack", 'F', 1.4f, 0.0f),
            W("R2_13", "CrateStack", 'F', 1.4f, 2.7f), W("R2_13", "SackStack", 'F', 1.4f, -2.7f),
            W("R2_14", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_14", "RainBarrel", 'F', 1.4f, 2.6f),
            W("R2_16", "FlowerBox", 'F', 1.4f, -2.8f), W("R2_16", "RainBarrel", 'F', 1.4f, 4.0f), W("R2_16", "CrateStack", 'F', 1.4f, 0.0f),
            W("R2_19", "CrateStack", 'F', 1.4f, -2.6f), W("R2_19", "SackStack", 'F', 1.4f, 2.6f), W("R2_19", "RainBarrel", 'F', 1.4f, 0.0f),
            W("R2_20", "FlowerBox", 'F', 1.4f, 0.0f), W("R2_20", "RainBarrel", 'F', 1.4f, -3.6f),
            W("R2_23", "CrateStack", 'F', 1.4f, 0.0f), W("R2_23", "RainBarrel", 'F', 1.4f, 2.6f),
        };

        // Spots on the square beside the market stalls (ring 1, anchored on a landmark instead of a lot): (asset, x, z, yaw, reach).
        static readonly (string Asset, float X, float Z, float Yaw, float Reach)[] StallWishes =
        {
            ("Handcart", 9.2f, -6.6f, 90f, 2.5f),    // by the tools stall (Stall_3)
            ("SackStack", 7.4f, 5.6f, 160f, 2.5f),   // by the produce stall (Stall_1)
        };

        // The well: the first of these spots that passes (ring, centre x, z). A square-edge spot clear of the capture sight lines first, then ring 2 lane widenings.
        static readonly (int Ring, float X, float Z)[] WellSpots =
        {
            (1, 15.5f, 1.0f), (1, 14.5f, 2.5f), (1, 12.5f, 2.0f), (1, 16.5f, -0.2f),
        };

        // Signs: (lot, trade, side of the face centre: +1 right / -1 left as seen from outside). Only buildings without their own Part_Sign: the workshops (no sign
        // part in their kit) and a tailor and a grocer on the south townhouses. The bakery, inn, smithy and schoolhouse art carry their own boards.
        static readonly (string Lot, string Trade, int Side)[] SignWishes =
        {
            ("R2_08", "Workshop", 1), ("R2_13", "Workshop", 1), ("R2_19", "Workshop", -1), ("R2_23", "Workshop", 1),
            ("F06", "Tailor", -1), ("F07", "Grocer", 1),
        };

        // Washing lines: neighbouring rebuilt houses (the wall of each that faces the other), at most ~7 m apart. A candidate that does not pass SpanProblems is dropped.
        static readonly (string A, string B, float UA, float UB, float AH, float BH)[] SpanWishes =
        {
            ("R2_11", "R2_12", 0.4f, 0.4f, 3.5f, 3.4f),
            ("R2_19", "R2_20", 0.4f, 0.4f, 3.5f, 3.4f),
            ("R2_08", "R2_09", 0.3f, 0.3f, 3.5f, 3.3f),
            ("R2_12", "R2_13", 0.3f, 0.3f, 3.5f, 3.4f),
            ("R2_04", "R2_05", 0.3f, 0.3f, 3.5f, 3.4f),
        };

        // ---------------------------------------------------------------------------------------------------- lot frames
        // The door side of a foundation as a world direction, and the lateral axis to its right as seen from outside.
        public static TownPoint DoorDir(Foundation f) { TownPoint s = Foundation.SideDir(f.Facing); Footprint fp = f.Footprint; return fp.AxisX * s.X + fp.AxisZ * s.Z; }
        static TownPoint Right(TownPoint d) => new TownPoint(d.Z, -d.X);
        public static float YawOf(TownPoint d) => (float)(Math.Atan2(d.X, d.Z) * 180.0 / Math.PI);
        static TownPoint Pt(float x, float z) => new TownPoint(x, z);

        // Body size (across the front, front to back) of the building on a lot. The kit exports' FOOTPRINT numbers (Content/Art/Town SOURCES.md); the inn's is not
        // written down, so it takes a safe 9.0 x 6.5 (the greybox home: the class maximum). False for a lot with no building or the approved facade.
        public static bool BodySize(Foundation f, HollowmereLayout.HomeAssignment a, out float w, out float d)
        {
            w = d = 0f;
            float max = Foundation.MaxBuilding(f.Size);
            switch (a.Kind)
            {
                case HollowmereLayout.BuildingKind.Empty: case HollowmereLayout.BuildingKind.Garden: case HollowmereLayout.BuildingKind.ApprovedFacade: return false;
                case HollowmereLayout.BuildingKind.Bakery: w = 9.62f; d = 6.84f; return true;
                case HollowmereLayout.BuildingKind.Inn: w = 9.0f; d = 6.5f; return true;
                case HollowmereLayout.BuildingKind.Smithy: w = 7.2f; d = 5.6f; return true;
                case HollowmereLayout.BuildingKind.Schoolhouse: w = 5.4f; d = 6.4f; return true;
                case HollowmereLayout.BuildingKind.Workshop:
                    if (a.Variant == 'A') { w = 4.83f; d = 4.34f; } else if (a.Variant == 'B') { w = 6.73f; d = 6.30f; } else { w = 6.95f; d = 5.28f; }
                    return true;
                case HollowmereLayout.BuildingKind.StoneCottage:
                    if (a.Variant == 'A') { w = 4.85f; d = 4.77f; } else if (a.Variant == 'B') { w = 4.61f; d = 4.38f; } else { w = 4.88f; d = 4.52f; }
                    return true;
                case HollowmereLayout.BuildingKind.Townhouse:
                    if (a.Variant == 'A') { w = 5.45f; d = 6.85f; } else if (a.Variant == 'B') { w = 5.71f; d = 6.19f; } else { w = 5.13f; d = 6.89f; }
                    return true;
                default: w = max; d = max; return true;   // a greybox home on a lot with no kit
            }
        }

        sealed class Body { public Foundation Lot; public HollowmereLayout.HomeAssignment Home; public float W, D; public Footprint Fp; public TownPoint Dir, Right; }

        // ---------------------------------------------------------------------------------------------------- context
        sealed class Ctx
        {
            public TownLayout T;
            public readonly List<Body> Bodies = new List<Body>();
            public readonly Dictionary<string, Body> ByLot = new Dictionary<string, Body>();
            public readonly List<(Footprint Fp, string Lot)> CutFill = new List<(Footprint, string)>();
        }

        static Ctx MakeCtx(TownLayout t)
        {
            var c = new Ctx { T = t };
            foreach (Foundation f in t.Foundations)
            {
                HollowmereLayout.HomeAssignment a = HollowmereLayout.RebuiltHome(f.Id);
                if (f.Ring <= 2 && BodySize(f, a, out float w, out float d))
                {
                    TownPoint dir = DoorDir(f);
                    var b = new Body { Lot = f, Home = a, W = w, D = d, Dir = dir, Right = Right(dir) };
                    b.Fp = new Footprint(f.Centre, w, d, YawOf(dir));   // local Z = out through the door side, X to the right as seen from outside
                    c.Bodies.Add(b); c.ByLot[f.Id] = b;
                }
                foreach (Footprint fp in CutFillBoxes(f)) c.CutFill.Add((fp, f.Id));
            }
            return c;
        }

        // The retaining walls and fill banks the builder puts round a lot (HollowmereBuilder.CutAndFill): mirrored here so clutter stays off them.
        static List<Footprint> CutFillBoxes(Foundation f)
        {
            var boxes = new List<Footprint>();
            float half = f.Side * 0.5f; Footprint lot = f.Footprint;
            foreach (LotSide s in TownLayout.AllSides)
            {
                TownPoint dir = Foundation.SideDir(s), along = Pt(-dir.Z, dir.X);
                float hi = float.MinValue, lo = float.MaxValue;
                for (int k = -2; k <= 2; k++)
                {
                    TownPoint l = dir * (half + 2.3f) + along * (k * half * 0.45f);
                    TownPoint w = lot.ToWorld(l.X, l.Z);
                    float y = HollowmereLayout.NaturalGround(w.X, w.Z);
                    hi = Math.Max(hi, y); lo = Math.Min(lo, y);
                }
                bool eastWest = Math.Abs(dir.X) > 0.5f;
                if (hi > f.Y + 0.7f)
                {
                    TownPoint c = lot.ToWorld(dir.X * (half + 1.3f), dir.Z * (half + 1.3f));
                    boxes.Add(new Footprint(c, eastWest ? 0.7f : f.Side + 2.6f, eastWest ? f.Side + 2.6f : 0.7f, f.Yaw));
                }
                if (lo < f.Y - 0.7f)
                {
                    TownPoint c = lot.ToWorld(dir.X * (half + 0.65f), dir.Z * (half + 0.65f));
                    boxes.Add(new Footprint(c, eastWest ? 1.3f : f.Side + 1.3f, eastWest ? f.Side + 1.3f : 1.3f, f.Yaw));
                }
            }
            return boxes;
        }

        // ---------------------------------------------------------------------------------------------------- rows
        public static Footprint FootprintOf(ClutterRow r) => new Footprint(Pt(r.X, r.Z), r.SizeX, r.SizeZ, r.Yaw);
        static Footprint Inflated(ClutterRow r, float by) => new Footprint(Pt(r.X, r.Z), r.SizeX + 2f * by, r.SizeZ + 2f * by, r.Yaw);

        static readonly Lazy<ClutterPlan> _default = new Lazy<ClutterPlan>(() => Build(HollowmereLayout.Create()));
        public static ClutterPlan Default => _default.Value;
        public static IReadOnlyList<ClutterRow> Rows => Default.Rows;
        public static IReadOnlyList<ClutterSign> Signs => Default.Signs;
        public static IReadOnlyList<WashingSpan> Spans => Default.Spans;

        public static ClutterPlan Build(TownLayout t)
        {
            Ctx c = MakeCtx(t);
            var plan = new ClutterPlan();
            var rebuilt = plan.Rows;

            // The well first (it is the biggest piece), then each wish in table order.
            Spec well = SpecOf("Well");
            foreach (var s in WellSpots)
            {
                ClutterRow r = NewRow("Clutter_Well", well, Rb, false, s.Ring, null, s.X, s.Z, 0f);
                if (Fits(c, r, rebuilt, null)) { rebuilt.Add(r); break; }
            }
            var perLot = new Dictionary<string, int>();
            foreach (Wish w in Wishes)
            {
                Foundation f = t.Foundation(w.Lot);
                if (f == null || !c.ByLot.ContainsKey(w.Lot)) continue;
                perLot.TryGetValue(w.Lot, out int n);
                ClutterRow r = Search(c, f, w, rebuilt, "Clutter_" + w.Lot + "_" + n + "_" + w.Asset);
                if (r != null) { rebuilt.Add(r); perLot[w.Lot] = n + 1; }
            }

            int sq = 0;
            foreach (var sw in StallWishes)
            {
                Spec s = SpecOf(sw.Asset);
                ClutterRow r = SearchAround(c, s, Pt(sw.X, sw.Z), Pt(0f, 1f), Pt(1f, 0f), sw.Yaw, 1, null, sw.Reach, rebuilt, "Clutter_Sq_" + sq + "_" + sw.Asset);
                if (r != null) { rebuilt.Add(r); sq++; }
            }

            foreach (var sw in SignWishes)
            {
                Foundation f = t.Foundation(sw.Lot);
                if (f == null || !c.ByLot.TryGetValue(sw.Lot, out Body b)) continue;
                float lateral = sw.Side * Math.Min(b.W * 0.5f * 0.6f, 2.8f);
                TownPoint p = f.Centre + b.Dir * (b.D * 0.5f) + b.Right * lateral;
                plan.Signs.Add(new ClutterSign { Id = "Sign_" + sw.Lot + "_" + sw.Trade, Asset = "ShopSign_" + sw.Trade, Trade = sw.Trade, Lot = sw.Lot, Preset = Rb, X = p.X, Z = p.Z, Yaw = YawOf(b.Dir), PivotHeight = SignHeight });
            }

            foreach (var sp in SpanWishes)
            {
                if (!c.ByLot.TryGetValue(sp.A, out Body a) || !c.ByLot.TryGetValue(sp.B, out Body b)) continue;
                TownPoint ab = b.Lot.Centre - a.Lot.Centre;
                float sa = Math.Sign(ab.X * a.Right.X + ab.Z * a.Right.Z), sb = Math.Sign(-(ab.X * b.Right.X + ab.Z * b.Right.Z));
                if (sa == 0f || sb == 0f) continue;
                TownPoint pa = a.Lot.Centre + a.Right * (sa * a.W * 0.5f) + a.Dir * (sp.UA * a.D * 0.5f);
                TownPoint pb = b.Lot.Centre + b.Right * (sb * b.W * 0.5f) + b.Dir * (sp.UB * b.D * 0.5f);
                var span = new WashingSpan { Id = "Line_" + sp.A + "_" + sp.B, LotA = sp.A, LotB = sp.B, Preset = Rb, AX = pa.X, AZ = pa.Z, AH = sp.AH, BX = pb.X, BZ = pb.Z, BH = sp.BH, Sag = LineSag };
                var spanErrors = new List<string>();
                SpanProblems(c, span, spanErrors);
                if (spanErrors.Count == 0) plan.Spans.Add(span);
            }

            AddRuined(c, plan);
            return plan;
        }

        static ClutterRow NewRow(string id, Spec s, DecoPreset preset, bool broken, int ring, string lot, float x, float z, float yaw)
        {
            return new ClutterRow
            {
                Id = id, Asset = broken ? s.Broken : s.Asset, Kind = s.Kind, Preset = preset, Broken = broken, Ring = ring, Lot = lot, X = x, Z = z, Yaw = yaw,
                SizeX = broken ? s.BX : s.X, SizeZ = broken ? s.BZ : s.Z, Height = broken ? s.BH : s.H,
            };
        }

        static ClutterRow Search(Ctx c, Foundation f, Wish w, List<ClutterRow> accepted, string id)
        {
            Body b = c.ByLot[w.Lot];
            Spec s = SpecOf(w.Asset);
            TownPoint o, a;
            if (w.Side == 'F') { o = b.Dir; a = b.Right; }
            else if (w.Side == 'R') { o = b.Right; a = b.Dir; }
            else { o = Pt(-b.Right.X, -b.Right.Z); a = b.Dir; }
            float half = f.Side * 0.5f;
            return SearchAround(c, s, f.Centre + o * (half + w.U) + a * w.V, o, a, YawOf(o) + w.Yaw, f.Ring, f.Id, Reach, accepted, id);
        }

        // The first spot that passes the rules, searched outward (nearest first) from baseP in steps along o (outward) and a (along the edge).
        static ClutterRow SearchAround(Ctx c, Spec s, TownPoint baseP, TownPoint o, TownPoint a, float yaw, int ring, string lot, float reach, List<ClutterRow> accepted, string id)
        {
            int n = (int)Math.Round(reach / Step);
            var offsets = new List<(float Du, float Dv)>();
            for (int i = -n; i <= n; i++) for (int j = -n; j <= n; j++) offsets.Add((i * Step, j * Step));
            // nearest first; ties in a fixed order (outward, then to the right), so the result never depends on the sort implementation
            offsets.Sort((p, q) =>
            {
                int cmp = (p.Du * p.Du + p.Dv * p.Dv).CompareTo(q.Du * q.Du + q.Dv * q.Dv);
                if (cmp != 0) return cmp;
                cmp = q.Du.CompareTo(p.Du);
                return cmp != 0 ? cmp : q.Dv.CompareTo(p.Dv);
            });
            foreach (var off in offsets)
            {
                TownPoint p = baseP + o * off.Du + a * off.Dv;
                ClutterRow r = NewRow(id, s, Rb, false, ring, lot, p.X, p.Z, yaw);
                if (Fits(c, r, accepted, null)) return r;
            }
            return null;
        }

        // ---------------------------------------------------------------------------------------------------- ruined
        // About a third of the ground rows, as the broken variants at the same spots (every kind at least once: a kind whose first spot does not take the broken
        // footprint tries its next Rebuilt spot), the Well_Ruined at the Well's spot. Some are tilted.
        static void AddRuined(Ctx c, ClutterPlan plan)
        {
            var src = new List<ClutterRow>();
            ClutterRow well = null;
            foreach (ClutterRow r in plan.Rows) { if (r.Kind == ClutterKind.Well) well = r; else src.Add(r); }
            int target = Math.Max(7, (int)Math.Floor(src.Count / 3.5f));
            var chosen = new List<ClutterRow>();
            var ruined = new List<ClutterRow>();
            if (well != null) ruined.Add(Broken(well, "Clutter_Ru_Well", 0));
            var taken = new HashSet<ClutterRow>();
            var kindsSeen = new HashSet<ClutterKind>();
            int idx = 0;
            bool Try(ClutterRow r, bool force)
            {
                if (taken.Contains(r) || (!force && chosen.Count >= target)) return false;
                ClutterRow b = Broken(r, "Clutter_Ru_" + (r.Lot ?? "Sq") + "_" + r.Kind + "_" + idx, idx);
                if (!Fits(c, b, ruined, null))
                {
                    // the tilt or yaw jitter may be what fails: try it square-on
                    b.Yaw = r.Yaw; b.TiltX = b.TiltZ = 0f;
                    if (!Fits(c, b, ruined, null)) return false;
                }
                ruined.Add(b); chosen.Add(b); taken.Add(r); kindsSeen.Add(r.Kind); idx++;
                return true;
            }
            // one of each kind first, walking the Rebuilt spots of that kind until one takes the broken variant
            foreach (ClutterKind k in new[] { ClutterKind.Handcart, ClutterKind.CrateStack, ClutterKind.SackStack, ClutterKind.RainBarrel, ClutterKind.FlowerBox })
                foreach (ClutterRow r in src) if (r.Kind == k && !kindsSeen.Contains(k)) Try(r, true);
            int stride = Math.Max(1, src.Count / Math.Max(1, target));
            for (int pass = 0; pass < stride && chosen.Count < target; pass++)
                for (int i2 = pass; i2 < src.Count && chosen.Count < target; i2 += stride) Try(src[i2], false);
            plan.Rows.AddRange(ruined);
        }

        static ClutterRow Broken(ClutterRow r, string id, int idx)
        {
            Spec s = SpecOf(r.Asset);
            ClutterRow b = NewRow(id, s, Ru, true, r.Ring, r.Lot, r.X, r.Z, r.Yaw);
            if (s.Kind != ClutterKind.Well)
            {
                b.Yaw = r.Yaw + ((idx * 37) % 41 - 20);
                // some of them lean or tip: a stack slumps, a barrel leans on its hoops, a flower box tilts on a broken leg
                if (idx % 2 == 1)
                {
                    float t = 6f + (idx * 5) % 9;
                    if (s.Kind == ClutterKind.CrateStack || s.Kind == ClutterKind.RainBarrel) { b.TiltX = t; b.TiltZ = -t * 0.6f; }
                    else if (s.Kind == ClutterKind.FlowerBox) b.TiltZ = t;
                }
            }
            return b;
        }

        // ---------------------------------------------------------------------------------------------------- rules
        // Every rule a ground row has to meet against the layout and the rows already accepted. Empty list = it fits. `all` stops at the first problem when null.
        static bool Fits(Ctx c, ClutterRow r, IReadOnlyList<ClutterRow> others, List<string> errors)
        {
            var found = errors ?? new List<string>();
            int before = found.Count;
            Problems(c, r, others, found, errors == null);
            return found.Count == before;
        }

        static void Problems(Ctx c, ClutterRow r, IReadOnlyList<ClutterRow> others, List<string> e, bool first)
        {
            TownLayout t = c.T;
            Footprint fp = FootprintOf(r), pad = Inflated(r, Clearance);
            // path graph: the key rule
            foreach (PathNode n in t.Nodes) if (pad.Contains(Pt(n.Pos.X, n.Pos.Z))) { e.Add(r.Id + " covers path node " + n.Id); if (first) return; }
            foreach (PathEdge ed in t.Edges)
            {
                PathNode a = t.Node(ed.A), b = t.Node(ed.B);
                if (a != null && b != null && pad.SegmentCrosses(a.Pos, b.Pos)) { e.Add(r.Id + " blocks path edge " + ed.A + "-" + ed.B); if (first) return; }
            }
            foreach (Foundation f in t.Foundations)
            {
                if (pad.Overlaps(f.Footprint)) { e.Add(r.Id + " overlaps foundation " + f.Id); if (first) return; }
            }
            foreach (Landmark l in t.Landmarks)
                if ((l.Kind == LandmarkKind.Stall ? Inflated(r, StallMargin) : pad).Overlaps(l.Footprint)) { e.Add(r.Id + " overlaps landmark " + l.Id); if (first) return; }
            if (fp.Overlaps(KeepStrip)) { e.Add(r.Id + " stands on the keep terrace wall strip"); if (first) return; }
            if (fp.Overlaps(HollowmereDressing.CornerRect)) { e.Add(r.Id + " stands in the approved beauty corner"); if (first) return; }
            foreach ((Footprint box, string lot) in c.CutFill) if (fp.Overlaps(box)) { e.Add(r.Id + " stands on the cut or fill wall of " + lot); if (first) return; }
            if (r.Ring != 1 && r.Ring != 2) { e.Add(r.Id + ": ring " + r.Ring + " is not 1 or 2"); if (first) return; }
            else foreach (TownPoint p in fp.Corners()) if (!OnRing(t, r.Ring, p)) { e.Add(r.Id + " is off ring " + r.Ring + " (the square and its lane stubs / beside a ring 2 lane)"); if (first) return; break; }
            foreach (DecoRow d in HollowmereDressing.Rows)
                if (HollowmereDressing.InPreset(d.Preset, r.Preset) && Inflated(r, DressingGap).Overlaps(HollowmereDressing.FootprintOf(d))) { e.Add(r.Id + " overlaps square dressing " + d.Id); if (first) return; }
            // slope: the four corners and the centre must sit on ground within MaxSlope of each other
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (TownPoint p in fp.Corners()) { float g = HollowmereLayout.Ground(t, p.X, p.Z); lo = Math.Min(lo, g); hi = Math.Max(hi, g); }
                if (hi - lo > MaxSlope) { e.Add(r.Id + ": ground under it varies " + (hi - lo).ToString("0.00") + " m (over " + MaxSlope + ")"); if (first) return; }
            }
            foreach (ClutterRow o in others)
            {
                if (o == r || o.SizeX <= 0f || !SharesPreset(r.Preset, o.Preset)) continue;
                if (Inflated(r, RowGap).Overlaps(FootprintOf(o))) { e.Add(r.Id + " overlaps " + o.Id); if (first) return; }
            }
        }

        static bool SharesPreset(DecoPreset a, DecoPreset b) => a == DecoPreset.Both || b == DecoPreset.Both || a == b;

        static bool OnRing(TownLayout t, int ring, TownPoint p)
        {
            if (ring == 1)
                return p.X >= HollowmereLayout.SquareMinX - HollowmereDressing.SquareMargin && p.X <= HollowmereLayout.SquareMaxX + HollowmereDressing.SquareMargin
                    && p.Z >= HollowmereLayout.SquareMinZ - HollowmereDressing.SquareMargin && p.Z <= HollowmereLayout.SquareMaxZ + HollowmereDressing.SquareMargin;
            foreach (Lane lane in t.Lanes)
                if (lane.Ring <= 2 && HollowmereLayout.DistanceToPolyline(p, lane.Points) <= lane.Width * 0.5f + LaneMargin) return true;
            return false;
        }

        // ---------------------------------------------------------------------------------------------------- validate
        public static List<string> Validate(TownLayout t) => Validate(t, Default.Rows, Default.Signs, Default.Spans);

        // Every broken rule as one line (empty = valid).
        public static List<string> Validate(TownLayout t, IReadOnlyList<ClutterRow> rows, IReadOnlyList<ClutterSign> signs, IReadOnlyList<WashingSpan> spans)
        {
            var errors = new List<string>();
            Ctx c = MakeCtx(t);
            var ids = new HashSet<string>();
            foreach (ClutterRow r in rows)
            {
                if (!ids.Add(r.Id)) errors.Add("duplicate clutter id " + r.Id);
                if (r.SizeX <= 0f || r.SizeZ <= 0f) { errors.Add(r.Id + ": footprint has no area"); continue; }
                Problems(c, r, new List<ClutterRow>(Later(rows, r)), errors, false);
            }
            foreach (ClutterSign s in signs) { if (!ids.Add(s.Id)) errors.Add("duplicate clutter id " + s.Id); SignProblems(c, s, errors); }
            foreach (WashingSpan w in spans) { if (!ids.Add(w.Id)) errors.Add("duplicate clutter id " + w.Id); SpanProblems(c, w, errors); }
            return errors;
        }

        // The rows after r in the list: each pair is checked once.
        static IEnumerable<ClutterRow> Later(IReadOnlyList<ClutterRow> rows, ClutterRow r)
        {
            bool after = false;
            foreach (ClutterRow o in rows) { if (after) yield return o; else if (o == r) after = true; }
        }

        static void SignProblems(Ctx c, ClutterSign s, List<string> e)
        {
            Foundation f = c.T.Foundation(s.Lot);
            if (f == null) { e.Add(s.Id + ": no lot " + s.Lot); return; }
            if (s.Preset != Rb) e.Add(s.Id + ": a sign needs a standing building, so Rebuilt only");
            HollowmereLayout.BuildingKind kind = HollowmereLayout.RebuiltBuilding(s.Lot);
            if (HasOwnSignArt(kind)) e.Add(s.Id + ": the " + kind + " art already has its own Part_Sign");
            if (!TradeFits(s.Trade, kind, f.Ring)) e.Add(s.Id + ": a " + s.Trade + " sign does not belong on a " + kind + " (" + s.Lot + ")");
            if (s.Asset != "ShopSign_" + s.Trade) e.Add(s.Id + ": asset " + s.Asset + " is not the " + s.Trade + " sign");
            if (s.PivotHeight - SignDrop < MinSignBoard) e.Add(s.Id + ": the board's lowest point " + (s.PivotHeight - SignDrop).ToString("0.00") + " m is under " + MinSignBoard + " m");
            if (!c.ByLot.TryGetValue(s.Lot, out Body b)) { e.Add(s.Id + ": " + s.Lot + " has no building body"); return; }
            TownPoint d = Pt(s.X - f.Centre.X, s.Z - f.Centre.Z);
            float along = d.X * b.Dir.X + d.Z * b.Dir.Z, lateral = d.X * b.Right.X + d.Z * b.Right.Z;
            if (Math.Abs(along - b.D * 0.5f) > 0.05f) e.Add(s.Id + ": the pivot is " + along.ToString("0.00") + " m from the lot centre, not on the door-side face at " + (b.D * 0.5f).ToString("0.00"));
            if (Math.Abs(lateral) > b.W * 0.5f - 0.3f) e.Add(s.Id + ": the pivot is off the face (" + lateral.ToString("0.00") + " m across a " + b.W.ToString("0.00") + " m front)");
            float yawErr = Math.Abs(Mathf360(s.Yaw - YawOf(b.Dir)));
            if (yawErr > 1f) e.Add(s.Id + ": the arm does not point out from the wall");
        }

        static float Mathf360(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }

        static void SpanProblems(Ctx c, WashingSpan w, List<string> e)
        {
            if (w.Preset != Rb) e.Add(w.Id + ": a washing line needs two standing walls, so Rebuilt only");
            if (w.AH < MinLineEnd || w.BH < MinLineEnd) e.Add(w.Id + ": an end is lower than " + MinLineEnd + " m");
            float len = w.Length;
            if (len < MinLineSpan || len > MaxLineSpan) e.Add(w.Id + ": span " + len.ToString("0.0") + " m is outside " + MinLineSpan + "-" + MaxLineSpan + " m");
            if (w.Sag <= 0f || w.Sag > 0.8f) e.Add(w.Id + ": sag " + w.Sag + " m is not between 0 and 0.8");
            TownPoint a = Pt(w.AX, w.AZ), b = Pt(w.BX, w.BZ);
            if (!c.ByLot.TryGetValue(w.LotA, out Body ba) || !c.ByLot.TryGetValue(w.LotB, out Body bb)) { e.Add(w.Id + ": both ends need a rebuilt building body"); return; }
            if (!OnWall(ba, a)) e.Add(w.Id + ": end A is not on a wall of " + w.LotA);
            if (!OnWall(bb, b)) e.Add(w.Id + ": end B is not on a wall of " + w.LotB);
            foreach (Body body in c.Bodies) if (body.Fp.SegmentCrosses(a, b)) e.Add(w.Id + " crosses the building on " + body.Lot.Id);
            foreach (Landmark l in c.T.Landmarks) if (l.Footprint.SegmentCrosses(a, b)) e.Add(w.Id + " crosses landmark " + l.Id);
            // the lowest cloth: the line's height there minus the longest cloth, against the ground under it
            float ya = HollowmereLayout.Ground(c.T, a.X, a.Z) + w.AH, yb = HollowmereLayout.Ground(c.T, b.X, b.Z) + w.BH, worst = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                float s = i / 20f;
                TownPoint p = a + (b - a) * s;
                float y = ya + (yb - ya) * s - 4f * w.Sag * s * (1f - s);
                worst = Math.Min(worst, y - ClothHang - HollowmereLayout.Ground(c.T, p.X, p.Z));
            }
            if (worst < MinClothClearance) e.Add(w.Id + ": the lowest cloth is only " + worst.ToString("0.00") + " m above the ground (needs " + MinClothClearance + ")");
        }

        // Is the point on the boundary of the body (within 5 cm) and not past its corner?
        static bool OnWall(Body b, TownPoint p)
        {
            TownPoint l = b.Fp.ToLocal(p);
            bool side = Math.Abs(Math.Abs(l.X) - b.Fp.HalfX) < 0.05f && Math.Abs(l.Z) <= b.Fp.HalfZ + 0.05f;
            bool end = Math.Abs(Math.Abs(l.Z) - b.Fp.HalfZ) < 0.05f && Math.Abs(l.X) <= b.Fp.HalfX + 0.05f;
            return side || end;
        }
    }
}
