State: IN PROGRESS
Session: Q12. Whole-town look pass (local PC)
Progress: 30%
Sheet: Reference/hollowmere/look/Q12_r0_baseline_midday.jpg
Blocker: none

## r0 baseline (main 7909f1e look)
Midday town reads washed: shared crops on Corner_Wide Rebuilt (fountain, foreground cobble) sit +0.07..0.09 value above the approved corner with the same spread (an exposure/ambient offset, not contrast). Sky: saturated cobalt top, lime-teal band at the horizon; no aerial perspective at town distance. Low sun close to the approved frame (town avg .17/.36/.66 vs approved .13/.33/.71).
Approved corner (midday avg) p10/p50/p90 .14/.41/.72 warmth +.28; town midday avg (play cameras) .30/.57/.74 +.15.

## Approach
New HollowmereLook.cs: the corner's ApplyPreset first, then town-only HM_* volume profiles and skies, town-scale fog and ambient scale. The approved corner's profiles and skies are untouched. HollowmereCapture renders look variants in one run (SC_LOOK_VARIANTS).
