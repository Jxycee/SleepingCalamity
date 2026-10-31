State: IN PROGRESS
Session: Q13. The Undercroft stair: heroes' home to the Cellars
Progress: 60%
Sheet: Reference/hollowmere/undercroft_stair/SHEET_undercroft_stair_r1.jpg
Blocker: none

Score: r1 24.5/40 (target 29.5 = r1 + 5; floor 25)
Tris: CellarStair 14,132 (StairFlight 4,252, StairTurn 3,788, StairDoor 6,092; budget 20k)
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
