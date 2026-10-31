Session: Q12. Whole-town look pass (local PC)
Job: Cellars variety foundations (asked by the user directly, 2026-10-31)
Progress: 30%
Status: implementer running on claude/cellar-variety
Needs: none
Blocker: none
Archive: no

Session summary for the coordinator:
- Job A, M5 Cellars floor generator: DONE, merged (28b3fde stage 1, 657a67f stage 2, 40a9c0c stage 3). Report: Reference/cellars/floors/STATUS.md.
- Job B, art polish: DONE, merged 85d2c7e (fence, scarecrow, stair) and 4ada664 (weir foam parameter pass). Report: Reference/hollowmere/polish/STATUS.md. Q14 sacks skipped (Q14 not merged).
- Research for the user: docs/research/procedural-dungeons.md merged (214e0c2, second pass ddb515b).
- Now: Cellars variety foundations, greybox, refine later. Layers: optional dressing sockets (fill chance), socket placement jitter, per-floor atmosphere roll (rare dark floor ~1/12, never floor 1), vignettes with a per-floor budget and pity counter (4-6 starters), stair exit minimum distance from entry, sameness tests. All pure in (seed, floor), so CellarFloorSync needs no change.
- Next: review (reviewer), one check, merge, then this file goes DONE and the session is ready to archive.
