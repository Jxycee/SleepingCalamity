State: DONE
Session: Q12. Whole-town look pass (local PC)
Progress: 100%
Sheet: Reference/hollowmere/look/Q12_final_midday.jpg
Blocker: none
Archive: ready

Score: 28/40 (sc-asset-review rubric on the final sheets, play cameras only): silhouette 4, proportion 3, form 3, surface 3, colour/value 4, wear 3, fits the set 4, emissive/efficiency 4. Coordinator read r2 at ~27/40; r3 removed the sky-shaft artefacts. Lighting-only scope: the rest is content (notes below).
SC RESULT check PASS (build PASS, test-edit 681/681, test-play 32 passed / 0 failed / 8 skipped) on 537125f (merged tip; main since then only gained a handoff commit).
Merge: 0331588 on main (merge commit, pushed).
Sheets: Q12_final_midday.jpg, Q12_final_lowsun.jpg ([approved Corner_Wide | Ruined | Rebuilt] per camera); numbers in Q12_final_palette.csv.

## Final numbers (p10/p50/p90 warmth)
Corner_Wide Rebuilt midday .18/.47/.71 +.24 (approved .20/.45/.67 +.24); low sun .12/.36/.69 +.38 (approved .13/.33/.71 +.42). Rebuilt midday play cameras p10 .13-.22, p50 .42-.50, p90 .68-.75 (r0 .16-.27 / .51-.64 / .70-.78). Ruined open views stay brighter (p10 .26-.40): flat pale ground and big sky, a content limit, not graded away (sc-scene-look greybox lesson).

## What changed (HollowmereLook.cs, look hooks only)
- Town profiles HM_Day_Profile / HM_LowSun_Profile (copies of the corner's): midday exposure -0.70 -> -0.85, contrast 30 -> 36, white balance +6, ambient x0.85; low sun WB +8.
- Town skies M_HM_Sky_*: neutral tint, thickness 1.0 (kills the lime-teal horizon band), midday sky exposure 1.3.
- Haze: midday exp2 0.016 -> 0.012 toward the pale horizon (0.78/0.80/0.84); low sun 0.011 warm; the overview gets its own thin haze (0.0025 / 0.003) instead of none.
- Pollen over the square, Market Street, both gates, keep stairs and lake shore; god-ray cones through the two gate arches and the keep stair (none against open sky).
- The approved corner's BeautyCorner_* profiles and M_BC_Sky_* are untouched. HollowmereCapture: SC_LOOK_VARIANTS renders look variants in one run; particles are settled before capture.

## Art issues for later (not touched here)
- Q8 wall and gatehouses: one even stone tone over the whole run; no value break between courses, quoins and wall face (LowerGate/EastGate/LakeShore).
- Overview: the meadow terrain is a flat bright lime and carries the frame (.51/.59/.75 with or without haze; r0 without fog .53/.62): meadow albedo/terrain layer darker and more varied, not more haze.
- Market Street: cobbles too uniform pale grey; the lane needs edge dressing.
- Ruined square: huge empty pale cobble field; dressing, not grade.
- Q6 fence stacking (from the brief): not in the fixed capture set, not checked here.
- Bare horizon: Q15.

## Usage waste
1. Baseline run lost to LFS pointer stubs in a fresh worktree (texture import error). Fix: `git lfs checkout` as the first step in every new worktree.
2. One Sonnet agent resumed for rounds 2-3 and the check: every resume re-read its whole transcript. Fix: fresh short-brief agent per step.
3. `python -I` fails (numpy only in the user site) and a custom sheet script was written. Fix: plain `python` for sc_ref_palette.py; use sc_ab_sheet.py.

## History
## r0 baseline (main 7909f1e look)
Midday town reads washed: shared crops on Corner_Wide Rebuilt (fountain, foreground cobble) sit +0.07..0.09 value above the approved corner with the same spread (an exposure/ambient offset, not contrast). Sky: saturated cobalt top, lime-teal band at the horizon; no aerial perspective at town distance. Low sun close to the approved frame (town avg .17/.36/.66 vs approved .13/.33/.71).
Approved corner (midday avg) p10/p50/p90 .14/.41/.72 warmth +.28; town midday avg (play cameras) .30/.57/.74 +.15.

## Approach
New HollowmereLook.cs: the corner's ApplyPreset first, then town-only HM_* volume profiles and skies, town-scale fog and ambient scale. The approved corner's profiles and skies are untouched. HollowmereCapture renders look variants in one run (SC_LOOK_VARIANTS).

## r1-r2 (variant runs)
r1 picked the neutral sky tint (lime horizon gone) and the grade exposure -0.85 / contrast 36 / ambient x0.85 / WB +6. r2 (defaults + town pollen and shafts): Corner_Wide Rebuilt midday .18/.47/.72 +.24 vs approved .20/.45/.67 +.24; low sun .12/.36/.70 +.38 vs approved .13/.33/.71 +.42. Shafts over the open square and Market Street floated in the sky: dropped, gate cones narrowed. Overview haze 0.005 went milky: 0.0025.
Merged origin/main (94b7fb2); final capture and check running.
