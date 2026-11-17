// Builds scene Hollowmere, the Wave 0 greybox town (docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md section 4), from the layout in
// Core/Town/HollowmereLayout. The build fails (throws) on any TownLayout.Validate() line: a foundation over another foundation, a landmark or a path node,
// a path edge across a footprint, or a lot side with no reachable doorstep.
// The scene holds both presets as sibling roots and ApplyPreset switches them: Ruined (the start: footing slab, doorstep and ruin stubs on every lot; the stalls
// bare frames; the belfry empty; the mill a roofless shell) and Rebuilt (ring 1 by HollowmereLayout.RebuiltBuilding: bakery and inn on L lots, homes on M/S,
// the approved facade on F01, three lots left empty; rings 2-3 as home massing with a few gardens). Each building block faces its lot's default door side.
// Art where it exists: the approved corner's rows (fountain B10, cobbles B6, stall B8, facade B4, props, foliage, lanterns) come in through
// BeautyCornerBuilder.PlaceRow at their corner positions; the three stalls are the Wave 1 Ring1/Stalls art (ruined or rebuilt state), else the B8 stall; the B4 bell tower when its folder exists;
// the heroes' home from Ring1/HeroesHome (HeroesHomeArt). Everything else is greybox on the LookTest materials, except the buildings whose final export is in
// (HollowmereArt: the bakery, smithy, schoolhouse and mill; HollowmereInnArt: the inn). Light, grade, sky and renderer: the beauty corner's (BeautyCornerBuilder.EnsureLook / ApplyPreset), so the
// approved corner looks the same here. Ground: a Unity Terrain from the layout heights (HollowmereTerrain).
// Ruins: every empty lot shows its ruin kit variant (RuinVariant, RuinTurn); a placed building or garden switches it off.
// Idempotent: the scene is rebuilt through BuilderUtil.SaveScene.
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static partial class HollowmereBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Hollowmere.unity";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        public enum Preset { Ruined, Rebuilt }
        public const string RuinedRoot = "Preset_Ruined", RebuiltRoot = "Preset_Rebuilt";

        // Corner rows that belong to the rebuilt F01 house and the dressed stall (absent in the ruined start), and the rows never brought in (the corner's backdrop).
        static readonly string[] RebuiltOnlyRows = { "Facade", "MarketStall", "Barrels", "Crates", "Sacks", "Buckets", "Baskets", "Jugs", "Ivy", "LintelIvy", "GrassStrip", "StallLantern", "BracketLantern" };

        // Play-position capture cameras (design 7: square entry from the heroes' home, fountain, tithe gate, a lane mouth), three low-angle terrain views and a
        // high oblique overview. Pos.y is the eye height above the ground at Pos (Ground, so the cameras follow the terrain); Target is absolute.
        // The approved corner's own three cameras are added too, unchanged (BeautyCornerTable.Cameras).
        public static readonly (string Name, Vector3 Pos, Vector3 Target, float Fov)[] Cameras =
        {
            ("Cap_HM_HomeDoor", new Vector3(12.0f, 1.65f, 5.4f), new Vector3(-3f, 1.6f, -5f), 50f),
            ("Cap_HM_Fountain", new Vector3(-6.5f, 1.65f, -9.5f), new Vector3(2.5f, 2.6f, 9f), 50f),
            ("Cap_HM_TitheGate", new Vector3(5.2f, 1.65f, 8.2f), new Vector3(5.6f, 5.5f, 22f), 55f),
            ("Cap_HM_LaneMouth", new Vector3(-0.4f, 1.65f, -40f), new Vector3(0f, 1.0f, -8f), 50f),
            ("Cap_HM_LakeShore", new Vector3(-26f, 1.7f, -88f), new Vector3(-2f, 3f, -20f), 50f),       // from the lake shore by R3_01 up at the hill town and the keep
            ("Cap_HM_MarketStreet", new Vector3(1.2f, 1.65f, -47.5f), new Vector3(-0.3f, -1.0f, -27f), 55f), // Q11: low on Market Street looking UP the flight to the square (risers face downhill)
            ("Cap_HM_KeepStairs", new Vector3(-31f, 1.7f, 13.8f), new Vector3(-20f, 3.0f, 19.6f), 58f),  // along the Wall Ginnel: terrace step, west stair, keep wall
            ("Cap_HM_Smithy", new Vector3(-48.6f, 1.7f, 2.9f), new Vector3(-38.8f, -1.0f, 7.4f), 55f),       // ring 2: the smithy on R2_01 from the Ring Lane (forge bay toward the camera)
            ("Cap_HM_Schoolhouse", new Vector3(-44.4f, 1.7f, -39.7f), new Vector3(-38.2f, -1.5f, -30.9f), 55f), // ring 2: the schoolhouse on R2_03 from the Ring Lane
            ("Cap_HM_Mill", new Vector3(70.8f, 2.0f, -74.3f), new Vector3(71.2f, -6.0f, -59.5f), 55f),        // the mill's front and wheel side from the stream below
            ("Cap_HM_Bakery", new Vector3(-6.2f, 1.65f, 2.6f), new Vector3(-12.5f, 3.4f, 12.2f), 55f),       // from the square at the bakery front on F02 (oven side on the right)
            ("Cap_HM_HeroesHome", new Vector3(1.5f, 1.65f, -2.0f), new Vector3(12.0f, 3.6f, 12.5f), 50f),    // from the fountain at the heroes' home: Wren's list, the lit door, the annex above
            ("Cap_HM_HeroesAnnex", new Vector3(19.0f, 1.65f, 20.8f), new Vector3(11.8f, 6.6f, 24.5f), 55f),  // from the keep stair head at the bell-loft annex door and loft
            ("Cap_HM_KeepWall", new Vector3(-2f, 1.65f, -3f), new Vector3(2f, 3.2f, 19.6f), 60f),          // from the fountain: keep wall, buttresses, planters, stairs (Ruined shows it whole)
            ("Cap_HM_Ruins", new Vector3(-15.5f, 1.65f, -1.5f), new Vector3(-26f, 0.2f, -8f), 50f),          // from the square's west side over the F03 and F05 ruins
            ("Cap_HM_Townhouses", new Vector3(-1f, 1.65f, -4f), new Vector3(-1f, 5.2f, -27f), 64f),        // from the square at the townhouse pair on F06 and F07 (kit family 3)
            ("Cap_HM_TownhouseSide", new Vector3(22f, 1.65f, -11f), new Vector3(5f, 5.0f, -27f), 55f),    // three-quarter on F07's front and east side
            ("Cap_HM_Kits", new Vector3(45.5f, 1.65f, 12f), new Vector3(39f, 2f, -1f), 62f),           // from the Ring Lane's north end looking south along the east run at the stone cottage on R2_09 and the workshop on R2_08 (kit families 2 and 4)
            ("Cap_HM_WallRun", new Vector3(-76f, 4.5f, 0f), new Vector3(-62f, -0.6f, 12f), 50f),     // polish 2: high outside the west wall, over the steep bank, looking along the wall run (stone tint, coursing, merlons)
            ("Cap_HM_LowEast", new Vector3(86f, 2.5f, -14f), new Vector3(30f, -2f, -6f), 50f),            // outside the east wall: ditch, bank, wall, terraces, keep
            ("Cap_HM_LowerGate", new Vector3(-21.3f, 1.65f, -78f), new Vector3(-21.3f, -3.2f, -64.6f), 52f),   // Q8: the Lower Gate from the road outside, looking north up through the wall (Target is absolute: the gate stands at about y -7.4)
            ("Cap_HM_EastGate", new Vector3(64.2f, 1.65f, -54.6f), new Vector3(56.6f, -4.5f, -46f), 52f),       // Q8: the East Gate from the Mill Lane outside, looking back up at the wall (gate at about y -8.5)
            ("Cap_HM_SquareSouth", new Vector3(-6.0f, 1.65f, -17.0f), new Vector3(4f, 1.2f, -6f), 55f),   // benches, planters, the Market Street mouth and the bunting
            ("Cap_HM_SquareWest", new Vector3(14.0f, 1.65f, -14.0f), new Vector3(-14f, 1.0f, -8f), 55f),   // the square's west half and the West Lane mouth
            ("Cap_HM_Clutter", new Vector3(44.6f, 1.65f, -17.2f), new Vector3(52.3f, -1.3f, -16.6f), 55f),      // Q14: down the gap beside R2_19 at the washing line to R2_20 (span x 52.1-52.6, z -19.75 to -13.43, ends 3.5 m up), eye 1.65 m
            ("Cap_HM_RuinedLane", new Vector3(-45.5f, 1.65f, -9.5f), new Vector3(-42f, -3.0f, -1.5f), 55f),      // polish 2: up the Ring Lane at the west lane wreck group (tipped cart, split barrel, slumped crates; Ruined)
            ("Cap_HM_LaneMouthWest", new Vector3(-45.0f, 1.65f, -8.1f), new Vector3(-30f, -0.6f, -7.3f), 55f),   // Q11: from the Ring Lane at the West Lane junction up the West Lane flight to the square
            ("Cap_HM_TerraceSteps", new Vector3(-46.2f, 3.4f, -22.5f), new Vector3(-45.2f, -4.6f, -30f), 55f),   // Q11 (stairs-traversal-3 r2: raised and closer, the old eye 1.65 m at z -20 looked along the flight's own 13 degree slope so the treads hid each other): the Ring Lane's south-west 0.24 drop: flight, kerbs, gutter, the retaining wall
            ("Cap_HM_LakeJetty", new Vector3(-13.5f, 1.9f, -76.5f), new Vector3(-16f, -12.2f, -100f), 55f),   // Q10: from the north bank beside the jetty (root ~(-22,-82), deck heading SE), looking over it and the lake to the far shore
            ("Cap_HM_Ravine", new Vector3(107.8f, 1.7f, -21.4f), new Vector3(91f, -11.5f, -43f), 55f),    // from the ravine's bank at s ~22 downstream along the pools and weirs W1, W2
            ("Cap_HM_MillLane", new Vector3(60.5f, 1.65f, -51.5f), new Vector3(72f, -5f, -64f), 65f),         // on Mill Lane toward the mill and the stream
            ("Cap_HM_PX_Annex", new Vector3(9.0f, 1.65f, 0.5f), new Vector3(14f, 8.5f, 22f), 55f),         // town polish r2 (A5, A7): cistern-house gable, the ground patch in front and the annex's upper storey, like trailer frame a3
            ("Cap_HM_PX_AnnexW", new Vector3(3.0f, 1.65f, 13.0f), new Vector3(12f, 9.5f, 24f), 58f),       // A5: the bell-loft annex's upper storey from the west, past the cistern house's gable
            ("Cap_HM_PX_AnnexE", new Vector3(24.0f, 1.65f, 8.0f), new Vector3(12f, 9.5f, 24f), 58f),       // A5: the same from the south-east
            ("Cap_HM_PX_Patch", new Vector3(10.0f, 1.65f, 2.0f), new Vector3(12.5f, 0.8f, 8.0f), 55f),    // A7: the ground in front of the cistern house's south gable, close
            ("Cap_HM_PX_Cottage", new Vector3(3.0f, 1.65f, 8.5f), new Vector3(8f, 2.3f, 11.5f), 55f),     // A5: the cistern house's west front (door, shutters, the bar over the window), like b1
            ("Cap_HM_PX_Ivy", new Vector3(-8f, 0.8f, 6f), new Vector3(0f, 3.5f, 19.6f), 55f),             // A4: low, from the square's north-west corner along the keep wall's ivy
            ("Cap_HM_PX_Weeds", new Vector3(-9f, 0.9f, -6f), new Vector3(3f, 0.8f, 12f), 62f),            // A3: low and wide over the open square, the weeds at the walls and kerbs
            ("Cap_HM_StairSide1", new Vector3(-41.2f, 0.8f, -33.5f), new Vector3(-45.6f, -5.0f, -31.0f), 60f),   // stairs seat: the Ring Lane south-west flight from its east side at 0.8 m (cheek, terrain meeting the treads)
            ("Cap_HM_StairSide3", new Vector3(-40.0f, 0.8f, -37.5f), new Vector3(-45.0f, -5.0f, -30.0f), 60f),   // stairs seat 2: the same flight's foot from the south-east at 0.8 m, looking up it (bottom step meeting the paving, stone under the treads) (stone mass under the treads, bottom step meeting the paving)
            ("Cap_HM_StairSide4", new Vector3(-41.0f, 0.8f, -29.0f), new Vector3(-45.5f, -5.3f, -31.5f), 60f),   // stairs seat 3: the Terrace / Ring Lane flight from its west side at 1 m (stone cores under the treads, contact strip at the foot)
            ("Cap_HM_StairSide2", new Vector3(24.5f, 0.8f, 11.5f), new Vector3(19.2f, 1.2f, 16.0f), 60f),        // stairs seat: the keep's east flight from the east side at 0.8 m
            ("Cap_HM_Overview", new Vector3(30f, 95f, -98f), new Vector3(0f, -5f, -22f), 42f),          // absolute height, rendered without fog (HollowmereCapture)
            ("Cap_HM_TerraceDown", new Vector3(-45.951f, -1.686f, -25.159f), new Vector3(-45.101f, -2.980f, -29.913f), 50f),   // play rig (pivot = feet + 1.5, boom 5.0, pitch 15 down, FOV 50), hero on the Ring Lane SW flight's middle tread facing downhill (LK0012/LK0013 LaneStep: X -44.068/-46.037, Y -4.640, Z -30.008/-30.360, yaw -10.13, tread centre +0.275 uphill, top Y+0.16); absolute position
            ("Cap_HM_TerraceUp", new Vector3(-44.251f, -1.686f, -34.668f), new Vector3(-45.101f, -2.980f, -29.913f), 50f),     // same tread, hero facing uphill (camera downhill of him); absolute position
            // junction survey (HollowmereJunctionScan): large pieces that do not meet. Pos y = height above ground, Target absolute; ~20 degrees down from 6 m.
            ("Cap_HM_J01", new Vector3(-21.78f, 3.68f, 13.65f), new Vector3(-20.95f, 1.50f, 19.59f), 55f),   // keep wall 1 runs through the west stair's core (KeepWall1 / Core16)
            ("Cap_HM_J02", new Vector3(38.88f, 3.86f, 10.84f), new Vector3(32.89f, -1.79f, 11.17f), 55f),   // NorthEastStub flight LK0185 foot: 0.9 m drop in front of the bottom tread
            ("Cap_HM_J03", new Vector3(-0.55f, 3.38f, -52.13f), new Vector3(-0.30f, -5.12f, -46.14f), 55f),   // Market Street flight LK0143 foot: 0.8 m drop in front of the bottom tread
            ("Cap_HM_J04", new Vector3(-22.98f, 6.68f, -43.56f), new Vector3(-22.58f, -3.20f, -37.57f), 55f),   // retaining walls LK0882/LK0799 cross at 60 degrees and overshoot
            ("Cap_HM_J05", new Vector3(-36.02f, 3.35f, -35.69f), new Vector3(-31.00f, -4.50f, -32.41f), 55f),   // retaining walls LK0816/LK0787 cross at 66 degrees
            ("Cap_HM_J06", new Vector3(-49.01f, 2.81f, 2.33f), new Vector3(-43.04f, -2.86f, 1.74f), 55f),   // RetainWallLow LK0753 through garden wall LK0486 beside the West Stub
            ("Cap_HM_J07", new Vector3(18.68f, 4.20f, -42.74f), new Vector3(18.38f, -4.47f, -36.75f), 55f),   // retaining walls LK0930/LK0676 cross at 87 degrees (T becomes a cross)
            ("Cap_HM_J08", new Vector3(-20.50f, 4.61f, -60.67f), new Vector3(-15.51f, -4.48f, -64.00f), 55f),   // town wall module M025 embedded 2.6 m in the Lower Gate shell (Ruined)
            ("Cap_HM_J09", new Vector3(13.50f, 3.80f, 15.50f), new Vector3(19.70f, 2.50f, 19.68f), 55f),   // keep wall end 5 against the east stair core 15
            ("Cap_HM_J10", new Vector3(-38.50f, 3.20f, -24.50f), new Vector3(-43.80f, -4.00f, -28.10f), 55f),   // retaining wall LK0797 into the Ring Lane SW flight's first treads
            ("Cap_HM_OverviewW", new Vector3(-110f, 45f, -20f), new Vector3(-30f, -3f, -20f), 48f),   // high oblique over the west lane and wall ring
            ("Cap_HM_OverviewE", new Vector3(100f, 45f, 35f), new Vector3(20f, -3f, -15f), 48f),     // high oblique over the east lane and wall ring and the keep terrace
            // Area 1 rework (Run 6): the eye-level set along the main walk, level gaze, 1.65 m. SC_CAPTURE_ONLY=Eye_ shoots just these; they are the cameras the rework is scored on.
            ("Cap_HM_Eye_Spawn", new Vector3(12.5f, 1.65f, 3.75f), new Vector3(-4f, 1.9f, -6f), 60f),        // the first frame: from the home door toward the fountain and the Market Street gap (DH8 yaw)
            ("Cap_HM_Eye_Postcard", new Vector3(0f, 1.65f, -19.5f), new Vector3(0f, 4.5f, 20f), 60f),       // Market Street mouth looking north: fountain, north row, terrace wall, church and tower (H1)
            ("Cap_HM_Eye_West", new Vector3(5f, 1.65f, -1f), new Vector3(-30f, 2.2f, -7f), 60f),            // from the fountain west over the ruined row and the West Lane mouth
            ("Cap_HM_Eye_Climb", new Vector3(22.5f, 1.65f, 8.5f), new Vector3(16.5f, 5.5f, 22f), 60f),      // Keep Lane toward the east stair, the terrace wall and the tower: the walk to the dive
            ("Cap_HM_Eye_BellYard", new Vector3(17f, 1.65f, 28.7f), new Vector3(2f, 5.4f, 29.2f), 60f),     // the keep walk looking west at the tower door, the trapdoor and the hill bank
            ("Cap_HM_Eye_Balcony", new Vector3(-2.25f, 1.65f, 20.7f), new Vector3(-1f, 0f, -30f), 60f),     // (between two parapet planters: one stands at x 0) the terrace edge over the whole square and Market Street to the south sky
            ("Cap_HM_Eye_Lake", new Vector3(-21.5f, 1.65f, -73f), new Vector3(6f, -9f, -100f), 60f),        // outside the Lower Gate, down the bank over the lake to the far shore
            // Area 1 world build (Run 7): the rest of the 20-camera eye set (docs/design/area1-world-build.md section 2) and two high views. For the cameras in EyeRelTarget the
            // Target y is an offset from the camera's own eye height (0 = level gaze), so the gaze follows the terrain; JettyBack's position is computed from the jetty.
            ("Cap_HM_Eye_Bakery", new Vector3(-6f, 1.65f, 3.5f), new Vector3(-14f, 1.5f, 12f), 60f),         // D1: the bakery front on F02 from the square
            ("Cap_HM_Eye_InnFront", new Vector3(16f, 1.65f, -13f), new Vector3(40f, 1.0f, -16.5f), 60f),     // D6: east along the inn row
            ("Cap_HM_Eye_HillWest", new Vector3(-42f, 1.65f, 17f), new Vector3(-28f, 3.0f, 36f), 60f),        // D3, D11: the west side of Bell Hill, looking north-north-east at the crag foot (T05: the old (-38, 23) view faced the keep's shaded retaining wall)
            ("Cap_HM_Eye_Churchyard", new Vector3(-9f, 1.65f, 21.5f), new Vector3(-21f, 0.5f, 28.5f), 60f),   // D3: on the keep terrace (y 4) looking west-north-west across the churchyard
            ("Cap_HM_Eye_MarketStreet", new Vector3(0f, 1.65f, -27f), new Vector3(0f, 0f, -50f), 60f),       // D5: down Market Street
            ("Cap_HM_Eye_LowerLane", new Vector3(-14.5f, 1.65f, -53f), new Vector3(-23f, 0f, -68f), 60f),    // D5: the lane to the Lower Gate
            ("Cap_HM_Eye_RingWest", new Vector3(-46f, 1.65f, 6f), new Vector3(-46f, 0f, -28f), 60f),         // D4: the west Ring Lane, south
            ("Cap_HM_Eye_RingSouth", new Vector3(-8f, 1.65f, -48.8f), new Vector3(22f, 0f, -48.5f), 60f),    // D4, D8: the south Ring Lane, east
            ("Cap_HM_Eye_Orchard", new Vector3(35f, 1.65f, -44.5f), new Vector3(43f, 0f, -58f), 60f),        // D7: the orchard
            ("Cap_HM_Eye_Mill", new Vector3(58f, 1.65f, -67f), new Vector3(70.5f, 2.5f, -58f), 60f),         // D9: the sunlit side of the mill with the stream in front (T05: the old Mill Lane view faced the mill's shadow wall)
            ("Cap_HM_Eye_Meadow", new Vector3(-8f, 1.65f, -72f), new Vector3(50f, 0f, -68f), 60f),           // D9: the lake meadow, east
            ("Cap_HM_Eye_JettyBack", new Vector3(0f, 0f, 0f), new Vector3(-5f, 0f, -40f), 60f),              // D9: from the jetty's end back at the town (position computed, Target y absolute 0)
            ("Cap_HM_Eye_RoadOut", new Vector3(-48f, 1.65f, -74f), new Vector3(-100f, 0.5f, -82f), 60f),     // D10: the Saltwake road, out
            ("Cap_HM_Eye_RoadBack", new Vector3(-85f, 1.65f, -80f), new Vector3(-20f, 0f, -55f), 60f),       // D10: the road, back at the town
            ("Cap_HM_High_Lake", new Vector3(10f, 38f, -128f), new Vector3(0f, 0f, -25f), 60f),              // absolute: high over the lake, the town behind
            ("Cap_HM_High_East", new Vector3(118f, 30f, -30f), new Vector3(0f, 2f, -20f), 60f),              // absolute: high east of the town, over the mill and the east wall
        };

        // Cameras whose Pos is an absolute world position (not ground + eye height).
        static readonly HashSet<string> AbsolutePosCameras = new HashSet<string> { "Cap_HM_Overview", "Cap_HM_OverviewW", "Cap_HM_OverviewE", "Cap_HM_TerraceDown", "Cap_HM_TerraceUp", "Cap_HM_High_Lake", "Cap_HM_High_East" };

        // Cameras whose Target y is an offset from the camera's own y.
        static readonly HashSet<string> EyeRelTarget = new HashSet<string> { "Cap_HM_Eye_Bakery", "Cap_HM_Eye_InnFront", "Cap_HM_Eye_HillWest", "Cap_HM_Eye_Churchyard", "Cap_HM_Eye_MarketStreet", "Cap_HM_Eye_LowerLane", "Cap_HM_Eye_RingWest", "Cap_HM_Eye_RingSouth",
            "Cap_HM_Eye_Orchard", "Cap_HM_Eye_Mill", "Cap_HM_Eye_Meadow", "Cap_HM_Eye_RoadOut", "Cap_HM_Eye_RoadBack" };

        [MenuItem("SleepingCalamity/Art/Build Hollowmere")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            RuinCache.Clear(); ruinFoliage = null;
            TownLayout layout = HollowmereLayout.Create();
            List<string> errors = layout.Validate();
            if (errors.Count > 0) throw new InvalidOperationException("Hollowmere layout invalid (" + errors.Count + "): " + string.Join("; ", errors.Take(20)));
            Debug.Log("SC HOLLOWMERE layout ok foundations=" + layout.Foundations.Count + " landmarks=" + layout.Landmarks.Count + " nodes=" + layout.Nodes.Count + " edges=" + layout.Edges.Count
                + " ring1=" + Mix(layout, 1) + " ring2=" + Mix(layout, 2) + " ring3=" + Mix(layout, 3));
            BeautyCornerBuilder.EnsureLook();
            BuildMaterials();
            BuildScene(layout);
            BeautyCornerBuilder.KeepDecalKeywords();   // the facade row rebuilt the facade materials, which strips the decal keyword: put it back last
        }

        static string Mix(TownLayout t, int ring) => t.Count(ring, FoundationSize.L) + "L/" + t.Count(ring, FoundationSize.M) + "M/" + t.Count(ring, FoundationSize.S) + "S";

        // --------------------------------------------------------------------------------------------- materials
        static void BuildMaterials()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Flat(lit, "meadow", TownGround.Meadow, 0.08f, 0f);    // sunlit grass, a little drier than the foliage green (palette: TownGround)
            Flat(lit, "water", TownGround.Water, 0.92f, 0f);     // deep lake green-blue, glossy (greybox, not the water shader)
            Flat(lit, "bed", TownGround.Soil, 0.05f, 0f);       // turned garden soil
            Flat(lit, "label", TownGround.Label, 0.2f, 0f);      // door markers on greybox blocks
            AssetDatabase.SaveAssets();
        }

        static void Flat(Shader lit, string key, Color c, float smooth, float metal)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_" + key + ".mat", lit);
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
        }

        static Material M(string key)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_HM_" + key + ".mat");
            return m != null ? m : BeautyCornerBuilder.PlaceholderMat(key);
        }

        // --------------------------------------------------------------------------------------------- scene
        static void BuildScene(TownLayout t)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Hollowmere").transform;
            HollowmereRuinDressing.Reset(); HollowmereRuinShells.Reset(); HollowmereRuinDressing.SetLayout(t); HollowmereWild.Reset();
            Transform set = Child(root, "Set");

            BuildGround(set, t);
            Transform ruined = Child(root, RuinedRoot), rebuilt = Child(root, RebuiltRoot);
            BuildWalls(set, t, ruined, rebuilt);
            BuildKeep(set, t);
            HollowmereLanes.Build(set, ruined, rebuilt, t);   // Q11 lane kit: terrace and lot retaining walls, steps, kerbs, gutters, garden walls, gates, lanterns

            Transform both = Child(set, "Corner");
            foreach (BeautyCornerTable.Row row in BeautyCornerTable.Rows)
            {
                if (row.Id.StartsWith("NEIGHBOUR_", StringComparison.Ordinal)) continue;
                if (row.Id == "PlazaCobbles") continue;   // B3 (DH4): the town square has one ground (HollowmereSquare); the corner's fan setts left an island round the fountain
                BeautyCornerTable.Row placed = StallFolder("Stall_1") != null ? WithoutStallDressing(row) : row;   // the produce stall art carries its own goods and lantern
                if (placed != null) BeautyCornerBuilder.PlaceRow(RebuiltOnlyRows.Contains(row.Id) ? Child(rebuilt, "Corner") : both, placed);
                if (row.Id == "FountainRing") DropRingBed(both);
            }
            ThinCornerWeeds(both, ruined, t);
            foreach (Foundation f in t.Foundations) { BuildFoundation(Child(ruined, "Foundations"), f, Preset.Ruined); BuildFoundation(Child(rebuilt, "Foundations"), f, Preset.Rebuilt); }
            HollowmereRuinDressing.LogSummary();
            {
                int nm = 0, nt = 0, nl = 0; foreach (var kv in HollowmereRuinShells.BuiltAll) { if (kv.Value == RuinClass.Mid) nm++; else if (kv.Value == RuinClass.Tall) nt++; else nl++; }
                int rRuin = 0, rCol = 0, rDress = 0, rRuined = 0, rShellCol = 0;
                foreach (Transform tr in rebuilt.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name.StartsWith("Ruin_", StringComparison.Ordinal)) rRuin++;
                    if (tr.name.StartsWith("FOOTPRINT_COL_", StringComparison.Ordinal)) rCol++;
                    if (tr.name.StartsWith("Dress", StringComparison.Ordinal)) rDress++;
                }
                foreach (Transform tr in ruined.GetComponentsInChildren<Transform>(true)) { if (tr.name.StartsWith("Ruin_", StringComparison.Ordinal)) rRuined++; if (tr.name.StartsWith("FOOTPRINT_COL_", StringComparison.Ordinal)) rShellCol++; }
                Debug.Log("SC HOLLOWMERE rebuilt check: under Preset_Rebuilt Ruin_* objects " + rRuin + " (low kit on empty lots), FOOTPRINT_COL_* " + rCol + ", Dress* " + rDress + "; under Preset_Ruined Ruin_* " + rRuined + ", FOOTPRINT_COL_* " + rShellCol);
                Debug.Log("SC HOLLOWMERE shells: Mid " + nm + " Tall " + nt + " Low " + nl + " (Ruined preset), shell colliders " + HollowmereRuinShells.Colliders + ", tallest point world y " + HollowmereRuinShells.Tallest.ToString("F2") + " at " + HollowmereRuinShells.TallestLot);
            }
            foreach (Landmark l in t.Landmarks) { BuildLandmark(Child(ruined, "Landmarks"), l, Preset.Ruined, t); BuildLandmark(Child(rebuilt, "Landmarks"), l, Preset.Rebuilt, t); }
            HollowmereSquare.Build(set, ruined, rebuilt, t);
            HollowmereClutter.Build(ruined, rebuilt, t);   // Q14: market and lane clutter, shop signs and washing lines
            HollowmereSurfaces.Build(set, ruined, rebuilt, t);   // town surfaces: square macro variation and contact AO under props (decals, no colliders)
            HollowmereFoliage.Place(ruined, rebuilt, t);   // B9 grass, weeds, moss and ivy on both presets (never inside the square)
            HollowmereRoad.Build(set, ruined, rebuilt, t);   // Q15: the Saltwake road's dressing, the field hedgerows and trees, the backdrop ring
            HollowmereWild.Build(set, ruined, rebuilt, t);   // area 1 rework: the great tree, town trees, the wall belt, Bell Hill's forest, square drift (after everything with a collider)
            BuildPathDebug(root, t);

            Transform lighting = Child(root, "Lighting");
            Light sun = LookTestBuilder.AddLight(lighting, "Sun", LightType.Directional, new Vector3(0, 10, 0), Quaternion.identity, Color.white, 3f, 10f, LightShadows.Soft);
            LookTestBuilder.AddLight(lighting, "ShadeFill", LightType.Directional, new Vector3(0, 10, 0), Quaternion.Euler(22f, 18f, 0f), new Color(0.78f, 0.84f, 1.0f), 0.5f, 10f, LightShadows.None);
            // Area 1 rework round 4: the bell yard (the walk to the dive) lies between the church's north wall and the crag, in shade all morning, and the wall read
            // black (p10 .05). A weak, shadowless, sky-coloured point light stands in for the light the sunlit crag and sky would bounce into the yard.
            LookTestBuilder.AddLight(lighting, "BellYardFill", LightType.Point, new Vector3(9f, HollowmereLayout.KeepHeight + 2.6f, 30.2f), Quaternion.identity, new Color(0.80f, 0.87f, 1.0f), 5f, 11f, LightShadows.None);
            LookTestBuilder.AddProbe(lighting, new Vector3(0f, 3f, 3f), new Vector3(30f, 12f, 28f));
            LookTestBuilder.AddVolume(lighting, BeautyCornerBuilder.ProfileName);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.sun = sun;

            HollowmereLook.BuildAtmosphere(root, t);   // Q12: town-wide pollen and god-ray shafts
            HollowmereAmbientBuilder.Build(root, t);   // phase-1 ambient life: birds, butterflies, bees, fish rings, reed sway, wind waves, chimney smoke (tuning: HollowmereAmbientLife)

            Transform cams = Child(root, "CaptureCameras");
            foreach (var c in BeautyCornerTable.Cameras) LookTestBuilder.AddCapture(cams, c.Name, c.Pos, c.Target, c.Fov);
            foreach (var c in Cameras)
            {
                Vector3 pos = AbsolutePosCameras.Contains(c.Name) ? c.Pos : new Vector3(c.Pos.x, Mathf.Max(HollowmereLayout.Ground(t, c.Pos.x, c.Pos.z), HollowmereLayout.WaterY) + c.Pos.y, c.Pos.z);   // never under the lake
                Vector3 target = c.Target;
                if (c.Name == "Cap_HM_Eye_JettyBack") pos = new Vector3(HollowmereLakeArt.JettyEnd.x, HollowmereLayout.WaterY + 0.6f + 1.65f, HollowmereLakeArt.JettyEnd.y);   // deck top + eye height at the jetty's end
                else if (EyeRelTarget.Contains(c.Name)) target.y += pos.y;
                Camera cam = LookTestBuilder.AddCapture(cams, c.Name, pos, target, c.Fov);
                cam.farClipPlane = c.Name.StartsWith("Cap_HM_Overview") || c.Name.StartsWith("Cap_HM_High") ? 400f : HollowmereRoad.FarClip;   // Q15: the low cameras see the backdrop ridges
                if (c.Name.StartsWith("Cap_HM_Eye_") || c.Name.StartsWith("Cap_HM_High"))
                {
                    Physics.SyncTransforms();
                    var near = new List<string>();
                    foreach (Collider col in Physics.OverlapSphere(pos, 1.5f))
                        if (!(col is TerrainCollider) && col.bounds.max.y > pos.y - 0.9f) near.Add(col.name + "@" + (col.transform.parent != null ? col.transform.parent.name : "-"));
                    string below = Physics.Raycast(pos + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 200f) ? " topHit " + hit.collider.name + " y " + hit.point.y.ToString("F2") : " topHit none";   // what the highest surface under the camera is (a hit above the camera = the camera is buried)
                    string gaze = Physics.Raycast(pos, (target - pos).normalized, out RaycastHit gh, 60f) ? " gaze " + gh.collider.name + "@" + (gh.collider.transform.parent != null ? gh.collider.transform.parent.name : "-") + " " + gh.distance.ToString("F1") + "m" : " gaze open";   // a hit under 3 m = the frame looks straight into a wall
                    Debug.Log("SC HOLLOWMERE cam " + c.Name + " pos " + pos.ToString("F2") + " target " + target.ToString("F2") + below + gaze + (near.Count > 0 ? " NEAR " + string.Join(",", near.Take(6)) : ""));
                }
            }

            foreach (var c in HollowmereRoad.Cameras)   // Q15: from the gate looking out, from the road looking back (Target absolute)
                LookTestBuilder.AddCapture(cams, c.Name, new Vector3(c.Pos.x, HollowmereLayout.Ground(t, c.Pos.x, c.Pos.z) + c.Pos.y, c.Pos.z), c.Target, c.Fov).farClipPlane = HollowmereRoad.FarClip;
            HollowmereGarden.AddCaptureCameras(cams, t);   // Q6: Cap_HM_Allotment, Cap_HM_Yard and the garden log line
            HollowmereAmbientBuilder.BakeAvoid(root);   // cards kept clear of every capture camera
            HollowmereLook.Apply(BeautyCornerBuilder.Preset.Morning);   // B3: the B0 Morning light; the corner's light with the town-scale haze, sky and grade (Q12)
            BuildHub(root, lighting, t);   // M6 S3: gameplay camera, spawn point, the cistern-stair DiveHub, the area look switcher (after Look.Apply: it finds the town's one Volume) and the TownView
            ApplyPreset(Preset.Ruined);
            GeneratedMeshes.Externalize(scene, "Hollowmere");   // T03 scene diet: generated meshes become assets instead of hex text in the scene file
            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC HOLLOWMERE scene built " + ScenePath);
        }

        // Shows one preset root and hides the other (the open scene).
        public static void ApplyPreset(Preset preset)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != "Hollowmere") continue;
                Transform r = go.transform.Find(RuinedRoot), b = go.transform.Find(RebuiltRoot);
                if (r == null || b == null) throw new InvalidOperationException("Hollowmere preset roots missing");
                r.gameObject.SetActive(preset == Preset.Ruined); b.gameObject.SetActive(preset == Preset.Rebuilt);
                return;
            }
            throw new InvalidOperationException("No Hollowmere root in the open scene");
        }

        // B3 polish: the FountainRing row also lays Art_RingBed (the corner's PlazaBed tileable, a different grain and tone from the square's coursed T01c ground), which showed as an
        // island round the fountain. The square bed under it is draped at the same height, so the ring bed goes; the flagstone ring itself stays.
        static void DropRingBed(Transform both)
        {
            Transform g = both.Find("FountainRing");
            if (g == null) return;
            var doomed = new List<GameObject>();
            foreach (Transform c in g.GetComponentsInChildren<Transform>(true)) if (c.name.StartsWith("Art_RingBed", StringComparison.Ordinal)) doomed.Add(c.gameObject);
            foreach (GameObject d in doomed) if (d != null) UnityEngine.Object.DestroyImmediate(d);
            Debug.Log("SC HOLLOWMERE B3 polish: " + doomed.Count + " Art_RingBed removed from the fountain ring");
        }

        // A3 round 2: the corner's CrackWeeds row is laid for the corner's own plaza (its "edge" is x +-6, z -4.5..6.2), so in the town it sprinkled tufts evenly over the open
        // cobbles of both presets. Keep the ones at the square's border or against a building (85%), a rare stray (7%) elsewhere; about 40% of the keepers stay in Rebuilt (a tended
        // town), the rest move under the Ruined root. Position-hashed, so two builds match.
        static void ThinCornerWeeds(Transform both, Transform ruined, TownLayout t)
        {
            Transform g = both.Find("CrackWeeds");
            if (g == null) return;
            if (g.childCount == 0) return;
            Transform holder = g.GetChild(0);   // BeautyCornerGround.Holder: the tufts are its children
            Transform ruinedOnly = Child(Child(ruined, "Corner"), "CrackWeeds");
            ruinedOnly.position = holder.position; ruinedOnly.rotation = holder.rotation; ruinedOnly.localScale = holder.lossyScale;
            var list = new List<Transform>();
            foreach (Transform c in holder) list.Add(c);
            int dropped = 0, moved = 0;
            Material crack = BeautyCornerArt.CrackWeedsMaterial();
            Material olive = crack != null ? HollowmereSquare.OliveTufts(crack) : null;   // the shared atlas reads lime on the town's stone: the square's olive copy
            foreach (Transform c in list)
            {
                Vector3 p = c.position; var tp = new TownPoint(p.x, p.z);
                float dEdge = Mathf.Min(Mathf.Min(p.x - HollowmereLayout.SquareMinX, HollowmereLayout.SquareMaxX - p.x), Mathf.Min(p.z - HollowmereLayout.SquareMinZ, HollowmereLayout.SquareMaxZ - p.z));
                bool atWall = dEdge < 2.2f;
                if (!atWall) foreach (Foundation f in t.Foundations) if (f.Footprint.Contains(tp, 1.8f)) { atWall = true; break; }
                if (!atWall) foreach (Landmark l in t.Landmarks) if (l.Footprint.Contains(tp, 1.8f)) { atWall = true; break; }
                uint h = unchecked((uint)Mathf.RoundToInt(p.x * 50f + 50000f) * 73856093u ^ (uint)Mathf.RoundToInt(p.z * 50f + 50000f) * 19349663u);
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
                float roll = (h % 1000u) / 1000f;
                if (roll >= (atWall ? 0.85f : 0f)) { UnityEngine.Object.DestroyImmediate(c.gameObject); dropped++; }   // B3 polish: no strays in the open middle (they read as lone lime tufts)
                else if (roll > 0.4f) { c.SetParent(ruinedOnly, true); moved++; }
                if (olive != null && c != null && roll < (atWall ? 0.85f : 0f))
                    foreach (Renderer rd in c.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                        for (int k = 0; k < mats.Length; k++) mats[k] = olive;
                        rd.sharedMaterials = mats;
                    }
            }
            Debug.Log("SC HOLLOWMERE corner weeds thinned: " + dropped + " removed, " + moved + " ruined-only, " + (list.Count - dropped - moved) + " in both presets");
        }

        static Transform Child(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // --------------------------------------------------------------------------------------------- ground, water, walls, keep
        static void BuildGround(Transform set, TownLayout t)
        {
            Terrain terrain = HollowmereTerrain.Build(set, t);
            HollowmereFoliage.TerrainGrass(terrain, t);   // a Unity Terrain from the layout heights (+ baked Terrain Tools erosion on the open ground)
            // The square keeps the approved corner's surround paving (B6 FarCobble with its macro breakup) as a slab over the terrain, so the corner reads as approved.
            Material far = HollowmereB3Ground.SquareGroundMaterial() ?? BeautyCornerArt.SurroundGround();   // B3: the slab shows wherever the irregular bed stops, so it takes the square's own ground
            var sq = LookTestBuilder.Box(set, "SquarePaving", new Vector3((HollowmereLayout.SquareMinX + HollowmereLayout.SquareMaxX) * 0.5f, -0.17f, (HollowmereLayout.SquareMinZ + HollowmereLayout.SquareMaxZ) * 0.5f),
                new Vector3(HollowmereLayout.SquareMaxX - HollowmereLayout.SquareMinX, 0.3f, HollowmereLayout.SquareMaxZ - HollowmereLayout.SquareMinZ),
                far != null ? far : LookTestBuilder.Mat("cobble"), default, far != null ? 2f : 3f);

            HollowmereWater.Build(set, t, terrain);   // lake + stepped stream pools + weir falls (SC_Water; HollowmereWater.cs)
        }

        static void BuildWalls(Transform set, TownLayout t, Transform ruined, Transform rebuilt)
        {
            // The town wall: the Q8 module kit per preset when its FBX are in (HollowmereTownWall), else today's greybox boxes under Set.
            var townRun = HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TownWallOffset, 16f), 0.5f);
            if (!HollowmereTownWall.TryBuild(ruined, rebuilt, t, townRun)) Wall(Child(set, "TownWall"), t, townRun, 2.4f, 1.0f);
            // The terrace retaining walls are the lane kit's (HollowmereLanes), not greybox boxes.
        }

        // A run of wall pieces standing on the ground: each piece reaches 1 m below the lowest ground on either side (1.5 m out) and `height` above the
        // highest, so a terrace wall retains its whole step and a free-standing wall gets its parapet height.
        static void Wall(Transform parent, TownLayout t, List<(TownPoint A, TownPoint B)> run, float height, float thick)
        {
            Material stone = TownSurface.Stone();
            int i = 0;
            foreach (var (a, b) in run)
            {
                TownPoint mid = (a + b) * 0.5f, dir = b - a;
                var nrm = new TownPoint(dir.Z / dir.Length, -dir.X / dir.Length);
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (TownPoint p in new[] { a, b, mid })
                    foreach (float side in new[] { -1.5f, 0f, 1.5f })
                    {
                        TownPoint q = p + nrm * side;
                        float y = HollowmereLayout.Ground(t, q.X, q.Z);
                        lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y);
                    }
                lo -= 1f; hi += height;
                float yaw = Mathf.Atan2(b.X - a.X, b.Z - a.Z) * Mathf.Rad2Deg;
                LookTestBuilder.Box(parent, "Wall" + (i++), new Vector3(mid.X, (lo + hi) * 0.5f, mid.Z), new Vector3(thick, hi - lo, TownPoint.Distance(a, b) + 0.05f), stone, new Vector3(0f, yaw, 0f), 2f);
            }
        }

        // The keep terrace's retaining wall with its parapet, broken up so it is not one blank face: buttresses every 6 m, ivy falling from planters on the
        // parapet, and the two stairs (Keep_StairFoot/Head from the square's north-east corner, Keep_WStairFoot/Head along the face from the Wall Ginnel).
        static void BuildKeep(Transform set, TownLayout t)
        {
            Transform keep = Child(set, "Keep");
            HollowmereKeepArt kit = HollowmereKeepArt.Load();
            Material stone = TownSurface.Stone(), green = TownSurface.Green();
            float kh = HollowmereLayout.KeepHeight, kz = HollowmereLayout.KeepWallZ;
            int i = 0;
            var run = HollowmereLayout.WallRun(t, HollowmereLayout.KeepWallLine(1f), 0.3f);
            int segments = 0, ends = 0, buttresses = 0, planters = 0;
            if (kit == null)
            {
                foreach (var (a, b) in run)
                {
                    TownPoint mid = (a + b) * 0.5f;
                    LookTestBuilder.Box(keep, "KeepWall" + (i++), new Vector3(mid.X, (kh + 0.9f - 0.6f) * 0.5f, mid.Z), new Vector3(TownPoint.Distance(a, b) + 0.02f, kh + 0.9f + 0.6f, 1.2f), stone, default, 2f);
                }
            }
            else
            {
                // Merge the 1 m pieces into continuous runs, tile each with 2 m wall segments, and cap the ends that face a gap (stairs, the tithe gate).
                var spans = new List<(float X0, float X1)>();
                foreach (var (a, b) in run)
                {
                    float x0 = Mathf.Min(a.X, b.X), x1 = Mathf.Max(a.X, b.X);
                    if (spans.Count > 0 && x0 <= spans[spans.Count - 1].X1 + 0.01f) spans[spans.Count - 1] = (spans[spans.Count - 1].X0, Mathf.Max(x1, spans[spans.Count - 1].X1));
                    else spans.Add((x0, x1));
                }
                foreach (var (x0, x1) in spans)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 2f));
                    float w = (x1 - x0) / n;
                    for (int k = 0; k < n; k++, segments++)
                        kit.Clone("WallSegment", keep, "KeepWall" + segments, new Vector3(x0 + (k + 0.5f) * w, 0f, kz), 0f, new Vector3(w / 2f, 1f, 1f));
                    float edge = HollowmereLayout.KeepHalfWidth - 0.01f;
                    if (x0 > -edge) { kit.Clone("WallEnd", keep, "KeepWallEnd" + ends++, new Vector3(x0 - 0.3f, 0f, kz), 0f, Vector3.one); }
                    if (x1 < edge) { kit.Clone("WallEnd", keep, "KeepWallEnd" + ends++, new Vector3(x1 + 0.3f, 0f, kz), 0f, Vector3.one); }
                }
            }
            for (float x = -HollowmereLayout.KeepHalfWidth + 3f; x < HollowmereLayout.KeepHalfWidth; x += 6f)
            {
                var p = new TownPoint(x, kz - 0.9f);
                if (!InRun(run, x)) continue;   // no buttress in a gap (stairs, the tithe gate)
                if (t.Foundations.Exists(f => f.Footprint.Contains(p, 0.6f))) continue;
                if (kit != null) { kit.Clone("Buttress", keep, "Buttress" + x, new Vector3(x, 0f, kz), 0f, Vector3.one); buttresses++; }
                else LookTestBuilder.Box(keep, "Buttress" + x, new Vector3(x, kh * 0.45f - 0.3f, kz - 0.95f), new Vector3(1.1f, kh * 0.9f + 0.6f, 0.7f), stone, new Vector3(-6f, 0f, 0f), 2f);
            }
            for (float x = -HollowmereLayout.KeepHalfWidth + 1.5f; x < HollowmereLayout.KeepHalfWidth; x += 4.5f)
            {
                if (!InRun(run, x) || !InRun(run, x - 1f) || !InRun(run, x + 1f)) continue;
                if (kit != null) { kit.Clone("Planter", keep, "Planter" + x, new Vector3(x, kh + 0.9f, kz), 0f, Vector3.one); planters++; }
                else LookTestBuilder.Box(keep, "Planter" + x, new Vector3(x, kh + 1.05f, kz + 0.2f), new Vector3(1.8f, 0.3f, 0.6f), green, default, 2f);
            }
            if (kit == null)
            {
                Stair(keep, "EastStair", t.Node("Keep_StairFoot"), t.Node("Keep_StairHead"), 2.2f, stone);
                Stair(keep, "WestStair", t.Node("Keep_WStairFoot"), t.Node("Keep_WStairHead"), 1.8f, stone);
                Debug.Log("SC HOLLOWMERE keep art=missing, greybox wall=" + run.Count + " pieces");
                return;
            }
            int steps = KitStair(kit, keep, "EastStair", t.Node("Keep_StairFoot"), t.Node("Keep_StairHead"), 2.2f, run, kz, t);
            steps += KitStair(kit, keep, "WestStair", t.Node("Keep_WStairFoot"), t.Node("Keep_WStairHead"), 1.8f, run, kz, t);
            // Benches on the terrace by the parapet, clear of the walk edges, the hall, the tower and the annex.
            int benches = 0;
            foreach (float bx in new[] { -15.5f, -11.5f, 22.0f })
            {
                var bp = new TownPoint(bx, kz + 1.4f);
                if (t.Landmarks.Exists(l => l.Footprint.Contains(bp, 0.8f))) continue;
                kit.Clone("Bench", keep, "Bench" + benches++, new Vector3(bx, kh, kz + 1.4f), 180f, Vector3.one);   // back to the terrace, facing the parapet's view
            }
            Debug.Log("SC HOLLOWMERE keep art=KeepTerrace wallSegments=" + segments + " ends=" + ends + " buttresses=" + buttresses + " planters=" + planters + " stairSteps=" + steps + " benches=" + benches);
        }

        // One flight from the kit: StairStep treads stacked foot to head (scaled to the width and the real rise and run), a stone core under them, raked balustrades on the
        // open sides (not where the retaining wall stands) and newels at the foot and the head. Returns the step count.
        static int KitStair(HollowmereKeepArt kit, Transform parent, string name, PathNode foot, PathNode head, float width, List<(TownPoint A, TownPoint B)> run, float kz, TownLayout t)
        {
            Transform g = Child(parent, name);
            TownPoint d = head.Pos - foot.Pos;
            float footY = Mathf.Min(foot.Y, HollowmereLayout.Ground(t, foot.Pos.X, foot.Pos.Z));   // the west flight's foot node is 0.3 m above the ground there: the first tread would be a 0.5 m step
            float len = d.Length, yaw = Mathf.Atan2(d.X, d.Z) * Mathf.Rad2Deg, rise = head.Y - footY;
            int n = Mathf.Max(6, Mathf.RoundToInt(rise / 0.222f));
            n = Mathf.Max(6, (n / 6) * 6);   // whole balustrade blocks of 6 steps
            float pitch = len / n, r = rise / n;
            var dir = new Vector3(d.X / len, 0f, d.Z / len);
            var side = new Vector3(dir.z, 0f, -dir.x);   // to the flight's right
            Material core = null;
            for (int s = 0; s < n; s++)
            {
                Vector3 start = new Vector3(foot.Pos.X, 0f, foot.Pos.Z) + dir * (pitch * s);
                float top = footY + r * (s + 1);
                kit.Clone("StairStep", g, "Step" + s, new Vector3(start.x, top - r, start.z), yaw, new Vector3(width / 2f, r / 0.222f, pitch / 0.367f));
                float bottom = footY - 0.3f;
                {   // stairs seat: carry the core down to the lowest ground under the flight's footprint (the west flight stands on a slope), 0.25 m buried
                    float low = float.MaxValue;
                    Vector3 sd2 = new Vector3(dir.z, 0f, -dir.x);
                    foreach (float lx in new[] { -0.5f, 0f, 0.5f }) foreach (float lz in new[] { 0f, 0.5f, 1f })
                    { Vector3 q = start + dir * (pitch * lz) + sd2 * (lx * (width + 0.4f)); low = Mathf.Min(low, HollowmereLayout.Ground(t, q.x, q.z)); }
                    bottom = Mathf.Min(bottom, low - 0.25f);
                }
                float h = top - r - bottom;
                if (h > 0.01f)
                {
                    var box = LookTestBuilder.Box(g, "Core" + s, start + dir * (pitch * 0.5f) + Vector3.up * (bottom + h * 0.5f), new Vector3(width - 0.02f, h, pitch + 0.02f),
                        core ?? (core = kit.CoreMaterial()), new Vector3(0f, yaw, 0f), 2f);
                    box.AddComponent<BoxCollider>().size = new Vector3(width - 0.02f, h, pitch + 0.02f);   // the solid under the treads
                }
            }
            float rake = kit.RakeSign();
            int blocks = n / 6;
            for (int k = 0; k < blocks; k++)
            {
                Vector3 mid = new Vector3(foot.Pos.X, 0f, foot.Pos.Z) + dir * (pitch * (6 * k + 3));
                float y = footY + r * (6 * k + 3.5f);
                foreach (float sd in new[] { -1f, 1f })
                {
                    Vector3 p = mid + side * (sd * (width * 0.5f - 0.12f));
                    if (InWall(run, p, kz)) continue;
                    p.y = y;
                    float by = yaw + (rake > 0f ? -90f : 90f);
                    kit.Clone("BalustradeRaked", g, "Rail" + k + (sd < 0 ? "L" : "R"), p, by, new Vector3(pitch / 0.367f, r / 0.222f, 1f));
                }
            }
            foreach (float sd in new[] { -1f, 1f })
            {
                Vector3 fp = new Vector3(foot.Pos.X, footY, foot.Pos.Z) + dir * 0.2f + side * (sd * (width * 0.5f - 0.12f));
                kit.Clone("Newel", g, "NewelFoot" + (sd < 0 ? "L" : "R"), fp, yaw, Vector3.one);
                Vector3 hp = new Vector3(head.Pos.X, head.Y, head.Pos.Z) - dir * 0.2f + side * (sd * (width * 0.5f - 0.12f));
                if (!InWall(run, hp, kz)) kit.Clone("Newel", g, "NewelHead" + (sd < 0 ? "L" : "R"), hp, yaw, Vector3.one);
            }
            return n;
        }

        // Inside the retaining wall's footprint (1.2 m thick on KeepWallZ, along a wall run, with a little margin).
        static bool InWall(List<(TownPoint A, TownPoint B)> run, Vector3 p, float kz) => Mathf.Abs(p.z - kz) < 0.9f && InRun(run, p.x);

        static bool InRun(List<(TownPoint A, TownPoint B)> run, float x)
        {
            foreach (var (a, b) in run) if (x >= Mathf.Min(a.X, b.X) - 0.01f && x <= Mathf.Max(a.X, b.X) + 0.01f) return true;
            return false;
        }

        // A straight flight from foot to head: solid steps rising from the foot's height to the head's, run along the line between them.
        static void Stair(Transform parent, string name, PathNode foot, PathNode head, float width, Material stone)
        {
            Transform g = Child(parent, name);
            TownPoint d = head.Pos - foot.Pos;
            float len = d.Length, yaw = Mathf.Atan2(d.X, d.Z) * Mathf.Rad2Deg, rise = head.Y - foot.Y;
            int steps = Mathf.Max(4, Mathf.RoundToInt(Mathf.Abs(rise) / 0.22f));
            for (int s = 0; s < steps; s++)
            {
                float f = (s + 0.5f) / steps, top = foot.Y + rise * (s + 1) / steps, bottom = Mathf.Min(foot.Y, head.Y) - 0.3f;
                TownPoint c = foot.Pos + d * f;
                LookTestBuilder.Box(g, "Step" + s, new Vector3(c.X, (top + bottom) * 0.5f, c.Z), new Vector3(width, top - bottom, len / steps + 0.02f), stone, new Vector3(0f, yaw, 0f), 2f);
            }
        }

        // --------------------------------------------------------------------------------------------- foundations
        // A stable 0..1 number per lot id and salt, for the greybox variation (block size, eave, roof pitch).
        static float Rand(string id, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                h ^= (uint)salt * 0x9E3779B9u; h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static void BuildFoundation(Transform parent, Foundation f, Preset preset)
        {
            // The lot's facing frame: local -Z is the default door side (Foundation.FacingYaw), origin at the lot centre on its pad.
            var go = new GameObject(f.Id + "_" + f.Size + (preset == Preset.Ruined ? "_" + f.RuinVariant : ""));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(f.Centre.X, f.Y, f.Centre.Z);
            go.transform.localRotation = Quaternion.Euler(0f, f.FacingYaw, 0f);
            Transform g = go.transform;
            float side = f.Side, half = side * 0.5f;
            Material stone = TownSurface.Stone();
            HollowmereLayout.BuildingKind kind = preset == Preset.Ruined ? HollowmereLayout.BuildingKind.Empty : HollowmereLayout.RebuiltBuilding(f.Id);

            // An empty lot shows its ruin (the ruin kit, or the greybox stubs when the art is missing); a placed building or garden switches the ruin off
            // (design 5.3), so it stands on a plain footing slab instead.
            bool ruin = kind == HollowmereLayout.BuildingKind.Empty && PlaceRuin(go.transform, f, preset);
            if (ruin && preset == Preset.Ruined) HollowmereRuinDressing.Dress(go.transform, f);   // B3 stage 0: ivy, saplings, brambles, debris, a crow (DH1)
            if (kind != HollowmereLayout.BuildingKind.ApprovedFacade && !ruin)   // the facade brings its own plinth; a slab would add a ledge in front of the approved look
                LookTestBuilder.Box(g, "Footing", new Vector3(0f, 0.05f, 0f), new Vector3(side, 0.4f, side), stone, default, 2f);
            switch (kind)
            {
                case HollowmereLayout.BuildingKind.Empty:
                    if (ruin) break;
                    LookTestBuilder.Box(g, "Doorstep", new Vector3(0f, 0.11f, -half - 0.45f), new Vector3(1.6f, 0.22f, 0.9f), stone, default, 2f);
                    RuinStubs(g, f, stone);
                    break;
                case HollowmereLayout.BuildingKind.ApprovedFacade:
                    break;   // the corner's Facade row stands here (Corner group of the rebuilt root)
                case HollowmereLayout.BuildingKind.Garden:
                    Garden(g, f);
                    break;
                case HollowmereLayout.BuildingKind.Townhouse:   // kit family 3 art (HollowmereTownhouse); greybox home if the FBX is missing
                    if (!HollowmereTownhouse.Place(g, f)) Building(g, f, HollowmereLayout.BuildingKind.Home);
                    break;
                case HollowmereLayout.BuildingKind.StoneCottage:   // kit family 2 art (HollowmereStoneCottage); greybox home if the FBX is missing
                    if (!HollowmereStoneCottage.Place(g, f)) Building(g, f, HollowmereLayout.BuildingKind.Home);
                    break;
                case HollowmereLayout.BuildingKind.Workshop:   // kit family 4 art (HollowmereWorkshop); greybox home if the FBX is missing
                    if (!HollowmereWorkshop.Place(g, f)) Building(g, f, HollowmereLayout.BuildingKind.Home);
                    break;
                case HollowmereLayout.BuildingKind.Bakery when HollowmereArt.TryPlaceBakery(g, f, 0.25f):   // on the footing slab's top
                    break;
                case HollowmereLayout.BuildingKind.Inn when HollowmereInnArt.TryPlaceInn(g, f, 0.25f):   // on the footing slab's top
                    break;
                case HollowmereLayout.BuildingKind.Smithy when HollowmereArt.TryPlaceBuilding(HollowmereArt.Smithy, g, f, 0.25f):
                    break;
                case HollowmereLayout.BuildingKind.Schoolhouse when HollowmereArt.TryPlaceBuilding(HollowmereArt.Schoolhouse, g, f, 0.25f):
                    break;
                default:
                    Building(g, f, kind);
                    HollowmereGarden.Yard(g, f, kind);   // Q6: a bed, a water butt and a back fence on deep-yard ring 2 homes
                    break;
            }
            SolidLotMasses(g);
        }

        // The lot's own masses were drawn with no collider (footing slab, ruin kit footing and wall stubs, greybox plinth/walls/gables/oven/chimney, rubble): the hero walked
        // through them. A kit mesh gets a MeshCollider, a generated box a BoxCollider of its size. The doorstep slab, the foliage and the building art (own colliders) are left out.
        static void SolidLotMasses(Transform g)
        {
            foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.name;
                bool solid = n == "Footing" || n == "Plinth" || n == "Walls" || n == "OvenBay" || n == "Chimney" || n.StartsWith("Stub_", StringComparison.Ordinal)
                    || n.StartsWith("Rubble", StringComparison.Ordinal) || n.StartsWith("Gable", StringComparison.Ordinal);
                if (!solid || mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                bool inArt = false;
                for (Transform t = mf.transform.parent; t != null && t != g; t = t.parent) if (t.name.StartsWith("Art_", StringComparison.Ordinal)) inArt = true;
                if (inArt) continue;
                if (mf.GetComponent<SleepingCalamity.Presentation.LookTestGeometry>() != null)
                {
                    var box = mf.gameObject.AddComponent<BoxCollider>(); box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size;
                }
                else if (mf.transform.parent != null && mf.transform.parent.Find("FOOTPRINT_COL_0") != null)
                {
                    // A T05 shell: its FOOTPRINT_COL_<n> boxes are the collision. The Footing mesh (rubble spilled up to 1.3 m past the footprint) as a MeshCollider closed the
                    // 0.8 m corridor between the lot and the lane's retaining wall (R2_01_North and R2_03_North became unreachable); the boxes stay inside the footprint.
                    if (n == "Footing" && mf.transform.parent.name.StartsWith("Ruin_", StringComparison.Ordinal))
                        HollowmereRuinSeal.Seal(g, mf);
                }
                else
                {
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    if (n == "Footing" && mf.transform.parent != null && mf.transform.parent.name.StartsWith("Ruin_", StringComparison.Ordinal))
                        HollowmereRuinSeal.Seal(g, mf);   // slots between the ruin's wall stubs that look passable but are narrower than the hero
                }
            }
        }

        // The lot's pad against the natural slope (retaining walls on cut sides, fill walls on built-up sides) is the lane kit's (HollowmereLanes, LaneKitPlan.LotSideHeights).

        // ------------------------------------------------------------------------------------------------ ruin kit (Content/Art/Town/Ruins, Reference/hollowmere/ruins)
        // Contract: one FBX per Ruin_<S|M|L>_<A|B|C>, pivot at the lot centre on the ground, front (doorstep) toward local +Z, children Footing (slots
        // FK_Sandstone / FK_ChimneyStone / FK_PlinthJoint / FK_Oak, the B4 facade kit's names, WearLit), Foliage (M_B9_Foliage), FOOTPRINT (hidden) and
        // SOCKET_Doorstep. Placed on the pad at the lot's RuinVariant and RuinTurn (layout data), in the lot's own frame.
        const string RuinsFolder = "Assets/_Project/Content/Art/Town/Ruins";
        sealed class RuinArt { public GameObject Fbx; public Dictionary<string, Material> Slots; }
        static readonly Dictionary<string, RuinArt> RuinCache = new Dictionary<string, RuinArt>();

        // name: "Ruin_S_A" (the low kit) or "Ruin_S_A_Mid" / "Ruin_S_A_Tall" (the T05 shells); the FBX sits in the base variant's folder.
        static RuinArt LoadRuin(string name)
        {
            if (RuinCache.TryGetValue(name, out RuinArt cached)) return cached;
            RuinArt art = null;
            string baseVariant = name.Length > 8 ? name.Substring(0, 8) : name;
            string folder = RuinsFolder + "/" + baseVariant;
            GameObject fbx = AssetDatabase.IsValidFolder(folder) ? BeautyCornerArt.LoadModel(folder, name + ".fbx", keepMaterialSlots: true) : null;
            if (fbx != null)
            {
                var slots = new Dictionary<string, Material>(BeautyCornerArt.BuildFacadeMaterials());
                slots["M_B9_Foliage"] = RuinFoliage();
                art = new RuinArt { Fbx = fbx, Slots = slots };
            }
            Debug.Log("SC HOLLOWMERE ruin " + name + " art=" + (art != null ? folder + "/" + name + ".fbx" : "missing, greybox stubs"));
            RuinCache[name] = art;
            return art;
        }

        // The ruin folders hold byte-identical copies of the B9 atlas (different GUIDs): build the one shared material from a fixed folder, so it does not
        // flip between copies with the order the variants load in.
        static Material ruinFoliage;
        static Material RuinFoliage()
        {
            if (ruinFoliage == null) ruinFoliage = BeautyCornerArt.MaterialFromSet(RuinsFolder + "/Ruin_S_A", "T_B9_Foliage", "HM_RuinFoliage", foliage: true);
            return ruinFoliage;
        }

        static bool PlaceRuin(Transform lot, Foundation f, Preset preset)
        {
            // T05: the Ruined preset puts a Mid or Tall shell on the lots the height table names; Rebuilt keeps the low kit on its few empty lots. A missing shell file
            // falls back to the low ruin with a warning.
            RuinClass cls = preset == Preset.Ruined ? HollowmereRuinShells.Planned(f.Id) : RuinClass.Low;
            RuinArt art = null;
            if (cls != RuinClass.Low)
            {
                art = LoadRuin(HollowmereRuinShells.FileName(f.RuinVariant, cls));
                if (art == null) { Debug.LogWarning("SC HOLLOWMERE ruin shell " + HollowmereRuinShells.FileName(f.RuinVariant, cls) + " missing for lot " + f.Id + ": low ruin instead"); cls = RuinClass.Low; }
            }
            if (art == null) art = LoadRuin(f.RuinVariant);
            if (art == null) return false;
            if (preset == Preset.Ruined) HollowmereRuinShells.Record(f.Id, cls);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(art.Fbx, lot);
            obj.name = "Ruin_" + f.RuinVariant.Substring(5) + "_T" + f.RuinTurn;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.rotation = Quaternion.Euler(0f, f.Yaw + 90f * f.RuinTurn, 0f);
            if (cls != RuinClass.Low)
            {
                PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);   // the collision stand-ins lose their mesh components
                // 5% tighter in plan: a shell's perimeter wall stands on the footprint edge, which left 1.18 m between R2_01's east wall and the lane's retaining wall and cut the
                // pocket's doorstep off at the hero's reach; 0.98 still failed, 0.96 passed (traversal probe), 0.95 keeps a margin. Heights are unchanged.
                obj.transform.localScale = new Vector3(0.95f, 1f, 0.95f);
                HollowmereRuinShells.MakeColliders(obj.transform);
            }
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("FOOTPRINT", StringComparison.Ordinal)) { r.enabled = false; continue; }   // the collider / NPC-block shape, never drawn
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!art.Slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE ruin: no material for slot '" + slot + "' on " + r.name); mats[k] = TownSurface.Stone(); }
                }
                r.sharedMaterials = mats;
            }
            if (cls != RuinClass.Low) HollowmereRuinShells.Measure(f.Id, cls, obj.transform, lot.position.y);
            return true;
        }

        // The ruined lot by its variant (greybox, used when the ruin kit art is missing): a = back wall stub and a side stub; b = a broken back corner and rubble; c = broken front wall each side of the doorway and rubble.
        static void RuinStubs(Transform g, Foundation f, Material stone)
        {
            float half = f.Side * 0.5f, inset = half - 0.35f;
            char v = f.RuinVariant[f.RuinVariant.Length - 1];
            if (v == 'a')
            {
                LookTestBuilder.Box(g, "Stub_Back", new Vector3(-0.15f * f.Side, 0.25f + 0.55f, inset), new Vector3(f.Side * 0.65f, 1.1f, 0.5f), stone, default, 2f);
                LookTestBuilder.Box(g, "Stub_Side", new Vector3(-inset, 0.25f + 0.3f, 0.2f * f.Side), new Vector3(0.5f, 0.6f, f.Side * 0.5f), stone, default, 2f);
            }
            else if (v == 'b')
            {
                LookTestBuilder.Box(g, "Stub_CornerA", new Vector3(inset - 0.9f * f.Side * 0.25f, 0.25f + 0.8f, inset), new Vector3(f.Side * 0.45f, 1.6f, 0.5f), stone, default, 2f);
                LookTestBuilder.Box(g, "Stub_CornerB", new Vector3(inset, 0.25f + 0.55f, inset - f.Side * 0.18f), new Vector3(0.5f, 1.1f, f.Side * 0.36f), stone, default, 2f);
            }
            else
            {
                float w = f.Side * 0.3f;
                LookTestBuilder.Box(g, "Stub_FrontL", new Vector3(-half + 0.35f + w * 0.5f, 0.25f + 0.4f, -inset), new Vector3(w, 0.8f, 0.5f), stone, default, 2f);
                LookTestBuilder.Box(g, "Stub_FrontR", new Vector3(half - 0.35f - w * 0.5f, 0.25f + 0.3f, -inset), new Vector3(w, 0.6f, 0.5f), stone, default, 2f);
            }
            int n = v == 'a' ? 2 : 4;
            for (int i = 0; i < n; i++)
            {
                float x = (Rand(f.Id, 10 + i) - 0.5f) * f.Side * 0.6f, z = (Rand(f.Id, 20 + i) - 0.3f) * f.Side * 0.5f, s = 0.35f + Rand(f.Id, 30 + i) * 0.45f;
                LookTestBuilder.Box(g, "Rubble" + i, new Vector3(x, 0.25f + s * 0.4f, z), new Vector3(s, s * 0.8f, s * 1.2f), stone, new Vector3(Rand(f.Id, 40 + i) * 25f, Rand(f.Id, 50 + i) * 180f, 0f), 2f);
            }
        }

        static void Garden(Transform g, Foundation f)
        {
            if (HollowmereGarden.Allotment(g, f)) return;   // Q6 art (Garden/); the greybox below is the fallback
            float half = f.Side * 0.5f;
            Material oak = TownSurface.Timber();
            for (int i = 0; i < 3; i++)
                LookTestBuilder.Box(g, "Bed" + i, new Vector3(0f, 0.32f, -half + 1.2f + i * (f.Side - 2f) / 3f + 0.5f), new Vector3(f.Side - 1.6f, 0.15f, 1.1f), M("bed"), default, 2f);
            LookTestBuilder.Box(g, "FenceBack", new Vector3(0f, 0.75f, half - 0.15f), new Vector3(f.Side, 0.9f, 0.08f), oak, default, 2f);
            LookTestBuilder.Box(g, "FenceL", new Vector3(-half + 0.15f, 0.75f, 0f), new Vector3(0.08f, 0.9f, f.Side), oak, default, 2f);
            LookTestBuilder.Box(g, "FenceR", new Vector3(half - 0.15f, 0.75f, 0f), new Vector3(0.08f, 0.9f, f.Side), oak, default, 2f);
        }

        // A building-sized greybox: plinth, walls, gable roof, a door marker on the facing side (local -Z). Bakery adds an oven bay and chimney, the inn a porch;
        // both get a sign bracket. Sizes stay inside Foundation.MaxBuilding for the class.
        static void Building(Transform g, Foundation f, HollowmereLayout.BuildingKind kind)
        {
            float max = Foundation.MaxBuilding(f.Size);
            // Bakery and inn leave room inside the body limit for their oven bay (side) and porch (front).
            float w = max * (kind == HollowmereLayout.BuildingKind.Home ? 0.82f + 0.16f * Rand(f.Id, 1) : kind == HollowmereLayout.BuildingKind.Bakery ? 0.74f : 0.95f);
            float d = max * (kind == HollowmereLayout.BuildingKind.Home ? 0.72f + 0.22f * Rand(f.Id, 2) : kind == HollowmereLayout.BuildingKind.Inn ? 0.61f : 0.82f);
            float eave = (f.Size == FoundationSize.S ? 3.6f : f.Size == FoundationSize.M ? 5.6f : 6.2f) + 0.8f * Rand(f.Id, 3);
            if (kind == HollowmereLayout.BuildingKind.Inn) eave = 8.4f;
            float pitch = 36f + 10f * Rand(f.Id, 4);
            float zc = kind == HollowmereLayout.BuildingKind.Inn ? max * 0.5f - d * 0.5f : -(max - d) * 0.35f;   // the block sits toward the door side, the yard behind it; the inn sits back behind its porch
            const float plinth = 0.55f;
            Material plaster = LookTestBuilder.Mat("plaster"), roof = LookTestBuilder.Mat("roof_tile"), stone = TownSurface.Stone(), wood = LookTestBuilder.Mat("painted_wood"), oak = TownSurface.Timber();

            LookTestBuilder.Box(g, "Plinth", new Vector3(0f, 0.25f + plinth * 0.5f, zc), new Vector3(w + 0.2f, plinth, d + 0.2f), stone, default, 2f);
            LookTestBuilder.Box(g, "Walls", new Vector3(0f, 0.25f + plinth + (eave - plinth) * 0.5f, zc), new Vector3(w, eave - plinth, d), plaster, default, 2f);
            float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * d * 0.5f, slope = (d * 0.5f + 0.35f) / Mathf.Cos(pitch * Mathf.Deg2Rad);
            float ry = 0.25f + eave + rise * 0.5f;
            LookTestBuilder.Box(g, "RoofFront", new Vector3(0f, ry, zc - d * 0.25f), new Vector3(w + 0.6f, 0.25f, slope), roof, new Vector3(-pitch, 0f, 0f), 2f);
            LookTestBuilder.Box(g, "RoofBack", new Vector3(0f, ry, zc + d * 0.25f), new Vector3(w + 0.6f, 0.25f, slope), roof, new Vector3(pitch, 0f, 0f), 2f);
            for (int k = -1; k <= 1; k += 2)   // gable ends: stacked plaster slabs under the roof
                for (int s = 0; s < 3; s++)
                {
                    float frac = (s + 0.5f) / 3f, ww = 0.2f + 0.8f * (1f - frac) * 0.98f;
                    LookTestBuilder.Box(g, "Gable" + (k < 0 ? "L" : "R") + s, new Vector3(k * (w * 0.5f - 0.1f), 0.25f + eave + rise * (s / 3f) + rise / 6f, zc), new Vector3(0.2f, rise / 3f, d * ww), plaster, default, 2f);
                }
            float front = zc - d * 0.5f;
            LookTestBuilder.Box(g, "Door", new Vector3(0f, 0.25f + plinth + 1.1f, front - 0.06f), new Vector3(1.3f, 2.2f, 0.12f), wood, default, 2f);
            LookTestBuilder.Box(g, "DoorMarker", new Vector3(0f, 0.27f, front - 0.6f), new Vector3(1.3f, 0.04f, 0.8f), M("label"), default, 2f);
            for (int k = -1; k <= 1; k += 2)
                if (w > 5f) LookTestBuilder.Box(g, "Shutter" + k, new Vector3(k * w * 0.3f, 0.25f + plinth + 1.5f, front - 0.05f), new Vector3(0.9f, 1.1f, 0.1f), wood, default, 2f);
            if (kind == HollowmereLayout.BuildingKind.Bakery)
            {
                LookTestBuilder.Box(g, "OvenBay", new Vector3(w * 0.5f + 0.6f, 0.25f + 1.4f, zc + d * 0.15f), new Vector3(1.2f, 2.8f, 2.6f), stone, default, 2f);
                LookTestBuilder.Box(g, "Chimney", new Vector3(w * 0.32f, 0.25f + eave + rise + 0.6f, zc + d * 0.2f), new Vector3(1.0f, 2.6f, 1.0f), stone, default, 2f);
            }
            if (kind == HollowmereLayout.BuildingKind.Inn)
            {
                LookTestBuilder.Box(g, "PorchRoof", new Vector3(0f, 0.25f + 3.2f, front - 1.1f), new Vector3(4f, 0.18f, 2.2f), roof, new Vector3(-12f, 0f, 0f), 2f);
                LookTestBuilder.Box(g, "PorchPostL", new Vector3(-1.8f, 0.25f + 1.55f, front - 2.0f), new Vector3(0.2f, 3.1f, 0.2f), oak, default, 2f);
                LookTestBuilder.Box(g, "PorchPostR", new Vector3(1.8f, 0.25f + 1.55f, front - 2.0f), new Vector3(0.2f, 3.1f, 0.2f), oak, default, 2f);
            }
            if (kind == HollowmereLayout.BuildingKind.Bakery || kind == HollowmereLayout.BuildingKind.Inn)
            {
                LookTestBuilder.Box(g, "SignArm", new Vector3(w * 0.3f, 0.25f + 3.4f, front - 0.5f), new Vector3(0.08f, 0.08f, 1.0f), oak, default, 2f);
                LookTestBuilder.Box(g, "Sign", new Vector3(w * 0.3f, 0.25f + 2.95f, front - 0.75f), new Vector3(0.06f, 0.75f, 0.6f), wood, default, 2f);
            }
            else if (Rand(f.Id, 5) > 0.45f)
                LookTestBuilder.Box(g, "Chimney", new Vector3(-w * 0.3f, 0.25f + eave + rise * 0.7f, zc + d * 0.15f), new Vector3(0.7f, 2.0f, 0.7f), stone, default, 2f);
        }

        // --------------------------------------------------------------------------------------------- landmarks
        // The landmark's door node in the landmark frame g (NaN when it has none).
        static Vector3 DoorLocal(Transform g, TownLayout t, Landmark l)
        {
            PathNode n = t.Node(l.Id + "_Door");
            return n == null ? new Vector3(float.NaN, float.NaN, float.NaN) : g.InverseTransformPoint(new Vector3(n.Pos.X, l.Y, n.Pos.Z));
        }

        static void BuildLandmark(Transform parent, Landmark l, Preset preset, TownLayout t)
        {
            var go = new GameObject(l.Id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(l.Footprint.Centre.X, l.Y, l.Footprint.Centre.Z);
            go.transform.localRotation = Quaternion.Euler(0f, l.Footprint.Yaw, 0f);
            Transform g = go.transform;
            float sx = l.Footprint.HalfX * 2f, sz = l.Footprint.HalfZ * 2f;
            Material stone = TownSurface.Stone(), roof = LookTestBuilder.Mat("roof_tile"), oak = TownSurface.Timber(), iron = LookTestBuilder.Mat("iron"), wood = LookTestBuilder.Mat("painted_wood");
            bool rebuilt = preset == Preset.Rebuilt;
            switch (l.Kind)
            {
                case LandmarkKind.Fountain:
                    break;   // the corner's DryFountain row (B10) stands at the origin in both presets
                case LandmarkKind.Stall:
                    if (StallArt(g, l.Id, rebuilt)) break;
                    if (rebuilt && l.Id == "Stall_1") break;   // the corner's MarketStall row
                    if (rebuilt) { StallArt(g); break; }
                    // Ruined: a bare frame, counter and corner posts, no awning (design 3).
                    LookTestBuilder.Box(g, "Counter", new Vector3(0f, 0.45f, 0.45f), new Vector3(2.6f, 0.9f, 0.7f), wood, default, 2f);
                    foreach (float x in new[] { -1.25f, 1.25f }) foreach (float z in new[] { -0.75f, 0.7f })
                            LookTestBuilder.Box(g, "Post", new Vector3(x, 1.25f, z), new Vector3(0.1f, 2.5f, 0.1f), oak, default, 2f);
                    LookTestBuilder.Box(g, "TornAwning", new Vector3(-0.6f, 2.5f, 0f), new Vector3(1.2f, 0.04f, 1.6f), LookTestBuilder.Mat("canvas"), new Vector3(10f, 0f, 25f), 2f);
                    break;
                case LandmarkKind.BellTower:
                    BellTower(g, l, rebuilt);
                    break;
                case LandmarkKind.KeepHall:
                    if (HollowmereArt.TryPlaceKeepHall(g, rebuilt)) break;   // Q9 art (sc_keep_hall.py): Shell when ruined, Frame + door when rebuilt
                    LookTestBuilder.Box(g, "Walls", new Vector3(0f, 3f, 0f), new Vector3(sx, 6f, sz), stone, default, 2f);
                    LookTestBuilder.Box(g, "RoofS", new Vector3(0f, 7.6f, -sz * 0.25f), new Vector3(sx + 0.6f, 0.25f, sz * 0.62f), roof, new Vector3(-38f, 0f, 0f), 2f);
                    LookTestBuilder.Box(g, "RoofN", new Vector3(0f, 7.6f, sz * 0.25f), new Vector3(sx + 0.6f, 0.25f, sz * 0.62f), roof, new Vector3(38f, 0f, 0f), 2f);
                    LookTestBuilder.Box(g, "Door", new Vector3(-sx * 0.5f - 0.06f, 1.1f, 0.5f), new Vector3(0.12f, 2.2f, 1.3f), wood, default, 2f);
                    break;
                case LandmarkKind.TitheGate:
                    if (HollowmereTitheGate.TryPlace(g)) break;   // Wave 2 art (sc_tithe_gate.py) at the row's height; the greybox below stays as the fallback
                    foreach (float x in new[] { -2.3f, 2.3f }) LookTestBuilder.Box(g, "Pier", new Vector3(x, 2.4f, 0f), new Vector3(1.0f, 5.4f, 1.6f), stone, default, 2f);
                    LookTestBuilder.Box(g, "Lintel", new Vector3(0f, 4.6f, 0f), new Vector3(5.6f, 1.0f, 1.6f), stone, default, 2f);
                    LookTestBuilder.Box(g, "Glow", new Vector3(0f, 2.0f, 0.5f), new Vector3(3.6f, 4.0f, 0.05f), BeautyCornerBuilder.PlaceholderMat("warm"), default, 2f);
                    for (int i = 0; i < 9; i++) LookTestBuilder.Box(g, "Bar" + i, new Vector3(-1.8f + i * 0.45f, 2.0f, 0f), new Vector3(0.09f, 4.0f, 0.09f), iron, default, 2f);
                    LookTestBuilder.Box(g, "Rail", new Vector3(0f, 2.6f, 0f), new Vector3(3.7f, 0.12f, 0.12f), iron, new Vector3(0f, 0f, 4f), 2f);   // cracked: the crossbar hangs askew
                    break;
                case LandmarkKind.HeroesHome:
                    if (HeroesHomeArt.Place(g, l.Id, DoorLocal(g, t, l)))
                    {
                        if (l.Id == "HeroesHome_Cistern")
                        {
                            var stairA = new GameObject("UndercroftStair");   // P25: the way down; a marker for M5/M6 (inside the house)
                            stairA.transform.SetParent(g, false);
                            stairA.transform.position = new Vector3(HollowmereLayout.UndercroftStair.X, l.Y, HollowmereLayout.UndercroftStair.Z);
                            HeroesHomeArt.AddStairLights(g, stairA.transform);
                        }
                    }
                    else if (l.Id == "HeroesHome_Cistern")
                    {
                        LookTestBuilder.Box(g, "Walls", new Vector3(0f, l.Height * 0.5f, 0f), new Vector3(sx, l.Height, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "RoofS", new Vector3(0f, l.Height + 1.0f, -sz * 0.25f), new Vector3(sx + 0.5f, 0.25f, sz * 0.62f), roof, new Vector3(-30f, 0f, 0f), 2f);
                        LookTestBuilder.Box(g, "RoofN", new Vector3(0f, l.Height + 1.0f, sz * 0.25f), new Vector3(sx + 0.5f, 0.25f, sz * 0.62f), roof, new Vector3(30f, 0f, 0f), 2f);
                        LookTestBuilder.Prim(g, "CisternDrum", PrimitiveType.Cylinder, new Vector3(sx * 0.32f, (l.Height + 1.5f) * 0.5f, sz * 0.1f), new Vector3(3.2f, (l.Height + 1.5f) * 0.5f, 3.2f), stone);
                        LookTestBuilder.Box(g, "Door", new Vector3(0f, 1.15f, -sz * 0.5f - 0.06f), new Vector3(1.4f, 2.3f, 0.12f), wood, default, 2f);
                        LookTestBuilder.Box(g, "DoorMarker", new Vector3(0f, 0.02f, -sz * 0.5f - 0.6f), new Vector3(1.4f, 0.04f, 0.8f), M("label"), default, 2f);
                        LookTestBuilder.Box(g, "PatchBoards", new Vector3(-sx * 0.3f, 3.6f, -sz * 0.5f - 0.04f), new Vector3(1.6f, 1.2f, 0.08f), oak, new Vector3(0f, 0f, 6f), 2f);
                        var stair = new GameObject("UndercroftStair");   // P25: the way down; a marker for M5/M6 (inside the house)
                        stair.transform.SetParent(g, false);
                        stair.transform.position = new Vector3(HollowmereLayout.UndercroftStair.X, l.Y, HollowmereLayout.UndercroftStair.Z);
                    }
                    else
                    {
                        LookTestBuilder.Box(g, "Walls", new Vector3(0f, l.Height * 0.5f, 0f), new Vector3(sx, l.Height, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "Loft", new Vector3(0f, l.Height + 1.0f, 0f), new Vector3(sx - 0.8f, 2.0f, sz - 0.8f), oak, default, 2f);
                        LookTestBuilder.Box(g, "RoofW", new Vector3(-sx * 0.25f, l.Height + 2.6f, 0f), new Vector3(sx * 0.6f, 0.25f, sz + 0.4f), roof, new Vector3(0f, 0f, 35f), 2f);
                        LookTestBuilder.Box(g, "RoofE", new Vector3(sx * 0.25f, l.Height + 2.6f, 0f), new Vector3(sx * 0.6f, 0.25f, sz + 0.4f), roof, new Vector3(0f, 0f, -35f), 2f);
                        LookTestBuilder.Box(g, "Door", new Vector3(sx * 0.5f + 0.06f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 1.2f), wood, default, 2f);
                    }
                    break;
                case LandmarkKind.Mill:
                    if (HollowmereArt.TryPlaceMill(g, rebuilt)) break;   // Q2 art (sc_mill.py): Shell when ruined, restored Frame + wheel and door when rebuilt
                    if (rebuilt)
                    {
                        LookTestBuilder.Box(g, "Walls", new Vector3(0f, 3f, 0f), new Vector3(sx, 6f, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "RoofW", new Vector3(0f, 7.2f, -sz * 0.25f), new Vector3(sx + 0.6f, 0.25f, sz * 0.62f), roof, new Vector3(-35f, 0f, 0f), 2f);
                        LookTestBuilder.Box(g, "RoofE", new Vector3(0f, 7.2f, sz * 0.25f), new Vector3(sx + 0.6f, 0.25f, sz * 0.62f), roof, new Vector3(35f, 0f, 0f), 2f);
                    }
                    else
                    {
                        LookTestBuilder.Box(g, "WallW", new Vector3(-sx * 0.5f + 0.35f, 2.4f, 0f), new Vector3(0.7f, 4.8f, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "WallE", new Vector3(sx * 0.5f - 0.35f, 3f, 0f), new Vector3(0.7f, 6f, sz), stone, default, 2f);
                        LookTestBuilder.Box(g, "WallN", new Vector3(0f, 2f, sz * 0.5f - 0.35f), new Vector3(sx, 4f, 0.7f), stone, default, 2f);
                        LookTestBuilder.Box(g, "WallS", new Vector3(0.8f, 1.4f, -sz * 0.5f + 0.35f), new Vector3(sx - 1.6f, 2.8f, 0.7f), stone, default, 2f);
                    }
                    var wheel = LookTestBuilder.Prim(g, "Wheel", PrimitiveType.Cylinder, new Vector3(sx * 0.5f + 0.7f, 2.4f, 0f), new Vector3(5f, 0.4f, 5f), oak);
                    wheel.transform.localRotation = Quaternion.Euler(rebuilt ? 0f : 18f, 0f, 90f);
                    break;
                case LandmarkKind.TownGate:
                    if (HollowmereTownWall.PierHandled(parent, t, l, rebuilt)) break;   // Q8 gatehouse art (carries the piers); the greybox pier below is the fallback
                    LookTestBuilder.Box(g, "Pier", new Vector3(0f, l.Height * 0.5f - 1f, 0f), new Vector3(sx, l.Height + 2f, sz), stone, default, 2f);
                    break;
            }
        }

        static void BellTower(Transform g, Landmark l, bool rebuilt)
        {
            // The B4 BellTower art when its folder is in (pivot = shaft bottom centre, clock face to local +Z, so ArtYaw 180 faces it south to the square).
            if (BeautyCornerArt.FindFolder("B4", "BellTower") != null)
            {
                var row = new BeautyCornerTable.Row
                {
                    Brief = BeautyCornerTable.Neighbours, Id = "BellTower", Batch = "B4", Names = new string[0], ArtYaw = 180f,
                    Instances = new[] { new BeautyCornerTable.Instance(0f, 0f, 0f, 0f, 1f, "BellTower") }, Placeholder = new BeautyCornerTable.Part[0],
                };
                BeautyCornerBuilder.PlaceRow(g, row);
                return;
            }
            Material stone = TownSurface.Stone(), oak = TownSurface.Timber(), roof = LookTestBuilder.Mat("roof_tile");
            LookTestBuilder.Box(g, "Base", new Vector3(0f, 0.5f, 0f), new Vector3(6.5f, 1.0f, 6.5f), stone, default, 2f);
            LookTestBuilder.Box(g, "Shaft", new Vector3(0f, 6.3f, 0f), new Vector3(4.4f, 11.6f, 4.4f), stone, default, 2f);
            foreach (float x in new[] { -2.2f, 2.2f }) foreach (float z in new[] { -2.2f, 2.2f })   // open belfry: corner posts under the spire
                    LookTestBuilder.Box(g, "BelfryPost", new Vector3(x, 13.2f, z), new Vector3(0.4f, 2.2f, 0.4f), oak, default, 2f);
            LookTestBuilder.Box(g, "BelfryFloor", new Vector3(0f, 12.15f, 0f), new Vector3(4.8f, 0.15f, 4.8f), oak, default, 2f);
            float[] widths = { 4.6f, 3.4f, 2.2f, 1.0f };
            for (int i = 0; i < 4; i++) LookTestBuilder.Box(g, "Spire" + i, new Vector3(0f, 14.7f + i * 0.8f, 0f), new Vector3(widths[i], 0.8f, widths[i]), roof, new Vector3(0f, 45f, 0f), 2f);
            if (rebuilt) LookTestBuilder.Prim(g, "Bell", PrimitiveType.Cylinder, new Vector3(0f, 13.1f, 0f), new Vector3(1.3f, 0.6f, 1.3f), LookTestBuilder.Mat("iron"));
            LookTestBuilder.Box(g, "Door", new Vector3(0f, 2.1f, 3.3f), new Vector3(1.2f, 2.2f, 0.12f), LookTestBuilder.Mat("painted_wood"), default, 2f);
        }

        // The three fixed stalls (Wave 1): Ring1/Stalls/<Variant>/Stall_<Variant>.fbx holds both states as children Ruined and Rebuilt, each with its own baked
        // set (Stall_<Variant>_<State>_BaseColor/_Normal/_ORM); the preset keeps its state and drops the other. Placed at the landmark (layout row incl. its pad height),
        // counter to local +Z. False when the art is not in, so the greybox below stands in.
        const string StallsFolder = BeautyCornerArt.ArtRoot + "/Ring1/Stalls";
        static readonly Dictionary<string, string> StallVariants = new Dictionary<string, string> { { "Stall_1", "Produce" }, { "Stall_2", "Cloth" }, { "Stall_3", "Tools" } };
        static readonly HashSet<string> OnStallAssets = new HashSet<string> { "Basket_Produce", "Basket_Bread", "Jug_A", "Jug_B" };   // corner B7 goods on the B8 counter

        static string StallFolder(string id) =>
            StallVariants.TryGetValue(id, out string v) && AssetDatabase.IsValidFolder(StallsFolder + "/" + v) ? StallsFolder + "/" + v : null;

        static bool StallArt(Transform g, string id, bool rebuilt)
        {
            string folder = StallFolder(id);
            if (folder == null) return false;
            string v = StallVariants[id], keep = rebuilt ? "Rebuilt" : "Ruined";
            GameObject model = BeautyCornerArt.LoadModel(folder, "Stall_" + v + ".fbx");
            if (model == null) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, g);
            go.name = "Stall_" + v + "_" + keep;
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            Material m = BeautyCornerArt.MaterialFromSet(folder, "Stall_" + v + "_" + keep, "Stall_" + v + "_" + keep);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (Transform c in go.transform.Cast<Transform>().ToList())
            {
                if (!c.name.StartsWith(keep)) { UnityEngine.Object.DestroyImmediate(c.gameObject); continue; }   // the LOD import names the state node "<State>.001" (children <State>_LOD0..2), not "<State>"
                foreach (MeshRenderer r in c.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = m;   // <State>_LOD0..2 children under the state's LODGroup node
            }
            foreach (LODGroup lg in go.GetComponentsInChildren<LODGroup>(true))   // the other state's renderers are gone: drop them from the LOD rows so the group keeps only live ones
            {
                LOD[] lods = lg.GetLODs();
                for (int i = 0; i < lods.Length; i++) lods[i].renderers = (lods[i].renderers ?? new Renderer[0]).Where(r => r != null).ToArray();
                lg.SetLODs(lods);
            }
            var col = go.AddComponent<BoxCollider>();   // counter and frame (B8 header); the awning overhang has none
            col.center = new Vector3(0f, 0.8f, 0f); col.size = new Vector3(2.7f, 1.6f, 1.6f);
            return true;
        }

        // A corner row without the B8 stall, its counter lantern and the B7 goods standing on its counter (the produce stall art replaces all three); null = drop the row.
        static BeautyCornerTable.Row WithoutStallDressing(BeautyCornerTable.Row row)
        {
            if (row.Id == "MarketStall" || row.Id == "StallLantern") return null;
            if (!row.Instances.Any(i => i.Asset != null && OnStallAssets.Contains(i.Asset))) return row;
            return new BeautyCornerTable.Row
            {
                Brief = row.Brief, Id = row.Id, Batch = row.Batch, Names = row.Names, Placeholder = row.Placeholder, Foliage = row.Foliage, ArtYaw = row.ArtYaw, Scatter = row.Scatter,
                Instances = row.Instances.Where(i => i.Asset == null || !OnStallAssets.Contains(i.Asset)).ToArray(),
            };
        }

        // Without the Wave 1 art: stalls 2 and 3 reuse the B8 MarketStall row's art (or its greybox) at their own place and turn.
        static void StallArt(Transform g)
        {
            BeautyCornerTable.Row src = BeautyCornerTable.Rows.First(r => r.Id == "MarketStall");
            var row = new BeautyCornerTable.Row
            {
                Brief = src.Brief, Id = src.Id, Batch = src.Batch, Names = src.Names, ArtYaw = src.ArtYaw,
                Instances = new[] { new BeautyCornerTable.Instance(0f, 0f, 0f) }, Placeholder = src.Placeholder,
            };
            BeautyCornerBuilder.PlaceRow(g, row);
        }

        // --------------------------------------------------------------------------------------------- path graph (editor-only gizmo data)
        // An inactive empty per doorstep, door and stair node, so M6 work can find them in the hierarchy; the full graph lives in Core (HollowmereLayout)
        // and the approval map draws it.
        static void BuildPathDebug(Transform root, TownLayout t)
        {
            Transform dbg = Child(root, "PathGraph (markers, hidden)");
            dbg.gameObject.SetActive(false);
            foreach (PathNode n in t.Nodes)
            {
                if (n.Kind != PathNodeKind.Doorstep && n.Kind != PathNodeKind.Door && n.Kind != PathNodeKind.Stair) continue;
                var m = new GameObject(n.Id);
                m.transform.SetParent(dbg, false);
                m.transform.localPosition = new Vector3(n.Pos.X, n.Y, n.Pos.Z);
            }
        }
    }
}
