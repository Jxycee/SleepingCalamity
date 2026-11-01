State: DONE (merged to main 8a2f9b8)
Session: Town polish 2: wall stone, merlons, ruined clutter
Progress: 100%
Sheet: Reference/hollowmere/polish2/polish2_final.jpg (reference beside WallRun Rebuilt r3, LowerGate Ruined r2, RuinedLane Ruined r2); earlier: polish2_r2.jpg, polish2_r1_ruined.jpg, polish2_blender_r1.jpg
Needs: -
Blocker: none
Archive: ready

Job: coordinator notes (1) wall stone tone/coursing, (2) merlon rhythm, (3) Ruined clutter near lanes; max 2 rounds
Branch: claude/hopeful-planck-afz6d9
Status:
- (1) done in sc_town_wall.py: plain rubble over 4 tints (new slots TW_RubbleDark/Warm/Pale, also in HollowmereTownWall TwSlots), wandering course lines, split and pinning stones; patches and dressed ends stay regular.
- (2) done: every merlon varies in width/height/offset/yaw; Rebuilt chips ~1 in 10; new Wall_Straight_A/B_Ruined (chip ~1/3, missing ~1/3) used by the builder in Ruined; gate battlements vary, ruined Shell chips ~1/3.
- All 10 wall FBX + both gates re-exported (budgets met), SOURCES.md updated. New camera Cap_HM_WallRun.
- (3) done (Q14 merged in): HollowmereClutter.AddLaneWrecks after AddRuined: 4 groups at ring 2 lane edges (tipped Handcart_Broken + leaning RainBarrel_Broken and/or slumped CrateStack_Broken), every row through Q14's Fits (path graph, foundations, landmarks, slope, rows); Ruined 15 -> 24 rows. Q14's density test keeps its 3x rule for the broken twins and caps the wrecks at 4 groups of 3. New test Ruined_HasLaneWreckGroups; clutter tests 15/15 (sc_core_tests). Camera Cap_HM_RuinedLane on the west group.
Next: none. Follow-up noted by the coordinator: the dark streaky bank face under the west wall (terrain/bank material, not this job).

Score: final approved by the coordinator at about 27-28/40 (sheet polish2_final.jpg, reference beside the renders)
SC RESULT: check PASS on aded310 (build PASS; test-edit 812/812; test-play 37 passed / 8 skipped / 0 failed); merge tree == checked tip
Merge commit: 8a2f9b8

## Usage waste
- Round 1 capture lost its Rebuilt half to an OOM kill (two presets in one graphics run exceed ~15 GB); fixed by one preset per run (SC_CAPTURE_PRESET).
- The WallRun camera took three placements (ditch, behind the bank face, then high over the bank) because the steep bank outside the west wall wasn't known; reading the ground and bank from the scene before placing it would have saved one capture (~30 min).
- The capture exec hangs after writing its PNGs until unity.sh's timeout (cost ~15 to 30 min per run); one round 2 run was killed by the 30-minute background-task limit.
- The default 5-minute build timeout cut one -nographics build off during reimport (rerun with --timeout 20).
