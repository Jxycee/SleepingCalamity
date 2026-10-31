State: IN PROGRESS
Session: Q12. Whole-town look pass (local PC)
Progress: 75%
Sheet: Reference/hollowmere/look/Q12_r2_midday.jpg
Blocker: none

## r0 baseline (main 7909f1e look)
Midday town reads washed: shared crops on Corner_Wide Rebuilt (fountain, foreground cobble) sit +0.07..0.09 value above the approved corner with the same spread (an exposure/ambient offset, not contrast). Sky: saturated cobalt top, lime-teal band at the horizon; no aerial perspective at town distance. Low sun close to the approved frame (town avg .17/.36/.66 vs approved .13/.33/.71).
Approved corner (midday avg) p10/p50/p90 .14/.41/.72 warmth +.28; town midday avg (play cameras) .30/.57/.74 +.15.

## Approach
New HollowmereLook.cs: the corner's ApplyPreset first, then town-only HM_* volume profiles and skies, town-scale fog and ambient scale. The approved corner's profiles and skies are untouched. HollowmereCapture renders look variants in one run (SC_LOOK_VARIANTS).

## r1-r2 (variant runs)
r1 picked the neutral sky tint (lime horizon gone) and the grade exposure -0.85 / contrast 36 / ambient x0.85 / WB +6. r2 (defaults + town pollen and shafts): Corner_Wide Rebuilt midday .18/.47/.72 +.24 vs approved .20/.45/.67 +.24; low sun .12/.36/.70 +.38 vs approved .13/.33/.71 +.42. Shafts over the open square and Market Street floated in the sky: dropped, gate cones narrowed. Overview haze 0.005 went milky: 0.0025.
Merged origin/main (94b7fb2); final capture and check running.
