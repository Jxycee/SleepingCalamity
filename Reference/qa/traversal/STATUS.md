# World traversal scan (collision, pinch points, stuck spots)
Status: DONE (scan tool + regression test merged to main on a green check; REPORT.md, maps and FIXLIST.md committed)
Blocker: none
Branch: claude/great-lovelace-r22yqw (merged)
Last check: PASS at 1ea5beb (EditMode 1046/1046, PlayMode 64 passed 8 skipped)
Next: owning sessions work the fix list: FIXLIST.md (Hollowmere items -> M6 S3 hub / town polish; Cellars items -> procgen / floor art)
Session: World traversal scan (cloud)
Job: Headless traversal scan of Hollowmere (Ruined + Rebuilt) and Cellars floors 1-4 x 50 seeds; report first, fix second
Archive: ready

## Issue counts (scan of 2026-11-01, after main at 34ad534)
- Hollowmere Ruined 544 / Rebuilt 614: Unreachable 3/2, StairToNowhere 2/2, Stuck 2/0, Blocked 29/39, Hole 12/12, Narrow 2/2, Step 7/7, Slope 18/18, Island 13/8, WalkThrough 422/437, InvisibleBlocker 28/72, ClutterInDoorway 0/5, TightGap 1/5, FloorMismatch 5/5.
- Cellars F1-F4 x 50 seeds (200 floors, all presets, twists, dark floors, fallbacks): 3103/3103 targets reachable; no blocked/narrow/step/hole/stuck/island; WalkThrough on every floor (chests and dressing have no colliders): 2194 / 2384 / 2422 / 2853.

## Fix list for the owners (details and sources: FIXLIST.md)
- Hollowmere (owner M6 S3 hub, then town polish; no builder edits made here):
  - Rebuilt buildings have no colliders.
  - Town-wall stairs M024/M044 lead nowhere.
  - Lot R2_16 sits inside the town wall.
  - R2_03 corner is cut off.
  - Clutter blocks lanes and doorsteps.
  - The invisible lower-gate pier collider blocks a lane.
  - Keep stair steps have no colliders.
  - Stuck pocket by F03 (Ruined).
  - Ruin stubs are walk-through.
  - The Saltwake road ends at the edge of the ground.
- Cellars (owner procgen / floor art): chests are built with collider: false; dressing kit pieces have no colliders.

## Notes for the coordinator
- Regression test TraversalRegressionTests (EditMode, ~1-2 min): fails only on a newly unreachable target or more blocked/narrow spots than Reference/qa/traversal/baseline.json. When S3 lands (spawn point and hub in town) or the colliders above are fixed, re-run TraversalScan.RunAll and TraversalScan.WriteBaseline in that branch and commit the new baseline.
- Churn seen: each cloud `check` rewrote Hollowmere.unity, BeautyCorner_Hollowmere.unity and CellarFloorTest.unity with float last-digit changes (rotation 7th digit). Not committed. The scan never saves scenes.
