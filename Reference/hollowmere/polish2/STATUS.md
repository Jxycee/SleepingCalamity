State: IN PROGRESS
Session: Town polish 2: wall stone, merlons, ruined clutter
Progress: 55%
Sheet: round 1 Blender preview (scratch, not committed); in-engine round 1 capture running (WallRun, LowerGate, EastGate)
Needs: Q14 clutter on main (fix 3 builds on HollowmereClutter); Q15 merged first (both touch HollowmereTownWall.cs)
Blocker: none
Archive: no

Job: coordinator notes (1) wall stone tone/coursing, (2) merlon rhythm, (3) Ruined clutter near lanes; max 2 rounds
Branch: claude/hopeful-planck-afz6d9
Status:
- (1) done in sc_town_wall.py: plain rubble over 4 tints (new slots TW_RubbleDark/Warm/Pale, also in HollowmereTownWall TwSlots), wandering course lines, split and pinning stones; patches and dressed ends stay regular.
- (2) done: every merlon varies in width/height/offset/yaw; Rebuilt chips ~1 in 10; new Wall_Straight_A/B_Ruined (chip ~1/3, missing ~1/3) used by the builder in Ruined; gate battlements vary, ruined Shell chips ~1/3.
- All 10 wall FBX + both gates re-exported (budgets met), SOURCES.md updated. New camera Cap_HM_WallRun.
- (3) waiting for Q14: plan is a LaneWrecks pass after AddRuined (Handcart_Broken tipped, RainBarrel_Broken, CrateStack_Broken at lane edges, every row through Q14's Fits = path-graph safe).
Next: review in-engine capture; Q14 lands -> fix 3 + Ruined lane capture; after Q15 lands merge origin/main, regenerate Hollowmere (-nographics), one check, merge.

Score: round 1 not scored yet
SC RESULT: - (no check yet)
Merge commit: -

## Usage waste
- none so far (one preview render agent, one reduced capture)
