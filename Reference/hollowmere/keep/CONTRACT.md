# Q9 keep terrace: art and integration contract (2026-10-31)

Queue item Q9 (`Reference/hollowmere/QUEUE.md`): the keep hall beside the bell tower on the +4 m terrace, and the terrace dressing (buttressed
retaining wall with ivy planters, both stairs with balustrades, benches). Captures: `Cap_HM_KeepStairs` and `Cap_HM_KeepWall`, both presets.
Shared rules: CLAUDE.md, the mass-production design (`docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md` 5.2),
`docs/art/beauty-corner-brief.md` scoring, `docs/PHASE1.md` 3 (style bible). Code template for generators: `Art/Blender/town/sc_bakery.py` /
`sc_bell_tower.py` (FK helpers, EXTRA materials, linear CORNER `wear`, deterministic seeds via FK.rng/zlib.crc32 (never hash()),
export -Z forward / Y up, `path_mode='STRIP'`). Axis mapping after export: Unity (x, y, z) = Blender (-x, z, -y).

## Layout facts (Core/Town/HollowmereLayout.cs; Unity metres, +Z north, square at y 0)
- Terrace top y = KeepHeight 4.0; the retaining wall's square-side face runs along z = KeepWallZ 19.6, |x| < 24 (gaps at the stairs and the
  tithe gate at x 5.6). Greybox today (HollowmereBuilder.BuildKeep): wall 1.2 thick from y -0.6 to 4.9 (parapet 0.9 above the terrace),
  buttresses 1.1 x 0.7 every 6 m battered 6 deg, planters 1.8 x 0.6 x 0.3 on the parapet every 4.5 m, east stair Keep_StairFoot
  (19.2, 0, 14.0) -> Keep_StairHead (19.2, 4, 20.6) 2.2 wide, west stair Keep_WStairFoot (-26.5, 0, 17.6) -> Keep_WStairHead (-20.8, 4, 20.6)
  1.8 wide.
- Bell tower footprint x 2.35..8.85, z 20.75..27.25. Terrace walk: WStairHead -> Keep_Walk_W (-8, 4, 29) -> Keep_Walk_N (5.6, 4, 29.2) -> tower door.

## Keep hall (new fixed landmark)
- Generator `Art/Blender/town/sc_keep_hall.py`, root `KeepHall`, export `Assets/_Project/Content/Art/Town/Keep/KeepHall/KeepHall.fbx` + SOURCES.md.
- Footprint body 9.0 (x) x 6.0 (z) m, centred at its origin (Unity), on the terrace at centre (-3.0, 4.0, 24.5): x -7.5..1.5, z 21.5..27.5.
  Long side faces the square (Unity -Z = Blender -Y front). Door on the WEST gable end (Unity -X = Blender +X side!), its threshold centre at
  local (-4.5, 0, 0.5); a door node goes 0.8 m outside it.
- An old stone hall: rubble/sandstone walls with dressed quoins, tall round-headed or lancet windows (3 on the square side, 2 on the back),
  buttresses at the corners, a steep roof (slate look: B1 roof_tile tinted dark blue-grey, or sandstone tinted slate, thin coursed slates),
  a small bellcote or louvred vent optional, a stone porch hood over the west door. Eave ~6 m, ridge ~10 m: it must not out-shout the 17 m
  bell tower beside it. Distinct from the town houses (no half-timber). All four sides to mid-distance detail.
- Two states like the mill: `Shell` (Ruined: roof partly fallen, windows empty, door gone) and `Frame` (Rebuilt) + `Part_Door`
  (pivot `SOCKET_Door`, under Frame). `FOOTPRINT` 9 x 6 x 0.1. Budget: Shell < 25k, Frame + parts < 35k tris.

## Terrace kit (new)
- Generator `Art/Blender/town/sc_keep_terrace.py`, root `KeepTerrace`, export `Assets/_Project/Content/Art/Town/Keep/KeepTerrace/KeepTerrace.fbx`
  + SOURCES.md. Each piece a direct child mesh at its own pivot at the origin (the builder clones children), sandstone/rubble shared tileables:
  | Piece | Extent (Unity, metres; local) | Notes |
  |---|---|---|
  | `WallSegment` | x -1..1, z -0.6..0.6, y -0.6..4.9 | 2 m of retaining wall: square-side face at z -0.6 (coursed rubble, a plinth course, damp foot), terrace side at z +0.6 flush with the terrace top y 4.0, parapet y 4.0..4.9 with a coping |
  | `WallEnd` | x -0.3..0.3 | a quoined end/return for gaps (stairs, tithe gate) |
  | `Buttress` | x -0.55..0.55, z -1.3..-0.6, y -0.6..4.0 | battered, with a weathered sloping top; stands on the square side of the wall face |
  | `Planter` | x -0.9..0.9, z -0.3..0.3, y 0..0.35 | stone trough sitting ON the parapet coping (builder places it at y 4.9), with trailing ivy over the square side (cards/ribbons, a few leaves) |
  | `StairStep` | x -1..1 (scaled to width by the builder in x), z 0..0.32, y 0..0.22 | one worn tread with a nosing; the builder stacks them |
  | `Balustrade` | x -1..1, y 0..1.0 | 2 m of stone balustrade (rail, balusters, base) for the stair's open side; the builder rakes it along the flight |
  | `Newel` | 0.4 x 0.4, y 0..1.2 | stair-foot / stair-head post with a cap |
  | `Bench` | x -0.9..0.9, z -0.25..0.25, y 0..0.5 | stone slab bench on two legs (oak seat optional) |
  Budget: whole kit < 30k tris.

## Integration (one agent, after the art)
- Add `LandmarkKind.KeepHall` (append) and a layout row `KeepHall` at (-3.0, 24.5), 9 x 6, yaw 0, height 10, door (-8.3, 25.0), Y = KeepHeight;
  connect `KeepHall_Door` to `Keep_Walk_W`; keep `TownLayout.Validate()` and the layout tests green (add a test for the row).
- HollowmereBuilder: a KeepHall landmark case (art via HollowmereArt-style placer: Shell in Ruined, Frame + Part_Door in Rebuilt, greybox
  fallback), and BuildKeep places the terrace kit along the existing run/gaps (wall segments, ends at gaps, buttresses where the greybox
  had them, planters on the parapet, stairs from the existing foot/head nodes with balustrades on the open side and newels, 3-4 benches on the
  terrace by the parapet), greybox fallback when the FBX is missing. Then `unity.sh build`, captures (`HollowmereCapture`, cameras
  Cap_HM_KeepStairs / Cap_HM_KeepWall), `unity.sh check`.
