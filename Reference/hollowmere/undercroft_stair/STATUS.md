State: IN PROGRESS
Session: Q13. The Undercroft stair: heroes' home to the Cellars
Progress: 30%
Sheet: none yet
Blocker: none

Score: -
Tris: -
SC RESULT: -
Merge commit: -

## Log
- 2026-10-31: started on branch claude/beautiful-sagan-1xmzq1. Reading the B5 kit generator, DungeonCornerBuilder and the heroes' home layout rows.
- Design done: CONTRACT.md (3 new kit pieces StairDoor/StairFlight/StairTurn in a separate CellarStair.fbx, placement chain, lights, cameras). Next: generator + builder agents in parallel (Sonnet), Unity editor was still downloading.
- Paused by the coordinator (usage) after the design step.
- Resumed by the coordinator; generator + builder agents starting.
- Checked before building (user rule): Blender's bundled stair generators (Archimesh stairs, Extra Objects) don't produce our CV_* slots + wear attribute or the B5 joins, so the stair pieces extend the in-repo B5 generator (same helpers as StairEntry) and the scene reuses DungeonCornerBuilder (room factored into BuildRoom) rather than new tools.
- Builder + capture committed (722ecd5): UndercroftStairBuilder (greybox stair until CellarStair.fbx lands), UndercroftStairCapture, wired into RunAll after DungeonCorner. Not compiled yet: the cloud Unity download had died during the pause (571 MB .part); restarted with resume. Blender stair pieces in progress (Sonnet agent).
