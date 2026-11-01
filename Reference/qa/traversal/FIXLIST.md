# Traversal fix list (judged)

From the traversal scan of 2026-11-01 (`REPORT.md` is the raw output; `maps/` has the top-down maps). Every item below was checked against the builder code or a probe, not only the scan. Report first, fix second: none of these were fixed here, because the Hollowmere builders belong to the M6 S3 hub work (`claude/m6-s3-hub`) and the Cellars builders to the procgen session until their work is on main.

Hero capsule from `PlayerCapsule.prefab`: radius 0.35 m, height 1.8 m, step 0.4 m, slope 45°, jump 1.2 m; an opening "fits" at 0.8 m.

After a fix: re-run `bash Tools/cloud/unity.sh exec --method SleepingCalamity.EditorTools.TraversalScan.RunAll --out <scratch>`, then `...TraversalScan.WriteBaseline` so the regression test (`TraversalRegressionTests`) holds the new, lower counts.

## Hollowmere (owner: M6 S3 hub session, then whoever takes town polish)

Positions are world (x, y, z). R = Ruined, B = Rebuilt.

1. **Rebuilt buildings have no colliders (B, critical).** `HollowmereArt` instantiates each building FBX (`Frame`, `Part_Door`, `Part_Porch`, `Part_Oven`, `Part_Anvil`, `Part_Wheel`) and nothing adds a collider (`HollowmereArt.cs:74`; no runtime code adds one either). The hero walks straight through about 20 rebuilt buildings: bakery, inn, homes, keep hall, cistern house and annex, mill (the yellow footprints on `maps/Hollowmere_Rebuilt.png`). Fix: box colliders per wall or a MeshCollider on `Frame` (as `HollowmereTownWall.AddColliders` does), door left as the threshold.
2. **Town-wall stairs lead nowhere (R+B).** `TownWall/M024_Stair` (-27.6, -6.7, -62.6) and `M044_Stair` (60.8, -3.7, -14.9), the `Wall_Stair` module. Probe: the steps rise from the ground (y -6.0) to y -4.0 in 0.18 m risers, then stop 1.2 m below the wall-walk (y -2.8) with a 2.2 m drop past the top. The tread is also too narrow for the capsule, so the NavMesh never covers it. Fix: take `Wall_Stair` out of the town-wall plan (`HollowmereTownWall.cs:50`), or carry it to a walkable wall-walk landing at least 0.8 m wide. `M044_Stair` also blocks the R2_20 lanes (R2_20_East -> R2_20_C2).
3. **Lot R2_16 sits inside the town wall (R+B).** Doorstep `R2_16_North` (4.4, -6.3, -63.7) can't be reached; the lines R2_16_North -> C3 and C2 -> North run through `TownWall/M025_StraightA`, `End_End` and `M027_StraightB` (26 + 20 + 2 samples where the capsule doesn't fit at all). Fix in the ring-2 lot placement against `TownWallOffset` (HollowmereLayout).
4. **Lot R2_03 corner cut off (R+B).** Corner `R2_03_C3` (-36.9, -5.5, -24.5) is unreachable; its lines are blocked by lane step `LK0010_LaneStep`, retaining walls `LK1050/LK1053_RetainWall` and `LK1024_RetainWallBreach` (`HollowmereLanes.cs:108-111`), with a 0.68 m terrain step at the corner.
5. **Clutter blocks lanes and front doorsteps.** `HollowmereClutter` places stacks on path lines: rain barrel F05 on SQ_16 -> SQ_17 (-19.4, -0.1, -16.4, widest fit 0.53 m), rain barrel F09 on SQ_9 -> SQ_10 (19.6, -0.1, -9.5, 0.65 m), rain barrels R2_05 and R2_11, crate stack R2_08 on R2_08_South -> C1 (0.50 m), sack and crate stacks in R2_05/R2_08 (B). In B, a crate stack, sack stack and rain barrel stand in the front doorsteps of R2_23, R2_08 and R2_19. Fix: keep clutter 0.8 m or more off path edges and out of front doorsteps.
6. **Lower gate pier collider is invisible and blocks a lane (R+B).** `LowerGate/Collider_PierW` (`HollowmereTownWall.cs:286`) sits more than 0.3 m from any visible mesh and blocks R2_23_C1 -> R2_23_East (-17.7, -6.9, -62.6).
7. **Keep stairs have no step colliders.** The keep's `Step`, `Rail*` and `Newel*` meshes (`HollowmereTitheGate.cs:152`, `HollowmereBuilder.cs:305-318`) are walk-through. The terrain underneath carries the climb instead, with a 1.35 m step on Keep_StairFoot -> Keep_StairHead (19.2, 3.6, 19.9), a 0.70 m step and holes on the west stair, and 47° slopes. The heads are still reachable by other routes. Fix: collider treads that match the art.
8. **Stuck pockets (R).** A 32 m² pocket at (-35.6, -2.8, -1.4) next to lot F03, 2.6 m below the lane: the hero can drop in but not climb or jump out. A 0.5 m² pocket at (33.9, -1.6, -8.4) by R2_08 (1.62 m). Rebuilt has none (the building fills the lot).
9. **Ruin stubs are walk-through (R).** Ruined lots' `Footing`, `Plinth` and `Walls` meshes have no colliders (`HollowmereBuilder.cs:426`, `:578-579`), so the hero walks through raised footings and wall stubs.
10. **The Saltwake road ends at the edge of the ground.** `SaltwakeRoad_9` (-152, -18.7, -86) is unreachable, with 360 hole samples around (-150, -89): no ground collider there. The road fence rails (`SaltwakeRoadPlan.cs:152`) have no colliders (590 samples).
11. **Colliders bigger than their props.** Rain barrels, crate and sack stacks (`HollowmereClutter`), `Deco_Pot`, `Deco_Lamp`, `Deco_Banner` (`HollowmereSquare.cs:261`) and the heroes' home allotment `FOOTPRINT` box (`HeroesHomeArt.cs:97`) block the hero more than 0.3 m before anything visible. A few make 0.47-0.58 m gaps between flower boxes, barrels and pots that look passable but aren't.
12. **Other walk-through art** (lower priority): low and high retaining walls in places (`LK_RetainWall*`), lane kerbs, the dry fountain, keep core/buttress/bench, bell tower quoins, the beauty-corner barrels, crates and sacks (`BeautyCornerTable.cs:109-115`), lake shore rocks, weirs. Full list in REPORT.md under WalkThrough.
13. **Minor:** 13 (R) / 8 (B) small terrain islands of 1.5-17 m² (largest at (26.9, -12.0, -68.1) by the lake path); 18 terrain slopes of 46-47° on ring-2 lanes; lane-step treads drawn 0.08 m below their colliders on MarketStreet; a 0.69 m step on MarketStreet_3 -> RingLane_10 (lane step `LK0156`).

