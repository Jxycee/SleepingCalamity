# Hollowmere layout: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

State: IN PROGRESS (ruin integration)
Progress: 30%
Sheet: Reference/hollowmere/layout/Hollowmere_Captures_Sheet.jpg
Map: Reference/hollowmere/layout/Hollowmere_Map_Town.jpg

**Wave 0 greybox:** merged to main as `1f6ff88` (`check` PASS on main).

**Terrain pass (user feedback 2026-10-31):** heights are in the layout data (`Foundation.Y`, `Landmark.Y`, `Lane.Heights`, path node `Y`; metres relative to the square). The square shelf is 0 and the keep terrace +4. The Ring Lane is one 3 m terrace down on the flanks (-3.5) and spirals down to a second terrace on the south (-6.5). The town wall stands on a raised bank with a dry ditch outside it (-9 to -12). The lake is a basin with water at -12 (bed -16.5), the stream runs in a ravine, and the south slope falls to the lake. Every lot is a flat pad at its lane's height (cut and fill walls in the builder). Lanes are relaxed to 17 deg or less. Validation adds a 20 deg rule (stairs exempt) and requires doorsteps at the lot's height. The keep wall gets buttresses, planters with ivy and a second (west) stair. Foundations have not moved.

| Item | State |
|---|---|
| Heights + validation + tests (22) | done; `sc_core_tests` PASS |
| Elevation-shaded map | done (`Hollowmere_Map_Town.jpg`, `Hollowmere_Map_Square.jpg`) |
| Ground as a Unity Terrain (controller, 2026-10-31) | done: `HollowmereTerrain` writes the layout heights with `TerrainData.SetHeights` (513, 0.59 m texels); the custom mesh is gone |
| Terrain Tools erosion | done: `com.unity.terrain-tools` 5.3.3 added; its Perlin noise and hydraulic and thermal erosion ran in a cloud graphics run (llvmpipe compute) and are baked to `Content/Terrain/Hollowmere/Hollowmere_Eroded_513.r16` (seed 1031, max 1.55 m change, open ground only). The build blends the RAW in outside the protect mask (shelf, pads, lanes, walls, bank, ditch), so pads stay exact and the build needs no GPU. Rebake: `unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereTerrain.Bake`. The eroders are internal to the package, so the bake calls them by reflection (pinned to 5.3.3) |
| Terrain layers | grass and dirt (flat greybox tints; no B1 grass or soil tileable exists yet) and the B1 cobble under the lanes; the square keeps the corner's own surround paving as a slab |
| Builder: terrace walls, cut and fill, keep-wall breakup, stairs | done |
| Captures, both presets | done: `captures/` (HomeDoor, Fountain, TitheGate, LaneMouth, LakeShore, MarketStreet, KeepStairs, KeepWall, LowEast, Overview, Corner_Wide) |
| `check`, merge | `SC RESULT check PASS` on 591df06 (build PASS; test-edit 591/591; test-play 32 passed, 8 skipped, 0 failed). Merged to main as `1e20cb2`; its tree is identical to 591df06 |

**Not done (coordinator note, not blocking):** the bank face and the slope outside the ditch are still smooth (the LowEast view reads as three flat stripes); give them a little of the erosion noise in a follow-up. Grass and dirt stay flat tints until B1 grass and soil tileables exist.

**Blockers:** none.
