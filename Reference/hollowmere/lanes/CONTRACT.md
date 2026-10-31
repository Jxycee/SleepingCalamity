# Q11 lane kit: art and integration contract (2026-10-31)

Queue item Q11 (`Reference/hollowmere/QUEUE.md`): the ring 2 lane kit (garden walls, terrace retaining walls, lane steps and ramps within the
20 degree rule, wall lanterns, gates into yards, drain gutters), placed along the lanes at their heights in both presets (Ruined, Rebuilt).
Shared rules: CLAUDE.md, the mass-production design (`docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md`),
`docs/art/beauty-corner-brief.md` scoring, `docs/PHASE1.md` 3 (style bible), `.claude/rules/art-assets.md`.
Templates: art `Art/Blender/town/sc_keep_terrace.py` (Q9: same piece-per-child kit pattern, FK helpers, linear CORNER `wear`, deterministic seeds
via FK.rng / zlib.crc32, never hash()); placement plan `Assets/_Project/Core/Town/TownWallPlan.cs` + placer
`Assets/_Project/Editor/Builders/HollowmereTownWall.cs` (Q8: art-or-greybox fallback, material slot table from SOURCES.md).

## Layout facts (measured on Core, 2026-10-31)
- Lanes carry per-point `Heights` relaxed to grade <= 0.30 (MaxLaneGrade, ~17 deg; the town rule TownLayout.MaxGrade is 20 deg). Every stub
  (West, Market Street, East, Keep Lane, NW ginnel) has one 6-8 m segment at 0.28-0.30 and shoulders at 0.10-0.15; the Ring Lane is flat
  (-3.5 north, -6.5 south) except two 9 m drops at 0.24 (south-west and south-east) with 0.10 shoulders; Lower Lane ends with 8 m at 0.30.
- Ground: `HollowmereLayout.Ground(t, x, z)` (lane corridors + lot pads). Never place at y = 0; every row stands on Ground or a lane height.
- Greybox to replace: `HollowmereBuilder.BuildWalls` TerraceWall (OffsetLine(TerraceOffset 0.75), ~77 m) and LowerTerraceWall (LowerTerraceOffset 2.6,
  ~23 m) via `Wall()`; the lot cut/fill boxes `HollowmereBuilder.CutAndFill` (Retain*/Fill* boxes per lot side).
- Not ours: the town wall and gates (Q8, `TownWallPlan`, merged), the keep terrace wall and stairs (Q9, BuildKeep), clutter (Q14), garden decorations (Q6).
  Lanes in scope: RingLane, WestStub, MarketStreet, EastStub, NorthEastStub, NorthWestGinnel, LowerLane up to 2 m inside the town wall line.
  Not MillLane, not SaltwakeRoad.

## Art: `Art/Blender/town/sc_lane_kit.py` -> `Assets/_Project/Content/Art/Town/Lanes/LaneKit/LaneKit.fbx` + SOURCES.md
Root `LaneKit`; each piece a direct child mesh at its own pivot at the origin (the builder clones children by name). Axis mapping as Q9:
Unity (x, y, z) = Blender (-x, z, -y); export -Z forward / Y up, path_mode STRIP. Extents below are Unity local metres.
Wall-like pieces run along local X; their LANE (seen) face is local -Z.

| Piece | Extent (Unity local) | Notes |
|---|---|---|
| `GardenWall` | x -1..1, z -0.25..0.25, y -0.8..1.35 | 2 m free-standing rubble garden wall, cock-and-hen or rounded cap stones; both faces at mid-distance detail (yard side plainer); the -0.8 skirt hides the step when the builder stairs segments down a 0.3 slope |
| `GardenWallBroken` | same | Ruined: top tumbled to 0.5-0.9 m, gaps, rubble spill at the foot to z +-0.7 |
| `GardenWallEnd` | x -0.3..0.3, z -0.3..0.3, y -0.8..1.55 | dressed pier with a cap, ends a run and flanks gaps |
| `RetainWall` | x -1..1, z -0.35..0.35, y -1.0..3.0, parapet to 3.45 | 2 m terrace retaining wall: coursed rubble face at z -0.35 (the lower, lane side), damp foot, coping at y 3.0 flush with the upper ground at z +0.35, low parapet 0.45 |
| `RetainWallLow` | x -1..1, z -0.35..0.35, y -0.6..1.5, parapet to 1.95 | the same for steps up to ~1.6 m |
| `RetainWallBreach` | as RetainWall, spill to z -1.6 | Ruined: slumped top, coping missing, fallen stones fanned at the foot |
| `RetainButtress` | x -0.5..0.5, z -1.0..-0.35, y -1.0..2.6 | battered, sloping weathered top; stands on the lane face |
| `LaneStep` | x -1..1, z 0..0.55, y 0..0.16 visible (hidden body to y -0.8) | one stepped-street tread of 2-3 setts: riser face at z 0 facing -Z (downhill), nosing, worn centre dip; builder tiles it across the lane and stacks it (rise ~0.16, run ~0.55) |
| `LaneKerb` | x -1..1, z -0.15..0.15, y -0.3..0.12 | edge stones along a ramp or flight edge, builder tilts it to the slope |
| `Gutter` | x -1..1, z -0.2..0.2, y -0.25..0.02 | dished stone drain channel, damp dark centre, top flush with ground at 0 |
| `GutterChoked` | same | Ruined: silted with earth, a few stones/leaves |
| `GutterGrate` | x -0.3..0.3, z -0.3..0.3, y -0.2..0.02 | iron grate in a stone frame, where a gutter run ends |
| `WallLantern` | mount plate at z 0 (the wall face), arm to z -0.6, y -0.6..0.4 around the mount at the origin | iron bracket + lantern, lit glass slot `LK_GlassLit` (emissive) |
| `WallLanternBroken` | same | Ruined: bent arm, empty cage, no glass |
| `GatePier` | x -0.3..0.3, z -0.3..0.3, y -0.8..2.1 | dressed gate pier with cap and ball; a lantern can mount on its lane face at y 1.8 |
| `GateLeaf` | hinge at origin, leaf x 0..1.4, z -0.04..0.04, y 0.05..1.25 | board-and-batten yard gate, faded green paint (brief: sage to forest green), iron strap hinges |
| `GateLeafBroken` | hinge at origin | Ruined: hangs off one hinge (tilted), a board missing |

