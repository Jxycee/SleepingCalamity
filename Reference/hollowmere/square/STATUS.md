State: IN PROGRESS
Session: Wave 2: Square ground + dressing
Progress: 85%
Sheet: Reference/hollowmere/square/props_sheet_r1.jpg (props beside the approved corner; square captures next)
Blocker: paused by coordinator for usage (resume after the 12:10 UTC reset)

## Done
- Ground generator `Art/Blender/town/sc_square_ground.py` (branch claude/practical-tesla-lqajeo). Reads the plan from HollowmereLayout.cs,
  rasterises the approved corner's cobble_placements.json as occupied (corner untouched), continues the corner's fan grid over the square,
  lane-stub rows across 5 lanes, gutter courses, 5 drains with ring courses, worn edges against the lots. 35,379 new setts + 370 loose,
  1,152 weed slots; LOD0 5.0M instanced tris (the LODGroup drops setts > 10 m to the 44-tri LOD1). SquareBed 35k tris.
  Output: Content/Art/Town/B6/SquareCobbles/ (square_placements.json, SquareBed.fbx). Reuses the corner's 14 variants + CobbleBed set: no new textures.

## In progress
- Dressing props (Sonnet subagent): Bench, Planter A/B, Pot, LanternPost, Bunting parts, Drain + broken Ruined variants, adopting
  Poly Haven CC0 (painted_wooden_bench, planter_box_01-03, planter_pot_clay) through the approved B7 sc_props_adopt pipeline.
- Unity placement (HollowmereBuilder hook + Core dressing table with footprints + path-graph test).

## Checked before building (common practice / existing tools)
- Reused: sc_cobbles.py Layout/course/fill code and family, CobbleSpawner, sc_props_adopt pipeline, sc_fetch_models.py, B10 BannerPole, B7 props, B8 lantern.
- Poly Haven model list searched (api.polyhaven.com) for bench/planter/lamp/bunting/drain: bench + planters + pot adopted; no medieval
  lantern post, bunting or gully grate there, so those three are generated (bunting = pennant/rope parts composed along a catenary in Unity, the usual approach).

## Paused state (2026-10-31)
- Dressing art: 7 of 11 assets built as previews only (--res-scale 0.25). Nothing is exported yet. Generator `Art/Blender/town/sc_square_dressing.py` is committed (WIP).
  Tris: Bench_A 614, Bench_Broken 1,655, Planter_A 5,999, Planter_B 6,000, Planter_Broken 29,090 (over budget: the decimate didn't apply to the boolean mesh; fix before export), Pot_Clay 2,905, Pot_Broken 3,852.
  Not started: LanternPost_A/Broken, Bunting (Rope_1m must run along Blender -X to be Unity +X), Drain_A. No contact sheet yet.
- Unity placement: written and pushed (d5c21bd + 1d3a8a1), but not compiled because the Unity editor was still downloading. Core HollowmereDressing.cs (rows, footprints, Validate) passes its 6 Core tests (sc_core_tests: Validate is empty against the path graph). HollowmereSquare.cs places the ground, the drains, the dressing (greybox until the art is exported) and the bunting. HollowmereBuilder gets one line.
  Resume: setup.sh --wait, sc_cloud_compile.py (--csproj first), unity.sh build, test-edit, then captures, check and merge.

## Unity (fc56874)
- Compile PASS, build PASS, test-edit 598/598 PASS. The square ground follows the layout heights: a 0.5 m correction grid on the spawner, a draped bed copy, residual 0.009 m, 0.000 m inside the corner rect. Spawn: square 35,749 setts in 5.2 s, corner 6,967 in 0.35-0.53 s.
- Decorations: 58 rows. Validate (path graph with 0.25 m clearance, stalls with a 0.4 m margin, lots, landmarks, corner rect) is empty. Waiting for the dressing art export, then captures, check and merge.

## Dressing art (bf9749f)
All 11 exported: Bench_A 614 tris, Bench_Broken 1,676, Planter_A 6,000, Planter_B 5,999, Planter_Broken 5,700, Pot_Clay 2,905 (placed at 1.5x scale), Pot_Broken 4,127, LanternPost_A 1,830 (carries the B8 lantern), LanternPost_Broken 1,724, Bunting 2,732 (6 pennants + rope, composed along a catenary), Drain_A 895.
My props-sheet score: about 26/40. Weak spots: the bench reads as a low settle, the planters have no straps, a pale liner shows at the soil edge.
Next: rebuild with the art, captures in both presets, check.

## Integration (in progress)
Coordinator's quick prop fixes are folded in: mixed planting in the Rebuilt planters and pots (grass, weeds, ivy trailing over the front), the B8 lantern on every Rebuilt post, bunting sag 0.5-0.8 m with a shared fade tint. Built in Unity. Square captures are rendering now (both presets, midday + low sun, two new cameras Cap_HM_SquareSouth/SquareWest). Next: sheets, merge origin/main (townhouse bca6ee8), check, merge.

## Paused (2026-10-31)
The Unity agent was told to finish its current step and push. Open items: the black sticks in the east half (diagnosis pending), the wide green joints (SquareBed may move under the slab), the final captures/sheets, merging origin/main (townhouse bca6ee8), check, merge.
