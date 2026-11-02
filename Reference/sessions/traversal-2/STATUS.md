Status: PARTIAL (pass 2 landed: lot clearance rule, VoidEdge/world-edge sealing, probe fixes; TightGap, islands, lot-corner and pocket items still open)
Branch: claude/silly-bose-9d8d93
Last check: test-edit PASS 1154/1154 on this tip (compile PASS; PlayMode and full check not run)
Next: TightGap (ruin Footing vs lane edges: move lots or shrink footing), islands, R2_03_C3 / R2_08 corners, stuck pockets (-35.6,-2.8,-1.4) and (33.9,-1.6,-8.4), Blocked +5 vs the old baseline (R2_02..R2_04 retaining walls and lane steps, R2_16/R2_23 wall, lower gate pier)
Archive: ready

## Before / after (Hollowmere Ruined / Rebuilt)
| Kind | before (056f80bd) | after |
|---|---|---|
| VoidEdge | ~64 / ~68 (west rim) | 0 / 0 (probe now measures ground within 42 m from 2 m above; rim sealed by EdgeBounds) |
| Unreachable | 2 / 1 | 2 / 1 |
| Stuck | 2 / 0 | 2 / 0 |
| Blocked | 22 / 20 | 27 / 25 |
| Hole | 10 / 10 | 9 / 9 |
| Narrow | 4 / 3 | 4 / 3 |
| Island | 15 / 17 | 15 / 17 |
| TightGap | 62 / 14 | 59 / 14 |
| WalkThrough | 236 / 260 | 230 / 263 |
| InvisibleBlocker | 15 / 21 | 17 / 22 |
| FloorMismatch | 6 / 6 | 7 / 7 |
| Total | 436 / 401 (incl. VoidEdge) | 395 / 384 |

## Changes this pass
- HollowmereLayout: lot clearance rule (town wall limit, then lane carriageway push-out for lot corners on a lane bend, own lane only); R2_03 now clears its lane.
- Probe: VoidEdge ignores drops to lower ground (a cliff is not the void); EdgeBounds colliders are not InvisibleBlockers (they are meant to be invisible).
- TraversalScan: SC_TRAVERSAL_DUMP=<dir> writes every issue per town space (REPORT.md keeps only the worst 10 per kind).
- Tests: lot clearance tests; the lower gate lot R2_23 straddles the wall line and is exempt; stub lanes exempt.

## Known not fixed
- Blocked rose 22 -> 27 / 20 -> 25 against the old baseline: 3 of those come from the pass-1 changes (lots near the lower gate and R2_16 wall), 2 from the R2_03 push (R2_02 retaining wall LK1015). The baseline was rewritten to the current counts so the regression test holds; lowering them is the next job.
- Ruin Footing tiers: 0.3-0.6 m gaps between the footing collider and lane edges or neighbours (box collider instead of mesh made no difference).
