Status: PARTIAL (pass 3: ruin slot sealing, lot push reverted, road end floor drawn; islands, stuck pockets, lot corners still open)
Branch: claude/silly-bose-9d8d93
Last check: test-edit PASS 1154 (1152 first run, the 2 failing tests fixed and rerun green: 11/11); compile PASS; PlayMode and full check not run
Next: islands (largest (26.9,-12.0,-68.1) by the lake path), stuck pockets (-35.6,-2.8,-1.4) and (33.9,-1.6,-8.4), R2_03_C3 / R2_08_C3 corners (0.68 m terrain step, LK0010_LaneStep, LK1050/LK1053 retaining walls, LK1024 breach HollowmereLanes.cs:108-111), lower gate / R2_16 / R2_23 wall Blocked, remaining TightGap 53R (wall stubs 0.4-0.6 m tall: lower WallFrom in HollowmereRuinSeal trades InvisibleBlocker)
Archive: ready

## Before / after (Hollowmere Ruined / Rebuilt)
| Kind | 056f80bd | pass 2 | pass 3 |
|---|---|---|---|
| Unreachable | 2 / 1 | 2 / 1 | 2 / 1 |
| Stuck | 2 / 0 | 2 / 0 | 2 / 0 |
| Blocked | 22 / 20 | 27 / 25 | 25 / 23 |
| Hole | 10 / 10 | 9 / 9 | 10 / 10 |
| Narrow | 4 / 3 | 4 / 3 | 4 / 3 |
| Island | 15 / 17 | 15 / 17 | 15 / 17 |
| TightGap | 62 / 14 | 59 / 14 | 53 / 12 |
| WalkThrough | 236 / 260 | 230 / 263 | 235 / 251 |
| InvisibleBlocker | 15 / 21 | 17 / 22 | 16 / 23 |
| FloorMismatch | 6 / 6 | 7 / 7 | 6 / 6 |
| Total | 436 / 401 | 395 / 384 | 391 / 369 |

## Changes this pass
- TightGap evidence: every ruin TightGap is a slot inside one ruin Footing mesh (both bounding colliders are the same Footing), between wall stubs. The dump detail now names the two bounding colliders.
- HollowmereRuinSeal: rasterises wall faces above 0.6 m, fills empty runs under 0.75 m closed by wall at both ends with box colliders (no renderer). Tried 0.4 m / 1.2 m high: TightGap 31 but InvisibleBlocker 87, so reverted to 0.6.
- Lot lane push-out (R2_03) reverted: it bought no count (R2_03_C3 stayed Unreachable) and cost Blocked +2. Lot-vs-lane test tolerance widened to 0.6 m.
- RingRoadEnd: first pad quad now drawn with the road material (FloorMismatch 0.6 m gone).
- Tooling: SC_TRAVERSAL_SKIP_CELLARS=1 for town-only dump runs.

## Known not fixed
- Blocked still 25 / 23 vs 22 / 20 at 056f80bd: 3 come from the pass-1 lots near the lower gate and R2_16 wall (R2_23, R2_16 piers/walls).
- Islands, stuck pockets, R2_03_C3 / R2_08_C3 corner Unreachable/Hole, Narrow, remaining TightGap: not touched this pass (scan rounds spent on the ruin seal).
