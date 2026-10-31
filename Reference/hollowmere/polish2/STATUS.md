State: IN PROGRESS
Session: Town polish 2: wall stone, merlons, ruined clutter
Progress: 75%
Sheet: round 1 in-engine (Ruined only, scratch): WallRun / LowerGate / EastGate; final sheet committed with round 2
Needs: Q15 merged first (both touch HollowmereTownWall.cs; it also brings SC_CAPTURE_PRESET for one-preset capture runs)
Blocker: none (waiting for Q15 on main)
Archive: no

Job: coordinator notes (1) wall stone tone/coursing, (2) merlon rhythm, (3) Ruined clutter near lanes; max 2 rounds
Branch: claude/hopeful-planck-afz6d9
Status:
- (1) done in sc_town_wall.py: plain rubble over 4 tints (new slots TW_RubbleDark/Warm/Pale, also in HollowmereTownWall TwSlots), wandering course lines, split and pinning stones; patches and dressed ends stay regular.
- (2) done: every merlon varies in width/height/offset/yaw; Rebuilt chips ~1 in 10; new Wall_Straight_A/B_Ruined (chip ~1/3, missing ~1/3) used by the builder in Ruined; gate battlements vary, ruined Shell chips ~1/3.
- All 10 wall FBX + both gates re-exported (budgets met), SOURCES.md updated. New camera Cap_HM_WallRun.
- (3) done (Q14 merged in): HollowmereClutter.AddLaneWrecks after AddRuined: 4 groups at ring 2 lane edges (tipped Handcart_Broken + leaning RainBarrel_Broken and/or slumped CrateStack_Broken), every row through Q14's Fits (path graph, foundations, landmarks, slope, rows); Ruined 15 -> 24 rows. Q14's density test keeps its 3x rule for the broken twins and caps the wrecks at 4 groups of 3. New test Ruined_HasLaneWreckGroups; clutter tests 15/15 (sc_core_tests). Camera Cap_HM_RuinedLane on the west group.
Next: when Q15 lands: merge origin/main, round 2 captures (Ruined: WallRun, LowerGate, RuinedLane; Rebuilt: WallRun), regenerate Hollowmere (-nographics), one check, merge.

Score: round 1 wall self 29.5/40 (was ~28.5): tint spread and broken gate battlements read in engine; WallRun camera was in the ditch (moved onto the bank top); Rebuilt not seen (capture run OOM-killed at ~10 GB on the second preset: round 2 runs one preset per run via SC_CAPTURE_PRESET after Q15 lands)
SC RESULT: - (no check yet)
Merge commit: -

## Usage waste
- round 1 capture (23 min, first Library import included) lost its Rebuilt half to an OOM kill: both presets in one graphics run exceed the container's memory; one preset per run from now on
