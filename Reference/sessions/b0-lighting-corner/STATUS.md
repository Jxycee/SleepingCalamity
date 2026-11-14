State: DONE (user approved the B0 sheets 2026-11-14; merged into main, see main's log for the merge commit; check PASS: build, EditMode 1735/1735, PlayMode 255 passed 0 failed 28 skipped, tree d25b5044, marker commit 159a7564 = the STATUS commit above the checked code tip afd052ad)
Branch: claude/b0-lighting-corner-4098b7
Task: build-plan row B0 (docs/superpowers/plans/2026-11-13-area1-dungeon1-build.md)

## Concept palette (sc_ref_palette.py --k 7, 2026-11-14; value = max-channel sRGB; warmth = lights minus darks, same metric the bands use)
| img | p10 | p50 | p90 | warmth |
|---|---|---|---|---|
| H1 square midday | 0.16 | 0.48 | 0.91 | +0.07 |
| H2 square low sun | 0.23 | 0.53 | 0.99 | +0.18 |
| H3 cobble close | 0.18 | 0.44 | 0.76 | +0.14 |
| C1 corridor | 0.07 | 0.24 | 0.74 | +0.52 |
| C2 larder | 0.07 | 0.23 | 0.49 | +0.35 |
| C3 wine vault | 0.06 | 0.40 | 0.78 | +0.46 |
| C4 coal cellar | 0.05 | 0.19 | 0.42 | +0.30 |
| C5 checkpoint | 0.11 | 0.24 | 0.58 | +0.34 |
| C6 church entry | 0.05 | 0.14 | 0.45 | +0.15 |
| C7 oven maze | 0.10 | 0.35 | 0.73 | +0.38 |
| C8 combat room | 0.11 | 0.36 | 0.65 | +0.42 |

DC5 reading (proposal, decided only when the user chooses): no concept reaches p10 0.12, so neither band fits as written. Lit rooms (C3, C7, C8): p50 0.35-0.40, p90 0.65-0.78 (the brighter band) with p10 0.06-0.11. Corridors and dark rooms (C1, C2, C4, C5, C6): p50 0.14-0.24 (PHASE1's darker band). Proposed split: lit rooms floors 1-5 take brighter p50/p90 with p10 0.06-0.11; corridors, floors 6-7 and rolled dark floors take the darker band. Town: H1 warmth +0.07 sits below the +0.12 band; H2/H3 inside it.


## Built (branch claude/b0-lighting-corner-4098b7, tip afd052ad)
Town: Morning and Evening presets (HDRI sky K01 turned to the sun, Exponential fog 0.0048/0.006 from the horizon, cloud-shadow cookie 220 m on the BeautyCorner sun, post per table), sun-elevation capture 54/46/38, T01 cobbles. Cellar: C01 brick on vault, C02 floor, lamp recipe and warm ambient/post, wet patch, C15 sconce. Sky ambient is AmbientMode.Skybox; the APV bake was NOT done (proof file dropped by the builder, no determinism evidence): raise with the user.
Art rounds used: 3 (round-1 numbers below, then deeper town shadows and brighter cellar, then cookie fix, tan brick, dark-iron sconce, contrast cobbles).
Fab download method (Epic Launcher, settings Texture Set + Mid, vault path) is in the build plan "Fab download method".

## Palette (value p10/p50/p90, warmth)
Round 1 -> final. Town Morning Wide 0.32/0.59/0.80 +0.14 -> 0.20/0.50/0.76 +0.22; Mid 0.24/0.53/0.81 -> 0.14/0.42/0.78; Close 0.25/0.67/0.83 -> 0.14/0.61/0.80; Evening Wide 0.33/0.53/0.81 +0.07 -> 0.27/0.43/0.86 +0.19. Cellar Wide 0.09/0.12/0.38 +0.21 -> 0.10/0.27/0.68 +0.29; Mid 0.09/0.11/0.40 -> 0.10/0.24/0.63 +0.28; Close 0.10/0.23/0.73 -> 0.13/0.34/0.87 +0.29. Town p90 stays 0.76-0.78 (cream walls cap it).

## Scores (/40, reviewer, rubric; user reads about 8-10 higher)
Round 2: town 25/25/26/26 (Wide, Mid, Close, Evening), cellar 24/21/19. Final: town 26/26/26/25 (103/160), cellar 23/23/21 (67/120). Town meets the 25/40 target; cellar Close 21 and Wide 23 are under it. Biggest defects: town plaza cobbles flat orange-tan with no stone-to-stone variation or cast structure; cellar brick still pink-salmon with noisy micro-pattern, chunky niche bricks in Close.
Sun elevation 54/46/38: nearly identical at sheet size; reviewer picks 38 (longest shadows) with 46 second, low confidence; design default 42.

## Landed
Approved captures: Reference/hollowmere/approved_corner/ (Corner_*_Morning, Corner_Wide_Evening) and Reference/cellars/approved_corner/ (first Cellars row). Still open from the sheets: DC5 split proposal, APV bake skipped, UndercroftStair.unity not captured, sun elevation left at the design 42 (reviewer favoured 38, then 46), Fab download method recorded in the build plan.
Archive: ready
