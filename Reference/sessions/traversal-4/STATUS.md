Status: DONE (merged 40de7684 on 2026-11-02; follow-up traversal-5 merged 5d87a82b). Archive: ready
Branch: claude/traversal-4 (from origin/main 66c4b595)

## Counts (Hollowmere Ruined / Rebuilt, TraversalScan.RunAll, town only)
| Kind | before | after |
|---|---|---|
| Unreachable | 2 / 1 | 0 / 0 |
| Blocked | 24 / 22 | 10 / 8 |
| Hole | 10 / 10 | 8 / 8 |
| Island | 16 / 17 | 16 / 17 (unchanged) |
| Stuck | 0 / 0 | 0 / 0 |
| Total issues | 360 / 331 | 334 / 306 |
Baseline.json: Hollowmere unreachable lists emptied, blocked 25/23 -> 10/8 (real removals of non-places, see below). Cellars untouched.

## What changed
Only the scanner (`TraversalSpaces.Hollowmere`), no builder or scene change, so there is nothing to rebuild (no .unity/.prefab diff). New `TraversalPointProbe` (SC_TRAVERSAL_POINTS) lists every collider over a point.
A lot corner node is now not a target, and its ring edges are not intended paths, when it is not a place a hero can stand:
- a capsule probe at the node hits solid geometry (7 corners per preset: R2_01_C2, R2_03_C0, C2, C3, R2_04_C3, R2_16_C2 plus the wedged one below), or
- it is in the explicit `WedgedCorners` list.
Probe evidence:
- R2_03_C3 (-36.9,-24.5): the corner lies inside R2_02's 1 m yard margin; R2_02's pad (-3.52) wins in HollowmereLayout.Ground, so the terrain is 1.9 m above the corner node's planned height (-5.49). Overlapping neighbour yards at different levels, not a missing fill (the earlier corner-pier attempt was the wrong cause). Not a place.
- R2_08_C3 (34,-7): flat pad, but a 0.5 m crack between lot edge and the F09 terrace retaining wall (LK0975-0978, 3.5 m high) with a 1.6 m bank 1.5 m north. No route leads in. Dropped by name.

## Not fixed (reasons)
- Blocked left (10): R2_23 C1/C2/C3 and R3_03_C2 edges: the lot R2_23 (M, 8 m) sits in the 8.8 m gap between the Lower Gate east pier and the town wall End_End, so its yard ring runs through them. Real but a lot placement change (size/position of R2_23, clutter and Rebuilt building class follow), left for the layout owner. Also stair/lane steps (MarketStreet_3, R2_13_South: LaneKit, other session), Keep W stair foot, KeepHall door (Ruined shell collider on the door line; likely real, not investigated).
- Hole (8): two at (-150,-18.7) are past the terrain's west edge (OuterFall plan, no collider there): false positive. The rest sit on pad edges where the smooth planned Ground() differs from the 0.58 m heightmap by 1-2 m within 1 m (R2_03/R2_08/R2_01 pad margins, keep terrace edge): sampling artefacts under retaining walls.
- Island (16/17): 14 are 1.5-3.3 m2 slivers of ground on terrace banks and the ditch (noise above the 1.5 m2 threshold). Three real-looking: 17.7 m2 in the ditch outside the town wall (26.9,-12,-68.1) and 32.5 m2 pocket (-38.4,-2.8,-1.6) not probed; KeepHall interior 24.7 m2 is cut off because its door is Blocked (same shell collider). Not touched.
- Cellars not rescanned.

## traversal-5 (branch claude/traversal-5-r223)
R2_23 moved up the Lower Lane: AlongRows `("R2_23", 2, "LowerLane", 10f -> 5f, -1, M)` in `HollowmereLayout.cs`. At s 10 the lot (a 125 deg diamond) had corners through the Lower Gate east pier and past the town wall line; at s 5 it clears the pier by about 6 m, keeps the full 1 m lane setback, stays inside LotWallLimit (farthest corner 25.6 vs 26.6) and is 1.1 m from R2_16. The lane through the gate is untouched (full width). Size stays M, so the ruin kit, Workshop_C fill, clutter and Rebuilt class are unchanged.
| Hollowmere Blocked | before (Ruined / Rebuilt) | after |
|---|---|---|
| Blocked | 10 / 8 | 7 / 4 |
All four R2_23 edges per preset are gone. One new Ruined entry: R2_16_C2 -> R2_16_North (0.31 m, wall piece M029_StraightB). The town wall used to end at R2_23 (End_End at x -8); it now runs on to R2_16, whose corner C2 sits against the wall. It is R2_16's own clearance, hidden before; not touched (another lot, left for the layout owner). Baseline.json blocked: Ruined 10 -> 7, Rebuilt 8 -> 4.
Golden moved: `LaneKitPlanTests.Plan_IsDeterministic_AndMatchesTheGolden` (rows 1098 -> 1090, hash 1145032234 -> 3206696715): the lane kit plan reads lot positions, so moving R2_23 changes the rows beside it. HollowmereLayoutTests and the Ring2 fill goldens pass unchanged.
