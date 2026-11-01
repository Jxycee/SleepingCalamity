# Saltwake road 2: RoadOut view, haze band, low-sun ridges: status

Session: Saltwake road 2 (Q15 follow-up) (resumed in cloud session claude/vibrant-bell-ujeedj)
Job: fix Q15's three accepted gaps plus the low cameras' bare horizon: (1) RoadOut Rebuilt view blocked by a house; (2) flat haze strip behind the tree line; (3) low-sun ridges flat orange; (4) low town cameras (Cap_HM_Ruins) get a backdrop tree line
Progress: 100%
Status: DONE
Blocker: none
Archive: ready
Branch: claude/vibrant-bell-ujeedj (resumes claude/fervent-sagan-bzpbbq, WIP merged in cb750c1)
Score: r1 APPROVED by the coordinator ~26-27/40 (my own read 27/40); no more rounds
Sheet: Reference/hollowmere/road2/road2_final.jpg (reference column + RoadOut/RoadBack/Ruins, Ruined midday / Rebuilt midday / Rebuilt low sun)
SC RESULT: SC RESULT check PASS on fbc1f04 (build PASS; test-edit 904/904, test-play 61 passed / 8 skipped / 0 failed); merge tree == checked tip plus main's handoff-only commit dcdf91d (CURRENT.md, outside Assets)
Merge commit: a6b72f2 (main, no-ff)

## Notes (2026-11-01)
- The house in the RoadOut Rebuilt frame is R3_01 (M lot, 22 m along the Saltwake Road, south side, BuildingKind.Home), not R3_02 (already an allotment in RebuiltGardens). It sits on the camera's exact bearing (about 24 deg south of west from the camera).
- Plan: re-aim/re-seat Cap_HM_RoadOut so the road down the fall holds the centre and R3_01 frames the left edge (no layout change, R3 lots untouched); Backdrop shader gets a height gradient, world-space noise in the haze and a view-vs-sun haze hue plus a sky-tinted shadow side.
- 2026-11-01: PARKED by the coordinator (session 6), user priority M5 floor contents. Round 1 work (camera re-seat, Backdrop shader height gradient + noise + sky-tinted shadow side + view-dependent haze hue) is pushed WIP; see the line below for its branch and verification state.
- WIP 8eb1f53: Cap_HM_RoadOut pos (-28,1.7,-71) target (-85,-12,-82) (not yet checked against R3_01 and the gate piers); SC_Backdrop gets _MistScale, _NoiseAmp, _NoiseLit, _ShadowTint, _HazeCool, _HazeCoolAmt with per-layer values in HollowmereRoad.Backdrop(). Resume: compile, sc_shader_check, then round 1 captures.
- 2026-11-01: RESUMED in a new cloud session (branch claude/vibrant-bell-ujeedj). Round 1 brief: verify the camera re-seat against R3_01, close any ring/MidHills gap behind the band, foot-to-crest haze gradient + noise, per-layer value steps at low sun, and a clumped backdrop tree belt (170-260 m, lake gap) in SaltwakeRoadPlan for the low town cameras. Captures RoadOut/RoadBack/Ruins, one preset per process. Merge waits for the merge lock: after M5, procgen items 2 and 4, floor art and Polish 3b (coordinator, session 6).
- r1 (926d581, merged into this branch): the flat strip was skybox through the MidHills/FarRidge annuli, whose triangles faced down (every slope below eye height culled); winding flipped, MidHills foot pulled to 560 m. Haze steps 0.56/0.72/0.84 (trees/mid/far) with foot mist on the mid hills; low-sun cool hue luminance-normalised per layer. Cap_HM_RoadOut target (-84,-12,-73): R3_01 now in the left quarter, road centred, no lot moved. Horizon belt SaltwakeRoadPlan.Belt (clumps of 1.4-2.4x trees 165-270 m, lake gap at azimuth -95 +/-48) + Core test; Core 403/403, compile PASS, SC_Backdrop shader check OK.
- r1 read: strip gone, ridges read as rolling layers with lit/shadow sides; low sun now steps warm trees / grey-blue mid / violet far. Weak: RoadOut's right edge is a gate-pier slab; the far ridge's low-sun violet too lilac; midday layers still pale; from Cap_HM_Ruins the belt sits below the far ground (west fall) and barely reads. Round 2 targets those.
- r1 APPROVED by the coordinator (session 6), no more rounds; round 2 agent stopped before any commit. Final sheet road2_final.jpg. Noted, not now (PARKING_LOT): at low sun the R3_02 house face and the gate bank slab go saturated orange.
- Next (my turn after procgen item 4 and Polish 3b): merge origin/main, regenerate Hollowmere with a -nographics build, one `unity.sh check`, merge on PASS, STATUS DONE + Archive: ready.
- 08:44 UTC: origin/main merged once more (procgen item 3, 81e0281); scenes regenerating with the builder (-nographics), then one check.
- DONE: merged to main as a6b72f2 after check PASS on fbc1f04 (main merged incl. procgen 1d and 3, scenes regenerated -nographics). Session stops; Archive: ready.
