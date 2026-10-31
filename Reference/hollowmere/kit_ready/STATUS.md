State: DONE
Session: Wave 1: Bakery (L, frame + parts)
Job: Q2: Smithy, schoolhouse and mill (kit-ready buildings)
Progress: 100%
Sheet: Reference/hollowmere/kit_ready/SHEET_q2_unity.jpg
Needs: none

- Merged: 89d361d (Merge claude/sharp-goldberg-vpyt8c) on main.
- Check: SC RESULT check PASS on the branch tip 956dfec (build PASS, test-edit 594/594, test-play 32 passed / 8 skipped / 0 failed); main only gained a STATUS doc since. Reviewer (Sonnet): no blocking findings.
- Smithy: sc_smithy.py, Ring2/Smithy/Smithy.fbx, 31,524 tris, self 27.5/40, on R2_01 in Rebuilt (footprint 7.20 x 5.60, M). Sheet kit_ready/smithy/SHEET_smithy_final.jpg.
- Schoolhouse: sc_schoolhouse.py, Ring2/Schoolhouse/Schoolhouse.fbx, 29,344 tris, coordinator 27/40 + fixes applied, on R2_03 (5.40 x 6.40, M). Sheet kit_ready/schoolhouse/SHEET_schoolhouse_final.jpg.
- Mill: sc_mill.py, Ring3/Mill/Mill.fbx, Shell 24,784 / Frame + parts 31,532 tris, coordinator 27/40 + fixes applied; landmark row: Shell in Ruined, Frame + Part_Wheel/Part_Door in Rebuilt. Sheet mill/SHEET_mill_final.jpg.
- Code: HollowmereArt is now data-driven (Spec per building, TryPlaceBuilding, TryPlaceMill); BuildingKind Smithy/Schoolhouse appended; RebuiltRing2 shared with Q1's stone cottage/workshop; capture cameras Cap_HM_Smithy/Schoolhouse/Mill.
- Notes: the mill's wheel-pit corner overhangs the terrain slope at its landmark row (terrain, not art); the smithy's forge bay reads dark at midday (a warm fill or bigger ember glow is the next visible gain); M_BK_Ember's MotionVectors pass flips between graphics and -nographics runs (committed as the check's version).
- Blockers: none.
