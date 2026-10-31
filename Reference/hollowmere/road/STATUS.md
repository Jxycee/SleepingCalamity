# Q15 Beyond the Lower Gate: the Saltwake road: status

State: IN PROGRESS
Session: Q15. Beyond the Lower Gate: the Saltwake road
Progress: 45%
Sheet: none yet
Blocker: none

## Done so far (2026-10-31)
- Layout: `HollowmereLayout.OuterFall` (the meadow west of x -72 falls about 9 m to the terrain's west edge); the Saltwake Road rerouted past R3_02 in two S-bends down that fall and off the terrain's west edge (drops about 8 m; R3 lots untouched). Core tests 303/303 + 7 new.
- `Core/Town/SaltwakeRoadPlan.cs` + `SaltwakeRoadPlanTests` (7 PASS): roadside hedges/fences (swap sides at the shrine), 3 milestones, the shrine at the second bend, the cart (broken / parked) per preset; field patchwork (rotated brick-bonded grid) with hedgerow cards, hedgerow trees and copses on the terrain (523 hedge cards, 144 trees) and on a backdrop ring to 620 m.
- Art: first draft of `Art/Blender/town/sc_saltwake_road.py` pushed (9753b98): code for Milestone_A/B, Shrine, Shrine_Ruined, Cart, Cart_Broken, Fence_Rail, Fence_Rail_Broken and Hedge_A/B (B9 ivy leaf-mass cards). Never run in Blender yet (syntax-checked only): expect first-run fixes. Not written yet: `sc_road_trees.py` (far-tree atlas), references (Openverse timed out), renders, sheets, scores, FBX exports, Road/SOURCES.md.

## Finding
The town's fog (exp2 density 0.016, from the corner's Midday/LowSun presets) leaves about 8% at 100 m and nothing past ~150 m, so a far backdrop ring alone cannot fix the low cameras' horizon. The plan puts the horizon mass (tree lines, copses, hedgerows) on the terrain 40-140 m out; the ring past the terrain reads only in the fog-free overview or after Q12 thins the town fog. Q12 should know.

## Resumed (2026-10-31, after the usage pause)
- origin/main merged (Q6, Q8, Q9, Q12 and others; clean). Q12 set the town haze to exp2 0.012 (overview 0.0025): ~4% left at 150 m, so the plan above stands.
- HollowmereTerrain: the old erosion RAW now rides on top of OuterFall (Final adds the fall, Bake stores without it); the Saltwake Road past a 9 m gate apron is painted as a rutted dirt road (worn path, two soil ruts, a grass crown) instead of cobble; the field patchwork painted outside the wall (hay = dry path layer, ploughed = soil furrows, soil bands under the hedgerows).
- New `HollowmereRoad.cs`: roadside art from Content/Art/Town/Road (greybox stand-ins until the FBX land), crossed-card trees and hedgerow cards merged per 100 m tile, the backdrop ring mesh (NaturalGround at the edge rising into far hills) with a generated field-patchwork texture, and a road ribbon past the terrain. Cameras Cap_HM_RoadOut / Cap_HM_RoadBack. Hooks: one line in HollowmereBuilder (+ the camera loop), HollowmereTownWall.BuildMaterials/ApplyMaterials made internal, HollowmereFoliage.FoliageMaterial().
- Art: a fresh Sonnet agent (own worktree) is running and fixing the generator, writing the far-tree atlas, rendering, scoring and exporting.
- Unity editor download had died in the background setup; restarted.

## Next
HollowmereTerrain: blend the old erosion RAW on top of OuterFall (Final/Bake), dirt road ruts instead of cobble on the Saltwake Road, field splat; HollowmereRoad builder (art placement, far-tree and hedge cards, ring mesh + road ribbon), cameras Cap_HM_RoadOut / Cap_HM_RoadBack, captures midday + low sun, score, check, merge.

## Usage waste
(filled in at the end)