The cistern-house UndercroftStair, every building door node and 423-424 of the 426 path nodes are reachable in both presets.

## Cellars (owner: procgen / floor-art session)

Scanned floors 1-4 x 50 seeds (12345, 1-49): 200 floors covering every layout preset (Spine 42, Ring 39, Hub 36, Wing 33 floors), every twist (Ambush 11, Thin 13, Crowded 9), 14 dark floors and 2 generator fallbacks.

1. **Chests have no collider (every floor).** `CellarFloorBuilder.cs:251` builds each chest with `Box(..., collider: false)`; the hero walks through every chest and key chest.
2. **Dressing has no colliders (every floor).** Kit pieces placed by `PlacePiece` (`CellarFloorBuilder.Art.cs:427`): barrels, crates, crate stacks, sack piles, jar clusters, wall shelves and racks. The kit prefabs carry none and nothing adds them. Fix: a box from the renderer bounds in `PlacePiece` (skip candles and wall lanterns), or colliders on the kit. Then re-run the scan: once dressing blocks, it may narrow doorways and corridors, which today it can't.
3. **Otherwise clean.** All 3,103 targets on all 200 floors are reachable (entry starts, stair exit, chests, key chests, gate-key carrier, locked door, bakery pieces, healing point, checkpoint, heart bank, baker, warden). Items inside the locked room are reachable once the door opens. No blocked or narrow paths, steps, holes, stuck pockets or islands on any seed.

## Not covered

The UndercroftStair scene, the dive hub (Arena_Dive) and the M6 S3 hub in town (not on main yet) weren't scanned. Jump routes don't count as reachable.
