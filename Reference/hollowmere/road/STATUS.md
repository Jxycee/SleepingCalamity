# Q15 Beyond the Lower Gate: the Saltwake road: status

State: IN PROGRESS
Session: Q15. Beyond the Lower Gate: the Saltwake road
Progress: 25%
Sheet: none yet
Blocker: paused by coordinator for usage

## Done so far (2026-10-31)
- Layout: `HollowmereLayout.OuterFall` (the meadow west of x -72 falls about 9 m to the terrain's west edge); the Saltwake Road rerouted past R3_02 in two S-bends down that fall and off the terrain's west edge (drops about 8 m; R3 lots untouched). Core tests 303/303 + 7 new.
- `Core/Town/SaltwakeRoadPlan.cs` + `SaltwakeRoadPlanTests` (7 PASS): roadside hedges/fences (swap sides at the shrine), 3 milestones, the shrine at the second bend, the cart (broken / parked) per preset; field patchwork (rotated brick-bonded grid) with hedgerow cards, hedgerow trees and copses on the terrain (523 hedge cards, 144 trees) and on a backdrop ring to 620 m.
- Art: first draft of `Art/Blender/town/sc_saltwake_road.py` pushed (9753b98): code for Milestone_A/B, Shrine, Shrine_Ruined, Cart, Cart_Broken, Fence_Rail, Fence_Rail_Broken and Hedge_A/B (B9 ivy leaf-mass cards). Never run in Blender yet (syntax-checked only): expect first-run fixes. Not written yet: `sc_road_trees.py` (far-tree atlas), references (Openverse timed out), renders, sheets, scores, FBX exports, Road/SOURCES.md.

## Finding
The town's fog (exp2 density 0.016, from the corner's Midday/LowSun presets) leaves about 8% at 100 m and nothing past ~150 m, so a far backdrop ring alone cannot fix the low cameras' horizon. The plan puts the horizon mass (tree lines, copses, hedgerows) on the terrain 40-140 m out; the ring past the terrain reads only in the fog-free overview or after Q12 thins the town fog. Q12 should know.

## Next (on resume)
HollowmereTerrain: blend the old erosion RAW on top of OuterFall (Final/Bake), dirt road ruts instead of cobble on the Saltwake Road, field splat; HollowmereRoad builder (art placement, far-tree and hedge cards, ring mesh + road ribbon), cameras Cap_HM_RoadOut / Cap_HM_RoadBack, captures midday + low sun, score, check, merge.

## Usage waste
(filled in at the end)
