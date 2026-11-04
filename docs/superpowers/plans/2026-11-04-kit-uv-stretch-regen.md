# Kit UV-stretch regen plan (planning only, 2026-11-04)

For an art session after the break. No Blender, captures or code ran to write this. Source: polish round 4 deferred list (`Reference/sessions/town-art-leftovers/STATUS.md`, `scan_ranked_round4.md`), `HollowmereFaultScan.Uv()` (flags `uv_stretch` > 2.5, area-weighted singular-value ratio of the UV-to-world Jacobian, tiling PBR materials only).

## What stretch means here
A flagged material has UVs whose U and V world-scale differ by more than 2.5x on average, so a tiling texture smears along one axis. Round 4 left 33 raw rows (14 ranked items, 12 mesh families). No `.blend` is tracked: the Blender generators in `Art/Blender/tools/town/` write the FBX directly, so each fix is a change to a generator, then a re-export.

## Shared fix recipe (per generator)
1. Find where the flagged material's faces get UVs (per-part `uv_*` helper or a smart-project call). Replace non-uniform or bounding-box-normalised UVs by **world-scale box projection**: per face, pick the dominant axis, set UV = the two other world coordinates in metres divided by the material's tile size, so texel density is equal on U and V.
2. Thin parts (signs, bells, chimney bands, spire, iron straps): the long axis was normalised to 0..1. Keep metres-per-tile instead, and let the UV run past 1 (the material tiles).
3. Baked sets (Basket_Produce, Pot_Broken): these use one baked atlas, not a tiling material. Re-unwrap the island with a uniform scale (island aspect = world aspect), pack again, rebake only if island areas change by more than 10%.
4. Export with `path_mode='STRIP'` (art rules), run `sc_asset_audit.py` on the FBX, then re-run `HollowmereFaultScan.Scan` (`SC_CAPTURE_NOBUILD=1`) and confirm no row of that family is above 2.5.
5. One generator fixes several rows: do them together (workshop, facade, smithy, bakery).

The recipe is the standard fix for this metric, not yet checked against each generator's code; the first job of the art session is to read each UV helper.

## Rows to fix, by generator
| Generator | Rows | Specific fix |
|---|---|---|
| `town/sc_workshop.py` | #5 Workshop_C Frame (chimney stone 7.5, oak 2.9, iron 2.7), #12 Workshop_B Frame (iron 3.15, oak 2.9, roof tile 2.7) | Chimney stone is the worst: box-project the chimney stack at the stone tile size. Oak and iron frame members: metres-per-tile on the long axis. Confirm the export line (the scan found none). |
| `town/sc_bakery.py` | #6 Part_Sign (gilt 6.8), #21 Part_Oven (brick 2.55) | Sign: gilt plate UV on its own tile, metres-per-tile. Oven: box projection at the brick tile. |
| `town/sc_bell_tower.py` | #7 Tower_Spire, Tower_Belfry (iron 5.9 / 5.5) | Tall thin iron: project by world Y and the face normal axis, not a 0..1 height normalise. |
| `town/sc_smithy.py` | #8 Part_Sign (iron 5.4), #20 Frame (roof tile 2.6) | Sign as bakery sign. Roof tile: per-slope projection onto the slope plane. |
| `town/sc_schoolhouse.py` | #9 Part_Bell (iron 4.1) | Bell is a revolve: cylindrical projection with arc length in metres on U. |
| `town/sc_mill.py` | #11 Shell (roof tile 3.2) | Slope-plane projection as the smithy roof. |
| `town/sc_facade_kit.py` | #19 Kit_LeanTo (oak 2.8), Kit_Chimney (roof tile 2.5) | Source is `FacadeKit.fbx` copied to `Facade.fbx` (see `SOURCES.md`): fix, re-export, re-copy. |
| `town/sc_props_adopt.py` | #10 Basket_Produce (baked set 3.5) | Adopt step: re-unwrap with uniform island scale, repack. The scan found no export line, so locate the writer first. |
| `town/sc_square_dressing.py` | #22 Pot_Broken (baked set 2.65), 3 instances | Re-unwrap the shard islands uniformly. Confirm the export line. |
| `town/sc_garden.py` (+ `sc_cabbage_tex.py`) | #23 GD_VegBed_A / B cabbage (2.85) | Cabbage leaf UV is stretched along the bed: uniform scale on the leaf cluster, or tile the cabbage texture at the bed's metre size. |
| `town/sc_lakeshore.py` | #24 LakeShore Jetty (2.6) | Plank deck: box projection at the plank tile. Rock_Shore_C is a **density** issue (2.6-2.7x texel density), not stretch: out of scope, file separately. |