Gate opening: 1.4 m between pier inner faces (pier centres +-1.0 from the opening centre).
Material slots (exact names; SOURCES.md lists them with their tileable, tint, normal scale): `LK_Rubble`, `LK_Dressed`, `LK_Mortar` (sandstone family
like KT_*/BT_Rubble, the shared B1 tileables), `LK_Oak`, `LK_GatePaint`, `LK_Iron`, `LK_GlassLit`, `LK_Earth`; foliage (optional) `M_B9_Foliage`.
Budget (tris): GardenWall <= 2,500 each variant, GardenWallEnd 800, RetainWall 4,000, RetainWallLow 2,500, RetainWallBreach 4,500, RetainButtress 1,500,
LaneStep 250, LaneKerb 200, Gutter/Choked 300, GutterGrate 400, WallLantern 1,500, GatePier 1,000, GateLeaf/Broken 1,200. Kit under 30k. Maps max 2048.

## Placement: Core `Assets/_Project/Core/Town/LaneKitPlan.cs` (pure C#) + Editor `Assets/_Project/Editor/Builders/HollowmereLanes.cs`
Rows: (Id, Piece, Preset Ruined|Rebuilt|Both, X, Y, Z, Yaw, ScaleX, Tilt (pitch along the run, for kerbs/gutters on slopes)). Deterministic.
- **Steps:** every in-scope lane segment with |grade| >= 0.18: LaneStep rows across the full lane width (minus the gutter), risers ~0.16 m, each tread's
  top at the lane height at its uphill edge; landings at segment ends. Both presets.
- **Ramps:** segments 0.05 <= |grade| < 0.18 get LaneKerb along both edges (tilted); flights get kerbs as cheeks too.
- **Gutters:** along the right edge (lane direction) of every in-scope segment outside the square, tilted to the slope; GutterGrate at each run end.
  Ruined: GutterChoked.
- **Retaining walls:** the TerraceWall / LowerTerraceWall runs (`WallRun(..., minStep 1)`) as RetainWall/RetainWallLow modules (by step height; sunk so the
  coping meets the upper ground), RetainButtress every ~6-8 m on the lower face; the lot cut/fill sides (today's CutAndFill boxes) the same way.
  Ruined: ~20% RetainWallBreach (seeded).
- **Garden walls:** along lane edges (lane half width + 0.6) where the line is clear of every lot footprint + 1 m yard, landmark, the square, the keep,
  4 m of a lane junction, 2 m of the town wall line, and every path edge (leave a gap with GardenWallEnd piers). Segments stair down slopes, each at
  Ground under its centre. Where the ground beside the lane is > 0.8 m above it, use RetainWallLow facing the lane instead. A gate (two GatePiers +
  GateLeaf) in the middle of every run >= 10 m. Ruined: ~35% GardenWallBroken, gates GateLeafBroken or no leaf.
- **Lanterns:** a lane gets a lantern about every 15 m: on gate piers (lane face), retaining walls facing the lane within 3 m, and flight heads/feet.
  Rebuilt WallLantern (lit, plus a small warm point light at low intensity), Ruined WallLanternBroken.
- Tests (EditMode, `Tests/EditMode/LaneKitPlanTests.cs`): deterministic (golden count/hash), every row's Y within 0.25 m of Ground (steps: within a riser
  of the lane line), no wall/gate/lantern inside a lot footprint, landmark, the square or the keep, no wall row crossing a path edge, nothing within 2 m
  of the town wall line, step risers <= 0.2 m and below the player step offset (`MovementMath.StepOffset`), both presets populated.
- HollowmereBuilder: one call into HollowmereLanes.Build(set, ruined, rebuilt, t); drop the two terrace `Wall(...)` lines and the CutAndFill call
  when the art is present (greybox stays as the fallback). Keep the edit small: the file is shared.
- Captures (fixed cameras, both presets, midday): `Cap_HM_MarketStreet` (from the square's south edge looking down the Market Street steps),
  `Cap_HM_LaneMouthWest` (from the Ring Lane at the West Lane junction looking up the West Lane flight to the square), `Cap_HM_TerraceSteps`
  (the Ring Lane's south-west 0.24 drop: flight, kerbs, gutter, the retaining wall).
