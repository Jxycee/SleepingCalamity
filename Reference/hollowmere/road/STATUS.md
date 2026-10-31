# Q15 Beyond the Lower Gate: the Saltwake road: status

State: IN PROGRESS
Session: Q15. Beyond the Lower Gate: the Saltwake road
Progress: 92%
Sheet: Reference/hollowmere/road/road_scene_r3.jpg
Blocker: none
Job: QUEUE Q15, cloud session, branch claude/upbeat-meitner-64d6hn
Score: r3 APPROVED by the coordinator ~27/40 (final round; my own read 29/40; r2 25 coordinator / 28 mine; r1 24)
Tris: roadside props 606-5,376 per piece (table below); cards 7,093 quads (162 terrain trees + 10 roadside clumps, 4,339 ring trees, 2,582 hedgerow cards), ring + hills + ridge ~45k
SC RESULT: SC RESULT check PASS on 14c9cfb (test-edit 718/718, test-play 33 passed / 8 skipped / 0 failed); main then moved (Q10 weir, polish merge), merged again in cbb1f14: rebuild + re-check running
Merge commit: none yet
Next: -nographics rebuild + one check on the new tip, merge to main if PASS, then State DONE + Archive: ready
Accepted weak points (coordinator): the R3_02 house blocks the RoadOut Rebuilt view; the haze band behind the tree line reads as a flat strip; the low-sun ridges are flat orange.

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

## Art and in-engine rounds (2026-10-31)
- Props (Sonnet, own worktree): Milestone_A 820 / Milestone_B 606 / Shrine 4,144 / Shrine_Ruined 5,078 / Cart 5,056 / Cart_Broken 5,376 / Fence_Rail 816 / Fence_Rail_Broken 856 / Hedge_A 1,792 / Hedge_B 1,842 tris, all inside budget; Blender sheet r2 28/40 self-score (`road_props_sheet_r2.jpg`; my read ~25: hedges lime leaf-boxes, broken cart barely tipped). Far-tree atlas r2 (`far_trees_r2.jpg`): oak, maple, poplar, thorn clump, irregular lobed crowns (r1 read as lollipops in engine).
- In-engine r1 (cap2, greybox-free): 24/40. Hedgerow, fence, ruts and the town behind read well from the road; card trees lollipops; RoadOut aimed flat along the apron, R3_02's house blocking it in Rebuilt.
- In-engine r2 (`road_scene_r2.jpg`, midday, Ruined): 28/40 (composition 4, materials 3.5, colour 3, wear/story 3.5, density 3, atmosphere/horizon 4, scale 3.5, style 3.5). Varied hedgerow trees; a hazy tree line now frames the low cameras' horizon (RoadOut, Ruins); the overview shows hedged field patchwork, ploughed strips and copses round the town, continuing on the ring. Target met (floor 25; r1 below it).
- Capture issue: the r2 run timed out in the Rebuilt reflection-probe bake (Lightmapping.BakeReflectionProbe hung ~90 min after the Ruined frames were written in 2 min). Low-sun run in progress as a retest.
- Weak / for later: hedges use the shared B9 lime leaf atlas (a darker hedge leaf set would help); RoadOut's valley drop reads only as the road falling over a crest; broken cart could tip more.

## Round 3 (coordinator review of r2: ~25/40, one final round)
- (1) Layered backdrop: new `SleepingCalamity/Backdrop` shader (URP, lit by the main light + ambient SH, no scene fog, its own haze toward the fog/horizon colour by distance; DXC check 24/24 variants). Three depths: the ring trees (haze 0.62-0.84), the mid hill ridge at ~1.2 km (0.78-0.85, slight blue) and the far ridge at ~2.3 km (0.88-0.91, bluer); the ring ground matches the scene fog at the terrain edge (no seam). Low cameras draw to 3 km (overview stays 400 m).
- (2) Gate apron: cobble hands over to the dirt road over ~7 m with a ragged edge, then scattered setts in the dirt for ~9 m.
- (3) RoadOut skyline: roadside clumps of 2-3 card trees every 14 m, alternating sides, 20-34 m off the road.
- First r3 Ruined frames look right (layers read; a pale band at the hill foot from ground mist, fixed in c609e3b).
- Capture issue found: in one software-GL process the first frame after the Ruined -> Rebuilt switch stalls until the timeout (3 runs). Workaround: `SC_CAPTURE_PRESET=Ruined|Rebuilt` (HollowmereCapture, 43e2cfb) renders one preset per process. Earlier "timeouts" were also the wrapper's 5 min default when --timeout was dropped.
- Running now (2026-10-31 ~22:00 UTC): r3 captures Ruined midday, Rebuilt midday, Rebuilt low sun (one process each), then the r3 sheet, merge origin/main, -nographics build, check, merge.

## Next
HollowmereTerrain: blend the old erosion RAW on top of OuterFall (Final/Bake), dirt road ruts instead of cobble on the Saltwake Road, field splat; HollowmereRoad builder (art placement, far-tree and hedge cards, ring mesh + road ribbon), cameras Cap_HM_RoadOut / Cap_HM_RoadBack, captures midday + low sun, score, check, merge.

## Usage waste
(filled in at the end)
