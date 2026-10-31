# LaneKit (Hollowmere ring 2 lane kit, Q11): sources

- Generator: `Art/Blender/town/sc_lane_kit.py` (procedural, builds on `sc_facade_kit.py` materials and the `sc_keep_terrace.py` geometry helpers). Cloud export (2026-10-31, Blender 4.5.3):
  `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_lane_kit.py -- --out <scratch> --no-render --export --export-dir Assets/_Project/Content/Art/Town/Lanes/LaneKit`.
  `--assemble` renders the review lane (never exported), `--tris` prints the table. Contract: `Reference/hollowmere/lanes/CONTRACT.md`. No `.meta` (Unity import makes them).
- Axes: authored as Unity (x, y, z), exported -Z forward, Y up, FBX_SCALE_UNITS, bake_space_transform, FACE smoothing, linear colours, `path_mode='STRIP'`; Unity (x, y, z) = Blender (-x, z, -y).
  Root empty `LaneKit` (identity); every piece is a direct child mesh at its own pivot at the origin, so the builder clones children by name. Re-import check: 17 meshes, names and slots as below, `wear` attribute on every mesh. 18,562 tris in all.
- Wall modules (`GardenWall*`, `RetainWall*`) tile every 2 m in X: stones that cross the seam are cut on the seam planes, the UVs are world metres (2 m per tile) with a per-stone offset that is identical on both halves, wear noise repeats every 2 m. `LaneStep` setts and `Gutter` slabs are cut on the seam the same way.

## Pieces (Unity local extents measured from the exported meshes; lane face is local -Z)

| Piece | Tris (budget) | Extent x / y up / z | Notes |
|---|---|---|---|
| `GardenWall` | 2,408 (2,500) | -1..1 / -0.81..1.36 / -0.26..0.26 | rubble both faces (yard side plainer), cock-and-hen rounded coping, skirt to -0.8 |
| `GardenWallBroken` | 2,300 (2,500) | -1..1.06 / -0.80..1.23 / -0.57..0.64 | top tumbled 0.5-1.0 m, one gap (x 0.1..0.75), rubble and fallen coping stones at the foot (spill to 0.64) |
| `GardenWallEnd` | 560 (800) | -0.31..0.30 / -0.80..1.55 / -0.30..0.30 | dressed pinwheel pier, cap, symmetric |
| `RetainWall` | 3,240 (4,000) | -1..1 / -1.0..3.44 / -0.36..0.35 | lane face z -0.35, string course and flush walkway coping at y 3.0, parapet to 3.44 |
| `RetainWallLow` | 1,652 (2,500) | -1..1 / -0.62..1.94 / -0.35..0.35 | same, coping at y 1.5 |
| `RetainWallBreach` | 2,860 (4,500) | -1..1.09 / -1.0..3.0 / -1.49..0.31 | slumped top, notch at x -0.2..0.7, coping missing, soil slump, stones fanned to z -1.49; parapet stub at x < -0.78 |
| `RetainButtress` | 1,252 (1,500) | -0.52..0.52 / -1.0..2.60 / -0.99..-0.35 | battered, quoins, sloping dressed top meeting the wall at 2.6 |
| `LaneStep` | 222 (250) | -1..1 / -0.80..0.16 / 0..0.55 | riser face at z 0 facing -Z (downhill); 19 setts in three running-bond rows, nosing row, worn centre dip; hidden body to y -0.8 |
| `LaneKerb` | 132 (200) | -1..1 / -0.30..0.12 / -0.16..0.15 | three dressed edge stones |
| `Gutter` | 144 (300) | -0.99..0.99 / -0.25..0.02 / -0.20..0.20 | four dished slabs, damp dark centre |
| `GutterChoked` | 244 (300) | same | silted with earth, a few stones |
| `GutterGrate` | 272 (400) | -0.3..0.3 / -0.20..0.02 / -0.30..0.30 | stone frame, iron bars |
| `WallLantern` | 688 (1,500) | -0.14..0.14 / -0.31..0.30 / -0.60..0.00 | plate on the wall face (z 0), arm and caged lantern toward -Z, lit glass in `LK_GlassLit` |
| `WallLanternBroken` | 600 (1,500) | -0.12..0.19 / -0.46..0.30 / -0.54..0.09 | arm sagged, cage crooked with one post missing, no glass |
| `GatePier` | 712 (1,000) | -0.3..0.3 / -0.80..2.11 / -0.30..0.30 | dressed courses, cap at 1.85..1.97, ball to 2.11; lantern mount on the lane face at y 1.8 |
| `GateLeaf` | 704 (1,200) | 0..1.39 / 0.05..1.25 / -0.04..0.04 | hinge at the origin, board-and-batten, oak battens and brace on the yard side, iron straps and latch on the lane side |
| `GateLeafBroken` | 572 (1,200) | 0.02..1.66 / -0.31..1.19 / -0.04..0.21 | hangs off its upper hinge (pitched 15 deg, yawed 7 deg), one board missing, one snapped, lower strap gone |

