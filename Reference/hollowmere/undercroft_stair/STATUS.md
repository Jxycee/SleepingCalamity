State: IN PROGRESS
Session: Q13. The Undercroft stair: heroes' home to the Cellars
Progress: 85%
Sheet: Reference/hollowmere/undercroft_stair/SHEET_undercroft_stair_r2.jpg
Blocker: none

Score: r3 28/40 (r1 24.5, r2 25.5; coordinator cap 3 rounds reached; target 29.5 not reached) (target 29.5 = r1 + 5; floor 25)
Tris: CellarStair 13,500 (StairFlight 3,748, StairTurn 3,724, StairDoor 6,028; budget 20k)
SC RESULT: -
Merge commit: -

## Log
- 2026-10-31: started on branch claude/beautiful-sagan-1xmzq1. Reading the B5 kit generator, DungeonCornerBuilder and the heroes' home layout rows.
- Design done: CONTRACT.md (3 new kit pieces StairDoor/StairFlight/StairTurn in a separate CellarStair.fbx, placement chain, lights, cameras). Next: generator + builder agents in parallel (Sonnet), Unity editor was still downloading.
- Paused by the coordinator (usage) after the design step.
- Resumed by the coordinator; generator + builder agents starting.
- Checked before building (user rule): Blender's bundled stair generators (Archimesh stairs, Extra Objects) don't produce our CV_* slots + wear attribute or the B5 joins, so the stair pieces extend the in-repo B5 generator (same helpers as StairEntry) and the scene reuses DungeonCornerBuilder (room factored into BuildRoom) rather than new tools.
- Builder + capture committed (722ecd5): UndercroftStairBuilder (greybox stair until CellarStair.fbx lands), UndercroftStairCapture, wired into RunAll after DungeonCorner. Not compiled yet: the cloud Unity download had died during the pause (571 MB .part); restarted with resume. Blender stair pieces in progress (Sonnet agent).
- Paused by the coordinator (usage). WIP committed: the stair pieces in sc_cellar_vault.py (p_stair_flight/turn/door, --stair) are partly written (agent stopped while adding the wear rules; not yet rendered or exported; Python syntax OK). Unity download left running (no token use). Next on resume: finish the wear hook, --stair --room review renders, export CellarStair.fbx, compile, captures, score, check, merge.
- Resumed. Builder compile fixed (missing using; sc_cloud_compile PASS); origin/main merged (Q9 keep 7bac761). Unity editor installed.
- Round 1 in Unity (licensed compile PASS; DungeonCorner scene unchanged by the BuildRoom refactor; stair art picked up). Score 24.5: silhouette 3, proportion 3.5, form 3, surface 3, colour 2.5, wear 3, set 3.5, light 3. Fixes for r2: red-maroon cast (all-brick walls + orange fills) -> limewashed/stone walls and the room's paler gold; Top too dark, door out of frame, motes as big squares near the lens; proud wall stones read as brackets + a dark slot along the flight wall top; Bottom tunnel flat orange.
- Round 2 (coordinator notes folded in): limewash walls, stair-only round motes (24, shared M_DC_Mote untouched), paler gold, the room's single DungeonCorner_Profile volume only, top camera inside the house framing the open door, wall-top slot closed. Captures p10/p50/p90/warmth: Top .018/.018/.108/+24, Turn .019/.111/.442/+48, Bottom .024/.112/.271/+46. Score 25.5 (+1, under the 1.5 rule): the door framing gained, but the top is a black void around a spot circle (no house), limewash normal reads lumpy under grazing light, flush wall stones read as black slashes, StairEntry tunnel brick still maroon. Round 3 has concrete fixes (house shell + broad daylight, rubble stone walls, StairEntry tunnel walls to stone); if it doesn't gain 1.5 I stop at the floor and merge.
- Round 3 (last, coordinator cap): stair walls B5 rubble vault stone (proud stones/brick patches removed), StairEntry tunnel walls brick -> vault stone (CellarVault.fbx re-exported; other 11 pieces' tris unchanged; DungeonCorner captures vs previous export: Wide 0.14%, Mid 0.25%, Close 0.0% mean abs diff), house shell around the top camera (limewash, flagstones, oak beams, window) with broad cool window light + door spill, stair-only local volume dropping the vignette to 0.15 at the top (shared profile untouched). r3 captures Top .068/.314/.517/+26 (too bright -> house lights dimmed 9/2 -> 3.5/0.8), Turn .018/.155/.554/+56, Bottom .024/.168/.485/+46. Score 28 (+2.5). Integrating: final capture + unity.sh check running on d95f1f9 (origin/main merged).
