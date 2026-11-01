State: IN PROGRESS
Session: Town polish 2: wall stone, merlons, ruined clutter
Progress: 92%
Sheet: Reference/hollowmere/polish2/polish2_final.jpg (reference beside WallRun Rebuilt r3, LowerGate Ruined r2, RuinedLane Ruined r2); earlier: polish2_r2.jpg, polish2_r1_ruined.jpg, polish2_blender_r1.jpg
Needs: -
Blocker: none
Archive: no

Job: coordinator notes (1) wall stone tone/coursing, (2) merlon rhythm, (3) Ruined clutter near lanes; max 2 rounds
Branch: claude/hopeful-planck-afz6d9
Status:
- (1) done in sc_town_wall.py: plain rubble over 4 tints (new slots TW_RubbleDark/Warm/Pale, also in HollowmereTownWall TwSlots), wandering course lines, split and pinning stones; patches and dressed ends stay regular.
- (2) done: every merlon varies in width/height/offset/yaw; Rebuilt chips ~1 in 10; new Wall_Straight_A/B_Ruined (chip ~1/3, missing ~1/3) used by the builder in Ruined; gate battlements vary, ruined Shell chips ~1/3.
- All 10 wall FBX + both gates re-exported (budgets met), SOURCES.md updated. New camera Cap_HM_WallRun.
- (3) done (Q14 merged in): HollowmereClutter.AddLaneWrecks after AddRuined: 4 groups at ring 2 lane edges (tipped Handcart_Broken + leaning RainBarrel_Broken and/or slumped CrateStack_Broken), every row through Q14's Fits (path graph, foundations, landmarks, slope, rows); Ruined 15 -> 24 rows. Q14's density test keeps its 3x rule for the broken twins and caps the wrecks at 4 groups of 3. New test Ruined_HasLaneWreckGroups; clutter tests 15/15 (sc_core_tests). Camera Cap_HM_RuinedLane on the west group.
Next: -nographics regenerate (running), commit, one check, merge.

Score: final ~28/40 (coordinator read r2 at 27/40 and approved). r3 WallRun camera fix: the outer face and varied merlons read (the west face is in shade at this sun). Rounds: Blender preview + 3 in-engine.
SC RESULT: - (no check yet)
Merge commit: -

## Usage waste
- round 1 capture (23 min, first Library import included) lost its Rebuilt half to an OOM kill: both presets in one graphics run exceed the container's memory; one preset per run from now on