Deviations from the contract: `GardenWallBroken` spill reaches z +-0.64 (contract +-0.7), `RetainWall` z -0.36 (1 cm of coping chamfer wobble), `GateLeafBroken` swings out of its box (it is tilted).

## Slots (materials rebuilt by Unity from this table)

Every textured slot is a shared B1 tileable (2048 px at most) sampled with the UV0 metre UVs (2 m per tile). Tint is a linear multiplier on the albedo; normal scale is the normal-map strength. `wear` (corner colour, linear) drives the kit's vertex-mask shader as in the B4 facade: R grime (mixes toward a warm dark grime colour; on rubble and dressed stone it also carries the dark end of the per-stone value variation), G moss, B polish (also the light end of the per-stone variation).

| Slot | Tileable (`Art/Textures/B1_town/...`) | Linear tint | Normal scale | Emissive | Used for |
|---|---|---|---|---|---|
| `LK_Rubble` | sandstone | (0.57, 0.60, 0.66) | 1.5 | | rubble face stones, coping, spill (cool grey against the pale dressed stone) |
| `LK_Dressed` | sandstone | (1.00, 0.95, 0.86) | 1.3 | | piers, quoins, string courses, kerbs, gutter slabs, grate frame |
| `LK_Mortar` | sandstone | (0.26, 0.245, 0.21) | 0.6 | | dark core and joint fill behind the stones |
| `LK_Oak` | oak_timber | (0.80, 0.76, 0.72) | 1.2 | | gate battens and brace |
| `LK_GatePaint` | painted_wood | (0.66, 0.72, 0.28) (faded sage-forest green) | 1.0 | | gate boards |
| `LK_Iron` | iron | (1.0, 1.0, 1.0) | 1.0 | | lantern bracket and cage, straps, latch, grate bars |
| `LK_GlassLit` | none (flat colour (0.9, 0.55, 0.2), roughness 0.3) | | linear (1.0, 0.62, 0.28), intensity about 3 | yes | the lantern glass (warm lit) |
| `LK_Earth` | none (flat colour (0.034, 0.025, 0.017), roughness 0.95) | | | | silt in `GutterChoked`, soil slump in `RetainWallBreach` |

No foliage slot is used (`M_B9_Foliage` is not in this FBX).

## How the builder tiles it (art notes)

- `LaneStep`: pivot at the riser foot; stack with rise 0.16 and run 0.55 (grade 0.29), each tread's top at the lane height at its uphill edge; across a 5 m lane use three pieces at X scale 0.833 (the setts stretch a little), or 2 m pieces plus a scaled one. The hidden body (y -0.8..0) makes the flight solid at any step height up to 0.8.
- `LaneKerb` and `Gutter` take a pitch along the run (tested at 16 degrees in the review lane). `GutterGrate` caps a gutter run.
- Garden walls stair down a slope: place each 2 m module at the ground under its centre, the -0.8 skirt hides the step. Gate: piers at +-1.0 from the opening centre, `GateLeaf` hinge at the pier's inner face (z-axis along the lane), lantern on a pier's lane face at y 1.8.

## Licences

The B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`): sandstone `sandstone_blocks_05`, oak_timber `rough_wood`, painted_wood `blue_painted_planks`, iron `rusty_metal_02` (all CC0 1.0, Rob Tuytel). The review-only ground uses cobble (CC0), meadow_grass (CC0, ambientCG) and packed_soil (CC0) and is not exported. Geometry is procedural, ours. Reference photos (study only, never shipped or traced): existing licence-checked frames in `Reference/hollowmere/ground/` (see its `REFS.md`); no new photos were fetched, the Wikimedia Commons API rate-limited the session (2026-10-31).

## Review (sc-asset-review rubric, /40; floor 25; calibrated per the beauty-corner brief)

Round 1 baseline 26.5, round 2 28, round 3 29 (target first + 5 = 31.5 not reached; stopped at the 3-round limit the controller set). Final: silhouette 4, proportion 3.5, form 4, surface 3.5, colour 3, wear 3.5, set 3.5, efficiency 4. Sheet: `Reference/hollowmere/lanes/SHEET_lane_kit.jpg` (reference left, render right).