## Inventory (scanner output, unedited)
| Ranked row | Family / mesh | Material(s) and stretch | Instances (world pos) | Preset | Raw rows | Source FBX (Assets/_Project/Content/Art/Town/...) | Generator (Art/Blender/...) |
|---|---|---|---|---|---|---|---|
| #5 | Workshop_C Frame | sandstone M_FK_ChimneyStone 7.5; oak 2.9; iron 2.7 | 2: (-8.4,-3.5,-56.3), (-51.8,-1.4,-30.9) | B | 6 | Ring1/Workshop/Workshop_C/Workshop_C.fbx | town/sc_workshop.py (likely: no export line) |
| #6 | Bakery Part_Sign | M_BK_Gilt 6.8 | 1: (-16.5,3.9,9.0) | B | 1 | Ring1/Bakery/Bakery.fbx | town/sc_bakery.py (confirmed: exports Bakery.fbx) |
| #7 | BellTower Tower_Spire, Tower_Belfry | M_FK_Iron 5.9 / 5.5 | 2: (5.6,18.8,24.0), (5.6,14.4,24.0) | R+B | 4 | B4/BellTower/BellTower.fbx | town/sc_bell_tower.py (confirmed) |
| #8 | Smithy Part_Sign | M_FK_Iron 5.4 | 1: (-41.4,-1.2,5.6) | B | 1 | Ring2/Smithy/Smithy.fbx | town/sc_smithy.py (confirmed) |
| #9 | Schoolhouse Part_Bell | M_FK_Iron 4.1 | 1: (-40.4,3.6,-32.3) | B | 1 | Ring2/Schoolhouse/Schoolhouse.fbx | town/sc_schoolhouse.py (confirmed) |
| #10 | Basket_Produce | baked set 3.5 | 1: (2.9,0.15,-10.1) | B | 1 | B7/Basket_Produce/Basket_Produce.fbx | town/sc_props_adopt.py (likely: adopt step, no export line) |
| #11 | Mill Shell | M_FK_RoofTile 3.2 | 1: (70.6,-4.3,-59.5) | R | 1 | Ring3/Mill/Mill.fbx | town/sc_mill.py (confirmed) |
| #12 | Workshop_B Frame | iron 3.15; roof tile 2.7; oak 2.9 | 2: (39.0,0.3,-2.8), (53.0,0.2,-23.0) | B | 6 | Ring1/Workshop/Workshop_B/Workshop_B.fbx | town/sc_workshop.py (likely) |
| #19 | Facade Kit_LeanTo; Kit_Chimney | oak 2.8; roof tile 2.5 | 2: (-6.0,1.4,11.6), (-3.1,9.4,9.6) | B | 2 | B4/Facade/Facade.fbx | town/sc_facade_kit.py (exports FacadeKit.fbx, copied to Facade.fbx per SOURCES.md) |
| #20 | Smithy Frame | roof tile 2.6 | 1: (-38.9,1.0,7.4) | B | 1 | Ring2/Smithy/Smithy.fbx | town/sc_smithy.py (confirmed) |
| #21 | Bakery Part_Oven | M_BK_Brick 2.55 | 1: (-8.1,1.3,13.7) | B | 1 | Ring1/Bakery/Bakery.fbx | town/sc_bakery.py (confirmed) |
| #22 | Pot_Broken | baked set 2.65 | 3: (11.5,0.18,-6.4), (-13.4,0.18,5.4), (-2.5,0.18,-16.1) | R | 3 | Square/Pot_Broken/Pot_Broken.fbx | town/sc_square_dressing.py (likely: no export line) |
| #23 | GD_VegBed_A / _B cabbage | cabbage 2.85 | 3 per ranked count, 2 positions listed: (-58.5,-9.9,-71.3), (-54.6,-9.5,-73.5) | B | 3 | Garden/VegBed_A/VegBed_A.fbx; Garden/VegBed_B/VegBed_B.fbx | town/sc_garden.py (likely; cabbage tex: town/sc_cabbage_tex.py) |
| #24 | LakeShore Jetty | 2.6 | 1: (-18.8,-12.4,-83.6) | R+B | 2 | Ring3/Lake/LakeShore.fbx | town/sc_lakeshore.py (confirmed) |

Raw-row total: 6+1+4+1+1+1+1+6+2+1+1+3+3+2 = 33. This matches the STATUS.md "uv_stretch 33".
Excluded: #24 Rock_Shore_C is uv_density (2.6-2.7x texel density), not stretch. Generator: town/sc_lakeshore.py.

## Count verdict
- 33 raw rows are identifiable at family, material and position level. The 33 reconciles exactly with the table above under the code's row rule (one row per instance x flagged material x preset).
- Not independently verified: the per-row TSV is not in the repo, so the row-to-raw-row multiplicities come from the ranked report's material lists, not from the scan output. Re-running HollowmereFaultScan.Scan with SC_SCAN_DIR (Unity editor) would confirm.
- Family count: 12 (Workshop_C, Workshop_B, Bakery, BellTower, Smithy, Schoolhouse, Basket_Produce, Mill, Facade, Pot_Broken, GD_VegBed, LakeShore Jetty).
- Generator column: "confirmed" means the script's export line writes that FBX name; "likely" means only a name match, no export line found.

## Order of work (suggested, one art session)
1. Workshop (4 rows, 12 raw rows), then bakery, smithy, facade (signs and roof tile: same helper).
2. Bell tower and schoolhouse bell (thin iron and revolve: new helper).
3. Baked props (basket, pot) and the garden cabbage.
4. Mill, lakeshore jetty.
5. One `HollowmereFaultScan` run at the end; update `STATUS.md` with the new `uv_stretch` count. Target 0 rows over 2.5.

## Open items
- Several export lines were not found by name match (workshop, props_adopt, square_dressing, garden): grep for the FBX name in `Art/Blender/tools/` before editing.
- The per-instance TSV (`scan_uv_stretch.tsv`) is not in the repo; the 33 comes from the ranked report's material lists and matches STATUS.md. A fresh scan will confirm it.
- Fixing a mesh changes its FBX, so the Hollowmere rebuild regenerates `.unity`/`.mat` output: run one `build` and check per `.claude/rules/session-usage.md`.
