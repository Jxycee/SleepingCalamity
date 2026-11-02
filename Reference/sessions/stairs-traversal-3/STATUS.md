Status: DONE after check+merge (3/3 art rounds, ~22/40; traversal job 3 NOT fixed, see open items)
Branch: claude/stairs-traversal-3
Tip: see git log (commit "stairs-traversal-3: nosing material actually rebuilt")

## Art (A9 round 3)
Root cause of the white planks: Flat() was edited in r1/r2 but the saved M_LK_StepNosing.mat was never rebuilt (still 0.906 = gamma of 0.80), so the r2 frame showed the old colour. Fixed colour is now linear 0.22/0.20/0.16 (reads about 0.5 gamma), smoothness 0.10, rebuilt via the builder. Frames: round3/HM_TerraceSteps_*.png, HM_KeepStairs_*.png (Midday).
Verdict: nosings are no longer blown out and the tread/riser rhythm reads, but they are flat grey-brown planks with no stone texture, slightly cool. Acceptable (~22/40 for the flight), not final: a textured stone nosing would be the next step.

## Traversal (Hollowmere Ruined / Rebuilt, TraversalScan.RunAll, town only)
| Kind | before | after |
|---|---|---|
| Unreachable | 2 / 1 | 2 / 1 |
| Stuck | 0 / 0 (scan; earlier notes list 2 pockets as Island) | 0 / 0 |
| Island | 16 / 17 | 16 / 17 |
| Hole | 10 / 10 | 10 / 10 |
| Blocked | 25 / 23 | 25 / 23 |
| Total | 373 / 349 | 373 / 349 |
No traversal change landed. Tried: stone corner-fill pier (1.8 m, solid) under lot corner nodes whose natural ground is >0.3 m below the pad. It changed nothing for R2_03_C3 / R2_08_C3 (still 1.53 / 1.35 m from reachable ground, Hole at the corner), so it was reverted. Terrain Ground() already forces the pad height within 1 m of a lot, so the corner gap is not a missing fill; the cause needs a probe of the collider stack around the corner (retaining walls LK1050/LK1053/LK0010 and the 0.68 m terrain step) and is not cheap.

## Open items
- R2_03_C3, R2_08_C3 unreachable corners and their Holes; 2 big islands (17.7 m2 at 26.9,-12,-68.1; 32.5 m2 pocket at -38.4,-2.8,-1.6; KeepHall 24.7 m2).
- Blocked +3 near lower gate (R2_23 pier W / End_End wall, R2_16 M026 wall): needs lot/wall clearance moves in HollowmereLayout.
- Cellars not rescanned (town only).
