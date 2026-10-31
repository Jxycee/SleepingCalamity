State: IN PROGRESS
Session: Wave 2: Square ground + dressing
Progress: 25%
Sheet: none (top-down plan: Reference/hollowmere/square/ground_plan_r1.png)
Blocker: paused by coordinator for usage (both subagents told to finish their current step, commit/leave work, and stop)

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
