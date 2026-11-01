# Saltwake road 2: RoadOut view, haze band, low-sun ridges: status

Session: Saltwake road 2 (Q15 follow-up)
Job: fix Q15's three accepted gaps: (1) RoadOut Rebuilt view blocked by a house; (2) flat haze strip behind the tree line; (3) low-sun ridges flat orange
Progress: 20% (round 1 code in a worktree; no captures scored)
Status: PARKED (re-tasked to M5 contents)
Blocker: none
Archive: no
Branch: claude/fervent-sagan-bzpbbq
Score: -
Sheet: -
SC RESULT: -
Merge commit: -

## Notes (2026-11-01)
- The house in the RoadOut Rebuilt frame is R3_01 (M lot, 22 m along the Saltwake Road, south side, BuildingKind.Home), not R3_02 (already an allotment in RebuiltGardens). It sits on the camera's exact bearing (about 24 deg south of west from the camera).
- Plan: re-aim/re-seat Cap_HM_RoadOut so the road down the fall holds the centre and R3_01 frames the left edge (no layout change, R3 lots untouched); Backdrop shader gets a height gradient, world-space noise in the haze and a view-vs-sun haze hue plus a sky-tinted shadow side.
- 2026-11-01: PARKED by the coordinator (session 6), user priority M5 floor contents. Round 1 work (camera re-seat, Backdrop shader height gradient + noise + sky-tinted shadow side + view-dependent haze hue) is pushed WIP; see the line below for its branch and verification state.
